// edit: the clicks and Escape that put down or call off a widget following the pointer, heard by
// hooks wherever they land (Karpo.Dock\EditFollowHook.cs).
//
// The bug (the person, 2026-10-07): picked from the add list, the widget "gets stuck. the esc also
// does not work and placing with left click does also not work." The add list, a menu, had the
// foreground; at the pick another app got it and took the mouse from the bar carrying the widget,
// which let the block go but left the widget following, in the list, with nothing to move it, put
// it down or call it off. Held here:
//
//  - the rule: the left press puts it down, the right press and Escape call it off, all three kept
//    from what is under the pointer, and their releases kept too; the release of a button down
//    before the follow began goes on as it went down; once the follow is over, nothing more is
//    taken, but a release still owed is kept;
//  - in edit mode (round 6: Escape did not leave it), Escape with nothing following is
//    posted as the end of edit mode, wherever the keys are — unless a popup of the dock's own has
//    them, which closes on it itself;
//  - the hook posts what it heard to the dock's window, through the real queue, with where the
//    press was; a press on another desktop is let by, and whether a desktop has the input is each
//    thread's own to stand in for.
//
// SetWindowsHookEx itself is not exercised: that needs the person's own input, and hooks put in by
// a test would take the person's clicks while it runs. The dock's half —
// the posted message landing the widget, or taking it out of the list again — needs the whole dock
// on a desktop, which no test stands up.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe class EditFollowHookTests
{
    [Fact]
    public void ALeftPressPutsItDownAndItAndItsReleaseAreKept()
    {
        var gesture = new FollowGesture { Following = true };
        Assert.Equal((true, FollowInput.Place), gesture.Mouse(WM.WM_LBUTTONDOWN));
        Assert.True(gesture.Owes);
        // The dock puts it down from the posted message, before the button comes up.
        gesture.Following = false;
        Assert.Equal((true, FollowInput.None), gesture.Mouse(WM.WM_LBUTTONUP));
        Assert.False(gesture.Owes);
        // Over: the next click is the window's under it.
        Assert.Equal((false, FollowInput.None), gesture.Mouse(WM.WM_LBUTTONDOWN));
        Assert.Equal((false, FollowInput.None), gesture.Mouse(WM.WM_LBUTTONUP));
    }

    [Fact]
    public void ARightPressCallsItOffAndItsReleaseIsKept()
    {
        var gesture = new FollowGesture { Following = true };
        Assert.Equal((true, FollowInput.Cancel), gesture.Mouse(WM.WM_RBUTTONDOWN));
        gesture.Following = false;
        Assert.Equal((true, FollowInput.None), gesture.Mouse(WM.WM_RBUTTONUP));
        Assert.False(gesture.Owes);
    }

    [Fact]
    public void EscapeCallsItOffOnceHeldDownOrNot()
    {
        var gesture = new FollowGesture { Following = true };
        Assert.Equal((true, FollowInput.Cancel), gesture.Key(WM.WM_KEYDOWN, VK.VK_ESCAPE));
        // Held down, it repeats: kept, and nothing more.
        Assert.Equal((true, FollowInput.None), gesture.Key(WM.WM_KEYDOWN, VK.VK_ESCAPE));
        gesture.Following = false;
        Assert.Equal((true, FollowInput.None), gesture.Key(WM.WM_KEYDOWN, VK.VK_ESCAPE));
        Assert.Equal((true, FollowInput.None), gesture.Key(WM.WM_KEYUP, VK.VK_ESCAPE));
        Assert.False(gesture.Owes);
        Assert.Equal((false, FollowInput.None), gesture.Key(WM.WM_KEYDOWN, VK.VK_ESCAPE));
    }

    [Fact]
    public void OtherKeysButtonsAndMovesGoOn()
    {
        var gesture = new FollowGesture { Following = true };
        Assert.Equal((false, FollowInput.None), gesture.Key(WM.WM_KEYDOWN, VK.VK_TAB));
        Assert.Equal((false, FollowInput.None), gesture.Key(WM.WM_KEYUP, VK.VK_RETURN));
        Assert.Equal((false, FollowInput.None), gesture.Mouse(WM.WM_MOUSEMOVE));
        Assert.Equal((false, FollowInput.None), gesture.Mouse(WM.WM_MBUTTONDOWN));
        Assert.Equal((false, FollowInput.None), gesture.Mouse(WM.WM_MOUSEWHEEL));
        Assert.False(gesture.Owes);
    }

    /// <summary>
    /// A button down before the follow began comes up while a widget follows: its press was not
    /// taken, so its release goes on to the window under it, as it went down — kept, it would leave
    /// the button down for good.
    /// </summary>
    [Fact]
    public void TheReleaseOfAPressMadeBeforeTheFollowGoesOn()
    {
        var gesture = new FollowGesture { Following = true };
        Assert.Equal((false, FollowInput.None), gesture.Mouse(WM.WM_LBUTTONUP));
        Assert.Equal((false, FollowInput.None), gesture.Key(WM.WM_KEYUP, VK.VK_ESCAPE));
        // The next click, then, puts it down.
        Assert.Equal((true, FollowInput.Place), gesture.Mouse(WM.WM_LBUTTONDOWN));
    }

    [Fact]
    public void WhereThePressWasSurvivesTheMessage()
    {
        foreach (var at in new[] { new POINT { x = 0, y = 0 }, new POINT { x = 1919, y = 1079 }, new POINT { x = -1920, y = -40 }, new POINT { x = 5120, y = -1 } })
        {
            var back = EditFollowHook.Unpack(EditFollowHook.Pack(at));
            Assert.Equal((at.x, at.y), (back.x, back.y));
        }
    }

    [Fact]
    public void TheFollowsMessageIsItsOwn()
    {
        Assert.NotEqual(FlyoutHost.WM_KARPO_HOST_DISMISS, EditFollowHook.WM_KARPO_FOLLOW);
        Assert.InRange<uint>(EditFollowHook.WM_KARPO_FOLLOW, WM.WM_APP, 0xBFFF);
    }

    /// <summary>
    /// What the hook hears reaches the dock's window through the real queue: a left press posts
    /// Place with where it was, a right press and Escape post Cancel, and each is kept from what is
    /// under the pointer; once released, a click goes on and posts nothing.
    /// </summary>
    [UiFact]
    public void WhatTheHookHearsIsPostedToTheDocksWindow()
    {
        var window = MessageWindow();
        using var hook = new EditFollowHook(HINSTANCE.NULL, () => window);
        try
        {
            InputDesktop.Holds = () => true;
            hook.FollowWithoutHooks();
            try
            {
                Assert.True(hook.HearMouse(WM.WM_LBUTTONDOWN, new POINT { x = -300, y = 700 }));
                var (input, at) = Posted(window);
                Assert.Equal(FollowInput.Place, input);
                Assert.Equal((-300, 700), (at.x, at.y));
                Assert.True(hook.HearMouse(WM.WM_LBUTTONUP, new POINT { x = -300, y = 700 }));
                Assert.Null(TryPosted(window));

                Assert.True(hook.HearMouse(WM.WM_RBUTTONDOWN, new POINT { x = 10, y = 10 }));
                Assert.Equal(FollowInput.Cancel, Posted(window).Input);
                Assert.True(hook.HearMouse(WM.WM_RBUTTONUP, new POINT { x = 10, y = 10 }));

                Assert.True(hook.HearKey(WM.WM_KEYDOWN, VK.VK_ESCAPE));
                Assert.Equal(FollowInput.Cancel, Posted(window).Input);
                Assert.True(hook.HearKey(WM.WM_KEYUP, VK.VK_ESCAPE));

                // Put down: a press owed still has its release kept, then nothing more is taken.
                Assert.True(hook.HearMouse(WM.WM_LBUTTONDOWN, new POINT { x = 5, y = 5 }));
                _ = Posted(window);
                hook.Release();
                Assert.True(hook.Owes);
                Assert.True(hook.HearMouse(WM.WM_LBUTTONUP, new POINT { x = 5, y = 5 }));
                Assert.False(hook.HearMouse(WM.WM_LBUTTONDOWN, new POINT { x = 5, y = 5 }));
                Assert.False(hook.HearKey(WM.WM_KEYDOWN, VK.VK_ESCAPE));
                Assert.Null(TryPosted(window));
            }
            finally
            {
                hook.Dispose();
            }
        }
        finally
        {
            InputDesktop.Holds = InputDesktop.Here;
            _ = DestroyWindow(window);
        }
    }

    /// <summary>
    /// In edit mode with nothing following, the hook posts Escape as the end of edit mode, keeps it
    /// and its release, and lets every other key by; where a popup of the dock's own has the keys it
    /// lets Escape by too. Out of edit mode, nothing.
    /// </summary>
    [UiFact]
    public void EscapeInEditModeIsPostedAsItsEnd()
    {
        var window = MessageWindow();
        var popup = false;
        using var hook = new EditFollowHook(HINSTANCE.NULL, () => window, () => popup);
        try
        {
            InputDesktop.Holds = () => true;
            hook.EditWithoutHooks(true);
            Assert.True(hook.HearKey(WM.WM_KEYDOWN, VK.VK_ESCAPE));
            Assert.Equal(FollowInput.Leave, Posted(window).Input);
            Assert.True(hook.Owes);
            Assert.True(hook.HearKey(WM.WM_KEYUP, VK.VK_ESCAPE));
            Assert.False(hook.Owes);
            Assert.False(hook.HearKey(WM.WM_KEYDOWN, VK.VK_RETURN));
            Assert.Null(TryPosted(window));

            // A menu of the dock's has the keys: it closes on Escape itself.
            popup = true;
            Assert.False(hook.HearKey(WM.WM_KEYDOWN, VK.VK_ESCAPE));
            Assert.False(hook.HearKey(WM.WM_KEYUP, VK.VK_ESCAPE));
            Assert.Null(TryPosted(window));

            // A widget follows: Escape calls it off, whoever has the keys.
            hook.FollowWithoutHooks();
            Assert.True(hook.HearKey(WM.WM_KEYDOWN, VK.VK_ESCAPE));
            Assert.Equal(FollowInput.Cancel, Posted(window).Input);
            Assert.True(hook.HearKey(WM.WM_KEYUP, VK.VK_ESCAPE));

            // Out of edit mode, Escape is the window's with the keys.
            hook.Release();
            hook.EditWithoutHooks(false);
            popup = false;
            Assert.False(hook.HearKey(WM.WM_KEYDOWN, VK.VK_ESCAPE));
            Assert.Null(TryPosted(window));
            Assert.False(hook.HearsKeys);
        }
        finally
        {
            InputDesktop.Holds = InputDesktop.Here;
            hook.Dispose();
            _ = DestroyWindow(window);
        }
    }

    /// <summary>
    /// §490: the Escape Karpo sends Windows' menu to close it (WindowsPanels.EscapeTaps, marked as Karpo's own) is let by:
    /// nothing posted, nothing kept, nothing owed — it was closing the add list and leaving edit mode as it opened over
    /// Windows' menu. A person's Escape is edit mode's still.
    /// </summary>
    [UiFact]
    public void KarposOwnEscapeIsLetBy()
    {
        var window = MessageWindow();
        using var hook = new EditFollowHook(HINSTANCE.NULL, () => window, () => false);
        try
        {
            InputDesktop.Holds = () => true;
            hook.EditWithoutHooks(true);
            Assert.False(hook.HearKey(WM.WM_KEYDOWN, VK.VK_ESCAPE, TaskbarKeys.Ours));
            Assert.False(hook.HearKey(WM.WM_KEYUP, VK.VK_ESCAPE, TaskbarKeys.Ours));
            Assert.Null(TryPosted(window));
            Assert.False(hook.Owes);
            Assert.True(hook.HearKey(WM.WM_KEYDOWN, VK.VK_ESCAPE));
            Assert.Equal(FollowInput.Leave, Posted(window).Input);
            Assert.True(hook.HearKey(WM.WM_KEYUP, VK.VK_ESCAPE));
        }
        finally
        {
            InputDesktop.Holds = InputDesktop.Here;
            hook.Dispose();
            _ = DestroyWindow(window);
        }
    }

    /// <summary>A press on a desktop other than the bar's is let by: nothing is posted, nothing kept.</summary>
    [UiFact]
    public void APressOnAnotherDesktopIsLetBy()
    {
        var window = MessageWindow();
        using var hook = new EditFollowHook(HINSTANCE.NULL, () => window);
        try
        {
            InputDesktop.Holds = () => false;
            hook.FollowWithoutHooks();
            Assert.False(hook.HearMouse(WM.WM_LBUTTONDOWN, new POINT { x = 5, y = 5 }));
            Assert.False(hook.HearKey(WM.WM_KEYDOWN, VK.VK_ESCAPE));
            Assert.False(hook.HearMouse(WM.WM_LBUTTONUP, new POINT { x = 5, y = 5 }));
            Assert.Null(TryPosted(window));
        }
        finally
        {
            InputDesktop.Holds = InputDesktop.Here;
            hook.Dispose();
            _ = DestroyWindow(window);
        }
    }

    /// <summary>
    /// Whether a desktop has the input is each thread's own to stand in for: a test saying so on its
    /// thread leaves every other thread's hooks — another test's, the dock's — with Windows' answer.
    /// </summary>
    [Fact]
    public void WhetherADesktopHasTheInputIsEachThreadsOwnToStandInFor()
    {
        Func<bool> mine = () => true;
        InputDesktop.Holds = mine;
        try
        {
            Func<bool>? theirs = null;
            var thread = new Thread(() => theirs = InputDesktop.Holds);
            thread.Start();
            thread.Join();
            Assert.Equal(InputDesktop.Here, theirs);
            Assert.Same(mine, InputDesktop.Holds);
        }
        finally
        {
            InputDesktop.Holds = InputDesktop.Here;
        }
    }

    /// <summary>A message-only window of this thread's, standing for the dock's.</summary>
    private static HWND MessageWindow()
    {
        fixed (char* cls = "STATIC")
        {
            var window = CreateWindowExW(0, cls, null, 0, 0, 0, 0, 0, HWND.HWND_MESSAGE, HMENU.NULL, HINSTANCE.NULL, null);
            Assert.NotEqual(HWND.NULL, window);
            return window;
        }
    }

    private static (FollowInput Input, POINT At) Posted(HWND window) =>
        TryPosted(window) ?? throw new Xunit.Sdk.XunitException("nothing was posted to the dock's window");

    private static (FollowInput Input, POINT At)? TryPosted(HWND window)
    {
        MSG msg;
        if (!PeekMessageW(&msg, window, EditFollowHook.WM_KARPO_FOLLOW, EditFollowHook.WM_KARPO_FOLLOW, PM.PM_REMOVE))
        {
            return null;
        }
        return ((FollowInput)(uint)(nuint)msg.wParam, EditFollowHook.Unpack(msg.lParam));
    }
}
