// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Keyboard containment and return for the two existing dialog surfaces.
//
// Usage:
//   Call useDialogFocus with the dialog ref and optional safe initial target;
//   delegate Tab to trapDialogTab from the dialog's key handler.
//
// Coding Instructions:
//   Never choose the destructive action as initial focus. Restoring only a
//   connected opener avoids focusing a record that was removed by the action.
//   Escape and callbacks remain with the caller: focus is presentation, not
//   permission to confirm, cancel or change a workflow.

import { useEffect, type KeyboardEvent, type RefObject } from 'react';

export function useDialogFocus(
  dialog: RefObject<HTMLElement | null>,
  initial?: RefObject<HTMLElement | null>,
) {
  useEffect(() => {
    const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    (initial?.current ?? dialog.current)?.focus();
    return () => {
      if (opener?.isConnected) opener.focus({ preventScroll: true });
    };
  }, [dialog, initial]);
}

export function trapDialogTab(event: KeyboardEvent<HTMLElement>) {
  if (event.key !== 'Tab') return;

  const dialog = event.currentTarget;
  const targets = Array.from(dialog.querySelectorAll<HTMLElement>(
    'button, [href], input, select, textarea, [tabindex]',
  )).filter((element) => element.tabIndex >= 0 && !element.matches(':disabled, [hidden]'));
  const first = targets[0];
  const last = targets[targets.length - 1];

  if (!first || !last) {
    event.preventDefault();
    dialog.focus();
  } else if (document.activeElement === dialog) {
    event.preventDefault();
    (event.shiftKey ? last : first).focus();
  } else if (event.shiftKey && document.activeElement === first) {
    event.preventDefault();
    last.focus();
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault();
    first.focus();
  }
}
