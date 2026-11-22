// §667: Karpo's answer to Windows' "where is this window's button?" on real windows (KARPO_UI_TESTS=1), the test's own on
// hidden desktops. Windows sends no such question on a desktop that is not the one shown (measured: nothing comes there
// as a window minimizes), so the test asks as Windows asks — WM_KLUDGEMINRECT, which it marshals between processes as
// Windows' own: the RECT goes in and comes back — and DefWindowProc turns it into HSHELL_GETMINRECT as it does for
// Windows'. Nothing of the person's is minimized, and nothing is registered on their desktop.
//
// - The hook on its own: what it answers is what it is told, written back into the RECT; a window it knows nothing of
//   keeps the RECT as it came; a dialog goes to its owner's button.
// - A bar's places as a real layout lays them out: a bar at the right at 150 %, its buttons, a folder, "…".
// - The real dock (Karpo.exe --stage on a desktop of its own): its own window answers, with the middle of its bar for a
//   window it shows no button for (a stage shows none of any window).

using System.Runtime.InteropServices;
using System.Text.Json;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using Karpo.Tests.Present;
using TerraFX.Interop.Windows;
using Xunit.Abstractions;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Docking;

public sealed unsafe class MinRectUiTests(ITestOutputHelper output)
{
    private static RECT R(int left, int top, int right, int bottom) => new() { left = left, top = top, right = right, bottom = bottom };

    private static string Show(RECT rect) => $"{rect.left},{rect.top}..{rect.right},{rect.bottom}";

    /// <summary>Explorer's answer as it came on the user's PC: its hidden taskbar's, below the bottom of the main display.</summary>
    private static readonly RECT Explorers = R(479, 2158, 480, 2230);

    /// <summary>Windows' question to <paramref name="hook"/> about <paramref name="window"/>, the RECT as it came before: the RECT it holds after.</summary>
    private static RECT Ask(HWND hook, HWND window, RECT before)
    {
        var rect = before;
        nuint result;
        Assert.True(SendMessageTimeoutW(hook, MinRect.WM_KLUDGEMINRECT, (WPARAM)(nint)window, (LPARAM)(nint)(&rect), SMTO_ABORTIFHUNG, 5000, &result) != 0,
            $"the hook did not answer: {Marshal.GetLastSystemError()}");
        return rect;
    }

    /// <summary>
    /// The hook on a hidden desktop, as the dock starts it, asked as Windows asks: about a window on a bar, its button; about
    /// a dialog, its owner's; about a window no bar shows, the middle of the bar; with no bars at all, Explorer's answer kept.
    /// Asked again after it registered again (a new Explorer), the same.
    /// </summary>
    [UiFact]
    public void TheHookAnswersAsWindowsAsks() => OnHiddenDesktop("minrect-hook", () =>
    {
        var app = Make("Static", WS.WS_OVERLAPPEDWINDOW | WS.WS_VISIBLE, HWND.NULL);
        var dialog = Make("Static", WS.WS_POPUP | WS.WS_CAPTION | WS.WS_VISIBLE, app);
        var other = Make("Static", WS.WS_OVERLAPPEDWINDOW | WS.WS_VISIBLE, HWND.NULL);
        var buttons = new WindowButtons();
        var button = R(3783, 66, 3834, 117);
        var hook = MinRect.Start(window => buttons.For((HWND)window));
        Assert.NotNull(hook);
        var asked = hook!.Hwnd;
        try
        {
            Assert.True(IsWindow(hook.Hwnd));
            // No bars yet: Explorer's answer stands.
            Assert.Equal(Show(Explorers), Show(Ask(hook.Hwnd, app, Explorers)));
            // The main display's bar at its right (as the user has it), its display wherever Windows has these windows.
            buttons.Publish(1, new BarButtons(Display(app), true, R(3777, 0, 3840, 2160), [new ButtonPlace(button, [(nint)app])]));
            Assert.Equal(Show(button), Show(Ask(hook.Hwnd, app, Explorers)));
            Assert.Equal(Show(button), Show(Ask(hook.Hwnd, dialog, Explorers)));
            Assert.Equal("3777,1048..3840,1111", Show(Ask(hook.Hwnd, other, Explorers)));
            // Registered again, last, as after a new Explorer: still the answer.
            hook.Again();
            Thread.Sleep(100);
            Assert.Equal(Show(button), Show(Ask(hook.Hwnd, app, Explorers)));
        }
        finally
        {
            hook.Dispose();
            _ = DestroyWindow(other);
            _ = DestroyWindow(dialog);
            _ = DestroyWindow(app);
        }
        Assert.False(IsWindow(asked), "the hook's window outlived it");
    });

    /// <summary>
    /// A bar at the right of a 3840x2160 display at 150 %, as the user's: each app button's windows go to the button as the
    /// layout places it on the screen, down the bar's side; a folder's to the folder's button; those of buttons the bar had
    /// no room for to "…".
    /// </summary>
    [UiFact]
    public void ABarsPlacesAreWhereItLaysOutItsButtons() => FolderButtonTests.OnStaThread(gfx =>
    {
        const uint dpi = 144;
        var display = R(0, 0, 3840, 2160);
        var config = new Config { Height = 42 };
        var look = new Look(Glass: false, Dark: true, Accent: Windows.UI.Color.FromArgb(255, 0, 0x78, 0xD4));
        var folderItem = FolderButtonTests.Folder(2);
        var folderKey = Config.IslandKey(null, folderItem.Id);
        var made = new IslandCollection();
        var folder = Karpo.Dock.Islands.Create(folderKey, folderItem, gfx, config);
        Assert.NotNull(folder);
        _ = folder!.Refresh(gfx);
        made[folderKey] = folder;
        try
        {
            var specs = new List<PartSpec> { new(Config.IslandKey(null, "apps"), BarKind.Apps), new(folderKey, folderItem.Kind) };
            Content Apps(int count) => new()
            {
                Tiles = [.. Enumerable.Range(0, count).Select(index => (new Item { Target = $"https://app{index}.example", Name = $"App {index}", Kind = ItemKind.Link, Keys = [] }, new Mark(1, false, false, default)))],
                TileParts = [.. Enumerable.Range(0, count).Select(_ => (string?)null)],
                Islands = made,
            };
            List<IReadOnlyList<nint>> Open(int count) => [.. Enumerable.Range(0, count).Select(index => (IReadOnlyList<nint>)[100 + index])];
            var folders = new Dictionary<string, IReadOnlyList<nint>>(StringComparer.OrdinalIgnoreCase) { [folderKey] = [7, 8] };

            var layout = new Layout(gfx, dpi, display, display, Edge.Right, config, look, specs, Apps(3));
            var laid = WindowButtons.Laid(layout, Open(3), folders, display, main: true);
            Assert.Equal(Show(layout.OnScreen(layout.PanelRect)), Show(laid.Panel));
            Assert.True(laid.Panel.left >= 3700 && laid.Panel.right == 3840, $"the bar is not at the right: {Show(laid.Panel)}");
            for (var index = 0; index < 3; index++)
            {
                var place = laid.Places[index];
                Assert.Equal<nint>([100 + index], place.Windows);
                Assert.Equal(Show(layout.OnScreen(layout.ItemRect(Hit.Tile(index)))), Show(place.Rect));
                // Down the side, inside the bar.
                Assert.True(place.Rect.left >= laid.Panel.left && place.Rect.right <= laid.Panel.right, $"button {index} at {Show(place.Rect)} is off the bar {Show(laid.Panel)}");
                Assert.Equal(Show(place.Rect), Show(WindowButtons.Of([laid], 100 + index, display)!.Value));
            }
            Assert.True(laid.Places[1].Rect.top >= laid.Places[0].Rect.bottom, "the buttons are not one below the other");
            var shelf = laid.Places.Single(place => place.Windows.Contains(7));
            Assert.Equal<nint>([7, 8], shelf.Windows);
            Assert.Equal(Show(layout.OnScreen(layout.BlockRect(layout.PartIndex(folderKey)))), Show(shelf.Rect));

            // Too many to fit down a short display: the rest at "…".
            var shortDisplay = R(0, 0, 3840, 700);
            var full = new Layout(gfx, dpi, shortDisplay, shortDisplay, Edge.Right, config, look, specs, Apps(40));
            Assert.NotEmpty(full.Overflowed);
            var fullLaid = WindowButtons.Laid(full, Open(40), folders, shortDisplay, main: true);
            var more = Show(full.OnScreen(full.ItemRect(Hit.AppOverflow)));
            foreach (var index in full.Overflowed)
            {
                Assert.Equal(more, Show(WindowButtons.Of([fullLaid], 100 + index, shortDisplay)!.Value));
            }
        }
        finally
        {
            folder.Dispose();
        }
    });

    /// <summary>
    /// The real dock (Karpo.exe --stage) on a made-up display at 150 %, a desktop of its own: its own window answers, last,
    /// as Windows asks. A stage lists no windows (Apps.Probe: none of the person's on a recording's bar), so a window of the
    /// test's has no button there and goes to the middle of the bar along the display's bottom — never what came in.
    /// </summary>
    [UiFact]
    public void TheDockAnswersFromItsOwnWindow()
    {
        var folder = Path.Combine(Path.GetTempPath(), "karpo-tests", "minrect-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(folder);
        var name = "karpo-minrect-" + Guid.NewGuid().ToString("N");
        HDESK desktop;
        fixed (char* chars = name)
        {
            desktop = CreateDesktopW(chars, null, null, 0, 0x10000000u /* GENERIC_ALL */, null);
        }
        Assert.True(desktop != HDESK.NULL, $"CreateDesktopW failed: {Marshal.GetLastSystemError()}");
        // The made-up display, 2560 by 1440 at 150 %, left of the main one (InputReachesTheBarsTests' own).
        var stage = R(-2560, 0, 0, 1440);
        InputReachesTheBarsTests.StageDock? dock = null;
        string? failed = null;
        var said = new List<string>();
        var thread = new Thread(() =>
        {
            HWND app = default;
            try
            {
                Assert.True(SetThreadDesktop(desktop));
                _ = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
                app = Make("Static", WS.WS_OVERLAPPEDWINDOW | WS.WS_VISIBLE, HWND.NULL, R(-2000, 200, -1200, 800), "Karpo min rect test app");
                dock = InputReachesTheBarsTests.StageDock.Start(name, folder, $"{stage.left},{stage.top},{stage.right - stage.left},{stage.bottom - stage.top},144");
                var bar = Bar(dock, folder);
                said.Add($"the bar's entries span {Show(bar)}");
                var hook = Hook(dock);
                var middle = Ask(hook, app, Explorers);
                said.Add($"the answer: {Show(middle)}");
                Assert.NotEqual(Show(Explorers), Show(middle));
                // As thick as the bar, centred along the made-up display's bottom, on the bar.
                var centre = (middle.left + middle.right) / 2;
                Assert.InRange(centre, ((stage.left + stage.right) / 2) - 2, ((stage.left + stage.right) / 2) + 2);
                Assert.Equal(stage.bottom, middle.bottom);
                Assert.True(middle.top <= bar.top && middle.top > stage.bottom - 120, $"the answer {Show(middle)} is not the bar's middle (its entries {Show(bar)})");
                Assert.Equal(middle.right - middle.left, middle.bottom - middle.top);
                Assert.True(dock.Running, "the dock ended");
            }
            catch (Exception error)
            {
                failed = error.ToString();
            }
            finally
            {
                if (app != HWND.NULL)
                {
                    _ = DestroyWindow(app);
                }
            }
        });
        thread.Start();
        thread.Join();
        dock?.Dispose();
        HiddenDesktop.Close(desktop);
        foreach (var line in said)
        {
            output.WriteLine(line);
        }
        Assert.True(failed is null, failed);
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>What the entries along the dock's bar span once it laid one out (stage-map.json, Stage.Mapped).</summary>
    private static RECT Bar(InputReachesTheBarsTests.StageDock dock, string folder)
    {
        var path = Path.Combine(folder, "stage-map.json");
        var until = Environment.TickCount64 + 60_000;
        while (Environment.TickCount64 < until)
        {
            Assert.True(dock.Running, "the dock ended before it laid out a bar");
            if (File.Exists(path))
            {
                try
                {
                    using var json = JsonDocument.Parse(File.ReadAllText(path));
                    var entries = json.RootElement.EnumerateArray().Select(entry => R(
                        entry.GetProperty("x").GetInt32(),
                        entry.GetProperty("y").GetInt32(),
                        entry.GetProperty("x").GetInt32() + entry.GetProperty("w").GetInt32(),
                        entry.GetProperty("y").GetInt32() + entry.GetProperty("h").GetInt32())).ToList();
                    if (entries.Count > 0)
                    {
                        // Laid out: its places published with it, as the map is written.
                        Pump(300);
                        return R(entries.Min(e => e.left), entries.Min(e => e.top), entries.Max(e => e.right), entries.Max(e => e.bottom));
                    }
                }
                catch (Exception error) when (error is IOException or JsonException or InvalidOperationException)
                {
                    // Being written.
                }
            }
            Pump(100);
        }
        throw new Xunit.Sdk.XunitException("the dock never laid out a bar");
    }
    /// <summary>The dock's own window that answers Windows' question, on this desktop.</summary>
    private static HWND Hook(InputReachesTheBarsTests.StageDock dock)
    {
        var until = Environment.TickCount64 + 10_000;
        while (Environment.TickCount64 < until)
        {
            var at = HWND.NULL;
            fixed (char* type = "Karpo.MinRect")
            {
                while ((at = FindWindowExW(HWND.NULL, at, type, null)) != HWND.NULL)
                {
                    uint pid = 0;
                    _ = GetWindowThreadProcessId(at, &pid);
                    if (pid == dock.Pid)
                    {
                        return at;
                    }
                }
            }
            Pump(100);
        }
        throw new Xunit.Sdk.XunitException("the dock has no window answering where a window's button is");
    }

    private static void Pump(int ms)
    {
        var until = Environment.TickCount64 + ms;
        while (Environment.TickCount64 < until)
        {
            _ = MsgWaitForMultipleObjects(0, null, BOOL.FALSE, (uint)Math.Max(1, until - Environment.TickCount64), QS.QS_ALLINPUT);
            MSG msg;
            while (PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_REMOVE))
            {
                _ = TranslateMessage(&msg);
                _ = DispatchMessageW(&msg);
            }
        }
    }

    /// <summary>The display <paramref name="window"/> is on, as Windows has it.</summary>
    private static RECT Display(HWND window)
    {
        var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        Assert.True(GetMonitorInfoW(MonitorFromWindow(window, (uint)MONITOR.MONITOR_DEFAULTTONEAREST), &info));
        return info.rcMonitor;
    }

    private static HWND Make(string type, uint style, HWND owner, RECT? at = null, string title = "", uint ex = 0)
    {
        var rect = at ?? R(100, 100, 500, 400);
        fixed (char* kind = type)
        fixed (char* text = title)
        {
            var window = CreateWindowExW(ex, kind, text, style, rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top, owner, HMENU.NULL, HINSTANCE.NULL, null);
            Assert.True(window != HWND.NULL, $"CreateWindowExW failed: {Marshal.GetLastSystemError()}");
            return window;
        }
    }

    private static void OnHiddenDesktop(string what, Action body)
    {
        string? failure = null;
        HDESK hidden = default;
        var thread = new Thread(() =>
        {
            try
            {
                fixed (char* name = "karpo-" + what + "-" + Guid.NewGuid().ToString("N"))
                {
                    hidden = CreateDesktopW(name, null, null, 0, 0x10000000u /* GENERIC_ALL */, null);
                }
                Assert.True(hidden != HDESK.NULL, $"CreateDesktopW failed: {Marshal.GetLastSystemError()}");
                Assert.True(SetThreadDesktop(hidden));
                body();
            }
            catch (Exception error)
            {
                failure = error.ToString();
            }
        });
        thread.Start();
        thread.Join();
        HiddenDesktop.Close(hidden);
        Assert.True(failure is null, failure);
    }
}
