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
| `IAuthenticator`, `IssuedSession`, `AuthenticatedCaller`, `AuthErrors` | sign in, validate a session, sign out |
| `ISecurityPolicy`, `RoleSecondFactorPolicy` | read and set which roles must hold a second factor |
| `IGlobalAdministration` + its records, `AdminErrors` | control-plane sign-in, and the one door to support access |
| `IdentityRegistration` | `services.AddIdentity()`, `services.AddControlPlane()` |
| `IdentitySeeder`, `DevelopmentAccount`, `ControlPlaneSeeder` | Development-only account seeding |
| `Permissions` | the catalogue of action names — a shared vocabulary, not an internal |

Everything else — `IdentityDb`, `ControlPlaneDb`, `AccessService`,
`Authenticator`, `GlobalAdministrationService`, `SqlAuditSink`, `User`, `Role`,
`UserAssignment`, `Session`, `AuditEvent`, `Administrator`, `AdminSession`,
`SupportGrant` — is `internal`.

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

A session carries a second, independent secret: the anti-forgery token. It is
generated at sign-in, stored as a hash on the same row, and returned to the
browser in a cookie that script *can* read — the client has to read it to put it
in a header, and a header is what a cross-site request cannot forge. It is not a
credential and opens nothing on its own. `VerifyAntiForgeryAsync` is the only way
to check it, and it refuses a token belonging to a different or inactive session.

## Audit

Audit events are append-only (ADR-016). `IdentityDb.SaveChangesAsync` throws if
anything tries to update or delete one, so the rule is enforced by code rather
than by convention. Corrections are new records.

Entries must never carry credentials, tokens, credit data, government
identifiers, or document content.

## The second factor

TOTP — the six digits in Google Authenticator, Authy, 1Password, or any other app
implementing RFC 6238. Anyone may enrol; a dealer organization may also *require*
it (see below).

Signing in becomes two steps for an enrolled account. The password buys a
**challenge**, not a session: a short-lived, hashed, single-use token that grants
nothing on its own and dies after five minutes or five wrong codes. Only a valid
code exchanges it for a session.

Four details worth keeping:

- **The secret is encrypted at rest** with `ISecretProtector`, so a stolen
  database does not hand over everybody's second factor.
- **Enrolment is two-phase.** Generating a secret changes nothing about signing
  in until the user proves they can produce a code. A one-phase enrolment locks
  out anybody who mis-scans the QR.
- **Ten recovery codes** are issued once, stored only as hashes, and each works
  exactly once — a replayable code is a password with extra steps.
- **Turning it off needs a current code**, so a borrowed session cannot quietly
  strip the protection off an account.

`Totp.cs` is verified against the published RFC 6238 test vectors. That matters
more than it looks: the other implementation is on somebody's phone and cannot be
adjusted to agree with us.

### Requiring it

`Role.RequiresSecondFactor` says whether holding that role obliges the user to
have one, and `ISecurityPolicy` is how the application reads and changes it. The
rule sits on the role because "who must" is a statement about responsibility —
a new salesperson is covered the day they are hired, without anybody remembering.

`ValidateAsync` works the obligation out on **every** request, for the same
reason sessions are checked on every request: a rule that waits eight hours for
everyone to sign out is not in force. A caller who owes one still gets a real
session; `CurrentUserMiddleware` then lets them reach enrolment and nothing else.

Nobody is ever locked out by turning it on, and that is the property to protect
if this code changes. Disabling a second factor is not reachable from a
restricted session — otherwise the policy could be answered by removing the very
thing it asks for.

> HMAC-SHA1 is used because the RFC specifies it and every authenticator app
> implements only that. The analyser suppression at the call site explains why
> that is not the weakness it appears to be.

## The control plane

Whoever runs the deployment is a different kind of record in a different
database. `Administrator`, `AdminSession`, and `SupportGrant` live in the
`control` schema of the **host catalog** — never in a tenant's — and hold no
permission from the catalogue above. There is no method anywhere that turns an
administrator into an `ICurrentUser`, and an architecture test says so.

Why it is in this project rather than a folder under `src/App`: password
verification, TOTP, and session issuance must exist in exactly one place, and
this is the project the application cannot reach into. A second implementation
next to the features would be visible to all of them.

A second factor is mandatory here rather than a policy choice. An administrator
who has not enrolled gets a real session that reaches `me`, `mfa/enrol`,
`mfa/confirm`, and `logout` — and nothing else, support access included.

### Support access

The one deliberate way from operating the installation into a dealership's data.
`GrantSupportAccessAsync` mints a **tenant** session for that tenant's own
support principal: a user row with no password hash, so `CanSignIn` is false and
no credential opens it, holding a read-only role organization-wide. Four
properties are what make it a control rather than a back door, and each has a
test:

- **A written reason is required**, and blank is refused.
- **It is read-only.** Adding a write permission to `SupportPermissions` is a
  decision about what a vendor may do inside a customer's business.
- **The dealership sees it in their own audit trail**, naming the administrator
  and the stated reason. Visibility that exists only in the vendor's console is
  not visibility.
- **Ending the grant revokes the session** in the same act, so the record can
  never say "closed" while the access keeps working. The window is clamped to an
  hour; asking for a day gets an hour.

## Not built yet

OIDC federation. Administrator recovery codes, and administrator accounts created
through anything but the development seeder. See
[`docs/implementation/STATUS.md`](../../docs/implementation/STATUS.md).

> `identity` is a reserved T-SQL keyword. EF quotes it automatically; hand-written
> SQL must bracket it as `[identity].[AuditEvents]`.
