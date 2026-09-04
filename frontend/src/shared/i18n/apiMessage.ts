// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   apiMessage — turning a refusal from the server into a sentence the reader's
//   language can carry.
//
// Usage:
//   const describe = useApiMessage(); ... catch (e) { setError(describe(e)) }
//
// Coding Instructions:
//   Read what this deliberately does NOT do.
//
//   The API answers with a stable `code` and a human `detail`, and the
//   detail is written in English by the server. Translating ALL ~100 of
//   them would mean either shipping the whole API vocabulary into the
//   browser — where it goes stale the first time a message is reworded —
//   or teaching the server five languages, which is a different piece of
//   work with a different owner (it would also have to translate the
//   printed documents, which are records rather than UI).
//
//   So the split is by WHO the message is about. Codes about the READER —
//   your session ended, you may not see this, the server is unreachable —
//   appear on every screen in the application and are translated here.
//   Codes about the DATA — "a deal's terms are frozen once it is
//   submitted" — keep the server's wording, which is precise, contextual,
//   and the only copy of that sentence anywhere.
//
//   An untranslated detail is therefore English text inside an otherwise
//   Arabic screen. That is a known and named gap, not an oversight: it is
//   recorded in docs/implementation/STATUS.md, and closing it is a backend
//   change. Showing the server's accurate English beats showing a vague
//   translated placeholder that loses which rule was broken.

import { useCallback } from 'react';
import { ApiError } from '../api';
import { useI18n, type MessageKey } from './index';

/**
 * Codes whose meaning is about the caller rather than about the record they
 * touched. Both worlds appear — a browser holds a dealership session and an
 * administrator one at the same time during a support visit.
 */
const TRANSLATED: Readonly<Record<string, MessageKey>> = {
  network: 'error.network',

  'auth.invalid_credentials': 'error.invalidCredentials',
  'admin.invalid_credentials': 'error.invalidCredentials',

  'auth.session_required': 'error.sessionRequired',
  'admin.session_required': 'error.adminSessionRequired',

  'auth.session_invalid': 'error.sessionInvalid',
  'admin.session_invalid': 'error.sessionInvalid',

  'auth.antiforgery_failed': 'error.antiForgeryFailed',
  'admin.antiforgery_failed': 'error.antiForgeryFailed',

  'auth.second_factor_rejected': 'error.secondFactorRejected',
  'admin.second_factor_rejected': 'error.secondFactorRejected',

  'auth.second_factor_required': 'error.secondFactorRequired',
  'admin.second_factor_required': 'error.secondFactorRequired',

  'auth.mfa_not_enrolled': 'error.mfaNotEnrolled',
  'auth.mfa_already_on': 'error.mfaAlreadyOn',
  'admin.mfa_already_on': 'error.mfaAlreadyOn',

  'admin.not_a_tenant_caller': 'error.notATenantCaller',

  tenant_required: 'error.tenantRequired',
  tenant_not_found: 'error.tenantNotFound',

  // Not a server code at all — the records screen raises this itself when it
  // gives up waiting on a background import. It is here because it is a
  // sentence the reader sees, and the client is the only thing that can
  // translate its own message.
  'migration.timeout': 'records.timeout',
};

/**
 * Describes any thrown value as something worth showing a person.
 *
 * A bare 403 with no mapped code falls back to the server's own detail, which
 * for a permission answer is already phrased for the reader.
 */
export function useApiMessage(): (failure: unknown) => string {
  const { t } = useI18n();

  return useCallback(
    (failure: unknown) => {
      if (!(failure instanceof ApiError)) {
        return t('common.unexpected');
      }

      const key = TRANSLATED[failure.code];
      if (key !== undefined) {
        return t(key);
      }

      // Deliberately NOT a catch-all for `*.forbidden`. Mapping every one of
      // them to a single "you do not have permission to see this" looked tidy
      // and lost real information: `customers.forbidden` raised while ADDING
      // somebody means "you cannot add customers", and the generic sentence
      // says the opposite of what happened. The API phrases each refusal for
      // the situation it came from, and that is worth more than a uniform
      // translation of a sentence that would then be wrong.
      //
      // The cost is honest and recorded: on a non-English screen these arrive
      // in English. Fixing that properly means the server learning the reader's
      // language, which is a backend change.
      return failure.message === '' ? t('common.unexpected') : failure.message;
    },
    [t],
  );
}
