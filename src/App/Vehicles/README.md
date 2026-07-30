# Vehicles

A vehicle as an **identity** — VIN, year, make, model. It answers "what car is
this?", not "whose lot is it on". That second question belongs to
[Inventory](../Inventory/README.md), which is a separate folder because it is a
separate scope ([doc 04 §1](../../../docs/04-Data-and-Tenancy.md)).

**A vehicle belongs to the whole dealer organization.** The same car is bought at
one location, serviced at another, and traded back in at a third. Hiding it by
location would make staff record it three times.

## Layout

| File | Role |
|---|---|
| `Vehicle.cs` | the record and its rules; no EF or ASP.NET dependency |
| `Vin.cs` | normalizing and checking a VIN |
| `VehicleService.cs` | search, read, and record, with the permission checks |
| `VehicleEndpoints.cs` | HTTP surface under `/api/v1/vehicles` |
| `VehicleTables.cs` | EF configuration; owns the `vehicles` schema, shared with Inventory |
| `IVehicles.cs` | what other capabilities may call, and the read models |

## Two rules worth knowing before you change anything

**A VIN is not a unique key.** The same physical vehicle legitimately reappears —
sold, then taken back as a trade-in years later — and imported data contains
mistyped numbers. There is deliberately no unique index on VIN. Duplicates are
resolved as a workflow, not by a constraint
([doc 04 §4](../../../docs/04-Data-and-Tenancy.md)).

**A VIN that fails the standard shape is recordable with a reason.** Pre-1981
vehicles, imports, trailers, and equipment genuinely have shorter or oddly shaped
numbers. Refusing them outright is what pushes staff into typing a fake VIN — the
exact data problem the rule was meant to prevent. `Vehicle.Record` accepts a
non-standard VIN only when `vinExceptionReason` is given, and keeps that sentence
on the record. The North American check-digit rule is **not** enforced: it does not
hold worldwide.

## Permissions

`Vehicles.Read` to read. Recording a vehicle requires `Inventory.Manage` — it is a
stock action, and giving it its own permission would create a right nobody holds.

## Boundaries (enforced by `tests/Architecture`)

- Entities have no EF or ASP.NET dependency.
- Nothing here reaches into Organization, Customers, or **Inventory**. The
  dependency runs the other way: a unit knows its vehicle, not the reverse.

## Not built yet

Pricing and price history, vehicle images, options and equipment decoding, and any
incoming provider feed. Those arrive with reporting and the integration runtime
([doc 05](../../../docs/05-Integration-Framework.md)).
