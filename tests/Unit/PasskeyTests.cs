// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   PasskeyTests — the rules the credential record enforces on its own.
//
// Usage:
//   Runs with the normal unit suite.
//
// Coding Instructions:
//   The counter rule is duplicated here and in WebAuthn.cs on purpose. The
//   verifier checks it on the sign-in path; this checks it on any path at
//   all. Two copies of a rule is usually a defect — this one is a deliberate
//   second lock on the only clone detection the standard offers.

using System.Security.Cryptography;
using FluentAssertions;
using DealerFOSS.Identity;

namespace DealerFOSS.UnitTests;

public sealed class PasskeyTests
{
    [Fact]
    public void A_passkey_starts_unused_and_takes_the_label_it_was_given()
    {
        var passkey = Make(label: "  Work laptop  ");

        passkey.Label.Should().Be("Work laptop");
        passkey.LastUsedAt.Should().BeNull(
            because: "a credential registered and never used is worth showing as exactly that");
    }

    [Fact]
    public void A_passkey_with_no_label_is_still_recognisable_in_a_list()
    {
        Make(label: "   ").Label.Should().Be("Passkey");
    }

    [Fact]
    public void Using_a_passkey_carries_the_counter_forward_and_records_when()
    {
        var passkey = Make(signCount: 4);
        var at = DateTimeOffset.UtcNow;

        passkey.RecordUse(9, at);

        passkey.Counter.Should().Be(9u);
        passkey.LastUsedAt.Should().Be(at);
    }

    /// <summary>
    /// The clone signal. A counter that repeats means two copies of a credential
    /// that was supposed to exist once.
    /// </summary>
    [Fact]
    public void A_counter_that_does_not_advance_is_refused()
    {
        var passkey = Make(signCount: 9);

        var reuse = () => passkey.RecordUse(9, DateTimeOffset.UtcNow);
        reuse.Should().Throw<InvalidOperationException>().WithMessage("*must advance*");

        var rewind = () => passkey.RecordUse(2, DateTimeOffset.UtcNow);
        rewind.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Platform passkeys commonly do not count and send zero every time.
    /// Refusing that would refuse most passkeys in existence.
    /// </summary>
    [Fact]
    public void An_authenticator_that_never_counts_can_keep_signing_in()
    {
        var passkey = Make(signCount: 0);

        passkey.RecordUse(0, DateTimeOffset.UtcNow);
        passkey.RecordUse(0, DateTimeOffset.UtcNow);

        passkey.Counter.Should().Be(0u);
    }

    [Fact]
    public void A_credential_without_an_id_or_a_key_is_refused()
    {
        var noId = () => new Passkey(
            Guid.NewGuid(), Guid.NewGuid(), [], [1], CoseAlgorithm.Es256, 0, "x", DateTimeOffset.UtcNow);
        noId.Should().Throw<ArgumentException>();

        var noKey = () => new Passkey(
            Guid.NewGuid(), Guid.NewGuid(), [1], [], CoseAlgorithm.Es256, 0, "x", DateTimeOffset.UtcNow);
        noKey.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_challenge_is_single_use_and_expires()
    {
        var now = DateTimeOffset.UtcNow;
        var challenge = new PasskeyChallenge(
            Guid.NewGuid(), RandomNumberGenerator.GetBytes(32), forRegistration: false, null, now);

        challenge.IsUsableAt(now).Should().BeTrue();
        challenge.IsUsableAt(now + PasskeyChallenge.Lifetime).Should().BeFalse(
            because: "a challenge left on a screen must not be a standing invitation");

        challenge.Consume(now);
        challenge.IsUsableAt(now).Should().BeFalse(
            because: "replaying a recorded ceremony is exactly what the challenge prevents");
    }

    /// <summary>
    /// A short challenge is guessable, and a guessable challenge removes the
    /// freshness the whole scheme depends on.
    /// </summary>
    [Fact]
    public void A_challenge_too_short_to_be_unguessable_is_refused()
    {
        var tooShort = () => new PasskeyChallenge(
            Guid.NewGuid(), [1, 2, 3, 4], forRegistration: false, null, DateTimeOffset.UtcNow);

        tooShort.Should().Throw<ArgumentException>().WithMessage("*guessable*");
    }

    private static Passkey Make(string label = "Phone", uint signCount = 0) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        RandomNumberGenerator.GetBytes(32),
        RandomNumberGenerator.GetBytes(91),
        CoseAlgorithm.Es256,
        signCount,
        label,
        DateTimeOffset.UtcNow);
}
