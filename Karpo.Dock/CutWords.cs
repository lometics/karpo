// Words drawn cut short, said whole in a tooltip (the person, 2026-11-03: "when hovering over text that is partially hidden
// (like when i search for vanguard ftse all world etfs for example) i would like the tooltip to show the text after hovering
// for a while over it").
//
// One rule, the kit's: a line is cut where DirectWrite trimmed it — its ellipsis drawn at the room it was given
// (Gfx.TextLayout's trimming). Nothing else decides it, so no widget says it of itself:
//
//  - **Painted lines** — a folder's lines, a bar's faces — go through the kit's two places that draw a line
//    (TextLine.Draw, PopupPaint's Draw). While a surface paints, its host listens (While): every line drawn cut is
//    remembered with its whole words and its box, in the painter's own coordinates, the box the pointer is then looked up
//    in (At). A mirrored line's box is its unmirrored one, as the pointer is mirrored back into it (FolderFlyout.CardSpace).
//  - **A menu's rows** (Flyout) draw each of their words once, kept and shared, so they ask the rule as they lay a row
//    out (Gfx.Cuts): the row's words, and what it is set to.
//
// The tooltip is the host's own — a menu's, a folder's, the bar's — after Windows' hover time, as their buttons' are. A part
// with a tooltip of its own keeps it; the whole words are said where it has none.

using System.Numerics;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>A line drawn cut short (<see cref="CutWords"/>): its whole words, and its box in the painter's own coordinates.</summary>
internal readonly record struct CutRun(string Text, D2D_RECT_F Box);

internal static unsafe class CutWords
{
    /// <summary>The lines cut while a host listens on this thread (<see cref="While"/>); null while none does.</summary>
    [ThreadStatic]
    private static List<CutRun>? heard;

    /// <summary>
    /// Whether <paramref name="layout"/> is cut: DirectWrite trimmed its line at the room it was given, its ellipsis drawn.
    /// The one rule (see the top of the file).
    /// </summary>
    public static bool Trimmed(IDWriteTextLayout* layout)
    {
        if (layout == null)
        {
            return false;
        }
        DWRITE_LINE_METRICS line;
        uint lines;
        return layout->GetLineMetrics(&line, 1, &lines).SUCCEEDED && lines >= 1 && line.isTrimmed;
    }

    /// <summary>
    /// The lines <paramref name="paint"/> draws cut short on this thread, each with its whole words and its box; none where
    /// nothing was cut. A host listens while it paints a surface the pointer can rest on.
    /// </summary>
    public static IReadOnlyList<CutRun> While(Action paint)
    {
        var was = heard;
        List<CutRun> runs = [];
        heard = runs;
        try
        {
            paint();
        }
        finally
        {
            heard = was;
        }
        return runs;
    }

    /// <summary>
    /// <paramref name="layout"/>, made from <paramref name="text"/>, drawn at <paramref name="origin"/>: remembered where it
    /// is cut and a host listens. <paramref name="cut"/>: a text the kit fitted itself (PopupPaint.Wrapped's lines, its last
    /// ending in an ellipsis), where it said so; else DirectWrite's trimming says. Called by the kit's places that draw a
    /// line, nowhere else.
    /// </summary>
    public static void Drawn(IDWriteTextLayout* layout, string? text, D2D_POINT_2F origin, bool? cut = null)
    {
        if (heard is not { } runs || string.IsNullOrEmpty(text) || layout == null || !(cut ?? Trimmed(layout)))
        {
            return;
        }
        DWRITE_TEXT_METRICS metrics;
        if (layout->GetMetrics(&metrics).FAILED)
        {
            return;
        }
        var left = origin.x + metrics.left;
        var top = origin.y + metrics.top;
        runs.Add(new CutRun(text, new D2D_RECT_F(left, top, left + metrics.width, top + metrics.height)));
    }

    /// <summary>
    /// What a part's tooltip says: its own (<paramref name="own"/>, a button's name and shortcut), and where its words were
    /// cut (<paramref name="cut"/>) those whole on the first line, so they are always said somewhere; null for neither.
    /// </summary>
    public static string? Said(string? cut, string? own) =>
        cut is null ? own : own is null || own == cut ? cut : $"{cut}\n{own}";

    /// <summary>The cut line under <paramref name="point"/> (the painter's coordinates), its whole words and its box; null where none is.</summary>
    public static CutRun? At(IReadOnlyList<CutRun>? runs, Vector2 point)
    {
        if (runs is null)
        {
            return null;
        }
        // The last drawn is on top.
        for (var at = runs.Count - 1; at >= 0; at--)
        {
            var box = runs[at].Box;
            if (point.X >= box.left && point.X < box.right && point.Y >= box.top && point.Y < box.bottom)
            {
                return runs[at];
            }
        }
        return null;
    }
}
