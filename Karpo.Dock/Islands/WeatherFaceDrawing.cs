// The weather face (Karpo.Core\FolderFaces.cs, docs\widgets.md §80): what the weather entry on the
// bar drew, as a folder's face — the weather preset's, and any folder's that holds a weather line.
// The entry went with the preset (2026-10-09); its looks came with it (WeatherFace):
//
//   simple    the conditions' glyph and the temperature: what the entry always was
//   detailed  the same, and today's high and low after the temperature in the quiet ink
//   forecast  today and the next two days: the weekday, the glyph, and "high/low"
//   conditions  Windows' Widgets button (2026-10-13): the glyph, then the temperature
//             over the conditions in words, two lines at the largest of the text's sizes the card
//             holds two of, as the date-and-time face's; the simple look where the bar is too thin
//
// The place is the face's own (FolderFace.Weather), as each weather line has its own: nothing is
// shared across a folder (2026-10-10). The forecast is the one the store keeps for it, which the
// folder reads as a user of its own (FolderIsland.Weather), so a face and a line of one place are
// one fetch.
//
// The sizes and the boxes come from one plan (WeatherRow), made from the settings and the bar's
// numbers and from templates of text: each piece's box is the widest it can say (the scale's widest
// reading, the region's widest weekday), past which it is cut. Across a bar the face is as wide as
// its pieces read now (§244): a 9° is narrower than a -10°, and the bar is laid out again as the
// weather changes.
//
// A forecast day is its weekday, its glyph and "18°/8°" written as one pair — the high in the text's
// ink, the low in the quiet one —, the days twice the gap inside a day apart, so three days read as
// three groups rather than a run of evenly spaced pieces.
//
// Down a bar on its side, and before a place is set, the weather is its simple self whichever look
// is chosen: the glyph over the temperature, or the pin and "Set a place". A reading past due draws
// its temperature in the quiet ink, as the entry's did.

using System.Globalization;
using Karpo.Core;
using Karpo.Dock.Native;
using Karpo.Dock.Weather;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>
/// What a weather look on a horizontal bar is made of, in px: the pieces' sizes and their boxes'
/// widths along the bar. The face's size is <see cref="Content"/>, and the drawing cuts each piece
/// to its box and stands what it reads now in the middle of that room, so it never draws past it.
/// </summary>
internal readonly record struct WeatherRow
{
    /// <summary>The look this plan is for (<see cref="WeatherFace"/>).</summary>
    public required string Look { get; init; }

    /// <summary>The size of the text.</summary>
    public required float Px { get; init; }

    /// <summary>The size of the quieter text: the high and the low of the detailed look.</summary>
    public required float SmallPx { get; init; }

    /// <summary>The height of a line of <see cref="Px"/> text.</summary>
    public required float TextHeight { get; init; }

    /// <summary>The height of a line of <see cref="SmallPx"/> text.</summary>
    public required float SmallHeight { get; init; }

    /// <summary>The size of the conditions' glyph.</summary>
    public required float Glyph { get; init; }

    /// <summary>The room between the pieces of a group.</summary>
    public required float Gap { get; init; }

    /// <summary>The room between two days of the forecast.</summary>
    public required float Between { get; init; }

    /// <summary>The temperature's box (simple, detailed), or what a simple face says before a place is set.</summary>
    public required float Figure { get; init; }

    /// <summary>The high and low, "24°/14°", in one box: the detailed look's in the smaller text, a forecast day's in the text's own.</summary>
    public required float Range { get; init; }

    /// <summary>The forecast's weekday box: the widest weekday the region has.</summary>
    public required float Name { get; init; }

    /// <summary>conditions: the words' box, the widest the language has for any weather, cut as a long message is.</summary>
    public float Words { get; init; }

    /// <summary>One day of the forecast: its weekday, glyph, and its high and low.</summary>
    public float Day => Name + Gap + Glyph + Gap + Range;

    /// <summary>How many days the forecast look shows.</summary>
    public const int Days = 3;

    /// <summary>The pieces' width along the bar.</summary>
    public float Content => Look switch
    {
        WeatherFace.Forecast => (Days * Day) + ((Days - 1) * Between),
        WeatherFace.Detailed => Glyph + Gap + Figure + Gap + Range,
        WeatherFace.Conditions => Glyph + Gap + MathF.Max(Figure, Words),
        _ => Glyph + (Figure > 0 ? Gap + Figure : 0),
    };

    /// <summary>
    /// The plan for <paramref name="look"/> on a horizontal bar. A look that needs a place is the
    /// simple look until one is set (<paramref name="set"/>).
    /// </summary>
    public static WeatherRow Of(Gfx gfx, in IslandStyle style, string look, bool set)
    {
        var fit = new Fit(style);
        if (!set || look is not (WeatherFace.Detailed or WeatherFace.Forecast or WeatherFace.Conditions))
        {
            look = WeatherFace.Simple;
        }
        if (look == WeatherFace.Conditions && Conditions(gfx, style, fit) is { } two)
        {
            return two;
        }
        if (look == WeatherFace.Conditions)
        {
            look = WeatherFace.Simple;
        }
        var px = fit.Primary;
        var reading = set ? WeatherFaceDrawing.Warmest : S.WidgetWeatherSetLocation;
        var (figure, height) = TextLine.Template(gfx, reading, px, Weight.Regular);
        var glyph = WeatherFaceDrawing.GlyphPx(style, fit, height);
        var small = look == WeatherFace.Detailed ? fit.Small : px;
        var (_, smallHeight) = TextLine.Template(gfx, reading, small, Weight.Regular);
        var range = 0f;
        var name = 0f;
        if (look is WeatherFace.Detailed or WeatherFace.Forecast)
        {
            range = TextLine.Template(gfx, $"{reading}/{reading}", small, Weight.Regular).Width;
        }
        if (look == WeatherFace.Forecast)
        {
            name = Days7().Max(day => TextLine.Template(gfx, day, px, Weight.Regular).Width);
        }
        return new WeatherRow
        {
            Look = look,
            Px = px,
            SmallPx = small,
            TextHeight = height,
            SmallHeight = smallHeight,
            Glyph = glyph,
            Gap = fit.Gap,
            Between = 2 * fit.Gap,
            Figure = MathF.Min(figure, WeatherFaceDrawing.Widest(fit)),
            Range = range,
            Name = name,
        };
    }

    /// <summary>
    /// The conditions look's plan: the largest of the text's sizes at which two lines fit the card's
    /// thickness with a pixel to spare at each edge, as the date-and-time face finds its own; null
    /// where none does.
    /// </summary>
    private static WeatherRow? Conditions(Gfx gfx, in IslandStyle style, in Fit fit)
    {
        var across = MathF.Max(1, style.Cell - (2 * MathF.Max(1, fit.Px(1))));
        foreach (var px in (ReadOnlySpan<float>)[fit.Primary, fit.Secondary, fit.Small])
        {
            var (figure, height) = TextLine.Template(gfx, WeatherFaceDrawing.Warmest, px, Weight.Regular);
            if (2 * height > across)
            {
                continue;
            }
            var words = WeatherCodes.Conditions.Max(text => TextLine.Template(gfx, text, px, Weight.Regular).Width);
            return new WeatherRow
            {
                Look = WeatherFace.Conditions,
                Px = px,
                SmallPx = px,
                TextHeight = height,
                SmallHeight = height,
                Glyph = WeatherFaceDrawing.GlyphPx(style, fit, height),
                Gap = fit.Gap,
                Between = 2 * fit.Gap,
                Figure = figure,
                Range = 0,
                Name = 0,
                Words = MathF.Ceiling(MathF.Min(words, WeatherFaceDrawing.Widest(fit))),
            };
        }
        return null;
    }

    /// <summary>The seven weekdays in the region's short names, as the forecast writes them.</summary>
    internal static IEnumerable<string> Days7() =>
        Enumerable.Range(0, 7).Select(day => Panels.Nls.ShortDayName((DayOfWeek)day));
}

/// <summary>The weather: the conditions' glyph and the temperature, in one of three looks.</summary>
internal sealed unsafe class WeatherFaceDrawing : IFaceDrawing
{
    public string Id => FolderFaces.WeatherId;

    public bool Seconds(FolderFace face) => false;

    /// <summary>
    /// The widest a temperature plausibly reads in the scale it is shown in, which is a setting, so
    /// the face keeps its size whatever the weather does: Fahrenheit runs to three figures, Celsius
    /// to two and a sign. A figure is wider than the sign, so three figures is the wider shape.
    /// </summary>
    internal static string Warmest => WeatherUnits.Fahrenheit ? "188°" : "-88°";

    /// <summary>A long message is cut here.</summary>
    internal static float Widest(in Fit fit) => 140 * fit.Scale * fit.TextScale;

    /// <summary>
    /// The glyph's size beside a reading <paramref name="firstHeight"/> px tall: about as tall as the
    /// text, larger on a thick bar, and never more than three fifths of the cell.
    /// </summary>
    internal static float GlyphPx(in IslandStyle style, in Fit fit, float firstHeight)
    {
        var tall = style.Vertical
            ? style.Thickness * 0.5f
            : MathF.Max(firstHeight, style.Thickness * 0.5f);
        return MathF.Min(fit.GlyphFor(tall), fit.Glyph(0.6f));
    }

    /// <summary>Whether the face has a place set for its weather.</summary>
    private static bool Set(in FaceContext context) => WeatherPlaces.OfFace(context.Options) is not null;

    /// <summary>Down a vertical bar: the glyph over the reading, the reading smaller where the bar is too narrow for it.</summary>
    private static (float Glyph, float Px, float Height) Stacked(in FaceContext context)
    {
        var fit = context.Fit;
        var set = Set(context);
        var first = set ? Warmest : "";
        if (first.Length == 0)
        {
            return (GlyphPx(context.Style, fit, 0f), fit.Primary, 0f);
        }
        // The largest size the widest reading fits the bar's room at, the room a line has there.
        var (px, _, height) = VerticalFit.Across(context.Gfx, first, VerticalFit.Room(fit), Weight.Regular, VerticalFit.Ladder(fit));
        return (GlyphPx(context.Style, fit, height), px, height);
    }

    public FaceSize Measure(in FaceContext context)
    {
        if (context.Vertical)
        {
            var (glyph, _, height) = Stacked(context);
            return new FaceSize(glyph + height, Square: false);
        }
        return new FaceSize(Drawn(context), Square: false);
    }

    /// <summary>
    /// What the face shows now: the glyph, the temperature or what it says instead, the forecast where
    /// a place is set, whether the reading is known and past due, and the scale it is shown in.
    /// </summary>
    private static (string Glyph, string Reading, Forecast? Forecast, bool Known, bool Stale, Func<double?, double?> Unit) Now(in FaceContext context)
    {
        var set = Set(context);
        var weather = context.Folder?.Weather;
        var shown = weather?.Shown;
        var forecast = set ? shown?.Forecast : null;
        var stale = WeatherReading.IsStale(shown, Clock.Now().ToUniversalTime(), weather?.Problem);
        var fahrenheit = shown?.Fahrenheit ?? false;
        double? Unit(double? value) => WeatherUnits.Temperature(value, fahrenheit, WeatherUnits.Fahrenheit);
        var (glyph, reading) = !set ? (WeatherCodes.Location, context.Vertical ? "" : S.WidgetWeatherSetLocation)
            : forecast?.Current is { } current ? (WeatherCodes.Describe(current.WeatherCode, current.IsDay != 0).Glyph, WeatherUnits.Degrees(Unit(current.Temperature)))
            // The device's location, which Windows will not give: the pin, as before a place is set.
            : WeatherStore.IsLocationOff(weather?.Problem) ? (WeatherCodes.Location, "–")
            : (WeatherCodes.Cloud, "–");
        return (glyph, reading, forecast, forecast?.Current is not null, stale, Unit);
    }

    /// <summary>
    /// How long the row is as it reads now across a bar (§244): the pieces <see cref="Paint"/> lays out,
    /// each as wide as it reads and cut to its box in the plan — the forecast's days, the temperature
    /// over the conditions, or the glyph, the temperature and today's high and low.
    /// </summary>
    private static float Drawn(in FaceContext context)
    {
        var gfx = context.Gfx;
        var set = Set(context);
        var (_, reading, forecast, _, _, unit) = Now(context);
        var plan = WeatherRow.Of(gfx, context.Style, WeatherFace.LookOf(context.Face), set);
        var days = forecast is null ? [] : NextDays(forecast, WeatherRow.Days, Clock.Now().ToUniversalTime(), unit);
        if (plan.Look == WeatherFace.Forecast && days.Count > 0)
        {
            var row = days.Sum(day =>
            {
                var loud = Wide(gfx, day.High, plan.Px, plan.Range);
                return Wide(gfx, day.Name, plan.Px, plan.Name) + plan.Gap + plan.Glyph + plan.Gap + loud + Wide(gfx, $"/{day.Low}", plan.Px, MathF.Max(1, plan.Range - loud));
            });
            return MathF.Ceiling(row + ((days.Count - 1) * plan.Between));
        }
        if (plan.Look == WeatherFace.Forecast)
        {
            plan = WeatherRow.Of(gfx, context.Style, WeatherFace.Simple, set);
        }
        if (plan.Look == WeatherFace.Conditions)
        {
            var words = forecast?.Current is { } now ? WeatherCodes.Describe(now.WeatherCode, now.IsDay != 0).Text : "";
            return MathF.Ceiling(plan.Glyph + plan.Gap + MathF.Max(Wide(gfx, reading, plan.Px, plan.Figure), Wide(gfx, words, plan.Px, plan.Words)));
        }
        var figure = Wide(gfx, reading, plan.Px, plan.Figure);
        var small = 0f;
        if (plan.Look == WeatherFace.Detailed)
        {
            var (high, low) = days.Count > 0 ? (days[0].High, days[0].Low) : ("–", "–");
            small = Wide(gfx, $"{high}/{low}", plan.SmallPx, plan.Range);
        }
        return MathF.Ceiling(plan.Glyph + (figure > 0 ? plan.Gap + figure : 0) + (small > 0 ? plan.Gap + small : 0));
    }

    /// <summary>What the face says now: a new reading, a new day or a reading going off paints it again.</summary>
    public string Reading(in FaceContext context)
    {
        if (!Set(context))
        {
            return "unset";
        }
        if (context.Folder?.Weather is not { Shown.Forecast.Current: { } now } weather)
        {
            return "waiting";
        }
        var stale = WeatherReading.IsStale(weather.Shown, Clock.Now().ToUniversalTime(), weather.Problem);
        var days = weather.Shown.Forecast is { } forecast
            ? string.Join(",", NextDays(forecast, WeatherRow.Days, Clock.Now().ToUniversalTime(), value => value).Select(day => $"{day.Name}{day.Glyph}{day.High}{day.Low}"))
            : "";
        return FormattableString.Invariant($"{now.Temperature}|{now.WeatherCode}|{now.IsDay}|{stale}|{weather.Shown.Fahrenheit}|{WeatherUnits.Fahrenheit}|{days}");
    }

    /// <summary>None: the folder opens on the forecast, which says more than a tooltip could.</summary>
    public string? Tooltip(in FaceContext context) => null;

    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        var fit = context.Fit;
        var ink = new IslandInk(context.Style.Look);
        var gfx = context.Gfx;
        var set = Set(context);
        var (glyph, reading, forecast, known, stale, Unit) = Now(context);
        var figureInk = known && !stale ? ink.Text : ink.Secondary;
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        ID2D1SolidColorBrush* brush = null;
        try
        {
            brush = Gfx.Brush(dc, ink.Text);
            var width = box.right - box.left;
            var height = box.bottom - box.top;
            if (context.Vertical)
            {
                var (size, px, line) = Stacked(context);
                // The glyph over the reading, the two centred by their ink: from the glyph's top down
                // to the figures' foot, not the room the boxes keep round them.
                var (inkAbove, glyphBottom) = GlyphInk(gfx, glyph, size);
                var foot = reading.Length > 0 ? size + TextLine.Ink(gfx, Warmest, px, Weight.Regular).Bottom : glyphBottom;
                var top = box.top + TextLine.Round((height / 2) - ((inkAbove + foot) / 2));
                PutGlyph(dc, gfx, brush, glyph, size, ink.Text, box.left + ((width - size) / 2), top, size);
                if (reading.Length > 0)
                {
                    var laid = TextLine.Lay(gfx, reading, px, Weight.Regular, width);
                    try
                    {
                        laid.Draw(dc, brush, figureInk, box.left + ((width - laid.Width) / 2), top + size);
                    }
                    finally
                    {
                        laid.Release();
                    }
                }
                return;
            }
            var plan = WeatherRow.Of(gfx, context.Style, WeatherFace.LookOf(context.Face), set);
            // One row: every piece is centred on the line through the middle of the cell, a glyph on
            // its box, the loud figures' ink on the line, and a quiet figure on their baseline. Along
            // the bar the pieces as they read now stand in the middle of the face's room
            // (2026-10-11: "the taskbar weather face should be centerd").
            var (inkTop, inkBottom) = TextLine.Ink(gfx, WeatherFaceDrawing.Warmest, plan.Px, Weight.Regular);
            var y = box.top + TextLine.Round((height / 2) - ((inkTop + inkBottom) / 2));
            var glyphTop = box.top + ((height - plan.Glyph) / 2);
            var days = forecast is null ? [] : NextDays(forecast, WeatherRow.Days, Clock.Now().ToUniversalTime(), Unit);
            if (plan.Look == WeatherFace.Forecast && days.Count > 0)
            {
                // Each day as wide as it reads: its weekday, its glyph and "18°/8°" — the high loud, the
                // slash and the low quiet —, the days the forecast's room apart.
                var pieces = days.Select(day =>
                {
                    var name = Wide(gfx, day.Name, plan.Px, plan.Name);
                    var loud = Wide(gfx, day.High, plan.Px, plan.Range);
                    var quiet = Wide(gfx, $"/{day.Low}", plan.Px, MathF.Max(1, plan.Range - loud));
                    return (Name: name, Loud: loud, Quiet: quiet);
                }).ToList();
                var row = pieces.Sum(piece => piece.Name + plan.Gap + plan.Glyph + plan.Gap + piece.Loud + piece.Quiet) + ((pieces.Count - 1) * plan.Between);
                var x = box.left + TextLine.Round((width - row) / 2);
                for (var at = 0; at < days.Count; at++)
                {
                    var (name, dayGlyph, high, low, today) = days[at];
                    var piece = pieces[at];
                    Put(dc, gfx, brush, name, plan.Px, today ? figureInk : ink.Secondary, x, plan.Name, y);
                    x += piece.Name + plan.Gap;
                    PutGlyph(dc, gfx, brush, dayGlyph, plan.Glyph, ink.Text, x, glyphTop, plan.Glyph);
                    x += plan.Glyph + plan.Gap;
                    Put(dc, gfx, brush, high, plan.Px, figureInk, x, plan.Range, y);
                    Put(dc, gfx, brush, $"/{low}", plan.Px, ink.Secondary, x + piece.Loud, MathF.Max(1, plan.Range - piece.Loud), y);
                    x += piece.Loud + piece.Quiet + plan.Between;
                }
                return;
            }
            if (plan.Look == WeatherFace.Forecast)
            {
                // The forecast's room with nothing to put in it yet: the simple pieces, in the middle.
                plan = WeatherRow.Of(gfx, context.Style, WeatherFace.Simple, set);
            }
            if (plan.Look == WeatherFace.Conditions)
            {
                // The glyph, then the temperature over the words, the two lines starting together
                // as Windows' Widgets button writes them, the whole in the middle of the face's room.
                var words = forecast?.Current is { } now ? WeatherCodes.Describe(now.WeatherCode, now.IsDay != 0).Text : "";
                var high = Wide(gfx, reading, plan.Px, plan.Figure);
                var said = Wide(gfx, words, plan.Px, plan.Words);
                var block = plan.Glyph + plan.Gap + MathF.Max(high, said);
                var at = box.left + TextLine.Round((width - block) / 2);
                // No reading yet: the dash alone, on the line through the middle, as the simple look's.
                var top = words.Length == 0 ? y : box.top + TextLine.Round((height - (2 * plan.TextHeight)) / 2);
                PutGlyph(dc, gfx, brush, glyph, plan.Glyph, ink.Text, at, glyphTop, plan.Glyph);
                at += plan.Glyph + plan.Gap;
                Put(dc, gfx, brush, reading, plan.Px, figureInk, at, plan.Figure, top);
                Put(dc, gfx, brush, words, plan.Px, figureInk, at, plan.Words, top + plan.TextHeight);
                return;
            }
            var figure = Wide(gfx, reading, plan.Px, plan.Figure);
            var range = "";
            var small = 0f;
            if (plan.Look == WeatherFace.Detailed)
            {
                var (high, low) = days.Count > 0 ? (days[0].High, days[0].Low) : ("–", "–");
                range = $"{high}/{low}";
                small = Wide(gfx, range, plan.SmallPx, plan.Range);
            }
            var pieceRow = plan.Glyph + (figure > 0 ? plan.Gap + figure : 0) + (small > 0 ? plan.Gap + small : 0);
            var left = box.left + TextLine.Round((width - pieceRow) / 2);
            PutGlyph(dc, gfx, brush, glyph, plan.Glyph, ink.Text, left, glyphTop, plan.Glyph);
            left += plan.Glyph + plan.Gap;
            Put(dc, gfx, brush, reading, plan.Px, set ? figureInk : ink.Text, left, plan.Figure, y);
            if (small > 0)
            {
                Put(dc, gfx, brush, range, plan.SmallPx, ink.Secondary, left + figure + plan.Gap, plan.Range, y + plan.TextHeight - plan.SmallHeight);
            }
        }
        finally
        {
            ComUtil.Release(ref brush);
        }
    }

    /// <summary>How wide <paramref name="text"/> at <paramref name="px"/> reads, cut to <paramref name="box"/> as <see cref="Put"/> cuts it.</summary>
    private static float Wide(Gfx gfx, string text, float px, float box)
    {
        if (text.Length == 0)
        {
            return 0;
        }
        var line = TextLine.Lay(gfx, text, px, Weight.Regular, box);
        try
        {
            return MathF.Min(line.Width, box);
        }
        finally
        {
            line.Release();
        }
    }

    /// <summary>
    /// <paramref name="text"/> at <paramref name="px"/> in a box <paramref name="box"/> px wide at
    /// <paramref name="x"/>, its top at <paramref name="y"/>, at the box's left and cut to it.
    /// Returns the width it took.
    /// </summary>
    private static float Put(ID2D1DeviceContext* dc, Gfx gfx, ID2D1SolidColorBrush* brush, string text, float px, DXGI_RGBA color, float x, float box, float y)
    {
        if (text.Length == 0)
        {
            return 0;
        }
        var line = TextLine.Lay(gfx, text, px, Weight.Regular, box);
        try
        {
            line.Draw(dc, brush, color, x, y);
            return line.Width;
        }
        finally
        {
            line.Release();
        }
    }

    /// <summary>Where <paramref name="glyph"/>'s ink lies down a box of <paramref name="px"/> it is drawn in (<see cref="PutGlyph"/>), in px from the box's top.</summary>
    private static (float Top, float Bottom) GlyphInk(Gfx gfx, string glyph, float px)
    {
        if (glyph.Length == 0)
        {
            return (0, px);
        }
        var line = TextLine.Lay(gfx, glyph, px, Weight.Regular, null, gfx.IconFamily);
        try
        {
            DWRITE_OVERHANG_METRICS overhang;
            if (line.Layout == null || line.Layout->GetOverhangMetrics(&overhang).FAILED)
            {
                return (0, px);
            }
            // Overhangs are measured outward from the layout's own box, which PutGlyph centres in the glyph's.
            var at = (px - line.Height) / 2;
            return (at - overhang.top, at + line.Layout->GetMaxHeight() + overhang.bottom);
        }
        finally
        {
            line.Release();
        }
    }

    /// <summary>A conditions' glyph of <paramref name="px"/> in a box as wide as itself at <paramref name="x"/>, its top at <paramref name="top"/>.</summary>
    private static void PutGlyph(ID2D1DeviceContext* dc, Gfx gfx, ID2D1SolidColorBrush* brush, string glyph, float px, DXGI_RGBA color, float x, float top, float box)
    {
        if (glyph.Length == 0)
        {
            return;
        }
        var line = TextLine.Lay(gfx, glyph, px, Weight.Regular, null, gfx.IconFamily);
        try
        {
            line.Draw(dc, brush, color, x + ((px - line.Width) / 2), top + ((box - line.Height) / 2));
        }
        finally
        {
            line.Release();
        }
    }

    /// <summary>
    /// The place's next days from its own today: up to <paramref name="most"/> of them as the
    /// forecast look writes them — the region's short weekday, the conditions' glyph, the high and
    /// the low. A forecast that is days old starts at its today and not at the day it was fetched.
    /// </summary>
    internal static List<(string Name, string Glyph, string High, string Low, bool Today)> NextDays(
        Forecast forecast, int most, DateTime utcNow, Func<double?, double?> unit)
    {
        var list = new List<(string, string, string, string, bool)>();
        if (forecast.Daily is not { Time: { } dates } daily)
        {
            return list;
        }
        var today = DateOnly.FromDateTime(WeatherWindow.LocalNow(forecast, utcNow));
        var first = 0;
        for (var index = 0; index < dates.Length; index++)
        {
            if (ParseDay(dates[index]) is { } day && day >= today)
            {
                first = index;
                break;
            }
            first = index + 1;
        }
        for (var index = first; index < dates.Length && list.Count < most; index++)
        {
            if (ParseDay(dates[index]) is not { } day)
            {
                continue;
            }
            var code = daily.WeatherCode is { } codes && index < codes.Length ? codes[index] : null;
            var high = daily.High is { } highs && index < highs.Length ? highs[index] : null;
            var low = daily.Low is { } lows && index < lows.Length ? lows[index] : null;
            list.Add((Panels.Nls.ShortDayName(day.DayOfWeek), WeatherCodes.Describe(code, true).Glyph, WeatherUnits.Degrees(unit(high)), WeatherUnits.Degrees(unit(low)), day == today));
        }
        return list;
    }

    /// <summary>Open-Meteo's "2026-09-22"; null when it is missing or unreadable.</summary>
    private static DateOnly? ParseDay(string? text) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;
}
