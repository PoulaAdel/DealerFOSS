# Tenant and Rooftop Resolution

Specified in [04 — Data & Tenancy](../04-Data-and-Tenancy.md).

```mermaid
sequenceDiagram
    autonumber
    participant B as Browser
    participant S as Session/Auth
    participant T as Tenant middleware
    participant H as Host catalog
    participant A as Authorization
    participant D as Dealer organization DB
    participant E as Endpoint

    B->>S: Request + secure session
    S->>T: authenticated tenant and user
    T->>H: resolve active tenant database/version
    H-->>T: connection reference
    T->>D: create scoped tenant context
    T->>A: load rooftop/department assignments
    E->>A: authorize action + requested resource scope
    A-->>E: allowed rooftop set
    E->>D: scoped query/write
    E-->>B: response
```

A request resolves one dealer organization. It may access one or several rooftops only when the user has explicit scope.
