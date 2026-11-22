// Which keys the dock takes to be held (KeyState): the keys a test says it holds, on its own
// thread; nothing on a desktop that is not receiving the input; and Windows' own answer — the
// thread's key table — only on the desktop that is.
//
// The keyboard is stood in for with the thread's own key table (SetKeyboardState), which no other
// thread and no desktop sees, on a thread of the test's own, so nothing held here reaches the
// person's keyboard or another test.

using Karpo.Dock;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Menus;

public sealed unsafe class KeyStateTests
{
    /// <summary>A test's keys are the ones it says, for both questions, and only on its own thread.</summary>
    [Fact]
    public void ATestsKeysAreTheOnesItSaysOnItsOwnThread()
    {
        OnAThreadOfItsOwn(() =>
        {
            KeyState.Held = key => key == VK.VK_SHIFT;
            Assert.True(KeyState.Down(VK.VK_SHIFT));
            Assert.True(KeyState.DownNow(VK.VK_SHIFT));
            Assert.False(KeyState.Down(VK.VK_CONTROL));
            Assert.False(KeyState.DownNow(VK.VK_ESCAPE));
            Func<int, bool>? theirs = key => true;
            var other = new Thread(() => theirs = KeyState.Held);
            other.Start();
            other.Join();
            Assert.Null(theirs);
        });
    }

    /// <summary>
    /// Where the thread's desktop is not receiving the input, no key is held, whatever the keyboard
    /// says: a window there is pressed by nobody, and Windows is not asked. Where it is, the keyboard's
    /// answer is Windows' own.
    /// </summary>
    [Fact]
    public void OnADesktopWithoutTheInputNoKeyIsHeld()
    {
        OnAThreadOfItsOwn(() =>
        {
            ThisThreadsShift(down: true);
            try
            {
                InputDesktop.Holds = () => false;
                Assert.False(KeyState.Down(VK.VK_SHIFT));
                InputDesktop.Holds = () => true;
                Assert.True(KeyState.Down(VK.VK_SHIFT));
                // A test's own keys stand in for the keyboard's either way.
                KeyState.Held = _ => false;
                Assert.False(KeyState.Down(VK.VK_SHIFT));
            }
            finally
            {
                ThisThreadsShift(down: false);
                InputDesktop.Holds = InputDesktop.Here;
                KeyState.Held = null;
            }
        });
    }

    /// <summary>Shift down, or up, in this thread's own key table: what the keyboard reports to this thread and no other.</summary>
    private static void ThisThreadsShift(bool down)
    {
        var state = new byte[256];
        fixed (byte* keys = state)
        {
            Assert.True(GetKeyboardState(keys));
            keys[VK.VK_SHIFT] = keys[VK.VK_LSHIFT] = (byte)(down ? 0x80 : 0);
            Assert.True(SetKeyboardState(keys));
        }
    }

    /// <summary><paramref name="body"/> on a new thread, so its key table and its stand-ins go with it.</summary>
    private static void OnAThreadOfItsOwn(Action body)
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
        });
        thread.Start();
        thread.Join();
        if (failed is not null)
        {
            throw new Xunit.Sdk.XunitException("On the test's thread: " + failed);
        }
    }
}
