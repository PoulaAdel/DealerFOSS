// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   useRecordRoute — the open record lives in the address bar, so it can be
//   sent to somebody.
//
// Usage:
//   const job = useRecordRoute<RepairOrderDetail>({
//     area: '/workshop',
//     load: (id, signal) => api<RepairOrderDetail>(`/repair-orders/${id}`, { signal }),
//   });
//
//   <Route path="/workshop/:id?" element={<WorkshopPage />} />
//
// Coding Instructions:
//   READ ADR-020 BEFORE CHANGING THIS. That decision says "a detail is a band,
//   not a route", and this hook is not a reversal of it — it is the narrow
//   amendment recorded there on 2026-09-16. The detail is STILL a band, still
//   rendered below the list with the list still on screen. The only thing that
//   moved into the URL is WHICH record the band is showing.
//
//   That distinction is the whole design, and it is why `:id?` is an OPTIONAL
//   segment on the SAME route rather than a second route. Two routes would be
//   two different matches, React Router would unmount and remount the page
//   between them, and the filter, the page number and the scroll position
//   would all be lost on every open and every close — which is exactly the
//   zero-jump rule ADR-020 exists to protect. One route with an optional
//   segment keeps the component mounted throughout.
//
//   `load` is held in a ref and is NOT an effect dependency. Callers build it
//   inline, so it is a new function on every render; depending on it would
//   refetch the record forever. The effect depends on the id alone, because
//   the id is the only thing that should cause a fetch.
//
//   Failure is deliberately ONE state and one message. The server already
//   answers "unknown" and "not yours" identically for scoped records so a
//   caller cannot probe for other rooftops' work, and a screen that told the
//   two apart would hand back the distinction the server just refused to give.
//   Customers answer 404 for a genuinely missing record because a customer is
//   organization-wide — whoever may read one may read all of them, so there is
//   nothing to leak — but the screen still says the same sentence, so a later
//   server change cannot quietly turn into a probe.

import { useCallback, useEffect, useRef, useState } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router';

export type RecordRouteState<T> =
  | { kind: 'closed' }
  | { kind: 'opening' }
  | { kind: 'open'; record: T }
  | { kind: 'unreachable' };

export interface RecordRoute<T> {
  /** What the detail band should render. */
  state: RecordRouteState<T>;

  /**
   * The id in the address bar, or null. Given separately from `state` because
   * the list wants to mark the row as selected the instant somebody clicks it,
   * not when the record finishes loading.
   */
  openId: string | null;

  /** Open a record: pushes its URL, so the back button closes it. */
  open: (id: string) => void;

  /**
   * Open a record already in hand — one just created, or one picked out of a
   * list response. Navigates and shows it without asking the server for what
   * the caller already has.
   */
  openWith: (id: string, record: T) => void;

  /** Close the record and return to the list. */
  close: () => void;

  /**
   * The open record changed in place: a status moved, a line was added. Shows
   * the new version WITHOUT navigating, because nothing about which record is
   * open has changed and a history entry per edit would make the back button
   * walk through an editing session.
   */
  refresh: (record: T) => void;
}

export function useRecordRoute<T>({
  area,
  load,
  carryQuery = false,
}: {
  area: string;
  load: (id: string, signal: AbortSignal) => Promise<T>;

  /**
   * Whether the list's query string travels with the record.
   *
   * Off by default, and that default is a safety one. Stock wants it on: its
   * `?stock=` is a real filter, so dropping it would silently widen the list
   * somebody comes back to. The deal desk must have it OFF: its `?leadId=` is a
   * one-shot handoff from a won enquiry, and carrying it into a shareable
   * address means the link a salesperson sends a manager starts a second deal
   * on the same enquiry when it is opened.
   *
   * A filter is worth carrying; an instruction is not. When in doubt, a URL
   * that does less when pasted is the right one.
   */
  carryQuery?: boolean;
}): RecordRoute<T> {
  const { id = null } = useParams();
  const navigate = useNavigate();
  const location = useLocation();

  const [state, setState] = useState<RecordRouteState<T>>(
    id === null ? { kind: 'closed' } : { kind: 'opening' },
  );

  // See the header: a dependency here would refetch forever.
  const loadRef = useRef(load);
  loadRef.current = load;

  // A record handed to `openWith`, waiting for the navigation it triggered to
  // arrive. Without it, creating a car would show it, then immediately fetch
  // the same car again and flicker through "Opening…" on the way back to what
  // was already on screen.
  const seeded = useRef<{ id: string; record: T } | null>(null);

  // Whether THIS page put the record in the history, as opposed to somebody
  // arriving on the record's own URL. It decides what closing does: see
  // `close` below.
  const pushed = useRef(false);

  useEffect(() => {
    if (id === null) {
      setState({ kind: 'closed' });
      return;
    }

    if (seeded.current?.id === id) {
      setState({ kind: 'open', record: seeded.current.record });
      seeded.current = null;
      return;
    }

    const stop = new AbortController();
    setState({ kind: 'opening' });

    void (async () => {
      try {
        const record = await loadRef.current(id, stop.signal);
        if (!stop.signal.aborted) {
          setState({ kind: 'open', record });
        }
      } catch {
        // An abort lands here too. Leaving the state alone is right in that
        // case: the next effect has already set 'opening' for the new id, and
        // overwriting it with 'unreachable' would flash a failure for a record
        // nobody is waiting on any more.
        if (!stop.signal.aborted) {
          setState({ kind: 'unreachable' });
        }
      }
    })();

    return () => stop.abort();
  }, [id]);

  const search = carryQuery ? location.search : '';

  const open = useCallback(
    (recordId: string) => {
      pushed.current = true;
      navigate({ pathname: `${area}/${recordId}`, search });
    },
    [area, search, navigate],
  );

  const openWith = useCallback(
    (recordId: string, record: T) => {
      seeded.current = { id: recordId, record };
      pushed.current = true;
      navigate({ pathname: `${area}/${recordId}`, search });
    },
    [area, search, navigate],
  );

  const close = useCallback(() => {
    if (pushed.current) {
      // Going back rather than forward to the list, so opening and closing
      // three records does not leave six entries to walk out through.
      pushed.current = false;
      navigate(-1);
      return;
    }

    // Arrived here on the record's own URL — a link from a colleague. There is
    // no entry of ours to go back to, and `navigate(-1)` would leave the
    // application entirely. Replacing keeps the browser's back button pointing
    // at wherever they were before this tab showed our screen.
    navigate({ pathname: area, search }, { replace: true });
  }, [area, search, navigate]);

  const refresh = useCallback((record: T) => {
    setState({ kind: 'open', record });
  }, []);

  return { state, openId: id, open, openWith, close, refresh };
}
