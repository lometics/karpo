// Cast and nearby sharing are presets (docs\widgets.md §114, §115). The person, 2026-10-11:
// "sharing widget just opens the sharing settings, is it possible to actually make a proper flyout
// out of this?", "the cast icon opens the windows cast flyout ... why dont we implement it so that it
// looks and behaves the same everywhere?" and "all the remaining flyouts should become folders with a
// face now". So:
//
//   * Cast: Windows' Cast panel as a line (the wireless displays in range, the one cast to first; a
//     row opens Windows' panel, which alone may connect), Win+P's modes as a line, and the cast
//     actions, under the cast face (a dot while this PC is cast to a display).
//   * Nearby sharing: Windows' nearby sharing as a line (off, my devices, everyone nearby; where
//     received files go, with Change; the name this PC is found by) and its settings, under the
//     share face, dimmed while it is off. Its switch line is a real switch now.
//
// Made-up sources throughout (SampleCast, SampleProjection, SampleNearby through
// QuickPageTile.Sources): no screen, display, registry value or dialog is touched.
//
// Pictured: cast-preset-<dark|light>.png, nearby-preset-<dark|light>.png (150 %, and 225 % text),
// cast-nearby-faces-<dark|light>.png.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Core;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Folders.ListFormPictures;

namespace Karpo.Tests.Folders;

public sealed unsafe class CastNearbyPresetTests
{
    private static readonly PresetMachine Machine = new(HasBattery: false, FixedDrives: []);

    private static BarItem Holder(string id) => Presets.Folder(Presets.Find(id)!, Machine);

    /// <summary>
    /// The quick sources with made-up casts, screens and nearby sharing: each asked for in turn (the
    /// face's first, as the folder is made, then the line's, as it opens), the last again once they
    /// run out.
    /// </summary>
    private static QuickSources Sources(SampleCast[]? casts = null, SampleNearby[]? nearby = null, SampleProjection? projection = null)
    {
        var nextCast = 0;
        var nextNearby = 0;
        casts ??= [new SampleCast()];
        nearby ??= [new SampleNearby()];
        projection ??= new SampleProjection();
        return new QuickSources
        {
            Wifi = static () => new SampleWifi(),
            Bluetooth = static () => new SampleBluetooth(),
            Battery = static () => new SampleBattery(),
            Desktops = static () => new SampleDesktopNames(),
            Project = () => projection,
            Cast = _ => casts[Math.Min(nextCast++, casts.Length - 1)],
            Nearby = () => nearby[Math.Min(nextNearby++, nearby.Length - 1)],
        };
    }

    private static FolderIsland Made(Gfx gfx, string preset, bool open, QuickSources sources)
    {
        QuickPageTile.Sources = sources;
        var holder = Holder(preset);
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

    private static QuickPageTile Line(FolderIsland folder, QuickKind kind) =>
        folder.Tiles.Select(each => each.Island).OfType<QuickPageTile>().Single(tile => tile.Page == kind);

    private static (D2D_RECT_F Box, IReadOnlyList<Widget> Placed) Laid(QuickPageTile tile, TileStyle style)
    {
        tile.Pump();
        var width = FolderFlyout.ListWidth(1.5f);
        var box = PopupPaint.Rect(0, 0, width, tile.ListHeight(width, style));
        tile.LayOut(box, style);
        return (box, tile.Placed);
    }

    private static string Said(QuickPageTile tile, TileStyle style) =>
        string.Join("\n", Laid(tile, style).Placed.SelectMany(widget => widget switch
        {
            Label label => [label.Text],
            ListRow row => new[] { row.Title, row.Detail ?? "" },
            Button button => [button.Text],
            _ => [],
        }));

    private static void Click(QuickPageTile tile, TileStyle style, string name)
    {
        var (box, placed) = Laid(tile, style);
        var widget = placed.FirstOrDefault(each => each.Name == name) ?? throw new Xunit.Sdk.XunitException($"no {name} on the line");
        var at = widget.Bounds;
        if (tile.CardPress(new Vector2((at.left + at.right) / 2, (at.top + at.bottom) / 2), box, style, WidgetSurface.List))
        {
            tile.CardRelease();
        }
        tile.Pump();
    }

    private static ListRow Row(QuickPageTile tile, TileStyle style, string name) =>
        Assert.IsType<ListRow>(Laid(tile, style).Placed.Single(widget => widget.Name == name));

    // --- The presets -----------------------------------------------------------------------------------

    /// <summary>
    /// Cast: Windows' Cast panel as a line, Win+P's modes, and in the same card Display and Add
    /// device (its "Find other devices"), under the cast face, which has nothing to set.
    /// </summary>
    [Fact]
    public void TheCastPresetIsThePanelTheModesAndTheirFooter()
    {
        var preset = Presets.Find(Presets.CastId)!;
        Assert.Equal(FolderFaces.CastId, preset.Face!.Kind);
        Assert.Equal(["wirelessDisplays", "projection", "actions"], preset.Items.Select(item => item.Form));
        Assert.Same(PremadeWidgets.CastActions, preset.Items[^1].Premade);
        Assert.Equal(("display", SettingsActions.AddDeviceId), PremadeWidgets.CastActions.Actions);

        var folder = Holder(Presets.CastId).Folder!;
        Assert.Same(FolderFaces.Cast, folder.Face.Info);
        Assert.Empty(folder.Face.Info.Options);
        Assert.Equal([ListKind.WirelessDisplays, ListKind.Projection, ListKind.Actions], folder.Cells.Select(cell => cell!.Island!.List));
    }

    /// <summary>Nearby sharing: its line and, in the same card, its settings alone on the right, under the share face.</summary>
    [Fact]
    public void TheNearbyPresetIsTheLineAndItsSettings()
    {
        var preset = Presets.Find(Presets.NearbyId)!;
        Assert.Equal(FolderFaces.NearbyId, preset.Face!.Kind);
        Assert.Equal(["nearbySharing", "actions"], preset.Items.Select(item => item.Form));
        Assert.Equal(((string?)null, SettingsActions.NearbySharingId), PremadeWidgets.NearbyActions.Actions);
        Assert.Equal("ms-settings:crossdevice", SettingsActions.Find(SettingsActions.NearbySharingId)!.Uri);

        var folder = Holder(Presets.NearbyId).Folder!;
        Assert.Same(FolderFaces.Nearby, folder.Face.Info);
        Assert.Equal([ListKind.NearbySharing, ListKind.Actions], folder.Cells.Select(cell => cell!.Island!.List));
    }

    /// <summary>
    /// Both buttons are gone from the bar; their lines are lines of a folder (pages, never a bar's
    /// widget); nearby sharing's switch stays the line it was, in Connectivity.
    /// </summary>
    [Fact]
    public void TheButtonsAreGoneAndTheSwitchStaysALine()
    {
        Assert.Null(BarEntries.Find("cast"));
        Assert.Null(BarEntries.Find("nearbyShare"));
        // A quick settings tile again since 5c4e84df: the line whose whole is the wireless displays' page.
        Assert.NotNull(WidgetInfos.Find("cast"));
        foreach (var id in new[] { "wirelessDisplays", "projection", "nearbySharing" })
        {
            Assert.Equal(WidgetSource.QuickPage, ListWidgets.Find(id)!.Drawing);
            Assert.Null(BarEntries.Find(id));
        }
        Assert.Equal(WidgetSource.Switch, ListWidgets.Find("nearbyShare")!.Drawing);
        Assert.Contains(Presets.Find("connectivityGroup")!.Items, item => item.Form == "nearbyShare");
    }

    // --- Nearby sharing's values ---------------------------------------------------------------------

    /// <summary>
    /// The policy Windows keeps: 0 off, 1 my devices only, 2 everyone nearby, anything else unknown;
    /// turning it on brings back the mode last on, my devices only where none is remembered; received
    /// files go where the value says, or to Downloads.
    /// </summary>
    [Fact]
    public void WindowsValuesReadAsTheModes()
    {
        Assert.Equal(NearbyMode.Off, NearbyModes.FromPolicy(0));
        Assert.Equal(NearbyMode.MyDevices, NearbyModes.FromPolicy(1));
        Assert.Equal(NearbyMode.Everyone, NearbyModes.FromPolicy(2));
        Assert.Null(NearbyModes.FromPolicy(3));
        Assert.Null(NearbyModes.FromPolicy(null));
        Assert.Null(NearbyModes.FromPolicy("2"));
        foreach (var mode in NearbyModes.All)
        {
            Assert.Equal(mode, NearbyModes.FromPolicy(NearbyModes.Policy(mode)));
        }
        Assert.Equal(NearbyMode.Everyone, NearbyModes.Remembered(2));
        Assert.Equal(NearbyMode.MyDevices, NearbyModes.Remembered(1));
        Assert.Equal(NearbyMode.MyDevices, NearbyModes.Remembered(0));
        Assert.Equal(NearbyMode.MyDevices, NearbyModes.Remembered(null));
        Assert.Equal(@"C:\Downloads", NearbyModes.SaveTo(null, @"C:\Downloads"));
        Assert.Equal(@"C:\Downloads", NearbyModes.SaveTo("  ", @"C:\Downloads"));
        Assert.Equal(@"D:\Shared", NearbyModes.SaveTo(@" D:\Shared ", @"C:\Downloads"));
        Assert.Equal(@"D:\Shared", NearbyModes.SaveTo(@"D:\Shared", @"C:\Downloads"));
    }

    /// <summary>
    /// What a change takes: off goes through the tile (unless it is off already) and leaves 0; on from
    /// off remembers the mode, then the tile turns it on; from one mode to the other the tile is left
    /// alone and the policy written.
    /// </summary>
    [Fact]
    public void AChangeGoesThroughTheTileWhereWindowsWould()
    {
        Assert.Equal(((int?)null, (bool?)false, 0), NearbyModes.Plan(NearbyMode.Everyone, NearbyMode.Off));
        Assert.Equal(((int?)null, (bool?)null, 0), NearbyModes.Plan(NearbyMode.Off, NearbyMode.Off));
        Assert.Equal(((int?)2, (bool?)true, 2), NearbyModes.Plan(NearbyMode.Off, NearbyMode.Everyone));
        Assert.Equal(((int?)1, (bool?)true, 1), NearbyModes.Plan(null, NearbyMode.MyDevices));
        Assert.Equal(((int?)1, (bool?)null, 1), NearbyModes.Plan(NearbyMode.Everyone, NearbyMode.MyDevices));
    }

    // --- The cast line ---------------------------------------------------------------------------------

    /// <summary>
    /// The displays in range, the one cast to first and marked; a row opens Windows' Cast panel, as
    /// "Open Cast" does; nothing found says so; while looking, a ring. No footer on the line.
    /// </summary>
    [UiFact]
    public void TheCastLineListsTheDisplaysAndOpensWindowsPanel() => Offscreen.OnStaThread(gfx =>
    {
        var line = new SampleCast();
        var folder = Made(gfx, Presets.CastId, open: true, Sources(casts: [new SampleCast(), line]));
        try
        {
            var style = Style(LookFor(dark: true));
            var tile = Line(folder, QuickKind.Cast);
            var said = Said(tile, style);
            Assert.Contains(S.DockQuickCastTitle, said);
            Assert.Contains("Living Room TV", said);
            Assert.Contains(S.DockQuickConnected, said);
            Assert.Contains(S.DockQuickCastHow, said);
            var rows = Laid(tile, style).Placed.OfType<ListRow>().ToList();
            Assert.Equal("Living Room TV", rows[0].Title);
            Assert.True(rows[0].Selected);
            Assert.False(rows[1].Selected);
            Click(tile, style, "Fire TV Stick");
            Assert.Equal(1, line.Opened);
            Click(tile, style, "Open Cast");
            Assert.Equal(2, line.Opened);
        }
        finally
        {
            Done(folder);
        }

        folder = Made(gfx, Presets.CastId, open: true, Sources(casts: [new SampleCast([], looking: false)]));
        try
        {
            var said = Said(Line(folder, QuickKind.Cast), Style(LookFor(dark: true)));
            Assert.Contains(S.DockQuickCastNone, said);
        }
        finally
        {
            Done(folder);
        }
    });

    /// <summary>Win+P's modes as a line: the one in force marked, a click asks for another and marks it at once.</summary>
    [UiFact]
    public void TheProjectionLineMarksAndChoosesAMode() => Offscreen.OnStaThread(gfx =>
    {
        var screens = new SampleProjection(ProjectMode.Extend);
        var folder = Made(gfx, Presets.CastId, open: true, Sources(projection: screens));
        try
        {
            var style = Style(LookFor(dark: true));
            var tile = Line(folder, QuickKind.Projection);
            Assert.True(Row(tile, style, S.DockQuickProjectExtend).Selected);
            Click(tile, style, S.DockQuickProjectDuplicate);
            Assert.Equal([ProjectMode.Duplicate], screens.Applied);
            Assert.True(Row(tile, style, S.DockQuickProjectDuplicate).Selected);
            Assert.False(Row(tile, style, S.DockQuickProjectExtend).Selected);
        }
        finally
        {
            Done(folder);
        }
    });

    // --- The nearby sharing line -----------------------------------------------------------------------

    /// <summary>
    /// The three modes, the one in force marked; a click sets another; where received files go and
    /// Change, which asks Windows' folder dialog; the name it is found by.
    /// </summary>
    [UiFact]
    public void TheNearbyLineSetsTheModeAndAsksWhereFilesGo() => Offscreen.OnStaThread(gfx =>
    {
        var face = new SampleNearby();
        var line = new SampleNearby();
        var folder = Made(gfx, Presets.NearbyId, open: true, Sources(nearby: [face, line]));
        try
        {
            var style = Style(LookFor(dark: true));
            var tile = Line(folder, QuickKind.Nearby);
            var said = Said(tile, style);
            Assert.Contains(S.DockQuickNearbyTitle, said);
            Assert.Contains(S.DockQuickNearbySaveTo, said);
            Assert.Contains(line.Folder, said);
            Assert.Contains(line.Device, said);
            Assert.True(Row(tile, style, S.DockQuickNearbyMyDevices).Selected);

            Click(tile, style, S.DockQuickNearbyEveryone);
            Assert.Equal([NearbyMode.Everyone], line.Asked);
            Assert.True(Row(tile, style, S.DockQuickNearbyEveryone).Selected);
            Assert.False(Row(tile, style, S.DockQuickNearbyMyDevices).Selected);
            // A click on the mode in force asks for nothing.
            Click(tile, style, S.DockQuickNearbyEveryone);
            Assert.Single(line.Asked);

            Click(tile, style, S.DockQuickNearbyChange);
            Assert.Equal(1, line.Choosing);
            Assert.Empty(face.Asked);
        }
        finally
        {
            Done(folder);
        }
    });

    /// <summary>
    /// The switch line is a real switch now: off goes off, on brings back the mode last on, and while
    /// it is on it says who may share; before Windows has said, it shows no state and is no switch.
    /// </summary>
    [Fact]
    public void TheSwitchTurnsItOffAndOnAsItWasLastOn()
    {
        var sharing = new SampleNearby(NearbyMode.Everyone);
        using var nearby = new NearbySwitch("Nearby sharing", () => sharing); // not-ui: a test's name
        Assert.Null(nearby.On);
        Assert.False(nearby.Switches);
        nearby.Start(() => { });
        Assert.True(nearby.On);
        Assert.True(nearby.Switches);
        Assert.Equal(S.DockQuickNearbyEveryone, nearby.Detail);
        nearby.Act();
        Assert.False(nearby.On);
        Assert.Null(nearby.Detail);
        nearby.Act();
        Assert.Equal([NearbyMode.Off, NearbyMode.Everyone], sharing.Asked);
        Assert.True(nearby.On);

        using var unread = new NearbySwitch("Nearby sharing", () => new SampleNearby(mode: null)); // not-ui: a test's name
        unread.Start(() => { });
        Assert.Null(unread.On);
        Assert.False(unread.Switches);
        Assert.Equal(S.DockQuickSwitchNearbyNote, unread.Note);
    }

    // --- The faces -------------------------------------------------------------------------------------

    /// <summary>The cast face: a dot only once a display is read to be cast to; its hover names it.</summary>
    [Fact]
    public void TheCastFaceDotsWhileCastAndNamesTheDisplay()
    {
        var casting = new SampleCast().Read();
        var none = new SampleCast([new("tv", "Living Room TV", false)]).Read(); // not-ui: a sample display
        Assert.True(CastFaceDrawing.Casting(casting));
        Assert.False(CastFaceDrawing.Casting(none));
        Assert.False(CastFaceDrawing.Casting(casting with { Known = false }));
        Assert.False(CastFaceDrawing.Casting(null));
        Assert.Null(CastFaceDrawing.Tip(null));
        Assert.Equal(S.DockQuickCastTitle, CastFaceDrawing.Tip(casting with { Known = false }));
        Assert.Equal($"{S.DockQuickCastTitle}\n{S.DockQuickCastCastingTo("Living Room TV")}", CastFaceDrawing.Tip(casting));
        Assert.Equal($"{S.DockQuickCastTitle}\n{S.DockQuickCastNotCasting}", CastFaceDrawing.Tip(none));
    }

    /// <summary>The nearby face: dimmed while off or unread; its hover says whether it is on and who may share.</summary>
    [Fact]
    public void TheNearbyFaceIsDimWhileOff()
    {
        Assert.True(NearbyFaceDrawing.Dim(null));
        Assert.True(NearbyFaceDrawing.Dim(new SampleNearby(mode: null).Read()));
        Assert.True(NearbyFaceDrawing.Dim(new SampleNearby(NearbyMode.Off).Read()));
        Assert.False(NearbyFaceDrawing.Dim(new SampleNearby(NearbyMode.MyDevices).Read()));
        Assert.False(NearbyFaceDrawing.Dim(new SampleNearby(NearbyMode.Everyone).Read()));
        Assert.Null(NearbyFaceDrawing.Tip(null));
        Assert.Equal(S.DockQuickNearbyTitle, NearbyFaceDrawing.Tip(new SampleNearby(mode: null).Read()));
        Assert.Equal(S.WidgetSwitchOffTip(S.DockQuickNearbyTitle), NearbyFaceDrawing.Tip(new SampleNearby(NearbyMode.Off).Read()));
        Assert.Equal($"{S.WidgetSwitchOnTip(S.DockQuickNearbyTitle)}\n{S.DockQuickNearbyEveryone}", NearbyFaceDrawing.Tip(new SampleNearby(NearbyMode.Everyone).Read()));
    }

    /// <summary>
    /// The faces on the bar: one square whatever they show; the cast face reads which display is
    /// cast to on its own tick (no radio scan: its source is the one that does not look); the nearby
    /// face follows its source's news, painted again in place, and has no tick.
    /// </summary>
    [UiFact]
    public void TheFacesFollowTheirSourcesInPlace() => Offscreen.OnStaThread(gfx =>
    {
        var config = new Config { Height = 48 };
        var style = new IslandStyle(1f, false, Layout.CellPx(96, config), Layout.IconPx(96, config), LookFor(dark: true));

        var asked = new List<bool>();
        var castFace = new SampleCast([new("tv", "Living Room TV", false)]); // not-ui: a sample display
        var sources = Sources();
        QuickPageTile.Sources = new QuickSources
        {
            Wifi = sources.Wifi, Bluetooth = sources.Bluetooth, Battery = sources.Battery, Desktops = sources.Desktops,
            Cast = discover =>
            {
                asked.Add(discover);
                return castFace;
            },
        };
        var holder = Holder(Presets.CastId);
        var cast = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
        try
        {
            Assert.True(cast.ShowsCast);
            Assert.Equal([false], asked);
            Assert.Equal(10_000, cast.IntervalMs);
            var size = cast.CellSize(0, style);
            Assert.Equal(FaceWidthTests.GlyphButton(style), size.Width, 0.5f);
            Assert.Equal($"{S.DockQuickCastTitle}\n{S.DockQuickCastNotCasting}", cast.Tooltip(0));
            _ = cast.Tick(gfx);
            Assert.Equal(1, castFace.Polled);
        }
        finally
        {
            Done(cast);
        }

        var sharing = new SampleNearby(NearbyMode.Off);
        var nearby = Made(gfx, Presets.NearbyId, open: false, Sources(nearby: [sharing]));
        try
        {
            Assert.True(nearby.ShowsNearby);
            Assert.Equal(0, nearby.IntervalMs);
            var size = nearby.CellSize(0, style);
            _ = nearby.Measure(style);
            _ = nearby.Surface(0, style);
            Assert.StartsWith("True|", nearby.FaceShown, StringComparison.Ordinal);
            sharing.Set(NearbyMode.Everyone);
            Assert.False(((IIsland)nearby).Refresh(gfx));
            Assert.StartsWith("False|", nearby.FaceShown, StringComparison.Ordinal);
            Assert.Equal($"{S.WidgetSwitchOnTip(S.DockQuickNearbyTitle)}\n{S.DockQuickNearbyEveryone}", nearby.Tooltip(0));
            Assert.Equal(size, nearby.CellSize(0, style));
        }
        finally
        {
            Done(nearby);
        }
    });

    // --- Pictures --------------------------------------------------------------------------------------

    /// <summary>
    /// The cast preset: a display cast to, still looking, none found, and at 225 % text; the nearby
    /// sharing preset: my devices only, everyone nearby with a long folder, off, and at 225 % text.
    /// </summary>
    [UiFact]
    public void PresetPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var folders = new List<FolderIsland>();
                try
                {
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    void Add(string caption, string preset, QuickSources sources, float text = 1f)
                    {
                        var look = LookFor(dark, text);
                        var folder = Made(gfx, preset, open: true, sources);
                        folders.Add(folder);
                        foreach (var tile in folder.Tiles.Select(each => each.Island).OfType<QuickPageTile>())
                        {
                            Laid(tile, Style(look));
                        }
                        panels.Add((caption, look, folder, Plan(folder, look, 1.5f).Plan));
                    }

                    Add("Cast: casting to the TV", Presets.CastId, Sources());
                    Add("Cast: looking", Presets.CastId, Sources(casts: [new SampleCast([], looking: true)], projection: new SampleProjection(ProjectMode.PcScreenOnly, 1)));
                    Add("Cast: none found", Presets.CastId, Sources(casts: [new SampleCast([], looking: false)]));
                    Add("Cast: 225 % text", Presets.CastId, Sources(), 2.25f);
                    Sheet(gfx, $"cast-preset-{(dark ? "dark" : "light")}.png", 1.5f, panels);

                    panels.Clear();
                    Add("Nearby: my devices only", Presets.NearbyId, Sources());
                    Add("Nearby: everyone, a long folder", Presets.NearbyId, Sources(nearby: [new SampleNearby(NearbyMode.Everyone, @"D:\Shared with everyone nearby\Received from phones and tablets")]));
                    Add("Nearby: off", Presets.NearbyId, Sources(nearby: [new SampleNearby(NearbyMode.Off)]));
                    Add("Nearby: 225 % text", Presets.NearbyId, Sources(), 2.25f);
                    Sheet(gfx, $"nearby-preset-{(dark ? "dark" : "light")}.png", 1.5f, panels);
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

    /// <summary>The faces on bars of 40 and 48 DIP and at 150 %: cast idle and casting, nearby off and on.</summary>
    [UiFact]
    public void FacePictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                var islands = new List<FolderIsland>
                {
                    Made(gfx, Presets.CastId, false, Sources(casts: [new SampleCast([new("tv", "Living Room TV", false)])])), // not-ui: a sample display
                    Made(gfx, Presets.CastId, false, Sources()),
                    Made(gfx, Presets.NearbyId, false, Sources(nearby: [new SampleNearby(NearbyMode.Off)])),
                    Made(gfx, Presets.NearbyId, false, Sources(nearby: [new SampleNearby(NearbyMode.Everyone)])),
                };
                var styles = new[] { (40, 96u), (48, 96u), (48, 144u) }
                    .Select(pair => new IslandStyle(pair.Item2 / 96f, false, Layout.CellPx(pair.Item2, new Config { Height = pair.Item1 }), Layout.IconPx(pair.Item2, new Config { Height = pair.Item1 }), look))
                    .ToList();
                try
                {
                    const float gap = 12;
                    var width = gap + styles.Sum(style => (islands.Count * (islands[0].CellSize(0, style).Width + gap)) + gap);
                    var height = gap + styles.Max(style => islands[0].CellSize(0, style).Height) + gap;
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
                    }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"cast-nearby-faces-{(dark ? "dark" : "light")}.png")));
                }
                finally
                {
                    foreach (var island in islands)
                    {
                        island.Dispose();
                    }
                    QuickPageTile.Sources = null;
                }
            });
        }
    }
}
