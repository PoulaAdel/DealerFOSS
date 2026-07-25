using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenDealer360.Modules.Organization.Contracts;

namespace OpenDealer360.Modules.Organization;

/// <summary>
/// HTTP surface for the Organization capability. Thin: it resolves the request's
/// tenant (via middleware, upstream) and delegates to the directory. Server-side
/// authorization is added with the Identity module (doc 06 §3); until then these
/// endpoints require a resolved tenant but not yet a permission.
/// </summary>
internal static class OrganizationEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/organization").WithTags("Organization");

        group.MapGet("", GetStructure);
    }

    private static async Task<IResult> GetStructure(IOrganizationDirectory directory, CancellationToken cancellationToken)
    {
        var view = await directory.GetStructureAsync(cancellationToken);
        return view is null ? Results.NotFound() : Results.Ok(view);
    }
}
