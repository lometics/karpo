// keys: the keyboard's ring where the keys are (§470), as the compositor draws it, at 100 and 150 %, dark and light, in
// KARPO_PNG_DIR:
//
//   keys-folder-<dark|light>-<100|150>.png    a folder of lines, the ring round a button of the media line's
//   keys-flyout-<dark|light>-<100|150>.png    a widget's flyout (the weather's now, hours and days): the hours' own
//                                             ring on the hour their keys are on, and none round the line (§471)
//   keys-own-ring-<…>.png                     the month's own ring on the day its keys moved to, the folder's none
//   keys-month-arrow-<…>.png                  the folder's ring round the month's "next" arrow, a stop of its own
//   keys-join-<…>.png                         the day view's Join, a stop (the sweep's day of meetings)
//   keys-clipboard-pin-<…>.png                a clipboard row's pin, its row lit (the sweep's three copies)
//   keys-menu-<dark|light>-<100|150>.png      a menu, its second row the keys'
//
// Needs a desktop: KARPO_UI_TESTS=1. Made on a hidden desktop, on the gutter sheets' stand-ins.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using Karpo.Tests.Perf;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Folders;

[Collection(SamplerState.Collection)]
public sealed class KeyRingPictures
{
    [UiFact]
    public void TheRingWhereTheKeysArePictures()
    {
        ListGutterTests.StoodIn<(Gfx Gfx, HINSTANCE Instance)>(
            SavedWeather.Fixed,
            body => WindowShots.OnHiddenDesktop("keys-ring", (gfx, instance) => body((gfx, instance))),
            context =>
            {
                var (gfx, instance) = context;
                foreach (var dark in new[] { true, false })
                {
                    foreach (var scale in new[] { 1f, 1.5f })
                    {
                        var look = ListFormPictures.LookFor(dark) with { Animations = false };
                        var suffix = $"{(dark ? "dark" : "light")}-{scale * 100:0}";
                        using (var flyout = new FolderFlyout(gfx, instance, 0))
                        {
                            Folder(gfx, flyout, look, scale, [ListKind.Media, ListKind.Timer, ListKind.Rss], [VK.VK_RIGHT, VK.VK_RIGHT], $"keys-folder-{suffix}.png");
                            Folder(gfx, flyout, look, scale, [ListKind.WeatherNow, ListKind.WeatherHours, ListKind.WeatherDays], [VK.VK_DOWN, VK.VK_RIGHT, VK.VK_RIGHT], $"keys-flyout-{suffix}.png");
                            // §471: the month's own ring on its day alone; its arrow the folder's stop; a day's Join; a clipboard row's pin.
                            Folder(gfx, flyout, look, scale, [ListKind.Month, ListKind.Clock], [VK.VK_DOWN, VK.VK_RIGHT], $"keys-own-ring-{suffix}.png");
                            Folder(gfx, flyout, look, scale, [ListKind.Month, ListKind.Clock], [VK.VK_RIGHT], $"keys-month-arrow-{suffix}.png");
                            Walked(gfx, flyout, look, scale, ListKind.DayView, (_, target) => target >= CalendarViewTile.JoinTarget, $"keys-join-{suffix}.png");
                            Walked(gfx, flyout, look, scale, ListKind.Clipboard, (line, target) => ((ClipboardLine)line).TargetOf(target).Spot == ClipboardLine.Spot.Pin, $"keys-clipboard-pin-{suffix}.png");
                        }
                        using var menu = new Flyout(gfx, instance, 0);
                        List<FlyoutItem> rows = [new FlyoutItem(1, "Open"), new FlyoutItem(2, "Refresh"), new FlyoutItem(3, "Settings")];
                        menu.Show(gfx, look, scale, rows, FlyoutLayout.List, new POINT { x = 1200, y = 1400 }, new RECT { right = 2400, bottom = 1400 }, Edge.Bottom, null);
                        _ = menu.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_DOWN, 0);
                        _ = menu.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_DOWN, 0);
                        WindowShots.Settle(300);
                        Assert.True(WindowShots.Save(menu.Hwnd, $"keys-menu-{suffix}.png"), "the menu came back blank");
                        menu.Hide();
                    }
                }
            });
    }

    /// <summary>
    /// A folder of <paramref name="kind"/> on the sweep's stand-ins (LineClickTests.Folder) opened by the keys, the keys
    /// put on the first stop <paramref name="wanted"/> says (the sweep walks them there), saved as <paramref name="file"/>.
    /// </summary>
    private static void Walked(Gfx gfx, FolderFlyout flyout, Look look, float scale, ListKind kind, Func<CellIsland, int, bool> wanted, string file)
    {
        var folder = LineClickTests.Folder(gfx, kind);
        try
        {
            flyout.Show(gfx, look, scale, new POINT { x = 1200, y = 1400 }, new RECT { right = 2400, bottom = 1400 }, Edge.Bottom, folder, FlyoutAlign.Center);
            flyout.TakeKeys();
            var line = (CellIsland)folder.Tiles[0].Island;
            var stop = Assert.Single(flyout.StopsOfLine(0).Where(stop => wanted(line, stop.Target)).Take(1));
            flyout.KeysOnStop(0, stop.Target);
            Assert.True(flyout.FocusRingShows, "no ring");
            WindowShots.Settle(300);
            Assert.True(WindowShots.Save(flyout.Hwnd, file), $"{file} came back blank");
        }
        finally
        {
            flyout.Hide();
            folder.Dispose();
        }
    }

    /// <summary>A folder of <paramref name="kinds"/> opened by the keys, <paramref name="keys"/> pressed, saved as <paramref name="file"/>.</summary>
    private static void Folder(Gfx gfx, FolderFlyout flyout, Look look, float scale, ListKind[] kinds, int[] keys, string file)
    {
        var folder = ListFormPictures.Folder(gfx, ListGutterTests.Before, [.. kinds.Select(ListGutterTests.Made)]);
        try
        {
            flyout.Show(gfx, look, scale, new POINT { x = 1200, y = 1400 }, new RECT { right = 2400, bottom = 1400 }, Edge.Bottom, folder, FlyoutAlign.Center);
            flyout.TakeKeys();
            foreach (var key in keys)
            {
                _ = flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)key, 0);
            }
            Assert.True(flyout.Lit.Keyboard, "no ring");
            WindowShots.Settle(300);
            Assert.True(WindowShots.Save(flyout.Hwnd, file), $"{file} came back blank");
        }
        finally
        {
            flyout.Hide();
            folder.Dispose();
        }
    }
}
