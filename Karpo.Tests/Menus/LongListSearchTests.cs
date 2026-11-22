// menus: a long list's level is searched (the person, 2026-10-22: "long select lists that have more than
// 10 items or so should be searcheable"; Karpo.Dock\Flyout.Inline.cs, docs\widgets.md §279). Held here:
//
//  - **The rule.** A level of more than ten rows to pick and no field of its own has a search field under
//    its head; ten or fewer, a level with a field (a search's, an icon picker's) or of tiles, none.
//  - **The field.** It has the keys as the level opens, stands still under the head, and leaves the rows
//    whose words hold what is typed, without the headings and rules; nothing matching says so; Escape
//    gives every row back; Enter goes on to the first row, which a pick keeps, the level staying.
//  - **English too.** A row in another language is found by its English words where they are Karpo's.
//  - **Pictures** in KARPO_PNG_DIR, at 150 %, dark: the long list searched (menus6-list-search-dark), a
//    folder's right click at rest with no heading per face (menus6-folder-rest-dark), and edit mode's
//    menus saying what the widget is (menus6-edit-type-dark).
//
// The drawn half needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed class LongListSearchTests
{
    private const float Scale = 1.5f;

    private static readonly RECT Work = new() { right = (int)(1920 * Scale), bottom = (int)(1040 * Scale) };

    private static readonly POINT Anchor = new() { x = Work.right / 3, y = Work.bottom };

    /// <summary>A choice of <paramref name="count"/> of Switzerland's cantons, as a widget's row and its menu's rows.</summary>
    private static (List<FlyoutItem> Items, FlyoutItem Row, List<OptionPick> Picks) Choice(int count)
    {
        var regions = HolidayPlaces.Regions("CH").Take(count).ToList();
        var chosen = 0;
        var option = new OptionRow("region", OptionRowKind.Choice, S.SettingsHolidaysRegionHeader)
        {
            Glyph = OptionGlyphs.Place,
            Choices = [.. regions.Select(region => region.Name)],
            Chosen = chosen,
            Set = at => (chosen = at) >= 0,
        };
        List<OptionPick> picks = [];
        List<FlyoutItem> items = [.. TheDock.WidgetButtons(), .. OptionMenu.Section([option], picks)];
        return (items, items.Single(item => item.Text == S.SettingsHolidaysRegionHeader), picks);
    }

    private static Flyout Show(Gfx gfx, HINSTANCE instance, IReadOnlyList<FlyoutItem> items, POINT? anchor = null)
    {
        var menu = new Flyout(gfx, instance, 0) { Configures = true };
        menu.Show(gfx, AddListPictures.LookFor(true), Scale, items, FlyoutLayout.List, anchor ?? Anchor, Work, Edge.Bottom, null);
        WindowShots.Settle();
        return menu;
    }

    private static FlyoutEvent Key(Flyout menu, int key) => menu.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)key, 0);

    private static void Type(Flyout menu, string text)
    {
        foreach (var c in text)
        {
            _ = menu.Message(WM.WM_CHAR, (WPARAM)(nuint)c, 0);
        }
    }

    /// <summary>The rows a level shows under its head, its rule and its field: what the filter left.</summary>
    private static List<string> Listed(Flyout menu) =>
        [.. menu.FieldShowing.SkipWhile(item => item.Control is not FlyoutField).Skip(1).Select(item => item.Text)];

    /// <summary>
    /// More than ten rows to pick: a search field. Ten, a heading or a rule among them, tiles, or a field
    /// of the level's own (a search's, an icon picker's): none.
    /// </summary>
    [Fact]
    public void ALevelOfMoreThanTenRowsIsSearched()
    {
        static List<FlyoutItem> Rows(int count) => [.. Enumerable.Range(1, count).Select(at => new FlyoutItem((uint)at, $"Row {at}"))];

        Assert.Equal(10, Flyout.ListFilterFrom);
        Assert.False(Flyout.Searchable(Rows(10)));
        Assert.True(Flyout.Searchable(Rows(11)));
        Assert.False(Flyout.Searchable([.. Rows(10), new FlyoutItem(0, "", Separator: true), new FlyoutItem(0, "Heading", Header: true)]));
        // §429: a link under them is a way out, no row to pick (a widget's styles' "Styles ↗", Appearance's).
        Assert.False(Flyout.Searchable([.. Rows(10), new FlyoutItem(0, "", Separator: true), new FlyoutItem(98, "Styles", Opens: "Opens Styles in Layout")]));
        Assert.False(Flyout.Searchable([.. Rows(30), new FlyoutItem(99, "Search", Control: new FlyoutField("", Filters: true))]));
        Assert.False(Flyout.Searchable([.. Enumerable.Range(1, 30).Select(at => new FlyoutItem((uint)at, $"Icon {at}", new FlyoutTile(null, "", 24)))]));
        // Toggles count: a long group of switches (the status icons) is a list too.
        Assert.True(Flyout.Searchable([.. Enumerable.Range(1, 12).Select(at => new FlyoutItem((uint)at, $"Icon {at}", Control: new FlyoutControl.Toggle(true)))]));
    }

    /// <summary>A row is found by its English words too, where Karpo speaks another language and they are Karpo's.</summary>
    [Fact]
    public void ARowIsFoundByItsEnglishToo()
    {
        using (InLanguage.Use("de"))
        {
            var row = new FlyoutItem(1, S.DockMenuRemove, Detail: "Zweite Zeile");
            var words = Flyout.FilterWords(row);
            Assert.Contains("Entfernen", words);
            Assert.Contains("Remove", words);
            Assert.Contains("Zweite Zeile", words);
            Assert.True(IconSearch.Matches(words, "remo"));
            Assert.Null(Strings.EnglishOf("Basel-Stadt"));
        }
        Assert.Null(Strings.EnglishOf(S.DockMenuRemove));
        // A row with words of its own (an icon's, a widget's) is found by those.
        Assert.Equal(["home", "house"], Flyout.FilterWords(new FlyoutItem(1, "Home", Words: ["home", "house"])));
    }

    /// <summary>
    /// A choice of 26 cantons opened in place: the field under the head has the keys; typing leaves the
    /// cantons whose names hold it; nothing matching says so; Escape gives every canton back; Enter goes on
    /// to the first, Enter again picks it, the level staying with its field. Ten cantons: no field.
    /// </summary>
    [UiFact]
    public void ALongChoiceIsSearchedInPlace()
    {
        WindowShots.OnHiddenDesktop("long-list-search", (gfx, instance) =>
        {
            var (items, row, picks) = Choice(26);
            using var menu = Show(gfx, instance, items);
            _ = menu.Click(row.Id);
            Assert.Equal((1, row.Text), (menu.InlineDepth, menu.InlineTitle));
            var field = menu.FieldShowing.Single(item => item.Control is FlyoutField);
            Assert.Equal(Flyout.InlineFilterId - 1, field.Id);
            Assert.Equal(S.DockMenuSearch, ((FlyoutField)field.Control!).Placeholder);
            // Under the head and its rule, the keys in it as the level opens.
            Assert.Equal([Flyout.InlineBackId, 0u, field.Id], menu.FieldShowing.Take(3).Select(item => item.Id));
            Assert.Equal(field.Id, menu.FieldKeys);
            Assert.Equal(row.Children!.Select(item => item.Text), Listed(menu));

            Type(menu, "gall");
            Assert.Equal(["St. Gallen"], Listed(menu));
            Type(menu, "xyz");
            Assert.Equal([S.DockMenuNoMatch], Listed(menu));
            Assert.False(menu.FieldShowing[^1].Enabled);
            _ = Key(menu, VK.VK_ESCAPE);
            Assert.Equal(row.Children!.Select(item => item.Text), Listed(menu));
            Assert.Equal(1, menu.InlineDepth);

            // Typed again, Enter goes on to the first canton found and Enter picks it; the level stays.
            if (menu.FieldKeys == 0)
            {
                menu.PointTo(field.Id);
                _ = menu.Click(field.Id);
            }
            Type(menu, "ell");
            var first = row.Children!.Single(item => item.Text == "Appenzell Innerrhoden");
            Assert.Equal(["Appenzell Innerrhoden", "Appenzell Ausserrhoden"], Listed(menu));
            _ = Key(menu, VK.VK_RETURN);
            Assert.Equal(first.Id, menu.HotId);
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, first.Id), Key(menu, VK.VK_RETURN));
            Assert.Equal(OptionPickKind.Set, OptionMenu.PickOf(picks, first.Id)!.Kind);
            Assert.Equal(1, menu.InlineDepth);
            // Typing on a canton goes back into the field, on from what it holds.
            Type(menu, " au");
            Assert.Equal(field.Id, menu.FieldKeys);
            Assert.Equal(["Appenzell Ausserrhoden"], Listed(menu));
        });

        WindowShots.OnHiddenDesktop("long-list-short", (gfx, instance) =>
        {
            var (items, row, _) = Choice(10);
            using var menu = Show(gfx, instance, items);
            _ = menu.Click(row.Id);
            Assert.Equal(1, menu.InlineDepth);
            Assert.DoesNotContain(menu.FieldShowing, item => item.Control is FlyoutField);
            Assert.Equal(row.Children!.First().Id, menu.RowsShown[1].Id);
        });
    }

    /// <summary>
    /// The pictures, 150 %, dark: Switzerland's cantons searched for "bas" (menus6-list-search-dark), a folder
    /// of a clock, a calendar and the sound's right click at rest, its faces' rows under no heading
    /// (menus6-folder-rest-dark), and edit mode's menus of a clock and of a folder, each saying under its
    /// buttons what it is (menus6-edit-type-dark).
    /// </summary>
    [UiFact]
    public void MenuListPictures()
    {
        WindowShots.OnHiddenDesktop("long-list-pictures", (gfx, instance) =>
        {
            var black = ((byte)0, (byte)0, (byte)0);
            POINT At(int eighth) => Anchor with { x = Work.right * eighth / 8 };
            bool Save(IEnumerable<Flyout> menus, string name)
            {
                WindowShots.Settle(300);
                return WindowShots.SaveTogether([.. menus.SelectMany(menu => menu.OpenPanels)], $"{name}-dark.png", black);
            }

            var (items, row, _) = Choice(26);
            using (var whole = Show(gfx, instance, items, At(1)))
            using (var searched = Show(gfx, instance, items, At(4)))
            {
                _ = whole.Click(row.Id);
                _ = searched.Click(row.Id);
                Type(searched, "bas");
                Assert.True(Save([whole, searched], "menus6-list-search"));
            }

            var folder = new BarItem { Id = "f", Kind = BarKind.FolderList, Folder = new FolderOptions { Faces = [ClockFace.New(), FolderFace.Of(FolderFaces.CalendarId), FolderFace.Of(FolderFaces.SoundId)] } };
            using (var rest = Show(gfx, instance, TheDock.BarMenuRows(folder).Items, At(2)))
            {
                Assert.DoesNotContain(rest.ShownItems, item => item.Header);
                Assert.True(Save([rest], "menus6-folder-rest"));
            }

            var clock = ListKind.Clock.New();
            clock.Clock!.TimeZone = "Tokyo Standard Time";
            List<FlyoutItem> clockMenu = [.. TheDock.WidgetRowsFor(clock), .. OptionMenu.Section(OptionRows.Of(clock, new OptionDevices()), [])];
            List<OptionPick> picks = [];
            List<FlyoutItem> folderMenu = [.. TheDock.WidgetRowsFor(folder), .. OptionMenu.Section(OptionRows.Of(folder), picks), .. OptionMenu.FaceSection(folder.Folder, picks)];
            using (var one = Show(gfx, instance, clockMenu, At(1)))
            using (var two = Show(gfx, instance, folderMenu, At(4)))
            {
                Assert.True(Save([one, two], "menus6-edit-type"));
            }
        });
    }
}
