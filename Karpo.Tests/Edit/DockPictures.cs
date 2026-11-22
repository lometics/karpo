// A Dock layout's three docks (the person, 2026-10-09: "dock mode should get the left and right slot
// as well"): Start at the left end, search, Task view and the open apps centred, a graph and the
// clock at the right end, each a floating dock of its own, as the dock lays them out. At the bottom
// and the top, dark and light, at rest and in edit mode, at 150 %; and in edit mode with the start
// left empty, its empty room at the left end where a widget dropped there would stand. To
// KARPO_PNG_DIR as dock-three-<bottom|top>-<rest|edit>-<dark|light>-150.png and
// dock-three-empty-start-edit-<dark|light>-150.png.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Tests.Islands;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void DockSheets()
    {
        WindowShots.OnHiddenDesktop("dock-three", (gfx, instance) =>
        {
            const uint dpi = 144;
            var config = new Config();
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                var name = dark ? "dark" : "light";
                foreach (var edge in new[] { Edge.Bottom, Edge.Top })
                {
                    var dock = PictureLayout(LayoutType.Dock, edge);
                    foreach (var editing in new[] { false, true })
                    {
                        EditedDisplay(gfx, instance, dpi, config, look, dock, $"dock-three-{edge.ToString().ToLowerInvariant()}-{(editing ? "edit" : "rest")}-{name}-150.png", editing: editing);
                    }
                }
                var bare = PictureLayout(LayoutType.Dock, Edge.Bottom);
                bare.Islands.RemoveAll(island => island.Section == BarSection.Start);
                EditedDisplay(gfx, instance, dpi, config, look, bare, $"dock-three-empty-start-edit-{name}-150.png");
            }
        });
    }

}
