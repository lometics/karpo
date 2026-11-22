// The earthquake face (Karpo.Core\FolderFaces.cs, docs\widgets.md §240): a dot in the magnitude's
// colour and the past day's latest earthquake near the face's own place (FolderFace.Weather, as the
// weather face's), "M4.8 · 120 km", from the feed the earthquakes' store keeps, which the folder
// reads as a user of its own (FolderIsland.Quakes).
//
// Across a bar it is as wide as what it says now (§244, which ends §12.2's rule for faces). What it
// says is the fullest of its forms that fits the room of the widest magnitude ("M8.8"), distance (as
// many figures as its radius has, five for the world), compass point and age in the form its width
// option asks for, and the quiet day's words beside them (AirText.Fit); that room is the face's most.
//
// A quiet day — nothing in its radius over its magnitude for 24 hours — is a faded dot and "No
// earthquakes in 24 h" ("–" where only the magnitude is shown). Before a place is set the pin and
// "Set location"; while nothing has come the dot is faded and the words a dash; past due, or left
// standing by a failure, the words are quiet and the dot faded. Down a bar on its side, the dot over
// the figure.

using System.Globalization;
using Karpo.Core;
using Karpo.Dock.Native;
using Karpo.Dock.Weather;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>The latest earthquake near a place: its magnitude's dot and words, in one of three widths.</summary>
internal sealed unsafe class QuakeFaceDrawing : IFaceDrawing
{
    public string Id => FolderFaces.QuakesId;

    public bool Seconds(FolderFace face) => false;

    /// <summary>Whether the face looks anywhere: a place set, or the whole world.</summary>
    internal static bool Set(FolderFace face) => face.Weather is { IsSet: true } || QuakesFace.SettingsOf(face).Worldwide;

    /// <summary>The reading the face shows now, from what the folder holds.</summary>
    internal static QuakeReading Read(in FaceContext context) =>
        QuakeReading.From(context.Folder?.Quakes ?? QuakeHeld.None, QuakesFace.SettingsOf(context.Face), context.Face.Weather, Clock.Now().ToUniversalTime());

    /// <summary>The dot's size beside text <paramref name="height"/> px tall: a little over half of it.</summary>
    private static float Dot(in Fit fit, float height) => MathF.Max(fit.Px(6), TextLine.Round(height * 0.55f));

    /// <summary>What a reading may say for a face set to <paramref name="width"/>, fullest first; never more than the setting says.</summary>
    internal static IReadOnlyList<string> Forms(QuakeRow row, string width)
    {
        var withAway = row.Away.Length > 0 ? row.Magnitude + FaceDrawings.Dot + row.Away : row.Magnitude;
        return width switch
        {
            QuakesFace.Wide => [row.Magnitude + FaceDrawings.Dot + row.Under, withAway, row.Magnitude],
            QuakesFace.Compact => [row.Magnitude],
            _ => [withAway, row.Magnitude],
        };
    }

    /// <summary>What a quiet day says for <paramref name="width"/>, fullest first.</summary>
    internal static IReadOnlyList<string> Quiet(string width) =>
        width == QuakesFace.Compact ? [Dash] : [S.DockQuakesNoneDay, Dash];

    private const string Dash = "–";

    /// <summary>The widest the face's words read for its settings, at <paramref name="px"/>.</summary>
    internal static float Room(Gfx gfx, FolderFace face, float px)
    {
        var width = QuakesFace.WidthOf(face);
        var settings = QuakesFace.SettingsOf(face);
        var figures = settings.Worldwide ? 5 : settings.Radius.ToString(CultureInfo.InvariantCulture).Length;
        var distance = new string('8', figures);
        var magnitude = QuakeReading.Magnitude(8.8);
        var widest = 0f;
        float Measure(string text) => TextLine.Template(gfx, text, px, Weight.Regular).Width;
        var ages = new[] { Feeds.Age(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(59)), Feeds.Age(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(23)) };
        for (var point = 0; point < 8; point++)
        {
            var away = S.DockQuakesAway(distance: distance, direction: QuakeGeo.Compass(point));
            foreach (var age in ages)
            {
                widest = MathF.Max(widest, Measure(Forms(new QuakeRow(new Quake(), 0, magnitude, away, age), width)[0]));
            }
        }
        widest = MathF.Max(widest, Measure(Quiet(width)[0]));
        return MathF.Ceiling(widest);
    }

    /// <summary>Down a vertical bar: the size the widest figure fits the bar's room at, and its height.</summary>
    private static (float Px, float Height) Stacked(in FaceContext context)
    {
        var fit = context.Fit;
        var (px, _, height) = VerticalFit.Across(context.Gfx, Figure(8.8), VerticalFit.Room(fit), Weight.Regular, VerticalFit.Ladder(fit));
        return (px, height);
    }

    /// <summary>The magnitude alone, "4.8", for a bar on its side.</summary>
    private static string Figure(double magnitude) =>
        magnitude.ToString("0.0", CultureInfo.InvariantCulture).Replace(".", Panels.Nls.DecimalSeparator(), StringComparison.Ordinal);

    public FaceSize Measure(in FaceContext context)
    {
        var fit = context.Fit;
        if (context.Vertical)
        {
            // Before a place is set, the pin alone (Paint).
            var (_, height) = Stacked(context);
            return new FaceSize(Set(context.Face) ? Dot(fit, height) + fit.Leading + height : fit.Glyph(), Square: false);
        }
        var px = fit.Primary;
        if (!Set(context.Face))
        {
            return new FaceSize(fit.Glyph() + fit.Gap + TextLine.Template(context.Gfx, S.WidgetWeatherSetLocation, px, Weight.Regular).Width, Square: false);
        }
        var (_, line) = TextLine.Template(context.Gfx, QuakeReading.Magnitude(8.8), px, Weight.Regular);
        // What it says now (§244): "M4.8 · 120 km NE" is wider than a dash.
        return new FaceSize(MathF.Ceiling(Dot(fit, line) + fit.Gap + Said(context, Read(context), px).Width), Square: false);
    }

    /// <summary>
    /// What the face says after its dot for <paramref name="reading"/> at <paramref name="px"/>, and how
    /// wide: the fullest of its forms that fits the room of its widest reading (<see cref="Room"/>), a
    /// quiet day's words, or the dash while nothing has come.
    /// </summary>
    private static (string Text, float Width) Said(in FaceContext context, QuakeReading reading, float px)
    {
        var gfx = context.Gfx;
        var faceWidth = QuakesFace.WidthOf(context.Face);
        var room = Room(gfx, context.Face, px);
        var latest = reading.State == QuakeState.Ready ? reading.Latest : null;
        IReadOnlyList<string> forms = latest is { } now ? Forms(now, faceWidth)
            : reading.State == QuakeState.Ready ? Quiet(faceWidth)
            : [Dash];
        var said = AirText.Fit(forms, room, text => TextLine.Measure(gfx, text, px, Weight.Regular, null));
        return (said, MathF.Min(room, TextLine.Measure(gfx, said, px, Weight.Regular, null)));
    }

    /// <summary>What the face shows now: another earthquake, its age, staleness or the width going off paints it again.</summary>
    public string Reading(in FaceContext context)
    {
        if (!Set(context.Face))
        {
            return "unset";
        }
        var reading = Read(context);
        var latest = reading.Latest;
        return $"{reading.State}|{latest?.Quake.Id}|{latest?.Magnitude}|{latest?.Under}|{reading.Stale}|{QuakesFace.WidthOf(context.Face)}";
    }

    /// <summary>The place and how far it looks, the day's earthquake and the answer's age; none before a place is set.</summary>
    public string? Tooltip(in FaceContext context) =>
        Set(context.Face) ? Read(context).Tooltip(PlaceNames.Known(context.Face.Weather)) : null;

    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        var fit = context.Fit;
        var ink = new IslandInk(context.Style.Look);
        var gfx = context.Gfx;
        var width = box.right - box.left;
        var height = box.bottom - box.top;
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        if (!Set(context.Face))
        {
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
        var latest = reading.State == QuakeState.Ready ? reading.Latest : null;
        var location = reading.State != QuakeState.Ready && context.Face.Weather is { UseDeviceLocation: true } && WeatherStore.IsLocationOff(reading.Problem);
        var words = latest is not null && !reading.Stale ? ink.Text : ink.Secondary;
        var dotInk = latest is { } row ? QuakeLevels.Ink(row.Level, reading.Stale ? 0.45f : 1) : IslandInk.Faded(ink.Secondary, 0.5f);
        if (context.Vertical)
        {
            var (px, line) = Stacked(context);
            var dot = Dot(fit, line);
            var top = box.top + TextLine.Round((height - (dot + fit.Leading + line)) / 2);
            Mark(dc, gfx, location, dotInk, ink, box.left + ((width - dot) / 2), top, dot);
            FaceDrawings.Put(dc, gfx, latest is { } shown ? Figure(shown.Quake.Magnitude) : Dash, px, words, box, top + dot + fit.Leading);
            return;
        }
        var textPx = fit.Primary;
        var (_, textLine) = TextLine.Template(gfx, QuakeReading.Magnitude(8.8), textPx, Weight.Regular);
        var size = Dot(fit, textLine);
        var room = Room(gfx, context.Face, textPx);
        var (said, saidWidth) = Said(context, reading, textPx);
        var left = box.left + TextLine.Round((width - (size + fit.Gap + saidWidth)) / 2);
        // The words' ink on the line through the cell's middle, the dot centred on the same line.
        var (inkTop, inkBottom) = TextLine.Ink(gfx, QuakeReading.Magnitude(8.8), textPx, Weight.Regular);
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

    /// <summary>The magnitude's dot, or the pin where this PC's location is off and nothing is known of where it is.</summary>
    private static void Mark(ID2D1DeviceContext* dc, Gfx gfx, bool location, DXGI_RGBA colour, in IslandInk ink, float left, float top, float size)
    {
        if (location)
        {
            PopupPaint.Glyph(dc, gfx, WeatherCodes.Location, size, ink.Secondary, PopupPaint.Rect(left, top, size, size));
            return;
        }
        var dot = PopupPaint.Rect(left, top, size, size);
        PopupPaint.Fill(dc, dot, size / 2, colour);
        // A hairline of the text's ink round it, so the yellow still reads on a light bar.
        PopupPaint.Stroke(dc, dot, size / 2, 1, IslandInk.Faded(ink.Text, 0.18f));
    }
}
