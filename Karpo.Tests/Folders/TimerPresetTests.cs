// The timer, the focus session and the stopwatch are presets (docs\widgets.md §92): what a click on
// each entry gave — its own panel, its card at full size — is a folder holding its line at the height
// of the whole card, under a face that says what the entry said: the glyph and the length set at
// rest, what is left while it runs, quieter while paused, the accent once over; the stopwatch what
// it has counted in whole seconds. The face reads the line's own countdown in the dock's store — one
// per widget — so the face, the line and a clock face over the same folder are one timer.
//
// Pictured beside the panels and entries they replace (rendered before they went:
// countdown-panel-*.png, stopwatch-panel-*.png, countdown-bar-*.png, stopwatch-bar-*.png):
// timer-preset-<dark|light>.png, stopwatch-preset-<dark|light>.png at 150 %, and the faces on the bar,
// time-faces-<dark|light>.png.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Folders.ListFormPictures;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

public sealed unsafe class TimerPresetTests
{
    private static readonly PresetMachine Machine = new(HasBattery: false, FixedDrives: []);

    private static readonly DateTime Frozen = new(2026, 10, 10, 14, 40, 7, DateTimeKind.Local);

    private static TimeSpan Minutes(double minutes) => TimeSpan.FromMinutes(minutes);

    /// <summary>The preset <paramref name="id"/>'s folder, as the add list makes it.</summary>
    private static BarItem Holder(string id) => Presets.Folder(Presets.Find(id)!, Machine);

    /// <summary>The line inside a preset's folder.</summary>
    private static CellItem LineOf(BarItem holder) => Assert.Single(holder.Folder!.Cells).Island;

    /// <summary>The preset's folder on the bar, running on <paramref name="countdowns"/> as the dock's runs on the dock's.</summary>
    private static FolderIsland Made(Gfx gfx, BarItem holder, Countdowns countdowns) =>
        new($"{Owner}/{holder.Id}", holder, gfx, new Config(), countdowns);

    // --- The presets ------------------------------------------------------------------------------------

    /// <summary>
    /// Each is its line alone under its own face, with no name and nothing to set — a countdown its one
    /// row, a kind of one form with no height (§432), the stopwatch the five rows the person gave it
    /// (2026-10-11). The panels had no footer, so there is none.
    /// </summary>
    [Theory]
    [InlineData(Presets.TimerId, FolderFaces.TimerId, "timer", 2)]
    [InlineData(Presets.FocusId, FolderFaces.FocusId, "focus", 2)]
    [InlineData(Presets.StopwatchId, FolderFaces.StopwatchId, "stopwatch", 10)]
    public void EachPresetIsItsLineUnderItsFace(string id, string face, string kind, int cells)
    {
        var preset = Presets.Find(id)!;
        Assert.Equal(face, preset.Face!.Kind);
        Assert.Equal(kind == "stopwatch" ? LineHeight.Exactly(cells) : null, Assert.Single(preset.Items).Height);
        Assert.Equal(kind, preset.Items[0].Kind);

        var folder = Holder(id).Folder!;
        Assert.Same(FolderFaces.Of(face), folder.Face.Info);
        Assert.Empty(folder.Face.Info.Options);
        var line = LineOf(Holder(id));
        Assert.Equal(kind, line.Kind);
        Assert.Equal(LineHeight.Exactly(cells), LineHeight.For(line));
        Assert.Same(FolderIsland.FaceLine(folder), folder.Cells[0].Island);
    }

    /// <summary>
    /// The entries are gone from the bar and the add list, and with them their panels; the lines are
    /// all there is, and a new bar has none of the three, as it never had the entries.
    /// </summary>
    [Fact]
    public void TheEntriesAreGone()
    {
        foreach (var kind in new[] { "timer", "focus", "stopwatch" })
        {
            Assert.Null(BarEntries.Find(kind));
            Assert.NotNull(ListWidgets.Find(kind));
            Assert.DoesNotContain(Catalogue.On(WidgetType.Bar), row => row.Key == $"bar/{kind}");
        }
        var bar = Sections.DefaultLayout([], new InputHardware(Pen: false, Touch: false, Slate: false), WindowsTaskbar.OutOfTheBox);
        Assert.DoesNotContain(bar, island => FolderFaces.LineOf(island.Folder?.Face.Info ?? FolderFaces.Icon) is not null);
    }

    /// <summary>Only a timer, focus or stopwatch face reads a line; the face reads the first of its kind, and none where there is none.</summary>
    [Fact]
    public void AFaceReadsTheFirstLineOfItsKind()
    {
        Assert.Same(WidgetInfos.Timer, FolderFaces.LineOf(FolderFaces.Timer));
        Assert.Same(WidgetInfos.Focus, FolderFaces.LineOf(FolderFaces.Focus));
        Assert.Same(WidgetInfos.Stopwatch, FolderFaces.LineOf(FolderFaces.Stopwatch));
        Assert.All(FolderFaces.All.Except([FolderFaces.Timer, FolderFaces.Focus, FolderFaces.Stopwatch]), face => Assert.Null(FolderFaces.LineOf(face)));

        var holder = Holder(Presets.TimerId);
        var first = LineOf(holder);
        var second = ListKind.Timer.New();
        holder.Folder!.Put(ListKind.Focus.New());
        holder.Folder!.Put(second);
        Assert.Same(first, FolderIsland.FaceLine(holder.Folder));
        holder.Folder.Face = FolderFace.Of(FolderFaces.FocusId);
        Assert.Equal("focus", FolderIsland.FaceLine(holder.Folder)!.Kind);
        holder.Folder.Face = FolderFace.Of(FolderFaces.StopwatchId);
        Assert.Null(FolderIsland.FaceLine(holder.Folder));
        holder.Folder.Face = FolderFace.Of(FolderFaces.ClockId);
        Assert.Null(FolderIsland.FaceLine(holder.Folder));
    }

    // --- What a face says --------------------------------------------------------------------------------

    /// <summary>
    /// A countdown face says what its entry said: the length set at rest ("12m", its kind's where none
    /// is set), what is left in five figures while it runs, the same quieter while paused, "00:00" in
    /// the accent once over — and from an hour up, hours and minutes. Never the tenths.
    /// </summary>
    [Fact]
    public void ACountdownFaceSaysWhatTheEntrySaid()
    {
        var timer = ListKind.Timer.New();
        var store = new Countdowns();
        TimeFaceReading Read() => CountdownFaceDrawing.Read(FolderFaces.TimerId, timer, store, Frozen, TimeSpan.Zero);

        Assert.Equal(new TimeFaceReading(S.WidgetCountdownBarMinutes(5), TimeFaceInk.Text, ""), Read());
        Assert.Equal(S.WidgetCountdownBarMinutes(25), CountdownFaceDrawing.Read(FolderFaces.FocusId, ListKind.Focus.New(), store, Frozen, TimeSpan.Zero).Text);
        Assert.Equal(S.WidgetCountdownBarMinutes(25), CountdownFaceDrawing.Read(FolderFaces.FocusId, null, null, Frozen, TimeSpan.Zero).Text);
        timer.Countdown = new CountdownOptions { Minutes = 12, Name = " Tea " };
        Assert.Equal(new TimeFaceReading(S.WidgetCountdownBarMinutes(12), TimeFaceInk.Text, "Tea"), Read());

        store.Start(timer.Id, CountdownKind.Timer, Minutes(12), Frozen.AddSeconds(-13));
        Assert.Equal(new TimeFaceReading("11:47", TimeFaceInk.Text, "Tea"), Read());
        store.Pause(timer.Id, Frozen);
        Assert.Equal(new TimeFaceReading("11:47", TimeFaceInk.Paused, "Tea"), Read());
        store.Set(timer.Id, Countdown.Start(CountdownKind.Timer, Minutes(12), Frozen.AddMinutes(-20)).At(Frozen));
        Assert.Equal(new TimeFaceReading("00:00", TimeFaceInk.Over, "Tea"), Read());
        store.Start(timer.Id, CountdownKind.Timer, Minutes(90), Frozen.AddMinutes(-5));
        Assert.Equal("01:25", Read().Text);
        store.Clear(timer.Id);
        Assert.Equal(S.WidgetCountdownBarMinutes(12), Read().Text);
    }

    /// <summary>
    /// A stopwatch face says what it has counted in whole seconds, as its entry did — never the
    /// tenths, which only the open folder's line runs — quieter while paused, nought at rest.
    /// </summary>
    [Fact]
    public void AStopwatchFaceSaysWholeSeconds()
    {
        var line = ListKind.Stopwatch.New();
        var store = new Countdowns();
        var now = TimeSpan.FromSeconds(500);
        TimeFaceReading Read() => CountdownFaceDrawing.Read(FolderFaces.StopwatchId, line, store, Frozen, now);

        Assert.Equal(new TimeFaceReading("00:00", TimeFaceInk.Text, ""), Read());
        store.Stopwatches.Act(line.Id, now - TimeSpan.FromSeconds(83.9));
        Assert.Equal(new TimeFaceReading("01:23", TimeFaceInk.Text, ""), Read());
        store.Stopwatches.Act(line.Id, now);
        Assert.Equal(new TimeFaceReading("01:23", TimeFaceInk.Paused, ""), Read());
        store.Stopwatches.Set(line.Id, StopwatchState.Start(now - TimeSpan.FromSeconds(3723.4)));
        Assert.Equal("01:02", Read().Text);
    }

    // --- One timer ---------------------------------------------------------------------------------------

    /// <summary>
    /// The face and the line in its folder are one timer: started from the line, the face counts; the
    /// face's menu starts, pauses, stops and steps the line's own countdown, as the line's own menu
    /// does in the open folder, and a minute stepped from it is the line's length. A clock face over
    /// the same folder shows the same countdown (§67). Nothing of its own is kept by the face.
    /// </summary>
    [UiFact]
    public void TheFaceAndItsLineAreOneTimer()
    {
        Offscreen.OnStaThread(gfx =>
        {
            Clock.Now = () => Frozen;
            try
            {
                var store = new Countdowns();
                var holder = Holder(Presets.TimerId);
                var line = LineOf(holder);
                using var folder = Made(gfx, holder, store);
                var tile = Assert.IsType<CountdownTile>(folder.FaceWidget);
                Assert.Equal(line.Id, tile.Id);
                Assert.Same(store, folder.Countdowns);

                // The face's own rows: the line's, worked on the line, then the Clock app.
                var (items, acts) = TheDock.BarMenuRows(holder, folder);
                // Nothing to stop yet: no Stop (§308).
                Assert.Equal([S.DockClockStart, S.DockWidgetMenuMinuteLonger, S.DockWidgetMenuMinuteShorter, S.DockWidgetMenuOpenClock], items.Select(item => item.Text));
                Assert.Same(tile, acts[0].Works);
                void Never(string target, string? verb) => Assert.Fail("a countdown's row opened something");
                TheDock.RunAct(acts.Single(row => row.Act == WidgetAct.Longer), folder.FaceWidget, Never);
                Assert.Equal(6, line.Countdown!.Minutes);
                TheDock.RunAct(acts.Single(row => row.Act == WidgetAct.StartPause), folder.FaceWidget, Never);
                Assert.Equal(CountdownPhase.Running, store.Of(line.Id).Phase);
                Assert.Equal(1, store.Keys.Count);
                Assert.Equal("06:00", CountdownFaceDrawing.Read(FolderFaces.TimerId, line, folder.Countdowns, Frozen, TimeSpan.Zero).Text);

                // Paused from the line: the face says so, in the quieter ink.
                _ = tile.Click((int)CountdownTargetKind.Action);
                Assert.Equal(TimeFaceInk.Paused, CountdownFaceDrawing.Read(FolderFaces.TimerId, line, store, Frozen, TimeSpan.Zero).Ink);
                (items, _) = TheDock.BarMenuRows(holder, folder);
                Assert.Equal(S.DockClockResume, items[0].Text);

                // A clock face over the same folder reads the same countdown.
                holder.Folder!.Face = ClockFace.New();
                folder.Configure(holder, new Config());
                Assert.Equal("06:00", folder.CountdownLeft());
                Assert.Equal([WindowsPages.DateTime.Title, S.DockWidgetMenuOpenClock], TheDock.BarMenuRows(holder, folder).Items.Select(item => item.Text));
                Assert.Null(folder.FaceWidget);
            }
            finally
            {
                Clock.Now = Clock.Real;
            }
        });
    }

    /// <summary>The stopwatch face's rows are its line's: start, lap and reset, each only where its button does something (§308).</summary>
    [UiFact]
    public void TheStopwatchFacesRowsAreItsLines()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var store = new Countdowns();
            var holder = Holder(Presets.StopwatchId);
            using var folder = Made(gfx, holder, store);
            var (items, acts) = TheDock.BarMenuRows(holder, folder);
            Assert.Equal([S.DockClockStart, S.DockWidgetMenuOpenClock], items.Select(item => item.Text));
            TheDock.RunAct(acts[0], folder.FaceWidget, (_, _) => Assert.Fail("a stopwatch's row opened something"));
            Assert.True(store.Stopwatches.Of(LineOf(holder).Id).Running);
            (items, _) = TheDock.BarMenuRows(holder, folder);
            Assert.Equal(4, items.Count);
            Assert.All(items, item => Assert.True(item.Enabled));
        });
    }

    /// <summary>
    /// The face is painted again in place on the dock's second while it runs — never a rebuild, never
    /// another size — and not at all where nothing changed; a countdown starting, which says more, lays
    /// the bar out again.
    /// </summary>
    [UiFact]
    public void TheFacePaintsAgainInPlaceEachSecond()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var now = Frozen;
            Clock.Now = () => now;
            try
            {
                var store = new Countdowns();
                var holder = Holder(Presets.TimerId);
                var line = LineOf(holder);
                using var island = Made(gfx, holder, store);
                Assert.False(island.FaceSeconds);
                var config = new Config { Height = 48 };
                var style = new IslandStyle(1f, false, Layout.CellPx(96, config), Layout.IconPx(96, config), LookFor(dark: true));
                var size = island.CellSize(0, style);
                _ = island.Measure(style);
                _ = island.Surface(0, style);
                Assert.StartsWith($"{S.WidgetCountdownBarMinutes(5)}|", island.FaceShown, StringComparison.Ordinal);
                var painted = 0;
                PaintWatch.Watching = (_, _, color) =>
                {
                    painted++;
                    return color;
                };
                try
                {
                    Assert.False(island.FaceTick(gfx));
                    Assert.Equal(0, painted);

                    // "5m" to "05:00" is wider (docs\widgets.md §244): the bar is laid out again, and
                    // the face painted in its new cell.
                    store.Start(line.Id, CountdownKind.Timer, Minutes(5), now);
                    Assert.True(island.FaceTick(gfx));
                    Assert.True(island.CellSize(0, style).Width > size.Width, "a countdown started is no wider than one at rest");
                    size = island.CellSize(0, style);
                    island.Forget();
                    _ = island.Measure(style);
                    _ = island.Surface(0, style);
                    Assert.True(painted > 0, "a countdown started was not painted");
                    Assert.StartsWith("05:00|", island.FaceShown, StringComparison.Ordinal);

                    // Then each second in place, in one width.
                    (painted, now) = (0, now.AddSeconds(1));
                    Assert.False(island.FaceTick(gfx));
                    Assert.True(painted > 0, "a second gone was not painted");
                    Assert.StartsWith("04:59|", island.FaceShown, StringComparison.Ordinal);
                    Assert.Equal(size, island.CellSize(0, style));

                    painted = 0;
                    Assert.False(island.FaceTick(gfx));
                    Assert.Equal(0, painted);
                }
                finally
                {
                    PaintWatch.Watching = null;
                }
            }
            finally
            {
                Clock.Now = Clock.Real;
            }
        });
    }

    /// <summary>
    /// A face across a bar is as wide as what it says (docs\widgets.md §244): running, paused and over
    /// one width, its figures tabular, so a running one never moves the bar as it counts; at rest no
    /// wider. Down a bar on its side the rows it says now (§275), so at rest no longer either. On every bar, at every text size, the
    /// glyph, a name given to the line and the figures stand inside it in that order.
    /// </summary>
    [UiFact]
    public void AFaceIsAsWideAsWhatItSaysAndCountsInOneWidth()
    {
        Offscreen.OnStaThread(gfx =>
        {
            Clock.Now = () => Frozen;
            try
            {
                foreach (var id in new[] { Presets.TimerId, Presets.FocusId, Presets.StopwatchId })
                {
                    foreach (var named in new[] { false, true })
                    {
                        if (named && id == Presets.StopwatchId)
                        {
                            continue;
                        }
                        foreach (var height in new[] { 32, 48, 72 })
                        {
                            foreach (var vertical in new[] { false, true })
                            {
                                foreach (var text in new[] { 1f, 2.25f })
                                {
                                    var config = new Config { Height = height };
                                    var style = new IslandStyle(1.5f, vertical, Layout.CellPx(144, config), Layout.IconPx(144, config), LookFor(true, text));
                                    var sizes = States(id).Select(state =>
                                    {
                                        var holder = Holder(id);
                                        if (named)
                                        {
                                            LineOf(holder).Countdown = new CountdownOptions { Name = "Tea" };
                                        }
                                        var store = new Countdowns();
                                        state(store, LineOf(holder).Id);
                                        using var island = Made(gfx, holder, store);
                                        var size = island.CellSize(0, style);
                                        var boxes = Watched(island, style, size);
                                        var where = $"{id}{(named ? ", named" : "")}, {height} DIP, text ×{text}{(vertical ? ", vertical" : "")}";
                                        // Along the bar, every piece stays inside the face's cells.
                                        Assert.All(boxes, box => Assert.True(vertical
                                            ? box.top >= -0.5f && box.bottom <= size.Height + 0.5f
                                            : box.left >= -0.5f && box.right <= size.Width + 0.5f,
                                            $"{where}: a piece runs out of its cell"));
                                        if (!vertical)
                                        {
                                            for (var i = 1; i < boxes.Count; i++)
                                            {
                                                Assert.True(boxes[i - 1].right <= boxes[i].left + 0.5f, $"{where}: piece {i} stands on the one before it");
                                            }
                                        }
                                        return size;
                                    }).ToList();
                                    var said = $"{id}{(named ? ", named" : "")}, {height} DIP, text ×{text}{(vertical ? ", vertical" : "")}: {string.Join(", ", sizes)}";
                                    if (vertical)
                                    {
                                        // The rows it says now (§275): a running one's two figures over each
                                        // other, or one row — the length set at rest ("10m"), the name once over —
                                        // so two lengths at the most, running and paused one of them.
                                        Assert.True(sizes.Select(size => size.Height).Distinct().Count() <= 2, said);
                                        Assert.True(sizes[1] == sizes[2] && sizes[0].Height <= sizes[1].Height, said);
                                    }
                                    else
                                    {
                                        // Five figures, running, paused or over, are one width (tabular); a countdown at
                                        // rest says its length ("10m"), no wider; a stopwatch at rest says "00:00".
                                        var five = id == Presets.StopwatchId ? sizes[..3] : sizes[1..4];
                                        Assert.True(five.Distinct().Count() == 1, said);
                                        Assert.True(sizes[0].Width <= five[0].Width, said);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                Clock.Now = Clock.Real;
            }
        });
    }

    /// <summary>What its line can be doing, set on a store.</summary>
    private static IEnumerable<Action<Countdowns, string>> States(string id)
    {
        if (id == Presets.StopwatchId)
        {
            var counted = StopwatchClock.Now();
            yield return (_, _) => { };
            yield return (store, key) => store.Stopwatches.Set(key, StopwatchState.Start(counted - TimeSpan.FromSeconds(83.4)));
            yield return (store, key) => store.Stopwatches.Set(key, StopwatchState.Start(counted - TimeSpan.FromSeconds(83.4)).Pause(counted));
            yield return (store, key) => store.Stopwatches.Set(key, StopwatchState.Start(counted - TimeSpan.FromHours(30)));
            yield break;
        }
        var kind = id == Presets.FocusId ? CountdownKind.Focus : CountdownKind.Timer;
        var running = Countdown.Start(kind, Minutes(10), Frozen.AddSeconds(-133));
        yield return (_, _) => { };
        yield return (store, key) => store.Set(key, running);
        yield return (store, key) => store.Set(key, running.Pause(Frozen));
        yield return (store, key) => store.Set(key, Countdown.Start(kind, Minutes(10), Frozen.AddMinutes(-11)).At(Frozen));
        yield return (store, key) => store.Set(key, Countdown.Start(kind, Minutes(720), Frozen));
    }

    /// <summary>Each glyph's and line's box the face paints in its cell.</summary>
    private static List<D2D_RECT_F> Watched(FolderIsland island, IslandStyle style, (float Width, float Height) size)
    {
        var boxes = new List<D2D_RECT_F>();
        Offscreen.Bitmap(size.Width, size.Height, dc =>
        {
            PaintWatch.Watching = (box, _, color) =>
            {
                boxes.Add(box);
                return color;
            };
            try
            {
                island.PaintCell(dc, 0, style, size.Width, size.Height);
            }
            finally
            {
                PaintWatch.Watching = null;
            }
        }, _ => { });
        return boxes;
    }

    /// <summary>The hover says the line's name or its kind's and what it is doing.</summary>
    [UiFact]
    public void TheHoverSaysWhatItIsDoing()
    {
        Offscreen.OnStaThread(gfx =>
        {
            Clock.Now = () => Frozen;
            try
            {
                var store = new Countdowns();
                var holder = Holder(Presets.TimerId);
                var line = LineOf(holder);
                using var island = Made(gfx, holder, store);
                Assert.Equal($"{S.DockClockTimer}\n{Countdown.Words(Minutes(5))}", island.Tooltip(0));
                line.Countdown = new CountdownOptions { Minutes = 12, Name = "Tea" };
                store.Start(line.Id, CountdownKind.Timer, Minutes(12), Frozen.AddSeconds(-13));
                Assert.Equal("Tea\n11:47", island.Tooltip(0));
                store.Set(line.Id, Countdown.Start(CountdownKind.Timer, Minutes(12), Frozen.AddMinutes(-20)).At(Frozen));
                Assert.Equal($"Tea\n{S.DockClockTimerOver}", island.Tooltip(0));

                var watch = Holder(Presets.StopwatchId);
                using var stopwatch = Made(gfx, watch, store);
                Assert.StartsWith($"{S.DockStopwatchTitle}\n0", stopwatch.Tooltip(0), StringComparison.Ordinal);
            }
            finally
            {
                Clock.Now = Clock.Real;
            }
        });
    }

    /// <summary>
    /// A lap's name and total never stand on its length, at any width the line may have and
    /// at a large text, where the time sits near the line's end: the length moves left before the
    /// total, and where there is no room for all three the total gives way. A regression: at 225 %%
    /// text the stopwatch preset drew a lap's length over its total.
    /// </summary>
    [UiFact]
    public void ALapsFiguresNeverMeet()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var counted = TimeSpan.FromSeconds(4000);
            var state = StopwatchState.Start(counted - TimeSpan.FromSeconds(3723.4)).Lap(counted - TimeSpan.FromSeconds(70)).Lap(counted - TimeSpan.FromSeconds(3.9));
            foreach (var text in new[] { 1f, 1.5f, 2.25f })
            {
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    var look = LookFor(true, text);
                    var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
                    for (var width = 160f * scale; width <= 480f * scale; width += 20 * scale)
                    {
                        // A lap's row: its name and where it ended after the text edge, how long it was against
                        // the trailing gutter, the name cut where the two would meet (LineRow).
                        var sketch = new LineSketch(null, gfx);
                        StopwatchCard.Draw(sketch, PopupPaint.Rect(0, 0, width, Rows(4, scale, text)), style, new StopwatchInputs(state, counted));
                        var names = sketch.Pieces.Where(piece => piece.Name == "laps").ToList();
                        var lengths = sketch.Pieces.Where(piece => piece.Name == "lap length").ToList();
                        var right = width - ListForm.Of(gfx, style).Inset;
                        for (var index = 0; index < lengths.Count; index++)
                        {
                            var where = $"{width} px at {scale}x, text {text}: lap {index}";
                            Assert.True(lengths[index].Box.right <= right + 1, $"{where}: its length runs off the line");
                            if (index < names.Count)
                            {
                                Assert.True(names[index].Box.right <= lengths[index].Box.left, $"{where}: its name meets its length");
                            }
                        }
                    }
                }
            }
        });
    }

    // --- Pictures ----------------------------------------------------------------------------------------

    /// <summary>
    /// The timer and focus presets opened, at rest, running, paused, over and named, and at 225 %
    /// text; the stopwatch's at rest, running with laps and paused — at 150 %, to stand beside
    /// countdown-panel-*.png and stopwatch-panel-*.png of the panels they replace.
    /// </summary>
    [UiFact]
    public void TimePresetPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                Clock.Now = () => Frozen;
                var folders = new List<FolderIsland>();
                try
                {
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    void Add(List<(string, Look, FolderIsland, FolderPlan)> into, string caption, string id, Action<Countdowns, CellItem> set, float text = 1f)
                    {
                        var look = LookFor(dark, text);
                        var holder = Holder(id);
                        var store = new Countdowns();
                        set(store, LineOf(holder));
                        var folder = Made(gfx, holder, store);
                        folder.Opened(gfx);
                        folders.Add(folder);
                        into.Add((caption, look, folder, Plan(folder, look, 1.5f).Plan));
                    }

                    var running = Countdown.Start(CountdownKind.Timer, Minutes(10), Frozen.AddSeconds(-133));
                    Add(panels, "Timer, at rest", Presets.TimerId, (_, _) => { });
                    Add(panels, "Running", Presets.TimerId, (store, line) => store.Set(line.Id, running));
                    Add(panels, "Paused", Presets.TimerId, (store, line) => store.Set(line.Id, running.Pause(Frozen.AddSeconds(30))));
                    Add(panels, "Over, named", Presets.TimerId, (store, line) =>
                    {
                        line.Countdown = new CountdownOptions { Minutes = 10, Name = "Tea" };
                        store.Set(line.Id, Countdown.Start(CountdownKind.Timer, Minutes(10), Frozen.AddMinutes(-11)).At(Frozen));
                    });
                    Add(panels, "Focus, running", Presets.FocusId, (store, line) => store.Set(line.Id, Countdown.Start(CountdownKind.Focus, Minutes(25), Frozen.AddMinutes(-3))));
                    Add(panels, "Timer: 225 % text", Presets.TimerId, (_, _) => { }, 2.25f);
                    Sheet(gfx, $"timer-preset-{(dark ? "dark" : "light")}.png", 1.5f, panels);

                    var counted = StopwatchClock.Now();
                    var watches = new List<(string, Look, FolderIsland, FolderPlan)>();
                    var laps = StopwatchState.Start(counted - TimeSpan.FromSeconds(83.4)).Lap(counted - TimeSpan.FromSeconds(70)).Lap(counted - TimeSpan.FromSeconds(41.2)).Lap(counted - TimeSpan.FromSeconds(3.9));
                    Add(watches, "Stopwatch, at rest", Presets.StopwatchId, (_, _) => { });
                    Add(watches, "Running, three laps", Presets.StopwatchId, (store, line) => store.Stopwatches.Set(line.Id, laps));
                    Add(watches, "Paused, four laps", Presets.StopwatchId, (store, line) => store.Stopwatches.Set(line.Id, laps.Lap(counted - TimeSpan.FromSeconds(1)).Pause(counted)));
                    Add(watches, "225 % text", Presets.StopwatchId, (store, line) => store.Stopwatches.Set(line.Id, laps), 2.25f);
                    Sheet(gfx, $"stopwatch-preset-{(dark ? "dark" : "light")}.png", 1.5f, watches);
                }
                finally
                {
                    foreach (var folder in folders)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                    Clock.Now = Clock.Real;
                }
            });
        }
    }

    /// <summary>
    /// The faces on a bar of 40 and 48 DIP at 100 %, 48 at 150 % and down a vertical one, a row per
    /// state — time-faces-&lt;dark|light&gt;.png, beside countdown-bar-*.png and stopwatch-bar-*.png
    /// of the entries before they went.
    /// </summary>
    [UiFact]
    public void TimeFacePictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                Clock.Now = () => Frozen;
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                var styles = new[] { (40, 96u, false), (48, 96u, false), (48, 144u, false), (48, 96u, true) }
                    .Select(each => new IslandStyle(each.Item2 / 96f, each.Item3, Layout.CellPx(each.Item2, new Config { Height = each.Item1 }), Layout.IconPx(each.Item2, new Config { Height = each.Item1 }), look))
                    .ToList();
                var islands = new List<FolderIsland>();
                foreach (var id in new[] { Presets.TimerId, Presets.FocusId, Presets.StopwatchId })
                {
                    foreach (var state in States(id))
                    {
                        var holder = Holder(id);
                        var store = new Countdowns();
                        state(store, LineOf(holder).Id);
                        islands.Add(Made(gfx, holder, store));
                    }
                }
                var named = Holder(Presets.TimerId);
                LineOf(named).Countdown = new CountdownOptions { Name = "Tea" };
                var namedStore = new Countdowns();
                namedStore.Set(LineOf(named).Id, Countdown.Start(CountdownKind.Timer, Minutes(10), Frozen.AddSeconds(-133)));
                islands.Add(Made(gfx, named, namedStore));
                try
                {
                    const float gap = 12;
                    var columns = styles.Select(style => islands.Max(island => island.CellSize(0, style).Width)).ToList();
                    var rows = islands.Select(island => styles.Max(style => island.CellSize(0, style).Height)).ToList();
                    var width = gap + columns.Sum(column => column + gap);
                    var height = gap + rows.Sum(row => row + gap);
                    Offscreen.Bitmap(width, height, dc =>
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
                        var y = gap;
                        for (var row = 0; row < islands.Count; row++)
                        {
                            var x = gap;
                            for (var column = 0; column < styles.Count; column++)
                            {
                                var style = styles[column];
                                var island = islands[row];
                                var (w, h) = island.CellSize(0, style);
                                PopupPaint.Fill(dc, PopupPaint.Rect(x, y, w, h), 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                                Offscreen.Shifted(dc, x, y, () => island.PaintCell(dc, 0, style, w, h));
                                x += columns[column] + gap;
                            }
                            y += rows[row] + gap;
                        }
                    }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"time-faces-{(dark ? "dark" : "light")}.png")));
                }
                finally
                {
                    foreach (var island in islands)
                    {
                        island.Dispose();
                    }
                    Clock.Now = Clock.Real;
                }
            });
        }
    }
}
