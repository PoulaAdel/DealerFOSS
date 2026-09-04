// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   RecoverPassword — the way back in for somebody who cannot sign in.
//
// Usage:
//   Reachable at /recover, linked from the sign-in screen. No session, by
//   definition — not having one is the problem this screen exists to fix.
//
// Coding Instructions:
//   Four things here are deliberate.
//
//   (1) THE METHODS COME FROM THE SERVER AND CARRY NO EMAIL. Asking "what can
//   this account use" would answer whether the address exists and whether it
//   has an authenticator. The list is a property of the installation, so this
//   screen asks once, before anybody types anything.
//
//   (2) THE REFUSAL IS SHOWN, NEVER INTERPRETED. The server answers every
//   failure identically on purpose, and any attempt here to be more helpful —
//   "that email is not registered" — would undo exactly what it is protecting.
//
//   (3) ONE SUBMIT SETS THE PASSWORD. There is no prove-then-redeem, because
//   the server has no intermediate ticket; adding a second step here would be
//   theatre suggesting a security property that does not exist.
//
//   (4) THE TWO PASSWORD BOXES ARE COMPARED IN THE BROWSER. That check is a
//   typo-catcher and nothing else — the server never sees the second box —
//   so it is done here where it costs nothing.

import { useEffect, useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import { api, post } from '../../shared/api';
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';
import { AppearanceControls } from '../../app/AppearanceControls';
import type { RecoveryMethods } from '../../shared/contracts';

type Method = 'authenticator' | 'issuedCode';

type Stage =
  | { kind: 'choosing' }
  | { kind: 'proving'; method: Method }
  | { kind: 'done' };

export function RecoverPassword() {
  const { t } = useI18n();
  const describe = useApiMessage();

  const [offered, setOffered] = useState<RecoveryMethods | null>(null);
  const [stage, setStage] = useState<Stage>({ kind: 'choosing' });

  const [email, setEmail] = useState('');
  const [code, setCode] = useState('');
  const [password, setPassword] = useState('');
  const [again, setAgain] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    // No email in this request, and there never should be. See note (1).
    void api<RecoveryMethods>('/auth/recover')
      .then(setOffered)
      .catch(() => {
        // A method list that will not load is not worth an error screen: both
        // built-in methods are always available, so offering them is the
        // useful answer and the server refuses anything it cannot honour.
        setOffered({ authenticator: true, issuedCode: true, email: false, textMessage: false });
      });
  }, []);

  async function submit(event: FormEvent) {
    event.preventDefault();

    if (stage.kind !== 'proving') {
      return;
    }

    if (password !== again) {
      setError(t('recover.mismatch'));
      return;
    }

    setError(null);
    setBusy(true);

    try {
      const route = stage.method === 'authenticator' ? 'authenticator' : 'code';
      await post(`/auth/recover/${route}`, { email, code, password });

      setStage({ kind: 'done' });
      setCode('');
      setPassword('');
      setAgain('');
    } catch (failure) {
      // Shown as the server phrased it. See note (2).
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  if (stage.kind === 'done') {
    return (
      <main className="signin">
        <div className="signin__appearance">
          <AppearanceControls />
        </div>

        <h1>{t('recover.doneTitle')}</h1>
        <p className="signin__lede">{t('recover.doneLede')}</p>

        <Link className="button" to="/sign-in">
          {t('recover.toSignIn')}
        </Link>
      </main>
    );
  }

  const nothingOffered =
    offered !== null && !offered.authenticator && !offered.issuedCode;

  return (
    <main className="signin">
      {/* Reachable before signing in, for the same reason it is on the sign-in
          screen: somebody who reads only Arabic must not have to read English
          to find out how to stop reading English. */}
      <div className="signin__appearance">
        <AppearanceControls />
      </div>

      <h1>{t('recover.title')}</h1>

      {stage.kind === 'choosing' ? (
        <>
          <p className="signin__lede">{t('recover.lede')}</p>

          {nothingOffered ? (
            <p className="note">{t('recover.noMethods')}</p>
          ) : (
            <div className="actions actions--lead">
              {offered?.authenticator === false ? null : (
                <div className="field">
                  <button
                    type="button"
                    className="primary"
                    onClick={() => setStage({ kind: 'proving', method: 'authenticator' })}
                  >
                    {t('recover.withAuthenticator')}
                  </button>
                  <p className="hint">{t('recover.withAuthenticatorHint')}</p>
                </div>
              )}

              {offered?.issuedCode === false ? null : (
                <div className="field">
                  <button
                    type="button"
                    onClick={() => setStage({ kind: 'proving', method: 'issuedCode' })}
                  >
                    {t('recover.withCode')}
                  </button>
                  <p className="hint">{t('recover.withCodeHint')}</p>
                </div>
              )}
            </div>
          )}
        </>
      ) : (
        <form onSubmit={(e) => void submit(e)} noValidate>
          <label htmlFor="recover-email">{t('recover.email')}</label>
          {/* An email address reads left to right inside a mirrored page. */}
          <input
            id="recover-email"
            name="email"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            autoComplete="username"
            dir="ltr"
            required
          />

          <label htmlFor="recover-code">
            {stage.method === 'authenticator'
              ? t('recover.codeFromApp')
              : t('recover.codeFromManager')}
          </label>
          <input
            id="recover-code"
            name="code"
            value={code}
            onChange={(e) => setCode(e.target.value)}
            inputMode={stage.method === 'authenticator' ? 'numeric' : 'text'}
            autoComplete="one-time-code"
            dir="ltr"
            autoFocus
            required
          />

          <label htmlFor="recover-password">{t('recover.newPassword')}</label>
          <input
            id="recover-password"
            name="password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="new-password"
            dir="ltr"
            required
          />
          <p className="hint">{t('setPassword.passwordHint')}</p>

          <label htmlFor="recover-again">{t('recover.newPasswordAgain')}</label>
          <input
            id="recover-again"
            name="again"
            type="password"
            value={again}
            onChange={(e) => setAgain(e.target.value)}
            autoComplete="new-password"
            dir="ltr"
            required
          />

          <p className="error" aria-live="polite">
            {error ?? ''}
          </p>

          <button type="submit" disabled={busy}>
            {busy ? t('recover.working') : t('recover.submit')}
          </button>

          <button
            type="button"
            onClick={() => {
              setStage({ kind: 'choosing' });
              setError(null);
            }}
          >
            {t('recover.back')}
          </button>
        </form>
      )}
    </main>
  );
}
