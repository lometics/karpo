// The data sources whose data Windows tells as it changes (DataSources): Windows' security state,
// a one-button widget's switch, and the output volume.
//
// Each of these was the widget's own: every security widget registered with Security Center for
// itself, every airplane or hotspot button made and started a switch of its own, every volume
// slider opened Core Audio for itself — so sixteen hotspot buttons in a folder were sixteen looks
// at the network stack, and a widget in a folder nobody had opened asked nothing until it was
// drawn, which is the moment it had to say something. Now each kind is one source, started at the
// dock's start for every widget of it the config names, and the widgets read it: the state is
// there when a folder opens or a layout is shown, and one change of it wakes every widget of it.
//
// A test that hands a widget a stand-in of its own (Security.Made, QuickSwitches.Source,
// VolumeWatch.Stand, each per thread) keeps it: such a widget is not given the shared source,
// because what a test hands one widget must not reach another, nor the other way round.

using Karpo.Core;
using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Dock;

/// <summary>
/// A source whose data changes on its own and is told, not gathered: the widgets that show it are
/// woken when it changes, and read it then.
/// </summary>
internal abstract class ToldData(SourceKey key) : DataSource(key)
{
    private readonly List<Action> listeners = [];

    /// <summary>Nothing ticks: what it stands for says when it changed.</summary>
    public override int IntervalMs => 0;

    /// <summary>A widget shows this data: it is woken, from any thread, when it changes.</summary>
    public void Listen(Action wake)
    {
        lock (listeners)
        {
            listeners.Add(wake);
        }
    }

    /// <summary>That widget is gone.</summary>
    public void Unlisten(Action wake)
    {
        lock (listeners)
        {
            listeners.Remove(wake);
        }
    }

    /// <summary>How many widgets are listening, for a test.</summary>
    internal int Listening
    {
        get
        {
            lock (listeners)
            {
                return listeners.Count;
            }
        }
    }

    protected override bool Gather(long now) => false;

    /// <summary>What it stands for changed: every widget of it is woken.</summary>
    protected void Changed()
    {
        Action[] all;
        lock (listeners)
        {
            all = [.. listeners];
        }
        foreach (var wake in all)
        {
            wake();
        }
    }
}

/// <summary>Windows' security state, followed from the dock's start (Security Center's own callback).</summary>
internal sealed class SecurityData() : ToldData(KeyOf())
{
    public static SourceKey KeyOf() => new("security");

    /// <summary>What Windows says: read it any time.</summary>
    public ISecuritySource Windows { get; } = Security.Source();

    protected internal override void Start() => Windows.Start(Changed);

    public override void Dispose() => Windows.Dispose();
}

/// <summary>
/// One switch — airplane mode, the hotspot, energy saver, the night light and the rest — followed
/// from the dock's start, however many buttons of it there are.
/// </summary>
internal sealed class SwitchData : ToldData
{
    public SwitchData(QuickSwitch which)
        : base(KeyOf(which)) => Switch = QuickSwitches.For(which);

    /// <summary>
    /// A toggles row's toggle (Karpo.Core\Toggles.cs): the one-button widget's own source where the
    /// toggle is one — the same key, so a row and a button read one switch — and its radio's for Wi-Fi
    /// and Bluetooth.
    /// </summary>
    public SwitchData(Toggle toggle)
        : base(KeyOf(toggle)) => Switch = ToggleSwitches.For(toggle);

    public static SourceKey KeyOf(QuickSwitch which) => new("switch", which.ToString());

    /// <summary>A toggle's source: its one-button widget's, so a row and the widget read one switch ("switch:WifiRadio").</summary>
    public static SourceKey KeyOf(Toggle toggle) =>
        Toggles.Find(toggle)?.Switch is { } which ? KeyOf(which) : new("switch", toggle.ToString());

    /// <summary>The switch: read it, or act on it, any time.</summary>
    public IQuickSwitch Switch { get; }

    protected internal override void Start() => Switch.Start(Changed);

    /// <summary>The power broadcasts, which energy saver follows: once, for every button of it.</summary>
    protected internal override void Hear(uint msg, nuint wParam) => Switch.Heard(msg, wParam);

    public override void Dispose() => Switch.Dispose();
}

/// <summary>The output's volume and mute, followed from the dock's start (Core Audio's own callbacks).</summary>
internal sealed class VolumeData() : ToldData(KeyOf())
{
    public static SourceKey KeyOf() => new("volume");

    private VolumeWatch? watch;

    /// <summary>The output's volume: read it, or set it, any time after the start.</summary>
    public VolumeWatch Watch => watch ?? throw new InvalidOperationException("The volume is not followed yet.");

    protected internal override void Start() => watch = new VolumeWatch(Changed);

    public override void Dispose() => watch?.Dispose();
}
