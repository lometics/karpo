// The mobile hotspot's page, in the quick settings' style: the switch in the
// title row, then what Windows' own hotspot page shows under "Properties" — the
// network's name, its password (hidden until the eye shows it, and copied by the
// button beside it), its band and how many devices are on it of how many it
// takes — then the devices themselves while it is on. It is a line of a list
// folder (QuickPageTile, 2026-10-10), the mobile hotspot preset's, rather than
// the flyout of the mobile hotspot button it was: its footer — "Mobile hotspot"
// for the flyout's "Mobile hotspot settings", and "Network & internet" — is the
// actions line under it (PremadeWidgets.HotspotActions).
//
// Where this PC can't host a hotspot the switch is left out and the page says
// why in the words for that reason (no connection to share, no adapter that can,
// an administrator's policy, the connection or edition not allowing it), as the
// other pages say "No Bluetooth": a switch that cannot work would be a lie.
//
// While the page is up it asks who is on the hotspot every few seconds, on its
// own wake (there is no event for a device joining); closed, it asks nothing.

using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using Win32 = TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock.QuickSettings;

internal sealed class HotspotPage : QuickPage
{
    private static readonly string Rune = char.ConvertFromUtf32(0xE704);

    // Segoe Fluent Icons: View and Hide (the eye, and the eye struck out), Copy, the tick it
    // turns into for a moment once copied, and Connect for a device on the hotspot.
    private static readonly string Eye = char.ConvertFromUtf32(0xE7B3);
    private static readonly string NoEye = char.ConvertFromUtf32(0xED1A);
    private static readonly string CopyGlyph = char.ConvertFromUtf32(0xE8C8);
    private static readonly string Tick = char.ConvertFromUtf32(0xE73E);
    private static readonly string Device = char.ConvertFromUtf32(0xE703);

    /// <summary>How often who is on it is asked again while the page is open.</summary>
    private const int PollMs = 5_000;

    /// <summary>How long Copy shows its tick.</summary>
    private const int CopiedMs = 1_500;

    private readonly IHotspotSource source;
    private readonly Label title = new(S.DockQuickHotspotName, LabelStyle.Strong);
    private readonly ToggleSwitch toggle = new() { Name = S.DockQuickHotspotName };
    private readonly Label problem = new("", LabelStyle.Critical, lines: 3);
    private readonly Notice notice = new(Rune);
    private readonly Progress asking = new(S.DockQuickAskingWindows);
    private readonly Label heading = new(S.DockQuickHotspotProperties, LabelStyle.Strong);
    private readonly (Label Caption, Label Value) name = Pair(S.DockQuickHotspotNetworkName);
    private readonly (Label Caption, Label Value) password = Pair(S.DockQuickHotspotPassword);
    private readonly (Label Caption, Label Value) band = Pair(S.DockQuickHotspotBand);
    private readonly (Label Caption, Label Value) clients = Pair(S.DockQuickHotspotDevices);
    private readonly Button show = new("", ButtonStyle.Subtle, Eye) { Name = S.DockQuickHotspotShowPassword };
    private readonly Button hide = new("", ButtonStyle.Subtle, NoEye) { Name = S.DockQuickHotspotHidePassword };
    private readonly Button copy = new("", ButtonStyle.Subtle, CopyGlyph) { Name = S.DockQuickHotspotCopyPassword };
    private readonly Keyed<ListRow> rows = new();
    private HotspotState state = HotspotState.Unknown;
    private bool copied;

    public HotspotPage(IQuickHost host, IHotspotSource source)
        : base(host)
    {
        this.source = source;
        toggle.Clicked = () =>
        {
            toggle.On = !toggle.On;
            source.Switch(toggle.On);
        };
        show.Clicked = () =>
        {
            Revealed = true;
            Host.Rebuild();
        };
        hide.Clicked = () =>
        {
            Revealed = false;
            Host.Rebuild();
        };
        copy.Clicked = () =>
        {
            if (state.Password.Length == 0)
            {
                return;
            }
            // The clipboard takes text only from an owner: the panel's window, or in a folder's card
            // the window that is active, the folder's, as a field's Copy there has it (TextField).
            source.Copy(copy.Panel is { } panel ? panel.Hwnd : Win32.GetActiveWindow(), state.Password);
            copied = true;
            Host.Wake(CopiedMs);
            Host.Rebuild();
        };
    }

    public override QuickKind Kind => QuickKind.Hotspot;

    /// <summary>Whether the password shows as itself rather than as dots. Hidden every time the page opens.</summary>
    internal bool Revealed { get; private set; }

    public override void Start()
    {
        source.Start(Changes);
        state = source.Read();
        Host.Rebuild();
        Host.Wake(PollMs);
    }

    protected override void Refresh(int news)
    {
        state = source.Read();
        // The page's own wake: the tick goes back to Copy, and who is on the hotspot is asked again.
        if (news == 0)
        {
            copied = false;
            source.Poll();
            Host.Wake(PollMs);
        }
    }

    public override void Build(PageLayout layout)
    {
        var context = layout.Context;
        if (state is { Can: HotspotCan.Yes, On: { } on })
        {
            toggle.On = on;
            toggle.Enabled = !state.Busy;
            layout.Title(title, (toggle, context.D(44)));
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
        if (HotspotText.Why(state.Can) is { } why)
        {
            Notice(layout, notice.Say(Rune, why.Title, why.Detail));
        }
        else if (state.Can == HotspotCan.Looking)
        {
            layout.Gap(4);
            layout.Place(asking, layout.TextLeft, layout.TextRight - layout.TextLeft, context.T(32));
        }
        else
        {
            Properties(layout);
            Devices(layout);
        }
        rows.Done();
    }

    /// <summary>The network's name, its password with the eye and Copy, its band and the count, as Windows' page lists them.</summary>
    private void Properties(PageLayout layout)
    {
        var context = layout.Context;
        layout.Heading(heading);
        Property(layout, name, state.Name);
        password.Value.Text = Revealed ? state.Password : HotspotText.Hidden(state.Password);
        copy.Glyph = copied ? Tick : CopyGlyph;
        copy.Enabled = state.Password.Length > 0;
        var top = layout.Y;
        var side = context.T(32);
        var right = layout.RowRight - context.D(4);
        var eye = Revealed ? hide : show;
        eye.Enabled = state.Password.Length > 0;
        var buttonsLeft = right - (2 * side) - context.D(4);
        Property(layout, password, password.Value.Text, buttonsLeft - context.D(8));
        var middle = PopupPaint.Round(top + ((layout.Y - top - side) / 2));
        layout.Put(copy, PopupPaint.Rect(right - side, middle, side, side));
        layout.Put(eye, PopupPaint.Rect(right - (2 * side) - context.D(4), middle, side, side));
        if (state.Band is { } set)
        {
            Property(layout, band, HotspotText.Band(set));
        }
        Property(layout, clients, HotspotText.Devices(state.Clients, state.MaxClients));
    }

    /// <summary>The devices on it now, while it is on: the name each gave, its address under it.</summary>
    private void Devices(PageLayout layout)
    {
        var context = layout.Context;
        if (state.On != true || state.Devices.Count == 0)
        {
            return;
        }
        layout.Gap(4);
        foreach (var client in state.Devices)
        {
            var key = client.Address.Length > 0 ? client.Address : client.Name;
            var row = rows.Get(key, () => new ListRow { Name = key });
            row.Title = HotspotText.Title(client);
            row.Detail = client.Name.Length > 0 ? client.Address : null;
            row.Glyph = Device;
            layout.Row(row, context.T(client.Name.Length > 0 ? 52 : 40));
            layout.Gap(2);
        }
    }

    private static (Label Caption, Label Value) Pair(string caption) =>
        (new Label(caption, LabelStyle.Caption), new Label("", LabelStyle.Body));

    /// <summary>One property: what it is in the caption's ink, and its value under it, up to <paramref name="right"/>.</summary>
    private static void Property(PageLayout layout, (Label Caption, Label Value) pair, string value, float? right = null)
    {
        var context = layout.Context;
        var width = MathF.Max(1, (right ?? layout.TextRight) - layout.TextLeft);
        pair.Value.Text = value;
        layout.Gap(4);
        layout.Place(pair.Caption, layout.TextLeft, width, context.T(16));
        layout.Place(pair.Value, layout.TextLeft, width, context.T(22));
        layout.Gap(4);
    }

    public override void Dispose() => source.Dispose();
}
