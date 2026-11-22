// tray: the bar a new config gets, with three apps open, four tray icons and the chevron, and three
// status icons showing (the microphone in use, a metered network, a restart waiting), drawn by the
// dock's real scene with real widgets over a desktop of the tests' own and captured as the compositor
// draws it (WindowShots), to KARPO_PNG_DIR:
//   tray-bar-<style>-<look>-<scale>.png  the right end of the bar in each look: the tray's icons the
//                                        status icons' size (16 DIP on 48), in square cells side by
//                                        side, and the status icons on the same background as the network's;
//   tray-bar-<style>-hover-tray-<look>-<scale>.png  the second tray icon under the pointer;
//   tray-bar-small-<look>-<scale>.png    the same in the islands look, its icons handed over as most
//                                        apps hand them: their small icon for the DPI (16 DIP), filtered
//                                        up to the bar's size (Tray.Capture), not copied up;
//   tray-overflow-<look>-<scale>.png     the overflow with seven hidden icons, as the dock opens it:
//                                        the bar's squares, five to a row, the panel as large as they are;
//   tray-overflow-one-<look>-<scale>.png the overflow with a single icon, no larger than it;
// dark and light, at 100 % and 150 %. Every part is measured by the real Layout.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using static TerraFX.Interop.Windows.S;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>Three status icons: the microphone in use, a metered network and a restart waiting.</summary>
    private static IndicatorState ThreeStatusIcons => new()
    {
        Microphone = ["Teams"],
        Metered = true,
        Update = UpdateState.Due,
    };

    /// <summary>shell32's icon <paramref name="which"/> as an app hands it over at <paramref name="small"/> px, taken at <paramref name="size"/> (Tray.Render).</summary>
    private static Pixels? SmallShellIcon(int which, int small, int size)
    {
        var icon = HICON.NULL;
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shell32.dll");
        fixed (char* name = path)
        {
            if (SHDefExtractIconW(name, TrayShellIcons[which % TrayShellIcons.Length], 0, &icon, null, (uint)small).Value != S_OK
                || icon == HICON.NULL)
            {
                return null;
            }
        }
        try
        {
            return Tray.Render(icon, size);
        }
        finally
        {
            _ = DestroyIcon(icon);
        }
    }

    [UiFact]
    public void TraySizeSheets()
    {
        WindowShots.OnHiddenDesktop("tray-size", (gfx, instance) =>
        {
            // Per thread: the hidden desktop's.
            IndicatorSources.Made = () => new SampleIndicators(ThreeStatusIcons);
            try
            {
                foreach (var dark in new[] { true, false })
                {
                    foreach (var dpi in new uint[] { 96, 144 })
                    {
                        var scale = dpi / 96f;
                        var name = $"{(dark ? "dark" : "light")}-{dpi * 100 / 96}";
                        var look = AddListPictures.LookFor(dark) with { Animations = false };
                        foreach (var style in new[] { BarStyle.Islands, BarStyle.Floating })
                        {
                            var config = new Config { Height = 48, Style = style };
                            var width = dpi == 96 ? 1600 : 2400;
                            var area = new RECT { left = 0, top = 0, right = width, bottom = (int)(dpi * 10) };
                            var px = (int)MathF.Ceiling(Layout.TrayIconPx(dpi, config));
                            var icons = DraggedTrayIcons(4, px);
                            var (specs, islands) = Made(gfx, config, Sections.DefaultLayout(pins: [], inputs: NoPenNoTouch));
                            try
                            {
                                var content = new Content
                                {
                                    Islands = islands,
                                    Tiles = [.. Enumerable.Range(0, 3).Select(BusyApp)],
                                    Tray = icons,
                                    TrayOverflow = true,
                                };
                                var layout = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, content);
                                var tray = layout.Parts.Single(part => part.Kind == BarKind.Tray);
                                var status = layout.Parts.Single(part => part.Kind == BarKind.Indicators);
                                Assert.Equal(3, status.Count);
                                var word = style.ToString().ToLowerInvariant();
                                var from = (int)tray.Left - (int)(48 * scale);
                                OverADesktop(gfx, layout, look, content, null, $"tray-bar-{word}-{name}.png", from);
                                // Under the pointer: a status icon lights as the network beside it does.
                                var statusPart = layout.Parts.FindIndex(part => part.Kind == BarKind.Indicators);
                                // The network: the Wi-Fi preset's folder, right after the input indicator.
                                var wifiPart = layout.Parts.FindIndex(part => part.Kind == BarKind.NightLight) + 1;
                                OverADesktop(gfx, layout, look, content, null, $"tray-bar-{word}-hover-status-{name}.png", from, scene => scene.Highlight(Hit.Cell(statusPart, 1), false));
                                OverADesktop(gfx, layout, look, content, null, $"tray-bar-{word}-hover-wifi-{name}.png", from, scene => scene.Highlight(Hit.Cell(wifiPart, 0), false));
                                // Under the pointer: a tray icon lights its own square, its neighbours touching it.
                                OverADesktop(gfx, layout, look, content, null, $"tray-bar-{word}-hover-tray-{name}.png", from, scene => scene.Highlight(Hit.Tray(1), false));

                                if (style == BarStyle.Islands)
                                {
                                    // As most apps hand their icons over: the small icon for the DPI.
                                    var small = (int)MathF.Round(16 * scale);
                                    var handed = new Content
                                    {
                                        Islands = islands,
                                        Tiles = content.Tiles,
                                        Tray = [.. icons.Select((icon, index) => icon with { Icon = SmallShellIcon(index, small, px) })],
                                        TrayOverflow = true,
                                    };
                                    var smallLayout = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, handed);
                                    OverADesktop(gfx, smallLayout, look, handed, null, $"tray-bar-small-{name}.png", from);
                                }

                                // The tray's icons are the status icons' size (Windows' 16 DIP here, docs\widgets.md §57), in cells of the icon and its padding that touch (§290).
                                Assert.Equal(MathF.Round(16 * scale), layout.TrayIconSize);
                                Assert.True(layout.TrayStep < layout.TrayCell);
                                for (var index = 0; index < icons.Count; index++)
                                {
                                    var cell = layout.ItemRect(Hit.Tray(index));
                                    var picture = layout.TrayIconRect(index);
                                    Assert.Equal(layout.TrayStep, cell.Width);
                                    Assert.Equal(layout.TrayCell, cell.Height);
                                    Assert.Equal(layout.TrayIconSize, picture.Width);
                                    Assert.True(picture.X >= cell.X && picture.Right <= cell.Right + 0.01f, $"icon {index} outside its cell at {name}");
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

                        // The overflow, as the dock opens it (Dock.ShowFlyout): seven hidden icons, then one.
                        var bar = Layout.ForTests(dpi, new RECT { left = 0, top = 0, right = 2400, bottom = 1200 }, Edge.Bottom, new Config { Height = 48 },
                            [new(Config.IslandKey(null, "tray"), BarKind.Tray)], Layout.CellPx(dpi, new Config { Height = 48 }), tray: 2, trayOverflow: true);
                        foreach (var (count, file) in new[] { (7, $"tray-overflow-{name}.png"), (1, $"tray-overflow-one-{name}.png") })
                        {
                            using var flyout = new Flyout(gfx, instance, 0)
                            {
                                StripIconSize = bar.TrayIconSize,
                                StripCell = bar.TrayOverflowCell,
                                GridColumns = Layout.TrayOverflowColumns,
                                FitsItsIcons = true,
                                GroupContent = true,
                                ContextClicks = true,
                                DragItems = true,
                                TellsPointer = true,
                            };
                            var hidden = DraggedTrayIcons(count, (int)MathF.Ceiling(bar.TrayIconSize));
                            List<FlyoutItem> items = [.. hidden.Select((icon, index) => new FlyoutItem(2000u + (uint)index, icon.Tip, icon.Icon is { } pixels ? new FlyoutIcon.Picture(pixels) : new FlyoutIcon.Glyph("")))];
                            var work = new RECT { left = 0, top = 0, right = (int)(1920 * scale), bottom = (int)(1032 * scale) };
                            var anchor = new POINT { x = work.right - (int)(120 * scale), y = work.bottom };
                            flyout.Show(gfx, look, scale, items, FlyoutLayout.Grid, anchor, work, Edge.Bottom);
                            Assert.True(flyout.IsOpen);
                            WindowShots.Settle();
                            Assert.True(WindowShots.Save(flyout.Hwnd, file), $"{file} came back blank");

                            // The panel is its icons' squares and the room around them, nothing more.
                            var columns = Math.Min(count, Layout.TrayOverflowColumns);
                            var rows = (count + Layout.TrayOverflowColumns - 1) / Layout.TrayOverflowColumns;
                            Assert.Equal(Flyout.FittedSize(columns, rows, bar.TrayOverflowCell, scale), flyout.PanelSize);
                            // Each icon in the bar's own square, five to a row: the sixth starts the second row.
                            var first = Assert.NotNull(flyout.ItemOnScreen(2000));
                            if (count == 7)
                            {
                                var second = Assert.NotNull(flyout.ItemOnScreen(2001));
                                Assert.InRange((float)(second.left - first.left), bar.TrayOverflowCell - 1, bar.TrayOverflowCell + 1);
                                var fifth = Assert.NotNull(flyout.ItemOnScreen(2004));
                                var sixth = Assert.NotNull(flyout.ItemOnScreen(2005));
                                Assert.Equal(first.top, fifth.top);
                                Assert.InRange((float)(fifth.left - first.left), 4 * bar.TrayOverflowCell - 1, 4 * bar.TrayOverflowCell + 1);
                                Assert.InRange(sixth.left - first.left, -1, 1);
                                Assert.InRange((float)(sixth.top - first.top), bar.TrayOverflowCell - 1, bar.TrayOverflowCell + 1);
                            }
                            flyout.Hide();
                            WindowShots.Settle(50);
                        }
                    }
                }
            }
            finally
            {
                IndicatorSources.Made = null;
            }
        });
    }
}
