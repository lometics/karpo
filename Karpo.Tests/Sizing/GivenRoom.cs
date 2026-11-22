// The room a test hands the layout for a taskbar entry.
//
// An entry has no size of its own since 2026-10-08 ("remove the ability to resize widgets on the
// taskbar"): the bar reads its kind's range (WidgetBounds.For), and a width put on it is dropped.
// The layout still holds every part to the range it is handed (PartSpec.Room) — a drive is pinned
// to its two cells by its kind, a one-cell button to one — and the tests of the layout and of the
// drawings hold that rule over ranges no kind has: a widget pinned wider than its content, one
// held under it. They say the range here, beside the widget, rather than on it.

using System.Runtime.CompilerServices;
using Karpo.Dock;

namespace Karpo.Tests.Sizing;

internal static class GivenRoom
{
    private static readonly ConditionalWeakTable<BarItem, StrongBox<WidgetBounds>> Rooms = new();

    /// <summary>
    /// <paramref name="widget"/>, to be laid out with <paramref name="least"/> to
    /// <paramref name="most"/> cells (each held to 1 to 32, the fewest winning) rather than its kind's.
    /// </summary>
    public static BarItem Given(this BarItem widget, int least, int most)
    {
        Rooms.AddOrUpdate(widget, new StrongBox<WidgetBounds>(WidgetBounds.Of(least, most, WidgetBounds.Any)));
        return widget;
    }

    /// <summary>The room <paramref name="widget"/> is laid out with: the one a test gave it, else its kind's (<see cref="WidgetBounds.For(WidgetItem)"/>).</summary>
    public static WidgetBounds RoomOf(BarItem widget) =>
        Rooms.TryGetValue(widget, out var room) ? room.Value : WidgetBounds.For(widget);

    /// <summary>The part <paramref name="widget"/> is on a bar, as the dock makes it (<see cref="PartSpec.For"/>), with the room the test gave it.</summary>
    public static PartSpec Part(string key, BarItem widget) => PartSpec.For(key, widget) with { Room = RoomOf(widget) };
}
