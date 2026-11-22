// A VPN connected, as Windows shows it (the person, 2026-10-04: "same like Windows"): no status icon of
// its own, but a small shield badge (F8C0) on the network icon, in Windows' place and size — the
// network glyph cut away for it ("TopLeftCutout", NetworkIcon.dll), the shield over it at the glyph's
// own size and origin — and the VPN named in the icon's hover and Narrator name (SystemStatus.Compose,
// SystemGlyph.Paint, Dock.Uia). The network icon is the Wi-Fi preset's face since 2026-10-10
// (StatusFaceDrawing), drawn as the network button was. The VPN switch stays the widget it was. Every state is made up:
// nothing reads this PC's connections.
//
// KARPO_PNG_DIR\network-vpn-{scale}-{theme}.png: the network icon for Wi-Fi at four, three, two and
// one bars and for a cable, each without and with a VPN, at 100 and 150 %, dark and light.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;

namespace Karpo.Tests.QuickSettings;

public sealed unsafe class NetworkIconVpnTests
{
    private static SystemStatus.Connection WiFi(int bars, bool internet = true) => new(SystemStatus.Medium.WiFi, internet, "Home", bars);

    private static SystemStatus.Connection Cable => new(SystemStatus.Medium.Ethernet, true, "Office", 0);

    private static readonly SystemStatus.Audio Speakers = new(true, "Speakers", 40, false);

    private static SystemState Of(SystemStatus.Connection network, string? vpn = null) => SystemStatus.Compose(network, Speakers, 1, vpn);

    /// <summary>Without a VPN the network icon is as it was: no shield, and nothing said of one.</summary>
    [Fact]
    public void WithoutAVpnThereIsNoShield()
    {
        var state = Of(WiFi(4));
        Assert.Null(state.NetworkBadge);
        Assert.Null(state.Vpn);
        Assert.Equal("", state.NetworkGlyph);
        Assert.Equal("Home\nInternet access", state.NetworkTip);
        Assert.Null(Of(Cable, vpn: "").NetworkBadge);
    }

    /// <summary>
    /// With a VPN connected the icon wears Windows' shield over the glyph Windows cuts away for it:
    /// Wi-Fi at four, three and two bars and the cable have their cut forms (F8C4, F8C3, F8C2, F8CC),
    /// the dimmed arcs a weaker signal does not reach too; one bar and cellular leave the shield's
    /// corner empty and stay as they are.
    /// </summary>
    [Theory]
    [InlineData(4, "", null)]
    [InlineData(3, "", "")]
    [InlineData(2, "", "")]
    [InlineData(1, "", "")]
    public void WithAVpnTheWiFiIconWearsTheShield(int bars, string glyph, string? back)
    {
        var state = Of(WiFi(bars), "Work");
        Assert.Equal("", state.NetworkBadge);
        Assert.Equal(glyph, state.NetworkGlyph);
        Assert.Equal(back, state.NetworkBackGlyph);
        Assert.Equal("Work", state.Vpn);
    }

    [Fact]
    public void WithAVpnTheCableAndCellularWearTheShield()
    {
        var cable = Of(Cable, "Work");
        Assert.Equal(("", ""), (cable.NetworkGlyph, cable.NetworkBadge));
        var cellular = Of(new SystemStatus.Connection(SystemStatus.Medium.Cellular, true, "Carrier", 0), "Work");
        Assert.Equal(("", ""), (cellular.NetworkGlyph, cellular.NetworkBadge));
    }

    /// <summary>
    /// Where Windows has no glyph to wear the shield — no internet, not connected — none is drawn over
    /// the glyph, which stays whole; the hover still names the VPN.
    /// </summary>
    [Fact]
    public void WithoutAGlyphCutForItThereIsNoShieldButTheHoverSaysIt()
    {
        foreach (var network in new[] { WiFi(4, internet: false), WiFi(2, internet: false), new SystemStatus.Connection(SystemStatus.Medium.Ethernet, false, "Office", 0), default })
        {
            var plain = Of(network);
            var state = Of(network, "Work");
            Assert.Null(state.NetworkBadge);
            Assert.Equal((plain.NetworkGlyph, plain.NetworkBackGlyph), (state.NetworkGlyph, state.NetworkBackGlyph));
            Assert.EndsWith("\nConnected to VPN: Work", state.NetworkTip, StringComparison.Ordinal);
        }
    }

    /// <summary>The hover names the VPN under the connection, as the status icon used to; two are named together.</summary>
    [Fact]
    public void TheHoverNamesTheVpn()
    {
        Assert.Equal("Home\nInternet access\nConnected to VPN: Work", Of(WiFi(4), "Work").NetworkTip);
        Assert.Equal("Office\nInternet access\nConnected to VPN: Work, Home", Of(Cable, "Work, Home").NetworkTip);
    }

    /// <summary>Only the connections that are up count: one connecting or down is no VPN connected.</summary>
    [Fact]
    public void OnlyAConnectionThatIsUpIsAVpnConnected()
    {
        Assert.Null(SystemStatus.VpnNames(null));
        Assert.Null(SystemStatus.VpnNames([]));
        Assert.Null(SystemStatus.VpnNames([new VpnConnection("Work", "", VpnState.Connecting), new VpnConnection("Home", "", VpnState.Disconnected)]));
        Assert.Equal("Work", SystemStatus.VpnNames([new VpnConnection("Work", "", VpnState.Connected), new VpnConnection("Home", "", VpnState.Disconnected)]));
        Assert.Equal("Work, Home", SystemStatus.VpnNames([new VpnConnection("Work", "", VpnState.Connected), new VpnConnection("Home", "", VpnState.Connected)]));
    }

    /// <summary>
    /// On the bar: only the network's face wears the shield — the sound and the Bluetooth faces
    /// never do — its hover names the VPN, and the folder wearing it says the VPN to Narrator while
    /// one is connected, and only then.
    /// </summary>
    [UiFact]
    public void OnlyTheNetworkIconWearsItAndNarratorSaysIt()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var on = Of(WiFi(4), "Work");
            var off = Of(WiFi(4));
            Assert.Equal("", StatusFaceDrawing.Glyphs(FolderFaces.WifiId, on).Badge);
            Assert.Null(StatusFaceDrawing.Glyphs(FolderFaces.SoundId, on).Badge);
            Assert.Null(StatusFaceDrawing.Glyphs(FolderFaces.BluetoothId, on).Badge);
            Assert.Contains("Connected to VPN: Work", StatusFaceDrawing.Tip(FolderFaces.WifiId, on), StringComparison.Ordinal);
            Assert.Equal("Home, connected to VPN Work", S.WidgetSystemControlVpnSpoken(name: "Home", vpn: "Work"));

            var holder = Presets.Folder(Presets.Find(Presets.WifiId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
            using var folder = new FolderIsland(Config.IslandKey(null, holder.Id), holder, gfx, new Config());
            folder.SetStatus(on, gfx);
            Assert.Equal("Work", folder.Vpn);
            Assert.Contains("Connected to VPN: Work", folder.Tooltip(0), StringComparison.Ordinal);
            folder.SetStatus(off, gfx);
            Assert.Null(folder.Vpn);
            Assert.Null(StatusFaceDrawing.Glyphs(FolderFaces.WifiId, off).Badge);
        });
    }

    /// <summary>The Wi-Fi preset's folder, its face showing <paramref name="state"/>.</summary>
    private static FolderIsland Wifi(Gfx gfx, SystemState state)
    {
        FolderIsland.StatusStand = state;
        try
        {
            var holder = Presets.Folder(Presets.Find(Presets.WifiId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
            return new FolderIsland(Config.IslandKey(null, holder.Id), holder, gfx, new Config());
        }
        finally
        {
            FolderIsland.StatusStand = null;
        }
    }

    /// <summary>The network's face for a state: one glyph's square, drawn as the face draws it.</summary>
    private sealed class Icon(Gfx gfx, SystemState state) : IDisposable
    {
        private readonly FolderIsland measure = Wifi(gfx, state);

        public (float Width, float Height) CellSize(int index, IslandStyle style) => measure.CellSize(index, style);

        public void PaintCell(ID2D1DeviceContext* dc, int index, IslandStyle style, float width, float height)
        {
            var (glyph, back, dim, badge) = StatusFaceDrawing.Glyphs(FolderFaces.WifiId, state);
            SystemGlyph.Paint(dc, gfx, style, glyph, back, dim, badge, new D2D_RECT_F(0, 0, width, height));
        }

        public void Dispose() => measure.Dispose();
    }

    private static IslandStyle Style(float scale, Look look) =>
        new(scale, false, Layout.CellPx(96, new Config { Height = 48 }) * scale, Layout.IconPx(96, new Config { Height = 48 }) * scale, look);

    /// <summary>
    /// Drawn offscreen at 100 and 150 %, dark and light: with a VPN the shield is in the top-left of
    /// the icon, where Windows puts it, the glyph under it does not move (its ink's right and bottom
    /// edges are where they were without the VPN, to a pixel), and the whole icon stays in its cell.
    /// The sheet is written for a look: each network without and with a VPN, side by side.
    /// </summary>
    [UiFact]
    public void TheShieldIsInWindowsPlaceAndTheIconDoesNotMove()
    {
        var wrong = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            (string Name, SystemStatus.Connection Network)[] networks = [("4 bars", WiFi(4)), ("3 bars", WiFi(3)), ("2 bars", WiFi(2)), ("1 bar", WiFi(1)), ("cable", Cable)];
            foreach (var (scale, dark) in new[] { (1f, true), (1f, false), (1.5f, true), (1.5f, false) })
            {
                var look = Karpo.Tests.Folders.TestLooks.LookFor(dark);
                var style = Style(scale, look);
                var where = $"{scale * 100:0} % {(dark ? "dark" : "light")}";
                var icons = networks.SelectMany(each => new[] { (each.Name, Vpn: false, Island: new Icon(gfx, Of(each.Network))), (each.Name, Vpn: true, Island: new Icon(gfx, Of(each.Network, "Work"))) }).ToList();
                try
                {
                    var (width, height) = icons[0].Island.CellSize(0, style);
                    Offscreen.Bitmap((width + 8) * icons.Count + 8, height + 16, dc =>
                    {
                        var background = dark ? new DXGI_RGBA(0x20 / 255f, 0x20 / 255f, 0x20 / 255f, 1) : new DXGI_RGBA(0xF3 / 255f, 0xF3 / 255f, 0xF3 / 255f, 1);
                        dc->Clear(&background);
                        for (var index = 0; index < icons.Count; index++)
                        {
                            var island = icons[index].Island;
                            Offscreen.Shifted(dc, 8 + (index * (width + 8)), 8, () => island.PaintCell(dc, 0, style, width, height));
                        }
                    }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"network-vpn-{scale * 100:0}-{(dark ? "dark" : "light")}.png")));

                    for (var index = 0; index < icons.Count; index += 2)
                    {
                        var plain = Ink(icons[index].Island, style, width, height);
                        var shielded = Ink(icons[index + 1].Island, style, width, height);
                        var name = icons[index].Name;
                        if (Math.Abs(plain.Right - shielded.Right) > 1 || Math.Abs(plain.Bottom - shielded.Bottom) > 1)
                        {
                            wrong.Add($"{name} at {where}: the icon moved with the VPN, its ink ends at {shielded.Right},{shielded.Bottom}, without at {plain.Right},{plain.Bottom}");
                        }
                        if (shielded.Left < 1 || shielded.Top < 1 || shielded.Right > width - 1 || shielded.Bottom > height - 1)
                        {
                            wrong.Add($"{name} at {where}: with the VPN, ink {shielded.Left},{shielded.Top} to {shielded.Right},{shielded.Bottom} in {width}x{height}");
                        }
                        // The shield is the top-left of the icon: more ink there with it than without.
                        var size = new Fit(style).SystemIcon;
                        var corner = (Left: shielded.Right - size, Top: shielded.Bottom - size);
                        if (shielded.InCorner(corner, size) <= plain.InCorner(corner, size))
                        {
                            wrong.Add($"{name} at {where}: no shield in the icon's top-left corner");
                        }
                    }
                }
                finally
                {
                    foreach (var (_, _, island) in icons)
                    {
                        island.Dispose();
                    }
                }
            }
        });
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    /// <summary>One icon's ink: its box, and how much of it is in a corner of the icon's square.</summary>
    private static Inked Ink(Icon island, IslandStyle style, float width, float height)
    {
        Inked found = default;
        Offscreen.Bitmap(width, height, dc => island.PaintCell(dc, 0, style, width, height), bitmap =>
        {
            var (w, h) = ((uint)MathF.Ceiling(width), (uint)MathF.Ceiling(height));
            var (left, top, right, bottom) = Offscreen.InkOf(bitmap, w, h);
            var pixels = new uint[w * h];
            fixed (uint* into = pixels)
            {
                Karpo.Dock.Native.ComUtil.Check(bitmap->CopyPixels(null, w * 4, w * h * 4, (byte*)into));
            }
            found = new Inked(left, top, right, bottom, pixels, w);
        });
        return found;
    }

    private readonly record struct Inked(float Left, float Top, float Right, float Bottom, uint[] Pixels, uint Width)
    {
        /// <summary>How many inked pixels are in the top-left 45 % of the icon's square at <paramref name="corner"/>.</summary>
        public int InCorner((float Left, float Top) corner, float size)
        {
            var count = 0;
            for (var at = 0; at < Pixels.Length; at++)
            {
                var (x, y) = (at % (int)Width, at / (int)Width);
                if ((Pixels[at] >> 24) > 64 && x >= corner.Left && x < corner.Left + (size * 0.45f) && y >= corner.Top && y < corner.Top + (size * 0.45f))
                {
                    count++;
                }
            }
            return count;
        }
    }
}
