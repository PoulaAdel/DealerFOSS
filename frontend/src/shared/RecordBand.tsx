// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   RecordBand — what the detail band shows while a record is arriving, and
//   when it will never arrive.
//
// Usage:
//   <RecordBandStatus route={job} />
//   {job.state.kind === 'open' ? <JobDetail job={job.state.record} /> : null}
//
// Coding Instructions:
//   This exists so five screens cannot drift into five different answers to
//   "that link does not work". They are the two states a detail band only
//   acquired once a record could be reached by URL: before, a band was opened
//   by clicking a row that was already on screen, so the record was known to
//   exist and known to be readable.
//
//   ONE SENTENCE FOR EVERY REASON, and do not be tempted to improve on it. A
//   record that was deleted, a record that never existed, and a record in a
//   rooftop the reader may not see must read identically. The server already
//   refuses to tell those apart for scoped records — see the "Unknown and
//   unauthorized answer identically" comments in InventoryService, LeadService,
//   DealService and RepairOrderService — and a helpful screen that said "that
//   repair order belongs to another branch" would hand back the fact the server
//   just withheld, one guessed id at a time.

import { useI18n } from './i18n';
import type { RecordRoute } from './useRecordRoute';

export function RecordBandStatus<T>({ route }: { route: RecordRoute<T> }) {
  const { t } = useI18n();

  if (route.state.kind === 'opening') {
    // `status` and not `alert`: a record on its way is not a problem, and an
    // assertive announcement on every row click would talk over the reader.
    return (
      <p className="state" role="status">
        {t('record.opening')}
      </p>
    );
  }

  if (route.state.kind === 'unreachable') {
    return (
      <div className="state" role="alert">
        <p>{t('record.unreachable')}</p>
        {/* A way out that does not require knowing the URL scheme. Somebody who
            followed a dead link from a colleague is the likeliest reader here,
            and the list is what they wanted. */}
        <button type="button" onClick={route.close}>
          {t('record.backToList')}
        </button>
      </div>
    );
  }

  return null;
}
