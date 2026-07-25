using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenDealer360.Modules.Organization.Contracts;
using OpenDealer360.Platform.Kernel;

namespace OpenDealer360.Modules.Organization;

/// <summary>
/// HTTP surface for the Organization capability. Thin by design: it delegates
/// and maps a <see cref="Result"/> to a status code. Authorization lives in the
/// service so every caller is checked, not only HTTP ones (doc 06 §3).
/// </summary>
internal static class OrganizationEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/organization").WithTags("Organization");

        group.MapGet("", GetStructure);
        group.MapGet("/rooftops/{rooftopId:guid}", GetRooftop);
    }

    private static async Task<IResult> GetStructure(
        IOrganizationDirectory directory,
        CancellationToken cancellationToken)
    {
        var result = await directory.GetStructureAsync(cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    private static async Task<IResult> GetRooftop(
        Guid rooftopId,
        IOrganizationDirectory directory,
        CancellationToken cancellationToken)
    {
        var result = await directory.GetRooftopAsync(new RooftopId(rooftopId), cancellationToken);
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
