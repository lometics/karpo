// app clicks: a window's own menu, which Shift+right click opens on its button as on Windows 11's
// taskbar — restore, move, size, minimize, maximize, close, and whatever the app added to it —, and
// the menu of a button of several windows, as Windows 10's was: restore them all, minimize them all,
// close them all.
//
// The window's menu is the one Windows keeps for it (GetSystemMenu), read line by line and drawn as
// Karpo's other menus are. Which of the standard lines can be chosen is worked out here from the
// window's shape, as DefWindowProc works it out when the window shows the menu itself: Karpo shows
// it instead, so the window never had the chance. A line chosen is its system command posted to the
// window (WM_SYSCOMMAND), with the window brought forward first where the command wants it in front;
// Minimize is the taskbar's one minimize (Apps.Minimize), and a command Windows refuses to post to an
// app run as administrator (a maximize; UIPI lets minimize, restore and close through, §662) is
// tried without asking (Apps.Command).

using System.Text;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>app clicks: a window's shape, as its menu needs it.</summary>
/// <param name="Minimized">Minimized now.</param>
/// <param name="Maximized">Maximized now.</param>
/// <param name="CanMinimize">It has a minimize button (WS_MINIMIZEBOX).</param>
/// <param name="CanMaximize">It has a maximize button (WS_MAXIMIZEBOX).</param>
/// <param name="Sizable">Its frame can be dragged to size it (WS_THICKFRAME).</param>
/// <param name="CanClose">Its class allows closing (no CS_NOCLOSE).</param>
public sealed record WindowShape(
    bool Minimized = false,
    bool Maximized = false,
    bool CanMinimize = true,
    bool CanMaximize = true,
    bool Sizable = true,
    bool CanClose = true);

/// <summary>
/// app clicks: one line of a window's menu as Windows keeps it: its command, its text — with its
/// &amp; marks, and its shortcut after a tab —, whether it can be chosen, and what kind of line it is.
/// </summary>
public readonly record struct MenuLine(uint Id, string Text, bool Enabled = true, bool Separator = false, bool Submenu = false);

/// <summary>app clicks: what choosing a line of a window's menu does: the window brought forward first, the command posted to it.</summary>
public readonly record struct WindowSteps(bool Activate, bool Post);

internal static unsafe class WindowMenus
{
    // The system commands as WM_SYSCOMMAND carries them; their last four bits are Windows' own.
    internal const uint SizeCommand = 0xF000;
    internal const uint MoveCommand = 0xF010;
    internal const uint MinimizeCommand = 0xF020;
    internal const uint MaximizeCommand = 0xF030;
    internal const uint CloseCommand = 0xF060;
    internal const uint RestoreCommand = 0xF120;

    // What TerraFX leaves out: a separator line's type (MFT_SEPARATOR), and the class styles' index (GCL_STYLE).
    private const uint SeparatorType = 0x00000800;
    private const int ClassStyles = -26;

    private const string RestoreGlyph = "\uE923";
    private const string MinimizeGlyph = "\uE921";
    private const string MaximizeGlyph = "\uE922";
    private const string CloseGlyph = "\uE8BB";

    /// <summary>
    /// Whether standard command <paramref name="command"/> can be chosen for a window of
    /// <paramref name="shape"/>, as DefWindowProc greys the lines when the window shows its menu
    /// itself; null for a command of the app's own — and for Close where the class allows it —,
    /// which keeps what the menu says.
    /// </summary>
    internal static bool? Enabled(uint command, WindowShape shape) => (command & 0xFFF0u) switch
    {
        RestoreCommand => shape.Minimized || shape.Maximized,
        MoveCommand => !shape.Maximized,
        SizeCommand => shape.Sizable && !shape.Minimized && !shape.Maximized,
        MinimizeCommand => shape.CanMinimize && !shape.Minimized,
        MaximizeCommand => shape.CanMaximize && !shape.Maximized,
        CloseCommand => shape.CanClose ? null : false,
        _ => null,
    };

    /// <summary>
    /// A window's menu as Karpo draws it: its lines in their order, each standard one with the glyph
    /// Windows gives it and chosen as the window's shape allows, the app's own ones as they are; a
    /// submenu, a line without text and a line without a command left out, and no separator at
    /// either end or two together.
    /// </summary>
    internal static List<FlyoutItem> Rows(IReadOnlyList<MenuLine> lines, WindowShape shape)
    {
        var rows = new List<FlyoutItem>();
        foreach (var line in lines)
        {
            if (line.Separator)
            {
                if (rows.Count > 0 && !rows[^1].Separator)
                {
                    rows.Add(new FlyoutItem(0, "", Separator: true));
                }
                continue;
            }
            var (text, keys) = Split(line.Text);
            if (line.Submenu || line.Id == 0 || text.Length == 0)
            {
                continue;
            }
            rows.Add(new FlyoutItem(line.Id, text, GlyphFor(line.Id), Enabled: Enabled(line.Id, shape) ?? line.Enabled, Accelerator: keys));
        }
        while (rows.Count > 0 && rows[^1].Separator)
        {
            rows.RemoveAt(rows.Count - 1);
        }
        return rows;
    }

    /// <summary>
    /// The menu of a button of several windows, as Windows 10's Shift+right click gave it: restore
    /// them all where any is minimized, minimize them all where any can be and is not, and close
    /// them all — that row nearest the pointer, as the jump list's closing row is (<see cref="Dock.NearestRowIsFirst"/>).
    /// </summary>
    internal static List<FlyoutItem> GroupRows(IReadOnlyList<WindowShape> windows, Edge edge, uint restore, uint minimize, uint close)
    {
        List<FlyoutItem> rest =
        [
            new(restore, S.DockWindowMenuRestoreAll, new FlyoutIcon.Glyph(RestoreGlyph), Enabled: windows.Any(window => window.Minimized)),
            new(minimize, S.DockWindowMenuMinimizeAll, new FlyoutIcon.Glyph(MinimizeGlyph), Enabled: windows.Any(Minimizes)),
        ];
        FlyoutItem closing = new(close, S.DockMenuCloseAllWindows, new FlyoutIcon.Glyph(CloseGlyph));
        FlyoutItem rule = new(0, "", Separator: true);
        return Dock.NearestRowIsFirst(edge) ? [closing, rule, .. rest] : [.. rest, rule, closing];
    }

    /// <summary>
    /// What choosing <paramref name="command"/> does to a window of <paramref name="shape"/>:
    /// Minimize and Close are only posted, as they want nothing in front; Restore on a minimized
    /// window is the window brought forward, as it was before (maximized, if it was), with nothing
    /// posted to restore it further; everything else — moving and sizing by the keys above all —
    /// has the window in front first.
    /// </summary>
    internal static WindowSteps Steps(uint command, WindowShape shape) => (command & 0xFFF0u) switch
    {
        MinimizeCommand or CloseCommand => new WindowSteps(Activate: false, Post: true),
        RestoreCommand when shape.Minimized => new WindowSteps(Activate: true, Post: false),
        _ => new WindowSteps(Activate: true, Post: true),
    };

    /// <summary>The text and the shortcut of a menu line: "&amp;Close\tAlt+F4" is "Close" and "Alt+F4"; a doubled &amp; is one.</summary>
    internal static (string Text, string? Keys) Split(string text)
    {
        var tab = text.IndexOf('\t');
        var keys = tab < 0 ? "" : text[(tab + 1)..].Trim();
        var shown = tab < 0 ? text : text[..tab];
        var plain = new StringBuilder(shown.Length);
        for (var index = 0; index < shown.Length; index++)
        {
            if (shown[index] != '&')
            {
                plain.Append(shown[index]);
            }
            else if (index + 1 < shown.Length && shown[index + 1] == '&')
            {
                plain.Append('&');
                index++;
            }
        }
        return (plain.ToString().Trim(), keys.Length > 0 ? keys : null);
    }

    private static FlyoutIcon? GlyphFor(uint command) => (command & 0xFFF0u) switch
    {
        RestoreCommand => new FlyoutIcon.Glyph(RestoreGlyph),
        MinimizeCommand => new FlyoutIcon.Glyph(MinimizeGlyph),
        MaximizeCommand => new FlyoutIcon.Glyph(MaximizeGlyph),
        CloseCommand => new FlyoutIcon.Glyph(CloseGlyph),
        _ => null,
    };

    // --- The window's own -------------------------------------------------------------------------

    /// <summary>The lines of <paramref name="hwnd"/>'s menu, as Windows keeps it for the window; none for a window without one.</summary>
    internal static List<MenuLine> Read(HWND hwnd)
    {
        var lines = new List<MenuLine>();
        var menu = IsWindow(hwnd) ? GetSystemMenu(hwnd, BOOL.FALSE) : HMENU.NULL;
        if (menu == HMENU.NULL)
        {
            return lines;
        }
        var count = GetMenuItemCount(menu);
        var text = stackalloc char[256];
        for (var index = 0; index < count; index++)
        {
            var info = new MENUITEMINFOW
            {
                cbSize = (uint)sizeof(MENUITEMINFOW),
                fMask = MIIM_FTYPE | MIIM_ID | MIIM_STATE | MIIM_STRING | MIIM_SUBMENU,
                dwTypeData = text,
                cch = 255,
            };
            if (!GetMenuItemInfoW(menu, (uint)index, BOOL.TRUE, &info))
            {
                continue;
            }
            var separator = (info.fType & SeparatorType) != 0;
            var words = separator || info.dwTypeData == null ? "" : new string(text, 0, (int)Math.Min(info.cch, 255u));
            lines.Add(new MenuLine(
                info.wID,
                words,
                Enabled: (info.fState & (MFS_DISABLED | MFS_GRAYED)) == 0,
                Separator: separator,
                Submenu: info.hSubMenu != HMENU.NULL));
        }
        return lines;
    }

    /// <summary><paramref name="hwnd"/>'s shape now, read from its state and its styles.</summary>
    internal static WindowShape ShapeOf(HWND hwnd)
    {
        var style = (uint)GetWindowLongW(hwnd, GWL.GWL_STYLE);
        var classStyle = (uint)GetClassLongPtrW(hwnd, ClassStyles);
        return new WindowShape(
            Minimized: IsIconic(hwnd),
            Maximized: IsZoomed(hwnd),
            CanMinimize: (style & WS.WS_MINIMIZEBOX) != 0,
            CanMaximize: (style & WS.WS_MAXIMIZEBOX) != 0,
            Sizable: (style & WS.WS_THICKFRAME) != 0,
            CanClose: (classStyle & CS.CS_NOCLOSE) == 0);
    }

    /// <summary>
    /// Line <paramref name="command"/> of <paramref name="hwnd"/>'s menu chosen (<see cref="Steps"/>): the
    /// command asked of the window, as Windows' taskbar asks it — done without it where the app can't
    /// answer or Windows refuses the ask (<see cref="Apps.Command"/>).
    /// </summary>
    internal static void Choose(HWND hwnd, uint command)
    {
        if (!IsWindow(hwnd))
        {
            return;
        }
        var steps = Steps(command, ShapeOf(hwnd));
        if (steps.Activate)
        {
            Apps.Activate(hwnd);
        }
        if (steps.Post)
        {
            if ((command & 0xFFF0u) == MinimizeCommand)
            {
                // The one way a window is minimized (Apps.Minimize).
                Apps.Minimize(hwnd);
            }
            else if ((command & 0xFFF0u) == CloseCommand)
            {
                Apps.Close(hwnd);
            }
            else
            {
                _ = Apps.Command(hwnd, command);
            }
        }
    }

    /// <summary>
    /// Whether a window of <paramref name="shape"/> is minimized from its button, as Windows' taskbar
    /// does it: one with a minimize button (WS_MINIMIZEBOX) that is not minimized already. A window
    /// without one (a borderless app's, a dialog's) stays as it is, as on Windows.
    /// </summary>
    internal static bool Minimizes(WindowShape shape) => shape.CanMinimize && !shape.Minimized;

    /// <summary>Every minimized one of <paramref name="windows"/> (most recently used first) shown again, the one restore (<see cref="Apps.Restore"/>), the most recent last so it ends in front.</summary>
    internal static void RestoreAll(IReadOnlyList<HWND> windows)
    {
        for (var index = windows.Count - 1; index >= 0; index--)
        {
            Apps.Restore(windows[index]);
        }
        if (windows.Count > 0 && IsWindow(windows[0]))
        {
            Apps.Activate(windows[0]);
        }
    }

    /// <summary>Every one of <paramref name="windows"/> that can be, minimized, the one way (<see cref="Apps.Minimize"/>).</summary>
    internal static void MinimizeAll(IReadOnlyList<HWND> windows)
    {
        foreach (var window in windows)
        {
            Apps.Minimize(window);
        }
    }
}
