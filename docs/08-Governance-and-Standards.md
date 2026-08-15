# 08 — Governance & Standards

← [Delivery Roadmap](07-Delivery-Roadmap.md) · Next: [Implementation Roadmap](09-Implementation-Roadmap.md) · [Workbook](00-Workbook.md)

## 1. License and product promise

DealerFOSS’s complete self-hosted core is licensed under GNU AGPLv3. An optional commercial license provides alternative terms for closed embedding or commercial arrangements; it is not required to obtain a functional self-hosted DMS. “Dual-licensed open source” is clearer than implying that required core features are withheld.

The project publishes a trademark policy separately. “DealerFOSS Certified” may identify tested releases/connectors without restricting lawful forks.

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

**Every hand-written source file opens with a header.** Three parts, kept short —
a reader should learn whether this file concerns them before scrolling:

```csharp
// Money — an amount together with its ISO currency.
//
// Use:  new Money(24995.00m, "USD"). Add/Subtract refuse mixed currencies on
//       purpose; there is no implicit conversion anywhere in the system.
// Edit: only add operations that are true for every currency. Conversion needs
//       a rate and a date, so it belongs to the module that owns those.
```

`Use` is for the caller; `Edit` is for whoever changes the file — what belongs
here, what does not, and the trap that is not visible from the code. Write the
`Edit` line for the person who will get it wrong. It complements the XML
`<summary>` on the type, which serves IntelliSense, rather than repeating it.
Generated files (anything under `Migrations/`) are exempt.

### The agreed successor — decided 2026-08-15, not yet applied

The maintainer has settled a four-part header that adds a copyright notice and
splits the prose more explicitly. **It is not in the tree yet**: all 347
hand-written source files still carry the three-part form above, and that remains
the standard until the change is scheduled and applied in one pass. Recorded here
so the decision does not evaporate between sessions, and so nobody applies half
of it.

```csharp
// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   What this file is for, why it is shaped the way it is, and the engineering
//   judgement behind it. Absorbs the old one-line summary and grows it: a
//   reader should learn whether this file concerns them, and why it exists as
//   a separate thing, before scrolling.
//
// Usage:
//   new Money(24995.00m, "USD")
//   a.Add(b)  → refuses when the currencies differ
//
// Coding Instructions:
//   What belongs here, what does not, and the trap not visible from the code.
//   Written for the person who will get it wrong.
```

**Mapping from the current form**, so nothing is lost in the reformat: the
summary line and its reasoning become `Overview`, `Use:` becomes `Usage:`, and
`Edit:` becomes `Coding Instructions:`. The existing headers carry the most
specific reasoning in the repository and are to be **restructured, never
replaced with boilerplate** — a generic four-section header repeated 347 times is
noise people learn to skip, which is worse than the three-part header it replaced.

**Scope when it is applied:** `.cs`, `.ts`, `.tsx`, `.css`, `.ps1` and `.yml`.
Two exclusions, both load-bearing:

- **Anything under `Migrations/`** — generated, forbidden to hand-edit, and EF
  overwrites it.
- **`.json`** — `package.json` cannot carry comments at all.

**This supersedes the SPDX rule below when it lands**, and not before.

### Licence headers — the rule that holds today

SPDX is applied **once at assembly level** in `Directory.Build.props`
(`AGPL-3.0-or-later`), and per-file licence headers are not used. The four-part
header above reverses this deliberately; until it is applied, do not add a
copyright line to an individual file, because a repository where some files carry
one and most do not is worse than either consistent answer.

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
- [ ] Add or update domain model and feature handler; every new file opens with a Use/Edit header (§5).
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

A new contributor starts at **[Onboarding](ONBOARDING.md)**, which is the one
door and orders the reading for them: the repository map, then STATUS, then a
single request traced through nine files, and only then the design documents.

This section used to give a different, longer list starting at `README.md` —
a second entry point that quietly disagreed with the first. Two doors is worse
than either door, so there is now one, and the order lives there.

## 9. Branching and review

**The policy below is for the project this becomes when a second contributor
arrives. It is not how the repository runs today**, and saying so is the point of
this note — a governance document describing a review process nobody performs
teaches a newcomer to expect one.

Today: one maintainer, one agent committing sequentially to `main`, pushes done
by hand, and local verification (`dotnet build`, `dotnet test`,
`verify-e2e.ps1`, and the frontend gate) as the real gate. Feature branches would
add a second layer of isolation on top of one that already exists.

The policy, for when it applies: trunk-based development with short-lived
branches. Every pull request links an issue, states user/operational impact,
identifies schema/public-contract/security effects, and passes CI. At least one
code-owner review is required; security-, accounting-, compliance-, migration-,
and connector-certification changes require the relevant specialist reviewer.
Adopt it properly at the moment a second person can review, not before.

## 10. Sustainability and strategy

Revenue comes from managed hosting, implementation/migration, support, certified integrations, training, and optional commercial licensing. Self-hosting and data export remain complete.

The project protects its ten-year path by funding boring reliability: migration, reconciliation, updates, restore, documentation, certification, and support. Those are harder to copy—and more valuable to dealers—than a broad list of partially implemented modules.
