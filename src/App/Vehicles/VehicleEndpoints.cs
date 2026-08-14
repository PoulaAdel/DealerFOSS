// VehicleEndpoints — the HTTP surface for vehicle records.
//
// Use:  mapped from Program.cs; routes under /api/v1/vehicles.
//       GET  /api/v1/vehicles?search=1HGCM&limit=25
//       GET  /api/v1/vehicles/{id}
//       GET  /api/v1/vehicles/{id}/recalls
//       POST /api/v1/vehicles
// Edit: keep it thin — delegate, then map a Result to a status code.
//       Authorization lives in VehicleService so background callers get it too.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using DealerFOSS.App;
using DealerFOSS.Core;

namespace DealerFOSS.Vehicles;

internal static class VehicleEndpoints
{
    public static void MapVehicles(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/vehicles").WithTags("Vehicles");

        group.MapGet("", SearchAsync);
        group.MapGet("/{vehicleId:guid}", GetAsync);
        group.MapGet("/{vehicleId:guid}/recalls", RecallsAsync);
        group.MapPost("", AddAsync);
    }

    private static async Task<IResult> SearchAsync(
        IVehicles vehicles,
        CancellationToken cancellationToken,
        string? search = null,
        int limit = 25)
    {
        var result = await vehicles.SearchAsync(search, limit, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> GetAsync(
        Guid vehicleId,
        IVehicles vehicles,
        CancellationToken cancellationToken)
    {
        var result = await vehicles.GetAsync(vehicleId, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    /// <summary>
    /// Recall campaigns for this vehicle's model. Answers 503 rather than an
    /// empty list when the regulator is unreachable — the two mean opposite
    /// things to somebody deciding whether a car is safe to hand over.
    /// </summary>
    private static async Task<IResult> RecallsAsync(
        Guid vehicleId,
        ISafetyRecalls recalls,
        CancellationToken cancellationToken)
    {
        var result = await recalls.ForVehicleAsync(vehicleId, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> AddAsync(
        NewVehicle request,
        IVehicles vehicles,
        CancellationToken cancellationToken)
    {
        var result = await vehicles.AddAsync(request, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/v1/vehicles/{result.Value.Id}", result.Value)
            : result.Error.ToProblem();
    }
}
