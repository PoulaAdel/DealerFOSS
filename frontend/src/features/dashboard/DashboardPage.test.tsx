// DashboardPage.test — the screen somebody lands on, actually drawn.
//
// Use:  npm test
// Edit: four of these are safeguards rather than coverage.
//
//       "nothing sold is not a margin of zero" and "up from nothing" both guard
//       arithmetic that is only wrong in the months nobody tests by hand — a new
//       dealership's first one, and the one after a quiet December.
//
//       "says what it is not showing" guards the difference between a dashboard
//       that is empty and one that is withholding. Those look identical and mean
//       opposite things.
//
//       "moves month without leaving the page" is the whole claim of the screen.

import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router';
import { DashboardPage } from './DashboardPage';
import { apiCalls, mockApi, mockApiPending } from '../../test/setup';
import { setCurrentTenant } from '../../shared/api';
import type { LedgerPerformance, MonthInReview } from '../../shared/contracts';

function trading(overrides: Partial<LedgerPerformance> = {}): LedgerPerformance {
  return {
    from: '2026-08-01',
    to: '2026-08-31',
    currency: 'USD',
    departments: [
      { name: 'Vehicles', revenue: 240000, cost: 208000, gross: 32000, margin: 0.1333 },
      { name: 'Finance and insurance', revenue: 14000, cost: 8000, gross: 6000, margin: 0.4286 },
      { name: 'Service', revenue: 31000, cost: 12000, gross: 19000, margin: 0.6129 },
    ],
    totalRevenue: 285000,
    totalCost: 228000,
    totalGross: 57000,
    vehiclesDelivered: 12,
    serviceInvoices: 44,
    ...overrides,
  };
}

const august: MonthInReview = {
  year: 2026,
  month: 8,
  startsOn: '2026-08-01',
  endsOn: '2026-08-31',
  books: 'Open',
  closedAt: null,
  trading: trading(),
  priorMonth: trading({
    from: '2026-07-01',
    to: '2026-07-31',
    totalGross: 50000,
    vehiclesDelivered: 10,
    serviceInvoices: 51,
  }),
  stock: {
    asOf: '2026-08-07',
    units: 9,
    bands: [
      { name: '0 to 30 days', fromDay: 0, toDay: 30, units: 4 },
      { name: '31 to 60 days', fromDay: 31, toDay: 60, units: 3 },
      { name: '61 to 90 days', fromDay: 61, toDay: 90, units: 1 },
      { name: 'Over 90 days', fromDay: 91, toDay: null, units: 1 },
    ],
    oldest: [
      {
        id: 'u1',
        stockNumber: 'NAG-1042',
        vehicleDisplayName: '2021 Toyota RAV4',
        status: 'Available',
        daysInStock: 141,
        ageIsEstimated: false,
      },
    ],
  },
  withheld: [],
};

const noOrganization = { '/organization': { ok: true as const, body: { legalEntities: [] } } };

function renderDashboard() {
  setCurrentTenant('northgroup');

  return render(
    <MemoryRouter initialEntries={['/dashboard']}>
      <DashboardPage />
    </MemoryRouter>,
  );
}

describe('how did we do this month', () => {
  it('says it is adding up before the answer arrives', () => {
    mockApiPending();
    renderDashboard();

    expect(screen.getByText('Adding the month up…')).toBeVisible();
  });

  it('leads with the total gross and how it compares with last month', async () => {
    mockApi({ '/reporting/month': { ok: true, body: august }, ...noOrganization });
    renderDashboard();

    // Scoped to the tiles: the same figure is in the table's total row, and a
    // bare text query would match both and fail for the wrong reason.
    expect(within(await tiles()).getByText('$57,000')).toBeVisible();
    expect(screen.getByText(/14% up on last month/)).toBeVisible();
  });

  it('reports the car and the warranty as two businesses', async () => {
    mockApi({ '/reporting/month': { ok: true, body: august }, ...noOrganization });
    renderDashboard();

    // The split a dealer principal reads first. One combined figure would make
    // the more profitable of the two invisible.
    const made = await tiles();
    expect(within(made).getByText('$32,000')).toBeVisible();
    expect(within(made).getByText('$6,000')).toBeVisible();
    expect(within(made).getByText('$19,000')).toBeVisible();
  });

  it('counts what sold and what it averaged', async () => {
    mockApi({ '/reporting/month': { ok: true, body: august }, ...noOrganization });
    renderDashboard();

    const sold = await screen.findByLabelText('What sold');
    expect(within(sold).getByText('12')).toBeVisible();
    expect(within(sold).getByText('was 10')).toBeVisible();

    // (32,000 front + 6,000 back) / 12 cars.
    expect(within(sold).getByText('$3,167')).toBeVisible();
  });

  it('says whether the figures can still move', async () => {
    mockApi({ '/reporting/month': { ok: true, body: august }, ...noOrganization });
    renderDashboard();

    expect(await screen.findByText(/books are open, so these figures can still move/))
      .toBeVisible();
  });

  it('says a closed month is the one that was reported', async () => {
    mockApi({
      '/reporting/month': {
        ok: true,
        body: { ...august, books: 'Closed', closedAt: '2026-09-04T16:00:00Z' },
      },
      ...noOrganization,
    });
    renderDashboard();

    expect(await screen.findByText(/These are the figures that were reported/)).toBeVisible();
  });

  it('does not claim a margin on a department that sold nothing', async () => {
    // 0% would say we sold things and made nothing on them, which is a
    // different and false statement about a month that has not started.
    mockApi({
      '/reporting/month': {
        ok: true,
        body: {
          ...august,
          trading: trading({
            departments: [
              { name: 'Vehicles', revenue: 0, cost: 0, gross: 0, margin: null },
            ],
          }),
        },
      },
      ...noOrganization,
    });
    renderDashboard();

    const table = await screen.findByLabelText('Where the gross came from');
    expect(within(table).getAllByText('—').length).toBeGreaterThan(0);
    expect(within(table).queryByText('0%')).not.toBeInTheDocument();
  });

  it('does not divide by a month that made nothing', async () => {
    mockApi({
      '/reporting/month': {
        ok: true,
        body: { ...august, priorMonth: trading({ totalGross: 0 }) },
      },
      ...noOrganization,
    });
    renderDashboard();

    // "∞% up" is a worse answer than admitting there is nothing to compare.
    expect(await screen.findByText('up from nothing last month')).toBeVisible();
    expect(screen.queryByText(/Infinity/)).not.toBeInTheDocument();
  });

  it('offers no comparison at all when there is no previous month to read', async () => {
    mockApi({
      '/reporting/month': { ok: true, body: { ...august, priorMonth: null } },
      ...noOrganization,
    });
    renderDashboard();

    expect(await screen.findAllByText('no comparison')).not.toHaveLength(0);
  });
});

describe('what is standing on the lot', () => {
  it('states every band as a number and not only as a bar', async () => {
    mockApi({ '/reporting/month': { ok: true, body: august }, ...noOrganization });
    renderDashboard();

    const stock = await screen.findByLabelText('How old the stock is');
    expect(within(stock).getByText('0 to 30 days')).toBeVisible();
    expect(within(stock).getByText('Over 90 days')).toBeVisible();

    // A width alone cannot be read out, and cannot be read precisely by anybody.
    expect(within(stock).getByText('4')).toBeVisible();
  });

  it('names the oldest cars and links straight to them', async () => {
    mockApi({ '/reporting/month': { ok: true, body: august }, ...noOrganization });
    renderDashboard();

    const link = await screen.findByRole('link', { name: 'NAG-1042' });
    expect(link).toHaveAttribute('href', '/inventory?stock=NAG-1042');
  });

  it('admits when an age was estimated rather than recorded', async () => {
    mockApi({
      '/reporting/month': {
        ok: true,
        body: {
          ...august,
          stock: {
            ...august.stock!,
            oldest: [{ ...august.stock!.oldest[0]!, ageIsEstimated: true }],
          },
        },
      },
      ...noOrganization,
    });
    renderDashboard();

    expect(await screen.findByText(/no acquisition date was recorded/)).toBeVisible();
  });

  it('says the lot is empty rather than drawing an empty chart', async () => {
    mockApi({
      '/reporting/month': {
        ok: true,
        body: { ...august, stock: { asOf: '2026-08-07', units: 0, bands: [], oldest: [] } },
      },
      ...noOrganization,
    });
    renderDashboard();

    expect(await screen.findByText('Nothing unsold on the lot.')).toBeVisible();
  });
});

describe('what the reader is not allowed to see', () => {
  it('says what it is withholding rather than leaving a blank panel', async () => {
    // A blank panel reads as "the dealership sold nothing", which is a very
    // different statement from "this is not yours to see".
    mockApi({
      '/reporting/month': {
        ok: true,
        body: { ...august, trading: null, priorMonth: null, withheld: ['Trading'] },
      },
      ...noOrganization,
    });
    renderDashboard();

    expect(await screen.findByText(/money on this month is not yours to see/)).toBeVisible();
    expect(screen.getByLabelText('How old the stock is')).toBeVisible();
  });

  it('says so plainly when none of it is theirs', async () => {
    mockApi({
      '/reporting/month': {
        ok: false,
        status: 403,
        code: 'reporting.forbidden',
        detail: 'No.',
      },
      ...noOrganization,
    });
    renderDashboard();

    expect(await screen.findByRole('alert')).toHaveTextContent(/do not have access/);
  });

  it('offers a retry when the server fails', async () => {
    mockApi({
      '/reporting/month': [
        { ok: false, status: 500, code: 'server', detail: 'Something broke.' },
        { ok: true, body: august },
      ],
      ...noOrganization,
    });
    renderDashboard();

    await userEvent.click(await screen.findByRole('button', { name: 'Try again' }));

    expect(within(await tiles()).getByText('$57,000')).toBeVisible();
  });
});

describe('moving between months', () => {
  it('asks the server for the month before without leaving the page', async () => {
    mockApi({ '/reporting/month': { ok: true, body: august }, ...noOrganization });
    renderDashboard();
    await tiles();

    const first = monthAsked();
    await userEvent.click(screen.getByRole('button', { name: 'Previous month' }));

    expect(monthAsked()).not.toEqual(first);
    // Still the dashboard. Moving month is a change to this view, not a
    // different page.
    expect(screen.getByRole('group', { name: 'Which month' })).toBeVisible();
  });

  it('moves month from the keyboard, and back again', async () => {
    mockApi({ '/reporting/month': { ok: true, body: august }, ...noOrganization });
    renderDashboard();
    await tiles();

    const first = monthAsked();

    // "[[" is user-event's escape for a literal "[" — a single one is the start
    // of a key descriptor.
    await userEvent.keyboard('[[');
    const back = monthAsked();
    expect(back).not.toEqual(first);

    await userEvent.keyboard('t');
    expect(monthAsked()).toEqual(first);
  });

  it('will not step past this month from the keyboard either', async () => {
    // The button for it is disabled; a shortcut that ignored that would be a
    // second, quieter set of rules.
    mockApi({ '/reporting/month': { ok: true, body: august }, ...noOrganization });
    renderDashboard();
    await tiles();

    const first = monthAsked();
    await userEvent.keyboard(']');

    expect(monthAsked()).toEqual(first);
  });

  it('will not offer a month that has not happened', async () => {
    mockApi({ '/reporting/month': { ok: true, body: august }, ...noOrganization });
    renderDashboard();
    await tiles();

    expect(screen.getByRole('button', { name: 'Next month' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'This month' })).toBeDisabled();
  });

  it('remembers which rooftop was being looked at', async () => {
    mockApi({
      '/reporting/month': { ok: true, body: august },
      '/organization': {
        ok: true,
        body: {
          legalEntities: [
            {
              id: 'e1',
              name: 'North Group',
              rooftops: [
                { id: 'r1', name: 'Northgate', code: 'NAG-01', timeZone: 'UTC', legalEntityId: 'e1' },
                { id: 'r2', name: 'Southgate', code: 'NAG-02', timeZone: 'UTC', legalEntityId: 'e1' },
              ],
            },
          ],
        },
      },
    });
    renderDashboard();

    await userEvent.selectOptions(await screen.findByLabelText('Rooftop'), 'r2');

    expect(apiCalls().some((call) => call.path.includes('rooftopId=r2'))).toBe(true);
    expect(localStorage.getItem('dfoss.dashboard.rooftop')).toBe('r2');
  });
});

/** The headline figures, once they have arrived. */
function tiles(): Promise<HTMLElement> {
  return screen.findByLabelText('What the month made');
}

/** The query on the most recent month request, for comparing before and after. */
function monthAsked(): string {
  return [...apiCalls()].reverse().find((call) => call.path.startsWith('/reporting/month'))!.path;
}
