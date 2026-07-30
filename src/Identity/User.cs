// User — a person who can sign in to this dealer organization.
//
// Use:  created by administration workflows (and the development seeder today).
// Edit: credentials and sessions arrive with the authentication milestone and
//       belong here. Email is a sign-in handle, not an identity: customer
//       matching must never key on it (doc 04 §4).

using OpenDealer360.Core;

namespace OpenDealer360.Identity;

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

    public ICollection<UserAssignment> Assignments { get; } = new List<UserAssignment>();

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
}
