// Account — one line of the chart of accounts.
//
// Use:  looked up by code when posting. The chart is seeded, not created by
//       users, until account administration lands.
// Edit: the account kind decides which side increases it, and every report ever
//       written depends on that being right. Do not add a kind without deciding
//       its normal balance.

using OpenDealer360.Core;

namespace OpenDealer360.Accounting;

public sealed class Account : AuditableEntity
{
    public Guid Id { get; private set; }

    /// <summary>Stable numeric code — "1000". What accountants actually use.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public AccountKind Kind { get; private set; }

    public bool IsActive { get; private set; } = true;

    /// <summary>Which side increases this account. Assets and expenses are debits.</summary>
    public bool IncreasesOnDebit =>
        Kind is AccountKind.Asset or AccountKind.Expense;

    private Account()
    {
    }

    public Account(Guid id, string code, string name, AccountKind kind)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("An account needs a code.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("An account needs a name.", nameof(name));
        }

        Id = id;
        Code = code.Trim();
        Name = name.Trim();
        Kind = kind;
    }

    public void Deactivate() => IsActive = false;
}

public enum AccountKind
{
    Asset = 0,
    Liability = 1,
    Equity = 2,
    Revenue = 3,
    Expense = 4,
}

/// <summary>
/// The accounts this slice posts to. Codes are conventional retail-dealer
/// numbering; a real installation will replace the chart, which is why nothing
/// outside this file hard-codes anything but these constants.
/// </summary>
public static class AccountCodes
{
    public const string Cash = "1000";
    public const string VehicleInventory = "1300";
    public const string TradeInventory = "1310";
    public const string VehicleSalesRevenue = "4000";
    public const string FeeRevenue = "4100";
    public const string SalesDiscounts = "4900";
    public const string CostOfVehicleSales = "5000";
}
