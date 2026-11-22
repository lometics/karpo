// The input indicator on the taskbar (Karpo.Dock\Islands\KeyboardIsland.cs): one cell with the
// language over the region as Windows' own indicator sets them ("ENG" over "CH") where two lines
// fit the cell, and the language alone where they do not (the person, 2026-10-08); its codes at
// the bar's own text size, square where they fit a square and wider where they do not (2026-10-09).
// It is the keyboard preset's face since 2026-10-11, measured and drawn as the entry was: what is
// held here is the folder wearing it.
//
// Held on real layouts from the thinnest bar to a tall one, at 100 % and 150 %:
//
// - the entry is one cell, its codes at the clock's size, as long as the codes it shows and its
//   ends need and never shorter than square (docs\widgets.md §244);
// - on a bar whose cell has the height for two lines, two lines of text, the one under the other,
//   each centred across the cell and inside it; one line where it has not, and at 28 DIP it has not;
// - a layout without a region is its language alone, in the same cell.
//
// The sheets (keyboard-square-<dpi>-<look>.png, and a .txt of their numbers) go to KARPO_PNG_DIR
// before anything is asserted.

using System.Globalization;
using System.Text;
using Karpo.Dock;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Islands;

public sealed unsafe class KeyboardSquareTests
{
    /// <summary>The bar heights held: the thinnest, two where two lines start to fit, the taskbar's own, tall ones.</summary>
    private static readonly int[] Heights = [28, 32, 36, 40, 48, 64, 72];

    /// <summary>What the indicator is shown: a layout with a region, one without, and another language.</summary>
    private static readonly (string Language, string Region)[] Labels = [("ENG", "CH"), ("ENG", ""), ("DEU", "DE")];

    private static readonly InputHardware Desktop = new(Pen: false, Touch: false, Slate: false);

    /// <summary>One indicator laid out on a bar of <paramref name="height"/> DIP, the dock's way, showing <paramref name="label"/>.</summary>
    private static (FolderIsland Island, Layout Layout) Lay(Gfx gfx, uint dpi, int height, Look look, (string, string) label)
    {
        var config = new Config { Height = height };
        var item = Presets.Folder(Presets.Find(Presets.KeyboardId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
        item.Id = "k";
        var key = Config.IslandKey(null, item.Id);
        var island = new FolderIsland(key, item, gfx, config);
        // Shown after the first refresh, which reads the layout this PC has in use.
        _ = island.Refresh(gfx);
        island.Keyboard!.Show(label);
        var area = new RECT { left = 0, top = 0, right = 3840, bottom = 2160 };
        var content = new Content { Islands = new IslandCollection { [key] = island } };
        var layout = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, [PartSpec.For(key, item, Desktop)], content);
        return (island, layout);
    }

    /// <summary>The boxes of the lines of text the cell draws, in the cell's own px.</summary>
    private static List<D2D_RECT_F> TextOf(FolderIsland island, in IslandStyle style, float width, float height)
    {
        var boxes = new List<D2D_RECT_F>();
        var shown = style;
        PaintWatch.Watching = (box, what, color) =>
        {
            if (what == Painted.Text && box.bottom > box.top)
            {
                boxes.Add(box);
            }
            return color;
        };
        try
        {
            Bitmap(width, height, dc => island.PaintCell(dc, 0, shown, width, height), _ => { });
        }
        finally
        {
            PaintWatch.Watching = null;
        }
        return [.. boxes.OrderBy(box => box.top)];
    }

    /// <summary>
    /// One cell, its codes at the bar's own text size (the clock's, Fit.Primary) and never smaller,
    /// as long along the bar as that text and its ends need and never shorter than square — so
    /// square on the taskbar's 48 DIP, wider on a thin bar (the person, 2026-10-09) — at every
    /// height and scale, for the codes of the layout in use (docs\widgets.md §244).
    /// </summary>
    [UiFact]
    public void TheIndicatorIsItsTextAtTheBarsSizeAndNeverLessThanSquare()
    {
        var failures = new List<string>();
        var wider = new HashSet<int>();
        OnStaThread(gfx =>
        {
            var look = Karpo.Tests.Edit.AddListPictures.LookFor(true);
            foreach (var height in Heights)
            {
                foreach (var dpi in (uint[])[96, 144])
                {
                    foreach (var label in Labels)
                    {
                        var (island, layout) = Lay(gfx, dpi, height, look, label);
                        using (island)
                        {
                            var style = layout.StyleFor(0, look);
                            var fit = new Fit(style);
                            // The codes it shows now (docs\widgets.md §244), not the widest installed.
                            var (text, px, _) = KeyboardIsland.Room(gfx, style, withRegion: true, [label]);
                            var along = layout.BlockRect(0).Width;
                            var cell = layout.ItemRect(Hit.Cell(0, 0));
                            var need = MathF.Max(layout.CellSide, text + (2 * fit.Edge));
                            var where = $"{height} DIP, {dpi} DPI, {label}";
                            if (island.CellCount != 1 || MathF.Abs(along - need) > 1f || MathF.Abs(cell.Height - layout.CellSide) > 0.5f)
                            {
                                failures.Add($"{where}: {island.CellCount} cells, {along:0} px along for {need:0} px of text and ends, drawn {cell.Width:0} by {cell.Height:0} on a {layout.CellSide:0} px cell");
                            }
                            if (along < layout.CellSide - 0.5f)
                            {
                                failures.Add($"{where}: {along:0} px along, under the {layout.CellSide:0} px square");
                            }
                            if (MathF.Abs(px - KeyboardIsland.CodePx(fit)) > 0.01f || px > fit.Primary)
                            {
                                failures.Add($"{where}: its codes at {px:0.#} px, not a step under the bar's {fit.Primary:0.#} px");
                            }
                            if (along > layout.CellSide + 0.5f && label == Labels[0] && dpi == 96)
                            {
                                wider.Add(height);
                            }
                        }
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
        // The case the rule is for: a thin bar's square is too small for the codes at the bar's size.
        Assert.Contains(28, wider);
    }

    /// <summary>
    /// The room is the widest code the layouts one can switch to show, whichever is showing: a PC
    /// with German beside English keeps DEU's room while it shows ENG, and one more layout whose
    /// codes are narrower changes nothing. With nothing installed to go by, three of the widest letter.
    /// </summary>
    [UiFact]
    public void TheRoomIsTheWidestInstalledCode()
    {
        OnStaThread(gfx =>
        {
            var look = Karpo.Tests.Edit.AddListPictures.LookFor(true);
            foreach (var height in (int[])[28, 48])
            {
                var (island, layout) = Lay(gfx, 96, height, look, Labels[0]);
                using (island)
                {
                    var style = layout.StyleFor(0, look);
                    var px = KeyboardIsland.CodePx(new Fit(style));
                    if (height == 48)
                    {
                        // On the taskbar's own bar the codes are 11 DIP, under the clock's 12.5 (2026-10-11).
                        Assert.Equal(KeyboardIsland.CodeDips, px);
                    }
                    float Room(params (string, string)[] codes) => KeyboardIsland.Room(gfx, style, true, codes).Width;
                    float Width(string text) => TextLine.Template(gfx, text, px, Weight.Regular).Width;
                    var two = KeyboardIsland.Room(gfx, style, true, [("ENG", "US")]).TwoLines;

                    Assert.Equal(MathF.Max(Width("ENG"), two ? Width("US") : 0f), Room(("ENG", "US")), 0.01f);
                    Assert.Equal(MathF.Max(Width("ENG"), Width("DEU")), Room(("ENG", "CH"), ("DEU", "DE")), 0.01f);
                    Assert.Equal(Room(("ENG", "CH"), ("DEU", "DE")), Room(("DEU", "DE"), ("ENG", "CH"), ("ITA", "IT")), 0.01f);
                    Assert.Equal(Width(KeyboardIsland.Widest), Room(), 0.01f);
                }
            }
        });
    }

    /// <summary>
    /// Two lines where the cell has the height for them — the language over the region, each
    /// centred across the cell and inside it — and one where it has not: at 28 DIP one, at the
    /// taskbar's 48 two. A layout with no region is its language alone.
    /// </summary>
    [UiFact]
    public void TheLanguageStandsOverTheRegionWhereTwoLinesFit()
    {
        var failures = new List<string>();
        var twos = new HashSet<int>();
        var ones = new HashSet<int>();
        OnStaThread(gfx =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var dpi in (uint[])[96, 144])
                {
                    var look = Karpo.Tests.Edit.AddListPictures.LookFor(dark);
                    var sheet = new List<(int Height, (string, string) Label, FolderIsland Island, IslandStyle Style, float Width, float Height2)>();
                    var text = new StringBuilder();
                    text.AppendLine(CultureInfo.InvariantCulture, $"The input indicator: {dpi} DPI, {(dark ? "dark" : "light")}. px in the cell.");
                    text.AppendLine("height  label    cell     lines  boxes");
                    try
                    {
                        foreach (var height in Heights)
                        {
                            foreach (var label in Labels)
                            {
                                var (island, layout) = Lay(gfx, dpi, height, look, label);
                                var style = layout.StyleFor(0, look);
                                var (width, tall) = island.CellSize(0, style);
                                sheet.Add((height, label, island, style, width, tall));
                                var two = KeyboardIsland.Room(gfx, style, withRegion: true).TwoLines;
                                var boxes = TextOf(island, style, width, tall);
                                var where = $"{height} DIP, {dpi} DPI, {(dark ? "dark" : "light")}, {label}";
                                text.AppendLine(CultureInfo.InvariantCulture,
                                    $"{height,4}    {label.Item1} {label.Item2,-3}  {width,3:0}x{tall,-3:0}  {(two ? 2 : 1)}      {string.Join(" ", boxes.Select(box => $"[{box.left:0},{box.top:0}-{box.right:0},{box.bottom:0}]"))}");
                                if (dark && label.Item2.Length > 0)
                                {
                                    (two ? twos : ones).Add(height);
                                }
                                var lines = two && label.Item2.Length > 0 ? 2 : 1;
                                if (boxes.Count != lines)
                                {
                                    failures.Add($"{where}: {boxes.Count} lines of text, and the cell has room for {lines}");
                                    continue;
                                }
                                if (lines == 2 && boxes[1].top < boxes[0].bottom - 0.5f)
                                {
                                    failures.Add($"{where}: the region at y {boxes[1].top:0} is not under the language, which ends at {boxes[0].bottom:0}");
                                }
                                // The block, one line or two, in the middle of the cell's height.
                                var block = (boxes[0].top + boxes[^1].bottom) / 2f;
                                if (MathF.Abs(block - (tall / 2f)) > 1f)
                                {
                                    failures.Add($"{where}: the text stands {block - (tall / 2f):+0.#;-0.#} px off the cell's middle");
                                }
                                foreach (var box in boxes)
                                {
                                    var middle = (box.left + box.right) / 2f;
                                    if (box.left < -0.5f || box.right > width + 0.5f || box.top < -0.5f || box.bottom > tall + 0.5f)
                                    {
                                        failures.Add($"{where}: a line at {box.left:0},{box.top:0}-{box.right:0},{box.bottom:0} leaves the {width:0} by {tall:0} cell");
                                    }
                                    else if (MathF.Abs(middle - (width / 2f)) > 1.5f)
                                    {
                                        failures.Add($"{where}: a line stands {middle - (width / 2f):+0;-0} px off the cell's middle");
                                    }
                                }
                            }
                        }
                        var name = $"keyboard-square-{dpi}-{(dark ? "dark" : "light")}";
                        File.WriteAllText(Path.Combine(Pictures, name + ".txt"), text.ToString());
                        Picture(look, sheet.Select(item => (item.Island, item.Style, item.Width, item.Height2)).ToList(), Path.Combine(Pictures, name + ".png"));
                    }
                    finally
                    {
                        foreach (var item in sheet)
                        {
                            item.Island.Dispose();
                        }
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
        Assert.Contains(48, twos);
        Assert.Contains(28, ones);
    }

    /// <summary>
    /// The sheet: a row per bar height, the three layouts side by side, each cell on the bar's own
    /// colour with its square outlined so its shape can be seen.
    /// </summary>
    private static void Picture(Look look, List<(FolderIsland Island, IslandStyle Style, float Width, float Height)> cells, string path)
    {
        const float pad = 12f;
        var per = Labels.Length;
        var rows = cells.Count / per;
        var width = pad + (per * (cells.Max(cell => cell.Width) + pad));
        var height = pad + Enumerable.Range(0, rows).Sum(row => cells[row * per].Height + pad);
        var bar = look.Dark ? new DXGI_RGBA(0.12f, 0.12f, 0.12f, 1f) : new DXGI_RGBA(0.93f, 0.93f, 0.93f, 1f);
        var edge = look.Dark ? new DXGI_RGBA(0.35f, 0.35f, 0.35f, 1f) : new DXGI_RGBA(0.7f, 0.7f, 0.7f, 1f);
        Bitmap(width, height, dc =>
        {
            ID2D1SolidColorBrush* fill = null;
            ID2D1SolidColorBrush* line = null;
            try
            {
                fill = Gfx.Brush(dc, bar);
                line = Gfx.Brush(dc, edge);
                var all = new D2D_RECT_F(0, 0, width, height);
                dc->FillRectangle(&all, (ID2D1Brush*)fill);
                var y = pad;
                for (var row = 0; row < rows; row++)
                {
                    var x = pad;
                    for (var at = 0; at < per; at++)
                    {
                        var (island, style, w, h) = cells[(row * per) + at];
                        var box = new D2D_RECT_F(x - 0.5f, y - 0.5f, x + w + 0.5f, y + h + 0.5f);
                        dc->DrawRectangle(&box, (ID2D1Brush*)line, 1f, null);
                        var shown = style;
                        Shifted(dc, x, y, () => island.PaintCell(dc, 0, shown, w, h));
                        x += w + pad;
                    }
                    y += cells[row * per].Height + pad;
                }
            }
            finally
            {
                ComUtil.Release(ref line);
                ComUtil.Release(ref fill);
            }
        }, bitmap => Save(bitmap, path));
    }
}
