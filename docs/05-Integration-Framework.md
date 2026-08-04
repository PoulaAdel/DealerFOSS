# 05 — Integration Framework

← [Data & Tenancy](04-Data-and-Tenancy.md) · Next: [Security & API](06-Security-and-API.md)  
Visual: [Integration flow](diagrams/04-dms-sync-flow.md)

## 1. Boundary and layout

Integrations adapt external systems; they do not own dealership rules.

```text
src/Integrations/
├── Abstractions/             connector manifests and capability interfaces
├── Contracts/
│   ├── Common/               source, version, provenance, deletion metadata
│   ├── Customers/V1/
│   ├── Inventory/V1/
│   ├── Deals/V1/
│   ├── Service/V1/
│   └── Finance/V1/
├── Runtime/                  inbox, outbox, checkpoints, replay, reconciliation
└── Connectors/
    ├── CdkFortellis/
    ├── Tekion/
    └── ...
```

Each connector folder may contain `Auth`, `Client`, `Dtos`, `Mappings`, capability adapters, fixtures, and tests. This is intentionally clearer than one very large vendor file.

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

Adding an optional field is compatible. Removing, changing meaning, or changing requiredness creates a new major version with a documented support window.

## 3. Connector capabilities and status

A connector manifest identifies provider, version, authentication type, supported capability/version, direction (read/write), webhook/poll support, certification state, and known limitations. The UI displays these facts.

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
6. Advance a poll cursor only after the full page commits.
7. Update counts, timing, lag, warnings, and reconciliation status.

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
- reconciliation and operator replay.

Fixture tests permit merge. Only sandbox/production evidence changes certification status.

## 8. Secrets and observability

Connector credentials are encrypted with the deployment key provider, redacted from logs/diagnostics, scoped per organization/provider, and rotatable without restart where feasible. Metrics include success/error rate, throttling, duration, cursor age, inbox/outbox age, quarantine count, mapping warnings, and reconciliation differences. Alerts link to connector-specific runbooks.
