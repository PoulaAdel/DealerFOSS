// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   IPasskeys — registering a passkey, and signing in with one.
//
// Usage:
//   The only door to passkey credentials. The application maps four routes
//   onto it and never touches the tables behind it.
//
// Coding Instructions:
//   NOTE WHAT THIS CONTRACT DOES NOT CARRY, because that is what makes it
//   safe to make public.
//
//   No public key leaves this project, no challenge is ever handed back to
//   a caller after it has been issued, and there is no way to register a
//   credential against a user other than the one signed in. A caller cannot
//   ask "does this credential exist" — the sign-in path answers a completed
//   ceremony, not a probe.
//
//   Both Finish methods take raw bytes and return a Result. That is
//   deliberate: verification lives behind this line, so nothing outside
//   Identity can be written that accidentally accepts an assertion nobody
//   checked. The application's job is to move bytes and set a cookie.
//
//   Failures are DELIBERATELY COARSE at this boundary. Every refusal comes
//   back as one error. Telling a caller which check failed would help
//   somebody assembling a forgery far more than it helps a person who
//   tapped the wrong key, and the precise reason is logged where an
//   administrator can read it.

using DealerFOSS.Core;

namespace DealerFOSS.Identity;

/// <summary>
/// Passkeys: phishing-resistant credentials that live on a person's device.
/// </summary>
/// <remarks>
/// <para>
/// This exists <b>alongside</b> passwords rather than replacing them. A
/// dealership that locks a service advisor out at eight on a Saturday morning
/// will not forgive it, so passwords and recovery codes stay until somebody has
/// registered enough passkeys to be safe without them — which is a policy
/// decision nobody has made yet, and is not implied by this interface.
/// </para>
/// </remarks>
public interface IPasskeys
{
    /// <summary>
    /// Starts registering a passkey for the signed-in user. Returns the challenge
    /// the browser must hand to the authenticator.
    /// </summary>
    Task<Result<PasskeyRegistrationChallenge>> BeginRegistrationAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Completes registration. The credential is stored only if the response
    /// proves genuine against the challenge issued by
    /// <see cref="BeginRegistrationAsync"/>.
    /// </summary>
    Task<Result<RegisteredPasskey>> FinishRegistrationAsync(
        PasskeyRegistrationResponse response,
        CancellationToken cancellationToken);

    /// <summary>
    /// Starts a passkey sign-in. Takes no email and no password: the credential
    /// in the response is what identifies the account, so this reveals nothing
    /// about who does or does not have one.
    /// </summary>
    Task<Result<PasskeySignInChallenge>> BeginSignInAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Completes a passkey sign-in and issues a session, exactly as a password
    /// sign-in would — the same cookie, the same anti-forgery pair, the same
    /// expiry rules. A second way in must not be a second set of rules.
    /// </summary>
    Task<Result<IssuedSession>> FinishSignInAsync(
        PasskeySignInResponse response,
        CancellationToken cancellationToken);

    /// <summary>The signed-in user's own passkeys. Never anybody else's.</summary>
    Task<Result<IReadOnlyList<RegisteredPasskey>>> ListMineAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Removes one of the signed-in user's own passkeys. Deleted rather than
    /// disabled: a credential that still exists but is ignored is a thing two
    /// people will eventually disagree about.
    /// </summary>
    Task<Result> ForgetAsync(Guid passkeyId, CancellationToken cancellationToken);
}

/// <summary>
/// What the browser needs to ask an authenticator to create a credential. Every
/// value is base64url, which is what the WebAuthn API expects on the wire.
/// </summary>
public sealed record PasskeyRegistrationChallenge(
    Guid ChallengeId,
    string Challenge,
    string RelyingPartyId,
    string RelyingPartyName,
    string UserHandle,
    string UserName,
    string UserDisplayName,

    /// <summary>
    /// Credentials this user already has. The browser uses it to avoid
    /// registering the same authenticator twice, which otherwise produces a
    /// second credential that behaves identically and confuses everybody.
    /// </summary>
    IReadOnlyList<string> AlreadyRegistered);

/// <summary>What comes back from the authenticator, still unverified.</summary>
public sealed record PasskeyRegistrationResponse(
    Guid ChallengeId,
    string ClientDataJson,
    string AttestationObject,

    /// <summary>What the person wants to call it — "work laptop", "phone".</summary>
    string Label);

/// <summary>A challenge for signing in. Carries nothing about any account.</summary>
public sealed record PasskeySignInChallenge(
    Guid ChallengeId,
    string Challenge,
    string RelyingPartyId);

public sealed record PasskeySignInResponse(
    Guid ChallengeId,
    string CredentialId,
    string ClientDataJson,
    string AuthenticatorData,
    string Signature);

/// <summary>
/// A registered passkey as its owner sees it. No key material, because a screen
/// listing credentials has no use for one.
/// </summary>
public sealed record RegisteredPasskey(
    Guid Id,
    string Label,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt);

/// <summary>Stable error codes for passkeys (doc 06 §6).</summary>
public static class PasskeyErrors
{
    /// <summary>
    /// One refusal for every way a ceremony can fail. See the file header: the
    /// precise reason is logged, not returned.
    /// </summary>
    public static Error NotAccepted { get; } = Error.Validation(
        "passkey.not_accepted",
        "That passkey could not be accepted. Try again, or sign in with your password.");

    public static Error ChallengeExpired { get; } = Error.Validation(
        "passkey.challenge_expired",
        "That took too long. Start again.");

    public static Error AlreadyRegistered { get; } = Error.Conflict(
        "passkey.already_registered",
        "That passkey is already registered.");

    public static Error NotFound { get; } = Error.NotFound(
        "passkey.not_found",
        "That passkey is not on this account.");
}
