// What the library shows, on each of its pages (docs\widgets.md §7; the split, S7).
//
// A widget is one of two types, and the type is where it stands: a taskbar entry stands on the
// bar, a list widget is a line of a list folder. Each type has its own registry (BarEntries,
// ListWidgets), so the library has two pages and each lists exactly its registry, grouped by
// category. Nothing here decides membership: a widget is on a page exactly when that page's
// registry has it. The Grid page went with the grid folder (2026-10-09).
//
// A row is a **member of one registry**, and everything a row needs — its name, its icon, its
// blurb, the words it is found by, its group — is on its WidgetInfo, which the registries share.
// So a clock on the Taskbar page and a clock on the List page cannot be called two different
// things, and the settings page and the bar's own "+" cannot drift.
//
// What the add list offers is a page arranged as a tree of subjects (AddTree.cs), with the bar's
// presets and a folder's premade widgets (PremadeWidgets.cs) among them.
//
// The page that is active follows where the person is adding: a place on the bar opens Taskbar, a
// list folder opens List. The other can be browsed, and its rows say where they would go instead.
//
// This replaced the Widgets and Components pages, which were two views of one registry cut by a
// flag on each kind (WidgetKind.Offer); a flag decided by hand is what the split was for.

namespace Karpo.Core;

/// <summary>One row of the library: a member of one type's registry, as the add list draws it and the search reads it.</summary>
public sealed record CatalogueEntry
{
    /// <summary>
    /// What names the row within its page: the widget's id, config.json's word. The same id is on
    /// more than one page ("clock" is a taskbar entry and a list widget), so a row is named by its
    /// page and its id together (<see cref="Key"/>).
    /// </summary>
    public required string Id { get; init; }

    /// <summary>Which page, and so which type, the row is.</summary>
    public required WidgetType Type { get; init; }

    public required string Title { get; init; }

    public required string Glyph { get; init; }

    public required string Blurb { get; init; }

    public required IReadOnlyList<string> Keywords { get; init; }

    /// <summary>
    /// Its English name and line, where Karpo speaks another language (its English search words
    /// are in <see cref="Keywords"/> already); empty in English. The search reads them too, so a
    /// widget is found by the name somebody knows it by from English as well.
    /// </summary>
    public IReadOnlyList<string> English { get; init; } = [];

    public required WidgetCategory Category { get; init; }

    /// <summary>The taskbar entry a click places; set exactly on a row of the Taskbar page.</summary>
    public BarKind? Bar { get; init; }

    /// <summary>The list widget a click places; set exactly on a row of the List page.</summary>
    public ListKind? List { get; init; }

    /// <summary>
    /// The premade widget a click places (<see cref="PremadeWidgets"/>): its widget, set up as it
    /// says; null on a widget's own row. <see cref="Id"/> stays its widget's word.
    /// </summary>
    public PremadeWidget? Premade { get; init; }

    /// <summary>The row's name across the pages: "list/clock", "list/actions/bluetoothActions".</summary>
    public string Key => Premade is { } premade ? $"{KeyOf(Type, Id)}/{premade.Id}" : KeyOf(Type, Id);

    /// <summary>A row's name across the pages.</summary>
    public static string KeyOf(WidgetType type, string id) => $"{type.ToString().ToLowerInvariant()}/{id}";

    /// <summary>The row for a taskbar entry.</summary>
    public static CatalogueEntry Of(BarEntry entry) => Of(WidgetType.Bar, entry.Info) with { Bar = entry.Kind };

    /// <summary>
    /// The row for a list widget. A widget with styles is a row per style it offers
    /// (<see cref="StyledWidget.Offered"/>), named and found as the widget: "Calendar", found by
    /// "week" as by "agenda"; a widget with readings a row per reading, "Memory", found by "ram" (§283).
    /// </summary>
    public static CatalogueEntry Of(ListWidget widget) => WidgetStyles.Of(widget.Kind) is { Widget: var styled, Style: var style }
        ? Of(WidgetType.List, widget.Info) with
        {
            List = widget.Kind,
            Title = styled.TitleOf(style),
            Glyph = styled.GlyphOf(style),
            Blurb = styled.BlurbOf(style),
            Keywords = styled.KeywordsOf(style),
            English = [.. styled.EnglishWordsOf(style)],
        }
        : Of(WidgetType.List, widget.Info) with { List = widget.Kind };

    /// <summary>
    /// The row for a premade widget: its own name, icon and the names of what it is set to, in its
    /// widget's group, found by its widget's words as well.
    /// </summary>
    public static CatalogueEntry Of(PremadeWidget premade)
    {
        var widget = Of(ListWidgets.Of(premade.Kind));
        return widget with
        {
            Title = premade.Title,
            Glyph = premade.Glyph,
            Blurb = premade.Blurb,
            Keywords = [widget.Title, .. widget.Keywords],
            English = [.. premade.EnglishWords, .. widget.English],
            Premade = premade,
        };
    }

    private static CatalogueEntry Of(WidgetType type, WidgetInfo info) => new()
    {
        Id = info.Id,
        Type = type,
        // The add list's name: a toggle's says "on/off" (WidgetInfo.AddTitle), which holds its title.
        Title = info.AddTitle,
        Glyph = info.Glyph,
        Blurb = info.Blurb,
        Keywords = info.Keywords,
        English = [.. info.EnglishWords, .. info.EnglishAddWords],
        Category = info.Category,
    };

    /// <summary>
    /// Two rows are the same row when they say the same: the word lists by their words, not by
    /// which list holds them. A widget's words are read afresh each time they are asked for (in
    /// the language Karpo speaks now), so the same widget's row, made twice, holds two lists.
    /// </summary>
    public bool Equals(CatalogueEntry? other) =>
        other is not null
        && Id == other.Id
        && Type == other.Type
        && Title == other.Title
        && Glyph == other.Glyph
        && Blurb == other.Blurb
        && Keywords.SequenceEqual(other.Keywords)
        && English.SequenceEqual(other.English)
        && Category == other.Category
        && Bar == other.Bar
        && List == other.List
        && Premade == other.Premade;

    public override int GetHashCode() => HashCode.Combine(Type, Id, Title, Category, Bar, List, Premade);
}

/// <summary>The library's pages, as rows, and what each page says about itself.</summary>
public static class Catalogue
{
    /// <summary>The pages, in the order they are shown.</summary>
    public static readonly WidgetType[] Pages = [WidgetType.Bar, WidgetType.List];

    /// <summary>
    /// What <paramref name="page"/> shows: exactly that type's registry, group by group, and within
    /// a group in the registry's own order — the order a bar shows them when nobody has moved
    /// anything, which is also the order a person scanning a group expects.
    /// </summary>
    public static IReadOnlyList<CatalogueEntry> On(WidgetType page)
    {
        var members = Members(page);
        var rows = new List<CatalogueEntry>(members.Count);
        foreach (var category in Enum.GetValues<WidgetCategory>())
        {
            foreach (var row in members)
            {
                if (row.Category == category)
                {
                    rows.Add(row);
                }
            }
        }
        return rows;
    }

    /// <summary>Every row of every page, each page's in its own order: what a model holds so it can show any of them.</summary>
    public static IReadOnlyList<CatalogueEntry> Everything() => [.. Pages.SelectMany(On)];

    /// <summary>The page's registry, as rows, in the registry's order.</summary>
    private static List<CatalogueEntry> Members(WidgetType page) => page switch
    {
        // A widget with styles once, as its first style, the others its styles, not rows; once a reading for a widget with readings (WidgetStyles, §283).
        WidgetType.List => [.. ListWidgets.All.Where(widget => WidgetStyles.Of(widget.Kind) is not { } styled || styled.Widget.Offered.Contains(styled.Style)).Select(CatalogueEntry.Of)],
        _ => [.. BarEntries.All.Select(CatalogueEntry.Of)],
    };

    /// <summary>What the page's tab says.</summary>
    public static string Title(this WidgetType page) => page switch
    {
        WidgetType.List => S.CoreCataloguePageList,
        _ => S.CoreCataloguePageTaskbar,
    };

    /// <summary>What one widget of the type is called, in a heading: "Taskbar entry".</summary>
    public static string Name(this WidgetType type) => type switch
    {
        WidgetType.List => S.CoreCatalogueNameList,
        _ => S.CoreCatalogueNameTaskbar,
    };

    /// <summary>What the page says about itself, under its tab: one line, in the user's own terms.</summary>
    public static string Blurb(this WidgetType page) => page switch
    {
        WidgetType.List => S.CoreCatalogueBlurbList,
        _ => S.CoreCatalogueBlurbTaskbar,
    };

    /// <summary>
    /// Why a widget is not offered on this PC at all, as Windows leaves its own tile out where the
    /// PC cannot do the thing: rotation lock where the screen does not turn
    /// (<paramref name="turns"/>). Null for everything this PC has. A few words: the add list
    /// shows them in the row, under the widget's name.
    /// </summary>
    public static string? WhyNotHere(WidgetInfo? widget, bool turns) =>
        widget?.Switch == QuickSwitch.RotationLock && !turns ? S.CoreCatalogueScreenDoesNotTurn : null;

    /// <summary>
    /// Why <paramref name="row"/> cannot be added to a place of its own type, as the add list greys
    /// it: what this PC has not got (<see cref="WhyNotHere"/>). Null where it can.
    /// </summary>
    public static string? WhyNot(CatalogueEntry row, bool turns) => WhyNotHere(WidgetInfos.Find(row.Id), turns);

    /// <summary>
    /// The type a widget standing in a folder of <paramref name="view"/> is (<see cref="Partners.TypeOf"/>),
    /// and null — no folder at all — the bar: the one question the library asks that the partners do
    /// not, because a place to add to may be the bar itself.
    /// </summary>
    public static WidgetType TypeOf(FolderView? view) => view is { } folder ? Partners.TypeOf(folder) : WidgetType.Bar;
}
