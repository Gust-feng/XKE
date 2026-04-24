using System.Text;
using Xke.Security;
using Xke.Storage;
using Xunit;

namespace Xke.SecurityStorage.Tests;

public sealed class SecurityStorageCompatibilityTests
{
    [Fact]
    public void LegacySecureJsonDetector_RecognizesValidEnvelope()
    {
        const string payload = """
            {
              "format": "xke-secure-json",
              "algorithm": "AES-256-GCM",
              "ciphertext_b64": "Y2lwaGVydGV4dA==",
              "nonce_b64": "bm9uY2U=",
              "tag_b64": "dGFn"
            }
            """;

        Assert.True(LegacySecureJsonDetector.IsSecureJsonEnvelope(payload));
    }

    [Fact]
    public void LegacySecureJsonDetector_RejectsPlainJsonPayload()
    {
        const string payload = """{ "hello": "world" }""";

        Assert.False(LegacySecureJsonDetector.IsSecureJsonEnvelope(payload));
    }

    [Fact]
    public void NoticeCanonicalJson_SortsObjectKeysDeterministically()
    {
        var canonical = Encoding.UTF8.GetString(NoticeCanonicalJson.Serialize(new
        {
            z = 3,
            a = 1,
            nested = new
            {
                beta = 2,
                alpha = 1,
            },
        }));

        Assert.Equal("{\"a\":1,\"nested\":{\"alpha\":1,\"beta\":2},\"z\":3}", canonical);
    }
}
