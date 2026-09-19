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
