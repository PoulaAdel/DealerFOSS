// IMigration — bringing a dealership's existing records in from a file.
//
// Use:  the Migration endpoints call this. Nothing else does yet; when a
//       connector lands it will queue jobs through the same contract.
// Edit: the shape to protect is that submitting a file and running it are
//       separate. The request stages rows and returns; a worker does the work.
//       A dealership's export is tens of thousands of rows and an HTTP request
//       that tried to finish the job would time out somewhere in the middle,
//       having half-imported their customers with no record of where it stopped.

using OpenDealer360.Core;

namespace OpenDealer360.DataMigration;

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

    Task<Result<IReadOnlyList<ImportJobView>>> ListAsync(int limit, CancellationToken cancellationToken);

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
}

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
}
