using Microsoft.EntityFrameworkCore;
using OpenDealer360.Modules.Organization.Contracts;
using OpenDealer360.Modules.Organization.Data;
using OpenDealer360.Platform.Kernel;

namespace OpenDealer360.Modules.Organization;

/// <summary>
/// Read workflows for the Organization capability. Bound to the tenant database
/// resolved for the current request — it never chooses a connection itself.
/// </summary>
public sealed class OrganizationService(OrganizationDbContext db) : IOrganizationDirectory
{
    private readonly OrganizationDbContext _db = db;

    public async Task<OrganizationView?> GetStructureAsync(CancellationToken cancellationToken)
    {
        var organization = await _db.Organizations
            .AsNoTracking()
            .Include(o => o.LegalEntities)
                .ThenInclude(le => le.Rooftops)
                    .ThenInclude(r => r.Departments)
            .SingleOrDefaultAsync(cancellationToken);

        if (organization is null)
        {
            return null;
        }

        return new OrganizationView(
            organization.Id,
            organization.Name,
            organization.Slug,
            organization.LegalEntities
                .OrderBy(le => le.Name, StringComparer.Ordinal)
                .Select(le => new LegalEntityView(
                    le.Id,
                    le.Name,
                    le.Rooftops
                        .OrderBy(r => r.Code, StringComparer.Ordinal)
                        .Select(r => new RooftopView(
                            r.Id,
                            r.Name,
                            r.Code,
                            r.TimeZone,
                            r.Departments
                                .OrderBy(d => d.Kind)
                                .Select(d => new DepartmentView(d.Id, d.Name, d.Kind.ToString()))
                                .ToList()))
                        .ToList()))
                .ToList());
    }

    public async Task<bool> RooftopExistsAsync(RooftopId rooftopId, CancellationToken cancellationToken) =>
        await _db.Rooftops.AsNoTracking().AnyAsync(r => r.Id == rooftopId, cancellationToken);
}
