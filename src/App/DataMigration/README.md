# Data migration

Bringing a dealership's existing records in from a file, and letting them take
them away again. [Doc 05 §6](../../../docs/05-Integration-Framework.md) — the
migration workflow — without the connector half, which cannot be built honestly
until there is a provider to test against.

> **Why the folder is `DataMigration` and not `Migration`.** `DealerFOSS.Migration`
> shadows `Microsoft.EntityFrameworkCore.Migrations.Migration`, which every
> generated EF migration inherits from. Those files are generated artifacts and
> must not be hand-edited, so the namespace moved instead. The HTTP route is
> still `/api/v1/migration`.

## The shape

Submitting a file and importing it are separate acts.

1. `POST /api/v1/migration/imports` validates the columns, **stages every row
   exactly as it arrived**, and answers `202` with a job. Nothing is imported.
2. `ImportWorker` picks the job up, decides each row, and records the outcome.
3. `GET /imports/{id}` is the reconciliation report; `GET /imports/{id}/rows`
   is the exception list.

A real dealer extract is tens of thousands of rows. A request that tried to
finish the work would time out somewhere in the middle, having half-imported
their customers with no record of where it stopped.

## The three properties worth protecting

**A trial changes nothing, and says what the real run will do.** Every row goes
through the same validation and the same natural-key lookup in both modes; only
the write at the end is skipped. This is why `Vin.IsWellFormed` is checked *in
the runner* rather than left to `IVehicles.AddAsync` — a check that only the
write performs makes a trial optimistic, and an optimistic trial is worse than
none. A test asserts the two agree about an unusual VIN.

What a trial genuinely cannot predict is named rather than hidden: a failure only
the database can raise, and the effect of rows on each other. Two rows carrying
the same VIN both read as "create" in a trial, because neither has been written
when the other is examined; the real run finds the first and reports the second
as skipped. The counts move by one.

**Importing the same file twice does not duplicate anything.** Vehicles match on
VIN — the industry agreed on a seventeen-character key, so no external reference
is needed. Customers match on `externalid`, their identifier in the system they
came from, stored on `Customer.ExternalReference` under a filtered unique index.
Filtered, because a plain one would allow exactly one hand-typed customer per
database.

**The counts add up.** Created, Updated, Skipped, Failed — every row lands in
exactly one, and they sum to the row total. A reconciliation report nobody can
trust is worse than no report.

## Staged rows are never rewritten

`ImportRow.Raw` holds the line as it arrived and is not edited, because the
workflow forbids resolving an exception by changing what the dealership sent
(doc 05 §6 step 6). Fix the source, import again. The file's SHA-256 is on the
job, so a trial and the run that follows it can be shown to be about the same
extract.

The header is staged too, as row 1, so the original document can be
reconstructed exactly and the worker reads the columns from what actually
arrived rather than from a re-joined copy.

## Columns

Names are matched without case, spaces, or underscores — `Model Year`,
`model_year`, and `MODELYEAR` are the same column. Extra columns are ignored
rather than refused: a dealership exports what their old system gives them.

| Kind | Required | Also read |
|---|---|---|
| Customers | `externalid`, `lastname` | `kind`, `firstname`, `email`, `phone`, `addressline1`, `addressline2`, `city`, `state`, `postalcode`, `country` |
| Vehicles | `vin`, `modelyear`, `make`, `model` | `trim`, `bodystyle`, `exteriorcolor`, `vinexceptionreason` |

A missing column is refused at submission, and the response names which ones —
the difference between fixing the file in a minute and guessing at it for an
afternoon.

## An export is a valid import

`GET /api/v1/migration/exports/{kind}` returns a CSV whose columns are **exactly
what the importer reads**, in an order it accepts. That is not a formatting
preference — it is the promise an open DMS makes. A dealership can take their
records to a competitor, or bring them back, without anybody here writing them a
converter.

`ExportTests.Records_survive_a_round_trip_into_another_dealership` exports one
tenant and feeds that exact file to a *different* one through the ordinary import
endpoint, then checks nothing was refused. It earned its place immediately: it
caught the exporter dropping `vinexceptionreason`, which made every trailer and
pre-1981 vehicle un-importable — the written reason that permitted the unusual
VIN was gone, so the row was correctly refused on the way back in. A round trip
is the only test that would have found that.

Three smaller decisions:

- **Every field is quoted**, unconditionally. It costs a few bytes and removes
  the whole class of bug where `Bob "Big Bob" Special, Ltd` becomes two columns
  in whatever receives the file.
- **A hand-typed customer exports under its own id.** It has no external
  reference, and a row without one is refused on import — so an export of
  hand-typed customers would not be an export at all. Their id here *is* their
  reference to the outside.
- **`X-Content-SHA256` and `X-Row-Count`** ride on the response. A file truncated
  in transit is worse than one that failed, because it looks like data.

Exporting is a separate permission from importing (`Migration.Export`), because
it is a different act: bulk personal data leaving the building
([doc 06 §3](../../../docs/06-Security-and-API.md)). Somebody trusted to load a
supplier's stock list is not automatically trusted to walk out with every
customer the group has. Both are organization-wide or nothing.

Reads walk the set with **keyset paging** (`PageForExportAsync`), not offsets: an
offset shifts under a concurrent insert, so a long export would silently skip or
repeat somebody. One export is capped at 50,000 rows — beyond that the response
needs streaming, which changes how it is written and none of the logic.

## The worker is the pattern for background work

`ImportWorker` is the first thing in the system that is not a request, and four
of its habits are the template for reconciliation, outbox delivery, and whatever
follows.

- **It names the tenant.** There is no ambient "current dealership" outside a
  request and there must never be one. Every unit of work opens an
  `ITenantScopeFactory` scope for a tenant it has explicitly identified.
- **It runs as the person who asked.** The requester is part of the `JobContext`
  the scope is opened with, so the import is authorized by their permissions and
  audited under their name. This used to be a habit; since 2026-09-05 it is the
  only way `OpenAsync` can be called, because a worker running as nobody is a
  permission check silently skipped.
- **It claims before it works.** Two instances share one database, so Queued →
  Running is a conditional update and the loser finds nothing to do.
- **It uses two scopes per job, deliberately.** Polling and claiming is nobody's
  request — that one is an `UnattendedJob` and its writes say `system`. Running
  the job is the requester's, and that scope is opened only once their id is
  known. One scope meant the claim was written before any caller existed, and a
  job that failed early was recorded as the system's doing while one that failed
  late was recorded as a person's.
- **The two scopes are different TYPES, and can see different things.** The
  dispatcher holds an `UnattendedScope`, whose only way to a service is
  `Get<T>() where T : IUnattendedSafe` — it reaches `TenantDb` to claim the job
  and cannot compile a call to `ICustomers`. The run holds a `TenantScope` with
  the full provider, because it has a person to authorize against. That is why
  `MarkFailedAsync` takes a `TenantDb` rather than a scope: both callers can
  produce one, and neither has to pretend to be the other kind.

## Not built yet

Updating an existing customer's details from a file — a matched row is reported
and left alone, because deciding that a file outranks what staff have since
typed is a policy nobody has set. Deletions and tombstones. Profiling and
duplicate detection before a run (doc 05 §6 step 3). Cancellation of a running
job. Any screen: this is API-only.

**Only customers and vehicles.** Leads, deals, and the ledger have no import or
export path, so "take your data" is not yet the whole truth — it is the two
record types a dealership would migrate first. Streaming for an export past
50,000 rows. Documents, and the relationships between records.

Every imported record writes its own audit entry, so a 20,000-row import writes
20,000 of them. That is the correct answer to "who created this customer" and a
lot of rows; if it becomes a problem the fix is a bulk-attributed entry, not
silence.
