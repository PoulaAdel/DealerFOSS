# ADR-021 — A value that does not fit becomes absent, never a substitute

Date: 2026-08-12
Status: Accepted
Supersedes: —

## Context

External systems send values our storage cannot hold: a 300-character address for
a 100-character field, a model year of `0`, an amount outside any plausible range,
a date in 1899. Two things are true at once and they pull in opposite directions.

**One bad field must not fail a ten-thousand-row batch.** An import that refuses
the whole night's data because one address was long is an import the dealership
turns off. So values are checked and adjusted before writing, per field, with the
batch surviving.

**But the adjustment is where the damage happens.** The obvious implementation —
clamp the number, truncate the string, substitute a default date — produces
values that are indistinguishable from real ones. A substituted `0` is a
plausible sale amount. A sentinel date is a plausible delivery date. Neither
trips a validation rule, both appear in reports and totals, and by the time
anyone asks, the source record may no longer be retrievable.

This is not hypothetical. It is what the existing implementations of this pattern
do, and it is the reason to write the decision down before ours exists.

## Decision

A value that cannot be stored as received is recorded as **absent**, with the raw
text preserved in the record's `MappingWarnings` alongside the reason.

- Never a clamped bound, a zero, an empty string, or a sentinel date.
- **Truncation is the single exception**, permitted only for free text, and only
  with a warning recorded. Never for an identifier, a code, or any field a later
  lookup is keyed on — a truncated key silently matches the wrong record, which
  is worse than no record.
- A field that is required by the owning module and arrives unstorable
  quarantines the record. Absent is honest for an optional field; it is not a
  way to smuggle an incomplete record past a rule.
- Corrections are counted per field per run and surfaced with the run, not
  written once per row into a log nobody reads.

## Alternatives considered

- **Fail the batch on any unstorable value.** Rejected: correct in principle,
  abandoned in practice within a week, and abandoned by disabling the check
  rather than by fixing the data.
- **Clamp or substitute, and log it.** Rejected: this is the pattern being
  corrected. A log entry does not travel with the record, and the record is what
  every downstream consumer sees.
- **Widen every column so nothing fails.** Rejected: it moves the problem to the
  first consumer with a real constraint — a printed document, a lender's API, a
  statutory report — and does so far from where the data entered.

## Consequences

- Absent values are visible and chaseable; a substituted value is neither.
- Downstream code must handle absence in fields that "always" have a value.
  That is the cost, and it is the correct cost: those fields do not always have a
  value, and the substitute was hiding it.
- Reconciliation gains a real signal — a field's absence rate per connector is a
  measure of mapping quality rather than noise.

## Validation / review trigger

Revisit if a provider is found whose absent and zero are genuinely
indistinguishable at source, which would make our distinction unprovable for that
field rather than merely inconvenient.
