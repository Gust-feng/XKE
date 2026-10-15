using System.Net;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Core.Auth;
using Xke.Core.Network;
using Xke.Storage;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class NetworkMaintenanceReliabilityTests
{
    [Theory]
    [InlineData("<html><form action='/signin'>Sign in to this network</form></html>")]
    [InlineData("<html><form><input type='password'></form></html>")]
    [InlineData("<html><head><title>Portal</title></head><body>Dr.COM</body></html>")]
    public async Task CaptiveSuccessPage_IsNotMistakenForInternet(string html)
    {
        using var fixture = new Fixture();
        fixture.Handler.External = _ => (HttpStatusCode.OK, html, null);
        var status = await fixture.Service.GetStatusAsync(fixture.Session);
        Assert.True(status.Payload.CampusConnected);
        Assert.False(status.Payload.InternetConnected);
        var result = await fixture.Service.MaintainInBackgroundAsync(fixture.Session);
        Assert.Equal(1, fixture.Handler.Logins);
        Assert.False(result.Payload.InternetConnected);
        Assert.NotEmpty(result.Payload.OperationError);
        Assert.False(fixture.Student.LoadNetworkAuthHistory().Records.Last().Success);
    }

    [Theory]
    [InlineData(HttpStatusCode.Redirect)]
    [InlineData(HttpStatusCode.ProxyAuthenticationRequired)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task NonSuccessProbe_IsNotOnline(HttpStatusCode code)
    {
        using var fixture = new Fixture();
        fixture.Handler.External = _ => (code, "response", null);
        Assert.False((await fixture.Service.GetStatusAsync(fixture.Session)).Payload.InternetConnected);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task TargetBusinessErrors_StillProveReachabilityWithoutReauthentication(HttpStatusCode code)
    {
        using var fixture = new Fixture();
        fixture.Handler.External = _ => (code, "target response", null);
        var result = await fixture.Service.MaintainInBackgroundAsync(fixture.Session);
        Assert.True(result.Payload.InternetConnected);
        Assert.Empty(result.Payload.OperationError);
        Assert.Equal(0, fixture.Handler.Logins);
    }

    [Fact]
    public async Task ForeignOriginResponse_IsNotOnline()
    {
        using var fixture = new Fixture();
        fixture.Handler.External = _ => (HttpStatusCode.OK, "Sign in", "https://auth.invalid/signin");
        Assert.False((await fixture.Service.GetStatusAsync(fixture.Session)).Payload.InternetConnected);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "<html>OK<script>const template=\"<form><input type=password></form>\"</script></html>")]
    [InlineData(HttpStatusCode.NoContent, "")]
    public async Task GenuineSuccess_DoesNotSendCredentials(HttpStatusCode code, string text)
    {
        using var fixture = new Fixture();
        fixture.Handler.External = _ => (code, text, null);
        var result = await fixture.Service.MaintainInBackgroundAsync(fixture.Session);
        Assert.True(result.Payload.InternetConnected);
        Assert.Empty(result.Payload.OperationError);
        Assert.Equal(0, fixture.Handler.Logins);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Authentication_UsesOneFinalSnapshotForStatusErrorAndHistory(bool finalOnline)
    {
        using var fixture = new Fixture();
        fixture.Handler.External = n =>
        {
            var online = n switch { 1 => false, 2 => finalOnline, _ => !finalOnline };
            return online ? (HttpStatusCode.OK, "OK", null) : (HttpStatusCode.ServiceUnavailable, "unavailable", null);
        };
        var result = await fixture.Service.MaintainInBackgroundAsync(fixture.Session);
        Assert.Equal(2, fixture.Handler.ExternalCalls);
        Assert.Equal(1, fixture.Handler.Logins);
        Assert.Equal(finalOnline, result.Payload.InternetConnected);
        Assert.Equal(finalOnline, string.IsNullOrEmpty(result.Payload.OperationError));
        Assert.Equal(finalOnline, fixture.Student.LoadNetworkAuthHistory().Records.Last().Success);
    }

    [Theory]
    [InlineData("logout")]
    [InlineData("disable")]
    [InlineData("provider")]
    [InlineData("portal")]
    [InlineData("credentials")]
    [InlineData("active-session")]
    public async Task ChangedContextDuringDetection_CannotSendCredentials(string change)
    {
        using var fixture = new Fixture();
        fixture.Handler.HoldFirstProbe = true;
        var pending = fixture.Service.MaintainInBackgroundAsync(fixture.Session);
        await fixture.Handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        if (change == "logout") fixture.Root.Sessions().Remove(fixture.Session);
        else if (change == "active-session") fixture.ActiveSession = "another-active-session";
        else if (change == "credentials") fixture.Student.NetworkCredentials().Save(new StoredNetworkCredentialDocument { Provider = "campus", Account = "changed-synthetic-user", Password = "changed-synthetic-secret" });
        else fixture.Student.SavePreferences(new AppSettingsDocument { Network = new NetworkSettingsDocument
        {
            HeartbeatEnabled = change != "disable", Provider = change == "provider" ? "telecom" : "campus",
            PortalUrl = change == "portal" ? "http://192.0.2.9/" : fixture.Settings.PortalUrl,
            ConnectivityCheckUrls = fixture.Settings.ConnectivityCheckUrls,
        } });
        fixture.Handler.Release.TrySetResult();
        if (change == "logout") await Assert.ThrowsAsync<AuthException>(() => pending);
        else Assert.Equal(409, (await Assert.ThrowsAsync<NetworkException>(() => pending)).StatusCode);
        Assert.Equal(0, fixture.Handler.Logins);
        Assert.Empty(fixture.Student.LoadNetworkAuthHistory().Records);
    }

    [Fact]
    public async Task LogoutAfterLoginWasSent_StopsFurtherProbesAndNeverRecordsSuccess()
    {
        using var fixture = new Fixture();
        fixture.Handler.HoldLogin = true;
        var pending = fixture.Service.MaintainInBackgroundAsync(fixture.Session);
        await fixture.Handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        fixture.Root.Sessions().Remove(fixture.Session); fixture.Handler.Release.TrySetResult();
        await Assert.ThrowsAsync<AuthException>(() => pending);
        Assert.Equal(1, fixture.Handler.Logins); // An already sent request cannot be recalled.
        Assert.Equal(1, fixture.Handler.ExternalCalls);
        Assert.Empty(fixture.Student.LoadNetworkAuthHistory().Records);
    }

    [Fact]
    public async Task DisablingBackgroundMaintenance_DoesNotDisableAnExplicitManualConnection()
    {
        using var fixture = new Fixture();
        fixture.Student.SavePreferences(new AppSettingsDocument { Network = new NetworkSettingsDocument { HeartbeatEnabled = false, PortalUrl = fixture.Settings.PortalUrl, ConnectivityCheckUrls = fixture.Settings.ConnectivityCheckUrls } });
        Assert.Equal(409, (await Assert.ThrowsAsync<NetworkException>(() => fixture.Service.MaintainInBackgroundAsync(fixture.Session))).StatusCode);
        Assert.Equal(0, fixture.Handler.ExternalCalls);
        Assert.True((await fixture.Service.MaintainAsync(fixture.Session, new NetworkActionRequest())).Payload.InternetConnected);
        Assert.Equal(1, fixture.Handler.Logins);
    }

    [Fact]
    public async Task ManualAndBackgroundMaintenance_SerializeAndAvoidDuplicateLogin()
    {
        using var fixture = new Fixture();
        fixture.Handler.HoldFirstProbe = true;
        var first = fixture.Service.MaintainInBackgroundAsync(fixture.Session);
        await fixture.Handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var second = fixture.Service.MaintainAsync(fixture.Session, new NetworkActionRequest());
        Assert.False(second.IsCompleted);
        fixture.Handler.Release.TrySetResult();
        await Task.WhenAll(first, second);
        Assert.Equal(1, fixture.Handler.Logins);
    }

    [Fact]
    public async Task CallerCancellation_StopsBeforeAuthenticationAndReleasesTheMaintenanceGate()
    {
        using var fixture = new Fixture();
        fixture.Handler.HoldFirstProbe = true;
        using var stop = new CancellationTokenSource();
        var pending = fixture.Service.MaintainInBackgroundAsync(fixture.Session, stop.Token);
        await fixture.Handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(0, fixture.Handler.Logins);
        fixture.Handler.Release.TrySetResult();
        var next = await fixture.Service.MaintainAsync(fixture.Session, new NetworkActionRequest());
        Assert.True(next.Payload.InternetConnected);
        Assert.Equal(1, fixture.Handler.Logins);
    }

    [Fact]
    public void DefaultTransport_DisablesProxyAndRedirects()
    {
        using var handler = NetworkHttpTransport.CreateHandler();
        Assert.False(handler.UseProxy);
        Assert.False(handler.AllowAutoRedirect);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TemporaryDirectory _data = new();
        private readonly HttpClient _http;
        public XkeDataRoot Root { get; }
        public XkeStudentStore Student { get; }
        public string Session { get; }
        public string? ActiveSession { get; set; }
        public NetworkSettingsDocument Settings { get; } = new() { HeartbeatEnabled = true, PortalUrl = "http://192.0.2.1/", ConnectivityCheckUrls = ["https://connectivity.invalid/check"] };
        public ProbeHandler Handler { get; } = new();
        public NetworkService Service { get; }
        public Fixture()
        {
            Root = new XkeDataRoot(_data.RootPath); Student = Root.ForStudent("SYNTHETIC-STUDENT");
            Student.SavePreferences(new AppSettingsDocument { Network = Settings });
            Student.NetworkCredentials().Save(new StoredNetworkCredentialDocument { Provider = "campus", Account = "synthetic-campus", Password = "synthetic-secret" });
            Session = Root.Sessions().Create("synthetic-user", Student.StudentId, "auto").SessionId;
            _http = new HttpClient(Handler); ActiveSession = Session;
            Service = new NetworkService(Root, _http, new Commands(), () => ActiveSession);
        }
        public void Dispose() { Service.Dispose(); _http.Dispose(); _data.Dispose(); }
    }

    private sealed class Commands : INetworkCommandExecutor
    {
        public Task<NetworkCommandResult> RunAsync(string name, IReadOnlyList<string> args, TimeSpan timeout, CancellationToken token = default)
            => Task.FromResult(new NetworkCommandResult(0, "", ""));
    }

    private sealed class ProbeHandler : HttpMessageHandler
    {
        public int Logins, ExternalCalls;
        public bool HoldFirstProbe, HoldLogin;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Func<int, (HttpStatusCode Code, string Body, string? FinalUri)>? External;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri!.Host == "192.0.2.1")
            {
                if (request.RequestUri.AbsolutePath.Contains("drcom/login"))
                {
                    Interlocked.Increment(ref Logins);
                    if (HoldLogin) { Entered.TrySetResult(); await Release.Task.WaitAsync(token); }
                    return Reply(request, HttpStatusCode.OK, "{\"result\":1}");
                }
                return Reply(request, HttpStatusCode.OK, "<html>synthetic portal</html>");
            }
            var n = Interlocked.Increment(ref ExternalCalls);
            if (HoldFirstProbe && n == 1) { Entered.TrySetResult(); await Release.Task.WaitAsync(token); }
            var (code, body, uri) = External?.Invoke(n) ?? (Logins > 0 ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable, Logins > 0 ? "OK" : "unavailable", null);
            if (uri is not null) request = new HttpRequestMessage(HttpMethod.Get, uri);
            return Reply(request, code, body);
        }
        private static HttpResponseMessage Reply(HttpRequestMessage request, HttpStatusCode code, string body)
            => new(code) { RequestMessage = request, Content = new StringContent(body) };
    }
}
