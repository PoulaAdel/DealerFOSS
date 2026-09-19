// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ListScreen.test — page navigation costs a fixed number of Tab stops, even
//   when a full customer page holds 100 individually reachable records.
//
// Usage:
//   npm test
//
// Coding Instructions:
//   Count actual user-event Tabs, not buttons in the DOM. Disabled directions
//   are not stops, and a pager can exist while sitting behind every row — the
//   September 2026 audit found exactly that. Keep walking through the records
//   too: removing them from the tab order would make the first assertion pass
//   by breaking the other half of keyboard access. Browser walks still prove
//   the real focus ring and layout; jsdom cannot do either.

import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '../test/render';
import { ListScreen } from './ListScreen';

describe('paging without walking every record', () => {
  it('skips both unavailable directions when the list fits on one page', async () => {
    const user = userEvent.setup();
    render(
      <ListScreen
        load={{ kind: 'ready', page: { rows: ['Only customer'], offset: 0, limit: 100, total: 1 } }}
        onPage={vi.fn()}
        onRetry={vi.fn()}
        loadingMessage="Loading"
        deniedMessage="Denied"
        emptyMessage="Empty"
        columns={<th scope="col">Customer</th>}
        row={(name) => <tr key={name}><td><button type="button">{name}</button></td></tr>}
      />,
    );
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled();
    await user.tab();
    expect(screen.getByRole('button', { name: 'Only customer' })).toHaveFocus();
  });

  it.each([
    { offset: 0, direction: 'Next', stops: 1, destination: 100 },
    { offset: 100, direction: 'Next', stops: 2, destination: 200 },
    { offset: 200, direction: 'Previous', stops: 1, destination: 100 },
  ])('reaches $direction in $stops stops at offset $offset, keeping all 100 rows reachable', async ({
    offset, direction, stops, destination,
  }) => {
    const user = userEvent.setup();
    const onPage = vi.fn();
    const rows = Array.from({ length: 100 }, (_, index) => `Customer ${offset + index + 1}`);
    render(
      <ListScreen
        load={{ kind: 'ready', page: { rows, offset, limit: 100, total: 300 } }}
        onPage={onPage}
        onRetry={vi.fn()}
        loadingMessage="Loading"
        deniedMessage="Denied"
        emptyMessage="Empty"
        columns={<th scope="col">Customer</th>}
        row={(name) => <tr key={name}><td><button type="button">{name}</button></td></tr>}
      />,
    );

    const target = screen.getByRole('button', { name: direction });
    let count = 0;
    // Bound the walk so a missing or unreachable control fails instead of hangs.
    while (document.activeElement !== target && count < 105) {
      await user.tab();
      count += 1;
    }
    expect(count).toBe(stops);
    expect(target).toHaveFocus();
    await user.keyboard('{Enter}');
    expect(onPage).toHaveBeenCalledExactlyOnceWith(destination);

    const records = within(screen.getByRole('table')).getAllByRole('button');
    expect(records).toHaveLength(100);
    for (const record of records) {
      await user.tab();
      expect(record).toHaveFocus();
    }
    await user.tab();
    expect(document.body).toHaveFocus();
  });
});
