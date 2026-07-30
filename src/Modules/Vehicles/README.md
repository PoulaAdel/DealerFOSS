# Vehicles module

Vehicles, and what is standing on each lot
([doc 04 §3](../../../docs/04-Data-and-Tenancy.md)).

The module holds two things that are deliberately not the same thing:

| | What it answers | Scope |
|---|---|---|
| `Vehicle` | "What car is this?" — VIN, year, make, model | **Organization-shared** |
| `InventoryUnit` | "Whose lot is it on, what state is it in, what did it cost?" | **Rooftop-owned** |

A vehicle is bought at one location, serviced at another, and traded back in at a
third. Hiding it by location would make staff record it three times, so vehicles
are shared. A *unit* is a specific car on a specific lot — that is a permission
boundary, and one location must not see or move another's stock.

## Layout

| Path | Role |
|---|---|
| `Domain/` | `Vehicle`, `Vin`, `InventoryUnit`, `InventoryStatus` + transition rules, `InventoryStatusChange` — entities and rules; no EF/ASP.NET dependency |
| `Data/` | `VehiclesDbContext` (owns the `vehicles` schema), migrations, design-time factory |
| `Contracts/` | `IVehicleDirectory`, `IInventoryDirectory` + read models — the only surface other modules use (ADR-008) |
| `VehicleService.cs` | search, read, and record a vehicle |
| `InventoryService.cs` | list, read, receive, and move stock — with the rooftop scope |
| `VehiclesEndpoints.cs` | HTTP surface under `/api/v1/vehicles` and `/api/v1/inventory` |
| `VehiclesModule.cs` | DI registration + endpoint mapping |

## Three rules worth knowing before you change anything

**A VIN is not a unique key.** The same physical vehicle legitimately reappears —
sold, then taken back as a trade-in years later — and imported data contains
mistyped numbers. There is deliberately no unique index on VIN. Duplicates are
resolved as a workflow, not by a constraint ([doc 04 §4](../../../docs/04-Data-and-Tenancy.md)).

**A VIN that fails the standard shape is recordable with a reason.** Pre-1981
vehicles, imports, trailers, and equipment genuinely have shorter or oddly shaped
numbers. Refusing them outright is what pushes staff into typing a fake VIN — the
exact data problem the rule was meant to prevent. `Vehicle.Record` accepts a
non-standard VIN only when `vinExceptionReason` is given, and keeps that sentence
on the record. The North American check-digit rule is **not** enforced: it does not
hold worldwide.

**A unit's history is append-only.** Every status change writes a line, and
`VehiclesDbContext` refuses to update or delete one. A wrong move is corrected by
making the opposite move with a reason, not by editing history. A sold car whose
deal falls through goes back to `Available` — a recorded event, not an erased one.

## Statuses

`Incoming → Reconditioning → Available → OnHold → Sold`, with `Removed` as the
end of the line. `InventoryStatusRules` holds the legal moves; a refused move
names the ones that are available instead. A removed unit does not come back by
changing status — it is received again, so its second stay has its own history
and its own cost.

## Permissions

| Permission | Covers |
|---|---|
| `Vehicles.Read` | reading vehicles; organization-wide, like the vehicles themselves |
| `Inventory.Read` | seeing a lot's stock; held per rooftop, or organization-wide |
| `Inventory.Manage` | receiving stock, moving its status, and recording a vehicle |

They are checked separately on purpose. The development `Advisor` role holds the
first two and is scoped to one rooftop, which is what makes "can see the stock but
cannot move it" and "cannot see the other lot" real assertions in the tests.

## Boundaries (enforced by `tests/Architecture`)

- Domain has no EF or ASP.NET dependency.
- The module owns the `vehicles` schema and reads no other module's tables.
- Identity is reachable only through its `Contracts` namespace.
- This module does not reference the Host, Organization, or Customers — Sales will
  depend on it later, so a reference back would be circular.

## Not built yet

Pricing and price history, aging analytics, vehicle images, options and equipment
decoding, transfers between rooftops, and any incoming provider feed. Those arrive
with reporting and the integration runtime
([doc 05](../../../docs/05-Integration-Framework.md)).
