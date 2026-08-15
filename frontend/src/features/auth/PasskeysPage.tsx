// PasskeysPage — the passkeys on your own account, and how to add one.
//
// Use:  reachable at /security/passkeys.
// Edit: this screen only ever shows YOUR credentials. The API has no way to ask
//       about anybody else's, which is why there is no filter and no search here
//       — the absence is the contract, not an unfinished feature.
//
//       Two things are said out loud rather than assumed.
//
//       (1) The password still works. A screen that lists passkeys without
//       saying so reads like a replacement, and somebody will register one on a
//       phone they are about to trade in. Nothing here takes the password away,
//       and nothing here can.
//
//       (2) Forgetting is deletion, not disablement (see IPasskeys). The
//       confirmation says the device stops working, because that is the part
//       people are actually deciding about — and it is the one act on this
//       screen the server will happily perform and cannot undo, which is exactly
//       what ADR-020 allows a confirm step for.

import { useCallback, useEffect, useState } from 'react';
import { ApiError, api, post, remove } from '../../shared/api';
import type {
  PasskeyRegistrationChallenge,
  RegisteredPasskey,
} from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';
import { createPasskey, passkeysAvailable } from './webauthn';

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; passkeys: RegisteredPasskey[] }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

export function PasskeysPage() {
  const { t, format } = useI18n();
  const describe = useApiMessage();

  const [load, setLoad] = useState<Load>({ kind: 'loading' });
  const [label, setLabel] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [announcement, setAnnouncement] = useState<string | null>(null);

  const [canRegister] = useState(passkeysAvailable);

  const find = useCallback(async () => {
    setLoad({ kind: 'loading' });

    try {
      setLoad({ kind: 'ready', passkeys: await api<RegisteredPasskey[]>('/auth/passkeys') });
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 403) {
        setLoad({ kind: 'denied' });
        return;
      }

      setLoad({ kind: 'failed', message: describe(failure) });
    }
  }, [describe]);

  useEffect(() => {
    void find();
  }, [find]);

  async function add() {
    setError(null);
    setAnnouncement(null);
    setBusy(true);

    try {
      const challenge = await post<PasskeyRegistrationChallenge>(
        '/auth/passkeys/register/begin',
        {},
      );

      const ceremony = await createPasskey(challenge, label.trim());

      if (ceremony.kind === 'cancelled' || ceremony.kind === 'unsupported') {
        // Somebody changed their mind at their own operating system's prompt.
        // Nothing was created and nothing needs saying.
        return;
      }

      if (ceremony.kind === 'failed') {
        setError(t('passkey.ceremonyFailed'));
        return;
      }

      const registered = await post<RegisteredPasskey>(
        '/auth/passkeys/register/finish',
        ceremony.response,
      );

      setLabel('');
      setAnnouncement(t('passkey.added', { label: registered.label }));
      await find();
    } catch (failure) {
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  async function forget(passkey: RegisteredPasskey) {
    if (!window.confirm(t('passkey.forgetConfirm', { label: passkey.label }))) {
      return;
    }

    setError(null);
    setAnnouncement(null);
    setBusy(true);

    try {
      await remove<void>(`/auth/passkeys/${passkey.id}`);
      setAnnouncement(t('passkey.forgot', { label: passkey.label }));
      await find();
    } catch (failure) {
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="page">
      <header className="page__head">
        <h1>{t('passkey.title')}</h1>
      </header>

      <p className="note">{t('passkey.lede')}</p>

      {canRegister ? (
        <section className="panel">
          <h2>{t('passkey.addTitle')}</h2>
          <p className="note">{t('passkey.addNote')}</p>

          <div className="row">
            <div className="field field--grow">
              <label htmlFor="passkey-label">{t('passkey.label')}</label>
              <input
                id="passkey-label"
                value={label}
                placeholder={t('passkey.labelPlaceholder')}
                onChange={(event) => setLabel(event.target.value)}
              />
              <p className="hint">{t('passkey.labelHint')}</p>
            </div>
          </div>

          <div className="actions">
            <button
              type="button"
              className="primary"
              disabled={busy || label.trim() === ''}
              onClick={() => void add()}
            >
              {busy ? t('passkey.adding') : t('passkey.add')}
            </button>
          </div>
        </section>
      ) : (
        <p className="note note--warn">{t('passkey.unsupported')}</p>
      )}

      {/* Always in the DOM so the region exists before anything is put in it. */}
      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>
      <p className="notice" aria-live="polite">
        {announcement ?? ''}
      </p>

      <h2>{t('passkey.yoursTitle')}</h2>
      <Body load={load} onRetry={() => void find()} busy={busy} onForget={forget} format={format} />
    </section>
  );
}

function Body({
  load,
  onRetry,
  busy,
  onForget,
  format,
}: {
  load: Load;
  onRetry: () => void;
  busy: boolean;
  onForget: (passkey: RegisteredPasskey) => Promise<void>;
  format: ReturnType<typeof useI18n>['format'];
}) {
  const { t } = useI18n();

  switch (load.kind) {
    case 'loading':
      return (
        <p className="state" aria-live="polite">
          {t('passkey.loading')}
        </p>
      );

    case 'denied':
      return (
        <p className="state" role="alert">
          {t('common.notPermitted')}
        </p>
      );

    case 'failed':
      return (
        <div className="state" role="alert">
          <p>{load.message}</p>
          <button type="button" onClick={onRetry}>
            {t('common.retry')}
          </button>
        </div>
      );

    case 'ready':
      return load.passkeys.length === 0 ? (
        <p className="state">{t('passkey.none')}</p>
      ) : (
        <div className="scroll">
          <table className="table">
            <caption className="visually-hidden">
              {t('passkey.caption', { count: load.passkeys.length })}
            </caption>
            <thead>
              <tr>
                <th scope="col">{t('passkey.colLabel')}</th>
                <th scope="col">{t('passkey.colAdded')}</th>
                <th scope="col">{t('passkey.colLastUsed')}</th>
                <th scope="col">&nbsp;</th>
              </tr>
            </thead>
            <tbody>
              {load.passkeys.map((passkey) => (
                <tr key={passkey.id}>
                  <td>{passkey.label}</td>
                  <td>{format.date(passkey.createdAt)}</td>
                  <td>
                    {/* "Never used" is a different fact from "used at some
                        unknown time", and it is the one that tells somebody
                        which of two phones this entry is. */}
                    {passkey.lastUsedAt === null ? (
                      <span className="muted">{t('passkey.neverUsed')}</span>
                    ) : (
                      format.dateTime(passkey.lastUsedAt)
                    )}
                  </td>
                  <td>
                    <button type="button" disabled={busy} onClick={() => void onForget(passkey)}>
                      {t('passkey.forget')}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      );
  }
}
