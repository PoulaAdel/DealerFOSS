// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   permissions — the permission strings this browser needs to name, mirroring
//   src/Identity/Permissions.cs.
//
// Usage:
//   const { holds } = useSession();
//   holds(Permission.AccountingRead)
//
// Coding Instructions:
//   A PARTIAL MIRROR, ON PURPOSE. The server defines thirty-four permissions;
//   only the ones a screen needs in order to decide what to DRAW belong here.
//   Copying all of them would suggest the browser has opinions about the other
//   twenty-three, and it must not.
//
//   Same rule as contracts.ts: these are hand-written, so renaming one on the
//   server means renaming it here in the same commit. A string that stops
//   matching fails safe — `holds` returns false and the link is not drawn —
//   which is a missing link rather than an open door, and a missing link is
//   the failure you notice.
//
//   NOTHING HERE DECIDES ANYTHING. See the remarks on `holds` in session.tsx
//   and on GetHeldPermissionsAsync in IAccessDirectory.cs. The server enforces
//   every act for itself; these strings only keep a technician from being
//   shown the accounting menu.

export const Permission = {
  CustomersRead: 'Customers.Read',
  InventoryRead: 'Inventory.Read',
  LeadsRead: 'Leads.Read',
  DealsRead: 'Deals.Read',
  ServiceRead: 'Service.Read',
  PartsRead: 'Parts.Read',
  AccountingRead: 'Accounting.Read',
  StaffRead: 'Staff.Read',
  MigrationImport: 'Migration.Import',
  MigrationExport: 'Migration.Export',

  // The first entry here that decides whether to draw a COLUMN rather than a
  // link (ADR-029). Without it the server sends cost and gross as null, which is
  // also what an unrecorded figure looks like — so a screen that merely rendered
  // the null would tell a salesperson every car was bought for nothing. Asking
  // this instead lets it leave the column out altogether, which is the honest
  // drawing of "not yours to see".
  ProfitabilityRead: 'Profitability.Read',
} as const;

export type PermissionName = (typeof Permission)[keyof typeof Permission];
