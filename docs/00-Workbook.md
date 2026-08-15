# DealerFOSS — Engineering Workbook

**Project:** DealerFOSS — Open-Source Dealer Management System  
**License:** AGPLv3, with an optional commercial license  
**Status:** Accepted baseline; changes require an ADR

This workbook is the entry point to the documentation set: what each document
owns, how to read the set without reading all of it, and the one rule that keeps
the rest honest.

---

## The reading rule: specification and state are different things

**Most of this set describes the product being built, not the product that
exists.** Documents 01–08 are the specification. They are written in the present
tense, which is normal for a specification and dangerous when it is read as a
description — "commands accept an idempotency key" is a design decision, and no
endpoint has ever read one.

Two documents, and only two, answer *what is true today*:

| Question | Document |
|---|---|
| What works, in plain language, for somebody who is not a developer | [Progress](PROGRESS.md) |
| What is built, what proves it, and what each milestone deliberately excluded | [implementation/STATUS.md](implementation/STATUS.md) |

**Where a specification document and one of those two disagree, the two are
right.** A specification says what should be; it cannot make itself true.

Everywhere the gap is large enough to mislead, the specification now says so in
place — "specified, not built", with the reason. Those markers were added on
2026-08-15 after a sweep found eleven such claims being read as descriptions of a
working system, including a CLI, an ETag, and a federation path that has never
existed.

### Three rules that follow, for whoever edits these documents

1. **One fact, one home.** If a fact is worth stating twice, the second place
   links to the first. Copies go stale independently and then argue with each
   other — Identity's exported type list was copied into three documents and was
   wrong in all three by the time anyone checked. It now lives only in the test
   that asserts it.
2. **Prefer a command to a claim.** "Fifteen capability folders exist" is worse
   than "`ls src/App` is the authority". A reader can run the second.
3. **Correct in place, and say what was wrong.** A silently fixed error teaches
   nobody; a dated correction tells the next reader which kind of mistake this
   set makes. Several sections below carry one.

---

## Where to start, by what you came for

Reading eighteen documents in numerical order is nobody's best route into any of
them.

| You want to know | Read, in this order |
|---|---|
| **What this thing does today** | [Progress](PROGRESS.md) — plain language, no code. Then [STATUS](implementation/STATUS.md) for the evidence behind each claim |
| **How to get it running and start changing it** | [Onboarding](ONBOARDING.md) — **the one door**, and it does not start with documents. Then [Local Development](LOCAL-DEVELOPMENT.md) when your environment fights you |
| **Whether to build on it** | [02 Architecture](02-Architecture-and-Decisions.md) → [03 Structure](03-Project-Structure.md) → [the ADRs](adr/README.md). The ADRs are the *why*; the code is the *what* |
| **Whether it can be sold, and to whom** | [11 Franchise & External Scope](11-Franchise-and-External-Scope.md) — start at §1, which reframes the rest, then the register at §12 |
| **What is safe to change** | [06 Security & API](06-Security-and-API.md) and [08 Governance](08-Governance-and-Standards.md). Then the file header of whatever you are about to edit: the load-bearing rules are written where they can be broken, not only here |
| **What happens next** | [09 Implementation Roadmap](09-Implementation-Roadmap.md) for the phase you are in and its exit criteria; [07 Delivery Roadmap](07-Delivery-Roadmap.md) for the commercial plan around it; [11 §11](11-Franchise-and-External-Scope.md) for the decisions blocking both |
| **How to run it for somebody else** | [Operating](OPERATING.md) — first start, setting a dealership up, backups, and what to check when something is wrong |

**Two things are not in this list on purpose.** A per-file guide to the codebase
does not exist and should not: the file headers are that guide, and a second copy
would be a second thing to keep true. And there is no "getting started tutorial"
— [Onboarding](ONBOARDING.md) is deliberately the only entry point, so there is
one door rather than three that disagree.

---

## Document index

Eighteen prose documents, plus the ADRs and diagrams. Doc 10 is the coding
agent's working instructions and is git-ignored, so a published checkout has
seventeen.

### The specification — what the product is meant to be

| # | Document | Owns |
|---|---|---|
| 01 | [Vision & Scope](01-Vision-and-Scope.md) | product boundaries, users, releases, and competitive path |
| 02 | [Architecture & Decisions](02-Architecture-and-Decisions.md) | system shape, the ADR register, and the technology actually in use |
| 03 | [Project Structure](03-Project-Structure.md) | code layout, naming, and dependency enforcement |
| 04 | [Data & Tenancy](04-Data-and-Tenancy.md) | dealer organizations, rooftops, databases, data lifecycle, and reporting |
| 05 | [Integration Framework](05-Integration-Framework.md) | external contracts, connectors, synchronization, import, and export |
| 06 | [Security & API](06-Security-and-API.md) | identity, authorization, privacy, audit, and API rules |
| 07 | [Delivery Roadmap](07-Delivery-Roadmap.md) | commercial phases, exit gates, service targets, and the risk register |
| 08 | [Governance & Standards](08-Governance-and-Standards.md) | licensing, contribution, releases, and coding standards |
| 11 | [Franchise & External Scope](11-Franchise-and-External-Scope.md) | what a franchised dealer needs that we do not have, and what blocks each piece |

### The state — what is true today

| Document | Owns |
|---|---|
| [Progress](PROGRESS.md) | the plain-language account of what works, for a reader who is not a developer |
| [implementation/STATUS.md](implementation/STATUS.md) | what is built, the command that proves each claim, and what each milestone excluded |

### Working documents — how to do the work

| # | Document | Owns |
|---|---|---|
| — | [Onboarding](ONBOARDING.md) | **start here** — how to read, run, and change the project |
| — | [Local Development](LOCAL-DEVELOPMENT.md) | prerequisites, canonical commands, and the environment facts that are easy to get wrong |
| — | [Operating](OPERATING.md) | **for whoever runs the installation** — first start, setting a dealership up, backups, diagnosis |
| 09 | [Implementation Roadmap](09-Implementation-Roadmap.md) | implementation workflow, phase roadmap, completion rules, and progress reporting |
| — | [Commercial License](COMMERCIAL-LICENSE.md) | the alternative terms offered alongside AGPLv3 |

[Architecture decisions](adr/README.md) and [diagrams](diagrams/README.md) are
concise companions to these documents. If prose and a diagram disagree, the
owning document governs and the diagram must be corrected in the same change.

**Two roadmaps, and they are not duplicates — they are two halves of one plan.**
Doc 07 owns the *commercial* side: pilots, certification, service targets, risks,
and dates, in phases 0–8. Doc 09 owns the *engineering* side: what to build and
what evidence closes a phase, in phases I0–I9. **They line up one to one** — `I1`
delivers Delivery Phase 1, `I2` delivers Phase 2, and so on, with `I9+` covering
the standalone releases past the coexistence gates.

The split is deliberate and worth keeping: **a delivery phase closes on a date
and a business gate; an I-phase closes on evidence.** Neither supersedes the
other. If you only need one, use doc 09 to decide what to do this week and doc 07
to explain to somebody why it matters.

---

## System summary

DealerFOSS is a modular monolith: one ASP.NET Core application whose business
capabilities have explicit boundaries, backed by one database per **dealer
organization**. A dealer organization may operate one or many rooftops. Shared
records and group reporting remain inside the organization boundary; rooftop and
department scope control operational access. External systems connect through an
Integration edge that maps versioned provider data into DealerFOSS contracts and
applies it through owning capabilities. The first release is a coexistence
product; standalone system-of-record capability is earned subsystem by subsystem
through migration, reconciliation, and operational proof.

## Vocabulary

| Term | Meaning |
|---|---|
| Dealer organization | the customer and tenant boundary; may own one or many rooftops |
| Rooftop | a physical dealership/location operating under the organization |
| Department | Sales, F&I, Service, Parts, Accounting, or another operating unit at a rooftop |
| Tenant | the technical isolation unit; in v1 it equals one dealer organization |
| Capability | a flat folder in `src/App` owning one area of dealership work (ADR-017). Earlier documents call these *modules*; the two words mean the same thing here |
| Connector | an adapter to an external DMS or provider |
| Control plane | whoever runs the installation, and the separate identity, database schema and console they use. Never a dealership user |
| System of record | the authoritative source for a named subsystem, never a claim about unfinished capabilities |

## Change rule

Each accepted architecture decision has a stable ADR entry. A material change
adds a dated ADR that supersedes the earlier decision and updates every affected
document and diagram in the same pull request. Editorial corrections do not
require an ADR.

**A change that makes a specification claim true should delete its "specified,
not built" marker in the same commit.** That is the one piece of bookkeeping this
convention costs, and it is cheaper than the alternative it replaced.
