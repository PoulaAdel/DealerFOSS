# System Architecture

Specified in [02 — Architecture & Decisions](../02-Architecture-and-Decisions.md).
Dashed and dimmed means designed but not built — see the
[reading note](README.md#reading-them).

```mermaid
flowchart TB
    Browser["React browser<br/>25 screens · 5 languages"]
    subgraph App["DealerFOSS modular monolith — one deployable application"]
        Host["Request pipeline<br/>security headers · rate limit · tenant · user · anti-forgery"]
        Modules["Business capabilities<br/>Organization · Customers · Vehicles · Inventory · Leads · Deals<br/>Finance · RepairOrders · Parts · Accounting · Documents<br/>Reporting · DataMigration · Administration"]
        Edge["Integration edge<br/>manifest · window arithmetic · cursors<br/>quarantine · run history"]
        Inbox["inbox · outbox · reconciliation"]
        Core["Core + Identity + Tenancy<br/>shared types · authentication · tenant routing"]
    end
    HostDB[("Host catalog<br/>tenant routing only")]
    TenantDB[("Dealer organization DB<br/>one or many rooftops · business data")]
    Docs[("Document store")]
    Redis[["Redis<br/>optional, required for scale-out"]]
    External["DMS and provider APIs"]

    Browser -->|HTTPS · session cookie| Host
    Host --> Modules
    Host --> Edge
    Modules --> Core
    Edge --> Core
    Edge -->|IRecordSink| Modules
    Host --> HostDB
    Modules --> TenantDB
    Edge --> TenantDB
    Core --> Docs
    Core -. scale-out .-> Redis
    Edge -.- Inbox
    Inbox -. versioned contracts .-> External

    classDef planned stroke-dasharray:5 4,color:#888,stroke:#888
    class Inbox,External planned
```

One tenant is one dealer organization. Rooftops are scoped business units inside
its database, not separate tenants.

**The integration edge holds no dealership rules.** It reaches a capability only
through `IRecordSink`, and an architecture test fails the build if it references
a capability's entities directly — a connector able to write a deal row would be
a route around every rule Deals enforces, arriving from outside the building.

**Not built:** the durable inbox and outbox, reconciliation, webhooks, and any
connection to a real provider. The edge itself — manifests, window arithmetic,
cursors, quarantine and run history — is built and tested against a fixture
connector that misbehaves on purpose.
