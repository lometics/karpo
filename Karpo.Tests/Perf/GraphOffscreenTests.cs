// perf: pictures of the performance graphs, so what they look like can be
// looked at rather than argued about.
//
// Every reading here is **fed in**, never sampled (PerformanceIsland.Feed).
// The samplers are process-wide statics, so a sheet drawn from them would show
// whatever this machine happened to be doing at the moment the test ran —
// a different picture every time, and no picture at all on a machine with no
// second graphics adapter or no spare disk. A fed history is the same history
// on every machine, which is the difference between a picture and a test.
//
// The histories are a deterministic waveform rather than a flat line, because a
// flat line is exactly the graph that looks fine when the drawing is wrong: it
// hides the stroke's joins, the fill under the line, the clip at the cell's
// edge and the scale a network graph picks for itself.
//
// Assertions are on the ink — every widget drew something, and nothing drew
// outside the box it was given. The sheets are for a person.
//
// On the bar a reading is its preset's face now (docs\widgets.md §97): the glyph and the figure
// the entry was, read from the preset's line, which is what is fed.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Perf;

[Collection(SamplerState.Collection)]
public sealed unsafe class GraphOffscreenTests
{
    /// <summary>The bar heights in the person's own files: a thin bar, theirs, and a thick one.</summary>
    private static readonly int[] Heights = [28, 41, 72];

    private static readonly ListKind[] Kinds =
        [ListKind.Cpu, ListKind.Gpu, ListKind.Memory, ListKind.Disk, ListKind.Network];

    private static readonly PresetMachine Machine = new(HasBattery: false, FixedDrives: []);

    private static Look LookFor(bool dark, float textScale = 1f) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8),
        TextScale: textScale);

    /// <summary>
    /// The bar at each height, every reading's face side by side, dark and light. Each is given a
    /// history and a reading, so the sheets are the same on every machine.
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void GraphsOnTheBar()
    {
        var blank = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var height in Heights)
            foreach (var dark in new[] { true, false })
            {
                blank.AddRange(Sheet(gfx, height, dark, vertical: false, $"perf-bar-{height}-{Theme(dark)}.png"));
            }
            // Upright, where the face stacks its glyph over its reading.
            foreach (var height in Heights)
            {
                blank.AddRange(Sheet(gfx, height, dark: true, vertical: true, $"perf-vertical-{height}-dark.png"));
            }
            // The largest text Windows offers, which is where a template that only just fits stops.
            blank.AddRange(Sheet(gfx, 41, dark: true, vertical: false, "perf-bar-41-bigtext-dark.png", textScale: 2.25f));
        });
        Assert.Empty(blank);
    }

    /// <summary>
    /// The same five as lines of a list folder, one row tall and three — the height a new line of
    /// theirs starts at — at the width every panel has: a caption, the figure, and the graph under
    /// them, as tall as the line is given. Both heights are in the sheet, side by side.
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void GraphsAsLinesOfAList()
    {
        var wrong = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var scale in new[] { 1f, 1.5f })
            foreach (var dark in new[] { true, false })
            {
                wrong.AddRange(Lines(gfx, scale, dark, $"perf-lines-{(int)MathF.Round(scale * 100)}-{Theme(dark)}.png"));
            }
        });
        Assert.Empty(wrong);
    }

    /// <summary>One sheet of the bar; the names of any widgets that drew nothing at all.</summary>
    private static List<string> Sheet(Gfx gfx, int height, bool dark, bool vertical, string file, float textScale = 1f)
    {
        const uint dpi = 96;
        var config = new Config { Height = height };
        var look = LookFor(dark, textScale);
        var theme = FlyoutTheme.For(look);
        var style = new IslandStyle(dpi / 96f, vertical, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), look);
        var gap = MathF.Round(6 * style.Scale);
        var made = new List<(CellIsland Island, ListKind Kind, float Length)>();
        var blank = new List<string>();
        try
        {
            foreach (var kind in Kinds)
            {
                var island = Face(gfx, config, kind);
                made.Add((island, kind, island.Measure(style).CellLength));
            }
            var along = made.Sum(entry => entry.Length) + (gap * (made.Count + 1));
            var across = style.Cell + (2 * gap);
            var width = vertical ? across : along;
            var tall = vertical ? along : across;

            Offscreen.Bitmap(
                width,
                tall,
                dc =>
                {
                    PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, tall), 0, PopupPaint.Ink(theme.Base));
                    var at = gap;
                    foreach (var (island, _, length) in made)
                    {
                        var (cellWidth, cellHeight) = island.CellSize(0, style);
                        var x = vertical ? gap : at;
                        var y = vertical ? at : gap;
                        PopupPaint.Fill(dc, PopupPaint.Rect(x, y, cellWidth, cellHeight), MathF.Round(8 * style.Scale), PopupPaint.Ink(theme.CardBackground));
                        Offscreen.Shifted(dc, x, y, () => island.PaintCell(dc, 0, style, cellWidth, cellHeight));
                        at += length + gap;
                    }
                },
                bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, file)));

            // A widget that drew nothing at all is the failure a picture test can actually catch:
            // each one alone on a transparent ground, and its ink read back.
            foreach (var (island, kind, _) in made)
            {
                var (cellWidth, cellHeight) = island.CellSize(0, style);
                var inked = default((float Left, float Top, float Right, float Bottom));
                Offscreen.Bitmap(
                    cellWidth,
                    cellHeight,
                    dc => island.PaintCell(dc, 0, style, cellWidth, cellHeight),
                    bitmap => inked = Offscreen.InkOf(bitmap, (uint)MathF.Ceiling(cellWidth), (uint)MathF.Ceiling(cellHeight)));
                if (inked.Right <= inked.Left)
                {
                    blank.Add($"{kind} drew nothing at {height} DIP {(vertical ? "upright" : "across")} text×{textScale}");
                }
            }
        }
        finally
        {
            foreach (var (island, _, _) in made)
            {
                island.Dispose();
            }
        }
        return blank;
    }

    /// <summary>One sheet of lines; the names of any that drew nothing or drew past their line.</summary>
    private static List<string> Lines(Gfx gfx, float scale, bool dark, string file)
    {
        var config = new Config();
        var look = LookFor(dark);
        var theme = FlyoutTheme.For(look);
        var style = FolderFlyout.StyleFor(scale, look, theme);
        var across = PopupPaint.Round(FolderListWidths.MediumDip * scale);
        var gap = PopupPaint.Round(8 * scale);
        var margin = PopupPaint.Round(16 * scale);
        // One row, and the three a new line of a graph starts at (ListWidgets).
        int[] heights = [1, 3];
        float Down(int rows) => rows * LineHeight.Row * FolderFlyout.CellPx(scale, 1);
        var tallest = heights.Max(Down);
        var made = new List<PerformanceIsland>();
        var wrong = new List<string>();
        try
        {
            foreach (var kind in Kinds)
            {
                made.Add(Build(gfx, config, kind));
            }
            var width = margin + (heights.Length * (across + gap)) - gap + margin;
            var height = margin + (Kinds.Length * (tallest + gap)) - gap + margin;

            Offscreen.Bitmap(
                width,
                height,
                dc =>
                {
                    PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
                    var y = margin;
                    foreach (var island in made)
                    {
                        var x = margin;
                        foreach (var rows in heights)
                        {
                            var down = Down(rows);
                            var box = PopupPaint.Rect(0, 0, across, down);
                            Offscreen.Shifted(dc, x, y, () => island.PaintOn(dc, box, style, WidgetSurface.List));
                            x += across + gap;
                        }
                        y += tallest + gap;
                    }
                },
                bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, file)));

            // Each line alone with a margin round it, so ink past the line's own edge — which in a
            // real list is ink over the next line — shows up as a number rather than as a look.
            foreach (var rows in heights)
            {
                foreach (var island in made)
                {
                    var down = Down(rows);
                    var inked = default((float Left, float Top, float Right, float Bottom));
                    Offscreen.Bitmap(
                        across + (2 * margin),
                        down + (2 * margin),
                        dc => Offscreen.Shifted(dc, margin, margin, () => island.PaintOn(dc, PopupPaint.Rect(0, 0, across, down), style, WidgetSurface.List)),
                        bitmap => inked = Offscreen.InkOf(bitmap, (uint)MathF.Ceiling(across + (2 * margin)), (uint)MathF.Ceiling(down + (2 * margin))));
                    var what = $"{island.Info.Id} on a line of {rows} rows, scale {scale}";
                    if (inked.Right <= inked.Left)
                    {
                        wrong.Add($"{what} drew nothing at all");
                    }
                    else if (inked.Left < margin - 1 || inked.Top < margin - 1
                        || inked.Right > margin + across + 1 || inked.Bottom > margin + down + 1)
                    {
                        wrong.Add($"{what} drew past its line, over whatever is next to it");
                    }
                }
            }
        }
        finally
        {
            foreach (var island in made)
            {
                island.Dispose();
            }
        }
        return wrong;
    }

    /// <summary>
    /// A widget of <paramref name="kind"/> with a history fed into it: the same one on every
    /// machine, and never the machine's own. A waveform rather than a flat line, because a flat
    /// line is the graph that looks right when the drawing is wrong. On the bar, or as a list's
    /// line where <paramref name="line"/> says so.
    /// </summary>
    private static PerformanceIsland Build(Gfx gfx, Config config, ListKind kind)
    {
        var id = kind.ToString().ToLowerInvariant();
        var widget = kind.New();
        widget.Id = id;
        var island = (PerformanceIsland)Karpo.Dock.Islands.CreateLine(Config.IslandKey(null, widget.Id), widget, kind, gfx, config)!;
        Feed(island, kind);
        return island;
    }

    /// <summary>The reading's preset on the bar, its line fed as <see cref="Build"/>'s, which its face shows.</summary>
    private static FolderIsland Face(Gfx gfx, Config config, ListKind kind)
    {
        var holder = Presets.Folder(Presets.Find(kind switch
        {
            ListKind.Cpu => Presets.CpuId,
            ListKind.Gpu => Presets.GpuId,
            ListKind.Memory => Presets.MemoryId,
            ListKind.Disk => Presets.DiskId,
            _ => Presets.NetworkActivityId,
        })!, Machine);
        var folder = new FolderIsland(Config.IslandKey(null, holder.Id), holder, gfx, config);
        Feed((PerformanceIsland)folder.Tiles[0].Island, kind);
        return folder;
    }

    /// <summary>A waveform's history fed to <paramref name="island"/>, a phase of its own per kind.</summary>
    private static void Feed(PerformanceIsland island, ListKind kind)
    {
        // A different phase per kind, so a sheet of five does not read as one graph drawn five
        // times, and a busy end so the fill under the line has something to fill.
        var phase = Array.IndexOf(Kinds, kind) * 0.7f;
        for (var step = 0; step < 160; step++)
        {
            var wave = (MathF.Sin((step * 0.11f) + phase) + 1) / 2;
            var share = Math.Clamp((0.15f * wave) + (0.55f * wave * wave) + (step / 900f), 0f, 1f);
            if (ListWidgets.Of(kind).Info.Metric == Metric.Network)
            {
                // Bytes a second: a trickle with bursts, so the graph's own scaling shows.
                island.Feed(down: (share * 24 * 1024 * 1024) + (32 * 1024), up: share * 2 * 1024 * 1024);
            }
            else
            {
                island.Feed(share);
            }
        }
    }

    private static string Theme(bool dark) => dark ? "dark" : "light";
}
