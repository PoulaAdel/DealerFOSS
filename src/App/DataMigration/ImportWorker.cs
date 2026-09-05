// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ImportWorker — the background service that actually runs queued imports.
//
// Usage:
//   Registered in Program.cs. Nothing calls it.
//
// Coding Instructions:
//   This is the first piece of work in the system that is not a request, so
//   three things it does are the pattern for everything that follows —
//   reconciliation, outbox delivery, and whatever comes after.
//
//   **It names the tenant.** There is no ambient "current dealership" out
//   here, and there must never be one. Every unit of work opens a
//   TenantScope for a tenant it has explicitly identified, and a job in one
//   dealership's database can only ever be run against that database.
//
//   **It runs as the person who asked.** The requester is named in the
//   JobContext the scope is opened with, so the import is authorized by their
//   permissions and audited under their name. This used to be a promise this
//   comment made on the code's behalf; it is now the only way OpenAsync can be
//   called.
//
//   **It claims before it works.** Two instances of the application share one
//   database, so the move from Queued to Running is a conditional update and
//   the loser simply finds nothing to do.
//
//   **Two scopes per job, and that is not an accident.** Polling for work and
//   claiming it is nobody's request — the dispatcher scope says so and its
//   writes are attributed to the system. Running the job is the requester's,
//   and that scope is opened only once their id is known. Doing it in one scope
//   meant the claim was written before any caller existed, and a job that
//   failed early was attributed to the system while one that failed late was
//   attributed to the person: the same event, two different answers, depending
//   on timing.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DealerFOSS.Core;
using DealerFOSS.Customers;
using DealerFOSS.Data;
using DealerFOSS.Tenancy;
using DealerFOSS.Vehicles;

namespace DealerFOSS.DataMigration;

internal sealed partial class ImportWorker(
    IServiceScopeFactory scopeFactory,
    ITenantScopeFactory tenantScopes,
    IClock clock,
    ILogger<ImportWorker> logger)
    : BackgroundService
{
    // Source-generated rather than called through LoggerExtensions: the analyzer
    // requires it, and it means a message is defined once instead of being
    // formatted on every call whether or not anything is listening.

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Import worker pass failed; continuing.")]
    private static partial void PassFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Import {JobId} failed in {Tenant}.")]
    private static partial void ImportFailed(
        ILogger logger, Guid jobId, string tenant, Exception exception);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Could not record the failure of import {JobId}.")]
    private static partial void CouldNotRecordFailure(
        ILogger logger, Guid jobId, Exception exception);

    /// <summary>
    /// How long to wait when there was nothing to do. Short enough that a person
    /// watching a progress bar sees it move, long enough that an idle
    /// installation is not querying every tenant continuously.
    /// </summary>
    private static readonly TimeSpan Idle = TimeSpan.FromSeconds(2);

    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ITenantScopeFactory _tenantScopes = tenantScopes;
    private readonly IClock _clock = clock;
    private readonly ILogger<ImportWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var didWork = false;

            try
            {
                didWork = await RunOnePassAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // A worker that dies on one bad job stops every
            // dealership's imports, including the ones that would have worked.
            // The failure is logged and the loop continues.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                PassFailed(_logger, ex);
            }

            if (!didWork)
            {
                try
                {
                    await Task.Delay(Idle, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    /// <summary>
    /// One job, in one dealership. Returns true when something was done, so a
    /// busy installation keeps working without waiting.
    /// </summary>
    private async Task<bool> RunOnePassAsync(CancellationToken cancellationToken)
    {
        foreach (var slug in await ActiveTenantsAsync(cancellationToken))
        {
            if (await RunNextJobAsync(slug, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<IReadOnlyList<string>> ActiveTenantsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<HostDb>();

        return await catalog.Tenants
            .AsNoTracking()
            .Where(t => t.Status == TenantStatus.Active)
            .OrderBy(t => t.Slug)
            .Select(t => t.Slug)
            .ToListAsync(cancellationToken);
    }

    private async Task<bool> RunNextJobAsync(string slug, CancellationToken cancellationToken)
    {
        // Nobody asked for the polling, so the dispatcher says so and its writes —
        // the claim — are attributed to the system, which is what happened.
        await using var dispatch = await _tenantScopes.OpenAsync(
            JobContext.Unattended(slug, DispatcherReason), cancellationToken);

        if (dispatch is null)
        {
            // Suspended between listing and opening. Its queued work waits,
            // which is the correct behaviour for a dealership out of service.
            return false;
        }

        var claim = await ClaimNextJobAsync(dispatch, cancellationToken);
        if (claim is null)
        {
            return false;
        }

        // A second scope, opened now that there is a person to open it as.
        await using var run = await _tenantScopes.OpenAsync(
            JobContext.RequestedBy(slug, claim.Value.RequestedByUserId, RunReason),
            cancellationToken);

        if (run is null)
        {
            // Suspended in the gap between the claim and the run. The job is
            // already Running, so leaving it would strand it there forever.
            await MarkFailedAsync(
                dispatch,
                claim.Value.JobId,
                "This dealership was suspended between the job being claimed and being started.",
                cancellationToken);

            return true;
        }

        try
        {
            await ProcessAsync(run, claim.Value.JobId, cancellationToken);
        }
#pragma warning disable CA1031 // Any failure of the job as a whole is recorded on
        // the job so a person can see it, rather than vanishing into a log.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            ImportFailed(_logger, claim.Value.JobId, run.Tenant.Key, ex);
            await MarkFailedAsync(run, claim.Value.JobId, ex.Message, cancellationToken);
        }

        return true;
    }

    /// <summary>Reasons carried on the two job contexts, so an audit can tell them apart.</summary>
    internal const string DispatcherReason = "import dispatcher";

    internal const string RunReason = "csv import";

    /// <summary>What the dispatcher learned: which job, and who to run it as.</summary>
    private readonly record struct Claim(Guid JobId, Guid RequestedByUserId);

    /// <summary>
    /// Takes the next queued job for this dealership, or returns null when there
    /// is none — or when another instance took it first.
    /// </summary>
    private async Task<Claim?> ClaimNextJobAsync(
        TenantScope dispatch,
        CancellationToken cancellationToken)
    {
        var db = dispatch.Services.GetRequiredService<TenantDb>();

        var job = await db.ImportJobs
            .AsNoTracking()
            .Where(j => j.Status == ImportStatus.Queued)
            .OrderBy(j => j.QueuedAt)
            .Select(j => new { j.Id, j.RequestedByUserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (job is null)
        {
            return null;
        }

        // Claim it. Two application instances share this database, so whoever
        // updates the row first owns the job and the other finds it gone.
        var claimed = await db.ImportJobs
            .Where(j => j.Id == job.Id && j.Status == ImportStatus.Queued)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(j => j.Status, ImportStatus.Running)
                    .SetProperty(j => j.StartedAt, _clock.UtcNow),
                cancellationToken);

        return claimed == 0 ? null : new Claim(job.Id, job.RequestedByUserId);
    }

    private async Task ProcessAsync(
        TenantScope scope,
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var db = scope.Services.GetRequiredService<TenantDb>();

        var job = await db.ImportJobs.SingleAsync(j => j.Id == jobId, cancellationToken);

        // Nothing establishes the caller here any more: the scope arrived already
        // running as the requester, because JobContext made that part of opening it.
        var runner = new ImportRunner(
            scope.Services.GetRequiredService<ICustomers>(),
            scope.Services.GetRequiredService<IVehicles>());

        var rows = await db.ImportRows
            .Where(r => r.JobId == jobId && r.RowNumber != HeaderRowNumber)
            .OrderBy(r => r.RowNumber)
            .ToListAsync(cancellationToken);

        // Read from the staged header line rather than from anything remembered,
        // so there is nowhere for a header and its rows to disagree.
        var (header, headerLine) = await HeaderForAsync(db, jobId, cancellationToken);

        var created = 0;
        var updated = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var row in rows)
        {
            var parsed = Csv.Read($"{headerLine}\n{row.Raw}");
            RowDecision decision;

            if (parsed.Rows.Count == 0)
            {
                decision = new RowDecision(RowOutcome.Failed, "This row could not be read.");
            }
            else if (parsed.Rows[0].Fields.Count != header.Count)
            {
                // Padding a short row would import a customer with somebody
                // else's phone number in the address column.
                decision = new RowDecision(
                    RowOutcome.Failed,
                    $"This row has {parsed.Rows[0].Fields.Count} values but the file has "
                    + $"{header.Count} columns.");
            }
            else
            {
                decision = await runner.RunAsync(
                    job.Kind, job.Mode, header, parsed.Rows[0], cancellationToken);
            }

            row.Record(decision.Outcome, decision.Message);

            switch (decision.Outcome)
            {
                case RowOutcome.Created: created++; break;
                case RowOutcome.Updated: updated++; break;
                case RowOutcome.Skipped: skipped++; break;
                default: failed++; break;
            }
        }

        job.Complete(created, updated, skipped, failed, _clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The file's column names and the line they came from, staged as row 1 so
    /// the original document can be reconstructed exactly.
    /// </summary>
    private static async Task<(IReadOnlyList<string> Header, string HeaderLine)> HeaderForAsync(
        TenantDb db,
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var line = await db.ImportRows
            .AsNoTracking()
            .Where(r => r.JobId == jobId && r.RowNumber == HeaderRowNumber)
            .Select(r => r.Raw)
            .SingleOrDefaultAsync(cancellationToken);

        // The trailing "x" gives Read a data row to parse; only the header is
        // wanted, and a header with no rows after it reads as an empty document.
        return line is null ? ([], string.Empty) : (Csv.Read($"{line}\nx").Header, line);
    }

    /// <summary>Row 1 of the file, as a spreadsheet counts.</summary>
    internal const int HeaderRowNumber = 1;

    private async Task MarkFailedAsync(
        TenantScope scope,
        Guid jobId,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            var db = scope.Services.GetRequiredService<TenantDb>();
            db.ChangeTracker.Clear();

            var job = await db.ImportJobs.SingleAsync(j => j.Id == jobId, cancellationToken);
            job.Fail(reason.Length > 1000 ? reason[..1000] : reason, _clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
        }
#pragma warning disable CA1031 // If even recording the failure fails, the job is
        // left Running and the log is the only record. Throwing here would take
        // the worker down with it.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            CouldNotRecordFailure(_logger, jobId, ex);
        }
    }
}
