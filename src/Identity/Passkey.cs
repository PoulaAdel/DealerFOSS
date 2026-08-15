// Passkey — one registered credential, and the state that keeps it honest.
//
// Use:  created by PasskeyDirectory once WebAuthn has proven a registration.
// Edit: TWO fields carry security meaning and are not bookkeeping.
//
//       SignCount is clone detection. It only ever moves forward, and the domain
//       refuses to move it backwards rather than trusting the caller to check —
//       a repeated count is the one signal the standard gives that a credential
//       supposed to be unclonable has been copied.
//
//       PublicKeySpki is a PUBLIC key and nothing else. There is no private
//       material here, which is the whole point of the scheme: a stolen copy of
//       this table lets an attacker verify signatures, not produce them. That is
//       why a passkey survives a database breach and a password hash only
//       resists one.
//
//       There is deliberately no "disabled" flag. A passkey somebody no longer
//       wants is deleted, because a credential that still exists but is ignored
//       is a thing two people will disagree about.

namespace DealerFOSS.Identity;

internal sealed class Passkey
{
    private Passkey()
    {
    }

    public Passkey(
        Guid id,
        Guid userId,
        byte[] credentialId,
        byte[] publicKeySpki,
        CoseAlgorithm algorithm,
        uint signCount,
        string label,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(credentialId);
        ArgumentNullException.ThrowIfNull(publicKeySpki);

        if (credentialId.Length == 0)
        {
            throw new ArgumentException("A credential needs an id.", nameof(credentialId));
        }

        if (publicKeySpki.Length == 0)
        {
            throw new ArgumentException("A credential needs a public key.", nameof(publicKeySpki));
        }

        Id = id;
        UserId = userId;
        CredentialId = credentialId;
        PublicKeySpki = publicKeySpki;
        Algorithm = algorithm;
        SignCount = signCount;
        Label = Describe(label);
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>
    /// What the authenticator calls this credential. Unique across the whole
    /// dealership: two users cannot hold the same credential id, and the sign-in
    /// path finds the account from this alone.
    /// </summary>
    public byte[] CredentialId { get; private set; } = [];

    /// <summary>The public half, in SubjectPublicKeyInfo form. See the header.</summary>
    public byte[] PublicKeySpki { get; private set; } = [];

    public CoseAlgorithm Algorithm { get; private set; }

    /// <summary>
    /// Stored as a signed 64-bit value because the database has no unsigned type,
    /// while the protocol counter is a uint. The conversion is confined to this
    /// class so nothing outside has to remember which is which.
    /// </summary>
    public long SignCount { get; private set; }

    /// <summary>
    /// What the person calls it — "work laptop", "phone". Their word, so a list
    /// of three credentials is a list they can actually choose from.
    /// </summary>
    public string Label { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Null until it has signed somebody in. A passkey registered and never used
    /// is worth showing as exactly that.
    /// </summary>
    public DateTimeOffset? LastUsedAt { get; private set; }

    public uint Counter => (uint)SignCount;

    /// <summary>
    /// Records a successful sign-in. Refuses a counter that has not advanced:
    /// the verifier checks this too, and having the rule in both places is
    /// deliberate — this is the copy that survives somebody calling the domain
    /// directly.
    /// </summary>
    public void RecordUse(uint counter, DateTimeOffset at)
    {
        if (counter != 0 && counter <= SignCount)
        {
            throw new InvalidOperationException(
                $"A passkey's counter must advance. Stored {SignCount}, received {counter}.");
        }

        SignCount = counter;
        LastUsedAt = at;
    }

    public void Rename(string label) => Label = Describe(label);

    private static string Describe(string label) =>
        string.IsNullOrWhiteSpace(label) ? "Passkey" : label.Trim();
}

/// <summary>
/// A challenge issued for one WebAuthn ceremony.
/// </summary>
/// <remarks>
/// <para>
/// The freshness of the whole scheme rests on this row: the authenticator signs
/// a number we chose, so a recording of a previous sign-in cannot be replayed.
/// It is therefore <b>single-use and short-lived</b>, and consuming it is part of
/// the same transaction that accepts the response.
/// </para>
/// <para>
/// <see cref="UserId"/> is null for sign-in, because at the moment the challenge
/// is issued nobody has said who they are yet — the credential in the response
/// is what identifies the account. It is set for registration, where the person
/// is already signed in and the credential is being added to a known account.
/// </para>
/// </remarks>
internal sealed class PasskeyChallenge
{
    /// <summary>
    /// Long enough to find a phone, cross a workshop, and touch a sensor. Short
    /// enough that a challenge left on a screen is not a standing invitation.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private PasskeyChallenge()
    {
    }

    public PasskeyChallenge(Guid id, byte[] challenge, bool forRegistration, Guid? userId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        if (challenge.Length < 16)
        {
            throw new ArgumentException(
                "A challenge shorter than 16 bytes is guessable.", nameof(challenge));
        }

        Id = id;
        Challenge = challenge;
        ForRegistration = forRegistration;
        UserId = userId;
        ExpiresAt = now.Add(Lifetime);
    }

    public Guid Id { get; private set; }

    public byte[] Challenge { get; private set; } = [];

    /// <summary>
    /// Which ceremony this was issued for. Checked on the way back in, so a
    /// registration challenge cannot be redeemed as a sign-in.
    /// </summary>
    public bool ForRegistration { get; private set; }

    public Guid? UserId { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public bool IsUsableAt(DateTimeOffset now) => ConsumedAt is null && now < ExpiresAt;

    public void Consume(DateTimeOffset at) => ConsumedAt = at;
}
