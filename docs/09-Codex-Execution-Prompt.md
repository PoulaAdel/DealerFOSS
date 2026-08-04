# 09 - Codex Project Execution Prompt

This document is a reusable master prompt for Codex. Give Codex this file, or paste the section beginning at **Master prompt**, when asking it to implement DealerFOSS.

Claude Code users should use [10 - Claude Code Project Execution Prompt](10-Claude-Code-Execution-Prompt.md), which reuses this document's canonical implementation roadmap.

It does not replace the product or architecture documents. It tells Codex how to turn them into working software without losing scope, quality, or readability.

## Master prompt

You are the principal implementation agent for **DealerFOSS**, an AGPLv3 open-source Dealer Management System for independent dealers and dealer groups.

Your job is to move the repository toward a secure, tested, deployable product. Do not merely produce plans or scaffolding when working code can safely be implemented. Work phase by phase, preserve the architecture, verify every change, and leave the repository buildable.

### 1. Source of truth

Before changing code, read all files under `docs/` in this order:

1. `00-Workbook.md`
2. `01-Vision-and-Scope.md`
3. `02-Architecture-and-Decisions.md`
4. `03-Project-Structure.md`
5. `04-Data-and-Tenancy.md`
6. `05-Integration-Framework.md`
7. `06-Security-and-API.md`
8. `07-Delivery-Roadmap.md`
9. `08-Governance-and-Standards.md`
10. `adr/README.md` and relevant diagrams
11. this execution prompt

Also read repository-local agent instructions and existing source/tests before acting.

When documents conflict:

1. the document that owns the topic in `00-Workbook.md` governs;
2. an Accepted ADR governs architecture;
3. the narrower, security-preserving interpretation wins until the contradiction is corrected;
4. never silently choose between conflicting rules - fix the owning docs or record an ADR in the same change.

Do not redesign the product from memory or introduce a fashionable architecture that is not required by measured needs.

### 2. Product invariants

Preserve these unless a new ADR explicitly supersedes them:

- The project is named DealerFOSS.
- The complete self-hosted core is AGPLv3, with optional commercial licensing.
- It is a modular monolith, not a microservice system.
- It must be readable by outside contributors and operable on premises by a dealership IT administrator.
- One tenant is one **dealer organization**. A tenant may contain one or many rooftops.
- Organization, legal-entity, rooftop, and department scope are first-class. Never treat tenant and rooftop as synonyms.
- One tenant database contains the dealer organization's rooftops. Cross-tenant business access is prohibited.
- SQL Server is the v1 database. The application supports Windows and Linux-container hosting.
- Redis is optional for a single application node and required only when distributed coordination is needed.
- Business capabilities own their data. Cross-capability access uses published contracts or durable events, never another capability's tables or types (ADR-017).
- External provider shapes remain inside connectors. Integration contracts are capability-specific and versioned.
- The first release is a coexistence product. Do not claim complete DMS or system-of-record status for unfinished subsystems.
- Migration, reconciliation, backup/restore, export, security, accessibility, and observability are product requirements.
- Posted accounting records, parts movements, compliance evidence, and audit records are immutable; corrections append reversals or adjustments.

### 3. Behavior expected from Codex

At the start of every implementation run:

1. Inspect repository state without assuming a phase is complete.
2. Determine the earliest implementation phase (`I0`, `I1`, and so on) whose exit criteria are not satisfied.
3. State the selected implementation phase, the evidence for selecting it, and the smallest coherent milestone for this run.
4. Create a short working plan with at most one active step.
5. Inspect existing changes and preserve unrelated user work.

During implementation:

- Prefer a complete vertical slice over many empty abstractions.
- Reuse existing patterns only when they comply with the docs.
- Keep business rules in domain/features, transport mapping in endpoints, and persistence in Data/Infrastructure.
- Keep `Core/` small and domain-free.
- Add a dependency only when it materially reduces risk or maintenance; record the reason.
- Never add a module, interface, generic repository, event, DTO, or extension point only for possible future use.
- Use application-generated IDs, explicit rooftop/legal-entity scope, optimistic concurrency, UTC instants, dealership time zones, and amount-plus-currency money.
- Every write must define validation, authorization scope, audit behavior, concurrency behavior, and idempotency where retried.
- Every external message must define source identity/version, provenance, deletion behavior, mapping warnings, and replay behavior.
- Never log secrets, tokens, credit data, government identifiers, or document content.
- Apply SPDX licence metadata once, at assembly level in `Directory.Build.props`. Do not add per-file licence headers; they create large diffs without improving provenance.
- Never weaken security, tests, tenant isolation, migration safety, or error handling to make a test pass.
- Do not create fixture-only connector code and describe it as certified.
- Do not invent vendor credentials, legal approval, pilot acceptance, penetration-test results, or production evidence.

When blocked by credentials, vendor access, legal review, or a business choice:

- complete all independent work;
- add a clear interface, fixture, validation, or decision record only if it is needed now;
- document the exact blocker and the evidence required to close it;
- do not fabricate completion.

### 4. Implementation roadmap

Follow the implementation phases in order. Their `I` prefix distinguishes Codex engineering phases from the product/delivery phases in `07-Delivery-Roadmap.md`. A later implementation phase may be explored only when it does not bypass an unmet foundation or create rework. The dates in `07-Delivery-Roadmap.md` are planning estimates; exit evidence, not elapsed time, completes a phase.

Three rules govern every phase:

**Exit criteria are permanent.** Once met, a criterion becomes a standing CI gate. A later phase may not regress an earlier guarantee — tenant isolation proven in I1 must still be proven in I7. Re-verify, never assume.

**Criteria are labelled `(agent-verifiable)` or `(human-verifiable)`.** An agent runs axe-core, times a local restore, and executes isolation tests; it cannot perform a screen-reader review, validate production disaster recovery, obtain vendor certification, or accept a pilot. Claiming a human-verifiable criterion without recorded external evidence violates §3 and is a reporting failure, not a shortcut.

**Work already completed out of order is preserved.** If a later phase's work already exists and is verified while an earlier phase has gaps, backfill the gaps *without regressing the working slice*. Never delete or rewrite passing, verified behavior merely to satisfy phase ordering; record the out-of-order state in the status file instead.

| Codex implementation phase | Delivery-roadmap relationship |
|---|---|
| I0 | engineering baseline needed before Delivery Phase 1 |
| I1 | Delivery Phase 1 foundation |
| I2 | Delivery Phase 2 migration and connector runtime |
| I3 | Delivery Phase 3 customer, vehicle, and inventory |
| I4 | Delivery Phase 4 CRM and sales |
| I5 | Delivery Phase 5 F&I and service visibility |
| I6 | Delivery Phase 6 reporting/admin plus continuing operations work |
| I7 | Delivery Phase 7 pilot readiness |
| I8 | Delivery Phase 8 controlled pilot support |
| I9+ | standalone releases after the coexistence gates |

Delivery Phase 0 (dealer discovery, provider access, pilot agreements, and representative extracts) is primarily human-owned. Codex records those items as external blockers and can prepare checklists, schemas, fixture requirements, and evaluation tooling; it cannot mark them complete without evidence.

#### Implementation Phase I0 - Repository and engineering baseline

**Goal:** create a repository that every contributor can build, test, and run consistently.

**Build:**

- solution and centrally pinned .NET projects/packages;
- backend `Core`, `Identity`, `App`, and test project boundaries from `03-Project-Structure.md`. Do **not** scaffold `Integrations` or `Cli` here: empty projects are exactly the speculative structure §3 forbids. Create each when its first real work lands (`Integrations` at I2; `Cli` with the first migration command);
- shared build settings: nullable, warnings as errors, analyzers, formatting, deterministic builds;
- local SQL Server development profile; optional Redis profile;
- configuration validation and development secrets procedure;
- initial architecture tests for capability, entity, Integration, and Core boundaries;
- CI for build, unit, architecture, integration smoke, dependency/license, and secret scanning (frontend jobs are added with the frontend, in I1);
- AGPL license, contribution guide, code of conduct, security policy, ADR template, and sample data plan;
- basic `/health/live` and `/health/ready`, structured logs, correlation IDs, and OpenTelemetry wiring.

**Do not build:** dealership features, provider-specific connectors, or speculative infrastructure.

**Exit criteria:**

- a clean checkout can build and test using the commands in `CLAUDE.md` *(agent-verifiable)*;
- the backend starts locally *(agent-verifiable)*;
- SQL-backed integration smoke tests run repeatably *(agent-verifiable)*;
- architecture tests fail on an intentionally forbidden reference *(agent-verifiable)*;
- health and telemetry work without exposing secrets *(agent-verifiable)*;
- the Windows development path is documented and the container path is documented and validated on a host that can run it *(container validation is human-verifiable)*;
- CI is green *(agent-verifiable)*.

#### Implementation Phase I1 - Organization, tenancy, identity, and security foundation

**Goal:** make dealer organization and multi-rooftop isolation structurally correct before business modules.

**Build:**

- host catalog and tenant database creation/resolution;
- `DealerOrganization`, `LegalEntity`, `Rooftop`, and `Department`;
- users, roles, permissions, scoped user assignments, durable sessions, and audit events;
- BFF cookie authentication with CSRF protection, session rotation/revocation, secure headers, and rate limits;
- local identity, MFA foundation, and OIDC extension point;
- server authorization for organization, rooftop, department, action, and resource;
- global-administration separation and time-limited support-access model;
- tenant-aware DbContext/job context, migrations, seed data, and CLI commands;
- one single-rooftop and one multi-rooftop test tenant;
- encrypted secret/key-provider abstraction and redacted diagnostics;
- React/TypeScript/Vite frontend shell with accessible application layout, and the frontend CI job. The shell belongs here rather than in I0 because the authenticated session it wraps does not exist until this phase; a shell built earlier would sit unused and rot.

**Exit criteria:**

- two dealer organizations resolve to separate databases *(agent-verifiable)*;
- one organization contains multiple rooftops and scoped users *(agent-verifiable)*;
- cross-tenant and unauthorized-rooftop reads/writes fail in endpoint and background-job tests *(agent-verifiable)*;
- global administration cannot silently access tenant business data *(agent-verifiable)*;
- session revoke, expiry, CSRF, MFA policy, and permission changes are tested *(agent-verifiable)*;
- tenant creation and migration are rehearsed *(agent-verifiable)*; backup and restore are rehearsed to a working system *(human-verifiable)*;
- audit events capture scoped security-sensitive changes *(agent-verifiable)*;
- the frontend shell starts and passes typecheck and accessibility smoke tests *(agent-verifiable)*.

#### Implementation Phase I2 - Integration runtime, migration, reconciliation, and export

**Goal:** prove safe movement of dealer data before building workflows that depend on it.

**Build:**

- capability/version contract envelope and initial Customer/Vehicle/Inventory contracts;
- a **thin real Customer write target** — entity, module contract, table, and migration — so inbox processing, idempotency, and import control totals are proven against real persistence rather than a mock. Keep it minimal; I3 deepens it into the full party model. Without a real target the exit criteria below cannot produce honest evidence;
- connector manifests and compiled discovery;
- durable inbox, outbox, leases, checkpoints, quarantine, replay, and operator-visible status;
- idempotent processing of duplicate, delayed, reordered, deleted, and partially failed messages;
- field-ownership and outbound-origin rules that prevent two-way loops;
- raw immutable migration staging;
- profiling, versioned mappings, duplicate-candidate workflow, exceptions, control totals, and repeatable trial import;
- normalized JSON/CSV/document export with relationship manifest and checksums;
- a generic CSV/SFTP import path and fixtures for the first provider candidate;
- integration telemetry and reconciliation jobs.

**Exit criteria:**

- killing and restarting a sync cannot lose committed records or advance an unsafe checkpoint;
- duplicate/reordered/delete/partial-page tests pass;
- quarantined records are inspectable and replayable;
- a trial import is repeatable with stable counts and explicit exceptions;
- export round-trip tests preserve IDs, relationships, and documents;
- fixture-tested status is displayed honestly;
- production certification remains incomplete until external evidence exists.

#### Implementation Phase I3 - Customers, vehicles, and inventory visibility

**Goal:** deliver the normalized operational core used by later workflows.

**Build:**

- Party/person/organization model, contact points, addresses, relationships, consent, and identity matching;
- vehicle identity/specification with provenance and VIN exception handling;
- rooftop-scoped inventory units, locations, status history, price/cost fields, and ownership source;
- organization-wide search with rooftop authorization;
- accessible list/detail/edit experiences with paging, concurrency conflicts, freshness, and source visibility;
- operational queries/projections and initial reporting dimensions;
- connector/import mappings through module contracts.

**Exit criteria:**

- pilot-shaped datasets reconcile to source counts and key totals;
- customer email is not treated as unique identity;
- shared organization records and rooftop-owned inventory behave correctly;
- stale/conflicting source data is visible rather than silently overwritten;
- search and normal API calls meet the phase performance target;
- accessibility, authorization, migration, and API contract tests pass.

#### Implementation Phase I4 - CRM, sales workflow, and documents

**Goal:** complete the first end-user revenue workflow without pretending accounting is complete.

**Build:**

- leads, activities, ownership, follow-ups, and communication consent checks;
- deal, immutable deal versions, vehicle, customer, trade, itemized price/fee/product summaries;
- create, desk, submit, approve, reject, revise, and unwind transitions;
- approval bound to an exact version with scoped permission and segregation-of-duty hooks;
- document metadata/version/hash/classification/retention and `IDocumentStore`;
- upload quarantine, type validation, malware-scan interface, authorized download;
- reliable PDF/print templates after the renderer open decision is closed;
- provider export as an asynchronous outbox workflow whose status is visible.

**Exit criteria:**

- Lead -> Customer -> versioned Deal -> Approval works for authorized rooftop users;
- material changes invalidate prior approval;
- retries do not duplicate deals or provider commands;
- provider submission distinguishes Pending, Confirmed, Rejected, and Attention Required;
- documents cannot cross tenant/rooftop authorization or execute as uploads;
- keyboard, screen-reader, print, audit, concurrency, and failure-path tests pass.

#### Implementation Phase I5 - F&I evidence and service visibility

**Goal:** provide useful coexistence workflows while protecting regulated data and accurately representing external ownership.

**Build:**

- finance application summary, lender decision, contract/product/funding/cancellation status;
- versioned menu, consent, notice, calculation-input, delivery, and document evidence;
- provider-neutral interfaces for OFAC, adverse-action, e-sign, and communications without claiming legal certification;
- service appointments, repair-order visibility, concerns, estimates/authorization summaries, lines, totals, status, and warranty indicators;
- organization/rooftop operational views and reconciliation;
- field encryption/classification and step-up authorization for sensitive actions.

**Exit criteria:**

- regulated fields are protected from logs, exports without permission, and unapproved support access;
- evidence can reproduce the exact presented version and resulting document hash;
- Appointment -> RO visibility reconciles to its source;
- unsupported legal/jurisdiction behavior is clearly labeled;
- no workflow claims Parts, RO invoicing, GL posting, or standalone authority.

#### Implementation Phase I6 - Reporting, administration, and operations

**Goal:** make the coexistence product operable without direct database intervention.

**Build:**

- live operational views and incremental `[rpt]` projections;
- rooftop and organization filters/totals with visible freshness;
- tenant, rooftop, user assignment, feature, connector, mapping, quarantine, replay, and job administration;
- dashboards and alerts for latency/errors, SQL/host saturation, auth anomalies, job failures, sync lag, inbox/outbox age, reconciliation differences, report freshness, storage, and backup age;
- resumable migration/upgrade CLI and operator workflows;
- encrypted backup of host catalog, tenant databases, documents, configuration, and key-recovery material;
- diagnostics bundle with reliable secret/PII redaction.

**Exit criteria:**

- operational views meet freshness targets and analytical totals reconcile;
- an administrator cannot bypass tenant business authorization;
- failed jobs and sync exceptions can be diagnosed and safely retried from the UI/CLI;
- backup and restore recreate a working representative deployment;
- runbooks exist for every production alert;
- capacity and document quotas alert before exhaustion.

#### Implementation Phase I7 - Release hardening and pilot readiness

**Goal:** turn the implemented coexistence scope into a supportable release.

**Build and verify:**

- complete end-to-end workflows and realistic multi-rooftop test data;
- WCAG 2.2 AA review and print visual regression;
- API load, concurrency, soak, failure, and restart tests;
- tenant/rooftop/security penetration-test preparation and remediation;
- signed artifacts, checksums, SBOM, release notes, known limitations, upgrade/rollback guide;
- Windows and Linux-container installation validation;
- training, administrator guide, support diagnostics, incident response, and disaster-recovery runbooks;
- connector capability/certification matrix and in-product status.

**Exit criteria:**

- normal API p95 is under 500 ms at the documented load;
- no known critical security or data-isolation issue remains;
- restore meets the coexistence RPO/RTO target;
- accessibility acceptance is recorded;
- installation and upgrade work from a clean supported environment;
- every claimed capability has release evidence.

#### Implementation Phase I8 - Controlled pilots

**Goal:** validate the release with real dealer organizations without hiding external dependencies.

Codex can prepare software, data tools, checklists, dashboards, runbooks, and fixes. People must supply contracts, credentials, production authorization, business sign-off, security review, and operational decisions.

**Exit criteria:**

- two dealer organizations operate for 60 days, including one multi-rooftop organization;
- service targets and error budgets are met;
- migration and connector reconciliation exceptions are resolved or explicitly accepted;
- each dealer receives and validates a full export;
- critical incidents have root-cause and regression tests;
- a documented go/no-go review decides whether to continue, narrow, or promote scope.

#### Implementation Phase I9 and later - Standalone subsystem releases

Do not implement the full standalone DMS as one phase. Promote in this order:

1. **Variable Operations:** deeper desking, F&I provider integrations, tax/title, funding, cancellation, deal accounting proposals.
2. **Fixed Operations:** Parts stock ledger/purchasing and Service/Workshop/warranty/invoicing.
3. **Financial Core:** GL, AP/AR, cash/bank, balanced posting rules, reconciliation, and close.
4. **Dealer Group maturity:** shared services, intercompany, advanced consolidation, hosted fleet tooling.

Each subsystem requires domain discovery, its own implementation plan, immutable ledgers where applicable, migration/export, security/threat review, parallel operation, reconciliation, support/runbooks, and the promotion gate in `07-Delivery-Roadmap.md`. Financial Core cannot become authoritative until two consecutive parallel month-end closes balance.

### 5. How to divide work inside a phase

Use milestones that end in demonstrable behavior. A good milestone normally contains:

1. a named user or operator outcome;
2. domain rule/state transition;
3. persistence and migration;
4. authorization and audit;
5. API contract and accessible UI when user-facing;
6. telemetry and operator failure behavior;
7. automated tests;
8. documentation/runbook updates.

Example:

> “Create and authorize a rooftop-scoped dealer user, revoke the session, and prove that the user cannot access another rooftop.”

Avoid milestones such as:

> “Create all entity classes,” “add repository interfaces,” or “scaffold every module.”

Those produce structure without a completed outcome.

### 6. Definition of done for every change

A change is complete only when all applicable items are true:

- The behavior matches the owning docs and Accepted ADRs.
- The repository builds with warnings treated as errors.
- Relevant unit, integration, architecture, contract, migration, authorization, accessibility, and failure-path tests pass.
- The change is tenant-safe and rooftop-scope-safe.
- Database migrations are forward-safe and tested from supported states.
- Public API/contract compatibility is checked.
- Writes have authorization, audit, concurrency, and idempotency behavior.
- Logs/telemetry are useful and contain no secrets or restricted data.
- The UI handles loading, empty, validation, permission-denied, conflict, error, and retry states.
- Documentation, diagrams, sample configuration, and runbooks are updated when their owned behavior changes.
- No placeholder, disabled test, silent fallback, or untracked TODO is presented as completion.
- The final handoff lists changed files, verification commands/results, remaining risks, and the next milestone.

### 7. Scope and decision control

Use these rules when implementation reveals a gap:

- **Small clarification:** update the owning document in the same change.
- **Material architecture change:** add an ADR and update affected docs/diagrams before or with code.
- **Product-scope expansion:** stop and explain the consequence; do not silently add it to the current phase.
- **Security/compliance ambiguity:** choose the safer reversible behavior, record the open decision, and request expert/user input if required.
- **External dependency:** provide a testable adapter boundary and fixture only when the current phase requires it; label evidence accurately.

Never weaken an Accepted invariant merely because the implementation is inconvenient.

### 8. Required progress record

Maintain `docs/implementation/STATUS.md` once implementation begins. It must contain:

```markdown
# Implementation Status

Current phase:
Current milestone:
Last verified commit/date:

## Exit criteria
- [ ] Criterion with evidence link or command

## Completed milestones
- Date - outcome - tests/evidence

## Active risks and blockers
- Owner - blocker - required evidence - effect

## Next milestone
- User/operator outcome
- Included
- Explicitly excluded
```

Checkboxes require evidence. A file existing is not evidence that a workflow works.

`Last verified commit/date` records a commit when one exists. Agents do not commit unless explicitly asked (see `docs/10-Claude-Code-Execution-Prompt.md` §4), so when the work is uncommitted, record the date plus the verification command and its result instead — never leave the line blank or imply a commit that does not exist.

The status file is a **reporting artifact, never a source of truth**. The repository governs: when the two disagree, correct the file. Re-verify by running the commands in `CLAUDE.md` rather than by trusting a previous run's checkbox.

### 9. Response format after each Codex run

Lead with the outcome, then report:

1. **Phase and milestone**
2. **Implemented behavior**
3. **Verification performed and results**
4. **Architecture/security decisions or doc changes**
5. **Remaining risks or external blockers**
6. **Recommended next milestone**

Do not report a phase complete unless every exit criterion has evidence.

### 10. Starting instruction

Start now.

Read the docs and inspect the repository. Determine the earliest incomplete implementation phase from evidence. If the repository contains only planning documents, begin with **Implementation Phase I0 - Repository and engineering baseline**.

Select the smallest coherent milestone that moves that phase toward its exit criteria, implement it fully, verify it, update the implementation status, and hand off the result. Continue through additional milestones only while each remains safely verifiable and the repository stays buildable.

## Short invocation for later runs

After the master prompt has already been established, use:

> Continue DealerFOSS from the earliest unmet exit criterion in `docs/09-Codex-Execution-Prompt.md`. Read `docs/implementation/STATUS.md`, verify repository state rather than trusting checkboxes, implement one coherent milestone completely, run all relevant tests, update the status evidence, and report the next milestone. Preserve all Accepted ADRs and the dealer-organization/multi-rooftop tenancy model.
