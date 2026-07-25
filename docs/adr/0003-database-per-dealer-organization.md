# ADR-003 — Database per dealer organization

Date: 2026-07-25
Status: Accepted
Supersedes: —

## Context

Cross-tenant data leakage is the most damaging failure a multi-tenant dealer system can have. Dealer groups also expect per-organization backup, restore, and maintenance windows.

## Decision

Each tenant, meaning one dealer organization, has one database containing all of its rooftops. Tenant business tables do not carry a tenant identifier; rooftop- and legal-entity-owned records do carry `RooftopId` and, where relevant, `LegalEntityId`. The host catalog stores tenant routing only.

## Alternatives considered

- **Shared schema with a tenant discriminator.** Rejected for v1: a single missing filter leaks data across dealers.
- **Database per rooftop.** Rejected: it would fragment organization-wide customers, vehicles, and reporting.

## Consequences

- Isolation is structural rather than a filter that must be remembered.
- Migrations fan out across tenant databases and need resumable tooling.
- A pooled hosted database is not designed now and would require a new ADR backed by operational need.

## Validation / review trigger

Revisit if hosted operation reaches a tenant count where per-database maintenance becomes the dominant operational cost.
