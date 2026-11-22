// §593, §698 (the person, 2026-11-13: "sometimes the add widget flyout is very short in height ... when i open it via the folder
// column add button"): how tall the add page is made with the folder it adds into open (EditDeck.AddTall).

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed class AddPageRoomTests
{
    private static RECT R(int left, int top, int right, int bottom) => new() { left = left, top = top, right = right, bottom = bottom };

    // The edit bar along the top of a 2560 x 1440 display, the page hung under it with 1000 px of room.
    private static readonly RECT Bar = R(780, 16, 1780, 64);
    private const int Gap = 8;
    private const int Least = 360;

    [Fact]
    public void AFolderBesideThePageTakesNothingOfIt()
    {
        // A column at the display's left side, nearly its whole height.
        Assert.Equal(1000, EditDeck.AddTall(1000, Bar, R(0, 80, 400, 1380), Edge.Top, Gap, Least));
        Assert.Equal(1000, EditDeck.AddTall(1000, Bar, R(1780, 80, 2200, 1380), Edge.Top, Gap, Least));
    }

    [Fact]
    public void AFolderUnderThePageStopsItShortWhereEnoughIsLeft()
    {
        // The folder from 800 down: 800 - 8 - (64 + 8) = 720 left.
        Assert.Equal(720, EditDeck.AddTall(1000, Bar, R(900, 800, 1500, 1380), Edge.Top, Gap, Least));
    }

    [Fact]
    public void TooLittleLeftThePageGoesOverTheFolder()
    {
        // 380 px left: past the least, short of half the room — a short page, the regression: over the folder instead.
        Assert.Equal(1000, EditDeck.AddTall(1000, Bar, R(900, 460, 1500, 1380), Edge.Top, Gap, Least));
        // 300 px: short of the least.
        Assert.Equal(600, EditDeck.AddTall(600, Bar, R(900, 380, 1500, 1380), Edge.Top, Gap, Least));
    }

    [Fact]
    public void FromABarAtTheFootTheRoomIsAboveTheFolder()
    {
        var bottom = R(780, 1376, 1780, 1424);
        // The folder up to 500: 1376 - 8 - (500 + 8) = 860.
        Assert.Equal(860, EditDeck.AddTall(1000, bottom, R(900, 80, 1500, 500), Edge.Bottom, Gap, Least));
        Assert.Equal(1000, EditDeck.AddTall(1000, bottom, default, Edge.Bottom, Gap, Least));
    }
}
