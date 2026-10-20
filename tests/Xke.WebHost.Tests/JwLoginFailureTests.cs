using System.Net;
using Xke.Core.Auth.Jw;
using Xke.Core.Schedule;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class JwLoginFailureTests
{
    // Minimal, sanitized fragments of the two observed school responses. No
    // request credentials, images, cookies or complete real pages are fixtures.
    [Theory]
    [InlineData("<font id='showMsg' color='red' size='2'>验证码错误!!</font>", JwLoginFailureKind.CaptchaRejected)]
    [InlineData("<font id='showMsg' color='red' size='2'>该帐号不存在或密码错误,请联系管理员!</font>", JwLoginFailureKind.CredentialsRejected)]
    [InlineData("<font id='showMsg'>\n 验证码错误 ！！ \n</font>", JwLoginFailureKind.CaptchaRejected)]
    [InlineData("<font id='showMsg'>该帐号不存在或密码错误，请联系管理员！</font>", JwLoginFailureKind.CredentialsRejected)]
    [InlineData("<font id='showMsg'>验&#35777;码错误&#33;&#33;</font>", JwLoginFailureKind.CaptchaRejected)]
    public async Task CompleteAsync_ClassifiesOnlyTheObservedErrorField(string html, JwLoginFailureKind expected)
    {
        using var http = new HttpClient(new ResponseHandler(html));
        using var protocol = new DirectJwLoginProtocol("test-user", "test-password", http);
        var error = await Assert.ThrowsAsync<JwLoginException>(() => protocol.CompleteAsync("1234"));
        Assert.Equal(expected, error.Kind);
        Assert.Equal(401, error.StatusCode); // Preserve the current API status contract.
    }

    [Theory]
    [InlineData("<html><script>const message='验证码错误!!';</script></html>")]
    [InlineData("<font id='showMsg'>验证码错误或账号密码错误</font>")]
    [InlineData("<font id='showMsg'>验证码错误次数过多，请稍后重试</font>")]
    [InlineData("<font id='showMsg'>账号已锁定</font>")]
    [InlineData("<font id='showMsg'></font><div>验证码错误!!</div>")]
    [InlineData("<html>unexpected login response</html>")]
    public async Task CompleteAsync_UnknownOrAmbiguousResponseNeverBecomesCaptchaFailure(string html)
    {
        using var http = new HttpClient(new ResponseHandler(html));
        using var protocol = new DirectJwLoginProtocol("test-user", "test-password", http);
        var error = await Assert.ThrowsAsync<JwLoginException>(() => protocol.CompleteAsync("1234"));
        Assert.Equal(JwLoginFailureKind.Unknown, error.Kind);
    }

    [Fact]
    public async Task CompleteAsync_DoesNotExposeEchoedCredentialsOrRawResponse()
    {
        const string html = "<font id='showMsg'>Unknown: sensitive-echo</font><input value='test-password'>";
        using var http = new HttpClient(new ResponseHandler(html));
        using var protocol = new DirectJwLoginProtocol("test-user", "test-password", http);
        var error = await Assert.ThrowsAsync<JwLoginException>(() => protocol.CompleteAsync("1234"));
        Assert.Equal(JwLoginFailureKind.Unknown, error.Kind);
        Assert.DoesNotContain("sensitive-echo", error.Message);
        Assert.DoesNotContain("test-password", error.Message);
        Assert.DoesNotContain("<font", error.Message);
    }

    [Fact]
    public async Task CompleteAsync_WorkspaceRedirectStillSucceedsWithoutParsingLoginHints()
    {
        using var http = new HttpClient(new ResponseHandler("<font id='showMsg'>验证码错误!!</font>", workspace: true));
        using var protocol = new DirectJwLoginProtocol("test-user", "test-password", http);
        await protocol.CompleteAsync("1234");
    }

    [Fact]
    public async Task CompleteAsync_HttpFailureIsNotMistakenForCaptchaFailure()
    {
        using var http = new HttpClient(new ResponseHandler("<font id='showMsg'>验证码错误!!</font>", status: HttpStatusCode.ServiceUnavailable));
        using var protocol = new DirectJwLoginProtocol("test-user", "test-password", http);
        var error = await Assert.ThrowsAsync<JwScheduleException>(() => protocol.CompleteAsync("1234"));
        Assert.Equal(502, error.StatusCode);
    }

    private sealed class ResponseHandler(string html, bool workspace = false, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.Query.Contains("flag=sess", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = request,
                    Content = new StringContent("test-padding#00000000000000000000"),
                });
            return Task.FromResult(new HttpResponseMessage(status)
            {
                RequestMessage = workspace
                    ? new HttpRequestMessage(HttpMethod.Get, "http://jw.example.test/jsxsd/framework/xsMainV.jsp")
                    : request,
                Content = new StringContent(html),
            });
        }
    }
}
