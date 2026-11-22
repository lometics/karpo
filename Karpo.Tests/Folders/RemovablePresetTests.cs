// The removable devices are a preset (docs\widgets.md §91): what a click on the removable-devices
// button gave — its flyout, a row per device with what it holds and its Eject — is a folder holding
// the removable devices' line (RemovableIsland.Cards.cs) under the removable face: the stick the
// button was, quiet with nothing plugged in, a count on its corner from two, painted again in place
// as devices come and go. Made-up devices throughout (DriveWatch.Source, FolderIsland.DevicesStand):
// nothing reaches a real volume.
//
// Pictured beside the flyout it replaces (rendered before it went: removable-two-*.png,
// removable-none-*.png, removable-mixed-dark.png): removable-preset-<dark|light>.png at 150 %, and
// the face on the bar, removable-face-<dark|light>.png.

using Karpo.Dock;
using Karpo.Dock.Drives;
using Karpo.Tests.Islands;
using Karpo.Tests.Storage;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Folders.ListFormPictures;

namespace Karpo.Tests.Folders;

[Collection(AttachedDrivesCollection.Collection)]
public sealed unsafe class RemovablePresetTests
{
    private static readonly PresetMachine Machine = new(HasBattery: false, FixedDrives: []);

    /// <summary>The removable devices' preset folder, as the add list makes it.</summary>
    private static BarItem Holder() => Presets.Folder(Presets.Find(Presets.RemovableId)!, Machine);

    private static ulong Gb(double gigabytes) => (ulong)(gigabytes * 1024 * 1024 * 1024);

    private static Drive Device(int letter, string label, double total, double free, DriveKind kind = DriveKind.Removable) => new()
    {
        Letter = letter,
        Kind = kind,
        Volume = $@"\\?\Volume{{7e57{letter:D4}-0000-0000-0000-000000000000}}\",
        Label = label,
        Total = Gb(total),
        Free = Gb(free),
    };

    private static readonly Drive Stick = Device(4, "SANDISK", 29, 12);
    private static readonly Drive Card = Device(5, "SD Card", 64, 60);

    /// <summary>Stands in for what is plugged in and the devices' pictures while it lives.</summary>
    private sealed class Plugged : IDisposable
    {
        private readonly Func<bool, bool, IReadOnlyList<Drive>>? drives = DriveWatch.Source;
        private readonly Func<string, int, Pixels?>? icons = DriveIcons.Source;
        private Drive[] devices;

        public Plugged(params Drive[] devices)
        {
            this.devices = devices;
            DriveWatch.Source = (_, remote) => [.. this.devices.Where(device => remote || device.Kind != DriveKind.Remote)];
            DriveIcons.Source = (_, _) => null;
        }

        /// <summary>Something else is plugged in from now on.</summary>
        public void Now(params Drive[] next) => devices = next;

        public void Dispose()
        {
            DriveWatch.Source = drives;
            DriveIcons.Source = icons;
        }
    }

    // --- The preset ------------------------------------------------------------------------------------

    /// <summary>
    /// The flyout's list, the removable devices' line, alone: the flyout had no footer, so the preset
    /// has none. It wears the removable face, which has no name and nothing to set.
    /// </summary>
    [Fact]
    public void ThePresetIsTheRemovableDevicesLineUnderTheStick()
    {
        var preset = Presets.Find(Presets.RemovableId)!;
        Assert.Equal(FolderFaces.RemovableId, preset.Face!.Kind);
        Assert.Equal(["removableDevices"], preset.Items.Select(item => item.Form));

        var folder = Holder().Folder!;
        Assert.Same(FolderFaces.Removable, folder.Face.Info);
        Assert.Empty(folder.Face.Info.Options);
        Assert.Equal(ListKind.RemovableDevices, Assert.Single(folder.Cells).Island.List);
    }

    /// <summary>
    /// The button is gone from the bar, and with it its flyout height and its badge's setting: the
    /// line is all there is, and a line has neither.
    /// </summary>
    [Fact]
    public void TheButtonIsGoneAndTheLineHasNothingOfItsOwnToSet()
    {
        Assert.Null(BarEntries.Find("removableDevices"));
        Assert.NotNull(ListWidgets.Find("removableDevices"));
        Assert.Equal(WidgetOptionsKind.None, WidgetInfos.RemovableDevices.Options);
    }

    /// <summary>The folder opened: the removable devices' line, listing what is plugged in.</summary>
    [UiFact]
    public void TheFolderHoldsTheLineOfWhatIsPluggedIn()
    {
        using var plugged = new Plugged(Stick, Card);
        Offscreen.OnStaThread(gfx =>
        {
            using var folder = Open(gfx);
            var line = Assert.IsType<RemovableIsland>(Assert.Single(folder.Tiles).Island);
            Assert.Equal(["SANDISK (E:)", "SD Card (F:)"], line.Devices.Select(device => device.Title));
            folder.Closed();
        });
    }

    // --- The face --------------------------------------------------------------------------------------

    /// <summary>
    /// The count on the stick's corner, as the button had it: none for nothing or one (the glyph says
    /// one), the number from two, "9+" past nine.
    /// </summary>
    [Theory]
    [InlineData(0, null)]
    [InlineData(1, null)]
    [InlineData(2, "2")]
    [InlineData(9, "9")]
    [InlineData(10, "9+")]
    [InlineData(30, "9+")]
    public void TheCountStandsOnTheStickFromTwo(int count, string? badge) =>
        Assert.Equal(badge, RemovableFaceDrawing.Badge(count));

    /// <summary>The hover is the button's: how many and their names, five at most, or that nothing is plugged in.</summary>
    [Fact]
    public void TheHoverSaysHowManyAndWhich()
    {
        Assert.Equal($"{S.WidgetRemovableTitle}\n{S.WidgetRemovableNothing}", RemovableIsland.Tip([]));
        Assert.Equal($"{S.WidgetRemovableCount(2)}\nSANDISK (E:)\nSD Card (F:)", RemovableIsland.Tip([Stick, Card]));
        var many = Enumerable.Range(0, 7).Select(index => Device(4 + index, $"Stick {index}", 32, 16)).ToList();
        var tip = RemovableIsland.Tip(many);
        Assert.StartsWith(S.WidgetRemovableCount(7), tip, StringComparison.Ordinal);
        Assert.EndsWith("…", tip, StringComparison.Ordinal);
        Assert.Equal(7, tip.Split('\n').Length);
    }

    /// <summary>
    /// A device plugged in or taken out paints the face again in place — the count, the quiet stick,
    /// the hover — never a rebuild, and it stays one size, its count's room kept (§276); a tick with
    /// nothing new paints nothing. Windows saying so (the dock forgets the drives, then refreshes every island)
    /// and the minute's tick, for what it does not announce, both reach it. Arranged, where the face
    /// stays with nothing plugged in (§290); at rest that takes it off the bar (the test below).
    /// </summary>
    [UiFact]
    public void TheFacePaintsAgainInPlaceAsDevicesComeAndGo()
    {
        using var plugged = new Plugged(Stick);
        Offscreen.OnStaThread(gfx =>
        {
            var holder = Holder();
            using var island = new FolderIsland(Config.IslandKey(null, holder.Id), holder, gfx, new Config()) { Arranged = true };
            Assert.True(island.ShowsDevices);
            Assert.Equal(60_000, island.IntervalMs);
            var config = new Config { Height = 48 };
            var look = LookFor(dark: true);
            var style = new IslandStyle(1f, false, Layout.CellPx(96, config), Layout.IconPx(96, config), look);
            var size = island.CellSize(0, style);
            _ = island.Measure(style);
            _ = island.Surface(0, style);
            Assert.StartsWith("1|", island.FaceShown, StringComparison.Ordinal);
            var painted = 0;
            var glyphs = new List<DXGI_RGBA>();
            var texts = 0;
            PaintWatch.Watching = (_, what, color) =>
            {
                painted++;
                if (what == Painted.Glyph)
                {
                    glyphs.Add(color);
                }
                texts += what == Painted.Text ? 1 : 0;
                return color;
            };
            try
            {
                // Nothing came or went: nothing to paint.
                Assert.False(island.FaceTick(gfx));
                Assert.Equal(0, painted);

                // A card plugged in, and Windows says so: the count, in place.
                plugged.Now(Stick, Card);
                DriveWatch.Forget();
                Assert.False(((IIsland)island).Refresh(gfx));
                Assert.True(painted > 0, "a device plugged in was not painted");
                Assert.StartsWith("2|", island.FaceShown, StringComparison.Ordinal);
                Assert.Equal(1, texts);
                Assert.Equal(RemovableIsland.Tip([Stick, Card]), island.Tooltip(0));
                Assert.Equal(size, island.CellSize(0, style));

                // Both taken out, which Windows did not announce: the minute's tick finds it, and the
                // stick is drawn quiet, with no count.
                (painted, texts) = (0, 0);
                glyphs.Clear();
                plugged.Now();
                Assert.False(island.Tick(gfx));
                Assert.True(painted > 0, "the devices taken out were not painted");
                Assert.StartsWith("0|", island.FaceShown, StringComparison.Ordinal);
                Assert.Equal(0, texts);
                Assert.Equal(Rgba(new IslandInk(look).Disabled), Rgba(Assert.Single(glyphs)));
                Assert.Equal($"{S.WidgetRemovableTitle}\n{S.WidgetRemovableNothing}", island.Tooltip(0));
                Assert.Equal(size, island.CellSize(0, style));

                // One again: the stick in the text's ink.
                glyphs.Clear();
                plugged.Now(Card);
                DriveWatch.Forget();
                Assert.False(((IIsland)island).Refresh(gfx));
                Assert.Equal(Rgba(new IslandInk(look).Text), Rgba(Assert.Single(glyphs)));
            }
            finally
            {
                PaintWatch.Watching = null;
            }
        });
    }

    /// <summary>
    /// The person, 2026-10-22: "if there is no removable device it should not show up as a face in
    /// view mode, only in edit mode" (§290). With nothing plugged in the face is off the bar at rest —
    /// the folder takes no cell, as now playing's does while nothing plays — and on it while the bar is
    /// arranged; a device plugged in, and Windows saying so, lays the bar out again with it, and taking
    /// it out takes it off again.
    /// </summary>
    [UiFact]
    public void WithNothingPluggedInTheFaceIsOnTheBarOnlyWhileArranged()
    {
        using var plugged = new Plugged();
        Offscreen.OnStaThread(gfx =>
        {
            var holder = Holder();
            using var island = new FolderIsland(Config.IslandKey(null, holder.Id), holder, gfx, new Config());
            var config = new Config { Height = 48 };
            var style = new IslandStyle(1f, false, Layout.CellPx(96, config), Layout.IconPx(96, config), LookFor(dark: true));

            Assert.Equal(0, island.Measure(style).Count);
            Assert.Empty(island.FacesOnBar);

            island.Arranged = true;
            Assert.Equal(1, island.Measure(style).Count);
            Assert.Same(FolderFaces.Removable, Assert.Single(island.FacesOnBar).Info);
            var arranged = island.CellSize(0, style);

            island.Arranged = false;
            Assert.Equal(0, island.Measure(style).Count);
            plugged.Now(Stick);
            DriveWatch.Forget();
            Assert.True(((IIsland)island).Refresh(gfx), "a device plugged in did not lay the bar out again");
            Assert.Equal(1, island.Measure(style).Count);
            Assert.Equal(arranged, island.CellSize(0, style));

            plugged.Now();
            DriveWatch.Forget();
            Assert.True(((IIsland)island).Refresh(gfx), "the device taken out did not lay the bar out again");
            Assert.Equal(0, island.Measure(style).Count);
        });
    }

    /// <summary>
    /// The face is its glyph, its widest count's room across a bar and the button's ends whatever is
    /// plugged in (§276), at every bar, across it and down it; with nothing, as it is arranged.
    /// </summary>
    [UiFact]
    public void TheFaceIsOneSizeWhateverIsPluggedIn()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var height in new[] { 28, 48, 72, 115 })
            {
                var config = new Config { Height = height };
                foreach (var vertical in new[] { false, true })
                {
                    foreach (var text in new[] { 1f, 2.25f })
                    {
                        var style = new IslandStyle(1.5f, vertical, Layout.CellPx(144, config), Layout.IconPx(144, config), LookFor(dark: true, text));
                        var where = $"{height} DIP, text ×{text}{(vertical ? ", vertical" : "")}";
                        foreach (var count in new[] { 0, 1, 2, 9, 30 })
                        {
                            using var island = Made(gfx, Enumerable.Range(0, count).Select(index => Device(4 + index, $"Stick {index}", 32, 16)).ToArray());
                            island.Arranged = count == 0;
                            var (width, tall) = island.CellSize(0, style);
                            var trail = vertical ? 0 : RemovableFaceDrawing.CountTrail(gfx, style, "9+");
                            Assert.True(trail > 0 || vertical, $"{where}: no room for the count");
                            Assert.True(MathF.Abs(FaceWidthTests.GlyphButton(style, trail) - (vertical ? tall : width)) <= 0.5f, $"{where}, {count} plugged in: {width}×{tall}");
                        }
                    }
                }
            }
        });
    }

    /// <summary>
    /// The face's right click: each device plugged in under its name with its line's rows — none with no
    /// folder made to read them — then AutoPlay's page, what Windows does with a device plugged in.
    /// </summary>
    [Fact]
    public void TheFacesRowsAreItsDevicesAndAutoPlay() =>
        Assert.Equal([WindowsPages.AutoPlay.Title], Karpo.Dock.Dock.BarMenuRows(Holder()).Items.Select(item => item.Text));

    // --- Pictures --------------------------------------------------------------------------------------

    /// <summary>
    /// The preset as it opens with two devices, with nothing plugged in, with a disc drive empty and a
    /// stick, and at 225 % text — at 150 %, to stand beside removable-*.png of the flyout.
    /// </summary>
    [UiFact]
    public void RemovablePresetPictures()
    {
        using var plugged = new Plugged();
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var folders = new List<FolderIsland>();
                try
                {
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    void Add(string caption, Drive[] devices, float text = 1f)
                    {
                        plugged.Now(devices);
                        DriveWatch.Forget();
                        var look = LookFor(dark, text);
                        var folder = Open(gfx);
                        folders.Add(folder);
                        panels.Add((caption, look, folder, Plan(folder, look, 1.5f).Plan));
                    }

                    Add("Two plugged in", [Stick, Card]);
                    Add("Nothing plugged in", []);
                    Add("A disc drive, empty, and a stick", [Device(4, "CD Drive", 0, 0, DriveKind.Optical), Device(6, "Backup Stick", 238, 201)]);
                    Add("Two plugged in: 225 % text", [Stick, Card], 2.25f);
                    Sheet(gfx, $"removable-preset-{(dark ? "dark" : "light")}.png", 1.5f, panels);
                }
                finally
                {
                    foreach (var folder in folders)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                }
            });
        }
    }

    /// <summary>
    /// The face on a bar of 40 and 48 DIP and at 150 %: nothing plugged in, one, two and twelve —
    /// removable-face-&lt;dark|light&gt;.png, beside the button's drive-bar-*.png before it went.
    /// </summary>
    [UiFact]
    public void RemovableFacePictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                int[] counts = [0, 1, 2, 12];
                var styles = new[] { (40, 96u), (48, 96u), (48, 144u) }
                    .Select(pair => new IslandStyle(pair.Item2 / 96f, false, Layout.CellPx(pair.Item2, new Config { Height = pair.Item1 }), Layout.IconPx(pair.Item2, new Config { Height = pair.Item1 }), look))
                    .ToList();
                // Nothing plugged in: as it is arranged, the only time it is on the bar (§290).
                var islands = counts.Select(count => Made(gfx, [.. Enumerable.Range(0, count).Select(index => Device(4 + index, $"Stick {index}", 32, 16))])).ToList();
                islands[0].Arranged = true;
                try
                {
                    const float gap = 12;
                    var width = gap + styles.Sum(style => (counts.Length * (island0(style).Width + gap)) + gap);
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
                    }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"removable-face-{(dark ? "dark" : "light")}.png")));
                }
                finally
                {
                    foreach (var island in islands)
                    {
                        island.Dispose();
                    }
                }

                (float Width, float Height) island0(IslandStyle style) => islands[0].CellSize(0, style);
            });
        }
    }

    private static (float R, float G, float B, float A) Rgba(DXGI_RGBA color) => (color.r, color.g, color.b, color.a);

    /// <summary>The preset's folder wearing the face over <paramref name="devices"/>, never what this PC has.</summary>
    private static FolderIsland Made(Gfx gfx, Drive[] devices)
    {
        var holder = Holder();
        FolderIsland.DevicesStand = devices;
        try
        {
            return new FolderIsland(Config.IslandKey(null, holder.Id), holder, gfx, new Config());
        }
        finally
        {
            FolderIsland.DevicesStand = null;
        }
    }

    /// <summary>The preset's folder opened: its line reads what <see cref="Plugged"/> stands in.</summary>
    private static FolderIsland Open(Gfx gfx)
    {
        var holder = Holder();
        var folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
        folder.Opened(gfx);
        return folder;
    }
}
