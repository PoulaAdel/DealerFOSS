// App.test — does this application actually draw?
//
// Use:  npm test
// Edit: these mount the real components into a real DOM and read what a person
//       would read. That is the whole point: before this file existed, "the
//       frontend has never been seen rendering" was an honest limitation nobody
//       could close without opening a browser. Assert on visible text and roles
//       rather than class names, so a restyle does not read as a regression.

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { App } from './App';
import { mockApi, mockApiUnreachable } from '../test/setup';
import { setCurrentTenant } from '../shared/api';
import type { MonthInReview } from '../shared/contracts';

/** Enough of a month for the landing screen to draw. */
const month: MonthInReview = {
  year: 2026,
  month: 8,
  startsOn: '2026-08-01',
  endsOn: '2026-08-31',
  books: 'Open',
  closedAt: null,
  trading: {
    from: '2026-08-01',
    to: '2026-08-31',
    currency: 'USD',
    departments: [],
    totalRevenue: 0,
    totalCost: 0,
    totalGross: 0,
    vehiclesDelivered: 0,
    serviceInvoices: 0,
  },
  priorMonth: null,
  stock: { asOf: '2026-08-07', units: 0, bands: [], oldest: [] },
  withheld: [],
};

/** What a signed-in browser asks for the moment it lands. */
const signedIn = {
  '/auth/me': { ok: true as const, body: { userId: 'u1', mustEnrolSecondFactor: false } },
  '/reporting/month': { ok: true as const, body: month },
  '/organization': { ok: true as const, body: { legalEntities: [] } },
};

describe('the application shell', () => {
  it('shows the sign-in form when nobody is signed in', async () => {
    mockApi({ '/auth/me': { ok: false, status: 401, code: 'auth.session_required', detail: 'Sign in.' } });

    render(<App />);

    expect(await screen.findByRole('heading', { name: 'DealerFOSS' })).toBeVisible();
    expect(screen.getByLabelText('Email')).toBeVisible();
    expect(screen.getByLabelText('Password')).toBeVisible();
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeEnabled();
  });

  it('lands on the month and shows the navigation once signed in', async () => {
    setCurrentTenant('northgroup');
    mockApi(signedIn);

    render(<App />);

    // The default route redirects to the dashboard, so this is also the proof
    // that routing runs rather than merely compiling. Asserted on the lede
    // rather than the heading, which is a month name and therefore a moving
    // target every first of the month.
    expect(await screen.findByText('So far this month.')).toBeVisible();
    expect(screen.getByRole('link', { name: 'Trial balance' })).toBeVisible();
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeVisible();
    expect(screen.getByText('northgroup')).toBeVisible();
  });

  it('moves between screens when the navigation is used', async () => {
    setCurrentTenant('northgroup');
    mockApi({
      ...signedIn,
      '/accounting/balances': {
        ok: true,
        body: {
          from: null, to: null, currency: 'USD',
          totalDebits: 0, totalCredits: 0, balances: true, accounts: [],
        },
      },
    });

    render(<App />);
    await screen.findByText('So far this month.');

    await userEvent.click(screen.getByRole('link', { name: 'Trial balance' }));

    expect(await screen.findByRole('heading', { name: 'Trial balance' })).toBeVisible();
  });

  it('takes a keyboard-only user between screens without a mouse', async () => {
    setCurrentTenant('northgroup');
    mockApi({ ...signedIn, '/inventory': { ok: true, body: [] } });

    render(<App />);
    await screen.findByText('So far this month.');

    // "g" then "s". A power user never reaches for the navigation.
    await userEvent.keyboard('gs');

    expect(await screen.findByRole('heading', { name: 'Stock' })).toBeVisible();
  });

  it('can be asked what the shortcuts are, because an unknown one is useless', async () => {
    setCurrentTenant('northgroup');
    mockApi(signedIn);

    render(<App />);
    await screen.findByText('So far this month.');

    await userEvent.keyboard('?');

    const panel = await screen.findByRole('dialog', { name: 'Keyboard shortcuts' });
    expect(panel).toBeVisible();
    expect(screen.getByText('Go to stock')).toBeVisible();
  });

  it('says the server is unreachable rather than showing a blank page', async () => {
    setCurrentTenant('northgroup');
    mockApiUnreachable();

    render(<App />);

    // An unreachable server cannot confirm a session, so the honest answer is
    // the sign-in form — not a spinner that never stops.
    expect(await screen.findByRole('button', { name: 'Sign in' })).toBeVisible();
  });

  it('offers a way out that clears the session', async () => {
    setCurrentTenant('northgroup');
    mockApi({ ...signedIn, '/auth/logout': { ok: true, status: 204 } });

    render(<App />);
    await screen.findByText('So far this month.');

    await userEvent.click(screen.getByRole('button', { name: 'Sign out' }));

    await waitFor(() => expect(screen.getByRole('button', { name: 'Sign in' })).toBeVisible());
    expect(localStorage.getItem('dfoss.tenant')).toBeNull();
  });
});
