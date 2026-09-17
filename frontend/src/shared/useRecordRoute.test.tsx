// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   useRecordRoute.test — the properties that make a record's URL worth having.
//
// Usage:
//   npm test.
//
// Coding Instructions:
//   The test that matters most here is "keeps the page mounted". Everything
//   else this hook does would still work with two routes instead of one
//   optional segment; the zero-jump rule ADR-020 protects would not. It is
//   proven by counting mounts, because that is the thing that breaks — a
//   remount is invisible in the rendered output and takes the filter with it.

import { render, screen, waitFor } from '../test/render';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router';
import { useRef, useState } from 'react';
import { useRecordRoute } from './useRecordRoute';

interface Thing {
  id: string;
  name: string;
}

let mounts = 0;

function ThingsPage({ load }: { load: (id: string) => Promise<Thing> }) {
  const counted = useRef(false);
  if (!counted.current) {
    counted.current = true;
    mounts += 1;
  }

  // Stands in for the filter, the page number and the scroll position: state
  // the page holds that a remount would silently discard.
  const [filter, setFilter] = useState('');

  const thing = useRecordRoute<Thing>({ area: '/things', load: (id) => load(id) });
  const where = useLocation();

  return (
    <div>
      <p>at {where.pathname}</p>
      <p>filter is {filter === '' ? 'empty' : filter}</p>
      <button type="button" onClick={() => setFilter('available')}>
        Filter
      </button>
      <button type="button" onClick={() => thing.open('t1')}>
        Open one
      </button>
      <button type="button" onClick={() => thing.open('t2')}>
        Open two
      </button>
      <button type="button" onClick={() => thing.openWith('t9', { id: 't9', name: 'Brand new' })}>
        Create
      </button>
      <button
        type="button"
        onClick={() => thing.refresh({ id: thing.openId ?? '', name: 'Changed in place' })}
      >
        Edit
      </button>

      <p>selected row: {thing.openId ?? 'none'}</p>

      {thing.state.kind === 'closed' ? <p>no record open</p> : null}
      {thing.state.kind === 'opening' ? <p>opening</p> : null}
      {thing.state.kind === 'unreachable' ? <p>cannot be opened</p> : null}
      {thing.state.kind === 'open' ? (
        <section>
          <p>showing {thing.state.record.name}</p>
          <button type="button" onClick={thing.close}>
            Close
          </button>
        </section>
      ) : null}
    </div>
  );
}

function renderThings(load: (id: string) => Promise<Thing>, entries: string[] = ['/things']) {
  mounts = 0;

  return render(
    <MemoryRouter initialEntries={entries}>
      <Routes>
        <Route path="/elsewhere" element={<p>somewhere else entirely</p>} />
        <Route path="/things/:id?" element={<ThingsPage load={load} />} />
      </Routes>
    </MemoryRouter>,
  );
}

const found = (id: string) => Promise.resolve({ id, name: `Thing ${id}` });

describe('a record in the address bar', () => {
  it('opens nothing when the URL names nothing', async () => {
    renderThings(found);

    expect(await screen.findByText('no record open')).toBeVisible();
    expect(screen.getByText('selected row: none')).toBeVisible();
  });

  it('opens the record the URL names, without anybody clicking', async () => {
    // The whole point: a link from a colleague lands on the record itself.
    renderThings(found, ['/things/t7']);

    expect(await screen.findByText('showing Thing t7')).toBeVisible();
  });

  it('puts the record in the address bar when a row is opened', async () => {
    renderThings(found);
    await screen.findByText('no record open');

    await userEvent.click(screen.getByRole('button', { name: 'Open one' }));

    expect(await screen.findByText('showing Thing t1')).toBeVisible();
    expect(screen.getByText('at /things/t1')).toBeVisible();
  });

  it('keeps the page mounted across opening and closing', async () => {
    // See the file header. A second route would remount here, and the filter
    // below stands in for everything a remount would throw away.
    renderThings(found);
    await screen.findByText('no record open');

    await userEvent.click(screen.getByRole('button', { name: 'Filter' }));
    await userEvent.click(screen.getByRole('button', { name: 'Open one' }));
    await screen.findByText('showing Thing t1');

    await userEvent.click(screen.getByRole('button', { name: 'Close' }));
    await screen.findByText('no record open');

    expect(screen.getByText('filter is available')).toBeVisible();
    expect(mounts).toBe(1);
  });

  it('marks the row as selected immediately, not when the record arrives', async () => {
    let settle: (thing: Thing) => void = () => {};
    const slow = () =>
      new Promise<Thing>((resolve) => {
        settle = resolve;
      });

    renderThings(slow);
    await screen.findByText('no record open');

    await userEvent.click(screen.getByRole('button', { name: 'Open one' }));

    expect(screen.getByText('selected row: t1')).toBeVisible();
    expect(screen.getByText('opening')).toBeVisible();

    settle({ id: 't1', name: 'Thing t1' });
    expect(await screen.findByText('showing Thing t1')).toBeVisible();
  });

  it('closes by going back, so three records do not leave six entries', async () => {
    renderThings(found);
    await screen.findByText('no record open');

    await userEvent.click(screen.getByRole('button', { name: 'Open one' }));
    await screen.findByText('showing Thing t1');
    await userEvent.click(screen.getByRole('button', { name: 'Close' }));

    expect(await screen.findByText('at /things')).toBeVisible();
    expect(screen.getByText('no record open')).toBeVisible();
  });

  it('does not leave the application when a deep link is closed', async () => {
    // Somebody arrived on the record's own URL. There is no entry of ours
    // behind it, and going back would take them out of the product.
    renderThings(found, ['/elsewhere', '/things/t7']);
    await screen.findByText('showing Thing t7');

    await userEvent.click(screen.getByRole('button', { name: 'Close' }));

    expect(await screen.findByText('no record open')).toBeVisible();
    expect(screen.getByText('at /things')).toBeVisible();
  });

  it('says the record cannot be opened, in one sentence for every reason', async () => {
    renderThings(() => Promise.reject(new Error('403')), ['/things/gone']);

    expect(await screen.findByText('cannot be opened')).toBeVisible();
  });

  it('shows a record handed to it without asking the server again', async () => {
    const load = vi.fn(found);
    renderThings(load);
    await screen.findByText('no record open');

    await userEvent.click(screen.getByRole('button', { name: 'Create' }));

    expect(await screen.findByText('showing Brand new')).toBeVisible();
    expect(screen.getByText('at /things/t9')).toBeVisible();
    expect(load).not.toHaveBeenCalled();
  });

  it('shows an edit in place without adding a history entry', async () => {
    renderThings(found);
    await screen.findByText('no record open');

    await userEvent.click(screen.getByRole('button', { name: 'Open one' }));
    await screen.findByText('showing Thing t1');

    await userEvent.click(screen.getByRole('button', { name: 'Edit' }));
    expect(await screen.findByText('showing Changed in place')).toBeVisible();

    // One Close, and we are back at the list — not at the version before the
    // edit, which is what an extra history entry would give.
    await userEvent.click(screen.getByRole('button', { name: 'Close' }));
    expect(await screen.findByText('at /things')).toBeVisible();
  });

  it('abandons the first record when a second is opened before it arrives', async () => {
    const pending = new Map<string, (thing: Thing) => void>();
    const slow = (id: string) =>
      new Promise<Thing>((resolve) => {
        pending.set(id, resolve);
      });

    renderThings(slow);
    await screen.findByText('no record open');

    await userEvent.click(screen.getByRole('button', { name: 'Open one' }));
    await userEvent.click(screen.getByRole('button', { name: 'Open two' }));

    // The first reply lands last. It must not win, or the address bar says one
    // record and the band shows another.
    pending.get('t2')!({ id: 't2', name: 'Thing t2' });
    pending.get('t1')!({ id: 't1', name: 'Thing t1' });

    expect(await screen.findByText('showing Thing t2')).toBeVisible();
    await waitFor(() => expect(screen.getByText('at /things/t2')).toBeVisible());
    expect(screen.queryByText('showing Thing t1')).not.toBeInTheDocument();
  });
});
