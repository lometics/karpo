// edit: the buttons of edit mode's own bars kept, not made again (docs\widgets.md §363; the flicker design's
// rule R1 for the edit bar and the sections' edit bars). Both drew every button afresh whenever anything of
// theirs changed — a crumb's words, Undo greyed, a crumb come or gone, the rail moved with its section: the
// host's content emptied and every shape, glyph and words made and drawn again. Now each thing such a bar
// shows is an element kept by a key — a button by its part, a › between two crumbs, a separator, the focus
// ring — with what it was drawn from (its signature: words, glyph, ink, fill, rim, size and scale; never
// where it stands):
//
//  - **The same** is kept as it is, moved to where it stands now; nothing is made or drawn.
//  - **Changed** is made again, alone, in the same commit; its fill brush is the owner's to light again.
//  - **Gone** is let go of, in the same commit.
//
// The elements are the bar's, not its host's showing's (FlyoutHost.Keep): they stay through the bar going out
// of sight and coming back (the edit bar from one edit mode to the next, a rail with nothing to say), its root put back
// into the host's content as a showing empties it, and go to the host only as the bar goes for good.

using System.Numerics;
using Windows.UI.Composition;
using Color = Windows.UI.Color;

namespace Karpo.Dock;

/// <summary>A button of edit mode's own bars, kept by its key (see the top of the file).</summary>
internal sealed class KeptButton
{
    /// <summary>Where it stands in the panel (px): the container its pieces stand in, at its corner.</summary>
    public required ContainerVisual Visual { get; init; }

    /// <summary>Its fill, lit and pressed by its owner; null for what has none.</summary>
    public CompositionColorBrush? Fill { get; internal set; }

    /// <summary>What its owner keeps with it: the rail's "not shown" mark, the focus ring's pieces.</summary>
    public object? Tag { get; set; }

    internal required object Sig;
    internal readonly List<IDisposable> Owned = [];
    internal bool Seen;
}

/// <summary>The kept buttons of one of edit mode's bars, in its host's content (see the top of the file).</summary>
internal sealed class KeptButtons(FlyoutHost host)
{
    private readonly Dictionary<string, KeptButton> kept = new(StringComparer.Ordinal);

    // The bar's own: its elements under, what stands over them (the focus ring) on top.
    private readonly List<IDisposable> frameOwned = [];
    private ContainerVisual? root;
    private ContainerVisual? under;
    private ContainerVisual? over;
    private KeptButton? making;

    /// <summary>Tests only: how many elements were made, and surfaces drawn for them, since the bar was made.</summary>
    internal (int Elements, int Surfaces) Made => (elementsMade, surfacesDrawn);

    private int elementsMade;
    private int surfacesDrawn;

    /// <summary>The element keyed <paramref name="key"/>, while it is kept.</summary>
    public KeptButton? this[string key] => kept.GetValueOrDefault(key);

    /// <summary>
    /// A showing, after the host's: the bar's root back in the host's content (a showing not kept empties it),
    /// every element unseen until <see cref="Place"/> sees it.
    /// </summary>
    public void Begin()
    {
        var c = host.Compositor;
        if (root is null)
        {
            root = Own(c.CreateContainerVisual());
            under = Own(c.CreateContainerVisual());
            over = Own(c.CreateContainerVisual());
            root.Children.InsertAtTop(under);
            root.Children.InsertAtTop(over);
        }
        if (root.Parent != host.Content)
        {
            try
            {
                root.Parent?.Children.Remove(root);
            }
            catch (Exception error) when (error is ObjectDisposedException or System.Runtime.InteropServices.COMException)
            {
                // The window it stood in was let go of (Alt+F4): its tree with it.
            }
            host.Content.Children.InsertAtTop(root);
        }
        foreach (var each in kept.Values)
        {
            each.Seen = false;
        }
    }

    /// <summary>
    /// The element keyed <paramref name="key"/> at <paramref name="rect"/> (panel px): kept and moved there where it
    /// was drawn from <paramref name="sig"/> already, else made by <paramref name="make"/> — its pieces in its own px,
    /// through <see cref="Shape"/>, <see cref="Sprite"/> and <see cref="Solid"/> — in place of what was there.
    /// <paramref name="on"/>: over every other element (the focus ring).
    /// </summary>
    public KeptButton Place(string key, RectF rect, object sig, Action<KeptButton> make, bool on = false)
    {
        if (kept.TryGetValue(key, out var found) && Equals(found.Sig, sig))
        {
            found.Visual.Offset = new Vector3(rect.X, rect.Y, 0f);
            found.Seen = true;
            return found;
        }
        if (found is not null)
        {
            Close(found);
        }
        var c = host.Compositor;
        var visual = c.CreateContainerVisual();
        var made = new KeptButton { Visual = visual, Sig = sig, Seen = true };
        made.Owned.Add(visual);
        visual.Offset = new Vector3(rect.X, rect.Y, 0f);
        visual.Size = rect.Size;
        kept[key] = made;
        elementsMade++;
        making = made;
        try
        {
            make(made);
        }
        finally
        {
            making = null;
        }
        (on ? over! : under!).Children.InsertAtTop(visual);
        return made;
    }

    /// <summary>What was not placed since <see cref="Begin"/> let go of, in the same commit.</summary>
    public void End()
    {
        foreach (var (key, each) in kept.ToList())
        {
            if (!each.Seen)
            {
                kept.Remove(key);
                Close(each);
            }
        }
    }

    /// <summary>
    /// A rounded button's shape at <paramref name="rect"/> (its element's px), filled with a brush of its own (the
    /// element's <see cref="KeptButton.Fill"/>, clear until its owner lights it), rimmed <paramref name="strokeWidth"/>
    /// px inside its edge in <paramref name="stroke"/> where it has one.
    /// </summary>
    public ShapeVisual Shape(RectF rect, float radius, Color? stroke = null, float strokeWidth = 1f)
    {
        var c = host.Compositor;
        var visual = Own(c.CreateShapeVisual());
        visual.Offset = new Vector3(rect.X, rect.Y, 0f);
        visual.Size = rect.Size;
        var inset = stroke is null ? 0f : strokeWidth / 2f;
        var geometry = Own(c.CreateRoundedRectangleGeometry());
        geometry.Offset = new Vector2(inset, inset);
        geometry.Size = new Vector2(MathF.Max(0f, rect.Width - 2f * inset), MathF.Max(0f, rect.Height - 2f * inset));
        var corner = MathF.Max(0f, radius - inset);
        geometry.CornerRadius = new Vector2(corner, corner);
        var shape = Own(c.CreateSpriteShape(geometry));
        var fill = Own(c.CreateColorBrush(Color.FromArgb(0, 0, 0, 0)));
        shape.FillBrush = fill;
        if (stroke is { } rim)
        {
            shape.StrokeBrush = Own(c.CreateColorBrush(rim));
            shape.StrokeThickness = strokeWidth;
        }
        visual.Shapes.Add(shape);
        Into(visual);
        making!.Fill = fill;
        return visual;
    }

    /// <summary>
    /// <paramref name="surface"/>, drawn for the element and closed with it, at <paramref name="rect"/> (its element's
    /// px); none, an empty sprite whose brush its owner sets (the rail's "not shown" mark).
    /// </summary>
    public SpriteVisual Sprite(Windows.UI.Composition.CompositionDrawingSurface? surface, RectF rect)
    {
        var c = host.Compositor;
        var sprite = Own(c.CreateSpriteVisual());
        if (surface is not null)
        {
            surfacesDrawn++;
            Own(surface);
            var brush = Own(c.CreateSurfaceBrush(surface));
            brush.Stretch = CompositionStretch.None;
            brush.HorizontalAlignmentRatio = 0f;
            brush.VerticalAlignmentRatio = 0f;
            sprite.Brush = brush;
        }
        sprite.Offset = new Vector3(rect.X, rect.Y, 0f);
        sprite.Size = rect.Size;
        Into(sprite);
        return sprite;
    }

    /// <summary>A rectangle of <paramref name="color"/> at <paramref name="rect"/> (its element's px): a command bar's separator.</summary>
    public SpriteVisual Solid(RectF rect, Color color)
    {
        var c = host.Compositor;
        var sprite = Own(c.CreateSpriteVisual());
        sprite.Brush = Own(c.CreateColorBrush(color));
        sprite.Offset = new Vector3(rect.X, rect.Y, 0f);
        sprite.Size = rect.Size;
        Into(sprite);
        return sprite;
    }

    /// <summary>Keeps <paramref name="value"/>: the element's being made, else the bar's, closed with it.</summary>
    public T Own<T>(T value)
        where T : IDisposable
    {
        (making?.Owned ?? frameOwned).Add(value);
        return value;
    }

    /// <summary>Everything kept goes to the host, which closes it once its window no longer shows it: the bar goes for good.</summary>
    public void Release()
    {
        foreach (var each in kept.Values)
        {
            foreach (var value in each.Owned)
            {
                host.Keep(value);
            }
            each.Owned.Clear();
        }
        kept.Clear();
        foreach (var value in frameOwned)
        {
            host.Keep(value);
        }
        frameOwned.Clear();
        (root, under, over) = (null, null, null);
    }

    private void Into(Visual visual) => making!.Visual.Children.InsertAtTop(visual);

    private static void Close(KeptButton gone)
    {
        try
        {
            gone.Visual.Parent?.Children.Remove(gone.Visual);
        }
        catch (Exception error) when (error is ObjectDisposedException or System.Runtime.InteropServices.COMException)
        {
        }
        FlyoutHost.CloseAll(gone.Owned);
    }
}
