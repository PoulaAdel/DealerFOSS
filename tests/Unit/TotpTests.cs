// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   TotpTests — proves the code generation matches RFC 6238, because the other
//   implementation is on somebody's phone and cannot be adjusted to agree with us.
//
// Usage:
//   Runs with the normal test suite; no infrastructure required.
//
// Coding Instructions:
//   The RFC test vector is the important one. If it fails, every
//   authenticator app in the world disagrees with this code, and no amount
//   of local testing would have told us.

using FluentAssertions;
using DealerFOSS.Identity;

namespace DealerFOSS.UnitTests;

public sealed class TotpTests
{
    /// <summary>
    /// RFC 6238 Appendix B uses the ASCII secret "12345678901234567890". That is
    /// base32 GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ.
    /// </summary>
    private const string RfcSecret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    [Theory]
    [InlineData(59, "287082")]
    [InlineData(1111111109, "081804")]
    [InlineData(1111111111, "050471")]
    [InlineData(1234567890, "005924")]
    [InlineData(2000000000, "279037")]
    public void Codes_match_the_published_rfc_test_vectors(long unixSeconds, string expected)
    {
        // If this fails, phones and this code disagree and nobody can sign in.
        var at = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

        Totp.Generate(RfcSecret, at).Should().Be(expected);
    }

    [Fact]
    public void A_freshly_generated_code_verifies()
    {
        var secret = Totp.NewSecret();
        var now = DateTimeOffset.UtcNow;

        Totp.Verify(secret, Totp.Generate(secret, now), now).Should().BeTrue();
    }

    [Fact]
    public void A_code_from_the_previous_step_still_works()
    {
        // Phone clocks drift, and a code typed as it rolls over must not be
        // rejected.
        var secret = Totp.NewSecret();
        var now = DateTimeOffset.UtcNow;

        Totp.Verify(secret, Totp.Generate(secret, now.AddSeconds(-30)), now).Should().BeTrue();
    }

    [Fact]
    public void A_code_from_a_few_minutes_ago_does_not()
    {
        var secret = Totp.NewSecret();
        var now = DateTimeOffset.UtcNow;

        Totp.Verify(secret, Totp.Generate(secret, now.AddMinutes(-5)), now).Should().BeFalse(
            because: "an old code read over a shoulder must stop working quickly");
    }

    [Fact]
    public void A_code_for_a_different_secret_does_not_verify()
    {
        var mine = Totp.NewSecret();
        var theirs = Totp.NewSecret();
        var now = DateTimeOffset.UtcNow;

        Totp.Verify(mine, Totp.Generate(theirs, now), now).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("abcdef")]
    [InlineData(null)]
    public void Nonsense_is_rejected_rather_than_throwing(string? code)
    {
        var secret = Totp.NewSecret();

        Totp.Verify(secret, code, DateTimeOffset.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void A_code_typed_with_a_space_in_the_middle_still_works()
    {
        // Authenticator apps display "123 456", and people type what they see.
        var secret = Totp.NewSecret();
        var now = DateTimeOffset.UtcNow;
        var code = Totp.Generate(secret, now);
        var spaced = code[..3] + " " + code[3..];

        Totp.Verify(secret, spaced, now).Should().BeTrue();
    }

    [Fact]
    public void The_enrolment_uri_is_what_an_authenticator_app_expects()
    {
        var secret = Totp.NewSecret();

        var uri = Totp.EnrolmentUri(secret, "DealerFOSS", "gm@dev.local");

        uri.Should().StartWith("otpauth://totp/");
        uri.Should().Contain($"secret={secret}");
        uri.Should().Contain("issuer=DealerFOSS");
        uri.Should().Contain("digits=6");
        uri.Should().Contain("period=30");
    }

    [Fact]
    public void Every_enrolment_gets_its_own_secret()
    {
        Totp.NewSecret().Should().NotBe(Totp.NewSecret());
    }
}
