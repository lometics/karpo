// How a drive widget's cell is put together (DriveIsland.Rows), measured rather
// than looked at. The two complaints the widget it replaces earned:
//
//   "the padding of the disk widget is off" — the icon+name row and the
//   capacity bar had different edges, because the head was centred inside the
//   content and the bar spanned it;
//   "the layout of the disk icon looks off in general" — the icon was not in
//   the square every other island's glyph sits in.
//
// And the rule since: across a horizontal bar a taskbar entry is one row at
// every height (docs\widgets.md §12.5) — the icon, the name, the capacity bar,
// side by side on the cell's middle line. Only down a vertical bar do they
// stack, and there the name and the bar still share their edges.
//
// All of it is arithmetic, so all of it is checked here at every bar size Karpo
// takes, without a window.

using Karpo.Core;
using Karpo.Dock;

namespace Karpo.Tests.Storage;

public sealed class DriveCellTests
{
    private static readonly int[] Heights = [28, 40, 48, 72, 115, 160];

    private static IslandStyle StyleFor(int height, uint dpi, bool vertical, float textScale = 1f)
    {
        var config = new Config { Height = height };
        var look = new Look(
            Glass: false,
            Dark: true,
            Accent: Windows.UI.Color.FromArgb(255, 0x00, 0x78, 0xD4))
        { TextScale = textScale };
        return new IslandStyle(dpi / 96f, vertical, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), look);
    }

    /// <summary>Every bar a drive widget is drawn on here.</summary>
    private static IEnumerable<(string Where, IslandStyle Style)> Styles()
    {
        foreach (var height in Heights)
        {
            foreach (var dpi in (uint[])[96, 144, 192])
            {
                foreach (var scale in (float[])[1f, 2.25f])
                {
                    foreach (var vertical in new[] { false, true })
                    {
                        yield return ($"{height} DIP, {dpi} DPI, text ×{scale}{(vertical ? ", vertical" : "")}",
                            StyleFor(height, dpi, vertical, scale));
                    }
                }
            }
        }
    }

    /// <summary>The cell a widget of <paramref name="cells"/> cells gets, in px.</summary>
    private static (float Width, float Height) CellOf(in IslandStyle style, int cells) =>
        style.Vertical ? (style.Cell, cells * style.Cell) : (cells * style.Cell, style.Cell);

    /// <summary>
    /// Across a horizontal bar the cell is one row: the icon, then the name, then the capacity bar,
    /// left to right inside the cell without meeting, each centred on the cell's middle line. Down a
    /// vertical bar the name and the bar stack and have the same edges, whatever the cell was
    /// widened to — the padding complaint, as arithmetic.
    /// </summary>
    [Fact]
    public void TheRowIsTheIconTheNameAndTheBarAndAStackSharesItsEdges()
    {
        foreach (var (where, style) in Styles())
        {
            foreach (var cells in new[] { 1, 2, 4 })
            {
                var (width, height) = CellOf(style, cells);
                var rows = DriveIsland.Rows(new Fit(style), width, height);
                var at = $"{where}, {cells} cells";
                if (!style.Vertical)
                {
                    var middle = height / 2f;
                    Assert.False(rows.Stacked, $"{at}: a horizontal bar stacked the drive");
                    Assert.True(MathF.Abs(((rows.Icon.top + rows.Icon.bottom) / 2f) - middle) <= 1f, $"{at}: the icon is off the middle line");
                    if (rows.WithName)
                    {
                        Assert.True(rows.Left >= rows.Icon.right, $"{at}: the name starts inside the icon");
                        Assert.True(rows.NameRight > rows.Left, $"{at}: the name has no room");
                        Assert.True(MathF.Abs(rows.NameTop + (rows.Row / 2f) - middle) <= 1f, $"{at}: the name is off the middle line");
                    }
                    if (rows.WithBar)
                    {
                        Assert.True(rows.BarLeft >= (rows.WithName ? rows.NameRight : rows.Icon.right), $"{at}: the bar starts before what it follows");
                        Assert.True(rows.Right > rows.BarLeft && rows.Right <= width, $"{at}: the bar has no length, or runs past the cell");
                        Assert.True(MathF.Abs(rows.BarTop + (rows.Bar / 2f) - middle) <= 1f, $"{at}: the bar is off the middle line");
                    }
                    continue;
                }

                // One content rectangle: the name and the bar start and end together.
                Assert.True(rows.Right > rows.Left, $"{at}: the content has no width");
                Assert.True(rows.Right <= width, $"{at}: the content runs past the cell");
                Assert.True(rows.Left >= 0, $"{at}: the content starts before the cell");
                Assert.Equal(rows.Left, rows.BarLeft);
                Assert.Equal(rows.Right, rows.NameRight);
                if (rows.WithName && rows.WithBar)
                {
                    Assert.True(rows.BarTop > rows.NameTop, $"{at}: the bar is not under the name");
                    Assert.True(rows.BarTop + rows.Bar <= height + 0.01f, $"{at}: the bar runs past the cell");
                }
            }
        }
    }

    /// <summary>
    /// Given the name as it is drawn, the bar starts one gap after it — the gap the name keeps from
    /// the icon — and runs to the content's end, never shorter than its least length: a wider cell
    /// is a longer bar, not a blank stretch before a short one ("the disk progress bar should scale
    /// to take up the remaining space").
    /// </summary>
    [Fact]
    public void TheBarRunsFromOneGapAfterTheNameToTheEnd()
    {
        var seen = 0;
        foreach (var (where, style) in Styles().Where(each => !each.Style.Vertical))
        {
            var fit = new Fit(style);
            foreach (var cells in new[] { 2, 3, 5, 8 })
            {
                var (width, height) = CellOf(style, cells);
                var room = DriveIsland.Rows(fit, width, height);
                if (!room.WithName || !room.WithBar)
                {
                    continue;
                }
                var gap = room.Left - room.Icon.right;
                // Not told the name's width, the bar is its least length at the end.
                var least = room.Right - room.BarLeft;
                foreach (var share in new[] { 0.1f, 0.5f, 1f })
                {
                    var name = MathF.Max(1, MathF.Floor((room.NameRight - room.Left) * share));
                    var rows = DriveIsland.Rows(fit, width, height, 0, true, name);
                    var at = $"{where}, {cells} cells, a name {name} px wide";
                    seen++;
                    Assert.Equal(room.Right, rows.Right);
                    Assert.True(MathF.Abs(rows.BarLeft - (rows.Left + name + gap)) <= 0.01f, $"{at}: the bar starts at {rows.BarLeft}, one gap after the name is {rows.Left + name + gap}");
                    Assert.True(rows.Right - rows.BarLeft >= least - 0.01f, $"{at}: the bar is {rows.Right - rows.BarLeft} px, under its least {least}");
                }
            }
        }
        Assert.True(seen > 100, $"only {seen} rows were looked at");
    }

    [Fact]
    public void TheIconIsASquareInsideTheCell()
    {
        foreach (var (where, style) in Styles())
        {
            foreach (var cells in new[] { 1, 2, 4 })
            {
                var (width, height) = CellOf(style, cells);
                var rows = DriveIsland.Rows(new Fit(style), width, height);
                var box = rows.Icon;

                Assert.Equal(box.right - box.left, box.bottom - box.top, 3);
                Assert.True(box.left >= 0 && box.top >= -0.01f, $"{where}, {cells} cells: the icon starts outside the cell");
                Assert.True(box.right <= width + 0.01f && box.bottom <= height + 0.01f, $"{where}, {cells} cells: the icon runs past the cell");
                Assert.True(box.right - box.left >= 1, $"{where}, {cells} cells: the icon has no size");
            }
        }
    }

    [Fact]
    public void TheBlockIsCentredInTheCell()
    {
        foreach (var (where, style) in Styles())
        {
            var (width, height) = CellOf(style, 2);
            var fit = new Fit(style);
            var rows = DriveIsland.Rows(fit, width, height);

            // The air above the block and under it is the same to within a pixel: the widget
            // this replaces sat about three px low in its cell.
            Assert.True(MathF.Abs(rows.Top - (height - rows.Bottom)) <= 1.51f,
                $"{where}: {rows.Top} px above the block and {height - rows.Bottom} px below it");
            Assert.True(rows.Top >= -0.01f && rows.Bottom <= height + 0.01f, $"{where}: the block runs past the cell");
        }
    }

    [Fact]
    public void OneCellIsTheIconAndTheBarAndTwoAddTheName()
    {
        var style = StyleFor(48, 96, vertical: false);
        var fit = new Fit(style);

        var one = DriveIsland.Rows(fit, style.Cell, style.Cell);
        var two = DriveIsland.Rows(fit, 2 * style.Cell, style.Cell);

        // One cell: no room for a name after the icon, so the row is the icon and the bar after
        // it — the bar is what the widget is for, and it keeps its place before the name does.
        Assert.False(one.Stacked);
        Assert.False(one.WithName);
        Assert.True(one.WithBar);
        Assert.True(one.BarLeft >= one.Icon.right, "the bar follows the icon");
        // Two cells: the icon at the content's start, the name after it, the bar at the end.
        Assert.False(two.Stacked);
        Assert.True(two.WithName);
        Assert.True(two.WithBar);
        Assert.True(two.Left > two.Icon.right, "the name starts after the icon");
        Assert.True(two.BarLeft > two.NameRight, "the bar follows the name");
    }

    [Fact]
    public void AVerticalBarStacksTheIconTheNameAndTheBar()
    {
        var style = StyleFor(48, 96, vertical: true);
        var fit = new Fit(style);

        // One cell across the bar's width and two down it: the room is downwards.
        var rows = DriveIsland.Rows(fit, style.Cell, 2 * style.Cell);

        Assert.True(rows.Stacked);
        Assert.True(rows.WithName);
        Assert.True(rows.WithBar);
        Assert.True(rows.NameTop > rows.Icon.bottom - 1, "the name is under the icon");
        Assert.True(rows.BarTop > rows.NameTop, "the bar is under the name");
    }

    [Fact]
    public void TheArrangementDoesNotDependOnWhatTheDriveShows()
    {
        // Rows is handed only the style and the cell it was given: there is no way for how
        // full the drive is, or whether it is connected, to reach it. Two calls for the same
        // cell are the same arrangement, which is the whole of the fixed-size rule here.
        foreach (var (where, style) in Styles())
        {
            var (width, height) = CellOf(style, 2);
            var fit = new Fit(style);

            Assert.True(DriveIsland.Rows(fit, width, height) == DriveIsland.Rows(fit, width, height), where);
        }
    }

    /// <summary>
    /// A drive face is at most the two cells the entry it replaced was (docs\widgets.md §99, §275:
    /// otherwise what it shows, DriveWidgetOffscreenTests); and no taskbar entry is a drive any more.
    /// </summary>
    [Fact]
    public void ADriveFaceIsAtMostTwoCellsAndNoEntry()
    {
        Assert.Equal(2, DriveIsland.WideCells);
        Assert.Null(BarEntries.Find("drive"));
        Assert.Null(BarEntries.Find("networkDrive"));
    }
}
