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

describe('the application shell', () => {
  it('shows the sign-in form when nobody is signed in', async () => {
    mockApi({ '/auth/me': { ok: false, status: 401, code: 'auth.session_required', detail: 'Sign in.' } });

    render(<App />);

    expect(await screen.findByRole('heading', { name: 'OpenDealer360' })).toBeVisible();
    expect(screen.getByLabelText('Email')).toBeVisible();
    expect(screen.getByLabelText('Password')).toBeVisible();
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeEnabled();
  });

  it('shows the stock list and the navigation once signed in', async () => {
    setCurrentTenant('northgroup');
    mockApi({
      '/auth/me': { ok: true, body: { userId: 'u1', mustEnrolSecondFactor: false } },
      '/inventory': { ok: true, body: [] },
    });

    render(<App />);

    // The default route redirects to the stock list, so this is also the proof
    // that routing runs rather than merely compiling.
    expect(await screen.findByRole('heading', { name: 'Stock' })).toBeVisible();
    expect(screen.getByRole('link', { name: 'Trial balance' })).toBeVisible();
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeVisible();
    expect(screen.getByText('northgroup')).toBeVisible();
  });

  it('moves between screens when the navigation is used', async () => {
    setCurrentTenant('northgroup');
    mockApi({
      '/auth/me': { ok: true, body: { userId: 'u1', mustEnrolSecondFactor: false } },
      '/inventory': { ok: true, body: [] },
      '/accounting/trial-balance': {
        ok: true,
        body: {
          from: null, to: null, currency: 'USD',
          totalDebits: 0, totalCredits: 0, balances: true, accounts: [],
        },
      },
    });

    render(<App />);
    await screen.findByRole('heading', { name: 'Stock' });

    await userEvent.click(screen.getByRole('link', { name: 'Trial balance' }));

    expect(await screen.findByRole('heading', { name: 'Trial balance' })).toBeVisible();
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
    mockApi({
      '/auth/me': { ok: true, body: { userId: 'u1', mustEnrolSecondFactor: false } },
      '/auth/logout': { ok: true, status: 204 },
      '/inventory': { ok: true, body: [] },
    });

    render(<App />);
    await screen.findByRole('heading', { name: 'Stock' });

    await userEvent.click(screen.getByRole('button', { name: 'Sign out' }));

    await waitFor(() => expect(screen.getByRole('button', { name: 'Sign in' })).toBeVisible());
    expect(localStorage.getItem('odms.tenant')).toBeNull();
  });
});
