// DevSecretProtector — a no-op ISecretProtector for local development.
//
// Use:  nowhere but Development. Program.cs refuses to start if this is
//       registered in any other environment.
// Edit: do not "improve" this into a real implementation. Add a separate
//       DPAPI/certificate/KMS class and register that instead.

using OpenDealer360.Core;

namespace OpenDealer360.Tenancy;

/// <summary>
/// DEVELOPMENT ONLY. A no-op protector so the resolution seam works end to end
/// before real key management lands. A production deployment must register a
/// DPAPI/certificate/KMS-backed <see cref="ISecretProtector"/> instead
/// (doc 06 §4). The Host refuses to start with this protector outside Development.
/// </summary>
public sealed class DevSecretProtector : ISecretProtector
{
    public string Protect(string plaintext) => plaintext;

    public string Unprotect(string protectedValue) => protectedValue;
}
