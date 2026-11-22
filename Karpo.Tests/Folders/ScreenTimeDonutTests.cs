// Screen time's ring (Karpo.Dock\ScreenTime\ScreenTimeDonut.cs): the pieces' arithmetic — the top
// apps, "Other", a sliver folded into it, the angles round once — and which day it shows: the day
// pointed at in the total's bars, else the folder's day, else today, back to the day shown as the
// pointer leaves. A piece under the pointer lights, says its app, time and share, and is the folder's
// app pointed at; a click picks it. Every height from three rows to six at three scales and both text
// sizes stays in its line with no two words on each other. Pictured to KARPO_PNG_DIR:
// screentime-ring-<dark|light>-<100|150>.png and the ready-made folder screentime-ring-folder-*.png.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Folders.ListFormPictures;
using static Karpo.Tests.Folders.ScreenTimeLinesTests;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

[Collection(ScreenTimeLinesTests.Names)]
public sealed unsafe class ScreenTimeDonutTests
{
    private static readonly DateOnly Day = new(2026, 10, 9);

    /// <summary>A day of <paramref name="total"/> hours with <paramref name="apps"/> hours each.</summary>
    private static ScreenTimeLog LogOf(double total, params (string App, double Hours)[] apps)
    {
        var log = new ScreenTimeLog();
        var day = log.Ensure(Day);
        day.Total = TimeSpan.FromHours(total);
        foreach (var (app, hours) in apps)
        {
            day.Apps[app] = TimeSpan.FromHours(hours);
        }
        return log;
    }

    // --- The pieces ---------------------------------------------------------------------------------

    /// <summary>The five top apps most used first, the rest and the time for no app as "Other"; round once, each from where the last ended.</summary>
    [Fact]
    public void TheTopAppsAndTheRestGoRoundOnce()
    {
        var ring = ScreenTimeRing.Of(LogOf(10, ("a", 3), ("b", 2), ("c", 1.5), ("d", 1), ("e", 0.8), ("f", 0.6), ("g", 0.3)), Day);

        Assert.Equal(TimeSpan.FromHours(10), ring.Total);
        Assert.Equal(["a", "b", "c", "d", "e"], ring.Top);
        Assert.Equal(6, ring.Slices.Count);
        Assert.Equal([0, 1, 2, 3, 4, ScreenTimeLine.OtherPart], ring.Slices.Select(slice => slice.Part));
        // "Other": f, g and the 0.8 h counted for no app.
        Assert.Null(ring.Slices[^1].App);
        Assert.Equal(TimeSpan.FromHours(1.7), ring.Slices[^1].Time);
        Assert.Equal(0, ring.Slices[0].Start);
        Assert.Equal(108, ring.Slices[0].Sweep, 3);
        for (var index = 1; index < ring.Slices.Count; index++)
        {
            Assert.Equal(ring.Slices[index - 1].Start + ring.Slices[index - 1].Sweep, ring.Slices[index].Start, 3);
        }
        Assert.Equal(360f, ring.Slices[^1].Start + ring.Slices[^1].Sweep);
        Assert.Equal(1, ring.Slices.Sum(slice => slice.Share), 3);
    }

    /// <summary>An app under 2 % of the day is a sliver nobody could point at: it is in "Other", and an app under a minute never has a piece.</summary>
    [Fact]
    public void ASliverIsInOther()
    {
        var ring = ScreenTimeRing.Of(LogOf(10, ("a", 6), ("b", 3.85), ("c", 0.15)), Day);

        Assert.Equal(["a", "b"], ring.Top);
        Assert.Equal(TimeSpan.FromHours(0.15), ring.Slices[^1].Time);
        Assert.Equal(ScreenTimeLine.OtherPart, ring.Slices[^1].Part);

        var seconds = ScreenTimeRing.Of(LogOf(0.01, ("a", 0.01)), Day);
        Assert.Empty(seconds.Top);
        Assert.Single(seconds.Slices);
        Assert.Equal(360, seconds.Slices[0].Sweep);
    }

    /// <summary>A day all one app is one piece and no "Other"; a day with none has no pieces; a total under its apps (a hand-edited file) is their sum.</summary>
    [Fact]
    public void OneAppNoneAndATotalUnderItsApps()
    {
        var one = ScreenTimeRing.Of(LogOf(2, ("a", 2)), Day);
        Assert.Single(one.Slices);
        Assert.Equal(("a", 0f, 360f), (one.Slices[0].App, one.Slices[0].Start, one.Slices[0].Sweep));

        var none = ScreenTimeRing.Of(new ScreenTimeLog(), Day);
        Assert.Equal(TimeSpan.Zero, none.Total);
        Assert.Empty(none.Slices);
        Assert.Equal(-1, none.SliceAt(10));

        var under = ScreenTimeRing.Of(LogOf(1, ("a", 2), ("b", 1)), Day);
        Assert.Equal(TimeSpan.FromHours(3), under.Total);
        Assert.Equal(2, under.Slices.Count);
        Assert.DoesNotContain(under.Slices, slice => slice.Part == ScreenTimeLine.OtherPart);
    }

    /// <summary>The piece at an angle: twelve o'clock is the first's start, the angles wrap, a piece's end is the next's.</summary>
    [Fact]
    public void ThePieceAtAnAngle()
    {
        var ring = ScreenTimeRing.Of(LogOf(4, ("a", 2), ("b", 1), ("c", 1)), Day);

        Assert.Equal(0, ring.SliceAt(0));
        Assert.Equal(0, ring.SliceAt(179.9f));
        Assert.Equal(1, ring.SliceAt(180));
        Assert.Equal(2, ring.SliceAt(-1));
        Assert.Equal(2, ring.SliceAt(359.99f));
        Assert.Equal(0, ring.SliceAt(360));
        Assert.Equal(1, ring.SliceAt(-135));
    }

    // --- The day ------------------------------------------------------------------------------------

    /// <summary>The day pointed at before the day picked, the day picked before today.</summary>
    [Fact]
    public void TheDayIsThePointersThenThePickedThenToday()
    {
        var today = new DateOnly(2026, 10, 12);
        Assert.Equal(today, ScreenTimeDonut.DayOf(null, null, today));
        Assert.Equal(today.AddDays(-3), ScreenTimeDonut.DayOf(null, today.AddDays(-3), today));
        Assert.Equal(today.AddDays(-5), ScreenTimeDonut.DayOf(today.AddDays(-5), today.AddDays(-3), today));
        Assert.Equal(today.AddDays(-5), ScreenTimeDonut.DayOf(today.AddDays(-5), null, today));
    }

    private static D2D_RECT_F BoxOf(int rows, float scale, float textScale = 1f) =>
        PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(scale), Rows(rows, scale, textScale));

    private static TileStyle StyleAt(float scale, float textScale = 1f, bool dark = true)
    {
        var look = LookFor(dark, textScale);
        return FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
    }

    private static Vector2 Middle(D2D_RECT_F rect) => new((rect.left + rect.right) / 2, (rect.top + rect.bottom) / 2);

    /// <summary>The history over the ring (§603), both reading the made-up fortnight, opened.</summary>
    private static FolderIsland TotalAndRing(Gfx gfx, Action<CellItem>? ring = null)
    {
        var days = Fortnight();
        Clock.Now = () => Today.ToDateTime(new TimeOnly(10, 30));
        return ListFormPictures.Folder(gfx, made =>
        {
            foreach (var tile in made.Tiles)
            {
                Feed(tile.Island, days);
            }
        }, Line(ListKind.ScreenTimeHistory), Line(ListKind.ScreenTimeChart, ring));
    }

    /// <summary>
    /// In a folder: today; the day the total steps to; while the pointer is on a bar, that bar's day,
    /// live from one bar to the next; the day shown again as it leaves the bars; nothing pointed at
    /// once the folder closes.
    /// </summary>
    [UiFact]
    public void TheRingFollowsTheDayShownAndTheDayPointedAt()
    {
        Standing(gfx =>
        {
            var folder = TotalAndRing(gfx);
            try
            {
                var total = (ScreenTimeLine)folder.Tiles[0].Island;
                var ring = (ScreenTimeDonut)folder.Tiles[1].Island;
                var style = StyleAt(1f);
                var box = ScreenTimeLinesTests.FormBox(total, 1f);
                Assert.Equal(Today, ring.Shown);

                total.Click(ScreenTimeLine.PreviousTarget);
                Assert.Equal(Today.AddDays(-1), ring.Shown);

                var bars = total.TotalPlan(box, style).Bars;
                total.HoverOn(Middle(bars[^5].Column), box, style, WidgetSurface.List);
                Assert.Equal(bars[^5].Day, folder.ChannelValue(Channels.DayHover));
                Assert.Equal(bars[^5].Day, ring.Shown);
                Assert.Equal(bars[^5].Day, ring.Ring.Day);

                // A piece of another bar: that bar's day, as the pointer moves.
                var piece = bars[^3].Segments[0];
                total.HoverOn(Middle(piece.Box), box, style, WidgetSurface.List);
                Assert.Equal(bars[^3].Day, ring.Shown);

                // Off the bars: the day shown again.
                total.HoverOn(new Vector2(box.left + 2, box.top + 2), box, style, WidgetSurface.List);
                Assert.Null(folder.ChannelValue(Channels.DayHover));
                Assert.Equal(Today.AddDays(-1), ring.Shown);

                total.HoverOn(Middle(bars[^5].Column), box, style, WidgetSurface.List);
                total.HoverOn(null, box, style, WidgetSurface.List);
                Assert.Equal(Today.AddDays(-1), ring.Shown);

                total.HoverOn(Middle(bars[^5].Column), box, style, WidgetSurface.List);
                folder.Closed();
                Assert.Null(folder.ChannelValue(Channels.DayHover));
            }
            finally
            {
                folder.Dispose();
                Clock.Now = Clock.Real;
            }
        });
    }

    /// <summary>
    /// A piece under the pointer is the folder's app pointed at and says its app, time and share; its
    /// legend row is the same piece; "Other" is no app; a click picks the app and a second lets it go;
    /// the app pointed at in the total's bars lights its piece here.
    /// </summary>
    [UiFact]
    public void APieceLightsSaysItsShareAndPicksItsApp()
    {
        Standing(gfx =>
        {
            var folder = TotalAndRing(gfx);
            try
            {
                var total = (ScreenTimeLine)folder.Tiles[0].Island;
                var ring = (ScreenTimeDonut)folder.Tiles[1].Island;
                var style = StyleAt(1.5f);
                var box = BoxOf(4, 1.5f);
                var plan = ring.Plan(box, style);
                Assert.True(plan.Beside);
                Assert.Equal(6, plan.Ring.Slices.Count);
                Assert.Equal(6, plan.Legend.Count);

                var first = Middle(plan.PieceBox(0));
                ring.HoverOn(first, box, style, WidgetSurface.List);
                Assert.Equal(ScreenTimeDonut.SliceTarget, ring.Hot);
                Assert.Equal(new AppId(Browser), folder.ChannelValue(Channels.AppHover));
                Assert.Equal((0, -1), ring.Lit(plan.Ring));
                var tip = ring.CardTip(first, box, style, WidgetSurface.List);
                Assert.NotNull(tip);
                var share = (int)MathF.Round(plan.Ring.Slices[0].Share * 100);
                Assert.Equal(S.WidgetScreenTimeShareTip(name: "Browser", time: ScreenTimeText.Duration(plan.Ring.Slices[0].Time), percent: share), tip!.Value.Text);

                // Its legend row is the same piece.
                Assert.Equal(ScreenTimeDonut.SliceTarget, plan.TargetAt(Middle(plan.Legend[0].Box)));
                // "Other": no app pointed at, but a tip all the same.
                var other = Middle(plan.PieceBox(5));
                ring.HoverOn(other, box, style, WidgetSurface.List);
                Assert.Equal(ScreenTimeDonut.SliceTarget + 5, ring.Hot);
                Assert.Null(folder.ChannelValue(Channels.AppHover));
                Assert.StartsWith(S.WidgetScreenTimeOther, ring.CardTip(other, box, style, WidgetSurface.List)!.Value.Text);
                // The middle of the ring is no piece.
                ring.HoverOn(plan.Centre, box, style, WidgetSurface.List);
                Assert.Equal(ScreenTimeDonut.NoTarget, ring.Hot);

                // A click picks, a second lets go.
                ring.Click(ScreenTimeDonut.SliceTarget + 1);
                Assert.Equal(new AppId(plan.Ring.Slices[1].App!), folder.ChannelValue(Channels.App));
                Assert.Equal((-1, 1), ring.Lit(plan.Ring));
                ring.Click(ScreenTimeDonut.SliceTarget + 1);
                Assert.Null(folder.ChannelValue(Channels.App));
                ring.Click(ScreenTimeDonut.SliceTarget + 5);
                Assert.Null(folder.ChannelValue(Channels.App));

                // An app pointed at in the bars lights its piece.
                var totalBox = ScreenTimeLinesTests.FormBox(total, 1.5f);
                var bar = total.TotalPlan(totalBox, style).Bars[^1];
                var segment = bar.Segments.First(each => each.Part == 0);
                total.HoverOn(Middle(segment.Box), totalBox, style, WidgetSurface.List);
                var chart = total.TotalPlan(totalBox, style).Chart!;
                Assert.Equal(ring.Ring.Slices.ToList().FindIndex(slice => string.Equals(slice.App, chart.Top[0], StringComparison.OrdinalIgnoreCase)), ring.Lit(ring.Ring).Pointed);
            }
            finally
            {
                folder.Closed();
                folder.Dispose();
                Clock.Now = Clock.Real;
            }
        });
    }

    /// <summary>An app wears the bars' colour in the ring: the same palette place for every app the two share.</summary>
    [UiFact]
    public void AnAppWearsTheBarsColour()
    {
        Standing(gfx =>
        {
            var folder = TotalAndRing(gfx);
            try
            {
                var total = (ScreenTimeLine)folder.Tiles[0].Island;
                var ring = (ScreenTimeDonut)folder.Tiles[1].Island;
                var style = StyleAt(1f);
                var bars = total.TotalPlan(BoxOf(4, 1f), style);
                var plan = ring.Plan(BoxOf(4, 1f), style);
                var shared = 0;
                for (var part = 0; part < plan.Ring.Top.Count; part++)
                {
                    var inBars = ScreenTimeLine.PartOf(bars.Chart!, plan.Ring.Top[part]);
                    if (inBars >= 0)
                    {
                        Assert.Equal(bars.Slots[inBars], plan.Slots[part]);
                        shared++;
                    }
                }
                Assert.True(shared >= 3);
                Assert.Equal(plan.Slots.Length, plan.Slots.Distinct().Count());
            }
            finally
            {
                folder.Closed();
                folder.Dispose();
                Clock.Now = Clock.Real;
            }
        });
    }

    // --- Every height -------------------------------------------------------------------------------

    /// <summary>
    /// From three rows to six, at 100, 150 and 200 % and at the largest text, on today, an older day and
    /// a day with nothing: all of it inside its line, no two words on each other, and the ring there.
    /// </summary>
    [UiFact]
    public void EveryHeightStaysInItsLineAndNothingOverlaps()
    {
        var wrong = new List<string>();
        Standing(gfx =>
        {
            var folder = TotalAndRing(gfx);
            try
            {
                var ring = (ScreenTimeDonut)folder.Tiles[1].Island;
                foreach (var day in new[] { Today, Today.AddDays(-3), Today.AddDays(-30) })
                {
                    ((IWidgetContainer)folder).Publish(Channels.Day, day);
                    foreach (var scale in new[] { 1f, 1.5f, 2f })
                    {
                        foreach (var textScale in TextScales)
                        {
                            for (var rows = 3; rows <= 6; rows++)
                            {
                                var at = $"{day} at {rows} rows, {scale * 100:0} %, text {textScale * 100:0} %";
                                var box = BoxOf(rows, scale, textScale);
                                var style = StyleAt(scale, textScale);
                                var plan = ring.Plan(box, style);
                                if (plan.Outer < 10)
                                {
                                    wrong.Add($"{at}: a ring of {plan.Outer:0} px");
                                }
                                if (plan.Centre.Y - plan.Outer - plan.Pop < box.top - 0.5f || plan.Centre.Y + plan.Outer + plan.Pop > box.bottom + 0.5f
                                    || plan.Centre.X - plan.Outer - plan.Pop < box.left - 0.5f || plan.Centre.X + plan.Outer + plan.Pop > box.right + 0.5f)
                                {
                                    wrong.Add($"{at}: the ring leaves its line");
                                }
                                var (ink, pieces) = Draw(ring, box, style);
                                if (ink.Left < -0.5f || ink.Top < -0.5f || ink.Right > box.right + 0.5f || ink.Bottom > box.bottom + 0.5f)
                                {
                                    wrong.Add($"{at}: ink at {ink.Left:0},{ink.Top:0}..{ink.Right:0},{ink.Bottom:0} outside {box.right:0}x{box.bottom:0}");
                                }
                                for (var one = 0; one < pieces.Count; one++)
                                {
                                    for (var two = one + 1; two < pieces.Count; two++)
                                    {
                                        if (Over(pieces[one], pieces[two]))
                                        {
                                            wrong.Add($"{at}: words at {pieces[one].left:0},{pieces[one].top:0} and {pieces[two].left:0},{pieces[two].top:0} overlap");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                folder.Closed();
                folder.Dispose();
                Clock.Now = Clock.Real;
            }
        });
        Assert.True(wrong.Count == 0, $"{wrong.Count} are wrong:\n{string.Join("\n", wrong.Take(60))}");
    }

    private static bool Over(D2D_RECT_F one, D2D_RECT_F two) =>
        MathF.Min(one.right, two.right) - MathF.Max(one.left, two.left) > 1
        && MathF.Min(one.bottom, two.bottom) - MathF.Max(one.top, two.top) > 1;

    /// <summary>The ring drawn alone into <paramref name="box"/>: where its ink went, and each run of words it drew.</summary>
    private static ((float Left, float Top, float Right, float Bottom) Ink, List<D2D_RECT_F> Words) Draw(CellIsland line, D2D_RECT_F box, TileStyle style)
    {
        var margin = 24f;
        var words = new List<D2D_RECT_F>();
        (float, float, float, float) ink = default;
        try
        {
            Bitmap(box.right + (2 * margin), box.bottom + (2 * margin), dc =>
            {
                PaintWatch.Watching = (at, what, color) =>
                {
                    if (what is Painted.Text)
                    {
                        words.Add(new D2D_RECT_F(at.left - margin, at.top - margin, at.right - margin, at.bottom - margin));
                    }
                    return color;
                };
                Shifted(dc, margin, margin, () => line.PaintOn(dc, box, style, WidgetSurface.List));
            }, bitmap => ink = InkOf(bitmap, (uint)MathF.Ceiling(box.right + (2 * margin)), (uint)MathF.Ceiling(box.bottom + (2 * margin))));
        }
        finally
        {
            PaintWatch.Watching = null;
        }
        var (left, top, right, bottom) = ink;
        return ((left - margin, top - margin, right - margin, bottom - margin), words);
    }

    // --- Pictures -----------------------------------------------------------------------------------

    /// <summary>
    /// The total over the ring at 100 and 150 %, dark and light, in five states side by side — today;
    /// a piece under the pointer; an app picked in the apps' line; a bar of the total under the pointer
    /// (its day in the ring); a day with nothing — and the ring alone at three to six rows:
    /// screentime-ring-&lt;dark|light&gt;-&lt;100|150&gt;.png, screentime-ring-heights-&lt;dark|light&gt;.png; and
    /// the ready-made folder at 100 % and 225 % text, screentime-ring-folder-&lt;dark|light&gt;.png.
    /// </summary>
    [UiFact]
    public void ThePictures()
    {
        Standing(gfx =>
        {
            var days = Fortnight();
            foreach (var dark in new[] { true, false })
            {
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    var look = LookFor(dark);
                    var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
                    var folders = new List<FolderIsland>();
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    try
                    {
                        foreach (var caption in new[] { "Today", "Pointer on a piece", "Mail picked", "Pointer on a bar: its day", "A day with nothing" })
                        {
                            var folder = TotalAndRing(gfx);
                            folders.Add(folder);
                            var (_, plan) = Plan(folder, look, scale);
                            var totalBox = Local(plan.Cards[0]);
                            var ringBox = Local(plan.Cards[1]);
                            var total = (ScreenTimeLine)folder.Tiles[0].Island;
                            var ring = (ScreenTimeDonut)folder.Tiles[1].Island;
                            switch (caption)
                            {
                                case "Pointer on a piece":
                                    ring.HoverOn(Middle(ring.Plan(ringBox, style).PieceBox(1)), ringBox, style, WidgetSurface.List);
                                    break;
                                case "Mail picked":
                                    ((IWidgetContainer)folder).Publish(Channels.App, new AppId("aumid:Example.Mail_8wekyb3d8bbwe!App"));
                                    break;
                                case "Pointer on a bar: its day":
                                    total.HoverOn(Middle(total.TotalPlan(totalBox, style).Bars[^4].Column), totalBox, style, WidgetSurface.List);
                                    break;
                                case "A day with nothing":
                                    ((IWidgetContainer)folder).Publish(Channels.Day, Today.AddDays(-11));
                                    break;
                            }
                            panels.Add((caption, look, folder, Plan(folder, look, scale).Plan));
                        }
                        Sheet(gfx, $"screentime-ring-{(dark ? "dark" : "light")}-{scale * 100:0}.png", scale, panels);
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
                }

                // The ring alone, three rows to six, at 150 %.
                var alone = new List<FolderIsland>();
                var heights = new List<(string, Look, FolderIsland, FolderPlan)>();
                try
                {
                    for (var rows = 3; rows <= 6; rows++)
                    {
                        var count = rows;
                        var folder = ListFormPictures.Folder(gfx, made => Feed(made.Tiles[0].Island, days), Line(ListKind.ScreenTimeChart, line => line.Height = LineHeight.Exactly(count * LineHeight.Row)));
                        alone.Add(folder);
                        heights.Add(($"{rows} rows: 150 %", LookFor(dark), folder, Plan(folder, LookFor(dark), 1.5f).Plan));
                    }
                    Clock.Now = () => Today.ToDateTime(new TimeOnly(10, 30));
                    Sheet(gfx, $"screentime-ring-heights-{(dark ? "dark" : "light")}.png", 1.5f, heights);
                }
                finally
                {
                    foreach (var folder in alone)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                    Clock.Now = Clock.Real;
                }

                // The ready-made folder, at the usual text and the largest.
                var made = Presets.Folder(Presets.Find("screenTimeGroup")!, new PresetMachine(false, []));
                var island = new FolderIsland($"{Owner}/{made.Id}", made, gfx, new Config());
                foreach (var tile in island.Tiles)
                {
                    Feed(tile.Island, days);
                }
                island.Opened(gfx);
                try
                {
                    Clock.Now = () => Today.ToDateTime(new TimeOnly(10, 30));
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    foreach (var textScale in TextScales)
                    {
                        var look = LookFor(dark, textScale);
                        panels.Add(($"Screen time: text {textScale * 100:0} %", look, island, Plan(island, look, 1f).Plan));
                    }
                    Sheet(gfx, $"screentime-ring-folder-{(dark ? "dark" : "light")}.png", 1f, panels);
                }
                finally
                {
                    island.Closed();
                    island.Dispose();
                    Clock.Now = Clock.Real;
                }
            }
        });
    }

    private static D2D_RECT_F Local(D2D_RECT_F card) => PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top);
}
