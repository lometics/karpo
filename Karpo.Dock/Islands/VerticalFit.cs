// Text in the width of a vertical bar.
//
// A vertical taskbar gives a widget a narrow column: its cells run down the
// bar, and the bar's own width is all the room any line of text has. The
// failure nearly every kind shares there is that the text is measured at the
// size a horizontal bar would use and then simply clipped, so a date reads
// "W… 2…" and a drive's label is a blank line.
//
// The rule here is the one the clock and the screen time already followed by
// hand: take the largest size at which the text's own shape fits across the
// bar, stepping down through the sizes a widget has, and only then cut. A
// widget that still cannot fit its essential text at the smallest size is in
// the third branch of the vertical rule (docs\widgets.md §12.4) and wants its
// content rotated rather than shrunk — the decision recorded there is
// bottom-to-top, with glyphs left upright.
//
// The size chosen is a function of the settings and the bar's numbers — a
// template is measured, never the value being shown — so it obeys the same
// no-shift rule as everything else: a widget cannot change size, or change
// the size of its text, because its state changed.

namespace Karpo.Dock;

internal static class VerticalFit
{
    /// <summary>
    /// The largest of <paramref name="sizes"/> (largest first) at which
    /// <paramref name="shape"/> — a template of the widest the text can be, not
    /// the value showing now — fits <paramref name="across"/> px, and the
    /// smallest where none do, since a widget shows what it can rather than
    /// nothing. The measured width at that size comes back with it.
    /// </summary>
    public static (float Px, float Width, float Height) Across(Gfx gfx, string shape, float across, Weight weight, ReadOnlySpan<float> sizes)
    {
        if (sizes.Length == 0 || shape.Length == 0)
        {
            return (0f, 0f, 0f);
        }
        var last = TextLine.Template(gfx, shape, sizes[^1], weight);
        foreach (var px in sizes)
        {
            var line = TextLine.Template(gfx, shape, px, weight);
            if (line.Width <= across)
            {
                return (px, line.Width, line.Height);
            }
        }
        // None of them fit: the smallest, cut to the room it has.
        return (sizes[^1], MathF.Min(last.Width, MathF.Max(0f, across)), last.Height);
    }

    /// <summary>
    /// The room a line of text actually has across a vertical bar: the cell
    /// less the side a line keeps at either side (<see cref="Fit.Side"/>,
    /// 2 to 8 px with the bar's thickness).
    /// <para>
    /// This is the number to fit text to. It is deliberately <b>not</b>
    /// <see cref="Fit.Across"/>, which is the bar's own width less a single
    /// pixel: fitting to that chooses a size for 2 to 14 px of room the drawing
    /// never has, and the text then reaches the cell's edge or loses its
    /// ellipsis margin. That was the defect behind the date reading "W… 2…".
    /// </para>
    /// </summary>
    public static float Room(in Fit fit) => MathF.Max(1f, fit.Cell - (2 * fit.Side));

    /// <summary>
    /// The bar's own width, less a pixel either side (<see cref="Fit.Across"/>).
    /// Rarely what a widget wants: only something drawn edge to edge, with no
    /// inset of its own, may fit to this. Text wants <see cref="Room"/>.
    /// </summary>
    public static float BarWidth(in Fit fit) => fit.Across;

    /// <summary>
    /// Whether <paramref name="shape"/> can be read across the bar at all — it
    /// fits at the smallest size a widget will go to. A widget whose essential
    /// text answers false here is one to rotate rather than shrink.
    /// </summary>
    public static bool Fits(Gfx gfx, string shape, float across, Weight weight, float smallest) =>
        shape.Length == 0 || TextLine.Template(gfx, shape, smallest, weight).Width <= across;

    /// <summary>
    /// The largest of <paramref name="sizes"/> (largest first) at which every one of
    /// <paramref name="shapes"/> fits <paramref name="across"/> px, the smallest where none does,
    /// and a line's height there: one size for rows that stand one over another.
    /// </summary>
    public static (float Px, float Height) AcrossAll(Gfx gfx, IReadOnlyList<string> shapes, float across, Weight weight, ReadOnlySpan<float> sizes)
    {
        if (sizes.Length == 0)
        {
            return (0f, 0f);
        }
        foreach (var px in sizes)
        {
            var fits = true;
            foreach (var shape in shapes)
            {
                if (shape.Length > 0 && TextLine.Template(gfx, shape, px, weight).Width > across)
                {
                    fits = false;
                    break;
                }
            }
            if (fits)
            {
                return (px, TextLine.Template(gfx, "8", px, weight).Height);
            }
        }
        return (sizes[^1], TextLine.Template(gfx, "8", sizes[^1], weight).Height);
    }

    /// <summary>
    /// The sizes a line down a side bar steps through, largest first: the cell's own three, then
    /// a few steps under them through 11 DIP (the smallest the bar's own text is) to 9 DIP, at
    /// Windows' text size — and last the same three steps at 100 % text, so that on the thinnest
    /// bar, with the text made larger in Windows, a row is set smaller than that size asks rather
    /// than cut (§598). A line takes the largest size that fits, so the last steps are only taken
    /// where nothing above them does.
    /// </summary>
    public static float[] Ladder(in Fit fit)
    {
        var dip = fit.Scale * fit.TextScale;
        float[] sizes = [fit.Primary, fit.Secondary, fit.Small, 0.85f * fit.Small, 0.7f * fit.Small, 11 * dip, 10 * dip, 9 * dip,
            11 * fit.Scale, 10 * fit.Scale, 9 * fit.Scale];
        var floor = 9 * MathF.Min(dip, fit.Scale);
        return [.. sizes.Where(size => size >= floor - 0.01f).Distinct().OrderDescending()];
    }

    /// <summary>
    /// How a text stands down a side bar (<see cref="Rows(Gfx, in Fit, int, Func{int, IEnumerable{string[]}}, Weight, float)"/>):
    /// which of its forms, the one size of its rows, a row's height, and how many rows the form
    /// takes at its most.
    /// </summary>
    internal readonly record struct SideRows(int Form, float Px, float Height, int Rows)
    {
        /// <summary>The length down the bar of the form at its most rows (a date's, which are always as many).</summary>
        public float Length => Rows * Height;

        /// <summary>
        /// The length down the bar of <paramref name="rows"/>, what a reading says now in this form: a face
        /// is as long as what it shows (§244) — "42 min" one row, "3 h" over "42 min" two — at one size.
        /// </summary>
        public float LengthOf(string[] rows) => rows.Length * Height;
    }

    /// <summary>The text on one row.</summary>
    public static string[] Whole(string text) => [text];

    /// <summary>
    /// The text broken before each word that starts with a figure, where the row has one already:
    /// "3 h 42 min" is "3 h" over "42 min", "3 Std. 42 Min." is "3 Std." over "42 Min.".
    /// </summary>
    public static string[] Groups(string text)
    {
        var rows = new List<string>();
        var row = "";
        foreach (var word in Words(text))
        {
            if (row.Length > 0 && char.IsDigit(word[0]) && row.Any(char.IsDigit))
            {
                rows.Add(row);
                row = word;
            }
            else
            {
                row = row.Length == 0 ? word : $"{row} {word}";
            }
        }
        if (row.Length > 0)
        {
            rows.Add(row);
        }
        return [.. rows];
    }

    /// <summary>Every word of the text a row of its own.</summary>
    public static string[] Words(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// A reading of figures and words — a total ("3 h 42 min"), a time ("10∶58 PM"), a score ("2–1"):
    /// on one row, else a row for each figure with its words (<see cref="Groups"/>), else a row for each
    /// word, else its parts (<see cref="Parts"/>: "10" over "58" over "PM", "3" over "小时"). Not for a
    /// decimal ("1.25"), which its parts would break.
    /// </summary>
    public static readonly Func<string, string[]>[] Phrase = [Whole, Groups, Words, text => Parts(text)];

    /// <summary>
    /// <see cref="Rows(Gfx, in Fit, int, Func{int, IEnumerable{string[]}}, Weight, float)"/> for a text that reads as one
    /// of <paramref name="templates"/>, broken by each of <paramref name="forms"/> in turn.
    /// </summary>
    public static SideRows Rows(Gfx gfx, in Fit fit, IReadOnlyList<Func<string, string[]>> forms, IReadOnlyList<string> templates, Weight weight = Weight.Regular, float most = float.MaxValue) =>
        Rows(gfx, fit, forms.Count, form => templates.Select(forms[form]), weight, most);

    /// <summary>
    /// How much smaller than the largest any of its forms allows a text may be set to stay on fewer rows:
    /// "3 h" over "42 min" at 10 DIP rather than "3", "h", "42", "min" at 14; "28.12." over "2026" at 20
    /// DIP rather than "28.12.2026" on one row at 12 under a time at 20.
    /// </summary>
    private const float FewerRowsShare = 2f / 3;

    /// <summary>
    /// How a text stands across a side bar: the one rule every face of words follows there (§598).
    /// Each of its <paramref name="forms"/> — the same words on more rows each, fewest first — is set at
    /// the largest size of the ladder (<see cref="Ladder"/>), no larger than <paramref name="most"/>, at
    /// which every one of its rows fits the room a line has (<see cref="Room"/>); the first is taken
    /// that is no less than two thirds of the largest any form reaches, and no less than 9 DIP of
    /// Windows' text size. So a text is said on fewer rows while that keeps it about as large, on more
    /// where that makes it markedly larger — and it is never said less: where no form fits that well,
    /// the one that fits largest, the ladder's 100 % steps included; where none fits at all (nothing
    /// down a side bar Karpo has is that narrow), the last, cut. <paramref name="rows"/> gives every way
    /// a form's rows can read — from templates, never the reading now — so neither the form nor the
    /// size moves as the reading does.
    /// </summary>
    public static SideRows Rows(Gfx gfx, in Fit fit, int forms, Func<int, IEnumerable<string[]>> rows, Weight weight = Weight.Regular, float most = float.MaxValue)
    {
        var room = Room(fit);
        float[] ladder = [.. Ladder(fit).Where(size => size <= most + 0.01f)];
        if (ladder.Length == 0)
        {
            ladder = [most];
        }
        var sized = new List<(SideRows Plan, bool Fits)>();
        for (var form = 0; form < forms; form++)
        {
            var each = rows(form).ToList();
            var shapes = each.SelectMany(row => row).ToList();
            var (px, height) = AcrossAll(gfx, shapes, room, weight, ladder);
            var fits = shapes.TrueForAll(shape => shape.Length == 0 || TextLine.Template(gfx, shape, px, weight).Width <= room);
            sized.Add((new SideRows(form, px, height, each.Count == 0 ? 0 : each.Max(row => row.Length)), fits));
        }
        var fitting = sized.Where(each => each.Fits).Select(each => each.Plan).ToList();
        if (fitting.Count == 0)
        {
            return sized[^1].Plan;
        }
        var largest = fitting.Max(plan => plan.Px);
        var least = MathF.Max(FewerRowsShare * largest, MathF.Min(9 * fit.Scale * fit.TextScale, largest));
        return fitting.First(plan => plan.Px >= least - 0.01f);
    }

    /// <summary>
    /// A time's parts, one row each down a side bar (the person, 2026-10-13: "it should show minutes
    /// and hours on a different row"): "22:58:59" is 22, 58 and 59, "10:58 pm" is 10, 58 and pm, and
    /// with <paramref name="words"/> a designator written against the figures ("午後10:58") is a row
    /// of its own too. The separators — a colon, the ratio the clock sets, a point, a space, a score's
    /// dash ("2–1") — are what the rows stand in for, and go.
    /// </summary>
    public static string[] Parts(string time, bool words = true)
    {
        var parts = new List<string>();
        var run = new System.Text.StringBuilder();
        var digits = false;
        void Close()
        {
            if (run.Length > 0)
            {
                parts.Add(run.ToString());
                run.Clear();
            }
        }
        foreach (var c in time)
        {
            if (c is ':' or '.' or (char)0x2236 or (char)0x2013 || char.IsWhiteSpace(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format)
            {
                Close();
                continue;
            }
            var digit = char.IsDigit(c);
            if (words && run.Length > 0 && digit != digits)
            {
                Close();
            }
            digits = digit;
            run.Append(c);
        }
        Close();
        return [.. parts];
    }

    /// <summary>
    /// <paramref name="text"/> in rows of whole words each at most <paramref name="across"/> px at
    /// <paramref name="px"/>: a label down a side bar, said in full on as many rows as it takes. A
    /// word wider than the room is a row of its own, which its line then cuts.
    /// </summary>
    public static List<string> Wrap(Gfx gfx, string text, float px, float across, Weight weight)
    {
        var rows = new List<string>();
        var row = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var longer = row.Length == 0 ? word : $"{row} {word}";
            if (row.Length > 0 && TextLine.Template(gfx, longer, px, weight).Width > across)
            {
                rows.Add(row);
                row = word;
            }
            else
            {
                row = longer;
            }
        }
        if (row.Length > 0)
        {
            rows.Add(row);
        }
        return rows;
    }
}
