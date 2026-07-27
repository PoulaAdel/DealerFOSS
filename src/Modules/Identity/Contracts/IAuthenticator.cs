// IAuthenticator — the Identity module's sign-in surface, used by the Host to
// turn credentials into a session and a session token back into a caller.
//
// Use:  SignInAsync at the login endpoint; ValidateAsync on every request;
//       RevokeAsync at logout.
// Edit: every failure returns the same error on purpose. Distinguishing "no such
//       user" from "wrong password" tells an attacker which emails are real.

using OpenDealer360.Core;

namespace OpenDealer360.Identity.Contracts;

public interface IAuthenticator
{
    /// <summary>
    /// Verifies credentials and starts a session. The returned token is the only
    /// time it exists in plaintext — store it in the cookie and forget it.
    /// </summary>
    Task<Result<IssuedSession>> SignInAsync(
        string email,
        string password,
        string? deviceSummary,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a session token to its user, sliding the idle window forward.
    /// A revoked, expired, or unknown token fails.
    /// </summary>
    Task<Result<Guid>> ValidateAsync(string token, CancellationToken cancellationToken);

    /// <summary>Ends a session immediately. Unknown tokens succeed silently.</summary>
    Task RevokeAsync(string token, CancellationToken cancellationToken);
}

/// <summary>A newly started session: the token for the cookie, and when it dies.</summary>
public sealed record IssuedSession(string Token, DateTimeOffset AbsoluteExpiresAt);

/// <summary>Stable failures for the sign-in surface (doc 06 §6).</summary>
public static class AuthErrors
{
    /// <summary>
    /// Used for every credential failure — unknown email, wrong password,
    /// deactivated account, no password set. The caller cannot tell which.
    /// </summary>
    public static Error InvalidCredentials { get; } = Error.Forbidden(
        "auth.invalid_credentials",
        "That email and password combination is not valid.");

    public static Error SessionInvalid { get; } = Error.Forbidden(
        "auth.session_invalid",
        "Your session has ended. Sign in again.");
}
