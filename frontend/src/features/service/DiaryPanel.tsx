// DiaryPanel — the cars that are coming, on the same screen as the cars that are
// here.
//
// Use:  mounted at the top of WorkshopPage. Not a route of its own.
// Edit: four things here are deliberate.
//
//       (1) It lives ON the workshop screen rather than behind a second tab. The
//       question a service manager asks is "what have I got today" and the answer
//       is both halves at once — what is on the ramps and what is still to come.
//       Splitting them across two routes makes somebody hold the answer in their
//       head.
//
//       (2) "It's here" opens the job AND selects it, in one click. The server
//       does both in one transaction; this screen would be lying to offer them as
//       two steps.
//
//       (3) The day's load comes from the server, not from summing the rows here.
//       An arrived car's hours belong to its job, and a browser that re-derived
//       the figure would be a second copy of that rule — the one that drifts.
//
//       (4) A refusal is shown, never predicted. Booking into a full day is
//       allowed on purpose (see AppointmentService), so there is nothing for this
//       screen to grey out.

import { useCallback, useEffect, useState } from 'react';
import { ApiError, api, post } from '../../shared/api';
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';
import { useEnumLabel } from '../../shared/i18n/enums';
import type {
  AppointmentView,
  ArrivalResult,
  CustomerSummary,
  Diary,
  RepairOrderDetail,
  VehicleSummary,
} from '../../shared/contracts';

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; diary: Diary }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

export function DiaryPanel({ onArrived }: { onArrived: (job: RepairOrderDetail) => void }) {
  const { t, format } = useI18n();
  const describe = useApiMessage();
  const label = useEnumLabel();

  const [load, setLoad] = useState<Load>({ kind: 'loading' });
  const [working, setWorking] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [booking, setBooking] = useState(false);

  const find = useCallback(async () => {
    setLoad({ kind: 'loading' });

    try {
      // Only what is still expected. A diary showing last month's no-shows above
      // this afternoon is the wrong way round for the person reading it.
      setLoad({ kind: 'ready', diary: await api<Diary>('/appointments?openOnly=true&limit=100') });
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

  async function arrive(appointment: AppointmentView) {
    setWorking(appointment.id);
    setError(null);

    try {
      const result = await post<ArrivalResult>(`/appointments/${appointment.id}/arrive`, {
        currency: 'USD',
      });

      await find();
      // Straight into the job. The car is at the counter and the next thing
      // anybody does is write up what it came in for.
      onArrived(result.repairOrder);
    } catch (failure) {
      setError(describe(failure));
    } finally {
      setWorking(null);
    }
  }

  async function didNotCome(appointment: AppointmentView) {
    setWorking(appointment.id);
    setError(null);

    try {
      await post(`/appointments/${appointment.id}/close`, { cancelled: false });
      await find();
    } catch (failure) {
      setError(describe(failure));
    } finally {
      setWorking(null);
    }
  }

  if (load.kind === 'loading') {
    return (
      <p className="state" aria-live="polite">
        {t('diary.loading')}
      </p>
    );
  }

  // Not an error. Plenty of people who can see the workshop have no business
  // taking bookings, and a red panel would say they had done something wrong.
  if (load.kind === 'denied') {
    return null;
  }

  if (load.kind === 'failed') {
    return (
      <section className="panel" role="alert">
        <h2>{t('diary.title')}</h2>
        <p className="error">{load.message}</p>
        <button type="button" onClick={() => void find()}>
          {t('common.retry')}
        </button>
      </section>
    );
  }

  const { appointments, load: byDay } = load.diary;

  return (
    <section className="panel panel--diary">
      <header className="panel__head">
        <h2>{t('diary.title')}</h2>
        <span className="muted">{t('diary.count', { count: appointments.length })}</span>
        {booking ? null : (
          <button type="button" onClick={() => setBooking(true)}>
            {t('diary.book')}
          </button>
        )}
      </header>

      {booking ? (
        <BookCar
          onCancel={() => setBooking(false)}
          onBooked={async () => {
            setBooking(false);
            await find();
          }}
        />
      ) : null}

      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>

      {/* What each day is carrying, straight from the server. This is the whole
          answer to "can I fit this in", and it is why the diary is worth having
          rather than a list of dates. */}
      {byDay.length === 0 ? null : (
        <ul className="chips">
          {byDay.map((day) => (
            <li key={day.date} className="chip">
              <strong>{format.date(day.date)}</strong>{' '}
              {/* A day with an unestimated car on it says so. Folding it in as
                  zero hours would read as a free day, which is the one thing
                  this figure must never say by mistake. */}
              {t(day.unestimated === 0 ? 'diary.dayLoad' : 'diary.dayLoadSome', {
                count: day.expected,
                hours: format.number(day.bookedHours, { maximumFractionDigits: 1 }),
                unestimated: day.unestimated,
              })}
            </li>
          ))}
        </ul>
      )}

      {appointments.length === 0 ? (
        <p className="state">{t('diary.empty')}</p>
      ) : (
        <div className="scroll">
          <table>
            <caption className="visually-hidden">
              {t('diary.count', { count: appointments.length })}
            </caption>
            <thead>
              <tr>
                <th scope="col">{t('diary.colWhen')}</th>
                <th scope="col">{t('diary.colCustomer')}</th>
                <th scope="col">{t('diary.colVehicle')}</th>
                <th scope="col">{t('diary.colReason')}</th>
                <th scope="col">{t('diary.colHours')}</th>
                <th scope="col">{t('diary.colWhat')}</th>
              </tr>
            </thead>
            <tbody>
              {appointments.map((appointment) => (
                <tr key={appointment.id}>
                  <td>{format.dateTime(appointment.scheduledFor)}</td>
                  {/* Customer names, cars and the reason they gave are records.
                      They print exactly as the dealership typed them, in every
                      language. */}
                  <td>{appointment.customerName}</td>
                  <td>{appointment.vehicle}</td>
                  <td>{appointment.reason}</td>
                  <td>
                    {appointment.estimatedHours === null ? (
                      <span className="muted">{t('diary.unestimated')}</span>
                    ) : (
                      format.number(appointment.estimatedHours, { maximumFractionDigits: 1 })
                    )}
                  </td>
                  <td>
                    {appointment.isOpen ? (
                      <div className="actions">
                        <button
                          type="button"
                          className="primary"
                          disabled={working === appointment.id}
                          onClick={() => void arrive(appointment)}
                        >
                          {working === appointment.id ? t('diary.arriving') : t('diary.itsHere')}
                        </button>
                        <button
                          type="button"
                          disabled={working === appointment.id}
                          onClick={() => void didNotCome(appointment)}
                        >
                          {t('diary.didNotCome')}
                        </button>
                      </div>
                    ) : appointment.repairOrderNumber === null ? (
                      <span className="muted">
                        {label('appointmentStatus', appointment.status)}
                      </span>
                    ) : (
                      <span className="muted">
                        {t('diary.becameJob', { number: appointment.repairOrderNumber })}
                      </span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

/**
 * Taking a booking.
 *
 * The customer and the car are chosen from what the dealership already has, not
 * typed — a service diary full of "J. Smith, blue Focus" is how a workshop ends
 * up unable to find a car's history, which is the one thing the capability
 * exists to keep.
 */
function BookCar({
  onCancel,
  onBooked,
}: {
  onCancel: () => void;
  onBooked: () => Promise<void>;
}) {
  const { t } = useI18n();
  const describe = useApiMessage();

  const [customers, setCustomers] = useState<CustomerSummary[]>([]);
  const [vehicles, setVehicles] = useState<VehicleSummary[]>([]);
  const [rooftopId, setRooftopId] = useState<string | null>(null);

  const [customerId, setCustomerId] = useState('');
  const [vehicleId, setVehicleId] = useState('');
  const [when, setWhen] = useState('');
  const [hours, setHours] = useState('');
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void (async () => {
      try {
        setCustomers(await api<CustomerSummary[]>('/customers?limit=200'));
        setVehicles(await api<VehicleSummary[]>('/vehicles?limit=200'));

        // The workshop this person covers. Taken from a job they can already
        // see rather than asked for, because somebody at one location has
        // exactly one answer and being made to pick it is noise.
        const jobs = await api<{ rooftopId: string }[]>('/repair-orders?limit=1');
        setRooftopId(jobs[0]?.rooftopId ?? null);
      } catch (failure) {
        setError(describe(failure));
      }
    })();
  }, [describe]);

  async function submit() {
    setBusy(true);
    setError(null);

    try {
      await post('/appointments', {
        rooftopId,
        customerId,
        vehicleId,
        // datetime-local has no zone. The browser's own offset is the honest
        // reading of what somebody typed at a counter in that workshop.
        scheduledFor: new Date(when).toISOString(),
        reason,
        estimatedHours: hours.trim() === '' ? null : Number(hours),
      });

      await onBooked();
    } catch (failure) {
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  const ready =
    rooftopId !== null &&
    customerId !== '' &&
    vehicleId !== '' &&
    when !== '' &&
    reason.trim() !== '';

  return (
    <section className="panel">
      <h3>{t('diary.bookTitle')}</h3>

      <div className="row">
        <div className="field field--grow">
          <label htmlFor="diary-customer">{t('diary.customer')}</label>
          <select
            id="diary-customer"
            value={customerId}
            onChange={(event) => setCustomerId(event.target.value)}
          >
            <option value="">{t('diary.pickCustomer')}</option>
            {customers.map((customer) => (
              <option key={customer.id} value={customer.id}>
                {customer.displayName}
              </option>
            ))}
          </select>
        </div>

        <div className="field field--grow">
          <label htmlFor="diary-vehicle">{t('diary.vehicle')}</label>
          <select
            id="diary-vehicle"
            value={vehicleId}
            onChange={(event) => setVehicleId(event.target.value)}
          >
            <option value="">{t('diary.pickVehicle')}</option>
            {vehicles.map((vehicle) => (
              <option key={vehicle.id} value={vehicle.id}>
                {vehicle.displayName}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="row">
        <div className="field field--grow">
          <label htmlFor="diary-when">{t('diary.when')}</label>
          <input
            id="diary-when"
            type="datetime-local"
            value={when}
            onChange={(event) => setWhen(event.target.value)}
            dir="ltr"
          />
        </div>

        <div className="field">
          <label htmlFor="diary-hours">{t('diary.hours')}</label>
          <input
            id="diary-hours"
            type="number"
            min="0"
            step="0.25"
            value={hours}
            onChange={(event) => setHours(event.target.value)}
            dir="ltr"
          />
          <p className="hint">{t('diary.hoursHint')}</p>
        </div>
      </div>

      <div className="field">
        <label htmlFor="diary-reason">{t('diary.reason')}</label>
        <input
          id="diary-reason"
          value={reason}
          placeholder={t('diary.reasonPlaceholder')}
          onChange={(event) => setReason(event.target.value)}
        />
      </div>

      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>

      <div className="actions">
        <button
          type="button"
          className="primary"
          disabled={busy || !ready}
          onClick={() => void submit()}
        >
          {busy ? t('diary.taking') : t('diary.take')}
        </button>
        <button type="button" onClick={onCancel}>
          {t('common.cancel')}
        </button>
      </div>
    </section>
  );
}
