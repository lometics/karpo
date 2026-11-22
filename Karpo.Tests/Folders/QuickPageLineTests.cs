// The network's and Bluetooth's quick-settings pages as lines of their presets' folders
// (docs\widgets.md §88; Karpo.Dock\Islands\QuickPageTile.cs): what the network and Bluetooth flyouts
// did, done by the same pages in a folder — a network opened onto its key and joined, the hidden
// network by its name, an enterprise network signing in, Refresh's ring and the wake that ends a scan,
// the cables over the networks and what an opened one says, a cable pulled out, a device's battery,
// an audio device connected and disconnected, the three empty states of Bluetooth — driven the way
// the folder drives a card: CardPress and CardRelease, CardKey and CardChar. Made-up networks and
// devices throughout (SampleWifi, SampleWired, SampleBluetooth): nothing reaches this PC's.
//
// Pictured beside the flyouts they replace (rendered before they went: quick-wifi-*.png,
// quick-bluetooth-*.png): wifi-preset-<dark|light>.png, bluetooth-preset-<dark|light>.png, at 150 %
// and at 225 % text.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.ListFormPictures;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

public sealed unsafe class QuickPageLineTests
{
    /// <summary>The samples of one test, kept so it can see what they were asked to do.</summary>
    private sealed class Samples
    {
        public SampleWifi Wifi { get; set; } = new();

        /// <summary>The cables: none unless a test hands some, so the Wi-Fi tests see Wi-Fi alone.</summary>
        public SampleWired Wired { get; set; } = new();

        public SampleBluetooth Bluetooth { get; set; } = new();

        public QuickSources Sources => new()
        {
            Wifi = () => Wifi,
            Wired = () => Wired,
            Bluetooth = () => Bluetooth,
            Battery = () => new SampleBattery(),
            Desktops = () => new SampleDesktopNames(),
        };
    }

    /// <summary>
    /// A preset's folder opened, its page's line found, and the folder's way of driving a card: what
    /// the open folder hands it, in the card's own px.
    /// </summary>
    private sealed class Line : IDisposable
    {
        public Line(Gfx gfx, string preset, Samples samples, bool dark = true, float scale = 1.5f, float text = 1f)
        {
            Gfx = gfx;
            Look = LookFor(dark, text);
            Scale = scale;
            QuickPageTile.Sources = samples.Sources;
            QuickPageTile.Opener = Opened.Add;
            var holder = Presets.Folder(Presets.Find(preset)!, new PresetMachine(HasBattery: false, FixedDrives: []));
            Folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
            Folder.Opened(gfx);
            Tile = Folder.Tiles.Select(tile => tile.Island).OfType<QuickPageTile>().Single();
        }

        public Gfx Gfx { get; }

        public Look Look { get; }

        public float Scale { get; }

        public FolderIsland Folder { get; }

        public QuickPageTile Tile { get; }

        /// <summary>What the line asked Windows to open, in order.</summary>
        public List<string> Opened { get; } = [];

        public TileStyle Style => FolderFlyout.StyleFor(Scale, Look, FlyoutTheme.For(Look));

        /// <summary>The line's card as the folder gives it: the list's width, as tall as it measures.</summary>
        public D2D_RECT_F Box
        {
            get
            {
                var width = FolderFlyout.ListWidth(Scale);
                return PopupPaint.Rect(0, 0, width, Tile.ListHeight(width, Style));
            }
        }

        public IReadOnlyList<Widget> Placed
        {
            get
            {
                Tile.LayOut(Box, Style);
                return Tile.Placed;
            }
        }

        /// <summary>The control named <paramref name="name"/>, or null.</summary>
        public Widget? Find(string name) => Placed.FirstOrDefault(widget => widget.Name == name);

        /// <summary>Whether a label on the line says exactly <paramref name="words"/>.</summary>
        public bool Says(string words) => Placed.OfType<Label>().Any(label => label.Text == words);

        /// <summary>A press and a release in the middle of the control named <paramref name="name"/>.</summary>
        public void Click(string name)
        {
            var widget = Find(name) ?? throw new Xunit.Sdk.XunitException($"no {name} on the line");
            var at = widget.Bounds;
            // A row's own: what it shows, not what an opened row offers under it (ListRow.Below).
            var bottom = widget is ListRow row ? at.bottom - row.Under - row.Below : at.bottom;
            var top = widget is ListRow shown ? at.top + shown.Over : at.top;
            ClickAt(new Vector2((at.left + at.right) / 2, (top + bottom) / 2));
        }

        public void ClickAt(Vector2 point)
        {
            var box = Box;
            if (Tile.CardPress(point, box, Style, WidgetSurface.List))
            {
                Tile.CardRelease();
            }
            Pump();
        }

        public void Type(string text)
        {
            foreach (var typed in text)
            {
                _ = Tile.CardChar(typed, WidgetSurface.List);
            }
        }

        public bool Key(int key)
        {
            var used = Tile.CardKey(key, WidgetSurface.List);
            Pump();
            return used;
        }

        /// <summary>What the sources posted, taken as the dock's thread would.</summary>
        public void Pump() => Tile.Pump();

        public void Dispose()
        {
            Folder.Closed();
            Folder.Dispose();
            QuickPageTile.Sources = null;
            QuickPageTile.Opener = null;
        }
    }

    // --- Wi-Fi ---------------------------------------------------------------------------------------

    /// <summary>
    /// A network opened onto its key and joined, as the flyout did it: "Connect automatically" ticked,
    /// Connect, the key's field taking the keys (Next off until something is typed), a wrong key said
    /// and asked again, the right one joining; the connected network's Disconnect; the radio's switch.
    /// </summary>
    [UiFact]
    public void ANetworkJoinsWithAKey() => OnStaThread(gfx =>
    {
        var samples = new Samples();
        using var line = new Line(gfx, Presets.WifiId, samples, dark: false, scale: 1.25f);
        line.Click("FRITZ!Box 7590 KL");
        Assert.True(line.Find("Connect automatically") is CheckBox { Checked: true });
        line.Click("Connect");
        var key = Assert.IsType<TextField>(line.Find("Network security key"));
        Assert.True(key.Focused);
        Assert.True(key.Password);
        Assert.Same(key, line.Tile.Focused);
        Assert.False(line.Find("Next")!.Enabled);
        line.Type("wrong");
        Assert.Equal("wrong", key.Text);
        Assert.True(line.Find("Next")!.Enabled);
        // Every key is the field's while it is typed in: a space, a letter, the folder sees none.
        Assert.True(line.Key(VK.VK_SPACE));
        Assert.True(line.Key('A'));
        Assert.True(line.Key(VK.VK_RETURN));
        Assert.Equal(WifiOutcome.WrongKey, samples.Wifi.Read().Attempt?.Outcome);
        Assert.True(line.Find("Network security key") is TextField { Text.Length: 0, Focused: true });
        line.Type("carrots!");
        line.Click("Next");
        Assert.Contains(samples.Wifi.Read().Networks, network => network.Ssid == "FRITZ!Box 7590 KL" && network.Connected);
        // Joined: the card closed.
        Assert.Null(line.Find("Network security key"));

        line.Click("FRITZ!Box 7590 KL");
        Assert.NotNull(line.Find("Properties"));
        line.Click("Disconnect");
        Assert.DoesNotContain(samples.Wifi.Read().Networks, network => network.Connected);

        line.Click("Wi-Fi");
        Assert.False(samples.Wifi.Read().RadioOn);
        Assert.True(line.Says("Wi-Fi is off"));
        line.Click("Wi-Fi");
        Assert.True(samples.Wifi.Read().RadioOn);
    });

    /// <summary>
    /// Keys typed straight after Connect, before the folder has painted, go to the key's field the
    /// page gave the keyboard (regression: they went nowhere until the next paint).
    /// </summary>
    [UiFact]
    public void KeysTypedStraightAfterConnectGoToTheField() => OnStaThread(gfx =>
    {
        using var line = new Line(gfx, Presets.WifiId, new Samples());
        line.Click("FRITZ!Box 7590 KL");
        line.Click("Connect");
        line.Type("carrots");
        Assert.Equal("carrots", Assert.IsType<TextField>(line.Find("Network security key")).Text);
    });

    /// <summary>
    /// The line grows by a network's card and shrinks again: it is as tall as what the page shows,
    /// and the rows above the opened one stay where they were.
    /// </summary>
    [UiFact]
    public void AnOpenedNetworkGrowsTheLineAndMovesNothingAboveIt() => OnStaThread(gfx =>
    {
        using var line = new Line(gfx, Presets.WifiId, new Samples());
        var shut = line.Box.bottom;
        var above = line.Find("Café Central Guest")!.Bounds;
        line.Click("FRITZ!Box 7590 KL");
        Assert.True(line.Box.bottom > shut, "the line kept its height with a network opened");
        Assert.Equal(above, line.Find("Café Central Guest")!.Bounds);
        line.Click("FRITZ!Box 7590 KL");
        Assert.Equal(shut, line.Box.bottom);
    });

    /// <summary>
    /// A press beside the controls ends the editing, as one on nothing in particular does in Windows
    /// 11: the field shows no caret and takes no keys, and the keys are the folder's again; a press on
    /// the field gives it back.
    /// </summary>
    [UiFact]
    public void APressBesideTheControlsEndsTheEditing() => OnStaThread(gfx =>
    {
        using var line = new Line(gfx, Presets.WifiId, new Samples());
        line.Click("FRITZ!Box 7590 KL");
        line.Click("Connect");
        var key = Assert.IsType<TextField>(line.Find("Network security key"));
        line.Type("carrots");
        Assert.True(key.Focused);

        // The title row's empty middle: no control answers there.
        line.ClickAt(new Vector2(line.Box.right / 2, 4));
        Assert.False(key.Focused);
        Assert.Null(line.Tile.Focused);
        line.Type("x");
        Assert.Equal("carrots", key.Text);
        Assert.False(line.Key(VK.VK_SPACE), "a space with nothing being typed in is the folder's");

        line.Click("Network security key");
        Assert.True(key.Focused);
    });

    /// <summary>The hidden network joins as Windows' own list joins it: Connect, its name, then its key.</summary>
    [UiFact]
    public void TheHiddenNetworkJoinsByItsName() => OnStaThread(gfx =>
    {
        var samples = new Samples { Wifi = new SampleWifi { Hidden = true } };
        using var line = new Line(gfx, Presets.WifiId, samples, dark: false, scale: 1.25f);
        line.Click(WifiProfiles.HiddenName);
        line.Click("Connect");
        var name = Assert.IsType<TextField>(line.Find("Network name"));
        Assert.True(name.Focused);
        Assert.False(name.Password);
        Assert.False(line.Find("Next")!.Enabled);
        line.Type(SampleWifi.HiddenSsid);
        Assert.True(line.Find("Next")!.Enabled);
        line.Key(VK.VK_RETURN);
        Assert.Null(line.Find("Network name"));
        Assert.True(Assert.IsType<TextField>(line.Find("Network security key")).Focused);
        line.Type("carrots!");
        line.Click("Next");
        var state = samples.Wifi.Read();
        Assert.Contains(state.Networks, network => network.Ssid == SampleWifi.HiddenSsid && network.Connected);
        Assert.DoesNotContain(state.Networks, network => network.Hidden);
    });

    /// <summary>
    /// An enterprise network asks for a user name and a password, wrong ones are said and asked
    /// again keeping the user name, right ones join; the connected network's Properties are its own.
    /// </summary>
    [UiFact]
    public void AnEnterpriseNetworkSignsInAndANetworkOpensItsOwnProperties() => OnStaThread(gfx =>
    {
        var samples = new Samples();
        using var line = new Line(gfx, Presets.WifiId, samples, dark: false, scale: 1.25f);
        line.Click("Rabbit Hole");
        line.Click("Properties");
        Assert.Equal(["Rabbit Hole"], samples.Wifi.PropertiesShown);
        Assert.Empty(line.Opened);

        line.Click("eduroam");
        line.Click("Connect");
        Assert.Equal(WifiOutcome.NeedsSignIn, samples.Wifi.Read().Attempt?.Outcome);
        Assert.True(Assert.IsType<TextField>(line.Find("User name")).Focused);
        Assert.True(Assert.IsType<TextField>(line.Find("Password")).Password);
        Assert.False(line.Find("OK")!.Enabled);
        line.Type("alice");
        line.Click("Password");
        line.Type("rabbit");
        Assert.True(line.Find("OK")!.Enabled);
        line.Key(VK.VK_RETURN);
        Assert.Equal(WifiOutcome.WrongSignIn, samples.Wifi.Read().Attempt?.Outcome);
        Assert.True(line.Find("User name") is TextField { Text: "alice" });
        Assert.True(line.Find("Password") is TextField { Text.Length: 0 });
        line.Click("Password");
        line.Type("wonderland");
        line.Click("OK");
        Assert.Contains(samples.Wifi.Read().Networks, network => network.Ssid == "eduroam" && network.Connected);
        Assert.Null(line.Find("User name"));
    });

    /// <summary>
    /// Refresh looks again and its ring turns while the scan runs, the networks staying where they
    /// were; the page's own wake ends a scan Windows never reports on, and the ring stops.
    /// </summary>
    [UiFact]
    public void RefreshLooksAgainAndItsRingTurns() => OnStaThread(gfx =>
    {
        var samples = new Samples();
        using var line = new Line(gfx, Presets.WifiId, samples);
        var refresh = Assert.IsType<Button>(line.Find("Refresh"));
        Assert.False(refresh.Busy);
        var before = line.Find("Rabbit Hole")!.Bounds;
        line.Click("Refresh");
        Assert.True(samples.Wifi.IsScanning);
        Assert.True(Assert.IsType<Button>(line.Find("Refresh")).Busy);
        Assert.Equal(before, line.Find("Rabbit Hole")!.Bounds);

        // The page asked to be woken in 1.5 s; the timer's post, taken.
        _ = Patience.Until(() =>
        {
            line.Pump();
            return !samples.Wifi.IsScanning;
        });
        Assert.False(Assert.IsType<Button>(line.Find("Refresh")).Busy);
    });

    /// <summary>
    /// The keys reach every control: Down walks them from the title's switch, Enter clicks the one
    /// ringed, and past the last the keys are the folder's again.
    /// </summary>
    [UiFact]
    public void TheKeysWalkTheControlsAndLeaveAtTheEnd() => OnStaThread(gfx =>
    {
        var samples = new Samples();
        using var line = new Line(gfx, Presets.WifiId, samples);
        _ = line.Placed;
        Assert.True(line.Key(VK.VK_DOWN));
        Assert.NotNull(line.Tile.Focused);
        Assert.True(line.Tile.Focused!.FocusVisible);
        while (line.Tile.Focused is not Button { Name: "Refresh" })
        {
            Assert.True(line.Key(VK.VK_DOWN), "Refresh was never reached");
        }
        Assert.True(line.Key(VK.VK_RETURN));
        Assert.True(samples.Wifi.IsScanning);
        var steps = 0;
        while (line.Key(VK.VK_DOWN))
        {
            Assert.True(++steps < 40, "the keys never left the line");
        }
        Assert.Null(line.Tile.Focused);
    });

    // --- The cables -----------------------------------------------------------------------------------

    /// <summary>
    /// A cable in has its row under the one title and above the networks; opened, it says what Windows
    /// says of it and has Properties, Windows' own page; pulled out, its row goes, and an opened one's
    /// card with it.
    /// </summary>
    [UiFact]
    public void TheCablesStandOverTheNetworks() => OnStaThread(gfx =>
    {
        var samples = new Samples { Wired = new SampleWired(SampleWired.Office) };
        using var line = new Line(gfx, Presets.WifiId, samples);
        var cable = Assert.IsType<ListRow>(line.Find("Network 2"));
        Assert.Equal("Connected", cable.Detail);
        Assert.True(cable.Bounds.bottom <= line.Find("Rabbit Hole")!.Bounds.top);
        Assert.Single(line.Placed.OfType<Label>(), label => label.Text == "Wi-Fi");
        Assert.False(line.Says("Network"));

        line.Click("Network 2");
        Assert.True(line.Says("Link speed") && line.Says("1 Gbps"));
        Assert.True(line.Says("IPv4 address") && line.Says("192.168.1.24"));
        Assert.Null(line.Find("Disconnect"));
        line.Click("Properties");
        Assert.Equal([Wired.PropertiesPage], line.Opened);
        Assert.Empty(samples.Wifi.PropertiesShown);

        samples.Wired.Plug(SampleWired.Office.Id, false);
        line.Pump();
        Assert.Null(line.Find("Network 2"));
        Assert.Null(line.Find("Properties"));
        samples.Wired.Plug(SampleWired.Office.Id, true);
        line.Pump();
        Assert.NotNull(line.Find("Network 2"));
        Assert.Null(line.Find("Properties"));
    });

    /// <summary>A PC without Wi-Fi: its cable is the page, untitled, and a line under it says there is no Wi-Fi.</summary>
    [UiFact]
    public void WithoutWifiTheCablesAreThePage() => OnStaThread(gfx =>
    {
        var samples = new Samples { Wifi = new SampleWifi { Access = WifiAccess.NoAdapter }, Wired = new SampleWired(SampleWired.Office) };
        using var line = new Line(gfx, Presets.WifiId, samples);
        Assert.NotNull(line.Find("Network 2"));
        Assert.False(line.Says("Wi-Fi"));
        Assert.True(line.Says(WifiPage.NoWifiLine));
        Assert.Null(line.Find("Wi-Fi"));
    });

    // --- Bluetooth ------------------------------------------------------------------------------------

    /// <summary>
    /// A device that reports its battery shows it, filled to its level; one that does not shows
    /// nothing, and the room is kept on every row so the names line up.
    /// </summary>
    [UiFact]
    public void ADevicesBatteryShowsWhereThereIsOne() => OnStaThread(gfx =>
    {
        using var line = new Line(gfx, Presets.BluetoothId, new Samples());
        var withLevel = Assert.IsType<ListRow>(line.Find("WH-1000XM5"));
        var without = Assert.IsType<ListRow>(line.Find("MX Master 3S"));
        Assert.Equal("72%", withLevel.Tail);
        Assert.True(string.IsNullOrEmpty(without.Tail));
        Assert.Equal(withLevel.TailRoom, without.TailRoom);
        Assert.Equal(withLevel.Width, without.Width);
    });

    /// <summary>
    /// A paired audio device's card offers Disconnect while it is connected and Connect while it is
    /// not, beside Remove device; a mouse's offers only its removal. The switch turns the radio off,
    /// and the line says so.
    /// </summary>
    [UiFact]
    public void AnAudioDeviceIsConnectedAndDisconnectedFromItsCard() => OnStaThread(gfx =>
    {
        var samples = new Samples();
        using var line = new Line(gfx, Presets.BluetoothId, samples);
        line.Click("MX Master 3S");
        Assert.NotNull(line.Find("Remove device"));
        Assert.Null(line.Find("Disconnect"));
        Assert.Null(line.Find("Connect"));

        line.Click("WH-1000XM5");
        Assert.NotNull(line.Find("Disconnect"));
        line.Click("Disconnect");
        Assert.Equal(["disconnect 1"], samples.Bluetooth.Asked);
        if (line.Find("Connect") is null)
        {
            line.Click("WH-1000XM5");
        }
        line.Click("Connect");
        Assert.Equal(["disconnect 1", "connect 1"], samples.Bluetooth.Asked);
        Assert.Contains(samples.Bluetooth.Read().Devices, device => device.Id == "1" && device.Connected);

        line.Click("Bluetooth");
        Assert.True(line.Says("Bluetooth is off"));
    });

    /// <summary>
    /// §605 (the person: "in the bluetooth submenu when i open a device it should show a line on the left as a selection
    /// indicator and when hovering it should light up the whole background and not leave out the area where the connect
    /// button is. the same should be done for the wifi"): an opened row is one item with what it offers — its band runs
    /// from its own top past its buttons, the whole list's width; the pointer anywhere on it but on a button lights the
    /// row, on a button the button alone; it is drawn chosen, its accent pill on its leading edge in the middle of the
    /// whole item. Closed again, it is a row as the others. The Bluetooth headset, a Wi-Fi network, a cable.
    /// </summary>
    [UiFact]
    public void AnOpenedRowIsOneItemWithWhatItOffers() => OnStaThread(gfx =>
    {
        foreach (var (preset, samples, name, offers) in new (string, Samples, string, string[])[]
        {
            (Presets.BluetoothId, new Samples(), "WH-1000XM5", ["Disconnect", "Remove device"]),
            (Presets.WifiId, new Samples(), "FRITZ!Box 7590 KL", ["Connect"]),
            (Presets.WifiId, new Samples { Wired = new SampleWired(SampleWired.Office) }, SampleWired.Office.Name, []),
        })
        {
            using var line = new Line(gfx, preset, samples);
            var box = line.Box;
            var row = Assert.IsType<ListRow>(line.Find(name));
            var shut = row.Bounds;
            Assert.False(row.Open);
            line.Click(name);
            Assert.Same(row, line.Find(name));
            Assert.True(row.Open, $"{name}: not open");
            Assert.True(row.Below > 0, $"{name}: nothing under it is its own");
            Assert.True(row.Bounds.bottom > shut.bottom, $"{name}: its band stops at its own row");
            Assert.Equal(0, row.Bounds.left);
            Assert.Equal(box.right - box.left, row.Bounds.right, 0.5f);
            Assert.All(line.Placed.OfType<ListRow>().Where(other => other != row), other => Assert.False(other.Open));
            foreach (var offer in offers)
            {
                var button = line.Find(offer) ?? throw new Xunit.Sdk.XunitException($"{name}: no {offer}");
                Assert.True(button.Bounds.top >= row.Bounds.top && button.Bounds.bottom <= row.Bounds.bottom + 0.5f, $"{name}: {offer} stands outside its band");

                // Beside the button, at its middle: the row's; on it, the button's alone.
                var middle = (button.Bounds.top + button.Bounds.bottom) / 2;
                line.Tile.CardHover(new Vector2(button.Bounds.left - 3, middle), box, line.Style, WidgetSurface.List);
                Assert.True(row.Hot, $"{name}: the room beside {offer} does not light the row");
                line.Tile.CardHover(new Vector2((button.Bounds.left + button.Bounds.right) / 2, middle), box, line.Style, WidgetSurface.List);
                Assert.True(button.Hot, $"{name}: {offer} does not light");
                Assert.False(row.Hot, $"{name}: the row lights under {offer}");
            }
            // Under all it offers, at its band's foot: still the row's.
            line.Tile.CardHover(new Vector2(row.Bounds.right - 4, row.Bounds.bottom - row.Under - 2), box, line.Style, WidgetSurface.List);
            Assert.True(row.Hot, $"{name}: the foot of what it offers does not light the row");
            line.Tile.CardHover(null, box, line.Style, WidgetSurface.List);

            // The pill: on its leading edge, in the middle of the whole item, not of its own row.
            var (top, bottom) = PillOf(line, row);
            var middleOfItem = (row.Over + (row.Height - row.Under)) / 2;
            Assert.True(bottom > top, $"{name}: no pill");
            Assert.True(MathF.Abs(((top + bottom) / 2) - middleOfItem) <= 1.5f, $"{name}: the pill stands at {(top + bottom) / 2:0.#}, the item's middle at {middleOfItem:0.#}");

            // Closed again: a row as the others.
            line.ClickAt(new Vector2(row.Bounds.left + 40, row.Bounds.top + row.Over + 4));
            _ = line.Placed;
            Assert.False(row.Open, $"{name}: still open");
            Assert.Equal(0, row.Below);
            Assert.Equal(shut.bottom - shut.top, row.Bounds.bottom - row.Bounds.top, 0.5f);
        }
    });

    /// <summary>Where <paramref name="row"/>'s pill is drawn, its own px: the opaque run before its glyph's column.</summary>
    private static (float Top, float Bottom) PillOf(Line line, ListRow row)
    {
        var look = line.Look;
        var context = new QuickContext { Gfx = line.Gfx, Theme = QuickTheme.For(FlyoutTheme.For(look)), Scale = line.Scale, TextScale = look.TextScale };
        var lead = (int)MathF.Floor(row.Lead ?? 0);
        var width = (uint)MathF.Ceiling(row.Width);
        var height = (uint)MathF.Ceiling(row.Height);
        float top = -1, bottom = -1;
        Bitmap(row.Width, row.Height, dc => row.Paint(dc, context), bitmap =>
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < lead; x++)
                {
                    if (AlphaAt(bitmap, width, height, x, y) > 200)
                    {
                        top = top < 0 ? y : top;
                        bottom = y + 1;
                        break;
                    }
                }
            }
        });
        return (top, bottom);
    }

    /// <summary>No radio, a radio that is off, and nothing paired: three states, three answers.</summary>
    [UiFact]
    public void TheThreeEmptyStatesReadDifferently() => OnStaThread(gfx =>
    {
        using (var none = new Line(gfx, Presets.BluetoothId, new Samples { Bluetooth = new SampleBluetooth(radio: null) }))
        {
            Assert.True(none.Says("No Bluetooth"));
            Assert.False(none.Says("No paired devices"));
            Assert.Null(none.Find("Bluetooth"));
        }
        using (var off = new Line(gfx, Presets.BluetoothId, new Samples { Bluetooth = new SampleBluetooth(radio: false) }))
        {
            Assert.True(off.Says("Bluetooth is off"));
            Assert.False(off.Says("No Bluetooth"));
        }
        using var nothing = new Line(gfx, Presets.BluetoothId, new Samples { Bluetooth = new SampleBluetooth(paired: false) });
        Assert.True(nothing.Says("No paired devices"));
        Assert.False(nothing.Says("Bluetooth is off"));
    });

    // --- The folder ------------------------------------------------------------------------------------

    /// <summary>
    /// The page lives while the folder is open: closing it ends its sources, and opening it again
    /// starts afresh — nothing opened, nothing typed.
    /// </summary>
    [UiFact]
    public void ThePageLivesWhileTheFolderIsOpen() => OnStaThread(gfx =>
    {
        var samples = new Samples();
        using var line = new Line(gfx, Presets.WifiId, samples);
        line.Click("FRITZ!Box 7590 KL");
        Assert.NotNull(line.Find("Connect"));
        line.Folder.Closed();
        line.Folder.Opened(gfx);
        Assert.Null(line.Find("Connect"));
        Assert.NotNull(line.Find("FRITZ!Box 7590 KL"));
    });

    /// <summary>The footer's Add device opens Windows' Bluetooth settings, not the Cast panel (the person's bug).</summary>
    [UiFact]
    public void TheFootersAddDeviceOpensBluetoothSettings() => OnStaThread(gfx =>
    {
        using var line = new Line(gfx, Presets.BluetoothId, new Samples());
        var actions = line.Folder.Tiles.Select(tile => tile.Island).OfType<ActionsTile>().Single();
        var opened = new List<string>();
        ActionsTile.Opener = opened.Add;
        try
        {
            actions.Open(ActionsTile.RightTarget);
            actions.Open(ActionsTile.LeftTarget);
        }
        finally
        {
            ActionsTile.Opener = null;
        }
        Assert.Equal(["ms-settings:bluetooth", "ms-settings:bluetooth"], opened);
        Assert.Equal(SettingsActions.AddDeviceId, actions.Chosen.Right!.Id);
    });

    // --- Pictures --------------------------------------------------------------------------------------

    /// <summary>
    /// §605: qs-&lt;bluetooth|wifi&gt;-&lt;dark|light&gt;.png (KARPO_PNG_DIR), at 150 %: a row opened at rest — its pill, the chosen
    /// fill over all it offers —, the same under the pointer beside its button (lit whole, behind the button too), on its
    /// button (the button lit), and a closed row under the pointer.
    /// </summary>
    [UiFact]
    public void OpenedRowSheets()
    {
        foreach (var dark in new[] { true, false })
        {
            foreach (var (preset, file, name, offer, other) in new[]
            {
                (Presets.BluetoothId, "bluetooth", "WH-1000XM5", "Disconnect", "Keychron K2"),
                (Presets.WifiId, "wifi", "FRITZ!Box 7590 KL", "Connect", "Café Central Guest"),
            })
            {
                OnStaThread(gfx =>
                {
                    var lines = new List<Line>();
                    try
                    {
                        var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                        void Add(string caption, Line line) => panels.Add((caption, line.Look, line.Folder, Plan(line.Folder, line.Look, 1.5f).Plan));
                        Line Opened(string? hover)
                        {
                            var line = new Line(gfx, preset, new Samples(), dark, 1.5f);
                            lines.Add(line);
                            line.Click(name);
                            if (hover is not null)
                            {
                                var button = line.Find(hover) ?? throw new Xunit.Sdk.XunitException($"no {hover}");
                                var middle = (button.Bounds.top + button.Bounds.bottom) / 2;
                                var x = hover == offer ? button.Bounds.left - 12 : (button.Bounds.left + button.Bounds.right) / 2;
                                line.Tile.CardHover(new Vector2(x, middle), line.Box, line.Style, WidgetSurface.List);
                            }
                            return line;
                        }
                        Add($"{name} opened: at rest", Opened(null));
                        Add($"Beside its {offer}: lit whole", Opened(offer));
                        var on = Opened(null);
                        var onButton = on.Find(offer)!;
                        on.Tile.CardHover(new Vector2((onButton.Bounds.left + onButton.Bounds.right) / 2, (onButton.Bounds.top + onButton.Bounds.bottom) / 2), on.Box, on.Style, WidgetSurface.List);
                        Add($"On its {offer}: the button lit", on);
                        var closed = new Line(gfx, preset, new Samples(), dark, 1.5f);
                        lines.Add(closed);
                        var row = closed.Find(other) ?? throw new Xunit.Sdk.XunitException($"no {other}");
                        closed.Tile.CardHover(new Vector2(row.Bounds.right - 20, (row.Bounds.top + row.Bounds.bottom) / 2), closed.Box, closed.Style, WidgetSurface.List);
                        Add($"{other}: a closed row under the pointer", closed);
                        Sheet(gfx, $"qs-{file}-{(dark ? "dark" : "light")}.png", 1.5f, panels);
                    }
                    finally
                    {
                        foreach (var line in lines)
                        {
                            line.Dispose();
                        }
                    }
                });
            }
        }
    }

    /// <summary>
    /// The Wi-Fi preset as it opens, a network opened onto its key with a wrong one said, a cable over
    /// the networks, the radio off, and at 225 % text — at 150 %, to stand beside quick-wifi-*.png.
    /// </summary>
    [UiFact]
    public void WifiPresetPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            OnStaThread(gfx =>
            {
                var lines = new List<Line>();
                try
                {
                    Line Open(Samples samples, float text = 1f)
                    {
                        var line = new Line(gfx, Presets.WifiId, samples, dark, 1.5f, text);
                        lines.Add(line);
                        return line;
                    }
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    void Add(string caption, Line line) => panels.Add((caption, line.Look, line.Folder, Plan(line.Folder, line.Look, 1.5f).Plan));

                    Add("As it opens: 100 % text", Open(new Samples()));
                    var key = Open(new Samples());
                    key.Click("FRITZ!Box 7590 KL");
                    key.Click("Connect");
                    key.Type("wrong");
                    key.Key(VK.VK_RETURN);
                    key.Type("car");
                    Add("A key asked for: a wrong one said", key);
                    Add("A cable: over the networks", Open(new Samples { Wired = new SampleWired(SampleWired.Office) }));
                    var off = new Samples();
                    off.Wifi.SetRadio(false);
                    Add("Wi-Fi off: the notice", Open(off));
                    Add("As it opens: 225 % text", Open(new Samples(), 2.25f));
                    Sheet(gfx, $"wifi-preset-{(dark ? "dark" : "light")}.png", 1.5f, panels);
                }
                finally
                {
                    foreach (var line in lines)
                    {
                        line.Dispose();
                    }
                }
            });
        }
    }

    /// <summary>
    /// The Bluetooth preset as it opens, the headset opened on its Disconnect and Remove device, the
    /// radio off, nothing paired, and at 225 % text — at 150 %, beside quick-bluetooth-*.png.
    /// </summary>
    [UiFact]
    public void BluetoothPresetPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            OnStaThread(gfx =>
            {
                var lines = new List<Line>();
                try
                {
                    Line Open(Samples samples, float text = 1f)
                    {
                        var line = new Line(gfx, Presets.BluetoothId, samples, dark, 1.5f, text);
                        lines.Add(line);
                        return line;
                    }
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    void Add(string caption, Line line) => panels.Add((caption, line.Look, line.Folder, Plan(line.Folder, line.Look, 1.5f).Plan));

                    Add("As it opens: 100 % text", Open(new Samples()));
                    var headset = Open(new Samples());
                    headset.Click("WH-1000XM5");
                    Add("A headset opened: its buttons", headset);
                    Add("Bluetooth off: the notice", Open(new Samples { Bluetooth = new SampleBluetooth(radio: false) }));
                    Add("Nothing paired", Open(new Samples { Bluetooth = new SampleBluetooth(paired: false) }));
                    Add("As it opens: 225 % text", Open(new Samples(), 2.25f));
                    Sheet(gfx, $"bluetooth-preset-{(dark ? "dark" : "light")}.png", 1.5f, panels);
                }
                finally
                {
                    foreach (var line in lines)
                    {
                        line.Dispose();
                    }
                }
            });
        }
    }
}
