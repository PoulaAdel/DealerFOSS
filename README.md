# DealerFOSS

**FOSS — Free and Open-Source Software.** DealerFOSS is a Dealer Management System for independent dealers and dealer groups, licensed under AGPLv3 with optional commercial terms.

The name is a commitment rather than a label. A dealership's records are its own: they can be exported at any time as ordinary files that this same system reads straight back in, so leaving costs nothing but the decision. That is asserted by a round-trip test, not promised in a paragraph.

A **tenant is a dealer organization** and may contain one or many rooftops. DealerFOSS is designed as a readable modular monolith that a dealership IT administrator can operate on premises, while leaving a disciplined path to a hosted offering.

## Product path

- **Coexistence release:** run beside an incumbent DMS, unify dealer data and workflows, and prove migration, synchronization, reconciliation, security, multi-rooftop access, backup, and export.
- **Standalone path:** become authoritative for variable operations, fixed operations, and accounting only after each subsystem passes parallel-operation and reconciliation gates.

The project does not call a customer/vehicle/deal UI a complete DMS. Accounting, Parts, deep Service, F&I compliance, tax/title, communications, and document evidence are explicit parts of the standalone plan.

## Current state

**[Where we are](docs/PROGRESS.md)** — plain language: how far along the project is, what works today, and what does not exist yet.

**[Implementation status](docs/implementation/STATUS.md)** — the engineering detail, where every completed item names the command that proves it.

## New here?

Start with **[Onboarding](docs/ONBOARDING.md)** — a 30-minute path that reads the
code before the documents, gets the app running, and lists the traps worth knowing.
Then [Local Development](docs/LOCAL-DEVELOPMENT.md) for prerequisites, the
canonical commands, and the environment facts that are easy to get wrong.

**Running an installation rather than changing it?** [Operating](docs/OPERATING.md)
is the runbook: first start, setting a dealership up, backups, and what to check
when something is wrong.

## Repository map

Every directory has one job. You should not need the docs to know where something lives.

```text
src/
├── Core/          shared types everything uses: Result, Money, Ids, Clock, and the
│                  interfaces. No database, no web, no dealership concepts. Flat.
├── Identity/      users, roles, permissions, sessions, audit. Its own project so
│                  its tables are unreachable from the rest of the application.
└── App/           the application — startup, plumbing, and every capability
    ├── Program.cs      where everything is wired together
    ├── Tenancy/        finds the right dealer database for a request
    ├── Data/           the tenant database and its migrations
    │
    ├── Organization/   dealer organization → legal entity → rooftop → department
    ├── Customers/      the people and businesses the dealership deals with
    ├── Vehicles/       vehicles as identities — VIN, year, make, model
    ├── Inventory/      a vehicle on a lot, with a status and a cost
    ├── Leads/          enquiries, and following them up
    ├── Deals/          selling a car: pricing, trade-ins, charges, approval
    ├── Finance/        the products sold alongside a car — warranties, plans
    ├── RepairOrders/   the workshop, and the service booking diary
    ├── Parts/          the parts catalogue, and stock on each rooftop's shelf
    ├── Accounting/     accounts, journals, and closing a month
    ├── Documents/      printable paperwork
    ├── Reporting/      dashboards. Owns no data; composes other capabilities
    ├── DataMigration/  bringing records in from a file, and taking them out
    ├── Integrations/   the edge where another system's data arrives
    └── Administration/ the control plane, for whoever runs the installation

tests/
├── Unit/          domain rules, no infrastructure needed
├── Integration/   drives the real app against a real database
└── Architecture/  rules about what may reference what; fails the build on a breach

deploy/            docker compose for local services, and the end-to-end check script
docs/              the engineering workbook (design decisions and specifications)
.github/           CI workflow, contributing guide, security policy, code of conduct
```

**Why only three projects.** A separate project is a wall the compiler enforces,
and walls have a cost paid on every change. Two are worth it: `Core` must never
learn about a database, and `Identity` decides who may see what — so its tables
and services are `internal`, and no other code can write a user row or an audit
row except through `IAccessDirectory` and `IAuthenticator`. Every other boundary
is held by architecture tests instead, which fail the build just as hard
([ADR-017](docs/adr/0017-three-projects-flat-features.md)).

**Inside a capability**, one flat folder with files named for their job:
`Customer.cs` (the rules), `CustomerService.cs` (what you can do),
`CustomerEndpoints.cs` (the HTTP surface), `CustomerTables.cs` (how it is stored),
and `ICustomers.cs` (what other capabilities may call).

## Engineering workbook

Start with the [workbook index](docs/00-Workbook.md).

| # | Document |
|---|---|
| 01 | [Vision & Scope](docs/01-Vision-and-Scope.md) |
| 02 | [Architecture & Decisions](docs/02-Architecture-and-Decisions.md) |
| 03 | [Project Structure](docs/03-Project-Structure.md) |
| 04 | [Data & Tenancy](docs/04-Data-and-Tenancy.md) |
| 05 | [Integration Framework](docs/05-Integration-Framework.md) |
| 06 | [Security & API](docs/06-Security-and-API.md) |
| 07 | [Delivery Roadmap](docs/07-Delivery-Roadmap.md) |
| 08 | [Governance & Standards](docs/08-Governance-and-Standards.md) |

See also the [ADR index](docs/adr/README.md) and [visual diagrams](docs/diagrams/README.md).

## Architecture at a glance

- One deployable ASP.NET Core modular monolith.
- One SQL Server database per dealer organization, containing one or many rooftops.
- Capability-first folders; compiler walls only where a breach would be expensive,
  architecture tests everywhere else.
- Versioned integration contracts, durable inbox/outbox processing, replay, and reconciliation.
- Secure browser sessions, scoped multi-rooftop authorization, MFA, immutable audit/evidence, and protected uploads.
- Windows and Linux-container packages; Redis is optional until the application is scaled out.
- Live operational views plus isolated reporting projections with visible freshness.

Accepted decisions may change through dated ADRs when implementation evidence requires it; they are not silently edited or treated as immune to learning.
