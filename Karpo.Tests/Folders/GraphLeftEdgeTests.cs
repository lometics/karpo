// A folder line's graph runs to the line's edges, under the folder's rim (docs\widgets.md §9, "One
// gutter", and §185). The person, 2026-10-16: "the graphs in the folder view dont go all the way to
// the left side anymore." Since the folder wore Windows' taskbar flyout surface (674affd8) its rim
// lies outside the material, and every card was clipped inside the rim, the graphs with it: a
// graph stopped a rim's width short of the panel's edge, the rim showing what is behind the folder
// between them. A graph runs under the rim again, the rim over it, as before; the actions line's
// footer band still stops inside it, as Windows' footer does.
//
// Held on the folder as it opens, captured as the compositor draws it (Edit\WindowShots), at 100 %
// and 150 %: the panel's outermost column on a graph's row is the graph's accent under the rim, not
// the rim's grey over the black behind the window; on the footer band's row it is the rim over that
// black, no brighter. The readings are fed, the shares stand-ins: nothing reads this PC or the
// network.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1. The pictures go to KARPO_PNG_DIR as
// graph-edge-<folder>-<100|150>.png.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using Karpo.Tests.Perf;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Folders;

[Collection(SamplerState.Collection)]
public sealed unsafe class GraphLeftEdgeTests
{
    private static readonly PresetMachine Machine = new(HasBattery: false, FixedDrives: []);

    /// <summary>The folders held: System's five readings, the five in one card, and the shares.</summary>
    private static readonly string[] Folders = ["system", "oneCard", "stocks"];

    /// <summary>The taskbar flyout's rim, #757575 at 40 %, over black: the brightest the footer's outermost column may be.</summary>
    private const int RimOverBlack = 0x2F;

    /// <summary>The folder <paramref name="name"/> stands for, as the add list makes it.</summary>
    private static BarItem Holder(string name)
    {
        var holder = Presets.Folder(Presets.Find(name == "stocks" ? Presets.MarketId : "systemGroup")!, Machine);
        if (name == "oneCard")
        {
            // The readings one under another in one card, no divider between them, at two rows each
            // (the person's own quick settings folder): a line's left edge is no card's corner.
            holder.Folder!.Lists = [[.. holder.Folder.Lists.SelectMany(list => list).Where(cell => !FolderFlyout.Divides(cell))]];
            foreach (var cell in holder.Folder.Cells)
            {
                if (cell.Island is { Performance: not null } widget)
                {
                    widget.Height = new LineHeight(true, 2);
                }
            }
        }
        return holder;
    }

    [UiFact]
    public void AGraphRunsUnderTheRimAndTheFooterStopsInsideIt()
    {
        var failures = new List<string>();
        var graphs = 0;
        var footers = 0;
        WindowShots.OnHiddenDesktop("graph-edge", (gfx, instance) =>
        {
            // The shares' prices kept, on the thread that makes the folders (MarketIsland.Made is per thread).
            MarketIsland.Made = MarketOffscreenTests.Settled(new()
            {
                ["MSFT"] = MarketOffscreenTests.Answer("MSFT", "Microsoft Corporation", price: 412.34, percent: 1.24, previous: 407.3),
                ["AAPL"] = MarketOffscreenTests.Answer(),
                ["NVDA"] = MarketOffscreenTests.Answer("NVDA", "NVIDIA Corporation", price: 181.2, percent: 2.1, previous: 177.47),
                ["^GSPC"] = MarketOffscreenTests.Answer("^GSPC", "S&P 500", price: 6417.29, percent: 0.02, previous: 6416.02),
                ["MSFT 5y"] = MarketRangeTests.Ranged("MSFT", 261, 7 * 86400, 289, 412.34, 289.1, wander: 40),
                ["AAPL 5y"] = MarketRangeTests.Ranged("AAPL", 261, 7 * 86400, 142, 336.16, 140.2, wander: 22, name: "Apple Inc."),
                ["NVDA 5y"] = MarketRangeTests.Ranged("NVDA", 261, 7 * 86400, 14.5, 181.2, 14.1, wander: 9, name: "NVIDIA Corporation"),
                ["^GSPC 5y"] = MarketRangeTests.Ranged("^GSPC", 261, 7 * 86400, 3270, 6417.29, 3240.5, wander: 260, name: "S&P 500"),
            });
            try
            {
                foreach (var name in Folders)
                {
                    foreach (var scale in new[] { 1f, 1.5f })
                    {
                        var (g, f) = Held(gfx, instance, name, scale, failures);
                        graphs += g;
                        footers += f;
                    }
                }
            }
            finally
            {
                MarketIsland.Made = null;
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        // Five readings in two folders and four shares, at two scales; System's footer in both.
        Assert.Equal(2 * ((2 * 5) + 4), graphs);
        Assert.Equal(2 * 2, footers);
    }

    /// <summary>
    /// The folder <paramref name="name"/> opened at <paramref name="scale"/>, each graph's and the
    /// footer's outermost columns read; how many of each were.
    /// </summary>
    private static (int Graphs, int Footers) Held(Gfx gfx, HINSTANCE instance, string name, float scale, List<string> failures)
    {
        var holder = Holder(name);
        var look = AddListPictures.LookFor(dark: true);
        var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
        var anchor = new POINT { x = work.right / 2, y = work.bottom };
        var folder = new FolderIsland($"layout:default/{holder.Id}", holder, gfx, new Config());
        foreach (var tile in folder.Tiles)
        {
            if (tile.Island is PerformanceIsland line)
            {
                // A full history at the top of its scale: the graph's area fills the line under its words.
                for (var at = 0; at < 600; at++)
                {
                    line.Feed(reading: 1f, down: 1024 * 1024, up: 1024 * 1024, read: 1024 * 1024, written: 1024 * 1024);
                }
            }
        }
        var graphs = 0;
        var footers = 0;
        using var flyout = new FolderFlyout(gfx, instance, 0);
        try
        {
            flyout.Show(gfx, look, scale, anchor, work, Edge.Bottom, folder);
            Assert.True(flyout.IsOpen, $"{name} did not open");
            WindowShots.Settle(300);
            RECT window;
            window = WindowShots.Extent(flyout.Hwnd);
            var panel = flyout.PanelOnScreen;
            var (dx, dy) = (panel.left - window.left, panel.top - window.top);
            var plan = flyout.Plan!;
            var file = $"graph-edge-{name}-{(int)(scale * 100)}.png";
            Assert.True(WindowShots.Save(flyout.Hwnd, file, default, (pixels, width) =>
            {
                for (var index = 0; index < plan.Cards.Count && index < folder.Tiles.Count; index++)
                {
                    var island = folder.Tiles[index].Island;
                    var graph = island is PerformanceIsland { Info.Facts: false } or MarketIsland;
                    var footer = island.Info.Id == WidgetInfos.Actions.Id;
                    if (!graph && !footer)
                    {
                        continue;
                    }
                    graphs += graph ? 1 : 0;
                    footers += footer ? 1 : 0;
                    var card = plan.Cards[index];
                    var left = dx + (int)MathF.Round(card.left);
                    var right = dx + (int)MathF.Round(card.right) - 1;
                    // A share's chart starts at the left edge; its day ends where the session stands.
                    int[] edges = island is MarketIsland ? [left] : [left, right];
                    // The graph's rows: the line's lower half, where every graph's area is (outside the
                    // card's rounded corner, black, which no graph reaches); the footer's middle row.
                    var bottom = dy + (int)card.bottom;
                    var top = dy + (int)((card.top + card.bottom) / 2);
                    int[] rows = footer ? [top] : [.. Enumerable.Range(top, Math.Max(1, bottom - top))];
                    foreach (var x in edges)
                    {
                        (byte R, byte G, byte B) Ink(int y)
                        {
                            var at = ((y * width) + x) * 4;
                            return (pixels[at + 2], pixels[at + 1], pixels[at]);
                        }
                        // The rim over the black behind the window is a grey; the accent under it is not.
                        static bool Grey((byte R, byte G, byte B) c) => Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B)) <= 2;
                        var said = $"{file}: {island.Info.Id} at x {x - dx}, y {rows[0] - dy}..{rows[^1] - dy}";
                        if (graph && rows.All(y => Grey(Ink(y))))
                        {
                            failures.Add($"{said}: the rim over what is behind on every row, the graph stopping short of the edge");
                        }
                        if (footer && rows.Any(y => Ink(y) is var c && Math.Max(c.R, Math.Max(c.G, c.B)) > RimOverBlack + 2))
                        {
                            failures.Add($"{said}: brighter than the rim over black, the footer band running under the rim");
                        }
                    }
                }
            }), $"{file} came back blank");
        }
        finally
        {
            flyout.Hide();
            folder.Dispose();
        }
        return (graphs, footers);
    }
}
