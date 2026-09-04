// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   appearance — light or dark.
//
// Usage:
//   const { theme, setTheme } = useAppearance()
//
// Coding Instructions:
//   The setting is applied to <html> as an attribute, not to a React tree.
//   CSS then does the work, which is what makes the switch instant — there is
//   no re-render, no reflow of a component tree, and no flash of the wrong
//   theme on the next screen.
//
//   Which way the page RUNS is not here. Direction is a property of the
//   chosen language and lives in shared/i18n — Arabic runs right to left and
//   there is no such thing as wanting it not to. It used to be a separate
//   toggle beside the theme, which let somebody select "Arabic, left to
//   right": a broken layout with a switch in front of it.
//
//   The stored choice is applied at module load, BEFORE React mounts, so the
//   first paint is already right. Doing it in an effect would paint light and
//   then correct itself, which is exactly the eye strain the dark theme is
//   there to avoid.
//
//   "Follow the machine" is resolved here to a real light or dark, so the
//   stylesheet carries one dark palette rather than two copies of it.
//
//   Not an inline script in index.html, deliberately: the application is
//   served under a Content-Security-Policy that forbids inline script, and a
//   thing that works in development and is blocked in production is worse
//   than the flash.

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';

/** `system` follows the operating system and keeps following it as it changes. */
export type ThemeChoice = 'system' | 'light' | 'dark';

const THEME_KEY = 'dfoss.theme';

const THEMES: ThemeChoice[] = ['system', 'light', 'dark'];

function storedTheme(): ThemeChoice {
  const saved = localStorage.getItem(THEME_KEY);
  return THEMES.includes(saved as ThemeChoice) ? (saved as ThemeChoice) : 'system';
}

const DARK_QUERY = '(prefers-color-scheme: dark)';

function prefersDark(): boolean {
  // matchMedia is missing in some test environments. Light is the safer guess
  // when nobody has said — it is what a machine with no preference shows.
  return window.matchMedia?.(DARK_QUERY).matches ?? false;
}

/**
 * Puts the resolved theme — always `light` or `dark`, never `system` — on
 * `<html>`.
 *
 * Resolving here rather than leaving `system` to a `prefers-color-scheme` rule
 * means the stylesheet needs exactly ONE dark palette, keyed off one attribute.
 * The alternative needs the same sixteen colours written twice, once for the
 * attribute and once for the media query, and the second copy is the one that
 * drifts.
 */
function applyTheme(theme: ThemeChoice): void {
  const resolved = theme === 'system' ? (prefersDark() ? 'dark' : 'light') : theme;
  document.documentElement.setAttribute('data-theme', resolved);
}

// Before React mounts. See the note at the top of the file.
applyTheme(storedTheme());

interface Appearance {
  theme: ThemeChoice;
  setTheme: (theme: ThemeChoice) => void;
}

const AppearanceContext = createContext<Appearance | null>(null);

export function AppearanceProvider({ children }: { children: ReactNode }) {
  const [theme, setThemeState] = useState<ThemeChoice>(storedTheme);

  // Runs on mount as well as on change, which is what makes the provider
  // authoritative: a test or a second tab that cleared storage gets the
  // attribute put back rather than left at whatever the module-load call set.
  useEffect(() => applyTheme(theme), [theme]);

  // Following the machine has to mean following it as it changes — a laptop that
  // goes dark at dusk should take the app with it, without a reload.
  useEffect(() => {
    if (theme !== 'system') {
      return;
    }

    const query = window.matchMedia?.(DARK_QUERY);
    if (query === undefined) {
      return;
    }

    const follow = () => applyTheme('system');
    query.addEventListener('change', follow);
    return () => query.removeEventListener('change', follow);
  }, [theme]);

  const setTheme = useCallback((next: ThemeChoice) => {
    localStorage.setItem(THEME_KEY, next);
    setThemeState(next);
  }, []);

  const value = useMemo<Appearance>(() => ({ theme, setTheme }), [theme, setTheme]);

  return <AppearanceContext.Provider value={value}>{children}</AppearanceContext.Provider>;
}

export function useAppearance(): Appearance {
  const appearance = useContext(AppearanceContext);
  if (appearance === null) {
    throw new Error('useAppearance must be used inside an AppearanceProvider.');
  }

  return appearance;
}
