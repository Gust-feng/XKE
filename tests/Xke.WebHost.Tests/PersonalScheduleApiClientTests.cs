using System.Net;
using System.Text.Json;
using Xke.Contracts.Requests;
using Xke.Core.Schedule;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class PersonalScheduleApiClientTests
{
    [Fact]
    public async Task ValidSnapshot_ConvertsCoursesToTheExistingScheduleModel()
    {
        var handler = new SnapshotHandler();
        var client = Create(handler);
        var result = await client.FetchAsync("20260001", new ScheduleRequest { Mode = "current" }, default);
        Assert.NotNull(result.Schedule);
        Assert.Equal("jw_schedule", result.Schedule!.SourceKey);
        Assert.Equal("jw", result.Schedule.SourceType);
        Assert.Equal("第8周", result.Schedule.WeekLabel);
        Assert.Equal("第1-2节", Assert.Single(result.Schedule.Rows)["section"]);
        Assert.Equal("08:00-09:35", result.Schedule.Rows[0]["time_range"]);
        Assert.Equal("Bearer test-token", handler.Authorization);
        Assert.Equal("personal_api", result.Schedule.RawPayload["retrieval_channel"]);
        Assert.Equal("2026-10-07T00:00:00.0000000+00:00", result.Schedule.RawPayload["updated_at"]);
        Assert.DoesNotContain("test-token", JsonSerializer.Serialize(result.Schedule));
        Assert.DoesNotContain("example.test", JsonSerializer.Serialize(result.Schedule));
        Assert.Empty(result.FallbackReason);
    }

    [Theory]
    [InlineData("stale", "snapshot_stale")]
    [InlineData("future", "snapshot_stale")]
    [InlineData("wrong-account", "snapshot_account_mismatch")]
    [InlineData("bad-date", "snapshot_invalid")]
    [InlineData("bad-section", "snapshot_invalid")]
    [InlineData("missing-courses", "snapshot_invalid")]
    [InlineData("no-offset", "snapshot_invalid")]
    public async Task InvalidSnapshots_AreRejectedWithoutReturningPersonalData(string mutation, string expectedReason)
    {
        var body = SnapshotHandler.Snapshot();
        switch (mutation)
        {
            case "stale": body["fetched_at"] = "2026-10-04T00:00:00Z"; break;
            case "future": body["fetched_at"] = "2026-10-08T00:00:00Z"; break;
            case "wrong-account": body["student_id"] = "20269999"; break;
            case "bad-date": body["dates"] = new Dictionary<string, string>(); break;
            case "bad-section": ((Dictionary<string, object?>[])body["courses"]!)[0]["sections"] = new[] { 0 }; break;
            case "missing-courses": body.Remove("courses"); break;
            case "no-offset": body["fetched_at"] = "2026-10-07T00:00:00"; break;
        }
        var result = await Create(new SnapshotHandler { Body = body }).FetchAsync("20260001", new ScheduleRequest(), default);
        Assert.Null(result.Schedule);
        Assert.Equal(expectedReason, result.FallbackReason);
    }

    [Fact]
    public async Task EmptySuccessfulWeek_IsValidAndWeekOrTermMismatchFallsBack()
    {
        var body = SnapshotHandler.Snapshot();
        body["courses"] = Array.Empty<object>();
        var client = Create(new SnapshotHandler { Body = body });
        var empty = await client.FetchAsync("20260001", new ScheduleRequest(), default);
        Assert.Empty(empty.Schedule!.Rows);
        Assert.Equal("snapshot_week_missing", (await client.FetchAsync("20260001", new ScheduleRequest { Mode = "next" }, default)).FallbackReason);
        Assert.Equal("snapshot_week_missing", (await client.FetchAsync("20260001", new ScheduleRequest { Mode = "manual", ManualWeek = "第9周" }, default)).FallbackReason);
        Assert.NotNull((await client.FetchAsync("20260001", new ScheduleRequest { Mode = "manual", ManualWeek = "第8周" }, default)).Schedule);
        Assert.Equal("snapshot_term_mismatch", (await client.FetchAsync("20260001", new ScheduleRequest { AcademicTerm = "other-term" }, default)).FallbackReason);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Found)]
    public async Task HttpFailure_ReturnsOnlyStableFallbackCode(HttpStatusCode status)
    {
        var result = await Create(new SnapshotHandler { Status = status }).FetchAsync("20260001", new ScheduleRequest(), default);
        Assert.Null(result.Schedule);
        Assert.Equal("snapshot_http_failure", result.FallbackReason);
    }

    [Fact]
    public async Task Cancellation_PropagatesAndDoesNotBecomeSchoolFallback()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create(new SnapshotHandler()).FetchAsync("20260001", new ScheduleRequest(), cancellation.Token));
    }

    [Fact]
    public async Task UnconfiguredOrAnotherAccount_NeverMakesARequest()
    {
        var handler = new SnapshotHandler();
        var unconfigured = new PersonalScheduleApiClient(new HttpClient(handler), new ScheduleApiOptions(), new SnapshotClock());
        Assert.Null((await unconfigured.FetchAsync("20260001", new ScheduleRequest(), default)).Schedule);
        Assert.Null((await Create(handler).FetchAsync("20269999", new ScheduleRequest(), default)).Schedule);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task InvalidPrivateConfiguration_DisablesOnlySnapshotSource()
    {
        var handler = new SnapshotHandler();
        var client = new PersonalScheduleApiClient(new HttpClient(handler), new ScheduleApiOptions { InvalidConfiguration = true }, new SnapshotClock());
        var result = await client.FetchAsync("20260001", new ScheduleRequest(), default);
        Assert.Equal("snapshot_configuration_invalid", result.FallbackReason);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task MalformedJson_TriggersFallbackWithoutEchoingServerBody()
    {
        var handler = new SnapshotHandler { RawBody = "private-invalid-content" };
        var result = await Create(handler).FetchAsync("20260001", new ScheduleRequest(), default);
        Assert.Equal("snapshot_invalid", result.FallbackReason);
        Assert.Null(result.Schedule);
    }

    [Fact]
    public async Task Timeout_UsesStableCodeAndNetworkErrorDoesNotExposeUrlOrToken()
    {
        var timeoutClient = new PersonalScheduleApiClient(new HttpClient(new SlowHandler()), new ScheduleApiOptions
        {
            Endpoint = "https://example.test/schedule", StudentId = "20260001", TimeoutSeconds = 1,
        }, new SnapshotClock());
        var timeout = await timeoutClient.FetchAsync("20260001", new ScheduleRequest(), default);
        Assert.Equal("snapshot_timeout", timeout.FallbackReason);
        var failed = new PersonalScheduleApiClient(new HttpClient(new FailingHandler()), new ScheduleApiOptions
        {
            Endpoint = "https://example.test/schedule", StudentId = "20260001", BearerToken = "private-test-token",
        }, new SnapshotClock());
        var failure = await failed.FetchAsync("20260001", new ScheduleRequest(), default);
        Assert.Equal("snapshot_unavailable", failure.FallbackReason);
        Assert.DoesNotContain("private-test-token", JsonSerializer.Serialize(failure));
        Assert.DoesNotContain("example.test", JsonSerializer.Serialize(failure));
    }

    private sealed class SlowHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new(HttpStatusCode.OK);
        }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("private-test-token https://example.test/schedule");
    }

    [Fact]
    public async Task DocumentedSnapshotWithoutIdentityOrTerm_UsesPrivateAccountBinding()
    {
        var body = SnapshotHandler.Snapshot();
        body.Remove("student_id");
        body.Remove("academic_term");
        body.Remove("term_label");
        body["week_label"] = "第八周";
        var client = Create(new SnapshotHandler { Body = body });
        var result = await client.FetchAsync("20260001", new ScheduleRequest { Mode = "current", AcademicTerm = "test-term" }, default);
        Assert.NotNull(result.Schedule);
        Assert.Equal("第8周", result.Schedule!.WeekLabel);
        var term = Assert.IsType<Dictionary<string, object?>>(result.Schedule.RawPayload["term"]);
        Assert.Equal("test-term", term["academic_term"]);
    }

    [Fact]
    public async Task PersonalSnapshotWithoutAccountBinding_IsUsableForCurrentLocalAccount()
    {
        var body = SnapshotHandler.Snapshot();
        body.Remove("student_id");
        body.Remove("academic_term");
        body.Remove("term_label");
        var handler = new SnapshotHandler { Body = body };
        var client = new PersonalScheduleApiClient(new HttpClient(handler), new ScheduleApiOptions
        {
            Endpoint = "https://example.test/schedule", BearerToken = "synthetic-personal-token",
        }, new SnapshotClock());

        var result = await client.FetchAsync("local-personal-account", new ScheduleRequest { Mode = "current" }, default);

        Assert.NotNull(result.Schedule);
        Assert.Equal("jw_schedule", result.Schedule!.SourceKey);
        Assert.Equal("personal_api", result.Schedule.RawPayload["retrieval_channel"]);
        Assert.Equal(1, handler.Calls);
        Assert.Equal("Bearer synthetic-personal-token", handler.Authorization);
    }

    [Fact]
    public async Task ExplicitResponseIdentityMismatch_StillFallsBackWithoutPrivateAccountBinding()
    {
        var client = new PersonalScheduleApiClient(new HttpClient(new SnapshotHandler()), new ScheduleApiOptions
        {
            Endpoint = "https://example.test/schedule",
        }, new SnapshotClock());

        var result = await client.FetchAsync("different-account", new ScheduleRequest(), default);

        Assert.Null(result.Schedule);
        Assert.Equal("snapshot_account_mismatch", result.FallbackReason);
    }

    internal static PersonalScheduleApiClient Create(SnapshotHandler handler)
        => new(new HttpClient(handler), new ScheduleApiOptions
        {
            Endpoint = "https://example.test/schedule", BearerToken = "test-token", StudentId = "20260001", AcademicTerm = "test-term",
        }, new SnapshotClock());

    internal sealed class SnapshotClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 7, 1, 0, 0, TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    internal sealed class SnapshotHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Authorization { get; private set; }
        public string? ApiKey { get; private set; }
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        public object? Body { get; init; }
        public string? RawBody { get; init; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            Authorization = request.Headers.Authorization?.ToString();
            ApiKey = request.Headers.TryGetValues("X-API-Key", out var keys) ? keys.Single() : null;
            return Task.FromResult(new HttpResponseMessage(Status)
            {
                Content = new StringContent(RawBody ?? JsonSerializer.Serialize(Body ?? Snapshot())),
            });
        }

        internal static Dictionary<string, object?> Snapshot() => new()
        {
            ["student_id"] = "20260001", ["fetched_at"] = "2026-10-07T00:00:00Z", ["week"] = 8,
            ["academic_term"] = "test-term", ["term_label"] = "测试学期",
            ["dates"] = new Dictionary<string, string>
            {
                ["星期一"] = "2026-10-05", ["星期二"] = "2026-10-06", ["星期三"] = "2026-10-07", ["星期四"] = "2026-10-08",
                ["星期五"] = "2026-10-09", ["星期六"] = "2026-10-10", ["星期日"] = "2026-10-11",
            },
            ["courses"] = new Dictionary<string, object?>[]
            {
                new() { ["course_name"] = "测试课程", ["teacher"] = "测试老师", ["room"] = "测试教室", ["weeks"] = "第8周",
                    ["credit"] = "2", ["sections"] = new[] { 1, 2 }, ["weekday"] = "星期一", ["date"] = "2026-10-05", ["block_time"] = "08:00-09:35" },
            },
        };
    }
}
