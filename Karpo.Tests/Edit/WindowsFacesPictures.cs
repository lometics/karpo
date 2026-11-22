// §474: Windows' own widgets on a bar — the Widgets button (the weather), quick settings' button (the network, the
// volume, the battery) and the clock (the time over the date, the bell) — as the Windows preset has them (§485: the
// Widgets button, which the preset no longer has, added at the start as Windows 11 has it), drawn by the
// dock's real scene with real widgets on a desktop of its own (WindowShots), at 100 % and 150 %, dark and light; and
// quick settings' button lit, as it is while Windows' quick settings is open. Nothing of this PC's is shown: the time,
// the weather, the network, the volume, the charge and the notifications are stand-ins. To KARPO_PNG_DIR as
// windows-widgets-<dark|light>-<scale>.png, windows-widgets-corner-<dark|light>-<scale>.png and
// windows-widgets-lit-<dark|light>-150.png. §490: and each of Windows' own beside the Karpo folder of its faces, in three
// styles (windows-beside-karpo-<style>-<dark|light>-<scale>.png): the same picture but for Windows' content.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Folders;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>
    /// Windows' own widgets each beside the Karpo folder of the same faces — quick settings' button and the Quick
    /// settings folder, the clock and the notification centre's, the Widgets button and the weather's, the sound, the
    /// Wi-Fi, cast and clipboard buttons and their folders — in Follow Windows, Graphite and Paper, dark and light, at
    /// 100 % and 150 %, to KARPO_PNG_DIR as windows-beside-karpo-&lt;style&gt;-&lt;dark|light&gt;-&lt;scale&gt;.png.
    /// </summary>
    [UiFact]
    public void WindowsOwnBesideKarposSheets()
    {
        var machine = new LayoutMachine([], NoPenNoTouch, WindowsTaskbar.OutOfTheBox with { Battery = true });
        var folder = NotificationsPresetTests.Temp();
        try
        {
            WindowShots.OnHiddenDesktop("windows-beside-karpo", (gfx, _) =>
            {
                Clock.Now = () => new DateTime(2026, 11, 2, 9, 41, 0);
                FolderIsland.WeatherStand = SavedWeather.Cache();
                var probe = BatteryIsland.Probe;
                BatteryIsland.Probe = () => new SystemStatus.Battery(76, Plugged: false, Saver: false);
                IndicatorSources.Made = () => new SampleIndicators(new IndicatorState());
                NotificationPartTile.Stand = NotificationsPresetTests.Store(folder,
                [
                    (1001L, 1, 3, "<toast><visual><binding template=\"ToastGeneric\"><text>Design review</text><text>Moved to 14:00.</text></binding></visual></toast>"),
                ], "windows-beside-karpo");
                try
                {
                    foreach (var style in new[] { null, LookPalettes.GraphiteId, LookPalettes.PaperId })
                    {
                        foreach (var dark in new[] { true, false })
                        {
                            foreach (var dpi in new uint[] { 96, 144 })
                            {
                                var config = new Config { Height = 48 };
                                Assert.True(style is null || LookPalettes.Wear(config, LookPalettes.Of(style)!));
                                var width = dpi == 96 ? 1600 : 2400;
                                var area = new RECT { left = 0, top = 0, right = width, bottom = (int)(dpi * 10) };
                                List<BarItem> bar = [];
                                void Pair(BarKind windows, string preset)
                                {
                                    var own = windows.New();
                                    own.Section = BarSection.Middle;
                                    bar.Add(own);
                                    var karpo = Presets.Folder(Presets.Find(preset)!, machine.Folders);
                                    karpo.Section = BarSection.Middle;
                                    bar.Add(karpo);
                                }
                                Pair(BarKind.WindowsQuickSettings, Presets.QuickPanelId);
                                Pair(BarKind.WindowsNotifications, Presets.NotificationCenterId);
                                Pair(BarKind.WidgetsBoard, Presets.WeatherId);
                                Pair(BarKind.WindowsSound, Presets.SoundId);
                                Pair(BarKind.WindowsNetworks, Presets.WifiId);
                                Pair(BarKind.WindowsCast, Presets.CastId);
                                Pair(BarKind.WindowsClipboard, Presets.ClipboardId);
                                config.DefaultLayout.Islands = bar;
                                gfx.Typeface = Looks.MenuText(config);
                                var look = (TestLooks.LookFor(dark) with { Animations = false }).Wearing(config);
                                var (specs, islands) = Made(gfx, config, bar);
                                try
                                {
                                    var content = new Content { Islands = islands };
                                    var layout = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, content);
                                    var panel = layout.PanelRect;
                                    var first = layout.Parts.First(part => part.Kind == BarKind.WindowsQuickSettings);
                                    var last = layout.Parts[^1];
                                    var margin = (int)(12 * dpi / 96f);
                                    ShootWith(gfx, layout, content, look, $"windows-beside-karpo-{style ?? "windows"}-{(dark ? "dark" : "light")}-{dpi * 100 / 96}.png",
                                        new RECT { left = (int)(panel.X + first.Left) - margin, top = (int)panel.Y, right = (int)(panel.X + last.Left + last.Width) + margin, bottom = (int)panel.Bottom });
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
                }
                finally
                {
                    NotificationPartTile.Stand = null;
                    FolderIsland.WeatherStand = null;
                    BatteryIsland.Probe = probe;
                    IndicatorSources.Made = null;
                    Clock.Now = Clock.Real;
                }
            });
        }
        finally
        {
            NotificationsPresetTests.Clean(folder);
        }
    }

    [UiFact]
    public void WindowsFacesSheets()
    {
        var machine = new LayoutMachine(WindowsTaskbar.DefaultPins(PinTargets.System), NoPenNoTouch, WindowsTaskbar.OutOfTheBox);
        var folder = NotificationsPresetTests.Temp();
        try
        {
            WindowShots.OnHiddenDesktop("windows-widgets", (gfx, _) =>
            {
                Clock.Now = () => new DateTime(2026, 11, 2, 9, 41, 0);
                FolderIsland.WeatherStand = SavedWeather.Cache();
                var probe = BatteryIsland.Probe;
                BatteryIsland.Probe = () => new SystemStatus.Battery(76, Plugged: false, Saver: false);
                IndicatorSources.Made = () => new SampleIndicators(new IndicatorState());
                NotificationPartTile.Stand = NotificationsPresetTests.Store(folder,
                [
                    (1001L, 1, 3, "<toast><visual><binding template=\"ToastGeneric\"><text>Design review</text><text>Moved to 14:00.</text></binding></visual></toast>"),
                    (1002L, 2, 18, "<toast><visual><binding template=\"ToastGeneric\"><text>Lunch on Friday?</text><text>By the river.</text></binding></visual></toast>"),
                ], "windows-widgets");
                try
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
                            var bar = LayoutPresets.Make(LayoutPresets.Windows, machine, "Windows").Islands;
                            var widgets = BarKind.WidgetsBoard.New();
                            widgets.Section = BarSection.Start;
                            bar.Insert(0, widgets);
                            var (specs, islands) = Made(gfx, config, bar);
                            try
                            {
                                var content = new Content { Islands = islands };
                                var layout = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, content);
                                var quick = layout.Parts.Single(part => part.Kind == BarKind.WindowsQuickSettings);
                                var clock = layout.Parts.Single(part => part.Kind == BarKind.WindowsNotifications);
                                var board = layout.Parts.Single(part => part.Kind == BarKind.WidgetsBoard);
                                // Each as long as its faces, never one glyph's cell.
                                Assert.All(new[] { quick, clock, board }, part => Assert.True(part.Width > layout.Thickness, $"{part.Kind} is {part.Width} px"));
                                ShootWith(gfx, layout, content, look, $"windows-widgets-{name}.png", default);
                                var panel = layout.PanelRect;
                                var from = (int)MathF.Max(panel.X, panel.X + quick.Left - (120 * dpi / 96f));
                                ShootWith(gfx, layout, content, look, $"windows-widgets-corner-{name}.png",
                                    new RECT { left = from, top = (int)panel.Y, right = (int)panel.Right, bottom = (int)panel.Bottom });
                                if (dpi == 144)
                                {
                                    ShootWith(gfx, layout, content, look, $"windows-widgets-lit-{(dark ? "dark" : "light")}-150.png",
                                        new RECT { left = from, top = (int)panel.Y, right = (int)panel.Right, bottom = (int)panel.Bottom }, open: specs[layout.Parts.IndexOf(quick)].Key);
                                }
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
                finally
                {
                    NotificationPartTile.Stand = null;
                    FolderIsland.WeatherStand = null;
                    BatteryIsland.Probe = probe;
                    IndicatorSources.Made = null;
                    Clock.Now = Clock.Real;
                }
            });
        }
        finally
        {
            NotificationsPresetTests.Clean(folder);
        }
    }
}
