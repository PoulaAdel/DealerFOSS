# 04 — Data & Tenancy

← [Project Structure](03-Project-Structure.md) · Next: [Integration Framework](05-Integration-Framework.md)  
Visual: [Tenant resolution](diagrams/03-tenant-resolution.md)

## 1. Tenant model

A tenant is a **dealer organization**, not a single building. One tenant database supports one or many rooftops.

```text
DealerOrganization (tenant)
└── LegalEntity (one or more)
    └── Rooftop (one or more physical dealerships)
        └── Department (Sales, F&I, Service, Parts, Accounting...)
```

Customers and vehicles can be organization-shared. Inventory units, deals, repair orders, accounting entries, documents, and most employee assignments record their rooftop and legal-entity scope. A user can have different roles at different rooftops.

## 2. Databases

- **`DealerFOSS_Host`:** tenant ID, name/slug, deployment status, encrypted connection reference, database version, feature flags, allowed domains, and global administrator identities. It contains no dealership business data.
- **`DealerFOSS_Tenant_{id}`:** organization, rooftops, departments, users, business data, integration state, audit records, and `[rpt]` projections for that dealer organization.

No request may select an arbitrary tenant connection. Tenant middleware resolves the authenticated tenant once, validates it is active, and creates the scoped tenant data context. A request may legitimately access multiple authorized rooftops inside that tenant database.

## 3. Core data model

> **This is the target model, not the schema.** Roughly half of the tables named
> below exist; the rest are the shape the product is heading for. Do not read a
> name here as evidence that a table is there — the schema is what
> `src/App/**/*Tables.cs` configures, and
> [`implementation/STATUS.md`](implementation/STATUS.md) says which capabilities
> are built.

### Organization and identity

`DealerOrganizations`, `LegalEntities`, `Rooftops`, `Departments`, `Users`, `Roles`, `Permissions`, `UserAssignments` (user + scope + role), `Sessions`, `AuditEvents`.

### Shared operational model

- **Party/Customers:** `Parties`, `People`, `Organizations`, `ContactPoints`, `Addresses`, `PartyRelationships`, `Consents`, `IdentityMatches`.
- **Vehicles/Inventory:** `Vehicles`, `VehicleIdentifiers`, `InventoryUnits`, `InventoryLocations`, `InventoryStatusHistory`, `VehicleImages`.
- **CRM/Sales:** `Leads`, `Activities`, `Deals`, `DealVersions`, `TradeIns`, `DealApprovals`, `DealCharges`, `DealProducts`.
- **Finance/compliance:** `CreditApplications`, `LenderDecisions`, `FinanceContracts`, `FinanceProducts`, `Menus`, `ComplianceEvents`, `Notices`, `FundingEvents`, `ProductCancellations`.
- **Service:** `Appointments`, `RepairOrders`, `RepairConcerns`, `Estimates`, `Authorizations`, `RepairLines`, `LaborOperations`, `TechnicianAssignments`, `TimeEntries`, `WarrantyClaims`.
- **Documents/communications:** `Documents`, `DocumentVersions`, `Signatures`, `MessageTemplates`, `Messages`, `DeliveryEvents`.

Standalone Parts, Accounting, and Tax/Title tables are introduced only with their modules and migration plans, but their required concepts are defined in [doc 01 §4](01-Vision-and-Scope.md).

## 4. Conventions

- Primary keys are application-generated UUIDs; external IDs are never primary keys.
- `ExternalReferences` stores provider, entity type, external ID, external version, first/last seen, and deletion state.
- Business records use `CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy`, and an optimistic concurrency token.
- Soft delete is used only where deletion is a valid domain action. Posted financial entries, stock movements, evidence, and audit events are never edited or deleted; corrections are appended.
- Email is a contact point, not a unique customer identity. VIN validation supports documented exceptions and duplicate-resolution workflow rather than relying on a universal unique index.
- Money stores amount and ISO currency. Address stores country plus country-appropriate administrative area and postal code. Tenant settings include time zone, culture, default currency, and enabled jurisdiction rule packs.
- **Jurisdiction packs are reference data in the host catalog, not tenant data** ([ADR-024](adr/0024-compliance-is-baseline-pack-and-posture.md)). Rates and rules are effective-dated and never updated in place; only the *enablement* is per tenant. A deal stores the tax it charged as evidence — amount, basis, rate, jurisdiction, pack version, resolved address and provenance — frozen at the sale and never recomputed on read.
- **An address carries state and county separately** (2026-09-05). `Address.AdministrativeArea` is the state, province or region; `Address.County` is the sub-division within it, null in most countries. US sales tax varies by both, so folding them together loses what decides the rate. Neither is inferred from the other or from the postcode: an absent county is absent ([ADR-021](adr/0021-coerced-values-become-absent.md)), because a guessed county is a wrong tax rate that looks exactly like a right one.
- Indexes follow measured query patterns and include all external-reference lookups and scope/status/date combinations used by operational screens.

## 5. Tenant and rooftop resolution

1. Authentication establishes the tenant ID and session.
2. Tenant middleware resolves the tenant database from the host catalog and verifies deployment/version status.
3. Authorization loads the user’s organization, rooftop, department, and permission assignments.
4. The endpoint declares the required scope. Data queries apply it centrally.
5. Organization-wide access is an explicit permission; global administration never grants silent access to tenant business data.

Background jobs must carry a validated tenant job context and create a fresh data context per tenant, and cannot reuse request-scoped tenant state. **Both halves now hold** (2026-09-05). `JobContext` names the dealership *and* the person the work runs as — `RequestedByUserId` is a `Guid`, not a `Guid?`, so there is no value of it meaning "nobody". `ITenantScopeFactory.OpenAsync` accepts nothing else and there is no string overload, so a worker that omits either does not compile. The factory sets both holders before it returns the scope, and `Set` is not on `ICurrentUser` or `ITenantContext` — a feature can ask who the caller is and cannot decide.

Work nobody asked for is a **different type** carrying a required reason, `UnattendedJob`, opened through `OpenUnattendedAsync`. It returns an `UnattendedScope`, which has **no `IServiceProvider`**: the only way out is `Get<T>() where T : IUnattendedSafe`, an allow-list marker that today only `TenantDb` carries. So a sweep asking for a permission-checked capability is a **compile error** (CS0311), not a throw from somewhere inside it, and its rows are attributed to `system`.

The guarantee is worth stating exactly, because it is narrower than it sounds: an unattended job cannot use a service that authorizes against a person. It can still write through `TenantDb` — attributed to `system`, under the append-only rules, through domain constructors that enforce their own invariants. What is closed is the case where a sweep passes every permission check because there is nobody to fail one.

> **A word this paragraph used to carry, corrected 2026-09-05.** It said the
> context must be *signed*. It is not, and a signature would not be the
> guarantee here. Signing exists to protect a job description crossing an
> untrusted boundary; ours is constructed in-process from a row the worker just
> read out of the tenant's own database, so it would be signing our own data to
> ourselves. **The constructor is the guarantee.** If a durable external queue
> is ever introduced, the signing question comes back with it — and that is the
> review trigger, not a task outstanding today.

## 6. Reporting

> **One lane of the two exists.** There is no `[rpt]` schema and no projection —
> `grep -rn '"rpt"' src` finds nothing. Reporting today is the operational lane:
> indexed queries in `src/App/Reporting` answering the month in review and stock
> aging. The second lane below is the design for when a query gets too slow to
> run live, and that has not happened yet.

Reporting has two lanes:

- **Operational views:** indexed transactional queries or small projections for current inventory, appointments, dispatch, deal/funding status, cash exceptions, and sync errors. Target freshness is seconds to one minute.
- **Analytical `[rpt]` projections:** incremental projections for trends, aging, performance, and consolidated rooftop comparisons. Every report declares freshness; only explicitly non-urgent reports may be hourly.

Reporting never writes operational tables. Projection checkpoints and rebuild commands are durable and observable. Organization reports filter/aggregate rooftops within the tenant database.

## 7. Migration, retention, and portability

Imports land first in immutable staging tables with source file/hash, batch, raw value, mapping version, and error status. A migration run includes profiling, duplicate review, mappings, trial import, exceptions, control totals, delta cutover, sign-off, and rollback plan. Details live in [doc 05](05-Integration-Framework.md).

Data has a classification and retention rule. Legal holds suspend purge. PII export/restriction/deletion workflows preserve records that must be retained for financial or regulatory evidence. Documents and database records share retention identifiers.

Every supported release exports normalized JSON/CSV, relationship manifests, checksums, and original documents. Export is a product feature, not a support-only database dump.

## 8. Migrations and upgrades

**There is no CLI.** This section described one — preview, backup verification, apply, resume, status — and doc 02 §4 has recorded "There is no CLI" since 2026-08-14. Both cannot be true. What exists today is `dotnet ef` against four migration sets ([Local Development](LOCAL-DEVELOPMENT.md#migrations)) and the application applying them at start-up; an operator's route is [Operating](OPERATING.md).

The rest of this section is the intended shape and is unbuilt: production migrations should use expand → data migration → contract steps so the current and next application versions can coexist during an upgrade, and hosted/fleet operation must add canary rollout and automated tenant-version tracking before scaling beyond manually supportable deployments.
