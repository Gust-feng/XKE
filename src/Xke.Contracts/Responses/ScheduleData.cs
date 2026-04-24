using System.Text.Json.Serialization;

namespace Xke.Contracts.Responses;

public sealed record ScheduleData(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("source_label")] string SourceLabel,
    [property: JsonPropertyName("source_type")] string SourceType,
    [property: JsonPropertyName("rows")] IReadOnlyList<ScheduleRow> Rows,
    [property: JsonPropertyName("week_label")] string WeekLabel,
    [property: JsonPropertyName("available_weeks")] IReadOnlyList<ScheduleWeekOption> AvailableWeeks,
    [property: JsonPropertyName("week_dates")] IReadOnlyDictionary<string, string> WeekDates,
    [property: JsonPropertyName("current_week_label")] string CurrentWeekLabel,
    [property: JsonPropertyName("current_week_number")] int? CurrentWeekNumber,
    [property: JsonPropertyName("viewed_week_number")] int? ViewedWeekNumber,
    [property: JsonPropertyName("remarks")] string Remarks,
    [property: JsonPropertyName("block_times")] IReadOnlyList<ScheduleBlockTime> BlockTimes,
    [property: JsonPropertyName("saved_files")] IReadOnlyList<string> SavedFiles,
    [property: JsonPropertyName("fallback_reason")] string FallbackReason,
    [property: JsonPropertyName("data_warnings")] IReadOnlyList<ScheduleWarning> DataWarnings,
    [property: JsonPropertyName("term")] ScheduleTermSummary Term,
    [property: JsonPropertyName("student")] ScheduleStudentSummary Student,
    [property: JsonPropertyName("cached")] bool Cached,
    [property: JsonPropertyName("data_origin")] string DataOrigin,
    [property: JsonPropertyName("updated_at")] string UpdatedAt,
    [property: JsonPropertyName("file_updated_at")] string FileUpdatedAt
);

public sealed record ScheduleWeekOption(
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("selected")] bool Selected
);

public sealed record ScheduleBlockTime(
    [property: JsonPropertyName("block_label")] string BlockLabel,
    [property: JsonPropertyName("section")] string Section,
    [property: JsonPropertyName("block_time")] string BlockTime,
    [property: JsonPropertyName("small_section_1")] string SmallSection1,
    [property: JsonPropertyName("small_section_2")] string SmallSection2,
    [property: JsonPropertyName("break_minutes")] int BreakMinutes
);

public sealed record ScheduleConflict(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("kept_room")] string KeptRoom,
    [property: JsonPropertyName("kept_weeks")] string KeptWeeks,
    [property: JsonPropertyName("discarded_rooms")] IReadOnlyList<string> DiscardedRooms
);

public sealed record ScheduleWarningDiscardedItem(
    [property: JsonPropertyName("weeks")] string Weeks,
    [property: JsonPropertyName("room")] string Room,
    [property: JsonPropertyName("course")] string Course
);

public sealed record ScheduleWarning(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("course_name")] string CourseName,
    [property: JsonPropertyName("week_label")] string WeekLabel,
    [property: JsonPropertyName("weekday")] string Weekday,
    [property: JsonPropertyName("kept_room")] string KeptRoom,
    [property: JsonPropertyName("kept_weeks")] string KeptWeeks,
    [property: JsonPropertyName("discarded")] IReadOnlyList<ScheduleWarningDiscardedItem> Discarded,
    [property: JsonPropertyName("message")] string Message
);

public sealed record ScheduleTermSummary(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("academic_term")] string AcademicTerm
);

public sealed record ScheduleStudentSummary(
    [property: JsonPropertyName("student_id")] string StudentId,
    [property: JsonPropertyName("name")] string Name
);
