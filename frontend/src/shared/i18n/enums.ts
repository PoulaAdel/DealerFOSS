// enums — turning what the API says into what a person reads.
//
// Use:  const label = useEnumLabel();
//       label('inventoryStatus', unit.status)   → "On hold" / "Réservé"
//
// Edit: the API answers with `"OnHold"`, and several screens used to print that
//       straight into a table cell. That was already wrong in English — nobody
//       writes "OnHold" — and it is wrong in a way that no amount of translating
//       the surrounding page fixes, because the value never passed through a
//       catalogue at all.
//
//       The type parameter is what keeps this honest. `EnumName` is derived from
//       the message keys themselves, so a family can only be asked for if it
//       exists, and adding a value to a union in contracts.ts without adding its
//       label here is a typecheck failure at the CALL SITE — not a screen that
//       silently prints the raw value.

import { useCallback } from 'react';
import { useI18n, type MessageKey } from './index';

// Both of these go through a helper taking a naked type parameter, which is
// what makes the conditional DISTRIBUTE over the union of message keys. Written
// inline against `MessageKey` directly it asks "does the whole union match this
// pattern", the answer is no, and the result is `never` — which shows up as
// every call site failing with "not assignable to parameter of type 'never'".
type NameOf<Key> = Key extends `enum.${infer Name}.${string}` ? Name : never;
type ValueOf<Key, Name extends string> = Key extends `enum.${Name}.${infer Value}`
  ? Value
  : never;

/**
 * The enum families the catalogue carries labels for, extracted from the key
 * names. `'enum.inventoryStatus.OnHold'` yields `'inventoryStatus'`.
 */
export type EnumName = NameOf<MessageKey>;

/** The values one family has labels for. */
export type EnumValue<Name extends EnumName> = ValueOf<MessageKey, Name>;

export function useEnumLabel(): <Name extends EnumName>(
  name: Name,
  value: EnumValue<Name>,
) => string {
  const { t } = useI18n();

  return useCallback(
    <Name extends EnumName>(name: Name, value: EnumValue<Name>) =>
      // The template matches the key shape the catalogue is written in, so this
      // cast is a formality rather than an escape — EnumValue already narrowed
      // `value` to something that produces a real key.
      t(`enum.${name}.${value}` as MessageKey),
    [t],
  );
}
