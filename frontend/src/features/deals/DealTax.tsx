// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   DealTax — the tax on a deal: what is charged, and where each figure came
//   from. Either worked out from the dealership's rate table or typed.
//
// Usage:
//   Mounted by DealsPage while the deal's terms are open. SoldTax renders the
//   same lines read-only once they are frozen.
//
// Coding Instructions:
//   EVERY LINE SAYS WHERE ITS FIGURE CAME FROM, ON SCREEN, AND THAT IS THE
//   WHOLE POINT (ADR-024 R4/R5). The provenance column is not decoration and
//   must not be dropped to save width: a typed figure must never look like one
//   a rate table produced.
//
//   "WORK OUT THE TAX" IS THE SERVER'S ACT, NOT THIS SCREEN'S. It posts to
//   /tax/from-pack and the server resolves, computes and writes the lines
//   itself, because it is the only thing allowed to sign a pack's name to a
//   figure. This screen cannot construct a pack-sourced line and must not try —
//   the API refuses a `Pack` provenance it did not produce.
//
//   A ROW THAT IS TOUCHED STOPS BEING THE PACK'S, visibly and on the wire. The
//   chip disappears the moment somebody edits any field of it and the line is
//   sent as EnteredByPerson, because a person who changes a figure owns it. An
//   untouched pack row is sent back WITH its id and the pack's name, which is
//   how editing one row leaves the others' authorship alone — this editor
//   replaces the whole set, and always has.
//
//   AN UNTOUCHED ROW IS SENT FROM THE SAVED LINE, NOT FROM THE INPUT BOXES, and
//   that is not tidiness. The server compares a pack row field for field against
//   what it stored, and re-parsing "1.5" back into 0.015 can land one unit in
//   the last place away from the stored decimal — which would be a refusal
//   nobody could explain from the screen. So the saved line is kept whole and
//   sent verbatim.
//
//   NOT RESOLVING IS AN ORDINARY ANSWER (R5), not an error. No registration
//   address, no pack enabled, and no rate covering the address each get their
//   own sentence, because they need different things done about them — and the
//   deal is untouched, so whatever was typed is still there.
//
//   The rate is entered as a PERCENTAGE and stored as a fraction. 6.25 in the
//   box becomes 0.0625 on the wire. Anyone typing a tax rate is reading it off a
//   table that says "6.25%", and asking them to divide by a hundred is how a
//   deal gets taxed a hundred times over.

import { useState } from 'react';
import { post } from '../../shared/api';
import type { DealDetail, DealTaxFromPack, TaxLineView } from '../../shared/contracts';
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

  /**
   * The saved line this row still is, when a pack produced it and nobody has
   * touched it since. Null for anything a person typed, and cleared by the first
   * edit — see the file header for why the whole line is kept rather than just
   * its ids.
   */
  fromPack: TaxLineView | null;
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

    fromPack:
      line.provenance === 'Pack' && line.packId !== null && line.packVersion !== null
        ? line
        : null,
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
    fromPack: null,
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
  const [working, setWorking] = useState(false);
  const [unresolved, setUnresolved] = useState<DealTaxFromPack['outcome'] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const total = rows.reduce((sum, r) => sum + (Number(r.amount) || 0), 0);

  function update(key: string, change: Partial<Row>) {
    setRows((current) =>
      current.map((r) => {
        if (r.key !== key) {
          return r;
        }

        const next = { ...r, ...change };

        // Touching any field ends the pack's claim on this row. See the header:
        // a person who changes a figure owns it, and the chip going away is the
        // screen saying so before the save does.
        next.fromPack = null;

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

  /**
   * Asks the server to work the tax out from the dealership's rate table. It
   * resolves, computes and writes the lines; this screen only reports what came
   * back and redraws from it.
   */
  async function workItOut() {
    setWorking(true);
    setError(null);
    setUnresolved(null);

    try {
      const answer = await post<DealTaxFromPack>(`/deals/${deal.id}/tax/from-pack`, {});

      if (answer.outcome === 'Resolved') {
        setRows(rowsFrom(answer.deal.taxLines));
        setArea(answer.deal.taxedAt?.administrativeArea ?? '');
        setCounty(answer.deal.taxedAt?.county ?? '');
        setPostalCode(answer.deal.taxedAt?.postalCode ?? '');
        setCountry(answer.deal.taxedAt?.country ?? '');
      } else {
        // The deal came back untouched, so nothing typed is lost. Say which
        // answer it was rather than "no".
        setUnresolved(answer.outcome);
      }

      onChanged(answer.deal);
    } catch (failure) {
      setError(describe(failure));
    } finally {
      setWorking(false);
    }
  }

  async function save() {
    setBusy(true);
    setError(null);
    setUnresolved(null);

    try {
      const lines = rows
        // A row somebody started and abandoned is not a tax. Discarding it here
        // rather than sending it means the API's "a line needs a description"
        // refusal is reserved for a real mistake.
        .filter((r) => r.description.trim() !== '' || r.amount.trim() !== '')
        .map((r) =>
          r.fromPack === null
            ? {
                description: r.description.trim(),
                jurisdiction: r.jurisdiction.trim(),
                basis: Number(r.basis) || 0,
                rate: (Number(r.ratePercent) || 0) / 100,
                amount: Number(r.amount) || 0,
                provenance: 'EnteredByPerson',
              }
            : {
                // Verbatim from the saved line, not from the boxes — see the
                // header. The server checks these field for field.
                description: r.fromPack.description,
                jurisdiction: r.fromPack.jurisdiction,
                basis: r.fromPack.basis,
                rate: r.fromPack.rate,
                amount: r.fromPack.amount,
                provenance: 'Pack',
                packId: r.fromPack.packId,
                packVersion: r.fromPack.packVersion,
                id: r.fromPack.id,
              },
        );

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

      <div className="actions">
        <button type="button" disabled={busy || working} onClick={() => void workItOut()}>
          {working ? t('tax.workingOut') : t('tax.workOutFromPack')}
        </button>
      </div>

      {/* Each outcome gets its own sentence because each needs something
          different done about it: add an address, enable a pack, or accept that
          this jurisdiction is typed by hand. */}
      {unresolved === null ? null : (
        <p className="note" role="status">
          {unresolved === 'NoAddress' ? t('tax.outcomeNoAddress') : null}
          {unresolved === 'NoPackEnabled' ? t('tax.outcomeNoPackEnabled') : null}
          {unresolved === 'NoRateForAddress' ? t('tax.outcomeNoRateForAddress') : null}
        </p>
      )}

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
              <th scope="col">{t('tax.colSource')}</th>
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 ? (
              <tr>
                <td colSpan={6} className="muted">
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
                  <td>
                    {/* The chip is here while the row is still the pack's. Edit
                        any field of it and this becomes "a person entered it",
                        before the save rather than after. */}
                    {row.fromPack === null ? (
                      <span className="chip chip--tax-enteredbyperson">
                        {t('tax.from.EnteredByPerson')}
                      </span>
                    ) : (
                      <span className="chip chip--tax-pack">
                        {t('tax.fromPack', {
                          pack: row.fromPack.packId ?? '',
                          version: row.fromPack.packVersion ?? 0,
                        })}
                      </span>
                    )}
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
