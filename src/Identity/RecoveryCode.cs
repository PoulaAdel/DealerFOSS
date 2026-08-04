// RecoveryCode — a one-time way back in when the phone with the authenticator on
// it is lost, broken, or in a taxi.
//
// Use:  issued as a set when the second factor is confirmed, and shown to the
//       user exactly once.
// Edit: only the hash is stored, for the same reason as a password — a stolen
//       database must not contain a working way past the second factor. A code
//       is consumed on use and never reissued.

using System.Security.Cryptography;
using System.Text;

namespace DealerFOSS.Identity;

internal sealed class RecoveryCode
{
    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>SHA-256 of the code. The code itself is never stored.</summary>
    public string CodeHash { get; private set; } = string.Empty;

    public DateTimeOffset? UsedAt { get; private set; }

    public bool IsAvailable => UsedAt is null;

    private RecoveryCode()
    {
    }

    private RecoveryCode(Guid id, Guid userId, string codeHash)
    {
        Id = id;
        UserId = userId;
        CodeHash = codeHash;
    }

    public void Consume(DateTimeOffset at) => UsedAt = at;

    /// <summary>
    /// Generates a set of codes, returning the plaintext for the user to keep and
    /// the records to store. This is the only moment the plaintext exists.
    /// </summary>
    public static (IReadOnlyList<string> Plaintext, IReadOnlyList<RecoveryCode> Records) Issue(
        Guid userId,
        int count = 10)
    {
        var plaintext = new List<string>(count);
        var records = new List<RecoveryCode>(count);

        for (var i = 0; i < count; i++)
        {
            // Ten characters from an unambiguous alphabet: no O/0 or I/1, because
            // these get read off paper and typed by somebody already having a bad
            // day.
            var code = Random("ABCDEFGHJKLMNPQRSTUVWXYZ23456789", 10);
            plaintext.Add(code);
            records.Add(new RecoveryCode(Guid.NewGuid(), userId, Hash(code)));
        }

        return (plaintext, records);
    }

    public static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(code.Trim().ToUpperInvariant().Replace("-", string.Empty, StringComparison.Ordinal))));

    private static string Random(string alphabet, int length)
    {
        var builder = new StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            builder.Append(alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]);
        }

        return builder.ToString();
    }
}
