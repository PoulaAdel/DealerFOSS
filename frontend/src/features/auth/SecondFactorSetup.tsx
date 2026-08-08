// SecondFactorSetup — turning on the six-digit code, in three steps.
//
// Use:  reachable at /security/second-factor. It is also the *only* screen a
//       person can reach when their role obliges them to have a second factor
//       and they do not yet.
// Edit: the order of the steps is the safety property, not a design preference.
//       The secret is generated first and confirmed second, so somebody who
//       mis-scans is not locked out of their own account — nothing about signing
//       in changes until a working code proves the phone and the server agree.
//
//       The recovery codes are shown once and never fetched again, because the
//       server keeps only their hashes. If this screen ever gains a "show them
//       again" button, that button is a lie.

import { useState } from 'react';
import { QRCodeSVG } from 'qrcode.react';
import { post } from '../../shared/api';
import type { MfaEnrolment } from '../../shared/contracts';
import { useSession } from '../../app/session';
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';

type Step =
  | { kind: 'intro' }
  | { kind: 'scan'; enrolment: MfaEnrolment }
  | { kind: 'done'; recoveryCodes: string[] };

export function SecondFactorSetup() {
  const { user, refresh } = useSession();
  const { t } = useI18n();
  const describe = useApiMessage();

  const [step, setStep] = useState<Step>({ kind: 'intro' });
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const required = user?.mustEnrolSecondFactor === true;

  async function begin() {
    setError(null);
    setBusy(true);

    try {
      setStep({
        kind: 'scan',
        enrolment: await post<MfaEnrolment>('/auth/mfa/enrol', {}),
      });
    } catch (failure) {
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  async function confirm(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    setBusy(true);

    try {
      const { recoveryCodes } = await post<{ recoveryCodes: string[] }>(
        '/auth/mfa/confirm',
        { code },
      );

      setStep({ kind: 'done', recoveryCodes });

      // The obligation is lifted server-side the moment this succeeds. Asking
      // again is what unlocks the rest of the application without a fresh
      // sign-in — the whole point of the restricted session.
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
        <h1>{t('secondFactor.title')}</h1>
      </header>

      {required && step.kind !== 'done' ? (
        <p className="state" role="alert">
          {t('secondFactor.required')}
        </p>
      ) : null}

      {step.kind === 'intro' ? (
        <section className="panel">
          <p>{t('secondFactor.intro')}</p>

          <Error message={error} />

          <button type="button" onClick={() => void begin()} disabled={busy}>
            {busy ? t('secondFactor.starting') : t('secondFactor.start')}
          </button>
        </section>
      ) : null}

      {step.kind === 'scan' ? (
        <section className="panel">
          <ol className="steps">
            <li>
              <p>{t('secondFactor.pointApp')}</p>

              <QRCodeSVG
                value={step.enrolment.enrolmentUri}
                size={192}
                // Announced rather than decorative: somebody who cannot see it
                // needs to know the alternative below exists.
                title={t('secondFactor.qrTitle')}
                includeMargin
              />

              <details>
                <summary>{t('secondFactor.cannotScan')}</summary>
                <p>{t('secondFactor.typeInstead')}</p>
                {/* The secret is base32 and is typed into an app character by
                    character, so it reads left to right even on an Arabic
                    page. Without dir the bidi algorithm reorders the groups
                    and somebody copies out a secret that does not work. */}
                <p className="mono secret" dir="ltr">
                  {step.enrolment.secret}
                </p>
              </details>
            </li>

            <li>
              <form onSubmit={(e) => void confirm(e)} noValidate>
                <label htmlFor="code">{t('secondFactor.enterCode')}</label>
                <input
                  id="code"
                  name="code"
                  value={code}
                  onChange={(e) => setCode(e.target.value)}
                  // Not type="number": codes have leading zeros.
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  autoFocus
                  required
                />

                <Error message={error} />

                <button type="submit" disabled={busy}>
                  {busy ? t('secondFactor.checking') : t('secondFactor.turnOn')}
                </button>
              </form>
            </li>
          </ol>

          <p className="note">{t('secondFactor.notYet')}</p>
        </section>
      ) : null}

      {step.kind === 'done' ? (
        <section className="panel">
          <p role="status">{t('secondFactor.onNow')}</p>

          <h2>{t('secondFactor.saveTitle')}</h2>
          <p>{t('secondFactor.saveLede')}</p>

          {/* Latin characters and digits, transcribed by hand. Same reason as
              the enrolment secret above. */}
          <ul className="codes mono" aria-label={t('secondFactor.recoveryCodes')} dir="ltr">
            {step.recoveryCodes.map((recoveryCode) => (
              <li key={recoveryCode}>{recoveryCode}</li>
            ))}
          </ul>
        </section>
      ) : null}
    </>
  );
}

/**
 * Always in the DOM, so the live region exists before anything is put into it —
 * a region inserted along with its message is announced unreliably.
 *
 * `aria-live` without `role="alert"` on purpose. The policy banner above is this
 * screen's alert; two of them would leave a screen reader user, and any test,
 * unable to tell which is which.
 */
function Error({ message }: { message: string | null }) {
  return (
    <p className="error" aria-live="polite">
      {message ?? ''}
    </p>
  );
}
