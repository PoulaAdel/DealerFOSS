# 08 — Governance & Standards

← [Delivery Roadmap](07-Delivery-Roadmap.md) · Next: [Codex Execution Prompt](09-Codex-Execution-Prompt.md) · [Workbook](00-Workbook.md)

## 1. License and product promise

OpenDealer360’s complete self-hosted core is licensed under GNU AGPLv3. An optional commercial license provides alternative terms for closed embedding or commercial arrangements; it is not required to obtain a functional self-hosted DMS. “Dual-licensed open source” is clearer than implying that required core features are withheld.

The project publishes a trademark policy separately. “OpenDealer360 Certified” may identify tested releases/connectors without restricting lawful forks.

## 2. Contributor provenance and dependencies

The project chooses DCO or CLA with legal review before accepting external code, because optional commercial relicensing requires clear provenance. The chosen policy and rationale are public.

Every source file and package carries SPDX metadata where appropriate. CI checks dependency licenses against an allowlist and produces an SBOM. Commercial dependencies must be optional adapters; a contributor can build, test, and operate the complete AGPL core using redistributable dependencies.

## 3. Governance

- A public charter defines maintainer responsibilities, nomination/removal, voting, conflicts of interest, and succession.
- Each business module and certified connector has named maintainers.
- Architecture changes use immutable ADRs with Proposed, Accepted, Superseded, or Rejected status.
- Product/compliance evidence can block a release; an architectural vote cannot declare an unreconciled financial workflow correct.
- A public roadmap distinguishes committed, planned, and exploratory work.
- The Code of Conduct and private security-reporting process apply to all project spaces.

## 4. Release and support policy

The project publishes:

- a predictable release cadence and supported LTS versions;
- security severity and patch targets;
- API/contract/database compatibility and deprecation rules;
- migration and rollback guides;
- connector certification and support status;
- reproducible release artifacts, checksums/signatures, SBOM, and known limitations.

Experimental community connectors are welcome but cannot be labeled certified without the evidence in [doc 05](05-Integration-Framework.md).

## 5. Coding standards

- Nullable reference types and warnings-as-errors are enabled.
- Async I/O accepts `CancellationToken`; no `async void` outside true event handlers.
- Domain code has no EF/ASP.NET/provider dependency.
- Expected business failures use typed results; unexpected failures use centralized exception handling and Problem Details.
- No `IQueryable`, entity type, connection string, secret, or vendor DTO crosses a module/public API boundary.
- Commands that may be retried are idempotent.
- Every business write produces the required audit event through shared transaction behavior, not an easily forgotten manual line.
- Monetary arithmetic uses `decimal` plus currency; dates distinguish instant, local date, and dealership time zone.
- User-visible strings are localizable. Accessible semantics and keyboard behavior are part of definition of done.
- Database migrations are forward-safe, reviewed, tested from every supported version, and never modify posted immutable history.

## 6. Adding a feature

- [ ] Confirm the owning module and rooftop/legal-entity scope.
- [ ] Define state transition, authorization, audit, retention, and concurrency behavior.
- [ ] Add or update domain model and feature handler.
- [ ] Add EF mapping/migration and indexes based on query use.
- [ ] Change a public contract only with compatibility/version review.
- [ ] Add unit, integration, authorization, migration, and relevant accessibility/print tests.
- [ ] Add telemetry for failure, latency, queue/lag, or business exception where operators need it.
- [ ] Update the owning document and diagram if behavior or a decision changed.

## 7. Adding a connector

- [ ] Create a provider folder/project and accurate manifest.
- [ ] Keep vendor DTOs/protocols inside the connector.
- [ ] Map to supported versioned contracts with provenance and mapping warnings.
- [ ] Implement auth rotation, paging, throttling, timeout, retry, and health behavior.
- [ ] Pass conformance tests for duplicates, ordering, deletes, partial failure, idempotency, replay, and reconciliation.
- [ ] Provide sanitized fixtures and mapping documentation.
- [ ] Label it Fixture-tested, Sandbox-certified, Production-certified, or Experimental—never imply more.

## 8. Developer onboarding

Local development uses a checked-in Compose profile for SQL Server and optional Redis, plus .NET and Node. Sample data includes one single-rooftop and one multi-rooftop dealer organization. Setup commands validate prerequisites, create development keys, migrate/seed, and print the local URLs; secrets are never committed.

A new contributor reads:

1. `README.md` and [workbook index](00-Workbook.md);
2. [Vision & Scope](01-Vision-and-Scope.md);
3. [Architecture & Decisions](02-Architecture-and-Decisions.md);
4. the module or connector README for their task;
5. architecture and conformance tests;
6. `CONTRIBUTING.md`, security policy, and relevant ADRs.

## 9. Branching and review

Use trunk-based development with short-lived branches. Every pull request links an issue, states user/operational impact, identifies schema/public-contract/security effects, and passes CI. At least one code-owner review is required; security-, accounting-, compliance-, migration-, and connector-certification changes require the relevant specialist reviewer.

## 10. Sustainability and strategy

Revenue comes from managed hosting, implementation/migration, support, certified integrations, training, and optional commercial licensing. Self-hosting and data export remain complete.

The project protects its ten-year path by funding boring reliability: migration, reconciliation, updates, restore, documentation, certification, and support. Those are harder to copy—and more valuable to dealers—than a broad list of partially implemented modules.
