// AdminSignIn — the other door.
//
// Use:  the only route reachable at /admin while signed out of the control plane.
// Edit: one form, not two steps. A second factor is mandatory here, so there is
//       no branch where a password alone means anything and therefore nothing
//       for a challenge to protect. The code field is optional only because a
//       newly created administrator has not enrolled one yet — and they get a
//       session that can reach enrolment and nothing else.

import { useState, type FormEvent } from 'react';
import { ApiError, adminPost } from '../../shared/adminApi';
import { useAdminSession } from '../../app/adminSession';

export function AdminSignIn() {
  const { refresh } = useAdminSession();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setBusy(true);

    try {
      await adminPost('/login', {
        email,
        password,
        // Empty means "I have not enrolled one". Sending '' would read as a
        // wrong code rather than an absent one.
        code: code.trim() === '' ? null : code.trim(),
      });

      await refresh();
    } catch (failure) {
      setError(failure instanceof ApiError ? failure.message : 'Something went wrong. Try again.');
      setCode('');
    } finally {
      setBusy(false);
    }
  }

  return (
    // Marked as firmly as the console itself. Without this the two sign-in
    // screens are near-identical, which is worst at exactly the moment somebody
    // is typing a password: they cannot tell which door they are at.
    <main className="signin signin--admin">
      <h1>
        DealerFOSS <span className="shell__badge">Administration</span>
      </h1>

      <form onSubmit={(e) => void submit(e)} noValidate>
        <p className="signin__lede">
          This signs you in to the installation, not to a dealership.
        </p>

        <label htmlFor="admin-email">Email</label>
        <input
          id="admin-email"
          name="email"
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          autoComplete="username"
          required
        />

        <label htmlFor="admin-password">Password</label>
        <input
          id="admin-password"
          name="password"
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          autoComplete="current-password"
          required
        />

        <label htmlFor="admin-code">Code from your authenticator app</label>
        <input
          id="admin-code"
          name="code"
          value={code}
          onChange={(e) => setCode(e.target.value)}
          inputMode="numeric"
          autoComplete="one-time-code"
        />
        <p className="note">Leave blank only if you have not set one up yet.</p>

        <p className="error" aria-live="polite">
          {error ?? ''}
        </p>

        <button type="submit" disabled={busy}>
          {busy ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </main>
  );
}
