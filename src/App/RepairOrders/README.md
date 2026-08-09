# RepairOrders — the workshop

A car is expected, it comes in, somebody works on it, the customer pays. This
capability owns the `service` schema and nothing else.

## What it does

- **Takes a booking** for a car that is not here yet, and records what each day
  is committed to. See "The diary" below.
- **Books a car in** against a customer and *their own vehicle* — not a unit in
  stock. A customer's car is not on anybody's lot, and modelling service against
  inventory would make the capability unusable the day after the warranty runs
  out.
- **Records the work** as lines: labour (hours × rate), parts, and sublet.
- **Records what the customer said** about work found mid-job.
- **Invoices**, which posts to the ledger in the same transaction.

Every job belongs to one rooftop, and that is a permission boundary
(doc 04 §1). The customer and the car are shared across the organization; the
job is not.

## The rule this capability exists to hold

**A job cannot be invoiced while any line is still waiting on the customer.**

A technician strips a wheel off and finds the discs are gone. That is real work
and it should be recorded immediately — but nobody may bill it until somebody has
actually asked. So:

- a line added while the job is still **Booked** is what the customer came in for,
  and is authorized on arrival;
- a line added once the job is **InProgress** was found, and starts **Pending**;
- a Pending line **blocks the invoice**, and says so by name in the refusal;
- **Declined is a perfectly good answer.** The line stays on the record at nil,
  which is what makes "we did offer" provable a year later when the same fault
  brings the same car back.

The check lives on the entity (`RepairOrder.EnsureNothingIsUnanswered`), not only
in the service, so a background job or an import cannot route around it.

## Segregation of duties, and where it deliberately stops

`Service.Write` writes work up. `Service.Authorize` records what the customer
said. They are separate permissions because noticing that the discs are gone and
having the conversation about paying for them are different acts, and only the
second may put money on a bill.

**Unlike `Deals.Approve`, there is no ban on the same person doing both.** In an
independent workshop the advisor who spots the work is usually the one who picks
up the phone; forbidding that would stop real shops working. The control is that
the answer is a distinct, permissioned, timestamped act with a note saying how it
was obtained — not that two different people must perform it.

The seeded `Technician` role holds `Service.Write` and not `Service.Authorize`,
which is what makes the split testable rather than a claim in this file.

## The diary

A booking is **a promise that a car will arrive**, and it is deliberately not a
repair order with an earlier date on it.

```
Scheduled ──► Arrived      (opens the job, and records which one)
    │
    ├───────► NoShow       (silence)
    └───────► Cancelled    (the customer told us)
```

**A promise becomes a job exactly once.** Arriving is refused the second time,
naming the job that already exists. This is what makes "Appointment → RO
reconciles to its source" true rather than asserted: the arrival opens the repair
order and links it **inside one transaction**, so there can never be an arrival
with no job or a job the diary has lost. Removing that transaction has been
rehearsed — it leaves two repair orders against one car.

**A car that never came is recorded, not deleted.** NoShow and Cancelled are kept
apart because one is silence and the other is the customer ringing, and the
difference is exactly what tells a manager who to remind the day before.

**Capacity is reported, not enforced.** The diary returns each day's committed
hours alongside the bookings, and refuses nothing. Real shops overbook on
purpose; a diary that refused at eight hours would be worked around within a week
by booking everything as an estimate of zero, which would make the figure
useless. An arrived car stops counting — its hours belong to its job, and
counting both would show the shop as twice as busy as it is.

Booking takes `Service.Write`, the same right that opens a job. Arriving a car
*is* opening a job, so a weaker booking permission would be a route to a stronger
one.

## Statuses

```
Booked ──► InProgress ──► Completed ──► Invoiced
   │            │              │
   └────────────┴──► Cancelled └──► InProgress   (sent back; reopens the lines)
```

Lines are editable while **Booked** or **InProgress**. Once **Completed** they are
frozen, and changing them means sending the job back — a recorded move somebody
has to make deliberately. That mirrors the Draft boundary on a deal, for the same
reason: a bill that can be edited quietly is not a bill.

## What this is not

Named rather than hidden, because a service module that pretends to be complete
is worse than one that says where it stops:

- ~~No parts inventory~~ — built, and this entry is kept struck through rather
  than deleted because it was wrong for weeks after it stopped being true. A line
  may name a catalogue part, and invoicing issues it from stock at cost inside
  the same transaction as the ledger posting, so **service does have a
  gross-profit figure**. A line with no `partId` is still legitimate — a one-off
  item bought for a single job never enters the catalogue — and that line simply
  carries no cost.
- **No estimate versus actual.** One set of numbers, which is what is billed.
- **No labour operation catalogue**, no flat-rate times, no technician clocking,
  and therefore no efficiency or productivity reporting.
- **No warranty claims**, no internal jobs, and no split-pay across customer,
  warranty, and internal on the same job.
- **No technician-level scheduling.** The diary loads a *workshop*, not a person
  or a ramp. "Which technician is free at eleven" is a different model and is not
  answered here.
- **No reminders.** The diary knows who is expected tomorrow and sends nobody a
  message about it; there is no communications channel yet (roadmap I5 lists one
  as provider-neutral, and none is built).
- **No multi-line invoice document**, and no printing.

## Files

| File | Job |
|---|---|
| `Appointment.cs` | a promise, and the rule that it becomes one job |
| `AppointmentStatus.cs` | the four states, and which of them count against a day |
| `IAppointments.cs` | what other capabilities may call, and the diary's shape |
| `AppointmentService.cs` | scope, permissions, and the arrival transaction |
| `AppointmentTables.cs` | how a booking is stored |
| `AppointmentEndpoints.cs` | the HTTP surface for the diary |
| `RepairOrder.cs` | the rules: what may be added when, and what blocks an invoice |
| `ServiceLine.cs` | one piece of work, and its authorization state |
| `RepairOrderStatus.cs` | the statuses, the legal moves, and the two line enums |
| `RepairOrderStatusChange.cs` | append-only history (ADR-016) |
| `IRepairOrders.cs` | what other capabilities may call |
| `RepairOrderService.cs` | scope, permissions, and the ledger posting |
| `RepairOrderTables.cs` | how it is stored |
| `RepairOrderEndpoints.cs` | the HTTP surface |
