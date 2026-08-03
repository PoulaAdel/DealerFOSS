// AdminSecondFactorSetup — the same three steps as the dealership's, minus the
// recovery codes.
//
// Use:  the only route an administrator who has not enrolled can reach.
// Edit: it is a separate component from the dealership's rather than a shared
//       one taking two endpoints as props. The two differ in what they return
//       (no recovery codes here) and in what they mean if they go wrong, and a
//       shared component would have to be told which world it is in — the same
//       mistake `adminApi.ts` exists to avoid.
//
//       An administrator has no recovery codes. That is a real gap, not an
//       omission from this screen: nothing on the server issues them, so losing
//       the phone means a database operation. It is named in STATUS.md.

import { useState, type FormEvent } from 'react';
import { QRCodeSVG } from 'qrcode.react';
import { ApiError, adminPost } from '../../shared/adminApi';
import type { MfaEnrolment } from '../../shared/contracts';
import { useAdminSession } from '../../app/adminSession';

type Step = { kind: 'intro' } | { kind: 'scan'; enrolment: MfaEnrolment };

export function AdminSecondFactorSetup() {
  const { refresh } = useAdminSession();

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
      setError(messageFor(failure));
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
      setError(messageFor(failure));
      setCode('');
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <header className="page__head">
        <h1>Set up your second factor</h1>
      </header>

      <p className="state" role="alert">
        Administrator accounts must have one. Until you set it up, this is the
        only screen you can use.
      </p>

      {step.kind === 'intro' ? (
        <section className="panel">
          <p>
            This account can enter any dealership on this installation, so a
            password on its own is not enough to hold it.
          </p>

          <p className="error" aria-live="polite">
            {error ?? ''}
          </p>

          <button type="button" onClick={() => void begin()} disabled={busy}>
            {busy ? 'Starting…' : 'Start'}
          </button>
        </section>
      ) : (
        <section className="panel">
          <ol className="steps">
            <li>
              <p>Point your authenticator app at this square.</p>

              <QRCodeSVG
                value={step.enrolment.enrolmentUri}
                size={192}
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
                <label htmlFor="admin-setup-code">Now enter the code it shows</label>
                <input
                  id="admin-setup-code"
                  name="code"
                  value={code}
                  onChange={(e) => setCode(e.target.value)}
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  autoFocus
                  required
                />

                <p className="error" aria-live="polite">
                  {error ?? ''}
                </p>

                <button type="submit" disabled={busy}>
                  {busy ? 'Checking…' : 'Turn it on'}
                </button>
              </form>
            </li>
          </ol>

          <p className="note">
            There are no recovery codes for an administrator account. If you lose
            this phone, someone with database access has to clear it for you.
          </p>
        </section>
      )}
    </>
  );
}

function messageFor(failure: unknown): string {
  return failure instanceof ApiError ? failure.message : 'Something went wrong. Try again.';
}
