// edit: one look for edit mode, the bar's (Scene) and an open folder's (FolderFlyout) alike (the person,
// 2026-10-12: "the editor for the folder view and the editor for the taskbar are different but still i
// want the overall look and feel to be the same ... animations ... dnd behaviour"). Each drew its own
// from numbers typed in twice, and they had drifted: the folder's rings stood outside its lines where the
// bar's stand inside its blocks, its hairline was the scale rounded where the bar's is floored, its
// outline another ink, its lifted line opaque, its hover instant. The numbers are here once, and both
// read them (docs\widgets.md, "One edit mode").
//
//  - **Motion.** A move — a block or a line giving way, settling, gliding to its new place, a ghost
//    following the gap — is the one motion's Move, a hover's wash its Hover (Motion.cs, §489).
//  - **Lifted.** What is carried is a little see-through (LiftOpacity) over the shadow Windows 11 gives a
//    floating surface (blur LiftShadowBlurDip, LiftShadowOffsetDip down, LiftShadowBoost times the
//    surface's own shadow), out of its place's way.
//  - **Rings.** The selected ring is twice the hairline, on the item's edge, inside it; the focus
//    rectangle the same outside band in the focus ink with a hairline inside it in the window ink, as
//    Windows draws its own. One hairline everywhere: the scale floored, at least a px (Hair).
//  - **Ink.** The outline every block on the bar wears while arranged, brighter under the pointer (an
//    open folder's lines wear none: only the selected one its ring, the person, 2026-10-23); the wash under
//    the pointer; the ghost where a carried item would land: the accent, faint, edged in the accent.
//
// Plain numbers and arithmetic, so the tests can hold both surfaces to them.

using System.Numerics;

namespace Karpo.Dock;

/// <summary>Edit mode's look, the bar's and an open folder's; see the top of the file.</summary>
public static class EditLook
{
    /// <summary>How much of what is carried shows: a little see-through, so what it passes over shows through.</summary>
    public const float LiftOpacity = 0.9f;

    /// <summary>How much of a line carried out of its folder shows, where nothing would take it.</summary>
    public const float CarriedOutOpacity = 0.6f;

    /// <summary>
    /// §629: how much of the add page's ghost shows through the page where it stands behind it — over the page's ground, under its
    /// tiles (SeeThroughView): the page half see-through there, its tiles and words whole.
    /// </summary>
    public const float SeeThrough = 0.5f;

    /// <summary>The lifted shadow's blur, in DIPs.</summary>
    public const float LiftShadowBlurDip = 12;

    /// <summary>How far down the lifted shadow falls, in DIPs.</summary>
    public const float LiftShadowOffsetDip = 4;

    /// <summary>How much darker the lifted shadow is than the surface's own (a tooltip's, a flyout's).</summary>
    public const float LiftShadowBoost = 1.4f;

    /// <summary>The lifted shadow's opacity, from the surface's own (<paramref name="surface"/>): none where it has none.</summary>
    public static float LiftShadow(float surface) => MathF.Min(1, surface * LiftShadowBoost);

    /// <summary>Edit mode's hairline at <paramref name="scale"/>: the scale floored, at least a px — crisp on whole pixels.</summary>
    public static float Hair(float scale) => MathF.Max(1, MathF.Floor(scale));

    /// <summary>The selected ring's thickness, and the focus rectangle's outer band's, for a hairline of <paramref name="hair"/>.</summary>
    public static float Ring(float hair) => 2 * hair;

    /// <summary>The outline's ink's alpha: white on the dark look, black on the light one; the text's own in high contrast.</summary>
    public static float OutlineAlpha(bool dark, bool highContrast = false) => highContrast ? 1 : dark ? 0.28f : 0.24f;

    /// <summary>The outline's alpha under the pointer.</summary>
    public static float OutlineStrongAlpha(bool dark, bool highContrast = false) => highContrast ? 1 : dark ? 0.60f : 0.50f;

    /// <summary>The ghost's fill: the accent at this alpha.</summary>
    public static float GhostAlpha(bool dark) => dark ? 0.16f : 0.14f;

    /// <summary>
    /// A stroke <paramref name="thickness"/> px wide, all of it inside a box of <paramref name="size"/> with
    /// corners of <paramref name="radius"/>, <paramref name="inward"/> px in from its edge: the outline it
    /// is centred on — where it starts, how big it is, its corners'. On whole pixels where the box is.
    /// </summary>
    public static (Vector2 Offset, Vector2 Size, float Corner) Stroke(Vector2 size, float radius, float thickness, float inward = 0)
    {
        var inset = inward + thickness / 2;
        return (
            new Vector2(inset, inset),
            new Vector2(MathF.Max(size.X - 2 * inset, 0), MathF.Max(size.Y - 2 * inset, 0)),
            MathF.Max(0, radius - inset));
    }

    /// <summary>
    /// §621: where rectangle <paramref name="index"/> of a ghost of <paramref name="count"/> rectangles — one round each widget
    /// or line it holds, never one round them all — grows from, the ghost having stood as <paramref name="was"/>: its own
    /// rectangle there, where the ghost has as many; else, one turning into several or several into one (the add page's empty
    /// ghost into a pick of a few), the room all of them took, so the one rectangle splits into the widgets' and they gather
    /// into one again; none where no ghost stood, which is put where it goes at once. The bar's (Scene.ShowGhost) and an open
    /// folder's (FolderFlyout.ShowGhosts) alike.
    /// </summary>
    public static RectF? GhostFrom(IReadOnlyList<RectF> was, int count, int index) =>
        was.Count == 0 ? null : was.Count == count ? was[index] : Around(was);

    /// <summary>The room <paramref name="rects"/> take together: the least rectangle round them all; empty for none.</summary>
    public static RectF Around(IReadOnlyList<RectF> rects)
    {
        if (rects.Count == 0)
        {
            return default;
        }
        var (left, top, right, bottom) = (rects[0].X, rects[0].Y, rects[0].Right, rects[0].Bottom);
        foreach (var rect in rects)
        {
            (left, top, right, bottom) = (MathF.Min(left, rect.X), MathF.Min(top, rect.Y), MathF.Max(right, rect.Right), MathF.Max(bottom, rect.Bottom));
        }
        return new RectF(left, top, right - left, bottom - top);
    }
}
