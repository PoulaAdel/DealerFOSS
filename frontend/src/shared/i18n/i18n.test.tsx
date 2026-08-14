// i18n.test — the words change, the page turns round, and the plurals are right.
//
// Use:  npm test
// Edit: the catalogue-completeness check at the bottom is the one that earns its
//       keep. TypeScript already fails the build on a MISSING key, but it cannot
//       see a key that was copied across and left in English — which is the way
//       a translation actually rots. That test reads the files.

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { I18nProvider, useI18n, LANGUAGES } from './index';
import { AppearanceProvider } from '../appearance';
import { AppearanceControls } from '../../app/AppearanceControls';
import { en } from './locales/en';
import { fr } from './locales/fr';
import { de } from './locales/de';
import { ru } from './locales/ru';
import { ar } from './locales/ar';

function renderControls() {
  return render(
    <I18nProvider>
      <AppearanceProvider>
        <AppearanceControls />
      </AppearanceProvider>
    </I18nProvider>,
  );
}

/** The picker, by its accessible name in whichever language is showing. */
function picker(): HTMLSelectElement {
  return screen.getByRole('combobox') as HTMLSelectElement;
}

function pageLanguage(): string | null {
  return document.documentElement.getAttribute('lang');
}

function pageDirection(): string | null {
  return document.documentElement.getAttribute('dir');
}

describe('choosing a language', () => {
  it('starts in English and says so on the page itself', () => {
    renderControls();

    // `lang` is what a screen reader picks a voice from. It is not decoration.
    expect(pageLanguage()).toBe('en');
    expect(pageDirection()).toBe('ltr');
  });

  it('changes the words on screen', async () => {
    renderControls();

    expect(screen.getByRole('button', { name: 'Dark' })).toBeVisible();

    await userEvent.selectOptions(picker(), 'de');

    expect(screen.getByRole('button', { name: 'Dunkel' })).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Dark' })).toBeNull();
  });

  it('names each language in itself, not in English', () => {
    renderControls();

    // Somebody who needs Arabic is the person least able to find the word
    // "Arabic" written in English.
    expect(screen.getByRole('option', { name: 'العربية' })).toBeVisible();
    expect(screen.getByRole('option', { name: 'Русский' })).toBeVisible();
    expect(screen.queryByRole('option', { name: 'Arabic' })).toBeNull();
  });

  it('remembers the choice for next time', async () => {
    renderControls();
    await userEvent.selectOptions(picker(), 'ru');

    expect(localStorage.getItem('dfoss.language')).toBe('ru');
  });
});

describe('direction follows the language', () => {
  it('turns the page round for Arabic', async () => {
    renderControls();
    await userEvent.selectOptions(picker(), 'ar');

    // Every layout rule is written with logical properties, so this one
    // attribute is the entire mechanism that mirrors the application.
    expect(pageDirection()).toBe('rtl');
    expect(pageLanguage()).toBe('ar');
  });

  it('turns it back for a left-to-right language', async () => {
    renderControls();

    await userEvent.selectOptions(picker(), 'ar');
    expect(pageDirection()).toBe('rtl');

    await userEvent.selectOptions(picker(), 'fr');
    expect(pageDirection()).toBe('ltr');
  });

  it('offers no way to ask for Arabic left to right', () => {
    renderControls();

    // The old build had a separate LTR/RTL switcher beside the theme, which
    // made "Arabic, left to right" a selectable combination. There must be
    // exactly one direction control, and it must be the language.
    expect(screen.queryByRole('button', { name: 'RTL' })).toBeNull();
    expect(screen.queryByRole('group', { name: /direction/i })).toBeNull();
  });
});

describe('plurals', () => {
  function Counted({ count }: { count: number }) {
    const { t } = useI18n();
    return <p>{t('stock.count', { count })}</p>;
  }

  function renderCount(language: string, count: number) {
    localStorage.setItem('dfoss.language', language);
    return render(
      <I18nProvider>
        <Counted count={count} />
      </I18nProvider>,
    );
  }

  it('uses the two English forms', () => {
    const { unmount } = renderCount('en', 1);
    expect(screen.getByText('1 car in stock')).toBeVisible();
    unmount();

    renderCount('en', 4);
    expect(screen.getByText('4 cars in stock')).toBeVisible();
  });

  it('uses all four Russian forms, which "n === 1" cannot', () => {
    // 1 машина / 3 машины / 11 машин / 22 машины. A two-form catalogue gets
    // three of these four wrong, and they are ordinary numbers on a stock list.
    for (const [count, expected] of [
      [1, '1 машина в наличии'],
      [3, '3 машины в наличии'],
      [11, '11 машин в наличии'],
      [22, '22 машины в наличии'],
    ] as const) {
      const { unmount } = renderCount('ru', count);
      expect(screen.getByText(expected)).toBeVisible();
      unmount();
    }
  });

  it('uses the Arabic dual and few forms', () => {
    // Arabic distinguishes two from three-to-ten, which no European language
    // does. Getting this wrong is immediately visible to a reader.
    for (const [count, expected] of [
      [1, 'سيارة واحدة في المخزون'],
      [2, 'سيارتان في المخزون'],
      [3, '3 سيارات في المخزون'],
      [11, '11 سيارة في المخزون'],
    ] as const) {
      const { unmount } = renderCount('ar', count);
      expect(screen.getByText(expected)).toBeVisible();
      unmount();
    }
  });
});

describe('the catalogues', () => {
  const catalogues = { fr, de, ru, ar };
  const keys = Object.keys(en) as (keyof typeof en)[];

  it('all carry every key English defines', () => {
    // TypeScript enforces this too. Asserting it as well means the guarantee
    // survives somebody widening the Catalogue type to make an error go away.
    for (const [name, catalogue] of Object.entries(catalogues)) {
      const missing = keys.filter((key) => catalogue[key] === undefined);
      expect(missing, `${name} is missing keys`).toEqual([]);
    }
  });

  it('are not quietly still in English', () => {
    // A key whose translation is character-for-character the English is almost
    // always one somebody pasted across and never came back to — and TypeScript
    // cannot see it, because the key IS there and it IS a string.
    //
    // The exceptions are real, so they are listed with the reason rather than
    // the check being weakened. Each is a word that a French, German, Russian
    // or Arabic dealership genuinely writes the same way: an acronym, a brand,
    // or a loanword that has entered the trade.
    const sameWordEverywhere = new Map<string, string>([
      ['app.name', 'a product name'],
      ['signIn.code', 'the same word in French'],
      ['setPassword.code', 'the same word in French'],
      ['appearance.auto', 'the same abbreviation in French and German'],
      ['enum.financeProductKind.Gap', 'an acronym, unchanged in the trade'],
      ['enum.financeProductKind.Protection', 'the same word in French'],
      ['nav.stock', 'French uses "stock" for vehicle inventory'],
      ['stock.title', 'French uses "stock" for vehicle inventory'],
      ['stock.colVin', 'an acronym; German uses FIN, Russian and French keep VIN'],
      ['enum.leadSource.Website', 'the same word in German'],
      ['stock.status', 'the same word in German'],
      ['stock.colStatus', 'the same word in German'],
      ['trialBalance.total', 'the same word in French'],
      ['customers.colName', 'the same word in German'],
      ['deals.stockLine', 'French uses "stock" for vehicle inventory'],
      ['staff.colName', 'the same word in German'],
      ['staff.name', 'the same word in German'],
      ['parts.colNote', 'the same word in French'],
      ['dash.financeShort', 'an industry abbreviation, unchanged in the trade'],
      ['dash.total', 'the same word in French'],
      ['dash.colStatus', 'the same word in German'],
      ['workshop.miles', 'the same word in French'],
      ['workshop.colDetail', 'the same word in German'],
      ['admin.badge', 'the same word in French'],
      ['admin.colName', 'the same word in German'],
      ['admin.colStatus', 'the same word in German'],
      ['admin.colSchema', 'the same word in German'],
      ['stock.moved', 'two placeholders and an arrow; only Arabic turns the arrow round'],
    ]);

    for (const [name, catalogue] of Object.entries(catalogues)) {
      const untranslated = keys.filter((key) => {
        if (sameWordEverywhere.has(key)) {
          return false;
        }

        const english = en[key];
        return typeof english === 'string' && catalogue[key] === english;
      });

      expect(untranslated, `${name} has untranslated keys`).toEqual([]);
    }
  });

  it('give every language a direction and a name in itself', () => {
    expect(LANGUAGES.map((l) => l.code)).toEqual(['en', 'es', 'fr', 'de', 'ru', 'ar']);
    expect(LANGUAGES.filter((l) => l.direction === 'rtl').map((l) => l.code)).toEqual(['ar']);

    for (const language of LANGUAGES) {
      expect(language.nativeName.length, `${language.code} has no native name`).toBeGreaterThan(0);
    }
  });
});

describe('using it outside the provider', () => {
  it('fails loudly rather than silently showing English', () => {
    function Orphan() {
      useI18n();
      return null;
    }

    expect(() => render(<Orphan />)).toThrow(/I18nProvider/);
  });
});
