// The status icons Windows 11's taskbar shows while something is so, for the Status icons entry
// (IndicatorsIsland): what each is, when it is shown, what its hover says, what a click opens and
// what a right click offers — the pure half, which the tests hold — and where each is read.
//
// The set is Windows' own, taken from its taskbar (SystemTray.dll's strings, and the pages it links):
//
//   privacy          the microphone, the camera, the location and presence sensing in use, each
//                    with the apps using it, as the privacy icon's hover lists them ("Microphone in
//                    use by:"). Windows draws the microphone and the location in its tray and says
//                    the camera with a flash on the screen; the entry shows all four the same way.
//   network          airplane mode (the network icon becomes the plane), the mobile hotspot on, and a
//                    metered connection. A VPN connected is not one of them: Windows shows it only as
//                    the shield badge on the network icon (F8C0, NetworkIcon.dll's
//                    NetworkIconVPNOverlay), and so does Karpo's network icon (SystemStatus).
//   power            energy saver on (the battery's leaf, and on a PC without one an icon of its own).
//   notifications    do not disturb (the bell's "zZ").
//   updates          Windows 11's update icon: updates waiting, downloading or installing, or a restart
//                    waiting to finish installing them. Windows' own is a notification icon of the
//                    update orchestrator's (WindowsUpdateIcon.cs), which the tray leaves to this one
//                    where a bar shows the status icons, so there is one icon for it, here.
//
// Karpo's own bar already has the rest: the network, sound and battery icons are widgets of their
// own, and Windows Security, "Safely remove hardware" and the pen and touch keyboard are
// notification icons the tray passes on untouched.
//
// Shown in that order, only while each is so. With none of them, the entry takes no room at all.
// The microphone and the location in use together are one icon, as they are in Windows' tray.
//
// EACH ICON IS WINDOWS' OWN GLYPH, a Segoe Fluent Icons code point, read from where Windows draws it:
//
//   microphone          E720   SystemTray.dll's privacy icon, alone with the microphone in use
//   location            E37A   the same icon, alone with the location in use (the arrow, not a pin)
//   microphone and      F47F   the same icon with both in use: SystemTray.dll picks E720, E37A or
//   location                   F47F from the two, and a click on F47F opens the privacy page
//   camera              E722   the "Camera on" flash on the screen (HardwareConfirmator), which is
//                              how Windows says it; Settings' camera privacy page has the same
//   presence sensing    E425   Settings' presence sensing privacy page; Windows' tray has none
//   airplane mode       E709   the network icon's own (NetworkIcon.dll), and Settings' page
//   mobile hotspot      E704   Settings' mobile hotspot page; the tray has none
//   metered             E8EC   none of Windows': it shows no sign of a metered connection anywhere
//                              on its taskbar, in its flyouts or in Settings' index, so Karpo's own
//   energy saver        F1E8   the tray's leaf, from its battery font (Sysbatt.ttf), which Segoe
//                              Fluent Icons has at the same code point
//   do not disturb      F285   the tray's bell with "zZ" while nothing is waiting (SystemTray.dll;
//                              F2A8, the filled one, is for notifications waiting)
//   updates             E895   the two arrows of Windows' own update icon files (WindowsUpdateSeeker,
//                              -Warning and -Important.ico), which are Windows Update's glyph in
//                              Settings, with the icon's dot in its place and size: the glyph in the
//                              one colour, the dot blue, orange or red as Windows' (RestartDot.cs,
//                              WindowsUpdate.Badge)
//
// Every one is drawn as the network, sound and battery icons are: the tray's 16 DIP on the 48-DIP
// bar, by its ink in the middle of its square (IndicatorsIsland, Fit.SystemIcon).
//
// WHERE EACH IS READ, all off the dock's thread and all followed rather than polled:
//
//   privacy          the CapabilityAccessManager's usage (HKCU and HKLM ...\CapabilityAccessManager\
//                    ConsentStore\<capability>): an app's key, packaged or under NonPackaged with its
//                    path's "\" written "#", is using it while LastUsedTimeStart is set and
//                    LastUsedTimeStop is 0. Windows' own icon asks the same service through an
//                    internal class with no public metadata; the registry is what that service writes,
//                    and it is watched.
//   airplane mode    the radio management service (RadioManager.cs), as the airplane switch reads it.
//   hotspot          the tethering manager of the connection in use, as the hotspot switch reads it.
//   metered          the connection in use's cost (ConnectionCost.NetworkCostType), again on each
//                    network change.
//   energy saver     the power status, again on each power broadcast.
//   do not disturb   Windows' own (DoNotDisturb.cs), shared with the bell and the switch.
//   updates          WindowsUpdate.cs: the keys Windows Update keeps while a restart is due, and the
//                    update orchestrator's word on what it is doing, watched; and Windows' own update
//                    icon in the tray, as the dock hears it (WindowsUpdateIcon.cs).

using Microsoft.Win32;
using Karpo.Core;
using Connectivity = Windows.Networking.Connectivity;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>Which status icon, in the order the entry shows them.</summary>
internal enum IndicatorKind
{
    Microphone,

    /// <summary>The microphone and the location in use together: one icon in Windows' tray, in the microphone's place.</summary>
    MicrophoneAndLocation,
    Camera,
    Location,
    Presence,
    AirplaneMode,
    Hotspot,
    Metered,
    EnergySaver,
    DoNotDisturb,

    /// <summary>Windows Update: updates waiting, downloading or installing, or a restart waiting for them.</summary>
    WindowsUpdate,
}

/// <summary>What a status icon's right click can do beyond opening a page (<see cref="IndicatorRow"/>).</summary>
internal enum IndicatorAct
{
    /// <summary>Opens the row's page.</summary>
    Open,
    TurnOffAirplaneMode,
    TurnOffHotspot,
    TurnOffEnergySaver,
    TurnOffDoNotDisturb,
}

/// <summary>A row of a status icon's right-click menu: a page to open, or something to turn off.</summary>
internal sealed record IndicatorRow(string Text, string Glyph, IndicatorAct Act, string? Uri = null);

/// <summary>One status icon, as the entry shows it.</summary>
/// <param name="Uri">What a click opens: the page Windows opens for it.</param>
/// <param name="Badge">The dot it wears, as Windows' update icon does: the update icon's alone.</param>
internal sealed record Indicator(IndicatorKind Kind, string Glyph, string Tooltip, string Uri, IReadOnlyList<IndicatorRow> Menu, UpdateBadge Badge = UpdateBadge.None);

/// <summary>What is so right now, as the sources read it. Made up whole in the tests.</summary>
internal sealed record IndicatorState
{
    public IReadOnlyList<string> Microphone { get; init; } = [];

    public IReadOnlyList<string> Camera { get; init; } = [];

    public IReadOnlyList<string> Location { get; init; } = [];

    public IReadOnlyList<string> Presence { get; init; } = [];

    public bool AirplaneMode { get; init; }

    public bool Hotspot { get; init; }

    public bool Metered { get; init; }

    public bool EnergySaver { get; init; }

    public bool DoNotDisturb { get; init; }

    /// <summary>Windows Update: a restart waiting, and what it is doing (WindowsUpdate.cs).</summary>
    public UpdateState Update { get; init; } = UpdateState.Nothing;

    /// <summary>Windows' own update icon in the tray, while it has one (WindowsUpdateIcon.cs): what is shown where <see cref="Update"/> says nothing.</summary>
    public WindowsUpdateIcon? WindowsIcon { get; init; }

    public static IndicatorState Nothing => new();
}

/// <summary>What the status icons are, for a state: which, in what order, and what each says. Pure.</summary>
internal static class Indicators
{
    private const string Settings = "\uE713";
    private const string Off = "\uE711";

    /// <summary>
    /// The glyph Windows draws for <paramref name="kind"/>, a Segoe Fluent Icons code point: where
    /// each comes from is at the top of this file.
    /// </summary>
    internal static string Glyph(IndicatorKind kind) => kind switch
    {
        IndicatorKind.Microphone => "\uE720",
        IndicatorKind.MicrophoneAndLocation => "\uF47F",
        IndicatorKind.Camera => "\uE722",
        IndicatorKind.Location => "\uE37A",
        IndicatorKind.Presence => "\uE425",
        IndicatorKind.AirplaneMode => "\uE709",
        IndicatorKind.Hotspot => "\uE704",
        IndicatorKind.Metered => "\uE8EC",
        IndicatorKind.EnergySaver => "\uF1E8",
        IndicatorKind.DoNotDisturb => Services.DoNotDisturb.Glyph,
        IndicatorKind.WindowsUpdate => WindowsUpdate.Glyph,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>
    /// The person's setting that stands for <paramref name="kind"/> (<see cref="StatusIcons"/>): its
    /// own, and none for the microphone and the location together, which is made only of two icons
    /// that stand in the same place (<see cref="Split"/>).
    /// </summary>
    internal static StatusIcon? Setting(IndicatorKind kind) => kind switch
    {
        IndicatorKind.Microphone => StatusIcon.Microphone,
        IndicatorKind.Camera => StatusIcon.Camera,
        IndicatorKind.Location => StatusIcon.Location,
        IndicatorKind.Presence => StatusIcon.Presence,
        IndicatorKind.AirplaneMode => StatusIcon.AirplaneMode,
        IndicatorKind.Hotspot => StatusIcon.Hotspot,
        IndicatorKind.Metered => StatusIcon.Metered,
        IndicatorKind.EnergySaver => StatusIcon.EnergySaver,
        IndicatorKind.DoNotDisturb => StatusIcon.DoNotDisturb,
        IndicatorKind.WindowsUpdate => StatusIcon.WindowsUpdate,
        _ => null,
    };

    /// <summary>
    /// The status icons of <paramref name="state"/> where <paramref name="options"/> put them (Karpo.Core\
    /// StatusIcons.cs): those on the bar, and those behind the entry's arrow, each in Windows' order.
    /// One hidden completely is in neither. The microphone and the location in use are one icon only
    /// where both stand in the same place; set apart, each shows alone in its own.
    /// </summary>
    internal static (IReadOnlyList<Indicator> OnBar, IReadOnlyList<Indicator> Behind) Split(IndicatorState state, StatusIconOptions? options) =>
        (Of(Only(state, icon => StatusIcons.State(options, icon) == TrayState.Taskbar)),
         Of(Only(state, icon => StatusIcons.State(options, icon) == TrayState.Overflow)));

    /// <summary><paramref name="state"/> with what <paramref name="keep"/> refuses said to be not so.</summary>
    private static IndicatorState Only(IndicatorState state, Func<StatusIcon, bool> keep)
    {
        var update = keep(StatusIcon.WindowsUpdate);
        return state with
        {
            Microphone = keep(StatusIcon.Microphone) ? state.Microphone : [],
            Camera = keep(StatusIcon.Camera) ? state.Camera : [],
            Location = keep(StatusIcon.Location) ? state.Location : [],
            Presence = keep(StatusIcon.Presence) ? state.Presence : [],
            AirplaneMode = state.AirplaneMode && keep(StatusIcon.AirplaneMode),
            Hotspot = state.Hotspot && keep(StatusIcon.Hotspot),
            Metered = state.Metered && keep(StatusIcon.Metered),
            EnergySaver = state.EnergySaver && keep(StatusIcon.EnergySaver),
            DoNotDisturb = state.DoNotDisturb && keep(StatusIcon.DoNotDisturb),
            Update = update ? state.Update : UpdateState.Nothing,
            WindowsIcon = update ? state.WindowsIcon : null,
        };
    }

    /// <summary>The status icons for <paramref name="state"/>, only those that are so, in Windows' order.</summary>
    internal static IReadOnlyList<Indicator> Of(IndicatorState state)
    {
        var shown = new List<Indicator>();
        // The microphone and the location in use together are one icon in Windows' tray, F47F, and
        // a click on it opens the privacy page rather than either one's; its hover names both.
        var both = state.Microphone.Count > 0 && state.Location.Count > 0;
        if (both)
        {
            var tip = InUseBy(S.DockQuickIndicatorMicrophoneInUse, state.Microphone) + "\n" + InUseBy(S.DockQuickIndicatorLocationInUse, state.Location);
            shown.Add(new Indicator(IndicatorKind.MicrophoneAndLocation, Glyph(IndicatorKind.MicrophoneAndLocation), tip, "ms-settings:privacy",
            [
                new(S.DockQuickIndicatorMicrophonePrivacy, Settings, IndicatorAct.Open, "ms-settings:privacy-microphone"),
                new(S.DockQuickIndicatorLocationPrivacy, Settings, IndicatorAct.Open, "ms-settings:privacy-location"),
            ]));
        }
        else
        {
            Privacy(shown, IndicatorKind.Microphone, state.Microphone, S.DockQuickIndicatorMicrophoneInUse, S.DockQuickIndicatorMicrophonePrivacy, "ms-settings:privacy-microphone");
        }
        Privacy(shown, IndicatorKind.Camera, state.Camera, S.DockQuickIndicatorCameraInUse, S.DockQuickIndicatorCameraPrivacy, "ms-settings:privacy-webcam");
        if (!both)
        {
            Privacy(shown, IndicatorKind.Location, state.Location, S.DockQuickIndicatorLocationInUse, S.DockQuickIndicatorLocationPrivacy, "ms-settings:privacy-location");
        }
        Privacy(shown, IndicatorKind.Presence, state.Presence, S.DockQuickIndicatorPresenceInUse, S.DockQuickIndicatorPresencePrivacy, "ms-settings:privacy-presencesensing");
        if (state.AirplaneMode)
        {
            shown.Add(new Indicator(IndicatorKind.AirplaneMode, Glyph(IndicatorKind.AirplaneMode), S.DockQuickIndicatorAirplaneOn, "ms-settings:network-airplanemode",
            [
                new(S.DockQuickIndicatorAirplaneOff, Off, IndicatorAct.TurnOffAirplaneMode),
                Page(WindowsPages.AirplaneMode),
            ]));
        }
        if (state.Hotspot)
        {
            shown.Add(new Indicator(IndicatorKind.Hotspot, Glyph(IndicatorKind.Hotspot), S.DockQuickIndicatorHotspotOn, "ms-settings:network-mobilehotspot",
            [
                new(S.DockQuickIndicatorHotspotOff, Off, IndicatorAct.TurnOffHotspot),
                Page(WindowsPages.MobileHotspot),
            ]));
        }
        if (state.Metered)
        {
            shown.Add(new Indicator(IndicatorKind.Metered, Glyph(IndicatorKind.Metered), S.DockQuickIndicatorMetered, "ms-settings:network-status",
            [
                Page(WindowsPages.Network),
                Page(WindowsPages.DataUsage) with { Glyph = "\uE9D2" },
            ]));
        }
        if (state.EnergySaver)
        {
            shown.Add(new Indicator(IndicatorKind.EnergySaver, Glyph(IndicatorKind.EnergySaver), S.DockQuickIndicatorEnergySaverOn, "ms-settings:batterysaver",
            [
                new(Dock.TurnText(WidgetAct.EnergySaver, on: false), Off, IndicatorAct.TurnOffEnergySaver),
                Page(WindowsPages.EnergySaver),
            ]));
        }
        if (state.DoNotDisturb)
        {
            shown.Add(new Indicator(IndicatorKind.DoNotDisturb, Glyph(IndicatorKind.DoNotDisturb), S.DockQuickIndicatorDndOn, "ms-settings:notifications",
            [
                new(Dock.TurnText(WidgetAct.DoNotDisturb, on: false), Off, IndicatorAct.TurnOffDoNotDisturb),
                Page(WindowsPages.Notifications),
            ]));
        }
        if (Update(state) is { } update)
        {
            shown.Add(update);
        }
        return shown;
    }

    /// <summary>
    /// The update icon, or null while there is nothing to say: Karpo's own reading of Windows Update
    /// where it says anything — "Updates available", "Downloading updates", "Installing updates",
    /// "Restart required", each with its dot — else Windows' own icon where the tray has it, with its
    /// hover and its dot (an orange one where its picture said none). A click opens Windows Update, as
    /// Windows' does, and the menu has two rows: its page and its restart options, never a restart
    /// from here.
    /// </summary>
    private static Indicator? Update(IndicatorState state)
    {
        var stage = WindowsUpdate.Shown(state.Update);
        string tip;
        UpdateBadge badge;
        if (stage != UpdateStage.None)
        {
            (tip, badge) = (WindowsUpdate.Status(stage), WindowsUpdate.Badge(state.Update));
        }
        else if (state.WindowsIcon is { } windows)
        {
            // Windows' hover, in Windows' words; its name where it has none.
            tip = windows.Tip.Trim().Length > 0 ? windows.Tip.Trim() : S.DockQuickIndicatorWindowsUpdate;
            badge = windows.Badge == UpdateBadge.None ? UpdateBadge.Warning : windows.Badge;
        }
        else
        {
            return null;
        }
        return new Indicator(IndicatorKind.WindowsUpdate, Glyph(IndicatorKind.WindowsUpdate), tip, WindowsUpdate.Page,
        [
            new(WindowsUpdate.PageTitle, WindowsUpdate.Glyph, IndicatorAct.Open, WindowsUpdate.Page),
            new(WindowsUpdate.RestartOptionsTitle, Settings, IndicatorAct.Open, WindowsUpdate.RestartOptions),
        ], badge);
    }

    /// <summary>A row opening <paramref name="page"/>, under its one name (WindowsPages, §308).</summary>
    private static IndicatorRow Page(WindowsLink page) => new(page.Title, Settings, IndicatorAct.Open, page.Uri);

    /// <summary>
    /// A privacy icon, where anything is using what it stands for: its hover names the apps as
    /// Windows' does ("Microphone in use by:" and one app a line), and its menu is that capability's
    /// privacy page. <paramref name="inUse"/> is the hover's first line ("Microphone in use by:"),
    /// <paramref name="settings"/> the menu's row ("Microphone privacy settings").
    /// </summary>
    private static void Privacy(List<Indicator> shown, IndicatorKind kind, IReadOnlyList<string> apps, string inUse, string settings, string uri)
    {
        if (apps.Count == 0)
        {
            return;
        }
        shown.Add(new Indicator(kind, Glyph(kind), InUseBy(inUse, apps), uri, [new(settings, Settings, IndicatorAct.Open, uri)]));
    }

    /// <summary>Windows' hover for a capability in use: "Microphone in use by:" and one app a line.</summary>
    private static string InUseBy(string inUse, IReadOnlyList<string> apps) => inUse + "\n" + string.Join("\n", apps);

    /// <summary>
    /// The apps using a capability, from its usage keys (see the top of this file): each key's name
    /// and its two times, an app using it while it has started and not stopped. A packaged app is
    /// named by its package ("Microsoft.WindowsCamera_8wekyb3d8bbwe" is "WindowsCamera"), a desktop
    /// one by <paramref name="describe"/> of its path ("C:#Program Files#obs-studio#…#obs64.exe").
    /// Named once each, in the order found.
    /// </summary>
    internal static IReadOnlyList<string> InUse(IEnumerable<(string Key, bool Packaged, long Start, long Stop)> usage, Func<string, string> describe)
    {
        var names = new List<string>();
        foreach (var (key, packaged, start, stop) in usage)
        {
            if (start == 0 || stop != 0)
            {
                continue;
            }
            var name = packaged ? PackageName(key) : describe(key.Replace('#', '\\'));
            if (name.Length > 0 && !names.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(name);
            }
        }
        return names;
    }

    /// <summary>"Publisher.Name_hash" is "Name"; anything else is itself.</summary>
    internal static string PackageName(string family)
    {
        var name = family;
        var cut = name.IndexOf('_');
        if (cut > 0)
        {
            name = name[..cut];
        }
        var dot = name.IndexOf('.');
        return dot >= 0 && dot < name.Length - 1 ? name[(dot + 1)..] : name;
    }
}

/// <summary>Where the status icons' state comes from: Windows, or made up in the tests and the pictures.</summary>
internal interface IIndicatorSource : IDisposable
{
    /// <summary>What was last read. Any thread may ask.</summary>
    IndicatorState State { get; }

    /// <summary>Starts reading and following; <paramref name="changed"/> comes from any thread. Must not block.</summary>
    void Start(Action changed);

    /// <summary>Energy saver may have changed: the dock forwards the power broadcasts.</summary>
    void PowerChanged();

    /// <summary>Does <paramref name="act"/>; on the pool where it reaches Windows.</summary>
    void Act(IndicatorAct act);
}

/// <summary>Where the entry's source comes from: Windows', or the samples on a run that may not reach the person's world.</summary>
internal static class IndicatorSources
{
    public static IIndicatorSource Source() =>
        Made?.Invoke() ?? (Config.IsIsolated || QuickSources.UseSamples ? new SampleIndicators() : new WindowsIndicators());

    /// <summary>A test's own source, per thread.</summary>
    [ThreadStatic]
    private static Func<IIndicatorSource>? made;

    internal static Func<IIndicatorSource>? Made
    {
        get => made;
        set => made = value;
    }
}

/// <summary>A made-up PC: what it shows is set by the test, and every act is kept.</summary>
internal sealed class SampleIndicators(IndicatorState? state = null) : IIndicatorSource
{
    private Action? changed;

    public IndicatorState State { get; set; } = state ?? IndicatorState.Nothing;

    public List<IndicatorAct> Acts { get; } = [];

    public void Start(Action onChanged) => changed = onChanged;

    public void PowerChanged()
    {
    }

    public void Act(IndicatorAct act) => Acts.Add(act);

    /// <summary>Says what changed, as Windows' own would.</summary>
    public void Tell(IndicatorState now)
    {
        State = now;
        changed?.Invoke();
    }

    public void Dispose()
    {
    }
}

/// <summary>Windows' own state for the status icons (see the top of this file).</summary>
internal sealed class WindowsIndicators : IIndicatorSource
{
    private const string Consent = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";

    /// <summary>The capabilities whose use Windows records, and which of the state's lists each fills.</summary>
    private static readonly string[] Capabilities = ["microphone", "webcam", "location", "humanPresence"];

    private readonly object gate = new();
    private readonly List<IDisposable> watches = [];
    private readonly Dictionary<string, string> described = new(StringComparer.OrdinalIgnoreCase);
    private IndicatorState state = IndicatorState.Nothing;
    private SystemAirplane? airplane;
    private HotspotSwitch? hotspot;
    private Connectivity.NetworkStatusChangedEventHandler? networkChanged;
    private Action? changed;
    private bool disposed;

    public IndicatorState State
    {
        get
        {
            lock (gate)
            {
                return state;
            }
        }
    }

    public void Start(Action onChanged)
    {
        changed = onChanged;
        // Everything is asked on the pool: registry walks, a service in another process, RAS.
        _ = Task.Run(() =>
        {
            foreach (var capability in Capabilities)
            {
                Follow(RegistryWatch.Open(RegistryHive.CurrentUser, $@"{Consent}\{capability}", subtree: true, Privacy));
                Follow(RegistryWatch.Open(RegistryHive.LocalMachine, $@"{Consent}\{capability}", subtree: true, Privacy));
            }
            // Updates are read from Windows Update's own keys, watched (WindowsUpdate.cs); and
            // Windows' own update icon as the dock hears it in the tray.
            foreach (var (key, subtree) in WindowsUpdateRegistry.Watched)
            {
                Follow(RegistryWatch.Open(RegistryHive.LocalMachine, key, subtree, () => Update(Updates)));
            }
            WindowsUpdateTray.Changed += TrayIconChanged;
            var radio = SystemAirplane.Open();
            radio?.Start(() => Update(Airplane));
            var tethering = new HotspotSwitch();
            tethering.Start(() => Update(Hotspot));
            networkChanged = _ => Update(Metered);
            try
            {
                Connectivity.NetworkInformation.NetworkStatusChanged += networkChanged;
            }
            catch (Exception)
            {
                networkChanged = null;
            }
            lock (gate)
            {
                if (disposed)
                {
                    radio?.Dispose();
                    tethering.Dispose();
                    WindowsUpdateTray.Changed -= TrayIconChanged;
                    return;
                }
                (airplane, hotspot) = (radio, tethering);
            }
            Update(state => Metered(Updates(TrayIcon(Airplane(Power(Privacy(state)))))));
        });
    }

    public void PowerChanged() => Update(Power);

    public void Act(IndicatorAct act) => _ = Task.Run(() =>
    {
        try
        {
            switch (act)
            {
                case IndicatorAct.TurnOffAirplaneMode:
                    airplane?.Set(false);
                    break;
                case IndicatorAct.TurnOffHotspot when hotspot is { On: true } tethering:
                    tethering.Act();
                    break;
                case IndicatorAct.TurnOffEnergySaver:
                    SettingHandler.Open(SettingHandler.EnergySaver)?.Set(false);
                    break;
            }
        }
        catch (Exception error)
        {
            Troubles.Note($"Status icons: {act}", error);
        }
        Update(state => Airplane(Power(state)));
    });

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            foreach (var watch in watches)
            {
                watch.Dispose();
            }
            watches.Clear();
            airplane?.Dispose();
            hotspot?.Dispose();
        }
        WindowsUpdateTray.Changed -= TrayIconChanged;
        if (networkChanged is { } handler)
        {
            try
            {
                Connectivity.NetworkInformation.NetworkStatusChanged -= handler;
            }
            catch (Exception)
            {
            }
        }
    }

    private void Follow(IDisposable? watch)
    {
        if (watch is null)
        {
            return;
        }
        lock (gate)
        {
            if (disposed)
            {
                watch.Dispose();
                return;
            }
            watches.Add(watch);
        }
    }

    private void Privacy() => Update(Privacy);

    private void TrayIconChanged() => Update(TrayIcon);

    /// <summary>Reads again what <paramref name="read"/> reads, and says so where anything moved.</summary>
    private void Update(Func<IndicatorState, IndicatorState> read)
    {
        IndicatorState before;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }
            before = state;
        }
        IndicatorState after;
        try
        {
            after = read(before);
        }
        catch (Exception error)
        {
            Troubles.Note("Status icons", error);
            return;
        }
        lock (gate)
        {
            state = after;
        }
        if (!Same(before, after))
        {
            changed?.Invoke();
        }
    }

    private static bool Same(IndicatorState a, IndicatorState b) =>
        a.Microphone.SequenceEqual(b.Microphone) && a.Camera.SequenceEqual(b.Camera) && a.Location.SequenceEqual(b.Location)
        && a.Presence.SequenceEqual(b.Presence)
        && (a.AirplaneMode, a.Hotspot, a.Metered, a.EnergySaver, a.DoNotDisturb, a.Update, a.WindowsIcon)
            == (b.AirplaneMode, b.Hotspot, b.Metered, b.EnergySaver, b.DoNotDisturb, b.Update, b.WindowsIcon);

    private IndicatorState Privacy(IndicatorState now) => now with
    {
        Microphone = InUse("microphone"),
        Camera = InUse("webcam"),
        Location = InUse("location"),
        Presence = InUse("humanPresence"),
    };

    private IndicatorState Airplane(IndicatorState now) => now with { AirplaneMode = airplane?.On == true };

    private IndicatorState Hotspot(IndicatorState now) => now with { Hotspot = hotspot?.On == true };

    private static IndicatorState Power(IndicatorState now) => now with { EnergySaver = PowerNow.Read().Saving };

    private static IndicatorState Metered(IndicatorState now)
    {
        var metered = false;
        try
        {
            if (Connectivity.NetworkInformation.GetInternetConnectionProfile() is { } profile)
            {
                metered = profile.GetConnectionCost().NetworkCostType is Connectivity.NetworkCostType.Fixed or Connectivity.NetworkCostType.Variable;
            }
        }
        catch (Exception)
        {
            // No connection to ask about: not metered.
        }
        return now with { Metered = metered };
    }

    private static IndicatorState Updates(IndicatorState now) => now with { Update = WindowsUpdateRegistry.Read() };

    private static IndicatorState TrayIcon(IndicatorState now) => now with { WindowsIcon = WindowsUpdateTray.Current };

    /// <summary>The apps using <paramref name="capability"/> now, the person's and the machine's.</summary>
    private IReadOnlyList<string> InUse(string capability)
    {
        var usage = new List<(string, bool, long, long)>();
        foreach (var hive in (RegistryHive[])[RegistryHive.CurrentUser, RegistryHive.LocalMachine])
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var key = root.OpenSubKey($@"{Consent}\{capability}", writable: false);
                if (key is null)
                {
                    continue;
                }
                Read(key, packaged: true, usage);
                using var desktop = key.OpenSubKey("NonPackaged", writable: false);
                if (desktop is not null)
                {
                    Read(desktop, packaged: false, usage);
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
            }
        }
        return Indicators.InUse(usage, Describe);
    }

    private static void Read(RegistryKey key, bool packaged, List<(string, bool, long, long)> usage)
    {
        foreach (var name in key.GetSubKeyNames())
        {
            if (packaged && name == "NonPackaged")
            {
                continue;
            }
            using var app = key.OpenSubKey(name, writable: false);
            if (app?.GetValue("LastUsedTimeStart") is long start)
            {
                usage.Add((name, packaged, start, app.GetValue("LastUsedTimeStop") is long stop ? stop : 0));
            }
        }
    }

    /// <summary>A desktop app's name as Windows gives it: its file's description, else its file's name. Kept.</summary>
    private string Describe(string path)
    {
        lock (gate)
        {
            if (described.TryGetValue(path, out var known))
            {
                return known;
            }
        }
        var name = Path.GetFileNameWithoutExtension(path);
        try
        {
            if (File.Exists(path) && System.Diagnostics.FileVersionInfo.GetVersionInfo(path).FileDescription is { Length: > 0 } description)
            {
                name = description.Trim();
            }
        }
        catch (Exception)
        {
        }
        lock (gate)
        {
            described[path] = name;
        }
        return name;
    }
}
