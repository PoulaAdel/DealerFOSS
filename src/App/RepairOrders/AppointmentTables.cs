// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   AppointmentTables — how the service diary is stored. Part of the "service"
//   schema the workshop owns (ADR-014).
//
// Usage:
//   Nothing calls this directly. TenantDb finds it by scanning the assembly.
//
// Coding Instructions:
//   No foreign key to RepairOrders, even though both tables live in this
//   schema and the column points at one. The link is set inside a transaction
//   that writes both rows, so the constraint would buy nothing a test does not
//   already prove — and it would make deleting a job impossible without first
//   finding every booking that mentions it, which is the wrong way round: the
//   diary's record of what happened should survive.
//
//   No foreign key to Customers or Vehicles either, for the same reason as
//   RepairOrders: those are other capabilities' tables.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DealerFOSS.Core;
using DealerFOSS.Data;

namespace DealerFOSS.RepairOrders;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments", ServiceSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.RooftopId)
            .HasConversion(id => id.Value, value => new RooftopId(value))
            .IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Outcome).HasMaxLength(1000);

        // Two decimals, matching the hours on a service line. A workshop that
        // books in six-minute units is recording 0.1, not 0.100.
        builder.Property(x => x.EstimatedHours).HasPrecision(9, 2);

        builder.Ignore(x => x.IsOpen);

        // The diary itself: "what is this workshop expecting, in date order".
        builder.HasIndex(x => new { x.RooftopId, x.ScheduledFor });

        // "What is still coming" — the working view, and the one the load figure
        // is computed from.
        builder.HasIndex(x => new { x.RooftopId, x.Status, x.ScheduledFor });

        // A car's booking history, and a customer's. Both are asked for by name
        // at a service counter.
        builder.HasIndex(x => x.VehicleId);
        builder.HasIndex(x => x.CustomerId);

        // Reconciling the diary against the workshop: "which booking produced
        // this job". Filtered, because most rows have not arrived yet and an
        // index full of nulls is dead weight.
        builder.HasIndex(x => x.RepairOrderId).HasFilter("[RepairOrderId] IS NOT NULL");

        builder.ConfigureAudit();
    }
}
