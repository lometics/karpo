// The places' lines on the kit's blocks (docs\widgets.md §401; the proposal's places mockup): the weather's
// conditions are a Hero — the condition's Feature glyph, the temperature in the Hero (28) at the text edge, the
// condition beside it, what it feels like under it — over the place, a Caption (§607: no header) — and nothing
// more at their own height; the air quality
// and the tides are a Row each, their subject the row's name and their reading the Value at the trailing
// gutter, never a Hero of their own; the sun's times are a widget each, one Row whose time reads in the Body
// (§404), and those of one place share one reckoning of its day; the earthquakes
// have a Header over their items. Each at the height it starts at, with no room it cannot fill. Drawn with
// the painting watched (PaintWatch.Styled) at 100 %, dark. Runs with KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.Weather;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Forecasts;

public sealed unsafe class PlacesAnatomyTests
{
    private const float Scale = 1f;

    /// <summary>
    /// §607: the current weather is a Hero over its place — no header: the temperature in the Hero at the text edge, the
    /// condition's Feature glyph, the condition beside it, what it feels like under it, then the place a Caption at the
    /// text edge under all of it; nothing in the Body strong.
    /// </summary>
    [UiFact]
    public void TheWeatherIsAHeroOverItsPlace() => Run(gfx =>
    {
        using var tile = Line<WeatherPartTile>(gfx, ListKind.WeatherNow, line => line.Source = SavedWeather.Source());
        var (style, edges) = Look();
        var tall = ((IListContent)tile).ListHeight(Width, style);
        // A hero with two captions: its air, its line and two captions'.
        var most = LineHero.Height(gfx, style, 2) + 1;
        Assert.True(tall <= most, $"the conditions measure {tall:0} px, more than a hero with two captions ({most:0})");
        var drawn = Draw(gfx, tile, tall, style);
        var hero = Assert.Single(drawn, piece => piece.What == PaintedKind.Text && Dip(piece) == LineType.HeroDip);
        Assert.True(AtTextEdge(hero, edges), $"the temperature starts at {hero.Box.left:0}, the text edge is {edges.Text:0}");
        Assert.DoesNotContain(drawn, piece => piece.What == PaintedKind.Text && piece.Box.bottom <= hero.Box.top + 0.5f);
        Assert.Contains(drawn, piece => piece.What == PaintedKind.Glyph && Dip(piece) == LineGlyph.FeatureDip);
        Assert.DoesNotContain(drawn, piece => piece.What == PaintedKind.Text && piece.Weight >= 600 && Dip(piece) < LineType.HeroDip);
        // The condition beside the temperature, on its band.
        Assert.Contains(drawn, piece => piece.What == PaintedKind.Text && Dip(piece) == 14 && piece.Box.left > hero.Box.right && piece.Box.top < hero.Box.bottom && piece.Box.bottom > hero.Box.top);
        // The place: the last line, a Caption at the text edge.
        var words = drawn.Where(piece => piece.What == PaintedKind.Text).ToList();
        var last = words.Max(piece => piece.Box.top);
        var foot = words.Where(piece => piece.Box.top >= last - 0.5f).MinBy(piece => piece.Box.left)!;
        Assert.Equal(LineType.CaptionDip, Dip(foot));
        Assert.True(AtTextEdge(foot, edges), $"the place starts at {foot.Box.left:0}, the text edge is {edges.Text:0}");
    });
    [UiFact]
    public void TheAirQualityIsARow() => Run(gfx =>
    {
        var (style, edges) = Look();
        using var tile = Line<AirQualityTile>(gfx, ListKind.AirQuality, line => line.Source = SavedAir.Source(SavedAir.Cache()), Compact);
        var tall = ((IListContent)(object)tile).ListHeight(Width, style);
        Assert.Equal(LineRow.Height(style, caption: true), tall, 0.5f);
        var drawn = Draw(gfx, tile, tall, style);
        Assert.DoesNotContain(drawn, piece => piece.What == PaintedKind.Text && Dip(piece) > LineType.ValueDip);
        Assert.Contains(drawn, piece => piece.What == PaintedKind.Text && Dip(piece) == 14 && AtTextEdge(piece, edges));
        Assert.Contains(drawn, piece => piece.What == PaintedKind.Text && Dip(piece) == LineType.ValueDip && piece.Weight >= 600);
        Assert.Contains(drawn, piece => piece.What == PaintedKind.Glyph && InColumn(piece, edges));
        // §607: its place a Caption under its name, at the text edge.
        Assert.Contains(drawn, piece => piece.What == PaintedKind.Text && Dip(piece) == LineType.CaptionDip && AtTextEdge(piece, edges));

        // Out of reach: the one Status row, one row tall.
        using var away = Line<AirQualityTile>(gfx, ListKind.AirQuality, line => line.Source = SavedAir.Source(null, "Open-Meteo can't be reached"), Compact);
        Assert.Equal(LineRow.Height(style, caption: true), ((IListContent)(object)away).ListHeight(Width, style), 0.5f);
    });

    /// <summary>§603: the tides now at Full is its Row with its caption, the next turn under the name; its curve is the tide graph's.</summary>
    [UiFact]
    public void TheTidesNowIsARowWithItsCaption() => Run(gfx =>
    {
        var (style, edges) = Look();
        using var tile = Line<TidesTile>(gfx, ListKind.Tides, line =>
        {
            line.Source = SavedTides.Source(SavedTides.Cache());
            line.Imperial = false;
        });
        var tall = ((IListContent)(object)tile).ListHeight(Width, style);
        var drawn = Draw(gfx, tile, tall, style);
        Assert.DoesNotContain(drawn, piece => piece.What == PaintedKind.Text && Dip(piece) > LineType.ValueDip);
        var row = LineRow.Height(style, true);
        var head = drawn.Where(piece => piece.Box.bottom <= row + 0.5f).ToList();
        Assert.Contains(head, piece => piece.What == PaintedKind.Text && Dip(piece) == 14 && piece.Weight < 600 && AtTextEdge(piece, edges));
        Assert.Contains(head, piece => piece.What == PaintedKind.Text && Dip(piece) == LineType.CaptionDip && AtTextEdge(piece, edges));
        var reading = Assert.Single(head, piece => piece.What == PaintedKind.Text && Dip(piece) == LineType.ValueDip);
        // On the trailing gutter: no refresh button keeps room past it (§451).
        Assert.True(MathF.Abs(reading.Box.right - (Width - edges.Gutter)) <= 3,
            $"the height now ends at {reading.Box.right:0}, the trailing gutter at {Width - edges.Gutter:0}");
        Assert.Equal(row, tall, 0.5f);
    });

    /// <summary>The sun's times, each a widget of its own (§404).</summary>
    private static readonly ListKind[] SunTimes = [ListKind.Sunrise, ListKind.Sunset, ListKind.Daylight, ListKind.SolarNoon, ListKind.Dawn, ListKind.Dusk];

    /// <summary>
    /// §404: each of the sun's times is one Row — its glyph in the leading column, its name at the text edge, its
    /// note as a caption under it, its time in the Body on the trailing gutter — and as tall as that row where it
    /// starts, automatic: 52 DIP with a note, 40 without.
    /// </summary>
    [UiFact]
    public void EachSunTimeIsOneRow() => Run(gfx =>
    {
        var (style, edges) = Look();
        foreach (var kind in SunTimes)
        {
            using var tile = Line<SunTile>(gfx, kind, line =>
            {
                line.Stand = (47.3769, 8.5417);
                line.Zone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
            });
            var tall = ((IListContent)(object)tile).ListHeight(Width, style);
            var drawn = Draw(gfx, tile, tall, style);
            var caption = drawn.Any(piece => piece.What == PaintedKind.Text && Dip(piece) == LineType.CaptionDip);
            Assert.True(MathF.Abs(tall - LineRow.Height(style, caption)) <= 0.5f, $"{kind}: {tall:0} px, its row {LineRow.Height(style, caption):0}");
            Assert.Single(drawn, piece => piece.What == PaintedKind.Glyph && InColumn(piece, edges));
            var name = Assert.Single(drawn, piece => piece.What == PaintedKind.Text && Dip(piece) == 14 && piece.Box.left < Width / 2);
            Assert.True(AtTextEdge(name, edges), $"{kind}: its name starts at {name.Box.left:0}, the text edge is {edges.Text:0}");
            var time = Assert.Single(drawn, piece => piece.What == PaintedKind.Text && Dip(piece) == 14 && piece.Box.left >= Width / 2);
            Assert.True(MathF.Abs(time.Box.right - (Width - edges.Gutter)) <= 3, $"{kind}: its time ends at {time.Box.right:0}, the gutter at {Width - edges.Gutter:0}");
            Assert.DoesNotContain(drawn, piece => piece.What == PaintedKind.Text && piece.Weight >= 600);
        }
    });

    /// <summary>
    /// §404: the times of one place share one reckoning of its day — worked out once a minute for the place, not
    /// once a widget — and a time of another place has its own.
    /// </summary>
    [UiFact]
    public void TheSunsTimesOfOnePlaceShareOneDay() => Run(gfx =>
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        SunTile Of(ListKind kind, (double, double) where) => Line<SunTile>(gfx, kind, line =>
        {
            line.Stand = where;
            line.Zone = zone;
        });
        using var sunrise = Of(ListKind.Sunrise, (47.3769, 8.5417));
        using var daylight = Of(ListKind.Daylight, (47.3769, 8.5417));
        using var arc = Of(ListKind.SunArc, (47.3769, 8.5417));
        using var quito = Of(ListKind.Daylight, (-0.18, -78.47));
        var day = sunrise.Read();
        Assert.NotNull(day);
        Assert.Same(day, daylight.Read());
        Assert.Same(day, arc.Read());
        Assert.NotSame(day, quito.Read());
        Assert.Equal(-0.18, quito.Read()!.Latitude);
    });

    [UiFact]
    public void TheEarthquakesAreAHeaderOverTheirItems() => Run(gfx =>
    {
        var (style, edges) = Look();
        using var tile = Line<QuakesLine>(gfx, ListKind.Quakes, line => line.Source = () => QuakePictures.Week(), item =>
        {
            item.Weather = SavedQuakes.Zurich();
            item.Weather.Quakes = new Karpo.Core.QuakeOptions { Radius = 2000, Magnitude = 2.5 };
        });
        var tall = ((IListContent)tile).ListHeight(Width, style);
        var drawn = Draw(gfx, tile, tall, style);
        var row = LineRow.Height(style, false);
        var head = drawn.Where(piece => piece.Box.bottom <= row + 0.5f).ToList();
        Assert.Contains(head, piece => piece.What == PaintedKind.Glyph && InColumn(piece, edges));
        Assert.Contains(head, piece => piece.What == PaintedKind.Text && Dip(piece) == 14 && piece.Weight >= 600 && AtTextEdge(piece, edges));
        Assert.Contains(head, piece => piece.What == PaintedKind.Text && Dip(piece) == LineType.CaptionDip);
    });

    /// <summary>
    /// §404 (the person, 2026-10-29: "earthquakes should show if there are none"): a quiet week within its radius is
    /// said — the header ("Earthquakes", where and how far) over the one Status, "No earthquakes in the past 7 days",
    /// its glyph in the leading column and its words at the text edge; on one row the Status alone. Never a blank line.
    /// </summary>
    [UiFact]
    public void TheEarthquakesSayWhenThereAreNone() => Run(gfx =>
    {
        var (style, edges) = Look();
        using var tile = Line<QuakesLine>(gfx, ListKind.Quakes,
            line => line.Source = () => new QuakeHeld { Quakes = [], FetchedUtc = Clock.Now().ToUniversalTime().AddMinutes(-1) },
            item =>
            {
                item.Weather = SavedQuakes.Zurich();
                item.Weather.Quakes = new Karpo.Core.QuakeOptions { Radius = 1000, Magnitude = 2.5 };
            });
        var reading = tile.Read();
        Assert.Empty(reading.Rows);
        Assert.Equal(S.DockQuakesNoneWeek, QuakesLine.Why(reading));
        var tall = ((IListContent)tile).ListHeight(Width, style);
        var row = LineRow.Height(style, false);
        Assert.Equal(2 * row, tall, 0.5f);
        var plan = tile.Plan(PopupPaint.Rect(0, 0, Width, tall), style, reading);
        Assert.True(!plan.One && plan.Headed && plan.Rows.Count == 0, "a header over the Status");
        var drawn = Draw(gfx, tile, tall, style);
        var under = drawn.Where(piece => piece.Box.top >= row - 0.5f).ToList();
        Assert.Contains(under, piece => piece.What == PaintedKind.Glyph && InColumn(piece, edges));
        Assert.Contains(under, piece => piece.What == PaintedKind.Text && Dip(piece) == 14 && AtTextEdge(piece, edges));
        Assert.True(tile.Plan(PopupPaint.Rect(0, 0, Width, row), style, reading).One);
        Assert.Contains(Draw(gfx, tile, row, style), piece => piece.What == PaintedKind.Text && AtTextEdge(piece, edges));
        foreach (var dark in new[] { true, false })
        {
            ListLinesTests.Sheet(gfx, tile, ListKind.Quakes, 1.5f, ListFormPictures.LookFor(dark), Path.Combine(Pictures, $"quakes-none-{(dark ? "dark" : "light")}.png"));
        }
    });

    private static float Width => FolderFlyout.ListWidth(Scale);

    /// <summary>A line at its Compact size (§590): the air quality's row with its caption.</summary>
    private static void Compact(Karpo.Core.CellItem item)
    {
        item.Size = LineSize.Compact;
        item.Normalize();
    }

    private static void Run(Action<Gfx> body) =>
        ListGutterTests.StoodIn<Gfx>(SavedWeather.Fixed, OnStaThread, body);

    private static (TileStyle Style, (float Gutter, float Icon, float Text) Edges) Look()
    {
        var look = ListFormPictures.LookFor(dark: true);
        var style = FolderFlyout.StyleFor(Scale, look, FlyoutTheme.For(look));
        var textPx = style.Text(14);
        return (style, (FolderFlyout.RowInset(textPx), FolderFlyout.RowIcon(textPx), FolderFlyout.RowText(textPx)));
    }

    private static T Line<T>(Gfx gfx, ListKind kind, Action<T> set, Action<Karpo.Core.CellItem>? item = null)
        where T : CellIsland
    {
        var made = ListGutterTests.Made(kind);
        item?.Invoke(made);
        var tile = (T)Karpo.Dock.Islands.CreateLine(Karpo.Core.Config.IslandKey(ListFormPictures.Owner, made.Id), made, kind, gfx, new Karpo.Core.Config())!;
        set(tile);
        return tile;
    }

    private static float Dip(PaintedStyle piece) => MathF.Round(piece.Px / Scale, 1);

    private static bool AtTextEdge(PaintedStyle piece, (float Gutter, float Icon, float Text) edges) => MathF.Abs(piece.Box.left - edges.Text) <= 1.5f;

    private static bool InColumn(PaintedStyle piece, (float Gutter, float Icon, float Text) edges)
    {
        var middle = (piece.Box.left + piece.Box.right) / 2;
        return middle >= edges.Gutter - 1.5f && middle <= edges.Gutter + edges.Icon + 1.5f;
    }

    /// <summary><paramref name="tile"/> drawn alone into a box <paramref name="height"/> px tall, its painting watched, in its own px.</summary>
    private static List<PaintedStyle> Draw(Gfx gfx, CellIsland tile, float height, in TileStyle style)
    {
        var drawn = new List<PaintedStyle>();
        var shown = style;
        try
        {
            Bitmap(Width, height, dc =>
            {
                var device = (nint)dc;
                PaintWatch.Styled = piece =>
                {
                    D2D_MATRIX_3X2_F at;
                    ((ID2D1DeviceContext*)device)->GetTransform(&at);
                    var placed = new D2D_RECT_F(
                        (piece.Box.left * at._11) + (piece.Box.top * at._21) + at._31,
                        (piece.Box.left * at._12) + (piece.Box.top * at._22) + at._32,
                        (piece.Box.right * at._11) + (piece.Box.bottom * at._21) + at._31,
                        (piece.Box.right * at._12) + (piece.Box.bottom * at._22) + at._32);
                    var grown = MathF.Abs(at._22) > 0 ? MathF.Abs(at._22) : 1;
                    drawn.Add(piece with { Box = placed, Px = piece.Px * grown, Radius = piece.Radius * grown });
                };
                tile.PaintOn(dc, PopupPaint.Rect(0, 0, Width, height), shown, WidgetSurface.List);
            }, _ => { });
        }
        finally
        {
            PaintWatch.Styled = null;
        }
        return [.. drawn.Where(piece => piece.Box.bottom > 0 && piece.Box.top < height)];
    }
}
