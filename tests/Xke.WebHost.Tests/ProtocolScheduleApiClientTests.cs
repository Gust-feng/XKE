using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xke.Contracts.Api;
using Xke.Contracts.Requests;
using Xke.Contracts.Responses;
using Xke.Core.Schedule;
using Xke.Security;
using Xke.Storage;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class ProtocolScheduleApiClientTests : IDisposable
{
    private const string StudentId = "20260001";
    private const string Term = "2026-2027-1";
    private const string UpdatedAt = "2026-10-07T08:00:00+08:00";
    private readonly TemporaryDirectory _temp = new();
    private readonly List<HttpClient> _clients = [];

    [Fact]
    public async Task CurrentWeek_UsesSharedContractAndOnlyProtocolApiKey()
    {
        const string term = "测试学期 & 1";
        var data = WeekData();
        data["term"] = new ScheduleTermSummary("测试学期", term);
        var handler = new ProtocolHandler { Bodies = [data] };
        var result = await Create(handler, LoadOptions(academicTerm: "configured-term", endpoint: "https://example.test/protocol"))
            .FetchAsync(StudentId, new ScheduleRequest { AcademicTerm = term }, default);

        var schedule = Assert.IsType<JwScheduleFetchResult>(result.Schedule);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/protocol/api/schedule/week", request.Uri.AbsolutePath);
        var query = QueryHelpers.ParseQuery(request.Uri.Query);
        Assert.Equal(term, query["academic_term"]);
        Assert.False(query.ContainsKey("week"));
        Assert.Equal("synthetic-protocol-key", request.ApiKey);
        Assert.Null(request.Authorization);
        Assert.False(request.HasContent);
        Assert.DoesNotContain("X-API-Key", request.Headers);
        Assert.DoesNotContain("X-XKE-Session", request.Headers);
        Assert.Equal("jw_schedule", schedule.SourceKey);
        Assert.Equal("jw", schedule.SourceType);
        Assert.Equal("第8周", schedule.WeekLabel);
        var row = Assert.Single(schedule.Rows);
        Assert.Equal("测试课程", row["course_name"]);
        Assert.Equal(new long[] { 1, 2 }, Assert.IsType<List<object?>>(row["section_numbers"]).Cast<long>());
        Assert.Equal(new[] { "08:00-09:35" }, Assert.IsType<List<object?>>(row["time_segments"]).Cast<string>());
        Assert.Equal("2学时", row["hours_text"]);
        Assert.Equal("protocol_api", schedule.RawPayload["retrieval_channel"]);
        Assert.Equal(UpdatedAt, schedule.RawPayload["updated_at"]);
        Assert.Equal("备注原文", Assert.Single(Assert.IsType<List<object?>>(schedule.RawPayload["notes"])));
        Assert.Single(Assert.IsType<List<object?>>(schedule.RawPayload["supplementary_courses"]));
        Assert.Empty(result.FallbackReason);
        Assert.DoesNotContain("synthetic-protocol-key", JsonSerializer.Serialize(result));
        Assert.DoesNotContain("example.test", JsonSerializer.Serialize(result));
    }

    [Theory]
    [InlineData("https://example.test", "/api/schedule/week")]
    [InlineData("https://example.test/protocol/", "/protocol/api/schedule/week")]
    public async Task UnspecifiedTerm_QueriesServerCurrentWeek(string endpoint, string path)
    {
        var handler = new ProtocolHandler();
        var result = await Create(handler, LoadOptions(endpoint: endpoint)).FetchAsync(StudentId, new ScheduleRequest(), default);
        Assert.NotNull(result.Schedule);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(path, request.Uri.AbsolutePath);
        Assert.Empty(request.Uri.Query);
    }

    [Fact]
    public async Task ManualHistoricalWeek_SendsIntegerWeekAndConfiguredTermWithoutInventingDates()
    {
        var body = WeekData(14);
        body["week_dates"] = new Dictionary<string, string>();
        body["current_week_number"] = null;
        var handler = new ProtocolHandler { Bodies = [body] };
        var result = await Create(handler, LoadOptions(academicTerm: Term)).FetchAsync(StudentId,
            new ScheduleRequest { Mode = "manual", ManualWeek = "第十四周" }, default);

        var query = QueryHelpers.ParseQuery(Assert.Single(handler.Requests).Uri.Query);
        Assert.Equal("14", query["week"]);
        Assert.Equal(Term, query["academic_term"]);
        Assert.NotNull(result.Schedule);
        Assert.Empty(result.Schedule!.WeekDates);
        Assert.Null(result.Schedule.CurrentWeekNumber);
        Assert.Equal(14, result.Schedule.ViewedWeekNumber);
    }

    [Fact]
    public async Task NextWeek_ResolvesServerWeekThenPinsItsTermAndPreservesTargetTimestamp()
    {
        var current = WeekData();
        var next = WeekData(9);
        next["updated_at"] = "2026-10-06T20:00:00Z";
        next["current_week_number"] = null;
        next["current_week_label"] = "";
        var handler = new ProtocolHandler { Bodies = [current, next] };
        var result = await Create(handler).FetchAsync(StudentId,
            new ScheduleRequest { Mode = "next", CookieRq = "2026-10-12" }, default);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Empty(handler.Requests[0].Uri.Query);
        var query = QueryHelpers.ParseQuery(handler.Requests[1].Uri.Query);
        Assert.Equal("9", query["week"]);
        Assert.Equal(Term, query["academic_term"]);
        Assert.Equal(9, result.Schedule!.ViewedWeekNumber);
        Assert.Equal(8, result.Schedule.CurrentWeekNumber);
        Assert.Equal("2026-10-06T20:00:00Z", result.Schedule.RawPayload["updated_at"]);
        Assert.All(handler.Requests, request => Assert.Equal("/api/schedule/week", request.Uri.AbsolutePath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptySchedule_IsSuccessAndRetainsSourceTime(bool semester)
    {
        var body = semester ? SemesterData() : WeekData();
        body["rows"] = Array.Empty<ScheduleRow>();
        var result = await Fetch(Create(new ProtocolHandler { Bodies = [body] }), semester);
        Assert.NotNull(result.Schedule);
        Assert.Empty(result.Schedule!.Rows);
        Assert.Equal(UpdatedAt, result.Schedule.RawPayload["updated_at"]);
        Assert.Equal("protocol_api", result.Schedule.RawPayload["retrieval_channel"]);
    }

    [Fact]
    public async Task Semester_SendsExplicitTermAndRetainsSharedBusinessMetadata()
    {
        var handler = new ProtocolHandler { Bodies = [SemesterData()] };
        var result = await Create(handler, LoadOptions(academicTerm: "configured-term"))
            .FetchSemesterAsync(StudentId, Term, default);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/schedule/semester", request.Uri.AbsolutePath);
        var query = QueryHelpers.ParseQuery(request.Uri.Query);
        Assert.Single(query);
        Assert.Equal(Term, query["academic_term"]);
        Assert.Equal("synthetic-protocol-key", request.ApiKey);
        var schedule = Assert.IsType<JwScheduleFetchResult>(result.Schedule);
        Assert.Equal("全部周", schedule.WeekLabel);
        Assert.Null(schedule.ViewedWeekNumber);
        Assert.Null(schedule.CurrentWeekNumber);
        Assert.Empty(schedule.AvailableWeeks);
        Assert.Single(Assert.IsType<List<object?>>(schedule.RawPayload["available_terms"]));
        Assert.Single(Assert.IsType<List<object?>>(schedule.RawPayload["notes"]));
        Assert.Single(Assert.IsType<List<object?>>(schedule.RawPayload["supplementary_courses"]));
        Assert.Equal(Term, Assert.IsType<Dictionary<string, object?>>(schedule.RawPayload["term"])["academic_term"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData(Term)]
    public async Task Semester_UsesConfiguredTermOrServerDefault(string configuredTerm)
    {
        var handler = new ProtocolHandler { Bodies = [SemesterData()] };
        Assert.NotNull((await Create(handler, LoadOptions(academicTerm: configuredTerm))
            .FetchSemesterAsync(StudentId, "", default)).Schedule);
        var query = QueryHelpers.ParseQuery(Assert.Single(handler.Requests).Uri.Query);
        if (configuredTerm.Length == 0) Assert.Empty(query);
        else Assert.Equal(configuredTerm, query["academic_term"]);
    }

    [Theory]
    [InlineData(false, "2026-10-05T12:59:59Z", "protocol_stale")]
    [InlineData(true, "2026-10-05T12:59:59Z", "protocol_stale")]
    [InlineData(false, "2026-10-07T01:05:01Z", "protocol_stale")]
    [InlineData(true, "2026-10-07T01:05:01Z", "protocol_stale")]
    [InlineData(false, "2026-10-07T01:00:00", "protocol_invalid")]
    [InlineData(true, "", "protocol_invalid")]
    public async Task InvalidSourceTime_FallsBack(bool semester, string updatedAt, string reason)
    {
        var body = semester ? SemesterData() : WeekData();
        body["updated_at"] = updatedAt;
        AssertUnavailable(await Fetch(Create(new ProtocolHandler { Bodies = [body] }), semester), reason);
    }

    [Theory]
    [InlineData("2026-10-05T13:00:00Z", 36)]
    [InlineData("2026-10-07T01:05:00Z", 36)]
    [InlineData("2026-10-04T01:00:00Z", 72)]
    public async Task Freshness_UsesConfiguredAgeAndFiveMinuteClockTolerance(string updatedAt, int maxAgeHours)
    {
        var body = WeekData();
        body["updated_at"] = updatedAt;
        var result = await Create(new ProtocolHandler { Bodies = [body] }, LoadOptions(maxAgeHours: maxAgeHours))
            .FetchAsync(StudentId, new ScheduleRequest(), default);
        Assert.Equal(updatedAt, result.Schedule!.RawPayload["updated_at"]);
    }

    [Theory]
    [InlineData(false, "account", "protocol_account_mismatch")]
    [InlineData(true, "account", "protocol_account_mismatch")]
    [InlineData(false, "term", "protocol_term_mismatch")]
    [InlineData(true, "term", "protocol_term_mismatch")]
    [InlineData(false, "identity-missing", "protocol_invalid")]
    [InlineData(true, "term-missing", "protocol_invalid")]
    [InlineData(false, "week", "protocol_week_missing")]
    [InlineData(false, "row-week", "protocol_week_missing")]
    [InlineData(false, "current-week", "protocol_week_missing")]
    [InlineData(false, "dates", "protocol_invalid")]
    public async Task MismatchedResponse_IsRejected(bool semester, string mutation, string reason)
    {
        var body = semester ? SemesterData() : WeekData();
        switch (mutation)
        {
            case "account": body["student"] = new ScheduleStudentSummary("other-student", "假身份"); break;
            case "term": body["term"] = new ScheduleTermSummary("其它学期", "other-term"); break;
            case "identity-missing": body.Remove("student"); break;
            case "term-missing": body.Remove("term"); break;
            case "week": body["viewed_week_number"] = 9; break;
            case "row-week": body["rows"] = new[] { Row(9) }; break;
            case "current-week": body["current_week_number"] = 9; break;
            case "dates": body["week_dates"] = new Dictionary<string, string> { ["星期一"] = "2026-10-06" }; break;
        }
        AssertUnavailable(await Fetch(Create(new ProtocolHandler { Bodies = [body] }), semester), reason);
    }

    [Theory]
    [InlineData("manual", "protocol_week_missing")]
    [InlineData("term-label", "protocol_term_mismatch")]
    [InlineData("cookie-date", "protocol_week_missing")]
    [InlineData("current-date", "protocol_week_missing")]
    [InlineData("no-current-context", "protocol_week_missing")]
    public async Task WeekSelection_MustMatchResponse(string selection, string reason)
    {
        var body = selection == "current-date" ? WeekData(9) : WeekData();
        var request = selection switch
        {
            "manual" => new ScheduleRequest { Mode = "manual", ManualWeek = "第9周" },
            "term-label" => new ScheduleRequest { TermLabel = "other-label" },
            "cookie-date" => new ScheduleRequest { CookieRq = "2026-10-12" },
            _ => new ScheduleRequest(),
        };
        if (selection == "no-current-context")
        {
            body["week_dates"] = new Dictionary<string, string>();
            body["current_week_number"] = null;
        }
        AssertUnavailable(await Create(new ProtocolHandler { Bodies = [body] }).FetchAsync(StudentId, request, default), reason);
    }

    [Theory]
    [InlineData("term", "protocol_term_mismatch")]
    [InlineData("week", "protocol_week_missing")]
    public async Task NextWeek_MustMatchResolvedTermAndWeek(string mutation, string reason)
    {
        var next = WeekData(mutation == "week" ? 10 : 9);
        if (mutation == "term") next["term"] = new ScheduleTermSummary("其它学期", "other-term");
        var handler = new ProtocolHandler { Bodies = [WeekData(), next] };
        AssertUnavailable(await Create(handler).FetchAsync(StudentId, new ScheduleRequest { Mode = "next" }, default), reason);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Theory]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.Unauthorized)]
    [InlineData(false, HttpStatusCode.Found)]
    [InlineData(true, HttpStatusCode.ServiceUnavailable)]
    public async Task HttpFailure_ReturnsFallbackWithoutLoginOrServerDetails(bool semester, HttpStatusCode status)
    {
        var handler = new ProtocolHandler { Status = status, RawBody = "private-server-detail" };
        var result = await Fetch(Create(handler), semester);
        AssertUnavailable(result, "protocol_http_failure");
        Assert.Single(handler.Requests);
        Assert.DoesNotContain("private-server-detail", JsonSerializer.Serialize(result));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Timeout_FallsBackAndCallerCancellationPropagates(bool semester)
    {
        var slow = new ProtocolHandler { Slow = true };
        AssertUnavailable(await Fetch(Create(slow, LoadOptions(timeoutSeconds: 1)), semester), "protocol_timeout");
        var cancelled = new ProtocolHandler { Slow = true };
        using var cancellation = new CancellationTokenSource();
        var task = Fetch(Create(cancelled), semester, cancellation.Token);
        await cancelled.Started.Task;
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        var notStarted = new ProtocolHandler();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Fetch(Create(notStarted), semester, cancellation.Token));
        Assert.Empty(notStarted.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NetworkFailure_DoesNotEchoEndpointOrApiKey(bool semester)
    {
        var result = await Fetch(Create(new ProtocolHandler
        {
            Failure = new HttpRequestException("synthetic-protocol-key https://example.test"),
        }), semester);
        AssertUnavailable(result, "protocol_unavailable");
        Assert.DoesNotContain("synthetic-protocol-key", JsonSerializer.Serialize(result));
        Assert.DoesNotContain("example.test", JsonSerializer.Serialize(result));
    }

    [Theory]
    [InlineData("private-invalid-json")]
    [InlineData("{\"ok\":false,\"msg\":\"private\",\"data\":null}")]
    [InlineData("{\"ok\":true,\"data\":[]}")]
    [InlineData("{\"week\":8,\"courses\":[]}")]
    public async Task InvalidEnvelope_FallsBack(string json)
    {
        AssertUnavailable(await Create(new ProtocolHandler { RawBody = json }).FetchAsync(StudentId, new ScheduleRequest(), default), "protocol_invalid");
    }

    [Theory]
    [InlineData("http://example.test")]
    [InlineData("https://user:password@example.test")]
    [InlineData("https://example.test?token=private")]
    [InlineData("https://example.test#fragment")]
    public async Task InvalidBaseUrl_NeverMakesRequest(string endpoint)
    {
        var handler = new ProtocolHandler();
        AssertUnavailable(await Create(handler, LoadOptions(endpoint: endpoint)).FetchAsync(StudentId, new ScheduleRequest(), default), "protocol_configuration_invalid");
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task MissingKeyOrConfiguredIdentityMismatch_NeverMakesRequest()
    {
        var handler = new ProtocolHandler();
        AssertUnavailable(await Create(handler, new ScheduleApiOptions { Provider = "protocol", Endpoint = "https://example.test", BearerToken = "synthetic-bearer" })
            .FetchAsync(StudentId, new ScheduleRequest(), default), "protocol_configuration_invalid");
        AssertUnavailable(await Create(handler, LoadOptions(studentId: "other-student"))
            .FetchSemesterAsync(StudentId, Term, default), "protocol_account_mismatch");
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("unsupported", "8")]
    [InlineData("manual", "0")]
    [InlineData("manual", "2026-10-05")]
    public async Task InvalidWeekRequest_NeverMakesRequest(string mode, string week)
    {
        var handler = new ProtocolHandler();
        AssertUnavailable(await Create(handler).FetchAsync(StudentId, new ScheduleRequest { Mode = mode, ManualWeek = week }, default), "protocol_request_invalid");
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("snapshot", false)]
    [InlineData("protocol", false)]
    [InlineData("unknown", true)]
    public void ProviderConfiguration_IsExplicitAndDefaultRemainsSnapshot(string provider, bool invalid)
    {
        Assert.Equal("snapshot", new ScheduleApiOptions().Provider);
        var options = LoadOptions(provider: provider);
        Assert.Equal(invalid, options.InvalidConfiguration);
        if (!invalid) Assert.Equal(provider, options.Provider);
        Assert.DoesNotContain("synthetic-protocol-key", JsonSerializer.Serialize(options));
    }

    [Theory]
    [InlineData("snapshot", false)]
    [InlineData("protocol", false)]
    [InlineData("snapshot", true)]
    [InlineData("protocol", true)]
    public async Task HostRegistration_PreservesOptionsAndSnapshotOverride(string provider, bool useOverride)
    {
        using var web = new TemporaryWebRoot();
        var options = new ScheduleApiOptions { Provider = provider };
        var source = new WeekOnlySource();
        await using var app = XkeWebHostApplication.Create(options: new XkeWebHostOptions
        {
            AuthDataRootPath = _temp.RootPath, WebRootPath = web.RootPath, EnableBackgroundTasks = false,
            ScheduleApiOptions = options, ScheduleSnapshotSource = useOverride ? source : null,
        });
        Assert.Same(options, app.Services.GetRequiredService<ScheduleApiOptions>());
        var registered = app.Services.GetRequiredService<IScheduleSnapshotSource>();
        if (useOverride) Assert.Same(source, registered);
        else if (provider == "protocol") Assert.IsType<ProtocolScheduleApiClient>(registered);
        else Assert.IsType<PersonalScheduleApiClient>(registered);
    }

    [Fact]
    public async Task ExistingWeekOnlySource_DefaultSemesterRemainsUnavailable()
    {
        IScheduleSnapshotSource source = new WeekOnlySource();
        AssertUnavailable(await source.FetchSemesterAsync(StudentId, Term, default), string.Empty);
    }

    private ProtocolScheduleApiClient Create(ProtocolHandler handler, ScheduleApiOptions? options = null)
    {
        var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        _clients.Add(http);
        return new(http, options ?? LoadOptions(), new ProtocolClock());
    }

    private ScheduleApiOptions LoadOptions(string endpoint = "https://example.test", string academicTerm = "", string studentId = "",
        int timeoutSeconds = 8, int maxAgeHours = 36, string provider = "protocol")
    {
        // Only generated test files may be loaded, never a developer's private override.
        Assert.True(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ScheduleApiOptions.ConfigEnvironmentVariable)));
        Assert.False(File.Exists(Path.Combine(AppContext.BaseDirectory, ScheduleApiOptions.RuntimeFileName)));
        var directory = Path.Combine(_temp.RootPath, "开发数据", "课表服务");
        Directory.CreateDirectory(directory);
        new ProtectedJsonFileStore().Write(Path.Combine(directory, "token.dpapi"), "synthetic-protocol-key");
        File.WriteAllText(Path.Combine(directory, "client.local.json"), JsonSerializer.Serialize(new
        {
            provider, endpoint, protected_token_file = "token.dpapi", academic_term = academicTerm, student_id = studentId,
            timeout_seconds = timeoutSeconds, max_age_hours = maxAgeHours,
        }));
        return ScheduleApiOptions.Load(_temp.RootPath);
    }

    private static Task<ScheduleSnapshotAttempt> Fetch(ProtocolScheduleApiClient client, bool semester, CancellationToken token = default)
        => semester ? client.FetchSemesterAsync(StudentId, Term, token) : client.FetchAsync(StudentId, new ScheduleRequest { AcademicTerm = Term }, token);

    private static void AssertUnavailable(ScheduleSnapshotAttempt result, string reason)
    {
        Assert.Null(result.Schedule);
        Assert.Equal(reason, result.FallbackReason);
    }

    private static ScheduleRow Row(int week) => new()
    {
        Source = "教务系统课表", WeekLabel = $"第{week}周", Weekday = "星期一", Section = "第1-2节",
        CourseName = "测试课程", Teacher = "测试教师", Room = "测试教室", Weeks = "1-16周", SectionNumbers = [1, 2],
        WeekNumbers = Enumerable.Range(1, 16).ToArray(), TimeRange = "08:00-09:35", BlockTime = "08:00-09:35",
        TimeSegments = ["08:00-09:35"], HoursText = "2学时",
    };

    private static Dictionary<string, object?> WeekData(int week = 8)
    {
        string[] days = ["星期一", "星期二", "星期三", "星期四", "星期五", "星期六", "星期日"];
        var monday = new DateOnly(2026, 10, 5).AddDays((week - 8) * 7);
        var dates = days.Select((day, index) => new { day, date = monday.AddDays(index).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) })
            .ToDictionary(item => item.day, item => item.date);
        var data = new ScheduleData("教务系统课表", "教务系统课表", "jw", [Row(week)], $"第{week}周",
            [new(dates["星期一"], $"第{week}周", true)], dates, "第8周", 8, week, "获取成功", [], [], "", [],
            new("测试学期", Term), new(StudentId, "测试学生"), false, "live_fetch", UpdatedAt, "");
        var body = JsonValueConverter.ToDictionary(JsonSerializer.SerializeToElement(data));
        body["notes"] = new[] { "备注原文" };
        body["supplementary_courses"] = new[] { new Dictionary<string, string> { ["course_name"] = "无课表课程" } };
        return body;
    }

    private static Dictionary<string, object?> SemesterData()
    {
        var data = new SemesterScheduleData(new("测试学期", Term), new(StudentId, "测试学生"), [Row(8)], [],
            [new(Term, "测试学期", true)], ["备注原文"], [new() { ["course_name"] = "无课表课程" }], UpdatedAt, []);
        return JsonValueConverter.ToDictionary(JsonSerializer.SerializeToElement(data));
    }

    private sealed class ProtocolClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 7, 1, 0, 0, TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed record SentRequest(HttpMethod Method, Uri Uri, string? ApiKey, string? Authorization, bool HasContent, string[] Headers);

    private sealed class ProtocolHandler : HttpMessageHandler
    {
        public List<SentRequest> Requests { get; } = [];
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object[] Bodies { get; init; } = [WeekData()];
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        public string? RawBody { get; init; }
        public bool Slow { get; init; }
        public Exception? Failure { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new(request.Method, request.RequestUri!,
                request.Headers.TryGetValues("X-XKE-Api-Key", out var keys) ? keys.Single() : null,
                request.Headers.Authorization?.ToString(), request.Content is not null, request.Headers.Select(header => header.Key).ToArray()));
            Started.TrySetResult();
            if (Failure is not null) throw Failure;
            if (Slow) await Task.Delay(Timeout.Infinite, cancellationToken);
            return new(Status)
            {
                Content = new StringContent(RawBody ?? JsonSerializer.Serialize(new ApiEnvelope(Data: Bodies[Math.Min(Requests.Count - 1, Bodies.Length - 1)]))),
            };
        }
    }

    private sealed class WeekOnlySource : IScheduleSnapshotSource
    {
        public Task<ScheduleSnapshotAttempt> FetchAsync(string studentId, ScheduleRequest request, CancellationToken cancellationToken)
            => Task.FromResult(ScheduleSnapshotAttempt.Unavailable("snapshot_unavailable"));
    }

    public void Dispose()
    {
        foreach (var http in _clients) http.Dispose();
        _temp.Dispose();
    }
}
