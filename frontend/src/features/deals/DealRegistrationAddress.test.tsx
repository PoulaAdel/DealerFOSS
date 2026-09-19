// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   DealRegistrationAddress.test — the address a car will be registered or
//   garaged at.
//
// Usage:
//   npm test.
//
// Coding Instructions:
//   The point worth guarding: this is a fact about the DEAL, not the
//   customer, and the form must not silently start acting like the
//   customer's own address — see the file it tests for why (ADR-024).

import { render, screen } from '../../test/render';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { DealRegistrationAddress, SoldRegistrationAddress } from './DealRegistrationAddress';
import { apiCalls, mockApi } from '../../test/setup';
import { setCurrentTenant } from '../../shared/api';
import type { DealDetail } from '../../shared/contracts';

const deal: DealDetail = {
  id: 'd1',
  rooftopId: 'r1',
  status: 'Draft',
  customerId: 'c1',
  customerName: 'Marisol Alvarez',
  inventoryUnitId: 'u1',
  stockNumber: 'A1001',
  vehicle: '2021 Toyota RAV4',
  leadId: null,
  currency: 'USD',
  subtotal: 24000,
  amountDue: 24000,
  tradeIn: null,
  charges: [],
  products: [],
  productGross: 0,
  salespersonUserId: 's1',
  approvedByUserId: null,
  approvedAt: null,
  termsAreOpen: true,
  taxLines: [],
  taxTotal: 0,
  taxedAt: null,
  registrationAddress: null,
  history: [],
  isApproved: false,
};

function renderAddress(over: Partial<DealDetail> = {}) {
  setCurrentTenant('northgroup');
  return render(<DealRegistrationAddress deal={{ ...deal, ...over }} onChanged={vi.fn()} />);
}

describe('the deal registration address', () => {
  it('seeds from the address already on the deal', () => {
    renderAddress({
      registrationAddress: {
        line1: '18 Kestrel Way',
        line2: null,
        city: 'Springfield',
        administrativeArea: 'IL',
        county: 'Sangamon',
        postalCode: '62704',
        country: 'US',
      },
    });

    expect(screen.getByLabelText('Address line 1')).toHaveValue('18 Kestrel Way');
    expect(screen.getByLabelText('County')).toHaveValue('Sangamon');
  });

  it('sends the address as its own record, not folded into the deal terms', async () => {
    mockApi({ '/deals/d1/registration-address': { ok: true, body: deal } });
    renderAddress();

    await userEvent.type(screen.getByLabelText('Address line 1'), '18 Kestrel Way');
    await userEvent.type(screen.getByLabelText('City'), 'Springfield');
    await userEvent.type(screen.getByLabelText('State or region'), 'IL');
    await userEvent.type(screen.getByLabelText('Country'), 'US');

    await userEvent.click(screen.getByRole('button', { name: 'Save the address' }));

    const sent = apiCalls().find((call) => call.path === '/deals/d1/registration-address');
    expect(sent).toBeDefined();

    const body = JSON.parse(sent!.init!.body as string) as {
      address: { line1: string; city: string; administrativeArea: string; country: string };
    };
    expect(body.address.line1).toBe('18 Kestrel Way');
    expect(body.address.city).toBe('Springfield');
    expect(body.address.country).toBe('US');
  });

  it('offers to clear the address only once one is on file', () => {
    renderAddress();
    expect(screen.queryByRole('button', { name: 'Clear the address' })).not.toBeInTheDocument();

    renderAddress({
      registrationAddress: {
        line1: '18 Kestrel Way',
        line2: null,
        city: 'Springfield',
        administrativeArea: 'IL',
        county: null,
        postalCode: null,
        country: 'US',
      },
    });
    expect(screen.getByRole('button', { name: 'Clear the address' })).toBeVisible();
  });

  it('clearing sends null rather than a set of blank fields', async () => {
    mockApi({ '/deals/d1/registration-address': { ok: true, body: deal } });
    renderAddress({
      registrationAddress: {
        line1: '18 Kestrel Way',
        line2: null,
        city: 'Springfield',
        administrativeArea: 'IL',
        county: null,
        postalCode: null,
        country: 'US',
      },
    });

    await userEvent.click(screen.getByRole('button', { name: 'Clear the address' }));

    const sent = apiCalls().find((call) => call.path === '/deals/d1/registration-address');
    const body = JSON.parse(sent!.init!.body as string) as { address: unknown };
    expect(body.address).toBeNull();
  });
});

describe('the registration address once the deal is frozen', () => {
  it('is read-only and carries no editor', () => {
    setCurrentTenant('northgroup');
    render(
      <SoldRegistrationAddress
        deal={{
          ...deal,
          termsAreOpen: false,
          registrationAddress: {
            line1: '18 Kestrel Way',
            line2: null,
            city: 'Springfield',
            administrativeArea: 'IL',
            county: 'Sangamon',
            postalCode: '62704',
            country: 'US',
          },
        }}
      />,
    );

    expect(screen.getByText(/18 Kestrel Way/)).toBeVisible();
    expect(screen.queryByLabelText('Address line 1')).not.toBeInTheDocument();
  });

  it('shows nothing when no address was ever recorded', () => {
    setCurrentTenant('northgroup');
    const { container } = render(
      <SoldRegistrationAddress deal={{ ...deal, termsAreOpen: false }} />,
    );

    expect(container).toBeEmptyDOMElement();
  });
});
