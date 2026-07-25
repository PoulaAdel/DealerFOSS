# Integration Flow

Specified in [05 — Integration Framework](../05-Integration-Framework.md).

```mermaid
sequenceDiagram
    autonumber
    participant X as External provider
    participant C as Connector
    participant I as Durable inbox
    participant M as Mapper/validator
    participant O as Owning module
    participant B as Outbox
    participant R as Reconciliation

    X->>C: poll page or signed webhook
    C->>I: persist raw envelope + source version
    C-->>X: acknowledge after durable receipt
    I->>M: process at least once
    M->>M: deduplicate · order/version · map
    alt invalid or unknown
        M->>I: quarantine with reason
    else valid
        M->>O: idempotent module command
        O->>B: commit result event atomically
        M->>I: processed; checkpoint committed page
    end
    R->>X: periodic source comparison
    R->>I: record differences / schedule replay
```

Deletes use tombstones, outbound writes use an origin ID to prevent loops, and partial failures never advance an uncommitted checkpoint.
