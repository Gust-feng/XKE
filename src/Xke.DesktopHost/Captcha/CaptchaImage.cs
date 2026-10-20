using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Xke.DesktopHost.Captcha;

internal static class CaptchaImage
{
    public static DenseTensor<float> Decode(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        var image = BitmapDecoder.Create(stream,
            BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile,
            BitmapCacheOption.OnLoad).Frames[0];
        if (image.PixelWidth > 2048 || image.PixelHeight > 2048)
            throw new ArgumentException("验证码图片尺寸过大");
        var width = Math.Max(1, (int)(image.PixelWidth * (64d / image.PixelHeight)));
        if (width > 2048) throw new ArgumentException("验证码图片宽高比异常");
        var color = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[image.PixelWidth * image.PixelHeight * 4];
        color.CopyPixels(pixels, image.PixelWidth * 4, 0);
        for (var i = 3; i < pixels.Length; i += 4)
            if (pixels[i] != 255) throw new NotSupportedException("透明验证码尚未通过本地模型验证，请人工识别");
        // Match Pillow RGB Lanczos -> grayscale -> float32 [0,1], in that order.
        pixels = CaptchaResampler.Resize(pixels, image.PixelWidth, image.PixelHeight, width, 64);
        var tensor = new DenseTensor<float>(new[] { 1, 1, 64, width });
        for (var i = 0; i < width * 64; i++)
        {
            var offset = i * 4;
            var gray = (19595 * pixels[offset + 2] + 38470 * pixels[offset + 1]
                + 7471 * pixels[offset] + 32768) >> 16;
            tensor.Buffer.Span[i] = gray / 255f;
        }
        return tensor;
    }
}
