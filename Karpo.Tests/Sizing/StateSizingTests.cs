// A widget must not change size because its state changed: switching a
// keyboard layout, a notification arriving, a battery charging past 99, a
// track with a longer title — none of them may move the bar. A widget that
// shows something of varying length keeps room for the worst it can show and
// draws into that room, cutting the content where it does not fit.
//
// Two ways of holding to it:
//
//  * the islands with a seam for their state are measured in each state and
//    must come out the same number of cells;
//  * for the rest, the room each keeps is a template of the worst case, so the
//    test measures every state's text against that template. A new state whose
//    text is wider than the room — another weather condition, another duration
//    shape — fails here, which is where it should fail.
//
// Both need Direct2D for measuring, so they run only with KARPO_UI_TESTS=1.

using Karpo.Dock;
// weather: WeatherCodes.Conditions moved to Karpo.Dock.Weather with the rest of the weather's words.
using Karpo.Dock.Weather;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.Sizing;

public sealed unsafe class StateSizingTests
{
    private static readonly int[] Heights = [28, 48, 72, 115, 160];

    private static Look LookFor(float textScale = 1f) => new(
        Glass: false,
        Dark: true,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8))
    { TextScale = textScale };

    /// <summary>Every style a widget is measured in here: the bar's heights, scales and edges.</summary>
    private static IEnumerable<(string Where, IslandStyle Style)> Styles()
    {
        foreach (var height in Heights)
        {
            var config = new Config { Height = height };
            foreach (var dpi in (uint[])[96, 144])
            {
                foreach (var vertical in new[] { false, true })
                {
                    yield return (
                        $"{height} DIP, {dpi} DPI{(vertical ? ", vertical" : "")}",
                        new IslandStyle(dpi / 96f, vertical, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), LookFor()));
                }
            }
        }
    }

    /// <summary>How many cells <paramref name="island"/> takes in <paramref name="style"/>, cell by cell.</summary>
    private static int[] CellsOf(IIsland island, in IslandStyle style)
    {
        var cells = island.Measure(style);
        var counts = new int[cells.Count];
        for (var index = 0; index < cells.Count; index++)
        {
            var length = cells.Lengths is { } lengths && index < lengths.Count ? lengths[index] : cells.CellLength;
            counts[index] = (int)MathF.Round(length / style.Cell);
        }
        return counts;
    }

    // --- Islands with a seam for their state -----------------------------------

    /// <summary>
    /// The bell — the notifications preset's face (§100): nothing waiting, a few, and more than the
    /// badge can count. A face is no more than it shows and its ends (§275), but the bell keeps its widest
    /// badge's room ("99+") whether one shows or not, so the button never changes size as notifications
    /// come and go (§276).
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void TheBellKeepsItsBadgesRoomWhateverIsWaiting()
    {
        OnStaThread(gfx =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "karpo-tests", "state-" + Guid.NewGuid().ToString("N"));
            var config = new Config();
            var islands = new List<FolderIsland>();
            try
            {
                foreach (var count in new[] { 0, 5, 150 })
                {
                    var database = Path.Combine(folder, $"db{count}", "wpndatabase.db");
                    Karpo.Tests.Islands.FlyoutOffscreenTests.BuildDatabase(database, count);
                    var store = new NotificationStore(database, Path.Combine(folder, $"copy{count}"), Path.Combine(folder, $"state{count}"), app => (app, null));
                    store.Prepare();
                    var island = Karpo.Tests.Islands.BadgeSizeTests.BellFace(gfx, store, $"/n{count}", config);
                    _ = island.Refresh(gfx);
                    islands.Add(island);
                }
                foreach (var (where, style) in Styles())
                {
                    foreach (var island in islands)
                    {
                        var cells = island.Measure(style);
                        Assert.True(cells.Count == 1, $"one button, whatever is waiting, at {where}");
                        Assert.Equal(Karpo.Tests.Folders.FaceWidthTests.GlyphButton(style, GlyphBadge.Trail(gfx, style)), cells.Lengths is [var only] ? only : cells.CellLength, 0.5f);
                    }
                }
            }
            finally
            {
                foreach (var island in islands)
                {
                    island.Dispose();
                }
            }
        });
    }

    /// <summary>
    /// The keyboard layout: switching it must not move the bar, including to and from a layout
    /// with no region ("ENG" over "US" against "DEU" alone). This is the test whose absence let
    /// the widget ship with its sizing branching on whether the layout in use had a region —
    /// the branch happened to measure the same room on both sides, but nothing said it had to.
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void TheKeyboardIsTheSameSizeForEveryLayout()
    {
        OnStaThread(gfx =>
        {
            var config = new Config();
            (string Language, string Region)[] layouts =
            [
                ("ENG", "US"),
                ("DEU", ""),
                ("FRA", "CA"),
                ("POR", ""),
                ("NLD", "NL"),
            ];
            using var island = new KeyboardIsland("/keyboard", new BarItem { Id = "k", Kind = BarKind.NightLight }, gfx, config);
            foreach (var (where, style) in Styles())
            {
                island.Show(layouts[0]);
                var first = CellsOf(island, style);
                foreach (var layout in layouts)
                {
                    island.Show(layout);
                    Assert.Equal(first, CellsOf(island, style));
                }
                Assert.True(first.Length == 1, $"the keyboard is one cell, not {first.Length}, at {where}");
            }
        });
    }

    /// <summary>
    /// The clock keeps room for the longest the time ever reads, not for what it reads now. In a
    /// region that writes 12-hour time, one o'clock is a figure shorter than eleven, so a clock
    /// measured on the time showing was narrower all morning and the bar moved at ten — which is
    /// what it did until stage 3. This is the test that would fail if someone reached for the
    /// live time again; on a 24-hour region it passes either way, which is why it is written
    /// over every hour rather than over the clock.
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void TheClocksRoomFitsEveryHourOfTheDay()
    {
        OnStaThread(gfx =>
        {
            foreach (var seconds in new[] { false, true })
            {
                var px = 20f;
                var (widest, _) = TextLine.Template(gfx, Clock.Widest(seconds), px, Weight.Regular);
                for (var hour = 0; hour < 24; hour++)
                {
                    var reading = Clock.At(hour, 59, 59, seconds);
                    var (width, _) = TextLine.Template(gfx, reading, px, Weight.Regular);
                    Assert.True(
                        width <= widest + 0.01f,
                        $"the clock keeps {widest} for \"{Clock.Widest(seconds)}\" but {reading} needs {width}");
                }
            }
        });
    }

    /// <summary>
    /// The volume: its glyph changes with the level, and the cell does not. (The Wi-Fi and
    /// Bluetooth faces are one glyph's square whatever they show: FolderFaceTests.)
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void TheSystemControlsAreTheSameSizeInEveryState()
    {
        OnStaThread(gfx =>
        {
            var config = new Config();
            SystemState[] states =
            [
                new("", "", NetworkBackGlyph: "", BluetoothGlyph: "", BluetoothOn: false),
                new("", "", NetworkBackGlyph: "", BluetoothGlyph: "", BluetoothOn: true),
                new("", "", NetworkBackGlyph: "", BluetoothGlyph: "", BluetoothOn: false),
            ];
            // The sound, the sound preset's face: the speaker at every level keeps its square.
            using var island = new FolderIsland("/sound", Presets.Folder(Presets.Find(Presets.SoundId)!, new PresetMachine(HasBattery: false, FixedDrives: [])), gfx, config);
            foreach (var (where, style) in Styles())
            {
                island.SetStatus(states[0], gfx);
                var first = CellsOf(island, style);
                foreach (var state in states)
                {
                    island.SetStatus(state, gfx);
                    Assert.Equal(first, CellsOf(island, style));
                }
                Assert.True(first.Length == 1, $"the sound face at {where}");
            }
        });
    }

    /// <summary>
    /// Automatic means the smallest size that shows a widget's essential
    /// content, not the widest thing it could ever say: on the taskbar's own
    /// 48-DIP bar the weather is its glyph and a temperature, the calendar is
    /// its date, and neither keeps room for words that belong to a wider width.
    /// The user called the old weather widget huge; this is the guard.
    /// <para>
    /// One row at every height (docs\widgets.md §12.5) costs the battery
    /// (its glyph and "88%") the second cell its stack used to save: two, and
    /// no more. The input indicator is one square cell, its language over its
    /// region, as Windows' own (2026-10-08).
    /// </para>
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void AutomaticIsTheSmallestSizeThatShowsTheEssentials()
    {
        OnStaThread(gfx =>
        {
            var config = new Config { Height = 48 };
            var style = new IslandStyle(1f, false, Layout.CellPx(96, config), Layout.IconPx(96, config), LookFor());
            (BarItem Widget, int Most)[] widgets =
            [
                // The weather and the date are folders' faces now (WeatherFaceTests, FolderFaceTests).
                (new BarItem { Id = "d", Kind = BarKind.NightLight }, 2),
                (Presets.Folder(Presets.Find(Presets.BatteryId)!, new PresetMachine(HasBattery: true, FixedDrives: [])), 2),
                (new BarItem { Id = "k", Kind = BarKind.NightLight }, 1),
            ];
            foreach (var (widget, most) in widgets)
            {
                if (Karpo.Dock.Islands.Create(Config.IslandKey(null, widget.Id), widget, gfx, config) is not { } island)
                {
                    continue;
                }
                using (island)
                {
                    _ = island.Refresh(gfx);
                    var cells = CellsOf(island, style);
                    Assert.True(cells.Sum() <= most, $"{widget.Kind} takes {cells.Sum()} cells at its automatic size, more than the {most} it should need");
                }
            }
        });
    }

    // --- Islands whose room is a template of the worst case --------------------

    /// <summary>
    /// The room each of those widgets keeps must hold every state it can be in.
    /// Measured with the dock's own text measuring, at every text size a bar
    /// uses, so a state whose words outgrow the room fails here.
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void TheRoomAWidgetKeepsHoldsEveryStateItCanBeIn()
    {
        OnStaThread(gfx =>
        {
            foreach (var (where, style) in Styles())
            {
                var fit = new Fit(style);
                foreach (var px in new[] { fit.Primary, fit.Secondary, fit.Small })
                {
                    // The battery's percentage, every charge it can report.
                    Fits(gfx, px, Weight.Regular, "100%", Percentages(), $"battery at {where}");

                    // The screen-time total, every minute of a day.
                    Fits(gfx, px, Weight.Regular, "88h 88m", Durations(), $"screen time at {where}");

                    // The weather's temperature, in each unit. The words have
                    // no room of their own any more: they fill what the cell
                    // leaves over and are cut there, so there is nothing to
                    // reserve for them.
                    Fits(gfx, px, Weight.Regular, "-88°", ["0°", "-9°", "18°", "-18°", "-88°", "–"], $"celsius at {where}");
                    Fits(gfx, px, Weight.Regular, "188°", ["0°", "-9°", "72°", "104°", "188°", "–"], $"fahrenheit at {where}");

                    // The weekday on the date and calendar widgets, every day of the week.
                    foreach (var weight in new[] { Weight.Regular, Weight.Semibold })
                    {
                        Fits(gfx, px, weight, Widest(gfx, px, weight, Weekdays()), Weekdays(), $"weekday at {where}");
                    }

                    // The keyboard's language and region codes.
                    Fits(gfx, px, Weight.Regular, "WWW", ["ENG", "DEU", "FRA", "POR", "NLD", "US", "CH", "GB", "DVORAK"[..3]], $"keyboard at {where}");
                }
            }
        });
    }

    /// <summary>Every percentage a battery can report.</summary>
    private static string[] Percentages() => [.. Enumerable.Range(0, 101).Select(percent => Units.Percent(percent / 100f))];

    /// <summary>Every total a day of screen time can read.</summary>
    private static string[] Durations() => [.. Enumerable.Range(0, 24 * 60).Select(Units.Duration)];

    /// <summary>The week's seven short day names, as the region writes them.</summary>
    private static string[] Weekdays()
    {
        var monday = new DateTime(2026, 1, 5);
        return [.. Enumerable.Range(0, 7).Select(day => Dates.Format(monday.AddDays(day), "ddd"))];
    }

    private static string Widest(Gfx gfx, float px, Weight weight, IReadOnlyList<string> texts)
    {
        var widest = texts[0];
        foreach (var text in texts)
        {
            if (TextLine.Template(gfx, text, px, weight).Width > TextLine.Template(gfx, widest, px, weight).Width)
            {
                widest = text;
            }
        }
        return widest;
    }

    /// <summary>Every one of <paramref name="states"/> measures no wider than <paramref name="room"/>.</summary>
    private static void Fits(Gfx gfx, float px, Weight weight, string room, IReadOnlyList<string> states, string where)
    {
        var kept = TextLine.Template(gfx, room, px, weight).Width;
        foreach (var state in states)
        {
            var wanted = TextLine.Template(gfx, state, px, weight).Width;
            Assert.True(wanted <= kept + 0.01f, $"{where}: \"{state}\" is {wanted} px, the room kept is {kept} px (\"{room}\")");
        }
    }

    // --- The thread it all runs on ---------------------------------------------

    private static void OnStaThread(Action<Gfx> body)
    {
        Exception? failed = null;
        var thread = new Thread(() =>
        {
            try
            {
                var options = new DispatcherQueueOptions
                {
                    dwSize = (uint)sizeof(DispatcherQueueOptions),
                    threadType = DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_CURRENT,
                    apartmentType = DISPATCHERQUEUE_THREAD_APARTMENTTYPE.DQTAT_COM_STA,
                };
                IDispatcherQueueController* controller = null;
                Karpo.Dock.Native.ComUtil.Check(CreateDispatcherQueueController(options, &controller));
                try
                {
                    using var gfx = new Gfx(gpu: false);
                    body(gfx);
                }
                finally
                {
                    controller->Release();
                }
            }
            catch (Exception error)
            {
                failed = error;
            }
        });
        thread.Start();
        thread.Join();
        if (failed is not null)
        {
            throw new Xunit.Sdk.XunitException("On the offscreen thread: " + failed);
        }
    }
}
