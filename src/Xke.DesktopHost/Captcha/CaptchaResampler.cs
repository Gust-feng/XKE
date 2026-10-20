namespace Xke.DesktopHost.Captcha;

// Pillow-compatible Lanczos-3 sampling, including per-pass byte rounding.
// Algorithm reference: Pillow 12.0.0 libImaging/Resample.c (Pillow/HPND license).
// Not a general-purpose image API; keep the license with this adapter.
internal static class CaptchaResampler
{
    private const int Precision = 22;
    private sealed record Kernel(int Start, int[] Weights);

    public static byte[] Resize(byte[] pixels, int width, int height, int targetWidth, int targetHeight)
    {
        if (width != targetWidth)
        {
            var kernels = Coefficients(width, targetWidth);
            var output = new byte[targetWidth * height * 4];
            for (var y = 0; y < height; y++)
                for (var x = 0; x < targetWidth; x++)
                    for (var c = 0; c < 4; c++)
                    {
                        var k = kernels[x];
                        long value = 1 << (Precision - 1);
                        for (var j = 0; j < k.Weights.Length; j++)
                            value += (long)pixels[(y * width + k.Start + j) * 4 + c] * k.Weights[j];
                        output[(y * targetWidth + x) * 4 + c] = Clip(value);
                    }
            pixels = output;
            width = targetWidth;
        }
        if (height != targetHeight)
        {
            var kernels = Coefficients(height, targetHeight);
            var output = new byte[width * targetHeight * 4];
            for (var y = 0; y < targetHeight; y++)
                for (var x = 0; x < width; x++)
                    for (var c = 0; c < 4; c++)
                    {
                        var k = kernels[y];
                        long value = 1 << (Precision - 1);
                        for (var j = 0; j < k.Weights.Length; j++)
                            value += (long)pixels[((k.Start + j) * width + x) * 4 + c] * k.Weights[j];
                        output[(y * width + x) * 4 + c] = Clip(value);
                    }
            pixels = output;
        }
        return pixels;
    }

    private static byte Clip(long value) => (byte)Math.Clamp(value >> Precision, 0, 255);

    private static Kernel[] Coefficients(int source, int target)
    {
        var scale = (double)source / target;
        var filterScale = Math.Max(1, scale);
        var support = 3 * filterScale;
        var result = new Kernel[target];
        for (var i = 0; i < target; i++)
        {
            var center = (i + .5) * scale;
            var start = Math.Max(0, (int)(center - support + .5));
            var end = Math.Min(source, (int)(center + support + .5));
            var values = new double[end - start];
            var sum = 0d;
            for (var j = 0; j < values.Length; j++)
            {
                var x = (j + start - center + .5) / filterScale;
                var weight = x is >= -3 and < 3 ? Sinc(x) * Sinc(x / 3) : 0;
                values[j] = weight;
                sum += weight;
            }
            var weights = new int[values.Length];
            for (var j = 0; j < weights.Length; j++)
            {
                var weight = values[j] / sum;
                weights[j] = (int)(weight * (1 << Precision) + (weight < 0 ? -.5 : .5));
            }
            result[i] = new Kernel(start, weights);
        }
        return result;
    }

    private static double Sinc(double x) => x == 0 ? 1 : Math.Sin(x * Math.PI) / (x * Math.PI);
}
