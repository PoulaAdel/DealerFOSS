// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ScheduleWorker — what starts a sync run when nobody is at a keyboard.
//
//   Before this, ConnectorRuntime had no caller anywhere in src: it was
//   registered in DI and started by tests. Every run was somebody's click,
//   which is fine for a one-off migration and useless for keeping in step with
//   a live system overnight.
//
// Usage:
//   Registered in Program.cs. Nothing calls it.
//
// Coding Instructions:
//   TWO SCOPES PER SCHEDULE, OF TWO DIFFERENT TYPES, and that is the whole
//   safety argument — the same shape ImportWorker uses and for the same reason.
//   Nobody asked for the polling, so the dispatcher holds an UnattendedScope
//   and can reach Get<TenantDb>() and nothing else: Get<IIntegrations>() here
//   is a compile error, not a runtime discovery. Somebody DID ask for the sync,
//   by arming the schedule, so the run itself is a TenantScope opened as that
//   person once their id has been read off the row.
//
//   AN UNATTENDED TRIGGER MUST NOT BECOME A WAY AROUND THE PERMISSION CHECK,
//   which is the one thing this file could get catastrophically wrong. The
//   tempting shape is a sweep that writes records itself, attributed to
//   "system" — it always succeeds, because there is nobody to refuse it. What
//   it actually builds is a second route into every record that ignores the
//   permission system, reachable on a timer. So this worker writes no
//   dealership record at all. It decides WHEN, and ConnectorRuntime — inside the
//   arming person's scope, through IRecordSink, through the owning capability —
//   decides WHETHER. ADR-028 has the reasoning.
//
//   THE GRANT IS RE-CHECKED AT FIRE TIME, NOT TRUSTED FROM ARMING TIME. This is
//   the part that makes "it carries somebody's authority" true rather than
//   decorative. Somebody arms a nightly customer sync and then leaves the
//   company, or has Migration.Import revoked. Their name is still on the row.
//   Without the re-check the feed keeps writing on the authority of a grant
//   that no longer exists, and the audit trail says a former employee did it
//   every night. The re-check happens INSIDE the attended scope on purpose: it
//   needs IAccessDirectory, which is permission-checked and must never carry
//   the IUnattendedSafe marker, so doing it in the dispatcher would have meant
//   widening the allow-list — the exact thing the allow-list exists to make
//   somebody argue for out loud.
//
//   MISCONFIGURED SUSPENDS; FAILED DOES NOT. Misconfigured means invalid
//   settings or no registered sink, and neither of those fixes itself with
//   time — a schedule for a contract with no sink would otherwise report the
//   same refusal every quarter-hour forever, which is how a run history becomes
//   something nobody reads. Failed is a provider having a bad night, and the
//   next interval is precisely what that is for.
//
//   The claim advances NextRunAt before the run, so a process killed mid-run
//   waits for its next interval rather than being retried at once. That is
//   correct and free: the cursor did not move, so the same window is read
//   again, and IRecordSink's idempotence obligation makes the re-read cost
//   nothing. Retrying immediately would hammer a provider that is already
//   failing.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DealerFOSS.Core;
using DealerFOSS.Data;
using DealerFOSS.Identity;
using DealerFOSS.Tenancy;

namespace DealerFOSS.Integrations;

internal sealed partial class ScheduleWorker(
    IServiceScopeFactory scopeFactory,
    ITenantScopeFactory tenantScopes,
    IClock clock,
    ILogger<ScheduleWorker> logger)
    : BackgroundService
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Schedule worker pass failed; continuing.")]
    private static partial void PassFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Scheduled sync {ScheduleId} failed in {Tenant}.")]
    private static partial void SyncFailed(
        ILogger logger, Guid scheduleId, string tenant, Exception exception);

    /// <summary>
    /// How often to look for a due schedule. Not how often a feed runs — that is
    /// the schedule's own interval, and its floor is five minutes. This is only
    /// the granularity with which "due" is noticed.
    /// </summary>
    private static readonly TimeSpan Idle = TimeSpan.FromSeconds(20);

    /// <summary>
    /// How far back a feed reaches the first time it runs, when it has no cursor
    /// yet. A week, because the alternative — reaching back to the beginning —
    /// makes a newly armed schedule's first act a full history pull nobody asked
    /// for, against a provider that will rate-limit it.
    /// </summary>
    private static readonly TimeSpan FirstRunLookback = TimeSpan.FromDays(7);

    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ITenantScopeFactory _tenantScopes = tenantScopes;
    private readonly IClock _clock = clock;
    private readonly ILogger<ScheduleWorker> _logger = logger;

    /// <summary>Reasons carried on the two job contexts, so an audit can tell them apart.</summary>
    internal const string DispatcherReason = "sync schedule dispatcher";

    internal const string RunReason = "scheduled sync";

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
#pragma warning disable CA1031 // A worker that dies on one bad feed stops every
            // dealership's syncs, including the ones that would have worked.
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
    /// One due schedule, in one dealership. Returns true when something ran, so
    /// a busy installation keeps working without waiting.
    /// </summary>
    internal async Task<bool> RunOnePassAsync(CancellationToken cancellationToken)
    {
        foreach (var slug in await ActiveTenantsAsync(cancellationToken).ConfigureAwait(false))
        {
            if (await RunNextDueAsync(slug, cancellationToken).ConfigureAwait(false))
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
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> RunNextDueAsync(string slug, CancellationToken cancellationToken)
    {
        // Nobody asked for the polling, so the dispatcher says so. Its only way
        // out is Get<TenantDb>(); a capability here would not compile.
        await using var dispatch = await _tenantScopes
            .OpenUnattendedAsync(UnattendedJob.For(slug, DispatcherReason), cancellationToken)
            .ConfigureAwait(false);

        if (dispatch is null)
        {
            // Suspended between listing and opening. Its feeds wait, which is
            // correct for a dealership out of service.
            return false;
        }

        var claim = await ClaimNextDueAsync(dispatch, cancellationToken).ConfigureAwait(false);
        if (claim is null)
        {
            return false;
        }

        // A second scope, opened now that there is a person to open it as.
        await using var run = await _tenantScopes
            .OpenAsync(
                JobContext.RequestedBy(slug, claim.Value.ArmedByUserId, RunReason),
                cancellationToken)
            .ConfigureAwait(false);

        if (run is null)
        {
            // Suspended in the gap. Nothing was claimed beyond a due time that
            // will come round again, so there is nothing to repair.
            return false;
        }

        try
        {
            await FireAsync(run, claim.Value.ScheduleId, cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // One feed's failure is recorded and the loop
        // carries on. A throw here would take every dealership's syncs down.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            SyncFailed(_logger, claim.Value.ScheduleId, run.Tenant.Key, ex);
        }

        return true;
    }

    /// <summary>What the dispatcher learned: which schedule, and whose authority.</summary>
    private readonly record struct Claim(Guid ScheduleId, Guid ArmedByUserId);

    /// <summary>
    /// Take the earliest due schedule, or return null when there is none — or
    /// when another instance took it first.
    /// </summary>
    private async Task<Claim?> ClaimNextDueAsync(
        UnattendedScope dispatch,
        CancellationToken cancellationToken)
    {
        var db = dispatch.Get<TenantDb>();
        var now = _clock.UtcNow;

        var due = await db.Set<ConnectorSchedule>()
            .AsNoTracking()
            .Where(s => s.State == ScheduleState.Armed && s.NextRunAt != null && s.NextRunAt <= now)
            .OrderBy(s => s.NextRunAt)
            .Select(s => new { s.Id, s.ArmedByUserId, s.NextRunAt, s.IntervalMinutes })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (due is null)
        {
            return null;
        }

        // The claim. Two instances share this database, so whoever moves
        // NextRunAt first owns the run and the other's update matches no row.
        // Matching on the value read is what makes it conditional — matching on
        // the id alone would let both through.
        var claimed = await db.Set<ConnectorSchedule>()
            .Where(s => s.Id == due.Id
                && s.State == ScheduleState.Armed
                && s.NextRunAt == due.NextRunAt)
            .ExecuteUpdateAsync(
                set => set.SetProperty(s => s.NextRunAt, now.AddMinutes(due.IntervalMinutes)),
                cancellationToken)
            .ConfigureAwait(false);

        return claimed == 0 ? null : new Claim(due.Id, due.ArmedByUserId);
    }

    /// <summary>
    /// Run one schedule inside the arming person's scope. Everything that
    /// decides whether a record may be written happens below this, in the
    /// capability that owns it.
    /// </summary>
    private async Task FireAsync(
        TenantScope scope,
        Guid scheduleId,
        CancellationToken cancellationToken)
    {
        var db = scope.Services.GetRequiredService<TenantDb>();

        var schedule = await db.Set<ConnectorSchedule>()
            .SingleOrDefaultAsync(s => s.Id == scheduleId, cancellationToken)
            .ConfigureAwait(false);

        if (schedule is null)
        {
            // Deleted between the claim and the run. Nothing to do and nothing
            // wrong.
            return;
        }

        // The grant, re-checked now rather than trusted from arming time. See
        // the header — this is what makes the authority answerable.
        var access = scope.Services.GetRequiredService<IAccessDirectory>();

        var allowed = await access
            .IsAuthorizedAsync(
                schedule.ArmedByUserId,
                Permissions.MigrationImport,
                schedule.RooftopId,
                cancellationToken)
            .ConfigureAwait(false);

        if (!allowed)
        {
            // Suspended, not skipped. A feed that has quietly stopped because
            // somebody changed roles three months ago is the kind of thing found
            // by a reconciliation, long after the gap matters.
            schedule.Suspend(
                "The person who armed this no longer has permission to import records for this rooftop.");

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var connector = scope.Services
            .GetServices<IConnector>()
            .FirstOrDefault(c => string.Equals(
                c.Manifest.Provider, schedule.Connector, StringComparison.OrdinalIgnoreCase));

        if (connector is null)
        {
            // The build no longer ships this connector. Non-transient.
            schedule.Suspend($"This build does not ship a connector called '{schedule.Connector}'.");
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var capability = connector.Manifest.Capability(schedule.Contract, schedule.Version);

        if (capability is null)
        {
            schedule.Suspend(
                $"'{schedule.Connector}' no longer offers '{schedule.Contract}' v{schedule.Version}.");

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var protector = scope.Services.GetRequiredService<ISecretProtector>();
        var settings = ConnectorSettings.Reveal(connector.Manifest, schedule.Settings, protector);

        var runtime = scope.Services.GetRequiredService<ConnectorRuntime>();

        var run = await runtime
            .RunAsync(connector, schedule.RooftopId, capability, settings, FirstRunLookback, cancellationToken)
            .ConfigureAwait(false);

        schedule.Recorded(run.Id, run.Outcome, _clock.UtcNow);

        if (run.Outcome == RunOutcome.Misconfigured)
        {
            // Settings that no longer validate, or a contract with no sink.
            // Neither improves by being tried again in fifteen minutes.
            schedule.Suspend(
                $"The last run could not start: {run.FailureCode}. Fix the configuration and arm it again.");
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
