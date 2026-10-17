using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xke.Contracts.Requests;

namespace Xke.Core.Schedule;

public sealed class PersonalScheduleApiClient : IScheduleSnapshotSource
{
    private static readonly string[] Weekdays = ["星期一", "星期二", "星期三", "星期四", "星期五", "星期六", "星期日"];
    private readonly HttpClient _http;
    private readonly ScheduleApiOptions _options;
    private readonly TimeProvider _clock;

    public PersonalScheduleApiClient(HttpClient http, ScheduleApiOptions options, TimeProvider clock)
    {
        _http = http;
        _options = options;
        _clock = clock;
    }

    public async Task<ScheduleSnapshotAttempt> FetchAsync(string studentId, ScheduleRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_options.InvalidConfiguration) return ScheduleSnapshotAttempt.Unavailable("snapshot_configuration_invalid");
        if (string.IsNullOrWhiteSpace(_options.Endpoint)) return ScheduleSnapshotAttempt.Unavailable(string.Empty);
        if (!string.IsNullOrWhiteSpace(_options.StudentId)
            && !string.Equals(_options.StudentId, studentId, StringComparison.Ordinal))
            return ScheduleSnapshotAttempt.Unavailable("snapshot_account_mismatch");
        if (!Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme != "https" || !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query))
            return ScheduleSnapshotAttempt.Unavailable("snapshot_configuration_invalid");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds is >= 1 and <= 60 ? _options.TimeoutSeconds : 8));
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, endpoint);
            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
                message.Headers.Add("X-API-Key", _options.ApiKey);
            else if (!string.IsNullOrWhiteSpace(_options.BearerToken))
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.BearerToken);
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await _http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return ScheduleSnapshotAttempt.Unavailable("snapshot_http_failure");
            using var content = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            var snapshot = await JsonSerializer.DeserializeAsync<ScheduleSnapshotDocument>(content, cancellationToken: timeout.Token).ConfigureAwait(false);
            if (snapshot is null) return ScheduleSnapshotAttempt.Unavailable("snapshot_invalid");
            return ConvertSnapshot(snapshot, studentId, request);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return ScheduleSnapshotAttempt.Unavailable("snapshot_timeout"); }
        catch (HttpRequestException) { return ScheduleSnapshotAttempt.Unavailable("snapshot_unavailable"); }
        catch (JsonException) { return ScheduleSnapshotAttempt.Unavailable("snapshot_invalid"); }
        catch (IOException) { return ScheduleSnapshotAttempt.Unavailable("snapshot_unavailable"); }
        catch (FormatException) { return ScheduleSnapshotAttempt.Unavailable("snapshot_configuration_invalid"); }
    }

    private ScheduleSnapshotAttempt ConvertSnapshot(ScheduleSnapshotDocument snapshot, string studentId, ScheduleRequest request)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.StudentId) && snapshot.StudentId != studentId)
            return ScheduleSnapshotAttempt.Unavailable("snapshot_account_mismatch");
        if (snapshot.Week <= 0 || snapshot.Courses is null || snapshot.Dates is null
            || !DateTimeOffset.TryParse(snapshot.FetchedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fetchedAt)
            || !(snapshot.FetchedAt.EndsWith('Z') || System.Text.RegularExpressions.Regex.IsMatch(snapshot.FetchedAt, @"[+-]\d{2}:\d{2}$")))
            return ScheduleSnapshotAttempt.Unavailable("snapshot_invalid");
        var age = _clock.GetUtcNow() - fetchedAt;
        if (age < TimeSpan.FromMinutes(-5) || age > TimeSpan.FromHours(_options.MaxAgeHours))
            return ScheduleSnapshotAttempt.Unavailable("snapshot_stale");

        var dates = new Dictionary<string, string>(StringComparer.Ordinal);
        DateOnly? monday = null;
        for (var index = 0; index < Weekdays.Length; index++)
        {
            if (!snapshot.Dates.TryGetValue(Weekdays[index], out var rawDate)
                || !DateOnly.TryParseExact(rawDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                || (index == 0 && date.DayOfWeek != DayOfWeek.Monday)
                || (index > 0 && date != monday!.Value.AddDays(index)))
                return ScheduleSnapshotAttempt.Unavailable("snapshot_invalid");
            monday ??= date;
            dates[Weekdays[index]] = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        var today = DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);
        var targetDate = request.Mode == "next" ? today.AddDays(7) : today;
        if (request.Mode == "manual")
        {
            var requestedWeek = ReadWeekNumber(request.ManualWeek);
            if (requestedWeek != snapshot.Week) return ScheduleSnapshotAttempt.Unavailable("snapshot_week_missing");
        }
        else if (targetDate < monday || targetDate > monday!.Value.AddDays(6))
            return ScheduleSnapshotAttempt.Unavailable("snapshot_week_missing");
        if (!string.IsNullOrWhiteSpace(request.CookieRq)
            && (!DateOnly.TryParseExact(request.CookieRq, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var requestedDate)
                || requestedDate < monday || requestedDate > monday!.Value.AddDays(6)))
            return ScheduleSnapshotAttempt.Unavailable("snapshot_week_missing");
        var academicTerm = string.IsNullOrWhiteSpace(snapshot.AcademicTerm) ? _options.AcademicTerm : snapshot.AcademicTerm;
        if (!string.IsNullOrWhiteSpace(request.AcademicTerm) && academicTerm != request.AcademicTerm)
            return ScheduleSnapshotAttempt.Unavailable("snapshot_term_mismatch");
        if (!string.IsNullOrWhiteSpace(request.TermLabel) && snapshot.TermLabel != request.TermLabel)
            return ScheduleSnapshotAttempt.Unavailable("snapshot_term_mismatch");

        var weekLabel = $"第{snapshot.Week}周";
        var rows = new List<Dictionary<string, object?>>();
        foreach (var course in snapshot.Courses)
        {
            if (course is null || string.IsNullOrWhiteSpace(course.CourseName) || !dates.ContainsKey(course.Weekday ?? string.Empty)
                || course.Sections is null || course.Sections.Count == 0 || course.Sections.Any(section => section < 1)
                || (!string.IsNullOrWhiteSpace(course.Date) && course.Date != dates[course.Weekday!]))
                return ScheduleSnapshotAttempt.Unavailable("snapshot_invalid");
            rows.Add(new Dictionary<string, object?>
            {
                ["source"] = "教务系统课表", ["week_label"] = weekLabel,
                ["weekday"] = course.Weekday, ["section"] = ScheduleSectionNormalizer.FormatSectionRanges(course.Sections.Distinct().Order().ToArray()),
                ["course_name"] = course.CourseName, ["teacher"] = course.Teacher ?? string.Empty,
                ["room"] = course.Room ?? string.Empty, ["weeks"] = course.Weeks ?? string.Empty,
                ["credit"] = course.Credit ?? string.Empty, ["time_range"] = course.BlockTime ?? string.Empty,
                ["block_time"] = course.BlockTime ?? string.Empty,
            });
        }
        var todayMonday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var currentWeek = snapshot.Week - (monday!.Value.DayNumber - todayMonday.DayNumber) / 7;
        var term = new Dictionary<string, object?> { ["label"] = snapshot.TermLabel ?? string.Empty, ["academic_term"] = academicTerm };
        var raw = new Dictionary<string, object?> { ["term"] = term, ["updated_at"] = fetchedAt.ToString("O"), ["retrieval_channel"] = "personal_api" };
        var result = new JwScheduleFetchResult(
            SourceKey: "jw_schedule", SourceLabel: "教务系统课表", SourceType: "jw", WeekLabel: weekLabel,
            CurrentWeekNumber: currentWeek > 0 ? currentWeek : snapshot.Week, ViewedWeekNumber: snapshot.Week,
            AvailableWeeks: [new() { ["value"] = dates["星期一"], ["label"] = weekLabel, ["selected"] = true }],
            WeekDates: dates, BlockTimes: [], DataWarnings: [], Rows: rows, RawPayload: raw,
            Remarks: "课表已更新", FallbackReason: string.Empty, TermLabel: snapshot.TermLabel ?? string.Empty);
        return new(result, string.Empty);
    }

    private static int ReadWeekNumber(string value)
    {
        var match = System.Text.RegularExpressions.Regex.Match(value ?? string.Empty, @"\d+");
        return match.Success && int.TryParse(match.Value, out var number) ? number : 0;
    }
}

internal sealed class ScheduleSnapshotDocument
{
    [JsonPropertyName("student_id")] public string? StudentId { get; init; }
    [JsonPropertyName("fetched_at")] public string FetchedAt { get; init; } = string.Empty;
    [JsonPropertyName("academic_term")] public string AcademicTerm { get; init; } = string.Empty;
    [JsonPropertyName("term_label")] public string? TermLabel { get; init; }
    [JsonPropertyName("week")] public int Week { get; init; }
    [JsonPropertyName("dates")] public Dictionary<string, string>? Dates { get; init; }
    [JsonPropertyName("courses")] public List<ScheduleSnapshotCourse?>? Courses { get; init; }
}

internal sealed class ScheduleSnapshotCourse
{
    [JsonPropertyName("course_name")] public string? CourseName { get; init; }
    [JsonPropertyName("teacher")] public string? Teacher { get; init; }
    [JsonPropertyName("room")] public string? Room { get; init; }
    [JsonPropertyName("weeks")] public string? Weeks { get; init; }
    [JsonPropertyName("credit")] public string? Credit { get; init; }
    [JsonPropertyName("sections")] public List<int>? Sections { get; init; }
    [JsonPropertyName("weekday")] public string? Weekday { get; init; }
    [JsonPropertyName("date")] public string? Date { get; init; }
    [JsonPropertyName("block_time")] public string? BlockTime { get; init; }
}
