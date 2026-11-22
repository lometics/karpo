// How many whole items a room holds (docs\widgets.md §9, "Filling the room"; §109, §461).
//
// A line of a list never says what it left out (the person, 2026-10-11: "dont just show x more items,
// let the user scroll down to see all of the other items too"; 2026-10-31: "when a folder has more items
// to show it should actually not say that but just scroll"). A line of items given less than its items
// take is laid out as tall as all of them and scrolls (ListScroll.cs). What is left here is the
// whole-items arithmetic a line still lays a form out by where it does not scroll: as many whole items
// as fit, never half of one, and nothing said about the rest.
//
// Until §461 this also kept a last slot for a row saying how many were not there ("12 other apps",
// "9 more items"), the rule before scrolling; the folder's contents were the last to say it.

namespace Karpo.Dock;

/// <summary>
/// How many of a component's items its room holds, from the first, whole. Pure arithmetic on numbers a
/// component already has, so every kind gets the same answer.
/// </summary>
internal readonly record struct WidgetFill
{
    /// <summary>Half a pixel, so a row that fits exactly is not rounded out of its own room.</summary>
    private const float Tolerance = 0.5f;

    /// <summary>How many whole items are drawn, from the first.</summary>
    public required int Shown { get; init; }

    /// <summary>How many are not drawn; 0 when everything is shown.</summary>
    public required int Left { get; init; }

    /// <summary>The room the drawn rows take, in px.</summary>
    public required float Height { get; init; }

    /// <summary><paramref name="count"/> items, each <paramref name="item"/> px tall, in <paramref name="room"/> px.</summary>
    public static WidgetFill Of(int count, float item, float room)
    {
        count = Math.Max(0, count);
        if (count == 0 || item <= 0 || room <= 0)
        {
            return new() { Shown = 0, Left = count, Height = 0 };
        }
        var shown = Math.Min(count, (int)MathF.Floor((room + Tolerance) / item));
        return new() { Shown = shown, Left = count - shown, Height = shown * item };
    }

    /// <summary>
    /// The same, for items that are not all the same height — a notification of two lines beside one of
    /// four: taken in order, as many as fit whole.
    /// </summary>
    public static WidgetFill Of(ReadOnlySpan<float> items, float room)
    {
        var used = 0f;
        var shown = 0;
        while (room > 0 && shown < items.Length && used + MathF.Max(0, items[shown]) <= room + Tolerance)
        {
            used += MathF.Max(0, items[shown]);
            shown++;
        }
        return new() { Shown = shown, Left = items.Length - shown, Height = used };
    }
}
