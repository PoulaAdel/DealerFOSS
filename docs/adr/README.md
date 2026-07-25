# Architecture Decision Records

The authoritative decision summaries are in [02 — Architecture & Decisions](../02-Architecture-and-Decisions.md). Each accepted decision will be copied to an immutable one-topic ADR file when implementation begins.

Statuses are **Proposed**, **Accepted**, **Superseded**, or **Rejected**. A material change creates a new dated ADR that supersedes the old one; it never silently rewrites history.

| ADR | Decision | Status |
|---|---|---|
| [ADR-001](../02-Architecture-and-Decisions.md#adr-001--modular-monolith--accepted) | Modular monolith | Accepted |
| [ADR-002](../02-Architecture-and-Decisions.md#adr-002--capability-first-modules-with-limited-internal-structure--accepted) | Capability-first modules with limited internal structure | Accepted |
| [ADR-003](../02-Architecture-and-Decisions.md#adr-003--database-per-dealer-organization--accepted) | Database per dealer organization | Accepted |
| [ADR-004](../02-Architecture-and-Decisions.md#adr-004--live-operational-views-and-isolated-reporting-projections--accepted) | Live operational views and isolated reporting projections | Accepted |
| [ADR-005](../02-Architecture-and-Decisions.md#adr-005--redis-optional-for-single-node-deployments--accepted) | Redis optional for single-node deployments | Accepted |
| [ADR-006](../02-Architecture-and-Decisions.md#adr-006--versioned-integration-contracts--accepted) | Versioned integration contracts | Accepted |
| [ADR-007](../02-Architecture-and-Decisions.md#adr-007--integration-is-a-platform-edge--accepted) | Integration is a platform edge | Accepted |
| [ADR-008](../02-Architecture-and-Decisions.md#adr-008--module-contracts-and-durable-events--accepted) | Module contracts and durable events | Accepted |
| [ADR-009](../02-Architecture-and-Decisions.md#adr-009--browser-sessions-and-api-tokens--accepted) | Browser sessions and API tokens | Accepted |
| [ADR-010](../02-Architecture-and-Decisions.md#adr-010--document-storage-behind-idocumentstore--accepted) | Document storage behind `IDocumentStore` | Accepted |
| [ADR-011](../02-Architecture-and-Decisions.md#adr-011--one-folderproject-per-connector--accepted) | One folder/project per connector | Accepted |
| [ADR-012](../02-Architecture-and-Decisions.md#adr-012--compiled-connector-discovery--accepted) | Compiled connector discovery | Accepted |
| [ADR-013](../02-Architecture-and-Decisions.md#adr-013--agplv3-with-optional-commercial-license--accepted) | AGPLv3 with optional commercial license | Accepted |
| [ADR-014](../02-Architecture-and-Decisions.md#adr-014--boundaries-enforced-in-three-ways--accepted) | Compiler, architecture-test, and schema boundary enforcement | Accepted |
| [ADR-015](../02-Architecture-and-Decisions.md#adr-015--sql-server-first-cross-platform-application--accepted) | SQL Server first, cross-platform application | Accepted |
| [ADR-016](../02-Architecture-and-Decisions.md#adr-016--immutable-business-ledgers--accepted) | Immutable business ledgers | Accepted |

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
