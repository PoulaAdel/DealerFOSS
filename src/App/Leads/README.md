# Leads

Somebody who might buy a car, and the record of chasing them. The first thing a
salesperson touches in the morning and the last thing they check at night.

**A lead belongs to one rooftop, and that is a permission boundary**
([doc 04 §1](../../../docs/04-Data-and-Tenancy.md)). A salesperson at one location
must not see another location's enquiries — the branch manager's numbers depend on
it, and so does the customer's experience of not being rung by two salespeople
from the same group.

The *customer* is organization-shared; only the *enquiry* is local. The same
person legitimately has an open lead at two rooftops, and the group needs to be
able to see that rather than prevent it.

## Layout

| File | Role |
|---|---|
| `Lead.cs` | the record and its rules; no EF or ASP.NET dependency |
| `LeadStatus.cs` | the statuses, the legal moves between them, and lead sources |
| `LeadStatusChange.cs` | one line of history; append-only |
| `LeadService.cs` | the workflows — **where the rooftop scope is enforced** |
| `LeadEndpoints.cs` | HTTP surface under `/api/v1/leads` |
| `LeadTables.cs` | EF configuration; owns the `leads` schema |
| `ILeads.cs` | what other capabilities may call, and the read models |

## Three rules worth knowing before you change anything

**Customers and vehicles are reached only through their contracts.** `LeadService`
takes `ICustomers` and `IVehicles`, never `TenantDb.Customers` and never the
`Customer` type. An architecture test names those entity types explicitly and
fails the build if a lead touches one — which is what keeps the boundary real now
that every capability shares a project ([ADR-017](../../../docs/adr/0017-three-projects-flat-features.md)).

**The customer's name is resolved, never copied.** A lead list asks Customers for
the names of the customers on that page, in one query. Keeping a copy on the lead
would make every screen show whatever the name was on the day the enquiry arrived.
The car of interest works the same way, through `IVehicles.GetManyAsync` — one
query for the page, not one per row.

**The screen is told which moves are legal; it does not work them out.**
`LeadDetail.AvailableMoves` is `LeadStatusRules.MovesFrom` and nothing else, so a
browser offers exactly what the domain allows. Do not let a screen grow its own
transition table: two copies drift, and the one people see is the wrong one.

**A lost lead can come back.** They were not ready in March and walked in again in
June — that is the same enquiry continuing, and reopening it keeps the history of
the first attempt. Won is the end of the line; a second purchase is a second
enquiry.

## Statuses

`New → Working → Appointment → Won`, with `Lost` reachable from anywhere open and
reopening to `Working`. `LeadStatusRules` holds the legal moves, and a refused
move names the ones available instead.

Assignment is deliberately **not** a status change: who owns a lead and how far
along it is are different questions, and conflating them puts noise in the history.

## Permissions

`Leads.Read` to see a lot's enquiries; `Leads.Manage` to capture one, move it on,
or reassign it. Checked separately, so "can see the board but cannot work it" is a
real role rather than an assumption.

Reading leads also requires `Customers.Read`, because a lead list without customer
names is not usable. That dependency is intentional and visible: the call goes
through `ICustomers`, so it is checked by the Customers capability's own rules.

## Boundaries (enforced by `tests/Architecture`)

- Entities have no EF or ASP.NET dependency.
- This capability owns the `leads` schema and reads no other's tables.
- Leads may use `ICustomers` and `IVehicles`, but not `Customer`, `ContactPoint`,
  `Vehicle`, or `InventoryUnit`.
- Nothing else depends on Leads. Sales will, when it exists.

## Not built yet

Appointments as records, activities and reminders beyond a note on each status
change, lead-source ROI reporting, automatic assignment rules, duplicate-lead
detection, and any inbound feed from a marketplace or the dealership website.

**Assignment to a named colleague.** `AssignAsync` takes any user id and the
screen can only send the caller's own or null, because nothing in the product
lists staff. That is a missing capability, not a missing parameter — see the next
milestone in `docs/implementation/STATUS.md`.
