# Authentication and Scope Flow

Specified in [06 — Security & API](../06-Security-and-API.md). Dashed and dimmed
means designed but not built — see the [reading note](README.md#reading-them).

Sessions are **durable, not stateless**: every request revalidates against the
database, so revoking one takes effect immediately rather than whenever a token
would have expired. That trade is deliberate and is the whole reason the sessions
are stored.

```mermaid
sequenceDiagram
    autonumber
    participant B as Browser
    participant P as Request pipeline
    participant T as TenantMiddleware
    participant Id as Identity (sealed)
    participant D as Tenant DB
    participant A as Authorization
    participant E as Endpoint

    B->>P: POST /auth/login + tenant key
    P->>T: resolve the dealer organization first
    Note over T: user records live in the tenant's own<br/>database — there is nowhere to look<br/>anyone up until the tenant is known.
    T->>Id: verify the password
    Id->>D: read the user, record the attempt
    alt this dealership demands a second factor
        Id-->>B: restricted session — enrolment path only
        B->>Id: TOTP code
        Id-->>B: session becomes unrestricted, no re-login
    end
    Id-->>B: dfoss_session (HttpOnly, Secure, SameSite)<br/>+ dfoss_csrf (readable)

    B->>P: request + session cookie
    P->>D: validate the session — every request
    P->>P: a write must echo dfoss_csrf as a header
    P->>E: caller established
    E->>A: may this user do X, at this rooftop?
    A-->>E: allow, or deny with a stable code
    E-->>B: response
```

The anti-forgery token is a **readable** cookie the browser copies into a header.
That is the entire mechanism: a cross-site form can send our cookies but cannot
set a header.

## Two kinds of administrator, and they are not related

Whoever runs the installation is not a dealership user and **cannot read
dealership data**. This is structural, not a permission: an architecture test
forbids control-plane code from even *referencing* `ICurrentUser`, the type every
capability uses to ask "may this person see this?".

```mermaid
flowchart LR
    Op["Operator<br/>separate cookie, own database"] --> CP["Control plane<br/>tenants · provisioning · support"]
    CP -->|"opens a grant"| G["Support access<br/>approved · time-limited · visible · audited"]
    G -->|"mints a session for the tenant's<br/>OWN support principal"| Sess["ordinary tenant session"]
    Sess --> Data["dealership data"]
    CP -.->|"never"| Data

    classDef no stroke-dasharray:5 4,color:#888,stroke:#888
    class Data no
```

Support access is not an exception to the rule. It issues a session for the
tenant's own support user, which the ordinary middleware then resolves in the
ordinary way — no administrator ever *becomes* a dealership caller. The grant is
visible to the dealership while it is open.

**Not built:** signing in with a company account (OIDC/SSO). It cannot be
honestly built or tested without a real identity provider, and a fake one would
prove nothing. Also missing: a second operator account, and any way for an
operator to recover their own access — there is nobody above them to issue a
code, so it needs a different answer entirely.
