# ADR-008 — Module contracts and durable events

Date: 2026-07-25
Status: Accepted
Supersedes: —

## Context

In a single process it is trivially easy for one module to query another's tables. That coupling silently destroys the boundaries the architecture depends on and makes later extraction impossible.

## Decision

Synchronous lookups use narrow published interfaces. Cross-module state changes use durable events written through a transactional outbox. No module reads another module's tables or depends on its data or service implementation.

## Alternatives considered

- **Shared database access between modules.** Rejected: it is precisely the coupling this architecture exists to prevent.
- **An external message bus.** Rejected for v1: it adds infrastructure a single-node install should not require.

## Consequences

- A transactional outbox is required before the first cross-module event ships.
- Handlers must tolerate at-least-once delivery and must not assume an external side effect shares their transaction.

## Validation / review trigger

Revisit the transport if event volume or delivery guarantees exceed what an SQL-backed outbox can serve.
