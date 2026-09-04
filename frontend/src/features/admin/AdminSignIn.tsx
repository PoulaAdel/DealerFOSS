// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   AdminSignIn — the other door.
//
// Usage:
//   The only route reachable at /admin while signed out of the control plane.
//
// Coding Instructions:
//   One form, not two steps. A second factor is mandatory here, so there is
//   no branch where a password alone means anything and therefore nothing
//   for a challenge to protect. The code field is optional only because a
//   newly created administrator has not enrolled one yet — and they get a
//   session that can reach enrolment and nothing else.

import { useState, type FormEvent } from 'react';
import { adminPost } from '../../shared/adminApi';
import { useAdminSession } from '../../app/adminSession';
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';
import { AppearanceControls } from '../../app/AppearanceControls';

export function AdminSignIn() {
  const { refresh } = useAdminSession();
  const { t } = useI18n();
  const describe = useApiMessage();

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
      setError(describe(failure));
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
      {/* Same reason as the dealership's sign-in: the language control has to be
          reachable BEFORE anybody signs in, or somebody who reads only Arabic
          has to read English to find out how to stop reading English. */}
      <div className="signin__appearance">
        <AppearanceControls />
      </div>

      <h1>
        {t('app.name')} <span className="shell__badge">{t('admin.badge')}</span>
      </h1>

      <form onSubmit={(e) => void submit(e)} noValidate>
        <p className="signin__lede">{t('admin.signInLede')}</p>

        <label htmlFor="admin-email">{t('signIn.email')}</label>
        <input
          id="admin-email"
          name="email"
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          autoComplete="username"
          dir="ltr"
          required
        />

        <label htmlFor="admin-password">{t('signIn.password')}</label>
        <input
          id="admin-password"
          name="password"
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          autoComplete="current-password"
          dir="ltr"
          required
        />

        <label htmlFor="admin-code">{t('admin.signInCode')}</label>
        <input
          id="admin-code"
          name="code"
          value={code}
          onChange={(e) => setCode(e.target.value)}
          inputMode="numeric"
          autoComplete="one-time-code"
          dir="ltr"
        />
        <p className="note">{t('admin.signInCodeNote')}</p>

        <p className="error" aria-live="polite">
          {error ?? ''}
        </p>

        <button type="submit" disabled={busy}>
          {busy ? t('signIn.submitting') : t('signIn.submit')}
        </button>
      </form>
    </main>
  );
}
