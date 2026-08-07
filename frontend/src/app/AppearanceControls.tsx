// AppearanceControls — light or dark, and which way the page runs.
//
// Use:  rendered in the shell bar and in the administration console's.
// Edit: both are segmented buttons rather than a toggle. A toggle can only say
//       "dark: on or off", and the third state — follow the machine — is the one
//       most people actually want. It also cannot say which of the two it is
//       currently showing you, which is precisely what somebody looking at the
//       control wants to know.

import { useAppearance, type Direction, type ThemeChoice } from '../shared/appearance';

const themes: { value: ThemeChoice; label: string; hint: string }[] = [
  { value: 'system', label: 'Auto', hint: 'Follow this machine' },
  { value: 'light', label: 'Light', hint: 'Always light' },
  { value: 'dark', label: 'Dark', hint: 'Always dark' },
];

const directions: { value: Direction; label: string; hint: string }[] = [
  { value: 'ltr', label: 'LTR', hint: 'Left to right' },
  { value: 'rtl', label: 'RTL', hint: 'Right to left' },
];

export function AppearanceControls() {
  const { theme, setTheme, direction, setDirection } = useAppearance();

  return (
    <div className="appearance">
      <div className="switcher" role="group" aria-label="Appearance">
        {themes.map((option) => (
          <button
            key={option.value}
            type="button"
            // The pressed state is what a screen reader reads out as "selected",
            // and it is also what the stylesheet keys the highlight off — so the
            // two can never disagree.
            aria-pressed={theme === option.value}
            title={option.hint}
            onClick={() => setTheme(option.value)}
          >
            {option.label}
          </button>
        ))}
      </div>

      <div className="switcher" role="group" aria-label="Text direction">
        {directions.map((option) => (
          <button
            key={option.value}
            type="button"
            aria-pressed={direction === option.value}
            title={option.hint}
            onClick={() => setDirection(option.value)}
          >
            {option.label}
          </button>
        ))}
      </div>
    </div>
  );
}
