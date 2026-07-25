# ADR-014 — Compiler, architecture-test, and schema boundary enforcement

Date: 2026-07-25
Status: Accepted
Supersedes: —

## Context

A modular monolith keeps its boundaries only if crossing one is difficult. Documentation and code review alone do not survive schedule pressure or contributor turnover.

## Decision

Enforce boundaries three ways: project and namespace references provide compiler boundaries, architecture tests detect forbidden references and cycles, and database tests enforce schema ownership.

## Alternatives considered

- **Convention and review only.** Rejected: boundaries erode silently.
- **Compiler boundaries alone.** Rejected: they cannot express rules such as "no module may reference the integration edge" within a shared project.

## Consequences

- Tests support a readable structure; they do not substitute for one.
- Once met, a boundary rule becomes a standing CI gate that later phases may not regress.

## Validation / review trigger

Add a rule whenever a boundary violation reaches review; a violation a human had to catch is a missing test.
