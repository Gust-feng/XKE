namespace Xke.Storage;

public sealed class LegacyStudentRoot
{
    // Describes the old Python student tree for compatibility tests and fixture
    // readers. Active .NET storage must stay under XkeDataRoot students/{id}.
    public string DataRootPath { get; }
    public string StudentId { get; }
    public string DirectoryPath => Path.Combine(DataRootPath, StudentId);
    public string InfoDirectory => Path.Combine(DirectoryPath, "info");
    public string CacheDirectory => Path.Combine(DirectoryPath, "cache");
    public string SchedulesDirectory => Path.Combine(DirectoryPath, "schedules");
    public string NetworkInfoDirectory => Path.Combine(InfoDirectory, "network");

    public string CredentialsPath => Path.Combine(DirectoryPath, ".env");
    public string PreferencesPath => Path.Combine(DirectoryPath, "settings.json");
    public string LegacyPreferencesPath => Path.Combine(DirectoryPath, "preferences.json");
    public string BasicInfoPath => Path.Combine(InfoDirectory, "basic_info.json");
    public string LegacyBasicInfoPath => Path.Combine(DirectoryPath, "basic_info.json");
    public string PreloginCapturePath => Path.Combine(DirectoryPath, "prelogin_capture.xml");
    public string AvatarBasePath => Path.Combine(DirectoryPath, "avatar");
    public string AppRuntimePath => Path.Combine(InfoDirectory, "app_runtime.json");
    public string NetworkAuthHistoryPath => Path.Combine(InfoDirectory, "network_auth_history.json");
    public string RemoteSensingSnapshotPath => Path.Combine(InfoDirectory, "remote_sensing_snapshot.json");
    public string CourseGrabGuardPath => Path.Combine(InfoDirectory, "course_grab_guard.json");

    public string TokenFilePath => Path.Combine(CacheDirectory, "token_cache.json");
    public string BundleFilePath => Path.Combine(CacheDirectory, "sso_cookie_bundle.json");
    public string CookieHeaderPath => Path.Combine(CacheDirectory, "jw_cookie_header.txt");
    public string PreviewFilePath => Path.Combine(CacheDirectory, "preview_courses.json");
    public string CookieScheduleHtmlPath => Path.Combine(CacheDirectory, "jw_schedule.html");
    public string CookieScheduleParamsPath => Path.Combine(CacheDirectory, "jw_schedule_params.json");
    public string CookieScheduleParsedPath => Path.Combine(CacheDirectory, "jw_schedule_parsed.json");

    public LegacyStudentRoot(string dataRootPath, string studentId)
    {
        DataRootPath = dataRootPath;
        StudentId = studentId;
    }

    public string NetworkProviderInfoPath(string provider)
    {
        var sanitized = string.IsNullOrWhiteSpace(provider) ? "campus" : provider.Trim().ToLowerInvariant();
        return Path.Combine(NetworkInfoDirectory, $"{sanitized}.json");
    }

    public string ScheduleTermDirectory(string termLabel)
    {
        return Path.Combine(SchedulesDirectory, string.IsNullOrWhiteSpace(termLabel) ? "当前学期" : termLabel.Trim());
    }

    public string ScheduleWeekDirectory(string termLabel, string weekLabel)
    {
        return Path.Combine(ScheduleTermDirectory(termLabel), "weeks", string.IsNullOrWhiteSpace(weekLabel) ? "当前周" : weekLabel.Trim());
    }

    public string ScheduleSourcePath(string termLabel, string weekLabel, string sourceKey)
    {
        var safeSourceKey = string.IsNullOrWhiteSpace(sourceKey) ? "schedule" : sourceKey.Trim();
        return Path.Combine(ScheduleWeekDirectory(termLabel, weekLabel), $"{safeSourceKey}.json");
    }

    public string ScheduleSourceRawPath(string termLabel, string weekLabel, string sourceKey)
    {
        var safeSourceKey = string.IsNullOrWhiteSpace(sourceKey) ? "schedule" : sourceKey.Trim();
        return Path.Combine(ScheduleWeekDirectory(termLabel, weekLabel), $"{safeSourceKey}_raw.json");
    }
}
