// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   api.test — the rules every call obeys, whether or not each caller remembers.
//
// Usage:
//   npm test
//
// Coding Instructions:
//   The anti-forgery rule is the one worth guarding hardest. It has to hold
//   for every write without any screen opting in, because the screen added
//   next year will not think about it.

import { describe, expect, it } from 'vitest';
import { ApiError, api, post, setCurrentTenant } from './api';
import { headerOn, mockApi } from '../test/setup';

describe('talking to the API', () => {
  it('sends the dealer group on every call', async () => {
    setCurrentTenant('northgroup');
    mockApi({ '/inventory': { ok: true, body: [] } });

    await api('/inventory');

    expect(headerOn('/inventory', 'X-Tenant')).toBe('northgroup');
  });

  it('carries the anti-forgery token on a write', async () => {
    mockApi({ '/customers': { ok: true, body: {} } });

    await post('/customers', { firstName: 'Ada' });

    expect(headerOn('/customers', 'X-CSRF-Token')).toBe('test-token');
  });

  it('does not bother sending one on a read', async () => {
    mockApi({ '/inventory': { ok: true, body: [] } });

    await api('/inventory');

    expect(headerOn('/inventory', 'X-CSRF-Token')).toBeUndefined();
  });

  it('sends an empty token rather than inventing one when signed out', async () => {
    // Clearing the cookie the setup file plants.
    document.cookie = 'dfoss_csrf=; expires=Thu, 01 Jan 1970 00:00:00 GMT';
    mockApi({ '/customers': { ok: true, body: {} } });

    await post('/customers', {});

    // The server refuses this, and its refusal is more accurate than a guess
    // made here would be.
    expect(headerOn('/customers', 'X-CSRF-Token')).toBe('');
  });

  it('surfaces the server’s own words, not a substitute', async () => {
    mockApi({
      '/deals': {
        ok: false, status: 409, code: 'deals.unit_committed',
        detail: 'That car is already on another deal.',
      },
    });

    await expect(post('/deals', {})).rejects.toThrow('That car is already on another deal.');
  });

  it('treats a failed anti-forgery check as a reason to sign in again', async () => {
    mockApi({
      '/customers': {
        ok: false, status: 403, code: 'auth.antiforgery_failed', detail: 'Refused.',
      },
    });

    const failure = await post('/customers', {}).catch((e: unknown) => e);

    expect(failure).toBeInstanceOf(ApiError);
    expect((failure as ApiError).needsSignIn).toBe(true);
  });

  it('treats an ordinary refusal as an answer to show, not a sign-in prompt', async () => {
    mockApi({
      '/inventory': { ok: false, status: 403, code: 'access.denied', detail: 'No.' },
    });

    const failure = await api('/inventory').catch((e: unknown) => e);

    expect((failure as ApiError).needsSignIn).toBe(false);
  });

  it('handles a 204 without trying to parse a body', async () => {
    mockApi({ '/auth/logout': { ok: true, status: 204 } });

    await expect(post('/auth/logout', {})).resolves.toBeUndefined();
  });
});
