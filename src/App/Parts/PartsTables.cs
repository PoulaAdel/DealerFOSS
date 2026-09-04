// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   PartsTables — how parts and their stock are stored.
//
// Usage:
//   Nothing calls these directly. TenantDb finds them by scanning the
//   assembly.
//
// Coding Instructions:
//   The part-number index is unique across the ORGANIZATION, not per rooftop
//   — the opposite of a stock number. A stock number is a sticker one lot put
//   on one car; a part number is the manufacturer's name for a component, and
//   two catalogue rows for it is the failure that makes a parts department
//   stop trusting the figures.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DealerFOSS.Core;
using DealerFOSS.Data;

namespace DealerFOSS.Parts;

/// <summary>Parts have their own schema, like every other capability (ADR-014).</summary>
public static class PartSchema
{
    public const string Name = "parts";
}

internal sealed class PartConfiguration : IEntityTypeConfiguration<Part>
{
    public void Configure(EntityTypeBuilder<Part> builder)
    {
        builder.ToTable("Parts", PartSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.PartNumber).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(300).IsRequired();

        builder.HasIndex(x => x.PartNumber).IsUnique();

        builder.ConfigureAudit();
    }
}

internal sealed class StockReceiptConfiguration : IEntityTypeConfiguration<StockReceipt>
{
    public void Configure(EntityTypeBuilder<StockReceipt> builder)
    {
        builder.ToTable("StockReceipts", PartSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.RooftopId)
            .HasConversion(id => id.Value, value => new RooftopId(value))
            .IsRequired();

        // Quantities carry three decimals: fluids and consumables are issued in
        // fractions of a litre, and rounding those to whole units at the point of
        // sale would quietly lose stock.
        builder.Property(x => x.QuantityReceived).HasPrecision(18, 3);
        builder.Property(x => x.RemainingQuantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitCostAmount).HasPrecision(18, 4);
        builder.Property(x => x.UnitCostCurrency).HasMaxLength(3);
        builder.Property(x => x.Reference).HasMaxLength(100);

        builder.Ignore(x => x.UnitCost);

        // Every issue asks "what is on this shelf for this part, oldest first".
        builder.HasIndex(x => new { x.PartId, x.RooftopId, x.ReceivedAt });

        builder.HasOne<Part>()
            .WithMany()
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ConfigureAudit();
    }
}

internal sealed class PartsSettingsConfiguration : IEntityTypeConfiguration<PartsSettings>
{
    public void Configure(EntityTypeBuilder<PartsSettings> builder)
    {
        builder.ToTable("PartsSettings", PartSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CostingMethod).HasConversion<string>().HasMaxLength(20);

        builder.ConfigureAudit();
    }
}
