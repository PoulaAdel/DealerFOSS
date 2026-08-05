// DealsPage.test — the desk, and the two rules it has to explain without
// re-implementing.
//
// Use:  npm test
// Edit: the trap this file guards is the screen quietly becoming the second
//       place a rule lives. A salesperson cannot approve their own deal, and the
//       numbers freeze on submission — both are enforced in DealService. The
//       screen's job is to *show* the refusal the server gave, never to predict
//       one. If these tests start asserting that a button is hidden from a
//       particular person, the rule has been copied and the copies will drift.

import { render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { DealsPage } from './DealsPage';
import { apiCalls, mockApi, mockApiUnreachable } from '../../test/setup';
import type { DealDetail, DealSummary } from '../../shared/contracts';

const summary: DealSummary = {
  id: 'd1',
  rooftopId: 'r1',
  status: 'Draft',
  customerId: 'c1',
  customerName: 'Marisol Alvarez',
  inventoryUnitId: 'u1',
  stockNumber: 'NAG-1042',
  vehicle: '2021 Toyota RAV4 XLE',
  amountDue: 24000,
  currency: 'USD',
  salespersonUserId: 's1',
  isApproved: false,
};

const detail = (over: Partial<DealDetail> = {}): DealDetail => ({
  ...summary,
  leadId: null,
  subtotal: 24000,
  tradeIn: null,
  charges: [{ kind: 'VehiclePrice', description: 'The car', amount: 24000 }],
  approvedByUserId: null,
  approvedAt: null,
  termsAreOpen: true,
  history: [
    { fromStatus: null, toStatus: 'Draft', occurredAt: '2026-08-04T09:00:00Z', changedByUserId: 's1', note: null, amountAtChange: 24000 },
  ],
  ...over,
});

/**
 * The desk reads the address bar: a won enquiry hands the deal over through it.
 * So these mount inside a router, which is also what the real application does —
 * a test that rendered the page outside one was quietly testing something the
 * product never runs.
 */
function renderDeals(at = '/deals') {
  return render(
    <MemoryRouter initialEntries={[at]}>
      <DealsPage />
    </MemoryRouter>,
  );
}

async function openDeal() {
  await userEvent.click(await screen.findByRole('button', { name: 'Marisol Alvarez' }));
}

describe('the deal desk', () => {
  it('lists what is being worked', async () => {
    mockApi({ '/deals': { ok: true, body: [summary] } });
    renderDeals();

    expect(await screen.findByRole('button', { name: 'Marisol Alvarez' })).toBeVisible();
    expect(screen.getByText('2021 Toyota RAV4 XLE')).toBeVisible();
    expect(screen.getByText('NAG-1042')).toBeVisible();
    expect(screen.getByText('$24,000.00')).toBeVisible();
  });

  it('defaults to the ones still being worked', async () => {
    mockApi({ '/deals': { ok: true, body: [summary] } });
    renderDeals();
    await screen.findByRole('button', { name: 'Marisol Alvarez' });

    // A desk full of finished deals is not a desk.
    expect(apiCalls().some((c) => c.path.includes('openOnly=true'))).toBe(true);
  });

  it('says the desk is empty rather than showing an empty table', async () => {
    mockApi({ '/deals': { ok: true, body: [] } });
    renderDeals();

    expect(await screen.findByText(/No deals here/)).toBeVisible();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('explains a refusal, and offers a retry when the server is unreachable', async () => {
    mockApi({ '/deals': { ok: false, status: 403, code: 'deals.forbidden', detail: 'No.' } });
    const { unmount } = renderDeals();
    expect(await screen.findByRole('alert')).toHaveTextContent(/do not have access to deals/i);
    unmount();

    mockApiUnreachable();
    renderDeals();
    expect(await screen.findByRole('alert')).toHaveTextContent(/Could not reach the server/);
    expect(screen.getByRole('button', { name: 'Try again' })).toBeVisible();
  });
});

describe('one deal', () => {
  it('shows the numbers and what they add up to', async () => {
    mockApi({
      '/deals/d1': { ok: true, body: detail() },
      '/deals': { ok: true, body: [summary] },
    });

    renderDeals();
    await openDeal();

    const panel = await screen.findByRole('heading', { name: /Marisol Alvarez/ });
    expect(panel).toBeVisible();
    expect(screen.getByText('Due from the customer')).toBeVisible();
  });

  it('shows a trade-in as reducing what is owed, so the column adds up', async () => {
    mockApi({
      '/deals/d1': {
        ok: true,
        body: detail({
          subtotal: 26894,
          amountDue: 23594,
          charges: [{ kind: 'VehiclePrice', description: 'The car', amount: 26894 }],
          tradeIn: {
            description: '2014 Honda Civic',
            allowance: 3300,
            payoff: 0,
            equity: 3300,
            isNegativeEquity: false,
          },
        }),
      },
      '/deals': { ok: true, body: [summary] },
    });

    renderDeals();
    await openDeal();

    // A person reading down the column has to reach the total at the bottom.
    // Shown positive, the trade-in read as adding $3,300 rather than taking it
    // off — the figures disagreed with each other on screen.
    expect(await screen.findByText('-$3,300.00')).toBeVisible();
    expect(screen.getByText('$23,594.00')).toBeVisible();
  });

  it('shows negative equity as increasing what is owed', async () => {
    mockApi({
      '/deals/d1': {
        ok: true,
        body: detail({
          amountDue: 26500,
          tradeIn: {
            description: '2016 Ford Focus',
            allowance: 4000,
            payoff: 6500,
            equity: -2500,
            isNegativeEquity: true,
          },
        }),
      },
      '/deals': { ok: true, body: [summary] },
    });

    renderDeals();
    await openDeal();

    // Owing more than it is worth genuinely adds to the bill.
    expect(await screen.findByText('$2,500.00')).toBeVisible();
  });

  it('says plainly when a trade-in is worth less than is owed on it', async () => {
    mockApi({
      '/deals/d1': {
        ok: true,
        body: detail({
          tradeIn: {
            description: '2016 Ford Focus',
            allowance: 4000,
            payoff: 6500,
            equity: -2500,
            isNegativeEquity: true,
          },
        }),
      },
      '/deals': { ok: true, body: [summary] },
    });

    renderDeals();
    await openDeal();

    // It changes what the customer has to find, so it is stated rather than left
    // to be worked out from two numbers.
    expect(await screen.findByText(/owes more than it is worth/)).toBeVisible();
  });

  it('offers only the move the deal’s stage allows', async () => {
    mockApi({
      '/deals/d1': { ok: true, body: detail({ status: 'Draft' }) },
      '/deals': { ok: true, body: [summary] },
    });

    renderDeals();
    await openDeal();

    expect(await screen.findByRole('button', { name: 'Send to a manager' })).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Hand the car over' })).not.toBeInTheDocument();
  });

  it('offers approval once it has been submitted, and says who may not do it', async () => {
    mockApi({
      '/deals/d1': { ok: true, body: detail({ status: 'Submitted', termsAreOpen: false }) },
      '/deals': { ok: true, body: [{ ...summary, status: 'Submitted' }] },
    });

    renderDeals();
    await openDeal();

    expect(await screen.findByRole('button', { name: 'Approve' })).toBeVisible();
    expect(screen.getByText(/cannot be the one who approves it/)).toBeVisible();
    expect(screen.getByText(/numbers are frozen/)).toBeVisible();
  });

  it('shows the server’s refusal rather than predicting it', async () => {
    mockApi({
      '/deals/d1': { ok: true, body: detail({ status: 'Submitted' }) },
      '/deals/d1/status': {
        ok: false, status: 403, code: 'deals.self_approval',
        detail: 'You cannot approve a deal you built.',
      },
      '/deals': { ok: true, body: [{ ...summary, status: 'Submitted' }] },
    });

    renderDeals();
    await openDeal();
    await userEvent.click(await screen.findByRole('button', { name: 'Approve' }));

    // The server knows who is asking and which rule they hit. Anything this
    // screen invented would be a guess, and a second place for the rule to live.
    expect(await screen.findByText('You cannot approve a deal you built.')).toBeVisible();
  });

  it('moves a deal on and reflects the new stage', async () => {
    mockApi({
      '/deals/d1': { ok: true, body: detail({ status: 'Draft' }) },
      '/deals/d1/status': { ok: true, body: detail({ status: 'Submitted', termsAreOpen: false }) },
      '/deals': { ok: true, body: [summary] },
    });

    renderDeals();
    await openDeal();
    await userEvent.click(await screen.findByRole('button', { name: 'Send to a manager' }));

    await waitFor(() => expect(screen.getByRole('button', { name: 'Approve' })).toBeVisible());
  });

  it('offers nothing on a finished deal', async () => {
    mockApi({
      '/deals/d1': { ok: true, body: detail({ status: 'Delivered' }) },
      '/deals': { ok: true, body: [{ ...summary, status: 'Delivered' }] },
    });

    renderDeals();
    await openDeal();

    expect(await screen.findByText(/This deal is finished/)).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Mark lost' })).not.toBeInTheDocument();
  });

  it('opens ready to build when a won enquiry hands the deal over', async () => {
    mockApi({
      '/customers/c1': {
        ok: true,
        body: {
          id: 'c1', displayName: 'Marisol Alvarez', kind: 'Person',
          primaryEmail: null, primaryPhone: null,
        },
      },
      '/inventory': { ok: true, body: [] },
      '/deals': { ok: true, body: [] },
    });

    renderDeals('/deals?leadId=l1&customerId=c1');

    // The buyer is decided by the enquiry. Offering a picker here would let
    // somebody build the deal for the wrong person while the lead still claims
    // credit for it.
    expect(await screen.findByText(/From the enquiry for/)).toBeVisible();
    expect(screen.getByText('Marisol Alvarez')).toBeVisible();
    expect(screen.queryByLabelText('Who is buying')).not.toBeInTheDocument();
  });

  it('carries the enquiry onto the deal it creates', async () => {
    mockApi({
      '/customers/c1': {
        ok: true,
        body: {
          id: 'c1', displayName: 'Marisol Alvarez', kind: 'Person',
          primaryEmail: null, primaryPhone: null,
        },
      },
      '/inventory': {
        ok: true,
        body: [{
          id: 'u1', stockNumber: 'NAG-1042', rooftopId: 'r1', status: 'Available',
          vehicleId: 'v1', vin: '1HGCM82633A004352', vehicleDisplayName: '2021 Toyota RAV4 XLE',
        }],
      },
      '/deals': [{ ok: true, body: [] }, { ok: true, body: detail({ leadId: 'l1' }) }, { ok: true, body: [summary] }],
    });

    renderDeals('/deals?leadId=l1&customerId=c1');
    await screen.findByText(/From the enquiry for/);

    await userEvent.selectOptions(screen.getByLabelText('Which car'), 'u1');
    await userEvent.click(screen.getByRole('button', { name: 'Start the deal' }));

    // Without this the two halves of one sale sit in the system unaware of each
    // other, and nothing can say which enquiries turned into cars sold.
    const created = apiCalls().find((c) => c.path === '/deals' && c.init?.method === 'POST');
    expect(created).toBeDefined();
    expect(JSON.parse(String(created!.init!.body)).leadId).toBe('l1');
  });

  it('shows what happened, newest first', async () => {
    mockApi({
      '/deals/d1': {
        ok: true,
        body: detail({
          status: 'Submitted',
          history: [
            { fromStatus: null, toStatus: 'Draft', occurredAt: '2026-08-04T09:00:00Z', changedByUserId: 's1', note: null, amountAtChange: 0 },
            { fromStatus: 'Draft', toStatus: 'Submitted', occurredAt: '2026-08-04T10:00:00Z', changedByUserId: 's1', note: 'Priced up.', amountAtChange: 24000 },
          ],
        }),
      },
      '/deals': { ok: true, body: [{ ...summary, status: 'Submitted' }] },
    });

    renderDeals();
    await openDeal();

    const history = await screen.findByRole('list');
    const entries = within(history).getAllByRole('listitem');
    expect(entries[0]).toHaveTextContent('Submitted');
    expect(entries[0]).toHaveTextContent('Priced up.');
    expect(entries[1]).toHaveTextContent('Draft');
  });
});
