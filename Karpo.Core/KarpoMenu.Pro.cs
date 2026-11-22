// §339: "Karpo Pro ›", a level: the settings app's License page in the edit bar's ⚙ (the settings-in-the-dock
// plan's K1 to K5). The licence is the one the dock asks the Store for (StoreLicense) and LicenseGate
// refuses adds by; its words are LicenseView's, the page's own:
//
//  - **What this copy is** — Free, Pro, Developer, checking, the offline grace — and why, a note.
//  - **What of the free version is in use**, where its limits apply: each layout's widgets ("12 of 20"),
//    the layouts ("1 of 2"), and what counts as a widget, notes.
//  - **Get Karpo Pro**, with the Store's price once it is known: the Store's own dialog over the bar, how
//    the purchase ended under it afterwards; greyed with why where Pro can't be bought here.
//  - **Restore a purchase**: the Store asked again, how it went under it.
//
// The Store's rows are where the page had its Microsoft Store section: not for a developer build, and
// Get Karpo Pro not where Pro is had. Its row on ⚙ says what this copy is at its end.

namespace Karpo.Core;

/// <summary>
/// §339: the licence as the dock has it now, and what asks the Store: <paramref name="Status"/> (StoreLicense.Status),
/// whether there is a Store to ask (<paramref name="HasStore"/>) and Pro to buy there (<paramref name="CanPurchase"/>);
/// <paramref name="Purchase"/> shows the Store's purchase dialog, <paramref name="Refresh"/> asks it again.
/// </summary>
public sealed record KarpoLicence(LicenseStatus Status, bool HasStore, bool CanPurchase, Action Purchase, Action Refresh)
{
    /// <summary>Pro's price as the Store shows it to this person ("CHF 9.00"), once known; null for none yet.</summary>
    public string? Price { get; init; }

    /// <summary>How the last purchase ended, in words (LicenseView.Purchase); null for none since the dock started.</summary>
    public string? Purchased { get; init; }

    /// <summary>How asking the Store again went, in words; null for not asked.</summary>
    public string? Asked { get; init; }
}

/// <summary>§339: the licence's part of what ⚙ is given.</summary>
public sealed partial record KarpoServices
{
    /// <summary>The licence and the Store (<see cref="KarpoLicence"/>); null where there is none to show, and then no Karpo Pro row.</summary>
    public KarpoLicence? Licence { get; init; }
}

public static partial class KarpoMenu
{
    /// <summary>The licence's state's row.</summary>
    public const string ProStatusRow = "ProStatus";

    /// <summary>A layout's widgets' row: this, then the layout's id.</summary>
    public const string ProLayoutRow = "ProLayout.";

    /// <summary>The layouts' row.</summary>
    public const string ProLayoutsRow = "ProLayouts";

    /// <summary>What counts as a widget's row.</summary>
    public const string ProCountsRow = "ProCounts";

    /// <summary>Get Karpo Pro's row.</summary>
    public const string GetProRow = "GetPro";

    /// <summary>Restore a purchase's row.</summary>
    public const string RestoreRow = "ProRestore";

    // Segoe Fluent Icons: the shop bag (the License page's and the add list's "Get Karpo Pro"), InfoSolid (Info, E946,
    // is drawn as Explorer's Properties in a menu, ExplorerIcons), Completed, Refresh.
    private const string GlyphShop = "\uE719";
    private const string GlyphInfo = "\uF167";
    private const string GlyphPro = "\uE930";
    private const string GlyphRefresh = "\uE72C";

    /// <summary>
    /// "Karpo Pro ›": the licence of <paramref name="services"/> as the License page shows it (see the top of
    /// the file), <paramref name="config"/>'s layouts counted; none where the licence isn't given.
    /// </summary>
    public static IReadOnlyList<OptionRow> Pro(Config config, KarpoServices services)
    {
        if (services.Licence is not { } licence)
        {
            return [];
        }
        var status = licence.Status;
        // §517: the licence at its row's end ("Free version").
        OptionRow In(OptionRow row) => row with { Group = S.SettingsLicenseProTitle, GroupGlyph = GlyphShop, GroupSummary = LicenseView.Title(status) };
        List<OptionRow> rows =
        [
            new OptionRow(ProStatusRow, OptionRowKind.Note, LicenseView.Title(status))
            {
                Glyph = status.IsLimited ? GlyphInfo : GlyphPro,
                Why = LicenseView.Detail(status, until => RegionFormats.FollowedDate(until.LocalDateTime, @long: false) ?? until.LocalDateTime.ToShortDateString()),
            },
        ];
        if (LicenseView.ShowsUsage(status))
        {
            rows.AddRange(config.Layouts.Where(layout => layout is not null).Select(layout => new OptionRow(ProLayoutRow + layout.Id, OptionRowKind.Note, layout.Name)
            {
                Glyph = BarLooks.GlyphOf(layout),
                Why = S.SettingsLicenseUsageLayoutWidgets,
                Summary = LicenseView.Of(LicenseCount.Widgets(layout), LicenseRules.FreeWidgets),
            }));
            rows.Add(new OptionRow(ProLayoutsRow, OptionRowKind.Note, S.SettingsLicenseUsageLayouts)
            {
                // §411 (R13): apart from the layouts' own over it.
                Glyph = OptionGlyphs.Tiles,
                Summary = LicenseView.Of(config.Layouts.Count, LicenseRules.FreeLayouts),
            });
            rows.Add(new OptionRow(ProCountsRow, OptionRowKind.Note, S.SettingsLicenseCountsHeader)
            {
                // §411 (R13): a question's, apart from the state's.
                Glyph = OptionGlyphs.Help,
                Why = string.Join("\n", S.SettingsLicenseCountsDescription, S.SettingsLicenseCountsFolder, S.SettingsLicenseCountsFree, S.SettingsLicenseCountsHidden),
            });
        }
        if (LicenseView.ShowsStore(status))
        {
            if (LicenseView.OffersPro(status))
            {
                rows.Add(new OptionRow(GetProRow, OptionRowKind.Action, licence.Price is { Length: > 0 } price ? S.SettingsLicenseBuyPrice(price) : S.CoreLicenseGetPro)
                {
                    Glyph = GlyphShop,
                    Enabled = licence.CanPurchase,
                    Why = licence.Purchased ?? LicenseView.Pitch(licence.CanPurchase, licence.HasStore),
                    Set = _ =>
                    {
                        licence.Purchase();
                        return false;
                    },
                });
            }
            rows.Add(new OptionRow(RestoreRow, OptionRowKind.Action, S.SettingsLicenseRestoreHeader)
            {
                Glyph = GlyphRefresh,
                Enabled = licence.HasStore,
                Why = licence.Asked ?? S.SettingsLicenseRestoreDescription,
                Set = _ =>
                {
                    licence.Refresh();
                    return false;
                },
            });
        }
        return [.. rows.Select(In)];
    }
}
