// Totp — time-based one-time codes, RFC 6238. The maths behind the six digits in
// an authenticator app.
//
// Use:  Totp.Verify(secret, code, now) at sign-in; Totp.NewSecret() at enrolment.
// Edit: three details are not adjustable without breaking every authenticator
//       app in the world: HMAC-SHA1, a 30-second step, and 6 digits. They look
//       dated and they are what Google Authenticator, Authy, 1Password and the
//       rest implement.
//
//       The verification window is deliberately one step either side. Wider
//       accepts a code for longer after it is shown on screen — including to
//       somebody reading it over a shoulder — and narrower rejects users whose
//       phone clock is a few seconds out, which is most of them.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OpenDealer360.Identity;

internal static class Totp
{
    private const int Digits = 6;
    private const int StepSeconds = 30;

    /// <summary>How many steps either side of now are accepted.</summary>
    private const int WindowSteps = 1;

    /// <summary>160 bits, the size RFC 4226 recommends for the shared secret.</summary>
    private const int SecretBytes = 20;

    /// <summary>A new shared secret, base32-encoded as authenticator apps expect.</summary>
    public static string NewSecret() => Base32.Encode(RandomNumberGenerator.GetBytes(SecretBytes));

    /// <summary>
    /// The URI an authenticator app reads from a QR code. The issuer appears as
    /// the account name in the app, so it wants to be recognisable to the person
    /// scanning it rather than to us.
    /// </summary>
    public static string EnrolmentUri(string secret, string issuer, string account)
    {
        var label = Uri.EscapeDataString($"{issuer}:{account}");
        var query = $"secret={secret}&issuer={Uri.EscapeDataString(issuer)}&digits={Digits}&period={StepSeconds}";

        return $"otpauth://totp/{label}?{query}";
    }

    /// <summary>
    /// Whether the code matches, allowing for a phone clock slightly out of step.
    /// Comparison is constant-time: a timing difference would let an attacker
    /// discover the code one digit at a time.
    /// </summary>
    public static bool Verify(string secret, string? code, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var candidate = code.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        if (candidate.Length != Digits || !candidate.All(char.IsAsciiDigit))
        {
            return false;
        }

        byte[] key;
        try
        {
            key = Base32.Decode(secret);
        }
        catch (FormatException)
        {
            return false;
        }

        var step = now.ToUnixTimeSeconds() / StepSeconds;

        for (var offset = -WindowSteps; offset <= WindowSteps; offset++)
        {
            var expected = Generate(key, step + offset);

            if (CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(candidate)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The code for a given step. Exposed so tests can compute one.</summary>
    public static string Generate(string secret, DateTimeOffset at) =>
        Generate(Base32.Decode(secret), at.ToUnixTimeSeconds() / StepSeconds);

    [SuppressMessage(
        "Security",
        "CA5350:Do not use weak cryptographic algorithms",
        Justification =
            "RFC 6238 specifies HMAC-SHA1 for TOTP, and every authenticator app implements " +
            "only that. Using a stronger hash here would produce codes no phone can generate. " +
            "The weakness the rule warns about — collision resistance — is not what HMAC " +
            "depends on, and the secret is 160 bits of fresh randomness per user.")]
    private static string Generate(byte[] key, long step)
    {
        var counter = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counter);
        }

        var hash = HMACSHA1.HashData(key, counter);

        // Dynamic truncation, RFC 4226 §5.3: the low nibble of the last byte
        // picks where in the hash to read from.
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);

        return (binary % 1_000_000).ToString(CultureInfo.InvariantCulture).PadLeft(Digits, '0');
    }
}

/// <summary>
/// Base32 as RFC 4648, without padding. Authenticator apps read this alphabet and
/// no other, which is why it is here rather than base64.
/// </summary>
internal static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(byte[] data)
    {
        var builder = new StringBuilder();
        var buffer = 0;
        var bits = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;

            while (bits >= 5)
            {
                builder.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            builder.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        }

        return builder.ToString();
    }

    public static byte[] Decode(string encoded)
    {
        var cleaned = encoded.Trim().TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>(cleaned.Length * 5 / 8);
        var buffer = 0;
        var bits = 0;

        foreach (var c in cleaned)
        {
            var index = Alphabet.IndexOf(c, StringComparison.Ordinal);
            if (index < 0)
            {
                throw new FormatException($"'{c}' is not a base32 character.");
            }

            buffer = (buffer << 5) | index;
            bits += 5;

            if (bits >= 8)
            {
                bytes.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        return [.. bytes];
    }
}
