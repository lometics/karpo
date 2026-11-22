// A glyph and its text must never overlap, and a widget that is a glyph in a
// cell must be exactly one cell.
//
// Both are properties of the arithmetic rather than of any one widget, so they
// are tested over the whole range of rooms, glyph sizes and text sizes a bar
// can produce rather than on the handful a picture would show. That is what
// makes this a test of the class of bug and not of one instance of it.

using Karpo.Dock;

namespace Karpo.Tests.Sizing;

public sealed class GlyphAndTextTests
{
    private static Look LookFor(float textScale) => new(
        Glass: false,
        Dark: true,
        Accent: Windows.UI.Color.FromArgb(255, 0, 0x78, 0xD4))
    { TextScale = textScale };

    /// <summary>Every bar a widget can be measured on: its heights, scales, text scales and edges.</summary>
    private static IEnumerable<(string Where, IslandStyle Style)> Styles()
    {
        foreach (var height in new[] { 28, 32, 40, 48, 64, 72, 96, 115, 160 })
        {
            var config = new Config { Height = height };
            foreach (var dpi in (uint[])[96, 144, 192])
            {
                foreach (var textScale in new[] { 1f, 1.5f, 2.25f })
                {
                    foreach (var vertical in new[] { false, true })
                    {
                        yield return (
                            $"{height} DIP, {dpi} DPI, text x{textScale}{(vertical ? ", vertical" : "")}",
                            new IslandStyle(dpi / 96f, vertical, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), LookFor(textScale)));
                    }
                }
            }
        }
    }

    [Fact]
    public void AGlyphAndItsTextNeverShareAPixel()
    {
        foreach (var (where, style) in Styles())
        {
            var fit = new Fit(style);
            foreach (var cells in new[] { 1, 2, 3, 5, 8 })
            {
                var room = (cells * style.Cell) - (2 * fit.Edge);
                foreach (var glyph in new[] { 0f, 16f * style.Scale, fit.Glyph(0.4f), fit.Glyph(0.6f), room })
                {
                    foreach (var text in new[] { 0f, 8f, 40f * style.Scale, room / 2f, room, room * 2f })
                    {
                        var row = GlyphAndText.Row(room, fit, glyph, text);

                        Assert.True(row.TextRoom <= room + 0.01f, $"{where}: the text is given {row.TextRoom} px of {room}");
                        Assert.True(row.TextAt >= -0.01f, $"{where}: the text starts at {row.TextAt}");
                        if (row.WithGlyph)
                        {
                            // The text begins a whole gap past the glyph's box.
                            Assert.True(row.TextAt >= row.GlyphAt + glyph + fit.Gap - 0.01f,
                                $"{where}: the glyph ends at {row.GlyphAt + glyph} and the text starts at {row.TextAt}");
                            Assert.True(row.GlyphAt >= -0.01f, $"{where}: the glyph starts at {row.GlyphAt}");
                            Assert.True(row.TextAt + row.TextRoom <= room + 0.01f, $"{where}: the pair runs past the room");
                        }
                        else
                        {
                            // The glyph gave way; the text has the room to itself.
                            Assert.True(glyph <= 0f || room - row.TextRoom - fit.Gap < glyph,
                                $"{where}: the glyph gave way with {room - row.TextRoom - fit.Gap} px left, which would have held {glyph}");
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void AGlyphOverItsTextNeverShareAPixelEither()
    {
        foreach (var (where, style) in Styles())
        {
            var fit = new Fit(style);
            var across = style.Cell;
            foreach (var glyph in new[] { 0f, 16f * style.Scale, fit.Glyph(0.4f), across })
            {
                foreach (var height in new[] { 0f, 12f * style.Scale, across / 2f, across })
                {
                    var stack = GlyphAndText.Stack(across, across, fit, glyph, 20f, height);

                    if (stack.WithGlyph)
                    {
                        Assert.True(stack.TextTop >= stack.GlyphTop + glyph - 0.01f, $"{where}: the text starts inside the glyph");
                        Assert.True(stack.GlyphTop >= -0.01f, $"{where}: the glyph starts above the cell");
                        Assert.True(stack.TextTop + height <= across + 0.01f, $"{where}: the pair runs past the cell");
                    }
                    else
                    {
                        Assert.True(glyph <= 0f || glyph + height > across, $"{where}: the glyph gave way where it would have fitted");
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(0, "0m")]
    [InlineData(1, "1m")]
    [InlineData(13, "13m")]
    [InlineData(30, "30m")]
    [InlineData(59, "59m")]
    [InlineData(60, "1h")]
    [InlineData(119, "1h")]     // rounded down: elapsed time reads as how much so far
    [InlineData(120, "2h")]
    [InlineData(1439, "23h")]
    [InlineData(1500, "25h")]
    [InlineData(1560, "26h")]
    public void TheShortTotalIsOneUnitRoundedDown(int minutes, string reads) =>
        Assert.Equal(reads, Units.ShortDuration(minutes));

    [Fact]
    public void AGlyphInACellIsExactlyOneCell()
    {
        foreach (var (where, style) in Styles())
        {
            var fit = new Fit(style);
            foreach (var glyph in new[] { fit.Glyph(0.4f), fit.Glyph(0.5f), fit.Glyph(0.6f), 16f * style.Scale })
            {
                var length = GlyphAndText.Square(style, fit, glyph);

                Assert.Equal(1, style.Cells(length));
                Assert.True(length > 0f, $"{where}: a glyph in a cell has no length");
            }
        }
    }
}
