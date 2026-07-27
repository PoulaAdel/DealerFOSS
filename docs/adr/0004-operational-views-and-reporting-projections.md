# ADR-004 — Live operational views and isolated reporting projections

Date: 2026-07-25
Status: Accepted
Supersedes: —

## Context

Showroom and service-lane screens need current data. Analytical dashboards are expensive and must not contend with operational writes during peak hours.

## Decision

Operational screens query indexed transactional data or near-real-time projections. Analytical dashboards and expensive reports read `[rpt]` projections refreshed incrementally. Every report displays its source and freshness. Hourly refresh is permitted only where the documented business use tolerates it.

## Alternatives considered

- **Everything live.** Rejected: heavy analytics would lock operational tables at the worst moment.
- **Everything hourly.** Rejected: dispatch, inventory, and funding status must not be an hour stale.

## Consequences

- Two clearly separated read paths, each with a stated freshness target.
- Reporting never writes operational tables; projection checkpoints and rebuilds must be durable and observable.

## Validation / review trigger

Revisit if operational projection freshness targets cannot be met, or if report freshness complaints recur.
