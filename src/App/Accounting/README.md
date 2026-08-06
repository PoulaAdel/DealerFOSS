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

**Answered by the maintainer, 2026-08-06.** This supersedes an earlier assumed
rule, and the difference is not cosmetic — see the warning below.

- **The cutoff is the calendar month end**, the 30th or the 31st. That is the
  line transactions fall on one side or the other of.
- **The close then runs over the next few business days.** Accounts are
  reconciled, adjustments posted, statements reviewed — and only then is the
  month locked.
- **Fiscal year is the calendar year.**
- **Per organization**, because a group with several franchises may be pushed to
  a different rhythm by a manufacturer.

> **Locking is an act somebody performs, not a date that passes.** The earlier
> version of this note said prior-month entries were accepted "until the 10th,
> then locked", which would have been built as a date comparison — and that is
> the wrong shape. The close window has no fixed length: it is however long the
> work takes. So the model is an **accounting period with a state** and an
> explicit Close operation, not a rule about dates. Getting this backwards would
> have meant either locking a month somebody was still working on, or leaving one
> open because nobody's calendar said otherwise.

An adjustment posted **during** the close is ordinary and belongs in the month
being closed — that is what the window is for. What happens to a transaction
dated inside a month that is **already locked** is a separate policy decision and
is not yet settled.

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
