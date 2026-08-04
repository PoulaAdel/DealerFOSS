// VehicleService — finding, reading, and recording vehicles.
//
// Use:  through IVehicles.
// Edit: a vehicle is organization-shared, so there is no rooftop filter here —
//       only a permission check. That is the documented model (doc 04 §1): the
//       same car is bought at one location, serviced at another, and traded back
//       in at a third, and hiding it by location would make staff record it three
//       times. Rooftop scope lives on InventoryUnit, in InventoryService.

using Microsoft.EntityFrameworkCore;
using DealerFOSS.Core;
using DealerFOSS.Data;
using DealerFOSS.Identity;
using DealerFOSS.Vehicles;

namespace DealerFOSS.Vehicles;

public sealed class VehicleService(
    TenantDb db,
    IAccessDirectory access,
    ICurrentUser currentUser,
    IAuditSink audit)
    : IVehicles
{
    private const string ReadPermission = "Vehicles.Read";

    /// <summary>
    /// Recording a vehicle is a stock action, so it is gated by the permission
    /// that lets someone manage stock — not by a separate right nobody holds.
    /// </summary>
    private const string CreatePermission = "Inventory.Manage";

    /// <summary>Caps how many rows a single search can return, however it is called.</summary>
    private const int MaxResults = 100;

    private readonly TenantDb _db = db;
    private readonly IAccessDirectory _access = access;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditSink _audit = audit;

    public async Task<Result<IReadOnlyList<VehicleSummary>>> SearchAsync(
        string? term,
        int limit,
        CancellationToken cancellationToken)
    {
        if (!await IsAllowedAnywhereAsync(ReadPermission, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<VehicleSummary>>(VehicleErrors.Forbidden);
        }

        var take = Math.Clamp(limit <= 0 ? 25 : limit, 1, MaxResults);
        var query = _db.Vehicles.AsNoTracking();

        var search = (term ?? string.Empty).Trim();
        if (search.Length > 0)
        {
            // Staff read the last six of a VIN off a windscreen far more often
            // than they type all seventeen, so a partial match has to work.
            var vin = Vin.Normalize(search);

            query = query.Where(v =>
                EF.Functions.Like(v.Vin, $"%{vin}%")
                || EF.Functions.Like(v.Make, $"%{search}%")
                || EF.Functions.Like(v.Model, $"%{search}%"));
        }

        var vehicles = await query
            .OrderByDescending(v => v.ModelYear)
            .ThenBy(v => v.Make)
            .ThenBy(v => v.Model)
            .Take(take)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<VehicleSummary>>(
            vehicles.Select(Summarize).ToList());
    }

    public async Task<Result<VehicleDetail>> GetAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        if (!await IsAllowedAnywhereAsync(ReadPermission, cancellationToken))
        {
            return Result.Failure<VehicleDetail>(VehicleErrors.Forbidden);
        }

        var vehicle = await _db.Vehicles
            .AsNoTracking()
            .SingleOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        return vehicle is null
            ? Result.Failure<VehicleDetail>(VehicleErrors.NotFound)
            : Result.Success(Describe(vehicle));
    }

    public async Task<Result<VehicleDetail>> AddAsync(NewVehicle vehicle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vehicle);

        if (!await IsAllowedAnywhereAsync(CreatePermission, cancellationToken))
        {
            return Result.Failure<VehicleDetail>(VehicleErrors.Forbidden);
        }

        Vehicle recorded;
        try
        {
            recorded = Vehicle.Record(
                Guid.NewGuid(),
                vehicle.Vin,
                vehicle.ModelYear,
                vehicle.Make,
                vehicle.Model,
                vehicle.Trim,
                vehicle.VinExceptionReason,
                vehicle.BodyStyle,
                vehicle.ExteriorColor);
        }
        catch (ArgumentException ex)
        {
            // Domain invariants speak in plain sentences; surface that rather
            // than a generic "invalid request".
            return Result.Failure<VehicleDetail>(Error.Validation("vehicles.invalid", ex.Message));
        }

        _db.Vehicles.Add(recorded);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, CreatePermission, AuditOutcome.Allowed,
                "Vehicle", recorded.Id.ToString(), null, null, null, null),
            cancellationToken);

        return Result.Success(Describe(recorded));
    }

    public async Task<Result<VehicleDetail?>> FindByVinAsync(
        string vin,
        CancellationToken cancellationToken)
    {
        if (!await IsAllowedAnywhereAsync(ReadPermission, cancellationToken))
        {
            return Result.Failure<VehicleDetail?>(VehicleErrors.Forbidden);
        }

        // Normalized the same way it was stored, or a VIN typed with an O
        // instead of a zero would look like a different car.
        var normalized = Vin.Normalize(vin ?? string.Empty);
        if (normalized.Length == 0)
        {
            return Result.Success<VehicleDetail?>(null);
        }

        var vehicle = await _db.Vehicles
            .AsNoTracking()
            .SingleOrDefaultAsync(v => v.Vin == normalized, cancellationToken);

        return Result.Success(vehicle is null ? null : Describe(vehicle));
    }

    public async Task<Result<IReadOnlyList<VehicleDetail>>> PageForExportAsync(
        Guid? after,
        int take,
        CancellationToken cancellationToken)
    {
        if (!await IsAllowedAnywhereAsync(ReadPermission, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<VehicleDetail>>(VehicleErrors.Forbidden);
        }

        var query = _db.Vehicles.AsNoTracking();
        if (after is { } cursor)
        {
            query = query.Where(v => v.Id.CompareTo(cursor) > 0);
        }

        var vehicles = await query
            .OrderBy(v => v.Id)
            .Take(Math.Clamp(take <= 0 ? 500 : take, 1, 1000))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<VehicleDetail>>(
            vehicles.Select(Describe).ToList());
    }

    /// <summary>
    /// A vehicle is organization-wide, so holding the permission anywhere is
    /// enough. Denials are audited by the access directory.
    /// </summary>
    private async Task<bool> IsAllowedAnywhereAsync(string permission, CancellationToken cancellationToken)
    {
        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, permission, cancellationToken);
        return !scope.GrantsNothing;
    }

    private static VehicleSummary Summarize(Vehicle v) =>
        new(v.Id, v.Vin, v.DisplayName, v.ModelYear, v.Make, v.Model, v.Trim, v.HasVinException);

    private static VehicleDetail Describe(Vehicle v) =>
        new(v.Id, v.Vin, v.DisplayName, v.ModelYear, v.Make, v.Model,
            v.Trim, v.BodyStyle, v.ExteriorColor, v.VinExceptionReason);
}

/// <summary>Stable error codes for the Vehicles capability (doc 06 §6).</summary>
internal static class VehicleErrors
{
    public static Error Forbidden { get; } = Error.Forbidden(
        "vehicles.forbidden",
        "You do not have access to vehicle records.");

    public static Error NotFound { get; } = Error.NotFound(
        "vehicles.not_found",
        "No such vehicle.");
}
