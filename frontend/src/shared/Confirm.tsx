// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Confirm — a real dialog for the acts window.confirm was standing in for.
//
// Usage:
//   const [confirming, setConfirming] = useState(false);
//   ...
//   {confirming ? (
//     <Confirm
//       title={t('admin.suspendTitle', { name: tenant.name })}
//       body={t('admin.suspendConfirm', { name: tenant.name })}
//       confirmLabel={t('admin.suspend')}
//       typeToConfirm={tenant.name}
//       onConfirm={() => { setConfirming(false); void doSuspend(); }}
//       onCancel={() => setConfirming(false)}
//     />
//   ) : null}
//
// Coding Instructions:
//   This exists because window.confirm cannot be styled, cannot be trapped,
//   does not mirror for Arabic, and its OK/Cancel come from the operating
//   system rather than the application (UX audit finding C2). It replaces the
//   three sites that used it: forgetting a passkey, suspending a dealership,
//   and closing an accounting period.
//
//   THE FRICTION IS THE FEATURE, so nothing here is optimised for the fewest
//   clicks. `typeToConfirm`, when given, disables the confirm button until
//   the reader has typed that exact text back — see the UX audit's decision
//   to demand it for the two truly consequential sites. The passkey site
//   uses the plain two-button form: forgetting one is a personal act with a
//   narrow blast radius, not an outage for other people.
//
//   FOCUS IS TRAPPED, NOT JUST MOVED. Tab and Shift+Tab cycle inside the
//   dialog rather than escaping to the page behind it, which is the part
//   window.confirm gave for free and a hand-rolled one usually forgets.
//   Escape and the backdrop both cancel — never confirm — because leaving is
//   always the safe direction.
//
//   INITIAL FOCUS LANDS ON CANCEL, not on the confirm button, in the
//   two-button form: a stray Enter from whatever the reader was doing before
//   the dialog opened must not complete a destructive act. The typed-text
//   form focuses the input instead, since typing IS the safe first action
//   there.

import { useEffect, useId, useRef, useState, type KeyboardEvent } from 'react';
import { useI18n } from './i18n';

export interface ConfirmProps {
  title: string;
  body: string;
  confirmLabel: string;
  cancelLabel?: string;
  /** When given, the confirm button stays disabled until this exact text is typed back. */
  typeToConfirm?: string;
  busy?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

const FOCUSABLE = 'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])';

export function Confirm({
  title,
  body,
  confirmLabel,
  cancelLabel,
  typeToConfirm,
  busy = false,
  onConfirm,
  onCancel,
}: ConfirmProps) {
  const { t } = useI18n();
  const dialog = useRef<HTMLDivElement>(null);
  const typedInput = useRef<HTMLInputElement>(null);
  const cancelButton = useRef<HTMLButtonElement>(null);
  const [typed, setTyped] = useState('');
  const titleId = useId();
  const bodyId = useId();

  // Typing IS the safe first action when a typed answer is required; the
  // cancel button is the safe default otherwise — see the file header.
  useEffect(() => {
    (typeToConfirm === undefined ? cancelButton.current : typedInput.current)?.focus();
  }, [typeToConfirm]);

  const canConfirm = !busy && (typeToConfirm === undefined || typed === typeToConfirm);

  function trapTab(event: KeyboardEvent<HTMLDivElement>) {
    if (event.key === 'Escape') {
      onCancel();
      return;
    }

    if (event.key !== 'Tab' || dialog.current === null) {
      return;
    }

    const focusable = Array.from(dialog.current.querySelectorAll<HTMLElement>(FOCUSABLE)).filter(
      (el) => !el.hasAttribute('disabled'),
    );
    if (focusable.length === 0) {
      return;
    }

    const first = focusable[0]!;
    const last = focusable[focusable.length - 1]!;

    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }

  return (
    <div className="confirm-backdrop" onClick={onCancel}>
      <div
        className="sheet sheet--confirm"
        role="alertdialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={bodyId}
        tabIndex={-1}
        ref={dialog}
        onKeyDown={trapTab}
        // Stops a click inside the dialog from bubbling to the backdrop and
        // being read as "cancel".
        onClick={(event) => event.stopPropagation()}
      >
        <h2 id={titleId}>{title}</h2>
        <p id={bodyId} className="note">
          {body}
        </p>

        {typeToConfirm === undefined ? null : (
          <div className="field">
            <label htmlFor="confirm-typed">
              {t('confirm.typeToConfirm', { text: typeToConfirm })}
            </label>
            <input
              id="confirm-typed"
              ref={typedInput}
              value={typed}
              dir="ltr"
              autoComplete="off"
              onChange={(event) => setTyped(event.target.value)}
            />
          </div>
        )}

        <div className="actions">
          <button
            type="button"
            className="danger"
            disabled={!canConfirm}
            onClick={onConfirm}
          >
            {confirmLabel}
          </button>
          <button type="button" ref={cancelButton} onClick={onCancel}>
            {cancelLabel ?? t('common.cancel')}
          </button>
        </div>
      </div>
    </div>
  );
}
