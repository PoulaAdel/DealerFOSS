// appearance.test — the theme and the direction actually reach the page.
//
// Use:  npm test
// Edit: these assert on `<html>` rather than on a React tree, because that is
//       where the setting genuinely lives — the whole design is that CSS does the
//       work off two attributes. A test that only checked component state would
//       pass with the stylesheet disconnected.

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { AppearanceProvider, useAppearance } from './appearance';
import { AppearanceControls } from '../app/AppearanceControls';

function renderControls() {
  return render(
    <AppearanceProvider>
      <AppearanceControls />
    </AppearanceProvider>
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

describe('which way the page runs', () => {
  it('starts left to right', () => {
    renderControls();

    expect(document.documentElement.getAttribute('dir')).toBe('ltr');
  });

  it('mirrors the whole page on request', async () => {
    renderControls();

    await userEvent.click(screen.getByRole('button', { name: 'RTL' }));

    // Every layout rule is written with logical properties, so this one
    // attribute is the entire mechanism.
    expect(document.documentElement.getAttribute('dir')).toBe('rtl');
    expect(localStorage.getItem('dfoss.direction')).toBe('rtl');
  });

  it('remembers that too', async () => {
    renderControls();
    await userEvent.click(screen.getByRole('button', { name: 'RTL' }));

    cleanupPage();
    renderControls();

    expect(document.documentElement.getAttribute('dir')).toBe('rtl');
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
 * page rather than one still wearing the previous test's attributes.
 */
function cleanupPage(): void {
  document.documentElement.removeAttribute('data-theme');
  document.documentElement.removeAttribute('dir');
}
