// AppearanceControls — the theme, and the language the application speaks.
//
// Use:  rendered in the shell bar and in the administration console's.
// Edit: theme is a segmented control rather than a toggle. A toggle can only
//       say "dark: on or off", and the third state — follow the machine — is
//       the one most people actually want. It also cannot say which of the two
//       it is currently showing you, which is precisely what somebody looking
//       at the control wants to know.
//
//       Language is a <select> rather than five buttons: five segments do not
//       fit the bar, and a select is the control every operating system already
//       renders as a language picker. Each option names itself IN ITSELF —
//       "العربية", not "Arabic" — because somebody who needs to switch to
//       Arabic is, by definition, the person least able to find the word
//       "Arabic" written in English.
//
//       There is no direction control any more. Direction follows the language
//       (see shared/i18n), because "Arabic, left to right" is not a preference
//       anybody holds.

import { useAppearance, type ThemeChoice } from '../shared/appearance';
import { LANGUAGES, useI18n, type LanguageCode } from '../shared/i18n';
import type { MessageKey } from '../shared/i18n';

const themes: { value: ThemeChoice; label: MessageKey; hint: MessageKey }[] = [
  { value: 'system', label: 'appearance.auto', hint: 'appearance.autoHint' },
  { value: 'light', label: 'appearance.light', hint: 'appearance.lightHint' },
  { value: 'dark', label: 'appearance.dark', hint: 'appearance.darkHint' },
];

export function AppearanceControls() {
  const { theme, setTheme } = useAppearance();
  const { language, setLanguage, t } = useI18n();

  return (
    <div className="appearance">
      <div className="switcher" role="group" aria-label={t('shell.appearance')}>
        {themes.map((option) => (
          <button
            key={option.value}
            type="button"
            // The pressed state is what a screen reader reads out as "selected",
            // and it is also what the stylesheet keys the highlight off — so the
            // two can never disagree.
            aria-pressed={theme === option.value}
            title={t(option.hint)}
            onClick={() => setTheme(option.value)}
          >
            {t(option.label)}
          </button>
        ))}
      </div>

      <select
        className="language"
        aria-label={t('shell.language')}
        value={language.code}
        onChange={(event) => setLanguage(event.target.value as LanguageCode)}
      >
        {LANGUAGES.map((option) => (
          // `lang` on the option so a screen reader pronounces "Français" with a
          // French voice rather than reading it as mangled English.
          <option key={option.code} value={option.code} lang={option.code}>
            {option.nativeName}
          </option>
        ))}
      </select>
    </div>
  );
}
