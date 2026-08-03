// MigrationTables — how import jobs and their staged rows are stored. Owns the
// "migration" schema and no other (ADR-014).
//
// Use:  nothing calls these directly. TenantDb finds them by scanning.
// Edit: the raw row column is deliberately generous and deliberately not indexed.
//       It exists to be read back by a person resolving an exception, not to be
//       searched — and a row from a real dealer export can be long.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenDealer360.Data;

namespace OpenDealer360.DataMigration;

/// <summary>The schema this feature owns.</summary>
internal static class MigrationSchema
{
    public const string Name = "migration";
}

internal sealed class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
    public void Configure(EntityTypeBuilder<ImportJob> builder)
    {
        builder.ToTable("ImportJobs", MigrationSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        // Stored as text: a status column full of 2s is unreadable in the
        // database, which is exactly where somebody looks when a job is stuck.
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Mode).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        builder.Property(x => x.SourceName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.SourceHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.FailureReason).HasMaxLength(1000);

        // The worker's claim query: oldest queued job first.
        builder.HasIndex(x => new { x.Status, x.QueuedAt });

        builder.ConfigureAudit();
    }
}

internal sealed class ImportRowConfiguration : IEntityTypeConfiguration<ImportRow>
{
    public void Configure(EntityTypeBuilder<ImportRow> builder)
    {
        builder.ToTable("ImportRows", MigrationSchema.Name);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Raw).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(1000);

        // Reading a job's rows back, worst first — which is the order somebody
        // resolving exceptions wants them in.
        builder.HasIndex(x => new { x.JobId, x.Outcome });
        builder.HasIndex(x => new { x.JobId, x.RowNumber }).IsUnique();
    }
}
