// What an event's block in the day and week views says, and at what size: the
// type steps down from Caption as the hours get short, and the title wraps over
// the block's lines before its time and its place get one. Pure but for the
// measuring it is handed, so every rule has a test with a fake measure
// (Karpo.Tests\Calendars\EventTextTests.cs).
//
// The person, on a busy week whose hours came to some 19 px and whose titles were
// cut to two or three letters ("Sta…"): "maybe reduce the font size". So the
// blocks' type is Caption (12) where an hour holds two of its lines, then 11,
// then 10, the floor, below which text stops being read; all at the text size
// the person chose. One size for the whole view, so the blocks read as one
// list and not as a jumble of sizes.
//
// In a block the title comes first: as many lines of it as the block holds,
// broken between words, the last ending in "…" where it still goes on; the time
// and then the place only on the lines the whole title leaves.

namespace Karpo.Dock;

/// <summary>What an event's block says: its type size, the title's lines, and the time and place lines under them.</summary>
/// <param name="Px">The type size, px.</param>
/// <param name="Line">How tall a line is at <paramref name="Px"/>, px.</param>
/// <param name="Title">The title's lines, top to bottom; the last ends cut where the title goes on.</param>
/// <param name="Details">The time, then the place, each on a line, where the title left lines for them.</param>
internal sealed record BlockText(float Px, float Line, IReadOnlyList<string> Title, IReadOnlyList<string> Details)
{
    public static readonly BlockText None = new(0, 0, [], []);
}

internal static class EventText
{
    /// <summary>The block's type sizes, DIP at the normal text size, largest first: Caption, then down to the floor.</summary>
    public static readonly float[] StepsDip = [12, 11, 10];

    /// <summary>The floor: the smallest a block's type gets (<see cref="StepsDip"/>'s last).</summary>
    public static float FloorDip => StepsDip[^1];

    /// <summary>
    /// The view's block type size, px: the largest step whose two lines, with the block's air
    /// (<paramref name="air"/> px above and below them together), fit in an hour of
    /// <paramref name="hour"/> px; the floor where none does. <paramref name="size"/> is a step's px
    /// (its DIP at the scale and the text size), <paramref name="line"/> a line's height at a px.
    /// </summary>
    public static float Px(float hour, float air, Func<float, float> size, Func<float, float> line)
    {
        foreach (var dip in StepsDip)
        {
            var px = size(dip);
            if (2 * line(px) + air <= hour)
            {
                return px;
            }
        }
        return size(FloorDip);
    }

    /// <summary>
    /// What a block <paramref name="room"/> px across and <paramref name="height"/> px tall inside its
    /// air says at <paramref name="px"/> (a line <paramref name="line"/> px): the title over as many
    /// lines as it needs and the block holds (one at the least), then the time and the place on what
    /// is left.
    /// <paramref name="width"/> is a text's width at <paramref name="px"/>, and <paramref name="bold"/>
    /// whether it is the title's weight.
    /// </summary>
    public static BlockText Lay(string title, string time, string? place, float px, float line, float room, float height,
        Func<string, bool, float> width)
    {
        if (room < 4 || line <= 0 || (title.Length == 0 && time.Length == 0))
        {
            return BlockText.None with { Px = px, Line = line };
        }
        // A block shorter than a line still says the first of its title, cut by its edge, as an
        // event of a few minutes is drawn a line tall at the least.
        var lines = Math.Max(1, (int)MathF.Floor(height / line));
        var titleLines = Wrap(title, lines, text => width(text, true) <= room);
        var details = new List<string>();
        // The title whole, and lines to spare: the time, then the place.
        if (titleLines.Count < lines && Whole(titleLines, title, text => width(text, true) <= room))
        {
            foreach (var more in new[] { time, place })
            {
                if (titleLines.Count + details.Count >= lines)
                {
                    break;
                }
                if (more is { Length: > 0 } text)
                {
                    details.Add(text);
                }
            }
        }
        return new BlockText(px, line, titleLines, details);
    }

    /// <summary>
    /// <paramref name="text"/> broken between words over at most <paramref name="most"/> lines, each
    /// as full as <paramref name="fits"/> allows: a word longer than a line has its own, cut where it
    /// is drawn. The last line holds the rest, and is cut with "…" where it is longer.
    /// </summary>
    public static List<string> Wrap(string text, int most, Func<string, bool> fits)
    {
        var lines = new List<string>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var at = 0;
        while (at < words.Length && lines.Count < most)
        {
            if (lines.Count == most - 1)
            {
                lines.Add(string.Join(' ', words[at..]));
                break;
            }
            var line = words[at++];
            while (at < words.Length && fits(line + " " + words[at]))
            {
                line += " " + words[at++];
            }
            lines.Add(line);
        }
        return lines;
    }

    /// <summary>Whether <paramref name="lines"/> say all of <paramref name="text"/>, each line whole.</summary>
    public static bool Whole(IReadOnlyList<string> lines, string text, Func<string, bool> fits) =>
        lines.All(fits)
        && string.Join(' ', lines) == string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
