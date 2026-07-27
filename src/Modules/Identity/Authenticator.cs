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
using OpenDealer360.Core;
using OpenDealer360.Identity.Contracts;
using OpenDealer360.Identity.Data;
using OpenDealer360.Identity.Domain;

namespace OpenDealer360.Identity;

public sealed class Authenticator(
    IdentityDbContext db,
    IClock clock,
    IPasswordHasher<User> passwordHasher,
    IAuditSink audit)
    : IAuthenticator
{
    /// <summary>
    /// A valid hash of a throwaway password. Verified against when no user
    /// matches, so a missing account costs the same time as a wrong password.
    /// </summary>
    private static readonly string DecoyHash =
        new PasswordHasher<User>().HashPassword(null!, "not-a-real-password");

    private readonly IdentityDbContext _db = db;
    private readonly IClock _clock = clock;
    private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;
    private readonly IAuditSink _audit = audit;

    public async Task<Result<IssuedSession>> SignInAsync(
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
            return Result.Failure<IssuedSession>(AuthErrors.InvalidCredentials);
        }

        var verification = _passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash!, password ?? string.Empty);

        if (verification == PasswordVerificationResult.Failed)
        {
            await RecordFailureAsync(user.Id, normalized, cancellationToken);
            return Result.Failure<IssuedSession>(AuthErrors.InvalidCredentials);
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            // The stored hash used older parameters; upgrade it while we hold
            // the plaintext, which is the only moment we can.
            user.SetPasswordHash(_passwordHasher.HashPassword(user, password!));
        }

        var (token, tokenHash) = NewToken();
        var now = _clock.UtcNow;
        var session = new Session(Guid.NewGuid(), user.Id, tokenHash, now, deviceSummary);

        _db.Sessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(user.Id, "Auth.SignIn", AuditOutcome.Allowed,
                "Session", session.Id.ToString(), null, null, null, null),
            cancellationToken);

        return Result.Success(new IssuedSession(token, session.AbsoluteExpiresAt));
    }

    public async Task<Result<Guid>> ValidateAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result.Failure<Guid>(AuthErrors.SessionInvalid);
        }

        var hash = Hash(token);
        var session = await _db.Sessions
            .SingleOrDefaultAsync(s => s.TokenHash == hash, cancellationToken);

        var now = _clock.UtcNow;
        if (session is null || !session.IsActiveAt(now))
        {
            return Result.Failure<Guid>(AuthErrors.SessionInvalid);
        }

        // A user deactivated mid-session loses access at once, without waiting
        // for the session to expire.
        var stillActive = await _db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == session.UserId && u.IsActive, cancellationToken);

        if (!stillActive)
        {
            return Result.Failure<Guid>(AuthErrors.SessionInvalid);
        }

        session.Touch(now);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(session.UserId);
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
