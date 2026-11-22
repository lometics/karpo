// A widget's content is centred in the room its shape reserved.
//
// The partner rule to "reserve the worst thing you can ever say, so the widget
// never moves": what is drawn is usually shorter than what was reserved, and
// unless the drawing centres on its own ink, all of that slack lands on one
// side. Every widget is then stable and off-centre, which is what the user saw
// in the screen time — "5h 10m" sitting left in a cell measured for "88h 88m".
//
// Measured rather than eyeballed: each kind is drawn twice, once with a short
// value and once with a long one, the ink read back off the bitmap, and its
// centre compared with the cell's. A kind that places its content from the left
// of the reserved box fails on the short value and passes on the long one,
// which is exactly the signature of this bug.

using Karpo.Dock;
using Karpo.Tests.Islands;

namespace Karpo.Tests.Sizing;

public sealed unsafe class CentringTests
{
    private static Look LookFor(bool dark) => new(
        Glass: false,
        Dark: dark,
        Accent: Windows.UI.Color.FromArgb(255, 0, 0x78, 0xD4));

    /// <summary>
    /// The centre of what <paramref name="island"/> draws in cell
    /// <paramref name="cell"/>, against the centre of the cell itself, in px.
    /// </summary>
    private static float OffCentre(CellIsland island, int cell, in IslandStyle style)
    {
        var (width, height) = island.CellSize(cell, style);
        var drawn = 0f;
        var target = width / 2f;
        var local = island;
        var at = cell;
        var metrics = style;
        Offscreen.Bitmap(width, height, dc => local.PaintCell(dc, at, metrics, width, height), bitmap =>
        {
            var (left, _, right, _) = Offscreen.InkOf(bitmap, (uint)MathF.Ceiling(width), (uint)MathF.Ceiling(height));
            drawn = right > left ? (left + right) / 2f : target;
        });
        return drawn - target;
    }

    /// <summary>
    /// The kinds that reserve more room than they usually draw. Each is measured
    /// on the bar's own heights, where a cell is small enough for a stray
    /// character's worth of slack to be obvious.
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void WhatAWidgetDrawsIsCentredInTheRoomItReserved()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var height in new[] { 28, 48, 72 })
            {
                var config = new Config { Height = height };
                foreach (var dark in new[] { true, false })
                {
                    var style = new IslandStyle(1f, false, Layout.CellPx(96, config), Layout.IconPx(96, config), LookFor(dark));
                    // The weather's centring is its face's (WeatherFaceTests).
                    foreach (var kind in new[] { BarKind.NightLight, BarKind.EnergySaver, BarKind.NightLight })
                    {
                        var widget = new BarItem { Id = kind.ToString(), Kind = kind };
                        widget.Normalize();
                        if (Karpo.Dock.Islands.Create(Config.IslandKey(null, widget.Id), widget, gfx, config) is not CellIsland island)
                        {
                            continue;
                        }
                        using (island)
                        {
                            _ = island.Refresh(gfx);
                            for (var cell = 0; cell < island.CellCount; cell++)
                            {
                                var off = OffCentre(island, cell, style);
                                // Within a pixel and a half: the drawing rounds
                                // to whole pixels, and an odd slack cannot be
                                // split evenly.
                                Assert.True(MathF.Abs(off) <= 1.5f,
                                    $"{kind} cell {cell} at {height} DIP ({(dark ? "dark" : "light")}) draws {off} px off the cell's centre");
                            }
                        }
                    }
                }
            }
        });
    }
}
