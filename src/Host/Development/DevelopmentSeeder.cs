using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OpenDealer360.Modules.Organization.Data;
using OpenDealer360.Modules.Organization.Domain;
using OpenDealer360.Platform.Kernel;
using OpenDealer360.Platform.Persistence.HostCatalog;
using OpenDealer360.Platform.Security;

namespace OpenDealer360.Host.Development;

/// <summary>
/// DEVELOPMENT ONLY. Provisions the host catalog and two sample dealer
/// organizations — one multi-rooftop, one single-rooftop (doc 08 §8) — each in
/// its own database, so tenant isolation and rooftop resolution can be exercised
/// end to end. Idempotent: safe to run repeatedly.
/// </summary>
internal static class DevelopmentSeeder
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
