# ADR-0027 — A records package carries records, not transactions, and its documents are re-rendered rather than copied

**Status:** Accepted · **Date:** 2026-09-21

## Context

I2's last unmet exit criterion read: *"Export round-trip tests preserve IDs,
relationships and documents."* Scored on 2026-09-09 as **IDs yes, the other two
no** — export covered customer and vehicle columns only, as two flat CSVs, with
no relationship manifest and nothing at all for documents.

Three questions had to be settled before anything could be built, and none of
them is answered by the criterion's own wording.

### What is a "document" here?

Checked in the code rather than assumed. `IDocuments` **owns no data**: its own
header says so, and `DocumentService` reads through `IDeals`, `IRepairOrders`,
`ICustomers` and `IOrganization` and renders HTML at the moment somebody asks.
`RenderedDocument` is never stored. A search across `src` for `IFormFile`,
`BlobClient`, `FileStream` and `Attachment` returns nothing: **this system has no
file storage of any kind.**

So "preserve documents" cannot mean copying files, because there are none. It
could mean shipping the rendered HTML in the package — but that would freeze a
copy the receiving installation would immediately contradict, and it would prove
nothing about whether the data behind it survived.

### What does "preserve IDs" require?

The CSV export already satisfied this reading of the criterion by carrying each
record's identifier — falling back to the Guid when a hand-typed customer has no
external reference. But an identifier that is *carried* is not the same as an
identifier that is *kept*, and relationships need the second: a deal's
`CustomerId` has to still name that customer on the far side.

Every one of the five aggregates already takes an explicit id in its factory —
`Customer.Person(id, …)`, `Vehicle.Record(id, …)`, `InventoryUnit.Receive(id,
…)`, `Deal.Start(id, …)`, `RepairOrder.Open(id, …)`. Nothing had to be opened up
to keep primary keys across a round trip.

### What happens to the money?

A deal is not only a record. Delivering one posts the sale, relieves the car's
book value from 1300 and raises a receivable; invoicing a job posts revenue,
takes parts off the shelf and raises another. If importing replayed those, a
dealership moving onto DealerFOSS would have every historical sale posted into
this month's ledger — on top of the opening balances they entered when they were
set up, which is where that money already is.

## Decision

**A package carries records, not transactions.**

1. **Format.** One JSON document, `dealerfoss.package` version 1, holding one
   rooftop's stock, deals and jobs plus exactly the customers and cars those
   records name. Self-contained by construction, so a dangling reference in a
   package is corruption rather than a normal case. It sits *beside* the two
   CSVs and does not replace them: a dealership taking its customer list to a
   system that is not DealerFOSS wants a spreadsheet, and a package is not one.

2. **Identity is kept, not translated.** Every record carries its own id and
   every reference is that id. The receiving installation stores the same keys.
   There is no id map and there must not be one: the moment ids are re-minted, a
   package can be imported once only, and re-running a half-finished import —
   the recovery path — stops working.

3. **An id already present is left alone.** `ImportOutcome.AlreadyPresent`, not
   an update. The receiving dealership may have corrected that record since the
   package was produced, and silently reverting their correction is worse than
   doing nothing. This is what makes re-running safe, which is what makes
   applying a package without a wrapping transaction acceptable.

4. **Nothing is posted.** `IInventory.ImportAsync`, `IDeals.ImportAsync` and
   `IRepairOrders.ImportAsync` write the aggregate in its final state and touch
   no ledger, no stock movement and no receivable. **Opening balances are how
   the money arrives; a package is how the records arrive.** The two mechanisms
   already existed separately and this keeps them separate.

5. **A status is placed, not walked.** Replaying Draft → Submitted → Approved →
   Delivered would write a history claiming the deal was approved today by
   whoever ran the import — and would be refused outright for any deal whose
   salesperson happened to be that person. Each imported aggregate gets one
   history entry saying it arrived in a package.

6. **The total is carried as evidence.** Each deal and job states what the
   source system said it came to. The importer recomputes it from the lines it
   just wrote and **refuses the record if the two disagree**, naming both
   figures. Nothing reads the figure back out afterwards; it exists so that a
   package which lost a line says so instead of arriving quietly wrong.

7. **Documents are re-rendered, not copied.** The package carries everything the
   renderer reads, and `PackageTests.The_paperwork_comes_out_the_same_on_the_other_side`
   renders the vehicle order and the service invoice on both sides and compares
   the money tables character for character. That is a **stronger** claim than
   shipping HTML: it fails if any field behind the page was lost, including
   fields nobody thought to assert.

8. **A refusal is an answer, not a failure.** One record that cannot be written
   leaves the rest to land and appears in the report by kind, id and reason in
   words. The request succeeds; the refusals are its result.

## Consequences

**The dependency order is the algorithm.** Customers and vehicles, then units,
then deals, then jobs — decided by the importer and never read from the file. A
package hand-edited or written by a tool that sorts its output still applies.
This is the reordered-delivery property the connector runtime has, applied to
files.

**Two defects were found by the paperwork test and fixed with it.** A deal's
charges, its products, its tax lines and a job's service lines had **no defined
print order**, so the far side — where those rows carry fresh ids — printed them
in a different sequence. Two copies of one document that do not agree about
their own row order are not the same document, and until this ADR nothing had
ever needed them to be. `DealService` and `RepairOrderService` now order them
explicitly, which also means a reprint here is stable, which it previously was
not.

**Scope is a lot, not an installation.** A package is one rooftop, imported into
one rooftop the caller names — the rooftop id inside the file belongs to
somebody else's installation and names nothing here. Whole-installation copying
is `deploy/backup.ps1` and stays there; this must not grow into a backup system.

**What is deliberately absent, and named rather than hidden:** the ledger,
receivables and payments; parts stock and its movements; leads; appointments;
the F&I catalogue (a sold product travels with its own name, price and cost, so
the receiving installation need not stock it); technician clockings; and the
organization structure itself. A package moves a lot's *records*, and the
receiving dealership's books, shelves and staff are its own.

**Two conflicts the round trip will report rather than resolve.** A VIN already
here under a different id, and an external reference already used by a different
customer, are both refused by name. Both are real — two installations seeded
from the same legacy extract share external references, which is how the second
one was found — and both are somebody's decision, not the importer's.
