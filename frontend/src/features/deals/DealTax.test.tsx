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
  financing: null,
  availableMoves: ['Submitted', 'Cancelled'],
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
  it('offers both ways of getting a figure, and says each line records its own source', () => {
    renderTax();

    // The lede used to say nothing worked the tax out. That stopped being true
    // on 2026-11-04, and a screen telling people to type what a rate table
    // would give them is worse than no instruction at all.
    expect(screen.getByText(/Work it out from the rate table in use, or type what applies/i))
      .toBeVisible();
    expect(screen.getByRole('button', { name: 'Work the tax out' })).toBeVisible();
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

/**
 * The three numbers have to agree (2026-09-19).
 *
 * A person used to type the basis, the rate AND the answer, and nothing checked
 * the three against each other — so a deal could carry a tax figure its own
 * basis and rate contradict. That figure is the one that reaches the invoice
 * and the ledger.
 *
 * The escape hatch stays: `EnteredByPerson` exists so an unsupported
 * jurisdiction is a label rather than a blocker, and a capped or tiered tax is
 * not basis times rate. What ends is the SILENT disagreement.
 */
describe('the tax arithmetic', () => {
  it('works the amount out from the basis and the rate', async () => {
    renderTax();
    await userEvent.click(screen.getByRole('button', { name: 'Add a tax line' }));

    await userEvent.type(screen.getByLabelText('Amount taxed on line 1'), '33000');
    await userEvent.type(screen.getByLabelText('Rate on line 1, as a percentage'), '6.25');

    expect(screen.getByLabelText('Tax charged on line 1')).toHaveValue('2062.50');
  });

  it('follows the basis when it changes, rather than leaving a stale figure', async () => {
    renderTax();
    await userEvent.click(screen.getByRole('button', { name: 'Add a tax line' }));

    await userEvent.type(screen.getByLabelText('Amount taxed on line 1'), '1000');
    await userEvent.type(screen.getByLabelText('Rate on line 1, as a percentage'), '10');
    expect(screen.getByLabelText('Tax charged on line 1')).toHaveValue('100.00');

    await userEvent.type(screen.getByLabelText('Amount taxed on line 1'), '0');
    expect(screen.getByLabelText('Tax charged on line 1')).toHaveValue('1000.00');
  });

  it('still lets a person type their own figure, for a tax that is not a multiplication', async () => {
    renderTax();
    await userEvent.click(screen.getByRole('button', { name: 'Add a tax line' }));

    await userEvent.type(screen.getByLabelText('Amount taxed on line 1'), '33000');
    await userEvent.type(screen.getByLabelText('Rate on line 1, as a percentage'), '6.25');

    const amount = screen.getByLabelText('Tax charged on line 1');
    await userEvent.clear(amount);
    await userEvent.type(amount, '500');

    expect(amount).toHaveValue('500');
  });

  it('says so when an overridden figure contradicts its own basis and rate', async () => {
    // The point. A capped tax is legitimate; a silent contradiction is not.
    renderTax();
    await userEvent.click(screen.getByRole('button', { name: 'Add a tax line' }));

    await userEvent.type(screen.getByLabelText('Amount taxed on line 1'), '33000');
    await userEvent.type(screen.getByLabelText('Rate on line 1, as a percentage'), '6.25');

    const amount = screen.getByLabelText('Tax charged on line 1');
    await userEvent.clear(amount);
    await userEvent.type(amount, '500');

    expect(screen.getByText(/does not match .* which comes to .*2,062\.50/)).toBeVisible();
  });

  it('says nothing when the three agree', async () => {
    // A note under every row would be read as decoration within a week.
    renderTax();
    await userEvent.click(screen.getByRole('button', { name: 'Add a tax line' }));

    await userEvent.type(screen.getByLabelText('Amount taxed on line 1'), '33000');
    await userEvent.type(screen.getByLabelText('Rate on line 1, as a percentage'), '6.25');

    expect(screen.queryByText(/does not match/)).not.toBeInTheDocument();
  });

  it('does not rewrite a figure already saved on the deal', async () => {
    // A saved line is somebody's settled figure — often a capped one. Editing
    // the basis must not silently overwrite it.
    renderTax({
      taxLines: [{
        id: 't1', description: 'Capped tax', jurisdiction: 'IL',
        basis: 33000, rate: 0.0625, amount: 300,
        provenance: 'EnteredByPerson', packId: null, packVersion: null,
      }],
    });

    await userEvent.type(screen.getByLabelText('Amount taxed on line 1'), '0');

    expect(screen.getByLabelText('Tax charged on line 1')).toHaveValue('300');
  });
});

/**
 * Working the tax out from a rate table (2026-11-04).
 *
 * The server is the only thing allowed to sign a pack's name to a figure, so
 * this screen's whole job here is to ask, redraw, and tell the truth about what
 * came back. The three unresolved answers are ORDINARY, not errors — each needs
 * something different done about it, which is why each gets its own sentence.
 */
describe('working the tax out from the rate table', () => {
  const packLine: TaxLineView = {
    id: 'p1',
    description: 'Indiana sales tax',
    jurisdiction: 'US-IN',
    basis: 15500,
    rate: 0.07,
    amount: 1085,
    provenance: 'Pack',
    packId: 'us-in',
    packVersion: 2,
  };

  it('draws what the server worked out, and names the pack on the row', async () => {
    mockApi({
      '/deals/d1/tax/from-pack': {
        ok: true,
        body: {
          outcome: 'Resolved',
          packId: 'us-in',
          packVersion: 2,
          deal: {
            ...deal,
            taxLines: [packLine],
            taxTotal: 1085,
            taxedAt: {
              administrativeArea: 'IN',
              county: null,
              postalCode: '46204',
              country: 'US',
            },
          },
        },
      },
    });

    renderTax();
    await userEvent.click(screen.getByRole('button', { name: 'Work the tax out' }));

    expect(screen.getByLabelText('Tax charged on line 1')).toHaveValue('1085');
    // The chip is in the EDITABLE table, not only once the deal is frozen.
    expect(screen.getByText('us-in v2')).toBeVisible();
    // The address came back with it, so nobody has to retype what decided the rate.
    expect(screen.getByLabelText('State or region')).toHaveValue('IN');
  });

  it.each([
    ['NoAddress', /Add the address the car will be registered at first/i],
    ['NoPackEnabled', /No rate table is in use for that country/i],
    ['NoRateForAddress', /has nothing for that address/i],
  ])('says what happened when it resolves nothing: %s', async (outcome, expected) => {
    mockApi({
      '/deals/d1/tax/from-pack': {
        ok: true,
        body: { outcome, packId: null, packVersion: null, deal },
      },
    });

    renderTax();
    await userEvent.click(screen.getByRole('button', { name: 'Work the tax out' }));

    expect(screen.getByText(expected)).toBeVisible();
  });

  it('keeps what a person typed when the rate table has no answer', async () => {
    // The deal comes back untouched, so asking and being told "no" must not
    // throw away work somebody had already done by hand.
    mockApi({
      '/deals/d1/tax/from-pack': {
        ok: true,
        body: { outcome: 'NoPackEnabled', packId: null, packVersion: null, deal },
      },
    });

    renderTax();
    await userEvent.click(screen.getByRole('button', { name: 'Add a tax line' }));
    await userEvent.type(screen.getByLabelText('Tax on line 1'), 'VAT');

    await userEvent.click(screen.getByRole('button', { name: 'Work the tax out' }));

    expect(screen.getByLabelText('Tax on line 1')).toHaveValue('VAT');
  });

  it('sends an untouched pack row back verbatim, still claiming the pack', async () => {
    // How editing one row leaves the others' authorship alone: this editor
    // replaces the whole set, and the server accepts a Pack line only if it is
    // field-for-field what it stored.
    mockApi({ '/deals/d1/tax': { ok: true, body: deal } });
    renderTax({ taxLines: [packLine], taxTotal: 1085 });

    await userEvent.click(screen.getByRole('button', { name: 'Save the tax' }));

    const sent = apiCalls().find((call) => call.path === '/deals/d1/tax');
    const body = JSON.parse(sent!.init!.body as string) as {
      lines: { provenance: string; packId?: string; packVersion?: number; id?: string; rate: number }[];
    };

    const first = body.lines[0];
    expect(first).toBeDefined();
    expect(first!.provenance).toBe('Pack');
    expect(first!.packId).toBe('us-in');
    expect(first!.packVersion).toBe(2);
    expect(first!.id).toBe('p1');
    // Verbatim from the saved line, not re-parsed out of the percentage box —
    // a round trip through "7" can land one unit in the last place away from
    // the stored decimal, and the server compares exactly.
    expect(first!.rate).toBe(0.07);
  });

  it('becomes the person’s the moment they edit it, on screen and on the wire', async () => {
    mockApi({ '/deals/d1/tax': { ok: true, body: deal } });
    renderTax({ taxLines: [packLine], taxTotal: 1085 });

    expect(screen.getByText('us-in v2')).toBeVisible();

    const amount = screen.getByLabelText('Tax charged on line 1');
    await userEvent.clear(amount);
    await userEvent.type(amount, '900');

    // The chip goes before the save does, which is the screen telling the truth
    // about who is answerable for the figure.
    expect(screen.queryByText('us-in v2')).not.toBeInTheDocument();
    expect(screen.getByText('a person entered it')).toBeVisible();

    await userEvent.click(screen.getByRole('button', { name: 'Save the tax' }));

    const sent = apiCalls().find((call) => call.path === '/deals/d1/tax');
    const body = JSON.parse(sent!.init!.body as string) as {
      lines: { provenance: string; packId?: string | null; amount: number }[];
    };

    const first = body.lines[0];
    expect(first!.provenance).toBe('EnteredByPerson');
    expect(first!.packId).toBeUndefined();
    expect(first!.amount).toBe(900);
  });
});
