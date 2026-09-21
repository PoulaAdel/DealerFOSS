// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ImportOutcome — what happened to one record arriving from another
//   installation.
//
// Usage:
//   The return type of every capability's ImportAsync. DataMigration counts
//   them into the report a person reads after a package lands.
//
// Coding Instructions:
//   It lives in Core because five capabilities answer with it and none of them
//   may reference DataMigration — a capability importing a record must not
//   learn anything about the file it came out of.
//
//   THERE IS NO "UPDATED" VALUE AND THERE MUST NOT BE ONE. An id already in
//   the receiving database is answered with AlreadyPresent and left exactly as
//   it is. The dealership on this side may have corrected that record since the
//   package was produced, and a re-run that quietly reverted their correction
//   would be a far worse failure than a re-run that did nothing. Re-importing
//   the same package is therefore safe by construction, which is what makes
//   retrying after a partial failure an ordinary thing to do rather than a
//   decision somebody has to weigh.

namespace DealerFOSS.Core;

public enum ImportOutcome
{
    /// <summary>The record did not exist here and now does, with the same id.</summary>
    Created = 0,

    /// <summary>
    /// That id is already here. Nothing was read out of the incoming copy and
    /// nothing was written.
    /// </summary>
    AlreadyPresent = 1,
}
