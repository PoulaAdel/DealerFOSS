// IVehicles — what other features may call to reach vehicle records.
//
// Use:  Sales and Service read a vehicle through this. They never query the
//       tables themselves, and they never reference VehicleService.
// Edit: a vehicle is organization-shared — it answers "what car is this?", not
//       "whose lot is it on". The rooftop-scoped half lives in IInventory. Do not
//       merge the two: the scope is the reason they are separate.

using OpenDealer360.Core;

namespace OpenDealer360.Vehicles;

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
    Task<Result<IReadOnlyList<VehicleSummary>>> SearchAsync(
        string? term,
        int limit,
        CancellationToken cancellationToken);

    Task<Result<VehicleDetail>> GetAsync(Guid vehicleId, CancellationToken cancellationToken);

    Task<Result<VehicleDetail>> AddAsync(NewVehicle vehicle, CancellationToken cancellationToken);
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
