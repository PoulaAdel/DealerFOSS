// DealTerms.test — putting numbers on a deal, and the two ways that goes wrong.
//
// Use:  npm test
// Edit: the properties worth guarding are that saving **replaces** the whole set
//       (so the form must start from what is already there, or editing one line
//       deletes the rest), and that the editor is absent rather than disabled
//       once a deal is submitted. Both are the kind of thing that looks like a
//       tidy-up and loses somebody's work.

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { DealTerms } from './DealTerms';
import { DealsPage } from './DealsPage';
import { apiCalls, mockApi } from '../../test/setup';
import type { DealDetail } from '../../shared/contracts';

const base: DealDetail = {
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
  leadId: null,
  subtotal: 24000,
  tradeIn: null,
  charges: [{ kind: 'VehiclePrice', description: 'The car', amount: 24000 }],
  approvedByUserId: null,
  approvedAt: null,
  termsAreOpen: true,
  history: [],
};

describe('entering the numbers', () => {
  it('starts from what is already on the deal', async () => {
    mockApi({});
    render(<DealTerms deal={base} onSaved={vi.fn()} />);

    // Saving replaces the whole set. An empty form would mean editing one line
    // silently deleted every other one.
    expect(screen.getByLabelText('Line 1 description')).toHaveValue('The car');
    expect(screen.getByLabelText('Line 1 amount')).toHaveValue('24000');
  });

  it('sends every line, not only the edited one', async () => {
    mockApi({ '/deals/d1/terms': { ok: true, body: base } });

    render(
      <DealTerms
        deal={{
          ...base,
          charges: [
            { kind: 'VehiclePrice', description: 'The car', amount: 24000 },
            { kind: 'Fee', description: 'Documentation fee', amount: 399 },
          ],
        }}
        onSaved={vi.fn()}
      />,
    );

    await userEvent.clear(screen.getByLabelText('Line 1 amount'));
    await userEvent.type(screen.getByLabelText('Line 1 amount'), '25000');
    await userEvent.click(screen.getByRole('button', { name: 'Save the numbers' }));

    await waitFor(() => {
      const sent = apiCalls().find((c) => c.path === '/deals/d1/terms');
      const body = JSON.parse(sent!.init!.body as string) as { charges: unknown[] };
      expect(body.charges).toHaveLength(2);
    });
  });

  it('will not save a deal with no price for the car', async () => {
    mockApi({});
    render(
      <DealTerms
        deal={{ ...base, charges: [{ kind: 'Fee', description: 'Documentation fee', amount: 399 }] }}
        onSaved={vi.fn()}
      />,
    );

    expect(screen.getByRole('button', { name: 'Save the numbers' })).toBeDisabled();
    expect(screen.getByText(/needs a price for the car itself/)).toBeVisible();
  });

  it('adds and removes lines, but never the last one', async () => {
    mockApi({});
    render(<DealTerms deal={base} onSaved={vi.fn()} />);

    expect(screen.getByRole('button', { name: 'Remove' })).toBeDisabled();

    await userEvent.click(screen.getByRole('button', { name: 'Add a line' }));
    expect(screen.getByLabelText('Line 2 kind')).toBeVisible();

    await userEvent.click(screen.getAllByRole('button', { name: 'Remove' })[1]!);
    expect(screen.queryByLabelText('Line 2 kind')).not.toBeInTheDocument();
  });

  it('warns while typing when a trade-in is worth less than is owed', async () => {
    mockApi({});
    render(<DealTerms deal={base} onSaved={vi.fn()} />);

    await userEvent.click(screen.getByRole('button', { name: 'Add a trade-in' }));
    await userEvent.type(screen.getByLabelText(/What we are allowing/), '4000');
    await userEvent.type(screen.getByLabelText(/What is still owed/), '6500');

    // Said now rather than after saving: it changes what the customer has to
    // find, and discovering it later is worse.
    expect(screen.getByText(/owe more on it than we are allowing/)).toBeVisible();
  });

  it('sends the trade-in when there is one, and null when it is removed', async () => {
    mockApi({ '/deals/d1/terms': { ok: true, body: base } });
    render(<DealTerms deal={base} onSaved={vi.fn()} />);

    await userEvent.click(screen.getByRole('button', { name: 'Add a trade-in' }));
    await userEvent.type(screen.getByLabelText(/What they are trading/), '2014 Honda Civic');
    await userEvent.type(screen.getByLabelText(/What we are allowing/), '3300');
    await userEvent.type(screen.getByLabelText(/What is still owed/), '0');
    await userEvent.click(screen.getByRole('button', { name: 'Save the numbers' }));

    await waitFor(() => {
      const sent = apiCalls().find((c) => c.path === '/deals/d1/terms');
      const body = JSON.parse(sent!.init!.body as string) as { tradeIn: { allowance: number } | null };
      expect(body.tradeIn?.allowance).toBe(3300);
    });
  });

  it('shows the server’s refusal rather than inventing one', async () => {
    mockApi({
      '/deals/d1/terms': {
        ok: false, status: 400, code: 'deals.discount_must_be_negative',
        detail: 'A discount is money off, so it has to be negative.',
      },
    });

    render(<DealTerms deal={base} onSaved={vi.fn()} />);
    await userEvent.click(screen.getByRole('button', { name: 'Save the numbers' }));

    expect(await screen.findByText('A discount is money off, so it has to be negative.')).toBeVisible();
  });
});

describe('once a deal is submitted', () => {
  it('the editor is gone, not merely disabled', async () => {
    mockApi({
      '/deals': { ok: true, body: [{ ...base, status: 'Submitted' }] },
      '/deals/d1': { ok: true, body: { ...base, status: 'Submitted', termsAreOpen: false } },
    });

    render(<DealsPage />);
    await userEvent.click(await screen.findByRole('button', { name: 'Marisol Alvarez' }));

    // A disabled form still reads as somewhere to type. Somebody fills it in,
    // presses save, and finds their work has gone.
    expect(await screen.findByText(/numbers are frozen/)).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Save the numbers' })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Line 1 amount')).not.toBeInTheDocument();
  });

  it('the editor is there while it is still a draft', async () => {
    mockApi({
      '/deals': { ok: true, body: [base] },
      '/deals/d1': { ok: true, body: base },
    });

    render(<DealsPage />);
    await userEvent.click(await screen.findByRole('button', { name: 'Marisol Alvarez' }));

    expect(await screen.findByRole('button', { name: 'Save the numbers' })).toBeVisible();
  });
});

describe('starting a deal', () => {
  const unit = {
    id: 'u1', stockNumber: 'NAG-1042', rooftopId: 'r1', status: 'Available',
    vehicleId: 'v1', vin: '1HGBH41JXMN109186', vehicleDisplayName: '2021 Toyota RAV4',
  };

  it('offers only cars that are actually available', async () => {
    mockApi({
      '/deals': { ok: true, body: [] },
      '/inventory': { ok: true, body: [unit] },
      '/customers': { ok: true, body: [] },
    });

    render(<DealsPage />);
    await userEvent.click(await screen.findByRole('button', { name: 'Start a deal' }));

    // A car on somebody else's deal is held; offering it and then explaining the
    // refusal wastes a salesperson's time in front of a customer.
    await waitFor(() =>
      expect(apiCalls().some((c) => c.path.includes('status=Available'))).toBe(true),
    );
  });

  it('takes the lot from the chosen car rather than asking again', async () => {
    mockApi({
      '/deals': [
        { ok: true, body: [] },
        { ok: true, body: { ...base } },
        { ok: true, body: [base] },
      ],
      '/inventory': { ok: true, body: [unit] },
      '/customers': { ok: true, body: [{ id: 'c1', displayName: 'Marisol Alvarez', kind: 'Person', primaryEmail: null, primaryPhone: null }] },
      '/deals/d1': { ok: true, body: base },
    });

    render(<DealsPage />);
    await userEvent.click(await screen.findByRole('button', { name: 'Start a deal' }));

    await userEvent.selectOptions(await screen.findByLabelText('Who is buying'), 'c1');
    await userEvent.selectOptions(screen.getByLabelText('Which car'), 'u1');
    await userEvent.click(screen.getByRole('button', { name: 'Start the deal' }));

    await waitFor(() => {
      const sent = apiCalls().find((c) => c.path === '/deals' && c.init?.method === 'POST');
      const body = JSON.parse(sent!.init!.body as string) as { rooftopId: string };
      // A car is on exactly one lot. Asking somebody to name it again invites
      // picking the wrong one.
      expect(body.rooftopId).toBe('r1');
    });
  });

  it('cannot start without both a buyer and a car', async () => {
    mockApi({
      '/deals': { ok: true, body: [] },
      '/inventory': { ok: true, body: [unit] },
      '/customers': { ok: true, body: [] },
    });

    render(<DealsPage />);
    await userEvent.click(await screen.findByRole('button', { name: 'Start a deal' }));

    expect(await screen.findByRole('button', { name: 'Start the deal' })).toBeDisabled();
  });

  it('says so when nothing on the lot is available', async () => {
    mockApi({
      '/deals': { ok: true, body: [] },
      '/inventory': { ok: true, body: [] },
      '/customers': { ok: true, body: [] },
    });

    render(<DealsPage />);
    await userEvent.click(await screen.findByRole('button', { name: 'Start a deal' }));

    expect(await screen.findByText(/Nothing on the lot is available/)).toBeVisible();
  });
});
