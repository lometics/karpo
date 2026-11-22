// The list forms: a taskbar entry's own line in a list folder, drawn by the class that draws its
// card and fed by what its card and its flyout already read (docs\widgets.md §9, "List forms").
//
// A list line is the widget, not its name: the rule is the user's ("no list line without content"),
// and these are the lines that have content — a battery's charge and time left, Security's
// standing, the clock's face, the calendar's agenda, a stock's header and its day,
// what is playing with its own controls, a drive's capacity, a graph. Each is held here to the same
// things: it draws, inside its line; it says how tall it is (IListContent), and the list gives it
// exactly that, held inside its range; a new line's range holds its content at the largest text
// Windows offers; and its click does what a click in a folder may — nothing that opens a popup.
//
// Every line is pictured at 100 % and at 225 % text, beside each other on one sheet, drawn by the
// folder's own painters. The contents are fixed, and none of them is read from this PC, so two runs
// — on two machines, or at two times of day — draw the same sheet: a frozen clock, a sample battery
// and Security, a calendar of made-up events, a stock whose price is already kept (nothing is
// fetched and waited for), a track that is not playing anywhere, drives with no letter (so no
// shell is asked for a drive's picture) and fed graphs. Needs Direct2D, so it runs only with
// KARPO_UI_TESTS=1; the sheets go to KARPO_PNG_DIR. Nothing is shown on the person's desktop.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using Karpo.Tests.Perf;
using Karpo.Tests.Storage;
using TerraFX.Interop.DirectX;
using Xunit.Abstractions;
using static Karpo.Tests.Folders.ListFormPictures;
using static Karpo.Tests.Islands.Offscreen;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

/// <summary>What the list forms' tests share: the frozen day, the made-up answers, and the sheet with its checks.</summary>
internal static class ListFormCases
{
    public const float Scale = 1f;

    /// <summary>The day every picture is of: a Tuesday afternoon, the day the sample events are on.</summary>
    public static readonly DateTime Frozen = new(2026, 9, 22, 14, 40, 7, DateTimeKind.Local);

    /// <summary>One sheet's case: its name, the lines it shows, and what it expects of them.</summary>
    /// <param name="Lines">Makes the case's lines, after setting its fakes on the drawing thread.</param>
    /// <param name="Overflows">True where the content is meant to be more than a new line's most, and summarised.</param>
    /// <param name="Ready">Runs once the folder is made, before it is measured: waiting on a fetch.</param>
    public sealed record Case(string Name, Func<CellItem[]> Lines, bool Overflows = false, Action<Gfx, FolderIsland>? Ready = null);

    /// <summary>
    /// Draws each case's lines at both text sizes into a list folder, holds every line to the rules
    /// every list form keeps, saves the sheet, and hands each case's folder and plan to
    /// <paramref name="check"/>.
    /// </summary>
    public static void Pictured(ITestOutputHelper output, string file, IReadOnlyList<Case> cases, Action<Case, float, FolderIsland, float[], FolderPlan>? check = null)
    {
        OnStaThread(gfx =>
        {
            Clock.Now = () => Frozen;
            var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
            try
            {
                foreach (var @case in cases)
                {
                    foreach (var textScale in TextScales)
                    {
                        var look = LookFor(dark: true, textScale);
                        var folder = ListFormPictures.Folder(gfx, @case.Lines());
                        @case.Ready?.Invoke(gfx, folder);
                        var (natural, plan) = ListFormPictures.Plan(folder, look, Scale);
                        for (var index = 0; index < folder.Tiles.Count; index++)
                        {
                            var island = folder.Tiles[index].Cell.Island;
                            var given = Given(plan, index);
                            var content = natural[index];
                            var at = $"{@case.Name}, {island.Kind} at {textScale:0.##}x";
                            Assert.True(content > 0, $"{at}: the line says nothing about its height");
                            // The line is its content, held to its height: nothing added, and nothing
                            // cut but what its most cuts — or exactly its rows where it is fixed; a sized
                            // line (§432) the fewest whole rows that hold its form.
                            var height = LineHeight.For(island);
                            var sized = island.List is { } kind && kind.Sized();
                            var cell = FolderFlyout.CellPx(Scale, textScale);
                            Assert.Equal(sized ? cell * LineHeight.CellsFor(content, cell) : height.Given(content, cell), given, 1);
                            // A fixed line is its rows whatever its content: one too short for it at a
                            // large text is drawn shortened (docs\widgets.md §138).
                            if (!sized && !@case.Overflows && !height.Fixed)
                            {
                                // A new line's most holds its content at every text size.
                                Assert.True(content <= Cells(height.Cells, Scale, textScale) + 0.5f,
                                    $"{at}: {content:0} px of content is more than the {height.Cells} cells a new line may have ({Cells(height.Cells, Scale, textScale):0} px)");
                            }
                            var ink = Ink(folder, plan, index, look, Scale);
                            Assert.True(ink.Right > ink.Left, $"{at}: nothing drawn");
                            Assert.True(ink.Left >= 0 && ink.Top >= 0 && ink.Bottom <= given + 0.5f,
                                $"{at}: ink at {ink.Left:0},{ink.Top:0}..{ink.Right:0},{ink.Bottom:0} outside a line {given:0} px tall");
                            output.WriteLine($"{at}: content {content:0} px, given {given:0} px of {height}, ink {ink.Left:0},{ink.Top:0}..{ink.Right:0},{ink.Bottom:0}");
                        }
                        check?.Invoke(@case, textScale, folder, natural, plan);
                        panels.Add(($"{@case.Name}: {textScale * 100:0} % text", look, folder, plan));
                    }
                }
                Sheet(gfx, file, Scale, panels);
            }
            finally
            {
                foreach (var (_, _, folder, _) in panels)
                {
                    folder.Closed();
                    folder.Dispose();
                }
            }
        });
    }

    /// <summary>The middle of a rectangle, for a click on it.</summary>
    public static Vector2 Centre(D2D_RECT_F rect) => new((rect.left + rect.right) / 2, (rect.top + rect.bottom) / 2);

    // --- The calendar's made-up week ------------------------------------------------------------

    private static readonly Color Family = CalendarColours.Palette[0];
    private static readonly Color Club = CalendarColours.Palette[2];

    private static CalendarEvent Timed(string title, int day, double from, double to, Color colour = default, string? place = null) =>
        new(new DateTime(2026, 9, day).AddHours(from), new DateTime(2026, 9, day).AddHours(to), false, title, place, Colour: colour);

    /// <summary>A week with a few things in it: what is left of today, and the days after.</summary>
    public static readonly CalendarEvent[] Week =
    [
        new(new DateTime(2026, 9, 22), new DateTime(2026, 9, 23), true, "Autumn equinox", null, Colour: Family),
        Timed("Focus time", 22, 14, 16),
        Timed("Call with the bank", 22, 15, 15.5, Family),
        Timed("Dentist", 24, 8, 9, Family),
        Timed("Concert", 26, 19, 22.5, Club, "Tonhalle"),
    ];

    /// <summary>A week with more in it than any line holds: every weekday full of meetings.</summary>
    public static readonly CalendarEvent[] Busy =
    [
        .. Week,
        .. Enumerable.Range(23, 5).SelectMany(day => new[]
        {
            Timed("Standup", day, 9.5, 9.75),
            Timed("Design review", day, 10, 11.5),
            Timed("Lunch", day, 12, 13, Family),
            Timed("Planning", day, 15, 16),
        }),
    ];

    /// <summary>A store of its own holding <paramref name="events"/>, which reaches no account.</summary>
    public static CalendarStore Store(IReadOnlyList<CalendarEvent> events)
    {
        var store = new CalendarStore();
        store.Add(new ListCalendar("test:week", "Week", default, events));
        return store;
    }
}

public sealed unsafe class ListFormTests(ITestOutputHelper output)
{
    /// <summary>A battery that is charging, with Windows' own estimate of the time to full.</summary>
    private sealed class ChargingBattery : IBatterySource
    {
        public void Start(Changes changes)
        {
        }

        public BatteryState Read() => new(true, 45, true, true, null, TimeSpan.FromMinutes(50), false, null);

        public void SetMode(PowerMode mode)
        {
        }

        public void Dispose()
        {
        }
    }

    // --- The battery -----------------------------------------------------------------------------

    /// <summary>
    /// The battery's line: its level's glyph, the percentage and what it is doing, the charge as a
    /// bar, and the time left in the flyout's own words — on battery, charging, and on a PC with no
    /// battery, where the line says so instead of showing a charge.
    /// </summary>
    [UiFact]
    public void TheBatteryLineIsItsChargeAndItsTimeLeft() =>
        ListFormCases.Pictured(output, "list-form-battery.png",
        [
            new("On battery", () =>
            {
                BatteryIsland.Probe = () => new SystemStatus.Battery(76, false, false);
                BatteryIsland.Made = () => new SampleBattery();
                return [Line(ListKind.Battery)];
            }),
            new("Charging", () =>
            {
                BatteryIsland.Probe = () => new SystemStatus.Battery(45, true, false);
                BatteryIsland.Made = () => new ChargingBattery();
                return [Line(ListKind.Battery)];
            }),
            new("No battery", () =>
            {
                BatteryIsland.Probe = () => null;
                BatteryIsland.Made = () => new SampleBattery { Present = false };
                return [Line(ListKind.Battery)];
            }),
        ]);

    // --- Security --------------------------------------------------------------------------------

    /// <summary>
    /// Security's line: where the machine stands where the list puts a name, who is in charge under
    /// it, and the glyph on the accent while it is protected — protected, wanting attention, and
    /// not answered yet, which says so rather than guessing.
    /// </summary>
    [UiFact]
    public void TheSecurityLineIsItsStanding() =>
        ListFormCases.Pictured(output, "list-form-security.png",
        [
            new("Protected", () =>
            {
                Security.Made = () => new SampleSecurity();
                return [Line(ListKind.Security)];
            }),
            new("Needs attention", () =>
            {
                Security.Made = () => new SampleSecurity(new SecurityState(ProviderHealth.Poor, ProviderHealth.Good, "Contoso Antivirus Plus", Open: Security.WindowsSecurity));
                return [Line(ListKind.Security)];
            }),
            new("Not answered", () =>
            {
                Security.Made = () => new SampleSecurity(SecurityState.Nothing);
                return [Line(ListKind.Security)];
            }),
        ]);

    // --- The clock ---------------------------------------------------------------------------------

    /// <summary>
    /// The clock's line is the clock flyout's face at the list's width — the time large, the long
    /// date, and the zone or the label. (The date had a line of its own, the calendar's page, until
    /// the person took it out of lists on 2026-09-26.)
    /// </summary>
    [UiFact]
    public void TheClockIsItsFace() =>
        ListFormCases.Pictured(output, "list-form-clock.png",
        [
            new("This PC's time", () => [Line(ListKind.Clock)]),
            new("A zone with a label", () =>
            [
                Line(ListKind.Clock, island => island.Clock = new ClockOptions { TimeZone = "Tokyo Standard Time", Label = "Tokyo", Seconds = true }),
            ]),
        ]);

    // --- The calendar ----------------------------------------------------------------------------

    /// <summary>
    /// The calendar's line is today's page and the agenda beside it: every event coming in the days
    /// the widget looks ahead, each in its calendar's colour. A week with more in it than the line
    /// may hold fills the line and says how many more there are, in the one wording every component
    /// uses; a week with nothing says so.
    /// </summary>
    [UiFact]
    public void TheCalendarLineIsAnAgenda() =>
        ListFormCases.Pictured(output, "list-form-calendar.png",
        [
            // A list that grows: its most is the list's own most, and at the largest text even a
            // few events are more than that, so the line fills and summarises there too.
            new("A few events", () =>
            {
                CalendarIsland.Made = ListFormCases.Store(ListFormCases.Week);
                return [Line(ListKind.Calendar)];
            }, Overflows: true),
            new("A busy week", () =>
            {
                CalendarIsland.Made = ListFormCases.Store(ListFormCases.Busy);
                return [Line(ListKind.Calendar)];
            }, Overflows: true),
            new("Nothing coming", () =>
            {
                CalendarIsland.Made = ListFormCases.Store([]);
                return [Line(ListKind.Calendar)];
            }),
        ],
        (@case, textScale, folder, natural, plan) =>
        {
            if (@case.Name != "A busy week")
            {
                return;
            }
            // More than a line holds: the most is what it is given, and the rest is summarised.
            var most = LineHeight.For(folder.Tiles[0].Cell.Island).Cells;
            Assert.True(natural[0] > Cells(most, ListFormCases.Scale, textScale), $"a busy week measured only {natural[0]:0} px");
            var given = Given(plan, 0);
            Assert.Equal(Cells(most, ListFormCases.Scale, textScale), given, 1);
            // And the room is filled: the summary stands within less than an event and the line's
            // own air of the bottom, rather than a gap an event would have fitted in.
            var ink = Ink(folder, plan, 0, LookFor(dark: true, textScale), ListFormCases.Scale);
            Assert.True(given - ink.Bottom <= 56 * textScale, $"the agenda stops {given - ink.Bottom:0} px above the bottom of its line");
        });

    // --- A stock -------------------------------------------------------------------------------

    /// <summary>
    /// A stock's line in its two sizes (§432): Full, the symbol, the company, the price and the change on a
    /// row and the day under them — the chart under the words, never taller than its most; Compact, its
    /// ticker on one row, the chart behind the words (docs\widgets.md §9, "A line at every height", which
    /// ListLineReadingsTests holds at every height).
    /// </summary>
    [UiFact]
    public void TheStockLineIsItsWordsAndItsDayAtEveryHeight() =>
        ListFormCases.Pictured(output, "list-form-stock.png",
        [
            new("A trading day", () =>
            {
                MarketIsland.Made = MarketOffscreenTests.Settled(new() { ["AAPL"] = MarketOffscreenTests.Answer() });
                return [Line(ListKind.Markets, island => island.Market = new MarketOptions { Symbol = "AAPL", Range = MarketRange.Day })];
            }),
            new("Compact", () =>
            {
                MarketIsland.Made = MarketOffscreenTests.Settled(new() { ["MSFT"] = MarketOffscreenTests.Answer("MSFT", "Microsoft Corporation", price: 412.34, percent: 1.24, previous: 407.3) });
                return [Line(ListKind.Markets, island =>
                {
                    island.Market = new MarketOptions { Symbol = "MSFT", Range = MarketRange.Day };
                    island.Size = LineSize.Compact;
                })];
            }),
        ],
        (@case, textScale, folder, natural, plan) =>
        {
            // Compact is the ticker's one row.
            if (@case.Name == "Compact" && textScale == 1)
            {
                Assert.Equal(Rows(1, ListFormCases.Scale, 1), Given(plan, 0), 1);
            }
        });
    // --- What is playing -------------------------------------------------------------------------

    /// <summary>
    /// What is playing is its art, its track and artist, and its own controls at the line's end:
    /// playing, and paused with the previous track out of reach.
    /// </summary>
    [UiFact]
    public void TheMediaLineIsWhatIsPlayingAndItsControls() =>
        ListFormCases.Pictured(output, "list-form-media.png",
        [
            new("Playing", () =>
            {
                MediaIsland.Stand = new MediaStand();
                return [Line(ListKind.Media)];
            }),
            new("Paused, first track", () =>
            {
                MediaIsland.Stand = new MediaStand("Blackbird", "The Beatles", "Sample Player", IsPlaying: false, CanPrevious: false);
                return [Line(ListKind.Media)];
            }),
        ]);

    // --- Clicks ----------------------------------------------------------------------------------

    /// <summary>
    /// A click on a line in a folder opens no popup: the calendar, the battery and what is
    /// playing are lines whose click does nothing (their <see cref="CellAct"/>), so whatever
    /// they ask for, anywhere on the line, the folder does not carry out. What is playing's own
    /// buttons are its own: a click on one is handled there and the folder stays open. A graph,
    /// Security, a stock and the clock are the lines that act — an app opening, a search in the
    /// browser (§263, §377), is what a folder lets a click do — and a click anywhere on one is the whole widget's.
    /// </summary>
    [UiFact]
    public void AClickOnALineOpensNoPopup()
    {
        OnStaThread(gfx =>
        {
            Clock.Now = () => ListFormCases.Frozen;
            CalendarIsland.Made = ListFormCases.Store(ListFormCases.Week);
            BatteryIsland.Probe = () => new SystemStatus.Battery(76, false, false);
            BatteryIsland.Made = () => new SampleBattery();
            MediaIsland.Stand = new MediaStand();
            Security.Made = () => new SampleSecurity();
            MarketIsland.Made = MarketOffscreenTests.Settled(new() { ["AAPL"] = MarketOffscreenTests.Answer() });
            var look = LookFor(dark: true);
            var folder = ListFormPictures.Folder(gfx,
                Line(ListKind.Clock), Line(ListKind.Calendar), Line(ListKind.Battery),
                Line(ListKind.Markets, island => island.Market = new MarketOptions { Symbol = "AAPL", Range = MarketRange.Day }), Line(ListKind.Media),
                Line(ListKind.Security), Line(ListKind.Cpu));
            try
            {
                var (_, plan) = ListFormPictures.Plan(folder, look, ListFormCases.Scale);
                for (var index = 0; index < folder.Tiles.Count; index++)
                {
                    var tile = folder.Tiles[index];
                    var line = (CellIsland)tile.Island;
                    var kind = tile.Cell.Island.List!.Value;
                    var card = plan.Cards[index];
                    var box = PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top);
                    var style = FolderFlyout.StyleFor(ListFormCases.Scale, look, FlyoutTheme.For(look));
                    var points = new[] { new Vector2(4, 4), ListFormCases.Centre(box), new Vector2(box.right - 4, box.bottom - 4) };
                    // What the folder asks of the line before it carries a click out.
                    var act = FolderFlyout.ActOf(tile.Cell);
                    Assert.Equal(ListWidgets.Of(kind).Act, act);
                    if (act == CellAct.Action)
                    {
                        // The whole line is the one target, and the click is the widget's own act:
                        // it opens an app, which is not called here because it would.
                        // A line is Task Manager's; a reading has no taskbar entry, its preset's face is (§97),
                        // and nor have markets and the clock, whose clicks search what the ticker tracks and the
                        // time where the clock stands once the folder has gone (§263, §377).
                        Assert.Equal(ListWidgets.Of(kind).Info.Metric is null && kind is not (ListKind.Markets or ListKind.Clock) ? BarClick.Action : null, BarEntries.Find(kind.Id())?.Click);
                        Assert.All(points, point => Assert.Equal(0, line.TargetOn(point, box, style, WidgetSurface.List)));
                        continue;
                    }
                    if (line is MediaIsland media)
                    {
                        // Its own controls, each its own target, handled where it is.
                        var parts = media.CardParts(box, style);
                        foreach (var button in new[] { parts.Previous, parts.Play, parts.Next })
                        {
                            Assert.True(button.right > button.left, "a list line without its controls");
                            var target = line.TargetOn(ListFormCases.Centre(button), box, style, WidgetSurface.List);
                            Assert.NotEqual(0, target);
                            Assert.False(FolderFlyout.Forwards(line.Click(target), act), "a media button left the folder");
                        }
                    }
                    foreach (var point in points)
                    {
                        var target = line.TargetOn(point, box, style, WidgetSurface.List);
                        var action = line.Click(target);
                        Assert.False(FolderFlyout.Forwards(action, act), $"{kind}'s line opened {action.View} from a folder");
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
}

/// <summary>A drive's line, run alone: every drive widget reads the one seam that stands in for the drives.</summary>
[Collection(AttachedDrivesCollection.Collection)]
public sealed class ListFormDriveTests(ITestOutputHelper output)
{
    private static ulong Gb(double gigabytes) => (ulong)(gigabytes * 1024 * 1024 * 1024);

    private static Drive Disk(int letter, string label, double total, double free, DriveKind kind = DriveKind.Fixed, string volume = @"\\?\Volume{11111111-1111-1111-1111-111111111111}\") =>
        new() { Letter = letter, Kind = kind, Volume = volume, Label = label, Total = Gb(total), Free = Gb(free) };

    private static CellItem Drive(ListKind kind, string volume, string letter, string label) =>
        Line(kind, island => island.Drive = new DriveTargetOptions { Volume = volume, Letter = letter, Label = label });

    /// <summary>
    /// A drive's line is the drives flyout's header: its picture, its name and kind, the capacity
    /// bar — in the alarm colour once nearly nothing is left — and how much is used and free. A
    /// network drive is the same line; a drive that isn't there says why in the figures' place.
    /// </summary>
    [UiFact]
    public void TheDriveLineIsItsCapacity()
    {
        var before = DriveWatch.Source;
        const string system = @"\\?\Volume{11111111-1111-1111-1111-111111111111}\";
        const string data = @"\\?\Volume{33333333-3333-3333-3333-333333333333}\";
        const string share = @"\\?\Volume{44444444-4444-4444-4444-444444444444}\";
        // No letters, so no roots: a drive's picture is the shell's, read off the dock's thread when a
        // widget first asks, and it would be this PC's — or none yet, depending on how soon it came
        // back. A drive with no letter draws its kind's glyph, the same on every machine.
        Drive[] drives =
        [
            Disk(-1, "Windows", 931, 419, volume: system),
            Disk(-1, "Data", 1863, 96, volume: data),
            Disk(-1, "Team", 4096, 1200, DriveKind.Remote, share),
        ];
        DriveWatch.Source = (_, remote) => drives.Where(drive => remote || drive.Kind != DriveKind.Remote).ToList();
        try
        {
            ListFormCases.Pictured(output, "list-form-drives.png",
            [
                new("Drives", () =>
                [
                    Drive(ListKind.Drive, system, "C:", "Windows"),
                    Drive(ListKind.Drive, data, "D:", "Data"),
                    Drive(ListKind.NetworkDrive, share, "Z:", "Team"),
                    Drive(ListKind.Drive, @"\\?\Volume{55555555-5555-5555-5555-555555555555}\", "E:", "Stick"),
                ]),
            ]);
        }
        finally
        {
            DriveWatch.Source = before;
        }
    }
}

/// <summary>The graphs' lines, run with the other tests of the shared samplers.</summary>
[Collection(SamplerState.Collection)]
public sealed class ListFormGraphTests(ITestOutputHelper output)
{
    /// <summary>
    /// A graph's line is its glyph, its name and its reading at the line's end, and the graph under
    /// them as wide as the line: the card's own pieces and its own graph, drawn wider. The five
    /// readings each fed a minute of made-up samples, so the pictures are the same every run.
    /// </summary>
    [UiFact]
    public void AGraphLineIsItsReadingAndItsGraph() =>
        ListFormCases.Pictured(output, "list-form-graphs.png",
        [
            new("Graphs", () => [Line(ListKind.Cpu), Line(ListKind.Gpu), Line(ListKind.Memory), Line(ListKind.Disk), Line(ListKind.Network)],
                Ready: (gfx, folder) =>
                {
                    for (var index = 0; index < folder.Tiles.Count; index++)
                    {
                        var graph = (PerformanceIsland)folder.Tiles[index].Island;
                        graph.Forgotten();
                        for (var second = 0; second < 180; second++)
                        {
                            var wave = (MathF.Sin((second + (index * 17)) / 11f) + 1) / 2;
                            if (folder.Tiles[index].Cell.Island.List == ListKind.Network)
                            {
                                graph.Feed(down: 900_000 * wave, up: 120_000 * wave);
                            }
                            else
                            {
                                graph.Feed(0.15f + (0.6f * wave));
                            }
                        }
                    }
                }),
        ]);
}
