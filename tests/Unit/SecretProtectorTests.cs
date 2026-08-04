// SecretProtectorTests — proves the thing that stands between a stolen database
// file and every dealer's connection string.
//
// Use:  runs with the normal test suite; no infrastructure required.
// Edit: the tamper test is the one that matters most. Authenticated encryption
//       means an altered value must FAIL rather than decrypt to something wrong,
//       and that property is easy to lose by switching cipher mode.

using System.Security.Cryptography;
using FluentAssertions;
using DealerFOSS.Tenancy;

namespace DealerFOSS.UnitTests;

public sealed class SecretProtectorTests
{
    private const string Connection =
        "Server=sql;Database=DealerFOSS_Tenant_northgroup;User Id=sa;Password=hunter2;Encrypt=True";

    [Fact]
    public void What_goes_in_comes_back_out()
    {
        var protector = Protector();

        var sealed_ = protector.Protect(Connection);

        sealed_.Should().NotContain("hunter2", because: "that is the entire point");
        protector.Unprotect(sealed_).Should().Be(Connection);
    }

    [Fact]
    public void The_same_value_encrypts_differently_every_time()
    {
        // A fresh nonce per call. Identical ciphertext for identical input would
        // tell an attacker which tenants share a connection string.
        var protector = Protector();

        var first = protector.Protect(Connection);
        var second = protector.Protect(Connection);

        first.Should().NotBe(second);
        protector.Unprotect(first).Should().Be(protector.Unprotect(second));
    }

    [Fact]
    public void A_tampered_value_fails_instead_of_decrypting_to_something_wrong()
    {
        var protector = Protector();
        var sealed_ = protector.Protect(Connection);

        // Flip one character of the payload.
        var parts = sealed_.Split(':', 3);
        var payload = parts[2].ToCharArray();
        payload[10] = payload[10] == 'A' ? 'B' : 'A';
        var tampered = $"{parts[0]}:{parts[1]}:{new string(payload)}";

        var read = () => protector.Unprotect(tampered);

        read.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void A_value_from_a_different_key_cannot_be_read()
    {
        var mine = Protector();
        var theirs = new EnvelopeSecretProtector(
            new Dictionary<string, string> { ["2026-07"] = Key() }, "2026-07");

        var read = () => mine.Unprotect(theirs.Protect(Connection));

        read.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void A_value_written_under_an_older_key_still_reads_after_rotation()
    {
        // The reason the key id travels with the value. Rotating must not make
        // every tenant unreachable.
        var oldKey = Key();
        var newKey = Key();

        var before = new EnvelopeSecretProtector(
            new Dictionary<string, string> { ["2026-06"] = oldKey }, "2026-06");
        var sealedLastMonth = before.Protect(Connection);

        var after = new EnvelopeSecretProtector(
            new Dictionary<string, string> { ["2026-06"] = oldKey, ["2026-07"] = newKey },
            currentKeyId: "2026-07");

        after.Unprotect(sealedLastMonth).Should().Be(Connection,
            because: "old values must stay readable while they are re-encrypted");

        // And new values use the new key.
        after.Protect(Connection).Should().StartWith("dfoss.v1:2026-07:");
    }

    [Fact]
    public void Retiring_a_key_that_data_still_uses_says_so_plainly()
    {
        var retired = new EnvelopeSecretProtector(
            new Dictionary<string, string> { ["2026-06"] = Key() }, "2026-06");
        var sealed_ = retired.Protect(Connection);

        var onlyNewKey = new EnvelopeSecretProtector(
            new Dictionary<string, string> { ["2026-07"] = Key() }, "2026-07");

        var read = () => onlyNewKey.Unprotect(sealed_);

        read.Should().Throw<CryptographicException>().WithMessage("*not configured*");
    }

    [Fact]
    public void A_plaintext_value_left_by_development_is_recognised_rather_than_mangled()
    {
        var protector = Protector();

        var read = () => protector.Unprotect(Connection);

        read.Should().Throw<CryptographicException>().WithMessage("*migrated*");
        EnvelopeSecretProtector.IsProtected(Connection).Should().BeFalse();
        EnvelopeSecretProtector.IsProtected(protector.Protect(Connection)).Should().BeTrue();
    }

    [Theory]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(64)]
    public void A_key_that_is_not_256_bits_is_refused_at_startup(int bytes)
    {
        var build = () => new EnvelopeSecretProtector(
            new Dictionary<string, string> { ["k"] = Convert.ToBase64String(new byte[bytes]) }, "k");

        build.Should().Throw<ArgumentException>().WithMessage("*32*");
    }

    [Fact]
    public void A_current_key_that_is_not_in_the_key_set_is_refused_at_startup()
    {
        var build = () => new EnvelopeSecretProtector(
            new Dictionary<string, string> { ["2026-06"] = Key() }, "2026-07");

        build.Should().Throw<ArgumentException>().WithMessage("*not one of the configured keys*");
    }

    [Fact]
    public void No_keys_at_all_is_refused_at_startup()
    {
        var build = () => new EnvelopeSecretProtector(new Dictionary<string, string>(), "k");

        build.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Nonsense_that_is_not_base64_is_refused_at_startup()
    {
        var build = () => new EnvelopeSecretProtector(
            new Dictionary<string, string> { ["k"] = "not base64 at all!" }, "k");

        build.Should().Throw<ArgumentException>().WithMessage("*base64*");
    }

    private static EnvelopeSecretProtector Protector() =>
        new(new Dictionary<string, string> { ["2026-07"] = Key() }, "2026-07");

    private static string Key() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
