// Test setup — the shared arrangements every component test relies on.
//
// Use:  loaded automatically by vitest (see vite.config.ts).
// Edit: fetch is stubbed rather than intercepted at the network layer, because
//       what these tests are for is proving the components paint. A test that
//       needs a real server is an integration test and lives in tests/Integration.

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

  // A cookie the server would have set at sign-in. Present by default so a
  // component under test behaves as it does for a signed-in person; the
  // anti-forgery tests clear it deliberately.
  document.cookie = 'odms_csrf=test-token';
});

afterEach(() => {
  cleanup();
});

/** What the API returns for one path, in the order the component asks. */
export type Reply =
  | { ok: true; body: unknown }
  | { ok: true; status: 204 }
  | { ok: false; status: number; code: string; detail: string };

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

      if (reply.ok) {
        const status = 'status' in reply ? reply.status : 200;
        return Promise.resolve({
          ok: true,
          status,
          json: () => Promise.resolve('body' in reply ? reply.body : undefined),
        } as Response);
      }

      return Promise.resolve({
        ok: false,
        status: reply.status,
        json: () => Promise.resolve({ code: reply.code, detail: reply.detail }),
      } as Response);
    }),
  );
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
