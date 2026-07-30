# ADR-002 — Capability-first modules with limited internal structure

Date: 2026-07-25
Status: Superseded by [ADR-017](0017-three-projects-flat-features.md) on 2026-07-30
Supersedes: —

> **Superseded.** Four capabilities in, this shape had produced seven projects,
> five `DbContext` classes, and four-level paths to one-screen files — while the
> boundary it enforced turned out not to be one anybody was trying to breach.
> [ADR-017](0017-three-projects-flat-features.md) replaces it: compiler walls only
> where a breach is expensive, architecture tests everywhere else. Kept here
> unedited because the reasoning below was sound for the size the project was.

## Context

A contributor looking for Sales or Service should find its model, workflows, persistence, and endpoints together. Uniform technical-layer folders inside every module fragment a single feature across several directories and add depth without adding clarity.

## Decision

Top-level folders map to dealership capabilities. Small modules stay flat. A module may add `Domain/`, `Features/`, `Data/`, or `Contracts/` subfolders once file count or distinct workflows make navigation genuinely harder.

## Alternatives considered

- **Uniform `Domain/Application/Infrastructure` per module.** Rejected: it applies technical layering at a granularity where it fragments features.
- **Strictly flat, always.** Rejected: a mature Sales or Accounting module will not scan comfortably as one flat folder.

## Consequences

- Folder depth grows only when a module earns it; predictable ownership matters more than uniform shape.
- The permitted subfolder names are fixed so navigation stays predictable across modules.

## Validation / review trigger

If contributors regularly cannot locate a capability's code, revisit the permitted structure.
