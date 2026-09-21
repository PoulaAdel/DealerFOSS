// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   VehicleService — finding, reading, and recording vehicles.
//
// Usage:
//   Through IVehicles.
//
// Coding Instructions:
//   A vehicle is organization-shared, so there is no rooftop filter here —
//   only a permission check. That is the documented model (doc 04 §1): the
//   same car is bought at one location, serviced at another, and traded back
//   in at a third, and hiding it by location would make staff record it three
//   times. Rooftop scope lives on InventoryUnit, in InventoryService.

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

    public async Task<Result<Page<VehicleSummary>>> SearchAsync(
        string? term,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        if (!await IsAllowedAnywhereAsync(ReadPermission, cancellationToken))
        {
            return Result.Failure<Page<VehicleSummary>>(VehicleErrors.Forbidden);
        }

        var take = Paging.Limit(limit, fallback: 25);
        var skip = Paging.Offset(offset);
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

        var total = await query.CountAsync(cancellationToken);

        // Id breaks ties: a dealership stocking six identical model-year Civics
        // has six rows the sort cannot separate, and an order that is not total
        // lets one of them appear on two pages and another on none.
        var vehicles = await query
            .OrderByDescending(v => v.ModelYear)
            .ThenBy(v => v.Make)
            .ThenBy(v => v.Model)
            .ThenBy(v => v.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return Result.Success(new Page<VehicleSummary>(
            vehicles.Select(Summarize).ToList(),
            total,
            skip,
            take));
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

    public async Task<Result<IReadOnlyList<VehicleSummary>>> GetManyAsync(
        IReadOnlyCollection<Guid> vehicleIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vehicleIds);

        if (!await IsAllowedAnywhereAsync(ReadPermission, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<VehicleSummary>>(VehicleErrors.Forbidden);
        }

        if (vehicleIds.Count == 0)
        {
            return Result.Success<IReadOnlyList<VehicleSummary>>([]);
        }

        var wanted = vehicleIds.Distinct().ToList();

        var vehicles = await _db.Vehicles
            .AsNoTracking()
            .Where(v => wanted.Contains(v.Id))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<VehicleSummary>>(
            vehicles.Select(Summarize).ToList());
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

    public async Task<Result<ImportOutcome>> ImportAsync(
        ImportedVehicle vehicle,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vehicle);

        if (!await IsAllowedAnywhereAsync(CreatePermission, cancellationToken))
        {
            return Result.Failure<ImportOutcome>(VehicleErrors.Forbidden);
        }

        if (await _db.Vehicles.AsNoTracking().AnyAsync(v => v.Id == vehicle.Id, cancellationToken))
        {
            return Result.Success(ImportOutcome.AlreadyPresent);
        }

        // Checked before the insert rather than left to the unique index,
        // because a DbUpdateException here would abort the whole package and
        // this is a refusal about one car that the rest can survive.
        if (!string.IsNullOrWhiteSpace(vehicle.Vin)
            && await _db.Vehicles.AsNoTracking().AnyAsync(v => v.Vin == vehicle.Vin, cancellationToken))
        {
            return Result.Failure<ImportOutcome>(VehicleErrors.VinAlreadyHereUnderAnotherId);
        }

        Vehicle arriving;
        try
        {
            arriving = Vehicle.Record(
                vehicle.Id,
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
            return Result.Failure<ImportOutcome>(Error.Validation("vehicles.invalid", ex.Message));
        }

        _db.Vehicles.Add(arriving);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // One record must not take the rest of the package down with it, and
            // a failed insert left Added would be retried on the next save.
            _db.ForgetPendingWrites();
            return Result.Failure<ImportOutcome>(VehicleErrors.CouldNotBeWritten(ex));
        }

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, CreatePermission, AuditOutcome.Allowed,
                "Vehicle", arriving.Id.ToString(), null, "Imported from a package", null, null),
            cancellationToken);

        return Result.Success(ImportOutcome.Created);
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

    /// <summary>
    /// An arriving car's VIN is already recorded here against a different id.
    /// Refused rather than merged: the two records have different histories and
    /// picking one would silently give the incoming deals the wrong car.
    /// </summary>
    public static Error VinAlreadyHereUnderAnotherId { get; } = Error.Conflict(
        "vehicles.vin_already_here",
        "That VIN is already recorded here against a different record.");

    /// <summary>See the note on the customer equivalent.</summary>
    public static Error CouldNotBeWritten(Exception cause) => Error.Conflict(
        "vehicles.could_not_be_written",
        $"That vehicle could not be written: {cause?.InnerException?.Message ?? cause?.Message}");
}
