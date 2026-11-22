// What the note's live editor holds (docs\widgets.md §87): the note's text, the caret and the
// selection (TextEditing's rules), what was done to it to undo, and the edits a note's Markdown asks
// for — Enter goes on with a list or a quote, Tab and Shift+Tab indent a list's items, Backspace at
// an item's words takes its marker away. Every mark is a character on screen (§321), so the caret
// moves through them one at a time like any other. Pure, so the rules are tested without a window;
// NoteLayout knows where the lines wrap and the note line feeds it the keys.

using Karpo.Core;
using Karpo.Dock.QuickSettings.Controls;

namespace Karpo.Dock;

/// <summary>A note's text after an edit, and where the caret and the selection's other end are.</summary>
internal readonly record struct NoteEdit(string Text, int Caret, int Anchor);

/// <summary>The edits a note's Markdown asks for, each a function of the text and the caret.</summary>
internal static class NoteEdits
{
    /// <summary>
    /// Enter at <paramref name="caret"/> (nothing selected): in a list item's words, a new item of the
    /// same kind under it — a task unticked, a number one on — with what was after the caret; in an
    /// empty item, the item one level out, or no item at all at the outermost, as Obsidian and Word
    /// end a list; the same for a quote's lines. Null where Enter is a new line and nothing more.
    /// </summary>
    public static NoteEdit? Enter(string text, int caret)
    {
        var lines = Markdown.Lines(text);
        var line = lines[Markdown.LineAt(lines, caret)];
        if (!(line.IsItem || line is { Kind: MdKind.Paragraph, Quote: > 0 }) || caret < line.Content)
        {
            return null;
        }
        var inside = Inside(text, line);
        var empty = text.AsSpan(line.Content, line.End - line.Content).IsWhiteSpace();
        if (empty)
        {
            if (line.IsItem && Indent(text, inside) is var spaces and > 0)
            {
                var take = Math.Min(Markdown.IndentStep, spaces);
                return new NoteEdit(text.Remove(inside, take), caret - take, caret - take);
            }
            // The outermost item's marker, or the quote's marks, go: the line is left as an empty line.
            var from = line.IsItem ? inside : line.Start;
            var cleared = string.Concat(text.AsSpan(0, from), text.AsSpan(line.End));
            return new NoteEdit(cleared, from, from);
        }
        var prefix = text[line.Start..(inside + Indent(text, inside))];
        var marker = line.Kind switch
        {
            MdKind.Bullet => $"{text[inside + Indent(text, inside)]} ",
            MdKind.Task => $"{text[inside + Indent(text, inside)]} [ ] ",
            MdKind.Numbered => Next(line.Marker) + " ",
            _ => "",
        };
        var added = "\n" + prefix + marker;
        var made = text.Insert(caret, added);
        return new NoteEdit(made, caret + added.Length, caret + added.Length);
    }

    /// <summary>"3." as the number after it, "4."; "9)" as "10)".</summary>
    internal static string Next(string marker)
    {
        var digits = marker[..^1];
        return int.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number) && number < 999_999_999
            ? (number + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + marker[^1]
            : marker;
    }

    /// <summary>
    /// Tab (or Shift+Tab, <paramref name="outdent"/>) with the caret or the selection on list items:
    /// every item it touches one level in (or out), the caret and the selection kept on their words.
    /// Null where it touches no item, and Tab is not the note's.
    /// </summary>
    public static NoteEdit? Indent(string text, int caret, int anchor, bool outdent)
    {
        var lines = Markdown.Lines(text);
        var first = Markdown.LineAt(lines, Math.Min(caret, anchor));
        var last = Markdown.LineAt(lines, Math.Max(caret, anchor));
        var edits = new List<(int At, int Added)>();
        for (var index = first; index <= last; index++)
        {
            var line = lines[index];
            if (!line.IsItem)
            {
                continue;
            }
            var inside = Inside(text, line);
            if (!outdent)
            {
                edits.Add((inside, Markdown.IndentStep));
            }
            else if (Math.Min(Markdown.IndentStep, Indent(text, inside)) is var take and > 0)
            {
                edits.Add((inside, -take));
            }
            else
            {
                // An item at the outermost already: still the list's key, though nothing moves.
                edits.Add((inside, 0));
            }
        }
        if (edits.Count == 0)
        {
            return null;
        }
        var made = new System.Text.StringBuilder(text);
        // From the end, so each edit's place still stands.
        for (var index = edits.Count - 1; index >= 0; index--)
        {
            var (at, added) = edits[index];
            if (added > 0)
            {
                made.Insert(at, new string(' ', added));
            }
            else if (added < 0)
            {
                made.Remove(at, -added);
            }
        }
        return new NoteEdit(made.ToString(), Moved(caret, edits), Moved(anchor, edits));
    }

    private static int Moved(int place, List<(int At, int Added)> edits)
    {
        var moved = place;
        foreach (var (at, added) in edits)
        {
            if (added > 0 && place >= at)
            {
                moved += added;
            }
            else if (added < 0 && place > at)
            {
                moved -= Math.Min(-added, place - at);
            }
        }
        return moved;
    }

    /// <summary>
    /// Backspace at the start of an item's or a quote's words, nothing selected, as a word processor's
    /// list ends: a nested item one level out, an outermost item's marker (and box) gone, a quote's
    /// innermost mark gone. Null where Backspace takes the character before the caret.
    /// </summary>
    public static NoteEdit? Backspace(string text, int caret)
    {
        var lines = Markdown.Lines(text);
        var line = lines[Markdown.LineAt(lines, caret)];
        if (caret != line.Content || line.Content == line.Start || !(line.IsItem || line is { Kind: MdKind.Paragraph, Quote: > 0 }))
        {
            return null;
        }
        var inside = Inside(text, line);
        if (line.IsItem)
        {
            if (Indent(text, inside) is var spaces and > 0)
            {
                var take = Math.Min(Markdown.IndentStep, spaces);
                return new NoteEdit(text.Remove(inside, take), caret - take, caret - take);
            }
            return new NoteEdit(text.Remove(inside, line.Content - inside), inside, inside);
        }
        // A quote's line: its last ">" and what follows it up to the words.
        var mark = text.LastIndexOf('>', line.Content - 1, line.Content - line.Start);
        return mark < 0 ? null : new NoteEdit(text.Remove(mark, line.Content - mark), mark, mark);
    }

    /// <summary>Where <paramref name="line"/>'s quote marks end and its indent starts.</summary>
    internal static int Inside(string text, MdLine line)
    {
        if (line.Quote == 0)
        {
            return line.Start;
        }
        var at = line.Start;
        for (var quote = 0; quote < line.Quote; quote++)
        {
            at = text.IndexOf('>', at, line.End - at) + 1;
            if (at < line.End && text[at] == ' ')
            {
                at++;
            }
        }
        return at;
    }

    private static int Indent(string text, int at)
    {
        var count = 0;
        while (at + count < text.Length && text[at + count] == ' ')
        {
            count++;
        }
        return count;
    }
}

/// <summary>
/// What was done to a note, to undo and redo. A run of typing (or of deleting) is one step, up to a
/// space after a word, as a word processor's is; anything else is a step of its own.
/// </summary>
internal sealed class NoteHistory
{
    /// <summary>The most steps kept.</summary>
    internal const int Most = 200;

    internal enum Kind
    {
        Other,
        Typing,
        Deleting,
    }

    private readonly LinkedList<NoteEdit> undo = new();
    private readonly Stack<NoteEdit> redo = new();
    private Kind? last;
    private int lastAt = -1;

    public bool CanUndo => undo.Count > 0;

    public bool CanRedo => redo.Count > 0;

    /// <summary>
    /// An edit of <paramref name="kind"/> is about to change <paramref name="before"/>: a step, unless
    /// it goes on with the run before it — the same kind, where that one left the caret.
    /// </summary>
    public void Will(NoteEdit before, Kind kind)
    {
        var goesOn = kind != Kind.Other && last == kind && before.Caret == lastAt && before.Caret == before.Anchor && undo.Count > 0;
        if (!goesOn)
        {
            undo.AddLast(before);
            if (undo.Count > Most)
            {
                undo.RemoveFirst();
            }
        }
        redo.Clear();
        last = kind;
    }

    /// <summary>The edit left the caret at <paramref name="caret"/>, where the next of its kind goes on from.</summary>
    public void Did(int caret) => lastAt = caret;

    /// <summary>The run ends: the caret moved by itself, or a word was finished.</summary>
    public void Break() => last = null;

    /// <summary>Back a step from <paramref name="now"/>: what it was before; null where there is nothing to undo.</summary>
    public NoteEdit? Undo(NoteEdit now)
    {
        if (undo.Last is not { } step)
        {
            return null;
        }
        undo.RemoveLast();
        redo.Push(now);
        last = null;
        return step.Value;
    }

    /// <summary>On a step that was undone; null where there is none.</summary>
    public NoteEdit? Redo(NoteEdit now)
    {
        if (redo.Count == 0)
        {
            return null;
        }
        undo.AddLast(now);
        last = null;
        return redo.Pop();
    }

    public void Clear()
    {
        undo.Clear();
        redo.Clear();
        last = null;
    }
}

/// <summary>The note being edited: its text and caret, its lines read, and what can be undone.</summary>
internal sealed class NoteEditor
{
    private readonly TextEditing editing = new(Notebook.MostBody, multiLine: true);
    private readonly NoteHistory history = new();
    private IReadOnlyList<MdLine>? lines;
    private string? linesOf;

    public string Text => editing.Text;

    public int Caret => editing.Caret;

    public int Anchor => editing.Anchor;

    public int SelectionStart => editing.SelectionStart;

    public int SelectionEnd => editing.SelectionStart + editing.SelectionLength;

    public bool HasSelection => editing.HasSelection;

    public string SelectedText => editing.SelectedText;

    internal NoteHistory History => history;

    /// <summary>The text's lines, read once for each text.</summary>
    public IReadOnlyList<MdLine> Lines
    {
        get
        {
            if (lines is null || !ReferenceEquals(linesOf, editing.Text))
            {
                lines = Markdown.Lines(editing.Text);
                linesOf = editing.Text;
            }
            return lines;
        }
    }

    /// <summary>The line the caret is on.</summary>
    public MdLine CaretLine => Lines[Markdown.LineAt(Lines, Caret)];

    /// <summary>Starts on <paramref name="text"/> — the caret at its end — forgetting what could be undone, unless it is the text already there.</summary>
    public void Load(string text)
    {
        if (text == editing.Text)
        {
            return;
        }
        editing.SetText(text);
        history.Clear();
    }

    private NoteEdit Now => new(editing.Text, editing.Caret, editing.Anchor);

    private void Apply(NoteEdit edit)
    {
        editing.SetText(edit.Text);
        editing.MoveTo(edit.Anchor, extend: false);
        editing.MoveTo(edit.Caret, extend: true);
    }

    /// <summary>An edit made to the text, as one of <paramref name="kind"/>: true when the text changed.</summary>
    private bool Change(NoteHistory.Kind kind, Func<bool> edit)
    {
        var before = Now;
        if (!edit())
        {
            return false;
        }
        if (editing.Text == before.Text)
        {
            return false;
        }
        history.Will(before, kind);
        history.Did(editing.Caret);
        return true;
    }

    private bool Change(NoteHistory.Kind kind, NoteEdit? edit) => edit is { } made && Change(kind, () =>
    {
        Apply(made);
        return true;
    });

    /// <summary>What was typed, in place of the selection.</summary>
    public bool Type(string typed)
    {
        // A space or a line after a word ends the word's step, as a word processor's undo does.
        if (typed.Length > 0 && char.IsWhiteSpace(typed[0]) && Caret > 0 && !char.IsWhiteSpace(Text[Caret - 1]))
        {
            history.Break();
        }
        return Change(NoteHistory.Kind.Typing, () => editing.Insert(typed));
    }

    /// <summary>Enter: a new line, going on with the list or the quote the caret is in (<see cref="NoteEdits.Enter"/>).</summary>
    public bool Enter()
    {
        history.Break();
        if (HasSelection)
        {
            _ = Change(NoteHistory.Kind.Other, () => editing.Delete(word: false));
        }
        return Change(NoteHistory.Kind.Other, NoteEdits.Enter(Text, Caret)) || Change(NoteHistory.Kind.Other, () => editing.Insert("\n"));
    }

    public bool Backspace(bool word)
    {
        if (!HasSelection && !word && NoteEdits.Backspace(Text, Caret) is { } lead)
        {
            history.Break();
            return Change(NoteHistory.Kind.Other, lead);
        }
        return Change(NoteHistory.Kind.Deleting, () => editing.Backspace(word));
    }

    public bool Delete(bool word) => Change(NoteHistory.Kind.Deleting, () => editing.Delete(word));

    /// <summary>
    /// Tab or Shift+Tab: the list items the caret or the selection is on one level in or out. False
    /// in <paramref name="took"/> where it is on none, and Tab is the folder's.
    /// </summary>
    public bool Indent(bool outdent, out bool took)
    {
        var edit = NoteEdits.Indent(Text, Caret, Anchor, outdent);
        took = edit is not null;
        history.Break();
        return Change(NoteHistory.Kind.Other, edit);
    }

    /// <summary>Ctrl+V: the clipboard's text, as it is, in place of the selection.</summary>
    public bool Paste(string text)
    {
        history.Break();
        return Change(NoteHistory.Kind.Other, () => editing.Paste(text));
    }

    /// <summary>Ctrl+C: the selection's text, its Markdown as written; null where nothing is selected.</summary>
    public string? Copy() => editing.Copy();

    /// <summary>Ctrl+X: the selection's text, taken out.</summary>
    public string? Cut()
    {
        string? taken = null;
        history.Break();
        _ = Change(NoteHistory.Kind.Other, () => (taken = editing.Cut()) is not null);
        return taken;
    }

    /// <summary>The task box at <paramref name="box"/> ticked or cleared, the caret where it was.</summary>
    public bool Toggle(int box)
    {
        history.Break();
        var ticked = Markdown.Toggled(Text, box);
        return Change(NoteHistory.Kind.Other, new NoteEdit(ticked, Caret, Anchor));
    }

    /// <summary>A toolbar button or its key: <paramref name="format"/> toggled where the caret or the selection is (<see cref="NoteFormats.Apply(string, IReadOnlyList{MdLine}, int, int, NoteFormat)"/>), one step to undo.</summary>
    public bool Format(NoteFormat format)
    {
        history.Break();
        var done = Change(NoteHistory.Kind.Other, NoteFormats.Apply(Text, Lines, Caret, Anchor, format));
        history.Break();
        return done;
    }

    /// <summary>Whether <paramref name="format"/> is on where the caret is: its button shows pressed.</summary>
    public bool IsOn(NoteFormat format) => NoteFormats.IsOn(Text, Lines, Caret, Anchor, format);

    public bool Undo()
    {
        if (history.Undo(Now) is not { } step)
        {
            return false;
        }
        Apply(step);
        return true;
    }

    public bool Redo()
    {
        if (history.Redo(Now) is not { } step)
        {
            return false;
        }
        Apply(step);
        return true;
    }

    // --- The caret ------------------------------------------------------------------------------------

    public void Left(bool extend, bool word)
    {
        editing.Left(extend, word);
        history.Break();
    }

    public void Right(bool extend, bool word)
    {
        editing.Right(extend, word);
        history.Break();
    }

    public void MoveTo(int index, bool extend)
    {
        editing.MoveTo(index, extend);
        history.Break();
    }

    public void Home(bool extend) => MoveTo(0, extend);

    public void End(bool extend) => MoveTo(Text.Length, extend);

    public void SelectAll()
    {
        editing.SelectAll();
        history.Break();
    }

    /// <summary>A double click: the word at <paramref name="index"/>.</summary>
    public void SelectWordAt(int index)
    {
        editing.SelectWordAt(index);
        history.Break();
    }

    /// <summary>A triple click: the line at <paramref name="index"/>, its words without its \n.</summary>
    public void SelectLineAt(int index)
    {
        var line = Lines[Markdown.LineAt(Lines, index)];
        editing.MoveTo(line.Start, extend: false);
        editing.MoveTo(line.End, extend: true);
        history.Break();
    }
}
