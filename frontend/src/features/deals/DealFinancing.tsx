// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   DealFinancing — the finance structure on a deal, and the monthly payment
//   that follows from it. SoldFinancing renders the same terms read-only once
//   the deal's numbers have frozen.
//
// Usage:
//   Mounted by DealsPage after DealProducts while the deal's terms are open.
//
// Coding Instructions:
//   Three things here are deliberate.
//
//   (1) NOT IN THE DEAL'S COLUMN. Every figure here sits in its own table below
//   the one that adds up to the amount due, because none of it is part of that
//   total — a down payment is how the customer pays, not money off. Adding it
//   into the column above would make a column that is supposed to reach the
//   printed total stop reaching it, which is the defect that has been fixed in
//   this product four times. The tests scope to the table named "the numbers on
//   this deal" for that reason; keep this table out of it.
//
//   (2) THE PAYMENT IS NOT COMPUTED HERE. The server works it out and sends it
//   down, and this screen prints what it was given. A second implementation of
//   the amortisation in the browser would be a second answer to "what does the
//   customer pay", and the two would drift — the same reasoning as ADR-025 for
//   authorization rules, applied to arithmetic.
//
//   (3) THE RATE BOX IS A PERCENTAGE AND THE WIRE IS A FRACTION. A person types
//   6.49 because that is what is written on the rate sheet; the contract carries
//   0.0649, like a tax rate. The conversion happens once, on save, and the box
//   says "(%)" so nobody has to guess which is wanted.

import { useState } from 'react';
import { post } from '../../shared/api';
import type { DealDetail, DealFinancingView } from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';

type Draft = {
  lender: string;
  downPayment: string;
  /** As a person types it: 6.49, not 0.0649. */
  aprPercent: string;
  termMonths: string;
};

function draftFrom(financing: DealFinancingView | null): Draft {
  return {
    lender: financing?.lender ?? '',
    downPayment: financing === null ? '' : String(financing.downPayment),
    aprPercent: financing === null ? '' : String(financing.annualPercentageRate * 100),
    termMonths: financing === null ? '' : String(financing.termMonths),
  };
}

export function DealFinancing({
  deal,
  onChanged,
}: {
  deal: DealDetail;
  onChanged: (updated: DealDetail) => void;
}) {
  const { t, format } = useI18n();
  const describe = useApiMessage();
  const money = (amount: number) => format.money(amount, deal.currency);

  const [draft, setDraft] = useState<Draft>(() => draftFrom(deal.financing));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function save(financing: object | null) {
    setError(null);
    setBusy(true);

    try {
      onChanged(await post<DealDetail>(`/deals/${deal.id}/financing`, { financing }));
    } catch (failure) {
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <h3>{t('financing.title')}</h3>
      <p className="note">{t('financing.lede')}</p>

      <fieldset className="taxAddress">
        <legend className="visually-hidden">{t('financing.title')}</legend>

        <div className="row">
          {/* The provider NAME is dealership data and is stored as typed. */}
          <label htmlFor="fin-lender">{t('financing.provider')}</label>
          <input
            id="fin-lender"
            value={draft.lender}
            disabled={busy}
            onChange={(e) => setDraft({ ...draft, lender: e.target.value })}
          />

          <label htmlFor="fin-down">{t('financing.downPayment')}</label>
          <input
            id="fin-down"
            inputMode="decimal"
            value={draft.downPayment}
            disabled={busy}
            onChange={(e) => setDraft({ ...draft, downPayment: e.target.value })}
          />

          <label htmlFor="fin-apr">{t('financing.aprPercent')}</label>
          <input
            id="fin-apr"
            inputMode="decimal"
            value={draft.aprPercent}
            disabled={busy}
            onChange={(e) => setDraft({ ...draft, aprPercent: e.target.value })}
          />

          <label htmlFor="fin-term">{t('financing.termMonths')}</label>
          <input
            id="fin-term"
            inputMode="numeric"
            value={draft.termMonths}
            disabled={busy}
            onChange={(e) => setDraft({ ...draft, termMonths: e.target.value })}
          />
        </div>
      </fieldset>

      <p className="note">{t('financing.providerHint')}</p>

      {deal.financing === null ? (
        <p className="note">{t('financing.notFinanced')}</p>
      ) : (
        <PaymentTerms financing={deal.financing} amountDue={deal.amountDue} money={money} />
      )}

      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>

      <div className="actions">
        <button
          type="button"
          disabled={busy}
          onClick={() =>
            void save({
              lender: draft.lender.trim() === '' ? null : draft.lender.trim(),
              downPayment: Number(draft.downPayment) || 0,
              // The one conversion in this file. Divided rather than parsed
              // differently, so a rate sheet's 6.49 becomes the 0.0649 the
              // contract carries.
              annualPercentageRate: (Number(draft.aprPercent) || 0) / 100,
              termMonths: Number(draft.termMonths) || 0,
            })
          }
        >
          {busy
            ? t('common.saving')
            : deal.financing === null
              ? t('financing.finance')
              : t('financing.save')}
        </button>

        {deal.financing === null ? null : (
          <button
            type="button"
            disabled={busy}
            onClick={() => {
              setDraft(draftFrom(null));
              void save(null);
            }}
          >
            {t('financing.makeItCash')}
          </button>
        )}
      </div>
    </>
  );
}

/**
 * The terms as they were when the deal froze. Read-only, like SoldTax and
 * SoldRegistrationAddress: this is the payment the customer was quoted and a
 * manager approved.
 */
export function SoldFinancing({ deal }: { deal: DealDetail }) {
  const { t, format } = useI18n();

  if (deal.financing === null) {
    return null;
  }

  return (
    <>
      <h3>{t('financing.title')}</h3>
      <PaymentTerms
        financing={deal.financing}
        amountDue={deal.amountDue}
        money={(amount) => format.money(amount, deal.currency)}
      />
    </>
  );
}

/**
 * The figures, in their own table. Shared by the editor and the frozen view so
 * the desk reads the same numbers either side of submission.
 *
 * Deliberately NOT inside the deal's money table — see the note at the top of
 * this file. Its accessible name is its own, so a test that reads the column of
 * "the numbers on this deal" cannot pick these up and conclude that the deal
 * stopped adding up.
 */
function PaymentTerms({
  financing,
  amountDue,
  money,
}: {
  financing: DealFinancingView;
  amountDue: number;
  money: (amount: number) => string;
}) {
  const { t, format } = useI18n();

  const rate = `${format.number(financing.annualPercentageRate * 100, {
    maximumFractionDigits: 3,
  })}%`;

  const term = t('financing.months', { count: financing.termMonths });

  return (
    <>
      <div className="scroll">
        <table className="table terms">
          <caption className="visually-hidden">{t('financing.title')}</caption>
          <tbody>
            <tr>
              <td>{t('financing.downPayment')}</td>
              <td className="num">{money(financing.downPayment)}</td>
            </tr>
            <tr>
              <td>{t('financing.amountFinanced')}</td>
              <td className="num">{money(financing.amountFinanced)}</td>
            </tr>
            <tr>
              <td>{t('financing.rateAndTerm', { rate, term })}</td>
              <td className="num">—</td>
            </tr>

            {financing.monthlyPayment === null ? (
              // A reprice has left nothing to finance. Said in words rather than
              // shown as a zero payment, which would read as a free car — and the
              // deal cannot be submitted in this state either way.
              <tr>
                <td colSpan={2}>
                  <span className="strong">
                    {t('financing.nothingToFinance', {
                      down: money(financing.downPayment),
                      due: money(amountDue),
                    })}
                  </span>
                </td>
              </tr>
            ) : (
              <>
                <tr>
                  <td className="strong">{t('financing.monthlyPayment')}</td>
                  <td className="num strong">{money(financing.monthlyPayment)}</td>
                </tr>

                {/* Only when it differs. A final payment identical to the other
                    fifty-nine is a row that makes a reader hunt for a difference
                    that is not there. */}
                {financing.finalPayment === null ||
                financing.finalPayment === financing.monthlyPayment ? null : (
                  <tr>
                    <td>{t('financing.finalPayment')}</td>
                    <td className="num">{money(financing.finalPayment)}</td>
                  </tr>
                )}

                {financing.totalOfPayments === null ? null : (
                  <tr>
                    <td>{t('financing.totalOfPayments')}</td>
                    <td className="num">{money(financing.totalOfPayments)}</td>
                  </tr>
                )}

                {financing.financeCharge === null ? null : (
                  <tr>
                    <td>{t('financing.financeCharge')}</td>
                    <td className="num">{money(financing.financeCharge)}</td>
                  </tr>
                )}
              </>
            )}

            {financing.lender === null ? null : (
              <tr>
                {/* Printed as stored: a finance company's name is dealership
                    data and is not translated. */}
                <td>{t('financing.provider')}</td>
                <td className="num">{financing.lender}</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* The sentence that stops somebody reading the cash down as a discount.
          It is the question this band gets asked most. */}
      <p className="note">{t('financing.notMoneyOff', { amountDue: money(amountDue) })}</p>
    </>
  );
}
