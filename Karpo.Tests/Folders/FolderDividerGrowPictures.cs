// A folder's divider that grows (docs\widgets.md §435), the real folder in a window: two columns, the first taller, the
// second's divider set to Grow, so its last lines stand at its foot. Graphite (dark) and Paper (light), at rest, arranged
// with the divider selected, and arranged with the pointer over it. To KARPO_PNG_DIR as
// folder-divider-grow-<theme>-<state>-150.png and folder-divider-grow-<theme>-150.png, a row of the three. What it
// holds as numbers is FolderDividerGrowTests'; this is for looking at. §482: on a bottom and a top bar, one column and
// two, with and without a Fill line, the growing divider's column all the room the folder has, as
// folder-divider-grow-<bottom|top>-<one|two|one-fill|two-fill>-150.png and a row of the four,
// folder-divider-grow-<bottom|top>-150.png.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Folders;

public sealed class FolderDividerGrowPictures
{
    private const float Scale = 1.5f;

    /// <summary>Where the growing divider is among the folder's lines.</summary>
    private const int Grown = 8;

    private static readonly ListKind[] Seven =
        [ListKind.NightLight, ListKind.AirplaneMode, ListKind.EnergySaver, ListKind.Cpu, ListKind.Memory, ListKind.Battery, ListKind.Clock];

    private static readonly ListKind[] Growing = [ListKind.NightLight, ListKind.Divider, ListKind.Battery, ListKind.Clock];

    /// <summary>Two columns: seven switches and readings; night light, a growing divider, the battery and the clock.</summary>
    private static BarItem Folder() => Folder(Seven, Growing);

    /// <summary>A list folder of <paramref name="lists"/>, every divider growing; with <paramref name="fill"/> a calendar set to Fill heads the last column.</summary>
    private static BarItem Folder(params ListKind[][] lists) => Folder(false, lists);

    private static BarItem Folder(bool fill, params ListKind[][] lists)
    {
        var island = BarKind.FolderList.New();
        island.Id = "grow";
        island.Folder!.Lists = [];
        for (var list = 0; list < lists.Length; list++)
        {
            if (fill && list == lists.Length - 1)
            {
                var calendar = ListKind.Calendar.New();
                calendar.Height = LineHeight.For(calendar).As(LineHeightMode.Fill);
                island.Folder.Put(calendar, list);
            }
            foreach (var kind in lists[list])
            {
                var line = kind.New();
                if (kind == ListKind.Divider)
                {
                    line.Divider = new DividerOptions { Style = DividerStyle.Grow };
                }
                island.Folder.Put(line, list);
            }
        }
        return island;
    }

    /// <summary>§482: the growing divider's column all the room the folder has, on a bottom bar and a top bar.</summary>
    [UiFact]
    public void DividerGrowEdgeSheets()
    {
        WindowShots.OnHiddenDesktop("folder-divider-grow-edge", (gfx, instance) =>
        {
            var work = new RECT { left = 0, top = 0, right = 2400, bottom = 1500 };
            var config = LookShorthand.Wearing(palette: "graphite");
            var look = TestLooks.LookFor(true).Wearing(config) with { Animations = false };
            foreach (var edge in new[] { Edge.Bottom, Edge.Top })
            {
                var anchor = new POINT { x = 1200, y = edge == Edge.Top ? 0 : 1500 };
                var pictures = new List<(byte[] Canvas, int Width, int Height)>();
                foreach (var (name, island) in new[]
                {
                    ("one", Folder(Growing)),
                    ("two", Folder(Seven, Growing)),
                    ("one-fill", Folder(true, Growing)),
                    ("two-fill", Folder(true, Seven, Growing)),
                })
                {
                    using var folder = new FolderIsland($"layout:default/{island.Id}", island, gfx, config);
                    using var flyout = new FolderFlyout(gfx, instance, 0);
                    flyout.Show(gfx, look, Scale, anchor, work, edge, folder, FlyoutAlign.Center, false);
                    Assert.True(flyout.IsOpen);
                    WindowShots.Settle(250);
                    byte[]? pixels = null;
                    var width = 0;
                    var file = $"folder-divider-grow-{edge.ToString().ToLowerInvariant()}-{name}-150.png";
                    var (window, panel) = (WindowShots.Extent(flyout.Hwnd), flyout.PanelOnScreen);
                    const int room = 40;
                    var part = new RECT
                    {
                        left = Math.Max(0, panel.left - window.left - room),
                        top = Math.Max(0, panel.top - window.top - room),
                        right = Math.Min(window.right - window.left, panel.right - window.left + room),
                        bottom = Math.Min(window.bottom - window.top, panel.bottom - window.top + room),
                    };
                    Assert.True(WindowShots.Save(flyout.Hwnd, file, part, (shown, w) => (pixels, width) = (shown, w)), $"{file} came back blank");
                    pictures.Add((pixels!, width, pixels!.Length / 4 / width));
                    flyout.Hide();
                }
                WindowShots.SaveSideBySide(pictures, $"folder-divider-grow-{edge.ToString().ToLowerInvariant()}-150.png", (0x80, 0x80, 0x80), gap: 16);
            }
        });
    }

    [UiFact]
    public void DividerGrowSheets()
    {
        WindowShots.OnHiddenDesktop("folder-divider-grow", (gfx, instance) =>
        {
            var work = new RECT { left = 0, top = 0, right = 2400, bottom = 1500 };
            var anchor = new POINT { x = 1200, y = 1500 };
            foreach (var (theme, dark) in new[] { ("graphite", true), ("paper", false) })
            {
                var config = LookShorthand.Wearing(palette: theme);
                var look = TestLooks.LookFor(dark).Wearing(config) with { Animations = false };
                var pictures = new List<(byte[] Canvas, int Width, int Height)>();
                foreach (var state in new[] { "rest", "selected", "hover" })
                {
                    using var folder = new FolderIsland($"layout:default/{Folder().Id}", Folder(), gfx, config);
                    using var flyout = new FolderFlyout(gfx, instance, 0);
                    flyout.Show(gfx, look, Scale, anchor, work, Edge.Bottom, folder, FlyoutAlign.Center, state != "rest");
                    Assert.True(flyout.IsOpen);
                    if (state == "selected")
                    {
                        flyout.Picked = flyout.LineKeys[Grown];
                    }
                    else if (state == "hover")
                    {
                        _ = flyout.Message(WM.WM_MOUSEMOVE, 0, FolderEditClickTests.Client(flyout.Hwnd, flyout.ScreenOfCard(Grown)));
                    }
                    WindowShots.Settle(250);
                    byte[]? pixels = null;
                    var width = 0;
                    var file = $"folder-divider-grow-{theme.ToString().ToLowerInvariant()}-{state}-150.png";
                    var (window, panel) = (WindowShots.Extent(flyout.Hwnd), flyout.PanelOnScreen);
                    const int room = 90;
                    var part = new RECT
                    {
                        left = Math.Max(0, panel.left - window.left - room),
                        top = Math.Max(0, panel.top - window.top - room),
                        right = Math.Min(window.right - window.left, panel.right - window.left + room),
                        bottom = Math.Min(window.bottom - window.top, panel.bottom - window.top + room),
                    };
                    Assert.True(WindowShots.Save(flyout.Hwnd, file, part, (shown, w) => (pixels, width) = (shown, w)), $"{file} came back blank");
                    pictures.Add((pixels!, width, pixels!.Length / 4 / width));
                    flyout.Hide();
                }
                WindowShots.SaveSideBySide(pictures, $"folder-divider-grow-{theme.ToString().ToLowerInvariant()}-150.png", (0x80, 0x80, 0x80), gap: 16);
            }
        });
    }
}
