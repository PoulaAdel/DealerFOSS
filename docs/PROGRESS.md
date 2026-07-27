# Where we are

Plain language, no jargon. For the engineering detail — every claim paired with
the command that proves it — see [implementation/STATUS.md](implementation/STATUS.md).

**Last updated:** 27 July 2026

---

## The short answer

**The foundation is being built. No dealership features exist yet.**

Several dealership groups can already share one installation without ever seeing
each other's data, and staff can be restricted to their own location. Nothing a
salesperson or service advisor would recognise as their job has been built.

**About 5% of the first release. Stage 1 of 8.**

---

## The journey

Eight stages to a working pilot with real dealerships. The plan estimates 7–8
months of focused work.

| | Stage | What it means | Status |
|---|---|---|---|
| 0 | Find pilot dealers, get provider access | Agreements, real data samples, access to the systems we must connect to. People work, not code. | **Yours to do** |
| 1 | Foundation | Keeping dealership groups apart, locations, staff permissions, and signing in. | **In progress** |
| 2 | Moving data in and out | Importing a dealer's existing records, syncing with their current system, proving nothing is lost or duplicated. | Not started |
| 3 | Customers, vehicles, inventory | The first screens a dealership would actually use day to day. | Not started |
| 4 | Leads and selling a car | Following up a lead, building a deal, trade-ins, approvals, paperwork. | Not started |
| 5 | Financing and the service lane | Finance applications, contracts, appointments, repair orders. | Not started |
| 6 | Reports and administration | Dashboards, and the tools to run the system without a developer. | Not started |
| 7 | Ready to hand to a real dealership | Security testing, performance, backups, training, installation. | Not started |
| 8 | Live pilot | Two dealerships running on it for 60 days. | Not started |

---

## Inside stage 1 — five of eight pieces done

- [x] Dealership groups cannot see each other's data — each gets its own separate database
- [x] A group can have several locations, each with its own departments
- [x] Staff can be restricted to one location and cannot reach another
- [x] Refused access attempts are recorded permanently and cannot be erased
- [x] Automated checks guard all of the above, and were tested by deliberately breaking things
- [ ] **Signing in** — today the software simply trusts whoever the request claims to be
- [ ] **Any screens at all** — there is no user interface yet, only the engine behind it
- [ ] **A rehearsed backup and restore**

---

## What works today

- Two dealership groups on one installation, fully separated
- Multiple locations inside a group
- Permissions that hold up when tested against
- A permanent record of refused access

## What does not exist

- Entering a customer
- Entering a vehicle or inventory
- Building a deal or selling anything
- Service appointments or repair orders
- Accounting, parts, reports
- Any screen a person would look at

---

## Next

**Signing in properly** — real accounts, real sessions, and the ability to revoke
access immediately. That closes the last shortcut in the foundation.

After it: **customer records** — the first feature a dealership would recognise.

---

## What is blocked on you, not on me

| Item | Why it matters |
|---|---|
| Pilot dealerships and access to their current systems | Stage 2 cannot be finished or proven without real data and a real provider connection |
| A machine that can run the database container, or accepting the local alternative | One development path stays unverified without it |
| Node.js installed | All screen/interface work is blocked until then |
