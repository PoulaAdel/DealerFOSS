# 03 — Project Structure

← [Architecture & Decisions](02-Architecture-and-Decisions.md) · Next: [Data & Tenancy](04-Data-and-Tenancy.md)  
Visual: [Project structure](diagrams/02-project-structure.md)

## 1. Organizing rule

**A wall goes where a breach would be expensive. Everywhere else, a test.**

Three backend projects, and inside the application one flat folder per dealership
capability. A contributor looking for Customers opens `src/App/Customers/` and
sees every file that feature owns, named for what it does. Nothing is nested more
than it must be ([ADR-017](adr/0017-three-projects-flat-features.md)).

Two boundaries are compiler-enforced, because breaching either is a security or
correctness incident:

- **`Core`** must never learn about a database, or business rules become
  untestable and coupled to storage.
- **`Identity`** owns who may see what. Its tables and services are `internal`,
  so application code cannot write a user row or an audit row except through the
  handful of contracts it exports. **That list is asserted by
  `BoundaryTests`, which is the authority** — do not maintain a second copy of it
  here or anywhere else. Read the test: each entry carries a comment saying why
  exporting it was a security decision rather than a convenience.

Every other boundary is held by architecture tests, which fail the build on a
breach. Accounting will earn a compiler wall of its own when it lands, for the
same reason Identity has one.

## 2. Top-level layout

```text
DealerFOSS/
├── src/
│   ├── Core/         Result, Money, Ids, Clock, AuditableEntity, and the
│   │                 interfaces everything depends on. No EF, no ASP.NET. Flat.
│   ├── Identity/     users, roles, permissions, sessions, audit, passkeys, and
│   │                 the control plane. Internals sealed; the exported list is
│   │                 asserted by tests/Architecture/BoundaryTests.cs.
│   └── App/          the application — everything else
│       ├── Program.cs          composition root
│       ├── AuthEndpoints.cs    sign in, sign out, who am I, passkeys, recovery
│       ├── ProblemResults.cs   the one Error → HTTP mapping
│       ├── Tenancy/            host catalog, tenant resolution, middleware
│       ├── Data/               TenantDb + Migrations/
│       ├── Organization/       dealer organization → legal entity → rooftop → department
│       ├── Customers/          people and businesses the dealership deals with
│       ├── Vehicles/           vehicles as identities — VIN, year, make, model,
│       │                       and the public safety-recall lookup
│       ├── Inventory/          a vehicle on a rooftop's lot, with a status and a cost
│       ├── Leads/              enquiries being worked at a rooftop
│       ├── Deals/              one customer buying one car, priced and approved
│       ├── Finance/            what was sold alongside the car, and what it made
│       ├── RepairOrders/       the workshop and the booking diary
│       ├── Parts/              catalogue, stock receipts, costed issue to a job
│       ├── Accounting/         the ledger behind a delivered sale or a service invoice
│       ├── Documents/          IDocumentStore and the printable paperwork
│       ├── Reporting/          the month in review, and stock aging
│       ├── DataMigration/      importing and exporting a dealership's records
│       ├── Integrations/       the connector edge (ADR-007)
│       └── Administration/     the control plane's own HTTP surface
├── tests/
│   ├── Unit/           domain rules, no infrastructure
│   ├── Integration/    the real app against a real database
│   └── Architecture/   what may reference what; fails the build on a breach
├── deploy/
└── docs/
```

Verified against the tree on 2026-08-15. The list above was eight folders short
for about a week — it named Accounting as the last capability while six more had
landed — which is the failure mode of writing a tree by hand. **If you are asking
"what capabilities exist", `ls src/App` is the authority and this is a
description of it.**

Further capabilities arrive as sibling folders inside `App/`. Speculative empty
folders are forbidden. Tax/title, communications, and compliance begin as
features inside their owning capability and separate only when they acquire
independent data ownership and workflows.

**Why Vehicles and Inventory are separate folders but one schema.** They answer
different questions at different scopes: a vehicle is organization-shared and
answers "what car is this?", while an inventory unit is rooftop-owned and answers
"whose lot is it on, and what state is it in?". That scope difference is a
permission boundary and deserves to be visible. They share the `vehicles` schema
because nearly every read joins them ([doc 04 §1](04-Data-and-Tenancy.md)).

## 3. Feature layout

One flat folder per capability, with files named for their role:

```text
App/Customers/
├── Customer.cs           the records and their rules — no EF, no ASP.NET
├── ContactPoint.cs
├── Address.cs
├── CustomerService.cs    what you can do, and the permission checks
├── CustomerEndpoints.cs  the HTTP surface
├── CustomerTables.cs     EF configuration and the schema this feature owns
└── ICustomers.cs         what other features may call
```

The roles have stable meanings:

| File | Responsibility | May depend on |
|---|---|---|
| `<Entity>.cs` | entities, value objects, invariants, state transitions | `Core` only |
| `<Feature>Service.cs` | workflows, authorization, validation | its own entities, `TenantDb`, other features' `I<Feature>` |
| `<Feature>Endpoints.cs` | transport mapping and delegation | its own service and contracts |
| `<Feature>Tables.cs` | EF configuration; declares the schema the feature owns | its own entities and EF Core |
| `I<Feature>.cs` | the narrow cross-feature interface and its read models | `Core` only |

A subfolder appears only when a folder genuinely stops scanning comfortably —
roughly fifteen files, or several independent workflows. It is earned, never
applied pre-emptively.

## 4. Core boundary

`Core/` contains only what everything broadly requires. It is flat — the project
*is* the shared kernel, so it needs no inner folders:

```text
Core/                    types and abstractions only — no EF, no ASP.NET, no domain
├── Result.cs            typed success/failure for expected business outcomes
├── Error.cs             stable application error codes
├── Money.cs             amount plus ISO currency; cross-currency maths refused
├── Ids.cs               DealerOrganizationId, LegalEntityId, RooftopId, DepartmentId
├── Clock.cs             IClock, so "now" is injectable and testable
├── AuditableEntity.cs   audit columns and the concurrency stamp
├── ITenantContext.cs    the tenant resolved for this request
├── ICurrentUser.cs      the caller resolved for this request
├── IAuditSink.cs        how a security-sensitive event is recorded
└── ISecretProtector.cs  how sensitive configuration is protected at rest
```

Business concepts such as Deal, RepairOrder, Rooftop, TaxRule, or Journal never
enter `Core`. Shared code must have at least two real consumers; "might be reused
later" is insufficient.

## 5. Persistence

Four contexts, not one per capability:

| Context | Lives in | Holds | Physical database |
|---|---|---|---|
| `HostDb` | `App/Tenancy/` | which dealer organization lives in which database | host catalog |
| `ControlPlaneDb` | `Identity/` | administrators, their sessions, and support-access grants — `internal` | host catalog, `control` schema |
| `IdentityDb` | `Identity/` | users, roles, sessions, audit — `internal` | each tenant, `identity` schema |
| `TenantDb` | `App/Data/` | one dealer's business data across every feature | each tenant |

`ControlPlaneDb` is the fourth and is easy to miss because it shares a *file
tree* with `IdentityDb` and a *database* with `HostDb`. It is separate from both
on purpose: whoever runs the installation is not a dealership user, and putting
their credentials in a tenant database would mean a tenant restore could resurrect
an administrator. See [`LOCAL-DEVELOPMENT.md`](LOCAL-DEVELOPMENT.md#migrations)
for what that costs when generating a migration.

`TenantDb` knows no table names. Each feature contributes an
`IEntityTypeConfiguration<T>` in its own `<Feature>Tables.cs`, including the
schema it owns (`org`, `customers`, `vehicles`), and `TenantDb` collects them with
`ApplyConfigurationsFromAssembly`. Two behaviours live centrally in `TenantDb`
because forgetting either is a silent data-integrity failure: audit columns and
the concurrency stamp are set on save, and anything marked `IAppendOnly` is
refused any update or delete. The marker is why a history table added later is
protected by implementing an interface rather than by somebody remembering to
extend a guard.

`IdentityDb` stays separate even though it lives in the same physical database.
Merging it would hand every feature a `DbSet<User>`, undoing the wall that is the
whole reason `Identity` is its own project.

## 6. Boundary enforcement

`tests/Architecture` verifies, on every build:

- `Core` has no dependency on ASP.NET Core or EF Core, and knows about no feature.
- `Identity` has no dependency on the application or any feature.
- `Identity` exports only its access and sign-in contracts — the list is asserted,
  so widening it is a deliberate decision.
- No feature reaches into another feature. `Inventory` may see `Vehicles`, and
  `Leads` may see the `ICustomers` and `IVehicles` contracts — but not the entity
  types behind them, which are named individually in the rule.
- Entities — anything inheriting `AuditableEntity` — have no EF or ASP.NET
  dependency, wherever the file sits.
- Tenancy knows about no business feature.
- No feature builds its own `DbContextOptionsBuilder`, which would let it choose a
  connection and escape the resolved tenant.

Still to come as the matching subsystems land: integrations calling only published
contracts, the transactional outbox, and public-contract/migration compatibility
tests.

## 7. Frontend

```text
frontend/src/
├── app/                    router, providers, both authenticated shells
├── features/               one folder per area, mirroring src/App where it can
│   ├── auth/               sign in, second factor, recovery, passkeys
│   ├── admin/              the control-plane console — a separate shell
│   ├── customers/  deals/  inventory/  leads/  parts/  service/
│   ├── accounting/ dashboard/ migration/ staff/ vehicles/
├── shared/                 api.ts, contracts.ts, i18n/, reusable accessible UI
├── test/                   the render wrapper and the fetch stub
└── theme/                  app.css — the whole stylesheet
```

Two differences from the original sketch, both real: **`shared/` holds a
hand-written `contracts.ts`, not a generated client** (doc 02 §4), and there is
**no `print/` folder** — printable paperwork is server-rendered HTML from
`src/App/Documents`, opened in a tab with a print stylesheet, so the browser
needs no print code of its own (ADR-010).

Each route declares its organization/rooftop context. UI permission checks improve
usability but never replace server authorization. The shape of a screen is
[ADR-020](adr/0020-screen-shape-and-interface-standards.md)'s five bands.

## 8. Naming

**Never repeat the path in the name.** A file called `Core.csproj` inside
`src/Core/` is obvious; `DealerFOSS.Core.csproj` only makes the tree harder to
scan. Project files, folders, and types are named for the one thing they are.

- Project files carry the bare name: `Core.csproj`, `Identity.csproj`,
  `App.csproj`.
- Assemblies and namespaces keep the `DealerFOSS.` root — those are *global*
  identifiers, and a bare `Core` namespace or `Core.dll` would collide with other
  libraries and read as anonymous in a stack trace. Nothing beyond that root is
  repeated: the namespace is `DealerFOSS.Customers`, not
  `DealerFOSS.App.Customers`, because "App" describes the project, not the code.
- Folders use dealership capability names.
- Files are named for their role, not their layer: `CustomerService.cs`, not
  `Application/Services/CustomerService.cs`.
- Commands are verbs (`CreateDeal`); queries describe returned data
  (`GetInventoryAging`); events use past tense (`DealApprovedV1`).
- Domain types use plain names (`Deal`, `Customer`).
- External contracts include their version in namespace or type.
- IDs identify their scope (`RooftopId`, `LegalEntityId`, `ExternalSystemId`).
- Files may be split when that improves reading; arbitrary "one file per role"
  limits are prohibited.
