// menus: what keeps a menu open (the person, 2026-10-19: "in general, when i change a setting, choose
// something or do another action the submenu it should not close the whole menu. because then if i want
// to change multiple things i always have to open the stuff again. when a tree node is extended and i
// click it again it should close again. if i click somewhere away from the node like it is already it
// should close the menu completely, as it is now"; docs\widgets.md §219).
//
//  - **A row that stays** (FlyoutItem.Stays): a setting's choice, a face added, moved or taken off, a
//    widget's own switch or choice — picked by the pointer, the keys or a screen reader, it acts and
//    the menu stays, every submenu open, as a control's row does (FlyoutEventKind.Adjust). Whoever
//    showed it shows the rows as they are now (Flyout.Refresh), the open path found again row by row
//    (Again) and each level where it was scrolled — but a choice picked on a level of one choice, shown
//    again, is back on the level it opened from (FlyoutItem.PickOne, §392). What takes the person elsewhere — a page of
//    Settings, an app, a file, a link, a meeting, "Copy" (the copy on the pointer), "Remove" — and a
//    one-off command still close it, as does a dropdown's pick (a list of choices that is the whole
//    menu: a list's width, a sound device's dropdown, the keyboard layouts).
//  - **A row with a submenu** (Flyout.ParentClicked): a click opens it where it is shut, in place of
//    another open one, and shuts it where it is open, the menu over it staying. The pointer resting on
//    a row only lights it: a submenu opens by a click or the keys alone, and stays open wherever the
//    pointer goes after (the person, 2026-10-19: "the sub menus should [not] open on hover, only when
//    i click them deliberatly"; §246).
//  - **A press outside every level** closes the whole menu, as before (FlyoutHost, LightDismiss).

namespace Karpo.Dock;

/// <summary>menus: what keeps a menu open; see the top of the file.</summary>
internal static class MenuKeeps
{
    /// <summary>
    /// Where <paramref name="was"/>, a row of a menu shown before, stands among <paramref name="rows"/>,
    /// the menu's rows now: the row of its id and its words; else the one row of its words alone (a
    /// face moved, a row whose command moved as rows before it came or went); else (none, or several of its
    /// words) the one row of its id alone, its words changed under it (§365: a layout renamed in its own
    /// level, a clock face's label set in the face's, a thing renamed by its pencil, another language — a row
    /// is numbered before the rows of its level, so nothing done in a level moves the row it opened from); -1
    /// for none. How a menu shown again finds its open levels and its lit row, and one opened where it was
    /// left (§356).
    /// </summary>
    public static int Again(IReadOnlyList<FlyoutItem> rows, FlyoutItem was)
    {
        var (worded, words) = (-1, 0);
        var (numbered, ids) = (-1, 0);
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (row.Separator || row.Header)
            {
                continue;
            }
            if (row.Text == was.Text)
            {
                if (row.Id == was.Id)
                {
                    return index;
                }
                (worded, words) = (index, words + 1);
            }
            else if (was.Id != 0 && row.Id == was.Id)
            {
                (numbered, ids) = (index, ids + 1);
            }
        }
        return words == 1 ? worded : ids == 1 ? numbered : -1;
    }

    /// <summary>
    /// Whether a widget's own act stays (<see cref="FlyoutItem.Stays"/>): a switch ticked while on, or
    /// one of a few under a heading, the one in force ticked — what a tick shows again as it is. An act
    /// whose row says its state in words (play and pause, "Turn … on" and "off", repeat), one that is done
    /// once (refresh, clear, eject) and one that takes the person elsewhere close the menu.
    /// </summary>
    public static bool Stays(WidgetAct act) => Switches(act) || Picks(act);

    // A switch: its tick flips.
    private static bool Switches(WidgetAct act) => act is WidgetAct.Mute or WidgetAct.MuteMicrophone or WidgetAct.MuteApp
        or WidgetAct.Shuffle;

    // One of a few: the tick moves to it.
    private static bool Picks(WidgetAct act) => act is WidgetAct.ChooseDevice or WidgetAct.FollowSession or WidgetAct.PowerMode or WidgetAct.AwakeFor;

    /// <summary>
    /// <paramref name="items"/>, a menu whose act <paramref name="id"/> of <paramref name="acts"/> was
    /// just done and stayed, as it shows now: a switch's tick flipped, a pick's moved to it from the
    /// others of its act; submenus too. Every other row as it was.
    /// </summary>
    public static List<FlyoutItem> Ticked(IReadOnlyList<FlyoutItem> items, IReadOnlyList<WidgetActRow> acts, uint id)
    {
        if (Dock.ActRowOf(acts, id) is not { } done || !Stays(done.Act))
        {
            return [.. items];
        }
        FlyoutItem Tick(FlyoutItem item)
        {
            if (item.Children is { } children)
            {
                item = item with { Children = children.Select(Tick).ToList() };
            }
            if (item.Id == id && Switches(done.Act))
            {
                return item with { Checked = item.Checked != true };
            }
            if (Picks(done.Act) && Dock.ActRowOf(acts, item.Id) is { } row && row.Act == done.Act)
            {
                return item with { Checked = item.Id == id };
            }
            return item;
        }
        return items.Select(Tick).ToList();
    }
}
