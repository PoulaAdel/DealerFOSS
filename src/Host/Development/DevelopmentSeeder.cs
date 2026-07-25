using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OpenDealer360.Identity.Data;
using OpenDealer360.Identity.Domain;
using OpenDealer360.Organization.Data;
using OpenDealer360.Organization.Domain;
using OpenDealer360.Core;
using OpenDealer360.Tenancy;

namespace OpenDealer360.Host.Development;

/// <summary>
/// DEVELOPMENT ONLY. Provisions the host catalog and two sample dealer
/// organizations — one multi-rooftop, one single-rooftop (doc 08 §8) — each in
/// its own database, so tenant isolation and rooftop resolution can be exercised
/// end to end. Idempotent: safe to run repeatedly.
/// </summary>
public static class DevelopmentSeeder
{
    public static async Task RunAsync(WebApplication app, string hostConnectionString)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var hostCatalog = services.GetRequiredService<HostCatalogDbContext>();
        var protector = services.GetRequiredService<ISecretProtector>();
        var clock = services.GetRequiredService<IClock>();

        await hostCatalog.Database.MigrateAsync();

        await SeedTenantAsync(hostCatalog, protector, clock, hostConnectionString, "northgroup", BuildNorthGroup);
        await SeedTenantAsync(hostCatalog, protector, clock, hostConnectionString, "citymotors", BuildCityMotors);

        await hostCatalog.SaveChangesAsync();
    }

    private static async Task SeedTenantAsync(
        HostCatalogDbContext hostCatalog,
        ISecretProtector protector,
        IClock clock,
        string hostConnectionString,
        string slug,
        Func<DealerOrganization> build)
    {
        var tenantConnection = new SqlConnectionStringBuilder(hostConnectionString)
        {
            InitialCatalog = $"OpenDealer360_Tenant_{slug}",
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseSqlServer(tenantConnection)
            .Options;

        await using var tenantDb = new OrganizationDbContext(options, clock);
        await tenantDb.Database.MigrateAsync();

        if (!await tenantDb.Organizations.AnyAsync())
        {
            tenantDb.Organizations.Add(build());
            await tenantDb.SaveChangesAsync();
        }

        var organizationId = await tenantDb.Organizations.Select(o => o.Id).FirstAsync();
        var name = await tenantDb.Organizations.Select(o => o.Name).FirstAsync();

        await SeedIdentityAsync(tenantConnection, clock, tenantDb);

        var record = await hostCatalog.Tenants.SingleOrDefaultAsync(t => t.Slug == slug);
        if (record is null)
        {
            hostCatalog.Tenants.Add(new TenantRecord
            {
                Id = organizationId.Value,
                Name = name,
                Slug = slug,
                Status = TenantStatus.Active,
                ProtectedConnectionString = protector.Protect(tenantConnection),
                DatabaseVersion = 1,
                CreatedAt = clock.UtcNow,
            });
        }
    }

    /// <summary>
    /// Well-known development users. Fixed ids so tests and manual checks can
    /// act as a specific scope without first querying for one.
    /// </summary>
    public static class DevUsers
    {
        /// <summary>Holds Organization.Read across every rooftop.</summary>
        public static Guid OrganizationWide { get; } = new("11111111-1111-1111-1111-111111111111");

        /// <summary>Holds Organization.Read at the tenant's first rooftop only.</summary>
        public static Guid FirstRooftopOnly { get; } = new("22222222-2222-2222-2222-222222222222");

        /// <summary>Exists and is active, but holds no assignment at all.</summary>
        public static Guid Unassigned { get; } = new("33333333-3333-3333-3333-333333333333");
    }

    private static async Task SeedIdentityAsync(
        string tenantConnection,
        IClock clock,
        OrganizationDbContext tenantDb)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer(tenantConnection)
            .Options;

        await using var identityDb = new IdentityDbContext(options, clock);
        await identityDb.Database.MigrateAsync();

        if (await identityDb.Users.AnyAsync())
        {
            return;
        }

        var manager = new Role(Guid.NewGuid(), "Manager");
        manager.Grant(Permissions.OrganizationRead);
        manager.Grant(Permissions.OrganizationManage);

        var advisor = new Role(Guid.NewGuid(), "Advisor");
        advisor.Grant(Permissions.OrganizationRead);

        identityDb.Roles.AddRange(manager, advisor);

        identityDb.Users.AddRange(
            new User(DevUsers.OrganizationWide, "gm@dev.local", "Organization Manager"),
            new User(DevUsers.FirstRooftopOnly, "advisor@dev.local", "Single Rooftop Advisor"),
            new User(DevUsers.Unassigned, "nobody@dev.local", "Unassigned User"));

        // The scoped user is deliberately tied to one rooftop, so an attempt to
        // read a sibling rooftop is a genuine authorization failure.
        var firstRooftopId = await tenantDb.Rooftops
            .OrderBy(r => r.Code)
            .Select(r => r.Id)
            .FirstAsync();

        identityDb.UserAssignments.AddRange(
            UserAssignment.ForOrganization(Guid.NewGuid(), DevUsers.OrganizationWide, manager.Id),
            UserAssignment.ForRooftop(Guid.NewGuid(), DevUsers.FirstRooftopOnly, advisor.Id, firstRooftopId));

        await identityDb.SaveChangesAsync();
    }

    private static DealerOrganization BuildNorthGroup()
    {
        var org = new DealerOrganization(DealerOrganizationId.New(), "North Auto Group", "northgroup");

        var entity = new LegalEntity(LegalEntityId.New(), org.Id, "North Auto LLC");
        entity.SetRegistration("North Auto Group, LLC", "82-1234567");

        var downtown = new Rooftop(RooftopId.New(), entity.Id, "North Auto Downtown", "NAG-01", "America/New_York");
        AddCoreDepartments(downtown);

        var uptown = new Rooftop(RooftopId.New(), entity.Id, "North Auto Uptown", "NAG-02", "America/New_York");
        AddCoreDepartments(uptown);

        entity.Rooftops.Add(downtown);
        entity.Rooftops.Add(uptown);
        org.LegalEntities.Add(entity);
        return org;
    }

    private static DealerOrganization BuildCityMotors()
    {
        var org = new DealerOrganization(DealerOrganizationId.New(), "City Motors", "citymotors");
        var entity = new LegalEntity(LegalEntityId.New(), org.Id, "City Motors Inc");

        var main = new Rooftop(RooftopId.New(), entity.Id, "City Motors Main", "CM-01", "America/Chicago");
        AddCoreDepartments(main);

        entity.Rooftops.Add(main);
        org.LegalEntities.Add(entity);
        return org;
    }

    private static void AddCoreDepartments(Rooftop rooftop)
    {
        rooftop.Departments.Add(new Department(DepartmentId.New(), rooftop.Id, "Sales", DepartmentKind.Sales));
        rooftop.Departments.Add(new Department(DepartmentId.New(), rooftop.Id, "Finance & Insurance", DepartmentKind.FinanceAndInsurance));
        rooftop.Departments.Add(new Department(DepartmentId.New(), rooftop.Id, "Service", DepartmentKind.Service));
        rooftop.Departments.Add(new Department(DepartmentId.New(), rooftop.Id, "Parts", DepartmentKind.Parts));
    }
}
