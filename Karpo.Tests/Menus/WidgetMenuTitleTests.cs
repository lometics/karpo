// A widget's right-click menu draws no title (the person, 2026-10-10: "in the right click context
// menu for all taskbar widgets you should remove their title, e.g. Folder - Calendar"): its first row
// is its first act, at rest and while the bar is arranged, for every taskbar entry and every line of
// a list. The widget's name stays the menu's name for a screen reader (Dock.WidgetMenuName,
// Flyout.SpokenName).
//
// The drawn half needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed unsafe class WidgetMenuTitleTests
{
    /// <summary>A widget's buttons in their order: Cut, Copy, Remove.</summary>
    private static readonly List<FlyoutItem> Buttons = TheDock.WidgetButtons();

    /// <summary>Every widget's menu, at rest and arranged: each taskbar entry's, the desktops', and each line's in a list.</summary>
    private static IEnumerable<(string Name, List<FlyoutItem> Rows)> Menus()
    {
        foreach (var entry in BarEntries.All)
        {
            var island = entry.Kind.New();
            yield return ($"{entry.Kind} at rest", TheDock.RestMenuRows(island, inFolder: false).Items);
            yield return ($"{entry.Kind} arranged", TheDock.WidgetRowsFor(island));
        }
        yield return ("Desktops at rest", TheDock.DesktopRows(Switchers.ActionsFor(0, current: 0, count: 2, shell: true)));
        foreach (var kind in Enum.GetValues<ListKind>())
        {
            var line = kind.New();
            yield return ($"{kind} line at rest", TheDock.RestMenuRows(line, inFolder: true).Items);
            yield return ($"{kind} line arranged", TheDock.WidgetRowsFor(line));
        }
    }

    /// <summary>
    /// No widget's menu opens with a heading, or with the separator a title would have stood over:
    /// the first row is something to pick.
    /// </summary>
    [Fact]
    public void NoWidgetsMenuStartsWithATitleOrASeparator()
    {
        var installed = WindowsButtons.VersionOf;
        WindowsButtons.VersionOf = _ => new Version(11, 2607);
        try
        {
            foreach (var (name, rows) in Menus())
            {
                Assert.NotEmpty(rows);
                Assert.False(rows[0].Header, $"{name}: opens with the heading {rows[0].Text}");
                Assert.False(rows[0].Separator, $"{name}: opens with a separator");
                Assert.NotEqual(0u, rows[0].Id);
                Assert.False(rows[^1].Separator, $"{name}: ends with a separator");
            }
        }
        finally
        {
            WindowsButtons.VersionOf = installed;
        }
    }

    /// <summary>The menu is still named for its widget — its header, or its kind for one the file has lost.</summary>
    [Fact]
    public void AWidgetsMenuIsNamedForItsWidget()
    {
        var clock = ListKind.Clock.New();
        clock.Clock!.Label = "Tokyo";
        Assert.Equal(IslandEdits.Header(clock), TheDock.WidgetMenuName(clock, unknown: null));
        Assert.Equal("Clock · Tokyo", TheDock.WidgetMenuName(clock, unknown: null));
        Assert.Equal("Weather", TheDock.WidgetMenuName(null, "Weather"));
        Assert.Null(TheDock.WidgetMenuName(null, null));
    }

    /// <summary>
    /// §314, the person: "the widget titles in the edit menus should just be the widget type, not anything
    /// else like place or so". What an edit menu says it is of is the widget's type alone, for every kind
    /// there is — a clock with its zone and label, a weather line with its place, a pin with its target, a
    /// named folder with faces among them —, and none of its settings is a heading of it any more: the
    /// type is the start of the menu's top row (Flyout.Primary.cs, §323). The menu still opens with its buttons.
    /// </summary>
    [Fact]
    public void AnEditMenuIsNamedByTheWidgetsTypeAlone()
    {
        var clock = ListKind.Clock.New();
        clock.Clock!.Label = "Tokyo";
        clock.Clock.TimeZone = "Tokyo Standard Time";
        var weather = ListKind.WeatherNow.New();
        weather.Weather = new WeatherOptions { Place = "Zürich", Latitude = 47.37, Longitude = 8.54 };
        var pin = BarKind.Pin.New();
        pin.Pin!.Target = @"C:\Users\Ada\Documents\budget.xlsx";
        var folder = BarKind.FolderList.New();
        folder.Folder.Faces = [FolderFace.Of(FolderFaces.ClockId), FolderFace.Of(FolderFaces.SoundId)];
        Assert.Equal(
            [WidgetStyles.Clock.Title, WidgetInfos.WeatherNow.Title, WidgetInfos.Pin.Title, S.CoreWidgetFolderListTitle],
            new WidgetItem[] { clock, weather, pin, folder }.Select(item => IslandEdits.Kind(item)));

        IEnumerable<WidgetItem> every = [.. Enum.GetValues<BarKind>().Select(kind => (WidgetItem)kind.New()), .. Enum.GetValues<ListKind>().Select(kind => kind.New()), clock, weather, pin, folder];
        foreach (var item in every)
        {
            var kind = IslandEdits.Kind(item);
            Assert.Equal(item is BarItem { Folder: not null } ? S.CoreWidgetFolderListTitle : WidgetStyles.Of(item)?.Widget.Title ?? item.Info?.Title ?? S.DockMenuWidget, kind);
            List<FlyoutItem> menu = [.. TheDock.WidgetRowsFor(item), .. OptionMenu.Settings(item, [], new OptionDevices(), null, FolderSide.Face, roomCells: null)];
            Assert.True(menu[0].Primary, $"{kind}: opens with {menu[0].Text}");
            Assert.DoesNotContain(menu, row => row.Header && (row.Text == kind || row.Text == IslandEdits.Header(item)));
        }

        // The dock names every configuration menu by it, a widget's and a bar's.
        var repo = RepoSource.Root();
        Assert.Contains("TypeTitle = !(arranged || configure) ? null : island is not null ? IslandEdits.Kind(island) : unknown,", File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.cs")));
        Assert.Contains("TypeTitle = BarLooks.Kind(config, bar, what.LooksScope)", File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.BarLooks.cs")));
        // §314's caption over the menu is gone, its file and the host's room for it.
        Assert.False(File.Exists(Path.Combine(repo, "Karpo.Dock", "Flyout.Caption.cs")));
        foreach (var file in new[] { "Flyout.cs", "FlyoutHost.cs", "Dock.cs", "Flyout.Primary.cs" })
        {
            Assert.DoesNotContain("Caption", File.ReadAllText(Path.Combine(repo, "Karpo.Dock", file)).Replace("LiveCaptions", "", StringComparison.Ordinal), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// §323, the person: "some type names read oddly: 'Conditions' for weather, 'Processor graph'". A line of
    /// a widget of styles or readings is named by that widget, as the add list offers it and its Style and
    /// Reading rows change it — whatever its style, whatever it reads —, never by its form's catalogue name;
    /// in English and in German.
    /// </summary>
    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    public void ALinesTypeIsTheWidgetItIsAStyleOf(string language)
    {
        using var speaking = InLanguage.Use(language);
        (ListKind Kind, StyledWidget Widget)[] lines =
        [
            (ListKind.WeatherHours, WidgetStyles.Hourly), (ListKind.WeatherHoursList, WidgetStyles.Hourly),
            (ListKind.WeatherDays, WidgetStyles.Daily), (ListKind.WeatherDaysGraph, WidgetStyles.Daily),
            (ListKind.Cpu, WidgetStyles.Performance), (ListKind.Gpu, WidgetStyles.Performance), (ListKind.Memory, WidgetStyles.Performance),
            (ListKind.Disk, WidgetStyles.Performance), (ListKind.Network, WidgetStyles.Performance),
            (ListKind.CpuDetails, WidgetStyles.PerformanceDetails), (ListKind.NetworkDetails, WidgetStyles.PerformanceDetails),
            (ListKind.Calendar, WidgetStyles.Calendar), (ListKind.Events, WidgetStyles.Calendar), (ListKind.WeekView, WidgetStyles.Calendar),
            (ListKind.Clock, WidgetStyles.Clock), (ListKind.TimeHeader, WidgetStyles.Clock),
        ];
        foreach (var (kind, widget) in lines)
        {
            Assert.Equal(widget.Title, IslandEdits.Kind(kind.New()));
        }
        // §590: the current weather is a widget of its own, of no style.
        Assert.Equal(language == "de" ? "Aktuelles Wetter" : "Current weather", IslandEdits.Kind(ListKind.WeatherNow.New()));
        Assert.Equal(language == "de" ? "Leistung" : "Performance", IslandEdits.Kind(ListKind.Cpu.New()));
        // No line is called by its form's catalogue name where that is not the widget's.
        foreach (var kind in Enum.GetValues<ListKind>())
        {
            var line = kind.New();
            if (WidgetStyles.Of(line) is { } styled && styled.Widget.Title != line.Info?.Title)
            {
                Assert.NotEqual(line.Info?.Title, IslandEdits.Kind(line));
            }
        }
        // A widget of one style keeps its own name: today's date, a pin, the volume mixer.
        Assert.Equal(WidgetInfos.DateHeader.Title, IslandEdits.Kind(ListKind.DateHeader.New()));
        Assert.Equal(WidgetInfos.SoundMixer.Title, IslandEdits.Kind(ListKind.SoundMixer.New()));
    }

    /// <summary>
    /// §286 ("the title of the folder was repeated many times in the face edit context menu"), §288 ("right
    /// click folder widget configuration should only show one header"), §314: a folder's edit menu has no
    /// heading at all, "Add a face" right under its faces, through every Refresh.
    /// </summary>
    [Fact]
    public void AFoldersEditMenuHasNoHeadingThroughEveryRefresh()
    {
        var folder = BarKind.FolderList.New();
        folder.Id = "box";
        folder.Folder!.Faces = [FolderFace.Of(FolderFaces.ClockId), FolderFace.Of(FolderFaces.CalendarId), FolderFace.Of(FolderFaces.SoundId)];
        List<FlyoutItem> Settings(List<OptionPick> picks) =>
            [.. OptionMenu.Section(OptionRows.Of(folder), picks), .. OptionMenu.FaceSection(folder.Folder, picks)];
        void NoHeading(List<FlyoutItem> menu)
        {
            Assert.DoesNotContain(menu, item => item.Header);
            var last = menu.FindLastIndex(item => item.Reorder == OptionMenu.FaceGroup);
            Assert.Equal(S.SettingsFolderFacesAddHeader, menu[last + 1].Text);
        }

        List<OptionPick> picks = [];
        List<FlyoutItem> menu = [.. TheDock.WidgetRowsFor(folder), .. Settings(picks)];
        NoHeading(menu);
        var count = menu.Count;
        for (var again = 0; again < 4; again++)
        {
            // A face moved, as a drag in the menu does, and the menu shown again.
            var move = picks.First(pick => pick.Kind == OptionPickKind.MoveFace && pick.Face == 0) with { Value = 1 };
            Assert.True(OptionMenu.Apply(folder, move));
            picks = [];
            menu = OptionMenu.Updated(menu, Settings(picks));
            NoHeading(menu);
            Assert.Equal(count, menu.Count);
        }
    }

    /// <summary>
    /// §323, the person: "actually move the title into the toprow inside the menu again" (§314 stood it on a
    /// plate over the menu). A configuration menu's type is the start of its top row, inside the panel, in
    /// the row of its buttons, which stand together at the row's far end — Cut, Copy, Remove, a hairline
    /// apart each, as in every menu (§350) — and the
    /// panel stands where and as tall as it does without one. It stays through a Refresh; a level shown in
    /// place has its head instead, and the type is back on the way out; Narrator names the menu by it. A menu
    /// with no buttons (the bar's looks) has the row for its type alone, its rows under it. In Arabic the
    /// row is turned: the type at the right, the buttons at the left.
    /// </summary>
    [UiFact]
    public void TheTypeStandsAtTheStartOfTheTopRow()
    {
        const float scale = 1.5f;
        var clock = ListKind.Clock.New();
        List<FlyoutItem> rows = [.. TheDock.WidgetRowsFor(clock), .. OptionMenu.Settings(clock, [], new OptionDevices(), null, FolderSide.Face, roomCells: LineHeight.Most)];
        var level = rows.First(row => LineHeightRows.Holds(row.Id) && row.Children is { Count: > 0 });
        List<FlyoutItem> looks = [new(1, "Size", Children: [new(11, "Small")], Value: new FlyoutValue("Small")), new(2, "Material", Children: [new(21, "Acrylic")])];
        WindowShots.OnHiddenDesktop("menu-type-title", (gfx, instance) =>
        {
            var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
            var look = AddListPictures.LookFor(true);
            int Px(float dip) => (int)MathF.Round(dip * scale, MidpointRounding.AwayFromZero);
            foreach (var edge in new[] { Edge.Bottom, Edge.Top })
            {
                var anchor = new POINT { x = work.right / 2, y = edge == Edge.Bottom ? work.bottom : 0 };
                RECT Panel(string? type)
                {
                    using var menu = new Flyout(gfx, instance, 0) { Configures = true, Configuration = true, TypeTitle = type, SpokenName = "Clock · Tokyo" };
                    menu.Show(gfx, look, scale, rows, FlyoutLayout.List, anchor, work, edge);
                    WindowShots.Settle();
                    var panel = menu.OpenPanels[0].Panel;
                    if (type is null)
                    {
                        Assert.Null(menu.TypeTitleOnScreen);
                        Assert.Equal("Clock · Tokyo", menu.UiaName);
                        menu.Hide();
                        return panel;
                    }
                    var words = menu.TypeTitleOnScreen!.Value;
                    var cut = menu.ItemOnScreen(Buttons[0].Id)!.Value;
                    var copy = menu.ItemOnScreen(Buttons[1].Id)!.Value;
                    var remove = menu.ItemOnScreen(Buttons[2].Id)!.Value;
                    // Inside the panel, a flyout title's 16 DIP in, upright in the buttons' row.
                    Assert.InRange(words.left - panel.left, Px(16) - 1, Px(16) + 1);
                    Assert.True(words.top > panel.top && words.bottom < cut.bottom + Px(Flyout.PrimaryPlateDip), $"{words.top}..{words.bottom} in {panel.top}, the row to {cut.bottom}");
                    Assert.InRange(((words.top + words.bottom) / 2) - ((cut.top + cut.bottom) / 2), -2, 2);
                    // The buttons together at the far end: Remove last, the gaps alike; all after the type.
                    Assert.InRange(panel.right - remove.right, Px(Flyout.PrimaryEdgeDip + Flyout.PrimaryPlateDip), Px(Flyout.PrimaryEdgeDip + Flyout.PrimaryPlateDip + 2));
                    Assert.True(copy.right < remove.left && cut.right < copy.left && words.right < cut.left);
                    Assert.Equal(copy.left - cut.right, remove.left - copy.right);
                    Assert.Equal(type, menu.UiaName);
                    // Shown again: where it stood.
                    menu.Refresh(rows);
                    WindowShots.Settle();
                    Assert.Equal(words, menu.TypeTitleOnScreen);
                    Assert.Equal(panel, menu.OpenPanels[0].Panel);
                    // A level in place has its head and no type; back on its own rows, the type again.
                    _ = menu.Click(level.Id);
                    Assert.Equal(1, menu.InlineDepth);
                    Assert.Null(menu.TypeTitleOnScreen);
                    _ = menu.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_ESCAPE, 0);
                    Assert.Equal(0, menu.InlineDepth);
                    Assert.Equal(words, menu.TypeTitleOnScreen);
                    Assert.True(menu.IsOpen);
                    menu.Hide();
                    return panel;
                }

                // The panel where and as tall as without one (§288): nothing over it any more.
                Assert.Equal(Panel(null), Panel(WidgetInfos.Clock.Title));
            }

            // No buttons: the type's row alone, the rows under it.
            using (var bar = new Flyout(gfx, instance, 0) { Configures = true, Configuration = true, TypeTitle = S.SettingsLayoutsTypeTaskbar })
            {
                bar.Show(gfx, look, scale, looks, FlyoutLayout.List, new POINT { x = work.right / 2, y = work.bottom }, work, Edge.Bottom);
                WindowShots.Settle();
                var panel = bar.OpenPanels[0].Panel;
                var words = bar.TypeTitleOnScreen!.Value;
                var first = bar.ItemOnScreen(1)!.Value;
                Assert.InRange(words.left - panel.left, Px(16) - 1, Px(16) + 1);
                Assert.True(words.top > panel.top && words.bottom < first.top, $"{words.top}..{words.bottom}, the first row from {first.top}");
                Assert.True(first.top - panel.top >= Px(Flyout.PrimaryButtonDip), $"the first row {first.top - panel.top} px down");
                Assert.Equal([1u, 2u], bar.RowsShown.Select(row => row.Id));
                bar.Hide();
            }

            // Arabic: turned, the type at the right and the buttons at the left.
            using (InLanguage.Use("ar"))
            {
                using var menu = new Flyout(gfx, instance, 0) { Configures = true, Configuration = true, TypeTitle = WidgetInfos.Clock.Title };
                menu.Show(gfx, look, scale, rows, FlyoutLayout.List, new POINT { x = work.right / 2, y = work.bottom }, work, Edge.Bottom);
                WindowShots.Settle();
                var panel = menu.OpenPanels[0].Panel;
                var words = menu.TypeTitleOnScreen!.Value;
                Assert.InRange(panel.right - words.right, Px(16) - 1, Px(16) + 1);
                var (cut, copy, remove) = (menu.ItemOnScreen(Buttons[0].Id)!.Value, menu.ItemOnScreen(Buttons[1].Id)!.Value, menu.ItemOnScreen(Buttons[2].Id)!.Value);
                Assert.True(remove.right < copy.left && copy.right < cut.left && cut.right < words.left);
                // Together at the row's far left, the gaps alike (§350).
                Assert.InRange(remove.left - panel.left, Px(Flyout.PrimaryEdgeDip + Flyout.PrimaryPlateDip), Px(Flyout.PrimaryEdgeDip + Flyout.PrimaryPlateDip + 2));
                Assert.Equal(cut.left - copy.right, copy.left - remove.right);
                menu.Hide();
            }
        });
    }

    /// <summary>
    /// §288: a face's level has no heading of its own either: its head (the back arrow and the face's name,
    /// Flyout.Inline.cs) is every level's, its rows right under it — for every face there is.
    /// </summary>
    [Fact]
    public void AFacesLevelHasNoHeadingOfItsOwn()
    {
        foreach (var face in FolderFaces.All)
        {
            var folder = BarKind.FolderList.New();
            folder.Folder!.Faces = [FolderFace.Of(face.Id)];
            var row = OptionMenu.FaceSection(folder.Folder, []).Single(item => item.Reorder == OptionMenu.FaceGroup);
            Assert.DoesNotContain(row.Children ?? [], item => item.Header && (item.Text == row.Text || item.Text == face.Title));
            Assert.False(row.Children is [{ Header: true }, ..], $"{face.Id}: its level opens with the heading {row.Children?[0].Text}");
        }
    }

    /// <summary>
    /// A menu shown with a spoken name and no title draws no title band — it is as tall as the same
    /// rows with nothing named — and is read out as the name; a drawn title still wins.
    /// </summary>
    [UiFact]
    public void AMenuWithASpokenNameDrawsNoTitle()
    {
        List<FlyoutItem> rows = [new(TheDock.CMD_WIDGET_ACT, "Open"), new(0, "", Separator: true), new(TheDock.CMD_TASKBAR_SETTINGS, "Taskbar settings")];
        WindowShots.OnHiddenDesktop("menu-no-title", (gfx, instance) =>
        {
            var work = new RECT { right = 1920, bottom = 1040 };
            var anchor = new POINT { x = 960, y = 1040 };
            int Height(Flyout menu)
            {
                RECT box;
                box = WindowShots.Extent(menu.Hwnd);
                return box.bottom - box.top;
            }

            using var plain = new Flyout(gfx, instance, 0);
            plain.Show(gfx, AddListPictures.LookFor(true), 1f, rows, FlyoutLayout.List, anchor, work, Edge.Bottom);
            WindowShots.Settle();
            var bare = Height(plain);
            Assert.Equal(S.DockUiaMenu, plain.UiaName);
            plain.Hide();

            using var spoken = new Flyout(gfx, instance, 0) { SpokenName = "Clock · Tokyo" };
            spoken.Show(gfx, AddListPictures.LookFor(true), 1f, rows, FlyoutLayout.List, anchor, work, Edge.Bottom);
            WindowShots.Settle();
            Assert.Equal(bare, Height(spoken));
            Assert.Equal("Clock · Tokyo", spoken.UiaName);
            spoken.Hide();

            using var titled = new Flyout(gfx, instance, 0) { SpokenName = "Clock · Tokyo" };
            titled.Show(gfx, AddListPictures.LookFor(true), 1f, rows, FlyoutLayout.List, anchor, work, Edge.Bottom, "Missing");
            WindowShots.Settle();
            Assert.True(Height(titled) > bare);
            Assert.Equal("Missing", titled.UiaName);
            titled.Hide();
        });
    }
}
