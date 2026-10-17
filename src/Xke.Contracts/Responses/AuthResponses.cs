using System.Text.Json.Serialization;
using Xke.Contracts.Models;

namespace Xke.Contracts.Responses;

public sealed record AuthStudentSummary(
    [property: JsonPropertyName("student_id")] string StudentId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("nickname")] string Nickname,
    [property: JsonPropertyName("use_nickname")] bool UseNickname,
    [property: JsonPropertyName("class_name")] string ClassName,
    [property: JsonPropertyName("major_name")] string MajorName,
    [property: JsonPropertyName("academy_name")] string AcademyName,
    [property: JsonPropertyName("training_level")] string TrainingLevel,
    [property: JsonPropertyName("campus_name")] string CampusName,
    [property: JsonPropertyName("avatar_url")] string AvatarUrl,
    [property: JsonPropertyName("updated_at")] string UpdatedAt = "",
    [property: JsonPropertyName("program_duration_years")] int? ProgramDurationYears = null,
    [property: JsonPropertyName("enrollment_date")] string EnrollmentDate = ""
);

public sealed record AuthPayload(
    [property: JsonPropertyName("session_id")] string SessionId,
    [property: JsonPropertyName("student")] AuthStudentSummary Student,
    [property: JsonPropertyName("preferences")] LegacyPreferencesDocument Preferences,
    [property: JsonPropertyName("settings")] AppSettingsDocument Settings,
    [property: JsonPropertyName("storage_dir")] string StorageDir,
    [property: JsonPropertyName("connected")] bool Connected,
    [property: JsonPropertyName("prelogin_mode")] string PreloginMode,
    [property: JsonPropertyName("cached_schedule")] object? CachedSchedule,
    [property: JsonPropertyName("trusted_login_token")] string TrustedLoginToken
);

public sealed record AuthPreferencesPayload(
    [property: JsonPropertyName("preferences")] LegacyPreferencesDocument Preferences,
    [property: JsonPropertyName("settings")] AppSettingsDocument Settings,
    [property: JsonPropertyName("storage_dir")] string StorageDir
);

public sealed record AuthProfilePayload(
    [property: JsonPropertyName("student")] AuthStudentSummary Student
);

public sealed record AuthAvatarUploadPayload(
    [property: JsonPropertyName("avatar_url")] string AvatarUrl,
    [property: JsonPropertyName("saved_file")] string SavedFile
);
