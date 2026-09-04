// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   WebAuthnTests — proves the passkey verification refuses what it must refuse.
//
// Usage:
//   Runs with the normal unit suite. No database, no HTTP.
//
// Coding Instructions:
//   The happy path is one test. The other nine are the point. Each one
//   breaks exactly one property of the ceremony and asserts the specific
//   refusal, because "it rejected something" is not evidence that it
//   rejected it for the right reason — a verifier that returns
//   MalformedResponse for everything would pass a sloppier suite.

using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using DealerFOSS.Identity;

namespace DealerFOSS.UnitTests;

public sealed class WebAuthnTests
{
    private const string RelyingParty = "dealerfoss.example";
    private const string Origin = "https://dealerfoss.example";

    private static readonly string[] AllowedOrigins = [Origin];

    [Fact]
    public void A_genuine_registration_yields_a_credential_and_a_usable_key()
    {
        using var authenticator = new FakeAuthenticator(RelyingParty);
        var challenge = RandomNumberGenerator.GetBytes(32);
        var (clientData, attestation) = authenticator.Register(challenge, Origin);

        var failure = WebAuthn.TryVerifyRegistration(
            clientData, attestation, challenge, RelyingParty, AllowedOrigins, out var registration);

        failure.Should().Be(WebAuthnFailure.None);
        registration.Should().NotBeNull();
        registration!.CredentialId.Should().Equal(authenticator.CredentialId);
        registration.Algorithm.Should().Be(CoseAlgorithm.Es256);

        // The key has to be genuinely importable, not merely non-empty: this is
        // the artefact every future sign-in is checked against.
        using var key = ECDsa.Create();
        var import = () => key.ImportSubjectPublicKeyInfo(registration.PublicKeySpki, out _);
        import.Should().NotThrow();
    }

    [Fact]
    public void A_genuine_sign_in_is_accepted_and_carries_the_counter_forward()
    {
        var (authenticator, credential) = Registered();
        using var owned = authenticator;

        authenticator.Counter = 7;
        var challenge = RandomNumberGenerator.GetBytes(32);
        var (clientData, authData, signature) = authenticator.Sign(challenge, Origin);

        var failure = WebAuthn.TryVerifyAssertion(
            clientData, authData, signature, challenge, RelyingParty, AllowedOrigins,
            credential.PublicKeySpki, credential.Algorithm, storedSignCount: 3, out var counter);

        failure.Should().Be(WebAuthnFailure.None);
        counter.Should().Be(7);
    }

    /// <summary>
    /// The anti-phishing property, and the single most important test here. The
    /// browser writes the real origin into the client data, so a passkey used on
    /// a lookalike site produces a response naming that site — which must not
    /// verify no matter how well formed everything else is.
    /// </summary>
    [Fact]
    public void A_response_from_a_lookalike_site_is_refused()
    {
        var (authenticator, credential) = Registered();
        using var owned = authenticator;

        var challenge = RandomNumberGenerator.GetBytes(32);
        var (clientData, authData, signature) =
            authenticator.Sign(challenge, "https://dealerfoss.example.evil.test");

        var failure = WebAuthn.TryVerifyAssertion(
            clientData, authData, signature, challenge, RelyingParty, AllowedOrigins,
            credential.PublicKeySpki, credential.Algorithm, 0, out _);

        failure.Should().Be(WebAuthnFailure.UntrustedOrigin);
    }

    /// <summary>
    /// A recorded response replayed later must fail, which is what the
    /// server-issued challenge is for.
    /// </summary>
    [Fact]
    public void A_response_to_somebody_elses_challenge_is_refused()
    {
        var (authenticator, credential) = Registered();
        using var owned = authenticator;

        var (clientData, authData, signature) =
            authenticator.Sign(RandomNumberGenerator.GetBytes(32), Origin);

        var failure = WebAuthn.TryVerifyAssertion(
            clientData, authData, signature, RandomNumberGenerator.GetBytes(32),
            RelyingParty, AllowedOrigins, credential.PublicKeySpki, credential.Algorithm, 0, out _);

        failure.Should().Be(WebAuthnFailure.ChallengeMismatch);
    }

    [Fact]
    public void A_signature_over_different_bytes_is_refused()
    {
        var (authenticator, credential) = Registered();
        using var owned = authenticator;

        var challenge = RandomNumberGenerator.GetBytes(32);
        var (clientData, authData, signature) =
            authenticator.Sign(challenge, Origin, signOverJunk: true);

        var failure = WebAuthn.TryVerifyAssertion(
            clientData, authData, signature, challenge, RelyingParty, AllowedOrigins,
            credential.PublicKeySpki, credential.Algorithm, 0, out _);

        failure.Should().Be(WebAuthnFailure.BadSignature);
    }

    /// <summary>
    /// A key that did not sign this must not verify it. Obvious, and exactly the
    /// thing that silently stops being true if the stored key is ever looked up
    /// by the wrong column.
    /// </summary>
    [Fact]
    public void Another_authenticators_key_does_not_verify_this_signature()
    {
        var (authenticator, _) = Registered();
        using var owned = authenticator;

        var (stranger, strangerCredential) = Registered();
        using var ownedStranger = stranger;

        var challenge = RandomNumberGenerator.GetBytes(32);
        var (clientData, authData, signature) = authenticator.Sign(challenge, Origin);

        var failure = WebAuthn.TryVerifyAssertion(
            clientData, authData, signature, challenge, RelyingParty, AllowedOrigins,
            strangerCredential.PublicKeySpki, strangerCredential.Algorithm, 0, out _);

        failure.Should().Be(WebAuthnFailure.BadSignature);
    }

    [Fact]
    public void A_response_for_a_different_relying_party_is_refused()
    {
        var (authenticator, credential) = Registered();
        using var owned = authenticator;

        var challenge = RandomNumberGenerator.GetBytes(32);
        var (clientData, authData, signature) =
            authenticator.Sign(challenge, Origin, relyingPartyId: "someone-else.example");

        var failure = WebAuthn.TryVerifyAssertion(
            clientData, authData, signature, challenge, RelyingParty, AllowedOrigins,
            credential.PublicKeySpki, credential.Algorithm, 0, out _);

        failure.Should().Be(WebAuthnFailure.WrongRelyingParty);
    }

    [Fact]
    public void A_response_where_nobody_touched_the_key_is_refused()
    {
        var (authenticator, credential) = Registered();
        using var owned = authenticator;

        var challenge = RandomNumberGenerator.GetBytes(32);
        var (clientData, authData, signature) =
            authenticator.Sign(challenge, Origin, userPresent: false);

        var failure = WebAuthn.TryVerifyAssertion(
            clientData, authData, signature, challenge, RelyingParty, AllowedOrigins,
            credential.PublicKeySpki, credential.Algorithm, 0, out _);

        failure.Should().Be(WebAuthnFailure.UserNotPresent);
    }

    /// <summary>
    /// The only clone detection the standard offers. An authenticator that counts
    /// must count upwards; a repeat means two copies of a credential that was
    /// supposed to be unclonable.
    /// </summary>
    [Fact]
    public void A_counter_that_does_not_advance_is_refused_as_a_clone()
    {
        var (authenticator, credential) = Registered();
        using var owned = authenticator;

        authenticator.Counter = 5;
        var challenge = RandomNumberGenerator.GetBytes(32);
        var (clientData, authData, signature) = authenticator.Sign(challenge, Origin);

        var failure = WebAuthn.TryVerifyAssertion(
            clientData, authData, signature, challenge, RelyingParty, AllowedOrigins,
            credential.PublicKeySpki, credential.Algorithm, storedSignCount: 5, out _);

        failure.Should().Be(WebAuthnFailure.CounterWentBackwards);
    }

    /// <summary>
    /// Platform passkeys commonly do not count at all and send zero every time.
    /// Treating that as a clone would refuse most passkeys in existence.
    /// </summary>
    [Fact]
    public void An_authenticator_that_does_not_count_at_all_is_accepted()
    {
        var (authenticator, credential) = Registered();
        using var owned = authenticator;

        authenticator.Counter = 0;
        var challenge = RandomNumberGenerator.GetBytes(32);
        var (clientData, authData, signature) = authenticator.Sign(challenge, Origin);

        var failure = WebAuthn.TryVerifyAssertion(
            clientData, authData, signature, challenge, RelyingParty, AllowedOrigins,
            credential.PublicKeySpki, credential.Algorithm, storedSignCount: 0, out _);

        failure.Should().Be(WebAuthnFailure.None);
    }

    /// <summary>
    /// A registration response replayed as a sign-in must not work. The ceremony
    /// type is the only thing that distinguishes them.
    /// </summary>
    [Fact]
    public void A_registration_cannot_be_replayed_as_a_sign_in()
    {
        using var authenticator = new FakeAuthenticator(RelyingParty);
        var challenge = RandomNumberGenerator.GetBytes(32);
        var (clientData, _) = authenticator.Register(challenge, Origin);

        var failure = WebAuthn.TryVerifyAssertion(
            clientData, new byte[37], [], challenge, RelyingParty, AllowedOrigins,
            [], CoseAlgorithm.Es256, 0, out _);

        failure.Should().Be(WebAuthnFailure.WrongCeremonyType);
    }

    /// <summary>
    /// Attestation formats other than "none" are refused rather than ignored.
    /// Accepting one we do not verify would imply a guarantee we are not making.
    /// </summary>
    [Fact]
    public void An_attestation_format_we_do_not_verify_is_refused_rather_than_ignored()
    {
        using var authenticator = new FakeAuthenticator(RelyingParty);
        var challenge = RandomNumberGenerator.GetBytes(32);
        var (clientData, attestation) = authenticator.Register(challenge, Origin, "packed");

        var failure = WebAuthn.TryVerifyRegistration(
            clientData, attestation, challenge, RelyingParty, AllowedOrigins, out var registration);

        failure.Should().Be(WebAuthnFailure.UnsupportedAttestation);
        registration.Should().BeNull();
    }

    [Fact]
    public void Rubbish_is_refused_without_throwing()
    {
        var nonsense = Encoding.UTF8.GetBytes("this is not CBOR and never was");

        var failure = WebAuthn.TryVerifyRegistration(
            nonsense, nonsense, [1, 2, 3], RelyingParty, AllowedOrigins, out var registration);

        failure.Should().Be(WebAuthnFailure.MalformedResponse);
        registration.Should().BeNull();
    }

    /// <summary>A registered authenticator and the credential it produced.</summary>
    private static (FakeAuthenticator Authenticator, VerifiedRegistration Credential) Registered()
    {
        var authenticator = new FakeAuthenticator(RelyingParty);
        var challenge = RandomNumberGenerator.GetBytes(32);
        var (clientData, attestation) = authenticator.Register(challenge, Origin);

        WebAuthn.TryVerifyRegistration(
            clientData, attestation, challenge, RelyingParty, AllowedOrigins, out var credential)
            .Should().Be(WebAuthnFailure.None);

        return (authenticator, credential!);
    }
}
