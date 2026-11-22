// Screen time's lines of a list folder (Karpo.Dock\ScreenTime\ScreenTimeLine.cs), on a made-up
// fortnight: today's and the history's each size, the apps' every height from one row to its most, at three
// scales and both text sizes, nothing outside its line and no two words on each other; Compact still saying the
// total and one row the top app; the folder's day followed and set — by a step, by the day's name, by a bar — and
// forgotten when the folder closes; the app rows and the steps lit under the pointer, and a row's
// click picking its app as the folder's, a piece of a bar too, the app pointed at lit in both lines,
// and nothing opening. Pictured to KARPO_PNG_DIR: each size of today and the history, every height of the
// apps, and the ready-made folder in both themes and at the largest text (§603: ScreenTimeSplitTests too).

using System.Numerics;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Folders.ListFormPictures;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

[Collection(ScreenTimeLinesTests.Names)]
public sealed unsafe class ScreenTimeLinesTests
{
    /// <summary>One at a time: the app names' stand-in is one static every line reads.</summary>
    public const string Names = "Screen time's app names";

    private static readonly float[] Scales = [1f, 1.5f, 2f];

    internal static readonly DateOnly Today = new(2026, 10, 9);

    internal const string Browser = @"C:\Program Files\Example\Browser\browser.exe";
    private const string Editor = @"C:\Users\Someone\AppData\Local\Programs\Editor\editor.exe";
    private const string Mail = "aumid:Example.Mail_8wekyb3d8bbwe!App";
    private const string Terminal = @"C:\Program Files\Example\Terminal\terminal.exe";
    private const string Music = "aumid:Example.Music_8wekyb3d8bbwe!App";
    private const string Photos = @"C:\Program Files\Example\Photos\photos.exe";
    private const string Chat = @"C:\Users\Someone\AppData\Local\Chat\chat.exe";
    private const string Files = @"C:\Windows\explorer.exe";

    private static readonly (string App, string Name, uint Colour)[] Apps =
    [
        (Browser, "Browser", 0xFF2D7DD2), (Editor, "Code Editor", 0xFF7A3EC8), (Mail, "Mail", 0xFF0F9D58), (Terminal, "Terminal", 0xFF333333),
        (Music, "Music", 0xFFE0245E), (Photos, "Photos", 0xFFF4B400), (Chat, "Chat with a rather long name", 0xFF00A3A3), (Files, "File Explorer", 0xFFE8A33D),
    ];

    /// <summary>
    /// Sixteen days ending today: a working fortnight with a quiet weekend, each day's apps in the
    /// same proportions, more of the browser on some days; today the morning so far.
    /// </summary>
    internal static ScreenTimeSnapshot Fortnight()
    {
        var log = new ScreenTimeLog();
        double[] hours = [6.5, 7.2, 5.1, 0.6, 0, 8.1, 7.7, 6.9, 7.4, 5.8, 1.2, 0.4, 7.9, 6.3, 6.8, 2.75];
        double[] shares = [0.34, 0.24, 0.12, 0.1, 0.08, 0.06, 0.04, 0.02];
        for (var index = 0; index < hours.Length; index++)
        {
            var date = Today.AddDays(index - (hours.Length - 1));
            if (hours[index] <= 0)
            {
                continue;
            }
            var day = log.Ensure(date);
            day.Total = TimeSpan.FromHours(hours[index]);
            for (var app = 0; app < Apps.Length; app++)
            {
                var share = shares[app] * (app == 0 ? 1 + (index % 3 * 0.2) : 1);
                day.Apps[Apps[app].App] = TimeSpan.FromHours(hours[index] * share);
            }
        }
        return new ScreenTimeSnapshot(Today, log);
    }

    /// <summary>The app names the shell would give, and a coloured tile for each logo; a stand-in pin that opens it.</summary>
    internal static void Standing(Action<Gfx> body)
    {
        var look = ScreenTimeNames.Look;
        ScreenTimeNames.Forget();
        ScreenTimeNames.Look = key =>
        {
            var (app, name, colour) = Apps.FirstOrDefault(entry => string.Equals(entry.App, key, StringComparison.OrdinalIgnoreCase));
            return app is null
                ? new ScreenTimeAppInfo(ScreenTimeNames.Fallback(key), null, null)
                : new ScreenTimeAppInfo(name, Tile(colour), new Item { Target = app, Name = name, Kind = ItemKind.Shell, Keys = [] });
        };
        try
        {
            OnStaThread(body);
        }
        finally
        {
            ScreenTimeNames.Look = look;
            ScreenTimeNames.Forget();
        }
    }

    /// <summary>A 48-px logo: a rounded square of <paramref name="colour"/> with a lighter middle.</summary>
    private static Pixels Tile(uint colour)
    {
        const int side = 48;
        var data = new byte[side * side * 4];
        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var corner = Math.Max(Math.Max(6 - x, x - (side - 7)), 0) + Math.Max(Math.Max(6 - y, y - (side - 7)), 0);
                if (corner > 6)
                {
                    continue;
                }
                var middle = x is > 14 and < 34 && y is > 14 and < 34;
                var at = ((y * side) + x) * 4;
                data[at] = (byte)(middle ? 255 : colour & 0xFF);
                data[at + 1] = (byte)(middle ? 255 : (colour >> 8) & 0xFF);
                data[at + 2] = (byte)(middle ? 255 : (colour >> 16) & 0xFF);
                data[at + 3] = 255;
            }
        }
        return new Pixels { Data = data, Width = side, Height = side };
    }

    /// <summary>A list folder of today's total over the apps over the history (§603), each reading <paramref name="days"/>, opened.</summary>
    private static FolderIsland Folder(Gfx gfx, ScreenTimeSnapshot days, params ListKind[] kinds) =>
        ListFormPictures.Folder(gfx, made =>
        {
            foreach (var tile in made.Tiles)
            {
                Feed(tile.Island, days);
            }
        }, [.. (kinds.Length == 0 ? [ListKind.ScreenTime, ListKind.ScreenTimeApps, ListKind.ScreenTimeHistory] : kinds).Select(kind => Line(kind))]);

    /// <summary>Screen time's lines and its ring read <paramref name="days"/>; anything else is left as it is.</summary>
    internal static void Feed(IIsland island, ScreenTimeSnapshot days)
    {
        if (island is ScreenTimeLine line)
        {
            line.Source = () => days;
        }
        else if (island is ScreenTimeDonut ring)
        {
            ring.Source = () => days;
        }
    }

    private static ScreenTimeLine LineOf(FolderIsland folder, int index) => (ScreenTimeLine)folder.Tiles[index].Island;

    private static TileStyle StyleAt(float scale, float textScale = 1f, bool dark = true)
    {
        var look = LookFor(dark, textScale);
        return FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
    }

    private static D2D_RECT_F BoxOf(int rows, float scale, float textScale = 1f) =>
        PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(scale), Rows(rows, scale, textScale));

    /// <summary>
    /// §603: the box <paramref name="line"/>'s form takes at <paramref name="scale"/>, its size's height: a sized line
    /// given a taller box draws and is hit in its form alone, at the box's top (CellIsland.Scrolled).
    /// </summary>
    internal static D2D_RECT_F FormBox(ScreenTimeLine line, float scale, float textScale = 1f)
    {
        var width = FolderFlyout.ListWidth(scale);
        return PopupPaint.Rect(0, 0, width, line.ListHeight(width, StyleAt(scale, textScale)));
    }

    private static Vector2 Middle(D2D_RECT_F rect) => new((rect.left + rect.right) / 2, (rect.top + rect.bottom) / 2);

    // --- Every height -------------------------------------------------------------------------------

    /// <summary>What one line drew: where its ink went, and its words and glyphs.</summary>
    internal sealed record Drawn(float Width, float Height, (float Left, float Top, float Right, float Bottom) Ink, List<(string Text, D2D_RECT_F Box)> Said, List<D2D_RECT_F> Pieces);

    private static Drawn Draw(CellIsland line, int rows, float scale, float textScale) => Draw(line, BoxOf(rows, scale, textScale), scale, textScale);

    /// <summary>§603: <paramref name="line"/> at <paramref name="size"/>, as tall as that size's form.</summary>
    internal static Drawn Sized(ScreenTimeLine line, LineSize size, float scale, float textScale)
    {
        line.Settings.Size = size;
        var look = LookFor(true, textScale);
        var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
        var width = FolderFlyout.ListWidth(scale);
        return Draw(line, PopupPaint.Rect(0, 0, width, MathF.Ceiling(line.ListHeight(width, style))), scale, textScale);
    }

    private static Drawn Draw(CellIsland line, D2D_RECT_F box, float scale, float textScale)
    {
        var look = LookFor(true, textScale);
        var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
        var margin = PopupPaint.Round(24 * scale);
        var said = new List<(string, D2D_RECT_F)>();
        var pieces = new List<D2D_RECT_F>();
        (float, float, float, float) ink = default;
        try
        {
            Bitmap(box.right + (2 * margin), box.bottom + (2 * margin), dc =>
            {
                PaintWatch.Said = (text, at) => said.Add((text, Moved(at, margin)));
                PaintWatch.Watching = (at, what, color) =>
                {
                    if (what is Painted.Glyph or Painted.Text)
                    {
                        pieces.Add(Moved(at, margin));
                    }
                    return color;
                };
                Shifted(dc, margin, margin, () => line.PaintOn(dc, box, style, WidgetSurface.List));
            }, bitmap => ink = InkOf(bitmap, (uint)MathF.Ceiling(box.right + (2 * margin)), (uint)MathF.Ceiling(box.bottom + (2 * margin))));
        }
        finally
        {
            PaintWatch.Watching = null;
            PaintWatch.Said = null;
        }
        var (left, top, right, bottom) = ink;
        return new Drawn(box.right, box.bottom, (left - margin, top - margin, right - margin, bottom - margin), said, pieces);
    }

    private static D2D_RECT_F Moved(D2D_RECT_F box, float margin) => new(box.left - margin, box.top - margin, box.right - margin, box.bottom - margin);

    private static bool Over(D2D_RECT_F one, D2D_RECT_F two) =>
        MathF.Min(one.right, two.right) - MathF.Max(one.left, two.left) > 1
        && MathF.Min(one.bottom, two.bottom) - MathF.Max(one.top, two.top) > 1;

    /// <summary>
    /// Today and the history at each of their sizes (§603), the apps at every height from one row to eight, at 100,
    /// 150 and 200 % and at the largest text: each line draws, all of it inside its line, and none of its words or
    /// glyphs on another. Today, and a day of the fortnight that is not today, as a folder's day.
    /// </summary>
    [UiFact]
    public void EveryHeightStaysInItsLineAndNothingOverlaps()
    {
        var wrong = new List<string>();
        Standing(gfx =>
        {
            var folder = Folder(gfx, Fortnight());
            try
            {
                foreach (var day in new DateOnly?[] { null, Today.AddDays(-3) })
                {
                    ((IWidgetContainer)folder).Publish(Channels.Day, day ?? Today);
                    foreach (var index in new[] { 0, 1, 2 })
                    {
                        var line = LineOf(folder, index);
                        var apps = line.Which == ScreenTimePart.Apps;
                        foreach (var scale in Scales)
                        {
                            foreach (var textScale in TextScales)
                            {
                                for (var rows = 1; rows <= (apps ? 8 : 2); rows++)
                                {
                                    var size = rows == 1 ? LineSize.Compact : LineSize.Full;
                                    var drawn = apps ? Draw(line, rows, scale, textScale) : Sized(line, size, scale, textScale);
                                    var at = $"{line.Which} on {day?.ToString() ?? "today"} at {(apps ? $"{rows} rows" : size.ToString())}, {scale * 100:0} %, text {textScale * 100:0} %";
                                    if (drawn.Ink.Right <= drawn.Ink.Left)
                                    {
                                        wrong.Add($"{at}: drew nothing");
                                        continue;
                                    }
                                    if (drawn.Ink.Left < -0.5f || drawn.Ink.Top < -0.5f || drawn.Ink.Right > drawn.Width + 0.5f || drawn.Ink.Bottom > drawn.Height + 0.5f)
                                    {
                                        wrong.Add($"{at}: ink at {drawn.Ink.Left:0},{drawn.Ink.Top:0}..{drawn.Ink.Right:0},{drawn.Ink.Bottom:0} outside {drawn.Width:0}x{drawn.Height:0}");
                                    }
                                    for (var one = 0; one < drawn.Pieces.Count; one++)
                                    {
                                        for (var two = one + 1; two < drawn.Pieces.Count; two++)
                                        {
                                            if (Over(drawn.Pieces[one], drawn.Pieces[two]))
                                            {
                                                wrong.Add($"{at}: pieces at {drawn.Pieces[one].left:0},{drawn.Pieces[one].top:0} and {drawn.Pieces[two].left:0},{drawn.Pieces[two].top:0} overlap");
                                            }
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
            }
        });
        Assert.True(wrong.Count == 0, $"{wrong.Count} lines are wrong:\n{string.Join("\n", wrong.Take(60))}");
    }

    /// <summary>Compact, today still says the day's total; one row tall, the apps the most used app and its time.</summary>
    [UiFact]
    public void OneRowSaysTheTotalAndTheTopApp()
    {
        Standing(gfx =>
        {
            var days = Fortnight();
            var folder = Folder(gfx, days);
            try
            {
                var top = days.Log.Day(Today)!.Ranked()[0];
                foreach (var scale in Scales)
                {
                    var total = Sized(LineOf(folder, 0), LineSize.Compact, scale, 1f);
                    Assert.Contains(total.Said, said => said.Text == ScreenTimeText.Duration(days.Log.TotalOn(Today)));
                    var apps = Draw(LineOf(folder, 1), 1, scale, 1f);
                    Assert.Contains(apps.Said, said => said.Text == "Browser");
                    Assert.Contains(apps.Said, said => said.Text == ScreenTimeText.Duration(top.Time));
                }
            }
            finally
            {
                folder.Closed();
                folder.Dispose();
            }
        });
    }

    // --- The folder's day ---------------------------------------------------------------------------

    /// <summary>
    /// The lines show the folder's day: set from outside (a month calendar), stepped back and on by
    /// the total, back to today by the day's name, chosen on the bars — and today again once the
    /// folder closes.
    /// </summary>
    [UiFact]
    public void TheLinesFollowAndSetTheFoldersDay()
    {
        Standing(gfx =>
        {
            var days = Fortnight();
            var folder = Folder(gfx, days);
            try
            {
                var total = LineOf(folder, 0);
                var apps = LineOf(folder, 1);
                var container = (IWidgetContainer)folder;
                Assert.Equal(Today, total.Shown);
                Assert.Equal(Today, apps.Shown);

                // A calendar in the folder chose the 4th.
                container.Publish(Channels.Day, Today.AddDays(-5));
                Assert.Equal(Today.AddDays(-5), total.Shown);
                Assert.Equal(Today.AddDays(-5), apps.Shown);
                var said = Draw(total, 4, 1f, 1f).Said;
                Assert.Contains(said, words => words.Text == ScreenTimeText.Duration(days.Log.TotalOn(Today.AddDays(-5))));

                // A step back and on: the apps follow the total.
                total.Click(ScreenTimeLine.PreviousTarget);
                Assert.Equal(Today.AddDays(-6), apps.Shown);
                total.Click(ScreenTimeLine.NextTarget);
                total.Click(ScreenTimeLine.NextTarget);
                Assert.Equal(Today.AddDays(-4), apps.Shown);

                // The day's name goes back to today, published as the folder's day.
                total.Click(ScreenTimeLine.TodayTarget);
                Assert.Equal(Today, container.Read(Channels.Day));
                Assert.Equal(Today, apps.Shown);

                // A day of the bars: the fortnight ends today, its first day ten days back is the third.
                var graph = ScreenTimeLine.GraphOf(days.Log, Today, Today);
                Assert.Equal(ScreenTimeLine.GraphDays, graph.Count);
                Assert.Equal(Today, graph[^1].Day);
                LineOf(folder, 2).Click(ScreenTimeLine.DayTarget + 2);
                Assert.Equal(graph[2].Day, apps.Shown);

                // A day nothing was counted for is shown as such, not stepped past.
                container.Publish(Channels.Day, Today.AddDays(-11));
                Assert.Equal(TimeSpan.Zero, days.Log.TotalOn(apps.Shown));
                Assert.Contains(Draw(apps, 2, 1f, 1f).Said, words => words.Text == S.WidgetScreenTimeNoApps);

                folder.Closed();
                Assert.Equal(Today, total.Shown);
            }
            finally
            {
                folder.Dispose();
            }
        });
    }

    /// <summary>A list folder of <paramref name="lines"/>, its screen time reading <paramref name="days"/>, opened, with the clock at the fortnight's today.</summary>
    private static FolderIsland FolderOf(Gfx gfx, ScreenTimeSnapshot days, params CellItem[] lines)
    {
        Clock.Now = () => Today.ToDateTime(new TimeOnly(10, 30));
        return ListFormPictures.Folder(gfx, made =>
        {
            foreach (var tile in made.Tiles)
            {
                Feed(tile.Island, days);
            }
        }, lines);
    }

    /// <summary>A click on a day of a month calendar in the folder: the total and the apps show that day.</summary>
    [UiFact]
    public void TheLinesFollowTheDayAMonthPicks()
    {
        Standing(gfx =>
        {
            var days = Fortnight();
            var folder = FolderOf(gfx, days, Line(ListKind.Month), Line(ListKind.ScreenTime), Line(ListKind.ScreenTimeApps));
            try
            {
                var month = (MonthTile)folder.Tiles[0].Island;
                var total = LineOf(folder, 1);
                var apps = LineOf(folder, 2);
                var day = Today.AddDays(-3);
                month.Click(MonthTile.Target(day));

                Assert.Equal(day, folder.ChannelValue(Channels.Day));
                Assert.Equal(day, total.Shown);
                Assert.Equal(day, apps.Shown);
                Assert.Contains(Draw(total, 4, 1f, 1f).Said, words => words.Text == ScreenTimeText.Duration(days.Log.TotalOn(day)));
                Assert.Contains(Draw(apps, 3, 1f, 1f).Said, words => words.Text == ScreenTimeText.Duration(days.Log.Day(day)!.Ranked()[0].Time));
            }
            finally
            {
                folder.Closed();
                folder.Dispose();
                Clock.Now = Clock.Real;
            }
        });
    }

    /// <summary>The total's step publishes the folder's day: a month and an agenda beside it follow.</summary>
    [UiFact]
    public void AStepPublishesTheDayAnAgendaFollows()
    {
        var store = CalendarIsland.Made;
        Standing(gfx =>
        {
            CalendarIsland.Made = ListFormCases.Store(ListFormCases.Week);
            var folder = FolderOf(gfx, Fortnight(), Line(ListKind.ScreenTime), Line(ListKind.Month), Line(ListKind.Calendar), Line(ListKind.ScreenTimeHistory));
            try
            {
                var total = LineOf(folder, 0);
                var month = (MonthTile)folder.Tiles[1].Island;
                var agenda = (CalendarIsland)folder.Tiles[2].Island;
                var now = Clock.Now();
                Assert.Null(agenda.Picked(now));

                total.Click(ScreenTimeLine.PreviousTarget);
                total.Click(ScreenTimeLine.PreviousTarget);

                Assert.Equal(Today.AddDays(-2), folder.ChannelValue(Channels.Day));
                Assert.Equal(Today.AddDays(-2), month.Picked);
                Assert.Equal(Today.AddDays(-2), agenda.Picked(now));

                // A day of the history's bars too.
                var graph = ScreenTimeLine.GraphOf(Fortnight().Log, total.Shown, Today);
                var history = (ScreenTimeLine)folder.Tiles[3].Island;
                history.Click(ScreenTimeLine.DayTarget + 4);
                Assert.Equal(graph[4].Day, agenda.Picked(now));
            }
            finally
            {
                folder.Closed();
                folder.Dispose();
                Clock.Now = Clock.Real;
                CalendarIsland.Made = store;
            }
        });
    }

    [Fact]
    public void AStepStaysBetweenTheFirstDayCountedAndToday()
    {
        var oldest = Today.AddDays(-15);
        Assert.Equal(Today.AddDays(-1), ScreenTimeLine.Stepped(ScreenTimeLine.PreviousTarget, Today, Today, oldest));
        Assert.Null(ScreenTimeLine.Stepped(ScreenTimeLine.NextTarget, Today, Today, oldest));
        Assert.Null(ScreenTimeLine.Stepped(ScreenTimeLine.PreviousTarget, oldest, Today, oldest));
        Assert.Equal(oldest.AddDays(1), ScreenTimeLine.Stepped(ScreenTimeLine.NextTarget, oldest, Today, oldest));
        Assert.Null(ScreenTimeLine.Stepped(ScreenTimeLine.TodayTarget, Today, Today, oldest));
        // A calendar's day past either end: a step comes back inside.
        Assert.Equal(Today, ScreenTimeLine.Stepped(ScreenTimeLine.PreviousTarget, Today.AddDays(3), Today, oldest));
        Assert.Equal(oldest, ScreenTimeLine.Stepped(ScreenTimeLine.NextTarget, oldest.AddDays(-20), Today, oldest));
    }

    [Fact]
    public void TheGraphIsTwoWeeksEndingTodayOrAtAnOlderDay()
    {
        var log = Fortnight().Log;
        var recent = ScreenTimeLine.GraphOf(log, Today.AddDays(-3), Today);
        Assert.Equal(Today, recent[^1].Day);
        var older = ScreenTimeLine.GraphOf(log, Today.AddDays(-14), Today);
        Assert.Equal(Today.AddDays(-14), older[^1].Day);
        // None before the first day counted.
        Assert.Equal(log.Oldest, older[0].Day);
        Assert.Empty(ScreenTimeLine.GraphOf(new ScreenTimeLog(), Today, Today));
        // The average is of the days used.
        var week = Enumerable.Range(0, 7).Select(back => log.TotalOn(Today.AddDays(-back))).Where(time => time > TimeSpan.Zero).ToList();
        Assert.Equal(TimeSpan.FromTicks(week.Sum(time => time.Ticks) / week.Count), ScreenTimeLine.AverageOf(log, Today));
        Assert.Null(ScreenTimeLine.AverageOf(new ScreenTimeLog(), Today));
    }

    /// <summary>
    /// The ready-made folder: today Full over the history Full (§603), the ring Full (2026-10-12), the apps a Fixed six
    /// (eight from 2026-10-10 until the ring came between).
    /// </summary>
    [Fact]
    public void TheReadyMadeFolderFixesItsLines()
    {
        var made = Presets.Folder(Presets.Find("screenTimeGroup")!, new PresetMachine(false, []));
        var cells = made.Folder!.Cells;
        Assert.Equal(["screenTime", "screenTimeHistory", "screenTimeChart", "screenTimeApps"], cells.Select(cell => cell.Island.Kind));
        // §432, §603: the sized lines their Full form, and no height.
        Assert.All(cells.Take(3), cell => Assert.Equal(((LineHeight?)null, (LineSize?)LineSize.Full), (cell.Island.Height, cell.Island.Size)));
        Assert.Equal(LineHeight.Exactly(12), cells[3].Island.Height);
    }

    /// <summary>
    /// Regression (2026-10-10): the apps' rows shared a taller line's height between them, up to a
    /// quarter more than a row each. Every row is one list row at 4 to 12 rows, at every scale and
    /// text size; a taller line shows more apps, and what is left under the last stays empty.
    /// </summary>
    [UiFact]
    public void TheAppsRowsNeverStretchIntoSpareRoom()
    {
        Standing(gfx =>
        {
            var log = new ScreenTimeLog();
            var day = log.Ensure(Today);
            for (var app = 0; app < 16; app++)
            {
                day.Apps[$@"C:\Apps\app{app:00}.exe"] = TimeSpan.FromMinutes(200 - (app * 10));
            }
            day.Total = TimeSpan.FromTicks(day.Apps.Values.Sum(time => time.Ticks));
            var folder = Folder(gfx, new ScreenTimeSnapshot(Today, log));
            try
            {
                var apps = LineOf(folder, 1);
                foreach (var scale in Scales)
                {
                    foreach (var textScale in new[] { 1f, 2.25f })
                    {
                        var style = StyleAt(scale, textScale);
                        var row = PopupPaint.Round(40 * scale * textScale);
                        var before = 0;
                        for (var rows = 4; rows <= 12; rows++)
                        {
                            var box = BoxOf(rows, scale, textScale);
                            var plan = apps.AppsPlan(box, style);
                            Assert.All(plan.Rows, drawn => Assert.Equal(row, drawn.Box.bottom - drawn.Box.top, 0.01f));
                            // As many as fit at a row each, whole; the rest scroll (ListScroll.cs).
                            Assert.Equal(Math.Clamp((int)MathF.Floor((box.bottom - box.top + 0.5f) / row), 1, 16), plan.Rows.Count);
                            Assert.True(plan.Rows[^1].Box.bottom <= box.bottom + 0.5f);
                            Assert.Null(plan.Said);
                            Assert.True(apps.Scrolled(box, style, WidgetSurface.List).Scrolls);
                            Assert.True(plan.Rows.Count >= before, $"{rows} rows at {scale} / {textScale}: fewer apps than a shorter line");
                            before = plan.Rows.Count;
                        }
                        Assert.True(apps.AppsPlan(BoxOf(12, scale, textScale), style).Rows.Count > apps.AppsPlan(BoxOf(4, scale, textScale), style).Rows.Count);
                    }
                }
            }
            finally
            {
                folder.Closed();
                folder.Dispose();
            }
        });
    }

    // --- The pointer --------------------------------------------------------------------------------

    /// <summary>
    /// The row under the pointer lights (and the line as a whole does not), and a click on it opens
    /// that app; the steps and a day of the graph light as well, a step that goes nowhere does not.
    /// </summary>
    [UiFact]
    public void RowsAndStepsLightUnderThePointerAndARowOpensItsApp()
    {
        Standing(gfx =>
        {
            var folder = Folder(gfx, Fortnight());
            try
            {
                var style = StyleAt(1.5f);
                var box = BoxOf(6, 1.5f);
                var apps = LineOf(folder, 1);
                Assert.True(apps.LightsItsRows);
                var plan = apps.AppsPlan(box, style);
                Assert.InRange(plan.Rows.Count, 3, 7);
                // The rest scroll (ListScroll.cs): nothing says how many more.
                Assert.Null(plan.Said);
                Assert.True(apps.Scrolled(box, style, WidgetSurface.List).Scrolls);
                apps.HoverOn(Middle(plan.Rows[2].Box), box, style, WidgetSurface.List);
                Assert.Equal(ScreenTimeLine.AppTarget + 2, apps.Hot);
                // A click picks the app, and asks the dock for nothing (2026-10-10: no app opens).
                var action = apps.Click(apps.TargetOn(Middle(plan.Rows[2].Box), box, style, WidgetSurface.List));
                Assert.Equal(default, action);
                Assert.False(FolderFlyout.Forwards(action, FolderFlyout.ActOf(folder.Tiles[1].Cell)));
                Assert.Equal(new AppId(Mail), apps.Selected);
                Assert.Equal(new AppId(Mail), apps.MenuApp);
                apps.HoverOn(null, box, style, WidgetSurface.List);
                Assert.Equal(ScreenTimeLine.NoTarget, apps.Hot);

                var total = LineOf(folder, 0);
                var whole = total.TotalPlan(BoxOf(4, 1.5f), style);
                Assert.True(whole.Steps);
                Assert.False(whole.CanGoOn);
                Assert.True(whole.CanGoBack);
                total.HoverOn(Middle(whole.Previous), BoxOf(4, 1.5f), style, WidgetSurface.List);
                Assert.Equal(ScreenTimeLine.PreviousTarget, total.Hot);
                var history = LineOf(folder, 2);
                var bars = history.TotalPlan(FormBox(history, 1.5f), style);
                history.HoverOn(Middle(bars.Bars[^1].Column), FormBox(history, 1.5f), style, WidgetSurface.List);
                Assert.Equal(ScreenTimeLine.DayTarget + bars.Bars.Count - 1, history.Hot);
                Assert.Equal(default, total.Click(ScreenTimeLine.NextTarget));
                Assert.Equal(Today, total.Shown);
            }
            finally
            {
                folder.Closed();
                folder.Dispose();
            }
        });
    }

    // --- The folder's app --------------------------------------------------------------------------

    /// <summary>
    /// A click on an app's row picks it as the folder's app, a second click lets it go, another row
    /// picks that one instead; nothing is ever asked of the dock, and the folder forgets the pick when
    /// it closes. Escape takes the pick back first, and only then is there nothing for it to take.
    /// </summary>
    [UiFact]
    public void ARowsClickPicksItsAppAndASecondLetsItGo()
    {
        Standing(gfx =>
        {
            var folder = Folder(gfx, Fortnight());
            try
            {
                var style = StyleAt(1f);
                var box = BoxOf(6, 1f);
                var total = LineOf(folder, 0);
                var apps = LineOf(folder, 1);
                var rows = apps.AppsPlan(box, style).Rows;
                int TargetOf(int row) => apps.TargetOn(Middle(rows[row].Box), box, style, WidgetSurface.List);

                Assert.Null(apps.Selected);
                Assert.Equal(default, apps.Click(TargetOf(1)));
                Assert.Equal(new AppId(Editor), folder.ChannelValue(Channels.App));
                Assert.Equal(new AppId(Editor), apps.Selected);
                Assert.Equal(new AppId(Editor), total.Selected);

                Assert.Equal(default, apps.Click(TargetOf(2)));
                Assert.Equal(new AppId(Mail), total.Selected);

                apps.Click(TargetOf(2));
                Assert.Null(folder.ChannelValue(Channels.App));
                Assert.Null(total.Selected);

                // Escape: the pick first, then nothing (the folder closes).
                apps.Click(TargetOf(0));
                Assert.True(folder.Escape());
                Assert.Null(apps.Selected);
                Assert.False(folder.Escape());

                // The folder closing forgets it.
                apps.Click(TargetOf(0));
                folder.Closed();
                Assert.Null(folder.ChannelValue(Channels.App));
                Assert.Null(apps.Selected);
                Assert.Null(total.Selected);
            }
            finally
            {
                folder.Dispose();
            }
        });
    }

    /// <summary>
    /// The pointer on an app — its row, or its piece of a bar — is the folder's app pointed at while
    /// it is there, and the line it leaves takes it back; the other line lights that app: the history's bars
    /// the app's pieces, the apps its row. Leaving for the "Other" piece, or the bar's empty part,
    /// is no app.
    /// </summary>
    [UiFact]
    public void ThePointerOnAnAppLightsItInBothLines()
    {
        Standing(gfx =>
        {
            var folder = Folder(gfx, Fortnight());
            try
            {
                var style = StyleAt(1.5f);
                var appsBox = BoxOf(8, 1.5f);
                var total = LineOf(folder, 2);
                var totalBox = FormBox(total, 1.5f);
                var apps = LineOf(folder, 1);
                var rows = apps.AppsPlan(appsBox, style).Rows;

                // A row under the pointer: the folder's, and the bars light that app.
                apps.HoverOn(Middle(rows[1].Box), appsBox, style, WidgetSurface.List);
                Assert.Equal(new AppId(Editor), folder.ChannelValue(Channels.AppHover));
                var chart = total.TotalPlan(totalBox, style).Chart!;
                var lit = total.EmphasisOf(chart);
                Assert.True(lit.Any);
                Assert.Equal(1, lit.Pointed);
                Assert.Equal(ScreenTimeLine.NoPart, lit.Picked);
                apps.HoverOn(null, appsBox, style, WidgetSurface.List);
                Assert.Null(folder.ChannelValue(Channels.AppHover));
                Assert.False(total.EmphasisOf(chart).Any);

                // A piece of a bar under the pointer: the folder's, and its row lights in the apps.
                var plan = total.TotalPlan(totalBox, style);
                var today = plan.Bars[^1];
                var piece = today.Segments.First(segment => segment.Part == 2);
                total.HoverOn(Middle(piece.Box), totalBox, style, WidgetSurface.List);
                Assert.Equal(ScreenTimeLine.SegmentTarget + (today.Index * ScreenTimeLine.SegmentStride) + 2, total.Hot);
                Assert.Equal(new AppId(chart.Top[2]), folder.ChannelValue(Channels.AppHover));
                var row = rows.First(each => string.Equals(each.App, chart.Top[2], StringComparison.OrdinalIgnoreCase));
                Assert.Equal(ScreenTimeLine.RowState.Hover, apps.StateOf(row));
                Assert.All(rows.Where(each => each.Index != row.Index), each => Assert.Equal(ScreenTimeLine.RowState.Rest, apps.StateOf(each)));

                // "Other" is no app; above the bar, the day.
                total.HoverOn(Middle(today.Segments[^1].Box), totalBox, style, WidgetSurface.List);
                Assert.Equal(ScreenTimeLine.DayTarget + today.Index, total.Hot);
                Assert.Null(folder.ChannelValue(Channels.AppHover));
                total.HoverOn(new Vector2(Middle(today.Column).X, today.Column.top + 1), totalBox, style, WidgetSurface.List);
                Assert.Null(folder.ChannelValue(Channels.AppHover));

                // The folder closing forgets it too.
                total.HoverOn(Middle(piece.Box), totalBox, style, WidgetSurface.List);
                folder.Closed();
                Assert.Null(folder.ChannelValue(Channels.AppHover));
                Assert.Equal(ScreenTimeLine.NoTarget, total.Hot);
            }
            finally
            {
                folder.Dispose();
            }
        });
    }

    /// <summary>
    /// A piece of a bar clicked picks its app and shows its day; on the day shown a second click
    /// lets it go. Today's total is then the app's time that day under its name, of the day's total;
    /// the apps' row of it is marked picked, under the pointer too where it is pointed at.
    /// </summary>
    [UiFact]
    public void APickedAppIsTheTotalsAndItsRowIsMarked()
    {
        Standing(gfx =>
        {
            var days = Fortnight();
            var folder = Folder(gfx, days);
            try
            {
                var style = StyleAt(1.5f);
                var today = LineOf(folder, 0);
                var total = LineOf(folder, 2);
                var totalBox = FormBox(total, 1.5f);
                var apps = LineOf(folder, 1);
                var plan = total.TotalPlan(totalBox, style);
                var older = plan.Bars[^3];
                var piece = older.Segments.First(segment => segment.Part == 0);
                var target = total.TargetOn(Middle(piece.Box), totalBox, style, WidgetSurface.List);

                total.Click(target);
                Assert.Equal(older.Day, folder.ChannelValue(Channels.Day));
                Assert.Equal(new AppId(Browser), folder.ChannelValue(Channels.App));

                // The total: the browser's time that day, its name, of the day's total.
                var said = Sized(today, LineSize.Full, 1.5f, 1f).Said.Select(words => words.Text).ToList();
                Assert.Contains(ScreenTimeText.Duration(ScreenTimeLine.TimeOf(days.Log, older.Day, new AppId(Browser))), said);
                Assert.Contains("Browser", said);
                Assert.Contains(S.WidgetScreenTimeOfTotal(total: ScreenTimeText.Duration(days.Log.TotalOn(older.Day))), said);
                Assert.DoesNotContain(S.WidgetScreenTimeAverage, said);
                // Compact: the app's time, its name and the day's total it is part of.
                var one = Sized(today, LineSize.Compact, 1.5f, 1f).Said.Select(words => words.Text).ToList();
                Assert.Contains(S.WidgetScreenTimeAppOfTotal(name: "Browser", total: ScreenTimeText.Duration(days.Log.TotalOn(older.Day))), one);
                Assert.Contains(ScreenTimeText.Duration(ScreenTimeLine.TimeOf(days.Log, older.Day, new AppId(Browser))), one);

                // The bars: the browser whole on every day, the rest dimmed, more than for the pointer.
                var chart = total.TotalPlan(totalBox, style).Chart!;
                var emphasis = total.EmphasisOf(chart);
                Assert.Equal(0, emphasis.Picked);
                Assert.Equal(1f, emphasis.Alpha(0, false));
                Assert.True(emphasis.Alpha(1, true) < 0.5f);
                var pointed = new ScreenTimeLine.Emphasis(ScreenTimeLine.NoPart, 0, true);
                Assert.True(pointed.Alpha(1, true) > emphasis.Alpha(1, true));
                var slots = total.TotalPlan(totalBox, style).Slots;
                Assert.Equal(ScreenTimeLine.PartInk(slots, 0, false, style), ScreenTimeLine.SegmentInk(slots, 0, 0, false, emphasis, style));
                Assert.True(ScreenTimeLine.SegmentInk(slots, 1, 1, true, emphasis, style).a < ScreenTimeLine.PartInk(slots, 1, true, style).a);
                // An older day's unsplit bar is no app's: dimmed whatever is picked.
                Assert.False(emphasis.On(ScreenTimeLine.NoPart));
                // In high contrast: the app the highlight, the rest grey.
                var contrast = FolderFlyout.StyleFor(1.5f, Contrast(), FlyoutTheme.For(Contrast()));
                Assert.Equal(PopupPaint.Ink(contrast.Theme.Accent), ScreenTimeLine.SegmentInk([], 0, 0, false, emphasis, contrast));
                Assert.Equal(contrast.Theme.TextDisabled, ScreenTimeLine.SegmentInk([], 1, 1, true, emphasis, contrast));

                // The apps: the browser's row picked; pointed at as well, both.
                var appsBox = BoxOf(8, 1.5f);
                var rows = apps.AppsPlan(appsBox, style).Rows;
                var browser = rows.First(row => row.App == Browser);
                Assert.Equal(ScreenTimeLine.RowState.Selected, apps.StateOf(browser));
                apps.HoverOn(Middle(browser.Box), appsBox, style, WidgetSurface.List);
                Assert.Equal(ScreenTimeLine.RowState.Selected | ScreenTimeLine.RowState.Hover, apps.StateOf(browser));
                apps.HoverOn(null, appsBox, style, WidgetSurface.List);

                // On the day shown, the piece again lets it go.
                total.Click(total.TargetOn(Middle(total.TotalPlan(totalBox, style).Bars[older.Index].Segments.First(segment => segment.Part == 0).Box), totalBox, style, WidgetSurface.List));
                Assert.Null(folder.ChannelValue(Channels.App));
                Assert.Equal(older.Day, folder.ChannelValue(Channels.Day));
                Assert.Contains(ScreenTimeText.Duration(days.Log.TotalOn(older.Day)), Sized(today, LineSize.Full, 1.5f, 1f).Said.Select(words => words.Text));
            }
            finally
            {
                folder.Closed();
                folder.Dispose();
            }
        });
    }

    /// <summary>
    /// Where nobody set a height, the apps' line is a row for each of the day's apps; today and the history their
    /// size's form (§603): today's Compact the kit's row with its caption, each Full taller than its Compact.
    /// </summary>
    [UiFact]
    public void ALinesOwnHeightIsItsAppsOrItsSize()
    {
        Standing(gfx =>
        {
            var folder = Folder(gfx, Fortnight());
            try
            {
                var style = StyleAt(1f);
                var width = FolderFlyout.ListWidth(1f);
                Assert.Equal(8 * PopupPaint.Round(40 * style.Scale * style.TextScale), LineOf(folder, 1).ListHeight(width, style));
                foreach (var index in new[] { 0, 2 })
                {
                    var line = LineOf(folder, index);
                    line.Settings.Size = LineSize.Compact;
                    var compact = line.ListHeight(width, style);
                    line.Settings.Size = LineSize.Full;
                    Assert.True(line.ListHeight(width, style) > compact, $"{line.Which}");
                }
                LineOf(folder, 0).Settings.Size = LineSize.Compact;
                Assert.Equal(LineRow.Height(style, caption: true), LineOf(folder, 0).ListHeight(width, style));
            }
            finally
            {
                folder.Closed();
                folder.Dispose();
            }
        });
    }

    // --- Bars by app --------------------------------------------------------------------------------

    /// <summary>The fortnight with its first five days as an older Karpo kept them: totals, no apps.</summary>
    internal static ScreenTimeSnapshot WithOldDays()
    {
        var days = Fortnight();
        foreach (var day in days.Log.Days.Take(5))
        {
            day.Apps.Clear();
        }
        return days;
    }

    private static TimeSpan Hours(double hours) => TimeSpan.FromHours(hours);

    /// <summary>
    /// The chart's apps are the period's most used, as many as asked, equal times by key and never
    /// one under a minute; each day split by them, and the rest — smaller apps and time for no app —
    /// as other; a day with no apps counted is one unsplit bar, a day nothing was counted for none.
    /// </summary>
    [Fact]
    public void TheChartIsTheTopAppsAndTheRestAsOther()
    {
        var log = new ScreenTimeLog();
        var one = log.Ensure(Today.AddDays(-2));
        one.Total = Hours(4);
        (one.Apps["a"], one.Apps["b"], one.Apps["c"], one.Apps["tiny"]) = (Hours(2), Hours(1), Hours(0.5), TimeSpan.FromSeconds(20));
        var two = log.Ensure(Today.AddDays(-1));
        two.Total = Hours(4);
        (two.Apps["A"], two.Apps["d"]) = (Hours(1), Hours(3));
        log.Ensure(Today).Total = Hours(5);
        DateOnly[] days = [Today.AddDays(-3), Today.AddDays(-2), Today.AddDays(-1), Today];

        var chart = ScreenTimeChart.Of(log, days, most: 2);

        // a and A are one app (keys ignore case), 3 h as d is: a first by its key.
        Assert.Equal(["a", "d"], chart.Top);
        Assert.Equal(4, chart.Bars.Count);
        Assert.Equal((TimeSpan.Zero, false), (chart.Bars[0].Total, chart.Bars[0].Split));
        Assert.Equal([Hours(2), TimeSpan.Zero], chart.Bars[1].Parts);
        Assert.Equal(Hours(2), chart.Bars[1].Other);
        Assert.True(chart.Bars[1].Split);
        Assert.Equal([Hours(1), Hours(3)], chart.Bars[2].Parts);
        Assert.Equal(TimeSpan.Zero, chart.Bars[2].Other);
        // Today, counted without apps: one bar, all of it other.
        Assert.Equal((Hours(5), Hours(5), false), (chart.Bars[3].Total, chart.Bars[3].Other, chart.Bars[3].Split));
        Assert.Equal(5, chart.FullHours);

        // More room than apps: every app of a minute or more, the tiny one never.
        Assert.Equal(["a", "d", "b", "c"], ScreenTimeChart.Of(log, days, most: 10).Top);
        // A total less than its apps (only a hand-edited file has one) is their sum, no negative rest.
        two.Total = Hours(1);
        Assert.Equal((Hours(4), TimeSpan.Zero), (ScreenTimeChart.Of(log, days).Bars[2].Total, ScreenTimeChart.Of(log, days).Bars[2].Other));
        Assert.Empty(ScreenTimeChart.Of(new ScreenTimeLog(), days).Top);
    }

    /// <summary>
    /// Days an older Karpo wrote — the live file's earlier totals, a history's day without apps —
    /// still load, and are bars of their own, unsplit; the days with apps beside them are split.
    /// </summary>
    [Fact]
    public void OldDaysWithoutAppsAreOneUnsplitBar()
    {
        var live = new ScreenTimeFile
        {
            Day = ScreenTimeFiles.Text(Today), Seconds = 3600, Idle = 5,
            Earlier = new() { [ScreenTimeFiles.Text(Today.AddDays(-3))] = 7200 },
        };
        var history = ScreenTimeFiles.ParseHistory($$"""
            { "rules": { "5": {
              "{{ScreenTimeFiles.Text(Today.AddDays(-2))}}": { "seconds": 5400 },
              "{{ScreenTimeFiles.Text(Today.AddDays(-1))}}": { "seconds": 3600, "apps": { "{{Browser.Replace(@"\", @"\\")}}": 3000 } }
            } } }
            """);
        var log = ScreenTimeFiles.Read(live, history, Today, 5).Logs[5];
        var chart = ScreenTimeChart.Of(log, [.. ScreenTimeLine.GraphOf(log, Today, Today).Select(point => point.Day)]);

        Assert.Equal([Browser], chart.Top);
        var bars = chart.Bars.ToDictionary(bar => bar.Day);
        Assert.Equal((Hours(2), Hours(2), false), (bars[Today.AddDays(-3)].Total, bars[Today.AddDays(-3)].Other, bars[Today.AddDays(-3)].Split));
        Assert.Equal((Hours(1.5), false), (bars[Today.AddDays(-2)].Other, bars[Today.AddDays(-2)].Split));
        Assert.Equal((TimeSpan.FromSeconds(3000), TimeSpan.FromSeconds(600), true), (bars[Today.AddDays(-1)].Parts[0], bars[Today.AddDays(-1)].Other, bars[Today.AddDays(-1)].Split));
    }

    /// <summary>
    /// An app takes the palette's colour nearest its logo's, unless one before it took it; a grey
    /// logo one from its key; no two apps share one, and the same apps always get the same colours.
    /// </summary>
    [Fact]
    public void EveryAppHasAColourOfItsOwnAndKeepsIt()
    {
        var blue = ScreenTimeColours.HueOf(Tile(0xFF2D7DD2));
        Assert.InRange(blue!.Value, 205, 215);
        Assert.Null(ScreenTimeColours.HueOf(Tile(0xFF333333)));
        Assert.Null(ScreenTimeColours.HueOf(Tile(0xFFF0F0F0)));
        Assert.Null(ScreenTimeColours.HueOf(null));
        Assert.Null(ScreenTimeColours.HueOf(new Pixels { Data = new byte[16 * 16 * 4], Width = 16, Height = 16 }));
        Assert.InRange(ScreenTimeColours.HueOf(Tile(0xFFE0245E))!.Value, 335, 345);

        string[] apps = ["browser", "edge", "terminal", "notes"];
        float?[] hues = [blue, 205f, null, null];
        var slots = ScreenTimeColours.Slots(apps, hues);
        Assert.Equal(0, slots[0]); // blue
        Assert.Equal(1, slots[1]); // a second blue: the nearest free, teal
        Assert.Equal(apps.Length, slots.Distinct().Count());
        Assert.Equal(slots, ScreenTimeColours.Slots(apps, hues));
        // More apps than colours: each still has one.
        var many = Enumerable.Range(0, 11).Select(index => $"app{index}").ToArray();
        var manySlots = ScreenTimeColours.Slots(many, new float?[many.Length]);
        Assert.Equal(ScreenTimeColours.Count, manySlots.Take(ScreenTimeColours.Count).Distinct().Count());
        Assert.All(manySlots, slot => Assert.InRange(slot, 0, ScreenTimeColours.Count - 1));
        // The hash is the same on every run (string.GetHashCode is not), whatever the case.
        Assert.Equal(ScreenTimeColours.Hash("C:\\A.exe"), ScreenTimeColours.Hash("c:\\a.EXE"));
        Assert.Equal(0xE40C292Cu, ScreenTimeColours.Hash("a"));

        // The palette for dark text is darker than the one for light text, colour for colour.
        for (var slot = 0; slot < ScreenTimeColours.Count; slot++)
        {
            var light = ScreenTimeColours.Colour(slot, lightText: true);
            var dark = ScreenTimeColours.Colour(slot, lightText: false);
            Assert.True(light.r + light.g + light.b > dark.r + dark.g + dark.b, $"colour {slot}");
        }
    }

    /// <summary>
    /// The history draws the fortnight as bars by app (§603: always): a bar a day, the last at the trailing end, no
    /// words over them; a piece under the pointer says its app and its time, the column around it the day and its
    /// total, an older day's unsplit bar its total; a click on a bar publishes its day.
    /// </summary>
    [UiFact]
    public void ABarsPiecesSayTheirAppAndABarsClickShowsItsDay()
    {
        Standing(gfx =>
        {
            var days = WithOldDays();
            var folder = Folder(gfx, days);
            try
            {
                var total = LineOf(folder, 2);
                var style = StyleAt(1.5f);
                var box = FormBox(total, 1.5f);
                var plan = total.TotalPlan(box, style);
                var chart = plan.Chart!;
                Assert.Equal(ScreenTimeLine.GraphDays, plan.Bars.Count);
                Assert.Empty(plan.Said);
                Assert.True(plan.Bars[^1].Column.right <= box.right && plan.Bars[0].Column.left >= box.left);
                Assert.Equal(Browser, chart.Top[0]);
                Assert.NotEmpty(plan.Legend);
                Assert.Equal("Browser", plan.Legend[0].Name.Text);

                // Today's bar: the browser's piece at its foot.
                var today = plan.Bars[^1];
                Assert.Equal(Today, today.Day);
                var piece = today.Segments[0];
                Assert.Equal(0, piece.Part);
                var tip = total.TipOn(Middle(piece.Box), box, style, WidgetSurface.List)!.Value;
                Assert.Equal(S.WidgetScreenTimeTip(name: "Browser", time: ScreenTimeText.Duration(days.Log.Day(Today)!.Apps[Browser])), tip.Text);
                Assert.Equal(piece.Box, tip.Part);
                // The rest at the top.
                var rest = today.Segments[^1];
                Assert.Equal(ScreenTimeLine.OtherPart, rest.Part);
                Assert.StartsWith(S.WidgetScreenTimeOther, total.TipOn(Middle(rest.Box), box, style, WidgetSurface.List)!.Value.Text);
                // Over the bar, in its column: the day and its total.
                var above = new Vector2(Middle(today.Column).X, today.Column.top + 1);
                Assert.Equal(S.WidgetScreenTimeTip(name: S.WidgetScreenTimeToday, time: ScreenTimeText.Duration(days.Log.TotalOn(Today))), total.TipOn(above, box, style, WidgetSurface.List)!.Value.Text);
                // An older day's bar is one piece, whose tip is its total.
                var old = plan.Bars.First(bar => bar.Segments.Length > 0);
                Assert.Single(old.Segments);
                Assert.Equal(ScreenTimeLine.OtherPart, old.Segments[0].Part);
                Assert.EndsWith(ScreenTimeText.Duration(days.Log.TotalOn(old.Day)), total.TipOn(Middle(old.Segments[0].Box), box, style, WidgetSurface.List)!.Value.Text);
                Assert.Null(total.TipOn(new Vector2(Middle(box).X, box.top + 2), box, style, WidgetSurface.List));

                // The empty part of a bar's column lights it; its click publishes its day, which the apps follow
                // (a piece of the bar is its app's: APickedAppIsTheTotalsAndItsRowIsMarked).
                var pick = plan.Bars[^4];
                var target = total.TargetOn(new Vector2(Middle(pick.Column).X, pick.Column.top + 1), box, style, WidgetSurface.List);
                Assert.Equal(ScreenTimeLine.DayTarget + pick.Index, target);
                total.HoverOn(new Vector2(Middle(pick.Column).X, pick.Column.top + 1), box, style, WidgetSurface.List);
                Assert.Equal(target, total.Hot);
                total.Click(target);
                Assert.Equal(pick.Day, folder.ChannelValue(Channels.Day));
                Assert.Equal(pick.Day, LineOf(folder, 1).Shown);
                Assert.Equal(pick.Day, total.Shown);
                total.HoverOn(null, box, style, WidgetSurface.List);
            }
            finally
            {
                folder.Closed();
                folder.Dispose();
            }
        });
    }

    /// <summary>
    /// The apps' line colours each share's bar as the total's bars colour that app: the chart's top
    /// apps their colours, the others "Other"'s grey, never the accent; on another day of the folder
    /// too, and whether or not the total is in the folder at all. In high contrast both are the
    /// shown day's highlight, as the shown day's bar is.
    /// </summary>
    [UiFact]
    public void TheAppsBarsAreTheChartsColours()
    {
        Standing(gfx =>
        {
            var days = Fortnight();
            var folder = Folder(gfx, days);
            var alone = Folder(gfx, days, ListKind.ScreenTimeApps);
            try
            {
                var total = LineOf(folder, 2);
                var apps = LineOf(folder, 1);
                foreach (var shown in new DateOnly?[] { null, Today.AddDays(-3) })
                {
                    if (shown is { } day)
                    {
                        ((IWidgetContainer)folder).Publish(Channels.Day, day);
                        ((IWidgetContainer)alone).Publish(Channels.Day, day);
                    }
                    foreach (var dark in new[] { true, false })
                    {
                        var style = StyleAt(1.5f, 1f, dark);
                        var chart = total.TotalPlan(BoxOf(5, 1.5f), style);
                        Assert.NotNull(chart.Chart);
                        foreach (var plan in new[] { apps.AppsPlan(BoxOf(8, 1.5f), style), LineOf(alone, 0).AppsPlan(BoxOf(8, 1.5f), style) })
                        {
                            Assert.Equal(chart.Slots, plan.Slots);
                            Assert.True(plan.Rows.Count > ScreenTimeChart.MostApps, $"{plan.Rows.Count} rows");
                            var accent = TilePaint.Accent(style);
                            foreach (var row in plan.Rows)
                            {
                                var part = chart.Chart!.Top.ToList().FindIndex(app => string.Equals(app, row.App, StringComparison.OrdinalIgnoreCase));
                                Assert.Equal(part < 0 ? ScreenTimeLine.OtherPart : part, row.Part);
                                var ink = ScreenTimeLine.PartInk(plan.Slots, row.Part, true, style);
                                Assert.Equal(ScreenTimeLine.PartInk(chart.Slots, part < 0 ? ScreenTimeLine.OtherPart : part, false, style), ink);
                                Assert.NotEqual(accent, ink);
                            }
                            // Every top app's colour its own; the rest one grey.
                            var tops = plan.Rows.Where(row => row.Part >= 0).Select(row => ScreenTimeLine.PartInk(plan.Slots, row.Part, true, style)).ToList();
                            Assert.Equal(ScreenTimeChart.MostApps, tops.Distinct().Count());
                            Assert.Single(plan.Rows.Where(row => row.Part < 0).Select(row => ScreenTimeLine.PartInk(plan.Slots, row.Part, true, style)).Distinct());
                        }
                    }
                    var contrast = FolderFlyout.StyleFor(1.5f, Contrast(), FlyoutTheme.For(Contrast()));
                    var highlight = PopupPaint.Ink(contrast.Theme.Accent);
                    foreach (var row in apps.AppsPlan(BoxOf(8, 1.5f), contrast).Rows)
                    {
                        Assert.Equal(highlight, ScreenTimeLine.PartInk([], row.Part, true, contrast));
                    }
                }

                // As drawn: the most used app's bar, full, in the chart's colour (in high contrast
                // the highlight), read back off the pixels.
                ((IWidgetContainer)folder).Publish(Channels.Day, Today);
                foreach (var look in new[] { LookFor(true), LookFor(false), Contrast() })
                {
                    var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
                    var box = BoxOf(8, 1.5f);
                    var row = apps.AppsPlan(box, style).Rows[0];
                    var chart = total.TotalPlan(BoxOf(5, 1.5f), style);
                    var wanted = look.HighContrast ? PopupPaint.Ink(style.Theme.Accent) : ScreenTimeLine.PartInk(chart.Slots, 0, false, style);
                    Assert.Equal(0, row.Part);
                    (byte B, byte G, byte R, byte A) pixel = default;
                    Bitmap(box.right, box.bottom, dc => apps.PaintOn(dc, box, style, WidgetSurface.List), bitmap =>
                        pixel = PixelAt(bitmap, (uint)MathF.Ceiling(box.right), (uint)MathF.Ceiling(box.bottom), (int)((row.Bar.left + row.Bar.right) / 2), (int)((row.Bar.top + row.Bar.bottom) / 2)));
                    Assert.Equal(255, pixel.A);
                    Assert.InRange(Math.Abs(pixel.R - (wanted.r * 255)), 0, 2);
                    Assert.InRange(Math.Abs(pixel.G - (wanted.g * 255)), 0, 2);
                    Assert.InRange(Math.Abs(pixel.B - (wanted.b * 255)), 0, 2);
                }
            }
            finally
            {
                folder.Closed();
                folder.Dispose();
                alone.Closed();
                alone.Dispose();
            }
        });
    }

    /// <summary>
    /// The history at each size, every scale and text size (§603): the bars inside their chart, the chart in the line
    /// under no words, the legend under the bars and inside the line, the band taller at Full; always bars.
    /// </summary>
    [UiFact]
    public void TheHistoryIsBarsAtEachSize()
    {
        Standing(gfx =>
        {
            var folder = Folder(gfx, WithOldDays());
            try
            {
                var history = LineOf(folder, 2);
                foreach (var scale in Scales)
                {
                    foreach (var textScale in TextScales)
                    {
                        var style = StyleAt(scale, textScale);
                        var width = FolderFlyout.ListWidth(scale);
                        var bands = new List<float>();
                        foreach (var size in new[] { LineSize.Compact, LineSize.Full })
                        {
                            history.Settings.Size = size;
                            var box = PopupPaint.Rect(0, 0, width, history.ListHeight(width, style));
                            var plan = history.TotalPlan(box, style);
                            var at = $"{size} at {scale * 100:0} %, text {textScale * 100:0} %";
                            Assert.NotNull(plan.Chart);
                            Assert.Empty(plan.Said);
                            Assert.Equal(ScreenTimeLine.GraphDays, plan.Bars.Count);
                            Assert.True(plan.ChartBox.top >= box.top && plan.ChartBox.bottom <= box.bottom, at);
                            foreach (var segment in plan.Bars.SelectMany(bar => bar.Segments))
                            {
                                Assert.True(segment.Box.top >= plan.ChartBox.top - 0.5f && segment.Box.bottom <= plan.ChartBox.bottom + 0.5f, $"{at}: a piece outside the chart");
                            }
                            Assert.NotEmpty(plan.Legend);
                            foreach (var item in plan.Legend)
                            {
                                Assert.True(item.Name.Top >= plan.ChartBox.bottom && item.Name.Left + item.Name.Room <= box.right + 0.5f
                                    && item.Name.Top + (item.Name.Px * 1.2f) <= box.bottom + 1, $"{at}: the legend outside");
                            }
                            bands.Add(plan.ChartBox.bottom - plan.ChartBox.top);
                        }
                        Assert.True(bands[1] > bands[0], "the Full band is the taller");
                    }
                }
            }
            finally
            {
                folder.Closed();
                folder.Dispose();
            }
        });
    }

    // --- Pictures -----------------------------------------------------------------------------------

    /// <summary>
    /// Every height of the apps at 150 %, dark and light — screentime-apps[-light].png (today and the history at their
    /// sizes are ScreenTimeSplitTests') — and the ready-made folder (today, the history and the ring over the apps),
    /// dark and light at 100 % and at 225 % text, with an app's row lit: screentime-folder-*.png; and a
    /// month calendar over the two, a day picked: screentime-month-*.png.
    /// </summary>
    [UiFact]
    public void ThePictures()
    {
        Standing(gfx =>
        {
            var days = Fortnight();
            var folder = Folder(gfx, days);
            try
            {
                foreach (var dark in new[] { true, false })
                {
                    Heights(gfx, LineOf(folder, 1), 8, LookFor(dark), $"screentime-apps{(dark ? "" : "-light")}.png");
                }
            }
            finally
            {
                folder.Closed();
                folder.Dispose();
            }

            // Bars by app with the older days an earlier Karpo kept, a day picked and a bar lit, in
            // both themes and in high contrast: screentime-bars-<dark|light|contrast>.png.
            var old = Folder(gfx, WithOldDays());
            try
            {
                var total = LineOf(old, 2);
                ((IWidgetContainer)old).Publish(Channels.Day, Today.AddDays(-4));
                foreach (var (name, look) in new[] { ("dark", LookFor(true)), ("light", LookFor(false)), ("contrast", Contrast()) })
                {
                    var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
                    var box = PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(1.5f), total.ListHeight(FolderFlyout.ListWidth(1.5f), style));
                    total.HoverOn(Middle(total.TotalPlan(box, style).Bars[^2].Column), box, style, WidgetSurface.List);
                    ScreenTimeSplitTests.SizesSheet(gfx, total, look, $"screentime-bars-{name}.png");
                    total.HoverOn(null, box, style, WidgetSurface.List);
                }
            }
            finally
            {
                old.Closed();
                old.Dispose();
            }

            var preset = Presets.Find("screenTimeGroup")!;
            var made = Presets.Folder(preset, new PresetMachine(false, []));
            var island = new FolderIsland($"{Owner}/{made.Id}", made, gfx, new Config());
            foreach (var tile in island.Tiles)
            {
                Feed(tile.Island, days);
            }
            island.Opened(gfx);
            try
            {
                foreach (var dark in new[] { true, false })
                {
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    foreach (var textScale in TextScales)
                    {
                        var look = LookFor(dark, textScale);
                        var (_, plan) = Plan(island, look, 1f);
                        panels.Add(($"Screen time: text {textScale * 100:0} %", look, island, plan));
                    }
                    // The list narrow and wide (2026-10-10, a width per list): the bars and the
                    // apps' rows follow the list's width.
                    foreach (var width in new[] { FolderListWidth.Narrow, FolderListWidth.Wide })
                    {
                        made.Folder!.Widths = [width];
                        island.Configure(made, new Config());
                        var (_, sized) = Plan(island, LookFor(dark), 1f);
                        panels.Add(($"Screen time: {width}, {FolderListWidths.Dip(width)} DIP", LookFor(dark), island, sized));
                    }
                    made.Folder!.Widths = null;
                    island.Configure(made, new Config());
                    var apps = (ScreenTimeLine)island.Tiles[3].Island;
                    var style = StyleAt(1f, 1f, dark);
                    var (_, first) = Plan(island, LookFor(dark), 1f);
                    var appsBox = first.Cards[3];
                    var row = apps.AppsPlan(PopupPaint.Rect(0, 0, appsBox.right - appsBox.left, appsBox.bottom - appsBox.top), style).Rows[1];
                    apps.HoverOn(Middle(row.Box), PopupPaint.Rect(0, 0, appsBox.right - appsBox.left, appsBox.bottom - appsBox.top), style, WidgetSurface.List);
                    Sheet(gfx, $"screentime-folder-{(dark ? "dark" : "light")}.png", 1f, panels);
                    apps.HoverOn(null, appsBox, style, WidgetSurface.List);
                }
            }
            finally
            {
                island.Closed();
                island.Dispose();
            }

            // A month calendar over the two, a day picked on it, which both show:
            // screentime-month-<dark|light>.png.
            var withMonth = FolderOf(gfx, days, Line(ListKind.Month),
                Line(ListKind.ScreenTime), Line(ListKind.ScreenTimeHistory), Line(ListKind.ScreenTimeApps));
            try
            {
                ((MonthTile)withMonth.Tiles[0].Island).Pick(Today.AddDays(-3));
                foreach (var dark in new[] { true, false })
                {
                    var look = LookFor(dark);
                    var (_, plan) = Plan(withMonth, look, 1f);
                    Sheet(gfx, $"screentime-month-{(dark ? "dark" : "light")}.png", 1f, [("A month's day, followed by screen time", look, withMonth, plan)]);
                }
            }
            finally
            {
                withMonth.Closed();
                withMonth.Dispose();
                Clock.Now = Clock.Real;
            }
        });
    }

    /// <summary>
    /// The folder's app, in four folders side by side — nothing; the pointer on an app's row; an app
    /// picked; an app picked and another pointed at in the bars — in dark, light and high contrast:
    /// screentime-app-&lt;dark|light|contrast&gt;.png.
    /// </summary>
    [UiFact]
    public void TheAppPictures()
    {
        Standing(gfx =>
        {
            var days = Fortnight();
            foreach (var (name, look) in new[] { ("dark", LookFor(true)), ("light", LookFor(false)), ("contrast", Contrast()) })
            {
                var style = FolderFlyout.StyleFor(1f, look, FlyoutTheme.For(look));
                var folders = new List<FolderIsland>();
                var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                try
                {
                    foreach (var caption in new[] { "Nothing", "Pointer on a row", "Mail picked", "Mail picked, Browser pointed at in the bars" })
                    {
                        var folder = FolderOf(gfx, days, Line(ListKind.ScreenTime), Line(ListKind.ScreenTimeApps), Line(ListKind.ScreenTimeHistory));
                        folders.Add(folder);
                        var (_, plan) = Plan(folder, look, 1f);
                        var totalBox = Local(plan.Cards[2]);
                        var appsBox = Local(plan.Cards[1]);
                        var total = LineOf(folder, 2);
                        var apps = LineOf(folder, 1);
                        var rows = apps.AppsPlan(appsBox, style).Rows;
                        switch (caption)
                        {
                            case "Pointer on a row":
                                apps.HoverOn(Middle(rows[1].Box), appsBox, style, WidgetSurface.List);
                                break;
                            case "Mail picked":
                                apps.Click(ScreenTimeLine.AppTarget + rows.First(row => row.App == Mail).Index);
                                break;
                            case "Mail picked, Browser pointed at in the bars":
                                apps.Click(ScreenTimeLine.AppTarget + rows.First(row => row.App == Mail).Index);
                                var bar = total.TotalPlan(totalBox, style).Bars[^2];
                                total.HoverOn(Middle(bar.Segments.First(segment => segment.Part == 0).Box), totalBox, style, WidgetSurface.List);
                                break;
                        }
                        panels.Add((caption, look, folder, Plan(folder, look, 1f).Plan));
                    }
                    Sheet(gfx, $"screentime-app-{name}.png", 1f, panels);
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
        });
    }

    private static D2D_RECT_F Local(D2D_RECT_F card) => PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top);

    /// <summary>The pixel at (<paramref name="x"/>, <paramref name="y"/>): premultiplied blue, green, red and alpha.</summary>
    private static (byte B, byte G, byte R, byte A) PixelAt(TerraFX.Interop.Windows.IWICBitmap* bitmap, uint width, uint height, int x, int y)
    {
        TerraFX.Interop.Windows.IWICBitmapLock* locked = null;
        try
        {
            var all = new TerraFX.Interop.Windows.WICRect { X = 0, Y = 0, Width = (int)width, Height = (int)height };
            Karpo.Dock.Native.ComUtil.Check(bitmap->Lock(&all, (uint)TerraFX.Interop.Windows.WICBitmapLockFlags.WICBitmapLockRead, &locked));
            uint stride = 0;
            Karpo.Dock.Native.ComUtil.Check(locked->GetStride(&stride));
            uint size = 0;
            byte* data = null;
            Karpo.Dock.Native.ComUtil.Check(locked->GetDataPointer(&size, &data));
            var at = (y * stride) + (x * 4);
            return (data[at], data[at + 1], data[at + 2], data[at + 3]);
        }
        finally
        {
            Karpo.Dock.Native.ComUtil.Release(ref locked);
        }
    }

    /// <summary>Windows' high contrast black.</summary>
    private static Look Contrast() => new(
        Glass: false,
        Dark: true,
        Accent: Windows.UI.Color.FromArgb(255, 0x00, 0x78, 0xD4),
        HighContrast: true,
        HcWindow: Windows.UI.Color.FromArgb(255, 0, 0, 0),
        HcText: Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF),
        HcHighlight: Windows.UI.Color.FromArgb(255, 0x1A, 0xEB, 0xFF),
        HcHighlightText: Windows.UI.Color.FromArgb(255, 0, 0, 0));

    /// <summary>One line at every height from one row to <paramref name="most"/>, four to a row, at 150 %.</summary>
    internal static void Heights(Gfx gfx, ScreenTimeLine line, int most, Look look, string file)
    {
        const float scale = 1.5f;
        var dark = look.Dark;
        var theme = FlyoutTheme.For(look);
        var style = FolderFlyout.StyleFor(scale, look, theme);
        var width = FolderFlyout.ListWidth(scale);
        const int across = 4;
        var pad = PopupPaint.Round(16 * scale);
        var label = PopupPaint.Round(11 * scale);
        var labelLine = TilePaint.LineHeight(gfx, label);
        var bands = Enumerable.Range(0, (most + across - 1) / across)
            .Select(band => Enumerable.Range((band * across) + 1, Math.Min(across, most - (band * across))).Max(rows => Rows(rows, scale, 1f)))
            .ToList();
        var sheetWidth = pad + (across * (width + pad));
        var sheetHeight = pad + bands.Sum(band => labelLine + band + pad);
        Bitmap(sheetWidth, sheetHeight, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, sheetWidth, sheetHeight), 0, PopupPaint.Ink(dark ? Windows.UI.Color.FromArgb(255, 0x1C, 0x1C, 0x1C) : Windows.UI.Color.FromArgb(255, 0xE8, 0xE8, 0xE8)));
            var y = pad;
            for (var band = 0; band < bands.Count; band++)
            {
                for (var slot = 0; slot < across; slot++)
                {
                    var rows = (band * across) + slot + 1;
                    if (rows > most)
                    {
                        break;
                    }
                    var x = pad + (slot * (width + pad));
                    var height = Rows(rows, scale, 1f);
                    PopupPaint.Text(dc, gfx, $"{rows} row{(rows == 1 ? "" : "s")}", label, Weight.Regular, theme.TextSecondary, x, y);
                    var box = PopupPaint.Rect(x, y + labelLine, width, height);
                    PopupPaint.Fill(dc, box, PopupPaint.Round(8 * scale), PopupPaint.Ink(theme.Base));
                    PopupPaint.Stroke(dc, box, PopupPaint.Round(8 * scale), MathF.Max(1, MathF.Round(scale)), PopupPaint.Ink(theme.Stroke));
                    var context = dc;
                    PopupPaint.Clipped(context, box, PopupPaint.Round(8 * scale), () => Shifted(context, box.left, box.top, () =>
                        FolderFlyout.PaintContent(context, line, PopupPaint.Rect(0, 0, width, height), style)));
                }
                y += labelLine + bands[band] + pad;
            }
        }, bitmap => Save(bitmap, Path.Combine(Pictures, file)));
    }
}

[CollectionDefinition(ScreenTimeLinesTests.Names, DisableParallelization = true)]
public sealed class ScreenTimeLinesCollection;
