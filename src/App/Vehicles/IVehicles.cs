// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   IVehicles — what other features may call to reach vehicle records.
//
// Usage:
//   Sales and Service read a vehicle through this. They never query the
//   tables themselves, and they never reference VehicleService.
//
// Coding Instructions:
//   A vehicle is organization-shared — it answers "what car is this?", not
//   "whose lot is it on". The rooftop-scoped half lives in IInventory. Do not
//   merge the two: the scope is the reason they are separate.

using DealerFOSS.Core;

namespace DealerFOSS.Vehicles;

/// <summary>
/// Vehicles as identities — VIN, year, make, model. Organization-shared: the same
/// car is bought at one location, serviced at another, and traded back in at a
/// third (doc 04 §1).
/// </summary>
public interface IVehicles
{
    /// <summary>
    /// Finds vehicles by VIN — whole or partial — or by make and model. An empty
    /// term returns a first page, so a screen has something before typing.
    /// </summary>
    Task<Result<Page<VehicleSummary>>> SearchAsync(
        string? term,
        int limit,
        int offset,
        CancellationToken cancellationToken);

    Task<Result<VehicleDetail>> GetAsync(Guid vehicleId, CancellationToken cancellationToken);

    /// <summary>
    /// A known set of vehicles, in one query. A work list showing which car each
    /// enquiry is about would otherwise fetch them one at a time — this exists so
    /// a caller never has to choose between a slow screen and a stale copy of the
    /// name. Unknown ids are simply absent from the result.
    /// </summary>
    Task<Result<IReadOnlyList<VehicleSummary>>> GetManyAsync(
        IReadOnlyCollection<Guid> vehicleIds,
        CancellationToken cancellationToken);

    Task<Result<VehicleDetail>> AddAsync(NewVehicle vehicle, CancellationToken cancellationToken);

    /// <summary>
    /// The vehicle with exactly this VIN, or null.
    /// </summary>
    /// <remarks>
    /// A lookup, not a search: the VIN is the car's identity, so it matches one
    /// vehicle or none. This is what lets an import run twice without creating a
    /// second copy of every car — no external reference is needed, because the
    /// industry already agreed on a key seventeen characters long.
    /// </remarks>
    Task<Result<VehicleDetail?>> FindByVinAsync(string vin, CancellationToken cancellationToken);

    /// <summary>
    /// One page of vehicles in id order, for walking the whole set. Pass the
    /// last id seen to get the next page; null starts at the beginning.
    /// </summary>
    /// <remarks>
    /// Keyset rather than offset paging (doc 06 §6): an offset shifts under a
    /// concurrent insert, so a long export would silently skip or repeat a car.
    /// </remarks>
    Task<Result<IReadOnlyList<VehicleDetail>>> PageForExportAsync(
        Guid? after,
        int take,
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
