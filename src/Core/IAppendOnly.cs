// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   A marker saying a record may be written once and never changed. It carries
//   no members: the whole design is that the tenant data context checks for the
//   INTERFACE in SaveChangesAsync and refuses any update or delete, so the rule
//   is enforced in one place rather than remembered at every call site.
//
//   Keying the guard off an interface rather than a list of type names is the
//   engineering decision that matters. A list is something somebody has to
//   maintain, and the failure mode of forgetting is silent — history quietly
//   becomes editable. Implementing an interface is something the compiler and
//   the reviewer both see.
//
// Usage:
//   Implement it on a history, ledger or evidence row. Nothing else is needed;
//   the protection is automatic from that moment.
//
// Coding Instructions:
//   This is the mechanism behind ADR-016. Adding the marker to a type is what
//   protects it, so LEAVING IT OFF a new history table is a silent hole.
//   Corrections to append-only data are new reversing rows, never edits.

namespace DealerFOSS.Core;

/// <summary>
/// A record that is appended and never rewritten (ADR-016). Corrections are made
/// by appending the opposite entry with a reason — status history, audit events,
/// and posted financial entries all work this way.
/// </summary>
public interface IAppendOnly;
