// adminApi.test — the rule that keeps the two worlds apart in the browser.
//
// Use:  npm test
// Edit: the header names are the point. During a support visit this browser
//       holds a dealership session *and* an administrator session at the same
//       time, so a client that guessed which to send would eventually guess
//       wrong. These tests are what stop the two clients being "simplified"
//       back into one.

import { describe, expect, it } from 'vitest';
import { adminApi, adminPost } from './adminApi';
import { api, post } from './api';
import { apiCalls, headerOn, mockApi } from '../test/setup';

describe('talking to the control plane', () => {
  it('carries the administrator’s own anti-forgery header on a write', async () => {
    document.cookie = 'odms_admin_csrf=admin-token';
    mockApi({ '/admin/tenants/x/status': { ok: true, body: {} } });

    await adminPost('/tenants/x/status', { status: 'Active' });

    expect(headerOn('/admin/tenants', 'X-Admin-CSRF-Token')).toBe('admin-token');
  });

  it('does not send the dealership’s token, even though the browser holds one', async () => {
    // Both cookies present, which is exactly the state during a support visit.
    document.cookie = 'odms_csrf=tenant-token';
    document.cookie = 'odms_admin_csrf=admin-token';
    mockApi({ '/admin/tenants/x/status': { ok: true, body: {} } });

    await adminPost('/tenants/x/status', { status: 'Active' });

    expect(headerOn('/admin/tenants', 'X-CSRF-Token')).toBeUndefined();
  });

  it('and the dealership client does not send the administrator’s', async () => {
    document.cookie = 'odms_csrf=tenant-token';
    document.cookie = 'odms_admin_csrf=admin-token';
    mockApi({ '/customers': { ok: true, body: {} } });

    await post('/customers', {});

    expect(headerOn('/customers', 'X-CSRF-Token')).toBe('tenant-token');
    expect(headerOn('/customers', 'X-Admin-CSRF-Token')).toBeUndefined();
  });

  it('sends no dealership header at all unless one is asked for', async () => {
    mockApi({ '/admin/tenants': { ok: true, body: [] } });

    await adminApi('/tenants');

    expect(headerOn('/admin/tenants', 'X-Tenant')).toBeUndefined();
  });

  it('names the dealership only where the endpoint needs it', async () => {
    document.cookie = 'odms_admin_csrf=admin-token';
    mockApi({ '/admin/support-access': { ok: true, body: {} } });

    await adminPost('/support-access', { reason: 'Ticket 1.', minutes: 60 }, 'northgroup');

    expect(headerOn('/admin/support-access', 'X-Tenant')).toBe('northgroup');
  });

  it('talks to a different address than the dealership client', async () => {
    mockApi({
      '/admin/tenants': { ok: true, body: [] },
      '/inventory': { ok: true, body: [] },
    });

    await adminApi('/tenants');
    await api('/inventory');

    expect(apiCalls().map((c) => c.path)).toEqual(['/admin/tenants', '/inventory']);
  });

  it('surfaces the control plane’s own refusal wording', async () => {
    mockApi({
      '/admin/support-access': {
        ok: false, status: 400, code: 'admin.support_reason_required',
        detail: 'Say why you need access.',
      },
    });

    await expect(adminPost('/support-access', {})).rejects.toThrow('Say why you need access.');
  });
});
