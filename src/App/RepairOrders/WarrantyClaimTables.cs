// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   WarrantyClaimTables — how a warranty claim and its history are stored. In
//   the "service" schema, same as the rest of RepairOrders (ADR-014).
//
// Usage:
//   Nothing calls these directly. TenantDb finds them by scanning the
//   assembly.
//
// Coding Instructions:
//   ONE CLAIM PER ORDER, and the unique index says so rather than trusting
//   every caller to check first — the same discipline Receivables' (Source,
//   Reference) index applies to a bill.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DealerFOSS.Core;
using DealerFOSS.Data;

namespace DealerFOSS.RepairOrders;

internal sealed class WarrantyClaimConfiguration : IEntityTypeConfiguration<WarrantyClaim>
{
    public void Configure(EntityTypeBuilder<WarrantyClaim> builder)
    {
        builder.ToTable("WarrantyClaims", ServiceSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.RooftopId)
            .HasConversion(id => id.Value, value => new RooftopId(value))
            .IsRequired();

        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.AmountPaid).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        // One claim per order — invoicing opens it once, and nothing here
        // re-invoices a job that already has one.
        builder.HasIndex(x => x.RepairOrderId).IsUnique();

        builder.HasMany(x => x.History)
            .WithOne()
            .HasForeignKey(h => h.WarrantyClaimId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ConfigureAudit();
    }
}

internal sealed class WarrantyClaimStatusChangeConfiguration : IEntityTypeConfiguration<WarrantyClaimStatusChange>
{
    public void Configure(EntityTypeBuilder<WarrantyClaimStatusChange> builder)
    {
        builder.ToTable("WarrantyClaimHistory", ServiceSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Note).HasMaxLength(1000);

        builder.Property(x => x.Sequence).ValueGeneratedOnAdd().UseIdentityColumn();
        builder.HasIndex(x => new { x.WarrantyClaimId, x.OccurredAt, x.Sequence });
    }
}
