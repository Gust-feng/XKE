using System.Net.Http.Json;
using System.Text.Json;
using Xke.Core.Schedule;

namespace Xke.Core.Auth.Jw;

/// <summary>Calls the private ddddocr sidecar; never sends account credentials.</summary>
public sealed class DdddOcrCaptchaRecognizer(HttpClient client, Uri endpoint) : ICaptchaRecognizer
{
    public async Task<string> RecognizeAsync(string captchaImageDataUrl, CancellationToken cancellationToken = default)
    {
        var comma = captchaImageDataUrl.IndexOf(',');
        if (comma < 0 || !captchaImageDataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
            || !captchaImageDataUrl[..comma].EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
            throw new JwScheduleException("验证码图片格式无效。阶段=captcha_ocr", 502);

        try
        {
            using var response = await client.PostAsJsonAsync(endpoint,
                new { image = captchaImageDataUrl[(comma + 1)..] }, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new JwScheduleException("验证码识别服务请求失败。阶段=captcha_ocr", 502);

            using var result = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var code = result.RootElement.ValueKind == JsonValueKind.Object
                && result.RootElement.TryGetProperty("code", out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()?.Trim() : null;
            if (string.IsNullOrWhiteSpace(code))
                throw new JwScheduleException("验证码识别服务未返回结果。阶段=captcha_ocr", 502);
            return code;
        }
        catch (HttpRequestException)
        {
            throw new JwScheduleException("无法连接验证码识别服务。阶段=captcha_ocr", 502);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new JwScheduleException("验证码识别服务超时。阶段=captcha_ocr", 504);
        }
        catch (JsonException)
        {
            throw new JwScheduleException("验证码识别服务响应格式无效。阶段=captcha_ocr", 502);
        }
    }
}
