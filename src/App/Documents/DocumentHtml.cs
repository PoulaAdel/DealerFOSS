// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   DocumentHtml — the print stylesheet and the small helpers every document uses.
//
// Usage:
//   DocumentHtml.Page(title, body) wraps a document body in a complete,
//   standalone HTML page.
//
// Coding Instructions:
//   A PDF library was deliberately not taken (maintainer, 2026-08-06). This
//   is an AGPL project and `NuGetAudit` fails the build on a vulnerable
//   package, so a rendering component would have to clear both a licence
//   review and an advisory history. A printable page needs neither, and the
//   browser's own print-to-PDF produces the customer's copy.
//
//   Everything is inlined — no stylesheet link, no script, no font, no image.
//   A document has to survive being saved to disk and opened next year, and
//   anything fetched at open time will not be there.
//
//   Escape EVERY value with Text(). A customer called "Bob & Sons <Motors>"
//   is ordinary, and an unescaped one produces a broken document rather than
//   an interesting exploit — but broken is bad enough, and the habit is what
//   stops the interesting version arriving later.

using System.Globalization;
using System.Net;
using System.Text;

namespace DealerFOSS.Documents;

internal static class DocumentHtml
{
    /// <summary>
    /// Wraps a body in a complete page. `@page` sets a real paper margin, and the
    /// screen rules keep it legible before anybody prints — most people read it
    /// on screen and print it rarely.
    /// </summary>
    public static string Page(string title, string body) =>
        "<!doctype html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n"
        + "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n"
        + $"<title>{Text(title)}</title>\n<style>\n{Stylesheet}\n</style>\n</head>\n<body>\n"
        + "<p class=\"screen-only\">Use your browser's print command to produce a copy for the "
        + "customer, or save it as a PDF.</p>\n"
        + body
        + "\n</body>\n</html>";

    /// <summary>
    /// Kept out of the interpolated string above: CSS is mostly braces, and
    /// escaping every one of them turns a readable stylesheet into a puzzle.
    /// </summary>
    private const string Stylesheet = """
        @page { size: A4; margin: 18mm 16mm; }
        * { box-sizing: border-box; }
        body {
          font: 12pt/1.45 "Segoe UI", system-ui, -apple-system, sans-serif;
          color: #111; background: #fff; margin: 0 auto; padding: 24px; max-width: 210mm;
        }
        h1 { font-size: 20pt; margin: 0 0 2px; }
        h2 { font-size: 13pt; margin: 22px 0 6px; border-bottom: 1px solid #ccc; padding-bottom: 3px; }
        .head { display: flex; justify-content: space-between; gap: 24px; align-items: flex-start; }
        .muted { color: #555; }
        .who p { margin: 1px 0; }
        table { width: 100%; border-collapse: collapse; margin-top: 6px; }
        th, td { text-align: left; padding: 5px 6px; border-bottom: 1px solid #e3e3e3; vertical-align: top; }
        th { font-size: 9pt; text-transform: uppercase; letter-spacing: .04em; color: #555; }
        /* `figure` looks exactly like `num` and is a different class on purpose.
           DocumentTests.The_printed_order_adds_up_to_its_own_total reads every
           `num` cell in the document and asserts the sum reaches the printed
           total, so that class means "this belongs in the column that adds up".
           A finance figure does not — the down payment is how the customer pays,
           not a reduction in what they owe — and printing one as `num` would
           break a test that exists to catch four real defects. */
        .num, .figure { text-align: right; font-variant-numeric: tabular-nums; white-space: nowrap; }
        tfoot td { font-weight: 600; border-top: 2px solid #111; border-bottom: none; }
        .total { font-size: 14pt; }
        .note { margin-top: 18px; font-size: 10pt; color: #444; }
        /* On the record, not on this bill: work that was declined, and work
           somebody other than the customer is paying for. Both carry a word
           where an amount would go, so neither joins the column that adds up
           to the total. */
        .declined, .unbilled { color: #666; }
        /* Kept off paper: on screen it explains how to produce the customer's
           copy, and on paper it would be a line of instructions nobody needs. */
        .screen-only {
          margin: 0 0 18px; padding: 8px 12px; background: #f4f4f5;
          border: 1px solid #ddd; border-radius: 4px; font-size: 10pt;
        }
        @media print {
          body { padding: 0; max-width: none; }
          .screen-only { display: none; }
          /* A table split across a page break loses its headings, which turns
             page two into a column of unlabelled numbers. */
          table { page-break-inside: avoid; }
          thead { display: table-header-group; }
        }
        """;

    /// <summary>HTML-escapes a value. Every interpolated string goes through this.</summary>
    public static string Text(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    /// <summary>
    /// Formats money in the document's own currency. Invariant culture, because a
    /// document is a record of what was agreed and must read the same wherever it
    /// is later opened — not follow the reader's machine settings.
    /// </summary>
    public static string Money(decimal amount, string currency)
    {
        var symbol = currency switch
        {
            "USD" => "$",
            "GBP" => "£",
            "EUR" => "€",
            _ => string.Empty,
        };

        // The sign goes outside the symbol. Formatting the whole amount first
        // produced "$-3,000.00" for a trade-in, which reads as a typo rather than
        // as money off.
        var sign = amount < 0 ? "-" : string.Empty;
        var formatted = Math.Abs(amount).ToString("N2", CultureInfo.InvariantCulture);

        return symbol.Length > 0
            ? $"{sign}{symbol}{formatted}"
            : $"{sign}{formatted} {currency}";
    }

    public static string Date(DateTimeOffset value) =>
        value.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>The dealership block at the top of every document.</summary>
    public static string Header(string title, string reference, string organization, string rooftop, DateTimeOffset date)
    {
        var builder = new StringBuilder();

        builder.Append("<div class=\"head\"><div>");
        builder.Append(CultureInfo.InvariantCulture, $"<h1>{Text(title)}</h1>");
        builder.Append(CultureInfo.InvariantCulture, $"<p class=\"muted\">{Text(reference)} · {Text(Date(date))}</p>");
        builder.Append("</div><div class=\"who\" style=\"text-align:right\">");
        builder.Append(CultureInfo.InvariantCulture, $"<p><strong>{Text(organization)}</strong></p>");
        builder.Append(CultureInfo.InvariantCulture, $"<p class=\"muted\">{Text(rooftop)}</p>");
        builder.Append("</div></div>");

        return builder.ToString();
    }
}
