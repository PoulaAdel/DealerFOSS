# ADR-007 — Integration is a platform edge

Date: 2026-07-25
Status: Accepted
Supersedes: —

## Context

Placing connectors among business modules blurs the line between the dealership domain and other companies' systems, and invites vendor concepts to leak into business code.

## Decision

`src/Integrations/` is a sibling of business modules. It owns external protocols, credentials, mapping, inbox and outbox processing, checkpoints, and connector health. It owns no dealership business rules.

## Alternatives considered

- **A connector module beside Sales and Service.** Rejected: it implies external systems are a business capability.
- **Connectors inside each consuming module.** Rejected: it duplicates transport, retry, and credential handling per module.

## Consequences

- Everything inside the edge faces outward; every business capability faces the business.
- The edge writes through published module contracts and never touches module internals.

## Validation / review trigger

Revisit if a connector legitimately needs to own business state, which would indicate a missing module.
