// Windows 11's quick-settings Bluetooth page, on its own: the switch in the
// title row and the paired devices (connected first), nothing else. It is a line
// of a list folder (QuickPageTile, 2026-10-10), the Bluetooth preset's, rather
// than a flyout of its own: its footer — "Bluetooth & devices" and "Add device",
// which opens Windows' Bluetooth settings with their own "Add device" at the top —
// is the actions line under it (SettingsActions.AddDeviceId). Choosing a
// device opens it in place with what Windows' own page offers: connect or
// disconnect a paired audio device (its driver's own request, BluetoothAudio.cs),
// or remove it. A mouse, a keyboard or a phone has no connect in Windows, and
// none here. A new device is Windows' to find and pair.
//
// A device that reports its battery shows it at the right of its row as Windows'
// Bluetooth page does: a battery glyph filled to the level (in tenths, Windows'
// own steps) and the percentage beside it. The room for both is kept on every row
// whether it reports or not — a level that arrives a second after the list does
// must not shorten the name beside it. Nothing is shown for a device that says
// nothing, which is most of them (Services\BluetoothNodes.cs): null is not zero.
//
// What updates while the page is open, and what it costs closed: the devices
// themselves are a DeviceWatcher's events, so a headset connecting redraws the
// row with no timer at all. The battery has no event — it lives on another
// device node — so it is asked for when a device connects and again on the
// page's own wake while it is open. Closed, the page is disposed, the watchers
// stop and nothing is asked for: no timer outlives the panel.
//
// The line is as tall as the devices paired, a device opened in place included.

using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock.QuickSettings;

internal sealed class BluetoothPage : QuickPage
{
    private static readonly string Rune = char.ConvertFromUtf32(0xE702);

    private readonly IBluetoothSource source;
    private readonly Label title = new(S.DockQuickBluetoothTitle, LabelStyle.Strong);
    private readonly ToggleSwitch radio = new() { Name = S.DockQuickBluetoothTitle };
    private readonly Label problem = new("", LabelStyle.Critical, lines: 3);
    private readonly Notice notice = new(Rune);
    private readonly Label none = new(S.DockQuickBluetoothNoDevices, LabelStyle.Caption);
    private readonly Progress working = new("");
    private readonly Keyed<ListRow> rows = new();

    /// <summary>The widest a level ever reads, whose room every row keeps (ListRow.TailRoom).</summary>
    private static string WidestLevel => S.DockQuickPercent(100);

    /// <summary>How often the levels are asked for again while the page is open.</summary>
    private const int BatteryMs = 30_000;
    private readonly Button remove = new(S.DockQuickBluetoothRemoveDevice) { Name = "Remove device" }; // not-ui: the automation id
    private readonly Button connect = new(S.DockQuickConnect, ButtonStyle.Accent) { Name = "Connect" };
    private readonly Button disconnect = new(S.DockQuickDisconnect) { Name = "Disconnect" };
    private BluetoothState state = new(null, false, [], null, null);
    private string? open;

    public BluetoothPage(IQuickHost host, IBluetoothSource source)
        : base(host)
    {
        this.source = source;
        radio.Clicked = () =>
        {
            radio.On = !radio.On;
            source.SetRadio(radio.On);
        };
        remove.Clicked = () =>
        {
            if (open is { } id)
            {
                source.Remove(id);
            }
        };
        connect.Clicked = () =>
        {
            if (open is { } id)
            {
                source.Connect(id, true);
            }
        };
        disconnect.Clicked = () =>
        {
            if (open is { } id)
            {
                source.Connect(id, false);
            }
        };
    }

    public override QuickKind Kind => QuickKind.Bluetooth;

    public override void Start()
    {
        source.Start(Changes);
        state = source.Read();
        Host.Rebuild();
        // The levels, again, for as long as the page is up (see the top of this file).
        Host.Wake(BatteryMs);
    }

    protected override void Refresh(int news)
    {
        var was = state;
        state = source.Read();
        // The wake this page asked for: nothing to read, so it is the levels that are due. Asking
        // again arms the next one, and closing the page takes the timer with it.
        if (news == 0)
        {
            source.Poll();
            Host.Wake(BatteryMs);
        }
        // A device removed: its card closes.
        if (was.Busy is { } done && state.Busy is null && open == done)
        {
            open = null;
        }
        if (open is not null && !state.Devices.Any(device => device.Id == open))
        {
            open = null;
        }
    }

    public override void Build(PageLayout layout)
    {
        var context = layout.Context;
        if (state.RadioOn is not null && state.CanSwitch)
        {
            radio.On = state.RadioOn == true;
            layout.Title(title, (radio, context.D(44)));
        }
        else
        {
            layout.Title(title);
        }
        if (state.Problem is { Length: > 0 } trouble)
        {
            problem.Text = trouble;
            layout.Gap(4);
            Text(layout, problem, layout.TextLeft, layout.TextRight);
            layout.Gap(8);
        }
        if (state.RadioOn is null)
        {
            Notice(layout, notice.Say(Rune, S.DockQuickBluetoothNone, S.DockQuickBluetoothNoneDetail));
        }
        else if (state.RadioOn == false)
        {
            Notice(layout, notice.Say(Rune, S.DockQuickBluetoothOff, S.DockQuickBluetoothOffDetail));
        }
        else
        {
            Devices(layout);
        }
        rows.Done();
    }

    /// <summary>The paired devices, a row each, or a line saying there are none.</summary>
    private void Devices(PageLayout layout)
    {
        var context = layout.Context;
        layout.Gap(2);
        var paired = state.Devices.Where(device => device.Paired).ToList();
        if (paired.Count == 0)
        {
            layout.Place(none, layout.TextLeft, layout.TextRight - layout.TextLeft, context.T(32));
        }
        foreach (var device in paired)
        {
            Device(layout, device);
        }
    }

    /// <summary>How far down the list one device's row takes: the row and the gap under it.</summary>
    internal static float RowPitch(QuickContext context) => context.T(56) + context.D(2);

    private void Device(PageLayout layout, BluetoothDevice device)
    {
        var context = layout.Context;
        var row = rows.Get(device.Id, () => new ListRow { Name = device.Name });
        row.Title = device.Name;
        row.Detail = device.Connected ? S.DockQuickConnected : S.DockQuickBluetoothPaired;
        row.Strong = device.Connected;
        row.Glyph = device.Glyph;
        // The level where the device gives one, its glyph beside it, in room every row keeps
        // either way.
        row.Tail = Level(device);
        row.TailGlyph = BatteryGlyph(device);
        row.TailRoom = WidestLevel;
        row.TailGlyphRoom = true;
        row.Clicked = () =>
        {
            open = open == device.Id ? null : device.Id;
            Host.Rebuild();
        };
        if (open != device.Id)
        {
            layout.Row(row, context.T(56));
            layout.Gap(2);
            return;
        }
        layout.Row(row, context.T(56));
        var left = layout.ListTextLeft;
        var right = layout.ListTextRight;
        layout.Gap(4);
        if (state.Busy == device.Id)
        {
            working.Text = Working(state.Connecting);
            layout.Place(working, left, right - left, context.T(32));
        }
        else
        {
            if (Offers(device) is { } link)
            {
                layout.Buttons(right, link ? disconnect : connect, remove);
            }
            else
            {
                layout.Buttons(right, remove);
            }
        }
        layout.Gap(12);
        layout.Opened(row);
        layout.Gap(2);
    }

    /// <summary>
    /// Whether a device's card offers to disconnect it (true), to connect it (false), or neither
    /// (null): a paired audio device, as Windows' page offers it. Pure: the tests check it.
    /// </summary>
    internal static bool? Offers(BluetoothDevice device) => device is { Paired: true, Audio: true } ? device.Connected : null;

    /// <summary>What a device's card says while something is asked of it.</summary>
    internal static string Working(bool? connecting) => connecting switch
    {
        true => S.DockQuickConnecting,
        false => S.DockQuickDisconnecting,
        _ => S.DockQuickBluetoothRemoving,
    };

    /// <summary>
    /// What a device's row says about its battery: its level, or nothing at all. A device that
    /// reports nothing has not reported zero, and a row that said "0%" for a headset that never
    /// answers would be worse than a row that says nothing.
    /// </summary>
    internal static string? Level(BluetoothDevice device) =>
        device.Battery is { } percent ? S.DockQuickPercent(percent) : null;

    /// <summary>
    /// The battery glyph beside the level: Windows' own, filled in tenths to the nearest, the steps
    /// its battery icon takes (SystemStatus.Describe); nothing where the device reports nothing.
    /// </summary>
    internal static string? BatteryGlyph(BluetoothDevice device) =>
        device.Battery is { } percent ? SystemStatus.Describe(new SystemStatus.Battery(Math.Clamp(percent, 0, 100), false, false)).Glyph : null;

    public override void Dispose() => source.Dispose();
}
