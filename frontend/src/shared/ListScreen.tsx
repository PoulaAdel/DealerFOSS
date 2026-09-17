// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ListScreen — the loading/denied/failed/empty/ready union every paged list
//   screen was hand-rolling separately (UX audit M4, prototyped on /customers
//   and /workshop).
//
// Usage:
//   <ListScreen
//     load={load}
//     onRetry={() => void find(search, offset)}
//     onPage={setOffset}
//     loadingMessage={t('customers.looking')}
//     deniedMessage={t('customers.denied')}
//     emptyMessage={t('customers.noMatches')}
//     columns={<>
//       <th scope="col">{t('customers.colName')}</th>
//       ...
//     </>}
//     row={(customer) => (
//       <tr key={customer.id}>...</tr>
//     )}
//   />
//
// Coding Instructions:
//   THIS IS THE SHAPE ADR-020 ALREADY SPECIFIES, not a new one. /customers,
//   /deals, /leads, /stock and /parts already agreed on it independently —
//   `.state` for loading/denied/failed/empty, aria-live on loading, role="alert"
//   on denied and failed. /workshop had drifted from it (a bare `<p>` for
//   loading, `.note`/`.error` instead of `.state`) before this component
//   existed; bringing it here is a consistency fix, not a behaviour change
//   anyone asked to keep.
//
//   `columns` and `row` stay as render props rather than a column-definition
//   array. A generic column model would have to cover `.num`, `dir="ltr"`,
//   chips and inline-edit triggers — every table here uses at least one — and
//   the abstraction that could still express all of that would not be
//   simpler than JSX already is.
//
//   ONLY SCREENS WITH A Page<T> BELONG HERE. A single-record load (the trial
//   balance, a statement, the labour report) is a different shape — no rows,
//   no pager — and forcing it through this component would need as many
//   escape hatches as it saved lines.

import type { ReactNode } from 'react';
import { Pager, usePageCaption } from './Pager';
import type { Page } from './contracts';
import { useI18n } from './i18n';

export type ListLoad<T> =
  | { kind: 'loading' }
  | { kind: 'ready'; page: Page<T> }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

export function ListScreen<T>({
  load,
  onRetry,
  onPage,
  loadingMessage,
  deniedMessage,
  emptyMessage,
  tableClassName,
  columns,
  row,
}: {
  load: ListLoad<T>;
  onRetry: () => void;
  onPage: (offset: number) => void;
  loadingMessage: string;
  deniedMessage: string;
  emptyMessage: string;
  tableClassName?: string;
  columns: ReactNode;
  row: (item: T) => ReactNode;
}) {
  const { t } = useI18n();
  const caption = usePageCaption();

  if (load.kind === 'loading') {
    return (
      <p className="state" aria-live="polite">
        {loadingMessage}
      </p>
    );
  }

  if (load.kind === 'denied') {
    return (
      <p className="state" role="alert">
        {deniedMessage}
      </p>
    );
  }

  if (load.kind === 'failed') {
    return (
      <div className="state" role="alert">
        <p>{load.message}</p>
        <button type="button" onClick={onRetry}>
          {t('common.retry')}
        </button>
      </div>
    );
  }

  if (load.page.total === 0) {
    return <p className="state">{emptyMessage}</p>;
  }

  return (
    <div className="scroll">
      <table className={tableClassName}>
        <caption className="visually-hidden">{caption(load.page)}</caption>
        <thead>
          <tr>{columns}</tr>
        </thead>
        <tbody>{load.page.rows.map(row)}</tbody>
      </table>

      <Pager page={load.page} onPage={onPage} />
    </div>
  );
}
