// A window list's cards beyond the left click (docs\widgets.md §362): a card's right click is its
// window's own menu, as on Windows' taskbar; a drag of files finds the card it rests on; the keys light
// the card they are on. The real popup, on a desktop of its own, with windows the test makes.
//
// Needs windows and a compositor, so it runs only with KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Signals;

public sealed unsafe class PreviewCardTests
{
    private static readonly Look Dressed = new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0, 0x78, 0xD4), Animations: false);

    [UiFact]
    public void ACardsRightClickIsItsWindowsMenuAndADragFindsItsCard()
    {
        PreviewToolbarTests.OnAHiddenDesktop(gfx =>
        {
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            PreviewToolbarTests.Register(Preview.ClassName, instance);
            PreviewToolbarTests.Register("karpo.tests.preview.app", instance);
            var first = PreviewToolbarTests.Window(instance);
            var second = PreviewToolbarTests.Window(instance);
            using var preview = new Preview(gfx, instance, 0);
            try
            {
                preview.Show(gfx, Dressed, 1f, 0, [(first, "First"), (second, "Second")], new POINT { x = 400, y = 1000 }, new RECT { right = 1920, bottom = 1040 });
                Assert.Equal(2, preview.Cards);
                Assert.Equal(first, preview.CardWindow(0));
                Assert.Equal(second, preview.CardWindow(1));
                Assert.Null(preview.CardWindow(2));

                var one = PreviewToolbarTests.Middle(preview.CardBounds(0)!.Value);
                var two = PreviewToolbarTests.Middle(preview.CardBounds(1)!.Value);
                // The right click: the window's own menu, the card's window named.
                Assert.Equal(PreviewEvent.Menu(second), preview.RightClicked(two.x, two.y));
                Assert.Equal(PreviewEvent.Menu(first), preview.RightClicked(one.x, one.y));
                // Off every card: nothing.
                Assert.Equal(PreviewEventKind.Nothing, preview.RightClicked(1, 1).Kind);
                // What a drag of files rests on.
                Assert.Equal(1, preview.CardAt(two.x, two.y));
                Assert.Equal(-1, preview.CardAt(1, 1));
                // The keys on a card, and off it again: nothing thrown, and a left click still names its window.
                preview.Select(1);
                preview.Select(-1);
                Assert.Equal(PreviewEvent.Activate(first), preview.Clicked(one.x, one.y, middle: false));
            }
            finally
            {
                _ = DestroyWindow(first);
                _ = DestroyWindow(second);
            }
        });
    }
}
