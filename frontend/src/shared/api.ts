// api — the one place the browser talks to the backend.
//
// Use:  await api<InventoryUnit[]>('/inventory?openOnly=true')
// Edit: every call carries the tenant header and the session cookie. The cookie
//       is HttpOnly, so this code cannot read it and does not try — "am I signed
//       in?" is answered by asking the server, not by inspecting storage.
//
//       Writes additionally carry the anti-forgery token, which the server hands
//       over in a readable cookie at sign-in. Copying it into a header is the
//       whole point: a cross-site form can send our cookies but cannot set a
//       header. Do not move this into the body — it must apply to every write
//       without each caller remembering.
//
//       Errors arrive as RFC 7807 Problem Details with a stable `code`. Surface
//       the server's message rather than inventing one: it was written to be
//       read by the person who hit it.

const TENANT_KEY = 'dfoss.tenant';
const ANTI_FORGERY_COOKIE = 'dfoss_csrf';
const ANTI_FORGERY_HEADER = 'X-CSRF-Token';

/** Methods that change nothing. Everything else carries an anti-forgery token. */
const SAFE_METHODS = ['GET', 'HEAD', 'OPTIONS'];

/** Which dealer organization this browser is working in. */
export function currentTenant(): string {
  return localStorage.getItem(TENANT_KEY) ?? '';
}

export function setCurrentTenant(tenant: string): void {
  localStorage.setItem(TENANT_KEY, tenant.trim());
}

export function clearCurrentTenant(): void {
  localStorage.removeItem(TENANT_KEY);
}

/** A failure the server described, with the code the API contract promises. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
    message: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }

  /** True when signing in again is the remedy. */
  get needsSignIn(): boolean {
    // A failed anti-forgery check means this browser no longer holds a token
    // that matches its session, which only a fresh sign-in can fix. Every other
    // 403 is a permission answer and must be shown, not papered over.
    return this.status === 401 || this.code === 'auth.antiforgery_failed';
  }
}

type Problem = { code?: string; title?: string; detail?: string };

/**
 * This session's anti-forgery token, as the server left it. Empty when signed
 * out — in which case the write is going to be refused anyway, and the server
 * says so more accurately than a guess here would.
 */
function antiForgeryToken(): string {
  const value = document.cookie.match(
    new RegExp(`(?:^|;\\s*)${ANTI_FORGERY_COOKIE}=([^;]*)`),
  )?.[1];

  return value === undefined ? '' : decodeURIComponent(value);
}

export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;
  const method = (init?.method ?? 'GET').toUpperCase();

  try {
    response = await fetch(`/api/v1${path}`, {
      ...init,
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
        'X-Tenant': currentTenant(),
        ...(SAFE_METHODS.includes(method)
          ? {}
          : { [ANTI_FORGERY_HEADER]: antiForgeryToken() }),
        ...init?.headers,
      },
    });
  } catch {
    // A network-level failure, not an answer from the API. Saying so is more
    // useful than "something went wrong".
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

export function post<T>(path: string, body: unknown): Promise<T> {
  return api<T>(path, { method: 'POST', body: JSON.stringify(body) });
}

/**
 * DELETE. Carries the anti-forgery token like any other write — a request that
 * takes something away is exactly the kind another site would like to make on
 * your behalf.
 */
export function remove<T>(path: string): Promise<T> {
  return api<T>(path, { method: 'DELETE' });
}

/**
 * Fetches a file and hands the browser a download.
 *
 * A plain `<a href>` cannot do this: every call needs the tenant header, and an
 * anchor sends none — the request would arrive without a dealership and be
 * refused. So the file is fetched like any other call and turned into a
 * download here.
 */
export async function download(path: string, fallbackName: string): Promise<void> {
  const response = await fetch(`/api/v1${path}`, {
    credentials: 'include',
    headers: { 'X-Tenant': currentTenant() },
  });

  if (!response.ok) {
    let problem: Problem = {};
    try {
      problem = (await response.json()) as Problem;
    } catch {
      // No JSON body; the status is all we have.
    }

    throw new ApiError(
      response.status,
      problem.code ?? 'unknown',
      problem.detail ?? `The server answered ${response.status}.`,
    );
  }

  // The server names the file; only fall back if it did not.
  const disposition = response.headers.get('Content-Disposition') ?? '';
  const named = /filename="?([^"';]+)"?/.exec(disposition)?.[1];

  const url = URL.createObjectURL(await response.blob());
  try {
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = named ?? fallbackName;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
  } finally {
    // Revoking immediately would cancel the download in some browsers; a tick
    // is enough for the click to have been handled.
    setTimeout(() => URL.revokeObjectURL(url), 0);
  }
}
