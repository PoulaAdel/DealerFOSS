// IInventory — what other features may call to reach stock on a lot.
//
// Use:  Sales will read and reserve a unit through this.
// Edit: every read and write behind this interface is filtered to the caller's
//       authorized rooftops. An empty scope is a denial, never "unfiltered" —
//       keep that contract, or a caller will read it as "no filter".

using DealerFOSS.Core;

namespace DealerFOSS.Inventory;

/// <summary>
/// Inventory units — a vehicle standing on one rooftop's lot. Rooftop-owned and
/// scoped, unlike the vehicle record itself (doc 04 §1, §3).
/// </summary>
public interface IInventory
{
    /// <summary>
    /// What is in stock. Narrow it by rooftop, by status, or by stock number —
    /// all within the caller's authorized rooftops.
    /// </summary>
    Task<Result<IReadOnlyList<InventoryUnitSummary>>> ListAsync(
        InventoryQuery query,
        CancellationToken cancellationToken);

    Task<Result<InventoryUnitDetail>> GetAsync(Guid unitId, CancellationToken cancellationToken);

    /// <summary>
    /// Summaries for a known set of units, in one query and still filtered to the
    /// caller's rooftops. A desk list showing stock numbers would otherwise fetch
    /// them one at a time. Units the caller may not see are simply absent.
    /// </summary>
    Task<Result<IReadOnlyList<InventoryUnitSummary>>> GetManyAsync(
        IReadOnlyCollection<Guid> unitIds,
        CancellationToken cancellationToken);

    Task<Result<InventoryUnitDetail>> ReceiveAsync(
        NewInventoryUnit unit,
        CancellationToken cancellationToken);

    Task<Result<InventoryUnitDetail>> ChangeStatusAsync(
        Guid unitId,
        StatusChangeRequest change,
        CancellationToken cancellationToken);

    /// <summary>
    /// How long the unsold stock has been standing. The one inventory figure that
    /// costs money every day nobody looks at it.
    /// </summary>
    /// <remarks>
    /// Answered here rather than by whatever screen wants it, because the age of a
    /// unit depends on knowing which statuses still count as stock and what to do
    /// when nobody recorded an acquisition date. Both are Inventory's business.
    /// </remarks>
    Task<Result<StockAging>> AgingAsync(StockAgingQuery query, CancellationToken cancellationToken);
}

/// <summary>Which stock to age, and as at when.</summary>
public sealed record StockAgingQuery(RooftopId? RooftopId = null, DateOnly? AsOf = null);

/// <summary>
/// The unsold stock, grouped by how long it has been here. Sold and removed units
/// are excluded: aging is a question about money still tied up, and a car that
/// left is not tying anything up.
/// </summary>
public sealed record StockAging(
    DateOnly AsOf,
    int Units,
    IReadOnlyList<StockAgeBand> Bands,

    /// <summary>
    /// The oldest few, named. A band count says there is a problem; this says
    /// which cars it is, which is the difference between a chart and an action.
    /// </summary>
    IReadOnlyList<AgingUnit> Oldest);

/// <summary>
/// One band. <paramref name="ToDay"/> is null on the last one, which is open-ended
/// — and is the band a manager is actually looking for.
/// </summary>
public sealed record StockAgeBand(string Name, int FromDay, int? ToDay, int Units);

public sealed record AgingUnit(
    Guid Id,
    string StockNumber,
    string VehicleDisplayName,
    string Status,
    int DaysInStock,

    /// <summary>
    /// True when the age is counted from the day the unit was entered rather than
    /// from an acquisition date, because nobody recorded one. Said out loud so a
    /// manager can tell a genuinely old car from a badly entered one.
    /// </summary>
    bool AgeIsEstimated);

/// <summary>One unit as an inventory list shows it.</summary>
public sealed record InventoryUnitSummary(
    Guid Id,
    string StockNumber,
    RooftopId RooftopId,
    string Status,
    Guid VehicleId,
    string Vin,
    string VehicleDisplayName);

/// <summary>One unit in full, with the moves it has made.</summary>
public sealed record InventoryUnitDetail(
    Guid Id,
    string StockNumber,
    RooftopId RooftopId,
    string Status,
    Guid VehicleId,
    string Vin,
    string VehicleDisplayName,
    decimal? CostAmount,
    string? CostCurrency,
    DateOnly? AcquiredOn,
    IReadOnlyList<InventoryStatusEntry> History);

public sealed record InventoryStatusEntry(
    string? FromStatus,
    string ToStatus,
    DateTimeOffset OccurredAt,
    string? Note);

/// <summary>How a caller narrows an inventory list.</summary>
public sealed record InventoryQuery(
    RooftopId? RooftopId = null,
    string? Status = null,
    string? StockNumber = null,
    string? Search = null,
    int Limit = 50);

/// <summary>What a caller supplies to take a vehicle into stock.</summary>
public sealed record NewInventoryUnit(
    Guid VehicleId,
    RooftopId RooftopId,
    string StockNumber,
    decimal? CostAmount = null,
    string? CostCurrency = null,
    DateOnly? AcquiredOn = null,
    string? Note = null);

/// <summary>What a caller supplies to move a unit to another status.</summary>
public sealed record StatusChangeRequest(string Status, string? Note = null);
