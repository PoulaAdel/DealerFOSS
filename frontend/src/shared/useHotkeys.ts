// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   useHotkeys — single keys and two-key sequences, for people who would rather
//   not reach for the mouse.
//
// Usage:
//   UseHotkeys({ '?': showHelp, 'g d': goToDashboard })
//
// Coding Instructions:
//   Two rules make the difference between a shortcut and a trap.
//
//   A key pressed while somebody is typing is a character, not a command.
//   Anything with a modifier belongs to the browser or the operating system,
//   and stealing it breaks copy, paste, and find.
//
//   Sequences ("g" then "d") expire after a second. Without that, a stray "g"
//   leaves the next unrelated keypress armed, and the page jumps somewhere
//   nobody asked for.

import { useEffect, useRef } from 'react';

/** How long the first key of a sequence stays armed. */
const SEQUENCE_MS = 1000;

function isTyping(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) {
    return false;
  }

  return (
    target.isContentEditable ||
    ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName)
  );
}

/**
 * Binds a map of keys to handlers for as long as the component is mounted.
 *
 * A key is either one character (`'/'`) or two separated by a space (`'g d'`).
 * Matching is case-sensitive on the printed character, so `?` needs Shift and
 * says so.
 */
export function useHotkeys(bindings: Record<string, () => void>, enabled = true): void {
  // Held in a ref so a handler that closes over fresh state does not have to
  // re-bind the listener on every render — and so a sequence in progress
  // survives one.
  const current = useRef(bindings);
  current.current = bindings;

  const pending = useRef<{ key: string; at: number } | null>(null);

  useEffect(() => {
    if (!enabled) {
      return;
    }

    function onKeyDown(event: KeyboardEvent) {
      if (event.ctrlKey || event.metaKey || event.altKey || isTyping(event.target)) {
        return;
      }

      const armed = pending.current;
      pending.current = null;

      if (armed !== null && Date.now() - armed.at < SEQUENCE_MS) {
        const handler = current.current[`${armed.key} ${event.key}`];
        if (handler !== undefined) {
          event.preventDefault();
          handler();
          return;
        }
      }

      const handler = current.current[event.key];
      if (handler !== undefined) {
        event.preventDefault();
        handler();
        return;
      }

      // Not a shortcut on its own, but the first half of one. Arm it.
      const startsSequence = Object.keys(current.current).some((binding) =>
        binding.startsWith(`${event.key} `),
      );

      if (startsSequence) {
        pending.current = { key: event.key, at: Date.now() };
      }
    }

    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [enabled]);
}
