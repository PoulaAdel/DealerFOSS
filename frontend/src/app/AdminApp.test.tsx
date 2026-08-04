// AdminApp.test — the console, and the separation it must not blur.
//
// Use:  npm test
// Edit: the two worth guarding hardest are that the console never asks a
//       dealership question of the control plane (no X-Tenant except on the one
//       call that needs it), and that a support visit cannot be opened without a
//       written reason. Both are the browser half of a property the server
//       already enforces — belt and braces, and the belt is the server.

import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { App } from './App';
import { apiCalls, mockApi } from '../test/setup';

const administrator = {
  administratorId: 'a1',
  email: 'root@control.local',
  mustEnrolSecondFactor: false,
};

const tenants = [
  {
    slug: 'northgroup', name: 'North Auto Group', status: 'Active',
    databaseVersion: '1.4', createdAt: '2026-07-30T00:00:00Z',
  },
  {
    slug: 'citymotors', name: 'City Motors', status: 'Suspended',
    databaseVersion: '1.4', createdAt: '2026-07-30T00:00:00Z',
  },
];

const grant = {
  id: 'g1', administratorId: 'a1', administratorEmail: 'root@control.local',
  tenantSlug: 'northgroup', reason: 'Ticket 4412: duplicated journal entry.',
  grantedAt: '2026-08-03T09:00:00Z', expiresAt: '2026-08-03T10:00:00Z',
  endedAt: null, isActive: true,
};

/** Puts the browser at /admin, which is a different world from /inventory. */
function atAdmin() {
  window.history.pushState({}, '', '/admin');
}

describe('the administration console', () => {
  it('asks for administrator credentials, not a dealership', async () => {
    atAdmin();
    mockApi({ '/admin/me': { ok: false, status: 401, code: 'admin.session_required', detail: 'No.' } });

    render(<App />);

    expect(await screen.findByLabelText('Email')).toBeVisible();
    expect(screen.getByText(/signs you in to the installation, not to a dealership/)).toBeVisible();

    // The dealership sign-in asks which dealer group. This one must not: an
    // administrator belongs to none.
    expect(screen.queryByLabelText('Dealer group')).not.toBeInTheDocument();

    // And it says so on the heading, not only in the body text. The two sign-in
    // screens are otherwise near-identical, which is worst at exactly the
    // moment somebody is typing a password.
    expect(screen.getByRole('heading', { name: /Administration/ })).toBeVisible();
  });

  it('never sends a dealership header when asking who is signed in', async () => {
    atAdmin();
    mockApi({
      '/admin/me': { ok: true, body: administrator },
      '/admin/tenants': { ok: true, body: tenants },
    });

    render(<App />);
    await screen.findByRole('heading', { name: 'Dealerships' });

    const headers = apiCalls()
      .filter((c) => c.path.startsWith('/admin/'))
      .map((c) => c.init?.headers as Record<string, string> | undefined);

    expect(headers.every((h) => h?.['X-Tenant'] === undefined)).toBe(true);
  });

  it('lists the dealerships with their state, and nothing about their business', async () => {
    atAdmin();
    mockApi({
      '/admin/me': { ok: true, body: administrator },
      '/admin/tenants': { ok: true, body: tenants },
    });

    render(<App />);

    expect(await screen.findByText('North Auto Group')).toBeVisible();
    expect(screen.getByText('City Motors')).toBeVisible();
    expect(screen.getByRole('button', { name: 'Suspend' })).toBeVisible();
    expect(screen.getByRole('button', { name: 'Resume' })).toBeVisible();
  });

  it('asks before taking a dealership out of service', async () => {
    atAdmin();
    mockApi({
      '/admin/me': { ok: true, body: administrator },
      '/admin/tenants': { ok: true, body: tenants },
      '/admin/tenants/northgroup/status': { ok: true, body: { slug: 'northgroup', status: 'Suspended' } },
    });

    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    render(<App />);

    await userEvent.click(await screen.findByRole('button', { name: 'Suspend' }));

    expect(confirm).toHaveBeenCalledOnce();
    // Said no, so nothing was sent. A suspension is a real outage for real
    // people, and a misclick must not cause one.
    expect(apiCalls().some((c) => c.path.includes('/status'))).toBe(false);
  });

  it('will not open a support visit without a written reason', async () => {
    atAdmin();
    window.history.pushState({}, '', '/admin/support-access');
    mockApi({
      '/admin/me': { ok: true, body: administrator },
      '/admin/tenants': { ok: true, body: tenants },
      '/admin/support-access': { ok: true, body: [] },
    });

    render(<App />);

    const button = await screen.findByRole('button', { name: 'Open access' });
    expect(button).toBeDisabled();

    await userEvent.type(screen.getByLabelText('Dealership'), 'northgroup');
    expect(button).toBeDisabled();

    await userEvent.type(screen.getByLabelText(/Why you need to go in/), 'Ticket 4412.');
    expect(button).toBeEnabled();
  });

  it('names the dealership on the one call that needs it, and says when it ends', async () => {
    atAdmin();
    window.history.pushState({}, '', '/admin/support-access');
    mockApi({
      '/admin/me': { ok: true, body: administrator },
      '/admin/tenants': { ok: true, body: tenants },
      '/admin/support-access': [
        { ok: true, body: [] },
        {
          ok: true,
          body: { grantId: 'g1', tenant: 'northgroup', expiresAt: '2026-08-03T10:00:00Z' },
        },
        { ok: true, body: [grant] },
      ],
    });

    render(<App />);

    await userEvent.type(await screen.findByLabelText('Dealership'), 'northgroup');
    await userEvent.type(screen.getByLabelText(/Why you need to go in/), 'Ticket 4412.');
    await userEvent.click(screen.getByRole('button', { name: 'Open access' }));

    expect(await screen.findByRole('status')).toHaveTextContent(/You are in northgroup until/);

    const opening = apiCalls().find(
      (c) => c.path === '/admin/support-access' && c.init?.method === 'POST',
    );
    expect((opening?.init?.headers as Record<string, string>)['X-Tenant']).toBe('northgroup');
  });

  it('keeps closed visits in the record rather than clearing them away', async () => {
    atAdmin();
    window.history.pushState({}, '', '/admin/support-access');
    mockApi({
      '/admin/me': { ok: true, body: administrator },
      '/admin/tenants': { ok: true, body: tenants },
      '/admin/support-access': {
        ok: true,
        body: [{ ...grant, isActive: false, endedAt: '2026-08-03T09:20:00Z' }],
      },
    });

    render(<App />);

    const table = await screen.findByRole('table');
    expect(within(table).getByText('Ticket 4412: duplicated journal entry.')).toBeVisible();
    expect(within(table).getByText(/^Closed /)).toBeVisible();
    expect(within(table).queryByRole('button', { name: 'Close now' })).not.toBeInTheDocument();
  });

  it('sends an unenrolled administrator to set up a second factor and nowhere else', async () => {
    atAdmin();
    mockApi({
      '/admin/me': { ok: true, body: { ...administrator, mustEnrolSecondFactor: true } },
    });

    render(<App />);

    expect(await screen.findByRole('heading', { name: 'Set up your second factor' })).toBeVisible();
    expect(screen.queryByRole('link', { name: 'Dealerships' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeVisible();
  });

  it('opens the console once the second factor is confirmed, without signing in again', async () => {
    atAdmin();
    mockApi({
      '/admin/me': [
        { ok: true, body: { ...administrator, mustEnrolSecondFactor: true } },
        { ok: true, body: administrator },
      ],
      '/admin/mfa/enrol': {
        ok: true,
        body: { secret: 'JBSWY3DPEHPK3PXP', enrolmentUri: 'otpauth://totp/x?secret=JBSWY3DPEHPK3PXP' },
      },
      '/admin/mfa/confirm': { ok: true, status: 204 },
      '/admin/tenants': { ok: true, body: tenants },
    });

    render(<App />);
    await userEvent.click(await screen.findByRole('button', { name: 'Start' }));

    await userEvent.type(await screen.findByLabelText(/enter the code it shows/i), '123456');
    await userEvent.click(screen.getByRole('button', { name: 'Turn it on' }));

    await waitFor(() => expect(screen.getByRole('link', { name: 'Dealerships' })).toBeVisible());
  });

  it('is visibly the console and not the dealership, so nobody confuses the two', async () => {
    atAdmin();
    mockApi({
      '/admin/me': { ok: true, body: administrator },
      '/admin/tenants': { ok: true, body: tenants },
    });

    render(<App />);
    await screen.findByRole('heading', { name: 'Dealerships' });

    expect(screen.getByText('Administration')).toBeVisible();
    expect(screen.getByText('root@control.local')).toBeVisible();
    // The dealership's own navigation must be nowhere near this screen.
    expect(screen.queryByRole('link', { name: 'Stock' })).not.toBeInTheDocument();
  });
});
