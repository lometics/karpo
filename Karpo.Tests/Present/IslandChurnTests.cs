// A widget's surfaces kept through the dock's rebuilds (docs\widgets.md §359; CellIsland.Rebuilding,
// Rebuilt): a rebuild used to make every widget forget what it drew (IIsland.Forget) and paint every cell
// again into the same surfaces, a redraw of the whole bar at every app opened or tray icon come. Now a
// rebuild that changed nothing of a widget draws nothing of it; its settings changed, edit mode on or off,
// another look, a cell of another length — that is what draws it again.

using Karpo.Dock;
using Karpo.Tests.Islands;

namespace Karpo.Tests.Present;

public sealed class IslandChurnTests
{
    [UiFact]
    public void ARebuildThatChangedNothingDrawsNothing()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var config = new Config();
            var item = BarKind.FolderList.New();
            item.Id = "glance";
            var clock = ListKind.Clock.New();
            clock.Id = "glance-clock";
            item.Folder!.Put(clock);
            using var island = new FolderIsland(Config.IslandKey(null, item.Id), item, gfx, config);
            var bar = new Config { Height = 48 };
            var style = new IslandStyle(1f, false, Layout.CellPx(96, bar), Layout.IconPx(96, bar), Edit.AddListPictures.LookFor(dark: true));
            Windows.UI.Composition.CompositionDrawingSurface Rebuild(IslandStyle at)
            {
                island.Rebuilding();
                _ = island.Measure(at);
                var surface = island.Surface(0, at);
                island.Rebuilt();
                return surface;
            }
            var first = Rebuild(style);
            var painted = island.Paints;
            Assert.True(painted > 0);
            // Again and again, nothing changed: the very surface, nothing drawn.
            for (var again = 0; again < 3; again++)
            {
                Assert.Same(first, Rebuild(style));
            }
            Assert.Equal(painted, island.Paints);
            // Its settings changed (the dock's SyncIslands: Forget where what it draws from changed): drawn
            // again, into the surface it had.
            island.Forget();
            Assert.Same(first, Rebuild(style));
            Assert.Equal(painted + 1, island.Paints);
            // Edit mode on: what it shows may change with it — drawn again; and only then.
            island.Arranged = true;
            Assert.Same(first, Rebuild(style));
            island.Arranged = true;
            Assert.Same(first, Rebuild(style));
            Assert.Equal(painted + 2, island.Paints);
            // Another look: another style, its own surface; the old one set aside, the new one kept.
            var light = style with { Look = Edit.AddListPictures.LookFor(dark: false) };
            var lit = Rebuild(light);
            Assert.NotSame(first, lit);
            Assert.Same(lit, Rebuild(light));
            Assert.Equal(painted + 3, island.Paints);
        });
    }
}
