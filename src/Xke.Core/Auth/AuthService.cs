using System.Security.Cryptography;
using System.Collections.Concurrent;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Contracts.Responses;
using Xke.Core.Auth.Jw;
using Xke.Core.Schedule;
using Xke.Storage;

namespace Xke.Core.Auth;

public sealed class AuthService : IDisposable
{
    private readonly Func<string, string, DirectJwLoginProtocol> _protocolFactory;
    private string? _activeSessionId;
    public string? ActiveSessionId => Volatile.Read(ref _activeSessionId);

    private readonly XkeDataRoot _dataRoot;
    private readonly IJwAuthClient _jwClient;
    private readonly ScheduleService _scheduleService;
    private readonly MobileSessionRuntimeStore _mobileSessionRuntime;
    private readonly TraditionalJwSessionRuntimeStore _traditionalJwSessions;
    private readonly ConcurrentDictionary<string, PendingProtocolLogin> _protocolLogins = new(StringComparer.Ordinal);
    private readonly object _connectedSessionsGate = new();
    private readonly HashSet<string> _connectedSessions = new(StringComparer.Ordinal);

    public AuthService(XkeDataRoot dataRoot, IJwAuthClient jwClient, ScheduleService scheduleService, MobileSessionRuntimeStore mobileSessionRuntime, TraditionalJwSessionRuntimeStore traditionalJwSessions, Func<string, string, DirectJwLoginProtocol>? protocolFactory = null)
    {
        _dataRoot = dataRoot;
        _jwClient = jwClient;
        _scheduleService = scheduleService;
        _mobileSessionRuntime = mobileSessionRuntime;
        _traditionalJwSessions = traditionalJwSessions;
        _protocolFactory = protocolFactory ?? ((user, password) => new DirectJwLoginProtocol(user, password));
    }

    public async Task<(string Message, object Payload)> BeginProtocolLoginAsync(LoginRequest request, bool forceRemote = false, CancellationToken cancellationToken = default)
    {
        CleanupExpiredProtocolLogins();
        var user = NormalizeRequired(request.User, "账号不能为空");
        var password = NormalizeRequired(request.Password, "密码不能为空");
        var existing = _dataRoot.Credentials().Load(user);
        if (existing is not null && !forceRemote)
        {
            if (!string.Equals(existing.Password, password, StringComparison.Ordinal))
            {
                throw new AuthException(401, "本地账号或密码不正确");
            }

            try
            {
                var restored = RestoreTrustedWorkspace(existing, NormalizePreloginMode(request.PreloginMode));
                return ("登录成功（已恢复本地工作区）", restored.Payload);
            }
            catch (AuthException ex) when (ex.StatusCode == 409)
            {
                // Saved credentials alone do not constitute an initialized workspace.
                // Rebuild through the school protocol instead of trapping the login form.
            }
        }

        var protocol = _protocolFactory(user, password);
        try
        {
            var image = await protocol.BeginAsync(cancellationToken).ConfigureAwait(false);
            var challengeId = Guid.NewGuid().ToString("N");
            _protocolLogins[challengeId] = new PendingProtocolLogin(user, password, NormalizePreloginMode(request.PreloginMode), existing, protocol, DateTimeOffset.Now.AddMinutes(2));
            return ("需要输入教务系统验证码", new Dictionary<string, object?>
            {
                ["status"] = "captcha_required",
                ["challenge_id"] = challengeId,
                ["captcha_image"] = image,
                ["expires_in_seconds"] = 120,
                ["protocol"] = "jw_form",
            });
        }
        catch (JwScheduleException ex)
        {
            protocol.Dispose();
            throw new AuthException(502, ex.Message);
        }
        catch
        {
            protocol.Dispose();
            throw;
        }
    }

    public async Task<(string Message, AuthPayload Payload)> SubmitProtocolCaptchaAsync(JwCaptchaSubmitRequest request, CancellationToken cancellationToken = default)
    {
        if (!_protocolLogins.TryRemove(request.ChallengeId?.Trim() ?? string.Empty, out var pending))
        {
            throw new AuthException(410, "登录验证码已过期，请重新登录");
        }

        if (pending.ExpiresAt <= DateTimeOffset.Now)
        {
            pending.Protocol.Dispose();
            throw new AuthException(410, "登录验证码已过期，请重新登录");
        }

        using (pending.Protocol)
        {
            try
            {
                await pending.Protocol.CompleteAsync(request.CaptchaCode, cancellationToken).ConfigureAwait(false);
            }
            catch (JwScheduleException ex)
            {
                throw new AuthException(ex.StatusCode is 400 or 401 ? ex.StatusCode : 502, ex.Message);
            }
            var jwSession = pending.Protocol.TransferAuthenticatedSession();
            StudentProfileDocument profile;
            try
            {
                profile = await jwSession.FetchStudentProfileAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                jwSession.Dispose();
                throw;
            }

            var previousJwSession = _dataRoot.JwSessions().Load(pending.User);
            var store = _dataRoot.ForStudent(profile.StudentId);
            using var snapshot = StudentInitializationSnapshot.Capture(_dataRoot, store);
            var credentials = new StoredCredentialDocument
            {
                User = pending.User,
                Password = pending.Password,
                StudentId = profile.StudentId,
                TrustedLoginToken = string.IsNullOrWhiteSpace(pending.PreviousCredentials?.TrustedLoginToken)
                    ? CreateTrustedLoginToken()
                    : pending.PreviousCredentials.TrustedLoginToken,
                UpdatedAt = DateTimeOffset.Now.ToString("O"),
            };
            var sessionId = string.Empty;
            try
            {
                store.SaveProfile(profile);
                var settings = AuthSettingsCatalog.WithPreloginMode(store.LoadPreferences() ?? AuthSettingsCatalog.CreateDefault(), pending.PreloginMode);
                store.SavePreferences(settings);
                _dataRoot.Credentials().Save(credentials);
                sessionId = _dataRoot.Sessions().Create(pending.User, profile.StudentId, pending.PreloginMode).SessionId;
                _traditionalJwSessions.Store(sessionId, jwSession);
                _dataRoot.JwSessions().Save(jwSession.ExportStoredSession());
                var schedule = await _scheduleService.FetchCookieAsync(sessionId, new ScheduleRequest { Mode = "current" }, cancellationToken).ConfigureAwait(false);
                // A successfully parsed empty week is a valid initial schedule.
                _dataRoot.JwSessions().Save(jwSession.ExportStoredSession());
                return ("首次登录成功，已使用教务系统协议完成本地初始化", BuildPayload(sessionId, profile, store, settings, credentials.TrustedLoginToken, schedule.Payload, connected: true));
            }
            catch
            {
                if (!string.IsNullOrWhiteSpace(sessionId)) { _dataRoot.Sessions().Remove(sessionId); _traditionalJwSessions.Remove(sessionId); }
                else { jwSession.Dispose(); }
                if (previousJwSession is null) _dataRoot.JwSessions().Remove(pending.User);
                else _dataRoot.JwSessions().Save(previousJwSession);
                if (pending.PreviousCredentials is null) { _dataRoot.Credentials().Remove(pending.User); }
                else { _dataRoot.Credentials().Save(pending.PreviousCredentials); }
                snapshot.Restore();
                throw;
            }
        }
    }

    public int SessionCount => _dataRoot.Sessions().Count();

    public async Task<(string Message, AuthPayload Payload)> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = NormalizeRequired(request.User, "账号不能为空");
        var password = NormalizeRequired(request.Password, "密码不能为空");
        var preloginMode = NormalizePreloginMode(request.PreloginMode);
        var credentialStore = _dataRoot.Credentials();
        var existingCredentials = credentialStore.Load(user);

        if (existingCredentials is not null)
        {
            if (!string.Equals(existingCredentials.Password, password, StringComparison.Ordinal))
            {
                throw new AuthException(401, "本地账号或密码不正确");
            }

            // Existing local accounts must stay on the local-trust path: verify
            // the persisted credential, then admit only fully initialized local workspaces.
            var restoredWorkspace = RestoreTrustedWorkspace(existingCredentials, preloginMode);

            return (
                "登录成功（本地凭据校验通过，已恢复本地可信状态）",
                restoredWorkspace.Payload);
        }

        return await InitializeRemoteStateAsync(
            user,
            password,
            preloginMode,
            previousCredentials: null,
            successMessage: "首次登录成功，已使用移动教务认证完成本地初始化",
            failurePrefix: "首次初始化未完成",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<(string Message, AuthPayload Payload)> ReinitializeAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = NormalizeRequired(request.User, "账号不能为空");
        var password = NormalizeRequired(request.Password, "密码不能为空");
        var preloginMode = NormalizePreloginMode(request.PreloginMode);
        var previousCredentials = _dataRoot.Credentials().Load(user);

        return await InitializeRemoteStateAsync(
            user,
            password,
            preloginMode,
            previousCredentials,
            successMessage: "重新初始化成功，已刷新本地可信状态",
            failurePrefix: "重新初始化未完成",
            cancellationToken).ConfigureAwait(false);
    }

    public (string Message, AuthPayload Payload) TrustedLogin(TrustedLoginRequest request)
    {
        var user = NormalizeRequired(request.User, "账号不能为空");
        var preloginMode = NormalizePreloginMode(request.PreloginMode);
        var credentials = _dataRoot.Credentials().Load(user);
        if (credentials is null || string.IsNullOrWhiteSpace(credentials.Password))
        {
            throw new AuthException(401, "当前设备尚未建立可信登录环境，请先手动登录一次");
        }

        var trustedToken = NormalizeRequired(request.TrustedLoginToken, "可信登录凭据为空");
        if (!string.Equals(credentials.TrustedLoginToken, trustedToken, StringComparison.Ordinal))
        {
            throw new AuthException(401, "可信登录环境不可用，请重新手动登录");
        }

        var restoredWorkspace = RestoreTrustedWorkspace(credentials, preloginMode);

        return (
            "已进入可信环境",
            restoredWorkspace.Payload);
    }

    public (string Message, string User, AuthPayload Payload) DesktopBootstrap()
    {
        if (ActiveSessionId is { } active && _dataRoot.Sessions().Load(active) is { } current)
        {
            var result = GetSession(active);
            return ("已恢复桌面本地可信状态", current.User, result.Payload);
        }

        var credentialStore = _dataRoot.Credentials();
        var orderedCandidates = credentialStore.LoadAll()
            .Where(document => !string.IsNullOrWhiteSpace(document.User) && !string.IsNullOrWhiteSpace(document.Password))
            .OrderByDescending(document => ParseCredentialUpdatedAt(document.UpdatedAt))
            .ThenBy(document => document.User, StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in orderedCandidates)
        {
            try
            {
                var restoredWorkspace = RestoreTrustedWorkspace(candidate, string.Empty);
                return ("已恢复桌面本地可信状态", restoredWorkspace.User, restoredWorkspace.Payload);
            }
            catch (AuthException ex) when (ex.StatusCode == 409)
            {
                // No complete local workspace: the desktop should show authentication.
            }
        }

        throw new AuthException(404, "当前设备尚未建立可信登录环境，请先手动登录一次");
    }

    public (string Message, AuthPayload Payload) GetSession(string sessionId)
    {
        var context = LoadContext(sessionId);
        var touchedCredentials = TouchCredentialUsage(context.Credentials, _dataRoot.Credentials());
        var cachedSchedule = LoadRequiredCachedSchedule(sessionId);
        return ("会话有效", BuildPayload(sessionId, context.Profile, context.StudentStore, context.Settings, touchedCredentials.TrustedLoginToken, cachedSchedule, connected: _traditionalJwSessions.TryGet(sessionId, out _)));
    }

    public async Task<(string Message, object Payload)> ConnectProtocolAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var context = LoadContext(sessionId);
        if (_traditionalJwSessions.TryGet(sessionId, out _)
            || await TryRestorePersistedJwSessionAsync(sessionId, context.Credentials, cancellationToken).ConfigureAwait(false))
        {
            var result = GetSession(sessionId);
            return ("教务会话已连接", result.Payload);
        }
        return await BeginProtocolLoginAsync(new LoginRequest
        {
            User = context.Session.User,
            Password = context.Credentials.Password,
            PreloginMode = context.Session.PreloginMode,
        }, forceRemote: true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<(string Message, AuthPayload Payload)> ConnectAsync(string sessionId, ConnectRequest request, CancellationToken cancellationToken = default)
    {
        var context = LoadContext(sessionId);
        var requestedMode = string.IsNullOrWhiteSpace(request.PreloginMode)
            ? context.Session.PreloginMode
            : request.PreloginMode;
        var preloginMode = NormalizePreloginMode(requestedMode);

        JwAuthResult result;
        try
        {
            result = await _jwClient.ConnectAsync(
                context.Session.User,
                context.Credentials.Password,
                preloginMode,
                cancellationToken,
                context.Credentials.MobileJw).ConfigureAwait(false);
        }
        catch (JwAuthException ex)
        {
            throw new AuthException(401, ex.Message);
        }

        var profile = EnsureRemoteProfile(result.Profile, "移动教务连接失败：未返回可用身份信息");
        var mobileToken = EnsureRemoteMobileToken(result.MobileToken, "移动教务连接失败：未返回有效 Token");
        if (!string.Equals(profile.StudentId, context.Profile.StudentId, StringComparison.OrdinalIgnoreCase))
        {
            throw new AuthException(409, "移动教务返回身份与本地初始化账号不一致，已保留本地可信状态");
        }

        var studentStore = _dataRoot.ForStudent(profile.StudentId);
        studentStore.SaveProfile(profile);
        var settings = AuthSettingsCatalog.WithPreloginMode(studentStore.LoadPreferences() ?? context.Settings, preloginMode);
        var nextSession = new StoredSessionDocument
        {
            SessionIdHash = context.Session.SessionIdHash,
            User = context.Session.User,
            StudentId = profile.StudentId,
            PreloginMode = preloginMode,
            CreatedAt = context.Session.CreatedAt,
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
        };
        _dataRoot.Sessions().Save(sessionId, nextSession);
        MarkSessionConnected(sessionId, mobileToken);

        var nextCredentials = new StoredCredentialDocument
        {
            User = context.Credentials.User,
            Password = context.Credentials.Password,
            StudentId = profile.StudentId,
            TrustedLoginToken = context.Credentials.TrustedLoginToken,
            MobileJw = result.MobileJw ?? context.Credentials.MobileJw,
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
        };
        _dataRoot.Credentials().Save(nextCredentials);

        var cachedSchedule = TryLoadCachedSchedule(sessionId);
        return ("移动教务连接成功", BuildPayload(sessionId, profile, studentStore, settings, nextCredentials.TrustedLoginToken, cachedSchedule, connected: true));
    }

    public (string Message, AuthPreferencesPayload Payload) GetPreferences(string sessionId)
    {
        var context = LoadContext(sessionId);
        return (
            "偏好已加载",
            new AuthPreferencesPayload(
                Preferences: AuthSettingsCatalog.ToLegacyPreferences(context.Settings),
                Settings: context.Settings,
                StorageDir: context.StudentStore.RelativeDirectory()));
    }

    public (string Message, AuthPreferencesPayload Payload) UpdatePreferences(string sessionId, PreferencesRequest request)
    {
        var context = LoadContext(sessionId);
        var updatedSettings = AuthSettingsCatalog.Apply(context.Settings, request);
        updatedSettings = AuthSettingsCatalog.WithPreloginMode(updatedSettings, updatedSettings.Auth.PreloginMode);
        context.StudentStore.SavePreferences(updatedSettings);

        var nextSession = new StoredSessionDocument
        {
            SessionIdHash = context.Session.SessionIdHash,
            User = context.Session.User,
            StudentId = context.Session.StudentId,
            PreloginMode = updatedSettings.Auth.PreloginMode,
            CreatedAt = context.Session.CreatedAt,
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
        };
        _dataRoot.Sessions().Save(sessionId, nextSession);

        return (
            "偏好已保存",
            new AuthPreferencesPayload(
                Preferences: AuthSettingsCatalog.ToLegacyPreferences(updatedSettings),
                Settings: updatedSettings,
                StorageDir: context.StudentStore.RelativeDirectory()));
    }

    public (string Message, AuthProfilePayload Payload) UpdateProfile(string sessionId, ProfileRequest request)
    {
        var context = LoadContext(sessionId);
        var nickname = (request.Nickname ?? string.Empty).Trim();
        var updatedProfile = new StudentProfileDocument
        {
            StudentId = context.Profile.StudentId,
            Name = context.Profile.Name,
            Nickname = nickname,
            UseNickname = request.UseNickname && !string.IsNullOrWhiteSpace(nickname),
            ClassName = context.Profile.ClassName,
            MajorName = context.Profile.MajorName,
            AcademyName = context.Profile.AcademyName,
            TrainingLevel = context.Profile.TrainingLevel,
            CampusName = context.Profile.CampusName,
            ProgramDurationYears = context.Profile.ProgramDurationYears,
            EnrollmentDate = context.Profile.EnrollmentDate,
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
        };
        context.StudentStore.SaveProfile(updatedProfile);
        return ("昵称已保存", new AuthProfilePayload(BuildStudentSummary(updatedProfile, ResolveAvatarUrl(sessionId, context.StudentStore))));
    }

    public string GetAvatarPath(string sessionId)
    {
        var context = LoadContext(sessionId);
        return context.StudentStore.GetAvatarPath()
            ?? throw new AuthException(404, "未设置头像");
    }

    public (byte[] Content, string FileName) GetAvatar(string sessionId)
    {
        var context = LoadContext(sessionId);
        var path = context.StudentStore.GetAvatarPath()
            ?? throw new AuthException(404, "未设置头像");
        return (context.StudentStore.ReadAvatar(path), path);
    }

    public (string Message, AuthAvatarUploadPayload Payload) SaveAvatar(string sessionId, string fileName, byte[] content)
    {
        var context = LoadContext(sessionId);
        if (content.Length == 0)
        {
            throw new AuthException(400, "上传文件为空");
        }

        if (content.Length > 3 * 1024 * 1024)
        {
            throw new AuthException(400, "头像大小不能超过 3MB");
        }

        var savedFile = context.StudentStore.SaveAvatar(fileName, content);
        return (
            "头像已保存",
            new AuthAvatarUploadPayload(
                AvatarUrl: ResolveAvatarUrl(sessionId, context.StudentStore),
                SavedFile: savedFile));
    }

    public string Logout(string sessionId)
    {
        var session = EnsureSessionExists(sessionId);
        Interlocked.CompareExchange(ref _activeSessionId, null, sessionId);
        _dataRoot.Sessions().Remove(sessionId);
        MarkSessionDisconnected(sessionId);
        _traditionalJwSessions.Remove(sessionId);
        _dataRoot.JwSessions().Remove(session.User);
        return "已注销";
    }

    public bool IsSessionConnected(string sessionId)
    {
        var cleaned = (sessionId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return false;
        }

        lock (_connectedSessionsGate)
        {
            return _connectedSessions.Contains(cleaned);
        }
    }

    public bool TryGetMobileToken(string sessionId, out string token)
    {
        return _mobileSessionRuntime.TryGetToken(sessionId, out token);
    }

    public string ResolveSessionStudentId(string sessionId)
    {
        var session = EnsureSessionExists(sessionId);
        return ResolveStudentId(session.User, session.StudentId);
    }

    private static string NormalizeRequired(string? value, string errorMessage)
    {
        var cleaned = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            throw new AuthException(400, errorMessage);
        }

        return cleaned;
    }

    private static string NormalizePreloginMode(string? value)
    {
        return AuthSettingsCatalog.NormalizePreloginMode(value);
    }



    private async Task<bool> TryRestorePersistedJwSessionAsync(
        string sessionId,
        StoredCredentialDocument credentials,
        CancellationToken cancellationToken)
    {
        var stored = _dataRoot.JwSessions().Load(credentials.User);
        if (stored is null || stored.Cookies.Count == 0)
        {
            return false;
        }

        var client = TraditionalJwClient.FromStoredSession(
            credentials.User,
            credentials.Password,
            stored,
            TraditionalJwOptions.FromDataRoot(_dataRoot));
        try
        {
            var profile = await client.FetchStudentProfileAsync(cancellationToken).ConfigureAwait(false);
            if (!string.Equals(profile.StudentId, ResolveStudentId(credentials.User, credentials.StudentId), StringComparison.OrdinalIgnoreCase))
            {
                client.Dispose();
                _dataRoot.JwSessions().Remove(credentials.User);
                return false;
            }

            _dataRoot.ForStudent(profile.StudentId).SaveProfile(profile);
            _traditionalJwSessions.Store(sessionId, client);
            _dataRoot.JwSessions().Save(client.ExportStoredSession());
            return true;
        }
        catch (TraditionalJwSessionExpiredException)
        {
            client.Dispose();
            _dataRoot.JwSessions().Remove(credentials.User);
            return false;
        }
        catch (JwScheduleException)
        {
            client.Dispose();
            _dataRoot.JwSessions().Remove(credentials.User);
            return false;
        }
    }

    private void CleanupExpiredProtocolLogins()
    {
        var now = DateTimeOffset.Now;
        foreach (var pair in _protocolLogins)
        {
            if (pair.Value.ExpiresAt > now || !_protocolLogins.TryRemove(pair.Key, out var expired))
            {
                continue;
            }

            expired.Protocol.Dispose();
        }
    }

    private static string ResolveStudentId(string user, string? storedStudentId)
    {
        return string.IsNullOrWhiteSpace(storedStudentId) ? user : storedStudentId;
    }

    private static string CreateTrustedLoginToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private RestoredTrustedWorkspace RestoreTrustedWorkspace(StoredCredentialDocument credentials, string preloginMode)
    {
        var credentialStore = _dataRoot.Credentials();
        var refreshedCredentials = EnsureTrustedToken(credentials, credentialStore);
        refreshedCredentials = TouchCredentialUsage(refreshedCredentials, credentialStore);
        var studentStore = _dataRoot.ForStudent(ResolveStudentId(refreshedCredentials.User, refreshedCredentials.StudentId));
        var profile = LoadInitializedProfile(studentStore);
        var effectivePreloginMode = string.IsNullOrWhiteSpace(preloginMode)
            ? AuthSettingsCatalog.Normalize(studentStore.LoadPreferences()).Auth.PreloginMode
            : NormalizePreloginMode(preloginMode);
        var settings = AuthSettingsCatalog.WithPreloginMode(studentStore.LoadPreferences() ?? AuthSettingsCatalog.CreateDefault(), effectivePreloginMode);
        var sessionResult = _dataRoot.Sessions().Create(refreshedCredentials.User, profile.StudentId, effectivePreloginMode);
        Dictionary<string, object?> cachedSchedule;
        try
        {
            cachedSchedule = LoadRequiredCachedSchedule(sessionResult.SessionId);
        }
        catch
        {
            _dataRoot.Sessions().Remove(sessionResult.SessionId);
            throw;
        }

        return new RestoredTrustedWorkspace(
            refreshedCredentials.User,
            BuildPayload(sessionResult.SessionId, profile, studentStore, settings, refreshedCredentials.TrustedLoginToken, cachedSchedule, connected: false));
    }

    private StoredCredentialDocument EnsureTrustedToken(StoredCredentialDocument credentials, XkeCredentialStore credentialStore)
    {
        if (!string.IsNullOrWhiteSpace(credentials.TrustedLoginToken))
        {
            return credentials;
        }

        var updated = new StoredCredentialDocument
        {
            User = credentials.User,
            Password = credentials.Password,
            StudentId = credentials.StudentId,
            TrustedLoginToken = CreateTrustedLoginToken(),
            MobileJw = credentials.MobileJw,
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
        };
        credentialStore.Save(updated);
        return updated;
    }

    private static DateTimeOffset ParseCredentialUpdatedAt(string? value)
    {
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.MinValue;
    }

    private static StoredCredentialDocument TouchCredentialUsage(StoredCredentialDocument credentials, XkeCredentialStore credentialStore)
    {
        var updated = new StoredCredentialDocument
        {
            User = credentials.User,
            Password = credentials.Password,
            StudentId = credentials.StudentId,
            TrustedLoginToken = credentials.TrustedLoginToken,
            MobileJw = credentials.MobileJw,
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
        };
        credentialStore.Save(updated);
        return updated;
    }

    private async Task<(string Message, AuthPayload Payload)> InitializeRemoteStateAsync(
        string user,
        string password,
        string preloginMode,
        StoredCredentialDocument? previousCredentials,
        string successMessage,
        string failurePrefix,
        CancellationToken cancellationToken)
    {
        JwAuthResult remoteResult;
        try
        {
            remoteResult = await _jwClient.InitializeAsync(
                user,
                password,
                preloginMode,
                cancellationToken,
                previousCredentials?.MobileJw).ConfigureAwait(false);
        }
        catch (JwAuthException ex)
        {
            throw new AuthException(401, ex.Message);
        }

        var profileFromRemote = EnsureRemoteProfile(remoteResult.Profile, $"{failurePrefix}：移动教务未返回可用身份信息");
        var mobileToken = EnsureRemoteMobileToken(remoteResult.MobileToken, $"{failurePrefix}：移动教务未返回有效 Token");
        var remoteStore = _dataRoot.ForStudent(profileFromRemote.StudentId);
        // Remote initialization writes profile/preferences/schedule before the final success gate;
        // snapshotting keeps a failed repair from polluting an existing trusted workspace.
        using var studentSnapshot = StudentInitializationSnapshot.Capture(_dataRoot, remoteStore);
        var credentialStore = _dataRoot.Credentials();
        var createdSessionId = string.Empty;
        try
        {
            remoteStore.SaveProfile(profileFromRemote);
            var settingsDocument = AuthSettingsCatalog.WithPreloginMode(remoteStore.LoadPreferences() ?? AuthSettingsCatalog.CreateDefault(), preloginMode);
            remoteStore.SavePreferences(settingsDocument);

            var credentials = new StoredCredentialDocument
            {
                User = user,
                Password = password,
                StudentId = profileFromRemote.StudentId,
                TrustedLoginToken = string.IsNullOrWhiteSpace(previousCredentials?.TrustedLoginToken)
                    ? CreateTrustedLoginToken()
                    : previousCredentials.TrustedLoginToken,
                MobileJw = remoteResult.MobileJw,
                UpdatedAt = DateTimeOffset.Now.ToString("O"),
            };
            credentialStore.Save(credentials);

            var createdSession = _dataRoot.Sessions().Create(user, profileFromRemote.StudentId, preloginMode);
            createdSessionId = createdSession.SessionId;
            // First-time or repair initialization is only valid after identity and
            // schedule have both been materialized locally.
            MarkSessionConnected(createdSessionId, mobileToken);
            var cachedSchedule = await InitializeCurrentScheduleAsync(
                createdSessionId,
                cancellationToken).ConfigureAwait(false);

            return (
                successMessage,
                BuildPayload(createdSessionId, profileFromRemote, remoteStore, settingsDocument, credentials.TrustedLoginToken, cachedSchedule, connected: true));
        }
        catch (AuthException)
        {
            RollbackRemoteInitialization(createdSessionId, user, studentSnapshot, previousCredentials);
            throw;
        }
        catch (ScheduleException ex)
        {
            RollbackRemoteInitialization(createdSessionId, user, studentSnapshot, previousCredentials);
            throw new AuthException(500, $"{failurePrefix}：{ex.Detail}");
        }
        catch (OperationCanceledException)
        {
            RollbackRemoteInitialization(createdSessionId, user, studentSnapshot, previousCredentials);
            throw;
        }
        catch (Exception ex)
        {
            RollbackRemoteInitialization(createdSessionId, user, studentSnapshot, previousCredentials);
            throw new AuthException(500, $"{failurePrefix}：本地初始化失败：{ex.Message}", ex);
        }
    }

    private AuthPayload BuildPayload(
        string sessionId,
        StudentProfileDocument profile,
        XkeStudentStore studentStore,
        AppSettingsDocument settings,
        string trustedLoginToken,
        object? cachedSchedule,
        bool connected)
    {
        var normalizedSettings = AuthSettingsCatalog.Normalize(settings);
        Volatile.Write(ref _activeSessionId, sessionId);
        return new AuthPayload(
            SessionId: sessionId,
            Student: BuildStudentSummary(profile, ResolveAvatarUrl(sessionId, studentStore)),
            Preferences: AuthSettingsCatalog.ToLegacyPreferences(normalizedSettings),
            Settings: normalizedSettings,
            StorageDir: studentStore.RelativeDirectory(),
            Connected: connected,
            PreloginMode: normalizedSettings.Auth.PreloginMode,
            CachedSchedule: cachedSchedule,
            TrustedLoginToken: trustedLoginToken);
    }

    private async Task<Dictionary<string, object?>> InitializeCurrentScheduleAsync(string sessionId, CancellationToken cancellationToken)
    {
        var result = await _scheduleService.FetchPreferredAsync(
            sessionId,
            new ScheduleRequest
            {
                Mode = "current",
            },
            cancellationToken).ConfigureAwait(false);
        // Source clients distinguish an empty week from failed parsing.
        return result.Payload;
    }

    private Dictionary<string, object?> LoadRequiredCachedSchedule(string sessionId)
    {
        Dictionary<string, object?> cachedSchedule;
        try
        {
            var result = _scheduleService.LoadLocal(sessionId, string.Empty, "preferred");
            cachedSchedule = result.Payload;
        }
        catch (ScheduleException ex)
        {
            throw new AuthException(
                ex.StatusCode == 404 ? 409 : ex.StatusCode,
                "本地初始化数据不完整：缺少可用的本地课表缓存，请重新初始化或修复本地数据");
        }

        // A successful persisted week can legitimately contain no classes.
        return cachedSchedule;
    }

    private Dictionary<string, object?>? TryLoadCachedSchedule(string sessionId)
    {
        try
        {
            var result = _scheduleService.LoadLocal(sessionId, string.Empty, "preferred");
            return result.Payload;
        }
        catch (ScheduleException)
        {
            return null;
        }
    }

    private void RollbackRemoteInitialization(
        string sessionId,
        string user,
        StudentInitializationSnapshot studentSnapshot,
        StoredCredentialDocument? previousCredentials)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            _dataRoot.Sessions().Remove(sessionId);
            MarkSessionDisconnected(sessionId);
        }

        if (previousCredentials is null)
        {
            _dataRoot.Credentials().Remove(user);
        }
        else
        {
            _dataRoot.Credentials().Save(previousCredentials);
        }

        studentSnapshot.Restore();
    }

    private AuthStudentSummary BuildStudentSummary(StudentProfileDocument profile, string avatarUrl)
    {
        return new AuthStudentSummary(
            StudentId: profile.StudentId,
            Name: string.IsNullOrWhiteSpace(profile.Name) ? profile.StudentId : profile.Name,
            Nickname: profile.Nickname,
            UseNickname: profile.UseNickname,
            ClassName: profile.ClassName,
            MajorName: profile.MajorName,
            AcademyName: profile.AcademyName,
            TrainingLevel: profile.TrainingLevel,
            CampusName: profile.CampusName,
            AvatarUrl: avatarUrl,
            UpdatedAt: profile.UpdatedAt,
            ProgramDurationYears: profile.ProgramDurationYears,
            EnrollmentDate: profile.EnrollmentDate);
    }

    private static StudentProfileDocument EnsureRemoteProfile(StudentProfileDocument profile, string errorMessage)
    {
        if (!string.IsNullOrWhiteSpace(profile.StudentId) && !string.IsNullOrWhiteSpace(profile.Name))
        {
            return profile;
        }

        throw new AuthException(502, errorMessage);
    }

    private static string EnsureRemoteMobileToken(string mobileToken, string errorMessage)
    {
        var cleaned = (mobileToken ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            throw new AuthException(502, errorMessage);
        }

        return cleaned;
    }

    private static StudentProfileDocument LoadInitializedProfile(XkeStudentStore studentStore)
    {
        var profile = studentStore.LoadProfile();
        if (profile is not null && !string.IsNullOrWhiteSpace(profile.StudentId) && !string.IsNullOrWhiteSpace(profile.Name))
        {
            return profile;
        }

        throw new AuthException(409, "本地初始化数据不完整：缺少可用身份信息，请重新初始化或修复本地数据");
    }

    private string ResolveAvatarUrl(string sessionId, XkeStudentStore store)
    {
        var avatarPath = store.GetAvatarPath();
        if (string.IsNullOrWhiteSpace(avatarPath))
        {
            return string.Empty;
        }

        var version = File.GetLastWriteTimeUtc(avatarPath).Ticks;
        return $"/api/auth/avatar?v={version}";
    }

    private void MarkSessionConnected(string sessionId, string mobileToken)
    {
        var cleaned = (sessionId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return;
        }

        lock (_connectedSessionsGate)
        {
            _connectedSessions.Add(cleaned);
        }

        _mobileSessionRuntime.StoreToken(cleaned, mobileToken);
    }

    private void MarkSessionDisconnected(string sessionId)
    {
        var cleaned = (sessionId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return;
        }

        lock (_connectedSessionsGate)
        {
            _connectedSessions.Remove(cleaned);
        }

        _mobileSessionRuntime.Remove(cleaned);
    }

    private AuthContext LoadContext(string sessionId)
    {
        var session = EnsureSessionExists(sessionId);
        var credentialStore = _dataRoot.Credentials();
        var credentials = credentialStore.Load(session.User)
            ?? throw new AuthException(401, "本地凭据不存在，请重新登录");
        var refreshedCredentials = EnsureTrustedToken(credentials, credentialStore);
        var studentStore = _dataRoot.ForStudent(ResolveStudentId(session.User, session.StudentId));
        var profile = LoadInitializedProfile(studentStore);
        var settings = AuthSettingsCatalog.WithPreloginMode(studentStore.LoadPreferences() ?? AuthSettingsCatalog.CreateDefault(), session.PreloginMode);

        return new AuthContext(session, refreshedCredentials, studentStore, profile, settings);
    }

    private StoredSessionDocument EnsureSessionExists(string sessionId)
    {
        var cleaned = NormalizeRequired(sessionId, "会话不能为空");
        return _dataRoot.Sessions().Load(cleaned)
            ?? throw new AuthException(401, "会话不存在或已过期，请重新登录");
    }

    private sealed class StudentInitializationSnapshot : IDisposable
    {
        private readonly string _dataRootPath;
        private readonly string _studentDirectory;
        private readonly string _snapshotDirectory;
        private readonly bool _existed;

        private StudentInitializationSnapshot(string dataRootPath, string studentDirectory, string snapshotDirectory, bool existed)
        {
            _dataRootPath = dataRootPath;
            _studentDirectory = studentDirectory;
            _snapshotDirectory = snapshotDirectory;
            _existed = existed;
        }

        public static StudentInitializationSnapshot Capture(XkeDataRoot dataRoot, XkeStudentStore studentStore)
        {
            var snapshotDirectory = Path.Combine(dataRoot.RootPath, ".auth_init_snapshots", Guid.NewGuid().ToString("N"));
            var existed = Directory.Exists(studentStore.StudentDirectory);
            if (existed)
            {
                CopyDirectory(studentStore.StudentDirectory, snapshotDirectory);
            }

            return new StudentInitializationSnapshot(dataRoot.RootPath, studentStore.StudentDirectory, snapshotDirectory, existed);
        }

        public void Restore()
        {
            if (Directory.Exists(_studentDirectory))
            {
                EnsureCanDelete(_studentDirectory);
                Directory.Delete(_studentDirectory, recursive: true);
            }

            if (_existed)
            {
                CopyDirectory(_snapshotDirectory, _studentDirectory);
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(_snapshotDirectory))
            {
                EnsureCanDelete(_snapshotDirectory);
                Directory.Delete(_snapshotDirectory, recursive: true);
            }

            var parent = Path.GetDirectoryName(_snapshotDirectory);
            if (!string.IsNullOrWhiteSpace(parent)
                && Directory.Exists(parent)
                && !Directory.EnumerateFileSystemEntries(parent).Any())
            {
                EnsureCanDelete(parent);
                Directory.Delete(parent);
            }
        }

        private void EnsureCanDelete(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var fileName = Path.GetFileName(fullPath);
            // Rollback can delete partially initialized student data, but only
            // after proving the target stays below the active .NET data root.
            if (fileName.Length == 0 || !XkeDataRoot.IsPathInsideRoot(_dataRootPath, fullPath))
            {
                throw new InvalidOperationException($"Refusing to delete unsafe initialization path: {fullPath}");
            }
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);
            foreach (var file in Directory.EnumerateFiles(sourceDirectory))
            {
                File.Copy(file, Path.Combine(destinationDirectory, Path.GetFileName(file)), overwrite: true);
            }

            foreach (var directory in Directory.EnumerateDirectories(sourceDirectory))
            {
                CopyDirectory(directory, Path.Combine(destinationDirectory, Path.GetFileName(directory)));
            }
        }
    }
    private sealed record AuthContext(
        StoredSessionDocument Session,
        StoredCredentialDocument Credentials,
        XkeStudentStore StudentStore,
        StudentProfileDocument Profile,
        AppSettingsDocument Settings);

    private sealed record RestoredTrustedWorkspace(
        string User,
        AuthPayload Payload);

    public void Dispose()
    {
        foreach (var pair in _protocolLogins)
        {
            if (_protocolLogins.TryRemove(pair.Key, out var pending)) pending.Protocol.Dispose();
        }
    }

    private sealed record PendingProtocolLogin(string User, string Password, string PreloginMode, StoredCredentialDocument? PreviousCredentials, DirectJwLoginProtocol Protocol, DateTimeOffset ExpiresAt);
}
