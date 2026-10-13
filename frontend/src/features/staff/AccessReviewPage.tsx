// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   AccessReviewPage — who holds which right where, and the button that records
//   that somebody has looked.
//
// Usage:
//   Reachable at /security/access-review by anybody holding
//   Security.ReviewAccess organization-wide. Everybody else is refused by the
//   server and shown the denied band.
//
// Coding Instructions:
//   THE FILTER IS THE SCREEN'S REASON TO EXIST. The server ships one flat table
//   because both reading directions are sorts of the same rows — "everything
//   Dana can do" and "everyone who can refund". Typing a permission is how a
//   reviewer asks the second question, which is the one they actually came for,
//   so it filters on permission AND on name rather than on name alone.
//
//   THE CHECKSUM GOES BACK UNTOUCHED. Signing sends the checksum of the table
//   that was DISPLAYED, not a fresh read — that is what makes the signature a
//   statement about what this person saw. If access changed in between the
//   server refuses by name and this screen re-reads and says so, rather than
//   signing the new picture on their behalf.
//
//   A person holding one permission through two roles appears twice. That is
//   the server's decision and it is right: collapsing them would hide that
//   removing one role changes nothing, which is what a reviewer is looking for.

import { useCallback, useEffect, useMemo, useState } from 'react';
import { ApiError, api, openDocument, post } from '../../shared/api';
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';
import type { AccessReview, AccessReviewSignature } from '../../shared/contracts';

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; review: AccessReview }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

export function AccessReviewPage() {
  const { t, format } = useI18n();
  const describe = useApiMessage();

  const [load, setLoad] = useState<Load>({ kind: 'loading' });
  const [filter, setFilter] = useState('');
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);
  const [signed, setSigned] = useState<AccessReviewSignature | null>(null);
  const [problem, setProblem] = useState<string | null>(null);

  const read = useCallback(async () => {
    setLoad({ kind: 'loading' });

    try {
      setLoad({ kind: 'ready', review: await api<AccessReview>('/security/access-review') });
    } catch (error) {
      if (error instanceof ApiError && error.status === 403) {
        setLoad({ kind: 'denied' });
        return;
      }

      setLoad({ kind: 'failed', message: describe(error) });
    }
  }, [describe]);

  useEffect(() => {
    void read();
  }, [read]);

  const rows = useMemo(() => {
    if (load.kind !== 'ready') {
      return [];
    }

    const wanted = filter.trim().toLowerCase();
    if (wanted === '') {
      return load.review.rows;
    }

    // Permission first in the predicate because it is the field a reviewer
    // types into: they arrive asking "who can approve a deal", not "what does
    // Dana do".
    return load.review.rows.filter(
      (r) =>
        r.permission.toLowerCase().includes(wanted) ||
        r.displayName.toLowerCase().includes(wanted) ||
        r.email.toLowerCase().includes(wanted) ||
        r.roleName.toLowerCase().includes(wanted),
    );
  }, [load, filter]);

  async function sign() {
    if (load.kind !== 'ready') {
      return;
    }

    setBusy(true);
    setProblem(null);

    try {
      const signature = await post<AccessReviewSignature>('/security/access-review/sign', {
        // The checksum of what is on screen. Not re-read on purpose — see the
        // header note.
        checksum: load.review.checksum,
        note: note.trim() === '' ? null : note.trim(),
      });

      setSigned(signature);
      setNote('');
      await read();
    } catch (error) {
      setProblem(describe(error));

      // Whatever went wrong, the table in front of them may now be stale — and
      // if the server refused BECAUSE it is stale, re-reading is the one thing
      // that lets them act. Doing it unconditionally keeps the two cases from
      // needing to be told apart here.
      await read();
    } finally {
      setBusy(false);
    }
  }

  if (load.kind === 'loading') {
    return <p className="state">{t('common.loading')}</p>;
  }

  if (load.kind === 'denied') {
    return <p className="state" role="alert">{t('accessReview.denied')}</p>;
  }

  if (load.kind === 'failed') {
    return (
      <div className="state">
        <p role="alert">{load.message}</p>
        <button type="button" onClick={() => void read()}>{t('common.retry')}</button>
      </div>
    );
  }

  const review = load.review;
  const overdue = review.nextDueAt !== null && new Date(review.nextDueAt) < new Date();

  return (
    <section>
      <header className="page-head">
        <h1>{t('accessReview.title')}</h1>
        <p className="muted">{t('accessReview.intro')}</p>
      </header>

      <div className="band">
        <dl className="facts">
          <dt>{t('accessReview.lastReviewed')}</dt>
          <dd>
            {review.lastSignature === null ? (
              <span className="chip chip--warn">{t('accessReview.never')}</span>
            ) : (
              <>
                {format.date(review.lastSignature.takenAt)}
                {' — '}
                {review.lastSignature.signedByDisplayName}
              </>
            )}
          </dd>

          <dt>{t('accessReview.nextDue')}</dt>
          <dd>
            {review.nextDueAt === null ? (
              <span className="muted">—</span>
            ) : overdue ? (
              <span className="chip chip--warn">{format.date(review.nextDueAt)}</span>
            ) : (
              format.date(review.nextDueAt)
            )}
          </dd>

          <dt>{t('accessReview.cadence')}</dt>
          <dd>{t('accessReview.everyDays', { days: review.reviewEveryDays })}</dd>
        </dl>

        {/* Shown only once there is something to compare against. "Nothing has
            changed since never" is not a useful sentence. */}
        {review.lastSignature !== null && (
          <p className={review.lastSignature.checksum === review.checksum ? 'note' : 'note note--warn'}>
            {review.lastSignature.checksum === review.checksum
              ? t('accessReview.unchanged')
              : t('accessReview.changedSince')}
          </p>
        )}
      </div>

      <div className="band">
        <label htmlFor="access-filter">{t('accessReview.filter')}</label>
        <input
          id="access-filter"
          type="search"
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          placeholder={t('accessReview.filterHint')}
        />

        <button type="button" onClick={() => void openDocument('/security/access-review/export')}>
          {t('accessReview.export')}
        </button>
      </div>

      {problem !== null && <p role="alert" className="state">{problem}</p>}
      {signed !== null && (
        <p role="status" className="note">
          {t('accessReview.signedOn', { date: format.date(signed.takenAt) })}
        </p>
      )}

      <div className="scroll">
        <table className="table">
          <caption className="visually-hidden">
            {t('accessReview.rowCount', { count: rows.length })}
          </caption>
          <thead>
            <tr>
              <th scope="col">{t('accessReview.colPerson')}</th>
              <th scope="col">{t('accessReview.colPermission')}</th>
              <th scope="col">{t('accessReview.colRole')}</th>
              <th scope="col">{t('accessReview.colWhere')}</th>
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 ? (
              <tr>
                <td colSpan={4} className="muted">{t('accessReview.noMatch')}</td>
              </tr>
            ) : (
              rows.map((row) => (
                <tr key={`${row.userId}-${row.permission}-${row.roleName}-${row.rooftopId ?? ''}`}>
                  <td>
                    {row.displayName}
                    {/* A leaver whose grants are still on the record is the
                        single most useful thing a review surfaces, so it is a
                        chip rather than a column somebody has to scan for. */}
                    {!row.isActive && <span className="chip chip--warn">{t('accessReview.inactive')}</span>}
                    {!row.hasSecondFactor && <span className="chip">{t('accessReview.noSecondFactor')}</span>}
                  </td>
                  <td><code>{row.permission}</code></td>
                  <td>{row.roleName}</td>
                  <td>
                    {row.isOrganizationWide ? (
                      <span className="chip chip--warn">{t('accessReview.everywhere')}</span>
                    ) : (
                      t('accessReview.oneLocation')
                    )}
                    {row.ownRecordsOnly && <span className="chip">{t('accessReview.ownWorkOnly')}</span>}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <div className="band">
        <h2>{t('accessReview.signHeading')}</h2>
        <p className="muted">{t('accessReview.signIntro')}</p>

        <label htmlFor="access-note">{t('accessReview.note')}</label>
        <input
          id="access-note"
          type="text"
          value={note}
          onChange={(e) => setNote(e.target.value)}
          placeholder={t('accessReview.noteHint')}
        />

        <button type="button" disabled={busy} onClick={() => void sign()}>
          {busy ? t('common.saving') : t('accessReview.sign')}
        </button>
      </div>
    </section>
  );
}
