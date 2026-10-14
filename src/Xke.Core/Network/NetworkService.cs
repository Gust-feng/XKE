using AngleSharp.Html.Parser;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xke.Contracts.Enums;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Contracts.Responses;
using Xke.Core.Auth;
using Xke.Storage;

namespace Xke.Core.Network;

public sealed class NetworkService : INetworkMaintenance, IDisposable
{
    private static readonly string[] ProviderKeys = ["campus", "telecom", "unicom", "mobile"];
    private static readonly Regex IpConfigHeaderPattern = new(@"^(?<prefix>.*?(?:adapter|适配器))\s+(?<name>[^:]+):\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly SemaphoreSlim _maintenanceGate = new(1, 1);
    private readonly XkeDataRoot _dataRoot;
    private readonly HttpClient _httpClient;
    private readonly INetworkCommandExecutor _commandExecutor;
    private readonly Func<string?>? _activeSession;

    public NetworkService(XkeDataRoot dataRoot, HttpClient httpClient, INetworkCommandExecutor commandExecutor, Func<string?>? activeSession = null)
    {
        _dataRoot = dataRoot;
        _httpClient = httpClient;
        _commandExecutor = commandExecutor;
        _activeSession = activeSession;
    }

    public (string Message, NetworkConfigPayload Payload) GetConfig(string sessionId)
    {
        var context = LoadContext(sessionId);
        var credentials = BuildCredentialBundle(context.StudentStore);
        return (
            "校园网配置已加载",
            new NetworkConfigPayload(
                Settings: context.Settings.Network,
                Credentials: credentials,
                CampusAccount: credentials["campus"].Account,
                StorageDir: context.StudentStore.RelativeDirectory()));
    }

    public (string Message, NetworkConfigPayload Payload) SaveConfig(
        string sessionId,
        NetworkConfigRequest request)
    {
        var context = LoadContext(sessionId);
        if (request.HeartbeatIntervalMinutes is < 1 or > 1440)
            throw new NetworkException(400, "心跳间隔须为1到1440分钟");
        var updatedSettings = AuthSettingsCatalog.Apply(
            context.Settings,
            new PreferencesRequest { Network = request });
        context.StudentStore.SavePreferences(updatedSettings);
        SaveProviderCredentials(context.StudentStore, request);

        var credentials = BuildCredentialBundle(context.StudentStore);
        return (
            "校园网配置已保存",
            new NetworkConfigPayload(
                Settings: updatedSettings.Network,
                Credentials: credentials,
                CampusAccount: credentials["campus"].Account,
                StorageDir: context.StudentStore.RelativeDirectory()));
    }

    public async Task<(string Message, NetworkStatusPayload Payload)> GetStatusAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var context = LoadContext(sessionId);
        var status = await DetectStatusAsync(context.Settings.Network, cancellationToken).ConfigureAwait(false);
        return ("校园网状态已刷新", status);
    }

    public Task<(string Message, NetworkStatusPayload Payload)> MaintainAsync(
        string sessionId, NetworkActionRequest request, CancellationToken cancellationToken = default)
        => RunMaintenanceAsync(sessionId, request, background: false, cancellationToken);

    public Task<(string Message, NetworkStatusPayload Payload)> MaintainInBackgroundAsync(
        string sessionId, CancellationToken cancellationToken = default)
        => RunMaintenanceAsync(sessionId, new NetworkActionRequest { Force = false }, background: true, cancellationToken);

    private async Task<(string Message, NetworkStatusPayload Payload)> RunMaintenanceAsync(
        string sessionId, NetworkActionRequest request, bool background, CancellationToken cancellationToken)
    {
        await _maintenanceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await MaintainCoreAsync(sessionId, request, background, cancellationToken).ConfigureAwait(false); }
        finally { _maintenanceGate.Release(); }
    }

    public void Dispose() => _maintenanceGate.Dispose();

    private async Task<(string Message, NetworkStatusPayload Payload)> MaintainCoreAsync(
        string sessionId, NetworkActionRequest request, bool background, CancellationToken cancellationToken)
    {
        var context = LoadContext(sessionId);
        var settings = context.Settings.Network;
        var provider = NormalizeProvider(settings.Provider);
        var revision = ConfigurationRevision(context, background);
        void Validate(NetworkLoginCredentials? expectedCredentials = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = LoadContext(sessionId); // Logout invalidates work that has not sent its next request.
            if (background && _activeSession is not null && _activeSession() != sessionId
                || current.StudentStore.StudentId != context.StudentStore.StudentId
                || background && !current.Settings.Network.HeartbeatEnabled
                || ConfigurationRevision(current, background) != revision)
                throw new NetworkException(409, "网络设置已变更，本次维护已取消");
            if (expectedCredentials is not null)
            {
                var currentCredentials = ResolveLoginCredentials(current, provider);
                if (currentCredentials.Account != expectedCredentials.Account || currentCredentials.Password != expectedCredentials.Password)
                    throw new NetworkException(409, "网络凭据已变更，本次维护已取消");
            }
        }
        Validate();
        var status = await DetectStatusAsync(settings, cancellationToken).ConfigureAwait(false);
        Validate();
        status = WithMaintainFields(status, provider, "none", [], string.Empty);
        if (!status.CampusConnected)
        {
            var message = "未接入校园网或认证门户不可达，未发送账号密码";
            status = WithMaintainFields(status, provider, "none", [], message) with { OperationError = message };
            RecordNetworkAuth(context.StudentStore, provider, context.Session.User, false, status, [], message);
            return (message, status);
        }
        if (status.InternetConnected && !request.Force)
        {
            var message = "校园网认证正常，无需补发心跳";
            status = WithMaintainFields(status, provider, "none", [], message);
            RecordNetworkAuth(context.StudentStore, provider, context.Session.User, true, status, [], message);
            return (message, status);
        }
        var credentials = ResolveLoginCredentials(context, provider);
        var attempts = new List<Dictionary<string, object?>>();
        var failedMessage = "认证已提交，但外网仍不可用";
        foreach (var username in credentials.Usernames)
        {
            Validate(credentials); // Recheck immediately before sending a password, including suffix retries.
            CampusPortalLoginResult loginResult;
            try { loginResult = await SendLoginRequestAsync(settings, username, credentials.Password, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            { loginResult = new(false, 0, "transport_error", "认证服务连接超时，请确认已接入校园网"); }
            catch (HttpRequestException)
            { loginResult = new(false, 0, "transport_error", "无法连接认证服务，请检查当前网络"); }
            Validate(credentials);
            attempts.Add(new Dictionary<string, object?>
            {
                ["username"] = MaskSecret(username), ["status_code"] = loginResult.StatusCode,
                ["accepted"] = loginResult.Accepted, ["error_kind"] = loginResult.ErrorKind, ["message"] = loginResult.Message,
            });
            if (!loginResult.Accepted)
            {
                failedMessage = loginResult.Message;
                if (loginResult.ErrorKind != "auth_rejected") break;
                continue;
            }
            // One final snapshot is the only success authority. Do not label a failed snapshot as restored.
            status = await DetectStatusAsync(settings, cancellationToken).ConfigureAwait(false);
            Validate(credentials);
            if (status.CampusConnected && status.InternetConnected)
            {
                var message = "校园网心跳已补发，外网访问已恢复";
                status = WithMaintainFields(status, provider, "heartbeat", attempts, message);
                RecordNetworkAuth(context.StudentStore, provider, credentials.Account, true, status, attempts, message);
                return (message, status);
            }
            break; // An accepted account must not be logged in again using another suffix.
        }
        Validate(credentials);
        status = WithMaintainFields(status, provider, "heartbeat", attempts, failedMessage) with { OperationError = failedMessage };
        RecordNetworkAuth(context.StudentStore, provider, credentials.Account, false, status, attempts, failedMessage);
        return (failedMessage, status);
    }

    private static string ConfigurationRevision(NetworkContext context, bool background)
    {
        var settings = context.Settings.Network;
        return JsonSerializer.Serialize(new
        {
            settings.Provider, settings.PortalUrl, settings.ConnectivityCheckUrls,
            Enabled = background && settings.HeartbeatEnabled,
            Interval = background ? settings.HeartbeatIntervalMinutes : 0,
            Credentials = context.StudentStore.NetworkCredentials().GetRevision(settings.Provider),
        });
    }

    public static IReadOnlyList<Dictionary<string, object?>> ParseIpConfigOutput(string text)
    {
        var adapters = new List<Dictionary<string, object?>>();
        Dictionary<string, object?>? current = null;
        var currentKey = string.Empty;

        void FinalizeCurrent()
        {
            if (current is null)
            {
                return;
            }

            current["default_gateway"] = FilterStrings(current.GetValueOrDefault("default_gateway"));
            current["dns_servers"] = FilterStrings(current.GetValueOrDefault("dns_servers"));
            adapters.Add(current);
        }

        foreach (var rawLine in (text ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.TrimEnd();
            var stripped = line.Trim();
            var header = IpConfigHeaderPattern.Match(stripped);
            if (header.Success)
            {
                FinalizeCurrent();
                current = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["name"] = header.Groups["name"].Value.Trim(),
                    ["description"] = string.Empty,
                    ["ipv4"] = string.Empty,
                    ["subnet_mask"] = string.Empty,
                    ["default_gateway"] = new List<string>(),
                    ["dns_servers"] = new List<string>(),
                    ["mac_address"] = string.Empty,
                    ["dhcp_enabled"] = string.Empty,
                    ["dhcp_server"] = string.Empty,
                    ["media_state"] = string.Empty,
                };
                currentKey = string.Empty;
                continue;
            }

            if (current is null)
            {
                continue;
            }

            if (line.Contains(':', StringComparison.Ordinal))
            {
                var parts = line.Split(':', 2);
                var key = MapIpConfigField(parts[0]);
                var value = CleanIpConfigValue(parts[1]);
                currentKey = key;
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (key is "default_gateway" or "dns_servers")
                {
                    ((List<string>)current[key]!).Add(value);
                }
                else
                {
                    current[key] = value;
                }
                continue;
            }

            if (currentKey is "default_gateway" or "dns_servers")
            {
                ((List<string>)current[currentKey]!).Add(CleanIpConfigValue(stripped));
            }
        }

        FinalizeCurrent();
        return adapters;
    }

    private async Task<NetworkStatusPayload> DetectStatusAsync(NetworkSettingsDocument settings, CancellationToken cancellationToken)
    {
        var campusConnected = await PingPortalAsync(settings, cancellationToken).ConfigureAwait(false);
        var portal = campusConnected
            ? await FetchPortalSnapshotAsync(settings, cancellationToken).ConfigureAwait(false)
            : BuildEmptyPortal();
        var localIp = Text(portal.GetValueOrDefault("local_ip"));
        var networkInterface = await LoadLocalNetworkSnapshotAsync(localIp, cancellationToken).ConfigureAwait(false);
        var external = campusConnected
            ? await CheckExternalConnectivityAsync(settings, cancellationToken).ConfigureAwait(false)
            : new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["connected"] = false,
                ["checked_url"] = string.Empty,
                ["status_code"] = 0,
                ["detail"] = "当前未接入校园网",
            };

        var internetConnected = external.GetValueOrDefault("connected") is true;
        return new NetworkStatusPayload(
            CheckedAt: DateTimeOffset.Now.ToString("O"),
            CampusConnected: campusConnected,
            InternetConnected: internetConnected,
            NeedsHeartbeat: campusConnected && !internetConnected,
            PortalHost: new Uri(settings.PortalUrl).Host,
            Portal: portal,
            Interface: networkInterface,
            External: external,
            Settings: settings,
            Provider: NormalizeProvider(settings.Provider),
            ProviderLabel: ProviderLabel(settings.Provider),
            Action: "none",
            Attempts: [],
            Message: string.Empty);
    }

    private async Task<bool> PingPortalAsync(NetworkSettingsDocument settings, CancellationToken cancellationToken)
    {
        try
        {
            var ping = await _commandExecutor.RunAsync(
                "ping",
                ["-n", "1", "-w", "1200", new Uri(settings.PortalUrl).Host],
                TimeSpan.FromSeconds(3),
                cancellationToken).ConfigureAwait(false);
            if (ping.ExitCode == 0)
            {
                return true;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            // Fall through to HTTP probing; status detection should stay best-effort.
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, settings.PortalUrl);
            using var response = await SendAsync(request, TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode || response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            return false;
        }
    }

    private async Task<Dictionary<string, object?>> FetchPortalSnapshotAsync(NetworkSettingsDocument settings, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, settings.PortalUrl);
            using var response = await SendAsync(request, TimeSpan.FromSeconds(4), cancellationToken).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["local_ip"] = ExtractFirst(@"lip='([^']*)'", text),
                ["auth_ip"] = ExtractFirst(@"v4ip='([^']*)'", text),
                ["online_user"] = ExtractFirst(@"uid='([^']*)'", text),
                ["display_name"] = ExtractFirst(@"NID='([^']*)'", text),
                ["login_time"] = ExtractFirst(@"stime='([^']*)'", text),
                ["logout_time"] = ExtractFirst(@"etime='([^']*)'", text),
                ["online_seconds"] = ExtractInt(@"time='(\d+)", text),
                ["used_flow_bytes"] = ExtractInt(@"flow='(\d+)", text),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            return BuildEmptyPortal();
        }
    }

    private async Task<Dictionary<string, object?>> CheckExternalConnectivityAsync(NetworkSettingsDocument settings, CancellationToken cancellationToken)
    {
        var lastError = string.Empty;
        foreach (var url in settings.ConnectivityCheckUrls)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await SendAsync(request, TimeSpan.FromSeconds(4), cancellationToken).ConfigureAwait(false);
                var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var target = new Uri(url);
                var finalUri = response.RequestMessage?.RequestUri ?? target;
                if (!string.Equals(finalUri.Host, target.Host, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(finalUri.Host, new Uri(settings.PortalUrl).Host, StringComparison.OrdinalIgnoreCase)
                    || text.Contains("Dr.COM", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("注销页", StringComparison.Ordinal)
                    || IsAuthenticationPage(text, target))
                {
                    lastError = "探测请求返回认证页面，未认定外网已连通";
                    continue;
                }
                // A target's own 4xx response still proves reachability; do not reauthenticate for a missing page.
                // Proxy authentication is different: it does not establish external connectivity.
                var code = (int)response.StatusCode;
                if (response.IsSuccessStatusCode || code is >= 400 and < 500 && response.StatusCode != HttpStatusCode.ProxyAuthenticationRequired)
                {
                    return new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["connected"] = true, ["checked_url"] = finalUri.ToString(),
                        ["status_code"] = code, ["detail"] = response.IsSuccessStatusCode ? "外网访问正常" : $"外网目标已响应（HTTP {code}）",
                    };
                }

                lastError = $"状态码 {(int)response.StatusCode}";
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["connected"] = false,
            ["checked_url"] = settings.ConnectivityCheckUrls[0],
            ["status_code"] = 0,
            ["detail"] = string.IsNullOrWhiteSpace(lastError) ? "外网访问失败" : lastError,
        };
    }

    private static bool IsAuthenticationPage(string text, Uri target)
    {
        // Inspect actual forms, not JavaScript strings containing login UI templates.
        using var document = new HtmlParser().ParseDocument(text);
        return document.QuerySelectorAll("form").Any(form =>
            form.QuerySelector("input[type=password]") is not null
            || Uri.TryCreate(target, form.GetAttribute("action"), out var action)
                && Regex.IsMatch(action.AbsolutePath, @"/(?:login|signin|sign-in|auth|sso)(?:[/?#._-]|$)", RegexOptions.IgnoreCase));
    }

    private async Task<Dictionary<string, object?>> LoadLocalNetworkSnapshotAsync(string localIp = "", CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _commandExecutor.RunAsync("ipconfig", ["/all"], TimeSpan.FromSeconds(4), cancellationToken).ConfigureAwait(false);
            var adapters = ParseIpConfigOutput(result.Stdout);
            var selected = adapters
                .Where(adapter => !string.IsNullOrWhiteSpace(Text(adapter.GetValueOrDefault("ipv4"))) || FilterStrings(adapter.GetValueOrDefault("default_gateway")).Count > 0)
                .DefaultIfEmpty(adapters.FirstOrDefault() ?? [])
                .MaxBy(adapter => ScoreAdapter(adapter, localIp));

            if (selected is null || selected.Count == 0)
            {
                return [];
            }

            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = Text(selected.GetValueOrDefault("name")),
                ["description"] = Text(selected.GetValueOrDefault("description")),
                ["ipv4"] = Text(selected.GetValueOrDefault("ipv4")),
                ["subnet_mask"] = Text(selected.GetValueOrDefault("subnet_mask")),
                ["default_gateway"] = FirstString(selected.GetValueOrDefault("default_gateway")),
                ["default_gateway_list"] = FilterStrings(selected.GetValueOrDefault("default_gateway")),
                ["dns_servers"] = FilterStrings(selected.GetValueOrDefault("dns_servers")),
                ["mac_address"] = Text(selected.GetValueOrDefault("mac_address")),
                ["dhcp_enabled"] = Text(selected.GetValueOrDefault("dhcp_enabled")),
                ["dhcp_server"] = Text(selected.GetValueOrDefault("dhcp_server")),
                ["media_state"] = Text(selected.GetValueOrDefault("media_state")),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            return [];
        }
    }

    private async Task<CampusPortalLoginResult> SendLoginRequestAsync(NetworkSettingsDocument settings, string username, string password, CancellationToken cancellationToken)
    {
        var callback = $"dr{Random.Shared.Next(1000, 9999)}";
        var query = new Dictionary<string, string>
        {
            ["callback"] = callback,
            ["DDDDD"] = username,
            ["upass"] = password,
            ["0MKKey"] = "123456",
            ["R1"] = "0",
            ["R2"] = string.Empty,
            ["R3"] = "0",
            ["R6"] = "0",
            ["para"] = "00",
            ["v6ip"] = string.Empty,
            ["terminal_type"] = "1",
            ["lang"] = "zh-cn",
            ["jsVersion"] = "4.2",
            ["v"] = Random.Shared.Next(1000, 9999).ToString(),
        };
        var url = $"{new Uri(new Uri(settings.PortalUrl), "drcom/login")}?{string.Join("&", query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"))}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await SendAsync(request, TimeSpan.FromSeconds(4), cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return CampusPortalLoginResult.Parse(text, (int)response.StatusCode, username, password);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        return await _httpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
    }

    private NetworkContext LoadContext(string sessionId)
    {
        var cleaned = (sessionId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            throw new AuthException(401, "会话不能为空");
        }

        var session = _dataRoot.Sessions().Load(cleaned)
            ?? throw new AuthException(401, "会话不存在或已过期，请重新登录");
        var studentId = string.IsNullOrWhiteSpace(session.StudentId) ? session.User : session.StudentId;
        var studentStore = _dataRoot.ForStudent(studentId);
        var settings = AuthSettingsCatalog.Normalize(studentStore.LoadPreferences() ?? AuthSettingsCatalog.CreateDefault());
        return new NetworkContext(session, studentStore, settings);
    }

    private static Dictionary<string, NetworkProviderCredential> BuildCredentialBundle(XkeStudentStore studentStore)
    {
        var result = new Dictionary<string, NetworkProviderCredential>(StringComparer.Ordinal);
        foreach (var provider in ProviderKeys)
        {
            var stored = studentStore.NetworkCredentials().Load(provider);
            result[provider] = new NetworkProviderCredential(
                Account: stored?.Account ?? string.Empty,
                Password: MaskSecret(stored?.Password ?? string.Empty));
        }

        return result;
    }

    private static void SaveProviderCredentials(XkeStudentStore studentStore, NetworkConfigRequest request)
    {
        SaveProviderCredential(studentStore, "campus", request.CampusAccount, request.CampusPassword);
        SaveProviderCredential(studentStore, "telecom", request.TelecomAccount, request.TelecomPassword);
        SaveProviderCredential(studentStore, "unicom", request.UnicomAccount, request.UnicomPassword);
        SaveProviderCredential(studentStore, "mobile", request.MobileAccount, request.MobilePassword);
    }

    private static void SaveProviderCredential(XkeStudentStore studentStore, string provider, string account, string password)
    {
        var current = studentStore.NetworkCredentials().Load(provider);
        var normalizedAccount = string.IsNullOrWhiteSpace(account) ? current?.Account ?? string.Empty : account.Trim();
        var rawPassword = password ?? string.Empty;
        var unchangedPassword = rawPassword.Length == 0 || rawPassword == MaskSecret(current?.Password ?? string.Empty);
        var normalizedPassword = unchangedPassword
            ? normalizedAccount == current?.Account ? current.Password : string.Empty
            : rawPassword;
        if (string.IsNullOrWhiteSpace(normalizedAccount) && string.IsNullOrWhiteSpace(normalizedPassword) && current is null)
        {
            return;
        }

        // Network passwords are independent from teaching-system credentials and DPAPI protected.
        studentStore.NetworkCredentials().Save(new StoredNetworkCredentialDocument
        {
            Provider = provider,
            Account = normalizedAccount,
            Password = normalizedPassword,
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
        });
    }

    private NetworkLoginCredentials ResolveLoginCredentials(NetworkContext context, string provider)
    {
        var stored = context.StudentStore.NetworkCredentials().Load(provider);
        if (stored is null || string.IsNullOrWhiteSpace(stored.Account) || string.IsNullOrWhiteSpace(stored.Password))
        {
            throw new NetworkException(400, $"未配置{ProviderLabel(provider)}账号或密码");
        }

        if (provider == "campus" || stored.Account.Contains('@', StringComparison.Ordinal))
        {
            return new NetworkLoginCredentials(stored.Account, stored.Password, [stored.Account]);
        }

        var usernames = ProviderSuffixes(provider)
            .Select(suffix => $"{stored.Account}{suffix}")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new NetworkLoginCredentials(stored.Account, stored.Password, usernames.Length == 0 ? [stored.Account] : usernames);
    }

    private void RecordNetworkAuth(
        XkeStudentStore studentStore,
        string provider,
        string account,
        bool success,
        NetworkStatusPayload status,
        IReadOnlyList<Dictionary<string, object?>> attempts,
        string message)
    {
        var history = studentStore.LoadNetworkAuthHistory();
        var records = history.Records.ToList();
        records.Add(new NetworkAuthRecordDocument
        {
            RecordedAt = DateTimeOffset.Now.ToString("O"),
            Provider = provider,
            ProviderLabel = ProviderLabel(provider),
            Account = MaskSecret(account),
            Success = success,
            Message = message,
            Attempts = attempts,
            Status = StatusToDictionary(status),
        });
        if (records.Count > 200)
        {
            records = records.Skip(records.Count - 200).ToList();
        }

        studentStore.SaveNetworkAuthHistory(new NetworkAuthHistoryDocument { Records = records });
    }

    private static Dictionary<string, object?> StatusToDictionary(NetworkStatusPayload status)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["checked_at"] = status.CheckedAt,
            ["campus_connected"] = status.CampusConnected,
            ["internet_connected"] = status.InternetConnected,
            ["needs_heartbeat"] = status.NeedsHeartbeat,
            ["portal_host"] = status.PortalHost,
            ["portal"] = status.Portal,
            ["interface"] = status.Interface,
            ["external"] = status.External,
            ["provider"] = status.Provider,
            ["provider_label"] = status.ProviderLabel,
            ["action"] = status.Action,
            ["attempts"] = status.Attempts,
            ["message"] = status.Message,
            ["operation_error"] = status.OperationError,
        };
    }

    private static NetworkStatusPayload WithMaintainFields(
        NetworkStatusPayload status,
        string provider,
        string action,
        IReadOnlyList<Dictionary<string, object?>> attempts,
        string message)
    {
        return status with
        {
            Provider = provider,
            ProviderLabel = ProviderLabel(provider),
            Action = action,
            Attempts = attempts,
            Message = message,
        };
    }

    private static string NormalizeProvider(string provider)
    {
        var cleaned = (provider ?? string.Empty).Trim().ToLowerInvariant();
        return AppOptions.NetworkProviders.Contains(cleaned, StringComparer.Ordinal) ? cleaned : "campus";
    }

    private static string ProviderLabel(string provider)
    {
        return NormalizeProvider(provider) switch
        {
            "telecom" => "中国电信",
            "unicom" => "中国联通",
            "mobile" => "中国移动",
            _ => "校园网",
        };
    }

    private static IReadOnlyList<string> ProviderSuffixes(string provider)
    {
        return NormalizeProvider(provider) switch
        {
            "telecom" => ["@telecom", "@dx"],
            "unicom" => ["@unicom", "@lt"],
            "mobile" => ["@cmcc"],
            _ => [string.Empty],
        };
    }

    private static Dictionary<string, object?> BuildEmptyPortal()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["local_ip"] = string.Empty,
            ["auth_ip"] = string.Empty,
            ["online_user"] = string.Empty,
            ["display_name"] = string.Empty,
            ["login_time"] = string.Empty,
            ["logout_time"] = string.Empty,
            ["online_seconds"] = null,
            ["used_flow_bytes"] = null,
        };
    }

    private static string MapIpConfigField(string label)
    {
        var normalized = Regex.Replace(label ?? string.Empty, @"[\s\.\u3000]+", string.Empty).ToLowerInvariant();
        if (normalized.Contains("description", StringComparison.Ordinal) || normalized.Contains("描述", StringComparison.Ordinal)) return "description";
        if (normalized.Contains("ipv4address", StringComparison.Ordinal) || normalized.Contains("ipv4地址", StringComparison.Ordinal)) return "ipv4";
        if (normalized.Contains("subnetmask", StringComparison.Ordinal) || normalized.Contains("子网掩码", StringComparison.Ordinal)) return "subnet_mask";
        if (normalized.Contains("defaultgateway", StringComparison.Ordinal) || normalized.Contains("默认网关", StringComparison.Ordinal)) return "default_gateway";
        if (normalized.Contains("dnsservers", StringComparison.Ordinal) || normalized.Contains("dns服务器", StringComparison.Ordinal)) return "dns_servers";
        if (normalized.Contains("physicaladdress", StringComparison.Ordinal) || normalized.Contains("物理地址", StringComparison.Ordinal)) return "mac_address";
        if (normalized.Contains("dhcpenabled", StringComparison.Ordinal) || normalized.Contains("dhcp已启用", StringComparison.Ordinal)) return "dhcp_enabled";
        if (normalized.Contains("dhcpserver", StringComparison.Ordinal) || normalized.Contains("dhcp服务器", StringComparison.Ordinal)) return "dhcp_server";
        if (normalized.Contains("mediastate", StringComparison.Ordinal) || normalized.Contains("媒体状态", StringComparison.Ordinal)) return "media_state";
        return string.Empty;
    }

    private static string CleanIpConfigValue(string value)
    {
        return Regex.Replace((value ?? string.Empty).Trim(), @"\((?:Preferred|首选)\)\s*$", string.Empty, RegexOptions.IgnoreCase).Trim();
    }

    private static int ScoreAdapter(Dictionary<string, object?> adapter, string localIp)
    {
        var score = 0;
        var nameBlob = $"{Text(adapter.GetValueOrDefault("name"))} {Text(adapter.GetValueOrDefault("description"))}".ToLowerInvariant();
        var mediaState = Text(adapter.GetValueOrDefault("media_state")).ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(localIp) && Text(adapter.GetValueOrDefault("ipv4")) == localIp) score += 200;
        if (FilterStrings(adapter.GetValueOrDefault("default_gateway")).Count > 0) score += 40;
        if (FilterStrings(adapter.GetValueOrDefault("dns_servers")).Count > 0) score += 18;
        if (!string.IsNullOrWhiteSpace(Text(adapter.GetValueOrDefault("ipv4")))) score += 20;
        if (mediaState.Contains("disconnected", StringComparison.Ordinal) || mediaState.Contains("断开", StringComparison.Ordinal)) score -= 120;
        foreach (var token in new[] { "vmware", "hyper-v", "wsl", "virtual", "tunnel", "mihomo", "loopback", "vethernet" })
        {
            if (nameBlob.Contains(token, StringComparison.Ordinal)) score -= 40;
        }

        if (nameBlob.Contains("ethernet", StringComparison.Ordinal) || nameBlob.Contains("以太网", StringComparison.Ordinal)) score += 10;
        if (nameBlob.Contains("wlan", StringComparison.Ordinal) || nameBlob.Contains("wi-fi", StringComparison.Ordinal) || nameBlob.Contains("wireless", StringComparison.Ordinal) || nameBlob.Contains("无线", StringComparison.Ordinal)) score += 8;
        return score;
    }

    private static List<string> FilterStrings(object? value)
    {
        if (value is IEnumerable<string> values)
        {
            return values.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).ToList();
        }

        if (value is IEnumerable<object?> objects)
        {
            return objects.Select(Text).Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
        }

        var text = Text(value);
        return string.IsNullOrWhiteSpace(text) ? [] : [text];
    }

    private static string FirstString(object? value)
    {
        return FilterStrings(value).FirstOrDefault() ?? string.Empty;
    }

    private static string Text(object? value)
    {
        return value switch
        {
            null => string.Empty,
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonElement element => element.ToString(),
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static string MaskSecret(string value)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length switch
        {
            0 => string.Empty,
            <= 2 => new string('*', text.Length),
            <= 6 => $"{text[..1]}{new string('*', text.Length - 2)}{text[^1..]}",
            _ => $"{text[..2]}{new string('*', text.Length - 4)}{text[^2..]}",
        };
    }

    private static string ExtractFirst(string pattern, string text)
    {
        var match = Regex.Match(text ?? string.Empty, pattern, RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static int? ExtractInt(string pattern, string text)
    {
        return int.TryParse(ExtractFirst(pattern, text), out var value) ? value : null;
    }

    private sealed record NetworkContext(
        StoredSessionDocument Session,
        XkeStudentStore StudentStore,
        AppSettingsDocument Settings);

    private sealed record NetworkLoginCredentials(string Account, string Password, IReadOnlyList<string> Usernames);
}
