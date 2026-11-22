// The taskbar's own right-click menu, drawn from the person's list (§532: Config.BarMenuEntries, Karpo's one,
// the default where the file has none; Karpo.Core\BarMenus.cs): what a right click on the bar itself shows
// (Dock.Menus.cs). Which lines it has and in what order is the list's, tidied as Core says (BarMenus.Lines);
// this gives each line its command and its picture, and does what a row asks.
//
// Karpo's own rows keep the commands they always had — "Taskbar settings" (§581; §366 "Edit"), the layouts, Task
// Manager, Lock the taskbar. Any
// other row, a built-in place of Windows', Quit Karpo (§532, asked first as the panel's Quit Karpo asks)
// or a shortcut, is numbered from CMD_BAR_ENTRY and remembered with the menu (Pending.BarMenu), so a row
// chosen is the entry it was drawn from.

using Karpo.Core;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <summary>A row of the bar's menu that has no command of Karpo's own: what <see cref="Dock.CMD_BAR_ENTRY"/> + n stands for.</summary>
internal sealed record BarMenuRow(uint Id, BarMenuEntry Entry);

/// <summary>
/// How the bar's menu sees a shortcut on this PC: whether its target is there, the name and the
/// picture the shell has for it. The dock asks the shell (<see cref="Dock.ShellLooks"/>); tests
/// hand in their own.
/// </summary>
internal sealed record ShortcutLooks(Func<string, TargetCheck> Check, Func<string, string?> Name, Func<BarMenuEntry, FlyoutIcon?> Icon)
{
    /// <summary>Nothing asked of anyone: every target taken as there, named and pictured by the entry alone.</summary>
    public static ShortcutLooks None { get; } = new(_ => new TargetCheck(TargetState.Unchecked, ""), _ => null, _ => null);
}

public sealed unsafe partial class Dock
{
    /// <summary>The first command of the bar's menu's rows that are the person's (<see cref="BarMenuRow"/>); below the add list's.</summary>
    internal const uint CMD_BAR_ENTRY = 5900;

    /// <summary>
    /// The bar's own menu as <paramref name="entries"/> make it (<see cref="BarMenus.Lines"/>), with
    /// <paramref name="layouts"/> — the rows <see cref="LayoutRows"/> gives this bar — where the list
    /// has "Layouts". A divider is a separator, a heading a header as a jump list draws its groups'
    /// titles, a shortcut its shell icon (or its own glyph, else the link glyph), greyed with the
    /// reason under it where this PC doesn't have its target. §534: "Lock the taskbar" ticked where the bars are
    /// <paramref name="locked"/>, as a new config has them.
    /// </summary>
    internal static (List<FlyoutItem> Items, List<BarMenuRow> Rows) BarRows(
        IReadOnlyList<BarMenuEntry> entries,
        IReadOnlyList<FlyoutItem> layouts,
        bool windowsTerminal,
        ShortcutLooks looks,
        bool locked = true)
    {
        var offered = layouts
            .Select(row => new BarMenuLayoutRow(row.Text, (row.Icon as FlyoutIcon.Glyph)?.Code ?? TaskbarLayouts.LayoutGlyph, row.Checked == true))
            .ToList();
        var items = new List<FlyoutItem>();
        var rows = new List<BarMenuRow>();
        foreach (var line in BarMenus.Lines(entries, offered, windowsTerminal, looks.Check, looks.Name, locked))
        {
            switch (line.Kind)
            {
                case BarMenuLine.Divider:
                    items.Add(new FlyoutItem(0, "", Separator: true));
                    continue;
                case BarMenuLine.Heading:
                    items.Add(new FlyoutItem(0, line.Text, Header: true));
                    continue;
            }
            var entry = line.Entry!;
            switch (entry.Kind == BarMenuKind.Action ? entry.Action : 0)
            {
                // Karpo's own rows, by the commands they always had.
                case BarMenuAction.Layouts when line.Layout >= 0 && line.Layout < layouts.Count:
                    items.Add(layouts[line.Layout]);
                    continue;
                case BarMenuAction.TaskManager:
                    items.Add(new FlyoutItem(CMD_TASK_MANAGER, line.Text, Glyph(line.Glyph ?? GlyphTaskManager)));
                    continue;
                // §581: Windows' "Taskbar settings": edit mode on this bar, its Taskbar page open (§366 to §580 "Edit").
                case BarMenuAction.TaskbarSettings:
                    items.Add(new FlyoutItem(CMD_TASKBAR_SETTINGS, line.Text, Glyph(line.Glyph ?? GlyphSettings)));
                    continue;
                // §534: Windows' "Lock the taskbar", ticked while locked; a click turns it (Dock.BarMove.cs).
                case BarMenuAction.LockTaskbar:
                    items.Add(new FlyoutItem(CMD_LOCK_TASKBAR, line.Text, Glyph(line.Glyph ?? OptionGlyphs.Lock), Checked: line.Checked));
                    continue;
            }
            if (rows.Count >= BarMenus.MostEntries)
            {
                continue;
            }
            var id = CMD_BAR_ENTRY + (uint)rows.Count;
            rows.Add(new BarMenuRow(id, entry));
            FlyoutIcon icon = line.Glyph is { } glyph ? Glyph(glyph)
                : looks.Icon(entry) ?? Glyph(BarMenus.ShortcutGlyph);
            items.Add(new FlyoutItem(id, line.Text, icon, Detail: line.Detail, Enabled: line.Enabled));
        }
        return (items, rows);
    }

    /// <summary>
    /// The bar's own menu on <paramref name="window"/>, at <paramref name="anchor"/>: the person's list (§532,
    /// <see cref="Config.BarMenuEntries"/>) with the layouts this bar offers.
    /// </summary>
    private void ShowBarMenu(DockWindow window, Hit? hit, POINT anchor)
    {
        var (items, rows) = BarRows(
            config.BarMenuEntries,
            // layouts: every named layout, the one this bar shows ticked (Dock.Folders.cs).
            LayoutItems(window),
            File.Exists(QuickLinks.WindowsTerminal),
            ShellLooks(window.Px(16f)),
            config.LockTaskbar);
        // Settings that keep not saving lead the menu, outside edit mode too (Dock.KeepConfig.cs).
        items.InsertRange(0, SavesRows(saves));
        ShowFlyout(new Pending(Topic.Panel, window) { Hit = hit, BarMenu = rows }, items, FlyoutLayout.List, anchor);
    }

    /// <summary>
    /// What the shell says of a shortcut, for the bar's menu: whether its target is on this PC
    /// (<see cref="PinTargets.Check"/>, the pins' own answer and sentence), its name and its icon
    /// fitted into <paramref name="px"/> px — the entry's own icon file where it has one. A network
    /// path is drawn with the link glyph rather than asked, since a share that isn't there can
    /// keep the shell, and the menu, waiting for seconds.
    /// </summary>
    internal static ShortcutLooks ShellLooks(int px) => new(
        target => PinTargets.Check(target, PinTargets.System),
        target => Items.IsWeb(target) ? Items.Host(target) : ShellName(target),
        entry =>
        {
            var file = entry.Icon is { Length: > 0 } own && !entry.IconIsGlyph ? own : entry.Target;
            if (file is not { Length: > 0 } || file.StartsWith(@"\\", StringComparison.Ordinal) || Items.IsWeb(file))
            {
                return null;
            }
            return Items.Icon(file, px) is { } pixels ? new FlyoutIcon.Picture(pixels) : null;
        });

    /// <summary>The name the shell gives <paramref name="target"/>; null where it can't find it, or for a network path.</summary>
    private static string? ShellName(string target)
    {
        if (target.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return null;
        }
        var shell = Items.ShellItem(target);
        if (shell == null)
        {
            return null;
        }
        try
        {
            return Items.DisplayName(shell);
        }
        finally
        {
            shell->Release();
        }
    }

    /// <summary>
    /// §532: the bar's menu's Quit Karpo: the settings panel's own (KarpoMenu.Quit) — asked first in Windows' dialog
    /// (<paramref name="ask"/>, over <paramref name="owner"/>), then <paramref name="services"/>' way out. Whether it went.
    /// </summary>
    internal static bool QuitAsked(IDeleteAsk ask, HWND owner, KarpoServices services)
    {
        var quit = KarpoMenu.Quit(new Config(), services)[0];
        if (quit.Ask is { } question && !ask.Ask(owner, question))
        {
            return false;
        }
        _ = quit.Set?.Invoke(0);
        return true;
    }

    /// <summary>The row of the bar's menu <paramref name="id"/> is, in <paramref name="rows"/>; null for any other command.</summary>
    internal static BarMenuRow? BarRowOf(IReadOnlyList<BarMenuRow> rows, uint id) =>
        id >= CMD_BAR_ENTRY && id - CMD_BAR_ENTRY < (uint)rows.Count && rows[(int)(id - CMD_BAR_ENTRY)] is { } row && row.Id == id ? row : null;

    /// <summary>
    /// A row of the bar's menu chosen: a shortcut opened as a pin opens (with its arguments, for a
    /// command), Run as its key, Show desktop as its button, Quit Karpo asked first over
    /// <paramref name="owner"/> (§532, <see cref="QuitAsked"/>), and every other built-in place the way
    /// the Start button's quick links open the same place.
    /// </summary>
    private void RunBarEntry(BarMenuEntry entry, DockWindow? owner)
    {
        if (entry.Kind == BarMenuKind.Shortcut)
        {
            if (entry.Target is { Length: > 0 } target)
            {
                var arguments = entry.Arguments;
                Later(() => Launch.Open(target, null, arguments));
            }
            return;
        }
        switch (entry.Action)
        {
            case BarMenuAction.Run:
                Chord((ushort)VK.VK_LWIN, VK_R);
                return;
            case BarMenuAction.ShowDesktop:
                ToggleDesktop();
                return;
            case BarMenuAction.QuitKarpo:
                // Once the menu has gone, as every row's launch.
                Later(() => QuitAsked(DeleteAsk, owner?.Hwnd ?? HWND.NULL, SettingsServices(KarpoPage.Karpo)));
                return;
        }
        if (BarMenus.Target(entry.Action, File.Exists(QuickLinks.WindowsTerminal)) is { } open)
        {
            if (open.Verb is { } verb)
            {
                Later(() => Launch.Open(open.File, verb));
            }
            else
            {
                LaunchLater(open.File);
            }
        }
    }
}
