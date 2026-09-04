// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ImportJob — one attempt at bringing a dealership's existing records in.
//
// Usage:
//   Created by MigrationService, run by ImportWorker.
//
// Coding Instructions:
//   The counts are the reconciliation report (doc 05 §6 step 7), so every row
//   must land in exactly one of created/updated/skipped/failed. If they stop
//   summing to RowsTotal, the report is lying and somebody will trust it.

using DealerFOSS.Core;

namespace DealerFOSS.DataMigration;

/// <summary>What kind of record a file holds. One file, one kind.</summary>
public enum ImportKind
{
    Customers = 1,
    Vehicles = 2,
}

/// <summary>
/// Whether this run is allowed to change anything.
/// </summary>
/// <remarks>
/// <see cref="Trial"/> is not a lesser version of <see cref="Apply"/>: it runs the
/// same code over the same rows and produces the same report, then rolls back. A
/// trial that took a different path would be answering a different question than
/// the one being asked, which is "what will happen when I do this for real?"
/// </remarks>
public enum ImportMode
{
    Trial = 1,
    Apply = 2,
}

public enum ImportStatus
{
    Queued = 1,
    Running = 2,
    Completed = 3,
    Failed = 4,
}

/// <summary>What happened to one staged row.</summary>
public enum RowOutcome
{
    Pending = 0,
    Created = 1,
    Updated = 2,
    Skipped = 3,
    Failed = 4,
}

internal sealed class ImportJob : AuditableEntity
{
    private ImportJob()
    {
    }

    public ImportJob(
        Guid id,
        ImportKind kind,
        ImportMode mode,
        string sourceName,
        string sourceHash,
        int rowsTotal,
        Guid requestedByUserId,
        DateTimeOffset queuedAt)
    {
        Id = id;
        Kind = kind;
        Mode = mode;
        SourceName = sourceName;
        SourceHash = sourceHash;
        RowsTotal = rowsTotal;
        RequestedByUserId = requestedByUserId;
        QueuedAt = queuedAt;
        Status = ImportStatus.Queued;
    }

    public Guid Id { get; private set; }

    public ImportKind Kind { get; private set; }

    public ImportMode Mode { get; private set; }

    /// <summary>The file's name as supplied, for a person recognising their own upload.</summary>
    public string SourceName { get; private set; } = string.Empty;

    /// <summary>
    /// SHA-256 of the file's bytes (doc 05 §6 step 1). Two jobs with the same hash
    /// read the same extract, which is how a trial and the real run that follows
    /// it can be shown to be about the same data.
    /// </summary>
    public string SourceHash { get; private set; } = string.Empty;

    public ImportStatus Status { get; private set; }

    public int RowsTotal { get; private set; }

    public int RowsCreated { get; private set; }

    public int RowsUpdated { get; private set; }

    public int RowsSkipped { get; private set; }

    public int RowsFailed { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public DateTimeOffset QueuedAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    /// <summary>Why the whole job stopped, as opposed to why one row did.</summary>
    public string? FailureReason { get; private set; }

    public void Start(DateTimeOffset now)
    {
        Status = ImportStatus.Running;
        StartedAt = now;
    }

    public void Complete(int created, int updated, int skipped, int failed, DateTimeOffset now)
    {
        RowsCreated = created;
        RowsUpdated = updated;
        RowsSkipped = skipped;
        RowsFailed = failed;
        Status = ImportStatus.Completed;
        FinishedAt = now;
    }

    /// <summary>
    /// The job itself broke — an unreadable file, a database that went away. Rows
    /// that failed individually do not come here; those are counted and the job
    /// still completes, because a report naming twelve bad rows out of nine
    /// thousand is a successful import with work to do.
    /// </summary>
    public void Fail(string reason, DateTimeOffset now)
    {
        Status = ImportStatus.Failed;
        FailureReason = reason;
        FinishedAt = now;
    }
}

/// <summary>
/// One row of the source, kept exactly as it arrived.
/// </summary>
/// <remarks>
/// The raw text is stored and never rewritten. Exceptions are resolved by fixing
/// the source and importing again, not by editing what we received — otherwise
/// nobody can say afterwards what the dealership actually sent.
/// </remarks>
internal sealed class ImportRow
{
    private ImportRow()
    {
    }

    public ImportRow(Guid id, Guid jobId, int rowNumber, string raw)
    {
        Id = id;
        JobId = jobId;
        RowNumber = rowNumber;
        Raw = raw;
        Outcome = RowOutcome.Pending;
    }

    public Guid Id { get; private set; }

    public Guid JobId { get; private set; }

    /// <summary>As a spreadsheet would number it, header included.</summary>
    public int RowNumber { get; private set; }

    public string Raw { get; private set; } = string.Empty;

    public RowOutcome Outcome { get; private set; }

    /// <summary>Why it was skipped or refused, in words the dealership can act on.</summary>
    public string? Message { get; private set; }

    public void Record(RowOutcome outcome, string? message = null)
    {
        Outcome = outcome;
        Message = message;
    }
}
