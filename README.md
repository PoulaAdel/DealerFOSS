# OpenDealer360

OpenDealer360 is an open-source Dealer Management System for independent dealers and dealer groups, licensed under AGPLv3 with optional commercial terms.

A **tenant is a dealer organization** and may contain one or many rooftops. OpenDealer360 is designed as a readable modular monolith that a dealership IT administrator can operate on premises, while leaving a disciplined path to a hosted offering.

## Product path

- **Coexistence release:** run beside an incumbent DMS, unify dealer data and workflows, and prove migration, synchronization, reconciliation, security, multi-rooftop access, backup, and export.
- **Standalone path:** become authoritative for variable operations, fixed operations, and accounting only after each subsystem passes parallel-operation and reconciliation gates.

The project does not call a customer/vehicle/deal UI a complete DMS. Accounting, Parts, deep Service, F&I compliance, tax/title, communications, and document evidence are explicit parts of the standalone plan.

## Current state

**[Implementation status](docs/implementation/STATUS.md)** — what is built, what is proven, and what is next. Every completed item names the command that proves it.

## New here?

Start with **[Onboarding](docs/ONBOARDING.md)** — a 30-minute path that reads the
code before the documents, gets the app running, and lists the traps worth knowing.
Then [`CLAUDE.md`](CLAUDE.md) for the verified local setup and canonical commands.

## Repository map

Every directory has one job. You should not need the docs to know where something lives.

```text
src/
├── Host/          the ASP.NET Core application — startup, middleware, wiring
├── Core/          shared types every module uses: Result, Money, Ids, Clock, and
│                  the interfaces modules depend on. No database, no web, no domain.
├── Tenancy/       finds the right dealer database for a request: the host catalog
│                  of tenants, the resolver, and its cache.
└── Modules/       the business capabilities — one folder each, self-contained
    ├── Organization/   dealer organization → legal entity → rooftop → department
    └── Identity/       users, roles, permissions, who-can-see-which-rooftop, audit

tests/
├── Architecture/  rules about what may reference what; fails the build on a breach
└── Integration/   drives the real app against a real database

deploy/            docker compose for local services, and the end-to-end check script
docs/              the engineering workbook (design decisions and specifications)
.github/           CI workflow, contributing guide, security policy, code of conduct
```

**Why `Core` and `Tenancy` are separate:** `Core` is deliberately free of Entity
Framework so business rules can never depend on the database — an architecture
test enforces it. `Tenancy` is where that database dependency is allowed to live.

**Inside a module**, small ones stay flat and larger ones use only what they need:
`Domain/` (rules), `Data/` (tables and queries), `Contracts/` (what other modules
may call), and `…Endpoints.cs` (the HTTP surface). A module is the only code that
touches its own tables.

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
- Capability-first modules with compiler, architecture-test, and schema boundaries.
- Versioned integration contracts, durable inbox/outbox processing, replay, and reconciliation.
- Secure browser sessions, scoped multi-rooftop authorization, MFA, immutable audit/evidence, and protected uploads.
- Windows and Linux-container packages; Redis is optional until the application is scaled out.
- Live operational views plus isolated reporting projections with visible freshness.

Accepted decisions may change through dated ADRs when implementation evidence requires it; they are not silently edited or treated as immune to learning.
