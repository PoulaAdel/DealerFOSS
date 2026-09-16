// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ServiceCatalogueTables — how the op-code catalogue and the labour rates are
//   stored.
//
// Usage:
//   Nothing calls these directly. TenantDb finds them by scanning the assembly.
//
// Coding Instructions:
//   TWO UNIQUE INDEXES, AND BOTH ARE THE POINT.
//
//   Code is unique across the whole organization, with no rooftop in it,
//   because an op code IS the group's shared vocabulary. Two rows reading
//   BRK-FRT would let one lot quietly mean something else by it, and every
//   op-code comparison between lots would then be nonsense.
//
//   (RooftopId, AppliesTo) is unique, so one lot has exactly one warranty rate.
//   Two active rates for the same payer at the same lot is not a choice between
//   them — it is a question with no answer, resolved by whichever row the query
//   happened to return first.
//
//   Both carry IsActive rather than being deletable. A job written in March
//   cites its op code and froze its rate; tidying the catalogue in September
//   must not change what that job says it was.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DealerFOSS.Core;
using DealerFOSS.Data;

namespace DealerFOSS.RepairOrders;

internal sealed class OpCodeConfiguration : IEntityTypeConfiguration<OpCode>
{
    public void Configure(EntityTypeBuilder<OpCode> builder)
    {
        builder.ToTable("OpCodes", ServiceSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(200).IsRequired();
        builder.Property(x => x.StandardHours).HasPrecision(9, 2);

        builder.Property(x => x.DefaultPayType)
            .HasConversion<string>().HasMaxLength(20).IsRequired();

        // No rooftop in this index, deliberately. See this file's header.
        builder.HasIndex(x => x.Code).IsUnique();

        builder.ConfigureAudit();
    }
}

internal sealed class LabourRateConfiguration : IEntityTypeConfiguration<LabourRate>
{
    public void Configure(EntityTypeBuilder<LabourRate> builder)
    {
        builder.ToTable("LabourRates", ServiceSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.RooftopId)
            .HasConversion(id => id.Value, value => new RooftopId(value))
            .IsRequired();

        builder.Property(x => x.Name).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.AmountPerHour).HasPrecision(18, 2);

        builder.Property(x => x.AppliesTo)
            .HasConversion<string>().HasMaxLength(20).IsRequired();

        // One rate per payer per lot. See this file's header.
        builder.HasIndex(x => new { x.RooftopId, x.AppliesTo }).IsUnique();

        builder.ConfigureAudit();
    }
}

/// <summary>
/// Time on a job. Kept in the service schema beside the work it measures.
/// </summary>
/// <remarks>
/// THE PARTIAL UNIQUE INDEX IS THE INVARIANT. One technician may have at most
/// one clocking open at a time, and saying so in the database means it stays
/// true even if a future caller forgets — two open clockings would double-count
/// every hour that technician worked and make efficiency look half what it is.
///
/// Filtered on StoppedAt IS NULL, because the same technician has thousands of
/// CLOSED entries and those must not collide with anything.
/// </remarks>
internal sealed class TechnicianClockingConfiguration : IEntityTypeConfiguration<TechnicianClocking>
{
    public void Configure(EntityTypeBuilder<TechnicianClocking> builder)
    {
        builder.ToTable("TechnicianClockings", ServiceSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.RooftopId)
            .HasConversion(id => id.Value, value => new RooftopId(value))
            .IsRequired();

        builder.Property(x => x.StoppedBecause).HasMaxLength(200);

        builder.HasIndex(x => x.TechnicianUserId)
            .IsUnique()
            .HasFilter("[StoppedAt] IS NULL");

        // What the labour report reads: everything closed in a period.
        builder.HasIndex(x => new { x.RooftopId, x.StoppedAt });
        builder.HasIndex(x => x.RepairOrderId);

        builder.Ignore(x => x.IsOpen);
        builder.Ignore(x => x.Hours);

        builder.ConfigureAudit();
    }
}
