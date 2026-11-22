// The Status icons entry (Islands\IndicatorsIsland.cs, QuickSettings\Services\Indicators.cs): which
// icons a state shows and in what order, what each says, opens and offers, how the apps using the
// microphone are read from Windows' usage keys, each icon's glyph (Windows' own, Indicators.cs says
// where each was read) and the microphone and the location in use together as Windows' one icon, and
// the entry on the bar — no room with nothing to say, a cell per icon, drawn offscreen at the tray's
// size and in the middle of its square. Every state is made up: nothing reads this PC's privacy
// keys, radios or connections, and nothing is opened or turned off.
//
// KARPO_PNG_DIR\indicators-{scale}-{theme}.png: the entry with every icon on, and with three.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;

namespace Karpo.Tests.QuickSettings;

public sealed unsafe class IndicatorsTests
{
    /// <summary>Everything Windows' taskbar can show, all at once.</summary>
    private static IndicatorState Everything => new()
    {
        Microphone = ["Teams", "OBS Studio"],
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

    /// <summary>Nothing is so: no icon at all.</summary>
    [Fact]
    public void NothingShowsNothing() => Assert.Empty(Indicators.Of(IndicatorState.Nothing));

    /// <summary>
    /// A VPN connected is no status icon, as it is none of Windows' (the person, 2026-10-04: "same like
    /// Windows"): Windows shows it only as the shield on its network icon, and so does Karpo
    /// (NetworkIconVpnTests). There is no such icon, no state for one and nothing to disconnect here;
    /// the VPN switch is its own widget, as it was.
    /// </summary>
    [Fact]
    public void AVpnIsNoStatusIcon()
    {
        Assert.DoesNotContain(Enum.GetNames<IndicatorKind>(), name => name.Contains("Vpn", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(Enum.GetNames<IndicatorAct>(), name => name.Contains("Vpn", StringComparison.OrdinalIgnoreCase));
        Assert.Null(typeof(IndicatorState).GetProperty("Vpn"));
        // Everything else on at once: nothing in the set is the network's shield or opens VPN settings.
        Assert.All(Indicators.Of(Everything), indicator =>
        {
            Assert.NotEqual("", indicator.Glyph);
            Assert.NotEqual("", indicator.Glyph);
            Assert.NotEqual("ms-settings:network-vpn", indicator.Uri);
            Assert.DoesNotContain("VPN", indicator.Tooltip, StringComparison.Ordinal);
        });
        // The VPN switch is still a widget of its own, a line of a list; on the bar it is the VPN preset.
        Assert.NotNull(ListWidgets.Find("vpn"));
        Assert.Equal(QuickSwitch.Vpn, WidgetInfos.Vpn.Switch);
    }

    /// <summary>
    /// Everything is so: every icon, in Windows' order — privacy, network, power, notifications,
    /// updates — the microphone and the location as their one icon, in the microphone's place.
    /// </summary>
    [Fact]
    public void EverythingShowsInWindowsOrder() => Assert.Equal(
        [
            IndicatorKind.MicrophoneAndLocation, IndicatorKind.Camera, IndicatorKind.Presence,
            IndicatorKind.AirplaneMode, IndicatorKind.Hotspot, IndicatorKind.Metered,
            IndicatorKind.EnergySaver, IndicatorKind.DoNotDisturb, IndicatorKind.WindowsUpdate,
        ],
        Indicators.Of(Everything).Select(indicator => indicator.Kind));

    /// <summary>Each icon is shown exactly while its own part of the state is so, and no other's.</summary>
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
    public void EachIsShownByItsOwnState(object which)
    {
        // An object, because the kind is the dock's own and a test's parameters are public.
        var kind = (IndicatorKind)which;
        var shown = Assert.Single(Indicators.Of(StateOf(kind)));
        Assert.Equal(kind, shown.Kind);
        Assert.StartsWith("ms-settings:", shown.Uri);
        Assert.NotEmpty(shown.Menu);
        Assert.InRange(shown.Glyph.Length, 1, 2);
    }

    /// <summary>The state that shows <paramref name="kind"/> and nothing else.</summary>
    private static IndicatorState StateOf(IndicatorKind kind) => kind switch
    {
        IndicatorKind.Microphone => new IndicatorState { Microphone = ["Teams"] },
        IndicatorKind.MicrophoneAndLocation => new IndicatorState { Microphone = ["Teams"], Location = ["Maps"] },
        IndicatorKind.Camera => new IndicatorState { Camera = ["Teams"] },
        IndicatorKind.Location => new IndicatorState { Location = ["Maps"] },
        IndicatorKind.Presence => new IndicatorState { Presence = ["Windows Hello"] },
        IndicatorKind.AirplaneMode => new IndicatorState { AirplaneMode = true },
        IndicatorKind.Hotspot => new IndicatorState { Hotspot = true },
        IndicatorKind.Metered => new IndicatorState { Metered = true },
        IndicatorKind.EnergySaver => new IndicatorState { EnergySaver = true },
        IndicatorKind.DoNotDisturb => new IndicatorState { DoNotDisturb = true },
        _ => new IndicatorState { Update = UpdateState.Due },
    };

    /// <summary>
    /// Each icon is the glyph Windows draws for it, a Segoe Fluent Icons code point; where each was
    /// read is at the top of Indicators.cs: the privacy icon's three from SystemTray.dll (the location
    /// is Windows' arrow, not the pin it was), the camera from the "Camera on" flash, presence sensing
    /// and the hotspot from Settings' pages, airplane mode from the network icon, energy saver
    /// from the tray's battery font, do not disturb from the tray's bell, and a restart from Windows
    /// Update's page. Metered is Karpo's own: Windows shows no sign of it.
    /// </summary>
    [Theory]
    [InlineData(IndicatorKind.Microphone, 0xE720)]
    [InlineData(IndicatorKind.MicrophoneAndLocation, 0xF47F)]
    [InlineData(IndicatorKind.Camera, 0xE722)]
    [InlineData(IndicatorKind.Location, 0xE37A)]
    [InlineData(IndicatorKind.Presence, 0xE425)]
    [InlineData(IndicatorKind.AirplaneMode, 0xE709)]
    [InlineData(IndicatorKind.Hotspot, 0xE704)]
    [InlineData(IndicatorKind.Metered, 0xE8EC)]
    [InlineData(IndicatorKind.EnergySaver, 0xF1E8)]
    [InlineData(IndicatorKind.DoNotDisturb, 0xF285)]
    [InlineData(IndicatorKind.WindowsUpdate, 0xE895)]
    public void EachIsWindowsOwnGlyph(object which, int code)
    {
        var kind = (IndicatorKind)which;
        var want = char.ConvertFromUtf32(code);

        Assert.Equal(want, Indicators.Glyph(kind));
        Assert.Equal(want, Assert.Single(Indicators.Of(StateOf(kind))).Glyph);
    }

    /// <summary>Every kind has a glyph of its own: no two states look alike.</summary>
    [Fact]
    public void NoTwoKindsShareAGlyph()
    {
        var glyphs = Enum.GetValues<IndicatorKind>().Select(Indicators.Glyph).ToList();

        Assert.Equal(glyphs.Count, glyphs.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// The microphone and the location in use together are one icon, as in Windows' tray: F47F, in
    /// the microphone's place, its hover naming both lists of apps, its click opening the privacy
    /// page (Windows opens that for F47F, and each one's own page for E720 and E37A), and its menu
    /// both pages. Either alone is its own icon again, and the camera, which Windows does not fold
    /// in, stays an icon of its own beside them.
    /// </summary>
    [Fact]
    public void TheMicrophoneAndTheLocationTogetherAreOneIcon()
    {
        var both = Indicators.Of(new IndicatorState { Microphone = ["Teams", "OBS Studio"], Location = ["Maps"] });

        var one = Assert.Single(both);
        Assert.Equal(IndicatorKind.MicrophoneAndLocation, one.Kind);
        Assert.Equal("\uF47F", one.Glyph);
        Assert.Equal("Microphone in use by:\nTeams\nOBS Studio\nLocation in use by:\nMaps", one.Tooltip);
        Assert.Equal("ms-settings:privacy", one.Uri);
        Assert.Equal(
            [("Microphone privacy settings", "ms-settings:privacy-microphone"), ("Location privacy settings", "ms-settings:privacy-location")],
            one.Menu.Select(row => (row.Text, row.Uri)));
        Assert.All(one.Menu, row => Assert.Equal(IndicatorAct.Open, row.Act));

        // Either alone: its own icon, its own page.
        var microphone = Assert.Single(Indicators.Of(new IndicatorState { Microphone = ["Teams"] }));
        Assert.Equal(("\uE720", "ms-settings:privacy-microphone"), (microphone.Glyph, microphone.Uri));
        var location = Assert.Single(Indicators.Of(new IndicatorState { Location = ["Maps"] }));
        Assert.Equal(("\uE37A", "ms-settings:privacy-location"), (location.Glyph, location.Uri));

        // The camera is not folded in, with either or both.
        Assert.Equal(
            [IndicatorKind.MicrophoneAndLocation, IndicatorKind.Camera],
            Indicators.Of(new IndicatorState { Microphone = ["Teams"], Camera = ["Teams"], Location = ["Maps"] }).Select(indicator => indicator.Kind));
        Assert.Equal(
            [IndicatorKind.Microphone, IndicatorKind.Camera],
            Indicators.Of(new IndicatorState { Microphone = ["Teams"], Camera = ["Teams"] }).Select(indicator => indicator.Kind));
        Assert.Equal(
            [IndicatorKind.Camera, IndicatorKind.Location],
            Indicators.Of(new IndicatorState { Camera = ["Teams"], Location = ["Maps"] }).Select(indicator => indicator.Kind));
    }

    /// <summary>A privacy icon's hover names the apps using it, as Windows' does; its click and its menu are that capability's page.</summary>
    [Fact]
    public void APrivacyIconNamesItsApps()
    {
        var microphone = Indicators.Of(new IndicatorState { Microphone = ["Teams", "OBS Studio"] })[0];

        Assert.Equal("Microphone in use by:\nTeams\nOBS Studio", microphone.Tooltip);
        Assert.Equal("ms-settings:privacy-microphone", microphone.Uri);
        var row = Assert.Single(microphone.Menu);
        Assert.Equal(("Microphone privacy settings", IndicatorAct.Open, "ms-settings:privacy-microphone"), (row.Text, row.Act, row.Uri));
        Assert.Equal("ms-settings:privacy-webcam", Indicators.Of(new IndicatorState { Camera = ["Teams"] })[0].Uri);
        Assert.Equal("ms-settings:privacy-location", Indicators.Of(new IndicatorState { Location = ["Maps"] })[0].Uri);
    }

    /// <summary>What can be turned off is offered first, then its page; a restart waiting offers Windows Update's pages alone.</summary>
    [Fact]
    public void WhatCanBeTurnedOffIsOffered()
    {
        var all = Indicators.Of(Everything).ToDictionary(indicator => indicator.Kind);

        Assert.Equal(IndicatorAct.TurnOffAirplaneMode, all[IndicatorKind.AirplaneMode].Menu[0].Act);
        Assert.Equal(IndicatorAct.TurnOffHotspot, all[IndicatorKind.Hotspot].Menu[0].Act);
        Assert.Equal(IndicatorAct.TurnOffEnergySaver, all[IndicatorKind.EnergySaver].Menu[0].Act);
        Assert.Equal(IndicatorAct.TurnOffDoNotDisturb, all[IndicatorKind.DoNotDisturb].Menu[0].Act);
        // A restart is Windows Update's to do: its page and its restart options, the Windows Update
        // widget's own two rows, never a restart from here.
        var restart = all[IndicatorKind.WindowsUpdate];
        Assert.Equal("ms-settings:windowsupdate", restart.Uri);
        Assert.Equal(
            [("Windows Update settings", "ms-settings:windowsupdate"), ("Restart options", "ms-settings:windowsupdate-options")],
            restart.Menu.Select(row => (row.Text, row.Uri)));
        Assert.All(restart.Menu, row => Assert.Equal(IndicatorAct.Open, row.Act));
        // Every row that opens something names what.
        Assert.All(all.Values.SelectMany(indicator => indicator.Menu), row => Assert.True(row.Act != IndicatorAct.Open || row.Uri is { Length: > 0 }));
    }

    /// <summary>
    /// The apps using a capability, from its usage keys as Windows writes them: using it while it has
    /// started and not stopped; a key granted and never used, or stopped, is not using it. A packaged
    /// app is named by its package, a desktop one by its file, and each once.
    /// </summary>
    [Fact]
    public void TheAppsInUseAreReadFromWindowsUsage()
    {
        (string, bool, long, long)[] usage =
        [
            ("5319275A.WhatsAppDesktop_cv1g1gvanyjgm", true, 134346536882896827, 0),
            ("Microsoft.GamingApp_8wekyb3d8bbwe", true, 0, 0),
            ("Claude_pzs8sxrjxfjjc", true, 134249957684843384, 134249958800652862),
            (@"C:#Program Files#obs-studio#bin#64bit#obs64.exe", false, 134271282414301862, 0),
            (@"C:#Program Files#obs-studio#bin#64bit#obs64.exe", false, 134271282414301862, 0),
            (@"C:#Tools#probe.exe", false, 134271282414301862, 134271282414301900),
        ];
        var seen = new List<string>();

        var names = Indicators.InUse(usage, path =>
        {
            seen.Add(path);
            return path.EndsWith("obs64.exe", StringComparison.Ordinal) ? "OBS Studio" : Path.GetFileNameWithoutExtension(path);
        });

        Assert.Equal(["WhatsAppDesktop", "OBS Studio"], names);
        // The path Windows writes with "#" is described as the path it is.
        Assert.Contains(@"C:\Program Files\obs-studio\bin\64bit\obs64.exe", seen);
    }

    [Theory]
    [InlineData("5319275A.WhatsAppDesktop_cv1g1gvanyjgm", "WhatsAppDesktop")]
    [InlineData("Microsoft.WindowsCamera_8wekyb3d8bbwe", "WindowsCamera")]
    [InlineData("Claude_pzs8sxrjxfjjc", "Claude")]
    [InlineData("Plain", "Plain")]
    public void APackageIsNamedByItsName(string family, string name) => Assert.Equal(name, Indicators.PackageName(family));

    // --- On the bar ----------------------------------------------------------------------------

    private static readonly Look Dark = Karpo.Tests.Folders.TestLooks.LookFor(true);

    private static IslandStyle Style(float scale = 1f, Look? look = null) =>
        new(scale, false, Layout.CellPx(96, new Config { Height = 48 }) * scale, Layout.IconPx(96, new Config { Height = 48 }) * scale, look ?? Dark);

    /// <summary>The entry on a bar, made on a source of the test's own.</summary>
    private static IndicatorsIsland Make(Gfx gfx, SampleIndicators source, List<string>? opened = null)
    {
        IndicatorSources.Made = () => source;
        try
        {
            var entry = BarKind.Indicators.New();
            entry.Id = "indicators";
            // The factory makes this class for the entry; the test makes its own, which opens nothing.
            using (var made = Karpo.Dock.Islands.Create(Config.IslandKey(null, entry.Id), entry, gfx, new Config()))
            {
                Assert.IsType<IndicatorsIsland>(made);
            }
            return new IndicatorsIsland(Config.IslandKey(null, entry.Id), entry, gfx, new Config()) { Opened = opened is null ? _ => { } : opened.Add };
        }
        finally
        {
            IndicatorSources.Made = null;
        }
    }

    /// <summary>With nothing to say the entry takes no room; with three things, three cells, one square each.</summary>
    [UiFact]
    public void OnTheBarItIsACellPerIconAndNoRoomWithout()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var source = new SampleIndicators();
            using var island = Make(gfx, source);
            IIsland entry = island;
            Assert.Equal(0, entry.Measure(Style()).Count);

            source.Tell(new IndicatorState { Microphone = ["Teams"], Hotspot = true, Update = UpdateState.Due });
            Assert.True(entry.Refresh(gfx));
            var cells = entry.Measure(Style());
            Assert.Equal(3, cells.Count);
            Assert.Null(cells.Lengths);
            Assert.Equal("Microphone in use by:\nTeams", entry.Tooltip(0));
            Assert.Equal("Restart required", entry.Tooltip(2));

            source.Tell(IndicatorState.Nothing);
            Assert.True(entry.Refresh(gfx));
            Assert.Equal(0, entry.Measure(Style()).Count);
        });
    }

    /// <summary>
    /// In edit mode the icons are the real states, nothing made up to arrange: do not disturb shows
    /// only while it is on. Edit mode's dim, which Windows' full-screen rule turns it on for, does
    /// not count (the person, 2026-10-08: "in edit mode the status icons widget always shows the do
    /// not disturb icon even when do not disturb is off"); the person's own does.
    /// </summary>
    [UiFact]
    public void InEditModeDoNotDisturbShowsOnlyWhileItIsOn()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var windows = new FakeQuiet();
            DoNotDisturb.Made = () => windows;
            try
            {
                var source = new SampleIndicators();
                using var island = Make(gfx, source);
                IIsland entry = island;
                // The first look follows Windows' do not disturb.
                _ = entry.Measure(Style());
                windows.Answer(new DndState(On: false));
                Assert.Empty(island.Shown);

                // Edit mode: its dim comes, and Windows' rule takes it for an app in full screen.
                DoNotDisturb.Shared.Dimmed(true);
                windows.Answer(new DndState(On: true, Automatic: true));
                _ = entry.Refresh(gfx);
                Assert.DoesNotContain(island.Shown, icon => icon.Kind == IndicatorKind.DoNotDisturb);
                Assert.Equal(0, entry.Measure(Style()).Count);

                // The person turns it on: shown, in edit mode as out of it.
                windows.Answer(new DndState(On: true));
                _ = entry.Refresh(gfx);
                Assert.Contains(island.Shown, icon => icon.Kind == IndicatorKind.DoNotDisturb);

                // And off again: gone.
                windows.Answer(new DndState(On: false));
                _ = entry.Refresh(gfx);
                Assert.Empty(island.Shown);
                DoNotDisturb.Shared.Dimmed(false);
            }
            finally
            {
                DoNotDisturb.Made = null;
            }
        });
    }

    /// <summary>A click opens the page Windows opens; a menu row opens its page or hands its act to the source.</summary>
    [UiFact]
    public void AClickOpensWindowsPageAndARowActs()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var source = new SampleIndicators(new IndicatorState { Location = ["Maps"], AirplaneMode = true });
            var opened = new List<string>();
            using var island = Make(gfx, source, opened);
            IIsland entry = island;
            Assert.Equal(2, entry.Measure(Style()).Count);

            Assert.Equal(default, entry.Click(0));
            Assert.Equal(["ms-settings:privacy-location"], opened);

            var rows = island.MenuRows(1);
            Assert.Equal(["Turn airplane mode off", "Airplane mode settings"], rows.Select(row => row.Text));
            Assert.Equal(IndicatorsIsland.MenuItem, rows[0].Id);
            island.MenuChosen(1, rows[0].Id);
            Assert.Equal([IndicatorAct.TurnOffAirplaneMode], source.Acts);
            island.MenuChosen(1, rows[1].Id);
            Assert.Equal(["ms-settings:privacy-location", "ms-settings:network-airplanemode"], opened);
            // A row that is none of the cell's does nothing.
            island.MenuChosen(1, IndicatorsIsland.MenuItem + 7);
            island.MenuChosen(9, IndicatorsIsland.MenuItem);
            Assert.Single(source.Acts);
            Assert.Empty(island.MenuRows(5));
        });
    }

    /// <summary>
    /// The status icons' glyph is the network's, the sound's and a battery-less PC's power glyph's
    /// one size (Fit.SystemIcon): Windows' tray's 16 DIP on the 48-DIP bar, 24 px at 150 %; a status
    /// icon keeps its cell's square, where the network face is its glyph and its button's ends (§275).
    /// </summary>
    [UiFact]
    public void TheGlyphIsTheTraysSizeAndTheNetworkIconsToo()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var scale in new[] { 1f, 1.5f })
            {
                var style = Style(scale);
                // The status icons stand at the widgets' own glyph size, a little over half the card (docs 56),
                // which at the 48-DIP bar is 20 DIPs where Windows' tray draws 16.
                Assert.Equal(new Fit(style).Glyph(), new Fit(style).SystemIcon);
                Assert.True(new Fit(style).SystemIcon >= 16 * scale);
                using var island = Make(gfx, new SampleIndicators(new IndicatorState { Microphone = ["Teams"] }));
                IIsland entry = island;
                Assert.Equal(1, entry.Measure(style).Count);
                FolderIsland.StatusStand = new SystemState("", "", "");
                var sound = Karpo.Core.Presets.Folder(Karpo.Core.Presets.Find(Karpo.Core.Presets.SoundId)!, new Karpo.Core.PresetMachine(HasBattery: false, FixedDrives: []));
                using var network = new FolderIsland(Config.IslandKey(null, sound.Id), sound, gfx, new Config());
                FolderIsland.StatusStand = null;
                Assert.Equal((style.Cell, style.Cell), island.CellSize(0, style));
                Assert.Equal(Karpo.Tests.Folders.FaceWidthTests.GlyphButton(style), network.CellSize(0, style).Width, 0.5f);
            }
        });
    }

    /// <summary>
    /// Drawn offscreen: every icon a glyph inside its own cell, by its ink in the middle of it and
    /// no bigger than the tray's glyph, at 100 and 150 %, dark and light, and the sheet written for a
    /// look.
    /// </summary>
    [UiFact]
    public void EveryIconIsDrawnInItsCell()
    {
        var wrong = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var (scale, dark) in new[] { (1f, true), (1f, false), (1.5f, true), (1.5f, false) })
            {
                var look = Karpo.Tests.Folders.TestLooks.LookFor(dark);
                var style = Style(scale, look);
                using var island = Make(gfx, new SampleIndicators(Everything));
                IIsland entry = island;
                var cells = entry.Measure(style);
                var (width, height) = island.CellSize(0, style);
                Offscreen.Bitmap(width * cells.Count, height, dc =>
                {
                    var background = dark ? new DXGI_RGBA(0.13f, 0.13f, 0.13f, 1) : new DXGI_RGBA(0.95f, 0.95f, 0.95f, 1);
                    dc->Clear(&background);
                    for (var index = 0; index < cells.Count; index++)
                    {
                        Offscreen.Shifted(dc, index * width, 0, () => island.PaintCell(dc, index, style, width, height));
                    }
                }, bitmap =>
                {
                    for (var index = 0; index < cells.Count; index++)
                    {
                        // Each icon's ink within its own cell: drawn, and not past it.
                        var one = new List<(float, float, float, float)>();
                        Offscreen.Bitmap(width, height, dc => island.PaintCell(dc, index, style, width, height), cell =>
                            one.Add(Offscreen.InkOf(cell, (uint)MathF.Ceiling(width), (uint)MathF.Ceiling(height))));
                        var (left, top, right, bottom) = one[0];
                        if (right <= left || left < 1 || top < 1 || right > width - 1 || bottom > height - 1)
                        {
                            wrong.Add($"{island.Shown[index].Kind} at {scale * 100:0} % {(dark ? "dark" : "light")}: ink {left},{top} to {right},{bottom} in {width}x{height}");
                        }
                        // By its ink in the middle of its square, as the network, sound and battery
                        // icons are (Gfx.InkOrigin): a pixel either way at most, for the rounding.
                        var (across, down) = ((left + right) / 2f - (width / 2f), (top + bottom) / 2f - (height / 2f));
                        if (MathF.Abs(across) > 1f || MathF.Abs(down) > 1f)
                        {
                            wrong.Add($"{island.Shown[index].Kind} at {scale * 100:0} % {(dark ? "dark" : "light")}: ink centred {across:0.#},{down:0.#} px off its square's middle");
                        }
                        // No bigger than the status icons' glyph (Fit.SystemIcon). The
                        // microphone and location together are one glyph a quarter wider than the
                        // rest (F47F's advance is 1.25 em in Segoe Fluent Icons): SystemTray.dll
                        // draws it at the same size as the others, so it is that much wider, as
                        // Windows' is, and no taller.
                        var glyphPx = new Fit(style).SystemIcon;
                        var wide = island.Shown[index].Kind == IndicatorKind.MicrophoneAndLocation ? 1.25f : 1f;
                        if (right - left > (glyphPx * wide) + 1 || bottom - top > glyphPx + 1)
                        {
                            wrong.Add($"{island.Shown[index].Kind} at {scale * 100:0} % {(dark ? "dark" : "light")}: ink {right - left}x{bottom - top}, past the status icons' {glyphPx} px");
                        }
                    }
                    Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"indicators-{scale * 100:0}-{(dark ? "dark" : "light")}.png"));
                });
            }
        });
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }
}
