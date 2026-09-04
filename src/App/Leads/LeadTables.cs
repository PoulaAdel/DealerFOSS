// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   LeadTables — how enquiries are stored. Owns the "leads" schema and no other
//   (ADR-014).
//
// Usage:
//   Nothing calls these directly. TenantDb finds them by scanning the
//   assembly.
//
// Coding Instructions:
//   There is no foreign key to Customers or Vehicles on purpose. Those are
//   other capabilities' tables, and a database-level constraint across that
//   line would couple their migrations to this one. The reference is checked
//   through ICustomers and IVehicles when the lead is captured, which is
//   where a readable error can be given (ADR-017).
//
//   Lead history has no audit columns: it IS the history, and TenantDb
//   refuses to update or delete a row of it.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DealerFOSS.Core;
using DealerFOSS.Data;

namespace DealerFOSS.Leads;

/// <summary>The schema this capability owns.</summary>
internal static class LeadSchema
{
    public const string Name = "leads";
}

internal sealed class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("Leads", LeadSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.RooftopId)
            .HasConversion(id => id.Value, value => new RooftopId(value))
            .IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Enquiry).HasMaxLength(1000);

        builder.Ignore(x => x.IsOpen);

        // The work list: "what is open at my lot, newest first".
        builder.HasIndex(x => new { x.RooftopId, x.Status, x.CapturedAt });

        // "What is this salesperson sitting on?" and "has this customer already
        // been contacted?" are both everyday questions.
        builder.HasIndex(x => x.AssignedToUserId);
        builder.HasIndex(x => x.CustomerId);

        builder.HasMany(x => x.History)
            .WithOne()
            .HasForeignKey(h => h.LeadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ConfigureAudit();
    }
}

internal sealed class LeadStatusChangeConfiguration : IEntityTypeConfiguration<LeadStatusChange>
{
    public void Configure(EntityTypeBuilder<LeadStatusChange> builder)
    {
        builder.ToTable("LeadHistory", LeadSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.Sequence).ValueGeneratedOnAdd().UseIdentityColumn();
        builder.HasIndex(x => new { x.LeadId, x.OccurredAt, x.Sequence });
    }
}
