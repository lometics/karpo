// A big time sized to its room, never cut. The clock's flyout writes the time
// large (34 DIP at the text size), and at the largest text on the narrowest
// panel that was wider than the panel and ended "14:4…". The person: "shrink to
// fit". So the size steps down until the time fits — and it is chosen once, for
// the widest the time can read in the person's format (every figure an eight,
// the hour of two figures, the morning's and the evening's marker), so it does
// not change size as the time does; the figures are tabular, so the time does
// not shift each second either.
//
// The clock's header in a folder (TimeHeaderTile) and a clock's line in a list (ClockTile) size
// their time here.

namespace Karpo.Dock;

internal static class TimeFit
{
    /// <summary>
    /// The shapes a time in this region's format can take, with or without
    /// <paramref name="seconds"/>, each as wide as the widest of its kind
    /// (<see cref="ClockTile.TimeShape"/>): the morning's last second and the evening's, which carry
    /// the two markers of a 12-hour clock, and <paramref name="shown"/>, the time on show, in case its
    /// format is not the region's.
    /// </summary>
    public static IReadOnlyList<string> Shapes(bool seconds, string shown) =>
        [.. new[] { Clock.At(11, 59, 59, seconds), Clock.At(23, 59, 59, seconds), shown }
            .Where(time => time.Length > 0)
            .Select(ClockTile.TimeShape)
            .Distinct()];

    /// <summary>
    /// <paramref name="px"/>, or as much smaller as every one of <paramref name="shapes"/> needs to fit
    /// in <paramref name="room"/> px, a whole px at a time; never under <paramref name="least"/>.
    /// <paramref name="width"/> is a shape's width at a size.
    /// </summary>
    public static float Px(float px, float room, float least, IReadOnlyList<string> shapes, Func<string, float, float> width)
    {
        float Widest(float at) => shapes.Count == 0 ? 0 : shapes.Max(shape => width(shape, at));
        var widest = Widest(px);
        if (widest <= room || px <= least)
        {
            return px;
        }
        // Where the widths follow the size, the size that fits is the room's share of it; hinting
        // and rounding can leave that a px over, and the steps down take that back.
        var at = MathF.Max(least, MathF.Min(px - 1, MathF.Floor(px * room / widest)));
        while (at > least && Widest(at) > room)
        {
            at = MathF.Max(least, at - 1);
        }
        return at;
    }
}
