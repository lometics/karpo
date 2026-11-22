// What a one-line text field holds: the text, the caret and the selection, and
// the edits a person makes with the keyboard and the pointer. Pure, so the rules
// are tested without a window (TextField draws it and feeds it the keys).
//
// The caret never stands inside a character a person sees as one: moving and
// Delete go by text elements (an emoji with its modifiers, a letter with its
// accents), Backspace by code points, as in Windows' own edit controls, so a
// wrong accent can be taken back without the letter under it. A password field
// has no words to jump between (Ctrl+arrows go to the ends, as PasswordBox's
// do) and nothing can be copied or cut out of it.

using System.Buffers;
using System.Globalization;
using System.Text;

namespace Karpo.Dock.QuickSettings.Controls;

internal sealed class TextEditing
{
    private string text = "";

    /// <summary>A one-line field's text; <paramref name="password"/> hides nothing here, only what may leave it.</summary>
    public TextEditing(int maxLength = int.MaxValue, bool password = false, bool multiLine = false)
    {
        MaxLength = Math.Max(0, maxLength);
        Password = password;
        MultiLine = multiLine;
    }

    /// <summary>
    /// Whether it keeps its line breaks, each as \n, rather than its first line alone: a note being
    /// edited (NoteTile). Moving between lines is the drawing's, which knows where they wrap.
    /// </summary>
    public bool MultiLine { get; }

    public string Text => text;

    /// <summary>Where the caret is: 0 to <see cref="Text"/>'s length, never inside a character.</summary>
    public int Caret { get; private set; }

    /// <summary>The selection's other end: where it started. Equal to <see cref="Caret"/> when nothing is selected.</summary>
    public int Anchor { get; private set; }

    /// <summary>The most UTF-16 units the text may have, as WinUI's MaxLength counts.</summary>
    public int MaxLength { get; }

    public bool Password { get; }

    public int SelectionStart => Math.Min(Caret, Anchor);

    public int SelectionLength => Math.Abs(Caret - Anchor);

    public bool HasSelection => Caret != Anchor;

    public string SelectedText => text.Substring(SelectionStart, SelectionLength);

    /// <summary>Replaces everything, cut to <see cref="MaxLength"/> and to its first line; the caret goes to the end.</summary>
    public void SetText(string value)
    {
        text = Clip(Clean(value), MaxLength);
        Caret = Anchor = text.Length;
    }

    /// <summary>
    /// What was typed (or pasted), in place of the selection. Control characters are dropped;
    /// what doesn't fit under <see cref="MaxLength"/> is cut, never half a character.
    /// True when the text changed.
    /// </summary>
    public bool Insert(string typed)
    {
        var clean = Clean(typed);
        var start = SelectionStart;
        var removed = SelectionLength;
        var room = MaxLength - (text.Length - removed);
        clean = Clip(clean, Math.Max(0, room));
        if (clean.Length == 0 && removed == 0)
        {
            return false;
        }
        text = string.Concat(text.AsSpan(0, start), clean, text.AsSpan(start + removed));
        Caret = Anchor = start + clean.Length;
        return true;
    }

    /// <summary>The selection, else the code point before the caret (with <paramref name="word"/>: back to the word's start).</summary>
    public bool Backspace(bool word)
    {
        if (HasSelection)
        {
            return RemoveSelection();
        }
        if (Caret == 0)
        {
            return false;
        }
        var from = word ? WordStartBefore(Caret) : PreviousCodePoint(Caret);
        Remove(from, Caret);
        return true;
    }

    /// <summary>The selection, else the character after the caret (with <paramref name="word"/>: up to the next word).</summary>
    public bool Delete(bool word)
    {
        if (HasSelection)
        {
            return RemoveSelection();
        }
        if (Caret == text.Length)
        {
            return false;
        }
        var to = word ? WordStartAfter(Caret) : NextElement(Caret);
        Remove(Caret, to);
        return true;
    }

    /// <summary>
    /// Left arrow: a character back, or a word with <paramref name="word"/>; with <paramref name="extend"/>
    /// the selection grows. Without it, a selection collapses to its start.
    /// </summary>
    public void Left(bool extend, bool word)
    {
        if (!extend && HasSelection && !word)
        {
            Caret = Anchor = SelectionStart;
            return;
        }
        var to = word ? WordStartBefore(Caret) : PreviousElement(Caret);
        Move(to, extend);
    }

    /// <summary>Right arrow, as <see cref="Left"/> the other way: a selection collapses to its end.</summary>
    public void Right(bool extend, bool word)
    {
        if (!extend && HasSelection && !word)
        {
            Caret = Anchor = SelectionStart + SelectionLength;
            return;
        }
        var to = word ? WordStartAfter(Caret) : NextElement(Caret);
        Move(to, extend);
    }

    public void Home(bool extend) => Move(0, extend);

    public void End(bool extend) => Move(text.Length, extend);

    public void SelectAll()
    {
        Anchor = 0;
        Caret = text.Length;
    }

    /// <summary>A click (or a drag, with <paramref name="extend"/>) at <paramref name="index"/>, moved to the nearest character's edge.</summary>
    public void MoveTo(int index, bool extend) => Move(Snap(Math.Clamp(index, 0, text.Length)), extend);

    /// <summary>A double click at <paramref name="index"/>: the word there (or the run of spaces or marks), whole.</summary>
    public void SelectWordAt(int index)
    {
        if (Password || text.Length == 0)
        {
            SelectAll();
            return;
        }
        index = Snap(Math.Clamp(index, 0, text.Length));
        // The character under the pointer decides what kind of run it is.
        var at = index < text.Length ? index : PreviousElement(index);
        var kind = Kind(at);
        var start = at;
        while (start > 0)
        {
            var before = PreviousElement(start);
            if (Kind(before) != kind)
            {
                break;
            }
            start = before;
        }
        var end = at;
        while (end < text.Length && Kind(end) == kind)
        {
            end = NextElement(end);
        }
        Anchor = start;
        Caret = end;
    }

    /// <summary>What Ctrl+C puts on the clipboard: the selection; nothing from a password.</summary>
    public string? Copy() => Password || !HasSelection ? null : SelectedText;

    /// <summary>Ctrl+X: the selection, taken out of the text; nothing from a password.</summary>
    public string? Cut()
    {
        if (Copy() is not { } taken)
        {
            return null;
        }
        RemoveSelection();
        return taken;
    }

    /// <summary>Ctrl+V: the clipboard's first line, as if typed.</summary>
    public bool Paste(string clipboard) => Insert(clipboard);

    // --- The rules -----------------------------------------------------------

    /// <summary>What may be typed in: the first line, or every line where it is multi-line.</summary>
    private string Clean(string value) => MultiLine ? Lines(value) : OneLine(value);

    /// <summary>Every line, each break a \n, without other control characters; a tab counts as four spaces.</summary>
    internal static string Lines(string value)
    {
        var clean = new StringBuilder(value.Length);
        foreach (var c in value.ReplaceLineEndings("\n"))
        {
            if (c == '\n')
            {
                clean.Append('\n');
            }
            else if (c == '\t')
            {
                clean.Append("    ");
            }
            else if (!char.IsControl(c))
            {
                clean.Append(c);
            }
        }
        return clean.ToString();
    }

    /// <summary>The first line, without control characters; a tab counts as a space.</summary>
    internal static string OneLine(string value)
    {
        var line = value.AsSpan();
        var end = line.IndexOfAny('\r', '\n');
        if (end >= 0)
        {
            line = line[..end];
        }
        var clean = new StringBuilder(line.Length);
        foreach (var c in line)
        {
            if (c == '\t')
            {
                clean.Append(' ');
            }
            else if (!char.IsControl(c))
            {
                clean.Append(c);
            }
        }
        return clean.ToString();
    }

    /// <summary>At most <paramref name="length"/> units of <paramref name="value"/>, never ending in half a surrogate pair.</summary>
    internal static string Clip(string value, int length)
    {
        if (value.Length <= length)
        {
            return value;
        }
        if (length > 0 && char.IsHighSurrogate(value[length - 1]))
        {
            length--;
        }
        return value[..length];
    }

    private void Move(int to, bool extend)
    {
        Caret = to;
        if (!extend)
        {
            Anchor = to;
        }
    }

    private bool RemoveSelection()
    {
        if (!HasSelection)
        {
            return false;
        }
        Remove(SelectionStart, SelectionStart + SelectionLength);
        return true;
    }

    private void Remove(int from, int to)
    {
        text = string.Concat(text.AsSpan(0, from), text.AsSpan(to));
        Caret = Anchor = from;
    }

    /// <summary>The nearest text element boundary at or before <paramref name="index"/>.</summary>
    private int Snap(int index)
    {
        var at = 0;
        while (at < text.Length)
        {
            var next = at + StringInfo.GetNextTextElementLength(text, at);
            if (next > index)
            {
                return index - at <= next - index ? at : next;
            }
            at = next;
        }
        return text.Length;
    }

    private int NextElement(int index) =>
        index >= text.Length ? text.Length : index + Math.Max(1, StringInfo.GetNextTextElementLength(text, index));

    private int PreviousElement(int index)
    {
        if (index <= 0)
        {
            return 0;
        }
        var at = 0;
        while (true)
        {
            var next = NextElement(at);
            if (next >= index)
            {
                return at;
            }
            at = next;
        }
    }

    private int PreviousCodePoint(int index) =>
        index >= 2 && char.IsLowSurrogate(text[index - 1]) && char.IsHighSurrogate(text[index - 2]) ? index - 2 : index - 1;

    private enum Run
    {
        Space,
        Word,
        Mark,
    }

    private Run Kind(int index)
    {
        // Half a surrogate pair (pasted from somewhere broken) is no letter.
        if (Rune.DecodeFromUtf16(text.AsSpan(index), out var rune, out _) != OperationStatus.Done)
        {
            return Run.Mark;
        }
        if (Rune.IsWhiteSpace(rune))
        {
            return Run.Space;
        }
        return Rune.IsLetterOrDigit(rune) || rune.Value == '_' ? Run.Word : Run.Mark;
    }

    /// <summary>Ctrl+Left: the start of the word before the caret (spaces before the caret skipped first).</summary>
    private int WordStartBefore(int index)
    {
        if (Password)
        {
            return 0;
        }
        var at = index;
        while (at > 0 && Kind(PreviousElement(at)) == Run.Space)
        {
            at = PreviousElement(at);
        }
        if (at == 0)
        {
            return 0;
        }
        var kind = Kind(PreviousElement(at));
        while (at > 0 && Kind(PreviousElement(at)) == kind)
        {
            at = PreviousElement(at);
        }
        return at;
    }

    /// <summary>Ctrl+Right: the start of the next word, past the rest of this one and the spaces after it.</summary>
    private int WordStartAfter(int index)
    {
        if (Password)
        {
            return text.Length;
        }
        var at = index;
        if (at < text.Length && Kind(at) != Run.Space)
        {
            var kind = Kind(at);
            while (at < text.Length && Kind(at) == kind)
            {
                at = NextElement(at);
            }
        }
        while (at < text.Length && Kind(at) == Run.Space)
        {
            at = NextElement(at);
        }
        return at;
    }
}
