// §474: Windows' own widgets wear Windows' faces (Karpo.Core\WindowsFaces.cs): the Widgets button the weather, quick
// settings' button the network, the volume and the battery, the clock the time over the date and the bell. Held here:
//
//   * each is drawn as a folder's faces are, from the same readings — stand-ins here, never this PC's —, and is no folder:
//     it holds nothing, its click opens nothing of Karpo's and always does something (Windows' menu), and a screen
//     reader hears Windows' button's name, then what its faces say;
//   * a click on one is Windows' menu's button, as the glyph buttons' are, and nothing else on the bar is: a folder of
//     Karpo's never (Dock.WindowsButtonOf), so hover, which asks the same, never opens Windows' own;
//   * its menu at rest is its faces' rows, Windows' own icons' (the network's, the volume's, the clock's), and it has
//     no face nor contents to set in edit mode;
//   * §485: quick settings' button shows the battery's percentage only where Windows' own "Battery percentage" is on
//     (IsBatteryPercentageEnabled, a stand-in here: never this PC's, never written); Karpo's own battery face always.
//
// The toggle (a second click closes) is WindowsSearchTests'; the Windows preset's shape LayoutPresetsTests'.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Folders;
using Karpo.Tests.Forecasts;
using static Karpo.Tests.Sizing.OneRowTests;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Islands;

public sealed unsafe class WindowsFacesTests
{
    private static readonly BarKind[] Own = [.. Enum.GetValues<BarKind>().Where(kind => kind.WearsWindowsFace())];

    /// <summary>
    /// §490: the Karpo folder that stands for each of Windows' menus (a preset), whose faces it wears; null for the two
    /// with none (the quick link menu, the emoji panel), which wear the icon face of their own glyph.
    /// </summary>
    private static readonly (BarKind Kind, string? Preset)[] Matching =
    [
        (BarKind.WindowsQuickSettings, Presets.QuickPanelId), (BarKind.WindowsNotifications, Presets.NotificationCenterId),
        (BarKind.WidgetsBoard, Presets.WeatherId), (BarKind.WindowsSound, Presets.SoundId), (BarKind.WindowsNetworks, Presets.WifiId),
        (BarKind.WindowsCast, Presets.CastId), (BarKind.WindowsClipboard, Presets.ClipboardId),
        (BarKind.QuickLink, null), (BarKind.EmojiPanel, null),
    ];

    /// <summary>The Karpo folder of <paramref name="kind"/>'s faces (<see cref="Matching"/>), on a PC with a battery.</summary>
    private static BarItem KarposFolder(BarKind kind)
    {
        if (Matching.Single(match => match.Kind == kind).Preset is { } preset)
        {
            return Presets.Folder(Presets.Find(preset)!, new PresetMachine(HasBattery: true, FixedDrives: [], Rotates: false));
        }
        var folder = BarKind.FolderList.New();
        folder.Folder = new FolderOptions { Faces = [new FolderFace { Kind = FolderFaces.IconId, Glyph = kind.Glyph() }] };
        return folder;
    }

    /// <summary>
    /// §490 (the person, 2026-11-02: "the windows menu taskbar widgets dont look like the ones on the folder faces, the
    /// icons are a bit different etc. i want them to look exactly the same"): every one of Windows' menus wears faces —
    /// none is a glyph button any more — and they are the faces of the Karpo folder that stands for it, worn as a
    /// folder's face is, its style's Taskbar widgets (§499: one part for everything on the bar, StyleTests).
    /// </summary>
    [Fact]
    public void EveryWindowsMenuWearsTheFacesOfItsKarpoFolder()
    {
        Assert.Equal(Matching.Select(match => match.Kind).Order(), Own.Order());
        Assert.All(Enum.GetValues<BarKind>().Where(kind => kind.IsWindowsMenu()), kind => Assert.True(kind.WearsWindowsFace(), $"{kind}"));
        foreach (var kind in Own)
        {
            Assert.False(kind.IsWindowsButton());
            Assert.Equal(WidgetSource.WindowsFaces, BarEntries.Of(kind).Drawing);
            var karpo = KarposFolder(kind);
            var own = WindowsFaces.Of(kind, battery: true, percent: true)!.Faces;
            Assert.Equal(karpo.Folder!.Faces.Select(face => (face.Kind, face.Glyph)), own.Select(face => (face.Kind, face.Glyph)));
        }
    }

    /// <summary>
    /// §490: one drawing — for the same readings and look, each of Windows' own measures and paints the very pixels of the
    /// Karpo folder of its faces, dark and light, at 100 % and 150 %. All but the Widgets button, whose weather says the
    /// conditions as Windows' does (its content; the same face, §474).
    /// </summary>
    [UiFact]
    public void WindowsOwnDrawsTheVeryPictureOfItsKarpoFolder() => Offscreen.OnStaThread(gfx =>
    {
        var probe = BatteryIsland.Probe;
        BatteryIsland.Probe = () => new SystemStatus.Battery(76, Plugged: false, Saver: false);
        WindowsBatteryPercent.Stand = true;
        FolderIsland.StatusStand = FolderFaceTests.Radios;
        Clock.Now = () => new DateTime(2026, 11, 2, 9, 41, 0);
        var config = new Config { Height = 48 };
        try
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var style = new IslandStyle(dpi / 96f, false, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), LookFor(dark));
                    foreach (var kind in Own.Where(kind => kind != BarKind.WidgetsBoard))
                    {
                        var karpo = KarposFolder(kind);
                        using var own = (FolderIsland)Karpo.Dock.Islands.Create(Config.IslandKey(null, kind.Id()), kind.New(), gfx, config)!;
                        using var folder = (FolderIsland)Karpo.Dock.Islands.Create(Config.IslandKey(null, karpo.Id), karpo, gfx, config)!;
                        var (ownCells, folderCells) = (own.Measure(style), folder.Measure(style));
                        var length = ownCells.Lengths?[0] ?? ownCells.CellLength;
                        Assert.Equal(folderCells.Lengths?[0] ?? folderCells.CellLength, length);
                        var (mine, theirs) = (Pixels(own, style, length), Pixels(folder, style, length));
                        Assert.Equal(folder.FaceShown, own.FaceShown);
                        Assert.True(mine.AsSpan().SequenceEqual(theirs), $"{kind} ({(dark ? "dark" : "light")}, {dpi} dpi) draws other pixels than its Karpo folder");
                        Assert.Contains(mine.Where((_, at) => at % 4 == 3), alpha => alpha > 24);
                    }
                }
            }
        }
        finally
        {
            BatteryIsland.Probe = probe;
            WindowsBatteryPercent.Stand = null;
            FolderIsland.StatusStand = null;
            Clock.Now = Clock.Real;
        }
    });

    /// <summary>What <paramref name="island"/> paints on the bar in a cell <paramref name="length"/> long: its premultiplied BGRA pixels.</summary>
    private static byte[] Pixels(FolderIsland island, IslandStyle style, float length)
    {
        _ = island.Surface(0, style);
        byte[] data = [];
        var (width, height) = ((uint)MathF.Ceiling(length), (uint)MathF.Ceiling(style.Thickness));
        Offscreen.Bitmap(width, height, dc => island.PaintBar(dc, 0, style, length, style.Thickness), bitmap =>
        {
            data = new byte[width * height * 4];
            fixed (byte* into = data)
            {
                Karpo.Dock.Native.ComUtil.Check(bitmap->CopyPixels(null, width * 4, (uint)data.Length, into));
            }
        });
        return data;
    }

    /// <summary>
    /// A hit is Windows' button where it is a glyph button's, or in a widget wearing Windows' face; a folder of Karpo's, a
    /// pin, Start (its own path) and nothing are none.
    /// </summary>
    [Fact]
    public void AClickOnWindowsFaceIsWindowsButtonAndOnAFolderNever()
    {
        foreach (var kind in Own)
        {
            Assert.Equal(kind, TheDock.WindowsButtonOf(Hit.Cell(3, 0), kind));
        }
        Assert.Equal(BarKind.PenMenu, TheDock.WindowsButtonOf(Hit.WindowsButton(BarKind.PenMenu).In(2), BarKind.PenMenu));
        Assert.Null(TheDock.WindowsButtonOf(Hit.Cell(3, 0), BarKind.FolderList));
        Assert.Null(TheDock.WindowsButtonOf(Hit.Cell(3, 0), BarKind.Volume));
        Assert.Null(TheDock.WindowsButtonOf(Hit.Start, BarKind.Start));
        Assert.Null(TheDock.WindowsButtonOf(null, BarKind.WindowsQuickSettings));
    }

    /// <summary>
    /// Its menu at rest: its faces' rows, as Windows' own icons have them — the network's troubleshooter and page, the
    /// volume's mixer, the clock's date and time —, worked on it; never a row that edits it, and in edit mode no face nor
    /// contents to set (it has no folder's options).
    /// </summary>
    [Fact]
    public void ItsMenuIsItsFacesRowsAndNothingToEdit()
    {
        var battery = WidgetMachine.Read().HasBattery;
        foreach (var kind in Own)
        {
            var item = kind.New();
            Assert.Null(item.Folder);
            var (rows, _) = TheDock.BarMenuRows(item);
            var (faces, _) = TheDock.FaceRows(WindowsFaces.Of(kind, battery, percent: true)!, null);
            Assert.NotEmpty(rows);
            // The weather's lead to its forecast's site, none without one; the icon face has none: the entry's own page then
            // (Windows' taskbar settings, the emoji panel's typing settings), as any entry's.
            Assert.Equal(faces.Count > 0 ? faces.Select(row => row.Text) : [TheDock.PageOf(kind).Title], rows.Select(row => row.Text));
            Assert.DoesNotContain(rows, row => row.Text == S.DockWidgetMenuEditFolder);
        }
        // The network's own rows are among quick settings', as on Windows' network icon.
        var (quick, _) = TheDock.BarMenuRows(BarKind.WindowsQuickSettings.New());
        var (network, _) = TheDock.FaceRows(new FolderOptions { Faces = [FolderFace.Of(FolderFaces.WifiId)] }, null);
        Assert.Subset(quick.Select(row => row.Text).ToHashSet(), network.Where(row => row.Id != 0).Select(row => row.Text).ToHashSet());
    }
    /// <summary>
    /// Each draws Windows' faces from the readings it is handed — the Wi-Fi's bars and the volume, the charge, the time,
    /// the weather —, all stand-ins: it is no folder, holds nothing, opens nothing of Karpo's, and is named as Windows'
    /// button is, what its faces say after.
    /// </summary>
    [UiFact]
    public void EachDrawsWindowsFacesFromTheReadings() => Offscreen.OnStaThread(gfx =>
    {
        var probe = BatteryIsland.Probe;
        BatteryIsland.Probe = () => new SystemStatus.Battery(76, Plugged: false, Saver: false);
        WindowsBatteryPercent.Stand = true;
        FolderIsland.StatusStand = FolderFaceTests.Radios;
        FolderIsland.WeatherStand = SavedWeather.Cache();
        Clock.Now = () => new DateTime(2026, 11, 2, 9, 41, 0);
        var config = new Config { Height = 48 };
        var style = new IslandStyle(1f, false, Layout.CellPx(96, config), Layout.IconPx(96, config), LookFor(dark: true));
        try
        {
            foreach (var kind in Own)
            {
                using var island = (FolderIsland)Karpo.Dock.Islands.Create(Config.IslandKey(null, kind.Id()), kind.New(), gfx, config)!;
                Assert.False(island.IsFolder);
                Assert.Empty(island.Tiles);
                Assert.Equal(WindowsFaces.Of(kind, battery: true, percent: true)!.Faces.Select(face => face.Kind), island.Options.Faces.Select(face => face.Kind));
                Assert.Equal(default, island.Click(0));
                Assert.True(island.ClickDoes(0));
                Assert.StartsWith(BarEntries.Of(kind).Info.Title + "\n", island.Tooltip(0) + "\n", StringComparison.Ordinal);
                var cells = island.Measure(style);
                Assert.True(cells.Exact);
                Assert.True(cells.Count == 1 && (cells.Lengths?[0] ?? cells.CellLength) > style.Icon, $"{kind}: {cells.CellLength}");
                _ = island.Surface(0, style);
                switch (kind)
                {
                    case BarKind.WindowsQuickSettings:
                        // The network, the volume and the charge: three readings, the charge's "76%".
                        Assert.Equal(3, island.FaceShown.Split('\u001F').Length);
                        Assert.Contains("|76%|", island.FaceShown, StringComparison.Ordinal);
                        Assert.True(island.Tooltip(0)!.Split('\n').Length > 1, "quick settings' button says nothing of its faces");
                        break;
                    case BarKind.WindowsNotifications:
                        Assert.Equal(2, island.FaceShown.Split('\u001F').Length);
                        break;
                }
            }
        }
        finally
        {
            BatteryIsland.Probe = probe;
            WindowsBatteryPercent.Stand = null;
            FolderIsland.StatusStand = null;
            FolderIsland.WeatherStand = null;
            Clock.Now = Clock.Real;
        }
    });

    /// <summary>
    /// §485: Windows' own battery face follows Windows' "Battery percentage": the faces it is made of say so (the battery's
    /// <see cref="FolderFace.NoPercent"/>, never written to the file), and it is read from Explorer\Advanced's
    /// IsBatteryPercentageEnabled, off where it is missing, as Windows ships it. A change of it alone, or of the clock's
    /// seconds, is a change the faces follow.
    /// </summary>
    [Fact]
    public void ItsBatterySaysItsPercentageOnlyWhereWindowsDoes()
    {
        FolderFace Battery(bool percent) => WindowsFaces.Of(BarKind.WindowsQuickSettings, battery: true, percent)!.Faces.Single(face => face.Kind == FolderFaces.BatteryId);
        Assert.False(Battery(percent: true).NoPercent);
        Assert.True(Battery(percent: false).NoPercent);
        Assert.True(Battery(percent: false).Clone().NoPercent);
        var written = BarKind.FolderList.New();
        written.Folder = WindowsFaces.Of(BarKind.WindowsQuickSettings, battery: true, percent: false);
        Assert.DoesNotContain("percent", System.Text.Json.JsonSerializer.Serialize(written, ConfigJson.Default.BarItem), StringComparison.OrdinalIgnoreCase);
        // Karpo's own battery face says it, whatever Windows says.
        Assert.False(FolderFace.Of(FolderFaces.BatteryId).NoPercent);

        static int? Missing(string name) => null;
        Assert.False(WindowsTaskbar.From(Missing, battery: true).BatteryPercent);
        Assert.True(WindowsTaskbar.From(name => name == WindowsTaskbar.BatteryPercentValue ? 1 : null, battery: true).BatteryPercent);
        Assert.False(WindowsTaskbar.From(name => name == WindowsTaskbar.BatteryPercentValue ? 0 : null, battery: true).BatteryPercent);
        Assert.Equal("IsBatteryPercentageEnabled", WindowsTaskbar.BatteryPercentValue);

        var off = WindowsTaskbar.OutOfTheBox;
        Assert.False(WindowsTaskbarFollowed.Differ(off, off with { Rotates = true }));
        Assert.True(WindowsTaskbarFollowed.Differ(off, off with { BatteryPercent = true }));
        Assert.True(WindowsTaskbarFollowed.Differ(off, off with { Seconds = true }));
    }

    /// <summary>
    /// §485: drawn, quick settings' button says "76%" beside its battery where Windows' "Battery percentage" is on, and the
    /// glyph alone where it is off — its tooltip saying the charge either way —, narrower; a folder of Karpo's wearing the
    /// battery says it whatever Windows says.
    /// </summary>
    [UiFact]
    public void ItsBatteryFaceFollowsWindowsBatteryPercentage() => Offscreen.OnStaThread(gfx =>
    {
        var probe = BatteryIsland.Probe;
        BatteryIsland.Probe = () => new SystemStatus.Battery(76, Plugged: false, Saver: false);
        FolderIsland.StatusStand = FolderFaceTests.Radios;
        var config = new Config { Height = 48 };
        var style = new IslandStyle(1f, false, Layout.CellPx(96, config), Layout.IconPx(96, config), LookFor(dark: true));
        try
        {
            (string Shown, float Length, string? Tip) Drawn(BarItem item, bool percent)
            {
                WindowsBatteryPercent.Stand = percent;
                using var island = (FolderIsland)Karpo.Dock.Islands.Create(Config.IslandKey(null, item.Id), item, gfx, config)!;
                var cells = island.Measure(style);
                _ = island.Surface(0, style);
                return (island.FaceShown, cells.Lengths?[0] ?? cells.CellLength, island.Tooltip(0));
            }
            var on = Drawn(BarKind.WindowsQuickSettings.New(), percent: true);
            var off = Drawn(BarKind.WindowsQuickSettings.New(), percent: false);
            Assert.Contains("|76%|", on.Shown, StringComparison.Ordinal);
            Assert.DoesNotContain("76%|", off.Shown, StringComparison.Ordinal);
            Assert.True(off.Length < on.Length, $"{off.Length} not narrower than {on.Length}");
            Assert.Equal(on.Tip, off.Tip);
            var folder = BarKind.FolderList.New();
            folder.Folder = new FolderOptions { Faces = [FolderFace.Of(FolderFaces.BatteryId)] };
            Assert.Contains("|76%|", Drawn(folder, percent: false).Shown, StringComparison.Ordinal);
        }
        finally
        {
            BatteryIsland.Probe = probe;
            WindowsBatteryPercent.Stand = null;
            FolderIsland.StatusStand = null;
        }
    });
}
