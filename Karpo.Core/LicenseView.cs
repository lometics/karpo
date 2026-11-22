// What the licence says (docs\widgets.md §138, §339): the licence's name and why it is what it is, how
// much of the free version is in use, and how a purchase ended — the settings app's License page's
// words, and the edit bar's ⚙ › Karpo Pro's (KarpoMenu.Pro.cs). The rules are Licensing.cs'
// (LicenseRules, LicenseCount, LicenseGate); this is their words. §545: and the settings panel's side-list footer, how much
// is placed against the limits (Footer): the free version a meter for each, Pro (or a developer build) what there is.

namespace Karpo.Core;

public static class LicenseView
{
    /// <summary>The status card's header: what this copy is.</summary>
    public static string Title(LicenseStatus status) => status.Basis switch
    {
        LicenseBasis.DeveloperBuild => S.SettingsLicenseDeveloperTitle,
        LicenseBasis.DeveloperFree => S.SettingsLicenseDeveloperFreeTitle,
        LicenseBasis.StorePro or LicenseBasis.OfflineGrace => S.SettingsLicenseProTitle,
        LicenseBasis.Checking => S.SettingsLicenseCheckingTitle,
        _ => S.SettingsLicenseFreeTitle,
    };

    /// <summary>The status card's description: why, and what it means. <paramref name="date"/> writes the grace's end.</summary>
    public static string Detail(LicenseStatus status, Func<DateTimeOffset, string> date) => status.Basis switch
    {
        LicenseBasis.DeveloperBuild => S.SettingsLicenseDeveloperDetail,
        LicenseBasis.DeveloperFree => S.SettingsLicenseDeveloperFreeDetail,
        LicenseBasis.StorePro => S.SettingsLicenseProDetail,
        LicenseBasis.OfflineGrace when status.GraceUntil is { } until => S.SettingsLicenseGraceDetail(date(until)),
        LicenseBasis.OfflineGrace => S.SettingsLicenseProDetail,
        LicenseBasis.Checking => S.SettingsLicenseCheckingDetail,
        LicenseBasis.StoreUnavailable => S.SettingsLicenseUnavailableDetail,
        LicenseBasis.StoreMissing => S.SettingsLicenseStoreMissing,
        _ => S.SettingsLicenseFreeDetail(widgets: LicenseRules.FreeWidgets, layouts: LicenseRules.FreeLayouts),
    };

    /// <summary>Whether the page shows what of the free version is in use: only where its limits apply.</summary>
    public static bool ShowsUsage(LicenseStatus status) => status.IsLimited;

    /// <summary>Whether the page has its Microsoft Store section: not for a developer build that acts as nothing but itself.</summary>
    public static bool ShowsStore(LicenseStatus status) => status.Basis != LicenseBasis.DeveloperBuild;

    /// <summary>Whether it offers Pro: everywhere but where Pro is had.</summary>
    public static bool OffersPro(LicenseStatus status) => status.Tier != LicenseTier.Pro;

    /// <summary>Each layout's name and how many widgets it shows as the free version counts them, in the file's order.</summary>
    public static List<(string Name, int Widgets)> Usage(Config config) =>
        [.. config.Layouts.Where(layout => layout is not null).Select(layout => (layout.Name, LicenseCount.Widgets(layout)))];

    /// <summary>"12 of 20".</summary>
    public static string Of(int count, int limit) => S.SettingsLicenseUsageOf(count: count, limit: limit);

    /// <summary>What the Pro card says under its name: the pitch where Pro can be bought, else why it can't.</summary>
    public static string Pitch(bool canPurchase, bool hasStore) =>
        canPurchase ? S.SettingsLicensePitch
        : hasStore ? S.SettingsLicenseNotOnSale
        : S.SettingsLicenseStoreMissing;

    /// <summary>How a purchase ended, in words.</summary>
    public static string Purchase(PurchaseOutcome outcome) => outcome switch
    {
        PurchaseOutcome.Purchased => S.SettingsLicensePurchased,
        PurchaseOutcome.AlreadyOwned => S.SettingsLicenseAlreadyOwned,
        PurchaseOutcome.Cancelled => S.SettingsLicenseCancelled,
        PurchaseOutcome.NetworkError => S.SettingsLicenseNetworkError,
        PurchaseOutcome.ServerError => S.SettingsLicenseServerError,
        PurchaseOutcome.NotConfigured => S.SettingsLicenseNotOnSale,
        _ => S.SettingsLicenseStoreMissing,
    };

    /// <summary>Whether a purchase's end is good news (or none at all) rather than a failure to say as one.</summary>
    public static bool WentWell(PurchaseOutcome outcome) =>
        outcome is PurchaseOutcome.Purchased or PurchaseOutcome.AlreadyOwned or PurchaseOutcome.Cancelled;
}
