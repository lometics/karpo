// The next meeting drawn (docs\widgets.md §205): the meeting preset's face on the bar in every state —
// a meeting coming, lit from its lead, started, just starting, without a link, none left — at its three
// widths, with and without Join, at 100 % and 150 %, dark and light, and down a bar on its side; and the
// meeting line at one to eight rows. The sheets go to KARPO_PNG_DIR (else %TEMP%\karpo-tests\pictures):
// meeting-faces-<dark|light>.png, meeting-lines.png, meeting-lines-light.png. A click on the face's Join
// opens the meeting, anywhere else on the face is the folder's; a click on a line's Join opens it once
// the folder has gone. Runs only with KARPO_UI_TESTS=1, like the other picture tests.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;

namespace Karpo.Tests.Calendars;

public sealed class MeetingPictures
{
    private const string Teams = "https://teams.microsoft.com/l/meetup-join/19%3ameeting_abc%40thread.v2/0";
    private const string Zoom = "https://us02web.zoom.us/j/85550001111";

    private static readonly DateTime Day = new(2026, 10, 16);

    private static CalendarEvent At(double hour, double minutes, string title, string? join = null, string? where = null)
    {
        var start = Day.AddMinutes(Math.Round(hour * 60));
        return new CalendarEvent(start, start.AddMinutes(minutes), false, title, where, Join: join);
    }

    /// <summary>
    /// A working day: a standup, a review in a room and on Teams, a Zoom call, an all-hands running into a
    /// workshop without a link, a call in the evening, and an all-day release that is no meeting.
    /// </summary>
    private static CalendarStore Store()
    {
        var store = new CalendarStore();
        store.Add(new ListCalendar("work", "Work", CalendarColours.Of("#0F6CBD"),
        [
            At(9, 15, "Daily standup", Teams),
            At(10, 60, "Quarterly planning review with the product team", Teams, "Room 3"),
            At(11.5, 30, "1:1 with Anna", Zoom),
            At(12.75, 105, "All-hands", Teams),
            At(14, 120, "Customer workshop", null, "Zurich office"),
            new CalendarEvent(Day, Day.AddDays(1), true, "Release day", null),
        ]));
        store.Add(new ListCalendar("home", "Personal", CalendarColours.Of("#C239B3"),
        [
            At(17.5, 30, "Call with Sam", "https://meet.google.com/abc-defg-hij"),
            At(24 + 10, 60, "Family brunch", "https://meet.google.com/xyz-abcd-efg"),
        ]));
        return store;
    }

    private static Look LookFor(bool dark) => ListFormPictures.LookFor(dark) with
    {
        AccentLight2 = Windows.UI.Color.FromArgb(255, 0x4C, 0xC2, 0xFF),
        AccentDark1 = Windows.UI.Color.FromArgb(255, 0x00, 0x5F, 0xB8),
    };

    private static BarItem Folder(string show = MeetingFace.Today, string width = MeetingFace.Standard, bool join = true)
    {
        var folder = Presets.Folder(Presets.Find(Presets.MeetingId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
        var face = folder.Folder!.Face;
        face.Set(FolderFaces.Meeting.Option(MeetingFace.Show)!, show);
        face.Set(FolderFaces.Meeting.Option(MeetingFace.Width)!, width);
        face.Set(FolderFaces.Meeting.Option(MeetingFace.Join)!, join);
        return folder;
    }

    /// <summary>What a sheet shows: a caption, the folder, the minute it is drawn at, the bar's scale and whether it stands on its side.</summary>
    private sealed record Shot(string Caption, BarItem Folder, DateTime At, float Scale = 1f, bool Vertical = false);

    [UiFact]
    public void TheFaceAndTheLineAreDrawnAndJoin()
    {
        var store = Store();
        Offscreen.OnStaThread(gfx =>
        {
            var real = Clock.Now;
            var opened = new List<string>();
            CalendarIsland.Made = store;
            MeetingJoin.Opening = opened.Add;
            try
            {
                var standard = Folder();
                Shot[] shots =
                [
                    new("Coming, in 25 min", standard, Day.AddHours(9).AddMinutes(-25)),
                    new("Lit, in 4 min", standard, Day.AddHours(9).AddMinutes(-4)),
                    new("Now", standard, Day.AddHours(9).AddSeconds(20)),
                    new("Started 3 min ago", standard, Day.AddHours(9).AddMinutes(3)),
                    new("Overlap: the all-hands gives way, the next in 5 min", standard, Day.AddHours(14).AddMinutes(-5)),
                    new("Started, no link", standard, Day.AddHours(14).AddMinutes(10)),
                    new("None left today", standard, Day.AddHours(20)),
                    new("Compact, lit", Folder(width: MeetingFace.Compact), Day.AddHours(9).AddMinutes(-4)),
                    new("Wide, lit", Folder(width: MeetingFace.Wide), Day.AddHours(10).AddMinutes(-4)),
                    new("Wide, coming", Folder(width: MeetingFace.Wide), Day.AddHours(9).AddMinutes(30)),
                    new("No Join button", Folder(join: false), Day.AddHours(10).AddMinutes(-4)),
                    new("Next, tomorrow", Folder(show: MeetingFace.Next), Day.AddHours(20)),
                    new("150 %, lit", standard, Day.AddHours(9).AddMinutes(-4), 1.5f),
                    new("150 %, started", Folder(width: MeetingFace.Wide), Day.AddHours(10).AddMinutes(12), 1.5f),
                    new("150 %, none", standard, Day.AddHours(20), 1.5f),
                    new("On its side, lit", standard, Day.AddHours(9).AddMinutes(-4), 1.5f, Vertical: true),
                ];
                foreach (var dark in new[] { true, false })
                {
                    Faces(gfx, shots, LookFor(dark), Path.Combine(Offscreen.Pictures, $"meeting-faces-{(dark ? "dark" : "light")}.png"));
                }

                // Set to show a meeting only shortly before it, the folder takes no room until then.
                Clock.Now = () => Day.AddHours(8);
                var soon = Folder(show: MeetingFace.Soon);
                using (var hidden = new FolderIsland(Config.IslandKey("layout:default", soon.Id), soon, gfx, new Config()))
                {
                    Assert.Equal(0, hidden.Measure(new IslandStyle(1, false, 48, 24, LookFor(true))).Count);
                    Clock.Now = () => Day.AddHours(9).AddMinutes(-5);
                    Assert.Equal(1, hidden.Measure(new IslandStyle(1, false, 48, 24, LookFor(true))).Count);
                }

                // A click on Join opens the meeting; on its words it is the folder's.
                Clock.Now = () => Day.AddHours(9).AddMinutes(-4);
                using (var face = new FolderIsland(Config.IslandKey("layout:default", standard.Id), standard, gfx, new Config()))
                {
                    var style = new IslandStyle(1.5f, false, 72, 36, LookFor(true));
                    var (width, height) = face.CellSize(0, style);
                    Assert.True(face.ClickAt(0, new Vector2(width - 20, height / 2), new Vector2(width, height), style));
                    // To Teams itself where this PC has it (msteams:), else its link.
                    Assert.EndsWith("/l/meetup-join/19%3ameeting_abc%40thread.v2/0", Assert.Single(opened));
                    Assert.False(face.ClickAt(0, new Vector2(30, height / 2), new Vector2(width, height), style));
                    Assert.Single(opened);
                }

                // The line at one to eight rows, at the review's lead: on now the standup is over, the review lit.
                Clock.Now = () => Day.AddHours(10).AddMinutes(-6);
                var line = ListKind.Meeting.New();
                line.Id = "meeting";
                using var island = (MeetingTile)Karpo.Dock.Islands.CreateLine(Config.IslandKey("layout:default", "meeting"), line, ListKind.Meeting, gfx, new Config())!;
                foreach (var dark in new[] { true, false })
                {
                    ListLinesTests.Sheet(gfx, island, ListKind.Meeting, 1.5f, LookFor(dark), Path.Combine(Offscreen.Pictures, $"meeting-lines{(dark ? "" : "-light")}.png"));
                }
                // Its rows: the review lit, the Zoom call and the rest of the day after it; the all-day release none.
                var tileStyle = FolderFlyout.StyleFor(1.5f, LookFor(true), FlyoutTheme.For(LookFor(true)));
                var box = PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(1.5f), LineRows.Px(4, tileStyle));
                var plan = island.Lay(box, tileStyle);
                Assert.False(plan.Single);
                Assert.Equal(new[] { "Quarterly planning review with the product team", "1:1 with Anna", "All-hands" }, plan.Rows.Take(3).Select(row => row.Title).ToArray());
                Assert.True(plan.Rows[0].Lit);
                Assert.False(plan.Rows[1].Lit);
                Assert.True(TilePaint.Width(plan.Rows[0].Join) > 0);
                var tall = island.Lay(PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(1.5f), LineRows.Px(8, tileStyle)), tileStyle);
                Assert.Equal(0, TilePaint.Width(tall.Rows.Single(row => row.Title == "Customer workshop").Join));
                Assert.DoesNotContain(tall.Rows, row => row.Title == "Release day");
                var one = island.Lay(PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(1.5f), LineRows.Px(1, tileStyle)), tileStyle);
                Assert.True(one.Single);
                Assert.Single(one.Rows);
                // A click on the second row's Join opens the Zoom call once the folder has gone.
                var plan2 = island.Lay(box, tileStyle);
                var join = plan2.Rows[1].Join;
                var target = island.TargetOn(new Vector2((join.left + join.right) / 2, (join.top + join.bottom) / 2), box, tileStyle, WidgetSurface.List);
                Assert.Equal(JoinButton.Target + 1, target);
                Assert.Equal(IslandView.App, island.Click(target).View);
                island.Open();
                Assert.Contains("85550001111", opened[^1]);
                Assert.Equal(IslandView.None, island.Click(0).View);
            }
            finally
            {
                Clock.Now = real;
                CalendarIsland.Made = null;
                MeetingJoin.Opening = null;
            }
        });
    }

    /// <summary>
    /// Set to show a meeting only shortly before it, with none that close, the meeting face has nothing
    /// to show, by the one rule every face has (IFaceDrawing.NothingToShow, §327; FolderIsland.MeetingIdle
    /// is gone): off the bar at rest, and beside another face only it goes; arranged it is there, saying
    /// there is none, greyed — fainter than the same words set to show today's. Pictured at 150 %:
    /// meeting-soon-&lt;dark|light&gt;.png (today's, then soon's arranged).
    /// </summary>
    [UiFact]
    public void TheSoonFaceWithNoneCloseIsGoneAtRestAndGreyedArranged()
    {
        var store = Store();
        Offscreen.OnStaThread(gfx =>
        {
            var real = Clock.Now;
            CalendarIsland.Made = store;
            var made = new List<FolderIsland>();
            FolderIsland Made(BarItem folder, bool arranged)
            {
                var island = new FolderIsland(Config.IslandKey("layout:default", folder.Id), folder, gfx, new Config()) { Arranged = arranged };
                made.Add(island);
                return island;
            }
            try
            {
                // None left today: today's says so, soon's has nothing to show.
                Clock.Now = () => Day.AddHours(20);
                foreach (var dark in new[] { true, false })
                {
                    var style = new IslandStyle(1.5f, false, 72, 36, LookFor(dark));
                    var today = Made(Folder(), arranged: false);
                    Assert.Equal(0, Made(Folder(show: MeetingFace.Soon), arranged: false).Measure(style).Count);
                    var soon = Made(Folder(show: MeetingFace.Soon), arranged: true);
                    Assert.Equal(1, soon.Measure(style).Count);
                    Assert.Equal(today.CellSize(0, style), soon.CellSize(0, style));
                    var greyed = MediaPresetTests.StandsOut(soon, style, dark);
                    var plain = MediaPresetTests.StandsOut(today, style, dark);
                    Assert.True(greyed < plain * 0.8f, $"arranged with none close, the face is not greyed: {greyed} against {plain}");
                    FaceCentringTests.Sheet(gfx, [("Today, none left", today, style), ("Soon, none close, arranged", soon, style)], $"meeting-soon-{(dark ? "dark" : "light")}.png");
                }

                // Beside a clock only the meeting face goes, and is back shortly before a meeting.
                var both = Folder(show: MeetingFace.Soon);
                both.Folder!.Faces = [both.Folder.Face, FolderFace.Of(FolderFaces.ClockId)];
                var bar = new IslandStyle(1, false, 48, 24, LookFor(true));
                Clock.Now = () => Day.AddHours(8);
                var beside = Made(both, arranged: false);
                Assert.Equal(1, beside.Measure(bar).Count);
                Assert.Equal([FolderFaces.Clock], beside.FacesOnBar.Select(face => face.Info));
                Clock.Now = () => Day.AddHours(9).AddMinutes(-5);
                Assert.Equal([FolderFaces.Meeting, FolderFaces.Clock], beside.FacesOnBar.Select(face => face.Info));
            }
            finally
            {
                foreach (var island in made)
                {
                    island.Dispose();
                }
                Clock.Now = real;
                CalendarIsland.Made = null;
            }
        });
    }

    /// <summary>Each shot's folder made and drawn at its minute, on the bar's ground, under its caption.</summary>
    private static unsafe void Faces(Gfx gfx, IReadOnlyList<Shot> shots, Look look, string path)
    {
        const float pad = 14;
        const float label = 18;
        var made = new List<(Shot Shot, FolderIsland Island, IslandStyle Style, float W, float H)>();
        try
        {
            foreach (var shot in shots)
            {
                Clock.Now = () => shot.At;
                var island = new FolderIsland(Config.IslandKey("layout:default", shot.Folder.Id), shot.Folder, gfx, new Config());
                var thickness = 48 * shot.Scale;
                var style = new IslandStyle(shot.Scale, shot.Vertical, thickness, MathF.Round(thickness / 2), look);
                var (w, h) = island.CellSize(0, style);
                made.Add((shot, island, style, w, h));
            }
            var width = pad + made.Max(item => MathF.Max(item.W, 240)) + pad;
            var height = pad + made.Sum(item => label + item.H + pad);
            Offscreen.Bitmap(width, height, dc =>
            {
                // Windows 11's taskbar: #202020 on dark, #EEEEEE on light (its Mica's tint without the wallpaper).
                var ground = look.Dark ? new DXGI_RGBA(0x20 / 255f, 0x20 / 255f, 0x20 / 255f, 1) : new DXGI_RGBA(0xEE / 255f, 0xEE / 255f, 0xEE / 255f, 1);
                dc->Clear(&ground);
                var caption = look.Dark ? new DXGI_RGBA(1, 1, 1, 0.6f) : new DXGI_RGBA(0, 0, 0, 0.6f);
                var y = pad;
                foreach (var (shot, island, style, w, h) in made)
                {
                    Clock.Now = () => shot.At;
                    PopupPaint.Text(dc, gfx, $"{shot.Caption} ({shot.At:HH:mm})", 12, Weight.Regular, caption, pad, y);
                    var top = y + label;
                    // The cell's own extent, faintly, so the face's room is seen.
                    PopupPaint.Stroke(dc, PopupPaint.Rect(pad - 1, top - 1, w + 2, h + 2), 2, 1, look.Dark ? new DXGI_RGBA(1, 1, 1, 0.12f) : new DXGI_RGBA(0, 0, 0, 0.12f));
                    var drawn = dc;
                    var cellStyle = style;
                    Offscreen.Shifted(dc, pad, top, () => island.PaintCell(drawn, 0, cellStyle, w, h));
                    y += label + h + pad;
                }
            }, bitmap => Offscreen.Save(bitmap, path));
        }
        finally
        {
            foreach (var item in made)
            {
                item.Island.Dispose();
            }
        }
    }
}
