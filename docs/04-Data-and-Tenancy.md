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
- Indexes follow measured query patterns and include all external-reference lookups and scope/status/date combinations used by operational screens.

## 5. Tenant and rooftop resolution

1. Authentication establishes the tenant ID and session.
2. Tenant middleware resolves the tenant database from the host catalog and verifies deployment/version status.
3. Authorization loads the user’s organization, rooftop, department, and permission assignments.
4. The endpoint declares the required scope. Data queries apply it centrally.
5. Organization-wide access is an explicit permission; global administration never grants silent access to tenant business data.

Background jobs carry a signed/validated tenant job context and create a fresh data context per tenant. They cannot reuse request-scoped tenant state.

## 6. Reporting

Reporting has two lanes:

- **Operational views:** indexed transactional queries or small projections for current inventory, appointments, dispatch, deal/funding status, cash exceptions, and sync errors. Target freshness is seconds to one minute.
- **Analytical `[rpt]` projections:** incremental projections for trends, aging, performance, and consolidated rooftop comparisons. Every report declares freshness; only explicitly non-urgent reports may be hourly.

Reporting never writes operational tables. Projection checkpoints and rebuild commands are durable and observable. Organization reports filter/aggregate rooftops within the tenant database.

## 7. Migration, retention, and portability

Imports land first in immutable staging tables with source file/hash, batch, raw value, mapping version, and error status. A migration run includes profiling, duplicate review, mappings, trial import, exceptions, control totals, delta cutover, sign-off, and rollback plan. Details live in [doc 05](05-Integration-Framework.md).

Data has a classification and retention rule. Legal holds suspend purge. PII export/restriction/deletion workflows preserve records that must be retained for financial or regulatory evidence. Documents and database records share retention identifiers.

Every supported release exports normalized JSON/CSV, relationship manifests, checksums, and original documents. Export is a product feature, not a support-only database dump.

## 8. Migrations and upgrades

The CLI supports host and tenant preview, backup verification, apply, resume, and status. Production migrations use expand → data migration → contract steps so the current and next application versions can coexist during an upgrade. Hosted/fleet operation must add canary rollout and automated tenant-version tracking before scaling beyond manually supportable deployments.
