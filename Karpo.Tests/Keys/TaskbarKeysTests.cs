// The taskbar's keys while Karpo's bars are the taskbar (Karpo.Dock\TaskbarKeys.cs): which keys the
// hook keeps and which it lets by, which app button Win+N stands for, what Win+Ctrl+N brings, and
// where the keyboard goes along a bar. All of it pure: nothing is hooked, pressed or started here,
// and the bars are plans (Layout.ForTests), not windows.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Keys;

public sealed class TaskbarKeysTests
{
    private const uint Win = VK.VK_LWIN;
    private const uint Shift = VK.VK_LSHIFT;
    private const uint Control = VK.VK_LCONTROL;
    private const uint Alt = VK.VK_LMENU;

    /// <summary>What the hook does with each of <paramref name="keys"/> in turn: + for down, - for up.</summary>
    private static List<KeyHeard> Press(KeyChords chords, params (uint Vk, bool Down)[] keys) =>
        [.. keys.Select(key => chords.Hear(key.Vk, key.Down))];

    private static (uint, bool) Down(uint vk) => (vk, true);

    private static (uint, bool) Up(uint vk) => (vk, false);

    // --- The hook's decision --------------------------------------------------------------------

    /// <summary>
    /// Win+1 is kept, down and up, and makes one chord; the Windows key goes by both ways, so Windows
    /// knows it was let go; and the first chord of the press masks the Start menu, which would open
    /// on the release of a Windows key that, as far as Windows knows, was pressed alone.
    /// </summary>
    [Fact]
    public void WinAndANumberIsKeptAndTheWindowsKeyGoesBy()
    {
        var heard = Press(new KeyChords(), Down(Win), Down('1'), Up('1'), Up(Win));

        Assert.Equal(new KeyHeard(false), heard[0]);
        Assert.Equal(new KeyHeard(true, Mask: true, Act: new TaskbarChord(ChordKind.App, 1)), heard[1]);
        Assert.Equal(new KeyHeard(true), heard[2]);
        Assert.Equal(new KeyHeard(false), heard[3]);
    }

    /// <summary>Every chord of the taskbar's, by the modifiers held with the Windows key; 0 is the tenth app.</summary>
    [Theory]
    [InlineData('3', false, false, false, ChordKind.App, 3)]
    [InlineData('0', false, false, false, ChordKind.App, 10)]
    [InlineData('4', true, false, false, ChordKind.NewApp, 4)]
    [InlineData('5', true, true, false, ChordKind.ElevatedApp, 5)]
    [InlineData('6', false, true, false, ChordKind.CycleApp, 6)]
    [InlineData('7', false, false, true, ChordKind.JumpList, 7)]
    [InlineData('T', false, false, false, ChordKind.FocusApps, 0)]
    [InlineData('T', true, false, false, ChordKind.FocusAppsBack, 0)]
    [InlineData('B', false, false, false, ChordKind.FocusTray, 0)]
    public void EveryChordOfTheTaskbarsIsKnown(char key, bool shift, bool control, bool alt, ChordKind kind, int number)
    {
        var chords = new KeyChords();
        var held = new List<uint> { Win };
        if (shift) held.Add(Shift);
        if (control) held.Add(Control);
        if (alt) held.Add(Alt);
        foreach (var modifier in held)
        {
            Assert.False(chords.Hear(modifier, true).Swallow);
        }

        var heard = chords.Hear(key, true);

        Assert.True(heard.Swallow);
        Assert.Equal(new TaskbarChord(kind, number), heard.Act);
    }

    /// <summary>
    /// Everything else goes by: a number without the Windows key, the Windows key's other chords
    /// (Win+D, Win+E, Win+Shift+S), and a taskbar key with modifiers the taskbar gives no meaning.
    /// </summary>
    [Theory]
    [InlineData(false, false, false, false, '1')]
    [InlineData(true, false, false, false, 'D')]
    [InlineData(true, false, false, false, 'E')]
    [InlineData(true, true, false, false, 'S')]
    [InlineData(true, false, true, true, '1')]
    [InlineData(true, true, false, true, '2')]
    [InlineData(true, false, true, false, 'B')]
    [InlineData(true, false, false, true, 'T')]
    [InlineData(true, false, false, false, VK.VK_NUMPAD1)]
    public void EverythingElseGoesBy(bool win, bool shift, bool control, bool alt, uint key)
    {
        var chords = new KeyChords();
        foreach (var (modifier, on) in new[] { (Win, win), (Shift, shift), (Control, control), (Alt, alt) })
        {
            if (on)
            {
                chords.Hear(modifier, true);
            }
        }

        Assert.Equal(new KeyHeard(false), chords.Hear(key, true));
        Assert.Equal(new KeyHeard(false), chords.Hear(key, false));
    }

    /// <summary>
    /// A chord key held down repeats: the repeats are kept and make nothing more. Let go and pressed
    /// again, it is a chord again (Win+Ctrl+N pressed again walks the windows), and the Start menu
    /// is masked once per press of the Windows key, not once per chord.
    /// </summary>
    [Fact]
    public void RepeatsAreKeptAndAChordIsMadeOncePerPress()
    {
        var chords = new KeyChords();
        var heard = Press(chords, Down(Win), Down(Control), Down('2'), Down('2'), Down('2'), Up('2'), Down('2'), Up('2'));

        Assert.Equal(new TaskbarChord(ChordKind.CycleApp, 2), heard[2].Act);
        Assert.True(heard[2].Mask);
        Assert.Equal(new KeyHeard(true), heard[3]);
        Assert.Equal(new KeyHeard(true), heard[4]);
        Assert.Equal(new KeyHeard(true, Mask: false, Act: new TaskbarChord(ChordKind.CycleApp, 2)), heard[6]);

        // The Windows key let go and pressed again: the next chord masks the Start menu again.
        Press(chords, Up(Control), Up(Win), Down(Win));
        Assert.True(chords.Hear('1', true).Mask);
    }

    /// <summary>
    /// The key of a chord comes up kept even when the Windows key went first — else the app in front
    /// would get the release of a key it never saw go down; and a key that went down before the
    /// Windows key comes up as it went down, let by.
    /// </summary>
    [Fact]
    public void AKeyComesUpAsItWentDown()
    {
        var chords = new KeyChords();
        Assert.Equal([new KeyHeard(false), new KeyHeard(true, true, new TaskbarChord(ChordKind.App, 9)), new KeyHeard(false), new KeyHeard(true)],
            Press(chords, Down(Win), Down('9'), Up(Win), Up('9')));

        var other = new KeyChords();
        Assert.Equal([new KeyHeard(false), new KeyHeard(false), new KeyHeard(false), new KeyHeard(false)],
            Press(other, Down('9'), Down(Win), Up('9'), Up(Win)));
    }

    /// <summary>The right Windows key and the generic modifier codes count as the left ones do.</summary>
    [Fact]
    public void EitherWindowsKeyAndEitherShiftCount()
    {
        var chords = new KeyChords();
        Press(chords, Down(VK.VK_RWIN), Down(VK.VK_RSHIFT));

        Assert.Equal(new TaskbarChord(ChordKind.NewApp, 1), chords.Hear('1', true).Act);
    }

    /// <summary>A chord crosses to the dock's thread packed into one message word, and comes back the same.</summary>
    [Theory]
    [InlineData(ChordKind.App, 1)]
    [InlineData(ChordKind.JumpList, 10)]
    [InlineData(ChordKind.FocusTray, 0)]
    public void AChordSurvivesTheMessage(ChordKind kind, int number)
    {
        var chord = new TaskbarChord(kind, number);

        Assert.Equal(chord, TaskbarChord.Unpack(chord.Packed));
    }

    // --- Start is Windows' ----------------------------------------------------------------------

    /// <summary>
    /// The Windows key alone, the left one and the right, goes to Windows both ways, held or not, so
    /// Windows' own Start opens; nothing is masked and nothing is asked of the dock.
    /// </summary>
    [Theory]
    [InlineData(VK.VK_LWIN)]
    [InlineData(VK.VK_RWIN)]
    public void ALoneWindowsKeyGoesToWindows(uint key)
    {
        var heard = Press(new KeyChords(), Down(key), Down(key), Down(key), Up(key));

        Assert.All(heard, one => Assert.Equal(default, one));
    }

    /// <summary>Ctrl+Esc, Windows' other way to Start, and Ctrl+Shift+Esc, Task Manager's, go to Windows.</summary>
    [Fact]
    public void ControlEscapeGoesToWindows()
    {
        Assert.All(Press(new KeyChords(), Down(Control), Down(VK.VK_ESCAPE), Down(VK.VK_ESCAPE), Up(VK.VK_ESCAPE), Up(Control)),
            one => Assert.Equal(default, one));
        Assert.All(Press(new KeyChords(), Down(Control), Down(Shift), Down(VK.VK_ESCAPE), Up(VK.VK_ESCAPE), Up(Shift), Up(Control)),
            one => Assert.Equal(default, one));
    }

    /// <summary>
    /// Windows' own shortcuts go by untouched: Win+L, Win+E, Win+R, Win+S, Win+X, Win+D, Win+V, Win+I,
    /// Win+A, Win+N, Win+W, Win+Tab, the arrows, Win+. and Win+Print Screen (VK_SNAPSHOT).
    /// </summary>
    [Theory]
    [InlineData('L')]
    [InlineData('E')]
    [InlineData('R')]
    [InlineData('S')]
    [InlineData('X')]
    [InlineData('D')]
    [InlineData('V')]
    [InlineData('I')]
    [InlineData('A')]
    [InlineData('N')]
    [InlineData('W')]
    [InlineData(VK.VK_TAB)]
    [InlineData(VK.VK_LEFT)]
    [InlineData(VK.VK_UP)]
    [InlineData(VK.VK_OEM_PERIOD)]
    [InlineData(VK.VK_SNAPSHOT)]
    public void WindowsShortcutsGoToWindows(uint key)
    {
        var heard = Press(new KeyChords(), Down(Win), Down(key), Up(key), Up(Win));

        Assert.All(heard, one => Assert.Equal(default, one));
    }

    /// <summary>
    /// The Windows key Karpo presses for Windows' Start where there is no taskbar to ask is marked Karpo's, so the
    /// hook lets it by rather than following it as the person's.
    /// </summary>
    [Fact]
    public void WindowsStartIsPressedWithKarposMark()
    {
        var keys = Desktops.StartKeys();

        Assert.Equal(2, keys.Count);
        Assert.All(keys, key => Assert.Equal(TaskbarKeys.Ours, key.ki.dwExtraInfo));
        Assert.All(keys, key => Assert.Equal((ushort)VK.VK_LWIN, key.ki.wVk));
    }

    /// <summary>
    /// Explorer's launcher of Start as a test has it: which displays it was connected to, how often
    /// it was asked to show Start, and whether each of those works.
    /// </summary>
    private sealed class FakeLauncher(bool connects = true, bool shows = true) : IStartLauncher
    {
        public List<nint> Connected { get; } = [];

        public int Shown { get; private set; }

        public bool Connect(nint monitor)
        {
            Connected.Add(monitor);
            return connects;
        }

        public bool Show()
        {
            Shown++;
            return shows;
        }
    }

    /// <summary>
    /// The person, 2026-10-28: "sometimes when clicking the start button the start just appears no the
    /// wrong screen." Start opens on the display Explorer's launcher is connected to, which follows the
    /// foreground window, not Karpo's bar (which never takes it): so the launcher is connected to the
    /// clicked button's display first and opens Start there, and neither the taskbar nor a key is asked.
    /// </summary>
    [Fact]
    public void StartOpensOnTheDisplayOfTheButtonClicked()
    {
        var buttons = (nint)0xB0B;
        var launcher = new FakeLauncher();
        var asked = 0;
        var pressed = 0;

        Desktops.OpenStart(buttons, launcher, (HWND)0x1234, _ => { asked++; return true; }, _ => pressed++);

        Assert.Equal([buttons], launcher.Connected);
        Assert.Equal(1, launcher.Shown);
        Assert.Equal(0, asked);
        Assert.Equal(0, pressed);
    }

    /// <summary>
    /// Where the launcher can't be had (Explorer restarting, another shell) or won't connect or show,
    /// the Start button asks Explorer's taskbar for Start, as the taskbar's own button does, and
    /// presses no key: a launcher that takes the Windows key (Raycast) swallowed the pressed one, and
    /// Start never opened (2026-10-25).
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void StartIsAskedOfTheTaskbarNotPressed(bool connects, bool shows)
    {
        var tray = (HWND)0x1234;
        var asked = new List<HWND>();
        var pressed = 0;

        Desktops.OpenStart(0xB0B, new FakeLauncher(connects, shows), tray, hwnd => { asked.Add(hwnd); return true; }, _ => pressed++);

        Assert.Equal([tray], asked);
        Assert.Equal(0, pressed);
    }

    /// <summary>With no display to connect to, the launcher is not asked at all.</summary>
    [Fact]
    public void WithoutADisplayTheLauncherIsLeftAlone()
    {
        var launcher = new FakeLauncher();

        Desktops.OpenStart(0, launcher, (HWND)0x1234, _ => true, _ => { });

        Assert.Empty(launcher.Connected);
        Assert.Equal(0, launcher.Shown);
    }

    /// <summary>With no taskbar of Explorer's, or one that will not take the message, Start is the Windows key.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StartIsTheWindowsKeyWithoutATaskbarToAsk(bool trayThere)
    {
        var asked = 0;
        List<INPUT>? pressed = null;

        Desktops.OpenStart(0xB0B, new FakeLauncher(connects: false), trayThere ? (HWND)0x1234 : HWND.NULL, _ => { asked++; return false; }, keys => pressed = keys);

        Assert.Equal(trayThere ? 1 : 0, asked);
        Assert.NotNull(pressed);
        Assert.All(pressed, key => Assert.Equal((ushort)VK.VK_LWIN, key.ki.wVk));
    }

    // --- Releases the hook never hears ----------------------------------------------------------

    /// <summary>
    /// The keyboard as the hook's IsDown reads it: every key pressed, heard or not, and each key
    /// put in it only after the hook has heard it, as GetAsyncKeyState has it inside the hook.
    /// </summary>
    private sealed class Keyboard
    {
        private readonly HashSet<uint> down = [];

        public KeyChords Chords { get; }

        public Keyboard() => Chords = new KeyChords { IsDown = down.Contains };

        public KeyHeard Heard(uint vk, bool press)
        {
            var heard = Chords.Hear(vk, press);
            Unheard(vk, press);
            return heard;
        }

        /// <summary>A key the hook never hears: another desktop has the input, or the hook was away.</summary>
        public void Unheard(uint vk, bool press)
        {
            if (press)
            {
                down.Add(vk);
            }
            else
            {
                down.Remove(vk);
            }
        }
    }

    /// <summary>
    /// Win+L: the lock comes on L, and the Windows key may come up on the lock screen, where the hook
    /// hears nothing. Back on the desktop a plain 1, T or B is typed, not kept as a chord.
    /// </summary>
    [Theory]
    [InlineData('1')]
    [InlineData('T')]
    [InlineData('B')]
    public void AWindowsKeyLetGoUnheardIsNotHeldForGood(uint typed)
    {
        var keyboard = new Keyboard();
        keyboard.Heard(Win, true);
        keyboard.Heard('L', true);
        keyboard.Unheard('L', false);
        keyboard.Unheard(Win, false);

        Assert.Equal(default, keyboard.Heard(typed, true));
        Assert.Equal(default, keyboard.Heard(typed, false));
    }

    /// <summary>
    /// Ctrl+Shift+Enter, run as administrator: UAC's prompt takes the input with Ctrl and Shift still
    /// down. Afterwards Win+1 is Win+1, not Win+Ctrl+Shift+1.
    /// </summary>
    [Fact]
    public void ModifiersLetGoUnheardMakeNoChordsAfterwards()
    {
        var keyboard = new Keyboard();
        keyboard.Heard(Control, true);
        keyboard.Heard(Shift, true);
        keyboard.Heard(VK.VK_RETURN, true);
        keyboard.Unheard(VK.VK_RETURN, false);
        keyboard.Unheard(Shift, false);
        keyboard.Unheard(Control, false);

        keyboard.Heard(Win, true);
        Assert.Equal(new TaskbarChord(ChordKind.App, 1), keyboard.Heard('1', true).Act);
    }

    /// <summary>
    /// Win+L, the Windows key let go unheard, and the Windows key the next key pressed: a new press,
    /// whose first chord masks Windows' Start again.
    /// </summary>
    [Fact]
    public void AWindowsKeyPressedAgainAfterAnUnheardReleaseIsANewPress()
    {
        var keyboard = new Keyboard();
        keyboard.Heard(Win, true);
        Assert.True(keyboard.Heard('1', true).Mask);
        keyboard.Heard('1', false);
        keyboard.Unheard(Win, false);

        Assert.Equal(default, keyboard.Heard(Win, true));
        Assert.True(keyboard.Heard('2', true).Mask);
    }

    /// <summary>What the keyboard holds stands: a held Windows key still makes Win+1, and its repeats change nothing.</summary>
    [Fact]
    public void ModifiersStillHeldStayHeld()
    {
        var keyboard = new Keyboard();
        keyboard.Heard(Win, true);
        keyboard.Heard(Win, true);
        Assert.Equal(new TaskbarChord(ChordKind.App, 1), keyboard.Heard('1', true).Act);
        keyboard.Heard('1', false);
        Assert.Equal(default, keyboard.Heard(Win, false));
    }

    // --- Which button is N ------------------------------------------------------------------------

    /// <summary>
    /// The Nth app is counted along the bar, pins and open apps alike, an app each: a pin standing as
    /// a button per window, or an app's windows each a button, is one number, as Windows counts
    /// them. Past the last app there is nothing.
    /// </summary>
    [Fact]
    public void TheNthAppIsCountedAnAppEach()
    {
        string[] buttons = ["pin:edge", "pin:word", "pin:word", "open:terminal", "open:paint", "open:paint", "open:paint"];

        Assert.Equal((0, 1), AppNumbers.Nth(buttons, 1));
        Assert.Equal((1, 2), AppNumbers.Nth(buttons, 2));
        Assert.Equal((3, 1), AppNumbers.Nth(buttons, 3));
        Assert.Equal((4, 3), AppNumbers.Nth(buttons, 4));
        Assert.Null(AppNumbers.Nth(buttons, 5));
        Assert.Null(AppNumbers.Nth(buttons, 0));
    }

    /// <summary>Win+0 is the tenth app, and an eleventh has no number.</summary>
    [Fact]
    public void ZeroIsTheTenth()
    {
        var buttons = Enumerable.Range(1, 11).Select(index => $"app{index}").ToList();

        Assert.Equal(10, AppNumbers.Numbered(buttons).Count(entry => entry.First < 10));
        Assert.Equal((9, 1), AppNumbers.Nth(buttons, KeyChords.ChordOf('0', false, false, false)!.Value.Number));
    }

    // --- Win+Ctrl+N ------------------------------------------------------------------------------

    /// <summary>
    /// Win+Ctrl+N: nothing open starts it; none of its windows in front brings the most recent; one
    /// in front brings the least recent, so pressing again walks through them; and it never
    /// minimizes, even the only window, in front.
    /// </summary>
    [Fact]
    public void WinCtrlNumberWalksTheWindowsAndNeverMinimizes()
    {
        AppWindow[] three = [new(11, false), new(12, false), new(13, true)];

        Assert.Equal(AppClickKind.Launch, AppKeys.Cycle([], 99).Kind);
        Assert.Equal(new AppClick(AppClickKind.Activate, 0), AppKeys.Cycle(three, 99));
        Assert.Equal(new AppClick(AppClickKind.Activate, 2), AppKeys.Cycle(three, 11));
        Assert.Equal(new AppClick(AppClickKind.Activate, 0), AppKeys.Cycle([new(11, false)], 11));
        // A minimized window is never the one in front.
        Assert.Equal(new AppClick(AppClickKind.Activate, 0), AppKeys.Cycle(three, 13));
    }

    // --- The keyboard along a bar ------------------------------------------------------------------

    private static Config Bar() => new() { Height = 48 };

    private static PartSpec Spec(string id, BarKind kind) => new(Config.IslandKey(null, id), kind);

    private static Layout Plan(int length, int tiles, int tray, Edge edge = Edge.Bottom)
    {
        var config = Bar();
        var area = edge.IsVertical()
            ? new RECT { left = 0, top = 0, right = 1600, bottom = length }
            : new RECT { left = 0, top = 0, right = length, bottom = 1600 };
        List<PartSpec> specs = [Spec("start", BarKind.Start), Spec("apps", BarKind.Apps), Spec("tray", BarKind.Tray), Spec("clock", BarKind.NightLight)];
        return Layout.ForTests(96, area, edge, config, specs, Layout.CellPx(96, config), tray: tray, trayOverflow: true, tiles: tiles);
    }

    private static float Along(Layout layout, Hit hit) => layout.Vertical ? layout.ItemRect(hit).Y : layout.ItemRect(hit).X;

    /// <summary>
    /// The keyboard stops at every app button the bar shows, in their order along it, and nowhere
    /// else of the bar; on the tray, at every icon it shows and the chevron of the hidden ones, in
    /// their order along it — across the bottom and down the side alike.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Left)]
    public void TheStopsAreTheButtonsAndIconsInTheirOrderAlongTheBar(Edge edge)
    {
        var plan = Plan(1600, tiles: 5, tray: 3, edge);

        var apps = KeyStops.Of(plan, KeyRegion.Apps, 5);
        var tray = KeyStops.Of(plan, KeyRegion.Tray, 5);

        Assert.Equal(Enumerable.Range(0, 5).Select(Hit.Tile), apps);
        Assert.Equal(4, tray.Count);
        Assert.Contains(Hit.TrayOverflow, tray);
        Assert.Equal(Enumerable.Range(0, 3).Select(Hit.Tray), tray.Where(hit => hit.Kind == HitKind.Tray));
        Assert.All(tray.Zip(tray.Skip(1)), pair => Assert.True(Along(plan, pair.First) < Along(plan, pair.Second)));
    }

    /// <summary>
    /// Everything else along the bar is the third region (docs\widgets.md, "One keyboard"): Start and the
    /// widget's cells, in their order along it; and Tab goes round the regions in the order they start
    /// along the bar, Shift+Tab back.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Left)]
    public void EverythingElseAlongTheBarIsARegionAndTabGoesRoundThem(Edge edge)
    {
        var plan = Plan(1600, tiles: 5, tray: 3, edge);

        var bar = KeyStops.Of(plan, KeyRegion.Bar, 5);
        var regions = KeyStops.Regions(plan, 5);

        Assert.Equal(HitKind.Start, bar[0].Kind);
        Assert.Contains(bar, hit => hit.Kind == HitKind.Island);
        Assert.All(bar, hit => Assert.Equal(KeyRegion.Bar, KeyStops.RegionOf(hit)));
        Assert.All(bar.Zip(bar.Skip(1)), pair => Assert.True(Along(plan, pair.First) < Along(plan, pair.Second)));
        // Start first, then the apps, then the tray, then the widget after it: Start's region comes first.
        Assert.Equal([KeyRegion.Bar, KeyRegion.Apps, KeyRegion.Tray], regions);
        Assert.Equal(KeyRegion.Apps, KeyStops.Next(regions, KeyRegion.Bar, 1));
        Assert.Equal(KeyRegion.Bar, KeyStops.Next(regions, KeyRegion.Tray, 1));
        Assert.Equal(KeyRegion.Tray, KeyStops.Next(regions, KeyRegion.Bar, -1));
        Assert.Equal(KeyRegion.Apps, KeyStops.Next([KeyRegion.Apps], KeyRegion.Apps, 1));
        Assert.Null(KeyStops.RegionOf(Hit.Add(0)));
    }

    /// <summary>
    /// Every kind the bar holds is reached by the keys (docs\widgets.md, "One keyboard"): on a bar with one of each,
    /// every entry a screen reader lists is a stop of one region, and every kind placed has a stop.
    /// </summary>
    [Fact]
    public void EveryKindOnTheBarIsAStop()
    {
        var config = Bar();
        var kinds = BarEntries.All.Select(entry => entry.Kind).Distinct().ToList();
        List<PartSpec> specs = [.. kinds.Select(kind => Spec(kind.ToString(), kind))];
        var plan = Layout.ForTests(96, new RECT { left = 0, top = 0, right = 30000, bottom = 1600 }, Edge.Bottom, config, specs, Layout.CellPx(96, config), tray: 3, trayOverflow: true, tiles: 4);

        var stops = KeyStops.Regions(plan, 4).SelectMany(region => KeyStops.Of(plan, region, 4)).ToHashSet();
        var entries = BarUia.Entries(plan, 4).Where(hit => hit.Kind != HitKind.Add).ToList();

        Assert.All(entries, entry => Assert.Contains(entry, stops));
        var unreached = Enumerable.Range(0, plan.Specs.Count)
            .Where(plan.Placed)
            .Where(part => !stops.Any(stop => plan.PartOf(stop) == part))
            .Select(part => plan.Specs[part].Kind)
            .ToList();
        Assert.True(unreached.Count == 0, "no stop: " + string.Join(", ", unreached));
    }

    /// <summary>
    /// A button behind "…" is no stop — the bar has no room to show it focused —, and "…" is, after
    /// the last button shown, where Enter opens the list of the rest.
    /// </summary>
    [Fact]
    public void AButtonBehindMoreIsNoStopAndMoreIs()
    {
        var plan = Plan(600, tiles: 30, tray: 0);
        Assert.NotEmpty(plan.Overflowed);

        var stops = KeyStops.Of(plan, KeyRegion.Apps, 30);

        Assert.Equal(Hit.AppOverflow, stops[^1]);
        Assert.DoesNotContain(stops, hit => hit.Kind == HitKind.Tile && plan.Overflowed.Contains(hit.Index));
        Assert.Equal(30 - plan.Overflowed.Count + 1, stops.Count);
    }

    /// <summary>
    /// The arrows walk the stops round from one end to the other, back for left and up, on for right
    /// and down; from nowhere, forward starts at the first and back at the last, as Win+T and
    /// Win+Shift+T do; and a bar with no stops has nowhere to go.
    /// </summary>
    [Fact]
    public void TheArrowsWalkTheStopsRound()
    {
        Assert.Equal(1, FocusNav.Step(4, 0, KeyStops.Arrow(VK.VK_RIGHT)));
        Assert.Equal(0, FocusNav.Step(4, 3, KeyStops.Arrow(VK.VK_DOWN)));
        Assert.Equal(3, FocusNav.Step(4, 0, KeyStops.Arrow(VK.VK_LEFT)));
        Assert.Equal(2, FocusNav.Step(4, 3, KeyStops.Arrow(VK.VK_UP)));
        Assert.Equal(0, FocusNav.Step(4, -1, 1));
        Assert.Equal(3, FocusNav.Step(4, -1, -1));
        Assert.Equal(-1, FocusNav.Step(0, -1, 1));
        Assert.Equal(0, KeyStops.Arrow(VK.VK_RETURN));
    }
}
