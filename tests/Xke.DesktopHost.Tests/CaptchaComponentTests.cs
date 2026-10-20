using System.IO;
using Xke.DesktopHost.Captcha;
using Xunit;

namespace Xke.DesktopHost.Tests;

public sealed class CaptchaComponentTests
{
    [Fact]
    public async Task MissingAssetsAreUnavailableWithoutLoadingTheNativeModel()
    {
        var path = Path.Combine(Path.GetTempPath(), "xke-missing-captcha-" + Guid.NewGuid().ToString("N"));
        var component = new DesktopCaptchaRecognizer(path);
        Assert.False(component.IsAvailable);
        await Assert.ThrowsAsync<InvalidOperationException>(() => component.RecognizeAsync("data:image/png;base64,YQ=="));
        Assert.False(Directory.Exists(path));
    }

    [Fact]
    public async Task CorruptedModelIsRejectedBeforeNativeInference()
    {
        var path = Path.Combine(Path.GetTempPath(), "xke-captcha-component-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(path, "ocr"));
        try
        {
            File.WriteAllText(Path.Combine(path, "ocr/common_old.onnx"), "synthetic-corrupt-model");
            File.WriteAllText(Path.Combine(path, "ocr/charset.json"), "[]");
            foreach (var name in new[] { "msvcp140.dll", "msvcp140_1.dll", "vcruntime140.dll", "vcruntime140_1.dll" }) File.WriteAllText(Path.Combine(path, name), "synthetic-only");
            var component = new DesktopCaptchaRecognizer(path);
            Assert.True(component.IsAvailable);
            await Assert.ThrowsAsync<InvalidDataException>(() => component.RecognizeAsync("data:image/png;base64,YQ=="));
        }
        finally
        {
            var resolved = Path.GetFullPath(path);
            if (Path.GetDirectoryName(resolved) != Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)
                || !Path.GetFileName(resolved).StartsWith("xke-captcha-component-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unsafe synthetic cleanup path");
            Directory.Delete(resolved, true);
        }
    }
}
