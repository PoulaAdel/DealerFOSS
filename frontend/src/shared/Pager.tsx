// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Pager — the footer under every list: which rows these are, how many there
//   are altogether, and the two buttons that reach the rest.
//
//   It was written inside the enquiry screen first, because that was the only
//   list that could be paged. Every other list said "showing the first 50" and
//   offered nothing, so there was nothing to share. Now that they all page, this
//   is the one place the wording, the arithmetic and the disabled states live.
//
// Usage:
//   <Pager page={page} onPage={setOffset} />
//
//   `page` is the Page<T> the server returned; `onPage` is handed the new
//   offset. The caller owns the offset and resets it when a filter changes.
//
// Coding Instructions:
//   THE RANGE IS COMPUTED FROM THE ROWS ACTUALLY RETURNED, not from offset +
//   limit. The last page is short, and "showing 51-100 of 73" is the kind of
//   wrong that makes somebody distrust every other number on the screen.
//
//   RESET THE OFFSET WHEN A FILTER CHANGES. That is the caller's job and it is
//   easy to forget: page 3 of one filter is not page 3 of another, and landing
//   on an empty page reads as "there is nothing here" rather than as "you are
//   past the end".
//
//   The count is in the table's caption as well as here, because a screen reader
//   reaching a table announces the caption and should not have to hunt for the
//   size of what it has just entered.

import { useI18n } from './i18n';
import type { Page } from './contracts';

export function Pager<T>({ page, onPage }: { page: Page<T>; onPage: (offset: number) => void }) {
  const { t } = useI18n();

  const { first, last, hasMore } = pageRange(page);

  // Nothing to page through, so nothing to show. The empty state says its own
  // piece and a disabled pair of buttons under it would only be furniture.
  if (page.total === 0) {
    return null;
  }

  return (
    <div className="paging">
      <p className="note note--footer">
        {t('paging.showingRange', { first, last, total: page.total })}
      </p>

      <div className="actions">
        <button
          type="button"
          disabled={page.offset === 0}
          onClick={() => onPage(Math.max(0, page.offset - page.limit))}
        >
          {t('paging.previous')}
        </button>
        <button
          type="button"
          disabled={!hasMore}
          onClick={() => onPage(page.offset + page.limit)}
        >
          {t('paging.next')}
        </button>
      </div>
    </div>
  );
}

/**
 * Which rows these are, one-based and inclusive, and whether there are more.
 *
 * Exported because the table caption needs the same numbers, and computing them
 * twice is how the caption and the footer end up disagreeing.
 */
export function pageRange<T>(page: Page<T>) {
  const first = page.total === 0 ? 0 : page.offset + 1;
  const last = page.offset + page.rows.length;

  return { first, last, hasMore: last < page.total };
}

/**
 * The caption a screen reader hears on entering the table.
 *
 * A hook rather than a component: it goes inside <caption>, which may not
 * contain arbitrary markup in every renderer, and the string is wanted by
 * itself anyway.
 */
export function usePageCaption() {
  const { t } = useI18n();

  return <T,>(page: Page<T>) => {
    const { first, last } = pageRange(page);
    return t('paging.showingRange', { first, last, total: page.total });
  };
}
