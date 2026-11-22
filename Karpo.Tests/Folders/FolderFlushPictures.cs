// A folder's lines edge to edge (the person, 2026-10-29: "the widgets themselves in the folder have a gap between
// them, this should not be, there should bascially be no folder background visible at all. the widgets themselves
// should also not have rounded corners, only the folder sections should have them"; docs\widgets.md §410). The real
// folder in a window, two columns, a section split by a separator in each, in three themes — Graphite (dark, cards),
// Paper (light, outlines), Deep sea (dark, outlines) —: at rest, a
// line hovered, and arranged with a line selected. To KARPO_PNG_DIR as folder-flush-<theme>-150.png,
// each a row of the three. What it holds as numbers is FolderFlushTests'; this is for looking at.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Folders;

public sealed class FolderFlushPictures
{
    private const float Scale = 1.5f;

    /// <summary>Two columns: three switches, a separator, the battery and the clock; the CPU and memory, a separator, a note.</summary>
    private static BarItem Folder()
    {
        var island = BarKind.FolderList.New();
        island.Id = "flush";
        island.Folder!.Lists = [];
        ListKind[][] lists =
        [
            [ListKind.NightLight, ListKind.AirplaneMode, ListKind.EnergySaver, ListKind.Divider, ListKind.Battery, ListKind.Clock],
            [ListKind.Cpu, ListKind.Memory, ListKind.Divider, ListKind.Note],
        ];
        for (var list = 0; list < lists.Length; list++)
        {
            foreach (var kind in lists[list])
            {
                island.Folder.Put(kind.New(), list);
            }
        }
        return island;
    }

    [UiFact]
    public void FlushSheets()
    {
        WindowShots.OnHiddenDesktop("folder-flush", (gfx, instance) =>
        {
            var work = new RECT { left = 0, top = 0, right = 2400, bottom = 1500 };
            var anchor = new POINT { x = 1200, y = 1500 };
            foreach (var theme in new[] { "graphite", "paper", "deepSea" })
            {
                var config = LookShorthand.Wearing(palette: theme);
                var look = TestLooks.LookFor(dark: true).Wearing(config) with { Animations = false };
                var pictures = new List<(byte[] Canvas, int Width, int Height)>();
                foreach (var state in new[] { "rest", "hover", "edit" })
                {
                    using var folder = new FolderIsland($"layout:default/{Folder().Id}", Folder(), gfx, config);
                    using var flyout = new FolderFlyout(gfx, instance, 0);
                    flyout.Show(gfx, look, Scale, anchor, work, Edge.Bottom, folder, FlyoutAlign.Center, state == "edit");
                    Assert.True(flyout.IsOpen);
                    if (state == "hover")
                    {
                        // The second switch, between two others in its section.
                        _ = flyout.Message(WM.WM_MOUSEMOVE, 0, FolderEditClickTests.Client(flyout.Hwnd, flyout.ScreenOfCard(1)));
                    }
                    else if (state == "edit")
                    {
                        // The first switch selected, at its section's top, and the battery hovered, at the next one's.
                        flyout.Picked = flyout.LineKeys[0];
                        _ = flyout.Message(WM.WM_MOUSEMOVE, 0, FolderEditClickTests.Client(flyout.Hwnd, flyout.ScreenOfCard(4)));
                    }
                    WindowShots.Settle(250);
                    byte[]? pixels = null;
                    var width = 0;
                    var file = $"folder-flush-{theme.ToString().ToLowerInvariant()}-{state}-150.png";
                    // The panels and the room round them, the "+" over them included.
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
                WindowShots.SaveSideBySide(pictures, $"folder-flush-{theme.ToString().ToLowerInvariant()}-150.png", (0x80, 0x80, 0x80), gap: 16);
            }
        });
    }
}
