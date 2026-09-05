// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   TenantDb — one dealer organization's business database: organization
//   structure, customers, vehicles, and stock.
//
// Usage:
//   Injected into feature services. It binds to the tenant resolved for the
//   current request and never chooses a connection itself, so a request
//   cannot address a second dealer's data (doc 04 §5).
//
// Coding Instructions:
//   Do NOT add entity configuration here. Each feature owns its own mapping
//   in a XTables.cs file and this context picks them up by scanning the
//   assembly, which is what keeps the features separable inside one project.
//   Two behaviours below are load-bearing and must not be relaxed: audit
//   columns are stamped centrally on save, and inventory status history is
//   append-only.

using Microsoft.EntityFrameworkCore;
using DealerFOSS.Accounting;
using DealerFOSS.Core;
using DealerFOSS.Customers;
using DealerFOSS.DataMigration;
using DealerFOSS.Deals;
using DealerFOSS.Finance;
using DealerFOSS.Integrations;
using DealerFOSS.Inventory;
using DealerFOSS.Leads;
using DealerFOSS.Organization;
using DealerFOSS.Parts;
using DealerFOSS.RepairOrders;
using DealerFOSS.Vehicles;

namespace DealerFOSS.Data;

/// <summary>
/// The tenant business database. Identity keeps its own context in its own
/// project (the <c>identity</c> schema lives in the same database but is
/// physically unreachable from here); routing lives in the host catalog.
/// </summary>
public sealed class TenantDb(DbContextOptions<TenantDb> options, IClock clock, ICurrentUser currentUser)
    : DbContext(options), IUnattendedSafe
{
    /// <summary>
    /// What a row says when no person asked for it. Matches the default on
    /// AuditableEntity, so a row written outside a request reads the same
    /// whether or not it passed through here.
    /// </summary>
    private const string SystemAuthor = "system";

    private readonly IClock _clock = clock;

    /// <summary>
    /// Who is writing. Unauthenticated for the seeder, tenant provisioning and
    /// design-time tooling — all of which genuinely are the system.
    /// </summary>
    private readonly ICurrentUser _currentUser = currentUser;

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

    public DbSet<DealProduct> DealProducts => Set<DealProduct>();

    public DbSet<DealStatusChange> DealHistory => Set<DealStatusChange>();

    public DbSet<FinanceProduct> FinanceProducts => Set<FinanceProduct>();

    public DbSet<RepairOrder> RepairOrders => Set<RepairOrder>();

    public DbSet<ServiceLine> ServiceLines => Set<ServiceLine>();

    public DbSet<RepairOrderStatusChange> RepairOrderHistory => Set<RepairOrderStatusChange>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<Part> Parts => Set<Part>();

    public DbSet<StockReceipt> StockReceipts => Set<StockReceipt>();

    public DbSet<PartsSettings> PartsSettings => Set<PartsSettings>();

    public DbSet<ConnectorCursor> ConnectorCursors => Set<ConnectorCursor>();

    public DbSet<ConnectorRun> ConnectorRuns => Set<ConnectorRun>();

    public DbSet<QuarantinedRecord> QuarantinedRecords => Set<QuarantinedRecord>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<AccountingPeriod> AccountingPeriods => Set<AccountingPeriod>();

    public DbSet<AccountingPeriodChange> AccountingPeriodHistory => Set<AccountingPeriodChange>();

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

        // Who, as well as when. Until 2026-09-04 only the timestamps were
        // stamped: CreatedBy kept the "system" default it was born with, so
        // every row in the database claimed the system wrote it — including
        // deals and repair orders made by people who were signed in at the time.
        //
        // "system" is retained as the fallback rather than throwing, because
        // three callers legitimately have no person behind them: the development
        // seeder, tenant provisioning, and `dotnet ef` at design time. A row
        // nobody asked for should say so.
        var author = _currentUser.IsAuthenticated
            ? _currentUser.Id.ToString()
            : SystemAuthor;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = author;
                    entry.Entity.ConcurrencyStamp = Guid.NewGuid();
                    break;
                case EntityState.Modified:
                    entry.Entity.ModifiedAt = now;
                    entry.Entity.ModifiedBy = author;
                    entry.Entity.ConcurrencyStamp = Guid.NewGuid();

                    // CreatedBy is never restamped. It answers a different
                    // question from ModifiedBy, and it is the more valuable of
                    // the two — overwriting it would lose the only record of who
                    // originated the row.
                    entry.Property(e => e.CreatedBy).IsModified = false;
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
