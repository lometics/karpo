// The stopwatch as a line of a list at one row to six, as the timer's is tested (CountdownFormsTests),
// and that its tenths repaint what shows them and rebuild nothing. Its entry of a taskbar and the
// panel that opened became the stopwatch preset (docs\widgets.md §92), whose face is TimerPresetTests'.
//
// Everything here draws, measures text or shows a window, so it runs with KARPO_UI_TESTS=1; the
// sheets go to KARPO_PNG_DIR beside the timer's. Every widget runs on a store of the test's own and
// a clock the test turns (StopwatchTile's now).

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using Edge = Karpo.Core.Edge;

namespace Karpo.Tests.Calendars;

public sealed unsafe class StopwatchFormsTests
{
    private static readonly RECT Work = new() { left = 0, top = 0, right = 1920, bottom = 1040 };
    private static readonly POINT Anchor = new() { x = 1700, y = 1040 };

    private static Look LookFor(bool dark, float textScale = 1f) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8),
        TextScale: textScale);

    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    /// <summary>What a stopwatch can be doing, read at 100 s on its clock.</summary>
    private static readonly TimeSpan Now = S(100);

    private static (string What, StopwatchState State)[] States()
    {
        var running = StopwatchState.Start(Now - S(83.4));
        return
        [
            ("idle", StopwatchState.None),
            ("running", running),
            ("paused", running.Pause(Now - S(20))),
            ("laps", running.Lap(Now - S(70)).Lap(Now - S(41.2)).Lap(Now - S(3.9))),
            ("an hour", StopwatchState.Start(Now - S(3723.4)).Lap(Now - S(60))),
        ];
    }

    /// <summary>A stopwatch widget of a list on a store of its own, showing <paramref name="state"/>.</summary>
    private static StopwatchTile Tile(Gfx gfx, StopwatchState state, Func<TimeSpan>? now = null, Countdowns? countdowns = null)
    {
        countdowns ??= new Countdowns();
        countdowns.Stopwatches.Set("probe", state);
        var item = Karpo.Core.ListKind.Stopwatch.New();
        item.Id = "probe";
        return new StopwatchTile(Karpo.Core.Config.IslandKey(null, "probe"), item, gfx, new Karpo.Core.Config(), countdowns, now ?? (() => Now), () => ".");
    }

    private static float RowsPx(int rows, float scale, float textScale) => Karpo.Tests.Folders.ListFormPictures.Rows(rows, scale, textScale);

    /// <summary>
    /// A line asks for no tick of its own: while it runs the open folder's quick tick draws its
    /// tenths (<see cref="StopwatchTile.Counting"/>), and its face's whole seconds are the dock's. A
    /// tick never asks for the folder to be laid out again. Two lines of one id are one stopwatch.
    /// </summary>
    [UiFact]
    public void ItsTicksRepaintAndRebuildNothing()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var now = Now;
            var countdowns = new Countdowns();
            using var other = Tile(gfx, StopwatchState.None, () => now, countdowns);
            using var line = Tile(gfx, StopwatchState.None, () => now, countdowns);
            Assert.Equal(0, line.IntervalMs);
            Assert.False(line.Counting);

            _ = line.Click((int)StopwatchTargetKind.None);
            Assert.True(countdowns.Stopwatches.Of("probe").Idle);

            _ = line.Click((int)StopwatchTargetKind.Action);
            Assert.Equal(0, line.IntervalMs);
            Assert.True(line.Counting);
            Assert.True(other.Counting);
            for (var tenth = 0; tenth < 25; tenth++)
            {
                now += S(0.1);
                Assert.False(line.Tick(gfx), "a tick asked for the folder to be laid out again");
            }
            Assert.Equal("00:02", StopwatchText.Cell(countdowns.Stopwatches.Of("probe").Elapsed(now)));

            // Paused, it holds, and asks for nothing.
            _ = line.Click((int)StopwatchTargetKind.Action);
            Assert.False(line.Counting);
        });
    }

    // --- A line of a list ------------------------------------------------------------

    /// <summary>
    /// Its line is the kit's row with its laps under it (§401), worked at its own height and at one row to six,
    /// at 100, 150 and 200 %, at every text size, on a list of every width, in every state: its start button on
    /// the trailing gutter of its row, where it stands in every state, and a click there reaches it; nothing
    /// outside the line or its gutters, nothing on anything else; one row lists no laps and shows the lap under
    /// way in its name's place once there is one; a taller line lists its laps a row each, never fewer than a
    /// shorter one; at the usual width and text it has its name, its lap and its reset buttons.
    /// </summary>
    [UiFact]
    public void TheLineIsWorkedAtEveryHeight()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var failures = new List<string>();
            foreach (var scale in new[] { 1f, 1.5f, 2f })
            {
                foreach (var textScale in new[] { 1f, 1.5f, 2.25f })
                {
                    var look = LookFor(true, textScale);
                    var style = new TileStyle(scale, look, FlyoutTheme.For(look), 1, 1);
                    var form = ListForm.Of(gfx, style);
                    foreach (var listWidth in Karpo.Core.FolderListWidths.All)
                    {
                        var width = FolderFlyout.ListWidth(scale, listWidth);
                        foreach (var (what, state) in States())
                        {
                            using var tile = Tile(gfx, state);
                            var shorterLaps = 0;
                            D2D_RECT_F? start = null;
                            for (var rows = 0; rows <= 6; rows++)
                            {
                                var height = rows == 0 ? StopwatchCard.RowHeight(style) : RowsPx(rows, scale, textScale);
                                var box = PopupPaint.Rect(0, 0, width, height);
                                var inside = new D2D_RECT_F(form.Inset - 0.5f, -0.5f, width - form.Inset + 0.5f, height + 0.5f);
                                var plan = tile.Plan(box, style);
                                var pieces = ((ILinePieces)tile).LinePieces(box, style).Where(piece => piece.Box.right > piece.Box.left).ToList();
                                var where = $"{scale:0.##}x, text {textScale:0.##}, {listWidth} ({width} px), {(rows == 0 ? "its own height" : $"{rows} rows")}, {what}";
                                void Fail(string why) => failures.Add($"{where}: {why}");
                                var action = plan.Targets[^1].Box;
                                if (MathF.Abs(action.right - (width - form.Inset)) > 1 || !Inside(action, inside) || action.bottom > plan.Row.Bottom + 0.5f)
                                {
                                    Fail($"its start button {Place(action)} is not on the trailing gutter of its row");
                                }
                                if (rows > 0 && start is { } before && (MathF.Abs(before.left - action.left) > 0.5f || MathF.Abs(before.top - action.top) > 0.5f))
                                {
                                    Fail($"its start button moved from {Place(before)} to {Place(action)}");
                                }
                                start ??= rows > 0 ? action : null;
                                var middle = new Vector2((action.left + action.right) / 2, (action.top + action.bottom) / 2);
                                if (tile.CardTarget(middle, box, style, Karpo.Dock.WidgetSurface.List) != (int)StopwatchTargetKind.Action)
                                {
                                    Fail("a click on its start button does not reach it");
                                }
                                foreach (var piece in pieces)
                                {
                                    if (!Inside(piece.Box, inside))
                                    {
                                        Fail($"its {piece.Name} {Place(piece.Box)} is outside the line {Place(inside)}");
                                    }
                                }
                                for (var one = 0; one < pieces.Count; one++)
                                {
                                    for (var other = one + 1; other < pieces.Count; other++)
                                    {
                                        if (Overlap(pieces[one].Box, pieces[other].Box))
                                        {
                                            Fail($"its {pieces[one].Name} and its {pieces[other].Name} overlap");
                                        }
                                    }
                                }
                                var split = pieces.Any(piece => piece.Name == "split");
                                // At the list's text: at a larger one the time in the Value leaves a narrow row no room for it.
                                if (plan.LapRows == 0 && state.Splits.Length > 0 && !split && textScale == 1)
                                {
                                    Fail("a line of one row with laps does not show the lap under way");
                                }
                                if ((plan.LapRows > 0 || state.Splits.Length == 0) && split)
                                {
                                    Fail("it shows the lap under way where it lists its laps or has none");
                                }
                                if (rows == 1 && plan.LapRows > 0)
                                {
                                    Fail("a line of one row lists laps");
                                }
                                if (rows >= 2 && textScale == 1 && plan.LapRows < 1)
                                {
                                    Fail("a line of two rows lists no lap");
                                }
                                if (rows > 1 && plan.LapRows < shorterLaps)
                                {
                                    Fail($"it lists fewer laps than a line a row shorter ({plan.LapRows} < {shorterLaps})");
                                }
                                shorterLaps = rows > 0 ? plan.LapRows : shorterLaps;
                                if (plan.Laps.Count != Math.Min(plan.LapRows, state.Splits.Length))
                                {
                                    Fail($"it lists {plan.Laps.Count} laps of {state.Splits.Length} in {plan.LapRows} rows");
                                }
                                if (listWidth == Karpo.Core.FolderListWidth.Medium && textScale == 1 && (plan.Targets.Count != 3 || (!split && !pieces.Any(piece => piece.Name == "name"))))
                                {
                                    Fail("it has not its name, its lap and its reset where the whole row fits");
                                }
                            }
                        }
                    }
                }
            }
            Assert.True(failures.Count == 0, $"{failures.Count} failures, the first of them:\n{string.Join("\n", failures.Take(40))}");
        });
    }

    /// <summary>
    /// Its buttons are greyed where they do nothing, as Windows' Clock has them, and a greyed one takes no
    /// click: idle, only Start; running, the lap and the reset too; paused, the reset. The newest lap is listed
    /// first, how long it was against the trailing gutter and where it ended after its name.
    /// </summary>
    [UiFact]
    public void ItsButtonsAreGreyedWhereTheyDoNothing()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var look = LookFor(true);
            var style = new TileStyle(1.5f, look, FlyoutTheme.For(look), 1, 1);
            var box = PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(1.5f), RowsPx(5, 1.5f, 1));
            var now = Now;
            using var tile = Tile(gfx, StopwatchState.None, now: () => now);
            Vector2 Middle(D2D_RECT_F at) => new((at.left + at.right) / 2, (at.top + at.bottom) / 2);
            StopwatchTargetKind At(D2D_RECT_F part) => (StopwatchTargetKind)tile.CardTarget(Middle(part), box, style, Karpo.Dock.WidgetSurface.List);
            StopwatchLinePlan Line() => tile.Plan(box, style);
            // The lap, the reset and the start, as the row orders them.
            bool[] Enabled() => Line().Targets.Select(target => target.Enabled).ToArray();
            D2D_RECT_F Button(StopwatchTargetKind kind) => Line().Targets.Single(target => target.Kind == kind).Box;

            Assert.Equal([false, false, true], Enabled());
            Assert.Equal(StopwatchTargetKind.None, At(Button(StopwatchTargetKind.Lap)));
            Assert.Equal(StopwatchTargetKind.None, At(Button(StopwatchTargetKind.Reset)));
            Assert.Equal("Start", Line().ActionText);

            _ = tile.Click((int)At(Button(StopwatchTargetKind.Action)));
            Assert.Equal([true, true, true], Enabled());
            Assert.Equal("Pause", Line().ActionText);
            now += S(12.3);
            _ = tile.Click((int)At(Button(StopwatchTargetKind.Lap)));
            now += S(5.054);
            _ = tile.Click((int)At(Button(StopwatchTargetKind.Lap)));
            Assert.Equal([new StopwatchLapText("Lap 2", "0:05.0", "0:17.3"), new StopwatchLapText("Lap 1", "0:12.3", "0:12.3")], Line().Laps);
            Assert.Equal("0:17.3", Line().ValueText);

            _ = tile.Click((int)At(Button(StopwatchTargetKind.Action)));
            Assert.Equal([false, true, true], Enabled());
            Assert.Equal("Resume", Line().ActionText);
            Assert.Equal(("Resume", false, true), tile.MenuButtons);

            _ = tile.Click((int)At(Button(StopwatchTargetKind.Reset)));
            Assert.True(tile.State.Idle);
            Assert.Empty(Line().Laps);
            Assert.Equal("0:00.0", Line().ValueText);

            // The menu's own: start, lap and reset, on the same stopwatch.
            tile.Act();
            now += S(1);
            tile.Lap();
            Assert.Single(tile.State.Splits);
            tile.Reset();
            Assert.True(tile.State.Idle);
        });
    }

    /// <summary>
    /// At one row its lap button shows what it did: from the first lap the lap under way stands in the name's
    /// place, counting from the last lap, held while paused, gone on a reset. A regression: the person, "for the
    /// stopwatch the flag does not do anything (at least when shown one row high.)". At every list width, the
    /// narrow one too.
    /// </summary>
    [UiFact]
    public void AtOneRowTheFlagShowsTheLapUnderWay()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var look = LookFor(true);
            foreach (var listWidth in Karpo.Core.FolderListWidths.All)
            {
                var style = new TileStyle(1, look, FlyoutTheme.For(look), 1, 1);
                var box = PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(1, listWidth), RowsPx(1, 1, 1));
                var now = Now;
                using var tile = Tile(gfx, StopwatchState.None, now: () => now);
                string Split() => ((ILinePieces)tile).LinePieces(box, style).Where(piece => piece.Name == "split").Select(piece => piece.Text).FirstOrDefault() ?? "";
                Assert.Equal(0, tile.Plan(box, style).LapRows);

                _ = tile.Click((int)StopwatchTargetKind.Action);
                now += S(12.3);
                Assert.Equal("", Split());
                _ = tile.Click((int)StopwatchTargetKind.Lap);
                Assert.Single(tile.State.Splits);
                Assert.Equal("0:00.0", Split());
                now += S(4.56);
                var pieces = ((ILinePieces)tile).LinePieces(box, style);
                Assert.Equal("0:04.5", Split());
                Assert.Equal("0:16.8", pieces.Single(piece => piece.Name == "time").Text);
                Assert.DoesNotContain(pieces, piece => piece.Name == "name");
                Assert.True(pieces.Single(piece => piece.Name == "split").Box.right < pieces.Single(piece => piece.Name == "time").Box.left,
                    $"{listWidth}: the lap under way runs into the time");

                tile.Act();
                now += S(30);
                Assert.Equal("0:04.5", Split());
                tile.Reset();
                Assert.Equal("", Split());
            }
        });
    }

    /// <summary>Its line says what it laid out, as every line of a list does (ILinePieces).</summary>
    [UiFact]
    public void ItsLineSaysWhatItLaidOut()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var look = LookFor(true);
            var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
            var width = FolderFlyout.ListWidth(1.5f);
            using var tile = Tile(gfx, States()[3].State);
            var line = (ILinePieces)tile;
            Assert.Equal("time", line.MainPiece);
            foreach (var rows in new[] { 1, 2, 6 })
            {
                var height = LineRows.Px(rows, style);
                var pieces = line.LinePieces(PopupPaint.Rect(0, 0, width, height), style);
                var names = pieces.Select(piece => piece.Name).ToList();
                // One row has no room for its laps: the lap under way stands in its name's place. Taller, its
                // name, and its laps under the row where they fit: at six rows all three.
                Assert.Equal((rows == 1 ? ["glyph", "split", "time", "lap", "reset", "start"] : new[] { "glyph", "name", "time", "lap", "reset", "start" }).Order(),
                    names.Where(name => name is not ("laps" or "lap length")).Order());
                Assert.Equal(rows == 6 ? 3 : rows == 2 ? 1 : 0, names.Count(name => name == "laps"));
                Assert.Equal("1:23.4", pieces.Single(piece => piece.Name == "time").Text);
            }
        });
    }

    /// <summary>
    /// In an open folder a running stopwatch arms the folder's quick tick, which draws its line alone;
    /// paused, the tick goes, and closed, nothing ticks at all.
    /// </summary>
    [UiFact]
    public void AFolderTicksATenthOnlyWhileOneCounts()
    {
        WindowShots.OnHiddenDesktop("stopwatch-folder", (gfx, instance) =>
        {
            var countdowns = new Countdowns();
            var island = Karpo.Core.BarKind.FolderList.New();
            island.Id = "clock";
            island.Folder = new Karpo.Core.FolderOptions();
            var stopwatch = Karpo.Core.ListKind.Stopwatch.New();
            stopwatch.Id = "sw";
            _ = Karpo.Core.Folders.Add(Karpo.Core.BarKind.FolderList, island.Folder, stopwatch);
            _ = Karpo.Core.Folders.Add(Karpo.Core.BarKind.FolderList, island.Folder, Karpo.Core.ListKind.Cpu.New());
            using var folder = new FolderIsland($"layout:default/{island.Id}", island, gfx, new Karpo.Core.Config(), countdowns);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, LookFor(true), 1f, Anchor, Work, Edge.Bottom, folder, FlyoutAlign.End);
            Assert.False(flyout.Quick, "a folder with an idle stopwatch ticks a tenth");

            countdowns.Stopwatches.Act("sw", StopwatchClock.Now());
            flyout.ArmQuick();
            Assert.True(flyout.Quick, "a folder with a running stopwatch does not tick a tenth");
            _ = flyout.Message(WM.WM_TIMER, (WPARAM)FolderFlyout.QuickTimer, 0);
            Assert.True(flyout.Quick);

            countdowns.Stopwatches.Act("sw", StopwatchClock.Now());
            flyout.ArmQuick();
            Assert.False(flyout.Quick, "a paused stopwatch still ticks its folder");

            countdowns.Stopwatches.Act("sw", StopwatchClock.Now());
            flyout.ArmQuick();
            Assert.True(flyout.Quick);
            flyout.Hide();
            Assert.False(flyout.Quick, "a closed folder ticks a tenth");
        });
    }

    // --- Sheets --------------------------------------------------------------------------

    /// <summary>
    /// Sheets beside the timer's (countdown-*.png): a line of a list at its own height and at one row
    /// to six, in every state, dark and light, at 100 and 150 %, and in high contrast.
    /// </summary>
    [UiFact]
    public void StopwatchSheets()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var scale in new[] { 1f, 1.5f })
            {
                foreach (var dark in new[] { true, false })
                {
                    var name = $"{(int)(scale * 100)}-{(dark ? "dark" : "light")}";
                    var look = LookFor(dark);
                    Lines(gfx, Path.Combine(Offscreen.Pictures, $"stopwatch-line-{name}.png"), look, scale);
                    OneRow(gfx, Path.Combine(Offscreen.Pictures, $"stopwatch-one-row-{name}.png"), look, scale);
                }
            }
            var contrast = LookFor(true) with
            {
                HighContrast = true,
                HcWindow = Color.FromArgb(255, 0, 0, 0),
                HcText = Color.FromArgb(255, 255, 255, 255),
                HcHighlight = Color.FromArgb(255, 0x1A, 0xEB, 0xFF),
                HcHighlightText = Color.FromArgb(255, 0, 0, 0),
            };
            Lines(gfx, Path.Combine(Offscreen.Pictures, "stopwatch-line-150-contrast.png"), contrast, 1.5f);
            // A flyout colour of somebody's own: the lines are drawn on it, in its inks.
            var own = LookFor(true) with { Colors = new Karpo.Core.Palette(Flyout: new Karpo.Core.Argb(0xFF, 0x1E, 0x3A, 0x5F)) };
            Lines(gfx, Path.Combine(Offscreen.Pictures, "stopwatch-line-150-colours.png"), own, 1.5f);
        });
    }

    private static void Lines(Gfx gfx, string path, Look look, float scale)
    {
        var theme = FlyoutTheme.For(look);
        var pad = PopupPaint.Round(12 * scale);
        var label = PopupPaint.Round(11 * scale);
        var labelLine = TilePaint.LineHeight(gfx, label);
        var lineWidth = FolderFlyout.ListWidth(scale);
        var style = FolderFlyout.StyleFor(scale, look, theme);
        var states = States();
        var heights = new List<(string Label, float Height)> { ("its own height", StopwatchCard.RowHeight(style)) };
        heights.AddRange(Enumerable.Range(1, 6).Select(rows => ($"{rows} row{(rows == 1 ? "" : "s")}", RowsPx(rows, scale, 1))));
        var width = pad + (states.Length * (lineWidth + pad));
        var height = pad + heights.Sum(each => labelLine + each.Height + pad);
        Offscreen.Bitmap(width, height, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
            var y = pad;
            foreach (var (caption, lineHeight) in heights)
            {
                var x = pad;
                for (var column = 0; column < states.Length; column++)
                {
                    using var tile = Tile(gfx, states[column].State);
                    PopupPaint.Text(dc, gfx, column == 0 ? $"{caption} · {states[column].What}" : states[column].What, label, Weight.Regular, theme.TextSecondary, x, y);
                    var at = PopupPaint.Rect(x, y + labelLine, lineWidth, lineHeight);
                    PopupPaint.Stroke(dc, at, PopupPaint.Round(8 * scale), MathF.Max(1, MathF.Round(scale)), PopupPaint.Ink(theme.Stroke));
                    var context = dc;
                    Offscreen.Shifted(context, at.left, at.top, () => tile.PaintCard(context, PopupPaint.Rect(0, 0, lineWidth, lineHeight), style, Karpo.Dock.WidgetSurface.List));
                    x += lineWidth + pad;
                }
                y += labelLine + lineHeight + pad;
            }
        }, bitmap => Offscreen.Save(bitmap, path));
    }

    /// <summary>
    /// A line of one row at every list width, narrow first, in every state and a lap just taken: the
    /// lap under way in the name's place once there is a lap (stopwatch-one-row-*.png).
    /// </summary>
    private static void OneRow(Gfx gfx, string path, Look look, float scale)
    {
        var theme = FlyoutTheme.For(look);
        var pad = PopupPaint.Round(12 * scale);
        var label = PopupPaint.Round(11 * scale);
        var labelLine = TilePaint.LineHeight(gfx, label);
        var style = FolderFlyout.StyleFor(scale, look, theme);
        (string What, StopwatchState State)[] states = [.. States(), ("a lap just taken", StopwatchState.Start(Now - S(9.2)).Lap(Now))];
        var widths = new[] { Karpo.Core.FolderListWidth.Narrow, Karpo.Core.FolderListWidth.Medium, Karpo.Core.FolderListWidth.Wide };
        var lineHeight = RowsPx(1, scale, 1);
        var widest = FolderFlyout.ListWidth(scale, Karpo.Core.FolderListWidth.Wide);
        var width = pad + (states.Length * (widest + pad));
        var height = pad + (widths.Length * (labelLine + lineHeight + pad));
        Offscreen.Bitmap(width, height, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
            var y = pad;
            foreach (var listWidth in widths)
            {
                var lineWidth = FolderFlyout.ListWidth(scale, listWidth);
                var x = pad;
                for (var column = 0; column < states.Length; column++)
                {
                    using var tile = Tile(gfx, states[column].State);
                    PopupPaint.Text(dc, gfx, $"{listWidth} · {states[column].What}", label, Weight.Regular, theme.TextSecondary, x, y);
                    var at = PopupPaint.Rect(x, y + labelLine, lineWidth, lineHeight);
                    PopupPaint.Stroke(dc, at, PopupPaint.Round(8 * scale), MathF.Max(1, MathF.Round(scale)), PopupPaint.Ink(theme.Stroke));
                    var context = dc;
                    Offscreen.Shifted(context, at.left, at.top, () => tile.PaintCard(context, PopupPaint.Rect(0, 0, lineWidth, lineHeight), style, Karpo.Dock.WidgetSurface.List));
                    x += widest + pad;
                }
                y += labelLine + lineHeight + pad;
            }
        }, bitmap => Offscreen.Save(bitmap, path));
    }

    private static bool Inside(D2D_RECT_F rect, D2D_RECT_F room) =>
        rect.left >= room.left && rect.top >= room.top && rect.right <= room.right && rect.bottom <= room.bottom;

    private static bool Overlap(D2D_RECT_F a, D2D_RECT_F b) =>
        a.right > a.left && b.right > b.left && a.left < b.right - 0.5f && b.left < a.right - 0.5f && a.top < b.bottom - 0.5f && b.top < a.bottom - 0.5f;

    private static string Place(D2D_RECT_F rect) => $"{rect.left},{rect.top},{rect.right},{rect.bottom}";
}
