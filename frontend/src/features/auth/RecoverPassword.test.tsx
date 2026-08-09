// RecoverPassword.test — the way back in, and what the screen must not do to be
// helpful.
//
// Use:  npm test.
// Edit: the test worth guarding hardest is that the screen never asks the server
//       about a specific email before the person commits to a reset. The server
//       deliberately answers every failure identically; a screen that "helpfully"
//       checked an address first would hand back exactly the answer the server
//       spent effort withholding.

import { render, screen } from '../../test/render';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router';
import { RecoverPassword } from './RecoverPassword';
import { apiCalls, mockApi } from '../../test/setup';
import { setCurrentTenant } from '../../shared/api';

const bothMethods = {
  ok: true as const,
  body: { authenticator: true, issuedCode: true, email: false, textMessage: false },
};

function show(extra: Record<string, unknown> = {}) {
  setCurrentTenant('northgroup');
  mockApi({ '/auth/recover': bothMethods, ...extra });

  return render(
    <MemoryRouter initialEntries={['/recover']}>
      <RecoverPassword />
    </MemoryRouter>,
  );
}

async function chooseManagerCode() {
  await userEvent.click(await screen.findByRole('button', { name: 'Use a code from my manager' }));
}

async function fillIn(code: string, password: string, again = password) {
  await userEvent.type(screen.getByLabelText('Email'), 'sam@dev.local');
  await userEvent.type(screen.getByLabelText(/code your manager gave you/i), code);
  await userEvent.type(screen.getByLabelText('New password'), password);
  await userEvent.type(screen.getByLabelText('New password again'), again);
}

describe('getting back into an account', () => {
  it('offers the ways this installation actually supports', async () => {
    show();

    expect(await screen.findByRole('button', { name: 'Use my authenticator app' })).toBeVisible();
    expect(screen.getByRole('button', { name: 'Use a code from my manager' })).toBeVisible();
  });

  it('never asks the server about a particular email', async () => {
    show();
    await chooseManagerCode();
    await fillIn('ABCD-EFGH-JKLM', 'A-Long-Enough-Pass1!');

    // The methods call carries no email, and nothing else is sent until the
    // person submits. Asking "does this address exist" would hand back the
    // answer the server refuses to give.
    const lookups = apiCalls().filter((call) => call.path.includes('/auth/recover'));
    expect(lookups).toHaveLength(1);
    expect(lookups[0]?.path).toBe('/auth/recover');
    expect(lookups[0]?.path).not.toContain('sam@dev.local');
  });

  it('sets the password in one call, not a prove-then-redeem pair', async () => {
    show({ '/auth/recover/code': { ok: true, status: 204 } });
    await chooseManagerCode();
    await fillIn('ABCD-EFGH-JKLM', 'A-Long-Enough-Pass1!');
    await userEvent.click(screen.getByRole('button', { name: 'Set my password' }));

    // The server has no intermediate ticket. A second step here would be theatre
    // implying a security property that does not exist.
    const writes = apiCalls().filter((call) => call.init?.method === 'POST');
    expect(writes).toHaveLength(1);
    expect(writes[0]?.path).toBe('/auth/recover/code');
  });

  it('says the password changed and everything was signed out', async () => {
    show({ '/auth/recover/code': { ok: true, status: 204 } });
    await chooseManagerCode();
    await fillIn('ABCD-EFGH-JKLM', 'A-Long-Enough-Pass1!');
    await userEvent.click(screen.getByRole('button', { name: 'Set my password' }));

    expect(await screen.findByRole('heading', { name: 'That is done' })).toBeVisible();
    expect(screen.getByText(/every device that was signed in has been signed out/i)).toBeVisible();
  });

  it('catches two different passwords before sending anything', async () => {
    show({ '/auth/recover/code': { ok: true, status: 204 } });
    await chooseManagerCode();
    await fillIn('ABCD-EFGH-JKLM', 'A-Long-Enough-Pass1!', 'A-Different-Pass2!');
    await userEvent.click(screen.getByRole('button', { name: 'Set my password' }));

    expect(await screen.findByText('Those two do not match.')).toBeVisible();

    // A typo-catcher, not a security control — but there is no reason to spend a
    // single-use code on one.
    expect(apiCalls().filter((call) => call.init?.method === 'POST')).toHaveLength(0);
  });

  it('shows the server’s refusal exactly as it came, without interpreting it', async () => {
    show({
      '/auth/recover/code': {
        ok: false,
        status: 403,
        code: 'recovery.refused',
        detail: 'That did not work. Check the email address and the code, and try again.',
      },
    });

    await chooseManagerCode();
    await fillIn('WRON-GCOD-EXXX', 'A-Long-Enough-Pass1!');
    await userEvent.click(screen.getByRole('button', { name: 'Set my password' }));

    // Deliberately vague, and the screen must not improve on it. "That email is
    // not registered" would undo what the single refusal protects.
    expect(await screen.findByText(/Check the email address and the code/)).toBeVisible();
    expect(screen.queryByText(/not registered|no such account|unknown/i)).not.toBeInTheDocument();
  });

  it('lets somebody who picked the wrong method go back', async () => {
    show();
    await chooseManagerCode();

    expect(screen.getByLabelText(/code your manager gave you/i)).toBeVisible();

    await userEvent.click(screen.getByRole('button', { name: 'Choose a different way' }));

    expect(await screen.findByRole('button', { name: 'Use my authenticator app' })).toBeVisible();
  });

  it('carries the language control, because a locked-out reader still has one', async () => {
    show();

    // Same reason it is on the sign-in screen: somebody who reads only Arabic
    // must not have to read English to find out how to stop reading English.
    expect(await screen.findByRole('combobox', { name: 'Language' })).toBeVisible();
  });
});
