// A column at each of the five widths (2026-10-18: two wider presets, no width of one's own): a
// folder of one list of a month, the day's events, the hours' weather, the media and a graph, at
// narrow, medium, wide, extra wide and widest; the width menu open, extra wide ticked; and a folder
// of a narrow, a widest and a medium list on a 1920 px display, each its preset, the last running
// past the screen's edge and the arranged folder saying so (§320). Dark, 150 %.
// To KARPO_PNG_DIR as folder-column-<width>.png, folder-column-menu.png, folder-column-menu-offscreen.png
// (§328: the widths that would run the folder off the screen marked) and folder-column-offscreen.png.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Color = Windows.UI.Color;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

public sealed unsafe class ColumnWidthPictures
{
    private const string Owner = "layout:default";
    private const float Scale = 1.5f;

    private static readonly Look Dark = new(
        Glass: false,
        Dark: true,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    private static readonly ListKind[] Lines = [ListKind.Month, ListKind.Events, ListKind.WeatherHours, ListKind.Media, ListKind.Cpu];

    /// <summary>One list at each preset, at rest, on a display with room for the widest.</summary>
    [UiFact]
    public void EachWidthSheets()
    {
        WindowShots.OnHiddenDesktop("folder-column-each", (gfx, instance) =>
        {
            var config = new Config();
            var work = new RECT { left = 0, top = 0, right = 3840, bottom = 2100 };
            foreach (var width in FolderListWidths.All)
            {
                var island = BarKind.FolderList.New();
                island.Id = $"each{width}";
                foreach (var kind in Lines)
                {
                    island.Folder!.Put(kind.New(), 0);
                }
                _ = Karpo.Core.Folders.SetWidth(island.Folder!, 0, width);
                using var folder = new FolderIsland($"{Owner}/each{width}", island, gfx, config);
                using var flyout = new FolderFlyout(gfx, instance, 0);
                flyout.Show(gfx, Dark, Scale, new POINT { x = 1920, y = 2100 }, work, Edge.Bottom, folder);
                Assert.True(flyout.IsOpen);
                Assert.Equal(FolderFlyout.ListWidth(Scale, width), flyout.Plan!.Lists[0].Width, 1);
                WindowShots.Settle(250);
                Assert.True(WindowShots.Save(flyout.Hwnd, $"folder-column-{width.ToString().ToLowerInvariant()}.png"), $"{width} came back blank");
                flyout.Hide();
            }
        });
    }

    /// <summary>A list's width menu, as the dock opens it beside the list's width button: the five, extra wide ticked.</summary>
    [UiFact]
    public void WidthMenuSheet()
    {
        WindowShots.OnHiddenDesktop("folder-column-menu", (gfx, instance) =>
        {
            using var menu = new Flyout(gfx, instance, 0);
            var work = new RECT { right = 2880, bottom = 1560 };
            menu.Show(gfx, Dark, Scale, TheDock.FolderWidthRows(FolderListWidth.ExtraWide), FlyoutLayout.List, new POINT { x = 1440, y = 1560 }, work, Edge.Bottom, null);
            WindowShots.Settle();
            Assert.True(WindowShots.Save(menu.Hwnd, "folder-column-menu.png"), "the menu came back blank");
            menu.Hide();
            // §328: wide ticked, extra wide and widest marked as running the folder off the screen.
            menu.Show(gfx, Dark, Scale, TheDock.FolderWidthRows(FolderListWidth.Wide, [FolderListWidth.ExtraWide, FolderListWidth.Widest]), FlyoutLayout.List, new POINT { x = 1440, y = 1560 }, work, Edge.Bottom, null);
            WindowShots.Settle();
            Assert.True(WindowShots.Save(menu.Hwnd, "folder-column-menu-offscreen.png"), "the marked menu came back blank");
            menu.Hide();
        });
    }

    /// <summary>Narrow, widest and medium on a 1920 px display, arranged: each its preset, none giving way, the medium one past the screen's edge and said to be (§320).</summary>
    [UiFact]
    public void OffScreenSheet()
    {
        WindowShots.OnHiddenDesktop("folder-column-offscreen", (gfx, instance) =>
        {
            var config = new Config();
            var work = new RECT { left = 0, top = 0, right = 1920, bottom = 1400 };
            var island = BarKind.FolderList.New();
            island.Id = "fitted";
            island.Folder!.Lists = [];
            ListKind[][] lists =
            [
                [ListKind.AirplaneMode, ListKind.NightLight, ListKind.Battery],
                [ListKind.Month, ListKind.Events, ListKind.WeatherHours],
                [ListKind.Clock, ListKind.Cpu],
            ];
            for (var list = 0; list < lists.Length; list++)
            {
                foreach (var kind in lists[list])
                {
                    island.Folder.Put(kind.New(), list);
                }
            }
            island.Folder.Widths = [FolderListWidth.Narrow, FolderListWidth.Widest, FolderListWidth.Medium];
            using var folder = new FolderIsland($"{Owner}/fitted", island, gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, Scale, new POINT { x = 960, y = 1400 }, work, Edge.Bottom, folder, FlyoutAlign.Center, editing: true);
            Assert.True(flyout.IsOpen);
            Assert.Equal(FolderFlyout.ListWidth(Scale, FolderListWidth.Widest), flyout.Plan!.Lists[1].Width);
            Assert.Equal([2], flyout.Plan.OffScreen);
            WindowShots.Settle(250);
            Assert.True(flyout.WarningShown.right > flyout.WarningShown.left, "no off-screen warning");
            Assert.True(WindowShots.SaveTogether([(flyout.Hwnd, work)], "folder-column-offscreen.png", (0x30, 0x30, 0x30), over: [flyout.Hwnd]), "the folder came back blank");
            flyout.Hide();
        });
    }
}
