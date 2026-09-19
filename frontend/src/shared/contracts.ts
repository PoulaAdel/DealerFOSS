// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   contracts — the shapes the API returns.
//
// Usage:
//   Api<InventoryUnitSummary[]>('/inventory')
//
// Coding Instructions:
//   These mirror the read models in src/App/**/I<Feature>.cs. They are hand
//   written for now; when the API grows an OpenAPI document this file is
//   generated from it and stops drifting. Until then, changing a record on
//   the server means changing it here in the same commit.

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

  /**
   * Every permission this person holds SOMEWHERE — organization-wide, or on at
   * least one rooftop. Sorted, and empty while they owe a second factor.
   *
   * **For deciding what to OFFER, never for deciding what is allowed.** The
   * server enforces every act for itself and will refuse one regardless of
   * what this list says; hiding a link is only a courtesy so that a technician
   * stops seeing Books and Staff and learning by being refused.
   *
   * It carries no scope on purpose. Somebody who may read accounting at one
   * rooftop out of four appears here the same as somebody who may read it
   * everywhere, because the question it answers is whether to draw the link at
   * all. Anything that needs to know *where* must ask the server.
   */
  permissions: string[];
}

/**
 * What this installation can offer somebody who is locked out. Deliberately not
 * per-account: asking about a specific email would answer whether that person
 * works here and whether they have an authenticator, which is a map of who is
 * easiest to attack. See ADR-018.
 */
export interface RecoveryMethods {
  authenticator: boolean;
  issuedCode: boolean;
  email: boolean;
  textMessage: boolean;
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

/** One customer in full, as the detail band below the results shows them. */
export interface CustomerDetail {
  id: string;
  displayName: string;
  kind: 'Person' | 'Business';
  firstName: string;
  lastName: string;
  homeRooftopId: string | null;
  address: AddressView | null;
  contactPoints: ContactPointView[];
  /** Their id in the system this record came from. Null when typed in by a person. */
  externalReference: string | null;
  /** The most this customer may owe across every open bill. Null is no cap. */
  creditLimit: number | null;
}

export type ContactKind = 'Email' | 'Phone' | 'Mobile';

export interface ContactPointView {
  id: string;
  // The union rather than `string`, so useEnumLabel can translate it. A bare
  // string forces an unchecked cast at every call site and loses the guarantee
  // that the catalogue has a label for whatever arrives.
  kind: ContactKind;
  value: string;
  isPrimary: boolean;
}

export interface AddressView {
  line1: string;
  line2: string | null;
  city: string;
  administrativeArea: string | null;
  /**
   * The county, where a country has one. Separate from administrativeArea
   * because US sales tax varies by state AND by county (ADR-024). Null in most
   * countries, and null wherever nobody supplied it — never inferred from the
   * state or the postcode.
   */
  county: string | null;
  postalCode: string | null;
  country: string;
}

/**
 * Enough to identify a car in a list. A customer's own car, which is not the
 * same thing as a unit in stock — see InventoryUnit for that.
 */
export interface VehicleSummary {
  id: string;
  vin: string;
  displayName: string;
  modelYear: number;
  make: string;
  model: string;
  trim: string | null;
  /** A car recorded without a valid VIN, which the workshop is allowed to do. */
  hasVinException: boolean;
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
  /** What the dealership paid to acquire the car, and nothing else. */
  costAmount: number | null;
  costCurrency: string | null;

  /**
   * Work capitalised onto this car since it came into stock. Zero, never null:
   * a car with no recon has absorbed nothing, which is a known amount.
   */
  reconditioningAmount: number;

  /**
   * Acquisition plus reconditioning — what the car is carried at, and what
   * delivery relieves from the ledger. Null exactly when `costAmount` is,
   * because a book value built on an unknown purchase price would be a smaller
   * number wearing the confidence of a complete one.
   */
  bookValueAmount: number | null;
}

/** One unit in full, as the detail band below the stock list shows it. */
export interface InventoryUnitDetail extends InventoryUnitSummary {
  /** ISO date. Null for a unit taken in before the field was recorded. */
  acquiredOn: string | null;
  history: InventoryStatusEntry[];

  /** Every posting behind `reconditioningAmount`, oldest first. */
  reconditioning: ReconditioningEntry[];
}

export interface ReconditioningEntry {
  amount: number;
  currency: string;
  sourceRepairOrderId: string;
  occurredAt: string;
}

export interface InventoryStatusEntry {
  fromStatus: InventoryStatus | null;
  toStatus: InventoryStatus;
  occurredAt: string;
  note: string | null;
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
  isCancelled: boolean;
  cancelledAt: string | null;
  refundAmount: number | null;
  cancellationReason: string | null;
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

/**
 * Who settles a line. Not a discount and not a status: the work happened either
 * way, and this says who is invoiced for it. Warranty goes to the manufacturer,
 * Internal is carried by the dealership — reconditioning its own stock, most
 * often — and only CustomerPay ever reaches the customer's bill.
 */
export type ServicePayType = 'CustomerPay' | 'Warranty' | 'Internal';

export const servicePayTypes: readonly ServicePayType[] = [
  'CustomerPay',
  'Warranty',
  'Internal',
];

/** Whether the customer has agreed to pay. Pending is what blocks an invoice. */
export type LineAuthorization = 'Pending' | 'Authorized' | 'Declined';

/**
 * A catalogued job: what the workshop sells, what it is called everywhere, and
 * how long it ought to take.
 *
 * Organization-wide, like a part number. What an HOUR costs is per rooftop and
 * lives in LabourRateView -- two lots do not charge the same.
 */
export interface OpCodeView {
  id: string;
  code: string;
  description: string;
  standardHours: number;
  defaultPayType: ServicePayType;
  isActive: boolean;
}

/** What an hour sells for at one lot, for one kind of payer. */
export interface LabourRateView {
  id: string;
  rooftopId: string;
  name: string;
  amountPerHour: number;
  currency: string;
  appliesTo: ServicePayType;
  isActive: boolean;
}

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

  /**
   * Which lot this job belongs to -- "NAG-01".
   *
   * Job numbers restart per rooftop by design, so a list mixing lots shows two
   * different jobs under one number. Empty when the rooftop could not be
   * resolved, which the screen treats as "do not show it".
   */
  rooftopCode: string;

  /** Phone calls the advisor owes. Every one of them blocks an invoice. */
  linesAwaitingAnswer: number;
  openedAt: string;
}

/** Where a booking got to. Only Scheduled can still be moved or arrived. */
export type AppointmentStatus = 'Scheduled' | 'Arrived' | 'NoShow' | 'Cancelled';

export interface AppointmentView {
  id: string;
  rooftopId: string;
  scheduledFor: string;
  /** Workshop time, not a slot length. Null means nobody estimated. */
  estimatedHours: number | null;
  status: AppointmentStatus;
  customerId: string;
  customerName: string;
  vehicleId: string;
  vehicle: string;
  reason: string;
  advisorUserId: string | null;
  /** The job this became. The only link between the diary and the workshop. */
  repairOrderId: string | null;
  repairOrderNumber: string | null;
  arrivedAt: string | null;
  outcome: string | null;
  isOpen: boolean;
}

/**
 * One day's commitment. `bookedHours` counts only cars still expected — once one
 * arrives its hours belong to the job, and counting both would show a workshop
 * as twice as busy as it is.
 */
export interface DiaryDay {
  date: string;
  expected: number;
  bookedHours: number;
  /** Reported apart from the hours: a car nobody estimated is not zero work. */
  unestimated: number;
}

export interface Diary {
  appointments: AppointmentView[];
  load: DiaryDay[];
}

/** Both halves, because the screen that marks a car in then wants the job. */
export interface ArrivalResult {
  appointment: AppointmentView;
  repairOrder: RepairOrderDetail;
}

export interface ServiceLineView {
  id: string;
  kind: ServiceLineKind;
  description: string;
  hours: number | null;
  rate: number | null;
  amount: number;
  /** Who is invoiced for this line. See ServicePayType. */
  payType: ServicePayType;
  authorization: LineAuthorization;
  authorizedAt: string | null;
  authorizedByUserId: string | null;
  /** How the answer was obtained — the part that matters if it is questioned. */
  authorizationNote: string | null;

  /**
   * The catalogued job this line sells, when it is one. Null is legitimate: a
   * one-off job nobody will do again still has to be billable.
   *
   * Provenance only — the description, hours and rate were copied onto the line
   * when it was written and do not move when the catalogue is revised.
   */
  opCodeId: string | null;
}

/** One stretch of time one technician spent on one job. */
export interface ClockingView {
  id: string;
  technicianUserId: string;
  startedAt: string;
  stoppedAt: string | null;

  /** Zero while it is still running -- a figure that changed every time you
   * looked at it could not be reconciled against anything. */
  hours: number;
  isOpen: boolean;

  /** Why it stopped, when it was not a person pressing stop. */
  stoppedBecause: string | null;
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

  /**
   * Which lot this job belongs to. Always worth showing here, unlike in a list:
   * one job read on its own carries no context to infer it from.
   */
  rooftopCode: string;
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
  /** What the CUSTOMER owes. Warranty and internal work is not in this figure. */
  amountDue: number;
  /** Owed by the manufacturer once a claim is accepted. */
  warrantyTotal: number;
  /** Carried by the dealership itself, never billed out. */
  internalTotal: number;
  /** Everything the job is worth, whoever settles it. */
  workTotal: number;
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

  /** Who has been on this job and for how long, newest first. */
  clockings: ClockingView[];

  /**
   * Hours actually spent, summed from the CLOSED clockings. What the job cost
   * in time, as against labourTotal which is what it was worth.
   */
  clockedHours: number;

  /**
   * This job's warranty claim, when it has one — meaning it has been invoiced
   * with warranty-pay work on it. Null before invoicing, and null forever on
   * a job with no warranty lines.
   */
  warrantyClaim: WarrantyClaimView | null;
}

export type WarrantyClaimStatus = 'Open' | 'Submitted' | 'Approved' | 'Denied' | 'Paid';

/** One repair order's warranty claim: what was billed, and where it stands. */
export interface WarrantyClaimView {
  id: string;
  status: WarrantyClaimStatus;
  amount: number;
  /** What the manufacturer actually paid. Null until status is Paid, and may differ from `amount`. */
  amountPaid: number | null;
  currency: string;
  availableMoves: WarrantyClaimStatus[];
  history: WarrantyClaimHistoryEntry[];
}

export interface WarrantyClaimHistoryEntry {
  fromStatus: WarrantyClaimStatus | null;
  toStatus: WarrantyClaimStatus;
  occurredAt: string;
  changedByUserId: string | null;
  note: string | null;
}

/**
 * What the workshop sold over a period.
 *
 * `notMeasured` is not an error list — it names the figures the trade expects
 * that this system cannot honestly produce, so the screen can say so out loud
 * instead of leaving a gap somebody fills in with an assumption.
 */
export interface LabourPerformance {
  from: string;
  to: string;
  hoursSold: number;
  labourRevenue: number;
  /** Revenue ÷ hours sold: what an hour realised, as against the posted rate. */
  effectiveLabourRate: number;

  /**
   * Hours actually spent on the work invoiced in this period, from the clock.
   *
   * Counted over the same JOBS as the hours sold, not the same dates — a job
   * clocked in March and invoiced in April belongs to April with all of its
   * time, which is the only way the two can be divided by each other.
   */
  hoursClocked: number;

  /**
   * Hours billed ÷ hours clocked. Null when nothing was clocked, which is a
   * missing measurement and not a productivity of zero.
   */
  productivity: number | null;
  byTechnician: TechnicianLabour[];
  byPayer: LabourByPayer[];
  /** 'Efficiency' — see UnmeasurableLabourFigure on the server. */
  notMeasured: string[];
}

/** Null `technicianUserId` is work invoiced with nobody assigned. Kept, not dropped. */
export interface TechnicianLabour {
  technicianUserId: string | null;
  hoursSold: number;
  revenue: number;
  effectiveLabourRate: number;

  /** Hours this technician clocked on the work invoiced here. */
  hoursClocked: number;

  /** Hours billed over hours clocked. Null when nothing was clocked. */
  productivity: number | null;
}

export interface LabourByPayer {
  payType: ServicePayType;
  hoursSold: number;
  revenue: number;
}

/**
 * What the workshop sold over a period, split by who pays.
 *
 * `partsGrossProfit` is the only gross-profit figure on this report — see
 * PayTypeBucket for why labour and sublet are left as revenue alone.
 */
export interface PayTypeReconciliation {
  from: string;
  to: string;
  totalRevenue: number;
  byPayer: PayTypeBucket[];
}

export interface PayTypeBucket {
  payType: ServicePayType;
  labourRevenue: number;
  partsRevenue: number;
  subletRevenue: number;
  revenue: number;
  partsCost: number;
  partsGrossProfit: number;
  lineCount: number;
  orderCount: number;
}

/**
 * Safety recall campaigns for a car's YEAR, MAKE AND MODEL.
 *
 * Read `appliesToModelNotVehicle` — it is always true, and it is the whole
 * honesty of this shape. The public record carries no note of whether any
 * particular car has had the work done, so a screen that renders these as "this
 * car needs four repairs" is stating something nobody knows.
 */
export interface RecallReport {
  vehicleId: string;
  modelYear: number;
  make: string;
  model: string;
  campaigns: RecallCampaign[];
  appliesToModelNotVehicle: boolean;
}

export interface RecallCampaign {
  campaignNumber: string;
  manufacturer: string;
  component: string;
  summary: string;
  remedy: string;
  reportedOn: string | null;
  /** The regulator's judgement that the car should not be driven. */
  doNotDrive: boolean;
  /** The regulator's judgement that the car should be parked outdoors. */
  parkOutside: boolean;
}

/**
 * What the browser hands to `navigator.credentials.create`. Every byte-valued
 * field arrives base64url encoded, because that is what survives JSON — see
 * `fromBase64Url` in features/auth/webauthn.ts.
 */
export interface PasskeyRegistrationChallenge {
  challengeId: string;
  challenge: string;
  relyingPartyId: string;
  relyingPartyName: string;
  userHandle: string;
  userName: string;
  userDisplayName: string;
  /** Credentials this account already has, so an authenticator is not enrolled twice. */
  alreadyRegistered: string[];
}

/** A sign-in challenge. Carries nothing about any account, deliberately. */
export interface PasskeySignInChallenge {
  challengeId: string;
  challenge: string;
  relyingPartyId: string;
}

/** A registered passkey as its owner sees it. No key material. */
export interface RegisteredPasskey {
  id: string;
  label: string;
  createdAt: string;
  lastUsedAt: string | null;
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
  /**
   * When a reset code was handed out and is still live, or null. On the record
   * so a dealership can SEE that somebody gave out access, not only in the audit
   * trail.
   */
  recoveryIssuedAt: string | null;
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
  /** Every tax charged, each saying where its figure came from. */
  taxLines: TaxLineView[];
  /** The tax added up. Already inside `amountDue`. */
  taxTotal: number;
  /** The address the tax was worked out from. Null when there is no tax. */
  taxedAt: TaxAddressView | null;
  /**
   * Where the car will be registered or garaged. Distinct from the customer's
   * own mailing address, and from `taxedAt` — this is the fact tax is traced to
   * (ADR-024); `taxedAt` is the narrower snapshot copied onto the tax lines once
   * it has been worked out.
   */
  registrationAddress: RegistrationAddressView | null;
  history: DealHistoryEntry[];
}

/**
 * Where a tax figure came from. `EnteredByPerson` is a real answer, not a
 * placeholder — it is what lets a dealership work in a jurisdiction nobody has
 * written a rate pack for, and the screen has to say so rather than presenting
 * a typed figure as if a rate table produced it (ADR-024).
 */
export type TaxProvenance = 'EnteredByPerson' | 'Pack' | 'Vendor';

export interface TaxLineView {
  id: string;
  description: string;
  jurisdiction: string;
  /** What the rate was applied to. */
  basis: number;
  /** A fraction, not a percentage: 0.0625 is six and a quarter percent. */
  rate: number;
  amount: number;
  provenance: TaxProvenance;
  /** The pack that produced this and its version. Null when a person typed it. */
  packId: string | null;
  packVersion: number | null;
}

/** State and county separately, because a US rate depends on both. */
export interface TaxAddressView {
  administrativeArea: string | null;
  county: string | null;
  postalCode: string | null;
  country: string;
}

/**
 * The full postal shape, because this address ends up on registration
 * paperwork and not only on a rate lookup — unlike TaxAddressView.
 */
export interface RegistrationAddressView {
  line1: string;
  line2: string | null;
  city: string;
  administrativeArea: string | null;
  county: string | null;
  postalCode: string | null;
  country: string;
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

/**
 * What one customer owes against one sale or one job.
 *
 * `outstanding` is derived on the server from the payments, never stored, so it
 * cannot drift from the rows underneath it. Treat it as read-only here too:
 * subtracting a payment locally to avoid a round trip is how the two would
 * start disagreeing.
 */
export interface ReceivableSummary {
  id: string;
  rooftopId: string;
  customerId: string;
  customerName: string;
  source: ReceivableSource;
  reference: string;
  amount: number;
  paid: number;
  outstanding: number;
  currency: string;
  billedAt: string;
  /** Zero once settled: "still owed for 40 days" is a claim about the present. */
  daysOutstanding: number;
  isSettled: boolean;
}

export interface ReceivableDetail extends ReceivableSummary {
  payments: PaymentView[];

  /**
   * Credits this bill produced, because somebody paid more than it asked for.
   * Almost always empty.
   *
   * Carried on the bill rather than fetched separately so the counter can say
   * "that is settled, and $59.50 is on their account" in the same breath as
   * taking the money. A credit found ten minutes later on another screen is a
   * credit the customer has already left without.
   */
  creditsRaised: CreditSummary[];
}

/** Money the dealership is holding that belongs to a customer. */
export interface CreditSummary {
  id: string;
  rooftopId: string;
  customerId: string;
  customerName: string;

  /** What was overpaid. Never changes. */
  amount: number;

  /** What has since been applied to a bill or handed back. */
  spent: number;

  /** What the dealership still owes out of this credit. */
  remaining: number;
  currency: string;

  /** The bill that was overpaid. */
  reference: string;
  raisedAt: string;
  isSpent: boolean;
  uses: CreditUseView[];
}

export interface CreditUseView {
  id: string;
  amount: number;
  currency: string;
  kind: CreditUseKind;
  receivableId: string | null;
  usedAt: string;
  note: string | null;
}

/** The two things that can happen to a credit. There is no third. */
export type CreditUseKind = 'AppliedToBill' | 'Refunded';

export interface PaymentView {
  id: string;
  amount: number;
  currency: string;
  method: PaymentMethod;
  receivedAt: string;
  note: string | null;
}

export type ReceivableSource = 'Deal' | 'RepairOrder';

/**
 * How the money arrived. A record of fact, not a routing instruction — nothing
 * here talks to a card terminal.
 *
 * `Finance` is a lender settling a car the customer signed for. It is a method
 * rather than a different kind of debt, because what the dealership is owed does
 * not change with who hands the money over.
 */
export type PaymentMethod =
  | 'Cash'
  | 'Card'
  | 'BankTransfer'
  | 'Cheque'
  | 'Finance'
  | 'CustomerCredit';

/**
 * The methods somebody may CHOOSE at the counter.
 *
 * `CustomerCredit` is deliberately absent. It is a real method and it appears on
 * payments that were settled from a credit, but it is not something a person
 * types in: a credit is put against a bill by its own action, which posts no
 * cash. Offering it here would be offering a way to mark a bill paid with money
 * that never arrived.
 */
export const paymentMethods: PaymentMethod[] = [
  'Cash',
  'Card',
  'BankTransfer',
  'Cheque',
  'Finance',
];

/** How a credit may be handed back. Not from a credit — that is not giving it back. */
export const refundMethods: PaymentMethod[] = ['Cash', 'Card', 'BankTransfer', 'Cheque'];

/**
 * Who owes what, split by how long it has been owed. `currency` applies to
 * every figure in the report — the server refuses to build one across more
 * than one currency rather than total a number that means nothing.
 */
export interface AgeingReport {
  currency: string;
  customers: CustomerAgeing[];
  totals: AgeingBucket;
}

export interface CustomerAgeing {
  customerId: string;
  customerName: string;
  bucket: AgeingBucket;
}

/** What is owed, split by how long it has been owed. */
export interface AgeingBucket {
  current: number;
  days31To60: number;
  days61To90: number;
  over90: number;
  total: number;
}

/** One customer's bills and payments over a period, with a running balance. */
export interface CustomerStatement {
  customerId: string;
  customerName: string;
  currency: string;
  from: string;
  to: string;
  openingBalance: number;
  closingBalance: number;
  lines: StatementLine[];
}

export interface StatementLine {
  date: string;
  kind: 'Invoice' | 'Payment';
  reference: string;
  amount: number;
  balance: number;
}

/**
 * A profit and loss. Departmental gross first, because that is how a dealership
 * is run; overheads and net profit below it.
 */
export interface ProfitAndLoss {
  from: string | null;
  to: string | null;
  currency: string;
  departments: DepartmentResult[];
  totalRevenue: number;
  totalCost: number;
  grossProfit: number;
  expenses: ExpenseLine[];
  totalExpenses: number;
  netProfit: number;
}

export interface ExpenseLine {
  code: string;
  name: string;
  amount: number;
}

/**
 * What the business owns and owes, as at a date.
 *
 * `balances` is not decoration. Assets must equal liabilities plus equity plus
 * what has been earned; if the server says they do not, show that rather than
 * printing a plausible page with a hole in it.
 */
export interface BalanceSheet {
  asAt: string | null;
  currency: string;
  assets: AccountBalance[];
  liabilities: AccountBalance[];
  equity: AccountBalance[];
  totalAssets: number;
  totalLiabilities: number;
  totalEquity: number;
  /** Revenue less every expense, for all time. Its own line: there is no year-end close. */
  earningsToDate: number;
  balances: boolean;
}

/** One line of an entry somebody writes by hand. */
export interface ManualLine {
  accountCode: string;
  debit: number;
  credit: number;
  memo: string | null;
}

/** One line of the chart of accounts, for a picker to label amounts with. */
export interface AccountView {
  id: string;
  code: string;
  name: string;
  kind: 'Asset' | 'Liability' | 'Equity' | 'Revenue' | 'Expense';
}

/** A posted journal entry, as the ledger hands it back. */
export interface JournalEntryDetail {
  id: string;
  rooftopId: string;
  entryDate: string;
  source: string;
  reference: string;
  memo: string;
  currency: string;
  totalDebits: number;
  totalCredits: number;
  lines: JournalLineView[];
}

export interface JournalLineView {
  accountCode: string;
  accountName: string;
  debit: number;
  credit: number;
  memo: string | null;
}

/**
 * One page of a list, and how many rows there are altogether.
 *
 * ONE TYPE FOR EVERY LIST. Every list endpoint returns this shape, so the
 * browser has one contract to read and one component to render it. There was a
 * `LeadPage` here once, for the only list that could be paged; the rest returned
 * bare arrays with a limit and no way past it.
 *
 * `total` is the whole point of a page rather than a list. "Showing the first
 * 50. There may be more" was true and useless; a dealership needs to know
 * whether it is 51 or 5,100, and needs a way to reach them.
 *
 * `offset` and `limit` come back from the SERVER rather than being assumed from
 * what was asked for: the limit is clamped there, so a screen that asked for
 * 10,000 rows is told it got 200.
 */
export interface Page<T> {
  rows: T[];
  total: number;
  offset: number;
  limit: number;
}

/**
 * Which end of the enquiry list matters.
 *
 * Not a presentation choice. The panel headed "Nobody is chasing these" used to
 * take the newest fifty and display them longest-waiting first, so it dropped
 * exactly the rows it existed for. The order has to be part of the query.
 */
export type LeadOrder = 'newest' | 'longestWaiting';
