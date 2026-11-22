// The shape of a taskbar button on the bar: one grid cell square, flush against the next on the
// card of its group (2026-10-09: the pins and the open apps join groups like every widget, "they
// should join too") — the same for a pinned app and for a running one that is not pinned, because
// they are the same button.
//
// Two complaints, one shape. "The application icons should also have the aspect ratio of 1x1": a
// button was its highlight and 2 DIPs either side, which is what the bar's thickness allows up to
// 48 DIPs and stops growing above it, while the grid cell every other widget stands in keeps
// growing — so a 64-DIP bar gave an app button a card 44 px wide and 60 px tall. "The applications
// that are not pinned should also be shown with a spacing in between": every pin is a part of its
// own, so the gap and the card came to it from the rules about parts, while the running apps all
// share the Apps part and had neither — one wide card under a row with no gaps in it.
//
// Measured on a real layout without a Gfx (Layout.ForTests), with the pins and the running apps on
// the one bar so the two are compared against each other rather than against a number.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Sizing;

public sealed class AppButtonTests
{
    private static readonly uint[] Dpis = [96, 120, 144, 192];

    private static readonly int[] Heights = [28, 32, 40, 48, 56, 72, 96, 160];

    private static readonly Edge[] Edges = [Edge.Bottom, Edge.Top, Edge.Left, Edge.Right];

    private const string FirstPin = "pin-a";

    private const string SecondPin = "pin-b";

    private const string AppsKey = "apps";

    private static RECT Area(int width, int height) => new() { left = 0, top = 0, right = width, bottom = height };

    private static PartSpec Spec(string id, BarKind kind) => new(Config.IslandKey(null, id), kind);

    /// <summary>Two pinned apps and <paramref name="running"/> running ones that are not pinned.</summary>
    private static Layout Bar(uint dpi, int height, Edge edge, int running, BarStyle? style = null)
    {
        var config = new Config { Height = height, Style = style };
        List<PartSpec> specs =
        [
            Spec(FirstPin, BarKind.Pin),
            Spec(SecondPin, BarKind.Pin),
            Spec(AppsKey, BarKind.Apps),
        ];
        var area = edge.IsVertical() ? Area(1600, 4000) : Area(4000, 1600);
        return Layout.ForTests(dpi, area, edge, config, specs, Layout.CellPx(dpi, config), tiles: 2 + running);
    }

    private static float Along(Layout layout, RectF rect) => layout.Vertical ? rect.Y : rect.X;

    private static float Length(Layout layout, RectF rect) => layout.Vertical ? rect.Height : rect.Width;

    private static float Across(Layout layout, RectF rect) => layout.Vertical ? rect.Width : rect.Height;

    // --- The square -----------------------------------------------------------------

    /// <summary>
    /// A button's cell is the grid's own square, so its card is 1×1 at every bar height. The two
    /// numbers agreed exactly up to a 48-DIP bar and parted above it, which is the sort of agreement
    /// that reads as a rule until somebody drags the bar taller.
    /// </summary>
    [Fact]
    public void AnAppButtonIsOneGridCell()
    {
        foreach (var dpi in Dpis)
        {
            foreach (var height in Heights)
            {
                var layout = Bar(dpi, height, Edge.Bottom, running: 3);

                Assert.Equal(layout.CellSide, layout.Slot);
            }
        }
    }

    /// <summary>A pinned app's block is that square, on every edge: as wide as it is tall.</summary>
    [Fact]
    public void APinnedAppsBlockIsASquare()
    {
        foreach (var dpi in Dpis)
        {
            foreach (var height in Heights)
            {
                foreach (var edge in Edges)
                {
                    var layout = Bar(dpi, height, edge, running: 3);
                    var block = layout.BlockRect(layout.PartIndex(Config.IslandKey(null, FirstPin)));

                    Assert.Equal(Across(layout, block), Length(layout, block));
                    Assert.Equal(layout.CellSide, Length(layout, block));
                }
            }
        }
    }

    /// <summary>
    /// And a running app's cell is the same square. This is what the card is drawn over
    /// (<see cref="Layout.TileCell"/>); the button's own highlight inside it is the taskbar's and is
    /// smaller, which is true of a pin as much as of a running app.
    /// </summary>
    [Fact]
    public void ARunningAppsCellIsAPinnedAppsCell()
    {
        foreach (var dpi in Dpis)
        {
            foreach (var height in Heights)
            {
                foreach (var edge in Edges)
                {
                    var layout = Bar(dpi, height, edge, running: 3);
                    var pin = layout.TileCell(0);
                    var what = $"{dpi} dpi, {height} DIP, {edge}";

                    Assert.False(pin.IsEmpty, what);
                    for (var tile = 2; tile < 5; tile++)
                    {
                        var cell = layout.TileCell(tile);
                        Assert.False(cell.IsEmpty, what);
                        Assert.Equal(Length(layout, pin), Length(layout, cell));
                        Assert.Equal(Across(layout, pin), Across(layout, cell));
                        Assert.Equal(layout.CellSide, Length(layout, cell));
                    }
                }
            }
        }
    }

    // --- The room between two of them -----------------------------------------------

    /// <summary>
    /// Two running apps keep the room two pins keep, and the last pin the same from the first
    /// running app. The gap is read off the same bar rather than compared with a number, so this
    /// says "the same" — a widget's gap (§501), as between any two widgets.
    /// </summary>
    [Fact]
    public void RunningAppsKeepTheRoomPinsKeep()
    {
        foreach (var dpi in Dpis)
        {
            foreach (var height in Heights)
            {
                // The islands look, where there is a gap to keep.
                var layout = Bar(dpi, height, Edge.Bottom, running: 3, BarStyle.Islands);
                var what = $"{dpi} dpi, {height} DIP";
                var firstPin = layout.BlockRect(layout.PartIndex(Config.IslandKey(null, FirstPin)));
                var secondPin = layout.BlockRect(layout.PartIndex(Config.IslandKey(null, SecondPin)));
                var betweenPins = Along(layout, secondPin) - (Along(layout, firstPin) + Length(layout, firstPin));

                for (var tile = 3; tile < 5; tile++)
                {
                    var before = layout.TileCell(tile - 1);
                    var after = layout.TileCell(tile);
                    var gap = Along(layout, after) - (Along(layout, before) + Length(layout, before));

                    Assert.Equal(betweenPins, gap, 0.01);
                }
                var firstRunning = layout.TileCell(2);
                Assert.Equal(betweenPins, Along(layout, firstRunning) - (Along(layout, secondPin) + Length(layout, secondPin)), 0.01);
                Assert.Equal(layout.WidgetGap, betweenPins, 0.01);
            }
        }
    }

    /// <summary>The block is its buttons and the room between them, and keeps none after the last.</summary>
    [Fact]
    public void TheBlockIsItsButtonsAndTheRoomBetweenThem()
    {
        foreach (var height in Heights)
        {
            foreach (var running in new[] { 1, 2, 5 })
            {
                var layout = Bar(96, height, Edge.Bottom, running, BarStyle.Islands);
                var block = layout.BlockRect(layout.PartIndex(Config.IslandKey(null, AppsKey)));
                var firstPin = layout.BlockRect(layout.PartIndex(Config.IslandKey(null, FirstPin)));
                var secondPin = layout.BlockRect(layout.PartIndex(Config.IslandKey(null, SecondPin)));
                var gap = Along(layout, secondPin) - (Along(layout, firstPin) + Length(layout, firstPin));

                Assert.Equal(running * layout.CellSide + (running - 1) * gap, Length(layout, block), 0.01);
            }
        }
    }

    /// <summary>
    /// A connected bar keeps the same gap here as between its widgets (§501): a member's, and a gap
    /// these buttons kept on their own would be a second rule for them alone.
    /// </summary>
    [Theory]
    [InlineData(28)]
    [InlineData(48)]
    public void ACompactBarPacksThemAsItPacksEverythingElse(int height)
    {
        var layout = Bar(96, height, Edge.Bottom, running: 3, BarStyle.Floating);
        var block = layout.BlockRect(layout.PartIndex(Config.IslandKey(null, AppsKey)));

        Assert.True(layout.Connected);
        Assert.Equal((3 * layout.CellSide) + (2 * layout.WidgetGap), Length(layout, block), 0.01);
    }

    // --- Hit-testing ----------------------------------------------------------------

    /// <summary>
    /// Each button owns the room after it, so a press in the gap still reaches one rather than the
    /// bar behind it — as a press between two pins does, and as the tray's cells answer.
    /// </summary>
    [Fact]
    public void NoPointOfTheBlockFallsBetweenTwoButtons()
    {
        foreach (var height in new[] { 28, 48, 96 })
        {
            // The islands look, where there is a gap between two buttons to fall into.
            var layout = Bar(96, height, Edge.Bottom, running: 4, BarStyle.Islands);
            var block = layout.BlockRect(layout.PartIndex(Config.IslandKey(null, AppsKey)));
            var across = block.Y + block.Height / 2f;

            for (var at = block.X; at < block.Right; at += 1f)
            {
                var hit = layout.HitTest(at, across);
                Assert.True(hit is { Kind: HitKind.Tile }, $"{height} DIP, at {at}: {hit?.Kind}");
            }
        }
    }

    /// <summary>Every button in the block is reachable, and each point lands on the one it is over.</summary>
    [Fact]
    public void EachButtonIsHitOnItsOwn()
    {
        foreach (var edge in Edges)
        {
            var layout = Bar(96, 48, edge, running: 4);
            for (var tile = 0; tile < 6; tile++)
            {
                var cell = layout.TileCell(tile);
                Assert.False(cell.IsEmpty, $"{edge}: button {tile}");
                var hit = layout.HitTest(cell.X + cell.Width / 2f, cell.Y + cell.Height / 2f);

                Assert.Equal(HitKind.Tile, hit?.Kind);
                Assert.Equal(tile, hit?.Index);
            }
        }
    }

    // --- What happens when they do not fit ------------------------------------------

    /// <summary>
    /// A button the bar has no room for is not drawn at all, rather than drawn cut in half: its cell
    /// and its highlight both come back empty, and the two agree, so a card is never left standing
    /// where no button is. It stands behind "…" instead, after the last button that fits, as Windows
    /// 11's taskbar puts the apps it cannot fit (AppOverflowTests).
    /// </summary>
    [Fact]
    public void AButtonPastTheBarsEndIsNotDrawnAndNeitherIsItsCard()
    {
        var config = new Config { Height = 48 };
        List<PartSpec> specs = [Spec(AppsKey, BarKind.Apps)];
        // A narrow bar and far more apps than fit.
        var layout = Layout.ForTests(96, Area(300, 1600), Edge.Bottom, config, specs, Layout.CellPx(96, config), tiles: 40);
        var shown = 0;

        for (var tile = 0; tile < 40; tile++)
        {
            var button = layout.ItemRect(Hit.Tile(tile));
            var cell = layout.TileCell(tile);

            Assert.Equal(button.IsEmpty, cell.IsEmpty);
            shown += button.IsEmpty ? 0 : 1;
        }

        Assert.True(shown is > 0 and < 40, $"{shown} of 40 buttons fit a 300 px bar");
        // The ones that fit are the ones at the start, and the rest are behind "…", which fits too.
        Assert.False(layout.ItemRect(Hit.Tile(0)).IsEmpty);
        Assert.True(layout.ItemRect(Hit.Tile(39)).IsEmpty);
        Assert.Equal(Enumerable.Range(shown, 40 - shown), layout.Overflowed);
        Assert.False(layout.ItemRect(Hit.AppOverflow).IsEmpty);
        Assert.True(layout.ItemRect(Hit.AppOverflow).Right <= 300);
    }
}
