// The tides face (Karpo.Core\FolderFaces.cs, docs\widgets.md §236): an arrow for the way the sea
// goes and the next high or low, "High 14:32", for the face's own place (FolderFace.Weather, as the
// weather face's). The answer is the one the tides' store keeps for the place, which the folder reads
// as a user of its own (FolderIsland.Tides), so a face and a line of one place are one fetch.
//
// Across a bar it is the arrow and what it says now (§244, which ends §12.2's rule for faces), up to
// the room of the widest thing its Show option can say — "High"'s and "Low"'s widest with the widest
// time, a height of two figures under the sea, or how soon at eighteen hours and fifty-eight minutes —
// in this language.
//
// Before a place is set the pin and "Set location", as the weather face says; while nothing has come
// the drop and a dash; a place off the sea, the drop and a dash in the quiet ink (its tooltip says
// why); a reading a failure left standing, or past due, in the quiet ink. Down a bar on its side the
// arrow over the next turn's time, or the height.

using Karpo.Core;
using Karpo.Dock.Native;
using Karpo.Dock.Panels;
using Karpo.Dock.Weather;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>The tides: the way the sea goes and the next high or low, in one of four forms.</summary>
internal sealed unsafe class TidesFaceDrawing : IFaceDrawing
{
    public string Id => FolderFaces.TidesId;

    public bool Seconds(FolderFace face) => false;

    /// <summary>Whether the face has a place set.</summary>
    private static bool Set(in FaceContext context) => context.Face.Weather is { IsSet: true };

    /// <summary>The reading the face shows now, from what the folder holds for its place.</summary>
    internal static TideReading Read(in FaceContext context) =>
        TideReading.From(context.Folder?.Tides?.Shown, Clock.Now().ToUniversalTime(), context.Folder?.Tides?.Problem);

    /// <summary>The widest time of day the region writes: every hour's ten to midnight, the widest of them.</summary>
    internal static string WidestTime(Gfx gfx, float px)
    {
        var widest = "";
        var most = 0f;
        foreach (var hour in new[] { 0, 10, 12, 20, 22, 23 })
        {
            var time = Nls.Time(new DateTime(2000, 1, 1, hour, 58, 0));
            var width = TextLine.Template(gfx, time, px, Weight.Regular).Width;
            if (width > most)
            {
                (widest, most) = (time, width);
            }
        }
        return widest;
    }

    /// <summary>The words <paramref name="show"/> can say at their widest (<see cref="TidesFace"/>): templates, never a reading.</summary>
    internal static IEnumerable<string> Templates(Gfx gfx, string show, float px, bool imperial)
    {
        var time = WidestTime(gfx, px);
        var height = TideText.WidestHeight(imperial);
        var soon = TimeSpan.FromMinutes((18 * 60) + 58);
        return show switch
        {
            TidesFace.NextHeight => [TideText.At(true, time) + FaceDrawings.Dot + height, TideText.At(false, time) + FaceDrawings.Dot + height],
            TidesFace.Countdown => [TideText.In(true, soon), TideText.In(false, soon)],
            TidesFace.Level => [height],
            _ => [TideText.At(true, time), TideText.At(false, time)],
        };
    }

    /// <summary>The widest the face's words read for its Show option, at <paramref name="px"/>.</summary>
    internal static float Room(Gfx gfx, FolderFace face, float px, bool imperial)
    {
        var widest = 0f;
        foreach (var words in Templates(gfx, TidesFace.ShowOf(face), px, imperial))
        {
            widest = MathF.Max(widest, TextLine.Template(gfx, words, px, Weight.Regular).Width);
        }
        return MathF.Ceiling(widest);
    }

    /// <summary>What the face says now for its Show option; a dash where it cannot.</summary>
    internal static string Words(TideReading reading, string show) =>
        reading.State != TideState.Ready ? "–"
        : show == TidesFace.Level ? reading.Height is null ? "–" : reading.HeightText()
        : reading.Next is null ? "–"
        : show switch
        {
            TidesFace.NextHeight => reading.NextAtHeight(),
            TidesFace.Countdown => reading.NextIn,
            _ => reading.NextAt,
        };

    /// <summary>Down a vertical bar: the next turn's time, or the height, alone.</summary>
    internal static string Short(TideReading reading, string show) =>
        reading.State != TideState.Ready ? "–"
        : show == TidesFace.Level ? reading.Height is { } metres ? TideText.Height(metres, reading.Imperial).Split(' ')[0] : "–"
        : reading.Next is { } next ? next.Time : "–";

    /// <summary>The arrow's size beside text <paramref name="height"/> px tall.</summary>
    private static float ArrowSize(in Fit fit, float height) => MathF.Max(fit.Px(10), TextLine.Round(height * 0.8f));

    /// <summary>
    /// The forms <see cref="Short"/> takes down a vertical bar (§598): a time on one row, else its parts
    /// one over another; a height ("1.25") always whole, as its parts would read as two numbers.
    /// </summary>
    private static IReadOnlyList<Func<string, string[]>> Forms(string show) => show == TidesFace.Level ? [VerticalFit.Whole] : VerticalFit.Phrase;

    /// <summary>Down a vertical bar: the rows the widest time, or height, stands in across the bar, and <see cref="Short"/> in them now.</summary>
    private static (VerticalFit.SideRows Plan, string[] Rows) Stacked(in FaceContext context, TideReading reading, string show)
    {
        var plan = VerticalFit.Rows(context.Gfx, context.Fit, Forms(show), show == TidesFace.Level
            ? [TideText.Height(-8.88, false).Split(' ')[0], TideText.Height(-8.88, true).Split(' ')[0]]
            : [WidestTime(context.Gfx, context.Fit.Primary)]);
        return (plan, Forms(show)[plan.Form](Short(reading, show)));
    }

    public FaceSize Measure(in FaceContext context)
    {
        var fit = context.Fit;
        if (context.Vertical)
        {
            // Before a place is set, the pin alone (Paint).
            if (!Set(context))
            {
                return new FaceSize(fit.Glyph(), Square: false);
            }
            var (plan, rows) = Stacked(context, Read(context), TidesFace.ShowOf(context.Face));
            return new FaceSize(ArrowSize(fit, plan.Height) + fit.Leading + plan.LengthOf(rows), Square: false);
        }
        var px = fit.Primary;
        if (!Set(context))
        {
            return new FaceSize(fit.Glyph() + fit.Gap + TextLine.Template(context.Gfx, S.WidgetWeatherSetLocation, px, Weight.Regular).Width, Square: false);
        }
        var (_, line) = TextLine.Template(context.Gfx, "Ag", px, Weight.Regular);
        // What it says now (§244), up to the room of the widest its Show option can say.
        var reading = Read(context);
        return new FaceSize(MathF.Ceiling(ArrowSize(fit, line) + fit.Gap + SaidWidth(context.Gfx, context.Face, reading, px)), Square: false);
    }

    /// <summary>How wide what the face says now is, at <paramref name="px"/>: its words, cut to its room.</summary>
    private static float SaidWidth(Gfx gfx, FolderFace face, TideReading reading, float px) =>
        MathF.Min(Room(gfx, face, px, reading.Imperial), TextLine.Measure(gfx, Words(reading, TidesFace.ShowOf(face)), px, Weight.Regular, null));

    /// <summary>What the face shows now: a new turn, minute, height or state paints it again.</summary>
    public string Reading(in FaceContext context)
    {
        if (!Set(context))
        {
            return "unset";
        }
        var reading = Read(context);
        var show = TidesFace.ShowOf(context.Face);
        return FormattableString.Invariant($"{reading.State}|{reading.Rising}|{Words(reading, show)}|{reading.Stale}|{reading.Imperial}|{show}");
    }

    /// <summary>The place, the sea now, the next highs and lows and the age; none before a place is set.</summary>
    public string? Tooltip(in FaceContext context) =>
        Set(context) ? Read(context).Tooltip(PlaceNames.Known(context.Face.Weather)) : null;

    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        var fit = context.Fit;
        var ink = new IslandInk(context.Style.Look);
        var gfx = context.Gfx;
        var width = box.right - box.left;
        var height = box.bottom - box.top;
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        if (!Set(context))
        {
            // The pin and "Set location", as the weather face says it; the pin alone down a bar on its side.
            if (context.Vertical)
            {
                var glyph = fit.Glyph();
                PopupPaint.Glyph(dc, gfx, WeatherCodes.Location, glyph, ink.Text, PopupPaint.Rect(box.left + ((width - glyph) / 2), box.top + ((height - glyph) / 2), glyph, glyph));
                return;
            }
            FaceDrawings.GlyphAndName(dc, context, box, WeatherCodes.Location, S.WidgetWeatherSetLocation, ink.Text);
            return;
        }
        var reading = Read(context);
        var show = TidesFace.ShowOf(context.Face);
        var ready = reading.State == TideState.Ready;
        var location = !ready && context.Face.Weather is { UseDeviceLocation: true } && WeatherStore.IsLocationOff(reading.Problem);
        var mark = location ? WeatherCodes.Location : ready && reading.Rising is { } rising ? TideText.Arrow(rising) : WidgetInfos.Tides.Glyph;
        var words = ready && !reading.Stale ? ink.Text : ink.Secondary;
        if (context.Vertical)
        {
            var (plan, rows) = Stacked(context, reading, show);
            var size = ArrowSize(fit, plan.Height);
            var top = box.top + TextLine.Round((height - (size + fit.Leading + plan.LengthOf(rows))) / 2);
            PopupPaint.Glyph(dc, gfx, mark, size, words, PopupPaint.Rect(box.left + ((width - size) / 2), top, size, size));
            FaceDrawings.Rows(dc, gfx, rows, plan, words, box, top + size + fit.Leading);
            return;
        }
        var textPx = fit.Primary;
        var (_, textLine) = TextLine.Template(gfx, "Ag", textPx, Weight.Regular);
        var arrow = ArrowSize(fit, textLine);
        var room = Room(gfx, context.Face, textPx, reading.Imperial);
        var said = Words(reading, show);
        var saidWidth = SaidWidth(gfx, context.Face, reading, textPx);
        var row = arrow + fit.Gap + saidWidth;
        var left = box.left + TextLine.Round((width - row) / 2);
        var (inkTop, inkBottom) = TextLine.Ink(gfx, "8", textPx, Weight.Regular);
        var y = box.top + TextLine.Round((height / 2) - ((inkTop + inkBottom) / 2));
        PopupPaint.Glyph(dc, gfx, mark, arrow, words, PopupPaint.Rect(left, box.top + TextLine.Round((height - arrow) / 2), arrow, arrow));
        var text = TextLine.Lay(gfx, said, textPx, Weight.Regular, room);
        ID2D1SolidColorBrush* brush = null;
        try
        {
            brush = Gfx.Brush(dc, words);
            text.Draw(dc, brush, words, left + arrow + fit.Gap, y);
        }
        finally
        {
            text.Release();
            ComUtil.Release(ref brush);
        }
    }
}
