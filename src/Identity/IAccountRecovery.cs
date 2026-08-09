// IAccountRecovery — how somebody who cannot sign in proves the account is theirs.
//
// Use:  inject IAccountRecovery. Reached from /api/v1/auth/recover, which is
//       unauthenticated by necessity — the caller has no session, that is the
//       whole problem.
// Edit: three properties hold this together, and each is the reason a familiar
//       shape was NOT used. See ADR-018.
//
//       ONE. RESETTING IS A SINGLE CALL. Email, proof, and the new password
//       arrive together and the password changes or nothing does. The usual
//       shape — prove, receive a ticket, redeem the ticket — needs a second
//       credential that exists between the two calls, has to be stored, expired,
//       transported and invalidated, and is worth stealing. Nothing here needs
//       to survive between two requests, so nothing does.
//
//       TWO. WHAT IS OFFERED IS A PROPERTY OF THE INSTALLATION, NEVER OF THE
//       ACCOUNT. "Which methods can I use?" is answered without being told an
//       email address. Answering per-account would mean saying whether that
//       address exists and whether it has an authenticator enrolled, which turns
//       the recovery screen into a way to enumerate a dealership's staff and
//       identify who is easiest to attack.
//
//       THREE. EVERY FAILURE IS THE SAME FAILURE. Unknown email, wrong code, no
//       authenticator, stopped account, account that never had a password — all
//       return RecoveryRefused. A caller must not be able to tell them apart.

using DealerFOSS.Core;

namespace DealerFOSS.Identity;

public interface IAccountRecovery
{
    /// <summary>
    /// The methods this installation can offer. Deliberately takes no email and
    /// reveals nothing about any account — see the note above.
    /// </summary>
    RecoveryMethods Offered { get; }

    /// <summary>
    /// Sets a new password for somebody who can still produce a code from their
    /// authenticator app. A recovery code from second-factor enrolment is
    /// accepted too, and is spent when it is used.
    /// </summary>
    /// <remarks>
    /// This does not need a manager, which is the point: the commonest case is
    /// somebody who has their phone and has forgotten a password, and routing
    /// that through a colleague makes the backstop into the everyday path.
    /// </remarks>
    Task<Result> ResetWithAuthenticatorAsync(
        string email,
        string code,
        string newPassword,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sets a new password using a code a manager issued. The backstop, for
    /// somebody who has lost the phone as well.
    /// </summary>
    Task<Result> ResetWithIssuedCodeAsync(
        string email,
        string code,
        string newPassword,
        CancellationToken cancellationToken);
}

/// <summary>
/// What this installation can offer. A method that is not configured is never
/// offered rather than offered and then failing (ADR-018).
/// </summary>
/// <param name="Authenticator">
/// Always available: TOTP is built in and needs nothing external. It still only
/// works for somebody who enrolled one, which this type deliberately cannot say.
/// </param>
/// <param name="IssuedCode">
/// Always available: the manager-issued backstop needs nothing external either.
/// </param>
/// <param name="Email">
/// Needs an SMTP host, and reverses this system's "nothing is sent outward"
/// default for one purpose. Off until an operator configures one; nothing is
/// built behind it yet, and it reports false.
/// </param>
/// <param name="TextMessage">
/// Needs a paid gateway AND a verified phone number on the staff record, which
/// is not a field that exists. Off, and reports false.
/// </param>
public sealed record RecoveryMethods(
    bool Authenticator,
    bool IssuedCode,
    bool Email,
    bool TextMessage);

public static class RecoveryErrors
{
    /// <summary>
    /// The only failure this capability has. Unknown address, wrong code, no
    /// authenticator enrolled, a stopped account, or an account that never had a
    /// password all arrive here — because a caller who can tell those apart can
    /// map a dealership's staff and pick the softest target.
    /// </summary>
    public static Error Refused { get; } = Error.Forbidden(
        "recovery.refused",
        "That did not work. Check the email address and the code, and try again.");

    public static Error PasswordTooShort { get; } = Error.Validation(
        "recovery.password_too_short",
        "A password needs at least 12 characters. Length is what makes one hard to guess.");
}
