# Where we are

Plain language, no jargon. For the engineering detail — every claim paired with
the command that proves it — see [implementation/STATUS.md](implementation/STATUS.md).

**Last updated:** 3 August 2026

---

## The short answer

**The foundation is done bar one item, the first dealership records work, and
there are two screens nobody has watched draw yet.**

Several dealership groups can already share one installation without ever seeing
each other's data, and staff can be restricted to their own location. Customers
and vehicles can be recorded and found, each location's stock is kept separate,
a car can be sold, and the sale writes its own accounting entry. Whoever runs the
servers is now structurally shut out of all of it, and can only come in through a
door you can see them open. Two screens exist — the stock list and the trial
balance — though nobody has yet watched them draw on a real browser.

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

## Inside stage 1 — ten of eleven pieces done

- [x] Dealership groups cannot see each other's data — each gets its own separate database
- [x] A group can have several locations, each with its own departments
- [x] Staff can be restricted to one location and cannot reach another
- [x] Refused access attempts are recorded permanently and cannot be erased
- [x] Automated checks guard all of the above, and were tested by deliberately breaking things
- [x] **Signing in** — real accounts and passwords, with sign-out taking effect immediately
- [x] **A second factor at sign-in**, with recovery codes for a lost phone
- [x] **A dealership can insist on that second factor** for the jobs that warrant it
- [x] **A malicious website cannot make your browser change anything** — see below
- [x] **Whoever runs the servers is kept out of the dealership's data**
- [ ] **Screens** — two exist and reach the system correctly, but neither has been
      seen drawn on a real browser
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
- **Whoever runs the servers cannot read the dealership's records.** Somebody has
  to keep the installation running, and that is now a different kind of login
  entirely — a different door, a different password list, a different database.
  It cannot see a customer, a car, or a deal. The reverse holds too: the most
  senior person at the dealership cannot touch the servers. Neither is a rule
  somebody has to remember to apply to the next screen we build; it is how the
  two are wired
- **When you ask for help, someone can come in — visibly, briefly, and looking
  only.** They have to write down why, they get an hour at most, they cannot
  change anything, and the visit appears in *your* log with their name and their
  stated reason. Closing it stops them on their very next click. Anyone who might
  do this has to have their own second factor set up before they can even try

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
- A screen for setting up the second factor, or for the people who run the
  servers. Both work; neither has a page
- A way to add a second person who runs the servers. There is one, created when
  the system is set up, and no way to add another yet — nor to get back in if
  they lose their phone
- Creating a new dealership from the administration side. It can list them and
  take one out of service; setting a new one up is still a developer's job

---

## Next

**Screens for the security work of the last three rounds.** All of it works, and
none of it can be reached without a developer's tools. The first and cheapest is
a page for setting up the second factor — showing the square barcode you point
your phone at, and the ten printed codes for the day you lose it. Then a small
console for whoever runs the servers: sign in, see which dealerships exist, open
a support visit with a reason, and close it again.

There is a caution attached, and it is not a small one: **nobody has yet watched
a single screen of this system draw on a real browser.** Building three more on
top of an unproven one multiplies whatever is wrong with it. Ten minutes with the
existing stock list would be worth more than a week of new pages — which is the
first item on your list below.

Signing in with an existing company login stays parked: it cannot be honestly
built or tested without a real login provider to test against, and a fake one
would prove nothing.

---

## What is blocked on you, not on me

| Item | Why it matters |
|---|---|
| **Opening the two screens in a browser and telling me what you see** | They serve correctly and talk to the system, but no one has confirmed they actually draw — and the next round of work is three more screens on top of them |
| Pilot dealerships and access to their current systems | Stage 2 cannot be finished or proven without real data and a real provider connection |
| A rehearsed backup and restore | Stage 1 cannot close without proving a real restore produces a working system |
| Confirming the month-end rule with a real dealer's accountant | Calendar month end with a grace period to the 10th is assumed, not confirmed |
