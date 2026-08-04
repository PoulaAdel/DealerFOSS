// InventoryPage.test — every state a real screen needs, actually drawn.
//
// Use:  npm test
// Edit: the states are the contract (doc 10 §5). A screen that only renders the
//       happy path is not finished, and this file is what stops that claim being
//       taken on trust.

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { InventoryPage } from './InventoryPage';
import { apiCalls, mockApi, mockApiPending, mockApiUnreachable } from '../../test/setup';
import type { InventoryUnitSummary } from '../../shared/contracts';

const unit: InventoryUnitSummary = {
  id: '11111111-1111-1111-1111-111111111111',
  stockNumber: 'NAG-1042',
  rooftopId: '22222222-2222-2222-2222-222222222222',
  status: 'Available',
  vehicleId: '33333333-3333-3333-3333-333333333333',
  vin: '1HGCM82633A004352',
  vehicleDisplayName: '2021 Toyota RAV4',
};

describe('the stock list', () => {
  it('says it is loading before the answer arrives', () => {
    mockApiPending();
    render(<InventoryPage />);

    expect(screen.getByText('Loading the stock list…')).toBeVisible();
  });

  it('draws a car once the list arrives', async () => {
    mockApi({ '/inventory': { ok: true, body: [unit] } });
    render(<InventoryPage />);

    expect(await screen.findByText('NAG-1042')).toBeVisible();
    expect(screen.getByText('2021 Toyota RAV4')).toBeVisible();
    expect(screen.getByText('1HGCM82633A004352')).toBeVisible();
    expect(screen.getByRole('columnheader', { name: 'Stock' })).toBeVisible();
  });

  it('explains an empty lot instead of showing an empty table', async () => {
    mockApi({ '/inventory': { ok: true, body: [] } });
    render(<InventoryPage />);

    expect(
      await screen.findByText(/Nothing here yet\. Cars appear once they are taken into stock\./),
    ).toBeVisible();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('explains a refusal in words a salesperson can act on', async () => {
    mockApi({
      '/inventory': { ok: false, status: 403, code: 'access.denied', detail: 'No.' },
    });
    render(<InventoryPage />);

    const message = await screen.findByRole('alert');
    expect(message).toHaveTextContent(/do not have access to this location/i);
    // A refusal is an answer, not a fault. Offering "try again" would invite
    // somebody to hammer a door that is not going to open.
    expect(screen.queryByRole('button', { name: 'Try again' })).not.toBeInTheDocument();
  });

  it('offers a retry when the server fails, and recovers on the second go', async () => {
    mockApi({
      '/inventory': [
        { ok: false, status: 500, code: 'unknown', detail: 'The server answered 500.' },
        { ok: true, body: [unit] },
      ],
    });
    render(<InventoryPage />);

    expect(await screen.findByRole('alert')).toHaveTextContent('The server answered 500.');

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByText('NAG-1042')).toBeVisible();
  });

  it('says the server is unreachable rather than blaming the user', async () => {
    mockApiUnreachable();
    render(<InventoryPage />);

    expect(await screen.findByRole('alert')).toHaveTextContent(/Could not reach the server/);
  });

  it('does not claim a full page is the whole lot', async () => {
    // The server clamps to 200. A dealership with 400 cars would otherwise be
    // told, on a screen they use to count their own stock, that they have 200.
    const full = Array.from({ length: 200 }, (_, i) => ({
      ...unit,
      id: `${i}`.padStart(8, '0') + '-1111-1111-1111-111111111111',
      stockNumber: `NAG-${1000 + i}`,
    }));

    mockApi({ '/inventory': { ok: true, body: full } });
    render(<InventoryPage />);

    // Said twice on purpose, to two different audiences: the table's caption is
    // what a screen reader announces, the note is what a sighted user reads.
    expect(await screen.findByText(/Showing the first 200/)).toBeVisible();
    expect(screen.getAllByText(/There may be more/)).toHaveLength(2);
  });

  it('says the count plainly when it is the whole lot', async () => {
    mockApi({ '/inventory': { ok: true, body: [unit] } });
    render(<InventoryPage />);

    await screen.findByText('NAG-1042');
    expect(screen.queryByText(/There may be more/)).not.toBeInTheDocument();
  });

  it('asks the server again when the status filter changes', async () => {
    mockApi({ '/inventory': { ok: true, body: [unit] } });
    render(<InventoryPage />);
    await screen.findByText('NAG-1042');

    await userEvent.selectOptions(screen.getByLabelText('Status'), 'Sold');

    // Filtering is the server's job: it is the only party that knows which
    // rooftops this caller may see.
    expect(apiCalls().some((c) => c.path.includes('status=Sold'))).toBe(true);
  });
});
