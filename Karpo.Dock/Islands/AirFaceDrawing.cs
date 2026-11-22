// The air-quality face (Karpo.Core\FolderFaces.cs, docs\widgets.md §210): a dot in the level's
// colour and the index, "32 · Good", for the face's own place (FolderFace.Weather, as the weather
// face's). The answer is the one the air's store keeps for the place, which the folder reads as a
// user of its own (FolderIsland.Air), so a face and a line of one place are one fetch.
//
// Across a bar it is as wide as what it says now (§244, which ends §12.2's rule for faces): "32 ·
// Fair" is a narrower face than "132 · Extremely poor". What it says is the fullest of its forms
// (AirText.Fit) that fits the room of the widest of its levels in the form its width option asks
// for, each with as many figures as its band can have ("88 · Good", "888 · Extremely poor") — the
// place's region's index where the place picks, both only for this PC's location or near the
// European area's edge (§230); that room is the face's most.
//
// Before a place is set the pin and "Set location", as the weather face says; while nothing has come
// the dot is quiet and the value a dash; a reading past due, or one a failure left standing, has its
// words in the quiet ink and its dot faded. Down a bar on its side, the dot over the value.

using Karpo.Core;
using Karpo.Dock.Native;
using Karpo.Dock.Weather;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>The air quality: the level's dot and the index, in one of three widths.</summary>
internal sealed unsafe class AirFaceDrawing : IFaceDrawing
{
    public string Id => FolderFaces.AirQualityId;

    public bool Seconds(FolderFace face) => false;

    /// <summary>Whether the face has a place set.</summary>
    private static bool Set(in FaceContext context) => context.Face.Weather is { IsSet: true };

    /// <summary>The reading the face shows now, from what the folder holds for its place.</summary>
    internal static AirReading Read(in FaceContext context) =>
        AirReading.From(context.Folder?.Air?.Shown, AirQualityFace.IndexOf(context.Face), Clock.Now().ToUniversalTime(), context.Folder?.Air?.Problem);

    /// <summary>The dot's size beside text <paramref name="height"/> px tall: a little over half of it.</summary>
    private static float Dot(in Fit fit, float height) => MathF.Max(fit.Px(6), TextLine.Round(height * 0.55f));

    /// <summary>
    /// The widest the face's words read for its width option, at <paramref name="px"/>: each level of
    /// the scales its place can show in its fullest form, with as many figures as that level can have
    /// ("88 · Good", "888 · Extremely poor"; §230).
    /// </summary>
    internal static float Room(Gfx gfx, FolderFace face, float px)
    {
        var width = AirQualityFace.WidthOf(face);
        var widest = 0f;
        foreach (var scale in AirBands.ScalesAt(AirQualityFace.IndexOf(face), face.Weather))
        {
            for (var level = 0; level < AirBands.Levels; level++)
            {
                var fullest = AirText.Forms(AirText.WidestAt(scale, level), AirBands.Short(scale, level), width)[0];
                widest = MathF.Max(widest, TextLine.Template(gfx, fullest, px, Weight.Regular).Width);
            }
        }
        return MathF.Ceiling(widest);
    }

    /// <summary>Down a vertical bar: the size the widest value fits the bar's room at, and its height.</summary>
    private static (float Px, float Height) Stacked(in FaceContext context)
    {
        var fit = context.Fit;
        var (px, _, height) = VerticalFit.Across(context.Gfx, AirText.WidestValue, VerticalFit.Room(fit), Weight.Regular, VerticalFit.Ladder(fit));
        return (px, height);
    }

    public FaceSize Measure(in FaceContext context)
    {
        var fit = context.Fit;
        if (context.Vertical)
        {
            // Before a place is set, the pin alone (Paint).
            var (_, height) = Stacked(context);
            return new FaceSize(Set(context) ? Dot(fit, height) + fit.Leading + height : fit.Glyph(), Square: false);
        }
        var px = fit.Primary;
        var (_, line) = TextLine.Template(context.Gfx, AirText.WidestValue, px, Weight.Regular);
        if (!Set(context))
        {
            return new FaceSize(fit.Glyph() + fit.Gap + TextLine.Template(context.Gfx, S.WidgetWeatherSetLocation, px, Weight.Regular).Width, Square: false);
        }
        // What it says now (§244): "32 · Fair" is narrower than "132 · Extremely poor".
        return new FaceSize(MathF.Ceiling(Dot(fit, line) + fit.Gap + Said(context, Read(context), px).Width), Square: false);
    }

    /// <summary>
    /// What the face says after its dot for <paramref name="reading"/> at <paramref name="px"/>, and how
    /// wide: the fullest of its forms that fits the room of its widest level (<see cref="AirText.Fit"/>),
    /// or the dash while nothing has come.
    /// </summary>
    private static (string Text, float Width) Said(in FaceContext context, AirReading reading, float px)
    {
        var gfx = context.Gfx;
        var room = Room(gfx, context.Face, px);
        var value = reading.State == AirState.Ready ? reading.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "–";
        var said = reading.State == AirState.Ready
            ? AirText.Fit(AirText.Forms(value, reading.Short, AirQualityFace.WidthOf(context.Face)), room, text => TextLine.Measure(gfx, text, px, Weight.Regular, null))
            : value;
        return (said, MathF.Min(room, TextLine.Measure(gfx, said, px, Weight.Regular, null)));
    }

    /// <summary>What the face shows now: a new value, level, index or age going off paints it again.</summary>
    public string Reading(in FaceContext context)
    {
        if (!Set(context))
        {
            return "unset";
        }
        var reading = Read(context);
        return FormattableString.Invariant($"{reading.State}|{reading.Scale}|{reading.Value}|{reading.Level}|{reading.Stale}|{AirQualityFace.WidthOf(context.Face)}");
    }

    /// <summary>The place, the index and its level, the pollutants, the pollen and the age; none before a place is set.</summary>
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
        var ready = reading.State == AirState.Ready;
        var location = !ready && context.Face.Weather is { UseDeviceLocation: true } && WeatherStore.IsLocationOff(reading.Problem);
        var value = ready ? reading.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "–";
        var words = ready && !reading.Stale ? ink.Text : ink.Secondary;
        var dotInk = ready ? AirBands.Ink(reading.Scale, reading.Level, reading.Stale ? 0.45f : 1) : IslandInk.Faded(ink.Secondary, 0.5f);
        if (context.Vertical)
        {
            var (px, line) = Stacked(context);
            var dot = Dot(fit, line);
            var top = box.top + TextLine.Round((height - (dot + fit.Leading + line)) / 2);
            Mark(dc, gfx, location, dotInk, ink, box.left + ((width - dot) / 2), top, dot);
            FaceDrawings.Put(dc, gfx, value, px, words, box, top + dot + fit.Leading);
            return;
        }
        var textPx = fit.Primary;
        var (_, textLine) = TextLine.Template(gfx, AirText.WidestValue, textPx, Weight.Regular);
        var size = Dot(fit, textLine);
        var room = Room(gfx, context.Face, textPx);
        var (said, saidWidth) = Said(context, reading, textPx);
        var row = size + fit.Gap + saidWidth;
        var left = box.left + TextLine.Round((width - row) / 2);
        // The value's ink on the line through the cell's middle, the dot centred on the same line.
        var (inkTop, inkBottom) = TextLine.Ink(gfx, AirText.WidestValue, textPx, Weight.Regular);
        var y = box.top + TextLine.Round((height / 2) - ((inkTop + inkBottom) / 2));
        Mark(dc, gfx, location, dotInk, ink, left, box.top + TextLine.Round((height - size) / 2), size);
        var text = TextLine.Lay(gfx, said, textPx, Weight.Regular, room);
        ID2D1SolidColorBrush* brush = null;
        try
        {
            brush = Gfx.Brush(dc, words);
            text.Draw(dc, brush, words, left + size + fit.Gap, y);
        }
        finally
        {
            text.Release();
            ComUtil.Release(ref brush);
        }
    }

    /// <summary>The level's dot, or the pin where this PC's location is off and nothing has come.</summary>
    private static void Mark(ID2D1DeviceContext* dc, Gfx gfx, bool location, DXGI_RGBA colour, in IslandInk ink, float left, float top, float size)
    {
        if (location)
        {
            PopupPaint.Glyph(dc, gfx, WeatherCodes.Location, size, ink.Secondary, PopupPaint.Rect(left, top, size, size));
            return;
        }
        var dot = PopupPaint.Rect(left, top, size, size);
        PopupPaint.Fill(dc, dot, size / 2, colour);
        // A hairline of the text's ink round it, so the European index's pale cyan and the US index's
        // yellow still read on a light bar.
        PopupPaint.Stroke(dc, dot, size / 2, 1, IslandInk.Faded(ink.Text, 0.18f));
    }
}
