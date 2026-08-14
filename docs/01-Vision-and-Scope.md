# 01 — Vision & Scope

← [Workbook](00-Workbook.md) · Next: [Architecture & Decisions](02-Architecture-and-Decisions.md)

## 1. Product vision

DealerFOSS is an open-source Dealer Management System for independent dealers and dealer groups. A deployment supports a dealer organization with one or many rooftops, shared customers and vehicles, rooftop-specific operations, and organization-wide reporting.

The product advances in two explicit stages:

- **Coexistence release:** run beside an incumbent DMS, unify dealer data and daily workflows, and prove imports, synchronization, reconciliation, security, and multi-rooftop access.
- **Standalone path:** become authoritative one subsystem at a time. A subsystem is called system of record only after its transactions, accounting effects, migration, export, backup, and reconciliation are proven in production.

This sequencing protects the ten-year vision. It avoids pretending that a modern UI and a few synchronized entities are already a complete DMS.

## 2. Problem and strategic wedge

Dealers lack control over their data and operate across a DMS, CRM, spreadsheets, portals, and paper. DealerFOSS’s initial wedge is:

1. repeatable migration and full data export;
2. a normalized organization-wide customer, vehicle, inventory, deal, and service view;
3. readable workflows and open APIs;
4. transparent synchronization and reconciliation rather than silent connector failures.

The long-term moat is tested mapping knowledge, dealer-owned history, reliable migration/reconciliation, and an open ecosystem. The number of connector class files is not a moat.

## 3. Users

| Role | Primary needs |
|---|---|
| Salesperson / BDC | customer lookup, leads, activities, deal progress |
| Sales / General Manager | approvals, inventory, gross, team and rooftop reporting |
| F&I Manager | applications, menu evidence, contracts, products, funding, cancellations |
| Service Advisor | appointments, estimates, authorization, repair orders, status |
| Technician / Dispatcher | assignments, labor operations, punches, parts readiness |
| Parts staff | stock, bins, purchase orders, receipts, issues, returns, counts |
| Accounting staff | posting batches, receivables/payables, cash, reconciliation, close |
| Dealer principal / group executive | consolidated and rooftop-specific performance |
| Dealer IT / administrator | one supported deployment, updates, backups, identity, diagnostics |
| Contributor / integrator | documented boundaries, contracts, fixtures, and extension points |

## 4. Capability map

### Coexistence release

Identity & Access · Organization & Rooftops · Customers · Vehicles · Inventory visibility · CRM · Sales workflow · basic F&I workflow/evidence · Service appointments and RO visibility · Documents · Reporting · Integrations · Migration/Export.

These capabilities may read or exchange data with the incumbent. The owning system is displayed per record and workflow.

### Standalone capabilities

The following are required before DealerFOSS can replace an incumbent DMS:

> **Two of these were started early, and that is recorded here rather than left
> to be discovered.** Partial Accounting and partial Parts exist in the codebase
> as of 2026-08-14 — Accounting has a chart of accounts, balanced journals,
> fiscal periods with close and reopen, and posting from a delivery or a service
> invoice; Parts has a catalogue, stock receipts, and costed issue to a job.
>
> Neither is close to the list below: Accounting has no subledgers, no AP/AR and
> no bank reconciliation; Parts has no purchasing, bins, returns, cores,
> supersession or physical count. §6 therefore still holds — the release promises
> neither complete accounting nor Parts, and neither is complete.
>
> What was anticipated is the **sequence**, not the promise. §8 places Parts at
> Years 2–4 and Accounting at Years 3–5, behind parallel-run reconciliation, and
> some of that work now exists before a single coexistence data path does.
> R02 in [doc 07](07-Delivery-Roadmap.md) rates exactly this drift Critical.
> **Neither should be extended further until the coexistence release has a
> certified connector.** They are useful, they are not the constraint, and
> "useful" is how the first release gets lost.

- **Accounting:** chart of accounts, balanced journals, subledgers, AP/AR, cash and bank reconciliation, posting rules, fiscal periods, close/reopen, and source traceability.
- **Deal accounting:** front/back gross, packs, commissions, incentives, trade payoff, CIT, funding variance, chargebacks, unwind, and product cancellation.
- **Parts:** catalog, bins, stock ledger, purchasing, receiving, issue/return, cores, supersession, pricing, and physical count.
- **Service depth:** estimates and authorization, dispatch, technician skills/time, flat-rate labor, inspections, warranty, recalls, sublet, comeback, and invoicing.
- **F&I compliance:** OFAC provider integration/evidence, Red Flags workflow, adverse-action and risk-based notices, Reg B/Z evidence, menu versions, lender stipulations, e-contracting, remittance, and cancellation.
- **Tax, title, and registration:** effective-dated jurisdiction rules, fees, exemptions, title/lien status, odometer disclosures, temporary tags, and provider integrations.

## 5. Multi-rooftop rules

- One tenant is one dealer organization.
- An organization contains one or more rooftops and each rooftop contains departments.
- Customers and vehicles may be shared across the organization.
- Inventory units, deals, repair orders, accounting entries, users’ operating access, and document retention always carry the appropriate rooftop and legal-entity scope.
- Users may be authorized for one rooftop, several rooftops, or the full organization.
- Organization-wide reports never require cross-tenant access.

## 6. Coexistence release scope

The release target is a useful, supportable product in approximately 6–8 months for a focused team. It includes:

- two pilot dealer organizations, including at least one multi-rooftop pilot;
- one live, certified connector and a second proven import path (API or CSV/SFTP);
- migration staging, deduplication, exception handling, control totals, and export;
- organization/rooftop-aware identity and permissions;
- customer, vehicle, inventory, CRM, deal, F&I evidence, service visibility, documents, and operational dashboards;
- observable sync with replay and reconciliation;
- secure installation, update, backup, and restore runbooks.

It does **not** promise seven live connectors, complete accounting, parts, full service invoicing, direct bureau access, e-contracting, or standalone DMS status.

## 7. Success criteria

- Two pilots operate for 60 days; one has multiple rooftops.
- A complete trial import can be repeated with signed record counts, amounts where applicable, duplicate decisions, and documented exceptions.
- One connector is certified against live or vendor-sandbox data; duplicate, reordered, deleted, and partially failed data is handled safely.
- Users can complete Lead → Customer → Deal workflow and Appointment → RO visibility without crossing unauthorized rooftop scope.
- API p95 is under 500 ms for normal interactive calls at 50 concurrent users per organization.
- Published availability, sync-lag, backup, and restore targets are met.
- No critical penetration-test findings or cross-tenant/unauthorized-rooftop access.
- Dealers can export their normalized records and documents without vendor assistance.
- The public repository contains AGPLv3 licensing, contribution/security policies, ADRs, install documentation, and a supported release.

## 8. Ten-year direction

| Horizon | Outcome |
|---|---|
| Year 1 | coexistence release, two pilots, one certified connector, migration/export proven |
| Years 1–2 | variable operations mature; selected F&I, tax/title, communications, and e-sign providers |
| Years 2–4 | Parts and Service become authoritative after parallel-run reconciliation |
| Years 3–5 | Accounting and deal posting achieve two successful parallel month-end closes before cutover |
| Years 4–7 | hosted option, additional certified connectors, mature dealer-group operation |
| Years 5–10 | credible system-of-record alternative for independent and small/mid-sized dealer groups |

The most likely failure is attempting incumbent breadth before one workflow and one data path are genuinely reliable. Release gates in [doc 07](07-Delivery-Roadmap.md) prevent that.
