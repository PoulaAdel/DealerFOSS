# ADR-0023 — STAR is a wire format to translate from, not our internal vocabulary

Date: 2026-09-04
Status: Accepted
Supersedes: —

Settles open decision **D3** in [doc 11 §11](../11-Franchise-and-External-Scope.md).

## Context

[STAR](https://www.starstandard.org/) — Standards for Technology in Automotive
Retail — is the body that defines how dealers, manufacturers and their vendors
exchange data. Its standards are **Business Object Documents (BODs)**: XML
messages built on the Open Applications Group methodology and UN/CEFACT Core
Components, where a BOD is an *ApplicationArea* (Sender, CreationDateTime,
BODID, Signature, Destination) plus a *DataArea* carrying a Verb and a Noun.
STAR publishes **over 200 message formats across more than 35 business areas**.

`src/App/Integrations/ContractFields.cs` declares our own vocabulary —
`customer.firstName`, `customer.address.postalCode` and nine more — so that a
connector translates a provider's names into ours exactly once at the edge, and
every sink reads one set of names. Doc 11 §2 recorded the discomfort: **the seam
is right, and the vocabulary is invented when a standard one exists.**

### What the research changed

Doc 11 §2 assumed adopting STAR meant "reading the specification", implying that
was the obstacle. **It is not.** The STAR 5 repository documentation is published
openly at `docs.starstandard.org` under the **Eclipse Public License v1.0**, with
no membership or login — STAR 5 and STAR 6 guidelines, OpenAPI definitions and
short codes. The vocabulary was readable all along.

So this decision could not be deferred on grounds of access. It had to be made on
the merits.

### What STAR's vocabulary actually looks like

From the address structure in STAR 5.13.4's `CustomerInformation` BOD, read
directly rather than summarised:

```
LineOne [0..1] … LineFive [0..1]           — a CHOICE against the structured form
  or BuildingNumber / BuildingName / StreetName / FloorIdentification /
     PostOfficeBox / BuildingNumberSuffix
CityName [0..1]        CitySub-DivisionName [0..*]
Postcode [0..1]        CountryID [0..1]
StateOrProvinceCountrySub-DivisionID [0..1]
CountyCountrySub-Division [0..1]
AddressID [0..1]  AddressType [0..1]  UseCode [0..1]  Privacy [0..*]
```

## Decision

**Keep our own contract vocabulary. Do not rename `ContractFields` to STAR
element names.** Treat STAR as two things instead:

1. **A wire format a future connector translates from**, exactly as any other
   provider's format is translated — which is what the seam already exists for.
2. **The reference model our contract is measured against.** When a contract is
   extended, check it against the corresponding STAR noun first and record what
   we deliberately do not carry.

### Why not adopt the names

**STAR is a wire format, not an internal vocabulary.** Renaming our keys to STAR
element names would put STAR-*looking* names on a structure that is not STAR.
That is worse than an honest local name, because it implies an interoperability
that does not exist — a reader would reasonably assume a STAR message could be
handed to us, and it could not.

**The flat dictionary structurally cannot carry STAR.** `ProviderRecord.Fields`
is `IReadOnlyDictionary<string, string?>`. STAR's address alone needs a *choice*
between five free-text lines and a structured form, and `[0..*]` repetition on
`AttentionOf`, `Privacy` and `CitySub-DivisionName`. A flat key/value map loses
both the choice and the cardinality. Adopting the names without the shape keeps
none of the value and all of the obligation.

**Certification is about messages, not names.** Where a manufacturer certifies a
DMS integration against STAR, what is certified is the exchange of actual BODs
over actual transport. Naming our internal constants after STAR elements gets us
no closer to passing that, and might persuade somebody it did.

**The cost of deferring is near zero and stays low.** There is one sink
(`CustomerRecordSink`) and one connector (`Fixture`, a deliberate test double).
If a real STAR connector arrives it translates at the edge, which is one file.

## Alternatives considered

- **Rename `ContractFields` to STAR element names.** Rejected for the four
  reasons above. It is the change that looks like progress and buys nothing.
- **Flatten STAR BOD paths into the dictionary keys** (`CustomerInformation.CustomerParty.BillingAddress.LineOne`).
  Rejected: it makes the internal vocabulary hostage to a wire format we do not
  speak, and imports STAR's ambiguity — five address lines *or* a structured
  form — into every sink, which would then have to handle both.
- **Adopt STAR's JSON/OpenAPI definitions instead of the XML BODs.** Rejected
  *for now* and worth revisiting: it is the closest fit to how this product
  actually moves data, and STAR publishes OpenAPI definitions openly. Revisit
  when there is a real provider to test against — the same standard applied to
  OIDC federation, and for the same reason.

## Consequences

- `ContractFields` keeps lowerCamelCase dotted names. The file now says why, so
  this is not re-opened every time somebody meets STAR.
- **STAR becomes a coverage checklist.** Comparing our eleven fields against
  STAR's address ABIE immediately found three things we do not carry, recorded
  here rather than fixed, because adding a field nothing populates is
  speculation:
  - ~~**County is collapsed.**~~ **Closed 2026-09-05.** We had one
    `customer.address.area`; STAR separates
    `StateOrProvinceCountrySub-DivisionID` from `CountyCountrySub-Division`.
    This mattered commercially — doc 11 §3.3 records that US sales tax varies by
    **state, county and sometimes city**, so our address could not express the
    thing that drives the tax calculation. `customer.address.county` now exists
    alongside `customer.address.area`, as an optional field, which doc 05 §2
    makes a compatible change needing no version bump.
    [ADR-024](0024-compliance-is-baseline-pack-and-posture.md) is what moved this
    from "recorded" to "on the critical path": the county is the jurisdiction
    that decides the rate. Neither field is derivable from the other, and nothing
    infers one from the other — a guessed county is a wrong tax rate that looks
    exactly like a right one.
  - **No address type or use.** STAR carries `AddressType` and `UseCode`; we
    cannot distinguish a billing address from a residence or a garaging address,
    and garaging address drives insurance and some tax. **Still open, and still
    deliberately** (reviewed 2026-09-05): a customer holds one address, and a
    type only discriminates between several. The registration address that
    decides a deal's tax is a fact about the *deal*, frozen with it
    ([ADR-024](0024-compliance-is-baseline-pack-and-posture.md) R3), so it lands
    there rather than as a second customer field nothing would populate.
  - **Two address lines, not five.** Rarely a problem, and named so it is a known
    limit rather than a surprise.
- A future STAR connector is an ordinary connector. It declares its capabilities
  in its manifest, states its STAR release in `Provider`/`Version`, and lists
  what it cannot map in `KnownLimitations`.

## Validation / review trigger

Revisit when **either** of these becomes true:

- A real STAR-speaking provider is available to test against — at which point
  the OpenAPI alternative above deserves a fresh look, because it may be a
  better fit than the XML BODs.
- A second sink is written and the two disagree about what a field means. That
  is the signal the local vocabulary has stopped paying for itself.

Sources, read 2026-09-04: [The Standards](https://www.starstandard.org/index.php/the-standards/) ·
[STAR XML BODs](https://www.starstandard.org/index.php/star-xml-bods/) ·
[STAR 5.13.4 CustomerInformation, BillingAddress](https://docs.starstandard.org/guidelines/STAR5134/CustomerInformation/ch06s58.html) ·
[docs.starstandard.org](https://docs.starstandard.org/)
