// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   DealTax.test — the tax band on a deal.
//
// Usage:
//   npm test.
//
// Coding Instructions:
//   TWO TESTS HERE GUARD THINGS THAT WOULD BE WRONG QUIETLY.
//
//   The rate is typed as a percentage and sent as a fraction. Anybody entering
//   a tax rate is reading "6.25%" off a table, and a change that sent 6.25
//   instead of 0.0625 would tax a deal a hundred times over — while every
//   screen still looked right, because the amount is typed separately.
//
//   A frozen deal must still SAY where each figure came from. That is the
//   question somebody asks months later with a customer on the phone, and it is
//   the only moment the provenance column really earns its width.

import { render, screen } from '../../test/render';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { DealTax, SoldTax } from './DealTax';
import { apiCalls, mockApi } from '../../test/setup';
import { setCurrentTenant } from '../../shared/api';
import type { DealDetail, TaxLineView } from '../../shared/contracts';

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

const typedLine: TaxLineView = {
  id: 't1',
  description: 'Sales tax',
  jurisdiction: 'US-IL-SANGAMON',
  basis: 24000,
  rate: 0.0625,
  amount: 1500,
  provenance: 'EnteredByPerson',
  packId: null,
  packVersion: null,
};

function renderTax(over: Partial<DealDetail> = {}) {
  setCurrentTenant('northgroup');
  return render(<DealTax deal={{ ...deal, ...over }} onChanged={vi.fn()} />);
}

describe('entering the tax on a deal', () => {
  it('says plainly that nothing works it out yet', () => {
    renderTax();

    // The alternative is a screen that presents a typed number with the same
    // authority as one a rate table produced.
    expect(screen.getByText(/Nothing works this out for you yet/i)).toBeVisible();
    expect(screen.getByText(/No tax on this deal yet/i)).toBeVisible();
  });

  it('asks for the county separately from the state', () => {
    // A US rate depends on both. Folding them into one box loses the thing that
    // decides the rate, which is the gap closed on 2026-09-05.
    renderTax();

    expect(screen.getByLabelText('State or region')).toBeVisible();
    expect(screen.getByLabelText('County')).toBeVisible();
  });

  it('seeds from the tax the deal already carries', () => {
    // Saving replaces the whole set, so an unseeded form would mean adding one
    // line deleted the rest.
    renderTax({
      taxLines: [typedLine],
      taxTotal: 1500,
      taxedAt: { administrativeArea: 'IL', county: 'Sangamon', postalCode: '62704', country: 'US' },
    });

    expect(screen.getByLabelText('Tax on line 1')).toHaveValue('Sales tax');
    expect(screen.getByLabelText('County')).toHaveValue('Sangamon');
    // Shown as a percentage, because that is how it is read off a table.
    expect(screen.getByLabelText('Rate on line 1, as a percentage')).toHaveValue('6.25');
  });

  it('sends the rate as a fraction after it is typed as a percentage', async () => {
    // The hundredfold bug. Every visible figure stays right if this breaks,
    // because the amount is typed separately — only the stored rate is wrong.
    mockApi({ '/deals/d1/tax': { ok: true, body: deal } });
    renderTax();

    await userEvent.click(screen.getByRole('button', { name: 'Add a tax line' }));
    await userEvent.type(screen.getByLabelText('Tax on line 1'), 'Sales tax');
    await userEvent.type(screen.getByLabelText('Jurisdiction on line 1'), 'US-IL');
    await userEvent.type(screen.getByLabelText('Rate on line 1, as a percentage'), '6.25');
    await userEvent.type(screen.getByLabelText('Tax charged on line 1'), '1500');
    await userEvent.type(screen.getByLabelText('State or region'), 'IL');
    await userEvent.type(screen.getByLabelText('Country'), 'US');

    await userEvent.click(screen.getByRole('button', { name: 'Save the tax' }));

    const sent = apiCalls().find((call) => call.path === '/deals/d1/tax');
    expect(sent).toBeDefined();

    const body = JSON.parse(sent!.init!.body as string) as {
      lines: { rate: number; provenance: string }[];
    };
    const first = body.lines[0];
    expect(first).toBeDefined();
    expect(first!.rate).toBe(0.0625);
    expect(first!.provenance).toBe('EnteredByPerson');
  });

  it('does not send a row somebody started and abandoned', async () => {
    mockApi({ '/deals/d1/tax': { ok: true, body: deal } });
    renderTax();

    await userEvent.click(screen.getByRole('button', { name: 'Add a tax line' }));
    await userEvent.click(screen.getByRole('button', { name: 'Save the tax' }));

    const sent = apiCalls().find((call) => call.path === '/deals/d1/tax');
    const body = JSON.parse(sent!.init!.body as string) as { lines: unknown[]; taxedAt: unknown };

    expect(body.lines).toHaveLength(0);
    expect(body.taxedAt).toBeNull(
      // An address with no tax attached is a leftover, not a record.
    );
  });
});

describe('the tax once the deal is frozen', () => {
  it('still says where each figure came from', () => {
    setCurrentTenant('northgroup');
    render(
      <SoldTax
        deal={{ ...deal, termsAreOpen: false, taxLines: [typedLine], taxTotal: 1500 }}
      />,
    );

    expect(screen.getByText('a person entered it')).toBeVisible();
  });

  it('names the pack and version when a rate table produced it', () => {
    setCurrentTenant('northgroup');
    render(
      <SoldTax
        deal={{
          ...deal,
          termsAreOpen: false,
          taxTotal: 1500,
          taxLines: [{ ...typedLine, provenance: 'Pack', packId: 'sst-il', packVersion: 3 }],
        }}
      />,
    );

    expect(screen.getByText('sst-il v3')).toBeVisible();
  });
});
