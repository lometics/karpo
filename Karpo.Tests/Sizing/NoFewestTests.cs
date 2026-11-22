// No kind has a fewest rows of its own (2026-10-13, the person: "remove the min size constraints on
// the widgets too"; docs\widgets.md §166): a line may be one row, whatever its kind, and the height
// a person sets under what a kind used to hold it to is the height it is given. Its content copes —
// a smaller form, fewer weeks or rows, the words dropped before they are cut — and keeps inside its
// line by itself, not only by the folder's clip: each is drawn here into a bitmap taller than the
// line, so ink below it shows.

using Karpo.Dock;
using Karpo.Dock.Weather;
using Karpo.Tests.Folders;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Folders.ListFormPictures;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Sizing;

public sealed unsafe class NoFewestTests
{
    /// <summary>
    /// The kinds that had a fewest over one row before, and that fewest — those that still have a height: the
    /// time header, the weather's hours, the screen time ring, the power mode, Win+P's and nearby sharing's pages
    /// are their forms' whole rows since §432.
    /// </summary>
    public static TheoryData<string, int> Floored => new()
    {
        { "month", 8 },
        { "dayView", 5 },
        { "weekView", 5 },
        { "yearView", 5 },
    };

    /// <summary>Every kind's rows start at one, and go up to past any screen (§192).</summary>
    [Fact]
    public void EveryKindMayBeOneRow()
    {
        foreach (var line in ListWidgets.All.Where(line => line.Line == ListLine.Card && line.Kind.HasHeight()))
        {
            Assert.Equal((LineHeight.Fewest, LineHeight.Most), LineHeight.Limits(line.Kind));
            Assert.Equal(LineHeight.Exactly(2), LineHeight.Exactly(2).Within(line.Kind));
        }
    }

    /// <summary>
    /// A height under a kind's old fewest is honoured: the plan gives the line exactly those rows, and
    /// what it draws, drawn with nothing to clip it, stays inside them.
    /// </summary>
    [UiTheory]
    [MemberData(nameof(Floored))]
    public void UnderTheOldFewestTheLineIsItsRowsAndItsInkStaysInside(string id, int oldFewest)
    {
        var kind = ListWidgets.Find(id)!.Kind;
        var weather = kind is ListKind.WeatherHours or ListKind.WeatherDays;
        OnStaThread(gfx =>
        {
            var was = Clock.Now;
            Clock.Now = weather ? () => SavedWeather.Fixed : () => new DateTime(2026, 9, 22, 14, 40, 7, DateTimeKind.Local);
            try
            {
                var look = LookFor(dark: true);
                var style = FolderFlyout.StyleFor(1f, look, FlyoutTheme.For(look));
                for (var rows = 1; rows < oldFewest; rows++)
                {
                    var set = rows;
                    var folder = ListFormPictures.Folder(gfx, Line(kind, island =>
                    {
                        island.Height = LineHeight.Exactly(set * LineHeight.Row);
                        if (weather)
                        {
                            island.Weather = SavedWeather.Options();
                        }
                    }));
                    try
                    {
                        foreach (var line in folder.Tiles.Select(tile => tile.Island).OfType<WeatherLineTile>())
                        {
                            line.Source = SavedWeather.Source();
                        }
                        var (_, plan) = Plan(folder, look, 1f);
                        var given = Given(plan, 0);
                        Assert.Equal(Rows(set, 1f, 1f), given, 1);
                        var width = plan.Cards[0].right - plan.Cards[0].left;
                        var tile = (CellIsland)folder.Tiles[0].Island;
                        // Laid out once at this height, as the open folder paints it before a look.
                        Bitmap(width, given, dc => tile.PaintOn(dc, PopupPaint.Rect(0, 0, width, given), style, WidgetSurface.List), _ => { });
                        var room = given + 120;
                        (float Left, float Top, float Right, float Bottom) ink = default;
                        Bitmap(width, room,
                            // The card's own drawing, without PaintOn's clip to the line.
                            dc => tile.PaintCard(dc, PopupPaint.Rect(0, 0, width, given), style, WidgetSurface.List),
                            bitmap => ink = InkOf(bitmap, (uint)MathF.Ceiling(width), (uint)MathF.Ceiling(room)));
                        Assert.True(ink.Bottom <= given + 0.5f, $"{id} at {set} rows: ink to {ink.Bottom:0} in a line {given:0} px tall");
                    }
                    finally
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                }
            }
            finally
            {
                Clock.Now = was;
            }
        });
    }
}
