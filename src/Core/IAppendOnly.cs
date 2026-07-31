// IAppendOnly — marks a record that may be written once and never changed.
//
// Use:  implement it on a history or evidence row. The tenant data context
//       refuses to update or delete anything carrying this marker, so the rule is
//       enforced centrally rather than remembered at each call site.
// Edit: this is the mechanism behind ADR-016. Adding the marker to a type is what
//       protects it; leaving it off a new history table is a silent hole, which
//       is exactly why the guard keys off an interface rather than a list of
//       types someone has to maintain.

namespace OpenDealer360.Core;

/// <summary>
/// A record that is appended and never rewritten (ADR-016). Corrections are made
/// by appending the opposite entry with a reason — status history, audit events,
/// and posted financial entries all work this way.
/// </summary>
public interface IAppendOnly;
