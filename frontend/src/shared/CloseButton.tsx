// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   CloseButton — the X that dismisses a record's detail panel.
//
// Usage:
//   <header className="page__head">
//     <h2>...</h2>
//     <CloseButton onClick={onClose} />
//   </header>
//
// Coding Instructions:
//   "Close" beside a heading is unambiguous wherever it appears in this
//   application — every detail panel puts it in the same corner, the same
//   way every dialog does. That repetition is exactly what makes the icon
//   safe here (skill: "close → X"), which a one-off icon button elsewhere
//   in the app would not automatically earn. `common.close` still supplies
//   the accessible name — the icon is decoration, not the label.

import { useI18n } from './i18n';

export function CloseButton({ onClick }: { onClick: () => void }) {
  const { t } = useI18n();

  return (
    <button type="button" className="icon-button" onClick={onClick} aria-label={t('common.close')}>
      <svg viewBox="0 0 16 16" width="14" height="14" aria-hidden="true">
        <path d="M3 3l10 10M13 3L3 13" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
      </svg>
    </button>
  );
}
