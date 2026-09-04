// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   NhtsaSafetyRecalls — ISafetyRecalls against the US road-safety regulator.
//
// Usage:
//   Registered as ISafetyRecalls; never referenced by name outside Program.
//
// Coding Instructions:
//   This is the FIRST thing in the product that calls out to a network, so
//   the rules it follows are new here and worth keeping:
//
//   1. It cannot throw. Every network fault becomes RecallErrors.Unavailable.
//   A car cannot become un-sellable because somebody else's service is down.
//   2. It has a deadline. The timeout lives on the HttpClient in Program.cs,
//   not in a token here, so it is configured in one visible place.
//   3. It never writes. No table, no cache, no cursor — ask and forget. A
//   stored answer would go stale silently and start asserting an all-clear
//   that nobody re-checked.
//
//   The regulator's own API is unauthenticated and free, which is why this
//   one is buildable when every other integration on the list is not
//   (doc 11 §3.4).

using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DealerFOSS.Core;

namespace DealerFOSS.Vehicles;

public sealed partial class NhtsaSafetyRecalls(
    HttpClient http,
    IVehicles vehicles,
    ILogger<NhtsaSafetyRecalls> logger) : ISafetyRecalls
{
    // Source-generated rather than called through LoggerExtensions: the analyzer
    // requires it, and the message is defined once instead of being formatted on
    // every call whether or not anything is listening.
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Safety recall lookup failed for vehicle {VehicleId}.")]
    private static partial void LookupFailed(ILogger logger, Guid vehicleId, Exception exception);

    /// <summary>The regulator indexes campaigns by these three, not by VIN.</summary>
    private const string RecallsPath = "recalls/recallsByVehicle";

    private readonly HttpClient _http = http;
    private readonly IVehicles _vehicles = vehicles;
    private readonly ILogger<NhtsaSafetyRecalls> _logger = logger;

    public async Task<Result<RecallReport>> ForVehicleAsync(
        Guid vehicleId,
        CancellationToken cancellationToken)
    {
        var vehicle = await _vehicles.GetAsync(vehicleId, cancellationToken);
        if (vehicle.IsFailure)
        {
            return Result.Failure<RecallReport>(vehicle.Error);
        }

        var car = vehicle.Value;

        // A trailer recorded from a frame plate has a make and model of sorts but
        // nothing the regulator would recognise. Refusing is honest; sending a
        // half-empty query and rendering "no recalls" would not be.
        if (string.IsNullOrWhiteSpace(car.Make) || string.IsNullOrWhiteSpace(car.Model))
        {
            return Result.Failure<RecallReport>(RecallErrors.NotIdentifiable);
        }

        var query =
            $"{RecallsPath}?make={Uri.EscapeDataString(car.Make)}" +
            $"&model={Uri.EscapeDataString(car.Model)}" +
            $"&modelYear={car.ModelYear.ToString(CultureInfo.InvariantCulture)}";

        NhtsaRecallResponse? payload;
        try
        {
            payload = await _http.GetFromJsonAsync<NhtsaRecallResponse>(query, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or NotSupportedException
            or System.Text.Json.JsonException)
        {
            // Logged at warning, not error: an unreachable public service is a
            // normal Tuesday, and paging somebody for it would train them to
            // ignore the alert.
            LookupFailed(_logger, vehicleId, ex);
            return Result.Failure<RecallReport>(RecallErrors.Unavailable);
        }

        var campaigns = (payload?.Results ?? [])
            .Select(Translate)
            .OrderByDescending(c => c.ReportedOn ?? DateOnly.MinValue)
            .ToList();

        return Result.Success(new RecallReport(
            car.Id, car.ModelYear, car.Make, car.Model, campaigns));
    }

    private static RecallCampaign Translate(NhtsaRecall recall) => new(
        CampaignNumber: recall.NHTSACampaignNumber ?? string.Empty,
        Manufacturer: recall.Manufacturer ?? string.Empty,
        Component: recall.Component ?? string.Empty,
        Summary: recall.Summary ?? string.Empty,
        Remedy: recall.Remedy ?? string.Empty,
        ReportedOn: ParseDate(recall.ReportReceivedDate),
        DoNotDrive: recall.ParkIt,
        ParkOutside: recall.ParkOutSide);

    /// <summary>
    /// The regulator sends MM/dd/yyyy. An unparseable date is left absent rather
    /// than guessed at — a campaign with no date is still a campaign, and a
    /// wrong date would sort it into the wrong place in the list.
    /// </summary>
    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParseExact(value, "MM/dd/yyyy", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed)
            ? parsed
            : null;

    // --- the regulator's own shape, kept private -----------------------------
    //
    // Deliberately not exposed: their field names, casing and optionality are
    // theirs to change. Translate() is the one place that has to move if they do.

    private sealed record NhtsaRecallResponse(
        [property: JsonPropertyName("Count")] int Count,
        [property: JsonPropertyName("results")] IReadOnlyList<NhtsaRecall>? Results);

    private sealed record NhtsaRecall(
        [property: JsonPropertyName("NHTSACampaignNumber")] string? NHTSACampaignNumber,
        [property: JsonPropertyName("Manufacturer")] string? Manufacturer,
        [property: JsonPropertyName("Component")] string? Component,
        [property: JsonPropertyName("Summary")] string? Summary,
        [property: JsonPropertyName("Remedy")] string? Remedy,
        [property: JsonPropertyName("ReportReceivedDate")] string? ReportReceivedDate,
        [property: JsonPropertyName("parkIt")] bool ParkIt,
        [property: JsonPropertyName("parkOutSide")] bool ParkOutSide);
}
