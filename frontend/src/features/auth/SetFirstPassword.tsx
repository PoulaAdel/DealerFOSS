// SetFirstPassword — a starter turns a one-time code into an account.
//
// Use:  reachable at /set-password, without signing in. It has to be: not having
//       a password is the state this screen exists to fix.
// Edit: the refusal is deliberately vague — "that code is not usable" covers an
//       unknown email, a wrong code, and an expired one, because the server
//       answers identically for all three. Do not "improve" the message by
//       guessing which one happened; the whole point is that somebody holding a
//       code cannot learn whose account it opens.

import { useState } from 'react';
import { useNavigate } from 'react-router';
import { ApiError, currentTenant, post, setCurrentTenant } from '../../shared/api';

export function SetFirstPassword() {
  const navigate = useNavigate();
  const [tenant, setTenant] = useState(currentTenant());
  const [email, setEmail] = useState('');
  const [code, setCode] = useState('');
  const [password, setPassword] = useState('');
  const [again, setAgain] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState(false);

  // Checked here as well as on the server, because this is the one field the
  // server cannot check for them: it never sees the second box.
  const mismatch = again !== '' && password !== again;

  async function submit() {
    setBusy(true);
    setError(null);

    try {
      setCurrentTenant(tenant.trim());
      await post('/auth/enrol', { email: email.trim(), code: code.trim(), password });
      setDone(true);
    } catch (failure) {
      setError(failure instanceof ApiError ? failure.message : 'That did not work.');
    } finally {
      setBusy(false);
    }
  }

  if (done) {
    return (
      <main className="state">
        <section className="panel">
          <h1>You are set up</h1>
          <p>Sign in with your email address and the password you just chose.</p>
          <button type="button" className="primary" onClick={() => void navigate('/sign-in')}>
            Go to sign in
          </button>
        </section>
      </main>
    );
  }

  return (
    <main className="state">
      <section className="panel">
        <h1>Set your password</h1>
        <p className="note">
          Your manager gave you a code. Use it once here to choose a password only
          you know — nobody at the dealership can see what you pick.
        </p>

        <div className="field">
          <label htmlFor="enrol-tenant">Dealership</label>
          <input
            id="enrol-tenant"
            value={tenant}
            onChange={(event) => setTenant(event.target.value)}
          />
        </div>

        <div className="field">
          <label htmlFor="enrol-email">Email</label>
          <input
            id="enrol-email"
            type="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
          />
        </div>

        <div className="field">
          <label htmlFor="enrol-code">Code</label>
          <input
            id="enrol-code"
            value={code}
            onChange={(event) => setCode(event.target.value)}
            placeholder="ABCD-EFGH-JKLM"
          />
        </div>

        <div className="field">
          <label htmlFor="enrol-password">New password</label>
          <input
            id="enrol-password"
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
          <p className="hint">At least 12 characters. Length is what makes one hard to guess.</p>
        </div>

        <div className="field">
          <label htmlFor="enrol-again">New password again</label>
          <input
            id="enrol-again"
            type="password"
            value={again}
            onChange={(event) => setAgain(event.target.value)}
          />
        </div>

        <p className="error" aria-live="polite">
          {mismatch ? 'Those two do not match.' : (error ?? '')}
        </p>

        <button
          type="button"
          className="primary"
          disabled={
            busy ||
            mismatch ||
            tenant.trim() === '' ||
            email.trim() === '' ||
            code.trim() === '' ||
            password === ''
          }
          onClick={() => void submit()}
        >
          Set my password
        </button>
      </section>
    </main>
  );
}
