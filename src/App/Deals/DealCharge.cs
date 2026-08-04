// DealCharge — one line on a deal: the car, a fee, a discount, an accessory.
//
// Use:  added through Deal.SetTerms, never constructed directly, so the sign
//       rules and the Draft-only restriction always apply.
// Edit: the sign convention is load-bearing. A discount is stored negative so the
//       subtotal is a plain sum — the alternative, storing it positive and
//       remembering to subtract it, is how a total ends up wrong on one screen
//       and right on another.

namespace DealerFOSS.Deals;

public sealed class DealCharge
{
    public Guid Id { get; private set; }

    public Guid DealId { get; private set; }

    public ChargeKind Kind { get; private set; }

    public string Description { get; private set; } = string.Empty;

    /// <summary>Signed: negative for a discount, positive for everything else.</summary>
    public decimal Amount { get; private set; }

    private DealCharge()
    {
    }

    internal DealCharge(Guid id, Guid dealId, ChargeKind kind, string description, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("A charge needs a description.", nameof(description));
        }

        if (kind == ChargeKind.Discount && amount > 0)
        {
            throw new ArgumentException(
                "A discount must be negative — it comes off the total.", nameof(amount));
        }

        if (kind != ChargeKind.Discount && amount < 0)
        {
            throw new ArgumentException(
                $"A {kind} cannot be negative. Record money off as a Discount.", nameof(amount));
        }

        Id = id;
        DealId = dealId;
        Kind = kind;
        Description = description.Trim();
        Amount = amount;
    }
}
