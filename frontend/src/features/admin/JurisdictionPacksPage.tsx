// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   JurisdictionPacksPage — which tax rate tables this installation holds,
//   where each came from, and whether anybody has taken responsibility for one.
//
// Usage:
//   A console screen, beside the dealership list.
//
// Coding Instructions:
//   THIS SCREEN'S JOB IS TO MAKE A CLAIM AUDITABLE, not to list rows. Every
//   column is something somebody would need in order to defend a tax figure
//   months later: which pack, which version, which source, read when, hashed to
//   what, reviewed by whom. The hash is shown rather than hidden because a
//   reviewer approves BYTES — if the file changes, their approval no longer
//   covers it, and the only way to see that is to be shown the hash.
//
//   THE REVIEW BUTTON IS NOT OFFERED WHILE A BASIS FLAG IS UNDECIDED. The API
//   refuses anyway, so this is not the safety mechanism — it is the screen
//   refusing to offer an action it already knows will be rejected, which is how
//   a refusal stays meaningful instead of becoming something people click
//   through. In its place the row says WHICH flags nobody has decided, because
//   "it cannot be reviewed" is not actionable and "nobody has decided whether
//   the trade-in comes off the taxable amount" is.
//
//   Withdrawing asks first. It stops the pack resolving tax on future deals for
//   every dealership on the installation, which is a real change to what people
//   are charged — and like suspending a dealership, the confirmation belongs
//   here rather than in the API, which is right to do what an authorized
//   request tells it.
//
//   Reviewing deliberately does NOT ask first. It is additive, recorded with the
//   reviewer's name, and reversible by withdrawing; a confirmation on every
//   approval would train people to dismiss the one that matters.

import { useCallback, useEffect, useState } from 'react';
import { adminApi, adminPost } from '../../shared/adminApi';
import { Confirm } from '../../shared/Confirm';
import type { PackView } from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';
import { useEnumLabel } from '../../shared/i18n/enums';

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; packs: PackView[] }
  | { kind: 'failed'; message: string };

/** Identity is the pack and the version together, never the pack alone. */
function keyOf(pack: PackView): string {
  return `${pack.packId}@${pack.version}`;
}

export function JurisdictionPacksPage() {
  const { t, format } = useI18n();
  const describe = useApiMessage();
  const label = useEnumLabel();

  const [load, setLoad] = useState<Load>({ kind: 'loading' });
  const [working, setWorking] = useState<string | null>(null);
  const [loadingShipped, setLoadingShipped] = useState(false);
  const [shippedResult, setShippedResult] = useState<number | null>(null);
  const [withdrawing, setWithdrawing] = useState<PackView | null>(null);

  const fetchPacks = useCallback(async () => {
    setLoad({ kind: 'loading' });

    try {
      setLoad({ kind: 'ready', packs: await adminApi<PackView[]>('/jurisdiction-packs') });
    } catch (failure) {
      setLoad({ kind: 'failed', message: describe(failure) });
    }
  }, [describe]);

  useEffect(() => {
    void fetchPacks();
  }, [fetchPacks]);

  async function loadShipped() {
    setLoadingShipped(true);
    setShippedResult(null);

    try {
      const added = await adminPost<PackView[]>('/jurisdiction-packs/shipped', {});
      setShippedResult(added.length);
      await fetchPacks();
    } catch (failure) {
      setLoad({ kind: 'failed', message: describe(failure) });
    } finally {
      setLoadingShipped(false);
    }
  }

  async function act(pack: PackView, action: 'review' | 'withdraw') {
    setWithdrawing(null);
    setWorking(keyOf(pack));

    try {
      await adminPost(
        `/jurisdiction-packs/${encodeURIComponent(pack.packId)}/${pack.version}/${action}`,
        {},
      );
      await fetchPacks();
    } catch (failure) {
      setLoad({ kind: 'failed', message: describe(failure) });
    } finally {
      setWorking(null);
    }
  }

  return (
    <>
      <header className="page__head">
        <h1>{t('packs.title')}</h1>
        <button
          type="button"
          className="primary"
          disabled={loadingShipped}
          onClick={() => void loadShipped()}
        >
          {loadingShipped ? t('packs.loadingShipped') : t('packs.loadShipped')}
        </button>
      </header>

      <p className="note">{t('packs.lede')}</p>

      {shippedResult === null ? null : (
        <p className="state" aria-live="polite">
          {shippedResult === 0
            ? t('packs.loadedNone')
            : t('packs.loadedSome', { count: shippedResult })}
        </p>
      )}

      {load.kind === 'loading' ? (
        <p className="state" aria-live="polite">
          {t('packs.loading')}
        </p>
      ) : null}

      {load.kind === 'failed' ? (
        <div className="state" role="alert">
          <p>{load.message}</p>
          <button type="button" onClick={() => void fetchPacks()}>
            {t('common.retry')}
          </button>
        </div>
      ) : null}

      {load.kind === 'ready' && load.packs.length === 0 ? (
        <p className="state">{t('packs.none')}</p>
      ) : null}

      {load.kind === 'ready' && load.packs.length > 0 ? (
        <div className="scroll">
          <table>
            <caption className="visually-hidden">
              {t('packs.caption', { count: load.packs.length })}
            </caption>
            <thead>
              <tr>
                <th scope="col">{t('packs.colPack')}</th>
                <th scope="col">{t('packs.colScheme')}</th>
                <th scope="col">{t('packs.colVersion')}</th>
                <th scope="col">{t('packs.colStatus')}</th>
                <th scope="col">{t('packs.colEffective')}</th>
                <th scope="col" className="num">
                  {t('packs.colRates')}
                </th>
                <th scope="col">{t('packs.colSource')}</th>
                <th scope="col">{t('packs.colReviewed')}</th>
                <th scope="col">{t('packs.colActions')}</th>
              </tr>
            </thead>
            <tbody>
              {load.packs.map((pack) => (
                <tr key={keyOf(pack)}>
                  <td>
                    {/* A pack id and a country code are codes, not prose. */}
                    <span className="mono" dir="ltr">
                      {pack.packId}
                    </span>{' '}
                    <span className="mono" dir="ltr">
                      {pack.country}
                    </span>
                  </td>
                  <td>{label('packScheme', pack.scheme)}</td>
                  <td className="mono" dir="ltr">
                    {pack.version}
                  </td>
                  <td>
                    {/* The class keys off the raw value; the word comes from the
                        catalogue, so the colour never carries the meaning. */}
                    <span className={`chip chip--pack-${pack.status.toLowerCase()}`}>
                      {label('packStatus', pack.status)}
                    </span>
                  </td>
                  <td>
                    {pack.effectiveTo === null
                      ? t('packs.effectiveOpen', { from: format.date(pack.effectiveFrom) })
                      : t('packs.effectiveRange', {
                          from: format.date(pack.effectiveFrom),
                          to: format.date(pack.effectiveTo),
                        })}
                  </td>
                  <td className="num">{pack.rateCount}</td>
                  <td>
                    {/* Opened in a new tab and never followed by the app: this is
                        a third party's page, cited so a reviewer can go and read
                        it. noreferrer as well as noopener, because which
                        installation is checking a tax table is nobody's business. */}
                    <a href={pack.sourceUri} target="_blank" rel="noopener noreferrer">
                      {t('packs.openSource')}
                    </a>
                    <br />
                    <span className="note">
                      {t('packs.readOn', { date: format.date(pack.fetchedAt) })}
                    </span>
                    <br />
                    {/* Shown, not hidden: an approval covers these bytes, so a
                        changed hash means the approval no longer applies. */}
                    <span className="mono note" dir="ltr">
                      {pack.contentHash.slice(0, 12)}
                    </span>
                  </td>
                  <td>
                    {pack.reviewedBy === null || pack.reviewedAt === null ? (
                      <span className="note">{t('packs.neverReviewed')}</span>
                    ) : (
                      t('packs.reviewedBy', {
                        who: pack.reviewedBy,
                        date: format.dateTime(pack.reviewedAt),
                      })
                    )}
                  </td>
                  <td>
                    {pack.missingRules.length > 0 ? (
                      // No review button at all. See the file header: the screen
                      // does not offer what the API will refuse, and it says what
                      // is actually missing instead.
                      <span className="error">
                        {t('packs.undecided', { flags: pack.missingRules.join(', ') })}
                      </span>
                    ) : null}

                    {pack.status === 'Draft' && pack.missingRules.length === 0 ? (
                      <button
                        type="button"
                        disabled={working === keyOf(pack)}
                        onClick={() => void act(pack, 'review')}
                      >
                        {t('packs.review')}
                      </button>
                    ) : null}

                    {pack.status === 'Supported' ? (
                      <button
                        type="button"
                        disabled={working === keyOf(pack)}
                        onClick={() => setWithdrawing(pack)}
                      >
                        {t('packs.withdraw')}
                      </button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}

      {withdrawing === null ? null : (
        <Confirm
          title={t('packs.withdrawTitle', {
            pack: withdrawing.packId,
            version: withdrawing.version,
          })}
          body={t('packs.withdrawBody')}
          confirmLabel={t('packs.withdraw')}
          busy={working === keyOf(withdrawing)}
          onConfirm={() => void act(withdrawing, 'withdraw')}
          onCancel={() => setWithdrawing(null)}
        />
      )}
    </>
  );
}
