// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   RecordPicker — choosing one record out of however many a real dealership
//   has, rather than out of the first two hundred.
//
//   WHAT IT REPLACES, AND WHY THAT WAS WORSE THAN IT LOOKED. Every picker in
//   this application was a bare `<select>` filled from `?limit=200`. Against
//   ~500 customers that is not a long dropdown, it is a screen where three
//   customers in five CANNOT BE CHOSEN AT ALL and nothing says so — the list
//   simply ends. Against cars it was worse: options read "2021 Toyota RAV4 XLE"
//   with no VIN and no stock number, and 32 of the 101 labels were EXACT
//   DUPLICATES, so the wrong car could be booked in and no screen anywhere
//   would ever show that it had been.
//
//   Paging did not fix this and could not: a dropdown has no page two.
//
//   THE SHAPE. A search box that queries the server, a list of what came back,
//   and — once something is chosen — the choice with a way to change it. Not a
//   combobox: this is deliberately plain HTML with real buttons, the same
//   pattern the enquiry screen's customer search already proved, because an
//   invented combobox is a keyboard trap waiting to be written and a native
//   `<select>` cannot search.
//
//   A SHORTLIST IS OFFERED BEFORE ANYONE TYPES, when the caller has one — the
//   cars this customer has been here with, say. Most bookings are a returning
//   customer with the same car, and that case should be one click, not a search.
//   The shortlist never replaces the search: a picker that can only offer known
//   records cannot record a new one.
//
// Usage:
//   <RecordPicker
//     id="diary-customer"
//     label={t('diary.customer')}
//     chosen={customer}
//     onChoose={setCustomer}
//     search={findCustomers}          // (term, signal) => Promise<PickerOption[]>
//     shortlist={theirCars}           // optional, shown before typing
//   />
//
//   An option is { id, label, hint }. `label` is what the record IS; `hint` is
//   what tells two of them apart — a VIN, a stock number, an email.
//
// Coding Instructions:
//   THE HINT IS NOT DECORATION. It is the entire reason this component exists.
//   A caller that passes only a label has rebuilt the duplicate-car problem, so
//   pass whatever distinguishes two otherwise identical records even when it
//   looks redundant on the happy path.
//
//   THE SEARCH IS ABORTED ON EVERY KEYSTROKE, not merely debounced. Debouncing
//   alone makes the race rarer, which is worse than leaving it obvious: a slow
//   answer for "f" landing after the right answer for "focus" leaves the screen
//   confidently showing the wrong list. See useDebounced.
//
//   A FAILED SEARCH SAYS SO. It must never render as "no matches" — that is the
//   same screen a person reads as "this customer does not exist", and the next
//   thing they do is create a duplicate.

import { useCallback, useEffect, useId, useRef, useState } from 'react';
import { useDebounced } from './useDebounced';
import { useI18n } from './i18n';

/** One thing that can be chosen. */
export interface PickerOption {
  id: string;

  /** What the record is. "Marisol Alvarez", "2021 Toyota RAV4 XLE". */
  label: string;

  /**
   * What tells this one apart from an identical-looking one — a VIN, a stock
   * number, an email. Shown beside the label and searched by the server, never
   * by this component.
   */
  hint?: string;
}

type Results =
  | { kind: 'idle' }
  | { kind: 'searching' }
  | { kind: 'ready'; options: PickerOption[] }
  | { kind: 'failed'; message: string };

export function RecordPicker({
  id,
  label,
  chosen,
  onChoose,
  search,
  shortlist = [],
  disabled = false,
}: {
  id: string;
  label: string;

  /** The record currently chosen, or null. */
  chosen: PickerOption | null;

  onChoose: (option: PickerOption | null) => void;

  /**
   * Asks the server. Given the abort signal so a keystroke can cancel the
   * request it replaced; must reject with the signal's AbortError rather than
   * resolving, which `api()` already does.
   */
  search: (term: string, signal: AbortSignal) => Promise<PickerOption[]>;

  /**
   * Offered before anybody types, and nowhere else. Empty is ordinary.
   */
  shortlist?: PickerOption[];

  disabled?: boolean;
}) {
  const { t } = useI18n();

  const [term, setTerm] = useState('');
  const [results, setResults] = useState<Results>({ kind: 'idle' });

  const settled = useDebounced(term);
  const listId = useId();

  // Held in a ref rather than the dependency list: a caller that builds its
  // search function inline would otherwise re-run the effect on every render
  // and cancel its own request forever.
  const ask = useRef(search);
  ask.current = search;

  useEffect(() => {
    const wanted = settled.trim();

    if (wanted === '') {
      setResults({ kind: 'idle' });
      return;
    }

    const stop = new AbortController();
    setResults({ kind: 'searching' });

    void (async () => {
      try {
        const found = await ask.current(wanted, stop.signal);
        setResults({ kind: 'ready', options: found });
      } catch (failure) {
        // An aborted request is this component cancelling itself, not a
        // failure to report. Rendering it would flash an error every keystroke.
        if (stop.signal.aborted) {
          return;
        }

        setResults({
          kind: 'failed',
          message: failure instanceof Error ? failure.message : String(failure),
        });
      }
    })();

    return () => stop.abort();
  }, [settled]);

  const take = useCallback(
    (option: PickerOption) => {
      onChoose(option);
      setTerm('');
      setResults({ kind: 'idle' });
    },
    [onChoose],
  );

  if (chosen !== null) {
    return (
      <div className="picker">
        <span className="picker-label">{label}</span>
        <p className="picker-chosen">
          <span className="strong">{chosen.label}</span>
          {chosen.hint === undefined ? null : (
            <>
              {' '}
              <span className="muted">{chosen.hint}</span>
            </>
          )}
        </p>
        <button type="button" disabled={disabled} onClick={() => onChoose(null)}>
          {t('picker.change')}
        </button>
      </div>
    );
  }

  // Before anybody types, the shortlist is the list. After, it is out of the way
  // — somebody who has started typing is looking for something not on it.
  const offered = results.kind === 'idle' ? shortlist : [];

  return (
    <div className="picker">
      <label htmlFor={id}>{label}</label>
      <input
        id={id}
        type="text"
        value={term}
        disabled={disabled}
        autoComplete="off"
        aria-controls={listId}
        placeholder={t('picker.typeToSearch')}
        onChange={(event) => setTerm(event.target.value)}
      />

      {results.kind === 'searching' ? <p className="note">{t('picker.searching')}</p> : null}

      {results.kind === 'failed' ? (
        <p className="error" role="alert">
          {t('picker.searchFailed')}
        </p>
      ) : null}

      {results.kind === 'ready' && results.options.length === 0 ? (
        <p className="note">{t('picker.noMatches')}</p>
      ) : null}

      {offered.length === 0 && results.kind === 'idle' ? (
        <p className="note">{t('picker.startTyping')}</p>
      ) : null}

      <ul id={listId} className="picker-results">
        {(results.kind === 'ready' ? results.options : offered).map((option) => (
          <li key={option.id}>
            <button type="button" disabled={disabled} onClick={() => take(option)}>
              <span className="strong">{option.label}</span>
              {option.hint === undefined ? null : (
                <>
                  {' '}
                  <span className="muted">{option.hint}</span>
                </>
              )}
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
