using System.Security.Cryptography;
using System.Text;

namespace Xke.Core.CloudSync;

public sealed class RemotePolicyService
{
    private const double MinimumCourseGrabIntervalSeconds = 0.5d;

    public Dictionary<string, object?> NormalizePolicy(Dictionary<string, object?> rawPolicy, Dictionary<string, object?> updateInfo)
    {
        var policy = rawPolicy.Count > 0 ? rawPolicy : [];
        var versionPolicy = GetDictionary(policy, "version_policy");
        var matchedVersionPolicy = SelectVersionPolicy(versionPolicy);
        var featureFlags = NormalizeFeatureFlags(GetDictionary(matchedVersionPolicy, "feature_flags"));
        var featureLimits = NormalizeFeatureLimits(GetDictionary(matchedVersionPolicy, "feature_limits"));
        var runtimePolicy = NormalizeRuntimePolicy(GetDictionary(matchedVersionPolicy, "runtime_policy"));
        var appControl = NormalizeAppControl(GetDictionary(matchedVersionPolicy, "app_control"));
        var normalizedUpdateInfo = NormalizeUpdateInfo(updateInfo);
        var telemetryEnabled = matchedVersionPolicy.GetValueOrDefault("telemetry_enabled") is not false
                               && policy.GetValueOrDefault("telemetry_enabled") is not false;
        var forceUpgrade = matchedVersionPolicy.GetValueOrDefault("force_upgrade") is true
                           || normalizedUpdateInfo.GetValueOrDefault("force_upgrade") is true;

        if (matchedVersionPolicy.GetValueOrDefault("enabled") is false)
        {
            appControl["state"] = "blocked";
            if (string.IsNullOrWhiteSpace(Text(appControl.GetValueOrDefault("reason"))))
            {
                appControl["reason"] = "version-disabled";
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["feature_flags"] = featureFlags,
            ["feature_limits"] = featureLimits,
            ["runtime_policy"] = runtimePolicy,
            ["app_control"] = appControl,
            ["version_policy"] = matchedVersionPolicy,
            ["version_policy_catalog"] = versionPolicy,
            ["update_info"] = normalizedUpdateInfo,
            ["latest_version"] = normalizedUpdateInfo.GetValueOrDefault("latest_version") ?? string.Empty,
            ["min_supported_version"] = normalizedUpdateInfo.GetValueOrDefault("min_supported_version") ?? string.Empty,
            ["download_url"] = normalizedUpdateInfo.GetValueOrDefault("download_url") ?? string.Empty,
            ["package_sha256"] = normalizedUpdateInfo.GetValueOrDefault("package_sha256") ?? string.Empty,
            ["package_size"] = normalizedUpdateInfo.GetValueOrDefault("package_size") ?? 0L,
            ["package_type"] = normalizedUpdateInfo.GetValueOrDefault("package_type") ?? string.Empty,
            ["release_notes_url"] = normalizedUpdateInfo.GetValueOrDefault("release_notes_url") ?? string.Empty,
            ["release_notes"] = normalizedUpdateInfo.GetValueOrDefault("release_notes") ?? string.Empty,
            ["force_upgrade"] = forceUpgrade,
            ["access_policy"] = NormalizeAccessPolicy(GetDictionary(policy, "access_policy")),
            ["telemetry"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["enabled"] = telemetryEnabled,
                ["upload_interval_sec"] = runtimePolicy.GetValueOrDefault("telemetry_upload_interval_sec") ?? 21600,
            },
        };
    }

    public Dictionary<string, object?> ResolveAccessDecision(Dictionary<string, object?> normalizedConfig, string studentId)
    {
        var accessPolicy = GetDictionary(normalizedConfig, "access_policy");
        var appControl = GetDictionary(normalizedConfig, "app_control");
        var normalizedStudentId = (studentId ?? string.Empty).Trim();
        var studentHash = string.IsNullOrWhiteSpace(normalizedStudentId)
            ? string.Empty
            : Sha256Hex(normalizedStudentId);
        var defaultMessage = Text(accessPolicy.GetValueOrDefault("default_message"));
        if (string.IsNullOrWhiteSpace(defaultMessage))
        {
            defaultMessage = "当前账号已被停止使用，如有疑问请联系开发者。";
        }

        if (string.Equals(Text(appControl.GetValueOrDefault("state")), "blocked", StringComparison.OrdinalIgnoreCase))
        {
            var reason = Text(appControl.GetValueOrDefault("reason"));
            if (IsVersionUpdateRestriction(reason))
            {
                return BuildAccessDecision(false, studentHash, string.Empty, string.Empty, defaultMessage);
            }

            return BuildAccessDecision(true, studentHash, defaultMessage, string.IsNullOrWhiteSpace(reason) ? "app-control-blocked" : reason, defaultMessage);
        }

        foreach (var item in GetList(accessPolicy, "blocked_students"))
        {
            var entry = item as Dictionary<string, object?> ?? [];
            var rawId = item is string text ? text : Text(entry.GetValueOrDefault("student_id"));
            var rawHash = Text(entry.GetValueOrDefault("student_id_hash"));
            if ((!string.IsNullOrWhiteSpace(rawId) && string.Equals(rawId, normalizedStudentId, StringComparison.Ordinal))
                || (!string.IsNullOrWhiteSpace(rawHash) && string.Equals(rawHash, studentHash, StringComparison.OrdinalIgnoreCase)))
            {
                return BuildAccessDecision(true, studentHash, Text(entry.GetValueOrDefault("message")), "access-policy-blocked", defaultMessage);
            }
        }

        foreach (var item in GetList(accessPolicy, "blocked_student_hashes"))
        {
            if (string.Equals(Text(item), studentHash, StringComparison.OrdinalIgnoreCase))
            {
                return BuildAccessDecision(true, studentHash, defaultMessage, "access-policy-blocked", defaultMessage);
            }
        }

        return BuildAccessDecision(false, studentHash, string.Empty, string.Empty, defaultMessage);
    }

    public Dictionary<string, object?> BuildBlockedDetail(string studentId, Dictionary<string, object?> decision)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["blocked"] = decision.GetValueOrDefault("blocked") is true,
            ["message"] = Text(decision.GetValueOrDefault("message")),
            ["student_id"] = studentId ?? string.Empty,
            ["student_id_hash"] = Text(decision.GetValueOrDefault("student_id_hash")),
            ["reason"] = Text(decision.GetValueOrDefault("reason")),
        };
    }

    private static Dictionary<string, object?> NormalizeFeatureFlags(Dictionary<string, object?> raw)
    {
        var defaults = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schedule_view"] = true,
            ["public_course_preview"] = true,
            ["course_grab"] = false,
            ["campus_login"] = true,
            ["keepalive"] = true,
        };
        foreach (var key in defaults.Keys.ToArray())
        {
            if (raw.GetValueOrDefault(key) is bool value)
            {
                defaults[key] = value;
            }
        }
        return defaults;
    }

    private static Dictionary<string, object?> NormalizeFeatureLimits(Dictionary<string, object?> raw)
    {
        var courseGrab = GetDictionary(raw, "course_grab");
        var minInterval = Math.Max(MinimumCourseGrabIntervalSeconds, Double(courseGrab.GetValueOrDefault("min_interval_sec"), MinimumCourseGrabIntervalSeconds));
        var maxRuntime = Math.Clamp(Int(courseGrab.GetValueOrDefault("max_runtime_sec"), 5), 1, 3600);
        var cooldown = Math.Clamp(Int(courseGrab.GetValueOrDefault("cooldown_sec"), 86400), 0, 604800);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["course_grab"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["min_interval_sec"] = minInterval,
                ["max_runtime_sec"] = maxRuntime,
                ["cooldown_sec"] = cooldown,
            },
        };
    }

    private static Dictionary<string, object?> NormalizeRuntimePolicy(Dictionary<string, object?> raw)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["config_pull_interval_sec"] = Math.Clamp(Int(raw.GetValueOrDefault("config_pull_interval_sec"), 86400), 300, 604800),
            ["telemetry_upload_interval_sec"] = Math.Clamp(Int(raw.GetValueOrDefault("telemetry_upload_interval_sec"), 21600), 300, 604800),
            ["summary_report_interval_sec"] = Math.Clamp(Int(raw.GetValueOrDefault("summary_report_interval_sec"), 3600), 300, 604800),
        };
    }

    private static Dictionary<string, object?> NormalizeAppControl(Dictionary<string, object?> raw)
    {
        var state = Text(raw.GetValueOrDefault("state")).ToLowerInvariant();
        if (state is not ("normal" or "readonly" or "blocked"))
        {
            state = "normal";
        }
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["state"] = state,
            ["reason"] = Truncate(Text(raw.GetValueOrDefault("reason")), 64),
        };
    }

    private static Dictionary<string, object?> NormalizeUpdateInfo(Dictionary<string, object?> raw)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["updated_at"] = Truncate(Text(raw.GetValueOrDefault("updated_at")), 64),
            ["latest_version"] = Truncate(Text(raw.GetValueOrDefault("latest_version")), 64),
            ["min_supported_version"] = Truncate(Text(raw.GetValueOrDefault("min_supported_version")), 64),
            ["download_url"] = Truncate(Text(raw.GetValueOrDefault("download_url")), 2000),
            ["package_sha256"] = Truncate(Text(raw.GetValueOrDefault("package_sha256")), 128).ToLowerInvariant(),
            ["package_size"] = Math.Max(0, Long(raw.GetValueOrDefault("package_size"), 0L)),
            ["package_type"] = NormalizePackageType(Text(raw.GetValueOrDefault("package_type"))),
            ["release_notes_url"] = Truncate(Text(raw.GetValueOrDefault("release_notes_url")), 2000),
            ["release_notes"] = raw.GetValueOrDefault("release_notes") is string releaseNotes ? Truncate(releaseNotes, 4000) : string.Empty,
            ["force_upgrade"] = raw.GetValueOrDefault("force_upgrade") is true,
        };
    }

    private static Dictionary<string, object?> NormalizeAccessPolicy(Dictionary<string, object?> raw)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["default_message"] = Truncate(Text(raw.GetValueOrDefault("default_message")), 500),
            ["blocked_students"] = GetList(raw, "blocked_students"),
            ["blocked_student_hashes"] = GetList(raw, "blocked_student_hashes"),
        };
    }

    private static Dictionary<string, object?> SelectVersionPolicy(Dictionary<string, object?> versionPolicy)
    {
        var versions = GetList(versionPolicy, "versions").OfType<Dictionary<string, object?>>().ToArray();
        if (versions.Length > 0)
        {
            return new Dictionary<string, object?>(versions[0], StringComparer.Ordinal);
        }
        return versionPolicy.Count > 0 ? new Dictionary<string, object?>(versionPolicy, StringComparer.Ordinal) : [];
    }

    private static Dictionary<string, object?> BuildAccessDecision(bool blocked, string studentHash, string message, string reason, string defaultMessage)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["blocked"] = blocked,
            ["message"] = string.IsNullOrWhiteSpace(message) ? (blocked ? defaultMessage : string.Empty) : message,
            ["student_id_hash"] = studentHash,
            ["reason"] = reason,
        };
    }

    private static bool IsVersionUpdateRestriction(string reason)
    {
        return string.Equals(reason, "version-disabled", StringComparison.OrdinalIgnoreCase)
               || string.Equals(reason, "version-too-low", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, object?> GetDictionary(Dictionary<string, object?> parent, string key)
    {
        return parent.GetValueOrDefault(key) is Dictionary<string, object?> dictionary ? dictionary : [];
    }

    private static List<object?> GetList(Dictionary<string, object?> parent, string key)
    {
        var value = parent.GetValueOrDefault(key);
        if (value is List<object?> list)
        {
            return list;
        }

        if (value is IEnumerable<object?> enumerable)
        {
            return enumerable.ToList();
        }

        if (value is System.Collections.IEnumerable rawValues)
        {
            return rawValues.Cast<object?>().ToList();
        }

        return [];
    }

    private static string Text(object? value)
    {
        return value?.ToString()?.Trim() ?? string.Empty;
    }

    private static string Truncate(string value, int maxLength)
    {
        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }

    private static int Int(object? value, int fallback)
    {
        return value switch
        {
            int number => number,
            long number => (int)number,
            double number => (int)number,
            _ when int.TryParse(value?.ToString(), out var parsed) => parsed,
            _ => fallback,
        };
    }

    private static long Long(object? value, long fallback)
    {
        return value switch
        {
            int number => number,
            long number => number,
            double number => (long)number,
            _ when long.TryParse(value?.ToString(), out var parsed) => parsed,
            _ => fallback,
        };
    }

    private static double Double(object? value, double fallback)
    {
        return value switch
        {
            double number => number,
            int number => number,
            long number => number,
            _ when double.TryParse(value?.ToString(), out var parsed) => parsed,
            _ => fallback,
        };
    }

    private static string Sha256Hex(string text)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    private static string NormalizePackageType(string value)
    {
        var packageType = value.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(packageType) ? "zip" : Truncate(packageType, 32);
    }
}
