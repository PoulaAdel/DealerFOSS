// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   adminApi — the browser's other conversation, with the control plane.
//
// Usage:
//   await adminApi<TenantRow[]>('/tenants')
//
// Coding Instructions:
//   This is a second client on purpose, not a flag on `shared/api.ts`. The
//   two worlds use different cookies and different anti-forgery headers
//   because during a support visit one browser holds both sets at once, and
//   a client that decided which to send from a boolean would eventually send
//   a dealership's token to the control plane, or the reverse. Two functions
//   cannot make that mistake.
//
//   Note also what is absent: no tenant header. An administrator belongs to
//   no dealership, and adding one here would be the first step towards the
//   separation this whole area exists to keep.

const ANTI_FORGERY_COOKIE = 'dfoss_admin_csrf';
const ANTI_FORGERY_HEADER = 'X-Admin-CSRF-Token';

/** Methods that change nothing. Everything else carries an anti-forgery token. */
const SAFE_METHODS = ['GET', 'HEAD', 'OPTIONS'];

export { ApiError } from './api';
import { ApiError } from './api';

type Problem = { code?: string; title?: string; detail?: string };

function antiForgeryToken(): string {
  const value = document.cookie.match(
    new RegExp(`(?:^|;\\s*)${ANTI_FORGERY_COOKIE}=([^;]*)`),
  )?.[1];

  return value === undefined ? '' : decodeURIComponent(value);
}

/**
 * Calls the control plane. `path` is relative to /api/v1/admin.
 *
 * `tenant` names the dealership for the one call that needs it — opening support
 * access. Every other endpoint here refuses to know about one.
 */
export async function adminApi<T>(
  path: string,
  init?: RequestInit & { tenant?: string },
): Promise<T> {
  const method = (init?.method ?? 'GET').toUpperCase();
  let response: Response;

  try {
    response = await fetch(`/api/v1/admin${path}`, {
      ...init,
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
        ...(SAFE_METHODS.includes(method)
          ? {}
          : { [ANTI_FORGERY_HEADER]: antiForgeryToken() }),
        ...(init?.tenant === undefined ? {} : { 'X-Tenant': init.tenant }),
        ...init?.headers,
      },
    });
  } catch {
    throw new ApiError(0, 'network', 'Could not reach the server. Is it running?');
  }

  if (!response.ok) {
    let problem: Problem = {};
    try {
      problem = (await response.json()) as Problem;
    } catch {
      // A response with no JSON body; the status is all we have.
    }

    throw new ApiError(
      response.status,
      problem.code ?? problem.title ?? 'unknown',
      problem.detail ?? `The server answered ${response.status}.`,
    );
  }

  return response.status === 204 ? (undefined as T) : ((await response.json()) as T);
}

export function adminPost<T>(
  path: string,
  body: unknown,
  tenant?: string,
): Promise<T> {
  return adminApi<T>(path, { method: 'POST', body: JSON.stringify(body), tenant });
}
