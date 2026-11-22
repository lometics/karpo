// The bar's tree kept (docs\widgets.md §358; the flicker design's phase 4, rule R1 for the bars).
//
// A bar's scene is made once, with its window, and laid out again by Update at every change: each
// thing it shows — a part's card, a widget's cell, an app's button and its indicator, a tray icon, a
// "+", the dress of a block in edit mode — is an element kept by a key (what it is: its
// part's key, its app, its icon) with a signature of what it is drawn from. An update keeps what draws
// the same, moves what only moved (gliding there, from where it was, where the bar laid out as before),
// makes again only what draws otherwise, and lets go of what is gone, in the same commit. An update
// with nothing changed makes nothing. What is the window's — its layers, the highlight under the
// pointer, the tooltip, the shared brushes — is made once and only coloured again, so a rebuild never
// fades the highlight or the tooltip in again, and a colour picked is set in place.

using System.Numerics;
using Karpo.Core;
using Windows.UI.Composition;
using Color = Windows.UI.Color;

namespace Karpo.Dock;

public sealed partial class Scene
{
    /// <summary>
    /// What an element is drawn from, item by item: two signatures with equal items draw the same,
    /// and an element whose signature is unchanged is kept. Where it stands is not in it: an element
    /// that only moved is moved.
    /// </summary>
    private sealed class Sig(params object?[] items) : IEquatable<Sig>
    {
        private readonly object?[] items = items;

        public bool Equals(Sig? other)
        {
            if (other is null || other.items.Length != items.Length)
            {
                return false;
            }
            for (var at = 0; at < items.Length; at++)
            {
                if (!Equals(items[at], other.items[at]))
                {
                    return false;
                }
            }
            return true;
        }

        public override bool Equals(object? obj) => Equals(obj as Sig);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var item in items)
            {
                hash.Add(item);
            }
            return hash.ToHashCode();
        }
    }

    /// <summary>What has a visual to stand in a container: a shape, a plate, a view of several.</summary>
    private interface IShown
    {
        Visual Visual { get; }
    }

    /// <summary>
    /// One thing the bar shows, kept by its key: what was made (<see cref="Thing"/>), the visual it
    /// stands in its container by, what it was drawn from, and everything made for it, closed with it.
    /// </summary>
    private sealed class Element(string key, Sig sig, Karpo.Core.TextStyle typeface)
    {
        public string Key { get; } = key;

        public Sig Sig { get; } = sig;

        /// <summary>§499: the font it was drawn in (<see cref="Gfx.Typeface"/>): drawn in another, it is made again.</summary>
        public Karpo.Core.TextStyle Typeface { get; } = typeface;

        public object Thing { get; set; } = null!;

        public Visual Visual { get; set; } = null!;

        /// <summary>Everything made for it, closed when it goes.</summary>
        public List<IDisposable> Bag { get; } = [];

        /// <summary>Its brushes in a colour somebody may choose, coloured again in place (<see cref="Dye"/>).</summary>
        public List<Swatched> Swatches { get; } = [];

        public ContainerVisual? Container { get; set; }

        /// <summary>The update it was last shown in, and whether it was made in it.</summary>
        public uint Seen { get; set; }

        public bool Fresh { get; set; }
    }

    /// <summary>A brush in a colour somebody may choose: how it reads the colour from the tokens, and whose tokens (a part's key; null for the bar's).</summary>
    private readonly record struct Swatched(CompositionColorBrush Brush, Func<Tokens, Color> Pick);

    /// <summary>§358: every element the bar shows, by its key, in the order they were made.</summary>
    private readonly Dictionary<string, Element> elements = [];

    /// <summary>Where what is made now goes: an element's bag while it is made, else the scene's own.</summary>
    private List<IDisposable> sink;

    /// <summary>Where a swatch made now is listed: an element's while it is made, else the scene's own.</summary>
    private List<Swatched> swatchSink;

    /// <summary>Each container's element last stood this update: the next one made goes over it.</summary>
    private readonly Dictionary<ContainerVisual, Visual> cursors = new(ReferenceEqualityComparer.Instance);

    /// <summary>The update now being laid out; an element seen in an earlier one only is let go of.</summary>
    private uint pass;

    /// <summary>Whether what moved in this update glides there: animations on, and the bar laid out as before (<see cref="Comparable"/>).</summary>
    private bool glide;

    // What a hit is, by key (an app's button by its app, a tray icon by its icon, a cell by its widget),
    // and back: the highlight and the tooltip find their item again after an update.
    private readonly Dictionary<Hit, string> hitKeys = [];
    private readonly Dictionary<string, Hit> keyHits = [];

    // tests: elements made, surfaces drawn, and fades in begun (the highlight, the tooltip, edit mode's marks), over the scene's life.
    private int made;
    private int drawn;
    private int fadesIn;

    /// <summary>tests: the fades in begun over the scene's life: the highlight, the tooltip, edit mode's marks.</summary>
    internal int FadesIn => fadesIn;

    /// <summary>
    /// tests (§358): what the scene has made and drawn over its life, and what it holds now — every
    /// composition object of its own and its elements'. An update with nothing changed changes none.
    /// </summary>
    internal (int Made, int Drawn, int Held) Churn =>
        (made, drawn, owned.Count + elements.Values.Sum(element => element.Bag.Count) + partViews.Values.Sum(view => view.Bag.Count));

    /// <summary>tests: what the last update moved and glided there, by key: from where, to where.</summary>
    internal IReadOnlyDictionary<string, (Vector3 From, Vector3 To)> Glides => glides;

    private readonly Dictionary<string, (Vector3 From, Vector3 To)> glides = [];

    /// <summary>tests: whether the element <paramref name="key"/> was made by the last update rather than kept.</summary>
    internal bool MadeLast(string key) => elements.TryGetValue(key, out var element) && element.Fresh;

    /// <summary>
    /// Lays the bar out as <paramref name="layout"/> with <paramref name="content"/> in
    /// <paramref name="look"/> (§358): what stays is kept, what moved glides there, what changed is drawn
    /// again, what is gone goes — the highlight, the tooltip and a press where they are. <paramref name="edit"/>
    /// is the view of a bar in edit mode (its layout made with editing on, see <see cref="Edit"/>); its
    /// marks fade in as edit mode begins. <paramref name="origin"/> is where the layout's window lies in
    /// the real one, which stands at its edge's most thickness (<see cref="Layout.WindowFor"/>).
    /// <paramref name="shown"/> is whether the bar is in sight: a bar shown or hidden otherwise is put so at
    /// once, and one sliding in or out goes on sliding.
    /// </summary>
    public void Update(Layout layout, Content content, Look look, Icons icons, bool shown, EditView? edit = null, Vector2 origin = default)
    {
        if (disposed)
        {
            return;
        }
        // §507: the bar's own text in its layout's Taskbar widgets' font; each widget draws in its own (CellIsland).
        using var typeface = gfx.Wearing(look.Typeface);
        var first = !updated;
        var before = updated ? this.layout : null;
        var wasEditing = before?.Editing == true;
        // §612: what edit mode's dress left fading out, let go of once its fade is over.
        LetFadedGo();
        // §612: edit mode's dress taken off (edit mode over, the Appearance's preview): its marks and its "+" fade out where
        // they stand, the blocks gliding together under them, by the one motion; at once with it off.
        undressing = wasEditing && !layout.Editing && look.Animations;
        // What the pointer, the tooltip and a carried block are on, by what each is, before the parts are counted again.
        var litWas = lit is { } on ? IdentityOf(on) : null;
        var tipWas = tipShown && tipOver is { } over ? IdentityOf(over) : null;
        (PartVisuals View, Vector3 At)? dropped = liftedPart >= 0 && liftedPart < parts.Length && parts[liftedPart] is { } held && liftedAt is { } at
            ? (held, at)
            : null;
        Lower();

        this.layout = layout;
        this.content = content;
        this.look = look;
        this.icons = icons;
        tokens = Glassed(Theme(look, layout), look, behind);
        glide = !first && look.Animations && Comparable(before!, layout);
        updated = true;
        pass++;
        glides.Clear();
        cursors.Clear();
        tiles.Clear();
        backs.Clear();
        marks.Clear();
        pills.Clear();
        pillCentres.Clear();
        signalPlaces.Clear();
        signalVisuals.Clear();
        pressables.Clear();
        hitKeys.Clear();
        keyHits.Clear();
        trayKeys.Clear();
        trayPictures.Clear();
        searchPlates.Clear();
        restPlates.Clear();
        parts = new PartVisuals?[layout.Specs.Count];
        foreach (var view in partViews.Values)
        {
            view.Unmarked();
        }
        root.Size = new Vector2(layout.Width, layout.Height);
        if (layout.Editing)
        {
            this.edit ??= new EditBrushes(compositor);
        }
        else
        {
            // §612: the marks fading out keep their brushes until they are gone.
            if (this.edit is { } brushes)
            {
                Faded(undressing, brushes.Dispose);
            }
            this.edit = null;
            foreach (var view in partViews.Values)
            {
                view.Lit = false;
            }
        }

        Compose();
        Sweep();
        Dye();

        // The indicators' first state is set before their animations are attached, or a new button's
        // would grow in from nothing; a button kept animates to its new one, unless the bar is laid out
        // otherwise (another edge, scale or size), where everything stands at once.
        var fresh = new bool[pills.Count];
        for (var index = 0; index < content.Tiles.Count && index < pills.Count; index++)
        {
            fresh[index] = pills[index].ImplicitAnimations is null;
            if (!glide && !fresh[index])
            {
                Still(index, () => SetMark(index, content.Tiles[index].Mark));
                continue;
            }
            SetMark(index, content.Tiles[index].Mark);
        }
        Animate(fresh);

        // edit: the block just dropped glides into its place from where the carry window held it,
        // as a folder's line settles from where it was let go (FolderFlyout.Settle).
        if (dropped is { } drop && glide && partViews.ContainsValue(drop.View) && drop.View.Seen == pass)
        {
            drop.View.Shift = drop.At;
            foreach (var (container, _) in LayersOf(drop.View))
            {
                container.StopAnimation("Offset");
                container.Offset = drop.At;
            }
        }
        if (layout.Editing)
        {
            Apply(edit, glide);
            if (look.Animations)
            {
                AnimateEdit(wasEditing, before);
            }
        }
        else
        {
            Rest();
        }
        // folders: the open folder's face where it stands now, before the highlight lights over it.
        PlaceOpen();
        Relight(litWas);
        LightPlates();
        Retip(tipWas);

        var standing = new Vector3(origin, 0f);
        var away = new Vector3(layout.HiddenOffset(), 0f);
        var moved = standing != this.origin || away != hiddenOffset;
        (this.origin, hiddenOffset) = (standing, away);
        if (tipHolder is not null)
        {
            // §359: the tooltip's window stands where the bar's does: the scene's corner in it is the same.
            tipHolder.Offset = standing;
            // §360: and its region where the tooltip stands now, moved with its item or the bar's corner.
            if (tipShown)
            {
                PlaceTip();
            }
        }
        if (first || shown != presented || moved)
        {
            Present(shown, animate: false);
        }
        retiredLooks?.Dispose();
        retiredLooks = null;
    }

    /// <summary>Everything the bar shows, in the order it stands: each element kept, moved or made (<see cref="Keep{T}"/>).</summary>
    private void Compose()
    {
        ComposePanel();
        placedCards.Clear();
        var carded = ComposeCards();
        DressCards();
        ComposeIslands();
        ComposeButtons();
        ComposeTiles();
        ComposeTray();
        if (layout.Editing)
        {
            ComposeEdit(carded);
        }
        else
        {
            // Out of edit mode its marks go with this update's sweep, and the "+"'s looks with them — §612 fading out first.
            empties = null;
            if (undressing && addButtons is { } looks)
            {
                Faded(true, looks.Dispose);
            }
            else
            {
                retiredLooks = addButtons;
            }
            addButtons = null;
            landingGhost = null;
            if (liftShadow is not null)
            {
                DropLiftShadow();
            }
        }
    }

    // --- Elements ----------------------------------------------------------------------------------

    /// <summary>
    /// The element <paramref name="key"/> in <paramref name="into"/>: kept where it was made from
    /// <paramref name="sig"/> as well, else made again (<paramref name="make"/>), its old self let go of.
    /// <paramref name="place"/> puts it where it stands now, kept or made; a kept one that moved glides
    /// there from where it was (<see cref="Glided"/>), where <paramref name="glides"/> and it showed.
    /// It stands over the element put into the same container before it in this update.
    /// </summary>
    private T Keep<T>(string key, ContainerVisual into, Sig sig, Func<T> make, Action<T>? place = null, bool glides = true)
        where T : class
    {
        // A key a part shown twice gives twice (two widgets of one key) is told apart by its
        // occurrence, in the same order at every update.
        while (elements.TryGetValue(key, out var taken) && taken.Seen == pass)
        {
            key += "'";
        }
        if (elements.TryGetValue(key, out var kept) && kept.Seen != pass && kept.Sig.Equals(sig) && kept.Typeface == gfx.Typeface)
        {
            kept.Seen = pass;
            kept.Fresh = false;
            var visual = kept.Visual;
            var (offset, size, showed) = (visual.Offset, visual.Size, visual.IsVisible);
            using (Making(kept.Bag, kept.Swatches))
            {
                place?.Invoke((T)kept.Thing);
            }
            if (glides && showed)
            {
                Glided(kept, offset, size);
            }
            Stand(kept, into);
            return (T)kept.Thing;
        }
        if (kept is not null)
        {
            elements.Remove(key);
            Retire(kept);
        }
        var element = new Element(key, sig, gfx.Typeface) { Seen = pass, Fresh = true };
        T thing;
        using (Making(element.Bag, element.Swatches))
        {
            thing = make();
            place?.Invoke(thing);
        }
        element.Thing = thing;
        element.Visual = VisualOf(thing);
        elements[key] = element;
        made++;
        Stand(element, into);
        return thing;
    }

    /// <summary>Whether the element <paramref name="key"/> was kept by this update rather than made in it.</summary>
    private bool Kept(string key) => elements.TryGetValue(key, out var element) && element.Seen == pass && !element.Fresh;

    private static Visual VisualOf(object thing) => thing switch
    {
        Visual visual => visual,
        IShown shown => shown.Visual,
        MarchingAnts ants => ants.Visual,
        _ => throw new ArgumentException($"no visual in {thing.GetType().Name}", nameof(thing)),
    };

    /// <summary>
    /// <paramref name="element"/> in <paramref name="into"/>, over the one put there before it in this
    /// update: one new to it goes there; one already in it stays where it stands among the others.
    /// </summary>
    private void Stand(Element element, ContainerVisual into)
    {
        var visual = element.Visual;
        if (!ReferenceEquals(element.Container, into) || visual.Parent is null)
        {
            if (visual.Parent is { } parent)
            {
                parent.Children.Remove(visual);
            }
            if (cursors.TryGetValue(into, out var under) && ReferenceEquals(under.Parent, into))
            {
                into.Children.InsertAbove(visual, under);
            }
            else
            {
                into.Children.InsertAtBottom(visual);
            }
            element.Container = into;
        }
        cursors[into] = visual;
    }

    /// <summary>
    /// A kept element moved by this update, from <paramref name="offset"/> and <paramref name="size"/>
    /// where it stood: it glides there with the Windows easing, a shape growing with it — what the
    /// next scene used to find of the last one by its key, now the same visual.
    /// </summary>
    private void Glided(Element element, Vector3 offset, Vector2 size)
    {
        if (!glide)
        {
            return;
        }
        var visual = element.Visual;
        if (offset != visual.Offset)
        {
            Slide(visual, offset, visual.Offset, true);
            glides[element.Key] = (offset, visual.Offset);
        }
        if (size != visual.Size)
        {
            var grow = compositor.CreateVector2KeyFrameAnimation();
            grow.InsertKeyFrame(0f, size);
            grow.InsertKeyFrame(1f, visual.Size, decelerate);
            grow.Duration = Time(MotionKind.Move);
            visual.StartAnimation("Size", grow);
            grow.Dispose();
            // A shape is sized by itself (Rounded.Place), not by its visual: it grows too, or a card
            // would jump to its new length at once while what stands on it slid.
            if (ShapeOf(element.Thing) is { } shape)
            {
                shape.GrowFrom(compositor, size, decelerate, Time(MotionKind.Move));
            }
        }
    }

    private static Rounded? ShapeOf(object thing) => thing switch
    {
        Rounded rounded => rounded,
        Shaped shaped => shaped.Shape,
        SearchPlate plate => plate.Shape,
        _ => null,
    };

    /// <summary>Lets go of every element this update did not show, and of every part the bar has no more.</summary>
    private void Sweep()
    {
        var gone = elements.Values.Where(element => element.Seen != pass).ToList();
        foreach (var element in gone)
        {
            elements.Remove(element.Key);
            // §612: edit mode's marks and its "+" fade out as its dress is taken off.
            if (undressing && IsEditMark(element.Key))
            {
                FadeOut(element);
                continue;
            }
            Retire(element);
        }
        foreach (var (key, view) in partViews.Where(pair => pair.Value.Seen != pass).ToList())
        {
            partViews.Remove(key);
            var layers = LayersOf(view);
            LetSignalsGo(home => view.Bag.Contains(home) || layers.Any(layer => ReferenceEquals(layer.Container, home)));
            foreach (var (container, _) in layers)
            {
                container.Parent?.Children.Remove(container);
            }
            Close(view.Bag);
        }
        foreach (var key in signals.Keys.Where(key => !signalsSeen.Contains(key)).ToList())
        {
            var gone2 = signals[key];
            signals.Remove(key);
            gone2.RunHome?.Children.Remove(gone2.Run);
            if (gone2.Corner is { } corner)
            {
                gone2.CornerHome?.Children.Remove(corner);
            }
            Close(gone2.Bag);
        }
        signalsSeen.Clear();
        foreach (var key in trayGlides.Keys.Where(key => !elements.ContainsKey(TrayPrefix + key)).ToList())
        {
            trayGlides.Remove(key);
            trayShifts.Remove(key);
        }
    }

    /// <summary>An element gone: out of its container, and everything made for it closed.</summary>
    private void Retire(Element element)
    {
        if (ReferenceEquals(pressedVisual, element.Visual))
        {
            pressedVisual = null;
        }
        LetSignalsGo(home => ReferenceEquals(home, element.Visual) || element.Bag.Contains(home));
        element.Visual.Parent?.Children.Remove(element.Visual);
        Close(element.Bag);
    }

    private static void Close(List<IDisposable> bag)
    {
        foreach (var item in bag)
        {
            try
            {
                item.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
        }
        bag.Clear();
    }

    /// <summary>What <see cref="Making"/> hands back: the sinks as they were, put back as it is disposed.</summary>
    private readonly struct Sinking(Scene scene, List<IDisposable> bag, List<Swatched> swatched) : IDisposable
    {
        public void Dispose() => (scene.sink, scene.swatchSink) = (bag, swatched);
    }

    /// <summary>What is made and every swatch until the result is disposed goes into <paramref name="bag"/> and <paramref name="swatched"/>.</summary>
    private Sinking Making(List<IDisposable> bag, List<Swatched> swatched)
    {
        var was = new Sinking(this, sink, swatchSink);
        (sink, swatchSink) = (bag, swatched);
        return was;
    }

    /// <summary>A surface drawn for the bar, counted (<see cref="Churn"/>) and owned as anything made is.</summary>
    private T Drew<T>(T surface)
        where T : class, IDisposable
    {
        drawn++;
        return Own(surface);
    }

    /// <summary>tests: the visual registered under <paramref name="key"/>, the element's, so a test can ask where in the tree a thing actually ended up. Nothing in the dock looks a visual up by name.</summary>
    internal Visual? Registered(string key) => elements.TryGetValue(key, out var element) ? element.Visual : null;

    /// <summary>tests: §462: the pattern's layer of the card or the panel kept as <paramref name="key"/>; null for none.</summary>
    internal PatternLayer? PatternOf(string key) => elements.TryGetValue(key, out var element)
        ? element.Thing switch { Shaped card => card.Shape.PatternLayer, PanelView panel => panel.Pattern, _ => null }
        : null;

    /// <summary>Registers what shrinks when <paramref name="hit"/> is pressed, about its own centre, and what it is (<paramref name="key"/>).</summary>
    private void Pressable(Hit hit, Visual visual, string key)
    {
        visual.CenterPoint = new Vector3(visual.Size.X / 2f, visual.Size.Y / 2f, 0f);
        var at = hit.In(layout.PartOf(hit));
        pressables[at] = visual;
        Known(at, key);
    }

    /// <summary>What <paramref name="hit"/> is, by <paramref name="key"/>: an update finds the highlight's and the tooltip's item by it.</summary>
    private void Known(Hit hit, string key)
    {
        var at = hit.In(layout.PartOf(hit));
        hitKeys[at] = key;
        keyHits[key] = at;
    }

    private string? IdentityOf(Hit hit) => hitKeys.GetValueOrDefault(hit.In(layout.PartOf(hit)));

    /// <summary>
    /// The highlight on the item it was on before the update (<paramref name="was"/>, by what it is),
    /// where that is now, moved there with it rather than faded in again; gone with it.
    /// </summary>
    private void Relight(string? was)
    {
        if (lit is not { } old)
        {
            return;
        }
        Hit? now = was is null ? (layout.ItemRect(old).IsEmpty ? null : old) : keyHits.TryGetValue(was, out var found) ? found : null;
        if (now is not { } hit || layout.ItemRect(hit).IsEmpty)
        {
            lit = null;
            highlight.Visual.StopAnimation("Opacity");
            highlight.Visual.Opacity = 0f;
            LightSearch(null, false);
            return;
        }
        lit = hit;
        var (offset, size) = (highlight.Visual.Offset, highlight.Visual.Size);
        Dress(highlight, highlightFill, hit, litPressed);
        if (glide)
        {
            if (offset != highlight.Visual.Offset)
            {
                Slide(highlight.Visual, offset, highlight.Visual.Offset, true);
            }
            if (size != highlight.Visual.Size)
            {
                highlight.GrowFrom(compositor, size, decelerate, Time(MotionKind.Move));
            }
        }
    }

    /// <summary>The tooltip over the item it was over before the update, where that stands now; gone with it.</summary>
    private void Retip(string? was)
    {
        if (!tipShown || tipOver is not { } old)
        {
            return;
        }
        Hit? now = was is null ? old : keyHits.TryGetValue(was, out var found) ? found : null;
        if (now is not { } hit || layout.ItemRect(hit) is not { IsEmpty: false } item)
        {
            HideTip();
            return;
        }
        tipOver = hit;
        Put(tip, layout.TooltipAnchor(Within(item, tipPart), tipSize));
    }

    /// <summary>
    /// Every brush the scene colours from the tokens, set again: the scene's own, every swatch (each
    /// read from its part's tokens, §311), the search boxes' plates and the highlight. Nothing is made.
    /// </summary>
    private void Dye()
    {
        sliverFill.Color = tokens.Running;
        outline.Color = tokens.Hover;
        keyOuterInk.Color = tokens.FocusOuter;
        keyInnerInk.Color = tokens.FocusInner;
        running.Color = tokens.Running;
        active.Color = tokens.Active;
        attention.Color = tokens.Attention;
        activeBack.Color = tokens.ActiveBack ?? default;
        attentionBack.Color = tokens.AttentionBack ?? default;
        activeStroke.Color = tokens.ActiveStroke ?? default;
        activeLine.Color = tokens.ActiveLine ?? default;
        tipFill.Color = tokens.TipFill;
        tipStroke.Color = tokens.TipStroke;
        dragFill?.Color = tokens.ZoneTargetFill;
        dragStroke?.Color = tokens.ZoneTargetStroke;
        edit?.Dye(tokens);
        foreach (var swatch in swatches)
        {
            Dye(swatch);
        }
        foreach (var element in elements.Values)
        {
            foreach (var swatch in element.Swatches)
            {
                Dye(swatch);
            }
        }
        // search: its plate takes the card's colours too (PlateInks, §325).
        foreach (var (part, plate) in searchPlates)
        {
            DressPlate(part, plate, animate: false);
        }
        if (lit is { } hit)
        {
            highlightFill.StopAnimation("Color");
            highlightFill.Color = ShadeOf(hit, litPressed);
        }
    }

    private void Dye(Swatched swatch)
    {
        var color = swatch.Pick(tokens);
        if (swatch.Brush.Color != color)
        {
            swatch.Brush.Color = color;
        }
    }

    // --- The panel ---------------------------------------------------------------------------------

    /// <summary>
    /// The bar's panel (<see cref="Panel"/>), kept while its kind is: its material, floating or not,
    /// whether it casts a shadow. Another size is set in place; another material is a panel made again.
    /// </summary>
    private void ComposePanel()
    {
        var material = tokens.Material;
        var floats = layout.Floating;
        var hairline = look.HighContrast ? tokens.BorderWidth : layout.Stroke;
        var casts = floats && material is not Material.Clear && !look.HighContrast;
        var sig = new Sig("panel", material, floats, casts, floats ? layout.PanelRadius : 0f, floats ? hairline : 0f,
            casts ? layout.PanelShadowBlur : 0f, casts ? layout.PanelShadowDrop : 0f, casts && look.Dark, look.Liquid);
        var view = Keep("panel", panels, sig, () => Panel(compositor), PlacePanel, glides: false);
        panelView = view;
        floating = view.Floating;
        floatingMaker = view.Maker;
        if (view.Maker is { } maker)
        {
            maker.Edge.Color = tokens.Border;
        }
    }

    // --- Cards ---------------------------------------------------------------------------------------

    /// <summary>
    /// §512: every widget's own card — a widget's, a folder's button's, a Windows button's, and §602 every app button's, a pin's
    /// as an open app's, and "…"'s — in its own rectangle (<see cref="Layout.Plates"/>), on every kind of bar: its background,
    /// its outline and its pattern as its look resolves them (the Appearance's Taskbar widgets, <see cref="tokens"/>); its
    /// pattern a sheet of its own (§515: no groups on the bar).
    /// Where no background is set, on a bar of cards, its plate at rest (§506). In high contrast, Windows' card on a bar of
    /// cards. Which parts wear a card filled, for edit mode's wash (<see cref="ComposeEdit"/>).
    /// </summary>
    private bool[] ComposeCards()
    {
        // No line is drawn between two widgets (§511: nor a separator, which is gone). The bar used to add
        // its own between every pair of neighbours on a connected bar, and the person asked for exactly the
        // opposite.
        var carded = new bool[layout.Specs.Count];
        // §704: the cards, gathered first: Liquid glass joins the filled ones that meet into one glass shape (LiquidGlass.Join).
        var pending = new List<(int Part, string Key, RectF Plate, bool Fill, bool Stroke)>();
        for (var i = 0; i < layout.Parts.Count && i < carded.Length; i++)
        {
            // §302: a bare corner at rest draws nothing (PartSpec.Bare).
            if (layout.Specs[i].Bare && !layout.Editing)
            {
                continue;
            }
            // search: a search box is its part's card (Layout.SearchBoxed), dressed in its colours (PlateInks); a card under
            // it showed round it as a faint second edge.
            if (layout.SearchBoxed(i))
            {
                carded[i] = true;
                continue;
            }
            var mine = tokens;
            // High contrast's tokens have Windows' card everywhere: drawn on a bar of cards alone. Otherwise a card is what
            // somebody set (the automatic tokens have none).
            var (fill, stroke) = look.HighContrast ? (layout.Cards, layout.Cards) : (mine.CardFill is not null, mine.CardStroke is not null);
            var pattern = Chosen.CardPattern;
            // §602: each of its cards — an app button's part one a button — in its own rectangle (Layout.Plates).
            var plates = layout.Plates(i);
            for (var n = 0; n < plates.Count; n++)
            {
                var key = n == 0 ? layout.Parts[i].Key : $"{layout.Parts[i].Key}#{n}";
                // §506: where no background is set, its plate at rest.
                if (layout.Cards && !fill && !look.HighContrast && !layout.Editing)
                {
                    RestPlate(i, key, plates[n]);
                }
                if (!fill && !stroke && pattern is null)
                {
                    continue;
                }
                pending.Add((i, $"card:{key}", plates[n], fill, stroke));
                // edit: a card with no fill (none chosen, §246; its own None, §311) leaves the part to the edit wash.
                carded[i] = fill && mine.CardFill is { A: > 0 };
            }
        }
        var joined = look.Liquid is { } glass
            ? LiquidGlass.Join([.. pending.Select(card => card.Fill ? new GlassBox(card.Plate.X, card.Plate.Y, card.Plate.Right, card.Plate.Bottom) : default)],
                hairline: MathF.Max(1f, MathF.Round(glass.DividerDip * layout.Scale)), inset: Px(glass.DividerInsetDip), touch: 0.5)
            : null;
        for (var at = 0; at < pending.Count; at++)
        {
            var card = pending[at];
            Card(card.Part, card.Key, card.Plate, card.Fill, card.Stroke, layout.PlateRadius, joined?[at]);
        }
        return carded;
    }

    /// <summary>
    /// §506: a plate at rest of part <paramref name="part"/> (<see cref="RestPlateAlpha"/>) at <paramref name="rect"/>, kept as
    /// <paramref name="name"/> — §602 an app button's part one a button: its own all the way round, where the
    /// highlight lights the whole part, in its corners, so lit it is this plate drawn stronger (<see cref="LightPlates"/>). Drawn as
    /// the highlight is, over the bar, so no hole is cut in the bar for it (§464) and the bar's pattern runs on under it.
    /// </summary>
    private void RestPlate(int part, string name, RectF rect)
    {
        var key = $"plate:{name}";
        var c = compositor;
        var plate = Keep(key, Into(part, Layer.Back), new Sig("plate"),
            () => new Shaped(new Rounded(c, sink), Own(c.CreateColorBrush(default))),
            made => PlaceCard(made.Shape, rect, made.Fill, null, layout.PlateRadius),
            glides: false);
        GlideCard(key, plate, rect);
        if (!restPlates.TryGetValue(part, out var own))
        {
            restPlates[part] = own = [];
        }
        own.Add(plate);
    }

    // --- Islands -------------------------------------------------------------------------------------

    /// <summary>
    /// Islands: per cell a clipped container holding the layers the island keeps redrawing, some of them
    /// gliding on the island's own animation. Cells get the highlight's height, which is what the dock
    /// measured them with. Kept while the island hands out the same surfaces, which it redraws in place.
    /// </summary>
    private void ComposeIslands()
    {
        if (content.Islands is not { } islands)
        {
            return;
        }
        for (var partIndex = 0; partIndex < layout.Parts.Count; partIndex++)
        {
            var part = layout.Parts[partIndex];
            if (!part.Kind.IsIsland() || !islands.TryGetValue(part.Key, out var island))
            {
                continue;
            }
            // Each part's own style: it carries the width chosen for that
            // widget, which is what the layout measured it with.
            // §405: in the look of its own, where its widget has one: its text, its accent.
            var style = layout.StyleFor(partIndex, look);
            for (var index = 0; index < part.Count; index++)
            {
                var hit = Hit.Cell(partIndex, index);
                if (layout.ItemRect(hit) is not { IsEmpty: false } rect)
                {
                    continue;
                }
                var cellLayers = island.Layers(index, style);
                var drawnFrom = new List<object?> { "cell", rect.Size };
                foreach (var layer in cellLayers)
                {
                    drawnFrom.AddRange([layer.Surface, layer.Offset, layer.Size, layer.Scroll]);
                }
                var key = $"island:{part.Key}:{index}";
                var cell = Keep(key, Into(partIndex, Layer.Front), new Sig([.. drawnFrom]),
                    () => IslandCell(compositor, cellLayers, rect.Size),
                    made => Put(made, rect));
                Pressable(hit, cell, key);
            }
        }
    }

    // --- Windows' buttons ----------------------------------------------------------------------------

    /// <summary>
    /// Start, search, Task view and Show desktop, every one the bar holds, then Windows' buttons that came
    /// after those — the widgets board, the pen menu, the touch keyboard, the virtual touchpad — and the
    /// shortcuts to Windows' apps, each its kind's glyph in its button's well. Anything the layout had no
    /// room for has an empty rectangle and is left out.
    /// </summary>
    private void ComposeButtons()
    {
        // Two Start buttons on one bar are two buttons, each drawn and each opening Start (the person,
        // 2026-10-07: "a second start menu button on the same taskbar does not show"). The first is
        // known by the kind's name, the others by their own key too.
        var buttonKinds = new HashSet<BarKind>();
        for (var partIndex = 0; partIndex < layout.Parts.Count; partIndex++)
        {
            var part = layout.Parts[partIndex];
            var name = part.Kind switch
            {
                BarKind.Start => "start",
                BarKind.ShowDesktop => "showdesktop",
                BarKind.Search => "search",
                BarKind.TaskView => "taskview",
                _ => null,
            };
            if (name is null || !layout.Placed(partIndex))
            {
                continue;
            }
            var key = buttonKinds.Add(part.Kind) ? name : $"{name}:{part.Key}";
            switch (part.Kind)
            {
                case BarKind.Start:
                    StartLogo(Hit.Start.In(partIndex), key);
                    break;
                // \§302: Windows 11's bare corner draws nothing at rest; its room is lit under the
                // pointer as a button's, and its icon greyed while the bar is arranged.
                case BarKind.ShowDesktop when layout.Specs[partIndex].Bare && !layout.Editing:
                    Known(Hit.ShowDesktop.In(partIndex), key);
                    break;
                case BarKind.ShowDesktop:
                    // One of the taskbar's own icons, not an app's: the status icons' size.
                    GlyphButton(Hit.ShowDesktop.In(partIndex), "", key, FaceGlyph(layout),
                        layout.Specs[partIndex].Bare ? ColorOf(new IslandInk(look).Disabled) : null);
                    break;
                case BarKind.Search when layout.Boxed(partIndex):
                    SearchBox(Hit.Search.In(partIndex), key);
                    break;
                case BarKind.Search:
                    GlyphButton(Hit.Search.In(partIndex), SearchGlyph, key);
                    break;
                default:
                    GlyphButton(Hit.TaskView.In(partIndex), TaskViewGlyph, key);
                    break;
            }
        }
        // One that shows nothing on this PC has only edit mode's placeholder, and a shortcut whose app is
        // not installed is faint with a warning badge, as a broken pin is.
        for (var partIndex = 0; partIndex < layout.Parts.Count; partIndex++)
        {
            var part = layout.Parts[partIndex];
            if (!part.Kind.IsWindowsButton() || layout.Empty(partIndex))
            {
                continue;
            }
            var hit = Hit.WindowsButton(part.Kind).In(partIndex);
            var key = $"windows:{part.Key}";
            // §497: at a face's icon size, as the faces beside it draw theirs.
            var glyph = FaceGlyph(layout);
            if (GlyphButton(hit, part.Kind.Glyph(), key, glyph) is not { } sprite)
            {
                continue;
            }
            var missing = WindowsButtons.Missing(part.Kind) is not null;
            sprite.Opacity = missing ? BrokenOpacity : 1f;
            if (missing)
            {
                var box = layout.IconBox(hit, glyph);
                var side = MathF.Max(Px(10f), Round(box.Height * 0.5f));
                GlyphSprite($"{key}/missing", Into(partIndex, Layer.Front),
                    new RectF(box.Right - side * 0.75f, box.Bottom - side * 0.75f, side, side), WarningGlyph, side, tokens.Attention);
            }
        }
    }

    // --- App buttons ---------------------------------------------------------------------------------

    /// <summary>
    /// Apps: icons drawn at exactly the size they show at, the focused app's background, and the running
    /// indicator on the edge side; a window's own button says its title; "…" for the buttons the bar has
    /// no room for. Each button known by its part and its app, so a button that moves glides.
    /// </summary>
    private void ComposeTiles()
    {
        var seen = new Dictionary<string, int>();
        for (var index = 0; index < content.Tiles.Count; index++)
        {
            var hit = Hit.Tile(index);
            // Each tile in its own part's containers: a pin is a part of its own, the open apps are the Apps widget's.
            var tilePart = layout.PartOf(hit);
            var rect = layout.ItemRect(hit);
            var placed = !rect.IsEmpty;
            var item = content.Tiles[index].Item;
            // Counted in its own part, so the open apps a bar holds twice glide each in its own widget.
            var stem = $"tile:{(tilePart >= 0 ? layout.Specs[tilePart].Key : "")}:{item.Target}";
            var twin = seen.GetValueOrDefault(stem);
            seen[stem] = twin + 1;
            var key = $"{stem}#{twin}";

            var back = Keep(key + "/back", Into(tilePart, Layer.Back), new Sig("back", rect.Size),
                () =>
                {
                    var plate = new Back(compositor, sink, rect);
                    plate.Visual.Opacity = 0f;
                    return plate;
                },
                plate =>
                {
                    plate.Visual.Offset = new Vector3(rect.X, rect.Y, 0f);
                    plate.Visual.IsVisible = placed;
                });
            backs.Add(back);
            marks.Add(default);

            var iconBox = layout.IconBox(hit, layout.Icon);
            object picture;
            Func<ICompositionSurface> drawPicture;
            if (content.TileSurfaces is { } given && given.TryGetValue(index, out var own))
            {
                (picture, drawPicture) = (own, () => own);
            }
            else if (item.PictureIsGlyph)
            {
                // A pin drawn with a glyph of its own.
                var (glyph, px, ink, width) = (item.Picture!, Round(layout.Icon * 0.8f), tokens.Text, layout.Icon);
                (picture, drawPicture) = (("glyph", glyph, px, ink, width), () => Drew(gfx.Glyph(glyph, px, Ink(ink), width)));
            }
            else
            {
                // The target's icon, from the icons the bars share (Icons): asked for at every update, so it
                // is not swept while it shows.
                var surface = icons.Get(gfx, item, layout.Icon);
                (picture, drawPicture) = (surface, () => surface);
            }
            var sprite = Keep(key, Into(tilePart, Layer.Front), new Sig("tile", picture),
                () =>
                {
                    var icon = Own(compositor.CreateSpriteVisual());
                    var brush = Own(compositor.CreateSurfaceBrush(drawPicture()));
                    brush.Stretch = CompositionStretch.Uniform;
                    icon.Brush = brush;
                    return icon;
                },
                icon =>
                {
                    Put(icon, iconBox);
                    icon.IsVisible = placed;
                    // A pin this PC can't open is faint, with a warning badge on it.
                    icon.Opacity = item.Broken is null ? 1f : BrokenOpacity;
                });
            tiles.Add(sprite);
            Pressable(hit, sprite, key);
            // app buttons: a window's own button says its title after its icon, cut short where it is
            // long (Layout.TileLabel), in the bar's own text.
            if (placed
                && content.Labels is { } labels
                && index < labels.Count
                && labels[index] is { Length: > 0 } label
                && layout.TileLabel(index) is { IsEmpty: false } labelBox)
            {
                var (labelPx, labelInk) = (layout.LabelPx, tokens.Text);
                Keep(key + "/label", Into(tilePart, Layer.Front), new Sig("label", label, labelPx, labelInk, labelBox.Width),
                    () =>
                    {
                        var (words, wordsBrush) = TextSprite(compositor);
                        var (said, size) = gfx.Text(label, labelPx, Weight.Regular, Ink(labelInk), labelBox.Width);
                        wordsBrush.Surface = Drew(said);
                        return new Labelled(words, size);
                    },
                    words =>
                    {
                        Put(words.Sprite, new RectF(labelBox.X, labelBox.Y + MathF.Floor((labelBox.Height - words.Size.Y) / 2f), words.Size.X, words.Size.Y));
                        words.Sprite.Opacity = item.Broken is null ? 1f : BrokenOpacity;
                    });
            }
            if (item.Broken is not null && placed)
            {
                var side = MathF.Max(Px(10f), Round(layout.Icon * 0.5f));
                GlyphSprite(key + "/broken", Into(tilePart, Layer.Front),
                    new RectF(iconBox.Right - side * 0.75f, iconBox.Bottom - side * 0.75f, side, side), WarningGlyph, side, tokens.Attention);
            }
            // The indicator glides by its own animations as SetMark moves it (Animate).
            var pill = Keep(key + "/pill", Into(tilePart, Layer.Front), new Sig("pill", layout.Pill),
                () => Pill(compositor, layout.Pill),
                dot => dot.IsVisible = placed,
                glides: false);
            pills.Add(pill);
            pillCentres.Add(PillCentre(rect));
            PlaceSignals(rect, iconBox, tilePart, key, placed);
        }

        // app buttons: "…" for the buttons the bar has no room for (Layout.Overflowed), after the
        // last of the ones it shows, a button like them on their part's card.
        if (layout.IconBox(Hit.AppOverflow, layout.SmallIcon) is { IsEmpty: false } moreBox)
        {
            var morePart = layout.PartOf(Hit.AppOverflow);
            var more = GlyphSprite("apps:more", Into(morePart, Layer.Front), moreBox, MoreGlyph, Round(layout.SmallIcon * ChevronShare), tokens.Text);
            Pressable(Hit.AppOverflow, more, "apps:more");
        }
    }

    /// <summary>A label's sprite and the size its words were drawn at.</summary>
    private sealed record Labelled(SpriteVisual Sprite, Vector2 Size) : IShown
    {
        public Visual Visual => Sprite;
    }

    /// <summary>
    /// The indicators made in this update (<paramref name="fresh"/>, by tile) take the animations they
    /// change with from now on, their first state set; with animations off none has any.
    /// </summary>
    private void Animate(bool[] fresh)
    {
        if (!look.Animations)
        {
            foreach (var pill in pills)
            {
                pill.ImplicitAnimations = null;
                PillShape(pill)?.ImplicitAnimations = null;
            }
            foreach (var back in backs)
            {
                back.Visual.ImplicitAnimations = null;
            }
            return;
        }
        // §489: made again for another speed: an implicit animation keeps the time it was made with.
        if (motionFor != look.Motion)
        {
            motion = null;
            motionFor = look.Motion;
        }
        var (move, fit, fade) = motion ??= MakeMotion();
        for (var index = 0; index < pills.Count; index++)
        {
            if (index < fresh.Length && !fresh[index] && pills[index].ImplicitAnimations is not null)
            {
                continue;
            }
            pills[index].ImplicitAnimations = move;
            PillShape(pills[index])?.ImplicitAnimations = fit;
        }
        foreach (var back in backs)
        {
            back.Visual.ImplicitAnimations ??= fade;
        }
        GrowSignals(move, fit);
    }

    /// <summary>The indicators' and the plates' animations: a move's for place and size, a fit's for a shape's size, a fade's.</summary>
    private (ImplicitAnimationCollection Move, ImplicitAnimationCollection Fit, ImplicitAnimationCollection Fade) MakeMotion()
    {
        var c = compositor;
        var move = Own(c.CreateImplicitAnimationCollection());
        move["Size"] = Own(Implicit(c.CreateVector2KeyFrameAnimation(), "Size", decelerate, Time(MotionKind.Move)));
        move["Offset"] = Own(Implicit(c.CreateVector3KeyFrameAnimation(), "Offset", decelerate, Time(MotionKind.Move)));
        var fit = Own(c.CreateImplicitAnimationCollection());
        fit["Size"] = Own(Implicit(c.CreateVector2KeyFrameAnimation(), "Size", decelerate, Time(MotionKind.Move)));
        var fade = Own(c.CreateImplicitAnimationCollection());
        fade["Opacity"] = Own(Implicit(c.CreateScalarKeyFrameAnimation(), "Opacity", linear, Time(MotionKind.Hover)));
        return (move, fit, fade);
    }

    private (ImplicitAnimationCollection Move, ImplicitAnimationCollection Fit, ImplicitAnimationCollection Fade)? motion;

    // §489: the motion the implicit animations were made for (MakeMotion).
    private Motion motionFor;

    /// <summary>Tile <paramref name="index"/>'s indicator and plate set by <paramref name="set"/> where they stand, without their animations.</summary>
    private void Still(int index, Action set)
    {
        var pill = pills[index];
        var shape = PillShape(pill);
        var back = backs[index].Visual;
        var (pillMoves, shapeFits, backFades) = (pill.ImplicitAnimations, shape?.ImplicitAnimations, back.ImplicitAnimations);
        pill.ImplicitAnimations = null;
        shape?.ImplicitAnimations = null;
        back.ImplicitAnimations = null;
        set();
        pill.ImplicitAnimations = pillMoves;
        shape?.ImplicitAnimations = shapeFits;
        back.ImplicitAnimations = backFades;
    }

    // --- The tray ------------------------------------------------------------------------------------

    /// <summary>
    /// The tray: the chevron for hidden icons, then the visible ones, side by side in cells of the icon
    /// and its padding along the bar (Layout.TrayStep), the bar's thickness across it (Layout.TrayCell),
    /// with no gap between them, as Windows 11's tray icons stand; each lit on its own cell under the
    /// pointer. A bar may hold the tray more than once: each is the whole tray, its own widget's icons in
    /// its own order (§347, Content.TrayOf), each with its own chevron where it has one. An icon kept is
    /// painted again in place when its picture is new; one that moved glides from where it is by now.
    /// </summary>
    private void ComposeTray()
    {
        var now = Environment.TickCount64;
        foreach (var trayIndex in layout.TrayParts)
        {
            var trayPart = layout.PartAt(trayIndex);
            var chevronHit = Hit.TrayOverflow.In(trayIndex);
            if (layout.TrayChevronOf(trayIndex) > 0f && layout.IconBox(chevronHit, layout.TrayIconSize) is { IsEmpty: false } chevronBox)
            {
                var key = TrayPrefix + TrayGlideKey(trayIndex, ChevronKey);
                var chevron = GlyphSprite(key, Into(trayIndex, Layer.Front), chevronBox, SystemGlyph.Chevron(layout.Edge), Round(layout.TrayIconSize * ChevronShare), tokens.Text);
                Pressable(chevronHit, chevron, key);
            }
            // tray: Content.TrayOf is already what this tray shows (TrayLayout.Split); filtering it by
            // Windows' own choice again left an icon the person had put on the bar undrawn.
            var shownIcons = content.TrayOf(layout.Specs[trayIndex].Key);
            var count = Math.Min(trayPart.Count, shownIcons.Count);
            // tray: their keys in order, for the gap a drag opens among them (TrayDrag).
            trayKeys[trayIndex] = [.. shownIcons.Take(count).Select(icon => icon.Key)];
            for (var index = 0; index < count; index++)
            {
                if (shownIcons[index].Icon is not { } pixels || layout.TrayIconRect(index, trayIndex) is not { IsEmpty: false } box)
                {
                    continue;
                }
                var glideKey = TrayGlideKey(trayIndex, shownIcons[index].Key);
                var key = TrayPrefix + glideKey;
                var was = elements.TryGetValue(key, out var standing) && standing.Seen != pass && standing.Visual.IsVisible
                    ? TrayNow(glideKey, standing.Visual.Offset, now) ?? standing.Visual.Offset
                    : (Vector3?)null;
                var view = Keep(key, Into(trayIndex, Layer.Front), new Sig("tray", box.Width),
                    () =>
                    {
                        var sprite = Own(compositor.CreateSpriteVisual());
                        var picture = Drew(gfx.Icon(pixels, box.Width));
                        var brush = Own(compositor.CreateSurfaceBrush(picture));
                        brush.Stretch = CompositionStretch.Uniform;
                        sprite.Brush = brush;
                        return new TrayIconView(sprite, picture, pixels);
                    },
                    glides: false);
                // perf: a new picture is painted again into the icon's own surface, nothing made.
                if (!ReferenceEquals(view.Pixels, pixels))
                {
                    gfx.RedrawIcon(view.Surface, pixels);
                    view.Pixels = pixels;
                }
                // tray: home, where the layout has it: a drag's room is the drag's to open again
                // (TrayDragRebuilt); an icon the last layout or a drag moved glides home from where it is
                // by now, and its glide is carried on, so the drag's next move goes on from there.
                var home = new Vector3(box.X, box.Y, 0f);
                trayShifts.Remove(glideKey);
                view.Sprite.StopAnimation("Opacity");
                view.Sprite.Opacity = 1f;
                Put(view.Sprite, box);
                if (glide && Kept(key) && was is { } from)
                {
                    CarryTray(glideKey, from, home, now);
                    Slide(view.Sprite, from, home, true);
                }
                else
                {
                    view.Sprite.StopAnimation("Offset");
                    trayGlides.Remove(glideKey);
                }
                Pressable(Hit.Tray(index).In(trayIndex), view.Sprite, key);
                trayPictures[(trayIndex, index)] = view;
            }
        }
    }

    /// <summary>A tray icon drawn: its sprite, its own surface and the picture painted in it (<see cref="SetTray"/>).</summary>
    private sealed class TrayIconView(SpriteVisual sprite, CompositionDrawingSurface surface, Pixels pixels) : IShown
    {
        public SpriteVisual Sprite { get; } = sprite;

        public CompositionDrawingSurface Surface { get; } = surface;

        public Pixels Pixels { get; set; } = pixels;

        public Visual Visual => Sprite;
    }

    // --- Edit mode -----------------------------------------------------------------------------------

    /// <summary>
    /// Edit mode's marks over the parts as they are: a subtle fill behind a part without a card, and over
    /// each block its outline, a hover fill and the focus rectangle; a part with nothing to show gets its
    /// kind's glyph. A section that holds nothing is an empty block of its own, a carried block's landing
    /// a blue ghost, and every section has its "+". <see cref="Apply"/> decides what shows. Kept from one
    /// update to the next with the parts they mark.
    /// </summary>
    private void ComposeEdit(bool[] carded)
    {
        var brushes = edit!;
        var c = compositor;
        var radius = EditRadius;
        var hair = layout.Stroke;
        ComposeEmpty();
        ComposeAdds();
        for (var part = 0; part < parts.Length; part++)
        {
            // What the pointer can take hold of, which is what lights up: the block.
            var block = layout.BlockRect(part);
            if (block.IsEmpty)
            {
                continue;
            }
            var spec = layout.Specs[part];
            var key = spec.Key;
            var visuals = Part(part);
            visuals.Block = block;
            if (!carded[part])
            {
                // Where the widgets wear cards, a part with none (an empty tray, no status icons) is
                // washed as a card is, so it reads as one more widget beside them: the edit wash is
                // a darkening in the light look, which made it the one grey card among white ones.
                var washed = layout.Cards && tokens.CardFill is { A: > 0 };
                var at = part;
                visuals.Fill = Keep($"edit:{key}:fill", Into(part, Layer.Under), new Sig("fill", washed),
                    () => new Shaped(new Rounded(c, sink), washed ? Swatch(t => t.CardFill ?? default) : null),
                    fill => fill.Shape.Place(block, radius, fill.Fill ?? brushes.Fill, null, 0f)).Shape;
            }
            if (layout.Empty(part) && spec.Kind.Glyph() is { Length: > 0 } code)
            {
                // A part with nothing to show says what it is.
                var px = MathF.Min(layout.SmallIcon, MathF.Floor(MathF.Min(block.Width, block.Height) * 0.6f));
                GlyphSprite($"edit:{key}:glyph", Into(part, Layer.Front), Centred(block, new Vector2(px, px)), code, px, tokens.TertiaryText);
            }
            // The wash and the block's hairline over what stands on the block, as ever, and under its
            // card's outline (§325): a chosen outline keeps its colour, lit or not; the hairline still
            // shows where the card has none.
            var lit = visuals.Lit;
            visuals.Hover = Keep($"edit:{key}:hover", Into(part, Layer.RimUnder), new Sig("hover"),
                () =>
                {
                    var hover = new Rounded(c, sink);
                    hover.Visual.Opacity = lit ? 1f : 0f;
                    return hover;
                },
                hover => hover.Place(block, radius, brushes.Hover, null, 0f));
            visuals.Outline = Keep($"edit:{key}:outline", Into(part, Layer.RimUnder), new Sig("outline"),
                () => new Rounded(c, sink),
                line => line.Place(block, radius, null, brushes.Outline, hair));
            // edit: the block last clicked, selected: the accent's ring, 2 px on the block's edge
            // over its outline, as Windows 11 rings a selected item. Under the focus rectangle,
            // which goes over it where the keys are on the same block.
            visuals.Selected = Keep($"edit:{key}:selected", Into(part, Layer.Top), new Sig("selected"),
                () =>
                {
                    var ring = new Rounded(c, sink);
                    ring.Visual.IsVisible = false;
                    return ring;
                },
                ring => ring.Place(block, radius, null, brushes.TargetStroke, EditLook.Ring(hair), 0f));
            // edit: on edit mode's clipboard, the marching ants in the ring's place (MarchingAnts.cs).
            visuals.Ants = Keep($"edit:{key}:ants", Into(part, Layer.Top), new Sig("ants", tokens.ZoneTargetStroke, look.Animations),
                () => new MarchingAnts(c, tokens.ZoneTargetStroke, look.Animations, value =>
                {
                    sink.Add(value);
                    return value;
                }),
                glides: false);
            // The Windows focus rectangle: 2 px outside, 1 px inside it; an open folder's line's the same (EditLook.cs).
            visuals.FocusOuter = Keep($"edit:{key}:focus", Into(part, Layer.Top), new Sig("focus"),
                () =>
                {
                    var ring = new Rounded(c, sink);
                    ring.Visual.IsVisible = false;
                    return ring;
                },
                ring => ring.Place(block, radius, null, brushes.FocusOuter, EditLook.Ring(hair)));
            visuals.FocusInner = Keep($"edit:{key}:focus-inner", Into(part, Layer.Top), new Sig("focus-inner"),
                () =>
                {
                    var ring = new Rounded(c, sink);
                    ring.Visual.IsVisible = false;
                    return ring;
                },
                ring => ring.Place(block, radius, null, brushes.FocusInner, hair, EditLook.Ring(hair)));
        }
        // edit: the ghost of a carried block where it would land, in the target's wash and ring, under
        // the blocks' marks and over the parts.
        landingGhost = Keep("edit:ghost", zones, new Sig("ghost"),
            () =>
            {
                var holder = Own(c.CreateShapeVisual());
                holder.IsVisible = false;
                return new Ghosts { Holder = holder, Bag = sink };
            },
            glides: false);
        // edit: the lifted shadow, a folder's line's too (EditLook.cs).
        var shadowFor = (radius, Px(EditLook.LiftShadowBlurDip), Px(EditLook.LiftShadowOffsetDip), EditLook.LiftShadow(tokens.TipShadow));
        if (tokens.TipShadow <= 0f)
        {
            DropLiftShadow();
        }
        else if (liftShadow is null || liftShadowMade != shadowFor)
        {
            DropLiftShadow();
            liftShadow = new Shadow(c, liftShadowBag, shadowFor.radius, shadowFor.Item2, shadowFor.Item3, shadowFor.Item4);
            liftShadowMade = shadowFor;
        }
    }

    private void DropLiftShadow()
    {
        liftShadow?.Visual.Parent?.Children.Remove(liftShadow.Visual);
        liftShadow = null;
        Close(liftShadowBag);
    }

    /// <summary>
    /// edit: the empty blocks of the sections that hold nothing, one shape visual over the bar's whole
    /// strip, a rounded shape in it per room (<see cref="ShowEmpty"/>). In the zones layer, under every part.
    /// </summary>
    private void ComposeEmpty()
    {
        var c = compositor;
        empties = Keep("edit:empties", zones, new Sig("empties"),
            () => new EmptyBlocks { Holder = Own(c.CreateShapeVisual()), Bag = sink },
            shown => shown.Holder.Size = new Vector2(layout.Width, layout.Height),
            glides: false);
    }

    /// <summary>
    /// edit: a button for every "+" of the bar (<see cref="Layout.Adds"/>), the round accent button of a
    /// folder's columns, drawn by the same hand (FolderFlyout.PaintAdd): the accent, lighter under the
    /// pointer, darker pressed or while its add list is open, Windows' Add glyph on it. In a layer of their
    /// own over the zones, in room no part stands in, each known by its section and its end: kept, and
    /// gliding with the blocks as they move, never faded in again by an update (the person, 2026-10-25: "the add buttons
    /// flicker"); §612 fading in and out with edit mode's dress (AnimateEdit, FadeOut). Drawn only: hit testing is the layout's (<see cref="HitKind.Add"/>).
    /// </summary>
    private unsafe void ComposeAdds()
    {
        var all = layout.Adds();
        if (all.Count == 0)
        {
            addButtons = null;
            return;
        }
        var side = all[0].Rect.Width;
        var theme = FlyoutTheme.For(look);
        var looksFor = new Sig("adds", side, theme.Accent, theme.AccentHover, theme.AccentPressed, theme.TextOnAccent);
        if (addButtons is not { } buttons || !buttons.For.Equals(looksFor))
        {
            // Another size or another accent: the three looks are drawn again, once; the buttons keep
            // standing and wear them (ShowAdds). The old looks go once this update is done.
            if (addButtons is { } old)
            {
                retiredLooks = old;
            }
            var bag = new List<IDisposable>();
            var c = compositor;
            CompositionSurfaceBrush Painted(FolderFlyout.AddLook state)
            {
                var surface = gfx.Draw(side, side, dc =>
                    FolderFlyout.PaintAdd(dc, gfx, new TerraFX.Interop.DirectX.D2D_RECT_F { left = 0, top = 0, right = side, bottom = side }, theme, state, FolderAddKind.Add));
                drawn++;
                bag.Add(surface);
                var brush = c.CreateSurfaceBrush(surface);
                bag.Add(brush);
                return brush;
            }
            buttons = addButtons = new AddButtons
            {
                For = looksFor,
                Rest = Painted(FolderFlyout.AddLook.Rest),
                Hover = Painted(FolderFlyout.AddLook.Hover),
                Pressed = Painted(FolderFlyout.AddLook.Pressed),
                Bag = bag,
            };
        }
        buttons.Visuals.Clear();
        foreach (var add in all)
        {
            var sprite = Keep($"add:{add.Section}:{add.End}", adds, new Sig("add"),
                () =>
                {
                    var made = Own(compositor.CreateSpriteVisual());
                    made.Brush = buttons.Rest;
                    return made;
                },
                made =>
                {
                    made.Offset = new Vector3(add.Rect.X, add.Rect.Y, 0f);
                    made.Size = add.Rect.Size;
                });
            buttons.Visuals.Add(sprite);
        }
    }

    /// <summary>The "+"'s looks an update replaced, let go of once it is done: the buttons wear the new ones by then.</summary>
    private AddButtons? retiredLooks;

    /// <summary>
    /// At rest: nothing carried over the bar, every part at its place, a floating bar's docks where its
    /// sections are — a drag going on shows itself again on the next move, as it always has.
    /// </summary>
    private void Rest()
    {
        foreach (var mark in (ReadOnlySpan<Rounded?>)[dragTarget, dragLine, dragLanding])
        {
            if (mark is not null)
            {
                mark.Visual.IsVisible = false;
            }
        }
        for (var part = 0; part < parts.Length; part++)
        {
            Shift(part, 0f, glide);
        }
        ShowPanels(layout.SectionPanels(), glide);
    }

    /// <summary>
    /// edit: coming into edit mode the marks fade in, §612 the "+" with them (their room opening as the blocks glide aside); a
    /// block added while editing fades in on its own (one the bar did not have before, <paramref name="before"/>).
    /// </summary>
    private void AnimateEdit(bool wasEditing, Layout? before)
    {
        if (!wasEditing)
        {
            FadeIn(blocks);
            FadeIn(zones);
            FadeIn(adds);
            foreach (var visuals in parts)
            {
                // It sits in the backgrounds layer rather than in blocks, so it does not come in
                // with it: a part's wash.
                if (visuals?.Fill is { } fill)
                {
                    FadeIn(fill.Visual);
                }
                // And its hairline, under its card's outline in the rims (§325).
                if (visuals?.Outline is { } line)
                {
                    FadeIn(line.Visual);
                }
            }
            return;
        }
        if (!glide || before is null)
        {
            return;
        }
        for (var part = 0; part < parts.Length; part++)
        {
            // §593: the add page's ghost fades in to its own faintness (Apply).
            if (parts[part] is { Ghosted: false } visuals && before.PartIndex(layout.Specs[part].Key) < 0)
            {
                foreach (var (container, _) in LayersOf(visuals))
                {
                    FadeIn(container);
                }
            }
        }
    }

    // --- §612: edit mode's dress taken off ------------------------------------------------------------

    /// <summary>§612: this update takes edit mode's dress off with motion on: its marks fade out (<see cref="FadeOut"/>).</summary>
    private bool undressing;

    /// <summary>§612: what fades out, and when each is let go of (<see cref="Environment.TickCount64"/>'s ms).</summary>
    private readonly List<(long Until, Action Gone)> fading = [];

    /// <summary>tests: what fades out now, edit mode's dress taken off (§612): its marks, the "+", their brushes.</summary>
    internal int Fading => fading.Count;

    /// <summary>§612: an element of edit mode's dress, by its key: a block's marks, the empty rooms, the ghost, a "+".</summary>
    private static bool IsEditMark(string key) => key.StartsWith("edit:", StringComparison.Ordinal) || key.StartsWith("add:", StringComparison.Ordinal);

    /// <summary>
    /// §612: <paramref name="element"/>, gone with edit mode's dress, fades out where it stands (a large surface's Hide, as
    /// the dim's), let go of once it has (<see cref="LetFadedGo"/>).
    /// </summary>
    private void FadeOut(Element element)
    {
        var visual = element.Visual;
        visual.StopAnimation("Offset");
        var hide = compositor.CreateScalarKeyFrameAnimation();
        hide.InsertKeyFrame(0f, visual.Opacity);
        hide.InsertKeyFrame(1f, 0f, accelerate);
        hide.Duration = Time(MotionKind.Hide);
        visual.StartAnimation("Opacity", hide);
        hide.Dispose();
        Faded(true, () =>
        {
            // A part only edit mode showed (a face with nothing to show) went with its containers meanwhile, and they were
            // closed with it: what faded in them is gone already, all but what was made for it.
            try
            {
                Retire(element);
            }
            catch (ObjectDisposedException)
            {
                Close(element.Bag);
            }
        });
    }

    /// <summary>§612: <paramref name="gone"/> once what fades out now has (<paramref name="later"/>), else at once.</summary>
    private void Faded(bool later, Action gone)
    {
        if (!later)
        {
            gone();
            return;
        }
        fading.Add((Environment.TickCount64 + (long)Math.Ceiling(Time(MotionKind.Hide).TotalMilliseconds) + FadeGraceMs, gone));
    }

    /// <summary>§612: past a fade out, before what faded is let go of: a frame for its last step to be drawn.</summary>
    private const int FadeGraceMs = 17;

    /// <summary>§612: what has faded out let go of — everything, where <paramref name="all"/> (the scene closing).</summary>
    private void LetFadedGo(bool all = false)
    {
        var now = Environment.TickCount64;
        for (var at = fading.Count - 1; at >= 0; at--)
        {
            if (all || fading[at].Until <= now)
            {
                var gone = fading[at].Gone;
                fading.RemoveAt(at);
                gone();
            }
        }
    }

    private void FadeIn(Visual visual)
    {
        fadesIn++;
        var appear = compositor.CreateScalarKeyFrameAnimation();
        appear.InsertKeyFrame(0f, 0f);
        appear.InsertKeyFrame(1f, 1f, decelerate);
        appear.Duration = Time(MotionKind.Swap);
        visual.StartAnimation("Opacity", appear);
        appear.Dispose();
    }
}
