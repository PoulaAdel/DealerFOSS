// ConnectorRuntime — the thing that makes the cursor rules real.
//
// Use:  RunAsync(connector, rooftop, capability, settings) for one dealership's
//       feed. Returns the run record it wrote.
// Edit: two orderings in here are load-bearing and neither is the obvious one.
//
//       1. THE RUN ROW IS SAVED BEFORE ANY WORK. A process killed mid-fetch
//          leaves a row with FinishedAt null, and that unfinished row is the
//          only evidence the attempt happened. Writing one tidy row at the end
//          instead would make a crash look like a night that never ran.
//       2. SLICES ADVANCE THE CURSOR ONE AT A TIME, IN ORDER, AND THE FIRST ONE
//          THAT CANNOT ACCOUNT FOR ITSELF STOPS THE LOOP. Fetching the rest
//          would leave the cursor behind a period that had already been read,
//          so the next run re-reads across a boundary the provider has already
//          moved past.
//
//       There is no interface over this class. Nothing else in the application
//       calls it — capabilities are called BY it, through IRecordSink — and an
//       interface with one implementation and no caller is ceremony.
//
//       Exceptions are not caught here. A thrown fetch leaves the run row open,
//       which is the correct evidence, and deciding whether the other
//       dealerships carry on is the scheduler's job. There is no scheduler yet,
//       so inventing that policy here would be guessing.

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using DealerFOSS.Core;
using DealerFOSS.Data;

namespace DealerFOSS.Integrations;

/// <summary>Runs one dealership's feed and records what happened.</summary>
public sealed class ConnectorRuntime(TenantDb db, IEnumerable<IRecordSink> sinks, IClock clock)
{
    private static readonly JsonSerializerOptions PayloadFormat = new(JsonSerializerDefaults.Web);

    private readonly TenantDb _db = db;
    private readonly IReadOnlyList<IRecordSink> _sinks = [.. sinks];
    private readonly IClock _clock = clock;

    /// <summary>
    /// Read one dealership's feed from wherever its cursor stands.
    /// </summary>
    /// <param name="firstRunLookback">
    /// How far back to start when this dealership has no cursor yet. Only used
    /// once; afterwards the stored position decides.
    /// </param>
    public async Task<ConnectorRun> RunAsync(
        IConnector connector,
        RooftopId rooftopId,
        ConnectorCapability capability,
        IReadOnlyDictionary<string, string?> settings,
        TimeSpan firstRunLookback,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connector);
        ArgumentNullException.ThrowIfNull(capability);

        var startedAt = _clock.UtcNow;
        var provider = connector.Manifest.Provider;

        // Saved immediately, before anything can fail. See the header.
        var run = ConnectorRun.Started(Guid.NewGuid(), provider, rooftopId, capability, startedAt);
        _db.Set<ConnectorRun>().Add(run);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var configured = connector.Manifest.ValidateSettings(settings);
        if (configured.IsFailure)
        {
            return await MisconfiguredAsync(run, configured.Error, cancellationToken).ConfigureAwait(false);
        }

        var sink = _sinks.FirstOrDefault(s =>
            string.Equals(s.Contract, capability.Contract, StringComparison.OrdinalIgnoreCase)
            && s.Version == capability.Version);

        if (sink is null)
        {
            var missing = IntegrationErrors.NoSinkRegistered(capability.Contract, capability.Version);
            return await MisconfiguredAsync(run, missing, cancellationToken).ConfigureAwait(false);
        }

        var cursor = await LoadCursorAsync(provider, rooftopId, capability, startedAt, firstRunLookback, cancellationToken)
            .ConfigureAwait(false);

        // A cursor is meaningless for an endpoint that takes no dates: it decides
        // what "recent" means and never says, so there is no position to hold.
        // Such a feed relies entirely on the sink being idempotent, which is the
        // first obligation IRecordSink states.
        var wanted = new DateRange(cursor?.Position ?? startedAt - firstRunLookback, startedAt);
        var plan = FetchWindow.Plan(capability.Window, wanted, startedAt);

        var applied = 0;
        var quarantined = 0;
        var warnings = 0;
        DateTimeOffset? servedFrom = null;
        DateTimeOffset? servedTo = null;

        foreach (var slice in plan.Slices)
        {
            var fetched = await connector
                .FetchAsync(capability, settings, slice, cancellationToken)
                .ConfigureAwait(false);

            if (fetched.IsFailure)
            {
                run.Failed(_clock.UtcNow, fetched.Error.Code);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return run;
            }

            var outcome = fetched.Value;
            warnings += outcome.Warnings.Count;

            var applyResult = await sink
                .ApplyAsync(rooftopId, outcome.Records, cancellationToken)
                .ConfigureAwait(false);

            if (applyResult.IsFailure)
            {
                run.Failed(_clock.UtcNow, applyResult.Error.Code);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return run;
            }

            applied += applyResult.Value.Applied;
            warnings += applyResult.Value.Warnings.Count;
            quarantined += Quarantine(provider, rooftopId, capability, applyResult.Value.Rejected);

            if (cursor is null)
            {
                // Dateless endpoint. Nothing to advance, nothing to hold.
                continue;
            }

            var advanced = cursor.Advance(outcome.Covered, _clock.UtcNow);
            if (advanced.IsFailure)
            {
                // The records are kept — they arrived and they are real. What
                // stops is reading any further, because the next slice would
                // leave this one behind unaccounted for.
                run.HeldCursor(advanced.Error.Code);
                break;
            }

            servedFrom ??= outcome.Covered?.Start;
            servedTo = outcome.Covered?.End ?? servedTo;
        }

        DateRange? served = servedFrom is { } from && servedTo is { } to ? new DateRange(from, to) : null;
        run.Completed(_clock.UtcNow, applied, quarantined, warnings, served);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return run;
    }

    /// <summary>
    /// The most recent runs for a dealership, newest first — the answer to "has
    /// this feed been failing all week?".
    /// </summary>
    public Task<List<ConnectorRun>> RecentRunsAsync(RooftopId rooftopId, int take, CancellationToken cancellationToken) =>
        _db.Set<ConnectorRun>()
            .AsNoTracking()
            .Where(r => r.RooftopId == rooftopId)
            .OrderByDescending(r => r.StartedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Records still waiting for somebody, for one dealership.
    /// </summary>
    /// <remarks>
    /// Filters on the expiry as well as the resolution, so a row past its
    /// retention stops appearing whether or not anything has deleted it yet
    /// (ADR-022). Nothing purges them physically — that is named in the README
    /// rather than implied by this filter.
    /// </remarks>
    public Task<List<QuarantinedRecord>> OpenQuarantineAsync(RooftopId rooftopId, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        return _db.Set<QuarantinedRecord>()
            .AsNoTracking()
            .Where(q => q.RooftopId == rooftopId && q.ResolvedAt == null && q.ExpiresAt > now)
            .OrderBy(q => q.QuarantinedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Where a dealership's feed currently stands, or null if it has never run.</summary>
    public Task<ConnectorCursor?> CursorAsync(
        string connector,
        RooftopId rooftopId,
        string contract,
        int version,
        CancellationToken cancellationToken) =>
        _db.Set<ConnectorCursor>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Connector == connector
                    && c.RooftopId == rooftopId
                    && c.Contract == contract
                    && c.Version == version,
                cancellationToken);

    private async Task<ConnectorCursor?> LoadCursorAsync(
        string provider,
        RooftopId rooftopId,
        ConnectorCapability capability,
        DateTimeOffset now,
        TimeSpan firstRunLookback,
        CancellationToken cancellationToken)
    {
        if (!capability.Window.AcceptsDates)
        {
            return null;
        }

        var existing = await _db.Set<ConnectorCursor>()
            .FirstOrDefaultAsync(
                c => c.Connector == provider
                    && c.RooftopId == rooftopId
                    && c.Contract == capability.Contract
                    && c.Version == capability.Version,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return existing;
        }

        var created = new ConnectorCursor(
            Guid.NewGuid(), provider, rooftopId, capability.Contract, capability.Version, now - firstRunLookback);

        _db.Set<ConnectorCursor>().Add(created);
        return created;
    }

    private int Quarantine(
        string provider,
        RooftopId rooftopId,
        ConnectorCapability capability,
        IReadOnlyList<RejectedRecord> rejected)
    {
        foreach (var item in rejected)
        {
            _db.Set<QuarantinedRecord>().Add(new QuarantinedRecord(
                Guid.NewGuid(),
                provider,
                rooftopId,
                capability,
                item.Record.ExternalId,
                item.Record.ExternalVersion,
                JsonSerializer.Serialize(item.Record.Fields, PayloadFormat),
                item.Reason,
                _clock.UtcNow));
        }

        return rejected.Count;
    }

    private async Task<ConnectorRun> MisconfiguredAsync(ConnectorRun run, Error reason, CancellationToken cancellationToken)
    {
        run.Misconfigured(_clock.UtcNow, reason.Code);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return run;
    }
}
