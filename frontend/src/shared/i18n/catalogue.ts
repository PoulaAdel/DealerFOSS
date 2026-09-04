// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   catalogue — what a translated message is allowed to be.
//
// Usage:
//   A locale file exports `Catalogue`; en.ts defines the keys.
//
// Coding Instructions:
//   Plural forms are the CLDR categories, and they are not decoration.
//   English needs two, French two, German two, Russian FOUR (one/few/many)
//   and Arabic SIX (zero/one/two/few/many/other). Writing "1 car / N cars"
//   and appending an s produces text that is simply wrong in Russian for
//   2, 3, 4, 22, 23… and wrong in Arabic for almost everything.
//
//   Which category applies is decided by Intl.PluralRules, which ships in
//   every browser and is maintained against CLDR by the engine — so this
//   carries no plural tables of its own to fall out of date.

/**
 * The CLDR plural categories. A catalogue entry supplies the ones its language
 * uses; `other` is required because every language has it and it is the
 * fallback when a category is missing.
 */
export interface PluralForms {
  readonly zero?: string;
  readonly one?: string;
  readonly two?: string;
  readonly few?: string;
  readonly many?: string;
  readonly other: string;
}

/** A message is either fixed text or a set of plural forms. */
export type Message = string | PluralForms;

/** Values substituted into `{placeholders}`. `count` also selects the plural form. */
export type Substitutions = Readonly<Record<string, string | number>>;

export function isPluralForms(message: Message): message is PluralForms {
  return typeof message !== 'string';
}
