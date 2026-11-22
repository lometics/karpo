// The status icons' colour and hover (Islands\IndicatorsIsland.cs): "the status icons should be show
// in the primary color like win11 does it on its taskbar". Windows 11 draws every one of them — the
// microphone and the location as much as airplane mode or a restart waiting — in the taskbar's
// primary text colour, the colour of its clock and of its network, sound and battery icons
// (TextFillColorPrimary: white on dark, near-black on light; the system's text colour in high
// contrast). Karpo's are held to exactly the network, sound and battery icons' colour twice: as the
// painters choose it (IndicatorsIsland.InkOf, IslandInk.SystemIcon), and as the pixels come out,
// each icon drawn offscreen and read back beside theirs. And each lights up on its own square under
// the pointer, as each of those does (Scene.LightsItsOwnCell), not the row of them together.
//
// The status icons stand on the same background as every other widget of the bar (the person,
// 2026-10-08: "all the status icons should show with primary background"): one card over the whole
// entry in the islands look, its icons side by side on it, as any widget's cells are .
// They stood each on a card of its own a gap from the next before, as the tray's icons did.
//
// KARPO_PNG_DIR\status-icons-beside-system-{scale}-{theme}.png: every status icon, then the network,
// sound and battery icons and two tray icons, where the bar's layout puts them and on the cards it
// gives them, at 100 and 150 %, in dark, light and high contrast.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.QuickSettings;

public sealed unsafe class StatusIconColourTests
{
    /// <summary>Every status icon at once, in Windows' order.</summary>
    private static IndicatorState Everything => new()
    {
        Microphone = ["Teams"],
        Camera = ["Teams"],
        Location = ["Maps"],
        Presence = ["Windows Hello"],
        AirplaneMode = true,
        Hotspot = true,
        Metered = true,
        EnergySaver = true,
        DoNotDisturb = true,
        Update = UpdateState.Due,
    };

    private static readonly Look Dark = Karpo.Tests.Folders.TestLooks.LookFor(true);

    private static readonly Look Light = Karpo.Tests.Folders.TestLooks.LookFor(false);

    /// <summary>
    /// High contrast with a text colour that is neither white nor black, so an icon drawn in anything
    /// but the system's text colour shows: yellow on black, as the classic high-contrast theme has it.
    /// </summary>
    private static readonly Look Contrast = Dark with
    {
        HighContrast = true,
        HcWindow = Color.FromArgb(255, 0, 0, 0),
        HcText = Color.FromArgb(255, 0xFF, 0xFF, 0x00),
        HcHighlight = Color.FromArgb(255, 0x1A, 0xEB, 0xFF),
        HcHighlightText = Color.FromArgb(255, 0, 0, 0),
    };

    private static readonly (string Name, Look Look)[] Looks = [("dark", Dark), ("light", Light), ("hc", Contrast)];

    /// <summary>TextFillColorPrimary on light: #E4000000.</summary>
    private const float LightPrimaryAlpha = 0xE4 / 255f;

    private static (float R, float G, float B, float A) Rgba(DXGI_RGBA color) => (color.r, color.g, color.b, color.a);

    // --- The colour each is drawn in ------------------------------------------------------------

    /// <summary>
    /// Each status icon's colour, in light, dark and high contrast, is the network, sound and battery
    /// icons' colour, and that is Windows' primary text colour: white on dark, TextFillColorPrimary's
    /// near-black on light, the system's text colour in high contrast — never the quieter secondary,
    /// and never the accent.
    /// </summary>
    [Theory]
    [InlineData(IndicatorKind.Microphone)]
    [InlineData(IndicatorKind.MicrophoneAndLocation)]
    [InlineData(IndicatorKind.Camera)]
    [InlineData(IndicatorKind.Location)]
    [InlineData(IndicatorKind.Presence)]
    [InlineData(IndicatorKind.AirplaneMode)]
    [InlineData(IndicatorKind.Hotspot)]
    [InlineData(IndicatorKind.Metered)]
    [InlineData(IndicatorKind.EnergySaver)]
    [InlineData(IndicatorKind.DoNotDisturb)]
    [InlineData(IndicatorKind.WindowsUpdate)]
    public void EachIconIsTheTaskbarsPrimaryColour(object which)
    {
        // An object, because the kind is the dock's own and a test's parameters are public.
        var kind = (IndicatorKind)which;
        foreach (var (name, look) in Looks)
        {
            var ink = new IslandInk(look);
            var drawn = Rgba(IndicatorsIsland.InkOf(kind, ink));

            // The network's, the sound's and the battery's own colour.
            Assert.Equal(Rgba(ink.SystemIcon), drawn);
            // The clock's: the bar's primary text, which is what Windows' TextFillColorPrimary is.
            Assert.Equal(Rgba(ink.Text), drawn);
            if (look.HighContrast)
            {
                Assert.Equal((1f, 1f, 0f, 1f), drawn);
                continue;
            }
            Assert.NotEqual(Rgba(ink.Secondary), drawn);
            Assert.NotEqual(Rgba(ink.Accent), drawn);
            if (look.Dark)
            {
                Assert.Equal((1f, 1f, 1f, 1f), drawn);
            }
            else
            {
                Assert.Equal((0f, 0f, 0f), (drawn.R, drawn.G, drawn.B));
                Assert.True(MathF.Abs(drawn.A - LightPrimaryAlpha) <= 0.01f, $"{kind} on {name}: alpha {drawn.A}, TextFillColorPrimary's is {LightPrimaryAlpha}");
            }
        }
    }

    /// <summary>
    /// The pixels agree: every status icon drawn offscreen comes out in exactly the colour the
    /// network, sound and battery icons come out in, at full strength, in dark, light and high
    /// contrast. Each is drawn alone onto a clear surface, as the bar draws a cell, so a pixel's
    /// colour is the ink's times its coverage: the colour is checked on every pixel, and the
    /// strength on the most covered one, which at 150 % is a whole pixel of every glyph's stroke.
    /// </summary>
    [UiFact]
    public void EachIconIsDrawnInTheNetworkSoundAndBatteryIconsColour()
    {
        var wrong = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            BatteryIsland.Probe = () => new SystemStatus.Battery(76, false, false);
            try
            {
                foreach (var (name, look) in Looks)
                {
                    foreach (var scale in new[] { 1f, 1.5f })
                    {
                        var style = Style(scale, look);
                        var ink = new IslandInk(look);
                        var expected = ink.SystemIcon;
                        var drawn = Strip(gfx);
                        try
                        {
                            foreach (var (label, island, index) in drawn)
                            {
                                var (width, height) = island.CellSize(index, style);
                                Offscreen.Bitmap(width, height, dc => island.PaintCell(dc, index, style, width, height), bitmap =>
                                {
                                    // A restart waiting wears Windows' red dot, which is its own colour
                                    // (RestartDotTests): everything else of it is the glyph's.
                                    Func<int, int, bool>? dot = null;
                                    if (island is IndicatorsIsland status && status.Shown[index].Kind == IndicatorKind.WindowsUpdate)
                                    {
                                        var (centre, radius, _) = RestartDot.In(RestartDot.Centred(width, height, new Fit(style).SystemIcon));
                                        dot = (x, y) => Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centre) <= radius + 1f;
                                    }
                                    var (off, strongest) = Pixels(bitmap, (uint)MathF.Ceiling(width), (uint)MathF.Ceiling(height), expected, dot);
                                    var where = $"{label} at {scale * 100:0} % {name}";
                                    if (strongest == 0)
                                    {
                                        wrong.Add($"{where}: drew nothing");
                                        return;
                                    }
                                    if (off > 0)
                                    {
                                        wrong.Add($"{where}: {off} pixels not in the taskbar icons' colour ({expected.r}, {expected.g}, {expected.b})");
                                    }
                                    // Never past the ink's own strength, and at 150 % reaching it: a
                                    // secondary colour (0.78 on dark, 0.62 on light) stops well short.
                                    if (strongest > (expected.a * 255) + 1)
                                    {
                                        wrong.Add($"{where}: alpha {strongest} past the ink's {expected.a * 255:0}");
                                    }
                                    if (scale > 1 && strongest < (expected.a - 0.1f) * 255)
                                    {
                                        wrong.Add($"{where}: at most alpha {strongest}, short of the ink's {expected.a * 255:0}: a quieter colour");
                                    }
                                });
                            }
                        }
                        finally
                        {
                            foreach (var (_, island, _) in drawn.DistinctBy(each => each.Island))
                            {
                                island.Dispose();
                            }
                        }
                    }
                }
            }
            finally
            {
                BatteryIsland.Probe = null!;
            }
        });
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    // --- Under the pointer ----------------------------------------------------------------------

    /// <summary>
    /// The status icons are the one widget whose cell lights up on its own: every one of them is a
    /// button, as each of Windows' is. Every other widget lights its card, as it did.
    /// </summary>
    [Fact]
    public void OnlyTheStatusIconsLightTheirOwnCell()
    {
        var own = BarEntries.All.Select(entry => entry.Kind).Where(Scene.LightsItsOwnCell).ToList();

        Assert.Equal([BarKind.Indicators], own);
    }

    /// <summary>
    /// What lights up under the pointer on a status icon is its own square (the cell's rectangle,
    /// Scene.Dress), and that square is the network icon's card beside it: the same size, across the
    /// bar at the same place. A press shrinks that one cell, as it shrinks the network icon's. The
    /// icons' squares are the whole of their row's card between them, with none left over.
    /// </summary>
    [Theory]
    [InlineData(48, 96u)]
    [InlineData(48, 144u)]
    [InlineData(40, 120u)]
    [InlineData(72, 96u)]
    public void AStatusIconsHoverIsTheNetworkIconsSquare(int height, uint dpi)
    {
        var config = new Config { Height = height };
        PartSpec[] specs =
        [
            new(Config.IslandKey(null, "indicators"), BarKind.Indicators),
            new(Config.IslandKey(null, "keyboard"), BarKind.NightLight),
            new(Config.IslandKey(null, "sound"), BarKind.FolderList),
            new(Config.IslandKey(null, "battery"), BarKind.FolderList),
        ];
        var layout = Layout.ForTests(dpi, new RECT { right = 4000, bottom = 1600 }, Edge.Bottom, config, specs, Layout.CellPx(dpi, config), cells: [3, 1, 1, 1]);

        var network = layout.CardRect(1);
        Assert.False(network.IsEmpty);
        // The network icon lights its card, which is its one cell.
        Assert.Equal(network, layout.ItemRect(Hit.Cell(1, 0)));
        var row = layout.CardRect(0);
        for (var index = 0; index < 3; index++)
        {
            var square = layout.ItemRect(Hit.Cell(0, index));
            Assert.Equal((network.Width, network.Height, network.Y), (square.Width, square.Height, square.Y));
            Assert.True(square.X >= row.X && square.Right <= row.Right, $"icon {index}'s square is outside its row");
        }
        Assert.Equal(row.X, layout.ItemRect(Hit.Cell(0, 0)).X);
        Assert.Equal(row.Right, layout.ItemRect(Hit.Cell(0, 2)).Right);
    }

    // --- A card each ----------------------------------------------------------------------------

    /// <summary>
    /// The person: *"all the status icons should show with primary background"*. The status icons
    /// wear the card every widget wears (§511: every part does), one over the whole entry, where they wore none and a
    /// card per icon instead, as the tray's icons did; each icon lit on its own.
    /// </summary>
    [Fact]
    public void TheStatusIconsLightTheirOwnCellOnTheirCard() => Assert.True(Scene.LightsItsOwnCell(BarKind.Indicators));

    /// <summary>
    /// Each status icon stands on a square the network icon's card's size, side by side with the next
    /// on the entry's one card, which runs on into the network's: the widgets beside it are one group
    /// with it (Layout.Joined). Every point of the card is one icon's to a click.
    /// </summary>
    [Theory]
    [InlineData(48, 96u)]
    [InlineData(48, 144u)]
    [InlineData(40, 120u)]
    [InlineData(72, 96u)]
    public void TheStatusIconsStandSideBySideOnOneCard(int height, uint dpi)
    {
        var layout = Beside(dpi, new Config { Height = height, Style = BarStyle.Islands }, icons: 3);
        var network = layout.CardRect(1);
        var card = layout.CardRect(0);
        var gap = layout.WidgetGap;
        Assert.True(gap > 0f);

        var squares = Enumerable.Range(0, 3).Select(index => layout.ItemRect(Hit.Cell(0, index))).ToList();
        foreach (var square in squares)
        {
            Assert.Equal((network.Width, network.Height, network.Y), (square.Width, square.Height, square.Y));
        }
        for (var index = 1; index < squares.Count; index++)
        {
            Assert.Equal(squares[index - 1].Right, squares[index].X);
        }
        // The one card holds the three, and the network's, the sound's and the battery's run on from
        // it, a widget's gap apart (§501, §515).
        Assert.Equal((squares[0].X, squares[^1].Right, network.Height, network.Y), (card.X, card.Right, card.Height, card.Y));
        Assert.Equal(layout.WidgetGap, network.X - card.Right);
        Assert.Equal(layout.WidgetGap, layout.CardRect(2).X - layout.CardRect(1).Right);

        var middle = squares[0].Y + (squares[0].Height / 2f);
        Assert.Equal(new Hit(HitKind.Island, 0, 0), layout.HitTest(squares[0].Right - 1f, middle));
        Assert.Equal(new Hit(HitKind.Island, 1, 0), layout.HitTest(squares[1].X + 1f, middle));
        Assert.Equal(new Hit(HitKind.Island, 2, 0), layout.HitTest(squares[2].Right - 1f, middle));
    }

    /// <summary>A compact bar has no gaps, between widgets or tray icons, and none between the status icons either.</summary>
    [Fact]
    public void OnACompactBarTheyMeetAsTheTraysIconsDo()
    {
        var layout = Beside(96, new Config { Height = 48, Style = BarStyle.Floating }, icons: 3);

        Assert.True(layout.Connected);
        Assert.Equal(layout.ItemRect(Hit.Cell(0, 0)).Right, layout.ItemRect(Hit.Cell(0, 1)).X);
        Assert.Equal(layout.ItemRect(Hit.Tray(0)).Right, layout.ItemRect(Hit.Tray(1)).X);
    }

    /// <summary>The status icons with <paramref name="icons"/> cells, then the network, sound and battery icons, then a tray of two.</summary>
    private static Layout Beside(uint dpi, Config config, int icons) => Layout.ForTests(
        dpi,
        new RECT { right = 4000, bottom = 1600 },
        Edge.Bottom,
        config,
        [
            new(Config.IslandKey(null, "indicators"), BarKind.Indicators),
            new(Config.IslandKey(null, "keyboard"), BarKind.NightLight),
            new(Config.IslandKey(null, "sound"), BarKind.FolderList),
            new(Config.IslandKey(null, "battery"), BarKind.FolderList),
            new(Config.IslandKey(null, "tray"), BarKind.Tray),
        ],
        Layout.CellPx(dpi, config),
        tray: 2,
        cells: [icons, 1, 1, 2]);

    // --- The picture ----------------------------------------------------------------------------

    /// <summary>
    /// Every status icon, then the network, sound and battery icons and two tray icons, where the
    /// bar's layout puts them and on the cards it gives them — one per widget, the status icons' one
    /// among them, and one per tray icon — at 100 and 150 %, in dark, light and high contrast. What
    /// to look at: one colour across the whole strip, every glyph Windows' own at its size in the
    /// middle of its square, and the status icons on one card as the widgets are.
    /// </summary>
    [UiFact]
    public void TheStatusIconsBesideTheNetworkSoundAndBattery()
    {
        Offscreen.OnStaThread(gfx =>
        {
            BatteryIsland.Probe = () => new SystemStatus.Battery(76, false, false);
            try
            {
                foreach (var (name, look) in Looks)
                {
                    foreach (var scale in new[] { 1f, 1.5f })
                    {
                        var drawn = Strip(gfx);
                        try
                        {
                            var icons = drawn.Count(each => each.Island is IndicatorsIsland);
                            var layout = Beside((uint)(96 * scale), new Config { Height = 48, Style = BarStyle.Islands }, icons);
                            Picture(gfx, layout, look, drawn, $"status-icons-beside-system-{scale * 100:0}-{name}.png");
                        }
                        finally
                        {
                            foreach (var (_, island, _) in drawn.DistinctBy(each => each.Island))
                            {
                                island.Dispose();
                            }
                        }
                    }
                }
            }
            finally
            {
                BatteryIsland.Probe = null!;
            }
        });
    }

    /// <summary>
    /// One strip as the bar draws it (Scene): its colour, a card per widget that wears one and per
    /// cell of the status icons and the tray, then every cell painted into the rectangle the layout
    /// gives it, and the tray's icons in their boxes.
    /// </summary>
    private static void Picture(Gfx gfx, Layout layout, Look look, List<(string Label, CellIsland Island, int Index)> drawn, string file)
    {
        const float pad = 8;
        var parts = Enumerable.Range(0, layout.Parts.Count).Where(part => !layout.BlockRect(part).IsEmpty).ToList();
        var left = parts.Min(part => layout.BlockRect(part).X);
        var right = parts.Max(part => layout.BlockRect(part).Right);
        var top = parts.Min(part => layout.BlockRect(part).Y);
        var bottom = parts.Max(part => layout.BlockRect(part).Bottom);
        // Scene's CardBackgroundFillColorDefault and CardStrokeColorDefault; high contrast draws no card fill.
        var (fill, stroke) = look.Dark
            ? (new DXGI_RGBA(1f, 1f, 1f, 0.051f), new DXGI_RGBA(0f, 0f, 0f, 0.098f))
            : (new DXGI_RGBA(1f, 1f, 1f, 0.702f), new DXGI_RGBA(0f, 0f, 0f, 0.0588f));
        Offscreen.Bitmap(right - left + (2 * pad), bottom - top + (2 * pad), dc =>
        {
            var background = look.HighContrast
                ? new DXGI_RGBA(0, 0, 0, 1)
                : look.Dark ? new DXGI_RGBA(0x20 / 255f, 0x20 / 255f, 0x20 / 255f, 1) : new DXGI_RGBA(0xF3 / 255f, 0xF3 / 255f, 0xF3 / 255f, 1);
            dc->Clear(&background);
            Offscreen.Shifted(dc, pad - left, pad - top, () =>
            {
                void Card(RectF box)
                {
                    if (look.HighContrast || box.IsEmpty)
                    {
                        return;
                    }
                    var rect = PopupPaint.Rect(box.X, box.Y, box.Width, box.Height);
                    PopupPaint.Fill(dc, rect, layout.CardRadius, fill);
                    PopupPaint.Stroke(dc, rect, layout.CardRadius, layout.Stroke, stroke);
                }

                var islands = drawn.Select(each => each.Island).Distinct().ToList();
                for (var part = 0; part < layout.Parts.Count; part++)
                {
                    var kind = layout.Parts[part].Kind;
                    // The tray's card is its block (Layout.CardRect has no cells for it), as the bar draws it.
                    Card(kind == BarKind.Tray ? layout.BlockRect(part) : layout.CardRect(part));
                    if (kind == BarKind.Tray)
                    {
                        var size = (int)MathF.Ceiling(layout.TrayIconSize);
                        for (var index = 0; index < layout.Parts[part].Count; index++)
                        {
                            if (Karpo.Tests.Islands.BarCardsOffscreenTests.Icon(index + 5, size) is { } pixels && layout.TrayIconRect(index) is { IsEmpty: false } box)
                            {
                                PopupPaint.Picture(dc, pixels, PopupPaint.Rect(box.X, box.Y, box.Width, box.Height), 0);
                            }
                        }
                        continue;
                    }
                    // The widgets in the strip's order: the status icons, the network, the sound, the battery.
                    if (part >= islands.Count)
                    {
                        continue;
                    }
                    var island = islands[part];
                    var style = layout.StyleFor(part, look);
                    for (var cell = 0; cell < layout.CellCount(part); cell++)
                    {
                        if (layout.ItemRect(Hit.Cell(part, cell)) is not { IsEmpty: false } square)
                        {
                            continue;
                        }
                        Offscreen.Shifted(dc, square.X, square.Y, () => island.PaintCell(dc, cell, style, square.Width, square.Height));
                    }
                }
            });
        }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, file)));
    }

    // --- Making them ----------------------------------------------------------------------------

    private static IslandStyle Style(float scale, Look look) =>
        new(scale, false, Layout.CellPx(96, new Config { Height = 48 }) * scale, Layout.IconPx(96, new Config { Height = 48 }) * scale, look);

    /// <summary>
    /// Every cell of the strip, in the bar's order: the status icons (one widget, a cell each), then
    /// the network, the sound and the battery. The caller disposes each widget once.
    /// </summary>
    private static List<(string Label, CellIsland Island, int Index)> Strip(Gfx gfx)
    {
        var indicators = StatusIcons(gfx, new SampleIndicators(Everything));
        IIsland entry = indicators;
        var count = entry.Measure(Style(1f, Dark)).Count;
        var strip = new List<(string, CellIsland, int)>();
        for (var index = 0; index < count; index++)
        {
            strip.Add((indicators.Shown[index].Kind.ToString(), indicators, index));
        }
        // A full signal, as SystemStatus.Compose has it: one glyph, with no dim arcs under it (those
        // are for a weak signal only; drawn under a full one, the two layers' alphas add up).
        var state = new SystemState("", "");
        // The sound, the sound preset's face.
        FolderIsland.StatusStand = state;
        try
        {
            var sound = Karpo.Core.Presets.Folder(Karpo.Core.Presets.Find(Karpo.Core.Presets.SoundId)!, new Karpo.Core.PresetMachine(HasBattery: false, FixedDrives: []));
            strip.Add(("Sound", new FolderIsland(Config.IslandKey(null, sound.Id), sound, gfx, new Config()), 0));
        }
        finally
        {
            FolderIsland.StatusStand = null;
        }
        // The battery: the battery preset's face, on the charge the test's probe reads.
        var battery = Karpo.Core.Presets.Folder(Karpo.Core.Presets.Find(Karpo.Core.Presets.BatteryId)!, new Karpo.Core.PresetMachine(HasBattery: true, FixedDrives: []));
        battery.Id = "battery";
        strip.Add(("Battery", new FolderIsland(Config.IslandKey(null, battery.Id), battery, gfx, new Config()), 0));
        return strip;
    }

    /// <summary>The Status icons entry, on a source of the test's own: nothing is read from this PC and nothing is opened.</summary>
    private static IndicatorsIsland StatusIcons(Gfx gfx, SampleIndicators source)
    {
        IndicatorSources.Made = () => source;
        try
        {
            var entry = BarKind.Indicators.New();
            entry.Id = "indicators";
            return new IndicatorsIsland(Config.IslandKey(null, entry.Id), entry, gfx, new Config()) { Opened = _ => { } };
        }
        finally
        {
            IndicatorSources.Made = null;
        }
    }

    /// <summary>
    /// How many inked pixels of <paramref name="bitmap"/> are not <paramref name="ink"/>, leaving out the
    /// ones <paramref name="skip"/> names (premultiplied BGRA on a clear surface: each channel is the
    /// ink's times the pixel's alpha, give or take the rounding), and the strongest alpha drawn, 0 to 255.
    /// </summary>
    private static (int Off, int Strongest) Pixels(IWICBitmap* bitmap, uint width, uint height, DXGI_RGBA ink, Func<int, int, bool>? skip = null)
    {
        var pixels = new uint[width * height];
        fixed (uint* into = pixels)
        {
            ComUtil.Check(bitmap->CopyPixels(null, width * 4, width * height * 4, (byte*)into));
        }
        var off = 0;
        var strongest = 0;
        for (var at = 0; at < pixels.Length; at++)
        {
            var pixel = pixels[at];
            var a = (int)(pixel >> 24);
            if (a == 0 || skip?.Invoke(at % (int)width, at / (int)width) == true)
            {
                continue;
            }
            strongest = Math.Max(strongest, a);
            var (r, g, b) = ((int)((pixel >> 16) & 0xFF), (int)((pixel >> 8) & 0xFF), (int)(pixel & 0xFF));
            if (!Near(r, ink.r, a) || !Near(g, ink.g, a) || !Near(b, ink.b, a))
            {
                off++;
            }
        }
        return (off, strongest);
    }

    /// <summary>Whether a premultiplied <paramref name="channel"/> is <paramref name="want"/> at <paramref name="alpha"/>, give or take the rounding.</summary>
    private static bool Near(int channel, float want, int alpha) => Math.Abs(channel - (want * alpha)) <= 2;
}
