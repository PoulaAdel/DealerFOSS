# 05 — Integration Framework

← [Data & Tenancy](04-Data-and-Tenancy.md) · Next: [Security & API](06-Security-and-API.md)  
Visual: [Integration flow](diagrams/04-dms-sync-flow.md)

## 1. Boundary and layout

Integrations adapt external systems; they do not own dealership rules.

```text
src/App/Integrations/
├── (manifest, capability interfaces, contracts, and runtime — flat)
└── Connectors/
    ├── CdkFortellis/
    ├── Tekion/
    └── ...
```

Integrations is a feature folder inside `src/App`, not a fourth project ([ADR-017](adr/0017-three-projects-flat-features.md)). Its own files stay flat and are named by role; `Connectors/` is the one subfolder, because [ADR-011](adr/0011-one-folder-per-connector.md) gives each provider a folder and a dozen providers is genuinely too many files to scan.

Each connector folder may contain authentication, transport, vendor DTOs, mappings, capability adapters, fixtures, and tests. This is intentionally clearer than one very large vendor file — and one very large vendor file is what an integration becomes if nobody decides otherwise first.

## 2. Versioned contracts

Contracts are capability-specific snapshots or events, not five universal entities. Every message contains:

```text
MessageId
ContractName + Version
SourceSystem + SourceDealerId
ExternalEntityId + ExternalVersion
OccurredAt + ObservedAt
IsDeleted
Provenance / source fields
Payload
MappingWarnings
```

Initial contract families cover:

- party/customer identity, contact points, addresses, relationships, and consent;
- vehicle identity/specification and inventory unit/location/status/pricing/cost;
- deal parties, vehicle/trade, itemized prices/fees/taxes/products, status, dates, funding summary;
- appointment, RO, concerns, estimates/authorization, labor/part/sublet lines, totals, technicians, warranty;
- finance application/decision, lender, terms, products, contract, funding, cancellation;
- reference/code mappings used by each provider.

Enumerated code systems replace free-form status strings. Unknown provider values are retained and quarantined or mapped with an explicit warning; they never silently become a default.

### A value that does not fit becomes absent, never a substitute

Providers send a 300-character address into a 100-character field, a model year of `0`, an amount outside any plausible range, and a date in 1899. Every value is checked against its target before a batch is written, because one bad field must not fail a ten-thousand-row night — that much is settled.

**What the check may do is narrow.** A value that does not fit is recorded as **absent**, and the raw text is preserved in `MappingWarnings` beside the reason. It is never replaced with `0`, an empty string, a clamped bound, or a sentinel date.

This is not fussiness. A substituted `0` is a plausible sale amount and a sentinel date is a plausible delivery date, so both survive every downstream check, appear in reports, and are indistinguishable from fact by the time anyone asks. Absent is visibly missing and can be chased. Truncation is the single exception, permitted only where the field is free text and the loss is recorded as a warning — never for an identifier, a code, or anything a later lookup will be keyed on.

Corrections are counted per field per run and surfaced with the run, not written once per row into a log nobody reads.

### Positional alignment is not a join

Providers commonly return parallel arrays — amounts in one, pay types in another; operation codes in one, descriptions and hours in others — with the correspondence implied by index alone. That correspondence breaks in production, silently, and the resulting record is worse than a missing one because it is confidently wrong.

Two provider collections are joined by a key present on both sides, or they are not joined. Where only one side carries a key, the dependent fields are dropped with a mapping warning that says so. Index is never a join key, and a length mismatch between two arrays quarantines the record rather than truncating to the shorter one.

Adding an optional field is compatible. Removing, changing meaning, or changing requiredness creates a new major version with a documented support window.

## 3. Connector capabilities and status

A connector manifest identifies provider, version, authentication type, supported capability/version, direction (read/write), webhook/poll support, certification state, and known limitations. The UI displays these facts.

The manifest also **declares every per-dealership setting the connector needs** — name, type, whether it is required, and how it is validated. Settings are typed fields, not delimited strings: no packing four identifiers into one semicolon-separated value, and no flag whose presence silently switches the connector to a sandbox. Configuration strings grow a grammar, the grammar goes undocumented, and the parser for it becomes the least-tested code in the connector.

Two consequences follow, and both matter more than the format:

- A setting that fails validation **blocks the save**. Bad configuration is rejected while somebody is looking at it, not at three in the morning.
- A setting missing at run time fails **that dealership, loudly**, and marks the connector broken for it in the UI. It never causes the dealership to be skipped — a store that quietly produces nothing every night can do so for months before anyone notices, and a caught-and-continued configuration error is exactly how that happens.

Status is one of:

- **Fixture-tested:** automated tests only; not a production promise.
- **Sandbox-certified:** passed vendor sandbox and failure/replay tests.
- **Production-certified:** completed dealer-approved shadow comparison and reconciliation.
- **Experimental/community:** support and limitations explicitly stated.

The coexistence release commits to one production- or sandbox-certified connector plus one repeatable file/API import path. Additional connectors are added after the runtime and reconciliation model are proven.

## 4. Correct synchronization

### Inbound

1. Authenticate and validate webhook replay window, or acquire the capability/tenant poll lease.
2. Store the raw envelope in the durable inbox before acknowledging a webhook.
3. Deduplicate by message/event ID and source entity version.
4. Validate and map. Unknown or invalid records enter quarantine with operator-visible errors.
5. Apply through the owning module contract in a transaction; record the processed inbox item and resulting outbox events.
6. Advance a poll cursor only after the full page commits, and only across the range the provider actually covered.
7. Update counts, timing, lag, warnings, and reconciliation status.

Steps 4 through 7 are built, in `src/App/Integrations/ConnectorRuntime.cs`, along with the cursor, run-history and quarantine tables behind them. Steps 1 and 2 are not: there is no inbox, no webhook path and no poll lease, so the only thing that can start a run today is a caller holding a connector instance. Nothing implements `IRecordSink` either, which means step 5 has no capability to apply through and a real deployment reports every run as misconfigured. `src/App/Integrations/README.md` keeps the current list.

Two details of the built runtime are worth stating here because they are easy to get wrong later. **The run row is written before the fetch**, so a process killed mid-run leaves an unfinished row rather than no row at all. And **a held cursor is recorded separately from the run's outcome**: a run can succeed, apply every record, and still not move, which is a different fact from a failure and has to stay visible as one.

### The range requested is not the range served

A cursor is not a date. Real providers impose window arithmetic that has nothing to do with what the caller wants: a delta endpoint may take **no date parameters at all** and decide "recent" for itself; a history endpoint may cap a request at a fixed span and require chunking; a bulk endpoint may reach back only a few weeks, so history and bulk must be stitched with a deliberate overlap; a search may refuse any range older than a week and silently clamp the one it was given.

So the connector declares its window arithmetic as data — maximum chunk span, maximum lookback, whether the endpoint accepts dates at all, and where two strategies must overlap — and **every fetch reports the range it actually covered.** The cursor advances from that reported range, never from the range that was asked for.

Advancing a cursor past a period the provider never served is the most expensive failure available here: it leaves a hole, it produces no error, and reconciliation finds it months later when the records are no longer retrievable.

Where a provider can report what remains — a file count, a page total, a "records left" acknowledgement — **that answer outranks our own bookkeeping.** A client's idea of progress is a guess; the source's is a fact, and a client that restarted after a partial failure has a worse guess than it thinks.

### Overlap is deliberate, and lateness is per-dealership

Windows overlap on purpose: a daily run asks for several days, and deduplication is what makes that safe. Overlap is also the only reason late-posted paperwork ever arrives, because a same-day request for a dealership that closes its deals the following morning returns nothing.

A **settlement delay** is therefore configured per connector *and* per dealership, not per provider alone. Dealerships on the same DMS post at different speeds, and a lookback tuned to the fastest one under-collects from every other.

### A poll deadline is not a retry count

Asynchronous provider jobs — request accepted, poll a status link, then follow a second link to the result — are the normal path for bulk reads, not an exception. Two settings govern them and they are not the same setting:

- a **retry budget** with backoff, for transport failures, respecting `Retry-After`;
- a **poll deadline** in wall-clock time, for how long we will wait on a job the provider has accepted.

A fixed number of poll attempts is not a duration: the same five attempts mean fifty seconds against a provider that says "check back in ten" and fifty minutes against one that says "check back in six hundred". Reaching the poll deadline is a **reportable operational state — the job is still running at the provider** — not an error, because the correct next action is almost always to ask again later rather than to start over.

### Correctness rules

- Delivery is at least once; every effect is idempotent.
- Webhooks may be duplicated, delayed, or reordered.
- Provider timestamps are not trusted as the only ordering signal; source version/sequence is preferred.
- Deletes use explicit tombstones and the owning module decides whether deletion, inactivation, or conflict is legal.
- Field ownership defines whether DealerFOSS, the provider, or a user wins. Two-way writes carry an origin/correlation ID to prevent loops.
- Partial failures never advance beyond uncommitted data.
- A periodic reconciliation compares record counts, key totals, missing/deleted IDs, and stale records.
- Operators can inspect, replay, retry from a checkpoint, or resolve a conflict with a recorded reason.

Retries respect idempotency and provider `Retry-After`. Timeouts, backoff, and circuit-breaker thresholds are configured per provider/capability rather than imposed as one universal number.

## 5. Outbound changes

Outbound operations use a transactional outbox. A module commits its local decision and an integration command together; a worker delivers the command, records the provider response/version, and publishes success or failure. The user sees Pending, Confirmed, Rejected, or Attention Required. No UI claims success merely because a local database transaction succeeded.

**"The provider's record has no room for this" is a rejection, and it is not transient.** DMS records frequently have fixed arity rather than collections — five fee slots, three insurance slots, a numbered set of options — so a deal carrying six fees cannot be written to a provider that holds five. The connector reports which items would not fit, the operation is Rejected rather than retried, and the user is told what was left out. Retrying it forever, or writing the first five and calling it success, are both worse than refusing.

## 6. Migration and export

Migration is a first-class workflow:

1. acquire and hash source extracts;
2. stage raw rows unchanged;
3. profile completeness, codes, duplicates, and financial/control totals;
4. configure versioned mappings and identity-match rules;
5. run repeatable trial conversions;
6. resolve exceptions without editing raw source;
7. reconcile counts, totals, documents, and relationships;
8. capture final deltas and execute a timed cutover;
9. obtain business-owner sign-off and retain the report.

Export includes all normalized records, external references, audit-safe history allowed by policy, relationships, documents, and checksums. A round-trip test verifies that an export is understandable without proprietary internal knowledge.

Exports and reconciliation reports are **published atomically**: written to a partial name and renamed only once the last record is committed, removed if the run fails. A crash then leaves either a complete artefact or none — never a truncated file that the next job reads as finished.

## 7. Conformance tests

Every connector is tested for:

- manifest/capability accuracy and contract validation;
- auth renewal and credential failure;
- paging, throttling, `Retry-After`, timeout, and circuit recovery;
- duplicate, reordered, deleted, and clock-skewed input;
- partial page failure, restart, cursor safety, and idempotency;
- unknown codes and mapping-loss reporting;
- webhook signature/replay defense;
- outbound loop suppression;
- reconciliation and operator replay;
- window arithmetic: a clamped or ignored date range advances the cursor only across what was served;
- a value too long, out of range, or unparseable becomes absent with a warning, never a substitute;
- two provider arrays of unequal length quarantine rather than truncate;
- a missing required setting fails that dealership loudly instead of skipping it;
- a poll deadline reached reports "still running at the provider" rather than failing the run.

Fixture tests permit merge. Only sandbox/production evidence changes certification status.

## 7a. Run history

Metrics show the shape of the last hour; they cannot answer "has this dealership been failing all week?" — and that is the question an operator actually asks, because a store producing zero rows every night looks identical to a quiet store.

Each run records, per connector and per dealership, a durable row: started, finished, records applied, records quarantined, mapping warnings, the range covered, and the failure if there was one. One dealership's failure never ends the run for the others — and never disappears with it either.

A run also records **whether the cursor moved**, separately from whether the run succeeded. The two are not the same question and the second one is the more urgent: a feed whose provider will not account for its window succeeds every night, applies records every night, and falls further behind every night. The cursor itself carries the count of consecutive holds, so the difference between an ordinary Tuesday and a store that has been stuck for a fortnight is a number rather than an inference.

## 8. Secrets and observability

Connector credentials are encrypted with the deployment key provider, redacted from logs/diagnostics, scoped per organization/provider, and rotatable without restart where feasible. Metrics include success/error rate, throttling, duration, cursor age, inbox/outbox age, quarantine count, mapping warnings, and reconciliation differences. Alerts link to connector-specific runbooks.

### Raw capture is required, and so is its expiry

An integration that cannot show what a provider actually sent cannot be operated. "Your API returned this" is unanswerable without the bytes, and every argument with a vendor comes down to them. Raw request and response capture is therefore a **named facility of the runtime**, not something a developer adds locally when a bug appears.

It is also a second copy of names, addresses, phone numbers, emails and VINs sitting outside the tenant's tables, which is why it carries obligations the metrics do not:

- **Retention is a property of the code, with a default that applies when nobody configures anything.** A retention policy that lives only in a runbook is a folder that grows for the life of the installation.
- **Capture is personal data** for every purpose, including erasure requests, export, and the tenant's storage boundary. It is not "logs".
- **Redaction covers the body, not only the header.** Masking `Authorization` while storing the full response is backwards: the credential is one line and the personal data is everything underneath it. Fields a provider requires in cleartext — a password inside a SOAP security header, for instance — are redacted before the capture is written, not after.
- Capture is **off by default for a connector at production-certified status** and enabled deliberately, per connector and dealership, with an expiry on the enabling.
