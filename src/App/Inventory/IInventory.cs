// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   IInventory — what other features may call to reach stock on a lot.
//
// Usage:
//   Sales will read and reserve a unit through this.
//
// Coding Instructions:
//   Every read and write behind this interface is filtered to the caller's
//   authorized rooftops. An empty scope is a denial, never "unfiltered" —
//   keep that contract, or a caller will read it as "no filter".

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
    Task<Result<Page<InventoryUnitSummary>>> ListAsync(
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

    /// <summary>
    /// A stock unit arriving from another DealerFOSS installation, in the state
    /// it was already in, keeping its id.
    /// </summary>
    /// <remarks>
    /// <b>This posts nothing to the ledger, and that is deliberate.</b>
    /// <see cref="ReceiveAsync"/> posts because a car being taken in is an event
    /// that happened here and changes what the business owns. A car arriving in
    /// a records package is not that event — it happened at the other
    /// installation, months ago, and its money is already inside the opening
    /// balances the receiving dealership entered when they were set up. Posting
    /// it again would count the same car twice. See ADR-027.
    /// </remarks>
    Task<Result<ImportOutcome>> ImportAsync(
        ImportedInventoryUnit unit,
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

    /// <summary>
    /// The unit for this vehicle that this rooftop currently owns, or null.
    ///
    /// <para>
    /// Exists so the workshop can tell "our own car" from "a customer's car"
    /// without knowing anything about stock. Reconditioning belongs in the car's
    /// cost; the identical job on a customer's vehicle does not, and the only
    /// difference between them is whether the answer here is null.
    /// </para>
    /// <para>
    /// Sold and removed units are not owned, so they do not count. A car that has
    /// left is not somewhere to put more cost.
    /// </para>
    /// </summary>
    Task<Result<Guid?>> FindOwnedAsync(
        Guid vehicleId,
        RooftopId rooftopId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Puts work the dealership paid for onto the car that absorbed it, so the
    /// car's book value is what it actually cost to get saleable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The other half of <see cref="FindOwnedAsync"/>. That method answers "is
    /// this our car"; until 2026-09-19 its answer was used as a yes/no and the
    /// unit id thrown away, so recon was debited to 1300 and never attributed —
    /// which meant delivery relieved acquisition cost only, used-vehicle gross
    /// was overstated by the recon spend, and 1300 never came back down. See
    /// <see cref="ReconditioningCharge"/> for the whole story.
    /// </para>
    /// <para>
    /// Called inside the invoicing transaction, so a car cannot end up carrying
    /// a charge for a posting that rolled back. <paramref name="amount"/> may be
    /// negative to correct an earlier charge; it may not be zero, and it must be
    /// the unit's own currency.
    /// </para>
    /// </remarks>
    Task<Result> CapitaliseReconditioningAsync(
        Guid unitId,
        Money amount,
        Guid sourceRepairOrderId,
        CancellationToken cancellationToken);
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
    string VehicleDisplayName,

    /// <summary>
    /// What the dealership paid to acquire the car, and nothing else. Null means
    /// unrecorded; zero means a recorded zero cost. This is NOT what the car is
    /// carried at — see <see cref="BookValueAmount"/>.
    /// </summary>
    decimal? CostAmount,
    string? CostCurrency,

    /// <summary>
    /// Work capitalised onto this car since it came into stock, summed from
    /// <see cref="ReconditioningCharge"/>. Zero, never null: a car with no recon
    /// has absorbed nothing, which is a known amount.
    /// </summary>
    decimal ReconditioningAmount,

    /// <summary>
    /// Acquisition plus reconditioning — what the car is actually carried at and
    /// what delivery relieves from 1300.
    ///
    /// <para>
    /// Null exactly when <see cref="CostAmount"/> is null, because a book value
    /// built on an unknown acquisition cost would be a smaller number presented
    /// with the confidence of a complete one. A screen showing this must say
    /// which of the two it is showing; calling acquisition cost a book value is
    /// the mistake this field exists to end.
    /// </para>
    /// </summary>
    decimal? BookValueAmount);

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
    IReadOnlyList<InventoryStatusEntry> History,

    /// <summary>See <see cref="InventoryUnitSummary.ReconditioningAmount"/>.</summary>
    decimal ReconditioningAmount,

    /// <summary>See <see cref="InventoryUnitSummary.BookValueAmount"/>.</summary>
    decimal? BookValueAmount,

    /// <summary>
    /// Every charge that makes up the reconditioning above, newest last, so
    /// "where did this come from" has an answer on the screen rather than only
    /// in the database.
    /// </summary>
    IReadOnlyList<ReconditioningEntry> Reconditioning);

/// <summary>One capitalised posting, as a screen shows it.</summary>
public sealed record ReconditioningEntry(
    decimal Amount,
    string Currency,
    Guid SourceRepairOrderId,
    DateTimeOffset OccurredAt);

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

    /// <summary>
    /// Only cars somebody could still end up with — everything except Sold and
    /// Removed.
    /// </summary>
    /// <remarks>
    /// Not the same as <c>Status = "Available"</c>, and the difference is the
    /// point. A car being reconditioned or on hold for another customer is worth
    /// offering to somebody enquiring; a car that has gone is not. One status
    /// cannot say that, which is why this is its own flag rather than a fourth
    /// value of <see cref="Status"/>.
    /// </remarks>
    bool StillGettable = false,
    int Limit = 50,
    int Offset = 0);

/// <summary>
/// A stock unit as another installation holds it. The rooftop is this
/// installation's, supplied by the caller — a rooftop id from somewhere else
/// names nothing here.
/// </summary>
public sealed record ImportedInventoryUnit(
    Guid Id,
    Guid VehicleId,
    RooftopId RooftopId,
    string StockNumber,
    string Status,
    decimal? CostAmount,
    string? CostCurrency,
    DateOnly? AcquiredOn);

/// <summary>What a caller supplies to take a vehicle into stock.</summary>
public sealed record NewInventoryUnit(
    Guid VehicleId,
    RooftopId RooftopId,
    string StockNumber,
    decimal? CostAmount = null,
    string? CostCurrency = null,
    DateOnly? AcquiredOn = null,
    string? Note = null,

    /// <summary>
    /// True when a lender paid for this car and will be repaid when it sells;
    /// false when the dealership bought it outright. Decides what the purchase
    /// entry credits - 2000 Floorplan payable or 1000 Cash - and it is a fact
    /// about THIS car rather than a setting, because most lots carry both.
    /// </summary>
    bool Floorplanned = false);

/// <summary>What a caller supplies to move a unit to another status.</summary>
public sealed record StatusChangeRequest(string Status, string? Note = null);
