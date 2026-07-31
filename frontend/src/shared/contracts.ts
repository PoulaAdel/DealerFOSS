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

export const inventoryStatuses: InventoryStatus[] = [
  'Incoming',
  'Reconditioning',
  'Available',
  'OnHold',
  'Sold',
  'Removed',
];
