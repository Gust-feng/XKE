using System.Net;
using System.Text.Json;
using Xke.Contracts.Models;
using Xke.Contracts.Responses;
using Xke.Core.Auth;
using Xke.Core.Network;
using Xke.Storage;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class CampusSelfServiceTests
{
    private const string Dashboard = """
        <div class="row"><label class="text-right">状态：</label><div>正常</div></div>
        <div class="row"><label class="text-right">套餐：</label><div>合成校园套餐</div></div>
        <div class="row"><label class="text-right">计费方式：</label><div>按月</div></div>
        <div class="row"><label class="text-right">计费周期：</label><div>2026-10-01 00:00:00 至 2026-10-31 23:59:59</div></div>
        <div class="row"><label class="text-right">余额：</label><div>12345</div></div>
        <div class="row"><label class="text-right">姓名：</label><div>must-not-leave-parser</div></div>
        <script>window.user = { userPassword: 'synthetic-secret' };</script>
        """;
    private static CampusSelfServiceOptions Options => new() { Endpoint = "http://campus.example.test/Self" };

    [Fact]
    public void Parser_EmitsOnlyUsefulAccountFactsAndKeepsServerPeriod()
    {
        var summary = CampusSelfServiceParser.Parse(Dashboard);
        Assert.Equal("正常", summary.AccountStatus);
        Assert.Equal("合成校园套餐", summary.PlanName);
        Assert.Equal("按月", summary.BillingMethod);
        Assert.Equal("2026-10-01 00:00:00", summary.PeriodStart);
        Assert.Equal("2026-10-31 23:59:59", summary.PeriodEnd);
        var json = JsonSerializer.Serialize(summary);
        Assert.DoesNotContain("12345", json);
        Assert.DoesNotContain("must-not-leave-parser", json);
        Assert.DoesNotContain("synthetic-secret", json);
        Assert.DoesNotContain("userPassword", json);
    }

    [Fact]
    public void Parser_DoesNotInventAbsentFieldsOrDates()
    {
        var summary = CampusSelfServiceParser.Parse("<div class='row'><span class='text-right'>套餐：</span><span>Test plan</span></div>");
        Assert.Equal("Test plan", summary.PlanName);
        Assert.Null(summary.AccountStatus);
        Assert.Null(summary.BillingMethod);
        Assert.Null(summary.PeriodStart);
        Assert.Null(summary.PeriodEnd);
        Assert.Throws<CampusSelfServiceException>(() => CampusSelfServiceParser.Parse("<html>not a dashboard</html>"));
    }

    [Fact]
    public async Task Client_SubmitsPageFieldsAndInitializesCode_ThenReusesSession()
    {
        var handler = new ProtocolHandler();
        using var client = new CampusSelfServiceClient(Options, handler);
        await client.GetSummaryAsync("synthetic account", "test&password", default);
        await client.GetSummaryAsync("synthetic account", "test&password", default);
        Assert.Equal(1, handler.LoginPosts);
        Assert.Equal(2, handler.DashboardReads);
        Assert.Contains("foo=page-foo", handler.Form);
        Assert.Contains("bar=page-bar", handler.Form);
        Assert.Contains("checkcode=0", handler.Form);
        Assert.Contains("code=", handler.Form);
        Assert.Contains("account=synthetic+account", handler.Form);
        Assert.Contains("password=test%26password", handler.Form);
        Assert.Equal(new[] { "GET /Self/login/", "GET /Self/login/randomCode", "POST /Self/login/verify", "GET /Self/dashboard", "GET /Self/dashboard" }, handler.Requests);
    }

    [Fact]
    public async Task Client_RenewsOnlyItsExpiredBackendSession()
    {
        var handler = new ProtocolHandler();
        using var client = new CampusSelfServiceClient(Options, handler);
        await client.GetSummaryAsync("synthetic", "test", default);
        handler.ExpireNextDashboard = true;
        var summary = await client.GetSummaryAsync("synthetic", "test", default);
        Assert.Equal("合成校园套餐", summary.PlanName);
        Assert.Equal(2, handler.LoginPosts);
        Assert.DoesNotContain(handler.Requests, request => request.Contains("logout") || request.Contains("tooffline"));
    }

    [Fact]
    public async Task Summary_CachesSuccessRetainsItOnFailureAndSeparatesAccounts()
    {
        using var temp = new TemporaryDirectory();
        var root = new XkeDataRoot(temp.RootPath);
        var firstSession = AddAccount(root, "synthetic-a");
        var secondSession = AddAccount(root, "synthetic-b");
        var clock = new ManualTime();
        var clients = new List<SummaryClient>();
        using var service = new CampusSelfServiceSummaryService(root, Options, clock, () =>
        {
            var client = new SummaryClient(); clients.Add(client); return client;
        });
        var first = await service.GetSummaryAsync(firstSession);
        var cached = await service.GetSummaryAsync(firstSession);
        Assert.False(first.Cached);
        Assert.True(cached.Cached);
        Assert.Equal(1, clients[0].Calls);
        clock.Advance(TimeSpan.FromMinutes(6));
        clients[0].Fail = true;
        var stale = await service.GetSummaryAsync(firstSession);
        Assert.True(stale.Available);
        Assert.True(stale.Cached);
        Assert.Equal(first.PlanName, stale.PlanName);
        Assert.Equal(first.UpdatedAt, stale.UpdatedAt);
        Assert.NotEmpty(stale.Message);
        var retry = await service.GetSummaryAsync(firstSession);
        Assert.False(retry.Cached);
        Assert.Empty(retry.Message);
        Assert.Equal(2, clients.Count);
        root.ForStudent("synthetic-a").NetworkCredentials().Save(new StoredNetworkCredentialDocument { Provider = "campus", Account = "synthetic-a", Password = "changed-synthetic-password" });
        var changedCredential = await service.GetSummaryAsync(firstSession);
        Assert.False(changedCredential.Cached);
        Assert.Equal(3, clients.Count);
        var second = await service.GetSummaryAsync(secondSession);
        Assert.Equal("Plan synthetic-b", second.PlanName);
        Assert.False(second.Cached);
        Assert.True(clients[0].Disposed);
    }

    [Fact]
    public async Task Summary_RequiresApplicationSessionAndDoesNotFetchWhenUnconfigured()
    {
        using var temp = new TemporaryDirectory();
        var root = new XkeDataRoot(temp.RootPath);
        var session = AddAccount(root, "synthetic");
        using var service = new CampusSelfServiceSummaryService(root, new(), TimeProvider.System,
            () => throw new InvalidOperationException("No external request expected"));
        await Assert.ThrowsAsync<AuthException>(() => service.GetSummaryAsync(""));
        var result = await service.GetSummaryAsync(session);
        Assert.False(result.Configured);
        Assert.False(result.Available);
    }

    [Fact]
    public async Task Summary_RequiresIndependentCampusCredentialsAndNeverFallsBackToTeachingPassword()
    {
        using var temp = new TemporaryDirectory(); var root = new XkeDataRoot(temp.RootPath);
        root.Credentials().Save(new StoredCredentialDocument { User = "teaching-account", StudentId = "student", Password = "teaching-secret" });
        var session = root.Sessions().Create("teaching-account", "student", "auto").SessionId;
        var clients = new List<SummaryClient>();
        using var service = new CampusSelfServiceSummaryService(root, Options, TimeProvider.System, () => { var client = new SummaryClient(); clients.Add(client); return client; });
        var missing = await service.GetSummaryAsync(session);
        Assert.False(missing.Available);
        Assert.Equal("请先配置校园网账号和密码", missing.Message);
        Assert.Empty(clients);
        root.ForStudent("student").NetworkCredentials().Save(new StoredNetworkCredentialDocument { Provider = "campus", Account = "campus-account", Password = "campus-secret" });
        Assert.Equal("Plan campus-account", (await service.GetSummaryAsync(session)).PlanName);
        Assert.Equal(1, Assert.Single(clients).Calls);
    }

    private static string AddAccount(XkeDataRoot root, string account)
    {
        root.ForStudent(account).NetworkCredentials().Save(new StoredNetworkCredentialDocument { Provider = "campus", Account = account, Password = "synthetic-password" });
        return root.Sessions().Create(account, account, "auto").SessionId;
    }

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 14, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan amount) => _now += amount;
    }

    private sealed class SummaryClient : ICampusSelfServiceClient
    {
        public int Calls { get; private set; }
        public bool Fail { get; set; }
        public bool Disposed { get; private set; }
        public Task<CampusSelfServiceSummary> GetSummaryAsync(string account, string password, CancellationToken token)
        {
            Calls++;
            if (Fail) throw new CampusSelfServiceException("synthetic failure");
            return Task.FromResult(new CampusSelfServiceSummary { PlanName = "Plan " + account });
        }
        public void Dispose() => Disposed = true;
    }

    private sealed class ProtocolHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        public int LoginPosts { get; private set; }
        public int DashboardReads { get; private set; }
        public bool ExpireNextDashboard { get; set; }
        public string Form { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add(request.Method + " " + path);
            if (path == "/Self/login/") return Html("""
                <form action="/Self/login/verify">
                <input name="foo" type="text" value="page-foo"><input name="bar" type="password" value="page-bar">
                <input name="checkcode" type="hidden" value="0"><input name="code" value="">
                <input name="account"><input name="password" type="password"></form>
                """);
            if (path == "/Self/login/randomCode") return new(HttpStatusCode.OK) { Content = new ByteArrayContent([0]) };
            if (path == "/Self/login/verify")
            {
                LoginPosts++;
                Form = await request.Content!.ReadAsStringAsync(token);
                return Redirect("/Self/dashboard");
            }
            if (path == "/Self/dashboard")
            {
                DashboardReads++;
                if (ExpireNextDashboard) { ExpireNextDashboard = false; return Redirect("/Self/login/"); }
                return Html(Dashboard);
            }
            throw new InvalidOperationException("Unexpected protocol path");
        }
        private static HttpResponseMessage Html(string html) => new(HttpStatusCode.OK) { Content = new StringContent(html) };
        private static HttpResponseMessage Redirect(string path)
        {
            var response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.Location = new Uri(path, UriKind.Relative);
            return response;
        }
    }
}
