// The bar a new config gets, with the input indicator Windows 11 keeps beside its system icons
// (backlog 30, on every new bar since 2026-10-10): drawn by the dock's real scene with real widgets
// on a desktop of its own and captured as the compositor draws it (WindowShots), to KARPO_PNG_DIR as
// default-bar-<look>-<scale>.png and, cut to the system icons, default-bar-system-<look>-<scale>.png.
// And the bar as Windows 11 25H2 shows its own taskbar (2026-10-05): out of the box with the pins
// Windows gives a new taskbar and on a PC without a battery, as
// default-bar-{windows,no-battery}-*.png, in the look a new install's bar wears, islands
// (2026-10-08: bars are islands, docks float), with no style set, so they show whatever the default
// is; and the same entries as a Dock layout's, a dock a section (§511), as
// default-bar-dock-<look>-<scale>.png.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1. The config is isolated, so a new bar is
// made for Windows' out-of-the-box taskbar whatever the PC has.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;
using Vector2 = System.Numerics.Vector2;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>A desktop PC: no pen, no touch screen, so no input buttons beside the tray.</summary>
    private static readonly InputHardware NoPenNoTouch = new(Pen: false, Touch: false, Slate: false);

    /// <summary>
    /// A bar in all three sections, as Windows' own centred one and a new bar until 2026-10-14: the
    /// weather preset at the left, a new bar's Start to the open apps in the middle, its system
    /// corner at the right. A new bar now has its buttons at the left and the middle empty.
    /// </summary>
    private static List<BarItem> CentredBar()
    {
        var bar = Sections.DefaultLayout(pins: [], inputs: NoPenNoTouch);
        foreach (var island in bar.Where(island => island.Section == BarSection.Start))
        {
            island.Section = BarSection.Middle;
        }
        var weather = Presets.Folder(Presets.Find(Presets.WeatherId)!, new PresetMachine(HasBattery: true, FixedDrives: []));
        weather.Section = BarSection.Start;
        bar.Insert(0, weather);
        Sections.Order(bar);
        return bar;
    }

    [UiFact]
    public void DefaultBarSheets()
    {
        WindowShots.OnHiddenDesktop("default-bar", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var name = $"{(dark ? "dark" : "light")}-{dpi * 100 / 96}";
                    var look = AddListPictures.LookFor(dark) with { Animations = false };
                    var config = new Config { Height = 48 };
                    var width = dpi == 96 ? 1600 : 2400;
                    var area = new RECT { left = 0, top = 0, right = width, bottom = (int)(dpi * 10) };
                    var bar = Sections.DefaultLayout(pins: [], inputs: NoPenNoTouch, windows: WindowsTaskbar.OutOfTheBox);
                    var (specs, islands) = Made(gfx, config, bar);
                    try
                    {
                        var content = new Content { Islands = islands };
                        var layout = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, content);
                        var tray = layout.Parts.Single(part => part.Kind == BarKind.Tray);
                        // Where Windows keeps it: after the tray, left of quick settings (the network, sound and battery).
                        var keyboard = layout.Parts[bar.FindIndex(island => island.Folder?.Face.Kind == FolderFaces.KeyboardId)];
                        var wifi = layout.Parts[layout.Parts.IndexOf(keyboard) + 1];
                        Assert.Equal(BarKind.FolderList, wifi.Kind);
                        Assert.True(tray.Right <= keyboard.Left + 0.5f && keyboard.Right <= wifi.Left + 0.5f);
                        Assert.True(keyboard.Width > 0f);
                        // The system icons and a little of what stands before them, across the bar's panel.
                        var panel = layout.PanelRect;
                        var from = (int)MathF.Max(panel.X, panel.X + wifi.Left - (260 * dpi / 96f));
                        var corner = new RECT { left = from, top = (int)panel.Y, right = (int)panel.Right, bottom = (int)panel.Bottom };
                        ShootWith(gfx, layout, content, look, $"default-bar-{name}.png", default);
                        ShootWith(gfx, layout, content, look, $"default-bar-system-{name}.png", corner);
                    }
                    finally
                    {
                        foreach (var island in islands.Values)
                        {
                            island.Dispose();
                        }
                    }
                }
            }
        });
    }

    /// <summary>
    /// The bar a new install gets, as Windows 11 25H2 shows its taskbar (2026-10-05): out of the box
    /// with Windows' own pins, and on a PC without a battery. To KARPO_PNG_DIR as
    /// default-bar-{windows,no-battery}-&lt;look&gt;-&lt;scale&gt;.png, for comparing with Windows'
    /// own taskbar by eye.
    /// </summary>
    [UiFact]
    public void DefaultBarAsWindowsSheets()
    {
        var pins = WindowsTaskbar.DefaultPins(PinTargets.System);
        (string Name, WindowsTaskbar Windows)[] variants =
        [
            ("windows", WindowsTaskbar.OutOfTheBox),
            ("no-battery", WindowsTaskbar.OutOfTheBox with { Battery = false }),
        ];
        WindowShots.OnHiddenDesktop("default-bar-windows", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var name = $"{(dark ? "dark" : "light")}-{dpi * 100 / 96}";
                    var look = AddListPictures.LookFor(dark) with { Animations = false };
                    var config = new Config { Height = 48 };
                    var width = dpi == 96 ? 1600 : 2400;
                    var area = new RECT { left = 0, top = 0, right = width, bottom = (int)(dpi * 10) };
                    foreach (var (variant, windows) in variants)
                    {
                        var bar = Sections.DefaultLayout(pins, NoPenNoTouch, windows);
                        var (specs, islands) = Made(gfx, config, bar);
                        try
                        {
                            var content = new Content { Islands = islands };
                            var layout = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, content);
                            // A new install's bar wears islands: bars are islands, docks float, and
                            // the connected look is gone (2026-10-08).
                            Assert.Equal(BarStyle.Islands, layout.Style);
                            Assert.False(layout.Connected, "a bar that chose no look wears islands, not the connected look");
                            Assert.Equal(BarSection.Start, bar.Single(island => island.Kind == BarKind.Start).Section);
                            // Start first at the left, nothing in the middle, no Widgets button nor the weather (2026-10-14).
                            Assert.Equal(BarKind.Start, layout.Parts.First(part => part.Width > 0f).Kind);
                            Assert.DoesNotContain(bar, island => island.Section == BarSection.Middle);
                            Assert.DoesNotContain(bar, island => island.Kind == BarKind.WidgetsBoard || island.Folder?.FaceOf(FolderFaces.WeatherId) is not null);
                            Assert.Equal(windows.Battery, bar.Any(island => island.Folder?.Faces.Any(face => face.Info == FolderFaces.Battery) == true));
                            Assert.Equal(BarKind.ShowDesktop, layout.Parts[^1].Kind);
                            ShootWith(gfx, layout, content, look, $"default-bar-{variant}-{name}.png", default);
                        }
                        finally
                        {
                            foreach (var island in islands.Values)
                            {
                                island.Dispose();
                            }
                        }
                    }
                }
            }
        });
    }

    /// <summary>
    /// A new bar's entries in a Dock layout: Windows' buttons the start's dock,
    /// the end's the tray, the system icons, quick settings, the clock button and
    /// show desktop (§81). To KARPO_PNG_DIR as default-bar-dock-&lt;look&gt;-&lt;scale&gt;.png.
    /// </summary>
    [UiFact]
    public void DefaultBarDockSheets()
    {
        var pins = WindowsTaskbar.DefaultPins(PinTargets.System);
        WindowShots.OnHiddenDesktop("default-bar-dock", (gfx, instance) =>
        {
            foreach (var dpi in new uint[] { 144, 96 })
            {
                foreach (var dark in new[] { true, false })
                {
                    var look = AddListPictures.LookFor(dark) with { Animations = false };
                    var islands = Sections.DefaultLayout(pins, NoPenNoTouch, WindowsTaskbar.OutOfTheBox);
                    var dock = new Picture(new TaskbarLayout { Id = "dock", Name = "Dock", Type = LayoutType.Dock, Islands = islands }, Edge.Bottom);
                    EditedDisplay(gfx, instance, dpi, new Config { Height = 48 }, look, dock, $"default-bar-dock-{(dark ? "dark" : "light")}-{dpi * 100 / 96}.png", editing: false, dips: new Vector2(1600f, 400f));
                }
            }
        });
    }

    /// <summary>The bar's parts as the dock makes them, and a real widget for each that has one.</summary>
    private static (List<PartSpec> Specs, IslandCollection Islands) Made(Gfx gfx, Config config, List<BarItem> bar)
    {
        var specs = new List<PartSpec>();
        var islands = new IslandCollection();
        var radios = new SystemState(
            char.ConvertFromUtf32(0xE873),
            char.ConvertFromUtf32(0xE994),
            NetworkBackGlyph: char.ConvertFromUtf32(0xE701),
            BluetoothGlyph: char.ConvertFromUtf32(0xE702),
            BluetoothOn: true);
        foreach (var item in bar)
        {
            var key = Config.IslandKey(null, item.Id);
            specs.Add(PartSpec.For(key, item, NoPenNoTouch));
            // The network's and the sound's faces on a made-up connection and level, never this PC's.
            FolderIsland.StatusStand = radios;
            IIsland? made;
            try
            {
                made = Karpo.Dock.Islands.Create(key, item, gfx, config);
            }
            finally
            {
                FolderIsland.StatusStand = null;
            }
            if (made is not { } island)
            {
                continue;
            }
            _ = island.Refresh(gfx);
            islands[key] = island;
        }
        return (specs, islands);
    }

    /// <summary>
    /// The scene of <paramref name="layout"/> with <paramref name="content"/>, shown on the hidden desktop, <paramref name="part"/>
    /// of it saved to <paramref name="file"/>; the part keyed <paramref name="open"/> lit as one whose flyout is open (Scene.Open).
    /// </summary>
    private static void ShootWith(Gfx gfx, Layout layout, Content content, Look look, string file, RECT part, string? open = null)
    {
        var window = MakeWindow();
        DesktopWindowTarget? target = null;
        Scene? scene = null;
        try
        {
            _ = SetWindowPos(window, HWND.NULL, 0, 0, (int)layout.Width, (int)layout.Height, SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
            target = Target(gfx, window);
            scene = Scenes.Build(gfx, target, layout, content, look, new Icons(), shown: true);
            if (open is not null)
            {
                scene.Open(open);
            }
            _ = ShowWindow(window, SW.SW_SHOWNOACTIVATE);
            WindowShots.Settle(300);
            if (part.right <= part.left)
            {
                var panel = layout.PanelRect;
                part = new RECT { left = (int)panel.X, top = (int)panel.Y, right = (int)panel.Right, bottom = (int)panel.Bottom };
            }
            Assert.True(WindowShots.Save(window, file, part), $"{file} came back blank");
        }
        finally
        {
            scene?.Dispose();
            _ = DestroyWindow(window);
        }
    }
}
