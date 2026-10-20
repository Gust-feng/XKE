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
    private readonly AutomaticCaptchaLogin? _automatic;
    private long _loginGeneration;
    private string? _activeSessionId;
    public string? ActiveSessionId => Volatile.Read(ref _activeSessionId);

    private readonly XkeDataRoot _dataRoot;
    private readonly ScheduleService _scheduleService;
    private readonly TraditionalJwSessionRuntimeStore _traditionalJwSessions;
    private readonly ConcurrentDictionary<string, PendingProtocolLogin> _protocolLogins = new(StringComparer.Ordinal);

    public AuthService(XkeDataRoot dataRoot, ScheduleService scheduleService, TraditionalJwSessionRuntimeStore traditionalJwSessions, Func<string, string, DirectJwLoginProtocol>? protocolFactory = null, AutomaticCaptchaLogin? automatic = null)
    {
        _dataRoot = dataRoot;
        _automatic = automatic;
        _scheduleService = scheduleService;
        _traditionalJwSessions = traditionalJwSessions;
        _protocolFactory = protocolFactory ?? ((user, password) => new DirectJwLoginProtocol(user, password));
    }

    public async Task<(string Message, object Payload)> BeginProtocolLoginAsync(LoginRequest request, bool forceRemote = false, CancellationToken cancellationToken = default)
    {
        var generation = Interlocked.Increment(ref _loginGeneration);
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
            void ValidateLogin()
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (generation != Volatile.Read(ref _loginGeneration)) throw new AuthException(409, "登录操作已取消");
            }
            var authenticated = false;
            if (_automatic?.CanUse(user, password) == true)
            {
                authenticated = await _automatic.TryLoginAsync(user, password, protocol, image, cancellationToken, ValidateLogin).ConfigureAwait(false);
                if (!authenticated) image = await protocol.BeginAsync(cancellationToken).ConfigureAwait(false);
            }
            ValidateLogin();
            var challengeId = Guid.NewGuid().ToString("N");
            _protocolLogins[challengeId] = new PendingProtocolLogin(user, password, NormalizePreloginMode(request.PreloginMode), existing, protocol, DateTimeOffset.Now.AddMinutes(2), generation, authenticated);
            if (authenticated)
            {
                var completed = await SubmitProtocolCaptchaAsync(new JwCaptchaSubmitRequest { ChallengeId = challengeId, CaptchaCode = string.Empty }, cancellationToken).ConfigureAwait(false);
                return (completed.Message, completed.Payload);
            }
            return ("需要输入教务系统验证码", new Dictionary<string, object?>
            {
                ["status"] = "captcha_required",
                ["challenge_id"] = challengeId,
                ["captcha_image"] = image,
                ["expires_in_seconds"] = 120,
                ["protocol"] = "jw_form",
                ["message"] = _automatic?.GetStatus(user).Message ?? string.Empty,
            });
        }
        catch (JwScheduleException ex)
        {
            protocol.Dispose();
            throw new AuthException(ex.StatusCode is 400 or 401 ? ex.StatusCode : 502, ex.Message);
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

        if (pending.ExpiresAt <= DateTimeOffset.Now || pending.Generation != Volatile.Read(ref _loginGeneration))
        {
            pending.Protocol.Dispose();
            throw new AuthException(410, "登录验证码已过期，请重新登录");
        }

        using (pending.Protocol)
        {
            try
            {
                if (!pending.Authenticated) await pending.Protocol.CompleteAsync(request.CaptchaCode, cancellationToken).ConfigureAwait(false);
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

            cancellationToken.ThrowIfCancellationRequested();
            if (pending.Generation != Volatile.Read(ref _loginGeneration)) { jwSession.Dispose(); throw new AuthException(409, "登录操作已取消"); }
            var previousJwSession = _dataRoot.JwSessions().Load(pending.User);
            var store = _dataRoot.ForStudent(profile.StudentId);
            var displayPreference = store.LoadProfile();
            if (displayPreference is not null) profile = WithNickname(profile, displayPreference.Nickname, displayPreference.UseNickname);
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
                if (schedule.Payload.GetValueOrDefault("status")?.ToString() == "captcha_required")
                    throw new AuthException(409, "初始化期间教务会话已失效，请重新验证");
                // A successfully parsed empty week is a valid initial schedule.
                _dataRoot.JwSessions().Save(jwSession.ExportStoredSession());
                cancellationToken.ThrowIfCancellationRequested();
                if (pending.Generation != Volatile.Read(ref _loginGeneration)) throw new AuthException(409, "登录操作已取消");
                _automatic?.Reset(pending.User);
                return ("登录成功", BuildPayload(sessionId, profile, store, settings, credentials.TrustedLoginToken, schedule.Payload, connected: true));
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
        if (context.Settings.Auth.AutoCaptchaEnabled != updatedSettings.Auth.AutoCaptchaEnabled)
        {
            if (updatedSettings.Auth.AutoCaptchaEnabled) _automatic?.Reset(context.Session.User);
            else _automatic?.Cancel(context.Session.User);
        }

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
        var updatedProfile = WithNickname(context.Profile, request.Nickname ?? string.Empty, request.UseNickname);
        context.StudentStore.SaveProfile(updatedProfile);
        return ("昵称已保存", new AuthProfilePayload(BuildStudentSummary(updatedProfile, ResolveAvatarUrl(sessionId, context.StudentStore))));
    }

    private static StudentProfileDocument WithNickname(StudentProfileDocument profile, string nickname, bool useNickname)
    {
        var value = nickname.Trim();
        return new StudentProfileDocument
        {
            StudentId = profile.StudentId, Name = profile.Name,
            Nickname = value, UseNickname = useNickname && value.Length > 0,
            ClassName = profile.ClassName, MajorName = profile.MajorName, AcademyName = profile.AcademyName,
            TrainingLevel = profile.TrainingLevel, CampusName = profile.CampusName,
            ProgramDurationYears = profile.ProgramDurationYears, EnrollmentDate = profile.EnrollmentDate,
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
        };
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
        Interlocked.Increment(ref _loginGeneration);
        _automatic?.Cancel(session.User);
        Interlocked.CompareExchange(ref _activeSessionId, null, sessionId);
        _dataRoot.Sessions().Remove(sessionId);
        _traditionalJwSessions.Remove(sessionId);
        _dataRoot.JwSessions().Remove(session.User);
        return "已注销";
    }

    public CaptchaRecognitionStatus GetCaptchaRecognitionStatus(string sessionId)
    {
        var context = LoadContext(sessionId);
        return _automatic?.GetStatus(context.Session.User) ?? new(false, !string.IsNullOrWhiteSpace(context.Credentials.TrustedLoginToken), context.Settings.Auth.AutoCaptchaEnabled, "unavailable", 0, "");
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

            var studentStore = _dataRoot.ForStudent(profile.StudentId);
            var displayPreference = studentStore.LoadProfile();
            if (displayPreference is not null) profile = WithNickname(profile, displayPreference.Nickname, displayPreference.UseNickname);
            studentStore.SaveProfile(profile);
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
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
        };
        credentialStore.Save(updated);
        return updated;
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

    private sealed record PendingProtocolLogin(string User, string Password, string PreloginMode, StoredCredentialDocument? PreviousCredentials, DirectJwLoginProtocol Protocol, DateTimeOffset ExpiresAt, long Generation, bool Authenticated);
}
