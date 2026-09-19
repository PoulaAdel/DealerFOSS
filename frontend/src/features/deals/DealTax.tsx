// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   DealTax — the tax on a deal: what is charged, and where each figure came
//   from.
//
// Usage:
//   Mounted by DealsPage while the deal's terms are open. SoldTax renders the
//   same lines read-only once they are frozen.
//
// Coding Instructions:
//   EVERY LINE SAYS WHERE ITS FIGURE CAME FROM, ON SCREEN, AND THAT IS THE
//   WHOLE POINT (ADR-024 R4/R5). Nothing computes tax yet: a person types it,
//   and the alternative to saying so is a screen that presents a typed number
//   with the same authority as one a rate table produced. That is the failure
//   this band exists to prevent, so the provenance column is not decoration and
//   must not be dropped to save width.
//
//   The address is required as soon as there is a line, and refused by the API
//   otherwise, because an address is how a rate is defended months later. It is
//   asked for as state / county / postcode / country separately — the county is
//   its own field because US sales tax varies by state AND county, which is the
//   gap closed on 2026-09-05.
//
//   The rate is entered as a PERCENTAGE and stored as a fraction. 6.25 in the
//   box becomes 0.0625 on the wire. Anyone typing a tax rate is reading it off a
//   table that says "6.25%", and asking them to divide by a hundred is how a
//   deal gets taxed a hundred times over.
//
//   Saving replaces the whole set, like the charges and products editors. An
//   empty form would mean adding one line deleted the rest.

import { useState } from 'react';
import { post } from '../../shared/api';
import type { DealDetail, TaxLineView } from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';

/** One row being edited. Everything is a string until it is saved. */
type Row = {
  key: string;
  description: string;
  jurisdiction: string;
  basis: string;
  ratePercent: string;
  amount: string;

  /**
   * Whether somebody typed over the computed figure. Tracked per row so the
   * arithmetic stops following the moment a person takes charge of it, rather
   * than overwriting what they just entered on the next keystroke elsewhere.
   */
  amountEdited: boolean;
};

/**
 * Basis times rate, to the cent, or null when either box is empty.
 *
 * Rounded here rather than left to float noise. A cent of drift in a tax figure
 * is a cent the invoice and the ledger will disagree about forever.
 */
function worksOutTo(row: { basis: string; ratePercent: string }): string | null {
  if (row.basis.trim() === '' || row.ratePercent.trim() === '') {
    return null;
  }

  const basis = Number(row.basis);
  const percent = Number(row.ratePercent);

  if (!Number.isFinite(basis) || !Number.isFinite(percent)) {
    return null;
  }

  return (Math.round(basis * percent) / 100).toFixed(2);
}

/**
 * Whether this row's amount contradicts its own basis and rate.
 *
 * A cent of tolerance, because the boxes hold what somebody typed and
 * "2062.50" against a computed "2062.5" is agreement, not a discrepancy.
 */
function disagrees(row: Row): boolean {
  const expected = worksOutTo(row);
  if (expected === null || row.amount.trim() === '') {
    return false;
  }

  return Math.abs(Number(row.amount) - Number(expected)) > 0.005;
}

function rowsFrom(lines: readonly TaxLineView[]): Row[] {
  return lines.map((line, index) => ({
    key: `${line.id}-${index}`,
    description: line.description,
    jurisdiction: line.jurisdiction,
    basis: String(line.basis),
    // Back to a percentage for the box it is read in.
    ratePercent: line.rate === 0 ? '' : String(line.rate * 100),
    amount: String(line.amount),

    // A saved line is somebody's settled figure. Treating it as untouched would
    // let a later edit to the basis silently rewrite it.
    amountEdited: true,
  }));
}

function blankRow(): Row {
  return {
    key: `new-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
    description: '',
    jurisdiction: '',
    basis: '',
    ratePercent: '',
    amount: '',
    amountEdited: false,
  };
}

export function DealTax({
  deal,
  onChanged,
}: {
  deal: DealDetail;
  onChanged: (updated: DealDetail) => void;
}) {
  const { t, format } = useI18n();
  const describe = useApiMessage();
  const money = (amount: number) => format.money(amount, deal.currency);

  const [rows, setRows] = useState<Row[]>(() => rowsFrom(deal.taxLines));
  const [area, setArea] = useState(deal.taxedAt?.administrativeArea ?? '');
  const [county, setCounty] = useState(deal.taxedAt?.county ?? '');
  const [postalCode, setPostalCode] = useState(deal.taxedAt?.postalCode ?? '');
  const [country, setCountry] = useState(deal.taxedAt?.country ?? '');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const total = rows.reduce((sum, r) => sum + (Number(r.amount) || 0), 0);

  function update(key: string, change: Partial<Row>) {
    setRows((current) =>
      current.map((r) => {
        if (r.key !== key) {
          return r;
        }

        const next = { ...r, ...change };

        // THE AMOUNT FOLLOWS THE ARITHMETIC. A person used to type basis, rate
        // AND the answer, with nothing checking the three agreed — so a deal
        // could carry a tax figure that its own basis and rate contradict, and
        // the figure is the one that goes on the invoice and into the ledger.
        //
        // Still overridable, deliberately. EnteredByPerson exists so an
        // unsupported jurisdiction is a label rather than a blocker, and a
        // capped or tiered tax is not basis times rate. What ends here is the
        // SILENT disagreement: override it and the row says what the
        // arithmetic gives instead.
        if (!next.amountEdited && (change.basis !== undefined || change.ratePercent !== undefined)) {
          next.amount = worksOutTo(next) ?? next.amount;
        }

        return next;
      }),
    );
  }

  async function save() {
    setBusy(true);
    setError(null);

    try {
      const lines = rows
        // A row somebody started and abandoned is not a tax. Discarding it here
        // rather than sending it means the API's "a line needs a description"
        // refusal is reserved for a real mistake.
        .filter((r) => r.description.trim() !== '' || r.amount.trim() !== '')
        .map((r) => ({
          description: r.description.trim(),
          jurisdiction: r.jurisdiction.trim(),
          basis: Number(r.basis) || 0,
          rate: (Number(r.ratePercent) || 0) / 100,
          amount: Number(r.amount) || 0,
          provenance: 'EnteredByPerson',
        }));

      onChanged(
        await post<DealDetail>(`/deals/${deal.id}/tax`, {
          lines,
          taxedAt:
            lines.length === 0
              ? null
              : {
                  administrativeArea: area.trim() || null,
                  county: county.trim() || null,
                  postalCode: postalCode.trim() || null,
                  country: country.trim().toUpperCase(),
                },
        }),
      );
    } catch (failure) {
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <h3>{t('tax.title')}</h3>
      <p className="note">{t('tax.lede')}</p>

      <fieldset className="taxAddress">
        <legend>{t('tax.workedOutFrom')}</legend>

        <div className="row">
          <label htmlFor="tax-area">{t('tax.state')}</label>
          <input
            id="tax-area"
            value={area}
            disabled={busy}
            onChange={(event) => setArea(event.target.value)}
          />

          {/* Its own field, not folded into the state: a US rate depends on both. */}
          <label htmlFor="tax-county">{t('tax.county')}</label>
          <input
            id="tax-county"
            value={county}
            disabled={busy}
            onChange={(event) => setCounty(event.target.value)}
          />

          <label htmlFor="tax-postal">{t('tax.postalCode')}</label>
          <input
            id="tax-postal"
            value={postalCode}
            disabled={busy}
            onChange={(event) => setPostalCode(event.target.value)}
          />

          <label htmlFor="tax-country">{t('tax.country')}</label>
          <input
            id="tax-country"
            value={country}
            disabled={busy}
            maxLength={2}
            onChange={(event) => setCountry(event.target.value)}
          />
        </div>
      </fieldset>

      <div className="scroll">
        <table className="table terms">
          <thead>
            <tr>
              <th scope="col">{t('tax.colDescription')}</th>
              <th scope="col">{t('tax.colJurisdiction')}</th>
              <th scope="col" className="num">
                {t('tax.colBasis')}
              </th>
              <th scope="col" className="num">
                {t('tax.colRate')}
              </th>
              <th scope="col" className="num">
                {t('tax.colAmount')}
              </th>
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 ? (
              <tr>
                <td colSpan={5} className="muted">
                  {t('tax.none')}
                </td>
              </tr>
            ) : (
              rows.map((row, index) => (
                <tr key={row.key}>
                  <td>
                    <input
                      aria-label={t('tax.descriptionOfLine', { line: index + 1 })}
                      value={row.description}
                      disabled={busy}
                      onChange={(event) => update(row.key, { description: event.target.value })}
                    />
                  </td>
                  <td>
                    <input
                      aria-label={t('tax.jurisdictionOfLine', { line: index + 1 })}
                      value={row.jurisdiction}
                      disabled={busy}
                      onChange={(event) => update(row.key, { jurisdiction: event.target.value })}
                    />
                  </td>
                  <td className="num">
                    <input
                      inputMode="decimal"
                      aria-label={t('tax.basisOfLine', { line: index + 1 })}
                      value={row.basis}
                      disabled={busy}
                      onChange={(event) => update(row.key, { basis: event.target.value })}
                    />
                  </td>
                  <td className="num">
                    <input
                      inputMode="decimal"
                      aria-label={t('tax.rateOfLine', { line: index + 1 })}
                      value={row.ratePercent}
                      disabled={busy}
                      onChange={(event) => update(row.key, { ratePercent: event.target.value })}
                    />
                  </td>
                  <td className="num">
                    <input
                      inputMode="decimal"
                      aria-label={t('tax.amountOfLine', { line: index + 1 })}
                      value={row.amount}
                      disabled={busy}
                      onChange={(event) =>
                        update(row.key, { amount: event.target.value, amountEdited: true })
                      }
                    />

                    {/* Said only when the three actually disagree. A note under
                        every row would be read as decoration within a week, and
                        the one row that matters would be invisible among them. */}
                    {disagrees(row) ? (
                      <span className="error" role="status">
                        {t('deals.taxDisagrees', {
                          basis: money(Number(row.basis)),
                          rate: `${row.ratePercent}%`,
                          expected: money(Number(worksOutTo(row))),
                        })}
                      </span>
                    ) : null}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <p className="note">{t('tax.totalIs', { total: money(total) })}</p>

      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>

      <div className="actions">
        <button
          type="button"
          disabled={busy}
          onClick={() => setRows((current) => [...current, blankRow()])}
        >
          {t('tax.addLine')}
        </button>
        <button type="button" disabled={busy} onClick={() => void save()}>
          {busy ? t('common.saving') : t('tax.save')}
        </button>
      </div>
    </>
  );
}

/**
 * The tax as charged, once the deal is frozen. Read-only, and every line still
 * says where its figure came from — that is the question somebody asks months
 * later with the customer on the phone, and it is the only moment the
 * provenance really earns its column.
 */
export function SoldTax({ deal }: { deal: DealDetail }) {
  const { t, format } = useI18n();
  const money = (amount: number) => format.money(amount, deal.currency);

  return (
    <>
      <h3>{t('tax.title')}</h3>
      <div className="scroll">
        <table className="table terms">
          <thead>
            <tr>
              <th scope="col">{t('tax.colDescription')}</th>
              <th scope="col">{t('tax.colJurisdiction')}</th>
              <th scope="col">{t('tax.colSource')}</th>
              <th scope="col" className="num">
                {t('tax.colAmount')}
              </th>
            </tr>
          </thead>
          <tbody>
            {deal.taxLines.map((line) => (
              <tr key={line.id}>
                <td>{line.description}</td>
                <td className="mono">{line.jurisdiction}</td>
                <td>
                  {/* Never a bare colour or icon: the chip says its own name,
                      which is the standing rule in app.css. */}
                  <span className={`chip chip--tax-${line.provenance.toLowerCase()}`}>
                    {line.provenance === 'Pack' && line.packId !== null
                      ? t('tax.fromPack', { pack: line.packId, version: line.packVersion ?? 0 })
                      : t(`tax.from.${line.provenance}` as 'tax.from.EnteredByPerson')}
                  </span>
                </td>
                <td className="num">{money(line.amount)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <td colSpan={3}>{t('tax.totalLabel')}</td>
              <td className="num strong">{money(deal.taxTotal)}</td>
            </tr>
          </tfoot>
        </table>
      </div>
    </>
  );
}
