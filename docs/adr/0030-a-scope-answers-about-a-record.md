# ADR-0030 — A scope answers about a record, not about a rooftop

**Status:** Accepted · **Date:** 2026-09-30 · **Supersedes:**
[ADR-029](0029-authorization-has-more-dimensions-than-a-permission-name.md)

## Context

ADR-029 said the permission model grows by **dimension** rather than by name,
and listed four in order: field-level visibility, department, ownership,
threshold. Dimension 1 is built — `Profitability.Read` across Deals, Inventory
and Accounting, in three commits on 2026-09-29 and 2026-09-30.

Building it, and then measuring before building the next one, found three things
that make the original ADR wrong enough to replace rather than annotate again.
It already carries a Correction and an Addendum; a third would leave the
Decision section saying something nobody should follow.

**One. `AuthorizedScope` does not carry the rule, and ADR-029's central argument
assumed it would.** That argument was good: a rule applied by hand in six
services will grow a seventh that forgets, which is exactly how the 2026-09-24
receivables disclosure happened — one rule, four sites, one miss. The conclusion
drawn from it was that the rule belongs "on `AuthorizedScope`". In practice
dimension 1 landed as one check per capability, because `AuthorizedScope.Covers`
answers only *"is this rooftop in my scope"* and each service must then remember
the extra condition separately. ADR-029's own Correction admits this. Dimension 3
would repeat it across **39 authorization call sites** — 12 in Deals, 15 in
RepairOrders, 7 in Appointments, 5 in Leads — and dimension 4 would pay the same
cost again.

**Two. Department is not the cheap next step, and the reason is a fact nobody
checked.** ADR-029 called it "the cheapest structural win — the `Department`
entity already exists and is unused by authorization". The second half is true.
The first is not: **no business record carries a `DepartmentId`.** Departments
are organisational structure — created by the seeder, returned in the
organization view, referenced by nothing. Scoping records by department means
first tagging deals, repair orders and parts with one, which is a data-model
change across capabilities rather than an Identity change.

Its value is also weaker than assumed. The six kinds — Sales, F&I, Service,
Parts, Accounting, Administration — map onto capabilities, and **the permission
catalogue already expresses that split**: a technician holds `Service.Read` and
`Parts.Read` and not `Deals.Read`. Department scope would add record filtering
*within* a capability, such as two service departments at one rooftop, which no
structure in this product has and nobody has asked for.

**Three. Ownership is the cheap one, and its data is already there.** Every
record that needs it already names its person — `Deal.SalespersonUserId`,
`Lead.AssignedToUserId`, `RepairOrder.TechnicianUserId` and `AdvisorUserId`,
`Appointment.AdvisorUserId` — and the filter expressions already exist in the
three list methods. They are **caller-supplied query parameters**: a convenience
anybody holding the capability's read permission can set to any user, or omit to
see the whole lot. The work is turning an optional filter into an enforced
narrowing, not inventing a concept.

## Decision

**A scope answers about a record.** `AuthorizedScope` stops being "which
rooftops" and becomes "what may this caller reach", with one call that knows
every dimension:

```
Allows(rooftopId, ownerUserId) = Covers(rooftopId)
                                 && (!OwnRecordsOnly || ownerUserId == UserId)
```

`Covers(rooftopId)` **stays**, and keeping it is the point rather than an
oversight. "May I reach this lot at all" is a real and different question —
receiving stock, opening a job, booking a car in all happen before any record
with an owner exists. A refactor that collapsed the two would force every such
site to invent an owner, which is how a mechanical change becomes a wrong one.

The scope carries `UserId`, so a caller never passes their own identity to a
question already resolved for them.

Identity stays free of persistence: for a list, the service reads
`OwnRecordsOnly` and `UserId` off the scope and builds its own `Where`. The
three filter expressions that exist today become enforced instead of optional.

**The order of the remaining dimensions, corrected:**

1. ~~Field-level visibility~~ — **built**, Deals, Inventory and Accounting.
   Parts (`IParts`, six fields) and RepairOrders (`IRepairOrders`) remain.
2. **Ownership** — records I am named on, as a narrowing of a rooftop grant.
3. **Threshold** — an approval right carrying a limit rather than a boolean.
4. **Department** — and only after something tags records with a department.
   Reopen the question of whether it is wanted at all at that point; the
   permission catalogue may already be the answer.

The access-review surface the FTC Safeguards Rule requires — who holds what,
exportable — is unchanged from ADR-029 and still not built.

**Splitting the catalogue finer stays last**, for ADR-029's reason, which this
ADR does not disturb: more names on a model that cannot express a dealership's
sentences would be more names that still cannot express it.

### Two rules carried forward, because they outlived the ADR that recorded them

**A withheld figure takes everything derived from it.** `ProfitAndLoss.NetProfit`
is `GrossProfit` less `TotalExpenses`; nulling gross and leaving net real hands
gross straight back to a caller who also sees overheads, which that report shows
regardless of the right. Find every field *computed from* a withheld one, not
only the ones an ADR names. (From ADR-029's Addendum, written by the Accounting
work.)

**A group total is null, never a partial sum.** When a grant does not cover every
rooftop that fed a single figure, the figure is withheld rather than recomputed
from the visible part. Presenting a partial sum as a total is the failure this
project has hit three separate times with columns that do not reach their own
totals, and a manager reading a partial gross as a whole one would not know to
doubt it. (Same source.)

**A field right applies to an OUTBOUND view, and "outbound" is not the same as
"public record in a capability contract".** `IParts.IssuedParts` and
`IssuedPart` are public, sit in `IParts.cs` beside genuine views, and never
reach a caller: `IssueAsync` has no endpoint and its only caller in the solution
is `RepairOrderService`, inside the invoice transaction, where the cost is used
to freeze the line. Gating `IssuedPart.Cost` would make that freeze silently not
happen — `RecordCost` is once-only and throws on a second call, so the job's
parts cost would be wrong permanently, and the reconciliation report would
average a real figure against a missing one.

Worse, nothing would catch it. The consuming line already casts to `decimal?`,
so the change compiles clean, and no test asserts a per-job cost. This is the
`PackageExporter` trap with the compiler taken away.

The rule: **follow who reads the field, not what type it is declared on.** A
list of line numbers is exactly the wrong handover for this, because it invites
gating by grep — which is how this one was nearly done. (Found by Service &
Parts reading the caller, 2026-09-30, on a list of six fields from this project
that contained one input record and two internal ones.)

**And null still means two things on purpose** — withheld, or never recorded.
The server does not distinguish them, because to a caller who may not see the
figure they are the same answer. The browser is told which it is by the
permission list on `/auth/me` (ADR-025) and leaves the field out rather than
drawing an empty one.

## Consequences

**This is a security decision, said out loud.** Unlike dimension 1, which needed
nothing new, this changes the shape of `AuthorizedScope` and `UserAssignment` —
both Identity internals under ADR-017. The commit implementing it says so, and
any widening of Identity's public surface extends the allow-list in
`tests/Architecture/BoundaryTests.cs` deliberately rather than to make a test
pass.

**Existing assignments are unaffected.** `OwnRecordsOnly` is an additive column
defaulting false, so everybody already assigned keeps exactly today's reach. The
right has to be granted before anybody is narrowed, which is the safe direction
for a change that takes access away.

**Grants combine additively, as they already do.** A caller is limited to their
own records only when *every* covering assignment says so; one broader grant
wins. That matches `GetAuthorizedScopeAsync`'s existing rule — any
organization-wide assignment beats a set of rooftops — and avoids the trap where
adding a narrow second role silently removes reach the first one gave.

**Good.** Dimensions 3 and 4 stop costing 39 sites each: the sites already call a
method that knows everything, so a later dimension changes one method rather
than every caller. ADR-029's "cannot be forgotten" becomes true instead of
aspirational.

**Bad.** Getting there touches those 39 sites once, mechanically, and a
mechanical edit across four capabilities is where a wrong one hides. The
`Covers`/`Allows` split is the mitigation — a site that genuinely has no owner
keeps calling `Covers` and says why — but it is a judgement per site, not a
compiler rule.

**Still not guaranteed.** A capability added next year must still call `Allows`
rather than `Covers` where a record has an owner. Nothing makes the wrong choice
a compile error. Saying so here rather than claiming otherwise is the whole
reason ADR-029 needed replacing.
