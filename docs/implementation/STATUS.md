# Implementation Status

Current phase: **I0 complete → I1 complete except OIDC, which is blocked.** Work
has since run ahead into I3, I4 and I5 rather than down the phase list — the
per-phase exit criteria below are the honest record of which parts are done
Current milestone: **a customer can carry an address, and a deal carries its
own registration address.** `PUT /customers/{id}/address` lets an existing
customer's mailing address be set or cleared — the domain type and the database
column already existed (import populated them); there was no way for a person
to type one in or change it. `Deal.RegistrationAddress`, set through
`POST /deals/{id}/registration-address`, is the field ADR-024 named as not built:
where the car will actually be registered or garaged, distinct from the
customer's own address and from `TaxedAt` (the narrower four-field snapshot a
tax line was resolved from). It freezes with the rest of the deal's terms once
submitted, the same rule as the charges and the tax lines. Both are on screen —
`/customers/:id` and `/deals/:id` — in all six languages.
What is next is on the register in [`docs/11`](../11-Franchise-and-External-Scope.md)
§12 — but see the re-review of 2026-09-16 below before choosing from it, and
the UI/UX audit of 2026-09-17, whose remaining open findings are the customer
record having no cross-module actions and the arrival-motion work recorded in
the device-only motion audit. The pager finding closed on 2026-09-19; the token
migration closed on 2026-09-18, although this header still called it unfinished.
Last verified: 2026-09-23 · `dotnet build` 0 warnings/0 errors, `dotnet test` 874/874,
`verify-e2e.ps1` PASS against the configured SQL container (the canonical LocalDB
catalogue is detached with its MDF still on disk; see the milestone below).
The frontend gates were last run on 2026-09-21 — `npm audit` clean at high (two
moderate `@vitest/mocker` advisories are open and need a vitest 5 upgrade),
`npm run typecheck`, `npm test` 498/498, `npm run build` — and are not re-stated
here because nothing under `frontend/` has changed since

**Stage 1 is done.** The last open criterion — a rehearsed backup and restore —
closed on 2026-08-04. The only unmet identity item left is OIDC federation, which
cannot be honestly built or tested without a real identity provider; a fixture
pretending to be one would prove nothing. Stage 2 has both directions of the file
path working, with a screen.

> **Layout note (2026-07-30).** The repository moved from seven backend projects to
> three — `src/Core`, `src/Identity`, `src/App` — with one flat folder per
> capability inside `App` and three `DbContext` classes instead of five. Table
> names and schemas are unchanged. See
> [ADR-017](../adr/0017-three-projects-flat-features.md); paths in older entries
> below refer to the previous layout.

> The repository is the truth. If this file disagrees with the code, this file
> is wrong — correct it. A file existing is not evidence that a workflow works.
> Every checked item below names the command that proves it.

## Does it exist? — the whole product on one screen

The rest of this file is chronological, which answers "what happened" and not
"is X built". This table answers the second question and is the **single home**
for it — the specification documents link here instead of each hedging their own
claims ([Workbook](../00-Workbook.md), reading rule). Checked 2026-08-15.

**API** means the endpoint works and is tested. **Screen** means a person can
reach it in the browser. **Spec** means it is designed in docs 01–08 and no code
exists.

| Area | State | Where |
|---|---|---|
| Dealer organizations kept apart, one database each | API · proven by `verify-e2e.ps1` | `App/Tenancy` |
| Organization → legal entity → rooftop → department | API · Screen | `App/Organization` |
| Sign in, sessions, two-step (TOTP), recovery codes | API · Screen | `src/Identity` |
| **Passkeys** — enrol, sign in, forget | API · Screen | `src/Identity`, `/security/passkeys` |
| Roles, permissions, staff administration | API · Screen | `src/Identity`, `/staff` |
| Control plane: dealerships, suspend/resume, support access | API · Screen | `App/Administration`, `/admin` |
| Customers, with a duplicate check | API · Screen | `App/Customers` |
| Vehicles, VIN and its documented exceptions | API · Screen | `App/Vehicles` |
| **Public safety-recall lookup** — the one outbound call | API · Screen | `App/Vehicles`, stock detail |
| Stock: units, status history, cost, aging | API · Screen | `App/Inventory` |
| Enquiries and their status history | API · Screen | `App/Leads` |
| Deals: pricing, trade-ins, approval, delivery | API · Screen | `App/Deals` |
| F&I products sold with the car, and their profit | API · Screen | `App/Finance` |
| Workshop: repair orders, lines, authorization, invoicing | API · Screen | `App/RepairOrders` |
| Booking diary, and arrival opening a job | API · Screen | `App/RepairOrders` |
| **Pay type** — customer, warranty, internal | API · Screen | `App/RepairOrders` |
| **Labour report** — hours sold, effective rate | API · Screen | `/workshop/labour` |
| Parts: catalogue, receipts, costed issue to a job | API · Screen | `App/Parts` |
| Ledger, chart of accounts, fiscal close and reopen | API · Screen | `App/Accounting` |
| Month in review, stock aging | API · Screen | `App/Reporting` |
| Printable paperwork (HTML, print stylesheet) | API · Screen | `App/Documents` |
| Import from a file; export a dealership's records | API · Screen | `App/DataMigration` |
| **Move one lot to another installation, ids and all** | API · Screen — added 2026-09-21, `/records`. A JSON package keeping every id, every reference and the paperwork they render (ADR-027). Not a backup: no ledger, no receivables, no parts stock | `App/DataMigration` |
| Connector runtime: cursors, runs, quarantine | API · Screen — **this row said "API only" until 2026-09-21 and had been wrong since the 19th**, when `/integrations` landed and the certification finally reached a reader | `App/Integrations` |
| Six languages, RTL for Arabic | Screen | `shared/i18n` |
| A live connector to any real DMS | **Spec** — needs a provider agreement | doc 05 |
| OIDC / SAML federation | **Spec** — needs an identity provider to test against | doc 06 §2 |
| Scheduled background work (Quartz) | **Spec** — one in-process worker exists, nothing runs on a schedule | doc 07 §6 |
| Idempotency keys, ETags on the HTTP surface | **Spec** | doc 06 §6 |
| `[rpt]` analytical projections | **Spec** — reporting is live queries today | doc 04 §6 |
| A CLI | **Spec, and doc 04 §8 claimed it existed until 2026-08-15** | — |
| Warranty claim submission, manufacturer APIs, CSI | **Spec** — blocked on an OEM relationship | doc 11 §3 |
| Credit reports, auctions, title history, plate lookup | **Spec** — blocked on commercial contracts | doc 11 §3 |
| Tax on a deal | **API · Screen** — the basis, tax as evidence with provenance, and person-entered tax, with a band on the deal desk (2026-09-09). No rate tables and no pack data: nothing computes it | doc 11 §3.3 |
| Titling and registration (EVR/ERT) | **Spec** — still blocked, on a state list and a Service Provider contract per state | doc 11 §3.3 |

## Exit criteria

### I0 — repository and engineering baseline

- [x] Solution builds with warnings as errors — `dotnet build DealerFOSS.slnx -c Release` → 0 warnings, 0 errors *(automated)*
- [x] Centrally pinned packages — `Directory.Packages.props`; `NuGetAudit` blocked and forced an upgrade of vulnerable OpenTelemetry 1.12.0 → 1.17.0 *(automated)*
- [x] Backend starts locally — `dotnet run --project src/App`; `/` and both health endpoints return 200 *(automated)*
- [x] Liveness and readiness separated — `GET /health/live`, `GET /health/ready` *(automated)*
- [x] Structured logs and OpenTelemetry wired, no secrets emitted — Serilog console output; OTLP exporter registered only when configured *(automated)*
- [x] Architecture tests present and passing — `dotnet test` → 7/7 *(automated)*
- [x] Domain unit tests — `tests/Unit`, 58 tests covering money/currency, `Result` invariants, authorization scope, the permission catalogue, entity invariants, and request-context write-once semantics. **Mutation-proven 2026-07-27:** breaking cross-currency refusal, rooftop coverage, and the catalogue check failed exactly the 6 tests guarding them *(automated)*
- [x] CI runs build + tests with the audit gate — `.github/workflows/ci.yml`, plus a separate frontend job running `npm ci`, typecheck, and a production build *(automated — CI runs on push; every milestone so far was gated locally first)*
- [x] AGPLv3, contribution guide, security policy, ADR index *(automated)*
- [x] SQL-backed integration tests run repeatably — `dotnet test` → 8/8 in `IntegrationTests`, driving the real Host against SQL via `WebApplicationFactory`; CI supplies a SQL Server service container *(automated)*
- [x] Architecture tests demonstrably fail on a forbidden reference — rehearsed 2026-07-25: an EF Core dependency added to `Core/Result.cs` failed `Core_must_not_depend_on_web_or_persistence_frameworks` naming the offending type, then was reverted. Procedure: `tests/Architecture/README.md` *(automated)*
- [x] Code of conduct — `.github/CODE_OF_CONDUCT.md` *(automated)*
- [x] One immutable ADR file per Accepted decision — `docs/adr/0001…0016`, index repointed to the files *(automated)*
- [x] Windows and container development paths documented — both verified 2026-07-31 and written up in `deploy/README.md`. The container path was previously believed broken on this host; the cause was a Windows bind mount in the compose file, not the host. SQL Server 2022 (16.0.4265.3) accepts connections from a named volume. Node 22 is provided the same way, so the frontend needs nothing installed *(automated)*

Frontend shell is **not** an I0 item; it moved to I1, where the session it depends on exists.

### I1 — organization, tenancy, identity, and security foundation

- [x] Host catalog and tenant database creation/resolution — `deploy/verify-e2e.ps1` *(automated)*
- [x] `DealerOrganization`, `LegalEntity`, `Rooftop`, `Department` — `src/App/Organization/` + the `org` schema in migration `InitialTenant` *(automated)*
- [x] **Two dealer organizations resolve to separate databases** — `verify-e2e.ps1` → `North Auto Group: 2 rooftop(s)`, `City Motors: 1 rooftop(s)` *(automated)*
- [x] **One organization contains multiple rooftops** — `northgroup` has `NAG-01` and `NAG-02` *(automated)*
- [x] Unresolvable tenant is rejected at the edge — missing header → 400, unknown tenant → 404 *(automated)*
- [x] Migrations and seeding are idempotent — second `verify-e2e.ps1` run reports "already up to date" and skips seeding *(automated)*
- [x] **Secret protection implemented, not just seamed** — `EnvelopeSecretProtector` encrypts tenant connection strings with AES-256-GCM, a random nonce per value, and a key id carried with the ciphertext so keys rotate without rewriting stored data. Cross-platform by design: the maintainer targets Windows service, Linux container, and hosted, which rules DPAPI out. Real encryption is used in every environment where keys are configured, including Development, so what ships is what developers exercise. `Program.cs` still refuses to start on the pass-through outside Development, now with a message naming what to set. 13 tests cover round-trip, per-call nonce, tamper detection, wrong key, rotation, retired key, and every startup misconfiguration *(automated)*
- [x] Users, roles, permissions, scoped user assignments — `identity` schema, migration `InitialIdentity`; roles hold catalogued permissions, assignments are organization- or rooftop-scoped *(automated)*
- [x] **Unauthorized-rooftop reads fail in endpoint tests** — `RooftopAuthorizationTests` (7 tests): a rooftop-scoped user sees only their own rooftop in the list, is refused a sibling rooftop by direct id (403), and an unassigned user is refused entirely. **Regression-proven 2026-07-25:** removing the scope check fails exactly these tests *(automated)*
- [x] Audit events capture scoped security-sensitive changes — every denial writes an append-only `identity.AuditEvents` row; the context refuses to update or delete audit history (ADR-016) *(automated)*
- [x] **Background-job authorization tests** — the reason once given here ("no jobs exist yet") stopped being true when the import worker and the connector runtime arrived. Both carry the permissions of whoever asked for the work: `ImportWorker` names the requester in the `JobContext` it opens its scope with (it used to call `ICurrentUser.Set` itself, mid-method; see 2026-09-05), and `ConnectorRuntime` refuses to start without one. Proven by two tests — a run with nobody behind it is refused before the provider is called, and **a run asked for by somebody without the permission writes nothing** even though the provider behaves perfectly and the records are valid. The second is the one that matters: background work running privileged would be a second way into every record that ignores the permission system. **Checked for vacuity** — swapping the requester to a permitted user makes it fail, so it is discriminating rather than asserting that nothing ever happens *(automated)*
- [x] **Durable sessions and cookie sign-in** — password credentials, SQL-backed sessions, Secure/HttpOnly/SameSite=Strict cookie, sliding idle expiry (30 min) under a fixed 8-hour ceiling. Revocation takes effect on the next request, proven by `AuthenticationTests`. An unknown email and a wrong password return byte-identical responses. *(automated)*
- [x] **Explicit anti-forgery on writes** — sign-in issues a second random secret alongside the session token, stores only its hash on the session row, and returns it in a script-readable cookie. Every request that is not GET, HEAD, OPTIONS, or TRACE must repeat it in `X-CSRF-Token`; a valid session cookie on its own is refused with 403 `auth.antiforgery_failed`, and the denial is audited. Only sign-in and second-factor completion are exempt, because no session exists yet to have issued a token. Binding the token to the session means revocation kills both halves together and a token from one session cannot authorize a write on another — the hole a plain double-submit cookie leaves open. **Regression-proven:** removing the check failed exactly the three tests that demand a refusal *(automated)*
- [x] **MFA foundation** — opt-in TOTP (RFC 6238). A password buys a short-lived, hashed, single-use challenge rather than a session; only a valid code completes sign-in. The shared secret is encrypted at rest with `ISecretProtector`; enrolment is two-phase so a mis-scanned QR cannot lock anybody out; ten single-use recovery codes are stored as hashes; disabling needs a current code. Verified against the published RFC test vectors, so authenticator apps agree with us. **Regression-proven:** silently skipping the challenge fails exactly the four tests that demand it *(automated)*
- [ ] OIDC federation — not started, and needs an identity provider to test against
- [x] **MFA required by policy rather than by choice** — `Role.RequiresSecondFactor` says whether holding a role obliges the user to have one, read and set through `ISecurityPolicy` behind `Security.ManagePolicy`, which must be held **organization-wide**: a rule about the whole dealership is not set from one lot. The obligation is evaluated on every request rather than frozen at sign-in, so turning it on bites immediately instead of waiting eight hours for everyone to sign out. A user who owes one still gets a real session and may reach enrolment, confirmation, `auth/me`, and sign-out — and nothing else, `mfa/disable` included, since that would answer the policy by removing what it asks for. No role is seeded as requiring it. **Regression-proven:** removing the enforcement failed exactly the two tests that demand a refusal *(automated)*
- [x] **Global-administration separation and time-limited support access** — control-plane identities live in a `control` schema in the host catalog, are resolved by their own middleware from their own cookie (`dfoss_admin`, with its own anti-forgery pair), and **never reach `ICurrentUser`**. The refusal is therefore structural, not a check each capability performs: an administrator cookie on a business endpoint resolves to nobody and is refused before any endpoint runs, and the most privileged dealership account is refused at the control plane the same way. `FeatureBoundaryTests.The_control_plane_must_not_be_able_to_become_a_tenant_caller` fails the build if anything under `DealerFOSS.Administration` so much as references the type. A second factor is mandatory for administrators, not a policy choice — an unenrolled one reaches enrolment and sign-out and nothing else, support access included. Support access mints a **separate** tenant session for that tenant's own support principal (no password hash, so no credential opens it; read-only role organization-wide), requires a written reason, is clamped to one hour, is written into the **dealership's own** audit trail naming the administrator and the reason, and is revoked by ending the grant in the same act. **Regression-proven:** making support writable failed exactly the one test that demands a refusal; removing the administrator second-factor gate failed exactly two; setting `ICurrentUser` from the administrator middleware failed exactly the architecture rule *(automated)*

  **Deliberately not included, and named rather than hidden:** creating a second administrator through an endpoint (seeding only), administrator recovery codes, dual approval for support access, provisioning a dealership database from the control plane (listing and suspend/resume only), and a screen for any of it.
- [x] **Tenant-aware background job context** — `JobContext` names the dealership **and** who the work runs as, and `ITenantScopeFactory.OpenAsync` takes nothing else. There is no string overload, so a worker that tries to reach tenant data without answering both questions **does not compile**. The factory establishes both holders before it returns the scope, so there is no window in which a scope has a tenant and no caller — which is the window `ImportWorker` used to write its claim in. Work nobody asked for is a **different type** — `UnattendedJob`, opened through `OpenUnattendedAsync`, returning an `UnattendedScope` that has no `IServiceProvider` and whose `Get<T>` is constrained to the `IUnattendedSafe` allow-list. A sweep asking for a permission-checked capability is a compile error (CS0311), not a runtime throw.

  **`Set` is no longer on `ICurrentUser` or `ITenantContext`.** It lives on the concrete holders, which only `CurrentUserMiddleware` and `TenantScopeFactory` resolve — so a feature handed the interface can ask who the caller is and cannot decide. Three architecture tests stand in for the compile error a test cannot contain: one asserts `OpenAsync` has a single overload taking `JobContext`, two assert neither interface has a `Set`.

  **Rehearsed four ways**, each failing exactly what it should: dropping the caller stamp fails 18 tests including the whole import path; re-adding a string overload fails exactly the one architecture rule; putting `Set` back on either interface fails exactly its own rule; removing the empty-Guid guard fails exactly the unit test that names it *(automated)*
- [x] React/TypeScript/Vite shell with accessible layout — **it renders, and that is now asserted rather than assumed.** `frontend/` holds the project, an API client, a sign-in screen covering the second factor, a stock list, a trial balance, and a second-factor enrolment screen, each rendering loading, empty, permission-denied, failure, and retry states.

  Proven 2026-08-01 with the dev server running in the `dealerfoss-node` container and the API on the host: `npm run typecheck` clean; Vite serves `index.html` with the React Fast Refresh preamble; every module transforms and returns 200; and the **whole request chain works through the dev-server proxy** — `X-Tenant` passes through, `POST /auth/login` returns 200 and sets the session cookie, and that cookie authenticates `GET /inventory` and `GET /accounting/balances`, the latter returning `totalDebits == totalCredits == 96000.00`. CI runs typecheck plus a production build on every push.

  Proven 2026-08-03 by 27 component tests that mount the real tree into a DOM (jsdom) and query it the way a person reads a page — by heading, label, role, and visible text. React executes, React Router resolves and navigates, the shell paints, and every state renders its own words. `npm test` runs in CI alongside `npm audit --audit-level=high`, typecheck, and a production build *(automated)*

  **Seen, on a real browser, 2026-08-03.** The earlier claim that this could only be checked by hand was wrong — an automated browser reaches this machine's localhost. Every screen was signed into and walked at 1280 and at 375 — the shell, stock list, trial balance, enrolment (QR drawn on its white quiet zone), the console, and the restricted administrator state. The page does not scroll horizontally at either width; the wide table scrolls inside its own container as intended. Two real defects were found and fixed, neither of which a DOM test could have caught. Still needing a person: whether a phone camera physically reads that QR code *(automated)*
- [x] Tenant creation, migration, backup, and restore rehearsed — `deploy/backup.ps1` backs up the host catalog and every tenant with a manifest and per-file SHA-256; `deploy/restore.ps1 -Verify` restores alongside the original under a prefix, re-points the catalog, checks the restored databases are not empty, and then **runs the full end-to-end check against the restored copy**. Rehearsed 2026-08-04 on LocalDB: 3 databases out and back, `verify-e2e.ps1` PASS against `Restored_DealerFOSS_Host`. Damaging a manifest checksum was rehearsed too — the restore refuses before touching anything *(automated)*


### I2 — integration runtime, migration, reconciliation, and export

**Scored for the first time on 2026-09-09**, by running the checks rather than
reading the code. This phase was never given a scorecard, while a good deal of
I2-shaped work landed out of order — [doc 09](../09-Implementation-Roadmap.md)
§4 requires that state be recorded here, and it had not been. Two of the seven
criteria were met, three part-met and two not met.

**All seven are met as of 2026-09-21.** Three closed on 2026-09-19 (deletes,
replay, and the certification screen) and the last — the export round trip —
closed today. **I2 has no unmet criterion left.** Across the whole project one
remains open, `I1`'s OIDC federation, and it needs an identity provider to test
against. Re-read each of the seven before trusting this paragraph: it is a claim
about the code, and a claim nobody has re-checked since the last commit is a
guess.

- [x] **Killing and restarting a sync cannot lose committed records or advance an unsafe checkpoint.** Proven by five tests in `ConnectorRuntimeTests`: a provider that skips the start of a window **does not get the cursor moved past the hole**; a held window is asked for again next run rather than skipped; a provider that returns nothing still leaves a cursor a later run can use; a run that never finished leaves the evidence that it started; and two cursors for one feed are refused by the database. What is *not* tested is a literal process kill — the checkpoint safety is what is proven, and that is the substance of the criterion *(automated)*
- [x] **Duplicate, reordered, delete and partial-page tests pass** — all four, as of 2026-09-19. **Duplicate** holds three ways over: the same batch delivered twice produces one set of customers, importing the same file twice creates no second copy, and a record repeated *inside* one batch is applied once. **Partial page** is the cursor-hole test. **Reordered** is now two tests, because the criterion hides two different properties: independent records reach the same state in any order, and records about the *same* thing are applied in the order the provider sent them — "created then deleted" and "deleted then created" describe different days, and a sink that sorted its batch would turn one into the other. **Deletes are modelled** as a mark and never a removal (ADR-026)
- [x] **Quarantined records are inspectable and replayable** — both, as of 2026-09-19. Replay re-runs the stored payload through the **real sink** inside a transaction; there is no simulation mode and there must not be one. A replay that is refused again **leaves the row in the queue** with the *new* reason — fixing one mapping routinely reveals the next problem behind it, and resolving on attempt rather than on success would empty the queue without fixing anything. The row counts its attempts. Dismissing without replaying needs a reason in writing, because "Resolved" with no note is how a queue gets cleared by somebody who never read it. The payload is never returned by a read (ADR-022)
- [x] **A trial import is repeatable with stable counts and explicit exceptions.** A trial and the real run agree about an unusual VIN — the case where only the write would otherwise notice; the file is hashed so a trial and its run are provably about the same data; a bad row is reported by the line number a person sees in their spreadsheet and does not stop the others; and the counts add up to the row total *(automated)*
- [x] **Export round-trip tests preserve IDs, relationships and documents** — all three, as of 2026-09-21, through a **records package** (ADR-027) that sits beside the two CSVs rather than replacing them. **IDs are kept, not translated**: every record carries its own id, every reference is that id, and the receiving installation stores the same keys — which is only possible because all five aggregates already took an explicit id in their factories. **Relationships** are asserted one hop out, where a flat export cannot reach: the deal names the customer and the unit, and the unit names the same car the job does. **Documents** are the interesting third, because this system stores none — `IDocuments` owns no data and there is no file storage anywhere in `src`, so "preserving a document" has to mean the renderer produces the same page on the far side. `The_paperwork_comes_out_the_same_on_the_other_side` renders the vehicle order and the service invoice on both sides and compares the money tables character for character, which is a stronger claim than shipping HTML would have been: it fails if any field behind the page was lost, including ones nobody thought to assert. Also proven: re-running is a no-op, the array order in the file does not matter, a record whose reference did not arrive is refused by name while the rest land, and a deal whose lines no longer reach the total it claims is refused rather than stored quietly wrong *(automated, 11 tests)*
- [x] **Fixture-tested status is displayed honestly** — displayed, as of 2026-09-19, at `/integrations`. The certification is the headline rather than a footnote, and it is shown beside **what that level actually promises** — "automated tests only; not a promise that it works against a real provider" — because the level alone means nothing to a reader. The connector's own declared limitations sit beside it whatever the level says, so the fixture's "Serves fabricated records. Never certify anything against this." is on the screen. Fixture-tested and experimental wear the same warning colour deliberately: a dealership running its month-end on either is taking the same kind of risk
- [x] **Production certification remains incomplete until external evidence exists.** Nothing anywhere claims `SandboxCertified` or `ProductionCertified`; the only connector declares `FixtureTested` and is a test double. Trivially met, and worth recording because it is the criterion most easily broken by an optimistic edit *(automated)*

**Named and not built**, from the phase's own build list, re-checked by name
across `src` on 2026-09-21: a durable **inbox**, an **outbox**, **leases**, a
generic **SFTP** path, **profiling**, **versioned mappings**, and the
**duplicate-candidate workflow**. **Replay came off this list on 2026-09-19** and
the sentence saying it had not been written was left standing for two days; it is
struck now. Field ownership is still a named concern in a comment in
`CustomerRecordSink` and not a rule anything enforces.

The one that matters most of those left is the **unattended trigger** — an inbox
or a webhook that starts a run with nobody at a keyboard. Every run today is
asked for by a person, which is fine for a migration and useless for keeping in
step with a live system overnight. It is the whole of stage 2's remainder.

What *does* exist is the seam they would hang from: compiled connector
discovery, a versioned contract envelope, per-feed cursors with hold counting,
quarantine with retention and replay, run history, a CSV import/export path with
control totals and checksums, and a records package that moves a lot between
installations with its ids and references intact.
## Completed milestones

- **2026-07-25 — Engineering baseline.** Solution, Core kernel, architecture tests, CI, health endpoints, telemetry. Evidence: `dotnet build` 0/0, `dotnet test` 5/5.
- **2026-07-25 — Tenancy and Organization module.** Host catalog, cached tenant resolver, tenant middleware, organization→legal entity→rooftop→department model, EF migrations, development seeder. Evidence: `verify-e2e.ps1` → `PASS` (two isolated databases, multi-rooftop resolved, 400/404 contract), verified twice including an idempotent re-run.
- **2026-07-25 — Baseline committed.** `ef8793a` docs, `dd7b582` engineering baseline, on `feature/foundation`.
- **2026-07-25 — I0 gaps closed.** Tenant isolation moved from a manual script into CI-runnable integration tests; forbidden-reference rehearsal performed and documented; code of conduct and 16 immutable ADR files added; the verified local environment is recorded in `docs/LOCAL-DEVELOPMENT.md`. Evidence: `dotnet build` 0/0, `dotnet test` 13/13.
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
| When does an accounting month close? | **Answered 2026-08-06 by the maintainer.** The **cutoff is the calendar month end** (the 30th or 31st). The close then runs over **the next few business days** — reconciling accounts, posting adjustments, reviewing statements — and the month is **locked at the end of that**. Fiscal year = calendar year. Per organization. | **Recorded, not enforced.** Supersedes the earlier assumed rule ("prior-month entries until the 10th, then locked"), which was wrong in a way that matters: **locking is an act somebody performs, not a date that passes.** The close window has no fixed length, so the model needs an accounting period with a state and an explicit Close operation — not a date comparison. See `src/App/Accounting/README.md`. |
| How should documents be produced? | **Answered 2026-08-06.** Server-rendered HTML with a print stylesheet; the customer's copy comes from the browser's own print-to-PDF. | **Recorded, not built.** Chosen over a PDF library specifically to avoid a new dependency and its licence question — an AGPL project cannot take a component with an incompatible licence, and `NuGetAudit` fails the build on a vulnerable one. Keep the endpoint shape so a PDF renderer could replace the output later without changing the flow. |
| Where is a new dealership created from? | **Answered 2026-08-06.** The control-plane console, alongside the listing and suspend/resume already there. | **Recorded, not built.** A maintenance command was rejected because a hosted operator has no shell — and this must work for all three deployment targets. Whatever is built must **open the books**, or the new dealership's first sale is refused for a reason nobody will guess. |
| Where does this deploy? | **All three:** Windows service, Linux container, and hosted. | **Acted on.** DPAPI ruled out as Windows-only; replaced with AES-256-GCM envelope encryption keyed from configuration, which every target supplies the same way (`Secrets__Keys__<id>`). `deploy/README.md` covers all three. |

- **2026-07-31 — Secret protection, so this can ship at all.** Tenant connection strings are encrypted at rest with AES-256-GCM: a fresh nonce per value, an authentication tag so a tampered value fails rather than decrypting to something wrong, and a key id carried with the ciphertext so a key can be rotated without making existing data unreadable. Cross-platform, because all three deployment targets are wanted. Evidence: `dotnet test` 258/258, including a tamper test and a rotation test. **Gap named, not hidden:** nothing re-encrypts values under a new key, so rotation today means adding a key and keeping the old one; and switching an existing installation from the development pass-through needs a migration pass that does not exist.

- **2026-07-31 — A second factor at sign-in.** Opt-in TOTP: a password now buys a challenge rather than a session for an enrolled account, and only a code from an authenticator app — or a single-use recovery code — completes it. The secret is encrypted at rest with the protector built earlier the same day, enrolment is two-phase so a mis-scanned QR cannot lock somebody out, and guessing is cut off after five wrong codes. Verified against the published RFC 6238 test vectors, which is the only way to know real phones will agree. Evidence: `dotnet test` 282/282, plus a rehearsal in which silently skipping the challenge failed exactly the four tests that demand it. **Not yet:** requiring MFA by policy, and OIDC — which needs an identity provider to test against.

- **2026-07-31 — Each test run gets its own database.** The integration suite used to share one long-lived database, and assertions quietly became order-dependent as rows accumulated: three separate tests failed over time because a freshly created row fell off the end of a capped, sorted page. Each run now creates databases named for the run and drops them afterwards, sweeping anything a crashed run left behind. Tenant database names derive from the host catalog's name, so this needed no test-only branch in the seeder — and an installation whose catalog is named something else now keeps its databases together, which was arguably a latent bug. Evidence: `dotnet test` 282/282 with the integration suite unchanged at 7 seconds, and no `DealerFOSS_Test_*` database surviving the run.

- **2026-07-31 — A frontend, written but not yet run.** `frontend/` is a Vite + React + TypeScript project: an API client that carries the tenant header and the session cookie, a sign-in screen that handles the second-factor step, an authenticated shell with a skip link and keyboard-visible focus, and an inventory list rendering every state a real screen needs. The dev server proxies `/api` because the session cookie is `SameSite=Strict` and would otherwise be dropped on a cross-origin call. **Explicitly unverified:** no `npm install`, no typecheck, no browser. Treat every line of it as unproven until the container has run it once.

- **2026-07-31 — The ledger produces totals.** `GET /api/v1/accounting/balances` groups posted lines per account over a period and states each balance on the account's normal side, so an asset with more debits than credits reads positive. It reports whether the two columns agree — the headline of a trial balance is whether it balances, and a difference means something was lost on the way in. Rooftop-scoped, because a total is as revealing as the entries behind it, and it refuses to sum two currencies rather than printing a number that means nothing. Evidence: `dotnet test` 285/285, plus a rehearsal in which inverting the scope filter failed the totals test.

- **2026-08-01 — A forged write is refused.** A write now needs more than the browser's cookies. Signing in hands the browser a second secret, and every change it asks for must carry that secret in a header — which is the one thing a malicious site cannot add to a request it causes your browser to send. The secret is stored only as a hash, belongs to one session, and stops working the moment that session ends, so a token borrowed from another sign-in is refused too. Sessions that existed before this change cannot write until the user signs in again, which is the safe direction to fail. The end-to-end script now shows both halves: the same customer, refused without the token and accepted with it. Evidence: `dotnet test` 293/293 and `verify-e2e.ps1` PASS, plus a rehearsal in which switching the check off failed exactly the three tests that demand a refusal. **Also fixed here:** the verification script used to bind port 5080 silently, and if a development host was already running it would test *that* process instead of the build in front of it — it now refuses to start and takes `-Port`.

- **2026-08-01 — A dealership can demand a second factor instead of hoping for one.** Requiring it is a property of the *role*, so a salesperson hired next year is covered on their first day without anybody remembering to add them. Turning it on takes effect on the next request, not at the next sign-in — a rule that waits eight hours for everyone to sign out is not a rule. And it locks nobody out: somebody who owes a second factor still signs in normally, is told exactly what to do, and can reach the enrolment path and nothing else until they have done it. Turning off their own second factor is deliberately not one of the things they can reach. Only an organization-wide permission can change the policy, so one lot cannot set the group's rules. Evidence: `dotnet test` 301/301 and `verify-e2e.ps1` PASS — which now shows a salesperson mid-session being refused the moment the policy is switched on, still able to reach the way out, and the manager unaffected — plus a rehearsal in which removing the enforcement failed exactly the two tests that demand a refusal. **Not included:** a screen for any of it. Enrolment needs a QR code the frontend cannot yet draw, so this is API-only today.

- **2026-08-03 — Whoever runs the servers is kept out of the dealership's data.** Operating the installation and reading a customer record are now different jobs, held by different records, in different databases. An administrator signs in at a separate door with a separate cookie, and there is no code path by which they become a dealership caller — not a permission they lack, but a type they never become. The most privileged dealership account is refused at the control plane just as firmly, which is the half people forget. When a dealership does ask for help, support goes in through a door that has to be opened on purpose: a written reason, an hour at most, read-only, and an entry in the **dealership's own** log naming who came in and why — because visibility that exists only in the vendor's console is not visibility. Closing it stops the session on the very next request. An administrator must hold a second factor before any of this is reachable; the account that can step into any dealership is the one worth stealing. Evidence: `dotnet test` 321/321 and `verify-e2e.ps1` PASS twice, which now shows both refusals, an administrator enrolling and only then being let in, a day-long request granted an hour, support reading the stock list but refused a customer, the dealership's log naming the visitor, and the session dying the moment the grant is closed. Three rehearsals: making support writable failed exactly one test, removing the second-factor gate exactly two, and turning an administrator into a tenant caller exactly the architecture rule. **Not included:** creating a second administrator through an endpoint, recovery codes for one, dual approval, provisioning a dealership database from the control plane, and a screen for any of it.

- **2026-08-03 — The frontend is proven to render, and setting up a second factor has a screen.** "The frontend has never been seen rendering" was an honest limitation that nobody could close without opening a browser — and every screen built on top of it inherited the doubt. It is now closed by machine: 27 component tests mount the real React tree, the real router, and the real screens into a DOM and read what a person would read. Routing runs, the shell paints, the stock list draws a car, and each of loading, empty, refused, failed-with-retry, and unreachable renders the words it should. On that base, the enrolment screen: a scannable QR code, a typeable secret for people with no camera to point, a plain statement that nothing about signing in has changed until a code is accepted, and the recovery codes shown once with the reason they cannot be shown twice. Somebody whose role obliges them to hold a second factor is routed there and nowhere else — with sign-out still reachable, because locking a person into a screen with no way off a shared machine turns a safeguard into a trap. Confirming lifts the restriction without a second sign-in. **Also fixed in passing:** react-router carried a CSRF advisory (GHSA-qwww-vcr4-c8h2, affecting 7.12–8.2) that nothing was watching for — upgraded to 8.3, and `npm audit --audit-level=high` now runs in CI so the next one fails a build instead of waiting to be noticed. `contracts.ts` had also drifted from the API, missing `mustEnrolSecondFactor`. Evidence: `npm audit` clean, `npm run typecheck`, `npm test` 27/27, `npm run build`. Four rehearsals, each failing exactly the tests that guard it: breaking a route, dropping the tenant on sign-out, not applying the restricted routing, and discarding the recovery codes *(automated)*

  **Deliberately not included:** screens for customers, leads, deals, or the control plane; turning a second factor *off* from a screen; and any claim about how it looks. jsdom proves the components render and behave — it says nothing about fonts, spacing, dark-mode contrast, or whether a phone camera can read that QR code. Those need a person.

- **2026-08-03 — A console for whoever runs the installation.** The control plane stops being reachable only by `curl`. `/admin` is a second application in the same bundle: sign in with administrator credentials and a code, enrol a second factor if the account has none, see every dealership with its state and schema version, suspend or resume one, and open a support visit with a written reason — then see the whole record, closed visits included, and close an open one. The **routing split sits above `SessionProvider`**, so the dealership and control-plane session contexts are never mounted together and no screen can hold an identity without knowing which kind it is. There is a **second API client** (`shared/adminApi.ts`) rather than a flag on the first: the two worlds use different cookies and different anti-forgery headers precisely because one browser holds both sets during a support visit, and a client choosing between them from a boolean would eventually choose wrong. Tests assert it in both directions. Suspension asks for confirmation in the browser — it is a real outage for real people, and the server is right to do exactly what a properly authorized request tells it to, so the check belongs here. The console is visually marked so nobody mistakes it for the product. Evidence: `npm audit` clean, `npm run typecheck`, `npm test` 44/44, `npm run build`. Three rehearsals, each failing exactly the tests that guard it: leaking a tenant header from the control plane (2), giving both clients the same cookie and header names (2), and suspending without asking (1) *(automated)*

  **Deliberately not included:** creating an administrator, recovery codes for one, dual approval, provisioning a dealership, and any screen for customers, leads, or deals. Also unchanged: **no claim about how it looks.** jsdom has no layout engine.

- **2026-08-03 — Stage 2 opens: a dealership's existing records can be imported from a file.** The stage that gates onboarding a real dealership was at zero; it now has its spine. `POST /api/v1/migration/imports` validates the columns, stages every row **exactly as it arrived**, and answers 202 with a job — nothing is imported by the request, because a real extract is tens of thousands of rows and a request that tried to finish would time out half way with no record of where it stopped. A background worker does the work and the job is the reconciliation report. Three properties carry the design. **A trial changes nothing and says what the real run will do** — both modes take the same validation and the same natural-key lookup, and only the final write is skipped; `Vin.IsWellFormed` moved into the runner precisely because a check only the write performs makes a trial optimistic, and a test asserts the two agree about an unusual VIN. **Importing the same file twice duplicates nothing** — vehicles match on VIN, customers on `externalid` via a new `Customer.ExternalReference` under a filtered unique index. **The counts add up** — every row is Created, Updated, Skipped, or Failed, and they sum to the total. Failures name the line number as a spreadsheet counts it and quote the row back unaltered, because the workflow forbids resolving an exception by editing what the dealership sent. Also delivered, and the reason this milestone was worth doing now: **tenant-aware background work**, an outstanding I1 item. `ITenantScopeFactory` is the only sanctioned way for non-request work to reach tenant data; it demands the tenant be named, and the worker runs as the user who submitted the job so the import is authorized by their permissions and audited under their name. Evidence: `dotnet test` 348/348 (was 321). Three rehearsals, each failing exactly the tests that guard it: making a trial write (1), removing the natural-key lookup (1), and shifting row numbering by one (loudly — the first data row collides with the staged header) *(automated)*

  **Deliberately not included:** updating a matched customer's details from a file — a matched row is reported and left alone, because deciding that a file outranks what staff have since typed is a policy nobody has set. Also excluded: deletions and tombstones, profiling and duplicate detection before a run, export, cancelling a running job, connectors of any kind, and any screen. Named rather than hidden: every imported record writes its own audit entry, so a 20,000-row import writes 20,000 of them.

- **2026-08-03 — The screens were opened on a real browser, and three defects only a browser could find are fixed.** A standing assumption — that an automated browser could not reach this machine's localhost, and so "does it render?" and "how does it look?" were permanently a person's to answer — turned out to be false. Every screen was signed into and walked at 1280 and 375. What the DOM tests could not have told anyone: **(1)** the stock list captioned itself "200 vehicles in stock" when 200 is the *server's page cap*, so a dealership with 400 cars would be told, on the screen they use to count their own stock, that they have 200 — it now says "the first 200, there may be more", in the caption for a screen reader and as a visible note; **(2)** the administrator sign-in was near-identical to the dealership one, so the visual distinction the console was given only appeared *after* signing in, which is the wrong side of the moment somebody types a password — it now carries the same badge and accent rule; **(3)** Vite's file watcher does not see edits across the Windows bind mount, so hot reload was silently dead and even a hard refresh served the previous bundle — reading as "my change did nothing" rather than as a broken watcher. Polling is now enabled with a note saying why. Confirmed good and left alone: no horizontal page scroll at either width, the wide table scrolling inside its own container, the trial balance's verdict line and tabular figures, and the QR code drawn on a white quiet zone regardless of theme. Evidence: `npm test` 46/46 (was 44), and screenshots at both widths. Still needing a person: whether a phone camera physically reads that QR *(automated)*

- **2026-08-03 — A dealership can take its records away, and load them somewhere else.** `GET /api/v1/migration/exports/{kind}` returns a CSV whose columns are **exactly what the importer reads**. That is the promise an open DMS makes rather than a formatting choice: leave, and take your data, without anybody here writing you a converter. Proven by round trip, not by assertion — `ExportTests` exports one tenant and feeds that exact file to a *different* one through the ordinary import endpoint, and `verify-e2e.ps1` does the same on a live host: **899 vehicles out of northgroup, all 899 read back into citymotors, none refused.** The round trip earned its place immediately by catching the exporter dropping `vinexceptionreason` — which made every trailer and pre-1981 vehicle un-importable, because the written reason that permitted the unusual VIN was gone. Nothing but a round trip would have found that. Every field is quoted unconditionally, so a name like `Bob "Big Bob" Special, Ltd` survives; a hand-typed customer exports under its own id, because one with no external reference would be refused on the way back in and an un-importable export is not an export; and `X-Content-SHA256` rides on the response, because a file truncated in transit looks like data. Exporting is its own permission — bulk personal data leaving the building is a different act from loading a supplier's stock list — and organization-wide only. Reads use keyset paging, so a long export cannot skip or repeat somebody under a concurrent insert. Evidence: `dotnet test` 355/355 and `verify-e2e.ps1` PASS. Two rehearsals: renaming an exported column failed exactly the two tests that guard the contract; dropping the quoting failed exactly the round trip *(automated)*

  **Deliberately not included:** leads, deals, and the ledger have no path either way, so "take your data" covers the two record types a dealership migrates first and not yet the whole business. Streaming past 50,000 rows, documents, relationships between records, and any screen.

- **2026-08-04 — Bringing records in and taking them out has a screen.** `/records` is the last API-only half of stage 2 made reachable. One rule on it is a safeguard rather than a preference: **the real import is unreachable until a practice run has finished on this exact file**, and the lock re-arms whenever the file or the kind changes, because a rehearsal of one file says nothing about another. Somebody is about to write thousands of rows into their own business's history; one click of friction prevents the mistake nobody can undo. The report then reads in the tense that matches — *"2 would be added"* for a practice, *"2 added"* for the real thing — and says plainly that a practice wrote nothing. Refused rows are listed by **the line number they see in their own spreadsheet**, with the row quoted back exactly, because the workflow is to fix the source rather than edit what was sent. Export is two download buttons that go through `fetch` rather than a plain link: a link cannot carry the tenant header, so the request would arrive belonging to no dealership and be refused. **Verified in a real browser** against the renamed containers — signed in, walked the screen, and downloaded an export whose checksum header, row count, and column line were all correct. `FileReader` is used rather than the tidier `Blob.text()` because jsdom does not implement `text()`; polyfilling it would have meant the tests exercised the polyfill instead of this code. Evidence: `npm test` 55/55 (was 46). Two rehearsals: removing the practice-first lock failed exactly three tests, and treating a queued job as finished failed exactly the one that waits for the worker *(automated)*

  **Deliberately not included:** drag-and-drop, cancelling a running import, a progress bar for a job in flight (it polls and reports at the end), and choosing which columns to export. The file is read in the browser and posted as JSON, which suits the tens of thousands of rows the API caps at and not a real multi-megabyte extract — swapping in a multipart upload later changes the transport and not the flow.

- **2026-08-04 — A rehearsed backup and restore, which closes stage 1.** The deliverable is the drill, not the script: `backup.ps1` writes one `.bak` per database plus a manifest with a SHA-256 of each, and `restore.ps1 -Verify` puts them back **alongside** the original under a `Restored_` prefix — the only arrangement that proves anything, since a restore that overwrote the original would tell you nothing. **The drill immediately exposed a design hazard nobody had noticed:** each tenant's connection string lives in the host catalog *encrypted*, so a restored catalog still names the original databases — a "restored" installation would quietly read and write the live ones, which is worse than a restore that plainly failed. Only something holding the deployment's keys can rewrite those rows, so a maintenance verb was added to the application (`--repoint-tenants --prefix … [--server …] [--dry-run]`, idempotent) and the script calls it rather than being handed the keys. A second hole was closed on the way: `verify-e2e.ps1` seeds what it does not find, so an *empty* restore would have been seeded from scratch and passed — proving the application works and the backup does not. The drill now asserts the restored databases already hold rooftops and users before running it. Counting rows is not sufficient proof, but it is necessary. Evidence: 3 databases out and back on LocalDB, `verify-e2e.ps1` **PASS** against `Restored_DealerFOSS_Host`. Rehearsed: a tampered manifest checksum makes the restore refuse before touching anything *(automated)*

  **Deliberately not included:** copying backups off the host, encrypting them, scheduling, and retention — doc 08 owns those, and a `.bak` holds every customer record in plain form, so where the files go is a decision somebody must make on purpose. Tenant databases are found by naming convention rather than by decrypting the catalog; nothing creates an off-convention tenant today, and this is the script that changes if anything ever does.

- **2026-08-04 — The customer screen, with a duplicate check in front of every new record.** The first screen that is the everyday product rather than foundation. Search asks the server to do the matching — it is the only party that knows what this caller may see — across name, email, and phone. The part worth the effort is adding somebody: **the screen looks for them before it creates them**, searching each identifying field separately, because a duplicate usually differs in exactly one (the phone matches but the name is spelled differently; the surname matches but they used a work address). If anything is found it shows who, with enough to recognise them, and makes a person choose. It is deliberately **not** a refusal: two people genuinely do share a name, and a shop that cannot record the second one will get a fake name typed in instead. Two records for the same person is the failure that quietly makes a DMS untrustworthy — their service history splits and their deals sit under the wrong name — and nobody notices until it matters. The 100-row cap is stated honestly rather than presented as a total, the same defect the stock list had. **Verified in a real browser** against seeded data: typing an existing surname surfaced that customer and created nobody, confirmed by the absence of any POST. Evidence: `npm test` 70/70 (was 55). Two rehearsals: making the check never find anything failed exactly four tests, and checking only the name failed exactly the one that covers phone and email *(automated)*

  **Deliberately not included:** merging two records that are already duplicates, editing a customer, a customer detail page, and anything about their deals or vehicles. Search is capped and unpaged.

- **2026-08-04 — The deal desk has a screen.** The capability with the most logic already built and nothing to drive it. Lists what is being worked, opens one to show its charges, its trade-in, and what the customer owes, and moves it on: send to a manager, approve, hand the car over, mark lost. **The screen explains the rules rather than re-implementing them.** Only the transitions the deal's own stage allows are offered; whether *this caller* may make one is the server's answer, so a refusal is shown after asking rather than predicted. A salesperson cannot approve their own deal and the numbers freeze on submission — both stay in `DealService`, and the screen says what the rule is instead of becoming a second place for it to live, where the two copies would drift and the browser's would be the wrong one. **Verified in a real browser** against a seeded deal: opened it, approved it, watched the stage, the available action, and the history all follow. **A defect only looking could find:** the amount column is signed — a discount shows as −$500 — but the trade-in showed as +$3,300 while actually *reducing* what was due, so reading down the column gave $30,194 against a printed total of $23,594. The figures disagreed with each other on screen. Now negated, which also makes negative equity correctly *increase* the bill. Evidence: `npm test` 84/84 (was 70). Two rehearsals: swallowing the server's refusal failed exactly the test that demands it be shown, and offering moves on a finished deal failed exactly the one that forbids it *(automated)*

  **Deliberately not included:** starting a deal, editing the charges, entering a trade-in, F&I products, finance applications, tax and title, and printed paperwork. The desk shows and moves deals; building one is still an API call.

- **2026-08-04 — A deal can be built from the screen, and a session race that returned 500s is fixed.** Start a deal against a customer and an available car, enter and edit the charges, record a trade-in. Only cars that are genuinely available are offered — one on another deal is held — and the rooftop comes from the chosen car rather than being asked for again, since a car is on exactly one lot. The editor is mounted on `TermsAreOpen` and **disappears** once a deal is submitted rather than being disabled: a disabled form still looks like somewhere to type, and somebody would fill it in and lose the work. It is seeded from the deal's existing charges because saving replaces the whole set, so an empty form would mean editing one line deleted the rest.

  **The real find was a server bug this screen exposed.** `Authenticator.ValidateAsync` loaded the session row, stamped its last-seen time, and saved it through the change tracker. Two requests in flight on the same session — which is what a browser does constantly, and what this screen does on open — raced: the second found the concurrency stamp already moved and threw `DbUpdateConcurrencyException`, surfacing as **a 500 on whichever endpoint lost**. Latent since sessions landed, and invisible until a screen made two calls at once. Optimistic concurrency was the wrong tool: it exists to stop one edit silently overwriting another, and two requests a millisecond apart writing "last used" is not a conflict. Now a direct `ExecuteUpdateAsync` outside concurrency control; sliding expiry is unaffected. `AuthenticationTests.Several_requests_at_once_on_one_session_all_succeed` fires eight concurrent calls, and reverting the fix reproduces the failure exactly.

  **Verified in a real browser** against live data: created a deal from nothing, priced it at $41,500, and watched the panel and the list agree. Also fixed there: the charge-kind dropdown was clipping "VehiclePrice" to "VehiclePri". Evidence: `dotnet test` 356/356, `npm test` 97/97 (was 84). Three rehearsals: seeding the form empty failed four tests, leaving the editor mounted after submission failed two, and restoring the load-modify-save race failed the new concurrency test *(automated)*

  **Deliberately not included:** F&I products, finance applications, tax and title, printed paperwork, and editing anything after submission. Currency is fixed at USD until a dealership needs a second one, at which point it comes from the rooftop's legal entity rather than a box somebody types into.

- **2026-08-05 — The enquiry that comes before the deal has a screen, and the transition table stayed in one place.** `/leads` takes an enquiry against a customer with the car they asked about and their own words, lists what is being chased, and moves it on with a note on every step — including the lost lead who comes back, which reopens the first enquiry rather than starting a second that loses the history. A won enquiry opens the deal desk **with the buyer already chosen and the lead attached**, so `Deal.LeadId` is populated by the ordinary workflow instead of only by an API caller who remembered.

  **The design decision worth recording is where the rules live.** The deal desk hard-codes which transitions its stages allow, which was acceptable for five statuses but is exactly the duplication its own header comment warns about. `LeadStatusRules.MovesFrom` already existed and its doc comment already said it was "for a screen to offer" — so `LeadDetail` now carries `AvailableMoves`, and the screen renders what it is sent and holds no opinion of its own. `LeadTests.A_lead_reports_the_moves_it_allows_from_where_it_is` asserts the two agree at New and at Won. Whether *this caller* may make a move is still answered on the way in, unchanged.

  **Two scope rules held.** No rooftop picker on the list: `LeadService` already filters to the caller's authorized lots, so the list is already the right list and a picker would promise access the server refuses. On the capture form the rooftop **is** asked for, but only when the person actually works at more than one — a single-lot user is told which lot the enquiry belongs to instead of being given a choice of one.

  Also here: `IVehicles.GetManyAsync`, so the list can name the car in one query for the page rather than one per row — the same reasoning `ICustomers.GetManyAsync` was added for, and the alternative was an N+1 sitting next to a comment explaining why the line above avoids one.

  **Verified in a real browser** against seeded data: opened an enquiry at Appointment and saw exactly the three moves the domain allows from there, opened one at Working and saw its different three, claimed one and watched the list row change to "You", won it with a note that landed on the record, and followed "Build the deal" through to the desk with the buyer filled in and no picker offered. **Two defects only looking could find:** the seventh navigation link pushed the whole page into horizontal scroll at 375px — the nav now wraps; and the history list was rendering **numbered** while being ordered newest-first, so the most recent event read as "1." — step one, in the wrong direction. That one was pre-existing and shared with the deal desk. Evidence: `dotnet test` 357/357 (was 356), `npm test` 115/115 (was 97), `verify-e2e.ps1` PASS. Three rehearsals: hard-coding the transitions in the browser failed the test that sends an unexpected move list; dropping the note from the status call failed the test that reads it back; and reverting `GetManyAsync` to a per-row lookup was rejected on review rather than tested, since both spellings pass *(automated)*

  **Deliberately not included, and the reason:** handing an enquiry to a **named** colleague. Nothing in the product lists staff — there is no endpoint and no screen — so the alternatives were to invent a directory endpoint inside a leads milestone or to ship a pool model. Claim-and-release is the honest half; the named handover comes with the staff screen. Also excluded: follow-up reminders, appointments as diary entries, anything that messages a customer, and paging.

- **2026-08-05 — The workshop, and a bill nobody can pad.** The first capability outside selling cars. A car is booked in against the customer's *own vehicle* — not a unit in stock, because a customer's car is not on anybody's lot and modelling service against inventory would make the whole thing unusable the day after the warranty runs out. Work goes on as labour (hours × rate, kept separately so "how long did that take against what we charged" is answerable), parts, and sublet. Invoicing posts to the ledger in the same transaction as the status change, splitting labour, parts and sublet onto their own accounts — "we sold $464 of service" is useless to a workshop manager and "$180 labour, $284 parts" is what they run the department on.

  **The rule the capability exists to hold:** a job cannot be invoiced while any line is still waiting on the customer. A technician strips a wheel and finds the discs are gone — that is real work and should be recorded at once, but nobody may bill it until somebody has actually asked. So a line added while the job is still Booked is what the customer came in for and is authorized on arrival; a line added once work has started was *found*, and starts Pending. A Pending line blocks the invoice and **the refusal names it**, because a screen should send somebody to the phone rather than show them a generic conflict. **Declining is a perfectly good answer:** the line stays on the record at nil, which is what makes "we did offer" provable a year later when the same fault brings the same car back. Enforced on the entity, so an import or a background job cannot route around it.

  `Service.Write` writes work up; `Service.Authorize` records what the customer said. Unlike `Deals.Approve` there is deliberately **no ban on the same person doing both** — in an independent workshop the advisor who spots the work is usually the one who telephones, and forbidding that would stop real shops working. The control is that the answer is a distinct, permissioned, timestamped act carrying a note on how it was obtained. A seeded `Technician` role holds Write and not Authorize, which is what makes the split testable rather than a claim in a document.

  **A latent control failure was found and fixed on the way.** `IdentitySeeder` carried the comment *"Reversing is still a manager's job, because that is the operation that can hide a mistake"* — and nothing enforced it. Reversal shared `Accounting.Post` with posting, so the salesperson who delivers a car could unwind its own ledger entry. The comment had been a lie since the ledger landed, and no test caught it because the only account tested against reversal held neither permission. `Accounting.Reverse` is now its own right, held by the manager alone, with a test that reverses *as the salesperson who posted the entry* — the case that was silently open.

  Evidence: `dotnet test` 384/384 (was 356), `verify-e2e.ps1` **PASS**, which now books a car in, finds work on the ramp, refuses the invoice, refuses the technician's own authorization, then bills $464 and proves it posted as $180 labour and $284 parts on a balanced entry. Three rehearsals: removing the unanswered-work gate failed exactly the three tests that demand it, collapsing `Service.Authorize` into `Service.Write` failed exactly the technician test, and treating all work as authorized on arrival failed exactly twelve *(automated)*

  **Deliberately not included, and named rather than hidden:** there is **no parts inventory** — a part on a job is a description and a price, nothing is reserved or relieved from stock, so the ledger records service **revenue and no cost** and *gross profit on service does not exist yet*. Inventing a cost figure would be worse than the gap. Also excluded: estimate versus actual, a labour operation catalogue, flat-rate times, technician clocking and therefore any efficiency reporting, warranty claims, internal jobs, split-pay across customer/warranty/internal, appointments and workshop loading, a printed invoice, and **a screen** — the API is complete and nothing drives it.

- **2026-08-05 — A dealership can see and manage its own people, and a starter sets their own password.** Five gaps recorded across four earlier milestones turned out to be one missing capability: an enquiry could not be handed to a *named* colleague, the deal desk said "Somebody else" where a name belonged, the `DealerFOSS Support` role sat in every tenant a support visit had touched with **no way for the dealership to see it**, nobody could add a starter or stop a leaver, and a job could not be given to a named technician. All of it needed a staff list, and there wasn't one.

  **This opened Identity's sealed surface for the fourth time, deliberately.** `BoundaryTests` names every exported type and fails the build on a new one; `IStaffDirectory` and its records are now on that list with the reasoning beside them. What makes it safe is what it refuses to carry: no password hash, no TOTP secret, no session or recovery-code value. The projection is built by hand rather than mapped from the entity, so a field added to `User` later cannot cross the boundary unless somebody comes here and writes it out — and `No_credential_material_ever_appears_in_a_staff_response` reads the **raw JSON** rather than a typed model, because a typed check only catches fields somebody remembered to declare. Also absent, and a decision rather than an oversight: **when somebody last signed in.** Its honest use is spotting a dormant account; what it answers on a shared screen is "when was Dave working", and the audit trail is the right place to look when there is a reason to.

  **A starter never has a password typed for them.** There is no email sending, so there is no invite link — and the two easy answers (a manager choosing a temporary password, or an account nobody can use) both fail. Instead the account is created with **no credential at all**, and a single-use, hashed, 24-hour code is read out. `User.CanSignIn` already meant "active and holds a credential", so an un-enrolled starter is refused by machinery that already existed rather than by a new check somebody could forget. The code is 12 characters from an alphabet with O/0, I/1 and S/5 removed, because it is spoken across a desk far more often than copied.

  **Who may grant what is not uniform, and that is the point.** Handing somebody a role *at a rooftop* needs `Staff.Manage` at that rooftop; handing them *organization-wide* access needs it organization-wide. Without the split, whoever manages one lot could hand themselves the group. Stopping an account is organization-wide too — signing in is not a per-rooftop thing — and it **revokes live sessions in the same act**, so a leaver stops on the very next request rather than whenever their session would have lapsed. Nothing is deleted: their name still has to appear against the deals they did.

  **Two tests were found to be decoration, by rehearsal rather than by inspection.** Removing `enrolment.Consume(...)` left every test green — the "already has a password" guard fires first, so the consumed-code check is unreachable today. Removing the supersede loop was green too — redemption only ever looks at the newest live code. Both mechanisms were kept (they close a hazard the day a password reset exists) but the tests were **renamed to what they actually prove**, with the rehearsal result written down. Separately, `queryByLabelText('Hand to')` never matched anything: a wrapping `<label>` takes its accessible name from its whole `textContent`, which included every option — so two handover tests were passing vacuously. The label is now associated by id, and the rehearsal fails correctly.

  **Verified in a real browser, end to end:** added Rosa Delgado, read the code off the screen, signed out, redeemed it at `/set-password`, and signed in as her — where she correctly sees nothing, because she holds no role yet. **A defect only a browser could find:** the staff table was missing the `div.scroll` wrapper every other wide table uses, so at 375px its five columns pushed the whole page sideways instead of scrolling inside their own box. Measured, fixed, measured again (337 visible of 664, page not pushed). Evidence: `dotnet test` 401/401 (was 384), `npm test` 130/130 (was 115), `verify-e2e.ps1` PASS. Four rehearsals: leaking a password hash failed exactly the credential test, letting a rooftop-scoped manager grant organization-wide access failed exactly the escalation test, offering a role-less colleague failed exactly the dead-end test, and dropping the show-once warning failed exactly the test that demands it *(automated)*

  **Deliberately not included:** resetting a forgotten password (a different feature needing proof the asker owns the account — issuing a code to an enrolled account is refused rather than quietly becoming that feature), editing the permission catalogue, creating a role, merging or deleting a person, and any notion of a working pattern or shift. Named technician assignment on the workshop screen is now unblocked but not yet wired.

- **2026-08-05 — The workshop has a screen, and every capability now has one.** The last capability with real logic and nothing driving it. The list **leads with the calls somebody owes** rather than with a status filter: `linesAwaitingAnswer` is on the summary precisely so that panel can be drawn without opening anything, and every job on it is a phone call blocking an invoice.

  **Invoice is offered even when a line is unanswered, on purpose.** The screen asks and shows the refusal, because that refusal names the line — verified in a browser as *"1 line(s) are still waiting on the customer: Front discs and pads — worn beyond limit."* A greyed-out button would say less and would be a third copy of a rule that already lives in `RepairOrderStatusRules`. Same reasoning as the deal desk and the leads screen.

  **A flow defect that only walking it could find.** The answer button was gated on `linesAreOpen`, which is wrong: that flag governs editing the **work**, and the server's `AnswerLine` deliberately has no such check — because the real sequence is *finish the job → try to invoice → be told to ring → ring → record → invoice*. The screen told you to record what the customer said and had hidden the only way to do it. Every test passed; the browser found it in one click. Now gated on the line being Pending, with `Remove` and the write-up form still frozen. `still lets somebody record the answer after the work is frozen` guards it.

  **Verified in a browser, whole loop:** opened RO-1002, completed it, was refused the invoice by name, recorded *"Phoned 14:20, spoke to Mr Dhillon at Brightline"* against the line, and invoiced — ending at *"Invoiced 8/6/2026. Nothing more to do."* Layout measured at 375px: all three tables contained (287 of 356, 337 of 337, 337 of 569) and the page not pushed sideways, with a ninth nav link. One backend change: `RepairOrderDetail.OpenedAt`, which the summary already carried — "how long has this been sitting here" is the first thing anybody asks on opening a job. Evidence: `dotnet test` 401/401, `npm test` 149/149 (was 130), `verify-e2e.ps1` PASS. Three rehearsals, each failing exactly the test that guards it: predicting the invoice refusal instead of asking, dropping the note from the customer's answer, and the flow defect above *(automated)*

  **Deliberately not included:** a printed invoice, appointments and workshop loading, editing a line's amount once written up, and anything about parts stock — which is the next thing this department needs, because the workshop records what a job billed and nothing about what it cost.

- **2026-08-06 — Parts are real stock, and service finally has a profit figure.** Until now a part on a repair order was a description and a price somebody typed. Invoicing recorded revenue and no cost, so "what did the workshop actually make" had no answer — the gap was named in `ServiceInvoicePosting`'s own remarks. It is closed: parts leave the shelf when a job is invoiced, in the same transaction as the invoice and the ledger entry, and the books carry cost of parts sales against parts inventory.

  **The costing method is the manager's to choose** (maintainer's decision, 2026-08-06), defaulting to moving average, with last-cost and FIFO available. That choice forced the central design decision: **stock is held as receipt layers, always** — not as a running total plus an average. If it were, an organization that later switched to FIFO would have no delivery history to consume and would silently produce wrong costs from the day it switched. Keeping layers means all three methods read the same data, so switching is safe at any moment and needs no migration. `Every_method_reads_the_same_layers_so_switching_needs_no_migration` is the test that says so.

  **Switching affects future sales only, and the screen says so out loud.** A sold line freezes its cost and the ledger is immutable, so changing the method cannot restate a month already reported on. A manager who believed otherwise would have been misled by the control, which is why that sentence is a guarded assertion rather than a nicety.

  **Stock cannot go negative.** Invoicing a job for parts that are not there is refused with a message naming the part and the shortfall, and the refusal rolls the whole invoice back. A workshop that can sell parts it does not have has no stock figure at all.

  **Scope follows the same shape as everything else.** A part *number* is organization-wide — it means the same component at every location, so adding to the catalogue and changing the costing method both need organization-wide permission. The *stock* is rooftop-owned, like `InventoryUnit`: two lots holding the same number hold two different piles. Booking a delivery in is checked at that rooftop.

  **Two defects found by walking it, neither visible to any test.** A newly catalogued part was **invisible** — the list only emitted a row per shelf that had stock, so a new part could never be opened, and a part that cannot be opened cannot have stock booked onto it. A closed loop with no way in; the list now shows unstocked parts with a null rooftop and "Not stocked". Separately, EF warned at startup that `ServiceLine.CostAmount` and `PartQuantity` had no precision configured and **would be silently truncated** — now (18,4) and (18,3), matching the receipt they come from, because a part costing 0.0125 each is ordinary and fractions of a litre are how fluids are issued.

  **Verified in a browser:** added a part typed as `mz-690 411` (stored as `MZ690411`), booked in 10 at 5.00 and 10 at 9.00, saw 20 on hand at **7.00** average with both delivery notes listed, switched to FIFO and watched the unit cost move to **5.00**, switched back. Layout measured at 375px: both tables contained, page not pushed, ten nav links. Evidence: `dotnet test` 428/428 (was 401), `npm test` 159/159 (was 149), `verify-e2e.ps1` PASS — which now books stock in, sells two, and checks the cost, the shelf, and both sides of the ledger entry on a live host. Three rehearsals, each failing exactly the tests that guard it: dropping the frozen cost (2), allowing negative stock (3), and removing the "future sales only" warning (1) *(automated)*

  **Deliberately not included:** purchase orders and supplier records, stock takes and adjustments, bins and locations, superseded part numbers, returns to supplier, and any link to a manufacturer's parts catalogue. Also: a part typed by hand still bills and carries **no** cost rather than a zero — those are different things, and a zero would read as free.

- **2026-08-06 — The month can be closed, and a closed month refuses.** The ledger recorded entries and totalled accounts, and nothing stopped a posting landing in a month somebody had already reported on. That is the difference between a running total and a set of books, and it is now closed.

  **Locking is an act somebody performs, not a date that passes** — the maintainer's answer, and the whole shape of the design. The cutoff is the calendar month end, but the close then runs over however many business days the reconciling and adjusting takes. Built as a date comparison it would either lock a month somebody was still working on or leave one open because nobody's calendar said otherwise. So: an `AccountingPeriod` with a state and an explicit `Close`. An adjustment posted *during* the close belongs in the month being closed, which is exactly why `Open` is the state that accepts postings rather than "the current month".

  **Reopening is its own permission** (maintainer's decision). `Accounting.ClosePeriod` is routine month-end work; `Accounting.ReopenPeriod` lets a figure somebody already reported move, so it is deliberately holdable by fewer people than the job that closed it. Both organization-wide — the books close as a whole, not one lot at a time. A reopen **requires a written reason**, and every transition is kept as `IAppendOnly` history, because closed → reopened → changed is precisely the sequence an auditor needs to reconstruct.

  **The books have a deliberate beginning** (maintainer's decision): nothing posts into a month that has not been opened. That is a real consequence and it is handled rather than discovered — the refusal for a never-opened month is a *different* message from the refusal for a closed one, because they need opposite actions, and `DevelopmentSeeder` opens the books as part of setting a dealership up.

  **A reversal still works against a closed month**, and that is not a loophole. A reversal is dated today, so it lands in today's month and leaves the closed month exactly as it was reported — which is the entire reason reversals exist rather than edits.

  **Verified in a browser:** closed July, watched the history appear, opened the reopen panel and confirmed the button stays disabled with no reason typed, reopened with *"A supplier invoice arrived on the 4th"*, and saw it land on the record. Layout measured at 375px with an eleventh nav link: table contained, page not pushed. Evidence: `dotnet test` 439/439 (was 428), `npm test` 170/170 (was 159), `verify-e2e.ps1` PASS — which now closes the month on a live host, is refused a posting with 409, is refused a reasonless reopen with 400, reopens, invoices, and checks the reason is in the period's history. Three rehearsals, each failing exactly the test that guards it: letting a closed month accept a posting, dropping the reopen-reason requirement, and closing without asking on screen *(automated)*

  **Deliberately not included:** year-end close and retained-earnings roll-up, comparative statements, budgets, statutory reporting formats, and any automatic closing. Named rather than hidden: closing does not yet check that anything is *reconciled* — it locks the month, and whether the work was actually done is still a person's judgement.

- **2026-08-06 — Warranties and cover are sold with the car, and they have their own gross.** On many real deals the back end makes more than the car does, and none of it existed. Now: a catalogue of products with a provider and default figures, a menu on the deal, and F&I revenue and cost posting to their own ledger accounts on delivery.

  **Price and cost are copied onto the sale, not read from the catalogue.** F&I is negotiated — the same warranty goes out at different prices on different deals — so the figures typed on the deal are the ones recorded, and the catalogue supplies only the name. This is the same hazard as a part's cost and it has the same answer: repricing the catalogue must never rewrite what an earlier deal made. **Two tests hold the two halves of that rule, and rehearsal proved they catch different things:** reading the catalogue price at sale time fails only `The_gross_is_what_it_sold_for_less_what_it_cost`, while `Repricing_the_catalogue_does_not_touch_a_deal_already_done` covers the later-change direction.

  **Withdrawing a product does not unsell it.** A withdrawn product cannot be added to a new deal, and deals that already carry it are untouched — including on the screen, which keeps a withdrawn line visible and ticked so saving cannot silently drop it. Selling below cost is deliberately allowed: it is a real decision made to hold a deal together, and a system that forbids it just gets a false cost typed in instead.

  **A defect found by reading down a column.** The deal summary listed the vehicle price of $41,500 above a total of $42,450 with nothing explaining the difference — products were on the bill but not in the table that adds up to it. Exactly the flaw the trade-in had, found the same way. Now listed as their own rows.

  **A correction worth recording.** I also reported the deal desk as scrolling sideways at 375px. It was not. `document.documentElement.scrollWidth` over-reports whenever `overflow-x: auto` containers are present, and this application wraps every wide table in one; the page never actually scrolled, confirmed with `window.scrollTo`. The charges table was genuinely the one table left unwrapped and is now wrapped for consistency — but it was not causing a bug. **The honest test for horizontal scroll is whether the page moves, not what `scrollWidth` claims.**

  Evidence: `dotnet test` 452/452 (was 439), `npm test` 179/179 (was 170), `verify-e2e.ps1` PASS — which now sells cover at a discount on a live host, checks the deal owes 20,900 and the cover made 200, reprices the catalogue and checks the deal still says 200. Verified in a browser: ticked a warranty, changed 1,200 to 950, watched the gross follow to 250, saved, and saw the summary column reach the total. Two rehearsals, each failing exactly the test that guards it: reading the catalogue price at sale time, and sending the default instead of the typed price from the screen *(automated)*

  **Deliberately not included:** cancellations and pro-rata refunds, remitting to providers and reconciling with them, commission and pay plans, rate tables by term and mileage, e-contracting, and the compliance "menu" presentation. Also not included: a screen for managing the catalogue itself — products are added through the API today, and the menu on the deal is what got the screen.

- **2026-08-07 — The customer is handed something.** A car could be delivered and a job invoiced and the person paying walked away with nothing. There is now a **vehicle order** and a **service invoice**, rendered server-side as complete standalone HTML with a print stylesheet, opened from the deal desk and the workshop.

  **No PDF library was taken** (maintainer's decision, 2026-08-06). This is an AGPL project and `NuGetAudit` fails the build on a vulnerable package, so a rendering component would need to clear both a licence review and an advisory history. A printable page needs neither, and the browser's own print-to-PDF produces the customer's copy. `RenderedDocument` carries a content type and file name precisely so a PDF renderer can replace the output later without touching a single caller.

  **The safeguard that justified the whole shape.** `DealDetail` carries the cost and gross of every F&I product; `RepairOrderDetail` carries the cost of every part. **None of it is rendered**, and `No_dealership_only_figure_reaches_a_customers_copy` reads the raw HTML — not a model — to prove it, because the risk is a field somebody adds later without thinking about who sees the document. Rehearsed: rendering the product cost instead of its price fails exactly that test.

  **Documents own no data.** Everything is read through `IDeals`, `IRepairOrders`, `ICustomers`, and `IOrganization`, so the caller's permissions and rooftop scope are applied before anything is rendered — a document cannot show what the person asking could not already read. That is why the endpoint checks no permission of its own, and `Somebody_who_cannot_read_the_deal_cannot_print_it` is what proves that is sufficient rather than an omission.

  **Three defects found while building it.** Negative money rendered as `$-3,000.00` instead of `-$3,000.00`, which reads as a typo rather than as money off — the sign now sits outside the symbol. A handler named `print()` silently resolved to `window.print`, which would have printed the application page instead of the document; the compiler caught it as an unused local. And the print button was first placed among the stage buttons, which disappear once a deal is delivered — exactly when somebody asks for another copy — so it moved to the panel header.

  **Verified in a browser** on real data: the vehicle order reads 399 − 500 + 26,995 − 3,300 = **23,594**, matching its printed total, and the service invoice's 68.40 + 284.00 parts plus 180.00 labour reaches **532.40**. Also confirmed the design's premise: navigating straight to a document URL is refused for a missing tenant header, which is why `openDocument` fetches rather than using a link. Evidence: `dotnet test` 462/462 (was 452), `npm test` 180/180, `verify-e2e.ps1` PASS *(automated)*

  **Deliberately not included:** letterheads and per-dealership branding, emailing anything, statutory finance documents, terms and conditions text, and signature capture. Named rather than hidden: the vehicle order says on its face that it is not a tax invoice, because it is not one.

- **2026-08-07 — A dealership can be set up without a developer.** The gate in front of every pilot. Creating a tenant was a seeder run, so however good the rest got, none of it could be put in front of a real dealer. From the operator console it is now one form: the database is created and migrated, the organization and its first location are written, the chart of accounts is seeded, **the books are opened**, and one manager is created who sets their own password with a one-time code.

  **Opening the books is the step this milestone exists for.** Nothing posts into a month that has not been opened, so a dealership provisioned without it looks entirely healthy — the tenant resolves, sign-in works, screens load — right up until its first sale is refused for a reason nobody would guess. `A_new_dealership_can_record_a_sale_immediately` drives the whole path end to end and rehearsal confirms it is the only test that fails when the step is removed.

  **No password is ever invented.** The first manager gets the same enrolment code a starter gets, so there is exactly one place credentials are created rather than a second one nobody would think to harden. `IdentitySeeder.SeedRolesAsync` was extracted so a real dealership and the development seeder get an identical role catalogue — two would have drifted, and the version a paying dealership received would be the one nobody was testing against. Code generation moved to `StaffEnrolment` for the same reason.

  **The architecture tests moved this code twice, and were right both times.** It was written in `Administration`, which may not reference a business capability; then in `Tenancy`, which may not either. Provisioning both builds a database and writes an organization and a chart of accounts, so it belongs in neither — it is not a capability, it composes several, and it now sits in the composition root beside `DevelopmentSeeder`. The rules found that, not review.

  **An uppercase short name is refused rather than tidied.** Silently storing `upper` for a typed `UPPER` would be a surprise, and `TenantResolver` compares the slug raw against a cache keyed by the raw string — so normalising on write but not on read could behave differently on a cache hit than on a miss. Refusing keeps every stored slug lowercase and side-steps it.

  **The catalog row is written last**, so a run that fails half way leaves an orphaned database rather than a tenant that resolves to a broken one — the untidy failure instead of the dangerous one. Evidence: `dotnet test` 473/473 (was 462), `npm test` 182/182, `verify-e2e.ps1` PASS. Rehearsed: skipping the books fails exactly the end-to-end sale test *(automated)*

  **Deliberately not included:** deleting a dealership, moving one between servers, restoring one into a new tenant, choosing a database name by hand, and any second location beyond the first — that is the dealership's own job once they are in.

- **2026-08-07 — Security headers, a limit on guessing, an operator runbook, and a resolver bug that agreed only by coincidence.**

  **`docs/OPERATING.md`** is the runbook for whoever runs an installation: first start and the secret key that must be backed up separately, setting a dealership up, suspension, support visits, backups and the `--repoint-tenants` trap, a symptom table, and an explicit list of what the system will not do (nothing is emailed; there is no password reset; a second operator cannot be created).

  **Every response now carries security headers**, set by middleware registered *first* so a refused response carries them too — a header only present on the happy path is not a control, and that is the easy mistake. `frame-ancestors 'none'` matters specifically because the operator console has a Suspend button. The policy allows inline **styles** and forbids inline **script**: printed documents inline their whole stylesheet so they survive being saved and opened next year, while the half that turns a value into an execution stays shut.

  **Credential endpoints are rate limited** — sign-in, second factor, enrolment, and the control-plane door. Deliberately not applied to business endpoints, which would punish a busy dealership for being busy.

  **The limiter's first version had a real flaw, found by the suite going red.** Partitioning fell back to a constant `"unknown"` when there was no remote address, which put every caller in one bucket — twenty sign-ins from anywhere would have locked out everybody. That is also the shape of the production risk: a dealership behind one NAT shares an address. It now falls back to the connection id, and because the in-process test host is not a realistic caller (hundreds of sign-ins in seconds down one connection) **the limit is configurable, raised in `HostFixture`, and proven instead by `verify-e2e.ps1` against a real host over a real socket** — 40 wrong passwords, 27 refused. That is the more honest test.

  **`TenantResolver` was fixed.** It compared the tenant key raw: `TenantCache` is case-insensitive while `t.Slug == tenantKey` inherits the SQL Server's collation, so the two agreed only by coincidence. On a case-sensitive installation the same request would have succeeded or 404'd **depending on whether the cache was warm** — intermittent, and the worst kind to be handed. The key is now normalized once and the normalized form used for both, including `Invalidate`, which would otherwise have left a suspended dealership serving traffic for the rest of the cache's lifetime. A theory covers `northgroup`, `NORTHGROUP`, `NorthGroup`, and a padded variant.

  Evidence: `dotnet test` 483/483 (was 473), `npm test` 182/182, `verify-e2e.ps1` PASS — which now also asserts the headers and the throttling *(automated)*

  **Not included, and requested:** dashboards. Named here rather than half-built.

- **2026-08-07 — How did we do this month, answered in one screen and one call.** The figures existed; nothing put them in front of a dealer principal. There is now a landing screen showing front, back, and service gross against last month, cars delivered, jobs invoiced, gross per car, and how old the unsold stock is — plus whether the month's books are still open, because that is what decides whether any of it can still move.

  **The gross comes from the ledger, not from the deals.** A dashboard that added deal totals up would be a second opinion about the same month, and the two would eventually disagree with nobody able to say which was right. `IAccounting.PerformanceAsync` reads the same journal lines the trial balance reads, through the same rooftop scoping — one method, so a figure on a dashboard cannot cover a location the trial balance would not. The department split lives in Accounting because working out gross means knowing 4000 is a sale and 5000 is what it cost; aging lives in Inventory because it means knowing which statuses still count as stock. The new `Reporting` capability holds neither rule — it owns no table, no entity, and no `TenantDb`, which an architecture test now enforces.

  **Permission is per panel, not per page.** A salesperson who may read stock and not the ledger sees the stock and is told in words why the money is absent. A refusal becomes an absence; every other failure is still reported as itself, because two currencies in the ledger is a fact the reader needs and "not yours to see" is a fact about the reader. A caller entitled to nothing is refused outright, so an empty dashboard never silently means a forbidden one.

  **Three rehearsals, three catches.** Flipping the discount sign moved front gross by 600 and the delta test caught it; not subtracting reversals from the count left a car that had been un-sold still counted; overlapping the 30/31-day band boundary double-counted three units. The count and the money move together on purpose — otherwise somebody divides one by the other and gets a nonsense average per car.

  **Two figures refuse to lie in the quiet months.** A department that sold nothing has *no* margin rather than a margin of zero, and a month compared against one that made nothing says "up from nothing" rather than dividing by it. Both are only wrong in a new dealership's first month, which is the one nobody tests by hand.

  **The frontend gained the infrastructure the screen needed, and it applies everywhere.** An explicit light/dark/auto choice on `<html>`, resolved to a real theme in code so the stylesheet carries one palette instead of two copies; a direction toggle, with every physical `left`/`right` rule in the stylesheet converted to logical properties so the whole application mirrors; keyboard navigation (`g` then a letter, `[`/`]`/`t` on the dashboard) with a `?` panel generated from the same table the shell binds, so a shortcut cannot exist without being documented; and transitions expressed through one token that `prefers-reduced-motion` switches off for all of them at once.

  **Two colour pairs failed WCAG AA on measurement and were changed.** `--ink-3` was 4.0:1 on white — under AA for the small print it is used for, which is most of the labels in the application. `--accent` on `--accent-soft` was 4.1:1, the pairing behind every status chip and the active navigation pill. Worse, the dark theme's primary button was **white on light amber at 2.4:1**, hard-coded as `#fff` in two rules; it is now an `--on-accent` token that flips with the theme. Every measured pair in both themes now clears 4.5:1, the lowest being 4.51.

  **Verified in a browser**, in both themes and both directions: the month reads $532 service gross on 1 job from the seeded data, stepping back a month re-ages the stock as at that month's cutoff (26 days rather than 33) rather than showing today's, and the oldest-stock link lands on the stock list filtered to that one car. Evidence: `dotnet test` 509/509 (was 483), `npm test` 215/215 (was 182), `verify-e2e.ps1` PASS — which now also asserts the month reports 24,000 more front gross on one more car after a delivery, and returns to zero on both after the reversal *(automated)*

  **Known and not a defect:** in right-to-left mode the English strings show their full stops at the visual start. That is correct bidirectional rendering of left-to-right text in a right-to-left paragraph, and it goes away when the content itself is right-to-left. The layout mirrors; there are no translations yet.

  **Deliberately not included:** a per-rooftop breakdown side by side (the query takes one rooftop, so a group asks once per location), salesperson and advisor league tables, and anything daily.

- **2026-08-08 — Something an operator can actually install.** The runbook told somebody how to run an installation and there was nothing packaged for them to run. There are now two packages, and **the application serves its own web interface**, so an installation is one thing rather than an application plus a web server plus a proxy configuration.

  **A Linux container** — `deploy/Dockerfile`, built from source in three stages so the runtime image carries neither Node nor the SDK. The frontend is built *inside* the image rather than expected to exist beforehand, because an image that assumed it would sometimes be an image with an empty `wwwroot`: it starts, answers health, serves the API, and shows a blank page. `deploy/docker-compose.app.yml` runs it with SQL Server; every secret in it has no default, so compose fails loudly rather than starting an installation whose backups cannot be restored.

  **A Windows service** — `deploy/publish.ps1` builds the frontend, copies it into `wwwroot`, publishes, and refuses to produce a package whose `wwwroot` is empty. `deploy/install-service.ps1` registers it, sets restart-on-failure, and writes the connection string and key into **the service's own registry entry** rather than machine-wide: `setx /M` would make the key that decrypts every dealership's connection string readable by every process on the box. `UseWindowsService` and an explicit content root were both needed — without the first the SCM never sees "running" and `sc start` times out on a service that is actually up; without the second the service looks for `appsettings.json` and `wwwroot` in whatever directory the SCM chose.

  **Three defects found by running it rather than by reading it.**

  - **`deploy/new-key.ps1` did not work, and neither did the README snippet it replaced.** `[RandomNumberGenerator]::Fill` takes a `Span<byte>` and exists only on .NET Core 2.1+; Windows PowerShell 5.1 runs on .NET Framework and does not have it. **The documented first step of an installation had never worked on the documented shell** — it died with "does not contain a method named 'Fill'", which says nothing about keys.
  - **`docs/OPERATING.md` named the wrong variable.** `Secrets__ActiveKeyId`; the application reads `Secrets__CurrentKeyId`. Following the runbook exactly produced a refusal to start with a message about plaintext connection strings and no visible connection to the typo.
  - **`publish.ps1` failed on its first run for a reason that had nothing to do with publishing.** Windows PowerShell 5.1 turns *any* stderr output from a native command into a terminating error while `$ErrorActionPreference` is `Stop` — and `npm ci` prints a deprecation notice on a completely successful install. The script now judges native commands by their exit code.

  **Two routing traps, one of them rehearsed.** A matched endpoint beats static files: routing runs first, so the JSON identity document at `/` won over `index.html` and an operator opening the site for the first time was shown `{"name":"DealerFOSS"…}`. `UseDefaultFiles` does not fix that — it is the same collision — so the route is simply not mapped when there is a frontend to serve. And the shell fallback matches any path without a dot in it, which every mistyped API route is: **rehearsed by removing the `/api` guard, after which a signed-in caller asking for `/api/v1/organisation` got the application shell and HTTP 200.** A client then parses HTML looking for JSON and nothing reports an error. Both are now covered by `PackagedShellTests`.

  **Also caught:** `index.html` reached the browser with no cache header at all, because the middleware and the fallback endpoint are two different ways of sending the same file and each carries its own options. They now share one object. A cached shell points at asset filenames that no longer exist, which presents as a white page after an upgrade.

  **Verified by driving the published binary in a browser** with no dev server running anywhere: it signs in, shows the dashboard, and a deep link to `/accounting/periods` survives a real page load. Also verified it refuses to start without a key, and that a Production installation pointed at a Development-seeded catalog fails with a clear message — there is no upgrade path between them, and the symptom table now says so. Evidence: `dotnet test` 516/516 (was 509), `npm test` 215/215, `verify-e2e.ps1` PASS, `publish.ps1` produces a 22 MB folder that runs *(automated)*

  **One unrelated fix on the way past:** `npm audit` turned up a new high advisory in `nanoid` (via `vite` → `postcss`). Lockfile bumped 3.3.16 → 3.3.18; the audit gate is clean again.

  **Deliberately not included:** an installer with a user interface, automatic updates, a signed package, systemd units, Kubernetes manifests, and anything hosted-specific.

## Active risks and blockers

| Owner | Item | Required evidence | Effect |
|---|---|---|---|
| Human | Delivery Phase 0 — pilot dealers, provider access, sandbox data | Signed access and representative extracts | I2 connector certification cannot start |
| Human | Where backups are stored, and who holds the secret key copy | A named location off the server | An installation can be backed up but not survive losing the machine |
| Human | A mail provider, and a WhatsApp/SMS gateway account | Credentials in an installation's configuration | Those two recovery methods stay switched off; the other three work regardless |

**Closed 2026-08-07:** how somebody proves they own an account they cannot sign in
to. Answered — [ADR-018](../adr/0018-account-recovery-methods.md).

**Closed 2026-08-04:** backup and restore rehearsal. Listed here as an open human
item for three days after it was done — see line 9 and the stage-1 checklist. The
drill ran on LocalDB, three databases out and back, with `verify-e2e.ps1` passing
against the restored copy.

**Not blockers, and worth saying so:** install packaging and password reset are
both mine to build. Neither waits on a pilot dealer. Only the connectors and
single sign-on genuinely do — the first needs a real DMS to connect to, the
second a real login provider, and a fake one would prove nothing.

## Next milestone

**Not decided, and the reason is worth stating.** The three highest-value items
are not engineering, and none is blocked by anything in this repository:

- **Talk to five dealerships.** Nothing here has been seen by anybody who runs
  one. Every workflow encodes an assumption nobody has checked, and the cost of
  a wrong assumption grows with each capability built on top of it.
- **Apply for one vendor's API programme.** Delivery Phase 0 has not started and
  R01 rates the delay High. Applying costs an hour; access takes months, and
  nothing shortens that. The coexistence release cannot meet its own success
  criteria without it.
- **Decide the target market.** The compliance scope in doc 01 §4 is entirely US
  while the product ships six languages with RTL. Both cannot be the priority,
  and the answer decides whether the tax and title work is worth doing at all.

**Ready to build, when building is the right thing:**

- **Field ownership.** A record sink can insert but not update, so an existing
  customer is reported unchanged and left alone. Who wins when a provider and a
  member of staff disagree about a phone number is a product decision (doc 05
  §4), and any connector that syncs rather than seeds needs it settled.
- **Measure the performance target.** Doc 07 promises p95 under 500 ms at 50
  concurrent users and it has never been measured. Database-per-tenant carries a
  known cost — connection pool pressure, and migration time multiplied by tenant
  count — that has never been exercised. A day, and no external party needed.
- **A scheduler**, now that a run has somewhere to deliver. Nothing starts an
  integration unattended, and the quarantine expiry has nothing to purge it.

**Cautions carried forward:**

- **Settled 2026-08-06:** documents are server-rendered HTML with a print stylesheet, not a PDF library. Keep the endpoint shape if that ever changes.
- **Settled 2026-08-05:** a service advisor **may** authorize work they wrote up themselves. Not an oversight — most independents have one person doing both, and the control is that recording the customer's answer is a separate, permissioned, timestamped act. Do not "fix" it into the salesperson/approver split.

**Also outstanding, and cheap:** the API refuses a write whose anti-forgery token is missing, but no screen has yet had to recover from it. `ApiError.needsSignIn` covers the case and nothing acts on it — a write that fails this way should send the person to sign in again rather than showing them a raw refusal.

**Also outstanding:** leads, deals, and the ledger still have no import or export path, so "take your data with you" covers customers and vehicles and not yet the whole business.

**Closed 2026-08-05:** the `DealerFOSS Support` role used to sit in every tenant a support visit had touched with no way for the dealership to see it. `GET /api/v1/staff/roles` now lists every role and what holding it grants, so that residue is visible in the product rather than only in the audit trail.

---

## Milestone log

Newest last. Each entry says what now works that did not before, and names the
evidence. These used to accumulate under "Next milestone" — a heading they
outgrew — which made the document read as if a year of finished work were still
to come.

> **Every entry is a dated snapshot and is never revised.** A "still not built"
> note records what was missing *that day*, and several have since been built —
> the 2026-08-14 recall entry says "no screen", and there is one now. That is the
> log working as intended: it is the history, not the state.
>
> **For the state, use the "Does it exist?" table at the top of this file.** It
> is the single home for that question, and it is what the specification
> documents link to instead of each keeping their own answer.

- **2026-08-08 — The application speaks five languages, and Arabic turns the page round.** English, French, German, Russian and Arabic, with the direction of the page derived from the language rather than chosen beside it. There used to be an LTR/RTL toggle next to the theme, which made "Arabic, left to right" a selectable combination — a broken layout with a switch in front of it. Choosing a language now sets both `lang` and `dir` on `<html>`, and because the stylesheet was already written in logical properties, that one attribute mirrors every margin, border and table column at once.

  **Three properties make the translations hold up rather than merely exist.** English is the schema: the other four catalogues are typed against `keyof typeof en`, so a key added and not translated fails `npm run typecheck` instead of surfacing an English sentence mid-screen. A test additionally reads the files and fails on a translation that is character-for-character the English — which typecheck cannot see, and which is how a translation actually rots; the genuine loanwords (`GAP`, `VIN`, `Stock` in French, `Status` in German) are listed with their reason rather than the check being weakened. Plurals go through `Intl.PluralRules` and never `n === 1`: Russian needs four forms and Arabic six, and `1 vehicles in stock` was already wrong in English on a screen the dashboard links to. Numbers, money and dates go through the active locale, and **API enum values are translated rather than printed raw** — the stock list used to render `OnHold`, which nobody writes in any language.

  Codes carry `dir="ltr"`: VIN, stock number, account code, recovery codes, the TOTP secret, and the raw CSV row. A VIN reordered by the bidirectional algorithm is a different car, and a mis-transcribed enrolment secret locks somebody out of their own account.

  **Done: 12 of 25 screens** — the shell, both sign-in paths, first-password, two-step enrolment, stock, trial balance, the books, customers, records, enquiries, and taking an enquiry. **Not yet converted, and still English:** the dashboard, the deal desk (`DealsPage`, `DealTerms`, `DealProducts`, `StartDeal`), the workshop, parts, people, and the five control-plane console screens. The pattern is established and the remaining work is mechanical: add the screen's keys to all five catalogues, then replace its literals with `t(...)`, `useEnumLabel()` and `format.*`.

  **Named rather than hidden:** the API's own refusal messages are still English. Codes about the *reader* — your session ended, sign in again, this browser's token no longer matches — are mapped to catalogue keys in `shared/i18n/apiMessage.ts`; codes about a *record* keep the server's wording, which is precise, contextual, and the only copy of that sentence anywhere. A blanket `*.forbidden` mapping was tried and reverted: it turned "you cannot add customers" into "you do not have permission to see this", which says the opposite of what happened. Closing this properly means the server learning the reader's language.

  Evidence: `dotnet build` 0/0, `dotnet test` 516/516, `verify-e2e.ps1` **PASS**, `npm run typecheck` clean, `npm test` 226/226 (was 215).

- **2026-08-08 — Security hardening, alongside the above.** Three real gaps, each verified.

  **MFA confirm and disable were not rate limited**, on either the dealership or the control plane. Both take a six-digit code, and unlike the sign-in challenge there is no per-secret counter behind them to die after five wrong answers — that row is only created by a password sign-in. Somebody holding a stolen cookie jar could walk the whole million codes at whatever rate the server would answer: `confirm` to bind an authenticator they control, `disable` to take the second factor off the account entirely. Both are persistence rather than access, which is exactly the step worth making expensive. Now on the same per-caller limiter as sign-in.

  **No HSTS.** The session cookie is already `Secure`, so it never travels in the clear — but the first request to a hand-typed hostname has no cookie and no scheme, goes out over HTTP, and carries the tenant in a header. Now `max-age=63072000; includeSubDomains`, on HTTPS outside Development only (sending it from a dev host would pin `localhost` to HTTPS in the developer's own browser, which is close to unrecoverable). **No `preload`** — that is the operator's domain and their decision, and it is close to irreversible.

  **Printed documents were losing their filename.** `RenderedDocument.FileName` was computed at three call sites and discarded by `Render`, so a customer's copy saved out of the browser was named after the URL — a bare GUID for a deal. Now sent as `Content-Disposition: inline`, with quotes and control characters stripped, because a repair-order number is dealership-typed text going into a header.

  Also fixed: the sign-in screen pre-filled `northgroup`, a dealer group that exists only in the development seed, presenting it to every real installation as if it were theirs. It now remembers the last group this browser used. And `LeadHistoryEntry.toStatus` / `DealHistoryEntry.toStatus` were typed `string` while the repair-order equivalent was properly typed — tightened to their status unions.

  **Named rather than hidden:** the credential rate limiter partitions on `RemoteIpAddress`. Behind a reverse proxy that is the *proxy's* address, so every caller shares one bucket and twenty sign-ins from anywhere would lock out the whole dealership — the exact failure the partitioning was written to avoid. Fixing it means trusting `X-Forwarded-For`, which is spoofable unless the operator configures known proxies, and a spoofable partition key is worse than a shared one. This needs a `ForwardedHeaders` configuration with `KnownProxies`, and it needs the operator to set it; `deploy/README.md` is where that belongs.

- **2026-08-09 — Every screen a dealership uses speaks five languages, and the sign-in limiter survives a reverse proxy.** Twenty of twenty-five screens are off hard-coded English: the shell, both sign-in paths, first-password, two-step enrolment, stock, trial balance, the books, customers, records, enquiries, taking an enquiry, the deal desk and its three panels, people, parts, the dashboard, and the workshop. The five control-plane console screens are the remainder and are named below rather than left to be discovered.

  **The design is settled in [ADR-019](../adr/0019-language-owns-direction-and-ui-only-translation.md), and two decisions in it are the ones worth not re-litigating.** Direction is a property of the language rather than a setting beside the theme — the old LTR/RTL toggle made "Arabic, left to right" a state somebody could select, which is a broken layout with a switch in front of it. And **only UI vocabulary is translated; records are rendered exactly as stored** (maintainer, 2026-08-09). The boundary is who wrote the string: `shared/i18n/locales/*` is ours, an API response carrying record content is not. Customer names, vehicle descriptions, part numbers, chart-of-accounts names, rooftop codes, history notes and imported CSV rows all print verbatim in every language. API status enums are the one thing on the line and were accepted as UI vocabulary: they get a translated label, the stored value is untouched, and CSS classes still key off the raw value so behaviour never depends on language.

  **Four properties hold the translations up.** English is the schema — the other four catalogues are typed `Catalogue`, so a key added and not translated fails `npm run typecheck`. A test additionally reads the catalogue files and fails on a translation identical to the English, which typecheck cannot see; the genuine loanwords (`GAP`, `VIN`, `Status` in German, `Stock` and `Total` in French) are listed with their reason rather than the check being weakened. Plurals go through `Intl.PluralRules` — Russian needs four categories, Arabic six, and `1 vehicles in stock` was already wrong in English on a route the dashboard links to. Numbers, money and dates go through the active locale; four screens were formatting with `Intl.NumberFormat(undefined, …)`, which follows the *operating system* rather than the application, so a French screen could show `$1,234.50`.

  **Verified in a real browser at 1280**, not merely in jsdom: all five languages switched live through the picker, `dir` flipping to `rtl` for Arabic and back for the other four, `lang` following it, no horizontal page scroll in RTL (measured by scrolling, not by trusting `scrollWidth`), and the Arabic stock list rendering translated navigation, headings and chips with the correct Arabic *few* plural for three cars while the VIN and stock number still ran left to right inside the mirrored table.

  **The credential rate limiter now works behind a reverse proxy** — the gap named when it was last reviewed. It partitions on the remote address, which behind nginx or a load balancer is the *proxy's* for everybody, so twenty failed sign-ins from one attacker locked out the whole dealership. Forwarded headers are honoured, but **only from addresses the operator listed** under `Network:TrustedProxies`: an unconditionally trusted `X-Forwarded-For` is attacker-controlled, so a fresh address per request means a fresh bucket per request and the limiter becomes decoration — worse than the shared bucket it replaced. With nothing configured the middleware is not in the pipeline at all. The framework's loopback defaults are cleared rather than appended to; an unparseable entry throws at startup and names itself; and `X-Forwarded-Proto` is honoured too, because a proxy terminating TLS otherwise leaves `Request.IsHttps` false and the HSTS header silently absent. **Rehearsed:** removing the clear-the-defaults line failed exactly the two tests that demand it. `deploy/README.md` carries the operator's half.

  Evidence: `dotnet build` 0/0, `dotnet test` **523/523** (was 516), `verify-e2e.ps1` PASS, `npm audit` clean, `npm run typecheck` clean, `npm test` 226/226, `npm run build` ok.

  **Still English, and named rather than hidden.** (1) The five control-plane console screens — `AdminApp`, `AdminSignIn`, `AdminSecondFactorSetup`, `SupportAccessPage`, `TenantsPage`. The pattern is established and the work is mechanical: add the screen's keys to all five catalogues, then replace its literals with `t()`, `useEnumLabel()` and `format.*`. (2) The API's own refusal messages *about a record*. A blanket mapping of every `*.forbidden` code to one translated sentence was tried and reverted — it turned "you cannot add customers" into "you do not have permission to see this", which says the opposite of what happened. Closing it means the server learning the reader's language. (3) Printed documents (`DocumentHtml`) are English and `lang="en"`; they are a record of what was agreed rather than UI, and which language a customer's paperwork should be in is a question nobody has answered.

- **2026-08-09 — Every screen speaks five languages, and the workshop has a diary.** Two milestones in one run.

  **The control-plane console was the last English holdout, and is now 25 of 25.** The administration shell, its sign-in, its second-factor enrolment, the dealership list and support access all read from the catalogue. The console keeps its own vocabulary rather than borrowing the dealership's: whoever reads it runs the installation, "dealership" there means an account on a server rather than a place with a forecourt, and the catalogue comment says so to the next translator.

  Converting them found four things that were not translation work. The dealership list told an empty installation that "creating one is still a developer's job" — directly beside the button that creates one; the screen had grown the capability and the sentence had not. Support access printed its expiry through `toLocaleTimeString()`, which follows the operating system rather than the chosen language, so a new `format.time` joins the other formatters. German showed a suspended dealership as *Ausgesetzt* but labelled the button *Sperren*, and Arabic showed *موقوفة* and offered *تعليق* — two words for one state read as two states, so the verbs now match the status they produce. And the tenant key, schema version, enrolment code, TOTP secret and both email addresses carry `dir="ltr"`: a tenant key travels in an HTTP header, and a mis-transcribed enrolment secret locks a new manager out of a dealership that does not exist yet.

  **The service diary closes the I5 hole the workshop's own README named** — "No appointments or workshop loading. A job is booked in when the car is there." A booking is a promise that a car will arrive, and deliberately not a repair order with an earlier date on it. It lives in `src/App/RepairOrders/` because the workshop owns the `service` schema and this is the gap in that capability, not a new one.

  **A promise becomes a job exactly once**, which is what makes the exit criterion "Appointment → RO visibility reconciles to its source" true rather than asserted. Arriving opens the repair order and links it inside **one transaction**, so there can never be an arrival with no job or a job the diary has lost, and the second arrival is refused by name. **Rehearsed:** removing the transaction and the guard leaves two repair orders — RO-1003 and RO-1004 — against one car, one visit billed twice, and the test names it.

  Three further decisions are recorded rather than left to be inferred. **A car that never came is recorded, not deleted**, and NoShow is kept apart from Cancelled because one is silence and the other is the customer ringing — the difference is exactly what tells a manager who to remind the day before. **Capacity is reported, not enforced**: the diary returns each day's committed hours and refuses nothing, because real shops overbook deliberately and a diary that refused at eight hours would be worked around within a week by booking everything as an estimate of zero. An arrived car stops counting, since its hours belong to its job. **Booking takes `Service.Write`**, the same right that opens a job, because arriving a car *is* opening a job and a weaker booking permission would be a route to a stronger one.

  The screen sits **on** the workshop page rather than behind a second route: a service manager's day is what is on the ramps and what is still to come, at once. "It's here" opens the job and hands it straight over in one click, because the server does both in one transaction and offering two steps would be a lie about that. Translated in all five languages like everything else.

  Also closed on the way past: `VehicleSummary` was missing from `frontend/src/shared/contracts.ts` entirely — no screen had listed cars before — and is now mirrored from the server.

  Evidence: `dotnet build` 0/0, `dotnet test` **546/546** (was 523), `verify-e2e.ps1` **PASS**, `npm audit` clean, `npm run typecheck` clean, `npm test` **236/236** (was 226), `npm run build` ok.

  **Named rather than hidden.** The diary loads a *workshop*, not a technician or a ramp — "who is free at eleven" is a different model and is not answered. It knows who is expected tomorrow and messages nobody, because no communications channel exists yet. Unchanged from the previous entry: the API's record-level refusals are still English, and printed documents are still English with `lang="en"`.

- **2026-08-09 — A forgotten password has an answer.** The gap named as most likely to be hit in a pilot's second week. Two of ADR-018's five methods are built — the two that need nothing external — and the other three are declared by the contract rather than left to be discovered.

  **Resetting is a SINGLE CALL.** Email, proof and the new password arrive together; the password changes or nothing does. The familiar shape — prove, receive a ticket, redeem the ticket — needs a second credential that exists between two requests, has to be stored, expired, transported and invalidated, and is worth stealing. Nothing here needs to survive between two requests, so nothing does. ADR-018 has been amended to record that.

  **What is offered is a property of the INSTALLATION, never of an account.** `GET /auth/recover` takes no email and returns the same body whichever one is appended, which a test asserts. Answering per-account would say whether an address exists and whether it has an authenticator — a map of who is easiest to attack. Every failure is one indistinguishable `recovery.refused`, and the unknown-account path still runs the password hasher so absence is not detectable by timing. **Rehearsed:** giving the unknown-account branch its own error code failed exactly the enumeration test.

  **Issuing the manager backstop is its own permission.** `Staff.ResetPassword`, organization-wide, deliberately not folded into `Staff.Manage` — adding a starter is administration, but handing somebody the ability to sign in *as* an existing colleague, possibly a more privileged one, is a different act. Seeded onto Manager because on a fresh installation there is nobody else, but a dealership wanting resets held by fewer people than rotas can already arrange that without a code change. The issue is visible on the staff record until it is used or expires, not only in the audit trail.

  **A successful reset ends every session the account had**, because somebody recovering an account may be recovering it *from* someone.

  **Identity's sealed surface was widened, which is a security decision and is recorded as one.** `IAccountRecovery`, `RecoveryMethods` and `RecoveryErrors` are now public, with the reasoning and — more importantly — what the surface *cannot* do written into `BoundaryTests` beside them: it cannot say whether an account exists, it issues no session so a reset cannot be chained into being signed in, it cannot mint the manager code, and it cannot touch an account that never had a password.

  **Named honestly rather than overstated.** A `Purpose` column now separates enrolment codes from reset codes, and the header comment originally claimed it was load-bearing. It is not, yet: a rehearsal removing the filter from *both* paths failed no test, because the opposite preconditions on `PasswordHash` already separate them completely. The column stays — that separation is two checks happening to agree, and the day either is relaxed it becomes the thing that stops a starter's code opening a reset — but the comments and the test now say what they actually prove.

  **A defect in the generated migration was corrected by hand.** EF scaffolded `defaultValue: ""` for the new non-nullable `Purpose` column, which matches neither enum name — every enrolment code outstanding at upgrade time would have silently stopped working, leaving a starter holding a dead code. Now defaults to `Enrolment`, with the reason in the migration.

  Evidence: `dotnet build` 0/0, `dotnet test` **559/559** (was 546), `verify-e2e.ps1` **PASS**, `npm audit` clean, `npm run typecheck` clean, `npm test` **245/245** (was 236), `npm run build` ok. Walked in a real browser in English and Arabic: every field pinned `dir="ltr"`, the page mirroring, no horizontal scroll.

  **Still missing, and named.** A passkey or fingerprint — it needs a WebAuthn dependency, which is a licence-and-advisory decision of the same kind ADR-018 and ADR-019 already made twice, not something to slip in. Email and text message need an account somebody buys, and report `false` rather than being offered and failing. And **whoever runs the installation still cannot recover their own account**: there is nobody above them to issue a code, so it needs a different answer entirely.

- **2026-08-09 — The interface standards, and the band nobody could see.** ADR-020's eleven standards go from five held to eight, and the two that moved were closed by *measuring* rather than by inspection.

  **Instant search** on Customers and Parts. The debounce is the small half; the part that made this more than two lines is that the superseded request is **aborted**. Without it a slow reply for "f" lands after the right answer for "focus" and overwrites it, and the screen sits there confidently showing the wrong list — debouncing alone only makes that rarer, which is worse than leaving it obvious. Two prerequisites had to be fixed first: the api client turned an aborted fetch into "could not reach the server" (a red error on every keystroke), and the test stub ignored cancellation and answered instantly, so it could not tell code that cancels from code that does not. **Rehearsed:** removing the AbortController fails the race test.

  **Inline editing**, in `shared/InlineEdit.tsx`, first used on the diary's estimate — the most repeated edit in a service diary and the one that stops being kept current the moment it needs a form, which then makes the day's load a lie. The idle state is a **button**, not a div with an `onClick`; that single choice is the difference between inline editing and a mouse-only feature.

  **A measured AA pass** at 320px and 640px (1280 at 200%), both directions, all nine screens. Reflow, resize, keyboard reach and focus visibility pass. Target size found **five real failures** — including the inline-edit value I had just written at 22px wide — all floored at 24px and re-measured clean.

  **Consolidation, checked rather than assumed.** Better than expected in one way: **no screen puts a record behind a route**; every route is an area and every detail is already an inline band. Worse in another: the **signal band existed on one screen and had no CSS at all**. `.panel--waiting` had been written into the workshop with no rules behind it, so the most important element on the busiest screen was rendering as an ordinary panel. Now `.panel--signal`, styled, and the deal desk has one: deals submitted and not signed off, drawn from data already loaded, gone when the queue is empty.

  **Three measurements were wrong before they were right, and all three were caught before being believed.** The target-size sweep first ran against pages that had not rendered — every screen reported clean because every screen was empty. The focus-ring check reported all 24 controls as failing, which was programmatic `.focus()` not triggering `:focus-visible`. And a browser found a defect jsdom cannot see: re-reading the diary after an inline save blanked the table, unmounting the row and taking the "Saved" confirmation with it before anybody could read it — **rehearsed, and no test fails without the fix**, because the stub answers instantly and both states land in one React batch. The test says so in place of pretending to guard it.

  Evidence: `dotnet build` 0/0, `dotnet test` **559/559**, `verify-e2e.ps1` **PASS**, `npm audit` clean, `npm run typecheck` clean, `npm test` **255/255** (was 245), `npm run build` ok.

  **Still open on the interface**: a signal band for untouched enquiries, detail bands on stock and customers, quick-action toolbars, systematic smart defaults, and a motion vocabulary for autosave and background sync — which do not exist as behaviours yet, so inventing their animation would be decoration.

- **2026-08-12 — The integration edge, designed against four that already ran.** Four private DMS integration codebases were read end to end and the lessons distilled into a device-only analysis (`local/`, deliberately not published: it is third-party work). What is committed is the effect on our own design — six amendments to doc 05, two ADRs, and a connector skeleton that holds them.

  **A cursor is not a date, and this is the expensive one.** Real endpoints cap a request at a fixed span, refuse anything older than a few days, or take no date parameters at all and decide "recent" for themselves. So `FetchWindowPolicy` declares the arithmetic as data and `FetchOutcome.Covered` reports what the provider *actually served* — nullable, because "it did not say" is the common answer. `Cursor.Advance` moves only from that, and refuses two ways: `coverage_unknown` when nothing was reported, `coverage_gap` when the served range starts after the cursor. **Rehearsed:** deleting the gap guard fails two tests, including the end-to-end one against the fixture.

  **A value that does not fit becomes absent** (ADR-021). The row-correction pass is the best idea in the four codebases — one bad field must not fail a ten-thousand-row night — and its substitutions are the worst: an out-of-range amount becomes `0`, an impossible date becomes a sentinel, and both then read as fact forever. `Coerce` returns present-or-absent with the raw text kept, and `FieldValue<T>` has no way to read a default out of an absence. Truncation survives only for free text; `isKey: true` refuses instead, because a truncated key matches the *wrong* record rather than failing. **Rehearsed:** returning `0` for an out-of-range amount fails the test.

  **Position is not a join key.** `ColumnSet.Align` quarantines when parallel provider arrays disagree in length. In the reviewed code this failure had already cost real data: labour descriptions and hours are permanently discarded there, with a comment explaining that the arrays stopped lining up.

  **A poll deadline is not a retry count.** Five attempts is fifty seconds against a provider saying "check back in 10" and fifty minutes against one saying "600". `PollBudget` spends wall-clock time, `ProviderTiming` keeps the poll deadline and the transport retry budget apart, and passing the deadline returns `StillRunningAtProvider` — a state to report, not a failure to raise.

  **Settings are declared one field at a time**, and validated when saved. A required setting left blank fails that dealership *loudly*; an undeclared key is refused rather than ignored. Both matter: in the reviewed code a blank spreadsheet column throws, the per-store catch swallows it, and that dealership is skipped silently every night.

  **ADR-022** makes raw request/response capture a named runtime facility with retention in code — required to operate an integration at all, and personal data, so redaction covers the body and not only the header.

  **A boundary was added before it could be crossed.** `FeatureBoundaryTests` now forbids Integrations from touching any capability's entities; **rehearsed** by giving a connector type a `Deal` and watching it fail.

  Also corrected doc 05's layout, which still showed `src/Integrations/` as a fourth project and contradicted ADR-017.

  Evidence: `dotnet build` 0/0, `dotnet test` **601/601** (was 559 — 41 new conformance and mapping tests, 1 new architecture case), `verify-e2e.ps1` **PASS**.

  **Nothing here talks to a network.** No real connector, no inbox or outbox, no quarantine store, no replay, no reconciliation, no persistence — `ConnectorRun` is a shape with no table behind it, and cursors are not stored anywhere. `src/App/Integrations/README.md` lists all of it rather than leaving a half-built edge looking finished.

- **2026-08-12 — The integration runtime: the cursor rules stop being checkable and start being enforced.** The skeleton committed earlier today held four rules and a conformance suite, and every one of them lived in memory. A cursor rule that is correct in a method and lost on save is worth nothing, because the entire payoff of refusing to advance is that *tomorrow's* run re-reads the same window — and tomorrow is a different process against the same row. Three tables, one runtime, 14 SQL-backed tests.

  **`ConnectorCursor` is the row the whole design exists to protect.** It carries the position, `HeldBecause`, and `ConsecutiveHolds` — the last of which is the number that turns an ordinary event into a reportable one. One hold is a Tuesday; six in a row is a dealership quietly falling behind with nobody being told. The unique index on (connector, dealership, contract, version) is enforced by the database rather than remembered by the runtime: two rows would let two runs each advance their own copy, and the feed would read as up to date while skipping whatever the other had passed.

  **A held cursor is not a failed run**, and `ConnectorRun.CursorHeld` is deliberately a separate column from `Outcome`. A feed whose provider will not account for its window succeeds every night, applies records every night, and falls further behind every night; collapsing the two facts would hide the more important half.

  **The run row is written before the fetch, not after.** A process killed mid-run leaves a row with `FinishedAt` null, and that unfinished row is the only evidence the attempt happened — one tidy row at the end would make a crash indistinguishable from a night that never ran. **Rehearsed:** moving the save to the end fails the test that proves it.

  **An endpoint that takes no dates keeps no cursor at all.** This was not the first design. A cursor for a dateless delta feed is permanently "held", which reads as a fault rather than as the normal shape of that kind of endpoint — so there is no row, and the feed depends entirely on the sink being idempotent, which is now the first stated obligation on `IRecordSink`.

  **`IRecordSink` is how a record reaches the capability that owns it**, and it exists because `FeatureBoundaryTests` forbids Integrations from seeing `Deal` or `Customer` at all. A run with no sink registered is refused as `Misconfigured` **before the provider is called** — spending a rate limit to throw the answer away looks like a working integration, which is worse than a failure.

  **Quarantine holds the provider's own payload**, which makes it personal data, which makes ADR-022 apply in full. Each row carries an expiry from a 90-day retention and reads filter on it, so the column does work from the day it is written rather than from the day somebody builds a purge job. The uncomfortable half is deliberate: a record left unresolved past its retention is gone. Keeping a customer's details indefinitely because a mapping bug was never fixed is not a data-quality feature. **Rehearsed:** dropping the expiry filter fails the test.

  **The rehearsal that mattered most:** changing the runtime to advance from the range it *requested* instead of the range the provider *served* — the exact mistake the whole design exists to prevent, and the shorter code — fails **five** tests.

  Evidence: `dotnet build` 0/0, `dotnet test` **615/615** (was 601), `verify-e2e.ps1` **PASS**.

  **Still not built, and listed rather than implied.** No sink implementation, so a real deployment reports every run misconfigured. No scheduler, endpoint or screen — a run happens because a test starts one. No inbox, no webhooks, no poll lease, no reconciliation, no outbound writes. No quarantine purge (the expiry is enforced on read only) and no replay: `Resolve` marks a record dealt with without re-applying it.

- **2026-08-14 — The integration loop closes: a record now reaches the capability that owns it.** The edge could fetch, translate, quarantine and track a cursor, and could deliver none of it: every run in a real deployment reported `Misconfigured` because nothing implemented `IRecordSink`. `CustomerRecordSink` is the first, and building it exposed two flaws in the port that a design review had not.

  **`ProviderRecord.Fields` was documented as the provider's own field names.** Had that stood, every sink would have needed a mapping for every provider — one per capability, per vendor, which is the multiplication that makes an integration layer collapse at about the fourth provider and is invisible until then. Fields are now keyed by **contract** names, declared once in `ContractFields.cs`, and translating from the vendor's vocabulary is the connector's job. One sink now serves every provider.

  **`IRecordSink` said "do not save", and no capability in this codebase could satisfy it** — every service saves, and the alternative was an awkward second no-save method on each one. The runtime now opens an explicit transaction around the whole run, so a sink's `SaveChangesAsync` flushes without committing and applying still commits together with the cursor. The invariant that actually matters is preserved and the contract got easier to obey rather than harder.

  **Idempotence is the whole job of a sink**, because a held cursor re-reads the same window by design. Every insert is guarded by an external-reference lookup. **Rehearsed:** removing that guard fails three tests.

  **An integration run happens on behalf of a named person**, following the CSV import worker rather than inventing a system principal — which would have been the one path into this application that writes records with nobody accountable. The runtime refuses to start without an authenticated caller. **Rehearsed:** removing the check fails the test.

  **A new column, `RecordsUnchanged`, and it is not bookkeeping.** A feed whose cursor is held re-reads the same window nightly. Counting those as applied would show five hundred records a night arriving and look perfectly healthy. "3 applied, 497 unchanged" is the shape of a working feed; "500 applied" every night is the shape of a stuck one.

  **A sink cannot update, only insert.** Field ownership is undecided (doc 05 §4), and silently overwriting a member of staff's correction with stale provider data would be worse than doing nothing. Named rather than left as a surprise.

  **A pre-existing flaky test was found and fixed.** `A_customer_can_be_found_by_surname` failed once in a full run and passed in isolation. Cause: `UniqueSurname()` produced `Test` + hex, hex contains digits, and customer search extracts digits from the term and matches them against **phone numbers** — so a surname could pull in any customer whose phone contained those digits. Every test customer shared `5550102030`; the new fixture rows added ten more and turned a latent coincidence into a regular one. Surnames are now letters-only, which switches the digit clause off entirely, and fixture phones are distinct per record. **Verified by three consecutive clean full-suite runs**, not by one.

  Evidence: `dotnet build` 0/0, `dotnet test` **622/622** (was 615), `verify-e2e.ps1` **PASS**.

  **Still not built.** Only one sink — `Deals` and `Service` are declared by the fixture and have nowhere to go. No scheduler, endpoint or screen, so a run happens because a test starts one. No real connector, no inbox or outbox, no webhooks, no replay, no reconciliation, no quarantine purge.

- **2026-08-14 — Every band ADR-020 names now exists, and the sixth language is Spanish.** The screen standard listed five bands; `panel--detail` had **no CSS rules behind it at all** — named in the ADR, referenced nowhere, so "the shape is applied everywhere" was true of four bands out of five. It now has rules and three screens using it: stock shows what a car cost, when it was taken in, and everything that has happened to it; customers show every way to reach them, their address, and where the record came from. Enquiries gained the missing signal band — the enquiries with nobody's name against them, oldest first, absent entirely when there are none, because a panel that says "nothing to worry about" is a panel people learn to skip.

  **The row controls are buttons, not clickable rows.** A `<tr onClick>` is unreachable by keyboard and announces nothing to a screen reader; the stock number and the customer name are now real buttons.

  **A defect the browser found and the DOM tests could not:** the stock history renders in a causally impossible order — *Available → On hold*, then *taken in as Incoming*, then *Incoming → Available*. The car becomes available before it arrives. The cause is not the screen. Two history rows carry an identical `OccurredAt` to the microsecond, and all **ten** history queries across Inventory, Deals, Leads, RepairOrders and Accounting order by that column alone, with no tiebreak — so SQL Server is free to return tied rows in any order, and did. Recorded rather than fixed here: it needs a stable ordering key on five append-only tables and a migration, which is its own milestone.

  **Spanish** is 795 keys, `usted` throughout, and vocabulary chosen to be neutral across Spain and Latin America ("repuestos", "vehículo") rather than idiomatic in one and odd in the other. All six catalogues carry **identical key sets**. French `Mobile` was still the English word and is now `Portable`; the language-count test had to be told about Spanish, which is the test doing its job.

  **Seen in a real browser** against seeded data, not only in jsdom: the signal band listing two unclaimed enquiries oldest-first (50 days, then 13), both detail bands opening on real records, Spanish rendering while the customer's name and address stay untranslated, and Arabic still turning the page round with `dir="rtl"`, no horizontal page scroll (measured by scrolling, not by trusting `scrollWidth`), and the correct CLDR **dual** form for two enquiries — a plural category English does not have.

  Evidence: `dotnet build` 0/0, `dotnet test` 622/622, `verify-e2e.ps1` PASS, `npm audit` clean, `npm run typecheck`, `npm test` **257/257** (was 255), `npm run build`.

- **2026-08-14 — A history is a sequence again, not a set with timestamps on it.** Every append-only history in the product — Inventory, Deals, Leads, RepairOrders, Accounting — was read with `OrderBy(OccurredAt)` and nothing else. Two rows can carry the same `OccurredAt` to the microsecond, and a tie leaves the order to whatever the store finds convenient: with the index on `(parentId, OccurredAt)`, ties resolved by the clustered key, which is a **Guid** — so the order was effectively random. It showed: a seeded car read *Available → On hold*, then *taken into stock*, then *Incoming → Available*. The car became available before it arrived. Ten query sites, five tables, one `Sequence` column (`bigint IDENTITY`), and `ThenBy(h => h.Sequence)` everywhere.

  **The rehearsal is the part worth recording, because the first attempt at it was wrong.** Removing `ThenBy` and re-running the new test showed it still passing — the composite index `(parentId, OccurredAt, Sequence)` returns rows in sequence order whether or not the query asks. A test that cannot fail proves nothing, so the fix was reverted *properly* — the index back to two columns as well — and the test then failed on exactly the right assertion: "a car has to arrive before it can move anywhere". Both halves matter and for different reasons: the index makes the correct order cheap, the explicit `ORDER BY` makes it **guaranteed** rather than a property of the current query plan.

  **Named rather than hidden: the backfill cannot repair existing rows.** `ALTER TABLE ADD Sequence bigint IDENTITY` numbers the rows already there in physical order — the same arbitrary order that caused the defect. The seeded car still reads wrong in the development database. Nothing can recover the true order of those rows, because it was never written down, and that absence is precisely the bug being fixed. Correct for every row written from the migration onwards.

  **Also not proven, and worth saying:** the test writes its history rows through separate requests, so it demonstrates that ties *read back* in insertion order. Two rows inserted in one `SaveChangesAsync` rely on EF Core preserving the order entities were added, which is true in practice but is not asserted anywhere here.

  Evidence: `dotnet build` 0/0, `dotnet test` **623/623** (was 622), `verify-e2e.ps1` PASS.

- **2026-08-14 — Something outside this machine answers, for the first time.** The project manager raised twenty-odd business requirements. Every term was checked against industry and government sources before anything was written down, and the write-up is [`docs/11-Franchise-and-External-Scope.md`](../11-Franchise-and-External-Scope.md). Most of the list turned out to be blocked on things engineering cannot supply — a manufacturer relationship, a commercial contract, or the overdue decision about which market we serve. **Exactly one item had nothing in front of it**, and it is now built.

  **Safety recalls from the US road-safety regulator.** `GET /api/v1/vehicles/{id}/recalls`. Their API is free and unauthenticated — verified live rather than taken from documentation — so this needed no approval and no contract. It is the only outbound call in the product.

  **What the feature refuses to claim is the point of it.** The regulator indexes campaigns by year, make and model; it holds no record of whether *this* car has had the work done, because only the manufacturer does. So the property is `campaigns`, not "openRecalls", and every response carries `appliesToModelNotVehicle: true` so a caller is handed the caveat rather than expected to remember it. Telling a dealership a car is clear when it is not concerns somebody's brakes.

  **"We could not ask" and "there is nothing to worry about" must never look the same.** An unreachable regulator is a **503** with its own error code, not an empty list — which needed a new `ErrorType.Unavailable` in Core, mapped at the edge. **Rehearsed:** making a lookup failure return an empty list instead failed exactly the two tests that demand a refusal.

  Three rules this adapter follows, new to the codebase because nothing else calls out: it cannot throw (every network fault becomes a `Result` failure, so a car never becomes unsellable because somebody else is down), its deadline lives on the `HttpClient` where the dependency is declared, and it stores nothing — a cached all-clear that nobody re-checked is worse than no answer.

  **The tests never reach the real regulator.** All six stub the handler. A suite that depends on a public service being up fails on a train, and one that hammers a government endpoint every CI run deserves to be blocked.

  **Two findings from the research that change decisions we had already made**, recorded in doc 11 rather than acted on: the industry has a data-exchange standard (**STAR**, 145+ message formats) and our `ContractFields` vocabulary is invented instead of using it — cheap to change now, expensive after several connectors; and *"a table for service pays"* resolves to **pay type** — Customer Pay, Warranty, Internal — which we do not model at all, and which sits underneath warranty claims, manufacturer reporting and any honest service gross figure.

  **Still not built, and named:** no screen — the endpoint is reachable by API only. No VIN decode, though the same regulator offers one free. No per-vehicle recall status, which is the manufacturer's to give.

  Evidence: `dotnet build` 0/0, `dotnet test` **629/629** (was 623), `verify-e2e.ps1` PASS.

- **2026-08-15 — Work knows who is paying for it, and the ledger tells the three apart.** A repair order line carries a **pay type**: Customer Pay, Warranty, or Internal. Every line was implicitly customer-pay before, which meant a warranty repair and the dealership's own reconditioning both landed on the customer's invoice. It is per line, not per job, because one job routinely mixes all three — the customer came in for a service, the water pump turned out to be under warranty, and the workshop changed a wiper blade off its own stock while the car was up.

  **`AmountDue` is now the customer's share alone**, with `WarrantyTotal` and `InternalTotal` beside it and `WorkTotal` for everything the workshop did. **The ledger separates them**: warranty debits a new receivable (1200) because the claim has not been paid and booking it as cash shows money the dealership has not got; internal debits its own charge (5400). Revenue is credited with all of it either way, because the workshop sold all of it.

  **Work the customer is not paying for needs no answer from them.** Asking somebody to authorise a repair they are not funding is a question with no meaning — and an unanswered line blocks the invoice, so it would have stopped the job as well. **An unknown pay type is refused**, not defaulted: a typo silently becoming `CustomerPay` would bill somebody for warranty work.

  **The posting carries the same money split two ways** — by what was sold and by who settles it — and refuses if they disagree, naming the caller rather than reporting an unbalanced entry and sending somebody hunting through account mappings.

  **Two pre-existing defects, both found by this change.** The chart of accounts was written out **twice**, in the development seeder and in tenant provisioning; adding two accounts to one broke the other, and the symptom was a newly provisioned dealership unable to invoice a repair order on its first day. There is now one shared catalogue, for exactly the reason the roles catalogue is already shared. And EF generated the new column with `defaultValue: ""`, which is not a member of the enum — every service line written before the migration would have thrown on read. Fixed in the model so the migration carries `CustomerPay`.

  **Rehearsed:** restoring `AmountDue` to bill everything fails exactly the three tests that demand the split.

  **Not done, and it is the other half of D4:** reconditioning is charged to 5400 rather than capitalised onto the car in stock, so a used vehicle's recorded cost still misses its recon. That couples the workshop to inventory and needs a decision. No screen yet.

  Evidence: `dotnet build` 0/0, `dotnet test` **633/633** (was 629), `verify-e2e.ps1` PASS.

- **2026-08-15 — The workshop can be measured, and the report says what it cannot measure.** `GET /api/v1/repair-orders/labour` gives hours sold, labour revenue, and the **effective labour rate** — what an hour actually realised, as against the rate on the wall — per technician and per payer, from invoiced jobs only. Work in progress is not revenue, and counting it flatters the month and then contradicts itself when a job is cancelled.

  **Every pay type counts towards hours sold.** A technician who spent Tuesday on warranty work sold those hours; who settles the bill changes the accounting, not whether the work happened.

  **The part that matters is what it refuses to give.** The trade benchmarks technicians on **efficiency** (hours produced ÷ hours available, NADA guideline 125%) and **productivity** (hours billed ÷ hours clocked, 87.5%). This system stores neither denominator — no roster, no time clock — so both are named in a `notMeasured` list rather than quietly omitted. A report that leaves them out invites a manager to assume they were fine, and these are numbers people are judged on.

  **Rehearsed:** dropping the labour-kind filter lets a $500 part count as an hour, and fails exactly the test guarding the realised rate.

  Evidence: `dotnet build` 0/0, `dotnet test` **636/636** (was 633).

- **2026-08-15 — A passkey can be proven genuine, or proven not to be.** The cryptographic half of WebAuthn, and **deliberately nothing else**: no table, no route, no sign-in path. This commit adds no attack surface, and whatever wires it up next can rely on the verification already being trustworthy instead of hoping.

  **Implemented directly rather than by adding a FIDO library.** A passkey needs one binary format decoded and two signature algorithms verified; pulling a whole attestation stack into the sealed Identity project to get that would be a far larger security dependency than the problem calls for. One package was added — Microsoft's own `System.Formats.Cbor` — because hand-rolling a CBOR reader would have been the riskiest line in the change.

  Seven checks, each the reason a passkey cannot be phished, replayed or forged: ceremony type, single-use challenge, **origin** (the anti-phishing property), relying-party hash, user presence, the signature over `authenticatorData || SHA-256(clientDataJSON)`, and the sign counter.

  **Attestation is not verified, and that is stated rather than hidden.** `none` is accepted — what platform passkeys send — and every other format is refused rather than ignored, because accepting one we do not check would imply a guarantee we are not making.

  **The fake authenticator is why any of this is worth trusting.** It holds a real P-256 key, writes real CBOR, assembles real authenticator data and signs real assertions — and misbehaves on demand: wrong origin, wrong relying party, a repeating counter, a signature over the wrong bytes. A suite built on hand-written byte arrays and a stubbed verifier would prove only that the stub returns what it was told. Thirteen tests: one happy path, twelve refusals, each asserting the specific reason.

  **Rehearsed:** deleting the origin check and the counter check fails exactly the lookalike-site test and the clone test, and nothing else.

  **Still not built:** the credential table, registration and sign-in endpoints, and the session issuance that would make a passkey actually sign somebody in. Until those exist **passkeys are not usable**, and no screen offers them.

  Evidence: `dotnet build` 0/0, `dotnet test` **649/649** (was 636).

- **2026-08-15 — Reconditioning lands on the car, and D4 is settled.** Internal work on a vehicle the rooftop owns is now capitalised onto that vehicle (1300) instead of charged to 5400. It was the deliberately-unfinished half of the pay-type change, and while it stood a used car's recorded cost missed the money spent making it saleable — **used-vehicle gross flattered itself by exactly the recon bill**, which is the classic way a used department looks profitable and is not.

  **Correction, checked 2026-09-19:** the posting goes to 1300, but the stock
  unit's recorded cost is unchanged. The test below asserts the account debit,
  not a per-car cost increase or its relief on delivery. Those claims were too
  broad; recon attribution and sale handling remain open in the scope register.

  **The workshop asks rather than guesses.** `IInventory.FindOwnedAsync` answers whether this rooftop owns this vehicle, excluding sold and removed units — a car that has left is not somewhere to put more cost. Work on anything not in stock (a courtesy car, a director's vehicle, a customer's car the dealership decided to cover) still lands on 5400, because there is no unit to put it on. The only difference between the two paths is whether that answer is null.

  It returns a `Guid?` rather than a summary: the caller needs to know *whether*, not *what*, and the fuller shape would have dragged a vehicle lookup into Inventory to populate fields nobody reads.

  **Rehearsed:** never capitalising fails exactly the new test and nothing else.

  Evidence: `dotnet build` 0/0, `dotnet test` **658/658** (was 657), `verify-e2e.ps1` PASS.

- **2026-08-15 — A passkey signs somebody in.** The four pieces built separately over the day — verification, storage, contract, implementation — are joined, and the feature stops being inert. Registration and sign-in both work end to end against a real database.

  **The routes live in the `auth` group, not their own.** A sign-in ends in the same cookie pair as every other, and mapping them elsewhere would have meant a second copy of `CompleteSession` — two places writing a session cookie is how the two quietly stop agreeing about `SameSite` or expiry. Sign-in reaches the identical call a password does, so the cookie, the anti-forgery token and the expiry are the same **by construction** rather than by somebody remembering.

  **Two middleware allowlists needed the sign-in routes**, and only those two. Registration is deliberately *not* exempt from either: adding a credential to an account requires already being signed in to it. For anti-forgery specifically, what replaces the token on the sign-in path is stronger than one — a signature over a server-issued single-use challenge, which a cross-site attacker cannot obtain or forge.

  **The fake authenticator is linked into the integration suite, not copied.** Two copies would drift: the unit one would be corrected for a spec change and this one would go on passing against the old shape, which is exactly what a shared fake exists to prevent.

  Five tests over HTTP, and the useful ones are the refusals: a **lookalike site** is refused by the endpoint and hands back no cookie at all; a **whole recorded response replayed** succeeds once and fails the second time; asking for a challenge **needs no account and names none**, so the endpoint cannot be used to discover who has a passkey; and a forgotten passkey **stops working**, not merely disappears from a list.

  **What this is not.** No screen offers a passkey, so it is reachable by API only. Attestation is still unverified by choice. And there is no policy making a passkey sufficient on its own — passwords and recovery codes remain, because a dealership locked out of its service desk on a Saturday morning does not forgive it.

  Evidence: `dotnet build` 0/0, `dotnet test` **664/664** (was 658), `verify-e2e.ps1` PASS.

- **2026-08-15 — Four features that only an API could reach became screens.** Pay type, the labour report, the recall check and passkeys were all built and tested during the day and none of them was usable by a person. That gap is closed: `WorkshopPage` carries who pays, `/workshop/labour` is the report, the stock detail has a recall band, and `/security/passkeys` enrols one while the sign-in screen accepts it.

  **The totals block is the part that matters.** "Due" now means what the *customer* owes and nothing else, with warranty and internal shown apart from it and a combined "all the work" figure when there is one. A job carrying £240 of warranty repair used to add that to the customer's invoice on screen. The warranty and internal rows are omitted entirely when they are zero — a permanent "Warranty 0.00" on every ordinary job is noise on the one block somebody reads while deciding what to charge.

  **Warranty and internal lines no longer claim the customer agreed.** The server marks them authorised on arrival because nobody needs asking, and the screen was about to print "Agreed" against them — a record of a conversation that never happened. It says "Not the customer's to agree" instead.

  **The labour report's "what this does not measure" band is not decoration.** Efficiency and productivity are what a service manager comes to a report like this for, and neither can be produced from what this system stores — there is no roster and no time clock. The server names them in `notMeasured` and the screen explains each one and what it would need. A report that showed three numbers and stayed silent about the missing two would invite somebody to assume they were fine, and both are used to judge individual people.

  **Both interface decisions from earlier in the day were kept, and one had to bend to six languages.** The recall check asks nothing until the button is pressed — asserted by a test, because the failure mode is a screen left open on a desk quietly polling a regulator all afternoon. The passkey button sits beside the password field; measured at 1280px it shares the line in English and wraps to the line immediately below in the other five, because "Use a passkey" is three short words in English and five long ones in French, and forcing one line squeezed the password box to 140px — narrower than the button beside it. Recorded in doc 11 §7 rather than left as a surprise.

  **A defect found by driving the screen, not by a test.** Every act on a repair order — answering a line, assigning a technician, writing work up — replaced the whole workshop with "Loading the workshop…" while the list refetched, then rebuilt it: the detail band was unmounted, half-typed input was lost, and the page jumped. A refresh now keeps what is on screen; a first load and a filter change still show the loading state, because then there is genuinely nothing to look at.

  **Three CSS classes were being written in JSX with no rule behind them** — the exact drift ADR-020 exists to stop. `chip--warn` was landing on the neutral grey in three screens; it now reads as something to act on, with the word still carrying the meaning.

  **Rehearsed, five ways.** Making the recall check fetch on mount, making "Due" show the total work, dropping the not-measured band, moving the passkey button out of the password row, and restoring the loading flash each failed exactly the test written for it and nothing unrelated.

  **What this is not.** Attestation is still unverified by choice — the browser is asked for `attestation: 'none'`, so nothing is requested that would then go unchecked. No policy lets a passkey replace a password; every account still has one. The recall data is still by model, and the screen says so above the list every time.

  Evidence: `dotnet build` 0/0, `dotnet test` **664/664**, `verify-e2e.ps1` PASS, frontend `npm audit` clean, `npm run typecheck`, `npm test` **303/303** (was 257), `npm run build`. All four screens driven in a real browser in English, German and Arabic, with no horizontal page scroll in any of them.

- **2026-08-15 — The documentation set stopped disagreeing with itself.** Eighteen prose documents grew one milestone at a time and were being asked to serve as a single comprehensive account of the product. A sweep of the whole set — every prose document, every ADR, the diagrams, and the two files that are git-ignored — found **fifteen contradictions**, and each is corrected in place with a dated note saying what was wrong.

  **The root cause was one thing, and naming it is the real fix.** Documents 01–08 are a specification written in the present tense, which is normal for a specification and indistinguishable from a description of a working system. "Commands accept an idempotency key" is a design decision; no endpoint has ever read one. Eleven of the fifteen were this. [`00-Workbook.md`](../00-Workbook.md) now opens with the reading rule — the specification says what should be, PROGRESS and STATUS say what is, and where they disagree the latter two win — and the gaps large enough to mislead are marked in place with the reason.

  **The four that were not that were genuine self-contradictions**, where two documents made incompatible claims about the same thing: doc 02 said ASP.NET Core Identity supplies local identity while its own §4 said the opposite; doc 04 described a CLI while doc 02 said there is no CLI; doc 06 said OIDC federation "is supported" while doc 02 had it as Selected-not-built; and doc 08 sent contributors to a second entry point that disagreed with `ONBOARDING.md`'s "one door".

  **Three duplicated facts were collapsed to one home each.** Identity's exported type list had been copied into doc 03, `LOCAL-DEVELOPMENT.md` and `CLAUDE.md`, and was three names short in all three — it now lives only in `BoundaryTests.cs`, which asserts it, and the copies were replaced with a pointer. Doc 03's hand-written source tree was eight capability folders behind; it now says `ls src/App` is the authority. And the question "does X exist?" gets a **single table at the top of this file**, which the specification documents link to instead of each hedging separately.

  **The worst single entry was doc 02's frontend stack**, which listed Material UI, MUI DataGrid, TanStack Query, React Hook Form, Zod, and a generated OpenAPI client. None has ever been installed. The real answer is four runtime dependencies, and `contracts.ts` is hand written and says so in its own header. Replaced with a table in the same Adopted/Selected form the backend already used, verified against `package.json`.

  **ADRs were corrected without being rewritten.** Two carried stale facts — ADR-007 names a folder that moved in ADR-017, ADR-009 names a library never used. Both get a dated Correction section confirming the *decision* is unaffected, and `adr/README.md`'s conflict rule is split accordingly: on a decision the ADR governs, on a fact about the code whichever text matches the repository governs, and the repository beats both.

  **Doc 09's "read all files under `docs/` before changing code"** contradicted `ONBOARDING.md`'s "do not start with the documents" and produced a confident memory of things no longer true. Replaced with four steps that start at this file.

  Evidence: every relative link in `docs/` resolves (checked by script); `dotnet build` 0/0; `dotnet test` 664/664; frontend typecheck and 303/303 unchanged, since nothing outside `docs/`, `CLAUDE.md` and the progress generator was touched.

- **2026-09-04 — Every source file says who owns it and how to change it.** The four-part header the maintainer settled on 2026-08-15 — Copyright / SPDX-License-Identifier / `Overview: Purpose, File Design, and Engineering` / `Usage:` / `Coding Instructions:` — is applied to all **347** hand-written files across four commits: 49 in Core and Identity, 145 in `src/App`, 60 tests, and 93 in the frontend, deploy scripts and CI.

  **Core's eleven were written by hand and the other 336 were transformed**, and the distinction is the point. `Overview` is where engineering judgement goes, and in Core that judgement is load-bearing: why `Money` refuses a mixed-currency add rather than converting, why `IAppendOnly` is an interface instead of a list of type names, why `ITenantContext` throws instead of returning null. Everywhere else an awk transformer moved the existing prose into the new shape — summary to `Overview`, `Use:` to `Usage:`, `Edit:` to `Coding Instructions:` — **without rewording a sentence**. Those headers carry the reasoning behind the CSRF session binding, the seven WebAuthn checks and the cursor-advance trap; a paraphrase would have been a downgrade dressed as a reformat, and a generic header repeated 347 times is noise people learn to skip.

  **Two classes of damage the transformer caused, found and repaired rather than shipped.** Capitalising the first word of each section turned commands into prose — `npm test` became `Npm test`, `await api(...)` became `Await api(...)` — across 47 files. The same rule title-cased lowercase module names, so the French catalogue introduced itself as `Fr` and the fetch wrapper as `Api`, across 25 more. Both were reverted by matching against the real filename and a list of code tokens, never by editing prose by hand.

  Seven files had free-prose headers with no `Use:`/`Edit:` to map, so their sections were written rather than derived: both compose files, four deploy scripts, and `vite.config.ts` — whose hanging indentation the dedent had flattened and which was rebuilt.

  **Both exclusions held**: the 50 generated files under `Migrations/` are untouched, because EF overwrites them, and no `.json` was given a comment it cannot carry.

  This reverses the standing "SPDX once at assembly level, no per-file licence headers" rule. SPDX is now declared **both** per file and at assembly level, which is deliberate rather than duplication: the per-file line is the authority for a file copied out of the tree, and the assembly attribute is what a package consumer sees. Doc 08 §5 keeps a note of what the rule used to say.

  Evidence: `dotnet build` 0/0, `dotnet test` **664/664**, `verify-e2e.ps1` PASS, all six PowerShell scripts parse, and the frontend gate — `npm audit` clean, typecheck, **303 tests**, production build — all pass.

- **2026-09-04 — A second person could now start.** The contributor path existed as three documents that disagreed about where to begin and no answer at all to "what should I do first". Both are fixed.

  **`docs/FIRST-TASKS.md` is the new part, and its value is that every item was checked against the code rather than imagined.** Eight tasks, each saying where it lives, why it is a reasonable place to start, what "done" looks like, and what to watch out for. Verified absent before being listed: the VIN decode, session list-and-revoke, import cancellation, passkey challenge pruning, ETags, and any route that creates a second administrator. Two items are marked **claimed** so nobody duplicates work in flight, and three more are named as deliberately unbuilt with a pointer to why.

  **CONTRIBUTING stopped being a third entry point.** It opened with its own reading list — after `README.md` sends people to ONBOARDING, and after doc 08 §8 was corrected this morning to do the same. It now assumes ONBOARDING has been read and covers only what is different about contributing: the loop, the four gates including the frontend four, the rehearsal habit, and a definition of done that names the four-part header, the catalogue rule and the five-band screen shape. Its branching section says plainly that the policy it describes is not how the repository currently runs.

  **Issue templates ask for the evidence this project runs on.** The bug form asks for the stable error code rather than the message, which document or file header made the claim, which seeded account was used — several are *meant* to get 403 — and which of the six languages was on screen, because defects have been specific to one before. The proposal form asks which of the five blockers applies, because for two thirds of the backlog that matters more than the idea. A config file routes security reports away from public issues.

  Evidence: every relative link in `docs/` and `.github/` resolves, checked by script.

- **2026-09-04 — The frontend gate that could not run, ran.** The header conversion's fourth pass was committed with the frontend unverified: Docker had stopped on this host, so `npm audit`, `typecheck`, `test` and `build` could not execute against the 83 changed frontend files, and the commit said so rather than implying otherwise. With Docker back, all four pass — `npm audit` clean, typecheck clean, **303 tests**, production build clean. The first attempt failed on a socket hang-up reaching the npm registry from a container that had just started, which is worth knowing: that failure looks like an audit finding and is not one.

- **2026-09-04 — A row now records who wrote it.** `TenantDb` stamps `CreatedBy` and `ModifiedBy` from `ICurrentUser` on save, alongside the timestamps it already stamped.

  **What was wrong.** `AuditableEntity.CreatedBy` defaulted to `"system"` and **nothing ever assigned it**. A query across the development database found `system` in every row of every table — 1,450 across 21 tables, including 134 deals and 185 repair orders created by signed-in people through the API. `ModifiedBy` was empty everywhere. Meanwhile [doc 03 §5](../03-Project-Structure.md) said "audit columns and the concurrency stamp are set on save" and `AuditableEntity`'s own header said "never set CreatedAt / ModifiedAt / CreatedBy / ModifiedBy by hand", which implies something else does. Both sentences are now true; neither was.

  **This was never blindness.** The audit trail records real actors and always has — `identity.AuditEvents` carries `ActorUserId`, and permission checks read `ICurrentUser` directly, so nothing ran unauthorised. What was missing is the ability to answer "who last touched this record" **from the record**, which is the question somebody asks with a customer on the phone rather than with a log viewer open.

  **`"system"` is kept as the fallback rather than throwing**, because three callers legitimately have no person behind them: the development seeder, tenant provisioning, and `dotnet ef` at design time. A row nobody asked for should say so, and a test asserts that it still does — so the fallback cannot quietly start naming somebody who was not there.

  **`CreatedBy` is never restamped on modify.** It answers a different question from `ModifiedBy` and is the more valuable of the two; overwriting it would destroy the only record of who originated a row.

  **Rehearsed twice.** Reverting the stamp to the constant fails exactly three of the four tests — the fourth expects `system` for seeded rows and correctly still passes. Restamping `CreatedBy` on modify fails exactly the one test that guards it, which is the subtle break the obvious test would have missed.

  **Not backfilled, and it cannot be.** The 1,450 existing rows keep `system`, because nobody recorded who wrote them and inventing an author would be worse than the gap.

  **Named and not done: `IdentityDb` has the same gap.** Its rows say `system` too. It is deliberately left for its own change, because the interesting case is a session row — written at the moment somebody signs in, when `ICurrentUser` is by definition not yet set — and that needs a decision rather than the same mechanical fix.

  Evidence: `dotnet build` 0/0, `dotnet test` **668/668** (was 664), `verify-e2e.ps1` PASS. Confirmed against the real database afterwards: rows written by `gm@dev.local` during the e2e run carry `11111111-1111-1111-1111-111111111111`.

- **2026-09-04 — D3 is settled: STAR is a wire format, not our vocabulary.** [ADR-023](../adr/0023-star-is-a-wire-format-not-our-vocabulary.md). `ContractFields` keeps its own names, and now says why in its own header so the question is not re-opened by the next person who meets STAR.

  **The research overturned our own premise.** Doc 11 §2 closed by saying adoption "means reading the specification", implying access was the obstacle. It is not: the STAR 5 and STAR 6 repositories, the OpenAPI definitions and the short codes are published openly at `docs.starstandard.org` under the **Eclipse Public License v1.0**, no membership and no login. The vocabulary was readable the whole time, so the decision had to be made on merit.

  **Made on merit, the answer is still to keep our own names**, for a reason the original framing missed. STAR is a *wire format*: its address alone offers a choice between five free-text lines and a structured form, with several elements repeating. `ProviderRecord.Fields` is a flat `string → string?` map, so it can carry STAR's names but not STAR's shape — and STAR-looking names on a non-STAR structure imply an interoperability nobody has built. Certification, where a manufacturer does it, is against real messages over real transport; renaming constants gets no closer to passing.

  **What STAR earns instead is the job of coverage checklist**, and it paid for itself immediately. Measuring our eleven customer fields against STAR's address structure found three gaps, recorded in the ADR and deliberately not built:

  - **`customer.address.area` collapses state and county.** STAR separates `StateOrProvinceCountrySub-DivisionID` from `CountyCountrySub-Division`. This one matters commercially: doc 11 §3.3 records that US sales tax varies by state, county and sometimes city, so **our address cannot express the thing that drives the tax calculation**.
  - **No `AddressType` or `UseCode`** — billing, residence and garaging addresses are indistinguishable, and garaging address drives insurance and some tax.
  - **Two address lines against STAR's five.** Rarely a problem; now a known limit rather than a surprise.

  None was built, because a field nothing populates is speculation — there is one sink and one connector, and that connector is a test double.

  **Two factual corrections to doc 11 §2**, both from STAR's own material: it publishes **200+** message formats across **35+** business areas, not the "145 across 40" we had. Both numbers were wrong, in opposite directions.

  Evidence: `dotnet build` 0/0, `dotnet test` **668/668**, every relative link in `docs/` resolves. Sources read 2026-09-04 and cited in the ADR.

- **2026-09-05 — D2 is settled: compliance is a baseline, a pack, and a posture.** [ADR-024](../adr/0024-compliance-is-baseline-pack-and-posture.md). The question arrived as "can the system dynamically fit multiple legal and tax rules to the user's current location?" and the research changed its shape three times before it could be answered.

  **"Current location" is never the input.** A vehicle sale is taxed at the buyer's **registration address**, not the dealer's location and not where anyone is standing — cross a state line and the home state collects use tax at registration instead. For privacy the governing facts are the data subject's residence and the deployer's establishment. All of those are addresses already on records we hold. **IP geolocation, browser locale and `Accept-Language` are rejected outright** and recorded as rejected: they answer a different question from the one the law asks, three of them are attacker-controlled, and a corporate VPN would change a tax rate.

  **Tax is two problems and only the smaller one is ours.** Rates and boundaries are unbuildable — 13,000+ US jurisdictions, and a single ZIP can span several of them because ZIPs are USPS delivery routes. But the automotive arithmetic **is** ours, and it is exactly what a general retail tax engine gets wrong: the **trade-in credit** (most states tax price minus trade; California taxes the full price, with the $20,000 car and $4,000 trade as CDTFA's own worked example), doc-fee taxability and caps, and lease basis. So the seam is a rate *provider* that is replaceable, and a taxable *basis* that lives next to `Deal.Subtotal` where the sign conventions already are.

  **The finding that made it shippable: rates are free and liability-shifted for a third of the country.** The 23 Streamlined Sales Tax member states each publish a rate file and a boundary file, free, quarterly, keyed to 5- and 9-digit ZIP — and *"the states hold a business harmless for charging too much or too little tax if the business calculated and collected the incorrect tax based on the state's rate and boundary files."* That removes the need for rooftop geocoding in those states and makes **provenance**, not accuracy in the abstract, the load-bearing engineering requirement.

  **Privacy is the opposite shape, and a rule table would have been a mistake.** A dealer arranging financing is a GLBA financial institution; Virginia, Colorado, Connecticut and Utah then grant an **entity-level** exemption while California grants only a **data-level** one. The same dealership is exempt in one state and not in another, per field. A rule reading `if (state == "VA") exempt` is wrong the moment they sell to a Californian — and encoding it would mean the software silently issuing a legal opinion about its operator. **Declined.** Underneath all of it the FTC Safeguards Rule applies to every US dealer regardless of state, so MFA, encryption at rest and in transit, and the audit trail become **product baseline** rather than posture.

  **And self-hosting moves the obligation.** AGPLv3, deployed by the dealership: they are the controller and the taxpayer of record, not us. GDPR Recital 78 encourages producers to design for it; it does not make a producer a controller. So the product's job is to make compliance *possible and evidenced*, not to enforce it — a claim it could not honour.

  **The decision.** Three dials with different lifetimes: a **baseline** in code that no deployment can switch off; a **pack** of one jurisdiction's versioned, sourced, human-reviewed *data* — flags and numbers, never logic; and a **posture** the dealership owns. Reference data lives in the host catalog and is effective-dated so a March deal still reads as March. A deal stores the tax it charged as frozen evidence with pack version and provenance, and **`entered-by-person` is a valid provenance** — which is what turns an unsupported jurisdiction from a blocker into a label, and what makes all of this buildable before a single pack exists.

  **What D2 resolves to:** US first for compliance depth (its floor is the strictest of the candidates, so building to it wastes nothing if the market answer changes), architecture stays country-neutral, first pack is `Manual` and the second is SST-23 — which notably requires choosing no individual state. **Titling and registration stay blocked** on a state list, correctly.

  **A doc 07 claim corrected.** The roadmap argued that US-only compliance and six shipped languages "cannot both be the priority". The tension was overstated: shipping six languages is *done* and carries no ongoing compliance cost. A language is not a jurisdiction — Spanish is spoken in the United States, and Arabic RTL is a UI capability, not a promise about anybody's law.

  **Named and not built.** Nothing here is code. The first consequence on the critical path is the gap ADR-023 found last week: `customer.address.area` collapses state and county, and the county is what drives the rate.

  Evidence: `dotnet build` 0/0, `dotnet test` 668/668, every relative link in `docs/` and `.github/` resolves. Sources read 2026-09-05 and cited in the ADR.

- **2026-09-05 — a background worker that does not name a requester no longer compiles.** The structural half of the tenant job context, which the stamping half (`5324c0d`) left open. `JobContext` names the dealership **and** the person the work runs as, and `ITenantScopeFactory.OpenAsync` takes nothing else — no string overload, so the omission is a compile error rather than a code review comment. **This closes the last unmet I1 exit criterion that was not blocked on somebody else.**

  **What was actually wrong.** `ImportWorker`'s own header said "it runs as the person who asked", and it did — by calling `ICurrentUser.Set` in the middle of `ProcessAsync`. That sentence was true of `ImportWorker` and true of nothing else, because nothing enforced it; the second background worker would have inherited the comment and not the behaviour. Worse, the call sat **after** the row claiming the job had already been written, so the claim was attributed to nobody, and an import that failed before that line was recorded as the system's doing while one that failed after it was recorded as a person's. **The same event, two answers, depending on how far it got.**

  **Two scopes per job now, and that is the fix rather than a cost.** Polling for work and claiming it is genuinely nobody's request, so the dispatcher opens `UnattendedJob.For(slug, "import dispatcher")` and its writes say `system` — which is what happened. Running the job is the requester's, and that scope is opened only once their id is known from the claimed row. Failure is recorded in whichever scope the work was in, so the answer no longer depends on timing. The gap where a dealership is suspended between the claim and the run is handled too: the job is failed with that reason instead of being stranded in `Running` forever.

  **`Set` is off `ICurrentUser` and off `ITenantContext`.** It lives on the concrete `CurrentUser` and `TenantContext`, which only `CurrentUserMiddleware` and `TenantScopeFactory` resolve. A feature injected the interface can **ask** who the caller is and which dealership it is in, and cannot **decide** either. Registration is concrete-first (`AddScoped<CurrentUser>()`, then the interface as a delegating factory) so both resolve to one instance per scope.

  **Unattended is an answer, not a loophole.** Retention sweeps and the dispatcher are really nobody's request, and inventing a requester would attribute a machine's work to a person. What it is not is a *default*: it has to be typed, it costs a required reason, and it is greppable. `RequestedBy` with `Guid.Empty` throws, because an anonymous job wearing a default Guid satisfies every type in the system and passes a permission check against nobody's roles.

  **A finding worth keeping.** The first version of the new attribution test assumed an unattended scope could write a customer and have the row say `system`. It cannot — `CustomerService` reads `ICurrentUser.Id`, which throws. That is the design working, so the test was rewritten to assert it: **an unattended job cannot reach a permission-checked service at all.** A sweep that passed every check because there was nobody to fail is the hole this change exists to close.

  **Three architecture tests stand in for the compile error**, since a call that does not compile cannot be written down in a test: `OpenAsync` has exactly one overload and its first parameter is `JobContext`; neither interface has a `Set`. Restore any one convenience and the matching test fails.

  **Rehearsed four ways, each failing exactly what it should.** Dropping the caller stamp fails 18 tests including the entire import path. Re-adding a string overload fails exactly one architecture rule. Putting `Set` back on either interface fails exactly its own rule. Removing the empty-Guid guard fails exactly the unit test that names it.

  **A word in doc 04 corrected.** It required a *signed* job context. A signature protects a job description crossing an untrusted boundary; ours is built in-process from a row the worker just read out of the tenant's own database, so it would be signing our own data to ourselves. The constructor is the guarantee. If a durable external queue ever arrives, the signing question arrives with it — that is a review trigger, not a task outstanding.

  **Named and not done.** The claim is an `ExecuteUpdate`, which bypasses the change tracker, so it stamps no `ModifiedBy` and rotates no concurrency token. That is unchanged by this work and harmless — the conditional update *is* the concurrency control — but it means a claimed job's `ModifiedBy` still shows whoever last saved it through the tracker.

  Evidence: `dotnet build` 0/0, `dotnet test` **686/686** (was 668), `verify-e2e.ps1` PASS.

- **2026-09-05 — an address can now say which county it is in.** The gap [ADR-023](../adr/0023-star-is-a-wire-format-not-our-vocabulary.md) found by measuring our eleven customer fields against STAR's address structure, and [ADR-024](../adr/0024-compliance-is-baseline-pack-and-posture.md) put on the critical path: **US sales tax varies by state, county and sometimes city**, and `customer.address.area` carried only one of them. The address could not express the thing that decides the rate.

  `Address.County` sits alongside `Address.AdministrativeArea` — the state, province or region — through the domain, the EF mapping and its migration, the customer API, both CSV directions, the connector contract, and the customer screen. Null in most countries, which is expected: it is a sub-division slot, not a promise that every address has one.

  **Nothing infers one from the other.** Not from the state, not from the postcode. An absent county is absent ([ADR-021](../adr/0021-coerced-values-become-absent.md)) because a guessed county is a wrong tax rate on a real invoice that looks exactly like a right one — and a test named `A_state_with_no_county_given_does_not_borrow_one` fails if anybody adds the convenience.

  **No version bump, and a doc corrected to say why.** `ContractFields`'s header claimed that any field added there forces one. [Doc 05 §2](../05-Integration-Framework.md), which governs the rule, says the opposite: *"Adding an optional field is compatible. Removing, changing meaning, or changing requiredness creates a new major version."* A connector compiled against v1 simply does not populate a field added later. The header now says what actually forces a bump — changing what an existing name means — because that is the case that is invisible at the call site.

  **The fixture connector now sends `IL` and `Sangamon`**, two different words, so a sink that folded one into the other would be caught rather than looking correct. Springfield IL 62704 really is in Sangamon County; seeded data that is geographically wrong teaches the wrong thing about a field whose whole purpose is deciding a tax rate.

  **Rehearsed three ways**, each failing exactly its own test: inferring the county from the state fails `A_state_with_no_county_given_does_not_borrow_one`; dropping the county from the export fails `A_county_survives_the_round_trip_apart_from_its_state`; making the sink read the state into the county slot fails `Contract_fields_arrive_as_a_usable_customer`.

  **Named and not done: the address TYPE**, the second gap ADR-023 recorded. A customer holds one address and a type only discriminates between several. The registration or garaging address that decides a deal's tax is a fact about the **deal**, frozen with it (ADR-024 R3), so it lands with the tax work rather than as a second customer field nothing would populate.

  Evidence: `dotnet build` 0/0, `dotnet test` **689/689** (was 686), `verify-e2e.ps1` PASS, and the frontend gate clean — `npm audit`, typecheck, **303 tests**, production build.

- **2026-09-05 — a sweep can no longer reach a capability that would authorize against nobody.** The half of the tenant job context that was left as a judgement call rather than a compiler rule. Asking for it back was the right call: the brief said "runs as nobody must fail to COMPILE", and `JobContext.Unattended` made it a documented convention instead.

  **Two types, not one with a nullable field.** `JobContext.RequestedByUserId` is a `Guid`, not a `Guid?` — there is no value of it meaning nobody, and no factory that leaves it unset. Work nobody asked for is `UnattendedJob`, a separate record neither assignable to nor from the other. That separation is what lets the factory return two different **kinds of scope**, which is where the guarantee actually lives.

  **`UnattendedScope` has no `IServiceProvider`.** Its only way out is `Get<T>() where T : IUnattendedSafe` — a new allow-list marker in Core, the mirror of [`IAppendOnly`](../../src/Core/IAppendOnly.cs) and for the same reason: a rule the compiler and the reviewer both see beats a rule in a header. Today exactly one type carries it, `TenantDb`, which reads `ICurrentUser` only to choose between a person's id and `system` and never reads `.Id` unguarded.

  **Proven by making it fail.** A file was added that tries `sweep.Get<ICustomers>()`, and the compiler rejected it:

  > `error CS0311: The type 'DealerFOSS.Customers.ICustomers' cannot be used as type parameter 'T' in the generic type or method 'UnattendedScope.Get<T>()'. There is no implicit reference conversion from 'DealerFOSS.Customers.ICustomers' to 'DealerFOSS.Core.IUnattendedSafe'.`

  The other half of the brief too: `OpenUnattendedAsync("northgroup", ct)` and `OpenAsync(UnattendedJob.For(...), ct)` are both `CS1503`. A worker that names no tenant, or tries to run unattended down the attended path, does not build.

  **What the guarantee is, stated exactly, because it is narrower than it sounds.** An unattended job cannot use a service that authorizes against a person. It can still write through `TenantDb` — attributed to `system`, under the append-only rules, through domain constructors that enforce their own invariants. What is closed is the case where a sweep passes every permission check because there is nobody to fail one.

  **Three guards, and the first version of them was not enough.** Two architecture rules assert the constraint on `Get<T>` and the absence of any `IServiceProvider` on the type. Rehearsing found a hole: neither caught somebody marking `ICustomers` as `IUnattendedSafe` to make a compile error go away — only an integration test did. So a third rule was added, an **allow-list of every type permitted to carry the marker**, in the same shape as the Identity exported-types list that sits above it in the same file. Re-rehearsed: it now fails.

  **Rehearsed five ways in total.** Two compile-error rehearsals above; adding an `IServiceProvider` back to `UnattendedScope` fails two tests; marking a capability `IUnattendedSafe` fails the allow-list; and the earlier four from `dc4dd14` still hold.

  **`MarkFailedAsync` now takes a `TenantDb` rather than a scope**, because its two callers hold two different kinds. Recording a failure is the same act either way, and the signature says so rather than forcing one caller to pretend.

  Evidence: `dotnet build` 0/0, `dotnet test` **694/694** (was 689), `verify-e2e.ps1` PASS.

- **2026-09-05 — the progress page now notices when its own recommendation has been done.** `local/progress.html` recommended "Tenant-aware background job context" for a day after it was committed. The page did flag staleness — *"3 commits since this was chosen"* — but that note sat **below** the copyable prompt and said only to check. The prompt was copied and the work asked for a second time.

  The recommendation now carries a `DoneWhen` phrase, and the generator checks three independent signals before rendering it: that phrase appearing in `STATUS.md`, a matching register row marked `Done`, and a matching exit criterion now ticked. Any of them replaces the quiet note with a red **ALREADY DONE** banner, placed **above** the prompt rather than under it.

  Rehearsed by pointing `DoneWhen` at a phrase that is already in `STATUS.md`: the banner appeared, and disappeared again on restore. The lesson generalises past this page — a staleness warning that is quieter than the thing it warns about is decoration.

- **2026-09-09 — I2 has a scorecard, and it says two of seven.** The phase was never scored. Meanwhile a good deal of I2-shaped work landed out of order, which [doc 09](../09-Implementation-Roadmap.md) §4 says must be recorded here — and it had not been, so nobody could say what the tax work would be built on.

  Scored by running the checks rather than reading the code. **Two met, three part-met, two not met**, written up under Exit criteria above.

  **Met:** checkpoint safety under interruption (five `ConnectorRuntimeTests` cases, including a provider that skips the start of a window not getting the cursor moved past the hole), and a repeatable trial import with stable counts. All 43 cited tests were run, not cited from memory.

  **Part-met:** duplicate and partial-page handling hold, **reordering has no test and deletes are not modelled at all** — no tombstone, no `IsDeleted` anywhere in `src/App/Integrations`. Quarantine is inspectable but **not replayable**, and the code already admitted it: `QuarantinedRecord.cs` carries the comment *"Does not replay it — nothing replays yet."* Export preserves IDs but carries **no relationship manifest and no documents**, despite a Documents capability existing.

  **Not met:** fixture-tested status is not displayed honestly because it is **not displayed at all** — `CertificationStatus` has four levels and the one connector correctly declares `FixtureTested`, but there is no connectors screen in the frontend. A status nobody can see cannot be shown honestly.

  **A false claim corrected in this file's own table.** The "does it exist?" row for the connector runtime said `API · Screen`. There is no screen: no feature folder, no route, nothing. That table is the single home for "is X built" and other documents link to it instead of hedging, so a wrong row there is worse than a wrong sentence anywhere else.

  **Named and not built**, checked by name across `src` rather than inferred: durable inbox, outbox, leases, replay, SFTP, profiling, versioned mappings, duplicate-candidate workflow. Field ownership is a concern named in a comment in `CustomerRecordSink`, not a rule anything enforces.

  **What this does not say** is that I2 is behind. It was never claimed to be started, and the seam the missing pieces would hang from is real: compiled discovery, a versioned contract envelope, per-feed cursors with hold counting, quarantine with retention, run history, and CSV in and out with control totals and checksums. What changed today is that the gap is now written down instead of assumed either way.

  Evidence: 43 cited integration tests run and passing; `dotnet build` 0/0, `dotnet test` 694/694.

- **2026-09-09 — a deal can carry tax, and every figure says where it came from.** The first build under [ADR-024](../adr/0024-compliance-is-baseline-pack-and-posture.md), and the part that needed no rate table and no market decision.

  **The taxable basis is ours, and it is the part a general tax engine gets wrong.** `Deal.TaxableBasis(TaxBasisRules)` reads three flags a pack supplies — does the trade-in reduce the basis, is the documentation fee taxable, are other fees taxable — and does the arithmetic here, in code under test, because a pack carries data and never logic (R1). The pair of tests that matters is the same deal scored twice: **$19,500 in most states and $29,500 in California**, because CDTFA Publication 34 taxes the full price where most states tax price-less-trade. Ten thousand dollars of basis, on one ordinary deal, in a direction no general retail engine can express.

  **`ChargeKind.DocumentationFee` is now its own kind**, separate from `Fee`. Tax treats them differently — the dealer's doc fee is part of the taxable price in most states while registration and title are government pass-throughs — and a basis calculation cannot tell them apart if they share a kind. **Named and not migrated:** existing deals that recorded a doc fee as `Fee` still read as `Fee`, so they would be taxed under the other flag. There are no real deals yet, so nothing was rewritten.

  **A tax line is the answer, not a pointer to one** (R3). It carries the amount, the basis it was worked out on, the rate, the jurisdiction, the pack and version that produced it, and the address that resolved it. Rate is stored at six decimal places rather than money precision, because 8.6375% is a real combined US rate and rounding it to 8.64% would hide the rounding somewhere nobody can see.

  **Provenance is required, and "a person typed it" is a real answer** (R4, R5). That is what lets the product work in a jurisdiction nobody has written a pack for — which is every jurisdiction today. Two refusals guard it in both directions: a line claiming `Pack` that cannot name its pack and version is rejected as unauditable, and a person-entered line carrying a pack id is rejected for claiming an authority nobody exercised. Tax with no address is refused too, because an address is how a rate is defended later.

  **One deviation from a literal reading of R3, named rather than hidden.** Tax lines are **replaceable while the deal is Draft** and frozen the moment it leaves, rather than `IAppendOnly` from the first keystroke. A salesperson fixing a postcode before anyone has seen the deal is not correcting history, and forcing a reversing entry for it would fill the record with noise that hides the corrections that matter. "Frozen at the moment of sale" holds exactly as written.

  **Not built, and none of it pretends to be:** rate tables, the `JurisdictionPacks` / `JurisdictionRates` / `JurisdictionRules` host-catalog data, the SST-23 pack, the resolution order, and any screen. Tax is API-only today.

  Evidence: `dotnet build` 0/0, `dotnet test` **711/711** (was 694) — 14 new unit tests on the basis and the line, 3 new integration tests through the real endpoint — `verify-e2e.ps1` PASS.

- **2026-09-09 — the tax band, so a person can actually charge tax.** The API landed earlier today with no reader; a capability only an HTTP client can reach is not a capability. `DealTax` sits on the deal desk while the terms are open, `SoldTax` shows the same lines read-only once they are frozen.

  **Every line says on screen where its figure came from.** Not a debug detail — it is the whole point of ADR-024 R4. Nothing computes tax yet, so the lede says so plainly rather than leaving a person to assume a rate table is behind the boxes, and a frozen line carries a chip reading *"a person entered it"* or the pack's id and version. The chip says its own name in words; the colour is reinforcement, never the message.

  **The rate is typed as a percentage and stored as a fraction.** Anybody entering one is reading "6.25%" off a table, and asking them to divide by a hundred is how a deal gets taxed a hundred times over. **This is the change that would have been wrong invisibly** — every figure on the screen stays right if the conversion breaks, because the amount is typed separately and only the stored rate is wrong. A component test asserts the wire carries `0.0625`, and it was confirmed against the running application: a rate typed as `6.25` reads back from the API as `0.0625`.

  **The county has its own box**, beside the state rather than folded into it, because a US rate depends on both — the gap closed on 2026-09-05, now visible to the person doing the work.

  **Driven end to end in a browser, not only mocked.** A Draft deal priced at $24,000 with a $500 documentation fee: tax entered through the form, saved, and the deal read back at **$26,031.25 due** with the rate stored correctly and the county intact. Then submitted, and the editor correctly disappeared in favour of the read-only view with the provenance chip — mounted rather than merely disabled, because a disabled form still looks like somewhere to type.

  **All six languages**, translated rather than copied. `tax.fromPack` is `{pack} v{version}` in every locale and is on the untranslated-keys allowlist with the reason: it is an id and a number with no words in it.

  **Still not built:** rate tables, the host-catalog pack data, the SST-23 pack, and the resolution order. Nothing computes tax; a person enters it, and the record says so.

  Evidence: `dotnet build` 0/0, `dotnet test` 711/711; `npm audit` clean at high, typecheck clean, **310 frontend tests** (was 303), production build clean.

- **2026-09-09 — a dealership with 484 customers in it, and the two bugs that found.** The demo dataset exists because the maintainer said progress was not visible, and the measurement agreed: of 50 commits in 30 days, roughly seven changed anything a dealership would notice, and the demo database held **three customers, three cars and two deals**. Every finished feature looked like a prototype against empty tables.

  `DemoData` fills a seeded tenant with **484 customers, 214 cars across every stock state, 143 enquiries, 94 deals of which 42 are delivered and 91 carry tax, and 162 repair orders with 84 invoiced**, spread over 95 days so the dashboard has a previous month to compare. Behind `Seed:Demo`, off by default — the test suite and `verify-e2e` both assert against the small set — deterministic from one fixed seed, and idempotent.

  **It goes through the services, not through `TenantDb`.** Delivering a deal posts to the ledger and invoicing a repair order posts to the ledger; writing rows directly would have meant re-implementing both postings where they would drift. The result is a trial balance of **1,860,434.89 on each side** that balances because the application balanced it.

  **And that is how it found two shipped bugs, both from this morning's tax commit, both invisible to the test suite.**

  **A deal with tax on it could not be delivered at all.** Tax went into `AmountDue`, the delivery posting debited the full amount and credited nothing against it, so every taxed delivery was refused with "an entry must balance" — out by exactly the tax. There was no account for it either: sales tax collected is a **liability**, money the dealership holds for the state and never owns, and `AccountCodes` had no liability at all until now. Booking it as revenue would have inflated the top line by the tax on every car.

  **A deal with a documentation fee could not be delivered either.** `ChargeKind.DocumentationFee` became its own kind this morning so the taxable basis could treat it differently from a registration fee, and the posting's fee mapping was never told — so the fee sat in the amount due with nothing credited against it, out by exactly 499.00.

  **Both were missed for the same reason**, and it is worth naming: no test delivered a deal that carried tax, and no test used the new charge kind. The features were tested; their effect on the *next* step was not. One integration test now covers both, and reverting either fix fails it while the untaxed delivery test carries on passing — which is exactly how the gap looked from inside.

  **The seeder resumes rather than restarting.** Each section guards itself, because the first run died on a VIN with 480 customers already written and a single top-level guard would have left the tenant permanently half full. A later run finished the job, and a `DeliverApprovedAsync` step recovered 53 deals stranded at Approved by the balance bug — which is why no database had to be dropped.

  Evidence: `dotnet build` 0/0, `dotnet test` **712/712** (was 711), `verify-e2e.ps1` PASS.

- **2026-09-10 — Four ordinary dealership jobs were walked through the running application, and one of the four finishes.** Written up in [DEALER-DAY.md](DEALER-DAY.md), which names a file and line or a live measurement for every claim.

  The walk existed because the numbers on this project had stopped meaning anything: 50 commits in 30 days, eight stages all showing progress, exit criteria climbing — and, it turns out, **no way to put a car into stock**. A walk-in becomes a sold car, end to end, including tax and an F&I product; a car arriving cannot even start; a service job reaches Invoiced and cannot be paid; and "what did the month make" can only be answered as gross.

  **Three findings would stop a real installation.** Account 1300 Vehicle inventory stands at minus $993,190, because delivery credits inventory and nothing anywhere debits it — the ledger balances and is externally wrong. Every sale debits Cash on the spot and the chart has no customer receivable, so a fleet customer cannot be invoiced on account and a car sold on finance reads as cash from the buyer. And "The numbers on this deal" prints a column that comes to $33,000 above a total of $36,331.25, because tax is in `amountDue` and has no row — the same defect the trade-in row's own comment was written about.

  **The scope register was rewritten from the walk.** It held four Build rows, all small, none of which would have moved any of the four jobs; it now holds 22, in the order a dealership feels them. `local/progress.html` leads with **1 of 4 jobs** and demotes the stage percentage to a planning indicator, because counting commits is what let 50 of them read as progress.

  **Three of my own readings during the walk were wrong and are recorded as retractions** — F&I catalogue prices, the saved tax line, and duplicate charge lines were all fine, and `innerText` not reporting `<input>` values caught me three times in one session.

  Nothing was changed in `src/` by this walk: it is a measurement, and the fixes are now register rows. Evidence: `dotnet build` 0/0, `dotnet test` 712/712, `verify-e2e.ps1` PASS.

- **2026-09-10 — A dealership can now buy a car, not only sell one, and the balance sheet knows about it.** Job B of the dealer-day walk went from *cannot start* to *completes end to end* on the day it was found. Two of the four jobs now finish.

  **Three things landed together, because none of them is any use alone.** A screen to take a car into stock, creating the vehicle record and the unit at once — a car arriving is almost always one nobody has seen, and making somebody create it elsewhere and come back is the shape that already makes a walk-in enquiry impossible. A way to move a car between stock states from its own record, so a car can finally leave Reconditioning; only the moves the domain allows are offered, and **Sold is never one of them**, because a car is sold by delivering a deal and that is what posts the sale. And the posting that had never existed: `IAccounting.PostStockPurchaseAsync`, debiting 1300 and crediting 1000.

  **Account 1300 went from minus $993,190 to plus $2,697,960.** The seeded dealership's existing two hundred cars were back-filled rather than requiring the database to be dropped — a demo that has to be rebuilt to show a fix is one nobody looks at twice.

  **Fixing it exposed the next lie, which is the point.** Cash is now minus $2,510,729, and that is the correct double-entry consequence of a chart with no way for a dealership to have any money: no opening balance, no capital, and no floorplan, so $3.7M of stock was bought out of an account that started at nothing. Before, inventory lied; now inventory is right and cash is wrong for a reason that has a name and two new register rows. Neither was visible while the first lie covered for the second.

  **Two rehearsals on the backend and two on the frontend.** Disabling the posting failed exactly the two new ledger tests while the other thirteen — including both delivery tests — stayed green, which is precisely why this was missed for so long. Reversing the debit and credit still balanced and was still caught. On the frontend, defaulting an empty cost to zero failed the test that guards the distinction between *unknown* and *nothing*; adding Sold to the Available moves failed **nothing**, because that test only opened a car in Reconditioning — so it was broadened to every movable state, and then it caught it.

  **A pre-existing test caught a real bug in the new code.** The first draft keyed the duplicate-posting check on the stock number alone, and `InventoryTests.The_same_stock_number_is_allowed_at_a_different_rooftop` refused it: a stock number is unique per rooftop, not per organization. It is the same mistake the workshop's job numbering makes on screen.

  Verified again in a browser rather than assumed: WALK01, a 2022 Mazda CX-5 at $19,750, taken in, moved Incoming → Reconditioning → Available with a note on each move, posting `1300 D19750 / 1000 C19750`. Evidence: `dotnet build` 0/0, `dotnet test` **715/715** (was 712), `verify-e2e.ps1` PASS, frontend `npm audit` clean, `npm run typecheck`, `npm test` **319/319** (was 310), `npm run build`.

- **2026-09-11 — A bill can be owed, and then paid. Three of the four dealership jobs now finish.** Job C went from *reaches Invoiced and stops* to *completes end to end*.

  **The gap was not a missing screen, it was a missing idea.** Delivering a car and invoicing a job both debited 1000 Cash for the whole amount on the spot, so the books asserted that every customer paid in full the moment they were billed. A fleet customer on account, a deposit, a part-payment and a lender's settlement cheque were all unrepresentable, and the bank balance was wrong by everything anybody was still owed.

  **Account 1100 and a customer sub-ledger.** Account 1100 alone answers "we are owed $84,000"; only the sub-ledger answers "and $19,000 of it is Ashgrove Couriers, six weeks old". Both are kept, and they must agree — the sum of what is outstanding in the sub-ledger is the balance of 1100 in the ledger. Verified live: 1100 read **$110.00** and "who owes us" totalled **$110.00**.

  **What is outstanding is derived from the payments and never stored**, so it cannot drift from the rows underneath it. Overpayment is refused rather than absorbed, because the difference belongs to the customer and somebody has to give it back — credit balances are a real thing, are not built, and are now a register row rather than a silent rounding.

  **A lender settling a financed car is a payment method**, not a different kind of debt: what the dealership is owed does not change with who hands the money over.

  **One defect only walking it could find.** The payment band looked its receivable up once, when the record was opened, and never again — so invoicing a job in the same session showed no band at all. It rendered perfectly on a fresh page load, which is the one place nobody was looking, and every unit test passed because they mount the band against a bill that already exists. Fixed with an explicit dependency, and there is now a test that fails without it.

  **Two rehearsals found weak tests rather than confirming strong ones.** Allowing overpayment failed the right test. But computing the outstanding figure locally instead of taking the server's answer **passed** — the fixture's numbers happened to equal naive subtraction, so the test was proving nothing; it now uses a server reply that local arithmetic cannot produce. Earlier the same day, adding Sold to the stock moves failed nothing because that test only opened a car in one state.

  **A pre-existing test caught a real bug in the new code**, again: the first draft keyed the duplicate-posting check on a stock number alone, and a stock number is unique per rooftop, not per organization.

  **Not back-filled, deliberately.** The seeded dealership's historical deliveries and invoices stay posted as cash. They were genuinely recorded that way at the time, and rewriting a posted ledger to look tidier is the one thing an accounting system must not do.

  **Known operational note:** account 1100 reaches existing dealerships through the seeder and tenant provisioning, both of which top up missing accounts from `AccountCodes.Standard`. That is the same route account 2100 took on 2026-09-09. A tenant provisioned before this change and never re-provisioned would need the account adding before it could post — there are none today, and it is worth a real mechanism before there are.

  Walked in a browser rather than assumed: RO-1083, a full service at $240, invoiced, then a $100 deposit by card and a $140 balance in cash, leaving `1100 D240 / 4200 C240` followed by two payments moving 1100 to 1000. Evidence: `dotnet build` 0/0, `dotnet test` **725/725** (was 715), `verify-e2e.ps1` PASS, frontend `npm audit` clean, `npm run typecheck`, `npm test` **327/327** (was 319), `npm run build`. Built on .NET SDK 10.0.401 after the host installation went missing mid-session and was reinstalled; `global.json` rolls forward and needed no change.

- **2026-09-11 — A dealership can say what the month made. All four dealership jobs now finish.** Job D went from *answered as gross only* to *completes end to end*, and it took the two lies the previous two jobs had exposed with it.

  **There were no expense accounts. None.** Not wages, not rent, not advertising. The system could record everything a dealership earned and nothing it spent, so "what did the month make" could only ever be answered as gross and net profit did not exist. The chart now carries five overheads, a floorplan liability, and an equity account — a business with no capital cannot buy anything, and a balance sheet with no equity section does not balance in any form a person would recognise.

  **A journal entry can be written by hand**, behind `Accounting.ManualEntry` — deliberately NOT `Accounting.Post`, which a salesperson holds because delivering a car posts the sale. That is posting a consequence of work somebody did; choosing the accounts and the amounts is the most powerful thing anybody can do to a set of books. `IAccounting`'s header used to say there would never be such a method; it now says why that changed and how it is fenced.

  **Two reports, with screens.** A profit and loss whose departmental half calls the same method the dashboard reads, so the two cannot disagree. A balance sheet that states whether it balances and says so *loudly* when it does not — a plausible page with a hole in it is worse than an alarming one.

  **A floorplan liability, chosen per car**, because most lots carry both financed and outright stock. Walked: rent of $4,500 recorded by hand, then an opening entry moving $2,717,710 of stock funding onto the lender. Cash went from **minus $2,530,239 to plus $182,971**, net profit from *did not exist* to **$223,378**, and the balance sheet balances at $3,019,333.

  **Adding the two reports up by hand found a hole nothing else would have.** They disagreed by $663.60. The first version of the P&L named its five overhead accounts explicitly, so 5400 Internal service charge — an expense, and not a department's cost of sales — appeared on no part of the report at all. The seeded dealership had $1,196 in it and the page did not mention it: money spent into an account that showed on no report. The report now asks the chart which accounts are overheads rather than consulting a list in code, so a new expense account is on the report the day somebody adds it. With that fixed, all-time net profit and earnings-to-date agree exactly at $223,910.40.

  **Five rehearsals.** Putting cost of sales into the overhead list, using `Accounting.Post` instead of the new permission, dropping earnings from the balance-sheet check, always crediting cash instead of floorplan, and reverting to the hard-coded overhead list — each failed exactly the intended test and nothing else. On the frontend, softening the does-not-balance alarm to a quiet note, and letting an unbalanced entry through, both failed the right test.

  **A guard caught the new permission before any test did:** `Role.Grant` refuses anything not in `Permissions.All`, so the whole suite went red until the catalogue listed it. That is the catalogue working as designed.

  **Known operational note, unchanged from yesterday:** the new accounts reach existing dealerships through the seeder and tenant provisioning, both of which top up from `AccountCodes.Standard`. A tenant provisioned before this and never re-provisioned would need them adding. There are none today; it is worth a real mechanism before there are.

  Evidence: `dotnet build` 0/0, `dotnet test` **736/736** (was 725), `verify-e2e.ps1` PASS, frontend `npm audit` clean, `npm run typecheck`, `npm test` **340/340** (was 327), `npm run build`.

- **2026-09-11 — A part comes off the shelf at cost when somebody bills it in a browser.** The last obviously-false figure a dealer principal reads.

  **This was never missing machinery.** `src/App/Parts` has been a complete capability since it was built — catalogue, stock receipts, average costing, stock levels — and `verify-e2e.ps1` has proved its costing on every run since: *booked in 20 at 7.00, sold 2, cost recorded 14.00, 18 left on the shelf*. The repair-order line contract has always accepted a `PartId`. The screen simply never sent one, so every part billed in a browser was free text: 5300 and 1400 never posted, and the dashboard stated a 100% margin on service as fact.

  A picker on the line now offers what is on the shelf with the quantity beside it, fills the description from the catalogue and leaves it editable, and says what is in stock so nobody bills two of a part there is one of. **Free text is still there and is named as a choice** — "Not from stock (type it below)" — because a one-off item bought for one job never enters the catalogue and still has to be billable. A catalogue that will not load falls back to free text rather than stopping the workshop.

  Walked: RO-1084, two brake pad sets billed at $24 off a shelf holding 20 at $7. The line froze `cost=14`, the entry read `1100 D24 / 5300 D14 / 1400 C14 / 4300 C24`, and the shelf went to 18. Service cost on the dashboard went from **$0 to $14** — the first parts cost ever recorded through a browser. It still *rounds* to a 100% margin, because every historical part in the seeded dealership was billed as free text and those lines are not rewritten; anything billed from now on carries its cost.

  **Two rehearsals, and one of them was answered by the runner rather than an assertion.** Removing the `partId` from the payload — the exact defect that existed — failed two tests. Letting a catalogue-load failure escape instead of falling back did *not* fail any assertion: the picker still rendered and the line still sent, so the behaviour under test genuinely survived. It failed `npm test` anyway, on the unhandled rejection, with exit code 1. Worth recording that the `catch` is there to avoid an unhandled rejection rather than to keep the screen alive.

  Evidence: `dotnet build` 0/0, `dotnet test` **736/736**, `verify-e2e.ps1` PASS, frontend `npm audit` clean, `npm run typecheck`, `npm test` **346/346** (was 340), `npm run build`.

- **2026-09-11 — The enquiry list stops hiding the customers who have waited longest.** The single worst correctness defect the dealer-day walk found, and it was one line of ordering.

  **The panel headed "Nobody is chasing these" took the fifty NEWEST enquiries and displayed them longest-waiting first.** So the list whose entire purpose is to surface neglect was populated by recency and dropped exactly the rows it existed for. Measured on 2026-09-10 with 52 open enquiries, the two longest-waiting customers — at 95 and 93 days — were never returned at all, and taking one new enquiry pushed the 95-day customer off the screen. Sorting the page after it arrives cannot fix a page that contains the wrong rows: **the order has to be part of the query**, and it is now `LeadOrder`.

  **The list returns a page rather than a bare array.** `total` is the point: "Showing the first 50. There may be more" was true and useless, and a dealership needs to know whether it is 51 or 5,100. Walked against the seeded dealership: the chase list now leads with the 96-day customers, the footer reads **"Showing 1–50 of 108"**, and rows 51–100 are reachable for the first time.

  **A walk-in who is not on file can be recorded without leaving the enquiry.** "Walk-in" is one of the five sources the form itself offers, and until now the only way to take one was to abandon the enquiry, create the customer on another screen, navigate back and start again. The customer search also has a button, instead of only answering to Enter — typing a name and tabbing onward left the previous results showing, so the person concluded the customer did not exist.

  **An unknown ordering is refused rather than guessed.** The whole reason the parameter exists is that the wrong order silently returned the wrong rows; a typo quietly falling back to the default would reproduce exactly that.

  **`verify-e2e.ps1` caught the contract change** — it parsed the list as an array and reported "manager sees 0 enquiry(ies)" before failing. Fixed in the same commit, which is what keeps the script honest.

  Two rehearsals: always ordering newest-first failed the two ordering tests, and dropping `order=longestWaiting` from the screen's query failed the test that asserts it is asked for.

  Evidence: `dotnet build` 0/0, `dotnet test` **740/740** (was 736), `verify-e2e.ps1` PASS, frontend `npm audit` clean, `npm run typecheck`, `npm test` **353/353** (was 346), `npm run build`.

- **2026-09-11 — The application wears the real brand.** Colours, wordmark and tagline from the sheet supplied that day; the mark itself is still waiting on its file. Recorded in [BRAND.md](../BRAND.md).

  The navy `#1b3e6f` and red `#d0202e` were a placeholder palette chosen before the brand existed. They are now the sheet's deep petrol teal and gold, the wordmark reads **Dealer** in teal and **FOSS** in gold, and **AUTOMOTIVE SOLUTIONS** sits beneath it on the sign-in page — hidden in the application bar, where the bar is already two rows and a third line of identity would push the navigation off a narrow screen.

  **The gold is not the artwork's own value, and that is deliberate.** `#be9231` measures **2.86:1 on white** — under 4.5:1 for text and under even the 3:1 large-text bar, so the half of the name that says FOSS would be the half nobody could read. `#916f1e` is the same hue carried down until it measures 4.67:1. The artwork value is kept as `--brand-gold-artwork` for a graphic on a dark ground, where it measures 6.04:1. The teal needed no change in the light theme: 8.26:1 as supplied.

  **The mark is still a placeholder** — a geometric stand-in now wearing the brand teal so it stops fighting the wordmark. It is artwork and cannot be reproduced from a picture of itself; hand-tracing it was attempted twice and rejected twice. BRAND.md says where to save the SVG.

  > **Superseded 2026-09-12.** Both claims in that paragraph are now wrong, and the gold values in the one above it are too. The mark was traced off the supplied artwork rather than drawn, and the palette was re-measured from the artwork's own pixels. See the two entries below.

  **The printed paperwork is deliberately left unbranded.** The order, the invoice and the job sheet carry the dealership's name, not ours: a DMS prints the dealership's documents, and putting the vendor's colours on a customer's invoice would be branding somebody else's paperwork.

  Evidence: `dotnet test` 740/740, `verify-e2e.ps1` PASS, frontend `npm test` 353/353, `npm run build`. The accessible name of the lockup is still exactly "DealerFOSS" — checked, because the accname algorithm inserting a space between adjacent spans has broken it once before.

- **2026-09-12 — The mark is the designer's, traced rather than drawn, and the palette is measured rather than guessed.** Two commits, `11f3456` and `6f37b70`.

  **Two hand-drawn attempts were rejected and both deserved to be.** Working by eye off a small picture produced a wing of three separate feathers and an F whose crossbar pointed right; the real mark has one swept wing with a split through it and a crossbar pointing left. Neither is recoverable from memory of a thumbnail. The third attempt stopped drawing and read the pixels: the sheet at its own resolution, each pixel turned into a **coverage figure** rather than a verdict — how much ink, from saturation, split between the two colours by hue — and the half-coverage contour taken with marching squares, which interpolates along cell edges and therefore lands between pixels. Thresholding first was tried and is the mistake to avoid on a retrace: it rounds every edge to a whole pixel and discards the anti-aliasing, which is precisely the information that says where the edge is.

  **The colours were set by eye in the first pass and both were too light.** They are now sampled from the **core** of each shape, eroded two pixels so bevels, glows and anti-aliased edges cannot drag the answer toward the background. Teal came back `#054b60`, not `#14556b`; gold `#b38524`, not `#be9231`. The dark pair keeps the hue and gives up saturation — `#3fa5c4` is 194° exactly, the artwork's own hue, eased from 0.95 saturated to 0.68 as it lifts, because lifting without easing gave an electric cyan that read as a different brand. The gold is lifted to *match* rather than to what it needs, since two halves of one word lifted by different amounts look like two logos.

  **The geometry has one home** — `frontend/src/app/markPaths.ts` — and the five drawings of it are kept in step by a test rather than by memory. `Mark.test.tsx` fails by name on any file that has fallen behind, and it reads the palette out of `app.css` instead of carrying its own copy, because a test holding a copy of the thing it checks cannot fail when that thing is wrong.

  **The rasters are drawn from the same path data, not screenshotted**, so a PNG cannot disagree with the vector it is an export of. Mark, lockup, wordmark, badge and banner as SVG; mark, badge, lockup, banner and a 1200×630 social card as PNG and JPEG, under `docs/assets`.

  Evidence: `dotnet build` 0/0, `dotnet test` **740/740**, `verify-e2e.ps1` PASS, `npm audit` clean, `npm run typecheck`, `npm test` **378/378**, `npm run build` with every icon shipped to `dist`. Checked in a real browser, light and dark.

  **Still needs a person:** the social preview is a *setting*, not a file. `docs/assets/dealerfoss-social-1200x630.png` is committed; somebody has to upload it under Settings → General → Social preview once.

- **2026-09-12 — Every list can be paged, and says how many there really are.** `9756133`.

  **Ten lists took a limit, clamped it, and returned the first N rows with no offset at all.** The screens said "showing the first 50, there may be more" — honest, and a dead end: a dealership with 215 cars in stock could not reach car 51 by any route the application offered. Stock now reads **"Showing 1–50 of 215"** and page two is a click away.

  **One `Page<T>` in Core, not eleven records.** Rows, total, offset and limit, behind every list endpoint and in front of every list screen as one `Pager`. Eleven capabilities each carried their own copy of the clamp and two of the copies disagreed about the cap; `Paging.Limit` and `Paging.Offset` are the only clamp now, and they are unit-tested once instead of never. The rules are ADR-025.

  **Two latent defects came out with it, and both are worse than the missing feature.** Three lists had **no total order** — skipping an unordered set is undefined, so the database may hand back row 51 twice and row 52 never; receivables, repair orders, journal entries, customers and vehicles now carry an id tiebreaker, because several invoices raised in the same second is ordinary. And **receivables filtered "still owed" in memory after the take**, which cannot be paged at all: page two started in the wrong place. Outstanding is still derived from the payments rather than stored — nobody may write a balance — but it is a correlated sum the database applies, so the filter, the count and the skip finally agree.

  **Parts is the one list whose page unit is not the row.** A part held at three rooftops becomes three summaries, so a page of 100 parts can return more than 100 rows and `total` counts parts. Splitting a part across pages because one of its rooftops fell over the boundary would be worse than a page that runs long. Commented where it happens.

  **Left unpaged deliberately:** finance products, rooftops, staff, accounts and the service diary. They are bounded by what a dealership *is* — a diary is a date range, not a list that grows — and a pager there is a control that never does anything.

  **`verify-e2e.ps1` caught one itself.** The practice-import check counted the response object rather than its rows and reported a car the dry run had not created — exactly the false pass that check exists to prevent.

  Evidence: `dotnet build` 0/0, `dotnet test` **755/755** (was 740), `verify-e2e.ps1` PASS, `npm audit` clean, `npm run typecheck`, `npm test` **379/379**, `npm run build`. Driven in a real browser: stock 1–50 of 215, then 51–100 with different cars; customers 1–100 of 486.

- **2026-09-14 — The walk record is four days behind the code, and one of its findings was never true.** No code changed; the documents that the progress page reads from did.

  Three of the seven closed findings had never been written down as closed, so `DEALER-DAY.md` still told a reader to fix things that were fixed. Worse, **finding 22 was wrong on the day it was written**: it said `/customers` had no search button and no as-you-type search, and that screen has searched as you type since `59bcc54` on **2026-08-09** — debounced, with the in-flight request aborted on each keystroke so a slow answer for "f" cannot land on top of the right answer for "focus". `/parts` too.

  **A wrong finding does not sit still.** The register row in doc 11 §12 was written from it, the progress page derived a candidate task from that row, and the task was "add a search button to a screen that has had search-as-you-type for a month". The finding is retracted for `/customers`, stands for the enquiry screen, and the rule taken from it is written into the walk record: a finding about a screen must name the file and the commit that made it true, the way the code-level findings do — every finding here citing a file and line was correct.

- **2026-09-14 — The header rule is enforced instead of remembered.** The register said this was open work "waiting to be scheduled". It had been finished on 2026-09-04.

  **The row was stale for ten days because nothing could answer the question.** The rule — Copyright, SPDX, `Overview`, `Usage`, `Coding Instructions` on every hand-written source file — was applied to 347 files by hand and then had nothing watching it. Finding out whether it was still true meant opening every file, so nobody did, so the row sat on the backlog describing work that did not exist. That is the same failure as the retracted walk finding the day before: a fact nobody can cheaply re-check becomes a wrong backlog.

  Checked properly: **378 of 379 correct**, and `deploy/otel-collector.yaml` had slipped through — added since the conversion, with the two-line comment it was born with. It now carries the header, including the two things a reader of a committed collector config needs told: that it exports to the console *and nowhere else* on purpose, because a default that forwarded local traces off the machine would take tenant names, user ids and request paths with it; and that both OTLP receiver protocols stay on, because a developer whose traces vanish into a port mismatch cannot tell that from "tracing is not wired up".

  **`SourceHeaderTests` closes it permanently.** It walks the working tree on every `dotnet test`, reports every offender and every missing part in one failure, and asserts the *file count* as well — a walk that loses the repository root finds nothing, and "none of the zero files I found is wrong" passes forever while guarding nothing. The copyright year is matched as a pattern, so the suite does not turn red on 1 January.

  Rehearsed rather than assumed: a headerless `.ts` file dropped into `frontend/src/shared` failed the test by name, and removing it passed. It also caught two things on its first run that were not defects — the frontend build copied into `src/App/wwwroot`, and a *second checkout of this repository* sitting in `.claude/worktrees` where a background agent is working. Both are pruned as directories rather than excused as exclusions, and doc 08 §5 says which is which.

  Evidence: `dotnet build` 0/0, `dotnet test` **756/756** (was 755), `verify-e2e.ps1` PASS. No frontend change.

- **2026-09-14 — A backlog row now says when it was last checked, and the dashboard says when that stopped being recent.** The third correction in a row, and the one that should stop there being a fourth.

  Two register rows went stale in September and **both went stale silently, in different ways**. *Paging on every list* was finished on the 12th and the row was not updated, so the dashboard kept offering finished work as the next thing to build. *Four-part file headers* read "decided, waiting to be scheduled", carried no date at all, and described work applied ten days earlier. Each cost a session to find, and both were found by reading the code — the expensive way.

  **Neither needed new data to catch.** The rows already carried the date they were last checked, written into their own prose; nothing was reading it. The dashboard now extracts the most recent date from each candidate's state, counts the commits landed since, and marks the row amber — an undated row loudest of all, because that is the one that sat longest. It says at the top how many of the candidates cannot currently be trusted: **6 of 11** on the day it was written.

  It found a third row immediately: *Documentation restructure* had `Blocker: Build` while its own state said `Done`, so a finished item had been sitting in the candidate list. Two more carried no date and were re-read against the code rather than dated on faith — neither has been started, and now they say so with a date.

  **The convention is written where the rows are**, in doc 11 §12, not only in the script: a row is a claim about the code, and a claim nobody has re-checked since the last commit is a guess. Re-read before picking, and write the date even when nothing changed.

- **2026-09-14 — Somebody can pay more than the bill, and the difference stops being the dealership's problem to refuse.** `2200 Customer credits`.

  **Refusing was honest and useless.** Taking more than was owed had been refused since the sub-ledger was built, for a good reason at the time: absorbing the extra with nowhere to put it would show the customer settled and quietly keep their money. But a customer paying a $110 invoice with $150 in cash has not made a mistake, and there is no version of "we cannot accept that" a service counter can say out loud.

  **The bill takes what it can hold and the rest becomes a liability.** One journal entry, because the customer performed one act: the whole amount arrives in 1000, the bill's share clears 1100, and the remainder credits 2200. `Outstanding` still cannot go negative — the entity keeps refusing more than is owed, and the service splits the money before it gets there, so a future caller that forgets to split gets an exception rather than a receivable owing a negative amount.

  **2200 is never netted against 1100.** Netting would let one customer's credit hide another customer's debt and make the figure the dealership chases quietly too small. What is owed to us and what we owe back are two facts on opposite sides of the balance sheet.

  **A credit is discharged two ways, and only one of them moves money.** Put against another bill the same customer owes — 2200 down, 1100 down, no cash, because the money arrived when they overpaid — or handed back, 2200 down and 1000 down. Each overpayment is its own row drawn down by uses rather than one running balance per customer, so "where did this $40 come from" has an answer.

  **One customer's credit cannot pay another customer's bill**, and that is the check that matters most here. Without it a credit is a way to move money between people who never agreed to it, and the ledger balances perfectly the entire time.

  **Refunding needs its own permission.** `Accounting.Refund`, not `Accounting.Post` — which a salesperson holds because delivering a car posts the sale. Everything else in this system records money the business earned or now owes; this one takes cash out for a customer, and a refund is the classic way a retail business is quietly stolen from: raise a credit, pay it to yourself, and the books balance throughout. Seeded on the manager only, and a test proves the salesperson is refused. Applying a credit is deliberately *not* gated this way, because no money leaves.

  **A settled bill still refuses a payment**, and that is not the same case. A payment against a bill with nothing left on it is almost always the same payment keyed twice, where no second money arrived — absorbing it would invent both the cash and the liability.

  **Walked, and the walk found the defect the tests could not.** RO-1082, a $110 invoice paid with $150. The band warned *before* the button — "That is $40.00 more than is owed" — then settled the bill and showed $40 owed back. But it read **"overpaid on 9c9d1557-7e22-46e7-a5b8-591a19f6e6dd"**: a workshop receivable's reference is the job's *id*, not its number, and nothing had ever put that string in front of a person before. It shows the date instead now; the reference stays in the journal memo, where it is a ledger key rather than something to read. Then refunded: 2200 went 40 in, 40 out, net zero, and the trial balance agreed.

  Evidence: `dotnet build` 0/0, `dotnet test` **761/761** (was 756), `verify-e2e.ps1` PASS, `npm audit` clean at high, `npm run typecheck`, `npm test` **382/382** (was 379), `npm run build`.

- **2026-09-15 — The wrong car can no longer be booked in, because the right one can now be found.** Findings 16 and 18.

  **The defect was the labels, not the length of the list.** Every picker in the application was a bare `<select>` filled from `?limit=200`. Against ~500 customers that is not a long dropdown — it is a screen where three customers in five cannot be chosen at all and nothing says so. Against cars it was worse: options read "2021 Toyota RAV4 XLE" with no VIN and no stock number, and **32 of the 101 offered had a label identical to another one**, so the wrong car could be booked and no screen anywhere would show it had been. Paging did not fix this and could not: a dropdown has no page two.

  **One `RecordPicker`**, on the booking screen, the enquiry form and the deal desk. A search box that queries the server, the results, and — once chosen — the choice with a way to change it. Deliberately plain HTML with real buttons rather than an invented combobox, which is a keyboard trap waiting to be written. Every option carries a **hint**: a VIN tail, a stock number, an email. That is the whole reason the component exists, and a caller passing only a label has rebuilt the defect.

  **The car list narrows to the customer, and how it does is the interesting part.** A vehicle has **no owner** in this system, and that is not an oversight to route around: cars change hands, ownership is a history with dates rather than a column, and inventing a `CustomerId` on `Vehicle` would be wrong the first time somebody sold their car privately. So "their cars" is answered from the workshop's own records — every vehicle on one of their repair orders or bookings, most recent first, nothing stored. It is incomplete on purpose (a car bought and never serviced is not there, because deals are another capability), which is exactly why the shortlist sits *beside* a search of every car rather than replacing one. Rehearsed: removing the customer filter fails all three of its tests.

  **The enquiry form had been offering sold cars since it was written**, under a comment saying it did not. `stillGettable` on the inventory query — everything except Sold and Removed — and deliberately not `status=Available`, which would have been the easy fix and would have dropped the two states the comment was protecting: a car in reconditioning, and one on hold for somebody else. Measured: 215 units, 162 still gettable.

  **The option id differs by screen, on purpose.** An enquiry records interest in a *vehicle* and must survive that unit being sold to somebody else; a deal is struck on one *unit* on one lot. Both are commented where they happen.

  **Parts is deliberately untouched.** That picker was built on 2026-09-11 with its own shape — quantity in stock beside each part, free text named as a choice for a one-off item — and converting it would trade a working design for consistency.

  Walked: "Alvarez" returns eleven customers including **two called Janusz Alvarez**, separated by their email; "RAV4" returns **two 2023 Toyota RAV4 XLEs**, separated by their VIN tails; choosing Amara Alvarez narrowed the cars to the single BMW she has been here with; and the booking landed against the right car.

  Evidence: `dotnet build` 0/0, `dotnet test` **764/764** (was 761), `verify-e2e.ps1` PASS, `npm audit` clean at high, `npm run typecheck`, `npm test` **390/390** (was 382), `npm run build`.

- **2026-09-15 — A job number says which lot it belongs to, and half the finding that asked for it was wrong.** Finding 17.

  **The data was always right.** `RepairOrderService` numbers per rooftop by design and a unique index on `(RooftopId, Number)` enforces it, so this dealership's 162 jobs share 82 numbers with 80 of them used twice. That is correct behaviour. The screen was not: it is headed "the work at the locations you cover", lists both lots together, and printed the bare number. Asking it for RO-1082 while walking the credit work returned **two buttons both labelled RO-1082** — different customers, different cars, both invoiced — and the only way to tell them apart was to read the DOM.

  **The printed job card was never ambiguous, and the finding said it was.** `DocumentHtml.Header` has put the dealership and "North Auto Downtown (NAG-01)" at the top right of every document since documents existed. Found by reading `DocumentService.WhereAsync` rather than by re-reading the walk record — the second finding in two days that was wrong about something nobody had re-checked.

  **The lot shows beside the number when the list actually mixes lots, and always on an open job.** Adaptive rather than always-on, because the question is *"is what I am looking at ambiguous"* rather than *"how many lots does this person cover"*: at a one-site dealership every row would carry the same code and say nothing, and a manager filtered to a single lot is not looking at anything ambiguous either. An open job is different — it carries no context to infer the lot from, and it is the heading somebody reads back down a phone.

  **A failed rooftop lookup does not fail the job list.** Every role that can read a job holds `Organization.Read` today, but that is a fact about the seeded roles rather than a rule. A list that went blank because somebody's role was narrowed would be far worse than a number without its lot — which is exactly what the screen showed before today.

  **The stored numbers are untouched.** Putting a rooftop prefix into the number itself would rewrite what is printed on job cards customers are already holding. That is a decision about somebody else's paperwork, not a detail to settle in passing.

  Evidence: `dotnet build` 0/0, `dotnet test` **765/765** (was 764), `verify-e2e.ps1` PASS, `npm audit` clean at high, `npm run typecheck`, `npm test` **392/392** (was 390), `npm run build`.

- **2026-09-16 — A job line can be a catalogued job at a known rate, instead of free text at a typed price.** Op codes and labour rates, the first thing built from the Dominion VUE discovery read.

  **Two advisors writing up the same job produced two different descriptions at two different prices**, and nothing could answer "what do we charge for front brakes" or "how long should that take". `EffectiveLabourRate` in the labour report was, and still is, an *output* — what an hour actually realised. It was never a setting, and there was nothing for it to be measured against.

  **The split mirrors parts, deliberately.** An op code is **organization-wide**, for the same reason a part number is: "front brakes, 1.4 hours" means the same job at every lot, and one store inventing its own version is how a catalogue stops being comparable. A labour **rate is per rooftop**, because what an hour sells for is a local decision — the seeded group charges $120 at NAG-01 and $135 at NAG-02, and the seed differs on purpose so a bug that ignored the rooftop would be visible.

  **A rate is the default for a pay type, not a free-floating number.** Warranty is reimbursed at what the manufacturer allows, internal work is carried near cost, retail is retail. Three prices for one hour, and which applies is decided by who is paying — which the line already knew.

  **The catalogue fills blanks and never overrules a person.** Anything typed wins. An advisor billing 2.5 hours against a 1.4-hour job has found a seized bolt, and a system that wrote the standard time back over them would be lying about the work and short-paying whoever did it.

  **Nothing is deleted, only withdrawn.** A withdrawn code is refused on a new line and left alone on every line that already cites it — a job written in March must not change because somebody tidied the catalogue in September. The line stores the op code as *provenance*; the description, hours and rate were copied when it was written and do not move.

  **`Service.Configure`, held organization-wide, and deliberately not `Service.Write`.** A technician writes work up all day. Setting the price of every future hour, and the standard time the whole group is then measured against, is a management act. A test proves the advisor is refused both, and can still *read* the catalogue — a picker they cannot load is worse than no picker.

  **A setup screen, so the catalogue is not seed-only.** `/workshop/setup`: rates as a grid of lots against pay types, and the jobs with withdraw and put-back. A lot that has not set a rate reads "Not set" rather than $0.00 — those are different facts and an advisor would believe the second.

  **A test caught a real bug in the screen.** The write-up button stayed disabled unless a description was typed, which defeated the whole point: the catalogue supplies the description. It now accepts either.

  Walked: the rates grid shows both lots and all three payers; searching "brake" offers *Front brake pads and discs · BRK-FRT · 1.4h* and *Rear brake pads · BRK-REAR · 1h*; choosing the first and pressing Write it up with nothing typed produced **"Front brake pads and discs (1.4 h at $120.00) — $168.00"**; switching to Warranty and choosing the recall produced **"(0.5 h at $95.00) — $47.50"**, the manufacturer's rate rather than retail.

  Evidence: `dotnet build` 0/0, `dotnet test` **771/771** (was 765), `verify-e2e.ps1` PASS, `npm audit` clean at high, `npm run typecheck`, `npm test` **394/394** (was 392), `npm run build`.

- **2026-09-16 — A technician goes on the clock, and productivity stops being a figure this system cannot produce.** The second half of the labour report, and the second thing built from the Dominion VUE read.

  **It could not have been built yesterday.** Productivity is hours billed over hours clocked, and until op codes landed this morning there was nothing to bill *against* — every labour line carried whatever hours somebody typed. Clocking without a standard would have measured a technician against a number the advisor invented, which is worse than not measuring them.

  **`notMeasured` is down to one.** The labour report has named the figures it cannot produce since it was written: *Efficiency — needs a roster* and *Productivity — needs a time clock*. There is a clock now, so Productivity came off the list and onto the screen. **Efficiency stays**, because it is hours produced over hours *available* and nothing here knows who was rostered on — that is attendance, which is payroll, and payroll is a module this system does not have.

  **Clocked hours are counted over the same JOBS as the hours sold, not the same dates.** A job clocked in March and invoiced in April belongs to April with all of its time. Counting clockings by the date they stopped would mix two windows and produce a ratio that looks precise and is not.

  **Clocking on elsewhere closes the open one, rather than being refused.** A technician cannot be on two jobs at once, but a shop where the system refuses the second clock-on is a shop where people stop clocking: they move between jobs all morning. The closed entry records *why* — "Switched to RO-1077 from RO-1078". A partial unique index on `(TechnicianUserId) WHERE StoppedAt IS NULL` makes the invariant true in the database rather than only in the service.

  **Several technicians on one job is ordinary** — a gearbox out is two people — so the one-open rule is per technician, never per job. Their hours are credited to whoever *clocked* them, not to whoever is named on the order, or one of two people on the same gearbox would look twice as slow as they are.

  **An open clocking contributes zero until it stops.** A figure that changed every time somebody looked at it could not be reconciled against anything. And where nothing was clocked the report says **"Not clocked"** rather than 0% — a workshop that has not started using the clock has not been unproductive, and a zero would be a damning number against a technician for a reason that has nothing to do with them.

  **`IAppendOnly` was tried on the entity and removed.** That marker forbids the context from ever updating a row, and *closing* a clocking is an update — the one operation the type exists to perform. Every clock-on failed with "TechnicianClocking is append-only" until it came off. The property actually wanted is narrower: a closed entry is never reopened, which `Stop()` enforces by refusing a second stop.

  Walked: clocked the technician onto RO-1078, then onto RO-1077. RO-1078's entry closed itself at **0.01 hours** with the reason *"Switched to RO-1077 from RO-1078"*, and exactly one clocking was left open.

  Evidence: `dotnet build` 0/0, `dotnet test` **775/775** (was 771), `verify-e2e.ps1` PASS, `npm audit` clean at high, `npm run typecheck`, `npm test` **397/397** (was 394), `npm run build`.

- **2026-09-16 — The planning indicator says where the remaining points are, and the answer is not where the work has been going.** No feature landed in this entry. The maintainer asked why the headline had not moved after two milestones, and the honest answer took a re-review of all seven path-bearing stages at once — the first time all of them had been checked on the same day.

  **Five milestones in a week were worth one point.** A job number that names its lot, a searchable record picker, credit balances on overpayment, catalogued op codes at posted labour rates, and a technician clock: every one of them landed in stage 5, which had 0.15 of itself left. The stage moved 0.85 → 0.9 and the headline 76% → 77%. Six of the seven shares did not move at all, because the work, good as it was, did not touch any of their named remainders.

  **The arithmetic nobody had written down.** Each built stage is worth an equal share of the headline — 12.5 points. So the headroom, not the activity, decides what the number does. Stage 7 holds 0.5 (6.2 points) and stage 2 holds 0.25 (3.1 points), and **neither waits on anybody outside the project**. Stage 5 holds 0.1 (1.2 points) and *cannot be finished from this machine*: what is left of it is finance applications, which need a lender, and warranty claim submission, which needs a manufacturer agreement. Stage 8 is a whole stage and needs two real dealerships. **About 86% is the ceiling reachable by building alone.**

  **Stage 2 is the one to take first**, because its remainder is also four of the five unmet exit criteria — deletes are not modelled at all, a quarantined record cannot be replayed (`QuarantinedRecord.cs` says so in its own comment), export carries no relationship manifest and no documents, and `CertificationStatus` reaches no reader because there is no connectors screen. Finishing it moves the judged indicator *and* the counted one, 36 of 41 to 40 of 41. The recommendation on the progress page was changed from "A URL for every record" to this on the same grounds: the URL work is real and still wanted, but it sits in no stage's remainder and would have moved neither number.

  **The page could not answer the question, so the page was changed.** It showed how much of each stage exists and never how much is left, or who the rest waits on — which is how a month of work went into the stage with the least room and the hardest ceiling without anything saying so. A headroom table now sits above the stage list: points remaining per stage, and who each one waits on. The staleness mechanism worked exactly as designed and was still not enough on its own; it said "seven stages need re-reading" for days without saying which of them was worth re-reading *first*.

  Evidence: `dotnet build` 0/0, `dotnet test` **775/775**, `verify-e2e.ps1` PASS, `npm audit` clean at high, `npm run typecheck`, `npm test` **397/397**, `npm run build`. No source file changed; the change is to `docs/PROGRESS.md`, this file, and the generator behind `local/progress.html`.

- **2026-09-17 — Every record has an address, so one can be sent to somebody instead of described.** `/customers/:id`, `/leads/:id`, `/deals/:id`, `/inventory/:id` and `/workshop/:id`. A link opens the record. The back button closes it. Two tabs can hold two cars. None of that was possible the day before: every record lived in component state, and the only way to say which one you meant was to describe where to click.

  **This contradicted a recorded decision, and the decision was narrowed rather than ignored.** ADR-020 says *"a detail is a band, not a route… routes are for areas and never for records within an area."* Read whole, every argument in that section is about **replacing the list with a second screen** — the operator losing their place, their filter and their scroll. None of it is about the address bar. The amendment dated 2026-09-17 in that ADR narrows the rule to what it was protecting: **a detail is still a band, and the band may have an address.**

  **`path="/inventory/:id?"` — an OPTIONAL segment on the SAME route — is the load-bearing detail.** Two routes, one for the list and one for the record, are two different matches, so React Router unmounts and remounts the page on every open and every close and the filter goes with it. That is the exact failure ADR-020 forbade, and it is still forbidden. One route with an optional segment keeps the page mounted, and `useRecordRoute.test` proves it **by counting mounts** rather than by reading the screen — a remount is invisible in rendered output.

  **A filter travels with the record; an instruction does not.** The stock list's `?stock=` is a real filter, so it goes with the car and comes back with it. The deal desk's `?leadId=` is a one-shot handoff from a won enquiry, and a link carrying it would **start a second deal on the same enquiry** for whoever opened it. `carryQuery` is off by default and opted into, because a URL that does less when pasted is the safer one.

  **One sentence for every reason a record will not open.** Deleted, never existed, and belongs to a rooftop you may not see all read identically, from one place — `RecordBand.tsx` — so five screens cannot drift into five answers. The server already refuses to tell those apart for scoped records (`InventoryService`, `LeadService`, `DealService`, `RepairOrderService` each carry the comment), and a screen saying "that job belongs to another branch" would hand back the fact the server withheld, one guessed id at a time. Customers answer 404 for a genuinely missing record because a customer is organization-wide and there is nothing to leak; the screen still says the same sentence, so a later server change cannot quietly become a probe.

  **Signing in keeps the address.** The signed-out router redirected to `/sign-in` and threw the requested address away, so a shared link opened by somebody signed out landed them on the dashboard — and somebody signed out, or whose session lapsed overnight, is the commonest reader of a shared link. Sign-in now renders where they asked to be, which needs no state carried at all: once the session exists the routing table is replaced and the location is still `/workshop/ro1`.

  **The tests had been mounting a tree the application never builds.** Five screens were rendered bare inside a `MemoryRouter` with no `Routes` — fine while the record was component state, useless the moment the page read `useParams`. Five tests failed for a reason that had nothing to do with the screens. `renderAtRecordRoute` in `test/render.tsx` mounts them the way `App.tsx` does, and returns an `address()` so a test can read what the address bar would say, which a `MemoryRouter` otherwise hides.

  Also closed: ADR-020's item 6, whose outstanding list was a signal band for untouched enquiries and detail bands on stock and customers. All three exist.

  Evidence: `dotnet build` 0/0, `dotnet test` **775/775**, `verify-e2e.ps1` PASS, `npm audit` clean at high, `npm run typecheck`, `npm test` **431/431** (was 397 — 11 for the hook, 23 across the five screens and the router), `npm run build`.

  Walked in a browser, all five screens. Clicking RO-1074 put `/workshop/1d58bcd8-…` in the address and the band read *"RO-1074 NAG-01 · Mateo Petrov"*; the back button closed the record and left the list and its 54 rows exactly where they were; a cold load of that same URL opened the job with the list beneath it. `/inventory?stock=A1001` carried its filter into `/inventory/907ea1f6-…?stock=A1001`. A made-up id answered *"That record cannot be opened…"* with the list still underneath and a way back. And the whole point, end to end: signed out, opened the job's link, got the sign-in form **at the job's own address**, signed in, and the job opened.

- **2026-09-18 — The navigation reflects the job, and hiding a link did not become a lock.** `/auth/me` now returns `permissions`: everything the caller holds somewhere, sorted, empty while they owe a second factor. The bar filters on it, a group whose every child is hidden hides itself, and Records appears for anybody who can import *or* export.

  **Why it did not exist already.** Not an oversight. This project holds one copy of an authorization rule and keeps it on the server; shipping a permission table to the browser looks exactly like starting a second copy, which is the mistake the deal desk and the workshop each nearly made with their transition tables. Per *record* the pattern was already solved — the server sends `availableMoves` and the screen offers precisely those. There was no equivalent for a *module*, so the bar assumed everybody was a general manager and a technician learned the shape of their own job by collecting refusals.

  **What makes it a hint rather than a control**, which is the whole of [ADR-025](../adr/0025-the-session-carries-permissions-as-a-hint.md): the list is deliberately **lossy** — it carries no scope, so a person holding accounting at one rooftop of four looks identical to one holding it everywhere, and a screen *cannot* misuse it to filter data because the information is not there. Nothing on the server branches on it. Every endpoint enforces exactly as before. And `SessionPermissionsTests.Permissions_are_a_hint_not_a_control` signs in as a technician, asserts their list lacks `Accounting.Read`, then calls the accounting endpoint and **requires a 403** — so the day somebody reasons "the browser already filters this", the suite goes red.

  **Never hidden:** the dashboard, because it is the landing screen and already withholds figures band by band rather than refusing wholesale — hiding it would leave somebody signed in with nowhere to be; and two-step sign-in and passkeys, because a person's own credentials are not the dealership's business.

  **The Identity surface changed**, and CLAUDE.md requires that be said out loud rather than done quietly. `IAccessDirectory` gained `GetHeldPermissionsAsync`. It is a method on an already-public interface rather than a newly public type, and it decides nothing — but it is the first thing on that contract that hands out information instead of a verdict, and it carries the longest remarks there for exactly that reason. One query, not thirty-three: the obvious alternative is calling `GetAuthorizedScopeAsync` once per permission on every page load.

  **What the tests caught.** Three existing tests failed the moment the nav started believing the session — because seven fixtures said `{ userId, mustEnrolSecondFactor }` and the browser now reads that as "this person holds nothing", so the bar came up empty. Correct behaviour, wrong fixtures. `test/session.ts` gives one `signedInAs()` whose default is everything, so the next fixture cannot make the same mistake and a test about the deal desk does not have to know which permission the deal desk needs.

  Walked as two people. **Technician** (`tech@dev.local`, holds six permissions): four destinations — Sales containing only Customers, Service containing Workshop and Parts, People & security containing only their own two screens, no Accounting group and no Records link. They then typed `/accounting` anyway: the screen loaded, the server answered **403**, and it read *"You do not have access to these figures."* **Manager** (`gm@dev.local`, holds all 33): six destinations, everything open.

  Evidence: `dotnet build` 0/0, `dotnet test` **808/808** (was 801 — seven new integration tests), `verify-e2e.ps1` PASS, `npm audit` clean at high, `npm run typecheck`, `npm test` **446/446** (was 440 — six new screen tests), `npm run build`.

- **2026-09-18 — The design tokens stopped being advice.** No feature. The scales added the day before were defined and roughly a third applied: **17 font sizes, 9 radii, 21 paddings and 16 margins** still written as literals, sitting beside tokens that named the same values. That is the worst state a design system can be in — worse than none — because the next person cannot tell which of the two is authoritative and both answers are defensible. Now: **3 font sizes, 1 radius, 3 paddings, 3 margins, 0 gaps**, and every survivor is deliberate with its reason at its own site (`0`, `auto`, three responsive `clamp()`s, two `em` values that must track their parent, two `inherit`s, a list indent in rem, and 30px of clearance measured against a glyph rather than a rhythm).

  **The scale was adjusted to fit the content, not the other way round.** `--t-meta` was 0.76rem with one consumer while `0.8rem` appeared six times as the commonest small size in the file; the step moved to meet the content rather than six sites being shrunk to meet the step. Two steps were added because the scale had no name for things that plainly exist: `--t-figure` and `--t-display`, because **a displayed number is not a heading** — forcing `.tile__value` onto `--t-page` would have made a dashboard figure and a page title the same size, which is exactly the distinction the dashboard draws. `--r-lg` likewise, for the cards at 12 and 14px that were neither `--r-md` nor a pill.

  **`--control-h` did not work, and the walk is what found it.** `min-block-size` is a **floor, not a height**, and a control whose content is taller simply ignores it: one toolbar on `/accounting` held a select at 41.5px, a text input at 42.8 and a date input at 44.8 — all three carrying `min-block-size: 40px`, none of them 40. Pinning `--control-line: 1.25` makes the content shorter than the floor so the floor is what everything lands on. **A select needed more**: Chrome does not apply `line-height` to `<select>` at all, so the rule landed and the computed value stayed `normal`; selects get an explicit `block-size`, which is safe for them because they cannot wrap. Every page control on six screens now measures **exactly 40px**, from three to seven distinct heights before.

  **A pre-existing WCAG failure surfaced, and I had made it worse before finding it.** A row's open-the-record button carries `padding: 0` to keep cells dense, leaving its height as whatever the line box is: **21px on the committed stylesheet, under the 24px floor, on 24 controls** across the enquiry, deal and customer lists. The sweep of 2026-09-17 missed it. Pinning the line-height took it to 17px, which is how it was caught. Attribution was settled by measuring — the committed stylesheet was checked out, measured at 21px, and restored — rather than by reasoning about the diff. Fixed with an explicit 24px floor, keeping the tight row: 24px of target around 17px of ink is what 2.5.8 is for.

  Measured after, across ten screens in both themes: **contrast failures 85 → 0 in dark and 65 → 0 in light** (2,112 text elements), **targets under 24px 24 → 0**, sideways scroll 0 at 320px. Walked in both themes at desktop width.

  Evidence: `dotnet build` 0/0, `dotnet test` **824/824**, `verify-e2e.ps1` PASS, `npm audit` clean at high, `npm run typecheck`, `npm test` **451/451**, `npm run build`.

- **2026-09-19 — Changing page no longer means walking every record first.** Customers, Enquiries, Deals, Stock and Workshop now put one pager before the table, in the picture and in the tab order. The pager remains part of the list band: signal and context keep their place ahead of it, and detail still follows the records. ADR-020 did not need narrowing for this.

  **The old number was a finding, not a baseline.** The September 17 audit recorded Next as stop 124. Repeating the walk on the unchanged checkout, signed in as the seeded manager with menus closed and 100 customer rows, measured **116** from a fresh load, counting the skip link as stop one. After: **16**. Exactly 100 record buttons came out of the path to paging; none came out of the keyboard's reach. Enter loaded rows 101–200 of 486, and the following Tab reached Previous.

  **One pager, before the rows, rather than a special keyboard order.** A positive tab index would make focus disagree with the picture, and duplicating the pager would add controls for the same operation. The shared `ListTable` inside `ListScreen.tsx` owns the order and keeps the controls outside the table's horizontal scroll box. Only Customers and Workshop actually used `ListScreen`; the other three still had their own tables. Those now use its ready-table component, keeping their existing load states and row contents instead of implementing the fix three more times.

  **Count stops, and then keep walking.** The regression tests reach the first-page direction in one Tab within the shared list, the middle-page Next in two, and last-page Previous in one, then walk every one of the 100 record buttons. A one-page list skips both disabled directions. The customer-route test measures three stops — Search, Add, Next — so the shell's navigation can change without moving the test's target. Removing row buttons from the tab order would fail these tests rather than look like an improvement.

  Walked all five screens with real Tabs. Stock reaches Next at **16**; Enquiries at **67**, because its 50 unassigned-enquiry signal actions deliberately still precede the list; Deals with Everything selected at **24**, including eight signal actions; Workshop at **28**, including nine diary-row controls. No main-list record precedes its pager. The default deal filter fits on one page and both directions are disabled. At **320 px**, English and Arabic controls wrap within the viewport; light and dark both show the keyboard focus ring. Opening a customer by keyboard and going back preserves the list and its 100 rows.

  **The machine facts needed correcting too.** The default LocalDB E2E command failed with `Cannot create file 'C:\Users\poula\DealerFOSS_Host.mdf' because it already exists.` The catalogue is absent from `sys.databases` while the file remains. No file was removed or attached: the same verifier passed against the application's configured, already-running SQL container on port 5082. Repairing the LocalDB attachment belongs to the maintainer. The status header still called the completed token migration unfinished, and two onboarding instructions still forbade the per-file SPDX line that `SourceHeaderTests` requires; both contradictions are corrected here.

  Evidence: unchanged baseline `dotnet build` **0 warnings/0 errors**, `dotnet test` **824/824**; final `dotnet build` **0/0**, `dotnet test` **824/824**, `verify-e2e.ps1` **PASS against the SQL container**, `npm audit` clean at high (the same two moderate findings), `npm run typecheck`, `npm test` **456/456** (was 451), `npm run build`. The first final typecheck caught an unsupported `exact` option in the new Testing Library assertions; it was removed and all four frontend gates rerun. Existing React `act`/jsdom test diagnostics and Vite's large-chunk warning remain; they were present in the unchanged frontend run too.

  Left alone: customer cross-module actions, arrival motion, and the number of actions in the signal and context bands. Next engineering work remains the stage 2 remainder named in the September 16 re-review; this keyboard fix changes neither its exit criteria nor its stage share.

- **2026-09-19 — Stock shows what each car cost to acquire.** The stock list
  previously showed identity and status while cost required opening every car.
  Its scoped summary now carries the recorded amount and currency; one shared
  renderer displays that figure in the list and detail. An unrecorded cost says
  so, a recorded zero is money rather than missing, and each currency stays with
  its car. All six labels say **Acquisition cost**. No extra request per row,
  page subtotal, schema change or new authorization rule is involved.

  **The label is a limit on the claim.** Checking the source exposed an older
  overstatement: reconditioning debits 1300 but does not update a stock unit's
  cost or retain the owned unit id on the posting. Delivery still uses that
  recorded cost. The register now separately names per-car recon attribution
  and relief on sale; the earlier status entry, product progress and Inventory
  README are corrected. This display must not imply a recon-inclusive carrying
  value or market appraisal.

  **Walked against the local application:** 221 cars with costs on page one and
  page two; D0048 reads $23,500 in both list and detail, and closing keeps page
  two. Reconditioning filters to 44 cars and retains their costs. At 320 px,
  English/light and Arabic/dark both keep page `scrollX` at zero after real
  horizontal scrolling; the table itself scrolls to the cost column. Desktop
  Arabic mirrors the column and keeps the currency readable.

  **A failing gate caught a test-fixture mistake.** The first API theory received
  a positive EUR car into the shared USD ledger; six existing report tests then
  correctly refused mixed currencies with HTTP 400. Restoring the committed
  Inventory fixture made the affected Inventory/Ledger group pass **60/60**.
  The final API case uses a recorded zero in EUR to prove currency preservation
  without adding another posted currency, while the UI test covers a positive
  EUR amount. No report rule or gate was weakened.

  Evidence: `dotnet build` **0 warnings/0 errors**, `dotnet test` **828/828**
  (was 824), `verify-e2e.ps1` **PASS against the configured SQL container** on
  port 5081, `npm audit --audit-level=high` passes with the same two moderate
  findings, `npm run typecheck`, `npm test` **460/460** (was 456), and
  `npm run build` pass. Existing React `act`/jsdom diagnostics and Vite's
  large-chunk warning remain. The first sandboxed build could not read the
  existing NuGet configuration; the authorized rerun passed without changing
  configuration. No phase exit criterion or hand-set stage share changed.

- **2026-09-19 — A customer can carry an address, and a deal carries its own
  registration address.** Two separate gaps, closed together because the second
  explains why the first stayed narrow.

  **The customer side was a database column with no door to it.** `Customer.Address`,
  `Address.Create` and the `customers.Customers` columns already existed —
  populated by import, read by the detail screen, and otherwise untouched since
  ADR-023/024 landed on 2026-09-05. Nobody could type one in or fix a typo.
  `PUT /customers/{id}/address` (`Customers.Manage`, the same permission as the
  credit limit) fills that gap; the screen's read-only paragraph became a small
  editor — seven fields, Save or Remove address, seeded from whatever is on file.

  **The deal side is the field ADR-024 named and explicitly left undone: "A
  garaging or registration address becomes a real field... Not built."** Reading
  `Address.cs`'s own header confirmed why it could not simply be added to the
  customer: "the registration or garaging address that drives tax is a fact
  about a DEAL, frozen with it (ADR-024 R3), so it belongs there rather than as
  a second customer field nothing would populate." `Deal.RegistrationAddress`
  is a new, Deals-owned value type — not a reference to `Customers.Address`,
  which would have coupled a frozen historical fact to a row somebody can edit
  next week, and which the feature-boundary rule (Deals may not depend on
  another capability's entities) exists to catch structurally rather than by
  convention. It freezes the moment the deal leaves Draft, the same rule
  `SetTerms` and `SetTax` already enforce.

  **This is not the same field as `TaxedAt`, and the two now sit side by side
  on purpose.** `TaxedAt`/`TaxAddress` is the narrower four-field snapshot
  (state, county, postcode, country) copied onto the tax lines once a figure
  has actually been worked out — evidence, per R3, of what a rate was defended
  by. `RegistrationAddress` is the full postal shape, entered before any tax
  exists, because it is also what ends up on registration paperwork. Nothing
  wires one to the other yet: a person still enters both separately, and
  resolving tax automatically from the registration address is future work,
  not this milestone.

  **The customer's own address and the deal's registration address are
  deliberately never linked.** The registration form is not pre-filled from
  the customer record — a company car registered at the business rather than
  the buyer's home, or a gift registered at the recipient's address, are
  ordinary cases where the two addresses differ, and seeding one from the other
  would make them look connected when the whole point of keeping them apart is
  that they are not.

  Walked in a browser, signed in as the seeded manager. Opening Marisol
  Alvarez's customer record showed her existing seeded address
  ("18 Kestrel Way, Springfield, IL, 62704, US") with an **Edit** button;
  adding a county and saving produced
  "18 Kestrel Way, Springfield, IL, Sangamon, 62704, US" in place, no reload.
  Opening a Draft deal (D0108, Marisol Halvorsen) showed **Registration
  address** as its own band directly under the tax editor, with the lede
  explaining it is not necessarily the customer's own address; filling it in
  and saving showed a **Clear the address** button appear, confirming the save
  took. A Submitted deal with no registration address on file showed nothing
  in its place — no empty heading, no error — between "Sold with the car" and
  the approval buttons.

  Evidence: `dotnet build` **0 warnings/0 errors**, `dotnet test` **836/836**
  (was 828 — 4 unit, 4 integration), `verify-e2e.ps1` **PASS against the
  configured SQL container**, `npm audit --audit-level=high` passes with the
  same two moderate findings, `npm run typecheck`, `npm test` **470/470** (was
  460 — 10 new: 4 for the customer address editor, 6 for the deal registration
  address), and `npm run build` pass. One EF migration
  (`AddDealRegistrationAddress`); the customer side needed none, the columns
  already existed. No phase exit criterion or hand-set stage share changed —
  this closes an explicitly named gap in ADR-024's own consequences, not a
  roadmap item.

- **2026-09-19 — A reconditioned car costs what it cost, and inventory comes back to zero.** The workshop has capitalised internal work to 1300 Vehicle inventory since the day it was built, correctly and with a comment saying why. Nothing recorded **which car** absorbed it: `FindOwnedAsync` returned the unit id and the caller used it as a yes/no. So delivery relieved 1300 by the unit's acquisition cost alone, and two things followed.

  **Used-vehicle gross was overstated by exactly the recon spend** — the precise failure the posting's own comment in `AccountingService` says it exists to prevent. And **1300 grew forever**: every reconditioned car left its recon behind, so vehicle inventory on the balance sheet drifted permanently upward and stopped tying to the cars on the lot. Neither was catchable by the balancing checks, because every entry balanced on its own. It was an account that never returned to zero, not an entry that failed to add up.

  **`ReconditioningCharge`**, one row per capitalised posting, naming the car, the amount and the repair order. A running total on the unit would have fixed the arithmetic and lost the answer to "where did this $180 come from" — the same reasoning that made `CustomerCredit` a row drawn down by uses rather than a balance. **Append-only**, so a correction is a negative row and never an edit: a carrying value that can be quietly rewritten is one nobody can audit. Written inside the invoicing transaction, so a rolled-back invoice leaves no charge behind.

  **One stay in stock is one unit**, so no separate "stock stay" is modelled. `FindOwnedAsync` excludes Sold and Removed, so a car that leaves and comes back is received as a new unit with a new stock number and starts from nothing. The `ReconditioningCharge` header names itself as the file that breaks first if that ever stops being true.

  **What was deliberately not done.** The register row also asked for the unit id stamped on the ledger line. `JournalLine` has no subject column, and adding one touches an `IAppendOnly` entity and every posting path. The trail exists without it and runs both ways: the invoice's journal entry carries the repair order id in its `Reference`, and the charge names the same repair order — so 1300 walks back to a car, and a car walks forward to its postings. A subject column on the ledger remains a reasonable thing to want; it is not needed for this.

  Also: currency is refused rather than converted, because a rate invented at posting time is a guess buried in the balance sheet. A car with no recorded purchase price keeps a **null** book value rather than reporting its recon as though it were the whole figure, and relieves nothing on sale — exactly as before.

  **The test was checked against the bug, not just against the fix.** `A_reconditioned_car_leaves_nothing_behind_on_inventory_either` passes now; reverting the one line in `DealService` to the old `CostAmount` makes it fail with *"Expected off to be 14680M … but found 14500.00M (difference of -180.00)"*. A test that passes on the broken code proves nothing, and the sibling test that has guarded the no-recon round trip since 2026-09-10 is exactly why this needed its own.

  The stock detail band now shows acquisition, reconditioning and the total separately, with the postings behind it. The list column still reads "Acquisition cost" — which is what it is, and honest, so it was left alone.

  Evidence: `dotnet build` 0/0, `dotnet test` **837/837** (was 828 — nine new), `verify-e2e.ps1` PASS against the SQL container, `npm audit` clean at high, `npm run typecheck`, `npm test` **473/473** (was 460), `npm run build`.

- **2026-09-19 — A record the provider deleted stops being theirs without stopping being ours.** Stage 2's exit criteria named four delivery behaviours a sync must survive — duplicate, reordered, partial-page and delete. Three had tests; **delete had no implementation at all**, and reordered had never been written. Both are now closed, which takes the unmet criteria from five to four.

  **The question the mechanism could not answer.** A provider withdrawing a customer has said something about *its* database. We may have three repair orders, two deals and an outstanding balance against that person. The obvious implementation — hide the record — is worse than leaving deletes unmodelled: the repair orders would still name somebody the screens can no longer find, and an advisor with that customer on the telephone would search and get nothing. A defect that removes information silently is harder to notice, and much harder to explain, than a missing feature.

  **[ADR-026](../adr/0026-a-deleted-record-is-marked-not-removed.md): a delete is a mark, never a removal.** `RemovedAtProviderOn` is deliberately not `IsArchived` — archiving is the dealership's own decision to stop seeing a record, and this is somebody else's statement about their own database. A marked customer stays in every list and every search, still resolves from everything that names them, and loses only the right to be chosen for *new* work. The mark is **visible**: a chip in the list and a sentence on the record saying they are kept and that everything already attached still works. A mark nobody can see is silent removal under another name.

  **A structural constraint pointed at the answer.** `FeatureBoundaryTests` forbids Customers from referencing `Deal` or `RepairOrder`, so the sink *cannot ask* whether a customer has been used. Any "hide it only if unused" design would have needed a cross-capability usage probe invented to support a behaviour we did not want — and would have produced a rule nobody can predict, where the same provider action has two outcomes depending on invisible state. The uniform answer needed no new machinery.

  **Deletes ride on the record, not a second method.** `ProviderRecord` carries a `RecordAction`, because deletes arrive interleaved with upserts in one delta feed and the order between them is the provider's meaning. Four behaviours, each tested: a delete marks and counts as applied; a replayed delete is `Unchanged`, so a stuck feed cannot look busy; a delete for a record we never had is `Unchanged` rather than rejected, because delta feeds report those constantly and quarantining them would bury the real refusals; and a provider serving the record again restores it, because feeds undelete.

  **The reordered criterion hid two different properties**, which is probably why it was never written. Independent records must reach the same state in any order — order between them carries no meaning. Records about the *same* thing must be applied in the order sent — "created then deleted" and "deleted then created" describe different days, and a sink that sorted its batch would turn one into the other. Both are now tests, and so is idempotence *within* a single batch, which a provider paging over a moving window will exercise.

  One defensive fix found on the way: the screen guarded on `=== null`, so a response missing the field entirely threw inside `format.date` and blanked the whole detail band. A field absent from the wire must never blank a screen.

  Evidence: `dotnet build` 0/0, `dotnet test` **845/845** (was 837 — eight new), `verify-e2e.ps1` PASS against the SQL container, `npm audit` clean at high, `npm run typecheck`, `npm test` **476/476** (was 473), `npm run build`.

- **2026-09-19 — The integration edge became reachable, and says what it is.** Two exit criteria closed together because they had the same root cause: **there was no endpoints file for Integrations at all.** `CertificationStatus` had four levels, the shipped connector had always correctly declared itself `FixtureTested`, and no route existed by which a reader could learn it. Quarantined records could be resolved in code and by nothing else. Unmet criteria go from five to **two**, and one of those needs an identity provider.

  **Replay runs the real sink.** It rebuilds the `ProviderRecord` from the stored payload and hands it to the same sink that refused it, inside a transaction. There is no simulation mode and there must not be one — the only replay worth having is the one that would have happened. A replay refused again **stays in the queue** with the *new* reason, because fixing one mapping routinely reveals the next problem behind it and a screen still showing the original would send somebody to look in a place that is already correct. The row counts its attempts, so the history that it has been tried is not lost when the reason is replaced.

  **Resolving on attempt would have been the easy version and the wrong one.** It empties the queue without fixing anything, which is exactly what the quarantine exists to prevent. Dismissing without replaying is still available for the records that will never apply — and it **requires a reason in writing**, because "Resolved" with no note is indistinguishable from a queue nobody read.

  **The payload is never returned by a read.** Those fields are a customer's name, address and telephone number exactly as a provider sent them (ADR-022). `QuarantineEntry` carries the reason and the provider's id and has no payload property at all, so there is nothing to render even by accident; a test asserts the absence of the property rather than the absence of a value. Replay reads it server-side and it stays there.

  **The screen says what a certification promises, not just its name.** "Fixture tested" means nothing to a reader on its own, so the level sits beside *"automated tests only; not a promise that it works against a real provider"*, and the connector's own limitations are shown whatever the level says — which puts "Serves fabricated records. Never certify anything against this." on the screen. Fixture-tested and experimental wear the **same** warning colour on purpose: a dealership running its month-end on either is taking the same kind of risk, and colouring one of them calmly would be the screen taking a view it has no business taking.

  The fixture connector is registered rather than hidden. It is a real shipped connector that serves fabricated records and says so in its own manifest; hiding it would make the screen claim the product has no connectors, which is a different and less honest statement than the truth.

  Evidence: `dotnet build` 0/0, `dotnet test` **852/852** (was 845 — seven new), `verify-e2e.ps1` PASS against the SQL container, `npm audit` clean at high, `npm run typecheck`, `npm test` **483/483** (was 476), `npm run build`.

- **2026-09-19 — The numbers on a deal add up, and the column proves it.** Three register rows, all walked on 2026-09-10 and open since, all one complaint: the deal summary's figures did not agree with each other.

  **Tax was in `AmountDue` on the server and missing from the column that adds up to it**, so a deal showed $33,000 above a total of $36,331.25 with nothing to explain the gap. That is the **third** time this table has lost a line — the trade-in first, then the products, each found the same way, by a person reading down the column in a browser, and each fixed in isolation. So the fix is not a third patch. `the numbers on a deal add up` reads every amount in the column, sums them, and asserts the sum reaches the printed total, with a car, a fee, a discount, a trade-in, a product and tax all present at once. It was checked against the bug: removing the tax rows makes it fail with *"expected 2112.44 to be less than 0.02"*. A fourth thing cannot be quietly left out.

  **A person typed the basis, the rate and the answer, and nothing checked the three agreed** — so a deal could carry a tax figure its own basis and rate contradict, and that figure is the one that reaches the invoice and the ledger. The amount now follows basis × rate as either is typed, rounded to the cent because a cent of float drift is a cent the invoice and the ledger will disagree about forever. **It stays overridable on purpose**: `EnteredByPerson` exists so an unsupported jurisdiction is a label rather than a blocker, and a capped or tiered tax is not a multiplication. What ends is the *silent* disagreement — an override that contradicts its own basis and rate says so on the row, with the figure the arithmetic gives. A line already saved is never rewritten by a later edit to the basis, because a saved figure is somebody's settled decision.

  **`ChargeKind.DocumentationFee` had existed since 2026-09-09** — its own kind rather than a Fee because it is part of the taxable price in most US states while registration fees are not (ADR-024) — and `contracts.ts` and the six locales had never learned it, so the one charge a dealership adds to nearly every deal could not be entered on any screen.

  Walked: a seeded deal now reads Discount −$650.00, Documentation fee $499.00, Vehicle price $16,520.00, Fee $145.00, Trade-in −$3,500.00, Tax $624.15 — summing to **$13,638.15** against a printed **$13,638.15**, with the tax row reading *"Sales tax $12,869.00 at 4.85%"*.

  Evidence: `dotnet build` 0/0, `dotnet test` **852/852**, `verify-e2e.ps1` PASS against the SQL container, `npm audit` clean at high, `npm run typecheck`, `npm test` **492/492** (was 483 — nine new), `npm run build`.

- **2026-09-21 — The paperwork the customer is handed adds up.** A readiness review traced the three journeys a dealership actually runs, and the sale ended somewhere nobody had looked: the **printed vehicle order**. Deal `0e24786d` renders 2025 Toyota RAV4 XLE $32,500.00 and GAP cover $500.00 above **Due from the customer $36,331.25**. The missing $3,331.25 is the sales tax, which `Deal.AmountDue` includes and `DocumentService` never printed. The word "tax" did not occur on the page outside the disclaimer.

  This is the **fourth** time a line inside that total has been missing from the column above it — the trade-in, then the F&I products, then the tax on the deal desk, and now the same tax on the document — and the **second copy of the same column**. The deal desk was answered on 2026-09-19 with a test that reads every amount and sums it; the document had no such test, which is exactly why the fix landed on one side and not the other. It has one now: `The_printed_order_adds_up_to_its_own_total` parses the rendered HTML, pulls every `<td class="num">` out of the table body, sums them, and asserts the sum equals the figure in `<td class="num total">` — with a car, a warranty, a trade-in and tax all present at once.

  **Checked against the bug rather than assumed:** on the committed `DocumentService` the new test fails with *"Expected amounts to contain at least 4 item(s) … but found 3: {20000.00M, 900.00M, -3000.00M}"*, naming the absent line. With the fix it passes at 20000 + 900 − 3000 + 1650 = 19550 against a printed 19550.

  The tax row prints its basis and rate beside the amount — *"Sales tax WA / King / Seattle — $20,000.00 at 8.25%"* — because a buyer querying a tax figure is asking on what and at what rate, and a document that cannot answer sends them back to the desk. A line a person typed outright carries no rate and shows its jurisdiction alone, rather than "at 0%", which would be a false claim about the law.

  The service invoice was checked for the same defect and does not have it: five invoiced jobs sampled through the running application, Labour + Parts + Sent out equal to the printed total in every one. What is *not* covered by that sample is a job split across pay types, since all five were wholly customer-pay.

  Evidence: `dotnet build` 0 warnings / 0 errors, `dotnet test` **853/853** (was 852 — one new), `verify-e2e.ps1` PASS against the SQL container. No frontend change, so the four `npm` gates were not required and were not run.

- **2026-09-21 — A whole lot moves to another installation with its identities, its references and its paperwork intact.** The last exit criterion anybody here could close on their own. I2 asked that an export round trip preserve **IDs, relationships and documents**; on 2026-09-09 that scored as IDs yes, the other two no, and export was two flat CSVs of customer and vehicle columns.

  **Three questions had to be settled before anything could be built**, and the code answered all three (ADR-027).

  *What is a document?* Generated, never stored. `IDocuments` owns no data, `DocumentService` renders a deal or a job into HTML when somebody asks, and a search across `src` for `IFormFile`, `BlobClient`, `FileStream` and `Attachment` returns **nothing** — there is no file storage in this product. So "preserve documents" cannot mean copying files, and shipping the rendered HTML would freeze a copy the far side would immediately contradict. It means **the renderer, pointed at the far side, produces the same page**. That is a strictly stronger claim, because it fails if any field behind the page was lost, including ones nobody thought to assert.

  *What does preserving an ID require?* Keeping it, not carrying it. Every record travels with its own id and every reference is that id, so the receiving installation stores the same keys. Nothing had to be opened up for this: all five aggregates already took an explicit id in their factories.

  *What happens to the money?* Nothing, deliberately. `IInventory.ImportAsync`, `IDeals.ImportAsync` and `IRepairOrders.ImportAsync` write the aggregate in its final state and post nothing. A deal in a package was sold at the other installation and its money is already inside the opening balances the receiving dealership entered when they were set up; posting it again would sell the same car twice. **Opening balances are how the money arrives; a package is how the records arrive.**

  **The paperwork test found two defects nobody was looking for.** A deal's charges, its products, its tax lines and a job's service lines had **no defined print order** — the far side, where those rows carry fresh ids, printed them in a different sequence. Two copies of one document that disagree about their own row order are not the same document, and nothing had ever needed them to be stable, so a reprint *here* was not reproducible either. Both services now order them explicitly.

  **A conflict found by running it rather than by reasoning:** both demo dealerships were seeded from the same generator, so 571 of 886 records carried an external reference already in use on the other side. That is refused by name — the two records may be the same person and merging them is not the importer's decision — and it cascades, because a deal whose customer was refused has nowhere to hang. The report says so, record by record, which is the answer.

  **Walked across two tenant databases**, `citymotors/CM-01` into `northgroup/NAG-02`, through the screen: **"5 written, 315 already here, 571 not brought in"**, every refusal named in a table. The deal's vehicle order and the job's service invoice came out **byte-identical** on the far side, at `$19,400.00` and `$270.00`; the deal still names its customer and its car, the car still names its vehicle, and all of it landed in the lot the person chose rather than the one written in the file.

  Evidence: `dotnet build` 0 warnings / 0 errors, `dotnet test` **864/864** (was 853 — eleven new), `verify-e2e.ps1` PASS against the SQL container, `npm audit` clean at high, `npm run typecheck`, `npm test` **498/498** (was 492 — six new), `npm run build`.

- **2026-09-23 — A workshop that has taken in another installation's records can still book a car in.** Found by CI rather than by a desk, and the mechanism is worth keeping because the failure looked like a race and was not. `RepairOrderService.NextNumberAsync` allocated a job number by **counting** the rooftop's jobs: `RO-{1000 + count + 1}`. That is correct only while a rooftop's numbers are the one contiguous block `1001 … 1000+count`.

  `IRepairOrders.ImportAsync` landed on 2026-09-21 and broke exactly that assumption. It writes the number a job carried at the **other** installation, and only for the subset whose customer and vehicle survived the package — refusing the rest by name. So a rooftop that has received a package is sparse: holes below its top and rows above it. The count then lands on a number already in use, the unique index on `(RooftopId, Number)` refuses the insert, and the person booking a car in is told the number is taken.

  It reached CI rather than anybody's screen because it is **order-dependent**, not racy: `PackageTests` imports `citymotors/CM-01` into `northgroup/NAG-02`, and only a run where that happened first left the NAG-02 tests opening a job into a hole. Everything is sequential inside `HostCollection`; the order of test *classes* is not fixed.

  **Allocation is now a high-water mark**, `MAX(NumberSequence) + 1`, where `RepairOrder.NumberSequence` is a **persisted computed column** the database derives from the display number. Computed rather than stored beside it for two reasons: a stored column would need a data backfill that EF cannot generate, and this project does not hand-edit generated migrations — a computed one is correct for every existing row the moment it exists; and it cannot drift from the `Number` it is derived from. The cost, stated rather than hidden: the number format now lives in the mapping as well as in the service. A prefix guard keeps another installation's `CM-1044` out of our sequence, and anything unparseable maps to 0 and takes no part in it. `MAX` over the string itself would not have done — `"RO-9999"` sorts above `"RO-10000"`, so a workshop passing four digits would have stopped dead.

  **What was deliberately not changed:** the `DbUpdateException` → `NumberTaken` handler in `OpenAsync`. A high-water mark removes the *systematic* reissue; it does not remove two cars booked in at the same instant, and that one should still collide visibly rather than disappear into a retry loop.

  **Proven against the bug first**, which is the only reason the fix is trustworthy: `A_job_number_is_never_reissued_after_a_sparse_import` places one job at `RO-9000` at NAG-02 and opens another there. On the old allocator it was issued **`RO-1081`**. It asserts the number is above everything in use rather than merely different from it — the property the count never had, and one that holds whatever order the classes run in.

  Evidence: `dotnet build` 0 warnings / 0 errors, `dotnet test` **873/873** (was 872 — one new), `verify-e2e.ps1` PASS against the SQL container. Nothing under `frontend/` changed.

- **2026-09-23 — The service invoice adds up on a job somebody else is helping to pay for.** The **fifth** time a summary column in this product has failed to reach the total printed under it, and the first with a different shape — which is the part worth keeping. The other four were a line missing from the column: the trade-in, the F&I products, the tax on the deal desk, the same tax on the printed order. Each was fixed by printing the one missing line.

  Here the total was right and the **column** was wrong. `LabourTotal`, `PartsTotal` and `SubletTotal` are every line of that kind **whatever pays for it**, and the work was itemised at full value; `AmountDue` — printed under them as "Total due" — is **customer-pay only**. So any job carrying warranty or internal work showed figures that overshot what was owed, and overshot it **upwards**, which is the direction that causes an argument at the counter. Five invoiced jobs sampled through the running app on 2026-09-21 all added up, because all five were wholly customer-pay. In the demo dealership **27 of 126 invoiced jobs show nothing due at all** — wholly warranty or internal — and every one of those printed a column of real money under a **$0.00** total.

  **What "fixed" means was a real decision, and it is not "print the missing rows".** Every line of work done to the car is still listed; only the ones the customer is being asked to pay for carry a figure. The rest say why they do not, where an amount would go — `warranty`, `no charge` — exactly as declined work already did, and for the same reason: somebody whose water pump went under warranty should be able to see it was done. What they must not see is the figure, because what a manufacturer is billed is between the dealership and the manufacturer. That is not a new position: `WorkshopPage`'s totals block has said so in as many words since it was written — *"warranty and internal work is money the workshop earns and the customer never sees"*. The renderer had simply never caught up. The three kind totals are customer-pay too, so the summary agrees with the lines above it, and the column reaches the total **by construction** rather than by a compensating row.

  **A second defect, found only by reading a rendered invoice in a browser.** The fix suppressed the warranty amount while the detail column went on printing `2.00 h at $130.00` — both factors of the withheld $260.00, one multiplication apart. Every assertion passed, because they assert on the amount. An unbilled labour line now shows its hours without the rate. A **declined** line keeps its rate, and the asymmetry is deliberate: declined work is customer-pay work that was offered, so the rate is what the customer was quoted and is what makes "we did offer" checkable a year later.

  **Proven against the bug first.** `The_printed_invoice_adds_up_to_its_own_total` builds a job carrying customer, warranty, internal and declined work at once, then reads every amount out of **both** money tables and sums each. On the pre-fix renderer it printed **$485.00 of work under a $180.00 total**. `ColumnOf` gained a heading parameter for it — the invoice has two money tables, and scanning the whole document would have summed them together and reported every invoice as exactly double.

  Evidence: `dotnet build` 0 warnings / 0 errors, `dotnet test` **874/874** (was 873 — one new), `verify-e2e.ps1` PASS against the SQL container, and the document itself read in a browser at RO-1136: `1.50 h at $120.00` → `$180.00`, water pump `2.00 h` → warranty, discs → declined, wiper blade → no charge, Labour $180.00 + Parts $0.00 + Sent out $0.00 = **Total due $180.00**. Nothing under `frontend/` changed.
