// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ConnectorsPage — what this build connects to, how far each has been
//   proven, and what would not come in.
//
// Usage:
//   Reachable at /integrations, behind Migration.Import.
//
// Coding Instructions:
//   THIS SCREEN EXISTS TO SAY AN UNCOMFORTABLE THING. `CertificationStatus`
//   has had four levels since the integration edge was built, the one shipped
//   connector has always correctly declared itself `FixtureTested`, and until
//   2026-09-19 no reader could learn any of it. A status nobody can read
//   cannot be read honestly — which is how the exit criterion put it, and it
//   was right.
//
//   So the certification is the headline, not a footnote, and the connector's
//   own declared limitations are shown BESIDE it whatever the level says. The
//   fixture's first limitation is "Serves fabricated records. Never certify
//   anything against this." If that sentence ever stops being visible on this
//   screen, the screen has stopped doing its job.
//
//   THE QUARANTINE LIST CARRIES NO PAYLOAD, and must not gain one. Those
//   fields are a customer's name, address and telephone number exactly as a
//   provider sent them (ADR-022). A review queue that displayed them would be
//   a personal-data export with a queue painted on it. Replay reads the
//   payload on the server and it stays there.
//
//   Replay is the primary action and dismissal is not. Dismissing marks a
//   record dealt with WITHOUT re-running it, so it needs a reason in writing —
//   the server refuses a blank one, and this screen must not offer a way to
//   send one.

import { useCallback, useEffect, useState } from 'react';
import { ApiError, api, post } from '../../shared/api';
import type { ConnectorSummary, QuarantineEntry, ReplayOutcome } from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; connectors: ConnectorSummary[]; held: QuarantineEntry[] }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

export function ConnectorsPage() {
  const { t, format } = useI18n();
  const describe = useApiMessage();

  const [load, setLoad] = useState<Load>({ kind: 'loading' });
  const [busy, setBusy] = useState<string | null>(null);
  const [said, setSaid] = useState<string | null>(null);

  const read = useCallback(async () => {
    setLoad({ kind: 'loading' });

    try {
      const [connectors, held] = await Promise.all([
        api<ConnectorSummary[]>('/integrations/connectors'),
        api<QuarantineEntry[]>('/integrations/quarantine'),
      ]);

      setLoad({ kind: 'ready', connectors, held });
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 403) {
        setLoad({ kind: 'denied' });
        return;
      }

      setLoad({ kind: 'failed', message: describe(failure) });
    }
  }, [describe]);

  useEffect(() => {
    void read();
  }, [read]);

  async function replay(entry: QuarantineEntry) {
    setBusy(entry.id);
    setSaid(null);

    try {
      const outcome = await post<ReplayOutcome>(
        `/integrations/quarantine/${entry.id}/replay`, {});

      // Refused again is an ANSWER, not an error: the request worked and this
      // is what it found out. Saying it plainly is the whole value of replay
      // over marking the row resolved and hoping.
      setSaid(outcome.applied
        ? t('connectors.replayApplied', { id: entry.externalId })
        : t('connectors.replayRefused', {
            id: entry.externalId,
            reason: outcome.reasonDetail ?? outcome.reasonCode ?? '',
          }));

      await read();
    } catch (failure) {
      setSaid(describe(failure));
    } finally {
      setBusy(null);
    }
  }

  async function dismiss(entry: QuarantineEntry) {
    // A reason in writing, because the server refuses a blank one and because
    // a queue cleared without one is indistinguishable from a queue nobody
    // read. Prompt rather than a form: this is the rare action, and a panel
    // for it would give it the weight replay should have.
    const note = window.prompt(t('connectors.dismissAsk', { id: entry.externalId }));
    if (note === null || note.trim() === '') {
      return;
    }

    setBusy(entry.id);
    setSaid(null);

    try {
      await post(`/integrations/quarantine/${entry.id}/dismiss`, { note });
      setSaid(t('connectors.dismissed', { id: entry.externalId }));
      await read();
    } catch (failure) {
      setSaid(describe(failure));
    } finally {
      setBusy(null);
    }
  }

  return (
    <>
      <header className="page__head">
        <h1>{t('connectors.title')}</h1>
      </header>

      {load.kind === 'loading' ? <p className="state">{t('connectors.loading')}</p> : null}

      {load.kind === 'denied' ? (
        <p className="state" role="alert">{t('connectors.denied')}</p>
      ) : null}

      {load.kind === 'failed' ? (
        <div className="state" role="alert">
          <p>{load.message}</p>
          <button type="button" onClick={() => void read()}>{t('common.retry')}</button>
        </div>
      ) : null}

      {load.kind !== 'ready' ? null : (
        <>
          <section className="panel">
            <h2>{t('connectors.shipped')}</h2>

            {load.connectors.length === 0 ? (
              <p className="note">{t('connectors.none')}</p>
            ) : (
              load.connectors.map((connector) => (
                <article key={`${connector.provider}-${connector.version}`} className="panel--detail">
                  <h3>
                    {connector.provider} <span className="muted">{connector.version}</span>
                  </h3>

                  {/* The headline, not a footnote. A chip keyed off the raw
                      enum so the colour cannot drift from the meaning. */}
                  <p>
                    <span className={`chip chip--${certificationTone(connector.certification)}`}>
                      {t(`connectors.certification.${connector.certification}` as never)}
                    </span>
                  </p>

                  <p className="note">
                    {t(`connectors.certificationMeans.${connector.certification}` as never)}
                  </p>

                  <dl className="facts">
                    <dt>{t('connectors.reads')}</dt>
                    <dd>{connector.capabilities.join(', ')}</dd>
                  </dl>

                  {connector.knownLimitations.length === 0 ? null : (
                    <>
                      <h4>{t('connectors.limitations')}</h4>
                      <ul className="calls">
                        {connector.knownLimitations.map((limitation) => (
                          <li key={limitation}>{limitation}</li>
                        ))}
                      </ul>
                    </>
                  )}
                </article>
              ))
            )}
          </section>

          <section className="panel">
            <h2>{t('connectors.heldBack')}</h2>

            {said === null ? null : <p className="notice" role="status">{said}</p>}

            {load.held.length === 0 ? (
              <p className="note">{t('connectors.nothingHeld')}</p>
            ) : (
              <>
                <p className="note">{t('connectors.heldNote')}</p>

                <div className="scroll">
                  <table>
                    <thead>
                      <tr>
                        <th scope="col">{t('connectors.colRecord')}</th>
                        <th scope="col">{t('connectors.colWhy')}</th>
                        <th scope="col">{t('connectors.colHeldSince')}</th>
                        <th scope="col">{t('connectors.colUntil')}</th>
                        <th scope="col">{t('connectors.colActions')}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {load.held.map((entry) => (
                        <tr key={entry.id}>
                          <td>
                            {/* A provider's own identifier is a code. */}
                            <span className="mono" dir="ltr">{entry.externalId}</span>
                            <br />
                            <span className="muted">
                              {entry.connector} · {entry.contract} v{entry.version}
                            </span>
                          </td>
                          <td>{entry.reasonDetail}</td>
                          <td>{format.date(entry.quarantinedAt)}</td>
                          <td>{format.date(entry.expiresAt)}</td>
                          <td>
                            <button
                              type="button"
                              className="primary"
                              disabled={busy !== null}
                              onClick={() => void replay(entry)}
                            >
                              {busy === entry.id ? t('connectors.replaying') : t('connectors.replay')}
                            </button>{' '}
                            <button
                              type="button"
                              disabled={busy !== null}
                              onClick={() => void dismiss(entry)}
                            >
                              {t('connectors.dismiss')}
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </>
            )}
          </section>
        </>
      )}
    </>
  );
}

/**
 * Which chip a certification wears.
 *
 * Fixture-tested and experimental are deliberately the SAME warning tone.
 * "Automated tests only. Not a production promise." is not a lesser worry than
 * "community or experimental" — a dealership running its month-end on either
 * is taking the same kind of risk, and colouring one of them calmly would be
 * the screen taking a view it has no business taking.
 */
function certificationTone(certification: ConnectorSummary['certification']): string {
  switch (certification) {
    case 'ProductionCertified':
      return 'done';
    case 'SandboxCertified':
      return 'inprogress';
    default:
      return 'warn';
  }
}
