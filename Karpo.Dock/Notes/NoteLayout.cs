// A note's lines (Markdown.cs) laid out with DirectWrite at a width as the live editor shows them,
// and drawn: every line of the text a row, all of its text (MdShown), each run's weight, slant,
// strike, underline, face and ink set on its range. Windows' type ramp: a first heading at 20, a
// second at 18, a third at 16, the rest and the body at 14, all grown with the text size, its #s as
// big; code in Consolas, on a faint plate. The marks are text like the rest, faint (a task's box in
// the accent), at rest and with the caret anywhere, so a row is laid out the same wherever the caret is.
//
// One hanging indent for every block (§669, the person: "the bullet point lists should properly
// indent the text too. same for the task list"): a row is two layouts, its lead — the marks before
// its words: a quote's >s, a list's indent, marker and box, a heading's #s — on one line, and its
// body, the words, wrapping in the column the lead ends at, so a wrapped line starts under the words
// and not under the marker. Where the lead's marks stand is set by spacing between its characters
// (IDWriteTextLayout1), never by drawing them elsewhere: a quote's > after its bar, a bullet or a box
// at its parent's words, a number right-aligned with its list's widest, so 9. and 10. end together.
// A paragraph's line indented under an item goes on in that item's column.
//
// It answers what the editor asks of where things are: which place in the text a point is, where
// the caret stands, a wrapped line's ends, the place above or below — each through the row's own
// layouts, whose characters are the line's, one for one.

using System.Numerics;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal sealed unsafe class NoteLayout : IDisposable
{
    private const string CodeFont = "Consolas"; // not-ui: a font

    /// <summary>
    /// Part of a row's shown text in a layout of its own, [<see cref="From"/>, From + <see cref="Length"/>):
    /// the lead, on one line, or the body, wrapping.
    /// </summary>
    internal sealed class Part
    {
        public IDWriteTextLayout* Layout;

        public required int From { get; init; }

        public required int Length { get; init; }

        /// <summary>The row's runs that fall in it, counted from its own start.</summary>
        public required IReadOnlyList<MdRun> Runs { get; init; }

        /// <summary>What the layout was made of, so a part that is the same next time keeps it.</summary>
        public required string Key { get; init; }

        /// <summary>Where the layout's top left stands, from the note's top left.</summary>
        public Vector2 Origin { get; set; }
    }

    /// <summary>One line of the note laid out: its lead and its body, and where it stands.</summary>
    internal sealed class Row
    {
        public required MdShown Shown { get; init; }

        public MdLine Line => Shown.Line;

        /// <summary>The marks before the words, on one line; null where the words start the line.</summary>
        public Part? Lead { get; init; }

        /// <summary>The words, wrapping in the column the lead ends at.</summary>
        public required Part Body { get; init; }

        /// <summary>Where the words' column starts, and the top of their first line, from the note's top left.</summary>
        public Vector2 Origin => Body.Origin;

        /// <summary>The top of its band, the room above it (a heading's) included, from the note's top.</summary>
        public required float Top { get; init; }

        public required float Bottom { get; init; }

        public required float Px { get; init; }

        /// <summary>The code block it is a line of, counted from the note's first; -1 for none.</summary>
        public int Plate { get; init; } = -1;

        public IEnumerable<Part> Parts
        {
            get
            {
                if (Lead is not null)
                {
                    yield return Lead;
                }
                yield return Body;
            }
        }
    }

    /// <summary>A lead as laid out before its marks are placed: where its marker and its quotes' >s are in it, and how wide it is.</summary>
    private sealed class Lead
    {
        public required Part Part { get; init; }

        /// <summary>Its marker's first character (a list's -, a number, a heading's #); its length where it has none.</summary>
        public required int Marker { get; init; }

        /// <summary>The marker and the spaces and box after it, wide as they are.</summary>
        public required float MarkerWidth { get; init; }

        public required float Width { get; init; }

        /// <summary>Where each quote's > is in it.</summary>
        public required List<int> Quotes { get; init; }
    }

    private readonly List<Row> rows = [];

    private NoteLayout(float width) => Width = width;

    public float Width { get; }

    /// <summary>The whole note's height, in px.</summary>
    public float Height { get; private set; }

    /// <summary>How far each quote's column is in from the one it stands in: its bar, room, and its "> ".</summary>
    public float QuoteStep { get; private set; }

    /// <summary>How far a quote's > stands in from its bar's left.</summary>
    public float BarRoom { get; private set; }

    /// <summary>The least room a list's marker has before its words; a list nested where its parent has none, one such step in.</summary>
    public float Slot { get; private set; }

    public IReadOnlyList<Row> Rows => rows;

    /// <summary>The text size of a heading of <paramref name="level"/>, in DIP before the text scale.</summary>
    internal static float HeadingDip(int level) => level switch
    {
        1 => 20,
        2 => 18,
        3 => 16,
        _ => 14,
    };

    /// <summary>
    /// <paramref name="lines"/> of <paramref name="text"/> laid out <paramref name="width"/> px wide in
    /// <paramref name="style"/>. The layouts of <paramref name="before"/> that are the same are taken
    /// over, and the rest of it let go: it is not to be used after.
    /// </summary>
    public static NoteLayout Lay(Gfx gfx, string text, IReadOnlyList<MdLine> lines, float width, in TileStyle style, NoteLayout? before = null)
    {
        var layout = new NoteLayout(width);
        Dictionary<string, Part>? kept = null;
        var taken = new HashSet<nint>();
        if (before is not null)
        {
            kept = [];
            foreach (var part in before.rows.SelectMany(row => row.Parts))
            {
                kept.TryAdd(part.Key, part);
            }
        }
        try
        {
            layout.Build(gfx, text, lines, style, kept, taken);
            return layout;
        }
        catch
        {
            layout.Dispose();
            throw;
        }
        finally
        {
            if (before is not null)
            {
                // What was taken over is the new layout's now; the rest goes.
                foreach (var part in before.rows.SelectMany(row => row.Parts))
                {
                    if (part.Layout != null && !taken.Contains((nint)part.Layout))
                    {
                        part.Layout->Release();
                    }
                    part.Layout = null;
                }
                before.rows.Clear();
            }
        }
    }

    private void Build(Gfx gfx, string text, IReadOnlyList<MdLine> lines, in TileStyle style, Dictionary<string, Part>? kept, HashSet<nint> taken)
    {
        var body = style.Text(14);
        var grown = style.Scale * style.TextScale;
        float T(float dip) => PopupPaint.Round(dip * grown);
        BarRoom = T(10);
        QuoteStep = BarRoom + PopupPaint.Measure(gfx, "> ", body, Weight.Regular).X;
        Slot = T(18);
        var count = lines.Count;
        var shown = new MdShown[count];
        var leads = new Lead?[count];

        // Each line's lead, laid out as it comes, its marks where they fall.
        var inList = false;
        for (var index = 0; index < count; index++)
        {
            var line = lines[index];
            shown[index] = Markdown.Show(text, line);
            var length = LeadLength(text, line, inList);
            inList = line is { IsItem: true, Quote: 0 } || (inList && (line.Kind == MdKind.Blank || (line.Kind == MdKind.Paragraph && length > 0)));
            if (length > 0)
            {
                leads[index] = MakeLead(gfx, shown[index], length, PxOf(line, style), WeightOf(line), kept, taken);
            }
        }
        bool Continues(int index) => leads[index] is not null && lines[index] is { Kind: MdKind.Paragraph, Quote: 0 };

        // A numbered list's markers as wide as its widest, so they end together: the items of a depth
        // and quote up to a line that is not of the list, or an item further out.
        var numbers = new float[count];
        for (var index = 0; index < count; index++)
        {
            var first = lines[index];
            if (first.Kind != MdKind.Numbered || numbers[index] > 0 || leads[index] is null)
            {
                continue;
            }
            var members = new List<int>();
            var widest = 0f;
            for (var next = index; next < count; next++)
            {
                var each = lines[next];
                if (each.Kind == MdKind.Blank || Continues(next))
                {
                    continue;
                }
                if (!each.IsItem || each.Quote != first.Quote || each.Level < first.Level)
                {
                    break;
                }
                if (each.Kind == MdKind.Numbered && each.Level == first.Level && leads[next] is { } lead)
                {
                    members.Add(next);
                    widest = MathF.Max(widest, lead.MarkerWidth);
                }
            }
            foreach (var member in members)
            {
                numbers[member] = MathF.Max(widest, float.Epsilon);
            }
        }

        // Where each row's words start, and the rows.
        var columns = new float?[Markdown.MostDepth + 1];
        var columnsQuote = -1;
        var y = 0f;
        // Which code block a row is on: a fence opens one, the next closes it.
        var plate = -1;
        var plates = 0;
        for (var index = 0; index < count; index++)
        {
            var line = lines[index];
            var lead = leads[index];
            var onPlate = -1;
            if (line.Kind == MdKind.Fence)
            {
                onPlate = plate >= 0 ? plate : plates++;
                plate = plate >= 0 ? -1 : onPlate;
            }
            else if (line.Kind == MdKind.Code)
            {
                onPlate = plate;
            }
            // The columns of the list a line is in: an item's words are its children's markers' column.
            if (line.IsItem)
            {
                if (columnsQuote != line.Quote)
                {
                    Array.Clear(columns);
                    columnsQuote = line.Quote;
                }
            }
            else if (line.Kind != MdKind.Blank && !Continues(index))
            {
                Array.Clear(columns);
                columnsQuote = -1;
            }
            var px = PxOf(line, style);
            var weight = WeightOf(line);
            var family = line.Kind is MdKind.Code or MdKind.Fence ? CodeFont : null;
            var inset = line.Quote * QuoteStep;
            var column = 0f;
            var leadLeft = 0f;
            if (family is not null)
            {
                // Code stands in from its plate's edges.
                column = T(8);
            }
            else if (lead is not null)
            {
                var stops = lead.Quotes.Select((at, level) => (At: at, X: (level * QuoteStep) + BarRoom)).ToList();
                float wanted;
                if (line.IsItem)
                {
                    var depth = line.Level;
                    var start = depth == 0 ? inset : columns[depth - 1] ?? inset + (depth * Slot);
                    var numbered = line.Kind == MdKind.Numbered;
                    wanted = start + MathF.Max(Slot, numbered ? numbers[index] : lead.MarkerWidth);
                    // A bullet and a box at the column's start; a number at its end.
                    stops.Add((lead.Marker, numbered ? wanted - lead.MarkerWidth : start));
                }
                else if (line.Kind == MdKind.Heading)
                {
                    stops.Add((lead.Marker, inset));
                    wanted = inset;
                }
                else if (Continues(index))
                {
                    // Indented under an item: in the words' column of the item its indent says.
                    var depth = Math.Min((lead.Marker / Markdown.IndentStep) - 1, Markdown.MostDepth);
                    while (depth > 0 && columns[depth] is null)
                    {
                        depth--;
                    }
                    wanted = depth >= 0 ? columns[depth] ?? 0 : 0;
                }
                else
                {
                    wanted = inset;
                }
                (leadLeft, var end) = Place(lead, stops);
                column = MathF.Max(wanted, end);
                if (line.IsItem)
                {
                    columns[line.Level] = column;
                    Array.Clear(columns, line.Level + 1, columns.Length - line.Level - 1);
                }
            }
            var from = lead?.Part.Length ?? 0;
            var room = MathF.Max(1, Width - column - (family is null ? 0 : T(8)));
            var words = shown[index].Text[from..];
            var runs = Clip(shown[index].Runs, from, shown[index].Text.Length);
            var key = $"{px}|{weight}|{family}|{room}|{words}|{Key(runs)}"; // not-ui: a cache's key
            var made = Take(kept, taken, key);
            if (made == null)
            {
                made = Make(gfx, words, runs, px, weight, room, family);
            }
            var part = new Part { Layout = made, From = from, Length = words.Length, Runs = runs, Key = key };
            DWRITE_TEXT_METRICS metrics;
            ComUtil.Check(made->GetMetrics(&metrics));
            var tall = MathF.Max(TilePaint.LineHeight(gfx, px), metrics.height);
            // A heading has room above it, the more the bigger it is, and a little under it.
            var above = line.Kind == MdKind.Heading && y > 0 ? T(line.Level <= 2 ? 10 : 6) : 0;
            var below = line.Kind == MdKind.Heading ? T(2) : 0;
            part.Origin = new Vector2(column, y + above);
            if (lead is not null)
            {
                DWRITE_TEXT_METRICS marks;
                ComUtil.Check(lead.Part.Layout->GetMetrics(&marks));
                tall = MathF.Max(tall, marks.height);
                // On the words' first baseline, whatever the fonts on either line.
                lead.Part.Origin = new Vector2(leadLeft, y + above + Baseline(made) - Baseline(lead.Part.Layout));
            }
            tall = MathF.Ceiling(tall);
            rows.Add(new Row
            {
                Shown = shown[index],
                Lead = lead?.Part,
                Body = part,
                Top = y,
                Bottom = y + above + tall + below,
                Px = px,
                Plate = onPlate,
            });
            y += above + tall + below;
        }
        Height = MathF.Ceiling(y);
    }

    private static float PxOf(MdLine line, in TileStyle style) => line.Kind switch
    {
        MdKind.Heading => style.Text(HeadingDip(line.Level)),
        MdKind.Code or MdKind.Fence => style.Text(LineType.CaptionDip),
        _ => style.Text(14),
    };

    private static Weight WeightOf(MdLine line) => line.Kind == MdKind.Heading ? Weight.Semibold : Weight.Regular;

    /// <summary>
    /// How many of a line's shown characters are its lead: an item's or a heading's marks before its
    /// words, a quote's >s; a paragraph's spaces where it goes on under a list's item (<paramref name="inList"/>).
    /// </summary>
    internal static int LeadLength(string text, MdLine line, bool inList)
    {
        switch (line.Kind)
        {
            case MdKind.Heading or MdKind.Bullet or MdKind.Numbered or MdKind.Task:
            case MdKind.Paragraph when line.Quote > 0:
                return line.Content - line.Start;
            case MdKind.Rule when line.Quote > 0:
                var at = line.Start;
                for (var quote = 0; quote < line.Quote; quote++)
                {
                    at = text.IndexOf('>', at, line.End - at) + 1;
                    if (at < line.End && text[at] == ' ')
                    {
                        at++;
                    }
                }
                return at - line.Start;
            case MdKind.Paragraph when inList:
                var spaces = 0;
                while (line.Start + spaces < line.End && text[line.Start + spaces] == ' ')
                {
                    spaces++;
                }
                return spaces >= Markdown.IndentStep ? spaces : 0;
            default:
                return 0;
        }
    }

    /// <summary>A line's lead laid out on one line, its spacing cleared, and measured.</summary>
    private static Lead MakeLead(Gfx gfx, MdShown shown, int length, float px, Weight weight, Dictionary<string, Part>? kept, HashSet<nint> taken)
    {
        var marks = shown.Text[..length];
        var runs = Clip(shown.Runs, 0, length);
        var key = $"lead|{px}|{weight}|{marks}|{Key(runs)}"; // not-ui: a cache's key
        var layout = Take(kept, taken, key);
        if (layout == null)
        {
            layout = Make(gfx, marks, runs, px, weight, null, null);
        }
        var part = new Part { Layout = layout, From = 0, Length = length, Runs = runs, Key = key };
        // Taken over, it may have been spaced for other neighbours: measured as it is.
        Space(layout, length, []);
        var quotes = new List<int>();
        var marker = 0;
        while (marker < length && quotes.Count < shown.Line.Quote)
        {
            if (marks[marker] == '>')
            {
                quotes.Add(marker);
            }
            marker++;
        }
        while (marker < length && marks[marker] == ' ')
        {
            marker++;
        }
        DWRITE_TEXT_METRICS metrics;
        ComUtil.Check(layout->GetMetrics(&metrics));
        var width = metrics.widthIncludingTrailingWhitespace;
        return new Lead { Part = part, Marker = marker, MarkerWidth = width - X(layout, marker), Width = width, Quotes = quotes };
    }

    /// <summary>
    /// The lead's marks moved right to where <paramref name="stops"/> want them (a character's index
    /// in it, and an x from the note's left), never left, by the room after the character before
    /// each, so a caret before a mark stands at it: its left, and where it ends.
    /// </summary>
    private static (float Left, float End) Place(Lead lead, List<(int At, float X)> stops)
    {
        var layout = lead.Part.Layout;
        var natural = stops.Select(stop => X(layout, stop.At)).ToArray();
        var spacing = new Dictionary<int, (float Leading, float Trailing)>();
        var left = 0f;
        var shift = 0f;
        for (var index = 0; index < stops.Count; index++)
        {
            var (at, wanted) = stops[index];
            var pad = wanted - (left + natural[index] + shift);
            if (pad <= 0)
            {
                continue;
            }
            if (at == 0)
            {
                left += pad;
            }
            else
            {
                spacing[at - 1] = (0, pad);
                shift += pad;
            }
        }
        Space(layout, lead.Part.Length, spacing);
        return (left, left + lead.Width + shift);
    }

    /// <summary>A layout's spacing round its characters set to <paramref name="spacing"/>, and none round the rest.</summary>
    private static void Space(IDWriteTextLayout* layout, int length, Dictionary<int, (float Leading, float Trailing)> spacing)
    {
        IDWriteTextLayout1* spaced = null;
        if (length == 0 || layout->QueryInterface(__uuidof<IDWriteTextLayout1>(), (void**)&spaced).FAILED)
        {
            return;
        }
        try
        {
            _ = spaced->SetCharacterSpacing(0, 0, 0, new DWRITE_TEXT_RANGE { startPosition = 0, length = (uint)length });
            foreach (var (at, (leading, trailing)) in spacing)
            {
                _ = spaced->SetCharacterSpacing(leading, trailing, 0, new DWRITE_TEXT_RANGE { startPosition = (uint)at, length = 1 });
            }
        }
        finally
        {
            spaced->Release();
        }
    }

    /// <summary>The x of the place before character <paramref name="at"/> of a layout, from its left.</summary>
    private static float X(IDWriteTextLayout* layout, int at)
    {
        float x;
        float y;
        DWRITE_HIT_TEST_METRICS hit;
        return layout->HitTestTextPosition((uint)at, BOOL.FALSE, &x, &y, &hit).SUCCEEDED ? x : 0;
    }

    /// <summary>A layout's first line's baseline, from its top.</summary>
    private static float Baseline(IDWriteTextLayout* layout)
    {
        // Every line's asked for: a buffer short of them fills none.
        uint count = 0;
        _ = layout->GetLineMetrics(null, 0, &count);
        if (count == 0)
        {
            return 0;
        }
        var metrics = new DWRITE_LINE_METRICS[count];
        fixed (DWRITE_LINE_METRICS* first = metrics)
        {
            return layout->GetLineMetrics(first, count, &count).SUCCEEDED ? first->baseline : 0;
        }
    }

    /// <summary>The runs of <paramref name="runs"/> in [<paramref name="from"/>, <paramref name="to"/>), counted from <paramref name="from"/>.</summary>
    private static List<MdRun> Clip(IReadOnlyList<MdRun> runs, int from, int to)
    {
        var clipped = new List<MdRun>();
        foreach (var run in runs)
        {
            var start = Math.Max(run.Start, from);
            var end = Math.Min(run.Start + run.Length, to);
            if (end > start)
            {
                clipped.Add(run with { Start = start - from, Length = end - start });
            }
        }
        return clipped;
    }

    private static string Key(IReadOnlyList<MdRun> runs) => string.Join(";", runs.Select(run => $"{run.Start},{run.Length},{(int)run.Style}")); // not-ui: a cache's key

    /// <summary>A layout of the last layout that was made of <paramref name="key"/>, taken over; null for none.</summary>
    private static IDWriteTextLayout* Take(Dictionary<string, Part>? kept, HashSet<nint> taken, string key)
    {
        if (kept is not null && kept.Remove(key, out var old) && old.Layout != null)
        {
            taken.Add((nint)old.Layout);
            return old.Layout;
        }
        return null;
    }

    /// <summary>
    /// <paramref name="text"/> in a layout, wrapping at <paramref name="width"/> where there is one (else
    /// on one line), its runs' weight, slant, strike, underline and face set, and room either side of
    /// inline code's words for its plate.
    /// </summary>
    private static IDWriteTextLayout* Make(Gfx gfx, string text, IReadOnlyList<MdRun> runs, float px, Weight weight, float? width, string? family)
    {
        var layout = gfx.TextLayout(text, px, weight, null, family);
        try
        {
            if (width is { } most)
            {
                ComUtil.Check(layout->SetWordWrapping(DWRITE_WORD_WRAPPING.DWRITE_WORD_WRAPPING_EMERGENCY_BREAK));
                ComUtil.Check(layout->SetMaxWidth(most));
            }
            var spacing = new Dictionary<int, (float Leading, float Trailing)>();
            foreach (var run in runs)
            {
                var range = new DWRITE_TEXT_RANGE { startPosition = (uint)run.Start, length = (uint)run.Length };
                if (run.Style.HasFlag(MdStyle.Bold))
                {
                    _ = layout->SetFontWeight(DWRITE_FONT_WEIGHT.DWRITE_FONT_WEIGHT_BOLD, range);
                }
                if (run.Style.HasFlag(MdStyle.Italic))
                {
                    _ = layout->SetFontStyle(DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_ITALIC, range);
                }
                if (run.Style.HasFlag(MdStyle.Strike))
                {
                    _ = layout->SetStrikethrough(BOOL.TRUE, range);
                }
                if (run.Style.HasFlag(MdStyle.Link))
                {
                    _ = layout->SetUnderline(BOOL.TRUE, range);
                }
                if (run.Style.HasFlag(MdStyle.Code) && family is null)
                {
                    fixed (char* code = CodeFont)
                    {
                        _ = layout->SetFontFamilyName(code, range);
                    }
                    // Room for the plate between the words and their faint `s.
                    var pad = PopupPaint.Round(px * 3 / 14);
                    if (run.Start > 0)
                    {
                        spacing[run.Start - 1] = (spacing.GetValueOrDefault(run.Start - 1).Leading, pad);
                    }
                    if (run.Start + run.Length < text.Length)
                    {
                        var end = run.Start + run.Length;
                        spacing[end] = (pad, spacing.GetValueOrDefault(end).Trailing);
                    }
                }
            }
            if (spacing.Count > 0)
            {
                Space(layout, text.Length, spacing);
            }
            return layout;
        }
        catch
        {
            layout->Release();
            throw;
        }
    }

    // --- Where things are -------------------------------------------------------------------------------

    /// <summary>The row of the line <paramref name="source"/> (a place in the note) is on.</summary>
    public int RowOf(int source)
    {
        var low = 0;
        var high = rows.Count - 1;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (rows[middle].Line.Start <= source)
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

    /// <summary>The row a height of the note (px from its top) is in: the first or the last where it is past them.</summary>
    private int RowAt(float y)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            if (y < rows[index].Bottom)
            {
                return index;
            }
        }
        return rows.Count - 1;
    }

    /// <summary>The part of <paramref name="row"/> a place in its shown text is drawn in: the lead's up to its words, the body's from them.</summary>
    private static Part PartOf(Row row, int shown) => row.Lead is { } lead && shown < lead.Length ? lead : row.Body;

    /// <summary>Where the caret at <paramref name="source"/> is drawn, from the note's top left: its x, its top and its height.</summary>
    public (float X, float Top, float Height) CaretAt(int source)
    {
        if (rows.Count == 0)
        {
            return (0, 0, 0);
        }
        var row = rows[RowOf(source)];
        var shown = row.Shown.ToShown(source);
        var part = PartOf(row, shown);
        float x;
        float y;
        DWRITE_HIT_TEST_METRICS hit;
        ComUtil.Check(part.Layout->HitTestTextPosition((uint)(shown - part.From), BOOL.FALSE, &x, &y, &hit));
        return (part.Origin.X + x, part.Origin.Y + hit.top, hit.height > 0 ? hit.height : row.Bottom - row.Origin.Y);
    }

    /// <summary>The place in the note at <paramref name="point"/> (from the note's top left): the nearest place between two characters that show.</summary>
    public int PositionAt(Vector2 point)
    {
        if (rows.Count == 0)
        {
            return 0;
        }
        return At(rows[RowAt(point.Y)], point);
    }

    /// <summary>
    /// The place in a row nearest <paramref name="point"/>: in its lead left of its words' column on
    /// their first line, else in its words, a wrapped line's hanging indent its line's start.
    /// </summary>
    private static int At(Row row, Vector2 point)
    {
        var part = row.Lead is not null && point.X < row.Body.Origin.X && point.Y < row.Body.Origin.Y + Wrapped(row)[0].Height ? row.Lead : row.Body;
        BOOL trailing;
        BOOL inside;
        DWRITE_HIT_TEST_METRICS hit;
        if (part.Layout->HitTestPoint(point.X - part.Origin.X, point.Y - part.Origin.Y, &trailing, &inside, &hit).FAILED)
        {
            return row.Line.Content;
        }
        return row.Shown.ToSource(part.From + (int)hit.textPosition + (trailing ? (int)hit.length : 0));
    }

    /// <summary>
    /// What is at <paramref name="point"/> (from the note's top left): a task's box (its "[" in the
    /// note), a link's address, and the place in the text it is nearest.
    /// </summary>
    public (int Box, string? Link, int Position) Find(Vector2 point)
    {
        if (rows.Count == 0)
        {
            return (-1, null, 0);
        }
        var row = rows[RowAt(point.Y)];
        var position = At(row, point);
        if (row is { Line.Kind: MdKind.Task, Lead: { } lead } && point.Y >= row.Top && point.Y < row.Bottom)
        {
            // Its "[ ]" and a little room either side, a finger's worth.
            uint count = 0;
            DWRITE_HIT_TEST_METRICS box;
            if (lead.Layout->HitTestTextRange((uint)(row.Shown.ToShown(row.Line.Box) - lead.From), 3, lead.Origin.X, lead.Origin.Y, &box, 1, &count).SUCCEEDED && count > 0)
            {
                var slack = box.width / 3;
                if (point.X >= box.left - slack && point.X < box.left + box.width + slack)
                {
                    return (row.Line.Box, null, position);
                }
            }
        }
        BOOL trailing;
        BOOL inside;
        DWRITE_HIT_TEST_METRICS hit;
        var words = row.Body;
        if (point.X >= words.Origin.X && words.Layout->HitTestPoint(point.X - words.Origin.X, point.Y - words.Origin.Y, &trailing, &inside, &hit).SUCCEEDED && inside)
        {
            var at = (int)hit.textPosition;
            foreach (var run in words.Runs)
            {
                if (run.Link is { } link && at >= run.Start && at < run.Start + run.Length)
                {
                    return (-1, link, position);
                }
            }
        }
        return (-1, null, position);
    }

    /// <summary>
    /// The wrapped lines of a row: where each starts in its shown text, how long it is, its newline's
    /// and trailing spaces' length, its top and height. The first holds the lead.
    /// </summary>
    private static List<(int Start, int Length, int Trailing, float Top, float Height)> Wrapped(Row row)
    {
        var layout = row.Body.Layout;
        uint count = 0;
        _ = layout->GetLineMetrics(null, 0, &count);
        var metrics = new DWRITE_LINE_METRICS[Math.Max(1, (int)count)];
        fixed (DWRITE_LINE_METRICS* first = metrics)
        {
            _ = layout->GetLineMetrics(first, (uint)metrics.Length, &count);
        }
        var lines = new List<(int, int, int, float, float)>(Math.Max(1, (int)count));
        var lead = row.Body.From;
        var start = lead;
        var top = row.Origin.Y;
        for (var index = 0; index < count; index++)
        {
            var line = metrics[index];
            var from = index == 0 ? 0 : start;
            lines.Add((from, start + (int)line.length - from, (int)line.trailingWhitespaceLength, top, line.height));
            start += (int)line.length;
            top += line.height;
        }
        if (lines.Count == 0)
        {
            lines.Add((0, row.Shown.Text.Length, 0, top, row.Bottom - top));
        }
        return lines;
    }

    /// <summary>Home or End at <paramref name="source"/>: the start or the end of the wrapped line the caret is on, in the note.</summary>
    public int LineEdge(int source, bool end)
    {
        if (rows.Count == 0)
        {
            return source;
        }
        var row = rows[RowOf(source)];
        var shown = row.Shown.ToShown(source);
        var lines = Wrapped(row);
        for (var index = 0; index < lines.Count; index++)
        {
            var (start, length, trailing, _, _) = lines[index];
            var last = index == lines.Count - 1;
            if (shown < start + length || last)
            {
                // Before the space a line wrapped at, so End stays on its line.
                return row.Shown.ToSource(end ? Math.Max(start, start + length - (last ? 0 : trailing)) : start);
            }
        }
        return end ? row.Line.End : row.Line.Content;
    }

    /// <summary>
    /// Up (<paramref name="lines"/> -1) or Down (+1), or more for Page Up and Down: the place
    /// <paramref name="lines"/> wrapped lines away nearest <paramref name="goal"/> across; the note's
    /// start or end where it runs out.
    /// </summary>
    public int Vertical(int source, float goal, int lines, int length)
    {
        if (rows.Count == 0)
        {
            return source;
        }
        var row = RowOf(source);
        var wrapped = Wrapped(rows[row]);
        var shown = rows[row].Shown.ToShown(source);
        var line = wrapped.FindIndex(each => shown < each.Start + each.Length);
        if (line < 0)
        {
            line = wrapped.Count - 1;
        }
        var step = Math.Sign(lines);
        for (var moved = 0; moved != lines; moved += step)
        {
            line += step;
            if (line < 0)
            {
                if (--row < 0)
                {
                    return 0;
                }
                wrapped = Wrapped(rows[row]);
                line = wrapped.Count - 1;
            }
            else if (line >= wrapped.Count)
            {
                if (++row >= rows.Count)
                {
                    return length;
                }
                wrapped = Wrapped(rows[row]);
                line = 0;
            }
        }
        var target = wrapped[line];
        return At(rows[row], new Vector2(goal, target.Top + (target.Height / 2)));
    }

    // --- Drawn ------------------------------------------------------------------------------------------

    /// <summary>
    /// Draws the rows that show in <paramref name="clip"/> with the note's top left at
    /// <paramref name="at"/>: plates and quotes' bars, the selection from <paramref name="from"/> to
    /// <paramref name="to"/>, the words, rules' lines, and the caret at <paramref name="caret"/> (-1 for none).
    /// </summary>
    public void Paint(ID2D1DeviceContext* dc, Vector2 at, D2D_RECT_F clip, QuickTheme theme, in TileStyle style, int from, int to, int caret)
    {
        dc->PushAxisAlignedClip(&clip, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        try
        {
            var hair = MathF.Max(1, style.Px(1));
            var bar = MathF.Max(2, style.Px(3));
            var low = Math.Min(from, to);
            var high = Math.Max(from, to);
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var line = row.Line;
                if (at.Y + row.Bottom < clip.top)
                {
                    continue;
                }
                if (at.Y + row.Top > clip.bottom)
                {
                    break;
                }
                var firstShown = index == 0 || at.Y + rows[index - 1].Bottom < clip.top;
                // A code block's plate: from its first row to its last, drawn once, with the first that shows.
                if (row.Plate >= 0 && (firstShown || rows[index - 1].Plate != row.Plate))
                {
                    var (first, last) = Run(index, each => each.Plate == row.Plate);
                    var plate = new D2D_RECT_F(at.X, at.Y + rows[first].Top, at.X + Width, at.Y + rows[last].Bottom);
                    PopupPaint.Fill(dc, plate, style.Px(4), theme.ControlFill);
                    PopupPaint.Stroke(dc, plate, style.Px(4), hair, theme.ControlStroke);
                }
                // A bar for each quote a row stands in, down the quote's rows, its >s after it.
                for (var level = 0; level < line.Quote; level++)
                {
                    if (firstShown || rows[index - 1].Line.Quote <= level)
                    {
                        var deep = level;
                        var (first, last) = Run(index, each => each.Line.Quote > deep);
                        var left = PopupPaint.Round(at.X + (level * QuoteStep));
                        PopupPaint.Fill(dc, new D2D_RECT_F(left, at.Y + rows[first].Origin.Y, left + bar, at.Y + rows[last].Bottom), bar / 2, theme.StrongStroke);
                    }
                }
                var chosen = high > low && low <= line.End && high >= line.Start;
                var selected = chosen ? (row.Shown.ToShown(Math.Max(low, line.Start)), row.Shown.ToShown(Math.Min(high, line.End))) : (0, 0);
                var ink = line.Quote > 0 || line is { Kind: MdKind.Task, Checked: true } ? theme.TextSecondary : theme.Text;
                foreach (var part in row.Parts)
                {
                    var origin = new D2D_POINT_2F(PopupPaint.Round(at.X + part.Origin.X), PopupPaint.Round(at.Y + part.Origin.Y));
                    Selection(dc, part, origin, selected, theme);
                    Text(dc, row, part, origin, ink, theme, at.X + Width, selected);
                }
                if (chosen && high > line.End)
                {
                    // A selected line break shows as a sliver, as Windows' edit boxes show it.
                    var (x, top, tall) = CaretAt(line.End);
                    PopupPaint.Fill(dc, new D2D_RECT_F(at.X + x, at.Y + top, at.X + x + style.Px(4), at.Y + top + tall), 0, theme.Selection);
                }
                if (line.Kind == MdKind.Rule)
                {
                    // A rule's line, from its marks to the note's edge, through their middle.
                    DWRITE_TEXT_METRICS metrics;
                    ComUtil.Check(row.Body.Layout->GetMetrics(&metrics));
                    var left = PopupPaint.Round(at.X + row.Origin.X + metrics.widthIncludingTrailingWhitespace + style.Gap);
                    var middle = PopupPaint.Round(at.Y + row.Origin.Y + (metrics.height / 2));
                    if (left < at.X + Width)
                    {
                        PopupPaint.Fill(dc, new D2D_RECT_F(left, middle, at.X + Width, middle + hair), 0, theme.Divider);
                    }
                }
                if (caret >= 0 && caret >= line.Start && caret <= line.End)
                {
                    var (x, top, tall) = CaretAt(caret);
                    var left = PopupPaint.Round(at.X + x);
                    PopupPaint.Fill(dc, new D2D_RECT_F(left, at.Y + top, left + hair, at.Y + top + tall), 0, theme.Text);
                }
            }
        }
        finally
        {
            dc->PopAxisAlignedClip();
        }
    }

    /// <summary>The rows round <paramref name="index"/> that <paramref name="holds"/> holds for, one after another: the first and the last.</summary>
    private (int First, int Last) Run(int index, Func<Row, bool> holds)
    {
        var first = index;
        while (first > 0 && holds(rows[first - 1]))
        {
            first--;
        }
        var last = index;
        while (last + 1 < rows.Count && holds(rows[last + 1]))
        {
            last++;
        }
        return (first, last);
    }

    /// <summary>The selection over a part, <paramref name="selected"/> in the row's shown text.</summary>
    private static void Selection(ID2D1DeviceContext* dc, Part part, D2D_POINT_2F origin, (int Start, int End) selected, QuickTheme theme)
    {
        var start = Math.Max(selected.Start, part.From) - part.From;
        var end = Math.Min(selected.End, part.From + part.Length) - part.From;
        if (end <= start)
        {
            return;
        }
        var layout = part.Layout;
        uint count = 0;
        _ = layout->HitTestTextRange((uint)start, (uint)(end - start), origin.x, origin.y, null, 0, &count);
        if (count == 0)
        {
            return;
        }
        var hits = new DWRITE_HIT_TEST_METRICS[count];
        fixed (DWRITE_HIT_TEST_METRICS* first = hits)
        {
            _ = layout->HitTestTextRange((uint)start, (uint)(end - start), origin.x, origin.y, first, count, &count);
        }
        foreach (var hit in hits.AsSpan(0, (int)count))
        {
            PopupPaint.Fill(dc, new D2D_RECT_F(hit.left, hit.top, hit.left + hit.width, hit.top + hit.height), 0, theme.Selection);
        }
    }

    /// <summary>Windows' check box, <paramref name="box"/> square: a rim round a faint fill, or the accent with its check mark.</summary>
    internal static void CheckBox(ID2D1DeviceContext* dc, Gfx gfx, D2D_RECT_F box, bool ticked, bool hot, QuickTheme theme, in TileStyle style)
    {
        var radius = MathF.Max(2, style.Px(3));
        if (ticked)
        {
            PopupPaint.Fill(dc, box, radius, hot ? theme.AccentHover : theme.Accent);
            PopupPaint.Glyph(dc, gfx, "\uE73E", PopupPaint.Round((box.right - box.left) * 0.65f), theme.TextOnAccent, box);
            return;
        }
        PopupPaint.Fill(dc, box, radius, hot ? theme.ControlAltFillHover : theme.ControlAltFill);
        PopupPaint.Stroke(dc, box, radius, MathF.Max(1, style.Px(1)), theme.StrongStroke);
    }

    /// <summary>
    /// A part's words: inline code on its plate, links in the link colour, marks faint, the rest in
    /// <paramref name="ink"/>, and what is <paramref name="selected"/> (the row's shown text) on the accent in its own colour, as a text box's.
    /// </summary>
    private static void Text(ID2D1DeviceContext* dc, Row row, Part part, D2D_POINT_2F origin, DXGI_RGBA ink, QuickTheme theme, float right, (int Start, int End) selected)
    {
        var layout = part.Layout;
        if (part.Length == 0)
        {
            return;
        }
        // Inline code stands on a faint plate, as GitHub and Windows' own docs draw it, in the room Make left either side of it.
        if (row.Line.Kind is not (MdKind.Code or MdKind.Fence))
        {
            var inflate = MathF.Max(1, PopupPaint.Round(row.Px * 3 / 14) - 1);
            foreach (var run in part.Runs)
            {
                if (!run.Style.HasFlag(MdStyle.Code))
                {
                    continue;
                }
                uint count = 0;
                _ = layout->HitTestTextRange((uint)run.Start, (uint)run.Length, origin.x, origin.y, null, 0, &count);
                if (count == 0)
                {
                    continue;
                }
                var hits = new DWRITE_HIT_TEST_METRICS[count];
                fixed (DWRITE_HIT_TEST_METRICS* first = hits)
                {
                    if (layout->HitTestTextRange((uint)run.Start, (uint)run.Length, origin.x, origin.y, first, count, &count).FAILED)
                    {
                        continue;
                    }
                }
                foreach (var hit in hits.AsSpan(0, (int)count))
                {
                    // A little wider than the words, but never past the note's own edges.
                    var plate = new D2D_RECT_F(hit.left - inflate, hit.top, MathF.Min(right, hit.left + hit.width + inflate), hit.top + hit.height);
                    PopupPaint.Fill(dc, plate, 3, theme.ControlFill);
                }
            }
        }
        var brushes = new List<nint>();
        try
        {
            foreach (var run in part.Runs)
            {
                // A task's box in the accent's text colour (a link's, which high contrast keeps legible).
                DXGI_RGBA? color = run.Style.HasFlag(MdStyle.Mark) ? theme.TextTertiary
                    : run.Style.HasFlag(MdStyle.Box) || run.Link is not null ? theme.Link
                    : null;
                if (color is not { } paint)
                {
                    continue;
                }
                var brush = Gfx.Brush(dc, paint);
                brushes.Add((nint)brush);
                _ = layout->SetDrawingEffect((IUnknown*)brush, new DWRITE_TEXT_RANGE { startPosition = (uint)run.Start, length = (uint)run.Length });
            }
            var start = Math.Max(selected.Start, part.From) - part.From;
            var end = Math.Min(selected.End, part.From + part.Length) - part.From;
            if (end > start)
            {
                var brush = Gfx.Brush(dc, theme.SelectionText);
                brushes.Add((nint)brush);
                _ = layout->SetDrawingEffect((IUnknown*)brush, new DWRITE_TEXT_RANGE { startPosition = (uint)start, length = (uint)(end - start) });
            }
            var main = Gfx.Brush(dc, PaintWatch.Seen(layout, origin, ink));
            try
            {
                Mirror.Text(dc, origin, layout, (ID2D1Brush*)main, D2D1_DRAW_TEXT_OPTIONS.D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT);
            }
            finally
            {
                main->Release();
            }
        }
        finally
        {
            if (brushes.Count > 0)
            {
                _ = layout->SetDrawingEffect(null, new DWRITE_TEXT_RANGE { startPosition = 0, length = (uint)part.Length });
                foreach (var brush in brushes)
                {
                    ((ID2D1SolidColorBrush*)brush)->Release();
                }
            }
        }
    }

    public void Dispose()
    {
        foreach (var part in rows.SelectMany(row => row.Parts))
        {
            if (part.Layout != null)
            {
                part.Layout->Release();
                part.Layout = null;
            }
        }
        rows.Clear();
    }
}

