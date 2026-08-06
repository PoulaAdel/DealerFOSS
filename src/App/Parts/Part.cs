// Part — a part the dealership sells, and the stock of it held at each location.
//
// Use:  Part is the catalogue entry (organization-shared, like Vehicle);
//       StockReceipt is a delivery of it onto one rooftop's shelf.
// Edit: the split matters. A part NUMBER means the same thing across the group —
//       two lots ordering "MZ-690411" mean the same component — but the pile on
//       each shelf is that lot's own, like InventoryUnit and unlike Vehicle.
//
//       There is deliberately no QuantityOnHand column. Quantity is the sum of
//       what is left on the receipts, so it cannot drift from the layers that
//       produce it. A stored total and a layer list WILL disagree eventually,
//       and when they do nobody can tell which one is lying.

using DealerFOSS.Core;

namespace DealerFOSS.Parts;

/// <summary>
/// A part in the catalogue. Organization-shared: the number identifies the same
/// component wherever it is stocked, and the stock itself is rooftop-owned.
/// </summary>
public sealed class Part : AuditableEntity
{
    public Guid Id { get; private set; }

    /// <summary>The manufacturer's number, normalized so lookup is case-insensitive.</summary>
    public string PartNumber { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    private Part()
    {
    }

    public Part(Guid id, string partNumber, string description)
    {
        if (string.IsNullOrWhiteSpace(partNumber))
        {
            throw new ArgumentException("A part number is required.", nameof(partNumber));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("A description is required.", nameof(description));
        }

        Id = id;
        PartNumber = Normalize(partNumber);
        Description = description.Trim();
    }

    public void Describe(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("A description is required.", nameof(description));
        }

        Description = description.Trim();
    }

    /// <summary>
    /// Upper-cased and stripped of spaces and hyphens. Suppliers and staff write
    /// the same number a dozen ways, and two catalogue rows for one component is
    /// the failure that makes a parts department stop trusting the system.
    /// </summary>
    public static string Normalize(string partNumber) =>
        partNumber
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();
}
