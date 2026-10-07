using Xke.Core.Auth.Jw;
using Xke.Core.Schedule;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class DirectJwLoginProtocolTests
{
    [Fact]
    public void EncodeCredentials_InterleavesTheServerPaddingWithoutKeepingPlainValues()
    {
        var encoded = DirectJwLoginProtocol.EncodeCredentials("abcdef#12345678901234567890", "20260001", "secret");

        Assert.NotEqual("20260001%%%secret", encoded);
        Assert.StartsWith("2a0bc2def6", encoded, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("missing-separator")]
    [InlineData("#123")]
    public void EncodeCredentials_RejectsInvalidSeedMaterial(string seed)
    {
        var error = Assert.Throws<JwScheduleException>(() => DirectJwLoginProtocol.EncodeCredentials(seed, "20260001", "secret"));

        Assert.Equal(502, error.StatusCode);
        Assert.Contains("jw_login_seed", error.Message);
    }

    [Fact]
    public async Task BeginAsync_RetriesAnEmptyCaptchaResponseWithImageHeaders()
    {
        var handler = new CaptchaHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://jw.hnsoftedu.com/") };
        using var protocol = new DirectJwLoginProtocol("20260001", "secret", http);

        var image = await protocol.BeginAsync();

        Assert.StartsWith("data:image/jpeg;base64,", image, StringComparison.Ordinal);
        Assert.Equal(2, handler.CaptchaRequests);
        Assert.True(handler.ImageAcceptObserved);
        Assert.Equal("222.243.161.213", handler.LoginHost);
    }

    private sealed class CaptchaHandler : HttpMessageHandler
    {
        public int CaptchaRequests { get; private set; }
        public bool ImageAcceptObserved { get; private set; }
        public string LoginHost { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LoginHost = request.RequestUri?.Host ?? string.Empty;
            if (request.RequestUri?.AbsolutePath == "/")
            {
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("<html>login</html>"),
                });
            }

            if (request.RequestUri?.AbsolutePath == "/verifycode.servlet")
            {
                CaptchaRequests++;
                ImageAcceptObserved = request.Headers.Accept.Any(value => value.MediaType == "image/avif");
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = CaptchaRequests == 1
                        ? new ByteArrayContent([])
                        : new ByteArrayContent([1, 2, 3])
                        {
                            Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg") },
                        },
                });
            }

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        }
    }
}
