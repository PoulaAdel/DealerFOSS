// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   App.test — does this application actually draw?
//
// Usage:
//   npm test
//
// Coding Instructions:
//   These mount the real components into a real DOM and read what a person
//   would read. That is the whole point: before this file existed, "the
//   frontend has never been seen rendering" was an honest limitation nobody
//   could close without opening a browser. Assert on visible text and roles
//   rather than class names, so a restyle does not read as a regression.

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { App } from './App';
import { mockApi, mockApiUnreachable, page } from '../test/setup';
import { setCurrentTenant } from '../shared/api';
import type { MonthInReview, RepairOrderDetail } from '../shared/contracts';

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
    mockApi({ ...signedIn, '/inventory': { ok: true, body: page([]) } });

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

/**
 * Records got their own addresses on 2026-09-16, which put a `:id?` segment on
 * five area routes. These are the two things that could quietly break as a
 * result, and neither would be visible in any screen's own tests.
 */
describe('the routes a record address sits beside', () => {
  const job: RepairOrderDetail = {
    id: '99999999-9999-9999-9999-999999999999',
    rooftopId: 'r1',
    rooftopCode: 'NAG-01',
    number: 'RO-1084',
    status: 'InProgress',
    customerId: 'c1',
    customerName: 'Priya Raman',
    vehicleId: 'v1',
    vehicle: '2021 Toyota RAV4',
    complaint: 'Brakes squeal',
    odometerReading: 41000,
    currency: 'USD',
    labourTotal: 0,
    partsTotal: 0,
    subletTotal: 0,
    amountDue: 0,
    warrantyTotal: 0,
    internalTotal: 0,
    workTotal: 0,
    advisorUserId: null,
    technicianUserId: null,
    openedAt: '2026-09-16T08:00:00Z',
    invoicedAt: null,
    linesAreOpen: true,
    availableMoves: [],
    lines: [],
    clockings: [],
    clockedHours: 0,
    history: [],
  };

  it('still reaches the labour report, which /workshop/:id could have swallowed', async () => {
    // React Router ranks a static segment above a dynamic one, so
    // `/workshop/labour` wins over `/workshop/:id`. That is documented
    // behaviour and it is also the kind of thing that would be discovered by a
    // service manager rather than by us, so it is asserted rather than trusted.
    setCurrentTenant('northgroup');
    window.history.pushState({}, '', '/workshop/labour');
    mockApi({
      ...signedIn,
      '/repair-orders/labour': {
        ok: true,
        body: {
          from: '2026-09-01', to: '2026-09-16',
          hoursSold: 0, labourRevenue: 0, effectiveLabourRate: 0,
          hoursClocked: 0, productivity: null,
          byTechnician: [], byPayer: [], notMeasured: ['Efficiency'],
        },
      },
      '/staff': { ok: true, body: [] },
    });

    render(<App />);

    expect(await screen.findByRole('heading', { name: 'Labour' })).toBeVisible();
  });

  it('opens a job from its own address, which is the point of the whole change', async () => {
    setCurrentTenant('northgroup');
    window.history.pushState({}, '', `/workshop/${job.id}`);
    mockApi({
      ...signedIn,
      [`/repair-orders/${job.id}`]: { ok: true, body: job },
      '/repair-orders': { ok: true, body: page([]) },
      '/appointments': { ok: true, body: { appointments: [], load: [] } },
    });

    render(<App />);

    // The band's own heading, which is what somebody following a link is here
    // to read. The list beneath it is empty in this arrangement on purpose: the
    // record arrived from the address, not from a row.
    expect(
      await screen.findByRole('heading', { name: /RO-1084 NAG-01 · Priya Raman/ }),
    ).toBeVisible();
  });

  it('keeps the address while somebody signs in, so a shared link survives it', async () => {
    // The commonest way a link is opened is by somebody who is signed out, or
    // whose session lapsed overnight. Redirecting to /sign-in threw the address
    // away and landed them on the dashboard — which is precisely the "describe
    // where to click" problem this change exists to end.
    window.history.pushState({}, '', `/workshop/${job.id}`);
    mockApi({
      '/auth/me': [
        { ok: false, status: 401, code: 'auth.session_required', detail: 'Sign in.' },
        signedIn['/auth/me'],
      ],
      '/reporting/month': signedIn['/reporting/month'],
      '/organization': signedIn['/organization'],
      '/auth/sign-in': { ok: true, body: { userId: 'u1', mustEnrolSecondFactor: false } },
      [`/repair-orders/${job.id}`]: { ok: true, body: job },
      '/repair-orders': { ok: true, body: page([]) },
      '/appointments': { ok: true, body: { appointments: [], load: [] } },
    });

    render(<App />);

    // The form appears where they asked to be, not at /sign-in.
    await screen.findByRole('button', { name: 'Sign in' });
    expect(window.location.pathname).toBe(`/workshop/${job.id}`);
  });
});
