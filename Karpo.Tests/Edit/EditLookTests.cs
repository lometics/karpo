// edit: one look for edit mode, the bar's and an open folder's (Karpo.Dock\EditLook.cs; the person,
// 2026-10-12: "i want the overall look and feel to be the same ... animations ... dnd behaviour"). The
// two drew from numbers typed in twice and had drifted apart: the folder's hairline was the scale
// rounded where the bar's is floored, its rings stood outside its lines, its lifted line was opaque.

using System.Numerics;
using Karpo.Dock;

namespace Karpo.Tests.Edit;

public sealed class EditLookTests
{
    [Theory]
    [InlineData(0.5f, 1f)]
    [InlineData(1f, 1f)]
    [InlineData(1.25f, 1f)]
    [InlineData(1.5f, 1f)]
    [InlineData(1.75f, 1f)]
    [InlineData(2f, 2f)]
    [InlineData(2.5f, 2f)]
    public void TheHairlineIsTheScaleFlooredAtLeastAPixel(float scale, float hair) =>
        Assert.Equal(hair, EditLook.Hair(scale));

    [Theory]
    [InlineData(1f, 0f)]
    [InlineData(2f, 0f)]
    [InlineData(1f, 2f)]
    public void AStrokeLandsWhollyInsideItsBox(float thickness, float inward)
    {
        var box = new Vector2(40, 24);

        var (offset, size, corner) = EditLook.Stroke(box, 4, thickness, inward);

        // Its outline is centred half the stroke in, so the stroke's outer edge is the box's (less inward).
        Assert.Equal(inward, offset.X - (thickness / 2));
        Assert.Equal(inward, offset.Y - (thickness / 2));
        Assert.Equal(box.X, offset.X + size.X + offset.X);
        Assert.Equal(box.Y, offset.Y + size.Y + offset.Y);
        Assert.Equal(MathF.Max(0, 4 - offset.X), corner);
    }

    [Fact]
    public void AStrokeOnABoxTooSmallForItHasNoSizeRatherThanANegativeOne()
    {
        var (_, size, corner) = EditLook.Stroke(new Vector2(2, 2), 1, 4);

        Assert.Equal(Vector2.Zero, size);
        Assert.Equal(0, corner);
    }

    [Fact]
    public void TheRingIsTwiceTheHairline() => Assert.Equal(4f, EditLook.Ring(2f));

    [Fact]
    public void TheLiftedShadowIsTheSurfacesOwnDarkenedAndNoneWhereItHasNone()
    {
        Assert.Equal(0f, EditLook.LiftShadow(0f));
        Assert.Equal(0.28f, EditLook.LiftShadow(0.2f), 4);
        Assert.Equal(1f, EditLook.LiftShadow(0.9f));
    }

    [Fact]
    public void UnderThePointerTheOutlineIsBrighterInEitherLook()
    {
        Assert.True(EditLook.OutlineStrongAlpha(dark: true) > EditLook.OutlineAlpha(dark: true));
        Assert.True(EditLook.OutlineStrongAlpha(dark: false) > EditLook.OutlineAlpha(dark: false));
        Assert.Equal(1f, EditLook.OutlineAlpha(dark: true, highContrast: true));
    }

    /// <summary>A carried thing is see-through, not opaque (both surfaces glide on the one motion's Move: MotionTests).</summary>
    [Fact]
    public void ACarriedThingIsSeeThrough()
    {
        Assert.InRange(EditLook.LiftOpacity, 0.5f, 0.99f);
        Assert.True(EditLook.CarriedOutOpacity < EditLook.LiftOpacity);
    }
}
