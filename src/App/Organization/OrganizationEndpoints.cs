// OrganizationEndpoints — the HTTP surface for the dealer organization structure.
//
// Use:  mapped from Program.cs; routes under /api/v1/organization.
//       GET /api/v1/organization
//       GET /api/v1/organization/rooftops/{id}
// Edit: keep it thin — delegate, then map a Result to a status code. No business
//       logic and no data access belong here. Authorization lives in
//       OrganizationService so every caller is checked, not only HTTP ones.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenDealer360.App;
using OpenDealer360.Core;

namespace OpenDealer360.Organization;

internal static class OrganizationEndpoints
{
    public static void MapOrganization(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/organization").WithTags("Organization");

        group.MapGet("", GetStructureAsync);
        group.MapGet("/rooftops/{rooftopId:guid}", GetRooftopAsync);
    }

    private static async Task<IResult> GetStructureAsync(
        IOrganization organization,
        CancellationToken cancellationToken)
    {
        var result = await organization.GetStructureAsync(cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> GetRooftopAsync(
        Guid rooftopId,
        IOrganization organization,
        CancellationToken cancellationToken)
    {
        var result = await organization.GetRooftopAsync(new RooftopId(rooftopId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }
}
