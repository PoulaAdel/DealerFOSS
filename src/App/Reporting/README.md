# Reporting

One question — *how did we do this month?* — answered in one round trip.

This capability owns **no table, no entity, and no migration**. It composes what
Accounting and Inventory already publish. That is the whole design: a dashboard is
a way of arranging figures, not a second place to compute them.

## Layout

| File | Role |
|---|---|
| `IReporting.cs` | the contract, and the shape a month comes back in |
| `ReportingService.cs` | composition, and the partial-permission rule |
| `ReportingEndpoints.cs` | HTTP surface under `/api/v1/reporting` |

## Where each figure actually comes from

| On the screen | Computed by | Source |
|---|---|---|
| Front, back, and service gross | `IAccounting.PerformanceAsync` | journal lines |
| Cars delivered, jobs invoiced | `IAccounting.PerformanceAsync` | journal entries, by source |
| Stock aging | `IInventory.AgingAsync` | acquisition dates |
| Whether the month is locked | `IAccounting.ListPeriodsAsync` | accounting periods |

**Gross is derived from the ledger, not from the deals.** A dashboard that added
deal totals up would be a second opinion about the same month, and the two would
eventually disagree — at which point nobody could say which was right. Reading the
same journal lines the trial balance reads means they cannot.

The department totals belong to Accounting because working them out means knowing
that 4000 is a vehicle sale and 5000 is what it cost. Aging belongs to Inventory
because it means knowing which statuses still count as stock. Neither rule is
repeated here.

## Permission is per panel, not per page

Every section is nullable and `Withheld` names the missing ones. A salesperson with
`Inventory.Read` and no `Accounting.Read` sees the stock panel and is told, in
words, why the money is absent.

A refusal from a section becomes an absence; **every other failure is reported as
itself**. Two currencies in the ledger is a fact about the data and the reader has
to be told; "you may not see this" is a fact about the reader. Only the second one
is swallowed.

When a caller may see nothing at all the call fails with `reporting.forbidden`, so
an empty dashboard never silently means a forbidden one.

## The comparison

`PriorMonth` is the same query over the previous month. A gross figure on its own
tells nobody whether it was a good month, and the screen would otherwise be
inviting a manager to remember last month's number.

It is allowed to be absent on its own account — a month back then that mixed
currencies must not blank out this one.

## Stock is aged as at the right day

Today, for the current month. The cutoff, for a past one. Asking how old the stock
was last March and being shown today's ages would be a plainly wrong answer to a
question somebody asked in good faith.

## Boundaries (enforced by `tests/Architecture`)

- No `TenantDb`. Every figure arrives through a published contract, so every
  permission check happens inside the capability that owns the data.
- No entity of any other capability — `Account`, `JournalEntry`, `InventoryUnit`
  are all off limits.

A dashboard that queried the tables directly would be a way to read past a rooftop
scope, and it would be the last place anybody thought to look for one.

## Not built yet

- **Per-rooftop breakdown in one view.** The query takes a rooftop, so a group must
  ask once per location rather than seeing them side by side.
- **Salesperson and advisor league tables.** The data exists on deals and repair
  orders; nothing composes it.
- **Anything daily.** This is a month, and a month only.
