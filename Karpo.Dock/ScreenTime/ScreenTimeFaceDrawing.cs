// The screen-time face (Karpo.Core\FolderFaces.cs, docs\widgets.md §504): the screen-time glyph and
// how long this PC was used today, "3 h 42 min", as the screen-time line says a day's total
// (ScreenTimeText.Duration). Read from the one tracker every screen-time widget reads
// (ScreenTimeTracker), which counts from the dock's start whether or not anything shows it; always
// today's, whatever day the folder's lines show. Nothing ticks for it but the dock's minute, on
// which its Reading moves with the total.
//
// Across a bar the glyph and the total as it reads now (§244): a minute more moves it only where a
// figure more or fewer is said ("59 min" to "1 h"). Down a bar on its side the glyph over the total,
// on one row where its widest ("88 h 88 min") fits across the bar, else the hours over the minutes,
// else a word a row (VerticalFit.Phrase, §598) — never cut.

using Karpo.Core;
using Karpo.Dock.Native;
using Karpo.Dock.Panels;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>Today's screen time: its glyph and the day's total.</summary>
internal sealed unsafe class ScreenTimeFaceDrawing : IFaceDrawing
{
    /// <summary>The days a test reads in place of the tracker's, on its own thread; null for the tracker's.</summary>
    [ThreadStatic]
    internal static Func<ScreenTimeSnapshot>? Stand;

    public string Id => FolderFaces.ScreenTimeId;

    public bool Seconds(FolderFace face) => false;

    /// <summary>How long this PC was used today, up to now: the tracker's count, or a test's; nothing where nothing counts.</summary>
    internal static TimeSpan Today()
    {
        var days = (Stand ?? ScreenTimeTracker.Shared.Snapshot)();
        return days.Log.TotalOn(days.Today);
    }

    /// <summary>What the face says for <paramref name="total"/>: the screen-time line's words for a day's total.</summary>
    internal static string Said(TimeSpan total) => ScreenTimeText.Duration(total);

    /// <summary>Down a vertical bar: the rows the widest total ("88 h 88 min", "&lt; 1 min") stands in across the bar.</summary>
    internal static VerticalFit.SideRows Stacked(in FaceContext context) =>
        VerticalFit.Rows(context.Gfx, context.Fit, VerticalFit.Phrase, [ScreenTimeText.DurationShape, S.WidgetScreenTimeUnderMinute]);

    public FaceSize Measure(in FaceContext context)
    {
        var fit = context.Fit;
        var glyph = fit.Glyph();
        if (context.Vertical)
        {
            var plan = Stacked(context);
            return new FaceSize(MathF.Ceiling(glyph + fit.Leading + plan.LengthOf(VerticalFit.Phrase[plan.Form](Said(Today())))), Square: false);
        }
        return new FaceSize(MathF.Ceiling(glyph + fit.Gap + TextLine.Template(context.Gfx, Said(Today()), fit.Primary, Weight.Regular).Width), Square: false);
    }

    /// <summary>The total as it reads: a minute that changes the words paints it again.</summary>
    public string Reading(in FaceContext context) => Said(Today());

    /// <summary>"Screen time — 3 h 42 min", to a screen reader.</summary>
    public string? Tooltip(in FaceContext context) => S.WidgetScreenTimeTip(name: WidgetInfos.ScreenTime.Title, time: Said(Today()));

    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        var fit = context.Fit;
        var ink = new IslandInk(context.Style.Look);
        var gfx = context.Gfx;
        var width = box.right - box.left;
        var height = box.bottom - box.top;
        var glyph = fit.Glyph();
        var said = Said(Today());
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        if (context.Vertical)
        {
            var plan = Stacked(context);
            var rows = VerticalFit.Phrase[plan.Form](said);
            var top = box.top + TextLine.Round((height - (glyph + fit.Leading + plan.LengthOf(rows))) / 2);
            PopupPaint.Glyph(dc, gfx, ScreenTimeLine.Glyph, glyph, ink.Text, PopupPaint.Rect(box.left + ((width - glyph) / 2), top, glyph, glyph));
            FaceDrawings.Rows(dc, gfx, rows, plan, ink.Text, box, top + glyph + fit.Leading);
            return;
        }
        FaceDrawings.GlyphAndName(dc, context, box, ScreenTimeLine.Glyph, said, ink.Text);
    }
}
