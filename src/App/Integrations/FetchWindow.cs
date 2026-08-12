// FetchWindow — window arithmetic as data, and the rule that keeps a cursor honest.
//
// Use:  FetchWindowPolicy describes what a provider's endpoint will actually
//       accept. Plan() turns a wanted range into the slices that endpoint will
//       serve. Cursor.Advance() moves the cursor from what came *back*.
// Edit: the one invariant worth defending is that nothing here ever returns the
//       requested range as if it were the served range. Every field that could
//       be confused for "what we asked for" is named for what it is.
//
//       Why this exists: real endpoints cap a request at a fixed span, refuse
//       anything older than a few days, or take no dates at all and decide
//       "recent" for themselves. A cursor advanced across a period the provider
//       never served produces no error and no missing-record warning — the hole
//       is found by a reconciliation months later, when the source no longer
//       has the data.

using DealerFOSS.Core;

namespace DealerFOSS.Integrations;

/// <summary>A closed period. <see cref="Start"/> is inclusive, <see cref="End"/> exclusive.</summary>
public readonly record struct DateRange
{
    public DateRange(DateTimeOffset start, DateTimeOffset end)
    {
        if (end < start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "A range cannot end before it starts.");
        }

        Start = start;
        End = end;
    }

    public DateTimeOffset Start { get; }

    public DateTimeOffset End { get; }

    public TimeSpan Duration => End - Start;

    public override string ToString() => $"{Start:O}..{End:O}";
}

/// <summary>
/// What one provider endpoint will actually accept. Declared per connector and
/// capability rather than assumed, because no two providers agree.
/// </summary>
/// <param name="AcceptsDates">
/// False for endpoints that take no date parameters at all and decide "recent"
/// for themselves. Such an endpoint can never justify moving a cursor on its
/// own, because we cannot know what it decided.
/// </param>
/// <param name="MaximumChunk">
/// The longest span one request may cover; longer wants are split. Null means
/// the endpoint has no limit.
/// </param>
/// <param name="MaximumLookback">
/// How far back the endpoint will go at all. A want that starts earlier is
/// clamped forward — and the clamp is reported, not silent.
/// </param>
/// <param name="SettlementDelay">
/// How long this dealership takes to post its paperwork. Shifts the whole
/// window back, because a same-day request against a slow store returns
/// nothing. Per dealership, not per provider: stores on the same DMS differ.
/// </param>
public sealed record FetchWindowPolicy(
    bool AcceptsDates,
    TimeSpan? MaximumChunk,
    TimeSpan? MaximumLookback,
    TimeSpan SettlementDelay)
{
    /// <summary>An endpoint with no limits — useful for fixtures and simple providers.</summary>
    public static FetchWindowPolicy Unlimited { get; } = new(true, null, null, TimeSpan.Zero);
}

/// <summary>
/// One request the connector will make. <see cref="Range"/> is null when the
/// endpoint takes no dates, which is the honest representation of "the provider
/// will decide".
/// </summary>
public sealed record FetchSlice(DateRange? Range);

/// <summary>
/// The slices a wanted range becomes, and what had to be given up to get there.
/// </summary>
/// <param name="Slices">The requests to make, in order.</param>
/// <param name="Wanted">The range asked for, before any clamping.</param>
/// <param name="ClampedTo">
/// The range that will actually be requested once the lookback limit and
/// settlement delay are applied, or null when the endpoint takes no dates.
/// This is still <em>requested</em>, not served — see <see cref="Cursor"/>.
/// </param>
public sealed record FetchPlan(IReadOnlyList<FetchSlice> Slices, DateRange Wanted, DateRange? ClampedTo)
{
    /// <summary>True when the provider will not go as far back as we wanted.</summary>
    public bool WasClamped => ClampedTo is { } clamped && clamped.Start > Wanted.Start;
}

/// <summary>Turns a wanted range into the requests a given endpoint will accept.</summary>
public static class FetchWindow
{
    /// <summary>
    /// Plan the requests for <paramref name="wanted"/> under
    /// <paramref name="policy"/>, as at <paramref name="now"/>.
    /// </summary>
    public static FetchPlan Plan(FetchWindowPolicy policy, DateRange wanted, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (!policy.AcceptsDates)
        {
            // The endpoint decides for itself. Sending one request is right;
            // pretending we know what period it covered is not.
            return new FetchPlan([new FetchSlice(null)], wanted, null);
        }

        var end = wanted.End - policy.SettlementDelay;
        var start = wanted.Start - policy.SettlementDelay;

        if (policy.MaximumLookback is { } lookback)
        {
            var earliest = now - lookback;
            if (start < earliest)
            {
                start = earliest;
            }

            if (end < earliest)
            {
                end = earliest;
            }
        }

        if (end <= start)
        {
            // Nothing left to ask for once the limits are applied. An empty plan
            // is a legitimate outcome and must not be confused with a failure.
            var empty = new DateRange(start, start);
            return new FetchPlan([], wanted, empty);
        }

        var clamped = new DateRange(start, end);
        var slices = new List<FetchSlice>();

        if (policy.MaximumChunk is { } chunk && chunk > TimeSpan.Zero)
        {
            var cursor = start;
            while (cursor < end)
            {
                var sliceEnd = cursor + chunk;
                if (sliceEnd > end)
                {
                    sliceEnd = end;
                }

                slices.Add(new FetchSlice(new DateRange(cursor, sliceEnd)));
                cursor = sliceEnd;
            }
        }
        else
        {
            slices.Add(new FetchSlice(clamped));
        }

        return new FetchPlan(slices, wanted, clamped);
    }
}

/// <summary>
/// Moves a poll cursor. The only input that may move it is what the provider
/// reported serving — never what was asked for.
/// </summary>
public static class Cursor
{
    /// <summary>
    /// Advance <paramref name="current"/> to the end of <paramref name="covered"/>.
    /// </summary>
    /// <param name="current">Where the cursor is now.</param>
    /// <param name="covered">
    /// The range the provider reported actually serving, or null if it did not
    /// say. Null is common and is not a bug — it is the reason this method
    /// exists rather than an assignment at the call site.
    /// </param>
    public static Result<DateTimeOffset> Advance(DateTimeOffset current, DateRange? covered)
    {
        if (covered is not { } served)
        {
            return Result.Failure<DateTimeOffset>(IntegrationErrors.CoverageUnknown);
        }

        if (served.Start > current)
        {
            // Everything between the cursor and the start of what came back was
            // never fetched. Moving the cursor to served.End would make that
            // period unreachable and produce no error anywhere.
            return Result.Failure<DateTimeOffset>(IntegrationErrors.CoverageGap);
        }

        // A provider may serve less than asked for, and that is fine — the
        // cursor simply moves less far and the rest is asked for again.
        return Result.Success(served.End > current ? served.End : current);
    }
}
