// DocumentEndpoints — the paperwork, as a page a browser can print.
//
// Use:  GET /api/v1/documents/deals/{id}
//       GET /api/v1/documents/repair-orders/{id}
// Edit: these return HTML rather than JSON, which makes them the only endpoints
//       here that do. That is the point: the browser prints the response, and
//       nothing has to be assembled client-side from data that could drift from
//       what the server holds.
//
//       No permission is checked here. It does not need to be — every document
//       is built by reading through IDeals or IRepairOrders, which apply the
//       caller's permissions and rooftop scope before returning anything. A
//       second check here would be a second place to get it wrong.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using DealerFOSS.App;

namespace DealerFOSS.Documents;

internal static class DocumentEndpoints
{
    public static void MapDocuments(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/documents").WithTags("Documents");

        group.MapGet("/deals/{dealId:guid}", DealSummaryAsync);
        group.MapGet("/repair-orders/{repairOrderId:guid}", ServiceInvoiceAsync);
    }

    private static async Task<IResult> DealSummaryAsync(
        Guid dealId,
        IDocuments documents,
        CancellationToken cancellationToken)
    {
        var result = await documents.DealSummaryAsync(dealId, cancellationToken);
        return Render(result);
    }

    private static async Task<IResult> ServiceInvoiceAsync(
        Guid repairOrderId,
        IDocuments documents,
        CancellationToken cancellationToken)
    {
        var result = await documents.ServiceInvoiceAsync(repairOrderId, cancellationToken);
        return Render(result);
    }

    private static IResult Render(Core.Result<RenderedDocument> result)
    {
        if (result.IsFailure)
        {
            // A refusal is still JSON. The caller asked for a document and did not
            // get one, and a problem document would be a strange thing to print.
            return result.Error.ToProblem();
        }

        return Results.Content(result.Value.Content, result.Value.ContentType);
    }
}
