// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   PageJson — reading a paged list out of a response, the same way everywhere.
//
//   Every list endpoint returns Page<T>: an object with rows, total, offset and
//   limit. Before that they returned bare arrays, and the tests each reached for
//   EnumerateArray directly. This exists so converting the next list means
//   changing the endpoint and not thirty assertions.
//
// Usage:
//   var body = await response.Content.ReadFromJsonAsync<JsonElement>();
//   body.Rows().Should().HaveCount(3);
//   body.Total().Should().Be(112);
//
// Coding Instructions:
//   ROWS THROWS ON A BARE ARRAY RATHER THAN COPING WITH ONE. A helper that
//   quietly accepted both shapes would let a list silently lose its paging and
//   every test still pass, which is the one outcome worth preventing here.

using System.Text.Json;

namespace DealerFOSS.IntegrationTests;

internal static class PageJson
{
    /// <summary>The rows of one page.</summary>
    public static IReadOnlyList<JsonElement> Rows(this JsonElement page)
    {
        if (page.ValueKind != JsonValueKind.Object || !page.TryGetProperty("rows", out var rows))
        {
            throw new InvalidOperationException(
                $"Expected a paged list with a 'rows' property, got {page.ValueKind}. "
                + "A list endpoint that returns a bare array has lost its paging.");
        }

        return rows.EnumerateArray().ToList();
    }

    /// <summary>How many rows match the query altogether, not just on this page.</summary>
    public static int Total(this JsonElement page) => page.GetProperty("total").GetInt32();

    /// <summary>How many rows were skipped to reach this page.</summary>
    public static int Offset(this JsonElement page) => page.GetProperty("offset").GetInt32();
}
