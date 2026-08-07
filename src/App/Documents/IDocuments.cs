// IDocuments — the paperwork a customer is handed.
//
// Use:  inject IDocuments and ask for a deal summary or a service invoice. The
//       result is a complete, standalone HTML page.
// Edit: this capability OWNS NO DATA. It reads through IDeals, IRepairOrders,
//       ICustomers, and IOrganization, which means the caller's permissions and
//       rooftop scope are already applied by the time anything is rendered — a
//       document cannot show what the person asking could not have read anyway.
//
//       The output type is deliberately a record rather than a raw string. When
//       a PDF renderer eventually replaces the HTML, ContentType and FileName
//       change and no caller does.

using DealerFOSS.Core;

namespace DealerFOSS.Documents;

public interface IDocuments
{
    /// <summary>
    /// What the customer signed for: the car, the charges, the trade-in, and
    /// anything sold alongside. Never the dealership's cost or gross.
    /// </summary>
    Task<Result<RenderedDocument>> DealSummaryAsync(Guid dealId, CancellationToken cancellationToken);

    /// <summary>
    /// The bill for a job: what was done, what was declined, and what is owed.
    /// </summary>
    Task<Result<RenderedDocument>> ServiceInvoiceAsync(Guid repairOrderId, CancellationToken cancellationToken);
}

/// <summary>
/// A finished document. <paramref name="ContentType"/> and
/// <paramref name="FileName"/> are here so swapping HTML for a PDF later changes
/// this capability and nothing else.
/// </summary>
public sealed record RenderedDocument(string Content, string ContentType, string FileName);
