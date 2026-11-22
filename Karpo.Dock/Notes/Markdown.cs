// A note's Markdown, read line by line for the note's live editor (docs\widgets.md §87). Pure: no
// device, no drawing — NoteLayout lays the lines out with DirectWrite and the note line draws them.
//
// The editor shows every line of the text as a line on screen, every character of it, marks and all
// (§321, the person: "the notes widget should always show the markup elements too, not only when i
// am on the line"): the marks faint, the words they mark drawn as what they mean — a heading big, bold
// bold, a link underlined, a task's box in the accent. One rule wherever the caret is, so a shown
// line is its text and a place in it is a place in the note (MdShown).
//
// What is read is the CommonMark a person writes in a note, and GitHub's few additions to it:
//
//   lines    headings (# to ######), bulleted lists (- * +) and numbered ones (1. or 1)), nested
//            by indenting two spaces, tasks (- [ ] and - [x]), quotes (>, which may hold any of
//            these), fenced code (``` or ~~~), a rule (---, ***, ___), and paragraphs' lines.
//   inline   **bold** and __bold__, *italic* and _italic_, ***both***, ~~struck~~, `code`,
//            [links](https://…), <https://…>, bare https:// and www. addresses, and \ before a
//            punctuation mark to write the mark itself. An image is its words.
//
// What is not: setext headings (a line of --- is a rule), indented code (four spaces is a nested
// list's indent far more often in a note), tables, HTML, footnotes, reference links. They read as
// the text they are.
//
// Every task's box keeps where its "[ ]" stands in the note, so a click on the box ticks the note
// itself (Toggled), and every link its address, which only http, https and mailto may be: a note's
// link never starts a program.

namespace Karpo.Dock;

/// <summary>What a line of a note is.</summary>
internal enum MdKind
{
    Blank,
    Paragraph,
    Heading,
    Bullet,
    Numbered,
    Task,

    /// <summary>A code block's ``` or ~~~, opening or closing it.</summary>
    Fence,

    /// <summary>A line inside a fenced code block, kept as it is.</summary>
    Code,
    Rule,
}

/// <summary>How a run of shown text is drawn; several at once.</summary>
[Flags]
internal enum MdStyle
{
    None = 0,
    Bold = 1,
    Italic = 2,
    Code = 4,
    Strike = 8,
    Link = 16,

    /// <summary>A Markdown mark — a heading's #s, a list's marker, **, a link's address: faint.</summary>
    Mark = 32,

    /// <summary>A task's "[ ]" or "[x]": in the accent, as something a click ticks.</summary>
    Box = 64,
}

/// <summary>
/// A run of inline Markdown in the note's text: [<paramref name="Start"/>, <paramref name="End"/>)
/// all of it, its first <paramref name="Open"/> characters the opening marks and its last
/// <paramref name="Close"/> the closing ones (a link's "](address)"); what is between is drawn in
/// <paramref name="Style"/>, and goes to <paramref name="Link"/>.
/// </summary>
internal readonly record struct MdSpan(int Start, int End, int Open, int Close, MdStyle Style, string? Link = null);

/// <summary>
/// One line of a note, where it stands in the note's text — [<paramref name="Start"/>,
/// <paramref name="End"/>), its \n not included — and what it is.
/// </summary>
/// <param name="Content">Where its words start: after its quote marks, its list marker and box, its heading's #s. Before it is its lead.</param>
/// <param name="Level">A heading's level, 1 to 6; a list item's depth, 0 for the outermost.</param>
/// <param name="Quote">How many quotes it stands in.</param>
/// <param name="Marker">A numbered item's number with its dot ("3."), as written; empty otherwise.</param>
/// <param name="Checked">A task's tick.</param>
/// <param name="Box">A task's "[" in the note's text, where a click ticks it; -1 for anything else.</param>
internal sealed record MdLine(
    MdKind Kind,
    int Start,
    int End,
    int Content,
    IReadOnlyList<MdSpan> Spans,
    int Level = 0,
    int Quote = 0,
    string Marker = "",
    bool Checked = false,
    int Box = -1)
{
    public bool IsItem => Kind is MdKind.Bullet or MdKind.Numbered or MdKind.Task;
}

/// <summary>A run of a shown line's text, [<paramref name="Start"/>, Start + <paramref name="Length"/>), drawn in <paramref name="Style"/>.</summary>
internal readonly record struct MdRun(int Start, int Length, MdStyle Style, string? Link = null);

/// <summary>A line as it is shown: its text, every character of it, and its runs.</summary>
internal sealed class MdShown
{
    public required MdLine Line { get; init; }

    /// <summary>What is drawn: the line's text as it stands in the note.</summary>
    public required string Text { get; init; }

    public required IReadOnlyList<MdRun> Runs { get; init; }

    /// <summary>Where a place in the note (the caret) stands in the shown text.</summary>
    public int ToShown(int source) => Math.Clamp(source - Line.Start, 0, Text.Length);

    /// <summary>Where a place in the shown text (a click) stands in the note.</summary>
    public int ToSource(int shown) => Line.Start + Math.Clamp(shown, 0, Text.Length);
}

internal static class Markdown
{
    /// <summary>The most a list nests, as drawn: deeper items stand at this depth.</summary>
    internal const int MostDepth = 4;

    /// <summary>The spaces a list item is indented by for each level.</summary>
    internal const int IndentStep = 2;

    /// <summary>The lines of <paramref name="note"/>, in order: as many as it has \n and one.</summary>
    public static IReadOnlyList<MdLine> Lines(string? note)
    {
        var text = note ?? "";
        var lines = new List<MdLine>();
        string? fence = null;
        var start = 0;
        while (true)
        {
            var end = text.IndexOf('\n', start);
            if (end < 0)
            {
                end = text.Length;
            }
            lines.Add(Line(text, start, end, ref fence));
            if (end >= text.Length)
            {
                break;
            }
            start = end + 1;
        }
        return lines;
    }

    /// <summary>The line of <paramref name="lines"/> that <paramref name="at"/> (a place in the note) is on.</summary>
    public static int LineAt(IReadOnlyList<MdLine> lines, int at)
    {
        var low = 0;
        var high = lines.Count - 1;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (lines[middle].Start <= at)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }
        return Math.Max(0, low);
    }

    /// <summary>
    /// <paramref name="note"/> with the task box at <paramref name="box"/> (its "[") ticked or cleared;
    /// the note as it is where no box stands there.
    /// </summary>
    public static string Toggled(string note, int box)
    {
        if (box < 0 || box + 2 >= note.Length || note[box] != '[' || note[box + 2] != ']')
        {
            return note;
        }
        var mark = note[box + 1] is 'x' or 'X' ? ' ' : 'x';
        return string.Concat(note.AsSpan(0, box + 1), mark.ToString(), note.AsSpan(box + 2));
    }

    /// <summary>Whether <paramref name="link"/> is one a click may open: http, https or mailto, and nothing that starts a program.</summary>
    public static bool Opens(string? link) =>
        link is { Length: > 0 and < 2048 } && Uri.TryCreate(link, UriKind.Absolute, out var url)
        && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeMailto)
        && !url.IsFile && !url.IsUnc;

    // --- Shown ----------------------------------------------------------------------------------------

    /// <summary>
    /// <paramref name="line"/> of <paramref name="note"/> as shown, wherever the caret is: every
    /// character, the marks faint, a task's box in the accent and its words struck once ticked, and
    /// what the marks mark styled.
    /// </summary>
    public static MdShown Show(string note, MdLine line)
    {
        var length = line.End - line.Start;
        var styles = new MdStyle[length];
        var links = new string?[length];
        switch (line.Kind)
        {
            case MdKind.Code:
                Array.Fill(styles, MdStyle.Code);
                break;
            case MdKind.Fence:
                Array.Fill(styles, MdStyle.Code | MdStyle.Mark);
                break;
            case MdKind.Rule:
                Array.Fill(styles, MdStyle.Mark);
                break;
            default:
                for (var at = line.Start; at < line.Content; at++)
                {
                    styles[at - line.Start] = line.Box >= 0 && at >= line.Box && at < line.Box + 3 ? MdStyle.Box : MdStyle.Mark;
                }
                if (line is { Kind: MdKind.Task, Checked: true })
                {
                    // A ticked task's words are struck through, as a to-do list's are.
                    Array.Fill(styles, MdStyle.Strike, line.Content - line.Start, line.End - line.Content);
                }
                foreach (var span in line.Spans)
                {
                    for (var at = span.Start; at < span.End; at++)
                    {
                        var index = at - line.Start;
                        if (at < span.Start + span.Open || at >= span.End - span.Close)
                        {
                            styles[index] |= MdStyle.Mark;
                        }
                        else
                        {
                            styles[index] |= span.Style;
                            links[index] ??= span.Link;
                        }
                    }
                }
                break;
        }
        var runs = new List<MdRun>();
        for (var index = 0; index < length; index++)
        {
            var style = styles[index];
            var link = links[index];
            if (style.HasFlag(MdStyle.Mark) && line.Kind is not (MdKind.Fence or MdKind.Code))
            {
                // A mark is faint and keeps only its words' weight and slant: a link's address is not underlined.
                style &= MdStyle.Bold | MdStyle.Italic | MdStyle.Mark;
                link = null;
            }
            if (style != MdStyle.None)
            {
                if (runs.Count > 0 && runs[^1] is var last && last.Start + last.Length == index && last.Style == style && last.Link == link)
                {
                    runs[^1] = last with { Length = last.Length + 1 };
                }
                else
                {
                    runs.Add(new MdRun(index, 1, style, link));
                }
            }
        }
        return new MdShown { Line = line, Text = note[line.Start..line.End], Runs = runs };
    }

    // --- Lines --------------------------------------------------------------------------------------

    private static MdLine Line(string text, int start, int end, ref string? fence)
    {
        var line = text.AsSpan(start, end - start);
        if (fence is not null)
        {
            var trimmed = line.TrimStart(' ');
            if (line.Length - trimmed.Length <= 3 && trimmed.StartsWith(fence, StringComparison.Ordinal) && trimmed.TrimEnd().Trim(fence[0]).Length == 0)
            {
                fence = null;
                return new MdLine(MdKind.Fence, start, end, end, []);
            }
            return new MdLine(MdKind.Code, start, end, start, []);
        }
        if (line.IsWhiteSpace())
        {
            return new MdLine(MdKind.Blank, start, end, start, []);
        }
        // The quote marks: ">" and a space after each, up to three spaces before each.
        var at = 0;
        var quote = 0;
        while (true)
        {
            var spaces = Spaces(line, at);
            if (spaces > 3 || at + spaces >= line.Length || line[at + spaces] != '>')
            {
                break;
            }
            at += spaces + 1;
            quote++;
            if (at < line.Length && line[at] == ' ')
            {
                at++;
            }
        }
        var indent = Spaces(line, at);
        var body = line[(at + indent)..];
        if (quote == 0 && indent <= 3 && Fence(body) is { } opened)
        {
            fence = opened;
            return new MdLine(MdKind.Fence, start, end, end, []);
        }
        if (indent <= 3 && IsRule(body))
        {
            return new MdLine(MdKind.Rule, start, end, end, [], Quote: quote);
        }
        if (indent <= 3 && Heading(body) is var level and > 0)
        {
            var content = at + indent + level;
            content += Spaces(line, content);
            return new MdLine(MdKind.Heading, start, end, start + content, Inline(text, start + content, end), level, quote);
        }
        if (Item(line, at + indent) is { } item)
        {
            var depth = Math.Min(MostDepth, indent / IndentStep);
            var content = start + item.Content;
            var spans = Inline(text, content, end);
            if (item.Number is { } number)
            {
                return new MdLine(MdKind.Numbered, start, end, content, spans, depth, quote, number);
            }
            return item.Box >= 0
                ? new MdLine(MdKind.Task, start, end, content, spans, depth, quote, Checked: item.Checked, Box: start + item.Box)
                : new MdLine(MdKind.Bullet, start, end, content, spans, depth, quote);
        }
        // A paragraph's line: in a quote its words start after the marks, else its own spaces are its words'.
        var words = quote > 0 ? start + at : start;
        return new MdLine(MdKind.Paragraph, start, end, words, Inline(text, words, end), Quote: quote);
    }

    private static int Spaces(ReadOnlySpan<char> line, int at)
    {
        var count = 0;
        while (at + count < line.Length && line[at + count] == ' ')
        {
            count++;
        }
        return count;
    }

    private static string? Fence(ReadOnlySpan<char> body)
    {
        foreach (var mark in "`~")
        {
            var run = 0;
            while (run < body.Length && body[run] == mark)
            {
                run++;
            }
            if (run >= 3 && (mark == '~' || !body[run..].Contains('`')))
            {
                return new string(mark, run);
            }
        }
        return null;
    }

    internal static bool IsRule(ReadOnlySpan<char> body)
    {
        var count = 0;
        var first = '\0';
        foreach (var c in body)
        {
            if (c == ' ')
            {
                continue;
            }
            if (c is not ('-' or '*' or '_') || (first != '\0' && c != first))
            {
                return false;
            }
            first = c;
            count++;
        }
        return count >= 3;
    }

    /// <summary>A heading's level: its #s, one to six, followed by a space or nothing; 0 for no heading.</summary>
    private static int Heading(ReadOnlySpan<char> body)
    {
        var level = 0;
        while (level < body.Length && body[level] == '#')
        {
            level++;
        }
        return level is < 1 or > 6 || (level < body.Length && body[level] != ' ') ? 0 : level;
    }

    /// <summary>A list item's marker read at <paramref name="at"/>: its number as written where it has one, its task box and where its words start, in the line.</summary>
    private readonly record struct ListItem(string? Number, int Box, bool Checked, int Content);

    private static ListItem? Item(ReadOnlySpan<char> line, int at)
    {
        if (at >= line.Length)
        {
            return null;
        }
        var marker = at;
        string? number = null;
        if (line[at] is '-' or '*' or '+')
        {
            at++;
        }
        else
        {
            var digits = at;
            while (digits < line.Length && char.IsAsciiDigit(line[digits]) && digits - at < 9)
            {
                digits++;
            }
            if (digits == at || digits >= line.Length || line[digits] is not ('.' or ')'))
            {
                return null;
            }
            number = line[at..(digits + 1)].ToString();
            at = digits + 1;
        }
        // The marker is followed by a space, or ends the line.
        if (at < line.Length && line[at] != ' ')
        {
            return null;
        }
        if (number is null && IsRule(line[marker..]))
        {
            return null;
        }
        at += Spaces(line, at);
        var box = -1;
        var ticked = false;
        if (number is null && at + 2 < line.Length && line[at] == '[' && line[at + 2] == ']'
            && line[at + 1] is ' ' or 'x' or 'X' && (at + 3 == line.Length || line[at + 3] == ' '))
        {
            box = at;
            ticked = line[at + 1] != ' ';
            at += 3;
            at += Spaces(line, at);
        }
        return new ListItem(number, box, ticked, at);
    }

    // --- Inline -------------------------------------------------------------------------------------

    /// <summary>The inline Markdown of <paramref name="text"/> from <paramref name="from"/> to <paramref name="to"/>, nested runs after the runs they stand in.</summary>
    internal static IReadOnlyList<MdSpan> Inline(string text, int from, int to)
    {
        var spans = new List<MdSpan>();
        Inline(text, from, to, null, spans);
        return spans;
    }

    private static void Inline(string source, int from, int to, string? link, List<MdSpan> spans)
    {
        var index = from;
        while (index < to)
        {
            var c = source[index];
            // A backslash before punctuation writes the mark itself: the backslash is a mark.
            if (c == '\\' && index + 1 < to && !char.IsAsciiLetterOrDigit(source[index + 1]) && !char.IsWhiteSpace(source[index + 1]))
            {
                spans.Add(new MdSpan(index, index + 2, 1, 0, MdStyle.None));
                index += 2;
                continue;
            }
            if (c == '`')
            {
                var run = Run(source, index, to, '`');
                var close = FindRun(source, index + run, to, '`', run);
                if (close >= 0)
                {
                    spans.Add(new MdSpan(index, close + run, run, run, MdStyle.Code));
                    index = close + run;
                    continue;
                }
                index += run;
                continue;
            }
            // <https://…> and <mailto:…>
            if (c == '<' && link is null && source.IndexOf('>', index + 1, to - index - 1) is var end and > 0)
            {
                var inside = source[(index + 1)..end];
                if (!inside.Contains(' ') && Opens(inside))
                {
                    var mailto = inside.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ? 7 : 0;
                    spans.Add(new MdSpan(index, end + 1, 1 + mailto, 1, MdStyle.Link, inside));
                    index = end + 1;
                    continue;
                }
            }
            // [words](address) and ![words](address), whose words are all of an image there is.
            if ((c == '[' || (c == '!' && index + 1 < to && source[index + 1] == '[')) && link is null)
            {
                var open = c == '!' ? index + 1 : index;
                if (Bracketed(source, open, to) is { } closed && closed + 1 < to && source[closed + 1] == '(' && Shut(source, closed + 1, to) is { } shut)
                {
                    var address = source[(closed + 2)..shut].Trim();
                    // A title after the address, in quotes, is not part of it.
                    if (address.IndexOf(' ') is var space and > 0)
                    {
                        address = address[..space];
                    }
                    address = address.Trim('<', '>');
                    var target = c == '!' || !Opens(address) ? null : address;
                    spans.Add(new MdSpan(index, shut + 1, open + 1 - index, shut + 1 - closed, target is null ? MdStyle.None : MdStyle.Link, target));
                    Inline(source, open + 1, closed, target ?? "", spans);
                    index = shut + 1;
                    continue;
                }
            }
            // https://… and www.… as they stand in the text.
            if (link is null && (c is 'h' or 'w') && (index == from || !char.IsLetterOrDigit(source[index - 1])) && Bare(source, index, to) is { } bare)
            {
                spans.Add(new MdSpan(index, index + bare.Length, 0, 0, MdStyle.Link, bare.Address));
                index += bare.Length;
                continue;
            }
            // Emphasis: ***, **, __, *, _ and ~~, matched by the same run further on.
            if (c is '*' or '_' or '~')
            {
                var run = Run(source, index, to, c);
                if (c == '~' && run != 2)
                {
                    index += run;
                    continue;
                }
                var take = Math.Min(run, 3);
                var opens = index + run < to && !char.IsWhiteSpace(source[index + run])
                    && (c != '_' || index == from || !char.IsLetterOrDigit(source[index - 1]));
                if (opens)
                {
                    var close = Closer(source, index + run, to, c, take);
                    // A run longer than its closer: as much of it as the closer matches, the rest plain.
                    while (close < 0 && take > 1 && c != '~')
                    {
                        take--;
                        close = Closer(source, index + run, to, c, take);
                    }
                    if (close > index + run - 1)
                    {
                        var added = c == '~' ? MdStyle.Strike : take switch
                        {
                            1 => MdStyle.Italic,
                            2 => MdStyle.Bold,
                            _ => MdStyle.Bold | MdStyle.Italic,
                        };
                        var opening = index + run - take;
                        spans.Add(new MdSpan(opening, close + take, take, take, added));
                        Inline(source, index + run, close, link, spans);
                        index = close + take;
                        continue;
                    }
                }
                index += run;
                continue;
            }
            index++;
        }
    }

    private static int Run(string source, int at, int to, char mark)
    {
        var end = at;
        while (end < to && source[end] == mark)
        {
            end++;
        }
        return end - at;
    }

    /// <summary>Where a run of exactly <paramref name="length"/> <paramref name="mark"/>s starts after <paramref name="from"/>; -1 for none.</summary>
    private static int FindRun(string source, int from, int to, char mark, int length)
    {
        var at = from;
        while (at < to)
        {
            if (source[at] == mark)
            {
                var run = Run(source, at, to, mark);
                if (run == length)
                {
                    return at;
                }
                at += run;
                continue;
            }
            at++;
        }
        return -1;
    }

    /// <summary>
    /// Where emphasis opened by <paramref name="length"/> <paramref name="mark"/>s closes: a run at
    /// least that long after a character that is not a space (and for _ not before a letter), past
    /// code spans; -1 for none.
    /// </summary>
    private static int Closer(string source, int from, int to, char mark, int length)
    {
        var at = from;
        while (at < to)
        {
            var c = source[at];
            if (c == '\\')
            {
                at += 2;
                continue;
            }
            if (c == '`')
            {
                var run = Run(source, at, to, '`');
                var close = FindRun(source, at + run, to, '`', run);
                at = close >= 0 ? close + run : at + run;
                continue;
            }
            if (c == mark)
            {
                var run = Run(source, at, to, mark);
                var fits = mark == '~' ? run == length : run >= length;
                if (fits && at > from && !char.IsWhiteSpace(source[at - 1])
                    && (mark != '_' || at + run >= to || !char.IsLetterOrDigit(source[at + run])))
                {
                    // The closer is the run's last marks, so "**bold***" closes the bold at its end.
                    return at + run - length;
                }
                at += run;
                continue;
            }
            at++;
        }
        return -1;
    }

    /// <summary>The "]" that closes the "[" at <paramref name="open"/>, brackets inside it counted; null for none.</summary>
    private static int? Bracketed(string source, int open, int to)
    {
        var depth = 0;
        for (var at = open; at < to; at++)
        {
            switch (source[at])
            {
                case '\\':
                    at++;
                    break;
                case '[':
                    depth++;
                    break;
                case ']':
                    depth--;
                    if (depth == 0)
                    {
                        return at;
                    }
                    break;
            }
        }
        return null;
    }

    /// <summary>The ")" that closes the "(" at <paramref name="open"/>, the parentheses of an address inside it counted; null for none.</summary>
    private static int? Shut(string source, int open, int to)
    {
        var depth = 0;
        for (var at = open; at < to; at++)
        {
            switch (source[at])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    if (depth == 0)
                    {
                        return at;
                    }
                    break;
            }
        }
        return null;
    }

    /// <summary>An address written out at <paramref name="at"/> — https://… or www.… — how long it is and where it goes; null for none.</summary>
    private static (int Length, string Address)? Bare(string source, int at, int to)
    {
        var rest = source.AsSpan(at, to - at);
        var www = rest.StartsWith("www.", StringComparison.OrdinalIgnoreCase);
        if (!www && !rest.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !rest.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        var end = 0;
        while (end < rest.Length && !char.IsWhiteSpace(rest[end]) && rest[end] != '<')
        {
            end++;
        }
        // The punctuation that ends a sentence is the sentence's, and a ")" with no "(" in the address.
        while (end > 0 && rest[end - 1] is '.' or ',' or ':' or ';' or '!' or '?' or '"' or '\'' or '*' or '_' or '~'
            || (end > 0 && rest[end - 1] == ')' && rest[..end].Count('(') < rest[..end].Count(')')))
        {
            end--;
        }
        var words = rest[..end].ToString();
        var address = www ? "https://" + words : words;
        return words.Length > (www ? 4 : 8) && Opens(address) ? (words.Length, address) : null;
    }
}
