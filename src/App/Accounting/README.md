# Accounting

The ledger behind a sale. When a car leaves the lot, this is what records that it
happened in money rather than in inventory.

**This is a sale ledger, not a general ledger.** Read "Not built yet" before
assuming anything else works — the gap between the two is large and deliberate.

## Layout

| File | Role |
|---|---|
| `Account.cs` | one line of the chart of accounts, and the account codes a sale uses |
| `JournalEntry.cs` | one balanced event; refuses to exist unless it balances |
| `JournalLine.cs` | one side of one account movement |
| `AccountingService.cs` | scoped reads, the posting map, and reversal |
| `AccountingEndpoints.cs` | HTTP surface under `/api/v1/accounting` |
| `AccountingTables.cs` | EF configuration; owns the `accounting` schema |
| `IAccounting.cs` | what other capabilities may call |

## Three rules, and none of them bend

**An entry balances or it does not exist.** Debits must equal credits, there must
be at least two lines, and a line is a debit or a credit — never both, never
negative. The check lives in `JournalEntry.Post`, so it holds for every caller.
Rehearsed: putting the posting map one penny out fails eight tests **and stops the
car being sold**, because the posting runs inside the delivery's transaction.

**Nothing is ever edited or deleted.** `JournalEntry` and `JournalLine` are marked
`IAppendOnly`, which `TenantDb` enforces. A mistake is corrected by posting the
reversal — the same lines with the sides swapped — which leaves both the error and
the correction visible. That is the only way an auditor can tell "this was always
right" from "somebody changed it afterwards". An entry cannot be reversed twice,
and a reversal cannot itself be reversed.

**A reversal states why.** An unexplained one is indistinguishable from a mistake.

## What a delivery posts

For a car sold at `price`, with `fees`, a `discount`, a trade worth `allowance`
with `payoff` still owed on it, and a vehicle that cost the dealership `cost`:

| | Debit | Credit |
|---|---|---|
| Cash | amount due from the customer | payoff paid to settle their trade |
| Trade-in inventory | allowance | |
| Sales discounts | discount | |
| Vehicle sales | | price |
| Fee income | | fees |
| Cost of vehicle sales | cost | |
| Vehicle inventory | | cost |

Cash appears on both sides on purpose: money comes in from the customer and goes
back out to the trade's lender, and showing both is more useful than netting them.

The last two lines are what make gross profit visible — revenue without a cost of
sale is a number nobody can check.

`AccountingService.BuildDeliveryLines` is the single place that decides this. When
a configurable chart of accounts arrives it becomes configuration; until then it
is deliberately one readable method rather than scattered constants.

## Scope and permissions

Entries record **both** the legal entity that owns the money and the rooftop that
generated it. The legal entity is resolved through `IOrganization` — money belongs
to a company, not to a building, and this cannot be backfilled later because the
rows are immutable.

`Accounting.Read` to look; `Accounting.Post` to put something in or reverse it.
The development `Salesperson` role holds both, because delivering a car posts the
sale — but only a manager should be reversing anything, which is a policy the
roles express rather than the code.

## Boundaries (enforced by `tests/Architecture`)

- Entities have no EF or ASP.NET dependency.
- This capability owns the `accounting` schema and reads no other's tables.
- Accounting may use `IOrganization`, but not `Rooftop` or `LegalEntity`.
- Accounting knows nothing about deals, customers, or inventory. Deals restates a
  sale in ledger terms before calling it, so neither side has to learn the other's
  vocabulary.

## The closing period, decided but not yet enforced

The maintainer had no strong preference here, so the default is standard
franchised-dealer practice:

- **Close monthly, on the calendar month end.** Dealerships close hard and fast
  because manufacturers want a financial statement within roughly the first ten
  days of the following month.
- **Fiscal year is the calendar year.**
- **A grace window** — prior-month entries may post until the 10th of the
  following month, after which the period locks and a correction has to be made
  in the open period instead.
- **Per organization**, because a group with several franchises may be pushed to
  a different rhythm by a manufacturer.

> This is general industry practice written down as a starting point, not advice
> checked against any specific franchise agreement. It should be confirmed with a
> dealer's own accountant before a pilot, and it is deliberately configuration
> rather than a constant so that confirming it is cheap.

**None of this is enforced yet.** Nothing currently stops an entry posting into a
month somebody already reported on. The decision is recorded here so the period
model is designed once, correctly — journal rows are immutable, so a period
column cannot be backfilled onto them later.

## Not built yet — and this list matters

- **Accounting periods, closing, and locking**, as above.
- ~~A trial balance~~ — built. `GET /api/v1/accounting/balances` totals every
  account over a period, states each balance on the account's normal side, and
  reports whether the two columns agree. Rooftop-scoped like everything else: a
  total is as revealing as the entries behind it. It refuses to add up two
  currencies rather than printing a meaningless number.
- **Finance.** The posting assumes the customer pays in full. A financed sale
  should create a receivable from the lender, and does not.
- **Tax.** No sales tax, registration, or title fees.
- **Floor-plan payoff** on the dealership's own inventory.
- **A configurable chart of accounts**, account administration, or manual journals.
- **Service, parts, and payroll** postings.

Until those exist this is an honest record of vehicle sales, not a set of books.
