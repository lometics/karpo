// edit: the widget being carried in edit mode, at the pointer, wherever on the screen the pointer is
// (the person, 2026-10-07: "the widget follows the mouse"). A bar's window is its strip along the
// edge, and nothing of it can be drawn past that, so the block it lifts (Scene.Lift) is drawn again
// in a window of its own that goes with the pointer (PointerWindow, §363: it stands still, the block moved
// in it in the commit the bar's ghost moves in): a visual surface of the lifted block, the very visuals, so
// it is the widget as it is right now. The bar keeps the block out of sight meanwhile,
// and where it would land a blue ghost of its size stands (Scene.ShowGhost).
//
// The window takes no activation and no pointer: the bar the drag began on holds the mouse
// throughout, and asks the others where the block would land (Dock.BarOffer).
//
// Several widgets carried together (Dock.EditSelect.cs) are each drawn so, side by side in the order
// they will land, the one taken hold of under the pointer where it was taken: a ghost per widget,
// whichever surface it came from; on any display (§519).

using System.Numerics;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>edit: the window a carried block goes with the pointer in; made with the first drag.</summary>
    private EditCarry? carry;

    /// <summary>
    /// edit: the block <paramref name="block"/> of <paramref name="source"/> (window px), the visual a
    /// bar lifted it into, at <paramref name="at"/> on the screen, its top left corner.
    /// </summary>
    private void ShowCarry(Visual source, RectF block, POINT at)
    {
        try
        {
            carry ??= new EditCarry(gfx, instance);
            carry.Show(source, block, at);
        }
        catch (Exception error)
        {
            Report(error);
        }
    }

    /// <summary>
    /// edit: several blocks carried together (<paramref name="pieces"/>, each the visual a surface
    /// lifted it into and its block there, in the order they will land) side by side along
    /// <paramref name="vertical"/>'s axis, <paramref name="gap"/> px apart, the one of index
    /// <paramref name="held"/> with its top left corner at <paramref name="at"/> on the screen.
    /// </summary>
    private void ShowCarry(IReadOnlyList<(Visual Source, RectF Block)> pieces, bool vertical, int held, float gap, POINT at)
    {
        try
        {
            carry ??= new EditCarry(gfx, instance);
            carry.Show(pieces, vertical, held, gap, at);
        }
        catch (Exception error)
        {
            Report(error);
        }
    }

    /// <summary>edit: no block is carried any more.</summary>
    private void HideCarry() => carry?.Hide();
}

/// <summary>The window a block carried in edit mode is drawn in, at the pointer (see the top of the file).</summary>
internal sealed unsafe class EditCarry : IDisposable
{
    private readonly PointerWindow window;
    private readonly SpriteVisual picture;
    private readonly CompositionVisualSurface surface;
    private readonly CompositionSurfaceBrush brush;
    private readonly Compositor compositor;

    /// <summary>The second and later blocks of several carried together, made as they are first needed.</summary>
    private readonly List<(CompositionVisualSurface Surface, CompositionSurfaceBrush Brush, SpriteVisual Picture)> more = [];

    public EditCarry(Gfx gfx, HINSTANCE instance)
    {
        window = new PointerWindow(gfx, instance, "Karpo carry"); // not-ui: a window's title
        var c = compositor = gfx.Compositor;
        surface = c.CreateVisualSurface();
        brush = c.CreateSurfaceBrush(surface);
        brush.Stretch = CompositionStretch.None;
        picture = c.CreateSpriteVisual();
        picture.Brush = brush;
        window.Content.Children.InsertAtTop(picture);
    }

    public HWND Hwnd => window.Hwnd;

    /// <summary>The window under the pointer, for a test.</summary>
    internal PointerWindow Window => window;

    /// <summary>Shows <paramref name="block"/> of <paramref name="source"/> with its top left corner at <paramref name="at"/> (screen px).</summary>
    public void Show(Visual source, RectF block, POINT at) => Show([(source, block)], false, 0, 0f, at);

    /// <summary>
    /// Shows each of <paramref name="pieces"/> side by side along <paramref name="vertical"/>'s axis,
    /// <paramref name="gap"/> px apart and lined up on their middles across it, the piece of index
    /// <paramref name="held"/> with its top left corner at <paramref name="at"/> (screen px): moved in the
    /// frame's commit, with the bar's ghost where it would land (§363).
    /// </summary>
    public void Show(IReadOnlyList<(Visual Source, RectF Block)> pieces, bool vertical, int held, float gap, POINT at)
    {
        if (pieces.Count == 0)
        {
            Hide();
            return;
        }
        var places = EditCarryPlan.Row([.. pieces.Select(piece => piece.Block.Size)], vertical, gap);
        var width = Math.Max(1, (int)MathF.Ceiling(places.Size.X));
        var height = Math.Max(1, (int)MathF.Ceiling(places.Size.Y));
        var grabbed = places.At[Math.Clamp(held, 0, pieces.Count - 1)];
        var (x, y) = (at.x - (int)MathF.Round(grabbed.X), at.y - (int)MathF.Round(grabbed.Y));
        // Shown before what it shows is set: a hide still waiting lets go of its pictures now, not after.
        window.Show(new RECT { left = x, top = y, right = x + width, bottom = y + height });
        while (more.Count < pieces.Count - 1)
        {
            var extra = compositor.CreateVisualSurface();
            var extraBrush = compositor.CreateSurfaceBrush(extra);
            extraBrush.Stretch = CompositionStretch.None;
            var extraPicture = compositor.CreateSpriteVisual();
            extraPicture.Brush = extraBrush;
            window.Content.Children.InsertAtTop(extraPicture);
            more.Add((extra, extraBrush, extraPicture));
        }
        for (var index = 0; index < more.Count; index++)
        {
            more[index].Picture.IsVisible = index < pieces.Count - 1;
            if (index >= pieces.Count - 1)
            {
                more[index].Surface.SourceVisual = null;
            }
        }
        for (var index = 0; index < pieces.Count; index++)
        {
            var (source, block) = pieces[index];
            var (into, sprite) = index == 0 ? (surface, picture) : (more[index - 1].Surface, more[index - 1].Picture);
            if (!ReferenceEquals(into.SourceVisual, source))
            {
                into.SourceVisual = source;
            }
            var wide = new Vector2(MathF.Max(1f, MathF.Ceiling(block.Width)), MathF.Max(1f, MathF.Ceiling(block.Height)));
            into.SourceOffset = new Vector2(block.X, block.Y);
            into.SourceSize = wide;
            sprite.Size = wide;
            sprite.Offset = new Vector3(places.At[index].X, places.At[index].Y, 0f);
        }
    }

    /// <summary>
    /// Clear in the frame's commit — the one its bar draws the block dropped in, where it landed (§354,
    /// §359) — out of sight once that is on the screen, and only then its pictures let go of.
    /// </summary>
    public void Hide() => window.Hide(() =>
    {
        surface.SourceVisual = null;
        foreach (var (extra, _, _) in more)
        {
            extra.SourceVisual = null;
        }
    });

    public void Dispose() => window.Dispose(() =>
    {
        foreach (var (extra, extraBrush, extraPicture) in more)
        {
            extraPicture.Dispose();
            extraBrush.Dispose();
            extra.Dispose();
        }
        more.Clear();
        picture.Dispose();
        brush.Dispose();
        surface.Dispose();
    });
}

/// <summary>
/// edit: where several blocks carried together stand in the carry window (Dock.EditCarry.cs), apart
/// from any window: side by side along the bar's axis in their order, <c>gap</c> px apart, lined up
/// on their middles across it, as they will land. Pure, so it can be tested.
/// </summary>
public static class EditCarryPlan
{
    /// <summary>
    /// Each of blocks <paramref name="sizes"/> (px) in a row along <paramref name="vertical"/>'s axis,
    /// <paramref name="gap"/> px apart: each one's top left corner, and the size of the whole.
    /// </summary>
    public static (IReadOnlyList<Vector2> At, Vector2 Size) Row(IReadOnlyList<Vector2> sizes, bool vertical, float gap)
    {
        var across = sizes.Count == 0 ? 0f : sizes.Max(size => vertical ? size.X : size.Y);
        var at = new List<Vector2>(sizes.Count);
        var along = 0f;
        foreach (var size in sizes)
        {
            var thick = vertical ? size.X : size.Y;
            var middle = MathF.Floor((across - thick) / 2f);
            at.Add(vertical ? new Vector2(middle, along) : new Vector2(along, middle));
            along += (vertical ? size.Y : size.X) + gap;
        }
        along = sizes.Count == 0 ? 0f : along - gap;
        return (at, vertical ? new Vector2(across, along) : new Vector2(along, across));
    }
}
