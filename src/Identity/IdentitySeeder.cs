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

        var roles = await SeedRolesAsync(db);
        var manager = roles.Manager;
        var advisor = roles.Advisor;
        var sales = roles.Sales;
        var technicianRole = roles.Technician;

        await SeedDevelopmentAccountsAsync(
            db, hasher, password, firstRooftop, manager, advisor, sales, technicianRole,
            organizationWide, rooftopScoped, unassigned, salesperson, secondFactor, technician);
    }

    /// <summary>
    /// The roles every dealership gets, and what each may reach.
    ///
    /// Shared by the development seeder and by provisioning a real dealership on
    /// purpose: two catalogues would drift, and the version a paying dealership
    /// received would be the one nobody was testing against.
    ///
    /// Reconciled on every run, not created once: each new feature adds
    /// permissions, and Grant is idempotent so nothing already correct changes.
    /// </summary>
    internal static async Task<(Role Manager, Role Advisor, Role Sales, Role Technician)> SeedRolesAsync(
        IdentityDb db)
    {
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
            // The backstop when somebody has lost both their phone and their
            // password (ADR-018). Seeded onto the manager because on a fresh
            // installation there is nobody else to hold it — but it is its own
            // permission, so a dealership that wants resets held by fewer people
            // than rotas can already arrange that without a code change.
            Permissions.StaffResetPassword,
            // The manager holds these organization-wide, which is what lets them
            // add to the catalogue and set the costing method. A rooftop-scoped
            // holder can book stock in and nothing else.
            Permissions.PartsRead,
            Permissions.PartsManage,
            // Closing the month, and unlocking one that was closed. Both are on
            // the manager because there is no separate bookkeeper role yet — but
            // they are two permissions, not one, so a dealership that wants the
            // reopen held by fewer people can already arrange that.
            Permissions.AccountingClosePeriod,
            Permissions.AccountingReopenPeriod,
            // The F&I catalogue: what may be sold with a car, and what it costs
            // the dealership. A group-level arrangement with a provider, so the
            // manager holds it and a rooftop-scoped user does not.
            Permissions.FinanceManageProducts,
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
            // Reading the catalogue, so a part can be picked off the shelf rather
            // than typed as free text. Booking stock IN is deliberately not here:
            // that is the parts department's job, and there is no parts role yet,
            // so it stays with the manager rather than being handed to whoever is
            // nearest.
            Permissions.PartsRead,
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
            // Writing up work means naming the part that is needed, so the
            // catalogue has to be readable. Still no Service.Authorize — a
            // technician finds work, somebody else records that the customer
            // agreed to pay for it.
            Permissions.PartsRead,
        ]);

        await db.SaveChangesAsync();

        return (manager, advisor, sales, technicianRole);
    }

    /// <summary>
    /// Creates the first person at a brand-new dealership: one manager, with no
    /// password, and a one-time code for them to set their own.
    ///
    /// The same enrolment path a starter uses, deliberately. Provisioning could
    /// have invented a temporary password, and that would have been a second
    /// place credentials are created — the one place nobody would be looking
    /// when the first one was hardened.
    /// </summary>
    public static async Task<string> ProvisionFirstManagerAsync(
        string tenantConnection,
        IClock clock,
        string email,
        string displayName)
    {
        ArgumentNullException.ThrowIfNull(clock);

        var options = new DbContextOptionsBuilder<IdentityDb>()
            .UseSqlServer(tenantConnection)
            .Options;

        await using var db = new IdentityDb(options, clock);
        await db.Database.MigrateAsync();

        var roles = await SeedRolesAsync(db);

        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        var existing = await db.Users.SingleOrDefaultAsync(u => u.Email == normalized);

        // Idempotent: provisioning that half-failed and is retried must not throw
        // on the account it already made.
        var user = existing ?? new User(Guid.NewGuid(), normalized, displayName);

        if (existing is null)
        {
            db.Users.Add(user);

            // Organization-wide, because this is the person who will then add
            // everybody else — and nobody exists yet to grant it to them.
            user.Assignments.Add(UserAssignment.ForOrganization(Guid.NewGuid(), user.Id, roles.Manager.Id));
        }

        var now = clock.UtcNow;

        foreach (var outstanding in await db.StaffEnrolments
            .Where(e => e.UserId == user.Id && e.ConsumedAt == null)
            .ToListAsync())
        {
            outstanding.Supersede(now);
        }

        var (code, hash) = StaffEnrolment.NewCode();
        db.StaffEnrolments.Add(new StaffEnrolment(Guid.NewGuid(), user.Id, hash, user.Id, now));

        await db.SaveChangesAsync();

        return code;
    }

    private static async Task SeedDevelopmentAccountsAsync(
        IdentityDb db,
        PasswordHasher<User> hasher,
        string password,
        RooftopId firstRooftop,
        Role manager,
        Role advisor,
        Role sales,
        Role technicianRole,
        DevelopmentAccount organizationWide,
        DevelopmentAccount rooftopScoped,
        DevelopmentAccount unassigned,
        DevelopmentAccount salesperson,
        DevelopmentAccount secondFactor,
        DevelopmentAccount technician)
    {
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
