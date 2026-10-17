using System.Net;
using System.Text;
using Xke.Core.Auth.Jw;
using Xke.Core.Schedule;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class DdddOcrCaptchaRecognizerTests
{
    [Fact]
    public async Task RecognizeAsync_SendsOnlyImageAndPreservesCodeCase()
    {
        using var client = new HttpClient(new Handler(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("http://127.0.0.1:8866/recognize", request.RequestUri!.ToString());
            Assert.Equal("{\"image\":\"YQ==\"}", await request.Content!.ReadAsStringAsync(cancellationToken));
            return Response("{\"code\":\" aB12 \"}");
        }));
        Assert.Equal("aB12", await new DdddOcrCaptchaRecognizer(client, new Uri("http://127.0.0.1:8866/recognize"))
            .RecognizeAsync("data:image/png;base64,YQ=="));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"code\":\" \"}")]
    [InlineData("{\"code\":123}")]
    [InlineData("[]")]
    [InlineData("not-json")]
    public async Task RecognizeAsync_InvalidResponseIsProtocolError(string body)
    {
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Response(body))));
        var exception = await Assert.ThrowsAsync<JwScheduleException>(() =>
            new DdddOcrCaptchaRecognizer(client, new Uri("http://localhost/recognize"))
                .RecognizeAsync("data:image/jpeg;base64,YQ=="));
        Assert.Equal(502, exception.StatusCode);
    }

    [Fact]
    public async Task RecognizeAsync_FailedHttpResponseDoesNotExposeBody()
    {
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Response("private-response", HttpStatusCode.InternalServerError))));
        var exception = await Assert.ThrowsAsync<JwScheduleException>(() =>
            new DdddOcrCaptchaRecognizer(client, new Uri("http://localhost/recognize"))
                .RecognizeAsync("data:image/png;base64,YQ=="));
        Assert.DoesNotContain("private-response", exception.Message);
    }

    [Theory]
    [InlineData("YQ==")]
    [InlineData("data:text/plain;base64,YQ==")]
    [InlineData("data:image/png,YQ==")]
    public async Task RecognizeAsync_RejectsUnexpectedImageFormat(string image)
    {
        using var client = new HttpClient(new Handler((_, _) => throw new InvalidOperationException("HTTP should not run")));
        await Assert.ThrowsAsync<JwScheduleException>(() =>
            new DdddOcrCaptchaRecognizer(client, new Uri("http://localhost/recognize")).RecognizeAsync(image));
    }

    [Fact]
    public async Task RecognizeAsync_RequestCancellationPropagates()
    {
        using var client = new HttpClient(new Handler((_, token) => Task.FromCanceled<HttpResponseMessage>(token)));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new DdddOcrCaptchaRecognizer(client, new Uri("http://localhost/recognize"))
                .RecognizeAsync("data:image/png;base64,YQ==", cancellation.Token));
    }

    private static HttpResponseMessage Response(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handle(request, cancellationToken);
    }
}
