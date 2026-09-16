// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   IServiceCatalogue — what the rest of the application may call to reach the
//   workshop's standing decisions: the jobs it sells, and what an hour costs.
//
// Usage:
//   Inject IServiceCatalogue. Nothing outside this folder touches OpCode,
//   LabourRate, or the service schema (ADR-014).
//
// Coding Instructions:
//   TWO SCOPES, AND THEY ARE NOT THE SAME. Op codes are organization-wide, like
//   the parts catalogue: "front brakes, 1.4 hours" is the same job at every
//   lot, and a group that lets each store invent its own cannot compare them.
//   Labour rates are per rooftop, because what an hour SELLS for is local.
//
//   Both are guarded by Service.Configure, held organization-wide, and that is
//   not Service.Write. Writing up a job is daily work a technician does; setting
//   the price of every hour the workshop will sell is not.

using DealerFOSS.Core;

namespace DealerFOSS.RepairOrders;

public interface IServiceCatalogue
{
    /// <summary>The jobs this group sells, active ones unless asked otherwise.</summary>
    Task<Result<Page<OpCodeView>>> ListOpCodesAsync(
        OpCodeQuery query,
        CancellationToken cancellationToken);

    Task<Result<OpCodeView>> AddOpCodeAsync(NewOpCode opCode, CancellationToken cancellationToken);

    Task<Result<OpCodeView>> ReviseOpCodeAsync(
        Guid opCodeId,
        ReviseOpCode revision,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stops it being offered. Never deletes: a job written up in March must
    /// still read correctly after the catalogue is tidied in September.
    /// </summary>
    Task<Result<OpCodeView>> SetOpCodeActiveAsync(
        Guid opCodeId,
        bool active,
        CancellationToken cancellationToken);

    /// <summary>What an hour sells for at the rooftops the caller covers.</summary>
    Task<Result<IReadOnlyList<LabourRateView>>> ListRatesAsync(
        RooftopId? rooftopId,
        CancellationToken cancellationToken);

    Task<Result<LabourRateView>> SetRateAsync(NewLabourRate rate, CancellationToken cancellationToken);

    /// <summary>
    /// The rate to put on a line, given who is paying and where the work is
    /// being done. Null when the lot has not set one, which is a legitimate
    /// state — a caller then uses whatever the person typed.
    /// </summary>
    Task<Result<LabourRateView?>> RateForAsync(
        RooftopId rooftopId,
        ServicePayType payType,
        CancellationToken cancellationToken);
}

/// <summary>How a caller narrows the catalogue of jobs.</summary>
public sealed record OpCodeQuery(
    string? Search = null,

    /// <summary>
    /// Default true. A withdrawn code is history; offering it on a picker is
    /// how it gets written onto a new job by accident.
    /// </summary>
    bool ActiveOnly = true,
    int Limit = 50,
    int Offset = 0);

public sealed record NewOpCode(
    string Code,
    string Description,
    decimal StandardHours,
    string DefaultPayType = "CustomerPay");

public sealed record ReviseOpCode(
    string Description,
    decimal StandardHours,
    string DefaultPayType);

public sealed record OpCodeView(
    Guid Id,
    string Code,
    string Description,
    decimal StandardHours,
    string DefaultPayType,
    bool IsActive);

/// <summary>
/// Setting a rate. One per pay type per rooftop: sending a second for the same
/// pair reprices the existing one rather than creating a rival.
/// </summary>
public sealed record NewLabourRate(
    RooftopId RooftopId,
    string Name,
    decimal AmountPerHour,
    string Currency,
    string AppliesTo);

public sealed record LabourRateView(
    Guid Id,
    RooftopId RooftopId,
    string Name,
    decimal AmountPerHour,
    string Currency,
    string AppliesTo,
    bool IsActive);
