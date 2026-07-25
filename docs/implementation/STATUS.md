# Implementation Status

Current phase: **I0 partial → I1 partial**
Current milestone: rooftop-scoped authorization (risk R05)
Last verified: 2026-07-25 · commit `dd7b582` on `feature/foundation`

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
- [x] Architecture tests present and passing — `dotnet test` → 5/5 *(agent-verifiable)*
- [x] CI runs build + tests with the audit gate — `.github/workflows/ci.yml` *(agent-verifiable)*
- [x] AGPLv3, contribution guide, security policy, ADR index *(agent-verifiable)*
- [x] SQL-backed integration tests run repeatably — `dotnet test` → 8/8 in `OpenDealer360.IntegrationTests`, driving the real Host against SQL via `WebApplicationFactory`; CI supplies a SQL Server service container *(agent-verifiable)*
- [x] Architecture tests demonstrably fail on a forbidden reference — rehearsed 2026-07-25: an EF Core dependency added to `Platform/Kernel/Result.cs` failed `Platform_must_not_depend_on_web_or_persistence_frameworks` naming the offending type, then was reverted. Procedure: `tests/Architecture/README.md` *(agent-verifiable)*
- [x] Code of conduct — `CODE_OF_CONDUCT.md` *(agent-verifiable)*
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
- [ ] **Users, roles, permissions, scoped user assignments** — not started
- [ ] **Cross-tenant and unauthorized-rooftop reads/writes fail in endpoint and job tests** — not started *(this is the current milestone)*
- [ ] Durable sessions, BFF cookie auth, CSRF, rotation and revocation — not started
- [ ] Local identity, MFA foundation, OIDC extension point — not started
- [ ] Global-administration separation and time-limited support access — not started
- [ ] Audit events for scoped security-sensitive changes — not started
- [ ] Tenant-aware background job context — not started
- [ ] React/TypeScript/Vite shell with accessible layout — not started *(blocked: Node not installed)*
- [ ] Tenant creation, migration, backup, and restore rehearsed — migration rehearsed; **backup and restore not** *(partly human-verifiable)*

## Completed milestones

- **2026-07-25 — Engineering baseline.** Solution, Platform kernel, architecture tests, CI, health endpoints, telemetry. Evidence: `dotnet build` 0/0, `dotnet test` 5/5.
- **2026-07-25 — Tenancy and Organization module.** Host catalog, cached tenant resolver, tenant middleware, organization→legal entity→rooftop→department model, EF migrations, development seeder. Evidence: `verify-e2e.ps1` → `PASS` (two isolated databases, multi-rooftop resolved, 400/404 contract), verified twice including an idempotent re-run.
- **2026-07-25 — Baseline committed.** `ef8793a` docs, `dd7b582` engineering baseline, on `feature/foundation`.
- **2026-07-25 — I0 gaps closed.** Tenant isolation moved from a manual script into CI-runnable integration tests; forbidden-reference rehearsal performed and documented; code of conduct and 16 immutable ADR files added; `CLAUDE.md` records the verified local environment. Evidence: `dotnet build` 0/0, `dotnet test` 13/13.

## Active risks and blockers

| Owner | Item | Required evidence | Effect |
|---|---|---|---|
| Agent | R05 — rooftop authorization not enforced | Denial test that fails when the check is removed | Critical risk stays open; the current endpoint has no scope check |
| Host env | SQL Server container unusable here (`LSA 0xc000004b`) | A working container host, or accept LocalDB for local development | Container development path unverified; ADR-015 unchanged |
| Host env | Node.js absent | Node installed | All frontend work blocked |
| Human | Backup and restore rehearsal | A timed restore producing a working system | I1 exit criterion cannot close |
| Human | Delivery Phase 0 — pilot dealers, provider access, sandbox data | Signed access and representative extracts | I2 connector certification cannot start |

## Next milestone

**Outcome:** a rooftop-scoped user cannot read another rooftop's data, proven by a test that fails when the scope check is removed.

- **Included:** permissions catalogue, scoped user assignments, server-side scope enforcement on the Organization endpoint, audit event on denial, unauthorized-rooftop denial test.
- **Explicitly excluded:** full identity, MFA, OIDC, durable sessions, support access — each is a later milestone in I1.
