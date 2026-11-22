// A line's page in an open folder (docs\widgets.md §170, Karpo.Core\FolderPages.cs): Windows' ">" on
// a quick settings tile. The page table; the column swapped for the page and back (the back arrow,
// Escape, the folder closing), nothing of it in the config; the ">" on the Wi-Fi, Bluetooth and
// accessibility lines and on the volume; the footer's Edit; and the accessibility page's three new
// switches on made-up tools. Every switch is a made-up one (QuickSwitches.Source), so nothing here
// turns a radio, the narrator, the magnifier or sticky keys on this PC.
//
// Pictured: quick-pages-<dark|light>-<100|150>.png — the default quick settings and its Wi-Fi,
// Bluetooth, sound and accessibility pages side by side, on the sample networks, devices and sound.

using System.Numerics;
using System.Text.Json;
using Karpo.Dock;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.ListFormPictures;
using static TerraFX.Interop.Windows.Windows;
using Edge = Karpo.Core.Edge;

namespace Karpo.Tests.QuickSettings;

public sealed unsafe class QuickPagesTests
{
    private static readonly PresetMachine Laptop = new(HasBattery: true, FixedDrives: []);

    /// <summary>Every page holds lines a list holds, a heading first where it has a title of its own, under ids of its own.</summary>
    [Fact]
    public void EveryPageIsLinesAListHolds()
    {
        var paged = Enum.GetValues<ListKind>().Where(FolderPages.Has).ToList();
        Assert.Equal(
            [ListKind.SoundVolume, ListKind.WifiSwitch, ListKind.BluetoothSwitch, ListKind.MobileHotspot, ListKind.Vpn, ListKind.NearbyShare, ListKind.Accessibility, ListKind.Cast, ListKind.Project],
            paged);
        foreach (var kind in paged)
        {
            var page = FolderPages.Of(kind)!;
            var cells = FolderPages.Cells(page);
            Assert.Equal(page.Lines.Count + (page.Head is null ? 0 : 1), cells.Count);
            Assert.Equal(page.Head is not null, cells[0].Island.Kind == "heading");
            Assert.Equal(Enumerable.Range(0, cells.Count).Select(index => $"page-{index}"), cells.Select(cell => cell.Island.Id));
            Assert.All(cells, cell => Assert.NotNull(ListWidgets.Find(cell.Island.Kind)));
            // Windows' page ends with its settings: every page's last line is a footer.
            Assert.Equal("actions", cells[^1].Island.Kind);
        }
        Assert.Equal(["wifi", "actions"], Kinds(ListKind.WifiSwitch));
        Assert.Equal(["bluetooth", "actions"], Kinds(ListKind.BluetoothSwitch));
        Assert.Equal(["heading", "soundOutput", "soundInput", "spatialSound", "divider", "soundMixer", "actions"], Kinds(ListKind.SoundVolume));
        Assert.Equal(["heading", "magnifier", "colorFilters", "narrator", "monoAudio", "liveCaptions", "stickyKeys", "actions"], Kinds(ListKind.Accessibility));
        Assert.Equal(["vpnConnections", "actions"], Kinds(ListKind.Vpn));
        // Windows' Cast and Project pages: the wireless displays, Win+P's modes (2026-10-16).
        Assert.Equal(["wirelessDisplays", "actions"], Kinds(ListKind.Cast));
        Assert.Equal(["projection", "actions"], Kinds(ListKind.Project));
        Assert.False(FolderPages.Has(ListKind.Brightness));
    }

    private static string[] Kinds(ListKind kind) => [.. FolderPages.Cells(FolderPages.Of(kind)!).Select(cell => cell.Island.Kind)];

    /// <summary>Where the open folder's line of <paramref name="kind"/> stands among its tiles.</summary>
    private static int LineOf(FolderIsland folder, string kind) =>
        folder.Tiles.Select((tile, index) => (tile, index)).First(each => each.tile.Cell.Island.Kind == kind).index;

    /// <summary>
    /// A new quick settings button is what is playing over Windows' own tiles (2026-10-16): Wi-Fi,
    /// Bluetooth and accessibility with their pages, live captions, cast and project, the plain on/off
    /// tiles a row of toggles in Windows' order, the two sliders, the battery and the pencil and the
    /// gear at its foot; on a PC without a battery the foot alone.
    /// </summary>
    [Fact]
    public void ANewQuickSettingsIsWindowsTiles()
    {
        string[][] Lists(PresetMachine machine) =>
            [.. Presets.Lists(Presets.Find(Presets.QuickPanelId)!, machine).Select(list => list.Select(cell => cell.Island.Kind).ToArray())];
        Assert.Equal([["media", "divider", "wifiSwitch", "bluetoothSwitch", "accessibility", "liveCaptions", "cast", "project", "toggles", "brightness", "soundVolume", "divider", "battery", "actions"]], Lists(Laptop));
        Assert.Equal([["media", "divider", "wifiSwitch", "bluetoothSwitch", "accessibility", "liveCaptions", "cast", "project", "toggles", "brightness", "soundVolume", "actions"]], Lists(new PresetMachine(false, [])));
        var cells = Presets.Folder(Presets.Find(Presets.QuickPanelId)!, Laptop).Folder!.Cells;
        Assert.Equal([Toggle.AirplaneMode, Toggle.Vpn, Toggle.EnergySaver, Toggle.NightLight, Toggle.MobileHotspot, Toggle.NearbyShare], cells.Single(cell => cell.Island.Kind == "toggles").Island.Toggles!.Shown);
        // Accessibility, cast and project switch nothing: each is a page.
        Assert.All(cells.Where(cell => cell.Island.Kind is "cast" or "project" or "accessibility"), cell => Assert.True(FolderPages.Has(ListWidgets.Find(cell.Island.Kind)!.Kind)));
        var foot = cells[^1].Island.Actions!;
        Assert.Equal((SettingsActions.EditId, SettingsActions.AllSettingsId), (foot.Left, foot.Right));
        Assert.Equal("ms-settings:", SettingsActions.Find(SettingsActions.AllSettingsId)!.Uri);
    }

    /// <summary>
    /// The page stands in its line's column, as Windows' panel swaps for it: its lines are the
    /// folder's tiles while it stands, made and dropped with it; the back arrow, Escape and the folder
    /// closing each bring the folder's lines back; nothing of it reaches the config.
    /// </summary>
    [UiFact]
    public void APageStandsInItsLinesColumnAndGoesBack()
    {
        Offscreen.OnStaThread(gfx =>
        {
            QuickSwitches.Source = which => new SampleSwitch(which.ToString(), on: true, switches: true);
            try
            {
                var holder = Presets.Folder(Presets.Find(Presets.QuickPanelId)!, Laptop);
                var before = JsonSerializer.Serialize(holder, ConfigJson.Default.BarItem);
                using var folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
                var changes = 0;
                folder.Changed += () => changes++;
                folder.Opened(gfx);
                var lines = folder.Tiles.Select(tile => tile.Island).ToList();
                var wifi = lines[LineOf(folder, "wifiSwitch")];
                Assert.False(folder.OpenPageOf(lines[LineOf(folder, "toggles")].Key), "the toggles row has no page");
                Assert.False(folder.OpenPageOf(lines[LineOf(folder, "media")].Key), "what is playing has no page");
                Assert.True(folder.OpenPageOf(wifi.Key));
                Assert.True(folder.Paged);
                Assert.Equal(wifi.Key, folder.PageOf);
                Assert.Equal(["wifi", "actions"], folder.Tiles.Select(tile => tile.Cell.Island.Kind));
                Assert.Single(folder.Laid.Lists);
                Assert.IsType<QuickPageTile>(folder.Tiles[0].Island);
                Assert.Equal(1, changes);
                // The folder's own lines and its config are as they were.
                Assert.Equal(lines.Count, folder.Options.Cells.Count);
                Assert.Same(holder.Folder, folder.Options);
                Assert.Equal(before, JsonSerializer.Serialize(holder, ConfigJson.Default.BarItem));
                Assert.Same(wifi, folder.Widget(wifi.Key));

                // Escape: back to the lines, the page's widgets gone with it.
                var page = folder.Tiles[0].Island;
                Assert.True(folder.Escape());
                Assert.False(folder.Paged);
                Assert.Equal(lines, folder.Tiles.Select(tile => tile.Island));
                Assert.Null(folder.Widget(page.Key));
                Assert.False(folder.Escape(), "a second Escape is the folder's own");

                // The back arrow of a quick settings page, from inside its press: on the folder's next news.
                Assert.True(folder.OpenPageOf(lines[LineOf(folder, "bluetoothSwitch")].Key));
                var bluetooth = Assert.IsType<QuickPageTile>(folder.Tiles[0].Island);
                Assert.NotNull(bluetooth.Back);
                bluetooth.Back!();
                Assert.True(folder.Paged);
                _ = ((IIsland)folder).Refresh(gfx);
                Assert.False(folder.Paged);

                // A page with a title of its own starts with its head, whose arrow goes back too.
                Assert.True(folder.OpenPageOf(lines[LineOf(folder, "accessibility")].Key));
                var head = Assert.IsType<PageHeadTile>(folder.Tiles[0].Island);
                Assert.Equal(WidgetInfos.Accessibility.Title, head.Text);
                _ = head.Click(PageHeadTile.BackTarget);
                _ = ((IIsland)folder).Refresh(gfx);
                Assert.False(folder.Paged);

                // The config changing under a page keeps it; closing the folder ends it.
                Assert.True(folder.OpenPageOf(lines[LineOf(folder, "soundVolume")].Key));
                folder.Configure(holder, new Config());
                Assert.True(folder.Paged);
                Assert.Equal("heading", folder.Tiles[0].Cell.Island.Kind);
                folder.Closed();
                Assert.False(folder.Paged);
                Assert.Equal(lines.Count, folder.Tiles.Count);
                Assert.Equal(before, JsonSerializer.Serialize(holder, ConfigJson.Default.BarItem));
            }
            finally
            {
                QuickSwitches.Source = null;
            }
        });
    }

    /// <summary>
    /// A switch's line with a page: the switch is switched where it is clicked and nowhere else, and
    /// all the rest of the line, its ">" with it, opens the page (2026-10-16, the person: "the toggle
    /// button should only be clicked when you click it directly, everywhere else it opens the sub
    /// flyout"). Accessibility switches nothing, so all of it is the page. On a page's own line there
    /// is no ">".
    /// </summary>
    [UiFact]
    public void OnlyTheSwitchSwitchesAndTheRestOpensThePage()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var radio = new CountingSwitch { On = true };
            QuickSwitches.Source = which => which == QuickSwitch.WifiRadio ? radio : new SampleSwitch(which.ToString(), on: false);
            try
            {
                var holder = Presets.Folder(Presets.Find(Presets.QuickPanelId)!, Laptop);
                using var folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
                folder.Opened(gfx);
                var wifi = Assert.IsType<SwitchIsland>(folder.Tiles[LineOf(folder, "wifiSwitch")].Island);
                Assert.True(wifi.PageShown);
                var style = FolderFlyout.StyleFor(1.5f, LookFor(dark: true), FlyoutTheme.For(LookFor(dark: true)));
                var box = PopupPaint.Rect(0, 0, 540, 60);
                var hit = wifi.ToggleHit(box, style);
                Assert.True(TilePaint.Width(hit) > 0, "the Wi-Fi line draws no switch");
                var toggle = new Vector2((hit.left + hit.right) / 2, (hit.top + hit.bottom) / 2);
                Assert.Equal(SwitchIsland.ToggleTarget, wifi.CardTarget(toggle, box, style, WidgetSurface.List));
                // The ">", the name, the glyph and the room between the name and the switch: the page's.
                var chevron = SwitchIsland.ChevronGlyphBox(box, style, switched: true);
                foreach (var x in new[] { (chevron.left + chevron.right) / 2, 40f, 140f, hit.left - 6, hit.right + 2 })
                {
                    Assert.Equal(SwitchIsland.PageTarget, wifi.CardTarget(new Vector2(x, 30), box, style, WidgetSurface.List));
                }
                Assert.Equal(SwitchIsland.PageTarget, wifi.CardTarget(new Vector2(toggle.X, 2), box, style, WidgetSurface.List));
                Assert.Equal(IslandView.Page, wifi.Click(SwitchIsland.PageTarget).View);
                Assert.Equal(0, radio.Acts);
                Assert.Equal(IslandView.None, wifi.Click(SwitchIsland.ToggleTarget).View);
                Assert.Equal(1, radio.Acts);
                // The switch lights itself, apart from the line.
                Assert.True(wifi.LightsItsRows);

                // A switch with no page stays one target that switches, as Windows' tile without a ">" does.
                var airplane = new SwitchIsland($"{folder.Key}/airplane", ListKind.AirplaneMode.New(), gfx, new Config()) { Container = folder };
                Assert.False(airplane.PageShown);
                Assert.Equal(0, airplane.CardTarget(new Vector2(40, 30), box, style, WidgetSurface.List));
                Assert.Equal(IslandView.None, airplane.Click(0).View);
                airplane.Dispose();

                var access = Assert.IsType<SwitchIsland>(folder.Tiles[LineOf(folder, "accessibility")].Island);
                Assert.True(access.PageShown);
                Assert.Equal(IslandView.Page, access.Click(0).View);
                // Cast and project switch nothing either: all of each is its page.
                foreach (var kind in new[] { "cast", "project" })
                {
                    var tile = Assert.IsType<SwitchIsland>(folder.Tiles[LineOf(folder, kind)].Island);
                    Assert.True(tile.PageShown);
                    Assert.Equal(0, tile.CardTarget(new Vector2(40, 30), box, style, WidgetSurface.List));
                    Assert.Equal(IslandView.Page, tile.Click(0).View);
                }

                Assert.True(folder.OpenPageOf(access.Key));
                var magnifier = Assert.IsType<SwitchIsland>(folder.Tiles[1].Island);
                Assert.False(magnifier.PageShown);

                // A switch on the bar, in no folder, has none.
                using var bare = new SwitchIsland("bar/wifi", ListKind.WifiSwitch.New(), gfx, new Config());
                Assert.False(bare.PageShown);
            }
            finally
            {
                QuickSwitches.Source = null;
            }
        });
    }

    /// <summary>
    /// In a real folder window: a click on the Wi-Fi line's ">" shows its page where the folder
    /// stands, and the folder stays open; Escape is the page's first, back to the lines, and then
    /// the folder's own; the foot's Edit has the dock arrange the folder, which ends a page.
    /// </summary>
    [UiFact]
    public void InTheOpenFolderTheChevronAndEditDoWhatTheySay()
    {
        WindowShots.OnHiddenDesktop("quick-pages-folder", (gfx, instance) =>
        {
            QuickSwitches.Source = which => new SampleSwitch(which.ToString(), on: true, switches: true);
            try
            {
                var holder = Presets.Folder(Presets.Find(Presets.QuickPanelId)!, Laptop);
                using var folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
                using var flyout = new FolderFlyout(gfx, instance, 0);
                var arranged = 0;
                flyout.ArrangeFromDrag = () => ++arranged > 0;
                flyout.Show(gfx, LookFor(true), 1f, new POINT { x = 1600, y = 1040 }, new RECT { left = 0, top = 0, right = 1920, bottom = 1040 }, Edge.Bottom, folder, FlyoutAlign.End);
                LPARAM At(RECT line, int x, int y)
                {
                    var point = new POINT { x = x < 0 ? line.right + x : line.left + x, y = line.top + y };
                    Assert.True(ScreenToClient(flyout.Hwnd, &point));
                    return (LPARAM)(nint)((point.y << 16) | (point.x & 0xFFFF));
                }
                FolderFlyoutEvent Click(LPARAM at)
                {
                    _ = flyout.Message(WM.WM_MOUSEMOVE, 0, at);
                    _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, at);
                    return flyout.Message(WM.WM_LBUTTONUP, 0, at);
                }
                var wifi = flyout.LineOnScreen(LineOf(folder, "wifiSwitch"));
                var middle = (wifi.bottom - wifi.top) / 2;
                // The ">", just before the switch's column on the trailing gutter.
                var style = FolderFlyout.StyleFor(1f, LookFor(true), FlyoutTheme.For(LookFor(true)));
                var sign = SwitchIsland.ChevronGlyphBox(PopupPaint.Rect(0, 0, wifi.right - wifi.left, wifi.bottom - wifi.top), style, switched: true);
                Assert.Equal(FolderFlyoutAction.None, Click(At(wifi, (int)((sign.left + sign.right) / 2), middle)).Kind);
                Assert.True(flyout.IsOpen);
                Assert.True(folder.Paged);
                Assert.IsType<QuickPageTile>(flyout.Folder!.Tiles[0].Island);

                Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_KEYDOWN, VK.VK_ESCAPE, 0).Kind);
                Assert.True(flyout.IsOpen, "Escape on a page closed the folder");
                Assert.False(folder.Paged);

                // The foot's Edit, on the line's left: the folder arranged where it stands.
                Assert.True(folder.OpenPageOf(folder.Tiles[LineOf(folder, "bluetoothSwitch")].Island.Key));
                Assert.True(folder.ClosePage());
                var foot = flyout.LineOnScreen(folder.Tiles.Count - 1);
                _ = Click(At(foot, 24, (foot.bottom - foot.top) / 2));
                Assert.Equal(1, arranged);
                Assert.True(flyout.Arranged);
                Assert.True(flyout.IsOpen);
            }
            finally
            {
                QuickSwitches.Source = null;
            }
        });
    }

    /// <summary>
    /// Back from a page, what lights is where the pointer is now (the person, 2026-10-29: "when clicking into
    /// a widget like the wifi one, when returning it is highlighted until i hover over it again"): the Wi-Fi
    /// line, clicked into, is not lit when the pointer is elsewhere as the page goes back — by its back arrow
    /// or with the pointer gone from the folder — and the line under the pointer is; by the keys, the keys are
    /// on the Wi-Fi line again, as Windows' own pages give it back.
    /// </summary>
    [UiFact]
    public void BackFromAPageLightsWhereThePointerIsAndTheKeysStayOnTheLine()
    {
        WindowShots.OnHiddenDesktop("quick-pages-back", (gfx, instance) =>
        {
            QuickSwitches.Source = which => new SampleSwitch(which.ToString(), on: true, switches: true);
            try
            {
                var holder = Presets.Folder(Presets.Find(Presets.QuickPanelId)!, Laptop);
                using var folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
                using var flyout = new FolderFlyout(gfx, instance, 0);
                flyout.Show(gfx, LookFor(true), 1f, new POINT { x = 1600, y = 1040 }, new RECT { left = 0, top = 0, right = 1920, bottom = 1040 }, Edge.Bottom, folder, FlyoutAlign.End);
                LPARAM Client(POINT screen)
                {
                    Assert.True(ScreenToClient(flyout.Hwnd, &screen));
                    return (LPARAM)(nint)((screen.y << 16) | (screen.x & 0xFFFF));
                }
                var wifiAt = LineOf(folder, "wifiSwitch");
                var wifi = Assert.IsType<SwitchIsland>(folder.Tiles[wifiAt].Island);
                var line = flyout.LineOnScreen(wifiAt);
                var style = FolderFlyout.StyleFor(1f, LookFor(true), FlyoutTheme.For(LookFor(true)));
                var sign = SwitchIsland.ChevronGlyphBox(PopupPaint.Rect(0, 0, line.right - line.left, line.bottom - line.top), style, switched: true);
                var chevron = Client(new POINT { x = line.left + (int)((sign.left + sign.right) / 2), y = (line.top + line.bottom) / 2 });
                void Open()
                {
                    _ = flyout.Message(WM.WM_MOUSEMOVE, 0, chevron);
                    Assert.True(wifi.Pointed, "the Wi-Fi line was not told the pointer is on it");
                    _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, chevron);
                    _ = flyout.Message(WM.WM_LBUTTONUP, 0, chevron);
                    Assert.True(folder.Paged);
                }

                // The pointer on another line of the folder's as the page goes back: that one is lit, never the Wi-Fi.
                var otherAt = LineOf(folder, "bluetoothSwitch");
                var other = flyout.LineOnScreen(otherAt);
                Open();
                var away = Client(new POINT { x = (other.left + other.right) / 2, y = (other.top + other.bottom) / 2 });
                _ = flyout.Message(WM.WM_MOUSEMOVE, 0, away);
                Assert.True(folder.ClosePage());
                Assert.False(wifi.Pointed, "back from its page, the Wi-Fi line is lit with the pointer elsewhere");
                Assert.Equal(otherAt, flyout.Lit.Hot);
                Assert.True(((CellIsland)folder.Tiles[otherAt].Island).Pointed, "the line under the pointer is not lit");

                // The pointer gone from the folder: nothing lit.
                Open();
                _ = flyout.Message(WM.WM_MOUSELEAVE, 0, 0);
                Assert.True(folder.ClosePage());
                Assert.False(wifi.Pointed, "back from its page, the Wi-Fi line is lit with the pointer gone");
                Assert.Equal(-1, flyout.Lit.Hot);

                // By the keys: on the Wi-Fi line, its page opened and Escape: the keys on the Wi-Fi line again, shown.
                for (var tab = 0; tab < folder.Tiles.Count && flyout.Lit.Focused != wifiAt; tab++)
                {
                    _ = flyout.Message(WM.WM_KEYDOWN, VK.VK_TAB, 0);
                }
                Assert.Equal(wifiAt, flyout.Lit.Focused);
                Assert.True(folder.OpenPageOf(wifi.Key));
                _ = flyout.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
                _ = flyout.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
                _ = flyout.Message(WM.WM_KEYDOWN, VK.VK_ESCAPE, 0);
                Assert.False(folder.Paged);
                Assert.Equal((wifiAt, true), (flyout.Lit.Focused, flyout.Lit.Keyboard));
                Assert.False(wifi.Pointed);
            }
            finally
            {
                QuickSwitches.Source = null;
            }
        });
    }

    /// <summary>
    /// A page's whole head is its way back (2026-10-30, the person: "for the wifi submenu or bluetooth submenu or also
    /// the folder view there is the back button, but the back button is only on the topleft, i want the whole header
    /// area to be the back button"): every point of the head row but the head's own controls — the Wi-Fi's switch and
    /// refresh, Bluetooth's switch — lights the way back under the pointer, the whole row in one plate, and a click
    /// there goes back; Narrator hears "Back, Wi-Fi". A page's own head (the accessibility and sound pages') is all
    /// the way back. Pictured hovered: page-head-wifi-&lt;dark|light&gt;-150.png.
    /// </summary>
    [UiFact]
    public void APagesWholeHeadIsTheWayBack()
    {
        Offscreen.OnStaThread(gfx =>
        {
            QuickSwitches.Source = which => new SampleSwitch(which.ToString(), on: true, switches: true);
            try
            {
                foreach (var dark in new[] { true, false })
                {
                    foreach (var (kind, controls) in new[] { ("wifiSwitch", 2), ("bluetoothSwitch", 1), ("cast", 0) })
                    {
                        var look = LookFor(dark);
                        var holder = Presets.Folder(Presets.Find(Presets.QuickPanelId)!, Laptop);
                        using var folder = new FolderIsland($"{Owner}/{holder.Id}-{kind}", holder, gfx, new Config());
                        folder.Opened(gfx);
                        Assert.True(folder.OpenPageOf(folder.Tiles[LineOf(folder, kind)].Island.Key));
                        _ = ((IIsland)folder).Refresh(gfx);
                        var page = Assert.IsType<QuickPageTile>(folder.Tiles[0].Island);
                        var plan = Plan(folder, look, 1.5f).Plan;
                        var card = plan.Cards[0];
                        var box = PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top);
                        var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
                        page.LayOut(box, style);
                        var head = page.ScrollHead(box.right - box.left, box.bottom - box.top, style);
                        var back = page.Placed.Single(widget => widget.Name == S.DockPageBack);
                        var title = page.Placed.OfType<Label>().First();
                        var own = page.Placed.Where(widget => widget != back && widget.HitTestVisible && widget.Bounds.bottom <= head).ToList();
                        Assert.Equal(controls, own.Count);
                        foreach (var y in new[] { 1f, MathF.Round(head / 2), head - 2 })
                        {
                            for (var x = 1f; x < box.right - 1; x += 4)
                            {
                                var at = new Vector2(x, y);
                                page.HoverOn(at, box, style, WidgetSurface.List);
                                var mine = own.FirstOrDefault(widget => widget.Contains(new Vector2(x - widget.Bounds.left, y - widget.Bounds.top)));
                                if (mine is null)
                                {
                                    Assert.True(back.Hot, $"{kind}: ({x}, {y}) of the head does not light the way back");
                                }
                                else
                                {
                                    Assert.True(mine.Hot && !back.Hot, $"{kind}: ({x}, {y}) is {mine.Name}'s, not the way back");
                                }
                            }
                        }

                        // Narrator: the way back and where it goes.
                        Assert.Equal($"{S.DockPageBack}, {title.Text}", PanelAccess.Describe(back)!.Name);

                        // The title's middle: lit, the picture; pressed and let go there, back.
                        var middle = new Vector2((title.Bounds.left + title.Bounds.right) / 2, head / 2);
                        page.HoverOn(middle, box, style, WidgetSurface.List);
                        if (kind == "wifiSwitch")
                        {
                            var file = $"page-head-wifi-{(dark ? "dark" : "light")}-150.png";
                            Sheet(gfx, file, 1.5f, [($"Wi-Fi, the pointer on the title: {(dark ? "dark" : "light")}", look, folder, plan)]);
                        }
                        Assert.True(page.PressOn(middle, box, style, WidgetSurface.List));
                        page.ReleaseOn(WidgetSurface.List);
                        _ = ((IIsland)folder).Refresh(gfx);
                        Assert.False(folder.Paged, $"{kind}: a click on the head's title did not go back");
                    }
                }

                // A page's own head: every point of it is the way back.
                {
                    var holder = Presets.Folder(Presets.Find(Presets.QuickPanelId)!, Laptop);
                    using var folder = new FolderIsland($"{Owner}/{holder.Id}-access", holder, gfx, new Config());
                    folder.Opened(gfx);
                    Assert.True(folder.OpenPageOf(folder.Tiles[LineOf(folder, "accessibility")].Island.Key));
                    var heading = Assert.IsType<PageHeadTile>(folder.Tiles[0].Island);
                    var style = FolderFlyout.StyleFor(1.5f, LookFor(true), FlyoutTheme.For(LookFor(true)));
                    var box = PopupPaint.Rect(0, 0, 540, heading.ListHeight(540, style));
                    for (var y = 1f; y < box.bottom - 1; y += 5)
                    {
                        for (var x = 1f; x < box.right - 1; x += 5)
                        {
                            Assert.Equal(PageHeadTile.BackTarget, heading.TargetOn(new Vector2(x, y), box, style, WidgetSurface.List));
                        }
                    }
                    var end = new Vector2(box.right - 8, box.bottom / 2);
                    heading.HoverOn(end, box, style, WidgetSurface.List);
                    Assert.True(heading.Lit, "the head's far end does not light the way back");
                    _ = heading.Click(heading.TargetOn(end, box, style, WidgetSurface.List));
                    _ = ((IIsland)folder).Refresh(gfx);
                    Assert.False(folder.Paged);
                }
            }
            finally
            {
                QuickSwitches.Source = null;
            }
        });
    }

    /// <summary>The volume's ">" opens the sound page; its own line on that page has none.</summary>
    [UiFact]
    public void TheVolumesChevronOpensTheSoundPage()
    {
        Offscreen.OnStaThread(gfx =>
        {
            QuickSwitches.Source = which => new SampleSwitch(which.ToString(), on: false);
            try
            {
                var holder = Presets.Folder(Presets.Find(Presets.QuickPanelId)!, Laptop);
                using var folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
                folder.Opened(gfx);
                var volume = Assert.IsType<SoundPartTile>(folder.Tiles[LineOf(folder, "soundVolume")].Island);
                Assert.True(volume.PageShown);
                var style = FolderFlyout.StyleFor(1.5f, LookFor(dark: false), FlyoutTheme.For(LookFor(dark: false)));
                var box = PopupPaint.Rect(0, 0, 540, 60);
                var sign = SwitchIsland.ChevronGlyphBox(box, style, switched: false);
                var target = volume.CardTarget(new Vector2((sign.left + sign.right) / 2, 30), box, style, WidgetSurface.List);
                Assert.True(target > 0, "the volume's \">\" is no target");
                Assert.Equal(IslandView.Page, volume.Click(target).View);
                Assert.True(folder.OpenPageOf(volume.Key));
                Assert.Equal(["heading", "soundOutput", "soundInput", "spatialSound", "divider", "soundMixer", "actions"], folder.Tiles.Select(tile => tile.Cell.Island.Kind));
            }
            finally
            {
                QuickSwitches.Source = null;
            }
        });
    }

    /// <summary>The footer's Edit asks the folder to be arranged; All settings opens Windows' Settings, through the test's opener.</summary>
    [UiFact]
    public void TheFootersEditArrangesAndTheGearOpensSettings()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var opened = new List<string>();
            ActionsTile.Opener = opened.Add;
            try
            {
                using var foot = new ActionsTile("test/foot", PremadeWidgets.QuickActions.New(), gfx, new Config());
                Assert.Equal(IslandView.Arrange, foot.Click(ActionsTile.LeftTarget).View);
                Assert.Empty(opened);
                Assert.Equal(IslandView.None, foot.Click(ActionsTile.RightTarget).View);
                Assert.Equal(["ms-settings:"], opened);
            }
            finally
            {
                ActionsTile.Opener = null;
            }
        });
    }

    /// <summary>
    /// The magnifier, the narrator and sticky keys read and switch through their tools: a click asks
    /// for the other state, a setting's broadcast redraws, and nothing else is asked.
    /// </summary>
    [Fact]
    public void TheAccessibilitySwitchesAskTheirTools()
    {
        var tools = new FakeTools();
        using var magnifier = new AssistiveSwitch(QuickSwitch.Magnifier, tools);
        using var narrator = new AssistiveSwitch(QuickSwitch.Narrator, tools);
        using var sticky = new AssistiveSwitch(QuickSwitch.StickyKeys, tools);
        var heard = 0;
        sticky.Start(() => heard++);
        narrator.Start(() => heard++);
        Assert.All(new[] { magnifier, narrator, sticky }, each => Assert.True(each.Switches));
        Assert.Equal((false, false, (bool?)false), (magnifier.On == true, narrator.On == true, sticky.On));

        magnifier.Act();
        Assert.Equal([true], tools.Magnified);
        Assert.True(magnifier.On);
        magnifier.Act();
        Assert.Equal([true, false], tools.Magnified);

        narrator.Act();
        Assert.Equal(1, tools.Narrated);

        sticky.Act();
        Assert.Equal([true], tools.Stuck);
        Assert.Equal(true, sticky.On);
        Assert.Equal(1, heard);
        sticky.Heard(WM.WM_SETTINGCHANGE, SPI.SPI_SETSTICKYKEYS);
        narrator.Heard(WM.WM_SETTINGCHANGE, SPI.SPI_SETSCREENREADER);
        sticky.Heard(WM.WM_SETTINGCHANGE, SPI.SPI_SETWORKAREA);
        Assert.Equal(3, heard);
        Assert.Throws<ArgumentOutOfRangeException>(() => new AssistiveSwitch(QuickSwitch.Vpn, tools));
        Assert.All(new[] { QuickSwitch.Magnifier, QuickSwitch.Narrator, QuickSwitch.StickyKeys }, which => Assert.IsType<AssistiveSwitch>(QuickSwitches.Windows(which)));
    }

    /// <summary>
    /// The network Wi-Fi is on stands beside its name, on the name's row, not under it (2026-10-16, the
    /// person: "show the name of the wifi or connected devices to the right of the title and not
    /// underneath"): after the name's end and before the switch, at the name's size; the line one row
    /// as before. Narrower, the network gives way first — cut where four letters still show, then
    /// gone — and the name stays whole. Taller, it is still beside the name, never a line under it.
    /// </summary>
    [UiFact]
    public void TheRadiosWordsStandBesideTheName()
    {
        Offscreen.OnStaThread(gfx =>
        {
            QuickSwitches.Source = which => new SampleSwitch(which.ToString(), on: true, switches: true);
            QuickPageTile.Sources = new QuickSources
            {
                Wifi = static () => new SampleWifi(),
                Bluetooth = static () => new SampleBluetooth(),
                Battery = static () => new SampleBattery(),
                Desktops = static () => new SampleDesktopNames(),
            };
            try
            {
                var holder = Presets.Folder(Presets.Find(Presets.QuickPanelId)!, Laptop);
                using var folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
                folder.Opened(gfx);
                var wifi = Assert.IsType<SwitchIsland>(folder.Tiles[LineOf(folder, "wifiSwitch")].Island);
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    var look = LookFor(dark: true);
                    var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
                    var form = ListForm.Of(gfx, style);
                    var row = Rows(1, scale, 1);
                    var line = wifi.LineOf(PopupPaint.Rect(0, 0, 360 * scale, row), style);
                    Assert.Equal(ListTier.One, line.Tier);
                    Assert.Equal("Rabbit Hole", line.SubText);
                    Assert.Empty(line.Under);
                    // Windows' caption size beside the body's, on the name's baseline.
                    Assert.Equal(style.Text(12), form.SmallPx);
                    Assert.Equal(form.SmallLine, TilePaint.Height(line.Sub));
                    OnTheNamesBaseline(gfx, form, line, Weight.Regular, $"{scale}x");
                    Assert.True(line.Sub.left >= line.Name.left + PopupPaint.Measure(gfx, line.NameText!, form.TextPx, Weight.Regular).X, $"{scale}x: the network is not after the name");
                    Assert.True(line.Sub.right <= SwitchIsland.WordsEnd(PopupPaint.Rect(0, 0, 360 * scale, row), style, switched: true), $"{scale}x: the network runs into the >");

                    // Narrower and narrower: the name whole while the network shows; the network whole,
                    // then cut with four letters showing, then gone, and once gone gone.
                    var (cut, gone) = (false, false);
                    for (var width = 360 * scale; width > 100 * scale; width -= 2)
                    {
                        var narrow = wifi.LineOf(PopupPaint.Rect(0, 0, width, row), style);
                        if (narrow.SubText is null)
                        {
                            gone = true;
                            continue;
                        }
                        Assert.False(gone, $"{scale}x, {width} px: the network came back narrower");
                        Assert.Equal(wifi.Info.Title, narrow.NameText);
                        Assert.True(narrow.Sub.right <= SwitchIsland.WordsEnd(PopupPaint.Rect(0, 0, width, row), style, switched: true), $"{scale}x, {width} px: the network runs into the >");
                        if (PopupPaint.Measure(gfx, narrow.SubText, form.SmallPx, Weight.Regular).X > TilePaint.Width(narrow.Sub))
                        {
                            cut = true;
                            Assert.True(TilePaint.Width(narrow.Sub) >= PopupPaint.Measure(gfx, "Rabb…", form.SmallPx, Weight.Regular).X - 0.5f, $"{scale}x, {width} px: cut past four letters");
                        }
                    }
                    Assert.True(cut && gone, $"{scale}x: the network never cut ({cut}) or never went ({gone})");

                    // Full (§432), the name's row still says it, and nothing under the name repeats it.
                    var settings = folder.Tiles[LineOf(folder, "wifiSwitch")].Cell.Island;
                    settings.Size = LineSize.Full;
                    var tall = wifi.LineOf(PopupPaint.Rect(0, 0, 360 * scale, Rows(3, scale, 1)), style);
                    settings.Size = LineSize.Compact;
                    Assert.NotEqual(ListTier.One, tall.Tier);
                    Assert.Equal("Rabbit Hole", tall.SubText);
                    OnTheNamesBaseline(gfx, form, tall, Weight.Semibold, $"{scale}x, taller");
                    Assert.DoesNotContain(tall.Under, under => under.Text == "Rabbit Hole");
                }
                folder.Closed();
            }
            finally
            {
                QuickPageTile.Sources = null;
                QuickSwitches.Source = null;
            }
        });
    }

    /// <summary>
    /// The Wi-Fi and Bluetooth pages are one fixed height, however many networks and devices there are
    /// (2026-10-16, the person: "the wifi and bluetooth flyouts that pop up should have a fixed height,
    /// same for accessibility"): one network or twelve, no device or four, the page is as tall; its rows
    /// scroll under the title where they are more than it holds.
    /// </summary>
    [UiFact]
    public void TheWifiAndBluetoothPagesAreOneFixedHeight()
    {
        Offscreen.OnStaThread(gfx =>
        {
            QuickSwitches.Source = which => new SampleSwitch(which.ToString(), on: true, switches: true);
            try
            {
                var look = LookFor(dark: true);
                var heights = new List<float>();
                QuickPageTile? scrolled = null;
                foreach (var (kind, sources) in new (string, QuickSources)[]
                {
                    ("wifiSwitch", Sources(new SampleWifi { Many = 1 }, new SampleBluetooth())),
                    ("wifiSwitch", Sources(new SampleWifi { Many = 12 }, new SampleBluetooth())),
                    ("bluetoothSwitch", Sources(new SampleWifi(), new SampleBluetooth(paired: false))),
                    ("bluetoothSwitch", Sources(new SampleWifi(), new SampleBluetooth())),
                })
                {
                    QuickPageTile.Sources = sources;
                    var holder = Presets.Folder(Presets.Find(Presets.QuickPanelId)!, Laptop);
                    using var folder = new FolderIsland($"{Owner}/{holder.Id}-{heights.Count}", holder, gfx, new Config());
                    folder.Opened(gfx);
                    Assert.True(folder.OpenPageOf(folder.Tiles[LineOf(folder, kind)].Island.Key));
                    _ = ((IIsland)folder).Refresh(gfx);
                    var (_, plan) = Plan(folder, look, 1.5f);
                    heights.Add(plan.Height);
                    if (heights.Count == 2)
                    {
                        // Twelve networks are more than the page holds: they scroll under its title.
                        var page = Assert.IsType<QuickPageTile>(folder.Tiles[0].Island);
                        var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
                        var laid = page.Scrolled(plan.Cards[0], style, WidgetSurface.List);
                        Assert.True(laid.Scrolls, "twelve networks do not scroll");
                        Assert.True(laid.Head > 0, "the title scrolls with the networks");
                        scrolled = page;
                    }
                    folder.Closed();
                }
                Assert.NotNull(scrolled);
                Assert.All(heights, height => Assert.Equal(heights[0], height, 0.5f));
            }
            finally
            {
                QuickPageTile.Sources = null;
                QuickSwitches.Source = null;
            }
        });

        static QuickSources Sources(SampleWifi wifi, SampleBluetooth bluetooth) => new()
        {
            Wifi = () => wifi,
            Bluetooth = () => bluetooth,
            Battery = static () => new SampleBattery(),
            Desktops = static () => new SampleDesktopNames(),
        };
    }

    /// <summary>
    /// The sun and the speaker stand in the switches' icon column, the size their glyphs are, and the
    /// volume's ">" is the switches' ">", the same size at the same place (2026-10-16, the person: "the
    /// brigtness and volume icons should be aligned to be the same as the night light icon" and "the
    /// angle icons between the toggles and the volume should also be the same size and aligned at the
    /// same position"). At every height a slider's line has, both scales and both text sizes.
    /// </summary>
    [UiFact]
    public void TheSlidersGlyphsAndTheChevronsLineUpWithTheSwitches()
    {
        Offscreen.OnStaThread(gfx =>
        {
            QuickSwitches.Source = which => new SampleSwitch(which.ToString(), on: true, switches: true);
            try
            {
                var holder = Presets.Folder(Presets.Find(Presets.QuickPanelId)!, Laptop);
                using var folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
                folder.Opened(gfx);
                var volume = Assert.IsType<SoundPartTile>(folder.Tiles[LineOf(folder, "soundVolume")].Island);
                foreach (var scale in new[] { 1f, 1.5f, 2f })
                {
                    foreach (var textScale in new[] { 1f, 1.5f })
                    {
                        var look = LookFor(dark: true, textScale);
                        var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
                        var form = ListForm.Of(gfx, style);
                        var width = MathF.Round(360 * scale);
                        var column = form.IconBox(0, 0, form.Line);
                        var iconCentre = (column.left + column.right) / 2;
                        var context = new QuickContext { Gfx = gfx, Theme = QuickTheme.For(style.Theme), Scale = scale, TextScale = textScale };
                        foreach (var rows in new[] { 1, 2, 3, 4 })
                        {
                            var at = $"{rows} row(s) at {scale:0.##}x, text {textScale:0.##}";
                            var line = SliderLine.Lay(gfx, context, style, width, Rows(rows, scale, textScale));
                            Assert.True(MathF.Abs(((line.Button.left + line.Button.right) / 2) - iconCentre) <= 1, $"{at}: the slider's glyph is not in the icon column");
                            Assert.Equal(form.Glyph, line.GlyphPx);
                        }

                        // The ">": centred where a line with no switch has it, the switches' size.
                        var box = PopupPaint.Rect(0, 0, width, Rows(1, scale, textScale));
                        volume.LayOut(box, style);
                        var chevron = Assert.Single(volume.Placed.OfType<Karpo.Dock.QuickSettings.Controls.Button>(), button => button.Glyph == SwitchIsland.ChevronGlyph);
                        var glyph = style.Px(SwitchIsland.ChevronGlyphDip);
                        var chevrons = SwitchIsland.ChevronGlyphBox(box, style, switched: false);
                        var switches = (chevrons.left + chevrons.right) / 2;
                        Assert.True(MathF.Abs(((chevron.Bounds.left + chevron.Bounds.right) / 2) - switches) <= 1, $"{scale}x, text {textScale}: the volume's > is not where a switchless line's is");
                        Assert.Equal(glyph, chevron.GlyphSize);
                    }
                }
            }
            finally
            {
                QuickSwitches.Source = null;
            }
        });
    }

    /// <summary>
    /// The switches stand in one column against the trailing gutter, page or none, and a line's ">"
    /// just before its switch (2026-10-16, the person: "the toggles should actually always align to
    /// the right. then the angled buttons are directly do the left of them") or, where it draws none,
    /// on the gutter itself (2026-10-16: "the angled icons should not have a blank space on the right
    /// of them if they dont have a toggle button to the right"); a line's words end before its ">".
    /// At every height, both scales and text sizes.
    /// </summary>
    [UiFact]
    public void TheSwitchesAndTheChevronsStandInColumns()
    {
        Offscreen.OnStaThread(gfx =>
        {
            QuickSwitches.Source = which => which == QuickSwitch.Accessibility
                ? new SampleSwitch(WidgetInfos.For(which).Title, on: null)
                : new SampleSwitch(which.ToString(), on: true, switches: true);
            QuickPageTile.Sources = new QuickSources
            {
                Wifi = static () => new SampleWifi(),
                Bluetooth = static () => new SampleBluetooth(),
                Battery = static () => new SampleBattery(),
                Desktops = static () => new SampleDesktopNames(),
            };
            try
            {
                var holder = Presets.Folder(Presets.Find(Presets.QuickPanelId)!, Laptop);
                using var folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
                folder.Opened(gfx);
                // The folder's own, and the lines with a switch and no page the person added beside them.
                var switches = folder.Tiles.Select(tile => tile.Island).OfType<SwitchIsland>().ToList();
                using var airplane = new SwitchIsland($"{folder.Key}/airplane", ListKind.AirplaneMode.New(), gfx, new Config()) { Container = folder };
                using var night = new SwitchIsland($"{folder.Key}/night", ListKind.NightLight.New(), gfx, new Config()) { Container = folder };
                switches.AddRange([airplane, night]);
                Assert.Contains(switches, line => line.PageShown && TilePaint.Width(line.LineOf(PopupPaint.Rect(0, 0, 360, 40), FolderFlyout.StyleFor(1f, LookFor(dark: true), FlyoutTheme.For(LookFor(dark: true)))).Switch) <= 0);
                Assert.Contains(switches, line => !line.PageShown);
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    foreach (var textScale in new[] { 1f, 1.5f })
                    {
                        var look = LookFor(dark: true, textScale);
                        var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
                        var width = MathF.Round(360 * scale);
                        var right = width - ListForm.Gutter(style);
                        foreach (var rows in new[] { 1, 2, 3 })
                        {
                            var box = PopupPaint.Rect(0, 0, width, Rows(rows, scale, textScale));
                            var column = SwitchIsland.SwitchColumn(box, style);
                            Assert.Equal(right, column.right, 0.5f);
                            Assert.Equal(column.left - style.Px(SwitchIsland.ChevronToSwitchDip), SwitchIsland.ChevronGlyphBox(box, style, switched: true).right, 0.5f);
                            Assert.Equal(right, SwitchIsland.ChevronGlyphBox(box, style, switched: false).right, 0.5f);
                            foreach (var island in switches)
                            {
                                var at = $"{island.Info.Id}, {rows} row(s) at {scale:0.##}x, text {textScale:0.##}";
                                var line = island.LineOf(box, style);
                                var switched = TilePaint.Width(line.Switch) > 0;
                                Assert.Equal(island.DrawsSwitch, switched);
                                if (switched)
                                {
                                    Assert.True(MathF.Abs(line.Switch.right - right) <= 0.5f && MathF.Abs(line.Switch.left - column.left) <= 0.5f, $"{at}: the switch is not in the switches' column");
                                }
                                if (!island.PageShown)
                                {
                                    continue;
                                }
                                // Its ">" before its switch, or on the gutter where it has none: no empty column after it.
                                var sign = SwitchIsland.ChevronGlyphBox(box, style, switched);
                                var end = SwitchIsland.WordsEnd(box, style, switched);
                                Assert.True(line.Name.right <= end + 0.5f, $"{at}: the name runs into the >");
                                Assert.True(line.SubText is null || line.Sub.right <= end + 0.5f, $"{at}: the radio's words run into the >");
                                Assert.True(line.StateText.Length == 0 || line.State.right <= end + 0.5f, $"{at}: the state runs into the >");
                                Assert.True(TilePaint.Width(line.Switch) <= 0 || line.Switch.left - style.Px(4) > sign.right, $"{at}: the switch's target takes the >");
                            }
                        }
                    }
                }
                folder.Closed();
            }
            finally
            {
                QuickPageTile.Sources = null;
                QuickSwitches.Source = null;
            }
        });
    }

    /// <summary>The words beside the name stand on its baseline, to the pixel, inside its line.</summary>
    private static void OnTheNamesBaseline(Gfx gfx, ListForm form, SwitchIsland.SwitchLine line, Weight nameWeight, string at)
    {
        var name = line.Name.top + CountdownCard.Baseline(gfx, form.TextPx, nameWeight);
        var beside = line.Sub.top + CountdownCard.Baseline(gfx, form.SmallPx, Weight.Regular);
        Assert.True(MathF.Abs(name - beside) <= 0.5f, $"{at}: the words beside stand at {beside}, the name at {name}");
        Assert.True(line.Sub.top > line.Name.top, $"{at}: the smaller words stand at the name's top, not on its baseline");
        Assert.True(line.Sub.bottom <= line.Name.bottom + 0.5f, $"{at}: the words beside reach past the name's line");
    }

    /// <summary>
    /// quick-pages-&lt;dark|light&gt;-&lt;100|150&gt;.png: the default quick settings and each of its
    /// pages beside it, the radios on, on the sample networks, devices and sound, a battery at 76 %.
    /// </summary>
    [UiFact]
    public void QuickPagesPictures()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var was = (BatteryIsland.Probe, BatteryIsland.Made);
            BatteryIsland.Probe = () => new SystemStatus.Battery(76, false, false);
            BatteryIsland.Made = () => new SampleBattery();
            QuickSwitches.Source = which => which switch
            {
                QuickSwitch.WifiRadio or QuickSwitch.BluetoothRadio or QuickSwitch.NightLight or QuickSwitch.ColorFilters => new SampleSwitch(which.ToString(), on: true, switches: true),
                QuickSwitch.Accessibility or QuickSwitch.LiveCaptions or QuickSwitch.Cast or QuickSwitch.Project => new SampleSwitch(WidgetInfos.For(which).Title, on: null),
                _ => new SampleSwitch(which.ToString(), on: false, switches: true),
            };
            try
            {
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    foreach (var dark in new[] { true, false })
                    {
                        var look = LookFor(dark);
                        var folders = new List<FolderIsland>();
                        var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                        try
                        {
                            // A folder each, the main page and each page open in its own.
                            foreach (var (kind, name) in new[] { ("", "Quick settings"), ("lines", "As switch lines"), ("wifiSwitch", "Wi-Fi"), ("bluetoothSwitch", "Bluetooth"), ("soundVolume", "Sound"), ("accessibility", "Accessibility"), ("cast", "Cast"), ("project", "Project") })
                            {
                                var holder = Presets.Folder(Presets.Find(Presets.QuickPanelId)!, Laptop);
                                if (kind == "lines")
                                {
                                    // The person's own (2026-10-16): airplane mode, energy saver and night light
                                    // as lines of their own in place of the toggles row, no battery.
                                    var list = holder.Folder!.Lists[0];
                                    var at = list.FindIndex(cell => cell.Island.Kind == "toggles");
                                    list[at] = new FolderCell { Island = ListKind.AirplaneMode.New() };
                                    list.Insert(at + 1, new FolderCell { Island = ListKind.EnergySaver.New() });
                                    list.Insert(at + 2, new FolderCell { Island = ListKind.NightLight.New() });
                                    list.RemoveAll(cell => cell.Island.Kind is "battery" or "divider");
                                }
                                var folder = new FolderIsland($"{Owner}/{holder.Id}-{name}", holder, gfx, new Config());
                                folders.Add(folder);
                                _ = ((IIsland)folder).Refresh(gfx);
                                folder.Opened(gfx);
                                if (folder.Tiles.FirstOrDefault(tile => tile.Cell.Island.Kind == kind) is { } line)
                                {
                                    Assert.True(folder.OpenPageOf(line.Island.Key));
                                    _ = ((IIsland)folder).Refresh(gfx);
                                }
                                var (_, plan) = Plan(folder, look, scale);
                                panels.Add(($"{name}: {(dark ? "dark" : "light")}, {plan.Width / scale:0} × {plan.Height / scale:0} DIP", look, folder, plan));
                            }
                            Sheet(gfx, $"quick-pages-{(dark ? "dark" : "light")}-{scale * 100:0}.png", scale, panels);
                        }
                        finally
                        {
                            foreach (var folder in folders)
                            {
                                folder.Closed();
                                folder.Dispose();
                            }
                        }
                    }
                }
            }
            finally
            {
                QuickSwitches.Source = null;
                (BatteryIsland.Probe, BatteryIsland.Made) = was;
            }
        });
    }

    /// <summary>
    /// The sound page is as tall as its mixer's apps (2026-10-16, the person: "the quick settings volume
    /// bar flyout should show the volume mixer the full height, it should grow with its contents up to a
    /// generous max height"; §200): none, three, twelve or forty apps on a 1080p screen at 100 and 150 %,
    /// every app shown where the page stays within 80 % of the work area; past that the page is that
    /// tall, the mixer gives way and scrolls its apps, and the title, the devices and the foot stay as
    /// they are. It was four rows, whatever played.
    /// </summary>
    [UiFact]
    public void TheSoundPageGrowsWithItsMixerUpToItsMost()
    {
        Offscreen.OnStaThread(gfx =>
        {
            QuickSwitches.Source = which => new SampleSwitch(which.ToString(), on: false);
            try
            {
                var look = LookFor(dark: true);
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    var (work, room) = Screen1080(scale);
                    var most = MathF.Floor(work * LineHeight.GivingMost);
                    var lists = new List<float>();
                    foreach (var apps in new[] { 0, 3, 12, 40 })
                    {
                        using var folder = SoundPage(gfx, apps);
                        var (natural, free) = Plan(folder, look, scale);
                        var plan = Laid(folder, look, scale, natural, work, room);
                        var mixer = LineOf(folder, "soundMixer");
                        var list = plan.Lists[0];
                        var tall = list.Box.bottom - list.Box.top;
                        var freeTall = free.Lists[0].Box.bottom - free.Lists[0].Box.top;
                        var what = $"{apps} apps at {scale * 100:0} %";
                        Assert.True(list.Hidden.Count == 0, $"{what}: lines left out");
                        // Every line but the mixer is the height it is with all the room in the world.
                        for (var index = 0; index < folder.Tiles.Count; index++)
                        {
                            if (index != mixer)
                            {
                                Assert.Equal(Given(free, index), Given(plan, index), 0.5f);
                            }
                        }
                        var tile = Assert.IsType<SoundPartTile>(folder.Tiles[mixer].Island);
                        var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
                        var scrolls = tile.Scrolled(plan.Cards[mixer], style, WidgetSurface.List).Scrolls;
                        if (freeTall <= most)
                        {
                            // Every app: the mixer is its content in whole cells (§442), the page no taller than it.
                            Assert.Equal(FolderFlyout.CellPx(scale, 1) * LineHeight.CellsFor(natural[mixer], FolderFlyout.CellPx(scale, 1)), Given(plan, mixer), 0.5f);
                            Assert.Equal(freeTall, tall, 0.5f);
                            Assert.False(scrolls, $"{what}: scrolls with every app shown");
                        }
                        else
                        {
                            Assert.InRange(tall, most - 1, most + 0.5f);
                            Assert.True(scrolls, $"{what}: the apps the page has no room for do not scroll");
                        }
                        lists.Add(tall);
                    }
                    // None, three, twelve: taller with every app, as long as there is room.
                    Assert.True(lists[0] < lists[1] && lists[1] <= lists[2] && lists[2] <= lists[3], string.Join(", ", lists));
                    Assert.True(lists[3] > lists[1] + 0.5f, "forty apps take no more room than three");
                }
            }
            finally
            {
                QuickSwitches.Source = null;
            }
        });
    }

    /// <summary>
    /// sound-page-&lt;dark|light&gt;-150.png: the sound page with none, three, eight, twelve and forty
    /// apps in its mixer, on a 1080p screen's room (the first four whole, the last at its most, scrolling).
    /// </summary>
    [UiFact]
    public void SoundPagePictures()
    {
        Offscreen.OnStaThread(gfx =>
        {
            QuickSwitches.Source = which => new SampleSwitch(which.ToString(), on: false);
            try
            {
                const float scale = 1.5f;
                var (work, room) = Screen1080(scale);
                foreach (var dark in new[] { true, false })
                {
                    var look = LookFor(dark);
                    var folders = new List<FolderIsland>();
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    try
                    {
                        foreach (var apps in new[] { 0, 3, 8, 12, 40 })
                        {
                            var folder = SoundPage(gfx, apps);
                            folders.Add(folder);
                            var (natural, _) = Plan(folder, look, scale);
                            var plan = Laid(folder, look, scale, natural, work, room);
                            panels.Add(($"{apps} apps, {plan.Width / scale:0} × {plan.Height / scale:0} DIP", look, folder, plan));
                        }
                        Sheet(gfx, $"sound-page-{(dark ? "dark" : "light")}-150.png", scale, panels);
                    }
                    finally
                    {
                        foreach (var folder in folders)
                        {
                            folder.Closed();
                            folder.Dispose();
                        }
                    }
                }
            }
            finally
            {
                QuickSwitches.Source = null;
            }
        });
    }

    /// <summary>A 1080p screen's work area over a 48-DIP bar at <paramref name="scale"/>, and the room beside the bar.</summary>
    private static (float Work, float Room) Screen1080(float scale)
    {
        var work = 1080 - PopupPaint.Round(48 * scale);
        return (work, FlyoutPlacement.Room(work, scale));
    }

    /// <summary>The quick settings folder, open, its sound page open on a mixer of <paramref name="apps"/> apps.</summary>
    private static FolderIsland SoundPage(Gfx gfx, int apps)
    {
        var holder = Presets.Folder(Presets.Find(Presets.QuickPanelId)!, Laptop);
        var folder = new FolderIsland($"{Owner}/{holder.Id}-sound-{apps}", holder, gfx, new Config());
        folder.Opened(gfx);
        SoundParts.Made = () => new SampleSound { Apps = apps };
        try
        {
            Assert.True(folder.OpenPageOf(folder.Tiles[LineOf(folder, "soundVolume")].Island.Key));
        }
        finally
        {
            SoundParts.Made = null;
        }
        _ = ((IIsland)folder).Refresh(gfx);
        return folder;
    }

    /// <summary><paramref name="folder"/> laid out on a work area <paramref name="work"/> px tall with <paramref name="room"/> px beside the bar.</summary>
    private static FolderPlan Laid(FolderIsland folder, Look look, float scale, float[] natural, float work, float room) =>
        FolderFlyout.Build(look, scale, folder.View, folder.Laid, [.. folder.Tiles.Select(tile => tile.Cell)], work, natural: natural, room: room);

    private sealed class CountingSwitch : IQuickSwitch
    {
        public string Name => "Made up";

        public bool Available => true;

        public bool? On { get; set; }

        public string? Note => null;

        public bool Switches => true;

        public int Acts { get; private set; }

        public void Start(Action changed)
        {
        }

        public void Act() => Acts++;

        public void Dispose()
        {
        }
    }

    internal sealed class FakeTools : IAssistiveTools
    {
        public List<bool> Magnified { get; } = [];

        public List<bool> Stuck { get; } = [];

        public int Narrated { get; private set; }

        public bool Magnifying { get; private set; }

        public bool? Reading => Narrated % 2 == 1;

        public bool? Sticky { get; private set; } = false;

        public void Magnify(bool on)
        {
            Magnified.Add(on);
            Magnifying = on;
        }

        public void ToggleNarrator() => Narrated++;

        public bool Stick(bool on)
        {
            Stuck.Add(on);
            Sticky = on;
            return true;
        }
    }
}
