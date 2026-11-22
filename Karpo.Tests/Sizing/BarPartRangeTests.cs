// The bar's own parts keep the size rule every widget keeps.
//
// Start, Search, Task View, a pin, the open apps and the clock are measured outside the island
// branch of Layout, so the rule — the content's size held between the two numbers the person set
// (WidgetBounds) — is applied to them in a place of its own, and this holds that place to it: a
// range above the content gives exactly its least, one below gives exactly its most, and one the
// content lies inside leaves the part the length it measures. That last is what keeps Start at
// 45 DIP against a 44-DIP cell, and the open apps from being cut off at the eight cells most kinds
// start with.
//
// Measured on a real layout without a Gfx (Layout.ForTests), on a bar long enough that nothing is
// left off.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Sizing;

public sealed class BarPartRangeTests
{
    private const string Key = "part";

    private static readonly Config Bar = new() { Height = 48 };

    private static float Cell => Layout.CellPx(96, Bar);

    /// <summary>One part of <paramref name="kind"/> in range <paramref name="room"/>, with <paramref name="tiles"/> open apps.</summary>
    private static Part Lay(BarKind kind, WidgetBounds room, int tiles = 0) => LayOut(kind, room, tiles).Part;

    /// <summary><see cref="Lay"/>, and how many of its buttons stand behind "…" (<see cref="Layout.Overflowed"/>).</summary>
    private static (Part Part, int Overflowed) LayOut(BarKind kind, WidgetBounds room, int tiles = 0)
    {
        var key = Config.IslandKey(null, Key);
        var layout = Layout.ForTests(
            96,
            new RECT { left = 0, top = 0, right = 8000, bottom = 1080 },
            Edge.Bottom,
            Bar,
            [new PartSpec(key, kind, room)],
            Cell,
            tiles: tiles);
        Assert.Empty(layout.NotShown);
        return (layout.PartAt(layout.PartIndex(key)), layout.Overflowed.Count);
    }

    /// <summary>
    /// Where each starts, the part is the length it measures — the same as with no range at all.
    /// Start is the case that makes it matter: one button a hair wider than a cell, which counted
    /// in whole cells would be two, and held to its one would lose the hair.
    /// </summary>
    [Theory]
    [InlineData(BarKind.Start)]
    [InlineData(BarKind.Search)]
    [InlineData(BarKind.TaskView)]
    [InlineData(BarKind.Pin)]
    public void WhereItStartsThePartIsTheLengthItMeasures(BarKind kind)
    {
        Assert.Equal(Lay(kind, WidgetBounds.Any).Width, Lay(kind, WidgetBounds.DefaultFor(kind)).Width);
        if (kind == BarKind.Start)
        {
            Assert.True(Lay(kind, WidgetBounds.DefaultFor(kind)).Width > Cell, "Start is no longer wider than a cell, so this proves nothing about it");
        }
    }

    /// <summary>A least above what the content needs is exactly that many cells.</summary>
    [Theory]
    [InlineData(BarKind.Start)]
    [InlineData(BarKind.Search)]
    [InlineData(BarKind.TaskView)]
    [InlineData(BarKind.Pin)]
    public void ALeastAboveTheContentIsExactlyThatManyCells(BarKind kind)
    {
        Assert.True(Lay(kind, WidgetBounds.Any).Width < 5 * Cell, $"{kind} already measures five cells, so this proves nothing");
        Assert.Equal(5 * Cell, Lay(kind, new WidgetBounds(5, 7)).Width, 0.01f);
    }

    /// <summary>
    /// A most below what the content needs is exactly that many cells: a widget whose content wants
    /// three cells, set to one, is one.
    /// </summary>
    [Fact]
    public void AMostBelowTheContentIsExactlyThatManyCells()
    {
        static float Media(WidgetBounds room) => Layout.ForTests(96, new RECT { right = 8000, bottom = 1080 }, Edge.Bottom, Bar,
            [new PartSpec(Config.IslandKey(null, Key), BarKind.FolderList, room)], Cell, cells: [3]).PartAt(0).Width;
        Assert.True(Media(WidgetBounds.Any) > Cell, "the widget fits one cell, so this proves nothing");
        Assert.Equal(Cell, Media(new WidgetBounds(1, 1)), 0.01f);
    }

    /// <summary>
    /// The open apps start with room for as many as a bar can hold, not the eight cells most kinds
    /// start with: twelve open apps are twelve buttons.
    /// </summary>
    [Fact]
    public void TheOpenAppsAreNotCutOffWhereOtherKindsStart()
    {
        var apps = Lay(BarKind.Apps, WidgetBounds.DefaultFor(BarKind.Apps), tiles: 12);
        Assert.Equal(12, apps.Count);
        Assert.True(apps.Width > WidgetItem.MaxWidth * Cell, $"twelve buttons in {apps.Width / Cell:0.##} cells");
    }

    /// <summary>
    /// Held to a most, the open apps show the buttons that fit in that many cells, and the rest
    /// behind "…", which takes a button's place of the room — and never fewer buttons and "…" for
    /// one more app open, which counting buttons against a range of cells once did.
    /// </summary>
    [Fact]
    public void HeldToAMostTheOpenAppsShowWhatFitsAndNeverFewerForMore()
    {
        var most = new WidgetBounds(1, 4);
        var before = 0;
        var shown = 0;
        for (var open = 1; open <= 12; open++)
        {
            var (apps, behind) = LayOut(BarKind.Apps, most, tiles: open);
            Assert.True(apps.Width <= 4 * Cell + 0.01f, $"{open} apps took {apps.Width / Cell:0.##} cells of a most of four");
            Assert.Equal(open, apps.Count + behind);
            var standing = apps.Count + (behind > 0 ? 1 : 0);
            Assert.InRange(standing, before, open);
            (before, shown) = (standing, apps.Count);
        }
        Assert.True(shown > 1 && shown < 12, $"a most of four cells showed {shown} of twelve apps");
    }
}
