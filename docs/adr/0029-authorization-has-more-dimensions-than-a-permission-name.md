# ADR-0029 — Authorization has more dimensions than a permission name

**Status:** Proposed · **Date:** 2026-09-29

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
