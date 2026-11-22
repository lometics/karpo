// Every opener toggles what it opens (docs\widgets.md §430). The person, 2026-10-30: "the add button to add a widget,
// when the menu is already open and i click again it reopens it and flickers. it should just close it / toggle. this
// should be for all the menus. make sure this is the case".
//
// Asked of the real dock, Karpo.exe --stage on a desktop of its own (InputReachesTheBarsTests.StageDock), driven as
// the mouse drives it: each press goes first to the popups' mouse hook — posted to the dock's sink as the hook posts
// it (PressHook.StagedBit: the stage's desktop never has the input, so no hook hears it) — then to the window the
// pointer is over, or the one holding the mouse; its release a moment later, as a hand lets go. For every opener: a
// click opens its popup; a click on it again closes it, and it stays closed — filmed, it never shows again between;
// a click on another opener while one is open opens that one, the first gone, never a frame with neither.

using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Xunit.Abstractions;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Present;

public sealed unsafe class MenuToggleTests(ITestOutputHelper output)
{
    /// <summary>The bar's folders: faces that open them, at rest and in edit mode.</summary>
    private static readonly string[] Folders = ["toggle-one", "toggle-two"];

    /// <summary>Where the films go: the coordinator's folder for this work, else KARPO_PNG_DIR.</summary>
    private static string Pictures => Environment.GetEnvironmentVariable("KARPO_TOGGLE_DIR") is { Length: > 0 } named ? Directory.CreateDirectory(named).FullName : Offscreen.Pictures;

    /// <summary>
    /// At rest: a folder's face, the bar's own right click, a face's right click — each clicked again closes what it
    /// opened, and stays closed; one folder's face while the other's folder is open opens that one in one step.
    /// </summary>
    [UiFact]
    public void EveryOpenerOnTheBarTogglesItsPopup() => Drive(probe => probe.AtRest());

    /// <summary>
    /// In edit mode: a dropdown of the settings panel (§520), "Add" (the person's case), the bar's "+", "Add" into an open
    /// folder, a line's right click, the errors' indicator — each clicked again closes its popup and it stays closed;
    /// another opener clicked while one stands opens its own in one step.
    /// </summary>
    [UiFact]
    public void EveryOpenerInEditModeTogglesItsMenu() => Drive(probe => probe.InEditMode());

    private void Drive(Action<Probe> run)
    {
        var folder = Path.Combine(Path.GetTempPath(), "karpo-tests", "toggle-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(folder);
        var config = new Config();
        var start = BarKind.Start.New();
        start.Section = BarSection.Start;
        config.DefaultLayout.Islands.Add(start);
        config.DefaultLayout.Islands.Add(Readings(Folders[0], BarSection.Middle));
        config.DefaultLayout.Islands.Add(Readings(Folders[1], BarSection.End));
        foreach (var kind in new[] { BarKind.Tray, BarKind.Indicators, BarKind.Volume })
        {
            var made = kind.New();
            made.Section = BarSection.End;
            config.DefaultLayout.Islands.Add(made);
        }
        // §394: a line whose feed nobody answers for: an error on the edit bar, its indicator an opener too.
        var feed = ListKind.Rss.New();
        feed.Id = Folders[1] + "-feed";
        feed.Rss = new RssOptions { Address = "http://127.0.0.1:9/karpo-feed.xml", Title = "Nobody's news" };
        ((BarItem)config.DefaultLayout.Islands.First(island => island.Id == Folders[1])).Folder!.Put(feed);
        Sections.Order(config.DefaultLayout.Islands);
        File.WriteAllText(Path.Combine(folder, "config.json"), config.ToJson());
        var name = "karpo-toggle-" + Guid.NewGuid().ToString("N");
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
                    run(new Probe(dock, said));
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

    /// <summary>A list folder <paramref name="id"/> of three lines that only read: a clock, the battery, a heading.</summary>
    private static BarItem Readings(string id, BarSection section)
    {
        var options = new FolderOptions();
        var island = BarKind.FolderList.New();
        island.Id = id;
        island.Folder = options;
        island.Section = section;
        foreach (var (line, kind) in new[] { ("clock", ListKind.Clock), ("battery", ListKind.Battery) })
        {
            var made = kind.New();
            made.Id = $"{id}-{line}";
            options.Put(made);
        }
        var title = ListKind.Heading.New();
        title.Id = $"{id}-heading";
        title.Heading = new HeadingOptions { Text = "Notes" };
        options.Put(title);
        return island;
    }

    /// <summary>A thing to press on (stage-map.json, stage-edit.json): what it is and where, in screen px.</summary>
    private sealed record Spot(string Hit, int Index, string Key, RECT Rect, string Kind = "")
    {
        public POINT Center => new() { x = (Rect.left + Rect.right) / 2, y = (Rect.top + Rect.bottom) / 2 };
    }

    /// <summary>What the screen showed of the popups at one moment: which windows of theirs were in sight.</summary>
    private sealed record Moment(double At, string Shown);

    /// <summary>The drive and the checks, on a thread on the dock's desktop.</summary>
    private sealed class Probe(InputReachesTheBarsTests.StageDock dock, StringBuilder said)
    {
        private readonly List<string> wrong = [];

        /// <summary>Every window seen so far in stage-z.json, with its stratum.</summary>
        private readonly Dictionary<HWND, Stratum> strata = [];

        /// <summary>What counts as open: a popup and a menu; an arranged folder too where a folder's face is toggled in edit mode.</summary>
        private Stratum[] watched = [Stratum.Popup, Stratum.Menu];

        private IEnumerable<HWND> Popups => strata.Where(entry => watched.Contains(entry.Value) && !standing.Contains(entry.Key)).Select(entry => entry.Key);

        /// <summary>What stood open before an opener was clicked, that it has nothing to do with (one Escape left behind).</summary>
        private HashSet<HWND> standing = [];

        /// <summary>Each opener from nothing open: what Escape does not close is left out of what it opens.</summary>
        private void Afresh()
        {
            Close();
            standing = [];
            standing = [.. Shown()];
            if (standing.Count > 0)
            {
                said.AppendLine($"  left standing: {string.Join(", ", standing.Select(Describe))}");
            }
        }

        // --- The sweeps -----------------------------------------------------------------------------------

        public void AtRest()
        {
            var bar = Read("stage-map.json", wait: true);
            Pump(1500);
            Spot Face(string id) => bar.First(spot => spot.Key.EndsWith("/" + id, StringComparison.Ordinal));
            said.AppendLine($"the bar: {string.Join("; ", bar.Select(spot => $"{spot.Hit} {spot.Kind} {spot.Key} {Text(spot.Rect)}"))}");

            // A folder's face: open, closed by it again; another's while one is open.
            Toggle("rest-folder", Face(Folders[0]).Center, Folders[0]);
            Switch("rest-folder-to-folder", Face(Folders[0]).Center, Face(Folders[1]).Center);
            Close();

            // The volume's face, and the other faces of the bar that open something.
            foreach (var spot in bar.Where(spot => spot.Hit is "Island" or "TrayOverflow" or "AppOverflow" or "Start" && !Folders.Any(id => spot.Key.EndsWith("/" + id, StringComparison.Ordinal))))
            {
                Toggle($"rest-{spot.Hit}-{spot.Kind}-{spot.Key.Split('/')[^1]}", spot.Center, null, optional: true);
            }

            // Right clicks: the bar's own menu, a face's menu, the Start button's quick links. Right clicked again where
            // it was opened, it closes (§430: as the taskbar's jump list does); right clicked elsewhere, that one's opens.
            Toggle("rest-right-folder", Face(Folders[0]).Center, null, right: true);
            Toggle("rest-right-bar", Room(bar), null, right: true);
            Toggle("rest-right-start", bar.First(spot => spot.Hit == "Start").Center, null, right: true);
            Switch("rest-right-folder-to-bar", Face(Folders[0]).Center, Room(bar), right: true);
            Close();
            // A folder open, a right click on its face: the folder goes and the face's menu opens, in the one click.
            Switch("rest-folder-to-its-menu", Face(Folders[0]).Center, Face(Folders[0]).Center, secondRight: true);
            Close();
            Finish();
        }

        public void InEditMode()
        {
            var bar = Read("stage-map.json", wait: true);
            Pump(1500);
            Spot Face(string id) => bar.First(spot => spot.Key.EndsWith("/" + id, StringComparison.Ordinal));
            // §520: a second start opens edit mode with the settings panel open on the Karpo page.
            Assert.True(Host() is { } host && PostMessageW(host, SecondStart.Message, 0, 0), "no dock host");
            Pump(1500);

            // The panel's dropdowns, each toggled by its box; from one to the other while one stands; Add, from a dropdown.
            var language = Deck(PanelHitKind.Pop, "Karpo/");
            Toggle("edit-panel-dropdown", language.Center, null);
            // §575: Add's list a flyout of the edit bar's: from a dropdown open, the dropdown closing; and alone.
            Press(language.Center, right: false);
            Pump(800);
            AddToggles("edit-panel-dropdown-to-add", from: true);
            Close();
            AddToggles("edit-deck-add");
            // §527: a folder's face, the panel open: the folder opens arranged and its face closes it, as at rest; from one
            // folder's face to the other's in the one click.
            bar = Read("stage-map.json", wait: true);
            watched = [Stratum.Popup, Stratum.Menu, Stratum.ArrangedFolder];
            Toggle("edit-folder", Face(Folders[0]).Center, Folders[0]);
            Switch("edit-folder-to-folder", Face(Folders[0]).Center, Face(Folders[1]).Center);
            Close();
            watched = [Stratum.Popup, Stratum.Menu];
            // §575: the flyout open closed by its entry: the bar's own buttons.
            if (Read("stage-edit.json").FirstOrDefault(spot => spot.Hit == "Page") is { } open)
            {
                Press((open.Key == nameof(PanelPage.Add) ? Deck(PanelHitKind.Add) : Deck(PanelHitKind.Nav, open.Key)).Center, right: false);
                Pump(800);
            }

            // Each of the bar's blue "+" (§387): §576 the add page, hung from it, toggled by it.
            foreach (var plus in Read("stage-edit.json").Where(spot => spot.Hit == "BarAdd").Take(2))
            {
                AddToggles($"edit-bar-plus-{plus.Index}", at: plus.Center);
            }

            // §527: a click on a folder's face: the folder open.
            Click(Face(Folders[0]).Center);
            Assert.NotNull(OpenFolderKey());
            AddToggles("edit-deck-add-into-folder");
            foreach (var plus in Read("stage-edit.json").Where(spot => spot.Hit == "FolderAdd").Take(1))
            {
                AddToggles("edit-folder-plus", at: plus.Center);
            }

            // A line's menu by its right click, right clicked again: closed; from it to the other line's.
            var lines = Lines();
            Toggle("edit-line-right", lines[0].Center, null, right: true);
            Switch("edit-line-to-line", lines[0].Center, lines[1].Center, right: true);
            Close();
            Escape();

            // §394: the errors' indicator, once the feed has failed.
            var until = Environment.TickCount64 + 30_000;
            while (!Read("stage-edit.json").Any(spot => spot.Hit == "Deck" && spot.Index == (int)PanelHitKind.Errors) && Environment.TickCount64 < until)
            {
                Pump(250);
            }
            Toggle("edit-deck-errors", Deck(PanelHitKind.Errors).Center, null);
            Press(Deck(PanelHitKind.Errors).Center, right: false);
            Pump(800);
            AddToggles("edit-deck-errors-to-add", from: true);
            Close();
            Finish();
        }

        private void Finish()
        {
            said.AppendLine("wrong:");
            foreach (var line in wrong)
            {
                said.AppendLine("  " + line);
            }
            Assert.True(dock.Running, "the dock ended");
            Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
        }

        /// <summary>
        /// <paramref name="opener"/> clicked (or right clicked): a popup opens — <paramref name="folder"/>'s where named —;
        /// clicked again, filmed: it closes and stays closed, never shown again between.
        /// </summary>
        private void Toggle(string name, POINT opener, string? folder, bool right = false, bool optional = false)
        {
            Afresh();
            Press(opener, right);
            WaitShown();
            var shown = Shown();
            if (shown.Count == 0)
            {
                if (!optional)
                {
                    wrong.Add($"{name}: nothing opened");
                }
                said.AppendLine($"{name}: nothing opened; in sight: {string.Join(", ", Screen().Select(window => $"{Hex(window)} {(strata.TryGetValue(window, out var stratum) ? stratum.ToString() : "?")}"))}");
                return;
            }
            if (folder is not null && OpenFolderKey()?.EndsWith("/" + folder, StringComparison.Ordinal) != true && !Visible(Plan().Where(entry => entry.Stratum == Stratum.Popup).Select(entry => entry.Window)))
            {
                wrong.Add($"{name}: the folder did not open");
            }
            var film = Film(name, opener, () => Press(opener, right));
            // Every moment from the press on: open until it closes, then closed to the end; never open again.
            var states = new string([.. film.Select(moment => moment.Shown.Length > 0 ? 'o' : 'c')]);
            said.AppendLine($"{name}: opened {string.Join(",", shown.Select(Describe))}; clicked again: {states}; at the end {string.Join(",", Shown().Select(Describe))}");
            var closedAt = states.IndexOf('c');
            if (closedAt < 0)
            {
                wrong.Add($"{name}: clicked again, it is open still — closed and opened again in the one click (o open, c closed): {states}");
            }
            else if (states.IndexOf('o', closedAt) is var again and >= 0)
            {
                wrong.Add($"{name}: clicked again, it closed and opened again (o open, c closed): {states}; again: {film[again].Shown}");
            }
            Close();
        }

        /// <summary>
        /// <paramref name="first"/> clicked, then <paramref name="second"/>, filmed: the second's popup stands at the end,
        /// the first's gone, and no moment between shows neither.
        /// </summary>
        private void Switch(string name, POINT first, POINT second, bool right = false, bool secondRight = false)
        {
            Afresh();
            Press(first, right);
            WaitShown();
            var before = Shown();
            if (before.Count == 0)
            {
                wrong.Add($"{name}: the first opened nothing");
                return;
            }
            var film = Film(name, second, () => Press(second, right || secondRight));
            var states = new string([.. film.Select(moment => moment.Shown.Length > 0 ? 'o' : 'c')]);
            said.AppendLine($"{name}: {states}");
            if (states.EndsWith('c'))
            {
                wrong.Add($"{name}: the second opened nothing: {states}");
            }
            else if (states.Contains('c'))
            {
                wrong.Add($"{name}: a moment with neither open (o open, c closed): {states}");
            }
        }

        /// <summary>
        /// <paramref name="act"/> done and watched for a second: which popups are in sight, as fast as it can be asked,
        /// and a strip of the screen round <paramref name="near"/> and the popups (film-toggle-name.png).
        /// </summary>
        private List<Moment> Film(string name, POINT near, Action act)
        {
            var area = new RECT { left = near.x - 200, top = near.y - 200, right = near.x + 200, bottom = near.y + 200 };
            foreach (var window in Popups.Where(window => IsWindowVisible(window)))
            {
                RECT box;
                if (GetWindowRgnBox(window, &box) is SIMPLEREGION or COMPLEXREGION && RectOf(window, out var at))
                {
                    area = Union(area, new RECT { left = at.left + box.left, top = at.top + box.top, right = at.left + box.right, bottom = at.top + box.bottom });
                }
            }
            area = new RECT { left = Math.Max(area.left, -2560), top = Math.Max(area.top, 0), right = Math.Min(area.right, 0), bottom = Math.Min(area.bottom, 1440) };
            var film = new FrameFilm.Film("toggle-" + name, area) { Before = FrameFilm.Snap(area, Screen(), 0, "before") };
            var moments = new List<Moment>();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            act();
            var shot = 0;
            while (clock.ElapsedMilliseconds < 1000)
            {
                Pump(1);
                Plan();
                moments.Add(new Moment(clock.Elapsed.TotalMilliseconds, string.Join(",", Popups.Where(window => IsWindowVisible(window)).Select(Hex))));
                if (shot++ % 4 == 0)
                {
                    film.Frames.Add(FrameFilm.Snap(area, Screen(), clock.Elapsed.TotalMilliseconds, moments[^1].Shown));
                }
            }
            // A loaded machine (the whole suite at once) may take longer to land: watched on, unfilmed.
            while (clock.ElapsedMilliseconds < 2500)
            {
                Pump(20);
                Plan();
                moments.Add(new Moment(clock.Elapsed.TotalMilliseconds, string.Join(",", Popups.Where(window => IsWindowVisible(window)).Select(Hex))));
            }
            film.End = FrameFilm.Snap(area, Screen(), clock.Elapsed.TotalMilliseconds, "end");
            Save(film);
            return moments;
        }

        private static void Save(FrameFilm.Film film)
        {
            var was = Environment.GetEnvironmentVariable("KARPO_PNG_DIR");
            Environment.SetEnvironmentVariable("KARPO_PNG_DIR", Pictures);
            try
            {
                film.Save();
            }
            finally
            {
                Environment.SetEnvironmentVariable("KARPO_PNG_DIR", was);
            }
        }

        /// <summary>Until a popup is in sight (three seconds at most, a loaded machine's), and a moment for it to land.</summary>
        private void WaitShown()
        {
            var until = Environment.TickCount64 + 3000;
            while (Shown().Count == 0 && Environment.TickCount64 < until)
            {
                Pump(50);
            }
            Pump(400);
        }

        /// <summary>
        /// §575: "+ Add" pressed: its list's flyout opens, every menu open closing (<paramref name="from"/>: the menu open
        /// before, a dropdown's, the errors' list); pressed again: it closes.
        /// </summary>
        private void AddToggles(string name, bool from = false, POINT? at = null)
        {
            string? Shows() => Read("stage-edit.json").FirstOrDefault(spot => spot.Hit == "Page")?.Key;
            // The edit bar's Add, §576 or a "+" of a bar or a folder's column.
            var opener = at ?? Deck(PanelHitKind.Add).Center;
            Press(opener, right: false);
            var until = Environment.TickCount64 + 3000;
            while (Shows() != nameof(PanelPage.Add) && Environment.TickCount64 < until)
            {
                Pump(100);
            }
            Pump(300);
            said.AppendLine($"{name}: {Shows() ?? "nothing"} open, {Plan().Count(entry => entry.Stratum == Stratum.Menu && IsWindowVisible(entry.Window))} menus");
            if (Shows() != nameof(PanelPage.Add))
            {
                wrong.Add($"{name}: Add opened {Shows() ?? "nothing"}");
            }
            if (from && Plan().Any(entry => entry.Stratum == Stratum.Menu && IsWindowVisible(entry.Window)))
            {
                wrong.Add($"{name}: the menu open before stayed with the add list");
            }
            // §593: the add page at the edit bar's top whatever opened it, as wide as the bar, flush with its sides; never on a "+".
            var spots = Read("stage-edit.json");
            if (spots.FirstOrDefault(spot => spot.Hit == "Page") is { Rect: var page })
            {
                if (spots.FirstOrDefault(spot => spot.Hit == "EditBar" && spot.Rect.left < page.right && page.left < spot.Rect.right) is not { Rect: var bar }
                    || Math.Abs(page.left - bar.left) > 1 || Math.Abs(page.right - bar.right) > 1)
                {
                    wrong.Add($"{name}: the add page at {page.left},{page.top}..{page.right},{page.bottom} is not as wide as its edit bar");
                }
                if (at is { } plus && plus.x >= page.left && plus.x < page.right && plus.y >= page.top && plus.y < page.bottom)
                {
                    wrong.Add($"{name}: the add page at {page.left},{page.top}..{page.right},{page.bottom} stands on the \"+\" at {plus.x},{plus.y}");
                }
            }
            Press(opener, right: false);
            Pump(800);
            if (Shows() is { } still)
            {
                wrong.Add($"{name}: Add pressed again left {still} open");
            }
        }

        /// <summary>The popups in sight now.</summary>
        private List<HWND> Shown()
        {
            Plan();
            return [.. Popups.Where(window => IsWindowVisible(window))];
        }

        private static bool Visible(IEnumerable<HWND> windows) => windows.Any(window => IsWindowVisible(window));

        /// <summary>Every popup closed: Escape on each menu, then on what is left.</summary>
        private void Close()
        {
            for (var tries = 0; tries < 3 && Shown() is { Count: > 0 } open; tries++)
            {
                foreach (var window in open)
                {
                    _ = PostMessageW(window, WM.WM_KEYDOWN, VK.VK_ESCAPE, 0);
                }
                Pump(600);
            }
        }

        /// <summary>Escape on the arranged folder: it closes.</summary>
        private void Escape()
        {
            foreach (var (folder, _) in Plan().Where(entry => entry.Stratum == Stratum.ArrangedFolder && IsWindowVisible(entry.Window)))
            {
                _ = PostMessageW(folder, WM.WM_KEYDOWN, VK.VK_ESCAPE, 0);
            }
            Pump(800);
        }

        private void EscapeMenus()
        {
            foreach (var (menu, _) in Plan().Where(entry => entry.Stratum == Stratum.Menu))
            {
                _ = PostMessageW(menu, WM.WM_KEYDOWN, VK.VK_ESCAPE, 0);
            }
            Pump(800);
        }

        // --- Doing, as the mouse does it --------------------------------------------------------------------

        /// <summary>A click and a moment after it.</summary>
        private void Click(POINT at)
        {
            Press(at, right: false);
            Pump(1000);
        }

        /// <summary>
        /// A press and its release at <paramref name="at"/> (screen px), as Windows hands them out: the popups' hook
        /// hears the press first (its post to the dock's sink, read before the press itself); the press goes to the
        /// window holding the mouse, else the one under the pointer, chosen as it is pressed. The release with it, in
        /// one go: the cursor is not on this desktop, so a window told the pointer came hears it leave straight after
        /// (TrackMouseEvent), and a press or a release once it left clicks nothing (InputReachesTheBarsTests.Click).
        /// One go is the dock's thread held still while the four are queued (<see cref="Held"/>): it reads the first
        /// move, and its leave is posted, only once the press and the release are there before it. Posted while it ran,
        /// it now and then read the move before the press was queued, so the leave came between them, the press found
        /// the pointer gone and nothing opened (a folder's face, "the first opened nothing"), or a right press on what
        /// opened a menu found no hit and its release opened the menu again.
        /// </summary>
        private void Press(POINT at, bool right)
        {
            var (down, up, keys) = right ? ((uint)WM.WM_RBUTTONDOWN, (uint)WM.WM_RBUTTONUP, 0x0002u) : ((uint)WM.WM_LBUTTONDOWN, (uint)WM.WM_LBUTTONUP, 0x0001u);
            var target = Target(at);
            said.AppendLine($"  {(right ? "right " : "")}press at {at.x},{at.y} on {Hex(target)} {(strata.TryGetValue(target, out var stratum) ? stratum.ToString() : "?")}");
            // The hook's post, and what the dock posts for it (a popup's dismissal), are read before the press: a posted
            // message comes before input. The press here is posted too, so it goes after them: sent, the hook's is
            // handled by the time it returns, and what that posted is queued before the press.
            if (Sink() is { } sink)
            {
                nuint result;
                _ = SendMessageTimeoutW(sink, PressHook.WM_KARPO_PRESS, (WPARAM)(nuint)(down | PressHook.StagedBit), Pack(at), 0 /* SMTO_NORMAL */, 10_000, &result);
            }
            Held(target, () =>
            {
                Post(target, WM.WM_MOUSEMOVE, at, 0);
                Post(target, down, at, keys);
                Post(target, WM.WM_MOUSEMOVE, at, keys);
                Post(target, up, at, 0);
            });
        }

        /// <summary>
        /// <paramref name="post"/> done while the thread of <paramref name="window"/> (the dock's, a process of the test's
        /// own) is suspended, so what it posts is queued whole before that thread reads any of it.
        /// </summary>
        private static void Held(HWND window, Action post)
        {
            var thread = OpenThread(0x0002 /* THREAD_SUSPEND_RESUME */, BOOL.FALSE, GetWindowThreadProcessId(window, null));
            Assert.True(thread != HANDLE.NULL, $"OpenThread failed: {Marshal.GetLastSystemError()}");
            try
            {
                Assert.NotEqual(uint.MaxValue, SuspendThread(thread));
                try
                {
                    post();
                }
                finally
                {
                    _ = ResumeThread(thread);
                }
            }
            finally
            {
                _ = CloseHandle(thread);
            }
        }

        /// <summary>Where a mouse message at <paramref name="at"/> goes now: the dock's window holding the mouse, else the one under it.</summary>
        private HWND Target(POINT at)
        {
            var info = new GUITHREADINFO { cbSize = (uint)sizeof(GUITHREADINFO) };
            var bar = Plan().FirstOrDefault(entry => entry.Stratum == Stratum.Bar).Window;
            return bar != HWND.NULL && GetGUIThreadInfo(GetWindowThreadProcessId(bar, null), &info) && info.hwndCapture != HWND.NULL
                ? info.hwndCapture
                : GetAncestor(WindowFromPoint(at), GA.GA_ROOT);
        }

        /// <summary>The dock's sink for the hook's presses (PressHook), once a popup made it; null before.</summary>
        private HWND? Sink()
        {
            var at = HWND.NULL;
            fixed (char* name = "karpo.presses")
            {
                while ((at = FindWindowExW(HWND.HWND_MESSAGE, at, name, null)) != HWND.NULL)
                {
                    uint pid = 0;
                    _ = GetWindowThreadProcessId(at, &pid);
                    if (pid == dock.Pid)
                    {
                        return at;
                    }
                }
            }
            return null;
        }

        /// <summary>A point as the hook packs it (EditFollowHook.Pack).</summary>
        private static LPARAM Pack(POINT at) => EditFollowHook.Pack(at);

        private static void Post(HWND window, uint message, POINT screen, uint keys)
        {
            RECT rect;
            _ = GetWindowRect(window, &rect);
            var (x, y) = (screen.x - rect.left, screen.y - rect.top);
            _ = PostMessageW(window, message, keys, (LPARAM)(nint)((y << 16) | (x & 0xFFFF)));
        }

        // --- Reading ------------------------------------------------------------------------------------

        /// <summary>The middle of the widest room between two entries along the bar: the bar's own window there.</summary>
        private static POINT Room(List<Spot> bar)
        {
            var row = bar.OrderBy(spot => spot.Rect.left).ToList();
            var (best, at) = (0, new POINT());
            for (var index = 1; index < row.Count; index++)
            {
                var gap = row[index].Rect.left - row[index - 1].Rect.right;
                if (gap > best)
                {
                    best = gap;
                    at = new POINT { x = (row[index - 1].Rect.right + row[index].Rect.left) / 2, y = row[index].Center.y };
                }
            }
            return at;
        }

        private List<Spot> Lines() => [.. Read("stage-edit.json").Where(spot => spot.Hit == "Line").OrderBy(spot => spot.Index)];

        /// <summary>§520: the edit bar's or its panel's thing of <paramref name="part"/>, whose stage key starts with <paramref name="key"/> where given.</summary>
        private Spot Deck(PanelHitKind part, string key = "")
        {
            var until = Environment.TickCount64 + 10_000;
            while (true)
            {
                if (Read("stage-edit.json").FirstOrDefault(spot => spot.Hit == "Deck" && spot.Index == (int)part && spot.Key.StartsWith(part + ":" + key, StringComparison.Ordinal)) is { } found)
                {
                    return found;
                }
                Assert.True(Environment.TickCount64 < until, $"the edit bar shows no {part} {key}");
                Pump(250);
            }
        }

        private string? OpenFolderKey() => Read("stage-edit.json").FirstOrDefault(spot => spot.Hit == "Folder")?.Key;

        /// <summary>The plan the dock last applied, top first, each window with its stratum; the popups' windows remembered.</summary>
        private List<(HWND Window, Stratum Stratum)> Plan()
        {
            for (var tries = 0; ; tries++)
            {
                try
                {
                    using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(dock.Folder, "stage-z.json")));
                    List<(HWND Window, Stratum Stratum)> plan = [.. json.RootElement.EnumerateArray().Select(entry => ((HWND)(nint)entry.GetProperty("hwnd").GetInt64(), Enum.Parse<Stratum>(entry.GetProperty("stratum").GetString()!)))];
                    foreach (var (window, stratum) in plan)
                    {
                        strata[window] = stratum;
                    }
                    return plan;
                }
                catch (Exception error) when (error is IOException or JsonException && tries < 20)
                {
                    Pump(20);
                }
                catch (FileNotFoundException) when (tries < 200)
                {
                    Pump(50);
                }
            }
        }

        private List<HWND> Screen()
        {
            var found = new List<HWND>();
            for (var at = GetTopWindow(HWND.NULL); at != HWND.NULL; at = GetWindow(at, GW_HWNDNEXT))
            {
                uint pid = 0;
                _ = GetWindowThreadProcessId(at, &pid);
                if (pid == dock.Pid && IsWindowVisible(at))
                {
                    found.Add(at);
                }
            }
            return found;
        }

        private List<Spot> Read(string name, bool wait = false)
        {
            var until = Environment.TickCount64 + 60_000;
            while (true)
            {
                Assert.True(dock.Running, "the dock ended");
                try
                {
                    var path = Path.Combine(dock.Folder, name);
                    if (File.Exists(path))
                    {
                        using var json = JsonDocument.Parse(File.ReadAllText(path));
                        var spots = json.RootElement.EnumerateArray().Select(entry => new Spot(
                            entry.GetProperty("hit").GetString()!,
                            entry.GetProperty("index").GetInt32(),
                            entry.GetProperty("key").GetString()!,
                            new RECT
                            {
                                left = entry.GetProperty("x").GetInt32(),
                                top = entry.GetProperty("y").GetInt32(),
                                right = entry.GetProperty("x").GetInt32() + entry.GetProperty("w").GetInt32(),
                                bottom = entry.GetProperty("y").GetInt32() + entry.GetProperty("h").GetInt32(),
                            },
                            entry.TryGetProperty("kind", out var kind) ? kind.GetString() ?? "" : "")).ToList();
                        if (spots.Count > 0 || !wait)
                        {
                            return spots;
                        }
                    }
                    else if (!wait)
                    {
                        return [];
                    }
                }
                catch (Exception error) when (error is IOException or JsonException)
                {
                    // Being written.
                }
                Assert.True(Environment.TickCount64 < until, $"the dock never wrote {name}");
                Pump(100);
            }
        }

        private HWND? Host()
        {
            var at = HWND.NULL;
            fixed (char* name = DockHost.ClassName)
            {
                while ((at = FindWindowExW(HWND.NULL, at, name, null)) != HWND.NULL)
                {
                    uint pid = 0;
                    _ = GetWindowThreadProcessId(at, &pid);
                    if (pid == dock.Pid)
                    {
                        return at;
                    }
                }
            }
            return null;
        }

        private static bool RectOf(HWND window, out RECT rect)
        {
            RECT got;
            var ok = TerraFX.Interop.Windows.Windows.GetWindowRect(window, &got);
            rect = got;
            return ok;
        }

        private static RECT Union(RECT a, RECT b) => new() { left = Math.Min(a.left, b.left), top = Math.Min(a.top, b.top), right = Math.Max(a.right, b.right), bottom = Math.Max(a.bottom, b.bottom) };

        private static string Hex(HWND window) => $"{(nint)window:X}";

        /// <summary>A window by its handle, its title and the box of its region on the screen.</summary>
        private string Describe(HWND window)
        {
            var title = stackalloc char[128];
            var length = GetWindowTextW(window, title, 128);
            RECT box = default;
            _ = GetWindowRgnBox(window, &box);
            return $"{Hex(window)} {(strata.TryGetValue(window, out var stratum) ? stratum.ToString() : "?")} \"{new string(title, 0, length)}\" {Text(box)}";
        }

        private static string Text(RECT r) => $"{r.left},{r.top}..{r.right},{r.bottom}";

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
    }
}
