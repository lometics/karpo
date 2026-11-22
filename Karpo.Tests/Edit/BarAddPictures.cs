// The bar's "+" in edit mode on the dock's real scene (the person, 2026-10-23; docs\widgets.md §297):
// each where the layout says (Layout.Adds), none at rest, out of sight while a block is carried, and
// pressed while its add list is open. And pictures, to KARPO_PNG_DIR as
// bar-add-<case>-<look>-<scale>.png: the taskbar with the left, the middle and the right filled, with
// the middle empty, a dock, a bar down the left, and the busy bar the apps fold for.
//
// Needs a compositor: KARPO_UI_TESTS=1, as the other scene tree tests.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>In edit mode every "+" the layout has is drawn where it stands, at rest; at rest none is.</summary>
    [UiFact]
    public void EveryPlusIsDrawnWhereTheLayoutSaysAndNoneAtRest()
    {
        OnABar(48, ThreeSections(), (layout, scene) =>
        {
            Assert.Equal(4, layout.Adds().Count);
            Assert.Equal(layout.Adds().Select(add => (add.Rect, 0)), scene.AddsShown());
        });
        OnABar(48, NoMiddle(), (layout, scene) =>
        {
            Assert.Equal(3, layout.Adds().Count);
            Assert.Equal(layout.Adds().Select(add => (add.Rect, 0)), scene.AddsShown());
        });
        OnABar(48, ThreeSections(), (layout, scene) =>
        {
            Assert.Empty(layout.Adds());
            Assert.Empty(scene.AddsShown());
        }, editing: false);
    }

    /// <summary>
    /// Under the pointer a "+" is lighter, held down darker (§617 its add page open, the empty ghost stands at its place, no
    /// line); while a block is carried none shows, and they come back as it is put down; while a folder of the bar stands open
    /// none shows either (§397).
    /// </summary>
    [UiFact]
    public void APlusLightsPressesAndStepsAsideForACarriedBlock()
    {
        OnABar(48, ThreeSections(), (layout, scene) =>
        {
            scene.Edit(new EditView(AddLit: 1));
            Assert.Equal([0, 1, 0, 0], scene.AddsShown().Select(shown => shown.Look));

            scene.Edit(new EditView(AddPressed: 2));
            Assert.Equal([0, 0, 2, 0], scene.AddsShown().Select(shown => shown.Look));

            scene.Edit(new EditView(Dragging: 2, PointerAlong: 60f, DropIndex: new BarSlot(BarSection.Start, 1)));
            Assert.Empty(scene.AddsShown());

            scene.Edit(null);
            Assert.Equal(4, scene.AddsShown().Count);

            // §397: a folder of the bar open, its own "+" are where to add; the bar's stand aside until it closes.
            scene.Edit(new EditView(FolderOpen: true));
            Assert.Empty(scene.AddsShown());

            scene.Edit(new EditView());
            Assert.Equal(4, scene.AddsShown().Count);
        });
    }

    /// <summary>
    /// Pictures of the "+" on real bars in edit mode, dark and light, at 100 % and 150 %: the bar with
    /// all three sections filled, with nothing in the middle, a dock (floating), a bar down the left,
    /// and the busy bar whose apps fold into "…" for them.
    /// </summary>
    [UiFact]
    public void BarAddSheets()
    {
        WindowShots.OnHiddenDesktop("bar-add", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var name = $"{(dark ? "dark" : "light")}-{dpi * 100 / 96}";
                    var look = AddListPictures.LookFor(dark) with { Animations = false };
                    foreach (var (which, bar, style, edge, apps) in new (string, List<Karpo.Core.BarItem>, BarStyle, Edge, int)[]
                    {
                        ("taskbar", CentredBar(), BarStyle.Islands, Edge.Bottom, 4),
                        ("empty-middle", [.. CentredBar().Where(item => item.Section != BarSection.Middle)], BarStyle.Islands, Edge.Bottom, 0),
                        ("dock", CentredBar(), BarStyle.Floating, Edge.Bottom, 4),
                        ("left", CentredBar(), BarStyle.Islands, Edge.Left, 4),
                        ("busy", CentredBar(), BarStyle.Islands, Edge.Bottom, 40),
                    })
                    {
                        var config = new Config { Height = 48, Style = style };
                        var (specs, islands) = Made(gfx, config, bar);
                        try
                        {
                            var along = (int)(1600 * dpi / 96);
                            var area = edge.IsVertical()
                                ? new RECT { left = 0, top = 0, right = (int)(dpi * 10), bottom = along }
                                : new RECT { left = 0, top = 0, right = along, bottom = (int)(dpi * 10) };
                            var content = new Content { Islands = islands, Tiles = [.. Enumerable.Range(0, apps).Select(BusyApp)] };
                            var layout = new Layout(gfx, dpi, area, area, edge, config, look, specs, content, editing: true);
                            Assert.NotEmpty(layout.Adds());
                            Shoot(gfx, layout, look, new EditView(), $"bar-add-{which}-{name}.png", content);
                        }
                        finally
                        {
                            foreach (var island in islands.Values)
                            {
                                island.Dispose();
                            }
                        }
                    }
                }
            }
        });
    }
}
