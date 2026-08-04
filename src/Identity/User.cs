// User — a person who can sign in to this dealer organization.
//
// Use:  created by administration workflows (and the development seeder today).
// Edit: credentials and sessions arrive with the authentication milestone and
//       belong here. Email is a sign-in handle, not an identity: customer
//       matching must never key on it (doc 04 §4).

using DealerFOSS.Core;

namespace DealerFOSS.Identity;

/// <summary>
/// A person who can sign in to this dealer organization. Credentials and
/// sessions are added with the authentication milestone; this type exists now so
/// authorization has a subject to reason about.
/// </summary>
internal sealed class User : AuditableEntity
{
    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Hashed credential. Null means this account cannot sign in with a password
    /// yet — it is not the same as "no password required".
    /// </summary>
    public string? PasswordHash { get; private set; }

    public bool CanSignIn => IsActive && PasswordHash is not null;

    /// <summary>
    /// The TOTP shared secret, encrypted with <c>ISecretProtector</c>. A stolen
    /// database must not hand over everybody's second factor, so this is never
    /// stored as the app reads it.
    /// </summary>
    public string? MfaSecretProtected { get; private set; }

    /// <summary>
    /// When the user proved they could produce a code. Null while enrolment is
    /// half-finished — a secret that was generated but never confirmed must not
    /// lock anybody out.
    /// </summary>
    public DateTimeOffset? MfaConfirmedAt { get; private set; }

    public bool MfaEnabled => MfaConfirmedAt is not null && MfaSecretProtected is not null;

    public ICollection<UserAssignment> Assignments { get; } = new List<UserAssignment>();

    public ICollection<RecoveryCode> RecoveryCodes { get; } = new List<RecoveryCode>();

    private User()
    {
    }

    public User(Guid id, string email, string displayName)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        Id = id;
        // Email is a contact point and a sign-in handle, not a customer identity
        // (doc 04 §4). Stored normalized so lookups are case-insensitive.
        Email = email.Trim().ToLowerInvariant();
        DisplayName = displayName;
    }

    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Stores an already-hashed credential. Hashing belongs to the service that
    /// owns the algorithm; the domain never sees a plaintext password.
    /// </summary>
    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("A password hash is required.", nameof(passwordHash));
        }

        PasswordHash = passwordHash;
    }

    /// <summary>
    /// Stores a generated secret without switching the second factor on. Nothing
    /// changes about signing in until <see cref="ConfirmMfa"/>, because a user
    /// who mis-scans the QR code would otherwise be locked out of their account.
    /// </summary>
    public void BeginMfaEnrolment(string protectedSecret)
    {
        if (string.IsNullOrWhiteSpace(protectedSecret))
        {
            throw new ArgumentException("An enrolment needs a secret.", nameof(protectedSecret));
        }

        MfaSecretProtected = protectedSecret;
        MfaConfirmedAt = null;
        RecoveryCodes.Clear();
    }

    /// <summary>Switches it on, once the user has produced a working code.</summary>
    public void ConfirmMfa(DateTimeOffset at)
    {
        if (MfaSecretProtected is null)
        {
            throw new InvalidOperationException("There is no enrolment to confirm.");
        }

        MfaConfirmedAt = at;
    }

    public void DisableMfa()
    {
        MfaSecretProtected = null;
        MfaConfirmedAt = null;
        RecoveryCodes.Clear();
    }
}
