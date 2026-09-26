# Integrations — the edge where somebody else's data comes in

Integrations adapt external systems. They own no dealership rules: everything
they bring in is applied through the owning capability's contract, and
`FeatureBoundaryTests` fails the build if a connector reaches for another
capability's entity.

This is a feature folder inside `src/App`, not a fourth project
([ADR-017](../../../docs/adr/0017-three-projects-flat-features.md)). Its own
files are flat; `Connectors/` is the one subfolder, because
[ADR-011](../../../docs/adr/0011-one-folder-per-connector.md) gives each provider
a folder of its own.

## The four rules this folder exists to hold

Everything here is a mechanism for one of these. They are unusual rules and they
all look like over-engineering until the day they don't, so each one says what it
is preventing.

### 1. A cursor moves only across what the provider actually served

`FetchWindow.Plan` turns a wanted period into the requests an endpoint will
accept — chunked at its limit, clamped to its lookback, shifted by this
dealership's settlement delay. `Cursor.Advance` then moves the cursor from
`FetchOutcome.Covered`, which is **what came back**, and never from what was
asked for.

`Covered` is nullable because "the provider did not say" is a real and common
answer — some delta endpoints take no date parameters at all and decide what
"recent" means for themselves. When it is null the cursor does not move and the
same window is asked for again.

> **What this prevents.** A cursor advanced past a period the provider never
> served produces no exception, no warning, and no failed run. The hole is found
> by a reconciliation months later, when the provider no longer holds the data.
> There is no louder signal available, so the refusal *is* the signal.

### 2. A value that does not fit becomes absent, never a substitute

`Coerce` returns a `FieldValue<T>` that is either present or absent-with-a-reason,
and there is deliberately no way to read a default out of an absent one.

> **What this prevents.** The obvious coercion pass returns `0` for an
> unreadable amount and a fixed date for an unreadable date, so the caller always
> has something to write. A substituted `0` is a plausible sale amount; a
> sentinel date gives a whole dealership one delivery date. Both survive every
> downstream check. See
> [ADR-021](../../../docs/adr/0021-coerced-values-become-absent.md).

Truncation is the single exception, for free text only. `Coerce.Text(…,
isKey: true)` refuses instead — a truncated key matches the *wrong* record rather
than failing.

### 3. Position is not a join key

`ColumnSet.Align` checks that provider arrays only aligned by index have the same
length, and quarantines the record when they do not.

> **What this prevents.** Reading three operation codes against two descriptions
> gives one labour line another line's hours, and the record looks entirely
> reasonable afterwards. Where both sides carry a real key, join on it and do not
> use `ColumnSet` at all.

### 4. A poll deadline is not a retry count

`PollBudget` is spent by wall-clock time. `ProviderTiming` keeps the poll
deadline and the transport retry budget as separate settings, because a slow job
and a flapping server are different problems.

> **What this prevents.** Five attempts is fifty seconds against a provider that
> says "check back in 10" and fifty minutes against one that says "600" — a
> timeout the client invented while the job was running perfectly well. Reaching
> the deadline returns `StillRunningAtProvider`, a state to report rather than a
> failure to raise.

## The runtime

`ConnectorRuntime.RunAsync` is where the four rules stop being checkable and
start being enforced. For one dealership's feed it loads the cursor, plans the
window from it, fetches each slice, hands the records to the capability that
owns them, quarantines what will not apply, advances the cursor only from what
was served, and writes down what happened.

Two orderings in it are load-bearing and neither is the obvious one:

- **The run row is saved before any work.** A process killed mid-fetch leaves a
  row with `FinishedAt` null, and that unfinished row is the only evidence the
  attempt happened. One tidy row written at the end would make a crash
  indistinguishable from a night that never ran.
- **Slices advance the cursor one at a time, in order, and the first slice that
  cannot account for itself stops the loop.** Fetching the rest would leave the
  cursor behind a period that had already been read.

A held cursor is not a failed run. Records arrive, records apply, and the
position stays put — `ConnectorRun.CursorHeld` is deliberately separate from
`Outcome`, and `ConnectorCursor.ConsecutiveHolds` is the number that turns an
ordinary event into a reportable one. One hold is a Tuesday; six in a row is a
dealership quietly falling behind.

**An endpoint that takes no dates keeps no cursor at all.** There is no position
to hold when the provider decides what "recent" means and never says, and a row
that was permanently held would read as a fault rather than as the normal shape
of a delta feed. Such a feed depends entirely on the sink being idempotent.

### Records reach a capability through `IRecordSink`, never directly

Integrations cannot see `Deal` or `Customer` — `FeatureBoundaryTests` fails the
build on the reference. A capability implements `IRecordSink` for the contract it
owns and registers it; the runtime finds it by contract and version, and refuses
the run as `Misconfigured` when nothing is registered, **before calling the
provider**. Spending a rate limit to throw the answer away looks like a working
integration, which is worse than a failure.

**One obligation on an implementer: applying must be idempotent on
`ExternalId`.** A held cursor means the same records arrive again tomorrow, by
design and routinely. Saving is fine — the runtime opens a transaction around the
whole run, so a sink's `SaveChangesAsync` flushes without committing.

`src/App/Customers/CustomerRecordSink.cs` is the worked example. It guards every
insert with an external-reference lookup, reports an existing record as
`Unchanged` rather than `Applied`, and quarantines what it cannot map.

### A record arrives in contract vocabulary, not the provider's

`ProviderRecord.Fields` is keyed by **contract** field names — see
`ContractFields.cs`. Translating from the provider's own names is the connector's
job, done once at the edge.

> **What this prevents.** If a sink read raw provider names it would need one
> mapping per provider, per capability. That is the multiplication that makes an
> integration layer collapse at about the fourth provider, and it is invisible
> until then.

### An integration run happens on behalf of a named person

`ConnectorRuntime` refuses to start without an authenticated caller, and the
records it writes are subject to that person's permissions — the same rule the
CSV import worker follows. There is deliberately no system principal: an
integration that could write records nobody is accountable for would be the one
path into this application leaving no name on the audit trail.

**This still holds when nobody is at a keyboard**, and `ConnectorSchedule` is how
(ADR-028). A schedule names the person who armed it; the polling is unattended and
the run is not. `ScheduleWorker` opens an `UnattendedScope` to find and claim a due
row — where `Get<T>` reaches `TenantDb` and a capability would not compile — then
opens a second scope as that person to run it. The worker writes no dealership
record: it decides *when*, and the capability that owns the record still decides
*whether*.

**The grant is re-read on every fire, not trusted from arming time.** Somebody
arms a nightly sync and three months later changes roles. Without the re-check the
feed keeps writing on a grant that no longer exists, and the audit trail records a
former employee importing records every night. Such a schedule is **suspended with
a reason**, because a feed that has quietly stopped is found by a reconciliation
long after the gap matters.

> **Why the re-check is in the attended scope.** It needs `IAccessDirectory`, which
> is permission-checked and must never carry `IUnattendedSafe`. Doing it in the
> dispatcher would have meant widening that allow-list — which is exactly the
> argument the allow-list exists to force somebody to make out loud.

## Settings are declared, one field at a time

`ConnectorManifest` declares every per-dealership setting with a name, a kind and
whether it is required, and `ValidateSettings` runs when configuration is saved.
A required setting that is missing or blank fails **that dealership, loudly**; a
setting the manifest does not declare is refused rather than ignored, because a
misspelled key that silently does nothing is the same failure found later.

Never add a "delimited" setting kind. Packing four identifiers into
`sub123;dept-a;dept-b;test` is how appending a word silently moves a dealership
to the sandbox, and how the parser for it becomes the least-tested code in the
connector.

## Files

| File | Job |
|---|---|
| `IConnector.cs` | the whole surface a provider adapter presents, and `FetchOutcome.Covered` |
| `ConnectorManifest.cs` | what a connector claims, what it needs told, and setting validation |
| `FetchWindow.cs` | window arithmetic as data, and the cursor rules |
| `FieldValue.cs` | ADR-021 — coercion to absence, with the raw text kept |
| `ProviderShape.cs` | parallel arrays, and fixed-arity slots |
| `PollBudget.cs` | waiting on an accepted job, measured in time |
| `IRecordSink.cs` | how a record reaches the capability that owns it |
| `ContractFields.cs` | the field vocabulary a contract is spoken in |
| `ConnectorRuntime.cs` | the run: plan, fetch, apply, quarantine, advance, record |
| `ConnectorCursor.cs` | how far a feed has been read, and why it stopped |
| `ConnectorRun.cs` | per-dealership run history |
| `ConnectorSchedule.cs` | the standing instruction a run starts from, and whose authority it carries |
| `ScheduleWorker.cs` | the unattended trigger: claim a due feed, run it as the person who armed it |
| `ConnectorSettings.cs` | a dealership's settings at rest, with declared secrets protected |
| `QuarantinedRecord.cs` | what was held back, with the payload and an expiry |
| `IntegrationTables.cs` | how all three are stored |
| `IntegrationErrors.cs` | the stable refusal codes |
| `Connectors/Fixture/` | a provider that misbehaves the way real ones do |

## What this is not

Named rather than hidden, because a half-built edge that looks finished is worse
than one that says where it stops. **Nothing here talks to a network yet.**

- **No real connector.** `Connectors/Fixture` is the only one, it fabricates its
  records, and a passing conformance suite means *fixture-tested* and nothing
  more (doc 05 §3).
- **Only one sink exists.** `Customers` v1 receives records; `Deals` and
  `Service` are declared by the fixture and have nowhere to go, so a run against
  either reports `Misconfigured`.
- **A sink cannot update, only insert.** An existing customer is reported
  `Unchanged` and left alone, because field ownership — who wins when a provider
  and a member of staff disagree about a phone number — is undecided (doc 05 §4).
  Overwriting somebody's correction with stale provider data would be worse than
  doing nothing.
- **Nothing pushes.** A run starts on a clock or not at all. There is no webhook
  path and no durable inbox, so doc 05 §4 step 1 is half-open and step 2 is
  untouched. `ScheduleWorker` closed the older "nothing calls the runtime" gap on
  2026-09-26; the sentence saying no scheduler existed stood here until then.
- **No quarantine purge.** `QuarantinedRecord.ExpiresAt` is enforced on *read*,
  so an expired row stops being listed, but nothing deletes it from the table.
- **No lease.** The claim on a due schedule is a conditional update on one row,
  which stops two application instances firing the same feed. It says nothing
  about a provider's per-tenant limits, which is what doc 05 §4 means by a poll
  lease.
- **No cron, no time-of-day, no backoff.** A schedule is an interval with a
  five-minute floor. A failed run waits one interval; a run that could not start
  suspends instead.
- **No credential storage.** `SettingKind.Secret` says how a setting must be
  treated; nothing yet enforces it.
- **No raw capture**, though
  [ADR-022](../../../docs/adr/0022-raw-capture-is-personal-data-with-an-expiry.md)
  decides what it must do when it lands, and the quarantine payload already
  follows it.
- **No webhooks and no outbound writes.** `Slots.Fit` anticipates the fixed-arity
  refusal an outbound write will need; there is no outbound write.
