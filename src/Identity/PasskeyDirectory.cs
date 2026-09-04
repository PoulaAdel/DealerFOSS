// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   PasskeyDirectory — IPasskeys, over the credential store and the verifier.
//
// Usage:
//   Registered as IPasskeys; the application maps four routes onto it.
//
// Coding Instructions:
//   This class does almost nothing on its own, and that is the design. The
//   cryptography is in WebAuthn, the credential rules are on Passkey, and
//   sessions are minted by the one method every other sign-in uses. What
//   lives here is the ORDER those happen in, which is where ceremonies
//   usually go wrong:
//
//   * The challenge is consumed BEFORE the response is judged, and saved
//   either way. A challenge that survives a failed attempt can be retried
//   against, which is most of the way to removing it.
//   * A credential is looked up by its id, and the challenge is checked
//   against THAT credential's key — never "any key that verifies".
//   * The counter is written in the same save as the sign-in.
//
//   Every refusal outside is PasskeyErrors.NotAccepted. The specific reason
//   is logged. See IPasskeys for why.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using DealerFOSS.Core;

namespace DealerFOSS.Identity;

/// <summary>
/// Where the application is served from, as WebAuthn understands it.
/// </summary>
/// <remarks>
/// The relying-party id is a bare domain — never a URL and never a port —
/// and the origins are full origins. A passkey is bound to the relying-party id
/// for ever, so changing it in production orphans every credential already
/// registered. That is a migration, not a setting change.
/// </remarks>
internal sealed class PasskeyOptions
{
    public string RelyingPartyId { get; set; } = "localhost";

    public string RelyingPartyName { get; set; } = "DealerFOSS";

    public IList<string> Origins { get; set; } = ["http://localhost:5173", "http://localhost:5080"];
}

internal sealed partial class PasskeyDirectory(
    IdentityDb db,
    ISessionIssuer sessions,
    ICurrentUser currentUser,
    PasskeyOptions options,
    IClock clock,
    ILogger<PasskeyDirectory> logger)
    : IPasskeys
{
    private const int ChallengeBytes = 32;

    private readonly IdentityDb _db = db;
    private readonly ISessionIssuer _sessions = sessions;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly PasskeyOptions _options = options;
    private readonly IClock _clock = clock;
    private readonly ILogger<PasskeyDirectory> _logger = logger;

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Passkey ceremony refused: {Reason}.")]
    private static partial void Refused(ILogger logger, WebAuthnFailure reason);

    public async Task<Result<PasskeyRegistrationChallenge>> BeginRegistrationAsync(
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure<PasskeyRegistrationChallenge>(AuthErrors.SessionInvalid);
        }

        var user = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == _currentUser.Id, cancellationToken);

        if (user is null)
        {
            return Result.Failure<PasskeyRegistrationChallenge>(AuthErrors.SessionInvalid);
        }

        var challenge = await IssueAsync(forRegistration: true, user.Id, cancellationToken);

        var existing = await _db.Passkeys
            .AsNoTracking()
            .Where(p => p.UserId == user.Id)
            .Select(p => p.CredentialId)
            .ToListAsync(cancellationToken);

        return Result.Success(new PasskeyRegistrationChallenge(
            challenge.Id,
            Base64Url.Encode(challenge.Challenge),
            _options.RelyingPartyId,
            _options.RelyingPartyName,
            // The user handle is the account id, not the email. A handle travels
            // to the authenticator and stays on the device; putting an email
            // there would leave it on hardware the dealership does not own.
            Base64Url.Encode(user.Id.ToByteArray()),
            user.Email,
            user.DisplayName,
            [.. existing.Select(Base64Url.Encode)]));
    }

    public async Task<Result<RegisteredPasskey>> FinishRegistrationAsync(
        PasskeyRegistrationResponse response,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure<RegisteredPasskey>(AuthErrors.SessionInvalid);
        }

        var challenge = await ConsumeAsync(
            response.ChallengeId, forRegistration: true, cancellationToken);

        if (challenge is null)
        {
            return Result.Failure<RegisteredPasskey>(PasskeyErrors.ChallengeExpired);
        }

        // The challenge was issued to somebody. If that is not the caller, this
        // is one account trying to bolt a credential onto another.
        if (challenge.UserId != _currentUser.Id)
        {
            return Result.Failure<RegisteredPasskey>(PasskeyErrors.NotAccepted);
        }

        byte[] clientData, attestation;
        try
        {
            clientData = Base64Url.Decode(response.ClientDataJson);
            attestation = Base64Url.Decode(response.AttestationObject);
        }
        catch (FormatException)
        {
            return Result.Failure<RegisteredPasskey>(PasskeyErrors.NotAccepted);
        }

        var failure = WebAuthn.TryVerifyRegistration(
            clientData, attestation, challenge.Challenge,
            _options.RelyingPartyId, [.. _options.Origins], out var verified);

        if (failure != WebAuthnFailure.None || verified is null)
        {
            Refused(_logger, failure);
            return Result.Failure<RegisteredPasskey>(PasskeyErrors.NotAccepted);
        }

        var taken = await _db.Passkeys
            .AsNoTracking()
            .AnyAsync(p => p.CredentialId == verified.CredentialId, cancellationToken);

        if (taken)
        {
            return Result.Failure<RegisteredPasskey>(PasskeyErrors.AlreadyRegistered);
        }

        var passkey = new Passkey(
            Guid.NewGuid(), _currentUser.Id, verified.CredentialId, verified.PublicKeySpki,
            verified.Algorithm, verified.SignCount, response.Label, _clock.UtcNow);

        _db.Passkeys.Add(passkey);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(Describe(passkey));
    }

    public async Task<Result<PasskeySignInChallenge>> BeginSignInAsync(
        CancellationToken cancellationToken)
    {
        // Anonymous, and carries nothing about any account — asking for a
        // challenge must not be a way to find out who has a passkey.
        var challenge = await IssueAsync(forRegistration: false, null, cancellationToken);

        return Result.Success(new PasskeySignInChallenge(
            challenge.Id, Base64Url.Encode(challenge.Challenge), _options.RelyingPartyId));
    }

    public async Task<Result<IssuedSession>> FinishSignInAsync(
        PasskeySignInResponse response,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        var challenge = await ConsumeAsync(
            response.ChallengeId, forRegistration: false, cancellationToken);

        if (challenge is null)
        {
            return Result.Failure<IssuedSession>(PasskeyErrors.ChallengeExpired);
        }

        byte[] credentialId, clientData, authenticatorData, signature;
        try
        {
            credentialId = Base64Url.Decode(response.CredentialId);
            clientData = Base64Url.Decode(response.ClientDataJson);
            authenticatorData = Base64Url.Decode(response.AuthenticatorData);
            signature = Base64Url.Decode(response.Signature);
        }
        catch (FormatException)
        {
            return Result.Failure<IssuedSession>(PasskeyErrors.NotAccepted);
        }

        var passkey = await _db.Passkeys
            .SingleOrDefaultAsync(p => p.CredentialId == credentialId, cancellationToken);

        // Unknown credential and failed verification answer identically, so this
        // cannot be used to discover which credentials exist.
        if (passkey is null)
        {
            return Result.Failure<IssuedSession>(PasskeyErrors.NotAccepted);
        }

        var user = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == passkey.UserId, cancellationToken);

        // A leaver's passkey stops working the moment their account does. The
        // credential is still on their phone; it opens nothing.
        if (user is null || !user.IsActive)
        {
            return Result.Failure<IssuedSession>(PasskeyErrors.NotAccepted);
        }

        var failure = WebAuthn.TryVerifyAssertion(
            clientData, authenticatorData, signature, challenge.Challenge,
            _options.RelyingPartyId, [.. _options.Origins],
            passkey.PublicKeySpki, passkey.Algorithm, passkey.Counter, out var counter);

        if (failure != WebAuthnFailure.None)
        {
            Refused(_logger, failure);
            return Result.Failure<IssuedSession>(PasskeyErrors.NotAccepted);
        }

        passkey.RecordUse(counter, _clock.UtcNow);

        // The same method every other sign-in uses. Same cookie, same
        // anti-forgery pair, same expiry, same audit entry.
        var session = await _sessions.StartSessionAsync(
            user.Id, "Passkey", _clock.UtcNow, cancellationToken);

        return Result.Success(session);
    }

    public async Task<Result<IReadOnlyList<RegisteredPasskey>>> ListMineAsync(
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure<IReadOnlyList<RegisteredPasskey>>(AuthErrors.SessionInvalid);
        }

        var mine = await _db.Passkeys
            .AsNoTracking()
            .Where(p => p.UserId == _currentUser.Id)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<RegisteredPasskey>>([.. mine.Select(Describe)]);
    }

    public async Task<Result> ForgetAsync(Guid passkeyId, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure(AuthErrors.SessionInvalid);
        }

        // Filtered by owner in the query, not checked afterwards: somebody else's
        // credential is simply not found, which is also the honest answer.
        var passkey = await _db.Passkeys
            .SingleOrDefaultAsync(
                p => p.Id == passkeyId && p.UserId == _currentUser.Id, cancellationToken);

        if (passkey is null)
        {
            return Result.Failure(PasskeyErrors.NotFound);
        }

        _db.Passkeys.Remove(passkey);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<PasskeyChallenge> IssueAsync(
        bool forRegistration, Guid? userId, CancellationToken cancellationToken)
    {
        var challenge = new PasskeyChallenge(
            Guid.NewGuid(),
            RandomNumberGenerator.GetBytes(ChallengeBytes),
            forRegistration,
            userId,
            _clock.UtcNow);

        _db.PasskeyChallenges.Add(challenge);
        await _db.SaveChangesAsync(cancellationToken);

        return challenge;
    }

    /// <summary>
    /// Takes a challenge and spends it, saving before the response is judged.
    /// A challenge that survives a failed attempt can be retried against, which
    /// removes most of the value of having one.
    /// </summary>
    private async Task<PasskeyChallenge?> ConsumeAsync(
        Guid challengeId, bool forRegistration, CancellationToken cancellationToken)
    {
        var challenge = await _db.PasskeyChallenges
            .SingleOrDefaultAsync(c => c.Id == challengeId, cancellationToken);

        if (challenge is null
            || challenge.ForRegistration != forRegistration
            || !challenge.IsUsableAt(_clock.UtcNow))
        {
            return null;
        }

        challenge.Consume(_clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);

        return challenge;
    }

    private static RegisteredPasskey Describe(Passkey passkey) =>
        new(passkey.Id, passkey.Label, passkey.CreatedAt, passkey.LastUsedAt);
}

/// <summary>
/// Minting a session, exposed inside Identity only. Exists so passkey sign-in
/// reuses the one method every other sign-in uses instead of growing a second
/// one that drifts.
/// </summary>
internal interface ISessionIssuer
{
    Task<IssuedSession> StartSessionAsync(
        Guid userId, string? deviceSummary, DateTimeOffset now, CancellationToken cancellationToken);
}
