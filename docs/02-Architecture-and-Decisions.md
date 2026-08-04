# 02 — Architecture & Decisions

← [Vision & Scope](01-Vision-and-Scope.md) · Next: [Project Structure](03-Project-Structure.md)  
Visual: [System architecture](diagrams/01-system-architecture.md)

## 1. System shape

DealerFOSS is a modular monolith: one deployable ASP.NET Core application containing bounded business modules and an Integration edge. It is simple to operate on one server, while module-owned contracts, schemas, and background jobs prevent the codebase from becoming an undifferentiated monolith.

The tenant boundary is a dealer organization, not a building. One tenant database contains all rooftops belonging to that organization. Rooftop-scoped authorization and data ownership operate inside that database.

## 2. Decision policy

Statuses are **Accepted**, **Proposed**, or **Superseded**. Accepted decisions govern implementation. New evidence changes a decision through a new ADR and coordinated document update; “accepted” does not mean immune to learning.

## 3. Architecture decisions

### ADR-001 — Modular monolith — Accepted

One application is deployed and monitored. Modules communicate in process through public contracts and durable internal events. Microservices are introduced only when measured scaling, failure isolation, independent deployment, or regulatory isolation justifies their operational cost.

### ADR-002 — Capability-first modules with limited internal structure — Accepted

Top-level folders map to dealership capabilities. Small modules remain flat. A module may add `Features/`, `Domain/`, or `Data/` subfolders when file count or distinct workflows make navigation clearer. Folder depth is not a goal; predictable ownership is.

### ADR-003 — Database per dealer organization — Accepted

Each tenant/dealer organization has one database containing one or many rooftops. Tenant business tables do not need `TenantId`; rooftop- or legal-entity-owned records do require `RooftopId` and, where relevant, `LegalEntityId`. The host database stores tenant routing only.

This is optimized for self-hosting, isolation, organization backup/restore, and small-to-mid-sized dealer groups. A pooled hosted database is not designed now; it requires a future ADR backed by operational need.

### ADR-004 — Live operational views and isolated reporting projections — Accepted

Operational screens query indexed transactional data or near-real-time projections. Analytical dashboards and expensive reports read `[rpt]` projections refreshed incrementally. Each report displays its source and freshness. Hourly refresh is allowed only for reports whose documented business use tolerates it.

### ADR-005 — Redis optional for single-node deployments — Accepted

Single-node installations use in-process cache/locks plus durable SQL state. Redis is required only for multiple application nodes or workloads needing distributed coordination. Security-critical revocation and job state remain durable; Redis loss never silently grants privileged access.

### ADR-006 — Versioned integration contracts — Accepted

External data maps to capability-specific, versioned contracts with source, entity ID/version, event time, provenance, and deletion state. Vendor DTOs never enter business modules. Contracts are deliberately richer than the initial database model and evolve additively within a version.

### ADR-007 — Integration is a platform edge — Accepted

`src/Integrations/` is a sibling of business modules. It owns external protocols, credentials, mapping, inbox/outbox processing, checkpoints, and connector health—not dealership business rules.

### ADR-008 — Module contracts and durable events — Accepted

Synchronous lookups use narrow published interfaces. Cross-module state changes use durable events written through a transactional outbox. No module reads another module’s tables or depends on its data/service implementation.

### ADR-009 — Browser sessions and API tokens — Accepted

The React browser uses a backend-for-frontend session in Secure, HttpOnly, SameSite cookies with CSRF protection. OAuth/OIDC bearer tokens are used for machine integrations and public APIs. ASP.NET Core Identity supplies local identity; MFA and OIDC federation are supported. Global administrators require MFA.

### ADR-010 — Document storage behind `IDocumentStore` — Accepted

Filesystem storage is the on-prem default; an S3-compatible implementation may be configured. Metadata, hash, classification, version, retention, and transaction links live in SQL. Uploads are quarantined and scanned before availability.

### ADR-011 — One folder/project per connector — Accepted

Each provider has a dedicated connector folder or project containing auth, transport, vendor DTOs, mapping, capability adapters, and tests. This replaces the one-file rule, which would not remain readable at real vendor scale.

### ADR-012 — Compiled connector discovery — Accepted

Connectors publish a manifest and are discovered during application startup. v1 connectors are compiled and released with the application. Runtime third-party plugins are deferred until signing, compatibility, isolation, and support rules are designed.

### ADR-013 — AGPLv3 with optional commercial license — Accepted

The complete self-hosted core is AGPLv3. Alternative commercial terms support closed embedding and managed offerings. Contributor provenance and dependency licensing are enforced.

### ADR-014 — Boundaries enforced in three ways — Accepted

Project/namespace references provide compiler boundaries, architecture tests detect forbidden references/cycles, and database tests enforce schema ownership. Tests support a readable structure; they do not substitute for one.

### ADR-015 — SQL Server first, cross-platform application — Accepted

SQL Server 2022 is the supported v1 database. The application runs under IIS/Windows service or Kestrel in a Linux container. Application scheduling does not depend on SQL Server Agent. Database portability is a future evidence-based decision, not a claim.

### ADR-016 — Immutable business ledgers — Accepted

Posted accounting entries, parts stock movements, compliance evidence, and audit records are append-only. Corrections use reversals or adjustment records. This prevents invisible history changes and makes reconciliation possible.

## 4. Technology stack

Versions follow supported LTS/current stable releases and are pinned centrally. Upgrades require compatibility tests, not a new ADR unless the technology changes.

### Backend

| Concern | Choice |
|---|---|
| Runtime/API | .NET 10 LTS, ASP.NET Core, OpenAPI |
| Persistence | EF Core, SQL Server 2022 |
| Identity | ASP.NET Core Identity, cookie/BFF for browser, OIDC/OAuth for federation/API |
| Validation | FluentValidation |
| Resilience | Polly |
| Jobs | Quartz.NET with durable SQL store |
| Cache/coordination | in-process single-node; Redis for scale-out |
| Telemetry | OpenTelemetry and Serilog |
| Documents/PDF | `IDocumentStore`; QuestPDF or another AGPL-compatible renderer |
| Testing | xUnit, FluentAssertions, Testcontainers, architecture tests |
| CLI | Spectre.Console |

### Frontend

React + TypeScript + Vite; Material UI; MUI DataGrid; React Router; TanStack Query; React Hook Form + Zod. The generated OpenAPI client is the transport boundary. WCAG 2.2 AA is required.

### Deployment

Supported packages are Windows/IIS or Windows service and Linux OCI container/Compose. A small installation requires the application, SQL Server, and document storage. Redis, Kubernetes, Seq, and cloud services are optional.

## 5. Extraction rule

A module may be proposed for service extraction only when it:

1. has a stable versioned contract and owns its data;
2. already tolerates event delay and duplicate delivery;
3. needs independent scaling, deployment, failure containment, or compliance isolation;
4. has measured evidence that the benefit exceeds the on-prem operational cost.

Until all four are true, it remains in the monolith.
