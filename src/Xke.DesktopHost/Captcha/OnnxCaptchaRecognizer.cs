using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Xke.Core.Auth.Jw;

namespace Xke.DesktopHost.Captcha;

// Windows-only inference adapter; Core owns trust, retries and manual fallback.
public sealed class OnnxCaptchaRecognizer : ICaptchaRecognizer, IDisposable
{
    private readonly InferenceSession _session;
    private readonly string[] _charset;
    private readonly string _inputName;

    public OnnxCaptchaRecognizer(string modelPath, string charsetPath)
    {
        _charset = JsonSerializer.Deserialize<string[]>(File.ReadAllText(charsetPath))
            ?? throw new InvalidDataException("验证码字符表为空");
        if (_charset.Length != 8210 || _charset[0] != string.Empty)
            throw new InvalidDataException("字符表与默认验证码模型不匹配");
        using var options = new SessionOptions
        {
            IntraOpNumThreads = 1,
            InterOpNumThreads = 1,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR
        };
        options.AddSessionConfigEntry("session.intra_op.allow_spinning", "0");
        _session = new InferenceSession(modelPath, options);
        _inputName = _session.InputMetadata.Keys.Single();
    }

    public Task<string> RecognizeAsync(string captchaImageDataUrl, CancellationToken cancellationToken = default)
    {
        const string prefix = "data:image/";
        var comma = captchaImageDataUrl.IndexOf(',');
        if (!captchaImageDataUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || comma < 0
            || !captchaImageDataUrl[..comma].EndsWith(";base64", StringComparison.OrdinalIgnoreCase)
            || captchaImageDataUrl.Length > 2_800_000)
            throw new ArgumentException("验证码图片格式无效", nameof(captchaImageDataUrl));
        var bytes = Convert.FromBase64String(captchaImageDataUrl[(comma + 1)..]);
        return Task.Run(() => Recognize(CaptchaImage.Decode(bytes), cancellationToken), cancellationToken);
    }

    internal string Recognize(DenseTensor<float> input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var runOptions = new RunOptions();
        using var registration = cancellationToken.Register(() => runOptions.Terminate = true);
        try
        {
            using var output = _session.Run(new[] { NamedOnnxValue.CreateFromTensor(_inputName, input) },
                _session.OutputMetadata.Keys.ToArray(), runOptions);
            cancellationToken.ThrowIfCancellationRequested();
            return Decode(output.First().AsTensor<float>());
        }
        catch (OnnxRuntimeException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    internal string Decode(Tensor<float> output)
    {
        // The pinned model returns [time, 1, 8210] logits. Do not trust its stale
        // declared [1,seqlen] metadata, or filter/truncate an unexpected result.
        var shape = output.Dimensions;
        if (shape.Length != 3 || shape[1] != 1 || shape[2] != _charset.Length)
            throw new InvalidDataException("验证码模型输出结构不匹配");
        var text = new StringBuilder();
        var previous = -1;
        for (var step = 0; step < shape[0]; step++)
        {
            var index = 0;
            var max = output[step, 0, 0];
            for (var c = 1; c < _charset.Length; c++)
                if (output[step, 0, c] > max) { max = output[step, 0, c]; index = c; }
            if (index != previous && index != 0) text.Append(_charset[index]);
            previous = index;
        }
        return text.ToString();
    }

    public void Dispose() => _session.Dispose();
}
