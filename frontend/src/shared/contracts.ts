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

// --- how did we do this month? ---

/** One department's revenue, cost, and the difference. Margin is null when nothing sold. */
export interface DepartmentResult {
  name: string;
  revenue: number;
  cost: number;
  gross: number;
  margin: number | null;
}

/** The department names the server uses. Matching on a spelling, in one place. */
export const departments = {
  vehicles: 'Vehicles',
  finance: 'Finance and insurance',
  service: 'Service',
} as const;

export interface LedgerPerformance {
  from: string | null;
  to: string | null;
  currency: string;
  departments: DepartmentResult[];
  totalRevenue: number;
  totalCost: number;
  totalGross: number;
  /** Cars that left the lot, less any delivery reversed in the same period. */
  vehiclesDelivered: number;
  serviceInvoices: number;
}

export interface StockAgeBand {
  name: string;
  fromDay: number;
  /** Null on the last band, which is open-ended — and the one that matters. */
  toDay: number | null;
  units: number;
}

export interface AgingUnit {
  id: string;
  stockNumber: string;
  vehicleDisplayName: string;
  status: InventoryStatus;
  daysInStock: number;
  /** True when the age is counted from entry because no acquisition date was recorded. */
  ageIsEstimated: boolean;
}

export interface StockAging {
  asOf: string;
  units: number;
  bands: StockAgeBand[];
  /** The oldest few, named. A count says there is a problem; this says which cars. */
  oldest: AgingUnit[];
}

export type BooksState = 'NotOpened' | 'Open' | 'Closed' | 'Unknown';

/** Sections a caller may not read. Named rather than silently blank. */
export type WithheldSection = 'Trading' | 'Stock';

export interface MonthInReview {
  year: number;
  month: number;
  startsOn: string;
  /** The cutoff — the 30th or 31st, whichever this month has. */
  endsOn: string;
  books: BooksState;
  closedAt: string | null;
  /** Null when withheld. `withheld` says which sections those are. */
  trading: LedgerPerformance | null;
  /** The same query over the previous month, for comparison. */
  priorMonth: LedgerPerformance | null;
  stock: StockAging | null;
  withheld: WithheldSection[];
}

// --- the enquiry that comes before the deal ---

export type LeadStatus = 'New' | 'Working' | 'Appointment' | 'Won' | 'Lost';

export type LeadSource =
  | 'Unknown'
  | 'WalkIn'
  | 'Phone'
  | 'Website'
  | 'Referral'
  | 'Marketplace';

export const leadSources: LeadSource[] = [
  'WalkIn',
  'Phone',
  'Website',
  'Referral',
  'Marketplace',
  'Unknown',
];

export interface LeadSummary {
  id: string;
  rooftopId: string;
  status: LeadStatus;
  source: LeadSource;
  customerId: string;
  customerName: string;
  vehicleOfInterestId: string | null;
  /** Null when the enquiry named no particular car. Resolved server-side per page. */
  vehicleOfInterest: string | null;
  assignedToUserId: string | null;
  /** Who is chasing it, by name. Null when nobody has picked it up. */
  assignedTo: string | null;
  capturedAt: string;
  /** Stops counting on the day the lead closed, so aging is not skewed by old lost leads. */
  daysOpen: number;
}

export interface LeadHistoryEntry {
  // Typed as the status union rather than `string`. The API only ever sends a
  // LeadStatus here, RepairOrderHistoryEntry below already says so, and a bare
  // `string` meant nothing could translate it without an unchecked cast.
  fromStatus: LeadStatus | null;
  toStatus: LeadStatus;
  occurredAt: string;
  note: string | null;
}

/**
 * Note this does **not** extend LeadSummary: a detail carries the enquiry text
 * and the history a list has no business fetching, and the list carries an aging
 * figure the detail does not. Pretending one is the other would put a `daysOpen`
 * in the type that is never in the payload.
 */
/** A freshly created dealership. The code is shown once and never retrievable. */
export interface ProvisionedTenant {
  slug: string;
  name: string;
  managerEmail: string;
  enrolmentCode: string;
  openedBooksFrom: string;
}

export type FinanceProductKind = 'Warranty' | 'Gap' | 'ServicePlan' | 'Protection' | 'Other';

/** A product the dealership can sell with a car. The defaults are a starting point. */
export interface FinanceProductView {
  id: string;
  name: string;
  kind: FinanceProductKind;
  provider: string;
  defaultPrice: number;
  defaultCost: number;
  currency: string;
  termMonths: number | null;
  termMiles: number | null;
  isAvailable: boolean;
}

/** One product sold on a deal. Cost and gross never appear on a customer's copy. */
export interface DealProductView {
  id: string;
  financeProductId: string;
  name: string;
  provider: string | null;
  price: number;
  cost: number;
  gross: number;
  termMonths: number | null;
  termMiles: number | null;
}

export type AccountingPeriodState = 'Open' | 'Closed';

export interface AccountingPeriodView {
  id: string;
  year: number;
  month: number;
  state: AccountingPeriodState;
  startsOn: string;
  /** The cutoff — the 30th or 31st, whichever the month has. */
  endsOn: string;
  closedAt: string | null;
  closedByUserId: string | null;
  /** How many entries are dated into it. What a manager checks before closing. */
  entries: number;
  history: AccountingPeriodChangeView[];
}

export interface AccountingPeriodChangeView {
  fromState: AccountingPeriodState | null;
  toState: AccountingPeriodState;
  occurredAt: string;
  changedByUserId: string | null;
  note: string | null;
}

export type PartsCostingMethod = 'MovingAverage' | 'LastCost' | 'Fifo';

/** The current method and every method available, both from the server. */
export interface PartsCostingSetting {
  method: PartsCostingMethod;
  options: PartsCostingOption[];
}

export interface PartsCostingOption {
  method: PartsCostingMethod;
  name: string;
  explanation: string;
}

export interface PartSummary {
  id: string;
  partNumber: string;
  description: string;
  /** Null when the part has never been stocked anywhere the caller can see. */
  rooftopId: string | null;
  quantityOnHand: number;
  /** What one would cost to sell now under the current method. A forecast, not a commitment. */
  unitCost: number;
  currency: string;
}

export interface PartDetail {
  id: string;
  partNumber: string;
  description: string;
  costingMethod: PartsCostingMethod;
  stock: PartStockAtRooftop[];
}

export interface PartStockAtRooftop {
  rooftopId: string;
  quantityOnHand: number;
  unitCost: number;
  currency: string;
  layers: StockLayerView[];
}

/** One delivery still on the shelf — the honest answer to "why does this cost that?". */
export interface StockLayerView {
  id: string;
  quantityReceived: number;
  remainingQuantity: number;
  unitCost: number;
  receivedAt: string;
  reference: string | null;
}

export type RepairOrderStatus = 'Booked' | 'InProgress' | 'Completed' | 'Invoiced' | 'Cancelled';

export type ServiceLineKind = 'Labour' | 'Part' | 'Sublet';

/** Whether the customer has agreed to pay. Pending is what blocks an invoice. */
export type LineAuthorization = 'Pending' | 'Authorized' | 'Declined';

export interface RepairOrderSummary {
  id: string;
  rooftopId: string;
  number: string;
  status: RepairOrderStatus;
  customerId: string;
  customerName: string;
  vehicleId: string;
  vehicle: string;
  complaint: string;
  amountDue: number;
  currency: string;
  advisorUserId: string | null;
  technicianUserId: string | null;
  /** Phone calls the advisor owes. Every one of them blocks an invoice. */
  linesAwaitingAnswer: number;
  openedAt: string;
}

export interface ServiceLineView {
  id: string;
  kind: ServiceLineKind;
  description: string;
  hours: number | null;
  rate: number | null;
  amount: number;
  authorization: LineAuthorization;
  authorizedAt: string | null;
  authorizedByUserId: string | null;
  /** How the answer was obtained — the part that matters if it is questioned. */
  authorizationNote: string | null;
}

export interface RepairOrderHistoryEntry {
  fromStatus: RepairOrderStatus | null;
  toStatus: RepairOrderStatus;
  occurredAt: string;
  changedByUserId: string | null;
  note: string | null;
  amountAtChange: number;
}

export interface RepairOrderDetail {
  id: string;
  rooftopId: string;
  number: string;
  status: RepairOrderStatus;
  customerId: string;
  customerName: string;
  vehicleId: string;
  vehicle: string;
  complaint: string;
  odometerReading: number | null;
  currency: string;
  labourTotal: number;
  partsTotal: number;
  subletTotal: number;
  amountDue: number;
  advisorUserId: string | null;
  technicianUserId: string | null;
  openedAt: string;
  invoicedAt: string | null;
  /** Whether the work may still be edited. False once the job is Completed. */
  linesAreOpen: boolean;
  /** From RepairOrderStatusRules. The screen keeps no copy of the transitions. */
  availableMoves: RepairOrderStatus[];
  lines: ServiceLineView[];
  history: RepairOrderHistoryEntry[];
}

/** One colleague, as the staff screen sees them. Carries no credential material. */
export interface StaffMember {
  id: string;
  email: string;
  displayName: string;
  isActive: boolean;
  hasSecondFactor: boolean;
  canSignIn: boolean;
  /** True for a starter who has not redeemed a code — a different state from a leaver. */
  awaitingEnrolment: boolean;
  assignments: StaffAssignment[];
}

export interface StaffAssignment {
  id: string;
  roleId: string;
  roleName: string;
  isOrganizationWide: boolean;
  rooftopId: string | null;
}

export interface StaffRole {
  id: string;
  name: string;
  requiresSecondFactor: boolean;
  permissions: string[];
}

/** Returned once and never again — only its hash is stored. */
export interface StaffEnrolmentCode {
  code: string;
  expiresAt: string;
}

export interface LeadDetail {
  id: string;
  rooftopId: string;
  status: LeadStatus;
  source: LeadSource;
  customerId: string;
  customerName: string;
  vehicleOfInterestId: string | null;
  vehicleOfInterest: string | null;
  assignedToUserId: string | null;
  assignedTo: string | null;
  enquiry: string | null;
  capturedAt: string;
  closedAt: string | null;
  isOpen: boolean;

  /**
   * Where this lead may go from here, decided by `LeadStatusRules` on the server.
   *
   * The screen offers exactly these and holds no transition table of its own —
   * a second copy would drift, and the browser's would be the wrong one.
   */
  availableMoves: LeadStatus[];
  history: LeadHistoryEntry[];
}

// --- selling a car ---

export type DealStatus = 'Draft' | 'Submitted' | 'Approved' | 'Delivered' | 'Lost';

/** Exactly one VehiclePrice per deal; Discount is always negative. */
export type ChargeKind = 'VehiclePrice' | 'Fee' | 'Discount' | 'Accessory';

export const chargeKinds: ChargeKind[] = ['VehiclePrice', 'Fee', 'Discount', 'Accessory'];

export interface DealSummary {
  id: string;
  rooftopId: string;
  status: DealStatus;
  customerId: string;
  customerName: string;
  inventoryUnitId: string;
  stockNumber: string;
  vehicle: string;
  amountDue: number;
  currency: string;
  salespersonUserId: string | null;
  isApproved: boolean;
}

export interface ChargeView {
  // The union, not `string`. DealTerms was already casting this to ChargeKind
  // to seed its dropdown, which is the tell that the declared type was wider
  // than the truth — the API only ever sends one of the four.
  kind: ChargeKind;
  description: string;
  amount: number;
}

export interface TradeInView {
  description: string;
  allowance: number;
  payoff: number;
  equity: number;
  isNegativeEquity: boolean;
}

export interface DealHistoryEntry {
  // See the note on LeadHistoryEntry: the API sends a DealStatus.
  fromStatus: DealStatus | null;
  toStatus: DealStatus;
  occurredAt: string;
  changedByUserId: string | null;
  note: string | null;
  amountAtChange: number;
}

export interface DealDetail extends DealSummary {
  leadId: string | null;
  subtotal: number;
  tradeIn: TradeInView | null;
  charges: ChargeView[];
  /** What was sold alongside the car. */
  products: DealProductView[];
  /** What those products made. Reported apart from the car, as a dealer reads it. */
  productGross: number;
  approvedByUserId: string | null;
  approvedAt: string | null;
  /** False once submitted: the numbers are frozen from that point. */
  termsAreOpen: boolean;
  history: DealHistoryEntry[];
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
