# RepairOrders — the workshop

A car comes in, somebody works on it, the customer pays. This capability owns the
`service` schema and nothing else.

## What it does

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

- **No parts inventory.** A part on a job is a description and a price. Nothing
  is reserved, ordered, or relieved from stock — so the ledger records service
  **revenue** and no cost, and **gross profit on service does not exist yet**.
  Inventing a cost figure would be worse than the gap.
- **No estimate versus actual.** One set of numbers, which is what is billed.
- **No labour operation catalogue**, no flat-rate times, no technician clocking,
  and therefore no efficiency or productivity reporting.
- **No warranty claims**, no internal jobs, and no split-pay across customer,
  warranty, and internal on the same job.
- **No appointments or workshop loading.** A job is booked in when the car is
  there.
- **No multi-line invoice document**, and no printing.
- **No screen** — the API is complete and nothing drives it yet.

## Files

| File | Job |
|---|---|
| `RepairOrder.cs` | the rules: what may be added when, and what blocks an invoice |
| `ServiceLine.cs` | one piece of work, and its authorization state |
| `RepairOrderStatus.cs` | the statuses, the legal moves, and the two line enums |
| `RepairOrderStatusChange.cs` | append-only history (ADR-016) |
| `IRepairOrders.cs` | what other capabilities may call |
| `RepairOrderService.cs` | scope, permissions, and the ledger posting |
| `RepairOrderTables.cs` | how it is stored |
| `RepairOrderEndpoints.cs` | the HTTP surface |
