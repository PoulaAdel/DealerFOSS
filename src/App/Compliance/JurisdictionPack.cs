// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   JurisdictionPack, JurisdictionRate and JurisdictionRule — one jurisdiction's
//   tax DATA, as loaded, with the provenance needed to defend a figure later.
//   Also the three enums that describe a pack: its scheme, its status, and the
//   level a rate is charged at.
//
// Usage:
//   Written only by PackLoader and the review action in ComplianceService.
//   Read only by the resolver. Nothing outside DealerFOSS.Compliance sees these
//   types — Deals reaches the rates through ITaxResolution.
//
// Coding Instructions:
//   DATA AND DECLARATIONS, NEVER LOGIC (ADR-024 R1). There is no conditional,
//   no expression and no script anywhere in a pack. A rate is a number with
//   matchers; a rule is a flag. The arithmetic that reads them is
//   Deal.TaxableBasis, in our code, under our tests. The moment a pack wants to
//   say something these shapes cannot, that is the signal to add a flag in the
//   domain — not to let a pack carry a rule. A pack with logic in it would be a
//   plugin whose review burden is unbounded, which is the same reasoning that
//   put ADR-012 behind compiled connector discovery.
//
//   ROWS ARE NEVER UPDATED IN PLACE (R2). A rate change is a new pack VERSION,
//   which is new rows. Version 3 stays readable after version 4 lands, because a
//   deal taxed under version 3 has to still read as version 3 in an audit next
//   year. "Just update the rate table" is the failure mode this whole shape is
//   designed against: it would silently rewrite invoices customers already hold.
//   The only column that ever changes after a load is the review state, and that
//   is a statement about a person rather than about the data.
//
//   WHY THE MATCHER COLUMNS EXIST ALONGSIDE THE JURISDICTION CODE. ADR-024's
//   sketch named "jurisdiction code, postal key". Those are two different jobs
//   and conflating them does not survive contact with a US address: the code is
//   the LABEL that ends up on the tax line and on the paperwork, while the
//   matchers are what an address is actually tested against. A county rate
//   cannot be matched on a postcode at all — a single ZIP spans several taxing
//   jurisdictions, which is the whole reason ADR-024 rejected ZIPs as a key.
//   So a row carries both, and ADR-024's consequences record the addition.
//
//   EVERY MATCHING ROW APPLIES, one tax line each. Not "most specific wins":
//   state, county and city taxes genuinely stack on one car, which is what
//   RateKind is for. A precedence rule here would quietly undercharge every deal
//   in a county that levies its own rate on top of the state's.

namespace DealerFOSS.Compliance;

/// <summary>
/// How a pack's data was obtained, which decides what its numbers can be trusted
/// to mean (ADR-024).
/// </summary>
public enum PackScheme
{
    /// <summary>
    /// No rates at all. The pack exists so a jurisdiction can be named and
    /// labelled while a person enters the tax — ADR-024's first pack, and what
    /// makes R5 real rather than aspirational.
    /// </summary>
    Manual = 0,

    /// <summary>
    /// A Streamlined Sales Tax member state's published rate data. The member
    /// states hold a business harmless for a figure calculated from their own
    /// rate and boundary files, which is why provenance rather than accuracy in
    /// the abstract is the engineering requirement.
    /// </summary>
    Sst = 1,

    /// <summary>A tax provider answered. The call's evidence lives with the run (ADR-007).</summary>
    Vendor = 2,

    /// <summary>VAT on the dealer's margin at one national rate, as the EU used-car trade runs.</summary>
    EuVat = 3,
}

/// <summary>
/// Whether a pack may be used. <see cref="Supported"/> is a claim about a HUMAN
/// REVIEW, not about the file parsing.
/// </summary>
public enum PackStatus
{
    /// <summary>
    /// Loaded, hashed and readable, and deliberately NOT usable. A Draft pack
    /// resolves nothing. This is the state a pack ships in, because the product
    /// cannot know whether anybody qualified has read it.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// A named person recorded that they reviewed it. Only a Supported pack
    /// resolves. ADR-024's own review trigger says that a pack found Supported
    /// without a recorded review would mean the status means nothing — so the
    /// reviewer and the moment are columns, not an honour system.
    /// </summary>
    Supported = 1,

    /// <summary>
    /// Withdrawn, and kept. It stops resolving new deals and stays readable,
    /// because deals already taxed under it still have to be explainable.
    /// </summary>
    Withdrawn = 2,
}

/// <summary>Which authority levies a rate. Several stack on one sale.</summary>
public enum RateKind
{
    State = 0,
    County = 1,
    City = 2,

    /// <summary>
    /// A district, transit authority or similar body that levies on top of the
    /// others. Its own kind rather than folded into City, because it is a
    /// separate line on the paperwork and a separate authority to answer to.
    /// </summary>
    Special = 3,
}

/// <summary>
/// One loaded generation of one jurisdiction's data. Identified by
/// <see cref="PackId"/> plus <see cref="Version"/>, which is what a tax line
/// names.
/// </summary>
public sealed class JurisdictionPack
{
    /// <summary>The three flags <see cref="DealerFOSS.Deals.TaxBasisRules"/> needs, by rule key.</summary>
    /// <remarks>
    /// Named here rather than in the loader because two separate rules read the
    /// list: the loader refuses an unknown key, and the review action refuses to
    /// mark a pack Supported while one of these is missing. Both have to mean the
    /// same three keys or a pack could pass one and fail the other.
    /// </remarks>
    public static readonly string[] RequiredRuleKeys =
    [
        "tradeInReducesBasis",
        "documentationFeeIsTaxable",
        "otherFeesAreTaxable",
    ];

    public Guid Id { get; private set; }

    /// <summary>Stable across versions: "us-in" is Indiana in every generation.</summary>
    public string PackId { get; private set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2. A pack covers one country.</summary>
    public string Country { get; private set; } = string.Empty;

    public PackScheme Scheme { get; private set; }

    /// <summary>Monotonic per <see cref="PackId"/>. A new version is new rows, never an edit.</summary>
    public int Version { get; private set; }

    public DateOnly EffectiveFrom { get; private set; }

    /// <summary>Null means "still in force".</summary>
    public DateOnly? EffectiveTo { get; private set; }

    /// <summary>Where the data came from, so somebody can go and check it.</summary>
    public string SourceUri { get; private set; } = string.Empty;

    /// <summary>When the source was actually read. Not when the pack was written.</summary>
    public DateTimeOffset FetchedAt { get; private set; }

    /// <summary>
    /// SHA-256 of the pack document's bytes, so a reviewer's approval is tied to
    /// the exact content they approved rather than to a name and a number.
    /// </summary>
    public string ContentHash { get; private set; } = string.Empty;

    public PackStatus Status { get; private set; }

    public string? ReviewedBy { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public DateTimeOffset LoadedAt { get; private set; }

    private readonly List<JurisdictionRate> _rates = [];

    private readonly List<JurisdictionRule> _rules = [];

    public IReadOnlyList<JurisdictionRate> Rates => _rates;

    public IReadOnlyList<JurisdictionRule> Rules => _rules;

    private JurisdictionPack()
    {
    }

    internal JurisdictionPack(
        Guid id,
        string packId,
        string country,
        PackScheme scheme,
        int version,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        string sourceUri,
        DateTimeOffset fetchedAt,
        string contentHash,
        DateTimeOffset loadedAt)
    {
        Id = id;
        PackId = packId;
        Country = country;
        Scheme = scheme;
        Version = version;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        SourceUri = sourceUri;
        FetchedAt = fetchedAt;
        ContentHash = contentHash;
        LoadedAt = loadedAt;

        // Always. A pack that arrived already Supported would be a pack nobody
        // reviewed wearing the label that says somebody did.
        Status = PackStatus.Draft;
    }

    internal void Add(JurisdictionRate rate) => _rates.Add(rate);

    internal void Add(JurisdictionRule rule) => _rules.Add(rule);

    /// <summary>The rule keys this pack does not carry a value for.</summary>
    internal IReadOnlyList<string> MissingRequiredRules() =>
        [.. RequiredRuleKeys.Where(key =>
            !_rules.Any(r => string.Equals(r.RuleKey, key, StringComparison.Ordinal)))];

    /// <summary>
    /// Records that a named person reviewed this exact content and is willing to
    /// have the product charge money from it.
    /// </summary>
    /// <remarks>
    /// REFUSES WHILE A REQUIRED FLAG IS UNSET, and that refusal is the point.
    /// A pack shipped with the common-US flags as placeholders would mean a
    /// reviewer who never opened the source silently blessed whoever wrote the
    /// file — so the flags are absent rather than defaulted, and this is where
    /// their absence becomes impossible to miss. The alternative was to trust
    /// that a reviewer checks; ADR-024 is explicit that a rotted pack is worse
    /// than none precisely because it is trusted.
    /// </remarks>
    internal void MarkReviewed(string reviewedBy, DateTimeOffset at)
    {
        if (Status == PackStatus.Withdrawn)
        {
            throw new InvalidOperationException(
                "A withdrawn pack cannot be reviewed back into use. Load a new version instead.");
        }

        var missing = MissingRequiredRules();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"This pack cannot be marked supported until somebody decides what it taxes: "
                + $"{string.Join(", ", missing)}.");
        }

        if (Scheme != PackScheme.Manual && _rates.Count == 0)
        {
            throw new InvalidOperationException(
                "A pack with no rates cannot be supported unless its scheme is Manual, "
                + "which is the scheme that means a person enters the figure.");
        }

        Status = PackStatus.Supported;
        ReviewedBy = reviewedBy;
        ReviewedAt = at;
    }

    internal void Withdraw() => Status = PackStatus.Withdrawn;

    /// <summary>Whether this generation governs a sale on <paramref name="date"/>.</summary>
    internal bool IsEffectiveOn(DateOnly date) =>
        date >= EffectiveFrom && (EffectiveTo is null || date <= EffectiveTo);
}

/// <summary>One rate, with the matchers that decide whether an address is in it.</summary>
public sealed class JurisdictionRate
{
    public Guid Id { get; private set; }

    public Guid PackRowId { get; private set; }

    /// <summary>The label written onto the tax line: "US-IN", "US-IN-097".</summary>
    public string JurisdictionCode { get; private set; } = string.Empty;

    /// <summary>What a person reads on the paperwork — "Indiana sales tax".</summary>
    public string Description { get; private set; } = string.Empty;

    public RateKind Kind { get; private set; }

    /// <summary>A fraction: 0.07 is seven percent. Six decimal places, because some districts levy in eighths.</summary>
    public decimal Rate { get; private set; }

    /// <summary>State or region the address must be in, or null for "anywhere in the country".</summary>
    public string? AdministrativeArea { get; private set; }

    public string? County { get; private set; }

    /// <summary>Postcode or ZIP, exact. Null when the rate is not postcode-scoped.</summary>
    public string? PostalKey { get; private set; }

    public DateOnly EffectiveFrom { get; private set; }

    public DateOnly? EffectiveTo { get; private set; }

    public string SourceUri { get; private set; } = string.Empty;

    private JurisdictionRate()
    {
    }

    internal JurisdictionRate(
        Guid id,
        Guid packRowId,
        string jurisdictionCode,
        string description,
        RateKind kind,
        decimal rate,
        string? administrativeArea,
        string? county,
        string? postalKey,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        string sourceUri)
    {
        Id = id;
        PackRowId = packRowId;
        JurisdictionCode = jurisdictionCode;
        Description = description;
        Kind = kind;
        Rate = rate;
        AdministrativeArea = administrativeArea;
        County = county;
        PostalKey = postalKey;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        SourceUri = sourceUri;
    }

    /// <summary>
    /// Whether this rate applies to an address: every matcher the row populated
    /// has to agree, and the ones it left null do not constrain anything.
    /// </summary>
    /// <remarks>
    /// Case-insensitive and trimmed on purpose. These values are typed by people
    /// into a deal — "IN", "in", " IN " are the same state, and a rate that
    /// silently failed to match because of a capital letter would charge a
    /// customer nothing and look like a jurisdiction we do not support.
    /// </remarks>
    internal bool Matches(string? administrativeArea, string? county, string? postalCode) =>
        Agrees(AdministrativeArea, administrativeArea)
        && Agrees(County, county)
        && Agrees(PostalKey, postalCode);

    private static bool Agrees(string? required, string? supplied) =>
        required is null
        || (supplied is not null
            && string.Equals(required.Trim(), supplied.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// One flag or number a jurisdiction declares. A value, never an expression.
/// </summary>
public sealed class JurisdictionRule
{
    public Guid Id { get; private set; }

    public Guid PackRowId { get; private set; }

    public string RuleKey { get; private set; } = string.Empty;

    /// <summary>Stored as text and parsed by the reader that knows the key's type.</summary>
    public string Value { get; private set; } = string.Empty;

    private JurisdictionRule()
    {
    }

    internal JurisdictionRule(Guid id, Guid packRowId, string ruleKey, string value)
    {
        Id = id;
        PackRowId = packRowId;
        RuleKey = ruleKey;
        Value = value;
    }
}
