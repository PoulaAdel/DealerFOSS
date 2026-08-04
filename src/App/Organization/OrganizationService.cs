// OrganizationService — the read workflows, and the rooftop-scope check applied
// to them.
//
// Use:  through IOrganization.
// Edit: authorization lives here, not in the endpoint, so a background job gets
//       the same check as an HTTP call. An unauthorized rooftop and an unknown
//       one deliberately return the same failure, so the response cannot be used
//       to enumerate rooftops. Removing either check fails RooftopAuthorizationTests.

using Microsoft.EntityFrameworkCore;
using DealerFOSS.Identity;
using DealerFOSS.Organization;
using DealerFOSS.Core;
using DealerFOSS.Data;

namespace DealerFOSS.Organization;

/// <summary>
/// Read workflows for the Organization capability, scoped to what the caller is
/// authorized to see. Bound to the tenant database resolved for the current
/// request; it never chooses a connection itself.
/// </summary>
/// <remarks>
/// Authorization is applied here rather than in the endpoint so that every
/// caller — HTTP today, a background job later — goes through the same check.
/// </remarks>
public sealed class OrganizationService(
    TenantDb db,
    IAccessDirectory access,
    ICurrentUser currentUser)
    : IOrganization
{
    private const string ReadPermission = "Organization.Read";

    private readonly TenantDb _db = db;
    private readonly IAccessDirectory _access = access;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<Result<OrganizationView>> GetStructureAsync(CancellationToken cancellationToken)
    {
        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<OrganizationView>(AccessErrors.Forbidden);
        }

        var organization = await _db.Organizations
            .AsNoTracking()
            .Include(o => o.LegalEntities)
                .ThenInclude(le => le.Rooftops)
                    .ThenInclude(r => r.Departments)
            .SingleOrDefaultAsync(cancellationToken);

        if (organization is null)
        {
            return Result.Failure<OrganizationView>(AccessErrors.OrganizationNotProvisioned);
        }

        // Rooftops the caller cannot reach are removed, not merely hidden in the
        // UI: the response must never carry another rooftop's data.
        var legalEntities = organization.LegalEntities
            .OrderBy(le => le.Name, StringComparer.Ordinal)
            .Select(le => new LegalEntityView(
                le.Id,
                le.Name,
                le.Rooftops
                    .Where(r => scope.Covers(r.Id))
                    .OrderBy(r => r.Code, StringComparer.Ordinal)
                    .Select(r => new RooftopView(
                        r.Id,
                        r.Name,
                        r.Code,
                        r.TimeZone,
                        r.Departments
                            .OrderBy(d => d.Kind)
                            .Select(d => new DepartmentView(d.Id, d.Name, d.Kind.ToString()))
                            .ToList())
                    {
                        LegalEntityId = le.Id,
                    })
                    .ToList()))
            .Where(le => le.Rooftops.Count > 0)
            .ToList();

        return Result.Success(new OrganizationView(
            organization.Id,
            organization.Name,
            organization.Slug,
            legalEntities));
    }

    public async Task<Result<RooftopView>> GetRooftopAsync(RooftopId rooftopId, CancellationToken cancellationToken)
    {
        // Authorize before reading, and return the same failure whether the
        // rooftop is unauthorized or absent — otherwise the response reveals
        // which rooftops exist in the organization.
        if (!await _access.IsAuthorizedAsync(_currentUser.Id, ReadPermission, rooftopId, cancellationToken))
        {
            return Result.Failure<RooftopView>(AccessErrors.Forbidden);
        }

        var rooftop = await _db.Rooftops
            .AsNoTracking()
            .Include(r => r.Departments)
            .SingleOrDefaultAsync(r => r.Id == rooftopId, cancellationToken);

        if (rooftop is null)
        {
            return Result.Failure<RooftopView>(AccessErrors.Forbidden);
        }

        return Result.Success(new RooftopView(
            rooftop.Id,
            rooftop.Name,
            rooftop.Code,
            rooftop.TimeZone,
            rooftop.Departments
                .OrderBy(d => d.Kind)
                .Select(d => new DepartmentView(d.Id, d.Name, d.Kind.ToString()))
                .ToList())
        {
            LegalEntityId = rooftop.LegalEntityId,
        });
    }

    public async Task<bool> RooftopExistsAsync(RooftopId rooftopId, CancellationToken cancellationToken) =>
        await _db.Rooftops.AsNoTracking().AnyAsync(r => r.Id == rooftopId, cancellationToken);
}

/// <summary>Stable error codes for the Organization capability (doc 06 §6).</summary>
internal static class AccessErrors
{
    public static Error Forbidden { get; } = Error.Forbidden(
        "organization.forbidden",
        "You do not have access to this rooftop.");

    public static Error OrganizationNotProvisioned { get; } = Error.NotFound(
        "organization.not_provisioned",
        "This dealer organization has not been provisioned.");
}
