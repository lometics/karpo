// The time over the date, as Windows 11's own clock on the taskbar shows them (docs\widgets.md
// §118): the person, 2026-10-11, "create a folder face that combined time and clock together like it
// is shown on the windows default taskbar".
//
// Windows' clock is two lines in the taskbar's text size, the time over the short date, both in the
// primary ink and right-aligned to the end of their button, the block in the middle of the bar's
// thickness. The time is the region's without seconds, or with them where "Show seconds in system
// tray clock" is on (ShowSecondsInSystemClock, WindowsClock); the date the region's short date. The
// face across a bar is as wide as the two read now, in tabular figures (§244), so the minutes tick
// in one width and only a figure more or a new day moves the bar; its sizes are the templates'.
//
// A bar too thin for the two lines at any of the text's sizes has them on one line, the time then
// the date. Down a bar on its side the time's parts are one over another, as the clock face has them
// there (ClockFaceDrawing.SideTime: the hours over the minutes, then the seconds), and the date under
// them, each centred and each as large as the bar's width lets it be, the date no larger than the
// time: the region's short date, its month and its year always, on one row where it fits, else on
// as many as the bar asks (DateText.SideDate, §598: "28.12." over "2026", then "28" over "12" over
// "2026"). Never turned, and nothing of it left out.

using Karpo.Core;
using Karpo.Dock.Native;
using Karpo.Dock.Panels;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>Whether Windows' own clock on the taskbar shows seconds, as the time-and-date face follows it.</summary>
internal static class WindowsClock
{
    /// <summary>Tests only, on their thread: what Windows' clock is taken to show, instead of this PC's setting.</summary>
    [ThreadStatic]
    internal static bool? SecondsStand;

    /// <summary>ShowSecondsInSystemClock as last read (<see cref="WindowsTaskbarFollowed"/>).</summary>
    public static bool Seconds => SecondsStand ?? WindowsTaskbarFollowed.Now.Seconds;
}

/// <summary>How the time and the date stand, and at what sizes: from the settings and templates alone.</summary>
/// <param name="Stacked">The time over the date; else one line, the time then the date.</param>
/// <param name="TimePx">The time's text size.</param>
/// <param name="DatePx">The date's.</param>
/// <param name="TimeHeight">The time's line, down the bar.</param>
/// <param name="DateHeight">The date's (down a bar on its side, one of its rows).</param>
/// <param name="Along">The face's length along the bar.</param>
/// <param name="TimeRows">Down a bar on its side, the time's parts, one row each (<see cref="TimeHeight"/> is one of them); else 1.</param>
/// <param name="Gap">Down a bar on its side, between the time's rows and the date.</param>
/// <param name="Date">Down a bar on its side, how the date stands under the time (DateText.SideDate).</param>
internal readonly record struct DateTimePlan(bool Stacked, float TimePx, float DatePx, float TimeHeight, float DateHeight, float Along, int TimeRows = 1, float Gap = 0,
    VerticalFit.SideRows Date = default);

/// <summary>
/// The time over the date (<see cref="FolderFaces.DateTime"/>), as Windows' clock on the taskbar draws
/// them. See the top of the file.
/// </summary>
internal sealed unsafe class DateTimeFaceDrawing : IFaceDrawing
{
    public string Id => FolderFaces.DateTimeId;

    public bool Seconds(FolderFace face) => DateTimeFace.ShowsSeconds(face, WindowsClock.Seconds);

    /// <summary>The clock's settings the time is written with: the region's clock, in the face's zone (§230), with the seconds as the face says.</summary>
    internal static ClockFaceSettings Settings(FolderFace face) =>
        new(Seconds: DateTimeFace.ShowsSeconds(face, WindowsClock.Seconds), Hours: ClockFace.Region, Zone: DateTimeFace.ZoneOf(face), Label: "", Timer: false);

    /// <summary>Today in the face's zone: the date turns over at the zone's midnight, with its time.</summary>
    internal static DateOnly Today(FolderFace face) => DateOnly.FromDateTime(Clock.NowIn(DateTimeFace.ZoneOf(face)));

    public FaceSize Measure(in FaceContext context) => new(Plan(context.Gfx, context.Style, context.Face).Along, Square: false);

    /// <summary>
    /// The plan for <paramref name="face"/> on a bar of <paramref name="style"/>. Across a horizontal
    /// bar the largest of the text's sizes at which two lines fit the card's thickness, with a pixel
    /// to spare at each edge (the lines' ink stands inside their boxes), both lines that size; one line
    /// at the main size where none does.
    /// </summary>
    internal static DateTimePlan Plan(Gfx gfx, in IslandStyle style, FolderFace face)
    {
        var fit = new Fit(style);
        var settings = Settings(face);
        if (style.Vertical)
        {
            // The date with its month and its year, on as many rows as the bar asks, never larger
            // than the time over it (§598).
            var (timePx, timeHeight, rows) = ClockFaceDrawing.SideTime(gfx, settings, fit);
            var date = DateText.SideDate(gfx, fit, year: true, most: timePx);
            var gap = MathF.Max(fit.Px(2), 2 * fit.Leading);
            return new DateTimePlan(true, timePx, date.Px, timeHeight, date.Height, (rows * timeHeight) + gap + date.Length, rows, gap, date);
        }
        // Along the bar the time and the date as they read now (§244), in tabular figures: the
        // seconds and the minutes tick in one width; a figure more, or a new day, may move the bar.
        var time = ClockFaceDrawing.Time(settings, DateTime.MinValue);
        var day = Nls.ShortDate(Today(face));
        if (TwoLines(gfx, style, px => ClockFaceDrawing.WidestTime(gfx, settings, px)) is var (px, lineHeight))
        {
            var (timeWidth, dateWidth) = (TextLine.Template(gfx, time, px, Weight.Regular).Width, TextLine.Template(gfx, day, px, Weight.Regular).Width);
            return new DateTimePlan(true, px, px, lineHeight, lineHeight, MathF.Ceiling(MathF.Max(timeWidth, dateWidth)));
        }
        var main = fit.Primary;
        var (timeNow, height) = TextLine.Template(gfx, time, main, Weight.Regular);
        var dateNow = TextLine.Template(gfx, day, main, Weight.Regular).Width;
        return new DateTimePlan(false, main, main, height, height, MathF.Ceiling(timeNow + fit.Gap + dateNow));
    }

    /// <summary>The clock as Windows' taskbar sets it: the region's, without seconds, for <see cref="TwoLines"/>'s template.</summary>
    private static readonly ClockFaceSettings Plain = new(Seconds: false, Hours: ClockFace.Region, Zone: "", Label: "", Timer: false);

    /// <summary>
    /// The two lines of a face across a horizontal bar of <paramref name="style"/>, as Windows' clock sets its time over its
    /// date: the text's size and each line's height, the largest of the text's sizes at which two lines of
    /// <paramref name="template"/> (the region's widest time where null) fit the card's thickness with a pixel to spare
    /// at each edge; null where none does, or down a bar on its side. Every face of two lines takes them from here
    /// (§497: the now-playing face's title over its artist), so their two lines are the clock's to the pixel.
    /// </summary>
    internal static (float Px, float Line)? TwoLines(Gfx gfx, in IslandStyle style, Func<float, string>? template = null)
    {
        if (style.Vertical)
        {
            return null;
        }
        var fit = new Fit(style);
        var across = MathF.Max(1, style.Cell - (2 * MathF.Max(1, fit.Px(1))));
        foreach (var px in (ReadOnlySpan<float>)[fit.Primary, fit.Secondary, fit.Small])
        {
            var (_, line) = TextLine.Template(gfx, template?.Invoke(px) ?? ClockFaceDrawing.WidestTime(gfx, Plain, px), px, Weight.Regular);
            if (2 * line <= across)
            {
                return (px, line);
            }
        }
        return null;
    }

    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        var gfx = context.Gfx;
        var ink = new IslandInk(context.Style.Look).Text;
        var plan = Plan(gfx, context.Style, context.Face);
        var time = ClockFaceDrawing.Time(Settings(context.Face), DateTime.MinValue);
        var today = Today(context.Face);
        if (context.Vertical)
        {
            var top = TextLine.Round(box.top + ((box.bottom - box.top - plan.Along) / 2));
            foreach (var row in VerticalFit.Parts(time))
            {
                FaceDrawings.Line(dc, gfx, row, plan.TimePx, ink, new D2D_RECT_F(box.left, top, box.right, top + plan.TimeHeight), plan.TimeHeight);
                top += plan.TimeHeight;
            }
            FaceDrawings.Rows(dc, gfx, DateText.SideForms(today, year: true)[plan.Date.Form], plan.Date, ink, box, top + plan.Gap);
            return;
        }
        var shortDate = Nls.ShortDate(today);
        if (!plan.Stacked)
        {
            // One line: the time at the start of the room measured for it, the date at its end.
            var middle = TextLine.Round(box.top + ((box.bottom - box.top - plan.TimeHeight) / 2));
            var timeLine = TextLine.Template(gfx, time, plan.TimePx, Weight.Regular).Width;
            Right(dc, gfx, time, plan.TimePx, ink, box.left, box.left + timeLine + 1, middle);
            Right(dc, gfx, shortDate, plan.DatePx, ink, box.left, box.right, middle);
            return;
        }
        // Windows' two lines: right-aligned at the end of the face, the pair in the middle of the bar.
        // Not turned in a right-to-left language, and no choice (§277): the face is as wide as the
        // wider line, so only the narrower has room to stand in, and it stands where Windows' does.
        var first = TextLine.Round(box.top + ((box.bottom - box.top - plan.TimeHeight - plan.DateHeight) / 2));
        Right(dc, gfx, time, plan.TimePx, ink, box.left, box.right, first);
        Right(dc, gfx, shortDate, plan.DatePx, ink, box.left, box.right, first + plan.TimeHeight);
    }

    /// <summary>One line of text ending at <paramref name="right"/>, in tabular figures, its top at <paramref name="top"/>.</summary>
    private static void Right(ID2D1DeviceContext* dc, Gfx gfx, string text, float px, DXGI_RGBA color, float left, float right, float top)
    {
        ID2D1SolidColorBrush* brush = null;
        var line = TextLine.Lay(gfx, text, px, Weight.Regular, MathF.Max(1, right - left));
        try
        {
            brush = Gfx.Brush(dc, color);
            line.Draw(dc, brush, color, TextLine.Round(MathF.Max(left, right - line.Width)), top);
        }
        finally
        {
            line.Release();
            ComUtil.Release(ref brush);
        }
    }

    /// <summary>The time and the date: a new minute, a second where it shows them, and a new day paint it again.</summary>
    public string Reading(in FaceContext context) =>
        $"{ClockFaceDrawing.Time(Settings(context.Face), DateTime.MinValue)}|{Today(context.Face).ToString("O", System.Globalization.CultureInfo.InvariantCulture)}";

    /// <summary>The long date, as Windows' clock says it on hover; and the zone where it is set to one.</summary>
    public string? Tooltip(in FaceContext context) => CalendarFaceDrawing.Zoned(Nls.LongDate(Today(context.Face)), DateTimeFace.ZoneOf(context.Face));
}
