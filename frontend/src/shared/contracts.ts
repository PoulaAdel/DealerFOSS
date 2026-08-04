// contracts — the shapes the API returns.
//
// Use:  api<InventoryUnitSummary[]>('/inventory')
// Edit: these mirror the read models in src/App/**/I<Feature>.cs. They are hand
//       written for now; when the API grows an OpenAPI document this file is
//       generated from it and stops drifting. Until then, changing a record on
//       the server means changing it here in the same commit.

export type SignInResponse =
  | { secondFactorRequired?: false; expiresAt: string }
  | { secondFactorRequired: true; challengeToken: string; expiresAt: string };

export interface CurrentUser {
  userId: string;

  /**
   * True when a role this person holds obliges them to have a second factor and
   * they do not yet. The server allows such a session to reach enrolment and
   * nothing else, so the browser must send them there rather than showing a
   * shell whose every link answers 403.
   */
  mustEnrolSecondFactor: boolean;
}

/** What an authenticator app needs, returned once and never again. */
export interface MfaEnrolment {
  secret: string;
  enrolmentUri: string;
}

/** Enough to identify a customer in a list. */
export interface CustomerSummary {
  id: string;
  displayName: string;
  kind: 'Person' | 'Business';
  primaryEmail: string | null;
  primaryPhone: string | null;
}

/** What a caller supplies to create a customer. Mirrors NewCustomer. */
export interface NewCustomer {
  kind: 'Person' | 'Business';
  firstName?: string | null;
  lastName: string;
  email?: string | null;
  phone?: string | null;
}

export interface RooftopSummary {
  id: string;
  name: string;
  code: string;
  timeZone: string;
  legalEntityId: string;
}

export interface OrganizationSummary {
  id: string;
  name: string;
  slug: string;
  legalEntities: { id: string; name: string; rooftops: RooftopSummary[] }[];
}

export interface InventoryUnitSummary {
  id: string;
  stockNumber: string;
  rooftopId: string;
  status: InventoryStatus;
  vehicleId: string;
  vin: string;
  vehicleDisplayName: string;
}

export type InventoryStatus =
  | 'Incoming'
  | 'Reconditioning'
  | 'Available'
  | 'OnHold'
  | 'Sold'
  | 'Removed';

export interface AccountBalance {
  code: string;
  name: string;
  kind: 'Asset' | 'Liability' | 'Equity' | 'Revenue' | 'Expense';
  debits: number;
  credits: number;
  balance: number;
}

export interface TrialBalance {
  from: string | null;
  to: string | null;
  currency: string;
  totalDebits: number;
  totalCredits: number;
  balances: boolean;
  accounts: AccountBalance[];
}

// --- bringing records in, and taking them out ---

export type ImportKind = 'Customers' | 'Vehicles';

/** A trial changes nothing and reports what an Apply would do. */
export type ImportMode = 'Trial' | 'Apply';

export interface ImportJobView {
  id: string;
  kind: ImportKind;
  mode: ImportMode;
  status: 'Queued' | 'Running' | 'Completed' | 'Failed';
  sourceName: string;
  sourceHash: string;
  rowsTotal: number;
  rowsCreated: number;
  rowsUpdated: number;
  rowsSkipped: number;
  rowsFailed: number;
  queuedAt: string;
  startedAt: string | null;
  finishedAt: string | null;
  failureReason: string | null;
}

/** One staged row, exactly as it arrived, and what happened to it. */
export interface ImportRowView {
  rowNumber: number;
  raw: string;
  outcome: 'Pending' | 'Created' | 'Updated' | 'Skipped' | 'Failed';
  message: string | null;
}

// --- the control plane ---
//
// Note what a tenant row does not carry: no connection string, no counts, and
// nothing an administrator could learn about a dealership's business from
// reading it. Routing and lifecycle only.

export interface CurrentAdministrator {
  administratorId: string;
  email: string;
  mustEnrolSecondFactor: boolean;
}

export type TenantStatus = 'Active' | 'Suspended' | 'Provisioning' | 'Archived';

export interface TenantRow {
  slug: string;
  name: string;
  status: TenantStatus;
  databaseVersion: string;
  createdAt: string;
}

export interface SupportAccessRecord {
  id: string;
  administratorId: string;
  administratorEmail: string;
  tenantSlug: string;
  reason: string;
  grantedAt: string;
  expiresAt: string;
  endedAt: string | null;
  isActive: boolean;
}

export interface GrantedSupportAccess {
  grantId: string;
  tenant: string;
  expiresAt: string;
}

export const inventoryStatuses: InventoryStatus[] = [
  'Incoming',
  'Reconditioning',
  'Available',
  'OnHold',
  'Sold',
  'Removed',
];
