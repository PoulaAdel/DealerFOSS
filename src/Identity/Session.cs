// Session — a durable record of one signed-in browser, held in SQL so it can be
// revoked immediately rather than waiting for a token to expire.
//
// Use:  created by SignInService; validated on every request. The raw token is
//       never stored — only its hash — so a database leak cannot be replayed.
// Edit: expiry is two clocks at once. Idle expiry slides forward with activity;
//       absolute expiry never moves, so a session cannot be kept alive forever
//       by staying busy. Both must be checked, and revocation must beat both.
//
//       A session carries two independent secrets: the session token, which the
//       browser never sees in script, and the anti-forgery token, which it must
//       read and echo back on every write. Both are stored hashed, and both die
//       together when the session is revoked.

using OpenDealer360.Core;

namespace OpenDealer360.Identity;

internal sealed class Session : AuditableEntity
{
    /// <summary>How long a session may sit unused before it stops working.</summary>
    public static TimeSpan IdleTimeout { get; } = TimeSpan.FromMinutes(30);

    /// <summary>The longest a session may live regardless of activity (one shift).</summary>
    public static TimeSpan AbsoluteTimeout { get; } = TimeSpan.FromHours(8);

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>SHA-256 of the token handed to the browser. The token itself is never stored.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    /// <summary>
    /// SHA-256 of this session's anti-forgery token. Bound to the session rather
    /// than free-standing, so a token minted for one session cannot authorize a
    /// write on another — which is the hole a plain double-submit cookie leaves
    /// open to anything that can write cookies for this site.
    /// </summary>
    public string AntiForgeryHash { get; private set; } = string.Empty;

    public DateTimeOffset IssuedAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    /// <summary>Hard ceiling, fixed at sign-in and never extended.</summary>
    public DateTimeOffset AbsoluteExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Coarse device description for the user's own session list. Never a full user-agent.</summary>
    public string? DeviceSummary { get; private set; }

    private Session()
    {
    }

    public Session(
        Guid id,
        Guid userId,
        string tokenHash,
        string antiForgeryHash,
        DateTimeOffset now,
        string? deviceSummary)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("A session needs a token hash.", nameof(tokenHash));
        }

        if (string.IsNullOrWhiteSpace(antiForgeryHash))
        {
            throw new ArgumentException(
                "A session needs an anti-forgery hash, or its writes cannot be protected.",
                nameof(antiForgeryHash));
        }

        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        AntiForgeryHash = antiForgeryHash;
        IssuedAt = now;
        LastSeenAt = now;
        AbsoluteExpiresAt = now.Add(AbsoluteTimeout);
        DeviceSummary = deviceSummary;
    }

    public bool IsRevoked => RevokedAt is not null;

    /// <summary>
    /// Whether the session may still be used at <paramref name="now"/>. Revoked
    /// beats everything; then the absolute ceiling; then the idle window.
    /// </summary>
    public bool IsActiveAt(DateTimeOffset now) =>
        !IsRevoked
        && now < AbsoluteExpiresAt
        && now < LastSeenAt.Add(IdleTimeout);

    /// <summary>Slides the idle window forward. Never moves the absolute ceiling.</summary>
    public void Touch(DateTimeOffset now)
    {
        if (!IsActiveAt(now))
        {
            throw new InvalidOperationException("An inactive session cannot be renewed.");
        }

        LastSeenAt = now;
    }

    /// <summary>Ends the session immediately. Revoking twice is harmless.</summary>
    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
