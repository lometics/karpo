// What the note's toolbar and its keys do to the Markdown (docs\widgets.md §87): bold, italic,
// struck, code and a link round the selection, and a heading, a list, tasks or a quote on the
// lines it touches. Each toggles, as a word processor's buttons do: on text that is bold already,
// bold takes the marks away; on lines that are all a list already, the list goes. Nothing
// selected, the inline marks go in as a pair with the caret between them, to be typed into. Pure,
// as NoteEdits is: a function of the text, the caret and the selection's other end, so the rules
// are tested without a window, and the editor undoes it as one step.

namespace Karpo.Dock;

/// <summary>What a button of the note's toolbar does. Public only so a test's theory can name one.</summary>
public enum NoteFormat
{
    Heading,
    Bold,
    Italic,
    Strike,
    Code,
    Link,
    Bullets,
    Numbers,
    Tasks,
    Quote,
}

internal static class NoteFormats
{
    /// <summary>
    /// A change to the text: <paramref name="Removed"/> characters at <paramref name="At"/> taken
    /// out and <paramref name="Inserted"/> put in. A place in what was taken out goes to its start,
    /// or to the end of what went in where <paramref name="After"/>; a place at an insertion stays
    /// before it, or goes after it where <paramref name="After"/>.
    /// </summary>
    private readonly record struct Change(int At, int Removed, string Inserted, bool After);

    /// <summary>The marks <paramref name="format"/> writes round its words; empty for a line's.</summary>
    internal static string Mark(NoteFormat format) => format switch
    {
        NoteFormat.Bold => "**",
        NoteFormat.Italic => "*",
        NoteFormat.Strike => "~~",
        NoteFormat.Code => "`",
        _ => "",
    };

    private static MdStyle Style(NoteFormat format) => format switch
    {
        NoteFormat.Bold => MdStyle.Bold,
        NoteFormat.Italic => MdStyle.Italic,
        NoteFormat.Strike => MdStyle.Strike,
        _ => MdStyle.Code,
    };

    public static NoteEdit? Apply(string text, int caret, int anchor, NoteFormat format) =>
        Apply(text, Markdown.Lines(text), caret, anchor, format);

    /// <summary>
    /// <paramref name="format"/> toggled on the selection from <paramref name="anchor"/> to
    /// <paramref name="caret"/> (or at the caret): on where it is off, off where it is on, the
    /// caret and the selection kept on their words. Null where it does nothing (in a code block).
    /// </summary>
    public static NoteEdit? Apply(string text, IReadOnlyList<MdLine> lines, int caret, int anchor, NoteFormat format) => format switch
    {
        NoteFormat.Heading => Heading(text, lines, caret, anchor),
        NoteFormat.Link => Link(text, lines, caret, anchor),
        NoteFormat.Bullets or NoteFormat.Numbers or NoteFormat.Tasks => List(text, lines, caret, anchor, format),
        NoteFormat.Quote => Quote(text, lines, caret, anchor),
        _ => Inline(text, lines, caret, anchor, format),
    };

    /// <summary>Whether <paramref name="format"/> is on where the caret or the selection is: what its button shows pressed, and what pressing it takes away.</summary>
    public static bool IsOn(string text, IReadOnlyList<MdLine> lines, int caret, int anchor, NoteFormat format)
    {
        var low = Math.Min(caret, anchor);
        var high = Math.Max(caret, anchor);
        switch (format)
        {
            case NoteFormat.Heading:
                return Touched(lines, low, high, Headable) is [{ Kind: MdKind.Heading }, ..];
            case NoteFormat.Link:
                return LinkAt(text, lines, low, high) is not null;
            case NoteFormat.Bullets or NoteFormat.Numbers or NoteFormat.Tasks:
                return Touched(lines, low, high, Listable) is { Count: > 0 } items && items.All(line => line.Kind == ListKind(format));
            case NoteFormat.Quote:
                return Touched(lines, low, high, Quotable).Where(line => line.Kind != MdKind.Blank).ToList() is { Count: > 0 } quoted && quoted.All(line => line.Quote > 0);
            default:
                if (low == high && Empty(text, caret, Mark(format)))
                {
                    return true;
                }
                var pieces = Pieces(text, lines, low, high);
                return pieces.Count > 0 && pieces.All(piece => Covering(lines, piece, format) is not null);
        }
    }

    /// <summary>The heading level of the line the caret is on; 0 for none.</summary>
    public static int Level(IReadOnlyList<MdLine> lines, int caret) =>
        lines[Markdown.LineAt(lines, caret)] is { Kind: MdKind.Heading } heading ? heading.Level : 0;

    // --- Inline -------------------------------------------------------------------------------------

    private static NoteEdit? Inline(string text, IReadOnlyList<MdLine> lines, int caret, int anchor, NoteFormat format)
    {
        var mark = Mark(format);
        var low = Math.Min(caret, anchor);
        var high = Math.Max(caret, anchor);
        // Between a pair put in and not typed into yet: the pair goes again.
        if (low == high && Empty(text, caret, mark))
        {
            return new NoteEdit(text.Remove(caret - mark.Length, 2 * mark.Length), caret - mark.Length, caret - mark.Length);
        }
        var pieces = Pieces(text, lines, low, high);
        if (pieces.Count == 0)
        {
            return null;
        }
        var covering = pieces.Select(piece => Covering(lines, piece, format)).ToList();
        var changes = new List<Change>();
        if (covering.All(span => span is not null))
        {
            // On everywhere: off, each run's marks taken away, the words kept.
            foreach (var span in covering.Distinct())
            {
                var run = span!.Value;
                // A ***run*** that is bold and italic both keeps the other's marks.
                var (open, close) = (format, run.Style) switch
                {
                    (NoteFormat.Bold, MdStyle.Bold | MdStyle.Italic) => (2, 2),
                    (NoteFormat.Italic, MdStyle.Bold | MdStyle.Italic) => (1, 1),
                    _ => (run.Open, run.Close),
                };
                changes.Add(new Change(run.Start, open, "", After: false));
                changes.Add(new Change(run.End - close, close, "", After: false));
            }
            return Made(text, changes, caret, anchor);
        }
        if (low == high)
        {
            // Nothing selected: a pair, the caret between, to be typed into (after a list's marker, where the caret was before it).
            var at = pieces[0].From;
            return new NoteEdit(text.Insert(at, mark + mark), at + mark.Length, at + mark.Length);
        }
        for (var index = 0; index < pieces.Count; index++)
        {
            if (covering[index] is null)
            {
                changes.Add(new Change(pieces[index].From, 0, mark, After: true));
                changes.Add(new Change(pieces[index].To, 0, mark, After: false));
            }
        }
        return Made(text, changes, caret, anchor);
    }

    /// <summary>Whether the caret stands between a pair of <paramref name="mark"/> and nothing more of its character either side.</summary>
    private static bool Empty(string text, int caret, string mark) =>
        Run(text, caret, mark[0], back: true) == mark.Length && Run(text, caret, mark[0], back: false) == mark.Length;

    private static int Run(string text, int at, char c, bool back)
    {
        var count = 0;
        while (back ? at - count - 1 >= 0 && text[at - count - 1] == c : at + count < text.Length && text[at + count] == c)
        {
            count++;
        }
        return count;
    }

    /// <summary>
    /// The words the selection covers, line by line, trimmed of the spaces at their ends — marks
    /// stand against words — and not in a code block; the caret alone where nothing is selected.
    /// </summary>
    private static List<(int From, int To, int Line)> Pieces(string text, IReadOnlyList<MdLine> lines, int low, int high)
    {
        var pieces = new List<(int, int, int)>();
        var first = Markdown.LineAt(lines, low);
        var last = Markdown.LineAt(lines, high);
        for (var index = first; index <= last; index++)
        {
            var line = lines[index];
            if (line.Kind is MdKind.Code or MdKind.Fence || (line.Kind == MdKind.Rule && low != high))
            {
                continue;
            }
            if (low == high)
            {
                var at = Math.Max(low, line.Content);
                pieces.Add((at, at, index));
                break;
            }
            var from = Math.Max(low, line.Content);
            var to = Math.Min(high, line.End);
            while (from < to && char.IsWhiteSpace(text[from]))
            {
                from++;
            }
            while (to > from && char.IsWhiteSpace(text[to - 1]))
            {
                to--;
            }
            if (from < to)
            {
                pieces.Add((from, to, index));
            }
        }
        return pieces;
    }

    /// <summary>The smallest run of <paramref name="format"/> a piece stands in: the caret inside its marks, or the words within it, marks and all.</summary>
    private static MdSpan? Covering(IReadOnlyList<MdLine> lines, (int From, int To, int Line) piece, NoteFormat format)
    {
        var style = Style(format);
        MdSpan? best = null;
        foreach (var span in lines[piece.Line].Spans)
        {
            if (!span.Style.HasFlag(style))
            {
                continue;
            }
            var inside = piece.From == piece.To
                ? span.Start < piece.From && piece.From < span.End
                : span.Start <= piece.From && piece.To <= span.End;
            if (inside && (best is not { } was || span.End - span.Start < was.End - was.Start))
            {
                best = span;
            }
        }
        return best;
    }

    // --- A link ---------------------------------------------------------------------------------------

    /// <summary>
    /// Ctrl+K: a [link](…) the caret or the selection is in loses its marks, its words kept.
    /// Elsewhere, nothing selected, "[]()" with the caret in its brackets; words selected, "[words]()"
    /// with the caret in its parentheses for the address; an address selected, "[](address)" with
    /// the caret in the brackets for its words. A selection over more than a line is left alone.
    /// </summary>
    private static NoteEdit? Link(string text, IReadOnlyList<MdLine> lines, int caret, int anchor)
    {
        var low = Math.Min(caret, anchor);
        var high = Math.Max(caret, anchor);
        if (LinkAt(text, lines, low, high) is { } link)
        {
            return Made(text, [new Change(link.Start, link.Open, "", After: false), new Change(link.End - link.Close, link.Close, "", After: false)], caret, anchor);
        }
        var line = lines[Markdown.LineAt(lines, low)];
        if (high > line.End || line.Kind is MdKind.Code or MdKind.Fence)
        {
            return null;
        }
        if (low == high)
        {
            var at = Math.Max(caret, line.Content);
            return new NoteEdit(text.Insert(at, "[]()"), at + 1, at + 1);
        }
        var selected = text[low..high];
        var trimmed = selected.Trim();
        if (Markdown.Opens(trimmed) || (trimmed.StartsWith("www.", StringComparison.OrdinalIgnoreCase) && !trimmed.Contains(' ')))
        {
            var made = text[..low] + "[](" + trimmed + ")" + text[high..];
            return new NoteEdit(made, low + 1, low + 1);
        }
        var wrapped = text[..low] + "[" + selected + "]()" + text[high..];
        var into = high + 3;
        return new NoteEdit(wrapped, into, into);
    }

    /// <summary>The [words](address) the caret is inside or the selection within; null for none (a bare address is not one).</summary>
    private static (int Start, int End, int Open, int Close)? LinkAt(string text, IReadOnlyList<MdLine> lines, int low, int high)
    {
        var line = lines[Markdown.LineAt(lines, low)];
        foreach (var span in line.Spans)
        {
            var bracketed = span.Open > 0 && span.Close > 2 && text[span.Start] == '[' && text[span.End - span.Close] == ']';
            var inside = low == high ? span.Start < low && low < span.End : span.Start <= low && high <= span.End;
            if (bracketed && inside)
            {
                return (span.Start, span.End, span.Open, span.Close);
            }
        }
        return null;
    }

    // --- Lines ----------------------------------------------------------------------------------------

    private static bool Headable(MdLine line, bool alone) => line.Kind is not (MdKind.Code or MdKind.Fence or MdKind.Rule) && (alone || line.Kind != MdKind.Blank);

    private static bool Listable(MdLine line, bool alone) => Headable(line, alone);

    // A blank line between quoted ones is quoted with them, so the quote stays one.
    private static bool Quotable(MdLine line, bool alone) => line.Kind is not (MdKind.Code or MdKind.Fence);

    /// <summary>The lines the selection touches that <paramref name="takes"/>; a blank line only where it is the one line.</summary>
    private static List<MdLine> Touched(IReadOnlyList<MdLine> lines, int low, int high, Func<MdLine, bool, bool> takes)
    {
        var first = Markdown.LineAt(lines, low);
        var last = Markdown.LineAt(lines, high);
        // A selection that ends at the start of a line does not take that line.
        if (last > first && high == lines[last].Start)
        {
            last--;
        }
        var touched = new List<MdLine>();
        for (var index = first; index <= last; index++)
        {
            if (takes(lines[index], first == last))
            {
                touched.Add(lines[index]);
            }
        }
        return touched;
    }

    /// <summary>Where a line's lead to be replaced starts: after its quote marks and its indent.</summary>
    private static int LeadStart(string text, MdLine line)
    {
        var at = NoteEdits.Inside(text, line);
        while (at < line.End && text[at] == ' ')
        {
            at++;
        }
        return Math.Min(at, line.End);
    }

    /// <summary>Where it ends: the words' start for an item or a heading; nothing to replace for a paragraph's line.</summary>
    private static int LeadEnd(string text, MdLine line) =>
        line.IsItem || line.Kind == MdKind.Heading ? Math.Max(LeadStart(text, line), line.Content) : LeadStart(text, line);

    private static MdKind ListKind(NoteFormat format) => format switch
    {
        NoteFormat.Bullets => MdKind.Bullet,
        NoteFormat.Numbers => MdKind.Numbered,
        _ => MdKind.Task,
    };

    /// <summary>
    /// A heading in turn: none, then 1, 2, 3 and none again, as the first line the selection
    /// touches is; every line it touches takes that level, an item's marker going.
    /// </summary>
    private static NoteEdit? Heading(string text, IReadOnlyList<MdLine> lines, int caret, int anchor)
    {
        var touched = Touched(lines, Math.Min(caret, anchor), Math.Max(caret, anchor), Headable);
        if (touched.Count == 0)
        {
            return null;
        }
        var level = touched[0] is { Kind: MdKind.Heading } first ? first.Level switch
        {
            1 => 2,
            2 => 3,
            _ => 0,
        } : 1;
        var lead = level == 0 ? "" : new string('#', level) + " ";
        var changes = touched.Select(line => new Change(LeadStart(text, line), LeadEnd(text, line) - LeadStart(text, line), lead, After: true)).ToList();
        return Made(text, changes, caret, anchor);
    }

    /// <summary>
    /// A bulleted, numbered or task list on the lines the selection touches: off where they all are
    /// one already (their markers gone, their words kept), else each made one, a marker of another
    /// kind or a heading's #s replaced. Numbers count on from a numbered item just above.
    /// </summary>
    private static NoteEdit? List(string text, IReadOnlyList<MdLine> lines, int caret, int anchor, NoteFormat format)
    {
        var low = Math.Min(caret, anchor);
        var high = Math.Max(caret, anchor);
        var touched = Touched(lines, low, high, Listable);
        if (touched.Count == 0)
        {
            return null;
        }
        var kind = ListKind(format);
        var changes = new List<Change>();
        if (touched.All(line => line.Kind == kind))
        {
            foreach (var line in touched)
            {
                changes.Add(new Change(LeadStart(text, line), line.Content - LeadStart(text, line), "", After: true));
            }
            return Made(text, changes, caret, anchor);
        }
        // Each depth counts on its own; a shallower item starts the deeper ones again.
        var numbers = new Dictionary<int, int>();
        var above = Markdown.LineAt(lines, touched[0].Start) - 1;
        if (above >= 0 && lines[above] is { Kind: MdKind.Numbered } before && int.TryParse(before.Marker[..^1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var was))
        {
            numbers[Depth(text, before)] = was;
        }
        foreach (var line in touched)
        {
            if (line.Kind == kind)
            {
                continue;
            }
            var depth = Depth(text, line);
            foreach (var deeper in numbers.Keys.Where(key => key > depth).ToList())
            {
                numbers.Remove(deeper);
            }
            var start = LeadStart(text, line);
            string marker;
            if (format == NoteFormat.Numbers)
            {
                numbers[depth] = numbers.GetValueOrDefault(depth) + 1;
                marker = numbers[depth].ToString(System.Globalization.CultureInfo.InvariantCulture) + ". ";
            }
            else
            {
                // A bullet keeps its own character, and becomes a task by its box.
                var bullet = line.Kind is MdKind.Bullet or MdKind.Task ? text[start] : '-';
                marker = format == NoteFormat.Tasks ? $"{bullet} [ ] " : $"{bullet} ";
            }
            changes.Add(new Change(start, LeadEnd(text, line) - start, marker, After: true));
        }
        return Made(text, changes, caret, anchor);
    }

    private static int Depth(string text, MdLine line) => (LeadStart(text, line) - NoteEdits.Inside(text, line)) / Markdown.IndentStep;

    /// <summary>A quote on the lines the selection touches: off where they all are quoted (one level of "&gt;" taken away), else "&gt; " before each line that is not.</summary>
    private static NoteEdit? Quote(string text, IReadOnlyList<MdLine> lines, int caret, int anchor)
    {
        var low = Math.Min(caret, anchor);
        var high = Math.Max(caret, anchor);
        var touched = Touched(lines, low, high, Quotable);
        if (touched.Count == 0)
        {
            return null;
        }
        var worded = touched.Where(line => line.Kind != MdKind.Blank).ToList();
        var changes = new List<Change>();
        if (worded.Count > 0 && worded.All(line => line.Quote > 0))
        {
            foreach (var line in worded)
            {
                var mark = text.IndexOf('>', line.Start, line.End - line.Start);
                var end = mark + 1 < line.End && text[mark + 1] == ' ' ? mark + 2 : mark + 1;
                changes.Add(new Change(line.Start, end - line.Start, "", After: false));
            }
            return Made(text, changes, caret, anchor);
        }
        foreach (var line in touched)
        {
            if (line.Quote == 0)
            {
                changes.Add(new Change(line.Start, 0, "> ", After: true));
            }
        }
        return Made(text, changes, caret, anchor);
    }

    // --- Made ---------------------------------------------------------------------------------------

    /// <summary>The text with <paramref name="changes"/> made, from the last back, and the caret and the other end moved with them.</summary>
    private static NoteEdit? Made(string text, List<Change> changes, int caret, int anchor)
    {
        if (changes.Count == 0)
        {
            return null;
        }
        // From the end, so each change's place still stands; at one place, the later in the list first.
        var ordered = changes.Select((change, order) => (change, order)).OrderByDescending(each => each.change.At).ThenByDescending(each => each.order).Select(each => each.change);
        var made = new System.Text.StringBuilder(text);
        foreach (var change in ordered)
        {
            made.Remove(change.At, change.Removed);
            made.Insert(change.At, change.Inserted);
            caret = Moved(caret, change);
            anchor = Moved(anchor, change);
        }
        return new NoteEdit(made.ToString(), caret, anchor);
    }

    private static int Moved(int place, Change change)
    {
        if (place < change.At)
        {
            return place;
        }
        if (place > change.At + change.Removed)
        {
            return place - change.Removed + change.Inserted.Length;
        }
        return change.After ? change.At + change.Inserted.Length : change.At;
    }
}
