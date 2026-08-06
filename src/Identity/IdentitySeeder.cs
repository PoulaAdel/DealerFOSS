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
using DealerFOSS.Core;

namespace DealerFOSS.Identity;

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

    public const string SalespersonRole = "Salesperson";

    public const string TechnicianRole = "Technician";

    public static async Task SeedDevelopmentAsync(
        string tenantConnection,
        IClock clock,
        RooftopId firstRooftop,
        string password,
        DevelopmentAccount organizationWide,
        DevelopmentAccount rooftopScoped,
        DevelopmentAccount unassigned,
        DevelopmentAccount salesperson,
        DevelopmentAccount secondFactor,
        DevelopmentAccount technician)
    {
        ArgumentNullException.ThrowIfNull(organizationWide);
        ArgumentNullException.ThrowIfNull(rooftopScoped);
        ArgumentNullException.ThrowIfNull(unassigned);
        ArgumentNullException.ThrowIfNull(salesperson);
        ArgumentNullException.ThrowIfNull(secondFactor);
        ArgumentNullException.ThrowIfNull(technician);

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
            Permissions.DealsRead,
            Permissions.DealsWrite,
            Permissions.DealsApprove,
            Permissions.ServiceRead,
            Permissions.ServiceWrite,
            Permissions.ServiceAuthorize,
            Permissions.AccountingRead,
            Permissions.AccountingPost,
            // The only role that may make a posted entry disappear.
            Permissions.AccountingReverse,
            // Seeing and managing the people who work here. The manager holds it
            // organization-wide, which is what lets them add a starter and hand
            // out group-level access; a rooftop-scoped holder could do neither.
            Permissions.StaffRead,
            Permissions.StaffManage,
            // Who must hold a second factor is a management decision, so the
            // manager role is where it sits. No role is seeded as requiring one
            // — that is the dealership's call, not ours.
            Permissions.SecurityManagePolicy,
            // Bringing the dealership's old records in is a management job, and
            // one that happens a handful of times in the life of an installation.
            Permissions.MigrationImport,
            // A dealership must be able to take its own data with it. Withholding
            // this from the person who runs the group would make the promise
            // hollow (doc 05 §6).
            Permissions.MigrationExport,
        ]);

        // An advisor can look a customer up but not create one, and can see stock,
        // enquiries and deals but not move any of them.
        var advisor = await UpsertRoleAsync(db, AdvisorRole,
        [
            Permissions.OrganizationRead,
            Permissions.CustomersRead,
            Permissions.VehiclesRead,
            Permissions.InventoryRead,
            Permissions.LeadsRead,
            Permissions.DealsRead,
            Permissions.AccountingRead,
            // The service advisor's job in full: book a car in, write up what was
            // found, ring the customer, and record what they said. This is the
            // role the workshop actually runs on, so it holds Service.Authorize
            // even though it cannot move a deal — the two are unrelated jobs.
            Permissions.ServiceRead,
            Permissions.ServiceWrite,
            Permissions.ServiceAuthorize,
            // Invoicing a job posts it, so an advisor who can invoice must be
            // able to post. Reversing is still a manager's job.
            Permissions.AccountingPost,
        ]);

        // A salesperson does the whole job except sign their own deal off. That
        // one missing permission is what makes segregation of duties a real thing
        // the tests can assert rather than a claim in a document.
        var sales = await UpsertRoleAsync(db, SalespersonRole,
        [
            Permissions.OrganizationRead,
            Permissions.CustomersRead,
            Permissions.CustomersCreate,
            Permissions.VehiclesRead,
            Permissions.InventoryRead,
            Permissions.InventoryManage,
            Permissions.LeadsRead,
            Permissions.LeadsManage,
            // Reading the staff list, not managing it. Handing an enquiry to a
            // named colleague needs to know who the colleagues are; changing what
            // any of them may reach is a manager's job and stays one.
            Permissions.StaffRead,
            Permissions.DealsRead,
            Permissions.DealsWrite,
            // Delivering a car posts the sale, so a salesperson who can deliver
            // must be able to post it. Reversing is a manager's job, because that
            // is the operation that can hide a mistake — and it is now a separate
            // permission rather than a comment hoping nobody notices.
            Permissions.AccountingRead,
            Permissions.AccountingPost,
        ]);

        // A technician writes up what they find and cannot say the customer agreed
        // to pay for it. That one missing permission is the workshop's segregation
        // of duties, and it is what the tests assert rather than a claim in a
        // document. They also have no reason to see a deal or a customer's whole
        // record — the car and the job in front of them is the job.
        var technicianRole = await UpsertRoleAsync(db, TechnicianRole,
        [
            Permissions.OrganizationRead,
            Permissions.VehiclesRead,
            // A job names whose car it is, so reading the job means reading the
            // customer. Withholding this would not hide the name — it would break
            // the list, which is the worse kind of security theatre.
            Permissions.CustomersRead,
            Permissions.ServiceRead,
            Permissions.ServiceWrite,
        ]);

        // Accounts are reconciled one at a time rather than all-or-nothing, for the
        // same reason roles are: a database seeded before an account existed
        // should grow the new one instead of needing a wipe.
        await UpsertUserAsync(db, hasher, password, organizationWide,
            () => UserAssignment.ForOrganization(Guid.NewGuid(), organizationWide.Id, manager.Id));

        // Deliberately tied to one rooftop, so reaching a sibling rooftop is a
        // genuine authorization failure rather than an arranged one.
        await UpsertUserAsync(db, hasher, password, rooftopScoped,
            () => UserAssignment.ForRooftop(Guid.NewGuid(), rooftopScoped.Id, advisor.Id, firstRooftop));

        await UpsertUserAsync(db, hasher, password, salesperson,
            () => UserAssignment.ForRooftop(Guid.NewGuid(), salesperson.Id, sales.Id, firstRooftop));

        await UpsertUserAsync(db, hasher, password, technician,
            () => UserAssignment.ForRooftop(Guid.NewGuid(), technician.Id, technicianRole.Id, firstRooftop));

        // No assignment at all, on purpose.
        await UpsertUserAsync(db, hasher, password, unassigned, assignment: null);

        // Reserved for second-factor tests; needs to sign in, nothing more.
        await UpsertUserAsync(db, hasher, password, secondFactor, assignment: null);

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Creates the account if it is missing, and heals a missing password if the
    /// account predates credentials. Never overwrites an existing password.
    /// </summary>
    private static async Task UpsertUserAsync(
        IdentityDb db,
        PasswordHasher<User> hasher,
        string password,
        DevelopmentAccount account,
        Func<UserAssignment>? assignment)
    {
        var existing = await db.Users.SingleOrDefaultAsync(u => u.Id == account.Id);

        if (existing is not null)
        {
            if (existing.PasswordHash is null)
            {
                existing.SetPasswordHash(hasher.HashPassword(existing, password));
            }

            return;
        }

        var user = new User(account.Id, account.Email, account.DisplayName);
        user.SetPasswordHash(hasher.HashPassword(user, password));
        db.Users.Add(user);

        if (assignment is not null)
        {
            db.UserAssignments.Add(assignment());
        }

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
