// PeriodsPage.test — closing the month on screen.
//
// Use:  npm test.
// Edit: two tests are safeguards rather than coverage.
//
//       "asks before closing" — closing locks real figures, and the server is
//       right to do exactly what a properly authorized request tells it. So the
//       check belongs here, the same reasoning as suspending a dealership.
//
//       "will not reopen without a reason" — the reason is what makes a reopened
//       month honest afterwards. The server demands one too; this stops somebody
//       discovering that only after clicking.

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { MemoryRouter } from 'react-router';
import { PeriodsPage } from './PeriodsPage';
import { apiCalls, mockApi } from '../../test/setup';
import { setCurrentTenant } from '../../shared/api';
import type { AccountingPeriodView } from '../../shared/contracts';

const open: AccountingPeriodView = {
  id: 'p1',
  year: 2026,
  month: 8,
  state: 'Open',
  startsOn: '2026-08-01',
  endsOn: '2026-08-31',
  closedAt: null,
  closedByUserId: null,
  entries: 12,
  history: [
    {
      fromState: null,
      toState: 'Open',
      occurredAt: '2026-08-01T09:00:00Z',
      changedByUserId: 'u1',
      note: 'Opened when the dealership was set up.',
    },
  ],
};

const closed: AccountingPeriodView = {
  ...open,
  id: 'p0',
  month: 7,
  state: 'Closed',
  startsOn: '2026-07-01',
  endsOn: '2026-07-31',
  closedAt: '2026-08-04T16:00:00Z',
  closedByUserId: 'u1',
  entries: 40,
  history: [
    {
      fromState: null,
      toState: 'Open',
      occurredAt: '2026-07-01T09:00:00Z',
      changedByUserId: 'u1',
      note: null,
    },
    {
      fromState: 'Open',
      toState: 'Closed',
      occurredAt: '2026-08-04T16:00:00Z',
      changedByUserId: 'u1',
      note: 'Month-end done.',
    },
  ],
};

function renderPeriods() {
  setCurrentTenant('northgroup');

  return render(
    <MemoryRouter initialEntries={['/accounting/periods']}>
      <PeriodsPage />
    </MemoryRouter>,
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe('the books', () => {
  it('shows each month, its cutoff, and how many entries it holds', async () => {
    mockApi({ '/accounting/periods': { ok: true, body: [open, closed] } });
    renderPeriods();

    expect(await screen.findByText('August 2026')).toBeVisible();
    expect(screen.getByText('July 2026')).toBeVisible();
    expect(screen.getByText('12')).toBeVisible();
    expect(screen.getByText('40')).toBeVisible();
  });

  it('says plainly that nothing posts into an unopened or closed month', async () => {
    mockApi({ '/accounting/periods': { ok: true, body: [open] } });
    renderPeriods();

    expect(await screen.findByText(/until its books are open/i)).toBeVisible();
    expect(screen.getByText(/no date that does it for you/i)).toBeVisible();
  });

  it('offers Close on an open month and Reopen on a closed one', async () => {
    mockApi({ '/accounting/periods': { ok: true, body: [open, closed] } });
    renderPeriods();

    expect(await screen.findByRole('button', { name: 'Close it' })).toBeVisible();
    expect(screen.getByRole('button', { name: 'Reopen' })).toBeVisible();
  });

  it('says nothing can post when no month is open at all', async () => {
    mockApi({ '/accounting/periods': { ok: true, body: [] } });
    renderPeriods();

    expect(await screen.findByText(/Nothing can be posted until you open one/i)).toBeVisible();
  });

  it('says plainly when the caller may not see the accounts', async () => {
    mockApi({
      '/accounting/periods': { ok: false, status: 403, code: 'accounting.forbidden', detail: 'No.' },
    });
    renderPeriods();

    expect(await screen.findByText(/do not have access to the accounts/i)).toBeVisible();
  });
});

describe('closing a month', () => {
  it('asks before closing, because it locks real figures', async () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    mockApi({ '/accounting/periods': { ok: true, body: [open] } });
    renderPeriods();

    await userEvent.click(await screen.findByRole('button', { name: 'Close it' }));

    expect(confirm).toHaveBeenCalled();
    expect(apiCalls().some((c) => c.path.includes('/close'))).toBe(false);
  });

  it('closes when the question is answered yes', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    mockApi({
      '/accounting/periods/2026/8/close': { ok: true, body: { ...open, state: 'Closed' } },
      '/accounting/periods': { ok: true, body: [open] },
    });
    renderPeriods();

    await userEvent.click(await screen.findByRole('button', { name: 'Close it' }));

    expect(apiCalls().some((c) => c.path === '/accounting/periods/2026/8/close')).toBe(true);
  });

  it('shows the server refusal rather than predicting it', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    mockApi({
      '/accounting/periods/2026/8/close': {
        ok: false,
        status: 403,
        code: 'accounting.period_forbidden',
        detail: 'Closing the books needs organization-wide permission.',
      },
      '/accounting/periods': { ok: true, body: [open] },
    });
    renderPeriods();

    await userEvent.click(await screen.findByRole('button', { name: 'Close it' }));

    expect(await screen.findByText(/organization-wide permission/i)).toBeVisible();
  });
});

describe('reopening a month', () => {
  it('will not reopen without a reason', async () => {
    mockApi({ '/accounting/periods': { ok: true, body: [open, closed] } });
    renderPeriods();

    await userEvent.click(await screen.findByRole('button', { name: 'Reopen' }));

    expect(await screen.findByRole('button', { name: 'Reopen it' })).toBeDisabled();
    expect(screen.getByText(/may already have been reported/i)).toBeVisible();
  });

  it('sends the reason with the reopen', async () => {
    mockApi({
      '/accounting/periods/2026/7/reopen': { ok: true, body: { ...closed, state: 'Open' } },
      '/accounting/periods': { ok: true, body: [open, closed] },
    });
    renderPeriods();

    await userEvent.click(await screen.findByRole('button', { name: 'Reopen' }));
    await userEvent.type(
      screen.getByLabelText('Why is it being reopened?'),
      'A supplier invoice arrived on the 4th',
    );
    await userEvent.click(screen.getByRole('button', { name: 'Reopen it' }));

    const call = apiCalls().find((c) => c.path === '/accounting/periods/2026/7/reopen');
    expect(JSON.parse(String(call?.init?.body))).toEqual({
      note: 'A supplier invoice arrived on the 4th',
    });
  });

  it('shows what has happened to the books', async () => {
    mockApi({ '/accounting/periods': { ok: true, body: [open, closed] } });
    renderPeriods();

    expect(await screen.findByText(/Month-end done\./)).toBeVisible();
  });
});
