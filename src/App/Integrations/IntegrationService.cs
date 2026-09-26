// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   IntegrationService — reading the edge, and replaying what it refused.
//
// Usage:
//   Through IIntegrations. Registered in Program.cs.
//
// Coding Instructions:
//   REPLAY RUNS THE REAL SINK. It rebuilds the ProviderRecord from the stored
//   payload and hands it to the same IRecordSink that refused it, inside a
//   transaction. There is no simulation mode and there must not be one: the
//   only replay worth having is the one that would have happened.
//
//   A replay that fails again LEAVES THE ROW IN THE QUEUE and records the new
//   reason, which is often not the old one — a mapping fixed in one place
//   frequently reveals the next problem behind it. Resolving on attempt rather
//   than on success would empty the queue without fixing anything, which is
//   the failure mode the whole quarantine exists to prevent.
//
//   The payload is PERSONAL DATA (ADR-022) and is never returned by a read.
//   QuarantineEntry carries the reason and the provider's id, not the fields;
//   a screen listing quarantined customers' names and telephone numbers would
//   be a personal-data export with a review queue painted on it. Replay reads
//   the payload server-side and it stays there.

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using DealerFOSS.Core;
using DealerFOSS.Data;
using DealerFOSS.Identity;

namespace DealerFOSS.Integrations;

public sealed class IntegrationService(
    TenantDb db,
    IEnumerable<IConnector> connectors,
    IEnumerable<IRecordSink> sinks,
    IAccessDirectory access,
    ICurrentUser currentUser,
    IAuditSink audit,
    ISecretProtector protector,
    IClock clock)
    : IIntegrations
{
    /// <summary>
    /// Reading the edge is an import right. Somebody who may bring records in
    /// is the person who needs to see what would not come in.
    /// </summary>
    private const string ReadPermission = Permissions.MigrationImport;

    private static readonly JsonSerializerOptions PayloadFormat = new(JsonSerializerDefaults.Web);

    private readonly TenantDb _db = db;
    private readonly IEnumerable<IConnector> _connectors = connectors;
    private readonly IEnumerable<IRecordSink> _sinks = sinks;
    private readonly IAccessDirectory _access = access;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditSink _audit = audit;

    /// <summary>
    /// Protects a connector credential before it is stored. The one thing
    /// ISecretProtector's own header names this seam for, and the first code to
    /// use it that way.
    /// </summary>
    private readonly ISecretProtector _protector = protector;

    private readonly IClock _clock = clock;

    public async Task<Result<IReadOnlyList<ConnectorSummary>>> ConnectorsAsync(
        CancellationToken cancellationToken)
    {
        if (!await IsAllowedAsync(cancellationToken))
        {
            return Result.Failure<IReadOnlyList<ConnectorSummary>>(IntegrationErrors.Forbidden);
        }

        var summaries = _connectors
            .Select(c => c.Manifest)
            .OrderBy(m => m.Provider, StringComparer.Ordinal)
            .Select(m => new ConnectorSummary(
                m.Provider,
                m.Version,
                m.Certification.ToString(),
                m.Capabilities
                    .Select(c => $"{c.Contract} v{c.Version}")
                    .ToList(),
                m.KnownLimitations.ToList()))
            .ToList();

        return Result.Success<IReadOnlyList<ConnectorSummary>>(summaries);
    }

    public async Task<Result<IReadOnlyList<QuarantineEntry>>> QuarantineAsync(
        RooftopId? rooftopId,
        CancellationToken cancellationToken)
    {
        var scope = await _access.GetAuthorizedScopeAsync(
            _currentUser.Id, ReadPermission, cancellationToken);

        if (scope.GrantsNothing)
        {
            return Result.Failure<IReadOnlyList<QuarantineEntry>>(IntegrationErrors.Forbidden);
        }

        if (rooftopId is { } requested && !scope.Covers(requested))
        {
            return Result.Failure<IReadOnlyList<QuarantineEntry>>(IntegrationErrors.Forbidden);
        }

        var now = _clock.UtcNow;
        var held = _db.Set<QuarantinedRecord>()
            .AsNoTracking()
            // Unresolved and not yet expired, the same filter the runtime uses.
            // An expired row stops being visible whether or not anything has
            // deleted it (ADR-022).
            .Where(q => q.ResolvedAt == null && q.ExpiresAt > now);

        // Filtered in the query, not the results: another rooftop's held records
        // must never be read, let alone returned.
        if (!scope.IsOrganizationWide)
        {
            var allowed = scope.Rooftops.ToList();
            held = held.Where(q => allowed.Contains(q.RooftopId));
        }

        if (rooftopId is { } only)
        {
            held = held.Where(q => q.RooftopId == only);
        }

        var rows = await held
            .OrderBy(q => q.QuarantinedAt)
            .Select(q => new QuarantineEntry(
                q.Id, q.Connector, q.RooftopId, q.Contract, q.Version,
                q.ExternalId, q.ReasonCode, q.ReasonDetail, q.QuarantinedAt, q.ExpiresAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<QuarantineEntry>>(rows);
    }

    public async Task<Result<ReplayOutcome>> ReplayAsync(
        Guid quarantinedRecordId,
        CancellationToken cancellationToken)
    {
        var held = await _db.Set<QuarantinedRecord>()
            .SingleOrDefaultAsync(q => q.Id == quarantinedRecordId, cancellationToken);

        // Unknown and unauthorized answer identically, so a caller cannot probe
        // for another rooftop's held records by id.
        if (held is null)
        {
            return Result.Failure<ReplayOutcome>(IntegrationErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(
            _currentUser.Id, ReadPermission, held.RooftopId, cancellationToken))
        {
            return Result.Failure<ReplayOutcome>(IntegrationErrors.Forbidden);
        }

        if (held.ResolvedAt is not null)
        {
            return Result.Failure<ReplayOutcome>(IntegrationErrors.AlreadyResolved);
        }

        if (held.HasExpiredAt(_clock.UtcNow))
        {
            // The payload may still be on disk — nothing purges physically yet —
            // but its retention has run out and it has stopped being ours to
            // use. Replaying it would be writing a customer's details back into
            // the dealership after the day we said we would stop holding them.
            return Result.Failure<ReplayOutcome>(IntegrationErrors.QuarantineExpired);
        }

        var sink = _sinks.FirstOrDefault(s =>
            string.Equals(s.Contract, held.Contract, StringComparison.OrdinalIgnoreCase)
            && s.Version == held.Version);

        if (sink is null)
        {
            // The same refusal the runtime gives before calling a provider: no
            // sink means nothing can accept this, and saying so is better than
            // an exception from a null.
            return Result.Failure<ReplayOutcome>(IntegrationErrors.NoSink(held.Contract, held.Version));
        }

        var fields = JsonSerializer.Deserialize<Dictionary<string, string?>>(
            held.Payload, PayloadFormat) ?? [];

        var record = new ProviderRecord(held.ExternalId, held.ExternalVersion, fields);

        // The same transaction shape the runtime uses, so a sink that saves
        // flushes without committing and a refusal leaves nothing behind.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var outcome = await sink.ApplyAsync(held.RooftopId, [record], cancellationToken);

        if (outcome.IsFailure)
        {
            // Us being unable to write, not the record being wrong. The row
            // stays exactly as it was and the reason is not overwritten with
            // our own problem.
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<ReplayOutcome>(outcome.Error);
        }

        if (outcome.Value.Rejected.Count > 0)
        {
            var reason = outcome.Value.Rejected[0].Reason;

            await transaction.RollbackAsync(cancellationToken);

            // Refused again. The row stays in the queue, and the NEW reason is
            // recorded: fixing one mapping routinely reveals the next problem
            // behind it, and a screen still showing the old reason would send
            // somebody to look in the wrong place.
            held.Refuse(reason, _clock.UtcNow);
            await _db.SaveChangesAsync(cancellationToken);

            await RecordAsync(held, $"Replayed and refused again: {reason.Code}", cancellationToken);

            return Result.Success(new ReplayOutcome(false, reason.Code, reason.Message));
        }

        held.Resolve(_clock.UtcNow, "Replayed, and it applied.");
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await RecordAsync(held, "Replayed and applied", cancellationToken);

        return Result.Success(new ReplayOutcome(true, null, null));
    }

    public async Task<Result> DismissAsync(
        Guid quarantinedRecordId,
        string note,
        CancellationToken cancellationToken)
    {
        // Required, and not defaulted to "Resolved." A queue cleared without a
        // reason is indistinguishable from one nobody read.
        if (string.IsNullOrWhiteSpace(note))
        {
            return Result.Failure(IntegrationErrors.DismissalNeedsAReason);
        }

        var held = await _db.Set<QuarantinedRecord>()
            .SingleOrDefaultAsync(q => q.Id == quarantinedRecordId, cancellationToken);

        if (held is null)
        {
            return Result.Failure(IntegrationErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(
            _currentUser.Id, ReadPermission, held.RooftopId, cancellationToken))
        {
            return Result.Failure(IntegrationErrors.Forbidden);
        }

        if (held.ResolvedAt is not null)
        {
            return Result.Failure(IntegrationErrors.AlreadyResolved);
        }

        held.Resolve(_clock.UtcNow, note);
        await _db.SaveChangesAsync(cancellationToken);

        await RecordAsync(held, $"Dismissed without replay: {note.Trim()}", cancellationToken);

        return Result.Success();
    }

    // --- Schedules (ADR-028) ------------------------------------------------

    public async Task<Result<IReadOnlyList<SyncScheduleView>>> SchedulesAsync(
        RooftopId? rooftopId,
        CancellationToken cancellationToken)
    {
        var scope = await _access.GetAuthorizedScopeAsync(
            _currentUser.Id, ReadPermission, cancellationToken);

        if (scope.GrantsNothing)
        {
            return Result.Failure<IReadOnlyList<SyncScheduleView>>(IntegrationErrors.Forbidden);
        }

        if (rooftopId is { } requested && !scope.Covers(requested))
        {
            return Result.Failure<IReadOnlyList<SyncScheduleView>>(IntegrationErrors.Forbidden);
        }

        var schedules = _db.Set<ConnectorSchedule>().AsNoTracking();

        // Filtered in the query, not the results. Rooftop isolation is enforced
        // in services (ADR-014), and a list of feeds is as much a disclosure as
        // a list of records: it names another rooftop's providers and how often
        // they are read.
        if (!scope.IsOrganizationWide)
        {
            var allowed = scope.Rooftops.ToList();
            schedules = schedules.Where(s => allowed.Contains(s.RooftopId));
        }

        if (rooftopId is { } only)
        {
            schedules = schedules.Where(s => s.RooftopId == only);
        }

        var rows = await schedules
            .OrderBy(s => s.Connector)
            .ThenBy(s => s.Contract)
            .ToListAsync(cancellationToken);

        var manifests = _connectors.Select(c => c.Manifest).ToList();

        return Result.Success<IReadOnlyList<SyncScheduleView>>(
        [
            .. rows.Select(s =>
            {
                var manifest = manifests.FirstOrDefault(m =>
                    string.Equals(m.Provider, s.Connector, StringComparison.OrdinalIgnoreCase));

                return new SyncScheduleView(
                    s.Id,
                    s.Connector,
                    s.RooftopId,
                    s.Contract,
                    s.Version,
                    s.IntervalMinutes,
                    s.State.ToString(),
                    s.SuspendedReason,
                    s.ArmedByUserId,
                    s.NextRunAt,
                    s.LastRunAt,
                    s.LastOutcome,

                    // No manifest means the build stopped shipping this
                    // connector. The row is still shown — a feed that has
                    // vanished from the build is exactly what somebody needs to
                    // see — and it reports no settings rather than guessing at
                    // which of the stored values were secret.
                    manifest is null ? [] : ConnectorSettings.Describe(manifest, s.Settings));
            }),
        ]);
    }

    public async Task<Result<Guid>> ArmAsync(
        ArmSyncRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rooftopId = new RooftopId(request.RooftopId);

        // Arming is an exercise of the caller's own authority, so it is checked
        // exactly as a hand-run import would be. A schedule the caller could not
        // have run by hand must not be creatable.
        if (!await _access.IsAuthorizedAsync(
            _currentUser.Id, ReadPermission, rooftopId, cancellationToken))
        {
            return Result.Failure<Guid>(IntegrationErrors.Forbidden);
        }

        var connector = _connectors.FirstOrDefault(c => string.Equals(
            c.Manifest.Provider, request.Connector, StringComparison.OrdinalIgnoreCase));

        if (connector is null)
        {
            return Result.Failure<Guid>(IntegrationErrors.NoSuchConnector(request.Connector));
        }

        var capability = connector.Manifest.Capability(request.Contract, request.Version);

        if (capability is null)
        {
            return Result.Failure<Guid>(IntegrationErrors.NoSuchCapability(
                connector.Manifest.Provider, request.Contract, request.Version));
        }

        // Validated now rather than at three in the morning. A missing required
        // setting fails this request, loudly, for this dealership — which is the
        // rule ValidateSettings was written for.
        var configured = connector.Manifest.ValidateSettings(request.Settings);
        if (configured.IsFailure)
        {
            return Result.Failure<Guid>(configured.Error);
        }

        var existing = await _db.Set<ConnectorSchedule>()
            .AnyAsync(
                s => s.Connector == connector.Manifest.Provider
                    && s.RooftopId == rooftopId
                    && s.Contract == capability.Contract
                    && s.Version == capability.Version,
                cancellationToken);

        if (existing)
        {
            // Refused rather than replaced. The unique index would refuse it
            // anyway; answering here makes it a readable error instead of a
            // database exception.
            return Result.Failure<Guid>(IntegrationErrors.ScheduleExists);
        }

        var stored = ConnectorSettings.Protect(connector.Manifest, request.Settings, _protector);
        if (stored.IsFailure)
        {
            return Result.Failure<Guid>(stored.Error);
        }

        var schedule = ConnectorSchedule.Arm(
            Guid.NewGuid(),
            connector.Manifest.Provider,
            rooftopId,
            capability,
            stored.Value,
            _currentUser.Id,
            request.IntervalMinutes,
            _clock.UtcNow);

        if (schedule.IsFailure)
        {
            return Result.Failure<Guid>(schedule.Error);
        }

        _db.Set<ConnectorSchedule>().Add(schedule.Value);
        await _db.SaveChangesAsync(cancellationToken);

        await RecordScheduleAsync(
            schedule.Value,
            $"Armed to read {capability.Contract} v{capability.Version} from "
            + $"{connector.Manifest.Provider} every {request.IntervalMinutes} minutes",
            cancellationToken);

        return Result.Success(schedule.Value.Id);
    }

    public async Task<Result> DisarmAsync(Guid scheduleId, CancellationToken cancellationToken)
    {
        var found = await FindForWritingAsync(scheduleId, cancellationToken);
        if (found.IsFailure)
        {
            return Result.Failure(found.Error);
        }

        found.Value.Disarm();
        await _db.SaveChangesAsync(cancellationToken);
        await RecordScheduleAsync(found.Value, "Disarmed", cancellationToken);

        return Result.Success();
    }

    public async Task<Result> RearmAsync(
        Guid scheduleId,
        int intervalMinutes,
        IReadOnlyDictionary<string, string?>? settings,
        CancellationToken cancellationToken)
    {
        var found = await FindForWritingAsync(scheduleId, cancellationToken);
        if (found.IsFailure)
        {
            return Result.Failure(found.Error);
        }

        var schedule = found.Value;

        if (intervalMinutes < ConnectorSchedule.MinimumIntervalMinutes
            || intervalMinutes > ConnectorSchedule.MaximumIntervalMinutes)
        {
            return Result.Failure(IntegrationErrors.IntervalOutOfRange(
                ConnectorSchedule.MinimumIntervalMinutes, ConnectorSchedule.MaximumIntervalMinutes));
        }

        var connector = _connectors.FirstOrDefault(c => string.Equals(
            c.Manifest.Provider, schedule.Connector, StringComparison.OrdinalIgnoreCase));

        if (connector is null)
        {
            return Result.Failure(IntegrationErrors.NoSuchConnector(schedule.Connector));
        }

        if (settings is not null)
        {
            var merged = ConnectorSettings.Merge(
                connector.Manifest, schedule.Settings, settings, _protector);

            if (merged.IsFailure)
            {
                return Result.Failure(merged.Error);
            }

            // Re-validated against the merged result, not against what was sent.
            // Sending only an interval change must not be able to leave a
            // required setting blank, and validating the supplied half alone
            // would let it.
            var revealed = ConnectorSettings.Reveal(connector.Manifest, merged.Value, _protector);

            var configured = connector.Manifest.ValidateSettings(revealed);
            if (configured.IsFailure)
            {
                return Result.Failure(configured.Error);
            }

            schedule.Reconfigure(merged.Value);
        }

        // The caller's own authority, re-stated. Whoever fixes a suspended feed
        // becomes the name its runs are made under from now on — which is the
        // honest answer, since they are the one saying it should run.
        schedule.Rearm(_currentUser.Id, intervalMinutes, _clock.UtcNow);

        await _db.SaveChangesAsync(cancellationToken);
        await RecordScheduleAsync(
            schedule, $"Armed every {intervalMinutes} minutes", cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// A schedule the caller may change, or a refusal. Unknown and unauthorized
    /// answer identically, so an id cannot be used to probe another rooftop.
    /// </summary>
    private async Task<Result<ConnectorSchedule>> FindForWritingAsync(
        Guid scheduleId,
        CancellationToken cancellationToken)
    {
        var schedule = await _db.Set<ConnectorSchedule>()
            .SingleOrDefaultAsync(s => s.Id == scheduleId, cancellationToken);

        if (schedule is null)
        {
            return Result.Failure<ConnectorSchedule>(IntegrationErrors.NoSuchSchedule);
        }

        if (!await _access.IsAuthorizedAsync(
            _currentUser.Id, ReadPermission, schedule.RooftopId, cancellationToken))
        {
            return Result.Failure<ConnectorSchedule>(IntegrationErrors.NoSuchSchedule);
        }

        return Result.Success(schedule);
    }

    private Task RecordScheduleAsync(
        ConnectorSchedule schedule,
        string what,
        CancellationToken cancellationToken) =>
        _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, ReadPermission, AuditOutcome.Allowed,
                "ConnectorSchedule", schedule.Id.ToString(), schedule.RooftopId.Value, what, null, null),
            cancellationToken);

    private Task RecordAsync(QuarantinedRecord held, string what, CancellationToken cancellationToken) =>
        _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, ReadPermission, AuditOutcome.Allowed,
                "QuarantinedRecord", held.Id.ToString(), held.RooftopId.Value, what, null, null),
            cancellationToken);

    private async Task<bool> IsAllowedAsync(CancellationToken cancellationToken)
    {
        var scope = await _access.GetAuthorizedScopeAsync(
            _currentUser.Id, ReadPermission, cancellationToken);

        return !scope.GrantsNothing;
    }
}
