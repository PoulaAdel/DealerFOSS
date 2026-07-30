# Identity

Answers one question for the rest of the system: **may this user do this action,
at this scope?** It also owns the tenant's append-only audit trail
([doc 04 §3](../../docs/04-Data-and-Tenancy.md),
[doc 06 §3](../../docs/06-Security-and-API.md)).

It sits below every business capability: it knows about users, roles, and
rooftops, but nothing about deals, vehicles, or repair orders.

## Why this is a separate project

It is one of only two compiler-enforced walls in the repository
([ADR-017](../../docs/adr/0017-three-projects-flat-features.md)). Everything here
is `internal` except the surface listed below, so application code **cannot**
write a user row or an audit row except through `IAccessDirectory` and
`IAuthenticator`. Before this project existed, the development seeder constructed
the context directly and wrote user rows past `IAuditSink`; now that is a compile
error.

`BoundaryTests.Identity_exposes_only_its_access_and_sign_in_contracts` asserts the
exported type list, so widening it is a decision someone has to make on purpose.

## Public surface

| Type | Purpose |
|---|---|
| `IAccessDirectory`, `AuthorizedScope` | "what may this user reach?" |
| `IAuthenticator`, `IssuedSession`, `AuthErrors` | sign in, validate a session, sign out |
| `IdentityRegistration` | `services.AddIdentity()` |
| `IdentitySeeder`, `DevelopmentAccount` | Development-only account seeding |
| `Permissions` | the catalogue of action names — a shared vocabulary, not an internal |

Everything else — `IdentityDb`, `AccessService`, `Authenticator`, `SqlAuditSink`,
`User`, `Role`, `UserAssignment`, `Session`, `AuditEvent` — is `internal`.

## Layout

Flat: one file per type, named for what it is.

| File | Role |
|---|---|
| `User.cs`, `Role.cs`, `UserAssignment.cs`, `Session.cs`, `AuditEvent.cs` | the records and their rules |
| `Permissions.cs` | the catalogue; `Role.Grant` rejects anything not listed |
| `IdentityDb.cs` | owns the `identity` schema, stamps audit columns, refuses to rewrite audit history |
| `IAccessDirectory.cs`, `IAuthenticator.cs` | the public contracts |
| `AccessService.cs` | resolves what a user may reach, and audits refusals |
| `Authenticator.cs` | verifies passwords, issues and validates sessions |
| `SqlAuditSink.cs` | writes `IAuditSink` entries into `identity.AuditEvents` |
| `IdentityRegistration.cs` | DI registration |
| `IdentitySeeder.cs` | Development-only account seeding |

## How authorization works

A **permission** is a catalogued action (`Inventory.Manage`). A **role** holds
permissions. A **user assignment** grants a role either organization-wide or at
one named rooftop. Nothing grants a permission to a user directly, so access can
be reasoned about and revoked as a unit.

`AuthorizedScope` distinguishes *organization-wide* from *a set of rooftops* on
purpose: a rooftop added next year is automatically covered by an
organization-wide grant, and automatically **not** covered by a rooftop grant.

**Deny is the default.** An unknown user, an inactive user, or a user with no
covering assignment gets `AuthorizedScope.None`. Callers must treat an empty scope
as a refusal — never as "no filter".

## Sessions and credentials

Three rules hold sign-in together, and none may be relaxed for convenience:

- Every credential failure returns the same error, and a password is verified even
  when the user does not exist, so response time does not reveal which addresses
  are real.
- The session token is 256 bits of randomness; only its SHA-256 hash is stored.
  The raw value exists once, in the cookie.
- The session is checked against the database on **every** request, which is what
  makes revocation immediate. Do not cache that lookup without also solving
  revocation — that trade is the whole reason sessions are durable rather than
  stateless.

## Audit

Audit events are append-only (ADR-016). `IdentityDb.SaveChangesAsync` throws if
anything tries to update or delete one, so the rule is enforced by code rather
than by convention. Corrections are new records.

Entries must never carry credentials, tokens, credit data, government
identifiers, or document content.

## Not built yet

MFA, OIDC federation, global-administration separation, and time-limited support
access. See [`docs/implementation/STATUS.md`](../../docs/implementation/STATUS.md).

> `identity` is a reserved T-SQL keyword. EF quotes it automatically; hand-written
> SQL must bracket it as `[identity].[AuditEvents]`.
