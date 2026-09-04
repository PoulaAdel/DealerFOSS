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

**Every hand-written source file opens with a four-part header.** Applied to all
347 of them on 2026-09-04; `.cs`, `.ts`, `.tsx`, `.css`, `.ps1` and `.yml` alike.

```csharp
// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   What this file is for, why it is shaped the way it is, and the engineering
//   judgement behind it. A reader should learn whether this file concerns them,
//   and why it exists as a separate thing, before scrolling.
//
// Usage:
//   new Money(24995.00m, "USD")
//   a.Add(b)  → refuses when the currencies differ
//
// Coding Instructions:
//   What belongs here, what does not, and the trap not visible from the code.
//   Written for the person who will get it wrong.
```

`Usage` is for the caller; `Coding Instructions` is for whoever changes the
file. Write the latter for the person who will get it wrong. Both complement the
XML `<summary>` on the type, which serves IntelliSense, rather than repeating it.

`#` replaces `//` in PowerShell and YAML; `/* … */` in CSS. The four section
names do not change.

**Two exclusions, both load-bearing:**

- **Anything under `Migrations/`** — generated, forbidden to hand-edit, and EF
  overwrites it. 50 files.
- **`.json`** — `package.json` cannot carry comments at all.

**Licensing follows from this.** SPDX is declared per file in the header AND once
at assembly level in `Directory.Build.props` (`AGPL-3.0-or-later`). The per-file
line is the authority for a file copied out of the tree; the assembly attribute
is what a package consumer sees. Neither is redundant.

> **On the history of this rule.** The header was three parts with no copyright
> line until 2026-09-04, and this section said explicitly *"do not add per-file
> licence headers"*. The maintainer reversed that on 2026-08-15 and asked for it
> to be applied later, which is why the decision and the conversion carry
> different dates. The conversion restructured
> the existing prose rather than replacing it — summary to `Overview`, `Use:` to
> `Usage:`, `Edit:` to `Coding Instructions:` — because those headers carry the
> most specific reasoning in the repository and a generic four-section header
> repeated 347 times would be noise people learn to skip.

## 6. Adding a feature

- [ ] Confirm the owning module and rooftop/legal-entity scope.
- [ ] Define state transition, authorization, audit, retention, and concurrency behavior.
- [ ] Add or update domain model and feature handler; every new file opens with the four-part header (§5).
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
