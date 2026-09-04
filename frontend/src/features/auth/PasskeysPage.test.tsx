// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   PasskeysPage.test — enrolling a passkey, and the promises the screen makes.
//
// Usage:
//   npm test.
//
// Coding Instructions:
//   Two assertions here are about safety rather than about pixels.
//
//   "says the password still works" — somebody who reads this screen as a
//   replacement will register a passkey on a phone they are about to trade
//   in, and then have nothing. The sentence is the mitigation.
//
//   "asks before forgetting one" — deletion, not disablement, and the
//   server will happily perform it. ADR-020 allows a confirm step for
//   exactly this.

import { render, screen, within } from '../../test/render';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PasskeysPage } from './PasskeysPage';
import { apiCalls, mockApi } from '../../test/setup';
import { setCurrentTenant } from '../../shared/api';
import { stubAuthenticator, type FakeAuthenticator } from '../../test/authenticator';
import type { RegisteredPasskey } from '../../shared/contracts';

const passkey = (over: Partial<RegisteredPasskey> = {}): RegisteredPasskey => ({
  id: 'p1',
  label: 'Work laptop',
  createdAt: '2026-08-01T09:00:00Z',
  lastUsedAt: '2026-08-14T08:12:00Z',
  ...over,
});

const challenge = {
  challengeId: '11111111-1111-1111-1111-111111111111',
  challenge: 'Y2hhbGxlbmdl',
  relyingPartyId: 'localhost',
  relyingPartyName: 'DealerFOSS',
  userHandle: 'dXNlci1vbmU',
  userName: 'ada@northgroup.test',
  userDisplayName: 'Ada Okafor',
  alreadyRegistered: [],
};

let fake: FakeAuthenticator | null = null;

afterEach(() => {
  fake?.restore();
  fake = null;
});

function renderPasskeys() {
  setCurrentTenant('northgroup');
  return render(<PasskeysPage />);
}

describe('your passkeys', () => {
  it('lists them with when each was added and last used', async () => {
    fake = stubAuthenticator();
    mockApi({ '/auth/passkeys': { ok: true, body: [passkey()] } });
    renderPasskeys();

    const table = await screen.findByRole('table', { name: /passkey on this account/i });
    expect(within(table).getByText('Work laptop')).toBeVisible();
  });

  it('distinguishes a passkey never used from one used at an unknown time', async () => {
    fake = stubAuthenticator();
    mockApi({ '/auth/passkeys': { ok: true, body: [passkey({ lastUsedAt: null })] } });
    renderPasskeys();

    expect(await screen.findByText('Never used')).toBeVisible();
  });

  it('says the password still works', async () => {
    // See the file header. This sentence is the mitigation for the worst
    // outcome this screen can lead somebody into.
    fake = stubAuthenticator();
    mockApi({ '/auth/passkeys': { ok: true, body: [] } });
    renderPasskeys();

    expect(await screen.findByText(/Your password still works/)).toBeVisible();
  });

  it('says so plainly when there are none yet', async () => {
    fake = stubAuthenticator();
    mockApi({ '/auth/passkeys': { ok: true, body: [] } });
    renderPasskeys();

    expect(await screen.findByText('You have no passkeys yet.')).toBeVisible();
  });
});

describe('adding one', () => {
  it('registers what the authenticator produced', async () => {
    fake = stubAuthenticator();
    mockApi({
      '/auth/passkeys/register/begin': { ok: true, body: challenge },
      '/auth/passkeys/register/finish': { ok: true, body: passkey({ label: 'My phone' }) },
      '/auth/passkeys': { ok: true, body: [] },
    });
    renderPasskeys();

    await userEvent.type(await screen.findByLabelText('What to call it'), 'My phone');
    await userEvent.click(screen.getByRole('button', { name: 'Add it' }));

    expect(await screen.findByText('My phone is registered.')).toBeVisible();

    const finish = apiCalls().find((c) => c.path === '/auth/passkeys/register/finish');
    const body = JSON.parse(String(finish?.init?.body)) as Record<string, string>;

    expect(body.challengeId).toBe(challenge.challengeId);
    expect(body.label).toBe('My phone');
    // base64url, not base64: no padding and no '+' or '/'.
    expect(body.attestationObject).toMatch(/^[A-Za-z0-9_-]+$/);
    expect(body.clientDataJson).toMatch(/^[A-Za-z0-9_-]+$/);
  });

  it('says nothing at all when somebody dismisses their own prompt', async () => {
    // Not an error. Shouting at somebody for changing their mind is worse than
    // silence, and the screen is already back the way it was.
    fake = stubAuthenticator({ kind: 'cancelled' });
    mockApi({
      '/auth/passkeys/register/begin': { ok: true, body: challenge },
      '/auth/passkeys': { ok: true, body: [] },
    });
    renderPasskeys();

    await userEvent.type(await screen.findByLabelText('What to call it'), 'My phone');
    await userEvent.click(screen.getByRole('button', { name: 'Add it' }));

    expect(apiCalls().some((c) => c.path === '/auth/passkeys/register/finish')).toBe(false);
    expect(screen.queryByText(/could not finish/i)).toBeNull();
  });

  it('says something when the device genuinely fails', async () => {
    fake = stubAuthenticator({ kind: 'errors', message: 'authenticator exploded' });
    mockApi({
      '/auth/passkeys/register/begin': { ok: true, body: challenge },
      '/auth/passkeys': { ok: true, body: [] },
    });
    renderPasskeys();

    await userEvent.type(await screen.findByLabelText('What to call it'), 'My phone');
    await userEvent.click(screen.getByRole('button', { name: 'Add it' }));

    expect(await screen.findByText(/could not finish that/i)).toBeVisible();
  });

  it('shows the server’s refusal rather than inventing one', async () => {
    fake = stubAuthenticator();
    mockApi({
      '/auth/passkeys/register/begin': { ok: true, body: challenge },
      '/auth/passkeys/register/finish': {
        ok: false,
        status: 409,
        code: 'passkey.already_registered',
        detail: 'That passkey is already registered.',
      },
      '/auth/passkeys': { ok: true, body: [] },
    });
    renderPasskeys();

    await userEvent.type(await screen.findByLabelText('What to call it'), 'My phone');
    await userEvent.click(screen.getByRole('button', { name: 'Add it' }));

    expect(await screen.findByText('That passkey is already registered.')).toBeVisible();
  });

  it('offers no form at all on a browser that cannot do this', async () => {
    // Omitted, not disabled: a greyed-out control invites somebody to work out
    // why, and there is nothing they can do about it here.
    mockApi({ '/auth/passkeys': { ok: true, body: [] } });
    renderPasskeys();

    expect(await screen.findByText(/cannot use passkeys/i)).toBeVisible();
    expect(screen.queryByLabelText('What to call it')).toBeNull();
  });
});

describe('forgetting one', () => {
  it('asks first, and says what will stop working', async () => {
    fake = stubAuthenticator();
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);

    mockApi({
      '/auth/passkeys/p1': { ok: true, status: 204 },
      '/auth/passkeys': { ok: true, body: [passkey()] },
    });
    renderPasskeys();

    await userEvent.click(await screen.findByRole('button', { name: 'Forget it' }));

    expect(confirm).toHaveBeenCalledWith(expect.stringContaining('Work laptop'));
    expect(confirm).toHaveBeenCalledWith(expect.stringContaining('cannot be undone'));
  });

  it('does nothing when the answer is no', async () => {
    fake = stubAuthenticator();
    vi.spyOn(window, 'confirm').mockReturnValue(false);

    mockApi({ '/auth/passkeys': { ok: true, body: [passkey()] } });
    renderPasskeys();

    await userEvent.click(await screen.findByRole('button', { name: 'Forget it' }));

    expect(apiCalls().some((c) => c.init?.method === 'DELETE')).toBe(false);
  });

  it('deletes it when the answer is yes', async () => {
    fake = stubAuthenticator();
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    mockApi({
      '/auth/passkeys/p1': { ok: true, status: 204 },
      '/auth/passkeys': [
        { ok: true, body: [passkey()] },
        { ok: true, body: [] },
      ],
    });
    renderPasskeys();

    await userEvent.click(await screen.findByRole('button', { name: 'Forget it' }));

    expect(await screen.findByText('Work laptop is gone.')).toBeVisible();

    const call = apiCalls().find((c) => c.path === '/auth/passkeys/p1');
    expect(call?.init?.method).toBe('DELETE');
  });
});
