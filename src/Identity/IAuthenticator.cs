// IAuthenticator — the Identity project's sign-in surface, used by the
// application to turn credentials into a session and a session token back into a
// caller.
//
// Use:  SignInAsync at the login endpoint; if the outcome carries a challenge,
//       CompleteSignInAsync with the code. ValidateAsync on every request;
//       RevokeAsync at logout.
// Edit: every credential failure returns the same error on purpose. Distinguishing
//       "no such user" from "wrong password" — or "wrong code" from "no second
//       factor enrolled" — tells an attacker which accounts are worth attacking.

using OpenDealer360.Core;

namespace OpenDealer360.Identity;

public interface IAuthenticator
{
    /// <summary>
    /// Verifies credentials. Returns a session for an account with no second
    /// factor, or a challenge for one that has it enabled — never both.
    /// </summary>
    Task<Result<SignInOutcome>> SignInAsync(
        string email,
        string password,
        string? deviceSummary,
        CancellationToken cancellationToken);

    /// <summary>
    /// Finishes a sign-in that needed a second factor. Accepts a TOTP code or an
    /// unused recovery code.
    /// </summary>
    Task<Result<IssuedSession>> CompleteSignInAsync(
        string challengeToken,
        string code,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a session token to its user, sliding the idle window forward.
    /// A revoked, expired, or unknown token fails.
    /// </summary>
    Task<Result<Guid>> ValidateAsync(string token, CancellationToken cancellationToken);

    /// <summary>Ends a session immediately. Unknown tokens succeed silently.</summary>
    Task RevokeAsync(string token, CancellationToken cancellationToken);

    /// <summary>
    /// Generates a secret and returns what an authenticator app needs. Nothing
    /// changes about signing in until the user confirms with a working code.
    /// </summary>
    Task<Result<MfaEnrolment>> BeginMfaEnrolmentAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Turns the second factor on, and returns the recovery codes. This is the
    /// only time those codes exist in readable form.
    /// </summary>
    Task<Result<IReadOnlyList<string>>> ConfirmMfaAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken);

    /// <summary>Turns it off. Requires a current code, so a borrowed session cannot.</summary>
    Task<Result> DisableMfaAsync(Guid userId, string code, CancellationToken cancellationToken);
}

/// <summary>
/// What happened when credentials were accepted: either a session, or a demand
/// for a second factor.
/// </summary>
public sealed record SignInOutcome
{
    private SignInOutcome(IssuedSession? session, SecondFactorChallenge? challenge)
    {
        Session = session;
        Challenge = challenge;
    }

    public IssuedSession? Session { get; }

    public SecondFactorChallenge? Challenge { get; }

    public bool IsComplete => Session is not null;

    public static SignInOutcome Completed(IssuedSession session) => new(session, null);

    public static SignInOutcome NeedsSecondFactor(SecondFactorChallenge challenge) => new(null, challenge);
}

/// <summary>A newly started session: the token for the cookie, and when it dies.</summary>
public sealed record IssuedSession(string Token, DateTimeOffset AbsoluteExpiresAt);

/// <summary>
/// Proof that a password was accepted, exchangeable for a session with a valid
/// code. Grants nothing on its own.
/// </summary>
public sealed record SecondFactorChallenge(string Token, DateTimeOffset ExpiresAt);

/// <summary>What an authenticator app needs to start producing codes.</summary>
public sealed record MfaEnrolment(string Secret, string EnrolmentUri);

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

    /// <summary>
    /// Covers a wrong code, an expired challenge, a reused recovery code, and too
    /// many attempts. One message, so none of them can be told apart.
    /// </summary>
    public static Error SecondFactorRejected { get; } = Error.Forbidden(
        "auth.second_factor_rejected",
        "That code is not valid. Try again, or use a recovery code.");

    public static Error MfaNotEnrolled { get; } = Error.Validation(
        "auth.mfa_not_enrolled",
        "There is no second factor set up on this account.");

    public static Error MfaAlreadyOn { get; } = Error.Conflict(
        "auth.mfa_already_on",
        "A second factor is already set up. Turn it off before enrolling again.");
}
