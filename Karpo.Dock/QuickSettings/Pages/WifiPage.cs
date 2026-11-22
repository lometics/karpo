// The network widget's page: Windows 11's quick-settings Wi-Fi page, with the wired
// connections at the top of its list. Its one title is Windows' "Wi-Fi", with the
// radio's switch (and a refresh button beside it) at its right; the widget's own
// name, "Network", is not said again over it (the person, 2026-10-05: "the network
// flyout should not have a separate network title all the way on the top. only the
// wlan one"). It is a line of a list folder (QuickPageTile, 2026-10-10), the Wi-Fi
// preset's, rather than a flyout of its own: its footer is the actions line under it.
//
// Each cable that is in has a row first, under the title and above the networks, as
// Windows' list of connections had them, with the Ethernet glyph, the network's name
// and whether it reaches the internet (Wired.cs). Choosing one opens it in place, as
// a Wi-Fi network opens: its link speed, its IPv4 address and its network profile
// where Windows says them, and Properties, Windows' own page for the connection.
// There is no Disconnect: Windows has none for a cable short of turning the adapter
// off, which asks for an administrator. A cable pulled out takes its row with it; on
// a PC without Wi-Fi and with no cable in at all, each adapter says its cable is
// unplugged rather than the page going quiet.
//
// Then the networks in range with their signal and a lock when secured, the
// connected one first. Choosing a network opens it in place as Windows does:
// "Connect automatically" and Connect, then the security key when the network needs
// one (Next, Cancel); the connected one offers Disconnect and its properties —
// Windows' own page for that network. A PC with a cable and no Wi-Fi has no title
// row at all: its cables are the page, and a line under them says there is no Wi-Fi
// rather than the big notice.
//
// Two more steps, in Windows' own order: the "Hidden network" asks for the name of
// the network first (Next), then its key where it needs one; an enterprise network
// Windows has no credentials for asks for a user name and a password (OK), and
// Windows then shows the server's certificate for the person to trust.
//
// The line is as tall as what the page shows, a network opened in place included;
// past what the folder may be, the folder scrolls.

using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Dock.QuickSettings;

internal sealed class WifiPage : QuickPage
{
    // Segoe Fluent Icons: the Wi-Fi bars from one to four, a lock, refresh, a warning.
    private static readonly string[] Bars = [Glyph(0xE872), Glyph(0xE873), Glyph(0xE874), Glyph(0xE701)];
    private static readonly string FullBars = Glyph(0xE701);
    private static readonly string Lock = Glyph(0xE72E);
    private static readonly string RefreshGlyph = Glyph(0xE72C);

    /// <summary>
    /// The page's one title: Windows' Wi-Fi page's. The widget is "Network", but the flyout says
    /// so nowhere; the cables sit in the list under this title.
    /// </summary>
    internal static string Title => S.DockQuickWifiTitle;

    /// <summary>What a PC with a cable and no Wi-Fi says under its cables.</summary>
    internal static string NoWifiLine => S.DockQuickWifiNoAdapterLine;

    // A wired row's key: never a network's, whose key is its SSID in hex.
    private const string WiredKey = "wired:";

    private readonly IWifiSource source;
    private readonly IWiredSource wiredSource;
    private readonly Label title = new(Title, LabelStyle.Strong);
    private readonly Label noWifi = new(NoWifiLine, LabelStyle.Caption, lines: 2);
    private readonly ToggleSwitch radio = new() { Name = S.DockQuickWifiTitle };
    private readonly Button refresh = new("", ButtonStyle.Subtle, RefreshGlyph) { Name = S.DockQuickWifiRefresh };
    private readonly Label problem = new("", LabelStyle.Critical, lines: 3);
    private readonly Notice notice = new(FullBars);
    private readonly Keyed<ListRow> rows = new();
    private readonly CheckBox automatic = new(S.DockQuickWifiAutomatic, true) { Name = "Connect automatically" }; // not-ui: the automation id
    private readonly TextField key = new(password: true, maxLength: 64) { Name = S.DockQuickWifiKey, Placeholder = S.DockQuickWifiKeyPlaceholder };
    private readonly TextField ssid = new(maxLength: 32) { Name = S.DockQuickWifiName, Placeholder = S.DockQuickWifiNamePlaceholder };
    private readonly Label signInTitle = new(S.DockQuickWifiSignIn, LabelStyle.Caption, lines: 2);
    private readonly TextField user = new(maxLength: 256) { Name = S.DockQuickWifiUser, Placeholder = S.DockQuickWifiUser };
    private readonly TextField password = new(password: true, maxLength: 256) { Name = S.DockQuickWifiPassword, Placeholder = S.DockQuickWifiPassword };
    private readonly Button ok = new(S.DockQuickWifiOk, ButtonStyle.Accent) { Name = "OK" };
    private readonly Label attemptText = new("", LabelStyle.Critical, lines: 3);
    private readonly Progress connecting = new(S.DockQuickConnecting);
    private readonly Button connect = new(S.DockQuickConnect, ButtonStyle.Accent) { Name = "Connect" };
    private readonly Button next = new(S.DockQuickWifiNext, ButtonStyle.Accent) { Name = "Next" };
    private readonly Button cancel = new(S.DockQuickCancel) { Name = "Cancel" };
    private readonly Button disconnect = new(S.DockQuickDisconnect) { Name = "Disconnect" };
    private readonly Button properties = new(S.DockQuickWifiProperties, ButtonStyle.Link) { Name = "Properties" };
    private readonly Button networkList = new(S.DockQuickWifiOpenList) { Name = "Windows' list of networks" }; // not-ui: the automation id
    private readonly Button location = new(S.DockQuickWifiLocationSettings, ButtonStyle.Accent) { Name = "Location settings" }; // not-ui: the automation id
    private readonly Button windowsList = new(S.DockQuickWifiWindowsList) { Name = "Windows' list of networks" }; // not-ui: the automation id
    private readonly Button wiredProperties = new(S.DockQuickWifiProperties, ButtonStyle.Link) { Name = "Properties" };

    // An opened wired row's lines: what each says, and its value.
    private readonly Label[] facts = [.. Enumerable.Range(0, 6).Select(_ => new Label("", LabelStyle.Caption))];
    private WifiState state = new(null, false, WifiAccess.Allowed, [], true, null, null);
    private WiredState wired = new([]);

    // The row opened in place: a network by SSID (hex), a wired one by WiredKey and its adapter's id;
    // and what an opened network asks for.
    private string? open;
    private Ask asking;

    // The name typed for the hidden network, and what is wrong with it.
    private string? hiddenName;
    private string? nameProblem;

    /// <summary>What an opened network asks for before it joins, in Windows' order.</summary>
    private enum Ask
    {
        Nothing,

        /// <summary>The hidden network's name.</summary>
        Name,

        /// <summary>The security key.</summary>
        Key,

        /// <summary>An enterprise network's user name and password.</summary>
        SignIn,
    }

    public WifiPage(IQuickHost host, IWifiSource source, IWiredSource wiredSource)
        : base(host)
    {
        this.source = source;
        this.wiredSource = wiredSource;
        // Windows' own page for the connection; the flyout goes, as it does for a network's Properties.
        wiredProperties.Clicked = () => Host.Launch(Wired.PropertiesPage);
        radio.Clicked = () =>
        {
            radio.On = !radio.On;
            source.SetRadio(radio.On);
        };
        refresh.Clicked = () =>
        {
            source.Scan();
            Changes.Add(WifiNews.Networks);
        };
        connect.Clicked = Connect;
        next.Clicked = Next;
        key.Submitted = Next;
        ssid.Submitted = Next;
        key.TextChanged = () => next.Enabled = key.Text.Length > 0;
        ssid.TextChanged = () => next.Enabled = ssid.Text.Length > 0;
        ok.Clicked = SignIn;
        password.Submitted = SignIn;
        user.TextChanged = () => ok.Enabled = CanSignIn;
        password.TextChanged = () => ok.Enabled = CanSignIn;
        cancel.Clicked = Collapse;
        disconnect.Clicked = () =>
        {
            source.Disconnect();
            Collapse();
        };
        // The network's own properties, Windows' page for it; Windows' Wi-Fi settings where it has none.
        // Either way the flyout goes, as Windows' network list does when its Properties is clicked:
        // the dialog is a window of its own, and the list would only stand over it.
        properties.Clicked = () =>
        {
            if (Opened() is not { } network || !source.ShowProperties(network))
            {
                Host.Launch("ms-settings:network-wifi");
                return;
            }
            Host.Close();
        };
        networkList.Clicked = () => Host.Launch("ms-availablenetworks:");
        windowsList.Clicked = () => Host.Launch("ms-availablenetworks:");
        location.Clicked = () => Host.Launch("ms-settings:privacy-location");
    }

    public override QuickKind Kind => QuickKind.Wifi;

    public override void Start()
    {
        source.Start(Changes);
        wiredSource.Start(Changes);
        state = source.Read();
        wired = wiredSource.Read();
        Host.Rebuild();
    }

    protected override void Refresh(int news)
    {
        state = source.Read();
        // Read on every news, not only the cable's: a wake for a connection still being identified
        // carries none of its own.
        wired = wiredSource.Read();
        if (open is { } opened && opened.StartsWith(WiredKey, StringComparison.Ordinal) && OpenedWired() is null)
        {
            // Its cable went: its card goes with its row.
            open = null;
        }
        if (Attempted() is { } attempt)
        {
            switch (attempt.Outcome)
            {
                case WifiOutcome.Connected:
                    // Joined: the card closes, as Windows' does.
                    open = null;
                    asking = Ask.Nothing;
                    hiddenName = null;
                    source.Forget();
                    break;
                case WifiOutcome.NeedsKey:
                case WifiOutcome.WrongKey:
                    if (asking != Ask.Key)
                    {
                        asking = Ask.Key;
                        key.Text = "";
                    }
                    Host.Focus(key);
                    break;
                case WifiOutcome.NeedsSignIn:
                case WifiOutcome.WrongSignIn:
                    if (asking != Ask.SignIn)
                    {
                        asking = Ask.SignIn;
                        password.Text = "";
                    }
                    Host.Focus(user.Text.Length == 0 ? user : password);
                    break;
            }
        }
    }

    public override void Build(PageLayout layout)
    {
        var context = layout.Context;
        var cables = Cables();
        if (Titled(state.Access, cables.Count))
        {
            // The one title row, Windows' Wi-Fi page's: "Wi-Fi", with the radio's switch and the
            // refresh button at its right.
            var trailing = new List<(Widget, float)>();
            if (state.RadioOn is not null && state.CanSwitch)
            {
                radio.On = state.RadioOn == true;
                trailing.Add((radio, context.D(44)));
            }
            if (state.RadioOn == true && state.Access == WifiAccess.Allowed)
            {
                // It stays usable while a scan runs (pressing it looks again); the ring says it is busy.
                refresh.Busy = state.Scanning;
                trailing.Add((refresh, context.D(36)));
            }
            layout.Title(title, [.. trailing]);
        }
        else
        {
            // No title row: the first cable stands as far from the panel's top as the last row
            // does from the footer.
            layout.Gap(6);
        }
        if (state.Scanning || cables.Any(cable => cable.Plugged && cable.Internet == WiredInternet.Identifying))
        {
            // The dock hears when the scan is done, and when a connection's network is known; this
            // is for a scan that never reports back, and for Windows naming a network without a word.
            Host.Wake(1500);
        }

        if (state.Problem is { Length: > 0 } trouble)
        {
            problem.Text = trouble;
            layout.Gap(4);
            Text(layout, problem, layout.TextLeft, layout.TextRight);
            layout.Gap(8);
        }

        // The cables first, under the title and above the networks, as Windows' list of
        // connections had them: one list, the cables at its head.
        if (cables.Count > 0)
        {
            Wires(layout, cables);
        }
        switch (state.Access)
        {
            case WifiAccess.Denied:
                Notice(layout, notice.Say(Lock, S.DockQuickWifiDenied, S.DockQuickWifiDeniedDetail));
                layout.Buttons(layout.TextRight, location, windowsList);
                break;
            case WifiAccess.NoAdapter when cables.Count > 0:
                // The cables are the page here, untitled: the Wi-Fi's absence is a line under them.
                layout.Gap(6);
                Text(layout, noWifi, layout.TextLeft, layout.TextRight);
                layout.Gap(4);
                break;
            case WifiAccess.NoAdapter:
                Notice(layout, notice.Say(FullBars, S.DockQuickWifiNoAdapter, S.DockQuickWifiNoAdapterDetail));
                break;
            default:
                if (state.RadioOn == false)
                {
                    Notice(layout, notice.Say(FullBars, S.DockQuickWifiOff, S.DockQuickWifiOffDetail));
                }
                else
                {
                    // A scan changes nothing on the page: the busy Refresh button is the whole of
                    // it, so the networks never move under the pointer while one runs.
                    if (state.Networks.Count > 0)
                    {
                        Networks(layout, first: cables.Count == 0);
                    }
                    else
                    {
                        // Whether a scan is running or not: an empty list says the same, so the
                        // page doesn't grow or shrink when one starts or ends.
                        Notice(layout, notice.Say(FullBars, S.DockQuickWifiNoNetworks, S.DockQuickWifiNoNetworksDetail));
                    }
                }
                break;
        }
        rows.Done();
    }

    /// <summary>
    /// Whether the page has its title row, "Wi-Fi": always but on a PC without Wi-Fi whose cables
    /// are the page, where a Wi-Fi title would stand over no Wi-Fi and the widget's own "Network"
    /// is not said again. A PC with neither keeps the title over its "No Wi-Fi adapter" notice.
    /// </summary>
    internal static bool Titled(WifiAccess access, int cables) => access != WifiAccess.NoAdapter || cables == 0;

    /// <summary>How far down the list one row takes, a network's or a cable's: the row and the gap under it.</summary>
    internal static float RowPitch(QuickContext context) => context.T(56) + context.D(2);

    /// <summary>The wired rows to show: each cable that is in (Wired.Shown).</summary>
    private IReadOnlyList<WiredAdapter> Cables() => Wired.Shown(wired.Adapters, wifi: state.Access != WifiAccess.NoAdapter);

    /// <summary>The wired rows, each as a network's row is: a glyph, the name and where it stands.</summary>
    private void Wires(PageLayout layout, IReadOnlyList<WiredAdapter> cables)
    {
        var context = layout.Context;
        var rowHeight = context.T(56);
        var gap = context.D(2);
        layout.Gap(2);
        foreach (var cable in cables)
        {
            var id = WiredKey + cable.Id.ToString("N");
            // Kept by its name too: a network Windows names once it has identified it is a row of
            // that name, for Narrator as for the eye.
            var row = rows.Get(id + "/" + cable.Name, () => new ListRow { Name = cable.Name });
            row.Title = cable.Name;
            row.Detail = Wired.StateText(cable);
            row.Strong = cable.Plugged;
            row.Glyph = Wired.GlyphOf(cable);
            row.GlyphBack = null;
            row.Badge = null;
            row.Clicked = () => Toggle(id);
            if (open != id)
            {
                layout.Row(row, rowHeight);
                layout.Y += gap;
                continue;
            }
            layout.Row(row, rowHeight);
            Facts(layout, cable);
            layout.Y += context.D(12);
            layout.Opened(row);
            layout.Y += gap;
        }
    }

    /// <summary>
    /// What an opened wired row says under it, lined up with its name: the link speed, the IPv4
    /// address and the network's profile, each where Windows says it, then Properties.
    /// </summary>
    private void Facts(PageLayout layout, WiredAdapter cable)
    {
        var context = layout.Context;
        var left = layout.ListTextLeft;
        var right = layout.ListTextRight;
        (string Name, string? Value)[] said =
        [
            (S.DockQuickWiredLinkSpeed, cable.Plugged ? Wired.SpeedText(cable.Speed) : null),
            (S.DockQuickWiredAddress, cable.Plugged ? cable.Address : null),
            (S.DockQuickWiredProfile, cable.Plugged ? Wired.CategoryText(cable.Category) : null),
        ];
        var shown = said.Where(fact => fact.Value is not null).ToList();
        if (shown.Count > 0)
        {
            // The names in a column as wide as the widest, the values after it.
            var nameWidth = shown.Max(fact => Label.WidthOf(context, fact.Name, LabelStyle.Caption));
            var valueLeft = MathF.Min(left + MathF.Ceiling(nameWidth) + context.D(16), right - context.D(40));
            var lineHeight = context.T(20);
            layout.Gap(2);
            for (var i = 0; i < shown.Count; i++)
            {
                var (name, value) = shown[i];
                var key = facts[2 * i];
                var text = facts[(2 * i) + 1];
                key.Text = name;
                text.Text = value!;
                layout.Put(key, PopupPaint.Rect(left, layout.Y, valueLeft - left, lineHeight));
                layout.Place(text, valueLeft, right - valueLeft, lineHeight);
            }
        }
        layout.Gap(6);
        var width = MathF.Min(wiredProperties.NaturalWidth(context), right - left);
        // The link's text lines up with the name above it, as a network's Properties does.
        layout.Place(wiredProperties, left - context.D(11), width, context.T(32));
    }

    /// <param name="first">Whether the networks head the list; under the cables, they follow on as one more row would.</param>
    private void Networks(PageLayout layout, bool first)
    {
        var context = layout.Context;
        var rowHeight = context.T(56);
        var gap = context.D(2);
        layout.Gap(first ? 2 : 0);
        foreach (var network in state.Networks)
        {
            var id = Convert.ToHexString(network.SsidBytes);
            var row = rows.Get(id, () => new ListRow { Name = network.Ssid });
            row.Title = network.Ssid;
            row.Detail = WifiProfiles.StateText(network.Connected, network.Secured);
            row.Strong = network.Connected;
            row.Glyph = Bars[Math.Clamp(network.Bars, 1, 4) - 1];
            row.GlyphBack = FullBars;
            row.Badge = network.Secured ? Lock : null;
            row.Clicked = () => Toggle(id);
            if (open != id)
            {
                layout.Row(row, rowHeight);
                layout.Y += gap;
                continue;
            }
            // The opened network: the row and what it offers under it, one item (PageLayout.Opened).
            layout.Row(row, rowHeight);
            Offer(layout, network);
            layout.Y += context.D(12);
            layout.Opened(row);
            layout.Y += gap;
        }
    }

    /// <summary>What an opened network offers, under its row, lined up with its name.</summary>
    private void Offer(PageLayout layout, WifiNetwork network)
    {
        var context = layout.Context;
        var left = layout.ListTextLeft;
        var right = layout.ListTextRight;
        var attempt = Attempted();
        if (network.Connected)
        {
            layout.Gap(4);
            var y = layout.Y;
            layout.Buttons(right, disconnect);
            var width = MathF.Min(properties.NaturalWidth(context), right - left - context.D(104));
            // The link's text lines up with the name above it.
            layout.Put(properties, PopupPaint.Rect(left - context.D(11), y, width, context.T(32)));
            return;
        }
        if (attempt is { Outcome: WifiOutcome.Connecting })
        {
            layout.Gap(4);
            layout.Place(connecting, left, right - left, context.T(32));
            return;
        }
        if (!WifiProfiles.CanJoin(network.Security, network.Auth))
        {
            attemptText.Text = WifiProfiles.CantJoin;
            attemptText.Style = LabelStyle.Caption;
            layout.Gap(4);
            Text(layout, attemptText, left, right);
            layout.Gap(8);
            layout.Buttons(right, networkList);
            return;
        }
        if (asking == Ask.Name)
        {
            // The hidden network: its name first, as Windows asks it.
            layout.Gap(4);
            layout.Place(ssid, left, right - left, context.T(32));
            if (nameProblem is { Length: > 0 } wrongName)
            {
                attemptText.Text = wrongName;
                attemptText.Style = LabelStyle.Critical;
                layout.Gap(6);
                Text(layout, attemptText, left, right);
            }
            layout.Gap(12);
            next.Enabled = ssid.Text.Length > 0;
            layout.Buttons(right, cancel, next);
            return;
        }
        if (asking == Ask.SignIn)
        {
            // An enterprise network: the user name and the password, then OK.
            layout.Gap(4);
            Text(layout, signInTitle, left, right);
            layout.Gap(6);
            layout.Place(user, left, right - left, context.T(32));
            layout.Gap(6);
            layout.Place(password, left, right - left, context.T(32));
            if (attempt?.Problem is { Length: > 0 } refused)
            {
                attemptText.Text = refused;
                attemptText.Style = LabelStyle.Critical;
                layout.Gap(6);
                Text(layout, attemptText, left, right);
            }
            layout.Gap(12);
            ok.Enabled = CanSignIn;
            layout.Buttons(right, cancel, ok);
            return;
        }
        if (asking == Ask.Key)
        {
            layout.Gap(4);
            layout.Place(key, left, right - left, context.T(32));
            if (attempt?.Problem is { Length: > 0 } wrong)
            {
                attemptText.Text = wrong;
                attemptText.Style = LabelStyle.Critical;
                layout.Gap(6);
                Text(layout, attemptText, left, right);
            }
            layout.Gap(12);
            next.Enabled = key.Text.Length > 0;
            layout.Buttons(right, cancel, next);
            return;
        }
        layout.Gap(4);
        layout.Place(automatic, left, right - left, context.T(32));
        if (attempt?.Problem is { Length: > 0 } failed)
        {
            attemptText.Text = failed;
            attemptText.Style = LabelStyle.Critical;
            layout.Gap(6);
            Text(layout, attemptText, left, right);
        }
        layout.Gap(8);
        layout.Buttons(right, connect);
    }

    private void Toggle(string id)
    {
        open = open == id ? null : id;
        Forget();
        automatic.Checked = true;
        Host.Rebuild();
    }

    private void Collapse()
    {
        open = null;
        Forget();
        Host.Rebuild();
    }

    /// <summary>Nothing asked and nothing typed: the opened network starts again.</summary>
    private void Forget()
    {
        asking = Ask.Nothing;
        hiddenName = null;
        nameProblem = null;
        key.Text = "";
        ssid.Text = "";
        user.Text = "";
        password.Text = "";
        source.Forget();
    }

    private WifiNetwork? Opened() => open is null ? null : state.Networks.FirstOrDefault(network => Convert.ToHexString(network.SsidBytes) == open);

    /// <summary>The wired row opened, where it is still shown.</summary>
    private WiredAdapter? OpenedWired() => open is null ? null : Cables().FirstOrDefault(cable => WiredKey + cable.Id.ToString("N") == open);

    /// <summary>The opened network as it joins: the hidden network under the name typed for it.</summary>
    private WifiNetwork? Joining() => Opened() is { } network
        ? network.Hidden ? hiddenName is { } name ? WifiProfiles.Named(network, name) : null : network
        : null;

    /// <summary>The last attempt, where it is the opened network's.</summary>
    private WifiAttempt? Attempted() =>
        state.Attempt is { } attempt && Joining() is { } network && attempt.Ssid == network.Ssid ? attempt : null;

    private bool CanSignIn => user.Text.Trim().Length > 0 && password.Text.Length > 0;

    /// <summary>Connect: the hidden network asks for its name first; every other joins.</summary>
    private void Connect()
    {
        if (Opened() is { Hidden: true } && hiddenName is null)
        {
            asking = Ask.Name;
            ssid.Text = "";
            nameProblem = null;
            Host.Focus(ssid);
            Host.Rebuild();
            return;
        }
        Join(withKey: false);
    }

    /// <summary>Next: after the hidden network's name, joins by it; after the key, joins with it.</summary>
    private void Next()
    {
        if (asking != Ask.Name)
        {
            Join(withKey: true);
            return;
        }
        var name = ssid.Text;
        if (WifiProfiles.NameProblem(name) is { } wrong)
        {
            nameProblem = wrong;
            Host.Rebuild();
            return;
        }
        hiddenName = name;
        nameProblem = null;
        asking = Ask.Nothing;
        Join(withKey: false);
    }

    private void Join(bool withKey)
    {
        if (Joining() is not { } network)
        {
            return;
        }
        if (withKey && key.Text.Length == 0)
        {
            return;
        }
        source.Connect(network, withKey ? key.Text : null, automatic.Checked);
        if (withKey)
        {
            key.Text = "";
        }
        state = source.Read();
        Host.Rebuild();
    }

    /// <summary>OK: signs in to the enterprise network with the user name and the password typed; the password is not kept.</summary>
    private void SignIn()
    {
        if (Joining() is not { } network || !CanSignIn)
        {
            return;
        }
        source.Connect(network, null, automatic.Checked, new WifiSignIn(user.Text, password.Text));
        password.Text = "";
        state = source.Read();
        Host.Rebuild();
    }

    public override void Dispose()
    {
        source.Dispose();
        wiredSource.Dispose();
    }

    private static string Glyph(int code) => char.ConvertFromUtf32(code);
}
