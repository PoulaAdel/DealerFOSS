// api — the one place the browser talks to the backend.
//
// Use:  await api<InventoryUnit[]>('/inventory?openOnly=true')
// Edit: every call carries the tenant header and the session cookie. The cookie
//       is HttpOnly, so this code cannot read it and does not try — "am I signed
//       in?" is answered by asking the server, not by inspecting storage.
//
//       Errors arrive as RFC 7807 Problem Details with a stable `code`. Surface
//       the server's message rather than inventing one: it was written to be
//       read by the person who hit it.

const TENANT_KEY = 'odms.tenant';

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
    return this.status === 401;
  }
}

type Problem = { code?: string; title?: string; detail?: string };

export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;

  try {
    response = await fetch(`/api/v1${path}`, {
      ...init,
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
        'X-Tenant': currentTenant(),
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
