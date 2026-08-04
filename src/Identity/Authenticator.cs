// Authenticator — verifies passwords, starts sessions, and turns a session token
// back into a caller on every request.
//
// Use:  through IAuthenticator; the Host never touches sessions directly.
// Edit: three rules hold this together and none may be relaxed for convenience.
//       Every credential failure returns the same error. The raw token is hashed
//       before it is compared or stored. A password is verified even when the
//       user does not exist, so the response time does not reveal which emails
//       are real.

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using DealerFOSS.Core;
using DealerFOSS.Identity;

namespace DealerFOSS.Identity;

internal sealed class Authenticator(
    IdentityDb db,
    IClock clock,
    IPasswordHasher<User> passwordHasher,
    ISecretProtector secretProtector,
    IAuditSink audit)
    : IAuthenticator
{
    /// <summary>Shown as the account issuer in an authenticator app.</summary>
    private const string Issuer = "DealerFOSS";

    /// <summary>
    /// A valid hash of a throwaway password. Verified against when no user
    /// matches, so a missing account costs the same time as a wrong password.
    /// </summary>
    private static readonly string DecoyHash =
        new PasswordHasher<User>().HashPassword(null!, "not-a-real-password");

    private readonly IdentityDb _db = db;
    private readonly IClock _clock = clock;
    private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;
    private readonly ISecretProtector _secretProtector = secretProtector;
    private readonly IAuditSink _audit = audit;

    public async Task<Result<SignInOutcome>> SignInAsync(
        string email,
        string password,
        string? deviceSummary,
        CancellationToken cancellationToken)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();

        var user = await _db.Users
            .SingleOrDefaultAsync(u => u.Email == normalized, cancellationToken);

        if (user is null || !user.CanSignIn)
        {
            // Still do the work, so absence is not detectable by timing.
            _passwordHasher.VerifyHashedPassword(null!, DecoyHash, password ?? string.Empty);
            await RecordFailureAsync(user?.Id, normalized, cancellationToken);
            return Result.Failure<SignInOutcome>(AuthErrors.InvalidCredentials);
        }

        var verification = _passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash!, password ?? string.Empty);

        if (verification == PasswordVerificationResult.Failed)
        {
            await RecordFailureAsync(user.Id, normalized, cancellationToken);
            return Result.Failure<SignInOutcome>(AuthErrors.InvalidCredentials);
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            // The stored hash used older parameters; upgrade it while we hold
            // the plaintext, which is the only moment we can.
            user.SetPasswordHash(_passwordHasher.HashPassword(user, password!));
        }

        var now = _clock.UtcNow;

        // The password alone is not a session for an account with a second
        // factor. It buys a short-lived challenge and nothing else.
        if (user.MfaEnabled)
        {
            var (challengeToken, challengeHash) = NewToken();
            var challenge = new SignInChallenge(
                Guid.NewGuid(), user.Id, challengeHash, now, deviceSummary);

            _db.SignInChallenges.Add(challenge);
            await _db.SaveChangesAsync(cancellationToken);

            return Result.Success(SignInOutcome.NeedsSecondFactor(
                new SecondFactorChallenge(challengeToken, challenge.ExpiresAt)));
        }

        var session = await StartSessionAsync(user.Id, deviceSummary, now, cancellationToken);
        return Result.Success(SignInOutcome.Completed(session));
    }

    public async Task<Result<IssuedSession>> CompleteSignInAsync(
        string challengeToken,
        string code,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(challengeToken))
        {
            return Result.Failure<IssuedSession>(AuthErrors.SecondFactorRejected);
        }

        var hash = Hash(challengeToken);
        var challenge = await _db.SignInChallenges
            .SingleOrDefaultAsync(c => c.TokenHash == hash, cancellationToken);

        var now = _clock.UtcNow;
        if (challenge is null || !challenge.IsUsableAt(now))
        {
            return Result.Failure<IssuedSession>(AuthErrors.SecondFactorRejected);
        }

        var user = await _db.Users
            .Include(u => u.RecoveryCodes)
            .SingleOrDefaultAsync(u => u.Id == challenge.UserId, cancellationToken);

        if (user is null || !user.CanSignIn || !user.MfaEnabled)
        {
            return Result.Failure<IssuedSession>(AuthErrors.SecondFactorRejected);
        }

        var accepted = VerifyTotp(user, code, now) || ConsumeRecoveryCode(user, code, now);

        if (!accepted)
        {
            // Counted, so six digits cannot simply be guessed at.
            challenge.RecordFailure();
            await _db.SaveChangesAsync(cancellationToken);

            await _audit.RecordAsync(
                AuditEntry.Denied(user.Id, "Auth.SecondFactor", "User", user.Id.ToString(),
                    null, "Invalid second factor."),
                cancellationToken);

            return Result.Failure<IssuedSession>(AuthErrors.SecondFactorRejected);
        }

        challenge.Consume(now);
        var session = await StartSessionAsync(user.Id, challenge.DeviceSummary, now, cancellationToken);

        return Result.Success(session);
    }

    public async Task<Result<MfaEnrolment>> BeginMfaEnrolmentAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<MfaEnrolment>(AuthErrors.SessionInvalid);
        }

        if (user.MfaEnabled)
        {
            return Result.Failure<MfaEnrolment>(AuthErrors.MfaAlreadyOn);
        }

        var secret = Totp.NewSecret();
        user.BeginMfaEnrolment(_secretProtector.Protect(secret));
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(new MfaEnrolment(
            secret, Totp.EnrolmentUri(secret, Issuer, user.Email)));
    }

    public async Task<Result<IReadOnlyList<string>>> ConfirmMfaAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Include(u => u.RecoveryCodes)
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user?.MfaSecretProtected is null)
        {
            return Result.Failure<IReadOnlyList<string>>(AuthErrors.MfaNotEnrolled);
        }

        var now = _clock.UtcNow;
        if (!VerifyTotp(user, code, now))
        {
            return Result.Failure<IReadOnlyList<string>>(AuthErrors.SecondFactorRejected);
        }

        user.ConfirmMfa(now);

        var (plaintext, records) = RecoveryCode.Issue(user.Id);
        foreach (var record in records)
        {
            user.RecoveryCodes.Add(record);
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(user.Id, "Auth.MfaEnabled", AuditOutcome.Allowed,
                "User", user.Id.ToString(), null, null, null, null),
            cancellationToken);

        return Result.Success(plaintext);
    }

    public async Task<Result> DisableMfaAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Include(u => u.RecoveryCodes)
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null || !user.MfaEnabled)
        {
            return Result.Failure(AuthErrors.MfaNotEnrolled);
        }

        var now = _clock.UtcNow;

        // A current code, so a borrowed or hijacked session cannot quietly take
        // the second factor off an account.
        if (!VerifyTotp(user, code, now) && !ConsumeRecoveryCode(user, code, now))
        {
            return Result.Failure(AuthErrors.SecondFactorRejected);
        }

        user.DisableMfa();
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(user.Id, "Auth.MfaDisabled", AuditOutcome.Allowed,
                "User", user.Id.ToString(), null, null, null, null),
            cancellationToken);

        return Result.Success();
    }

    private bool VerifyTotp(User user, string? code, DateTimeOffset now)
    {
        if (user.MfaSecretProtected is null)
        {
            return false;
        }

        return Totp.Verify(_secretProtector.Unprotect(user.MfaSecretProtected), code, now);
    }

    /// <summary>
    /// Spends a recovery code if it matches an unused one. Single use: a code
    /// that could be replayed is a password with extra steps.
    /// </summary>
    private static bool ConsumeRecoveryCode(User user, string? code, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
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

    private async Task<IssuedSession> StartSessionAsync(
        Guid userId,
        string? deviceSummary,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var (token, tokenHash) = NewToken();

        // A second, independent secret. The browser reads this one and echoes it
        // on writes; it is generated here so it lives and dies with the session.
        var (antiForgeryToken, antiForgeryHash) = NewToken();

        var session = new Session(
            Guid.NewGuid(), userId, tokenHash, antiForgeryHash, now, deviceSummary);

        _db.Sessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(userId, "Auth.SignIn", AuditOutcome.Allowed,
                "Session", session.Id.ToString(), null, null, null, null),
            cancellationToken);

        return new IssuedSession(token, antiForgeryToken, session.AbsoluteExpiresAt);
    }

    public async Task<Result<AuthenticatedCaller>> ValidateAsync(
        string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result.Failure<AuthenticatedCaller>(AuthErrors.SessionInvalid);
        }

        var hash = Hash(token);
        var session = await _db.Sessions
            .SingleOrDefaultAsync(s => s.TokenHash == hash, cancellationToken);

        var now = _clock.UtcNow;
        if (session is null || !session.IsActiveAt(now))
        {
            return Result.Failure<AuthenticatedCaller>(AuthErrors.SessionInvalid);
        }

        // One round trip answers both questions: is this user still allowed in
        // at all, and does their organization's policy oblige them to set a
        // second factor up first. A user deactivated mid-session loses access at
        // once, without waiting for the session to expire.
        var caller = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == session.UserId && u.IsActive)
            .Select(u => new
            {
                HasSecondFactor = u.MfaConfirmedAt != null && u.MfaSecretProtected != null,
                PolicyDemandsOne = _db.UserAssignments.Any(a =>
                    a.UserId == u.Id
                    && _db.Roles.Any(r => r.Id == a.RoleId && r.RequiresSecondFactor)),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (caller is null)
        {
            return Result.Failure<AuthenticatedCaller>(AuthErrors.SessionInvalid);
        }

        session.Touch(now);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(new AuthenticatedCaller(
            session.UserId,
            MustEnrolSecondFactor: caller.PolicyDemandsOne && !caller.HasSecondFactor));
    }

    public async Task<bool> VerifyAntiForgeryAsync(
        string sessionToken,
        string? antiForgeryToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken) || string.IsNullOrWhiteSpace(antiForgeryToken))
        {
            return false;
        }

        var sessionHash = Hash(sessionToken);
        var session = await _db.Sessions
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.TokenHash == sessionHash, cancellationToken);

        // A revoked or expired session authorizes nothing, even if its
        // anti-forgery token is presented correctly.
        if (session is null || !session.IsActiveAt(_clock.UtcNow))
        {
            return false;
        }

        var presented = Encoding.UTF8.GetBytes(Hash(antiForgeryToken));
        var expected = Encoding.UTF8.GetBytes(session.AntiForgeryHash);

        if (CryptographicOperations.FixedTimeEquals(presented, expected))
        {
            return true;
        }

        await _audit.RecordAsync(
            AuditEntry.Denied(
                session.UserId,
                "Auth.AntiForgery",
                "Session",
                session.Id.ToString(),
                null,
                "Write refused: the anti-forgery token did not belong to this session."),
            cancellationToken);

        return false;
    }

    public async Task RevokeAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var hash = Hash(token);
        var session = await _db.Sessions
            .SingleOrDefaultAsync(s => s.TokenHash == hash, cancellationToken);

        if (session is null)
        {
            return;
        }

        session.Revoke(_clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(session.UserId, "Auth.SignOut", AuditOutcome.Allowed,
                "Session", session.Id.ToString(), null, null, null, null),
            cancellationToken);
    }

    private Task RecordFailureAsync(Guid? userId, string attemptedEmail, CancellationToken cancellationToken) =>
        _audit.RecordAsync(
            AuditEntry.Denied(
                actorUserId: userId,
                action: "Auth.SignIn",
                resourceType: "User",
                // The attempted address is recorded; the attempted password never is.
                resourceId: attemptedEmail,
                rooftopId: null,
                reason: "Invalid credentials."),
            cancellationToken);

    /// <summary>
    /// A 256-bit random token, plus the hash that is stored. The raw value goes
    /// to the browser and is never written down.
    /// </summary>
    private static (string Token, string Hash) NewToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes);
        return (token, Hash(token));
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
