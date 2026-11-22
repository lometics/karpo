// What lights under the pointer in a list folder (FolderFlyout.LightsWhole). The person
// (2026-10-11): "generally speaking, the widgets in the list view should not have a hover over
// animation, but all the logic subitems that have an logic/ action behind them."
//
// So a line lights as a whole only where the whole of it is the one thing a press does: a pin, a
// switch, a graph that opens Task Manager, Security, a drive, Task view, the weather's conditions and
// today's date (§379; the weather's hours and days are targets of their own, docs\widgets.md §131), the Snipping Tool, Settings, the power buttons, a markets ticker (§263) and a clock (§377). Every other line â€” a slider's, the
// media's, a timer's, a converter's, a calendar's, and every line made of rows that light
// themselves â€” keeps still, and only its buttons, rows and thumbs light, each on its own. Arranging
// the folder is apart: there a press picks the whole widget up, and every line lights.
//
// The rule is held over every kind a list holds, made the way a folder makes it; then the real
// folder on a desktop of its own, the pointer on a still point of each line, the fill each line
// wears read back off the compositor's shapes, and pictures to KARPO_PNG_DIR as
// line-hover-<dark|light>-<100|150>-<rest|clock|timer|media|play|pin>.png. Needs KARPO_UI_TESTS=1.
//
// §611 (the person, 2026-11-09: "the weather widgets like the graph or the daily forecast when hovering dont show the
// hover area properly. according to our design guidelines it should span to the top and bottom and left and right with
// no padding there"): what an item of a line lights — an hour, a day, a headline, a row — reaches the line's edges,
// top to foot and side to side, or the next item, or something drawn between them; never a plate inset by the line's
// padding. Held over every kind a list holds at every size it offers, at the height the open folder gives it (whole
// cells, a sized line's form in their middle); pictures of the forecasts' lines hovered at their first, middle and
// last item as item-hover-<kind>-<first|middle|last>-<dark|light>.png. §632 (the person,
// 2026-11-09: "the items in the hourly forecast should all have the same width"): items laid across a line are as wide
// as each other, the first and the last no wider.

using System.Reflection;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using CardFill = Karpo.Dock.FolderFlyout.CardFill;
using Color = Windows.UI.Color;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Folders;

public sealed unsafe class LineHoverRuleTests
{
    /// <summary>The kinds whose whole line is the one thing a press does: every other kind's line keeps still.</summary>
    private static readonly HashSet<ListKind> Whole =
    [
        ListKind.Cpu, ListKind.Gpu, ListKind.Memory, ListKind.Disk, ListKind.Network,
        ListKind.CpuDetails, ListKind.GpuDetails, ListKind.MemoryDetails, ListKind.DiskDetails, ListKind.NetworkDetails,
        ListKind.Pin, ListKind.Drive, ListKind.NetworkDrive, ListKind.Security,
        ListKind.TaskView, ListKind.SnippingTool, ListKind.WindowsSettings,
        ListKind.Sleep, ListKind.Hibernate, ListKind.ShutDown, ListKind.Restart, ListKind.SignOut, ListKind.Lock,
        // markets: a ticker's click searches what it tracks (§263); a converter's parts light themselves.
        ListKind.Markets,
        // clocks: a click searches the time where the clock stands (§377).
        ListKind.Clock, ListKind.TimeHeader,
        // §379: the weather's conditions and today's date: one thing to press, all of the line.
        ListKind.WeatherNow, ListKind.DateHeader,
        // §614: the air's four: a click searches the air quality at its place.
        ListKind.AirQuality, ListKind.AirPollutants, ListKind.AirPollen, ListKind.AirForecast,
        .. ListWidgets.All.Where(widget => widget.Drawing == WidgetSource.Switch).Select(widget => widget.Kind),
    ];

    /// <summary>A still line never lights, under the pointer or pressed; a whole one does, as it always has.</summary>
    [Fact]
    public void AStillLineNeverLights()
    {
        foreach (var hot in new[] { false, true })
        {
            foreach (var pressed in new[] { false, true })
            {
                Assert.Equal(CardFill.None, FolderFlyout.FillOf(hot, pressed, quiet: false, still: true));
            }
        }
        Assert.Equal(CardFill.LineHover, FolderFlyout.FillOf(hot: true, pressed: false, quiet: false, still: false));
        Assert.Equal(CardFill.LinePressed, FolderFlyout.FillOf(hot: true, pressed: true, quiet: false, still: false));
    }

    /// <summary>
    /// Every kind a list holds, made the way a folder makes it: it lights as a whole exactly where its
    /// whole line acts â€” or where it is not set up, and its whole line is its "Set up in Settings".
    /// </summary>
    [UiFact]
    public void EveryKindLightsAsAWholeOnlyWhereItsWholeLineActs()
    {
        var failures = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            MediaIsland.Stand = new MediaStand();
            try
            {
                foreach (var widget in ListWidgets.All)
                {
                    var cell = ListFormPictures.Line(widget.Kind);
                    var island = Karpo.Dock.Islands.CreateLine($"layout:default/folder/{cell.Id}", cell, widget.Kind, gfx, new Config());
                    try
                    {
                        var unset = island is CellIsland { Unset: not null };
                        var whole = FolderFlyout.LightsWhole(new FolderCell { Island = cell }, island);
                        if (whole != (unset || Whole.Contains(widget.Kind)))
                        {
                            failures.Add($"{widget.Kind} {(whole ? "lights" : "does not light")} as a whole{(unset ? " (not set up)" : "")}");
                        }
                        // What it says follows what its click does: a whole line acts.
                        if (Whole.Contains(widget.Kind) && widget.Act != CellAct.Action)
                        {
                            failures.Add($"{widget.Kind} lights as a whole, and its click does nothing");
                        }
                    }
                    finally
                    {
                        island?.Dispose();
                    }
                }
            }
            finally
            {
                MediaIsland.Stand = null;
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>A list folder: a clock, a timer, what's playing and a pin of notepad.</summary>
    private static BarItem List()
    {
        var island = BarKind.FolderList.New();
        island.Id = "lines";
        island.Folder = new FolderOptions();
        var clock = ListKind.Clock.New();
        clock.Id = "clock";
        var timer = ListKind.Timer.New();
        timer.Id = "timer";
        var media = ListKind.Media.New();
        media.Id = "media";
        media.Height = LineHeight.Exactly(4);
        var pin = BarKind.Pin.New();
        pin.Id = "pin";
        pin.Pin!.Target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe");
        foreach (var cell in new[] { clock, timer, media, pin.AsCell() })
        {
            Assert.NotNull(Karpo.Core.Folders.Add(BarKind.FolderList, island.Folder, cell));
        }
        return island;
    }

    /// <summary>
    /// The point (<paramref name="x"/>, <paramref name="y"/>) of <paramref name="flyout"/>'s panel (panel px, as its plan's cards
    /// stand) as a mouse message's lParam: in its window's client, where the panel stands in it. Not its window's extent's middle:
    /// a panel near its work area's end stands off it (its shadow's room cut on that side).
    /// </summary>
    private static nint At(FolderFlyout flyout, int x, int y)
    {
        var panel = ((FlyoutHost)typeof(FolderFlyout).GetField("host", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(flyout)!).Panel;
        var (cx, cy) = ((int)MathF.Round(panel.X) + x, (int)MathF.Round(panel.Y) + y);
        return ((cy & 0xFFFF) << 16) | (cx & 0xFFFF);
    }

    /// <summary>The colour line <paramref name="index"/> is filled with on the compositor now.</summary>
    private static Color FillOn(FolderFlyout flyout, int index)
    {
        // §464: a line's background is its wash, on a sprite of its own over its blur.
        var washes = (List<CompositionColorBrush>)typeof(FolderFlyout).GetField("washes", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(flyout)!;
        return washes[index].Color;
    }

    /// <summary>
    /// The real folder, in both themes at 100 and 150 %: the pointer on a still point of each line â€”
    /// its leading gutter, where nothing of it is drawn â€” lights the pin's line and no other; on the
    /// media's play button, the button lights and the line does not.
    /// </summary>
    [UiFact]
    public void OnTheScreenOnlyAWholeLineLights()
    {
        var failures = new List<string>();
        WindowShots.OnHiddenDesktop("line-hover", (gfx, instance) =>
        {
            var work = new RECT { left = 0, top = 0, right = 2400, bottom = 1500 };
            var anchor = new POINT { x = 1700, y = 1500 };
            using var flyout = new FolderFlyout(gfx, instance, 0);
            MediaIsland.Stand = new MediaStand();
            try
            {
                foreach (var dark in new[] { true, false })
                {
                    foreach (var scale in new[] { 1f, 1.5f })
                    {
                        var island = List();
                        var folder = new FolderIsland($"layout:default/{island.Id}", island, gfx, new Config());
                        try
                        {
                            var look = TestLooks.LookFor(dark);
                            // §464: a line lights over its own background, the folder's Windows' flyout surface.
                            var theme = FlyoutTheme.Folder(look.Flyout());
                            flyout.Show(gfx, look, scale, anchor, work, Karpo.Core.Edge.Bottom, folder, FlyoutAlign.End);
                            WindowShots.Settle(300);
                            var name = $"line-hover-{(dark ? "dark" : "light")}-{scale * 100:0}";
                            Assert.True(WindowShots.Save(flyout.Hwnd, $"{name}-rest.png"));
                            var plan = (FolderPlan)typeof(FolderFlyout).GetField("plan", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(flyout)!;
                            var ids = folder.Tiles.Select(tile => tile.Cell.Island.Id).ToList();
                            var lit = FolderFlyout.InkOf(theme, CardFill.LineHover);

                            // Across the media's line until its play button is under the pointer.
                            var mediaLine = ids.IndexOf("media");
                            var media = (MediaIsland)folder.Tiles[mediaLine].Island;
                            var at = plan.Cards[mediaLine];
                            var found = false;
                            for (var y = (int)at.top + 2; y < (int)at.bottom - 2 && !found; y += 2)
                            {
                                for (var x = (int)at.left + 2; x < (int)at.right - 2; x += 2)
                                {
                                    flyout.Message(WM.WM_MOUSEMOVE, 0, At(flyout, x, y));
                                    if (media.LineLit.Hot == 2)
                                    {
                                        // Into the button, so the picture shows it whole and lit.
                                        flyout.Message(WM.WM_MOUSEMOVE, 0, At(flyout, x + 4, y + 4));
                                        found = media.LineLit.Hot == 2;
                                        break;
                                    }
                                }
                            }
                            if (!found)
                            {
                                failures.Add($"{name}: no play button found on the media's line");
                            }
                            else
                            {
                                WindowShots.Settle(1500);
                                Assert.True(WindowShots.Save(flyout.Hwnd, $"{name}-play.png"));
                                if (FillOn(flyout, mediaLine) == lit)
                                {
                                    failures.Add($"{name}: the media's line lit with its play button");
                                }
                            }

                            // A still point of each line: its leading gutter's top corner.
                            foreach (var id in new[] { "clock", "timer", "media", "pin" })
                            {
                                var line = ids.IndexOf(id);
                                var card = plan.Cards[line];
                                var (x, y) = ((int)card.left + 3, (int)card.top + 3);
                                flyout.Message(WM.WM_MOUSEMOVE, 0, At(flyout, x - 1, y));
                                flyout.Message(WM.WM_MOUSEMOVE, 0, At(flyout, x, y));
                                WindowShots.Settle(200);
                                Assert.True(WindowShots.Save(flyout.Hwnd, $"{name}-{id}.png"));
                                var fill = FillOn(flyout, line);
                                // The clock's line is one thing to press since its click searches the time (§377).
                                var wanted = id is "pin" or "clock";
                                if ((fill == lit) != wanted)
                                {
                                    failures.Add($"{name}: the {id} line under the pointer {(wanted ? "did not light" : "lit as a whole")} ({fill})");
                                }
                            }
                            flyout.Hide();
                        }
                        finally
                        {
                            folder.Dispose();
                        }
                    }
                }
            }
            finally
            {
                MediaIsland.Stand = null;
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    // --- §611: an item lights its line to its edges ------------------------------------------------

    private const float EdgeScale = 1.5f;

    private const int EdgeStep = 3;

    /// <summary>
    /// A control — a button, a stepper, a thumb — is no taller than this and short of half the line's width, or no wider
    /// and short of half its height, DIP: its own plate, in from the line (§428).
    /// </summary>
    private const float ControlDip = 48;

    /// <summary>
    /// The calendar's grids, lit as the calendar flyout lights them — a day's cell, a day's column with its heading, round
    /// (§428: "not ... the month's days").
    /// </summary>
    private static readonly HashSet<ListKind> Grids = [ListKind.Month, ListKind.DayView, ListKind.WeekView];

    /// <summary>The sizes <paramref name="kind"/> is offered in, or its one form (null) where it offers none.</summary>
    private static IEnumerable<LineSize?> SizesOf(ListKind kind) =>
        kind.Offered() is { Count: > 0 } sizes ? sizes.Select(size => (LineSize?)size) : [null];

    /// <summary>A folder of <paramref name="kind"/>'s line at <paramref name="size"/>, its stand-ins in, opened.</summary>
    private static FolderIsland FolderOf(Gfx gfx, ListKind kind, params LineSize?[] sizes) =>
        ListFormPictures.Folder(gfx, ListGutterTests.Before, [.. sizes.Select(size =>
        {
            var item = ListGutterTests.Made(kind);
            if (size is { } chosen)
            {
                item.Size = chosen;
                item.Normalize();
            }
            return item;
        })]);

    /// <summary>Each target of <paramref name="line"/> in <paramref name="box"/>: the box round where it takes the pointer, and a point of it.</summary>
    private static Dictionary<int, (TerraFX.Interop.DirectX.D2D_RECT_F Hit, System.Numerics.Vector2 On)> TargetsOf(CellIsland line, TerraFX.Interop.DirectX.D2D_RECT_F box, TileStyle style)
    {
        var found = new Dictionary<int, (TerraFX.Interop.DirectX.D2D_RECT_F Hit, List<System.Numerics.Vector2> Points)>();
        for (var y = box.top + 0.5f; y < box.bottom; y += EdgeStep)
        {
            for (var x = box.left + 0.5f; x < box.right; x += EdgeStep)
            {
                var point = new System.Numerics.Vector2(x, y);
                if (line.TargetOn(point, box, style, WidgetSurface.List) is var target and not 0)
                {
                    var hit = found.TryGetValue(target, out var was) ? was.Hit : new(x, y, x, y);
                    var points = was.Points ?? [];
                    points.Add(point);
                    found[target] = (new(MathF.Min(hit.left, x), MathF.Min(hit.top, y), MathF.Max(hit.right, x), MathF.Max(hit.bottom, y)), points);
                }
            }
        }
        // A point in the middle of each: the one of its points nearest the middle of its box.
        return found.ToDictionary(each => each.Key, each =>
        {
            var hit = each.Value.Hit;
            var middle = new System.Numerics.Vector2((hit.left + hit.right) / 2, (hit.top + hit.bottom) / 2);
            return (hit, each.Value.Points.MinBy(point => System.Numerics.Vector2.DistanceSquared(point, middle)));
        });
    }

    /// <summary>Whether the pixel at (<paramref name="x"/>, <paramref name="y"/>) of a drawing <paramref name="width"/> px wide is inked.</summary>
    private static bool InkedAt(byte[] pixels, int width, int x, int y) =>
        x >= 0 && y >= 0 && x < width && ((y * width) + x) * 4 + 3 < pixels.Length && pixels[(((y * width) + x) * 4) + 3] > 8;

    /// <summary>
    /// Past <paramref name="lit"/>'s side (<paramref name="dx"/>, <paramref name="dy"/> outward), from its middle to the
    /// line's edge or the next target: null where the side is the edge or meets the next target, where something drawn
    /// stands in the room between (a heading, an axis, the words of a row with nothing to do), or where it is the room
    /// under the last of a list, which §436 leaves unlit; else how many px of nobody's room it leaves. §661: over or under an
    /// item, a heading drawn anywhere across the rows between parts it from the next (a grid's tiles under "Pinned", the
    /// heading on the gutter, not over each tile); beside the last tile of a grid's short row, the room to the line's edge
    /// is the grid's empty cells — whole cells as wide as the tile — as File Explorer's grid leaves them.
    /// </summary>
    private static float? Short(CellIsland line, TerraFX.Interop.DirectX.D2D_RECT_F box, TileStyle style, byte[] rest, int target,
        TerraFX.Interop.DirectX.D2D_RECT_F lit, int dx, int dy)
    {
        var width = (int)MathF.Ceiling(box.right);
        var (x, y) = dx < 0 ? (lit.left - 0.5f, (lit.top + lit.bottom) / 2) : dx > 0 ? (lit.right + 0.5f, (lit.top + lit.bottom) / 2)
            : dy < 0 ? ((lit.left + lit.right) / 2, lit.top - 0.5f) : ((lit.left + lit.right) / 2, lit.bottom + 0.5f);
        var gap = 0;
        var met = false;
        for (var (px, py) = (x, y); px >= 0 && py >= 0 && px < box.right && py < box.bottom; px += dx, py += dy)
        {
            if (line.TargetOn(new System.Numerics.Vector2(px, py), box, style, WidgetSurface.List) is var other and not 0 && other != target)
            {
                met = true;
                break;
            }
            gap++;
        }
        if (gap <= 1 || (dy > 0 && !met))
        {
            return null;
        }
        var wide = lit.right - lit.left;
        if (dx > 0 && !met && wide < box.right / 2 && MathF.Round(gap / wide) is var cells and >= 1 && MathF.Abs(gap - (cells * wide)) <= cells + 1)
        {
            return null;
        }
        // The room between, across the item's span — over or under it across the line's rows: anything drawn in it stands between.
        var room = dx < 0 ? (lit.left - gap, lit.top, lit.left, lit.bottom) : dx > 0 ? (lit.right, lit.top, lit.right + gap, lit.bottom)
            : dy < 0 ? (0f, lit.top - gap, box.right, lit.top) : (0f, lit.bottom, box.right, lit.bottom + gap);
        for (var py = (int)room.Item2; py < (int)room.Item4; py++)
        {
            for (var px = (int)room.Item1; px < (int)room.Item3; px++)
            {
                if (InkedAt(rest, width, px, py))
                {
                    return null;
                }
            }
        }
        return gap;
    }

    /// <summary>§661: what changed, but in <paramref name="part"/> (grown a px each way): its box drawn round the rest; null where nothing else changed.</summary>
    private static (TerraFX.Interop.DirectX.D2D_RECT_F Box, bool[] Lit)? Without((TerraFX.Interop.DirectX.D2D_RECT_F Box, bool[] Lit)? found, TerraFX.Interop.DirectX.D2D_RECT_F part, int width)
    {
        if (found is not { } changed || part.right <= part.left)
        {
            return found;
        }
        var lit = (bool[])changed.Lit.Clone();
        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        for (var at = 0; at < lit.Length; at++)
        {
            var (x, y) = (at % width, at / width);
            if (x >= part.left - 1 && x < part.right + 1 && y >= part.top - 1 && y < part.bottom + 1)
            {
                lit[at] = false;
            }
            if (lit[at])
            {
                (left, top, right, bottom) = (Math.Min(left, x), Math.Min(top, y), Math.Max(right, x + 1), Math.Max(bottom, y + 1));
            }
        }
        return right < 0 ? null : (new TerraFX.Interop.DirectX.D2D_RECT_F(left, top, right, bottom), lit);
    }

    /// <summary>The line drawn at rest, once what it shows has settled: drawn again until two drawings are the same (a picture come in).</summary>
    private static byte[] AtRest(CellIsland line, TerraFX.Interop.DirectX.D2D_RECT_F box, TileStyle style)
    {
        line.HoverOn(null, box, style, WidgetSurface.List);
        var rest = LineHitSweepTests.Drawn(line, box, style);
        for (var again = 0; again < 5; again++)
        {
            var next = LineHitSweepTests.Drawn(line, box, style);
            if (LineHitSweepTests.Apart(rest, next, (int)MathF.Ceiling(box.right)) is null)
            {
                break;
            }
            rest = next;
            Thread.Sleep(50);
        }
        return rest;
    }

    /// <summary>
    /// Every kind a list holds, at every size it offers, in a folder at the height the open folder gives it: each item a
    /// press on it acts on lights a band of the line — what it lights is where it takes the pointer, square, and each side
    /// of it the line's edge, the next item's, or against something drawn between; never the line's padding left unlit
    /// round it — but the room under the last of a list (§436); and the items laid across a line, each lighting it top to
    /// foot, all as wide as each other (§632), the first and the last no wider. A control (a button, a thumb: <see cref="ControlDip"/>)
    /// keeps its plate, the calendar's grids theirs (<see cref="Grids"/>); a line the folder lights whole is the folder's
    /// (<see cref="OnTheScreenOnlyAWholeLineLights"/>).
    /// </summary>
    [UiFact]
    public void AnItemLightsItsLineToItsEdges()
    {
        var failures = new List<string>();
        ListGutterTests.StoodIn<Gfx>(Karpo.Tests.Forecasts.SavedWeather.Fixed, Offscreen.OnStaThread, gfx =>
        {
            var look = ListFormPictures.LookFor(dark: true) with { Animations = false };
            var style = FolderFlyout.StyleFor(EdgeScale, look, FlyoutTheme.For(look));
            var control = style.Px(ControlDip);
            foreach (var widget in ListWidgets.All)
            {
                foreach (var size in SizesOf(widget.Kind))
                {
                    var name = size is { } chosen ? $"{widget.Kind} {chosen}" : widget.Kind.ToString();
                    FolderIsland folder;
                    try
                    {
                        folder = FolderOf(gfx, widget.Kind, size);
                    }
                    catch (Exception error) when (error is not OutOfMemoryException)
                    {
                        failures.Add($"{name}: threw while it was made: {error.GetType().Name}: {error.Message}");
                        continue;
                    }
                    try
                    {
                        ListGutterTests.Ready(folder);
                        if (folder.Tiles[0].Island is not CellIsland line || FolderFlyout.LightsWhole(folder.Tiles[0].Cell, line) || Grids.Contains(widget.Kind))
                        {
                            continue;
                        }
                        if (line is Karpo.Dock.Rss.RssIsland feed)
                        {
                            // Its pictures in first, so what lights is all that changes.
                            _ = Karpo.Tests.Rss.RssSamples.Pictured(feed);
                        }
                        var (_, plan) = ListFormPictures.Plan(folder, look, EdgeScale);
                        var card = plan.Cards[0];
                        var box = PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top);
                        var width = (int)MathF.Ceiling(box.right);
                        var rest = AtRest(line, box, style);
                        // §632: the items laid across the line, each lighting it top to foot.
                        var across = new List<float>();
                        // §661: the overlay scrollbar a line that scrolls shows while the pointer is on it is no item's light.
                        var thumb = line.Scrolled(box, style, WidgetSurface.List).Thumb(style);
                        foreach (var (target, (hit, on)) in TargetsOf(line, box, style))
                        {
                            line.HoverOn(on, box, style, WidgetSurface.List);
                            var apart = Without(LineHitSweepTests.Apart(rest, LineHitSweepTests.Drawn(line, box, style), width), thumb, width);
                            line.HoverOn(null, box, style, WidgetSurface.List);
                            if (apart is not { } found)
                            {
                                continue;
                            }
                            if (found.Box.top <= 1 && found.Box.bottom >= box.bottom - 1 && found.Box.right - found.Box.left < box.right - 1)
                            {
                                across.Add(found.Box.right - found.Box.left);
                            }
                            // A control by where it takes the pointer — one on an item (a notification's ×) lights its item
                            // with it — or by what it lights: a slider is pressed along its track and lights its thumb.
                            bool Small(TerraFX.Interop.DirectX.D2D_RECT_F part) => (part.bottom - part.top <= control && part.right - part.left < box.right / 2)
                                || (part.right - part.left <= control && part.bottom - part.top < box.bottom / 2);
                            if (Small(hit) || Small(found.Box))
                            {
                                continue;
                            }
                            var lit = found.Box;
                            var said = $"{name} {box.right:0}×{box.bottom:0}: item {target} lights {lit.left:0}-{lit.right:0} × {lit.top:0}-{lit.bottom:0}";
                            // Square, the line's own clip rounding what reaches a corner: its corners lit.
                            var corners = new[] { (lit.left, lit.top), (lit.right - 1, lit.top), (lit.left, lit.bottom - 1), (lit.right - 1, lit.bottom - 1) }
                                .Count(corner => !found.Lit[((int)corner.Item2 * width) + (int)corner.Item1]);
                            if (corners > 0)
                            {
                                failures.Add($"{said}: a rounded plate, {corners} of its corners unlit");
                            }
                            // What it lights is where it takes the pointer, to a sweep's step.
                            if (MathF.Abs(hit.left - lit.left) > EdgeStep + 1 || MathF.Abs(hit.right - lit.right) > EdgeStep + 1
                                || MathF.Abs(hit.top - lit.top) > EdgeStep + 1 || MathF.Abs(hit.bottom - lit.bottom) > EdgeStep + 1)
                            {
                                failures.Add($"{said}, and takes the pointer at {hit.left:0}-{hit.right:0} × {hit.top:0}-{hit.bottom:0}");
                            }
                            foreach (var (side, gap) in new[]
                            {
                                ("left", Short(line, box, style, rest, target, lit, -1, 0)),
                                ("right", Short(line, box, style, rest, target, lit, 1, 0)),
                                ("top", Short(line, box, style, rest, target, lit, 0, -1)),
                                ("bottom", Short(line, box, style, rest, target, lit, 0, 1)),
                            })
                            {
                                if (gap is { } room)
                                {
                                    failures.Add($"{said}: {room:0} px of the line unlit past its {side}, nothing drawn in it");
                                }
                            }
                        }
                        // §632: items across a line as wide as each other, the first and the last no wider (a px apart, by rounding).
                        if (across.Count >= 2 && across.Max() - across.Min() > 1)
                        {
                            failures.Add($"{name} {box.right:0}×{box.bottom:0}: its {across.Count} items across the line light {string.Join(", ", across.Select(each => $"{each:0}"))} px wide, not one width");
                        }
                    }
                    catch (Exception error) when (error is not OutOfMemoryException)
                    {
                        failures.Add($"{name}: threw {error.GetType().Name}: {error.Message}");
                    }
                    finally
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures) + $"\n({failures.Count} in all)");
    }

    /// <summary>
    /// §661 (the person, 2026-11-10: "the items in the widgets that were created newly dont go from edge to edge like the
    /// other widgets are designed to do"): a grid's tiles — Start's pinned and all apps, the recommended files, Windows
    /// search's Top apps and Pinned — are the line's whole width in cells as wide as each other, each row from its leading
    /// edge to its trailing one, the rows touching; a short last row keeps the others' cells; a cell's words stay inside the
    /// gutters at the line's edges. Before, each stood a gutter in on a rounded plate.
    /// </summary>
    [Fact]
    public void AGridsTilesSpanTheLineEdgeToEdgeAsWideAsEachOther()
    {
        var line = new TerraFX.Interop.DirectX.D2D_RECT_F(0, 0, 541, 900);
        var cells = LineItem.Grid(line, 40, 8, 3, 100);
        Assert.Equal(8, cells.Count);
        foreach (var row in new[] { 0, 3 })
        {
            Assert.Equal(line.left, cells[row].left);
            Assert.Equal(line.right, cells[row + 2].right);
            Assert.Equal(cells[row].right, cells[row + 1].left);
            Assert.Equal(cells[row + 1].right, cells[row + 2].left);
        }
        Assert.True(cells.Max(cell => cell.right - cell.left) - cells.Min(cell => cell.right - cell.left) <= 1);
        Assert.Equal((40f, 140f, 140f, 240f, 240f, 340f), (cells[0].top, cells[0].bottom, cells[3].top, cells[3].bottom, cells[6].top, cells[6].bottom));
        // The short row: the others' cells, the room past them nobody's.
        Assert.Equal((cells[0].left, cells[0].right, cells[1].right), (cells[6].left, cells[6].right, cells[7].right));
        // Words: a pad in from the cell, never past the gutters.
        Assert.Equal((12f, cells[0].right - 4), LineItem.Words(cells[0], line.left, line.right, 12, 4));
        Assert.Equal((cells[2].left + 4, line.right - 12), LineItem.Words(cells[2], line.left, line.right, 12, 4));
    }

    /// <summary>The kinds whose items stand across their line, each a band of it top to foot (§611, §632): the forecasts in columns.</summary>
    private static readonly ListKind[] Across = [ListKind.WeatherHours, ListKind.WeatherHoursList, ListKind.WeatherDays, ListKind.WeatherDaysGraph];

    /// <summary>
    /// The lines whose items stand across them hovered (§611, §632), dark and light: each kind a folder of its sizes, the
    /// first, the middle and the last item of each lit in turn — item-hover-&lt;kind&gt;-&lt;first|middle|last&gt;-&lt;dark|light&gt;.png
    /// in KARPO_PNG_DIR (the daily List's Full a row a day, down).
    /// </summary>
    [UiFact]
    public void TheItemsHoveredPictures()
    {
        ListGutterTests.StoodIn<Gfx>(Karpo.Tests.Forecasts.SavedWeather.Fixed, Offscreen.OnStaThread, gfx =>
        {
            foreach (var kind in Across)
            {
                foreach (var dark in new[] { true, false })
                {
                    var look = ListFormPictures.LookFor(dark) with { Animations = false };
                    var theme = FlyoutTheme.For(look);
                    var style = FolderFlyout.StyleFor(EdgeScale, look, theme);
                    using var folder = FolderOf(gfx, kind, [.. SizesOf(kind)]);
                    try
                    {
                        var (_, plan) = ListFormPictures.Plan(folder, look, EdgeScale);
                        var whole = FolderFlyout.LightsWhole(folder.Tiles[0].Cell, folder.Tiles[0].Island);
                        var lit = FolderFlyout.InkOf(theme, CardFill.LineHover);
                        // Each line's targets swept once, in order, for the three pictures.
                        var lines = Enumerable.Range(0, folder.Tiles.Count).Select(index =>
                        {
                            var card = plan.Cards[index];
                            var box = PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top);
                            var line = folder.Tiles[index].Island as CellIsland;
                            return (line, box, Targets: line is null ? new List<System.Numerics.Vector2>() : TargetsOf(line, box, style).OrderBy(each => each.Key).Select(each => each.Value.On).ToList());
                        }).ToList();
                        foreach (var at in new[] { "first", "middle", "last" })
                        {
                            foreach (var (line, box, targets) in lines)
                            {
                                if (line is not null && targets.Count > 0)
                                {
                                    line.HoverOn(at switch { "first" => targets[0], "last" => targets[^1], _ => targets[targets.Count / 2] }, box, style, WidgetSurface.List);
                                }
                            }
                            ListFormPictures.Sheet(gfx, $"item-hover-{kind}-{at}-{(dark ? "dark" : "light")}.png", EdgeScale,
                                [($"{kind}: {string.Join(" over ", SizesOf(kind))}, the {at} item hovered", look, folder, plan)],
                                whole ? (0, PopupPaint.Ink(lit)) : null);
                        }
                    }
                    finally
                    {
                        folder.Closed();
                    }
                }
            }
        });
    }
}
