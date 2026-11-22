// §590: the weather's and the air's split widgets as they draw (docs\widgets.md §590). The person, 2026-11-09: "these
// widgets should be broken down into smaller ones that always show the same amount of data ... those widgets then
// should also not have the size given in rows but just a few different sizes that make sense".
//
// So each kind says the same at Compact and at Full — the same words and figures, laid out smaller or larger —, a box
// taller than its size makes it say nothing more (nothing shows progressively), and its height is its size's whatever
// the forecast is. Drawn on the gutter tests' stand-ins (ListGutterTests.StoodIn): a frozen clock, the saved forecast
// and the saved air (Zürich, a fair day, its pollen in).
//
// Pictures (KARPO_PNG_DIR): weather-split-weather-<dark|light>.png — the current weather, the hourly forecast's Graph
// and List and the daily forecast's List and Graph, each a folder of its Compact over its Full, at 150 %;
// weather-split-air-<dark|light>.png the air quality's four the same; weather-split-*-text225-dark.png at 225 % text.
// Needs Direct2D: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.Weather;
using Karpo.Tests.Core;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

public sealed unsafe class WeatherSplitLinesTests
{
    private static readonly LineSize[] Sizes = [LineSize.Compact, LineSize.Full];

    /// <summary>A line of <paramref name="kind"/> with its place set, in <paramref name="size"/>.</summary>
    private static CellItem Made(ListKind kind, LineSize size)
    {
        var line = ListGutterTests.Made(kind);
        line.Size = size;
        line.Normalize();
        return line;
    }

    /// <summary>What a line says drawn into <paramref name="width"/> × <paramref name="height"/>: every word and figure, one by one.</summary>
    private static HashSet<string> Said(CellIsland line, float width, float height, in TileStyle style)
    {
        var said = new List<string>();
        var box = PopupPaint.Rect(0, 0, width, height);
        var drawn = style;
        PaintWatch.Said = (text, _) => said.Add(text);
        try
        {
            Bitmap(width, height, dc => line.PaintOn(dc, box, drawn, WidgetSurface.List), _ => { });
        }
        finally
        {
            PaintWatch.Said = null;
        }
        // One word or figure a token: a caption joined with " · " says what the larger size says on lines of their own.
        // Segoe Fluent Icons' glyphs (the private use area) are pictures, not words.
        return [.. said.SelectMany(text => text.Split([" · ", " ", " ", " "], StringSplitOptions.RemoveEmptyEntries))
            .Where(word => !word.All(c => c is >= '' and <= ''))];
    }

    /// <summary>
    /// Every split kind at Compact and at Full says the same words and figures — the current weather's age, after the
    /// place under its Full hero (§607), the one thing a size leaves out —, and drawn twice as tall says nothing more.
    /// </summary>
    [UiFact]
    public void EachSizeSaysTheSameAndATallerLineNoMore()
    {
        var failures = new List<string>();
        ListGutterTests.StoodIn<Gfx>(SavedWeather.Fixed, OnStaThread, gfx =>
        {
            foreach (var kind in LineSizeTests.Split)
            {
                var said = new Dictionary<LineSize, HashSet<string>>();
                string[] age = [];
                foreach (var size in Sizes)
                {
                    var folder = ListFormPictures.Folder(gfx, ListGutterTests.Before, Made(kind, size));
                    try
                    {
                        var look = ListFormPictures.LookFor(dark: true);
                        var (_, plan) = ListFormPictures.Plan(folder, look, 1f);
                        var card = plan.Cards[0];
                        var (width, height) = (card.right - card.left, card.bottom - card.top);
                        var style = FolderFlyout.StyleFor(1f, look, FlyoutTheme.For(look));
                        var line = (CellIsland)folder.Tiles[0].Island;
                        var at = Said(line, width, height, style);
                        var taller = Said(line, width, height * 2, style);
                        if (!at.SetEquals(taller))
                        {
                            failures.Add($"{kind} {size}: twice as tall it says {string.Join(", ", taller.Except(at))} more and {string.Join(", ", at.Except(taller))} less");
                        }
                        if (at.Count == 0)
                        {
                            failures.Add($"{kind} {size}: says nothing");
                        }
                        if (line is WeatherPartTile now && size == LineSize.Full)
                        {
                            age = [.. now.LayOut(width, height, style).Where(piece => piece.Name == WeatherLineTile.UpdatedName).SelectMany(piece => piece.Text.Split(' '))];
                        }
                        said[size] = at;
                    }
                    finally
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                }
                var onlyFull = said[LineSize.Full].Except(said[LineSize.Compact]).Except(age).ToList();
                var onlyCompact = said[LineSize.Compact].Except(said[LineSize.Full]).ToList();
                if (onlyFull.Count > 0 || onlyCompact.Count > 0)
                {
                    failures.Add($"{kind}: Full alone says {Shown(onlyFull)}; Compact alone {Shown(onlyCompact)}");
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>Words as a failure says them: each in brackets, its characters' code points where it has an unprintable one.</summary>
    private static string Shown(IEnumerable<string> words) => string.Join(", ", words.Select(word =>
        word.All(c => c > ' ' && !char.IsControl(c)) ? $"[{word}]" : $"[{string.Join(" ", word.Select(c => $"U+{(int)c:X4}"))}]"));

    /// <summary>
    /// A split line's height is its size's, whatever its forecast says — a forecast, none yet or none to be had —, at
    /// 100 % and 225 % text; and Compact is the smaller.
    /// </summary>
    [UiFact]
    public void ItsHeightIsItsSizesWhateverTheForecastSays()
    {
        var failures = new List<string>();
        ListGutterTests.StoodIn<Gfx>(SavedWeather.Fixed, OnStaThread, gfx =>
        {
            foreach (var textScale in ListFormPictures.TextScales)
            {
                var look = ListFormPictures.LookFor(dark: true, textScale);
                var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
                var width = FolderFlyout.ListWidth(1.5f);
                foreach (var kind in LineSizeTests.Split)
                {
                    var heights = new List<float>();
                    foreach (var size in Sizes)
                    {
                        var item = Made(kind, size);
                        using var shown = (CellIsland)Karpo.Dock.Islands.CreateLine(Config.IslandKey(null, "shown"), item, kind, gfx, new Config())!;
                        using var failed = (CellIsland)Karpo.Dock.Islands.CreateLine(Config.IslandKey(null, "failed"), Made(kind, size), kind, gfx, new Config())!;
                        switch ((shown, failed))
                        {
                            case (WeatherLineTile weather, WeatherLineTile none):
                                weather.Source = SavedWeather.Source();
                                none.Source = new WeatherSource { Read = () => (null, "offline", false), Ask = () => false };
                                break;
                            case (AirQualityTile air, AirQualityTile none):
                                air.Source = SavedAir.Source(SavedAir.Cache(european: 46, us: 54, pollen: "12.0"));
                                none.Source = SavedAir.Source(null, "offline");
                                break;
                        }
                        var full = ((IListContent)shown).ListHeight(width, style);
                        var empty = ((IListContent)failed).ListHeight(width, style);
                        if (full != empty)
                        {
                            failures.Add($"{kind} {size} at {textScale * 100:0} % text: {full:0} px with a forecast, {empty:0} without");
                        }
                        heights.Add(full);
                    }
                    if (!(heights[0] < heights[1]))
                    {
                        failures.Add($"{kind} at {textScale * 100:0} % text: Compact {heights[0]:0} px, Full {heights[1]:0}");
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// §607 (the person, 2026-11-09: "the weather widget should not show the location all the way at the top. it should
    /// display it smaller somewhere else"): every weather and air line that says its place says it once, at the
    /// Caption's size and never first — something larger stands over it —, at both sizes and at every text scale from
    /// 100 to 225 %; and nothing it draws leaves its line.
    /// </summary>
    [UiFact]
    public void ThePlaceIsACaptionUnderWhatTheLineSays()
    {
        var failures = new List<string>();
        ListGutterTests.StoodIn<Gfx>(SavedWeather.Fixed, OnStaThread, gfx =>
        {
            foreach (var textScale in new[] { 1f, 1.5f, 2.25f })
            {
                foreach (var kind in new[] { ListKind.WeatherNow, ListKind.AirQuality })
                {
                    foreach (var size in Sizes)
                    {
                        var folder = ListFormPictures.Folder(gfx, ListGutterTests.Before, Made(kind, size));
                        try
                        {
                            var look = ListFormPictures.LookFor(dark: true, textScale);
                            var (_, plan) = ListFormPictures.Plan(folder, look, 1.5f);
                            var card = plan.Cards[0];
                            var (width, height) = (card.right - card.left, card.bottom - card.top);
                            var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
                            var line = (CellIsland)folder.Tiles[0].Island;
                            var said = new List<(string Text, D2D_RECT_F Box)>();
                            PaintWatch.Said = (text, box) => said.Add((text, box));
                            try
                            {
                                Bitmap(width, height, dc => line.PaintOn(dc, PopupPaint.Rect(0, 0, width, height), style, WidgetSurface.List), _ => { });
                            }
                            finally
                            {
                                PaintWatch.Said = null;
                            }
                            var at = $"{kind} {size} at {textScale * 100:0} % text";
                            var places = said.Where(each => each.Text.StartsWith("Zürich", StringComparison.Ordinal)).ToList();
                            if (places.Count != 1)
                            {
                                failures.Add($"{at}: the place said {places.Count} times");
                                continue;
                            }
                            var place = places[0].Box;
                            var caption = TilePaint.LineHeight(gfx, style.Text(LineType.CaptionDip));
                            if (MathF.Abs((place.bottom - place.top) - caption) > 1)
                            {
                                failures.Add($"{at}: the place is {place.bottom - place.top:0} px tall, a caption {caption:0}");
                            }
                            if (!said.Any(each => each.Box.bottom <= place.top + 0.5f && each.Box.bottom - each.Box.top > caption + 1))
                            {
                                failures.Add($"{at}: nothing larger stands over the place");
                            }
                            foreach (var (text, box) in said.Where(each => each.Box.left < -0.5f || each.Box.top < -0.5f || each.Box.right > width + 0.5f || each.Box.bottom > height + 0.5f))
                            {
                                failures.Add($"{at}: \"{text}\" at ({box.left:0}, {box.top:0}, {box.right:0}, {box.bottom:0}) leaves its {width:0} × {height:0} line");
                            }
                        }
                        finally
                        {
                            folder.Closed();
                            folder.Dispose();
                        }
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>The pictures: each split widget a folder of its Compact over its Full, its styles side by side (see the top of the file).</summary>
    [UiFact]
    public void WeatherSplitPictures()
    {
        (string Caption, ListKind Kind)[] weather =
        [
            (S.CoreWidgetWeatherNowTitle, ListKind.WeatherNow),
            ($"{S.CoreStyledHourlyForecastTitle} · {S.CoreWidgetStyleHourlyForecastGraph}", ListKind.WeatherHours),
            ($"{S.CoreStyledHourlyForecastTitle} · {S.CoreWidgetStyleHourlyForecastList}", ListKind.WeatherHoursList),
            ($"{S.CoreStyledDailyForecastTitle} · {S.CoreWidgetStyleDailyForecastList}", ListKind.WeatherDays),
            ($"{S.CoreStyledDailyForecastTitle} · {S.CoreWidgetStyleDailyForecastGraph}", ListKind.WeatherDaysGraph),
        ];
        (string Caption, ListKind Kind)[] air =
        [
            (S.CoreWidgetAirQualityTitle, ListKind.AirQuality),
            (S.CoreWidgetAirPollutantsTitle, ListKind.AirPollutants),
            (S.CoreWidgetAirPollenTitle, ListKind.AirPollen),
            (S.CoreWidgetAirForecastTitle, ListKind.AirForecast),
        ];
        ListGutterTests.StoodIn<Gfx>(SavedWeather.Fixed, OnStaThread, gfx =>
        {
            foreach (var (name, kinds) in new[] { ("weather", weather), ("air", air) })
            {
                foreach (var (dark, textScale) in new[] { (true, 1f), (false, 1f), (true, 2.25f) })
                {
                    var look = ListFormPictures.LookFor(dark, textScale);
                    var folders = kinds.Select(each => (each.Caption, Folder: ListFormPictures.Folder(gfx, ListGutterTests.Before, Made(each.Kind, LineSize.Compact), Made(each.Kind, LineSize.Full)))).ToList();
                    try
                    {
                        var file = $"weather-split-{name}{(textScale > 1 ? "-text225" : "")}-{(dark ? "dark" : "light")}.png";
                        ListFormPictures.Sheet(gfx, file, 1.5f,
                            [.. folders.Select(each => ($"{each.Caption}: Compact over Full, {textScale * 100:0} % text", look, each.Folder, ListFormPictures.Plan(each.Folder, look, 1.5f).Plan))]);
                    }
                    finally
                    {
                        foreach (var (_, folder) in folders)
                        {
                            folder.Closed();
                            folder.Dispose();
                        }
                    }
                }
            }
        });
    }
}
