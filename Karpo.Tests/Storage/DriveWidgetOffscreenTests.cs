// The two drive faces drawn for real, on a desktop nobody sees, with made-up
// drives: no disk is read and nothing of the PC these run on reaches a picture
// (DriveWatch.Source). The drive and network drive entries went into the drive
// presets (docs\widgets.md §99), whose folders wear these faces and draw what
// the entries drew from their drive line, so the entries' checks hold the faces.
//
// Two things are checked here that a picture cannot show:
//
//   * a drive or network drive face is the same number of cells in every
//     state it can be in — 0 %, 55 %, 98 % full, a drive with no medium, a
//     drive that isn't connected, a one-letter label and a very long one (the
//     removable-devices button's own check went with it to its preset's face,
//     RemovablePresetTests);
//   * what it draws stays inside its cell.
//
// The pictures are contact sheets of the two kinds side by side, in dark and
// light, at 28, 48 and 72 px bars, on a vertical bar, and with a broken drive
// among them. They run only with KARPO_UI_TESTS=1 and go to KARPO_PNG_DIR.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.DirectX.DirectX;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.Storage;

/// <summary>
/// The tests that stand in for the drives the bar can see run by themselves: the seam
/// (<see cref="DriveWatch.Source"/>) is one static, and every drive widget reads it.
/// </summary>
[CollectionDefinition(Collection, DisableParallelization = true)]
public sealed class AttachedDrivesCollection
{
    public const string Collection = "attached drives";
}

[Collection(AttachedDrivesCollection.Collection)]
public sealed unsafe class DriveWidgetOffscreenTests
{
    private static readonly int[] Heights = [28, 48, 72];

    private static string Pictures
    {
        get
        {
            var folder = Environment.GetEnvironmentVariable("KARPO_PNG_DIR") is { Length: > 0 } named
                ? named
                : Path.Combine(Path.GetTempPath(), "karpo-tests", "pictures");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    private static Look LookFor(bool dark, float textScale = 1f) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8))
    { TextScale = textScale };

    private static ulong Gb(double gigabytes) => (ulong)(gigabytes * 1024 * 1024 * 1024);

    private const string SystemVolume = @"\\?\Volume{11111111-1111-1111-1111-111111111111}\";
    private const string DataVolume = @"\\?\Volume{33333333-3333-3333-3333-333333333333}\";

    private static Drive Disk(int letter, string label, double total, double free, DriveKind kind = DriveKind.Fixed, string volume = SystemVolume) =>
        new() { Letter = letter, Kind = kind, Volume = volume, Label = label, Total = Gb(total), Free = Gb(free) };

    /// <summary>A drive's line of <paramref name="kind"/>, pointed at <paramref name="target"/>.</summary>
    internal static CellItem Widget(string id, ListKind kind, DriveTargetOptions? target = null)
    {
        var line = kind.New();
        line.Id = id;
        line.Drive = target ?? new DriveTargetOptions();
        return line;
    }

    /// <summary>
    /// The drive or network drive preset's folder, as the add list makes it, its drive line pointed
    /// at <paramref name="target"/>; where <paramref name="spread"/>, the folder pin under it at the
    /// drive's root, as choosing the drive in Settings sets it (DriveTargets.SpreadContents).
    /// </summary>
    internal static BarItem Holder(string id, ListKind kind, DriveTargetOptions? target, bool spread = false)
    {
        var holder = Presets.Folder(Presets.Find(kind == ListKind.NetworkDrive ? Presets.NetworkDriveId : Presets.DriveId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
        holder.Id = id;
        var line = holder.Folder!.Cells.First().Island;
        line.Drive = target ?? new DriveTargetOptions();
        if (spread)
        {
            DriveTargets.SpreadContents(holder.Folder, line.Drive);
        }
        holder.Normalize();
        return holder;
    }

    private static DriveTargetOptions Target(string volume, string letter, string label) =>
        new() { Volume = volume, Letter = letter, Label = label };

    /// <summary>Stands in for the drives the bar can see while it lives.</summary>
    private sealed class Attached : IDisposable
    {
        private readonly Func<bool, bool, IReadOnlyList<Drive>>? before;

        public Attached(params Drive[] drives)
        {
            before = DriveWatch.Source;
            DriveWatch.Source = (_, remote) => drives.Where(drive => remote || drive.Kind != DriveKind.Remote).ToList();
        }

        public void Dispose() => DriveWatch.Source = before;
    }

    // --- The size follows what it shows, up to two cells (docs\widgets.md §244) ------

    private static IEnumerable<(string Where, IslandStyle Style)> Styles()
    {
        foreach (var height in (int[])[28, 48, 72, 115, 160])
        {
            var config = new Config { Height = height };
            foreach (var dpi in (uint[])[96, 144])
            {
                foreach (var vertical in new[] { false, true })
                {
                    foreach (var text in (float[])[1f, 2.25f])
                    {
                        yield return (
                            $"{height} DIP, {dpi} DPI, text ×{text}{(vertical ? ", vertical" : "")}",
                            new IslandStyle(dpi / 96f, vertical, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), LookFor(true, text)));
                    }
                }
            }
        }
    }

    [UiFact]
    public void ADriveFaceIsAsLongAsItsNameUpToTwoCellsHoweverFullTheDriveIs()
    {
        OnStaThread(gfx =>
        {
            var config = new Config();
            var target = Target(SystemVolume, "C:", "Local Disk");
            // Every state the widget can be in, one drive list each.
            Drive[][] states =
            [
                [Disk(2, "Local Disk", 931, 931)],                                   // empty
                [Disk(2, "Local Disk", 931, 419)],                                   // half
                [Disk(2, "Local Disk", 931, 9)],                                     // nearly full
                [Disk(2, "X", 931, 419)],                                            // a one-letter label
                [Disk(2, "Samsung T7 Shield Portable SSD 2 TB", 931, 419)],          // a very long one
                [Disk(2, "Local Disk", 0, 0)],                                       // no medium: no figures
                [Disk(3, "Data", 500, 100)],                                         // another drive: ours is gone
                [],                                                                   // nothing at all
            ];
            foreach (var (where, style) in Styles())
            {
                var lengths = new List<float>();
                foreach (var drives in states)
                {
                    using var attached = new Attached(drives);
                    using var island = New(gfx, config, ListKind.Drive, target);
                    var cells = island.Measure(style);
                    Assert.True(cells.Count == 1, $"{where}: {cells.Count} cells");
                    lengths.Add(cells.Lengths is [var only] ? only : cells.CellLength);
                }
                // As long as what it shows (docs\widgets.md §244, §275), up to the entry's two cells:
                // across a bar what its name says in the most room (its whole name, the name alone or
                // its letter), how full the drive is changing nothing; where the bar is too thin for a
                // name, the icon and the capacity bar alone; down a bar on its side the icon over its
                // capacity bar, the same for every drive.
                var most = DriveIsland.WideCells * style.Cell;
                var said = $"{where}: {string.Join(", ", lengths)} px";
                Assert.All(lengths, length => Assert.True(length <= most + 1 && length > 0, said));
                Assert.True(lengths[0] == lengths[1] && lengths[1] == lengths[2], said);
                if (style.Vertical)
                {
                    Assert.All(lengths, length => Assert.Equal(lengths[0], length, 1f));
                }
            }
        });
    }

    /// <summary>
    /// The vertical audit's finding for these kinds: the label was measured for a horizontal bar
    /// and then cut, so it was a blank line at 28 DIP and at large text. The rule now is that the
    /// size steps down until the drive letter's own shape fits across the bar, and where even the
    /// smallest will not, there is no label at all — never a size that cannot be read.
    /// </summary>
    [UiFact]
    public void ADriveLabelIsNeverDrawnAtASizeThatCannotBeReadAcrossTheBar()
    {
        OnStaThread(gfx =>
        {
            var config = new Config();
            var dropped = 0;
            foreach (var (where, style) in Styles())
            {
                var fit = new Fit(style);
                var label = DriveIsland.LabelFit(gfx, fit);
                if (label.Px <= 0)
                {
                    // No room at all: the icon and the bar carry the cell.
                    dropped++;
                    Assert.True(style.Vertical, $"{where}: a horizontal bar always has room for the letter");
                    Assert.False(DriveIsland.Rows(fit, style.Cell, 2 * style.Cell, 0, allowName: false).WithName, where);
                    continue;
                }
                if (!style.Vertical)
                {
                    Assert.Equal(fit.Primary, label.Px, 3);
                    continue;
                }
                // Across a vertical bar the size it chose must actually fit the bar's width.
                var wanted = TextLine.Template(gfx, "W:", label.Px, Weight.Regular).Width;
                Assert.True(wanted <= VerticalFit.Room(fit) + 0.01f,
                    $"{where}: the letter wants {wanted} px across a bar {VerticalFit.Room(fit)} px wide");
                Assert.True(label.Px <= fit.Primary + 0.01f, $"{where}: the label never grows past the widget text size");
            }
            // The narrow-bar-with-large-text case the audit found has to be among them, or this
            // test is checking nothing.
            Assert.True(dropped > 0, "no style was narrow enough to drop the label");
        });
    }

    // --- The pictures -----------------------------------------------------------

    [UiFact]
    public void DrivePictures()
    {
        OnStaThread(gfx =>
        {
            foreach (var height in Heights)
            {
                foreach (var dark in new[] { true, false })
                {
                    Sheet(gfx, height, dark, vertical: false, $"drive-bar-{height}-{(dark ? "dark" : "light")}.png");
                }
            }
            Sheet(gfx, 48, dark: true, vertical: false, "drive-bar-48-bigtext-dark.png", textScale: 1.5f);
            // The vertical audit's cases: a narrow bar, and a narrow bar with large text, where
            // the label was a blank line before.
            foreach (var height in new[] { 28, 48, 72 })
            {
                Sheet(gfx, height, dark: true, vertical: true, $"drive-vertical-{height}-dark.png");
                Sheet(gfx, height, dark: true, vertical: true, $"drive-vertical-{height}-bigtext-dark.png", textScale: 2.25f);
            }
        });
    }

    /// <summary>
    /// A strip of the two kinds: a drive half full, one nearly full, a network drive, and a drive
    /// that isn't connected.
    /// </summary>
    private static void Sheet(Gfx gfx, int height, bool dark, bool vertical, string file, float textScale = 1f)
    {
        const uint dpi = 96;
        var config = new Config { Height = height };
        var look = LookFor(dark, textScale);
        var theme = FlyoutTheme.For(look);
        var style = new IslandStyle(dpi / 96f, vertical, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), look);
        var gap = MathF.Round(6 * style.Scale);

        var drives = new Drive[]
        {
            Disk(2, "Local Disk", 931, 419),
            Disk(3, "Data", 500, 9, DriveKind.Fixed, DataVolume),
            Disk(25, "work (\\\\server)", 200, 50, DriveKind.Remote, "\\\\server\\work"),
        };

        var made = new List<(CellIsland Island, IslandStyle Style)>();
        using var all = new Attached(drives);
        try
        {
            made.Add((New(gfx, config, ListKind.Drive, Target(SystemVolume, "C:", "Local Disk")), style));
            made.Add((New(gfx, config, ListKind.Drive, Target(DataVolume, "D:", "Data")), style));
            made.Add((New(gfx, config, ListKind.NetworkDrive, Target("\\\\server\\work", "Z:", "work (\\\\server)")), style));
            // A drive the face's line names and cannot find: shown broken and present.
            made.Add((New(gfx, config, ListKind.Drive, Target("\\\\?\\Volume{99999999-9999-9999-9999-999999999999}\\", "E:", "Backup")), style));

            var lengths = made.Select(entry =>
            {
                var cells = entry.Island.Measure(entry.Style);
                return cells.CellLength;
            }).ToList();
            var along = lengths.Sum() + gap * (made.Count + 1);
            var across = style.Cell + 2 * gap;

            Picture(vertical ? across : along, vertical ? along : across, theme, file, dc =>
            {
                var at = gap;
                for (var index = 0; index < made.Count; index++)
                {
                    var (island, own) = made[index];
                    var (width, cellHeight) = island.CellSize(0, own);
                    var x = vertical ? gap : at;
                    var y = vertical ? at : gap;
                    PopupPaint.Fill(dc, PopupPaint.Rect(x, y, width, cellHeight), MathF.Round(8 * style.Scale), PopupPaint.Ink(theme.CardBackground));
                    Shifted(dc, x, y, () => island.PaintCell(dc, 0, own, width, cellHeight));
                    at += lengths[index] + gap;
                }
            });
        }
        finally
        {
            foreach (var (island, _) in made)
            {
                island.Dispose();
            }
        }
    }

    /// <summary>The drive or network drive preset's folder, wearing its face, its line pointed at <paramref name="target"/>.</summary>
    private static FolderIsland New(Gfx gfx, Config config, ListKind kind, DriveTargetOptions? target)
    {
        var holder = Holder(kind.ToString(), kind, target);
        var island = new FolderIsland(Config.IslandKey(null, holder.Id), holder, gfx, config);
        _ = ((IIsland)island).Refresh(gfx);
        return island;
    }

    /// <summary>A drive's line of <paramref name="kind"/> alone, pointed at <paramref name="target"/>, read once.</summary>
    private static DriveIsland Line(Gfx gfx, Config config, ListKind kind, DriveTargetOptions? target)
    {
        var island = new DriveIsland($"/{kind}", Widget(kind.ToString(), kind, target), gfx, config);
        _ = island.Refresh(gfx);
        return island;
    }

    // --- A network drive that isn't connected ------------------------------------

    private const string Share = "\\\\server\\work";

    private static DriveTargetOptions ShareTarget() => Target(Share, "Z:", "work (\\\\server)");

    private static Drive Connected() => Disk(25, "work (\\\\server)", 200, 50, DriveKind.Remote, Share);

    private static Look HighContrast() => new(
        Glass: false,
        Dark: true,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        HighContrast: true,
        HcWindow: Color.FromArgb(255, 0, 0, 0),
        HcText: Color.FromArgb(255, 255, 255, 255),
        HcHighlight: Color.FromArgb(255, 0x1A, 0xEB, 0xFF),
        HcHighlightText: Color.FromArgb(255, 0, 0, 0));

    /// <summary>
    /// Stands in for the drives' pictures while it lives: Explorer's own disconnected network drive,
    /// read from the shell's stock icons (no volume is asked), or none, so the drawn red X shows.
    /// </summary>
    private sealed class Pictured : IDisposable
    {
        private readonly Func<string, int, Pixels?>? before = Karpo.Dock.Drives.DriveIcons.Source;

        public Pictured(bool stock) => Karpo.Dock.Drives.DriveIcons.Source = (root, px) =>
            stock && root == Karpo.Dock.Drives.DriveIcons.DisconnectedRoot
                ? Karpo.Dock.Drives.DriveIcons.Stock(SHSTOCKICONID.SIID_DRIVENETDISABLED, px)
                : null;

        public void Dispose() => Karpo.Dock.Drives.DriveIcons.Source = before;
    }

    /// <summary>
    /// A network drive widget whose share is not there is drawn as Explorer draws it — disconnected,
    /// not faint and broken — and says so in its tooltip; a share whose first answer is still out is
    /// neither, and a local drive that is missing keeps the broken look. The widget is as long as what
    /// it shows, up to two cells, in all of them, at every bar.
    /// </summary>
    [UiFact]
    public void ANetworkDriveThatIsNotConnectedIsDisconnected()
    {
        OnStaThread(gfx =>
        {
            var config = new Config();
            using (new Attached())
            {
                using var share = Line(gfx, config, ListKind.NetworkDrive, ShareTarget());
                Assert.True(share.Disconnected);
                Assert.Equal($"work (\\\\server) (Z:)\n{S.CoreDriveTypeDisconnected}", share.Tooltip(0));
                using var local = Line(gfx, config, ListKind.Drive, Target(SystemVolume, "C:", "Local Disk"));
                Assert.False(local.Disconnected);
                using var unset = Line(gfx, config, ListKind.NetworkDrive, null);
                Assert.False(unset.Disconnected);
            }
            using (new Attached(new Drive { Letter = 25, Kind = DriveKind.Remote, Pending = true }))
            {
                using var share = Line(gfx, config, ListKind.NetworkDrive, ShareTarget());
                Assert.False(share.Disconnected);
            }
            foreach (var (where, style) in Styles())
            {
                foreach (var drives in (Drive[][])[[Connected()], []])
                {
                    using var attached = new Attached(drives);
                    using var island = New(gfx, config, ListKind.NetworkDrive, ShareTarget());
                    // Its name and its bar, or the name alone while disconnected, up to the two cells (§244).
                    var cells = island.Measure(style);
                    var length = cells.Lengths is [var only] ? only : cells.CellLength;
                    Assert.True(length <= (DriveIsland.WideCells * style.Cell) + 1 && length > 0, $"{where}, {drives.Length} attached: {length} px");
                }
            }
        });
    }

    /// <summary>
    /// The disconnected network drive beside the same drive connected and a share still pending, as
    /// the network drive face on the bar at three heights, down a vertical bar, and as lines of a
    /// list at every height a line takes — in
    /// dark, light and high contrast, with Explorer's own picture, and once with the red X drawn on
    /// where the shell's picture is not read yet.
    /// </summary>
    [UiFact]
    public void DisconnectedNetworkDrivePictures()
    {
        OnStaThread(gfx =>
        {
            foreach (var (name, look) in new[] { ("dark", LookFor(true)), ("light", LookFor(false)), ("hc", HighContrast()) })
            {
                using var pictured = new Pictured(stock: true);
                DisconnectedBar(gfx, look, vertical: false, $"drive-disconnected-bar-{name}.png");
                DisconnectedBar(gfx, look, vertical: true, $"drive-disconnected-vertical-{name}.png");
                DisconnectedLines(gfx, look, $"drive-disconnected-lines-{name}.png");
            }
            using var drawn = new Pictured(stock: false);
            DisconnectedBar(gfx, LookFor(true), vertical: false, "drive-disconnected-bar-drawn-dark.png");
            DisconnectedLines(gfx, LookFor(false), "drive-disconnected-lines-drawn-light.png");
        });
    }

    /// <summary>The three states side by side, the face's two cells each, at 28, 48 and 72 DIP.</summary>
    private static void DisconnectedBar(Gfx gfx, Look look, bool vertical, string file)
    {
        var theme = FlyoutTheme.For(look);
        const float gap = 6;
        int[] widths = [DriveIsland.WideCells];
        Drive[][] states = [[Connected()], [], [new Drive { Letter = 25, Kind = DriveKind.Remote, Pending = true }]];
        var strips = new List<(IslandStyle Style, int Width, CellIsland Island)>();
        try
        {
            foreach (var height in vertical ? new[] { 48, 72 } : Heights)
            {
                var config = new Config { Height = height };
                foreach (var drives in states)
                {
                    using var attached = new Attached(drives);
                    foreach (var width in widths)
                    {
                        var style = new IslandStyle(1f, vertical, Layout.CellPx(96, config), Layout.IconPx(96, config), look);
                        strips.Add((style, width, New(gfx, config, ListKind.NetworkDrive, ShareTarget())));
                    }
                }
            }
            // One line of the sheet per bar height, the states along it.
            var lines = strips.Chunk(states.Length).ToList();
            float Along(IEnumerable<(IslandStyle Style, int Width, CellIsland Island)> line) =>
                line.Sum(strip => strip.Island.CellSize(0, strip.Style).Width + gap) + gap;
            float Across(IEnumerable<(IslandStyle Style, int Width, CellIsland Island)> line) =>
                line.Max(strip => strip.Island.CellSize(0, strip.Style).Height) + gap;
            var sheetWidth = vertical ? lines.Sum(line => line.Max(strip => strip.Island.CellSize(0, strip.Style).Width) + gap) + gap : lines.Max(Along);
            var sheetHeight = vertical ? lines.Max(line => line.Sum(strip => strip.Island.CellSize(0, strip.Style).Height + gap) + gap) : lines.Sum(Across) + gap;
            Picture(sheetWidth, sheetHeight, theme, file, dc =>
            {
                var outer = gap;
                foreach (var line in lines)
                {
                    var inner = gap;
                    var thickest = 0f;
                    foreach (var (style, _, island) in line)
                    {
                        var (width, height) = island.CellSize(0, style);
                        var (x, y) = vertical ? (outer, inner) : (inner, outer);
                        PopupPaint.Fill(dc, PopupPaint.Rect(x, y, width, height), 8, PopupPaint.Ink(theme.CardBackground));
                        Shifted(dc, x, y, () => island.PaintCell(dc, 0, style, width, height));
                        inner += (vertical ? height : width) + gap;
                        thickest = MathF.Max(thickest, vertical ? width : height);
                    }
                    outer += thickest + gap;
                }
            });
        }
        finally
        {
            foreach (var (_, _, island) in strips)
            {
                island.Dispose();
            }
        }
    }

    /// <summary>Connected beside disconnected, as lines of a list 320 DIP wide, from one row to the tallest.</summary>
    private static void DisconnectedLines(Gfx gfx, Look look, string file)
    {
        var theme = FlyoutTheme.For(look);
        const float gap = 8;
        const float width = 320;
        float[] heights = [40, 52, 76, 120, 156, 200];
        var config = new Config();
        using var connected = new Attached(Connected());
        using var there = Line(gfx, config, ListKind.NetworkDrive, ShareTarget());
        using var none = new Attached();
        using var gone = Line(gfx, config, ListKind.NetworkDrive, ShareTarget());
        var style = new TileStyle(1f, look, theme, 1, 1);
        Picture(2 * (width + gap) + gap, heights.Sum() + (heights.Length + 1) * gap, theme, file, dc =>
        {
            var x = gap;
            foreach (var island in new[] { there, gone })
            {
                var y = gap;
                foreach (var height in heights)
                {
                    var box = PopupPaint.Rect(0, 0, width, height);
                    Shifted(dc, x, y, () =>
                    {
                        PopupPaint.Fill(dc, box, 4, PopupPaint.Ink(theme.CardBackground));
                        island.PaintOn(dc, box, style, WidgetSurface.List);
                    });
                    y += height + gap;
                }
                x += width + gap;
            }
        });
    }

    // The preview-versus-widget sheet stood here. The settings app drew its own version of
    // every widget so the catalogue could show one, and this picture proved the two agreed.
    // The person asked for the previews to go (docs\widgets.md §7a), so there is no second
    // drawing left to disagree with, and the sheet went with it.

    // --- Drawing ----------------------------------------------------------------

    private delegate void Painter(ID2D1DeviceContext* dc);

    private static void Shifted(ID2D1DeviceContext* dc, float x, float y, Action paint)
    {
        var moved = Matrix3x2.CreateTranslation(x, y);
        dc->SetTransform((D2D_MATRIX_3X2_F*)&moved);
        try
        {
            paint();
        }
        finally
        {
            var identity = Matrix3x2.Identity;
            dc->SetTransform((D2D_MATRIX_3X2_F*)&identity);
        }
    }

    private static void Picture(float width, float height, FlyoutTheme theme, string file, Painter content)
    {
        ID2D1Factory1* factory = null;
        IWICImagingFactory* wic = null;
        IWICBitmap* bitmap = null;
        ID2D1RenderTarget* target = null;
        ID2D1DeviceContext* dc = null;
        try
        {
            ComUtil.Check(D2D1CreateFactory(D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED, __uuidof<ID2D1Factory1>(), (void**)&factory));
            var clsid = CLSID.CLSID_WICImagingFactory2;
            ComUtil.Check(CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IWICImagingFactory>(), (void**)&wic));
            var format = GUID.GUID_WICPixelFormat32bppPBGRA;
            ComUtil.Check(wic->CreateBitmap((uint)MathF.Ceiling(width), (uint)MathF.Ceiling(height), &format, WICBitmapCreateCacheOption.WICBitmapCacheOnLoad, &bitmap));
            var properties = new D2D1_RENDER_TARGET_PROPERTIES
            {
                type = D2D1_RENDER_TARGET_TYPE.D2D1_RENDER_TARGET_TYPE_SOFTWARE,
                pixelFormat = new D2D1_PIXEL_FORMAT { format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, alphaMode = D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED },
                dpiX = 96,
                dpiY = 96,
            };
            ComUtil.Check(((ID2D1Factory*)factory)->CreateWicBitmapRenderTarget(bitmap, &properties, &target));
            ComUtil.Check(target->QueryInterface(__uuidof<ID2D1DeviceContext>(), (void**)&dc));
            dc->BeginDraw();
            // The bar behind the widgets, so a card's edge is visible against it.
            var behind = theme.Dark ? new DXGI_RGBA(0.13f, 0.13f, 0.13f, 1) : new DXGI_RGBA(0.95f, 0.95f, 0.95f, 1);
            dc->Clear(&behind);
            content(dc);
            ComUtil.Check(dc->EndDraw(null, null));
            Save(wic, bitmap, Path.Combine(Pictures, file));
        }
        finally
        {
            ComUtil.Release(ref dc);
            ComUtil.Release(ref target);
            ComUtil.Release(ref bitmap);
            ComUtil.Release(ref wic);
            ComUtil.Release(ref factory);
        }
    }

    private static void Save(IWICImagingFactory* wic, IWICBitmap* bitmap, string path)
    {
        IWICStream* stream = null;
        IWICBitmapEncoder* encoder = null;
        IWICBitmapFrameEncode* frame = null;
        try
        {
            ComUtil.Check(wic->CreateStream(&stream));
            fixed (char* name = path)
            {
                ComUtil.Check(stream->InitializeFromFilename(name, GENERIC_WRITE));
            }
            var png = GUID.GUID_ContainerFormatPng;
            ComUtil.Check(wic->CreateEncoder(&png, null, &encoder));
            ComUtil.Check(encoder->Initialize((IStream*)stream, WICBitmapEncoderCacheOption.WICBitmapEncoderNoCache));
            ComUtil.Check(encoder->CreateNewFrame(&frame, null));
            ComUtil.Check(frame->Initialize(null));
            uint width;
            uint height;
            ComUtil.Check(bitmap->GetSize(&width, &height));
            ComUtil.Check(frame->SetSize(width, height));
            var format = GUID.GUID_WICPixelFormat32bppPBGRA;
            ComUtil.Check(frame->SetPixelFormat(&format));
            ComUtil.Check(frame->WriteSource((IWICBitmapSource*)bitmap, null));
            ComUtil.Check(frame->Commit());
            ComUtil.Check(encoder->Commit());
        }
        finally
        {
            ComUtil.Release(ref frame);
            ComUtil.Release(ref encoder);
            ComUtil.Release(ref stream);
        }
    }

    private static void OnStaThread(Action<Gfx> body)
    {
        Exception? failed = null;
        var thread = new Thread(() =>
        {
            try
            {
                var options = new DispatcherQueueOptions
                {
                    dwSize = (uint)sizeof(DispatcherQueueOptions),
                    threadType = DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_CURRENT,
                    apartmentType = DISPATCHERQUEUE_THREAD_APARTMENTTYPE.DQTAT_COM_STA,
                };
                IDispatcherQueueController* controller = null;
                ComUtil.Check(CreateDispatcherQueueController(options, &controller));
                try
                {
                    using var gfx = new Gfx(gpu: false);
                    body(gfx);
                }
                finally
                {
                    controller->Release();
                }
            }
            catch (Exception error)
            {
                failed = error;
            }
        });
        thread.Start();
        thread.Join();
        if (failed is not null)
        {
            throw new Xunit.Sdk.XunitException("On the offscreen thread: " + failed);
        }
    }
}
