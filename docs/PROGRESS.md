# Where we are

Plain language, no jargon. For the engineering detail — every claim paired with
the command that proves it — see [implementation/STATUS.md](implementation/STATUS.md).

**Last updated:** 31 July 2026

---

## The short answer

**The foundation is nearly done, and the first real dealership records now work.**

Several dealership groups can already share one installation without ever seeing
each other's data, and staff can be restricted to their own location. On top of
that, customers and vehicles can now be recorded and found, and each location's
stock is kept separate from every other location's. There are still no screens —
this is the engine, not the dashboard.

**About 19% of the first release. Stage 1 of 8, with stages 3 and 4 well under way.**

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

## Inside stage 1 — six of eight pieces done

- [x] Dealership groups cannot see each other's data — each gets its own separate database
- [x] A group can have several locations, each with its own departments
- [x] Staff can be restricted to one location and cannot reach another
- [x] Refused access attempts are recorded permanently and cannot be erased
- [x] Automated checks guard all of the above, and were tested by deliberately breaking things
- [x] **Signing in** — real accounts and passwords, with sign-out taking effect immediately
- [ ] **Any screens at all** — there is no user interface yet, only the engine behind it
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

## What does not exist

- Finance applications, lenders, or F&I products
- Taxes and registration fees calculated by jurisdiction
- Printed or signed paperwork
- Accounting entries behind a sale
- Appointments as real diary entries, or automatic follow-up reminders
- Photos of a vehicle, or aging reports
- Service appointments or repair orders
- Accounting, parts, reports
- Any screen a person would look at

---

## Next

**A second factor at sign-in, and using an existing company login.** This is the
last piece of security groundwork we can finish without waiting on anyone. It
comes before more dealership features on purpose: every feature added later
inherits the sign-in path, and adding a second factor after deals and finance data
exist is far more disruptive than adding it now.

---

## What is blocked on you, not on me

| Item | Why it matters |
|---|---|
| Pilot dealerships and access to their current systems | Stage 2 cannot be finished or proven without real data and a real provider connection |
| A machine that can run the database container, or accepting the local alternative | One development path stays unverified without it |
| Node.js installed | All screen/interface work is blocked until then |
