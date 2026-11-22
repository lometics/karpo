// tray: where each notification icon shows, as rows of the tray's menu in edit mode (docs\widgets.md
// §261, the person: "tray icon configuration should go into the tray icon menu and not be shown in
// general settings"). One submenu, "Tray icons", a row per icon the tray has now — its own picture as
// the bar draws it (OptionRow.Picture), its app's name, and at its end where it shows — each opening
// Windows 11's two choices and Karpo's third: Show on taskbar, Show in hidden icon menu, Hide
// completely (TrayState), as the status icons' rows offer theirs. At the row's end each is said short,
// "On taskbar", "Behind arrow", "Hidden" (Said, §323): the sentences were cut there to "Show on task…",
// the name and the value sharing the row (§315).
//
// §347: they are the tray's own (BarItem.TrayIcons, the person: "scope these settings to the widget
// even"): a tray's menu offers its own icons' places, and a row writes into that tray alone, in the copy
// of the config the dock saves; the dock tells Windows' own record (PromoteChoices). The icons are the
// live ones the caller listed (OptionDevices.TrayIcons); without them there are none. Settings' General
// page had them from §228 until §261; it has none any more.
//
// Edit mode's menu and not the menu at rest, as every setting is since §257: where an icon shows is how
// the tray looks. At rest an icon is still dragged onto the chevron or back; one hidden completely shows
// nowhere to be dragged, and these rows are the way back for it.

namespace Karpo.Core;

/// <summary>
/// A notification icon as the tray's menu lists it: what <see cref="TrayIconOptions.States"/> names it by,
/// its app's name (<see cref="OptionTray.NameOf"/>), whether Windows' own choice for it is the hidden icon
/// menu, and the first line of its tooltip now.
/// </summary>
public sealed record TrayMenuIcon(string Identity, string Name, bool AppHidden, string Tip = "");

/// <summary>§261: the tray's rows' parts of a row (<see cref="OptionTray"/>).</summary>
public sealed partial record OptionRow
{
    /// <summary>
    /// §261: a picture the menu has of its own for the row, by what it names — a tray icon's identity,
    /// whose live picture the dock draws where the row's <see cref="Glyph"/> would be; null for the glyph.
    /// </summary>
    public string? Picture { get; init; }
}

/// <summary>The tray's rows of its menu; see the top of the file.</summary>
public static class OptionTray
{
    /// <summary>An icon's row: where it shows, then its identity (<see cref="TrayMenuIcon.Identity"/>).</summary>
    public const string Icon = "TrayIcon.";

    /// <summary>The three places, in the rows' order: on the taskbar, in the hidden icon menu, hidden completely.</summary>
    public static IReadOnlyList<TrayState> States { get; } = [TrayState.Taskbar, TrayState.Overflow, TrayState.Hidden];

    /// <summary>
    /// §323: the three places as a row says them at its end, short enough to be read whole beside an app's
    /// name (<see cref="OptionRow.ChoicesSaid"/>); the status icons' rows' too, whose arrow is theirs.
    /// </summary>
    public static IReadOnlyList<string> Said => [S.SettingsTraySaidTaskbar, S.SettingsTraySaidArrow, S.SettingsTraySaidHidden];

    /// <summary>The rows into <paramref name="rows"/>, for a tray, its icons listed.</summary>
    public static void Into(WidgetItem? item, OptionDevices? devices, List<OptionRow> rows)
    {
        if (item is BarItem { Kind: BarKind.Tray } tray && devices?.TrayIcons is { } icons)
        {
            rows.AddRange(Rows(tray, icons));
        }
    }

    /// <summary>
    /// <paramref name="icons"/> as rows of one group (<see cref="OptionRow.Group"/>), in the order
    /// <paramref name="tray"/> has them (<see cref="TrayLayout.InOrder"/>), each once: a choice of the
    /// three, the one in force its state in this tray or Windows' choice where nobody chose
    /// (<see cref="TrayLayout.State"/>), set into this tray as a drag sets it, its place in the order left
    /// alone (<see cref="TrayLayout.Set"/>). An icon whose name another shares — two of Explorer's, two
    /// windows of one app — is called by its tooltip, which tells them apart. None for no icons.
    /// </summary>
    public static IReadOnlyList<OptionRow> Rows(BarItem tray, IReadOnlyList<TrayMenuIcon> icons)
    {
        var unique = icons.Where(icon => !string.IsNullOrWhiteSpace(icon.Identity))
            .DistinctBy(icon => icon.Identity, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var byIdentity = unique.ToDictionary(icon => icon.Identity, StringComparer.OrdinalIgnoreCase);
        var shared = unique.GroupBy(icon => icon.Name, StringComparer.CurrentCultureIgnoreCase)
            .Where(names => names.Count() > 1)
            .Select(names => names.Key)
            .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        var rows = new List<OptionRow>();
        foreach (var entry in TrayLayout.InOrder(tray.TrayIcons, [.. unique.Select(icon => new TrayEntry(icon.Identity, icon.AppHidden))]))
        {
            var icon = byIdentity[entry.Identity];
            var chosen = IndexOf(TrayLayout.State(tray.TrayIcons, entry));
            var title = shared.Contains(icon.Name) && icon.Tip.Trim() is { Length: > 0 } tip ? tip : icon.Name;
            rows.Add(new OptionRow(Icon + icon.Identity, OptionRowKind.Choice, title)
            {
                Glyph = WidgetInfos.Tray.Glyph,
                Picture = icon.Identity,
                Choices = [S.SettingsTrayShowOnTaskbar, S.SettingsTrayShowInMenu, S.SettingsTrayHide],
                ChoicesSaid = Said,
                ChoiceGlyphs = [OptionGlyphs.Taskbar, OptionGlyphs.More, OptionGlyphs.Hide],
                Chosen = chosen,
                Set = at => at >= 0 && at < States.Count && at != chosen && TrayLayout.Set(tray.TrayIcons ??= new(), icon.Identity, States[at]),
                Group = S.SettingsTrayIconsHeader,
                GroupGlyph = WidgetInfos.Tray.Glyph,
                Slot = OptionSlot.Subject,
            });
        }
        // §517 (R4): the group says how many show, on the taskbar or behind its arrow.
        var said = S.DockEditShownCount(rows.Count(row => row.Chosen < States.Count - 1));
        return [.. rows.Select(row => row with { GroupSummary = said })];
    }

    /// <summary>The place of <paramref name="state"/> among the rows' choices (<see cref="States"/>).</summary>
    public static int IndexOf(TrayState state) => state switch
    {
        TrayState.Taskbar => 0,
        TrayState.Overflow => 1,
        _ => 2,
    };

    /// <summary>
    /// Explorer's own volume icon, which it adds to the notification area by this GUID
    /// ({7820AE73-…}, beside "Safely remove hardware"'s {7820AE78-…}). Its executable is Explorer, whose
    /// name ("Windows Explorer") says nothing of the speaker on the bar.
    /// </summary>
    public const string VolumeIcon = "{7820ae73-23e3-4229-82c1-e41cb67d5b9c}";

    /// <summary>Whether <paramref name="identity"/> is Explorer's volume icon's (<see cref="VolumeIcon"/>), a second one's (#2) too.</summary>
    public static bool IsVolume(string identity) =>
        string.Equals(TrayNames.Base(identity), VolumeIcon, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// An icon's name as its row says it, as Windows 11's "Other system tray icons" names it: what its
    /// app calls itself — its executable's <paramref name="description"/>, else its
    /// <paramref name="product"/> name —, else the first line of its <paramref name="tip"/>, else its
    /// executable's file name; "Tray icon" where there is nothing at all. Explorer's volume icon is
    /// Windows' word for it, "Volume".
    /// </summary>
    public static string NameOf(string identity, string? path, string? tip, string? description, string? product)
    {
        if (IsVolume(identity))
        {
            return S.SettingsTrayVolume;
        }
        foreach (var said in new[] { description, product, tip?.Split('\n', 2)[0] })
        {
            if (!string.IsNullOrWhiteSpace(said) && said.Trim() != TrayCatalogue.NoName)
            {
                return said.Trim();
            }
        }
        return string.IsNullOrWhiteSpace(path) ? TrayCatalogue.NoName : System.IO.Path.GetFileNameWithoutExtension(path.Trim());
    }
}
