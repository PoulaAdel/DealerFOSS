// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   InventoryPage.test — every state a real screen needs, actually drawn.
//
// Usage:
//   npm test
//
// Coding Instructions:
//   The states are the contract (doc 10 §5). A screen that only renders the
//   happy path is not finished, and this file is what stops that claim being
//   taken on trust.

import { render, screen } from '../../test/render';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router';
import { InventoryPage } from './InventoryPage';
import { apiCalls, mockApi, mockApiPending, mockApiUnreachable, page } from '../../test/setup';
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

/**
 * Inside a router because the screen reads `?stock=` from the address — that is
 * how the dashboard hands somebody a specific car.
 */
function renderStock(at = '/inventory') {
  return render(
    <MemoryRouter initialEntries={[at]}>
      <InventoryPage />
    </MemoryRouter>,
  );
}

describe('the stock list', () => {
  it('says it is loading before the answer arrives', () => {
    mockApiPending();
    renderStock();

    expect(screen.getByText('Loading the stock list…')).toBeVisible();
  });

  it('draws a car once the list arrives', async () => {
    mockApi({ '/inventory': { ok: true, body: page([unit]) } });
    renderStock();

    expect(await screen.findByText('NAG-1042')).toBeVisible();
    expect(screen.getByText('2021 Toyota RAV4')).toBeVisible();
    expect(screen.getByText('1HGCM82633A004352')).toBeVisible();
    expect(screen.getByRole('columnheader', { name: 'Stock' })).toBeVisible();
  });

  it('explains an empty lot instead of showing an empty table', async () => {
    mockApi({ '/inventory': { ok: true, body: page([]) } });
    renderStock();

    expect(
      await screen.findByText(/Nothing here yet\. Cars appear once they are taken into stock\./),
    ).toBeVisible();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('explains a refusal in words a salesperson can act on', async () => {
    mockApi({
      '/inventory': { ok: false, status: 403, code: 'access.denied', detail: 'No.' },
    });
    renderStock();

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
        { ok: true, body: page([unit]) },
      ],
    });
    renderStock();

    expect(await screen.findByRole('alert')).toHaveTextContent('The server answered 500.');

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByText('NAG-1042')).toBeVisible();
  });

  it('says the server is unreachable rather than blaming the user', async () => {
    mockApiUnreachable();
    renderStock();

    expect(await screen.findByRole('alert')).toHaveTextContent(/Could not reach the server/);
  });

  it('says which cars these are out of how many, and reaches the rest', async () => {
    // A dealership with 412 cars used to be told, on the screen they count
    // their own stock with, that they had 200 and "there may be more".
    const full = Array.from({ length: 50 }, (_, i) => ({
      ...unit,
      id: `${i}`.padStart(8, '0') + '-1111-1111-1111-111111111111',
      stockNumber: `NAG-${1000 + i}`,
    }));

    mockApi({ '/inventory': { ok: true, body: page(full, { total: 412 }) } });
    renderStock();

    // Said twice on purpose, to two different audiences: the table's caption is
    // what a screen reader announces, the note is what a sighted user reads.
    expect(await screen.findAllByText('Showing 1–50 of 412.')).toHaveLength(2);

    await userEvent.click(screen.getByRole('button', { name: 'Next' }));

    expect(
      apiCalls().some((call) => call.path.includes('offset=50')),
      'the second page has to be asked for, not sliced off the first',
    ).toBe(true);
  });

  it('cannot go back from the first page', async () => {
    mockApi({ '/inventory': { ok: true, body: page([unit], { total: 90 }) } });
    renderStock();

    await screen.findByRole('table');
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled();
  });

  it('says the count plainly when it is the whole lot', async () => {
    mockApi({ '/inventory': { ok: true, body: page([unit]) } });
    renderStock();

    await screen.findByText('NAG-1042');
    expect(screen.queryByText(/There may be more/)).not.toBeInTheDocument();
  });

  it('asks the server again when the status filter changes', async () => {
    mockApi({ '/inventory': { ok: true, body: page([unit]) } });
    renderStock();
    await screen.findByText('NAG-1042');

    await userEvent.selectOptions(screen.getByLabelText('Status'), 'Sold');

    // Filtering is the server's job: it is the only party that knows which
    // rooftops this caller may see.
    expect(apiCalls().some((c) => c.path.includes('status=Sold'))).toBe(true);
  });

  it('narrows to one car when sent here from somewhere that named it', async () => {
    // The dashboard's oldest-stock list links here. Landing on the whole list
    // would make that link a promise the screen does not keep.
    mockApi({ '/inventory': { ok: true, body: page([unit]) } });
    renderStock('/inventory?stock=NAG-1042');

    await screen.findByText('2021 Toyota RAV4');

    expect(apiCalls().some((c) => c.path.includes('stock=NAG-1042'))).toBe(true);
    expect(screen.getByRole('button', { name: 'Show everything' })).toBeVisible();
  });

  it('offers the way back to the whole list', async () => {
    mockApi({ '/inventory': { ok: true, body: page([unit]) } });
    renderStock('/inventory?stock=NAG-1042');

    await userEvent.click(await screen.findByRole('button', { name: 'Show everything' }));

    const last = apiCalls().at(-1)!;
    expect(last.path).not.toContain('stock=');
  });
});

/**
 * Taking a car in, and moving it once it is there.
 *
 * Both endpoints existed and were tested from the day inventory was built. What
 * did not exist was any way to reach them: the walk on 2026-09-10 found 205
 * buttons on this screen and every one of them a stock number, so a car could
 * only arrive from a seeder and could never leave Reconditioning. These tests
 * are about the reaching.
 */
describe('taking a car into stock', () => {
  const organization = {
    id: '99999999-9999-9999-9999-999999999999',
    name: 'North Auto Group',
    legalEntities: [
      {
        id: '88888888-8888-8888-8888-888888888888',
        name: 'North Auto Group LLC',
        rooftops: [{ id: unit.rooftopId, code: 'NAG-01', name: 'North Auto Downtown' }],
      },
    ],
  };

  /** The unit the receive POST answers with, as the server really answers it. */
  const received = {
    ...unit,
    costAmount: 14500,
    costCurrency: 'USD',
    acquiredOn: null,
    history: [],
  };

  /**
   * Replies to `/inventory` are sequential because the list GET and the receive
   * POST share a path: the first list, then the unit the POST returns, then the
   * refetch. Answering the POST with the list array instead made the detail band
   * throw on `unit.status` — the tests still passed and Vitest reported two
   * unhandled errors, which is the shape of a false positive.
   */
  function mockTakeIn() {
    mockApi({
      '/inventory': [
        { ok: true, body: page([unit]) },
        { ok: true, body: received },
        { ok: true, body: page([unit]) },
      ],
      '/organization': { ok: true, body: organization },
      '/vehicles': { ok: true, body: { id: unit.vehicleId } },
    });
  }

  async function fillTheForm(cost: string) {
    await userEvent.click(await screen.findByRole('button', { name: 'Take a car into stock' }));

    await userEvent.type(await screen.findByLabelText('Stock'), 'NAG-1042');
    await userEvent.type(screen.getByLabelText('Year'), '2021');
    await userEvent.type(screen.getByLabelText('Make'), 'Toyota');
    await userEvent.type(screen.getByLabelText('Model'), 'RAV4');

    if (cost !== '') {
      await userEvent.type(screen.getByLabelText('What it cost (optional)'), cost);
    }
  }

  function bodyOf(path: string): Record<string, unknown> {
    const call = [...apiCalls()].reverse().find((c) => c.path === path && c.init?.method === 'POST');
    expect(call, `nothing was POSTed to ${path}`).toBeDefined();

    return JSON.parse(call!.init!.body as string) as Record<string, unknown>;
  }

  it('creates the vehicle and the unit together, with what it cost', async () => {
    mockTakeIn();
    renderStock();
    await fillTheForm('14500');

    await userEvent.click(screen.getByRole('button', { name: 'Take it in' }));

    // The vehicle first, because the unit needs its id.
    expect(bodyOf('/vehicles')).toMatchObject({ modelYear: 2021, make: 'Toyota', model: 'RAV4' });

    expect(bodyOf('/inventory')).toMatchObject({
      vehicleId: unit.vehicleId,
      rooftopId: unit.rooftopId,
      stockNumber: 'NAG-1042',
      costAmount: 14500,
      costCurrency: 'USD',
    });
  });

  it('sends no cost rather than a cost of nothing when the box is left empty', async () => {
    // The distinction the ledger depends on. A car received without a cost is
    // recorded and not posted; a car received at zero would assert it was free.
    mockTakeIn();
    renderStock();
    await fillTheForm('');

    await userEvent.click(screen.getByRole('button', { name: 'Take it in' }));

    const received = bodyOf('/inventory');
    expect(received.costAmount).toBeNull();
    expect(received.costCurrency).toBeNull();
  });

  it('will not submit until it has the few things it cannot invent', async () => {
    mockTakeIn();
    renderStock();

    await userEvent.click(await screen.findByRole('button', { name: 'Take a car into stock' }));
    await userEvent.type(await screen.findByLabelText('Stock'), 'NAG-1042');

    expect(screen.getByRole('button', { name: 'Take it in' })).toBeDisabled();
  });

  it('opens the car it just took in, rather than leaving it to be found', async () => {
    // Adding a customer closes its panel and changes nothing visible, so nobody
    // can tell it worked without searching. A car arriving is the same event and
    // deliberately gets the opposite treatment.
    mockTakeIn();
    renderStock();
    await fillTheForm('14500');

    await userEvent.click(screen.getByRole('button', { name: 'Take it in' }));

    expect(await screen.findByRole('region', { name: 'Stock number NAG-1042' })).toBeVisible();
  });

  it('reports a refusal instead of pretending the car is on the lot', async () => {
    mockTakeIn();
    renderStock();
    await fillTheForm('14500');

    mockApi({
      '/inventory': {
        ok: false, status: 409, code: 'inventory.stock_number_taken',
        detail: 'That stock number is already in use.',
      },
      '/organization': { ok: true, body: organization },
      '/vehicles': { ok: true, body: { id: unit.vehicleId } },
    });

    await userEvent.click(screen.getByRole('button', { name: 'Take it in' }));

    expect(await screen.findByRole('alert')).toBeVisible();
  });
});

describe('moving a car between stock states', () => {
  const detailAt = (status: InventoryUnitSummary['status']) => ({
    ...unit,
    status,
    costAmount: 14500,
    costCurrency: 'USD',
    acquiredOn: null,
    history: [],
  });

  it('offers the moves the domain allows from where the car is', async () => {
    mockApi({
      '/inventory': { ok: true, body: page([unit]) },
      [`/inventory/${unit.id}`]: { ok: true, body: detailAt('Reconditioning') },
    });
    renderStock();

    await userEvent.click(await screen.findByRole('button', { name: /NAG-1042/ }));

    expect(await screen.findByRole('button', { name: 'Move to Available' })).toBeVisible();
    expect(screen.getByRole('button', { name: 'Move to On hold' })).toBeVisible();

    // Never Sold. A car is sold by delivering a deal, which is what posts the
    // sale — a button here would be the route that skips the ledger.
    expect(screen.queryByRole('button', { name: /Move to Sold/ })).not.toBeInTheDocument();
  });

  it('sends the new status and the note to the server', async () => {
    mockApi({
      '/inventory': { ok: true, body: page([unit]) },
      [`/inventory/${unit.id}`]: { ok: true, body: detailAt('Reconditioning') },
      [`/inventory/${unit.id}/status`]: { ok: true, body: detailAt('Available') },
    });
    renderStock();

    await userEvent.click(await screen.findByRole('button', { name: /NAG-1042/ }));
    await userEvent.type(await screen.findByLabelText('Note (goes on the record)'), 'Valeted.');
    await userEvent.click(screen.getByRole('button', { name: 'Move to Available' }));

    const call = [...apiCalls()]
      .reverse()
      .find((c) => c.path === `/inventory/${unit.id}/status` && c.init?.method === 'POST');

    expect(call, 'the move was never sent').toBeDefined();
    expect(JSON.parse(call!.init!.body as string)).toMatchObject({
      status: 'Available',
      note: 'Valeted.',
    });
  });


  it('never offers Sold, from any state a car can be moved from', async () => {
    // Broadened after a rehearsal: the first version of this only opened a car
    // in Reconditioning, so adding Sold to the Available moves broke nothing and
    // the suite stayed green. Available is the state most cars are in.
    for (const from of ['Incoming', 'Reconditioning', 'Available', 'OnHold'] as const) {
      mockApi({
        '/inventory': { ok: true, body: page([unit]) },
        [`/inventory/${unit.id}`]: { ok: true, body: detailAt(from) },
      });

      const view = renderStock();
      await userEvent.click(await screen.findByRole('button', { name: /NAG-1042/ }));
      await screen.findByRole('heading', { name: 'Where it goes next' });

      expect(
        screen.queryByRole('button', { name: /Move to Sold/ }),
        `${from} offered a way to sell a car without a deal`,
      ).not.toBeInTheDocument();

      view.unmount();
    }
  });

  it('says why a sold car cannot be moved rather than showing dead buttons', async () => {
    mockApi({
      '/inventory': { ok: true, body: page([unit]) },
      [`/inventory/${unit.id}`]: { ok: true, body: detailAt('Sold') },
    });
    renderStock();

    await userEvent.click(await screen.findByRole('button', { name: /NAG-1042/ }));

    expect(
      await screen.findByText('This car has been sold. Reverse the deal to undo that.'),
    ).toBeVisible();
  });
});
