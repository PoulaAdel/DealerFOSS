# ADR-024 — Compliance splits three ways: product baseline, jurisdiction pack, deployment posture

Date: 2026-09-05
Status: Accepted
Supersedes: —

Settles open decision **D2** ([doc 11 §11](../11-Franchise-and-External-Scope.md)),
which asked "which jurisdiction". The question as posed cannot be answered with
one name, and this ADR says what it decomposes into instead.

## Context

The question arrived as one question — *can the system dynamically fit and
enforce multiple legal terms and tax rules according to the user's current
location?* Researching it turned up three things that change its shape.

### 1. "Current location" is never the input

For a vehicle sale, the governing address is the buyer's **registration
address**, not the dealer's location and not where anyone is standing. A buyer
in a high-tax city who drives to a low-tax suburb still pays their own rate;
cross a state line and the home state collects **use tax** at registration
instead. For privacy, the governing facts are the data subject's residence and
the deployer's establishment.

All of those are **recorded addresses on records we already hold**. None of them
is discoverable from an IP address, a browser locale, or a time zone. Resolving
a legal consequence from a network artefact would be a defect with a legal
blast radius, not a feature — a corporate VPN would change a tax rate.

### 2. Tax is two problems, and only the smaller one is ours

**Rates and boundaries** are unbuildable and unbuyable-blindly. There are
**13,000+ US sales tax jurisdictions**, and ZIP codes are the wrong key —
a single ZIP can span several taxing jurisdictions with materially different
rates, because ZIPs are USPS delivery routes and were never boundaries.

**The vehicle-specific arithmetic** is ours, and it is the part a general
retail tax engine gets wrong, because it prices general retail:

- **The trade-in credit.** Most states tax price *minus* the trade allowance.
  California does not — CDTFA Publication 34 taxes the full price, and gives
  the $20,000 car with the $4,000 trade as its own worked example.
- **Doc fee taxability and caps.** Some states cap the fee, most do not, and
  it is part of the taxable price in most. The commonly repeated split is
  "17 capped, 35 uncapped" — which adds to 52, so at least one of those
  figures is wrong. That is exactly why these numbers belong in a pack with a
  cited source and a review date, and not in anybody's head.
- **Leases** are taxed on the payment in some states and the capitalised cost
  in others.

`Deal.Subtotal` and `Deal.AmountDue` already exist and already encode the sign
conventions. The taxable **basis** is the same kind of arithmetic and belongs
next to them, not inside a rate provider.

### 3. There is a free rate source that also shifts the liability

The **23 Streamlined Sales Tax full member states** — Arkansas, Georgia,
Indiana, Iowa, Kansas, Kentucky, Michigan, Minnesota, Nebraska, Nevada,
New Jersey, North Carolina, North Dakota, Ohio, Oklahoma, Rhode Island,
South Dakota, Utah, Vermont, Washington, West Virginia, Wisconsin, Wyoming —
each publish a **rate file and a boundary file, free, updated quarterly**,
keyed to 5-digit *and* 9-digit ZIP areas. And:

> "The states hold a business harmless for charging too much or too little tax
> if the business calculated and collected the incorrect tax based on the
> state's rate and boundary files."

That is the single most load-bearing fact in this document. It converts an
unbounded exposure into a bounded one across a third of the country, it removes
the need for rooftop geocoding in those states (ZIP+4 against the published
boundary file *is* the sanctioned method), and it means the engineering
requirement is **provenance** — which file, which version, which effective date
produced this number — rather than accuracy in the abstract.

It also fixes the shape of everything else. A jurisdiction whose rules we
cannot source and version is a jurisdiction we say we do not support.

### 4. Privacy is the opposite shape, and a rule table would be wrong

Privacy does not vary per transaction, and it cannot be resolved by lookup.

**A dealer that arranges financing is a GLBA "financial institution"** per the
FTC. Most state privacy laws — Virginia, Colorado, Connecticut, Utah — then
grant an **entity-level** exemption. **California grants only a data-level
exemption**: the nonpublic personal information is carved out, the entity is
not. So the *same dealership* is exempt in one state, partly exempt in another,
and the carve-out is per field rather than per company.

A rule that reads `if (state == "VA") exempt` is wrong the moment that
dealership sells to a Californian. Encoding this as data would mean the software
silently issuing a legal opinion about its operator's regulatory status, with
no lawyer in the loop. **We will not do that.**

**The FTC Safeguards Rule applies underneath all of it**, to every US dealer in
every state: a written information security program, a named Qualified
Individual, MFA, encryption in transit and at rest, and notification to the FTC
within 30 days of a security event touching unencrypted information of 500 or
more consumers. That is not a per-jurisdiction rule. It is a floor.

### 5. Self-hosting moves the obligation off us

DealerFOSS is AGPLv3 and self-hosted. **The dealership is the controller and
the taxpayer of record; we are neither.** GDPR Recital 78 *encourages* producers
to design so that controllers can meet their obligations — it does not make the
producer a controller. So the product's job is not to *enforce* the law, which
it cannot do and should not claim; it is to make compliance **possible and
evidenced**: retention that actually runs, export and erasure that actually
reach every store, consent recorded with its version, and an audit trail that
survives a regulator's question.

### 6. And the tax problem is not the same size everywhere

Where the US has 13,000 local rates, the EU used-car trade largely runs on the
**margin scheme**: VAT on the dealer's margin rather than the full price, at one
national rate. A pack that expresses "the basis is the margin, the rate is
national" is a small pack. The architecture has to make that cheap, or it has
quietly encoded the US into its bones.

## Decision

**Compliance is not one dial. It is three, with different owners, different
change rates, and different review regimes.**

| | **Baseline** | **Pack** | **Posture** |
|---|---|---|---|
| What | What every deployment must do | One jurisdiction's *data* | One deployment's own choices |
| Examples | Encryption, MFA, audit trail, retention machinery, erasure, export, consent versioning | Rates, boundaries, fee caps, taxability flags, required disclosures, effective dates | Retention periods, which packs are enabled, whether credit data is held at all (D5), who the Qualified Individual is |
| Lives in | Product code | A versioned pack, enabled per legal entity | Tenant settings |
| Set by | Us | A pack author, reviewed before it is marked Supported | The dealership |
| Changes | Rarely, and never becomes optional | Quarterly | Once, then rarely |

Five rules make it work rather than becoming thirteen thousand conditionals.

### R1 — A pack carries data and declarations, never logic

The moment a pack contains a conditional it is a plugin, the review burden
becomes unbounded, and [ADR-012](0012-compiled-connector-discovery.md)'s reason
for compiled discovery applies to it too. Vehicle-specific arithmetic is
expressible as **flags and numbers the deal engine reads** —
`tradeInReducesBasis: false`, `docFeeTaxable: true`, `docFeeCap: 85.00` — with
the engine's behaviour in our code and under our tests.

### R2 — Everything is effective-dated, and rows are never updated in place

A rate change is a **new row with a new effective date**, so a deal written in
March still reads as March in an audit next year. This is
[ADR-016](0016-immutable-business-ledgers.md) applied to reference data, and it
is why "just update the rate table" is the failure mode to design against.

### R3 — A deal stores the tax it charged, not a pointer to recompute from

Tax lines are **evidence, frozen at the moment of sale**, carrying the amount,
the basis, the rate, the jurisdiction, the pack and pack version that produced
them, and the address they were resolved from. Nothing recomputes them on read.
A customer who moves house does not retroactively change a sale.

### R4 — Provenance is mandatory, and "a person typed it" is a valid value

Every tax line records how it was produced: `pack` (with id and version),
`vendor` (with the call's evidence, under [ADR-007](0007-integration-platform-edge.md)),
or `entered-by-person`. There is no unlabelled number.

### R5 — An unsupported jurisdiction is not an error, it is manual entry with a label

A deal in a jurisdiction we have no pack for **is not blocked**. A person enters
the tax, the provenance says so, and the screen says so.
[Doc 09](../09-Implementation-Roadmap.md) already requires that unsupported
legal behaviour be clearly labelled; this is that requirement given a mechanism.
It is also what makes the whole design shippable before any pack exists.

### The resolution order, in one list

1. The **registration address recorded on the deal** — not the customer's
   current address, and not the rooftop's.
2. If the enabled pack's scheme is **SST**: ZIP+4 against that state's published
   boundary file, then its rate file. No geocoding, and hold-harmless applies.
3. Otherwise the pack declares its resolver. A US non-SST pack can use the
   **Census Bureau geocoder** — free, no key, address to state/county/tract
   FIPS, batchable — to obtain the county the address is actually in. A vendor
   pack calls the vendor.
4. If nothing resolves: **R5**. A person enters it.
5. **Never** IP geolocation, browser locale, `Accept-Language`, or the tenant's
   default currency. None of them is the buyer's registration address, and three
   of them are attacker-controlled.

### The shape in the database

Reference data, in the **host catalog** rather than per tenant — a rate table is
not a dealership's business data, and 23 states' quarterly files copied into
every tenant database is the same mistake as ZIP codes, one level up:

- `JurisdictionPacks` — pack id, country, scheme (`Manual` / `SST` / `Vendor` /
  `EuVat`), version, `EffectiveFrom`/`EffectiveTo`, source URI, fetched-at,
  content hash, reviewed-by, reviewed-at, status (`Draft` / `Supported` /
  `Withdrawn`).
- `JurisdictionRates` — pack, jurisdiction code, postal key, rate kind, rate,
  effective dates.
- `JurisdictionRules` — pack, rule key, value, effective dates. Flags and
  numbers only (R1).

Per tenant, alongside the deal:

- `DealTaxLines` — append-only, one row per charged tax, carrying description,
  basis, rate, amount, jurisdiction code, pack id, pack version, the resolved
  address, provenance (R4) and resolved-at. Written once with the deal and never
  updated (R3), which puts it under `IAppendOnly` like every other ledger.

And in tenant settings, the posture: enabled pack ids, retention periods, and
the switches the dealership owns.

### What this decides about D2 itself

- **Compliance depth: the United States first.** Not because it is the better
  market, but because its requirements are already scoped in
  [doc 01 §4](../01-Vision-and-Scope.md), and because GLBA plus the Safeguards
  Rule is the **strictest security floor among the candidates** — so building to
  it wastes nothing if the market answer later changes. A weaker floor first
  would have to be raised later, across the whole product.
- **The architecture stays country-neutral**, because the three-way split above
  costs nothing to keep neutral and `Address` and `Money` are already shaped for
  it.
- **The first pack is `Manual`** — the one that needs no rate table at all and
  makes R5 real. **The second is SST-23**, free, quarterly, hold-harmless, and
  notably requiring no choice of individual state.
- **What stays blocked on a market decision is titling and registration
  (EVR/ERT)**, which needs an approved Service Provider contract per state and
  therefore a state list. That is [doc 11 §3.3](../11-Franchise-and-External-Scope.md)
  and it remains correctly deferred.

## Alternatives considered

- **Pick one state, hard-code it, generalise later.** Rejected. The generalising
  never happens on a compliance surface; the state's assumptions get spread
  across the deal engine, the documents, and the reports, and the second state
  is then a rewrite. The three-way split costs one table and one interface now.
- **A general rules/policy engine evaluating declarative predicates.** Rejected,
  and this is the alternative the question actually proposed. It fails on
  privacy for a reason no amount of engine quality fixes: the GLBA exemption is
  per-field, per-state, per-entity, and encoding it makes the software issue a
  legal opinion. It fails on tax for a duller reason: the hard part is sourcing
  and versioning 13,000 jurisdictions' data, which an engine does not help with
  at all. An engine would let us *express* rules we have no way to *source*.
- **Buy a general tax API (Avalara, TaxJar) and be done.** Rejected as the whole
  answer, kept as the `Vendor` scheme. General engines do not model the trade-in
  credit, doc-fee caps, or lease basis — the automotive-specific parts — and a
  wrong tax with a vendor's name on it is still the dealer's liability. The
  vendor supplies rates; the basis stays ours.
- **Geo-IP or browser locale to detect jurisdiction.** Rejected outright, and
  recorded here so it is not proposed again. It answers a different question
  from the one the law asks.
- **Recompute tax on read from the current rate table.** Rejected: it makes
  history mutable, which contradicts ADR-016 and would silently rewrite closed
  accounting periods every quarter.
- **Per-tenant copies of the rate data.** Rejected: quarterly files for 23
  states duplicated per dealership, with 23 chances to be a version behind.
  Reference data is host-catalog data; only the *enablement* is per tenant.
- **EU or GCC first, given six languages and RTL already ship.** Rejected, and
  the reasoning behind the tension is corrected in
  [doc 07](../07-Delivery-Roadmap.md): shipping six languages is *done* and
  carries no ongoing compliance cost. A language is not a jurisdiction —
  Spanish is spoken in the United States, and Arabic RTL support is a UI
  capability, not a promise about UAE law. Compliance is the singular thing
  here; the product is not.

## Consequences

- **The tax work can start without the market decision**, which is what D2 was
  blocking. `Manual` plus the basis arithmetic is buildable now and useful now.
- ~~**`customer.address.area` has to split.**~~ **Done 2026-09-05.** The gap
  ADR-023 found by measuring against STAR was on the critical path rather than a
  note: state and county are different jurisdictions and the county drives the
  rate. `customer.address.county` now exists alongside it, through the domain,
  the API, both CSV directions and the connector contract. It needed **no**
  version bump — doc 05 §2 makes an added optional field compatible, and the
  claim in `ContractFields`'s header that any added field forces a bump was
  wrong and has been corrected.
- **A garaging or registration address becomes a real field**, distinct from the
  customer's mailing address — the second gap ADR-023 named. Not built, and it
  belongs on the **deal** rather than the customer: it is the address the tax
  was resolved from, frozen with the sale by R3 above.
- **A pack needs an owner and a review date, or it rots**, and a rotted pack is
  worse than none because it is trusted. `Supported` status is a claim about a
  human review, not about the file parsing.
- **We say plainly what we do not support.** For any jurisdiction without a
  Supported pack the screen shows a person-entered figure and says so. That is a
  product statement, and a better one than a confident wrong number.
- **The Safeguards baseline is now a product requirement**, not an optional
  posture: MFA, encryption at rest and in transit, and the audit trail cannot be
  configured away by a deployment.
- **This ADR does not decide D5.** Whether credit data is held at all is a
  posture switch here, and the decision about it stays open.

## Validation / review trigger

Revisit if any of these change:

- A state withdraws from Streamlined Sales Tax, or the hold-harmless provision
  changes — it is a third of the design's leverage.
- A manufacturer certification programme mandates a specific tax provider, which
  would make `Vendor` the primary scheme rather than one of four.
- The first non-US pack is written and the `Manual`/`SST`/`Vendor`/`EuVat`
  scheme set turns out not to cover it — a fifth scheme is fine; a pack needing
  *logic* is the signal that R1 was wrong.
- A pack is found to have been marked `Supported` without a recorded human
  review, which would mean the status means nothing.

## Sources

Read 2026-09-05.

- Streamlined Sales Tax — [About Streamlined FAQs](https://www.streamlinedsalestax.org/Shared-Pages/faqs/faqs---about-streamlined)
  (member states, hold-harmless) and [Rate and Boundary Files](https://www.streamlinedsalestax.org/Shared-Pages/rate-and-boundary-files)
  (quarterly cadence, posted by the first day of the month before each quarter).
- Avalara — [ZIP Codes: the wrong tool for determining tax rates](https://www.avalara.com/us/en/learn/whitepapers/zip-codes-the-wrong-tool-for-the-job.html).
- California CDTFA — [Tax Guide for Motor Vehicle Dealers](https://cdtfa.ca.gov/industry/motor-vehicle-dealers/industry-topics.htm)
  (trade-in allowance is not deducted from the taxable amount).
- Miller Martin — [Steering Through Privacy: State Law Variation Affecting Auto Dealers](https://millermartin.com/in-depth/alerts/steering-through-privacy-state-law-variation-affecting-auto-dealers/)
  and Orrick — [Where is the GLBA Entity-Level Exemption?](https://www.orrick.com/en/Insights/2025/07/Where-is-the-GLBA-Entity-Level-Exemption-Two-More-State-Privacy-Laws)
  (entity-level versus data-level exemptions).
- Fisher Phillips — [Steering Your Auto Dealership into Compliance with New Information Security Rules](https://www.fisherphillips.com/en/news-insights/auto-dealership-compliance-new-information-security-rules.html)
  (Safeguards Rule obligations for dealers).
- GDPR — [Recital 78](https://gdpr-info.eu/recitals/no-78/) (producers are
  encouraged; the obligation is the controller's).
- Irish Revenue — [Margin scheme](https://www.revenue.ie/en/vat/vat-on-goods/schemes/margin-scheme/index.aspx)
  (VAT on the margin for second-hand vehicles).
- US Census Bureau — [Census Geocoder](https://geocoding.geo.census.gov/)
  (free address to county FIPS, no key, batch mode).

**These are engineering sources for an engineering decision.** Nothing here is
legal advice, and a pack is not `Supported` until a person qualified to say so
has reviewed it ([doc 06 §5](../06-Security-and-API.md)).
