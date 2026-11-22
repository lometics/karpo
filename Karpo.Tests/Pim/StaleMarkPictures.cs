// The stale mark drawn (Karpo.Dock\Islands\StaleMark.cs): the mail's and the
// calendar's list lines at every height while an account could not be
// refreshed, and the mail face's envelope on the bar fresh and stale. The sheets go
// to KARPO_PNG_DIR (else %TEMP%\karpo-tests\pictures): stale-mail-lines.png,
// stale-calendar-lines.png, stale-envelope-<dark|light>.png. Runs only with
// KARPO_UI_TESTS=1, like the other picture tests.

using System.Numerics;
using Karpo.Accounts;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Pim;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;

namespace Karpo.Tests.Pim;

public sealed class StaleMarkPictures
{
    private static readonly DateTime Frozen = new(2026, 10, 9, 15, 0, 0);

    private static List<MailItem> Inbox() =>
    [
        new("1", "Sandra Gerber", "sandra@example.ch", "Lunch on Friday?", null, new DateTimeOffset(Frozen.AddMinutes(-4)), true),
        new("2", "GitHub", "noreply@github.com", "[karpo-app] CI passed on master", null, new DateTimeOffset(Frozen.AddMinutes(-52)), true),
        new("3", "Adrian Hager", "adrian@example.com", "Re: Quarterly review", null, new DateTimeOffset(Frozen.AddHours(-3)), false),
    ];

    /// <summary>
    /// A calendar store whose one account was read an hour ago, kept on disk, and does not answer
    /// now: its events shown, stale.
    /// </summary>
    private static async Task<CalendarStore> StaleCalendar()
    {
        var cache = new PimCache(Path.Combine(Isolation.Folder, "pim-" + Guid.NewGuid().ToString("N")));
        var reader = new FakeReader(address: "home@example.com");
        var utc = Frozen.ToUniversalTime();
        reader.Calendars.Add(new CalendarRead(
            new RemoteCalendar("cal", "Home", "#D13438"),
            [
                new Occurrence("Design review", "Room 4", false, false, utc.AddHours(1), utc.AddHours(2)),
                new Occurrence("Dentist", null, false, false, utc.AddDays(1).AddHours(-6), utc.AddDays(1).AddHours(-5)),
                new Occurrence("Team lunch", null, false, false, utc.AddDays(2).AddHours(-3), utc.AddDays(2).AddHours(-2)),
            ],
            []));
        var earlier = new CalendarStore(new AccountCalendars(new AccountSet([reader]), () => utc.AddHours(-1), cache: cache));
        earlier.Open();
        await earlier.Idle();
        reader.Failure = new HttpRequestException("offline");
        var store = new CalendarStore(new AccountCalendars(new AccountSet([reader]), () => utc, cache: cache));
        store.Open();
        await store.Idle();
        Assert.True(store.Freshness.Stale);
        return store;
    }

    [UiFact]
    public void TheLinesAndTheEnvelopeCarryTheMark()
    {
        var calendar = StaleCalendar().GetAwaiter().GetResult();
        Offscreen.OnStaThread(gfx =>
        {
            var real = Clock.Now;
            Clock.Now = () => Frozen;
            CalendarIsland.Made = calendar;
            try
            {
                var look = ListFormPictures.LookFor(dark: true);
                using var mail = new MailStore(new AccountSet([]));
                mail.Answered(Inbox(), unread: 2, new Freshness(Frozen.AddHours(-1).ToUniversalTime(), true));
                var mailLine = ListKind.Mail.New();
                mailLine.Id = "mail";
                using var mailIsland = new MailIsland(Config.IslandKey("layout:default", "mail"), mailLine, gfx, new Config(), mail, () => Frozen);
                ListLinesTests.Sheet(gfx, mailIsland, ListKind.Mail, 1.5f, look, Path.Combine(Offscreen.Pictures, "stale-mail-lines.png"));

                var calendarLine = ListKind.Calendar.New();
                calendarLine.Id = "calendar";
                using var calendarIsland = (CellIsland)Karpo.Dock.Islands.CreateLine(Config.IslandKey("layout:default", "calendar"), calendarLine, ListKind.Calendar, gfx, new Config())!;
                ListLinesTests.Sheet(gfx, calendarIsland, ListKind.Calendar, 1.5f, look, Path.Combine(Offscreen.Pictures, "stale-calendar-lines.png"));

                // The tooltip stands over the mark, and nowhere else on the line.
                var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
                var box = PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(1.5f), LineRows.Px(4, style));

                // The calendar preset's lines and the views carry it too (docs\widgets.md §80): the
                // day's events beside their heading, the month and the views before their arrows.
                foreach (var kind in new[] { ListKind.Events, ListKind.Month, ListKind.DayView, ListKind.WeekView, ListKind.YearView })
                {
                    var line = kind.New();
                    line.Id = kind.ToString();
                    using var island = (CellIsland)Karpo.Dock.Islands.CreateLine(Config.IslandKey("layout:default", line.Id), line, kind, gfx, new Config())!;
                    ListLinesTests.Sheet(gfx, island, kind, 1.5f, look, Path.Combine(Offscreen.Pictures, $"stale-{kind.ToString().ToLowerInvariant()}-lines.png"));
                    var tall = PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(1.5f), LineRows.Px(8, style));
                    var pieces = ((ILinePieces)island).LinePieces(tall, style);
                    var at = pieces.Single(piece => piece.Name == StaleMark.Piece).Box;
                    // Clear of everything else on its row: the title and the arrows.
                    foreach (var other in pieces.Where(piece => piece.Name is "title" or "day" or "previous" or "next"))
                    {
                        Assert.True(at.right <= other.Box.left || at.left >= other.Box.right || at.bottom <= other.Box.top || at.top >= other.Box.bottom, $"{kind}: the mark is on {other.Name}");
                    }
                    var middle = new Vector2((at.left + at.right) / 2, (at.top + at.bottom) / 2);
                    Assert.Equal(calendar.Freshness.Tip(Frozen), island.TipOn(middle, tall, style, WidgetSurface.List)?.Text);
                    Assert.Equal(calendar.Freshness.Tip(Frozen), island.StaleTip);
                }
                var mark = ((ILinePieces)mailIsland).LinePieces(box, style).Single(piece => piece.Name == StaleMark.Piece).Box;
                var centre = new Vector2((mark.left + mark.right) / 2, (mark.top + mark.bottom) / 2);
                Assert.Equal(mail.Freshness.Tip(Frozen), mailIsland.TipOn(centre, box, style, WidgetSurface.List)?.Text);
                Assert.Null(mailIsland.TipOn(new Vector2(box.left + 20, box.bottom - 10), box, style, WidgetSurface.List));
                Assert.Equal(mail.Freshness.Tip(Frozen), mailIsland.StaleTip);

                foreach (var dark in new[] { true, false })
                {
                    Envelopes(gfx, mail, dark, Path.Combine(Offscreen.Pictures, $"stale-envelope-{(dark ? "dark" : "light")}.png"));
                }
            }
            finally
            {
                Clock.Now = real;
                CalendarIsland.Made = null;
            }
        });
    }

    /// <summary>The mail face's envelope at two bar heights: fresh with a count, stale with a count, stale with none.</summary>
    private static unsafe void Envelopes(Gfx gfx, MailStore store, bool dark, string path)
    {
        var look = ListFormPictures.LookFor(dark);
        var bar = Presets.Folder(Presets.Find(Presets.MailId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
        MailIsland.Stand = store;
        FolderIsland made;
        try
        {
            made = new FolderIsland(Config.IslandKey("layout:default", bar.Id), bar, gfx, new Config());
        }
        finally
        {
            MailIsland.Stand = null;
        }
        using var island = made;
        var states = new (int Unread, bool Stale)[] { (2, false), (2, true), (0, true) };
        var thicknesses = new[] { 48f, 72f };
        const float gap = 12;
        var width = thicknesses.Max() * states.Length + (gap * (states.Length + 1));
        var height = thicknesses.Sum() + (gap * (thicknesses.Length + 1));
        Offscreen.Bitmap(width, height, dc =>
        {
            var background = dark ? new DXGI_RGBA(0.13f, 0.13f, 0.13f, 1) : new DXGI_RGBA(0.93f, 0.93f, 0.93f, 1);
            dc->Clear(&background);
            var y = gap;
            foreach (var thickness in thicknesses)
            {
                var style = new IslandStyle(1f, false, thickness, MathF.Round(thickness / 2 / 2) * 2, look);
                var x = gap;
                foreach (var (unread, stale) in states)
                {
                    store.Answered(Inbox(), unread, stale ? new Freshness(Frozen.ToUniversalTime(), true) : default);
                    var (cell, across) = island.CellSize(0, style);
                    var drawn = dc;
                    Offscreen.Shifted(dc, x, y, () => island.PaintCell(drawn, 0, style, cell, across));
                    x += thicknesses.Max() + gap;
                }
                y += thickness + gap;
            }
        }, bitmap => Offscreen.Save(bitmap, path));
    }
}
