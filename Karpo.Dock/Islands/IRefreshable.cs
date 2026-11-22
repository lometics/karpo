// A line that reads what it shows from somewhere it can read again — a forecast, a price, a feed, a
// scoreboard, an inbox, the calendars, a camera's picture (docs\widgets.md §451). It reads on its own
// clock, and again at once from "Refresh", the last row of its right click (Dock.WidgetMenus.cs,
// WidgetAct.Refresh; the person, 2026-10-31: "the refresh buttons should be removed and they should go
// into the right click context menu"): the row says when it last read under it, and is left out while
// a read is on its way or with nothing to read. A line draws no control of its own for it; what it
// shows was read is said by its stale mark (StaleMark.cs) where that went wrong.

namespace Karpo.Dock;

/// <summary>A widget that reads what it shows again from its menu's "Refresh" (<see cref="WidgetAct.Refresh"/>).</summary>
internal interface IRefreshable
{
    /// <summary>Whether there is anything to read again now — a place, a symbol, a feed, an account; false leaves "Refresh" out.</summary>
    bool CanRefresh { get; }

    /// <summary>Whether a read is on its way: "Refresh" is left out meanwhile.</summary>
    bool Refreshing { get; }

    /// <summary>When what it shows was read, under "Refresh"; null before the first read.</summary>
    DateTime? UpdatedUtc { get; }

    /// <summary>
    /// Why the last read did not bring what it shows up to date, in words for the person; null where
    /// it did. What Karpo's faults list says of the widget (Faults.cs).
    /// </summary>
    string? Problem { get; }

    /// <summary>Reads it again now, however fresh: "Refresh" picked.</summary>
    void RefreshNow();
}
