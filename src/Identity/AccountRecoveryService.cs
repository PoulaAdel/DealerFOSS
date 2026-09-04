// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   AccountRecoveryService — the two ways back into an account, and the care they
//   both need.
//
// Usage:
//   Through IAccountRecovery.
//
// Coding Instructions:
//   This is the softest surface in the application. It is unauthenticated by
//   necessity, it names an account by email, and success is a new password on
//   somebody else's login. Four things are load-bearing.
//
//   EVERY REFUSAL IS THE SAME REFUSAL, and every path does the same work
//   before returning it. An unknown email must not answer faster than a known
//   one with a wrong code, or the timing IS the answer — so the password
//   hasher runs even when there is no user to hash for.
//
//   A RECOVERY CODE CANNOT BE AN ENROLMENT CODE, and the purpose is matched
//   in the query rather than checked afterwards. Be honest about what that
//   buys today: it is DEFENCE IN DEPTH, not the control doing the work.
//   Rehearsed 2026-08-09 — removing the purpose filter from BOTH this path
//   and enrolment's failed no test, because the two are already separated by
//   their opposite preconditions on PasswordHash: recovery refuses an account
//   without one, enrolment refuses an account with one, so neither path can
//   currently reach the other's codes to be confused by them.
//
//   It stays because that separation is an accident of two checks agreeing,
//   and the day somebody relaxes either one — an "invite an existing user"
//   feature, a merge, an import that sets a hash — the purpose column is what
//   stops a starter's code opening a reset. Same reasoning as the supersede
//   loop in StaffDirectoryService, and recorded the same way.
//
//   A SUCCESSFUL RESET ENDS EVERY SESSION. Somebody recovering an account may
//   be recovering it FROM someone, and leaving the old sessions alive would
//   make the reset cosmetic.
//
//   THE AUTHENTICATOR PATH SPENDS WHAT IT USES. A second-factor recovery code
//   is single use here exactly as it is at sign-in; a code that could be
//   replayed to set a password is a password.

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using DealerFOSS.Core;

namespace DealerFOSS.Identity;

internal sealed class AccountRecoveryService(
    IdentityDb db,
    IPasswordHasher<User> passwordHasher,
    ISecretProtector secretProtector,
    IAuditSink audit,
    IClock clock)
    : IAccountRecovery
{
    /// <summary>The shortest password worth having. Matches enrolment.</summary>
    private const int MinimumPasswordLength = 12;

    private readonly IdentityDb _db = db;
    private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;
    private readonly ISecretProtector _secretProtector = secretProtector;
    private readonly IAuditSink _audit = audit;
    private readonly IClock _clock = clock;

    /// <summary>
    /// Both built-in methods are always on; neither needs anything external.
    /// Email and text message report false until they exist — see ADR-018 for why
    /// an unconfigured method is never offered rather than offered and failing.
    /// </summary>
    public RecoveryMethods Offered { get; } = new(
        Authenticator: true,
        IssuedCode: true,
        Email: false,
        TextMessage: false);

    public Task<Result> ResetWithAuthenticatorAsync(
        string email,
        string code,
        string newPassword,
        CancellationToken cancellationToken) =>
        ResetAsync(email, code, newPassword, ProofKind.Authenticator, cancellationToken);

    public Task<Result> ResetWithIssuedCodeAsync(
        string email,
        string code,
        string newPassword,
        CancellationToken cancellationToken) =>
        ResetAsync(email, code, newPassword, ProofKind.IssuedCode, cancellationToken);

    private enum ProofKind
    {
        Authenticator,
        IssuedCode,
    }

    private async Task<Result> ResetAsync(
        string email,
        string code,
        string newPassword,
        ProofKind proof,
        CancellationToken cancellationToken)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        var now = _clock.UtcNow;

        // Checked before anything is looked up, and it is the ONE refusal that
        // differs. It has to: a caller has to be told their password is too
        // short or they cannot proceed, and it reveals nothing — the answer is
        // the same whether or not the account exists.
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < MinimumPasswordLength)
        {
            return Result.Failure(RecoveryErrors.PasswordTooShort);
        }

        var user = await _db.Users
            .Include(u => u.RecoveryCodes)
            .SingleOrDefaultAsync(u => u.Email == normalized, cancellationToken);

        // An account with no password has never been enrolled, so there is
        // nothing to recover — that person needs their starter code, and saying
        // so here would confirm the address exists.
        if (user is null || !user.IsActive || user.PasswordHash is null)
        {
            // Same work as the success path, so an unknown address does not
            // answer measurably faster than a known one. Without this the timing
            // is an oracle for which staff emails are real. The hasher takes a
            // null user, exactly as the decoy verify in Authenticator does.
            _passwordHasher.HashPassword(null!, newPassword);
            return Result.Failure(RecoveryErrors.Refused);
        }

        var accepted = proof == ProofKind.Authenticator
            ? VerifyAuthenticator(user, code, now)
            : await VerifyIssuedCodeAsync(user, code, now, cancellationToken);

        if (!accepted)
        {
            await _db.SaveChangesAsync(cancellationToken);

            await _audit.RecordAsync(
                new AuditEntry(
                    user.Id, "Staff.RecoveryRefused", AuditOutcome.Denied, "User", user.Id.ToString(), null,
                    $"A password reset was attempted with a {Describe(proof)} and refused.", null, null),
                cancellationToken);

            return Result.Failure(RecoveryErrors.Refused);
        }

        user.SetPasswordHash(_passwordHasher.HashPassword(user, newPassword));

        // Whoever is recovering may be recovering the account FROM somebody. A
        // reset that left the other party signed in would be cosmetic.
        var live = await _db.Sessions
            .Where(s => s.UserId == user.Id && s.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var session in live)
        {
            session.Revoke(now);
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(
                user.Id, "Staff.Recovered", AuditOutcome.Allowed, "User", user.Id.ToString(), null,
                $"Set a new password with a {Describe(proof)}. {live.Count} session(s) ended.", null, null),
            cancellationToken);

        return Result.Success();
    }

    private static string Describe(ProofKind proof) =>
        proof == ProofKind.Authenticator ? "code from an authenticator app" : "manager-issued code";

    /// <summary>
    /// A live TOTP code, or one of the recovery codes issued at second-factor
    /// enrolment. The recovery code is spent, exactly as it is at sign-in.
    /// </summary>
    private bool VerifyAuthenticator(User user, string? code, DateTimeOffset now)
    {
        if (user.MfaSecretProtected is null || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        if (Totp.Verify(_secretProtector.Unprotect(user.MfaSecretProtected), code, now))
        {
            return true;
        }

        var hash = RecoveryCode.Hash(code);
        var match = user.RecoveryCodes.FirstOrDefault(r => r.IsAvailable && r.CodeHash == hash);

        if (match is null)
        {
            return false;
        }

        match.Consume(now);
        return true;
    }

    /// <summary>
    /// The newest live code a manager issued FOR RECOVERY. The purpose is part of
    /// the query rather than a check afterwards, so a starter's enrolment code is
    /// not merely rejected here — it is never found. Defence in depth today; see
    /// the note at the top of the file for what actually separates the two paths.
    /// </summary>
    private async Task<bool> VerifyIssuedCodeAsync(
        User user,
        string? code,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var issued = await _db.StaffEnrolments
            .Where(e =>
                e.UserId == user.Id &&
                e.ConsumedAt == null &&
                e.Purpose == EnrolmentPurpose.Recovery)
            .OrderByDescending(e => e.IssuedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (issued is null || !issued.IsUsableAt(now))
        {
            return false;
        }

        if (!FixedTimeEquals(issued.CodeHash, StaffEnrolment.HashOf(code ?? string.Empty)))
        {
            // Counted, so a code being hammered dies rather than standing there.
            issued.RecordFailure();
            return false;
        }

        issued.Consume(now);
        return true;
    }

    private static bool FixedTimeEquals(string a, string b) =>
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(a),
            System.Text.Encoding.UTF8.GetBytes(b));
}
