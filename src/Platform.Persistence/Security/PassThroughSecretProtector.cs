using OpenDealer360.Platform.Security;

namespace OpenDealer360.Platform.Persistence.Security;

/// <summary>
/// DEVELOPMENT ONLY. A no-op protector so the resolution seam works end to end
/// before real key management lands. A production deployment must register a
/// DPAPI/certificate/KMS-backed <see cref="ISecretProtector"/> instead
/// (doc 06 §4). The Host refuses to start with this protector outside Development.
/// </summary>
public sealed class PassThroughSecretProtector : ISecretProtector
{
    public string Protect(string plaintext) => plaintext;

    public string Unprotect(string protectedValue) => protectedValue;
}
