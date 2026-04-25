using System.Security.Cryptography;
using System.Text.Json;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Contracts.Responses;
using Xke.Storage;

namespace Xke.Core.Telemetry;

public sealed class TelemetryService
{
    private const int MaxSpoolRetryPerReport = 8;

    private readonly XkeDataRoot _dataRoot;
    private readonly ITelemetryTransport _transport;

    public TelemetryService(XkeDataRoot dataRoot, ITelemetryTransport transport)
    {
        _dataRoot = dataRoot;
        _transport = transport;
    }

    public async Task<(string Message, TelemetryReportPayload Payload)> ReportAsync(
        string sessionId,
        TelemetryReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var context = LoadContext(sessionId);
        var now = DateTimeOffset.Now.ToString("O");
        var installation = _dataRoot.Telemetry().LoadOrCreateInstallationProfile();
        var keyProfile = _dataRoot.Telemetry().LoadOrCreateKeyProfile();
        _dataRoot.Telemetry().EnsureSpoolDirectory();

        var runtime = context.StudentStore.LoadAppRuntimeInfo();
        var sessionKey = ResolveSessionKey(sessionId, request.SessionStartedAt);
        var sessionStartedAt = string.IsNullOrWhiteSpace(request.SessionStartedAt) ? now : request.SessionStartedAt.Trim();
        MergeRuntime(runtime, sessionKey, sessionStartedAt, request, now);
        context.StudentStore.SaveAppRuntimeInfo(runtime);

        var snapshot = BuildSnapshot(context, request, runtime, installation, now);
        var snapshotPath = context.StudentStore.SaveRemoteSensingSnapshot(snapshot);
        var spoolPayload = BuildTelemetrySpoolPayload(snapshot, now);
        var spoolPath = _dataRoot.Telemetry().SaveSpoolItem(spoolPayload, now);
        var cloud = await ResolveCloudResultAsync(request, spoolPayload, spoolPath, cancellationToken)
            .ConfigureAwait(false);

        installation["last_reported_at"] = now;
        _dataRoot.Telemetry().SaveInstallationProfile(installation);

        return (
            "本地状态已记录",
            new TelemetryReportPayload(
                InstallUuid: Text(installation.GetValueOrDefault("install_uuid")),
                FirstUsedAt: Text(installation.GetValueOrDefault("first_used_at")),
                ReportedAt: now,
                Cloud: cloud,
                Encryption: new TelemetryEncryptionSummary(
                    Algorithm: keyProfile.Algorithm,
                    Fingerprint: keyProfile.Fingerprint,
                    KeyPath: ResolveKeyPath(keyProfile.SourcePath)),
                SnapshotPath: snapshotPath));
    }

    private async Task<TelemetryCloudResult> ResolveCloudResultAsync(
        TelemetryReportRequest request,
        Dictionary<string, object?> spoolPayload,
        string spoolPath,
        CancellationToken cancellationToken)
    {
        if (!request.CloudUploadEnabled)
        {
            return new TelemetryCloudResult(
                Ok: true,
                Queued: true,
                Skipped: true,
                Reason: string.IsNullOrWhiteSpace(request.CloudSkipReason)
                    ? "cloud-upload-disabled"
                    : Truncate(request.CloudSkipReason, 64));
        }

        TelemetryCloudResult result;
        try
        {
            result = await _transport.ReportAsync(spoolPayload, request.CloudSkipReason, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new TelemetryCloudResult(
                Ok: false,
                Queued: true,
                Skipped: false,
                Reason: "cloud-unavailable");
        }

        if (ShouldRemoveSpoolItem(result))
        {
            _dataRoot.Telemetry().DeleteSpoolItem(spoolPath);
            await FlushPendingSpoolAsync(spoolPath, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private async Task FlushPendingSpoolAsync(string currentSpoolPath, CancellationToken cancellationToken)
    {
        foreach (var spoolPath in _dataRoot.Telemetry().EnumerateSpoolItems(MaxSpoolRetryPerReport))
        {
            if (string.Equals(spoolPath, currentSpoolPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var payload = _dataRoot.Telemetry().LoadSpoolItem(spoolPath);
            if (payload.Count == 0)
            {
                continue;
            }

            TelemetryCloudResult result;
            try
            {
                result = await _transport.ReportAsync(payload, "queued-retry", cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return;
            }

            if (!ShouldRemoveSpoolItem(result))
            {
                return;
            }

            _dataRoot.Telemetry().DeleteSpoolItem(spoolPath);
        }
    }

    private static bool ShouldRemoveSpoolItem(TelemetryCloudResult result)
    {
        return result.Ok
               && !result.Queued
               && (result.Skipped || string.Equals(result.Reason, "uploaded", StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, object?> BuildTelemetrySpoolPayload(Dictionary<string, object?> snapshot, string queuedAt)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema_version"] = "1",
            ["kind"] = "telemetry_snapshot",
            ["queued_at"] = queuedAt,
            ["payload"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["schema_version"] = snapshot.GetValueOrDefault("schema_version"),
                ["reported_at"] = snapshot.GetValueOrDefault("reported_at"),
                ["reason"] = snapshot.GetValueOrDefault("reason"),
                ["student_id"] = snapshot.GetValueOrDefault("student_id"),
                ["student_id_hash"] = snapshot.GetValueOrDefault("student_id_hash"),
                ["install_uuid"] = snapshot.GetValueOrDefault("install_uuid"),
                ["active_tab"] = snapshot.GetValueOrDefault("active_tab"),
                ["module_hint"] = snapshot.GetValueOrDefault("module_hint"),
                ["build"] = snapshot.GetValueOrDefault("build"),
                ["feature_usage_deltas"] = snapshot.GetValueOrDefault("feature_usage_deltas"),
                ["runtime"] = snapshot.GetValueOrDefault("runtime"),
            },
        };
    }

    private string ResolveKeyPath(string sourcePath)
    {
        var value = (sourcePath ?? string.Empty).Trim();
        return value.StartsWith("client://", StringComparison.Ordinal)
            ? value
            : _dataRoot.RelativePath(value);
    }

    public (string Message, Dictionary<string, object?> Payload) GetOverview(string sessionId)
    {
        var context = LoadContext(sessionId);
        var profiles = EnumerateProfiles([context.StudentId]);
        return ("本地统计概览已加载", BuildOverview(profiles));
    }

    public (string Message, Dictionary<string, object?> Payload) GetProfile(string sessionId, string studentId)
    {
        var context = LoadContext(sessionId);
        var requestedStudentId = (studentId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(requestedStudentId))
        {
            throw new TelemetryException(400, "student_id 缺失");
        }

        if (!string.Equals(requestedStudentId, context.StudentId, StringComparison.Ordinal))
        {
            throw new TelemetryException(403, "只能查看当前登录账号的本地统计数据");
        }

        var profile = BuildProfile(_dataRoot.ForStudent(requestedStudentId));
        var runtime = GetDictionary(profile, "runtime");
        var networkAuth = GetDictionary(profile, "network_auth");
        var hasLocalData =
            !string.IsNullOrWhiteSpace(Text(profile.GetValueOrDefault("display_name")))
            || ConvertToInt(runtime.GetValueOrDefault("session_count")) > 0
            || ConvertToInt(networkAuth.GetValueOrDefault("record_count")) > 0;
        if (!hasLocalData)
        {
            throw new TelemetryException(404, "未找到该学生的本地统计数据");
        }

        return ("本地统计档案已加载", profile);
    }

    private TelemetryContext LoadContext(string sessionId)
    {
        var cleaned = (sessionId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            throw new TelemetryException(401, "会话不存在或已过期，请重新登录");
        }

        var session = _dataRoot.Sessions().Load(cleaned)
            ?? throw new TelemetryException(401, "会话不存在或已过期，请重新登录");
        var studentId = string.IsNullOrWhiteSpace(session.StudentId) ? session.User : session.StudentId;
        return new TelemetryContext(session, studentId, _dataRoot.ForStudent(studentId));
    }

    private static string ResolveSessionKey(string sessionId, string sessionStartedAt)
    {
        var cleanedSessionId = (sessionId ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(cleanedSessionId))
        {
            return cleanedSessionId;
        }

        var fallback = (sessionStartedAt ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(fallback) ? Guid.NewGuid().ToString("N") : fallback;
    }

    private static void MergeRuntime(
        Dictionary<string, object?> runtime,
        string sessionKey,
        string sessionStartedAt,
        TelemetryReportRequest request,
        string now)
    {
        runtime["schema_version"] = "2026-04-23";
        var lifecycle = EnsureDictionary(runtime, "lifecycle");
        var usage = EnsureDictionary(runtime, "usage");
        var sessions = EnsureDictionaryList(runtime, "sessions");
        var featureUsage = EnsureDictionary(runtime, "feature_usage");

        if (string.IsNullOrWhiteSpace(Text(lifecycle.GetValueOrDefault("first_opened_at"))))
        {
            lifecycle["first_opened_at"] = now;
            lifecycle["first_initialized_login_at"] = now;
        }

        lifecycle["last_opened_at"] = now;
        lifecycle["last_initialized_login_at"] = now;

        var session = sessions.FirstOrDefault(item => string.Equals(Text(item.GetValueOrDefault("session_key")), sessionKey, StringComparison.Ordinal));
        if (session is null)
        {
            session = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["session_key"] = sessionKey,
                ["session_started_at"] = sessionStartedAt,
                ["first_seen_at"] = now,
                ["foreground_seconds"] = 0,
                ["background_seconds"] = 0,
                ["active_tab"] = string.Empty,
                ["build"] = string.Empty,
            };
            sessions.Add(session);
            lifecycle["app_open_count"] = ConvertToInt(lifecycle.GetValueOrDefault("app_open_count")) + 1;
        }

        var foregroundDelta = Math.Max(0, request.ForegroundSecondsDelta);
        var backgroundDelta = Math.Max(0, request.BackgroundSecondsDelta);
        session["last_reported_at"] = now;
        session["foreground_seconds"] = ConvertToInt(session.GetValueOrDefault("foreground_seconds")) + foregroundDelta;
        session["background_seconds"] = ConvertToInt(session.GetValueOrDefault("background_seconds")) + backgroundDelta;
        session["active_tab"] = Truncate(request.ActiveTab, 48);
        session["build"] = Truncate(request.Build, 64);

        usage["total_runtime_seconds"] = ConvertToInt(usage.GetValueOrDefault("total_runtime_seconds")) + foregroundDelta + backgroundDelta;
        usage["foreground_seconds"] = ConvertToInt(usage.GetValueOrDefault("foreground_seconds")) + foregroundDelta;
        usage["background_seconds"] = ConvertToInt(usage.GetValueOrDefault("background_seconds")) + backgroundDelta;
        usage["last_reported_at"] = now;
        usage["last_active_tab"] = Truncate(request.ActiveTab, 48);
        usage["last_build"] = Truncate(request.Build, 64);

        foreach (var (moduleId, modulePayload) in request.FeatureUsageDeltas)
        {
            if (modulePayload is not Dictionary<string, object> rawModule)
            {
                continue;
            }

            var bucket = EnsureDictionary(featureUsage, Truncate(moduleId, 64));
            bucket["open_count"] = ConvertToInt(bucket.GetValueOrDefault("open_count")) + ConvertToInt(rawModule.GetValueOrDefault("open_count"));
            bucket["foreground_seconds_delta"] = ConvertToInt(bucket.GetValueOrDefault("foreground_seconds_delta")) + ConvertToInt(rawModule.GetValueOrDefault("foreground_seconds_delta"));
            bucket["background_seconds_delta"] = ConvertToInt(bucket.GetValueOrDefault("background_seconds_delta")) + ConvertToInt(rawModule.GetValueOrDefault("background_seconds_delta"));
        }
    }

    private Dictionary<string, object?> BuildSnapshot(
        TelemetryContext context,
        TelemetryReportRequest request,
        Dictionary<string, object?> runtime,
        Dictionary<string, object?> installation,
        string now)
    {
        var featureUsage = request.FeatureUsageDeltas.ToDictionary(
            pair => Truncate(pair.Key, 64),
            pair => (object?)pair.Value,
            StringComparer.Ordinal);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema_version"] = "2026-04-23",
            ["reported_at"] = now,
            ["reason"] = Truncate(request.Reason, 32),
            ["student_id"] = context.StudentId,
            ["student_id_hash"] = ComputeStudentHash(context.StudentId),
            ["session_id"] = context.Session.SessionIdHash,
            ["session_started_at"] = Truncate(request.SessionStartedAt, 48),
            ["active_tab"] = Truncate(request.ActiveTab, 48),
            ["module_hint"] = Truncate(request.ModuleHint, 32),
            ["build"] = Truncate(request.Build, 64),
            ["feature_usage_deltas"] = featureUsage,
            ["runtime"] = runtime,
            ["install_uuid"] = Text(installation.GetValueOrDefault("install_uuid")),
            ["cloud_upload_enabled"] = request.CloudUploadEnabled,
            ["cloud_skip_reason"] = Truncate(request.CloudSkipReason, 64),
            ["storage"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["student_dir"] = context.StudentStore.RelativeDirectory(),
                ["runtime_path"] = _dataRoot.RelativePath(context.StudentStore.AppRuntimePath),
                ["snapshot_path"] = _dataRoot.RelativePath(context.StudentStore.RemoteSensingSnapshotPath),
            },
        };
    }

    private IReadOnlyList<Dictionary<string, object?>> EnumerateProfiles(IReadOnlyList<string>? allowedIds = null)
    {
        var allow = new HashSet<string>((allowedIds ?? Array.Empty<string>()).Where(item => !string.IsNullOrWhiteSpace(item)), StringComparer.Ordinal);
        if (!Directory.Exists(_dataRoot.StudentsDirectory))
        {
            return [];
        }

        return Directory.EnumerateDirectories(_dataRoot.StudentsDirectory)
            .Select(path => Path.GetFileName(path))
            .Where(studentId => allow.Count == 0 || allow.Contains(studentId))
            .Select(studentId => BuildProfile(_dataRoot.ForStudent(studentId)))
            .ToArray();
    }

    private Dictionary<string, object?> BuildOverview(IReadOnlyList<Dictionary<string, object?>> profiles)
    {
        var providerCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var buildCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var latestReportedAt = string.Empty;
        var latestOpenedAt = string.Empty;
        var latestNetworkAuthAt = string.Empty;

        foreach (var profile in profiles)
        {
            var runtime = GetDictionary(profile, "runtime");
            var usage = GetDictionary(runtime, "usage");
            var lifecycle = GetDictionary(runtime, "lifecycle");
            var networkAuth = GetDictionary(profile, "network_auth");
            latestReportedAt = MaxText(latestReportedAt, Text(usage.GetValueOrDefault("last_reported_at")));
            latestOpenedAt = MaxText(latestOpenedAt, Text(lifecycle.GetValueOrDefault("last_opened_at")));
            latestNetworkAuthAt = MaxText(latestNetworkAuthAt, Text(networkAuth.GetValueOrDefault("latest_recorded_at")));

            foreach (var record in GetDictionaryList(networkAuth, "records"))
            {
                var provider = Text(record.GetValueOrDefault("provider"));
                if (!string.IsNullOrWhiteSpace(provider))
                {
                    providerCounts[provider] = providerCounts.GetValueOrDefault(provider) + 1;
                }
            }

            foreach (var session in GetDictionaryList(runtime, "sessions"))
            {
                var build = Text(session.GetValueOrDefault("build"));
                if (!string.IsNullOrWhiteSpace(build))
                {
                    buildCounts[build] = buildCounts.GetValueOrDefault(build) + 1;
                }
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema_version"] = "2026-04-23",
            ["source"] = "local-data",
            ["data_root"] = _dataRoot.RelativePath(_dataRoot.RootPath),
            ["updated_at"] = MaxText(MaxText(latestReportedAt, latestOpenedAt), latestNetworkAuthAt),
            ["student_count"] = profiles.Count,
            ["total_app_open_count"] = profiles.Sum(profile => ConvertToInt(GetDictionary(GetDictionary(profile, "runtime"), "lifecycle").GetValueOrDefault("app_open_count"))),
            ["total_runtime_seconds"] = profiles.Sum(profile => ConvertToInt(GetDictionary(GetDictionary(profile, "runtime"), "usage").GetValueOrDefault("total_runtime_seconds"))),
            ["total_foreground_seconds"] = profiles.Sum(profile => ConvertToInt(GetDictionary(GetDictionary(profile, "runtime"), "usage").GetValueOrDefault("foreground_seconds"))),
            ["total_background_seconds"] = profiles.Sum(profile => ConvertToInt(GetDictionary(GetDictionary(profile, "runtime"), "usage").GetValueOrDefault("background_seconds"))),
            ["total_session_count"] = profiles.Sum(profile => ConvertToInt(GetDictionary(profile, "runtime").GetValueOrDefault("session_count"))),
            ["total_network_auth_records"] = profiles.Sum(profile => ConvertToInt(GetDictionary(profile, "network_auth").GetValueOrDefault("record_count"))),
            ["latest_reported_at"] = latestReportedAt,
            ["latest_opened_at"] = latestOpenedAt,
            ["latest_network_auth_at"] = latestNetworkAuthAt,
            ["provider_counts"] = providerCounts.ToDictionary(pair => pair.Key, pair => (object?)pair.Value, StringComparer.Ordinal),
            ["build_counts"] = buildCounts.ToDictionary(pair => pair.Key, pair => (object?)pair.Value, StringComparer.Ordinal),
            ["students"] = profiles.Select(profile => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["student_id"] = Text(profile.GetValueOrDefault("student_id")),
                ["display_name"] = Text(profile.GetValueOrDefault("display_name")),
                ["updated_at"] = Text(profile.GetValueOrDefault("updated_at")),
                ["app_open_count"] = ConvertToInt(GetDictionary(GetDictionary(profile, "runtime"), "lifecycle").GetValueOrDefault("app_open_count")),
                ["runtime_seconds"] = ConvertToInt(GetDictionary(GetDictionary(profile, "runtime"), "usage").GetValueOrDefault("total_runtime_seconds")),
                ["session_count"] = ConvertToInt(GetDictionary(profile, "runtime").GetValueOrDefault("session_count")),
                ["network_auth_records"] = ConvertToInt(GetDictionary(profile, "network_auth").GetValueOrDefault("record_count")),
                ["last_reported_at"] = Text(GetDictionary(GetDictionary(profile, "runtime"), "usage").GetValueOrDefault("last_reported_at")),
            }).Cast<object?>().ToArray(),
        };
    }

    private Dictionary<string, object?> BuildProfile(XkeStudentStore studentStore)
    {
        var profile = studentStore.LoadProfile();
        var runtime = studentStore.LoadAppRuntimeInfo();
        var networkHistory = studentStore.LoadNetworkAuthHistory();
        var lifecycle = EnsureDictionary(runtime, "lifecycle");
        var usage = EnsureDictionary(runtime, "usage");
        var sessions = EnsureDictionaryList(runtime, "sessions");
        var records = networkHistory.Records
            .Select(record => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["recorded_at"] = record.RecordedAt,
                ["provider"] = record.Provider,
                ["provider_label"] = record.ProviderLabel,
                ["account"] = record.Account,
                ["success"] = record.Success,
                ["message"] = record.Message,
                ["status"] = record.Status,
                ["attempts"] = record.Attempts,
            })
            .ToArray();
        var updatedAt = MaxText(
            Text(usage.GetValueOrDefault("last_reported_at")),
            MaxText(Text(lifecycle.GetValueOrDefault("last_opened_at")), networkHistory.Records.LastOrDefault()?.RecordedAt ?? string.Empty));
        var displayName = profile?.UseNickname == true && !string.IsNullOrWhiteSpace(profile.Nickname)
            ? profile.Nickname
            : !string.IsNullOrWhiteSpace(profile?.Name)
                ? profile.Name
                : studentStore.StudentId;

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema_version"] = "2026-04-23",
            ["source"] = "local-data",
            ["data_root"] = _dataRoot.RelativePath(_dataRoot.RootPath),
            ["student_id"] = studentStore.StudentId,
            ["display_name"] = displayName,
            ["updated_at"] = updatedAt,
            ["student"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["student_id"] = studentStore.StudentId,
                ["name"] = profile?.Name ?? string.Empty,
                ["nickname"] = profile?.Nickname ?? string.Empty,
                ["use_nickname"] = profile?.UseNickname ?? false,
                ["class_name"] = profile?.ClassName ?? string.Empty,
                ["updated_at"] = profile?.UpdatedAt ?? string.Empty,
            },
            ["runtime"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["lifecycle"] = lifecycle,
                ["usage"] = usage,
                ["session_count"] = sessions.Count,
                ["sessions"] = sessions,
                ["feature_usage"] = EnsureDictionary(runtime, "feature_usage"),
            },
            ["network_auth"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["record_count"] = records.Length,
                ["success_count"] = records.Count(record => record.GetValueOrDefault("success") is true),
                ["failure_count"] = records.Count(record => record.GetValueOrDefault("success") is not true),
                ["records"] = records,
                ["latest_recorded_at"] = networkHistory.Records.LastOrDefault()?.RecordedAt ?? string.Empty,
            },
            ["storage"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["student_dir"] = studentStore.RelativeDirectory(),
                ["info_dir"] = _dataRoot.RelativePath(studentStore.InfoDirectory),
                ["runtime_path"] = _dataRoot.RelativePath(studentStore.AppRuntimePath),
                ["network_auth_history_path"] = _dataRoot.RelativePath(studentStore.NetworkAuthHistoryPath),
            },
            ["raw"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["app_runtime"] = runtime,
                ["network_auth_history"] = records,
            },
        };
    }

    private static Dictionary<string, object?> EnsureDictionary(Dictionary<string, object?> parent, string key)
    {
        if (parent.TryGetValue(key, out var raw) && raw is Dictionary<string, object?> dictionary)
        {
            return dictionary;
        }

        var created = new Dictionary<string, object?>(StringComparer.Ordinal);
        parent[key] = created;
        return created;
    }

    private static List<Dictionary<string, object?>> EnsureDictionaryList(Dictionary<string, object?> parent, string key)
    {
        if (parent.TryGetValue(key, out var raw))
        {
            if (raw is List<Dictionary<string, object?>> typedList)
            {
                return typedList;
            }

            if (raw is IEnumerable<object?> values)
            {
                var converted = values
                    .OfType<Dictionary<string, object?>>()
                    .ToList();
                parent[key] = converted;
                return converted;
            }
        }

        var created = new List<Dictionary<string, object?>>();
        parent[key] = created;
        return created;
    }

    private static int ConvertToInt(object? value)
    {
        return value switch
        {
            int number => number,
            long number => (int)number,
            JsonElement element when element.ValueKind == System.Text.Json.JsonValueKind.Number && element.TryGetInt32(out var number) => number,
            _ when int.TryParse(value?.ToString(), out var parsed) => parsed,
            _ => 0,
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        var cleaned = (value ?? string.Empty).Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }

    private static string Text(object? value)
    {
        return value switch
        {
            null => string.Empty,
            JsonElement element when element.ValueKind == System.Text.Json.JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonElement element => element.ToString(),
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static string MaxText(string left, string right)
    {
        var resolvedLeft = left ?? string.Empty;
        var resolvedRight = right ?? string.Empty;
        return string.CompareOrdinal(resolvedLeft, resolvedRight) >= 0 ? resolvedLeft : resolvedRight;
    }

    private static Dictionary<string, object?> GetDictionary(Dictionary<string, object?> parent, string key)
    {
        return parent.TryGetValue(key, out var raw) && raw is Dictionary<string, object?> dictionary
            ? dictionary
            : [];
    }

    private static IReadOnlyList<Dictionary<string, object?>> GetDictionaryList(Dictionary<string, object?> parent, string key)
    {
        return parent.TryGetValue(key, out var raw) && raw is IEnumerable<Dictionary<string, object?>> dictionaries
            ? dictionaries.ToArray()
            : Array.Empty<Dictionary<string, object?>>();
    }

    private static string ComputeStudentHash(string studentId)
    {
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(studentId ?? string.Empty))).ToLowerInvariant();
    }

    private sealed record TelemetryContext(StoredSessionDocument Session, string StudentId, XkeStudentStore StudentStore);
}
