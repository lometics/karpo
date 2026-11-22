// The mobile hotspot is a preset (docs\widgets.md §93): what a click on the mobile hotspot button
// gave — its flyout, the switch, the network's name, password and band, the devices on it — is a
// folder holding the hotspot's page as a line (QuickPageTile, as the network's and Bluetooth's,
// §88) and the premade hotspot actions under it, under the hotspot face: the button's glyph, dimmed
// while it is off, with how many devices are on it, painted again in place as that changes. The
// switch alone stays a line of its own. Made-up hotspots throughout (SampleHotspot, through
// QuickPageTile.Sources): nothing is shared, switched or copied for real.
//
// Pictured beside the flyout it replaces (rendered before it went: quick-hotspot-*.png):
// hotspot-preset-<dark|light>.png at 150 % and 225 % text, and the face on the bar,
// hotspot-face-<dark|light>.png.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Folders.ListFormPictures;

namespace Karpo.Tests.Folders;

public sealed unsafe class HotspotPresetTests
{
    private static readonly PresetMachine Machine = new(HasBattery: false, FixedDrives: []);

    /// <summary>The mobile hotspot's preset folder, as the add list makes it.</summary>
    private static BarItem Holder() => Presets.Folder(Presets.Find(Presets.HotspotId)!, Machine);

    /// <summary>
    /// The quick sources with made-up hotspots: each asked for in turn from <paramref name="hotspots"/>
    /// (the face's first, as the folder is made, then the page's, as it opens), the last one again
    /// once they run out.
    /// </summary>
    private static QuickSources Sources(params SampleHotspot[] hotspots)
    {
        var next = 0;
        return new QuickSources
        {
            Wifi = static () => new SampleWifi(),
            Bluetooth = static () => new SampleBluetooth(),
            Battery = static () => new SampleBattery(),
            Desktops = static () => new SampleDesktopNames(),
            Hotspot = () => hotspots[Math.Min(next++, hotspots.Length - 1)],
        };
    }

    /// <summary>The preset's folder, its face and its line reading <paramref name="hotspots"/>; opened where asked.</summary>
    private static FolderIsland Made(Gfx gfx, bool open, params SampleHotspot[] hotspots)
    {
        QuickPageTile.Sources = Sources(hotspots);
        var holder = Holder();
        var folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
        if (open)
        {
            folder.Opened(gfx);
        }
        return folder;
    }

    private static void Done(FolderIsland folder)
    {
        folder.Closed();
        folder.Dispose();
        QuickPageTile.Sources = null;
        QuickPageTile.Opener = null;
    }

    private static TileStyle Style(Look look, float scale = 1.5f) => FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));

    /// <summary>The page's line laid out at the list's width: its controls, in the card's own px.</summary>
    private static (QuickPageTile Tile, D2D_RECT_F Box, IReadOnlyList<Widget> Placed) Laid(FolderIsland folder, TileStyle style)
    {
        var tile = folder.Tiles.Select(each => each.Island).OfType<QuickPageTile>().Single();
        tile.Pump();
        var width = FolderFlyout.ListWidth(1.5f);
        var box = PopupPaint.Rect(0, 0, width, tile.ListHeight(width, style));
        tile.LayOut(box, style);
        return (tile, box, tile.Placed);
    }

    /// <summary>Every word on the line: its labels, and its rows' titles and details.</summary>
    private static string Said(FolderIsland folder, TileStyle style) =>
        string.Join("\n", Laid(folder, style).Placed.SelectMany(widget => widget switch
        {
            Label label => [label.Text],
            ListRow row => new[] { row.Title, row.Detail ?? "" },
            _ => [],
        }));

    /// <summary>A press and a release in the middle of the line's control named <paramref name="name"/>.</summary>
    private static void Click(FolderIsland folder, TileStyle style, string name)
    {
        var (tile, box, placed) = Laid(folder, style);
        var widget = placed.FirstOrDefault(each => each.Name == name) ?? throw new Xunit.Sdk.XunitException($"no {name} on the line");
        var at = widget.Bounds;
        if (tile.CardPress(new Vector2((at.left + at.right) / 2, (at.top + at.bottom) / 2), box, style, WidgetSurface.List))
        {
            tile.CardRelease();
        }
        tile.Pump();
    }

    // --- The preset ------------------------------------------------------------------------------------

    /// <summary>
    /// The flyout's page as a line, then in the same card its footer as the premade hotspot actions —
    /// "Mobile hotspot" for the flyout's "Mobile hotspot settings", and "Network &amp; internet" — under
    /// the hotspot face, which has no name and nothing to set.
    /// </summary>
    [Fact]
    public void ThePresetIsThePageAndItsFooterUnderTheHotspotFace()
    {
        var preset = Presets.Find(Presets.HotspotId)!;
        Assert.Equal(FolderFaces.HotspotId, preset.Face!.Kind);
        Assert.Equal(["hotspot", "actions"], preset.Items.Select(item => item.Form));
        Assert.Same(PremadeWidgets.HotspotActions, preset.Items[^1].Premade);
        Assert.Equal(("mobileHotspot", "network"), PremadeWidgets.HotspotActions.Actions);

        var folder = Holder().Folder!;
        Assert.Same(FolderFaces.Hotspot, folder.Face.Info);
        Assert.Empty(folder.Face.Info.Options);
        Assert.Equal([ListKind.Hotspot, ListKind.Actions], folder.Cells.Select(cell => cell!.Island!.List));
        Assert.Equal(("mobileHotspot", "network"), (folder.Cells[^1]!.Island!.Actions!.Left, folder.Cells[^1]!.Island!.Actions!.Right));
    }

    /// <summary>
    /// The button is gone from the bar; the page is a line of its own, and the switch alone stays the
    /// line it was, in Connectivity and in a toggles row.
    /// </summary>
    [Fact]
    public void TheButtonIsGoneAndTheSwitchStaysALine()
    {
        Assert.Null(BarEntries.Find("mobileHotspot"));
        Assert.Equal(WidgetSource.QuickPage, ListWidgets.Find("hotspot")!.Drawing);
        Assert.Equal(WidgetSource.Switch, ListWidgets.Find("mobileHotspot")!.Drawing);
        Assert.Equal(QuickSwitch.MobileHotspot, WidgetInfos.MobileHotspot.Switch);
        Assert.Contains(Presets.Find("connectivityGroup")!.Items, item => item.Form == "mobileHotspot");
        Assert.NotNull(Toggles.Find(Toggle.MobileHotspot));
    }

    // --- The line --------------------------------------------------------------------------------------

    /// <summary>
    /// The folder opened: the page, as the flyout showed it — the name, the password hidden until the
    /// eye shows it, the band, two of eight devices and the devices themselves; Copy hands the sample
    /// the password (never the clipboard); the switch switches it, the devices going with it; and
    /// opened again, the password is hidden again. The footer is the actions line, not the page's.
    /// </summary>
    [UiFact]
    public void TheLineShowsCopiesAndSwitches() => Offscreen.OnStaThread(gfx =>
    {
        var face = new SampleHotspot();
        var page = new SampleHotspot();
        var folder = Made(gfx, open: true, face, page);
        try
        {
            var style = Style(LookFor(dark: true));
            var tile = folder.Tiles.Select(each => each.Island).OfType<QuickPageTile>().Single();
            Assert.Equal(QuickKind.Hotspot, tile.Page);
            var said = Said(folder, style);
            Assert.Contains(page.Name, said);
            Assert.Contains(HotspotText.Hidden(page.Password), said);
            Assert.DoesNotContain(page.Password, said);
            Assert.Contains(S.DockQuickHotspotBand5, said);
            Assert.Contains(S.DockQuickHotspotDevicesOf(2, 8), said);
            Assert.Contains("Pixel-8", said);
            // A device that gave no name is called by its address.
            Assert.Contains("3c:22:fb:8a:10:9d", said);
            // No footer on the line: the flyout's link is the actions line under it.
            Assert.DoesNotContain(Laid(folder, style).Placed, widget => widget.Name == WindowsPages.MobileHotspot.Title);

            Click(folder, style, S.DockQuickHotspotShowPassword);
            Assert.Contains(page.Password, Said(folder, style));
            Assert.Contains(Laid(folder, style).Placed, widget => widget.Name == S.DockQuickHotspotHidePassword);
            Click(folder, style, S.DockQuickHotspotCopyPassword);
            Assert.Equal([page.Password], page.Copied);

            Click(folder, style, S.DockQuickHotspotName);
            Assert.Equal([false], page.Switched);
            Assert.Empty(face.Switched);
            Assert.DoesNotContain("Pixel-8", Said(folder, style));

            folder.Closed();
            folder.Opened(gfx);
            Assert.Contains(HotspotText.Hidden(page.Password), Said(folder, style));
        }
        finally
        {
            Done(folder);
        }
    });

    /// <summary>Where this PC cannot host one there is no switch, and the line says why.</summary>
    [UiFact]
    public void WhereItCannotBeTheLineSaysWhy() => Offscreen.OnStaThread(gfx =>
    {
        var folder = Made(gfx, open: true, new SampleHotspot(HotspotCan.Policy));
        try
        {
            var style = Style(LookFor(dark: true));
            Assert.DoesNotContain(Laid(folder, style).Placed, widget => widget.Name == S.DockQuickHotspotName && widget is ToggleSwitch);
            var said = Said(folder, style);
            Assert.Contains(S.DockQuickHotspotPolicy, said);
            Assert.DoesNotContain(S.DockQuickHotspotNetworkName, said);
        }
        finally
        {
            Done(folder);
        }
    });

    /// <summary>The footer opens Windows' Mobile hotspot page and Network &amp; internet.</summary>
    [UiFact]
    public void TheFooterOpensTheHotspotsSettingsAndTheNetworks() => Offscreen.OnStaThread(gfx =>
    {
        var folder = Made(gfx, open: true, new SampleHotspot());
        var opened = new List<string>();
        ActionsTile.Opener = opened.Add;
        try
        {
            var actions = folder.Tiles.Select(tile => tile.Island).OfType<ActionsTile>().Single();
            actions.Open(ActionsTile.LeftTarget);
            actions.Open(ActionsTile.RightTarget);
            Assert.Equal(["ms-settings:network-mobilehotspot", "ms-settings:network-status"], opened);
        }
        finally
        {
            ActionsTile.Opener = null;
            Done(folder);
        }
    });

    // --- The face --------------------------------------------------------------------------------------

    /// <summary>
    /// What the face draws: the glyph dimmed unless the hotspot is on; while it is on, the devices on
    /// it from one, "9+" past nine; nothing on its corner while it is off, whatever Windows counted.
    /// </summary>
    [Fact]
    public void TheFaceIsDimWhileOffAndCountsTheDevicesWhileOn()
    {
        Assert.Equal((true, (string?)null), HotspotFaceDrawing.Look(null));
        Assert.Equal((true, (string?)null), HotspotFaceDrawing.Look(HotspotState.Unknown));
        Assert.Equal((true, (string?)null), HotspotFaceDrawing.Look(new SampleHotspot(on: false).Read()));
        // No adapter: undimmed, the folder greys the whole of it (§247).
        Assert.Equal((false, (string?)null), HotspotFaceDrawing.Look(new SampleHotspot(HotspotCan.NoAdapter).Read()));
        Assert.Equal((false, (string?)null), HotspotFaceDrawing.Look(new SampleHotspot(devices: []).Read()));
        Assert.Equal((false, "2"), HotspotFaceDrawing.Look(new SampleHotspot().Read()));
        Assert.Null(HotspotFaceDrawing.Badge(0));
        Assert.Equal("1", HotspotFaceDrawing.Badge(1));
        Assert.Equal("9", HotspotFaceDrawing.Badge(9));
        Assert.Equal("9+", HotspotFaceDrawing.Badge(10));
    }

    /// <summary>The hover: on and how many of how many are on it, off, why it cannot be, or its name while Windows has not said.</summary>
    [Fact]
    public void TheHoverSaysWhetherItIsOnAndWhoIsOnIt()
    {
        var name = S.DockQuickHotspotName;
        Assert.Null(HotspotFaceDrawing.Tip(null));
        Assert.Equal(name, HotspotFaceDrawing.Tip(HotspotState.Unknown));
        Assert.Equal($"{S.WidgetSwitchOnTip(name)}\n{S.WidgetHotspotDevicesOf(2, 8)}", HotspotFaceDrawing.Tip(new SampleHotspot().Read()));
        Assert.Equal(S.WidgetSwitchOffTip(name), HotspotFaceDrawing.Tip(new SampleHotspot(on: false).Read()));
        Assert.Equal($"{S.DockQuickHotspotCant}\n{S.DockQuickHotspotNoConnection}", HotspotFaceDrawing.Tip(new SampleHotspot(HotspotCan.NoConnection).Read()));
        var unbounded = new SampleHotspot().Read() with { MaxClients = 0 };
        Assert.Equal($"{S.WidgetSwitchOnTip(name)}\n{S.WidgetHotspotDevicesCount(2)}", HotspotFaceDrawing.Tip(unbounded));
    }

    /// <summary>
    /// The hotspot switched or a device joining paints the face again in place — the dim, the count,
    /// the hover — never a rebuild, and it stays one size, its count's room kept (§276); a tick with
    /// nothing new paints nothing.
    /// Its own source's news (Windows saying so) reaches it, and so does its tick, which asks again;
    /// news of the line in the open folder makes the face ask again too, and its own news does not.
    /// </summary>
    [UiFact]
    public void TheFacePaintsAgainInPlaceAsTheHotspotChanges() => Offscreen.OnStaThread(gfx =>
    {
        var face = new SampleHotspot(devices: [new("Pixel-8", "a4:c3:f0:12:7e:01")]); // not-ui: a sample device
        var page = new SampleHotspot();
        var island = Made(gfx, open: false, face, page);
        try
        {
            Assert.True(island.ShowsHotspot);
            Assert.Equal(30_000, island.IntervalMs);
            var config = new Config { Height = 48 };
            var look = LookFor(dark: true);
            var style = new IslandStyle(1f, false, Layout.CellPx(96, config), Layout.IconPx(96, config), look);
            var size = island.CellSize(0, style);
            _ = island.Measure(style);
            _ = island.Surface(0, style);
            Assert.StartsWith("False|1|", island.FaceShown, StringComparison.Ordinal);
            var painted = 0;
            var glyphs = new List<DXGI_RGBA>();
            var texts = new List<DXGI_RGBA>();
            PaintWatch.Watching = (_, what, color) =>
            {
                painted++;
                if (what == Painted.Glyph)
                {
                    glyphs.Add(color);
                }
                else if (what == Painted.Text)
                {
                    texts.Add(color);
                }
                return color;
            };
            try
            {
                // Nothing changed: nothing to paint.
                Assert.False(island.FaceTick(gfx));
                Assert.Equal(0, painted);

                // Switched off in Windows, which says so: dimmed, no count, in place, the count's room kept.
                face.Switch(false);
                Assert.False(((IIsland)island).Refresh(gfx));
                Assert.True(painted > 0, "the hotspot switched off was not painted");
                Assert.StartsWith("True||", island.FaceShown, StringComparison.Ordinal);
                Assert.Empty(texts);
                Assert.Equal(Rgba(new IslandInk(look).Disabled), Rgba(Assert.Single(glyphs)));
                Assert.Equal(S.WidgetSwitchOffTip(S.DockQuickHotspotName), island.Tooltip(0));
                Assert.Equal(size, island.CellSize(0, style));
                // Its own news: the face does not ask again for it.
                Assert.Equal(0, face.Polls);

                // On again with two on it, which only its tick finds (no event for a device joining).
                (painted, glyphs, texts) = (0, [], []);
                face.Devices.Add(new("", "3c:22:fb:8a:10:9d")); // not-ui: a sample device
                face.Switch(true);
                Assert.False(island.Tick(gfx));
                Assert.Equal(1, face.Polls);
                Assert.True(painted > 0, "the hotspot switched on was not painted");
                Assert.StartsWith("False|2|", island.FaceShown, StringComparison.Ordinal);
                Assert.Equal(Rgba(new IslandInk(look).SystemIcon), Rgba(Assert.Single(glyphs)));
                Assert.Single(texts);
                Assert.Equal($"{S.WidgetSwitchOnTip(S.DockQuickHotspotName)}\n{S.WidgetHotspotDevicesOf(2, 8)}", island.Tooltip(0));
                Assert.Equal(size, island.CellSize(0, style));

                // Open, a line's news (the page switched it, or asked who is on it): the face asks too.
                island.Opened(gfx);
                var asked = face.Polls;
                Assert.False(((IIsland)island).Refresh(gfx));
                Assert.Equal(asked + 1, face.Polls);
                // Asking switches nothing.
                Assert.Equal([false, true], face.Switched);
                Assert.Empty(page.Switched);
            }
            finally
            {
                PaintWatch.Watching = null;
            }
        }
        finally
        {
            Done(island);
        }
    });

    /// <summary>
    /// The face is its glyph, its widest count's room across a bar and the button's ends whatever the
    /// hotspot says (§276), at every bar, across it and down it.
    /// </summary>
    [UiFact]
    public void TheFaceIsOneSizeWhateverItSays() => Offscreen.OnStaThread(gfx =>
    {
        SampleHotspot[] states =
        [
            new(on: false),
            new(devices: []),
            new(),
            new(devices: Enumerable.Range(0, 12).Select(index => new HotspotClient($"Phone {index}", $"00:00:00:00:00:{index:x2}"))),
            new(HotspotCan.NoAdapter),
        ];
        foreach (var height in new[] { 28, 48, 72, 115 })
        {
            var config = new Config { Height = height };
            foreach (var vertical in new[] { false, true })
            {
                foreach (var text in new[] { 1f, 2.25f })
                {
                    var style = new IslandStyle(1.5f, vertical, Layout.CellPx(144, config), Layout.IconPx(144, config), LookFor(dark: true, text));
                    var where = $"{height} DIP, text ×{text}{(vertical ? ", vertical" : "")}";
                    foreach (var state in states)
                    {
                        var island = Made(gfx, open: false, state);
                        try
                        {
                            var (width, tall) = island.CellSize(0, style);
                            var trail = vertical ? 0 : RemovableFaceDrawing.CountTrail(gfx, style, "9+");
                            Assert.True(trail > 0 || vertical, $"{where}: no room for the count");
                            Assert.Equal(FaceWidthTests.GlyphButton(style, trail), vertical ? tall : width, 0.5f);
                        }
                        finally
                        {
                            Done(island);
                        }
                    }
                }
            }
        }
    });

    /// <summary>
    /// The face's right click: Windows' hotspot icon's way to its settings, besides the folder's own
    /// "Settings"; the line's in the folder is its page in Windows.
    /// </summary>
    [Fact]
    public void TheFacesRowOpensTheHotspotsSettings()
    {
        var (items, rows) = Karpo.Dock.Dock.BarMenuRows(Holder());
        Assert.Equal([WindowsPages.MobileHotspot.Title], items.Select(item => item.Text));
        Assert.Equal(WidgetAct.HotspotSettings, Assert.Single(rows).Act);
        Assert.Equal("ms-settings:network-mobilehotspot", Karpo.Dock.Dock.TargetOf(WidgetAct.HotspotSettings));
        Assert.True(Karpo.Dock.Dock.HandsAway(WidgetAct.HotspotSettings));
        Assert.Equal([WidgetAct.WindowsSettings], Karpo.Dock.Dock.ActsFor(ListKind.Hotspot));
        Assert.Equal("ms-settings:network-mobilehotspot", Karpo.Dock.Dock.WindowsLinkOf(WidgetInfos.Hotspot)!.Value.Uri);
    }

    // --- Pictures --------------------------------------------------------------------------------------

    /// <summary>
    /// The preset as it opens, its password shown, off, on a PC whose adapter cannot host one, and at
    /// 225 % text — at 150 %, to stand beside quick-hotspot-*.png of the flyout.
    /// </summary>
    [UiFact]
    public void HotspotPresetPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var folders = new List<FolderIsland>();
                try
                {
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    FolderIsland Add(string caption, SampleHotspot hotspot, float text = 1f, string? press = null)
                    {
                        var look = LookFor(dark, text);
                        var folder = Made(gfx, open: true, hotspot);
                        folders.Add(folder);
                        if (press is not null)
                        {
                            Click(folder, Style(look), press);
                        }
                        Laid(folder, Style(look));
                        panels.Add((caption, look, folder, Plan(folder, look, 1.5f).Plan));
                        return folder;
                    }

                    Add("On: two devices", new SampleHotspot());
                    Add("The password shown", new SampleHotspot(), press: S.DockQuickHotspotShowPassword);
                    Add("Off", new SampleHotspot(on: false, band: HotspotBand.Any));
                    Add("Can't: no adapter", new SampleHotspot(HotspotCan.NoAdapter));
                    Add("On: 225 % text", new SampleHotspot(), 2.25f);
                    Sheet(gfx, $"hotspot-preset-{(dark ? "dark" : "light")}.png", 1.5f, panels);
                }
                finally
                {
                    foreach (var folder in folders)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                    QuickPageTile.Sources = null;
                }
            });
        }
    }

    /// <summary>
    /// The face on a bar of 40 and 48 DIP and at 150 %: off, on with nobody on it, with two and with
    /// twelve, and on a PC that cannot host one — hotspot-face-&lt;dark|light&gt;.png.
    /// </summary>
    [UiFact]
    public void HotspotFacePictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                SampleHotspot[] states =
                [
                    new(on: false),
                    new(devices: []),
                    new(),
                    new(devices: Enumerable.Range(0, 12).Select(index => new HotspotClient($"Phone {index}", $"00:00:00:00:00:{index:x2}"))),
                    new(HotspotCan.NoAdapter),
                ];
                var styles = new[] { (40, 96u), (48, 96u), (48, 144u) }
                    .Select(pair => new IslandStyle(pair.Item2 / 96f, false, Layout.CellPx(pair.Item2, new Config { Height = pair.Item1 }), Layout.IconPx(pair.Item2, new Config { Height = pair.Item1 }), look))
                    .ToList();
                var islands = states.Select(state => Made(gfx, open: false, state)).ToList();
                try
                {
                    const float gap = 12;
                    var width = gap + styles.Sum(style => (islands.Count * (island0(style).Width + gap)) + gap);
                    var height = gap + styles.Max(style => island0(style).Height) + gap;
                    Offscreen.Bitmap(width, height, dc =>
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
                        var x = gap;
                        foreach (var style in styles)
                        {
                            foreach (var island in islands)
                            {
                                var (w, h) = island.CellSize(0, style);
                                PopupPaint.Fill(dc, PopupPaint.Rect(x, gap, w, h), 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                                Offscreen.Shifted(dc, x, gap, () => island.PaintCell(dc, 0, style, w, h));
                                x += w + gap;
                            }
                            x += gap;
                        }
                    }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"hotspot-face-{(dark ? "dark" : "light")}.png")));
                }
                finally
                {
                    foreach (var island in islands)
                    {
                        Done(island);
                    }
                }

                (float Width, float Height) island0(IslandStyle style) => islands[0].CellSize(0, style);
            });
        }
    }

    private static (float R, float G, float B, float A) Rgba(DXGI_RGBA color) => (color.r, color.g, color.b, color.a);
}
