// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   appearance.test — the theme actually reaches the page.
//
// Usage:
//   npm test
//
// Coding Instructions:
//   These assert on `<html>` rather than on a React tree, because that is
//   where the setting genuinely lives — the whole design is that CSS does the
//   work off an attribute. A test that only checked component state would
//   pass with the stylesheet disconnected.
//
//   Direction is NOT tested here any more. It is a property of the chosen
//   language rather than a setting of its own, and it is proven in
//   i18n/i18n.test.tsx alongside the language that decides it.

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { AppearanceProvider, useAppearance } from './appearance';
import { AppearanceControls } from '../app/AppearanceControls';
import { I18nProvider } from './i18n';

function renderControls() {
  return render(
    <I18nProvider>
      <AppearanceProvider>
        <AppearanceControls />
      </AppearanceProvider>
    </I18nProvider>,
  );
}

/** What CSS keys off. Always a real theme, never "system". */
function themeOnPage(): string | null {
  return document.documentElement.getAttribute('data-theme');
}

describe('light and dark', () => {
  it('resolves "follow this machine" to a real theme rather than leaving it undecided', () => {
    renderControls();

    // One dark palette in the stylesheet depends on this: the attribute is
    // always light or dark, so there is no second copy inside a media query.
    expect(themeOnPage()).toBe('light');
  });

  it('switches the page the moment it is asked', async () => {
    renderControls();

    await userEvent.click(screen.getByRole('button', { name: 'Dark' }));

    expect(themeOnPage()).toBe('dark');
  });

  it('says which one is current, to a screen reader as well as to the eye', async () => {
    renderControls();

    await userEvent.click(screen.getByRole('button', { name: 'Dark' }));

    expect(screen.getByRole('button', { name: 'Dark' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: 'Auto' })).toHaveAttribute('aria-pressed', 'false');
  });

  it('remembers the choice for next time', async () => {
    renderControls();
    await userEvent.click(screen.getByRole('button', { name: 'Dark' }));

    expect(localStorage.getItem('dfoss.theme')).toBe('dark');

    // A fresh mount, as a reload would be.
    cleanupPage();
    renderControls();

    expect(themeOnPage()).toBe('dark');
  });
});

describe('using it outside the provider', () => {
  it('fails loudly rather than silently doing nothing', () => {
    function Orphan() {
      useAppearance();
      return null;
    }

    // A component that quietly ignored the theme would be found by a person, in
    // production, wondering why one screen stayed light.
    expect(() => render(<Orphan />)).toThrow(/AppearanceProvider/);
  });
});

/**
 * Wipes what the last render left on `<html>`, so a remount is genuinely a fresh
 * page rather than one still wearing the previous test's attribute.
 */
function cleanupPage(): void {
  document.documentElement.removeAttribute('data-theme');
}
