// licence: what the free version's limit does to the dock's add list and its refusals
// (docs\widgets.md §138). Its words and its sums are Karpo.Core's (LicenseGate, LicenseCount); this
// is where the dock shows them. The person: the user must know why the limit was reached — so a
// layout at its limit opens the add list with "Get Karpo Pro" at the top, "20 of 20 widgets used"
// under it, every row that would count greyed with "Needs Karpo Pro" as its reason (as a row this PC
// can't have is greyed with Catalogue.WhyNot's), and the rows that don't count (a pin,
// Windows' own buttons) as they were. An add refused anyway (Copy, Ctrl+V, the edit bar's
// Folder) says why in a small "Free version" flyout with the same way to the License page. The
// License page says all of it in sentences, and what counts.
//
// Plain code without a window, so the tests read exactly what the list shows.

using Karpo.Core;

namespace Karpo.Dock;

public static class LicenseRows
{
    /// <summary>Segoe Fluent Icons' shop bag: "Get Karpo Pro".</summary>
    public const string Glyph = "";

    /// <summary>
    /// The add list's <paramref name="items"/> (<see cref="AddList.Rows"/>, its widgets
    /// <paramref name="entries"/> from <paramref name="firstId"/> and its ready-made folders
    /// <paramref name="presets"/> after them) for a layout the free version holds no more widgets on:
    /// each row that would count greyed with <paramref name="reason"/> ("Needs Karpo Pro"), and
    /// first under the first heading a row <paramref name="getPro"/> that opens the License page,
    /// <paramref name="used"/> under it ("20 of 20 widgets used"). A ready-made folder is a folder on
    /// the bar and always counts. The rows' second lines are short: the list's columns cut a sentence.
    /// </summary>
    public static List<FlyoutItem> Limited(IReadOnlyList<FlyoutItem> items, IReadOnlyList<CatalogueEntry> entries, IReadOnlyList<Preset> presets, uint firstId, string reason, string used, uint getPro)
    {
        bool Counts(uint id) =>
            id >= firstId && id - firstId < (uint)entries.Count
                ? entries[(int)(id - firstId)].Bar is not { } kind || LicenseCount.Counts(kind)
                : id >= firstId && id - firstId < (uint)(entries.Count + presets.Count);

        FlyoutItem Grey(FlyoutItem item) => item switch
        {
            { Children: { } children } => item with { Children = [.. children.Select(Grey)] },
            { Header: false, Separator: false, Enabled: true } when Counts(item.Id) => item with { Enabled = false, Detail = reason },
            _ => item,
        };

        var limited = items.Select(Grey).ToList();
        var first = limited.FindIndex(item => item.Header);
        limited.Insert(first + 1, Offer(getPro, used));
        return limited;
    }

    /// <summary>The row that opens the License page: "Get Karpo Pro", <paramref name="why"/> under it.</summary>
    public static FlyoutItem Offer(uint id, string why) =>
        new(id, S.CoreLicenseGetPro, new FlyoutIcon.Glyph(Glyph), Detail: why);

    /// <summary>
    /// The flyout an add refused by the licence opens, under "Free version": the limit, how much of it
    /// the layout's <paramref name="count"/> widgets use, and the row that opens ⚙ › Karpo Pro
    /// (<paramref name="getPro"/>). Short lines, as a menu's are: a sentence would be cut.
    /// </summary>
    public static List<FlyoutItem> Refusal(int count, uint getPro) =>
    [
        new(0, S.DockLicenseWidgets(LicenseRules.FreeWidgets), Detail: Used(count), Enabled: false),
        new(0, "", Separator: true),
        new(getPro, S.CoreLicenseGetPro, new FlyoutIcon.Glyph(Glyph)),
    ];

    /// <summary>"20 of 20 widgets used": what "Get Karpo Pro" says under it in the add list, and a refusal under the limit.</summary>
    public static string Used(int count) => S.DockLicenseUsed(count: count, limit: LicenseRules.FreeWidgets);
}
