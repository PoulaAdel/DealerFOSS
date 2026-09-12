// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   MigrationService — staging a file, and reading back what happened to it.
//
// Usage:
//   Through IMigration.
//
// Coding Instructions:
//   This does not import anything. It validates that a file is the shape it
//   claims to be, records every row untouched, and stops. The importing is
//   ImportRunner's job and happens later, in the worker.
//
//   The header check is here rather than in the runner on purpose: a file
//   with the wrong columns is a mistake somebody made two seconds ago and
//   can fix immediately, so telling them now beats queueing a job that is
//   certain to fail every row.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using DealerFOSS.Core;
using DealerFOSS.Customers;
using DealerFOSS.Data;
using DealerFOSS.Identity;
using DealerFOSS.Vehicles;

namespace DealerFOSS.DataMigration;

public sealed class MigrationService(
    TenantDb db,
    IAccessDirectory access,
    ICurrentUser currentUser,
    IClock clock,
    IAuditSink audit,
    ICustomers customers,
    IVehicles vehicles)
    : IMigration
{
    /// <summary>
    /// Importing writes records in bulk on behalf of a whole dealer organization,
    /// so it is its own permission and is granted organization-wide or not at all.
    /// </summary>
    private const string ImportPermission = Permissions.MigrationImport;

    /// <summary>
    /// One request's worth. A real dealer extract is bigger than this and is meant
    /// to be split — a cap here keeps a single request from staging a gigabyte
    /// into the tenant database before anybody has checked the columns are right.
    /// </summary>
    private const int MaxRows = 20_000;

    private const int MaxJobs = 100;

    /// <summary>Rows per page while walking the whole set for an export.</summary>
    private const int ExportPageSize = 500;

    /// <summary>
    /// A ceiling on one export, so a request cannot build an unbounded string in
    /// memory. A dealership larger than this needs streaming, which is a change
    /// to how the response is written rather than to any of the logic here.
    /// </summary>
    private const int MaxExportRows = 50_000;

    private readonly TenantDb _db = db;
    private readonly IAccessDirectory _access = access;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IClock _clock = clock;
    private readonly IAuditSink _audit = audit;
    private readonly ICustomers _customers = customers;
    private readonly IVehicles _vehicles = vehicles;

    public async Task<Result<ImportJobView>> SubmitAsync(
        NewImport import,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(import);

        if (!await MayImportAsync(cancellationToken))
        {
            return Result.Failure<ImportJobView>(MigrationErrors.Forbidden);
        }

        if (!Enum.TryParse<ImportKind>(import.Kind, ignoreCase: true, out var kind))
        {
            return Result.Failure<ImportJobView>(MigrationErrors.UnknownKind);
        }

        if (!Enum.TryParse<ImportMode>(import.Mode, ignoreCase: true, out var mode))
        {
            return Result.Failure<ImportJobView>(MigrationErrors.UnknownMode);
        }

        var document = Csv.Read(import.Content ?? string.Empty);
        if (document.Rows.Count == 0)
        {
            return Result.Failure<ImportJobView>(MigrationErrors.Empty);
        }

        if (document.Rows.Count > MaxRows)
        {
            return Result.Failure<ImportJobView>(MigrationErrors.TooLarge);
        }

        var missing = ImportRunner.MissingColumns(kind, document.Header);
        if (missing.Count > 0)
        {
            // Naming them is the difference between a person fixing the file in a
            // minute and guessing at it for an afternoon.
            return Result.Failure<ImportJobView>(Error.Validation(
                MigrationErrors.MissingColumns.Code,
                $"{MigrationErrors.MissingColumns.Message} Missing: {string.Join(", ", missing)}. "
                + $"Found: {(document.Header.Count == 0 ? "nothing" : string.Join(", ", document.Header))}."));
        }

        var job = new ImportJob(
            Guid.NewGuid(),
            kind,
            mode,
            Truncate(import.SourceName, 260),
            HashOf(import.Content ?? string.Empty),
            document.Rows.Count,
            _currentUser.Id,
            _clock.UtcNow);

        _db.ImportJobs.Add(job);

        // The header is staged too, as row 1, so the file can be reconstructed
        // exactly and the worker reads the columns from what actually arrived
        // rather than from a re-joined copy of them.
        var headerRow = new ImportRow(
            Guid.NewGuid(), job.Id, ImportWorker.HeaderRowNumber, document.HeaderLine);
        headerRow.Record(RowOutcome.Skipped, "Column names.");

        _db.ImportRows.Add(headerRow);
        _db.ImportRows.AddRange(
            document.Rows.Select(r => new ImportRow(Guid.NewGuid(), job.Id, r.Number, r.Raw)));

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, "Migration.Submit", AuditOutcome.Allowed,
                "ImportJob", job.Id.ToString(), null,
                $"{job.Mode} import of {document.Rows.Count} {job.Kind} row(s) from '{job.SourceName}'.",
                null, null),
            cancellationToken);

        return Result.Success(Describe(job));
    }

    public async Task<Result<ImportJobView>> GetAsync(Guid jobId, CancellationToken cancellationToken)
    {
        if (!await MayImportAsync(cancellationToken))
        {
            return Result.Failure<ImportJobView>(MigrationErrors.Forbidden);
        }

        var job = await _db.ImportJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        return job is null
            ? Result.Failure<ImportJobView>(MigrationErrors.NotFound)
            : Result.Success(Describe(job));
    }

    public async Task<Result<Page<ImportJobView>>> ListAsync(
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        if (!await MayImportAsync(cancellationToken))
        {
            return Result.Failure<Page<ImportJobView>>(MigrationErrors.Forbidden);
        }

        var take = Paging.Limit(limit, fallback: 25, max: MaxJobs);
        var skip = Paging.Offset(offset);
        var queued = _db.ImportJobs.AsNoTracking();

        var total = await queued.CountAsync(cancellationToken);

        var jobs = await queued
            .OrderByDescending(j => j.QueuedAt)
            .ThenBy(j => j.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return Result.Success(new Page<ImportJobView>(
            jobs.Select(Describe).ToList(), total, skip, take));
    }

    public async Task<Result<IReadOnlyList<ImportRowView>>> GetRowsAsync(
        Guid jobId,
        bool problemsOnly,
        int limit,
        CancellationToken cancellationToken)
    {
        if (!await MayImportAsync(cancellationToken))
        {
            return Result.Failure<IReadOnlyList<ImportRowView>>(MigrationErrors.Forbidden);
        }

        if (!await _db.ImportJobs.AnyAsync(j => j.Id == jobId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<ImportRowView>>(MigrationErrors.NotFound);
        }

        // The header is staged as row 1 but is not data, so it never appears in
        // the report — somebody reading their exceptions does not need to be
        // told their column names are column names.
        var query = _db.ImportRows
            .AsNoTracking()
            .Where(r => r.JobId == jobId && r.RowNumber != ImportWorker.HeaderRowNumber);

        if (problemsOnly)
        {
            query = query.Where(r =>
                r.Outcome == RowOutcome.Failed || r.Outcome == RowOutcome.Skipped);
        }

        var rows = await query
            .OrderBy(r => r.RowNumber)
            .Take(Math.Clamp(limit <= 0 ? 100 : limit, 1, 1000))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ImportRowView>>(
            rows.Select(r => new ImportRowView(
                r.RowNumber, r.Raw, r.Outcome.ToString(), r.Message)).ToList());
    }

    public async Task<Result<ExportedFile>> ExportAsync(
        string kind,
        CancellationToken cancellationToken)
    {
        // A separate permission from importing, because it is a different act:
        // this is bulk PII leaving the building (doc 06 §3). Somebody trusted to
        // load a supplier's stock list is not automatically trusted to walk out
        // with every customer the group has.
        var scope = await _access.GetAuthorizedScopeAsync(
            _currentUser.Id, Permissions.MigrationExport, cancellationToken);

        if (!scope.IsOrganizationWide)
        {
            return Result.Failure<ExportedFile>(MigrationErrors.ForbiddenExport);
        }

        if (!Enum.TryParse<ImportKind>(kind, ignoreCase: true, out var parsed))
        {
            return Result.Failure<ExportedFile>(MigrationErrors.UnknownKind);
        }

        var file = new StringBuilder();
        file.Append(Exporter.HeaderFor(parsed)).Append('\n');

        var rows = parsed == ImportKind.Customers
            ? await WriteCustomersAsync(file, cancellationToken)
            : await WriteVehiclesAsync(file, cancellationToken);

        if (rows.IsFailure)
        {
            return Result.Failure<ExportedFile>(rows.Error);
        }

        var content = file.ToString();

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, "Migration.Export", AuditOutcome.Allowed,
                "Export", parsed.ToString(), null,
                $"Exported {rows.Value} {parsed} record(s).", null, null),
            cancellationToken);

        return Result.Success(new ExportedFile(
            parsed.ToString(),
            $"{parsed.ToString().ToLower(CultureInfo.InvariantCulture)}.csv",
            content,
            HashOf(content),
            rows.Value));
    }

    private async Task<Result<int>> WriteCustomersAsync(
        StringBuilder file,
        CancellationToken cancellationToken)
    {
        Guid? after = null;
        var written = 0;

        while (written < MaxExportRows)
        {
            var page = await _customers.PageForExportAsync(after, ExportPageSize, cancellationToken);
            if (page.IsFailure)
            {
                return Result.Failure<int>(page.Error);
            }

            if (page.Value.Count == 0)
            {
                break;
            }

            foreach (var customer in page.Value)
            {
                file.Append(Exporter.Row(customer)).Append('\n');
                written++;
            }

            after = page.Value[^1].Id;
        }

        return Result.Success(written);
    }

    private async Task<Result<int>> WriteVehiclesAsync(
        StringBuilder file,
        CancellationToken cancellationToken)
    {
        Guid? after = null;
        var written = 0;

        while (written < MaxExportRows)
        {
            var page = await _vehicles.PageForExportAsync(after, ExportPageSize, cancellationToken);
            if (page.IsFailure)
            {
                return Result.Failure<int>(page.Error);
            }

            if (page.Value.Count == 0)
            {
                break;
            }

            foreach (var vehicle in page.Value)
            {
                file.Append(Exporter.Row(vehicle)).Append('\n');
                written++;
            }

            after = page.Value[^1].Id;
        }

        return Result.Success(written);
    }

    /// <summary>
    /// Organization-wide or nothing. An import writes records across every
    /// rooftop at once, so rooftop-scoped permission cannot express it — and
    /// treating a partial scope as sufficient would let a one-lot manager rewrite
    /// the group's customer list.
    /// </summary>
    private async Task<bool> MayImportAsync(CancellationToken cancellationToken)
    {
        var scope = await _access.GetAuthorizedScopeAsync(
            _currentUser.Id, ImportPermission, cancellationToken);

        return scope.IsOrganizationWide;
    }

    internal static ImportJobView Describe(ImportJob job) =>
        new(job.Id,
            job.Kind.ToString(),
            job.Mode.ToString(),
            job.Status.ToString(),
            job.SourceName,
            job.SourceHash,
            job.RowsTotal,
            job.RowsCreated,
            job.RowsUpdated,
            job.RowsSkipped,
            job.RowsFailed,
            job.QueuedAt,
            job.StartedAt,
            job.FinishedAt,
            job.FailureReason);

    /// <summary>
    /// SHA-256 of the submitted text. Two jobs sharing a hash read the same
    /// extract, which is how a trial and the run that follows it are shown to be
    /// about the same data (doc 05 §6 step 1).
    /// </summary>
    private static string HashOf(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)))
            .ToLower(CultureInfo.InvariantCulture);

    private static string Truncate(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return "(unnamed)";
        }

        return text.Length <= max ? text : text[..max];
    }
}
