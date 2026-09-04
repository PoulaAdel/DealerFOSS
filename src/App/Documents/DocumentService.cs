// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   DocumentService — turns a deal or a job into paperwork.
//
// Usage:
//   Through IDocuments.
//
// Coding Instructions:
//   Two rules here are not stylistic.
//
//   NOTHING THE CUSTOMER MUST NOT SEE IS RENDERED. DealDetail carries the
//   cost and gross of every F&I product, and RepairOrderDetail carries the
//   cost of every part. Neither appears here, and
//   DocumentTests.No_dealership_only_figure_reaches_a_customers_copy reads
//   the raw HTML to prove it. If a future field arrives on either detail, it
//   does not reach paper unless somebody comes here and writes it out.
//
//   A DOCUMENT IS A SNAPSHOT, and it is one because of where the data comes
//   from rather than anything done here. A deal's numbers freeze on
//   submission; a job's lines freeze on completion and its part costs freeze
//   at invoicing. Reprinting last month's invoice therefore gives last
//   month's figures. Do not "improve" this by recalculating anything.

using System.Globalization;
using DealerFOSS.Core;
using DealerFOSS.Customers;
using DealerFOSS.Deals;
using DealerFOSS.Organization;
using DealerFOSS.RepairOrders;
using System.Text;

namespace DealerFOSS.Documents;

public sealed class DocumentService(
    IDeals deals,
    IRepairOrders repairOrders,
    ICustomers customers,
    IOrganization organization)
    : IDocuments
{
    private readonly IDeals _deals = deals;
    private readonly IRepairOrders _repairOrders = repairOrders;
    private readonly ICustomers _customers = customers;
    private readonly IOrganization _organization = organization;

    public async Task<Result<RenderedDocument>> DealSummaryAsync(
        Guid dealId,
        CancellationToken cancellationToken)
    {
        // Read through the contract, so the caller's permissions and rooftop
        // scope have already decided whether this is allowed.
        var deal = await _deals.GetAsync(dealId, cancellationToken);
        if (deal.IsFailure)
        {
            return Result.Failure<RenderedDocument>(deal.Error);
        }

        var where = await WhereAsync(deal.Value.RooftopId, cancellationToken);
        var buyer = await BuyerAsync(deal.Value.CustomerId, cancellationToken);

        var body = new StringBuilder();

        body.Append(DocumentHtml.Header(
            "Vehicle order", $"Deal {deal.Value.Id}", where.Organization, where.Rooftop, DateTimeOffset.UtcNow));

        body.Append("<h2>Buyer</h2>");
        body.Append(buyer);

        body.Append("<h2>Vehicle</h2>");
        body.Append(CultureInfo.InvariantCulture, $"<p>{DocumentHtml.Text(deal.Value.Vehicle)}</p>");
        body.Append(CultureInfo.InvariantCulture, $"<p class=\"muted\">Stock {DocumentHtml.Text(deal.Value.StockNumber)}</p>");

        body.Append("<h2>What it comes to</h2>");
        body.Append("<table><thead><tr><th>Item</th><th class=\"num\">Amount</th></tr></thead><tbody>");

        foreach (var charge in deal.Value.Charges)
        {
            body.Append(
                CultureInfo.InvariantCulture,
                $"<tr><td>{DocumentHtml.Text(charge.Description)}</td>"
                + $"<td class=\"num\">{DocumentHtml.Text(DocumentHtml.Money(charge.Amount, deal.Value.Currency))}</td></tr>");
        }

        // Price only. Cost and gross are the dealership's business and are not
        // rendered — see the note at the top of this file.
        foreach (var product in deal.Value.Products)
        {
            var term = product.TermMonths is null ? string.Empty : $" ({product.TermMonths} months)";

            body.Append(
                CultureInfo.InvariantCulture,
                $"<tr><td>{DocumentHtml.Text(product.Name + term)}</td>"
                + $"<td class=\"num\">{DocumentHtml.Text(DocumentHtml.Money(product.Price, deal.Value.Currency))}</td></tr>");
        }

        if (deal.Value.TradeIn is { } trade)
        {
            // Negated, because this column is what the customer owes and a
            // trade-in reduces it. Negative equity correctly increases it. The
            // same rule the deal desk follows, and for the same reason: a column
            // somebody reads down has to reach the total printed under it.
            body.Append(
                CultureInfo.InvariantCulture,
                $"<tr><td>Trade-in — {DocumentHtml.Text(trade.Description)}</td>"
                + $"<td class=\"num\">{DocumentHtml.Text(DocumentHtml.Money(-trade.Equity, deal.Value.Currency))}</td></tr>");
        }

        body.Append("</tbody><tfoot><tr><td class=\"total\">Due from the customer</td>");
        body.Append(
            CultureInfo.InvariantCulture,
            $"<td class=\"num total\">{DocumentHtml.Text(DocumentHtml.Money(deal.Value.AmountDue, deal.Value.Currency))}</td>");
        body.Append("</tr></tfoot></table>");

        body.Append(
            "<p class=\"note\">This is a summary of what was agreed. It is not a tax invoice "
            + "and does not include any finance agreement.</p>");

        return Result.Success(new RenderedDocument(
            DocumentHtml.Page($"Vehicle order — {deal.Value.CustomerName}", body.ToString()),
            "text/html; charset=utf-8",
            $"deal-{deal.Value.Id}.html"));
    }

    public async Task<Result<RenderedDocument>> ServiceInvoiceAsync(
        Guid repairOrderId,
        CancellationToken cancellationToken)
    {
        var job = await _repairOrders.GetAsync(repairOrderId, cancellationToken);
        if (job.IsFailure)
        {
            return Result.Failure<RenderedDocument>(job.Error);
        }

        var where = await WhereAsync(job.Value.RooftopId, cancellationToken);
        var customer = await BuyerAsync(job.Value.CustomerId, cancellationToken);

        var body = new StringBuilder();

        // An invoiced job is dated when it was invoiced. Anything earlier has no
        // invoice date, and stamping today's on a draft would produce a document
        // that disagrees with itself once it is finally billed.
        var dated = job.Value.InvoicedAt ?? job.Value.OpenedAt;
        var title = job.Value.InvoicedAt is null ? "Job sheet" : "Service invoice";

        body.Append(DocumentHtml.Header(title, job.Value.Number, where.Organization, where.Rooftop, dated));

        if (job.Value.InvoicedAt is null)
        {
            body.Append(
                "<p class=\"note\"><strong>Not yet invoiced.</strong> This is the work as it "
                + "stands, not a bill.</p>");
        }

        body.Append("<h2>Customer</h2>");
        body.Append(customer);

        body.Append("<h2>Vehicle</h2>");
        body.Append(CultureInfo.InvariantCulture, $"<p>{DocumentHtml.Text(job.Value.Vehicle)}</p>");

        if (job.Value.OdometerReading is { } miles)
        {
            body.Append(CultureInfo.InvariantCulture, $"<p class=\"muted\">{miles:N0} miles</p>");
        }

        body.Append(CultureInfo.InvariantCulture, $"<h2>Reported</h2><p>{DocumentHtml.Text(job.Value.Complaint)}</p>");

        body.Append("<h2>Work done</h2>");
        body.Append("<table><thead><tr><th>Description</th><th>Detail</th><th class=\"num\">Amount</th></tr></thead><tbody>");

        foreach (var line in job.Value.Lines)
        {
            var declined = line.Authorization == "Declined";

            var detail = line.Hours is { } hours && line.Rate is { } rate
                ? $"{hours} h at {DocumentHtml.Money(rate, job.Value.Currency)}"
                : line.Kind;

            // Declined work stays on the record at nothing. A customer who said no
            // in March and comes back in September with the same fault should be
            // able to see they were told — that is the whole reason it is printed.
            var amount = declined
                ? "declined"
                : DocumentHtml.Money(line.Amount, job.Value.Currency);

            body.Append(CultureInfo.InvariantCulture, $"<tr class=\"{(declined ? "declined" : string.Empty)}\">");
            body.Append(CultureInfo.InvariantCulture, $"<td>{DocumentHtml.Text(line.Description)}</td>");
            body.Append(CultureInfo.InvariantCulture, $"<td class=\"muted\">{DocumentHtml.Text(detail)}</td>");
            body.Append(CultureInfo.InvariantCulture, $"<td class=\"num\">{DocumentHtml.Text(amount)}</td></tr>");
        }

        body.Append("</tbody></table>");

        body.Append("<h2>Totals</h2><table><tbody>");
        Total("Labour", job.Value.LabourTotal);
        Total("Parts", job.Value.PartsTotal);
        Total("Sent out", job.Value.SubletTotal);
        body.Append("</tbody><tfoot><tr><td class=\"total\">Total due</td>");
        body.Append(
            CultureInfo.InvariantCulture,
            $"<td class=\"num total\">{DocumentHtml.Text(DocumentHtml.Money(job.Value.AmountDue, job.Value.Currency))}</td>");
        body.Append("</tr></tfoot></table>");

        void Total(string label, decimal amount) =>
            body.Append(
                CultureInfo.InvariantCulture,
                $"<tr><td>{label}</td><td class=\"num\">"
                + $"{DocumentHtml.Text(DocumentHtml.Money(amount, job.Value.Currency))}</td></tr>");

        return Result.Success(new RenderedDocument(
            DocumentHtml.Page($"{title} {job.Value.Number}", body.ToString()),
            "text/html; charset=utf-8",
            $"{job.Value.Number}.html"));
    }

    /// <summary>
    /// The dealership and the location, for the top of the document. A failure to
    /// read either is not worth refusing a document over — the paperwork is still
    /// correct about the money, which is what it is for.
    /// </summary>
    private async Task<(string Organization, string Rooftop)> WhereAsync(
        RooftopId rooftopId,
        CancellationToken cancellationToken)
    {
        var org = await _organization.GetStructureAsync(cancellationToken);
        var rooftop = await _organization.GetRooftopAsync(rooftopId, cancellationToken);

        return (
            org.IsSuccess ? org.Value.Name : string.Empty,
            rooftop.IsSuccess ? $"{rooftop.Value.Name} ({rooftop.Value.Code})" : string.Empty);
    }

    /// <summary>
    /// The customer block. Falls back to nothing rather than failing: a document
    /// missing an address is still a usable document, and one that refuses to
    /// render is not.
    /// </summary>
    private async Task<string> BuyerAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await _customers.GetAsync(customerId, cancellationToken);
        if (customer.IsFailure)
        {
            return "<p class=\"muted\">(customer details unavailable)</p>";
        }

        var builder = new StringBuilder("<div class=\"who\">");
        builder.Append(CultureInfo.InvariantCulture, $"<p><strong>{DocumentHtml.Text(customer.Value.DisplayName)}</strong></p>");

        if (customer.Value.Address is { } address)
        {
            foreach (var line in new[] { address.Line1, address.Line2, address.City, address.PostalCode })
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    builder.Append(CultureInfo.InvariantCulture, $"<p class=\"muted\">{DocumentHtml.Text(line)}</p>");
                }
            }
        }

        // The primary contact if there is one, otherwise whichever is first.
        var points = customer.Value.ContactPoints;
        var contact = points.FirstOrDefault(c => c.IsPrimary)
            ?? (points.Count > 0 ? points[0] : null);

        if (contact is not null)
        {
            builder.Append(CultureInfo.InvariantCulture, $"<p class=\"muted\">{DocumentHtml.Text(contact.Value)}</p>");
        }

        builder.Append("</div>");
        return builder.ToString();
    }
}
