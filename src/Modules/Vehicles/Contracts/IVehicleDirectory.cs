// IVehicleDirectory / IInventoryDirectory — the Vehicles module's public
// contract, and the only part of it other modules may reference (ADR-008).
//
// Use:  Sales reads a vehicle through IVehicleDirectory and a unit through
//       IInventoryDirectory. Neither touches the tables.
// Edit: the split is the point. A vehicle answers "what car is this?" and is
//       organization-shared; a unit answers "whose lot is it on, and what state
//       is it in?" and is rooftop-scoped. Merging the two would lose the scope.

using OpenDealer360.Core;

namespace OpenDealer360.Vehicles.Contracts;

/// <summary>
/// Vehicles as identities — VIN, year, make, model. Organization-shared: any
/// rooftop that can read vehicles can read all of them, because the same car
/// moves between locations and comes back as a trade-in (doc 04 §1).
/// </summary>
public interface IVehicleDirectory
{
    /// <summary>
    /// Finds vehicles by VIN — whole or partial — or by make and model. An empty
    /// term returns a first page, so a screen has something before typing.
    /// </summary>
    Task<Result<IReadOnlyList<VehicleSummary>>> SearchAsync(
        string? term,
        int limit,
        CancellationToken cancellationToken);

    Task<Result<VehicleDetail>> GetAsync(Guid vehicleId, CancellationToken cancellationToken);

    Task<Result<VehicleDetail>> AddAsync(NewVehicle vehicle, CancellationToken cancellationToken);
}

/// <summary>
/// Inventory units — a vehicle standing on one rooftop's lot. Every read and
/// write here is filtered to the rooftops the caller is authorized for; an
/// empty scope is a denial, never "unfiltered".
/// </summary>
public interface IInventoryDirectory
{
    /// <summary>
    /// What is in stock. Narrow it by rooftop, by status, or by stock number —
    /// all within the caller's authorized rooftops.
    /// </summary>
    Task<Result<IReadOnlyList<InventoryUnitSummary>>> ListAsync(
        InventoryQuery query,
        CancellationToken cancellationToken);

    Task<Result<InventoryUnitDetail>> GetAsync(Guid unitId, CancellationToken cancellationToken);

    Task<Result<InventoryUnitDetail>> ReceiveAsync(
        NewInventoryUnit unit,
        CancellationToken cancellationToken);

    Task<Result<InventoryUnitDetail>> ChangeStatusAsync(
        Guid unitId,
        StatusChangeRequest change,
        CancellationToken cancellationToken);
}

/// <summary>Enough to identify a vehicle in a list.</summary>
public sealed record VehicleSummary(
    Guid Id,
    string Vin,
    string DisplayName,
    int ModelYear,
    string Make,
    string Model,
    string? Trim,
    bool HasVinException);

/// <summary>One vehicle in full.</summary>
public sealed record VehicleDetail(
    Guid Id,
    string Vin,
    string DisplayName,
    int ModelYear,
    string Make,
    string Model,
    string? Trim,
    string? BodyStyle,
    string? ExteriorColor,
    string? VinExceptionReason);

/// <summary>What a caller supplies to record a vehicle.</summary>
public sealed record NewVehicle(
    string? Vin,
    int ModelYear,
    string Make,
    string Model,
    string? Trim = null,
    string? BodyStyle = null,
    string? ExteriorColor = null,
    string? VinExceptionReason = null);

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
