// edit: the widgets on edit mode's clipboard wear Excel's marching ants on the bar (the person,
// 2026-10-12: "the item that is currently in the clipboard should get a look like an excel cell does
// when it is being copied. same if you select multiples of course"; EditView.Copied, MarchingAnts.cs).
// On the dock's real scene with the bar a new config gets: its last two blocks copied, the last
// selected too — its ring gives way to the ants — and its first block selected beside them,
// keeping its ring. To KARPO_PNG_DIR as edit-copied-<look>.png, a frame of the dashes marching.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void CopiedBlocksWearTheMarchingAnts()
    {
        WindowShots.OnHiddenDesktop("edit-copied", (gfx, instance) =>
        {
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark);
                var config = new Config { Height = 48 };
                var area = new RECT { left = 0, top = 0, right = 1920, bottom = 960 };
                var (specs, islands) = Made(gfx, config, Sections.DefaultLayout(pins: [], inputs: NoPenNoTouch));
                var window = MakeWindow();
                DesktopWindowTarget? target = null;
                Scene? scene = null;
                try
                {
                    var content = new Content { Islands = islands };
                    var layout = new Layout(gfx, 96, area, area, Edge.Bottom, config, look, specs, content, editing: true);
                    // The last two blocks the bar places (the clock's folder among them) copied; the first selected alone.
                    var placed = Enumerable.Range(0, specs.Count).Where(layout.Placed).ToList();
                    Assert.True(placed.Count >= 3, "the default bar places three blocks at least");
                    var (start, weather, clock) = (placed[0], placed[^2], placed[^1]);
                    _ = SetWindowPos(window, HWND.NULL, 0, 0, (int)layout.Width, (int)layout.Height, SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
                    target = Target(gfx, window);
                    scene = Scenes.Build(gfx, target, layout, content, look, new Icons(), shown: true,
                        edit: new EditView(Selected: clock, AlsoSelected: [start], Copied: [clock, weather]));
                    _ = ShowWindow(window, SW.SW_SHOWNOACTIVATE);
                    WindowShots.Settle(250);

                    bool Shows(string name) => scene.Registered(name) is Visual { IsVisible: true };
                    Assert.True(Shows($"edit:{specs[clock].Key}:ants"), "the copied clock wears the ants");
                    Assert.True(Shows($"edit:{specs[weather].Key}:ants"), "the copied weather wears the ants");
                    Assert.False(Shows($"edit:{specs[start].Key}:ants"), "Start was not copied");
                    Assert.False(Shows($"edit:{specs[clock].Key}:selected"), "the ants stand in the clock's ring's place");
                    Assert.True(Shows($"edit:{specs[start].Key}:selected"), "Start keeps its ring");
                    var panel = layout.PanelRect;
                    var part = new RECT { left = (int)panel.X, top = (int)panel.Y, right = (int)panel.Right, bottom = (int)panel.Bottom };
                    Assert.True(WindowShots.Save(window, $"edit-copied-{(dark ? "dark" : "light")}.png", part));

                    // Called off (Escape, Dock.ClearCopied): the ants go, the clock's ring is back.
                    scene.Edit(new EditView(Selected: clock, AlsoSelected: [start]));
                    Assert.False(Shows($"edit:{specs[clock].Key}:ants"));
                    Assert.True(Shows($"edit:{specs[clock].Key}:selected"));
                }
                finally
                {
                    scene?.Dispose();
                    _ = DestroyWindow(window);
                    foreach (var island in islands.Values)
                    {
                        island.Dispose();
                    }
                }
            }
        });
    }
}
