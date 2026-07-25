using OpenDealer360.Core;

namespace OpenDealer360.Identity.Domain;

/// <summary>
/// A person who can sign in to this dealer organization. Credentials and
/// sessions are added with the authentication milestone; this type exists now so
/// authorization has a subject to reason about.
/// </summary>
public sealed class User : AuditableEntity
{
    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;

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
}
