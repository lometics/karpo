// weather: the weather face (Karpo.Core\FolderFaces.cs, Karpo.Dock\Islands\WeatherFaceDrawing.cs,
// docs\widgets.md §80): what the weather entry on the bar drew, as a folder's face, with the entry's
// three looks — simple, detailed and the three-day forecast — and Windows' Widgets button's, the
// temperature over the conditions in words (2026-10-13, a new bar's).
//
// The file: the look is the face's option, the simple one its default and never written; a word
// this build does not know is kept and read as simple; every look is named in every language.
//
// The bar, held on real cells at the bar heights from the thinnest to a tall one, at 100 % and
// 150 %, in the dark and the light:
//
// - each look is as wide as its content needs, in that order (simple, detailed, forecast), and the
//   width is a function of the settings: another forecast, other temperatures, another condition
//   move nothing;
// - each look is drawn inside its cell, whole, on one row;
// - the forecast's weekdays are the region's (Nls.ShortDayName), start at the place's today, and a
//   forecast gone stale across midnight starts at the new day;
// - the place is the face's own, shared with no line of its folder.
//
// The sheets (weather-faces-<dpi>-<dark|light>.png) go to KARPO_PNG_DIR before anything is asserted.

using System.Globalization;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Dock.Panels;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

public sealed unsafe class WeatherFaceTests
{
    private static readonly int[] Heights = [28, 36, 48, 80];

    private static readonly string[] Looks = [WeatherFace.Simple, WeatherFace.Detailed, WeatherFace.Forecast, WeatherFace.Conditions];

    // --- The file -------------------------------------------------------------------------------

    /// <summary>Four looks, simple first and the default: the weather preset's face is the simple one; Windows' Widgets button's look last.</summary>
    [Fact]
    public void TheWeatherFaceHasFourLooksAndTheSimpleOneIsTheDefault()
    {
        var look = FolderFaces.Weather.Option(WeatherFace.Look)!;
        Assert.Equal(Looks, look.Choices);
        Assert.Equal(WeatherFace.Simple, WeatherFace.LookOf(FolderFace.Of(FolderFaces.WeatherId)));
        Assert.Equal(WeatherFace.Simple, WeatherFace.LookOf(Presets.Find(Presets.WeatherId)!.Face!));
        Assert.Contains(FolderFaces.Weather, FolderFaces.All);
    }

    /// <summary>A look other than the default is written in its own word and read back; the default is not written at all.</summary>
    [Theory]
    [InlineData(WeatherFace.Detailed)]
    [InlineData(WeatherFace.Forecast)]
    public void ALookSurvivesTheFile(string id)
    {
        var island = BarKind.FolderList.New();
        island.Id = "weather";
        island.Folder!.Face = WeatherFace.New(id);
        var config = Karpo.Tests.Core.Configs.With(island);

        var back = Karpo.Tests.Core.Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal(id, WeatherFace.LookOf(Karpo.Core.Folders.FolderIn(Karpo.Tests.Core.Configs.Islands(back), "weather")!.Face));
        Assert.Empty(WeatherFace.New().Options);
    }

    /// <summary>A word from a newer Karpo is kept in the file as it is, and read as the simple look.</summary>
    [Fact]
    public void AnUnknownLookReadsAsSimpleAndIsKept()
    {
        var face = FolderFace.Of(FolderFaces.WeatherId);
        face.Set(FolderFaces.Weather.Option(WeatherFace.Look)!, "hologram");
        Assert.Equal(WeatherFace.Simple, WeatherFace.LookOf(face));
        Assert.Equal("hologram", face.Options[WeatherFace.Look].GetString());
    }

    /// <summary>The face and every look are named in every language; the choice is called Style, as every style (§292).</summary>
    [Fact]
    public void EveryLookIsNamedInEveryLanguage()
    {
        Assert.Equal(S.SettingsWidgetStyleHeader, FolderFaces.Weather.TitleOf(FolderFaces.Weather.Option(WeatherFace.Look)!));
        foreach (var language in StringFiles.Codes)
        {
            var words = StringFiles.Map(language);
            string[] keys =
            [
                "core.face.weather.title", "core.face.weather.blurb",
                .. Looks.Select(look => $"core.face.weather.look.{look}"),
            ];
            foreach (var key in keys)
            {
                Assert.True(words.TryGetValue(key, out var text) && text.Length > 0, $"{language}.json has no {key}");
            }
        }
    }

    /// <summary>
    /// The face's place is its own (FolderFace.Weather): a line of its folder given a place gives the
    /// face none, and the face's place gives the lines none.
    /// </summary>
    [Fact]
    public void TheFacesPlaceIsItsOwn()
    {
        var folder = Presets.Folder(Presets.Find(Presets.WeatherId)!, new PresetMachine(false, [])).Folder!;
        foreach (var cell in folder.Cells.Where(cell => cell.Island.Weather is not null))
        {
            cell.Island.Weather = new WeatherOptions();
        }
        folder.Face.Weather = new WeatherOptions();
        Assert.Null(WeatherPlaces.OfFace(folder));

        var now = folder.Cells.Single(cell => cell.Island.List == ListKind.WeatherNow).Island;
        now.Weather = SavedWeather.Options();
        Assert.Null(WeatherPlaces.OfFace(folder));

        folder.Face.Weather = new WeatherOptions { Place = "Bern", Latitude = 46.95, Longitude = 7.45 };
        Assert.Equal("Bern", WeatherPlaces.OfFace(folder)!.Place);
        Assert.Equal(SavedWeather.Options().Place, now.Weather.Place);
        Assert.All(folder.Cells.Where(cell => cell.Island.Weather is not null && cell.Island != now), cell => Assert.False(cell.Island.Weather!.IsSet));

        Assert.Null(WeatherPlaces.OfFace(new FolderOptions()));
        var settled = WeatherPlaces.Settled(new WeatherOptions { Place = null!, UseDeviceLocation = true });
        Assert.Equal("", settled.Place);
        Assert.True(settled.IsSet);
    }

    // --- The bar ------------------------------------------------------------------------------------

    /// <summary>A forecast that is nothing like the saved one: other temperatures, other conditions, other days.</summary>
    private static WeatherCache Other()
    {
        var cache = SavedWeather.Cache();
        var forecast = cache.Forecast!;
        forecast.Current!.Temperature = -17.8;
        forecast.Current.WeatherCode = 95;
        forecast.Daily!.High = [-3.2, 105, 0.4, 9, 9, 9, 9];
        forecast.Daily.Low = [-19.6, -8, -0.4, 1, 1, 1, 1];
        forecast.Daily.WeatherCode = [95, 71, 3, 0, 0, 0, 0];
        return cache;
    }

    /// <summary>A folder wearing the weather face in <paramref name="look"/>, the face and its conditions' line each given the place.</summary>
    internal static BarItem Folder(string look, bool place = true)
    {
        var island = BarKind.FolderList.New();
        island.Id = "weather";
        var folder = island.Folder!;
        folder.Face = WeatherFace.New(look);
        var now = ListKind.WeatherNow.New();
        now.Id = "now";
        // Without a place: none at all, not the device's location a new face and line start with.
        now.Weather = place ? SavedWeather.Options() : new WeatherOptions();
        folder.Face.Weather = place ? SavedWeather.Options() : new WeatherOptions();
        folder.Put(now);
        island.Normalize();
        return island;
    }

    /// <summary>The folder made on this thread, showing <paramref name="stand"/> rather than anything fetched.</summary>
    internal static FolderIsland Make(Gfx gfx, BarItem folder, WeatherCache? stand)
    {
        FolderIsland.WeatherStand = stand;
        try
        {
            return new FolderIsland(Config.IslandKey(null, folder.Id), folder, gfx, new Config());
        }
        finally
        {
            FolderIsland.WeatherStand = null;
        }
    }

    private static IslandStyle Style(int height, uint dpi, Look look, bool vertical = false)
    {
        var config = new Config { Height = height };
        return new IslandStyle(dpi / 96f, vertical, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), look);
    }

    /// <summary>
    /// A look is as wide as what it shows now (docs\widgets.md §244): the three are in order, a
    /// forecast that reads otherwise is another width, and none yet — the dash — is no wider.
    /// </summary>
    [UiFact]
    public void EachLooksWidthIsWhatItShowsNow()
    {
        var failures = new List<string>();
        SavedWeather.AtAFixedTime(() => OnStaThread(gfx =>
        {
            foreach (var height in Heights)
            {
                foreach (var dpi in (uint[])[96, 144])
                {
                    var look = Karpo.Tests.Edit.AddListPictures.LookFor(true);
                    var style = Style(height, dpi, look);
                    var widths = new List<float>();
                    foreach (var id in Looks)
                    {
                        var sizes = new List<float>();
                        foreach (var stand in new[] { SavedWeather.Cache(), Other(), null })
                        {
                            using var island = Make(gfx, Folder(id), stand);
                            sizes.Add(island.CellSize(0, style).Width);
                        }
                        if (sizes[2] > MathF.Min(sizes[0], sizes[1]))
                        {
                            failures.Add($"{height} DIP, {dpi} DPI, {id}: {string.Join(" / ", sizes)} px, nothing read yet the widest");
                        }
                        widths.Add(sizes[0]);
                    }
                    if (!(widths[0] < widths[1] && widths[1] < widths[2]))
                    {
                        failures.Add($"{height} DIP, {dpi} DPI: simple, detailed, forecast are {string.Join(", ", widths.Select(width => width.ToString("0", CultureInfo.InvariantCulture)))} px");
                    }
                }
            }
        }));
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>
    /// Every look is drawn whole inside its cell at every height, in the dark and the light, and in
    /// the middle of it; before a place is set it says so in the simple look's room. Pictured.
    /// </summary>
    [UiFact]
    public void EachLookFitsItsCellOnOneRow()
    {
        var failures = new List<string>();
        SavedWeather.AtAFixedTime(() => OnStaThread(gfx =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var dpi in (uint[])[96, 144])
                {
                    var look = Karpo.Tests.Edit.AddListPictures.LookFor(dark);
                    var sheet = new List<(string Name, FolderIsland Island, IslandStyle Style)>();
                    try
                    {
                        foreach (var height in Heights)
                        {
                            foreach (var (id, place) in Looks.Select(id => (id, true)).Append((WeatherFace.Forecast, false)))
                            {
                                var island = Make(gfx, Folder(id, place), SavedWeather.Cache());
                                var style = Style(height, dpi, look);
                                sheet.Add(($"{id}{(place ? "" : ", no place")} {height}", island, style));
                                // The detailed look keeps the room of the widest reading between the temperature and the
                                // day's range, so a short reading leaves its room after it: centred is the simple look's.
                                failures.AddRange(Inside(island, style, $"{height} DIP, {dpi} DPI, {(dark ? "dark" : "light")}, {id}{(place ? "" : ", no place")}", centred: id != WeatherFace.Detailed));
                            }
                        }
                        foreach (var vertical in new[] { 48, 80 })
                        {
                            var island = Make(gfx, Folder(WeatherFace.Forecast), SavedWeather.Cache());
                            var style = Style(vertical, dpi, look, vertical: true);
                            sheet.Add(($"vertical {vertical}", island, style));
                            failures.AddRange(Inside(island, style, $"vertical {vertical} DIP, {dpi} DPI, {(dark ? "dark" : "light")}"));
                        }
                        Sheet(gfx, look, sheet, $"weather-faces-{dpi}-{(dark ? "dark" : "light")}.png");
                    }
                    finally
                    {
                        foreach (var item in sheet)
                        {
                            item.Island.Dispose();
                        }
                    }
                }
            }
        }));
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>Where the face's ink is drawn into its cell: inside it, and across a bar in its middle where <paramref name="centred"/>.</summary>
    private static List<string> Inside(FolderIsland island, IslandStyle style, string where, bool centred = true)
    {
        var failures = new List<string>();
        island.Forget();
        var (width, height) = island.CellSize(0, style);
        const float margin = 12f;
        (float Left, float Top, float Right, float Bottom) ink = default;
        Bitmap(
            width + (2 * margin),
            height + (2 * margin),
            dc => Shifted(dc, margin, margin, () => island.PaintCell(dc, 0, style, width, height)),
            bitmap => ink = InkOf(bitmap, (uint)MathF.Ceiling(width + (2 * margin)), (uint)MathF.Ceiling(height + (2 * margin))));
        var (left, top, right, bottom) = (ink.Left - margin, ink.Top - margin, ink.Right - margin, ink.Bottom - margin);
        if (right <= left)
        {
            failures.Add($"{where}: draws nothing");
            return failures;
        }
        if (left < -0.5f || top < -0.5f || right > width + 0.5f || bottom > height + 0.5f)
        {
            failures.Add($"{where}: draws {left:0.#},{top:0.#}-{right:0.#},{bottom:0.#} outside its {width:0.#} by {height:0.#} cell");
        }
        if (centred && !style.Vertical && MathF.Abs(((left + right) / 2) - (width / 2)) > MathF.Max(3f, 0.06f * width))
        {
            failures.Add($"{where}: its ink's middle is {((left + right) / 2) - (width / 2):+0.0;-0.0} px off the cell's");
        }
        return failures;
    }

    /// <summary>The weekdays are the region's short names, from the place's own today on.</summary>
    [Fact]
    public void TheForecastsDaysFollowTheRegionAndStartAtTheDay()
    {
        SavedWeather.AtAFixedTime(() =>
        {
            var forecast = SavedWeather.Cache().Forecast!;
            var days = WeatherFaceDrawing.NextDays(forecast, 3, Clock.Now().ToUniversalTime(), value => value);
            Assert.Equal(3, days.Count);
            var first = new DateOnly(2026, 9, 23);
            for (var at = 0; at < 3; at++)
            {
                Assert.Equal(Nls.ShortDayName(first.AddDays(at).DayOfWeek), days[at].Name);
            }
            Assert.True(days[0].Today);
            Assert.False(days[1].Today);
            Assert.All(days, day => Assert.NotEqual("", day.Glyph));
        });
        Assert.All(WeatherRow.Days7(), name => Assert.False(string.IsNullOrWhiteSpace(name)));
        Assert.Equal(7, WeatherRow.Days7().Count());
    }

    /// <summary>A forecast fetched two days ago starts at the place's today, not at the day it was fetched.</summary>
    [Fact]
    public void AStaleForecastStartsAtTodayAndNeverShowsADayGone()
    {
        SavedWeather.AtAFixedTime(() =>
        {
            var forecast = SavedWeather.Cache().Forecast!;
            var later = new DateTime(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc);
            var days = WeatherFaceDrawing.NextDays(forecast, 3, later, value => value);
            Assert.Equal(Nls.ShortDayName(new DateOnly(2026, 9, 25).DayOfWeek), days[0].Name);
            Assert.True(days[0].Today);
        });
    }

    /// <summary>What the face shows now changes with the forecast, and the face is painted again in place for it.</summary>
    [UiFact]
    public void ANewReadingIsANewReadingOfTheFace()
    {
        SavedWeather.AtAFixedTime(() => OnStaThread(gfx =>
        {
            using var one = Make(gfx, Folder(WeatherFace.Detailed), SavedWeather.Cache());
            using var two = Make(gfx, Folder(WeatherFace.Detailed), Other());
            using var none = Make(gfx, Folder(WeatherFace.Detailed, place: false), SavedWeather.Cache());
            var style = Style(48, 96, Karpo.Tests.Edit.AddListPictures.LookFor(true));
            string Reading(FolderIsland island) => FaceDrawings.Of(island.Options.Face).Reading(new FaceContext(gfx, style, island.Options, island));
            Assert.NotEqual(Reading(one), Reading(two));
            Assert.Equal("unset", Reading(none));
            Assert.Null(FaceDrawings.Of(one.Options.Face).Tooltip(new FaceContext(gfx, style, one.Options, one)));
        }));
    }

    /// <summary>The sheet: each face on the bar's card with its name over it.</summary>
    private static void Sheet(Gfx gfx, Look look, List<(string Name, FolderIsland Island, IslandStyle Style)> items, string file)
    {
        const float pad = 14f;
        const float label = 16f;
        const float most = 1500f;
        var theme = FlyoutTheme.For(look);
        var places = new List<(float X, float Y, float W, float H)>();
        var x = pad;
        var y = pad;
        var rowHeight = 0f;
        var widest = 0f;
        foreach (var (_, island, style) in items)
        {
            island.Forget();
            var (w, h) = island.CellSize(0, style);
            var slot = MathF.Max(w, 150f);
            if (x > pad && x + slot > most)
            {
                x = pad;
                y += label + rowHeight + pad;
                rowHeight = 0;
            }
            places.Add((x, y, w, h));
            rowHeight = MathF.Max(rowHeight, h);
            x += slot + pad;
            widest = MathF.Max(widest, x);
        }
        var total = y + label + rowHeight + pad;
        Bitmap(widest, total, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, widest, total), 0, PopupPaint.Ink(theme.Base));
            for (var index = 0; index < items.Count; index++)
            {
                var (name, island, style) = items[index];
                var (left, top, w, h) = places[index];
                PopupPaint.Text(dc, gfx, name, 11f, Weight.Regular, theme.TextSecondary, left, top, 200f);
                var cardTop = top + label;
                PopupPaint.Fill(dc, PopupPaint.Rect(left, cardTop, w, h), 4f * style.Scale, PopupPaint.Ink(theme.CardBackground));
                var shown = style;
                Shifted(dc, left, cardTop, () => island.PaintCell(dc, 0, shown, w, h));
            }
        }, bitmap => Save(bitmap, Path.Combine(Pictures, file)));
    }
}
