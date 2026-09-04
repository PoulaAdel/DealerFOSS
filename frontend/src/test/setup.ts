// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Test setup — the shared arrangements every component test relies on.
//
// Usage:
//   Loaded automatically by vitest (see vite.config.ts).
//
// Coding Instructions:
//   Fetch is stubbed rather than intercepted at the network layer, because
//   what these tests are for is proving the components paint. A test that
//   needs a real server is an integration test and lives in tests/Integration.

import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, beforeEach, expect, vi } from 'vitest';

declare global {
  // eslint-disable-next-line no-var
  var __apiCalls: { path: string; init?: RequestInit }[];
}

beforeEach(() => {
  globalThis.__apiCalls = [];
  localStorage.clear();

  // jsdom keeps cookies and history between tests in a file. Both are shared
  // state, and a test that inherits the previous one's administrator cookie or
  // its URL would pass or fail for reasons nothing in it explains.
  for (const cookie of document.cookie.split(';')) {
    const name = cookie.split('=')[0]?.trim();
    if (name) {
      document.cookie = `${name}=; expires=Thu, 01 Jan 1970 00:00:00 GMT`;
    }
  }

  window.history.pushState({}, '', '/');

  // A cookie the server would have set at sign-in. Present by default so a
  // component under test behaves as it does for a signed-in person; the
  // anti-forgery tests clear it deliberately.
  document.cookie = 'dfoss_csrf=test-token';
});

afterEach(() => {
  cleanup();
});

/**
 * What the API returns for one path, in the order the component asks.
 *
 * `delayMs` makes a reply slow. It exists for one kind of test that cannot be
 * written without it: proving that a superseded request loses the race. With
 * every reply instant, responses always arrive in request order and the bug
 * that instant search guards against is unreachable.
 */
export type Reply =
  | { ok: true; body: unknown; delayMs?: number }
  | { ok: true; status: 204; delayMs?: number }
  | { ok: false; status: number; code: string; detail: string; delayMs?: number };

/**
 * Answers the component's fetch calls from a table keyed by path prefix. Paths
 * are matched by `startsWith` so a component may add query parameters without
 * the test having to predict them.
 */
export function mockApi(replies: Record<string, Reply | Reply[]>): void {
  const remaining = new Map<string, Reply[]>(
    Object.entries(replies).map(([path, reply]) => [
      path,
      Array.isArray(reply) ? [...reply] : [reply],
    ]),
  );

  vi.stubGlobal(
    'fetch',
    vi.fn((url: string, init?: RequestInit) => {
      const path = url.replace('/api/v1', '');
      globalThis.__apiCalls.push({ path, init });

      const key = [...remaining.keys()]
        .filter((k) => path.startsWith(k))
        // Longest match wins, so '/auth/me' and '/auth' can both be listed.
        .sort((a, b) => b.length - a.length)[0];

      if (key === undefined) {
        throw new Error(
          `The component called ${path}, which the test did not arrange. ` +
            `Arranged: ${[...remaining.keys()].join(', ') || 'nothing'}.`,
        );
      }

      const queue = remaining.get(key)!;
      const reply = queue.length > 1 ? queue.shift()! : queue[0]!;

      // Honour cancellation, because a stub that ignores it cannot show the
      // difference between code that cancels and code that does not — which is
      // exactly the property instant search depends on. Rejects with the same
      // DOMException the real fetch does, so callers detect it identically.
      const abort = () =>
        Promise.reject(new DOMException('The operation was aborted.', 'AbortError'));

      if (init?.signal?.aborted === true) {
        return abort();
      }

      if (reply.delayMs !== undefined) {
        return new Promise<Response>((resolve, reject) => {
          const timer = setTimeout(() => void respond(reply).then(resolve), reply.delayMs);

          init?.signal?.addEventListener('abort', () => {
            clearTimeout(timer);
            reject(new DOMException('The operation was aborted.', 'AbortError'));
          });
        });
      }

      return respond(reply);
    }),
  );
}

/** Turns one arranged reply into the Response shape the api client reads. */
function respond(reply: Reply): Promise<Response> {
  if (reply.ok) {
    const status = 'status' in reply ? reply.status : 200;
    const body = 'body' in reply ? reply.body : undefined;

    // blob() and headers exist because a file download reads them rather
    // than json(). A mock that only speaks JSON silently fails any code
    // path that fetches a file.
    return Promise.resolve({
      ok: true,
      status,
      json: () => Promise.resolve(body),
      blob: () => Promise.resolve(new Blob([String(body ?? '')], { type: 'text/csv' })),
      text: () => Promise.resolve(String(body ?? '')),
      headers: new Headers({ 'Content-Disposition': 'attachment; filename="export.csv"' }),
    } as unknown as Response);
  }

  return Promise.resolve({
    ok: false,
    status: reply.status,
    json: () => Promise.resolve({ code: reply.code, detail: reply.detail }),
    headers: new Headers(),
  } as unknown as Response);
}

/** Makes every call fail the way an unreachable server does. */
export function mockApiUnreachable(): void {
  vi.stubGlobal('fetch', vi.fn(() => Promise.reject(new TypeError('Failed to fetch'))));
}

/**
 * Leaves every call hanging, so the loading state is genuinely what is on
 * screen. Resolving immediately would settle the component during the
 * assertion, which React reports as an unwrapped update — and the warning would
 * be right: that test would not be looking at a loading state at all.
 */
export function mockApiPending(): void {
  vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => {})));
}

/** The requests the component actually made, for asserting headers. */
export function apiCalls(): { path: string; init?: RequestInit }[] {
  return globalThis.__apiCalls;
}

/** The header value sent on the last call to a path, or undefined. */
export function headerOn(path: string, header: string): string | undefined {
  const call = [...apiCalls()].reverse().find((c) => c.path.startsWith(path));
  expect(call, `no call was made to ${path}`).toBeDefined();

  return (call!.init?.headers as Record<string, string> | undefined)?.[header];
}
