// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   DealTerms — putting the numbers on a deal while it is still a draft.
//
// Usage:
//   Rendered by DealsPage inside an open deal, only when termsAreOpen.
//
// Coding Instructions:
//   It is mounted on `termsAreOpen` rather than on the status string, and it
//   **disappears** rather than being disabled once a deal is submitted. A
//   disabled form still looks like somewhere to type; somebody fills it in,
//   presses save, and finds out their work is gone. Absent is kinder and
//   truer — the numbers really are frozen at that point.
//
//   Saving replaces the whole set, because that is what the API does
//   (`DealTerms` is not a patch). So the form is always seeded from what is
//   already there — otherwise pressing save after editing one line would
//   silently delete the rest.

import { useState } from 'react';
import { post } from '../../shared/api';
import { chargeKinds } from '../../shared/contracts';
import type { ChargeKind, DealDetail } from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useEnumLabel } from '../../shared/i18n/enums';
import { useApiMessage } from '../../shared/i18n/apiMessage';

interface ChargeRow {
  kind: ChargeKind;
  description: string;
  amount: string;
}

interface TradeInRow {
  description: string;
  allowance: string;
  payoff: string;
}

export function DealTerms({
  deal, onSaved,
}: {
  deal: DealDetail;
  onSaved: (updated: DealDetail) => Promise<void>;
}) {
  const { t } = useI18n();
  const label = useEnumLabel();
  const describe = useApiMessage();

  // Seeded from what is on the deal already: saving replaces everything, so
  // starting empty would quietly wipe whatever was there.
  const [charges, setCharges] = useState<ChargeRow[]>(() =>
    deal.charges.length > 0
      ? deal.charges.map((c) => ({
          kind: c.kind,
          description: c.description,
          amount: String(c.amount),
        }))
      : [{ kind: 'VehiclePrice', description: '', amount: '' }],
  );

  const [tradeIn, setTradeIn] = useState<TradeInRow | null>(() =>
    deal.tradeIn === null
      ? null
      : {
          description: deal.tradeIn.description,
          allowance: String(deal.tradeIn.allowance),
          payoff: String(deal.tradeIn.payoff),
        },
  );

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function update(index: number, change: Partial<ChargeRow>) {
    setCharges(charges.map((row, i) => (i === index ? { ...row, ...change } : row)));
  }

  async function save() {
    setError(null);
    setBusy(true);

    try {
      const updated = await post<DealDetail>(`/deals/${deal.id}/terms`, {
        charges: charges
          .filter((row) => row.description.trim() !== '' || row.amount.trim() !== '')
          .map((row) => ({
            kind: row.kind,
            description: row.description,
            amount: Number(row.amount),
          })),
        tradeIn:
          tradeIn === null
            ? null
            : {
                description: tradeIn.description,
                allowance: Number(tradeIn.allowance),
                payoff: Number(tradeIn.payoff),
              },
      });

      await onSaved(updated);
    } catch (failure) {
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  const priced = charges.some(
    (row) => row.kind === 'VehiclePrice' && row.amount.trim() !== '' && Number(row.amount) !== 0,
  );

  return (
    <section className="terms">
      <h3>{t('terms.title')}</h3>

      {/* Wrapped so it scrolls inside its own box, like every other wide table
          here. The editor's four columns — kind, description, amount, remove —
          do not fit 375px, and this was the one table left unwrapped.

          It was NOT causing the page to scroll sideways: measured after the fact,
          the page never did. `documentElement.scrollWidth` over-reports whenever
          scroll containers are present, which is what made it look that way. The
          honest test is whether `window.scrollTo(200, 0)` moves anything. */}
      <div className="scroll">
      <table>
        <caption className="visually-hidden">{t('terms.caption')}</caption>
        <thead>
          <tr>
            <th scope="col">{t('terms.colLine')}</th>
            <th scope="col">{t('terms.colDescription')}</th>
            <th scope="col" className="num">
              {t('terms.colAmount')}
            </th>
            <th scope="col">
              <span className="visually-hidden">{t('terms.remove')}</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {charges.map((row, index) => (
            <tr key={index}>
              <td>
                <label className="visually-hidden" htmlFor={`kind-${index}`}>
                  {t('terms.lineKind', { n: index + 1 })}
                </label>
                <select
                  id={`kind-${index}`}
                  value={row.kind}
                  onChange={(e) => update(index, { kind: e.target.value as ChargeKind })}
                >
                  {chargeKinds.map((kind) => (
                    <option key={kind} value={kind}>
                      {label('chargeKind', kind)}
                    </option>
                  ))}
                </select>
              </td>
              <td>
                <label className="visually-hidden" htmlFor={`description-${index}`}>
                  {t('terms.lineDescription', { n: index + 1 })}
                </label>
                <input
                  id={`description-${index}`}
                  value={row.description}
                  onChange={(e) => update(index, { description: e.target.value })}
                />
              </td>
              <td>
                <label className="visually-hidden" htmlFor={`amount-${index}`}>
                  {t('terms.lineAmount', { n: index + 1 })}
                </label>
                <input
                  id={`amount-${index}`}
                  // Not type="number": its spinner and locale handling cause more
                  // trouble than they solve for money, and a stray scroll over
                  // the field silently changes a price.
                  inputMode="decimal"
                  value={row.amount}
                  onChange={(e) => update(index, { amount: e.target.value })}
                />
              </td>
              <td>
                <button
                  type="button"
                  className="link"
                  onClick={() => setCharges(charges.filter((_, i) => i !== index))}
                  disabled={charges.length === 1}
                >
                  {t('terms.remove')}
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      </div>

      <div className="actions">
        <button
          type="button"
          onClick={() => setCharges([...charges, { kind: 'Fee', description: '', amount: '' }])}
        >
          {t('terms.addLine')}
        </button>

        {tradeIn === null ? (
          <button
            type="button"
            onClick={() => setTradeIn({ description: '', allowance: '', payoff: '' })}
          >
            {t('terms.addTradeIn')}
          </button>
        ) : (
          <button type="button" onClick={() => setTradeIn(null)}>
            {t('terms.dropTradeIn')}
          </button>
        )}
      </div>

      {tradeIn === null ? null : (
        <>
          <h3>{t('terms.tradeInTitle')}</h3>

          <label htmlFor="trade-description">{t('terms.whatTheyTrade')}</label>
          <input
            id="trade-description"
            value={tradeIn.description}
            onChange={(e) => setTradeIn({ ...tradeIn, description: e.target.value })}
          />

          <label htmlFor="trade-allowance">{t('terms.whatWeAllow')}</label>
          <input
            id="trade-allowance"
            inputMode="decimal"
            value={tradeIn.allowance}
            onChange={(e) => setTradeIn({ ...tradeIn, allowance: e.target.value })}
          />

          <label htmlFor="trade-payoff">{t('terms.whatIsOwed')}</label>
          <input
            id="trade-payoff"
            inputMode="decimal"
            value={tradeIn.payoff}
            onChange={(e) => setTradeIn({ ...tradeIn, payoff: e.target.value })}
          />

          {Number(tradeIn.payoff) > Number(tradeIn.allowance) ? (
            // Said while they are typing rather than after saving: it changes
            // what the customer has to find, and finding out later is worse.
            <p className="note">{t('terms.negativeEquity')}</p>
          ) : null}
        </>
      )}

      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>

      <div className="actions">
        <button type="button" className="primary" onClick={() => void save()} disabled={busy || !priced}>
          {busy ? t('common.saving') : t('terms.save')}
        </button>
      </div>

      {priced ? null : <p className="note">{t('terms.needsPrice')}</p>}
    </section>
  );
}
