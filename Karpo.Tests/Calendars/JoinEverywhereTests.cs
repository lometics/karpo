// A meeting's Join wherever Karpo lists meetings (docs\widgets.md §206): the day's events, the day and
// week views and the calendar's agenda give a meeting with a link the Join the next meetings' line has,
// and only while it is not over; a click on it joins and is not the event's (no day picked); Enter joins
// the lit one; a right click on a meeting has "Join meeting". The pictures — the lines at every height and
// the date-and-time face's "Clock & calendar" folder, dark and light at 150 % — go to KARPO_PNG_DIR as
// join-*.png. The drawing tests run only with KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Core;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Calendars;

public sealed class JoinEverywhereTests
{
    private const string Teams = "https://teams.microsoft.com/l/meetup-join/19%3ameeting_abc%40thread.v2/0";
    private const string Zoom = "https://us02web.zoom.us/j/85550001111";

    private static readonly DateTime Day = new(2026, 10, 16);

    /// <summary>The minute it is: the standup over, the review in five minutes, so lit.</summary>
    private static readonly DateTime Now = Day.AddHours(9).AddMinutes(55);

    private static CalendarEvent At(double hour, double minutes, string title, string? join = null, string? where = null)
    {
        var start = Day.AddMinutes(Math.Round(hour * 60));
        return new CalendarEvent(start, start.AddMinutes(minutes), false, title, where, Join: join);
    }

    /// <summary>A standup over, a review lit, a Zoom call later, a workshop with no link, and an all-day release with one.</summary>
    private static CalendarStore Store()
    {
        var store = new CalendarStore();
        store.Add(new ListCalendar("work", "Work", CalendarColours.Of("#0F6CBD"),
        [
            At(9, 15, "Daily standup", Teams),
            At(10, 60, "Design review", Teams, "Room 3"),
            At(11.5, 30, "1:1 with Anna", Zoom),
            At(14, 120, "Customer workshop", null, "Zurich office"),
            new CalendarEvent(Day, Day.AddDays(1), true, "Release day", null, Join: Teams),
        ]));
        return store;
    }

    // --- Which events have a Join ----------------------------------------------------------------

    [Fact]
    public void AJoinIsAMeetingWithALinkNotOver()
    {
        var zone = TimeZoneInfo.Utc;
        Assert.Null(JoinButton.Of(At(9, 15, "Over", Teams), Now, zone));
        Assert.NotNull(JoinButton.Of(At(10, 60, "Coming", Teams), Now, zone));
        Assert.NotNull(JoinButton.Of(At(9.5, 60, "On now", Zoom), Now, zone));
        Assert.Null(JoinButton.Of(At(10, 60, "No link"), Now, zone));
        Assert.Null(JoinButton.Of(At(10, 60, "Not a meeting's link", "https://example.com/agenda"), Now, zone));
        Assert.Null(JoinButton.Of(new CalendarEvent(Day, Day.AddDays(1), true, "All day", null, Join: Teams), Now, zone));
        // Lit from ten minutes before until it ends.
        Assert.True(JoinButton.Lit(At(10, 60, "In 5 min", Teams), Now, zone));
        Assert.False(JoinButton.Lit(At(10.5, 60, "In 35 min", Teams), Now, zone));
        Assert.True(JoinButton.Lit(At(9.5, 60, "On now", Teams), Now, zone));
    }

    [Fact]
    public void TheCalendarMenusOfferJoinMeetingFirst()
    {
        foreach (var kind in new[] { ListKind.Calendar, ListKind.Events, ListKind.DayView, ListKind.WeekView, ListKind.Meeting })
        {
            Assert.Equal(WidgetAct.JoinMeeting, Karpo.Dock.Dock.ActsFor(kind)[0]);
        }
        // The year's months show no events.
        Assert.DoesNotContain(WidgetAct.JoinMeeting, Karpo.Dock.Dock.ActsFor(ListKind.YearView));
        Assert.Equal(WidgetAct.JoinMeeting, Karpo.Dock.Dock.ActsFor(FolderFaces.Meeting)[0]);
        // Over nothing, the row is not there.
        var (items, rows) = Karpo.Dock.Dock.ActRows([WidgetAct.JoinMeeting], widget: null);
        Assert.Empty(rows);
        Assert.Empty(items);
    }

    // --- The lines ---------------------------------------------------------------------------------

    /// <summary>Runs <paramref name="body"/> with the clock at <see cref="Now"/>, the store's events, and Join opening into a list.</summary>
    private static void Staged(Action<Gfx, List<string>> body) =>
        Offscreen.OnStaThread(gfx =>
        {
            var real = Clock.Now;
            var opened = new List<string>();
            Clock.Now = () => Now;
            CalendarIsland.Made = Store();
            MeetingJoin.Opening = opened.Add;
            CalendarApp.Stand = () => "outlookcal:"; // not-ui
            try
            {
                body(gfx, opened);
            }
            finally
            {
                Clock.Now = real;
                CalendarIsland.Made = null;
                MeetingJoin.Opening = null;
                CalendarApp.Stand = null;
            }
        });

    private static T Line<T>(Gfx gfx, ListKind kind)
        where T : class
    {
        var item = kind.New();
        item.Id = kind.Id();
        return (T)Karpo.Dock.Islands.CreateLine(Config.IslandKey("layout:default", item.Id), item, kind, gfx, new Config())!;
    }

    private static TileStyle Style(bool dark = true) => FolderFlyout.StyleFor(1.5f, ListFormPictures.LookFor(dark), FlyoutTheme.For(ListFormPictures.LookFor(dark)));

    private static Vector2 Middle(D2D_RECT_F box) => new((box.left + box.right) / 2, (box.top + box.bottom) / 2);

    /// <summary>The "Join" menu row a right click at <paramref name="point"/> gives <paramref name="line"/>: its link, or null for none.</summary>
    private static string? MenuJoin(CellIsland line, Vector2 point, D2D_RECT_F box, TileStyle style)
    {
        line.HoverOn(point, box, style, WidgetSurface.List);
        var (items, rows) = Karpo.Dock.Dock.ActRows(Karpo.Dock.Dock.ActsFor((line.Settings as CellItem)!.List!.Value), line);
        var row = rows.FirstOrDefault(each => each.Act == WidgetAct.JoinMeeting);
        if (row is not null)
        {
            Assert.Contains(items, item => item.Id == row.Id && item.Text == S.DockMeetingJoinMeeting);
        }
        return row?.On;
    }

    [UiFact]
    public void TheDaysEventsJoinTheirMeetings()
    {
        Staged((gfx, opened) =>
        {
            using var line = Line<EventsTile>(gfx, ListKind.Events);
            var style = Style();
            var box = PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(1.5f), line.ListHeight(FolderFlyout.ListWidth(1.5f), style));
            var pieces = line.LinePieces(box, style);
            var row = (string title) => line.Rows.Single(each => each.Event.Title == title);
            // A Join for the review (lit, "Join" worded) and the Zoom call; none for the standup over, the workshop, the release.
            Assert.True(TilePaint.Width(row("Design review").Join) > 0);
            Assert.True(row("Design review").Lit);
            var worded = pieces.Single(piece => piece.Text == JoinButton.Name("Design review")).Box;
            Assert.True(TilePaint.Width(worded) > TilePaint.Height(worded));
            Assert.True(TilePaint.Width(row("1:1 with Anna").Join) > 0);
            Assert.False(row("1:1 with Anna").Lit);
            Assert.Equal(0, TilePaint.Width(row("Daily standup").Join));
            Assert.Equal(0, TilePaint.Width(row("Customer workshop").Join));
            Assert.Equal(0, TilePaint.Width(row("Release day").Join));
            // Called by its meeting for a screen reader.
            Assert.Contains(pieces, piece => piece.Text == JoinButton.Name("Design review") && piece.Name.StartsWith("join ", StringComparison.Ordinal));

            // A click on the Zoom call's Join joins it once the folder has gone; its words are its row's, the event's,
            // which opens it in a calendar (§434) — here the calendar app — as a row with no meeting does.
            var zoom = row("1:1 with Anna");
            var target = line.TargetOn(Middle(zoom.Join), box, style, WidgetSurface.List);
            Assert.True(target >= JoinButton.Target);
            Assert.Equal(IslandView.App, line.Click(target).View);
            Assert.Equal(CellAct.Action, FolderFlyout.ActOf(new FolderCell { Island = ListKind.Events.New() }, line));
            line.Open();
            Assert.Contains("85550001111", Assert.Single(opened));
            var words = new Vector2(zoom.Box.left + 60, Middle(zoom.Box).Y);
            var opensIt = line.TargetOn(words, box, style, WidgetSurface.List);
            Assert.True(opensIt is > 0 and < JoinButton.Target);
            Assert.Equal(IslandView.App, line.Click(opensIt).View);
            line.Open();
            Assert.Equal("outlookcal:", opened[^1]);
            Assert.True(line.TargetOn(Middle(row("Customer workshop").Box), box, style, WidgetSurface.List) is > 0 and < JoinButton.Target);
            Assert.Equal(IslandView.None, line.Click(0).View);

            // Enter joins the lit review.
            Assert.False(line.KeyOn(VK.VK_RETURN, WidgetSurface.List));
            Assert.Equal(IslandView.App, line.Click(0).View);
            line.Open();
            Assert.EndsWith("thread.v2/0", opened[^1]);

            // The menu: "Join meeting" over a meeting with a Join, none over the workshop.
            Assert.Equal(Zoom, MenuJoin(line, words, box, style));
            Assert.Null(MenuJoin(line, Middle(row("Customer workshop").Box), box, style));
        });
    }

    [UiFact]
    public void TheDayAndWeekViewsJoinOnTheirBlocks()
    {
        Staged((gfx, opened) =>
        {
            var style = Style();
            var width = FolderFlyout.ListWidth(1.5f);
            using var day = Line<CalendarViewTile>(gfx, ListKind.DayView);
            var box = PopupPaint.Rect(0, 0, width, LineRows.Px(12, style));
            var plan = day.Build(width, box.bottom, style);
            var block = (string title) => plan.Grid!.Blocks.Single(each => each.Title == title && each.Box.left < plan.Grid.Column(plan.Grid.Shown + 1) && each.Box.left >= plan.Grid.Column(plan.Grid.Shown));
            Assert.True(TilePaint.Width(block("Design review").Join) > 0);
            Assert.True(block("Design review").Lit);
            Assert.True(TilePaint.Width(block("1:1 with Anna").Join) > 0);
            Assert.Equal(0, TilePaint.Width(block("Daily standup").Join));
            Assert.Null(block("Daily standup").Link);
            Assert.Equal(0, TilePaint.Width(block("Customer workshop").Join));
            // Every Join is a meeting's, inside its block.
            foreach (var each in plan.Grid!.Blocks.Where(each => TilePaint.Width(each.Join) > 0))
            {
                Assert.NotNull(each.Link);
                Assert.True(each.Join.left >= each.Box.left && each.Join.right <= each.Box.right && each.Join.top >= each.Box.top && each.Join.bottom <= each.Box.bottom, each.Title);
            }

            // A click on the review's Join joins it and picks no day; beside it on the block, the day is picked.
            var pieces = day.LinePieces(box, style);
            var join = pieces.Single(piece => piece.Text == JoinButton.Name("Design review"));
            var target = day.TargetOn(Middle(join.Box), box, style, WidgetSurface.List);
            Assert.True(target >= CalendarViewTile.JoinTarget);
            Assert.Equal(IslandView.App, day.Click(target).View);
            day.Open();
            Assert.EndsWith("thread.v2/0", Assert.Single(opened));
            var beside = new Vector2(join.Box.left - (20 * 1.5f), Middle(join.Box).Y);
            var dayTarget = day.TargetOn(beside, box, style, WidgetSurface.List);
            Assert.True(dayTarget is >= CalendarViewTile.DayTarget and < CalendarViewTile.JoinTarget);
            Assert.Equal(IslandView.None, day.Click(dayTarget).View);
            Assert.Single(opened);
            // The menu over the review's block.
            Assert.Equal(Teams, MenuJoin(day, beside, box, style));
            // Enter joins the lit review.
            Assert.False(day.KeyOn(VK.VK_RETURN, WidgetSurface.List));
            Assert.Equal(IslandView.App, day.Click(0).View);

            // The week: a Join only on a meeting's block, and only where the block has the room.
            using var week = Line<CalendarViewTile>(gfx, ListKind.WeekView);
            var weekPlan = week.Build(width, box.bottom, style);
            Assert.All(weekPlan.Grid!.Blocks, each => Assert.True(TilePaint.Width(each.Join) == 0 || each.Link is not null, each.Title));
            Assert.All(weekPlan.Grid.Blocks.Where(each => each.Title is "Daily standup" or "Customer workshop"), each => Assert.Equal(0, TilePaint.Width(each.Join)));
            // The review's block, a column of a week, holds the camera alone.
            var review = weekPlan.Grid.Blocks.First(each => each.Title == "Design review");
            Assert.True(TilePaint.Width(review.Join) > 0);
            Assert.False(review.Worded);
        });
    }

    [UiFact]
    public void TheAgendaJoinsInItsWholeForm()
    {
        Staged((gfx, opened) =>
        {
            using var line = Line<CalendarIsland>(gfx, ListKind.Calendar);
            var style = Style();
            var width = FolderFlyout.ListWidth(1.5f);
            var box = PopupPaint.Rect(0, 0, width, LineRows.Px(6, style));
            var pieces = line.LinePieces(box, style);
            var joins = pieces.Where(piece => piece.Name.StartsWith("join ", StringComparison.Ordinal)).Select(piece => piece.Text).ToList();
            Assert.Equal([JoinButton.Name("Design review"), JoinButton.Name("1:1 with Anna")], joins);
            var join = pieces.First(piece => piece.Text == JoinButton.Name("1:1 with Anna"));
            var target = line.TargetOn(Middle(join.Box), box, style, WidgetSurface.List);
            Assert.True(target > 0);
            Assert.Equal(IslandView.App, line.Click(target).View);
            line.Open();
            Assert.Contains("85550001111", Assert.Single(opened));
            // The menu over the Zoom call's row; one row has no room for the button, and shows the all-day release, no meeting.
            Assert.Equal(Zoom, MenuJoin(line, new Vector2(join.Box.left - (60 * 1.5f), Middle(join.Box).Y), box, style));
            var one = PopupPaint.Rect(0, 0, width, LineRows.Px(1, style));
            Assert.DoesNotContain(line.LinePieces(one, style), piece => piece.Name.StartsWith("join ", StringComparison.Ordinal));
            Assert.Null(MenuJoin(line, Middle(one), one, style));
        });
    }

    // --- Pictures ----------------------------------------------------------------------------------

    /// <summary>
    /// The lines at every height, dark and light at 150 % (join-events, join-dayView, join-weekView,
    /// join-calendar), and the "Clock & calendar" folder the date-and-time face opens (join-clockCalendar).
    /// </summary>
    [UiFact]
    public void JoinPictures()
    {
        Staged((gfx, unused) =>
        {
            foreach (var kind in new[] { ListKind.Events, ListKind.DayView, ListKind.WeekView, ListKind.Calendar })
            {
                using var line = Line<CellIsland>(gfx, kind);
                foreach (var dark in new[] { true, false })
                {
                    ListLinesTests.Sheet(gfx, line, kind, 1.5f, ListFormPictures.LookFor(dark), Path.Combine(Offscreen.Pictures, $"join-{kind.Id()}{(dark ? "" : "-light")}.png"));
                }
            }
            CalendarApp.Stand = () => "outlookcal:"; // not-ui
            var folders = new List<FolderIsland>();
            try
            {
                var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                foreach (var dark in new[] { true, false })
                {
                    var holder = Presets.Folder(Presets.Find(Presets.ClockCalendarId)!, PresetsTests.Laptop);
                    var folder = new FolderIsland($"layout:default/{holder.Id}-{(dark ? "dark" : "light")}", holder, gfx, new Config());
                    folders.Add(folder);
                    _ = ((IIsland)folder).Refresh(gfx);
                    folder.Opened(gfx);
                    var look = ListFormPictures.LookFor(dark);
                    var (_, plan) = ListFormPictures.Plan(folder, look, 1.5f);
                    panels.Add(($"Clock & calendar, {(dark ? "dark" : "light")}", look, folder, plan));
                }
                ListFormPictures.Sheet(gfx, "join-clockCalendar.png", 1.5f, panels);
            }
            finally
            {
                CalendarApp.Stand = null;
                foreach (var folder in folders)
                {
                    folder.Closed();
                    folder.Dispose();
                }
            }
        });
    }
}
