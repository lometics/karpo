// menus: what a row is set to, at its end, and a search's room (2026-10-19, the person: "also show the
// set configuration value in the settings as well" and "when searching for a place for example, always
// show as if there are 5 entries being displayed"; 2026-10-22: "they should show all the items with the
// fixed height so that the list is just scrollable in the end"; Karpo.Dock\OptionMenu.cs,
// Flyout.Values.cs, Flyout.Fields.cs, Flyout.Inline.cs, MenuSearch.cs, docs\widgets.md §228, §271).
// Held here:
//
//  - **Each kind says its value.** A choice the one in force, a search its words at rest, a colour its
//    swatch or "Automatic", an icon its glyph; an alarm's days as its line says them, "Never" for none; a
//    toggles group how many it shows; the status icons' mix nothing.
//  - **The row is shared** (§315; 2026-10-23: "the width should be shared by title and value"). Both
//    whole where they fit; else the one within its share whole, the other the rest; else each its share,
//    both cut — the value never left out, in Arabic too; on a real menu it stands after the words and
//    before the chevron.
//  - **One room, every result.** A search's level is one height, its field standing still at its top
//    and ten rows' room under it (§286), from none to two to twelve found, "nothing found" too; all twelve are
//    rows, the level scrolling to the last as the keys go down, the field where it stood.
//  - **Pictures** in KARPO_PNG_DIR, at 150 %, dark and light: a widget's menu and a face's submenu
//    with their values (menu-values-rows-<look>.png, menu-values-face-<look>.png), a place found two
//    and twelve times, and scrolled to the last of the twelve (menu-values-place-2-<look>.png,
//    menu-values-place-12-<look>.png, menu-values-place-scrolled-<look>.png).

using System.Globalization;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed unsafe class MenuValuesTests
{
    private const float Scale = 1.5f;

    private static readonly RECT Work = new() { right = (int)(1920 * Scale), bottom = (int)(1040 * Scale) };

    private static readonly POINT Anchor = new() { x = Work.right / 3, y = Work.bottom };

    // --- Each kind says its value ----------------------------------------------------------------------

    [Fact]
    public void EveryRowThatOpensALevelSaysWhatItIsSetTo()
    {
        OptionRow[] options =
        [
            new("on", OptionRowKind.Toggle, "Seconds") { On = true },
            new("pick", OptionRowKind.Choice, "Clock") { Choices = ["Region", "12-hour", "24-hour"], Chosen = 2 },
            new("none", OptionRowKind.Choice, "To") { Choices = ["CHF", "EUR"], Chosen = -1 },
            new("zone", OptionRowKind.Search, "Time zone") { Text = "Tokyo" },
            new("place", OptionRowKind.Search, "Location"),
            new("count", OptionRowKind.Number, "Years") { Value = 3, Least = 1, Most = 3 },
        ];
        var rows = OptionMenu.Rows(options, []);
        // §411 (R4): a search with nothing says "None", greyed.
        Assert.Equal([null, new FlyoutValue("24-hour"), new FlyoutValue(S.SettingsWidgetActionsNone, Implied: true), new FlyoutValue("Tokyo"), new FlyoutValue(S.SettingsWidgetActionsNone, Implied: true), null], rows.Select(row => row.Value));

        // A widget's own: its style, its zone.
        var clock = ListKind.Clock.New();
        clock.Clock!.TimeZone = "Tokyo Standard Time";
        var zone = OptionRows.Of(clock).Single(row => row.Key == OptionRows.ClockZone);
        // §230: a zone only its offset, its field and its results the whole name.
        Assert.Equal(new FlyoutValue("UTC+9"), OptionMenu.Rows(OptionRows.Of(clock), []).Single(row => row.Text == zone.Title).Value);
        Assert.Contains("Tokyo", zone.Text, StringComparison.Ordinal);
        var cpu = ListKind.Cpu.New();
        var style = OptionRows.Of(cpu).Single(row => row.Key == OptionRows.Style);
        Assert.Equal(style.Choices[style.Chosen], OptionMenu.Rows(OptionRows.Of(cpu), []).Single(row => row.Text == style.Title).Value!.Text);

        // A face's own, in its submenu: the clock's hours and zone.
        var folder = BarKind.FolderList.New();
        folder.Folder!.Faces = [FolderFace.Of(FolderFaces.ClockId), FolderFace.Of(FolderFaces.WeatherId)];
        var face = folder.Folder.Faces[0];
        face.Set(face.Info.Options.Single(option => option.Key == ClockFace.Zone), "Tokyo Standard Time");
        var own = OptionMenu.FaceSection(folder.Folder, []).First(row => row.Reorder != 0).Children!;
        // An alignment says nothing at its end: its raised button is what it is set to (§253).
        Assert.All(OptionRows.OfFace(folder.Folder, 0).Where(row => !row.Segmented && row.Kind is OptionRowKind.Choice or OptionRowKind.Search),
            row => Assert.Equal(OptionMenu.ValueOf(row), own.Single(item => !item.Header && item.Text == row.Title).Value));
        Assert.Contains(own, item => item.Value?.Text == "UTC+9");
    }

    /// <summary>A group says what it comes to: an alarm's days as its line says them, a toggles group's count; a mix nothing.</summary>
    [Fact]
    public void AGroupSaysWhatItComesTo()
    {
        var names = CultureInfo.CurrentCulture.DateTimeFormat;
        OptionRow Day(DayOfWeek day, bool on) => new(OptionRows.AlarmDay + (int)day, OptionRowKind.Toggle, names.GetDayName(day)) { On = on, Group = "Days", GroupTitle = S.DockEditRepeatOn };
        List<OptionRow> Week(params DayOfWeek[] on) => [.. AlarmDays.From(DayOfWeek.Monday).Select(day => Day(day, on.Contains(day)))];

        FlyoutValue? Said(IReadOnlyList<OptionRow> run) => OptionMenu.Rows(run, []).Single().Value;
        Assert.Equal(S.DockAlarmWeekdays, Said(Week(DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday))!.Text);
        Assert.Equal(S.DockAlarmEveryDay, Said(Week([.. Enum.GetValues<DayOfWeek>()]))!.Text);
        Assert.Equal(S.DockEditRepeatNever, Said(Week())!.Text);
        Assert.Equal($"{names.GetAbbreviatedDayName(DayOfWeek.Monday)}, {names.GetAbbreviatedDayName(DayOfWeek.Wednesday)}", Said(Week(DayOfWeek.Wednesday, DayOfWeek.Monday))!.Text);

        // The alarm's own rows say the same as its line.
        var alarm = ListKind.Alarm.New();
        Assert.Equal(S.DockEditRepeatNever, OptionMenu.Rows(OptionRows.Of(alarm), []).Single(row => row.Text == S.DockEditRepeatOn).Value!.Text);

        // A toggles row's toggles are listed in the menu itself (an ordered list), under no heading (§411).
        var toggles = Enum.GetValues<ListKind>().Select(kind => kind.New()).First(item => item.Info?.Options == WidgetOptionsKind.Toggles);
        var shown = OptionRows.Of(toggles).Count(row => row.Group is not null && row.On);
        var listed = OptionMenu.Rows(OptionRows.Of(toggles), []);
        Assert.DoesNotContain(listed, row => row.Header);
        Assert.Equal(shown, listed.Count(row => row.Reorder == OptionMenu.ListGroup));

        // The status icons, each its own choice: no one word for them.
        Assert.Null(OptionMenu.GroupValue([new("a", OptionRowKind.Choice, "Camera") { Choices = ["On", "Off"], Chosen = 0, Group = "Icons" }]));
    }

    /// <summary>
    /// §315: the name and the value share the row — both whole where they fit; the one within its share
    /// (the value's two fifths, the name's three) whole and the other the rest; else each its share. A
    /// value is never left out, however long the name.
    /// </summary>
    [Theory]
    [InlineData(300, 100, 80, 100, 80)]
    [InlineData(300, 290, 8, 290, 8)]
    // A short name whole, a long value cut to the rest.
    [InlineData(300, 100, 400, 100, 200)]
    // A short value whole, a long name cut to the rest.
    [InlineData(300, 320, 8, 292, 8)]
    [InlineData(300, 400, 100, 200, 100)]
    // Both long: each its share, both cut (§228 left out the value of the first two).
    [InlineData(300, 270, 150, 180, 120)]
    [InlineData(300, 500, 500, 180, 120)]
    [InlineData(0, 50, 50, 0, 0)]
    public void TheNameAndTheValueShareTheRow(float room, float words, float natural, float wordsGiven, float valueGiven) =>
        Assert.Equal((wordsGiven, valueGiven), Flyout.Share(room, words, natural));

    // --- On a real menu ---------------------------------------------------------------------------------

    /// <summary>A place search that finds two for one letter, twelve for more, nothing for "zz"; at once, as the zones' does.</summary>
    private sealed class Counted : OptionFinder
    {
        public override int LeastLetters => 1;

        public override IReadOnlyList<OptionFound> Offered => [];

        public override Task<IReadOnlyList<OptionFound>> FindAsync(string query, CancellationToken cancel)
        {
            var count = query.Contains("zz", StringComparison.Ordinal) ? 0 : query.Length == 1 ? 2 : 12;
            string[] places = ["Zürich", "Zug", "Zermatt", "Zofingen", "Zollikon", "Zuchwil", "Zernez", "Zizers", "Zweisimmen", "Zell", "Zeiningen", "Zihlschlacht"];
            return Task.FromResult<IReadOnlyList<OptionFound>>([.. places.Take(count).Select(place => new OptionFound(place, "Switzerland", place))]);
        }

        public override string NoneFound(string query) => S.SettingsWidgetWeatherPlaceNotFound(query);
    }

    private static FlyoutEvent Key(Flyout menu, int key) => menu.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)key, 0);

    private static void Type(Flyout menu, string text)
    {
        foreach (var c in text)
        {
            _ = menu.Message(WM.WM_CHAR, (WPARAM)(nuint)c, 0);
        }
    }

    private static void KeysTo(Flyout menu, uint id)
    {
        for (var step = 0; step < 40 && menu.HotId != id; step++)
        {
            _ = Key(menu, VK.VK_DOWN);
        }
        Assert.Equal(id, menu.HotId);
    }

    private static Flyout Show(Gfx gfx, HINSTANCE instance, IReadOnlyList<FlyoutItem> items, bool dark = true)
    {
        // A widget's menu on an arranged bar, one width whatever it says (§230).
        var menu = new Flyout(gfx, instance, 0) { Configures = true };
        menu.Show(gfx, AddListPictures.LookFor(dark), Scale, items, FlyoutLayout.List, Anchor, Work, Edge.Bottom, null);
        WindowShots.Settle();
        return menu;
    }

    private static bool Save(Flyout menu, string name, bool dark) =>
        WindowShots.SaveTogether([.. menu.OpenPanels], name, dark ? ((byte)0, (byte)0, (byte)0) : ((byte)243, (byte)243, (byte)243));

    private static nint At(POINT point) => (nint)((point.y << 16) | (point.x & 0xFFFF));

    /// <summary>The value stands after the row's words and before its chevron; a long one cut, the name whole.</summary>
    [UiFact]
    public void TheValueStandsBetweenTheNameAndTheChevron()
    {
        WindowShots.OnHiddenDesktop("menu-values-look", (gfx, instance) =>
        {
            FlyoutItem[] items =
            [
                new(1, "Style", Children: [new(11, "Graph")], Value: new FlyoutValue("Graph")),
                new(2, "Time zone", Children: [new(21, "Tokyo")], Value: new FlyoutValue(string.Join(", ", Enumerable.Repeat("Osaka, Sapporo, Tokyo", 8)))),
                new(3, "Icon", Children: [new(31, "Home")], Value: new FlyoutValue(Icon: new FlyoutIcon.Glyph(""))),
                new(4, "Colour", Children: [new(41, "Navy")], Value: new FlyoutValue(Icon: new FlyoutTile(new Argb(0xFF, 0x1E, 0x3A, 0x5F), Cell: false))),
            ];
            using var menu = Show(gfx, instance, items);
            var looks = menu.Looks.ToDictionary(look => look.Id);
            foreach (var id in new uint[] { 1, 2, 3, 4 })
            {
                var look = looks[id];
                Assert.True(look.Value.W > 0, $"row {id} shows no value");
                // After the words, apart; before the chevron's 16 DIP slot, 8 DIP in from it.
                Assert.True(look.Value.X >= look.Words.X + look.Words.W + (12 * Scale), $"row {id}: value at {look.Value.X}, words end at {look.Words.X + look.Words.W}");
                Assert.InRange(look.Plate.X + look.Plate.W - (look.Value.X + look.Value.W), (16 + 8 + 8 - 2) * Scale, (16 + 8 + 8 + 2) * Scale);
                Assert.Equal(0, look.Icon.W);
            }
            // The long zone is cut to the menu's one width; the name stays whole.
            Assert.Equal(MathF.Round(Flyout.SettingsWidthDip * Scale), menu.PanelSize.X);
            Assert.True(looks[2].Words.W >= MathF.Floor(gfx.Measure("Time zone", 14 * Scale, Weight.Regular).X) - 1);
            Assert.True(looks[2].Value.W < gfx.Measure(items[1].Value!.Text, 14 * Scale, Weight.Regular).X / 2);
        });
    }

    /// <summary>
    /// §271, §286: a search's level is its head, its field and ten rows' room, its head and its field standing still at
    /// its top, with none, two, twelve and nothing found; all twelve are rows, and the keys going down to
    /// the last scroll the level, the field where it stood; a key typed there goes back to the field
    /// and to the top.
    /// </summary>
    [UiFact]
    public void ASearchsLevelKeepsOneHeightAndShowsEveryResult()
    {
        WindowShots.OnHiddenDesktop("menu-values-search", (gfx, instance) =>
        {
            FlyoutItem[] items =
            [
                new(1, "Remove"),
                new(2, S.SettingsWidgetWeatherPlaceHeader, Children: [new(20, S.SettingsWidgetWeatherPlaceHeader, Control: new FlyoutField("", S.SettingsWidgetWeatherPlacePlaceholder, Finder: new Counted()))]),
            ];
            using var menu = Show(gfx, instance, items);
            _ = menu.Click(2);
            Assert.Equal((1, 20u), (menu.InlineDepth, menu.FieldKeys));
            var search = menu.FieldSearchOf(20)!;
            Assert.True(search.Open);
            var open = menu.PanelSize.Y;
            var field = menu.FieldBoxCentre(20)!.Value;
            // Its head, its field and ten rows' room under them, whatever is found (§271, §286).
            Assert.True(menu.ShownHeight > menu.ContentHeight && menu.ShownHeight >= Flyout.ListFilterFrom * 32 * Scale, $"{menu.ShownHeight}");
            List<string> Results() => [.. menu.FieldShowing.SkipWhile(item => item.Id != 20).Skip(1).Where(item => item.Enabled).Select(item => item.Text)];

            Type(menu, "z");
            Assert.Equal(["Zürich", "Zug"], Results());
            Assert.Equal(open, menu.PanelSize.Y);

            Type(menu, "u");
            Assert.Equal(12, Results().Count);
            Assert.Equal(open, menu.PanelSize.Y);
            Assert.True(menu.ContentHeight > menu.ShownHeight);

            // Down into them and on to the last: the level scrolls under the field, which stands still.
            _ = Key(menu, VK.VK_DOWN);
            Assert.Equal(0u, menu.FieldKeys);
            for (var step = 0; step < 11; step++)
            {
                _ = Key(menu, VK.VK_DOWN);
            }
            var last = menu.HotId!.Value;
            Assert.Equal("Zihlschlacht", menu.FieldShowing.Single(item => item.Id == last).Text);
            Assert.True(menu.Scrolls[0] > 0);
            Assert.Equal(field, menu.FieldBoxCentre(20));
            var seen = menu.ItemOnScreen(last)!.Value;
            Assert.True(seen.bottom - seen.top >= menu.RowFill(last)!.Value.H - 1, "the last row is cut");
            Assert.Equal(open, menu.PanelSize.Y);

            // A key typed there goes back to the field, the level to its top.
            Type(menu, "r");
            Assert.Equal((20u, 0f), (menu.FieldKeys, menu.Scrolls[0]));
            Assert.Equal(open, menu.PanelSize.Y);

            // Nothing found says so in the same room.
            Type(menu, "zz");
            Assert.Equal("zurzz", menu.FieldOf(20)!.Text);
            Assert.Equal(open, menu.PanelSize.Y);
            Assert.Contains(menu.FieldShowing, item => !item.Enabled && item.Text.Length > 0);
            Assert.Empty(Results());
        });
    }

    /// <summary>
    /// §315: a name too long for its row and a long value share it — the value shown, cut, about its two
    /// fifths, never left out; the name cut too — while a short name and value stay whole; in Arabic the
    /// same, turned. Pictured in KARPO_PNG_DIR as menu-values-shared-<en|ar>.png.
    /// </summary>
    [UiFact]
    public void ALongNameAndItsValueShareTheRow()
    {
        const string Zone = "UTC+2 · Amsterdam, Berlin, Bern, Rome, Stockholm, Vienna";
        WindowShots.OnHiddenDesktop("menu-values-shared", (gfx, instance) =>
        {
            foreach (var language in new[] { "en", "ar" })
            {
                using var speaking = InLanguage.Use(language);
                FlyoutItem[] items =
                [
                    new(1, "Style", Children: [new(11, "Graph")], Value: new FlyoutValue("Graph")),
                    new(2, "The time zone the clock face of this folder shows its hours in", Children: [new(21, "Tokyo")], Value: new FlyoutValue(Zone)),
                    new(3, "The place whose weather this face of the folder shows", Children: [new(31, "Zürich")], Value: new FlyoutValue("UTC+9")),
                    new(4, "Time zone", Children: [new(41, "Tokyo")], Value: new FlyoutValue(Zone)),
                ];
                using var menu = Show(gfx, instance, items);
                var looks = menu.Looks.ToDictionary(look => look.Id);
                float Natural(string text) => gfx.Measure(text, 14 * Scale, Weight.Regular).X;
                foreach (var id in new uint[] { 1, 2, 3, 4 })
                {
                    var look = looks[id];
                    Assert.True(look.Value.W > 0 && look.Words.W > 0, $"{language} row {id}: words {look.Words.W}, value {look.Value.W}");
                    // Apart, the value at the row's end: after the words, or before them in Arabic.
                    var gap = language == "ar" ? look.Words.X - (look.Value.X + look.Value.W) : look.Value.X - (look.Words.X + look.Words.W);
                    Assert.True(gap >= 12 * Scale, $"{language} row {id}: {gap} between the words and the value");
                }
                // Short both: whole.
                Assert.True(looks[1].Value.W >= MathF.Floor(Natural("Graph")) - 1 && looks[1].Words.W >= MathF.Floor(Natural("Style")) - 1);
                // Both long: both cut, the value about its two fifths of what they share.
                var shared = looks[2].Words.W + looks[2].Value.W;
                Assert.True(looks[2].Words.W < Natural(items[1].Text) && looks[2].Value.W < Natural(Zone));
                Assert.InRange(looks[2].Value.W / shared, Flyout.ValueShare - 0.06f, Flyout.ValueShare + 0.02f);
                // A long name and a short value: the value whole, the name the rest.
                Assert.True(looks[3].Value.W >= MathF.Floor(Natural("UTC+9")) - 1 && looks[3].Words.W < Natural(items[2].Text));
                // A short name and a long value: the name whole, the value the rest.
                Assert.True(looks[4].Words.W >= MathF.Floor(Natural("Time zone")) - 1 && looks[4].Value.W > shared / 2);
                Assert.True(Save(menu, $"menu-values-shared-{language}.png", dark: true));
            }
        });
    }

    // --- Pictures --------------------------------------------------------------------------------------

    [UiFact]
    public void ValuePictures()
    {
        WindowShots.OnHiddenDesktop("menu-values-png", (gfx, instance) =>
        {
            foreach (var dark in new[] { true, false })
            {
                var tone = dark ? "dark" : "light";

                // A widget's own menu: an alarm's time, days and name; a graph's style; a clock's zone.
                {
                    var alarm = ListKind.Alarm.New();
                    alarm.Alarm = new AlarmOptions { Time = "07:30", Days = AlarmDays.Of(AlarmDays.Weekdays) };
                    var clock = ListKind.Clock.New();
                    clock.Clock!.TimeZone = "Tokyo Standard Time";
                    var place = new OptionPlace(new Config(), dark);
                    List<OptionPick> picks = [];
                    List<FlyoutItem> items = [.. OptionMenu.Rows(OptionRows.Of(ListKind.Cpu.New(), null, place), picks), .. OptionMenu.Rows(OptionRows.Of(clock, null, place), picks), .. OptionMenu.Rows(OptionRows.Of(alarm, null, place), picks)];
                    using var menu = Show(gfx, instance, items, dark);
                    Assert.True(Save(menu, $"menu-values-rows-{tone}.png", dark));
                }

                // A folder's faces: the clock's submenu, its hours and its zone; the folder's icon.
                {
                    var folder = BarKind.FolderList.New();
                    folder.Folder!.Faces = [FolderFace.Of(FolderFaces.ClockId), FolderFace.Of(FolderFaces.WeatherId), FolderFace.Of(FolderFaces.IconId)];
                    var face = folder.Folder.Faces[0];
                    face.Set(face.Info.Options.Single(option => option.Key == ClockFace.Zone), "Tokyo Standard Time");
                    folder.Folder.Faces[1].Weather = new WeatherOptions { Place = "Zürich", Latitude = 47.37, Longitude = 8.54 };
                    folder.Folder.Faces[2].Glyph = "";
                    var items = OptionMenu.FaceSection(folder.Folder, []);
                    using var menu = Show(gfx, instance, items, dark);
                    KeysTo(menu, items.First(item => item.Reorder != 0).Id);
                    _ = Key(menu, VK.VK_RIGHT);
                    WindowShots.Settle();
                    Assert.True(Save(menu, $"menu-values-face-{tone}.png", dark));
                    menu.Hide();
                    using var weather = Show(gfx, instance, items, dark);
                    KeysTo(weather, items.Where(item => item.Reorder != 0).ElementAt(1).Id);
                    _ = Key(weather, VK.VK_RIGHT);
                    WindowShots.Settle();
                    Assert.True(Save(weather, $"menu-values-weather-{tone}.png", dark));
                }

                // A weather face's place: two found, twelve, and scrolled to the last of them.
                {
                    var folder = BarKind.FolderList.New();
                    folder.Folder!.Faces = [FolderFace.Of(FolderFaces.WeatherId)];
                    // A place of its own, its device switch off (§240), so its search is in use.
                    FaceRows.UseDevice(folder.Folder.Faces[0], false);
                    var items = OptionMenu.FaceSection(folder.Folder, []);
                    var weather = items.Single(item => item.Children?.Any(child => child.Text == S.SettingsWidgetWeatherPlaceHeader) == true);
                    var placeRow = weather.Children!.Single(item => item.Text == S.SettingsWidgetWeatherPlaceHeader);
                    var place = placeRow.Children!.Single(item => item.Control is FlyoutField);
                    var counted = placeRow with { Children = [.. placeRow.Children!.Select(item => item == place ? place with { Control = ((FlyoutField)place.Control!) with { Finder = new Counted() } } : item)] };
                    items = [.. items.Select(item => item.Id == weather.Id ? item with { Children = [.. weather.Children!.Select(child => child.Id == placeRow.Id ? counted : child)] } : item)];
                    using var menu = Show(gfx, instance, items, dark);
                    KeysTo(menu, weather.Id);
                    _ = Key(menu, VK.VK_RIGHT);
                    for (var step = 0; step < 8 && menu.HotId != placeRow.Id; step++)
                    {
                        _ = Key(menu, VK.VK_DOWN);
                    }
                    _ = Key(menu, VK.VK_RIGHT);
                    for (var step = 0; step < 3 && menu.FieldKeys != place.Id; step++)
                    {
                        _ = Key(menu, VK.VK_DOWN);
                    }
                    Assert.Equal(place.Id, menu.FieldKeys);
                    Type(menu, "z");
                    WindowShots.Settle();
                    Assert.True(Save(menu, $"menu-values-place-2-{tone}.png", dark));
                    Type(menu, "u");
                    WindowShots.Settle();
                    Assert.True(Save(menu, $"menu-values-place-12-{tone}.png", dark));
                    for (var step = 0; step < 12; step++)
                    {
                        _ = Key(menu, VK.VK_DOWN);
                    }
                    WindowShots.Settle();
                    Assert.True(Save(menu, $"menu-values-place-scrolled-{tone}.png", dark));
                }
            }
        });
    }
}
