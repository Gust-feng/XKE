using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Xke.Contracts.Requests;
using Xke.Contracts.Responses;
using Xke.Storage;

namespace Xke.Core.Schedule;

/// <summary>Reads an already connected ProtocolHost; never sends school credentials or starts login.</summary>
public sealed class ProtocolScheduleApiClient(HttpClient http, ScheduleApiOptions options, TimeProvider clock) : IScheduleSnapshotSource
{
    private static readonly string[] Weekdays = ["星期一", "星期二", "星期三", "星期四", "星期五", "星期六", "星期日"];

    public Task<ScheduleSnapshotAttempt> FetchAsync(string studentId, ScheduleRequest request, CancellationToken cancellationToken)
        => ExecuteAsync(studentId, async (endpoint, token) =>
        {
            var term = string.IsNullOrWhiteSpace(request.AcademicTerm) ? options.AcademicTerm.Trim() : request.AcademicTerm.Trim();
            var week = request.Mode == "manual" ? ScheduleService.ParseWeekNumber(request.ManualWeek) : null;
            if (request.Mode is not ("current" or "manual" or "next") || (request.Mode == "manual" && week is not > 0))
                return ScheduleSnapshotAttempt.Unavailable("protocol_request_invalid");

            var document = await ReadAsync(endpoint, "week", term, week, token).ConfigureAwait(false);
            if (document.Reason.Length > 0) return ScheduleSnapshotAttempt.Unavailable(document.Reason);
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var attempt = ConvertWeek(document.Data, studentId, term, request.TermLabel, week,
                request.Mode == "manual" ? null : today, request.Mode == "next" ? null : request.CookieRq);
            if (request.Mode != "next" || attempt.Schedule is null) return attempt;

            // ProtocolHost accepts an integer week, so resolve next against its current week and term.
            var current = attempt.Schedule;
            var nextWeek = current.ViewedWeekNumber!.Value + 1;
            var currentTerm = (Dictionary<string, object?>)current.RawPayload["term"]!;
            term = currentTerm["academic_term"]!.ToString()!;
            document = await ReadAsync(endpoint, "week", term, nextWeek, token).ConfigureAwait(false);
            if (document.Reason.Length > 0) return ScheduleSnapshotAttempt.Unavailable(document.Reason);
            var next = ConvertWeek(document.Data, studentId, term, request.TermLabel, nextWeek,
                today.AddDays(7), request.CookieRq);
            return next.Schedule is { } schedule
                ? next with { Schedule = schedule with { CurrentWeekNumber = current.CurrentWeekNumber } }
                : next;
        }, cancellationToken);

    public Task<ScheduleSnapshotAttempt> FetchSemesterAsync(string studentId, string academicTerm, CancellationToken cancellationToken)
        => ExecuteAsync(studentId, async (endpoint, token) =>
        {
            var term = string.IsNullOrWhiteSpace(academicTerm) ? options.AcademicTerm.Trim() : academicTerm.Trim();
            var document = await ReadAsync(endpoint, "semester", term, null, token).ConfigureAwait(false);
            if (document.Reason.Length > 0) return ScheduleSnapshotAttempt.Unavailable(document.Reason);
            var data = document.Data.Deserialize<SemesterScheduleData>();
            if (data is null || data.Rows is null || data.BlockTimes is null)
                return ScheduleSnapshotAttempt.Unavailable("protocol_invalid");
            var reason = ValidateMetadata(data.Student, data.Term, data.UpdatedAt, studentId, term);
            if (reason.Length > 0) return ScheduleSnapshotAttempt.Unavailable(reason);
            return new(new JwScheduleFetchResult("jw_schedule", "教务系统课表", "jw", "全部周", null, null,
                [], new Dictionary<string, string>(), ToDictionaries(data.BlockTimes), [], ToDictionaries(data.Rows),
                ReadMetadata(document.Data, data.UpdatedAt), "课表已更新", string.Empty, TermLabel: data.Term.Label), string.Empty);
        }, cancellationToken);

    private async Task<ScheduleSnapshotAttempt> ExecuteAsync(string studentId,
        Func<Uri, CancellationToken, Task<ScheduleSnapshotAttempt>> fetch, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (options.InvalidConfiguration) return ScheduleSnapshotAttempt.Unavailable("protocol_configuration_invalid");
        if (string.IsNullOrWhiteSpace(options.Endpoint)) return ScheduleSnapshotAttempt.Unavailable(string.Empty);
        if (!string.IsNullOrWhiteSpace(options.StudentId) && options.StudentId != studentId)
            return ScheduleSnapshotAttempt.Unavailable("protocol_account_mismatch");
        if (string.IsNullOrWhiteSpace(options.ApiKey)
            || !Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme != "https" || endpoint.UserInfo.Length > 0 || endpoint.Query.Length > 0 || endpoint.Fragment.Length > 0)
            return ScheduleSnapshotAttempt.Unavailable("protocol_configuration_invalid");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds is >= 1 and <= 60 ? options.TimeoutSeconds : 8));
        try
        {
            return await fetch(endpoint, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return ScheduleSnapshotAttempt.Unavailable("protocol_timeout"); }
        catch (HttpRequestException) { return ScheduleSnapshotAttempt.Unavailable("protocol_unavailable"); }
        catch (IOException) { return ScheduleSnapshotAttempt.Unavailable("protocol_unavailable"); }
        catch (JsonException) { return ScheduleSnapshotAttempt.Unavailable("protocol_invalid"); }
        catch (FormatException) { return ScheduleSnapshotAttempt.Unavailable("protocol_configuration_invalid"); }
    }

    private async Task<(JsonElement Data, string Reason)> ReadAsync(Uri endpoint, string scope, string term, int? week, CancellationToken token)
    {
        var baseUri = new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/");
        var uri = new UriBuilder(new Uri(baseUri, "api/schedule/" + scope));
        var query = new List<string>();
        if (week.HasValue) query.Add("week=" + week.Value.ToString(CultureInfo.InvariantCulture));
        if (term.Length > 0) query.Add("academic_term=" + Uri.EscapeDataString(term));
        uri.Query = string.Join('&', query);
        using var message = new HttpRequestMessage(HttpMethod.Get, uri.Uri);
        message.Headers.Add("X-XKE-Api-Key", options.ApiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return (default, "protocol_http_failure");
        using var content = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        var envelope = await JsonSerializer.DeserializeAsync<ProtocolEnvelope>(content, cancellationToken: token).ConfigureAwait(false);
        return envelope is { Ok: true, Data.ValueKind: JsonValueKind.Object }
            ? (envelope.Data, string.Empty) : (default, "protocol_invalid");
    }

    private ScheduleSnapshotAttempt ConvertWeek(JsonElement document, string studentId, string term, string termLabel,
        int? requestedWeek, DateOnly? targetDate, string? cookieRq)
    {
        var data = document.Deserialize<ScheduleData>();
        if (data is null || data.Rows is null || data.WeekDates is null || data.AvailableWeeks is null
            || data.BlockTimes is null || data.DataWarnings is null)
            return ScheduleSnapshotAttempt.Unavailable("protocol_invalid");
        var reason = ValidateMetadata(data.Student, data.Term, data.UpdatedAt, studentId, term);
        if (reason.Length > 0) return ScheduleSnapshotAttempt.Unavailable(reason);
        if (!string.IsNullOrWhiteSpace(termLabel) && data.Term.Label != termLabel)
            return ScheduleSnapshotAttempt.Unavailable("protocol_term_mismatch");
        var viewed = data.ViewedWeekNumber;
        if (viewed is not > 0 || ScheduleService.ParseWeekNumber(data.WeekLabel) != viewed)
            return ScheduleSnapshotAttempt.Unavailable("protocol_week_missing");
        if ((requestedWeek.HasValue && requestedWeek != viewed)
            || (!requestedWeek.HasValue && data.CurrentWeekNumber.HasValue && data.CurrentWeekNumber != viewed)
            || data.Rows.Any(row => row is null || (!string.IsNullOrWhiteSpace(row.WeekLabel)
                && ScheduleService.ParseWeekNumber(row.WeekLabel) != viewed)))
            return ScheduleSnapshotAttempt.Unavailable("protocol_week_missing");

        // Historical term/week responses can omit dates. Never invent a calendar for them.
        DateOnly? monday = null;
        if (data.WeekDates.Count > 0)
        {
            for (var index = 0; index < Weekdays.Length; index++)
            {
                if (!data.WeekDates.TryGetValue(Weekdays[index], out var rawDate)
                    || !DateOnly.TryParseExact(rawDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                    || (index == 0 && date.DayOfWeek != DayOfWeek.Monday)
                    || (index > 0 && date != monday!.Value.AddDays(index)))
                    return ScheduleSnapshotAttempt.Unavailable("protocol_invalid");
                monday ??= date;
            }
        }
        if (targetDate.HasValue && ((monday.HasValue && (targetDate < monday || targetDate > monday.Value.AddDays(6)))
            || (!monday.HasValue && !requestedWeek.HasValue && data.CurrentWeekNumber is not > 0)))
            return ScheduleSnapshotAttempt.Unavailable("protocol_week_missing");
        if (!string.IsNullOrWhiteSpace(cookieRq)
            && (!monday.HasValue || !DateOnly.TryParseExact(cookieRq, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var requestedDate)
                || requestedDate < monday || requestedDate > monday.Value.AddDays(6)))
            return ScheduleSnapshotAttempt.Unavailable("protocol_week_missing");

        return new(new JwScheduleFetchResult("jw_schedule", "教务系统课表", "jw", $"第{viewed}周",
            data.CurrentWeekNumber, viewed, ToDictionaries(data.AvailableWeeks), data.WeekDates,
            ToDictionaries(data.BlockTimes), ToDictionaries(data.DataWarnings), ToDictionaries(data.Rows),
            ReadMetadata(document, data.UpdatedAt), data.Remarks, string.Empty, TermLabel: data.Term.Label), string.Empty);
    }

    private string ValidateMetadata(ScheduleStudentSummary? student, ScheduleTermSummary? term, string? updatedAt,
        string studentId, string academicTerm)
    {
        if (student is null || string.IsNullOrWhiteSpace(student.StudentId) || term is null || string.IsNullOrWhiteSpace(term.AcademicTerm))
            return "protocol_invalid";
        if (student.StudentId != studentId) return "protocol_account_mismatch";
        if (academicTerm.Length > 0 && term.AcademicTerm != academicTerm) return "protocol_term_mismatch";
        if (string.IsNullOrWhiteSpace(updatedAt)
            || !DateTimeOffset.TryParse(updatedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp)
            || !(updatedAt.EndsWith('Z') || Regex.IsMatch(updatedAt, @"[+-]\d{2}:\d{2}$")))
            return "protocol_invalid";
        var age = clock.GetUtcNow() - timestamp;
        return age < TimeSpan.FromMinutes(-5) || age > TimeSpan.FromHours(options.MaxAgeHours) ? "protocol_stale" : string.Empty;
    }

    private static Dictionary<string, object?> ReadMetadata(JsonElement document, string updatedAt)
    {
        var raw = new Dictionary<string, object?> { ["updated_at"] = updatedAt, ["retrieval_channel"] = "protocol_api" };
        foreach (var name in new[] { "term", "available_terms", "notes", "supplementary_courses" })
            if (document.TryGetProperty(name, out var value)) raw[name] = JsonValueConverter.ToObject(value);
        return raw;
    }

    private static Dictionary<string, object?>[] ToDictionaries<T>(IReadOnlyList<T> values)
        => values.Select(value => JsonValueConverter.ToDictionary(JsonSerializer.SerializeToElement(value))).ToArray();

    private sealed record ProtocolEnvelope(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("data")] JsonElement Data);
}
