// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   AdminSecondFactorSetup — the same three steps as the dealership's, minus the
//   recovery codes.
//
// Usage:
//   The only route an administrator who has not enrolled can reach.
//
// Coding Instructions:
//   It is a separate component from the dealership's rather than a shared
//   one taking two endpoints as props. The two differ in what they return
//   (no recovery codes here) and in what they mean if they go wrong, and a
//   shared component would have to be told which world it is in — the same
//   mistake `adminApi.ts` exists to avoid.
//
//   An administrator has no recovery codes. That is a real gap, not an
//   omission from this screen: nothing on the server issues them, so losing
//   the phone means a database operation. It is named in STATUS.md.

import { useState, type FormEvent } from 'react';
import { QRCodeSVG } from 'qrcode.react';
import { adminPost } from '../../shared/adminApi';
import type { MfaEnrolment } from '../../shared/contracts';
import { useAdminSession } from '../../app/adminSession';
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';

type Step = { kind: 'intro' } | { kind: 'scan'; enrolment: MfaEnrolment };

export function AdminSecondFactorSetup() {
  const { refresh } = useAdminSession();
  const { t } = useI18n();
  const describe = useApiMessage();

  const [step, setStep] = useState<Step>({ kind: 'intro' });
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function begin() {
    setError(null);
    setBusy(true);

    try {
      setStep({ kind: 'scan', enrolment: await adminPost<MfaEnrolment>('/mfa/enrol', {}) });
    } catch (failure) {
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  async function confirm(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setBusy(true);

    try {
      await adminPost('/mfa/confirm', { code });
      // Lifts the restriction server-side; asking again is what opens the rest
      // of the console without a second sign-in.
      await refresh();
    } catch (failure) {
      setError(describe(failure));
      setCode('');
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <header className="page__head">
        <h1>{t('admin.secondFactorTitle')}</h1>
      </header>

      <p className="state" role="alert">
        {t('admin.secondFactorRequired')}
      </p>

      {step.kind === 'intro' ? (
        <section className="panel">
          <p>{t('admin.secondFactorIntro')}</p>

          <p className="error" aria-live="polite">
            {error ?? ''}
          </p>

          <button type="button" onClick={() => void begin()} disabled={busy}>
            {busy ? t('secondFactor.starting') : t('secondFactor.start')}
          </button>
        </section>
      ) : (
        <section className="panel">
          <ol className="steps">
            <li>
              <p>{t('secondFactor.pointApp')}</p>

              <QRCodeSVG
                value={step.enrolment.enrolmentUri}
                size={192}
                title={t('secondFactor.qrTitle')}
                includeMargin
              />

              <details>
                <summary>{t('secondFactor.cannotScan')}</summary>
                <p>{t('secondFactor.typeInstead')}</p>
                {/* Base32, typed into an app character by character. Inside an
                    Arabic page the bidirectional algorithm would reorder its
                    groups, and somebody copies out a secret that does not
                    work — then cannot sign in to the account that runs the
                    whole installation. */}
                <p className="mono secret" dir="ltr">
                  {step.enrolment.secret}
                </p>
              </details>
            </li>

            <li>
              <form onSubmit={(e) => void confirm(e)} noValidate>
                <label htmlFor="admin-setup-code">{t('secondFactor.enterCode')}</label>
                <input
                  id="admin-setup-code"
                  name="code"
                  value={code}
                  onChange={(e) => setCode(e.target.value)}
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  dir="ltr"
                  autoFocus
                  required
                />

                <p className="error" aria-live="polite">
                  {error ?? ''}
                </p>

                <button type="submit" disabled={busy}>
                  {busy ? t('secondFactor.checking') : t('secondFactor.turnOn')}
                </button>
              </form>
            </li>
          </ol>

          <p className="note">{t('admin.noRecoveryCodes')}</p>
        </section>
      )}
    </>
  );
}
