# Tenant and Rooftop Resolution

Specified in [04 — Data & Tenancy](../04-Data-and-Tenancy.md).

**The tenant is resolved before the user, not after.** Sessions and user records
live in the *tenant's own* database, so there is nowhere to look a caller up
until the dealer organization is known. Every step below runs before any
endpoint does.

```mermaid
sequenceDiagram
    autonumber
    participant B as Browser
    participant T as TenantMiddleware
    participant H as Host catalog
    participant U as CurrentUserMiddleware
    participant F as AntiForgeryMiddleware
    participant E as Endpoint
    participant A as Authorization
    participant D as Dealer organization DB

    B->>T: request + session cookie + tenant key
    T->>H: resolve tenant, and is it Active?
    H-->>T: encrypted connection reference
    Note over T: decrypt into ITenantContext.<br/>TenantDb for this request now binds to it.<br/>Unknown or suspended → refused here.
    T->>U: tenant known
    U->>D: validate session against the database
    Note over U: checked every request, so revoking<br/>a session takes effect immediately.<br/>Owes a second factor → enrolment path only.
    U->>F: caller known
    Note over F: a write must echo the anti-forgery<br/>token. A read passes straight through.
    F->>E: authorized to proceed
    E->>A: may this user do X, at this rooftop?
    A-->>E: allowed rooftop set
    E->>D: scoped query or write
    E-->>B: response, or a Problem Details refusal
```

A request resolves exactly one dealer organization. It reaches one or several
rooftops only where the user has explicit scope.

The isolation is not a filter anyone has to remember. `TenantDb` is constructed
per request from the connection string resolved in step 2, so a query
**physically cannot** address a second organization's database — which is what
`deploy/verify-e2e.ps1` proves end to end.

Work that is not a request — a background import — has no ambient tenant and
must name one through `ITenantScopeFactory`. There is deliberately no "current"
tenant outside a request.
