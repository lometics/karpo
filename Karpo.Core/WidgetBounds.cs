// How much room a taskbar entry may be given along the bar: two numbers, its kind's.
//
// **Nobody sets them** (2026-10-08: "remove the ability to resize widgets on the taskbar").
// A taskbar entry takes the size it needs: its kind's range (DefaultFor), held round what its
// settings and its templates measure (WidgetShapes) — never what it shows now, so nothing moves as
// its value changes. An entry carries no numbers, and a file that still has them has them dropped
// where it is read (WidgetItem.Normalize).
//
// Between the two ends, the content decides:
//
//   given = bounds.Clamp(natural)
//
// The natural size is not stored: it is a measurement of the content, taken every time it is
// needed (CellIsland.Content). What the bar cannot hold, it drops rather than squeezes
// (Layout.NotShown): a widget given less than its fewest is not a smaller widget, it is an
// unreadable one, and the edit rail says how many are not shown.
//
// What this type is NOT: an appetite. Bounds say what a widget may be **given**; they say nothing
// about whether it **asks**. A widget with a maximum of eight does not want eight cells, it accepts
// up to eight. Folding an appetite into an unbounded maximum would quietly change who wins the room
// nobody claimed (Layout.AllotCells), so this type stays a clamp.
//
// It used to be a folder widget's height too, as a least and a most a person set (1 to 32 rows,
// the content deciding between them). That is LineHeight now — automatic up to a most, or fixed —
// and this type is the bar's alone. A list kind's measurement (ListWidget.Range) is still a pair:
// its most is where a new line's automatic height starts (LineHeight.StartFor).

namespace Karpo.Core;

/// <summary>
/// The range of cells a widget may be given on a surface: the fewest and the most, each from 1 to
/// <see cref="WidgetItem.MostCells"/>, the most never below the fewest.
/// </summary>
/// <param name="Least">The fewest cells; a widget that cannot have this is dropped rather than squeezed.</param>
/// <param name="Most">The most, which is never below <paramref name="Least"/>.</param>
public readonly record struct WidgetBounds(int Least, int Most)
{
    /// <summary>Whether <paramref name="cells"/> is a size this widget may be given.</summary>
    public bool Allows(int cells) => cells >= Least && (Open || cells <= Most);

    /// <summary>
    /// The nearest size to <paramref name="cells"/> that this widget may be given; past its most only
    /// where its most is the most there is (<see cref="Open"/>).
    /// </summary>
    public int Clamp(int cells) => Open ? Math.Max(cells, Least) : Math.Clamp(cells, Least, Most);

    /// <summary>
    /// Whether its most is the most there is (<see cref="WidgetItem.MostCells"/>), which holds nothing
    /// back: a widget whose content is its measure — the open apps' buttons, the status icons, a
    /// folder's faces — is as wide as that content, however wide, and the bar's own room is all that
    /// stops it (Layout's give-way). A fixed width is still that width.
    /// </summary>
    public bool Open => Most >= WidgetItem.MostCells && !IsFixed;

    /// <summary>Whether the two ends meet: the widget is exactly this many cells, whatever its content.</summary>
    public bool IsFixed => Least == Most;

    /// <summary>Every size a widget may be given: one cell to <see cref="WidgetItem.MostCells"/>.</summary>
    public static WidgetBounds Any => new(WidgetItem.MinWidth, WidgetItem.MostCells);

    /// <summary>
    /// The range a size said as one number means: exactly that many cells, or <see cref="Any"/>
    /// for <see cref="WidgetItem.AutomaticWidth"/> — which is what a number below
    /// <see cref="WidgetItem.MinWidth"/> has always meant. The mirror of <see cref="WidgetItem.Cells"/>, so
    /// a size read out as a number and put back as a range comes out the same size.
    /// </summary>
    public static WidgetBounds Chosen(int cells) =>
        cells < WidgetItem.MinWidth ? Any : Of(cells, cells, Any);

    /// <summary>
    /// The room a taskbar entry of <paramref name="kind"/> has along the bar
    /// (<see cref="BarEntry.Size"/>): the width its kind is where it has one, exactly;
    /// otherwise its fewest cells to its most, the content deciding in between. Also where a
    /// folder's widget of a taskbar kind starts.
    /// </summary>
    public static WidgetBounds DefaultFor(BarKind kind)
    {
        var size = BarEntries.Find(kind)?.Size;
        if (size is { } known && known.DefaultCells >= WidgetItem.MinWidth)
        {
            return Of(known.DefaultCells, known.DefaultCells, Any);
        }
        return Of(size?.MinCells ?? WidgetItem.MinWidth, size?.MaxCells ?? WidgetItem.MaxWidth, Any);
    }

    /// <summary>
    /// The room <paramref name="entry"/> may be given along the bar: its kind's
    /// (<see cref="DefaultFor"/>). An entry takes the size it needs and has none of its own to set;
    /// a folder's widget has a height instead (<see cref="LineHeight"/>).
    /// </summary>
    public static WidgetBounds For(BarItem entry) => DefaultFor(entry.Kind);

    /// <summary>
    /// Two ends, either missing, held to the sizes that exist: a missing end is
    /// <paramref name="fallback"/>'s, each end is 1 to <see cref="WidgetItem.MostCells"/>, and where
    /// the two disagree the fewest wins — it is the promise that the widget can be read at all, and
    /// a maximum is only a preference about room. Written out rather than inlined so the tests can
    /// hold the rule itself and not one caller's use of it.
    /// </summary>
    public static WidgetBounds Of(int? atLeast, int? atMost, WidgetBounds fallback)
    {
        var least = Math.Clamp(atLeast ?? fallback.Least, WidgetItem.MinWidth, WidgetItem.MostCells);
        var most = Math.Clamp(atMost ?? fallback.Most, WidgetItem.MinWidth, WidgetItem.MostCells);
        return new WidgetBounds(least, Math.Max(least, most));
    }
}
