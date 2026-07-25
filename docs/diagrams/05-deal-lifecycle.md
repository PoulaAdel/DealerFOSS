# Deal Lifecycle

Specified in [01 — Vision & Scope](../01-Vision-and-Scope.md) and [06 — Security & API](../06-Security-and-API.md).

A deal has related but separate commercial, funding, title, and accounting states. “Funded” is not the end of every responsibility.

```mermaid
stateDiagram-v2
    [*] --> Draft
    Draft --> Desking
    Desking --> PendingApproval
    PendingApproval --> Desking: changes requested
    PendingApproval --> Approved
    Approved --> Contracting
    Contracting --> Delivered
    Contracting --> Unwound
    Delivered --> FundingPending
    FundingPending --> Funded
    FundingPending --> FundingException
    Funded --> Posted: accounting system accepts batch
    Posted --> Closed: title/due-bill/cancellation checks complete
    Closed --> [*]
```

Approval applies to an exact deal version. A material change returns the deal to approval. Funding, title, and accounting status are tracked independently even when the UI presents one overall progress view.
