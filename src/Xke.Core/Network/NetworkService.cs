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

public sealed class NetworkService
{
    private const string PortalHost = "10.199.199.199";
    private const string PortalBaseUrl = $"http://{PortalHost}";
    private const string PortalLoginUrl = $"{PortalBaseUrl}/drcom/login";
    private static readonly string[] ProviderKeys = ["telecom", "unicom", "mobile"];
    private static readonly string[] ExternalCheckUrls = ["https://www.baidu.com/", "https://www.qq.com/"];
    private static readonly Regex IpConfigHeaderPattern = new(@"^(?<prefix>.*?(?:adapter|适配器))\s+(?<name>[^:]+):\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly XkeDataRoot _dataRoot;
    private readonly HttpClient _httpClient;
    private readonly INetworkCommandExecutor _commandExecutor;

    public NetworkService(XkeDataRoot dataRoot, HttpClient httpClient, INetworkCommandExecutor commandExecutor)
    {
        _dataRoot = dataRoot;
        _httpClient = httpClient;
        _commandExecutor = commandExecutor;
    }

    public (string Message, NetworkConfigPayload Payload) GetConfig(string sessionId)
    {
        var context = LoadContext(sessionId);
        return (
            "校园网配置已加载",
            new NetworkConfigPayload(
                Settings: context.Settings.Network,
                Credentials: BuildCredentialBundle(context.StudentStore),
                ApplyResult: null,
                CampusAccount: context.Session.User,
                StorageDir: context.StudentStore.RelativeDirectory()));
    }

    public async Task<(string Message, NetworkConfigPayload Payload)> SaveConfigAsync(
        string sessionId,
        NetworkConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        var context = LoadContext(sessionId);
        var updatedSettings = AuthSettingsCatalog.Apply(
            context.Settings,
            new PreferencesRequest { Network = request });
        context.StudentStore.SavePreferences(updatedSettings);
        SaveProviderCredentials(context.StudentStore, request);

        var applyResult = await BuildApplyResultAsync(updatedSettings.Network, cancellationToken).ConfigureAwait(false);
        var message = "校园网配置已保存";
        if (applyResult is not null && !applyResult.Applied)
        {
            message = applyResult.RequiresElevation
                ? "校园网配置已保存，但当前进程未以管理员权限运行，未实际修改本机 IP"
                : $"校园网配置已保存，但本机 IP 未同步：{applyResult.Error}";
        }

        return (
            message,
            new NetworkConfigPayload(
                Settings: updatedSettings.Network,
                Credentials: BuildCredentialBundle(context.StudentStore),
                ApplyResult: applyResult,
                CampusAccount: context.Session.User,
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

    public async Task<(string Message, NetworkStatusPayload Payload)> MaintainAsync(
        string sessionId,
        NetworkActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var context = LoadContext(sessionId);
        var settings = context.Settings.Network;
        var provider = NormalizeProvider(settings.Provider);
        var status = await DetectStatusAsync(settings, cancellationToken).ConfigureAwait(false);
        status = WithMaintainFields(status, provider, "none", [], string.Empty);

        if (!status.CampusConnected)
        {
            var message = "当前未接入校园网，未发送心跳";
            status = WithMaintainFields(status, provider, "none", [], message);
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

        var credentials = ResolveHeartbeatCredentials(context, provider);
        var attempts = new List<Dictionary<string, object?>>();
        foreach (var username in credentials.Usernames)
        {
            var loginResult = await SendLoginRequestAsync(username, credentials.Password, cancellationToken).ConfigureAwait(false);
            attempts.Add(loginResult);

            var external = await CheckExternalConnectivityAsync(cancellationToken).ConfigureAwait(false);
            if (external.GetValueOrDefault("connected") is true)
            {
                var refreshed = await DetectStatusAsync(settings, cancellationToken).ConfigureAwait(false);
                var message = "校园网心跳已补发，外网访问已恢复";
                refreshed = WithMaintainFields(refreshed, provider, "heartbeat", attempts, message);
                RecordNetworkAuth(context.StudentStore, provider, credentials.Account, true, refreshed, attempts, message);
                return (message, refreshed);
            }
        }

        var failedMessage = "校园网心跳已发送，但外网仍未恢复";
        status = WithMaintainFields(status, provider, "heartbeat", attempts, failedMessage);
        RecordNetworkAuth(context.StudentStore, provider, credentials.Account, false, status, attempts, failedMessage);
        return (failedMessage, status);
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

    private async Task<NetworkApplyResult?> BuildApplyResultAsync(NetworkSettingsDocument settings, CancellationToken cancellationToken)
    {
        if (!string.Equals(NormalizeProvider(settings.Provider), "campus", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(settings.CampusIp))
        {
            return null;
        }

        try
        {
            return await ApplyCampusIpSettingsAsync(settings, cancellationToken).ConfigureAwait(false);
        }
        catch (NetworkException ex)
        {
            var requiresElevation = LooksLikeElevationFailure(ex.Detail);
            return new NetworkApplyResult(false, ex.Detail, requiresElevation, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, []);
        }
        catch (Exception ex)
        {
            return new NetworkApplyResult(false, ex.Message, false, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, []);
        }
    }

    private async Task<NetworkApplyResult> ApplyCampusIpSettingsAsync(NetworkSettingsDocument settings, CancellationToken cancellationToken)
    {
        var currentInterface = await LoadLocalNetworkSnapshotAsync(settings.CampusIp, cancellationToken).ConfigureAwait(false);
        var interfaceName = Text(currentInterface.GetValueOrDefault("name"));
        if (string.IsNullOrWhiteSpace(interfaceName))
        {
            throw new NetworkException(500, "未识别到可用网卡，无法应用校园网 IP 设置");
        }

        var subnetMask = PrefixToMask(settings.CampusSubnetPrefix);
        var gateway = string.IsNullOrWhiteSpace(settings.CampusGateway)
            ? Text(currentInterface.GetValueOrDefault("default_gateway"))
            : settings.CampusGateway;
        var dns = string.IsNullOrWhiteSpace(settings.CampusDns)
            ? FirstString(currentInterface.GetValueOrDefault("dns_servers"))
            : settings.CampusDns;

        // Controlled system side effect: only this method invokes netsh, and failures are returned as apply_result.
        var addressResult = await _commandExecutor.RunAsync(
            "netsh",
            ["interface", "ip", "set", "address", $"name={interfaceName}", "static", settings.CampusIp, subnetMask, string.IsNullOrWhiteSpace(gateway) ? "none" : gateway, "1"],
            TimeSpan.FromSeconds(8),
            cancellationToken).ConfigureAwait(false);
        if (addressResult.ExitCode != 0)
        {
            throw new NetworkException(500, NormalizeCommandError(addressResult, "设置网卡 IP 失败，请确认程序是否以管理员权限运行"));
        }

        var dnsArguments = new List<string> { "interface", "ip", "set", "dns", $"name={interfaceName}", string.IsNullOrWhiteSpace(dns) ? "dhcp" : "static" };
        if (!string.IsNullOrWhiteSpace(dns))
        {
            dnsArguments.Add(dns);
            dnsArguments.Add("primary");
        }

        var dnsResult = await _commandExecutor.RunAsync("netsh", dnsArguments, TimeSpan.FromSeconds(8), cancellationToken).ConfigureAwait(false);
        if (dnsResult.ExitCode != 0)
        {
            throw new NetworkException(500, NormalizeCommandError(dnsResult, "设置 DNS 失败，请确认程序是否以管理员权限运行"));
        }

        var refreshedInterface = await LoadLocalNetworkSnapshotAsync(settings.CampusIp, cancellationToken).ConfigureAwait(false);
        return new NetworkApplyResult(true, string.Empty, false, interfaceName, settings.CampusIp, subnetMask, gateway, dns, refreshedInterface);
    }

    private async Task<NetworkStatusPayload> DetectStatusAsync(NetworkSettingsDocument settings, CancellationToken cancellationToken)
    {
        var campusConnected = await PingPortalAsync(cancellationToken).ConfigureAwait(false);
        var portal = campusConnected
            ? await FetchPortalSnapshotAsync(cancellationToken).ConfigureAwait(false)
            : BuildEmptyPortal();
        var localIp = Text(portal.GetValueOrDefault("local_ip"));
        var networkInterface = await LoadLocalNetworkSnapshotAsync(localIp, cancellationToken).ConfigureAwait(false);
        var external = campusConnected
            ? await CheckExternalConnectivityAsync(cancellationToken).ConfigureAwait(false)
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
            PortalHost: PortalHost,
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

    private async Task<bool> PingPortalAsync(CancellationToken cancellationToken)
    {
        try
        {
            var ping = await _commandExecutor.RunAsync(
                "ping",
                ["-n", "1", "-w", "1200", PortalHost],
                TimeSpan.FromSeconds(3),
                cancellationToken).ConfigureAwait(false);
            if (ping.ExitCode == 0)
            {
                return true;
            }
        }
        catch
        {
            // Fall through to HTTP probing; status detection should stay best-effort.
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, PortalBaseUrl);
            using var response = await SendAsync(request, TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode || response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found;
        }
        catch
        {
            return false;
        }
    }

    private async Task<Dictionary<string, object?>> FetchPortalSnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, PortalBaseUrl);
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
        catch
        {
            return BuildEmptyPortal();
        }
    }

    private async Task<Dictionary<string, object?>> CheckExternalConnectivityAsync(CancellationToken cancellationToken)
    {
        var lastError = string.Empty;
        foreach (var url in ExternalCheckUrls)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await SendAsync(request, TimeSpan.FromSeconds(4), cancellationToken).ConfigureAwait(false);
                var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var finalHost = response.RequestMessage?.RequestUri?.Host ?? string.Empty;
                var preview = text.Length > 240 ? text[..240] : text;
                if (finalHost.Contains(PortalHost, StringComparison.OrdinalIgnoreCase)
                    || preview.Contains("Dr.COM", StringComparison.OrdinalIgnoreCase)
                    || preview.Contains("注销页", StringComparison.Ordinal))
                {
                    lastError = "请求被校园网门户接管";
                    continue;
                }

                if ((int)response.StatusCode < 500)
                {
                    return new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["connected"] = true,
                        ["checked_url"] = response.RequestMessage?.RequestUri?.ToString() ?? url,
                        ["status_code"] = (int)response.StatusCode,
                        ["detail"] = "外网访问正常",
                    };
                }

                lastError = $"状态码 {(int)response.StatusCode}";
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["connected"] = false,
            ["checked_url"] = ExternalCheckUrls[0],
            ["status_code"] = 0,
            ["detail"] = string.IsNullOrWhiteSpace(lastError) ? "外网访问失败" : lastError,
        };
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
        catch
        {
            return [];
        }
    }

    private async Task<Dictionary<string, object?>> SendLoginRequestAsync(string username, string password, CancellationToken cancellationToken)
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
        var url = $"{PortalLoginUrl}?{string.Join("&", query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"))}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await SendAsync(request, TimeSpan.FromSeconds(4), cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["username"] = username,
            ["status_code"] = (int)response.StatusCode,
            ["preview"] = BuildPreview(text),
        };
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
        var credentials = _dataRoot.Credentials().Load(session.User)
            ?? throw new AuthException(401, "本地凭据不存在，请重新登录");
        var studentId = string.IsNullOrWhiteSpace(session.StudentId) ? session.User : session.StudentId;
        var studentStore = _dataRoot.ForStudent(studentId);
        var settings = AuthSettingsCatalog.Normalize(studentStore.LoadPreferences() ?? AuthSettingsCatalog.CreateDefault());
        return new NetworkContext(session, credentials, studentStore, settings);
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
        SaveProviderCredential(studentStore, "telecom", request.TelecomAccount, request.TelecomPassword);
        SaveProviderCredential(studentStore, "unicom", request.UnicomAccount, request.UnicomPassword);
        SaveProviderCredential(studentStore, "mobile", request.MobileAccount, request.MobilePassword);
    }

    private static void SaveProviderCredential(XkeStudentStore studentStore, string provider, string account, string password)
    {
        var current = studentStore.NetworkCredentials().Load(provider);
        var normalizedAccount = string.IsNullOrWhiteSpace(account) ? current?.Account ?? string.Empty : account.Trim();
        var rawPassword = (password ?? string.Empty).Trim();
        var preservePassword = ShouldPreservePassword(rawPassword)
            && string.Equals(normalizedAccount, current?.Account ?? string.Empty, StringComparison.Ordinal);
        var normalizedPassword = preservePassword
            ? current?.Password ?? string.Empty
            : ShouldPreservePassword(rawPassword) ? string.Empty : rawPassword;
        if (string.IsNullOrWhiteSpace(normalizedAccount) && string.IsNullOrWhiteSpace(normalizedPassword) && current is null)
        {
            return;
        }

        // Operator passwords are DPAPI protected at rest; API responses only expose masked values.
        studentStore.NetworkCredentials().Save(new StoredNetworkCredentialDocument
        {
            Provider = provider,
            Account = normalizedAccount,
            Password = normalizedPassword,
            UpdatedAt = DateTimeOffset.Now.ToString("O"),
        });
    }

    private static bool ShouldPreservePassword(string value)
    {
        var cleaned = (value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(cleaned) || cleaned.Contains('*', StringComparison.Ordinal);
    }

    private HeartbeatCredentials ResolveHeartbeatCredentials(NetworkContext context, string provider)
    {
        if (provider == "campus")
        {
            if (string.IsNullOrWhiteSpace(context.Session.User) || string.IsNullOrWhiteSpace(context.Credentials.Password))
            {
                throw new NetworkException(400, "当前会话缺少校园网登录所需的学号或密码");
            }

            return new HeartbeatCredentials(context.Session.User, context.Credentials.Password, [context.Session.User]);
        }

        var stored = context.StudentStore.NetworkCredentials().Load(provider);
        if (stored is null || string.IsNullOrWhiteSpace(stored.Account) || string.IsNullOrWhiteSpace(stored.Password))
        {
            throw new NetworkException(400, $"未配置{ProviderLabel(provider)}账号或密码");
        }

        if (stored.Account.Contains('@', StringComparison.Ordinal))
        {
            return new HeartbeatCredentials(stored.Account, stored.Password, [stored.Account]);
        }

        var usernames = ProviderSuffixes(provider)
            .Select(suffix => $"{stored.Account}{suffix}")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new HeartbeatCredentials(stored.Account, stored.Password, usernames.Length == 0 ? [stored.Account] : usernames);
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
            _ => "校园账号",
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

    private static string PrefixToMask(int prefix)
    {
        var normalized = Math.Clamp(prefix, 1, 32);
        var mask = uint.MaxValue << (32 - normalized);
        return string.Join(".", new[]
        {
            (mask >> 24) & 255,
            (mask >> 16) & 255,
            (mask >> 8) & 255,
            mask & 255,
        }.Select(part => part.ToString()));
    }

    private static bool LooksLikeElevationFailure(string message)
    {
        var resolved = message ?? string.Empty;
        var lower = resolved.ToLowerInvariant();
        return lower.Contains("administrator", StringComparison.Ordinal)
            || lower.Contains("elevation", StringComparison.Ordinal)
            || resolved.Contains("管理员权限", StringComparison.Ordinal);
    }

    private static string NormalizeCommandError(NetworkCommandResult result, string fallback)
    {
        var message = string.IsNullOrWhiteSpace(result.Stderr) ? result.Stdout : result.Stderr;
        return string.IsNullOrWhiteSpace(message) ? fallback : message.Trim();
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

    private static string BuildPreview(string text)
    {
        var normalized = Regex.Replace(text ?? string.Empty, @"\s+", " ");
        return normalized[..Math.Min(220, normalized.Length)];
    }

    private sealed record NetworkContext(
        StoredSessionDocument Session,
        StoredCredentialDocument Credentials,
        XkeStudentStore StudentStore,
        AppSettingsDocument Settings);

    private sealed record HeartbeatCredentials(string Account, string Password, IReadOnlyList<string> Usernames);
}
