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
        HttpContext context,
        IDocuments documents,
        CancellationToken cancellationToken)
    {
        var result = await documents.DealSummaryAsync(dealId, cancellationToken);
        return Render(context, result);
    }

    private static async Task<IResult> ServiceInvoiceAsync(
        Guid repairOrderId,
        HttpContext context,
        IDocuments documents,
        CancellationToken cancellationToken)
    {
        var result = await documents.ServiceInvoiceAsync(repairOrderId, cancellationToken);
        return Render(context, result);
    }

    private static IResult Render(HttpContext context, Core.Result<RenderedDocument> result)
    {
        if (result.IsFailure)
        {
            // A refusal is still JSON. The caller asked for a document and did not
            // get one, and a problem document would be a strange thing to print.
            return result.Error.ToProblem();
        }

        // The filename the service works out was being computed and thrown away,
        // so a saved document was named after its URL — a bare GUID for a deal,
        // which is what the customer's copy would have been filed as.
        //
        // `inline`, not `attachment`: the browser prints it, which is the whole
        // point. The name is only used if somebody chooses Save.
        context.Response.Headers.ContentDisposition =
            $"inline; filename=\"{SafeFileName(result.Value.FileName)}\"";

        return Results.Content(result.Value.Content, result.Value.ContentType);
    }

    /// <summary>
    /// Keeps a repair-order number fit to appear inside a quoted header value.
    /// A quote or a newline in there is header injection, and the number is
    /// dealership-supplied text rather than something this code chose.
    /// </summary>
    private static string SafeFileName(string name)
    {
        var cleaned = new string([.. name.Where(c => !char.IsControl(c) && c is not ('"' or '\\'))]);
        return cleaned.Length == 0 ? "document.html" : cleaned;
    }
}
