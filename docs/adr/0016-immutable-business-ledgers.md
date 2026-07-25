# ADR-016 — Immutable business ledgers

Date: 2026-07-25
Status: Accepted
Supersedes: —

## Context

Accounting entries, parts stock movements, compliance evidence, and audit records are the basis for reconciliation, audit, and regulatory defense. Editing them in place destroys the ability to explain what happened and when.

## Decision

Posted accounting entries, parts stock movements, compliance evidence, and audit records are append-only. Corrections are made with reversals or adjustment records, never by modifying or deleting history.

## Alternatives considered

- **Editable records with a separate audit trail.** Rejected: the trail and the record can diverge, and the record is what people read.
- **Soft delete everywhere.** Rejected: soft delete is appropriate only where deletion is a valid domain action.

## Consequences

- Migrations must never alter posted immutable history.
- Correction workflows and reversal semantics are product requirements, not later additions.
- Privacy erasure must preserve records required as financial or regulatory evidence.

## Validation / review trigger

Revisit only if a legal obligation requires destroying records this decision preserves; resolve through legal review, not implementation convenience.
