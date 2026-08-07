// ReportingEndpoints — the HTTP surface for the dashboard.
//
// Use:  mapped from Program.cs; routes under /api/v1/reporting.
//       GET /api/v1/reporting/month?year=&month=&rooftopId=
// Edit: one route, on purpose. The screen it feeds is a single view, and a
//       dashboard assembled from four calls is a dashboard that renders in four
//       stages — each one a chance to show a figure next to a stale one.
//
//       Year and month default to today's, so the common case is a bare GET.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using DealerFOSS.App;
using DealerFOSS.Core;

namespace DealerFOSS.Reporting;

internal static class ReportingEndpoints
{
    public static void MapReporting(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/reporting").WithTags("Reporting");

        group.MapGet("/month", MonthAsync);
    }

    private static async Task<IResult> MonthAsync(
        IReporting reporting,
        IClock clock,
        CancellationToken cancellationToken,
        int? year = null,
        int? month = null,
        Guid? rooftopId = null)
    {
        var today = clock.UtcNow.UtcDateTime;

        var query = new MonthQuery(
            year ?? today.Year,
            month ?? today.Month,
            rooftopId is null ? null : new RooftopId(rooftopId.Value));

        var result = await reporting.MonthAsync(query, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }
}
