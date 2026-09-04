// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Shortcuts — the keyboard map, and the panel that admits it exists.
//
// Usage:
//   Rendered by the shell; opened with "?" or the button beside it.
//
// Coding Instructions:
//   An undiscoverable shortcut is a shortcut nobody uses. The list below is
//   the only description of the bindings, and it is generated from the same
//   table the shell binds — so a shortcut cannot be added without appearing
//   here, and cannot be removed while still being advertised.
//
//   `says` is a message KEY, not a sentence. Holding the English here would
//   have put one screen's worth of untranslated text inside a table that
//   looks like configuration, which is exactly where it would have been
//   missed — the key makes the omission a compile error instead.
//
//   The key names are NOT translated. "g d" is what is physically printed
//   on the keyboard, and localising it to a mnemonic in another language
//   would describe a key the reader does not have.

import { useEffect, useRef } from 'react';
import { useI18n, type MessageKey } from '../shared/i18n';

export interface Shortcut {
  /** As useHotkeys binds it: "g d", "?", "[". */
  keys: string;
  says: MessageKey;
}

/**
 * Every binding, in the order they are worth learning. The shell turns the `keys`
 * into handlers; this panel turns them into instructions.
 */
export const shortcuts: Shortcut[] = [
  { keys: 'g d', says: 'shortcuts.goDashboard' },
  { keys: 'g s', says: 'shortcuts.goStock' },
  { keys: 'g c', says: 'shortcuts.goCustomers' },
  { keys: 'g e', says: 'shortcuts.goLeads' },
  { keys: 'g l', says: 'shortcuts.goDeals' },
  { keys: 'g w', says: 'shortcuts.goWorkshop' },
  { keys: 'g p', says: 'shortcuts.goParts' },
  { keys: 'g b', says: 'shortcuts.goBooks' },
  { keys: '[', says: 'shortcuts.monthBefore' },
  { keys: ']', says: 'shortcuts.monthAfter' },
  { keys: 't', says: 'shortcuts.thisMonth' },
  { keys: '?', says: 'shortcuts.showList' },
];

/**
 * How a key is printed. "g d" is two keys pressed one after the other.
 *
 * `dir="ltr"` on the row: a key sequence is read left to right even on an
 * Arabic page, the same way a phone number or a VIN is. Without it the two
 * halves of "g d" swap and the panel teaches the wrong sequence.
 */
function Keys({ keys, space }: { keys: string; space: string }) {
  return (
    <span dir="ltr">
      {keys.split(' ').map((key, index) => (
        <kbd key={key}>
          {index > 0 ? ' ' : ''}
          {key === ' ' ? space : key}
        </kbd>
      ))}
    </span>
  );
}

export function ShortcutsPanel({ onClose }: { onClose: () => void }) {
  const dialog = useRef<HTMLDivElement>(null);
  const { t } = useI18n();

  // Focus moves into the panel, so Escape and Tab do what somebody who never
  // touched the mouse expects. Without this the panel opens behind the keyboard
  // rather than in front of it.
  useEffect(() => dialog.current?.focus(), []);

  return (
    <div
      className="sheet"
      role="dialog"
      aria-modal="true"
      aria-label={t('shortcuts.title')}
      tabIndex={-1}
      ref={dialog}
      onKeyDown={(event) => {
        if (event.key === 'Escape') {
          onClose();
        }
      }}
    >
      <div className="sheet__head">
        <h2>{t('shortcuts.title')}</h2>
        <button type="button" onClick={onClose}>
          {t('common.close')}
        </button>
      </div>

      <dl className="keys">
        {shortcuts.map((shortcut) => (
          <div key={shortcut.keys}>
            <dt>
              <Keys keys={shortcut.keys} space={t('shortcuts.space')} />
            </dt>
            <dd>{t(shortcut.says)}</dd>
          </div>
        ))}
      </dl>

      <p className="note">{t('shortcuts.note')}</p>
    </div>
  );
}
