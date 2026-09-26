# ADR-028 — A scheduled run carries the authority that armed it, re-checked every time

Date: 2026-09-26
Status: Accepted
Supersedes: —

## Context

Until now nothing in this application started an integration run. `ConnectorRuntime`
was registered in DI and called by tests; there was no endpoint, no worker, and
no stored record of which connector a rooftop used. Every run was somebody's
click, which is adequate for a one-off migration and useless for staying in step
with a live system overnight — the last named gap in stage 2.

Adding a trigger raises a question the rest of the system has already answered
for requests and for queued imports, and has to answer again here: **on whose
authority does work run when nobody asked for it?**

Two existing rules constrain the answer, and both were written to prevent the
shortcut that looks attractive:

- `ConnectorRuntime` refuses to start without an authenticated caller
  (`integration.no_run_as_user`). Its remarks say why: "an integration that could
  write records nobody is accountable for would be the one way into this
  application that leaves no name on the audit trail."
- `UnattendedScope.Get<T>` is constrained to `IUnattendedSafe`, whose allow-list
  holds exactly one type — `TenantDb`. A sweep therefore cannot compile a call to
  a capability that authorizes against a person (ADR from the 2026-09-05 work;
  `tests/Architecture/BoundaryTests.cs`).

The project has already tried the obvious thing and found out. From STATUS,
2026-09-05: "The first version of the new attribution test assumed an unattended
scope could write a customer and have the row say `system`. It cannot —
`CustomerService` reads `ICurrentUser.Id`, which throws."

## Decision

**A schedule names a person, and the runs it starts carry that person's
authority. The grant is re-checked at fire time, not trusted from arming time.**

Concretely:

- `ConnectorSchedule.ArmedByUserId` is a `Guid`, not a `Guid?`. There is no value
  of it meaning "the system" and no factory that leaves it unset — the same rule,
  for the same reason, as `JobContext.RequestedByUserId`.
- Arming is an exercise of the caller's own `Migration.Import` grant. A caller
  cannot arm a feed for a rooftop they could not import to by hand.
- **The polling is unattended; the run is not.** `ScheduleWorker` opens an
  `UnattendedScope` to find and claim a due schedule, because nobody asked for
  the polling and the claim is honestly the system's act. It then opens a second
  scope — a `TenantScope`, as the arming person — to run it. Two scopes per
  schedule, of two different types, exactly as `ImportWorker` does.
- **The worker writes no dealership record.** It decides *when*. Whether a record
  may be written is decided below it, by `ConnectorRuntime` inside the arming
  person's scope, through `IRecordSink`, by the capability that owns the record.
- **The grant is re-read on every fire**, inside the attended scope. A schedule
  whose owner has lost `Migration.Import` for that rooftop is **suspended with a
  readable reason**, not skipped and not failed.

## Why the grant is re-checked

This is the part that makes "it carries somebody's authority" true rather than
decorative, and it is the whole reason this ADR exists.

Somebody arms a nightly customer sync. Three months later they change roles, or
leave. Their name is still on the row. Without the re-check the feed keeps
writing on the authority of a grant that no longer exists, and the audit trail
records a former employee importing records every night — an attribution that is
false in the one place the system is supposed to be honest. A standing
instruction is still a person's instruction, and a person's instruction cannot
outlive their permission to give it.

Suspension rather than a silent skip matters for the same reason the cursor
refuses to advance rather than guessing: a feed that has quietly stopped is found
by a reconciliation months later, when the provider no longer holds the data.

**The re-check happens inside the attended scope on purpose.** It needs
`IAccessDirectory`, which is permission-checked and must never carry the
`IUnattendedSafe` marker. Doing it in the dispatcher would have meant widening
that allow-list — and the allow-list exists precisely so that widening it is an
argument somebody has to make out loud, not a convenience.

## Alternatives considered

- **A system principal for scheduled runs.** Rejected. It is the thing
  `ConnectorRuntime`'s own refusal was written to prevent, and it creates a
  second route into every record that no permission check can see. It also always
  succeeds, which is why it looks like it is working.
- **The sweep writes records itself through `TenantDb`.** Rejected twice over: it
  bypasses `IRecordSink`, which is the capability boundary that
  `FeatureBoundaryTests` enforces, and it would make Integrations the one place
  in the product that writes another capability's rows.
- **Trust the grant recorded at arming time.** Rejected — see above. It is the
  difference between an answerable authority and a fossil.
- **Re-check in the dispatcher, marking `IAccessDirectory` as `IUnattendedSafe`.**
  Rejected. The marker's own header names this exact move: "Do not add this
  marker to make a compile error go away. The error is the feature."
- **A service account per dealership, armed once.** Not rejected on principle, but
  it is a whole identity feature — provisioning, rotation, its own audit story —
  and it answers a question nobody has asked yet. A named person is the honest
  answer while a person is who actually set the feed up.

## Consequences

- A feed cannot outlive the permission that created it, and stops loudly when it
  tries to.
- Whoever repairs a suspended feed becomes its authority from then on, because
  `RearmAsync` re-states the caller rather than inheriting the old owner. That is
  the honest answer: they are the one saying it should run.
- Revoking somebody's `Migration.Import` now has a visible second effect — their
  schedules stop. This is intended, and it is why the reason text names the
  person's permission rather than saying "failed".
- `ISecretProtector` gains its first connector-credential caller, which is one of
  the two uses its own header names. On a development box the configured
  protector is a deliberate no-op, so the credential sits in the table in the
  clear; `Program.cs` already refuses to start with it in any other environment.

## What this does not decide

- **Nothing pushes.** This is a poll on a clock. Doc 05 §4 step 1's webhook path
  and step 2's durable inbox are untouched, and there is still no lease beyond a
  conditional update on one row.
- **No cron, no time-of-day, no backoff.** An interval, with a five-minute floor.
- Whether a future durable external queue would need a signed job context. Doc 04
  raised it and it was answered "no" for in-process work built from a row we just
  read. That answer still holds here, and the review trigger is unchanged.
