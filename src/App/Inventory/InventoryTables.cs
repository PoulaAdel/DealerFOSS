// InventoryTables — how stock records are stored. Shares the "vehicles" schema
// with the Vehicles feature (ADR-014).
//
// Use:  nothing calls these directly. TenantDb finds them by scanning the
//       assembly.
// Edit: the stock-number index IS unique, but only within a rooftop: two
//       locations may each have a unit "A1234", one location may not have it
//       twice. Status history has no audit columns on purpose — it IS the
//       history, and TenantDb refuses to update or delete a row of it.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DealerFOSS.Core;
using DealerFOSS.Data;
using DealerFOSS.Vehicles;

namespace DealerFOSS.Inventory;

internal sealed class InventoryUnitConfiguration : IEntityTypeConfiguration<InventoryUnit>
{
    public void Configure(EntityTypeBuilder<InventoryUnit> builder)
    {
        builder.ToTable("InventoryUnits", VehicleSchema.Name);
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

        builder.ConfigureAudit();
    }
}

internal sealed class InventoryStatusChangeConfiguration : IEntityTypeConfiguration<InventoryStatusChange>
{
    public void Configure(EntityTypeBuilder<InventoryStatusChange> builder)
    {
        builder.ToTable("InventoryStatusHistory", VehicleSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasIndex(x => new { x.InventoryUnitId, x.OccurredAt });
    }
}
