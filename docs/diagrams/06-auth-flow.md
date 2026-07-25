# Authentication and Scope Flow

Specified in [06 — Security & API](../06-Security-and-API.md).

```mermaid
sequenceDiagram
    autonumber
    participant B as Browser
    participant BFF as Host/BFF
    participant Id as Identity or OIDC
    participant S as Durable sessions
    participant A as Authorization
    participant E as Tenant endpoint

    B->>BFF: login
    BFF->>Id: authenticate + required MFA
    Id-->>BFF: identity and authentication level
    BFF->>S: create rotated, bounded session
    BFF-->>B: Secure HttpOnly SameSite cookie
    B->>BFF: request + cookie + CSRF token for write
    BFF->>S: validate active session
    BFF->>A: action + tenant + rooftop/resource
    A-->>BFF: allow or deny
    BFF->>E: authorized request
    E-->>B: response
```

Global administration does not imply tenant-data access. Support access creates a separate, approved, time-limited, visible, and audited tenant session.
