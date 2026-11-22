// The calendar and the weather as ready-made folders, opened (Karpo.Core\Presets.cs, docs\widgets.md
// §80): what a click on the calendar entry and on the weather entry gave, made of widgets.
//
// The calendar: its date's header, month, day's events and footer agree on the day through the
// folder's Day channel with nothing set up — a click on a day in the month and the events under it
// are that day's, the header's click goes back to today, and closing forgets it; the day, week and
// year views publish and follow the day as the month does, step by a day, a week and a year, scroll
// their hours with the wheel, start their weeks on the region's day, and name a day by its day
// whatever the layout. The weather: its three lines read the one forecast, set up once on the
// conditions' line.
//
// Pictured, dark and light, at 100 % (the calendar, as its flyout was pictured) and 150 % (the
// weather, as its flyout was), and 225 % text: calendar-preset-<dark|light>.png,
// calendar-views-<dark|light>.png, weather-preset-<dark|light>.png in KARPO_PNG_DIR — the flyouts'
// own pictures, made before they went, are their references. Needs Direct2D: KARPO_UI_TESTS=1.
// Nothing reaches this PC's calendars or the network.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.Panels;
using Karpo.Dock.Weather;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.ListFormPictures;
using static Karpo.Tests.Islands.Offscreen;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed class CalendarWeatherFolderTests
{
    /// <summary>The day the calendar's flyout was pictured on: a Tuesday afternoon.</summary>
    private static readonly DateTime Frozen = new(2026, 9, 22, 14, 40, 7, DateTimeKind.Local);

    private static readonly DateOnly Today = DateOnly.FromDateTime(Frozen);
    private static readonly DateOnly Thursday = new(2026, 9, 24);

    private static readonly Color Family = CalendarColours.Palette[0];
    private static readonly Color Club = CalendarColours.Palette[2];

    private static CalendarEvent Timed(string title, int day, double from, double to, Color colour = default, string? place = null) =>
        new(new DateTime(2026, 9, day).AddHours(from), new DateTime(2026, 9, day).AddHours(to), false, title, place, Colour: colour);

    /// <summary>The week the calendar's flyout was pictured with: work (the accent), family and a club, overlaps, all-day and multi-day events.</summary>
    internal static readonly CalendarEvent[] Sample =
    [
        new(new DateTime(2026, 9, 22), new DateTime(2026, 9, 23), true, "Autumn equinox", null, Colour: Family),
        new(new DateTime(2026, 9, 23, 10, 0, 0), new DateTime(2026, 9, 25, 16, 0, 0), false, "Conference", "Basel"),
        new(new DateTime(2026, 9, 25), new DateTime(2026, 9, 29), true, "Holiday in Ticino", null, Colour: Family),
        Timed("Gym", 21, 7, 8, Club),
        Timed("Standup", 22, 9.5, 9.75, place: "Teams"),
        Timed("Design review", 22, 10, 11.5, place: "Room 4"),
        Timed("1:1 with Ada", 22, 10.5, 11),
        Timed("Lunch with Grace", 22, 12, 13.5, Family, "Zürich"),
        Timed("Focus time", 22, 14, 16),
        Timed("Call with the bank", 22, 15, 15.5, Family),
        Timed("Dentist", 24, 8, 9, Family),
        Timed("Concert", 26, 19, 22.5, Club, "Tonhalle"),
        new(new DateTime(2026, 9, 26, 22, 0, 0), new DateTime(2026, 9, 27, 6, 0, 0), false, "Night train", null, Colour: Family),
        Timed("Planning", 29, 9, 10),
        Timed("Birthday dinner", 3, 19, 21, Family),
        new(new DateTime(2026, 12, 24), new DateTime(2026, 12, 27), true, "Christmas", null, Colour: Family),
        new(new DateTime(2026, 7, 14, 18, 0, 0), new DateTime(2026, 7, 14, 20, 0, 0), false, "Summer party", null, Colour: Club),
    ];

    /// <summary>A store of <paramref name="events"/>, a calendar for each colour among them (work's the accent), as the flyout was pictured with.</summary>
    private static CalendarStore Calendars(IReadOnlyList<CalendarEvent> events)
    {
        var store = new CalendarStore();
        foreach (var calendar in events.GroupBy(item => item.Colour))
        {
            store.Add(new ListCalendar($"test:{calendar.Key}", calendar.Key.ToString(), calendar.Key, [.. calendar]));
        }
        return store;
    }

    /// <summary>Runs <paramref name="body"/> on a drawing thread at <see cref="Frozen"/>, the sample's calendar its store, a calendar app there to open.</summary>
    private static void AtTheSampleDay(Action<Gfx> body) =>
        OnStaThread(gfx =>
        {
            Clock.Now = () => Frozen;
            CalendarIsland.Made = Calendars(Sample);
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

    /// <summary><paramref name="preset"/>'s folder, as the library adds it, opened; <paramref name="before"/> runs first.</summary>
    private static FolderIsland Opened(Gfx gfx, string preset, Action<BarItem>? set = null, Action<FolderIsland>? before = null)
    {
        var holder = Presets.Folder(Presets.Find(preset)!, new PresetMachine(HasBattery: false, FixedDrives: []));
        set?.Invoke(holder);
        var folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
        before?.Invoke(folder);
        folder.Opened(gfx);
        return folder;
    }

    /// <summary>A folder of <paramref name="lines"/> in one list <paramref name="width"/> wide, opened.</summary>
    private static FolderIsland Lines(Gfx gfx, FolderListWidth width, params ListKind[] lines)
    {
        var holder = BarKind.FolderList.New();
        holder.Folder!.Widths = [width];
        foreach (var kind in lines)
        {
            Assert.NotNull(Karpo.Core.Folders.Add(BarKind.FolderList, holder.Folder, Line(kind)));
        }
        holder.Normalize();
        var folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
        folder.Opened(gfx);
        return folder;
    }

    private static T Tile<T>(FolderIsland folder) => folder.Tiles.Select(tile => tile.Island).OfType<T>().Single();

    // --- The calendar ---------------------------------------------------------------------------------

    /// <summary>
    /// A click on Thursday in the calendar's month: the folder's day is Thursday and the day's events
    /// are Thursday's; the header's click brings today back to both; closed, the folder forgets it.
    /// </summary>
    [UiFact]
    public void TheMonthPicksTheDayTheEventsShowAndTheHeaderGoesBack() =>
        AtTheSampleDay(gfx =>
        {
            var folder = Opened(gfx, Presets.CalendarId);
            var month = Tile<MonthTile>(folder);
            var events = Tile<EventsTile>(folder);
            var header = Tile<DateHeaderTile>(folder);
            var look = LookFor(dark: true);
            var style = FolderFlyout.StyleFor(1, look, FlyoutTheme.For(look));
            Assert.Equal(Today, events.Day);
            Assert.Contains("Standup", Titles(events, style));

            month.Click(MonthTile.Target(Thursday));
            Assert.Equal(Thursday, folder.ChannelValue(Channels.Day));
            Assert.Equal(Thursday, events.Day);
            Assert.Equal(["Conference", "Dentist"], Titles(events, style).Where(title => title != CalendarText.DayName(Thursday, Today)));

            header.Click(0);
            Assert.Equal(Today, folder.ChannelValue(Channels.Day));
            Assert.Equal(Today, month.Picked);
            Assert.Equal(Today, events.Day);

            month.Pick(Thursday);
            folder.Closed();
            Assert.Null(folder.ChannelValue(Channels.Day));
            Assert.Equal(Today, events.Day);
            folder.Dispose();
        });

    private static List<string> Titles(EventsTile events, in TileStyle style) =>
        [.. events.LinePieces(PopupPaint.Rect(0, 0, 360, events.ListHeight(360, style)), style).Where(piece => piece.Name.StartsWith("event ", StringComparison.Ordinal)).Select(piece => piece.Text)];

    /// <summary>
    /// A view is its word's even standing in no view yet (§205): a config copied by the dock for an
    /// edit had none, and a day or year view read as no line was drawn as a week view.
    /// </summary>
    [UiFact]
    public void AViewIsItsWordsWithoutALine() =>
        AtTheSampleDay(gfx =>
        {
            foreach (var (word, view) in new[] { ("dayView", CalendarView.Day), ("weekView", CalendarView.Week), ("yearView", CalendarView.Year) })
            {
                var island = new CellItem { Kind = word };
                Assert.Null(island.List);
                Assert.Equal(view, new CalendarViewTile($"{Owner}/f/{word}", island, gfx, new Config()).View);
            }
        });

    /// <summary>
    /// The views follow the folder's day and publish it: a day picked in the month moves the week and
    /// the day to it; a step of the week moves the month's day by a week; a click on a day of the year
    /// is the folder's day. A day is named by its day, so a layout in between changes nothing.
    /// </summary>
    [UiFact]
    public void TheViewsFollowAndPublishTheDay() =>
        AtTheSampleDay(gfx =>
        {
            var folder = Lines(gfx, FolderListWidth.Wide, ListKind.Month, ListKind.DayView, ListKind.WeekView, ListKind.YearView, ListKind.Events);
            var month = Tile<MonthTile>(folder);
            var views = folder.Tiles.Select(tile => tile.Island).OfType<CalendarViewTile>().ToList();
            var (day, week, year) = (views.Single(view => view.View == CalendarView.Day), views.Single(view => view.View == CalendarView.Week), views.Single(view => view.View == CalendarView.Year));
            Assert.All(views, view => Assert.Equal(Today, view.Picked));
            Assert.Equal(new DateOnly(2026, 9, 21), week.Shown);

            month.Pick(new DateOnly(2026, 10, 1));
            Assert.Equal(new DateOnly(2026, 10, 1), day.Picked);
            Assert.Equal(new DateOnly(2026, 9, 28), week.Shown);

            week.Click(CalendarViewTile.NextTarget);
            Assert.Equal(new DateOnly(2026, 10, 8), folder.ChannelValue(Channels.Day));
            Assert.Equal(new DateOnly(2026, 10, 8), month.Picked);
            Assert.Equal(new DateOnly(2026, 10, 5), week.Shown);

            day.Click(CalendarViewTile.PreviousTarget);
            Assert.Equal(new DateOnly(2026, 10, 7), folder.ChannelValue(Channels.Day));

            // A day of the year, by its day: whatever is laid out, the click is that day.
            year.Click(CalendarViewTile.DayTarget + new DateOnly(2026, 12, 24).DayNumber);
            Assert.Equal(new DateOnly(2026, 12, 24), folder.ChannelValue(Channels.Day));
            Assert.Equal(new DateOnly(2026, 12, 24), Tile<EventsTile>(folder).Day);
            // The year steps on and its day stays the folder's.
            year.Click(CalendarViewTile.NextTarget);
            Assert.Equal(2027, year.Shown.Year);
            Assert.Equal(new DateOnly(2026, 12, 24), folder.ChannelValue(Channels.Day));

            // Every style always follows (§240): a step of the day is the month's day, the week's, the year's and the events'.
            day.Click(CalendarViewTile.NextTarget);
            var christmas = new DateOnly(2026, 12, 25);
            Assert.Equal(christmas, folder.ChannelValue(Channels.Day));
            Assert.All(views, view => Assert.Equal(christmas, view.Picked));
            Assert.Equal(christmas, month.Picked);
            Assert.Equal(christmas, Tile<EventsTile>(folder).Day);

            folder.Closed();
            Assert.All(views, view => Assert.Equal(Today, view.Picked));
            folder.Dispose();
        });

    /// <summary>
    /// The day and week views open an hour before now, their whole day scrolling with the wheel and
    /// stopping at either end, where the wheel is the folder's again; a target is found where it is
    /// drawn, scrolled and slid.
    /// </summary>
    [UiFact]
    public void TheHoursScrollAndTheTargetsAreWhereTheyAreDrawn() =>
        AtTheSampleDay(gfx =>
        {
            var folder = Lines(gfx, FolderListWidth.Wide, ListKind.WeekView);
            var week = Tile<CalendarViewTile>(folder);
            var look = LookFor(dark: true);
            var style = FolderFlyout.StyleFor(1, look, FlyoutTheme.For(look));
            var height = week.ListHeight(640, style);
            var box = PopupPaint.Rect(0, 0, 640, height);
            var plan = week.Build(640, height, style);
            Assert.True(plan.Scrolls, "the whole day scrolls in the window");
            Assert.True(plan.FirstScroll > 0, "opened on the hour before now, not at midnight");

            // The heading of Thursday names Thursday; the day's column under it too.
            var heading = plan.Targets.Single(target => target.Kind == TargetKind.Day && target.Slides && !target.Scrolls && target.Day == Thursday);
            var grid = plan.Grid!;
            var at = new Vector2((heading.Box.left + heading.Box.right) / 2 + grid.Left - grid.RestSlide, (heading.Box.top + heading.Box.bottom) / 2 + grid.Head.top);
            Assert.Equal(CalendarViewTile.DayTarget + Thursday.DayNumber, week.CardTarget(at, box, style, WidgetSurface.List));
            var column = new Vector2(at.X, plan.ViewportTop + (plan.Viewport.Y / 2));
            Assert.Equal(CalendarViewTile.DayTarget + Thursday.DayNumber, week.CardTarget(column, box, style, WidgetSurface.List));
            Assert.Equal(CalendarViewTile.NextTarget, week.CardTarget(new Vector2((plan.Next.left + plan.Next.right) / 2, (plan.Next.top + plan.Next.bottom) / 2), box, style, WidgetSurface.List));

            // The wheel: down to the end, then it is the folder's.
            var turns = 0;
            while (week.CardWheel(column, box, style, WidgetSurface.List, -1))
            {
                Assert.True(++turns < 100, "the hours never stopped scrolling");
            }
            Assert.True(turns > 0);
            Assert.False(week.CardWheel(column, box, style, WidgetSurface.List, -1));
            Assert.True(week.CardWheel(column, box, style, WidgetSurface.List, 1));
            folder.Closed();
            folder.Dispose();
        });

    /// <summary>The week starts on the region's day: Monday, Sunday and Saturday, and its title and number follow.</summary>
    [UiFact]
    public void TheWeekStartsOnTheRegionsDay()
    {
        foreach (var first in new[] { DayOfWeek.Monday, DayOfWeek.Sunday, DayOfWeek.Saturday })
        {
            AtTheSampleDay(gfx =>
            {
                Nls.Formats = new LocalizationOptions { FirstDayOfWeek = first };
                var folder = Lines(gfx, FolderListWidth.Wide, ListKind.WeekView, ListKind.YearView);
                var week = (CalendarViewTile)folder.Tiles[0].Island;
                Assert.Equal(first, week.Shown.DayOfWeek);
                Assert.True(week.Shown <= Today && Today.DayNumber < week.Shown.DayNumber + 7);
                var look = LookFor(dark: true);
                var style = FolderFlyout.StyleFor(1, look, FlyoutTheme.For(look));
                var plan = week.Build(640, 0, style);
                Assert.Equal(first, plan.Grid!.Headings[plan.Grid.Shown].Day.DayOfWeek);
                var year = (CalendarViewTile)folder.Tiles[1].Island;
                var months = year.Build(640, 0, style).Year!;
                Assert.Equal(first, months.Months[0].First.DayOfWeek);
                folder.Closed();
                folder.Dispose();
            });
        }
    }

    /// <summary>
    /// The calendar preset as it opens, every line whole, at 100 % and 225 % text, and with Thursday
    /// picked: calendar-preset-<dark|light>.png, to stand beside the month flyout's picture.
    /// </summary>
    [UiFact]
    public void CalendarPresetPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            AtTheSampleDay(gfx =>
            {
                var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                try
                {
                    foreach (var (caption, text, pick) in new[] { ("As it opens", 1f, false), ("Thursday picked", 1f, true), ("As it opens", 2.25f, false) })
                    {
                        var look = LookFor(dark, text);
                        var folder = Opened(gfx, Presets.CalendarId);
                        if (pick)
                        {
                            Tile<MonthTile>(folder).Pick(Thursday);
                        }
                        var (_, plan) = Plan(folder, look, 1);
                        Assert.Equal(folder.Tiles.Count, plan.Cards.Count);
                        panels.Add(($"{caption}: {text * 100:0} % text", look, folder, plan));
                    }
                    Sheet(gfx, $"calendar-preset-{(dark ? "dark" : "light")}.png", 1, panels);
                }
                finally
                {
                    Close(panels);
                }
            });
        }
    }

    /// <summary>
    /// The day, week and year views as lines under the date's header, as their flyouts showed them:
    /// the week and the year 640 DIP wide, as the flyout was for them, the day and a year at the
    /// folder's own width: calendar-views-<dark|light>.png.
    /// </summary>
    [UiFact]
    public void CalendarViewPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            AtTheSampleDay(gfx =>
            {
                var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                try
                {
                    foreach (var (caption, width, kind, text) in new[]
                    {
                        ("Week view", FolderListWidth.Wide, ListKind.WeekView, 1f),
                        ("Day view", FolderListWidth.Medium, ListKind.DayView, 1f),
                        ("Year view", FolderListWidth.Wide, ListKind.YearView, 1f),
                        ("Year view", FolderListWidth.Medium, ListKind.YearView, 1f),
                        ("Week view", FolderListWidth.Wide, ListKind.WeekView, 2.25f),
                    })
                    {
                        var look = LookFor(dark, text);
                        var folder = Lines(gfx, width, ListKind.DateHeader, ListKind.Divider, kind);
                        var (_, plan) = Plan(folder, look, 1);
                        Assert.Equal(folder.Tiles.Count, plan.Cards.Count);
                        panels.Add(($"{caption}: {FolderListWidths.Dip(width)} DIP, {text * 100:0} % text", look, folder, plan));
                    }
                    Sheet(gfx, $"calendar-views-{(dark ? "dark" : "light")}.png", 1, panels);
                }
                finally
                {
                    Close(panels);
                }
            });
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

    // --- The weather -----------------------------------------------------------------------------------

    /// <summary>
    /// Each of the weather preset's lines has its own place, and none follows another (2026-10-10):
    /// the conditions set to one place and the week to another show each their own, the hours with
    /// none shows its "Set up in Settings", and the face, with no place of its own, has none either.
    /// </summary>
    [UiFact]
    public void EachWeatherLineHasItsOwnPlace() =>
        SavedWeather.AtAFixedTime(() => OnStaThread(gfx =>
        {
            var folder = Opened(gfx, Presets.WeatherId,
                set: holder =>
                {
                    holder.Folder!.Cells.Single(cell => cell.Island.List == ListKind.WeatherNow).Island.Weather = SavedWeather.Options();
                    holder.Folder.Cells.Single(cell => cell.Island.List == ListKind.WeatherDays).Island.Weather = new WeatherOptions { Place = "Chur", Latitude = 46.85, Longitude = 9.53 };
                    // The hours and the face without a place: not the device's location they start with.
                    holder.Folder.Cells.Single(cell => cell.Island.List == ListKind.WeatherHours).Island.Weather = new WeatherOptions();
                    holder.Folder.Face.Weather = new WeatherOptions();
                },
                before: opened =>
                {
                    foreach (var line in opened.Tiles.Select(tile => tile.Island).OfType<WeatherLineTile>())
                    {
                        line.Source = SavedWeather.Source();
                    }
                });
            var lines = folder.Tiles.Select(tile => tile.Island).OfType<WeatherLineTile>().ToList();
            Assert.Equal(3, lines.Count);
            var now = lines.OfType<WeatherPartTile>().Single();
            var hours = lines.OfType<WeatherHoursTile>().Single();
            var days = lines.OfType<WeatherDaysTile>().Single();
            Assert.Equal(SavedWeather.Options().Place, now.Options.Place);
            Assert.Null(now.Unset);
            Assert.False(hours.Options.IsSet);
            Assert.Equal(SetupField.Place, hours.Unset?.Link.Field);
            Assert.Equal("Chur", days.Options.Place);
            Assert.Null(days.Unset);
            Assert.Equal(FolderFaces.WeatherId, folder.Options.Face.Kind);
            Assert.Null(WeatherPlaces.OfFace(folder.Options));
            folder.Closed();
            folder.Dispose();
        }));

    /// <summary>
    /// The weather preset with its forecast, at 150 % as its flyout was pictured, and at 225 % text;
    /// and before a place is set: weather-preset-<dark|light>.png.
    /// </summary>
    [UiFact]
    public void WeatherPresetPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            SavedWeather.AtAFixedTime(() => OnStaThread(gfx =>
            {
                var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                try
                {
                    // The device's location as a new preset has it, and where Windows gives none; and a
                    // preset whose places were taken away.
                    foreach (var (caption, text, place) in new[] { ("Zürich", 1f, "place"), ("Zürich", 2.25f, "place"), ("Location off", 1f, "off"), ("No place", 1f, "none") })
                    {
                        var look = LookFor(dark, text);
                        var folder = Opened(gfx, Presets.WeatherId,
                            set: holder =>
                            {
                                foreach (var cell in holder.Folder!.Cells.Where(cell => cell.Island.Weather is not null))
                                {
                                    cell.Island.Weather = place == "place" ? SavedWeather.Options() : place == "off" ? WeatherOptions.New() : new WeatherOptions();
                                }
                            },
                            before: opened =>
                            {
                                foreach (var line in opened.Tiles.Select(tile => tile.Island).OfType<WeatherLineTile>())
                                {
                                    line.Source = place == "place" ? SavedWeather.Source()
                                        : new WeatherSource { Read = () => (null, place == "off" ? S.WidgetWeatherLocationOff : null, false), Ask = () => false };
                                }
                            });
                        var (_, plan) = Plan(folder, look, 1.5f);
                        Assert.Equal(folder.Tiles.Count, plan.Cards.Count);
                        panels.Add(($"{caption}: {text * 100:0} % text", look, folder, plan));
                    }
                    Sheet(gfx, $"weather-preset-{(dark ? "dark" : "light")}.png", 1.5f, panels);
                }
                finally
                {
                    Close(panels);
                }
            }));
        }
    }

    /// <summary>
    /// The weather preset's parts lit (docs\widgets.md §131): the pointer on the conditions, on the
    /// third day and on an afternoon hour; and the keys on the conditions, the second day and a
    /// morning hour — weather-hover-<dark|light>.png, at 150 % and at 225 % text.
    /// </summary>
    [UiFact]
    public void WeatherHoverPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            SavedWeather.AtAFixedTime(() => OnStaThread(gfx =>
            {
                var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                try
                {
                    foreach (var (caption, text, keys) in new[] { ("Pointer", 1f, false), ("Keys", 1f, true), ("Pointer", 2.25f, false) })
                    {
                        var look = LookFor(dark, text);
                        var folder = Opened(gfx, Presets.WeatherId,
                            set: holder =>
                            {
                                foreach (var cell in holder.Folder!.Cells.Where(cell => cell.Island.Weather is not null))
                                {
                                    cell.Island.Weather = SavedWeather.Options();
                                }
                            },
                            before: opened =>
                            {
                                foreach (var line in opened.Tiles.Select(tile => tile.Island).OfType<WeatherLineTile>())
                                {
                                    line.Source = SavedWeather.Source();
                                }
                            });
                        var (_, plan) = Plan(folder, look, 1.5f);
                        var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
                        for (var index = 0; index < folder.Tiles.Count; index++)
                        {
                            // The conditions are one thing to press, the folder's to light whole (§379): no parts of their own.
                            if (folder.Tiles[index].Island is not WeatherLineTile line || line is WeatherPartTile)
                            {
                                continue;
                            }
                            var card = plan.Cards[index];
                            var box = PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top);
                            if (keys)
                            {
                                var (walk, steps) = line switch
                                {
                                    WeatherDaysTile => (VK.VK_DOWN, 2),
                                    _ => (VK.VK_RIGHT, 9),
                                };
                                for (var step = 0; step < steps; step++)
                                {
                                    Assert.True(line.KeyOn(walk, WidgetSurface.List));
                                }
                                continue;
                            }
                            var wanted = line switch { WeatherDaysTile => 3, _ => 16 };
                            var at = Find(line, box, style, wanted);
                            Assert.True(at is not null, $"{line.Info.Id}: no target {wanted}");
                            line.HoverOn(at, box, style, WidgetSurface.List);
                            Assert.Equal(wanted, line.Lighted.Hot);
                        }
                        panels.Add(($"{caption}: {text * 100:0} % text", look, folder, plan));
                    }
                    Sheet(gfx, $"weather-hover-{(dark ? "dark" : "light")}.png", 1.5f, panels);
                }
                finally
                {
                    Close(panels);
                }
            }));
        }

        // Where on the line target <paramref name="wanted"/> is: a point inside it, scanned for.
        static Vector2? Find(CellIsland line, D2D_RECT_F box, in TileStyle style, int wanted)
        {
            for (var y = box.top + 1; y < box.bottom; y += 2)
            {
                for (var x = box.left + 1; x < box.right; x += 2)
                {
                    // §590: through the line's own band, a sized line's form standing in the middle of its cells.
                    if (line.TargetOn(new Vector2(x, y), box, style, WidgetSurface.List) == wanted)
                    {
                        return new Vector2(x + 4, y + 8);
                    }
                }
            }
            return null;
        }
    }
}
