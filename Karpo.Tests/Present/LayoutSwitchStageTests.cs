// §637: a layout switch on the real dock — Karpo.exe --stage on a desktop of its own (InputReachesTheBarsTests.StageDock), its
// config a file of its own — the display switched from a taskbar to a dock and back as another program's change of the file
// switches it (Dock.ConfigChanged, the very Apply the bar's menu runs): every bar window it showed before is the one it shows
// after and all along, none made or let go of (the window kept, a new scene laid out over the old, BarMorph.cs); with motion on
// the window's region its whole frame while the morph runs and the bar's own again once it is over (the morph's batch heard on
// the dock's thread). Needs a window station: KARPO_UI_TESTS=1.

using System.Runtime.InteropServices;
using System.Text;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Xunit.Abstractions;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Present;

public sealed unsafe class LayoutSwitchStageTests(ITestOutputHelper output)
{
    private const string Taskbar = "default";

    private const string Docked = "switch-dock";

    /// <summary>The stage's display shows <paramref name="layout"/>: Karpo's taskbar, or a dock of Start, the apps and the tray.</summary>
    private static string ConfigShowing(string layout)
    {
        var config = new Config();
        _ = config.DefaultLayout;
        config.Layouts.Add(new TaskbarLayout
        {
            Id = Docked,
            Name = "Dock",
            Type = LayoutType.Dock,
            Islands =
            [
                new BarItem { Id = "switch-start", Kind = BarKind.Start, Section = BarSection.Middle },
                new BarItem { Id = "switch-apps", Kind = BarKind.Apps, Section = BarSection.Middle },
                new BarItem { Id = "switch-tray", Kind = BarKind.Tray, Section = BarSection.End },
            ],
        });
        config.Screens = [new Screen { Id = "stage", Name = "Stage", ActiveLayoutId = layout == Taskbar ? config.DefaultLayout.Id : layout }];
        return config.ToJson();
    }

    [UiFact]
    public void ASwitchKeepsTheBarsWindowAndItsRegionComesBack()
    {
        var folder = Path.Combine(Path.GetTempPath(), "karpo-tests", "switch-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "config.json");
        File.WriteAllText(file, ConfigShowing(Taskbar));
        var name = "karpo-switch-" + Guid.NewGuid().ToString("N");
        HDESK desktop;
        fixed (char* chars = name)
        {
            desktop = CreateDesktopW(chars, null, null, 0, 0x10000000u /* GENERIC_ALL */, null);
        }
        Assert.True(desktop != HDESK.NULL, $"CreateDesktopW failed: {Marshal.GetLastSystemError()}");
        var dock = InputReachesTheBarsTests.StageDock.Start(name, folder);
        var said = new StringBuilder();
        Exception? failed = null;
        try
        {
            var thread = new Thread(() =>
            {
                try
                {
                    Assert.True(SetThreadDesktop(desktop), $"SetThreadDesktop failed: {Marshal.GetLastSystemError()}");
                    _ = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
                    Run(dock, file, said);
                }
                catch (Exception error)
                {
                    failed = error;
                }
            });
            thread.Start();
            thread.Join();
        }
        finally
        {
            dock.Dispose();
            HiddenDesktop.Close(desktop);
            output.WriteLine(said.ToString());
        }
        if (failed is not null)
        {
            throw new Xunit.Sdk.XunitException("On the dock's desktop: " + failed);
        }
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static void Run(InputReachesTheBarsTests.StageDock dock, string file, StringBuilder said)
    {
        var until = Environment.TickCount64 + 30_000;
        while (Bars().Count == 0 && Environment.TickCount64 < until)
        {
            Thread.Sleep(50);
        }
        Thread.Sleep(1500);
        var bars = Bars();
        Assert.NotEmpty(bars);
        said.AppendLine($"bars at rest: {string.Join(", ", bars.Select(bar => $"{(nint)bar:X} {Box(bar)}"))}");
        BOOL animated;
        _ = SystemParametersInfoW(SPI.SPI_GETCLIENTAREAANIMATION, 0, &animated, 0);
        foreach (var (layout, way) in new[] { (Docked, "taskbar to dock"), (Taskbar, "dock to taskbar") })
        {
            var rest = Box(bars[0]);
            File.WriteAllText(file, ConfigShowing(layout));
            var seen = new List<(long At, HashSet<HWND> Bars, RECT Box)>();
            var start = Environment.TickCount64;
            while (Environment.TickCount64 < start + 1500)
            {
                seen.Add((Environment.TickCount64 - start, [.. Bars()], Box(bars[0])));
                Thread.Sleep(4);
            }
            Assert.True(dock.Running, "the dock ended");
            // Every bar window the same all along: none made, none let go of.
            var others = seen.Where(look => !look.Bars.SetEquals(bars)).ToList();
            Assert.True(others.Count == 0, $"{way}: other bar windows at {string.Join(", ", others.Select(look => $"{look.At} ms: {string.Join(" ", look.Bars.Select(bar => ((nint)bar).ToString("X")))}"))}");
            var boxes = seen.Select(look => look.Box).Distinct().ToList();
            said.AppendLine($"{way}: the region {string.Join(" → ", boxes.Select(Text))}");
            var end = seen[^1].Box;
            Assert.NotEqual(Text(rest), Text(end));
            if (animated)
            {
                // Its whole frame while the morph runs, the bar's own again once it is over.
                var grown = seen.FindIndex(look => Area(look.Box) > Math.Max(Area(rest), Area(end)));
                Assert.True(grown >= 0, $"{way}: the region never held the whole frame for the morph");
                var back = seen.FindIndex(grown, look => Text(look.Box) == Text(end));
                Assert.True(back > grown, $"{way}: the region never came back to the bar's own");
                said.AppendLine($"{way}: the region whole from {seen[grown].At} ms, the bar's own again at {seen[back].At} ms");
            }
        }
    }

    /// <summary>The dock's bar windows in sight on this thread's desktop.</summary>
    private static List<HWND> Bars()
    {
        var found = new List<HWND>();
        fixed (char* name = "karpo.dock")
        {
            for (var window = FindWindowExW(HWND.NULL, HWND.NULL, name, null); window != HWND.NULL; window = FindWindowExW(HWND.NULL, window, name, null))
            {
                if (IsWindowVisible(window))
                {
                    found.Add(window);
                }
            }
        }
        return found;
    }

    /// <summary><paramref name="window"/>'s region's box (client px); the window's own where it has none.</summary>
    private static RECT Box(HWND window)
    {
        RECT box;
        if (GetWindowRgnBox(window, &box) is SIMPLEREGION or COMPLEXREGION)
        {
            return box;
        }
        _ = GetClientRect(window, &box);
        return box;
    }

    private static long Area(RECT rect) => (long)(rect.right - rect.left) * (rect.bottom - rect.top);

    private static string Text(RECT rect) => $"{rect.left},{rect.top}..{rect.right},{rect.bottom}";
}
