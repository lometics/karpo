// edit: a folder's spacer (the file's "divider") while the folder is arranged: its whole room a band of thin diagonal
// stripes moving slowly along it (§524; the person, 2026-11-07: "the spacers between groups should be a moving stripe
// pattern and not the line"). It shows that the room is a spacer to take hold of, and how much room it takes: a Fixed
// spacer its fixed room, a Grow spacer all it grows to (FolderPlan.Bands). At rest it is empty room and nothing is drawn
// (§505).
//
// Subtle: the folder's text at a low alpha, stronger under the pointer and selected (InkOf); a 4 DIP corner as a card's,
// none of a plate's or a card's look. The selected ring and the marching ants stand round it as round any line.
//
// How: one stroke, no surface. A straight line along the band, stroked 1 thick and dashed (StripeDip on, GapDip off),
// laid in a space of DIPs along the band and of the band's height across it, then leant (Lay): its dashes are the
// stripes, its thickness the band's height, all of it clipped to the band's rounded box. The stripes move by the
// compositor alone on the marching ants' own loop (MarchingAnts.Loop, March): one StrokeDashOffset key-framed through a
// stripe and its gap over Laps of the ants' lap. Nothing is drawn again while they move or as the band grows; with
// Karpo's Animations off they stand still.

using System.Numerics;
using Windows.UI.Composition;
using Color = Windows.UI.Color;

namespace Karpo.Dock;

/// <summary>An arranged folder spacer's band of moving diagonal stripes: its visual, clipped to its rounded box, and the stroke that is its stripes.</summary>
internal sealed class SpacerStripes
{
    /// <summary>A stripe, along the band, in DIPs.</summary>
    internal const float StripeDip = 1.5f;

    /// <summary>The gap after a stripe, along the band, in DIPs.</summary>
    internal const float GapDip = 4.5f;

    /// <summary>A stripe and its gap: what the stripes move through in one loop.</summary>
    internal const float PeriodDip = StripeDip + GapDip;

    /// <summary>How far a stripe leans along the band for each px of its height: 45°.</summary>
    internal const float Lean = 1f;

    /// <summary>The band's corner, as a card's (SettingsCard's 4 DIPs).</summary>
    internal const float CornerDip = 4f;

    /// <summary>How strong the stripes are, of the folder's text: at rest, and under the pointer or selected.</summary>
    internal const float RestAlpha = 0.22f, StrongAlpha = 0.5f;

    /// <summary>How many of the ants' laps the stripes take to move through a stripe and its gap: slow and steady.</summary>
    internal const double Laps = 4;

    private readonly CompositionLineGeometry line;
    private readonly CompositionSpriteShape stripes;
    private readonly CompositionRoundedRectangleGeometry box;
    private bool marching;

    /// <param name="keep">What the visuals are kept by, to be closed with them.</param>
    public SpacerStripes(Compositor c, Func<IDisposable, IDisposable> keep)
    {
        T Keep<T>(T value)
            where T : IDisposable => (T)keep(value);
        Visual = Keep(c.CreateShapeVisual());
        box = Keep(c.CreateRoundedRectangleGeometry());
        Visual.Clip = Keep(c.CreateGeometricClip(box));
        line = Keep(c.CreateLineGeometry());
        stripes = Keep(c.CreateSpriteShape(line));
        stripes.StrokeThickness = 1;
        stripes.StrokeDashArray.Add(StripeDip);
        stripes.StrokeDashArray.Add(GapDip);
        stripes.StrokeDashCap = CompositionStrokeCap.Flat;
        stripes.StrokeStartCap = CompositionStrokeCap.Flat;
        stripes.StrokeEndCap = CompositionStrokeCap.Flat;
        Visual.Shapes.Add(stripes);
        Visual.IsVisible = false;
    }

    public ShapeVisual Visual { get; }

    /// <summary>Tests only: whether the stripes are moving now.</summary>
    internal bool Marching => marching;

    /// <summary>Tests only: the brush the stripes are in now.</summary>
    internal CompositionBrush? Ink => stripes.StrokeBrush;

    /// <summary>
    /// The stripes over a band <paramref name="size"/> big (px), at <paramref name="scale"/> px per DIP: the visual that
    /// size, its rounded clip, and the stroke laid across it (<see cref="LayOf"/>). The stripes keep their place along
    /// the band whatever its size, so a band that grows does not jump.
    /// </summary>
    public void Lay(Vector2 size, float scale)
    {
        var laid = LayOf(size, scale);
        Visual.Size = size;
        box.Size = size;
        box.CornerRadius = new Vector2(laid.Corner, laid.Corner);
        line.Start = laid.From;
        line.End = laid.To;
        stripes.TransformMatrix = laid.Transform;
    }

    /// <summary>The stripes in <paramref name="brush"/>; set only where it is another.</summary>
    public void Wear(CompositionBrush? brush)
    {
        if (!ReferenceEquals(stripes.StrokeBrush, brush))
        {
            stripes.StrokeBrush = brush;
        }
    }

    /// <summary>The stripes moving on <paramref name="loop"/> where <paramref name="on"/>, else standing still (<see cref="MarchingAnts.March"/>).</summary>
    public void Move(ScalarKeyFrameAnimation? loop, bool on) => marching = MarchingAnts.March(stripes, loop, marching, on);

    /// <summary>
    /// How the stripes lie over a band <paramref name="size"/> big at <paramref name="scale"/>: the stroke's line, from
    /// <see cref="Laid.From"/> to <see cref="Laid.To"/> in a space of DIPs along the band (u) and of its height across it
    /// (v, 0 at its top, 1 at its bottom; the line at ½, its thickness of 1 the whole height), and
    /// <see cref="Laid.Transform"/> taking that space to the band's px, a stripe's top <see cref="Lean"/> times the height
    /// further along than its foot. The line runs a whole number of stripes past both ends, wherever the lean takes it,
    /// so the band is striped edge to edge while they move; <see cref="Laid.Corner"/> the clip's corner.
    /// </summary>
    internal static Laid LayOf(Vector2 size, float scale)
    {
        var px = scale > 0 ? scale : 1;
        var (width, height) = (MathF.Max(0, size.X), MathF.Max(0, size.Y));
        var lean = height * Lean;
        // u = 0 at the band's left foot; the line starts a whole number of stripes before it, past the lean.
        var before = MathF.Ceiling(lean / px / PeriodDip) + 1;
        var after = MathF.Ceiling(width / px / PeriodDip) + 1;
        var transform = new Matrix3x2(px, 0, -lean, height, lean, 0);
        var corner = MathF.Min(MathF.Round(CornerDip * px), MathF.Min(width, height) / 2);
        return new Laid(transform, new Vector2(-before * PeriodDip, 0.5f), new Vector2(after * PeriodDip, 0.5f), corner);
    }

    /// <summary>What <see cref="LayOf"/> says.</summary>
    internal readonly record struct Laid(Matrix3x2 Transform, Vector2 From, Vector2 To, float Corner);

    /// <summary>
    /// The colour of the stripes in <paramref name="theme"/>: the folder's text at <see cref="RestAlpha"/>, or
    /// <see cref="StrongAlpha"/> where <paramref name="strong"/> (under the pointer, selected); in high contrast the text
    /// itself, the selected ring saying the rest.
    /// </summary>
    internal static Color InkOf(FlyoutTheme theme, bool strong)
    {
        var text = theme.Text;
        var alpha = theme.HighContrast ? 1f : strong ? StrongAlpha : RestAlpha;
        static byte Byte(float value) => (byte)MathF.Round(Math.Clamp(value, 0f, 1f) * 255f);
        return Color.FromArgb(Byte(text.a * alpha), Byte(text.r), Byte(text.g), Byte(text.b));
    }
}
