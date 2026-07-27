# ADR-001 — Modular monolith

Date: 2026-07-25
Status: Accepted
Supersedes: —

## Context

OpenDealer360 must be installed and operated on premises by a dealership IT administrator, often on a single server, and must stay readable for outside contributors. Dealership scale is typically well under a hundred concurrent staff per store.

## Decision

Build one deployable ASP.NET Core application containing bounded business modules and an integration edge. Modules communicate in process through published contracts and durable internal events.

## Alternatives considered

- **Microservices.** Rejected for v1: network hops, distributed transactions, service discovery, and multi-service deployment are real operational cost with no benefit at this scale, and they would make on-premises installation substantially harder.
- **Unstructured monolith.** Rejected: without enforced boundaries the codebase becomes untestable and unextractable.

## Consequences

- One process to deploy, monitor, back up, and restore.
- Boundaries must be enforced mechanically, since the compiler alone will not stop a cross-module reference (see ADR-014).
- Extraction to a service remains possible because each module already owns its data and exposes a contract.

## Validation / review trigger

Revisit when a module demonstrates a measured need for independent scaling, failure isolation, independent deployment, or regulatory isolation that outweighs on-premises operational cost.
