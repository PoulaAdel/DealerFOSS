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
| `Financing.cs` | the instalment structure, and the payment arithmetic |
| `RegistrationAddress.cs` | where the car will be registered or garaged (ADR-024) |
| `DealTaxLine.cs` | one tax charged, with `TaxAddress`, the narrower snapshot it was resolved from |
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

## The financing

`Deal.Financing`, set through `POST /{id}/financing`, records four figures and
nothing else: a finance provider's name, the cash down, an annual percentage rate
and a term in months. Everything else is worked out from them:

```
amount financed   = amount due − cash down
monthly payment   = P·i·(1+i)^n / ((1+i)^n − 1)      i = APR ÷ 12
                  = P ÷ n                            at 0%
final payment     = whatever clears the balance
finance charge    = total of payments − amount financed
```

**The financing is not inside `AmountDue`, and that is the rule to know before
changing anything here.** A down payment is *how* the customer pays, not a
reduction in what they owe: the receivable opens at the full amount due and the
down payment settles part of it like any other receipt. Netting it would make the
same money disappear twice, and it would break both of the "read the column down
and reach the total" tests at once. On the deal desk and on the printed order the
payment terms therefore sit in their own table **below** the total.

**The payment is derived every time it is read, never stored.** The amount
financed follows from `AmountDue`, and two stored figures with a stored difference
between them is one figure too many — the same rule as `DealProduct.Gross`.

**The schedule is walked, not multiplied.** A rounded payment times the term is
not what a customer pays; the last instalment clears the balance. `PlanFor` walks
the months in `decimal` so the balance provably reaches zero, and
`DealFinancingTests.The_schedule_pays_the_loan_off_exactly` re-amortises the plan
independently and asserts it.

**The rate is a fraction**, `decimal(9,6)`, the same convention and precision as a
tax rate: `0.0649` is 6.49%. Anything at or above 1 is refused, because a rate
typed as `6.49` is the one data-entry slip this field will actually see. The
screen's box asks for a percentage and converts once, on save.

**No lender is involved.** A monthly payment is arithmetic over an amount
financed, a rate and a term, so recording what was agreed needs nothing outside
this installation. The provider here is a name the dealership typed; nothing is
sent anywhere and no decision is received.

## The registration address

`Deal.RegistrationAddress`, set through `POST /{id}/registration-address`, is
where the buyer will register or garage the car — the fact ADR-024 traces tax
to, not the customer's own mailing address and not the dealer's location. It is
a deal field on purpose: a customer who moves house afterwards must not
retroactively change what a past sale was taxed at. It freezes with the rest of
the deal's terms once submitted, the same rule as the charges and the tax lines.
`TaxAddress` next to it in `DealTaxLine.cs` is a different, narrower thing — the
four fields a rate needs, snapshotted onto the tax lines once resolved.

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
- Deals depends on `IAccounting` (delivering a deal posts the sale) and
  `IFinanceProducts` (the F&I catalogue). `Documents` depends on `IDeals`, for
  the printed order — the direction this used to say would run the other way.

## Not built yet, and deliberately

**The approver is not required to be someone other than the author.** The
permission split is the control today; a same-person check needs a policy decision
about single-person rooftops before it can be enforced.

Also absent: **lender submission and decisions** (the structure agreed is now
recorded — see "The financing" above — but no application is ever *sent* to a
lender and no decision is ever received; beyond that a lender is a name on the
deal and a payment method on a receivable), **dealer reserve** (what the
dealership earns on the finance itself is not recorded, and where it would post is
Accounting's question), **financing reworked after approval** (it freezes with the
rest of the numbers, so a change means sending the deal back to Draft),
**payment frequencies other than monthly**, **balloon and lease structures**,
**tax and fee rule packs** (a person still
types the tax; there is no rate table and no SST pack, so nothing computes
ADR-024's basis-times-rate automatically), **deal versions
for desking iterations** (one live set of terms, not a negotiation history),
**signatures** (the order prints — `GET /api/v1/documents/deals/{id}` — but
nothing captures a signature on it), and **commission**. One trade-in per deal,
which covers the overwhelming majority.

~~F&I products and menus~~ and ~~accounting postings~~ are both built and are
struck through here rather than removed, for the same reason RepairOrders keeps
its own corrections visible: a product is sold on a deal as its own
`DealProduct`, at a price and cost negotiated for that deal, through
`IFinanceProducts`'s catalogue; delivering a deal posts the sale to the ledger
through `IAccounting` in the same transaction as the inventory hold being
released.
