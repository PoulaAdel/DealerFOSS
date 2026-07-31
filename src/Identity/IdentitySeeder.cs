// IdentitySeeder — creates the development sign-in accounts. Development only.
//
// Use:  called by the application's DevelopmentSeeder. It exists because user,
//       role, and assignment records are internal to this project: without a
//       sanctioned entry point the seeder would need the internals opened up,
//       which is exactly what ADR-017 is preventing.
// Edit: it is idempotent, so repeated runs neither duplicate nor overwrite.
//       Roles are reconciled every run — a database seeded before a feature
//       existed would otherwise leave the development accounts unable to use it.
//       Never seed anything but obviously synthetic accounts here.

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenDealer360.Core;

namespace OpenDealer360.Identity;

/// <summary>One development account to create, described by the caller.</summary>
public sealed record DevelopmentAccount(Guid Id, string Email, string DisplayName);

/// <summary>
/// DEVELOPMENT ONLY. Provisions the identity schema and three well-known accounts:
/// one organization-wide manager, one advisor scoped to a single rooftop, and one
/// with no assignment at all. The asymmetry is deliberate — it is what makes
/// "allowed one thing, refused another" a real assertion in the tests.
/// </summary>
public static class IdentitySeeder
{
    public const string ManagerRole = "Manager";

    public const string AdvisorRole = "Advisor";

    public static async Task SeedDevelopmentAsync(
        string tenantConnection,
        IClock clock,
        RooftopId firstRooftop,
        string password,
        DevelopmentAccount organizationWide,
        DevelopmentAccount rooftopScoped,
        DevelopmentAccount unassigned)
    {
        ArgumentNullException.ThrowIfNull(organizationWide);
        ArgumentNullException.ThrowIfNull(rooftopScoped);
        ArgumentNullException.ThrowIfNull(unassigned);

        var options = new DbContextOptionsBuilder<IdentityDb>()
            .UseSqlServer(tenantConnection)
            .Options;

        await using var db = new IdentityDb(options, clock);
        await db.Database.MigrateAsync();

        var hasher = new PasswordHasher<User>();

        // Reconciled on every run, not created once: each new feature adds
        // permissions, and Grant is idempotent so nothing already correct changes.
        var manager = await UpsertRoleAsync(db, ManagerRole,
        [
            Permissions.OrganizationRead,
            Permissions.OrganizationManage,
            Permissions.CustomersRead,
            Permissions.CustomersCreate,
            Permissions.VehiclesRead,
            Permissions.InventoryRead,
            Permissions.InventoryManage,
            Permissions.LeadsRead,
            Permissions.LeadsManage,
        ]);

        // An advisor can look a customer up but not create one, and can see stock
        // and enquiries but not move either.
        var advisor = await UpsertRoleAsync(db, AdvisorRole,
        [
            Permissions.OrganizationRead,
            Permissions.CustomersRead,
            Permissions.VehiclesRead,
            Permissions.InventoryRead,
            Permissions.LeadsRead,
        ]);

        if (await db.Users.AnyAsync())
        {
            // Same reasoning for credentials: a database seeded before passwords
            // existed heals instead of needing a wipe.
            var passwordless = await db.Users.Where(u => u.PasswordHash == null).ToListAsync();
            foreach (var existing in passwordless)
            {
                existing.SetPasswordHash(hasher.HashPassword(existing, password));
            }

            await db.SaveChangesAsync();
            return;
        }

        var users = new[]
        {
            new User(organizationWide.Id, organizationWide.Email, organizationWide.DisplayName),
            new User(rooftopScoped.Id, rooftopScoped.Email, rooftopScoped.DisplayName),
            new User(unassigned.Id, unassigned.Email, unassigned.DisplayName),
        };

        foreach (var user in users)
        {
            user.SetPasswordHash(hasher.HashPassword(user, password));
        }

        db.Users.AddRange(users);

        // The scoped user is deliberately tied to one rooftop, so an attempt to
        // reach a sibling rooftop is a genuine authorization failure.
        db.UserAssignments.AddRange(
            UserAssignment.ForOrganization(Guid.NewGuid(), organizationWide.Id, manager.Id),
            UserAssignment.ForRooftop(Guid.NewGuid(), rooftopScoped.Id, advisor.Id, firstRooftop));

        await db.SaveChangesAsync();
    }

    /// <summary>Finds a role by name or creates it, then grants the permissions.</summary>
    private static async Task<Role> UpsertRoleAsync(
        IdentityDb db,
        string name,
        IReadOnlyCollection<string> permissions)
    {
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Name == name);

        if (role is null)
        {
            role = new Role(Guid.NewGuid(), name);
            db.Roles.Add(role);
        }

        foreach (var permission in permissions)
        {
            role.Grant(permission);
        }

        await db.SaveChangesAsync();
        return role;
    }
}
