// The sun face (Karpo.Core\FolderFaces.cs, docs\widgets.md §368): the sun on the horizon — its rays long
// for a sunrise, short for a sunset — and the next one's time, "18:32", for the face's own place
// (FolderFace.Weather, as the weather face's). Worked out on this PC (Weather\Sun.cs): nothing fetched,
// and nothing ticks for it but the dock's minute, on which its Reading moves with the countdown or the
// event passing.
//
// Across a bar it is the glyph and what it says now (§244), up to the room of the widest thing its Show
// option can say — the region's widest time, "Sunrise" and "Sunset" with it, how soon at eighteen hours
// and fifty-eight minutes, a polar day or night — in this language. Inside the polar circles the sun up
// or the moon and "Polar day" or "Polar night". Before a place is set the pin and "Set location", as the
// weather face says; while this PC's position is not known yet, or Windows gives none, the pin and a
// dash. Down a bar on its side the glyph over the time.

using Karpo.Core;
using Karpo.Dock.Native;
using Karpo.Dock.Panels;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>The sun: the next sunrise or sunset, in one of three forms.</summary>
internal sealed unsafe class SunFaceDrawing : IFaceDrawing
{
    public string Id => FolderFaces.SunId;

    public bool Seconds(FolderFace face) => false;

    /// <summary>Whether the face has a place set.</summary>
    private static bool Set(in FaceContext context) => context.Face.Weather is { IsSet: true };

    /// <summary>The sun now at the face's place; null while the place is not known.</summary>
    internal static SunDay? Read(in FaceContext context) =>
        // §635: a ghost's face Zürich's sun, no position of this PC's asked.
        (context.Folder is { Sampled: true } ? Samples.Zurich : SunPlaces.Where(context.Face.Weather, context.Folder)) is var (latitude, longitude)
            ? SunDays.Of(new DateTimeOffset(Clock.Now()), latitude, longitude, TimeZoneInfo.Local)
            : null;

    /// <summary>The words <paramref name="show"/> can say at their widest (<see cref="SunFace"/>): templates, never a reading.</summary>
    internal static IEnumerable<string> Templates(Gfx gfx, string show, float px)
    {
        var time = TidesFaceDrawing.WidestTime(gfx, px);
        var soon = TimeSpan.FromMinutes((18 * 60) + 58);
        IEnumerable<string> said = show switch
        {
            SunFace.Named => [SunText.At(SunEventKind.Sunrise, time), SunText.At(SunEventKind.Sunset, time)],
            SunFace.Countdown => [SunText.NamedIn(SunEventKind.Sunrise, soon), SunText.NamedIn(SunEventKind.Sunset, soon)],
            _ => [time],
        };
        return [.. said, S.DockSunPolarDay, S.DockSunPolarNight];
    }

    /// <summary>The widest the face's words read for its Show option, at <paramref name="px"/>.</summary>
    internal static float Room(Gfx gfx, FolderFace face, float px)
    {
        var widest = 0f;
        foreach (var words in Templates(gfx, SunFace.ShowOf(face), px))
        {
            widest = MathF.Max(widest, TextLine.Template(gfx, words, px, Weight.Regular).Width);
        }
        return MathF.Ceiling(widest);
    }

    /// <summary>What the face says now for its Show option; a dash where its place is not known.</summary>
    internal static string Words(SunDay? day, string show) =>
        day is null ? "–"
        : day.Next is not { } next ? day.Times.Polar == PolarState.Day ? S.DockSunPolarDay : S.DockSunPolarNight
        : show switch
        {
            SunFace.Named => SunText.At(next.Kind, SunText.Time(next.At)),
            SunFace.Countdown => SunText.NamedIn(next.Kind, next.At - day.Now),
            _ => SunText.Time(next.At),
        };

    /// <summary>Down a vertical bar: the next one's time alone, or the daylight in the polar circles.</summary>
    internal static string Short(SunDay? day) =>
        day is null ? "–" : day.Next is { } next ? SunText.Time(next.At) : SunText.Daylight(day.Times);

    /// <summary>The glyph: the next one's sun on the horizon, the sun or the moon in the polar circles, the pin while the place is not known.</summary>
    internal static string Mark(SunDay? day) =>
        day is null ? OptionGlyphs.Place
        : day.Next is { } next ? SunText.Glyph(next.Kind)
        : day.Times.Polar == PolarState.Day ? SunText.Sun : SunText.Moon;

    /// <summary>
    /// Down a vertical bar: the rows the widest time, or a polar day's daylight, stands in across the bar
    /// (§598), and <see cref="Short"/> in them now.
    /// </summary>
    private static (VerticalFit.SideRows Plan, string[] Rows) Stacked(in FaceContext context, SunDay? day)
    {
        var plan = VerticalFit.Rows(context.Gfx, context.Fit, VerticalFit.Phrase, [TidesFaceDrawing.WidestTime(context.Gfx, context.Fit.Primary), S.WidgetCommonHours(24)]);
        return (plan, VerticalFit.Phrase[plan.Form](Short(day)));
    }

    public FaceSize Measure(in FaceContext context)
    {
        var fit = context.Fit;
        var glyph = fit.Glyph();
        if (context.Vertical)
        {
            if (!Set(context))
            {
                return new FaceSize(glyph, Square: false);
            }
            var (plan, rows) = Stacked(context, Read(context));
            return new FaceSize(glyph + fit.Leading + plan.LengthOf(rows), Square: false);
        }
        var px = fit.Primary;
        if (!Set(context))
        {
            return new FaceSize(glyph + fit.Gap + TextLine.Template(context.Gfx, S.WidgetWeatherSetLocation, px, Weight.Regular).Width, Square: false);
        }
        // What it says now (§244), up to the room of the widest its Show option can say.
        return new FaceSize(MathF.Ceiling(glyph + fit.Gap + SaidWidth(context.Gfx, context.Face, Read(context), px)), Square: false);
    }

    /// <summary>How wide what the face says now is, at <paramref name="px"/>: its words, cut to its room.</summary>
    private static float SaidWidth(Gfx gfx, FolderFace face, SunDay? day, float px) =>
        MathF.Min(Room(gfx, face, px), TextLine.Measure(gfx, Words(day, SunFace.ShowOf(face)), px, Weight.Regular, null));

    /// <summary>What the face shows now: the next event passing, a new minute of a countdown, the place found.</summary>
    public string Reading(in FaceContext context)
    {
        if (!Set(context))
        {
            return "unset";
        }
        var day = Read(context);
        return $"{Mark(day)}|{Words(day, SunFace.ShowOf(context.Face))}";
    }

    /// <summary>The place, the next one and how soon, today's sunrise and sunset, and the daylight; none before a place is set.</summary>
    public string? Tooltip(in FaceContext context)
    {
        if (!Set(context))
        {
            return null;
        }
        var day = Read(context);
        var lines = new List<string>();
        if (PlaceNames.Of(context.Face.Weather) is { Length: > 0 } place)
        {
            lines.Add(place);
        }
        if (day is null)
        {
            lines.Add(SunPlaces.Off(context.Face.Weather) ? S.WidgetWeatherLocationOff : S.DockSunLocating);
            return string.Join('\n', lines);
        }
        if (day.Next is { } next)
        {
            lines.Add(SunText.At(next.Kind, SunText.Time(next.At)) + FaceDrawings.Dot + SunText.In(next.At - day.Now));
            lines.Add(SunText.At(SunEventKind.Sunrise, SunText.Time(day.Times.Sunrise)) + FaceDrawings.Dot + SunText.At(SunEventKind.Sunset, SunText.Time(day.Times.Sunset)));
        }
        else
        {
            lines.Add(day.Times.Polar == PolarState.Day ? S.DockSunPolarDay : S.DockSunPolarNight);
        }
        lines.Add(S.DockSunDaylightOf(SunText.Daylight(day.Times)));
        return string.Join('\n', lines);
    }

    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        var fit = context.Fit;
        var ink = new IslandInk(context.Style.Look);
        var gfx = context.Gfx;
        var width = box.right - box.left;
        var height = box.bottom - box.top;
        var glyph = fit.Glyph();
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        if (!Set(context))
        {
            // The pin and "Set location", as the weather face says it; the pin alone down a bar on its side.
            if (context.Vertical)
            {
                PopupPaint.Glyph(dc, gfx, OptionGlyphs.Place, glyph, ink.Text, PopupPaint.Rect(box.left + ((width - glyph) / 2), box.top + ((height - glyph) / 2), glyph, glyph));
                return;
            }
            FaceDrawings.GlyphAndName(dc, context, box, OptionGlyphs.Place, S.WidgetWeatherSetLocation, ink.Text);
            return;
        }
        var day = Read(context);
        var mark = Mark(day);
        var words = day is null ? ink.Secondary : ink.Text;
        if (context.Vertical)
        {
            var (plan, rows) = Stacked(context, day);
            var top = box.top + TextLine.Round((height - (glyph + fit.Leading + plan.LengthOf(rows))) / 2);
            PopupPaint.Glyph(dc, gfx, mark, glyph, words, PopupPaint.Rect(box.left + ((width - glyph) / 2), top, glyph, glyph));
            FaceDrawings.Rows(dc, gfx, rows, plan, words, box, top + glyph + fit.Leading);
            return;
        }
        var textPx = fit.Primary;
        var room = Room(gfx, context.Face, textPx);
        var said = Words(day, SunFace.ShowOf(context.Face));
        var saidWidth = SaidWidth(gfx, context.Face, day, textPx);
        var row = glyph + fit.Gap + saidWidth;
        var left = box.left + TextLine.Round((width - row) / 2);
        var (inkTop, inkBottom) = TextLine.Ink(gfx, "8", textPx, Weight.Regular);
        var y = box.top + TextLine.Round((height / 2) - ((inkTop + inkBottom) / 2));
        PopupPaint.Glyph(dc, gfx, mark, glyph, words, PopupPaint.Rect(left, box.top + TextLine.Round((height - glyph) / 2), glyph, glyph));
        var text = TextLine.Lay(gfx, said, textPx, Weight.Regular, room);
        ID2D1SolidColorBrush* brush = null;
        try
        {
            brush = Gfx.Brush(dc, words);
            text.Draw(dc, brush, words, left + glyph + fit.Gap, y);
        }
        finally
        {
            text.Release();
            ComUtil.Release(ref brush);
        }
    }
}
