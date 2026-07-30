// VehiclesDbContext — persistence for this module. Owns the "vehicles" schema
// and no other (ADR-014).
//
// Use:  injected into this module's own services; nothing outside Vehicles may
//       depend on it.
// Edit: there is deliberately no unique index on VIN. The same physical vehicle
//       legitimately reappears — sold, then taken back as a trade-in years later
//       — and imported data contains mistyped numbers. A unique index would
//       either block a real car or force staff to invent a fake VIN to get past
//       it. Duplicates are resolved as a workflow, not by a constraint
//       (doc 04 §4). The stock-number index IS unique, but only within a
//       rooftop: two locations may each have a unit "A1234".

using Microsoft.EntityFrameworkCore;
using OpenDealer360.Core;
using OpenDealer360.Vehicles.Domain;

namespace OpenDealer360.Vehicles.Data;

public sealed class VehiclesDbContext(DbContextOptions<VehiclesDbContext> options, IClock clock)
    : DbContext(options)
{
    public const string Schema = "vehicles";

    private readonly IClock _clock = clock;

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<InventoryUnit> InventoryUnits => Set<InventoryUnit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Vehicle>(builder =>
        {
            builder.ToTable("Vehicles");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Vin).HasMaxLength(32).IsRequired();
            builder.Property(x => x.VinExceptionReason).HasMaxLength(300);
            builder.Property(x => x.Make).HasMaxLength(60).IsRequired();
            builder.Property(x => x.Model).HasMaxLength(80).IsRequired();
            builder.Property(x => x.Trim).HasMaxLength(60);
            builder.Property(x => x.BodyStyle).HasMaxLength(60);
            builder.Property(x => x.ExteriorColor).HasMaxLength(40);

            // Computed from the columns above; not a column of its own.
            builder.Ignore(x => x.DisplayName);
            builder.Ignore(x => x.HasVinException);

            // Looking a vehicle up by VIN is the single most common query in a
            // dealership. Indexed, but not unique — see the note above.
            builder.HasIndex(x => x.Vin);
            builder.HasIndex(x => new { x.Make, x.Model, x.ModelYear });

            ConfigureAudit(builder);
        });

        modelBuilder.Entity<InventoryUnit>(builder =>
        {
            builder.ToTable("InventoryUnits");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.RooftopId)
                .HasConversion(id => id.Value, value => new RooftopId(value))
                .IsRequired();
            builder.Property(x => x.StockNumber).HasMaxLength(40).IsRequired();
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.CostAmount).HasPrecision(18, 2);
            builder.Property(x => x.CostCurrency).HasMaxLength(3);

            // Money is assembled from the two columns above; see InventoryUnit.
            builder.Ignore(x => x.Cost);

            // Two rooftops may each write "A1234" on a windscreen; one rooftop
            // may not write it twice.
            builder.HasIndex(x => new { x.RooftopId, x.StockNumber }).IsUnique();

            // Every inventory screen asks "what is on this lot, in this state".
            builder.HasIndex(x => new { x.RooftopId, x.Status });
            builder.HasIndex(x => x.VehicleId);

            builder.HasOne<Vehicle>()
                .WithMany()
                .HasForeignKey(x => x.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.StatusHistory)
                .WithOne()
                .HasForeignKey(h => h.InventoryUnitId)
                .OnDelete(DeleteBehavior.Cascade);

            ConfigureAudit(builder);
        });

        modelBuilder.Entity<InventoryStatusChange>(builder =>
        {
            builder.ToTable("InventoryStatusHistory");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.Note).HasMaxLength(500);
            builder.HasIndex(x => new { x.InventoryUnitId, x.OccurredAt });
        });
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
    /// Status history is the record of what happened to a car. Rewriting it would
    /// make aging, cost, and deal reconciliation unauditable, so a correction is
    /// an opposite move with a reason (doc 04 §4).
    /// </summary>
    private void GuardAppendOnlyHistory()
    {
        foreach (var entry in ChangeTracker.Entries<InventoryStatusChange>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "Inventory status history is append-only. Record the opposite move with a "
                    + $"reason instead of attempting to {entry.State.ToString().ToLowerInvariant()} an entry.");
            }
        }
    }

    private static void ConfigureAudit<TEntity>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> builder)
        where TEntity : AuditableEntity
    {
        builder.Property(x => x.CreatedBy).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ModifiedBy).HasMaxLength(120);
        builder.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
    }
}
