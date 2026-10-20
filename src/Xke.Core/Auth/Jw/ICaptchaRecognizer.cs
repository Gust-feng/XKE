namespace Xke.Core.Auth.Jw;

public interface ICaptchaRecognizer
{
    bool IsAvailable => true;

    Task<string> RecognizeAsync(string captchaImageDataUrl, CancellationToken cancellationToken = default);
}
