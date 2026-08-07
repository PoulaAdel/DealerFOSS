// appearance — light or dark, and which way the page runs.
//
// Use:  const { theme, setTheme, direction, setDirection } = useAppearance()
// Edit: both settings are applied to <html> as attributes, not to a React tree.
//       CSS then does the work, which is what makes the switch instant — there is
//       no re-render, no reflow of a component tree, and no flash of the wrong
//       theme on the next screen.
//
//       The stored choice is applied at module load, BEFORE React mounts, so the
//       first paint is already right. Doing it in an effect would paint light and
//       then correct itself, which is exactly the eye strain the dark theme is
//       there to avoid.
//
//       "Follow the machine" is resolved here to a real light or dark, so the
//       stylesheet carries one dark palette rather than two copies of it.
//
//       Not an inline script in index.html, deliberately: the application is
//       served under a Content-Security-Policy that forbids inline script, and a
//       thing that works in development and is blocked in production is worse
//       than the flash.

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

export type Direction = 'ltr' | 'rtl';

const THEME_KEY = 'dfoss.theme';
const DIRECTION_KEY = 'dfoss.direction';

const THEMES: ThemeChoice[] = ['system', 'light', 'dark'];
const DIRECTIONS: Direction[] = ['ltr', 'rtl'];

function storedTheme(): ThemeChoice {
  const saved = localStorage.getItem(THEME_KEY);
  return THEMES.includes(saved as ThemeChoice) ? (saved as ThemeChoice) : 'system';
}

function storedDirection(): Direction {
  const saved = localStorage.getItem(DIRECTION_KEY);
  return DIRECTIONS.includes(saved as Direction) ? (saved as Direction) : 'ltr';
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

function applyDirection(direction: Direction): void {
  document.documentElement.setAttribute('dir', direction);
}

// Before React mounts. See the note at the top of the file.
applyTheme(storedTheme());
applyDirection(storedDirection());

interface Appearance {
  theme: ThemeChoice;
  setTheme: (theme: ThemeChoice) => void;
  direction: Direction;
  setDirection: (direction: Direction) => void;
}

const AppearanceContext = createContext<Appearance | null>(null);

export function AppearanceProvider({ children }: { children: ReactNode }) {
  const [theme, setThemeState] = useState<ThemeChoice>(storedTheme);
  const [direction, setDirectionState] = useState<Direction>(storedDirection);

  // Runs on mount as well as on change, which is what makes the provider
  // authoritative: a test or a second tab that cleared storage gets the
  // attributes put back rather than left at whatever the module-load call set.
  useEffect(() => applyTheme(theme), [theme]);
  useEffect(() => applyDirection(direction), [direction]);

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

  const setDirection = useCallback((next: Direction) => {
    localStorage.setItem(DIRECTION_KEY, next);
    setDirectionState(next);
  }, []);

  const value = useMemo<Appearance>(
    () => ({ theme, setTheme, direction, setDirection }),
    [theme, setTheme, direction, setDirection],
  );

  return <AppearanceContext.Provider value={value}>{children}</AppearanceContext.Provider>;
}

export function useAppearance(): Appearance {
  const appearance = useContext(AppearanceContext);
  if (appearance === null) {
    throw new Error('useAppearance must be used inside an AppearanceProvider.');
  }

  return appearance;
}
