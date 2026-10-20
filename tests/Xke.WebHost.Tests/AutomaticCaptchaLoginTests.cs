using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xke.Contracts.Models;
using Xke.Contracts.Requests;
using Xke.Core.Auth;
using Xke.Core.Auth.Jw;
using Xke.Core.Schedule;
using Xke.Storage;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class AutomaticCaptchaLoginTests
{
    [Theory]
    [InlineData(false, true, "secret")]
    [InlineData(true, false, "secret")]
    [InlineData(true, true, "changed-password")]
    public void TrustPreferenceAndExactSavedPasswordAreAllRequired(bool trusted, bool enabled, string password)
    {
        using var f = new Fixture(trusted, enabled);
        Assert.False(f.Login.CanUse("synthetic", password));
        Assert.Equal(0, f.Recognizer.Calls);
    }

    [Fact]
    public async Task OnlyCaptchaErrorsRetry_ThreeTotalThenManualWithoutFurtherBackgroundAttempts()
    {
        using var f = new Fixture();
        var handler = new TraditionalJwClientTests.TraditionalJwFakeHandler { DirectFailuresBeforeSuccess = 3 };
        var native = f.Native(handler);
        var error = await Assert.ThrowsAsync<JwScheduleException>(() => native.FetchExistingSessionAsync("synthetic", "secret", "auto", new(), f.Session, default));
        Assert.Equal(401, error.StatusCode);
        Assert.Equal(3, handler.DirectLoginPosts);
        Assert.Equal(3, f.Recognizer.Calls);
        Assert.Equal("manual_required", f.Login.GetStatus("synthetic").State);
        Assert.Equal(3, f.Login.GetStatus("synthetic").Attempt);
        var before = handler.Requests.Count;
        await Assert.ThrowsAsync<JwScheduleException>(() => native.FetchExistingSessionAsync("synthetic", "secret", "auto", new(), f.Session, default));
        Assert.Equal(before, handler.Requests.Count);
    }

    [Fact]
    public async Task AQueuedRenewalCannotEraseTheThreeFailureManualState()
    {
        using var f = new Fixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Recognizer.Run = async token => { entered.TrySetResult(); await release.Task.WaitAsync(token); return "2468"; };
        var handler = new TraditionalJwClientTests.TraditionalJwFakeHandler { DirectFailuresBeforeSuccess = 3 };
        var first = f.Native(handler).FetchExistingSessionAsync("synthetic", "secret", "auto", new(), f.Session, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var queued = f.Native(handler).FetchExistingSessionAsync("synthetic", "secret", "auto", new(), f.Session, default);
        release.SetResult();
        await Assert.ThrowsAsync<JwScheduleException>(() => first);
        await Assert.ThrowsAsync<JwScheduleException>(() => queued);
        Assert.Equal(3, handler.DirectLoginPosts); Assert.Equal(3, f.Login.GetStatus("synthetic").Attempt);
        Assert.Equal("manual_required", f.Login.GetStatus("synthetic").State);
    }

    [Theory]
    [InlineData("该帐号不存在或密码错误,请联系管理员!", JwLoginFailureKind.CredentialsRejected)]
    [InlineData("账号已锁定", JwLoginFailureKind.Unknown)]
    [InlineData("验证码或密码错误", JwLoginFailureKind.Unknown)]
    public async Task CredentialsOrUnknownErrorStopAfterOneSubmission(string message, JwLoginFailureKind kind)
    {
        using var f = new Fixture();
        var handler = new TraditionalJwClientTests.TraditionalJwFakeHandler { DirectFailuresBeforeSuccess = 9, DirectFailureMessage = message };
        var error = await Assert.ThrowsAsync<JwLoginException>(() => f.Native(handler).FetchExistingSessionAsync("synthetic", "secret", "auto", new(), f.Session, default));
        Assert.Equal(kind, error.Kind);
        Assert.Equal(1, handler.DirectLoginPosts);
        Assert.Equal(1, f.Recognizer.Calls);
    }

    [Fact]
    public async Task BackgroundRenewalAndSubsequentCookieReuseShareTheSamePath()
    {
        using var f = new Fixture();
        var handler = new TraditionalJwClientTests.TraditionalJwFakeHandler();
        var native = f.Native(handler);
        var first = await native.FetchExistingSessionAsync("synthetic", "secret", "auto", new() { CookieRq = "2026-04-20" }, f.Session, default);
        var second = await native.FetchExistingSessionAsync("synthetic", "secret", "auto", new() { CookieRq = "2026-04-20" }, f.Session, default);
        Assert.Equal("jw_schedule", first.SourceKey);
        Assert.Equal("jw_schedule", second.SourceKey);
        Assert.Equal(1, handler.DirectLoginPosts);
        Assert.Equal(1, f.Recognizer.Calls);
        Assert.True(f.Runtime.TryGet(f.Session, out _));
    }

    [Fact]
    public async Task ThreeFailuresReturnAFreshManualChallenge_ThenManualCanSucceed()
    {
        using var f = new Fixture();
        var handler = new TraditionalJwClientTests.TraditionalJwFakeHandler { DirectFailuresBeforeSuccess = 3 };
        var challenge = await Assert.ThrowsAsync<JwCaptchaRequiredException>(() => f.Native(handler).FetchCookieAsync("synthetic", "secret", "auto", new() { CookieRq = "2026-04-20" }, sessionId: f.Session));
        Assert.Equal(3, handler.DirectLoginPosts);
        Assert.Equal(4, handler.CaptchaRequests); // Fresh GET, not a fourth automated submission.
        Assert.Contains("三次", challenge.Message);
        var result = await challenge.ContinueAsync!("2468", default);
        Assert.Equal("jw_schedule", result.SourceKey);
        Assert.Equal(4, handler.DirectLoginPosts); // This one is explicitly manual.
        Assert.Equal("idle", f.Login.GetStatus("synthetic").State);
    }

    [Fact]
    public async Task ClosingTheSwitchDuringRecognitionPreventsAnyLoginSubmission()
    {
        using var f = new Fixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Recognizer.Run = async token => { entered.SetResult(); await Task.Delay(Timeout.Infinite, token); return "2468"; };
        var handler = new TraditionalJwClientTests.TraditionalJwFakeHandler();
        var task = f.Native(handler).FetchExistingSessionAsync("synthetic", "secret", "auto", new(), f.Session, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        f.Student.SavePreferences(new() { Auth = new() { AutoCaptchaEnabled = false } });
        f.Login.Cancel("synthetic");
        await Assert.ThrowsAsync<JwScheduleException>(() => task);
        Assert.Equal(0, handler.DirectLoginPosts);
        Assert.False(f.Login.CanUse("synthetic", "secret"));
    }

    [Fact]
    public async Task RecognizerFailureFallsBackWithoutSubmittingOrRetryingTheComponent()
    {
        using var f = new Fixture();
        f.Recognizer.Run = _ => throw new InvalidOperationException("synthetic missing native library");
        var handler = new TraditionalJwClientTests.TraditionalJwFakeHandler();
        await Assert.ThrowsAsync<JwCaptchaRequiredException>(() => f.Native(handler).FetchCookieAsync("synthetic", "secret", "auto", new(), sessionId: f.Session));
        Assert.Equal(0, handler.DirectLoginPosts);
        Assert.Equal(1, f.Recognizer.Calls);
        Assert.DoesNotContain("synthetic missing", f.Login.GetStatus("synthetic").Message);
    }

    [Fact]
    public async Task ARejectedManualCodeReturnsANewUsableChallengeWithoutAutomaticAttempts()
    {
        using var f = new Fixture(enabled: false);
        f.Student.SaveProfile(new StudentProfileDocument { StudentId = f.Student.StudentId, Name = "Synthetic" });
        var handler = new TraditionalJwClientTests.TraditionalJwFakeHandler { DirectFailuresBeforeSuccess = 1 };
        using var service = new ScheduleService(f.Root, f.Native(handler), new(), new ManualCaptchaSolver());
        var first = await service.FetchCookieAsync(f.Session, new() { CookieRq = "2026-04-20" });
        var oldId = first.Payload["challenge_id"]!.ToString()!;
        var retry = await service.SubmitJwCaptchaAsync(f.Session, new() { ChallengeId = oldId, CaptchaCode = "bad" });
        var newId = retry.Payload["challenge_id"]!.ToString()!;
        Assert.NotEqual(oldId, newId); Assert.Equal(0, f.Recognizer.Calls);
        var done = await service.SubmitJwCaptchaAsync(f.Session, new() { ChallengeId = newId, CaptchaCode = "2468" });
        Assert.True(done.Payload.ContainsKey("rows")); Assert.Equal(2, handler.DirectLoginPosts);
    }

    [Fact]
    public async Task SessionRemovalDuringRecognitionPreventsPostingOrSavingAnOldCookie()
    {
        using var f = new Fixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Recognizer.Run = async token => { entered.SetResult(); await release.Task.WaitAsync(token); return "2468"; };
        var handler = new TraditionalJwClientTests.TraditionalJwFakeHandler();
        var task = f.Native(handler).FetchExistingSessionAsync("synthetic", "secret", "auto", new(), f.Session, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); f.Root.Sessions().Remove(f.Session); release.SetResult();
        var error = await Assert.ThrowsAsync<JwScheduleException>(() => task);
        Assert.Equal(409, error.StatusCode); Assert.Equal(0, handler.DirectLoginPosts);
        Assert.Null(f.Root.JwSessions().Load("synthetic"));
    }

    [Fact]
    public async Task FirstLoginStaysManual_ThenDefaultsOn_ExplicitOffSurvivesReauthentication()
    {
        using var data = new TemporaryDirectory(); using var web = new TemporaryWebRoot();
        var root = new XkeDataRoot(data.RootPath);
        var recognizer = new Recognizer(); var handler = new ProtocolHandler();
        await using var host = new Xke.WebHost.XkeWebHostService(new Xke.WebHost.XkeWebHostOptions {
            AuthDataRootPath = data.RootPath, WebRootPath = web.RootPath, Urls = ["http://127.0.0.1:0"], EnableBackgroundTasks = false,
            CaptchaRecognizer = recognizer, JwScheduleClient = new DefaultFakeJwScheduleClient(),
            JwLoginProtocolFactory = (user, password) => new(user, password, new HttpClient(handler, false)),
        });
        var launch = await host.StartAsync(); using var http = new HttpClient { BaseAddress = new(launch.BaseUrl) };
        async Task<JsonDocument> Post(string path, object body) { using var response = await http.PostAsJsonAsync(path, body); response.EnsureSuccessStatusCode(); return JsonDocument.Parse(await response.Content.ReadAsStringAsync()); }
        using var start = await Post("/api/auth/login", new { user = "20260001", password = "secret" });
        var challenge = start.RootElement.GetProperty("data");
        Assert.Equal("captcha_required", challenge.GetProperty("status").GetString()); Assert.Equal(0, recognizer.Calls);
        using var manual = await Post("/api/auth/captcha", new { challenge_id = challenge.GetProperty("challenge_id").GetString(), captcha_code = "1234" });
        var sid = manual.RootElement.GetProperty("data").GetProperty("session_id").GetString()!;
        Assert.True(root.ForStudent("20260001").LoadPreferences()!.Auth.AutoCaptchaEnabled);
        Assert.False(string.IsNullOrEmpty(root.Credentials().Load("20260001")!.TrustedLoginToken)); Assert.Equal(0, recognizer.Calls);
        using var automatic = await Post("/api/auth/reinitialize", new { user = "20260001", password = "secret" });
        Assert.True(automatic.RootElement.GetProperty("data").TryGetProperty("session_id", out var next)); Assert.Equal(1, recognizer.Calls);
        http.DefaultRequestHeaders.Add("X-XKE-Session", next.GetString());
        using var off = await Post("/api/auth/preferences", new { auth = new { auto_captcha_enabled = false } });
        using var reconnect = await Post("/api/auth/reinitialize", new { user = "20260001", password = "secret" });
        Assert.Equal("captcha_required", reconnect.RootElement.GetProperty("data").GetProperty("status").GetString()); Assert.Equal(1, recognizer.Calls);
        using var disabledManual = await Post("/api/auth/captcha", new { challenge_id = reconnect.RootElement.GetProperty("data").GetProperty("challenge_id").GetString(), captcha_code = "1234" });
        Assert.False(root.ForStudent("20260001").LoadPreferences()!.Auth.AutoCaptchaEnabled);
        Assert.True(disabledManual.RootElement.GetProperty("data").TryGetProperty("session_id", out _));
    }

    [Fact]
    public async Task ASecondChallengeDuringInitializationDoesNotCommitATrustedWorkspace()
    {
        using var data = new TemporaryDirectory(); using var web = new TemporaryWebRoot();
        var root = new XkeDataRoot(data.RootPath); var handler = new ProtocolHandler();
        await using var host = new Xke.WebHost.XkeWebHostService(new Xke.WebHost.XkeWebHostOptions {
            AuthDataRootPath = data.RootPath, WebRootPath = web.RootPath, Urls = ["http://127.0.0.1:0"], EnableBackgroundTasks = false,
            CaptchaRecognizer = new Recognizer(),
            JwScheduleClient = new DefaultFakeJwScheduleClient { CookieException = new JwCaptchaRequiredException("captcha", "data:image/png;base64,YQ==", 120) },
            JwLoginProtocolFactory = (user, password) => new(user, password, new HttpClient(handler, false)),
        });
        var launch = await host.StartAsync(); using var http = new HttpClient { BaseAddress = new(launch.BaseUrl) };
        using var start = await http.PostAsJsonAsync("/api/auth/login", new { user = "20260001", password = "secret" });
        using var json = JsonDocument.Parse(await start.Content.ReadAsStringAsync());
        using var submit = await http.PostAsJsonAsync("/api/auth/captcha", new { challenge_id = json.RootElement.GetProperty("data").GetProperty("challenge_id").GetString(), captcha_code = "1234" });
        Assert.Equal(HttpStatusCode.Conflict, submit.StatusCode); Assert.Null(root.Credentials().Load("20260001")); Assert.Equal(0, root.Sessions().Count());
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TemporaryDirectory _temp = new();
        public XkeDataRoot Root { get; } public XkeStudentStore Student { get; } public string Session { get; }
        public Recognizer Recognizer { get; } = new(); public AutomaticCaptchaLogin Login { get; }
        public TraditionalJwSessionRuntimeStore Runtime { get; } = new();
        public Fixture(bool trusted = true, bool enabled = true)
        {
            Root = new(_temp.RootPath); Student = Root.ForStudent("SYNTHETIC");
            Student.SavePreferences(new() { Auth = new() { AutoCaptchaEnabled = enabled } });
            Root.Credentials().Save(new() { User = "synthetic", Password = "secret", StudentId = Student.StudentId, TrustedLoginToken = trusted ? "synthetic-trust" : "" });
            Session = Root.Sessions().Create("synthetic", Student.StudentId, "auto").SessionId;
            Login = new(Root, Recognizer);
        }
        public NativeJwScheduleClient Native(HttpMessageHandler handler) => new(new TraditionalJwScheduleHtmlParser(), Runtime,
            (user, password, mode) => new TraditionalJwClient(user, password, mode, TraditionalJwOptions.FromDataRoot(Root), new HttpClient(handler, false)),
            (user, password) => new DirectJwLoginProtocol(user, password, new HttpClient(handler, false)), Root, Login);
        public void Dispose() => _temp.Dispose();
    }
    private sealed class Recognizer : ICaptchaRecognizer
    {
        public int Calls; public Func<CancellationToken, Task<string>>? Run;
        public Task<string> RecognizeAsync(string image, CancellationToken cancellationToken = default) { Calls++; return Run?.Invoke(cancellationToken) ?? Task.FromResult("2468"); }
    }
}
