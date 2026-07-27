# ADR-005 — Redis optional for single-node deployments

Date: 2026-07-25
Status: Accepted
Supersedes: —

## Context

A small dealership installation should require as few moving parts as possible. Mandating a distributed cache for a single-node deployment adds an operational dependency that earns nothing.

## Decision

Single-node installations use in-process cache and locks plus durable SQL state. Redis is required only for multiple application nodes or workloads needing distributed coordination. Security-critical revocation and job state remain durable in SQL.

## Alternatives considered

- **Redis mandatory everywhere.** Rejected: it adds an installation step and a failure mode with no benefit on one node.
- **No distributed option at all.** Rejected: it would block horizontal scale-out later.

## Consequences

- The small-install topology is application plus SQL Server plus document storage.
- Coordination sits behind an abstraction so a Redis implementation can be configured without touching business logic.
- Losing the cache must never silently grant privileged access.

## Validation / review trigger

Revisit when a supported deployment routinely runs more than one application node.
