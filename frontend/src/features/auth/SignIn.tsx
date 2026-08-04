// SignIn — email, password, and the six-digit code when the account has one.
//
// Use:  the only route reachable while signed out.
// Edit: the second step is a separate render rather than a separate page, so a
//       refresh does not strand somebody holding a challenge they cannot use.
//       Errors are announced to assistive technology, and focus moves to the
//       code field when it appears — a screen reader user must be told the form
//       changed under them.

import { useEffect, useRef, useState, type FormEvent } from 'react';
import { ApiError, post, setCurrentTenant } from '../../shared/api';
import type { SignInResponse } from '../../shared/contracts';
import { useSession } from '../../app/session';

type Stage = { kind: 'credentials' } | { kind: 'code'; challengeToken: string };

export function SignIn() {
  const { refresh } = useSession();

  const [tenant, setTenant] = useState('northgroup');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [code, setCode] = useState('');

  const [stage, setStage] = useState<Stage>({ kind: 'credentials' });
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const codeInput = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (stage.kind === 'code') {
      codeInput.current?.focus();
    }
  }, [stage]);

  async function submitCredentials(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setBusy(true);

    // Stored before the call: every request, including this one's follow-up,
    // needs the tenant header.
    setCurrentTenant(tenant);

    try {
      const result = await post<SignInResponse>('/auth/login', { email, password });

      if (result.secondFactorRequired) {
        setStage({ kind: 'code', challengeToken: result.challengeToken });
        return;
      }

      await refresh();
    } catch (failure) {
      setError(messageFor(failure));
    } finally {
      setBusy(false);
    }
  }

  async function submitCode(event: FormEvent) {
    event.preventDefault();
    if (stage.kind !== 'code') {
      return;
    }

    setError(null);
    setBusy(true);

    try {
      await post<{ expiresAt: string }>('/auth/login/second-factor', {
        challengeToken: stage.challengeToken,
        code,
      });

      await refresh();
    } catch (failure) {
      setError(messageFor(failure));
      setCode('');
      codeInput.current?.focus();
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="signin">
      <h1>DealerFOSS</h1>

      {stage.kind === 'credentials' ? (
        <form onSubmit={submitCredentials} noValidate>
          <p className="signin__lede">Sign in to your dealership.</p>

          <label htmlFor="tenant">Dealer group</label>
          <input
            id="tenant"
            name="tenant"
            value={tenant}
            onChange={(e) => setTenant(e.target.value)}
            autoComplete="organization"
            required
          />

          <label htmlFor="email">Email</label>
          <input
            id="email"
            name="email"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            autoComplete="username"
            required
          />

          <label htmlFor="password">Password</label>
          <input
            id="password"
            name="password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
            required
          />

          <Error message={error} />

          <button type="submit" disabled={busy}>
            {busy ? 'Signing in…' : 'Sign in'}
          </button>
        </form>
      ) : (
        <form onSubmit={submitCode} noValidate>
          <p className="signin__lede">
            Enter the six-digit code from your authenticator app, or one of your
            recovery codes.
          </p>

          <label htmlFor="code">Code</label>
          <input
            id="code"
            name="code"
            ref={codeInput}
            value={code}
            onChange={(e) => setCode(e.target.value)}
            // Not type="number": codes have leading zeros, and a recovery code
            // is letters.
            inputMode="text"
            autoComplete="one-time-code"
            required
          />

          <Error message={error} />

          <button type="submit" disabled={busy}>
            {busy ? 'Checking…' : 'Continue'}
          </button>

          <button
            type="button"
            className="link"
            onClick={() => {
              setStage({ kind: 'credentials' });
              setError(null);
              setCode('');
            }}
          >
            Start again
          </button>
        </form>
      )}
    </main>
  );
}

/// Announced rather than merely displayed: a sighted user sees it appear, and
/// everybody else needs to be told.
function Error({ message }: { message: string | null }) {
  return (
    <p className="error" role="alert" aria-live="polite">
      {message ?? ''}
    </p>
  );
}

function messageFor(failure: unknown): string {
  if (failure instanceof ApiError) {
    return failure.message;
  }

  return 'Something went wrong. Try again.';
}
