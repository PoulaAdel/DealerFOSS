# 11 — Field lessons from working DMS integrations

← [Integration Framework](05-Integration-Framework.md)

## 1. What this document is

Document 05 says how DealerFOSS *intends* to integrate. This one is written from
the opposite direction: four DMS integration codebases that actually ran against
live dealership data were read end to end, and what follows is what those systems
were forced to do by reality rather than by design.

They are private, third-party work and are not part of this repository. No code,
no configuration, no client name and no dealership record from them appears here
or anywhere in this project. What is recorded is the *shape of the problem* —
which is not anyone's intellectual property, and most of which is publicly
documented by the providers themselves. Sources are described only as "the
reviewed systems".

Between them the reviewed systems talked to CDK (both the older SOAP extract
service and the newer Fortellis REST platform), CDK's Elead CRM, Reynolds &
Reynolds, Tekion, DealerBuilt, and PBS — by REST, SOAP with WS-Security, SFTP
file drops, and a push-delivery protocol where the DMS initiates and the
integration listens.

Each lesson below closes with a verdict against our current design:
**Covered** (05 already says this), **Partly** (05 gestures at it and would not
survive contact), or **Missing**. The verdicts matter more than the lessons:
they are the reason to read this before `src/App/Integrations/` exists rather
than after.

Nothing in this document has been implemented. It is evidence for decisions, not
a record of them.

## 2. Transport

### 2.1 The long operation is the normal path, not the exception

Every REST provider reviewed answers a bulk request with `202 Accepted`, a status
link, and a "check back in N seconds" hint. You poll the status link until it
says complete, then follow a *second* link to the actual result. All three
codebases that speak this protocol implemented it independently, and all three
capped polling at five attempts.

Five attempts is not a duration. A provider that says "check back in 600 seconds"
gets fifty minutes; one that says "check back in 10" gets fifty seconds and then
a `TimeoutException` that is entirely the client's invention. Two of the three
also reused the same attempt counter for HTTP retry and for polling, so a slow
job and a flapping server exhaust the same budget.

**What we should do.** A poll budget is a wall-clock deadline, configured per
provider and capability, and it is a different setting from the retry count.
Exhausting it is a *reportable operational state* ("still running at the
provider") — not an error, because the job usually is still running and asking
again later is the correct next move.

**Verdict: Missing.** 05 §4 configures "timeouts, backoff, and circuit-breaker
thresholds per provider" but treats an async job as one long request.

### 2.2 One endpoint, two envelopes

The same repair-order feed returns a bare JSON array from its delta endpoint and
`{ "data": [ … ] }` from its bulk and history endpoints. The working parser
accepts both and fails loudly on a third shape. Error bodies from one provider
arrive as `{ "error": { "Code", "Message" } }` *and* as flat `{ "Code",
"Message" }`, and the reviewed code tries both before giving up.

The instinct to correct here is the assumption that a provider has *an* envelope.
It has one per endpoint, and sometimes one per mood.

**What we should do.** Envelope detection belongs in the connector, is explicit
about which shapes it accepts, and an unrecognised envelope quarantines the
*page* rather than throwing away the run.

**Verdict: Partly.** 05 §2 quarantines unknown enumerated codes. An unknown
envelope is a different failure at a different layer and has no stated handling.

### 2.3 A cursor is not a date

The window arithmetic the reviewed systems were forced into, for a single
provider:

- the delta endpoint takes **no date parameters at all** — "recent" is whatever
  the server decides it is;
- history must be requested in chunks of no more than 183 days;
- bulk covers only up to roughly the last month, so history and bulk have to be
  stitched with an overlap in the middle;
- the CRM's delta search refuses any range older than 7 days.

One codebase defines "recent starts two days ago" purely to line up with that
bulk/delta seam. Another silently clamps a requested start date forward to five
days ago when the endpoint will not accept more. Both are correct; both are
invisible to the caller.

**What we should do.** Window arithmetic is connector data, not connector code:
maximum chunk, maximum lookback, whether the endpoint accepts dates at all, and
where two strategies must overlap. The runtime must never assume the range it
asked for is the range it received — the connector reports the range actually
covered, and the cursor advances from *that*.

**Verdict: Missing**, and this is the one most likely to cause silent data loss.
A cursor advanced to a date the provider never actually served leaves a hole
nobody notices until a reconciliation months later.

### 2.4 Overlap is deliberate, and lateness is per-dealer

The reviewed pull schedules run daily but ask for five days, and one carries a
per-store `DelayDays` that shifts the whole window backwards because some
dealerships post their paperwork late and a same-day pull gets an empty answer.
Overlap plus deduplication is what makes late-arriving data arrive at all.

So a settlement delay is a property of the *dealership*, not only of the
provider — and a system that models it per provider will quietly under-collect
for the slow ones.

**Verdict: Partly.** 05 §4 deduplicates by message ID, which is the half that
makes overlap safe. The half that makes overlap happen — a configured lookback
and settlement delay per connector *and per dealership* — is not there.

### 2.5 Ask the source how much is left

The push-delivery protocol in the oldest reviewed system is the best restart
design of the four. The DMS announces a transaction of N files; the client
downloads, decrypts, decompresses and applies each one, then calls a `delivered`
endpoint — which returns *the number remaining*. The loop ends when the server's
count agrees, not when the client's counter runs out. A comment explains why: a
client that failed partway through and restarted may have fewer left than it
believes.

The client's idea of progress is a guess. The source's is a fact.

**Verdict: Covered in spirit** — 05 §4 advances a cursor only after a full page
commits — **but worth stating in the stronger form**: where a provider can tell
us what remains, that answer outranks our own bookkeeping.

## 3. Data

### 3.1 The row that will not fit

The most valuable single piece of code in all four systems: before a batch is
written, every value is checked against the destination column's real type and
length. Over-long strings are truncated, out-of-range numbers and impossible
dates are replaced, and every correction is accumulated per column and logged
once per commit rather than once per row.

This exists because a DMS will send a 300-character address into a
`VARCHAR(100)`, a year of `0`, and a date in 1899 — and without it, one bad field
fails a ten-thousand-row batch and the dealership gets nothing that night.

The pattern is right. **Its substitutions are wrong, and we must not copy them.**
An out-of-range amount becomes `0`. An impossible date becomes a fixed sentinel
date. Both then look exactly like real values to every downstream consumer — a
zero-dollar sale is a plausible sale, and a whole dealership's worth of vehicles
sharing one delivery date is a report nobody questions until it is a legal
problem.

**What we should do.** Keep the coercion pass; invert its output. A value that
does not fit becomes **absent**, with the raw text preserved in the record's
mapping warnings. Absent is honest and visibly missing. A substituted number is
a lie with good posture.

**Verdict: Partly.** 05 §2 puts `MappingWarnings` on every message, which is the
right container. Nothing yet says a coerced value must be distinguishable from a
real one, and that is the entire difference between this pattern helping and
harming.

### 3.2 Parallel arrays must never be joined by position

Repair-order totals arrive as one array of amounts and a separate array of pay
types, aligned by index. Labour lines arrive the same way: operation codes in one
array, descriptions and hours in others.

The reviewed code has fields commented out with a note that they caused "op code
mismatch" — the arrays did not stay aligned, so the integration now writes the
operation code and deliberately discards the description and the hours that were
supposed to go with it. That is data lost permanently, in production, because two
lists were assumed to be the same length.

**What we should do.** Position is not a join key. Either the provider gives a
key on both sides, or the dependent fields are dropped explicitly with a mapping
warning — never carried on the assumption that the arrays line up.

**Verdict: Missing.**

### 3.3 Numbered slots, not collections

DMS deal records do not have a list of fees. They have `AnnualFee1Amount`
through `AnnualFee5Amount`, `Insurance1` through `Insurance3`, and
`AddToCapCostFee5` through `7` — fixed arity, baked into the record layout,
sometimes with gaps where a slot was retired.

Reading them into a proper collection is straightforward and obviously right.
Writing back is where it bites: a deal with six fees cannot be pushed into five
slots, and the correct behaviour is an explicit refusal the user can see, not a
silent drop of the sixth.

**Verdict: Missing.** 05 §5 has Pending / Confirmed / Rejected / Attention
Required for outbound, which is the right vocabulary — but "the provider's record
has no room for this" is a rejection reason we have not anticipated, and it is
not a transient one to retry.

### 3.4 A person's name is not a structured field

The reviewed code splits a single name string by suffix regex (`JR|SR|II|III|IV`),
takes the last word as the surname and the middle words as a middle name. A
comment next to a second implementation records the bug that forced it: the
first name field was picking up the middle name too, so it now splits again at
the space.

This is lossy, it is wrong for a meaningful share of real people, and it is
wrong differently in every locale. It is also unavoidable when the provider
hands over one string.

**What we should do.** Keep the raw string next to any parse we perform, treat
the parse as a *suggestion*, and — critically — never let a later re-parse
overwrite a name a person has since corrected locally. That is exactly the field
ownership rule in 05 §4, applied to the one field where it is most likely to be
forgotten.

**Verdict: Partly.** The rule exists; the case that breaks it is not named.

## 4. Operations

### 4.1 Write it partial, rename it on success

One system writes every export to a `.partial` file and renames it only after
the last row, deleting the partial if anything throws. A crash therefore leaves
either a complete file or no file — never a truncated one that a downstream job
will happily read as complete.

**Verdict: Covered inbound** (05 §4 stores the envelope before acknowledging),
**missing outbound**: 05 §6 describes exports and reconciliation reports without
saying they are atomically published.

### 4.2 The raw capture is not optional, and neither is its expiry

All four systems save every request and response to disk, keyed by timestamp.
This is not debug cruft. It is the only artefact that settles "your API sent us
this" with a provider, and every one of these teams arrived at it independently.

It is also a shadow copy of customer names, addresses, phone numbers, emails and
VINs, sitting in a folder. One codebase documents its retention in a comment:
the filenames carry the date, and "external cleanup handles the 60-day policy" —
which is to say nobody does, and the folder is now however old the installation
is. Another masks the `Authorization` header when writing the capture but stores
the full response body, which is where all the personal data actually is.

**What we should do.** Keep the capture — an integration that cannot prove what
it received cannot be operated. Then make retention a property of the code with
a default that applies when nobody configures anything, keep it inside the
tenant's storage boundary, and treat it as personal data for every purpose
including deletion requests.

**Verdict: Missing.** 05 §8 says credentials are redacted from logs and does not
mention raw capture at all. Redacting the header while storing the body is
precisely the mistake §8 currently permits.

### 4.3 One dealership's failure must not end the run — and must outlive it

Both orchestrators wrap each dealership in a try/catch and continue, collecting
successes and failures into a summary. That is right, and we should copy it
exactly.

What neither does is *persist* the outcome. The summary is a string returned to
whoever triggered the run. "Has this dealership been failing all week?" is
unanswerable, so a store that silently produces zero rows every night can do so
for a long time.

**What we should do.** Per-connector, per-dealership, per-run status is a table:
started, finished, records applied, records quarantined, and the failure if
there was one. Metrics show the shape; the table answers the question.

**Verdict: Missing.** 05 §8 lists metrics, which are aggregates and expire.

### 4.4 Configuration grows a language nobody documents

Per-dealership settings in the reviewed systems live in a spreadsheet, and the
columns have accreted grammar. One field means "pull both feeds" as `Y`, `Yes`,
`B` or `Both` — or `E5T10` for "enrolments five days back, transactions ten days
back", parsed with a regular expression. Another packs a subscription ID, two
department IDs and an environment flag into one semicolon-delimited string,
where appending `test` switches the whole connector to the sandbox.

The failure this produces is worse than the ugliness. One field is parsed with
`int.Parse` and no default, so a dealership with that column left blank throws —
and because the loop catches per store and continues, **that dealership is
skipped entirely, every night, silently.** A comment in the source says so
outright.

**What we should do.** Connector settings are typed fields declared in the
manifest, validated when saved, not a delimited string parsed at run time. A
setting that fails validation blocks the *save*; a setting that is missing at
run time fails that dealership *loudly*, with the connector marked broken in the
UI rather than quietly producing nothing.

**Verdict: Partly.** 05 §3 has a manifest describing capabilities. It does not
say the manifest also declares and validates the per-dealership settings.

### 4.5 Putting a synchronous face on an asynchronous provider

Where the DMS pushes rather than answers, one system fakes request/response: the
HTTP handler generates a correlation ID, subscribes to a Redis channel keyed on
it, triggers the push, and waits — with a short-lived key as a fallback for the
race where the answer arrives before the subscription is live, and a timeout that
re-checks that key before giving up.

The mechanism is sound and the race handling is the part most people miss. But
ADR-005 makes Redis optional here, so if we ever need this shape, the correlation
store needs a single-node fallback and the fallback needs the same race handling
— which is where it will be got wrong.

**Verdict: Partly**, and it is an [ADR-005](adr/0005-redis-optional-single-node.md)
question before it is an integration one.

## 5. What must not be carried over

These are recorded because they are the normal state of shipped integration code,
not because the reviewed systems are unusual. Several would be caught by our
existing rules; the point of listing them is that they survived in production for
years, so "we would obviously not do that" is not evidence.

| Pattern found | Why it is wrong | Our position |
|---|---|---|
| Provider credentials in a settings file beside the code | One clone or one screen-share leaks every dealership | Credentials encrypted per organisation, never in a settings file ([06](06-Security-and-API.md)) |
| Admin secret used as a **URL path segment** to gate an endpoint | Lands in proxy logs, browser history, and `Referer` headers | Never place a secret in a URL. Authorisation is a header and a session |
| Shared secret compared with `!=` | Not constant-time, and the comparison is a byte-at-a-time oracle | Fixed-time comparison for every secret |
| The `Authorization` header written verbatim into the debug log | The credential outlives the request, in plain text | Redact at the sink, not at each call site |
| MD5 for password storage, and for a security nonce | Broken for the first; wrong primitive for the second | Handled — password hashing lives in `src/Identity` and nowhere else |
| A fixed AES key and IV compiled into the source, encrypting the log files | Obfuscation presented as confidentiality; one static IV across every record | Real key management, or plaintext honestly labelled — not the middle option |
| WS-Security `PasswordText`: the password in cleartext inside the SOAP body | Safe only while TLS holds, and the body gets saved to disk | If a provider requires it, the capture must redact it before the file is written — the reviewed code does exactly this, and it is the right instinct |
| A global TLS protocol setting mutated per request | Process-wide state changed by one connector, affecting all of them | Per-connector HTTP configuration, no shared mutable transport state |
| JSON-RPC dispatch by reflection onto any public method of the handler | The wire format decides which code runs; adding a public helper adds an endpoint | Explicit route registration only. Never dispatch by name from input |

The last one deserves emphasis: it is not a hardening detail, it is the whole
authorisation boundary. Every public method on that class was callable by anyone
who had the shared secret, including methods added later by someone who had no
idea they were publishing an API.

**As of this document, `src/App` contains no integration code and none of these
patterns.** That is the reason to write this now: the cheapest moment to decide
against a mistake is before the folder exists.

## 6. What changes as a result

Nothing yet. This document is evidence; the decisions belong in ADRs and in
document 05, and each of these is a separate change with its own reasoning:

1. **Amend 05 §4** — window arithmetic as connector data; the range actually
   covered, not the range requested, advances the cursor. *(§2.3, the highest
   risk of silent loss.)*
2. **Amend 05 §2** — a coerced value is absent plus a mapping warning, never a
   substituted number or sentinel date. *(§3.1.)*
3. **Amend 05 §8** — raw request/response capture is a named, required facility
   with a default retention, held as personal data. *(§4.2.)*
4. **New: durable per-connector, per-dealership run history.** *(§4.3.)*
5. **Amend 05 §3** — the manifest declares and validates per-dealership settings;
   no delimited configuration strings. *(§4.4.)*
6. **Amend 05 §4/§5** — separate poll deadline from retry budget; add "the
   provider's record has no room" as a non-transient outbound rejection.
   *(§2.1, §3.3.)*

## 7. What this review did not do

The four codebases were read for structure, protocol handling and failure
behaviour. Credential values were **not** read: configuration files were examined
for key *names* only, so that this document could say which secrets exist without
learning any of them. Dealership data files present in those folders were
identified by name and size and left unopened.

No file in any of the four was modified, and nothing from them has been copied
into this repository.
