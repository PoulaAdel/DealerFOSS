# 03 — Project Structure

← [Architecture & Decisions](02-Architecture-and-Decisions.md) · Next: [Data & Tenancy](04-Data-and-Tenancy.md)  
Visual: [Project structure](diagrams/02-project-structure.md)

## 1. Organizing rule

The repository is capability-first. A contributor looking for Sales, Parts, or Service starts in that module and finds its business model, workflows, persistence, endpoints, and tests nearby. Small modules stay flat; larger modules use the same small set of internal folders. This keeps the original at-a-glance navigation without forcing a mature DMS into one giant folder.

## 2. Top-level layout

```text
OpenDealer360/
├── src/
│   ├── Host/                 startup, middleware, composition, background workers
│   ├── Platform/             small shared kernel and infrastructure abstractions
│   ├── Modules/
│   │   ├── Organization/
│   │   ├── Identity/
│   │   ├── Customers/
│   │   ├── Vehicles/
│   │   ├── Inventory/
│   │   ├── Crm/
│   │   ├── Sales/
│   │   ├── Finance/
│   │   ├── Service/
│   │   ├── Parts/            added on the standalone path
│   │   ├── Accounting/       added on the standalone path
│   │   ├── Documents/
│   │   └── Reporting/
│   ├── Integrations/
│   └── Cli/
├── frontend/
├── tests/
├── deploy/
└── docs/
```

Tax/title, communications, and compliance begin as cohesive features in their owning modules. They become separate modules only when they acquire independent data ownership and workflows; speculative empty modules are forbidden.

## 3. Module layout

A small module may remain:

```text
Modules/Customers/
├── Customer.cs
├── CustomersService.cs
├── ICustomerDirectory.cs
├── CustomersData.cs
├── CustomersEndpoints.cs
├── CustomersDtos.cs
└── CustomersModule.cs
```

When a module no longer scans comfortably—normally more than about 20 files or several independent workflows—it adopts:

```text
Modules/Sales/
├── Domain/                  Deal, TradeIn, rules, state transitions
├── Features/
│   ├── CreateDeal/
│   ├── DeskDeal/
│   ├── ApproveDeal/
│   └── UnwindDeal/
├── Data/                    EF configuration, repositories/queries, migrations
├── Contracts/               public interfaces, commands/events, response models
├── SalesEndpoints.cs
└── SalesModule.cs
```

The permitted folders have stable meanings:

| Area | Responsibility | May depend on |
|---|---|---|
| Domain | entities, value objects, invariants, state transitions | `Platform/Kernel` only |
| Features | commands, queries, handlers, validation | Domain and published contracts |
| Data | EF configuration, storage implementations, projections | its module and EF Core |
| Contracts | narrow cross-module interfaces and versioned events | shared primitives only |
| Endpoints | authorization, transport mapping, delegation | Features and API DTOs |

## 4. Platform boundary

`Platform/` contains only capabilities broadly required by modules:

```text
Platform/
├── Kernel/          Result, Money, EntityId, Clock abstractions
├── Persistence/     tenant context factory, transaction/outbox support
├── Security/        authentication/authorization primitives, encryption interfaces
├── Jobs/            durable scheduling abstractions
├── Observability/   telemetry conventions and correlation
├── Storage/         IDocumentStore
└── Utilities/       domain-free helpers only
```

Business concepts such as Deal, RepairOrder, Rooftop, TaxRule, or Journal never enter Platform. Shared code must have at least two real consumers; “might be reused later” is insufficient.

## 5. Boundary enforcement

CI verifies:

- Domain code has no references to ASP.NET Core, EF Core, connector, or endpoint types.
- A module references another module only through that module’s `Contracts` namespace/assembly.
- Modules cannot read another module’s EF types or schema.
- Integrations call published module contracts and cannot reference module internals.
- Cross-module state changes use the transactional outbox; no handler assumes an external side effect is in the same transaction.
- There are no circular module dependencies.
- Public contracts and database migrations pass compatibility tests.

Important modules may be separate projects to make these compiler errors. Smaller modules may share a project while architecture tests enforce namespaces. The threshold is readability and boundary safety, not architectural fashion.

## 6. Frontend

```text
frontend/src/
├── app/                    router, providers, authenticated shell
├── features/
│   ├── organization/
│   ├── customers/
│   ├── sales/
│   ├── service/
│   └── ...
├── shared/                 generated API client, reusable accessible UI
├── print/                  print preview and local print-agent integration
└── theme/
```

Each route declares its organization/rooftop context. UI permission checks improve usability but never replace server authorization.

## 7. Naming

- Modules and folders use dealership capability names.
- Commands are verbs (`CreateDeal`); queries describe returned data (`GetInventoryAging`); events use past tense (`DealApprovedV1`).
- Domain types use plain names (`Deal`, `Customer`).
- External contracts include their version in namespace or type.
- IDs identify their scope (`RooftopId`, `LegalEntityId`, `ExternalSystemId`).
- Files may be split when that improves reading; arbitrary “one file per role” limits are prohibited.
