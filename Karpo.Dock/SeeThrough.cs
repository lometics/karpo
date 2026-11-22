// §629: the add page see-through where its ghost stands behind it (the person, 2026-11-09: "the add widget modal should be
// semi transparent in case the ghost preview is directly behind it. but it should only be semi transparent for the ghost preview
// and not for everything actually.").
//
//  - **Where.** The ghost's own rectangles (§621: one a widget on a bar, Layout.GhostRects; one a line of a ghost folder or of
//    the folder the page adds into, FolderFlyout.ShowGhosts) where they run under the page: each one's part in the page
//    (SeeThroughPlan), in its own rounded shape. Nothing else of the page changes, and nothing else behind it shows.
//  - **How.** The page's ground is its own sprite under the page's drawing (EditDeck: PanelPainter.PaintFlyout without its
//    ground). Between the two, a picture of each window the ghost stands in — the very visuals, a visual surface of its tree, as
//    a carried block's (EditCarry) — cut to each of the ghost's rectangles, at EditLook.SeeThrough over the ground. So the page's
//    ground thins there to the ghost while its tiles, their words and their cards stay over it as they are; the desktop, the
//    other windows, the rest of the bar and of the folder never show. The ghost is drawn live: it fades in and out, glides and
//    grows as the ghost does, at once with Animations off. Pictures only: the page's input is its layout's, so a tile over the
//    ghost takes the pointer and the click as before.

using System.Numerics;
using TerraFX.Interop.Windows;
using Windows.UI;
using Windows.UI.Composition;

namespace Karpo.Dock;

/// <summary>
/// §629: a ghost of the add page's as the page lets it show through: the tree of the window it stands in (<paramref name="Source"/>:
/// a bar's scene, a folder's popup), that window's top-left corner on the screen (<paramref name="Origin"/>, px), each of its
/// rectangles in that window (<paramref name="Rects"/>, px; §621 one a widget, one a line) and their corners' radius (px).
/// </summary>
internal readonly record struct GhostSight(Visual Source, POINT Origin, IReadOnlyList<RectF> Rects, float Radius);

/// <summary>
/// §629: a piece of the add page its ghost shows through: of sight <paramref name="Sight"/>, the part of one of its rectangles in
/// the page (<paramref name="At"/>, page px), that whole rectangle (<paramref name="Whole"/>, page px) and its corners' radius, and
/// where <paramref name="At"/> is in the sight's window (<paramref name="From"/>, px).
/// </summary>
internal readonly record struct SeeThroughCut(int Sight, RectF At, RectF Whole, float Radius, Vector2 From);

/// <summary>§629: where the add page is see-through: its ghost's rectangles ∩ the page.</summary>
internal static class SeeThroughPlan
{
    /// <summary>
    /// The pieces of <paramref name="page"/> (screen px) <paramref name="sights"/> show through, in their order (the later over
    /// the earlier): each of their rectangles' part in the page, none where one stands outside it.
    /// </summary>
    public static List<SeeThroughCut> Of(RECT page, IReadOnlyList<GhostSight> sights)
    {
        List<SeeThroughCut> cuts = [];
        for (var index = 0; index < sights.Count; index++)
        {
            var sight = sights[index];
            foreach (var rect in sights[index].Rects)
            {
                var (x, y) = (sight.Origin.x + rect.X, sight.Origin.y + rect.Y);
                var left = MathF.Max(x, page.left);
                var top = MathF.Max(y, page.top);
                var right = MathF.Min(x + rect.Width, page.right);
                var bottom = MathF.Min(y + rect.Height, page.bottom);
                if (right <= left || bottom <= top)
                {
                    continue;
                }
                cuts.Add(new SeeThroughCut(
                    index,
                    new RectF(left - page.left, top - page.top, right - left, bottom - top),
                    new RectF(x - page.left, y - page.top, rect.Width, rect.Height),
                    MathF.Max(0, MathF.Min(sight.Radius, MathF.Min(rect.Width, rect.Height) / 2)),
                    new Vector2(left - sight.Origin.x, top - sight.Origin.y)));
            }
        }
        return cuts;
    }
}

/// <summary>
/// §629: what the add page shows under its drawing (see the top of the file): its ground, and over it the ghost where it stands
/// behind the page, each piece a picture of the ghost's window cut to the ghost's shape; made as first placed, its pieces as first
/// asked for and kept.
/// </summary>
internal sealed class SeeThroughView : IDisposable
{
    private ContainerVisual? holder;
    private SpriteVisual? ground;
    private CompositionColorBrush? groundInk;

    /// <summary>The pieces, at <see cref="EditLook.SeeThrough"/> over the ground.</summary>
    private ContainerVisual? pieces;

    private readonly List<Piece> made = [];

    private sealed record Piece(SpriteVisual Sprite, CompositionVisualSurface Surface, CompositionSurfaceBrush Brush, CompositionRoundedRectangleGeometry Shape, CompositionGeometricClip Clip);

    /// <summary>Tests only: the pieces shown now (page px), in their order.</summary>
    internal IReadOnlyList<SeeThroughCut> Shown { get; private set; } = [];

    /// <summary>
    /// Under <paramref name="drawing"/> in <paramref name="into"/> (the page's content), <paramref name="size"/> px: the ground in
    /// <paramref name="ink"/>, and over it each of <paramref name="sights"/> where it runs under the page at <paramref name="page"/>
    /// (screen px).
    /// </summary>
    public void Place(Compositor c, ContainerVisual into, Visual drawing, Vector2 size, Color ink, RECT page, IReadOnlyList<GhostSight> sights)
    {
        if (holder is null)
        {
            holder = c.CreateContainerVisual();
            groundInk = c.CreateColorBrush();
            ground = c.CreateSpriteVisual();
            ground.Brush = groundInk;
            holder.Children.InsertAtTop(ground);
            pieces = c.CreateContainerVisual();
            pieces.Opacity = EditLook.SeeThrough;
            holder.Children.InsertAtTop(pieces);
        }
        if (!ReferenceEquals(holder.Parent, into))
        {
            try
            {
                holder.Parent?.Children.Remove(holder);
            }
            catch (Exception error) when (error is ObjectDisposedException or System.Runtime.InteropServices.COMException)
            {
                // The page it stood in was let go of: its tree with it.
            }
            into.Children.InsertBelow(holder, drawing);
        }
        holder.Size = size;
        ground!.Size = size;
        groundInk!.Color = ink;
        Cut(c, SeeThroughPlan.Of(page, sights), sights);
    }

    /// <summary>Nothing shows through any more, and nothing of the windows it showed is held.</summary>
    public void Clear() => Cut(null, [], []);

    private void Cut(Compositor? c, List<SeeThroughCut> cuts, IReadOnlyList<GhostSight> sights)
    {
        while (c is not null && pieces is not null && made.Count < cuts.Count)
        {
            var surface = c.CreateVisualSurface();
            var brush = c.CreateSurfaceBrush(surface);
            brush.Stretch = CompositionStretch.None;
            brush.HorizontalAlignmentRatio = 0;
            brush.VerticalAlignmentRatio = 0;
            var shape = c.CreateRoundedRectangleGeometry();
            var clip = c.CreateGeometricClip(shape);
            var sprite = c.CreateSpriteVisual();
            sprite.Brush = brush;
            sprite.Clip = clip;
            pieces.Children.InsertAtTop(sprite);
            made.Add(new Piece(sprite, surface, brush, shape, clip));
        }
        for (var index = 0; index < made.Count; index++)
        {
            var piece = made[index];
            try
            {
                if (index >= cuts.Count)
                {
                    piece.Sprite.IsVisible = false;
                    piece.Surface.SourceVisual = null;
                    continue;
                }
                var cut = cuts[index];
                var source = sights[cut.Sight].Source;
                if (!ReferenceEquals(piece.Surface.SourceVisual, source))
                {
                    piece.Surface.SourceVisual = source;
                }
                piece.Surface.SourceOffset = cut.From;
                piece.Surface.SourceSize = cut.At.Size;
                piece.Sprite.Offset = new Vector3(cut.At.X, cut.At.Y, 0);
                piece.Sprite.Size = cut.At.Size;
                // In the ghost's own shape: its whole rectangle's, corners and all, of which the piece is the part in the page.
                piece.Shape.Offset = new Vector2(cut.Whole.X - cut.At.X, cut.Whole.Y - cut.At.Y);
                piece.Shape.Size = cut.Whole.Size;
                piece.Shape.CornerRadius = new Vector2(cut.Radius, cut.Radius);
                piece.Sprite.IsVisible = true;
            }
            catch (Exception error) when (error is ObjectDisposedException or System.Runtime.InteropServices.COMException)
            {
                // The window it showed was let go of: nothing of it to show.
                piece.Sprite.IsVisible = false;
            }
        }
        Shown = cuts;
    }

    public void Dispose()
    {
        foreach (var piece in made)
        {
            piece.Surface.SourceVisual = null;
            piece.Sprite.Dispose();
            piece.Clip.Dispose();
            piece.Shape.Dispose();
            piece.Brush.Dispose();
            piece.Surface.Dispose();
        }
        made.Clear();
        pieces?.Dispose();
        ground?.Dispose();
        groundInk?.Dispose();
        holder?.Dispose();
        (holder, pieces, ground, groundInk) = (null, null, null, null);
        Shown = [];
    }
}
