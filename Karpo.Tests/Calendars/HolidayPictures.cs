// The public holidays drawn as a calendar of their own (docs\widgets.md §237): Zürich's holidays beside an
// account's calendar in the calendar preset as it opens a few days before Christmas, with Christmas picked,
// and in an agenda line and a week view. The sheets go to KARPO_PNG_DIR (else %TEMP%\karpo-tests\pictures):
// holidays-<preset|lines>-<dark|light>.png. Runs only with KARPO_UI_TESTS=1; nothing reaches the network.

using Karpo.Dock;
using Karpo.Dock.Panels;
using Karpo.Tests.Islands;
using static Karpo.Tests.Folders.ListFormPictures;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Calendars;

public sealed class HolidayPictures
{
    private static readonly DateTime Frozen = new(2026, 12, 22, 10, 20, 0, DateTimeKind.Local);

    private static readonly DateOnly Christmas = new(2026, 12, 25);

    /// <summary>
    /// A store of Zürich's holidays — or §265's several countries, <paramref name="countries"/> — and a
    /// family calendar with a few days around them.
    /// </summary>
    private static CalendarStore Calendars(params (string Country, string Region)[] countries)
    {
        var holidays = new HolidayCalendar(
            (country, year, _) => Task.FromResult(
                country == "GB" ? HolidayAnswers.Britain2026.Replace("\"2026-", $"\"{year}-", StringComparison.Ordinal)
                : year == 2027 ? HolidayAnswers.Switzerland2027 : HolidayAnswers.Switzerland2026),
            utcNow: () => Frozen.ToUniversalTime());
        var store = new CalendarStore(holidays: holidays);
        store.Add(new ListCalendar("test:family", "Family", CalendarColours.Palette[5],
        [
            new(new DateTime(2026, 12, 22, 12, 0, 0), new DateTime(2026, 12, 22, 13, 30, 0), false, "Lunch with Grace", "Zürich"),
            new(new DateTime(2026, 12, 24, 18, 0, 0), new DateTime(2026, 12, 24, 22, 0, 0), false, "Christmas Eve dinner", null),
            new(new DateTime(2026, 12, 25, 11, 0, 0), new DateTime(2026, 12, 25, 12, 0, 0), false, "Call the grandparents", null),
            new(new DateTime(2026, 12, 28), new DateTime(2027, 1, 2), true, "Skiing in Davos", null),
        ]));
        countries = countries.Length > 0 ? countries : [("CH", "CH-ZH")];
        holidays.Configure(new HolidayOptions { Calendars = [.. countries.Select(each => new HolidayCountry { Country = each.Country, Region = each.Region })] });
        store.Open();
        holidays.Idle().GetAwaiter().GetResult();
        return store;
    }

    private static void AtChristmas(Action<Gfx> body, params (string Country, string Region)[] countries) =>
        OnStaThread(gfx =>
        {
            Clock.Now = () => Frozen;
            CalendarIsland.Made = Calendars(countries);
            CalendarApp.Stand = () => "outlookcal:";
            Nls.Formats = new LocalizationOptions { FirstDayOfWeek = DayOfWeek.Monday };
            try
            {
                body(gfx);
            }
            finally
            {
                Nls.Formats = null;
                CalendarApp.Stand = null;
                CalendarIsland.Made = null;
                Clock.Now = Clock.Real;
            }
        });

    private static FolderIsland Preset(Gfx gfx)
    {
        var holder = Presets.Folder(Presets.Find(Presets.CalendarId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
        var folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
        folder.Opened(gfx);
        return folder;
    }

    private static FolderIsland Lines(Gfx gfx, FolderListWidth width, params ListKind[] kinds)
    {
        var holder = BarKind.FolderList.New();
        holder.Folder!.Widths = [width];
        foreach (var kind in kinds)
        {
            Assert.NotNull(Karpo.Core.Folders.Add(BarKind.FolderList, holder.Folder, Line(kind)));
        }
        holder.Normalize();
        var folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
        folder.Opened(gfx);
        return folder;
    }

    [UiFact]
    public void HolidaysInTheCalendarPreset()
    {
        foreach (var dark in new[] { true, false })
        {
            AtChristmas(gfx =>
            {
                var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                try
                {
                    foreach (var (caption, text, pick) in new[] { ("As it opens", 1f, false), ("Christmas picked", 1f, true), ("Christmas picked", 2.25f, true) })
                    {
                        var look = LookFor(dark, text);
                        var folder = Preset(gfx);
                        if (pick)
                        {
                            folder.Tiles.Select(tile => tile.Island).OfType<MonthTile>().Single().Pick(Christmas);
                        }
                        var (_, plan) = Plan(folder, look, 1);
                        Assert.Equal(folder.Tiles.Count, plan.Cards.Count);
                        panels.Add(($"{caption}: {text * 100:0} % text", look, folder, plan));
                    }
                    Sheet(gfx, $"holidays-preset-{(dark ? "dark" : "light")}.png", 1, panels);
                }
                finally
                {
                    Close(panels);
                }
            });
        }
    }

    [UiFact]
    public void HolidaysInTheLines()
    {
        foreach (var dark in new[] { true, false })
        {
            AtChristmas(gfx =>
            {
                var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                try
                {
                    foreach (var (caption, width, kinds) in new[]
                    {
                        ("Agenda", FolderListWidth.Medium, new[] { ListKind.Calendar }),
                        ("Week view", FolderListWidth.Wide, new[] { ListKind.DateHeader, ListKind.WeekView }),
                    })
                    {
                        var look = LookFor(dark);
                        var folder = Lines(gfx, width, kinds);
                        var (_, plan) = Plan(folder, look, 1);
                        panels.Add(($"{caption}: {FolderListWidths.Dip(width)} DIP", look, folder, plan));
                    }
                    Sheet(gfx, $"holidays-lines-{(dark ? "dark" : "light")}.png", 1, panels);
                }
                finally
                {
                    Close(panels);
                }
            });
        }
    }

    /// <summary>
    /// Every style of the calendar draws them (§240): the agenda and the day's events list them, the day
    /// and the week as all-day chips, the month and the year as a dot in their red, whose day says which
    /// holiday it is. Dark at 150 %: holidays-styles-dark.png.
    /// </summary>
    [UiFact]
    public void HolidaysInEveryStyle() =>
        AtChristmas(gfx =>
        {
            const float scale = 1.5f;
            var look = LookFor(dark: true);
            var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
            var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
            try
            {
                foreach (var (caption, width, kinds) in new[]
                {
                    ("Agenda", FolderListWidth.Medium, new[] { ListKind.Calendar }),
                    ("Month, Christmas picked, and its events", FolderListWidth.Medium, new[] { ListKind.Month, ListKind.Events }),
                    ("Day, stepped to Christmas", FolderListWidth.Medium, new[] { ListKind.DayView }),
                    ("Week", FolderListWidth.Wide, new[] { ListKind.WeekView }),
                    ("Year", FolderListWidth.Wide, new[] { ListKind.YearView }),
                })
                {
                    var folder = Lines(gfx, width, kinds);
                    var tiles = folder.Tiles.Select(tile => tile.Island).ToList();
                    tiles.OfType<MonthTile>().SingleOrDefault()?.Pick(Christmas);
                    if (tiles.OfType<CalendarViewTile>().SingleOrDefault(view => view.View == CalendarView.Day) is { } day)
                    {
                        while (day.Picked < Christmas)
                        {
                            day.Click(CalendarViewTile.NextTarget);
                        }
                    }
                    var (_, plan) = Plan(folder, look, scale);
                    panels.Add(($"{caption}: {FolderListWidths.Dip(width)} DIP", look, folder, plan));

                    // The month's Christmas and the week's heading of it say what holiday it is (the year's as the week's,
                    // a day of either the one target); a day that is none says nothing.
                    for (var at = 0; at < tiles.Count; at++)
                    {
                        if (tiles[at] is not (MonthTile or CalendarViewTile { View: CalendarView.Week }))
                        {
                            continue;
                        }
                        var cell = (CellIsland)tiles[at];
                        var card = plan.Cards[at];
                        var box = PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top);
                        var pieces = ((ILinePieces)cell).LinePieces(box, style);
                        var christmas = pieces.Single(piece => piece.Name == $"day {Christmas:yyyy-MM-dd}");
                        var plain = pieces.Single(piece => piece.Name == "day 2026-12-23");
                        Assert.Equal("Christmas Day", cell.CardTip(Karpo.Tests.Folders.ListFormCases.Centre(christmas.Box), box, style, WidgetSurface.List)?.Text);
                        Assert.Null(cell.CardTip(Karpo.Tests.Folders.ListFormCases.Centre(plain.Box), box, style, WidgetSurface.List));
                    }
                }
                Sheet(gfx, "holidays-styles-dark.png", scale, panels);
            }
            finally
            {
                Close(panels);
            }
        });

    /// <summary>
    /// §265: Zürich's holidays and the United Kingdom's as one calendar: the agenda says under each whose
    /// it is, after when it is, Christmas once for both, St. Stephen's Day Zürich's on the 26th and the United Kingdom's on the
    /// 28th; the month's Christmas says both in its tooltip. Dark and light at 150 %:
    /// holidays-several-&lt;dark|light&gt;.png.
    /// </summary>
    [UiFact]
    public void HolidaysOfSeveralCountries()
    {
        foreach (var dark in new[] { true, false })
        {
            AtChristmas(
                gfx =>
                {
                    const float scale = 1.5f;
                    var look = LookFor(dark);
                    var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    try
                    {
                        foreach (var (caption, width, kinds) in new[]
                        {
                            ("Agenda", FolderListWidth.Medium, new[] { ListKind.Calendar }),
                            ("Month, Christmas picked, and its events", FolderListWidth.Medium, new[] { ListKind.Month, ListKind.Events }),
                        })
                        {
                            var folder = Lines(gfx, width, kinds);
                            var tiles = folder.Tiles.Select(tile => tile.Island).ToList();
                            tiles.OfType<MonthTile>().SingleOrDefault()?.Pick(Christmas);
                            var (_, plan) = Plan(folder, look, scale);
                            panels.Add(($"{caption}: {FolderListWidths.Dip(width)} DIP", look, folder, plan));
                            for (var at = 0; at < tiles.Count; at++)
                            {
                                if (tiles[at] is not MonthTile)
                                {
                                    continue;
                                }
                                var cell = (CellIsland)tiles[at];
                                var card = plan.Cards[at];
                                var box = PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top);
                                var christmas = ((ILinePieces)cell).LinePieces(box, style).Single(piece => piece.Name == $"day {Christmas:yyyy-MM-dd}");
                                Assert.Equal(
                                    $"Christmas Day · {HolidayPlaces.Name("CH")}, {HolidayPlaces.Name("GB")}",
                                    cell.CardTip(Karpo.Tests.Folders.ListFormCases.Centre(christmas.Box), box, style, WidgetSurface.List)?.Text);
                            }
                        }
                        Sheet(gfx, $"holidays-several-{(dark ? "dark" : "light")}.png", scale, panels);
                    }
                    finally
                    {
                        Close(panels);
                    }
                },
                ("CH", "CH-ZH"),
                ("GB", ""));
        }
    }

    private static void Close(List<(string, Look, FolderIsland Folder, FolderPlan)> panels)
    {
        foreach (var (_, _, folder, _) in panels)
        {
            folder.Closed();
            folder.Dispose();
        }
    }
}
