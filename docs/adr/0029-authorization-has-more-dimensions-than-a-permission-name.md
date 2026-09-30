# ADR-0029 — Authorization has more dimensions than a permission name

**Status:** Superseded by [ADR-030](0030-a-scope-answers-about-a-record.md) · **Date:** 2026-09-29

## Context

A dealer-systems practitioner reviewed the product and said the permissions in a
real DMS could be **twenty times** what this one carries. We have **33**
permissions and four roles.

The count is roughly right, and it is the least useful part of the observation.

**What was measured.** No major vendor publishes a permission catalogue — CDK,
Reynolds and Tekion keep their administration guides behind customer portals, so
no exact multiple could be confirmed. Two sources were reachable:

- [DX1](https://help.dx1app.com/support/solutions/articles/35000163672-definitions-accountant-permissions),
  a **powersports** DMS and therefore simpler than an automotive one, publishes
  ~80 permissions for the **Accountant role alone**, with separate definition
  pages for Sales Manager, Parts Manager, Service Manager and Parts Receiver.
- The Dominion Vue captures held locally show a three-level menu: 8 top-level
  modules, 5–8 groups each, 6–7 leaves per group — roughly 180 distinct screens
  before multiplying by view/add/edit/delete.

So the honest statement is **an order of magnitude, 10–20x, not a confirmed
20x**, and two whole modules we do not have at all (payroll, and the
manufacturer communication system).

**Why the count is the wrong thing to chase.** A permission name is one
dimension. Our model has two: *which permission*, and *organization-wide or one
rooftop*. Everything is boolean.

A dealership speaks in dimensions we cannot express at any catalogue size:

| What a dealership says | Can the model say it? |
|---|---|
| "Service advisors do not see cost or gross" | **No.** Suppressed in the printed page only |
| "A salesperson sees their own deals, not the store's" | **No.** No ownership dimension |
| "Parts people work in parts, not in F&I" | **No.** `Rooftop` has `Departments`; authorization never reads them |
| "Discounts under $500 desk, $500–1,500 sales manager, above that the GM" | **No.** `Deals.Approve` is a boolean |
| "Nobody approves their own deal" | **Hard-coded**, `DealService.ChangeStatusAsync` |
| "GMs see their store, group leadership sees the portfolio" | **Yes.** This one it does well |

Two of those rules are already written as `if` statements in capability code
because the model has nowhere to put them. That is not a prediction that the
model will strain — it is the observation that it already has.

**A compliance driver, not only a feature request.** US dealers are financial
institutions under the FTC Safeguards Rule, and the FTC published
[dealer-specific FAQs in June 2025](https://www.ftc.gov/news-events/news/press-releases/2025/06/ftc-provides-guidance-updated-safeguards-rule).
The rule requires least privilege **and periodic access review**. There is no
access-review surface here at all: no "who holds what" report, no
recertification, no way for a manager to answer an auditor without reading the
database.

**What this is not.** None of it is a security defect. Nothing is broken; every
rule that exists is enforced on the server, in services, exactly as ADR-025 and
the 2026-09-24 security testing recorded. The system does what it was designed
to do. The design simply has no way to say some of what a dealership needs to
say, so the sayable part was applied wherever somebody thought of it.

## Decision

**The permission model grows by dimension, not by name.** New permission names
are added when the code already has a distinct action to protect — never as a
way of approximating a dimension the scope object cannot carry.

Four dimensions are adopted, in this order, each landing as its own change that
references this ADR:

1. **Field-level visibility** — whether the caller sees cost and gross.
2. **Department** — a third assignment level below rooftop. The `Department`
   entity already exists and is unused by authorization.
3. **Ownership** — "records I am named on" as a narrowing of a rooftop grant.
4. **Threshold** — an approval right carrying a limit rather than a boolean.

A fifth deliverable is not a dimension but is required by the same driver:
**an access-review surface** — who holds what, exportable, for the Safeguards
periodic review.

### Why a dimension and not a check in each capability

This is the whole argument, and it is drawn from a defect this project has
already had.

Cost and gross cross **six capability contracts** — `IAccounting` (5 fields),
`IDeals` (5), `IInventory` (4), `IParts` (6), `IRepairOrders` (1). Gating them
by hand means the same rule written in six services.

On 2026-09-24 the security pass found exactly that shape. The rule "unknown and
unauthorized must answer identically" was correctly held in `OrganizationService`,
`AppointmentService` and `AccountingService` — each with a comment saying so —
and **missed in the one place that had a reason to look different**, because
`FindAsync` takes a reference rather than a record id and so had a legitimate
"there is none" answer to hide behind. One rule, four sites, one miss.

Six hand-applied copies of a visibility rule will grow a seventh that forgets.
A dimension carried on `AuthorizedScope` cannot be forgotten by a capability
added next year, because that capability has to ask for the scope anyway. This
is ADR-025's reasoning turned around: that ADR made the browser's permission
list *deliberately lossy* so it could not be mistaken for a control; this one
puts the control in the single object every service already calls.

### What this costs, measured rather than assumed

**Catalogue growth is close to free, and an earlier claim in this project's
notes that it is not was wrong.**

`GetHeldPermissionsAsync` is one round trip that returns the permissions a user
holds; it does not iterate the catalogue. The comment beside it — "thirty-three
queries on every page load" — describes the alternative that was **rejected**,
not the code that ships. `GetAuthorizedScopeAsync` costs two queries per
permission *checked in a request*, which is one or two, and is likewise
independent of how many permissions exist. Going from 33 names to 600 does not
multiply a single query.

What *can* cost is this decision: each new dimension adds data to every check.
The number to protect is the p95 of **44 ms at the documented load of 50
concurrent users per organization**, measured 2026-09-29 (commit `f8f58c3`).
Each dimension's change re-runs that measurement and says what it did to it.

### This is a security decision, said out loud

Dimensions 1–4 change the shape of `AuthorizedScope` and `UserAssignment`, both
of which are Identity internals. Widening Identity's public surface is a
security decision under ADR-017, and each change that does so must say so in its
commit message and in the docs, and must extend the allow-list in
`tests/Architecture/BoundaryTests.cs` deliberately rather than to make a test
pass.

Field-level visibility in particular is a **default-deny** change: the fields
are returned to every reader today, so the first implementation removes access
that currently exists. That is the point, and it will be visible to users.

## Consequences

**Good.** A dealership can be configured the way it actually runs rather than
the way the model can describe. The two hard-coded rules move from `if`
statements into policy. The Safeguards access review becomes answerable. New
capabilities inherit visibility rules instead of re-implementing them.

**Bad.** Every dimension touches every service's scope check, which is the
widest blast radius in the codebase. Dimension 1 alone spans six contracts and
is a milestone, not a quick win — an earlier note in this work called it the
"smallest change" before the surface had been counted, and that was wrong.

**Deferred deliberately.** Splitting the catalogue finer — the thing the
original observation was about — is **last**, item 6, and is driven by where the
code already has a distinct action rather than by matching a competitor's count.
A larger catalogue on a two-dimensional model would be more names that still
cannot say what a dealership says, which is the failure this ADR exists to
avoid.

**Not adopted.** Time-of-day restrictions, IP-range restrictions, and per-record
access-control lists. Each is real in some systems; none was observed as a
dealership requirement in this review, and an unused dimension costs every check
forever.

## Correction — 2026-09-29

Written the same day as the ADR, on implementing dimension 1. **The decision is
unaffected; two claims above about the code were wrong.**

**Dimension 1 does not change the shape of `AuthorizedScope` or
`UserAssignment`, and is therefore not an Identity-internals change.** The
section "This is a security decision, said out loud" says dimensions 1–4 all do.
Field-level visibility turned out to need nothing new: it is an ordinary
permission name, resolved through the existing `GetAuthorizedScopeAsync`, and
the dimension is expressed by *where the answer is applied* — to fields on a
view rather than to the record — not by new data on the scope object. No public
type was added, and `BoundaryTests` is untouched. Dimensions 2–4 (department,
ownership, threshold) still change both shapes, and the paragraph stands for
them.

That makes the ordering better than it was argued, not worse: the cheapest
dimension to carry is the one that needed no new machinery at all.

**Three checks, not one.** The ADR says the rule belongs "on `AuthorizedScope`"
so that a capability cannot forget it. In practice each capability applies it in
the one method that builds its view — `DealService.DescribeAsync`,
`InventoryService.Summarize` and `.Describe` — so the guarantee is "one place
per capability", not "one place". That is materially weaker than the text
implies and is worth saying plainly. It is still far better than the six
hand-applied copies the argument was against, because every view of a deal in
the product goes through that one method; but a capability added next year must
still remember, and nothing yet makes forgetting a compile error. Making it one
genuinely cannot happen until a later dimension puts the data on the scope
object.

**One thing the change found on its own.** Making cost nullable broke the build
in `PackageExporter`, which reads through the capability contracts deliberately.
Without a new check, a records package would have exported with every cost field
quietly emptied — permanent on the far side, which cannot tell a withheld figure
from one never recorded. `ExportPackageAsync` now requires
`Profitability.Read` organization-wide alongside `Migration.Export`. The type
system caught what a reviewer reading the diff would not have.

## Addendum — Accounting, and a question this ADR left open

Written the same day, extending dimension 1 to `IAccounting`. Two things worth
recording for whichever capability applies this next.

**A withheld figure has to take everything derived from it with it, or the
withholding is decorative.** `ProfitAndLoss.NetProfit` is `GrossProfit` less
`TotalExpenses`. Nulling `GrossProfit` and leaving `NetProfit` a real number
would hand gross straight back to a caller who also knows overheads — which
this report shows regardless of `Profitability.Read`, because spending on rent
is not the same secret as what a car made. The rule: find every field on the
view that is *computed from* a withheld one, not only the ones this ADR named,
and withhold those too. `DepartmentResult.Margin` is the same case one level
down — it existed already, for a different reason (nothing sold), and the two
reasons are indistinguishable on purpose, same as everywhere else this
dimension applies.

**The question this ADR posed without answering: what does a GROUP total do
when the caller's grant does not cover every rooftop that fed it.** Unlike a
list (one row per record, nulled row by row — `DepartmentResult` looked like
this case and is not; see below), `LedgerPerformance.TotalCost` and
`ProfitAndLoss.GrossProfit` are single figures for however many rooftops the
query covers. Three answers were on the table: compute it from the rooftops
the caller can see, return null, or refuse the request outright. Chose null,
matching what dimension 1 already does elsewhere — computing a partial sum and
presenting it as the total is the exact failure shape this project has hit
repeatedly with columns that do not reach their own totals (the tax line, the
trade-in, the products — three separate incidents on `docs/11`), and a group
manager reading a partial gross as a whole one would not know to doubt it.
Applied as one rule for the whole query rather than per department:
`DepartmentResult` is not actually a per-rooftop row the way an inventory unit
or a deal is — it is one named category (`Vehicles`, `Service`, …) already
summed across whatever rooftops the query touched — so there is no row-level
signal to null field-by-field, and the same organization-wide-or-covers-the-
one-rooftop check that decides the totals decides every department's figures
together.
