# Where we are

Plain language, no jargon. For the engineering detail — every claim paired with
the command that proves it — see [implementation/STATUS.md](implementation/STATUS.md).

**Last updated:** 1 August 2026

---

## The short answer

**The foundation is nearly done, the first dealership records work, and there are
now two screens.**

Several dealership groups can already share one installation without ever seeing
each other's data, and staff can be restricted to their own location. Customers
and vehicles can be recorded and found, each location's stock is kept separate,
a car can be sold, and the sale writes its own accounting entry. Two screens
exist — the stock list and the trial balance — though nobody has yet watched them
draw on a real browser.

**About 25% of the first release. Stage 1 of 8, with stages 3 and 4 well under way.**

---

## The journey

Eight stages to a working pilot with real dealerships. The plan estimates 7–8
months of focused work.

| | Stage | What it means | Status |
|---|---|---|---|
| 0 | Find pilot dealers, get provider access | Agreements, real data samples, access to the systems we must connect to. People work, not code. | **Yours to do** |
| 1 | Foundation | Keeping dealership groups apart, locations, staff permissions, and signing in. | **In progress** |
| 2 | Moving data in and out | Importing a dealer's existing records, syncing with their current system, proving nothing is lost or duplicated. | Not started |
| 3 | Customers, vehicles, inventory | The first records a dealership would actually use day to day. | **Started early** |
| 4 | Leads and selling a car | Following up a lead, building a deal, trade-ins, approvals, paperwork. | **Well under way** |
| 5 | Financing and the service lane | Finance applications, contracts, appointments, repair orders. | Not started |
| 6 | Reports and administration | Dashboards, and the tools to run the system without a developer. | Not started |
| 7 | Ready to hand to a real dealership | Security testing, performance, backups, training, installation. | Not started |
| 8 | Live pilot | Two dealerships running on it for 60 days. | Not started |

---

## Inside stage 1 — nine of eleven pieces done

- [x] Dealership groups cannot see each other's data — each gets its own separate database
- [x] A group can have several locations, each with its own departments
- [x] Staff can be restricted to one location and cannot reach another
- [x] Refused access attempts are recorded permanently and cannot be erased
- [x] Automated checks guard all of the above, and were tested by deliberately breaking things
- [x] **Signing in** — real accounts and passwords, with sign-out taking effect immediately
- [x] **A second factor at sign-in**, with recovery codes for a lost phone
- [x] **A dealership can insist on that second factor** for the jobs that warrant it
- [x] **A malicious website cannot make your browser change anything** — see below
- [ ] **Screens** — two exist and reach the system correctly, but neither has been
      seen drawn on a real browser
- [ ] **Keeping whoever runs the servers out of the dealership's data**
- [ ] **A rehearsed backup and restore**

---

## What works today

- Two dealership groups on one installation, fully separated
- Multiple locations inside a group
- Permissions that hold up when tested against
- A permanent record of refused access
- Signing in with an email and password, and signing out immediately
- Adding a customer, and finding one by name, phone, or email
- **Recording a vehicle and finding it by VIN — even just the last few characters**
- **Putting a car into a location's stock under a stock number, and moving it
  through incoming → reconditioning → available → sold, with every move kept**
- **One location cannot see or move another location's stock, while the vehicle
  records themselves stay shared across the group**
- **Taking an enquiry from someone who might buy, chasing it through to a sale or
  a dead end, handing it between salespeople, and keeping every note along the way
  — including a lost lead who comes back months later**
- **Selling a car: putting a price on it, taking a trade-in, sending it to a
  manager for approval, and handing it over — with the car held on the lot the
  whole time so nobody else can sell it**
- **A salesperson cannot approve their own deal, and the price cannot change after
  a manager has agreed to it**
- **A second factor at sign-in.** Anyone can switch on the six-digit code from a
  phone app; after that a password on its own is not enough to get in, and there
  are ten printable one-time codes for the day the phone is lost
- **Each dealership's database password is encrypted**, so a stolen copy of the
  system does not hand over their data
- **Every sale writes its own accounting entry — what came in, what went out, what
  the car cost — and it must balance or the sale does not go through. Nothing in
  that record can ever be edited; a mistake is corrected by an opposite entry that
  leaves both visible**
- **A running total per account, and whether the two columns agree** — the
  headline of a trial balance is whether it balances, and a difference means
  something was lost on the way in
- **A malicious website cannot make your browser change anything.** If you are
  signed in here and then visit a hostile page, that page can cause your browser
  to send a request with your cookies attached. Every change now also demands a
  secret handed out at sign-in that only this application can read, so such a
  request is refused. It stops working the moment you sign out
- **Two screens** — the stock list and the trial balance — each showing something
  sensible while loading, when empty, when you lack permission, and when the
  server fails, with a way to try again
- **A dealership can insist on the second factor for the jobs that warrant it.**
  Say "salespeople must have one", and from that moment a salesperson who has not
  set one up can sign in, is told exactly what to do, and can do nothing else
  until they have. Nobody is ever locked out, and it applies to whoever holds the
  job next year without anybody remembering to add them. Only somebody with
  group-wide authority can set it — not a manager at one lot

## What does not exist

- **A full set of books.** Sales are recorded and accounts total up; there are no
  accounting periods and no month-end close yet
- Finance applications, lenders, or F&I products
- Taxes and registration fees calculated by jurisdiction
- Printed or signed paperwork
- Appointments as real diary entries, or automatic follow-up reminders
- Photos of a vehicle, or aging reports
- Service appointments or repair orders
- Parts, or reports
- Screens for customers, leads, or selling a car — the system underneath does all
  three, but there is nothing to click yet

---

## Next

**Keeping whoever runs the servers out of the dealership's data.** Someone has to
administer the installation — create dealerships, watch it stay healthy — and
today that person would be an ordinary user with wide permissions. It should be a
separate kind of login that structurally cannot read a customer record, plus a
deliberate, time-limited, logged way to step into one dealership when they ask
for help. This is the last piece of security groundwork that does not wait on
anyone.

Signing in with an existing company login stays parked: it cannot be honestly
built or tested without a real login provider to test against, and a fake one
would prove nothing.

Cheaper and also outstanding: **a screen for setting up the second factor.** It
works over the API, but there is no page showing the QR code you would scan.

---

## What is blocked on you, not on me

| Item | Why it matters |
|---|---|
| Pilot dealerships and access to their current systems | Stage 2 cannot be finished or proven without real data and a real provider connection |
| Opening the two screens in a browser and telling me what you see | They serve correctly and talk to the system, but no one has confirmed they actually draw |
| A rehearsed backup and restore | Stage 1 cannot close without proving a real restore produces a working system |
| Confirming the month-end rule with a real dealer's accountant | Calendar month end with a grace period to the 10th is assumed, not confirmed |
