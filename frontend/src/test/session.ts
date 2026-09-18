// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   session fixtures — what /auth/me answers for a test.
//
// Usage:
//   mockApi({ '/auth/me': signedInAs(), ... })                 // holds everything
//   mockApi({ '/auth/me': signedInAs([Permission.ServiceRead]) }) // a technician
//
// Coding Instructions:
//   This exists because on 2026-09-18 the session started carrying the
//   caller's permissions and the navigation started believing it. Seven
//   fixtures said `{ userId, mustEnrolSecondFactor }` and nothing else, which
//   the browser now reads as "this person holds nothing" — so three tests
//   failed with an empty navigation bar, correctly.
//
//   Writing the array out at each call site would have fixed those three and
//   left the next fixture to make the same mistake. One helper, one default.
//
//   THE DEFAULT IS EVERYTHING, on purpose. A test about the deal desk should
//   not have to know which permission the deal desk needs; it should fail when
//   the deal desk breaks, not when somebody renames a permission. A test that
//   is actually ABOUT what a role can see passes the list it means.

import { Permission } from '../shared/permissions';

/** Every permission the navigation knows how to ask about. */
export const allPermissions: string[] = Object.values(Permission);

/**
 * A signed-in reply for `mockApi`, holding `permissions` — everything by
 * default.
 */
export function signedInAs(permissions: readonly string[] = allPermissions) {
  return {
    ok: true as const,
    body: {
      userId: 'u1',
      mustEnrolSecondFactor: false,
      permissions: [...permissions],
    },
  };
}
