// useDebounced — a value that lags behind, so a keystroke does not become a
// request.
//
// Use:  const typed = useState('');
//       const settled = useDebounced(typed);
//
//       useEffect(() => {
//         const stop = new AbortController();
//         void find(settled, stop.signal);
//         return () => stop.abort();
//       }, [settled]);
//
// Edit: this is deliberately the SMALLER half of instant search. It only delays
//       a value; the cancelling is done by the effect that reads it, aborting on
//       cleanup. Those two together give the property that matters, which is not
//       "fewer requests" but:
//
//         THE LIST NEVER SHOWS RESULTS FOR A QUERY THE BOX NO LONGER HOLDS.
//
//       That is the whole reason this is not a two-line change. Without
//       cancellation, typing "focus" fires five searches whose answers arrive in
//       whatever order the network chooses, and the one that lands last wins —
//       so a slow reply for "f" can overwrite the right answer for "focus", and
//       the screen sits there confidently showing the wrong list. An aborted
//       request never resolves, so it can never win that race.
//
//       Debouncing alone would NOT fix it: it makes the race rarer, which is
//       worse than leaving it obvious.
//
//       250ms is chosen to sit under the ~300ms at which a pause starts to feel
//       like waiting, while still collapsing an ordinary typing burst into one
//       request.

import { useEffect, useState } from 'react';

/** How long a value must stop changing before it settles. */
const DefaultDelay = 250;

export function useDebounced<T>(value: T, delay: number = DefaultDelay): T {
  const [settled, setSettled] = useState(value);

  useEffect(() => {
    // The first value is already settled, so an empty box does not wait a
    // quarter of a second to show the list it could have shown immediately.
    if (settled === value) {
      return;
    }

    const timer = setTimeout(() => setSettled(value), delay);
    return () => clearTimeout(timer);
  }, [value, delay, settled]);

  return settled;
}
