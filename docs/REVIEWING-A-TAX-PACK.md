# Reviewing a tax pack

← [Workbook](00-Workbook.md) · [Onboarding](ONBOARDING.md)

**For a tax professional, not a programmer.** DealerFOSS works sales tax out on a
vehicle deal from a *pack*: a small file holding one jurisdiction's published
rates and a few decisions about what those rates apply to. A pack charges nobody
anything until somebody who knows the jurisdiction has read its source and said
so in public. This page is how that review works, what you are and are not
signing, and how to propose a pack for a place we do not cover.

**Nothing here is tax advice, and the project gives none.** We are asking you to
check that a file matches what the authority publishes — not to take
responsibility for anyone's return.

---

## 1. What a pack is, and why it arrives unusable

A pack is a text file. It carries:

- **Rates** — a percentage, which authority levies it (state, county, city or a
  special district), the addresses it applies to, and the dates it is in force.
- **Three flags** about what the rate is charged *on*, covered in §2.
- **Its own provenance** — the official page the figures came from, and the date
  somebody actually read that page.

It carries no logic. There is no formula, no condition and no script in a pack;
the arithmetic lives in the software, under test, and a pack only supplies
numbers and flags. That is deliberate — see
[ADR-024](adr/0024-compliance-is-baseline-pack-and-posture.md) — and it is what
makes a pack something a non-programmer can read and check.

**Every pack starts in the state `Draft`, and a Draft pack works nothing out.**
A pack that ships with the product is in exactly the same position as one a
stranger writes: held, hashed, readable, and ignored by the tax calculation. It
becomes usable only when a named person records that they have reviewed it.

The pack for Indiana that ships today is a worked example of why. Its rate is
real and sourced — 7 percent, statewide, read from the Indiana Department of
Revenue — but **its three flags are deliberately blank**, because nobody has yet
read Indiana's own bulletin on what a vehicle sale is taxed on. The software
refuses to let anyone mark it usable until those three are filled in. Shipping
it with plausible-looking defaults was considered and rejected: a reviewer who
never opened the source would then have been approving somebody's guess.

---

## 2. The review checklist

Work through these five. If any of them cannot be answered from an official
publication, say so rather than inferring — an unanswerable question is a useful
result and §5 says what to do with it.

### 2.1 The rate, against the official source

For each rate in the pack, check the figure against the authority's own
publication, and note **which page and which date** you read. A secondary source
is not enough; the point of the exercise is that somebody can go back to the
original later and see the same number.

Watch for the two traps this project has already been bitten by:

- **A rate written as a percentage instead of a fraction.** In the file, seven
  percent is `0.07`, not `7`. The loader refuses anything at or above `1` for
  this reason, but a rate of `0.7` would pass and be ten times too much.
- **Local taxes on top of the state's.** If the jurisdiction lets counties or
  cities levy their own rate on a vehicle, the pack needs a row for each, and a
  pack with only the state rate will undercharge every deal in those places. Say
  so plainly if the jurisdiction is one of these and the pack does not cover it.

### 2.2 The three flags: what the rate is charged on

These are the heart of the review, and the part a general-purpose tax engine
usually gets wrong. Each is simply true or false.

| Flag in the file | The question it answers |
|---|---|
| `tradeInReducesBasis` | Does the trade-in allowance come off the amount that is taxed? |
| `documentationFeeIsTaxable` | Is the dealer's documentation fee part of the taxed amount? |
| `otherFeesAreTaxable` | Are registration, title and delivery fees part of the taxed amount? |

The first one matters most. Most US states tax the price *less* the trade-in
allowance; **California does not**, and taxes the full price. On a $30,000 car
against a $10,000 trade, getting this wrong is the tax on ten thousand dollars,
in one direction, on every deal.

The second and third are separate on purpose. A documentation fee is the
dealer's own charge and is part of the taxable price in most places;
registration and title fees are collected on somebody else's behalf and usually
are not. "Usually" is why they are flags and not assumptions.

### 2.3 Effective dates

Every pack and every rate carries a start date and optionally an end date. Check
that the dates match when the authority says the figures apply.

A pack is never edited after it is loaded. **A rate change is a new version of
the pack**, which is new rows, so a deal taxed last March still reads as March
in an audit next year. If you find an out-of-date rate, the answer is a new
version, not a correction to the old one.

### 2.4 Addresses the rates apply to

Tax follows **where the buyer will register the vehicle**, not where the
dealership is and not where anyone happens to be. Check that each rate's
matchers — state or region, county, postcode — describe the right area, and in
particular that a county rate names its county. A state-only matcher on a county
rate would charge every address in the state.

### 2.5 Anything the flags cannot express

This is the question we most want you to answer, because it is the one that
tells us what to build next.

**Worked example: Michigan.** Michigan does not simply allow or disallow the
trade-in credit — it **caps** it. `tradeInReducesBasis` is true or false and has
nowhere to put a cap, so Michigan is a jurisdiction a pack cannot honestly
describe today, and we say so rather than approximating it. The same is true of
a tiered rate, a rate that changes above a price threshold, and of whether a
service contract sold with the car is taxable.

**If your jurisdiction needs something these flags cannot say, stop and write it
down instead of choosing the nearest flag.** That is a finding, not a failure,
and it is recorded as open work rather than guessed at. A pack that looks
complete and is subtly wrong is worse than no pack, because it is trusted.

---

## 3. How a review actually gets recorded

This section is deliberately blunt about who signs what, because it is easy to
describe more than the software can support.

### 3.1 You do not have an account, and that is fine

DealerFOSS is self-hosted: every dealership runs its own installation. An
outside reviewer has no account on anyone's installation, and the software has
no field a caller can put a reviewer's name into. **The only name the database
ever stores is the email of an administrator on the installation doing the
review**, taken from their signed-in session, never from the request.

So your review travels a different way, in two halves.

### 3.2 Half one — your attestation, in public, under your own name

You review in a public issue, using the
[tax pack review template](../.github/ISSUE_TEMPLATE/tax-pack-review.md). It asks
for the jurisdiction, the official source links, the three flags, anything the
flags cannot express, and the date you checked. That issue is the record of your
judgement, it carries your name and your own account, and it stays readable by
anyone afterwards.

The pack file then **quotes your attestation and links that issue in its own
comment header**. A pack file may carry comments, and this is what they are for.

### 3.3 Half two — an administrator accepts that exact file

Someone running an installation loads the pack and marks it reviewed, on the
**Tax rate tables** screen in the administration console. At that moment the
software records three things: which administrator, when, and the **content
hash** of the file — a fingerprint of its exact bytes.

The hash is what binds the two halves together. It is taken over the whole file
**including the comments**, so if anybody later edits your name, your issue
link, or any rate you signed off, the hash changes and the administrator's
acceptance visibly no longer covers that file.

**What the administrator is recording is therefore narrow and honest: that a
named outside review exists, and that this file is the one it covers.** They are
not claiming to have read Indiana's bulletin themselves.

### 3.4 What is not recorded, today

Your name and your issue link live in the file's comments, where a person can
read them. They are **not** stored as separate fields, so no screen can list
"reviewed by X, see issue 123" and nothing can search on it. That gap is written
down as open work in the scope register rather than quietly ignored; adding
optional reviewer fields to the pack format is a decision about the format and
the review gate, not a small change.

---

## 4. What a sign-off does and does not mean

**It does mean:** you read the authority's publication on the date you recorded,
and the figures and flags in that exact file match what you read.

**It does not mean:**

- **That the project, or you, are giving tax advice.** We state this in the
  ADR's own sources section and we mean it here too. You are checking a file
  against a publication.
- **That you are responsible for deals taxed from it.** The dealership is the
  taxpayer of record and the controller of its own data; the software is
  self-hosted and we are neither.
- **That anybody is now being charged.** Marking a pack reviewed only makes it
  *available*. Each dealership must separately turn it on for itself, which is a
  different act by a different person, and nothing is enabled by default. **Two
  acts, two people** — a reviewer's mistake does not reach a customer until a
  dealership has also chosen that pack.
- **That it is permanent.** A pack can be **withdrawn** at any time. It then
  stops working tax out on new deals and stays readable, because deals already
  taxed from it must still be explainable. Withdrawing does not alter a deal that
  has already been taxed: tax is frozen onto the deal when it is worked out and
  nothing recomputes it later.

---

## 5. Proposing a pack for a new place

Any country. The architecture is not US-specific — a pack can express a single
national rate on a margin just as easily as thirteen thousand local ones.

1. **Open an issue** with the
   [tax pack review template](../.github/ISSUE_TEMPLATE/tax-pack-review.md).
   Fill in the jurisdiction, the official source links, the three flags and the
   date you checked.
2. **Say what the flags cannot express**, if anything. This is the most valuable
   part of the submission and it is better than a confident guess.
3. **Someone writes the pack file** from your issue, quoting your attestation and
   linking the issue in its comment header. You do not have to write JSON unless
   you want to — the issue is the review.
4. **An administrator loads and accepts it** on an installation, as in §3.3.

If you would rather write the file yourself, the shipped Indiana pack at
`src/App/Compliance/Packs/us-in.json` is a commented worked example, and the
rules it has to satisfy are in §2.

---

## For the curious: the API behind the screens

A reviewer never needs these — the administration console does all of it — but
they are listed so nothing above is a black box. All of them require an
administrator session.

| Action | Route |
|---|---|
| List the packs held | `GET /api/v1/admin/jurisdiction-packs` |
| Load a pack file | `POST /api/v1/admin/jurisdiction-packs` |
| Load the packs that ship with the build | `POST /api/v1/admin/jurisdiction-packs/shipped` |
| Mark one reviewed | `POST /api/v1/admin/jurisdiction-packs/{packId}/{version}/review` |
| Withdraw one | `POST /api/v1/admin/jurisdiction-packs/{packId}/{version}/withdraw` |

A dealership's own choice of which packs to use is separate, needs the
`Compliance.Configure` right, and lives at `GET` and `PUT
/api/v1/compliance/posture`.

**Related reading:**
[ADR-024](adr/0024-compliance-is-baseline-pack-and-posture.md) for why compliance
splits into a product baseline, a jurisdiction pack and a deployment's own
posture; [the scope register](11-Franchise-and-External-Scope.md) §12 for what is
open.
