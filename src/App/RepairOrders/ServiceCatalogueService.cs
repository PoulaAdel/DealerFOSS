// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ServiceCatalogueService — the op-code catalogue and the labour rates, and
//   who may change them.
//
// Usage:
//   Through IServiceCatalogue.
//
// Coding Instructions:
//   READING IS Service.Read, WRITING IS Service.Configure, and the second is
//   organization-wide. Anybody who can write up a job needs to SEE the
//   catalogue — a picker they cannot load is worse than no picker — but setting
//   the price of every future hour is a management act.
//
//   OP CODES ARE NOT ROOFTOP-SCOPED, so there is no rooftop filter on them and
//   there must not be one. They are a group catalogue, like parts. The scope
//   check on reading is "may this caller see the workshop at all", which is
//   what IsAllowedAnywhereAsync answers.
//
//   SETTING A RATE FOR A PAY TYPE THAT ALREADY HAS ONE REPRICES IT rather than
//   adding a second. Two active rates for the same payer at the same lot is not
//   a choice between them, it is a question nobody can answer, and the unique
//   index in ServiceTables refuses it anyway.

using Microsoft.EntityFrameworkCore;
using DealerFOSS.Core;
using DealerFOSS.Data;
using DealerFOSS.Identity;

namespace DealerFOSS.RepairOrders;

public sealed class ServiceCatalogueService(
    TenantDb db,
    IAccessDirectory access,
    ICurrentUser currentUser,
    IAuditSink audit)
    : IServiceCatalogue
{
    private const string ReadPermission = Permissions.ServiceRead;
    private const string ConfigurePermission = Permissions.ServiceConfigure;

    private readonly TenantDb _db = db;
    private readonly IAccessDirectory _access = access;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditSink _audit = audit;

    public async Task<Result<Page<OpCodeView>>> ListOpCodesAsync(
        OpCodeQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!await AllowedAnywhereAsync(ReadPermission, cancellationToken))
        {
            return Result.Failure<Page<OpCodeView>>(ServiceErrors.Forbidden);
        }

        var take = Paging.Limit(query.Limit);
        var skip = Paging.Offset(query.Offset);
        var codes = _db.OpCodes.AsNoTracking();

        if (query.ActiveOnly)
        {
            codes = codes.Where(c => c.IsActive);
        }

        var search = (query.Search ?? string.Empty).Trim();
        if (search.Length > 0)
        {
            // Matched on both halves: an advisor either knows the code or knows
            // what the job is called, and which one they reach for depends on
            // how long they have worked here.
            var code = OpCode.Normalize(search);

            codes = codes.Where(c =>
                EF.Functions.Like(c.Code, $"%{code}%")
                || EF.Functions.Like(c.Description, $"%{search}%"));
        }

        var total = await codes.CountAsync(cancellationToken);

        var rows = await codes
            .OrderBy(c => c.Code)
            .ThenBy(c => c.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return Result.Success(new Page<OpCodeView>(
            rows.Select(Describe).ToList(), total, skip, take));
    }

    public async Task<Result<OpCodeView>> AddOpCodeAsync(
        NewOpCode opCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(opCode);

        if (!await MayConfigureAsync(cancellationToken))
        {
            return Result.Failure<OpCodeView>(ServiceErrors.Forbidden);
        }

        if (!Enum.TryParse<ServicePayType>(opCode.DefaultPayType, ignoreCase: true, out var payType))
        {
            return Result.Failure<OpCodeView>(ServiceErrors.UnknownPayType);
        }

        var code = OpCode.Normalize(opCode.Code ?? string.Empty);

        // One code, one job. Two rows reading BRK-FRT is how an op-code report
        // starts counting the same work twice.
        if (await _db.OpCodes.AnyAsync(c => c.Code == code, cancellationToken))
        {
            return Result.Failure<OpCodeView>(ServiceErrors.OpCodeAlreadyExists(code));
        }

        OpCode row;
        try
        {
            row = new OpCode(Guid.NewGuid(), opCode.Code ?? string.Empty,
                opCode.Description, opCode.StandardHours, payType);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return Result.Failure<OpCodeView>(Error.Validation("service.op_code_invalid", ex.Message));
        }

        _db.OpCodes.Add(row);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, ConfigurePermission, AuditOutcome.Allowed,
                "OpCode", row.Id.ToString(), null,
                $"Op code {row.Code} added at {row.StandardHours}h", null, null),
            cancellationToken);

        return Result.Success(Describe(row));
    }

    public async Task<Result<OpCodeView>> ReviseOpCodeAsync(
        Guid opCodeId,
        ReviseOpCode revision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(revision);

        if (!await MayConfigureAsync(cancellationToken))
        {
            return Result.Failure<OpCodeView>(ServiceErrors.Forbidden);
        }

        if (!Enum.TryParse<ServicePayType>(revision.DefaultPayType, ignoreCase: true, out var payType))
        {
            return Result.Failure<OpCodeView>(ServiceErrors.UnknownPayType);
        }

        var row = await _db.OpCodes.SingleOrDefaultAsync(c => c.Id == opCodeId, cancellationToken);
        if (row is null)
        {
            return Result.Failure<OpCodeView>(ServiceErrors.Forbidden);
        }

        try
        {
            row.Revise(revision.Description, revision.StandardHours, payType);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return Result.Failure<OpCodeView>(Error.Validation("service.op_code_invalid", ex.Message));
        }

        await _db.SaveChangesAsync(cancellationToken);

        // THE CODE ITSELF IS NOT REVISABLE, and that is why this says so. Jobs
        // already written cite it, printed invoices carry it, and renaming it
        // would silently rewrite what those jobs claim to be.
        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, ConfigurePermission, AuditOutcome.Allowed,
                "OpCode", row.Id.ToString(), null,
                $"Op code {row.Code} revised to {row.StandardHours}h", null, null),
            cancellationToken);

        return Result.Success(Describe(row));
    }

    public async Task<Result<OpCodeView>> SetOpCodeActiveAsync(
        Guid opCodeId,
        bool active,
        CancellationToken cancellationToken)
    {
        if (!await MayConfigureAsync(cancellationToken))
        {
            return Result.Failure<OpCodeView>(ServiceErrors.Forbidden);
        }

        var row = await _db.OpCodes.SingleOrDefaultAsync(c => c.Id == opCodeId, cancellationToken);
        if (row is null)
        {
            return Result.Failure<OpCodeView>(ServiceErrors.Forbidden);
        }

        if (active)
        {
            row.Restore();
        }
        else
        {
            row.Withdraw();
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, ConfigurePermission, AuditOutcome.Allowed,
                "OpCode", row.Id.ToString(), null,
                active ? $"Op code {row.Code} restored" : $"Op code {row.Code} withdrawn", null, null),
            cancellationToken);

        return Result.Success(Describe(row));
    }

    public async Task<Result<IReadOnlyList<LabourRateView>>> ListRatesAsync(
        RooftopId? rooftopId,
        CancellationToken cancellationToken)
    {
        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<IReadOnlyList<LabourRateView>>(ServiceErrors.Forbidden);
        }

        if (rooftopId is { } requested && !scope.Covers(requested))
        {
            return Result.Failure<IReadOnlyList<LabourRateView>>(ServiceErrors.Forbidden);
        }

        var rates = _db.LabourRates.AsNoTracking();

        if (!scope.IsOrganizationWide)
        {
            var allowed = scope.Rooftops.ToList();
            rates = rates.Where(r => allowed.Contains(r.RooftopId));
        }

        if (rooftopId is { } only)
        {
            rates = rates.Where(r => r.RooftopId == only);
        }

        // Not paged, and bounded by what a dealership IS: three pay types at each
        // of a handful of lots. A pager here would be a control that never does
        // anything (ADR-025).
        var rows = await rates
            .OrderBy(r => r.AppliesTo)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<LabourRateView>>(rows.Select(Describe).ToList());
    }

    public async Task<Result<LabourRateView>> SetRateAsync(
        NewLabourRate rate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rate);

        // Organization-wide even though the rate itself is local. Setting what an
        // hour sells for is a management decision about one lot, taken by
        // somebody who answers for the group — not by whoever happens to be on
        // that counter.
        if (!await MayConfigureAsync(cancellationToken))
        {
            return Result.Failure<LabourRateView>(ServiceErrors.Forbidden);
        }

        if (!Enum.TryParse<ServicePayType>(rate.AppliesTo, ignoreCase: true, out var payType))
        {
            return Result.Failure<LabourRateView>(ServiceErrors.UnknownPayType);
        }

        var existing = await _db.LabourRates.SingleOrDefaultAsync(
            r => r.RooftopId == rate.RooftopId && r.AppliesTo == payType, cancellationToken);

        try
        {
            var money = new Money(rate.AmountPerHour, rate.Currency);

            if (existing is null)
            {
                existing = new LabourRate(Guid.NewGuid(), rate.RooftopId, rate.Name, money, payType);
                _db.LabourRates.Add(existing);
            }
            else
            {
                existing.Reprice(rate.Name, money);
                existing.Restore();
            }
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return Result.Failure<LabourRateView>(Error.Validation("service.rate_invalid", ex.Message));
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, ConfigurePermission, AuditOutcome.Allowed,
                "LabourRate", existing.Id.ToString(), existing.RooftopId.Value,
                $"{existing.AppliesTo} labour rate set to {existing.AmountPerHour}", null, null),
            cancellationToken);

        return Result.Success(Describe(existing));
    }

    public async Task<Result<LabourRateView?>> RateForAsync(
        RooftopId rooftopId,
        ServicePayType payType,
        CancellationToken cancellationToken)
    {
        if (!await _access.IsAuthorizedAsync(_currentUser.Id, ReadPermission, rooftopId, cancellationToken))
        {
            return Result.Failure<LabourRateView?>(ServiceErrors.Forbidden);
        }

        var row = await _db.LabourRates
            .AsNoTracking()
            .SingleOrDefaultAsync(
                r => r.RooftopId == rooftopId && r.AppliesTo == payType && r.IsActive,
                cancellationToken);

        return Result.Success(row is null ? null : Describe(row));
    }

    /// <summary>
    /// Whether the caller may see the workshop anywhere at all. Op codes are a
    /// group catalogue with no rooftop of their own, so there is nothing
    /// narrower to check against.
    /// </summary>
    private async Task<bool> AllowedAnywhereAsync(string permission, CancellationToken cancellationToken)
    {
        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, permission, cancellationToken);
        return !scope.GrantsNothing;
    }

    /// <summary>
    /// Whether the caller may change what the workshop sells and charges.
    ///
    /// ORGANIZATION-WIDE, not "holds it at some rooftop" — the same shape the
    /// parts catalogue uses. Somebody scoped to one lot may run that lot; they
    /// may not set a standard time the whole group is then measured against.
    /// </summary>
    private async Task<bool> MayConfigureAsync(CancellationToken cancellationToken)
    {
        var scope = await _access.GetAuthorizedScopeAsync(
            _currentUser.Id, ConfigurePermission, cancellationToken);

        return scope.IsOrganizationWide;
    }

    private static OpCodeView Describe(OpCode row) =>
        new(row.Id, row.Code, row.Description, row.StandardHours,
            row.DefaultPayType.ToString(), row.IsActive);

    private static LabourRateView Describe(LabourRate row) =>
        new(row.Id, row.RooftopId, row.Name, row.AmountPerHour, row.Currency,
            row.AppliesTo.ToString(), row.IsActive);
}
