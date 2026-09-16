// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ServiceSetupPage — the two standing decisions a workshop makes once: the
//   jobs it sells, and what an hour costs.
//
//   IT EXISTS SO THE CATALOGUE IS NOT SEED-ONLY. Op codes and labour rates
//   arrived with the demo data already in them, and a capability a dealership
//   can use but not own is a capability reachable only by whoever can run a
//   program. That is the state this project spent a milestone getting out of.
//
// Usage:
//   Reachable at /workshop/setup, linked from the workshop.
//
// Coding Instructions:
//   TWO SCOPES ON ONE SCREEN, AND THE SCREEN SAYS SO. Op codes are the group's
//   shared vocabulary and have no rooftop; rates belong to one lot. Somebody
//   who does not know that will set a rate expecting it to apply everywhere, so
//   each panel states which it is rather than leaving it to be inferred.
//
//   NOTHING IS DELETED. Withdrawing is the operation, because a job written in
//   March cites its op code and must keep reading correctly.
//
//   A REFUSAL IS SHOWN, NOT PREDICTED. Changing any of this needs
//   Service.Configure organization-wide, which an advisor does not hold. The
//   screen offers the controls and reports what the server says — the same
//   choice every other screen here makes, and the same known gap: hiding
//   actions a role cannot take is its own register row.

import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router';
import { ApiError, api, post } from '../../shared/api';
import { useI18n, type MessageKey } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';
import {
  servicePayTypes,
  type LabourRateView,
  type OpCodeView,
  type OrganizationSummary,
  type Page,
  type RooftopSummary,
  type ServicePayType,
} from '../../shared/contracts';

type Load =
  | { kind: 'loading' }
  | { kind: 'ready' }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

export function ServiceSetupPage() {
  const { t } = useI18n();
  const describe = useApiMessage();

  const [load, setLoad] = useState<Load>({ kind: 'loading' });
  const [codes, setCodes] = useState<OpCodeView[]>([]);
  const [rates, setRates] = useState<LabourRateView[]>([]);
  const [rooftops, setRooftops] = useState<RooftopSummary[]>([]);
  const [error, setError] = useState<string | null>(null);

  const find = useCallback(async () => {
    try {
      // Withdrawn codes are shown here and nowhere else. This is the screen
      // where somebody puts one back, so hiding them would make that
      // impossible.
      const [catalogue, hourly, organization] = await Promise.all([
        api<Page<OpCodeView>>('/service/op-codes?activeOnly=false&limit=200'),
        api<LabourRateView[]>('/service/labour-rates'),
        api<OrganizationSummary>('/organization'),
      ]);

      setCodes(catalogue.rows);
      setRates(hourly);
      setRooftops(organization.legalEntities.flatMap((entity) => entity.rooftops));
      setLoad({ kind: 'ready' });
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 403) {
        setLoad({ kind: 'denied' });
        return;
      }

      setLoad({ kind: 'failed', message: describe(failure) });
    }
  }, [describe]);

  useEffect(() => {
    void find();
  }, [find]);

  async function act(work: () => Promise<unknown>) {
    setError(null);

    try {
      await work();
      await find();
    } catch (failure) {
      setError(describe(failure));
    }
  }

  if (load.kind === 'loading') {
    return <p>{t('serviceSetup.loading')}</p>;
  }

  if (load.kind === 'denied') {
    return (
      <section className="page">
        <h1>{t('serviceSetup.title')}</h1>
        <p className="note">{t('serviceSetup.denied')}</p>
      </section>
    );
  }

  if (load.kind === 'failed') {
    return (
      <section className="page">
        <h1>{t('serviceSetup.title')}</h1>
        <p className="error">{load.message}</p>
        <button type="button" onClick={() => void find()}>
          {t('common.retry')}
        </button>
      </section>
    );
  }

  return (
    <section className="page">
      <header className="page__head">
        <h1>{t('serviceSetup.title')}</h1>
        <Link to="/workshop">{t('serviceSetup.backToWorkshop')}</Link>
      </header>

      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>

      <RatesPanel
        rates={rates}
        rooftops={rooftops}
        onSet={(body) => act(() => post('/service/labour-rates', body))}
      />

      <OpCodesPanel
        codes={codes}
        onAdd={(body) => act(() => post('/service/op-codes', body))}
        onSetActive={(id, active) =>
          act(() => post(`/service/op-codes/${id}/active`, { active }))}
      />

      <p className="note">
        {t('serviceSetup.frozenNote', { count: codes.length })}
      </p>
    </section>
  );
}

/**
 * What an hour sells for, per lot and per payer.
 *
 * Shown as a grid of lots against pay types rather than a flat list, because
 * the question somebody arrives with is "what does NAG-01 charge for warranty"
 * and a list makes them find two things before they can answer it.
 */
function RatesPanel({
  rates,
  rooftops,
  onSet,
}: {
  rates: LabourRateView[];
  rooftops: RooftopSummary[];
  onSet: (body: Record<string, unknown>) => Promise<void>;
}) {
  const { t, format } = useI18n();

  const [rooftopId, setRooftopId] = useState('');
  const [appliesTo, setAppliesTo] = useState<ServicePayType>('CustomerPay');
  const [amount, setAmount] = useState('');

  const typed = Number(amount);
  const ready =
    rooftopId !== '' && amount.trim() !== '' && !Number.isNaN(typed) && typed >= 0;

  function rateFor(rooftop: string, payType: ServicePayType) {
    return rates.find((r) => r.rooftopId === rooftop && r.appliesTo === payType && r.isActive);
  }

  return (
    <section className="panel">
      <h2>{t('serviceSetup.ratesTitle')}</h2>
      <p className="note">{t('serviceSetup.ratesNote')}</p>

      <div className="scroll">
        <table>
          <thead>
            <tr>
              <th>{t('serviceSetup.location')}</th>
              {servicePayTypes.map((payType) => (
                <th key={payType}>{t(`enum.servicePayType.${payType}` as MessageKey)}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rooftops.map((rooftop) => (
              <tr key={rooftop.id}>
                <td>
                  {rooftop.name} <span className="muted">{rooftop.code}</span>
                </td>
                {servicePayTypes.map((payType) => {
                  const rate = rateFor(rooftop.id, payType);

                  return (
                    <td key={payType} className="num">
                      {/* Not shown as zero. A lot that has not set a rate and a
                          lot that charges nothing are different facts, and an
                          advisor reading 0.00 would believe the second. */}
                      {rate === undefined
                        ? <span className="muted">{t('serviceSetup.notSet')}</span>
                        : format.money(rate.amountPerHour, rate.currency)}
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="row">
        <div className="field">
          <label htmlFor="rate-rooftop">{t('serviceSetup.location')}</label>
          <select
            id="rate-rooftop"
            value={rooftopId}
            onChange={(event) => setRooftopId(event.target.value)}
          >
            <option value="">{t('serviceSetup.chooseLocation')}</option>
            {rooftops.map((rooftop) => (
              <option key={rooftop.id} value={rooftop.id}>
                {rooftop.name} · {rooftop.code}
              </option>
            ))}
          </select>
        </div>

        <div className="field">
          <label htmlFor="rate-applies">{t('serviceSetup.whoPays')}</label>
          <select
            id="rate-applies"
            value={appliesTo}
            onChange={(event) => setAppliesTo(event.target.value as ServicePayType)}
          >
            {servicePayTypes.map((payType) => (
              <option key={payType} value={payType}>
                {t(`enum.servicePayType.${payType}` as MessageKey)}
              </option>
            ))}
          </select>
        </div>

        <div className="field">
          <label htmlFor="rate-amount">{t('serviceSetup.perHour')}</label>
          <input
            id="rate-amount"
            inputMode="decimal"
            value={amount}
            onChange={(event) => setAmount(event.target.value)}
          />
        </div>
      </div>

      <div className="actions">
        <button
          type="button"
          disabled={!ready}
          onClick={() => {
            void onSet({
              rooftopId,
              // The pay type IS the name in every shop that has ever set these.
              // A separate name box would be a second thing to keep consistent
              // for no reader's benefit.
              name: appliesTo,
              amountPerHour: typed,
              currency: 'USD',
              appliesTo,
            }).then(() => setAmount(''));
          }}
        >
          {t('serviceSetup.setRate')}
        </button>
      </div>
    </section>
  );
}

/** The jobs the group sells. No rooftop: this is shared vocabulary. */
function OpCodesPanel({
  codes,
  onAdd,
  onSetActive,
}: {
  codes: OpCodeView[];
  onAdd: (body: Record<string, unknown>) => Promise<void>;
  onSetActive: (id: string, active: boolean) => Promise<void>;
}) {
  const { t } = useI18n();

  const [code, setCode] = useState('');
  const [description, setDescription] = useState('');
  const [hours, setHours] = useState('');
  const [payType, setPayType] = useState<ServicePayType>('CustomerPay');

  const typed = Number(hours);
  const ready =
    code.trim() !== '' && description.trim() !== '' && !Number.isNaN(typed) && typed > 0;

  return (
    <section className="panel">
      <h2>{t('serviceSetup.jobsTitle')}</h2>
      <p className="note">{t('serviceSetup.jobsNote')}</p>

      <div className="scroll">
        <table>
          <thead>
            <tr>
              <th>{t('serviceSetup.code')}</th>
              <th>{t('serviceSetup.describes')}</th>
              <th>{t('serviceSetup.standardHours')}</th>
              <th>{t('serviceSetup.whoPays')}</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {codes.map((entry) => (
              <tr key={entry.id} className={entry.isActive ? undefined : 'muted'}>
                <td>{entry.code}</td>
                <td>{entry.description}</td>
                <td className="num">{entry.standardHours}</td>
                <td>{t(`enum.servicePayType.${entry.defaultPayType}` as MessageKey)}</td>
                <td>
                  <button
                    type="button"
                    onClick={() => void onSetActive(entry.id, !entry.isActive)}
                  >
                    {entry.isActive ? t('serviceSetup.withdraw') : t('serviceSetup.restore')}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="row">
        <div className="field">
          <label htmlFor="op-code">{t('serviceSetup.code')}</label>
          <input id="op-code" value={code} onChange={(e) => setCode(e.target.value)} />
        </div>

        <div className="field field--grow">
          <label htmlFor="op-description">{t('serviceSetup.describes')}</label>
          <input
            id="op-description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
          />
        </div>

        <div className="field">
          <label htmlFor="op-hours">{t('serviceSetup.standardHours')}</label>
          <input
            id="op-hours"
            inputMode="decimal"
            value={hours}
            onChange={(e) => setHours(e.target.value)}
          />
        </div>

        <div className="field">
          <label htmlFor="op-pay">{t('serviceSetup.whoPays')}</label>
          <select
            id="op-pay"
            value={payType}
            onChange={(e) => setPayType(e.target.value as ServicePayType)}
          >
            {servicePayTypes.map((option) => (
              <option key={option} value={option}>
                {t(`enum.servicePayType.${option}` as MessageKey)}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="actions">
        <button
          type="button"
          disabled={!ready}
          onClick={() => {
            void onAdd({
              code: code.trim(),
              description: description.trim(),
              standardHours: typed,
              defaultPayType: payType,
            }).then(() => {
              setCode('');
              setDescription('');
              setHours('');
            });
          }}
        >
          {t('serviceSetup.addJob')}
        </button>
      </div>
    </section>
  );
}
