# ADR-011 — One folder or project per connector

Date: 2026-07-25
Status: Accepted
Supersedes: —

## Context

A real provider integration carries authentication, paging, throttling, retry, vendor DTOs, mapping, and tests. Compressing all of that into a single file does not stay readable at production scale.

## Decision

Each provider has a dedicated connector folder or project containing its authentication, transport, vendor DTOs, mapping, capability adapters, and tests.

## Alternatives considered

- **One file per connector.** Rejected: readable for a stub, unreadable for a certified integration.
- **A shared mapping layer across providers.** Rejected: it recreates coupling between unrelated vendor quirks.

## Consequences

- Vendor DTOs and protocol details stay inside the connector boundary.
- Every connector must pass conformance tests covering duplicates, ordering, deletes, partial failure, idempotency, replay, and reconciliation before any certification label is applied.

## Validation / review trigger

Revisit if connector folders accumulate genuinely shared, provider-neutral logic that belongs in the edge.
