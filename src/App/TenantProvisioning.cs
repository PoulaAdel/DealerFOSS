// TenantProvisioning — creating a dealership without a developer.
//
// Use:  through ITenantProvisioning, from the operator console.
//
// Where this lives: the composition root (DealerFOSS.App), beside
// DevelopmentSeeder, which does the same job for development data. It was tried
// in Administration and then in Tenancy, and the architecture tests refused both
// — correctly. Provisioning builds a database AND writes an organization and a
// chart of accounts, and neither of those namespaces may do both. It is not a
// capability; it composes several, which is what the root is for.
// Edit: the order below is not arbitrary and the comments say why at each step.
//       Two of them have bitten already:
//
//       THE BOOKS MUST BE OPENED. Nothing posts into a month that has not been
//       opened, so a dealership provisioned without this looks fine until its
//       very first sale is refused for a reason nobody would guess.
//
//       THE FIRST MANAGER GETS AN ENROLMENT CODE, not a password. Inventing a
//       temporary password here would be a second place credentials are created,
//       and the one nobody would think to harden.
//
//       The connection string is written to the host catalog ENCRYPTED and there
//       is deliberately no way to read it back. `--repoint-tenants` exists so
//       nothing needs to.

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using DealerFOSS.Accounting;
using DealerFOSS.Core;
using DealerFOSS.Data;
using DealerFOSS.Identity;
using DealerFOSS.Organization;
using DealerFOSS.Tenancy;

namespace DealerFOSS.App;

public interface ITenantProvisioning
{
    Task<Result<ProvisionedTenant>> CreateAsync(NewTenant tenant, CancellationToken cancellationToken);
}

/// <summary>
/// What to create. Deliberately small: a dealership, its first location, and the
/// person who will set everything else up.
/// </summary>
public sealed record NewTenant(
    string Slug,
    string Name,
    string LegalEntityName,
    string RooftopName,
    string RooftopCode,
    string ManagerEmail,
    string ManagerName,
    string TimeZone = "UTC");

/// <summary>
/// The result, including the manager's one-time code. Returned once and never
/// retrievable — only its hash is stored.
/// </summary>
public sealed record ProvisionedTenant(
    string Slug,
    string Name,
    string ManagerEmail,
    string EnrolmentCode,
    DateTimeOffset OpenedBooksFrom);

public sealed class TenantProvisioning(
    HostDb hostCatalog,
    ISecretProtector protector,
    IClock clock,
    IConfiguration configuration)
    : ITenantProvisioning
{
    private readonly HostDb _hostCatalog = hostCatalog;
    private readonly ISecretProtector _protector = protector;
    private readonly IClock _clock = clock;
    private readonly IConfiguration _configuration = configuration;

    public async Task<Result<ProvisionedTenant>> CreateAsync(
        NewTenant tenant,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        // Trimmed but NOT lowercased before validating. Silently turning "UPPER"
        // into "upper" would store something the operator did not type — and
        // TenantResolver compares the slug raw against a cache keyed by the raw
        // string, so a normalised-on-write, not-normalised-on-read pair could
        // behave differently on a cache hit than on a miss. Refusing keeps every
        // stored slug lowercase and side-steps that entirely.
        var slug = (tenant.Slug ?? string.Empty).Trim();

        if (!IsUsableSlug(slug))
        {
            return Result.Failure<ProvisionedTenant>(ProvisioningErrors.BadSlug);
        }

        if (await _hostCatalog.Tenants.AnyAsync(t => t.Slug == slug, cancellationToken))
        {
            return Result.Failure<ProvisionedTenant>(ProvisioningErrors.SlugTaken);
        }

        // The same key Program.cs starts from. Reading a different one produced a
        // provisioning path that worked nowhere.
        var hostConnection = _configuration.GetConnectionString("HostCatalog");
        if (string.IsNullOrWhiteSpace(hostConnection))
        {
            return Result.Failure<ProvisionedTenant>(ProvisioningErrors.NoHostConnection);
        }

        // The tenant database is named from the host catalog's own name, so an
        // installation called something other than DealerFOSS_Host keeps its
        // databases together instead of scattering them under a fixed prefix.
        var tenantConnection = new SqlConnectionStringBuilder(hostConnection)
        {
            InitialCatalog = DevelopmentSeeder.TenantDatabaseName(hostConnection, slug),
        }.ConnectionString;

        try
        {
            var openedFrom = await BuildTenantDatabaseAsync(tenant, tenantConnection, cancellationToken);

            // The first manager, with no password and a one-time code. Same path a
            // starter uses — see the note at the top of this file.
            var code = await IdentitySeeder.ProvisionFirstManagerAsync(
                tenantConnection, _clock, tenant.ManagerEmail, tenant.ManagerName);

            // The catalog row goes in LAST. Until it exists the dealership is
            // unreachable, so a run that fails half way leaves an orphaned
            // database rather than a tenant that resolves to a broken one — the
            // failure that is merely untidy instead of the one that is dangerous.
            _hostCatalog.Tenants.Add(new TenantRecord
            {
                Id = Guid.NewGuid(),
                Slug = slug,
                Name = tenant.Name.Trim(),
                ProtectedConnectionString = _protector.Protect(tenantConnection),
                Status = TenantStatus.Active,
                CreatedAt = _clock.UtcNow,
            });

            await _hostCatalog.SaveChangesAsync(cancellationToken);

            return Result.Success(new ProvisionedTenant(
                slug, tenant.Name.Trim(), tenant.ManagerEmail.Trim().ToLowerInvariant(), code, openedFrom));
        }
        catch (SqlException ex)
        {
            return Result.Failure<ProvisionedTenant>(Error.Conflict(
                "provisioning.database_failed",
                $"The dealership's database could not be created: {ex.Message}"));
        }
    }

    /// <summary>
    /// Creates and migrates the tenant database, then puts in the minimum a
    /// dealership needs to be usable on day one.
    /// </summary>
    private async Task<DateTimeOffset> BuildTenantDatabaseAsync(
        NewTenant tenant,
        string tenantConnection,
        CancellationToken cancellationToken)
    {
        var options = new DbContextOptionsBuilder<TenantDb>()
            .UseSqlServer(tenantConnection)
            .Options;

        await using var db = new TenantDb(options, _clock);
        await db.Database.MigrateAsync(cancellationToken);

        if (!await db.Organizations.AnyAsync(cancellationToken))
        {
            var organization = new DealerOrganization(
                DealerOrganizationId.New(), tenant.Name.Trim(), tenant.Slug.Trim().ToLowerInvariant());

            var entity = new LegalEntity(
                LegalEntityId.New(),
                organization.Id,
                string.IsNullOrWhiteSpace(tenant.LegalEntityName)
                    ? tenant.Name.Trim()
                    : tenant.LegalEntityName.Trim());

            entity.Rooftops.Add(new Rooftop(
                RooftopId.New(),
                entity.Id,
                tenant.RooftopName.Trim(),
                tenant.RooftopCode.Trim().ToUpperInvariant(),
                string.IsNullOrWhiteSpace(tenant.TimeZone) ? "UTC" : tenant.TimeZone.Trim()));

            organization.LegalEntities.Add(entity);

            db.Organizations.Add(organization);
            await db.SaveChangesAsync(cancellationToken);
        }

        await SeedChartOfAccountsAsync(db, cancellationToken);

        return await OpenTheBooksAsync(db, cancellationToken);
    }

    /// <summary>
    /// The accounts a sale and a service invoice need. Without these the first
    /// delivery fails on a missing account, which reads as a bug rather than as
    /// setup nobody did.
    /// </summary>
    private static async Task SeedChartOfAccountsAsync(TenantDb db, CancellationToken cancellationToken)
    {
        (string Code, string Name, AccountKind Kind)[] chart =
        [
            (AccountCodes.Cash, "Cash", AccountKind.Asset),
            (AccountCodes.VehicleInventory, "Vehicle inventory", AccountKind.Asset),
            (AccountCodes.TradeInventory, "Trade-in inventory", AccountKind.Asset),
            (AccountCodes.PartsInventory, "Parts inventory", AccountKind.Asset),
            (AccountCodes.VehicleSalesRevenue, "Vehicle sales", AccountKind.Revenue),
            (AccountCodes.FeeRevenue, "Fee income", AccountKind.Revenue),
            (AccountCodes.LabourRevenue, "Labour sales", AccountKind.Revenue),
            (AccountCodes.PartsRevenue, "Parts sales", AccountKind.Revenue),
            (AccountCodes.SubletRevenue, "Sublet sales", AccountKind.Revenue),
            (AccountCodes.FinanceProductRevenue, "Finance product sales", AccountKind.Revenue),
            (AccountCodes.SalesDiscounts, "Sales discounts", AccountKind.Revenue),
            (AccountCodes.CostOfVehicleSales, "Cost of vehicle sales", AccountKind.Expense),
            (AccountCodes.CostOfPartsSales, "Cost of parts sales", AccountKind.Expense),
            (AccountCodes.CostOfFinanceProducts, "Cost of finance products", AccountKind.Expense),
        ];

        var existing = await db.Accounts.Select(a => a.Code).ToListAsync(cancellationToken);

        foreach (var account in chart.Where(a => !existing.Contains(a.Code)))
        {
            db.Accounts.Add(new Account(Guid.NewGuid(), account.Code, account.Name, account.Kind));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Opens the current month. THE step most likely to be forgotten, and the one
    /// whose absence produces a dealership that cannot record its first sale.
    /// </summary>
    private async Task<DateTimeOffset> OpenTheBooksAsync(TenantDb db, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        var alreadyOpen = await db.AccountingPeriods
            .AnyAsync(p => p.Year == now.Year && p.Month == now.Month, cancellationToken);

        if (!alreadyOpen)
        {
            db.AccountingPeriods.Add(AccountingPeriod.Open(
                Guid.NewGuid(), now.Year, now.Month, now, null,
                "Opened when the dealership was set up."));

            await db.SaveChangesAsync(cancellationToken);
        }

        return now;
    }

    /// <summary>
    /// The slug becomes part of a database name and arrives in a header on every
    /// request, so it is kept to the characters that are safe in both.
    /// </summary>
    private static bool IsUsableSlug(string slug) =>
        slug.Length is >= 2 and <= 40
        && slug.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-');
}

internal static class ProvisioningErrors
{
    public static Error BadSlug { get; } = Error.Validation(
        "provisioning.bad_slug",
        "A short name uses 2 to 40 lowercase letters, digits, or hyphens. It becomes part of a "
            + "database name and travels in a header on every request.");

    public static Error SlugTaken { get; } = Error.Conflict(
        "provisioning.slug_taken",
        "A dealership already uses that short name.");

    public static Error NoHostConnection { get; } = Error.Validation(
        "provisioning.no_host_connection",
        "This installation has no host connection string configured, so it cannot create a database.");
}
