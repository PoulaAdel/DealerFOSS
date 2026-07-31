# Deals

One customer buying one car, at a price, with a trade-in and an approval. This is
the point the whole system exists to reach.

**A deal belongs to one rooftop, and that is a permission boundary**
([doc 04 §1](../../../docs/04-Data-and-Tenancy.md)). The customer is shared across
the organization; the deal is not.

## Layout

| File | Role |
|---|---|
| `Deal.cs` | the record and its rules; no EF or ASP.NET dependency |
| `DealStatus.cs` | the statuses, the legal moves, and the kinds of charge |
| `DealCharge.cs` | one line on the deal, with the sign rules |
| `TradeIn.cs` | the old car: allowance, payoff, equity |
| `DealStatusChange.cs` | one line of history; append-only |
| `DealService.cs` | the workflows — **rooftop scope, approval split, inventory hold** |
| `DealEndpoints.cs` | HTTP surface under `/api/v1/deals` |
| `DealTables.cs` | EF configuration; owns the `deals` schema |
| `IDeals.cs` | what other capabilities may call, and the read models |

## Four rules worth knowing before you change anything

**The numbers freeze when the deal leaves Draft.** A price that can change after a
manager approved it makes the approval worthless. Changing an approved deal means
moving it back to Draft — a recorded move somebody has to make deliberately, which
also withdraws the approval. Leaving the old approver on a repriced deal would be
a lie about who agreed to the new numbers.

**Writing a deal and approving one are separate permissions.** `Deals.Write`
builds it; `Deals.Approve` signs it off. The development `Salesperson` account
holds the first and not the second, which is what makes segregation of duties a
thing the tests assert rather than a claim in a document.

**Starting a deal holds the car.** The unit must be `Available`; starting a deal
moves it to `OnHold`, delivering moves it to `Sold`, and cancelling puts it back to
`Available`. That is what stops the same car being sold twice — the second deal
finds it held and is refused. The deal and the hold are committed in **one
transaction**, because a deal whose car is not held, or a held car with no deal,
are both worse than a failed request.

**A discount is stored negative.** The subtotal is then a plain sum. Storing it
positive and remembering to subtract it is how a total ends up right on one screen
and wrong on another.

## The money

```
subtotal   = sum of all charges (vehicle price + fees + accessories − discounts)
amount due = subtotal − trade allowance + trade payoff
```

Allowance and payoff stay separate and both positive. Netting them loses negative
equity — the customer owing more on the trade than it is worth — which is the
single most common source of an argument at the desk, so it is named rather than
hidden.

Every amount on a deal shares the deal's currency, so a total can never mix two.

## Statuses

`Draft → Submitted → Approved → Delivered`, with `Cancelled` reachable from any
open state and `Submitted → Draft` for "send it back and reprice it". Delivered
and Cancelled are the end of the line.

Each history entry records the amount at that moment, so an approval records the
number that was approved rather than whatever the total says today.

## Boundaries (enforced by `tests/Architecture`)

- Entities have no EF or ASP.NET dependency.
- This capability owns the `deals` schema and reads no other's tables.
- Deals may use `ICustomers` and `IInventory`, but not `Customer`, `Vehicle`, or
  `InventoryUnit`.
- Nothing else depends on Deals yet. Finance and Accounting will.

## Not built yet, and deliberately

**The approver is not required to be someone other than the author.** The
permission split is the control today; a same-person check needs a policy decision
about single-person rooftops before it can be enforced.

Also absent: lender submission and decisions, F&I products and menus, tax and fee
rule packs, deal versions for desking iterations, paperwork and signatures,
accounting postings, and commission. One trade-in per deal, which covers the
overwhelming majority.
