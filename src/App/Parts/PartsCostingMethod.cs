// PartsCostingMethod — how the cost of a part sold is worked out, and the
// per-organization setting that chooses it.
//
// Use:  PartsCosting.CostOf(method, layers, quantity) is the whole calculation.
//       The setting is read and changed through IParts.
// Edit: all three methods read the SAME data — the receipt layers — so switching
//       between them is safe at any moment and needs no migration. That is the
//       reason StockReceipt exists even for organizations that never use FIFO.
//
//       Changing the method affects FUTURE sales only. A sold line freezes the
//       cost it was sold at and the ledger is immutable, so switching cannot
//       rewrite a month somebody has already reported on. Do not "improve" this
//       into a recalculation.

using DealerFOSS.Core;

namespace DealerFOSS.Parts;

/// <summary>
/// How the cost of a part is decided when it is sold. A setting rather than a
/// constant, because dealer groups genuinely differ and their accountants have
/// opinions.
/// </summary>
public enum PartsCostingMethod
{
    /// <summary>
    /// The average cost of what is currently on the shelf. The default, and what
    /// most dealer systems use: it smooths supplier price changes and does not
    /// depend on which physical item somebody picked up.
    /// </summary>
    MovingAverage = 0,

    /// <summary>
    /// Whatever the most recent delivery cost. Simple to explain, and a single
    /// odd purchase price distorts every sale after it.
    /// </summary>
    LastCost = 1,

    /// <summary>
    /// Oldest stock first. The most faithful to what physically leaves the shelf,
    /// and the reason receipts are kept as separate layers.
    /// </summary>
    Fifo = 2,
}

/// <summary>
/// The organization's choice of costing method. One row per tenant database,
/// because one tenant is one dealer organization.
/// </summary>
public sealed class PartsSettings : AuditableEntity
{
    /// <summary>
    /// Fixed, because there is exactly one of these per organization and a tenant
    /// database holds exactly one organization. A known id makes "read the
    /// setting" a primary-key lookup rather than a scan hoping for one row.
    /// </summary>
    public static readonly Guid SingletonId = new("9a17e5c0-0000-4000-8000-000000000001");

    public Guid Id { get; private set; }

    public PartsCostingMethod CostingMethod { get; private set; } = PartsCostingMethod.MovingAverage;

    private PartsSettings()
    {
    }

    public PartsSettings(PartsCostingMethod method)
    {
        Id = SingletonId;
        CostingMethod = method;
    }

    public void Use(PartsCostingMethod method) => CostingMethod = method;
}

/// <summary>
/// The costing calculation itself, kept out of the service so it can be tested
/// without a database and read without scrolling past permission checks.
/// </summary>
public static class PartsCosting
{
    /// <summary>
    /// What <paramref name="quantity"/> of a part costs, given the layers on hand
    /// newest-last. Returns null when there is not enough stock — the caller
    /// decides whether that is a refusal, and it always is today.
    /// </summary>
    public static decimal? CostOf(
        PartsCostingMethod method,
        IReadOnlyList<StockReceipt> layers,
        decimal quantity)
    {
        ArgumentNullException.ThrowIfNull(layers);

        if (quantity <= 0)
        {
            return 0m;
        }

        var available = layers.Sum(l => l.RemainingQuantity);
        if (available < quantity)
        {
            return null;
        }

        return method switch
        {
            PartsCostingMethod.LastCost => LastCostOf(layers) * quantity,
            PartsCostingMethod.Fifo => FifoCostOf(layers, quantity),
            _ => MovingAverageOf(layers) * quantity,
        };
    }

    /// <summary>
    /// Total value on the shelf divided by total quantity. Zero quantity has no
    /// average — there is nothing to average — so it reports zero rather than
    /// dividing.
    /// </summary>
    public static decimal MovingAverageOf(IReadOnlyList<StockReceipt> layers)
    {
        var quantity = layers.Sum(l => l.RemainingQuantity);
        if (quantity <= 0)
        {
            return 0m;
        }

        return Round(layers.Sum(l => l.RemainingQuantity * l.UnitCostAmount) / quantity);
    }

    /// <summary>
    /// The newest receipt's cost, whether or not any of it is left — "last cost
    /// paid" is a fact about the last purchase, not about the shelf.
    /// </summary>
    public static decimal LastCostOf(IReadOnlyList<StockReceipt> layers) =>
        layers.Count == 0
            ? 0m
            : layers.OrderByDescending(l => l.ReceivedAt).ThenByDescending(l => l.Id).First().UnitCostAmount;

    private static decimal FifoCostOf(IReadOnlyList<StockReceipt> layers, decimal quantity)
    {
        var outstanding = quantity;
        var total = 0m;

        foreach (var layer in Oldest(layers))
        {
            if (outstanding <= 0)
            {
                break;
            }

            var taken = Math.Min(outstanding, layer.RemainingQuantity);
            total += taken * layer.UnitCostAmount;
            outstanding -= taken;
        }

        return Round(total);
    }

    /// <summary>
    /// Oldest first, with the id as a tiebreaker so two receipts booked in the
    /// same second consume in a stable order rather than whatever the database
    /// felt like returning.
    /// </summary>
    public static IEnumerable<StockReceipt> Oldest(IReadOnlyList<StockReceipt> layers) =>
        layers.Where(l => l.RemainingQuantity > 0)
            .OrderBy(l => l.ReceivedAt)
            .ThenBy(l => l.Id);

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.ToEven);
}
