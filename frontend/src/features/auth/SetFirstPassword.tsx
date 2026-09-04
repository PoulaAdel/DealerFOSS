// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   SetFirstPassword — a starter turns a one-time code into an account.
//
// Usage:
//   Reachable at /set-password, without signing in. It has to be: not having
//   a password is the state this screen exists to fix.
//
// Coding Instructions:
//   The refusal is deliberately vague — "that code is not usable" covers an
//   unknown email, a wrong code, and an expired one, because the server
//   answers identically for all three. Do not "improve" the message by
//   guessing which one happened; the whole point is that somebody holding a
//   code cannot learn whose account it opens.

import { useState } from 'react';
import { useNavigate } from 'react-router';
import { ApiError, currentTenant, post, setCurrentTenant } from '../../shared/api';
import { useI18n } from '../../shared/i18n';

export function SetFirstPassword() {
  const navigate = useNavigate();
  const { t } = useI18n();
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
      // Deliberately NOT routed through useApiMessage. The server's refusal here
      // is uniform on purpose — see the note at the top of this file — and the
      // fallback has to stay just as uninformative.
      setError(failure instanceof ApiError ? failure.message : t('setPassword.failed'));
    } finally {
      setBusy(false);
    }
  }

  if (done) {
    return (
      <main className="state">
        <section className="panel">
          <h1>{t('setPassword.doneTitle')}</h1>
          <p>{t('setPassword.doneLede')}</p>
          <button type="button" className="primary" onClick={() => void navigate('/sign-in')}>
            {t('setPassword.toSignIn')}
          </button>
        </section>
      </main>
    );
  }

  return (
    <main className="state">
      <section className="panel">
        <h1>{t('setPassword.title')}</h1>
        <p className="note">{t('setPassword.lede')}</p>

        <div className="field">
          <label htmlFor="enrol-tenant">{t('setPassword.dealership')}</label>
          <input
            id="enrol-tenant"
            value={tenant}
            onChange={(event) => setTenant(event.target.value)}
          />
        </div>

        <div className="field">
          <label htmlFor="enrol-email">{t('setPassword.email')}</label>
          <input
            id="enrol-email"
            type="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
          />
        </div>

        <div className="field">
          <label htmlFor="enrol-code">{t('setPassword.code')}</label>
          <input
            id="enrol-code"
            value={code}
            onChange={(event) => setCode(event.target.value)}
            placeholder="ABCD-EFGH-JKLM"
          />
        </div>

        <div className="field">
          <label htmlFor="enrol-password">{t('setPassword.password')}</label>
          <input
            id="enrol-password"
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
          <p className="hint">{t('setPassword.passwordHint')}</p>
        </div>

        <div className="field">
          <label htmlFor="enrol-again">{t('setPassword.again')}</label>
          <input
            id="enrol-again"
            type="password"
            value={again}
            onChange={(event) => setAgain(event.target.value)}
          />
        </div>

        <p className="error" aria-live="polite">
          {mismatch ? t('setPassword.mismatch') : (error ?? '')}
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
          {t('setPassword.submit')}
        </button>
      </section>
    </main>
  );
}
