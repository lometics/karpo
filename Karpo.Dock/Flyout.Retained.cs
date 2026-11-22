// menus: a menu's rows kept, not made again (docs\widgets.md §356; the flicker design's rule R1 for menus).
// Every showing of a menu — a Refresh after a pick, a stepper's held tick, a level opened or gone back from,
// a search's keystroke, a field's answer — laid out its rows and made every one of them again: new text
// surfaces, new sprites, a new lit plate fading in from nothing, so the row under the pointer blinked at
// every pick. Now each thing the menu shows is an element kept by a key — its level, its kind, its id and
// which of that id it is — with what it was drawn from (its signature: the row as it draws, the menu's
// measures, where in the row it stands):
//
//  - **The same** is kept as it is, its container moved where the row stands now; nothing is made.
//  - **Changed** (a toggle flipped, a stepper's value, another tick) is drawn again, alone; its lit plate
//    goes over to the new one as it is, lit or not, so a pick never fades the row under the pointer in again.
//  - **Gone** is let go of, in the same commit as the rest.
//
// The lit row, the tool under the pointer, an open submenu (a menu that cascades) and its tooltip stay with
// their row by its key through a showing again; a menu shown afresh (Show) starts unlit. What stands round
// the rows — the clip they scroll in, what stands still over and under them, the scroll indicator, the
// plates' brushes — is the showing's, made once (Frame). Text and glyph surfaces are drawn once a showing
// for what they say and shared (Drawn), let go when no row shows them. The host keeps its content through a
// showing again (FlyoutHost.Show's keep). It all goes to the host as the menu closes, which lets it go once
// the window is out of sight.

using System.Numerics;
using TerraFX.Interop.DirectX;
using Windows.UI.Composition;

namespace Karpo.Dock;

public sealed unsafe partial class Flyout
{
    /// <summary>§356: one thing the menu shows — a row, a rule, a heading, the title, the add list's field — kept by its key.</summary>
    private sealed class Element
    {
        public required string Key;

        /// <summary>What it was drawn from; drawn again where that differs.</summary>
        public required object Sig;

        /// <summary>Its container: what it is made of stands in it where it was drawn (<see cref="MadeAt"/>), and it moves as one.</summary>
        public required ContainerVisual Visual;

        public required ContainerVisual Parent;

        /// <summary>Where it stood as it was drawn, and where it stands now (content px, its top).</summary>
        public float MadeAt;
        public float Top;

        /// <summary>What was made for it, closed with it.</summary>
        public readonly List<IDisposable> Owned = [];

        /// <summary>The shared surfaces it shows (<see cref="Drawn"/>).</summary>
        public readonly List<Drawn> Uses = [];

        public Row? Row;

        /// <summary>Its lit plate, which goes over to it drawn again.</summary>
        public Plate? Plate;

        /// <summary>The type title's words (content px), or the add list's field's surface.</summary>
        public Area Words;
        public CompositionDrawingSurface? Surface;

        /// <summary>Shown in this showing.</summary>
        public bool Seen;

        /// <summary>The level it is of (its holders' ids, "40/41"; "" the menu's own rows); null for what is no level's (the title, the add list's field).</summary>
        public string? Level;

        /// <summary>Out of the tree, kept while a level under its own stands: a level gone back from shows it again as it was.</summary>
        public bool Detached;
    }

    /// <summary>§356: a row's lit plate: the rounded fill under it, lit under the pointer or the keys.</summary>
    private sealed class Plate : IDisposable
    {
        public required ShapeVisual Visual;
        public required CompositionRoundedRectangleGeometry Shape;
        public required CompositionSpriteShape Fill;

        public void Dispose()
        {
            FlyoutHost.CloseAll([Fill, Shape, Visual]);
        }

        /// <summary>Handed to <paramref name="host"/>, which closes it once its window no longer shows it.</summary>
        public void LetGo(FlyoutHost host)
        {
            host.Keep(Fill);
            host.Keep(Shape);
            host.Keep(Visual);
        }
    }

    /// <summary>§356: a surface drawn once for what it says — a row's words, a glyph — shared by the rows that show it.</summary>
    private sealed class Drawn
    {
        public CompositionDrawingSurface? Surface;
        public CompositionSurfaceBrush? Brush;
        public Vector2 Size;

        /// <summary>How many kept elements show it.</summary>
        public int Uses;

        /// <summary>Asked for in this showing.</summary>
        public bool Touched;

        public void Close()
        {
            List<IDisposable> gone = [];
            if (Brush is not null)
            {
                gone.Add(Brush);
            }
            if (Surface is not null)
            {
                gone.Add(Surface);
            }
            FlyoutHost.CloseAll(gone);
        }
    }

    /// <summary>What a shared surface is drawn from: §556 its words' font among it (a font's choice is in its own).</summary>
    private readonly record struct DrawnKey(char Kind, string Text, float Px, Weight Weight, float R, float G, float B, float A, float Limit, Karpo.Core.TextStyle Face = default);

    // §356: the elements kept, by key, and the shared surfaces.
    private readonly Dictionary<string, Element> kept = new(StringComparer.Ordinal);
    private readonly Dictionary<DrawnKey, Drawn> drawn = [];

    // The element being drawn: what is made is its (Own); and the level the elements begun now are of.
    private Element? making;
    private string? drawingLevel;

    // The showing's frame round the rows (Frame): made once a showing, closed with it.
    private readonly List<IDisposable> frameOwned = [];
    private ContainerVisual? frameWindow;
    private InsetClip? frameClip;
    private ContainerVisual? frameRows;
    private ContainerVisual? framePinned;
    private SpriteVisual? frameIndicator;
    private CompositionRoundedRectangleGeometry? frameIndicatorShape;
    private CompositionColorBrush? frameIndicatorBrush;
    private CompositionColorBrush? frameDivider;
    private ImplicitAnimationCollection? frameFades;

    // A level's slide: what the last one was made of, closed as the next starts.
    private readonly List<IDisposable> slideOwned = [];

    // §356: Show, not a showing again: nothing lit is kept.
    private bool fresh;

    // §356: a Refresh: an open submenu stays, shown again with its rows as they are now.
    private bool refreshing;

    /// <summary>Tests only: what the menu made since it was made — elements (rows, rules, headings), composition objects, and shared surfaces drawn.</summary>
    internal (int Elements, int Objects, int Surfaces) Made => (elementsMade, objectsMade, surfacesDrawn);

    private int elementsMade;
    private int objectsMade;
    private int surfacesDrawn;

    /// <summary>Tests only: the lit row's plate and how lit it is; null for none.</summary>
    internal (object Plate, float Opacity)? LitPlate =>
        hot >= 0 && hot < rows.Count && rows[hot].Highlight is { } plate ? (plate, plate.Opacity) : null;

    /// <summary>Keeps <paramref name="value"/>, made for the element being drawn, or for the showing's frame, and closes it with it.</summary>
    private T Own<T>(T value)
        where T : IDisposable
    {
        objectsMade++;
        (making?.Owned ?? frameOwned).Add(value);
        return value;
    }

    /// <summary>
    /// The showing's frame round the rows, made once a showing: the clip they scroll in, the rows, what stands
    /// still over and under them, the scroll indicator; the plates' and the rules' brushes in the theme as it is.
    /// </summary>
    private void Frame(Compositor c, bool animate)
    {
        if (frameWindow is null)
        {
            frameWindow = Own(c.CreateContainerVisual());
            frameClip = Own(c.CreateInsetClip());
            frameWindow.Clip = frameClip;
            frameRows = Own(c.CreateContainerVisual());
            frameWindow.Children.InsertAtTop(frameRows);
            framePinned = Own(c.CreateContainerVisual());
            frameIndicator = Own(c.CreateSpriteVisual());
            frameIndicatorShape = Own(c.CreateRoundedRectangleGeometry());
            frameIndicatorBrush = Own(c.CreateColorBrush());
            frameIndicator.Brush = frameIndicatorBrush;
            frameIndicator.Clip = Own(c.CreateGeometricClip(frameIndicatorShape));
            host.Content.Children.InsertAtTop(frameWindow);
            host.Content.Children.InsertAtTop(framePinned);
            host.Content.Children.InsertAtTop(frameIndicator);
            hoverBrush = Own(c.CreateColorBrush());
            pressedBrush = Own(c.CreateColorBrush());
            frameDivider = Own(c.CreateColorBrush());
        }
        if (animate && frameFades is null)
        {
            var linear = Own(MotionEase.Of(c, MotionKind.Hover));
            var toward = Own(c.CreateScalarKeyFrameAnimation());
            toward.Target = "Opacity";
            toward.InsertExpressionKeyFrame(1, "this.FinalValue", linear);
            toward.Duration = look.Motion.Time(MotionKind.Hover);
            frameFades = Own(c.CreateImplicitAnimationCollection());
            frameFades["Opacity"] = toward;
        }
        // §489: kept for the window's life, at the speed the look has now.
        if (animate && frameFades?["Opacity"] is KeyFrameAnimation fading)
        {
            fading.Duration = look.Motion.Time(MotionKind.Hover);
        }
        hoverBrush!.Color = theme.SubtleHover;
        pressedBrush!.Color = theme.SubtlePressed;
        frameDivider!.Color = theme.Divider;
        frameIndicatorBrush!.Color = theme.ScrollIndicator;
    }

    /// <summary>
    /// §356: begins the element <paramref name="key"/>, drawn from <paramref name="sig"/>, standing at
    /// <paramref name="top"/> in <paramref name="parent"/>. True where the one kept is it: moved there, and
    /// nothing to draw. False where it is drawn: a new one, empty, whose making is under way (<see cref="making"/>,
    /// <see cref="EndElement"/>); <paramref name="plate"/> the lit plate of the one it takes the place of.
    /// </summary>
    private bool BeginElement(Compositor c, string key, object sig, ContainerVisual parent, float top, out Element element, out Plate? plate)
    {
        plate = null;
        if (kept.TryGetValue(key, out var old) && !old.Seen)
        {
            if (old.Parent == parent && old.Row is not { Displaced: true } && old.Sig.Equals(sig))
            {
                old.Seen = true;
                if (old.Detached)
                {
                    old.Detached = false;
                    parent.Children.InsertAtTop(old.Visual);
                }
                MoveElement(old, top);
                element = old;
                return true;
            }
            if (old.Plate is { } carried)
            {
                old.Plate = null;
                Try(() => old.Visual.Children.Remove(carried.Visual));
                plate = carried;
            }
            Release(old);
            kept.Remove(key);
        }
        var visual = c.CreateContainerVisual();
        element = new Element { Key = key, Sig = sig, Visual = visual, Parent = parent, MadeAt = top, Top = top, Seen = true, Level = drawingLevel };
        element.Owned.Add(visual);
        objectsMade++;
        elementsMade++;
        parent.Children.InsertAtTop(visual);
        kept[key] = element;
        making = element;
        return false;
    }

    /// <summary>§356: the element drawn is done; turned across the panel in a mirrored menu (Mirror.cs).</summary>
    private void EndElement(Element element, float width)
    {
        making = null;
        // §523: a colour's spectrum and sliders read left to right in every language, as the colours they stand for (Flyout.Mixer.cs).
        if (!mirrored || element.Row is { Item.Control: FlyoutMix })
        {
            return;
        }
        foreach (var part in element.Visual.Children)
        {
            part.Offset = part.Offset with { X = Mirror.Across(part.Offset.X, part.Size.X, width) };
        }
        if (element.Words.W > 0)
        {
            element.Words = element.Words with { X = Mirror.Across(element.Words.X, element.Words.W, width) };
        }
        if (element.Row is not { } row)
        {
            return;
        }
        row.Bounds = row.Bounds with { X = Mirror.Across(row.Bounds.X, row.Bounds.W, width) };
        Area Turned(Area part) => part.W > 0 ? part with { X = Mirror.Across(part.X, part.W, width) } : part;
        (row.Icon, row.Words, row.Keys, row.Check, row.Value) = (Turned(row.Icon), Turned(row.Words), Turned(row.Keys), Turned(row.Check), Turned(row.Value));
        // menus: a stepper's buttons (Flyout.Controls.cs).
        (row.Fewer, row.More) = (Turned(row.Fewer), Turned(row.More));
        for (int i = 0; i < row.Tools.Count; i++)
        {
            row.Tools[i] = row.Tools[i] with { Box = Turned(row.Tools[i].Box) };
        }
        for (int i = 0; i < row.Parts.Count; i++)
        {
            row.Parts[i] = (row.Parts[i].Visual, row.Parts[i].Visual.Offset);
        }
    }

    /// <summary>§356: a kept element to <paramref name="top"/>: its container moved, its row's boxes with it.</summary>
    private static void MoveElement(Element element, float top)
    {
        var by = top - element.Top;
        if (by == 0)
        {
            return;
        }
        element.Top = top;
        Try(() => element.Visual.Offset = new Vector3(0, top - element.MadeAt, 0));
        element.Words = element.Words.W > 0 ? element.Words with { Y = element.Words.Y + by } : element.Words;
        element.Row?.Shift(by);
    }

    /// <summary>§356: an element let go of: out of the tree, what was made for it closed, its shared surfaces one use less.</summary>
    private static void Release(Element element)
    {
        Try(() => element.Parent.Children.Remove(element.Visual));
        element.Plate?.Dispose();
        element.Plate = null;
        foreach (var use in element.Uses)
        {
            use.Uses--;
        }
        element.Uses.Clear();
        FlyoutHost.CloseAll(element.Owned);
    }

    /// <summary>
    /// §356: the elements not shown in this showing let go of — but a level's under the one standing, at
    /// <paramref name="level"/>, which are taken out of the tree and kept, unlit, for going back to — and the
    /// shared surfaces no element shows nor this showing asked for.
    /// </summary>
    private void Sweep(string level)
    {
        foreach (var gone in kept.Values.Where(element => !element.Seen).ToList())
        {
            if (gone.Level is { } of && of != level && (of.Length == 0 || level.StartsWith(of + "/", StringComparison.Ordinal)))
            {
                if (!gone.Detached)
                {
                    gone.Detached = true;
                    Try(() => gone.Parent.Children.Remove(gone.Visual));
                    if (gone.Row is { } row)
                    {
                        Light(row, false);
                        foreach (var (_, _, plate) in row.Tools)
                        {
                            Try(() => plate.Opacity = 0f);
                        }
                    }
                }
                continue;
            }
            Release(gone);
            kept.Remove(gone.Key);
        }
        foreach (var (key, surface) in drawn.Where(pair => pair.Value.Uses <= 0 && !pair.Value.Touched).ToList())
        {
            surface.Close();
            drawn.Remove(key);
        }
        foreach (var surface in drawn.Values)
        {
            surface.Touched = false;
        }
    }

    /// <summary>
    /// §356: everything kept goes to the host, which closes it once the window no longer shows it: the menu
    /// closes, or its window's content is emptied for another showing.
    /// </summary>
    private void ReleaseKept()
    {
        foreach (var element in kept.Values)
        {
            element.Plate?.LetGo(host);
            element.Plate = null;
            foreach (var value in element.Owned)
            {
                host.Keep(value);
            }
            element.Owned.Clear();
            element.Uses.Clear();
        }
        kept.Clear();
        foreach (var surface in drawn.Values)
        {
            if (surface.Brush is not null)
            {
                host.Keep(surface.Brush);
            }
            if (surface.Surface is not null)
            {
                host.Keep(surface.Surface);
            }
        }
        drawn.Clear();
        foreach (var value in frameOwned.Concat(slideOwned))
        {
            host.Keep(value);
        }
        frameOwned.Clear();
        slideOwned.Clear();
        (frameWindow, frameClip, frameRows, framePinned, frameIndicator, frameIndicatorShape, frameIndicatorBrush, frameDivider, frameFades) =
            (null, null, null, null, null, null, null, null, null);
        hoverBrush = null;
        pressedBrush = null;
        making = null;
    }

    /// <summary>§356: words drawn once for what they say, shared; one more use asked for in this showing.</summary>
    private Drawn TextOf(Gfx g, string text, float px, Weight weight, DXGI_RGBA color, float limit, bool wrapped)
    {
        var key = new DrawnKey(wrapped ? 'w' : 't', text, px, weight, color.r, color.g, color.b, color.a, limit, g.Typeface);
        if (!drawn.TryGetValue(key, out var entry))
        {
            var (surface, size) = wrapped ? g.Wrapped(text, px, weight, color, limit) : g.Text(text, px, weight, color, limit);
            surfacesDrawn++;
            entry = new Drawn { Surface = surface, Size = size };
            drawn[key] = entry;
        }
        entry.Touched = true;
        return entry;
    }

    /// <summary>§356: a brush of a surface drawn once — a glyph, Explorer's picture — shared; null where none can be drawn.</summary>
    private Drawn? BrushOf(DrawnKey key, Func<CompositionDrawingSurface?> draw)
    {
        if (!drawn.TryGetValue(key, out var entry))
        {
            var surface = draw();
            surfacesDrawn++;
            entry = new Drawn { Surface = surface, Brush = surface is null ? null : host.Compositor.CreateSurfaceBrush(surface) };
            drawn[key] = entry;
        }
        entry.Touched = true;
        return entry;
    }

    /// <summary>§356: the element being drawn shows <paramref name="surface"/>: it stays while the element does.</summary>
    private void Uses(Drawn? surface)
    {
        if (surface is not null && making is { } element)
        {
            surface.Uses++;
            element.Uses.Add(surface);
        }
    }

    /// <summary>
    /// §356: a row as it draws, for its signature: what it holds below — a submenu's rows, a tool's level, a
    /// search's finder, the words a field shows from its own state, the words a filter finds it by — is not drawn.
    /// </summary>
    private static FlyoutItem Drawable(FlyoutItem item) => item with
    {
        Children = item.Children is null ? null : HasRows,
        Words = null,
        Control = item.Control switch
        {
            FlyoutField field => field with { Finder = null, Text = "" },
            FlyoutControl.Tools tools => tools with { Buttons = [.. tools.Buttons.Select(Drawable)] },
            FlyoutControl.Segments segments => segments with { Buttons = [.. segments.Buttons.Select(Drawable)] },
            var other => other,
        },
    };

    private static FlyoutTool Drawable(FlyoutTool tool) => tool.Level is null ? tool : tool with { Level = HasRows };

    // What a row's submenu and a tool's level are in a signature: there, whatever they hold.
    private static readonly FlyoutItem[] HasRows = [];

    /// <summary>§356: what a row of the menu is drawn from, past the row itself: the menu's measures, and where in it the row stands.</summary>
    private sealed record MenuSig(
        FlyoutLayout Layout,
        float Scale,
        float TextScale,
        FlyoutTheme Theme,
        bool Mirrored,
        float Width,
        float Widest,
        float TextRoom,
        float KeysWidth,
        bool AnyIcon,
        bool AnyCheck,
        float FillH,
        float HeaderH,
        float CellW,
        float CellH,
        float GridLeft,
        bool Group,
        bool Titles,
        float IconSize,
        bool Animate,
        Karpo.Core.TextStyle Typeface,
        bool Settings);

    /// <summary>§356: an element's signature: the menu's, what it shows, where it stands in its row and anything else it is drawn from.</summary>
    private sealed record ElementSig(MenuSig Menu, FlyoutItem? Item, float X, float Y, float W, float H, object? Extra = null);

    private sealed partial class Row
    {
        /// <summary>§356: the key of the element it is (<see cref="Element"/>).</summary>
        public string Key = "";

        /// <summary>§356: its parts were moved off their places (a drag's gap, a reorder): drawn again rather than kept.</summary>
        public bool Displaced;

        /// <summary>§356: its boxes <paramref name="by"/> px further down: its element moved.</summary>
        public void Shift(float by)
        {
            Area Down(Area part) => part.W > 0 || part.H > 0 ? part with { Y = part.Y + by } : part;
            Bounds = Bounds with { Y = Bounds.Y + by };
            (Icon, Words, Keys, Check, Value, Fewer, More) = (Down(Icon), Down(Words), Down(Keys), Down(Check), Down(Value), Down(Fewer), Down(More));
            for (var i = 0; i < Tools.Count; i++)
            {
                Tools[i] = Tools[i] with { Box = Down(Tools[i].Box) };
            }
        }
    }
}
