// SecondFactorSetup.test — the three steps, and the two properties that stop
// this screen locking somebody out of their own account.
//
// Use:  npm test
// Edit: the two worth guarding hardest are (1) nothing changes about signing in
//       until a code is accepted, and (2) a wrong code leaves the person on the
//       same screen able to try again. Both are what make it safe for a
//       dealership to switch the requirement on.

import { render, screen, waitFor, within } from '../../test/render';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { App } from '../../app/App';
import { SecondFactorSetup } from './SecondFactorSetup';
import { SessionProvider } from '../../app/session';
import { apiCalls, mockApi } from '../../test/setup';
import { setCurrentTenant } from '../../shared/api';

const enrolment = {
  secret: 'JBSWY3DPEHPK3PXP',
  enrolmentUri: 'otpauth://totp/DealerFOSS:sales@dev.local?secret=JBSWY3DPEHPK3PXP&issuer=DealerFOSS&digits=6&period=30',
};

const recoveryCodes = [
  '7QK4-2M9X', 'PD3R-8T6W', 'ZC5N-1H7B', 'K2VF-9L4S', 'RT8M-6J3D',
];

/** The screen on its own, with a session that does not demand anything. */
function renderAlone() {
  mockApi({
    '/auth/me': { ok: true, body: { userId: 'u1', mustEnrolSecondFactor: false } },
    '/auth/mfa/enrol': { ok: true, body: enrolment },
    '/auth/mfa/confirm': { ok: true, body: { recoveryCodes } },
  });

  // The language provider comes from test/render — every screen reads its words
  // from it, so a mount without one is not a lighter test but a tree the
  // application never builds.
  return render(
    <SessionProvider>
      <SecondFactorSetup />
    </SessionProvider>,
  );
}

describe('setting up two-step sign-in', () => {
  it('explains what is about to happen before doing anything', async () => {
    renderAlone();

    expect(await screen.findByRole('heading', { name: 'Two-step sign-in' })).toBeVisible();
    expect(screen.getByText(/six-digit code from an app on your phone/)).toBeVisible();

    // Nothing has been asked of the server yet: opening the page must not
    // quietly replace a secret somebody is in the middle of scanning.
    expect(apiCalls().some((c) => c.path.startsWith('/auth/mfa/enrol'))).toBe(false);
  });

  it('draws a scannable square and offers the secret for typing', async () => {
    renderAlone();
    await userEvent.click(await screen.findByRole('button', { name: 'Start' }));

    // findByTitle matches the SVG's <title> node, which is a label rather than
    // something drawn. The square itself is the element that has to be visible.
    const label = await screen.findByTitle('Scan this with your authenticator app');
    const square = label.closest('svg');
    expect(square).toBeVisible();

    // A QR code that encodes the wrong thing is worse than none: the app would
    // produce codes the server rejects, and nobody could tell why.
    expect(square).toHaveAttribute('viewBox');
    expect(square!.querySelectorAll('path').length).toBeGreaterThan(0);

    // The manual fallback matters: plenty of people set this up on the same
    // device they are reading it on, where there is no camera to point.
    await userEvent.click(screen.getByText('Can’t scan it?'));
    expect(screen.getByText('JBSWY3DPEHPK3PXP')).toBeVisible();
  });

  it('says plainly that nothing has changed until the code is accepted', async () => {
    renderAlone();
    await userEvent.click(await screen.findByRole('button', { name: 'Start' }));

    expect(
      await screen.findByText(/Nothing has changed about signing in yet/),
    ).toBeVisible();
  });

  it('shows the recovery codes once the code is accepted', async () => {
    renderAlone();
    await userEvent.click(await screen.findByRole('button', { name: 'Start' }));

    await userEvent.type(await screen.findByLabelText(/enter the code it shows/i), '123456');
    await userEvent.click(screen.getByRole('button', { name: 'Turn it on' }));

    expect(await screen.findByText(/Two-step sign-in is on/)).toBeVisible();

    const list = screen.getByRole('list', { name: 'Recovery codes' });
    expect(within(list).getAllByRole('listitem')).toHaveLength(5);
    expect(within(list).getByText('7QK4-2M9X')).toBeVisible();
    expect(screen.getByText(/only time they will ever be shown/)).toBeVisible();
  });

  it('lets somebody try again after a wrong code, on the same screen', async () => {
    mockApi({
      '/auth/me': { ok: true, body: { userId: 'u1', mustEnrolSecondFactor: false } },
      '/auth/mfa/enrol': { ok: true, body: enrolment },
      '/auth/mfa/confirm': [
        { ok: false, status: 400, code: 'auth.mfa_code_invalid', detail: 'That code was not right.' },
        { ok: true, body: { recoveryCodes } },
      ],
    });

    render(
      <SessionProvider>
        <SecondFactorSetup />
      </SessionProvider>,
    );

    await userEvent.click(await screen.findByRole('button', { name: 'Start' }));
    const field = await screen.findByLabelText(/enter the code it shows/i);

    await userEvent.type(field, '000000');
    await userEvent.click(screen.getByRole('button', { name: 'Turn it on' }));

    expect(await screen.findByText('That code was not right.')).toBeVisible();
    // Cleared, so the next attempt starts from an empty field rather than
    // requiring somebody to select six stale digits first.
    expect(field).toHaveValue('');

    await userEvent.type(field, '654321');
    await userEvent.click(screen.getByRole('button', { name: 'Turn it on' }));

    expect(await screen.findByText(/Two-step sign-in is on/)).toBeVisible();
  });
});

describe('when the dealership requires it', () => {
  it('sends the person straight there and hides everything else', async () => {
    setCurrentTenant('northgroup');
    mockApi({
      '/auth/me': { ok: true, body: { userId: 'u1', mustEnrolSecondFactor: true } },
      '/auth/mfa/enrol': { ok: true, body: enrolment },
    });

    render(<App />);

    expect(await screen.findByRole('heading', { name: 'Two-step sign-in' })).toBeVisible();
    expect(screen.getByRole('alert')).toHaveTextContent(/only screen you can use/);

    // The stock list is not merely hidden — it is not reachable, because the
    // server would refuse it anyway and a dead link is worse than no link.
    expect(screen.queryByRole('link', { name: 'Stock' })).not.toBeInTheDocument();

    // But they can always leave a shared machine.
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeVisible();
  });

  it('opens the rest of the application as soon as it is done, without signing in again', async () => {
    setCurrentTenant('northgroup');
    mockApi({
      '/auth/me': [
        { ok: true, body: { userId: 'u1', mustEnrolSecondFactor: true } },
        { ok: true, body: { userId: 'u1', mustEnrolSecondFactor: false } },
      ],
      '/auth/mfa/enrol': { ok: true, body: enrolment },
      '/auth/mfa/confirm': { ok: true, body: { recoveryCodes } },
      '/inventory': { ok: true, body: [] },
    });

    render(<App />);
    await userEvent.click(await screen.findByRole('button', { name: 'Start' }));

    await userEvent.type(await screen.findByLabelText(/enter the code it shows/i), '123456');
    await userEvent.click(screen.getByRole('button', { name: 'Turn it on' }));

    // The obligation lifts server-side the moment the code is accepted, and the
    // browser asks again rather than making somebody sign in a second time.
    await waitFor(() =>
      expect(screen.getByRole('link', { name: 'Stock' })).toBeVisible(),
    );
  });
});
