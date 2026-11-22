// menus: an icon on every row of a widget's and a face's settings, and on every choice in them, and the
// icon picker (2026-10-19, the person: "add icons to all of the submenus / subitems, like the different
// styles or so" and "improve the icon picker"; Karpo.Core\OptionGlyphs.cs, docs\widgets.md §249). Held here:
//
//  - **Every row, every choice.** Every row of every widget's menu and every face's, with every list
//    listed, its place's colours and holidays and a notebook to write in, has a glyph; every choice of
//    every one has one; every group's row has one; a widget's styles each a glyph of their own. And
//    every row the menu makes of them has its icon, however deep.
//  - **In the font.** Every glyph is one Segoe Fluent Icons has on this PC, and draws something.
//  - **The picker.** Its field first, then "Default (Folder)" as its first tile (§518), then the whole gallery under
//    its six headings, each glyph once, 40 DIP cells. On a real menu (KARPO_UI_TESTS=1): its field has
//    the keys as it opens, the icon in force ringed and scrolled into view; typing leaves the matches
//    in place of the grid, Down and Enter pick one, Backspace on a tile goes on in the field. Pictures in
//    KARPO_PNG_DIR: menu-icons-<widget>-<look>.png and menu-icons-picker[-search]-<look>.png, at 150 %.

using System.Buffers.Binary;
using System.Reflection;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed class MenuIconsTests
{
    /// <summary>Every list a row chooses from, listed (as OptionRowsTests lists them).</summary>
    private static readonly OptionDevices Sources = new(
        ["GPU 0: Intel UHD Graphics 770", "GPU 1: NVIDIA GeForce RTX 4060 Ti"],
        ["Disk 0 (C:)", "Disk 1 (E: F:)"])
    {
        Drives =
        [
            new DriveVolume(@"\\?\Volume{1}\", "C:", "Local Disk", DriveKind.Fixed, 100, 50),
            new DriveVolume(@"\\?\Volume{2}\", "E:", "Stick", DriveKind.Removable, 100, 50),
            new DriveVolume(@"\\server\share", "Z:", "share", DriveKind.Remote, 0, 0),
        ],
        TodoSources =
        [
            new TodoSource("me@example.com", "tasks", "Tasks", Default: true),
            new TodoSource("me@example.com", "shop", "Shopping"),
        ],
        Notebook = new NotebookFile
        {
            Lists = [new TodoList { Id = "a", Name = "Home" }],
            Notes = [new Note { Id = "n1", Title = "Ideas" }],
        },
        FirstDay = DayOfWeek.Monday,
        TrayIcons = [new TrayMenuIcon("spotify|0", "Spotify", AppHidden: false)],
    };

    /// <summary>A place whose holidays list a country with regions and one without, and where every folder is: every row there is.</summary>
    private static OptionPlace Place() =>
        new(new Config { Holidays = new HolidayOptions { Calendars = [new HolidayCountry { Country = "CH" }, new HolidayCountry { Country = "FR" }] } }, Dark: true, Exists: _ => true);

    /// <summary>A new widget of every kind there is, a pin to a folder showing its contents among them.</summary>
    private static IEnumerable<WidgetItem> EveryKind()
    {
        foreach (var item in Enum.GetValues<BarKind>().Select(kind => (WidgetItem)kind.New()).Concat(Enum.GetValues<ListKind>().Select(kind => kind.New())))
        {
            if (item.Info is null)
            {
                continue;
            }
            if (item.Pin is { } pin)
            {
                pin.Target = @"C:\Users";
            }
            yield return item;
        }
    }

    /// <summary>A folder wearing <paramref name="faces"/>.</summary>
    private static BarItem Folder(params string[] faces)
    {
        var folder = BarKind.FolderList.New();
        folder.Folder!.Faces = [.. faces.Select(FolderFace.Of)];
        return folder;
    }

    /// <summary>Every widget's rows and every face's, each with <paramref name="devices"/>, an item's own among them (§265); a market's of every type, its years too.</summary>
    private static List<(string Where, OptionRow Row)> AllRows(OptionDevices devices)
    {
        List<(string, OptionRow)> rows = [];
        foreach (var item in EveryKind())
        {
            rows.AddRange(OptionRows.Flat(OptionRows.Of(item, devices, Place())).Select(row => (item.Info!.Id, row)));
            if (item.Market is { } market)
            {
                foreach (var sample in Markets.MarketRowsTests.Samples)
                {
                    (market.Symbol, market.Style, market.Range) = (sample.Symbol, sample.Style, MarketRange.Years);
                    rows.AddRange(OptionRows.Of(item, devices, Place()).Select(row => (item.Info!.Id, row)));
                }
            }
        }
        foreach (var face in FolderFaces.All)
        {
            var folder = Folder(face.Id, FolderFaces.WifiId);
            rows.AddRange(OptionRows.OfFace(folder.Folder!, 0, devices).Select(row => ("face " + face.Id, row)));
            if (face.Id == FolderFaces.MarketsId)
            {
                var market = MarketFaces.Kept(folder.Folder!.Faces[0]);
                foreach (var sample in Markets.MarketRowsTests.Samples)
                {
                    (market.Symbol, market.Style, market.Range) = (sample.Symbol, sample.Style, MarketRange.Years);
                    rows.AddRange(OptionRows.OfFace(folder.Folder, 0, devices).Select(row => ("face " + face.Id, row)));
                }
            }
        }
        // §351: the public holidays are ⚙'s, which the calendars link to.
        rows.AddRange(OptionRows.Flat(OptionHolidays.Rows(Place().Config)).Select(row => ("⚙ holidays", row)));
        return rows;
    }

    /// <summary>The rows with a notebook to write in, whose new, rename and delete rows come with it.</summary>
    private static void WithBook(Action<OptionDevices> body)
    {
        var folder = Path.Combine(Path.GetTempPath(), "karpo-notebook-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var book = new Notebook(folder);
            body(Sources with { Book = book });
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    // --- Every row, every choice ---------------------------------------------------------------------

    [Fact]
    public void EveryRowAndChoiceHasAnIcon()
    {
        WithBook(devices =>
        {
            _ = devices.Book!.AddList("Groceries");
            var rows = AllRows(devices);
            Assert.Contains(rows, each => each.Row.Key == OptionRows.NotebookNew);
            Assert.Contains(rows, each => each.Row.Key.StartsWith(OptionRows.NotebookDelete, StringComparison.Ordinal));
            Assert.Contains(rows, each => each.Row.Key.StartsWith(OptionRows.NotebookName, StringComparison.Ordinal));
            Assert.Contains(rows, each => each.Row.Key == OptionHolidays.Region + 0);
            Assert.Contains(rows, each => each.Row.Key == OptionHolidays.Delete + 1);
            Assert.Contains(rows, each => each.Row.Key == OptionRows.PinNavigation);
            Assert.Contains(rows, each => each.Row.Key == OptionTray.Icon + "spotify|0");
            foreach (var (where, row) in rows)
            {
                Assert.True(row.Glyph.Length > 0, $"{where}: {row.Key} has no icon");
                if (row.Kind == OptionRowKind.Choice)
                {
                    Assert.True(row.ChoiceGlyphs.Count == 0 || row.ChoiceGlyphs.Count == row.Choices.Count, $"{where}: {row.Key}'s choices and their icons");
                    for (var at = 0; at < row.Choices.Count; at++)
                    {
                        Assert.True(row.ChoiceGlyph(at).Length > 0, $"{where}: {row.Key}'s {row.Choices[at]} has no icon");
                    }
                }
            }
            // A group's row has its own, on its first row; an ordered list stands under a heading instead.
            foreach (var group in rows.Where(each => each.Row is { Group: not null, Ordered: false }).GroupBy(each => (each.Where, each.Row.Group)))
            {
                Assert.True(group.First().Row.GroupGlyph.Length > 0, $"{group.Key.Where}: the group {group.Key.Group} has no icon");
            }
        });
    }

    /// <summary>A widget's styles and readings each look like what they are: no two of one widget's alike.</summary>
    [Fact]
    public void EachStyleHasAnIconOfItsOwn()
    {
        foreach (var item in EveryKind().Where(WidgetStyles.Has))
        {
            foreach (var row in OptionRows.Of(item).Where(row => row.Key is OptionRows.Style or OptionRows.Reading))
            {
                Assert.Equal(row.Choices.Count, row.ChoiceGlyphs.Count);
                Assert.Equal(row.ChoiceGlyphs.Count, row.ChoiceGlyphs.Distinct().Count());
            }
        }
        var calendar = ListKind.WeekView.New();
        Assert.Equal(
            [OptionGlyphs.Agenda, OptionGlyphs.Events, OptionGlyphs.Day, OptionGlyphs.Week, OptionGlyphs.Month, OptionGlyphs.Year],
            OptionRows.Of(calendar).Single(row => row.Key == OptionRows.Style).ChoiceGlyphs);
    }

    /// <summary>The rows a menu makes of every widget's and every face's settings, every level down: each has its icon.</summary>
    [Fact]
    public void EveryRowOfAMenuHasItsIcon()
    {
        WithBook(devices =>
        {
            void Walk(string where, IReadOnlyList<FlyoutItem> items)
            {
                foreach (var item in items)
                {
                    // A field fills its own level, named by the row it opens from (§246); a greyed row only says something; §411
                    // (R13): a choice's options wear icons only where each has its own.
                    if (!item.Header && !item.Separator && item.Enabled && item.Control is not FlyoutField && item.Text.Length > 0 && item.Checked is null && items.Any(other => other.Icon is not null))
                    {
                        Assert.True(item.Icon is not null, $"{where}: \"{item.Text}\" has no icon");
                    }
                    if (item.Children is { } children)
                    {
                        Walk($"{where} › {item.Text}", children);
                    }
                }
            }
            foreach (var item in EveryKind())
            {
                Walk(item.Info!.Id, OptionMenu.Section(OptionRows.Of(item, devices, Place()), []));
            }
            foreach (var face in FolderFaces.All)
            {
                Walk("face " + face.Id, OptionMenu.FaceSection(Folder(face.Id, FolderFaces.WifiId).Folder!, [], devices));
            }
            // A line whose content grows: the graphs are sized since §603.
            var line = ListKind.Mail.New();
            Walk("height", OptionMenu.Foot([.. LineHeightRows.Rows(LineHeightRows.For(line)!.Value)]));
        });
    }

    // --- In the font ----------------------------------------------------------------------------------

    /// <summary>Every glyph of every row, choice, menu and the gallery is in Segoe Fluent Icons and draws something.</summary>
    [Fact]
    public void EveryGlyphIsInTheFont()
    {
        HashSet<string> glyphs = [];
        foreach (var field in typeof(OptionGlyphs).GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => field.FieldType == typeof(string)))
        {
            glyphs.Add((string)field.GetValue(null)!);
        }
        WithBook(devices =>
        {
            foreach (var (_, row) in AllRows(devices))
            {
                glyphs.Add(row.Glyph);
                glyphs.UnionWith(row.ChoiceGlyphs);
                glyphs.UnionWith(row.Tiles.Select(tile => tile.Glyph).Where(glyph => glyph.Length > 0));
            }
        });
        glyphs.UnionWith(MenuIcons.All.Select(icon => icon.Glyph));
        glyphs.UnionWith(IconSearch.All.Select(icon => icon.Glyph));
        glyphs.UnionWith(Enum.GetValues<FaceGroup>().Select(FaceGallery.Glyph));
        glyphs.UnionWith(FolderFaces.All.Select(FaceGallery.GlyphOf));
        glyphs.Remove("");
        // §589: a glyph turned a quarter (OptionGlyphs.Turned) is its font glyph and the word joiner that turns it.
        glyphs = [.. glyphs.Select(glyph => glyph.TrimEnd(OptionGlyphs.QuarterTurn))];
        var missing = glyphs.Where(glyph => glyph.Length != 1 || !SegoeIcons.Draws(glyph[0])).Select(glyph => string.Join(",", glyph.Select(c => ((int)c).ToString("X4", System.Globalization.CultureInfo.InvariantCulture)))).ToList();
        Assert.True(missing.Count == 0, "Not in Segoe Fluent Icons: " + string.Join(" ", missing));
        // The reader reads the font: a glyph it has, and one it hasn't (the gallery's pie chart was one, §249).
        Assert.True(SegoeIcons.Draws('\uE8B7'));
        Assert.False(SegoeIcons.Draws('\uE9D4'));
    }

    /// <summary>Segoe Fluent Icons as this PC has it: which code points it maps to a glyph that has an outline.</summary>
    private static class SegoeIcons
    {
        private static readonly Lazy<(Dictionary<int, int> Map, Func<int, bool> Outlined)> Font = new(Read);

        public static bool Draws(char code) => Font.Value.Map.TryGetValue(code, out var glyph) && glyph != 0 && Font.Value.Outlined(glyph);

        private static (Dictionary<int, int>, Func<int, bool>) Read()
        {
            var font = File.ReadAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "SegoeIcons.ttf"));
            ushort U16(int at) => BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(at));
            uint U32(int at) => BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(at));
            Dictionary<string, (int Offset, int Length)> tables = [];
            for (var i = 0; i < U16(4); i++)
            {
                var record = 12 + (16 * i);
                tables[System.Text.Encoding.ASCII.GetString(font, record, 4)] = ((int)U32(record + 8), (int)U32(record + 12));
            }
            var cmap = tables["cmap"].Offset;
            Dictionary<int, int> map = [];
            for (var i = 0; i < U16(cmap + 2); i++)
            {
                var (platform, encoding, at) = (U16(cmap + 4 + (8 * i)), U16(cmap + 6 + (8 * i)), cmap + (int)U32(cmap + 8 + (8 * i)));
                if (platform != 3 || U16(at) != 4 || encoding != 1)
                {
                    continue;
                }
                var segments = U16(at + 6) / 2;
                var (ends, starts, deltas, ranges) = (at + 14, at + 16 + (2 * segments), at + 16 + (4 * segments), at + 16 + (6 * segments));
                for (var s = 0; s < segments; s++)
                {
                    for (int c = U16(starts + (2 * s)), end = U16(ends + (2 * s)); c <= end && c != 0xFFFF; c++)
                    {
                        var range = U16(ranges + (2 * s));
                        var glyph = range == 0 ? c + (short)U16(deltas + (2 * s)) : U16(ranges + (2 * s) + range + (2 * (c - U16(starts + (2 * s)))));
                        if (range != 0 && glyph != 0)
                        {
                            glyph += (short)U16(deltas + (2 * s));
                        }
                        map[c] = glyph & 0xFFFF;
                    }
                }
            }
            if (!tables.TryGetValue("glyf", out _) || !tables.TryGetValue("loca", out var loca))
            {
                return (map, _ => true);
            }
            var longOffsets = U16(tables["head"].Offset + 50) == 1;
            int Start(int glyph) => longOffsets ? (int)U32(loca.Offset + (4 * glyph)) : 2 * U16(loca.Offset + (2 * glyph));
            return (map, glyph => Start(glyph + 1) > Start(glyph));
        }
    }

    // --- The picker ------------------------------------------------------------------------------------

    /// <summary>A folder's icon picker: its field, the cross its first tile, then every glyph of the gallery once, in one grid with no headings (§306).</summary>
    [Fact]
    public void ThePickerIsTheWholeGalleryInOneGridAfterTheCross()
    {
        var folder = Folder(FolderFaces.IconId);
        List<OptionPick> picks = [];
        var face = OptionMenu.FaceSection(folder.Folder!, picks).First(row => row.Reorder != 0);
        var picker = face.Children!.Single(child => OptionMenu.PickOf(picks, child.Id)?.Key == OptionPickers.FolderIcon && child.Children is not null).Children!;
        var field = Assert.IsType<FlyoutField>(picker[0].Control);
        Assert.True(field.Filters);
        Assert.Equal((S.SettingsIconPickerSearch, S.SettingsIconPickerNoMatch), (field.Placeholder, field.NoMatch));
        Assert.Equal(OptionPickKind.None, OptionMenu.PickOf(picks, picker[0].Id)!.Kind);
        var tiles = picker.Where(Flyout.IsTile).ToList();
        Assert.Equal(picker[1], tiles[0]);
        Assert.Equal(("Default (Folder)", true, OptionGlyphs.Clear), (tiles[0].Text, tiles[0].Checked == true, ((FlyoutTile)tiles[0].Icon!).Code));
        Assert.All(tiles, tile => Assert.Equal((MenuPickers.IconCellDip, MenuPickers.IconTileDip), (((FlyoutTile)tile.Icon!).CellDip, ((FlyoutTile)tile.Icon!).SideDip)));
        Assert.Equal(IconSearch.All.Select(icon => icon.Glyph).Distinct().Count() + 1, tiles.Count);
        Assert.Equal(tiles.Count - 1, tiles.Skip(1).Select(tile => ((FlyoutTile)tile.Icon!).Code).Distinct().Count());
        Assert.All(tiles.Skip(1), tile => Assert.NotEmpty(tile.Words!));
        Assert.Equal(picker.Count - 1, tiles.Count);
        Assert.DoesNotContain(picker, item => item.Header);
        Assert.Equal(MenuIcons.All.Count, MenuIcons.All.Select(icon => icon.Glyph).Distinct().Count());

        // The glyph in force is the tile ringed, the default not.
        folder.Folder!.Face.Glyph = "\uE840";
        List<OptionPick> again = [];
        var shown = OptionMenu.FaceSection(folder.Folder, again).First(row => row.Reorder != 0).Children!
            .Single(child => OptionMenu.PickOf(again, child.Id)?.Key == OptionPickers.FolderIcon && child.Children is not null).Children!;
        Assert.Equal("\uE840", ((FlyoutTile)Assert.Single(shown, item => item.Checked == true).Icon!).Code);
    }

    private static bool Save(Flyout menu, string name, bool dark) =>
        WindowShots.SaveTogether([.. menu.OpenPanels], name, dark ? ((byte)0, (byte)0, (byte)0) : ((byte)243, (byte)243, (byte)243));

    private static void KeysTo(Flyout menu, uint id, Func<uint?> hot)
    {
        for (var step = 0; step < 80 && hot() != id; step++)
        {
            _ = menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
        }
        Assert.Equal(id, hot());
    }

    private static void Type(Flyout menu, string text)
    {
        foreach (var c in text)
        {
            _ = menu.Message(WM.WM_CHAR, (WPARAM)(nuint)c, 0);
        }
    }

    /// <summary>
    /// The picker on a real menu: its field has the keys as it opens, the icon in force ringed and in
    /// view at the gallery's far end; typing leaves the matches, Down and Enter pick one, Backspace on a
    /// tile goes on in the field; every line of tiles 40 DIP. Pictures, and several widgets' menus with
    /// a choice open, every row and every choice with its icon.
    /// </summary>
    [UiFact]
    public void ThePickerAndTheMenusOnARealMenu()
    {
        WindowShots.OnHiddenDesktop("menu-icons", (gfx, instance) =>
        {
            const float scale = 1.5f;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark);
                var tone = dark ? "dark" : "light";
                var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
                var anchor = new POINT { x = work.right / 4, y = work.bottom };

                // A folder wearing its icon, refresh's glyph, at the gallery's far end.
                var folder = Folder(FolderFaces.IconId);
                folder.Folder!.Face.Glyph = "\uE72C";
                using (var menu = new Flyout(gfx, instance, 0) { Configures = true })
                {
                    List<OptionPick> picks = [];
                    var items = OptionMenu.FaceSection(folder.Folder, picks);
                    menu.Show(gfx, look, scale, items, FlyoutLayout.List, anchor, work, Edge.Bottom, null);
                    WindowShots.Settle();
                    var face = items.First(item => item.Reorder != 0);
                    KeysTo(menu, face.Id, () => menu.HotId);
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                    WindowShots.Settle();
                    var icon = face.Children!.Single(child => child.Children is not null && OptionMenu.PickOf(picks, child.Id)?.Key == OptionPickers.FolderIcon);
                    KeysTo(menu, icon.Id, () => menu.HotId);
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                    WindowShots.Settle();
                    Assert.Equal(2, menu.InlineDepth);
                    var field = icon.Children![0];
                    // The field has the keys as it opens.
                    Assert.Equal(field.Id, menu.FieldKeys);
                    // The icon in force ringed and in view; the level as tall as a picker is.
                    var picker = menu;
                    var chosen = icon.Children.Single(child => child.Checked == true);
                    var panel = menu.OpenPanels[0].Panel;
                    var ringed = picker.Looks.Single(row => row.Id == chosen.Id).Plate;
                    Assert.InRange(ringed.Y, 0, panel.bottom - panel.top - ringed.H);
                    Assert.Equal(MathF.Round(MenuPickers.IconCellDip * scale) - (2 * MathF.Round(scale)), ringed.H);
                    WindowShots.Settle();
                    Assert.True(Save(menu, $"menu-icons-picker-{tone}.png", dark));

                    // Typing leaves the tiles whose words hold it, without headings, in place of the grid.
                    Type(menu, "pinn");
                    WindowShots.Settle();
                    var print = icon.Children.Single(child => Flyout.IsTile(child) && ((FlyoutTile)child.Icon!).Code == IconSearch.All.Single(each => each.Id == "pinned").Glyph);
                    Assert.Equal([print.Id], menu.FieldShowing.Where(Flyout.IsTile).Select(item => item.Id));
                    Assert.DoesNotContain(menu.FieldShowing, item => item.Header);
                    Assert.Equal(2, menu.InlineDepth);
                    Assert.Equal(panel.bottom - panel.top, menu.OpenPanels[0].Panel.bottom - menu.OpenPanels[0].Panel.top);
                    Assert.True(Save(menu, $"menu-icons-picker-search-{tone}.png", dark));
                    // Backspace on the tile goes on in the field: more match.
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
                    Assert.Equal(print.Id, menu.HotId);
                    Assert.Equal(0u, menu.FieldKeys);
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_BACK, 0);
                    Assert.Equal(field.Id, menu.FieldKeys);
                    Assert.True(menu.FieldShowing.Count(Flyout.IsTile) > 1);
                    Type(menu, "n");
                    // Enter in the field goes on to the first match, and Enter picks it; the menu stays.
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RETURN, 0);
                    Assert.Equal(print.Id, menu.HotId);
                    var picked = menu.Message(WM.WM_KEYDOWN, VK.VK_RETURN, 0);
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, print.Id), picked);
                    Assert.True(OptionMenu.Apply(folder, OptionMenu.PickOf(picks, picked.Id)!));
                    Assert.Equal(((FlyoutTile)print.Icon!).Code, folder.Folder.Face.Glyph);
                    Assert.True(menu.IsOpen);
                    // Nothing typed is kept as a setting's words.
                    Assert.Null(menu.TakeFieldCommit(field.Id));
                    // Typing on a tile goes on in the field; nothing matching says so.
                    Type(menu, "zzzq");
                    Assert.Equal(field.Id, menu.FieldKeys);
                    Assert.DoesNotContain(menu.FieldShowing, Flyout.IsTile);
                    Assert.Contains(menu.FieldShowing, item => item.Text == S.SettingsIconPickerNoMatch && !item.Enabled);
                    menu.Hide();
                }

                // Widgets' menus, a choice's level open: every row and every choice with its icon.
                (string Name, WidgetItem Item, string Key)[] menus =
                [
                    ("calendar", ListKind.WeekView.New(), OptionRows.Style),
                    ("performance", ListKind.Cpu.New(), OptionRows.Reading),
                    ("performance-style", ListKind.Cpu.New(), OptionRows.Style),
                    ("clock", ListKind.Clock.New(), OptionRows.Style),
                    ("weather", ListKind.WeatherNow.New(), OptionRows.Style),
                ];
                foreach (var (name, item, key) in menus)
                {
                    using var menu = new Flyout(gfx, instance, 0) { Configures = true };
                    List<OptionPick> picks = [];
                    List<FlyoutItem> items = [.. TheDock.WidgetButtons(), .. OptionMenu.Section(OptionRows.Of(item, Sources, Place()), picks)];
                    if (LineHeightRows.For(item) is { } height)
                    {
                        items.AddRange(OptionMenu.Foot([.. LineHeightRows.Rows(height)]));
                    }
                    menu.Show(gfx, look, scale, items, FlyoutLayout.List, anchor, work, Edge.Bottom, null);
                    WindowShots.Settle();
                    var row = items.First(each => OptionMenu.PickOf(picks, each.Id)?.Key == key && each.Children is not null);
                    KeysTo(menu, row.Id, () => menu.HotId);
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                    WindowShots.Settle();
                    Assert.Equal(1, menu.InlineDepth);
                    // Every row with words has its icon drawn, in one column.
                    var looks = menu.Looks.Where(look => look.Words.W > 0).ToList();
                    Assert.All(looks, each => Assert.True(each.Icon.W > 0, $"{name}: row {each.Id} draws no icon"));
                    Assert.Single(looks.Select(each => each.Icon.X).Distinct());
                    Assert.All(menu.Looks, each => Assert.True(each.Icon.W > 0, $"{name}: choice {each.Id} draws no icon"));
                    Assert.True(Save(menu, $"menu-icons-{name}-{tone}.png", dark));
                    menu.Hide();
                }

                // A folder's faces: a face's settings open, the weather's.
                using (var menu = new Flyout(gfx, instance, 0) { Configures = true })
                {
                    var weather = Folder(FolderFaces.WeatherId, FolderFaces.ClockId);
                    List<OptionPick> picks = [];
                    var items = OptionMenu.FaceSection(weather.Folder!, picks);
                    menu.Show(gfx, look, scale, items, FlyoutLayout.List, anchor, work, Edge.Bottom, null);
                    WindowShots.Settle();
                    var clock = items.Where(item => item.Reorder != 0).ElementAt(1);
                    KeysTo(menu, clock.Id, () => menu.HotId);
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                    WindowShots.Settle();
                    var hours = clock.Children!.First(child => child.Children is not null && OptionMenu.PickOf(picks, child.Id)?.Key == ClockFace.Hours);
                    KeysTo(menu, hours.Id, () => menu.HotId);
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                    WindowShots.Settle();
                    Assert.Equal(2, menu.InlineDepth);
                    Assert.True(Save(menu, $"menu-icons-faces-{tone}.png", dark));
                    menu.Hide();
                }
            }
        });
    }
}
