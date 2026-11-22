// The menus' low-level mouse hook runs on a thread of its own and only posts (PressHook.cs): on the
// dock's thread, anything slow done while a menu was open held the pointer of the whole machine
// (the person, 2026-10-10: "when i click connect on a network the mouse freezes for a second or so").
// The hook itself is put in and taken out for real; the presses are handed to its handler (Heard),
// as the person's own can't be made here.

using System.Diagnostics;
using Karpo.Dock;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Menus;

public sealed unsafe class PressHookTests
{
    private static readonly TimeSpan Long = TimeSpan.FromSeconds(10);

    private static void Until(Func<bool> done)
    {
        var clock = Stopwatch.StartNew();
        while (!done() && clock.Elapsed < Long)
        {
            Thread.Sleep(5);
        }
        Assert.True(done());
    }

    /// <summary>Runs <paramref name="body"/> on a thread of its own, whose sink window goes with it.</summary>
    private static void OnThread(Action body)
    {
        Exception? failed = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception error)
            {
                failed = error;
            }
            finally
            {
                PressHook.Stop();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(Long));
        if (failed is not null)
        {
            throw new Xunit.Sdk.XunitException(failed.ToString());
        }
    }

    /// <summary>
    /// The hook is put in on its own thread, not the menu's; a press it hears is posted to the menu's
    /// thread, with where it was; a move is not; and the hook comes out with the last menu.
    /// </summary>
    [Fact]
    public void TheHookRunsOnAThreadOfItsOwnAndOnlyPosts() => OnThread(() =>
    {
        PressHook.Listen();
        Until(() => PressHook.Armed);
        Assert.NotEqual(0u, PressHook.HookThread);
        Assert.NotEqual(GetCurrentThreadId(), PressHook.HookThread);

        // Presses the person makes meanwhile come here too: only the made-up ones, at their made-up
        // points, are counted.
        PressHook.Heard(WM.WM_MOUSEMOVE, new POINT { x = -7, y = -9 });
        PressHook.Heard(WM.WM_LBUTTONUP, new POINT { x = -7, y = -9 });
        PressHook.Heard(WM.WM_RBUTTONDOWN, new POINT { x = -30, y = 40 });
        var heard = Posted();
        Assert.Contains(((uint)WM.WM_RBUTTONDOWN, -30, 40), heard);
        Assert.DoesNotContain(heard, press => (press.X, press.Y) == (-7, -9));

        // The last menu closed: nothing more is posted here.
        PressHook.Stop();
        PressHook.Heard(WM.WM_LBUTTONDOWN, new POINT { x = -30, y = 41 });
        Assert.DoesNotContain(Posted(), press => (press.X, press.Y) == (-30, 41));
    });

    /// <summary>The presses posted to this thread so far, taken.</summary>
    private static List<(uint Message, int X, int Y)> Posted()
    {
        var heard = new List<(uint, int, int)>();
        MSG msg;
        while (PeekMessageW(&msg, HWND.NULL, PressHook.WM_KARPO_PRESS, PressHook.WM_KARPO_PRESS, PM.PM_REMOVE))
        {
            var at = EditFollowHook.Unpack(msg.lParam);
            heard.Add(((uint)(nuint)msg.wParam, at.x, at.y));
        }
        return heard;
    }

    /// <summary>
    /// A menu over a folder uses up a press outside its windows (docs\widgets.md §185): on the
    /// folder, a bar, another app or nothing at all. Not one on the menu or a submenu of it, and not
    /// one while the menu stands on a desktop without the input — a test's never takes the
    /// person's presses.
    /// </summary>
    [Fact]
    public void AMenuOverAFolderUsesUpThePressesOutsideItsWindows()
    {
        nint menu = 0x100, submenu = 0x200, folder = 0x300, app = 0x400;
        var eater = new PressHook.Eater(default, [menu, submenu]);
        bool Receiving(PressHook.Eater _) => true;
        bool Elsewhere(PressHook.Eater _) => false;

        foreach (var outside in new[] { folder, app, 0 })
        {
            Assert.True(PressHook.UsesUp(outside, [eater], Receiving));
            Assert.False(PressHook.UsesUp(outside, [eater], Elsewhere));
            Assert.False(PressHook.UsesUp(outside, [], Receiving));
        }
        Assert.False(PressHook.UsesUp(menu, [eater], Receiving));
        Assert.False(PressHook.UsesUp(submenu, [eater], Receiving));
    }

    /// <summary>A used-up press's release is used up too: each down has its own button's up, and nothing else counts.</summary>
    [Fact]
    public void APressAndItsReleaseAreOneButton()
    {
        foreach (var (down, up) in new (uint, uint)[] { (WM.WM_LBUTTONDOWN, WM.WM_LBUTTONUP), (WM.WM_RBUTTONDOWN, WM.WM_RBUTTONUP), (WM.WM_MBUTTONDOWN, WM.WM_MBUTTONUP), (WM.WM_XBUTTONDOWN, WM.WM_XBUTTONUP) })
        {
            Assert.NotEqual(0u, PressHook.Button(down));
            Assert.Equal(PressHook.Button(down), PressHook.Button(up));
        }
        Assert.Equal(4, new uint[] { WM.WM_LBUTTONDOWN, WM.WM_RBUTTONDOWN, WM.WM_MBUTTONDOWN, WM.WM_XBUTTONDOWN }.Select(PressHook.Button).Distinct().Count());
        Assert.Equal(0u, PressHook.Button((uint)WM.WM_MOUSEMOVE));
        Assert.Equal(0u, PressHook.Button((uint)WM.WM_MOUSEWHEEL));
    }

    /// <summary>A press the hook used up says so to the menu's thread, which closes the menu for it wherever it was.</summary>
    [Fact]
    public void AUsedUpPressSaysSo() => OnThread(() =>
    {
        PressHook.Listen();
        PressHook.Heard(WM.WM_LBUTTONDOWN, new POINT { x = -31, y = 42 }, usedUp: true);
        PressHook.Heard(WM.WM_LBUTTONDOWN, new POINT { x = -31, y = 43 });
        var heard = Posted();
        Assert.Contains(((uint)WM.WM_LBUTTONDOWN | PressHook.UsedUpBit, -31, 42), heard);
        Assert.Contains(((uint)WM.WM_LBUTTONDOWN, -31, 43), heard);
    });

    /// <summary>
    /// The hook answers at once while the menu's thread is busy: a press is posted, never waited for,
    /// and the busy thread finds it when it looks again.
    /// </summary>
    [Fact]
    public void ABusyMenuThreadDoesNotHoldTheHook()
    {
        using var listening = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        var found = false;
        var busy = new Thread(() =>
        {
            try
            {
                PressHook.Listen();
                listening.Set();
                // As the dock's thread was, asking the WLAN service to connect.
                release.Wait(Long);
                MSG msg;
                while (PeekMessageW(&msg, HWND.NULL, PressHook.WM_KARPO_PRESS, PressHook.WM_KARPO_PRESS, PM.PM_REMOVE))
                {
                    found |= (uint)(nuint)msg.wParam == WM.WM_LBUTTONDOWN;
                }
            }
            finally
            {
                PressHook.Stop();
            }
        });
        busy.SetApartmentState(ApartmentState.STA);
        busy.Start();
        Assert.True(listening.Wait(Long));
        var clock = Stopwatch.StartNew();
        PressHook.Heard(WM.WM_LBUTTONDOWN, new POINT { x = 1, y = 2 });
        clock.Stop();
        Assert.True(clock.ElapsedMilliseconds < 100, $"the hook waited {clock.ElapsedMilliseconds} ms for a busy thread");
        release.Set();
        Assert.True(busy.Join(Long));
        Assert.True(found, "the busy thread never heard the press");
    }
}
