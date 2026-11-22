// The switchers, and everything about them that is a decision rather than a drawing: what the
// face on the bar says, and what the line in its folder holds. The desktop switcher and the layout
// switcher were buttons on the bar until they became the desktops and layouts presets (2026-10-11,
// docs\widgets.md §116 and §127), whose faces name the desktop you are on and the layout this bar
// shows as the switchers did and whose lines are their lists — so their rows, and what a desktop's
// may have done to it, are still decided here.
//
// The user asked for the desktop switcher to stop being every desktop in one
// line: "it should just show the name of the current desktop, and it should have
// a flyout with all the desktops and the program icons on the right aligned.
// then you can choose another desktop from there. same should be done with a
// layout switcher". So the two are one shape, and one shape is one set of rules:
// a name on the bar, a list of names to choose from, and whatever each row can
// say about itself at its right.
//
// The layouts' rows are not decided here. They are the bar's own right-click
// menu's rows (Dock.LayoutRows), turned into list rows: the same layouts, the
// same order, the same names and glyphs, and the same rule that below two there
// is nothing to choose. There must not be two answers to "how do I change
// layout" that could disagree.
//
// Nothing here reads the registry, the config or a clock: it is handed what was
// read, so every rule below is tested without a bar (Karpo.Tests\Desktops\
// SwitcherTests.cs).

using Karpo.Core;

namespace Karpo.Dock;

/// <summary>
/// One row of a switcher's list: what it is called, the glyph before it, whether it is the one
/// showing now, and — for a desktop — the apps open on it, right-aligned.
/// </summary>
/// <param name="Apps">The apps whose icons the row shows, most recently used first; never more than <see cref="Switchers.RowIcons"/>.</param>
/// <param name="More">How many apps there are beyond those icons; 0 when they all have one.</param>
/// <param name="Key">
/// What the row is, rather than where it is: a desktop's id, a layout's. The list keeps a row's
/// widget by this, so a desktop that changed places is the same row in another place and not a new
/// row — which is what lets the pointer and the keyboard stay with the thing that just moved.
/// Empty where the caller has no id, and then the row's place stands in for one.
/// </param>
public readonly record struct SwitchRow(string Name, string Glyph, bool Current, IReadOnlyList<Item> Apps, int More, string Key = "")
{
    /// <summary>"+3" for the apps past the icons; empty where every app has an icon of its own.</summary>
    public string Plus => More > 0 ? $"+{More}" : "";
}

public static class Switchers
{
    /// <summary>
    /// The app icons one row shows before it starts counting. A row is 360 DIPs across rather than
    /// the 40-DIP square the old desktop buttons were, so it holds more than the three that fitted
    /// there — and few enough that the name keeps most of the row. Beyond this the rest are "+N",
    /// which is the count the buttons already had, in the place a row has for it.
    /// </summary>
    public const int RowIcons = 4;

    /// <summary>
    /// The room the button on the bar keeps for a name, whatever the name turns out to be: ten
    /// characters, which is what Windows' own "Desktop 88" needs and about what Karpo's own
    /// "New layout" does.
    /// <para>
    /// It is a fixed template on purpose. The name shown is <b>state</b> — it changes every time
    /// you switch — so measuring it would move the bar under the hand that is switching, which is
    /// the one thing a widget may never do (docs\widgets.md §4). A longer name is cut with an
    /// ellipsis and its whole self is in the tooltip; somebody who wants more room gives the
    /// widget a width, as they would to the clock.
    /// </para>
    /// </summary>
    public static string NameRoom => Numbered(87);

    /// <summary>
    /// What the desktop switcher's button says: the name of the desktop you are on. Windows names
    /// them "Desktop 1", "Desktop 2" until somebody types something, and that name is what shows —
    /// the old buttons showed the number alone, because a 1 × 1 square had room for nothing else.
    /// </summary>
    public static string DesktopName(IReadOnlyList<string> names, int current) =>
        current >= 0 && current < names.Count && names[current] is { Length: > 0 } name ? name : Numbered(current);

    /// <summary>Windows' own name for the desktop at <paramref name="index"/>, which it uses until one is typed.</summary>
    public static string Numbered(int index) => S.DockDesktopsNumbered(Math.Max(index, 0) + 1);

    /// <summary>
    /// The desktops as rows: each one's name, the apps open on it on this display, and how many
    /// more there are. <paramref name="showApps"/> is the widget's own setting: off, the rows are
    /// names alone. A desktop with nothing open shows its name and nothing at its right — the same
    /// as one whose icons are switched off, and both mean "no icons to show here".
    /// </summary>
    public static IReadOnlyList<SwitchRow> Desktops(DesktopStrip strip, bool showApps, string glyph)
    {
        var rows = new List<SwitchRow>(strip.Buttons.Count);
        for (var index = 0; index < strip.Buttons.Count; index++)
        {
            var button = strip.Buttons[index];
            var name = button.Name is { Length: > 0 } given ? given : Numbered(index);
            rows.Add(showApps
                ? new SwitchRow(name, glyph, index == strip.Current, button.Apps, button.MoreApps, button.Id)
                : new SwitchRow(name, glyph, index == strip.Current, [], 0, button.Id));
        }
        return rows;
    }

    /// <summary>
    /// The layouts as rows, from the rows the bar's own menu offers (<c>Dock.LayoutRows</c>): one
    /// per layout in the config's order, with its own icon, the one this bar shows marked. Fewer
    /// than two offer nothing to choose: none, which the layouts line says in its Status (§561: the
    /// bar's menu shows a lone one, which says which layout the bar is in).
    /// </summary>
    public static IReadOnlyList<SwitchRow> Layouts(IReadOnlyList<FlyoutItem> menu) => menu.Count < 2 ? [] :
    [
        .. menu.Select(item => new SwitchRow(
            item.Text,
            item.Icon is FlyoutIcon.Glyph glyph ? glyph.Code : TaskbarLayouts.LayoutGlyph,
            item.Checked == true,
            [],
            0,
            // The command the menu gives each layout, which is its place in the config's list: the
            // layouts cannot be reordered from here, so a place is as good an identity as an id.
            item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture))),
    ];

    /// <summary>
    /// What a desktop's row of the desktops' line may have done to it, and why not where it may not:
    /// its pencil, its cross and its gripper (DesktopsLine). A row that cannot do one still shows it,
    /// dimmed, with the reason in its tooltip — a missing button says nothing, and "Windows keeps one
    /// desktop" is worth reading.
    /// </summary>
    /// <param name="WhyNotRename">The reason the row cannot be renamed; null when it can.</param>
    /// <param name="WhyNotDelete">The same for closing it.</param>
    /// <param name="CanMove">Whether it can be dragged into another place in Windows' order.</param>
    /// <param name="WhyNotMove">The reason it cannot; null when it can.</param>
    public readonly record struct DesktopActions(
        bool CanRename,
        string? WhyNotRename,
        bool CanDelete,
        string? WhyNotDelete,
        bool CanMove,
        string? WhyNotMove);

    /// <summary>Windows keeps a desktop: the last one cannot go, and Task View refuses it too.</summary>
    public static string LastDesktop => S.DockDesktopsLastDesktop;

    /// <summary>
    /// Renaming and deleting another desktop are the shell's own calls, and Karpo only makes them
    /// on the Windows builds its table was checked against (<see cref="DesktopControl"/>).
    /// </summary>
    public static string NoShellRename => S.DockDesktopsNoShellRename;

    /// <summary>
    /// Without those calls the only thing left is Ctrl+Win+F4, which closes the desktop you are
    /// on. Deleting another one would have to switch there first, which is not what the row said
    /// it would do, so the row says why instead.
    /// </summary>
    public static string OnlyCurrentDeletes => S.DockDesktopsOnlyCurrentDeletes;

    /// <summary>
    /// Reordering is the shell's own call and nothing else: Windows has no shortcut for it, so
    /// without the interface there is no way to do it at all.
    /// </summary>
    public static string NoShellMove => S.DockDesktopsNoShellMove;

    /// <summary>What a desktop's gripper says: that it is dragged, and the keys that do the same.</summary>
    public static string Reorder => S.DockDesktopsReorder;

    /// <summary>
    /// What the row for desktop <paramref name="index"/> offers. <paramref name="shell"/> is
    /// whether the shell's own interface can be used here (<c>DesktopControl.Available</c>): with
    /// it any desktop can be renamed, closed and moved, and without it none of the three, except
    /// that the desktop you are on can still be closed with the shortcut Windows itself listens
    /// for. One desktop alone has nowhere to be moved to. A desktop Windows has not told of (none
    /// known, or <paramref name="index"/> past them) has nothing to rename.
    /// </summary>
    public static DesktopActions ActionsFor(int index, int current, int count, bool shell)
    {
        var rename = shell && index >= 0 && index < count;
        var delete = count > 1 && (shell || index == current);
        var move = shell && count > 1;
        return new DesktopActions(
            rename,
            rename ? null : NoShellRename,
            delete,
            delete ? null : count <= 1 ? LastDesktop : OnlyCurrentDeletes,
            move,
            move ? null : !shell ? NoShellMove : LastDesktop);
    }

    /// <summary>
    /// What the desktop switcher's tooltip says: the desktop's whole name, which the button may
    /// have had to cut, and where it stands among the others.
    /// </summary>
    public static string DesktopTooltip(string name, int current, int count) =>
        count > 1 ? $"{name}\n{S.DockDesktopsPosition(number: current + 1, count: count)}" : name;

    /// <summary>
    /// What the layout switcher's tooltip says: the layout's whole name, and — where it is the
    /// only one — why clicking it does nothing, since a button that answers no click has to say
    /// so somewhere.
    /// </summary>
    public static string LayoutTooltip(string name, int layouts) =>
        layouts > 1
            ? $"{name}\n{S.DockSwitcherLayoutTip}"
            : $"{name}\n{S.DockSwitcherOnlyLayout}";
}

/// <summary>
/// What the dock has just read about the virtual desktops, for the widget that draws them.
/// <para>
/// The desktops are one thing per PC, watched by the dock's own <see cref="Desktops"/> (a registry
/// key and a notification), while a widget is made per bar and per folder and cannot be given a
/// constructor argument of the dock's. So the dock puts what it read here, as
/// <c>NotificationStore.Shared</c> and <c>MediaSessions.Shared</c> hold what they read, and the
/// widget takes it from here as it paints. A test sets it directly and needs no registry.
/// </para>
/// </summary>
public static class DesktopState
{
    private static string[] names = [];

    /// <summary>
    /// What the desktops' line reads and acts through: the dock's own desktops, set as the dock
    /// starts; null before, and in a test that gives a line its own (DesktopsLine.Stand).
    /// </summary>
    internal static IDesktops? Service { get; set; }

    /// <summary>The desktops' names, in Windows' order; empty before anything has been read.</summary>
    public static IReadOnlyList<string> Names => names;

    /// <summary>Which of <see cref="Names"/> is the one in use.</summary>
    public static int Current { get; private set; }

    /// <summary>How many desktops there are; one at the least, since a PC always has one.</summary>
    public static int Count => Math.Max(names.Length, 1);

    /// <summary>The name of the desktop in use, as the switcher shows it.</summary>
    public static string Name => Switchers.DesktopName(names, Current);

    /// <summary>What the dock read; true when it differs from what was here.</summary>
    public static bool Set(IReadOnlyList<string> read, int current)
    {
        var fresh = read as string[] ?? [.. read];
        if (current == Current && fresh.AsSpan().SequenceEqual(names))
        {
            return false;
        }
        names = fresh;
        Current = current;
        return true;
    }
}

/// <summary>
/// The virtual desktops as the desktops' line (DesktopsLine) reads and changes them: the dock's
/// (Dock.DesktopsService), or a test's. Every index is a place in Windows' order.
/// </summary>
internal interface IDesktops
{
    /// <summary>The desktops now: each one's name — empty where Windows numbers it — its id and the apps open on it, and which is in use.</summary>
    DesktopStrip Strip();

    /// <summary>Whether the shell's own calls can be made here (<c>DesktopControl.Available</c>): renaming, closing another and moving.</summary>
    bool Shell { get; }

    /// <summary>A new desktop at the end of the order; true where one was made.</summary>
    bool Add();

    /// <summary>Closes desktop <paramref name="index"/>, its windows going to its neighbour, as Windows' own close does.</summary>
    bool Remove(int index);

    /// <summary>Moves desktop <paramref name="from"/> to <paramref name="to"/>, the others making room.</summary>
    bool Move(int from, int to);

    /// <summary>Names desktop <paramref name="index"/>, as Task View does.</summary>
    bool Rename(int index, string name);

    /// <summary>Raised on the dock's thread when what <see cref="Strip"/> reads has changed.</summary>
    event Action? Changed;
}

/// <summary>
/// layouts: the taskbar layout a bar shows, as the layouts face wears it — its name, its own glyph —
/// and how many there are, below two of which there is nothing to switch to.
/// </summary>
internal readonly record struct LayoutShown(string Name, string Glyph, int Count)
{
    /// <summary>Whether there is another layout to switch to.</summary>
    public bool Choosable => Count > 1;

    /// <summary>The layout <paramref name="owner"/>'s bar shows (a layout's, or a display's active one).</summary>
    public static LayoutShown Of(Config config, string? owner)
    {
        var layout = config.LayoutOf(owner);
        return new(
            layout?.Name is { Length: > 0 } name ? name : TaskbarLayouts.DefaultName,
            layout?.Icon is { Length: > 0 } icon ? icon : TaskbarLayouts.LayoutGlyph,
            config.Layouts.Count);
    }
}
