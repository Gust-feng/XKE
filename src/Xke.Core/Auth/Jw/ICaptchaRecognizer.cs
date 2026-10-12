namespace Xke.Core.Auth.Jw;

public interface ICaptchaRecognizer
{
    Task<string> RecognizeAsync(string captchaImageDataUrl, CancellationToken cancellationToken = default);
}
