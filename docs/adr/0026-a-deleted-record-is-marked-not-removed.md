# ADR-0026 — A record deleted at the provider is marked, never removed

**Status:** Accepted · **Date:** 2026-09-19

## Context

Stage 2's exit criteria named four delivery behaviours a sync must survive:
duplicate, reordered, partial-page and **delete**. Three had tests. Delete had
no implementation at all — there was no tombstone and no `IsDeleted` anywhere in
`src/App/Integrations`, so a record removed at the provider stayed ours and
nobody could tell it had gone.

Modelling it raises a question the mechanism cannot answer on its own: **what
does a delete mean when we have used the record since?** A provider withdrawing
a customer has said something about *its* database. We may have three repair
orders, two deals and an outstanding balance against that same person.

The obvious implementation — hide the record — is worse than leaving deletes
unmodelled. The repair orders would still name somebody the screens can no
longer find; an advisor with that customer on the telephone would search and get
nothing. A defect that removes information silently is harder to notice, and
much harder to explain, than a missing feature.

There is also a hard structural constraint. `FeatureBoundaryTests` forbids
Customers from referencing `Deal` or `RepairOrder`, so the sink **cannot ask**
whether a customer has been used. Any design conditioned on "is this record in
use" would need a new cross-capability usage probe — real machinery, invented to
support a behaviour we do not actually want.

## Decision

**A delete is a mark on the record. It never hides one and never removes one.**

- `Customer.RemovedAtProviderOn` records when the source system stopped having
  them. It is **not** `IsArchived`: archiving is the dealership's own decision to
  stop seeing a record, and this is somebody else's statement about their own
  database. Conflating the two is precisely how a customer with three repair
  orders disappears from a screen somebody is looking at.
- A marked customer **stays in every list and every search**, still resolves
  from everything that names them, and keeps working for all of it.
- What it loses is the right to be chosen for **new** work.
- The mark is **visible** — a chip in the list, and a sentence on the record
  saying the provider no longer has them, that they are kept, and that
  everything already attached still works. A mark nobody can see is the silent
  removal under another name.
- Hiding remains available and remains the dealership's: `Archive()`, unchanged.

Because the decision applies uniformly, the sink never has to ask whether a
record is in use — so the boundary constraint stops being an obstacle and
becomes a hint that the uniform answer was the right one.

### Delivery semantics

`ProviderRecord` carries a `RecordAction` — one enum on the record rather than a
second method on `IRecordSink`, because **deletes arrive interleaved with
upserts in the same delta feed and the order between them is the provider's
meaning**. "Created, then deleted" and "deleted, then created" describe
different days; two method calls would throw that away.

Four behaviours follow, each with a test:

| Case | Answer | Why |
|---|---|---|
| Delete a record we hold | Marked, counted `Applied` | The event happened |
| Delete the same record again | `Unchanged` | A held cursor replays every night; counting it applied would make a stuck feed look busy |
| Delete a record we never had | `Unchanged`, not rejected | Delta feeds routinely report deletions of records this dealership never received; quarantining them buries the real refusals |
| Provider serves it again | Restored, counted `Applied` | Feeds undelete. It has to be as ordinary as the delete was |

## Alternatives rejected

**Hard delete.** Destroys history and referential integrity. Never available.

**Soft delete that hides.** The failure described above, and the reason this ADR
exists rather than a one-line flag.

**Hide only when unused.** Needs a cross-capability usage probe that
`FeatureBoundaryTests` forbids, and produces a rule nobody can predict: the same
provider action has two different outcomes depending on invisible state. A
person cannot learn a system that behaves differently for reasons it does not
show them.

**Refuse the delete and quarantine it.** Considered seriously — it puts a person
in the loop. Rejected because it makes an ordinary, expected event into an
exception queue: a feed of any size would generate quarantine rows faster than
anybody could clear them, and the genuine refusals would be lost among them.

## Consequences

**Good.** Nothing can vanish. The mark is uniform, predictable and explainable
in one sentence to whoever uses the screen. The delete path needed no new
cross-capability mechanism.

**Cost.** Records accumulate. A dealership migrating from a provider that churns
records will collect marked customers it can only remove by archiving them one
at a time. If that becomes a real complaint, the answer is a bulk archive the
dealership performs deliberately — not an automatic one the feed performs on
their behalf.

**Cost.** Every capability that later accepts provider deletes must make the
same decision again, in its own terms. A deleted *vehicle* and a deleted
*customer* are not obviously the same case, and this ADR should be read rather
than copied.

**Watch for.** The list query is where somebody tidying up would add
`WHERE RemovedAtProviderOn IS NULL`. Three tests fail if they do — two in
`CustomerRecordSinkTests` and one in `CustomersPage.test.tsx` — and they say why
in their assertion messages.

## Validation / review trigger

Revisit when a second capability accepts provider deletes, or if a pilot
dealership reports the accumulation above as a real problem rather than a
theoretical one.
