using Xke.Contracts.Models;

namespace Xke.Core.Auth.Jw;

public interface IJwAuthClient
{
    Task<JwAuthResult> InitializeAsync(
        string user,
        string password,
        string preloginMode,
        CancellationToken cancellationToken = default,
        MobileJwEncryptedCredentialDocument? mobileJw = null);

    Task<JwAuthResult> ConnectAsync(
        string user,
        string password,
        string preloginMode,
        CancellationToken cancellationToken = default,
        MobileJwEncryptedCredentialDocument? mobileJw = null);
}

public sealed record JwAuthResult(
    StudentProfileDocument Profile,
    string MobileToken = "",
    MobileJwEncryptedCredentialDocument? MobileJw = null);
