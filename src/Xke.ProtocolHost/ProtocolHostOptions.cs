namespace Xke.ProtocolHost;

public sealed class ProtocolHostOptions
{
    public required string ApiKey { get; init; }
    public required string DataRootPath { get; init; }
    public required Uri DdddOcrEndpoint { get; init; }
    public IReadOnlyList<string> Urls { get; init; } = ["http://127.0.0.1:38444"];

    public static ProtocolHostOptions FromEnvironment()
    {
        static string Required(string name) => Environment.GetEnvironmentVariable(name) is { } value && !string.IsNullOrWhiteSpace(value)
            ? value : throw new InvalidOperationException($"必须配置环境变量 {name}。");

        return new ProtocolHostOptions
        {
            ApiKey = Required("XKE_PROTOCOL_API_KEY"),
            DataRootPath = Required("XKE_PROTOCOL_DATA_DIR"),
            DdddOcrEndpoint = new Uri(Required("XKE_DDDDOCR_URL"), UriKind.Absolute),
            Urls = Environment.GetEnvironmentVariable("XKE_PROTOCOL_URLS") is { Length: > 0 } urls
                ? urls.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                : ["http://127.0.0.1:38444"],
        };
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new InvalidOperationException("XKE_PROTOCOL_API_KEY 不能为空。");
        if (string.IsNullOrWhiteSpace(DataRootPath) || !Path.IsPathFullyQualified(DataRootPath))
            throw new InvalidOperationException("XKE_PROTOCOL_DATA_DIR 必须是独立协议服务的绝对数据路径。");
        if (!DdddOcrEndpoint.IsAbsoluteUri || DdddOcrEndpoint.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("XKE_DDDDOCR_URL 必须是完整的 HTTP/HTTPS 验证码识别地址。");
        if (Urls.Count == 0)
            throw new InvalidOperationException("协议服务至少需要一个监听地址。");
    }
}
