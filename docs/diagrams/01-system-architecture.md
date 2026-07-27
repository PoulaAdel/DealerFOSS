# System Architecture

Specified in [02 — Architecture & Decisions](../02-Architecture-and-Decisions.md).

```mermaid
flowchart TB
    Browser["React browser"]
    subgraph App["OpenDealer360 modular monolith"]
        Host["Host / BFF<br/>auth · API · jobs · composition"]
        Modules["Business modules<br/>Organization · Customers · Inventory · CRM<br/>Sales · Finance · Service · Documents · Reporting"]
        Edge["Integration edge<br/>contracts · connectors · inbox/outbox · reconciliation"]
        Core["Core + Tenancy<br/>shared types · tenant routing"]
    end
    HostDB[("Host catalog<br/>tenant routing only")]
    TenantDB[("Dealer organization DB<br/>one or many rooftops · operational data · rpt")]
    Docs[("Document store")]
    Redis[["Redis<br/>optional; required for scale-out"]]
    External["DMS and provider APIs"]

    Browser -->|HTTPS / secure session| Host
    Host --> Modules
    Host --> Edge
    Modules --> Core
    Edge --> Core
    Host --> HostDB
    Modules --> TenantDB
    Edge --> TenantDB
    Core --> Docs
    Core -. scale-out .-> Redis
    Edge <-->|versioned contracts| External
```

One tenant is one dealer organization. Rooftops are scoped business units inside its database, not separate tenants.
