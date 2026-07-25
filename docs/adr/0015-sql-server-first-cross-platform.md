# ADR-015 — SQL Server first, cross-platform application

Date: 2026-07-25
Status: Accepted
Supersedes: —

## Context

Dealership IT commonly runs Windows and SQL Server, while hosted and containerized deployment favors Linux. Claiming database portability without testing it would be dishonest.

## Decision

SQL Server 2022 is the supported v1 database. The application runs under IIS or as a Windows service, and as Kestrel in a Linux container. Application scheduling does not depend on SQL Server Agent. Database portability is a future evidence-based decision, not a claim.

## Alternatives considered

- **PostgreSQL first.** Rejected for v1: it fits the target dealership environment less well.
- **Database-agnostic from day one.** Rejected: it constrains the schema and cannot be honestly claimed without a tested second provider.

## Consequences

- Scheduling uses a durable job store rather than SQL Server Agent, keeping Linux hosting viable.
- Supporting a second database engine requires its own migration, testing, and support commitment.

## Validation / review trigger

Revisit when a supported deployment or funded demand requires a second database engine.
