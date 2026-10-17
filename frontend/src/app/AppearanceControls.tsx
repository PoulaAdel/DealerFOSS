// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   AppearanceControls — the theme, and the language the application speaks.
//
// Usage:
//   Rendered in the shell bar and in the administration console's.
//
// Coding Instructions:
//   Theme is a segmented control rather than a toggle. A toggle can only
//   say "dark: on or off", and the third state — follow the machine — is
//   the one most people actually want. It also cannot say which of the two
//   it is currently showing you, which is precisely what somebody looking
//   at the control wants to know.
//
//   EACH SEGMENT IS AN ICON, NOT A WORD, because three words ("Auto",
//   "Light", "Dark") in a bar that already carries the tenant name and a
//   sign-out button read as more chrome than the choice is worth. The
//   meaning still has to reach somebody who cannot see the icon, so the
//   label moves to `aria-label` and `title` rather than disappearing —
//   `aria-pressed` still carries the actual state to a screen reader
//   regardless of what is visible.
//
//   LANGUAGE IS A BUTTON THAT OPENS A LIST, NOT A <select> — and that is a
//   WCAG 3.2.2 fix, not a taste. A closed native <select> fires `change` on
//   every arrow press, so somebody moving through the options with a keyboard
//   had the whole page switch language under them at each step, and could not
//   reach the one they wanted without passing through the others. They are
//   also the person most likely to be doing exactly that: the one who cannot
//   read the language the page is currently in. Here nothing changes until a
//   language is CHOSEN (Enter, Space or a click on its own button); arrows only
//   move focus.
//
//   Each option still names itself IN ITSELF — "العربية", not "Arabic" —
//   because somebody who needs to switch to Arabic is, by definition, the
//   person least able to find the word "Arabic" written in English, and each
//   carries its own `lang` so a screen reader pronounces it with that voice.
//   The globe glyph is decorative only (aria-hidden); the trigger's accessible
//   name is the word for "Language" plus the current language, which contains
//   the text sighted users see (WCAG 2.5.3).
//
//   There is no direction control any more. Direction follows the language
//   (see shared/i18n), because "Arabic, left to right" is not a preference
//   anybody holds.

import { useEffect, useId, useRef, useState, type ReactElement } from 'react';
import { useAppearance, type ThemeChoice } from '../shared/appearance';
import { LANGUAGES, useI18n, type LanguageCode } from '../shared/i18n';
import type { MessageKey } from '../shared/i18n';

function AutoIcon() {
  return (
    <svg viewBox="0 0 16 16" width="15" height="15" aria-hidden="true">
      <rect x="1.5" y="2.5" width="13" height="9" rx="1.5" fill="none" stroke="currentColor" />
      <path d="M5.5 13.5h5M8 11.5v2" stroke="currentColor" strokeLinecap="round" />
    </svg>
  );
}

function SunIcon() {
  return (
    <svg viewBox="0 0 16 16" width="15" height="15" aria-hidden="true">
      <circle cx="8" cy="8" r="3.2" fill="none" stroke="currentColor" />
      <path
        d="M8 1.2v1.6M8 13.2v1.6M1.2 8h1.6M13.2 8h1.6M3.3 3.3l1.1 1.1M11.6 11.6l1.1 1.1M3.3 12.7l1.1-1.1M11.6 4.4l1.1-1.1"
        stroke="currentColor"
        strokeLinecap="round"
      />
    </svg>
  );
}

function MoonIcon() {
  return (
    <svg viewBox="0 0 16 16" width="15" height="15" aria-hidden="true">
      <path
        d="M13.5 9.7A5.8 5.8 0 1 1 6.3 2.5a4.6 4.6 0 0 0 7.2 7.2z"
        fill="none"
        stroke="currentColor"
        strokeLinejoin="round"
      />
    </svg>
  );
}

function GlobeIcon() {
  return (
    <svg viewBox="0 0 16 16" width="14" height="14" aria-hidden="true">
      <circle cx="8" cy="8" r="6.3" fill="none" stroke="currentColor" />
      <path
        d="M1.7 8h12.6M8 1.7c2 2 2 10.6 0 12.6M8 1.7c-2 2-2 10.6 0 12.6"
        fill="none"
        stroke="currentColor"
      />
    </svg>
  );
}

const themes: { value: ThemeChoice; label: MessageKey; hint: MessageKey; icon: () => ReactElement }[] = [
  { value: 'system', label: 'appearance.auto', hint: 'appearance.autoHint', icon: AutoIcon },
  { value: 'light', label: 'appearance.light', hint: 'appearance.lightHint', icon: SunIcon },
  { value: 'dark', label: 'appearance.dark', hint: 'appearance.darkHint', icon: MoonIcon },
];

export function AppearanceControls() {
  const { theme, setTheme } = useAppearance();
  const { t } = useI18n();

  return (
    <div className="appearance">
      <div className="switcher switcher--icons" role="group" aria-label={t('shell.appearance')}>
        {themes.map((option) => {
          const Icon = option.icon;
          return (
            <button
              key={option.value}
              type="button"
              // The pressed state is what a screen reader reads out as "selected",
              // and it is also what the stylesheet keys the highlight off — so the
              // two can never disagree.
              aria-pressed={theme === option.value}
              aria-label={t(option.label)}
              title={t(option.hint)}
              onClick={() => setTheme(option.value)}
            >
              <Icon />
            </button>
          );
        })}
      </div>

      <LanguageMenu />
    </div>
  );
}

function LanguageMenu() {
  const { language, setLanguage, t } = useI18n();
  const [open, setOpen] = useState(false);
  const root = useRef<HTMLSpanElement>(null);
  const trigger = useRef<HTMLButtonElement>(null);
  const menu = useRef<HTMLDivElement>(null);
  const menuId = useId();

  const options = () => Array.from(menu.current?.querySelectorAll<HTMLButtonElement>('button') ?? []);

  // Focus lands on the CURRENT language, so opening and pressing Escape is a
  // no-op and opening and pressing Enter changes nothing.
  useEffect(() => {
    if (!open) return;

    (options().find((button) => button.getAttribute('aria-current') === 'true') ?? options()[0])?.focus();

    function onPointerDown(event: MouseEvent) {
      if (!root.current?.contains(event.target as Node)) setOpen(false);
    }

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setOpen(false);
        trigger.current?.focus();
      }
    }

    document.addEventListener('mousedown', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('mousedown', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [open]);

  return (
    <span
      className="language-picker"
      ref={root}
      onBlur={(event) => {
        // Tab leaving the list closes it without pulling focus backwards.
        if (!event.currentTarget.contains(event.relatedTarget)) setOpen(false);
      }}
    >
      <button
        type="button"
        ref={trigger}
        className="language-picker__trigger"
        aria-label={`${t('shell.language')}: ${language.nativeName}`}
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        onClick={() => setOpen((was) => !was)}
        onKeyDown={(event) => {
          if (event.key === 'ArrowDown' && !open) {
            event.preventDefault();
            setOpen(true);
          }
        }}
      >
        <GlobeIcon />
        {language.nativeName}
      </button>

      {open ? (
        <div
          id={menuId}
          className="language-picker__menu"
          ref={menu}
          onKeyDown={(event) => {
            // Arrows MOVE focus; they never choose. Choosing is Enter, Space or
            // a click, which is the whole point of this control.
            if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') return;
            event.preventDefault();
            const all = options();
            const at = all.indexOf(document.activeElement as HTMLButtonElement);
            const next = event.key === 'ArrowDown' ? at + 1 : at - 1;
            all[(next + all.length) % all.length]?.focus();
          }}
        >
          {LANGUAGES.map((option) => (
            // `lang` so a screen reader pronounces "Français" with a French
            // voice rather than reading it as mangled English.
            <button
              key={option.code}
              type="button"
              lang={option.code}
              aria-current={option.code === language.code ? 'true' : undefined}
              onClick={() => {
                setLanguage(option.code as LanguageCode);
                setOpen(false);
                trigger.current?.focus();
              }}
            >
              {option.nativeName}
            </button>
          ))}
        </div>
      ) : null}
    </span>
  );
}
