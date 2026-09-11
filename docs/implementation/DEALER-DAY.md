# A day at the dealership, walked

**Walked 2026-09-10** against the seeded demo dealership (`northgroup`, two
rooftops, ~500 customers, 161 unsold cars, 162 repair orders), in a real browser,
signing in as the roles that would actually do each job.

This is not a design document. It is a record of what happened when four ordinary
jobs were driven through the running application, screen by screen, and where each
one stopped. It exists because the measurement that prompted it was uncomfortable:
50 commits in 30 days, of which about seven changed anything a dealership would
notice. The register was nearly empty, so every item left on it was small. The
backlog was the thing that had run out, not the effort.

Everything below was verified against the running application. Where a claim is
about code, the file and line are given. Three of my own readings were wrong
during the walk and are recorded as retractions, because a walk that only confirms
what you expected is not a walk.

## The four jobs

| Job | Role | Result on 2026-09-10 | Now |
|---|---|---|---|
| A walk-in becomes a sold car | Salesperson, then manager | **Completed end to end** | still does |
| A car arrives, is reconditioned, goes on sale | Manager | **Cannot start** | **completes** (see below) |
| A customer books service and pays | Manager | **Reaches Invoiced, cannot reach paid** | **completes** (see below) |
| The manager asks what the month made | Manager | **Answered as gross only** | **completes** (see below) |

All four jobs can be completed. That is the number the progress page reports,
and it moves only when somebody walks the job again.

Finishing them is not the same as finishing the product. The walk found thirty
things and four of them are fixed; the rest are register rows, and the four jobs
are the *ordinary* day rather than the whole job of running a dealership. What
has changed is that the ordinary day no longer stops.

## What has been fixed since the walk

Findings 1, 12 and 13 are done — the whole of Job B — in one change on the same
day. The rest stand.

- **A car can be taken into stock from a screen**, with what it cost, creating
  the vehicle record and the unit together so a car nobody has seen before does
  not require two screens and a round trip.
- **A car can be moved between stock states from its own record.** Only the moves
  the domain allows are offered, and Sold is never one of them: a car is sold by
  delivering a deal, which is what posts the sale.
- **Taking a car into stock posts to the ledger** — 1300 debited, 1000 credited —
  and the seeded dealership's existing two hundred cars were back-filled rather
  than requiring the database to be dropped.

Walked again to confirm rather than assumed: WALK01, a 2022 Mazda CX-5 at
$19,750, taken in, moved Incoming → Reconditioning ("Valet and two front tyres")
→ Available ("Ready for the forecourt"), with a journal entry reading
`1300 D19750 / 1000 C19750, Stock WALK01 — 2022 Mazda CX-5`.

Account 1300 went from **minus $993,190 to plus $2,697,960**.

### And it immediately exposed the next lie, which is the point

Cash is now **minus $2,510,729**.

That is not a regression, and it is not a defect in the posting — it is the
correct double-entry consequence of a chart of accounts that has no way for a
dealership to have any money. There is no opening balance, no capital, and no
floorplan: the lender that really pays for a dealer's stock does not exist here,
so $3.7M of cars was bought out of a cash account that started at nothing.

Before, inventory lied. Now inventory is right and cash is wrong, for a reason
that is written down and has a name. Both new register rows — floorplan, and
opening balances when a dealership is set up — come straight from this, and
neither was visible while the first lie was covering for the second.

### Job C, on 2026-09-11

Finding 2 is done. A bill is now something that can be **owed** and then
**settled**, rather than an event the books recorded as cash on the spot.

- **Account 1100, customer accounts receivable**, and a customer sub-ledger
  behind it. Account 1100 alone answers "we are owed $84,000"; only the
  sub-ledger answers "and $19,000 of it is Ashgrove Couriers, six weeks old".
- **Delivering a car and invoicing a job now debit 1100, not 1000.** Money
  arriving is a separate entry that moves 1100 to 1000 when it actually turns up.
- **Part-payments are ordinary**, so a deposit is a real thing. What is
  outstanding is derived from the payments and never stored, so it cannot drift
  from the rows underneath it. Overpayment is refused rather than absorbed: the
  difference belongs to the customer and somebody has to give it back.
- **A lender settling a financed car is a payment method**, not a different kind
  of debt — what the dealership is owed does not change with who hands it over.
- **A payment band on the job sheet and the deal desk**, one component for both,
  because the question is the same whether the thing sold was a car or a clutch.

Walked again to confirm: RO-1083, a full service at $240, invoiced, then a $100
deposit by card and a $140 balance in cash. The ledger read
`1100 D240 / 4200 C240`, then `1000 D100 / 1100 C100`, then `1000 D140 / 1100 C140`.
Account 1100 came back to zero on that job, and across the dealership the trial
balance's 1100 ($110.00) equals the sub-ledger's total outstanding ($110.00) —
which is the invariant the whole design exists to keep.

**One defect only the walk could find.** The band looked its receivable up once,
when the job was opened, and never again — so invoicing in the same session
showed nothing at all. It rendered perfectly on a fresh page load, which is the
one place nobody was looking. The unit tests all passed because they mount the
band against a bill that already exists. Fixed, and there is now a test that
fails without the fix.

**What is deliberately not built:** credit balances (hence refusing overpayment),
receivable ageing as a report, statements, and credit limits. The historical
deliveries and invoices in the seeded dealership are *not* back-filled into the
sub-ledger — they were genuinely posted as cash at the time, and rewriting a
posted ledger to look tidier is the one thing an accounting system must not do.

### Job D, on 2026-09-11

Findings 5 and 6 are done, and so is the cash problem the stock work exposed.

- **Expense accounts exist.** There were none — not one — so a dealership could
  record everything it earned and nothing it spent. Wages, rent, advertising,
  floorplan interest and a catch-all now sit in the chart.
- **A journal entry can be written by hand**, behind its own permission
  (`Accounting.ManualEntry`, which a salesperson does not hold even though they
  hold `Accounting.Post`). It is how an overhead is recorded, how a new
  installation states what it already owned, and how a mistake is corrected.
- **A profit and loss**: departmental gross, then overheads, then net. The
  departmental half calls the same method the dashboard reads, so the two cannot
  disagree.
- **A balance sheet** that says whether it balances, and says so loudly when it
  does not.
- **A floorplan liability**, chosen per car when it is taken in, so buying stock
  can credit the lender rather than the bank.

Walked again to confirm. Rent of $4,500 recorded by hand; an opening entry moving
$2,717,710 of stock funding from the bank to the floorplan lender. The result:

|  | before | after |
|---|---|---|
| Cash | −$2,530,239 | **+$182,971** |
| Net profit | did not exist | **$223,378** |
| Balance sheet | did not exist | assets $3,019,333 = liabilities $2,795,423 + equity $0 + earned $223,910 |

**And the numbers caught a hole nothing else would have.** Adding the two reports
up by hand, they disagreed by $663.60. The cause: the first version of the P&L
named its five overhead accounts explicitly, so 5400 Internal service charge —
an expense, and not a department's cost of sales — appeared on no part of the
report at all. The seeded dealership had $1,196 in it and the page did not
mention it. Money spent into an account that showed on no report.

Now the report asks the *chart* which accounts are overheads rather than
consulting a list in code, so a new expense account is on the report the day
somebody adds it, and the only way to keep one off is to name it a cost of sales
deliberately. With that fixed, all-time net profit and earnings-to-date agree
exactly at $223,910.40.

**What is deliberately not built:** a year-end close (hence earnings shown as
their own line rather than folded into capital), comparatives against last year,
departmental overhead allocation, and cash flow.

## What the walk found

Findings are ordered by what a dealership would feel first, not by where they were
found. The five marked **(stopper)** would stop a real installation.

### Money and the ledger

1. **(stopper) Nothing books stock IN, so the balance sheet is nonsense.**
   Account 1300 Vehicle inventory stands at **minus $993,190** — debits $310.00
   against credits $993,500.00. `AccountingService.cs:900` credits inventory when
   a car is delivered; `AccountingService.cs:941` debits it only for capitalised
   reconditioning, which is the entire $310. Nothing debits it when a car is
   bought. `InventoryService.cs` never references accounting at all, and
   `InventoryUnit.CostAmount` is never posted. Parts are the same shape:
   `AccountingService.cs:956` credits `PartsInventory` and nothing ever debits it.
   The ledger still balances. It is internally consistent and externally wrong.

2. **(stopper) Nothing can be sold except for cash.**
   `AccountingService.cs:936` debits Cash for the whole repair-order invoice
   ("Taken from the customer"); delivery does the same for a car. The chart has
   exactly one receivable, 1200 Warranty claims receivable. There is no trade or
   customer receivable. A fleet customer cannot be invoiced on account — and the
   demo dealership has four of them. A car sold on finance shows as cash from the
   customer rather than settlement from a lender. Deposits and part-payments do
   not exist. Almost no car is ever paid for the way this ledger assumes.

3. **The parts capability is built and tested, and the workshop screen does not
   use it.** This one is worth stating precisely, because the first way I wrote it
   was wrong. `src/App/Parts` is a complete capability — catalogue, stock
   receipts, average costing, stock levels — and `verify-e2e.ps1` exercises it end
   to end every run: *"booked in 20, average cost 7.00 / sold 2, cost recorded
   14.00 / stock left on the shelf 18"*. The repair-order line contract accepts an
   optional `PartId` (`IRepairOrders.cs:256`).

   The screen never sends one. Choosing "Part" gives a description and an amount,
   and `grep` for `partId` across `frontend/src/features/service` returns nothing.
   Every part billed through the browser is therefore free text with no cost:
   `invoice.PartsCost` is zero, `AccountingService.cs:955-956` skips both 5300 and
   1400, and the $352.40 of parts revenue in the trial balance carries no cost of
   sale. The advisor role even holds `Permissions.PartsRead` with the comment "so
   a part can be picked off the shelf rather than typed as free text".

   So this is not missing machinery. It is a picker that was never put on the
   screen, and it silently overstates parts gross by 100%.

   **Done 2026-09-11.** A picker on the repair-order line, offering what is on
   the shelf with the quantity beside it, filling the description from the
   catalogue and leaving it editable. Free text is still there and is named as a
   choice — "Not from stock (type it below)" — because a one-off item bought for
   one job never enters the catalogue and still has to be billable. A catalogue
   that will not load falls back to free text rather than stopping the workshop.

   Walked: RO-1084 billed two brake pad sets at $24 off a shelf holding 20 at $7.
   The line froze `cost=14`, the entry read `1100 D24 / 5300 D14 / 1400 C14 /
   4300 C24`, and the shelf went to 18. Service cost on the dashboard went from
   $0 to $14 — the first parts cost ever recorded through a browser. It still
   *rounds* to a 100% margin, because every historical part in the seeded
   dealership was billed as free text and those lines are not rewritten.

4. **The dashboard states a 100% margin on service as fact.** "Service $17,286
   revenue, $0 cost, 100% margin" — finding 3 arriving on the screen a dealer
   principal reads most often, with nothing to hint the cost side was never
   captured. Note that this is a *screen* reporting honestly on data a *screen*
   failed to collect: the ledger would have carried the cost had the part been
   picked off the shelf.

5. **There is no profit and loss and no balance sheet.** Accounting offers a trial
   balance and a screen for opening and closing months. A trial balance is a
   bookkeeping instrument, not a management report. There is no expense entry of
   any kind — no wages, rent, advertising or floorplan interest anywhere in the
   chart — so net profit does not exist in the system.

6. **Stock is counted but never valued.** The ageing panel says 161 unsold cars
   and nothing says what they are worth. "What is my stock worth" is the biggest
   number on a dealer's balance sheet and has no answer.

### The deal

7. **(stopper) "The numbers on this deal" does not add up.**
   `DealsPage.tsx:270-322`. The walk's deal read: vehicle price $32,500.00, GAP
   cover $500.00, **due from the customer $36,331.25**. The $3,331.25 of tax is in
   the total and nowhere in the column. `tbody` renders charges, products and
   trade-in; there is no tax row. `tfoot` prints `deal.amountDue`, which includes
   tax. The trade-in row three lines above carries this comment: *"leaving equity
   positive made the column stop adding up — read down it and you got a different
   total from the one printed at the bottom."* Tax has reintroduced the defect
   that comment was written about. This is the number a customer signs for.

8. **A documentation fee cannot be added by a person.** The charge-kind picker
   offers Vehicle price, Fee, Discount, Accessory. `contracts.ts:758-760` never
   learned `DocumentationFee`, and no locale has a name for it.
   `ChargeKind.DocumentationFee = 4` exists in the domain, `TaxBasisRules` has
   `DocumentationFeeIsTaxable`, and commit `e324695` fixed a shipped bug where a
   doc-fee deal could not be delivered. All of it is unreachable: the only way to
   put a documentation fee on a deal is to seed one. A doc fee is on essentially
   every retail car deal in the United States.

9. **The tax band makes a person do the arithmetic and checks none of it.**
   Entering a basis of 32,500 and a rate of 10.25 computes nothing; "Tax charged"
   stays empty until the person types 3,331.25 themselves, and nothing ever checks
   that basis times rate equals charged. `Deal.TaxableBasis(rules)` and
   `TaxBasisRules` were built for this and the screen does not call them. The
   basis is not even defaulted from the deal.

10. **The deal has no registration address.** `Deal` carries `TaxedAt`, a
    four-field `TaxAddress` — correct for rate lookup and deliberately so. ADR-024
    also wanted a registration address frozen with the sale, and there is nowhere
    to put one, on the deal or on the customer. A retail contract cannot be
    printed without it.

11. **The enquiry-to-deal hand-off drops the car without saying so.** "Build the
    deal" carries `leadId` and `customerId` in the URL, not the car. The picker
    opens on "Choose a car...". The enquiry named a 2021 Toyota RAV4 XLE; the
    salesperson gets an empty picker and no message — not "that car has been
    sold", not "pick another RAV4".

### Stock

12. **(stopper) Job B cannot start: there is no way to put a car into stock.**
    205 buttons on `/inventory`, every one of them a stock number. No "add a car",
    no "book one in". `InventoryEndpoints.cs:40` maps `POST ""` to `ReceiveAsync`
    — the capability exists and is on no screen. Every car in the system arrived
    from a seeder.

13. **A car cannot be moved between stock states either.** Opening a car offers
    "Check for recalls" and "Close". `InventoryEndpoints.cs:41` maps
    `POST /{unitId}/status`, also unsurfaced. A car cannot go Reconditioning to
    Available, be put on hold, or be withdrawn. The status filter on the list is
    decoration: nothing can ever move.

### Enquiries

14. **(stopper) The "Nobody is chasing these" list hides the most neglected
    enquiries.** `LeadService.cs:123` orders by `CapturedAt` **descending** and
    takes 50 — the 50 newest — and the screen then displays them longest-waiting
    first. Measured against the running application with 52 open enquiries:

    | | days waiting |
    |---|---|
    | oldest in the database | 95, 93, 92, 89, 88, 85 |
    | oldest the screen shows | 92, 89, 88, 85, 84, 83 |

    The two longest-waiting customers are not returned at all. It compounds:
    taking one new enquiry pushed the 95-day customer off the screen, and winning
    it let them back. At 200 open enquiries the 150 longest-waiting are invisible
    under a heading that says "Nobody is chasing these 50".

15. **A walk-in who is not already a customer cannot be recorded.** Searching a
    name that does not exist empties the picker and says "Search above to find
    them." There is no "add this person" control in the enquiry flow. The
    salesperson must abandon the enquiry, create the customer elsewhere, navigate
    back and start again — and "Walk-in" is one of the five sources the form
    itself offers.

16. **The enquiry form offers cars that are already sold.** `CaptureLead.tsx:74-77`
    carries the comment "Cars already sold are no use here"; the next line queries
    `/inventory?limit=200` with no status filter. The picker offered A1001, which
    the API reports as Sold.

### The workshop

17. **The workshop list shows two different jobs under one number.** Over the API:
    162 repair orders, 82 distinct numbers, 80 numbers used twice. The data is
    correct — `RepairOrderService.cs:690-702` numbers per rooftop by design and
    `RepairOrderTables.cs:65` enforces it with a unique index on
    `(RooftopId, Number)`. The screen is not: it is headed "The work at the
    locations you cover", lists both rooftops together, and shows the number
    without the location. A manager covering two lots cannot tell which RO-1078 is
    which, and neither can the customer holding the printed job card.

18. **Booking a car in: 100 customers, no search, and cars you cannot tell apart.**
    The customer picker is a raw `<select>` of 101 options with no search of any
    kind, against ~500 customers. The vehicle picker holds 101 options and does
    **not** filter when a customer is chosen — verified, it stayed at 101. Options
    carry no VIN, no plate, no stock number, and 32 of the labels are exact
    duplicates.

19. **Job numbering breaks if a repair order is ever deleted.** The next number is
    `FirstNumber + COUNT(orders at this rooftop) + 1`. A deletion makes the next
    number collide; two concurrent opens compute the same count. The unique index
    turns both into a failed insert rather than corruption — it fails loudly, but
    it fails, and the advisor sees an error they cannot act on.

20. **"Coming in" shows last month's appointments as expected today.** The diary
    reads "cars expected" over entries dated 10 and 13 August against a system
    date of 10 September, still offering "It's here" and "Did not come".

### Across every screen

21. **Nothing has a URL.** There is no `/deals/:id`, `/customers/:id`,
    `/leads/:id` or `/inventory/:id`; every record opens into in-page state. A
    deal cannot be linked to a colleague, two cars cannot be compared in two tabs,
    nothing can be bookmarked, and the back button leaves the screen instead of
    closing the record.

22. **Search works only if you guess to press Enter.** The search input sits in a
    `<form>` whose `onSubmit` runs the query. There is no submit button and no
    as-you-type search, on `/leads` or `/customers`. Typing a name and tabbing
    onward leaves the previous results showing, so the person concludes the record
    does not exist.

23. **Every list truncates, and none can be paged.** `/leads` says "Showing the
    first 50. There may be more — narrow it with the filters until paging exists."
    `/customers` says "The first 100 customers. There may be more." The
    disclosure is honest; there is still no way to reach the rest.

24. **Buttons are offered to roles that cannot use them.** On `/staff` a
    salesperson can open "Add somebody", type a name and email, and only then be
    refused: "This needs organization-wide permission." Security is correct; the
    surfacing is not. The deal desk does this better — it shows Approve with a
    note saying a manager is needed — though that note names the internal string
    `Deals.Approve` at a salesperson.

25. **A customer has no address.** Add-a-customer collects first name, last name,
    email and phone. `Address` is a seven-field record added for tax and nothing
    in the customer screens collects it.

26. **Adding a customer gives no confirmation.** The panel closes, the list does
    not move, and the new person is alphabetically elsewhere. The record did
    save — verified by searching for it — but the person has no way to know.

27. **Percentages against a near-empty month are noise.** "up 42861% on last
    month". True and useless; the F&I tile already gets this right with "up from
    nothing".

28. **The "Add somebody" panel is not a form.** Two bare inputs and a button, with
    no `<form>` element, so Enter does not submit and native validation and
    autofill do not engage. Sign-in *is* a real form, so this is inconsistency
    rather than house style.

29. **Demo pollution.** `TX516909 — Tax Preview516909`, a throwaway record from
    the tax work, sits in the manager's approval queue looking like a customer.

30. **Enquiries never name a car.** All 50 unassigned enquiries read "no
    particular car". `Lead.VehicleOfInterestId` exists and `DemoData.cs` never
    sets it, so the enquiry list cannot answer "who is waiting on this car".

## What still works, and worked well

The walk was not all bad news, and the parts that hold up are worth naming so they
are not disturbed.

- **Segregation of duties is real.** A salesperson could not approve their own
  deal, and the refusal said why: "Whoever built this deal cannot be the one who
  approves it." The same held on `/staff`.
- **The taxed delivery fixed in `e324695` holds** under a hand-driven deal:
  $32,500 car plus $500 GAP plus $3,331.25 tax delivered, and the trial balance
  stayed in balance at $2,352,349.05 with 2100 Sales tax payable carrying the
  liability.
- **Tax provenance surfaces properly.** Once submitted, the read-only view reads
  "Sales tax | WA / King / Seattle | a person entered it", which is exactly what
  ADR-024 R4 asked for.
- **Freezing on submit works.** "The numbers are frozen. They stopped being
  editable when this deal was submitted, so what a manager approves is what was
  put in front of them."
- **The repair order flow is complete** from booking to invoice: write-up, the
  agreed/not-agreed distinction, start work, finish, invoice.
- **The dashboard is the strongest screen in the application** — month and rooftop
  pickers, an open-books warning, gross by department with margin, cars delivered,
  jobs invoiced, gross per car, stock ageing in four bands, and the cars standing
  longest.

## What I got wrong during the walk

Recorded because the method matters more than the findings.

- **"The F&I catalogue prices are not rendering."** They are. Price and cost are
  disabled `<input>` elements and `innerText` does not report input values.
  Ticking the box enabled them at 500/260 and 1200/700, and selling GAP put $500
  on the deal with $240 gross.
- **"The saved tax line disappears after saving."** It does not. The row holds
  five inputs carrying the saved values and survives a full page reload. Same
  cause, second time.
- **"Saving the charge lines twice would duplicate them."** It does not. The
  editor is an edit-in-place list of the whole charge set; saving twice replaced
  rather than appended, and the deal stayed at $33,000. Verified by doing it.
- **"A salesperson can reach the trial balance and the staff list."** They can,
  and it is deliberate: the role holds `AccountingRead` because delivering a car
  posts the sale, and `StaffRead` because handing an enquiry to a colleague needs
  the colleague list. The defect is the action buttons, not the screens.

`innerText` caught me three times on one walk. Read the DOM, not the text.

## What this changes

The register in [`docs/11`](../11-Franchise-and-External-Scope.md) §12 has been
rewritten from this walk. Before it, the register held four Build rows, all small,
none of which would have moved any of the four jobs. The rows added here are the
ordinary work of running a dealership, in the order a dealership feels it.

The progress page now counts jobs that can be completed end to end — one of four —
rather than commits and checkboxes, which is what allowed 50 commits to read as
progress while a car could not be put into stock.
