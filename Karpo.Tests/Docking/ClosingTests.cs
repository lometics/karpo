// What a close does to the dock's windows (Karpo.Dock\Closing.cs). Regression (2026-10-22): the dock
// quit cleanly six seconds before the PC slept and stayed down, karpo.log silent on why. Every bar
// took WM_CLOSE for quit, so Alt+F4 on a bar, or any program closing the windows it finds, quit all of
// Karpo. Real windows here, answered as the dock answers them (Closing.Answer, then Closing.Default for
// the rest), so DefWindowProc's own turn of SC_CLOSE into WM_CLOSE is part of what is held. The Shut
// Down Windows dialog is the tests' own counter: none opens.

using System.Runtime.InteropServices;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Docking;

public sealed unsafe class ClosingTests
{
    private const string ClassName = "KarpoTests.Closing";

    private static readonly Dictionary<nint, Closing.Role> Roles = [];
    private static int quits;
    private static int dialogs;

    [UiFact]
    public void AltF4OnABarAsksForTheShutDownDialogAndQuitsNothing()
    {
        Around(Closing.Role.Bar, bar =>
        {
            // Alt+F4 on the bar with the keys, as DefWindowProc makes it of the key (it only does so
            // for keys really pressed): SC_CLOSE, posted; and the system menu's Close, sent.
            _ = PostMessageW(bar, WM.WM_SYSCOMMAND, Closing.CloseCommand, 0);
            Pump(bar);
            Assert.Equal(1, dialogs);
            _ = SendMessageW(bar, WM.WM_SYSCOMMAND, Closing.CloseCommand | 0x2, 0);
            Assert.Equal(2, dialogs);
            Assert.Equal(0, quits);
            Assert.True(IsWindow(bar));
        });
    }

    [UiFact]
    public void ACloseSentToABarIsIgnored()
    {
        Around(Closing.Role.Bar, bar =>
        {
            _ = SendMessageW(bar, WM.WM_CLOSE, 0, 0);
            // Not even Karpo's own mark makes a bar's close a quit: only the host's is.
            _ = SendMessageW(bar, WM.WM_CLOSE, (uint)DockHost.QuitBy.Settings, 0);
            Assert.Equal(0, quits);
            Assert.Equal(0, dialogs);
            Assert.True(IsWindow(bar));
        });
    }

    /// <summary>A menu, a folder or an edit bar: Alt+F4 and a close reach its own handler, and its window stays.</summary>
    [UiFact]
    public void AnyOtherWindowIsNeverDestroyedByAClose()
    {
        Around(Closing.Role.Other, menu =>
        {
            _ = SendMessageW(menu, WM.WM_SYSCOMMAND, Closing.CloseCommand, 0);
            _ = SendMessageW(menu, WM.WM_CLOSE, 0, 0);
            Assert.Equal(0, quits);
            Assert.Equal(0, dialogs);
            Assert.True(IsWindow(menu));
        });
    }

    [UiFact]
    public void TheHostQuitsOnlyForKarposOwnCloseAndSaysWhoAsked()
    {
        Around(Closing.Role.Host, host =>
        {
            _ = SendMessageW(host, WM.WM_CLOSE, 0, 0);
            _ = SendMessageW(host, WM.WM_SYSCOMMAND, Closing.CloseCommand, 0);
            Assert.Equal(0, quits);
            Assert.Null(Closing.Asked);
            Assert.True(IsWindow(host));

            _ = SendMessageW(host, WM.WM_CLOSE, (uint)DockHost.QuitBy.Settings, 0);
            Assert.Equal(1, quits);
            Assert.Equal(DockHost.QuitBy.Settings, Closing.Asked);
            Assert.Contains(Closing.Line(QuitWay.Settings), File.ReadAllText(Troubles.Path));
            _ = SendMessageW(host, WM.WM_CLOSE, (uint)DockHost.QuitBy.Command, 0);
            Assert.Equal(2, quits);
            Assert.Equal(DockHost.QuitBy.Command, Closing.Asked);
            Assert.Contains(Closing.Line(QuitWay.Command), File.ReadAllText(Troubles.Path));
            // §335: the edit bar's Quit Karpo, the dock's own, posted to its host.
            _ = SendMessageW(host, WM.WM_CLOSE, (uint)DockHost.QuitBy.Menu, 0);
            Assert.Equal(3, quits);
            Assert.Equal(DockHost.QuitBy.Menu, Closing.Asked);
            Assert.Contains(Closing.Line(QuitWay.Menu), File.ReadAllText(Troubles.Path));
            Assert.Equal(0, dialogs);
        });
    }

    /// <summary>Each way out reads apart in karpo.log, and only Karpo's own marks are a way out.</summary>
    [Fact]
    public void EachWayOutHasALineOfItsOwn()
    {
        var lines = Enum.GetValues<QuitWay>().Select(Closing.Line).ToList();
        Assert.Equal(lines.Count, lines.Distinct().Count());
        Assert.Equal(QuitWay.Settings, Closing.Way((uint)DockHost.QuitBy.Settings));
        Assert.Equal(QuitWay.Command, Closing.Way((uint)DockHost.QuitBy.Command));
        Assert.Equal(QuitWay.Menu, Closing.Way((uint)DockHost.QuitBy.Menu));
        Assert.Null(Closing.Way(0));
        Assert.Null(Closing.Way(1));
    }

    /// <summary>
    /// The dock's exit code is who asked it to quit, never 0, and only the host's marked close sets
    /// it: a dock ended from outside, or by a WM_QUIT Karpo did not post, never gives the code its
    /// guard leaves down (DockExit, GuardPolicy).
    /// </summary>
    [Fact]
    public void TheExitCodeIsWhoAskedAndOnlyTheHostsMarkedCloseSetsIt()
    {
        var asked = Closing.Asked;
        try
        {
            Closing.Asked = null;
            Assert.Equal(0, Closing.ExitCode(0));
            Assert.Equal(DockExit.Fatal, Closing.ExitCode(DockExit.Fatal));
            foreach (var by in Enum.GetValues<DockHost.QuitBy>())
            {
                Closing.Asked = by;
                Assert.Equal((uint)by, (uint)Closing.ExitCode(0));
                Assert.Equal(ExitKind.Quit, DockExit.Kind((uint)Closing.ExitCode(DockExit.Fatal)));
            }
        }
        finally
        {
            Closing.Asked = asked;
        }

        // Set in Closing.Answer and nowhere else; the dock's exits return it.
        var sources = Directory.EnumerateFiles(Path.Combine(StringFiles.Repository, "Karpo.Dock"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(file), @"\bClosing\.Asked\s*=[^=]"))
            .Select(Path.GetFileName)
            .ToList();
        Assert.Empty(sources);
        var closing = File.ReadAllText(Path.Combine(StringFiles.Repository, "Karpo.Dock", "Closing.cs"));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(closing, @"\bAsked\s*=[^=]"));
        var program = File.ReadAllText(Path.Combine(StringFiles.Repository, "Karpo.Dock", "Program.cs"));
        Assert.Contains("return Closing.ExitCode(0);", program, StringComparison.Ordinal);
        Assert.Contains("return Closing.ExitCode(DockExit.Fatal);", program, StringComparison.Ordinal);
    }

    private static void Around(Closing.Role role, Action<HWND> test)
    {
        var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
        HWND hwnd;
        fixed (char* className = ClassName)
        {
            var type = new WNDCLASSW { lpfnWndProc = &Procedure, hInstance = instance, lpszClassName = className };
            _ = RegisterClassW(&type);
            hwnd = CreateWindowExW(WS.WS_EX_TOOLWINDOW | WS.WS_EX_NOACTIVATE, className, null, WS.WS_POPUP, 0, 0, 0, 0, HWND.NULL, HMENU.NULL, instance, null);
        }
        Assert.True(hwnd != HWND.NULL, $"CreateWindowExW failed: error {Marshal.GetLastSystemError()}");
        var shutDown = Closing.ShutDownDialog;
        lock (Roles)
        {
            Roles[hwnd] = role;
        }
        quits = 0;
        dialogs = 0;
        var asked = Closing.Asked;
        Closing.Asked = null;
        Closing.ShutDownDialog = () => dialogs++;
        try
        {
            test(hwnd);
        }
        finally
        {
            Closing.ShutDownDialog = shutDown;
            Closing.Asked = asked;
            lock (Roles)
            {
                Roles.Remove(hwnd);
            }
            _ = DestroyWindow(hwnd);
        }
    }

    /// <summary>What was posted to the window, handed to it.</summary>
    private static void Pump(HWND hwnd)
    {
        MSG msg;
        while (PeekMessageW(&msg, hwnd, 0, 0, PM.PM_REMOVE))
        {
            _ = TranslateMessage(&msg);
            _ = DispatchMessageW(&msg);
        }
    }

    [UnmanagedCallersOnly]
    private static LRESULT Procedure(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        Closing.Role? role;
        lock (Roles)
        {
            role = Roles.TryGetValue(hwnd, out var known) ? known : null;
        }
        return role is { } of && Closing.Answer(of, msg, wParam, () => quits++) is { } answered
            ? answered
            : Closing.Default(hwnd, msg, wParam, lParam);
    }
}
