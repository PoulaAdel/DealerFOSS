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
//   A PARTIAL MIRROR, ON PURPOSE. The server defines thirty-three permissions;
//   only the ones a screen needs in order to decide what to DRAW belong here.
//   Copying all of them would suggest the browser has opinions about the other
//   twenty, and it must not.
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
} as const;

export type PermissionName = (typeof Permission)[keyof typeof Permission];
