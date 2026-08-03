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
import { ApiError, post } from '../../shared/api';
import type { MfaEnrolment } from '../../shared/contracts';
import { useSession } from '../../app/session';

type Step =
  | { kind: 'intro' }
  | { kind: 'scan'; enrolment: MfaEnrolment }
  | { kind: 'done'; recoveryCodes: string[] };

export function SecondFactorSetup() {
  const { user, refresh } = useSession();

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
      setError(messageFor(failure));
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
      setError(messageFor(failure));
      setCode('');
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <header className="page__head">
        <h1>Two-step sign-in</h1>
      </header>

      {required && step.kind !== 'done' ? (
        <p className="state" role="alert">
          Your dealership requires two-step sign-in for your role. Until you set
          it up, this is the only screen you can use.
        </p>
      ) : null}

      {step.kind === 'intro' ? (
        <section className="panel">
          <p>
            After this, signing in asks for a six-digit code from an app on your
            phone as well as your password. Google Authenticator, Authy and
            1Password all work.
          </p>

          <Error message={error} />

          <button type="button" onClick={() => void begin()} disabled={busy}>
            {busy ? 'Starting…' : 'Start'}
          </button>
        </section>
      ) : null}

      {step.kind === 'scan' ? (
        <section className="panel">
          <ol className="steps">
            <li>
              <p>Point your authenticator app at this square.</p>

              <QRCodeSVG
                value={step.enrolment.enrolmentUri}
                size={192}
                // Announced rather than decorative: somebody who cannot see it
                // needs to know the alternative below exists.
                title="Scan this with your authenticator app"
                includeMargin
              />

              <details>
                <summary>Can’t scan it?</summary>
                <p>Type this into the app by hand instead:</p>
                <p className="mono secret">{step.enrolment.secret}</p>
              </details>
            </li>

            <li>
              <form onSubmit={(e) => void confirm(e)} noValidate>
                <label htmlFor="code">Now enter the code it shows</label>
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
                  {busy ? 'Checking…' : 'Turn it on'}
                </button>
              </form>
            </li>
          </ol>

          <p className="note">
            Nothing has changed about signing in yet. It only takes effect once
            the code above is accepted.
          </p>
        </section>
      ) : null}

      {step.kind === 'done' ? (
        <section className="panel">
          <p role="status">
            Two-step sign-in is on. From now on you will be asked for a code
            after your password.
          </p>

          <h2>Save these somewhere safe</h2>
          <p>
            Each of these works once, and only if you lose your phone. This is
            the only time they will ever be shown.
          </p>

          <ul className="codes mono" aria-label="Recovery codes">
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

function messageFor(failure: unknown): string {
  return failure instanceof ApiError
    ? failure.message
    : 'Something went wrong. Try again.';
}
