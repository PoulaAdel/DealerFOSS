# Implementation Status

Current phase: **I0 complete (except container path) → I1 in progress**
Current milestone: MFA foundation and OIDC federation
Last verified: 2026-07-31 · `dotnet build` 0 warnings/0 errors, `dotnet test` 285/285,
`verify-e2e.ps1` PASS against **both** LocalDB and the SQL Server container

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
- [x] CI runs build + tests with the audit gate — `.github/workflows/ci.yml`, plus a separate frontend job running `npm ci`, typecheck, and a production build *(agent-verifiable — though CI has still never executed, since no remote is configured)*
- [x] AGPLv3, contribution guide, security policy, ADR index *(agent-verifiable)*
- [x] SQL-backed integration tests run repeatably — `dotnet test` → 8/8 in `IntegrationTests`, driving the real Host against SQL via `WebApplicationFactory`; CI supplies a SQL Server service container *(agent-verifiable)*
- [x] Architecture tests demonstrably fail on a forbidden reference — rehearsed 2026-07-25: an EF Core dependency added to `Core/Result.cs` failed `Core_must_not_depend_on_web_or_persistence_frameworks` naming the offending type, then was reverted. Procedure: `tests/Architecture/README.md` *(agent-verifiable)*
- [x] Code of conduct — `.github/CODE_OF_CONDUCT.md` *(agent-verifiable)*
- [x] One immutable ADR file per Accepted decision — `docs/adr/0001…0016`, index repointed to the files *(agent-verifiable)*
- [x] Windows and container development paths documented — both verified 2026-07-31 and written up in `deploy/README.md`. The container path was previously believed broken on this host; the cause was a Windows bind mount in the compose file, not the host. SQL Server 2022 (16.0.4265.3) accepts connections from a named volume. Node 22 is provided the same way, so the frontend needs nothing installed *(agent-verifiable)*

Frontend shell is **not** an I0 item; it moved to I1, where the session it depends on exists.

### I1 — organization, tenancy, identity, and security foundation

- [x] Host catalog and tenant database creation/resolution — `deploy/verify-e2e.ps1` *(agent-verifiable)*
- [x] `DealerOrganization`, `LegalEntity`, `Rooftop`, `Department` — `src/App/Organization/` + the `org` schema in migration `InitialTenant` *(agent-verifiable)*
- [x] **Two dealer organizations resolve to separate databases** — `verify-e2e.ps1` → `North Auto Group: 2 rooftop(s)`, `City Motors: 1 rooftop(s)` *(agent-verifiable)*
- [x] **One organization contains multiple rooftops** — `northgroup` has `NAG-01` and `NAG-02` *(agent-verifiable)*
- [x] Unresolvable tenant is rejected at the edge — missing header → 400, unknown tenant → 404 *(agent-verifiable)*
- [x] Migrations and seeding are idempotent — second `verify-e2e.ps1` run reports "already up to date" and skips seeding *(agent-verifiable)*
- [x] **Secret protection implemented, not just seamed** — `EnvelopeSecretProtector` encrypts tenant connection strings with AES-256-GCM, a random nonce per value, and a key id carried with the ciphertext so keys rotate without rewriting stored data. Cross-platform by design: the maintainer targets Windows service, Linux container, and hosted, which rules DPAPI out. Real encryption is used in every environment where keys are configured, including Development, so what ships is what developers exercise. `Program.cs` still refuses to start on the pass-through outside Development, now with a message naming what to set. 13 tests cover round-trip, per-call nonce, tamper detection, wrong key, rotation, retired key, and every startup misconfiguration *(agent-verifiable)*
- [x] Users, roles, permissions, scoped user assignments — `identity` schema, migration `InitialIdentity`; roles hold catalogued permissions, assignments are organization- or rooftop-scoped *(agent-verifiable)*
- [x] **Unauthorized-rooftop reads fail in endpoint tests** — `RooftopAuthorizationTests` (7 tests): a rooftop-scoped user sees only their own rooftop in the list, is refused a sibling rooftop by direct id (403), and an unassigned user is refused entirely. **Regression-proven 2026-07-25:** removing the scope check fails exactly these tests *(agent-verifiable)*
- [x] Audit events capture scoped security-sensitive changes — every denial writes an append-only `identity.AuditEvents` row; the context refuses to update or delete audit history (ADR-016) *(agent-verifiable)*
- [ ] Background-job authorization tests — no jobs exist yet; due with the first scheduled job
- [x] **Durable sessions and cookie sign-in** — password credentials, SQL-backed sessions, Secure/HttpOnly/SameSite=Strict cookie, sliding idle expiry (30 min) under a fixed 8-hour ceiling. Revocation takes effect on the next request, proven by `AuthenticationTests`. An unknown email and a wrong password return byte-identical responses. *(agent-verifiable)*
- [ ] Explicit anti-forgery on writes — write endpoints now exist (customers, vehicles, inventory). The session cookie is `SameSite=Strict`, which is the current defence; a token-based check is still outstanding
- [x] **MFA foundation** — opt-in TOTP (RFC 6238). A password buys a short-lived, hashed, single-use challenge rather than a session; only a valid code completes sign-in. The shared secret is encrypted at rest with `ISecretProtector`; enrolment is two-phase so a mis-scanned QR cannot lock anybody out; ten single-use recovery codes are stored as hashes; disabling needs a current code. Verified against the published RFC test vectors, so authenticator apps agree with us. **Regression-proven:** silently skipping the challenge fails exactly the four tests that demand it *(agent-verifiable)*
- [ ] OIDC federation — not started, and needs an identity provider to test against
- [ ] MFA required by policy rather than by choice — enrolment is currently opt-in per user
- [ ] Global-administration separation and time-limited support access — not started
- [ ] Tenant-aware background job context — not started
- [ ] React/TypeScript/Vite shell with accessible layout — **compiles, never run.** `frontend/` holds the project, an API client, a sign-in screen covering the second factor, a stock list, and a trial balance, each rendering loading, empty, permission-denied, failure, and retry states. `npm ci && npm run typecheck` passes clean in the `odms-node` container, and CI now runs typecheck plus a production build on every push. **That proves it compiles and nothing more.** Whether React Router behaves as assumed, whether the dev-server proxy reaches the API, and whether the session cookie survives the round trip are all unverified until somebody loads it in a browser *(blocked: awaiting the first run)*
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

- **2026-07-31 — The ledger behind a sale.** Delivering a car posts a balanced journal entry against a seeded chart of accounts, inside the same transaction as the delivery — so the deal, the inventory move, and the ledger can never disagree. Entries carry both the legal entity that owns the money and the rooftop that earned it, resolved through `IOrganization`. **Nothing is ever edited or deleted**: `JournalEntry` and `JournalLine` are `IAppendOnly`, and a mistake is corrected by posting a reversal, which cannot itself be reversed and cannot be applied twice. A delivery cannot be posted twice. Evidence: `dotnet test` 243/243 and `verify-e2e.ps1` PASS, plus a regression rehearsal in which putting the posting map one penny out failed eight tests **and blocked the sale**, which is the intended behaviour. **Scope caution:** this is a sale ledger, not a set of books — no periods, no trial balance, no tax, and it assumes the customer pays in full. See `src/App/Accounting/README.md`.

## Decisions recorded (2026-07-31)

Three open questions were answered by the maintainer. None is fully implemented;
they are recorded here so the design is settled before the code that depends on
them is written.

| Decision | Answer | State |
|---|---|---|
| *(consequence of the deployment answer below)* | Secret protection must be cross-platform | **Implemented** — AES-256-GCM envelope encryption with key ids. No re-encryption tool yet, so rotation currently means "add a key and keep the old one". |
| May a salesperson approve their own deal? | **No.** A sales manager approves it. | **Implemented** — enforced on the entity so background callers cannot route around it, and a denial is audited. Holding `Deals.Approve` is not sufficient if it is your own deal. |
| When does an accounting month close? | Calendar month end, fiscal year = calendar year, prior-month entries accepted until the 10th, then locked. Per organization. | **Recorded, not enforced.** Standard franchised-dealer practice chosen as the default; see `src/App/Accounting/README.md`. Wants confirmation from a real dealer's accountant. |
| Where does this deploy? | **All three:** Windows service, Linux container, and hosted. | **Acted on.** DPAPI ruled out as Windows-only; replaced with AES-256-GCM envelope encryption keyed from configuration, which every target supplies the same way (`Secrets__Keys__<id>`). `deploy/README.md` covers all three. |

- **2026-07-31 — Secret protection, so this can ship at all.** Tenant connection strings are encrypted at rest with AES-256-GCM: a fresh nonce per value, an authentication tag so a tampered value fails rather than decrypting to something wrong, and a key id carried with the ciphertext so a key can be rotated without making existing data unreadable. Cross-platform, because all three deployment targets are wanted. Evidence: `dotnet test` 258/258, including a tamper test and a rotation test. **Gap named, not hidden:** nothing re-encrypts values under a new key, so rotation today means adding a key and keeping the old one; and switching an existing installation from the development pass-through needs a migration pass that does not exist.

- **2026-07-31 — A second factor at sign-in.** Opt-in TOTP: a password now buys a challenge rather than a session for an enrolled account, and only a code from an authenticator app — or a single-use recovery code — completes it. The secret is encrypted at rest with the protector built earlier the same day, enrolment is two-phase so a mis-scanned QR cannot lock somebody out, and guessing is cut off after five wrong codes. Verified against the published RFC 6238 test vectors, which is the only way to know real phones will agree. Evidence: `dotnet test` 282/282, plus a rehearsal in which silently skipping the challenge failed exactly the four tests that demand it. **Not yet:** requiring MFA by policy, and OIDC — which needs an identity provider to test against.

- **2026-07-31 — Each test run gets its own database.** The integration suite used to share one long-lived database, and assertions quietly became order-dependent as rows accumulated: three separate tests failed over time because a freshly created row fell off the end of a capped, sorted page. Each run now creates databases named for the run and drops them afterwards, sweeping anything a crashed run left behind. Tenant database names derive from the host catalog's name, so this needed no test-only branch in the seeder — and an installation whose catalog is named something else now keeps its databases together, which was arguably a latent bug. Evidence: `dotnet test` 282/282 with the integration suite unchanged at 7 seconds, and no `OpenDealer360_Test_*` database surviving the run.

- **2026-07-31 — A frontend, written but not yet run.** `frontend/` is a Vite + React + TypeScript project: an API client that carries the tenant header and the session cookie, a sign-in screen that handles the second-factor step, an authenticated shell with a skip link and keyboard-visible focus, and an inventory list rendering every state a real screen needs. The dev server proxies `/api` because the session cookie is `SameSite=Strict` and would otherwise be dropped on a cross-origin call. **Explicitly unverified:** no `npm install`, no typecheck, no browser. Treat every line of it as unproven until the container has run it once.

- **2026-07-31 — The ledger produces totals.** `GET /api/v1/accounting/balances` groups posted lines per account over a period and states each balance on the account's normal side, so an asset with more debits than credits reads positive. It reports whether the two columns agree — the headline of a trial balance is whether it balances, and a difference means something was lost on the way in. Rooftop-scoped, because a total is as revealing as the entries behind it, and it refuses to sum two currencies rather than printing a number that means nothing. Evidence: `dotnet test` 285/285, plus a rehearsal in which inverting the scope filter failed the totals test.

## Active risks and blockers

| Owner | Item | Required evidence | Effect |
|---|---|---|---|
| Human | Backup and restore rehearsal | A timed restore producing a working system | I1 exit criterion cannot close |
| Human | Delivery Phase 0 — pilot dealers, provider access, sandbox data | Signed access and representative extracts | I2 connector certification cannot start |

## Next milestone

**Outcome:** a second factor can be enrolled and is demanded at sign-in, and an existing identity provider can be used instead of a local password.

This is the last unmet **security** criterion in I1 that does not need a person or a machine we do not have. It comes before more dealership features because every later feature inherits the sign-in path, and retrofitting a second factor after deals and finance data exist is far more disruptive.

- **Included:** TOTP enrolment and verification, recovery codes, a per-organization policy for who must use it, and OIDC federation as an alternative to the local password — with local accounts still working for organizations that do not federate.
- **Explicitly excluded:** WebAuthn/passkeys, SCIM user provisioning, and global-administration separation — each is its own milestone.
- **Caution:** enrolment secrets and recovery codes are credentials. They must go through `ISecretProtector`, must never reach a log or an audit row (ADR-016), and a failed second factor must be indistinguishable in timing and response from a wrong password, exactly as the existing sign-in path already is.
