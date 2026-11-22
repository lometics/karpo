// The drive face's row as the bar draws it — the drive entry's, until it went into the drive preset
// (docs\widgets.md §99): the icon, the name, and the capacity bar running from one gap after the name
// to the face's end.
//
// The user, of a 36 DIP bar at 150 %: "the disk progress bar should scale to take up the remaining
// space". The row put a short bar of a fixed length at the far end and the name after the icon, so a
// widget given more than two cells showed "Local Disk (C:)", then an empty stretch, then a bar about
// 20 px long.
//
// Held on real layouts at 28, 36, 48 and 72 DIP, at 100 % and 150 %, drawn as the scene draws the
// widget and measured from its pixels: with no name the bar starts one gap after the icon, with one it
// starts one gap after the name as drawn, and either way it ends at the widget's end less the air every
// cell keeps there, centred on the row's middle line and never shorter than its least length. A
// letter, the user's "Local Disk (C:)", a long name and a network drive; one cell, two, five and eight.
// And the drive filling up moves nothing: the name, the track and its ends are where they were.
//
// The drives are made up (DriveWatch.Source), which is why this is in the attached drives'
// collection. The sheets and their numbers (drive-row-<dpi>-<look>.png and .txt) go to KARPO_PNG_DIR
// before anything is asserted.

using System.Globalization;
using System.Text;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Tests.Islands;
using Karpo.Tests.Sizing;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Storage;

[Collection(AttachedDrivesCollection.Collection)]
public sealed unsafe class DriveRowTests
{
    private static readonly int[] Heights = [28, 36, 48, 72];

    private const string SystemVolume = @"\\?\Volume{11111111-1111-1111-1111-111111111111}\";

    private const string ShieldVolume = @"\\?\Volume{44444444-4444-4444-4444-444444444444}\";

    private const string Share = @"\\server\work";

    /// <summary>What text is drawn in when its ink is looked for: a magenta the widget never uses.</summary>
    private static readonly DXGI_RGBA TextMark = new(1f, 0f, 1f, 1f);

    private static ulong Gb(double gigabytes) => (ulong)(gigabytes * 1024 * 1024 * 1024);

    private static Drive Disk(int letter, string label, double total, double free, DriveKind kind = DriveKind.Fixed, string volume = SystemVolume) =>
        new() { Letter = letter, Kind = kind, Volume = volume, Label = label, Total = Gb(total), Free = Gb(free) };

    /// <summary>
    /// One face drawn on the sheet: what it is called, the kind of its line, the drive it names and
    /// shows, and whether it says the drive's name. A face is two cells, as the entry was.
    /// </summary>
    private sealed record Case(string Name, ListKind Kind, DriveTargetOptions Target, Drive Shown, bool ShowName = true);

    private static DriveTargetOptions Target(string volume, string letter, string label) =>
        new() { Volume = volume, Letter = letter, Label = label };

    private static readonly Case[] Cases =
    [
        new("letter", ListKind.Drive, Target(SystemVolume, "C:", "Local Disk"), Disk(2, "Local Disk", 931, 419), ShowName: false),
        new("Local Disk", ListKind.Drive, Target(SystemVolume, "C:", "Local Disk"), Disk(2, "Local Disk", 931, 419)),
        new("long name", ListKind.Drive, Target(ShieldVolume, "E:", "Samsung T7 Shield"), Disk(4, "Samsung T7 Shield", 1863, 120, DriveKind.Fixed, ShieldVolume)),
        new("network", ListKind.NetworkDrive, Target(Share, "Z:", @"work (\\server)"), Disk(25, @"work (\\server)", 200, 50, DriveKind.Remote, Share)),
    ];

    private static Look LookFor(bool dark) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    /// <summary>The preset's folder wearing the face, its line pointed at the case's drive.</summary>
    private static BarItem Widget(Case one)
    {
        var widget = DriveWidgetOffscreenTests.Holder(one.Kind.ToString().ToLowerInvariant(), one.Kind, one.Target);
        widget.Folder!.Face.Set(widget.Folder.Face.Info.Option(DriveFace.Name)!, one.ShowName);
        return widget;
    }

    /// <summary>The widget's island, made and read while <paramref name="drives"/> are the drives this PC has.</summary>
    private static CellIsland Make(Gfx gfx, BarItem widget, params Drive[] drives)
    {
        var before = DriveWatch.Source;
        DriveWatch.Source = (_, remote) => drives.Where(drive => remote || drive.Kind != DriveKind.Remote).ToList();
        try
        {
            if (Karpo.Dock.Islands.Create(Config.IslandKey(null, widget.Id), widget, gfx, new Config()) is not CellIsland island)
            {
                throw new Xunit.Sdk.XunitException($"{widget.Kind}: its descriptor names a widget class, and none was made.");
            }
            _ = island.Refresh(gfx);
            return island;
        }
        finally
        {
            DriveWatch.Source = before;
        }
    }

    /// <summary>A bar of <paramref name="height"/> DIP along the bottom with the one widget on it, laid out the dock's way.</summary>
    private static Layout Bar(Gfx gfx, CellIsland island, BarItem widget, uint dpi, int height, Look look)
    {
        var config = new Config { Height = height };
        var key = Config.IslandKey(null, widget.Id);
        var islands = new IslandCollection { [key] = island };
        var area = new RECT { right = 3840, bottom = 2160 };
        return new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, [GivenRoom.Part(key, widget)], new Content { Islands = islands });
    }

    /// <summary>What one widget's drawing says, in px from the start of its slot.</summary>
    private sealed record Measured(
        float Slot,
        float Edge,
        float Gap,
        float Least,
        float Letter,
        float Middle,
        D2D_RECT_F Icon,
        D2D_RECT_F? Name,
        D2D_RECT_F? Bar,
        (float Left, float Right) BarInk)
    {
        /// <summary>Where the bar should start: one gap after the name, or after the icon where there is none.</summary>
        public float Expected => (Name is { } name ? name.right : Icon.right) + Gap;

        public float Tail => Bar is { } bar ? Slot - bar.right : float.NaN;
    }

    /// <summary>
    /// Draws the widget's one cell into the slot the layout gave it, as the scene does, with its text
    /// in a mark of its own, and measures what it drew: the icon's, the name's and the bar's boxes as
    /// they were drawn, and the bar's own ink along its line.
    /// </summary>
    private static Measured Measure(Gfx gfx, CellIsland island, Layout layout, Look look)
    {
        var style = layout.StyleFor(0, look);
        var fit = new Fit(style);
        var slot = layout.ItemRect(Hit.Cell(0, 0));
        var (width, height) = island.CellSize(0, style);
        var shapes = new List<D2D_RECT_F>();
        var texts = new List<D2D_RECT_F>();
        PaintWatch.Watching = (box, what, color) =>
        {
            if (what == Painted.Shape)
            {
                shapes.Add(box);
            }
            else if (what == Painted.Text)
            {
                texts.Add(box);
                return TextMark;
            }
            return color;
        };
        var (w, h) = ((int)MathF.Ceiling(slot.Width), (int)MathF.Ceiling(slot.Height));
        var ink = new List<(int X, int Y)>();
        try
        {
            Offscreen.Bitmap(w, h, dc => island.PaintCell(dc, 0, style, width, height), bitmap => Ink(bitmap, w, h, ink));
        }
        finally
        {
            PaintWatch.Watching = null;
        }
        // The icon's box first, then the bar's (DriveIsland.PaintCell), watched in the drive's own
        // cell: the face's box and its Side round it, which starts the button's end less that Side
        // into the slot (§275).
        var shift = FolderIsland.Ends(fit) - fit.Side;
        D2D_RECT_F Slotted(D2D_RECT_F box) => box with { left = box.left + shift, right = box.right + shift };
        var icon = shapes.Count > 0 ? Slotted(shapes[0]) : default;
        D2D_RECT_F? bar = shapes.Count > 1 ? Slotted(shapes[^1]) : null;
        D2D_RECT_F? name = texts.Count > 0 ? Slotted(texts[0]) : null;
        var after = name is { } drawn ? drawn.right : icon.right;
        var (left, right) = (float.NaN, float.NaN);
        if (bar is { } line)
        {
            foreach (var (x, y) in ink.Where(point => point.X >= after && point.Y >= MathF.Floor(line.top) && point.Y < MathF.Ceiling(line.bottom)))
            {
                left = float.IsNaN(left) ? x : MathF.Min(left, x);
                right = float.IsNaN(right) ? x + 1 : MathF.Max(right, x + 1);
            }
        }
        var letter = TextLine.Template(gfx, "W:", DriveIsland.LabelFit(gfx, fit).Px, Weight.Regular).Width;
        return new Measured(slot.Width, FolderIsland.Ends(fit), fit.Px(6), DriveIsland.LeastBar(fit), letter, slot.Height / 2f, icon, name, bar, (left, right));
    }

    /// <summary>Every pixel with ink that is not the text's mark.</summary>
    private static void Ink(IWICBitmap* bitmap, int width, int height, List<(int X, int Y)> ink)
    {
        IWICBitmapLock* locked = null;
        try
        {
            var all = new WICRect { X = 0, Y = 0, Width = width, Height = height };
            ComUtil.Check(bitmap->Lock(&all, (uint)WICBitmapLockFlags.WICBitmapLockRead, &locked));
            uint stride = 0;
            ComUtil.Check(locked->GetStride(&stride));
            uint size = 0;
            byte* data = null;
            ComUtil.Check(locked->GetDataPointer(&size, &data));
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var at = data + (y * stride) + (x * 4);
                    var (b, g, r, a) = (at[0], at[1], at[2], at[3]);
                    var text = a > 64 && r * 4 >= a * 3 && b * 4 >= a * 3 && g * 5 <= a;
                    if (a > 24 && !text)
                    {
                        ink.Add((x, y));
                    }
                }
            }
        }
        finally
        {
            ComUtil.Release(ref locked);
        }
    }

    /// <summary>
    /// The row as the bar draws it on the face, at every height, at 150 % dark and light and at 100 %
    /// dark: the bar starts one gap after the name (or the icon) and ends at the face's end, by its
    /// drawn box and by its ink.
    /// </summary>
    [UiFact]
    public void TheBarRunsFromTheNameToTheWidgetsEnd()
    {
        var failures = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var (dpi, dark) in new[] { (144u, true), (144u, false), (96u, true) })
            {
                failures.AddRange(Sheet(gfx, dpi, dark));
            }
        });
        Assert.True(failures.Count == 0, $"{failures.Count} rows are off:\n{string.Join("\n", failures)}");
    }

    private static List<string> Sheet(Gfx gfx, uint dpi, bool dark)
    {
        var look = LookFor(dark);
        var theme = FlyoutTheme.For(look);
        var failures = new List<string>();
        var drawn = new List<(int Height, Case Case, CellIsland Island, Layout Layout, Measured Measured)>();
        try
        {
            foreach (var height in Heights)
            {
                foreach (var one in Cases)
                {
                    var widget = Widget(one);
                    var island = Make(gfx, widget, one.Shown);
                    var layout = Bar(gfx, island, widget, dpi, height, look);
                    drawn.Add((height, one, island, layout, Measure(gfx, island, layout, look)));
                }
            }
            var name = $"drive-row-{dpi}-{(dark ? "dark" : "light")}";
            Picture(gfx, look, theme, drawn.Select(item => (item.Height, item.Case.Name, item.Island, item.Layout)).ToList(), Path.Combine(Offscreen.Pictures, name + ".png"));
            File.WriteAllText(Path.Combine(Offscreen.Pictures, name + ".txt"), Table(dpi, dark, drawn.Select(item => (item.Height, item.Case.Name, item.Measured)).ToList()));
            foreach (var (height, one, _, _, row) in drawn)
            {
                var where = $"{dpi} DPI {(dark ? "dark" : "light")}, {height} DIP, {one.Name}";
                if (row.Bar is not { } bar)
                {
                    // Two cells always have a bar.
                    failures.Add($"{where}: no capacity bar");
                    continue;
                }
                if (MathF.Abs(bar.left - row.Expected) > 1f)
                {
                    failures.Add($"{where}: the bar starts at {bar.left:0.#} px, and one gap after the {(row.Name is null ? "icon" : "name")} is {row.Expected:0.#}");
                }
                if (MathF.Abs(row.Tail - row.Edge) > 0.5f)
                {
                    failures.Add($"{where}: {row.Tail:0.#} px after the bar, and a cell keeps {row.Edge:0.#} at its end");
                }
                if (float.IsNaN(row.BarInk.Left) || MathF.Abs(row.BarInk.Left - bar.left) > 1f || MathF.Abs(row.BarInk.Right - bar.right) > 1f)
                {
                    failures.Add($"{where}: the bar's ink runs {row.BarInk.Left:0}-{row.BarInk.Right:0}, and its box {bar.left:0.#}-{bar.right:0.#}");
                }
                if (bar.right - bar.left < MathF.Min(row.Least, row.Slot - row.Edge - row.Expected) - 0.5f)
                {
                    failures.Add($"{where}: the bar is {bar.right - bar.left:0.#} px, under its least {row.Least:0.#}");
                }
                if (MathF.Abs(((bar.top + bar.bottom) / 2f) - row.Middle) > 1f)
                {
                    failures.Add($"{where}: the bar's middle is {(bar.top + bar.bottom) / 2f:0.#} px, the row's {row.Middle:0.#}");
                }
                // A name wherever the letter fits in what the bar's least length leaves.
                var room = row.Slot - row.Edge - row.Least - row.Gap - (row.Icon.right + row.Gap);
                if (row.Name is null && room >= row.Letter)
                {
                    failures.Add($"{where}: no name, and {room:0} px were left for one where the letter needs {row.Letter:0}");
                }
            }
        }
        finally
        {
            foreach (var item in drawn)
            {
                item.Island.Dispose();
            }
        }
        return failures;
    }

    /// <summary>
    /// The drive filling up moves nothing: the name, the track and its ends, and the bar's ink are
    /// where they were at 5 %, 55 % and 97 % full; only how much of the track is filled changes.
    /// </summary>
    [UiFact]
    public void TheDriveFillingUpMovesNothing()
    {
        var failures = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            var look = LookFor(dark: true);
            var one = Cases.First(each => each.Name == "Local Disk");
            foreach (var height in Heights)
            {
                foreach (var dpi in (uint[])[96, 144])
                {
                    Measured? first = null;
                    foreach (var free in new[] { 0.95, 0.45, 0.03 })
                    {
                        var widget = Widget(one);
                        using var island = Make(gfx, widget, Disk(2, "Local Disk", 931, 931 * free));
                        var layout = Bar(gfx, island, widget, dpi, height, look);
                        var row = Measure(gfx, island, layout, look);
                        first ??= row;
                        if (row.Name?.right != first.Name?.right || row.Bar is not { } bar || first.Bar is not { } was
                            || bar.left != was.left || bar.right != was.right || row.BarInk != first.BarInk)
                        {
                            failures.Add($"{height} DIP, {dpi} DPI, {(1 - free) * 100:0} % full: the name ends at {row.Name?.right:0.#} and the bar runs {row.Bar?.left:0.#}-{row.Bar?.right:0.#}, and at {(1 - 0.95) * 100:0} % they were {first.Name?.right:0.#} and {first.Bar?.left:0.#}-{first.Bar?.right:0.#}");
                        }
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>The numbers of one sheet, one widget to a line.</summary>
    private static string Table(uint dpi, bool dark, List<(int Height, string Name, Measured Measured)> drawn)
    {
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"The drive row as the bar draws it: {dpi} DPI, {(dark ? "dark" : "light")}. px from the start of the widget's slot.");
        text.AppendLine("  name: its drawn box. bar: its drawn box, and its ink. after name: bar start less the name's end (the gap is the icon's gap). tail: slot end less bar end (a cell's edge).");
        text.AppendLine("height  widget                slot   icon       name        bar         bar ink     length  least  after name  gap  tail  edge");
        foreach (var (height, name, row) in drawn)
        {
            var named = row.Name is { } box ? $"{box.left:0}-{box.right:0}" : "none";
            var bar = row.Bar is { } line ? $"{line.left:0.#}-{line.right:0.#}" : "none";
            var length = row.Bar is { } drawnBar ? drawnBar.right - drawnBar.left : 0f;
            var after = row.Bar is { } start && row.Name is { } end ? (start.left - end.right).ToString("0.#", CultureInfo.InvariantCulture) : "-";
            text.AppendLine(CultureInfo.InvariantCulture,
                $"{height,4}    {name,-20}  {row.Slot,5:0}  {row.Icon.left,3:0}-{row.Icon.right,-5:0}  {named,-10}  {bar,-10}  {row.BarInk.Left,3:0}-{row.BarInk.Right,-6:0}  {length,6:0.#}  {row.Least,5:0}  {after,10}  {row.Gap,3:0}  {row.Tail,4:0.#}  {row.Edge,4:0}");
        }
        return text.ToString();
    }

    /// <summary>The sheet: a row per bar height, a column per widget, each on its card with its name over it.</summary>
    private static void Picture(Gfx gfx, Look look, FlyoutTheme theme, List<(int Height, string Name, CellIsland Island, Layout Layout)> drawn, string path)
    {
        const float pad = 16f;
        const float label = 18f;
        var names = Cases.Select(one => one.Name).ToArray();
        var columns = names.Select(name => drawn.Where(item => item.Name == name).Max(item => MathF.Max(item.Layout.ItemRect(Hit.Cell(0, 0)).Width, 130f))).ToArray();
        var rows = Heights.Select(height => drawn.Where(item => item.Height == height).Max(item => item.Layout.ItemRect(Hit.Cell(0, 0)).Height)).ToArray();
        var width = pad + columns.Sum(column => column + pad);
        var height = pad + rows.Sum(row => label + row + pad);
        Offscreen.Bitmap(width, height, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
            var y = pad;
            for (var r = 0; r < Heights.Length; r++)
            {
                var x = pad;
                for (var c = 0; c < names.Length; c++)
                {
                    var item = drawn.First(each => each.Height == Heights[r] && each.Name == names[c]);
                    var slot = item.Layout.ItemRect(Hit.Cell(0, 0));
                    var style = item.Layout.StyleFor(0, look);
                    var (cellWidth, cellHeight) = item.Island.CellSize(0, style);
                    PopupPaint.Text(dc, gfx, $"{Heights[r]} DIP · {names[c]}", 12f, Weight.Regular, theme.TextSecondary, x, y, columns[c]);
                    var top = y + label;
                    var card = item.Layout.CardRect(0);
                    PopupPaint.Fill(dc, PopupPaint.Rect(x + card.X - slot.X, top + card.Y - slot.Y, card.Width, card.Height), item.Layout.CardRadius, PopupPaint.Ink(theme.CardBackground));
                    var clip = PopupPaint.Rect(x, top, slot.Width, slot.Height);
                    dc->PushAxisAlignedClip(&clip, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
                    try
                    {
                        var island = item.Island;
                        Offscreen.Shifted(dc, x, top, () => island.PaintCell(dc, 0, style, cellWidth, cellHeight));
                    }
                    finally
                    {
                        dc->PopAxisAlignedClip();
                    }
                    x += columns[c] + pad;
                }
                y += label + rows[r] + pad;
            }
        }, bitmap => Offscreen.Save(bitmap, path));
    }
}
