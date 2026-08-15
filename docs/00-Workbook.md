# DealerFOSS — Engineering Workbook

**Project:** DealerFOSS — Open-Source Dealer Management System  
**License:** AGPLv3, with an optional commercial license  
**Status:** Accepted baseline; changes require an ADR

This workbook is the single source of truth for product scope, architecture, structure, data, integrations, security, delivery, and governance. Decisions are firm enough to build against but may be superseded when implementation evidence proves a better choice. No decision is “locked” against learning.

## Document index

| # | Document | Owns |
|---|---|---|
| — | [Onboarding](ONBOARDING.md) | **start here** — how to read, run, and change the project |
| 01 | [Vision & Scope](01-Vision-and-Scope.md) | product boundaries, users, releases, and competitive path |
| 02 | [Architecture & Decisions](02-Architecture-and-Decisions.md) | system shape, ADRs, and approved technology choices |
| 03 | [Project Structure](03-Project-Structure.md) | code layout, naming, and dependency enforcement |
| 04 | [Data & Tenancy](04-Data-and-Tenancy.md) | dealer organizations, rooftops, databases, data lifecycle, and reporting |
| 05 | [Integration Framework](05-Integration-Framework.md) | external contracts, connectors, synchronization, import, and export |
| 06 | [Security & API](06-Security-and-API.md) | identity, authorization, privacy, audit, and API rules |
| 07 | [Delivery Roadmap](07-Delivery-Roadmap.md) | phases, exit gates, testing, operations, and risks |
| 08 | [Governance & Standards](08-Governance-and-Standards.md) | licensing, contribution, releases, and coding standards |
| 09 | [Implementation Roadmap](09-Implementation-Roadmap.md) | implementation workflow, phase roadmap, completion rules, and progress reporting |
| 11 | [Franchise & External Scope](11-Franchise-and-External-Scope.md) | what a franchised dealer needs that we do not have, and what blocks each piece |
| — | [Progress](PROGRESS.md) | the plain-language account of what works today, for a reader who is not a developer |
| — | [Local Development](LOCAL-DEVELOPMENT.md) | prerequisites, canonical commands, and the environment facts that are easy to get wrong |
| — | [Operating](OPERATING.md) | **for whoever runs the installation** — first start, setting a dealership up, backups, and what to check when something is wrong |

[Architecture decisions](adr/README.md) and [diagrams](diagrams/README.md) are concise companions to these documents. If prose and a diagram disagree, the owning document governs and the diagram must be corrected in the same change.

## Where to start, by what you came for

The index above says who owns what. It does not say what to read, and reading
eighteen documents in numerical order is nobody's best route into any of them.

| You want to know | Read, in this order |
|---|---|
| **What this thing does today** | [Progress](PROGRESS.md) — plain language, no code. Then [STATUS](implementation/STATUS.md) for the evidence behind each claim |
| **Whether to build on it** | [02 Architecture](02-Architecture-and-Decisions.md) → [03 Structure](03-Project-Structure.md) → [the ADRs](adr/README.md). The ADRs are the *why*; the code is the *what* |
| **How to run it** | [Onboarding](ONBOARDING.md) → [Local Development](LOCAL-DEVELOPMENT.md). [Operating](OPERATING.md) if you are running it for somebody else |
| **Whether it can be sold, and to whom** | [11 Franchise & External Scope](11-Franchise-and-External-Scope.md) — start at §1, which reframes the rest, then the register at §12 |
| **What is safe to change** | [06 Security & API](06-Security-and-API.md) and [08 Governance](08-Governance-and-Standards.md). Then the file header of whatever you are about to edit: the load-bearing rules are written where they can be broken, not only here |
| **What happens next** | [07 Delivery Roadmap](07-Delivery-Roadmap.md) for the plan, [11 §11](11-Franchise-and-External-Scope.md) for the decisions blocking it |

**Two things are not in this list on purpose.** A per-file guide to the codebase
does not exist and should not: the file headers are that guide, and a second copy
would be a second thing to keep true. And there is no "getting started tutorial"
— [Onboarding](ONBOARDING.md) is deliberately the only entry point, so there is
one door rather than three that disagree.

## System summary

DealerFOSS is a modular monolith: one ASP.NET Core application whose business modules have explicit boundaries, backed by one database per **dealer organization**. A dealer organization may operate one or many rooftops. Shared records and group reporting remain inside the organization boundary; rooftop and department scope control operational access. External systems connect through an Integration edge that maps versioned provider data into DealerFOSS contracts and applies it through owning modules. The first release is a coexistence product; standalone system-of-record capability is earned subsystem by subsystem through migration, reconciliation, and operational proof.

## Vocabulary

| Term | Meaning |
|---|---|
| Dealer organization | the customer and tenant boundary; may own one or many rooftops |
| Rooftop | a physical dealership/location operating under the organization |
| Department | Sales, F&I, Service, Parts, Accounting, or another operating unit at a rooftop |
| Tenant | the technical isolation unit; in v1 it equals one dealer organization |
| Connector | an adapter to an external DMS or provider |
| System of record | the authoritative source for a named subsystem, never a claim about unfinished modules |

## Change rule

Each accepted architecture decision has a stable ADR entry. A material change adds a dated ADR that supersedes the earlier decision and updates every affected document and diagram in the same pull request. Editorial corrections do not require an ADR.
