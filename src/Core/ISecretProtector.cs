namespace OpenDealer360.Core;

/// <summary>
/// Protects sensitive configuration at rest — most importantly tenant connection
/// references and connector credentials (doc 06 §4). Real implementations use
/// authenticated envelope encryption with a key id and rotation; the interface
/// keeps that concern behind a seam so storage code never sees plaintext keys.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedValue);
}
