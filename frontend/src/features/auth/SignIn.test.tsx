// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   SignIn.test — the passkey door, beside the password rather than in front of it.
//
// Usage:
//   npm test.
//
// Coding Instructions:
//   The placement is a decision, not a layout accident (2026-08-15, doc 11
//   §7), so it is asserted rather than left to a screenshot. The password
//   remains the primary path; the passkey is offered to whoever has one.
//
//   The other property worth keeping: the ceremony sends NO email address.
//   The credential identifies the account, and asking for an email first
//   would make this screen able to answer "does this person work here",
//   which the sign-in ceremony is deliberately built not to do.

import { render, screen } from '../../test/render';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router';
import { SignIn } from './SignIn';
import { SessionProvider } from '../../app/session';
import { apiCalls, mockApi } from '../../test/setup';
import { stubAuthenticator, type FakeAuthenticator } from '../../test/authenticator';

const challenge = {
  challengeId: '22222222-2222-2222-2222-222222222222',
  challenge: 'Y2hhbGxlbmdl',
  relyingPartyId: 'localhost',
};

let fake: FakeAuthenticator | null = null;

afterEach(() => {
  fake?.restore();
  fake = null;
});

/**
 * Signed out, so the provider answers `null` and the form is what renders.
 * `/auth/me` is arranged because SessionProvider asks for it on mount.
 */
const signedOut = {
  '/auth/me': { ok: false as const, status: 401, code: 'auth.session_required', detail: 'Sign in.' },
};

function renderSignIn() {
  return render(
    <MemoryRouter initialEntries={['/sign-in']}>
      <SessionProvider>
        <SignIn />
      </SessionProvider>
    </MemoryRouter>,
  );
}

describe('signing in with a passkey', () => {
  it('puts the button beside the password field, not before the form', async () => {
    fake = stubAuthenticator();
    mockApi(signedOut);
    const { container } = renderSignIn();

    const button = await screen.findByRole('button', { name: 'Use a passkey' });
    const password = screen.getByLabelText('Password');

    // Same row, and that row holds the password. Asserting on the shared parent
    // rather than on a class name: what the decision was about is the two
    // controls being on one line.
    expect(button.parentElement).toBe(password.parentElement);
    expect(container.querySelector('.signin__beside')).toContainElement(button);

    // ...and the password is still the primary path: the submit button is the
    // form's own, and it is not the passkey one.
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeVisible();
  });

  it('is not offered at all on a browser that cannot do WebAuthn', async () => {
    mockApi(signedOut);
    renderSignIn();

    await screen.findByLabelText('Password');
    expect(screen.queryByRole('button', { name: 'Use a passkey' })).toBeNull();
  });

  it('asks for a dealer group first, because that decides which records to open', async () => {
    fake = stubAuthenticator();
    mockApi(signedOut);
    renderSignIn();

    await userEvent.clear(await screen.findByLabelText('Dealer group'));
    await userEvent.click(screen.getByRole('button', { name: 'Use a passkey' }));

    expect(await screen.findByText(/Type your dealer group first/)).toBeVisible();
    expect(apiCalls().some((c) => c.path.startsWith('/auth/passkeys'))).toBe(false);
  });

  it('completes the ceremony without ever sending an email address', async () => {
    fake = stubAuthenticator();
    mockApi({
      ...signedOut,
      '/auth/passkeys/sign-in/begin': { ok: true, body: challenge },
      '/auth/passkeys/sign-in/finish': { ok: true, body: { expiresAt: '2026-08-16T09:00:00Z' } },
    });
    renderSignIn();

    await userEvent.type(await screen.findByLabelText('Dealer group'), 'northgroup');
    await userEvent.click(screen.getByRole('button', { name: 'Use a passkey' }));

    const finish = apiCalls().find((c) => c.path === '/auth/passkeys/sign-in/finish');
    expect(finish).toBeDefined();

    const body = JSON.parse(String(finish!.init?.body)) as Record<string, string>;
    expect(body.challengeId).toBe(challenge.challengeId);
    expect(Object.keys(body)).not.toContain('email');

    // Every ceremony field is base64url — no padding, no '+' or '/'.
    for (const field of ['clientDataJson', 'authenticatorData', 'signature']) {
      expect(body[field]).toMatch(/^[A-Za-z0-9_-]+$/);
    }
  });

  it('leaves the form alone when somebody dismisses their own prompt', async () => {
    fake = stubAuthenticator({ kind: 'cancelled' });
    mockApi({
      ...signedOut,
      '/auth/passkeys/sign-in/begin': { ok: true, body: challenge },
    });
    renderSignIn();

    await userEvent.type(await screen.findByLabelText('Dealer group'), 'northgroup');
    await userEvent.click(screen.getByRole('button', { name: 'Use a passkey' }));

    expect(apiCalls().some((c) => c.path.endsWith('/sign-in/finish'))).toBe(false);
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeEnabled();
  });

  it('shows the server’s refusal, and leaves the password available', async () => {
    // A refused passkey must never be a dead end. The refusal itself is
    // deliberately coarse — the server will not say which check failed.
    fake = stubAuthenticator();
    mockApi({
      ...signedOut,
      '/auth/passkeys/sign-in/begin': { ok: true, body: challenge },
      '/auth/passkeys/sign-in/finish': {
        ok: false,
        status: 401,
        code: 'passkey.not_accepted',
        detail: 'That passkey could not be accepted. Try again, or sign in with your password.',
      },
    });
    renderSignIn();

    await userEvent.type(await screen.findByLabelText('Dealer group'), 'northgroup');
    await userEvent.click(screen.getByRole('button', { name: 'Use a passkey' }));

    expect(await screen.findByText(/could not be accepted/)).toBeVisible();
    expect(screen.getByLabelText('Password')).toBeEnabled();
  });
});
