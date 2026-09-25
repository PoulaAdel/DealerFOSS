// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   DealFinancing.test — the finance structure band, and the two properties that
//   would be expensive to get wrong.
//
// Usage:
//   npm test.
//
// Coding Instructions:
//   THE PAYMENT IS NOT COMPUTED IN THE BROWSER, so none of these tests asserts
//   an amortisation. They assert that what the server sent is what a person
//   reads, and that the rate a person TYPES reaches the wire as a fraction. The
//   arithmetic itself is proved in DealFinancingTests on the server, where it
//   lives.
//
//   The second property is that the cash down is not treated as money off. A
//   band that quietly subtracted it would look right on screen and would put the
//   deal's own column out by the down payment.

import { render, screen } from '../../test/render';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { DealFinancing, SoldFinancing } from './DealFinancing';
import { apiCalls, mockApi } from '../../test/setup';
import { setCurrentTenant } from '../../shared/api';
import type { DealDetail, DealFinancingView } from '../../shared/contracts';

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
  history: [],
  isApproved: false,
};

/** As the server sends it: $20,000 financed at 6.49% over 60 months. */
const financed: DealFinancingView = {
  lender: 'Ally Financial',
  downPayment: 4000,
  annualPercentageRate: 0.0649,
  termMonths: 60,
  amountFinanced: 20000,
  monthlyPayment: 391.23,
  finalPayment: 391.06,
  totalOfPayments: 23473.53,
  financeCharge: 3473.53,
};

function renderFinancing(over: Partial<DealDetail> = {}) {
  setCurrentTenant('northgroup');
  return render(<DealFinancing deal={{ ...deal, ...over }} onChanged={vi.fn()} />);
}

describe('the financing band', () => {
  it('says a deal is a cash deal when nothing is financed', () => {
    renderFinancing();

    expect(screen.getByText(/cash deal/i)).toBeVisible();
  });

  it('reads the monthly payment the server worked out', () => {
    renderFinancing({ financing: financed });

    expect(screen.getByText('$391.23')).toBeVisible();
    expect(screen.getByText('$20,000.00')).toBeVisible();
    expect(screen.getByText('$3,473.53')).toBeVisible();
  });

  it('shows the final payment only when it differs from the others', () => {
    const { unmount } = renderFinancing({ financing: financed });
    expect(screen.getByText(/final payment/i)).toBeVisible();
    unmount();

    // A 0% deal that divides evenly has nothing to say here, and a row inviting
    // somebody to spot a difference that is not there is worse than no row.
    renderFinancing({
      financing: {
        ...financed,
        annualPercentageRate: 0,
        termMonths: 40,
        monthlyPayment: 500,
        finalPayment: 500,
        totalOfPayments: 20000,
        financeCharge: 0,
      },
    });

    expect(screen.queryByText(/final payment/i)).toBeNull();
  });

  it('says what the customer still owes, so the cash down is not read as a discount', () => {
    // The property that matters most on this band. The down payment is how the
    // customer pays, not money off: the amount due is unchanged at 24,000 even
    // though 4,000 was put down.
    renderFinancing({ financing: financed });

    expect(screen.getByText(/still \$24,000\.00/)).toBeVisible();
  });

  it('sends the rate as a fraction after a person types a percentage', async () => {
    // The rate sheet says 6.49 and the contract carries 0.0649, the same
    // convention as a tax rate. The conversion happens once, here.
    mockApi({ '/deals/d1/financing': { ok: true, body: { ...deal, financing: financed } } });
    renderFinancing();

    await userEvent.type(screen.getByLabelText(/annual percentage rate/i), '6.49');
    await userEvent.type(screen.getByLabelText(/cash down/i), '4000');
    await userEvent.type(screen.getByLabelText(/term in months/i), '60');
    await userEvent.type(screen.getByLabelText(/finance provider/i), 'Ally Financial');

    await userEvent.click(screen.getByRole('button', { name: /finance this deal/i }));

    const sent = apiCalls().find((c) => c.path === '/deals/d1/financing');

    expect(JSON.parse(sent!.init!.body as string)).toEqual({
      financing: {
        lender: 'Ally Financial',
        downPayment: 4000,
        annualPercentageRate: 0.0649,
        termMonths: 60,
      },
    });
  });

  it('turns the deal back into a cash deal by sending nothing', async () => {
    mockApi({ '/deals/d1/financing': { ok: true, body: { ...deal, financing: null } } });
    renderFinancing({ financing: financed });

    await userEvent.click(screen.getByRole('button', { name: /make it a cash deal/i }));

    const sent = apiCalls().find((c) => c.path === '/deals/d1/financing');
    expect(JSON.parse(sent!.init!.body as string)).toEqual({ financing: null });
  });

  it('says so in words when a reprice has left nothing to finance', () => {
    // The server sends the structure with no payment on it, because the cash
    // down now covers the whole total. A zero payment would read as a free car.
    renderFinancing({
      amountDue: 3000,
      financing: {
        ...financed,
        amountFinanced: -1000,
        monthlyPayment: null,
        finalPayment: null,
        totalOfPayments: null,
        financeCharge: null,
      },
    });

    expect(screen.getByText(/nothing to finance/i)).toBeVisible();
  });

  it('shows the frozen terms read-only once the deal has been submitted', () => {
    setCurrentTenant('northgroup');
    render(<SoldFinancing deal={{ ...deal, status: 'Submitted', termsAreOpen: false, financing: financed }} />);

    expect(screen.getByText('$391.23')).toBeVisible();

    // Read-only: there is nothing here to save or clear.
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('renders nothing at all for a frozen cash deal', () => {
    setCurrentTenant('northgroup');
    const { container } = render(
      <SoldFinancing deal={{ ...deal, status: 'Submitted', termsAreOpen: false }} />,
    );

    expect(container).toBeEmptyDOMElement();
  });

  it('prints the finance company name as the dealership typed it', () => {
    // Dealership data is never translated, in any of the six languages.
    renderFinancing({ financing: { ...financed, lender: 'Banque Rousseau & Fils' } });

    expect(screen.getByText('Banque Rousseau & Fils')).toBeVisible();
  });
});
