// flush: on an islands bar spaced 0 (Layout.Flush, §533, §589) the plate under the pointer and the keyboard's ring are
// square, as its cards are, at any height; spaced 6 they keep their corners, a thin bar too (§123 to §532 every
// islands bar under Config.ThinHeight was flush by itself).
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiTheory]
    [InlineData(32, 0, true)]
    [InlineData(48, 0, true)]
    [InlineData(32, 6, false)]
    [InlineData(48, 12, false)]
    public void AFlushBarsPlatesAreSquare(int height, int spacing, bool square)
    {
        OnABar(height, SearchBar(box: false), (layout, scene) =>
        {
            Assert.Equal(square, layout.Flush);
            foreach (var hit in new[] { Hit.Start, Hit.Search, Hit.TaskView })
            {
                scene.Highlight(hit, pressed: false);
                scene.KeyFocus(hit);
                var (plate, ring) = scene.Corners;
                Assert.Equal(square, plate == 0f);
                Assert.Equal(square, ring == 0f);
                scene.Highlight(hit, pressed: true);
                Assert.Equal(square, scene.Corners.Highlight == 0f);
            }
        }, editing: false, style: BarStyle.Islands, spacing: spacing);
    }
}
