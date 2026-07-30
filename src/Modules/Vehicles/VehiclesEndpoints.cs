// VehiclesEndpoints — the HTTP surface for vehicles and inventory.
//
// Use:  mapped by VehiclesModule.
//       GET  /api/v1/vehicles?search=1HGCM&limit=25
//       GET  /api/v1/vehicles/{id}
//       POST /api/v1/vehicles
//       GET  /api/v1/inventory?rooftopId=&status=Available&stock=A1234
//       GET  /api/v1/inventory/{id}
//       POST /api/v1/inventory
//       POST /api/v1/inventory/{id}/status
// Edit: keep it thin — authorize, delegate, map a Result to a status code.
//       Authorization lives in the services so background callers get it too.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenDealer360.Core;
using OpenDealer360.Vehicles.Contracts;

namespace OpenDealer360.Vehicles;

internal static class VehiclesEndpoints
{
    public static void MapVehicles(this IEndpointRouteBuilder app)
    {
        var vehicles = app.MapGroup("/api/v1/vehicles").WithTags("Vehicles");
        vehicles.MapGet("", SearchVehiclesAsync);
        vehicles.MapGet("/{vehicleId:guid}", GetVehicleAsync);
        vehicles.MapPost("", AddVehicleAsync);

        var inventory = app.MapGroup("/api/v1/inventory").WithTags("Inventory");
        inventory.MapGet("", ListInventoryAsync);
        inventory.MapGet("/{unitId:guid}", GetUnitAsync);
        inventory.MapPost("", ReceiveUnitAsync);
        inventory.MapPost("/{unitId:guid}/status", ChangeStatusAsync);
    }

    private static async Task<IResult> SearchVehiclesAsync(
        IVehicleDirectory vehicles,
        CancellationToken cancellationToken,
        string? search = null,
        int limit = 25)
    {
        var result = await vehicles.SearchAsync(search, limit, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    private static async Task<IResult> GetVehicleAsync(
        Guid vehicleId,
        IVehicleDirectory vehicles,
        CancellationToken cancellationToken)
    {
        var result = await vehicles.GetAsync(vehicleId, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    private static async Task<IResult> AddVehicleAsync(
        NewVehicle request,
        IVehicleDirectory vehicles,
        CancellationToken cancellationToken)
    {
        var result = await vehicles.AddAsync(request, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/v1/vehicles/{result.Value.Id}", result.Value)
            : Problem(result.Error);
    }

    private static async Task<IResult> ListInventoryAsync(
        IInventoryDirectory inventory,
        CancellationToken cancellationToken,
        Guid? rooftopId = null,
        string? status = null,
        string? stock = null,
        string? search = null,
        int limit = 50)
    {
        var query = new InventoryQuery(
            rooftopId is null ? null : new RooftopId(rooftopId.Value),
            status,
            stock,
            search,
            limit);

        var result = await inventory.ListAsync(query, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    private static async Task<IResult> GetUnitAsync(
        Guid unitId,
        IInventoryDirectory inventory,
        CancellationToken cancellationToken)
    {
        var result = await inventory.GetAsync(unitId, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    private static async Task<IResult> ReceiveUnitAsync(
        NewInventoryUnit request,
        IInventoryDirectory inventory,
        CancellationToken cancellationToken)
    {
        var result = await inventory.ReceiveAsync(request, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/v1/inventory/{result.Value.Id}", result.Value)
            : Problem(result.Error);
    }

    private static async Task<IResult> ChangeStatusAsync(
        Guid unitId,
        StatusChangeRequest request,
        IInventoryDirectory inventory,
        CancellationToken cancellationToken)
    {
        var result = await inventory.ChangeStatusAsync(unitId, request, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    /// <summary>Maps a business error to RFC 7807 Problem Details (doc 06 §6).</summary>
    private static IResult Problem(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Results.Problem(
            title: error.Code,
            detail: error.Message,
            statusCode: status,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
