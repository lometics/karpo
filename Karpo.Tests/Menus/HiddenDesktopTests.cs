// The desktops the offscreen tests and the smoke draw on, and why they are closed where they are
// (Karpo.Dock\HiddenDesktop.cs).
//
// Every harness used to close its desktop from the thread that was on it, in a finally that threw
// the answer away. CloseDesktop refuses that (ERROR_BUSY), so no desktop closed before its test
// process ended: a full KARPO_UI_TESTS run held some ninety at once. Each is now closed by the
// thread that started the one on it, after joining it — except the
// one Windows' text services make their threads on, the first time the process engages text input,
// which stays until the process ends whoever closes it.

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Menus;

public sealed unsafe partial class HiddenDesktopTests
{
    private const uint DesktopReadObjects = 0x0001;
    private const int FileNotFound = 2;

    /// <summary>Whether a desktop of that name exists in this window station, asked without holding it.</summary>
    private static bool Exists(string name)
    {
        HDESK desktop;
        fixed (char* chars = name)
        {
            desktop = OpenDesktopW(chars, 0, BOOL.FALSE, DesktopReadObjects);
        }
        if (desktop == HDESK.NULL)
        {
            Assert.Equal(FileNotFound, Marshal.GetLastSystemError());
            return false;
        }
        _ = CloseDesktop(desktop);
        return true;
    }

    /// <summary>
    /// A thread on a hidden desktop, as every harness makes one: it makes the desktop, moves onto
    /// it and puts a window there. Returns the desktop and what closing it from that thread said,
    /// when <paramref name="closeOnIt"/> asks — the old way.
    /// </summary>
    private static (HDESK Desktop, bool ClosedOnIt, int Error) OnOne(string name, bool closeOnIt)
    {
        HDESK hidden = default;
        var closedOnIt = false;
        var error = 0;
        var thread = new Thread(() =>
        {
            fixed (char* chars = name)
            {
                hidden = CreateDesktopW(chars, null, null, 0, 0x10000000u /* GENERIC_ALL */, null);
            }
            Assert.True(hidden != HDESK.NULL, "no hidden desktop");
            Assert.True(SetThreadDesktop(hidden));
            HWND window;
            fixed (char* type = "Static")
            {
                window = CreateWindowExW(0, type, null, WS.WS_POPUP, 0, 0, 10, 10, HWND.NULL, HMENU.NULL, HINSTANCE.NULL, null);
            }
            Assert.True(window != HWND.NULL);
            _ = DestroyWindow(window);
            if (closeOnIt)
            {
                closedOnIt = CloseDesktop(hidden);
                error = Marshal.GetLastSystemError();
            }
        });
        thread.Start();
        thread.Join();
        return (hidden, closedOnIt, error);
    }

    /// <summary>The reason for all of it: a thread can't close the desktop it is on.</summary>
    [UiFact]
    public void A_thread_cannot_close_the_desktop_it_is_on()
    {
        var name = "karpo-close-test-" + Guid.NewGuid().ToString("N");

        var (desktop, closedOnIt, error) = OnOne(name, closeOnIt: true);

        Assert.False(closedOnIt);
        Assert.Equal(HiddenDesktop.Busy, error);
        Assert.True(Exists(name), "refused, it is still there after its thread has ended");
        HiddenDesktop.Close(desktop);
        Assert.False(Exists(name));
    }

    /// <summary>
    /// The regression: the way every harness closed its desktop left it standing until the test
    /// process ended; the way they close it now leaves nothing behind once the test has run.
    /// </summary>
    [UiFact]
    public void A_hidden_desktop_is_gone_once_its_test_has_run()
    {
        var before = "karpo-close-test-" + Guid.NewGuid().ToString("N");
        var now = "karpo-close-test-" + Guid.NewGuid().ToString("N");

        // Before: closed from the thread on it, the answer thrown away.
        var (left, _, _) = OnOne(before, closeOnIt: true);
        var leftBehind = Exists(before);
        HiddenDesktop.Close(left);

        // Now: closed from the thread that joined that one.
        var (made, _, _) = OnOne(now, closeOnIt: false);
        HiddenDesktop.Close(made);

        Assert.True(leftBehind, "the old way left its desktop standing");
        Assert.False(Exists(now), "the desktop was still there after its test had run");
    }

    [UiFact]
    public void Closing_a_desktop_never_made_does_nothing() => HiddenDesktop.Close(HDESK.NULL);

    /// <summary>
    /// The one desktop left to Windows' text services (whose threads, made the first time the
    /// process engages text input, move onto that thread's desktop and stay) must not become a way
    /// to let anything else go quietly. A thread of our own on it — as any harness thread is — keeps
    /// it: while that thread lives the close is refused out loud and says where it started, and once
    /// it has gone the desktop closes.
    /// </summary>
    [UiFact]
    public void A_desktop_our_own_thread_still_holds_is_not_let_go_of_quietly()
    {
        var name = "karpo-close-test-" + Guid.NewGuid().ToString("N");
        using var release = new ManualResetEventSlim();
        using var onIt = new ManualResetEventSlim();
        Thread? lingering = null;
        HDESK hidden = default;
        var thread = new Thread(() =>
        {
            fixed (char* chars = name)
            {
                hidden = CreateDesktopW(chars, null, null, 0, 0x10000000u /* GENERIC_ALL */, null);
            }
            Assert.True(hidden != HDESK.NULL, "no hidden desktop");
            Assert.True(SetThreadDesktop(hidden));
            lingering = new Thread(() =>
            {
                // A thread starts on the process's own desktop, not its maker's: it moves, as the
                // text services' threads do.
                Assert.True(SetThreadDesktop(hidden));
                onIt.Set();
                release.Wait();
            });
            lingering.Start();
            onIt.Wait();
        });
        thread.Start();
        thread.Join();

        var refused = Assert.Throws<Karpo.Dock.Native.NativeException>(() => HiddenDesktop.Close(hidden));
        Assert.Contains("ERROR_BUSY", refused.Message);
        Assert.Contains("started in", refused.Message);
        Assert.True(Exists(name));

        release.Set();
        lingering!.Join();
        HiddenDesktop.Close(hidden);
        Assert.False(Exists(name));
    }

    /// <summary>
    /// And no harness can drift back: whatever makes a hidden desktop closes it through
    /// <see cref="HiddenDesktop.Close"/> and never calls CloseDesktop itself. (This file is the
    /// exception, and says why: it shows the old way failing.)
    /// </summary>
    [Fact]
    public void Every_harness_closes_its_desktop_through_the_one_helper()
    {
        var repo = Repository();
        if (repo.Length == 0)
        {
            return;
        }
        var sources = Directory.EnumerateFiles(Path.Combine(repo, "Karpo.Tests"), "*.cs", SearchOption.AllDirectories)
            .Append(Path.Combine(repo, "Karpo.Dock", "Smoke.cs"))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.EndsWith("HiddenDesktopTests.cs", StringComparison.Ordinal));
        var makers = 0;
        foreach (var path in sources)
        {
            var code = File.ReadAllText(path);
            if (!MakesADesktop().IsMatch(code))
            {
                continue;
            }
            makers++;
            var name = Path.GetFileName(path);
            Assert.False(ClosesOnItsOwn().IsMatch(code), $"{name} calls CloseDesktop itself: close through HiddenDesktop.Close, from the thread that joined the one on the desktop");
            Assert.Contains("HiddenDesktop.Close(", code);
        }
        // 13 since the web search box's offscreen tests went with the widget (2026-10-11).
        Assert.True(makers >= 13, $"the harnesses and the smoke make hidden desktops; found {makers}");
    }

    [GeneratedRegex(@"\bCreateDesktopW\(")]
    private static partial Regex MakesADesktop();

    [GeneratedRegex(@"(?<![\w.])CloseDesktop\(")]
    private static partial Regex ClosesOnItsOwn();

    /// <summary>The repository, when these tests run from inside it; empty otherwise.</summary>
    private static string Repository()
    {
        for (var up = new DirectoryInfo(AppContext.BaseDirectory); up is not null; up = up.Parent)
        {
            if (File.Exists(Path.Combine(up.FullName, "Karpo.slnx")))
            {
                return up.FullName;
            }
        }
        return "";
    }
}
