# Where we are

Plain language, no jargon. For the engineering detail — every claim paired with
the command that proves it — see [implementation/STATUS.md](implementation/STATUS.md).

**Last updated:** 12 August 2026

---

## The short answer

**The foundation is finished. The first dealership records work, records move in
and out as ordinary files, and there are nine screens that provably draw.**

Several dealership groups can already share one installation without ever seeing
each other's data, and staff can be restricted to their own location. Customers
and vehicles can be recorded and found, each location's stock is kept separate,
a car can be sold, and the sale writes its own accounting entry. Whoever runs the
servers is structurally shut out of all of it, and can only come in through a
door you can see them open. A dealership can bring its old records in from a
spreadsheet and take them all out again. And the backup has actually been
restored from — proven by running the whole system against the restored copy,
not by assuming.

Ten screens exist — customers, enquiries, the deal desk, the stock list, the
trial balance, setting up two-step sign-in, moving records in and out, and a
console for whoever runs the installation — and a machine checks on every change
that they really appear and behave, which until recently only a person could
confirm.

**The everyday product now runs end to end on the screen**: an enquiry arrives,
is chased, and becomes a deal on a specific car with the two linked.

**And the dealership no longer stops when the customer drives away.** A car can
be booked in days before it arrives — the workshop can see what each day is
already committed to — and when it turns up, one click opens the job. It is then
worked on and invoiced, with parts coming off the shelf at cost so the department
has a real profit figure, and work nobody agreed to pay for cannot reach the
bill. The month can be closed and locked. The customer is handed a printed order
or invoice.

**Every one of those has a screen.** There is no part of the system left that
only a developer can reach.

**And a dealership can now be set up without a developer** — one form in the
operator console creates it, opens its books, and produces a one-time code for
its first manager.

**There is now a screen that answers "how did we do this month?"** — front, back
and service profit against last month, how many cars went out, and how old the
unsold stock is. Every figure on it is read out of the accounts rather than added
up a second time, so it cannot drift away from the books.

**And it is now something you can install.** A Linux container or a Windows
service — one thing either way, because the application serves its own screens.
There is no separate web server to set up.

**The application now speaks six languages: English, Spanish, French, German,
Russian and Arabic.** Somebody picks their language on the sign-in screen — before they
have an account, because otherwise you have to read English to find out how to
stop reading English — and every screen a dealership uses follows: the menu, the
buttons, the column headings, the empty screens, and the sentences that explain
a refusal. Prices, dates and counts follow it too, so a French screen writes
`1 250,00 $US` where an English one writes `$1,250.00`.

**Choosing Arabic turns the whole page round**, right to left, with the menu,
tables and buttons mirrored. Vehicle identification numbers, stock numbers and
security codes stay the way round they are printed, because a VIN read backwards
is a different car.

Two things it deliberately does **not** do. It never translates your records —
a customer called "Bob & Sons Motors" is that in every language, and so is every
vehicle description, note and part number. And a handful of detailed error
messages from the server are still English; those are named in the engineering
notes and closing it is a change to the server rather than to the screens.

**About 72% of the first release. Four of the eight stages are finished and three
more are under way.**

It moved from 69% because the service lane went from half-built to three-quarters
— the booking diary was one of the two things stage 5 was missing. Only finance
applications to lenders remain, and those need a lender to test against.

The languages did not move that number, and it would be dishonest if they had.
It measures how much of the *plan* exists, and speaking six languages was never
a line on the plan — it makes what is already built usable by more people rather
than building more of it.

---

## The journey

Eight stages to a working pilot with real dealerships. The plan estimates 7–8
months of focused work.

| | Stage | What it means | Status |
|---|---|---|---|
| 0 | Find pilot dealers, get provider access | Agreements, real data samples, access to the systems we must connect to. People work, not code. | **Yours to do** |
| 1 | Foundation | Keeping dealership groups apart, locations, staff and permissions, signing in, backups. | **Done** — only signing in with an existing company login is missing, and that needs a provider to test against |
| 2 | Moving data in and out | Importing a dealer's existing records, syncing with their current system, proving nothing is lost or duplicated. | **Half** — in and out both work, with a screen. Keeping in step with another live system now has its machinery built and tested, but no connection to a real system yet |
| 3 | Customers, vehicles, inventory | The first records a dealership would actually use day to day. | **Done** |
| 4 | Leads and selling a car | Following up a lead, building a deal, trade-ins, approvals, warranties, paperwork. | **Done** |
| 5 | Financing and the service lane | Finance applications, contracts, appointments, repair orders. | **Three quarters** — the workshop, parts stock and the booking diary are built. Finance applications to lenders are not, and need a lender to test against |
| 6 | Reports and administration | Dashboards, and the tools to run the system without a developer. | **Done** — balances, month-end, staff admin, an operator console, setting up a new dealership, and a dashboard for the month |
| 7 | Ready to hand to a real dealership | Security testing, performance, backups, training, installation. | **Half** — backups are rehearsed and there are two installable packages. Security testing, performance work and training are not |
| 8 | Live pilot | Two dealerships running on it for 60 days. | Not started |

---

## Inside stage 1 — done

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
- [x] **A rehearsed backup and restore** — done and proven, not just written

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
- **Nine screens** — customers, the deal desk, the stock list, the trial balance,
  setting up two-step sign-in, moving records in and out, and three for whoever
  runs the installation — each showing something sensible while loading, when
  empty, when you lack permission, and when the server fails, with a way to try
  again
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
- **A car can be sold from start to finish on the screen.** Pick the buyer, pick
  a car that is actually available, put the price and the fees in, take a
  trade-in, send it to a manager, approve it, hand it over. The car's location is
  taken from the car rather than asked for again, and once a deal goes to a
  manager the form disappears entirely rather than sitting there greyed out —
  because a greyed-out form still looks like somewhere to type
- **A deal desk.** What is being sold right now, and what stage each one is at.
  Open one and you see the price, the fees, the discount, the trade-in, and what
  the customer actually owes — then send it to a manager, approve it, or hand the
  car over. The rules that already existed are now visible: the numbers freeze
  the moment a deal goes to a manager, and it says so; and if you are the person
  who built it, it tells you a manager has to be the one who signs it off
- **A customer page — the first screen staff would actually use all day.** Search
  by name, phone, or email. Adding somebody **looks for them first**, and if
  anyone similar is already on file it shows who, with enough detail to recognise
  them, and asks whether that is the same person. It does not refuse — two people
  really do share a name, and a system that blocks the second one just gets a
  fake name typed in instead. But two records for the same customer is the
  failure that quietly ruins a system like this: their service history splits,
  their deals end up under the wrong name, and nobody notices until it matters
- **A page for moving records in and out.** Choose a spreadsheet from your old
  system and it insists you run a **practice first** — which changes nothing and
  tells you exactly what the real one would do. Only then does the real button
  become available, and it locks again if you pick a different file. Rows that
  could not be read are listed by **the line number you see in your own
  spreadsheet**, quoted back word for word, so you fix the file rather than
  guess. Taking records out is two buttons
- **A backup that has actually been restored from.** The scripts back up
  everything, put it back under different names *next to* the originals, and then
  prove the restored copy works by running the full end-to-end check against it —
  because a backup nobody has restored from is not a backup. Doing the drill
  found a real trap: the restored system would have quietly gone on reading and
  writing the *live* databases, which is worse than a restore that plainly
  failed. That is fixed and the drill now catches it
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
- **The enquiry that comes before the sale, on a screen.** Somebody rings up or
  walks onto the lot; that enquiry is taken down against a customer, with the car
  they asked about and what they actually said, and then chased. It is picked up
  by whoever is going to work it and can be put back for somebody else. Every
  move carries a note, and the whole story stays on the record — including the
  customer who went quiet in March and walked back in in June, which is one
  enquiry reopened rather than a second one that loses the first attempt. When
  they buy, one button opens the deal desk with the buyer already filled in and
  the enquiry attached, so the sale can be traced back to where it came from
- **The screen offers only the moves the rules allow, and it does not know them.**
  It asks the server what this particular enquiry can do next and draws exactly
  that. It matters because the alternative — the screen keeping its own copy of
  the rules — is how two versions of the same rule end up disagreeing, and the
  one people see is always the wrong one

### The workshop

- **A car can be booked in, worked on, and invoiced.** Against the customer's own
  car, not one of yours off the lot — which sounds obvious and is the distinction
  that decides whether the whole thing still works the day after a warranty runs
  out
- **Work is recorded as hours and a rate, not just a total.** "It took 1.5 hours
  and we charged 120 an hour" answers the question a workshop manager actually
  asks; "180" does not
- **A bill cannot include work nobody agreed to pay for.** A technician takes a
  wheel off and finds the brakes are shot. That gets written down straight away —
  but the job **cannot be invoiced** until somebody has actually rung the customer
  and recorded what they said. The refusal names the specific job you still need
  to ring about, so it sends you to the phone rather than just saying no
- **Saying no is a proper answer, not a deletion.** Declined work stays on the
  record at nothing. A year later, when the same fault brings the same car back,
  "we did offer and you declined" is written down rather than remembered
- **Writing work up and saying the customer agreed are different jobs.** A
  technician can do the first and not the second. They are deliberately *not*
  forbidden from being the same person — in a small shop the advisor who spots it
  is usually the one who telephones — but recording the answer is its own
  deliberate act, with a note saying how it was obtained
- **Invoicing writes its own accounting entry**, splitting labour from parts,
  because "we sold 464 of service" tells a manager nothing and "180 labour, 284
  parts" is the number they run the department on

### The workshop, on screen

- **The first thing you see is the calls somebody owes.** Not a list of jobs — a
  list of customers who need ringing, because every one of those is an invoice
  that cannot go out until somebody picks up the phone
- **The bill is refused by name.** Try to invoice a job with work nobody has
  agreed to, and it tells you *which* piece of work and who to ring about it.
  The button is deliberately not greyed out: a refusal that names the problem is
  worth more than a button that quietly does nothing
- **Recording the answer asks how you got it** — "phoned 14:20, spoke to Mr
  Dhillon" — because that is the part that matters if the bill is ever questioned
- **Work the customer declined shows a dash, not a price.** A figure in a money
  column reads as a charge
- **Labour, parts and sent-out work are totalled separately**, because "we sold
  532 of service" tells a manager nothing
- **A job can be given to a named technician**

### Getting back in

Somebody who forgot their password used to have nowhere to go. Now there are two
ways, and a dealership needs neither an email server nor anything bought in.

- **With their authenticator app.** Anybody set up with two-step sign-in can get
  back in on their own — a code from the app, or one of the recovery codes they
  saved when they set it up. No manager involved, because the commonest case is
  somebody who has their phone and has simply forgotten a password
- **With a code from a manager**, for somebody who has lost the phone as well.
  Read out across the desk, good for four hours, works once
- **Issuing one is its own permission.** Not folded into managing staff: adding a
  starter is admin, but handing over the ability to sign in *as* an existing
  colleague — possibly a more senior one — is a different act. A dealership that
  wants resets held by fewer people than rotas can already arrange that
- **The screen never says whether an address exists.** A wrong code and an
  unknown email give the same answer, word for word. Otherwise the page becomes a
  way for a stranger to find out who works there and who is easiest to target
- **Resetting signs out every device.** Somebody recovering an account may be
  recovering it *from* someone
- **The dealership can see a reset was handed out**, on the person's own record,
  until it is used or expires — not buried in a log

Not there yet: a passkey or fingerprint, and the email and text-message routes,
which are designed and switched off because they need an account you would have
to buy.

### Cars that have not arrived yet

The workshop used to start when a car was physically at the counter. It can now
take a booking days ahead, on the same screen as the jobs already on the ramps —
because "what have I got today" is both halves of that question at once.

- **One click turns a booking into a job.** When the car turns up you press
  "It's here", and the job opens with the customer, the car and what they said
  already on it. Behind that, the booking and the job are written together or
  not at all — so the diary can never show a car that arrived with no job
  against it, nor lose track of one it opened
- **A car cannot be booked in twice.** Press it again and you are told which job
  already exists, because a second job means one visit billed twice. This is the
  one rule the whole diary is built around
- **Each day shows what it is already committed to** — how many cars and how many
  hours of work. That is the only question a diary is ever really asked
- **A full day still takes the booking.** Workshops overbook deliberately: jobs
  come in under estimate and cars get collected late. The screen shows you the
  load and lets you decide. A system that refused would be worked around inside
  a week by booking everything as "no estimate", which would make the figure
  worthless
- **A car that never came is kept, not deleted** — and "they rang to cancel" is
  recorded separately from "nobody rang at all". The second one is how you know
  who to ring the day before next time
- **Nobody estimated it** shows as exactly that, rather than as zero hours

Not there yet: the diary loads a *workshop*, not a person or a ramp — "who is
free at eleven" is a different question. And it knows who is expected tomorrow
but sends nobody a reminder, because there is no way to send a message yet.

### What gets sold with the car

- **Warranties, GAP and cover are sold on the deal**, each with its own price,
  and they go onto what the customer owes
- **The price is yours to change.** It starts from the catalogue and you type over
  it — because that is how these are actually sold, and the figure you agree is
  the one recorded
- **Changing the price list later does not touch deals already done.** Last
  month's profit stays last month's profit
- **You can see what each one made**, separately from the car. On a lot of deals
  this is the bigger number
- **Withdrawing a product stops it being offered without unselling it.** Deals
  that already have it keep it, exactly as agreed

### Paperwork the customer takes away

- **A vehicle order and a service invoice**, printed from what is already
  recorded. Open it and use your browser's print command, or save it as a PDF
- **Nothing on it is yours alone.** What a warranty cost you, what you made on it,
  what a part cost — none of that reaches a page you hand across a desk. There is
  a test that reads the finished document to make sure
- **Declined work is printed at nothing rather than left off.** A customer who
  said no in March and comes back in September can see they were told
- **A job that has not been invoiced prints as a job sheet** and says on its face
  that it is not a bill
- **The columns add up.** A trade-in shows as money off, so reading down the page
  gets you to the total printed at the bottom

### Closing the month

- **A month gets closed on purpose.** There is no date that does it for you, and
  no countdown — because the close takes however many days the work takes. You
  close it when you are finished
- **A closed month stops taking postings**, and says so. Somebody invoicing a job
  into a locked month is told the month is closed rather than getting an
  unexplained failure
- **You can reopen one when something genuinely turns up late** — but it asks why,
  and your answer goes on the month's record permanently. Anybody looking later
  can see the month was reopened and what for
- **Reopening is a separate permission from closing.** Closing is routine
  month-end work; reopening lets a figure you have already reported move, so it
  can be held by fewer people
- **The books have a start you choose.** Nothing can be posted into a month until
  it is open — so the accounts begin where you decided, not wherever somebody
  first mistyped a date
- **Correcting a closed month still works the same way it always did.** You post
  a reversal, dated today, and the closed month stays exactly as you reported it

### How did we do this month

- **One screen, one question.** What the month made, split into the car, the
  warranty, and the workshop — because those are three different businesses and
  one combined number hides whichever is doing badly
- **Every figure is set against last month**, in words rather than only an arrow.
  A number on its own does not tell anybody whether it was a good month
- **The figures come out of the accounts**, not from adding the deals up a second
  time. Two sets of arithmetic over the same month eventually disagree, and then
  nobody can say which is right
- **It says whether the month is still open**, because that is what decides
  whether any of it can still change
- **How old the unsold stock is**, in thirty-day bands with the worst offenders
  named — and clicking one takes you straight to that car
- **You see what you are entitled to.** Somebody who may look at the stock but not
  the money sees the stock, and is told in a sentence why the rest is missing
  rather than being shown an empty page that looks like a bad month
- **Light or dark, and it mirrors for right-to-left reading.** The whole
  application does, not only this screen
- **It can be driven without a mouse.** Press `?` for the list

### Parts, and what the workshop actually earns

- **A part is real stock now.** It has a number, a description, and a count on
  each location's shelf — and the number is matched however anybody types it, so
  `MZ-690411`, `mz690411` and `MZ 690 411` are the same component rather than
  three catalogue entries
- **The workshop finally has a profit figure.** Until now a job recorded what it
  billed and nothing about what it cost. Selling a part takes it off the shelf at
  cost, and the books carry that cost — which is the difference between "we
  invoiced 532" and "we made X on it"
- **You choose how parts are valued**, and can change it whenever you like:
  average cost of what is on the shelf (the default), the last price you paid, or
  oldest-stock-first. The screen explains each one in a sentence
- **Changing it only affects future sales**, and says so. Work already invoiced
  keeps the cost it was sold at — nothing you have already reported on can move
  underneath you
- **You can see why a part costs what it does.** Every delivery still on the
  shelf is listed with its date, its delivery note and its price. The average is
  not a number you have to take on trust
- **You cannot sell parts you do not have.** Invoicing a job for stock that is
  not there is refused, and the refusal names the part and how many are actually
  on the shelf, so somebody goes and books the delivery in

### The people who work there

- **You can see who works here, and what each of them can reach.** Their name,
  their email, the roles they hold, whether they have set up two-step sign-in,
  and whether they are working, stopped, or still waiting to set a password
- **Adding a starter never means typing a password for them.** The account is
  created unable to sign in, and the screen produces a one-time code you read
  out. They set their own password with it. Nobody at the dealership ever sees or
  chooses it — including whoever added them
- **The code can only be shown once, and the screen says so.** Only a scrambled
  copy is kept, so it genuinely cannot be looked up again. If it goes astray you
  make a new one, which stops the old one working
- **Handing an enquiry to a named colleague works.** The lead list says who is
  chasing each one by name instead of "somebody else"
- **Somebody who manages one location cannot hand out access everywhere.** They
  can give somebody a role at their own lot; granting access across the whole
  group needs group-level permission. Without that split, whoever runs one site
  could quietly give themselves the lot
- **Stopping a leaver takes effect immediately** — their sessions end on their
  very next click, not whenever they would have been signed out anyway. Nothing
  is deleted: their name still has to appear against the deals they did
- **You can finally see the support role.** When someone from the vendor visits
  your dealership to help, a support role is left behind in your system. Until
  now you had no way to see it existed. It is on the roles list like everything
  else, with exactly what it can reach spelled out

## What does not exist

- **A full set of books.** The month closes and locks, but there is no year-end,
  no comparative statements, no budgets, and nothing in a format a tax authority
  would recognise
- **Ordering parts.** You can book a delivery in and sell from the shelf, but
  there are no purchase orders, no supplier records, no stock takes, and no
  returns to a supplier. Counting the shelf and correcting it is still a job for
  a spreadsheet
- Finance applications and lenders. The **products** sold alongside a car —
  warranties, cover, service plans — do exist and carry their own profit; sending
  an application to a lender does not
- Taxes and registration fees calculated by jurisdiction
- **Signed** paperwork. A vehicle order and a service invoice both print; nothing
  captures a signature
- Automatic follow-up reminders — the workshop knows who is expected tomorrow
  and there is still no way to send anybody a message
- Photos of a vehicle
- Planning **who** is working on what tomorrow. A service booking now exists and
  loads the workshop as a whole, but not a named technician or a particular ramp
- An enquiry marked "Appointment" is still only a status on that enquiry; it does
  not put anything in the service diary, which is a different department's book
- **Reports beyond the month.** There is a dashboard for the current month and a
  trial balance. There is nothing for a quarter or a year, no comparison between
  locations side by side, and no league table by salesperson or advisor
- ~~Reading the dashboard in another language~~ — built. Every screen in the
  product, the dashboard included, now reads in whichever of the six languages
  you pick. What is still English: the server's own refusal messages about a
  *record* ("a deal's terms are frozen once it is submitted"), and the printed
  paperwork a customer takes away
- A way to add a second person who runs the servers. There is one, created when
  the system is set up, and no way to add another yet — nor to get back in if
  they lose their phone. **Dealership staff can now recover an account; whoever
  runs the installation still cannot**, and that gap is deliberate for now:
  there is nobody above them to hand out a code, so it needs a different answer
- ~~Resetting a forgotten password~~ — **built, 9 Aug.** See "Getting back in"
  above. Two ways, neither needing anything bought in. A passkey, an emailed
  link and a text message are still to come
- Cancelling an import once it has started, or watching its progress while it
  runs — it reports when it finishes
- Moving anything beyond customers and cars. Enquiries, deals and the books have
  no way in or out, so "take your data with you" is true of the two record types
  a dealership migrates first and not yet of the whole business

---

## Next

**Talking to another dealership system — the machinery, 12 Aug.** Not a
connection to anything real: there is still nothing on the other end. What was
built is the part that decides *how far a system has been read*, and it was built
first on purpose, because the mistake it prevents is the one you cannot recover
from.

The problem in plain terms. When we read a supplier's system every night, we have
to remember where we got to, or we would re-read everything from the beginning
each time. The obvious way to remember is to write down what we *asked* for —
"give me the last three days" — and move on. The trouble is that suppliers very
often serve less than they were asked for, and quite often will not say what they
served at all. Write down the request, and the days that never arrived are gone:
no error, no warning, nothing on any screen. It surfaces months later when
somebody notices a week of sales missing, by which time the supplier no longer
holds them.

So this refuses. It moves the marker only across what the supplier actually
confirmed sending, and where the supplier says nothing, the marker stays put and
the same period is asked for again tomorrow. Reading the same days twice is
harmless — bringing a record in twice is designed to be safe. Skipping days is
not. A dealership stuck this way is also **counted**, because one night of it is
ordinary and six in a row is a store falling behind that nobody has told you
about.

Records that arrive and cannot be understood are set aside rather than guessed
at, kept with exactly what the supplier sent so somebody can see the original —
and, because that is a real customer's details, held for 90 days and no longer.

Four of these decisions came from reading your four older integration projects.
Every one of them is a mistake that had already happened somewhere, which is why
the machinery was worth building before the first real connection rather than
after.

**~~A forgotten password~~ — built, 9 Aug.** See "Getting back in" above. Two of
the five methods you chose are live: the authenticator app, and a manager handing
out a code. Email and text message are wired into the design and switched off,
because they need an account somebody has to go and buy. A passkey or fingerprint
is the one still to come; it needs an outside library, which is a decision worth
making deliberately rather than in passing.

The honest list is now short and mostly waiting on you: connectors to a real DMS,
and a pilot to point them at.

Signing in with an existing company login stays parked: it cannot be honestly
built or tested without a real login provider to test against, and a fake one
would prove nothing.

---

## What is blocked on you, not on me

| Item | Why it matters |
|---|---|
| **Pointing a phone at the setup page** | Everything else about the screens has now been checked on a real browser. The one thing left is physical: does a phone camera actually read that square? |
| Pilot dealerships and access to their current systems | Stage 2 cannot be finished or proven without real data and a real provider connection |
| ~~Confirming the month-end rule~~ | **Answered 6 Aug.** Cutoff is the calendar month end; the close runs over the next few business days and the month is locked at the end of it. Now recorded and no longer blocking |
| Deciding where backups are kept | The scripts write `.bak` files onto this machine and stop there. **Each one holds every customer record in plain form** — copying them somewhere safe, and encrypting them, is a decision about your customers' data that I should not make for you |
