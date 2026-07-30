# Project Structure

Specified in [03 — Project Structure](../03-Project-Structure.md), decided in
[ADR-017](../adr/0017-three-projects-flat-features.md).

Three backend projects. Dependencies flow one way only.

```mermaid
flowchart TB
    Root["OpenDealer360/"] --> Src["src/"]
    Root --> Front["frontend/"]
    Root --> Tests["tests/"]
    Root --> Deploy["deploy/"]
    Root --> Docs["docs/"]
    Src --> Core["Core/<br/>no EF, no ASP.NET"]
    Src --> Identity["Identity/<br/>internals sealed"]
    Src --> App["App/"]
    App --> Plumbing["Tenancy/ · Data/<br/>plumbing"]
    App --> Features["Organization/ · Customers/<br/>Vehicles/ · Inventory/<br/>capabilities"]
    App -.depends on.-> Identity
    App -.depends on.-> Core
    Identity -.depends on.-> Core
```

Inside a capability, one flat folder with files named by role:

```mermaid
flowchart LR
    subgraph Customers["App/Customers/"]
        Entities["Customer.cs · ContactPoint.cs<br/>Address.cs"]
        Service["CustomerService.cs<br/>workflows + authorization"]
        Endpoints["CustomerEndpoints.cs"]
        Tables["CustomerTables.cs<br/>EF configuration"]
        Contract["ICustomers.cs<br/>what other features may call"]
    end
    Endpoints --> Service --> Entities
    Service --> Contract
    Tables --> Entities
```

Two walls are compiler-enforced — `Core` and `Identity` are separate projects.
Everything else is held by architecture tests that fail the build on a breach.

```mermaid
flowchart LR
    subgraph Compiler["Compiler wall — a breach is a security incident"]
        C1["Core: no database"]
        C2["Identity: no user or audit row<br/>except through its contracts"]
    end
    subgraph Tests["Architecture tests — everywhere else"]
        T1["no feature reaches into another"]
        T2["entities free of EF and ASP.NET"]
        T3["no feature picks its own connection"]
        T4["tenancy knows no business feature"]
    end
```
