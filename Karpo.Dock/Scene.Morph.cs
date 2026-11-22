// §637: a bar's scene in a morph (BarMorph.cs): what it shows as a picture, and its side of the plan — the new scene coming in
// from the old bar's picture (MorphIn), the old one going to the new bar's (MorphOut) under it, in the same window. Every
// animation is one of the plan's steps over the one time (MotionKind.Morph); what the new scene animates is put where it ends
// once the morph is over (MorphOver), the old scene let go of whole.

using System.Numerics;
using Karpo.Core;
using Windows.UI.Composition;

namespace Karpo.Dock;

public sealed partial class Scene
{
    /// <summary>§637: what a running morph animated in this scene, with where each ends: put there once it is over (<see cref="MorphOver"/>).</summary>
    private readonly List<(Visual Visual, Vector3 Offset, float Opacity)> morphed = [];

    /// <summary>§637: the floating panels' shapes a morph reshaped, with where each ends.</summary>
    private readonly List<(CompositionRoundedRectangleGeometry Shape, Vector2 Offset, Vector2 Size, Vector2 Radius)> morphedShapes = [];

    /// <summary>§637: what a morph made — its easings, its animations, a strip's clip —, closed once it is over.</summary>
    private readonly List<IDisposable> morphMade = [];

    /// <summary>§637: the ground's cut round the cards (§464), taken off for the morph — the cards move, their holes would stay — and put back once it is over.</summary>
    private readonly List<(ContainerVisual Ground, CompositionClip? Cut)> uncut = [];

    /// <summary>§637: the strip's clip a morph put on the panel, taken off once it is over.</summary>
    private ContainerVisual? morphClipped;

    /// <summary>§637: the layout the scene was last laid out as; null before it was (what a morph asks: is the next another bar, <see cref="BarMorph.Between"/>).</summary>
    internal Layout? Laid => updated ? layout : null;

    /// <summary>§637: whether the bar is in sight as its last update or <see cref="Present"/> put it: only a bar in sight morphs, goes or comes by the motion.</summary>
    internal bool Presented => Ready && presented;

    /// <summary>tests (§637): whether a morph is running in this scene.</summary>
    internal bool Morphing => morphed.Count > 0 || morphedShapes.Count > 0 || morphClipped is not null;

    /// <summary>
    /// §637: what the bar shows, as a morph sees it, in its window's px: every widget placed on it by its key and kind, at its
    /// block (one that draws nothing at rest, a bare corner, too: it is matched all the same); its grounds — a floating bar's docks by section, else its strip. Empty before it was laid out.
    /// </summary>
    internal BarPicture Picture()
    {
        if (!Ready)
        {
            return BarPicture.Empty;
        }
        var at = new Vector2(origin.X, origin.Y);
        var shown = new List<MorphPart>();
        for (var part = 0; part < layout.Specs.Count && part < parts.Length; part++)
        {
            if (layout.Placed(part) && layout.BlockRect(part) is { IsEmpty: false } block)
            {
                shown.Add(new MorphPart(layout.Specs[part].Key, layout.Specs[part].Kind, BarMorph.Moved(block, at), part));
            }
        }
        var grounds = new List<MorphGround>();
        if (layout.Floating)
        {
            var docks = layout.SectionPanels();
            for (var section = 0; section < docks.Count; section++)
            {
                if (!docks[section].IsEmpty)
                {
                    grounds.Add(new MorphGround(section, BarMorph.Moved(docks[section], at), layout.PanelRadius));
                }
            }
        }
        else if (!layout.PanelRect.IsEmpty)
        {
            grounds.Add(new MorphGround(-1, BarMorph.Moved(layout.PanelRect, at), layout.Radius));
        }
        return new BarPicture(shown, grounds);
    }

    /// <summary>
    /// §637: the new bar comes in over the old (<paramref name="from"/>, its picture in this window's px): each widget on both
    /// glides from where the old one stood, at its size, fading in over it; each only here fades in from a little smaller once
    /// the glides are under way; the ground grows from the old one's (<see cref="MorphPlan.GroundsFrom"/>), fading in over it.
    /// </summary>
    internal void MorphIn(MorphPlan plan, BarPicture from, BarPicture to, Motion motion)
    {
        if (!Ready || !motion.On)
        {
            return;
        }
        var at = new Vector2(origin.X, origin.Y);
        foreach (var (was, now) in plan.Glides)
        {
            var (old, mine) = (BarMorph.Moved(from.Parts[was].Rect, -at), BarMorph.Moved(to.Parts[now].Rect, -at));
            var scale = BarMorph.ScaleFrom(old, mine);
            foreach (var container in MorphContainers(to.Parts[now].Part))
            {
                var stands = container.Offset;
                container.CenterPoint = new Vector3(mine.Center, 0f);
                Morph(container, "Offset", stands + new Vector3(old.Center - mine.Center, 0f), stands, BarMorph.Glide, motion);
                Morph(container, "Scale", new Vector3(scale, scale, 1f), Vector3.One, BarMorph.Glide, motion);
                Morph(container, "Opacity", 0f, container.Opacity, BarMorph.CrossIn, motion);
                morphed.Add((container, stands, container.Opacity));
            }
        }
        foreach (var now in plan.Entering)
        {
            var mine = BarMorph.Moved(to.Parts[now].Rect, -at);
            foreach (var container in MorphContainers(to.Parts[now].Part))
            {
                container.CenterPoint = new Vector3(mine.Center, 0f);
                Morph(container, "Scale", new Vector3(BarMorph.Shrunk, BarMorph.Shrunk, 1f), Vector3.One, BarMorph.Enter, motion);
                Morph(container, "Opacity", 0f, container.Opacity, BarMorph.Enter, motion);
                morphed.Add((container, container.Offset, container.Opacity));
            }
        }
        if (panelView is not { } panel)
        {
            return;
        }
        Uncut(panel);
        for (var ground = 0; ground < to.Grounds.Count && ground < plan.GroundsFrom.Count; ground++)
        {
            MorphGround(panel, to.Grounds[ground].Slot, plan.GroundsFrom[ground], to.Grounds[ground], at, motion);
        }
        Morph(panel.Container, "Opacity", 0f, panel.Container.Opacity, BarMorph.GroundIn, motion);
        morphed.Add((panel.Container, panel.Container.Offset, panel.Container.Opacity));
    }

    /// <summary>
    /// §637: the old bar goes under the new (<paramref name="to"/>, its picture in this window's px): each widget on both glides to
    /// where the new one stands, at its size, fading out under it once that is nearly there; each only here fades out a little
    /// smaller; the ground goes to the new one's (<see cref="MorphPlan.GroundsTo"/>), gone by the end. Nothing of it is put back:
    /// the scene is let go of once the morph is over.
    /// </summary>
    internal void MorphOut(MorphPlan plan, BarPicture from, BarPicture to, Motion motion)
    {
        if (!Ready)
        {
            return;
        }
        // What the pointer lit, and the tooltip, are the new bar's from now on.
        Highlight(null, false);
        HideTip();
        if (!motion.On)
        {
            return;
        }
        var at = new Vector2(origin.X, origin.Y);
        foreach (var (was, now) in plan.Glides)
        {
            var (mine, next) = (BarMorph.Moved(from.Parts[was].Rect, -at), BarMorph.Moved(to.Parts[now].Rect, -at));
            var scale = 1f / BarMorph.ScaleFrom(mine, next);
            foreach (var container in MorphContainers(from.Parts[was].Part))
            {
                var stands = container.Offset;
                container.CenterPoint = new Vector3(mine.Center, 0f);
                Morph(container, "Offset", stands, stands + new Vector3(next.Center - mine.Center, 0f), BarMorph.Glide, motion);
                Morph(container, "Scale", Vector3.One, new Vector3(scale, scale, 1f), BarMorph.Glide, motion);
                Morph(container, "Opacity", container.Opacity, 0f, BarMorph.CrossOut, motion);
            }
        }
        foreach (var was in plan.Leaving)
        {
            var mine = BarMorph.Moved(from.Parts[was].Rect, -at);
            foreach (var container in MorphContainers(from.Parts[was].Part))
            {
                container.CenterPoint = new Vector3(mine.Center, 0f);
                Morph(container, "Scale", Vector3.One, new Vector3(BarMorph.Shrunk, BarMorph.Shrunk, 1f), BarMorph.Leave, motion);
                Morph(container, "Opacity", container.Opacity, 0f, BarMorph.Leave, motion);
            }
        }
        if (panelView is not { } panel)
        {
            return;
        }
        Uncut(panel);
        for (var ground = 0; ground < from.Grounds.Count && ground < plan.GroundsTo.Count; ground++)
        {
            MorphGround(panel, from.Grounds[ground].Slot, from.Grounds[ground], plan.GroundsTo[ground], at, motion);
        }
        Morph(panel.Container, "Opacity", panel.Container.Opacity, 0f, BarMorph.GroundOut, motion);
    }

    /// <summary>§637: the morph is over: everything it animated stands where it ends, the strip's clip taken off, what it made closed.</summary>
    internal void MorphOver()
    {
        if (disposed)
        {
            return;
        }
        foreach (var (visual, offset, opacity) in morphed)
        {
            visual.StopAnimation("Offset");
            visual.StopAnimation("Scale");
            visual.StopAnimation("Opacity");
            visual.Offset = offset;
            visual.Scale = Vector3.One;
            visual.Opacity = opacity;
        }
        morphed.Clear();
        foreach (var (shape, offset, size, radius) in morphedShapes)
        {
            shape.StopAnimation("Offset");
            shape.StopAnimation("Size");
            shape.StopAnimation("CornerRadius");
            (shape.Offset, shape.Size, shape.CornerRadius) = (offset, size, radius);
        }
        morphedShapes.Clear();
        if (morphClipped is { } clipped)
        {
            clipped.Clip = null;
            morphClipped = null;
        }
        foreach (var (ground, cut) in uncut)
        {
            ground.Clip = cut;
        }
        uncut.Clear();
        Close(morphMade);
    }

    /// <summary>§637: the ground whole under the cards for the morph (§464's holes stay where the cards were, the cards glide off them), its cut kept to be put back.</summary>
    private void Uncut(PanelView panel)
    {
        foreach (var ground in (panel.Floating?.Values.Select(dock => dock.Cut) ?? []).Prepend(panel.Cut))
        {
            if (ground is { Clip: { } cut })
            {
                uncut.Add((ground, cut));
                ground.Clip = null;
            }
        }
    }

    /// <summary>§637: part <paramref name="part"/>'s containers, a layer each, which move it as one; none for a part it does not show.</summary>
    private IEnumerable<ContainerVisual> MorphContainers(int part) =>
        part >= 0 && part < parts.Length && parts[part] is { } visuals ? LayersOf(visuals).Select(layer => layer.Container) : [];

    /// <summary>
    /// §637: one ground from <paramref name="start"/> to <paramref name="end"/> (the window's px; <paramref name="at"/> the layout's
    /// corner in it): a dock's panel (<paramref name="slot"/> its section) by its shapes — its material, its hairline, its shadow —,
    /// the strip by a rounded clip over it, its corners rounding or squaring on the way.
    /// </summary>
    private void MorphGround(PanelView panel, int slot, MorphGround start, MorphGround end, Vector2 at, Motion motion)
    {
        var corner = new Vector2(layout.PanelRect.X, layout.PanelRect.Y) + at;
        var (from, to) = (BarMorph.Moved(start.Rect, -corner), BarMorph.Moved(end.Rect, -corner));
        if (slot >= 0)
        {
            if (panel.Floating?.GetValueOrDefault((BarSection)slot) is not { } dock)
            {
                return;
            }
            var inset = new Vector2(dock.Inset);
            MorphShape(dock.Shape, from, to, start.Radius, end.Radius, Vector2.Zero, motion);
            MorphShape(dock.Ring, from, to, MathF.Max(0f, start.Radius - dock.Inset), MathF.Max(0f, end.Radius - dock.Inset), inset, motion);
            if (dock.Cast is { } cast)
            {
                MorphShape(cast, from, to, start.Radius, end.Radius, Vector2.Zero, motion);
            }
            return;
        }
        // The strip: its whole panel seen through a rounded window that goes from the one to the other.
        var shape = Made(compositor.CreateRoundedRectangleGeometry());
        var clip = Made(compositor.CreateGeometricClip(shape));
        (shape.Offset, shape.Size, shape.CornerRadius) = (from.Position(), from.Size, new Vector2(start.Radius));
        panel.Container.Clip = clip;
        morphClipped = panel.Container;
        Morph(shape, "Offset", from.Position(), to.Position(), BarMorph.Glide, motion);
        Morph(shape, "Size", from.Size, to.Size, BarMorph.Glide, motion);
        Morph(shape, "CornerRadius", new Vector2(start.Radius), new Vector2(end.Radius), BarMorph.Glide, motion);
    }

    /// <summary>§637: a dock panel's shape from <paramref name="from"/> to <paramref name="to"/> (<paramref name="inset"/> in from each), kept to be put where it ends.</summary>
    private void MorphShape(CompositionRoundedRectangleGeometry shape, RectF from, RectF to, float fromRadius, float toRadius, Vector2 inset, Motion motion)
    {
        var (fromOffset, toOffset) = (from.Position() + inset, to.Position() + inset);
        var (fromSize, toSize) = (Vector2.Max(Vector2.Zero, from.Size - (2f * inset)), Vector2.Max(Vector2.Zero, to.Size - (2f * inset)));
        morphedShapes.Add((shape, shape.Offset, shape.Size, shape.CornerRadius));
        Morph(shape, "Offset", fromOffset, toOffset, BarMorph.Glide, motion);
        Morph(shape, "Size", fromSize, toSize, BarMorph.Glide, motion);
        Morph(shape, "CornerRadius", new Vector2(fromRadius), new Vector2(toRadius), BarMorph.Glide, motion);
    }

    /// <summary>§637: <paramref name="what"/> of <paramref name="on"/> from <paramref name="from"/> to <paramref name="to"/> by <paramref name="step"/> of the morph.</summary>
    private void Morph(CompositionObject on, string what, float from, float to, MorphStep step, Motion motion)
    {
        var steps = Made(compositor.CreateScalarKeyFrameAnimation());
        steps.InsertKeyFrame(0f, from);
        if (step.Start > 0f)
        {
            steps.InsertKeyFrame(step.Start, from);
        }
        steps.InsertKeyFrame(step.End, to, Made(MotionEase.Of(compositor, step.Curve)));
        if (step.End < 1f)
        {
            steps.InsertKeyFrame(1f, to);
        }
        steps.Duration = motion.Time(MotionKind.Morph);
        on.StartAnimation(what, steps);
    }

    /// <inheritdoc cref="Morph(CompositionObject, string, float, float, MorphStep, Motion)"/>
    private void Morph(CompositionObject on, string what, Vector2 from, Vector2 to, MorphStep step, Motion motion)
    {
        var steps = Made(compositor.CreateVector2KeyFrameAnimation());
        steps.InsertKeyFrame(0f, from);
        if (step.Start > 0f)
        {
            steps.InsertKeyFrame(step.Start, from);
        }
        steps.InsertKeyFrame(step.End, to, Made(MotionEase.Of(compositor, step.Curve)));
        if (step.End < 1f)
        {
            steps.InsertKeyFrame(1f, to);
        }
        steps.Duration = motion.Time(MotionKind.Morph);
        on.StartAnimation(what, steps);
    }

    /// <inheritdoc cref="Morph(CompositionObject, string, float, float, MorphStep, Motion)"/>
    private void Morph(CompositionObject on, string what, Vector3 from, Vector3 to, MorphStep step, Motion motion)
    {
        var steps = Made(compositor.CreateVector3KeyFrameAnimation());
        steps.InsertKeyFrame(0f, from);
        if (step.Start > 0f)
        {
            steps.InsertKeyFrame(step.Start, from);
        }
        steps.InsertKeyFrame(step.End, to, Made(MotionEase.Of(compositor, step.Curve)));
        if (step.End < 1f)
        {
            steps.InsertKeyFrame(1f, to);
        }
        steps.Duration = motion.Time(MotionKind.Morph);
        on.StartAnimation(what, steps);
    }

    /// <summary>§637: <paramref name="made"/>, closed once the morph is over.</summary>
    private T Made<T>(T made)
        where T : IDisposable
    {
        morphMade.Add(made);
        return made;
    }
}

/// <summary>§637: a rectangle's corner as a vector.</summary>
internal static class RectCorners
{
    public static Vector2 Position(this RectF rect) => new(rect.X, rect.Y);
}
