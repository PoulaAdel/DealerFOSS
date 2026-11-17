// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   PageTitle.test — the tab names the screen, not just the product.
//
// Usage:
//   npm test
//
// Coding Instructions:
//   WCAG 2.4.2. Each case is a title somebody would otherwise have read as
//   "DealerFOSS" on every tab. The heading is the source, so these tests mount
//   real headings rather than naming routes.

import { describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router';
import { render, screen, waitFor } from '../test/render';
import { PageTitle } from './PageTitle';
import { Wordmark } from './Mark';

function mount(path: string, page: React.ReactNode) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <PageTitle />
      {page}
    </MemoryRouter>,
  );
}

describe('the page title', () => {
  it('names the screen from its own heading', async () => {
    mount('/customers', <h1>Customers</h1>);

    await waitFor(() => expect(document.title).toBe('Customers — DealerFOSS'));
  });

  it('uses the navigation label on the dashboard, whose heading is only the month', async () => {
    mount('/dashboard', <h1>October 2026</h1>);

    await waitFor(() => expect(document.title).toBe('This month — DealerFOSS'));
  });

  it('follows a heading that arrives after the data does', async () => {
    const { rerender } = mount('/parts', <p>Loading…</p>);
    expect(document.title).toBe('DealerFOSS');

    rerender(
      <MemoryRouter initialEntries={['/parts']}>
        <PageTitle />
        <h1>Parts</h1>
      </MemoryRouter>,
    );

    await waitFor(() => expect(document.title).toBe('Parts — DealerFOSS'));
  });

  it('reads a signed-out screen by its action, and the wordmark only once', async () => {
    mount(
      '/sign-in',
      <main>
        <h1>
          <Wordmark tagline />
        </h1>
        <button type="submit">Sign in</button>
      </main>,
    );

    await waitFor(() => expect(document.title).toBe('Sign in — DealerFOSS'));
    expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument();
  });
});
