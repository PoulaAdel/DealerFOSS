// DevelopmentSeeder — creates sample dealer organizations so the system can be
// run and tested without hand-building data. Development only.
//
// Use:  runs at startup when Seed:Enabled is true. It is idempotent, so
//       repeated runs neither duplicate nor overwrite.
// Edit: the DevUsers ids are fixed on purpose — integration tests and
//       deploy/verify-e2e.ps1 both reference them. Changing one breaks both.
//       Sample data must stay synthetic; never seed real customer data.

using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OpenDealer360.Identity.Data;
using OpenDealer360.Identity.Domain;
using OpenDealer360.Organization.Data;
using OpenDealer360.Organization.Domain;
using OpenDealer360.Core;
using OpenDealer360.Customers.Data;
using OpenDealer360.Customers.Domain;
using CustomerAddress = OpenDealer360.Customers.Domain.Address;
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
        await SeedCustomersAsync(tenantConnection, clock);

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

        /// <summary>Shared password for every development account.</summary>
        public const string Password = "Dev@Pass1!";

        public const string OrganizationWideEmail = "gm@dev.local";
        public const string FirstRooftopOnlyEmail = "advisor@dev.local";
        public const string UnassignedEmail = "nobody@dev.local";
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

        var hasher = new PasswordHasher<User>();

        // Roles are reconciled every run, not created once. Each new module adds
        // permissions, and a database seeded before that module existed would
        // otherwise leave the development accounts unable to use it. Grant is
        // idempotent, so re-running changes nothing that is already correct.
        var manager = await UpsertRoleAsync(identityDb, "Manager",
        [
            Permissions.OrganizationRead,
            Permissions.OrganizationManage,
            Permissions.CustomersRead,
            Permissions.CustomersCreate,
        ]);

        // An advisor can look a customer up but not create one, so the tests have
        // a role that is allowed one thing and refused another.
        var advisor = await UpsertRoleAsync(identityDb, "Advisor",
        [
            Permissions.OrganizationRead,
            Permissions.CustomersRead,
        ]);

        if (await identityDb.Users.AnyAsync())
        {
            // Same reasoning for credentials: a database seeded before passwords
            // existed heals instead of needing a wipe.
            var passwordless = await identityDb.Users
                .Where(u => u.PasswordHash == null)
                .ToListAsync();

            foreach (var existing in passwordless)
            {
                existing.SetPasswordHash(hasher.HashPassword(existing, DevUsers.Password));
            }

            await identityDb.SaveChangesAsync();
            return;
        }

        // Every development account shares one obvious password. It is only ever
        // created in Development, and the seeder never runs elsewhere.
        var users = new[]
        {
            new User(DevUsers.OrganizationWide, DevUsers.OrganizationWideEmail, "Organization Manager"),
            new User(DevUsers.FirstRooftopOnly, DevUsers.FirstRooftopOnlyEmail, "Single Rooftop Advisor"),
            new User(DevUsers.Unassigned, DevUsers.UnassignedEmail, "Unassigned User"),
        };

        foreach (var user in users)
        {
            user.SetPasswordHash(hasher.HashPassword(user, DevUsers.Password));
        }

        identityDb.Users.AddRange(users);

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

    /// <summary>
    /// Finds a role by name or creates it, then grants the given permissions.
    /// Called on every run so a database created before a module existed picks up
    /// that module's permissions instead of silently lacking them.
    /// </summary>
    private static async Task<Role> UpsertRoleAsync(
        IdentityDbContext identityDb,
        string name,
        IReadOnlyCollection<string> permissions)
    {
        var role = await identityDb.Roles.SingleOrDefaultAsync(r => r.Name == name);

        if (role is null)
        {
            role = new Role(Guid.NewGuid(), name);
            identityDb.Roles.Add(role);
        }

        foreach (var permission in permissions)
        {
            role.Grant(permission);
        }

        await identityDb.SaveChangesAsync();
        return role;
    }

    /// <summary>
    /// A handful of invented customers, so search returns something on a fresh
    /// install. Names are obviously fictional; never seed real people.
    /// </summary>
    private static async Task SeedCustomersAsync(string tenantConnection, IClock clock)
    {
        var options = new DbContextOptionsBuilder<CustomersDbContext>()
            .UseSqlServer(tenantConnection)
            .Options;

        await using var customersDb = new CustomersDbContext(options, clock);
        await customersDb.Database.MigrateAsync();

        if (await customersDb.Customers.AnyAsync())
        {
            return;
        }

        var alvarez = Customer.Person(Guid.NewGuid(), "Marisol", "Alvarez");
        alvarez.AddContactPoint(Guid.NewGuid(), ContactKind.Email, "marisol.alvarez@example.test");
        alvarez.AddContactPoint(Guid.NewGuid(), ContactKind.Phone, "(555) 010-2030");
        alvarez.SetAddress(CustomerAddress.Create(
            "18 Kestrel Way", null, "Springfield", "IL", "62704", "US"));

        var okafor = Customer.Person(Guid.NewGuid(), "Daniel", "Okafor");
        okafor.AddContactPoint(Guid.NewGuid(), ContactKind.Mobile, "+15550117788");

        var fleet = Customer.Business(Guid.NewGuid(), "Brightline Facilities Ltd");
        fleet.AddContactPoint(Guid.NewGuid(), ContactKind.Email, "fleet@brightline.example.test");

        customersDb.Customers.AddRange(alvarez, okafor, fleet);
        await customersDb.SaveChangesAsync();
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
