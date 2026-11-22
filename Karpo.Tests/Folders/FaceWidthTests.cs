// folders: a face on the bar takes the room what it shows now needs, and no more (the person,
// 2026-10-19: "some faces take up more space than they need ... the widgets should always take the
// least amount of space while still showing all the information they have", §230; then "yes, size
// all faces to their current content", docs\widgets.md §244).
//
// Held in the pixels: every face's button is what it shows and the button's ends — its ink fills
// the face to within its letters' side bearings and a glyph's box, and never runs past it; a short
// reading and a long one of the same face differ by their words and nothing else. Held in the bar:
// a face that reads wider asks for the bar to be laid out again, and in the new layout it is wider
// and the widget after it moved by as much; a clock showing seconds ticks in one width, its figures
// tabular. The button is that and its ends, never a cell for a short face (§275).
//
// The sheets go to KARPO_PNG_DIR: face-widths-<100|150>.png (and .txt: the button's width and its
// ink's, in DIP), every face of words on a dark 48 DIP bar; face-content-150.png, the bar with every
// face at a short reading and at a long one, dark, at 150 %.

using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Folders;

[Collection(InLanguage.Collection)]
public sealed unsafe class FaceWidthTests
{
    /// <summary>One face: what it is called and how it is made, its reading in place.</summary>
    private sealed record Sample(string Name, Func<Gfx, FolderIsland> Make);

    private static BarItem Wearing(FolderFace face, params (string Key, string Value)[] options)
    {
        foreach (var (key, value) in options)
        {
            face.Set(face.Info.Option(key) ?? throw new ArgumentException(key), value);
        }
        return FaceCentringTests.Wearing(face);
    }

    private static readonly WeatherOptions NewYork = new() { Place = "New York, United States", Latitude = 40.71, Longitude = -74.01 };

    /// <summary>The air-quality face for <paramref name="place"/> (Zürich where none), set as asked.</summary>
    internal static BarItem Air(string width = AirQualityFace.Standard, string index = AirIndexes.Auto, WeatherOptions? place = null)
    {
        var face = AirQualityFace.New();
        face.Weather = place ?? SavedWeather.Options();
        if (width != AirQualityFace.Standard)
        {
            face.Set(FolderFaces.AirQuality.Option(AirQualityFace.Width)!, width);
        }
        if (index != AirIndexes.Auto)
        {
            face.Set(FolderFaces.AirQuality.Option(AirQualityFace.Index)!, index);
        }
        return FaceCentringTests.Wearing(face);
    }

    /// <summary>A folder wearing <paramref name="folder"/>'s face, reading <paramref name="air"/> and the saved forecast.</summary>
    internal static FolderIsland Made(Gfx gfx, BarItem folder, AirCache? air = null)
    {
        FolderIsland.AirStand = air;
        FolderIsland.WeatherStand = SavedWeather.Cache();
        try
        {
            return new FolderIsland(Config.IslandKey(null, folder.Id), folder, gfx, new Config());
        }
        finally
        {
            FolderIsland.AirStand = null;
            FolderIsland.WeatherStand = null;
        }
    }

    /// <summary>A share's ticker at <paramref name="price"/>, moved <paramref name="percent"/> %: the store the test's, never the network.</summary>
    private static FolderIsland Ticker(Gfx gfx, double price, double percent)
    {
        var holder = BarKind.FolderList.New();
        holder.Id = "ticker";
        holder.Folder!.Face = FolderFace.Of(FolderFaces.MarketsId);
        holder.Folder.Face.Market = (MarketOffscreenTests.Held("MSFT"));
        var line = ListKind.Clock.New();
        line.Id = "clock";
        holder.Folder.Put(line);
        holder.Normalize();
        var was = Clock.Now;
        Clock.Now = Clock.Real;
        MarketIsland.Made = MarketOffscreenTests.Settled(new()
        {
            ["MSFT"] = MarketOffscreenTests.Answer("MSFT", "Microsoft Corp.", price: price, percent: percent, previous: price / (1 + (percent / 100))),
        });
        try
        {
            var folder = new FolderIsland(Config.IslandKey(null, holder.Id), holder, gfx, new Config());
            Assert.True(Patience.Until(() => !folder.MarketQuote.Fetching), "the face's fetch never came back");
            _ = ((IIsland)folder).Refresh(gfx);
            return folder;
        }
        finally
        {
            MarketIsland.Made = null;
            Clock.Now = was;
        }
    }

    private static List<Sample> Samples()
    {
        Sample Face(string name, BarItem folder, AirCache? air = null) => new(name, gfx => Made(gfx, folder, air));
        return
        [
            Face("clock", Wearing(FolderFace.Of(FolderFaces.ClockId))),
            Face("clock 12h + seconds", Wearing(FolderFace.Of(FolderFaces.ClockId), (ClockFace.Hours, ClockFace.Twelve), (ClockFace.Seconds, "true"))),
            Face("calendar", Wearing(FolderFace.Of(FolderFaces.CalendarId))),
            Face("calendar day + date", Wearing(FolderFace.Of(FolderFaces.CalendarId), (CalendarFace.Form, CalendarFace.DayAndDate))),
            Face("time and date", Wearing(FolderFace.Of(FolderFaces.DateTimeId))),
            Face("weather", Wearing(WeatherFace.New())),
            Face("weather detailed", Wearing(WeatherFace.New(WeatherFace.Detailed))),
            Face("weather forecast", Wearing(WeatherFace.New(WeatherFace.Forecast))),
            Face("weather conditions", Wearing(WeatherFace.New(WeatherFace.Conditions))),
            Face("timer", Wearing(FolderFace.Of(FolderFaces.TimerId))),
            Face("focus", Wearing(FolderFace.Of(FolderFaces.FocusId))),
            Face("stopwatch", Wearing(FolderFace.Of(FolderFaces.StopwatchId))),
            Face("cpu", Wearing(FolderFace.Of(FolderFaces.CpuId))),
            Face("network activity", Wearing(FolderFace.Of(FolderFaces.NetworkActivityId))),
            new("stock 512.40 +0.86%", gfx => Ticker(gfx, 512.4, 0.86)),
            new("stock 8,888.88 +88.88%", gfx => Ticker(gfx, 8888.88, 88.88)),
            Face("news", Wearing(FolderFace.Of(FolderFaces.NewsId))),
            Face("drive", Wearing(FolderFace.Of(FolderFaces.DriveId))),
            Face("desktops", Wearing(FolderFace.Of(FolderFaces.DesktopsId))),
            Face("layouts", Wearing(FolderFace.Of(FolderFaces.LayoutsId))),
            Face("keyboard", Wearing(FolderFace.Of(FolderFaces.KeyboardId))),
            Face("meeting", Wearing(FolderFace.Of(FolderFaces.MeetingId))),
            Face("now playing", Wearing(Staying(FolderFace.Of(FolderFaces.NowPlayingId)))),
            Face("air, Zürich, 32 Fair", Air(), SavedAir.Cache(32, 72)),
            Face("air, Zürich, 12 Good", Air(), SavedAir.Cache(12, 32)),
            Face("air, Zürich, 132 Extremely poor", Air(), SavedAir.Cache(132, 412)),
            Face("air, Zürich, compact", Air(AirQualityFace.Compact), SavedAir.Cache(32, 72)),
            Face("air, Zürich, wide", Air(AirQualityFace.Wide), SavedAir.Cache(32, 72)),
            Face("air, New York, 72 Moderate", Air(place: NewYork), SavedAir.Cache(14, 72, europe: false)),
            Face("air, US AQI, 128 Sensitive", Air(index: AirIndexes.Us), SavedAir.Cache(32, 128)),
            Face("air, this PC's location", Air(place: WeatherOptions.New()), SavedAir.Cache(32, 72)),
        ];
    }

    /// <summary>Every face of words on a dark 48 DIP bar at 100 and 150 %, its width and its ink's written beside it.</summary>
    [UiFact]
    public void WidthsSheet()
    {
        SavedWeather.AtAFixedTime(() => Offscreen.OnStaThread(gfx => SavedWeather.AtAFixedTime(() =>
        {
            var made = Samples().Select(sample => (sample.Name, Island: sample.Make(gfx))).ToList();
            try
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var style = FaceCentringTests.Style(48, dpi);
                    var lines = new List<string>();
                    foreach (var (name, island) in made)
                    {
                        var ((left, _, right, _), width, _) = FaceCentringTests.InkOf(island, style, least: name.StartsWith("stock", StringComparison.Ordinal) ? (byte)160 : (byte)24);
                        lines.Add(FormattableString.Invariant($"{name}\t{width / style.Scale:0.#}\t{(right - left) / style.Scale:0.#}"));
                    }
                    var file = $"face-widths-{dpi * 100 / 96}";
                    File.WriteAllLines(Path.Combine(Offscreen.Pictures, file + ".txt"), lines);
                    FaceCentringTests.Sheet(gfx, [.. made.Select(item => (item.Name, item.Island, style))], file + ".png");
                }
            }
            finally
            {
                foreach (var (_, island) in made)
                {
                    island.Dispose();
                }
            }
        })));
    }

    /// <summary>A value at the top of each band of <paramref name="scale"/>, with the other index's alongside: every level at its widest.</summary>
    private static IEnumerable<(int Level, AirCache Cache)> Tops(AirScale scale, bool europe) =>
        Enumerable.Range(0, AirBands.Levels).Select(level =>
        {
            var top = level < AirBands.Levels - 1 ? AirBands.Most(scale, level) : scale == AirScale.European ? 132 : 412;
            return (level, scale == AirScale.European ? SavedAir.Cache(top, 50, europe) : SavedAir.Cache(30, top, europe));
        });

    /// <summary>
    /// The air-quality face is as wide as the level it reads (§244): each level's reading fills its
    /// face to within its letters' side bearings, every level at the top of its band says its fullest
    /// form within the room of its own index's widest level, which is the face's most — Zürich's
    /// European index, New York's US one, the US index chosen, at its three widths, at 100 and 150 %,
    /// in English and in Arabic. Zürich's room is not the US index's "Sensitive groups" one.
    /// </summary>
    [UiFact]
    public void TheAirQualityFaceIsAsWideAsTheLevelItReads()
    {
        var failures = new List<string>();
        SavedWeather.AtAFixedTime(() => Offscreen.OnStaThread(gfx => SavedWeather.AtAFixedTime(() =>
        {
            var cases = new (string Name, string Index, WeatherOptions? Place, AirScale Scale, bool Europe)[]
            {
                ("Zürich", AirIndexes.Auto, null, AirScale.European, true),
                ("New York", AirIndexes.Auto, NewYork, AirScale.Us, false),
                ("US AQI chosen", AirIndexes.Us, null, AirScale.Us, true),
            };
            foreach (var language in new[] { "en", "ar" })
            {
                using var _ = InLanguage.Use(language);
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var style = FaceCentringTests.Style(48, dpi);
                    var tolerance = 3 * style.Scale;
                    foreach (var (name, index, place, scale, europe) in cases)
                    {
                        foreach (var width in new[] { AirQualityFace.Compact, AirQualityFace.Standard, AirQualityFace.Wide })
                        {
                            var where = $"{name}, {width}, {dpi} DPI, {language}";
                            foreach (var (level, cache) in Tops(scale, europe))
                            {
                                using var island = Made(gfx, Air(width, index, place), cache);
                                var along = FolderIsland.Along(gfx, island.Options, style, folder: island);
                                var ((left, _, right, _), _, _) = FaceCentringTests.InkOf(island, style);
                                if (MathF.Abs(right - left - along) > tolerance)
                                {
                                    failures.Add($"{where}: level {level}'s ink is {right - left:0.#} px in a face of {along:0.#} px");
                                }
                                // Its fullest form, never a shorter one for want of room.
                                var reading = AirReading.From(cache, index, Clock.Now().ToUniversalTime(), null);
                                var form = AirText.Forms(reading.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), reading.Short, width)[0];
                                var room = AirFaceDrawing.Room(gfx, island.Options.Face, new Fit(style).Primary);
                                if (TextLine.Measure(gfx, form, new Fit(style).Primary, Weight.Regular, null) > room + 0.5f)
                                {
                                    failures.Add($"{where}: level {level}'s \"{form}\" does not fit its room of {room:0.#} px");
                                }
                            }
                        }
                    }
                }
            }

            // Zürich's room is no longer the US index's widest word's.
            var fit = new Fit(FaceCentringTests.Style(48, 144));
            var zurich = AirFaceDrawing.Room(gfx, Air().Folder!.Face, fit.Primary);
            var us = AirFaceDrawing.Room(gfx, Air(index: AirIndexes.Us).Folder!.Face, fit.Primary);
            Assert.True(zurich < us, $"Zürich's room {zurich} px, the US index's {us} px");
        })));
        Assert.True(failures.Count == 0, $"{failures.Count} air faces not as wide as their level:\n{string.Join("\n", failures.Take(40))}");
    }

    /// <summary>
    /// The scales a face can show: the chosen one; the place's region's where the place picks; both
    /// for this PC's location and within half a degree of the European area's edge. A level's widest
    /// value has the figures its band can have.
    /// </summary>
    [Fact]
    public void AFaceKeepsRoomForTheIndicesItsPlaceCanShow()
    {
        Assert.Equal([AirScale.European], AirBands.ScalesAt(AirIndexes.Auto, SavedWeather.Options()));
        Assert.Equal([AirScale.Us], AirBands.ScalesAt(AirIndexes.Auto, NewYork));
        Assert.Equal([AirScale.Us], AirBands.ScalesAt(AirIndexes.Us, SavedWeather.Options()));
        Assert.Equal([AirScale.European], AirBands.ScalesAt(AirIndexes.European, NewYork));
        Assert.Equal([AirScale.European, AirScale.Us], AirBands.ScalesAt(AirIndexes.Auto, WeatherOptions.New()));
        Assert.Equal([AirScale.European, AirScale.Us], AirBands.ScalesAt(AirIndexes.Auto, null));
        // Gavdos, 34.8° N, off Crete: the answer's grid point may fall on either side of 35°.
        Assert.Equal([AirScale.European, AirScale.Us], AirBands.ScalesAt(AirIndexes.Auto, new WeatherOptions { Place = "Gavdos", Latitude = 34.8, Longitude = 24.1 }));
        Assert.Equal(["88", "88", "88", "88", "888", "888"], Enumerable.Range(0, AirBands.Levels).Select(level => AirText.WidestAt(AirScale.European, level)));
        Assert.Equal(["88", "888", "888", "888", "888", "888"], Enumerable.Range(0, AirBands.Levels).Select(level => AirText.WidestAt(AirScale.Us, level)));
    }

    /// <summary>
    /// The ticker's change keeps the room of a rise, whose plus is wider than a minus: the widest
    /// price risen the most it says is drawn whole inside the button, at 100 and 150 %.
    /// </summary>
    [UiFact]
    public void AStocksRiseIsNotCut()
    {
        var failures = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            using var island = Ticker(gfx, 8888.88, 88.88);
            foreach (var dpi in new uint[] { 96, 144 })
            {
                var style = FaceCentringTests.Style(48, dpi);
                var along = FolderIsland.Along(gfx, island.Options, style, folder: island);
                var ((left, _, right, _), width, _) = FaceCentringTests.InkOf(island, style, least: 160);
                // The words keep inside the face's room, which stands in the middle of the button.
                var room = ((width - along) / 2, (width + along) / 2);
                if (left < room.Item1 - 1 || right > room.Item2 + 1)
                {
                    failures.Add($"{dpi} DPI: the ticker's words {left:0.#}-{right:0.#} px outside its room {room.Item1:0.#}-{room.Item2:0.#} px");
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    // --- What it shows now (§244) ---------------------------------------------------------------

    /// <summary>The button's ends, each side of its faces (WidgetShapes.Folder): a text widget's, never under 8 DIP (§506).</summary>
    private static float Pad(in IslandStyle style) => MathF.Max(new Fit(style).Edge, new Fit(style).Px(8));

    /// <summary>A folder's button of faces <paramref name="along"/> px long: they and its two ends, in whole px, and nothing more (§275).</summary>
    internal static float Button(in IslandStyle style, float along) => MathF.Ceiling(along + (2 * Pad(style)) - 0.05f);

    /// <summary>A lone glyph face's button: the glyph, the count's <paramref name="trail"/> where one shows, and the two ends — never a cell (§275).</summary>
    internal static float GlyphButton(in IslandStyle style, float trail = 0) => Button(style, new Fit(style).Glyph() + trail);

    /// <summary>
    /// Every face's button is what it shows now and the button's ends: as wide as its faces measure
    /// with the folder's reading and the two ends, never a cell for a short one (§275); its ink inside the face, and
    /// filling it but for its letters' side bearings and a glyph's box — no room kept for a reading it
    /// does not show — at 100 and 150 %.
    /// </summary>
    [UiFact]
    public void EveryFaceIsWhatItShowsAndTheButtonsEnds()
    {
        var failures = new List<string>();
        SavedWeather.AtAFixedTime(() => Offscreen.OnStaThread(gfx => SavedWeather.AtAFixedTime(() =>
        {
            var made = Samples().Select(sample => (sample.Name, Island: sample.Make(gfx))).ToList();
            try
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var style = FaceCentringTests.Style(48, dpi);
                    var fit = new Fit(style);
                    var pad = Pad(style);
                    foreach (var (name, island) in made)
                    {
                        var where = $"{name}, {dpi} DPI";
                        var along = FolderIsland.Along(gfx, island.Options, style, folder: island);
                        var ((left, _, right, _), width, _) = FaceCentringTests.InkOf(island, style, least: name.StartsWith("stock", StringComparison.Ordinal) ? (byte)160 : (byte)24);
                        if (MathF.Abs(width - Button(style, along)) > 0.5f)
                        {
                            failures.Add($"{where}: the button is {width:0.#} px for {along:0.#} px of face and its ends");
                        }
                        var slack = (fit.Glyph() / 2) + (4 * style.Scale);
                        if (left < pad - (2 * style.Scale) || right > width - pad + (2 * style.Scale))
                        {
                            failures.Add($"{where}: its ink {left:0.#}-{right:0.#} px runs past its face {pad:0.#}-{width - pad:0.#} px");
                        }
                        if (along - (right - left) > slack)
                        {
                            failures.Add($"{where}: its ink is {right - left:0.#} px in a face of {along:0.#} px");
                        }
                    }
                }
            }
            finally
            {
                foreach (var (_, island) in made)
                {
                    island.Dispose();
                }
            }
        })));
        Assert.True(failures.Count == 0, $"{failures.Count} faces not their content:\n{string.Join("\n", failures)}");
    }

    /// <summary>One face at a short reading and a long one.</summary>
    private sealed record Pair(string Name, Func<Gfx, FolderIsland> Short, Func<Gfx, FolderIsland> Long, DateTime? ShortAt = null, DateTime? LongAt = null);

    /// <summary>A short and a long morning, the shorter day of the week, and a 12-hour clock a figure apart.</summary>
    private static readonly DateTime Early = new(2026, 10, 5, 9, 5, 7);

    private static readonly DateTime Late = new(2026, 10, 7, 10, 58, 48);

    /// <summary>A now-playing face set to stay while nothing plays, which hides it by default (§319): measured idle too.</summary>
    private static FolderFace Staying(FolderFace face)
    {
        face.Set(FolderFaces.NowPlaying.Option(NowPlayingFace.HideIdle)!, false);
        return face;
    }

    /// <summary>
    /// The screen-time face reading <paramref name="today"/> so far, never this PC's count (§504): the stand-in stays
    /// on the thread, as the face reads it when it is measured and drawn, until the test lets it go.
    /// </summary>
    private static FolderIsland ScreenTime(Gfx gfx, TimeSpan today)
    {
        var log = new ScreenTimeLog();
        log.Ensure(new DateOnly(2026, 11, 5)).Total = today;
        ScreenTimeFaceDrawing.Stand = () => new ScreenTimeSnapshot(new DateOnly(2026, 11, 5), log);
        return FolderFaceTests.Make(gfx, FolderFaceTests.Folder(FolderFaces.ScreenTimeId));
    }

    /// <summary>The desktops' face, the desktop called <paramref name="name"/>: what it reads is the dock's, set before it is measured.</summary>
    private static FolderIsland Desktop(Gfx gfx, string name)
    {
        _ = DesktopState.Set([name], 0);
        return FolderFaceTests.Make(gfx, FolderFaceTests.Folder(FolderFaces.DesktopsId));
    }

    private static List<Pair> Pairs()
    {
        static Func<Gfx, FolderIsland> Face(BarItem folder) => gfx => FolderFaceTests.Make(gfx, folder);
        return
        [
            new("clock 12h", Face(FolderFaceTests.Folder(FolderFaces.ClockId, (ClockFace.Hours, ClockFace.Twelve))),
                Face(FolderFaceTests.Folder(FolderFaces.ClockId, (ClockFace.Hours, ClockFace.Twelve))), Early, Late),
            new("clock 12h + seconds", Face(FolderFaceTests.Folder(FolderFaces.ClockId, (ClockFace.Hours, ClockFace.Twelve), (ClockFace.Seconds, "true"))),
                Face(FolderFaceTests.Folder(FolderFaces.ClockId, (ClockFace.Hours, ClockFace.Twelve), (ClockFace.Seconds, "true"))), Early, Late),
            new("stock", gfx => Ticker(gfx, 5.12, 0.86), gfx => Ticker(gfx, 8888.88, 88.88)),
            new("air", gfx => Made(gfx, Air(), SavedAir.Cache(12, 32)), gfx => Made(gfx, Air(), SavedAir.Cache(132, 412))),
            new("desktops", gfx => Desktop(gfx, "X"), gfx => Desktop(gfx, "Desktop 2")),
            new("screen time", gfx => ScreenTime(gfx, TimeSpan.FromMinutes(5)), gfx => ScreenTime(gfx, TimeSpan.FromMinutes(222))),
        ];
    }

    /// <summary>
    /// A short reading and a long one of the same face differ by their words and nothing else: the
    /// button by as much as the ink, within a pixel or two — the clock's "9:05 AM" and "10:58 AM", a
    /// share at 5.12 and at 8,888.88, the air at "12 · Good" and "132 · Extremely poor", a desktop
    /// called "X" and one called "Desktop 2", "5 min" of screen time and "3 h 42 min" (now playing is a fixed width
    /// since §503).
    /// </summary>
    [UiFact]
    public void AShortReadingAndALongOneDifferByTheirWords()
    {
        var failures = new List<string>();
        SavedWeather.AtAFixedTime(() => Offscreen.OnStaThread(gfx => SavedWeather.AtAFixedTime(() =>
        {
            var was = Clock.Now;
            try
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var style = FaceCentringTests.Style(48, dpi);
                    foreach (var pair in Pairs())
                    {
                        (float Width, float Ink) Read(Func<Gfx, FolderIsland> make, DateTime? at)
                        {
                            Clock.Now = at is { } moment ? () => moment : was;
                            using var island = make(gfx);
                            var ((left, _, right, _), width, _) = FaceCentringTests.InkOf(island, style, least: pair.Name == "stock" ? (byte)160 : (byte)24);
                            return (width, right - left);
                        }
                        var (shortWidth, shortInk) = Read(pair.Short, pair.ShortAt);
                        var (longWidth, longInk) = Read(pair.Long, pair.LongAt);
                        var where = $"{pair.Name}, {dpi} DPI: {shortWidth:0.#} px ({shortInk:0.#} of ink) and {longWidth:0.#} px ({longInk:0.#})";
                        if (!(shortWidth < longWidth) || MathF.Abs((longWidth - shortWidth) - (longInk - shortInk)) > 2 * style.Scale)
                        {
                            failures.Add(where);
                        }
                    }
                }
            }
            finally
            {
                Clock.Now = was;
                _ = DesktopState.Set(["Desktop 1"], 0);
                ScreenTimeFaceDrawing.Stand = null;
            }
        })));
        Assert.True(failures.Count == 0, $"{failures.Count} faces not their words:\n{string.Join("\n", failures)}");
    }

    /// <summary>
    /// A face that reads wider lays the bar out again (§244): a 12-hour clock going from 9:59 to 10:00
    /// asks for it on the minute, the new layout's clock is a figure wider and the folder after it
    /// moved by as much; a minute later, the same shape, it asks for nothing — at 100 and 150 %.
    /// </summary>
    [UiFact]
    public void AFaceThatReadsWiderLaysTheBarOutAgain()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var was = Clock.Now;
            try
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var at = new DateTime(2026, 10, 19, 9, 59, 30);
                    Clock.Now = () => at;
                    var clockItem = FolderFaceTests.Folder(FolderFaces.ClockId, (ClockFace.Hours, ClockFace.Twelve));
                    clockItem.Id = "clock";
                    var iconItem = FolderFaceTests.Folder(FolderFaces.IconId);
                    iconItem.Id = "icon";
                    foreach (var item in new[] { clockItem, iconItem })
                    {
                        item.Section = BarSection.Start;
                    }
                    using var clock = FolderFaceTests.Make(gfx, clockItem);
                    using var icon = FolderFaceTests.Make(gfx, iconItem);
                    var (clockKey, iconKey) = (Config.IslandKey(null, clockItem.Id), Config.IslandKey(null, iconItem.Id));
                    var specs = new List<PartSpec> { PartSpec.For(clockKey, clockItem), PartSpec.For(iconKey, iconItem) };
                    var content = new Content { Islands = new IslandCollection { [clockKey] = clock, [iconKey] = icon } };
                    var config = new Config { Height = 48 };
                    var look = Karpo.Tests.Edit.AddListPictures.LookFor(true) with { Animations = false };
                    var area = new RECT { left = 0, top = 0, right = (int)(1920 * dpi / 96), bottom = (int)(1080 * dpi / 96) };
                    Layout Lay() => new(gfx, dpi, area, area, Edge.Bottom, config, look, specs, content);

                    var before = Lay();
                    // The same minute: nothing to lay out again.
                    Assert.False(clock.MinuteChanged(gfx));
                    at = new DateTime(2026, 10, 19, 10, 0, 0);
                    Assert.True(clock.MinuteChanged(gfx), $"{dpi} DPI: ten o'clock did not ask for the bar again");
                    var after = Lay();
                    var grown = after.BlockRect(0).Width - before.BlockRect(0).Width;
                    Assert.True(grown > 0, $"{dpi} DPI: ten o'clock is no wider than nine");
                    Assert.Equal(grown, after.BlockRect(1).X - before.BlockRect(1).X, 1f);
                    Assert.Equal(before.BlockRect(1).Width, after.BlockRect(1).Width);
                    // 10:01 reads the same shape: no new layout.
                    at = new DateTime(2026, 10, 19, 10, 1, 0);
                    Assert.False(clock.MinuteChanged(gfx), $"{dpi} DPI: 10:01 asked for the bar again");
                }
            }
            finally
            {
                Clock.Now = was;
            }
        });
    }

    /// <summary>
    /// A clock showing seconds never moves the bar as it ticks: its figures are tabular, so every
    /// second of two minutes, and the narrowest figures and the widest ("11:11:11", "10:00:00",
    /// "08:48:08"), are one width; no tick asks for the bar again — a 24-hour clock and a 12-hour
    /// one, at 100 and 150 %.
    /// </summary>
    [UiFact]
    public void AClockWithSecondsTicksInOneWidth()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var was = Clock.Now;
            try
            {
                foreach (var hours in new[] { ClockFace.TwentyFour, ClockFace.Twelve })
                {
                    foreach (var dpi in new uint[] { 96, 144 })
                    {
                        var at = new DateTime(2026, 10, 19, 10, 58, 0);
                        Clock.Now = () => at;
                        using var island = FolderFaceTests.Make(gfx, FolderFaceTests.Folder(FolderFaces.ClockId, (ClockFace.Hours, hours), (ClockFace.Seconds, "true")));
                        var style = FaceCentringTests.Style(48, dpi);
                        _ = island.Measure(style);
                        _ = island.Surface(0, style);
                        var width = island.CellSize(0, style).Width;
                        for (var second = 1; second < 120; second++)
                        {
                            at = new DateTime(2026, 10, 19, 10, 58, 0).AddSeconds(second);
                            Assert.False(island.FaceTick(gfx), $"{hours}, {dpi} DPI: {at:T} asked for the bar again");
                            Assert.Equal(width, island.CellSize(0, style).Width);
                        }
                        foreach (var moment in new[] { new DateTime(2026, 10, 19, 11, 11, 11), new DateTime(2026, 10, 19, 10, 0, 0), new DateTime(2026, 10, 19, 8, 48, 8) })
                        {
                            at = moment;
                            var reads = ClockFaceDrawing.Time(ClockFace.Read(island.Options.Face), DateTime.MinValue);
                            if (hours == ClockFace.TwentyFour || moment.Hour >= 10)
                            {
                                Assert.True(width == island.CellSize(0, style).Width, $"{hours}, {dpi} DPI: \"{reads}\" is another width");
                            }
                        }
                    }
                }
            }
            finally
            {
                Clock.Now = was;
            }
        });
    }

    /// <summary>
    /// face-content-150.png: the bar with every face of <see cref="Pairs"/> and the rest of the faces
    /// of words at a short reading, and under it the same bar at a long one — dark, 48 DIP, 150 %.
    /// </summary>
    [UiFact]
    public void ShortAndLongBars()
    {
        SavedWeather.AtAFixedTime(() => Offscreen.OnStaThread(gfx => SavedWeather.AtAFixedTime(() =>
        {
            var was = Clock.Now;
            var style = FaceCentringTests.Style(48, 144);
            var rows = new List<(string Label, DateTime At, string Desktop, List<FolderIsland> Islands)>();
            // What the row reads: its moment, and its desktop's name, which is the dock's and not the face's.
            void Reading((string Label, DateTime At, string Desktop, List<FolderIsland> Islands) row)
            {
                Clock.Now = () => row.At;
                _ = DesktopState.Set([row.Desktop], 0);
            }
            try
            {
                // Faces whose reading is the same in both rows but for the time and the day.
                BarItem[] timed =
                [
                    FolderFaceTests.Folder(FolderFaces.CalendarId, (CalendarFace.Form, CalendarFace.DayAndDate)),
                    FolderFaceTests.Folder(FolderFaces.DateTimeId),
                    Wearing(WeatherFace.New()),
                    FolderFaceTests.Folder(FolderFaces.TimerId),
                    FolderFaceTests.Folder(FolderFaces.CpuId),
                ];
                foreach (var (label, at, desktop, longOne) in new[] { ("short", Early, "X", false), ("long", new DateTime(2026, 12, 23, 23, 59, 59), "Desktop 2", true) })
                {
                    Clock.Now = () => at;
                    var islands = Pairs().Select(pair => (longOne ? pair.Long : pair.Short)(gfx)).ToList();
                    islands.AddRange(timed.Select(folder => Made(gfx, folder)));
                    rows.Add((label, at, desktop, islands));
                }
                const float gap = 30;
                var widths = rows.Select(row =>
                {
                    Reading(row);
                    return row.Islands.Sum(island => island.CellSize(0, style).Width);
                }).ToList();
                var bar = Whole(style.Thickness);
                var total = (2 * gap) + widths.Max();
                var tall = gap + (rows.Count * (bar + gap));
                var theme = FlyoutTheme.For(Karpo.Tests.Edit.AddListPictures.LookFor(true));
                Offscreen.Bitmap(total, tall, dc =>
                {
                    PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, total, tall), 0, PopupPaint.Ink(theme.Base));
                    var top = gap;
                    for (var index = 0; index < rows.Count; index++)
                    {
                        Reading(rows[index]);
                        PopupPaint.Text(dc, gfx, rows[index].Label, 14f, Weight.Regular, theme.TextSecondary, 4, top - 22, 200f);
                        var x = gap;
                        PopupPaint.Fill(dc, PopupPaint.Rect(x, top, widths[index], bar), 0, PopupPaint.Ink(theme.CardBackground));
                        foreach (var island in rows[index].Islands)
                        {
                            island.Forget();
                            var (w, h) = island.CellSize(0, style);
                            var drawn = style;
                            Offscreen.Shifted(dc, x, top, () => island.PaintCell(dc, 0, drawn, w, h));
                            // Where one button ends and the next starts.
                            PopupPaint.Fill(dc, PopupPaint.Rect(x + w - 0.5f, top + bar + 2, 1, 6), 0, new TerraFX.Interop.DirectX.DXGI_RGBA(1, 0.3f, 0.3f, 0.8f));
                            x += w;
                        }
                        top += bar + gap;
                    }
                }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, "face-content-150.png")));
            }
            finally
            {
                Clock.Now = was;
                _ = DesktopState.Set(["Desktop 1"], 0);
                foreach (var (_, _, _, islands) in rows)
                {
                    foreach (var island in islands)
                    {
                        island.Dispose();
                    }
                }
            }
        })));
    }

    private static float Whole(float px) => MathF.Max(1, MathF.Ceiling(px));
}
