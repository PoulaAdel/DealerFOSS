// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   DealRegistrationAddress — where the buyer will register or garage the car.
//   SoldRegistrationAddress renders the same address read-only once the deal's
//   terms are frozen.
//
// Usage:
//   Mounted by DealsPage next to DealTax while the deal's terms are open.
//
// Coding Instructions:
//   NOT THE CUSTOMER'S OWN ADDRESS (ADR-024). "Current location" is never the
//   input for tax — the governing fact is where the car will be registered,
//   which a salesperson may know before anybody has looked up the customer's
//   file, and which may differ from it (a company car registered at the
//   business, a gift registered at the recipient's address). That is also why
//   this is not pre-filled from the customer record: doing so would make the
//   two addresses look linked when they are deliberately two separate facts.
//
//   Saving replaces the whole address, the same one-call shape as DealTax's
//   TaxedAt. Clearing it is its own action rather than "save all blank fields",
//   because a person who empties every box is asking a different question than
//   one who typed a shorter address on purpose.

import { useState } from 'react';
import { post } from '../../shared/api';
import type { DealDetail, RegistrationAddressView } from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';

type Draft = {
  line1: string;
  line2: string;
  city: string;
  administrativeArea: string;
  county: string;
  postalCode: string;
  country: string;
};

function draftFrom(address: RegistrationAddressView | null): Draft {
  return {
    line1: address?.line1 ?? '',
    line2: address?.line2 ?? '',
    city: address?.city ?? '',
    administrativeArea: address?.administrativeArea ?? '',
    county: address?.county ?? '',
    postalCode: address?.postalCode ?? '',
    country: address?.country ?? '',
  };
}

function formatted(address: RegistrationAddressView): string {
  return [
    address.line1,
    address.line2,
    address.city,
    address.administrativeArea,
    address.county,
    address.postalCode,
    address.country,
  ]
    .filter((part) => part !== null && part !== '')
    .join(', ');
}

export function DealRegistrationAddress({
  deal,
  onChanged,
}: {
  deal: DealDetail;
  onChanged: (updated: DealDetail) => void;
}) {
  const { t } = useI18n();
  const describe = useApiMessage();

  const [draft, setDraft] = useState<Draft>(() => draftFrom(deal.registrationAddress));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function save(address: RegistrationAddressView | null) {
    setError(null);
    setBusy(true);

    try {
      onChanged(await post<DealDetail>(`/deals/${deal.id}/registration-address`, { address }));
    } catch (failure) {
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <h3>{t('deals.registrationAddress')}</h3>
      <p className="note">{t('deals.registrationAddressLede')}</p>

      <fieldset className="taxAddress">
        <legend className="visually-hidden">{t('deals.registrationAddress')}</legend>

        <div className="row">
          <label htmlFor="reg-line1">{t('deals.regLine1')}</label>
          <input
            id="reg-line1"
            value={draft.line1}
            disabled={busy}
            onChange={(e) => setDraft({ ...draft, line1: e.target.value })}
          />

          <label htmlFor="reg-line2">{t('deals.regLine2')}</label>
          <input
            id="reg-line2"
            value={draft.line2}
            disabled={busy}
            onChange={(e) => setDraft({ ...draft, line2: e.target.value })}
          />

          <label htmlFor="reg-city">{t('deals.regCity')}</label>
          <input
            id="reg-city"
            value={draft.city}
            disabled={busy}
            onChange={(e) => setDraft({ ...draft, city: e.target.value })}
          />

          <label htmlFor="reg-area">{t('deals.regArea')}</label>
          <input
            id="reg-area"
            value={draft.administrativeArea}
            disabled={busy}
            onChange={(e) => setDraft({ ...draft, administrativeArea: e.target.value })}
          />

          {/* Its own field, not folded into the state — the same reason the
              tax address keeps it separate: US sales tax varies by both. */}
          <label htmlFor="reg-county">{t('deals.regCounty')}</label>
          <input
            id="reg-county"
            value={draft.county}
            disabled={busy}
            onChange={(e) => setDraft({ ...draft, county: e.target.value })}
          />

          <label htmlFor="reg-postal">{t('deals.regPostalCode')}</label>
          <input
            id="reg-postal"
            value={draft.postalCode}
            disabled={busy}
            onChange={(e) => setDraft({ ...draft, postalCode: e.target.value })}
          />

          <label htmlFor="reg-country">{t('deals.regCountry')}</label>
          <input
            id="reg-country"
            value={draft.country}
            disabled={busy}
            maxLength={2}
            onChange={(e) => setDraft({ ...draft, country: e.target.value })}
          />
        </div>
      </fieldset>

      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>

      <div className="actions">
        <button
          type="button"
          disabled={busy}
          onClick={() =>
            void save({
              line1: draft.line1.trim(),
              line2: draft.line2.trim() === '' ? null : draft.line2.trim(),
              city: draft.city.trim(),
              administrativeArea: draft.administrativeArea.trim() === '' ? null : draft.administrativeArea.trim(),
              county: draft.county.trim() === '' ? null : draft.county.trim(),
              postalCode: draft.postalCode.trim() === '' ? null : draft.postalCode.trim(),
              country: draft.country.trim().toUpperCase(),
            })
          }
        >
          {busy ? t('common.saving') : t('deals.regSave')}
        </button>
        {deal.registrationAddress === null ? null : (
          <button
            type="button"
            disabled={busy}
            onClick={() => {
              setDraft(draftFrom(null));
              void save(null);
            }}
          >
            {t('deals.regClear')}
          </button>
        )}
      </div>
    </>
  );
}

/**
 * The registration address as it was when the deal froze. Read-only, for the
 * same reason SoldTax is — it is what an approval was actually defended by.
 */
export function SoldRegistrationAddress({ deal }: { deal: DealDetail }) {
  const { t } = useI18n();

  if (deal.registrationAddress === null) {
    return null;
  }

  return (
    <>
      <h3>{t('deals.registrationAddress')}</h3>
      <p className="note">{formatted(deal.registrationAddress)}</p>
    </>
  );
}
