// Input reaches the bars (docs\widgets.md §360): no window of Karpo's that shows nothing takes the
// pointer anywhere, and every bar gets its clicks.
//
// §359 put a bar's tooltip in a window of its own (BarTip), as large as the bar's window and standing
// on top of it, and counted on WS_EX_TRANSPARENT to let the pointer through. Without WS_EX_LAYERED that
// style lets nothing through: WindowFromPoint, and the mouse with it, found the tooltip's window over the
// whole bar. Hovering a button showed its tooltip, the tooltip's window took the pointer, the bar heard
// the pointer leave and took the tooltip away, the window went, the bar heard the pointer again and
// showed it again: no press reached a bar. Every hidden-desktop test passed, for none asked Windows
// what is under a point.
//
// So this asks it, of the real dock: Karpo.exe --stage on a desktop of its own (Stage.cs: no mutex, no
// appbar, no taskbar, a config of its own), at 150 % on a display left of the main one as the person's
// second display stands. A thread on that desktop asks WindowFromPoint over every entry along every bar
// (stage-map.json) and over a grid of the display away from the bars — at rest, the pointer moving over
// a button (its tooltip coming and going), a folder open and closed, a menu open and closed, edit mode
// on and off: over a bar, the bar's window; away from what is open, never a window of Karpo's.
// And of one bar in this process (BarTip with its scene): its tooltip up, every point of the bar is the bar's.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Xunit.Abstractions;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Present;

public sealed unsafe class InputReachesTheBarsTests(ITestOutputHelper output)
{
    /// <summary>The bars' window class (Dock.ClassName).</summary>
    private const string BarClass = "karpo.dock";

    /// <summary>The made-up display: 2560 by 1440 at 150 %, left of the main one, as the person's second display stands.</summary>
    private static readonly RECT Display = new() { left = -2560, top = 0, right = 0, bottom = 1440 };

    private const uint Dpi = 144;

    private const int Timeout = 60_000;

    [UiFact]
    public void InputReachesTheBars()
    {
        var folder = Path.Combine(Path.GetTempPath(), "karpo-tests", "input-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(folder);
        // A config to run on where one is given (the person's, copied: KARPO_INPUT_CONFIG), else what a first start makes.
        if (Environment.GetEnvironmentVariable("KARPO_INPUT_CONFIG") is { Length: > 0 } given && File.Exists(given))
        {
            File.Copy(given, Path.Combine(folder, "config.json"));
        }
        var name = "karpo-input-" + Guid.NewGuid().ToString("N");
        HDESK desktop;
        fixed (char* chars = name)
        {
            desktop = CreateDesktopW(chars, null, null, 0, 0x10000000u /* GENERIC_ALL */, null);
        }
        Assert.True(desktop != HDESK.NULL, $"CreateDesktopW failed: {Marshal.GetLastSystemError()}");
        var dock = StageDock.Start(name, folder);
        var said = new StringBuilder();
        Exception? failed = null;
        try
        {
            var thread = new Thread(() =>
            {
                try
                {
                    Assert.True(SetThreadDesktop(desktop), $"SetThreadDesktop failed: {Marshal.GetLastSystemError()}");
                    // In the dock's px, as it is (per monitor): a thread that is not would see every window scaled.
                    _ = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
                    new Probe(dock, said).Run();
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
            if (File.Exists(Path.Combine(folder, "local", "karpo.log")))
            {
                output.WriteLine(File.ReadAllText(Path.Combine(folder, "local", "karpo.log")));
            }
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

    /// <summary>
    /// One bar and its tooltip's own window (BarTip), as the dock stands them: the tooltip up over each
    /// button in turn, and gone — over every point of the bar's panel WindowFromPoint finds the bar's window,
    /// and the tooltip's window is under no point at all.
    /// </summary>
    [UiFact]
    public void ATooltipsWindowTakesNoPointer()
    {
        RetainedBarTests.OnHiddenDesktop("input-tip", (gfx, instance) =>
        {
            using var bar = new RetainedBarTests.Bar(gfx, instance, tips: true);
            bar.Lay(RetainedBarTests.Parts(), RetainedBarTests.Things(3), RetainedBarTests.Still, RetainedBarTests.Islands);
            bar.Clip();
            FrameFilm.Settle();
            var wrong = new List<string>();
            void Check(string when)
            {
                FrameFilm.Settle();
                var panel = bar.OnScreen(bar.Layout.PanelRect, inset: 1);
                for (var x = panel.left; x < panel.right; x += 5)
                {
                    for (var y = panel.top; y < panel.bottom; y += 3)
                    {
                        var under = WindowFromPoint(new POINT { x = x, y = y });
                        if (under != bar.Hwnd)
                        {
                            wrong.Add($"{when}: at {x},{y} {Describe(under)}, not the bar");
                            return;
                        }
                    }
                }
                // The tooltip's window only where the tooltip and its shadow are.
                var tip = bar.Scene.TipRect is { } shown ? bar.OnScreen(shown) : default;
                const int Shadow = 24;
                for (var x = bar.Frame.left; x < bar.Frame.right; x += 5)
                {
                    for (var y = bar.Frame.top; y < bar.Frame.bottom; y += 3)
                    {
                        var onTip = x >= tip.left - Shadow && x < tip.right + Shadow && y >= tip.top - Shadow && y < tip.bottom + Shadow;
                        if (!onTip && WindowFromPoint(new POINT { x = x, y = y }) == bar.TipHwnd)
                        {
                            wrong.Add($"{when}: at {x},{y} the tooltip's window, away from the tooltip at {Text(tip)}");
                            return;
                        }
                    }
                }
            }
            Check("at rest");
            for (var tile = 0; tile < 3; tile++)
            {
                var item = bar.Layout.ItemRect(Hit.Tile(tile));
                bar.Scene.Tooltip(gfx, $"App {tile}", bar.Layout.HitTest(item.Center.X, item.Center.Y));
                FrameFilm.Settle();
                Assert.True(IsWindowVisible(bar.TipHwnd), "the tooltip's window is not up");
                Check($"the tooltip up over button {tile}");
            }
            bar.Scene.Tooltip(gfx, null, null);
            Check("the tooltip gone");
            Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
        });
    }

    // --- The real dock ------------------------------------------------------------------------------------

    /// <summary>Karpo.exe --stage on the desktop, its config <c>folder</c> (§361: ArrangedFolderStandsTests runs it too).</summary>
    internal sealed class StageDock : IDisposable
    {
        private PROCESS_INFORMATION started;

        public required string Folder { get; init; }

        public uint Pid => started.dwProcessId;

        public HANDLE Process => started.hProcess;

        /// <summary>
        /// The dock on <paramref name="desktop"/>, its config in <paramref name="folder"/>, on the made-up display
        /// (<see cref="Display"/>), or on <paramref name="displays"/> where given (each x,y,w,h,dpi; Stage.cs).
        /// </summary>
        public static StageDock Start(string desktop, string folder, string? displays = null)
        {
            var dll = typeof(Karpo.Dock.Dock).Assembly.Location;
            var beside = Path.GetDirectoryName(dll)!;
            var stage = $"--stage {displays ?? $"{Display.left},{Display.top},{Display.right - Display.left},{Display.bottom - Display.top},{Dpi}"}";
            var tests = typeof(InputReachesTheBarsTests).Assembly.Location;
            // A build of its own where one is named (KARPO_INPUT_EXE: the published, native one), else this one's.
            var line = Environment.GetEnvironmentVariable("KARPO_INPUT_EXE") is { Length: > 0 } exe && File.Exists(exe)
                ? $"\"{exe}\" {stage}"
                : File.Exists(Path.Combine(beside, "Karpo.runtimeconfig.json"))
                ? $"\"{DotNet()}\" exec \"{dll}\" {stage}"
                : $"\"{DotNet()}\" exec --runtimeconfig \"{Path.ChangeExtension(tests, ".runtimeconfig.json")}\" --depsfile \"{Path.ChangeExtension(tests, ".deps.json")}\" \"{dll}\" {stage}";
            // Its own config and local folder, whatever this process has (Isolation.cs gives the tests theirs).
            var environment = new StringBuilder();
            foreach (var entry in Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>().OrderBy(entry => (string)entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!string.Equals((string)entry.Key, "KARPO_CONFIG_DIR", StringComparison.OrdinalIgnoreCase))
                {
                    environment.Append($"{entry.Key}={entry.Value}\0");
                }
            }
            environment.Append($"KARPO_CONFIG_DIR={folder}\0\0");
            var where = "WinSta0\\" + desktop;
            var dock = new StageDock { Folder = folder };
            fixed (char* command = line)
            fixed (char* on = where)
            fixed (char* block = environment.ToString())
            {
                var startup = new STARTUPINFOW { cb = (uint)sizeof(STARTUPINFOW), lpDesktop = on };
                PROCESS_INFORMATION info;
                // CREATE_NO_WINDOW | CREATE_UNICODE_ENVIRONMENT
                Assert.True(
                    CreateProcessW(null, command, null, null, BOOL.FALSE, 0x08000000u | 0x00000400u, block, null, &startup, &info),
                    $"CreateProcessW failed: {Marshal.GetLastSystemError()}");
                dock.started = info;
            }
            return dock;
        }

        /// <summary>Whether it still runs.</summary>
        public bool Running => WaitForSingleObject(started.hProcess, 0) == WAIT.WAIT_TIMEOUT;

        public void Dispose()
        {
            if (started.hProcess != HANDLE.NULL)
            {
                // A stage run holds nothing of the person's: no appbar, no taskbar, no tray (Stage.cs).
                _ = TerminateProcess(started.hProcess, 0);
                _ = WaitForSingleObject(started.hProcess, 5000);
                _ = CloseHandle(started.hProcess);
                _ = CloseHandle(started.hThread);
                started = default;
            }
        }

        private static string DotNet()
        {
            if (Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host && File.Exists(host))
            {
                return host;
            }
            return Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", "dotnet.exe"));
        }
    }

    /// <summary>A window of the dock's, as a thread on its desktop sees it.</summary>
    private sealed record Win(HWND Hwnd, string Class, string Title, bool Visible, RECT Rect, RECT? Region, uint ExStyle)
    {
        /// <summary>What of it takes the pointer, on the screen: its region's box, else the window.</summary>
        public RECT Takes => Region is { } box
            ? new RECT { left = Rect.left + box.left, top = Rect.top + box.top, right = Rect.left + box.right, bottom = Rect.top + box.bottom }
            : Rect;

        public override string ToString() =>
            $"{(nint)Hwnd:X} {Class} \"{Title}\" {(Visible ? "shown" : "hidden")} {Rect.left},{Rect.top}..{Rect.right},{Rect.bottom}"
            + $" region {(Region is { } r ? $"{r.left},{r.top}..{r.right},{r.bottom}" : "none")} ex 0x{ExStyle:X}";
    }

    /// <summary>An entry along a bar (stage-map.json): what it is and its rectangle on the screen.</summary>
    private sealed record Entry(string Hit, string Kind, RECT Rect)
    {
        public POINT Center => new() { x = (Rect.left + Rect.right) / 2, y = (Rect.top + Rect.bottom) / 2 };
    }

    /// <summary>The checks, on a thread on the dock's desktop.</summary>
    private sealed class Probe(StageDock dock, StringBuilder said)
    {
        private readonly List<string> wrong = [];

        public void Run()
        {
            var entries = Entries();
            said.AppendLine($"{entries.Count} entries along the bars");
            Settle(1500);
            Check("at rest", entries, mayTake: []);
            var bars = Windows().Where(win => win.Visible && win.Class == BarClass).ToList();
            Assert.NotEmpty(bars);
            // Each entry's bar, as it stands at rest.
            var barOf = entries.Distinct().ToDictionary(entry => entry, entry => BarAt(entry.Center));

            // The pointer moving over each button in turn, as the mouse moves it: tooltips come and go. A
            // tooltip's window up takes the pointer only where the tooltip is, never on a bar.
            var tips = 0;
            foreach (var entry in entries.Where(entry => entry.Hit is "Tile" or "Start" or "Tray" or "Island").Take(6))
            {
                var bar = barOf[entry];
                var until = Environment.TickCount64 + 600;
                while (Environment.TickCount64 < until)
                {
                    Post(bar, WM.WM_MOUSEMOVE, entry.Center);
                    Pump(15);
                    var when = $"the pointer on {entry.Hit} {entry.Kind} at {entry.Center.x},{entry.Center.y}";
                    // Their middles only: a tooltip comes and goes faster than every pixel of every entry is asked.
                    Over(entries, when, whole: false);
                    foreach (var tip in Windows().Where(win => win.Visible && win.Title == "Karpo tooltip"))
                    {
                        tips++;
                        foreach (var over in bars.Where(other => Meets(other.Takes, tip.Takes)))
                        {
                            wrong.Add($"{when}: the tooltip's window takes {Text(tip.Takes)}, over its bar's {Text(over.Takes)}");
                        }
                    }
                }
            }
            // Or nothing above asked anything of a tooltip's window.
            Assert.True(tips > 0, "no tooltip came up");
            Settle(800);
            Check("the pointer gone", entries, mayTake: []);

            // A folder on a bar opened, then closed by a press on it again.
            if (entries.FirstOrDefault(entry => entry.Kind.StartsWith("Folder", StringComparison.Ordinal)) is { } folder)
            {
                var bar = barOf[folder];
                var before = Windows().Where(win => win.Visible).Select(win => win.Hwnd).ToHashSet();
                List<Win> opened = [];
                // A dock still settling after its start may let a first click go: asked again, twice at most.
                for (var tries = 0; tries < 3 && !opened.Any(win => win.Class != BarClass); tries++)
                {
                    Click(bar, folder.Center);
                    Settle(800);
                    opened = [.. Windows().Where(win => win.Visible && !before.Contains(win.Hwnd))];
                }
                said.AppendLine($"the folder opened: {string.Join("; ", opened)}");
                Assert.Contains(opened, win => win.Class != BarClass);
                Check("a folder open", entries, mayTake: opened);
                Click(bar, folder.Center);
                Settle(800);
                Check("the folder closed", entries, mayTake: []);
            }

            // The bar's menu, opened by a right click on it, then closed by Escape.
            {
                var entry = entries.First(entry => entry.Hit is "Tile" or "Start" or "Panel" or "Island");
                var bar = barOf[entry];
                var before = Windows().Where(win => win.Visible).Select(win => win.Hwnd).ToHashSet();
                Post(bar, WM.WM_RBUTTONDOWN, entry.Center);
                Post(bar, WM.WM_RBUTTONUP, entry.Center);
                Settle(800);
                var opened = Windows().Where(win => win.Visible && !before.Contains(win.Hwnd)).ToList();
                said.AppendLine($"the menu opened: {string.Join("; ", opened)}");
                Assert.Contains(opened, win => win.Class == Flyout.ClassName);
                Check("a menu open", entries, mayTake: opened);
                foreach (var menu in opened)
                {
                    _ = PostMessageW(menu.Hwnd, WM.WM_KEYDOWN, VK.VK_ESCAPE, 0);
                }
                Settle(800);
                Check("the menu closed", entries, mayTake: []);
            }

            // Edit mode, as a second start asks for it (⚙ Settings: edit mode, §520 the edit bar's settings panel open).
            if (Host() is { } host)
            {
                _ = PostMessageW(host, SecondStart.Message, 0, 0);
                Settle(1200);
                var shown = Windows().Where(win => win.Visible && win.Class != BarClass).ToList();
                said.AppendLine($"edit mode: {string.Join("; ", shown)}");
                // The dims take what is pressed on the desktop in edit mode; over the bars, the bars still.
                Over(entries, "edit mode");
                Assert.Contains(shown, win => win.Title == "Karpo edit dim");
                // §520: a second start opens the settings panel, which is the edit bar (a karpo.flyout, Dock.EditDeck.cs),
                // no menu of its own. Escape, from the bar that has the keys in it (Dock.EditKey), from the inside out
                // (§520 decision 4, EditKeys.Escape): the panel folds first and edit mode stays, then edit mode ends. (A
                // real Escape on the panel is heard by the keyboard hook, EditFollowHook, as the bar's; a posted one is not.)
                void Escape()
                {
                    foreach (var bar in bars)
                    {
                        _ = PostMessageW(bar.Hwnd, WM.WM_KEYDOWN, VK.VK_ESCAPE, 0);
                    }
                    Settle(1200);
                }
                Escape();
                Assert.Contains(Windows(), win => win.Visible && win.Title == "Karpo edit dim");
                Over(entries, "the panel folded");
                Escape();
                Assert.DoesNotContain(Windows(), win => win.Visible && win.Title == "Karpo edit dim");
                Check("edit mode left", entries, mayTake: []);
            }

            said.AppendLine("windows at the end:");
            foreach (var win in Windows())
            {
                said.AppendLine("  " + win);
            }
            Assert.True(dock.Running, "the dock ended");
            Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong.Distinct().Take(40)));
        }

        /// <summary>The bar's window under <paramref name="at"/>, as it stands at rest.</summary>
        private static HWND BarAt(POINT at)
        {
            var under = GetAncestor(WindowFromPoint(at), GA.GA_ROOT);
            Assert.Equal(BarClass, ClassOf(under));
            return under;
        }

        /// <summary>A press and its release at <paramref name="at"/> (screen px), posted to <paramref name="bar"/>.</summary>
        /// <remarks>
        /// In one go, as the queue then has them: the cursor is not on this desktop, so a bar told the pointer
        /// came hears it leave straight after (TrackMouseEvent), and a release once it left clicks nothing.
        /// </remarks>
        private static void Click(HWND bar, POINT at)
        {
            Post(bar, WM.WM_MOUSEMOVE, at);
            Post(bar, WM.WM_LBUTTONDOWN, at);
            Post(bar, WM.WM_MOUSEMOVE, at);
            Post(bar, WM.WM_LBUTTONUP, at);
        }

        private static void Post(HWND window, uint message, POINT screen)
        {
            RECT rect;
            _ = GetWindowRect(window, &rect);
            var (x, y) = (screen.x - rect.left, screen.y - rect.top);
            var keys = message is WM.WM_LBUTTONDOWN ? 0x0001u : message is WM.WM_RBUTTONDOWN ? 0x0002u : 0u;
            _ = PostMessageW(window, message, keys, (LPARAM)(nint)((y << 16) | (x & 0xFFFF)));
        }

        /// <summary>
        /// Every check of a moment: over every entry, its bar; over the display away from the bars and from
        /// <paramref name="mayTake"/>'s regions (what was opened), never a window of the dock's.
        /// </summary>
        private void Check(string when, List<Entry> entries, List<Win> mayTake)
        {
            var windows = Windows();
            said.AppendLine($"{when}:");
            foreach (var win in windows.Where(win => win.Visible))
            {
                said.AppendLine("  " + win);
            }
            Over(entries, when);
            // No bar disabled, and at rest nothing holding the mouse: a press goes where it is.
            foreach (var bar in windows.Where(win => win.Visible && win.Class == BarClass))
            {
                if (!IsWindowEnabled(bar.Hwnd))
                {
                    wrong.Add($"{when}: the bar {bar} disabled");
                }
                var info = new GUITHREADINFO { cbSize = (uint)sizeof(GUITHREADINFO) };
                if (mayTake.Count == 0 && GetGUIThreadInfo(GetWindowThreadProcessId(bar.Hwnd, null), &info) && info.hwndCapture != HWND.NULL)
                {
                    wrong.Add($"{when}: the mouse held by {Describe(info.hwndCapture)}");
                }
            }
            var allowed =windows.Where(win => win.Visible && win.Class == BarClass).Concat(mayTake).Select(win => win.Takes).ToList();
            for (var x = Display.left + 7; x < Display.right; x += 61)
            {
                for (var y = Display.top + 5; y < Display.bottom; y += 47)
                {
                    if (allowed.Any(rect => x >= rect.left && x < rect.right && y >= rect.top && y < rect.bottom))
                    {
                        continue;
                    }
                    var under = WindowFromPoint(new POINT { x = x, y = y });
                    if (Ours(under))
                    {
                        wrong.Add($"{when}: at {x},{y} {Describe(under)}, which shows nothing there");
                    }
                }
            }
        }

        /// <summary>
        /// Over every entry along the bars, the bar's window — over all of it, every third pixel each way
        /// (§370): a window over part of an entry, its middle clear, went unasked.
        /// </summary>
        private void Over(List<Entry> entries, string when, bool whole = true)
        {
            foreach (var entry in entries)
            {
                if (NotItsBar(entry, whole) is var (at, under))
                {
                    wrong.Add($"{when}: over {entry.Hit} {entry.Kind} at {at.x},{at.y} {Describe(under)}, not its bar");
                }
            }
        }

        /// <summary>The first point of <paramref name="entry"/> (all of it, or its middle) where the window is not a bar's, and that window; null where there is none.</summary>
        private static (POINT At, HWND Under)? NotItsBar(Entry entry, bool whole)
        {
            var (step, from) = whole ? (3, new POINT { x = entry.Rect.left, y = entry.Rect.top }) : (int.MaxValue / 2, entry.Center);
            for (var y = from.y; y < entry.Rect.bottom; y += step)
            {
                for (var x = from.x; x < entry.Rect.right; x += step)
                {
                    var at = new POINT { x = x, y = y };
                    var under = GetAncestor(WindowFromPoint(at), GA.GA_ROOT);
                    if (ClassOf(under) != BarClass)
                    {
                        return (at, under);
                    }
                }
            }
            return null;
        }

        private bool Ours(HWND window)
        {
            uint pid = 0;
            _ = GetWindowThreadProcessId(window, &pid);
            return window != HWND.NULL && pid == dock.Pid;
        }

        /// <summary>The dock's top-level windows on this desktop.</summary>
        private List<Win> Windows()
        {
            var found = new List<Win>();
            var at = HWND.NULL;
            while ((at = FindWindowExW(HWND.NULL, at, null, null)) != HWND.NULL)
            {
                if (!Ours(at))
                {
                    continue;
                }
                RECT rect;
                _ = GetWindowRect(at, &rect);
                var region = CreateRectRgn(0, 0, 0, 0);
                RECT? box = null;
                if (GetWindowRgn(at, region) != 0)
                {
                    RECT got;
                    _ = GetRgnBox(region, &got);
                    box = got;
                }
                _ = DeleteObject(region);
                found.Add(new Win(at, ClassOf(at), TitleOf(at), IsWindowVisible(at), rect, box, (uint)GetWindowLongPtrW(at, GWL.GWL_EXSTYLE)));
            }
            return found;
        }

        private HWND? Host()
        {
            var at = HWND.NULL;
            fixed (char* name = DockHost.ClassName)
            {
                while ((at = FindWindowExW(HWND.NULL, at, name, null)) != HWND.NULL)
                {
                    if (Ours(at))
                    {
                        return at;
                    }
                }
            }
            return null;
        }

        /// <summary>Every entry along the bars, once the dock has written them (Stage.Mapped).</summary>
        private List<Entry> Entries()
        {
            var path = Path.Combine(dock.Folder, "stage-map.json");
            var until = Environment.TickCount64 + Timeout;
            while (Environment.TickCount64 < until)
            {
                Assert.True(dock.Running, "the dock ended before it laid out a bar");
                if (File.Exists(path))
                {
                    try
                    {
                        using var json = JsonDocument.Parse(File.ReadAllText(path));
                        var entries = json.RootElement.EnumerateArray().Select(entry => new Entry(
                            entry.GetProperty("hit").GetString()!,
                            entry.GetProperty("kind").GetString()!,
                            new RECT
                            {
                                left = entry.GetProperty("x").GetInt32(),
                                top = entry.GetProperty("y").GetInt32(),
                                right = entry.GetProperty("x").GetInt32() + entry.GetProperty("w").GetInt32(),
                                bottom = entry.GetProperty("y").GetInt32() + entry.GetProperty("h").GetInt32(),
                            })).Where(entry => entry.Rect.right - entry.Rect.left > 4 && entry.Rect.bottom - entry.Rect.top > 4).ToList();
                        if (entries.Count > 0)
                        {
                            return entries;
                        }
                    }
                    catch (Exception error) when (error is IOException or JsonException)
                    {
                        // Being written.
                    }
                }
                Pump(100);
            }
            throw new Xunit.Sdk.XunitException("the dock never laid out a bar");
        }

        /// <summary>Waits <paramref name="ms"/>, this thread's messages handled meanwhile.</summary>
        private static void Pump(int ms)
        {
            var until = Environment.TickCount64 + ms;
            while (Environment.TickCount64 < until)
            {
                _ = MsgWaitForMultipleObjects(0, null, BOOL.FALSE, (uint)Math.Max(1, until - Environment.TickCount64), QS.QS_ALLINPUT);
                MSG msg;
                while (PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_REMOVE))
                {
                    TranslateMessage(&msg);
                    DispatchMessageW(&msg);
                }
            }
        }

        private static void Settle(int ms) => Pump(ms);
    }

    private static string ClassOf(HWND window)
    {
        if (window == HWND.NULL)
        {
            return "";
        }
        var name = stackalloc char[128];
        var length = GetClassNameW(window, name, 128);
        return new string(name, 0, length);
    }

    private static bool Meets(RECT a, RECT b) => a.left < b.right && b.left < a.right && a.top < b.bottom && b.top < a.bottom;

    private static string Text(RECT r) => $"{r.left},{r.top}..{r.right},{r.bottom}";

    private static string TitleOf(HWND window)
    {
        var title = stackalloc char[128];
        var length = GetWindowTextW(window, title, 128);
        return new string(title, 0, length);
    }

    private static string Describe(HWND window)
    {
        if (window == HWND.NULL)
        {
            return "nothing";
        }
        RECT rect;
        _ = GetWindowRect(window, &rect);
        return $"{ClassOf(window)} \"{TitleOf(window)}\" ({rect.left},{rect.top}..{rect.right},{rect.bottom}, ex 0x{(uint)GetWindowLongPtrW(window, GWL.GWL_EXSTYLE):X})";
    }
}
