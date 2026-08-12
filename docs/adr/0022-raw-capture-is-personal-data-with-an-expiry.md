# ADR-022 — Raw provider capture is a required facility, and it is personal data

Date: 2026-08-12
Status: Accepted
Supersedes: —

## Context

Every serious integration ends up storing the raw request and response for each
provider call. Not as debugging residue — as the only artefact that can settle
"your API sent us this" with a vendor, and the only way to reconstruct what a
mapping was actually given when a record comes out wrong months later. Teams
arrive at this independently because there is no substitute for it.

The same capture is a second copy of customer names, addresses, phone numbers,
email addresses and VINs, living outside the tenant's tables in a form nobody
models. Two failure modes follow, and both are normal rather than exotic:

- **Retention described in a comment.** Filenames carry a date and the policy is
  "external cleanup handles it", which means nothing handles it, and the folder
  is as old as the installation.
- **Redaction pointed at the wrong thing.** The `Authorization` header is masked
  while the full response body is written verbatim. The credential is one line;
  the personal data is everything underneath it.

`docs/05` previously said only that credentials are redacted from logs. That
permits exactly the mistake above and says nothing about the facility that any
real connector will need on its first day.

## Decision

Raw request/response capture is a **named facility of the integration runtime**,
with these properties fixed in code rather than in configuration or a runbook:

- **A default retention applies when nobody configures anything.** Expiry is the
  runtime's job, not an operator's.
- **Capture is personal data** for every purpose — erasure requests, subject
  access, export, and the tenant's storage boundary. It is never classified as
  "logs".
- **Redaction covers the body, not only the header**, and covers credentials a
  provider requires in the payload itself (a password inside a SOAP security
  header, for example) *before* the capture is written.
- **Capture is off by default once a connector reaches production-certified
  status**, and is enabled deliberately per connector and dealership, with an
  expiry on the enabling so it cannot be switched on during an incident and left
  on.

## Alternatives considered

- **No raw capture; rely on structured logs.** Rejected: structured logs record
  what our code understood, which is precisely what is in doubt when a mapping is
  wrong or a vendor disagrees.
- **Capture everything, always, and rely on an operator to prune.** Rejected:
  this is the observed failure. It also makes every installation's disclosure
  obligations depend on somebody's cron job.
- **Capture with personal fields stripped at write time.** Rejected as the
  default: field-level stripping requires knowing the provider's shape, which is
  the thing capture exists to discover. Retention plus deliberate enablement
  bounds the exposure without pretending we already understand the payload.

## Consequences

- A connector author gets the diagnostic they would otherwise build badly, once,
  with the privacy obligations already attached.
- Erasure and export must reach the capture store. That is real work and it is
  the point of classifying it correctly now rather than after the first request.
- Debugging a production-certified connector takes one deliberate step to enable
  capture. Accepted: that step is also the audit record of who looked.

## Validation / review trigger

Revisit if a provider's terms forbid retaining raw responses at all, which would
make capture a per-provider capability rather than a runtime-wide one.
