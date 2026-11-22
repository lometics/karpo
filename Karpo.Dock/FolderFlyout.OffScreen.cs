// edit: part of the arranged folder is off screen (§320). No column's width, move or add is refused
// for making the folder wider than its display, and nothing narrows a column to fit it: the folder
// is placed as a narrower one is — centred on its face, kept inside the work area while it fits —
// and what does not fit runs past the screen's edge. Arranged, a chip in Windows' warning colours
// says so on the first column it cuts, against that edge ("Part of this folder is off screen"); it
// takes no press, and goes as soon as everything fits again. At rest nothing is said.

using System.Numerics;
using Karpo.Core;
using TerraFX.Interop.DirectX;
using Windows.UI.Composition;

namespace Karpo.Dock;

public sealed unsafe partial class FolderFlyout
{
    /// <summary>edit: the off-screen chip's least height, in DIPs: a compact control's.</summary>
    private const float WarningTallDip = 32;

    /// <summary>edit: the room either side of the chip's glyph and words, in DIPs.</summary>
    private const float WarningPadDip = 12;

    /// <summary>edit: the chip's warning glyph, in DIPs, and the room between it and the words.</summary>
    private const float WarningGlyphDip = 16;

    private const float WarningSpaceDip = 8;

    // edit: the chip, made with the folder's frame (§357, FolderFlyout.Retained.cs), drawn the first time a
    // column is off screen while the folder is arranged, and what it was drawn for.
    private SpriteVisual? warningVisual;
    private CompositionSurfaceBrush? warningBrush;
    private CompositionDrawingSurface? warningSurface;
    private string warningDrawn = "";

    /// <summary>For tests: where the off-screen chip stands now, in panel px; nothing where it is not shown.</summary>
    internal D2D_RECT_F WarningShown { get; private set; }

    /// <summary>
    /// edit: the off-screen chip where <paramref name="shown"/> has a column off screen
    /// (<see cref="FolderPlan.OffScreen"/>, <see cref="WarningBox"/>), drawn again only where its
    /// words, size or colours changed; hidden where none is.
    /// </summary>
    private void PlaceWarning(FolderPlan shown)
    {
        if (!shown.Editing || shown.OffScreen.Count == 0 || gfx is not { } drawer)
        {
            WarningShown = default;
            if (warningVisual is { } gone)
            {
                Try(() => gone.IsVisible = false);
            }
            return;
        }
        var room = Visible();
        var text = S.DockFolderOffScreen;
        var size = WarningSize(drawer, text, shown.TextPx, scale, room.right - room.left - (2 * PopupPaint.Round(WarningMarginDip * scale)));
        var box = WarningBox(shown, room, size, scale);
        var colours = theme;
        Try(() =>
        {
            if (warningVisual is null || warningBrush is null)
            {
                return;
            }
            var key = $"{text}|{size}|{shown.TextPx}|{colours.HighContrast}|{colours.Accent}";
            if (key != warningDrawn)
            {
                warningSurface = Paint(warningSurface, size.X, size.Y, dc => PaintWarning(dc, drawer, PopupPaint.Rect(0, 0, size.X, size.Y), colours, text, shown.TextPx, scale), warningBrush);
                warningDrawn = key;
            }
            warningVisual.Size = size;
            warningVisual.Offset = new Vector3(box.left, box.top, 0);
            warningVisual.IsVisible = true;
        });
        WarningShown = box;
    }

    /// <summary>
    /// edit: the widths list <paramref name="list"/> of the arranged folder would run it off the
    /// screen at, for its width menu to mark (§328; <see cref="RunsOff"/>); none at rest, before it is
    /// placed, and for a slot (a negative list), which is outside the lists with widgets.
    /// </summary>
    internal IReadOnlyList<FolderListWidth> RunsOffAt(int list) =>
        editing && host.Spread is not null && plan is { Editing: true } shown
            ? [.. FolderListWidths.All.Where(width => RunsOff(shown, list, width, scale, host.Placement.CoreRoom(scale)))]
            : [];

    /// <summary>
    /// edit: whether list <paramref name="list"/> of <paramref name="plan"/>, made <paramref name="width"/>
    /// wide, runs the folder's lists with widgets past the screen's edge (§328): their box
    /// (<see cref="FolderPlan.Core"/>) as much longer or shorter as the list gets, against
    /// <paramref name="room"/> px, the most it may be where it is placed (FlyoutPlacement.CoreRoom) —
    /// what <see cref="FolderPlan.OffScreen"/> then says, asked before the width is picked. Never for a
    /// list outside the lists with widgets, which is never said to be off screen either. Pure.
    /// </summary>
    internal static bool RunsOff(FolderPlan plan, int list, FolderListWidth width, float scale, float room)
    {
        var holding = Enumerable.Range(0, plan.Lists.Count).Where(at => plan.Lists[at].Runs.Count > 0).ToList();
        if (holding.Count == 0 || list < holding[0] || list > holding[^1])
        {
            return false;
        }
        var core = plan.Core.right - plan.Core.left + ListWidth(scale, width) - plan.Lists[list].Width;
        return core > room + 0.5f;
    }

    /// <summary>
    /// edit: how large the off-screen chip is for <paramref name="text"/> at <paramref name="textPx"/>,
    /// in px at <paramref name="scale"/>: its glyph and its words with their room, at least a compact
    /// control's height, never wider than <paramref name="most"/> — its words cut with an ellipsis there.
    /// </summary>
    internal static Vector2 WarningSize(Gfx gfx, string text, float textPx, float scale, float most)
    {
        float D(float dip) => PopupPaint.Round(dip * scale);
        var around = (2 * D(WarningPadDip)) + D(WarningGlyphDip) + D(WarningSpaceDip);
        var words = PopupPaint.Measure(gfx, text, textPx, Weight.Regular, MathF.Max(1, most - around));
        return new Vector2(MathF.Ceiling(around + words.X), MathF.Max(D(WarningTallDip), MathF.Ceiling(words.Y + (2 * D(6)))));
    }

    /// <summary>
    /// edit: the off-screen chip in <paramref name="box"/> (px of its surface): Karpo's warning — the
    /// edit bar's "not shown" orange, as a column's warning button wears it (EditRail.Warning) — in a
    /// control's rounded corners with its stroke, the warning glyph and <paramref name="text"/> in its
    /// ink. High contrast: the system's highlight and its text.
    /// </summary>
    internal static void PaintWarning(ID2D1DeviceContext* dc, Gfx gfx, D2D_RECT_F box, FlyoutTheme theme, string text, float textPx, float scale)
    {
        float D(float dip) => PopupPaint.Round(dip * scale);
        var warning = EditRail.Warning(theme);
        var radius = D(4);
        var hair = MathF.Max(1, MathF.Round(scale));
        PopupPaint.Fill(dc, box, radius, FlyoutTheme.Ink(warning.Fill));
        PopupPaint.Stroke(dc, PopupPaint.Rect(box.left + (hair / 2), box.top + (hair / 2), box.right - box.left - hair, box.bottom - box.top - hair), radius, hair, FlyoutTheme.Ink(warning.Stroke));
        var left = box.left + D(WarningPadDip);
        var glyph = D(WarningGlyphDip);
        var middle = (box.top + box.bottom) / 2;
        PopupPaint.Glyph(dc, gfx, EditRailPlan.Glyphs.NotShown, glyph, warning.Glyph, PopupPaint.Rect(left, middle - (glyph / 2), glyph, glyph));
        var from = left + glyph + D(WarningSpaceDip);
        var room = MathF.Max(1, box.right - D(WarningPadDip) - from);
        var size = PopupPaint.Measure(gfx, text, textPx, Weight.Regular, room);
        PopupPaint.Text(dc, gfx, text, textPx, Weight.Regular, warning.Glyph, from, middle - (size.Y / 2), room);
    }
}
