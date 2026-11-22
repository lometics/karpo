// Which parts of a bar show a hover tooltip, and what each says (docs\widgets.md §367, §382).
//
// None over a widget or a folder on the bar (the person, 2026-10-26: "remove the tooltips when
// hovering over a folder face or widget face"): not their words, not a folder's name, not the
// title of their kind, not why one is greyed, not the age of mail that could not be refreshed.
// What a face says is on the face, and a click opens the rest. At rest and in edit mode alike.
// But for Windows' status icons (2026-10-29, the person: the status icons get their tooltips back):
// the network, the sound, Bluetooth, the battery and the bell, each over its own icon, in the words
// Windows' taskbar has for it (IsStatusIcon, FolderIsland.StatusTipAt). And words a face drew cut short
// (2026-11-03, the person: "when hovering over text that is partially hidden ... i would like the tooltip
// to show the text"): those whole, over them (CutWords, CellIsland.CutAt).
// A widget's Tooltip is still asked, by a screen reader (Dock.Uia.cs), which has no face to look at;
// and the parts inside an open folder keep theirs (FolderFlyout.Tips.cs).
//
// One, as on Windows' taskbar, for what is not a widget:
//   * an app's button: its window's title, which the button may have cut short, else the app's name;
//     none for a pin this PC can't open, whose click is Karpo's menu saying why (ForTile);
//   * Windows' own parts: Start, search, task view and show desktop (ForPart), and the buttons that
//     came after them — the widgets board, the Snipping Tool, Settings, the pen menu, the touch
//     keyboard, the virtual touchpad — by the title Windows gives each (ForWindowsButton); none for
//     the Snipping Tool while it is not installed, whose click is Karpo's menu saying so;
//   * a tray icon: the app's own (Dock.TrayTip).
// None for the apps' "…" and the tray's chevron, which open Karpo's lists of what did not fit.

using Karpo.Core;

namespace Karpo.Dock;

internal static class BarTooltips
{
    /// <summary>
    /// The tooltip over <paramref name="hit"/>: <paramref name="tile"/> answers for an app's button by
    /// its index, <paramref name="tray"/> for a tray icon, <paramref name="status"/> for the status icon
    /// under the pointer on a widget's or a folder's cell; nothing else there has one (§367).
    /// </summary>
    public static string? For(Hit? hit, Func<int, string?> tile, Func<Hit, string?> tray, Func<Hit, string?> status) => hit switch
    {
        { Kind: HitKind.Tile, Index: var index } => tile(index),
        { Kind: HitKind.Start or HitKind.Search or HitKind.TaskView or HitKind.ShowDesktop or HitKind.AppOverflow or HitKind.TrayOverflow } part
            => ForPart(part.Kind),
        { Kind: HitKind.WindowsButton, Index: var which } => ForWindowsButton((BarKind)which, WindowsButtons.Missing((BarKind)which)),
        { Kind: HitKind.Tray } icon => tray(icon),
        { Kind: HitKind.Island } cell => status(cell),
        _ => null,
    };

    /// <summary>
    /// Whether <paramref name="face"/> is one of Windows' status icons, whose tooltip the bar shows
    /// over it as the taskbar does: the network, the sound, Bluetooth, the battery and the bell.
    /// </summary>
    public static bool IsStatusIcon(FaceInfo face) =>
        face.Id is FolderFaces.WifiId or FolderFaces.SoundId or FolderFaces.BluetoothId or FolderFaces.BatteryId or FolderFaces.NotificationsId;

    /// <summary>
    /// The tooltip over Windows' button of <paramref name="kind"/>: the title Windows gives it, and
    /// none where its app is <paramref name="missing"/> (the reason, or null while it is there) and
    /// its click opens Karpo's menu saying so.
    /// </summary>
    public static string? ForWindowsButton(BarKind kind, string? missing) =>
        missing is not null ? null : kind.Title();

    /// <summary>
    /// The tooltip over an app's button: its <paramref name="label"/> (a window's title, which the
    /// button may have cut short), else its <paramref name="name"/>; none for a pin this PC can't
    /// open (<paramref name="broken"/>), whose click opens Karpo's menu about it.
    /// </summary>
    public static string? ForTile(string name, string? label, string? broken) =>
        broken is not null ? null : label ?? name;

    /// <summary>
    /// The tooltip over a part the bar draws itself that is not an app's button, a widget, a tray
    /// icon or one of Windows' other buttons: Windows' Start, search and task view, and show
    /// desktop, named as on the taskbar; none for the apps' "…" and the tray's chevron, whose clicks
    /// open Karpo's lists of what did not fit.
    /// </summary>
    public static string? ForPart(HitKind part) => part switch
    {
        HitKind.Start => S.WidgetPartStart,
        HitKind.Search => S.WidgetPartSearch,
        HitKind.TaskView => S.WidgetPartTaskView,
        HitKind.ShowDesktop => S.WidgetPartShowDesktop,
        _ => null,
    };

    /// <summary>
    /// <paramref name="text"/> as the room beside the bar holds it (Layout.TooltipMostHeight): whole
    /// where it is at most <paramref name="most"/> px tall as <paramref name="height"/> measures it,
    /// else its first lines, as many as fit, the last ending in "…" — never run past the window and
    /// cut off, nor over the bar. One line stays whatever the room: the width cuts that.
    /// </summary>
    public static string Fitted(string text, float most, Func<string, float> height)
    {
        if (height(text) <= most)
        {
            return text;
        }
        var lines = text.Split('\n');
        for (var count = lines.Length - 1; count > 1; count--)
        {
            var cut = string.Join('\n', lines[..count]) + "…";
            if (height(cut) <= most)
            {
                return cut;
            }
        }
        return lines[0];
    }
}
