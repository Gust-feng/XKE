using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xke.Core.Schedule;
using Xke.Security;
using Xunit;

namespace Xke.WebHost.Tests;

public sealed class ScheduleApiTransportTests
{
    [Fact]
    public void ConfiguredPin_TrustsOnlyMatchingValidCertificateAndName()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        var fingerprint = certificate.GetCertHashString(HashAlgorithmName.SHA256);
        using var handler = ScheduleApiTransport.CreateHandler(new ScheduleApiOptions { CertificateSha256 = fingerprint });
        using var message = new HttpRequestMessage(HttpMethod.Get, "https://localhost/schedule");
        var validate = handler.ServerCertificateCustomValidationCallback!;
        Assert.True(validate(message, certificate, null, SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.False(validate(message, certificate, null, SslPolicyErrors.RemoteCertificateNameMismatch));
        Assert.False(validate(message, null, null, SslPolicyErrors.RemoteCertificateNotAvailable));
        using var wrongHandler = ScheduleApiTransport.CreateHandler(new ScheduleApiOptions { CertificateSha256 = new string('0', 64) });
        Assert.False(wrongHandler.ServerCertificateCustomValidationCallback!(message, certificate, null, SslPolicyErrors.None));
        using var invalidHandler = ScheduleApiTransport.CreateHandler(new ScheduleApiOptions { CertificateSha256 = "invalid" });
        Assert.False(invalidHandler.ServerCertificateCustomValidationCallback!(message, certificate, null, SslPolicyErrors.None));
        using var insecureMessage = new HttpRequestMessage(HttpMethod.Get, "http://localhost/schedule");
        Assert.False(validate(insecureMessage, certificate, null, SslPolicyErrors.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MatchingPin_DoesNotAcceptExpiredOrNotYetValidCertificate(bool future)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var start = DateTimeOffset.UtcNow.AddDays(future ? 1 : -2);
        using var certificate = request.CreateSelfSigned(start, start.AddDays(1));
        using var handler = ScheduleApiTransport.CreateHandler(new ScheduleApiOptions { CertificateSha256 = certificate.GetCertHashString(HashAlgorithmName.SHA256) });
        using var message = new HttpRequestMessage(HttpMethod.Get, "https://localhost/schedule");
        Assert.False(handler.ServerCertificateCustomValidationCallback!(message, certificate, null, SslPolicyErrors.RemoteCertificateChainErrors));
    }

    [Fact]
    public void UnpinnedTransport_UsesSystemValidationAndDisablesRedirectsAndCookies()
    {
        using var handler = ScheduleApiTransport.CreateHandler(new ScheduleApiOptions());
        Assert.Null(handler.ServerCertificateCustomValidationCallback);
        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseCookies);
        Assert.True(ScheduleApiTransport.IsValidFingerprint(string.Join(":", Enumerable.Repeat("ab", 32))));
        Assert.False(ScheduleApiTransport.IsValidFingerprint(new string('g', 64)));
    }

    [Fact]
    public async Task PrivateConfiguration_LoadsProtectedTokenAndUsesHeaderWithoutExposingIt()
    {
        using var temp = new TemporaryDirectory();
        var directory = Path.Combine(temp.RootPath, "开发数据", "课表服务");
        Directory.CreateDirectory(directory);
        new ProtectedJsonFileStore().Write(Path.Combine(directory, "token.dpapi"), "synthetic-api-secret");
        File.WriteAllText(Path.Combine(directory, "client.local.json"), """
            {"endpoint":"https://example.test/schedule","student_id":"20260001","protected_token_file":"token.dpapi","max_age_hours":192}
            """);
        var options = ScheduleApiOptions.Load(temp.RootPath);
        Assert.False(options.InvalidConfiguration);
        Assert.Equal(192, options.MaxAgeHours);
        Assert.Equal("synthetic-api-secret", options.ApiKey);
        Assert.DoesNotContain("synthetic-api-secret", System.Text.Json.JsonSerializer.Serialize(options));
        var handler = new PersonalScheduleApiClientTests.SnapshotHandler();
        var client = new PersonalScheduleApiClient(new HttpClient(handler), options, new PersonalScheduleApiClientTests.SnapshotClock());
        var result = await client.FetchAsync("20260001", new Xke.Contracts.Requests.ScheduleRequest(), default);
        Assert.NotNull(result.Schedule);
        Assert.Equal("synthetic-api-secret", handler.ApiKey);
        Assert.Null(handler.Authorization);
    }

    [Fact]
    public void MissingProtectedTokenOrMalformedPin_DisablesOnlyOptionalSource()
    {
        using var temp = new TemporaryDirectory();
        var directory = Path.Combine(temp.RootPath, "开发数据", "课表服务");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "client.local.json"), """
            {"endpoint":"https://example.test/schedule","protected_token_file":"missing.dpapi"}
            """);
        Assert.True(ScheduleApiOptions.Load(temp.RootPath).InvalidConfiguration);
        File.WriteAllText(Path.Combine(directory, "client.local.json"), """
            {"endpoint":"https://example.test/schedule","certificate_sha256":"broken-pin"}
            """);
        Assert.True(ScheduleApiOptions.Load(temp.RootPath).InvalidConfiguration);
    }
}
