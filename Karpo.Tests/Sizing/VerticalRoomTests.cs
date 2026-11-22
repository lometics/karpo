// The room a line of text has across a vertical bar.
//
// VerticalFit.Room used to answer the bar's own width (Fit.Across, a pixel in
// at either side) while every island draws inside Fit.Edge, 2 to 8 px. A kind
// fitting its text to that was choosing a size for room the drawing never had,
// which is how a date came out "W… 2…". These hold the corrected meaning, and
// the gap the defect opened, so nobody widens it again.

using Karpo.Dock;

namespace Karpo.Tests.Sizing;

public sealed class VerticalRoomTests
{
    private static Look LookFor(float textScale) => new(
        Glass: false,
        Dark: true,
        Accent: Windows.UI.Color.FromArgb(255, 0, 0x78, 0xD4))
    { TextScale = textScale };

    private static IEnumerable<(string Where, Fit Fit, IslandStyle Style)> Bars()
    {
        foreach (var height in new[] { 28, 32, 40, 48, 64, 72, 96, 115, 160 })
        {
            var config = new Config { Height = height };
            foreach (var dpi in (uint[])[96, 144, 192])
            {
                foreach (var textScale in new[] { 1f, 1.5f, 2.25f })
                {
                    var style = new IslandStyle(dpi / 96f, true, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), LookFor(textScale));
                    yield return ($"{height} DIP, {dpi} DPI, text x{textScale}", new Fit(style), style);
                }
            }
        }
    }

    [Fact]
    public void TheRoomIsTheCellLessItsEdgesAndNeverTheBarsWholeWidth()
    {
        foreach (var (where, fit, style) in Bars())
        {
            var room = VerticalFit.Room(fit);

            Assert.Equal(style.Cell - (2 * fit.Side), room, 3);
            Assert.True(room > 0f, $"{where}: no room at all");
            // The bar's own width is the wrong number for text, and always
            // larger: that difference is what the defect handed out.
            Assert.True(room <= VerticalFit.BarWidth(fit) + 0.01f, $"{where}: the room is wider than the bar");
        }
    }

    [Fact]
    public void FittingToTheBarsWidthOverFitsByTheEdgesItIgnores()
    {
        // The size of the bug, on the record rather than in a message: two
        // DIPs of room handed out that the drawing never had on the thinnest
        // bar, fourteen on the thickest — and twice that in device pixels at
        // 200 % scaling, which is where it bit hardest.
        var biggest = 0f;
        var biggestDips = 0f;
        foreach (var (_, fit, _) in Bars())
        {
            var over = VerticalFit.BarWidth(fit) - VerticalFit.Room(fit);
            biggest = MathF.Max(biggest, over);
            biggestDips = MathF.Max(biggestDips, over / fit.Scale);
        }

        Assert.True(biggest >= 2f, "the two answers differ, which is why one of them had to be chosen");
        Assert.True(biggestDips <= 16f, $"the widest over-fit is {biggestDips} DIP ({biggest} px)");
    }

    /// <summary>
    /// What every caller relies on: the size handed back genuinely fits the
    /// room, so a label that was told it fits is not then cut by the drawing.
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void TextChosenForTheRoomAlwaysFitsTheRoom()
    {
        Karpo.Tests.Islands.Offscreen.OnStaThread(gfx =>
        {
            foreach (var (where, fit, _) in Bars())
            {
                var room = VerticalFit.Room(fit);
                foreach (var shape in new[] { "WWW", "88", "Wed", "September" })
                {
                    ReadOnlySpan<float> sizes = [fit.Primary, fit.Secondary, fit.Small, 0.8f * fit.Small];
                    var (px, width, _) = VerticalFit.Across(gfx, shape, room, Weight.Regular, sizes);

                    Assert.True(px > 0f, $"{where}: no size chosen for \"{shape}\"");
                    // Either it fits, or nothing in the ladder did and the
                    // smallest was cut to the room — never wider, either way.
                    Assert.True(width <= room + 0.01f, $"{where}: \"{shape}\" was given {width} px of {room}");
                }
            }
        });
    }
}
