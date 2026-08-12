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
| `ConnectorRun.cs` | the shape of per-dealership run history |
| `IntegrationErrors.cs` | the stable refusal codes |
| `Connectors/Fixture/` | a provider that misbehaves the way real ones do |

## What this is not

Named rather than hidden, because a half-built edge that looks finished is worse
than one that says where it stops. **Nothing here talks to a network yet.**

- **No real connector.** `Connectors/Fixture` is the only one, it fabricates its
  records, and a passing conformance suite means *fixture-tested* and nothing
  more (doc 05 §3).
- **No runtime.** No inbox, no outbox, no quarantine store, no replay, no
  reconciliation — doc 05 §4 describes all of these and none of them exist.
- **No persistence.** `ConnectorRun` is a shape, not a table: no EF configuration,
  no migration, no endpoint. Cursors are not stored anywhere either.
- **No credential storage.** `SettingKind.Secret` says how a setting must be
  treated; nothing yet enforces it.
- **No raw capture**, though
  [ADR-022](../../../docs/adr/0022-raw-capture-is-personal-data-with-an-expiry.md)
  decides what it must do when it lands.
- **No scheduling, no webhooks, no outbound writes.** `Slots.Fit` anticipates the
  fixed-arity refusal an outbound write will need; there is no outbound write.
