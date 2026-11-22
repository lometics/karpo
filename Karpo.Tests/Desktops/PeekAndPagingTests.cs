// The peek's fallback outline (OutlineShape: a rounded rim, antialiased, in
// premultiplied BGRA), a pin group's row paging only when it has to
// (Flyout.StripPageSize), and the builds the virtual desktop interface is used
// on (DesktopControl.Known).

using Karpo.Dock;

// Not Karpo.Tests.Desktops: the core tests name BarKind.Desktops as "Desktops" (using static),
// which a namespace of that name would shadow.
namespace Karpo.Tests.DesktopsArea;

public sealed class PeekAndPagingTests
{
    [Fact]
    public void TheRimIsSolidAlongTheEdgesAndEmptyInside()
    {
        const int width = 200;
        const int height = 100;

        Assert.Equal(1f, OutlineShape.Coverage(100, 0, width, height, 3, 8));
        Assert.Equal(1f, OutlineShape.Coverage(100, 2, width, height, 3, 8));
        Assert.Equal(1f, OutlineShape.Coverage(0, 50, width, height, 3, 8));
        Assert.Equal(1f, OutlineShape.Coverage(199, 50, width, height, 3, 8));
        Assert.Equal(0f, OutlineShape.Coverage(100, 3, width, height, 3, 8));
        Assert.Equal(0f, OutlineShape.Coverage(100, 50, width, height, 3, 8));
    }

    [Fact]
    public void RoundCornersLeaveTheCornerPixelOutAndSquareOnesDoNot()
    {
        Assert.Equal(0f, OutlineShape.Coverage(0, 0, 200, 100, 3, 8));
        Assert.Equal(1f, OutlineShape.Coverage(0, 0, 200, 100, 3, 0));
        // Along the curve the edge is antialiased: neither empty nor full.
        var edge = OutlineShape.Coverage(2, 2, 200, 100, 3, 8);
        Assert.InRange(edge, 0.05f, 0.95f);
    }

    [Fact]
    public void PixelsArePremultiplied()
    {
        var pixels = new uint[4 * 4];

        OutlineShape.Fill(pixels, 0, 0, 4, 4, 4, 4, 1, 0, 0x00, 0x78, 0xD4);

        // A corner pixel of a square rim: fully covered, the colour as it is.
        Assert.Equal(0xFF0078D4u, pixels[0]);
        // The middle is a hole.
        Assert.Equal(0u, pixels[5]);
    }

    [Fact]
    public void AFillOfAPieceMatchesThatPieceOfTheWhole()
    {
        var whole = new uint[40 * 20];
        OutlineShape.Fill(whole, 0, 0, 40, 20, 40, 20, 3, 8, 10, 20, 30);
        var piece = new uint[10 * 6];
        OutlineShape.Fill(piece, 30, 14, 10, 6, 40, 20, 3, 8, 10, 20, 30);

        for (var row = 0; row < 6; row++)
        {
            for (var column = 0; column < 10; column++)
            {
                Assert.Equal(whole[(14 + row) * 40 + 30 + column], piece[row * 10 + column]);
            }
        }
    }

    [Fact]
    public void TheStripsCoverTheRimAndNoMoreThanHalf()
    {
        Assert.Equal(9, OutlineShape.Band(3, 8, 1920, 1080));
        Assert.Equal(4, OutlineShape.Band(3, 8, 1920, 8));
        Assert.Equal(0, OutlineShape.Band(3, 8, 0, 0));
    }

    [Theory]
    // Everything fits: one page, no arrows.
    [InlineData(10, 1000f, 50f, 10)]
    [InlineData(20, 1000f, 50f, 20)]
    // Too many: as many as fit beside the two arrows.
    [InlineData(21, 1000f, 50f, 18)]
    [InlineData(100, 1000f, 50f, 18)]
    // Never less than one, whatever the room.
    [InlineData(5, 60f, 50f, 1)]
    [InlineData(5, 0f, 50f, 1)]
    [InlineData(0, 1000f, 50f, 1)]
    public void AGroupsRowPagesOnlyWhenItDoesNotFit(int count, float room, float cell, int expected) =>
        Assert.Equal(expected, Flyout.StripPageSize(count, room, cell));

    [Theory]
    [InlineData(26100, true)]
    [InlineData(26120, true)]
    [InlineData(26200, true)]
    [InlineData(26220, true)]
    // The person's Dev build: without it, a desktop chosen in the list was switched to by Ctrl+Win+arrow,
    // whose slide Explorer draws over everything in Karpo's band, the bars too (§384).
    [InlineData(26300, true)]
    [InlineData(22631, false)]
    [InlineData(27000, false)]
    public void TheShellsDesktopInterfaceIsUsedOnlyOnCheckedBuilds(int build, bool known) =>
        Assert.Equal(known, DesktopControl.Known(build));
}
