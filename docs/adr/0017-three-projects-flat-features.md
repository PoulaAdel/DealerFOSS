# ADR-017 — Three projects, flat features, walls only where a breach is expensive

Date: 2026-07-30
Status: Accepted
Supersedes: [ADR-002](0002-capability-first-modules.md)

## Context

ADR-002 gave every dealership capability its own project with `Domain/`, `Data/`,
and `Contracts/` subfolders. After four capabilities that shape had produced seven
backend projects, five `DbContext` classes, and a four-level path to reach a file
that was frequently one screen long — `src/Modules/Customers/Domain/Customer.cs`.

Three costs showed up in practice rather than in theory:

- **Depth without information.** `Domain/Customer.cs` says nothing that
  `Customer.cs` does not. The folder repeated what the file name and the class
  already said.
- **Ceremony per capability.** A new capability meant a project file, a
  `DbContext`, a design-time factory, a migrations folder, a service registration,
  and solution wiring — before writing a line of dealership logic.
- **A boundary that was not actually load-bearing.** Every capability referenced
  every other capability's project anyway, because they all needed `Core` and
  `Identity`. The project boundary was enforcing a rule nobody was trying to
  break, at a cost paid on every single change.

Meanwhile one boundary *was* load-bearing and under-served: nothing stopped
application code constructing `IdentityDbContext` and writing a user row or an
audit row straight past `IAuditSink`. The development seeder did exactly that.

## Decision

Three backend projects, and the wall goes where a breach would be expensive.

```text
src/
├── Core/       no EF, no ASP.NET — Result, Money, Ids, Clock, and the interfaces
├── Identity/   users, roles, permissions, sessions, audit — internals sealed
└── App/        composition root, tenancy, and every business feature
```

**Compiler walls (a separate project) only where a breach is a security or
correctness incident.** Today that is exactly two: `Core`, which must never learn
about a database, and `Identity`, whose tables decide who may see what. Every
type in `Identity` is `internal` except `IAccessDirectory`, `IAuthenticator`, the
DI registration, the permission catalogue, and a Development-only seeding entry
point. Application code *cannot* write a user row or an audit row except through
those contracts. Accounting will earn the same treatment when it lands, for the
same reason.

**Architecture tests everywhere else.** Features inside `App` — Organization,
Customers, Vehicles, Inventory — are separated by folder and namespace. Nothing
in the compiler stops Customers reaching into Vehicles; `FeatureBoundaryTests`
does, and it fails the build when it happens. The rule is stated in one table
that a contributor can read in ten seconds, instead of being spread across seven
project files.

**Flat feature folders, files named by role.** No `Domain/`, `Data/`, or
`Contracts/` subfolder. A folder gets a subfolder only when it genuinely has too
many files to scan.

```text
App/Customers/
  Customer.cs  ContactPoint.cs  Address.cs   the records and their rules
  CustomerService.cs                         what you can do
  CustomerEndpoints.cs                       the HTTP surface
  CustomerTables.cs                          EF configuration
  ICustomers.cs                              what other features may call
```

**Three databases contexts instead of five.** `HostDb` (which dealer lives
where), `IdentityDb` (internal to Identity), and `TenantDb` (one dealer's
business data). Each feature contributes an `IEntityTypeConfiguration<T>` in its
own `XTables.cs`, and `TenantDb` picks them up with
`ApplyConfigurationsFromAssembly` — so a feature still owns its own mapping and
its own schema (`org`, `customers`, `vehicles`), and `TenantDb` knows no table
names.

## Alternatives considered

- **Keep a project per capability (ADR-002).** Rejected: the cost is paid on
  every change and the benefit was theoretical. A project boundary that everyone
  references anyway is not a boundary.
- **One project for everything, including Identity.** Rejected: it gives up the
  one wall that is genuinely worth having. Audit and user rows must be
  unreachable, not merely discouraged.
- **A `Features/` folder inside App.** Rejected: it adds a level that carries no
  information. `App/Customers/` and `App/Tenancy/` already read as business and
  plumbing without a word telling you which is which.
- **One `DbContext` for the whole tenant including identity.** Rejected: it would
  hand every feature a `DbSet<User>`, undoing the wall above.

## Consequences

- A new feature is a folder, a service registration line, and a map line. No
  project file, no context, no migrations folder.
- The features are now held apart by tests rather than by the compiler. That is a
  real trade: architecture tests fail *after* the code is written, where a
  missing project reference fails while typing. It is accepted deliberately,
  because the tests run on every build and the cost of the alternative was
  continuous.
- One migration per context instead of five. Table names and schemas are
  unchanged, so this is a repackaging, not a data migration.
- `Identity` is measurably stricter than before: `BoundaryTests` asserts its
  exported type list, so widening its public surface is a decision someone has to
  make on purpose.
- Moving a feature back out into its own project later stays cheap — it is
  already one folder with one namespace and one contract interface.

## Validation / review trigger

Revisit if a feature inside `App` grows past roughly fifteen files, if a
cross-feature dependency slips past the architecture tests into `main`, or when
Accounting lands — its ledgers are the next thing likely to deserve a compiler
wall of its own.
