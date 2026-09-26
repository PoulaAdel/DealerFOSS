// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   IntegrationTables — how the integration edge's own records are stored.
//
// Usage:
//   Nothing calls these directly. TenantDb finds them by scanning the
//   assembly.
//
// Coding Instructions:
//   The unique index on ConnectorCursor is the one to be careful with. Two
//   rows for the same connector, dealership and contract would let two runs
//   each advance their own copy, and the feed would read as up to date while
//   skipping whatever the other row had already passed. The database refuses
//   it rather than the runtime remembering to.
//
//   ConnectorSchedule carries the same index for the same reason, one level up:
//   two schedules for one feed would fire independently and fight over that
//   single cursor.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DealerFOSS.Core;
using DealerFOSS.Data;

namespace DealerFOSS.Integrations;

/// <summary>The integration edge has its own schema, like every capability (ADR-014).</summary>
public static class IntegrationSchema
{
    public const string Name = "integration";
}

internal sealed class ConnectorCursorConfiguration : IEntityTypeConfiguration<ConnectorCursor>
{
    public void Configure(EntityTypeBuilder<ConnectorCursor> builder)
    {
        builder.ToTable("ConnectorCursors", IntegrationSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Connector).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Contract).HasMaxLength(60).IsRequired();
        builder.Property(x => x.HeldBecause).HasMaxLength(80);
        builder.Property(x => x.RooftopId)
            .HasConversion(id => id.Value, value => new RooftopId(value))
            .IsRequired();

        // One cursor per feed. See the header — this index is load-bearing.
        builder.HasIndex(x => new { x.Connector, x.RooftopId, x.Contract, x.Version }).IsUnique();

        builder.ConfigureAudit();
    }
}

internal sealed class ConnectorRunConfiguration : IEntityTypeConfiguration<ConnectorRun>
{
    public void Configure(EntityTypeBuilder<ConnectorRun> builder)
    {
        builder.ToTable("ConnectorRuns", IntegrationSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Connector).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Contract).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.CursorHeldReason).HasMaxLength(80);
        builder.Property(x => x.FailureCode).HasMaxLength(80);
        builder.Property(x => x.RooftopId)
            .HasConversion(id => id.Value, value => new RooftopId(value))
            .IsRequired();

        // The operator's question is "how has this feed been doing lately", so
        // the index is per feed, newest first.
        builder.HasIndex(x => new { x.Connector, x.RooftopId, x.Contract, x.StartedAt });

        builder.ConfigureAudit();
    }
}

internal sealed class ConnectorScheduleConfiguration : IEntityTypeConfiguration<ConnectorSchedule>
{
    public void Configure(EntityTypeBuilder<ConnectorSchedule> builder)
    {
        builder.ToTable("ConnectorSchedules", IntegrationSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Connector).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Contract).HasMaxLength(60).IsRequired();
        builder.Property(x => x.State).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.SuspendedReason).HasMaxLength(300);
        builder.Property(x => x.LastOutcome).HasMaxLength(30);
        builder.Property(x => x.RooftopId)
            .HasConversion(id => id.Value, value => new RooftopId(value))
            .IsRequired();

        // Unbounded, like the quarantine payload: a connector may declare any
        // number of settings and a protected value is longer than its plaintext,
        // so a limit here is a length nobody can calculate and a truncation that
        // corrupts a credential.
        builder.Property(x => x.Settings).IsRequired();

        // One schedule per feed. See the header — this index is load-bearing.
        builder.HasIndex(x => new { x.Connector, x.RooftopId, x.Contract, x.Version }).IsUnique();

        // The dispatcher's only query is "what is due", every twenty seconds,
        // across every tenant. It is the one read worth indexing for.
        builder.HasIndex(x => new { x.State, x.NextRunAt });

        builder.ConfigureAudit();
    }
}

internal sealed class QuarantinedRecordConfiguration : IEntityTypeConfiguration<QuarantinedRecord>
{
    public void Configure(EntityTypeBuilder<QuarantinedRecord> builder)
    {
        builder.ToTable("QuarantinedRecords", IntegrationSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Connector).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Contract).HasMaxLength(60).IsRequired();
        builder.Property(x => x.ExternalId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ExternalVersion).HasMaxLength(100);
        builder.Property(x => x.ReasonCode).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ReasonDetail).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ResolutionNote).HasMaxLength(500);
        builder.Property(x => x.RooftopId)
            .HasConversion(id => id.Value, value => new RooftopId(value))
            .IsRequired();

        // Unbounded: a provider record can be any size, and truncating the
        // payload would defeat the point of keeping it (ADR-021 — a truncated
        // key matches the wrong record).
        builder.Property(x => x.Payload).IsRequired();

        // Every read is "what is still waiting for this dealership", and every
        // read filters on the expiry (ADR-022).
        builder.HasIndex(x => new { x.RooftopId, x.Contract, x.ResolvedAt, x.ExpiresAt });

        builder.ConfigureAudit();
    }
}
