// folders: a folder's lines, its lists and its columns' buttons kept, not made again (docs\widgets.md §357;
// the flicker design's rule R1 for folders). Every layout of a folder whose shape changed — a line added,
// removed or moved to another list, a list more, edit mode's "+" buttons coming or a list's warning beside
// them, a slot more — threw away every visual of the folder and made each one again (RemakeVisuals): every
// line's card and surface, every "+", the guide lines; a "+" drew again at every layout ("every button drawn
// afresh"), and the "+" were known by their place in the list, so a warning come in an earlier list moved
// the pressed and the keys' look onto another button. Now each thing the folder shows is an element kept
// by a key:
//
//  - **A line** by its widget's key: its card (its fill and its outline), its content and a spacer's
//    stripes over its room (§524). Kept through every layout, moved to the list or the card it stands in now;
//    drawn again where it is the widget's to draw, never made again.
//  - **A list** by its place among the lists, **a card of a list** by its list, its place and whether it is
//    the one under the rim: their clips, made once and placed.
//  - **A column's button** by what it is — a list's "+", width or warning, a slot's on either side and how
//    far out —, drawn again only where its look, its glyph or its size changed.
//
// What stands round them — the guide lines, the ghost, where a carry would land, the rings, the focus
// rectangle, the off-screen chip, the lifted lines' plates — is the folder's for its window's life (Frame),
// coloured again where the look changes. All of it is the folder's, not its host's showing's
// (FlyoutHost.Keep), so a folder closed keeps it and opens again with it — the dock keeps one folder a key
// (Dock.Folders.cs) — and hands it to the host only as it goes for good (Dispose).

using System.Numerics;
using Karpo.Core;
using TerraFX.Interop.DirectX;
using Windows.UI.Composition;
using Color = Windows.UI.Color;

namespace Karpo.Dock;

public sealed unsafe partial class FolderFlyout
{
    /// <summary>§357: a line of the folder, by its widget's key: its card, its content and a spacer's stripes over its room.</summary>
    private sealed class Line
    {
        public required string Key;

        /// <summary>§464: the line's surface, moved as the line is: the blur under it (<see cref="Frost"/>), and its background over that.</summary>
        public required ContainerVisual Card;

        /// <summary>§464: the blur of the desktop under a see-through background (FolderFlyout.LineFill), the line's background's shape.</summary>
        public required SpriteVisual Frost;

        /// <summary>§464: its background, on whole px over its blur.</summary>
        public required SpriteVisual Fill;
        public required CompositionColorBrush Wash;
        public required SpriteVisual Sprite;
        public required CompositionSurfaceBrush Brush;
        public CompositionDrawingSurface? Surface;

        /// <summary>§524: arranged, a spacer's room striped (<see cref="SpacerStripes"/>); hidden for any other line and at rest.</summary>
        public required SpacerStripes Band;
        public StripView? Strip;

        /// <summary>§462: its pattern over its fill, the piece of its section's sheet it stands on.</summary>
        public required PatternLayer Pattern;

        /// <summary>
        /// §649: a divider's line across its room in the folder's outline (<see cref="FolderFlyout.RuleOf"/>), while the folder
        /// stands attached; made the first time it is, hidden otherwise.
        /// </summary>
        public SpriteVisual? Rule;

        /// <summary>
        /// §704: Liquid glass's hairline toward the line above it in its piece (<see cref="FolderFlyout.Seams"/>), in its
        /// holder; made the first time there is one, hidden otherwise.
        /// </summary>
        public SpriteVisual? Seam;
        public CompositionColorBrush? SeamInk;

        public readonly List<IDisposable> Owned = [];
        public bool Seen;
    }

    /// <summary>
    /// §424: a line's part that moves on the compositor (LineStrip.cs): a window clipped over the line's content, and
    /// in it the strip's sprite, its offset bound to the line's own motion by an expression.
    /// </summary>
    private sealed class StripView
    {
        public required ContainerVisual View;
        public required SpriteVisual Sprite;
        public required CompositionSurfaceBrush Brush;
        public CompositionDrawingSurface? Surface;
        public object? Bound;
        public readonly List<IDisposable> Owned = [];
    }

    /// <summary>§357: a column's round button, by what it is (<see cref="AddKeyOf"/>), and what it was drawn from.</summary>
    private sealed class Button
    {
        public required string Key;
        public required SpriteVisual Sprite;
        public required CompositionSurfaceBrush Brush;
        public CompositionDrawingSurface? Surface;
        public object? Drawn;
        public readonly List<IDisposable> Owned = [];
        public bool Seen;
    }

    /// <summary>§357: a list's view, clipped to its panel's shape: its cards' views under its spacers' stripes.</summary>
    private sealed class ListView
    {
        public required ContainerVisual View;
        public required CompositionRoundedRectangleGeometry Shape;
        public required ContainerVisual Cards;
        public required ContainerVisual Marks;
        public readonly List<IDisposable> Owned = [];
    }

    /// <summary>§357: a card of a list's view, clipped to the card's shape; inside the rim, or to the card's edge under it.</summary>
    private sealed class RunView
    {
        public required ContainerVisual View;
        public required CompositionRoundedRectangleGeometry Shape;
        public required int List;
        public required int Run;
        public required bool Rim;
        public readonly List<IDisposable> Owned = [];
        public bool Seen;
    }

    /// <summary>Tests only: what the folder made since it was made: lines, columns' buttons, lists' and cards' views, composition objects, and surfaces drawn.</summary>
    internal readonly record struct FolderMade(int Lines, int Buttons, int Views, int Objects, int Surfaces);

    /// <summary>Tests only: what the folder made since it was made (<see cref="FolderMade"/>).</summary>
    internal FolderMade Made => new(linesMade, buttonsMade, viewsMade, objectsMade, surfacesDrawn);

    /// <summary>Tests only: how many times a column's button was drawn, made or again.</summary>
    internal int ButtonsDrawn { get; private set; }

    private int linesMade;
    private int buttonsMade;
    private int viewsMade;
    private int objectsMade;
    private int surfacesDrawn;

    // §357: the elements kept, by key.
    private readonly Dictionary<string, Line> lines = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Button> buttons = new(StringComparer.Ordinal);
    private readonly List<ListView> lists = [];
    private readonly Dictionary<(int List, int Run, bool Rim), RunView> runs = [];

    // §357: what is made for the element being made goes with it; anything else is the frame's.
    private List<IDisposable>? making;

    /// <summary>§464: the host backdrop every see-through line's blur is (<see cref="Line.Frost"/>), made with the frame.</summary>
    private CompositionBackdropBrush? frost;
    private readonly List<IDisposable> retained = [];

    // §357: the frame round the lines, made once for the folder's life: under the lists the guide lines and
    // the ghost, over them the empty lists' words and the landing; outside the panels the rings and the
    // marching ants, the buttons, the focus rectangle, the off-screen chip and the lifted lines.
    private ContainerVisual? contentRoot;
    private ContainerVisual? guidesLayer;
    private ContainerVisual? listsLayer;
    private ContainerVisual? outsideRoot;
    private ContainerVisual? ringsLayer;
    private ContainerVisual? addsLayer;
    private ContainerVisual? carriersLayer;
    // §524: a spacer's stripes, at rest and under the pointer or selected (SpacerStripes.InkOf), and the loop they move on.
    private CompositionColorBrush? bandInk;
    private CompositionColorBrush? bandStrong;
    private ScalarKeyFrameAnimation? bandLoop;
    private CompositionColorBrush? ringInk;
    private CompositionColorBrush? focusOuterInk;
    private CompositionColorBrush? focusInnerInk;
    private CompositionColorBrush? ghostFill;
    private CompositionColorBrush? ghostStroke;

    // §649: an attached folder's dividers' ink, its outline's (RuleInk).
    private CompositionColorBrush? ruleInk;
    private ImplicitAnimationCollection? washFade;
    private readonly List<CompositionColorBrush> alsoInks = [];

    // §357: the look the frame's brushes wear, and the scale what is made in it was made at; dressed again where either changes.
    private (FlyoutTheme Theme, float Scale)? dressedFor;

    // §357: what the empty lists' words were drawn for.
    private object? emptyDrawn;

    /// <summary>Keeps <paramref name="value"/>, made for the element being made or for the folder's frame, and closes it with it.</summary>
    private T Own<T>(T value)
        where T : IDisposable
    {
        objectsMade++;
        (making ?? retained).Add(value);
        return value;
    }

    /// <summary>
    /// §357: the frame round the lines, made the first time (see the top of the file), in the host's content
    /// and outside its panels again where a showing emptied them, and in the look as it is now.
    /// </summary>
    private void Frame()
    {
        var c = host.Compositor;
        if (contentRoot is null)
        {
            contentRoot = Own(c.CreateContainerVisual());
            guidesLayer = Own(c.CreateContainerVisual());
            contentRoot.Children.InsertAtTop(guidesLayer);
            // edit: the ghost where a carried line would land, under the lines (FolderFlyout.Drag.cs).
            ghostVisual = Own(c.CreateShapeVisual());
            ghostBox = Own(c.CreateRoundedRectangleGeometry());
            var ghost = Own(c.CreateSpriteShape(ghostBox));
            ghostFill = Own(c.CreateColorBrush());
            ghostStroke = Own(c.CreateColorBrush());
            ghost.FillBrush = ghostFill;
            ghost.StrokeBrush = ghostStroke;
            ghostVisual.Shapes.Add(ghost);
            ghostVisual.IsVisible = false;
            contentRoot.Children.InsertAtTop(ghostVisual);
            listsLayer = Own(c.CreateContainerVisual());
            contentRoot.Children.InsertAtTop(listsLayer);
            // §625: over them while the add page's ghost stands.
            OverTheLines();
            // Over every list, not in one's clip: any list may be an empty one saying so (PaintEmpty).
            empty = Own(c.CreateSpriteVisual());
            emptyBrush = Own(c.CreateSurfaceBrush());
            empty.Brush = emptyBrush;
            contentRoot.Children.InsertAtTop(empty);
            // edit: where a carried widget would land — the accent's bar across its list where it would
            // go in between (a slot shows its own: the ghost in it).
            targetOk = Own(c.CreateColorBrush());
            targetVisual = Own(c.CreateShapeVisual());
            targetBox = Own(c.CreateRoundedRectangleGeometry());
            var landingShape = Own(c.CreateSpriteShape(targetBox));
            landingShape.FillBrush = targetOk;
            targetVisual.Shapes.Add(landingShape);
            targetVisual.IsVisible = false;
            contentRoot.Children.InsertAtTop(targetVisual);
            // edit: the guide lines' ink; a spacer's stripes' two and the marching ants' loop they move on (§524).
            guideInk = Own(c.CreateColorBrush());
            ruleInk = Own(c.CreateColorBrush());
            bandInk = Own(c.CreateColorBrush());
            bandStrong = Own(c.CreateColorBrush());
            bandLoop = MarchingAnts.Loop(c, SpacerStripes.PeriodDip, SpacerStripes.Laps, value => Own(value));
            // §464: the blur of the desktop each see-through line wears under its own background (LineFill), one for the folder.
            frost = Own(c.CreateHostBackdropBrush());
            // edit: a wash's colour fading to each new one while the folder is arranged, as the bar's.
            var fade = Own(c.CreateColorKeyFrameAnimation());
            fade.InsertExpressionKeyFrame(1f, "this.FinalValue", Own(MotionEase.Of(c, MotionKind.Hover)));
            fade.Duration = look.Motion.Time(MotionKind.Hover);
            fade.Target = "Color";
            washFade = Own(c.CreateImplicitAnimationCollection());
            washFade["Color"] = fade;

            outsideRoot = Own(c.CreateContainerVisual());
            // edit: the selected widget's ring, the accent's, 2 px just outside its line, kept to what its
            // list shows (ShowStates). Outside the panel's clip, over everything but the "+" buttons (one
            // standing on it shows whole) and the focus rectangle: in the panel's content it lost its outer
            // edge to the panel's rounded clip at a first or last line, or a line as wide as its list
            // ("often clipped by the edge, clipped by other widgets, behind other stuff", 2026-10-11). The
            // other selected lines' rings under it, the marching ants over it (FolderFlyout.Ants.cs).
            ringsLayer = Own(c.CreateContainerVisual());
            outsideRoot.Children.InsertAtTop(ringsLayer);
            pickedVisual = Own(c.CreateShapeVisual());
            pickedRing = Own(c.CreateRoundedRectangleGeometry());
            var ring = Own(c.CreateSpriteShape(pickedRing));
            ringInk = Own(c.CreateColorBrush());
            ring.StrokeBrush = ringInk;
            pickedVisual.Shapes.Add(ring);
            pickedVisual.IsVisible = false;
            ringsLayer.Children.InsertAtTop(pickedVisual);
            // edit: the columns' round buttons, outside the panels, in the window's room round them, unclipped.
            addsLayer = Own(c.CreateContainerVisual());
            outsideRoot.Children.InsertAtTop(addsLayer);
            // The Windows focus rectangle: 2 px outside what the keys are on, 1 px inside it. Over
            // everything and unclipped, as it rings a "+" outside the panels as well as a line.
            focusVisual = Own(c.CreateShapeVisual());
            focusOuter = Own(c.CreateRoundedRectangleGeometry());
            focusInner = Own(c.CreateRoundedRectangleGeometry());
            var outer = Own(c.CreateSpriteShape(focusOuter));
            focusOuterInk = Own(c.CreateColorBrush());
            outer.StrokeBrush = focusOuterInk;
            var inner = Own(c.CreateSpriteShape(focusInner));
            focusInnerInk = Own(c.CreateColorBrush());
            inner.StrokeBrush = focusInnerInk;
            focusVisual.Shapes.Add(outer);
            focusVisual.Shapes.Add(inner);
            focusVisual.IsVisible = false;
            outsideRoot.Children.InsertAtTop(focusVisual);
            // edit: the off-screen chip (FolderFlyout.OffScreen.cs), over the buttons.
            warningVisual = Own(c.CreateSpriteVisual());
            warningBrush = Own(c.CreateSurfaceBrush());
            warningVisual.Brush = warningBrush;
            warningVisual.IsVisible = false;
            outsideRoot.Children.InsertAtTop(warningVisual);
            // edit: the lines lifted over everything (FolderFlyout.Drag.cs).
            carriersLayer = Own(c.CreateContainerVisual());
            outsideRoot.Children.InsertAtTop(carriersLayer);
        }
        // A showing starts with the host's content and what stands outside its panels emptied
        // (FlyoutHost.Show): the folder's own go back in, whole, in the same commit as the rest.
        Try(() =>
        {
            if (contentRoot.Parent != host.Content)
            {
                contentRoot.Parent?.Children.Remove(contentRoot);
                host.Content.Children.InsertAtTop(contentRoot);
            }
            if (outsideRoot!.Parent != host.Outside)
            {
                outsideRoot.Parent?.Children.Remove(outsideRoot);
                host.Outside.Children.InsertAtTop(outsideRoot);
            }
        });
        Dress();
    }

    /// <summary>
    /// §357: the frame's brushes in the look as it is now — the accent's, the focus rectangle's, the guide
    /// lines' and a spacer's stripes' — coloured in place; what is made in a look or at a scale (the marching ants,
    /// the lifted lines' plates and shadows) let go, to be made anew; every button and the empty lists' words
    /// drawn again. Nothing where the look and the scale are the ones it wears.
    /// </summary>
    private void Dress()
    {
        if (dressedFor is { } was && was.Theme == theme && was.Scale == scale)
        {
            return;
        }
        var first = dressedFor is null;
        dressedFor = (theme, scale);
        Try(() =>
        {
            var accent = theme.Accent;
            ghostFill!.Color = Color.FromArgb((byte)MathF.Round(255 * EditLook.GhostAlpha(theme.Dark)), accent.R, accent.G, accent.B);
            ghostStroke!.Color = accent;
            targetOk!.Color = accent;
            ringInk!.Color = accent;
            foreach (var also in alsoInks)
            {
                also.Color = accent;
            }
            focusOuterInk!.Color = theme.FocusOuter;
            focusInnerInk!.Color = theme.FocusInner;
            guideInk!.Color = GuideInk(theme);
            ruleInk!.Color = RuleInk(theme);
            bandInk!.Color = SpacerStripes.InkOf(theme, strong: false);
            bandStrong!.Color = SpacerStripes.InkOf(theme, strong: true);
        });
        if (first)
        {
            return;
        }
        // The marching ants and the lifted lines' plates carry the look they were made in.
        foreach (var gone in ants)
        {
            Try(() => gone.Visual.Parent?.Children.Remove(gone.Visual));
        }
        foreach (var owned in antsOwned)
        {
            FlyoutHost.CloseAll(owned);
        }
        ants.Clear();
        antsOwned.Clear();
        foreach (var gone in spareRaised)
        {
            FlyoutHost.CloseAll(gone.Owned);
        }
        spareRaised.Clear();
        foreach (var button in buttons.Values)
        {
            button.Drawn = null;
        }
        emptyDrawn = null;
        warningDrawn = "";
    }

    /// <summary>
    /// §357: the folder's elements for <see cref="plan"/> and <see cref="tiles"/>: kept by their keys, moved to
    /// where they stand now, made where they are new, let go of where they are gone — in place of making all
    /// of them again (see the top of the file). The ordered lists the rest of the folder reads by index
    /// (<see cref="cards"/>, <see cref="contents"/>, <see cref="addVisuals"/>…) are what they are now.
    /// </summary>
    private void Reconcile()
    {
        Frame();
        var c = host.Compositor;
        var shown = plan;
        var radius = shown?.Radius ?? 0;
        var listPlans = shown?.Lists ?? [];

        // A list's view a list, clipped to its panel's rounded shape; placed by DrawNow and PlaceLines.
        while (lists.Count < listPlans.Count)
        {
            making = [];
            var view = Own(c.CreateContainerVisual());
            var shape = Own(c.CreateRoundedRectangleGeometry());
            view.Clip = Own(c.CreateGeometricClip(shape));
            var inCards = Own(c.CreateContainerVisual());
            var inMarks = Own(c.CreateContainerVisual());
            view.Children.InsertAtTop(inCards);
            view.Children.InsertAtTop(inMarks);
            listsLayer!.Children.InsertAtTop(view);
            var made = new ListView { View = view, Shape = shape, Cards = inCards, Marks = inMarks };
            made.Owned.AddRange(making);
            making = null;
            lists.Add(made);
            viewsMade++;
        }
        // A list gone goes once its lines have moved out of it (below).
        var goneLists = lists.Skip(listPlans.Count).ToList();
        lists.RemoveRange(listPlans.Count, goneLists.Count);
        listViews.Clear();
        listShapes.Clear();
        foreach (var list in lists)
        {
            Try(() => list.Shape.CornerRadius = new Vector2(radius, radius));
            listViews.Add(list.View);
            listShapes.Add(list.Shape);
        }

        // Each card of a list in a view of its own inside the list's, inside the rim where the host draws it
        // outside the material (CardClip), or, for a graph, to the card's edge under it; placed by PlaceLines.
        foreach (var run in runs.Values)
        {
            run.Seen = false;
        }
        runViews.Clear();
        for (var list = 0; list < listPlans.Count; list++)
        {
            for (var at = 0; at < listPlans[list].Runs.Count; at++)
            {
                foreach (var rim in new[] { false, true })
                {
                    if (!runs.TryGetValue((list, at, rim), out var run))
                    {
                        making = [];
                        var view = Own(c.CreateContainerVisual());
                        var shape = Own(c.CreateRoundedRectangleGeometry());
                        view.Clip = Own(c.CreateGeometricClip(shape));
                        // The ones under the rim over the ones inside it, as each card's two stood.
                        if (rim)
                        {
                            lists[list].Cards.Children.InsertAtTop(view);
                        }
                        else
                        {
                            lists[list].Cards.Children.InsertAtBottom(view);
                        }
                        run = new RunView { View = view, Shape = shape, List = list, Run = at, Rim = rim };
                        run.Owned.AddRange(making);
                        making = null;
                        runs[(list, at, rim)] = run;
                        viewsMade++;
                    }
                    run.Seen = true;
                    var inside = rim ? 0 : host.RimInset;
                    Try(() => run.Shape.CornerRadius = new Vector2(MathF.Max(0, radius - inside), MathF.Max(0, radius - inside)));
                    runViews.Add((run.View, run.Shape, list, at, rim));
                }
            }
        }

        // The card line index stands on, or its list's view for a divider, which stands between two; the
        // card's view under the rim for what runs there (CellIsland.UnderRim).
        int ListOf(int index) => shown is { } laid && index < laid.CardList.Count ? laid.CardList[index] : 0;
        ContainerVisual CardViewOf(int index, bool rim)
        {
            var list = ListOf(index);
            var at = shown is { } laid && index < laid.CardRun.Count ? laid.CardRun[index] : -1;
            if (runs.TryGetValue((list, at, rim), out var run) && run.Seen)
            {
                return run.View;
            }
            return lists.Count > 0 ? lists[Math.Clamp(list, 0, lists.Count - 1)].Cards : listsLayer!;
        }
        ContainerVisual MarksOf(int index) => lists.Count > 0 ? lists[Math.Clamp(ListOf(index), 0, lists.Count - 1)].Marks : listsLayer!;

        foreach (var line in lines.Values)
        {
            line.Seen = false;
        }
        cards.Clear();
        washes.Clear();
        frosts.Clear();
        fills.Clear();
        contents.Clear();
        bands.Clear();
        var fade = shown is { Editing: true } && look.Animations ? washFade : null;
        // §489: kept for the window's life, at the speed the look has now.
        if (fade?["Color"] is KeyFrameAnimation washing)
        {
            washing.Duration = look.Motion.Time(MotionKind.Hover);
        }
        for (var index = 0; index < tiles.Count; index++)
        {
            // Its widget's key; a second line of one widget, which a folder never has, a key of its own.
            var key = tiles[index].Island.Key;
            for (var again = 1; lines.TryGetValue(key, out var taken) && taken.Seen; again++)
            {
                key = $"{tiles[index].Island.Key}#{again}";
            }
            if (!lines.TryGetValue(key, out var line))
            {
                line = MakeLine(c, key);
            }
            line.Seen = true;
            var view = CardViewOf(index, rim: false);
            // A graph runs under the rim to the card's edge; its line's fill stays inside the rim.
            var holds = tiles[index].Island is CellIsland { UnderRim: true } ? CardViewOf(index, rim: true) : view;
            Try(() =>
            {
                // edit: a line lifted over the folder stays on its plate (FolderFlyout.Drag.cs); it goes back
                // into where it stands now as it is let down.
                if (raised.Find(lifted => lifted.Body == line.Card.Parent) is { } lifted)
                {
                    (lifted.CardHome, lifted.Home, lifted.Index) = (view, holds, index);
                }
                else
                {
                    if (line.Card.Parent != view)
                    {
                        line.Card.Parent?.Children.Remove(line.Card);
                        line.Sprite.Parent?.Children.Remove(line.Sprite);
                        view.Children.InsertAtTop(line.Card);
                    }
                    if (line.Sprite.Parent != holds)
                    {
                        line.Sprite.Parent?.Children.Remove(line.Sprite);
                        holds.Children.InsertAtTop(line.Sprite);
                    }
                }
                var marks = MarksOf(index);
                // §502: a lifted spacer's stripes stay on its carrier, and go back to where it stands now.
                if (raised.Find(each => each.Mark == line.Band.Visual) is { } bearing)
                {
                    bearing.MarkHome = marks;
                }
                else if (line.Band.Visual.Parent != marks)
                {
                    line.Band.Visual.Parent?.Children.Remove(line.Band.Visual);
                    marks.Children.InsertAtTop(line.Band.Visual);
                }
                line.Wash.ImplicitAnimations = fade;
            });
            cards.Add(line.Card);
            washes.Add(line.Wash);
            frosts.Add(line.Frost);
            fills.Add(line.Fill);
            contents.Add(line);
            bands.Add(line.Band);
        }
        foreach (var gone in lines.Values.Where(line => !line.Seen).ToList())
        {
            Release(gone);
            lines.Remove(gone.Key);
        }
        foreach (var gone in runs.Values.Where(run => !run.Seen).ToList())
        {
            Try(() => gone.View.Parent?.Children.Remove(gone.View));
            FlyoutHost.CloseAll(gone.Owned);
            runs.Remove((gone.List, gone.Run, gone.Rim));
        }
        foreach (var gone in goneLists)
        {
            Try(() => listsLayer!.Children.Remove(gone.View));
            FlyoutHost.CloseAll(gone.Owned);
        }

        // edit: the columns' round buttons, by what each is.
        foreach (var button in buttons.Values)
        {
            button.Seen = false;
        }
        addVisuals.Clear();
        foreach (var add in shown?.Adds ?? [])
        {
            var key = AddKeyOf(add);
            for (var again = 1; buttons.TryGetValue(key, out var taken) && taken.Seen; again++)
            {
                key = $"{AddKeyOf(add)}#{again}";
            }
            if (!buttons.TryGetValue(key, out var button))
            {
                making = [];
                var sprite = Own(c.CreateSpriteVisual());
                var brush = Own(c.CreateSurfaceBrush());
                sprite.Brush = brush;
                addsLayer!.Children.InsertAtTop(sprite);
                button = new Button { Key = key, Sprite = sprite, Brush = brush };
                button.Owned.AddRange(making);
                making = null;
                buttons[key] = button;
                buttonsMade++;
            }
            button.Seen = true;
            addVisuals.Add(button);
        }
        foreach (var gone in buttons.Values.Where(button => !button.Seen).ToList())
        {
            Try(() => addsLayer!.Children.Remove(gone.Sprite));
            gone.Surface?.Dispose();
            FlyoutHost.CloseAll(gone.Owned);
            buttons.Remove(gone.Key);
        }
    }

    /// <summary>§357: a line of the folder, made for the widget keyed <paramref name="key"/>; placed where it stands by <see cref="Reconcile"/>.</summary>
    private Line MakeLine(Compositor c, string key)
    {
        making = [];
        // §464: a line is its own surface over the desktop: the blur under its background where that is see-through
        // (Frost, LineFill) and its background, in one holder that moves as the line does; §624 no outline of its own.
        var holder = Own(c.CreateContainerVisual());
        var blur = Own(c.CreateSpriteVisual());
        blur.Brush = frost;
        blur.IsVisible = false;
        holder.Children.InsertAtTop(blur);
        // Its background a sprite on whole px, so two lines meet edge to edge with no seam of what is under them
        // (two shapes' smoothed edges on a half px let it through).
        var wash = Own(c.CreateColorBrush(Color.FromArgb(0, 0, 0, 0)));
        var fill = Own(c.CreateSpriteVisual());
        fill.Brush = wash;
        holder.Children.InsertAtTop(fill);
        var visual = Own(c.CreateContainerVisual());
        visual.RelativeSizeAdjustment = Vector2.One;
        holder.Children.InsertAtTop(visual);
        var sprite = Own(c.CreateSpriteVisual());
        var brush = Own(c.CreateSurfaceBrush());
        sprite.Brush = brush;
        // §462: over its fill its pattern, the size of its box, the section's clip rounding it with the section.
        var pattern = Own(new PatternLayer(c));
        visual.Children.InsertAtTop(pattern.Visual);
        // edit: a spacer's room striped, moving (§524, FolderPlan.Bands; was §307's rule across its gap); at rest it is
        // empty space and shows nothing (§505). It is taken hold of over its grab all the same.
        var band = new SpacerStripes(c, value => Own(value));
        band.Wear(bandInk);
        var line = new Line
        {
            Key = key,
            Card = holder,
            Frost = blur,
            Fill = fill,
            Wash = wash,
            Sprite = sprite,
            Brush = brush,
            Band = band,
            Pattern = pattern,
        };
        line.Owned.AddRange(making);
        making = null;
        lines[key] = line;
        linesMade++;
        return line;
    }

    /// <summary>
    /// §649: line <paramref name="index"/>'s divider line, standing in its <paramref name="room"/> (panel px), shown while the
    /// folder stands flat on its ground (<see cref="Flat"/>) and the line is a divider that shows; hidden for any other.
    /// Made the first time, with the line, in its holder (<see cref="Line.Card"/>), so it moves as the line does.
    /// </summary>
    private void PlaceRule(Line line, int index, D2D_RECT_F room)
    {
        var ruled = Flat && index < tiles.Count && Divides(tiles[index].Cell) && plan?.Shows(index) == true;
        if (!ruled)
        {
            if (line.Rule is { } unused)
            {
                unused.IsVisible = false;
            }
            return;
        }
        if (line.Rule is null)
        {
            var made = host.Compositor.CreateSpriteVisual();
            objectsMade++;
            line.Owned.Add(made);
            made.Brush = ruleInk;
            line.Card.Children.InsertAtTop(made);
            line.Rule = made;
        }
        var rule = RuleOf(room, host.Rim);
        line.Rule.Offset = new Vector3(rule.left - room.left, rule.top - room.top, 0);
        line.Rule.Size = new Vector2(rule.right - rule.left, rule.bottom - rule.top);
        line.Rule.IsVisible = true;
    }

    /// <summary>
    /// §704: line <paramref name="line"/>'s Liquid glass hairline, <paramref name="seam"/> (panel px; null for none), placed in its
    /// holder from its <paramref name="room"/>'s corner, so it moves as the line does; in the glass's divider ink.
    /// </summary>
    private void PlaceSeam(Line line, D2D_RECT_F room, GlassBox? seam)
    {
        if (seam is not { } at || look.Liquid is not { } glass)
        {
            if (line.Seam is { } unused)
            {
                unused.IsVisible = false;
            }
            return;
        }
        if (line.Seam is null)
        {
            var made = host.Compositor.CreateSpriteVisual();
            var ink = host.Compositor.CreateColorBrush();
            objectsMade += 2;
            line.Owned.AddRange([made, ink]);
            made.Brush = ink;
            line.Card.Children.InsertAtTop(made);
            (line.Seam, line.SeamInk) = (made, ink);
        }
        line.SeamInk!.Color = GlassPaint.DividerInk(glass);
        line.Seam.Offset = new Vector3((float)at.Left - room.left, (float)at.Top - room.top, 0);
        line.Seam.Size = new Vector2((float)at.Width, (float)at.Height);
        line.Seam.IsVisible = true;
    }

    /// <summary>
    /// §704: each line's Liquid glass hairline in <paramref name="plan"/>, by index (null for none): the lines on a card that
    /// meet in a piece joined into one glass shape (LiquidGlass.Join), each with a line above it parted from it, the glass's
    /// inset short of the piece's sides. None while the folder stands flat on its ground, or in another look.
    /// </summary>
    internal IReadOnlyList<GlassBox?> Seams(FolderPlan plan) => SeamsOf(plan, Flat ? null : look.Liquid, scale, index => !Flat && index < tiles.Count && Carded(tiles[index].Cell));

    /// <inheritdoc cref="Seams(FolderPlan)"/>
    internal static IReadOnlyList<GlassBox?> SeamsOf(FolderPlan plan, GlassLook? glass, float scale, Func<int, bool> carded)
    {
        if (glass is null)
        {
            return [];
        }
        var boxes = plan.Cards.Select((card, index) => plan.Shows(index) && plan.RunOf(index) is not null && carded(index)
            ? new GlassBox(card.left, card.top, card.right, card.bottom) : default).ToList();
        var parts = LiquidGlass.Join(boxes, MathF.Max(1, MathF.Round(glass.DividerDip * scale)), MathF.Round(glass.DividerInsetDip * scale), touch: 1.5);
        return [.. parts.Select((part, index) => boxes[index].Width > 0 ? part.Divider : null)];
    }

    // §489: the widget each line drew last in this showing, by its key: one made again under the same key (another style or
    // view picked in its menu while the folder stands open, §224) is content swapped in place.
    private readonly Dictionary<string, IIsland> drawnIslands = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// §489: line <paramref name="index"/>'s content comes in by the one motion's swap where it was swapped in place since it
    /// was last drawn — a subfolder gone into or back (<see cref="CellIsland.TakeSwap"/>), another view, its widget made again
    /// in another style — the content alone, from that way (mirrored where the folder is). Nothing at once, nothing moves
    /// that was not swapped.
    /// </summary>
    private void SwapLine(int index)
    {
        var island = tiles[index].Island;
        var way = island is CellIsland cell ? cell.TakeSwap() : 0;
        if (way == 0 && drawnIslands.TryGetValue(island.Key, out var was) && !ReferenceEquals(was, island))
        {
            way = 1;
        }
        drawnIslands[island.Key] = island;
        if (way != 0 && !editing)
        {
            var line = contents[index];
            Try(() => MotionEase.SwapIn(host.Compositor, line.Sprite, line.Sprite.Offset, Vector3.UnitX, Mirror.On ? -way : way, scale, look.Motion, line.Owned.Add));
        }
    }

    /// <summary>
    /// §489: a page opened in a column (<paramref name="way"/> +1) or closed (−1): the lines not shown before
    /// (<paramref name="before"/>, their keys) come in by the one motion's swap, card and content together.
    /// </summary>
    private void SwapLines(IReadOnlySet<string> before, int way)
    {
        for (var index = 0; index < tiles.Count && index < contents.Count; index++)
        {
            if (before.Contains(tiles[index].Island.Key))
            {
                continue;
            }
            var line = contents[index];
            Try(() =>
            {
                foreach (var visual in new Visual[] { line.Card, line.Sprite })
                {
                    MotionEase.SwapIn(host.Compositor, visual, visual.Offset, Vector3.UnitX, Mirror.On ? -way : way, scale, look.Motion, line.Owned.Add);
                }
            });
        }
    }

    /// <summary>
    /// §357: nothing of the folder kept moves any more — a glide or a fade of a settle cut off as the folder
    /// closes — so its next showing places every line, clip and button where it has them, at once.
    /// </summary>
    private void StillKept()
    {
        Try(() =>
        {
            foreach (var line in lines.Values)
            {
                foreach (var visual in new Visual[] { line.Card, line.Sprite, line.Band.Visual })
                {
                    visual.StopAnimation("Offset");
                    visual.StopAnimation("Opacity");
                    visual.Opacity = 1;
                }
                // §524: a spacer's stripes move only while the folder shows; they start again as it is arranged again.
                line.Band.Move(bandLoop, on: false);
            }
            // The marching ants too: they come back round their lines as the folder shows them again (ShowAnts).
            foreach (var each in ants)
            {
                each.Show(false, default, default, 0, 0);
            }
            foreach (var button in buttons.Values)
            {
                button.Sprite.StopAnimation("Offset");
            }
            foreach (var list in lists)
            {
                list.Shape.StopAnimation("Offset");
                list.Shape.StopAnimation("Size");
            }
            foreach (var run in runs.Values)
            {
                run.Shape.StopAnimation("Offset");
                run.Shape.StopAnimation("Size");
            }
            ghostVisual?.StopAnimation("Offset");
        });
    }

    /// <summary>§357: a line gone from the folder let go of: out of the tree, its surface and what was made for it closed.</summary>
    private static void Release(Line line)
    {
        Try(() =>
        {
            line.Card.Parent?.Children.Remove(line.Card);
            line.Sprite.Parent?.Children.Remove(line.Sprite);
            line.Band.Visual.Parent?.Children.Remove(line.Band.Visual);
        });
        line.Surface?.Dispose();
        if (line.Strip is { } strip)
        {
            strip.Surface?.Dispose();
            FlyoutHost.CloseAll(strip.Owned);
        }
        FlyoutHost.CloseAll(line.Owned);
    }

    /// <summary>
    /// §424: line <paramref name="line"/>'s strip (LineStrip.cs) where its widget <paramref name="sliding"/> has one in
    /// view (<paramref name="strip"/>, line px of a line <paramref name="width"/> wide): its window placed over the
    /// line, the strip drawn into its surface, and its sprite bound to the widget's motion; hidden where it has none.
    /// </summary>
    private void PlaceStrip(Line line, IStripLine? sliding, LineStrip? strip, float width, TileStyle style, bool turned)
    {
        if (sliding is null || strip is not { } shows)
        {
            if (line.Strip is { } hidden)
            {
                hidden.View.IsVisible = false;
            }
            return;
        }
        var c = host.Compositor;
        if (line.Strip is not { } view)
        {
            var owned = new List<IDisposable>();
            T Kept<T>(T made)
                where T : IDisposable
            {
                owned.Add(made);
                objectsMade++;
                return made;
            }
            var window = Kept(c.CreateContainerVisual());
            window.Clip = Kept(c.CreateInsetClip());
            var brush = Kept(c.CreateSurfaceBrush());
            brush.Stretch = CompositionStretch.None;
            brush.HorizontalAlignmentRatio = 0f;
            brush.VerticalAlignmentRatio = 0f;
            var sprite = Kept(c.CreateSpriteVisual());
            sprite.Brush = brush;
            window.Children.InsertAtTop(sprite);
            line.Sprite.Children.InsertAtBottom(window);
            view = new StripView { View = window, Sprite = sprite, Brush = brush };
            view.Owned.AddRange(owned);
            line.Strip = view;
        }
        var box = shows.Window;
        // Arabic and Urdu: the line is mirrored, and so is where its strip shows and what is drawn in it (Mirror.cs).
        var left = turned ? width - box.right : box.left;
        view.View.Offset = new Vector3(left, box.top, 0);
        view.View.Size = new Vector2(box.right - box.left, box.bottom - box.top);
        view.View.IsVisible = true;
        var stripStyle = style;
        view.Surface = Paint(view.Surface, shows.Size.X, shows.Size.Y, dc =>
        {
            var was = turned ? Mirror.Begin(dc, 0, shows.Size.X) : default;
            try
            {
                sliding.PaintStrip(dc, stripStyle);
            }
            finally
            {
                if (turned)
                {
                    _ = Mirror.End(dc, was);
                }
            }
        }, view.Brush);
        view.Sprite.Size = shows.Size;
        var motion = sliding.Motion(c);
        if (!ReferenceEquals(view.Bound, motion))
        {
            var bind = c.CreateExpressionAnimation("Vector3(0, motion.top - motion.y, 0)"); // not-ui: an expression
            view.Owned.Add(bind);
            objectsMade++;
            bind.SetReferenceParameter("motion", motion);
            view.Sprite.StartAnimation("Offset", bind);
            view.Bound = motion;
        }
    }

    /// <summary>
    /// §357: what a column's button is, whichever its place among them: a list's "+", width or warning by
    /// its list, a slot's by the side it stands on and how far out. A warning come beside an earlier list
    /// leaves every other button the one it was.
    /// </summary>
    internal static string AddKeyOf(FolderAdd add) =>
        add.Place.New ? $"{add.Kind}/slot/{(add.Place.List == 0 ? "start" : "end")}/{add.Place.Apart}" : $"{add.Kind}/{add.List}";

    /// <summary>The index in <paramref name="shown"/>'s buttons of the one keyed <paramref name="key"/>; -1 for none.</summary>
    private static int AddIndexOf(FolderPlan shown, string? key)
    {
        for (var index = 0; key is not null && index < shown.Adds.Count; index++)
        {
            if (AddKeyOf(shown.Adds[index]) == key)
            {
                return index;
            }
        }
        return -1;
    }

    /// <summary>
    /// §357: everything the folder made goes to its host, which closes it once its window no longer shows it:
    /// the folder goes for good (<see cref="Dispose"/>), its last fade run out.
    /// </summary>
    private void ReleaseKept()
    {
        foreach (var line in lines.Values)
        {
            if (line.Surface is not null)
            {
                host.Keep(line.Surface);
            }
            foreach (var value in line.Owned)
            {
                host.Keep(value);
            }
        }
        lines.Clear();
        foreach (var button in buttons.Values)
        {
            if (button.Surface is not null)
            {
                host.Keep(button.Surface);
            }
            foreach (var value in button.Owned)
            {
                host.Keep(value);
            }
        }
        buttons.Clear();
        foreach (var value in lists.SelectMany(list => list.Owned).Concat(runs.Values.SelectMany(run => run.Owned))
            .Concat(spareRaised.Concat(raised).SelectMany(lifted => lifted.Owned)).Concat(antsOwned.SelectMany(owned => owned)).Concat(retained))
        {
            host.Keep(value);
        }
        ants.Clear();
        antsOwned.Clear();
        foreach (var surface in new[] { emptySurface, warningSurface })
        {
            if (surface is not null)
            {
                host.Keep(surface);
            }
        }
        lists.Clear();
        runs.Clear();
        raised.Clear();
        spareRaised.Clear();
        retained.Clear();
    }
}
