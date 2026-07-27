# ADR-006 — Versioned integration contracts

Date: 2026-07-25
Status: Accepted
Supersedes: —

## Context

External dealer systems differ in shape, completeness, and semantics. Letting vendor payloads reach business modules would couple dealership rules to one provider's quirks and make provider changes breaking changes.

## Decision

External data maps to capability-specific, versioned contracts carrying source, entity identity and version, event time, provenance, and deletion state. Vendor DTOs never enter business modules. Contracts evolve additively within a version.

## Alternatives considered

- **One universal canonical model.** Rejected: a single shape for every capability becomes either too thin to be useful or too broad to evolve safely.
- **Pass vendor payloads through.** Rejected: it destroys the anti-corruption boundary.

## Consequences

- Contracts are deliberately richer than the initial database model.
- A breaking provider change is absorbed inside its connector rather than propagated.

## Validation / review trigger

Revisit when a second certified provider forces a contract change that cannot be made additively.
