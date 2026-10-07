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
    [property: JsonPropertyName("updated_at")] string UpdatedAt = ""
);

public sealed record AuthCourseGrabState(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("running")] bool Running,
    [property: JsonPropertyName("cooldown_active")] bool CooldownActive = false,
    [property: JsonPropertyName("cooldown_until")] string CooldownUntil = "",
    [property: JsonPropertyName("cooldown_remaining_sec")] int CooldownRemainingSec = 0,
    [property: JsonPropertyName("runtime_deadline_at")] string RuntimeDeadlineAt = "",
    [property: JsonPropertyName("runtime_remaining_sec")] int RuntimeRemainingSec = 0,
    [property: JsonPropertyName("last_started_at")] string LastStartedAt = "",
    [property: JsonPropertyName("last_stopped_at")] string LastStoppedAt = "",
    [property: JsonPropertyName("last_reason")] string LastReason = "",
    [property: JsonPropertyName("zbid")] string Zbid = "",
    [property: JsonPropertyName("kcid")] string Kcid = "",
    [property: JsonPropertyName("jx0404id")] string Jx0404Id = "",
    [property: JsonPropertyName("interval_sec")] double IntervalSec = 0
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
    [property: JsonPropertyName("trusted_login_token")] string TrustedLoginToken,
    [property: JsonPropertyName("course_grab_state")] AuthCourseGrabState CourseGrabState
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
