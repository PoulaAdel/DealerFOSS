# Using DealerFOSS

← [Operating](OPERATING.md) · [Onboarding](ONBOARDING.md) · [Workbook](00-Workbook.md)

**For the people who work at the dealership.** Salespeople, sales managers,
service advisors, technicians, parts staff, whoever takes the money, and whoever
asks what the month made.

Two other guides exist and neither one is this:
[`OPERATING.md`](OPERATING.md) is for whoever runs the *installation* and says so
in its own first lines; [`ONBOARDING.md`](ONBOARDING.md) is for people changing
the code.

This is organised by **the job you came here to do**, not by the menu, because
nobody arrives at work wanting to visit a screen.

---

## 1. Signing in

Three things, not two: your **dealer group**, your **email**, and your
**password**. The dealer group is there because one installation can hold
several groups and they never see each other's records.

If your dealership has turned on **Two-step sign-in**, your password buys you a
prompt for a six-digit code rather than a session. Set it up under
**People & security → Two-step sign-in**. Keep the ten recovery codes you are
given somewhere that is not the same device as the authenticator app; each one
works once.

**Passkeys** (People & security → Passkeys) let you sign in with your device
instead of typing a password. Your password still exists and still works — that
is deliberate. A dealership locked out of its service desk on a Saturday morning
does not forgive a clever login.

Forgotten it: **I have forgotten my password** on the sign-in screen.

---

## 2. Why your screens are not the same as your colleague's

This confuses people on their first day, so it is worth a minute.

**You see the menu items your job needs and not the others.** If a colleague
mentions a screen you have not got, you are probably not meant to have it. Ask
your manager rather than assuming the system is broken.

Three separate things narrow what you can reach:

- **Which location you work at.** Most people are attached to one lot. You see
  that lot's cars, enquiries, deals and jobs. A group-wide role sees all of them.
- **Whether you see your own work or everybody's.** Some people are set up to
  see only the records they are named on — their own enquiries, their own deals.
  The enquiry pool is deliberately an exception, because an enquiry nobody has
  picked up has to be visible to somebody.
- **Whether you see what things cost us.** Cost, profit and margin figures are
  held back from everybody who is not a manager. This is the one that surprises
  people: you can sell a car perfectly well without being shown what it made.
  If a column looks like it should have a number and says nothing, that is this.

All three are decided on the server. Nothing you do in the browser changes what
you are allowed to do — it only changes what is drawn.

---

## 3. Selling a car

### Take the enquiry

**Sales → Enquiries.** The list is ordered by **who has been waiting longest**,
not by who came in last, because the oldest untouched enquiry is the one about
to go and buy elsewhere.

- **Take an enquiry** records a new one. If the person is not on file you can add
  them without leaving the screen — **Add and use them**.
- An enquiry moves **New → Working → Appointment → Won** or **Lost**. Use
  **Start chasing** when you pick it up and **Mark it lost** when it dies. A lost
  enquiry stays on the record; it is not deleted.
- **Take it over** and **Put it back in the pool** are how an enquiry changes
  hands. Use them rather than working somebody else's quietly, because *Chased
  by* is what the chase list is ordered on.

### Build the deal

**Sales → Deals → Start a deal.** Pick the customer and the car. Starting a deal
**holds that car** so nobody else can sell it; a second deal on the same car is
refused.

Then fill in, in whatever order suits you:

- **The numbers** — the vehicle price, any accessories, a documentation fee,
  other fees, a discount, and the trade-in. A trade-in that is worth less than
  the customer still owes on it says *owes more than it is worth*, which is a
  real deal and not an error.
- **Sold with the car** — warranties, GAP and anything else, at the price agreed
  for *this* deal rather than the list price.
- **Tax** — the amount, what it was charged on, and the rate. See §9: at the
  moment somebody types this.
- **Financing** — cash down, annual percentage rate, term in months. Or
  **Make it a cash deal** if there is no paper.

Read the column down. Every line is there, and the bottom line is **Due from the
customer**. If the lines do not add up to it, that is a bug and worth reporting —
it has happened four times and each time somebody noticed it by adding up the
column by hand.

### Get it approved and deliver it

**Send to a manager.** You cannot approve your own deal, even if you happen to
hold the right to approve deals — a manager who also sells is still not allowed
to sign off their own numbers. The refusal is the control working.

Once a manager has approved it, **Hand the car over** is what actually **sells
the car**: it moves the car to *Sold*, puts the money on the books, and raises
what the customer owes. There is deliberately no way to mark a car sold by hand,
and nothing is charged for before that moment.

**Print the order** gives the customer their paperwork.

---

## 4. Approving deals

**Sales → Deals**, open one that is *Submitted*, and **Approve**.

You are the second pair of eyes, so the things worth your attention are the ones
the system cannot judge: the discount, the trade-in allowance, and whether the
tax figure matches the address the car is being registered at. The system will
tell you when a tax line contradicts its own basis and rate, but it cannot tell
you whether the rate is the right one.

Your buttons are **Approve**, **Hand the car over** once it is approved,
**Send back for changes**, and **Mark lost**.

**If the numbers are wrong, send it back rather than approving them or killing
the deal.** The figures freeze the moment a deal leaves Draft, so a wrong
discount or trade-in allowance cannot simply be edited in place — sending it
back reopens them. The salesperson sees exactly why when they reopen it: your
note travels with the move and sits in the deal's history. You only ever see
this button on a deal you are actually able to send back; it is not offered on
one you are not — see the note in §9 on buttons that hide rather than refuse.

---

## 5. The workshop

### Before the car arrives

**Service → Coming in** is the diary. A car can be booked in days ahead, against
a customer and a car the workshop has seen before.

When the car turns up, arriving **opens the job** in one action. You cannot
accidentally open two jobs for one visit; the second attempt is refused by name.

### While it is in

**Service → Workshop.** A job runs **Booked in → Being worked on → Work finished
→ Invoiced**, with **Start work**, **Work is finished** and **Invoice it** to
move it along.

Write the work up under **The work**. Each line is either labour (hours at a
rate) or a part or sublet work, and each line says **who pays for it**: *Customer
pays*, *Warranty*, or *Internal*. Getting that right matters more than it looks —
it decides whose invoice the line lands on and where the money goes.

Anything found once work has started has to be **put to the customer**. Until
somebody answers, the line shows *Nobody has asked*. After that it is *Agreed* or
*Said no*.

**A job cannot be invoiced while anything is still unanswered.** That is the
single most useful refusal in the workshop: it is what stops a customer being
billed for work they never agreed to. Work they declined stays on the record
priced at nothing, so when the same fault comes back in six months you can show
you told them.

**Print the job sheet** at any point; **Print the invoice** once it is invoiced.

### Parts

**Service → Parts.** **Book a delivery in** when stock arrives, against a
delivery note. Putting a part on a job takes it off the shelf at what it cost,
which is what gives the department a real profit figure rather than a guess.

You cannot issue more of a part than you have.

**What a part cost is a manager's figure** — see §2. Without that right you see
the part, the description and how many are on the shelf, and the cost column is
not there at all rather than being blank. Note the asymmetry: booking a
delivery in still asks you to type a unit cost, because that is something you
are telling the system rather than something it is telling you. Today the person
who books stock in is the manager, who sees both; if that job is ever handed to
somebody else, they will be entering a figure they cannot then read back.

---

## 6. Taking the money

**Accounting → Who owes us** lists what is outstanding, in 30/60/90-day bands.
Open a bill and **Record the payment**: how much, how they paid (deposit, card,
transfer, cheque, or a lender), and a reference that goes on the record.

Part payments are normal. The bill shows **Billed**, **Paid so far** and **Still
owed**, and says *Paid in full* when it is done.

**If somebody overpays, the extra is not refused and not quietly swallowed.** It
becomes money the dealership owes them back, listed as *Owed back to this
customer*. You can put it against another bill they owe — **Put {amount} against
this bill** — or hand it back. Handing cash back needs its own permission,
because a refund is the classic way a retail business is quietly stolen from.

**Accounting → Statements** prints a customer's whole history over a period with
a running balance, which is what to send when somebody queries their account.

A customer can have a **credit limit**. A deal or a job that would take them past
it is refused when it tries to open the debt, not at the end.

---

## 7. Buying cars in

**Sales → Stock → Take a car into stock.** The car, the location, a stock number,
and what it cost. Say whether a lender paid for it or the dealership bought it
outright — that is a fact about *this* car, not a setting, because most lots carry
both.

A car moves **Incoming → Reconditioning → Available → On hold → Sold**, and you
move it with the buttons offered. Only the moves that make sense are offered, and
**Sold is never one of them** — a car is sold by delivering a deal.

On a car's own page three figures are kept apart on purpose:

| | |
|---|---|
| **Acquisition cost** | what you paid for it |
| **Reconditioning** | what has been spent making it saleable |
| **Total in the car** | the two added up — what it is really carried at |

Reconditioning work done on your own car lands on that car rather than
disappearing into a general expense, and it comes back off when the car sells.
*not recorded* and a recorded zero are shown differently, because "we do not know"
and "it was free" are different facts.

---

## 8. What the month made

**This month** is the first thing you see. Total gross, split into vehicles,
F&I and service, against the month before, with how many cars went out and what
the average car made. While the month is still open it says so, because the
figures can still move.

**Accounting → Reports** has the profit and loss, the balance sheet, and stock
ageing. The balance sheet **says loudly when it does not balance** rather than
hiding it.

**Accounting → The books** is where a month or a year is closed. A closed month
refuses new postings until somebody reopens it, and **reopening needs a written
reason** that stays on the record. That is not bureaucracy: a month that can be
quietly reopened is a month whose numbers mean nothing.

---

## 9. What it will not do yet

Worth knowing before you promise a customer something.

- **Tax is typed, not calculated.** The basis, the rate and the amount are
  recorded with who entered them, and the arithmetic is checked — but there are
  no rate tables yet, so somebody has to know the rate. Being built.
- **No credit applications.** You can record the financing that was agreed; you
  cannot send an application to a lender from here.
- **No warranty claim submission.** You can mark a line as warranty work and the
  books keep it separate; submitting the claim to the manufacturer happens
  elsewhere.
- **No titling or registration filing.**
- **No live link to another dealer system.** Records come in and go out as files
  (see **Records**), and a whole location can be moved to another installation
  with its history intact.
- **On the deal desk, a button you would be refused is not shown at all.**
  Fixed 2026-10-12: a deal's own salesperson no longer sees **Approve** on it,
  and anybody who holds nothing that changes a deal sees none of the four
  action buttons. Send back, hand over and mark lost only appear when you are
  actually allowed them. The same is not yet true everywhere else in the
  product — a button elsewhere that refuses you rather than being hidden is
  the server being right, not the screen being broken, and hiding it there is
  still being worked on, module by module.

---

## 10. Refusals that are doing their job

Collected in one place, because every one of these gets reported as a bug at
least once:

| What you tried | Why it was refused |
|---|---|
| Approving your own deal | A second pair of eyes is the whole point |
| Selling a car that is on another deal | It is being held for that customer |
| Marking a car Sold by hand | Delivering the deal is what sells it |
| Invoicing a job with work unanswered | The customer has not agreed to pay for it |
| Billing work the customer declined | It stays on the record at nothing instead |
| Issuing more of a part than you have | The shelf is a real number |
| Posting into a closed month | Reopen it, with a reason |
| Reopening a month with no reason | The reason is the record |
| Opening a bill past a credit limit | Someone set that limit deliberately |
| Seeing another location's records | You are attached to yours |
| Seeing cost or profit | Held back from non-managers |

---

## 11. Getting help

The person who installed this can read what happened — every refusal and every
sensitive change is written to your dealership's own audit trail, including the
rare occasions when a support engineer is let in. That access is time-limited,
needs a written reason, is read-only, and appears in **your** records naming who
it was and why.
