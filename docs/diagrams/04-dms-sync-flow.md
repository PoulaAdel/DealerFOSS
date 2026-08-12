# Integration Flow

Specified in [05 — Integration Framework](../05-Integration-Framework.md).
Dashed and dimmed means designed but not built — see the
[reading note](README.md#reading-them).

## What a run does today

`ConnectorRuntime.RunAsync` reads one dealership's feed. The cursor rules are the
reason this is a diagram rather than a loop.

```mermaid
sequenceDiagram
    autonumber
    participant R as ConnectorRuntime
    participant DB as Tenant DB
    participant C as Connector
    participant X as Provider
    participant S as IRecordSink

    R->>DB: insert run row, save immediately
    Note over R,DB: written BEFORE any work, so a process killed<br/>mid-run leaves FinishedAt null — the only<br/>evidence such an attempt happened.

    R->>C: validate this dealership's settings
    alt required setting missing, or no sink registered
        R->>DB: Misconfigured — provider never called
    else usable
        R->>DB: load cursor for (connector, rooftop, contract)
        Note over R: plan the window: chunk at the endpoint's limit,<br/>clamp to its lookback, shift by this dealership's<br/>settlement delay. No dates accepted → no cursor at all.

        loop each slice, in order
            R->>C: fetch slice
            C->>X: request
            X-->>C: records + what it ACTUALLY covered (often null)
            C-->>R: FetchOutcome
            R->>S: apply — idempotent on ExternalId
            S-->>R: applied, rejected, warnings
            R->>DB: quarantine each rejected record with its payload

            alt provider accounted for the window
                R->>DB: advance cursor to what was SERVED
            else silent, or served a later range
                R->>DB: hold cursor · record the reason · count the hold
                Note over R: stop here. Reading further slices would<br/>leave this one behind, unaccounted for.
            end
        end
        R->>DB: complete the run row
    end
```

**The cursor advances from what the provider reported serving, never from what
was requested.** Advancing past a period the provider never served produces no
exception, no warning and no failed run — the hole is found by a reconciliation
months later, when the source no longer holds the data. There is no louder
signal available, so the refusal is the signal.

A held cursor is **not** a failed run. Records arrived and were applied; the
position simply did not move, and the same window is asked for again next time.
That is why `IRecordSink` requires idempotency on the provider's own identifier.

## The wider design

Webhooks, a durable inbox and outbox, replay and reconciliation are specified in
doc 05 §4 and none of them exist.

```mermaid
flowchart LR
    X["Provider"] --> W["signed webhook"]
    X --> P["poll · built"]
    W --> I["durable inbox<br/>dedupe by message id"]
    P --> RT["ConnectorRuntime<br/>built"]
    I --> RT
    RT --> Q["quarantine<br/>built"]
    RT --> S["IRecordSink → owning capability<br/>port built, no implementation yet"]
    S --> O["outbox: result events"]
    Q --> RP["operator replay"]
    RT --> H["run history + cursor<br/>built"]
    REC["periodic reconciliation<br/>compare counts with the source"] -.-> X
    REC -.-> H

    classDef planned stroke-dasharray:5 4,color:#888,stroke:#888
    class W,I,O,RP,REC planned
```

**Built:** the manifest and its typed settings, window arithmetic, the cursor and
its hold rules, coercion to absence (ADR-021), quarantine with an expiry
(ADR-022), run history, and a fixture connector that misbehaves the way real
providers do — 55 tests across the unit and SQL-backed suites.

**Not built:** any real provider connector, any `IRecordSink` implementation
(so a real deployment reports every run `Misconfigured`), the scheduler that
would start a run, webhooks, the inbox and outbox, replay, reconciliation, the
quarantine purge, and outbound writes.

Deletes will use tombstones and outbound writes will carry an origin ID to
prevent loops. Both are specified; neither is written.
