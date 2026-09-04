// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   languages — the six the application speaks, and what each one needs.
//
// Usage:
//   LANGUAGES for a picker; languageFor(code) to resolve one safely.
//
// Coding Instructions:
//   Direction is a property OF THE LANGUAGE, not a separate preference.
//   Arabic runs right to left; the other five run left to right. Nobody
//   reads Arabic in a left-to-right layout, and offering the combination as
//   a choice invites somebody to pick the broken one. The old manual
//   LTR/RTL toggle is gone for exactly that reason.
//
//   `locale` is the BCP 47 tag handed to Intl for numbers and dates. It is
//   kept separate from `code` because a catalogue key and a formatting
//   locale are different things: `ar` selects Arabic words, and the region
//   in the tag decides whether those words come with Arabic-Indic digits.
//   We use `ar` (not `ar-EG`) so digits stay Western — a dealership's price
//   list mixing ٣ and 3 is harder to read, not more authentic.

/** The six languages the UI ships in. */
export type LanguageCode = 'en' | 'es' | 'fr' | 'de' | 'ru' | 'ar';

export type Direction = 'ltr' | 'rtl';

export interface Language {
  readonly code: LanguageCode;
  /** How the language names itself. A picker must never say "Arabic" in English. */
  readonly nativeName: string;
  /** For an English-speaking maintainer reading the code or a screenshot. */
  readonly englishName: string;
  readonly direction: Direction;
  /** The BCP 47 tag for Intl.NumberFormat, Intl.DateTimeFormat, Intl.PluralRules. */
  readonly locale: string;
}

export const LANGUAGES: readonly Language[] = [
  { code: 'en', nativeName: 'English', englishName: 'English', direction: 'ltr', locale: 'en' },
  { code: 'es', nativeName: 'Español', englishName: 'Spanish', direction: 'ltr', locale: 'es' },
  { code: 'fr', nativeName: 'Français', englishName: 'French', direction: 'ltr', locale: 'fr' },
  { code: 'de', nativeName: 'Deutsch', englishName: 'German', direction: 'ltr', locale: 'de' },
  { code: 'ru', nativeName: 'Русский', englishName: 'Russian', direction: 'ltr', locale: 'ru' },
  { code: 'ar', nativeName: 'العربية', englishName: 'Arabic', direction: 'rtl', locale: 'ar' },
];

export const DEFAULT_LANGUAGE: LanguageCode = 'en';

const BY_CODE = new Map(LANGUAGES.map((language) => [language.code, language]));

export function isLanguageCode(value: unknown): value is LanguageCode {
  return typeof value === 'string' && BY_CODE.has(value as LanguageCode);
}

/** Resolves a code to its language, falling back to English rather than throwing. */
export function languageFor(code: LanguageCode): Language {
  return BY_CODE.get(code) ?? BY_CODE.get(DEFAULT_LANGUAGE)!;
}

/**
 * The best match for what the browser asks for.
 *
 * Matches on the primary subtag, so `fr-CA` and `de-AT` find French and German
 * rather than falling through to English. A dealership in Montreal running a
 * Canadian-French browser should not have to set this by hand.
 */
export function detectLanguage(preferences: readonly string[]): LanguageCode {
  for (const preference of preferences) {
    const primary = preference.toLowerCase().split('-')[0];
    if (isLanguageCode(primary)) {
      return primary;
    }
  }

  return DEFAULT_LANGUAGE;
}
