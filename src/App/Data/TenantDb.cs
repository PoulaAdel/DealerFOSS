// TenantDb — one dealer organization's business database: organization
// structure, customers, vehicles, and stock.
//
// Use:  injected into feature services. It binds to the tenant resolved for the
//       current request and never chooses a connection itself, so a request
//       cannot address a second dealer's data (doc 04 §5).
// Edit: do NOT add entity configuration here. Each feature owns its own mapping
//       in a XTables.cs file and this context picks them up by scanning the
//       assembly, which is what keeps the features separable inside one project.
//       Two behaviours below are load-bearing and must not be relaxed: audit
//       columns are stamped centrally on save, and inventory status history is
//       append-only.

using Microsoft.EntityFrameworkCore;
using OpenDealer360.Accounting;
using OpenDealer360.Core;
using OpenDealer360.Customers;
using OpenDealer360.DataMigration;
using OpenDealer360.Deals;
using OpenDealer360.Inventory;
using OpenDealer360.Leads;
using OpenDealer360.Organization;
using OpenDealer360.Vehicles;

namespace OpenDealer360.Data;

/// <summary>
/// The tenant business database. Identity keeps its own context in its own
/// project (the <c>identity</c> schema lives in the same database but is
/// physically unreachable from here); routing lives in the host catalog.
/// </summary>
public sealed class TenantDb(DbContextOptions<TenantDb> options, IClock clock) : DbContext(options)
{
    private readonly IClock _clock = clock;

    public DbSet<DealerOrganization> Organizations => Set<DealerOrganization>();

    public DbSet<LegalEntity> LegalEntities => Set<LegalEntity>();

    public DbSet<Rooftop> Rooftops => Set<Rooftop>();

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<ContactPoint> ContactPoints => Set<ContactPoint>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<InventoryUnit> InventoryUnits => Set<InventoryUnit>();

    public DbSet<InventoryStatusChange> InventoryStatusHistory => Set<InventoryStatusChange>();

    public DbSet<Lead> Leads => Set<Lead>();

    public DbSet<LeadStatusChange> LeadHistory => Set<LeadStatusChange>();

    public DbSet<Deal> Deals => Set<Deal>();

    public DbSet<DealCharge> DealCharges => Set<DealCharge>();

    public DbSet<DealStatusChange> DealHistory => Set<DealStatusChange>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();

    public DbSet<JournalLine> JournalLines => Set<JournalLine>();

    internal DbSet<ImportJob> ImportJobs => Set<ImportJob>();

    internal DbSet<ImportRow> ImportRows => Set<ImportRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Each feature contributes its own IEntityTypeConfiguration, including
        // its schema name. Nothing here knows a table name.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenantDb).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        GuardAppendOnlyHistory();

        var now = _clock.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.ConcurrencyStamp = Guid.NewGuid();
                    break;
                case EntityState.Modified:
                    entry.Entity.ModifiedAt = now;
                    entry.Entity.ConcurrencyStamp = Guid.NewGuid();
                    break;
                default:
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// History is the record of what happened. Rewriting it would make aging,
    /// cost, and reconciliation unauditable, so a correction is an opposite entry
    /// with a reason (doc 04 §4, ADR-016).
    /// </summary>
    /// <remarks>
    /// Keyed off <see cref="IAppendOnly"/> rather than a list of types, so a
    /// history table added later is protected by implementing the marker instead
    /// of by somebody remembering to extend this method.
    /// </remarks>
    private void GuardAppendOnlyHistory()
    {
        foreach (var entry in ChangeTracker.Entries<IAppendOnly>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    $"{entry.Entity.GetType().Name} is append-only. Record the opposite entry with a "
                    + $"reason instead of attempting to {entry.State.ToString().ToLowerInvariant()} one.");
            }
        }
    }
}
