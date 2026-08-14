// RepairOrderTables — how service work is stored. Owns the "service" schema and
// no other (ADR-014).
//
// Use:  nothing calls these directly. TenantDb finds them by scanning the
//       assembly.
// Edit: the schema is named for the department rather than for the folder, which
//       is the one deliberate break from the pattern the other capabilities
//       follow. A DBA reading `service.RepairOrders` learns something;
//       `repairorders.RepairOrders` does not.
//
//       No foreign key to Customers or Vehicles, for the same reason as Deals —
//       those are other capabilities' tables and a database constraint across
//       that line couples their migrations to this one. The references are
//       checked through ICustomers and IVehicles where a readable error can be
//       given.
//
//       Money columns are decimal(18,2); hours are decimal(9,2), which is enough
//       for a job nobody would book as one line.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DealerFOSS.Core;
using DealerFOSS.Data;

namespace DealerFOSS.RepairOrders;

/// <summary>The schema this capability owns.</summary>
internal static class ServiceSchema
{
    public const string Name = "service";
}

internal sealed class RepairOrderConfiguration : IEntityTypeConfiguration<RepairOrder>
{
    public void Configure(EntityTypeBuilder<RepairOrder> builder)
    {
        builder.ToTable("RepairOrders", ServiceSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.RooftopId)
            .HasConversion(id => id.Value, value => new RooftopId(value))
            .IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Number).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Complaint).HasMaxLength(2000).IsRequired();

        // Computed from the lines; not columns of their own.
        builder.Ignore(x => x.LabourTotal);
        builder.Ignore(x => x.PartsTotal);
        builder.Ignore(x => x.SubletTotal);
        builder.Ignore(x => x.AmountDue);
        builder.Ignore(x => x.LinesAreOpen);
        builder.Ignore(x => x.AwaitingAnswer);

        // A job number is what everybody says out loud, so it has to be unique
        // where it is said — within the workshop that issued it.
        builder.HasIndex(x => new { x.RooftopId, x.Number }).IsUnique();

        // The workshop's day: "what is open here, and what is at each stage".
        builder.HasIndex(x => new { x.RooftopId, x.Status });
        builder.HasIndex(x => x.CustomerId);

        // A car's service history — the query that makes the capability worth
        // having, and the one a customer asks for by name.
        builder.HasIndex(x => x.VehicleId);
        builder.HasIndex(x => x.TechnicianUserId);

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(l => l.RepairOrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).AutoInclude();

        builder.HasMany(x => x.History)
            .WithOne()
            .HasForeignKey(h => h.RepairOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ConfigureAudit();
    }
}

internal sealed class ServiceLineConfiguration : IEntityTypeConfiguration<ServiceLine>
{
    public void Configure(EntityTypeBuilder<ServiceLine> builder)
    {
        builder.ToTable("ServiceLines", ServiceSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Hours).HasPrecision(9, 2);
        builder.Property(x => x.Rate).HasPrecision(18, 2);
        builder.Property(x => x.UnitAmount).HasPrecision(18, 2);
        builder.Property(x => x.Authorization).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.AuthorizationNote).HasMaxLength(1000);

        // Three decimals on the quantity, matching StockReceipt: fluids and
        // consumables are issued in fractions of a litre, and the default
        // precision would silently truncate them — EF says so at startup, which
        // is how this was caught.
        builder.Property(x => x.PartQuantity).HasPrecision(18, 3);

        // Four on the cost, also matching the receipt it came from. A part
        // costing 0.0125 each is ordinary in a parts department, and rounding it
        // on the way in would make the profit figure wrong by a little, forever.
        builder.Property(x => x.CostAmount).HasPrecision(18, 4);

        builder.HasIndex(x => x.PartId);

        // Derived from hours, rate, and whether the customer said yes.
        builder.Ignore(x => x.Amount);
        builder.Ignore(x => x.DrawsFromStock);

        builder.HasIndex(x => x.RepairOrderId);

        // "What am I still waiting on the customer for" — the advisor's work list,
        // and every row on it is blocking an invoice.
        builder.HasIndex(x => new { x.RepairOrderId, x.Authorization });
    }
}

internal sealed class RepairOrderStatusChangeConfiguration : IEntityTypeConfiguration<RepairOrderStatusChange>
{
    public void Configure(EntityTypeBuilder<RepairOrderStatusChange> builder)
    {
        builder.ToTable("RepairOrderHistory", ServiceSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.AmountAtChange).HasPrecision(18, 2);
        builder.Property(x => x.Sequence).ValueGeneratedOnAdd().UseIdentityColumn();
        builder.HasIndex(x => new { x.RepairOrderId, x.OccurredAt, x.Sequence });
    }
}
