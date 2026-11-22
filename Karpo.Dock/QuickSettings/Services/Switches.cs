// What the one-button widgets stand for: airplane mode (the system-wide switch
// of Windows' radio management service, which is what Windows' own tile flips:
// RadioManager.cs), the mobile hotspot, energy saver, night light (its state
// where Windows keeps it: NightLight.cs), nearby sharing (Nearby.cs), the Wi-Fi
// and Bluetooth radios (ToggleSwitches.cs), the sound's and the microphone's
// mute (MuteSwitch.cs), game mode, color filters and mono audio (SettingToggles.cs), keep
// awake (a power request of Karpo's own: KeepAwake.cs), Windows' dark mode (DarkMode.cs), and the
// things Windows only opens —
// live captions, project and accessibility.
//
// Each says what it can: whether this PC has it at all, whether it is on (null
// when Windows lets no app read that, and then the widget says so instead of
// showing a state it made up), and what happens on a click. Nothing polls: the
// radio service's state is watched where it writes it, the network reports its
// own, energy saver comes with the power broadcasts the dock already hands to
// islands, and the rest have no state to follow.
//
// AND NOTHING IS ASKED OF WINDOWS ON THE DOCK'S THREAD. Start is called once for
// each switch the config names, when its data source is made at the dock's start
// (Sources\DeviceData.cs, SwitchData) — or, for a switch a test hands a widget of
// its own, the first time that widget is looked at (SwitchIsland.Follow). Either
// way the dock is arranging or drawing its bars, so anything Start has to ask goes
// to the pool and comes back through the changed callback. The mobile hotspot is
// why: its look is GetInternetConnectionProfile plus CreateFromConnectionProfile,
// measured at 8-11 ms warm and 60 ms the first time in a process, and it once sat
// in the island's constructor, so every bar with the widget paid it while drawing.
//
// Until an answer arrives a switch says it does not know - On is null, which the
// widget draws as no state at all rather than as off. The tooltip must not claim
// anything either: a line that says Windows will not let Karpo switch this is a
// thing to say once that has been found out, not while it is still being asked.

using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Connectivity = Windows.Networking.Connectivity;
using Operators = Windows.Networking.NetworkOperators;
using Radios = Windows.Devices.Radios;

namespace Karpo.Dock.QuickSettings.Services;

internal interface IQuickSwitch : IDisposable
{
    /// <summary>What it is called, as the tooltip and a folder's card say it.</summary>
    string Name { get; }

    /// <summary>False when this PC hasn't got it: the widget is drawn as a command that can't be used.</summary>
    bool Available { get; }

    /// <summary>On, off, or null when Windows lets no app read it (then the widget shows no state).</summary>
    bool? On { get; }

    /// <summary>A line for the tooltip: why it can't be switched here, or what a click does.</summary>
    string? Note { get; }

    /// <summary>
    /// How many of its clicks Windows has refused since it started: one more ends a pending click
    /// (SwitchPending) at once, the real state showing again. Zero for a switch that cannot tell; its
    /// pending click then ends when it reads where it was sent, or times out.
    /// </summary>
    int Refusals => 0;

    /// <summary>
    /// Starts following it; <paramref name="changed"/> comes from any thread.
    /// <para>
    /// Called once, when the switch's data source is made at the dock's start (SwitchData) — or,
    /// for a switch a test hands a widget of its own, the first time that widget is drawn, hovered
    /// or clicked — and <b>it must not block</b>: the dock is arranging or drawing its bars then, so
    /// whatever has to be asked of Windows goes to the pool and answers through
    /// <paramref name="changed"/>. Until it answers, <see cref="On"/> is null and the widget shows
    /// no state at all, which is the one honest thing to show while the answer is on its way.
    /// </para>
    /// </summary>
    void Start(Action changed);

    /// <summary>A click: switches it, or opens what Windows shows for it.</summary>
    void Act();

    /// <summary>
    /// A click on a widget set as <paramref name="options"/> says: spatial sound turns on in the
    /// format its card names (<see cref="SwitchOptions.SpatialFormat"/>), color filters in the filter
    /// (<see cref="SwitchOptions.ColorFilter"/>). The rest have nothing to
    /// read there, and it is <see cref="Act()"/>.
    /// </summary>
    void Act(SwitchOptions? options) => Act();

    /// <summary>
    /// The glyph it shows now, where that changes with its state — the speaker or the microphone
    /// struck through while muted; null for the widget's own glyph, whatever its state.
    /// </summary>
    string? Glyph => null;

    /// <summary>
    /// Whether <see cref="Act"/> switches what it stands for — a radio, the hotspot — rather than
    /// opening a page or pressing a shortcut. A list's line draws a switch only for one that does
    /// (SwitchIsland): a switch that opened a page when pressed would be saying something untrue.
    /// Where it does, it is drawn only while <see cref="On"/> is known.
    /// </summary>
    bool Switches => false;

    /// <summary>A system message the dock forwards to every island (the power broadcasts).</summary>
    void Heard(uint msg, nuint wParam)
    {
    }

    /// <summary>
    /// What it is on as, where that is more than "on": the VPN it is connected to. Said under the
    /// name where a card says "On", and in the tooltip. Null for a switch that is on or off and
    /// nothing else.
    /// </summary>
    string? Detail => null;

    /// <summary>
    /// The switch's own list, as Windows' tile opens a page of it: the VPN connections, each
    /// connected or disconnected from its row. Null for a switch that has none. Read on the dock's
    /// thread as the list's line is built (ChoicesPage, the VPN preset's), so it answers from what
    /// the switch already knows and asks Windows nothing. A click on the switch's own line is
    /// <see cref="Act"/>.
    /// </summary>
    IReadOnlyList<SwitchChoice>? Choices => null;

    /// <summary>The row <paramref name="index"/> of <see cref="Choices"/> was chosen.</summary>
    void Choose(int index)
    {
    }
}

/// <summary>A row of a switch's list (<see cref="IQuickSwitch.Choices"/>).</summary>
/// <param name="Text">What the row says.</param>
/// <param name="Detail">The quieter line under it: "Connected".</param>
/// <param name="Checked">The one in use, ticked; null for a row that is not a choice among others.</param>
/// <param name="Glyph">A Segoe Fluent Icons glyph before it, for a row that leaves the list: its settings.</param>
/// <param name="Separated">A line above it, for the row that leaves the list.</param>
/// <param name="Enabled">False for a row that says something and does nothing: "No VPN connections".</param>
internal sealed record SwitchChoice(string Text, string? Detail = null, bool? Checked = null, string? Glyph = null, bool Separated = false, bool Enabled = true)
{
    /// <summary>
    /// §404: for the row that says there is nothing to choose, where one is added (a Settings page), which the line
    /// offers as its Status's "Add"; null for none.
    /// </summary>
    public string? Add { get; init; }
}

internal static class QuickSwitches
{
    /// <summary>
    /// Where the switches come from. Windows', except in the tests, which hand over made-up ones so
    /// a picture can show a switch on without turning this PC's radios off. Per thread, so a test
    /// that sets it can't be seen by another beside it.
    /// </summary>
    [ThreadStatic]
    private static Func<QuickSwitch, IQuickSwitch?>? source;

    internal static Func<QuickSwitch, IQuickSwitch?>? Source
    {
        get => source;
        set => source = value;
    }

    /// <summary>The switch behind a one-button widget.</summary>
    /// <summary>
    /// The switch <paramref name="which"/>: the made-up one where a test has handed some over,
    /// Windows' own otherwise — and never Windows' own on a run that may not reach the person's
    /// world.
    /// <para>
    /// That last part is the guarantee, and it lives <b>here</b> rather than in the things that
    /// ask for a switch. A switch starts following what it stands for as soon as its data source
    /// is made (SwitchData) or its widget is first drawn, hovered or pressed (SwitchIsland.Follow),
    /// and both are exactly what the offscreen picture tests and <c>Karpo.exe --smoke</c> produce — through a dozen paths that
    /// would each have to remember to hand over a made-up switch. One of them forgetting means a
    /// test enumerating this PC's radios and querying its live network stack, which is what was
    /// happening: the smoke pass sets KARPO_QUICK_SAMPLES and says in the same line that no radio
    /// is read, and its bar's airplane and hotspot widgets read them anyway, because that flag
    /// reaches the flyout's pages (QuickSources.Samples) and never reached these. So the answer
    /// is the store's: an isolated run gets switches with nothing to reach, and a new caller
    /// cannot get it wrong. Where the smoke wants the real ones exercised natively it says so and
    /// builds them itself (<see cref="Windows"/>, called from Smoke.cs).
    /// </para>
    /// </summary>
    public static IQuickSwitch For(QuickSwitch which)
    {
        if (Source?.Invoke(which) is { } given)
        {
            return given;
        }
        // Windows' own is made either way, and then stood down. It is the one list of what each
        // kind's button does, so making it here is what keeps a kind nobody has said anything
        // about failing loudly in a test rather than only in somebody's dock. Making one reaches
        // nothing: the reaching is in Start.
        var real = Windows(which);
        if (!Config.IsIsolated && !QuickSources.UseSamples)
        {
            return real;
        }
        var stood = new SampleSwitch(real.Name, on: null, note: real.Note, switches: real.Switches);
        real.Dispose();
        return stood;
    }

    /// <summary>
    /// Windows' own switch <paramref name="which"/>. Making one asks Windows nothing; what it
    /// asks, it asks in Start. Called by <see cref="For"/> for a dock that may reach the person's
    /// world, and by the smoke pass, which wants these exercised in the published build however
    /// isolated it is.
    /// </summary>
    internal static IQuickSwitch Windows(QuickSwitch which) => which switch
    {
        QuickSwitch.AirplaneMode => new AirplaneSwitch(),
        QuickSwitch.MobileHotspot => new HotspotSwitch(),
        QuickSwitch.EnergySaver => new EnergySaverSwitch(),
        QuickSwitch.LiveCaptions => new ShortcutSwitch(TitleOf(which), S.DockQuickSwitchLiveCaptionsNote, [VK.VK_LWIN, VK.VK_CONTROL, 'L']),
        QuickSwitch.NightLight => new NightLightSwitch(),
        // Windows' own, shared with the bell and the notification list (DoNotDisturb.cs).
        QuickSwitch.DoNotDisturb => new DoNotDisturbSwitch(TitleOf(which), DoNotDisturb.Shared),
        QuickSwitch.Vpn => new VpnSwitch(),
        QuickSwitch.RotationLock => new RotationSwitch(),
        QuickSwitch.SpatialSound => new SpatialSwitch(),
        QuickSwitch.NearbyShare => new NearbySwitch(TitleOf(which)),
        QuickSwitch.Accessibility => new ShortcutSwitch(TitleOf(which), S.DockQuickSwitchAccessibilityNote, uri: "ms-settings:easeofaccess"),
        // Windows' Cast and Project tiles: their pages in a folder (FolderPages), their shortcuts elsewhere.
        QuickSwitch.Cast => new ShortcutSwitch(TitleOf(which), S.DockQuickSwitchCastNote, [VK.VK_LWIN, 'K']),
        QuickSwitch.Project => new ShortcutSwitch(TitleOf(which), S.DockQuickSwitchProjectNote, [VK.VK_LWIN, 'P']),
        // The radios as their lines switch them (RadioSwitch), and the default endpoints' mute.
        QuickSwitch.WifiRadio => new RadioToggle(S.DockQuickWifiTitle, Radios.RadioKind.WiFi),
        QuickSwitch.BluetoothRadio => new RadioToggle(S.DockQuickBluetoothTitle, Radios.RadioKind.Bluetooth),
        QuickSwitch.Sound => new MuteSwitch(S.DockQuickSoundTitle, EDataFlow.eRender),
        QuickSwitch.Microphone => new MuteSwitch(S.DockQuickSoundMicrophone, EDataFlow.eCapture),
        // Through the Settings handlers Windows' own switches go through (SettingToggles.cs).
        QuickSwitch.GameMode => new SettingToggle(TitleOf(which), S.DockQuickGameModeNote, SettingPlace.GameMode),
        QuickSwitch.ColorFilters => new SettingToggle(TitleOf(which), S.DockQuickColorFiltersNote, SettingPlace.ColorFilters),
        QuickSwitch.MonoAudio => new SettingToggle(TitleOf(which), S.DockQuickMonoAudioNote, SettingPlace.MonoAudio),
        // The accessibility page's other three (Assistive.cs).
        QuickSwitch.Magnifier or QuickSwitch.Narrator or QuickSwitch.StickyKeys => new AssistiveSwitch(which, WindowsAssistive.Shared),
        // Karpo's own power request, shared with its toggle and its menu (KeepAwake.cs).
        QuickSwitch.KeepAwake => new KeepAwakeSwitch(TitleOf(which), KeepAwake.Shared),
        // Windows' own mode, the apps' and its own together (DarkMode.cs).
        QuickSwitch.DarkMode => new DarkModeSwitch(TitleOf(which)),
        // No fall-back: this decides what the button *does*, so a switch added without an arm
        // here must fail loudly rather than ship as somebody else's shortcut.
        _ => throw new ArgumentOutOfRangeException(nameof(which), which, "Nobody has said what this switch's button does."),
    };

    /// <summary>What the button that stands for <paramref name="which"/> is called (<see cref="WidgetInfos.For(QuickSwitch)"/>).</summary>
    private static string TitleOf(QuickSwitch which) => WidgetInfos.For(which).Title;

    /// <summary>Sends a chord the way Windows' own shortcuts are pressed: the keys down in order, up in reverse.</summary>
    internal static unsafe void Chord(int[] keys)
    {
        var inputs = stackalloc INPUT[keys.Length * 2];
        for (var i = 0; i < keys.Length; i++)
        {
            inputs[i] = Stroke(keys[i], up: false);
            inputs[keys.Length * 2 - 1 - i] = Stroke(keys[i], up: true);
        }
        _ = SendInput((uint)(keys.Length * 2), inputs, sizeof(INPUT));
    }

    private static INPUT Stroke(int key, bool up)
    {
        var input = default(INPUT);
        input.type = INPUT.INPUT_KEYBOARD;
        input.ki.wVk = (ushort)key;
        input.ki.dwFlags = (key == VK.VK_LWIN ? (uint)KEYEVENTF_EXTENDEDKEY : 0) | (up ? (uint)KEYEVENTF_KEYUP : 0);
        return input;
    }
}

/// <summary>A made-up switch: what the tests and the pictures use in place of this PC's own.</summary>
internal sealed class SampleSwitch : IQuickSwitch
{
    public SampleSwitch(string name, bool? on, bool available = true, string? note = null, string? detail = null, bool switches = false, string? glyph = null)
    {
        Name = name;
        On = on;
        Available = available;
        Note = note ?? S.DockQuickSwitchSampleNote;
        Detail = detail;
        Switches = switches;
        Glyph = glyph;
    }

    public string Name { get; }

    /// <summary>The glyph it shows in place of the widget's, for a picture of one that changes it: the speaker struck through.</summary>
    public string? Glyph { get; }

    /// <summary>Whether it stands in for a switch whose click switches it (<see cref="IQuickSwitch.Switches"/>).</summary>
    public bool Switches { get; }

    public bool Available { get; }

    public bool? On { get; private set; }

    public string? Note { get; }

    /// <summary>What it is on as, for a picture of one that says more than "On": the VPN it is connected to.</summary>
    public string? Detail { get; }

    public void Start(Action changed)
    {
    }

    public void Act()
    {
        if (Available && On is { } state)
        {
            On = !state;
        }
    }

    public void Dispose()
    {
    }
}

/// <summary>Live captions, project, cast, nearby sharing, accessibility: a click does the one thing Windows offers.</summary>
internal sealed class ShortcutSwitch : IQuickSwitch
{
    private readonly int[]? chord;
    private readonly string? uri;

    public ShortcutSwitch(string name, string note, int[]? chord = null, string? uri = null)
    {
        Name = name;
        Note = note;
        this.chord = chord;
        this.uri = uri;
    }

    /// <summary>The keys it presses, for tests; null where it opens a page instead.</summary>
    internal IReadOnlyList<int>? Keys => chord;

    public string Name { get; }

    public bool Available => true;

    /// <summary>Windows tells no app whether these are on, so the widget shows none.</summary>
    public bool? On => null;

    public string? Note { get; }

    public void Start(Action changed)
    {
    }

    public void Act()
    {
        if (chord is { Length: > 0 } keys)
        {
            QuickSwitches.Chord(keys);
            return;
        }
        if (uri is { Length: > 0 } page)
        {
            Launch.Open(page);
        }
    }

    public void Dispose()
    {
    }
}

/// <summary>
/// Nearby sharing as Windows' own tile switches it (2026-10-11, §115): off, or on in the mode it was
/// last on in (Nearby.cs, which goes through the tile's own Settings handler). Whether it is on, and
/// who may share, is where Windows keeps it, watched; until that is read the switch shows no state,
/// and a click then opens its settings, as the button did.
/// </summary>
internal sealed class NearbySwitch : IQuickSwitch
{
    private readonly Func<INearbySource> make;
    private INearbySource? source;
    private Action? changed;

    public NearbySwitch(string name)
        : this(name, static () => new WindowsNearby())
    {
    }

    internal NearbySwitch(string name, Func<INearbySource> make)
    {
        Name = name;
        this.make = make;
    }

    public string Name { get; }

    public bool Available => true;

    public bool? On => Mode switch
    {
        NearbyMode.Off => false,
        NearbyMode.MyDevices or NearbyMode.Everyone => true,
        _ => null,
    };

    /// <summary>Switches it, once Windows has said where it stands.</summary>
    public bool Switches => Mode is not null;

    public string? Note => Mode is null ? S.DockQuickSwitchNearbyNote : S.DockQuickSwitchNearbyNoteOn;

    /// <summary>Who may share with this PC while it is on: "Everyone nearby".</summary>
    public string? Detail => Mode is NearbyMode.MyDevices or NearbyMode.Everyone ? NearbyModes.Title(Mode.Value) : null;

    private NearbyMode? Mode => source?.Read().Mode;

    public void Start(Action onChanged)
    {
        changed = onChanged;
        var made = make();
        source = made;
        // Its reads are on the pool (WindowsNearby.Start): this asks nothing of Windows here.
        made.Start(new Changes(_ => changed?.Invoke()));
    }

    /// <summary>Off, or on as it was last on; before Windows has said, its settings.</summary>
    public void Act()
    {
        if (source?.Read() is not { Mode: { } now } state)
        {
            Launch.Open("ms-settings:crossdevice");
            return;
        }
        source.Set(now == NearbyMode.Off ? state.Last : NearbyMode.Off);
    }

    public void Dispose()
    {
        source?.Dispose();
        source = null;
    }
}

/// <summary>
/// Airplane mode as Windows' own tile has it: the system-wide switch of the Radio Management
/// service, read and set through the interface Settings uses, and followed through the state the
/// service writes when anybody flips it — Windows' own panel included (SystemAirplane). Where the
/// interface is not there, the radios one by one (RadiosAirplane), which is what the tile comes to
/// on such a PC.
/// <para>
/// Which of the two it is, is found on the pool the first time the widget is looked at, and so is
/// where it stands: until then On is null and the button shows no state.
/// </para>
/// </summary>
internal sealed class AirplaneSwitch : IQuickSwitch
{
    private readonly Func<IAirplaneSource?> system;
    private readonly Func<IAirplaneSource> radios;
    private readonly object gate = new();
    private IAirplaneSource? source;
    private Action? changed;
    private volatile bool looked;
    private volatile string? note;
    private bool disposed;

    /// <summary>Windows' own: the service's switch, else the radios.</summary>
    public AirplaneSwitch()
        : this(SystemAirplane.Open, static () => new RadiosAirplane())
    {
    }

    /// <param name="system">The service's switch, or null where it is not there; called on the pool.</param>
    /// <param name="radios">The radios one by one, for a PC without it.</param>
    internal AirplaneSwitch(Func<IAirplaneSource?> system, Func<IAirplaneSource> radios)
    {
        this.system = system;
        this.radios = radios;
    }

    public string Name => S.DockQuickAirplaneName;

    /// <summary>A click switches it, as Windows' own tile does.</summary>
    public bool Switches => true;

    /// <summary>Until it has been found, it is offered; a PC with neither the service nor a radio says so.</summary>
    public bool Available => !looked || Ask(static found => found.Available) == true;

    public bool? On => Ask(static found => found.On);

    /// <summary>
    /// Asks what was found, on the dock's thread, where these are read while the bar is drawn: a
    /// source that throws answers nothing rather than taking the drawing, and the dock, down.
    /// </summary>
    private bool? Ask(Func<IAirplaneSource, bool?> question)
    {
        if (Volatile.Read(ref source) is not { } found)
        {
            return null;
        }
        try
        {
            return question(found);
        }
        catch (Exception error)
        {
            Troubles.Note("Airplane mode: reading it", error);
            return null;
        }
    }

    public string? Note => note ?? Volatile.Read(ref source)?.Note ?? S.DockQuickAirplaneNote;

    private int refusals;

    public int Refusals => Volatile.Read(ref refusals);

    /// <summary>What it reads and switches through, once found: for the tests.</summary>
    internal IAirplaneSource? Source => Volatile.Read(ref source);

    public void Start(Action onChanged)
    {
        changed = onChanged;
        // On the pool: making the service's object is a round trip to another process, and the
        // radios' lookup is a WinRT call.
        _ = Task.Run(Look);
    }

    public void Act() => _ = Task.Run(() => Switch(On != true));

    public void Dispose()
    {
        IAirplaneSource? found;
        lock (gate)
        {
            disposed = true;
            found = source;
            source = null;
        }
        found?.Dispose();
    }

    private void Look()
    {
        IAirplaneSource? found = null;
        try
        {
            found = system();
        }
        catch (Exception error)
        {
            Troubles.Note("Airplane mode", error);
        }
        found ??= radios();
        lock (gate)
        {
            if (disposed)
            {
                found.Dispose();
                return;
            }
            source = found;
        }
        try
        {
            found.Start(() => changed?.Invoke());
        }
        catch (Exception error)
        {
            Troubles.Note("Airplane mode", error);
        }
        looked = true;
        changed?.Invoke();
    }

    private void Switch(bool on)
    {
        if (Volatile.Read(ref source) is not { } found)
        {
            return;
        }
        bool done;
        try
        {
            done = found.Set(on);
        }
        catch (Exception error)
        {
            Troubles.Note("Airplane mode: switching", error);
            done = false;
        }
        note = done ? null : S.DockQuickAirplaneRefused;
        if (!done)
        {
            Interlocked.Increment(ref refusals);
        }
        changed?.Invoke();
    }
}

/// <summary>
/// The mobile hotspot, through the tethering manager of the connection this PC is on. Windows
/// refuses it to some apps and on some connections; then the widget says so and a click opens
/// Windows' own page for it.
/// </summary>
internal sealed class HotspotSwitch : IQuickSwitch
{
    /// <summary>
    /// Made on the pool and read on the dock's thread, which the tethering manager takes: one was
    /// made on an MTA thread and read from an STA one, and it answered.
    /// </summary>
    private volatile Operators.NetworkOperatorTetheringManager? manager;

    /// <summary>Whether the look has come back, which is not the same as it having found one.</summary>
    private volatile bool looked;

    private readonly Func<Operators.NetworkOperatorTetheringManager?> find;
    private readonly Func<Action, IDisposable?> follow;
    private readonly ITimer again;
    private Action? changed;
    private volatile string? note;
    private IDisposable? following;

    /// <summary>How long after the network's last news the state is read once more (<see cref="Heard"/>), in ms.</summary>
    internal const int AgainMs = 1500;

    public HotspotSwitch()
        : this(Find, Follow, TimeProvider.System)
    {
    }

    /// <param name="find">The tethering manager of the connection this PC is on, or null; called on the pool.</param>
    /// <param name="follow">Starts following what tells of the hotspot (<see cref="Follow"/>): the news comes from any thread.</param>
    /// <param name="clock">What the look once more after the news (<see cref="AgainMs"/>) is timed by.</param>
    internal HotspotSwitch(Func<Operators.NetworkOperatorTetheringManager?> find, Func<Action, IDisposable?> follow, TimeProvider clock)
    {
        this.find = find;
        this.follow = follow;
        again = clock.CreateTimer(static state => ((HotspotSwitch)state!).Look(), this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    // 1 while a look is waiting to start: the news that comes meanwhile is what it will read.
    private int looking;

    public string Name => S.DockQuickHotspotName;

    /// <summary>A click switches the hotspot where Windows lets it; where not, <see cref="On"/> is null and no switch is drawn.</summary>
    public bool Switches => true;

    /// <summary>
    /// Always offered: where Windows lets an app switch the hotspot the button does it, and where it
    /// doesn't a click opens Windows' own page for it. <see cref="On"/> is then null, so the button
    /// shows no state rather than one it made up.
    /// </summary>
    public bool Available => true;

    /// <summary>
    /// Whether the hotspot is on, asked of Windows every time — and null until the look has
    /// found a manager, which the widget draws as no state rather than as off.
    /// <para>
    /// <b>This read is deliberately not cached, and it is deliberately on the drawing thread.</b>
    /// It costs 223 microseconds, measured, which looks like exactly the sort of thing to hoist
    /// out of a paint. Do not. There is no state-changed event on the tethering manager to hoist
    /// it onto: NetworkStatusChanged is about the connection, and a person switching the hotspot
    /// in Windows' own Settings does not always raise one. What says it is the hotspot's adapter
    /// coming and going (InterfaceWatch, docs\widgets.md §439), which only says that something
    /// changed — so the state is asked when the button is drawn, and that news draws it. The dock
    /// has no render loop either — this widget's IntervalMs is 0 — so the 223 microseconds is per
    /// event that redraws the bar, not per frame. Staleness for microseconds is not a trade worth making.
    /// </para>
    /// </summary>
    public bool? On
    {
        get
        {
            try
            {
                return manager?.TetheringOperationalState switch
                {
                    Operators.TetheringOperationalState.On => true,
                    Operators.TetheringOperationalState.Off => false,
                    _ => null,
                };
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// What a click will do, once that is known. While the look is still going the line is the
    /// plain one: saying Windows will not allow it before anybody has asked is a tooltip that is
    /// wrong for as long as the asking takes.
    /// </summary>
    public string? Note => note ?? (looked && manager is null
        ? S.DockQuickHotspotNotHere
        : S.DockQuickHotspotNote);

    public void Start(Action onChanged)
    {
        changed = onChanged;
        // The look is live network-stack queries - 8-11 ms warm, 60 ms the first time in a
        // process, measured - so the dock's thread does not do it. Until it lands On is null and
        // the button draws with no state, which is what it draws for a connection Windows will
        // not share anyway.
        Look();
        following = follow(Heard);
    }

    /// <summary>
    /// The network said something changed, from any thread: the connection, or an interface or an
    /// address — which is all a hotspot switched in Windows' own panel tells (InterfaceWatch.cs). The
    /// manager is found again on the pool and the button drawn, and once more <see cref="AgainMs"/>
    /// after the last of a burst: the hotspot's adapter is given its address as it starts, and the
    /// state may say "in transition" until a moment after.
    /// </summary>
    internal void Heard()
    {
        Look();
        _ = again.Change(TimeSpan.FromMilliseconds(AgainMs), Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// The manager found on the pool, and the button told; one look for a burst of news, since one
    /// waiting to start reads what the rest would.
    /// </summary>
    private void Look()
    {
        if (Interlocked.Exchange(ref looking, 1) == 1)
        {
            return;
        }
        _ = Task.Run(Found);
    }

    /// <summary>On the pool: the look itself (see the top of this file for what it costs).</summary>
    private void Found() => Troubles.Guard("Quick settings: finding a connection", () =>
    {
        // News from here on is after what this reads: it asks for a look of its own.
        Volatile.Write(ref looking, 0);
        try
        {
            manager = find();
        }
        catch (Exception)
        {
            manager = null;
        }
        looked = true;
        changed?.Invoke();
    });

    /// <summary>
    /// What tells of the hotspot: the connection changing (NetworkStatusChanged), and an interface or
    /// an address coming or going (InterfaceWatch), which is what turning it on or off does.
    /// </summary>
    internal static IDisposable? Follow(Action heard) => new Following(heard);

    /// <summary>Both of <see cref="Follow"/>'s, let go together.</summary>
    private sealed class Following : IDisposable
    {
        private readonly InterfaceWatch interfaces;
        private Connectivity.NetworkStatusChangedEventHandler? networkChanged;

        public Following(Action heard)
        {
            interfaces = new InterfaceWatch(heard);
            networkChanged = _ => heard();
            try
            {
                Connectivity.NetworkInformation.NetworkStatusChanged += networkChanged;
            }
            catch (Exception)
            {
                networkChanged = null;
            }
        }

        public void Dispose()
        {
            interfaces.Dispose();
            if (networkChanged is { } handler)
            {
                try
                {
                    Connectivity.NetworkInformation.NetworkStatusChanged -= handler;
                }
                catch (Exception)
                {
                }
                networkChanged = null;
            }
        }
    }

    /// <summary>
    /// Switches it, or opens Windows' own page where Windows will not let an app. A click in the
    /// moment between the widget's first frame and the look landing opens the page too: there is
    /// nothing to switch yet, and the page is the answer that is never wrong.
    /// </summary>
    public void Act()
    {
        if (manager is null)
        {
            Launch.Open("ms-settings:network-mobilehotspot");
            return;
        }
        _ = Switch(On != true);
    }

    public void Dispose()
    {
        following?.Dispose();
        following = null;
        again.Dispose();
        changed = null;
        manager = null;
    }

    /// <summary>
    /// The tethering manager of the connection this PC is on, or null. Never on the dock's thread:
    /// this is the expensive one (see the top of this file).
    /// </summary>
    private static Operators.NetworkOperatorTetheringManager? Find()
    {
        var profile = Connectivity.NetworkInformation.GetInternetConnectionProfile();
        return profile is null ? null : Operators.NetworkOperatorTetheringManager.CreateFromConnectionProfile(profile);
    }

    private async Task Switch(bool on)
    {
        try
        {
            if (manager is not { } tethering)
            {
                return;
            }
            var result = on ? await tethering.StartTetheringAsync() : await tethering.StopTetheringAsync();
            var done = result.Status == Operators.TetheringOperationStatus.Success;
            note = done ? null : S.DockQuickHotspotFailed(Words(result.Status.ToString()));
            if (!done)
            {
                Interlocked.Increment(ref refusals);
            }
        }
        catch (Exception)
        {
            note = S.DockQuickHotspotRefused;
            Interlocked.Increment(ref refusals);
        }
        changed?.Invoke();
    }

    private int refusals;

    public int Refusals => Volatile.Read(ref refusals);

    private static string Words(string name) => WindowsBluetooth.Words(name);
}

/// <summary>
/// Energy saver, on and off as Windows' own tile switches it: through the tile's Settings handler
/// (SettingHandlers.cs), which is the one road Windows has for it — there is no public API that
/// switches it. Whether it is on is the power status's (<c>SystemStatusFlag</c>), read every time,
/// and the dock hands the power broadcasts to every island, which is when it is drawn again. Where
/// the handler is not there, a click opens its settings, as it always did.
/// </summary>
internal sealed class EnergySaverSwitch : IQuickSwitch
{
    private const uint PbtPowerStatusChange = 0x000A;
    private const uint PbtPowerSettingChange = 0x8013;

    private readonly Func<ISettingToggle?> handler;
    private readonly Func<PowerNow> power;
    private readonly Func<bool> supported;
    private readonly object gate = new();
    private ISettingToggle? tile;
    private Action? changed;
    private volatile bool usable = true;
    private volatile bool looked;
    private volatile string? note;
    private bool disposed;

    public EnergySaverSwitch()
        : this(static () => SettingHandler.Open(SettingHandler.EnergySaver), PowerNow.Read, WindowsBattery.SaverSupported)
    {
    }

    /// <param name="handler">The tile's own handler, or null where it is not there; called on the pool.</param>
    /// <param name="power">The power status as it is now.</param>
    /// <param name="supported">Whether energy saver exists on a PC without a battery (Windows 11 24H2 and later).</param>
    internal EnergySaverSwitch(Func<ISettingToggle?> handler, Func<PowerNow> power, Func<bool> supported)
    {
        this.handler = handler;
        this.power = power;
        this.supported = supported;
    }

    public string Name => S.DockQuickEnergySaver;

    /// <summary>Whether it goes through the tile's own handler, once it has looked: for the smoke pass.</summary>
    internal bool ThroughTile => Volatile.Read(ref tile) is not null;

    /// <summary>
    /// A PC with a battery has always had it, and since Windows 11 24H2 so has one on the wall —
    /// asked of Windows itself (WindowsBattery.SaverSupported), not guessed from a build number —
    /// and the tile's handler has to say it applies, as it does where Windows shows the tile.
    /// </summary>
    public bool Available => usable && (power().Battery || supported());

    public bool? On => power() is { Known: true } now ? now.Saving : null;

    /// <summary>Whether a click switches it (<see cref="Act"/>): once it has found the tile's own handler.</summary>
    public bool Switches => looked && Volatile.Read(ref tile) is not null;

    /// <summary>What a click does, once that is known: switches it, or opens its settings where nothing here can.</summary>
    public string? Note => note ?? (looked && Volatile.Read(ref tile) is null
        ? S.DockQuickEnergySaverNotHere
        : S.DockQuickEnergySaverNote);

    public void Start(Action onChanged)
    {
        changed = onChanged;
        // On the pool: the handler starts up on a thread of its own (IQuickSwitch.Start).
        _ = Task.Run(() =>
        {
            ISettingToggle? found = null;
            try
            {
                found = handler();
            }
            catch (Exception error)
            {
                Troubles.Note("Energy saver", error);
            }
            var offered = found?.Usable ?? true;
            lock (gate)
            {
                if (disposed)
                {
                    (found as SettingHandler)?.Release();
                    return;
                }
                tile = found;
            }
            usable = offered;
            looked = true;
            changed?.Invoke();
        });
    }

    /// <summary>Switches it through the tile's handler; before it has looked, or without one, opens its settings.</summary>
    public void Act()
    {
        if (!looked || Volatile.Read(ref tile) is null)
        {
            Launch.Open("ms-settings:batterysaver");
            return;
        }
        var on = On != true;
        _ = Task.Run(() => Switch(on));
    }

    /// <summary>Switches energy saver <paramref name="on"/> through the tile's handler; on the pool.</summary>
    internal void Switch(bool on)
    {
        if (Volatile.Read(ref tile) is not { } through)
        {
            return;
        }
        var done = through.Set(on);
        note = done ? null : S.DockQuickEnergySaverRefused;
        if (!done)
        {
            Interlocked.Increment(ref refusals);
        }
        changed?.Invoke();
    }

    private int refusals;

    public int Refusals => Volatile.Read(ref refusals);

    public void Heard(uint msg, nuint wParam)
    {
        if (msg == WM.WM_POWERBROADCAST && wParam is PbtPowerStatusChange or PbtPowerSettingChange)
        {
            changed?.Invoke();
        }
    }

    public void Dispose()
    {
        ISettingToggle? found;
        lock (gate)
        {
            disposed = true;
            found = tile;
            tile = null;
        }
        (found as SettingHandler)?.Release();
    }
}

/// <summary>
/// What the power status says about energy saver: whether it could be read, whether this PC has a
/// battery, and whether energy saver is on — energy saver's own status where Windows gave one
/// (<see cref="EnergySaver"/>), <c>SYSTEM_POWER_STATUS.SystemStatusFlag</c> where it never did.
/// </summary>
internal readonly record struct PowerNow(bool Known, bool Battery, bool Saving)
{
    /// <summary>This PC's, now: one GetSystemPowerStatus, cheap enough for every draw.</summary>
    public static PowerNow Read() => Read(EnergySaver.Shared.On);

    /// <summary>This PC's power status, with energy saver's own <paramref name="saver"/> status where there is one.</summary>
    public static unsafe PowerNow Read(bool? saver)
    {
        SYSTEM_POWER_STATUS status = default;
        return GetSystemPowerStatus(&status)
            ? new PowerNow(true, (status.BatteryFlag & 128) == 0, EnergySaver.Saving(status.SystemStatusFlag == 1, saver))
            : default;
    }
}
