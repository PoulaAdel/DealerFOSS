// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   IMigration — a dealership's records arriving from a file, and leaving in one.
//
// Usage:
//   The Migration endpoints call this. Nothing else does yet; when a
//   connector lands it will queue jobs through the same contract.
//
// Coding Instructions:
//   The shape to protect on the way in is that submitting a file and running
//   it are separate. The request stages rows and returns; a worker does the
//   work. A dealership's export is tens of thousands of rows and an HTTP
//   request that tried to finish the job would time out somewhere in the
//   middle, having half-imported their customers with no record of where it
//   stopped.
//
//   The shape to protect on the way out is that **an export is a valid
//   import**. Identical column names, in an order the importer accepts, so a
//   dealership can take their data to a competitor — or back — without
//   anybody here writing a converter for them. That is what an open DMS
//   owes its users, and a round-trip test asserts it rather than a promise
//   in a README.

using DealerFOSS.Core;

namespace DealerFOSS.DataMigration;

public interface IMigration
{
    /// <summary>
    /// Stages a file and queues it. Returns as soon as the rows are safely
    /// recorded — nothing has been imported yet.
    /// </summary>
    Task<Result<ImportJobView>> SubmitAsync(
        NewImport import,
        CancellationToken cancellationToken);

    Task<Result<ImportJobView>> GetAsync(Guid jobId, CancellationToken cancellationToken);

    Task<Result<Page<ImportJobView>>> ListAsync(int limit, int offset, CancellationToken cancellationToken);

    /// <summary>
    /// The staged rows and what happened to each. <paramref name="problemsOnly"/>
    /// is the normal case: after nine thousand rows, what somebody needs is the
    /// twelve that did not work.
    /// </summary>
    Task<Result<IReadOnlyList<ImportRowView>>> GetRowsAsync(
        Guid jobId,
        bool problemsOnly,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Everything of one kind, as a file this same API would accept back.
    /// </summary>
    /// <remarks>
    /// Deliberately synchronous where importing is not. An export reads and
    /// writes nothing, so there is no half-finished state to recover from and
    /// nothing to be gained by making somebody poll for it.
    /// </remarks>
    Task<Result<ExportedFile>> ExportAsync(string kind, CancellationToken cancellationToken);

    /// <summary>
    /// One rooftop's records — its stock, its deals, its jobs, and the customers
    /// and cars they name — as a package this same API accepts back, with every
    /// id and every reference intact.
    /// </summary>
    /// <remarks>
    /// The two CSVs above and this are not alternatives and neither replaces the
    /// other. A CSV is what a dealership wants when they are taking their
    /// customer list somewhere that is not DealerFOSS; a package is what moves a
    /// lot between two DealerFOSS installations without losing what points at
    /// what. Documents are absent from both because this system stores no files
    /// — see ADR-027.
    /// </remarks>
    Task<Result<ExportedFile>> ExportPackageAsync(
        RooftopId rooftopId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Applies a package to one rooftop of this installation and reports what
    /// landed, what was already here, and what was refused.
    /// </summary>
    /// <remarks>
    /// Synchronous where a CSV import is queued, and for the opposite reason to
    /// the one in <see cref="ExportAsync"/>: a package is one lot rather than a
    /// whole group's history, so it fits in a request — and a person applying
    /// one is standing there waiting to find out whether it worked. Running the
    /// same package again is safe and is the recovery path, so there is nothing
    /// to poll for.
    /// </remarks>
    Task<Result<PackageImportReport>> ImportPackageAsync(
        RooftopId rooftopId,
        string content,
        CancellationToken cancellationToken);
}

/// <summary>
/// A dealership's records on their way out.
/// </summary>
/// <param name="Checksum">
/// SHA-256 of <paramref name="Content"/>. Published so the receiving end can
/// prove the file arrived whole — an export truncated in transit is worse than
/// one that failed, because it looks like data.
/// </param>
public sealed record ExportedFile(
    string Kind,
    string FileName,
    string Content,
    string Checksum,
    int RowCount);

/// <summary>A file to import, as submitted.</summary>
public sealed record NewImport(string Kind, string Mode, string SourceName, string Content);

/// <summary>
/// A job and its reconciliation report. The four counts sum to
/// <see cref="RowsTotal"/> once the job has finished; while it is running they
/// are what has been decided so far.
/// </summary>
public sealed record ImportJobView(
    Guid Id,
    string Kind,
    string Mode,
    string Status,
    string SourceName,
    string SourceHash,
    int RowsTotal,
    int RowsCreated,
    int RowsUpdated,
    int RowsSkipped,
    int RowsFailed,
    DateTimeOffset QueuedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? FailureReason);

/// <summary>One staged row, exactly as it arrived, and its outcome.</summary>
public sealed record ImportRowView(int RowNumber, string Raw, string Outcome, string? Message);

/// <summary>Stable error codes for the Migration capability (doc 06 §6).</summary>
internal static class MigrationErrors
{
    public static Error Forbidden { get; } = Error.Forbidden(
        "migration.forbidden",
        "You do not have permission to import records.");

    public static Error ForbiddenExport { get; } = Error.Forbidden(
        "migration.forbidden_export",
        "You do not have permission to export this dealership's records.");

    public static Error NotFound { get; } = Error.NotFound(
        "migration.not_found",
        "No such import.");

    public static Error UnknownKind { get; } = Error.Validation(
        "migration.unknown_kind",
        "An import is of Customers or Vehicles. One file holds one kind.");

    public static Error UnknownMode { get; } = Error.Validation(
        "migration.unknown_mode",
        "An import runs as a Trial, which changes nothing, or an Apply, which does.");

    public static Error Empty { get; } = Error.Validation(
        "migration.empty",
        "That file has a header but no rows, or no content at all.");

    public static Error TooLarge { get; } = Error.Validation(
        "migration.too_large",
        "That file has more rows than one import accepts. Split it and submit the parts.");

    public static Error MissingColumns { get; } = Error.Validation(
        "migration.missing_columns",
        "That file is missing columns this import needs.");

    /// <summary>
    /// The file is not one of ours. Named rather than left to a deserialization
    /// error, because "unexpected token" tells somebody holding the wrong file
    /// nothing about which file they should be holding.
    /// </summary>
    public static Error NotAPackage { get; } = Error.Validation(
        "migration.not_a_package",
        "That is not a DealerFOSS records package.");

    public static Error Unreadable { get; } = Error.Validation(
        "migration.package_unreadable",
        "That package could not be read. It may be truncated or it may not be JSON at all.");

    /// <summary>
    /// A package from a newer build. Refused rather than read on a best-effort
    /// basis: a partial import nobody was told about is the worst outcome
    /// available here, and upgrading is a thing a person can actually do.
    /// </summary>
    public static Error PackageIsNewer(int found, int supported) => Error.Validation(
        "migration.package_is_newer",
        $"That package is version {found} and this installation reads up to {supported}. Upgrade before importing it.");
}
