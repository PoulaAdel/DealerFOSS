# Inventory

A specific vehicle standing on a specific rooftop's lot, with a stock number, a
status, and a cost. It answers "whose lot is it on, and what state is it in?" —
the question [Vehicles](../Vehicles/README.md) deliberately does not answer.

**This is the rooftop-scoped half of the pair, and the scope is a permission
boundary** ([doc 04 §1, §3](../../../docs/04-Data-and-Tenancy.md)). A user assigned
to one location must never see or move another location's stock. Unlike
`Customer.HomeRooftopId`, `InventoryUnit.RooftopId` really does control access.

## Layout

| File | Role |
|---|---|
| `InventoryUnit.cs` | the record and its rules; no EF or ASP.NET dependency |
| `InventoryStatus.cs` | the statuses and the legal moves between them |
| `InventoryStatusChange.cs` | one line of a unit's history |
| `InventoryService.cs` | list, read, receive, and move stock — **where the rooftop scope is enforced** |
| `InventoryEndpoints.cs` | HTTP surface under `/api/v1/inventory` |
| `InventoryTables.cs` | EF configuration; shares the `vehicles` schema |
| `IInventory.cs` | what other capabilities may call, and the read models |

## Three rules worth knowing before you change anything

**Every read is filtered to the caller's authorized rooftops, in the query.** Not
in the results — another rooftop's rows must never be read, let alone returned. An
unauthorized unit and an unknown one return the same failure, so a response cannot
be used to discover what another location has in stock. Removing either check
fails `InventoryTests`.

**History is append-only.** Every status change writes a line, and `TenantDb`
refuses to update or delete one. A wrong move is corrected by making the opposite
move with a reason, not by editing history. A sold car whose deal falls through
goes back to `Available` — a recorded event, not an erased one.

**A stock number is unique within a rooftop, not across the group.** Two locations
may each write "A1234" on a windscreen; one location may not write it twice.

## Statuses

`Incoming → Reconditioning → Available → OnHold → Sold`, with `Removed` as the end
of the line. `InventoryStatusRules` holds the legal moves, and a refused move names
the ones that are available instead. A removed unit does not come back by changing
status — it is received again, so its second stay has its own history and its own
cost.

## Permissions

`Inventory.Read` to see a lot's stock; `Inventory.Manage` to receive stock and move
it. Checked separately, which is what makes "can see the stock but cannot move it"
a real assertion in the tests.

## Boundaries (enforced by `tests/Architecture`)

- Entities have no EF or ASP.NET dependency.
- Inventory may depend on Vehicles — a unit is a vehicle on a lot — but not on
  Organization or Customers.

## Acquisition cost on the stock list

The paged summary and the detail both carry `CostAmount` and `CostCurrency`.
The stock screen labels this **Acquisition cost**, formats each car in its recorded
currency, and shows an unrecorded cost as unknown rather than zero. The list does
not fetch each detail to find its cost, or add a page of mixed currencies into a
figure that looks like the value of the whole lot.

This is not a per-car ledger balance. Checked 2026-09-19: internal reconditioning
debits account 1300, but does not update the unit's cost or retain its stock-unit
id on that posting. Attributing recon to each stay in stock, including correction
and sale, remains open in the scope register. The old claim that the unit's cost
already included recon was broader than the implementation.

## What is still missing

Transfers between rooftops and recon-inclusive carrying cost per car. The old
remainder also named aging, floorplan financing and reservation: aging now exists
through `AgingAsync`, a financed acquisition credits floorplan payable, and the
deal workflow holds a car. Those are no longer wholly unbuilt capabilities.
