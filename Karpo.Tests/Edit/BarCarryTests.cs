// A widget carried along the bar while a folder stands open above it (BarCarry): over the folder's
// panel the bar stays as it stood; back on the bar the drag goes on as before.
//
// The person: "when you try to move a widget into a folder it can happen that multiple folders
// open because even though you are already in the folder area it somehow still moves the widget
// down at the taskbar as well, then this triggers the opening of the other folder."
//
// The bar is a real Layout; what the bar shows with the pointer on it is worked out as the dock
// does (Layout.DropSlot).

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed class BarCarryTests
{
    private static RECT Area(int width, int height) => new() { left = 0, top = 0, right = width, bottom = height };

    private static PartSpec Spec(string id, BarKind kind) => new(Config.IslandKey(null, id), kind);

    /// <summary>
    /// A bottom bar, its blocks in the start section: the pin carried (0), folder A (1), a pin (2),
    /// folder B (3) and another pin (4).
    /// </summary>
    private static Layout Bar() =>
        Layout.ForTests(96, Area(1920, 1080), Edge.Bottom, new Config(),
            [Spec("p", BarKind.Pin), Spec("a", BarKind.FolderList), Spec("x", BarKind.Pin), Spec("b", BarKind.FolderList), Spec("y", BarKind.Pin)],
            60f, editing: true);

    private const int Carried = 0;
    private const int FolderA = 1;
    private const int FolderB = 3;

    private static float Middle(Layout layout, int part) => layout.PartAt(part).Left + layout.PartAt(part).Width / 2f;

    /// <summary>What the bar shows with the pointer on it at <paramref name="along"/>, as the dock works it out.</summary>
    private static BarCarry.Shown OnBar(Layout layout, float along) => new(along, layout.DropSlot(along, Carried));

    /// <summary>
    /// Regression: folder A stood open above the bar, the pointer went up into A's panel and along
    /// it to above folder B — and the bar went on reading the pointer's x as a place along it,
    /// opening gaps and sliding its blocks under the folder. Now it keeps what it showed when the
    /// pointer left it and asks nothing of the bar, however long the pointer stays.
    /// </summary>
    [Fact]
    public void OverAnOpenFoldersPanelTheBarStandsStill()
    {
        var layout = Bar();
        var carry = new BarCarry();

        // Along the bar onto folder A's button.
        var onA = Middle(layout, FolderA);
        var first = carry.Step(false, () => OnBar(layout, onA));
        var standing = OnBar(layout, onA);
        Assert.Equal(standing, first);

        // Up into A's panel, and along it to above folder B. On the bar, that x is another place.
        var aboveB = Middle(layout, FolderB);
        Assert.NotEqual(standing.Along, OnBar(layout, aboveB).Along);
        var asked = 0;
        for (var tick = 0; tick < 40; tick++)
        {
            // A move up, a move along, then the drag's timer ticking with the pointer still.
            var pointer = tick == 0 ? onA : aboveB;
            var shown = carry.Step(true, () =>
            {
                asked++;
                return OnBar(layout, pointer);
            });
            // The bar as it stood: the gap where it was, the carried block where it was.
            Assert.Equal(standing.Index, shown.Index);
            Assert.Equal(standing.Along, shown.Along);
        }
        Assert.Equal(0, asked);
    }

    /// <summary>Back down from the panel onto the bar, the drag goes on: the bar follows the pointer again.</summary>
    [Fact]
    public void BackOnTheBarTheDragGoesOn()
    {
        var layout = Bar();
        var carry = new BarCarry();
        var onA = Middle(layout, FolderA);
        carry.Step(false, () => OnBar(layout, onA));

        // Over the panel a while, above B.
        var aboveB = Middle(layout, FolderB);
        for (var tick = 0; tick < 30; tick++)
        {
            carry.Step(true, () => OnBar(layout, aboveB));
        }

        // Down onto B's button: the bar is asked again and shows the pointer there.
        var asked = false;
        var shown = carry.Step(false, () =>
        {
            asked = true;
            return OnBar(layout, aboveB);
        });
        Assert.True(asked, "back on the bar, the bar is asked where the widget would go");
        Assert.Equal(OnBar(layout, aboveB), shown);
    }

    /// <summary>A drag whose pointer is over a panel before the bar has shown anything has one look at the bar and keeps it.</summary>
    [Fact]
    public void APanelBeforeTheBarKeepsTheBarsFirstLook()
    {
        var carry = new BarCarry();
        var asked = 0;
        var first = new BarCarry.Shown(50f, new BarSlot(BarSection.Start, 1));
        for (var tick = 0; tick < 10; tick++)
        {
            var shown = carry.Step(true, () =>
            {
                asked++;
                return tick == 0 ? first : new BarCarry.Shown(50f + (tick * 30), new BarSlot(BarSection.Start, 2));
            });
            Assert.Equal(first, shown);
        }
        Assert.Equal(1, asked);
    }
}
