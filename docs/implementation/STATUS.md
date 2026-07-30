# Implementation Status

Current phase: **I0 complete (except container path) → I1 in progress**
Current milestone: vehicles and inventory
Last verified: 2026-07-28 · `dotnet build` 0 warnings/0 errors, `dotnet test` 126/126

> The repository is the truth. If this file disagrees with the code, this file
> is wrong — correct it. A file existing is not evidence that a workflow works.
> Every checked item below names the command that proves it.

## Exit criteria

### I0 — repository and engineering baseline

- [x] Solution builds with warnings as errors — `dotnet build OpenDealer360.slnx -c Release` → 0 warnings, 0 errors *(agent-verifiable)*
- [x] Centrally pinned packages — `Directory.Packages.props`; `NuGetAudit` blocked and forced an upgrade of vulnerable OpenTelemetry 1.12.0 → 1.17.0 *(agent-verifiable)*
- [x] Backend starts locally — `dotnet run --project src/Host`; `/` and both health endpoints return 200 *(agent-verifiable)*
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
- [x] `DealerOrganization`, `LegalEntity`, `Rooftop`, `Department` — `src/Modules/Organization/Domain/` + migration `20260724210646_InitialOrganization` *(agent-verifiable)*
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
- [ ] CSRF protection on writes — due with the first write endpoint; every endpoint today is a read
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

## Active risks and blockers

| Owner | Item | Required evidence | Effect |
|---|---|---|---|
| Host env | SQL Server container unusable here (`LSA 0xc000004b`) | A working container host, or accept LocalDB for local development | Container development path unverified; ADR-015 unchanged |
| Host env | Node.js absent | Node installed | All frontend work blocked |
| Human | Backup and restore rehearsal | A timed restore producing a working system | I1 exit criterion cannot close |
| Human | Delivery Phase 0 — pilot dealers, provider access, sandbox data | Signed access and representative extracts | I2 connector certification cannot start |

## Next milestone

**Outcome:** a vehicle can be recorded and found by VIN or stock number, and a rooftop's inventory can be listed with its current status.

Vehicles are organization-shared like customers; an **inventory unit** — a specific vehicle on a specific lot, with a status and a cost — is rooftop-owned and must be scoped (doc 04 §1, §3).

- **Included:** `Vehicle` identity (VIN, year/make/model/trim), `InventoryUnit` with rooftop scope, status history, `Vehicles.Read` / `Inventory.Read` / `Inventory.Manage` permissions, search by VIN and stock number, and the rooftop-scope tests that prove one location cannot see another's stock.
- **Explicitly excluded:** pricing rules, aging analytics, vehicle images, and any incoming provider feed — those arrive with reporting and the integration runtime.
- **VIN caution (doc 04 §4):** VIN validation must allow documented exceptions and a duplicate-resolution path. Do **not** add a universal unique index on VIN — the same physical vehicle legitimately reappears as a trade-in, and bad source data is common.
