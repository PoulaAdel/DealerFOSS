// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   DealProducts.test — the F&I menu on a deal.
//
// Usage:
//   npm test.
//
// Coding Instructions:
//   The test that matters most is "the price is editable, and what is typed
//   is what is sent". F&I is negotiated: if a future change made the price
//   read-only or sent the catalogue default instead, the recorded gross would
//   be wrong on every discounted deal and nothing else would notice.

import { render, screen, within } from '../../test/render';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { DealProducts } from './DealProducts';
import { apiCalls, mockApi } from '../../test/setup';
import { setCurrentTenant } from '../../shared/api';
import type { DealDetail, FinanceProductView } from '../../shared/contracts';

const warranty: FinanceProductView = {
  id: 'fp1',
  name: '3-year warranty',
  kind: 'Warranty',
  provider: 'Northgate Underwriting',
  defaultPrice: 1200,
  defaultCost: 700,
  currency: 'USD',
  termMonths: 36,
  termMiles: null,
  isAvailable: true,
};

const gap: FinanceProductView = {
  ...warranty,
  id: 'fp2',
  name: 'GAP cover',
  kind: 'Gap',
  defaultPrice: 500,
  defaultCost: 260,
};

const deal: DealDetail = {
  id: 'd1',
  rooftopId: 'r1',
  status: 'Draft',
  customerId: 'c1',
  customerName: 'Daniel Okafor',
  inventoryUnitId: 'u1',
  stockNumber: 'A1001',
  vehicle: '2021 Toyota RAV4 XLE',
  leadId: null,
  currency: 'USD',
  subtotal: 24000,
  amountDue: 24000,
  tradeIn: null,
  charges: [{ kind: 'VehiclePrice', description: 'The car', amount: 24000 }],
  products: [],
  productGross: 0,
  approvedByUserId: null,
  approvedAt: null,
  termsAreOpen: true,
  taxLines: [],
  taxTotal: 0,
  taxedAt: null,
  registrationAddress: null,
  salespersonUserId: 'u9',
  isApproved: false,
  history: [],
};

function renderProducts(over: Partial<DealDetail> = {}) {
  setCurrentTenant('northgroup');

  return render(<DealProducts deal={{ ...deal, ...over }} onChanged={vi.fn()} />);
}

describe('the finance menu', () => {
  it('offers what can be sold, seeded from the catalogue', async () => {
    mockApi({ '/finance/products': { ok: true, body: [warranty, gap] } });
    renderProducts();

    expect(await screen.findByText('3-year warranty')).toBeVisible();
    expect(screen.getByText('GAP cover')).toBeVisible();
    expect(screen.getByLabelText('Price for 3-year warranty')).toHaveValue('1200');
  });

  it('says plainly when nothing is set up to sell', async () => {
    mockApi({ '/finance/products': { ok: true, body: [] } });
    renderProducts();

    expect(await screen.findByText(/No products are set up to sell/i)).toBeVisible();
  });

  it('seeds from what the deal already sold, not from the catalogue', async () => {
    // Saving replaces the whole set, so an unseeded form would mean ticking one
    // product deleted the rest. Same reasoning as the charges editor.
    mockApi({ '/finance/products': { ok: true, body: [warranty, gap] } });
    renderProducts({
      products: [
        {
          id: 'dp1',
          financeProductId: 'fp1',
          name: '3-year warranty',
          provider: 'Northgate Underwriting',
          price: 900,
          cost: 700,
          gross: 200,
          termMonths: 36,
          termMiles: null,
          isCancelled: false,
          cancelledAt: null,
          refundAmount: null,
          cancellationReason: null,
        },
      ],
      productGross: 200,
    });

    expect(await screen.findByLabelText('Price for 3-year warranty')).toHaveValue('900');
    expect(screen.getByLabelText('Sell 3-year warranty')).toBeChecked();
    expect(screen.getByLabelText('Sell GAP cover')).not.toBeChecked();
  });

  it('the price is editable, and what is typed is what is sent', async () => {
    mockApi({
      '/finance/products': { ok: true, body: [warranty] },
      '/deals/d1/products': { ok: true, body: deal },
    });
    renderProducts();

    await userEvent.click(await screen.findByLabelText('Sell 3-year warranty'));

    const price = screen.getByLabelText('Price for 3-year warranty');
    await userEvent.clear(price);
    await userEvent.type(price, '950');

    await userEvent.click(screen.getByRole('button', { name: 'Save what is being sold' }));

    const call = apiCalls().find((c) => c.path === '/deals/d1/products');
    const body = JSON.parse(String(call?.init?.body)) as { products: { price: number }[] };

    expect(body.products).toHaveLength(1);
    expect(body.products[0]!.price).toBe(950);
  });

  it('sends nothing for a product left unticked', async () => {
    mockApi({
      '/finance/products': { ok: true, body: [warranty, gap] },
      '/deals/d1/products': { ok: true, body: deal },
    });
    renderProducts();

    await userEvent.click(await screen.findByLabelText('Sell GAP cover'));
    await userEvent.click(screen.getByRole('button', { name: 'Save what is being sold' }));

    const call = apiCalls().find((c) => c.path === '/deals/d1/products');
    const body = JSON.parse(String(call?.init?.body)) as { products: { financeProductId: string }[] };

    expect(body.products).toHaveLength(1);
    expect(body.products[0]!.financeProductId).toBe('fp2');
  });

  it('shows the gross as the price is changed', async () => {
    mockApi({ '/finance/products': { ok: true, body: [warranty] } });
    renderProducts();

    await userEvent.click(await screen.findByLabelText('Sell 3-year warranty'));

    expect(screen.getByText(/making \$500\.00/)).toBeVisible();

    const price = screen.getByLabelText('Price for 3-year warranty');
    await userEvent.clear(price);
    await userEvent.type(price, '900');

    expect(screen.getByText(/making \$200\.00/)).toBeVisible();
  });

  it('keeps a withdrawn product visible when the deal already sold it', async () => {
    // It is absent from the menu the API returns. Dropping it from the form would
    // silently unsell it on the next save.
    mockApi({ '/finance/products': { ok: true, body: [gap] } });
    renderProducts({
      products: [
        {
          id: 'dp1',
          financeProductId: 'fp9',
          name: 'Discontinued cover',
          provider: null,
          price: 400,
          cost: 200,
          gross: 200,
          termMonths: null,
          termMiles: null,
          isCancelled: false,
          cancelledAt: null,
          refundAmount: null,
          cancellationReason: null,
        },
      ],
      productGross: 200,
    });

    const row = (await screen.findByText('Discontinued cover')).closest('tr')!;
    expect(within(row).getByText(/no longer offered/)).toBeVisible();
    expect(screen.getByLabelText('Sell Discontinued cover')).toBeChecked();
  });

  it('shows the server refusal rather than predicting it', async () => {
    mockApi({
      '/finance/products': { ok: true, body: [warranty] },
      '/deals/d1/products': {
        ok: false,
        status: 409,
        code: 'deals.terms_frozen',
        detail: 'A Submitted deal is frozen.',
      },
    });
    renderProducts();

    await userEvent.click(await screen.findByLabelText('Sell 3-year warranty'));
    await userEvent.click(screen.getByRole('button', { name: 'Save what is being sold' }));

    expect(await screen.findByText(/is frozen/i)).toBeVisible();
  });
});
