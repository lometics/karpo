// A note as a line of a list folder (docs\widgets.md §87): its title, and its body — one editor that
// is always live. The note is Markdown, shown with every mark it has, faint, and the words they mark
// as what they mean (Markdown.cs, NoteLayout.cs): headings big, bold bold, a task's "[ ]" in the
// accent; the same at rest and wherever the caret is (§321). Which note is the line's (NoteOptions);
// none, or one deleted, is the notebook's first.
//
// A click puts the caret there (a double click a word, a triple its line) and the keys are the
// note's (NoteEditor): typing, the arrows by the lines as they wrap, Shift to select, Ctrl+C/X/V
// as plain text, Ctrl+Z and Ctrl+Y, Enter going on with a list, Tab and Shift+Tab indenting its
// items, Ctrl+Enter ticking a task. A click on a task's "[ ]" ticks it; a click on a link opens it
// (http, https and mailto only, by the shell, off the bar's thread) unless the caret is in it.
// Escape, a click elsewhere in the folder or the folder closing lets the keys go; what was typed is
// kept every couple of seconds while it is typed, so a crash loses little, and when they go.
//
// As tall as the note (IListContent), up to the line's most; a longer note scrolls inside the line,
// by the wheel and to keep the caret in view, with a thin bar at its edge to say so.
//
// In the title's row, at its end, a toolbar that does not scroll with the note, as Windows' command
// bars stand over what they act on (§317, the person: "move the edit bar to the topbar area next to
// the title"; §87 to §313 it was a row of its own under the title): a heading in turn, bold, italic,
// struck, code and a link, and bulleted, numbered and task lists and a quote (NoteFormat.cs), each
// with its key in its tooltip. A button toggles, and shows pressed where the caret's words already
// are what it makes. A press on it leaves the caret and the selection where they were, so the keys
// stay the note's; at rest it starts the typing first. The title keeps the room for its words, up to
// MinTitle: where the rest has no room for every button, those that fit stand there with "More" (…)
// at the row's end, which opens the rest in rows under the title; too short a line has no toolbar.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal sealed unsafe class NoteTile : NotebookTile, IListContent, ILinePieces
{
    /// <summary>How long typing rests before what was typed is kept, in ms.</summary>
    private const long KeepAfter = 2000;

    private readonly NoteEditor editor = new();

    private bool typing;
    private string? typingNote;
    private long typedAt;
    private string kept = "";
    private bool selecting;
    private bool caretOn = true;
    private char pendingHigh;

    // Up and Down keep to the x they started at; the caret moved since the line was drawn, so the drawing brings it into view.
    private float? goal;
    private bool following;

    // How far the body is scrolled, px.
    private float scroll;

    // Where the body was last drawn and in what, for the keys, which come without either; and the caret there, for the IME.
    private D2D_RECT_F bodyIn;
    private TileStyle drawnStyle = new(1, new Look(false, false, default), FlyoutTheme.For(new Look(false, false, default)), 1, 1);
    private D2D_RECT_F? caretIn;

    // A press on a link or a task's box, acted on when it is let go where it went down.
    private (string? Link, int Box)? pressed;
    private Vector2 pressedAt;

    // The body laid out, kept while its words, its width and its text size are the same.
    private NoteLayout? laid;
    private (string Text, float Width, float Px, float Scale) laidFor;

    // The toolbar: the button under the pointer and the one pressed (Tool.Key; -1 for none), whether the
    // pointer is still on the one pressed, and whether "More" shows the buttons a narrow line has no room for.
    private int hotTool = -1;
    private int pressedTool = -1;
    private bool pressedOver;
    private bool more;

    public NoteTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        Configure(island, config);
    }

    public override void Configure(WidgetItem island, Config config) => Settings = island;

    /// <summary>
    /// The note this line shows: its own, else the notebook's first; null where there is none. §643: a ghost's (made showing the
    /// samples) whose note is empty or none, the samples' (<see cref="Samples.Note"/>), never written.
    /// </summary>
    internal Note? Note => Book.Current.Note(Settings.Note?.Note) is var own && Sampled && string.IsNullOrWhiteSpace(own?.Body) ? Samples.Note() : own;

    internal bool Typing => typing;

    internal NoteEditor Editor => editor;

    internal float Scroll => scroll;

    protected internal override bool CardTyping => typing;

    protected override void Blink() => caretOn = !caretOn;

    /// <summary>A tick while it is typed: the caret blinks, and what was typed is kept once the typing rests.</summary>
    protected override bool Sample(bool everything)
    {
        if (typing && editor.Text != kept && Environment.TickCount64 - typedAt >= KeepAfter)
        {
            Save();
        }
        return base.Sample(everything);
    }

    // --- Laid out ------------------------------------------------------------------------------------

    private static float Row(in TileStyle style) => PopupPaint.Round(40 * style.Scale * style.TextScale);

    private static float Bottom(in TileStyle style) => PopupPaint.Round(style.Text(10));

    /// <summary>What the body shows: the text being typed, else the note's.</summary>
    private string Shown => typing ? editor.Text : Note?.Body ?? "";

    /// <summary>The body laid out at <paramref name="width"/> (the line's), kept while nothing changes: the caret moving lays nothing out again.</summary>
    private NoteLayout Body(float width, in TileStyle style)
    {
        var room = MathF.Max(1, width - (2 * ListForm.Gutter(style)));
        var text = Shown;
        var key = (text, room, style.TextScale, style.Scale);
        if (laid is null || laidFor != key)
        {
            var lines = typing ? editor.Lines : Markdown.Lines(text);
            laid = NoteLayout.Lay(Gfx, text, lines, room, style, laid);
            laidFor = key;
        }
        return laid;
    }

    /// <summary>Where the body is drawn in <paramref name="box"/>: under the title and what "More" opened, between the gutters, above the foot.</summary>
    private D2D_RECT_F BodyBox(D2D_RECT_F box, in TileStyle style)
    {
        var gutter = ListForm.Gutter(style);
        var top = box.top + Row(style) + (Bar(box, style)?.Under ?? 0);
        return new D2D_RECT_F(box.left + gutter, top, box.right - gutter, MathF.Max(top, box.bottom - Bottom(style)));
    }

    /// <summary>The body's top left, as scrolled, in the line's px.</summary>
    private Vector2 Origin(D2D_RECT_F body) => new(body.left, body.top - scroll);

    public float ListHeight(float width, in TileStyle style)
    {
        var body = Body(width, style).Height;
        // What "More" opened under the title at this width; a line held this tall has room for it.
        var bar = Tools(PopupPaint.Rect(0, 0, width, 0), style).Under;
        // An empty note keeps a row for what it says (or for the caret).
        return Row(style) + bar + (string.IsNullOrWhiteSpace(Shown) ? Row(style) : body) + Bottom(style);
    }

    // --- The toolbar ----------------------------------------------------------------------------------

    /// <summary>The buttons in their groups, in order; a separator between two groups.</summary>
    private static readonly NoteFormat[][] Groups =
    [
        [NoteFormat.Heading],
        [NoteFormat.Bold, NoteFormat.Italic, NoteFormat.Strike, NoteFormat.Code, NoteFormat.Link],
        [NoteFormat.Bullets, NoteFormat.Numbers, NoteFormat.Tasks, NoteFormat.Quote],
    ];

    /// <summary>"More"'s key among the buttons' (a format's is its number).</summary>
    private const int MoreKey = 100;

    /// <summary>A toolbar's button: what it does (null for "More") and where it stands, in the line's px.</summary>
    private readonly record struct Tool(NoteFormat? Format, D2D_RECT_F Box)
    {
        public int Key => Format is { } format ? (int)format : MoreKey;
    }

    /// <summary>
    /// The toolbar laid out: its buttons, the lines between its groups, where the title's words end
    /// (TitleEnd, in the line's px), and how tall the rows "More" opened under the title are (0 shut).
    /// </summary>
    private sealed record Toolbar(IReadOnlyList<Tool> Tools, IReadOnlyList<D2D_RECT_F> Separators, float TitleEnd, float Under);

    /// <summary>A button's side: a square, as a command bar's icon buttons are.</summary>
    private static float Side(in TileStyle style) => PopupPaint.Round(style.Text(28));

    /// <summary>A row "More" opens under the title: a button and a little air above and below it.</summary>
    private static float BarRow(in TileStyle style) => PopupPaint.Round(style.Text(34));

    /// <summary>The room the title keeps for its words before the buttons take it, at most: its own width where shorter.</summary>
    private static float MinTitle(in TileStyle style) => PopupPaint.Round(style.Text(96));

    /// <summary>
    /// The toolbar in <paramref name="box"/> (the line), in the title's row at its end, inside the
    /// gutter: every button where the title keeps its words (up to <see cref="MinTitle"/>); else those
    /// that fit and "More" at the row's end, and with "More" open the rest in rows under the title,
    /// between the gutters.
    /// </summary>
    private Toolbar Tools(D2D_RECT_F box, in TileStyle style)
    {
        var gutter = ListForm.Gutter(style);
        var side = Side(style);
        var gap = style.Px(2);
        // A separator's room, the button's gap before it included: a line with air either side.
        var split = style.Px(13);
        // The air between the title's words and the first button.
        var apart = style.Px(8);
        var head = Row(style);
        var row = BarRow(style);
        var left = box.left + gutter;
        var right = box.right - gutter;
        var thin = MathF.Max(1, style.Px(1));
        var tall = PopupPaint.Round(side * 0.5f);
        var entries = new List<NoteFormat?>();
        foreach (var group in Groups)
        {
            if (entries.Count > 0)
            {
                // null: a separator.
                entries.Add(null);
            }
            entries.AddRange(group.Select(format => (NoteFormat?)format));
        }
        float Width(IEnumerable<NoteFormat?> run) => run.Sum(entry => entry is null ? split - gap : side + gap) - gap;
        var form = ListForm.Of(Gfx, style);
        var words = TextLine.Measure(Gfx, Note?.Title ?? S.WidgetNoteNewNote, form.TextPx, Weight.Semibold, null);
        var room = right - (left + MathF.Min(MathF.Ceiling(words), MinTitle(style)) + apart);
        // As many as fit in the title's row, "More" after them where not all do; never a separator last.
        var count = entries.Count;
        if (Width(entries) > room + 0.5f)
        {
            count = 0;
            while (count < entries.Count && Width(entries.Take(count + 1)) + gap + side <= room + 0.5f)
            {
                count++;
            }
            while (count > 0 && entries[count - 1] is null)
            {
                count--;
            }
        }
        var shown = entries.Take(count).ToList();
        var fits = count == entries.Count;
        var tools = new List<Tool>();
        var separators = new List<D2D_RECT_F>();
        var x = right - (fits ? Width(shown) : (shown.Count > 0 ? Width(shown) + gap : 0) + side);
        var titleEnd = MathF.Max(left, x - apart);
        var y = box.top + PopupPaint.Round((head - side) / 2);
        foreach (var entry in shown)
        {
            Place(entry);
        }
        var rows = 0;
        if (!fits)
        {
            tools.Add(new Tool(null, PopupPaint.Rect(right - side, y, side, side)));
            if (more)
            {
                // The rest in rows of their own under the title, from the gutter, wrapping at the other.
                var rest = entries.Skip(count).SkipWhile(entry => entry is null).ToList();
                x = left;
                y = box.top + head + PopupPaint.Round((row - side) / 2);
                rows = 1;
                for (var index = 0; index < rest.Count; index++)
                {
                    var entry = rest[index];
                    if (entry is null)
                    {
                        // A separator only between two buttons on one row; at a row's end it goes.
                        if (x > left && x - gap + split + side <= right + 0.5f)
                        {
                            Place(entry);
                        }
                        continue;
                    }
                    if (x > left && x + side > right + 0.5f)
                    {
                        x = left;
                        y += row;
                        rows++;
                    }
                    Place(entry);
                }
            }
        }
        return new Toolbar(tools, separators, titleEnd, rows * row);

        // An entry at x on the row at y.
        void Place(NoteFormat? entry)
        {
            if (entry is null)
            {
                var middle = PopupPaint.Round(x - gap + (split / 2));
                separators.Add(PopupPaint.Rect(middle - (thin / 2), y + PopupPaint.Round((side - tall) / 2), thin, tall));
                x += split - gap;
                return;
            }
            tools.Add(new Tool(entry, PopupPaint.Rect(x, y, side, side)));
            x += side + gap;
        }
    }

    /// <summary>The toolbar where the line has room for a line of the note under its title (and what "More" opened); null where it is too short.</summary>
    private Toolbar? Bar(D2D_RECT_F box, in TileStyle style)
    {
        var line = TilePaint.LineHeight(Gfx, style.Text(14));
        return box.bottom - box.top + 0.5f >= Row(style) + line + Bottom(style) ? Tools(box, style) : null;
    }

    /// <summary>The button at <paramref name="point"/>; null for none.</summary>
    private static Tool? ToolAt(Toolbar bar, Vector2 point)
    {
        foreach (var tool in bar.Tools)
        {
            if (LineRows.Inside(tool.Box, point))
            {
                return tool;
            }
        }
        return null;
    }

    /// <summary>
    /// Whether <paramref name="point"/> is on the toolbar: in the title's row from its first button on,
    /// or on the rows "More" opened under it (a button or the air between them).
    /// </summary>
    private bool OnBar(Vector2 point, D2D_RECT_F box, in TileStyle style)
    {
        if (Bar(box, style) is not { } bar)
        {
            return false;
        }
        var head = box.top + Row(style);
        return point.Y < head ? point.X >= bar.TitleEnd : point.Y < head + bar.Under;
    }

    /// <summary>What a button says over it: what it does and its key.</summary>
    private static string TipOf(NoteFormat? format) => format switch
    {
        NoteFormat.Heading => S.WidgetNoteFormatHeading,
        NoteFormat.Bold => S.WidgetNoteFormatBold,
        NoteFormat.Italic => S.WidgetNoteFormatItalic,
        NoteFormat.Strike => S.WidgetNoteFormatStrike,
        NoteFormat.Code => S.WidgetNoteFormatCode,
        NoteFormat.Link => S.WidgetNoteFormatLink,
        NoteFormat.Bullets => S.WidgetNoteFormatBullets,
        NoteFormat.Numbers => S.WidgetNoteFormatNumbers,
        NoteFormat.Tasks => S.WidgetNoteFormatTasks,
        NoteFormat.Quote => S.WidgetNoteFormatQuote,
        _ => S.WidgetNoteFormatMore,
    };

    /// <summary>A button's Segoe Fluent Icons glyph; empty for the two drawn otherwise (the heading's H, the numbered list).</summary>
    private static string GlyphOf(NoteFormat? format) => format switch
    {
        NoteFormat.Bold => "",
        NoteFormat.Italic => "",
        NoteFormat.Strike => "",
        NoteFormat.Code => "",
        NoteFormat.Link => "",
        NoteFormat.Bullets => "",
        NoteFormat.Tasks => "",
        NoteFormat.Quote => "",
        null => "",
        _ => "",
    };

    /// <summary>Whether a button shows pressed: its format where the caret is, while it types; "More" while it is open.</summary>
    private bool On(NoteFormat? format) => format is { } on ? typing && editor.IsOn(on) : more;

    private void PaintBar(ID2D1DeviceContext* dc, Toolbar bar, in TileStyle style, QuickTheme theme)
    {
        var radius = style.Px(4);
        var glyph = PopupPaint.Round(style.Text(16));
        for (var index = 0; index < bar.Tools.Count; index++)
        {
            var tool = bar.Tools[index];
            if (On(tool.Format))
            {
                PopupPaint.Fill(dc, tool.Box, radius, theme.SubtleSelected);
            }
            if (hotTool == tool.Key)
            {
                PopupPaint.Fill(dc, tool.Box, radius, pressedTool == tool.Key && pressedOver ? theme.SubtlePressed : theme.SubtleHover);
            }
            // At rest the toolbar stands back a little; while the note is typed in, or under the pointer, it is ink.
            var ink = typing || hotTool == tool.Key ? theme.Text : theme.TextSecondary;
            switch (tool.Format)
            {
                case NoteFormat.Heading:
                {
                    var level = typing ? NoteFormats.Level(editor.Lines, editor.Caret) : 0;
                    PopupPaint.Centred(dc, Gfx, level is > 0 and <= 6 ? $"H{level}" : "H", style.Text(LineType.BodyDip), Weight.Semibold, ink, tool.Box, ink: true);
                    break;
                }
                case NoteFormat.Numbers:
                    Numbered(dc, tool.Box, glyph, ink, style);
                    break;
                default:
                    PopupPaint.Glyph(dc, Gfx, GlyphOf(tool.Format), glyph, ink, tool.Box);
                    break;
            }
        }
        foreach (var separator in bar.Separators)
        {
            PopupPaint.Fill(dc, separator, 0, theme.Divider);
        }
    }

    /// <summary>
    /// A numbered list's icon, which Segoe Fluent Icons has none of: "1" and "2" before two lines, in
    /// the square a glyph of <paramref name="glyph"/> px stands in, the strokes as thin as the glyphs'.
    /// </summary>
    private void Numbered(ID2D1DeviceContext* dc, D2D_RECT_F box, float glyph, DXGI_RGBA ink, in TileStyle style)
    {
        var left = PopupPaint.Round(((box.left + box.right) / 2) - (glyph / 2));
        var middle = (box.top + box.bottom) / 2;
        var thin = MathF.Max(1, style.Px(1));
        var digit = glyph * 0.42f;
        for (var at = 0; at < 2; at++)
        {
            var y = PopupPaint.Round(middle + ((at == 0 ? -1 : 1) * glyph * 0.24f));
            var number = PopupPaint.Rect(left, y - (glyph * 0.25f), glyph * 0.3f, glyph * 0.5f);
            PopupPaint.Centred(dc, Gfx, at == 0 ? "1" : "2", digit, Weight.Semibold, ink, number, ink: true);
            PopupPaint.Fill(dc, PopupPaint.Rect(left + PopupPaint.Round(glyph * 0.38f), y - (thin / 2), PopupPaint.Round(glyph * 0.62f), thin), 0, ink);
        }
    }

    /// <summary>A button let go where it was pressed: "More" opens or closes; a format is toggled where the caret is, the typing begun first at rest.</summary>
    private void Use(int key)
    {
        if (key == MoreKey)
        {
            more = !more;
            Redrawn();
            return;
        }
        if (!typing)
        {
            Begin();
        }
        Format((NoteFormat)key);
        Redrawn();
    }

    /// <summary><paramref name="format"/> toggled where the caret is, by a button or its key.</summary>
    private void Format(NoteFormat format)
    {
        if (editor.Format(format))
        {
            typedAt = Environment.TickCount64;
        }
        Moved(keepGoal: false);
    }

    /// <summary>The scroll kept inside what there is to scroll; with the caret moved, the scroll that shows it.</summary>
    private void Keep(NoteLayout layout, float view)
    {
        if (typing && following)
        {
            var (_, top, tall) = layout.CaretAt(editor.Caret);
            if (top < scroll)
            {
                scroll = top;
            }
            else if (top + tall > scroll + view)
            {
                scroll = top + tall - view;
            }
        }
        following = false;
        scroll = Math.Clamp(scroll, 0, MathF.Max(0, layout.Height - view));
    }

    // --- Drawn ----------------------------------------------------------------------------------------

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var theme = QuickTheme.For(style.Theme);
        var form = ListForm.Of(Gfx, style);
        var width = box.right - box.left;
        var row = Row(style);
        var title = Note?.Title ?? S.WidgetNoteNewNote;
        var bar = Bar(box, style);
        // The title's words up to the toolbar beside them, cut short there.
        var end = bar?.TitleEnd ?? box.right - form.Inset;
        TilePaint.Line(dc, Gfx, title, form.TextPx, Weight.Semibold, theme.Text, box.left + form.Inset,
            box.top + PopupPaint.Round((row - TilePaint.LineHeight(Gfx, form.TextPx, Weight.Semibold)) / 2), MathF.Max(1, end - box.left - form.Inset));
        if (bar is not null)
        {
            PaintBar(dc, bar, style, theme);
        }
        var body = BodyBox(box, style);
        bodyIn = body;
        drawnStyle = style;
        var layout = Body(width, style);
        var view = body.bottom - body.top;
        Keep(layout, view);
        if (string.IsNullOrEmpty(Shown))
        {
            PopupPaint.Wrapped(dc, Gfx, S.WidgetNoteEmpty, form.TextPx, Weight.Regular, theme.TextTertiary, body.left, body.top, MathF.Max(1, body.right - body.left), 2);
        }
        // A little room either side, so a code block's rim and a plate round inline code are not cut.
        var clip = new D2D_RECT_F(body.left - style.Px(4), body.top, body.right + style.Px(4), body.bottom);
        var origin = Origin(body);
        var (from, to) = typing ? (editor.Anchor, editor.Caret) : (-1, -1);
        layout.Paint(dc, origin, clip, theme, style, from, to, typing && caretOn && !editor.HasSelection ? editor.Caret : -1);
        if (typing)
        {
            var (x, top, tall) = layout.CaretAt(editor.Caret);
            caretIn = PopupPaint.Rect(origin.X + x, origin.Y + top, MathF.Max(1, style.Px(1)), tall);
        }
        else
        {
            caretIn = null;
        }
        // Longer than the line: a thin bar at its edge says how much more there is and where this is, as a scrolling list's does at rest.
        if (layout.Height > view + 0.5f && view > 0)
        {
            var thin = MathF.Max(2, style.Px(2));
            var length = MathF.Max(style.Px(16), view * view / layout.Height);
            var at = body.top + ((view - length) * scroll / (layout.Height - view));
            var x = box.right - PopupPaint.Round(ListForm.Gutter(style) / 2) - (thin / 2);
            PopupPaint.Fill(dc, new D2D_RECT_F(x, at, x + thin, at + length), thin / 2, theme.TextTertiary);
        }
    }

    // --- The pointer ----------------------------------------------------------------------------------

    /// <summary>Whether the link run at <paramref name="position"/> has the caret in it, so a click there edits it rather than opening it.</summary>
    private bool InLink(int position)
    {
        if (!typing)
        {
            return false;
        }
        var lines = editor.Lines;
        var line = lines[Markdown.LineAt(lines, position)];
        var low = editor.SelectionStart;
        var high = editor.SelectionEnd;
        return line.Spans.Any(span => span.Link is not null && position >= span.Start && position <= span.End && low <= span.End && high >= span.Start);
    }

    /// <summary>What a point (the line's px) is on in the body: a task's box, a link, and the place in the text.</summary>
    private (int Box, string? Link, int Position) Find(Vector2 point, D2D_RECT_F box, in TileStyle style)
    {
        var layout = Body(box.right - box.left, style);
        var found = layout.Find(point - Origin(BodyBox(box, style)));
        return found.Link is not null && InLink(found.Position) ? found with { Link = null } : found;
    }

    protected internal override bool CardPress(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var clicks = Clicks(point);
        if (OnBar(point, box, style))
        {
            // A button acts when it is let go on it; the caret and the selection stay as they are, and the keys the note's.
            pressedTool = Bar(box, style) is { } bar && ToolAt(bar, point) is { } tool ? tool.Key : -1;
            pressedOver = pressedTool >= 0;
            Redrawn();
            return true;
        }
        if (point.Y - box.top < Row(style))
        {
            // The title is the line's to carry: the folder takes the press, and the typing ends.
            if (typing)
            {
                Stop();
                Redrawn();
            }
            return false;
        }
        var found = Find(point, box, style);
        pressedAt = point;
        if (found.Box >= 0 || found.Link is not null)
        {
            pressed = (found.Link, found.Box);
            return true;
        }
        if (!typing)
        {
            Begin();
        }
        switch (clicks)
        {
            case 2:
                editor.SelectWordAt(found.Position);
                break;
            case >= 3:
                editor.SelectLineAt(found.Position);
                break;
            default:
                editor.MoveTo(found.Position, KeyState.Down(VK.VK_SHIFT));
                break;
        }
        selecting = clicks == 1;
        Moved(keepGoal: false);
        Redrawn();
        return true;
    }

    protected internal override void CardDrag(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (pressedTool >= 0)
        {
            // Off the button, it lights as at rest and does nothing when let go; back on, it is pressed again.
            var over = Bar(box, style) is { } bar && ToolAt(bar, point)?.Key == pressedTool;
            if (over != pressedOver)
            {
                pressedOver = over;
                hotTool = over ? pressedTool : -1;
                Redrawn();
            }
            return;
        }
        if (selecting && typing)
        {
            var layout = Body(box.right - box.left, style);
            editor.MoveTo(layout.PositionAt(point - Origin(BodyBox(box, style))), extend: true);
            Moved(keepGoal: false);
            Redrawn();
        }
        else if (pressed is not null && Vector2.Distance(point, pressedAt) > 4 * style.Scale)
        {
            // Moved off: no click.
            pressed = null;
        }
    }

    protected internal override void CardRelease()
    {
        selecting = false;
        if (pressedTool >= 0)
        {
            var key = pressedTool;
            var over = pressedOver;
            pressedTool = -1;
            pressedOver = false;
            if (over)
            {
                Use(key);
            }
            else
            {
                Redrawn();
            }
            return;
        }
        if (pressed is not { } what)
        {
            return;
        }
        pressed = null;
        if (what.Link is { } link)
        {
            Open(link);
        }
        else if (what.Box >= 0)
        {
            Tick(what.Box);
        }
        Redrawn();
    }

    /// <summary>The task box at <paramref name="box"/> ticked or cleared: in the text being typed (so it is undone as typing is), else in the note.</summary>
    private void Tick(int box)
    {
        if (typing)
        {
            if (editor.Toggle(box))
            {
                typedAt = Environment.TickCount64;
                Save();
            }
        }
        else if (Note is { } note)
        {
            Book.SetBody(note.Id, Markdown.Toggled(note.Body, box), Now);
        }
    }

    protected internal override bool CardWheel(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface, int notches)
    {
        var body = BodyBox(box, style);
        var layout = Body(box.right - box.left, style);
        var before = scroll;
        scroll = Math.Clamp(scroll - (notches * 3 * TilePaint.LineHeight(Gfx, style.Text(14))), 0, MathF.Max(0, layout.Height - (body.bottom - body.top)));
        if (scroll == before)
        {
            // Nothing more to scroll that way: the list's to scroll.
            return false;
        }
        Redrawn();
        return true;
    }

    /// <summary>The button under the pointer lights; the folder draws the line again after telling it.</summary>
    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        hotTool = point is { } at && Bar(box, style) is { } bar && ToolAt(bar, at) is { } tool ? tool.Key : -1;

    protected internal override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (Bar(box, style) is { } bar && ToolAt(bar, point) is { } tool)
        {
            return (TipOf(tool.Format), tool.Box);
        }
        if (point.Y - box.top < Row(style) || OnBar(point, box, style) || Find(point, box, style).Link is not { } link)
        {
            return null;
        }
        // Where a link goes, said before it is clicked, as a browser says it.
        var size = style.Px(8);
        return (link, PopupPaint.Rect(point.X - size, point.Y - size, 2 * size, 2 * size));
    }

    /// <summary>The body takes text: the pointer over it is the text cursor, but over a link or a task's box.</summary>
    protected internal override bool CardText(Vector2 point, D2D_RECT_F box, in TileStyle style)
    {
        if (!LineRows.Inside(BodyBox(box, style), point))
        {
            return false;
        }
        var found = Find(point, box, style);
        return found.Box < 0 && found.Link is null;
    }

    protected internal override D2D_RECT_F? CardCaret => typing ? caretIn : null;

    // --- Typing ---------------------------------------------------------------------------------------

    /// <summary>Gives the note the keys: the caret where it was last, at the end the first time.</summary>
    internal void Begin()
    {
        typing = true;
        typingNote = Note?.Id;
        kept = Note?.Body ?? "";
        editor.Load(kept);
        typedAt = Environment.TickCount64;
        pendingHigh = '\0';
        Moved(keepGoal: false);
    }

    /// <summary>What was typed, kept in the notebook (the first note made where there is none).</summary>
    private void Save()
    {
        var text = editor.Text;
        typingNote = Book.SetBody(typingNote ?? Settings.Note?.Note, text, Now);
        kept = text;
    }

    private void Stop()
    {
        if (!typing)
        {
            return;
        }
        if (editor.Text != kept)
        {
            Save();
        }
        typing = false;
        typingNote = null;
        selecting = false;
        caretIn = null;
    }

    /// <summary>The caret moved: it shows at once, and the drawing brings it into view.</summary>
    private void Moved(bool keepGoal)
    {
        caretOn = true;
        following = true;
        if (!keepGoal)
        {
            goal = null;
        }
    }

    protected internal override void CardStopTyping() => Stop();

    protected internal override bool CardChar(char c, WidgetSurface surface)
    {
        if (!typing)
        {
            return false;
        }
        if (char.IsHighSurrogate(c))
        {
            pendingHigh = c;
            return true;
        }
        string typed;
        if (char.IsLowSurrogate(c))
        {
            if (pendingHigh == 0)
            {
                return true;
            }
            typed = new string([pendingHigh, c]);
        }
        else
        {
            typed = c.ToString();
        }
        pendingHigh = '\0';
        // Control characters (Tab, Enter, Escape, Ctrl+letter) were keys, not text.
        if (char.IsControl(c))
        {
            return true;
        }
        if (editor.Type(typed))
        {
            typedAt = Environment.TickCount64;
        }
        Moved(keepGoal: false);
        return true;
    }

    /// <summary>keys: the line walks its editor itself (CellIsland.WalksItsOwnKeys).</summary>
    protected internal override bool WalksItsOwnKeys => true;

    protected internal override bool CardKey(int key, WidgetSurface surface)
    {
        if (!typing)
        {
            if (key is VK.VK_RETURN or VK.VK_F2)
            {
                Begin();
                return true;
            }
            return false;
        }
        var shift = KeyState.Down(VK.VK_SHIFT);
        var control = KeyState.Down(VK.VK_CONTROL);
        var alt = KeyState.Down(VK.VK_MENU);
        var before = editor.Text;
        var keepGoal = false;
        switch (key)
        {
            case VK.VK_ESCAPE:
                Stop();
                return true;
            case VK.VK_TAB:
                _ = editor.Indent(outdent: shift, out var took);
                if (!took)
                {
                    // Off a list, Tab leaves the note for the next line, as a text box that takes no tabs does.
                    Stop();
                    return false;
                }
                break;
            case VK.VK_RETURN when control:
                // Ctrl+Enter ticks the task the caret is on, as Obsidian's does; anywhere else the typing ends.
                if (editor.CaretLine is { Kind: MdKind.Task } task)
                {
                    _ = editor.Toggle(task.Box);
                    break;
                }
                Stop();
                return true;
            case VK.VK_RETURN:
                _ = editor.Enter();
                break;
            case VK.VK_LEFT:
                editor.Left(shift, control);
                break;
            case VK.VK_RIGHT:
                editor.Right(shift, control);
                break;
            case VK.VK_HOME when control:
                editor.Home(shift);
                break;
            case VK.VK_END when control:
                editor.End(shift);
                break;
            case VK.VK_HOME or VK.VK_END:
                editor.MoveTo(Drawn().LineEdge(editor.Caret, key == VK.VK_END), shift);
                break;
            case VK.VK_UP or VK.VK_DOWN or VK.VK_PRIOR or VK.VK_NEXT:
                editor.MoveTo(Vertical(key), shift);
                keepGoal = true;
                break;
            case VK.VK_BACK:
                _ = editor.Backspace(control);
                break;
            case VK.VK_DELETE when shift && !control:
                Cut();
                break;
            case VK.VK_DELETE:
                _ = editor.Delete(control);
                break;
            case VK.VK_INSERT when shift:
                Paste();
                break;
            case VK.VK_INSERT when control:
                Copy();
                break;
            case 'A' when control:
                editor.SelectAll();
                break;
            // The toolbar's keys, as Word's and GitHub's are; never with Alt, which with Ctrl is AltGr typing € or @.
            case 'B' when control && !shift && !alt:
                _ = editor.Format(NoteFormat.Bold);
                break;
            case 'I' when control && !shift && !alt:
                _ = editor.Format(NoteFormat.Italic);
                break;
            case 'X' when control && shift && !alt:
                _ = editor.Format(NoteFormat.Strike);
                break;
            case 'E' when control && !shift && !alt:
                _ = editor.Format(NoteFormat.Code);
                break;
            case 'K' when control && !shift && !alt:
                _ = editor.Format(NoteFormat.Link);
                break;
            case 'H' when control && !shift && !alt:
                _ = editor.Format(NoteFormat.Heading);
                break;
            case '8' when control && shift && !alt:
                _ = editor.Format(NoteFormat.Bullets);
                break;
            case '7' when control && shift && !alt:
                _ = editor.Format(NoteFormat.Numbers);
                break;
            case '9' when control && shift && !alt:
                _ = editor.Format(NoteFormat.Tasks);
                break;
            case VK.VK_OEM_PERIOD when control && shift && !alt:
                _ = editor.Format(NoteFormat.Quote);
                break;
            case 'C' when control:
                Copy();
                break;
            case 'X' when control:
                Cut();
                break;
            case 'V' when control:
                Paste();
                break;
            case 'Z' when control && shift:
            case 'Y' when control:
                _ = editor.Redo();
                break;
            case 'Z' when control:
                _ = editor.Undo();
                break;
            case VK.VK_SPACE:
                // The space itself comes as a character.
                return true;
            default:
                // A key that types comes as a character; anything else is not the note's.
                return !control && KeyTypes(key);
        }
        if (editor.Text != before)
        {
            typedAt = Environment.TickCount64;
        }
        Moved(keepGoal);
        return true;
    }

    /// <summary>Whether <paramref name="key"/> types something (a WM_CHAR follows it), so it is the note's.</summary>
    private static bool KeyTypes(int key) =>
        key is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= VK.VK_NUMPAD0 and <= VK.VK_DIVIDE or >= VK.VK_OEM_1 and <= VK.VK_OEM_3 or >= VK.VK_OEM_4 and <= VK.VK_OEM_8 or VK.VK_OEM_102;

    /// <summary>The body as last drawn, for the keys.</summary>
    private NoteLayout Drawn() => Body(bodyIn.right - bodyIn.left + (2 * ListForm.Gutter(drawnStyle)), drawnStyle);

    /// <summary>Where Up, Down, Page Up or Page Down take the caret: the same x on the line above or below, or a body's height away.</summary>
    private int Vertical(int key)
    {
        var layout = Drawn();
        goal ??= layout.CaretAt(editor.Caret).X;
        var line = MathF.Max(1, TilePaint.LineHeight(Gfx, drawnStyle.Text(14)));
        var page = Math.Max(1, (int)((bodyIn.bottom - bodyIn.top) / line) - 1);
        var lines = key switch
        {
            VK.VK_UP => -1,
            VK.VK_DOWN => 1,
            VK.VK_PRIOR => -page,
            _ => page,
        };
        return layout.Vertical(editor.Caret, goal.Value, lines, editor.Text.Length);
    }

    private static HWND Owner => GetFocus();

    private void Copy()
    {
        if (editor.Copy() is { } text)
        {
            QuickSettings.Controls.Clipboard.SetText(Owner, text);
        }
    }

    private void Cut()
    {
        if (editor.HasSelection)
        {
            QuickSettings.Controls.Clipboard.SetText(Owner, editor.SelectedText);
            _ = editor.Cut();
        }
    }

    private void Paste()
    {
        if (QuickSettings.Controls.Clipboard.GetText(Owner) is { Length: > 0 } text)
        {
            _ = editor.Paste(text);
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        laid?.Dispose();
        laid = null;
    }

    // --- As pieces ----------------------------------------------------------------------------------

    public string MainPiece => "title";

    public IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F box, in TileStyle style)
    {
        var row = Row(style);
        var bar = Bar(box, style);
        var title = bar?.TitleEnd ?? box.right;
        var pieces = new List<LinePiece> { new("title", PopupPaint.Rect(box.left, box.top, title - box.left, row), Note?.Title ?? S.WidgetNoteNewNote) };
        if (bar is not null)
        {
            pieces.AddRange(bar.Tools.Select(tool => new LinePiece($"tool {(tool.Format?.ToString() ?? "More").ToLowerInvariant()}", tool.Box, TipOf(tool.Format))));
        }
        var body = BodyBox(box, style);
        var layout = Body(box.right - box.left, style);
        var origin = Origin(body);
        for (var index = 0; index < layout.Rows.Count; index++)
        {
            var each = layout.Rows[index];
            if (each.Line.Kind == MdKind.Blank || origin.Y + each.Bottom <= body.top)
            {
                continue;
            }
            if (origin.Y + each.Top >= body.bottom)
            {
                break;
            }
            var at = new D2D_RECT_F(body.left, MathF.Max(body.top, origin.Y + each.Top), body.right, MathF.Min(body.bottom, origin.Y + each.Bottom));
            pieces.Add(new LinePiece($"{each.Line.Kind.ToString().ToLowerInvariant()} {index}", at, each.Shown.Text));
        }
        return pieces;
    }
}
