// SecurityEndpoints — the rules a dealer organization applies to its own staff.
//
// Use:  GET  /api/v1/security/second-factor-policy   which roles demand one
//       POST /api/v1/security/second-factor-policy   turn it on or off for a role
// Edit: the permission is checked here rather than inside Identity, because
//       Identity answers "what may this user reach?" and must not also decide
//       who may change the answer — that is circular. The check demands
//       organization-wide scope: a rule about the whole dealership is not set
//       from one lot.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using DealerFOSS.Core;
using DealerFOSS.Identity;

namespace DealerFOSS.App;

internal static class SecurityEndpoints
{
    public static void MapSecurity(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/security").WithTags("Security");

        group.MapGet("/second-factor-policy", GetSecondFactorPolicyAsync);
        group.MapPost("/second-factor-policy", SetSecondFactorPolicyAsync);
    }

    private static async Task<IResult> GetSecondFactorPolicyAsync(
        ICurrentUser currentUser,
        IAccessDirectory access,
        ISecurityPolicy policy,
        CancellationToken cancellationToken)
    {
        if (!await MayManageAsync(currentUser, access, cancellationToken))
        {
            return Refused.ToProblem();
        }

        return Results.Ok(await policy.ListSecondFactorPolicyAsync(cancellationToken));
    }

    private static async Task<IResult> SetSecondFactorPolicyAsync(
        SecondFactorPolicyRequest request,
        ICurrentUser currentUser,
        IAccessDirectory access,
        ISecurityPolicy policy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!await MayManageAsync(currentUser, access, cancellationToken))
        {
            return Refused.ToProblem();
        }

        var result = await policy.RequireSecondFactorAsync(
            request.RoleId, request.Required, currentUser.Id, cancellationToken);

        return result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
    }

    /// <summary>
    /// Organization-wide, not "covers some rooftop". Holding the permission at
    /// one lot must not let somebody change what the whole group has to do.
    /// </summary>
    private static async Task<bool> MayManageAsync(
        ICurrentUser currentUser,
        IAccessDirectory access,
        CancellationToken cancellationToken)
    {
        var scope = await access.GetAuthorizedScopeAsync(
            currentUser.Id, Permissions.SecurityManagePolicy, cancellationToken);

        return scope.IsOrganizationWide;
    }

    private static Error Refused { get; } = Error.Forbidden(
        "security.policy_forbidden",
        "Changing the dealership's security policy needs organization-wide permission.");
}

/// <summary>Turn the second-factor obligation on or off for one role.</summary>
internal sealed record SecondFactorPolicyRequest(Guid RoleId, bool Required);
