# 11 — Franchise and External Scope

What it takes for this to be a dealer management system a franchised store can
actually run on, rather than one that only handles its own records.

Raised by the project manager on **2026-08-14**. Every term below was checked
against industry and government sources before being written down, because
several of them mean something narrower — or wider — than they sound. Where the
research contradicted the original phrasing, the correction is stated plainly
rather than quietly applied.

> This is scope, with each item labelled by **what actually blocks it**. Most of
> it is not built. Five items now are — the public recall lookup, Spanish, pay
> type on service work, reconditioning onto the car, and the labour report — and
> each is marked `Done` in the
> register at §12. Read [`implementation/STATUS.md`](implementation/STATUS.md)
> for the evidence behind any of it.

## 1. The finding that reframes everything else

**A franchised dealership cannot choose its DMS freely.** Vehicle manufacturers
run certification programmes, and certification is a condition of doing
business: it covers warranty claim submission, incentive reporting, parts
ordering, vehicle inventory feeds and monthly financial statement submission.
Requirements differ per manufacturer; some certify several vendors and let the
dealer pick, others are effectively exclusive.

This is not an engineering task. It is a commercial and legal programme, one per
manufacturer, and it gates roughly half the list below.

**It also splits the product in two:**

| | Franchised dealer | Independent dealer |
|---|---|---|
| OEM certification | **Required before they may use us at all** | Not applicable |
| Warranty claims | Core daily work | Rare or none |
| Manufacturer reporting | Mandatory, per brand | None |
| Recalls | OEM feed, per brand | Public NHTSA data only |
| Realistic path for us | Years, per brand, with contracts | **Open today** |

Independent used-car dealers need none of it. That makes "which market first" —
already the overdue decision **N3** in the handover — the decision that governs
this entire document. Building OEM plumbing before choosing is building for a
customer we have not identified.

## 2. The industry already has a data standard, and we did not use it

**STAR** — Standards for Technology in Automotive Retail — is the body that
defines how dealers, manufacturers and their vendors exchange data. It publishes
**over 145 XML message formats across more than 40 business areas**, and
Volkswagen Group of America, among others, certifies DMS integrations against
STAR XML through its Dealer Communication System. STAR's newer work moves to
JSON and a shared Retail Automotive Domain Model.

**This lands directly on a decision we already made.** `ContractFields.cs`
declares our own contract vocabulary (`customer.firstName`, `customer.email`,
and so on) so that a connector translates a vendor's names into ours and one
sink serves every provider. The seam is right. **The vocabulary is invented when
a standard one exists.**

Nothing needs to be undone today — the indirection is exactly what makes this
cheap to change later, and one sink still serves every provider either way. But
the naming should be reconsidered against STAR before there are many contracts,
not after. Recorded as an open decision rather than acted on, because adopting
STAR properly means reading the specification, not renaming constants to match a
press release.

## 3. Scope, by what blocks it

### 3.1 Blocked on a manufacturer relationship

Nothing here can be built or honestly tested without a signed relationship with
a specific manufacturer. A fixture pretending to be Ford proves nothing — the
same reason OIDC federation is still open.

| Item | What it actually is |
|---|---|
| **DMS certification** | Per-manufacturer approval. Covers warranty, incentives, parts, inventory feeds, financial statements. A business programme, not a sprint |
| **Manufacturer APIs** | Brand-specific interfaces, typically STAR-based. Not one integration — one per brand |
| **Warranty repair orders** | Claim submission and adjudication. Manufacturer pays technician labour at its own agreed rate, not the retail rate |
| **Per-manufacturer reporting** | Sales, inventory and financial statement submission on the brand's schedule and format |
| **Customer feedback to the manufacturer** | The industry name is **CSI** (Customer Satisfaction Index). The manufacturer runs the survey; the dealership's job is supplying accurate contact and transaction data, and living with the score |
| **Recalls from the manufacturer** | Only the OEM knows whether *this VIN* has had a given recall performed. See §3.4 for what is public |

### 3.2 Blocked on a commercial contract, not on approval

Real APIs exist and are documented; they need a paying relationship.

| Item | What the research found |
|---|---|
| **Auction platforms** | Manheim runs a public developer portal covering offerings, purchases, searches, titles, transactions and valuations (Manheim Market Report). ADESA/OPENLANE and ACV are the other major channels. Commercial terms, not open data |
| **Credit reports** | Dealers rarely contract the three bureaus directly; they go through an aggregator such as 700Credit or Dealertrack. See §4 — this one carries the heaviest legal load in the whole document |
| **Title history checks** | **NMVTIS**, run by the US Department of Justice, is the authoritative source for title brands (junk, salvage, flood), odometer readings and theft records. Reached through approved data providers |
| **Plate to VIN** | Commercial APIs with 50-state coverage. There is no free government equivalent |

### 3.3 Blocked on a jurisdiction decision

**A correction to the original phrasing.** There is no "county DMV" for vehicles
and no county API. Titling and registration are **state** functions. What
actually varies by county is **tax**.

- **Registration and titling** run through **EVR/ERT** (Electronic Vehicle
  Registration / Electronic Registration and Title). Dealers do not connect to a
  state directly — each state approves **Service Providers**, sometimes called
  First Line Service Providers, and the dealer's DMS integrates with an approved
  provider. Per state, with a different provider and a different contract in each.
- **Tax** genuinely is local: state, county and sometimes city rates, plus
  documentation fees that some states cap and others do not. This is normally
  bought rather than built.

None of it can be scoped before **N3** picks a market. Doc 01 §4 currently
assumes US-only compliance while the product ships six languages with full
right-to-left support; those two cannot both be the priority.

### 3.4 Open today — no approval, no contract, no market decision

**Verified live on 2026-08-14**, not taken from documentation:

- `https://vpic.nhtsa.dot.gov/api/vehicles/DecodeVinValues/{vin}?format=json`
  decodes a VIN. No key. Returned a correct decode for a seeded VIN.
- `https://api.nhtsa.gov/recalls/recallsByVehicle?make=&model=&modelYear=`
  returns safety recall campaigns. No key. Returned 24 campaigns for a 2003
  Honda Accord.

**What this genuinely gives us, and what it does not.** NHTSA answers *which
recall campaigns apply to this year, make and model*. It does **not** say
whether a particular car has had the work done — that is the manufacturer's
record, and per-VIN completion status is exactly what §3.1 is blocked on.
Presenting a campaign list as "this car has open recalls" would be telling a
dealership something we do not know, about a safety matter. The wording on any
screen has to carry that distinction, not bury it.

It is still worth having: it tells a dealership which cars on the lot need
checking with the manufacturer before they are sold.

Also open today, and needing nothing external at all:

- **Pay type on service work** (§5).
- **Labour reporting**, partially (§6).
- **Passkeys** (§7).

## 4. Credit reports: the one to be slowest about

The manager listed Equifax, Experian and TransUnion under "paid reports". The
cost is the smallest part.

Pulling a consumer credit report requires a **permissible purpose** under the
Fair Credit Reporting Act. The FTC has been explicit that a request for a test
drive is not one. The safe basis is the consumer's own written authorisation,
and a hard pull without it has produced real FCRA litigation against dealerships.

Beyond permissible purpose, holding this data drags in a compliance surface far
larger than anything currently in the product: adverse-action notices under
Regulation B when credit is declined, the Red Flags Rule for identity theft, and
the FTC Safeguards Rule for how the data is protected.

**The engineering consequence is the part that matters here.** Today the most
sensitive thing this system stores is a customer's address and a password hash.
A credit file changes the blast radius of every existing decision — encryption,
audit, retention, who may read what, and what a support visit is allowed to see.
It should be designed deliberately, not added as another integration, and it
should not be first.

## 5. "A table for service pays" — what it turns out to mean

The manager was unsure of this term. The research resolves it: in dealership
usage a repair order carries a **pay type**, and there are three.

| Pay type | Who pays | Why it is structurally different |
|---|---|---|
| **Customer Pay** | The vehicle owner | Retail labour rate. Most of the revenue |
| **Warranty** | The manufacturer | Manufacturer's agreed labour rate, and a claim to submit |
| **Internal** | Another department of the dealership | Reconditioning stock, demos. No external revenue at all |

**Built on 2026-08-15.** `ServiceLine` now carries a pay type, set per line
because one job routinely mixes all three. The customer is billed for their
share alone; warranty debits a receivable (1200) because the manufacturer has
not paid yet; internal debits its own charge account (5400). Revenue is credited
with all of it, because the workshop sold all of it.

**D4 is now settled in full.** Reconditioning a car the rooftop owns is
capitalised onto that car (1300) instead of charged to 5400, so used-vehicle
gross no longer flatters itself by the amount spent making the car saleable. The
workshop asks Inventory whether the vehicle is owned rather than guessing;
anything not in stock - a courtesy car, a director-s vehicle - still lands on
5400, because there is no unit to put it on.

That single missing field is underneath four separate items on the manager's
list: warranty claims to the manufacturer, per-manufacturer reporting, honest
labour reports, and any real service gross figure. It is also the only item on
the list that needs nothing external.

**It is not a small change, and it is not safe to half-build.** Warranty work is
receivable from the manufacturer, internal work is a cost moved between
departments, and only customer-pay work is money the customer owes. Adding the
field without teaching the ledger the difference would let somebody mark a line
"Warranty" and still bill the customer for it — a field that lies is worse than
a field that lies is worse than a field that is missing — which is why the
ledger learned the difference in the same change rather than after it.

## 6. Labour reports: what is computable and what is not

The industry uses two measures that sound alike and are not:

- **Efficiency** — hours produced ÷ hours available. NADA guideline **125%**: a
  technician on flat rate is expected to beat book time.
- **Productivity** — hours billed ÷ hours clocked. NADA guideline **87.5%**.
  Low productivity usually means dispatch, parts delays or hand-off friction
  rather than a slow technician.
- **Effective Labour Rate** — labour revenue ÷ labour hours sold. What the shop
  actually realises, as against the posted rate.

**Against what we store:** a repair order records hours, a rate and one assigned
technician, so **hours sold and effective labour rate are computable today**.
**Efficiency and productivity are not** — neither hours available nor hours
clocked exists anywhere in the model, and there is no time clock. Reporting
either would mean inventing the denominator.

**Built on 2026-08-15** at `GET /api/v1/repair-orders/labour`: hours sold,
labour revenue and effective labour rate, per technician and per payer, counted
from invoiced jobs only. The response carries a `notMeasured` list naming
efficiency and productivity, so a screen states the gap rather than leaving a
manager to assume those numbers were fine. Both stay unmeasurable until there is
a roster and a time clock — and both are used to judge individual people, which
is exactly why guessing at them would be worse than omitting them.

## 7. Getting rid of passwords

The manager's instinct is right and the standard is mature. **WebAuthn/FIDO2
passkeys** are classified by CISA as the only phishing-resistant tier of
multi-factor authentication: a key pair where the private half never leaves the
device and nothing replayable crosses the network, so a passkey cannot be
phished, reused, or stolen from our database.

Against the two alternatives the manager offered: **biometrics are not a
separate option** — a fingerprint or face unlocks the passkey on the device and
is never transmitted to us, which is the same mechanism. **IP allow-listing is
weaker than it looks** for a dealership: staff move between rooftops, work from
home, and use mobile data, and it protects nothing against somebody already
inside the building.

This fits what exists. Identity already owns credential verification, TOTP and
session issuance behind a sealed boundary, so passkeys are an additional
authenticator inside that boundary rather than a new system. Practical sequence:
passkeys **alongside** passwords first, then password-optional for accounts that
have registered two, with recovery codes retained — a dealership that locks out
a service advisor at 8am on a Saturday will not forgive it.

**Built on 2026-08-15, with screens.** Registration and sign-in both work end to
end. `/security/passkeys` enrols and removes them; the sign-in screen offers one
as a way in. Two interface decisions were settled before the screens existed, and
both are kept:

- **The passkey button sits beside the password field, not before it.** The
  password stays the primary path until a policy says otherwise, and putting a
  new control in front of the one everybody uses would make the common case feel
  like the exception.

  *How it renders, measured 2026-08-15 in all six languages at 1280px:* on one
  line in English, and wrapped to the line immediately below the password field
  in the other five. "Use a passkey" is three short words in English and five
  long ones in French, and forcing one line inside the 380px card squeezed the
  password box to 140px — narrower than the button beside it, which inverts
  which control is primary. It stays attached to the password field either way,
  which is what the decision was about.

- **The recall check runs on request, not when the screen opens.** It is an
  outbound call to somebody else's service, and a screen left open on a desk all
  afternoon must not keep asking. Built as a band on the stock detail, with a
  button and no automatic fetch.

Two things named here are still not built, and neither is implied by the screens:
**attestation is not verified** (the browser is asked for `attestation: 'none'`,
so nothing is requested that would then go unchecked), and **no policy yet lets a
passkey replace a password** — every account still has one.

## 8. Workflows and triggers

The manager asked for automated workflows and for research into which triggers
are applicable. Grouped by whether the trigger data exists here:

**Triggers we already hold the data for**
- A vehicle arrives, changes status, or is held for a deal
- An enquiry ages without being claimed — *already built, as the Enquiries signal band*
- A repair order opens, has work found on it that nobody authorised, or is invoiced
- A month is closed or reopened
- A deal is submitted, approved, or lost

**Triggers needing data we do not hold**
- **Walk-in identified by plate** — needs the plate-to-VIN service in §3.2, and
  we do not store plates at all
- **Birthdays and anniversaries** — we hold no date of birth, and it should not
  be collected without a reason that survives a privacy review; *purchase*
  anniversary is derivable from data we already have and carries none of that risk
- **Service due by mileage or time** — we record mileage on a repair order but
  no service schedule to compare it against

**Load-balancing repair orders to technicians** is real and has an industry
name — shop loading, or dispatching. It needs technician capacity, skill level
and current committed hours. We have one technician per repair order and no
capacity model, so this is blocked on the same gap as §6.

**Predicting a customer's next vehicle** is **equity mining**, and it is a
mature product category rather than a novel AI idea. It scores a customer on
loan balance, payment history, credit score and current market valuation to
estimate readiness to trade. Three of those four are data we do not hold — and
one of them is a credit file, so §4 applies in full. Worth wanting; not close.

## 9. Copying what other systems do on screen

Reasonable as stated — staff who know another DMS should not have to relearn
basic flows, and matching a familiar sequence lowers the cost of switching.

Two limits worth stating in advance. Copying screen *layouts* from a commercial
product is a legal question, not a design one; copying *workflow order* is
normal and fine. And the flows in incumbent systems carry decades of accumulated
compromise — several of the defects this project has deliberately avoided are
things established systems still do. Match the sequence a person expects. Do not
inherit the mistakes.

## 10. People

Outside engineering, recorded so they are not lost:

- **Ambassadors** — dealer-facing advocates. Depends entirely on **N3**: an
  ambassador to franchised dealers and one to independents are different people
  selling different things.
- **Contributors** — the repository is AGPLv3 with a contribution guide, a code
  of conduct and a security policy already in place, so the mechanics exist.
  What does not exist is anything a contributor could pick up: no public issues,
  no "good first issue", and the project has never been pushed to a remote.

## 11. Open decisions this creates

| # | Decision | Why it cannot be deferred much longer |
|---|---|---|
| **D1** | Franchised or independent dealers first | Governs §3.1, §3.2, §3.3 and both roles in §10. Already overdue as **N3** |
| **D2** | Which jurisdiction | Tax, titling and privacy all follow from it. Six languages and US-only compliance point in different directions |
| **D3** | Adopt STAR vocabulary for `ContractFields`, or keep our own | Cheap now, expensive once several connectors exist |
| ~~**D4**~~ | ~~Which accounts warranty and internal work post to~~ | **Settled 2026-08-15.** Warranty is a receivable from the manufacturer (`1200`), internal work is a charge against the dealership (`5400`), and reconditioning on a car we own is capitalised onto the unit rather than expensed — so used-vehicle cost carries its recon. Built and proven; see `4f0073c` |
| **D5** | Whether to hold credit data at all | Changes the security posture of the whole product (§4) |

## 12. Scope register

The index to §3, in one shape a machine can read — the local dashboard renders
this table directly rather than having the same list typed into it a second
time. **§3 is the detail; this is the summary.** Keep them in step: if a row
here disagrees with the section above it, the section is right.

`Blocker` is the single thing that has to change first, and is one of:
**OEM** (a manufacturer relationship), **Contract** (a commercial agreement),
**Market** (decision D1/D2), **Decision** (one of ours, D3–D5), **Build** (only
engineering time), or **Done**.

| Item | Area | Blocker | State |
|---|---|---|---|
| DMS certification per manufacturer | Business | OEM | Not started |
| Manufacturer APIs, brand by brand | Integration | OEM | Not started |
| Warranty claim submission | Service | OEM | Not started |
| Per-manufacturer sales and financial reporting | Reporting | OEM | Not started |
| Customer satisfaction surveys (CSI) | Customer | OEM | Not started |
| Per-VIN recall completion status | Vehicle | OEM | Not started |
| Auction platforms (Manheim, ADESA, ACV) | Inventory | Contract | Not started |
| Credit reports via an aggregator | Finance | Contract | Not started |
| Title history checks (NMVTIS) | Vehicle | Contract | Not started |
| Plate to VIN lookup | Vehicle | Contract | Not started |
| Registration and titling (EVR/ERT) | Compliance | Market | Not started |
| Sales tax by state, county and city | Accounting | Market | Not started |
| Ambassadors to dealers | Business | Market | Not started |
| Adopt STAR vocabulary for contracts | Integration | Decision | Not started |
| Pay type on service work | Service | Done | Built, with a screen |
| Reconditioning capitalised onto the car | Accounting | Done | Built |
| Technician load balancing | Service | Decision | Not started |
| Equity mining and next-vehicle prediction | Sales | Decision | Not started |
| Labour reports: hours sold, effective rate | Reporting | Done | Built, with a screen |
| Passkeys alongside passwords | Security | Done | Built, with a screen |
| Workflow triggers on data we already hold | Platform | Build | Not started |
| Contributor on-ramp: issues and first tasks | Business | Build | Not started |
| Documentation restructure: reorganise all 18 docs | Product | Build | Done |
| Four-part file headers with a copyright line (doc 08 §5) | Product | Build | Decided, waiting to be scheduled |
| Public safety recall lookup | Vehicle | Done | Built, with a screen |
| Spanish as a sixth language | Product | Done | Built |

## 13. What was done rather than only written down

- **Recall and VIN data from NHTSA** — the one item on the list with no
  approval, no contract and no market decision in front of it. See
  [`implementation/STATUS.md`](implementation/STATUS.md) for what shipped and
  what it deliberately does not claim.
- **Spanish** was already delivered on 2026-08-14, before the list arrived.
