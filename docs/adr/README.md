# Architecture Decision Records

Each Accepted decision has an immutable one-topic file here. [02 — Architecture & Decisions](../02-Architecture-and-Decisions.md) carries the same decisions as a readable summary.

Statuses are **Proposed**, **Accepted**, **Superseded**, or **Rejected**. A material change creates a new dated ADR that supersedes the old one; it never silently rewrites history.

**Which governs when an ADR and doc 02 differ.** The rule used to be "the ADR
file governs", full stop, and that sent readers to the wrong text twice — ADR-007
names a folder that moved and ADR-009 names a library that was never used. So the
rule is now split:

- On the **decision** — what was chosen and why — the ADR governs. That is what
  it is for, and it is immutable so that the reasoning survives.
- On a **fact about the code** — a path, a library, a type name — whichever text
  matches the repository governs, and the repository beats both.

An ADR carrying a stale fact gets a dated **Correction** section appended, saying
what is now true and confirming the decision is unaffected. It is never rewritten.
Two carry one today: [ADR-007](0007-integration-platform-edge.md) and
[ADR-009](0009-browser-sessions-and-api-tokens.md).

| ADR | Decision | Status |
|---|---|---|
| [ADR-001](0001-modular-monolith.md) | Modular monolith | Accepted |
| [ADR-002](0002-capability-first-modules.md) | Capability-first modules with limited internal structure | Superseded by [ADR-017](0017-three-projects-flat-features.md) |
| [ADR-003](0003-database-per-dealer-organization.md) | Database per dealer organization | Accepted |
| [ADR-004](0004-operational-views-and-reporting-projections.md) | Live operational views and isolated reporting projections | Accepted |
| [ADR-005](0005-redis-optional-single-node.md) | Redis optional for single-node deployments | Accepted |
| [ADR-006](0006-versioned-integration-contracts.md) | Versioned integration contracts | Accepted |
| [ADR-007](0007-integration-platform-edge.md) | Integration is a platform edge | Accepted |
| [ADR-008](0008-module-contracts-and-durable-events.md) | Module contracts and durable events | Accepted |
| [ADR-009](0009-browser-sessions-and-api-tokens.md) | Browser sessions and API tokens | Accepted |
| [ADR-010](0010-document-storage-behind-interface.md) | Document storage behind `IDocumentStore` | Accepted |
| [ADR-011](0011-one-folder-per-connector.md) | One folder/project per connector | Accepted |
| [ADR-012](0012-compiled-connector-discovery.md) | Compiled connector discovery | Accepted |
| [ADR-013](0013-agplv3-with-optional-commercial-license.md) | AGPLv3 with optional commercial license | Accepted |
| [ADR-014](0014-boundaries-enforced-three-ways.md) | Compiler, architecture-test, and schema boundary enforcement | Accepted |
| [ADR-015](0015-sql-server-first-cross-platform.md) | SQL Server first, cross-platform application | Accepted |
| [ADR-016](0016-immutable-business-ledgers.md) | Immutable business ledgers | Accepted |
| [ADR-017](0017-three-projects-flat-features.md) | Three projects, flat features, walls only where a breach is expensive | Accepted |
| [ADR-018](0018-account-recovery-methods.md) | Account recovery is a set of pluggable proofs, not one flow | Accepted |
| [ADR-019](0019-language-owns-direction-and-ui-only-translation.md) | The language owns the direction, and only the UI is translated | Accepted |
| [ADR-020](0020-screen-shape-and-interface-standards.md) | A screen is five bands, and the standards each is held to | Accepted |
| [ADR-021](0021-coerced-values-become-absent.md) | A value that does not fit becomes absent, never a substitute | Accepted |
| [ADR-022](0022-raw-capture-is-personal-data-with-an-expiry.md) | Raw provider capture is a required facility, and it is personal data | Accepted |
| [ADR-023](0023-star-is-a-wire-format-not-our-vocabulary.md) | STAR is a wire format to translate from, not our internal vocabulary | Accepted |
| [ADR-024](0024-compliance-is-baseline-pack-and-posture.md) | Compliance splits three ways: product baseline, jurisdiction pack, deployment posture | Accepted |

## ADR file template

```markdown
# ADR-NNN — Title
Date:
Status:
Supersedes:

## Context
## Decision
## Alternatives considered
## Consequences
## Validation / review trigger
```
