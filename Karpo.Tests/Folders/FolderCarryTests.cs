// edit: a folder's line copied by "Copy" in its menu follows the pointer in its folder with no button
// held, until a click puts it down or Escape or a right click calls it off (the person, 2026-10-11:
// "when copying a widget it should go into edit mode, and then not just place it next but actually
// pin it to the mouse until i left click somewhere i want it"; FolderFlyout.Carry.cs, Dock.EditCopy.cs).
// The dock's half — the copy, the hooks that hear the clicks, the clipboard — is held by
// WidgetButtonsTests; here the folder's, on real folder windows on a desktop of their own:
//
//  - carried, the line's landing follows the pointer's moves, no button held, and it is never carried
//    out of the folder: away from it, it lands nowhere;
//  - put down, the folder says where (FolderFlyoutAction.Place), and carries it no more;
//  - called off, it says nothing and carries it no more; a line it doesn't hold isn't carried.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.FolderEditClickTests;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed unsafe class FolderCarryTests
{
    private const string Owner = "layout:default";

    private static readonly Look Dark = new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));
    private static readonly POINT Anchor = new() { x = 960, y = 1040 };
    private static readonly RECT Work = new() { right = 1920, bottom = 1040 };

    [UiFact]
    public void ACopiedLineFollowsThePointerUntilItIsPutDownOrCalledOff()
    {
        WindowShots.OnHiddenDesktop("folder-carry", (gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", List(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            // At rest, as a folder whose line's menu was right-clicked: carrying arranges it.
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder);
            Assert.False(flyout.Carry($"{Owner}/box/gone"));
            Assert.False(flyout.Carrying);

            Assert.True(flyout.Carry($"{Owner}/box/clock"));
            Assert.True(flyout.Carrying);
            Assert.True(flyout.Arranged);

            // No button held: the moves aim it, and nothing is pressed.
            var battery = flyout.ScreenOfCard(1);
            var under = battery with { y = battery.y + 4 };
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_MOUSEMOVE, 0, Client(flyout.Hwnd, under)).Kind);
            Assert.Equal(-1, flyout.Pressed);
            // Away from the folder — on the bar, under the work area its arranged window reaches to,
            // where no slot is — it is not carried out of it.
            var away = new POINT { x = 40, y = Work.bottom + 20 };
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_MOUSEMOVE, 0, Client(flyout.Hwnd, away)).Kind);

            // Put down on the battery's lower half: under it, the list's end.
            var placed = flyout.LandCarry(under);
            Assert.Equal(FolderFlyoutAction.Place, placed.Kind);
            Assert.Equal($"{Owner}/box/clock", placed.Widget?.Key);
            Assert.Equal(new FolderPlace(0, 2), placed.Place);
            Assert.False(flyout.Carrying);

            // Put down away from the folder: nowhere to land, so where it went in.
            Assert.True(flyout.Carry($"{Owner}/box/battery"));
            Assert.Equal(FolderFlyoutAction.None, flyout.LandCarry(away).Kind);
            Assert.False(flyout.Carrying);

            // Called off: nothing said, nothing carried.
            Assert.True(flyout.Carry($"{Owner}/box/battery"));
            flyout.EndCarry();
            Assert.False(flyout.Carrying);
            Assert.Equal(FolderFlyoutAction.None, flyout.LandCarry(under).Kind);
            Assert.True(flyout.IsOpen);
            flyout.Hide();
            folder.Dispose();
        });
    }
}
