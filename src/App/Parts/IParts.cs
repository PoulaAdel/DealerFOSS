// IParts — what the rest of the application may call to reach parts stock.
//
// Use:  inject IParts. Nothing outside this folder touches Part, StockReceipt, or
//       the parts schema (ADR-014).
// Edit: IssueAsync is the one that matters. RepairOrders calls it while invoicing,
//       inside that same transaction, so the bill, the stock, and the ledger
//       cannot end up disagreeing about whether the part left the shelf.
//
//       The cost it returns is the cost AT THAT MOMENT, and the caller freezes it.
//       Do not add a "recost" operation: a supplier price rise must never rewrite
//       what last month's work cost.

using DealerFOSS.Core;

namespace DealerFOSS.Parts;

public interface IParts
{
    /// <summary>The catalogue with what is on hand at the caller's rooftops.</summary>
    Task<Result<IReadOnlyList<PartSummary>>> ListAsync(PartQuery query, CancellationToken cancellationToken);

    Task<Result<PartDetail>> GetAsync(Guid partId, CancellationToken cancellationToken);

    Task<Result<PartDetail>> AddAsync(NewPart part, CancellationToken cancellationToken);

    /// <summary>Books a delivery onto one rooftop's shelf at a known unit cost.</summary>
    Task<Result<PartDetail>> ReceiveAsync(Guid partId, StockDelivery delivery, CancellationToken cancellationToken);

    /// <summary>
    /// Takes stock off the shelf and reports what it cost, using whichever method
    /// the organization has chosen. Refuses rather than going negative: a
    /// workshop that can sell parts it does not have has no stock figure at all.
    ///
    /// Called inside the caller's transaction and does NOT save — the caller's
    /// SaveChanges commits the stock movement together with whatever it was doing.
    /// </summary>
    Task<Result<IssuedParts>> IssueAsync(
        IReadOnlyList<PartIssue> issues,
        RooftopId rooftopId,
        CancellationToken cancellationToken);

    /// <summary>Which costing method this organization uses, and what else it could use.</summary>
    Task<Result<PartsCostingSetting>> GetCostingMethodAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Changes it. Affects future sales only — see PartsCostingMethod.cs for why
    /// that is not negotiable.
    /// </summary>
    Task<Result<PartsCostingSetting>> SetCostingMethodAsync(
        PartsCostingMethod method,
        CancellationToken cancellationToken);
}

public sealed record PartQuery(
    RooftopId? RooftopId = null,
    string? Search = null,
    bool InStockOnly = false,
    int Limit = 100);

/// <summary>A catalogue row with the stock position at one rooftop.</summary>
public sealed record PartSummary(
    Guid Id,
    string PartNumber,
    string Description,

    /// <summary>
    /// Which shelf this row is about. **Null when the part has never been stocked
    /// anywhere the caller can see** — the part still appears, because a part
    /// that is invisible until it has stock cannot be opened, and a part that
    /// cannot be opened cannot have stock booked onto it. That dead end was real
    /// and was found by adding a part on the screen.
    /// </summary>
    RooftopId? RooftopId,
    decimal QuantityOnHand,

    /// <summary>
    /// What one would cost to sell right now, under the current method. A
    /// forecast, not a commitment — the figure that gets recorded is worked out
    /// at the moment of sale.
    /// </summary>
    decimal UnitCost,
    string Currency);

public sealed record PartDetail(
    Guid Id,
    string PartNumber,
    string Description,
    string CostingMethod,
    IReadOnlyList<PartStockAtRooftop> Stock);

public sealed record PartStockAtRooftop(
    RooftopId RooftopId,
    decimal QuantityOnHand,
    decimal UnitCost,
    string Currency,
    IReadOnlyList<StockLayerView> Layers);

/// <summary>
/// One delivery still on the shelf. Shown because "why does this part cost that?"
/// is a question a parts manager asks, and the layers are the honest answer.
/// </summary>
public sealed record StockLayerView(
    Guid Id,
    decimal QuantityReceived,
    decimal RemainingQuantity,
    decimal UnitCost,
    DateTimeOffset ReceivedAt,
    string? Reference);

public sealed record NewPart(string PartNumber, string Description);

public sealed record StockDelivery(
    decimal Quantity,
    decimal UnitCost,
    RooftopId RooftopId,
    string Currency = "USD",
    string? Reference = null);

/// <summary>One part and how many of it a job is taking.</summary>
public sealed record PartIssue(Guid PartId, decimal Quantity);

/// <summary>What came off the shelf, and what it cost in total.</summary>
public sealed record IssuedParts(decimal TotalCost, string Currency, IReadOnlyList<IssuedPart> Parts);

public sealed record IssuedPart(Guid PartId, string PartNumber, decimal Quantity, decimal Cost);

/// <summary>
/// The current method and every method available, so a screen can offer the
/// choice without keeping its own copy of the list.
/// </summary>
public sealed record PartsCostingSetting(string Method, IReadOnlyList<PartsCostingOption> Options);

public sealed record PartsCostingOption(string Method, string Name, string Explanation);
