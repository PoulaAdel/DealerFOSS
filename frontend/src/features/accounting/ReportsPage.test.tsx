// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ReportsPage.test — the two reports a dealer principal reads.
//
// Usage:
//   npm test
//
// Coding Instructions:
//   The assertion that matters is the alarming one: a balance sheet that does
//   not balance must SAY SO, loudly, rather than printing a plausible page with
//   a hole in it. If that ever becomes a quiet footnote, this is what fails.

import { render, screen } from '../../test/render';
import { describe, expect, it } from 'vitest';
import { ReportsPage } from './ReportsPage';
import { mockApi, mockApiPending } from '../../test/setup';
import { setCurrentTenant } from '../../shared/api';
import { signedInAs } from '../../test/session';
import { Permission } from '../../shared/permissions';
import type { BalanceSheet, ProfitAndLoss } from '../../shared/contracts';

const profit: ProfitAndLoss = {
  from: '2026-09-01',
  to: '2026-09-30',
  currency: 'USD',
  departments: [
    // Vehicles gross is 45,000 and not 40,000 on purpose: 40,000 is also the
    // total overheads, and a fixture where two different figures share a value
    // cannot tell you which one the screen put where.
    { name: 'Vehicles', revenue: 200000, cost: 155000, gross: 45000, margin: 0.225 },
    { name: 'Service', revenue: 20000, cost: 5000, gross: 15000, margin: 0.75 },
  ],
  totalRevenue: 220000,
  totalCost: 160000,
  grossProfit: 60000,
  expenses: [
    { code: '6000', name: 'Wages and salaries', amount: 30000 },
    { code: '6100', name: 'Rent and premises', amount: 8000 },
    { code: '6200', name: 'Advertising', amount: 0 },
    { code: '6300', name: 'Floorplan interest', amount: 2000 },
    { code: '6900', name: 'Other operating expenses', amount: 0 },
  ],
  totalExpenses: 40000,
  netProfit: 20000,
  priorYear: null,
};

const sheet: BalanceSheet = {
  asAt: '2026-09-30',
  currency: 'USD',
  assets: [{ code: '1000', name: 'Cash', kind: 'Asset', debits: 90000, credits: 0, balance: 90000 }],
  liabilities: [
    { code: '2000', name: 'Floorplan payable', kind: 'Liability', debits: 0, credits: 25000, balance: 25000 },
  ],
  equity: [
    { code: '3000', name: "Owners' capital", kind: 'Equity', debits: 0, credits: 50000, balance: 50000 },
  ],
  totalAssets: 90000,
  totalLiabilities: 25000,
  totalEquity: 50000,
  earningsToDate: 15000,
  balances: true,
};

function mockBoth(overrides: Partial<BalanceSheet> = {}) {
  // SessionProvider asks /auth/me only once a dealership is chosen.
  setCurrentTenant('northgroup');

  mockApi({
    '/accounting/profit-and-loss': { ok: true, body: profit },
    '/accounting/balance-sheet': { ok: true, body: { ...sheet, ...overrides } },
    // Signed in as somebody who may see gross (ADR-029) — without this the
    // whole P&L panel correctly withholds itself, which is its own test below.
    '/auth/me': signedInAs(),
  });
}

describe('the month reports', () => {
  it('says it is working before the figures arrive', () => {
    mockApiPending();
    render(<ReportsPage />);

    expect(screen.getByText('Working out the figures…')).toBeVisible();
  });

  it('takes overheads off the gross and shows what is left', async () => {
    mockBoth();
    render(<ReportsPage />);

    expect(await screen.findByText('Profit and loss')).toBeVisible();
    expect(screen.getByText('$60,000.00')).toBeVisible();
    expect(screen.getByText('$40,000.00')).toBeVisible();
    expect(screen.getByText(/Net profit: \$20,000\.00/)).toBeVisible();
  });

  it('lists every overhead account, including the ones at nothing', async () => {
    // A missing figure and no spending must not look the same.
    mockBoth();
    render(<ReportsPage />);

    expect(await screen.findByText(/Advertising/)).toBeVisible();
    expect(screen.getByText(/Other operating expenses/)).toBeVisible();
  });

  it('says the balance sheet balances when it does', async () => {
    mockBoth();
    render(<ReportsPage />);

    expect(await screen.findByText(/It balances/)).toBeVisible();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('shouts when the balance sheet does not balance', async () => {
    // Something has been posted the report cannot classify. A plausible page
    // with a hole in it is worse than an alarming one.
    mockBoth({ balances: false, totalAssets: 91000 });
    render(<ReportsPage />);

    const alarm = await screen.findByRole('alert');
    expect(alarm).toBeVisible();
    expect(alarm).toHaveTextContent(/does not balance/);
  });

  it('keeps what has been earned as its own line', async () => {
    // There is no year-end close, so folding it into capital would assert a
    // process nobody has run.
    mockBoth();
    render(<ReportsPage />);

    expect(await screen.findByText(/Earned since the beginning/)).toBeVisible();
    expect(screen.getByText(/no year has been closed yet/)).toBeVisible();
  });

  it('withholds the whole profit and loss from a caller without Profitability.Read', async () => {
    // ADR-029. The record right is untouched — this caller may read the
    // report, and the server still sends `profit`, but its cost/gross/net
    // fields are null. A page with only a revenue column would not be a
    // smaller P&L, it would be a confusing one, so the whole panel says it is
    // withheld instead.
    setCurrentTenant('northgroup');

    mockApi({
      '/accounting/profit-and-loss': {
        ok: true,
        body: {
          ...profit,
          departments: profit.departments.map((d) => ({ ...d, cost: null, gross: null, margin: null })),
          totalCost: null,
          grossProfit: null,
          netProfit: null,
        },
      },
      '/accounting/balance-sheet': { ok: true, body: sheet },
      '/auth/me': signedInAs(
        Object.values(Permission).filter((p) => p !== Permission.ProfitabilityRead),
      ),
    });
    render(<ReportsPage />);

    expect(await screen.findByText(/store made is not yours to see/)).toBeVisible();
    expect(screen.queryByText(/Net profit/)).not.toBeInTheDocument();
    expect(screen.queryByText('$60,000.00')).not.toBeInTheDocument();

    // The balance sheet is a different report, unaffected.
    expect(screen.getByText(/It balances/)).toBeVisible();
  });

  it('explains a refusal rather than showing an empty report', async () => {
    mockApi({
      '/accounting/profit-and-loss': {
        ok: false, status: 403, code: 'accounting.forbidden', detail: 'No.',
      },
      '/accounting/balance-sheet': {
        ok: false, status: 403, code: 'accounting.forbidden', detail: 'No.',
      },
    });
    render(<ReportsPage />);

    expect(await screen.findByText(/do not have access to the figures/)).toBeVisible();
  });
});
