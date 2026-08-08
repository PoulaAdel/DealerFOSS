// i18n — the application's words, in the language the reader chose.
//
// Use:  const { t, format } = useI18n()
//       t('nav.stock')                        → "Stock"
//       t('stock.count', { count: 4 })        → "4 cars in stock"
//       t('deal.owner', { name: person })     → "Sold by Ada"
//       format.money(1250, 'USD')             → "$1,250.00" / "1 250,00 $US"
//
// Edit: three rules hold this together.
//
//       ONE. The language decides the direction. `dir` on <html> is derived
//       from the chosen language and cannot be set independently, because
//       "Arabic, left to right" is not a configuration anybody wants — it is a
//       bug with a switch in front of it.
//
//       TWO. English is the schema. `MessageKey` is `keyof typeof en`, and the
//       other four catalogues are typed as `Catalogue`, so a key added to
//       English and forgotten in Russian fails `npm run typecheck` rather than
//       shipping an English sentence into a Russian screen. Warnings are errors
//       here, and this is the cheapest place to catch a missing translation.
//
//       THREE. Plurals go through Intl.PluralRules, never through `n === 1`.
//       Russian has four categories and Arabic six; picking between two of them
//       is wrong for most numbers in both.
//
//       The chosen language is applied to <html> BEFORE React mounts, for the
//       same reason the theme is: a page that paints left-to-right and then
//       flips is worse than one that starts correct. Not an inline script in
//       index.html, because the app is served under a CSP that forbids one.

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { isPluralForms, type Message, type Substitutions } from './catalogue';
import { en } from './locales/en';
import { fr } from './locales/fr';
import { de } from './locales/de';
import { ru } from './locales/ru';
import { ar } from './locales/ar';
import {
  detectLanguage,
  isLanguageCode,
  languageFor,
  type Language,
  type LanguageCode,
} from './languages';

export type { Direction, Language, LanguageCode } from './languages';
export { LANGUAGES } from './languages';

/** Every message key the application may ask for. English defines the set. */
export type MessageKey = keyof typeof en;

/** A complete translation. Missing a key is a compile error, not a runtime gap. */
export type Catalogue = Readonly<Record<MessageKey, Message>>;

const CATALOGUES: Readonly<Record<LanguageCode, Catalogue>> = { en, fr, de, ru, ar };

const LANGUAGE_KEY = 'dfoss.language';

function storedLanguage(): LanguageCode {
  const saved = localStorage.getItem(LANGUAGE_KEY);
  if (isLanguageCode(saved)) {
    return saved;
  }

  // Nobody has chosen yet, so follow the browser. navigator.languages is
  // missing in some test environments; navigator.language is the fallback.
  const preferences =
    navigator.languages ?? (navigator.language === undefined ? [] : [navigator.language]);

  return detectLanguage(preferences);
}

/**
 * Puts the language and its direction on `<html>`.
 *
 * `lang` is not cosmetic: it is what tells a screen reader which voice to use,
 * what a browser's translate offer keys off, and how the text is hyphenated.
 * `dir` is what makes the whole layout mirror — the stylesheet is written in
 * logical properties (inline-start, not left), so this one attribute flips
 * every margin, every border, and every table column at once.
 */
function applyLanguage(language: Language): void {
  const root = document.documentElement;
  root.setAttribute('lang', language.code);
  root.setAttribute('dir', language.direction);
}

// Before React mounts. See the note at the top of the file.
applyLanguage(languageFor(storedLanguage()));

/** Formatters bound to the active locale. */
export interface Formats {
  /** Money, in the currency the record is denominated in. */
  money: (amount: number, currency: string) => string;
  /** A plain number, grouped the way this locale groups. */
  number: (value: number, options?: Intl.NumberFormatOptions) => string;
  /** A date with no time — "12 August 2026". */
  date: (value: string | number | Date) => string;
  /** A date and time, for an audit trail. */
  dateTime: (value: string | number | Date) => string;
  /** "August 2026", for a period heading. */
  monthAndYear: (year: number, month: number) => string;
  /** Joins a list the way the language does: "a, b and c" / "a، b وc". */
  list: (items: readonly string[]) => string;
}

interface I18n {
  language: Language;
  setLanguage: (code: LanguageCode) => void;
  /** Translates a key, substituting `{placeholders}` and choosing a plural form. */
  t: (key: MessageKey, values?: Substitutions) => string;
  format: Formats;
}

const I18nContext = createContext<I18n | null>(null);

/**
 * Fills `{placeholders}`.
 *
 * A placeholder with no matching value is left as written rather than replaced
 * with "undefined" — a visible `{name}` in a screenshot gets reported and
 * fixed, whereas "Sold by undefined" reads like a data problem and gets chased
 * in the wrong place.
 */
function interpolate(template: string, values: Substitutions | undefined, locale: string): string {
  if (values === undefined) {
    return template;
  }

  return template.replace(/\{(\w+)\}/g, (whole, name: string) => {
    const value = values[name];
    if (value === undefined) {
      return whole;
    }

    // Numbers substituted into a sentence are grouped for the locale, so a
    // Russian screen reads "1 234 машины" and not "1234 машины".
    return typeof value === 'number' ? new Intl.NumberFormat(locale).format(value) : value;
  });
}

function selectForm(message: Message, values: Substitutions | undefined, locale: string): string {
  if (!isPluralForms(message)) {
    return message;
  }

  const count = values?.count;
  if (typeof count !== 'number') {
    // A plural entry asked for without a count. `other` is the honest choice —
    // it is the form that reads acceptably on its own in all five languages.
    return message.other;
  }

  // `select` returns a category this language actually uses, so the entry
  // should carry it. `other` covers the case where a catalogue omitted one —
  // reading slightly oddly beats rendering nothing.
  return message[new Intl.PluralRules(locale).select(count)] ?? message.other;
}

export function I18nProvider({ children }: { children: ReactNode }) {
  const [code, setCode] = useState<LanguageCode>(storedLanguage);

  const language = useMemo(() => languageFor(code), [code]);

  // Runs on mount as well as on change, so a test or a second tab that cleared
  // storage gets the attributes put back rather than left as the module-load
  // call set them.
  useEffect(() => applyLanguage(language), [language]);

  const setLanguage = useCallback((next: LanguageCode) => {
    localStorage.setItem(LANGUAGE_KEY, next);
    setCode(next);
  }, []);

  const t = useCallback(
    (key: MessageKey, values?: Substitutions) => {
      const catalogue = CATALOGUES[language.code];

      // Falling back to English is deliberate. A key that somehow has no
      // translation should show the English sentence, which a reader can at
      // least act on, rather than the raw key — which looks like a crash.
      const message: Message = catalogue[key] ?? en[key];
      if (message === undefined) {
        return key;
      }

      return interpolate(selectForm(message, values, language.locale), values, language.locale);
    },
    [language],
  );

  const format = useMemo<Formats>(() => {
    const { locale } = language;

    const dateFormat = new Intl.DateTimeFormat(locale, {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
    });

    const dateTimeFormat = new Intl.DateTimeFormat(locale, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });

    const monthFormat = new Intl.DateTimeFormat(locale, { year: 'numeric', month: 'long' });
    const listFormat = new Intl.ListFormat(locale, { style: 'long', type: 'conjunction' });

    // One formatter per currency, built on demand. Constructing an
    // Intl.NumberFormat is not free and a stock list asks for hundreds.
    const moneyFormats = new Map<string, Intl.NumberFormat>();

    return {
      money: (amount, currency) => {
        let formatter = moneyFormats.get(currency);
        if (formatter === undefined) {
          formatter = new Intl.NumberFormat(locale, { style: 'currency', currency });
          moneyFormats.set(currency, formatter);
        }

        return formatter.format(amount);
      },
      number: (value, options) => new Intl.NumberFormat(locale, options).format(value),
      date: (value) => dateFormat.format(new Date(value)),
      dateTime: (value) => dateTimeFormat.format(new Date(value)),
      // Built in UTC so a period labelled August is not shown as July to
      // somebody west of Greenwich — the month is a fact about the books, not
      // an instant in the reader's day.
      monthAndYear: (year, month) => monthFormat.format(new Date(Date.UTC(year, month - 1, 1))),
      list: (items) => listFormat.format(items),
    };
  }, [language]);

  const value = useMemo<I18n>(
    () => ({ language, setLanguage, t, format }),
    [language, setLanguage, t, format],
  );

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n(): I18n {
  const i18n = useContext(I18nContext);
  if (i18n === null) {
    throw new Error('useI18n must be used inside an I18nProvider.');
  }

  return i18n;
}
