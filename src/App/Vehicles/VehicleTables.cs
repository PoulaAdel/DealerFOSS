// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   VehicleTables — how vehicle records are stored. Shares the "vehicles" schema
//   with Inventory (ADR-014).
//
// Usage:
//   Nothing calls this directly. TenantDb finds it by scanning the assembly.
//
// Coding Instructions:
//   There is deliberately no unique index on VIN. The same physical vehicle
//   legitimately reappears — sold, then taken back as a trade-in years later
//   — and imported data contains mistyped numbers. A unique index would
//   either block a real car or force staff to invent a fake VIN to get past
//   it. Duplicates are resolved as a workflow, not by a constraint
//   (doc 04 §4).

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DealerFOSS.Data;

namespace DealerFOSS.Vehicles;

/// <summary>The schema this feature owns, shared with Inventory.</summary>
internal static class VehicleSchema
{
    public const string Name = "vehicles";
}

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles", VehicleSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Vin).HasMaxLength(32).IsRequired();
        builder.Property(x => x.VinExceptionReason).HasMaxLength(300);
        builder.Property(x => x.Make).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Model).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Trim).HasMaxLength(60);
        builder.Property(x => x.BodyStyle).HasMaxLength(60);
        builder.Property(x => x.ExteriorColor).HasMaxLength(40);

        // Computed from the columns above; not columns of their own.
        builder.Ignore(x => x.DisplayName);
        builder.Ignore(x => x.HasVinException);

        // Looking a vehicle up by VIN is the single most common query in a
        // dealership. Indexed, but not unique — see the note above.
        builder.HasIndex(x => x.Vin);
        builder.HasIndex(x => new { x.Make, x.Model, x.ModelYear });

        builder.ConfigureAudit();
    }
}
