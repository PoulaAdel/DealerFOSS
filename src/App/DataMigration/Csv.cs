// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Csv — reading the file a dealership actually sends.
//
// Usage:
//   Csv.Read(text) yields a header and then one CsvRow per line.
//
// Coding Instructions:
//   This follows RFC 4180 and is deliberately not a dependency. What arrives
//   from a dealership's old system is rarely clean, and the three things that
//   break naive splitting are all handled here: a comma inside quotes, a
//   doubled quote meaning one literal quote, and a newline inside a quoted
//   field. Anything stranger is reported as a bad row rather than guessed at.
//
//   Two decisions worth keeping. A row's raw text is preserved exactly as it
//   arrived, because the migration workflow forbids editing the source to fix
//   an exception (doc 05 §6). And a row with the wrong number of fields is a
//   failed row, not a truncated one — silently padding it would import a
//   customer with somebody else's phone number in the address column.

using System.Globalization;
using System.Text;

namespace DealerFOSS.DataMigration;

/// <summary>One parsed line, with the text it came from.</summary>
internal sealed record CsvRow(int Number, string Raw, IReadOnlyList<string> Fields);

/// <summary>
/// A parsed file: its column names, the header line exactly as it arrived, and
/// its data rows.
/// </summary>
/// <remarks>
/// <see cref="HeaderLine"/> is kept alongside the parsed <see cref="Header"/>
/// because the staged copy has to reconstruct the original document exactly, and
/// re-joining normalized column names would not.
/// </remarks>
internal sealed record CsvDocument(
    IReadOnlyList<string> Header,
    string HeaderLine,
    IReadOnlyList<CsvRow> Rows);

internal static class Csv
{
    /// <summary>
    /// Splits a document into a header row and its data rows. The header is
    /// returned separately because it names the columns and is not data.
    /// </summary>
    /// <remarks>
    /// Returns an empty header when the document has no content at all, which the
    /// caller reports as an empty file rather than treating as zero rows — an
    /// import that silently succeeds having done nothing is worse than an error.
    /// </remarks>
    public static CsvDocument Read(string text)
    {
        var lines = SplitRecords(text);
        if (lines.Count == 0)
        {
            return new CsvDocument([], string.Empty, []);
        }

        var header = ParseFields(lines[0]).Select(Normalize).ToList();
        var rows = new List<CsvRow>(lines.Count - 1);

        for (var i = 1; i < lines.Count; i++)
        {
            var raw = lines[i];

            // A blank line in the middle of an export is noise, not a record.
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            // Numbered as a person reading the file in a spreadsheet would count:
            // line 1 is the header, so the first record is line 2. An error that
            // says "row 4" must point at what row 4 looks like on their screen.
            rows.Add(new CsvRow(i + 1, raw, ParseFields(raw)));
        }

        return new CsvDocument(header, lines[0], rows);
    }

    /// <summary>
    /// A column's value by header name, or null when the column is absent or
    /// empty. Absent and empty are the same thing to an importer: there is
    /// nothing to record either way.
    /// </summary>
    public static string? Field(this CsvRow row, IReadOnlyList<string> header, string name)
    {
        var index = -1;
        for (var i = 0; i < header.Count; i++)
        {
            if (string.Equals(header[i], name, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        if (index < 0 || index >= row.Fields.Count)
        {
            return null;
        }

        var value = row.Fields[index].Trim();
        return value.Length == 0 ? null : value;
    }

    public static int? IntField(this CsvRow row, IReadOnlyList<string> header, string name)
    {
        var value = row.Field(header, name);
        if (value is null)
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// Splits on newlines that are not inside a quoted field. Doing this before
    /// parsing fields is what makes an address containing a line break survive.
    /// </summary>
    private static List<string> SplitRecords(string text)
    {
        var records = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
                current.Append(c);
                continue;
            }

            if (!inQuotes && (c == '\n' || c == '\r'))
            {
                // Treat CRLF as one break rather than two.
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                records.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
        {
            records.Add(current.ToString());
        }

        // A trailing newline is not a record.
        while (records.Count > 0 && string.IsNullOrWhiteSpace(records[^1]))
        {
            records.RemoveAt(records.Count - 1);
        }

        return records;
    }

    private static List<string> ParseFields(string record)
    {
        var fields = new List<string>();
        var value = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < record.Length; i++)
        {
            var c = record[i];

            if (inQuotes)
            {
                if (c != '"')
                {
                    value.Append(c);
                    continue;
                }

                // "" inside a quoted field is one literal quote.
                if (i + 1 < record.Length && record[i + 1] == '"')
                {
                    value.Append('"');
                    i++;
                    continue;
                }

                inQuotes = false;
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    fields.Add(value.ToString());
                    value.Clear();
                    break;
                default:
                    value.Append(c);
                    break;
            }
        }

        fields.Add(value.ToString());
        return fields;
    }

    /// <summary>
    /// Header names are compared without spaces, case, or underscores, because
    /// "Model Year", "model_year", and "MODELYEAR" are the same column and a
    /// dealership should not have to know which spelling we chose.
    /// </summary>
    private static string Normalize(string header) =>
        header.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("﻿", string.Empty, StringComparison.Ordinal)
            .Trim();
}
