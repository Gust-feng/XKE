using System.Net;
using System.Net.Http.Json;
using Xke.Contracts.Requests;
using Xke.WebHost;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class AuthProtocolWebHostTests
{
    [Fact]
    public async Task DefaultProtocolEndpoints_RejectInvalidLoginAndUnknownChallengeWithoutRemoteCalls()
    {
        using var dataRoot = new TemporaryDirectory();
        using var webRoot = new TemporaryWebRoot();
        await using var service = new XkeWebHostService(new XkeWebHostOptions
        {
            LocalOnly = true,
            AuthDataRootPath = dataRoot.RootPath,
            WebRootPath = webRoot.RootPath,
            Urls = new[] { "http://127.0.0.1:0" },
        });
        var launch = await service.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(launch.BaseUrl) };

        using var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            User = string.Empty,
            Password = "test-password",
        });
        Assert.Equal(HttpStatusCode.BadRequest, login.StatusCode);

        using var captcha = await client.PostAsJsonAsync("/api/auth/captcha", new JwCaptchaSubmitRequest
        {
            ChallengeId = "unknown-challenge",
            CaptchaCode = "1234",
        });
        Assert.Equal(HttpStatusCode.Gone, captcha.StatusCode);
        Assert.Empty(Directory.EnumerateFiles(dataRoot.RootPath, "*.json.dpapi", SearchOption.AllDirectories));
    }
}
