// How tall a component's content is, which a list folder's line follows (docs\widgets.md §12.3).
//
// A list folder gives each line the bar's rule measured downwards: what its content works out to,
// held inside what the line may be given — the person's least and most, in rows
// (WidgetBounds.For on WidgetType.List). Two of those three are numbers anybody can look up. The third, the content's own height, is a measurement, and only the widget can take
// it: a list of notifications is as tall as the notifications it holds, at the width it is drawn,
// in the text size the person reads at.
//
// So this is the **one seam** a component reports it through, and the reason there is one rather
// than five is the same reason there is one WidgetFill: a number a folder reads from five places
// that each decided for themselves what "natural" means will be five different things by the
// second change. It is asked by whoever has the widgets and a device — the open folder — and handed
// to FolderFlyout.Build as a list of px, because Build takes no device by design and must not start
// (the picture, the test and the live panel agree about geometry because none of them measure).
//
// **What the number is.** The height of the whole line this card needs to show everything it has,
// in px at the style's scale, the card's own padding included: exactly the height at which it
// would draw its content with nothing summarised and no room left over. Not rounded to rows — the
// list rounds a person's range to rows, never the content, so a card whose content is a row and a
// half is given a row and a half, and a range adds no room the content did not ask for.
//
// **What it must not be.** A function of the room it was last given. A card that reports "as tall
// as I am now" can only ever keep its height, and one that reports its height *after* summarising
// reports less than it needs and is summarised for ever. It is what the content would take with
// nothing in the way.
//
// **Zero means it cannot say**, and the line keeps what its kind declares (Folders.ListRowsOf) —
// which is what every line was before a range existed. A card whose content never changes height
// needs nothing here: its kind's declared rows are already its natural height.

namespace Karpo.Dock;

/// <summary>
/// A card whose content has a height of its own, which a list folder's line follows. The one place
/// a component says what "natural" means for it.
/// </summary>
internal interface IListContent
{
    /// <summary>
    /// How tall a list line <paramref name="width"/> px wide has to be for this card to show all it
    /// has, in px at <paramref name="style"/>'s scale, its own padding included; 0 where it cannot
    /// say, which leaves the line at what its kind declares.
    /// </summary>
    float ListHeight(float width, in TileStyle style);
}
