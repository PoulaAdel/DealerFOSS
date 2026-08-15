# ADR-007 — Integration is a platform edge

Date: 2026-07-25
Status: Accepted
Supersedes: —

## Context

Placing connectors among business modules blurs the line between the dealership domain and other companies' systems, and invites vendor concepts to leak into business code.

## Decision

`src/Integrations/` is a sibling of business modules. It owns external protocols, credentials, mapping, inbox and outbox processing, checkpoints, and connector health. It owns no dealership business rules.

## Correction, 2026-08-15 — the address changed, the decision did not

The path above is stale. [ADR-017](0017-three-projects-flat-features.md)
collapsed the tree to three projects, so the edge now lives at
`src/App/Integrations/` — a flat capability folder like every other.

**What this ADR decided is unaffected**: the edge is a *sibling* of business
capabilities rather than a layer beneath them, it owns transport and credentials
and mapping, and it owns no dealership business rules. All of that holds. Only
the folder moved.

## Alternatives considered

- **A connector module beside Sales and Service.** Rejected: it implies external systems are a business capability.
- **Connectors inside each consuming module.** Rejected: it duplicates transport, retry, and credential handling per module.

## Consequences

- Everything inside the edge faces outward; every business capability faces the business.
- The edge writes through published module contracts and never touches module internals.

## Validation / review trigger

Revisit if a connector legitimately needs to own business state, which would indicate a missing module.
