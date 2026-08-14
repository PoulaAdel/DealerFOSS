// DealTables — how deals are stored. Owns the "deals" schema and no other
// (ADR-014).
//
// Use:  nothing calls these directly. TenantDb finds them by scanning the
//       assembly.
// Edit: no foreign key to Customers or Inventory, for the same reason as Leads —
//       those are other capabilities' tables and a database constraint across
//       that line couples their migrations to this one. The references are
//       checked through ICustomers and IInventory where a readable error can be
//       given.
//
//       Money columns are decimal(18,2) and every amount on a deal shares the
//       deal's currency, so a total can never mix two.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DealerFOSS.Core;
using DealerFOSS.Data;

namespace DealerFOSS.Deals;

/// <summary>The schema this capability owns.</summary>
internal static class DealSchema
{
    public const string Name = "deals";
}

internal sealed class DealConfiguration : IEntityTypeConfiguration<Deal>
{
    public void Configure(EntityTypeBuilder<Deal> builder)
    {
        builder.ToTable("Deals", DealSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.RooftopId)
            .HasConversion(id => id.Value, value => new RooftopId(value))
            .IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();

        // Computed from the charges and the trade; not columns of their own.
        builder.Ignore(x => x.Subtotal);
        builder.Ignore(x => x.AmountDue);
        builder.Ignore(x => x.TermsAreOpen);

        // The desk list: "what is open at my lot, and what is waiting for me".
        builder.HasIndex(x => new { x.RooftopId, x.Status });
        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => x.SalespersonUserId);

        // One live deal per car is enforced by the inventory hold rather than by
        // an index — a cancelled deal legitimately leaves a second one behind.
        builder.HasIndex(x => x.InventoryUnitId);

        builder.OwnsOne(x => x.Trade, trade =>
        {
            trade.Property(t => t.Description).HasColumnName("TradeDescription").HasMaxLength(200);
            trade.Property(t => t.Allowance).HasColumnName("TradeAllowance").HasPrecision(18, 2);
            trade.Property(t => t.Payoff).HasColumnName("TradePayoff").HasPrecision(18, 2);
            trade.Ignore(t => t.Equity);
            trade.Ignore(t => t.IsNegativeEquity);
        });

        builder.HasMany(x => x.Charges)
            .WithOne()
            .HasForeignKey(c => c.DealId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Charges).AutoInclude();

        // Auto-included for the same reason as charges: every read of a deal
        // needs its total, and the total now includes what was sold with the car.
        builder.HasMany(x => x.Products)
            .WithOne()
            .HasForeignKey(p => p.DealId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Products).AutoInclude();

        builder.HasMany(x => x.History)
            .WithOne()
            .HasForeignKey(h => h.DealId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ConfigureAudit();
    }
}

internal sealed class DealChargeConfiguration : IEntityTypeConfiguration<DealCharge>
{
    public void Configure(EntityTypeBuilder<DealCharge> builder)
    {
        builder.ToTable("DealCharges", DealSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Description).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.HasIndex(x => x.DealId);
    }
}

internal sealed class DealProductConfiguration : IEntityTypeConfiguration<DealProduct>
{
    public void Configure(EntityTypeBuilder<DealProduct> builder)
    {
        builder.ToTable("DealProducts", DealSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        // Copied at the point of sale, so a renamed or withdrawn catalogue entry
        // cannot change what this deal says was sold.
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.Property(x => x.Cost).HasPrecision(18, 2);

        // Price minus cost, computed.
        builder.Ignore(x => x.Gross);

        builder.HasIndex(x => x.DealId);

        // No foreign key to FinanceProduct on purpose. The catalogue belongs to
        // another capability, and a database-level link would let a delete there
        // cascade into a sold deal — which must never happen. Withdrawal is how a
        // product goes away, and the copied name is what keeps this row readable
        // regardless.
    }
}

internal sealed class DealStatusChangeConfiguration : IEntityTypeConfiguration<DealStatusChange>
{
    public void Configure(EntityTypeBuilder<DealStatusChange> builder)
    {
        builder.ToTable("DealHistory", DealSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.AmountAtChange).HasPrecision(18, 2);
        builder.Property(x => x.Sequence).ValueGeneratedOnAdd().UseIdentityColumn();
        builder.HasIndex(x => new { x.DealId, x.OccurredAt, x.Sequence });
    }
}
