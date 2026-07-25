# Identity module

Answers one question for the rest of the system: **may this user do this action,
at this scope?** It also owns the tenant's append-only audit trail
([doc 04 §3](../../../docs/04-Data-and-Tenancy.md),
[doc 06 §3](../../../docs/06-Security-and-API.md)).

It sits below the business modules: it knows about users, roles, and rooftops,
but nothing about deals, vehicles, or repair orders.

## Layout

| Path | Role |
|---|---|
| `Domain/` | `User`, `Role` + `RolePermission`, `UserAssignment`, `AuditEvent`, and the `Permissions` catalogue |
| `Data/` | `IdentityDbContext` (owns the `identity` schema), migrations, design-time factory |
| `Contracts/` | `IAccessDirectory` + `AuthorizedScope` — the only surface other modules use (ADR-008) |
| `AccessService.cs` | resolves what a user may reach, and audits refusals |
| `SqlAuditSink.cs` | writes `IAuditSink` entries into `identity.AuditEvents` |
| `IdentityModule.cs` | DI registration |

## How authorization works

A **permission** is a catalogued action (`Organization.Read`). A **role** holds
permissions. A **user assignment** grants a role either organization-wide or at
one named rooftop. Nothing grants a permission to a user directly, so access can
be reasoned about and revoked as a unit.

`AuthorizedScope` distinguishes *organization-wide* from *a set of rooftops* on
purpose: a rooftop added next year is automatically covered by an
organization-wide grant, and is automatically **not** covered by a rooftop grant.

**Deny is the default.** An unknown user, an inactive user, or a user with no
covering assignment gets `AuthorizedScope.None`. Callers must treat an empty
scope as a refusal — never as "no filter".

## Audit

Audit events are append-only (ADR-016). `IdentityDbContext.SaveChangesAsync`
throws if anything tries to update or delete one, so the rule is enforced by the
code rather than by convention. Corrections are new records.

Entries must never carry credentials, tokens, credit data, government
identifiers, or document content.

## Boundaries (enforced by `tests/Architecture`)

- Identity references no other business module and not the Host.
- Other modules reach it only through `Contracts`, never `Domain` or `Data`.
- The module owns the `identity` schema and reads no other module's tables.

## Not built yet

Credentials, durable sessions, MFA, OIDC federation, global-administration
separation, and time-limited support access are all later milestones. Today the
caller is supplied by a Development-only `X-User` header, which the Host refuses
outside Development. See [`docs/implementation/STATUS.md`](../../../docs/implementation/STATUS.md).

> `identity` is a reserved T-SQL keyword. EF quotes it automatically; hand-written
> SQL must bracket it as `[identity].[AuditEvents]`.
