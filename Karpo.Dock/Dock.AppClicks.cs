// app clicks: what the keys held, Shift+right click and a drag of files do on an app's button, as on
// Windows 11's taskbar — the dock's side of it: which window, which menu, which program. The rules
// are in AppClicks, WindowMenus, EndTask and AppDrops, and tested there; nothing here decides.

using System.Numerics;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    // app clicks: End task in a jump list, and a button of several windows' menu.
    private const uint CMD_END_TASK = 2900;
    private const uint CMD_RESTORE_ALL = 2901;
    private const uint CMD_MINIMIZE_ALL = 2902;

    /// <summary>app clicks: End task's row, a stop sign as the process is stopped rather than asked.</summary>
    private const string GlyphEndTask = "\uE71A";

    /// <summary>app clicks: Windows' hover time (SPI_GETMOUSEHOVERTIME), 400 ms unless changed.</summary>
    internal static uint HoverTimeMs
    {
        get
        {
            uint ms = 400;
            _ = SystemParametersInfoW((uint)SPI.SPI_GETMOUSEHOVERTIME, 0, &ms, 0);
            return Math.Clamp(ms, 50u, 5000u);
        }
    }

    /// <summary>
    /// app clicks: the menu Shift+right click opens on button <paramref name="index"/> of
    /// <paramref name="window"/>: the window's own for a button of one (a window without one gets
    /// its app's jump list, as a right click opens), its windows' for a button of several.
    /// </summary>
    private void WindowMenu(DockWindow window, int index, Tile tile)
    {
        var anchor = Hit.Tile(index);
        if (tile.Open.Count == 1)
        {
            var hwnd = tile.Open[0];
            var lines = WindowMenus.Rows(WindowMenus.Read(hwnd), WindowMenus.ShapeOf(hwnd));
            if (lines.Count == 0)
            {
                AppMenu(window, index);
                return;
            }
            ShowFlyout(new Pending(Topic.Window, window) { Hit = anchor, Open = [hwnd] }, lines, FlyoutLayout.List, window.Anchor(anchor));
            return;
        }
        var rows = WindowMenus.GroupRows([.. tile.Open.Select(WindowMenus.ShapeOf)], window.Layout.Edge, CMD_RESTORE_ALL, CMD_MINIMIZE_ALL, CMD_CLOSE);
        ShowFlyout(new Pending(Topic.Window, window) { Hit = anchor, Open = [.. tile.Open] }, rows, FlyoutLayout.List, window.Anchor(anchor));
    }

    /// <summary>
    /// app clicks: a line of a window's menu chosen — the window's own command, which may be one of
    /// the app's own (its id is the window's, not Karpo's) —, or of its windows' menu: them all
    /// restored, minimized or closed.
    /// </summary>
    private void WindowChosen(Pending what, uint id)
    {
        if (what.Open.Count == 1)
        {
            WindowMenus.Choose(what.Open[0], id);
            return;
        }
        switch (id)
        {
            case CMD_RESTORE_ALL:
                WindowMenus.RestoreAll(what.Open);
                break;
            case CMD_MINIMIZE_ALL:
                WindowMenus.MinimizeAll(what.Open);
                break;
            case CMD_CLOSE:
                foreach (var open in what.Open)
                {
                    Apps.Close(open);
                }
                break;
        }
    }

    /// <summary>
    /// app clicks: what files dropped at <paramref name="at"/> (window px) on <paramref name="window"/>'s
    /// bar do (<see cref="AppDrops.Decide"/>), and what they land on: a folder's part, or an app's button.
    /// </summary>
    private (DropAction Action, int Part, int Tile) DropAt(DockWindow window, Vector2 at)
    {
        var layout = window.Layout;
        var along = layout.Vertical ? at.Y : at.X;
        for (var part = 0; part < layout.Specs.Count; part++)
        {
            if (!layout.Placed(part))
            {
                continue;
            }
            var block = layout.PartAt(part);
            if (along < block.Left || along >= block.Right)
            {
                continue;
            }
            if (layout.Specs[part].Kind.IsFolder() && FolderOptionsOf(layout.Specs[part].Key) is not null && Folders.CanAdd(layout.Specs[part].Kind, WidgetInfos.Pin.Id))
            {
                return (AppDrops.Decide(new DropSpot(Folder: true)), part, -1);
            }
            break;
        }
        if (layout.HitTest(at.X, at.Y) is { Kind: HitKind.Tile, Index: var index } && window.TileAt(index) is { } tile)
        {
            // A pin by the target as the person wrote it, which is what says whether it is a program.
            var target = tile.Pinned && tile.Key is { } key && config.FindIsland(Islands.OwnerOf(key), Islands.IdOf(key))?.Pin?.Target is { } written
                ? written
                : tile.Item.Target;
            var spot = new DropSpot(
                Button: true,
                Pinned: tile.Pinned,
                Program: tile.Pinned ? PinItems.Program(target) : null,
                Aumid: AppDrops.AumidIn(target) ?? tile.Item.Aumid,
                WindowExe: tile.Open.Select(apps.Get).FirstOrDefault(win => win is not null)?.Exe);
            return (AppDrops.Decide(spot), layout.PartOf(Hit.Tile(index)), index);
        }
        return (AppDrops.Decide(new DropSpot()), -1, -1);
    }

    /// <summary>app clicks: <paramref name="hwnd"/> in front, where it is not already — a drag rested on its button.</summary>
    private static void BringForward(HWND hwnd)
    {
        if (IsWindow(hwnd) && Apps.Foreground() != hwnd)
        {
            Apps.Activate(hwnd);
        }
    }

    private sealed partial class DockWindow
    {
        /// <summary>app clicks: a drag of files has rested on an app's button for Windows' hover time (DragHover).</summary>
        private const uint DROP_HOVER = 21;

        private DragHover? dropHover;

        /// <summary>app clicks: files are over tile <paramref name="tile"/> (-1 for none, or one with no window to bring forward).</summary>
        private void DropHover(int tile)
        {
            dropHover ??= new DragHover(HoverTimeMs);
            if (dropHover.Over(tile, Environment.TickCount64) is { } wait)
            {
                Arm(DROP_HOVER, Math.Max(1u, wait));
            }
            else
            {
                Kill(DROP_HOVER);
            }
        }

        /// <summary>
        /// app clicks: the drag rested long enough: a button of one window brings it forward; a button
        /// of several shows their list, whose cards the drag goes on to rest on to pick one, as Windows'
        /// taskbar does (Dock.AppList.cs).
        /// </summary>
        private void DropHoverDue()
        {
            Kill(DROP_HOVER);
            if (dropHover?.Due(Environment.TickCount64) is >= 0 and var tile && TileAt(tile) is { Open.Count: > 0 } button)
            {
                if (button.Open.Count == 1)
                {
                    BringForward(button.Open[0]);
                }
                else if (!(OwnsOpenPreview && dock.preview?.Tile == tile))
                {
                    dock.OpenPreview(this, tile);
                }
            }
        }

        /// <summary>app clicks: the drag left the bar or was dropped: nothing waits to come forward.</summary>
        private void DropHoverEnded()
        {
            dropHover?.Leave();
            Kill(DROP_HOVER);
        }
    }
}
