// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   DealTables — how deals are stored. Owns the "deals" schema and no other
//   (ADR-014).
//
// Usage:
//   Nothing calls these directly. TenantDb finds them by scanning the
//   assembly.
//
// Coding Instructions:
//   No foreign key to Customers or Inventory, for the same reason as Leads —
//   those are other capabilities' tables and a database constraint across
//   that line couples their migrations to this one. The references are
//   checked through ICustomers and IInventory where a readable error can be
//   given.
//
//   Money columns are decimal(18,2) and every amount on a deal shares the
//   deal's currency, so a total can never mix two.

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
        builder.Ignore(x => x.TaxTotal);

        // The amount financed is AmountDue less the cash down, and the payments
        // are worked out from it every time they are read. Neither is a column,
        // and neither should become one — see Financing.cs.
        builder.Ignore(x => x.AmountFinanced);
        builder.Ignore(x => x.Instalments);

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

        // The address the tax was resolved from, stored on the deal as a snapshot
        // rather than a reference. County is its own column beside the state
        // because a US rate depends on both.
        builder.OwnsOne(x => x.TaxedAt, at =>
        {
            at.Property(a => a.AdministrativeArea).HasColumnName("TaxedAtArea").HasMaxLength(120);
            at.Property(a => a.County).HasColumnName("TaxedAtCounty").HasMaxLength(120);
            at.Property(a => a.PostalCode).HasColumnName("TaxedAtPostalCode").HasMaxLength(20);
            at.Property(a => a.Country).HasColumnName("TaxedAtCountry").HasMaxLength(2);
        });

        // Where the car will be registered or garaged — the full postal shape,
        // unlike TaxedAt above, because this one ends up on registration
        // paperwork rather than only deciding a rate.
        builder.OwnsOne(x => x.RegistrationAddress, address =>
        {
            address.Property(a => a.Line1).HasColumnName("RegistrationAddressLine1").HasMaxLength(200);
            address.Property(a => a.Line2).HasColumnName("RegistrationAddressLine2").HasMaxLength(200);
            address.Property(a => a.City).HasColumnName("RegistrationCity").HasMaxLength(120);
            address.Property(a => a.AdministrativeArea).HasColumnName("RegistrationArea").HasMaxLength(120);
            address.Property(a => a.County).HasColumnName("RegistrationCounty").HasMaxLength(120);
            address.Property(a => a.PostalCode).HasColumnName("RegistrationPostalCode").HasMaxLength(20);
            address.Property(a => a.Country).HasColumnName("RegistrationCountry").HasMaxLength(2);
        });

        // Four columns and no fifth. The amount financed, the monthly payment and
        // the finance charge all follow from these and from AmountDue, so a
        // column for any of them would be a second answer to a question the deal
        // can already answer.
        builder.OwnsOne(x => x.Financing, financing =>
        {
            financing.Property(f => f.Lender).HasColumnName("FinanceLender").HasMaxLength(120);
            financing.Property(f => f.DownPayment).HasColumnName("FinanceDownPayment").HasPrecision(18, 2);

            // Six decimal places, not two, for the same reason as a tax rate: a
            // rate is not money. 0.0649 is 6.49%, and money precision would
            // round every rate to a hundredth of a percent.
            financing.Property(f => f.AnnualPercentageRate)
                .HasColumnName("FinanceAnnualRate").HasPrecision(9, 6);
            financing.Property(f => f.TermMonths).HasColumnName("FinanceTermMonths");
        });

        // Auto-included for the same reason as charges and products: every read
        // of a deal needs its total, and the total includes tax.
        builder.HasMany(x => x.TaxLines)
            .WithOne()
            .HasForeignKey(t => t.DealId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.TaxLines).AutoInclude();

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
        builder.Property(x => x.RefundAmount).HasPrecision(18, 2);
        builder.Property(x => x.CancellationReason).HasMaxLength(500);

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

internal sealed class DealTaxLineConfiguration : IEntityTypeConfiguration<DealTaxLine>
{
    public void Configure(EntityTypeBuilder<DealTaxLine> builder)
    {
        builder.ToTable("DealTaxLines", DealSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Description).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Jurisdiction).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Basis).HasPrecision(18, 2);

        // Six decimal places, not two. A rate is not money: 0.0625 is a real
        // rate and several US local rates run to four and five places once a
        // district tax is added. Storing it at money precision would round
        // 8.6375% to 8.64% and put the rounding somewhere nobody can see it.
        builder.Property(x => x.Rate).HasPrecision(9, 6);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Provenance).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.PackId).HasMaxLength(60);
        builder.HasIndex(x => x.DealId);
    }
}
