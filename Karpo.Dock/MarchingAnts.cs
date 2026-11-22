// edit: what is on edit mode's clipboard wears Excel's copied look (the person, 2026-10-12: "the item
// that is currently in the clipboard should get a look like an excel cell does when it is being
// copied. same if you select multiples of course").
//
// Excel runs a dashed border round a copied range and moves its dashes along it, the "marching ants",
// until Escape or the next copy. Here: the accent's dashes, as thick as the selected ring (2 px at
// 100 %), on a faint band of the accent so the gaps read as part of the border over any widget, round
// the block on the bar (Scene) and round the line in a folder (FolderFlyout), in the ring's place.
// The dashes move by the compositor alone: one key-framed StrokeDashOffset, looping, linear — nothing
// is drawn again while they march (Loop, March: the one loop a dashed stroke marches on, a folder spacer's
// stripes' too, SpacerStripes.cs). With Karpo's Animations off they stand still.
//
// Which widgets wear it, and until when, is the dock's (Dock.EditCopy.cs, ShowCopied).

using System.Numerics;
using Windows.UI.Composition;
using Color = Windows.UI.Color;

namespace Karpo.Dock;

/// <summary>A copied widget's marching border: its visual, the shape it runs round, and the dashed stroke that marches.</summary>
internal sealed class MarchingAnts
{
    /// <summary>A dash and the gap after it, in multiples of the stroke's thickness (a stroke's dashes are measured in it).</summary>
    internal const float Dash = 3;


    /// <summary>How strong the band under the dashes is, of the accent.</summary>
    internal const float BandAlpha = 0.3f;

    private readonly CompositionSpriteShape band;
    private readonly CompositionSpriteShape dashes;
    private readonly ScalarKeyFrameAnimation? lap;
    private bool marching;

    /// <param name="keep">What the visuals are kept by, to be closed with them.</param>
    /// <param name="animate">Whether the dashes move (Windows' animations on).</param>
    public MarchingAnts(Compositor c, Color accent, bool animate, Func<IDisposable, IDisposable> keep)
    {
        T Keep<T>(T value)
            where T : IDisposable => (T)keep(value);
        Visual = Keep(c.CreateShapeVisual());
        Box = Keep(c.CreateRoundedRectangleGeometry());
        band = Keep(c.CreateSpriteShape(Box));
        band.StrokeBrush = Keep(c.CreateColorBrush(Color.FromArgb((byte)MathF.Round(255 * BandAlpha), accent.R, accent.G, accent.B)));
        dashes = Keep(c.CreateSpriteShape(Box));
        dashes.StrokeBrush = Keep(c.CreateColorBrush(accent));
        dashes.StrokeDashArray.Add(Dash);
        dashes.StrokeDashArray.Add(Dash);
        dashes.StrokeDashCap = CompositionStrokeCap.Flat;
        Visual.Shapes.Add(band);
        Visual.Shapes.Add(dashes);
        Visual.IsVisible = false;
        if (animate)
        {
            lap = Loop(c, 2 * Dash, 1, keep);
        }
    }

    /// <summary>
    /// The one loop a dashed stroke marches on — the ants', a folder spacer's stripes' (<see cref="SpacerStripes"/>): its
    /// StrokeDashOffset key-framed from 0 to −<paramref name="distance"/> (in the stroke's thickness, as its dashes are),
    /// linear, over <paramref name="laps"/> of the ants' lap (<see cref="Motion.AntsLapMs"/>), forever. The compositor runs
    /// it alone; one loop may run on any number of strokes.
    /// </summary>
    /// <param name="keep">What the loop is kept by, to be closed with it.</param>
    internal static ScalarKeyFrameAnimation Loop(Compositor c, float distance, double laps, Func<IDisposable, IDisposable> keep)
    {
        var loop = (ScalarKeyFrameAnimation)keep(c.CreateScalarKeyFrameAnimation());
        var linear = (CompositionEasingFunction)keep(MotionEase.Straight(c));
        loop.InsertKeyFrame(0, 0, linear);
        loop.InsertKeyFrame(1, -distance, linear);
        loop.Duration = Motion.Lap(Motion.AntsLapMs * laps);
        loop.IterationBehavior = AnimationIterationBehavior.Forever;
        return loop;
    }

    /// <summary>
    /// <paramref name="shape"/>'s dashes started on <paramref name="loop"/> where <paramref name="on"/> and stopped where not,
    /// touched only where that is not what they do now (<paramref name="marching"/>); none to start where there is no loop.
    /// Whether they march now.
    /// </summary>
    internal static bool March(CompositionSpriteShape shape, ScalarKeyFrameAnimation? loop, bool marching, bool on)
    {
        on &= loop is not null;
        if (on == marching)
        {
            return marching;
        }
        if (on)
        {
            shape.StartAnimation("StrokeDashOffset", loop);
        }
        else
        {
            shape.StopAnimation("StrokeDashOffset");
        }
        return on;
    }

    public ShapeVisual Visual { get; }

    public CompositionRoundedRectangleGeometry Box { get; }

    /// <summary>Tests only: whether the dashes are marching now.</summary>
    internal bool Marching => marching;

    /// <summary>
    /// Shown or not; where shown, round a box <paramref name="size"/> big with its top left corner at
    /// <paramref name="at"/> (the visual's parent's px), its corners <paramref name="radius"/>, the
    /// stroke <paramref name="thickness"/> thick and all of it inside the box.
    /// </summary>
    public void Show(bool shown, Vector2 at, Vector2 size, float radius, float thickness)
    {
        Visual.IsVisible = shown;
        if (shown)
        {
            Visual.Offset = new Vector3(at, 0);
            Visual.Size = size;
            // As the selected ring is stroked (EditLook.cs).
            var (offset, inner, corner) = EditLook.Stroke(size, radius, thickness);
            Box.Offset = offset;
            Box.Size = inner;
            Box.CornerRadius = new Vector2(corner, corner);
            band.StrokeThickness = thickness;
            dashes.StrokeThickness = thickness;
        }
        marching = March(dashes, lap, marching, shown);
    }
}
