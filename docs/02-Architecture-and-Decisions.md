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

Integration is a sibling of business capabilities rather than a layer beneath them. It owns external protocols, credentials, mapping, inbox/outbox processing, checkpoints, and connector health—not dealership business rules.

The path in the original text was `src/Integrations/`, from when this was its own project. ADR-017 collapsed the tree to three projects, so it is now `src/App/Integrations/` — a flat capability folder like every other. **The decision is unchanged; only the address is.**

### ADR-008 — Module contracts and durable events — Accepted

Synchronous lookups use narrow published interfaces. Cross-module state changes use durable events written through a transactional outbox. No module reads another module’s tables or depends on its data/service implementation.

### ADR-009 — Browser sessions and API tokens — Accepted

The React browser uses a backend-for-frontend session in Secure, HttpOnly, SameSite cookies with CSRF protection. OAuth/OIDC bearer tokens are used for machine integrations and public APIs. Local identity, MFA and OIDC federation are all in scope. Global administrators require MFA.

> **Corrected 2026-08-15.** This paragraph read "ASP.NET Core Identity supplies
> local identity", which §4 of this same document has contradicted since
> 2026-08-14: the project uses `Microsoft.Extensions.Identity.Core` for
> `IPasswordHasher<T>` **only**, and users, roles, sessions, TOTP and audit are
> its own code in `src/Identity`. A document that disagrees with itself is worse
> than one that is merely out of date, because both halves look authoritative.
> §4 was right and is now the only statement of it.

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

### ADR-017 — Three projects, flat features, walls only where a breach is expensive — Accepted

Supersedes ADR-002. `src/Core`, `src/Identity`, `src/App` and no others; a new capability is a flat folder, not a project. See [`adr/0017`](adr/0017-three-projects-flat-features.md).

### ADR-018 — Account recovery is a set of pluggable proofs, not one flow — Accepted

Somebody who cannot sign in proves the account is theirs through one of several configurable methods — authenticator, passkey, email, WhatsApp/SMS — with a privileged manager-issued code as the backstop. A method that is not configured is not offered. See [`adr/0018`](adr/0018-account-recovery-methods.md).

### ADR-019 — The language owns the direction, and only the UI is translated — Accepted

Text direction derives from the chosen language rather than being separately selectable, and translation covers the interface only — dealership records are never translated. See [`adr/0019`](adr/0019-language-owns-direction-and-ui-only-translation.md).

### ADR-020 — A screen is five bands, and the standards each is held to — Accepted

Every dealership screen is one route built from five ordered bands, and a record detail is a band rather than a second route. See [`adr/0020`](adr/0020-screen-shape-and-interface-standards.md).

### ADR-021 — A value that does not fit becomes absent, never a substitute — Accepted

External data that cannot be stored as received is recorded as absent with the raw text kept as a mapping warning. Never a clamped bound, a zero, or a sentinel date — those are indistinguishable from real values downstream. See [`adr/0021`](adr/0021-coerced-values-become-absent.md).

### ADR-022 — Raw provider capture is a required facility, and it is personal data — Accepted

An integration that cannot show what a provider actually sent cannot be operated, so raw capture is part of the runtime — with a default retention in code, redaction of the body rather than only the header, and the same erasure obligations as any other personal data. See [`adr/0022`](adr/0022-raw-capture-is-personal-data-with-an-expiry.md).

### ADR-023 — STAR is a wire format to translate from, not our internal vocabulary — Accepted

`ContractFields` keeps its own names. The industry standard, STAR, is a nested XML message format whose shape a flat field map cannot carry, so adopting its names without its structure would imply an interoperability nobody has built. A STAR connector translates at the edge like any other; STAR's role internally is as the coverage checklist a contract is measured against. Settles open decision D3. See [`adr/0023`](adr/0023-star-is-a-wire-format-not-our-vocabulary.md).

### ADR-024 — Compliance splits three ways: product baseline, jurisdiction pack, deployment posture — Accepted

There is no single jurisdiction dial. A **baseline** is what every deployment must do and cannot switch off (encryption, MFA, audit, retention machinery, erasure, export). A **pack** is one jurisdiction's *data* — rates, boundaries, fee caps, taxability flags, effective dates — versioned, sourced, and reviewed before it is marked Supported; a pack carries data and declarations, never logic. A **posture** is what the dealership itself chooses. Tax is resolved from the registration address recorded on the deal, never from an IP address or a browser locale, and the charged tax is frozen on the deal as evidence with its pack version and provenance — including `entered-by-person`, which is what makes an unsupported jurisdiction a label rather than an error. Settles open decision D2. See [`adr/0024`](adr/0024-compliance-is-baseline-pack-and-posture.md).

### ADR-025 — Every list returns one page type, and a limit without an offset is a defect — Accepted

`Page<T>` in Core carries `rows`, `total`, `offset` and `limit`, and every list endpoint returns it. Eleven capabilities each had their own clamp, two of the copies disagreed about the cap, and only one list had an offset — so ten screens honestly said "showing the first 50" and offered no way to reach the rest. **`total` is counted over the same filters as the page**, which is what turns "there may be more" into "51 of 5,100". **The order is part of the query and must be total**: skipping an unordered set lets the database return one row on two pages and another on none, so every paged query carries a tiebreaker. Filters that a page is taken against must be expressed in the query rather than applied to the rows afterwards — receivables filtered "still owed" in memory, which cannot be paged at all. Offset paging, not keyset: the row counts are a dealership's, and the screens are page-numbered.

## 4. Technology stack

Versions follow supported LTS/current stable releases and are pinned centrally. Upgrades require compatibility tests, not a new ADR unless the technology changes.

### Backend

**Adopted** means it is referenced by the solution today. **Selected** means it is
the intended choice and nothing uses it yet. The distinction is kept because a
table that mixes the two describes a system nobody can find — verified against
`Directory.Packages.props` and the project files on 2026-08-14.

| Concern | Choice | State |
|---|---|---|
| Runtime/API | .NET 10, ASP.NET Core minimal APIs, OpenAPI | **Adopted** |
| Persistence | EF Core, SQL Server 2022 | **Adopted** |
| Credentials | `Microsoft.Extensions.Identity.Core` for `IPasswordHasher<T>` **only**. Users, roles, sessions, TOTP and audit are this project's own code in `src/Identity` — not ASP.NET Core Identity, and there is no `UserManager` or `IdentityDbContext` | **Adopted** |
| Browser auth | Durable server-side sessions, HttpOnly cookie, anti-forgery header on writes | **Adopted** |
| Federation | OIDC/OAuth for company sign-in | **Selected.** Not built — it needs a real identity provider to test against, and a fake one proves nothing |
| Validation | Guard clauses in entity constructors, returning `Result` for expected failures | **Adopted.** FluentValidation was listed here and was never taken |
| Resilience | Per-connector retry budgets and poll deadlines (`PollBudget`) | **Adopted** for the integration edge. Polly was listed here and was never taken |
| Jobs | Quartz.NET with a durable SQL store | **Selected.** The only background work today is one in-process `BackgroundService` for CSV import, which is why nothing runs on a schedule |
| Cache/coordination | in-process single-node | **Adopted.** Redis for scale-out is **selected** (ADR-005) and not wired |
| Telemetry | OpenTelemetry and Serilog | **Adopted** |
| Documents | `IDocumentStore`; server-rendered HTML with a print stylesheet | **Adopted.** No PDF library — decided 2026-08-06 rather than deferred, because a renderer must clear both an AGPL licence review and an advisory history. `RenderedDocument` carries a content type so one can be added later without touching callers |
| Testing | xUnit, FluentAssertions, NetArchTest, real SQL via `WebApplicationFactory` | **Adopted.** Testcontainers was listed here and was never taken — CI supplies a SQL service container instead |
| CLI | — | Spectre.Console was listed here and was never taken. There is no CLI |

### Frontend

Same two words, and the gap between them was wider here than anywhere else in
this document: the row below listed six libraries the frontend has never had.
Verified against `frontend/package.json` on 2026-08-15.

| Concern | Choice | State |
|---|---|---|
| Runtime | React 19, TypeScript, Vite | **Adopted** |
| Routing | React Router 8 (`react-router`; the DOM bindings moved into the main package) | **Adopted** |
| Components | none — plain elements against one hand-written stylesheet, `frontend/src/theme/app.css` | **Adopted.** Material UI and MUI DataGrid were listed here and were never taken |
| Data fetching | one `fetch` wrapper, `shared/api.ts`, which every call goes through | **Adopted.** TanStack Query was listed here and was never taken |
| Forms | controlled components and the server's own validation refusals | **Adopted.** React Hook Form and Zod were listed here and were never taken |
| API types | `shared/contracts.ts`, hand written and mirroring `src/App/**/I<Feature>.cs` | **Adopted.** A **generated OpenAPI client was listed here and does not exist** — the file says so in its own header, and changing a server record means changing it in the same commit |
| Translation | six catalogues, no library; English is the schema, so a missing key fails `npm run typecheck` (ADR-019) | **Adopted** |
| Testing | Vitest, Testing Library, jsdom | **Adopted** |
| QR codes | `qrcode.react` — the one UI dependency, for authenticator enrolment | **Adopted** |

Four runtime dependencies in total. That is a deliberate position and not an
unfinished one: the whole product surface is served by React, React Router and
one QR renderer, which is what keeps `npm audit` quiet and the bundle at roughly
210 kB gzipped.

WCAG 2.2 AA is required; ADR-020 records which parts of it are met and which are
not.

### Deployment

Supported packages are Windows/IIS or Windows service and Linux OCI container/Compose. A small installation requires the application, SQL Server, and document storage. Redis, Kubernetes, Seq, and cloud services are optional.

## 5. Extraction rule

A module may be proposed for service extraction only when it:

1. has a stable versioned contract and owns its data;
2. already tolerates event delay and duplicate delivery;
3. needs independent scaling, deployment, failure containment, or compliance isolation;
4. has measured evidence that the benefit exceeds the on-prem operational cost.

Until all four are true, it remains in the monolith.
