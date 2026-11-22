// Where content is placed in the room it has (GlyphAndText.Place): in the middle for one thing to
// show, against the start or the end for a group that holds one end of its widget, and never hanging
// off both sides where it is wider than its room.
//
// A second rule stood here, inferred cell by cell (CellIsland.PlaceOf): the cell handed room nobody
// asked for leaned its content away from it, which is how the now-playing widget's art and words
// held the bar's start and its buttons the end with the room as a gap between. That widget is one
// row now whose words take the room (WidgetShape.Row), so nothing is left over to lean from, and the
// rule went with its one user. Karpo.Tests\Islands\MediaRowTests holds the row.

using Karpo.Dock;

namespace Karpo.Tests.Sizing;

public sealed class CellPlaceTests
{
    [Theory]
    [InlineData(CellPlace.Centre, 100f, 40f, 30f)]
    [InlineData(CellPlace.Start, 100f, 40f, 0f)]
    [InlineData(CellPlace.End, 100f, 40f, 60f)]
    // Content wider than its room starts at the start and is cut at the end,
    // never hanging off both sides.
    [InlineData(CellPlace.Centre, 40f, 100f, 0f)]
    [InlineData(CellPlace.End, 40f, 100f, 0f)]
    internal void ContentIsPlacedWhereItWasTold(CellPlace place, float room, float content, float at) =>
        Assert.Equal(at, GlyphAndText.Place(place, room, content));
}
