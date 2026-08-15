// RecallCheck — what the public safety-recall record says about a car.
//
// Use:  <RecallCheck vehicleId={unit.vehicleId} />, inside a detail band.
// Edit: THREE THINGS HERE ARE ABOUT SOMEBODY'S BRAKES, NOT ABOUT LAYOUT.
//
//       ONE. It runs ON REQUEST, never when the screen opens (decided
//       2026-08-15, doc 11 §7). This is an outbound call to a regulator's
//       service, and a stock screen left open on a desk all afternoon must not
//       keep asking on somebody else's behalf. The button is the consent.
//
//       TWO. "Could not reach the service" and "no campaigns found" are drawn
//       DIFFERENTLY and must stay that way. The server answers 503 for the
//       first and an empty list for the second precisely so this screen can
//       tell them apart; rendering a timeout as an all-clear would invent
//       safety out of a network failure. That is the whole reason
//       `RecallErrors.Unavailable` exists.
//
//       THREE. The caveat is not fine print. The public record is indexed by
//       MODEL, not by VIN, and carries no note of whether this particular car
//       has had the work done — a car showing four campaigns may have had all
//       four done years ago by a previous owner. `appliesToModelNotVehicle` is
//       sent on every report so this screen has to be handed the caveat rather
//       than remember it, and it is rendered above the list, not below.

import { useState } from 'react';
import { ApiError, api } from '../../shared/api';
import type { RecallReport } from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';

type Check =
  | { kind: 'idle' }
  | { kind: 'checking' }
  | { kind: 'report'; report: RecallReport }
  /** The regulator did not answer. NOT the same as "this car is clear". */
  | { kind: 'unavailable'; message: string }
  /** Too little identification to look one up — a trailer with a frame plate. */
  | { kind: 'notIdentifiable'; message: string }
  | { kind: 'failed'; message: string };

export function RecallCheck({ vehicleId }: { vehicleId: string }) {
  const { t, format } = useI18n();
  const describe = useApiMessage();
  const [check, setCheck] = useState<Check>({ kind: 'idle' });

  async function run() {
    setCheck({ kind: 'checking' });

    try {
      setCheck({
        kind: 'report',
        report: await api<RecallReport>(`/vehicles/${vehicleId}/recalls`),
      });
    } catch (failure) {
      if (failure instanceof ApiError && failure.code === 'recalls.unavailable') {
        setCheck({ kind: 'unavailable', message: describe(failure) });
        return;
      }

      if (failure instanceof ApiError && failure.code === 'recalls.not_identifiable') {
        setCheck({ kind: 'notIdentifiable', message: describe(failure) });
        return;
      }

      setCheck({ kind: 'failed', message: describe(failure) });
    }
  }

  return (
    <section className="recalls">
      <h3>{t('recalls.title')}</h3>

      {check.kind === 'idle' ? <p className="note">{t('recalls.onRequest')}</p> : null}

      <div className="actions">
        <button type="button" disabled={check.kind === 'checking'} onClick={() => void run()}>
          {check.kind === 'checking'
            ? t('recalls.checking')
            : check.kind === 'idle'
              ? t('recalls.check')
              : t('recalls.checkAgain')}
        </button>
      </div>

      {/* An outbound service failing is not the dealership doing anything wrong,
          so it is a warning and not an alert — but it is emphatically not
          silence, because silence here reads as an all-clear. */}
      {check.kind === 'unavailable' ? (
        <p className="note note--warn" role="status">
          {check.message}
        </p>
      ) : null}

      {check.kind === 'notIdentifiable' ? (
        <p className="note" role="status">
          {check.message}
        </p>
      ) : null}

      {check.kind === 'failed' ? (
        <p className="error" role="alert">
          {check.message}
        </p>
      ) : null}

      {check.kind === 'report' ? (
        <>
          {/* Above the list, always. See point THREE in the file header. */}
          <p className="note note--warn">
            {t('recalls.caveat', {
              year: String(check.report.modelYear),
              make: check.report.make,
              model: check.report.model,
            })}
          </p>

          {check.report.campaigns.length === 0 ? (
            <p className="note" role="status">
              {t('recalls.noneFound')}
            </p>
          ) : (
            <ul className="recalls__list" aria-label={t('recalls.title')}>
              {check.report.campaigns.map((campaign) => (
                <li key={campaign.campaignNumber}>
                  <p>
                    {/* A regulator's campaign number is a code: it reads left to
                        right even on an Arabic page, or the bidi algorithm
                        reorders the groups and somebody quotes the wrong one. */}
                    <span className="strong mono" dir="ltr">
                      {campaign.campaignNumber}
                    </span>{' '}
                    <span className="muted">
                      {campaign.component}
                      {campaign.reportedOn === null
                        ? ''
                        : ` · ${format.date(campaign.reportedOn)}`}
                    </span>
                  </p>

                  {/* The regulator's two judgements, given as words rather than
                      as a colour: "do not drive" is the most serious thing this
                      screen can say and must survive being read aloud. */}
                  {campaign.doNotDrive ? (
                    <p className="chip chip--lost">{t('recalls.doNotDrive')}</p>
                  ) : null}
                  {campaign.parkOutside ? (
                    <p className="chip chip--warn">{t('recalls.parkOutside')}</p>
                  ) : null}

                  <p>{campaign.summary}</p>
                  <p className="muted">{t('recalls.remedy', { remedy: campaign.remedy })}</p>
                </li>
              ))}
            </ul>
          )}
        </>
      ) : null}
    </section>
  );
}
