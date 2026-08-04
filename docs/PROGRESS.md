# Where we are

Plain language, no jargon. For the engineering detail — every claim paired with
the command that proves it — see [implementation/STATUS.md](implementation/STATUS.md).

**Last updated:** 3 August 2026

---

## The short answer

**The foundation is done bar one item — a rehearsed backup — the first dealership
records work, and there are six screens that provably draw.**

Several dealership groups can already share one installation without ever seeing
each other's data, and staff can be restricted to their own location. Customers
and vehicles can be recorded and found, each location's stock is kept separate,
a car can be sold, and the sale writes its own accounting entry. Whoever runs the
servers is now structurally shut out of all of it, and can only come in through a
door you can see them open. Six screens exist — the stock list, the trial
balance, setting up two-step sign-in, and a console for whoever runs the
installation — and a machine now checks on every change that they really appear
and behave, which until this week only a person could confirm.

**About 25% of the first release. Stage 1 of 8, with stages 3 and 4 well under way.**

---

## The journey

Eight stages to a working pilot with real dealerships. The plan estimates 7–8
months of focused work.

| | Stage | What it means | Status |
|---|---|---|---|
| 0 | Find pilot dealers, get provider access | Agreements, real data samples, access to the systems we must connect to. People work, not code. | **Yours to do** |
| 1 | Foundation | Keeping dealership groups apart, locations, staff permissions, and signing in. | **In progress** |
| 2 | Moving data in and out | Importing a dealer's existing records, syncing with their current system, proving nothing is lost or duplicated. | **Well under way** |
| 3 | Customers, vehicles, inventory | The first records a dealership would actually use day to day. | **Well under way** |
| 4 | Leads and selling a car | Following up a lead, building a deal, trade-ins, approvals, paperwork. | **Well under way** |
| 5 | Financing and the service lane | Finance applications, contracts, appointments, repair orders. | Not started |
| 6 | Reports and administration | Dashboards, and the tools to run the system without a developer. | Not started |
| 7 | Ready to hand to a real dealership | Security testing, performance, backups, training, installation. | Not started |
| 8 | Live pilot | Two dealerships running on it for 60 days. | Not started |

---

## Inside stage 1 — eleven of twelve pieces done

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
- [x] **The screens draw** — proven automatically now, on every change
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
- **Seven screens** — the stock list, the trial balance, setting up two-step
  sign-in, moving records in and out, and three for whoever runs the
  installation — each showing something sensible while loading, when empty, when
  you lack permission, and when the server fails, with a way to try again
- **A console for whoever runs the servers.** A separate sign-in at its own
  address, marked so nobody confuses it with the dealership screens. It shows
  every dealership on the installation and whether each is in service, can take
  one out of service and put it back (asking first, since that stops everyone
  there working), and is where a support visit is opened with a written reason
  and closed again. The whole record of who went where and why is on that page —
  including visits already closed, because a log you can empty is not a log
- **The screens are now checked automatically every time anything changes**, and
  have been opened and used on a real browser. Until this week "does it actually
  appear on screen?" was a question only a person could answer, and everything
  built on top carried that doubt. Forty-six automated checks now open each
  screen and read it the way you would — by its headings, its labels, its words.
  On top of that, every screen has been signed into and walked at both desktop
  and phone width, which found three problems no automated check could have:
  the stock list was quietly overstating how many cars a dealership has, the
  administrator sign-in looked identical to the ordinary one, and edits to the
  code were not reaching the browser at all
- **A page for setting up two-step sign-in.** It shows the square you point your
  phone at, and the code to type by hand if you are setting it up on the same
  device. It says plainly that nothing changes until you have entered a working
  code — so a mis-scan cannot lock you out — and then shows your ten emergency
  codes once, saying why they can never be shown again. If your dealership
  requires two-step sign-in and you have not set it up, this is the one page you
  land on, and signing out still works so you are never stuck at a shared desk
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

- **A dealership's existing customers and cars can be brought in from a file.**
  This is the first thing a real dealership needs and the stage that was at zero.
  Three things make it safe to use on a business's own history. You can do a
  **practice run** that changes nothing and tells you exactly what the real one
  will do — how many would be added, how many are already here, and which rows
  have a problem. **Running the same file twice does not create everything
  twice**; cars are matched by their VIN and customers by their number in your
  old system. And a bad row is reported **by its line number as you see it in a
  spreadsheet**, quoted back to you word for word, with the rest of the file
  still imported. Fix the file, run it again
- **Work that takes minutes now happens in the background.** A file with
  thousands of rows would time out if the website waited for it. Submitting
  returns straight away with something you can watch, and the work carries on
  behind the scenes — which is also the groundwork for everything else that will
  need to run on a schedule
- **A page for moving records in and out.** Choose a spreadsheet from your old
  system and it insists you run a **practice first** — which changes nothing and
  tells you exactly what the real one would do. Only then does the real button
  become available, and it locks again if you pick a different file. Rows that
  could not be read are listed by **the line number you see in your own
  spreadsheet**, quoted back word for word, so you fix the file rather than
  guess. Taking records out is two buttons
- **And you can take your records back out again.** This is the part that makes
  the licence mean something: a dealership can download their customers and
  their cars as an ordinary spreadsheet file, and that file is one this system
  will read straight back in. No converter, no export format only we understand,
  nothing to ask us for. It is proved rather than promised — nearly nine hundred
  cars were exported from one dealership and loaded into another, and every
  single row was understood. The file comes with a fingerprint so the other end
  can tell it arrived whole. Taking data out is a separate permission from
  putting it in, because walking out with every customer the group has is a
  different act from loading a supplier's stock list

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
- A way to add a second person who runs the servers. There is one, created when
  the system is set up, and no way to add another yet — nor to get back in if
  they lose their phone
- Creating a new dealership from the administration side. It can list them and
  take one out of service; setting a new one up is still a developer's job
- Cancelling an import once it has started, or watching its progress while it
  runs — it reports when it finishes
- Moving anything beyond customers and cars. Enquiries, deals and the books have
  no way in or out, so "take your data with you" is true of the two record types
  a dealership migrates first and not yet of the whole business

---

## Next

**A rehearsed backup and restore** — the last thing standing between here and a
finished foundation. A backup nobody has restored from is not a backup, so the
deliverable is the drill: back everything up, put it back under different names,
and then prove the restored copy *actually works* by running the full end-to-end
check against it. Counting rows would not be proof.

Writing and documenting the procedure is mine. The restore itself needs you at
the keyboard, because it touches databases outside the project folder.

**Still outstanding from the foundation:** a rehearsed backup and restore. A
backup nobody has restored from is not a backup, so the deliverable is the drill:
back everything up, put it back under different names, and prove the restored
copy actually works by running the full end-to-end check against it. Counting
rows would not be proof. The restore itself needs you at the keyboard.

Signing in with an existing company login stays parked: it cannot be honestly
built or tested without a real login provider to test against, and a fake one
would prove nothing.

---

## What is blocked on you, not on me

| Item | Why it matters |
|---|---|
| **Pointing a phone at the setup page** | Everything else about the screens has now been checked on a real browser. The one thing left is physical: does a phone camera actually read that square? |
| Pilot dealerships and access to their current systems | Stage 2 cannot be finished or proven without real data and a real provider connection |
| A rehearsed backup and restore | Stage 1 cannot close without proving a real restore produces a working system |
| Confirming the month-end rule with a real dealer's accountant | Calendar month end with a grace period to the 10th is assumed, not confirmed |
