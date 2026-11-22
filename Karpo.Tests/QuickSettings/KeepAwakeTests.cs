// Keep awake (docs\widgets.md §269, §586; QuickSettings\Services\KeepAwake.cs): a click holds a
// power request — the PC and its screen kept on, §586 always both — and the next lets it go; the
// widget's options say how long; a timed one says what it has left and turns itself off at its end;
// Windows refusing leaves it off and says so; the last widget gone turns it off; and nothing of it is
// kept but the widget's own options. Its menu at rest offers the lengths, each row with its icon, on
// the bar and in a folder; edit mode's has how long as the widget's setting. Windows' own power
// request runs on calls of the test's own (FakePower) and a clock of the test's own, so no test keeps
// this PC awake, nor sets any power request at all. Pictured to KARPO_PNG_DIR as
// keep-awake-bar-<dark|light>-<100|150>.png (the bar's button off, on, and with its name),
// keep-awake-lines-<dark|light>-<100|150>.png (its line off, on, timed, and three rows tall) and
// keep-awake-menu-<rest|edit>-<dark|light>-<100|150>.png (its menus as the compositor draws them);
// the pictures need KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Core;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.ListFormPictures;
using static TerraFX.Interop.Windows.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.QuickSettings;

public sealed class KeepAwakeTests
{
    // --- Where it is ---------------------------------------------------------------------------

    /// <summary>
    /// It is a toggle (its add list name says "on/off") in the catalogue on both pages, in the add
    /// list's Quick settings beside energy saver and at home in Battery and power, and a toggles row
    /// can hold it; a coffee cup, as PowerToys Awake's, and its page Windows' Power &amp; battery.
    /// </summary>
    [Fact]
    public void TheCatalogueTheAddListAndTheTogglesHaveIt()
    {
        var info = WidgetInfos.KeepAwake;
        Assert.Same(info, WidgetInfos.For(QuickSwitch.KeepAwake));
        Assert.True(info.Flips);
        Assert.Equal("", info.Glyph);
        Assert.Equal("ms-settings:powersleep", info.Settings?.Uri);
        Assert.Equal("Keep awake on/off", info.AddTitle);
        Assert.Contains(Catalogue.On(WidgetType.Bar), entry => entry.Id == info.Id);
        Assert.Contains(Catalogue.On(WidgetType.List), entry => entry.Id == info.Id);
        Assert.Equal(WidgetSource.Switch, BarEntries.Of(BarKind.KeepAwake).Drawing);
        Assert.Equal(WidgetSource.Switch, ListWidgets.Of(ListKind.KeepAwake).Drawing);

        var quick = AddTree.QuickSettings.Rows.OfType<AddLeaf>().Select(leaf => leaf.Id).ToList();
        Assert.Equal(quick.IndexOf("energySaver") + 1, quick.IndexOf(info.Id));
        Assert.Contains(AddTree.QuickSettings.Rows, row => row is AddLeaf { Id: "keepAwake", Again: true });
        var power = AddTree.Browse.Rows.OfType<AddShelf>().Single(shelf => shelf.Id == "power");
        Assert.Contains(power.Rows, row => row is AddLeaf { Id: "keepAwake", Again: false });

        Assert.Equal(QuickSwitch.KeepAwake, Toggles.Find(Toggle.KeepAwake)?.Switch);
        Assert.Equal(info.Title, Toggles.Find(Toggle.KeepAwake)?.Name);
        using var real = QuickSwitches.Windows(QuickSwitch.KeepAwake);
        Assert.IsType<KeepAwakeSwitch>(real);
    }

    // --- On and off ----------------------------------------------------------------------------

    /// <summary>A click holds the request, until turned off unless the widget says another length; the next lets it go.</summary>
    [Fact]
    public void AClickHoldsTheRequestAndTheNextLetsItGo()
    {
        var (service, request, _) = Made();
        using var awake = Started(service, out var heard);
        Assert.Equal(false, awake.On);
        Assert.True(awake.Switches);
        Assert.Equal(S.DockQuickKeepAwakeNote, awake.Note);

        awake.Act();
        Assert.Equal(true, awake.On);
        Assert.True(request.Held);
        Assert.True(request.Screen);
        Assert.Equal(0, service.Minutes);
        Assert.Null(service.Left);
        // On until turned off says "On", nothing more.
        Assert.Null(awake.Detail);
        Assert.Equal(1, heard.Count);

        awake.Act();
        Assert.Equal(false, awake.On);
        Assert.False(request.Held);
        Assert.Equal(0, request.Open);
        Assert.Equal(["create", "set system", "set display", "clear display", "clear system", "close"], request.Asked);
        Assert.Equal(2, heard.Count);
    }

    /// <summary>
    /// §586 (the person: "keep awake should also keep the screen awake"): every way on — a click with the widget's options or
    /// none (a toggles row's), a length from its menu — keeps the screen on with the PC: the one request asks Windows for both,
    /// so the display's own timeout no longer darkens the screen.
    /// </summary>
    [Fact]
    public void EveryWayOnKeepsTheScreenOnToo()
    {
        var (service, request, _) = Made();
        using var awake = Started(service, out _);
        foreach (var on in new Action[] { () => awake.Act(), () => awake.Act(new SwitchOptions { AwakeMinutes = 30 }), () => service.Turn(60) })
        {
            on();
            Assert.Equal((true, true, true), (service.On, request.Held, request.Screen));
            Assert.Equal(S.DockQuickKeepAwakeNote, awake.Note);
            service.Off();
            Assert.Equal((false, false, 0), (request.Held, request.Screen, request.Open));
        }
    }

    /// <summary>
    /// Windows' request's life (PowerRequest): made once with Karpo's reason, the PC and its screen set; held again it asks
    /// nothing more; let go, both cleared and its handle closed; let go twice is nothing; held after, a new one.
    /// </summary>
    [Fact]
    public void TheRequestIsMadeOnceAndLetGoWhole()
    {
        var power = new FakePower();
        var request = new PowerRequest(power);
        Assert.True(request.Hold());
        Assert.True(request.Hold());
        Assert.Equal(S.DockQuickKeepAwakeReason, power.Reason);
        Assert.Equal(["create", "set system", "set display"], power.Asked);
        Assert.Equal((true, true, 1), (power.Held, power.Screen, power.Open));

        request.Release();
        request.Release();
        Assert.Equal(["create", "set system", "set display", "clear display", "clear system", "close"], power.Asked);
        Assert.Equal((false, false, 0), (power.Held, power.Screen, power.Open));

        Assert.True(request.Hold());
        Assert.Equal((true, true, 1), (power.Held, power.Screen, power.Open));
        request.Release();
    }

    /// <summary>
    /// Windows refusing any part lets the whole request go: no request made is nothing to close; the screen refused clears the
    /// PC's part and closes the handle — never the PC kept awake behind a dark screen, which is what the person saw.
    /// </summary>
    [Fact]
    public void APartRefusedLetsTheWholeRequestGo()
    {
        var power = new FakePower { Refuses = true };
        var request = new PowerRequest(power);
        Assert.False(request.Hold());
        Assert.Equal(["create"], power.Asked);

        power = new FakePower { RefusesScreen = true };
        request = new PowerRequest(power);
        Assert.False(request.Hold());
        Assert.Equal(["create", "set system", "set display", "clear system", "close"], power.Asked);
        Assert.Equal((false, false, 0), (power.Held, power.Screen, power.Open));
    }

    /// <summary>
    /// Timed, it says what it has left in whole minutes rounded up, its tooltip and line told each
    /// minute, and at its end it lets go by itself; turned on again, it starts again from then.
    /// </summary>
    [Fact]
    public void ATimedOneSaysWhatItHasLeftAndTurnsItselfOff()
    {
        var (service, request, clock) = Made();
        using var awake = Started(service, out var heard);
        awake.Act(new SwitchOptions { AwakeMinutes = 30 });
        Assert.Equal(30, service.Minutes);
        Assert.Equal(S.DockQuickKeepAwakeLeft("30 min"), awake.Detail);

        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(S.DockQuickKeepAwakeLeft("30 min"), awake.Detail);
        var told = heard.Count;
        clock.Advance(TimeSpan.FromSeconds(31));
        Assert.Equal(S.DockQuickKeepAwakeLeft("29 min"), awake.Detail);
        Assert.True(heard.Count > told, "the minute's change was not told");

        clock.Advance(TimeSpan.FromMinutes(28.5));
        Assert.Equal(S.DockQuickKeepAwakeLeft("1 min"), awake.Detail);
        Assert.Equal(true, awake.On);
        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.Equal(false, awake.On);
        Assert.False(request.Held);
        Assert.Null(awake.Detail);
        Assert.Equal(0, request.Open);

        // Four hours, said in hours and minutes; another length from the menu starts from now.
        service.Turn(240);
        Assert.Equal(S.DockQuickKeepAwakeLeft("4 h"), awake.Detail);
        clock.Advance(TimeSpan.FromMinutes(100));
        Assert.Equal(S.DockQuickKeepAwakeLeft("2 h 20 min"), awake.Detail);
        service.Turn(60);
        Assert.Equal(S.DockQuickKeepAwakeLeft("1 h"), awake.Detail);
        clock.Advance(TimeSpan.FromMinutes(60));
        Assert.Equal(false, awake.On);
        // Until turned off, nothing ends it.
        service.Turn(0);
        clock.Advance(TimeSpan.FromDays(2));
        Assert.Equal(true, awake.On);
    }

    /// <summary>Where Windows refuses, it stays off, the click's wait ends (a refusal), and the tooltip says why until the next.</summary>
    [Fact]
    public void ARefusalLeavesItOffAndSaysSo()
    {
        var (service, request, _) = Made();
        request.Refuses = true;
        using var awake = Started(service, out _);
        awake.Act();
        Assert.Equal(false, awake.On);
        Assert.Equal(1, awake.Refusals);
        Assert.Equal(S.DockQuickKeepAwakeRefused, awake.Note);
        request.Refuses = false;
        awake.Act();
        Assert.Equal(true, awake.On);
        Assert.Equal(S.DockQuickKeepAwakeNote, awake.Note);
    }

    /// <summary>
    /// Every widget for it reads one: the last one gone (taken off the bar, Karpo closing) turns it
    /// off, since nothing would be left to say the PC is kept awake. One that never started counts
    /// for nothing (an isolated run stands Windows' own down before it starts).
    /// </summary>
    [Fact]
    public void TheLastWidgetGoneTurnsItOff()
    {
        var (service, request, _) = Made();
        var first = Started(service, out _);
        var second = Started(service, out _);
        new KeepAwakeSwitch("Keep awake", service).Dispose();
        first.Act();
        Assert.Equal(true, second.On);
        first.Dispose();
        Assert.True(request.Held);
        second.Dispose();
        Assert.False(service.On);
        Assert.False(request.Held);
    }

    /// <summary>
    /// What a click turns on is the widget's, in the file: a length it offers, not written while it is
    /// the default, a length it does not offer read as until turned off, and another switch's dropped.
    /// Whether it is on is nowhere in the file, nor (§586) anything of the screen: it is always kept on.
    /// </summary>
    [Fact]
    public void OnlyTheWidgetsOptionsAreKept()
    {
        var config = Configs.Repaired($$"""
            { "version": {{Config.Shape}}, "layouts": [ { "id": "default", "name": "Default", "islands": [
              { "id": "a", "kind": "keepAwake", "switch": { "awakeMinutes": 120 } },
              { "id": "b", "kind": "keepAwake", "switch": { "awakeMinutes": 45 } },
              { "id": "c", "kind": "energySaver", "switch": { "awakeMinutes": 60 } },
              { "id": "d", "kind": "keepAwake" } ] } ] }
            """);
        var bar = Configs.Islands(config);
        Assert.Equal(["a", "b", "c", "d"], bar.Select(island => island.Id));
        Assert.Equal([120, 0, 0, 0], bar.Select(island => island.Switch!.AwakeMinutes));
        var json = config.ToJson();
        Assert.Equal(1, json.Split("awakeMinutes").Length - 1);

        var (service, _, _) = Made();
        service.Turn(60);
        Assert.Equal(json, config.ToJson());
    }

    // --- Its menus -----------------------------------------------------------------------------

    /// <summary>
    /// At rest (§257), on the bar and as a folder's line alike: the lengths, the one running ticked; its page in Windows — each
    /// row with its icon (§249), none of edit mode's, §586 nothing of the screen (always kept on). A length picked turns it on
    /// from now, the screen with it, and keeps the menu open.
    /// </summary>
    [Fact]
    public void TheMenuAtRestOffersTheLengths()
    {
        var (service, request, _) = Made();
        KeepAwake.Made = () => service;
        try
        {
            string[] expected =
            [
                "Until turned off", "For 30 minutes", "For 1 hour", "For 2 hours", "For 4 hours", "—",
                "Power & battery settings",
            ];
            foreach (var island in new WidgetItem[] { ListKind.KeepAwake.New(), BarKind.KeepAwake.New() })
            {
                var (items, acts) = TheDock.RestMenuRows(island, inFolder: island is CellItem);
                Assert.Equal(expected, Texts(items));
                Assert.DoesNotContain(items, item => item.Primary);
                Assert.All(items.Where(item => !item.Header && !item.Separator), item => Assert.IsType<FlyoutIcon.Glyph>(item.Icon));
                Assert.DoesNotContain(items, item => item.Checked == true);
                Assert.All(acts.Where(act => act.Act is WidgetAct.AwakeFor), act => Assert.True(MenuKeeps.Stays(act.Act)));
                Assert.All(acts.Where(act => act.Act is WidgetAct.AwakeFor), act => Assert.False(TheDock.HandsAway(act.Act)));
            }

            // Picked from off, a length turns it on from now, the screen with it.
            var line = ListKind.KeepAwake.New();
            var (rows, picks) = TheDock.RestMenuRows(line, inFolder: true);
            TheDock.RunAct(Pick(rows, picks, "For 2 hours"), widget: null, Opened);
            Assert.Equal((true, 120, true), (service.On, service.Minutes, request.Screen));
            (rows, picks) = TheDock.RestMenuRows(line, inFolder: true);
            Assert.Equal(expected, Texts(rows));
            Assert.Equal(["For 2 hours"], rows.Where(row => row.Checked == true).Select(row => row.Text));
            // Another length from now, the one request kept.
            TheDock.RunAct(Pick(rows, picks, "Until turned off"), widget: null, Opened);
            Assert.Equal((true, 0, true), (service.On, service.Minutes, request.Screen));
            Assert.Equal(["create", "set system", "set display"], request.Asked);
            // Its page is Windows' Power & battery.
            TheDock.RunAct(Pick(rows, picks, "Power & battery settings"), widget: null, Opened);
            Assert.Equal(["ms-settings:powersleep"], opened);
        }
        finally
        {
            KeepAwake.Made = null;
        }
    }

    /// <summary>Edit mode's settings (§233): how long a click keeps it awake, each length its icon; it writes the widget's options. §586 nothing of the screen.</summary>
    [Fact]
    public void EditModeSetsHowLong()
    {
        var island = BarKind.KeepAwake.New();
        var rows = OptionRows.Of(island);
        var length = rows.Single(row => row.Key == OptionRows.AwakeLength);
        Assert.Equal(S.SettingsWidgetAwakeLengthHeader, length.Title);
        Assert.Equal(["Until turned off", "For 30 minutes", "For 1 hour", "For 2 hours", "For 4 hours"], length.Choices);
        Assert.Equal(0, length.Chosen);
        Assert.Equal(5, length.ChoiceGlyphs.Distinct().Count());
        Assert.NotEmpty(length.Glyph);
        Assert.DoesNotContain(rows, row => row.Title == "Keep the screen on");

        Assert.True(length.Set!(3));
        Assert.Equal(120, island.Switch!.AwakeMinutes);
        Assert.Equal(3, OptionRows.Of(island).Single(row => row.Key == OptionRows.AwakeLength).Chosen);
        // Nobody else's switch has it.
        Assert.DoesNotContain(OptionRows.Of(BarKind.EnergySaver.New()), row => row.Key is OptionRows.AwakeLength);
    }

    // --- The bar, the line, the menus ------------------------------------------------------------

    /// <summary>The line says a timed one's time left beside its name on one row, and its tooltip says it after the name.</summary>
    [UiFact]
    public void TheLineAndTheTooltipSayTheTimeLeft() =>
        Offscreen.OnStaThread(gfx =>
        {
            var (service, _, clock) = Made();
            QuickSwitches.Source = which => new KeepAwakeSwitch(WidgetInfos.For(which).Title, service);
            var folder = Folder(gfx, Line(ListKind.KeepAwake));
            var line = (SwitchIsland)folder.Tiles[0].Island;
            try
            {
                var look = LookFor(dark: true);
                var style = FolderFlyout.StyleFor(1, look, FlyoutTheme.For(look));
                var box = PopupPaint.Rect(0, 0, 360, 40);
                Assert.Null(line.LineOf(box, style).SubText);
                line.Click(0);
                Assert.Equal(true, line.On);
                Assert.Null(line.LineOf(box, style).SubText);
                Assert.StartsWith(S.WidgetSwitchOnTip("Keep awake"), line.Tooltip(0));

                service.Turn(60);
                clock.Advance(TimeSpan.FromMinutes(20));
                var left = S.DockQuickKeepAwakeLeft("40 min");
                Assert.Equal(left, line.LineOf(box, style).SubText);
                Assert.StartsWith(S.WidgetSwitchDetailTip(name: "Keep awake", detail: left), line.Tooltip(0));
                line.Click(0);
                Assert.Equal(false, line.On);
                Assert.Null(line.LineOf(box, style).SubText);
            }
            finally
            {
                QuickSwitches.Source = null;
                folder.Closed();
                folder.Dispose();
            }
        });

    /// <summary>
    /// The pictures: the bar's button off, on, and with its name, at a 40 and a 48 DIP bar; its lines
    /// off, on until turned off, timed, and three rows tall; dark and light, at 100 % and 150 %.
    /// </summary>
    [UiFact]
    public void TheBarAndTheLinesArePictured()
    {
        foreach (var dark in new[] { true, false })
        {
            foreach (var scale in new[] { 1f, 1.5f })
            {
                Offscreen.OnStaThread(gfx =>
                {
                    var suffix = $"{(dark ? "dark" : "light")}-{scale * 100:0}";
                    var made = new Queue<KeepAwake>();
                    QuickSwitches.Source = which => new KeepAwakeSwitch(WidgetInfos.For(which).Title, made.Dequeue());
                    try
                    {
                        Bar(gfx, dark, scale, made, $"keep-awake-bar-{suffix}.png");
                        Lines(gfx, dark, scale, made, $"keep-awake-lines-{suffix}.png");
                    }
                    finally
                    {
                        QuickSwitches.Source = null;
                    }
                });
            }
        }
    }

    /// <summary>Its menus as the dock shows them, at rest (on for an hour) and in edit mode, on a desktop of their own.</summary>
    [UiFact]
    public void TheMenusArePictured()
    {
        var (service, _, _) = Made();
        KeepAwake.Made = () => service;
        List<FlyoutItem> rest;
        List<FlyoutItem> edit;
        try
        {
            service.Turn(60);
            var island = BarKind.KeepAwake.New();
            rest = TheDock.RestMenuRows(island, inFolder: false).Items;
            edit = [.. TheDock.WidgetRowsFor(island), .. OptionMenu.Section(OptionRows.Of(island), [])];
            Assert.Equal(["For 1 hour"], rest.Where(row => row.Checked == true).Select(row => row.Text));
        }
        finally
        {
            KeepAwake.Made = null;
        }
        WindowShots.OnHiddenDesktop("keep-awake-menus", (gfx, instance) =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    var look = AddListPictures.LookFor(dark);
                    var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
                    var anchor = new POINT { x = work.right / 2, y = work.bottom };
                    foreach (var (mode, rows) in new[] { ("rest", rest), ("edit", edit) })
                    {
                        using var menu = new Flyout(gfx, instance, 0) { SpokenName = "Keep awake" };
                        menu.Show(gfx, look, scale, rows, FlyoutLayout.List, anchor, work, Edge.Bottom);
                        WindowShots.Settle();
                        var file = $"keep-awake-menu-{mode}-{(dark ? "dark" : "light")}-{(int)(scale * 100)}.png";
                        Assert.True(WindowShots.Save(menu.Hwnd, file), $"{file} came back blank");
                        menu.Hide();
                    }
                }
            }
        });
    }

    /// <summary>The bar's button off, on, and on with its name beside it, at a 40 and a 48 DIP bar.</summary>
    private static unsafe void Bar(Gfx gfx, bool dark, float scale, Queue<KeepAwake> made, string file)
    {
        var look = LookFor(dark);
        var theme = FlyoutTheme.For(look);
        var dpi = (uint)(96 * scale);
        var states = new (bool On, bool Label)[] { (false, false), (true, false), (false, true), (true, true) };
        var islands = new List<(SwitchIsland Island, IslandStyle Style, float X, float Y, float W, float H)>();
        const float gap = 12;
        var y = gap;
        var right = 0f;
        foreach (var height in new[] { 40, 48 })
        {
            var style = new IslandStyle(scale, false, Layout.CellPx(dpi, new Config { Height = height }), Layout.IconPx(dpi, new Config { Height = height }), look);
            var x = gap;
            var tallest = 0f;
            foreach (var (on, label) in states)
            {
                var (service, _, _) = Made();
                made.Enqueue(service);
                var entry = BarKind.KeepAwake.New();
                entry.Id = $"k{islands.Count}";
                entry.Switch!.ShowLabel = label;
                var island = new SwitchIsland(Config.IslandKey(null, entry.Id), entry, gfx, new Config());
                if (on)
                {
                    service.Turn(0);
                }
                var (w, h) = island.CellSize(0, style);
                islands.Add((island, style, x, y, w, h));
                x += w + gap;
                tallest = MathF.Max(tallest, h);
            }
            right = MathF.Max(right, x);
            y += tallest + gap;
        }
        try
        {
            Offscreen.Bitmap(right, y, dc =>
            {
                PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, right, y), 0, PopupPaint.Ink(theme.Base));
                foreach (var (island, style, left, top, w, h) in islands)
                {
                    Offscreen.Shifted(dc, left, top, () => island.PaintCell(dc, 0, style, w, h));
                }
            }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, file)));
        }
        finally
        {
            foreach (var (island, _, _, _, _, _) in islands)
            {
                island.Dispose();
            }
        }
    }

    /// <summary>Its lines off, on until turned off, on for an hour with 40 minutes left, and the same three rows tall.</summary>
    private static void Lines(Gfx gfx, bool dark, float scale, Queue<KeepAwake> made, string file)
    {
        var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
        try
        {
            foreach (var textScale in TextScales)
            {
                var services = new List<(KeepAwake Service, TestClock Clock)>();
                for (var i = 0; i < 4; i++)
                {
                    var (service, _, clock) = Made();
                    made.Enqueue(service);
                    services.Add((service, clock));
                }
                var folder = Folder(gfx, Line(ListKind.KeepAwake), Line(ListKind.KeepAwake), Line(ListKind.KeepAwake),
                    Line(ListKind.KeepAwake, line => line.Height = LineHeight.Exactly(6)));
                services[1].Service.Turn(0);
                foreach (var (service, clock) in services.Skip(2))
                {
                    service.Turn(60);
                    clock.Advance(TimeSpan.FromMinutes(20));
                }
                var look = LookFor(dark, textScale);
                var (_, plan) = Plan(folder, look, scale);
                panels.Add(($"Keep awake: {textScale * 100:0} % text", look, folder, plan));
            }
            Sheet(gfx, file, scale, panels);
        }
        finally
        {
            foreach (var (_, _, folder, _) in panels)
            {
                folder.Closed();
                folder.Dispose();
            }
        }
    }

    // --- What the tests share ------------------------------------------------------------------

    private static readonly List<string> opened = [];

    private static void Opened(string target, string? verb) => opened.Add(target);

    private static string[] Texts(IEnumerable<FlyoutItem> rows) =>
        [.. rows.Select(row => row.Separator ? "—" : row.Header ? "# " + row.Text : row.Text)];

    private static FlyoutItem Row(IEnumerable<FlyoutItem> rows, string text) => rows.Single(row => row.Text == text && !row.Header);

    private static WidgetActRow Pick(List<FlyoutItem> rows, List<WidgetActRow> acts, string text) =>
        TheDock.ActRowOf(acts, Row(rows, text).Id) ?? throw new InvalidOperationException($"\"{text}\" is no act");

    /// <summary>A keep awake of the test's own: Windows' own request over calls of the test's own, and its clock.</summary>
    private static (KeepAwake Service, FakePower Request, TestClock Clock) Made()
    {
        var power = new FakePower();
        var clock = new TestClock();
        return (new KeepAwake(new PowerRequest(power), clock), power, clock);
    }

    /// <summary>A switch over <paramref name="service"/>, started, and what it has heard (<paramref name="heard"/>, one a time).</summary>
    private static KeepAwakeSwitch Started(KeepAwake service, out List<int> heard)
    {
        var told = new List<int>();
        heard = told;
        var awake = new KeepAwakeSwitch("Keep awake", service);
        awake.Start(() => told.Add(told.Count));
        return awake;
    }

    /// <summary>
    /// Windows' power calls as the test's own: they set nothing, write down what they were asked and keep what one request
    /// would hold; closing a handle clears what it held, as Windows does.
    /// </summary>
    private sealed class FakePower : IPowerCalls
    {
        private readonly HashSet<POWER_REQUEST_TYPE> kept = [];
        private nint last = 0x40;

        public List<string> Asked { get; } = [];

        /// <summary>Windows makes no request.</summary>
        public bool Refuses { get; set; }

        /// <summary>Windows will not keep the screen on.</summary>
        public bool RefusesScreen { get; set; }

        public string? Reason { get; private set; }

        /// <summary>How many requests are open: made and not closed.</summary>
        public int Open { get; private set; }

        public bool Held => kept.Contains(POWER_REQUEST_TYPE.PowerRequestSystemRequired);

        public bool Screen => kept.Contains(POWER_REQUEST_TYPE.PowerRequestDisplayRequired);

        public nint Create(string reason)
        {
            Asked.Add("create");
            if (Refuses)
            {
                return 0;
            }
            Reason = reason;
            Open++;
            return ++last;
        }

        public bool Set(nint request, POWER_REQUEST_TYPE type)
        {
            Asked.Add("set " + Word(type));
            return (type != POWER_REQUEST_TYPE.PowerRequestDisplayRequired || !RefusesScreen) && kept.Add(type);
        }

        public bool Clear(nint request, POWER_REQUEST_TYPE type)
        {
            Asked.Add("clear " + Word(type));
            return kept.Remove(type);
        }

        public void Close(nint request)
        {
            Asked.Add("close");
            Open--;
            kept.Clear();
        }

        private static string Word(POWER_REQUEST_TYPE type) => type == POWER_REQUEST_TYPE.PowerRequestDisplayRequired ? "display" : "system";
    }
}

/// <summary>A clock of the test's own: time moves when the test says, and the timers made on it fire then, in order, on the test's thread.</summary>
internal sealed class TestClock : TimeProvider
{
    private readonly List<Ticking> timers = [];

    public DateTimeOffset Now { get; private set; } = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new Ticking(this, callback, state);
        timer.Change(dueTime, period);
        timers.Add(timer);
        return timer;
    }

    /// <summary>Time moves on by <paramref name="by"/>, every timer due on the way firing at its time.</summary>
    public void Advance(TimeSpan by)
    {
        var end = Now + by;
        while (timers.Where(timer => timer.Due is { } due && due <= end).MinBy(timer => timer.Due) is { } next)
        {
            Now = next.Due!.Value;
            next.Due = null;
            next.Fire();
        }
        Now = end;
    }

    private sealed class Ticking(TestClock clock, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset? Due { get; set; }

        public void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            Due = dueTime == Timeout.InfiniteTimeSpan ? null : clock.Now + dueTime;
            return true;
        }

        public void Dispose() => Due = null;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
