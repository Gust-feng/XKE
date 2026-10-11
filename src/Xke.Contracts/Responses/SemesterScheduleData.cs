using System.Text.Json.Serialization;

namespace Xke.Contracts.Responses;

public sealed record SemesterScheduleData(
    [property: JsonPropertyName("term")] ScheduleTermSummary Term,
    [property: JsonPropertyName("student")] ScheduleStudentSummary Student,
    [property: JsonPropertyName("rows")] IReadOnlyList<ScheduleRow> Rows,
    [property: JsonPropertyName("block_times")] IReadOnlyList<ScheduleBlockTime> BlockTimes,
    [property: JsonPropertyName("available_terms")] IReadOnlyList<ScheduleTermOption> AvailableTerms,
    [property: JsonPropertyName("notes")] IReadOnlyList<string> Notes,
    [property: JsonPropertyName("supplementary_courses")] IReadOnlyList<Dictionary<string, string>> SupplementaryCourses,
    [property: JsonPropertyName("updated_at")] string UpdatedAt,
    [property: JsonPropertyName("saved_files")] IReadOnlyList<string> SavedFiles,
    [property: JsonPropertyName("cached")] bool Cached = false);

public sealed record ScheduleTermOption(
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("selected")] bool Selected);
