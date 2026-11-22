// What stays in sight through a peek (Karpo.Dock\LivePeek.cs): Karpo's bars and its window list are
// marked DWMWA_EXCLUDED_FROM_PEEK as they are made, as Explorer's taskbars and thumbnail list are.
// The regression: the peek's own argument keeps one window, the bar the card came from, and every
// other bar window — each place and row, every other display's — faded with everything else (the
// person, 2026-10-12: "it also hides the karpo taskbar").
//
// DWM does not read the attribute back (DwmGetWindowAttribute says E_INVALIDARG), so these check
// that DWM took it, on a desktop nobody sees. No peek is shown here: DWM's peek is not a thing of one
// desktop, and would fade the person's own screen. A bar itself is not made either: it registers an
// appbar with the person's shell; it is marked through the same StaysThrough, as it is made.
//
// Needs windows and a compositor, so it runs only with KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Signals;

public sealed unsafe class PeekExclusionTests
{
    /// <summary>The window list is marked as it is made, and DWM took it.</summary>
    [UiFact]
    public void TheWindowListStaysThroughAPeek()
    {
        PreviewToolbarTests.OnAHiddenDesktop(gfx =>
        {
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            PreviewToolbarTests.Register(Preview.ClassName, instance);
            using var preview = new Preview(gfx, instance, 0);

            Assert.True(preview.StaysThroughPeek);
        });
    }

    /// <summary>What StaysThrough says is DWM's answer: yes for a window, no for one that is gone.</summary>
    [UiFact]
    public void StaysThroughSaysWhetherDwmTookIt()
    {
        PreviewToolbarTests.OnAHiddenDesktop(gfx =>
        {
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            PreviewToolbarTests.Register("karpo.tests.preview.app", instance);
            var window = PreviewToolbarTests.Window(instance);

            Assert.True(LivePeek.StaysThrough(window));
            Assert.True(DestroyWindow(window));
            Assert.False(LivePeek.StaysThrough(window));
        });
    }
}
