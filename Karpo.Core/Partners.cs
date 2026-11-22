// Where a widget may be carried (the split, S6; 2026-10-09).
//
// A widget is of the type of the place it stands in: a layout's bar or a list folder. It moves
// anywhere of its own type — along a bar, from one bar to another, within a list and from one list
// folder to another — and nowhere else. A taskbar entry carried into a folder, or a folder's line
// carried onto the bar, is refused, in the person's words: "even though it would technically be
// possible ... it does not work for every widget so i would rather disable it completely"
// (2026-10-09). Nothing lights while a widget is carried over a place of the other type, nothing
// changes when it is let go there, and the place says why in one sentence (WhyNot).
//
// This was a table of partners: one subject as a taskbar entry, a grid folder's card and a list
// folder's line, and the options record that went with it from one type to the next. The
// grid went (2026-10-09) and so did every conversion, and with them the table.

namespace Karpo.Core;

/// <summary>
/// The types a widget can be, one per registry — <see cref="BarEntries"/> and
/// <see cref="ListWidgets"/>. A widget is of the type of the place it stands in: a layout's bar or
/// a list folder.
/// </summary>
public enum WidgetType
{
    /// <summary>An entry of a taskbar (<see cref="BarKind"/>).</summary>
    Bar,

    /// <summary>A line of a list folder (<see cref="ListKind"/>).</summary>
    List,
}

/// <summary>Whether a widget may go to a place, and the sentence that says why not.</summary>
public static class Partners
{
    /// <summary>The type of what a folder of <paramref name="view"/> holds.</summary>
    public static WidgetType TypeOf(FolderView view) => WidgetType.List;

    /// <summary>The type <paramref name="island"/> is: a folder's widget is a list's line, anything else a taskbar entry.</summary>
    public static WidgetType TypeOf(WidgetItem island) => island is CellItem ? WidgetType.List : WidgetType.Bar;

    /// <summary>
    /// Whether <paramref name="island"/> can stand in a place of <paramref name="type"/>: one of its
    /// own type (<see cref="TypeOf(WidgetItem)"/>). What a place a widget is carried over asks
    /// before it lights, and before it takes the widget.
    /// </summary>
    public static bool CanBe(WidgetItem island, WidgetType type) => TypeOf(island) == type;

    /// <summary>
    /// Why <paramref name="island"/> cannot stand in a place of <paramref name="type"/>, in one
    /// sentence for the place it was let go on, or empty where it can
    /// (<see cref="CanBe(WidgetItem, WidgetType)"/>): it names the widget and says that moving
    /// widgets between the taskbar and folders is not supported.
    /// </summary>
    public static string WhyNot(WidgetItem island, WidgetType type)
    {
        if (CanBe(island, type))
        {
            return "";
        }
        var widget = island.Info?.Title ?? S.CorePartnersThisWidget;
        return type == WidgetType.Bar ? S.CorePartnersCantGoBar(widget) : S.CorePartnersCantGoList(widget);
    }

    /// <summary>
    /// <paramref name="island"/>, a widget of <paramref name="from"/>, moved to a place of
    /// <paramref name="to"/>: the widget itself, as it is — a line keeps the height somebody gave
    /// it — where the two are one type, and null where they are not, since nothing moves between
    /// the taskbar and a folder.
    /// </summary>
    public static WidgetItem? Move(WidgetItem island, WidgetType from, WidgetType to) =>
        from == to && CanBe(island, to) ? island : null;
}
