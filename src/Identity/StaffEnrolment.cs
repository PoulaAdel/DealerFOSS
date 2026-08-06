// StaffEnrolment — the one-time code a starter uses to set their own password.
//
// Use:  created by StaffDirectoryService when a manager issues one; consumed when
//       the starter redeems it.
// Edit: this is a credential in its own right, and the same rules as
//       SignInChallenge apply — stored hashed, lives for hours rather than days,
//       single use, and capped on wrong guesses. It is longer-lived than a
//       sign-in challenge because it has to survive somebody being handed a code
//       and getting to a computer, and shorter than a working week because a code
//       that outlives the conversation is a password lying on a desk.
//
//       Issuing a new code supersedes any outstanding one. Two live codes for one
//       account doubles the guessing surface for no benefit.

namespace DealerFOSS.Identity;

internal sealed class StaffEnrolment
{
    /// <summary>Long enough to walk to a computer, short enough not to be a spare key.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    /// <summary>
    /// Wrong attempts tolerated before the code dies. The code itself is long
    /// enough that guessing is hopeless; the cap exists so that a code being
    /// hammered stops working rather than standing there indefinitely.
    /// </summary>
    public const int MaxAttempts = 5;

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string CodeHash { get; private set; } = string.Empty;

    public DateTimeOffset IssuedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public int FailedAttempts { get; private set; }

    /// <summary>Who handed it over. The audit trail names them; this is the row's own copy.</summary>
    public Guid IssuedByUserId { get; private set; }

    private StaffEnrolment()
    {
    }

    public StaffEnrolment(Guid id, Guid userId, string codeHash, Guid issuedByUserId, DateTimeOffset now)
    {
        Id = id;
        UserId = userId;
        CodeHash = codeHash;
        IssuedByUserId = issuedByUserId;
        IssuedAt = now;
        ExpiresAt = now.Add(Lifetime);
    }

    public bool IsUsableAt(DateTimeOffset now) =>
        ConsumedAt is null && FailedAttempts < MaxAttempts && now < ExpiresAt;

    public void RecordFailure() => FailedAttempts++;

    public void Consume(DateTimeOffset at) => ConsumedAt = at;

    /// <summary>Kills an outstanding code because a newer one has been issued.</summary>
    public void Supersede(DateTimeOffset at) => ConsumedAt = at;
}
