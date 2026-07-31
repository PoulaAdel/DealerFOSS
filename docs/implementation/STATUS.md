# Implementation Status

Current phase: **I0 complete (except container path) → I1 in progress**
Current milestone: MFA foundation and OIDC federation
Last verified: 2026-07-31 · `dotnet build` 0 warnings/0 errors, `dotnet test` 223/223,
`verify-e2e.ps1` PASS

> **Layout note (2026-07-30).** The repository moved from seven backend projects to
> three — `src/Core`, `src/Identity`, `src/App` — with one flat folder per
> capability inside `App` and three `DbContext` classes instead of five. Table
> names and schemas are unchanged. See
> [ADR-017](../adr/0017-three-projects-flat-features.md); paths in older entries
> below refer to the previous layout.

> The repository is the truth. If this file disagrees with the code, this file
> is wrong — correct it. A file existing is not evidence that a workflow works.
> Every checked item below names the command that proves it.

## Exit criteria

### I0 — repository and engineering baseline

- [x] Solution builds with warnings as errors — `dotnet build OpenDealer360.slnx -c Release` → 0 warnings, 0 errors *(agent-verifiable)*
- [x] Centrally pinned packages — `Directory.Packages.props`; `NuGetAudit` blocked and forced an upgrade of vulnerable OpenTelemetry 1.12.0 → 1.17.0 *(agent-verifiable)*
- [x] Backend starts locally — `dotnet run --project src/App`; `/` and both health endpoints return 200 *(agent-verifiable)*
- [x] Liveness and readiness separated — `GET /health/live`, `GET /health/ready` *(agent-verifiable)*
- [x] Structured logs and OpenTelemetry wired, no secrets emitted — Serilog console output; OTLP exporter registered only when configured *(agent-verifiable)*
- [x] Architecture tests present and passing — `dotnet test` → 7/7 *(agent-verifiable)*
- [x] Domain unit tests — `tests/Unit`, 58 tests covering money/currency, `Result` invariants, authorization scope, the permission catalogue, entity invariants, and request-context write-once semantics. **Mutation-proven 2026-07-27:** breaking cross-currency refusal, rooftop coverage, and the catalogue check failed exactly the 6 tests guarding them *(agent-verifiable)*
- [x] CI runs build + tests with the audit gate — `.github/workflows/ci.yml` *(agent-verifiable)*
- [x] AGPLv3, contribution guide, security policy, ADR index *(agent-verifiable)*
- [x] SQL-backed integration tests run repeatably — `dotnet test` → 8/8 in `IntegrationTests`, driving the real Host against SQL via `WebApplicationFactory`; CI supplies a SQL Server service container *(agent-verifiable)*
- [x] Architecture tests demonstrably fail on a forbidden reference — rehearsed 2026-07-25: an EF Core dependency added to `Core/Result.cs` failed `Core_must_not_depend_on_web_or_persistence_frameworks` naming the offending type, then was reverted. Procedure: `tests/Architecture/README.md` *(agent-verifiable)*
- [x] Code of conduct — `.github/CODE_OF_CONDUCT.md` *(agent-verifiable)*
- [x] One immutable ADR file per Accepted decision — `docs/adr/0001…0016`, index repointed to the files *(agent-verifiable)*
- [ ] Windows and container development paths documented — Windows path documented in `CLAUDE.md`; container path unverified on this host *(human-verifiable: needs a working container host)*

Frontend shell is **not** an I0 item; it moved to I1, where the session it depends on exists.

### I1 — organization, tenancy, identity, and security foundation

- [x] Host catalog and tenant database creation/resolution — `deploy/verify-e2e.ps1` *(agent-verifiable)*
- [x] `DealerOrganization`, `LegalEntity`, `Rooftop`, `Department` — `src/App/Organization/` + the `org` schema in migration `InitialTenant` *(agent-verifiable)*
- [x] **Two dealer organizations resolve to separate databases** — `verify-e2e.ps1` → `North Auto Group: 2 rooftop(s)`, `City Motors: 1 rooftop(s)` *(agent-verifiable)*
- [x] **One organization contains multiple rooftops** — `northgroup` has `NAG-01` and `NAG-02` *(agent-verifiable)*
- [x] Unresolvable tenant is rejected at the edge — missing header → 400, unknown tenant → 404 *(agent-verifiable)*
- [x] Migrations and seeding are idempotent — second `verify-e2e.ps1` run reports "already up to date" and skips seeding *(agent-verifiable)*
- [x] Secret-protector seam; development pass-through refused outside Development — `Program.cs` startup guard *(agent-verifiable)*
- [x] Users, roles, permissions, scoped user assignments — `identity` schema, migration `InitialIdentity`; roles hold catalogued permissions, assignments are organization- or rooftop-scoped *(agent-verifiable)*
- [x] **Unauthorized-rooftop reads fail in endpoint tests** — `RooftopAuthorizationTests` (7 tests): a rooftop-scoped user sees only their own rooftop in the list, is refused a sibling rooftop by direct id (403), and an unassigned user is refused entirely. **Regression-proven 2026-07-25:** removing the scope check fails exactly these tests *(agent-verifiable)*
- [x] Audit events capture scoped security-sensitive changes — every denial writes an append-only `identity.AuditEvents` row; the context refuses to update or delete audit history (ADR-016) *(agent-verifiable)*
- [ ] Background-job authorization tests — no jobs exist yet; due with the first scheduled job
- [x] **Durable sessions and cookie sign-in** — password credentials, SQL-backed sessions, Secure/HttpOnly/SameSite=Strict cookie, sliding idle expiry (30 min) under a fixed 8-hour ceiling. Revocation takes effect on the next request, proven by `AuthenticationTests`. An unknown email and a wrong password return byte-identical responses. *(agent-verifiable)*
- [ ] Explicit anti-forgery on writes — write endpoints now exist (customers, vehicles, inventory). The session cookie is `SameSite=Strict`, which is the current defence; a token-based check is still outstanding
- [ ] MFA foundation and OIDC federation — not started (local password identity is done)
- [ ] Global-administration separation and time-limited support access — not started
- [ ] Tenant-aware background job context — not started
- [ ] React/TypeScript/Vite shell with accessible layout — not started *(blocked: Node not installed)*
- [ ] Tenant creation, migration, backup, and restore rehearsed — migration rehearsed; **backup and restore not** *(partly human-verifiable)*

## Completed milestones

- **2026-07-25 — Engineering baseline.** Solution, Core kernel, architecture tests, CI, health endpoints, telemetry. Evidence: `dotnet build` 0/0, `dotnet test` 5/5.
- **2026-07-25 — Tenancy and Organization module.** Host catalog, cached tenant resolver, tenant middleware, organization→legal entity→rooftop→department model, EF migrations, development seeder. Evidence: `verify-e2e.ps1` → `PASS` (two isolated databases, multi-rooftop resolved, 400/404 contract), verified twice including an idempotent re-run.
- **2026-07-25 — Baseline committed.** `ef8793a` docs, `dd7b582` engineering baseline, on `feature/foundation`.
- **2026-07-25 — I0 gaps closed.** Tenant isolation moved from a manual script into CI-runnable integration tests; forbidden-reference rehearsal performed and documented; code of conduct and 16 immutable ADR files added; `CLAUDE.md` records the verified local environment. Evidence: `dotnet build` 0/0, `dotnet test` 13/13.
- **2026-07-25 — Rooftop authorization (closes R05).** Identity module with users, roles, catalogued permissions, organization- and rooftop-scoped assignments, and append-only audit. The Organization capability now filters reads to the caller's authorized rooftops and refuses a sibling rooftop addressed directly; denials are audited. Cross-module access goes only through `IAccessDirectory`, enforced by two new boundary tests. Evidence: `dotnet test` 22/22, plus a regression rehearsal in which removing the scope check failed exactly the three tests that assert it.
- **2026-07-27 — Domain unit tests.** `tests/Unit` added (58 tests). Mutation-proven: breaking cross-currency refusal, rooftop coverage, or the permission catalogue check fails exactly the tests that guard them. Evidence: `dotnet test` 80/80.

- **2026-07-27 — Signing in.** Password credentials, SQL-backed sessions, and a Secure/HttpOnly/SameSite cookie replace the development header that simply trusted the caller. Signing out stops the session on the very next request. Evidence: `dotnet test` 91/91 and `verify-e2e.ps1` PASS.

- **2026-07-28 — Customer records (first dealership feature).** A customer can be added, fetched, and found by surname, phone, or email — however the phone or email was typed. Customers are organization-shared, not rooftop-hidden. `Customers.Read` and `Customers.Create` are checked separately. Evidence: `dotnet test` 126/126, plus a regression rehearsal in which treating "no assignment" as permitted failed exactly the two permission tests.

- **2026-07-30 — Vehicles and inventory.** A vehicle can be recorded and found by whole or partial VIN; a unit can be taken into a rooftop's stock under a stock number, listed by rooftop or status, and moved through its life cycle with every move kept. Vehicles are organization-shared; **units are rooftop-owned and scoped**, proven by list, direct-id, and filtered-list routes. Stock numbers are unique within a rooftop and reusable across rooftops. A non-standard VIN is recordable with a written reason and no unique index is imposed on VIN (doc 04 §4). Evidence: `dotnet test` 178/178 and `verify-e2e.ps1` PASS, plus a regression rehearsal in which inverting the rooftop filter in `InventoryService.ListAsync` failed exactly the stock-leak tests.

- **2026-07-31 — Leads (first stage-4 feature).** An enquiry can be captured against a customer, optionally against a vehicle, worked through New → Working → Appointment → Won, lost and reopened, and handed between salespeople — with every move kept. Leads are **rooftop-owned and scoped**, proven on the list, direct-id, and filtered-list routes. The capability reaches Customers and Vehicles only through `ICustomers` and `IVehicles`; a new architecture rule names their entity types and fails the build if a lead touches one. Append-only history now keys off the `IAppendOnly` marker in Core rather than a hand-maintained type list, so a future history table is protected by implementing the interface. Evidence: `dotnet test` 198/198 and `verify-e2e.ps1` PASS, plus two regression rehearsals — inverting the rooftop filter failed exactly the leak test, and referencing `Customer` from `Lead` failed exactly the new boundary rule.

- **2026-07-31 — Deals (a car can actually be sold).** A deal is started on a specific car, priced with charges and a trade-in, submitted, approved, and delivered — or cancelled at any point. Terms freeze the moment the deal leaves Draft, and sending it back for changes withdraws the approval. **`Deals.Write` and `Deals.Approve` are separate**: a new `Salesperson` development account can build and submit a deal but cannot sign it off. **Starting a deal holds the car and cancelling releases it**, through `IInventory` and committed in one transaction, so the same car cannot be sold twice. Each history entry records the amount at that moment, so an approval records the number that was approved. Evidence: `dotnet test` 223/223 and `verify-e2e.ps1` PASS, plus a regression rehearsal in which collapsing `Deals.Approve` into `Deals.Write` failed exactly the segregation-of-duties test.

## Active risks and blockers

| Owner | Item | Required evidence | Effect |
|---|---|---|---|
| Host env | SQL Server container unusable here (`LSA 0xc000004b`) | A working container host, or accept LocalDB for local development | Container development path unverified; ADR-015 unchanged |
| Host env | Node.js absent | Node installed | All frontend work blocked |
| Human | Backup and restore rehearsal | A timed restore producing a working system | I1 exit criterion cannot close |
| Human | Delivery Phase 0 — pilot dealers, provider access, sandbox data | Signed access and representative extracts | I2 connector certification cannot start |

## Next milestone

**Outcome:** a second factor can be enrolled and is demanded at sign-in, and an existing identity provider can be used instead of a local password.

This is the last unmet **security** criterion in I1 that does not need a person or a machine we do not have. It comes before more dealership features because every later feature inherits the sign-in path, and retrofitting a second factor after deals and finance data exist is far more disruptive.

- **Included:** TOTP enrolment and verification, recovery codes, a per-organization policy for who must use it, and OIDC federation as an alternative to the local password — with local accounts still working for organizations that do not federate.
- **Explicitly excluded:** WebAuthn/passkeys, SCIM user provisioning, and global-administration separation — each is its own milestone.
- **Caution:** enrolment secrets and recovery codes are credentials. They must go through `ISecretProtector`, must never reach a log or an audit row (ADR-016), and a failed second factor must be indistinguishable in timing and response from a wrong password, exactly as the existing sign-in path already is.
