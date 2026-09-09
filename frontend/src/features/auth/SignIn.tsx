// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   SignIn — email, password, and the six-digit code when the account has one.
//
// Usage:
//   The only route reachable while signed out.
//
// Coding Instructions:
//   The second step is a separate render rather than a separate page, so a
//   refresh does not strand somebody holding a challenge they cannot use.
//   Errors are announced to assistive technology, and focus moves to the
//   code field when it appears — a screen reader user must be told the form
//   changed under them.
//
//   THE PASSKEY BUTTON SITS BESIDE THE PASSWORD FIELD, NOT ABOVE THE FORM
//   (decided 2026-08-15, doc 11 §7). The password is still how almost
//   everybody gets in, and putting a newer control in front of it would make
//   the common case read as the exception. Beside it, it is offered to
//   anybody who has one and ignored by everybody who has not.
//
//   It needs the dealer group and nothing else — no email. That is not an
//   oversight: the credential identifies the account, so asking for one
//   first would make this screen able to answer "does this person work
//   here", which the sign-in ceremony is deliberately built not to do.

import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import { currentTenant, post, setCurrentTenant } from '../../shared/api';
import type { PasskeySignInChallenge, SignInResponse } from '../../shared/contracts';
import { useSession } from '../../app/session';
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';
import { AppearanceControls } from '../../app/AppearanceControls';
import { Mark, Wordmark } from '../../app/Mark';
import { passkeysAvailable, usePasskey } from './webauthn';

type Stage = { kind: 'credentials' } | { kind: 'code'; challengeToken: string };

export function SignIn() {
  const { refresh } = useSession();
  const { t } = useI18n();
  const describe = useApiMessage();

  // The last dealer group this browser used, so a returning user does not
  // retype it. Was hard-coded to `northgroup` — a name that exists only in the
  // development seed, presented to every real installation as if it were
  // theirs, and quietly training people to sign in to a group that is not.
  const [tenant, setTenant] = useState(currentTenant);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [code, setCode] = useState('');

  const [stage, setStage] = useState<Stage>({ kind: 'credentials' });
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  // Read once. Whether this browser can do WebAuthn cannot change while the
  // screen is open, and re-asking on every render would only add noise.
  const [canUsePasskey] = useState(passkeysAvailable);

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
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  /**
   * The whole ceremony, in one button. No email is collected and none is sent:
   * the authenticator picks the credential, and the credential names the
   * account.
   */
  async function signInWithPasskey() {
    setError(null);

    // The dealer group decides which database this asks, so it is needed before
    // anything else. Said plainly rather than left to a 400 from the server.
    if (tenant.trim() === '') {
      setError(t('passkey.needDealerGroup'));
      return;
    }

    setCurrentTenant(tenant);
    setBusy(true);

    try {
      const challenge = await post<PasskeySignInChallenge>('/auth/passkeys/sign-in/begin', {});
      const ceremony = await usePasskey(challenge);

      if (ceremony.kind === 'cancelled' || ceremony.kind === 'unsupported') {
        // Nothing happened. Putting an error on screen for somebody who simply
        // dismissed their operating system's prompt would be shouting at them
        // for changing their mind.
        return;
      }

      if (ceremony.kind === 'failed') {
        setError(t('passkey.ceremonyFailed'));
        return;
      }

      await post<{ expiresAt: string }>('/auth/passkeys/sign-in/finish', ceremony.response);
      await refresh();
    } catch (failure) {
      setError(describe(failure));
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
      setError(describe(failure));
      setCode('');
      codeInput.current?.focus();
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="signin">
      {/* The language picker belongs HERE, not only in the signed-in shell.
          Somebody who reads only Arabic meets this screen first, and putting
          the only way to change language behind a successful sign-in asks them
          to read English to find out how to stop reading English. The theme
          rides along for the same reason it does everywhere else. */}
      <div className="signin__appearance">
        <AppearanceControls />
      </div>

      <div className="signin__brand">
        <Mark size={44} />
        <h1><Wordmark /></h1>
      </div>

      {stage.kind === 'credentials' ? (
        <form onSubmit={submitCredentials} noValidate>
          <p className="signin__lede">{t('signIn.lede')}</p>

          <label htmlFor="tenant">{t('signIn.dealerGroup')}</label>
          <input
            id="tenant"
            name="tenant"
            value={tenant}
            onChange={(e) => setTenant(e.target.value)}
            autoComplete="organization"
            required
          />

          <label htmlFor="email">{t('signIn.email')}</label>
          <input
            id="email"
            name="email"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            autoComplete="username"
            required
          />

          <label htmlFor="password">{t('signIn.password')}</label>
          {/* Beside, not before. See the file header. The button is omitted
              entirely — not disabled — on a browser that cannot do WebAuthn:
              a greyed-out control invites somebody to work out why. */}
          <div className="signin__beside">
            <input
              id="password"
              name="password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
              required
            />

            {canUsePasskey ? (
              <button
                type="button"
                className="signin__passkey"
                disabled={busy}
                onClick={() => void signInWithPasskey()}
              >
                {t('passkey.useOne')}
              </button>
            ) : null}
          </div>

          <Error message={error} />

          <button type="submit" disabled={busy}>
            {busy ? t('signIn.submitting') : t('signIn.submit')}
          </button>

          {/* On the credentials step only. Somebody who has already answered
              their password correctly and is being asked for a code has not
              forgotten it — offering the reset there invites them down a much
              longer road than the one they need. */}
          <p className="note">
            <Link to="/recover">{t('recover.link')}</Link>
          </p>
        </form>
      ) : (
        <form onSubmit={submitCode} noValidate>
          <p className="signin__lede">{t('signIn.codeLede')}</p>

          <label htmlFor="code">{t('signIn.code')}</label>
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
            {busy ? t('signIn.checking') : t('common.continue')}
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
            {t('signIn.startAgain')}
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
