using System.IO;
using System.Security.Cryptography;
using Xke.Core.Auth.Jw;

namespace Xke.DesktopHost.Captcha;

/// <summary>Lazy, CPU-only, app-local OCR. Sessions live for one recognition and
/// are disposed afterwards; a missing/corrupt component never blocks manual login.</summary>
public sealed class DesktopCaptchaRecognizer : ICaptchaRecognizer
{
    private readonly string _root;
    private readonly string _model;
    private readonly string _charset;
    public DesktopCaptchaRecognizer(string? root = null)
    {
        _root = root ?? AppContext.BaseDirectory;
        _model = Path.Combine(_root, "ocr", "common_old.onnx");
        _charset = Path.Combine(_root, "ocr", "charset.json");
    }
    public bool IsAvailable => File.Exists(_model) && File.Exists(_charset)
        && new[] { "msvcp140.dll", "msvcp140_1.dll", "vcruntime140.dll", "vcruntime140_1.dll" }.All(name => File.Exists(Path.Combine(_root, name)));

    public async Task<string> RecognizeAsync(string image, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable) throw new InvalidOperationException("识别组件不可用");
        return await Task.Run(async () =>
        {
            Verify(_model, "B8F2AD9CBC1F2E3922A6CB9459E30824E7E2467F3FB4FD61420640E34EA0BF68");
            Verify(_charset, "342D871C34FD467420CD39B58A4624B505DA53114E77A69B43B48B8A3EE1B24D");
            cancellationToken.ThrowIfCancellationRequested();
            using var recognizer = new OnnxCaptchaRecognizer(_model, _charset);
            return await recognizer.RecognizeAsync(image, cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static void Verify(string file, string expected)
    {
        using var stream = File.OpenRead(file);
        if (Convert.ToHexString(SHA256.HashData(stream)) != expected) throw new InvalidDataException("识别组件校验失败");
    }
}
