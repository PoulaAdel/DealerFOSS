# 07 — Delivery Roadmap

← [Security & API](06-Security-and-API.md) · Next: [Governance & Standards](08-Governance-and-Standards.md)  
Visual: [Deployment topology](diagrams/07-deployment.md)

## 1. Delivery principle

The coexistence release remains focused and understandable. It proves one complete data path and daily workflows before adding breadth. Connector certification, migration reconciliation, security, backup, accessibility, and operability are product work—not a final hardening phase.

Durations assume a focused, experienced team and vendor access secured in Phase 0. Vendor delays change scope or date; fixture-only connectors do not count as complete.

## 2. Coexistence release plan

| Phase | Duration | Scope | Exit criteria |
|---|---:|---|---|
| 0. Discovery and access | 3–4 wk | pilot workflows, multi-rooftop rules, provider agreement/sandbox, source extracts, threat model, domain glossary | two pilots agree scope; one provider path and representative data are available; open questions have owners |
| 1. Operable foundation | 4 wk | Host, Organization, tenant/rooftop resolution, sessions/MFA, module boundaries, outbox/inbox, telemetry, installer | two isolated organizations including multi-rooftop data; unauthorized rooftop tests pass; install/upgrade/restore rehearsal succeeds |
| 2. Migration and connector runtime | 5–6 wk | staging, profiling, dedupe, mappings, export, connector SDK/runtime, replay/reconciliation | repeatable trial import with signed counts/exceptions; duplicate/reorder/delete/partial-failure tests pass; export round-trip passes |
| 3. Customer, vehicle, inventory | 4 wk | shared party/vehicle model, rooftop inventory, search, provenance, operational views | pilot data reconciles; ownership/freshness visible; performance target met |
| 4. CRM and sales workflow | 4–5 wk | leads, activities, deal versions, trades, approvals, documents | Lead → Deal workflow completed by pilot users; versioned approval and authorization tests pass |
| 5. F&I/service visibility | 4–5 wk | application/decision/contract summaries, compliance evidence shell, appointments/RO visibility | regulated data protected; evidence versions retained; Appointment → RO visibility reconciles |
| 6. Reporting and admin | 3 wk | operational dashboards, `[rpt]` projections, rooftop/group filters, tenant/connector/admin UI | each report states freshness; rooftop and organization totals reconcile; no admin tenant bypass |
| 7. Pilot readiness | 3–4 wk | usability/accessibility, load/security test, backup/DR, training/runbooks, release packaging | WCAG 2.2 AA review; p95 <500 ms; no critical security findings; SLO/backup/restore targets met |
| 8. Controlled pilot | 8 wk | two production pilots and support | 60 days within error budgets; reconciliation exceptions resolved; dealer export delivered; go/no-go review |

Implementation is approximately 7–8 months before the controlled pilot. Work may overlap, but no phase’s evidence is waived to preserve a date.

## 3. Standalone sequence

| Release | Scope | Promotion gate |
|---|---|---|
| Variable Operations | deeper desking, F&I provider integrations, tax/title workflow, funding/cancellation | parallel deal comparison and compliance/provider review |
| Fixed Operations | Parts, purchasing, Service, Workshop, warranty, RO invoice | inventory and RO totals reconcile through sustained parallel operation |
| Financial Core | GL, AP/AR, cash/bank, posting rules, deal/service/parts accounting, close | balanced postings and two consecutive parallel month-end closes |
| Dealer Group | shared services, intercompany, advanced consolidated reporting, hosted fleet tooling | multi-rooftop pilot, canary upgrades, DR and scale tests |

## 4. Testing

| Test type | Prevents |
|---|---|
| Domain unit and property tests | invalid state transitions, unbalanced journals, broken inventory conservation |
| Architecture tests/compiler references | cross-module coupling and domain-to-infrastructure dependencies |
| SQL/Redis integration tests | false confidence from mocked persistence/coordination |
| API contract and compatibility tests | silent client breakage |
| Connector conformance tests | paging, retry, ordering, delete, idempotency, and mapping failures |
| Migration/upgrade tests | tenant corruption and unsupported version jumps |
| Authorization isolation tests | cross-tenant and unauthorized-rooftop access |
| Accessibility and print visual tests | unusable keyboard/screen-reader workflows and broken forms/labels |
| Load, soak, and concurrency tests | performance regression and double processing |
| Backup/restore and failure drills | backups that exist but cannot recover a working system |

Coverage is reported but not used as a substitute for critical-case tests. Financial, inventory, authorization, migration, and sync invariants require named tests.

## 5. CI and releases

Every pull request passes format/build, unit, architecture, integration, contract, migration, frontend, accessibility smoke, security/dependency/license, and connector tests relevant to the change. Generated migrations and public contract changes receive explicit review.

A release candidate includes signed artifacts and SBOM, test and vulnerability disposition, database compatibility, upgrade/rollback instructions, restore result, known limitations, connector certification matrix, and changed runbooks.

## 6. Deployment and observability

The small-install default is one application host, SQL Server, and filesystem document store. Redis is optional unless multiple application nodes are configured. Quartz.NET persists job schedules in SQL. Windows and Linux-container packages use the same application behavior.

OpenTelemetry provides correlated logs, metrics, and traces. Minimum dashboards/alerts cover:

- availability, latency, errors, SQL and host saturation;
- login anomalies and authorization denials;
- job failures, outbox/inbox age, connector lag/throttling/quarantine;
- reconciliation differences and report freshness;
- database/document growth, backup age, restore verification;
- accounting imbalance and parts inventory exceptions once those modules ship.

Health endpoints separate liveness, readiness, and degraded dependencies. An external connector outage degrades that connector; it does not take the application offline.

## 7. Service targets

| Target | Coexistence release | Standalone financial core |
|---|---:|---:|
| Monthly interactive availability | 99.5% | 99.9% |
| Normal API p95 | <500 ms | <300 ms |
| Operational projection freshness | <60 sec | <30 sec |
| Connector lag | per provider; alert at twice expected interval | <15 min where provider supports it |
| Backup RPO | 24 h | 15 min |
| Restore RTO | 8 h | 4 h |
| Accounting imbalance | not applicable | zero |

Targets are configurable upward, but a deployment cannot claim the relevant support tier without monitoring and tested recovery.

## 8. Backup, retention, and disaster recovery

Backups cover host catalog, tenant databases, documents, configuration, and key-recovery material. They are encrypted, stored off-host, and checked automatically. A restore verifies database consistency, application start, tenant routing, document hashes, and representative workflows.

Coexistence deployments run monthly automated restore verification and quarterly operator restore exercises. Financial-core deployments add transaction-log backups and quarterly full disaster-recovery rehearsals. Retention and legal-hold behavior are tested with restore/purge.

## 9. Risk register

| ID | Risk | Impact | Response |
|---|---|---|---|
| R01 | vendor access/certification delayed | High | secure access in Phase 0; launch with one certified path plus repeatable import |
| R02 | scope expands toward full DMS in first release | Critical | enforce coexistence boundary and subsystem promotion gates |
| R03 | migration data is incomplete/dirty | High | raw staging, profiling, exceptions, dedupe, control totals, repeated trials |
| R04 | sync duplicates/loss/loops | Critical | durable inbox/outbox, idempotency, checkpointing, ownership rules, reconciliation |
| R05 | tenant or rooftop authorization leak | Critical | central scope resolution, architecture/integration tests, penetration test |
| R06 | dealer organization model is retrofitted late | High | organization/legal entity/rooftop/department foundation in Phase 1 |
| R07 | key/credential loss prevents recovery | Critical | documented key custody, rotation, escrow/recovery, restore drill |
| R08 | SQL Express or disk capacity exhausted | High | capacity metrics/alerts, sizing guide, supported SQL upgrade and document quotas |
| R09 | external contribution volume is low | Medium | core team funds reference connector and clear contribution fixtures |
| R10 | compliance rules become unmaintainable | High | versioned jurisdiction/provider packs with owners, evidence tests, and review dates |
| R11 | Windows-only assumptions limit adoption | Medium | IIS/Windows service and Linux container are supported |
| R12 | staff reject workflows | High | pilot observation, accessible design, training, in-product provenance/freshness |

## 10. Open decisions

Open decisions are visible and time-bounded:

| Decision | Close by | Criteria |
|---|---|---|
| First certified DMS provider | Phase 0 | signed access, sandbox/data fidelity, pilot match, cost, supportability |
| PDF/report renderer | Phase 1 | AGPL-compatible redistribution, deterministic printing, accessibility, maintenance |
| E-sign and communications providers | before Variable Operations | evidence export, compliance coverage, delivery reliability, portability, cost |
| bilingual UX, address/currency/tax/privacy/title tests and expert review |
| Runtime third-party plugins | before ecosystem launch | signing, isolation, compatibility, revocation, support, and license policy |
