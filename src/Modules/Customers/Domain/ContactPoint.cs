// ContactPoint — one way to reach a customer: an email address or a phone number.
//
// Use:  added through Customer.AddContactPoint, never constructed directly, so
//       normalization and the primary-flag rules always apply.
// Edit: an email address is a way to reach someone, NOT their identity
//       (doc 04 §4). Two customers may legitimately share one — a couple, a
//       family business — so never add a unique constraint on the value and
//       never match customers on it alone.

namespace OpenDealer360.Customers.Domain;

public sealed class ContactPoint
{
    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public ContactKind Kind { get; private set; }

    /// <summary>Stored normalized, so searching does not depend on how it was typed.</summary>
    public string Value { get; private set; } = string.Empty;

    /// <summary>The one to use first for this kind. Exactly one per kind.</summary>
    public bool IsPrimary { get; private set; }

    private ContactPoint()
    {
    }

    internal ContactPoint(Guid id, Guid customerId, ContactKind kind, string normalizedValue)
    {
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            throw new ArgumentException("A contact point needs a value.", nameof(normalizedValue));
        }

        Id = id;
        CustomerId = customerId;
        Kind = kind;
        Value = normalizedValue;
    }

    internal void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;

    /// <summary>
    /// Makes a typed value comparable. Email is lower-cased; a phone number keeps
    /// only its digits and a leading "+", so "(555) 010-2030" and "555-010-2030"
    /// are recognised as the same number.
    /// </summary>
    public static string Normalize(ContactKind kind, string value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("A contact point needs a value.", nameof(value));
        }

        if (kind == ContactKind.Email)
        {
            return trimmed.ToLowerInvariant();
        }

        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            throw new ArgumentException("A phone number needs at least one digit.", nameof(value));
        }

        return trimmed.StartsWith('+') ? $"+{digits}" : digits;
    }
}

public enum ContactKind
{
    Email = 0,
    Phone = 1,
    Mobile = 2,
}
