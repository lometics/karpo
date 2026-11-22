// §658: an island's news reaches the dock as one WM_KARPO_ISLAND at a time (Karpo.Dock\Islands\Island.cs, Islands.Post): however
// much comes before the dock takes it, one message waits, and the one refresh reads it all; once taken (Islands.Taken, the
// dock's IslandReady) the next news posts again. A message per piece of news flooded the dock's queue — posted messages go
// before the keys and the mouse — and the first search's icons froze it (docs\widgets.md §658). A message-only window of the
// test's own stands for the dock's.

using Karpo.Dock;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Islands;

public sealed unsafe class IslandPostTests
{
    private static HWND Window()
    {
        fixed (char* cls = "STATIC")
        {
            var window = CreateWindowExW(0, cls, null, 0, 0, 0, 0, 0, HWND.HWND_MESSAGE, HMENU.NULL, HINSTANCE.NULL, null);
            Assert.NotEqual(HWND.NULL, window);
            return window;
        }
    }

    /// <summary>The tokens of the messages waiting now, taken as the dock takes them.</summary>
    private static uint[] Take(HWND window)
    {
        List<uint> taken = [];
        MSG msg;
        while (PeekMessageW(&msg, window, Karpo.Dock.Islands.WM_KARPO_ISLAND, Karpo.Dock.Islands.WM_KARPO_ISLAND, PM.PM_REMOVE))
        {
            Karpo.Dock.Islands.Taken((nint)window, (nuint)msg.wParam);
            taken.Add((uint)(nuint)msg.wParam);
        }
        return [.. taken.Order()];
    }

    [Fact]
    public void ManyPiecesOfNewsAreOneMessageUntilTheDockTakesIt()
    {
        var window = Window();
        try
        {
            for (var news = 0; news < 200; news++)
            {
                Assert.True(Karpo.Dock.Islands.Post((nint)window, 7));
                Assert.True(Karpo.Dock.Islands.Post((nint)window, 8));
            }
            Assert.Equal([7u, 8u], Take(window));
            // Taken: the next news posts again.
            Assert.True(Karpo.Dock.Islands.Post((nint)window, 7));
            Assert.Equal([7u], Take(window));
            Assert.Empty(Take(window));
        }
        finally
        {
            _ = DestroyWindow(window);
        }
    }
}
