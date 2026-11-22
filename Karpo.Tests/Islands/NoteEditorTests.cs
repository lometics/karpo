// The note's live editor without a window (Karpo.Dock\Notes\NoteEditor.cs): Enter going on with a
// list, Tab indenting, Backspace taking a marker away, the caret going through the marks, and what
// is undone. A caret in the texts below is written "|", a selection's other end "^".

using Karpo.Dock;

namespace Karpo.Tests.Islands;

public sealed class NoteEditorTests
{
    /// <summary>The text with "|" (and "^") taken out, and where they stood.</summary>
    private static (string Text, int Caret, int Anchor) Marked(string marked)
    {
        var caret = marked.IndexOf('|');
        var text = marked.Replace("|", "");
        var anchor = text.IndexOf('^');
        if (anchor >= 0)
        {
            text = text.Replace("^", "");
            if (anchor < caret)
            {
                caret--;
            }
        }
        else
        {
            anchor = caret;
        }
        return (text, caret, anchor);
    }

    private static string Mark(NoteEdit edit)
    {
        var text = edit.Text.Insert(edit.Caret, "|");
        return edit.Anchor == edit.Caret ? text : text.Insert(edit.Anchor + (edit.Anchor > edit.Caret ? 1 : 0), "^");
    }

    private static string? Enter(string marked)
    {
        var (text, caret, _) = Marked(marked);
        return NoteEdits.Enter(text, caret) is { } edit ? Mark(edit) : null;
    }

    [Theory]
    [InlineData("- milk|", "- milk\n- |")]
    [InlineData("* milk|", "* milk\n* |")]
    [InlineData("- [x] milk|", "- [x] milk\n- [ ] |")]
    [InlineData("3. three|", "3. three\n4. |")]
    [InlineData("9) nine|", "9) nine\n10) |")]
    [InlineData("  - nested|", "  - nested\n  - |")]
    [InlineData("> - quoted|", "> - quoted\n> - |")]
    [InlineData("> words|", "> words\n> |")]
    [InlineData("- mi|lk", "- mi\n- |lk")]
    [InlineData("- |milk", "- \n- |milk")]
    public void EnterGoesOnWithTheList(string before, string after) => Assert.Equal(after, Enter(before));

    [Theory]
    [InlineData("- milk\n- |", "- milk\n|")]
    [InlineData("- milk\n  - |", "- milk\n- |")]
    [InlineData("- [ ] |", "|")]
    [InlineData("1. |", "|")]
    [InlineData("> - |", "> |")]
    [InlineData("> words\n> |", "> words\n|")]
    public void EnterOnAnEmptyItemEndsItsList(string before, string after) => Assert.Equal(after, Enter(before));

    [Theory]
    [InlineData("plain|")]
    [InlineData("# Heading|")]
    [InlineData("|- before the marker")]
    [InlineData("```\ncode|\n```")]
    public void EnterIsANewLineElsewhere(string before) => Assert.Null(Enter(before));

    [Theory]
    [InlineData("9.", "10.")]
    [InlineData("1)", "2)")]
    [InlineData("999999999.", "999999999.")]
    public void TheNextNumber(string marker, string next) => Assert.Equal(next, NoteEdits.Next(marker));

    private static string? Indent(string marked, bool outdent)
    {
        var (text, caret, anchor) = Marked(marked);
        return NoteEdits.Indent(text, caret, anchor, outdent) is { } edit ? Mark(edit) : null;
    }

    [Theory]
    [InlineData("- a\n- b|", false, "- a\n  - b|")]
    [InlineData("- a\n  - b|", true, "- a\n- b|")]
    [InlineData("- a\n   - b|", true, "- a\n - b|")]
    [InlineData("- a|", true, "- a|")]
    [InlineData("> - a|", false, ">   - a|")]
    [InlineData("- ^a\ntext\n- b|", false, "  - ^a\ntext\n  - b|")]
    [InlineData("  - a^\n  - b|", true, "- a^\n- b|")]
    public void TabIndentsTheItems(string before, bool outdent, string after) => Assert.Equal(after, Indent(before, outdent));

    [Theory]
    [InlineData("text|")]
    [InlineData("# heading|")]
    public void TabOffAListIsNotTheNotes(string before) => Assert.Null(Indent(before, outdent: false));

    private static string? Backspace(string marked)
    {
        var (text, caret, _) = Marked(marked);
        return NoteEdits.Backspace(text, caret) is { } edit ? Mark(edit) : null;
    }

    [Theory]
    [InlineData("- |milk", "|milk")]
    [InlineData("- [ ] |milk", "|milk")]
    [InlineData("12. |milk", "|milk")]
    [InlineData("  - |milk", "- |milk")]
    [InlineData("> |words", "|words")]
    [InlineData("> > |words", "> |words")]
    [InlineData("> - |milk", "> |milk")]
    public void BackspaceAtTheWordsTakesTheMarkerAway(string before, string after) => Assert.Equal(after, Backspace(before));

    [Theory]
    [InlineData("- m|ilk")]
    [InlineData("|- milk")]
    [InlineData("# |Heading")]
    [InlineData("plain|")]
    public void BackspaceElsewhereIsACharacter(string before) => Assert.Null(Backspace(before));

    // --- The editor -----------------------------------------------------------------------------------

    private static NoteEditor With(string marked)
    {
        var (text, caret, anchor) = Marked(marked);
        var editor = new NoteEditor();
        editor.Load(text);
        editor.MoveTo(anchor, extend: false);
        editor.MoveTo(caret, extend: true);
        return editor;
    }

    private static string Mark(NoteEditor editor) => Mark(new NoteEdit(editor.Text, editor.Caret, editor.Anchor));

    private static void Type(NoteEditor editor, string words)
    {
        foreach (var c in words)
        {
            Assert.True(editor.Type(c.ToString()));
        }
    }

    [Fact]
    public void TypingIsUndoneAWordAtATime()
    {
        var editor = With("|");
        Type(editor, "one two");
        Assert.True(editor.Undo());
        Assert.Equal("one|", Mark(editor));
        Assert.True(editor.Undo());
        Assert.Equal("|", Mark(editor));
        Assert.False(editor.Undo());
        Assert.True(editor.Redo());
        Assert.True(editor.Redo());
        Assert.Equal("one two|", Mark(editor));
        Assert.False(editor.Redo());
    }

    [Fact]
    public void ACaretMovedOrAnotherKindOfEditIsAStepOfItsOwn()
    {
        var editor = With("ab|");
        Type(editor, "c");
        editor.Left(extend: false, word: false);
        Type(editor, "X");
        Assert.Equal("abX|c", Mark(editor));
        Assert.True(editor.Undo());
        Assert.Equal("ab|c", Mark(editor));
        Assert.True(editor.Backspace(word: false));
        Assert.True(editor.Backspace(word: false));
        Assert.Equal("|c", Mark(editor));
        Assert.True(editor.Undo());
        Assert.Equal("ab|c", Mark(editor));
        // Something new done after an undo: what was undone cannot come back.
        Type(editor, "!");
        Assert.False(editor.Redo());
    }

    [Fact]
    public void EnterTabAndTicksAreUndone()
    {
        var editor = With("- [ ] milk|");
        Assert.True(editor.Enter());
        Assert.Equal("- [ ] milk\n- [ ] |", Mark(editor));
        Type(editor, "eggs");
        Assert.True(editor.Indent(outdent: false, out var took));
        Assert.True(took);
        Assert.Equal("- [ ] milk\n  - [ ] eggs|", Mark(editor));
        Assert.True(editor.Toggle(editor.CaretLine.Box));
        Assert.Equal("- [ ] milk\n  - [x] eggs|", Mark(editor));
        Assert.True(editor.Undo());
        Assert.True(editor.Undo());
        Assert.Equal("- [ ] milk\n- [ ] eggs|", Mark(editor));
        Assert.True(editor.Undo());
        Assert.True(editor.Undo());
        Assert.Equal("- [ ] milk|", Mark(editor));
    }

    [Fact]
    public void EnterReplacesTheSelection()
    {
        var editor = With("- mi^lk and |eggs");
        Assert.True(editor.Enter());
        Assert.Equal("- mi\n- |eggs", Mark(editor));
    }

    [Fact]
    public void TabOffAListIsNotTaken()
    {
        var editor = With("words|");
        Assert.False(editor.Indent(outdent: false, out var took));
        Assert.False(took);
        Assert.Equal("words|", Mark(editor));
    }

    [Fact]
    public void CutAndPasteArePlainText()
    {
        var editor = With("a ^**b**| c");
        Assert.Equal("**b**", editor.Cut());
        Assert.Equal("a | c", Mark(editor));
        Assert.True(editor.Paste("x\r\ny\tz"));
        Assert.Equal("a x\ny    z| c", Mark(editor));
        Assert.True(editor.Undo());
        Assert.True(editor.Undo());
        Assert.Equal("a ^**b**| c", Mark(editor));
    }

    [Fact]
    public void ALineIsSelectedWhole()
    {
        var editor = With("one\nt|wo\nthree");
        editor.SelectLineAt(editor.Caret);
        Assert.Equal("two", editor.SelectedText);
    }

    [Fact]
    public void LoadingTheSameTextKeepsTheCaretAndWhatCanBeUndone()
    {
        var editor = With("ab|");
        Type(editor, "c");
        editor.MoveTo(1, extend: false);
        editor.Load("abc");
        Assert.Equal(1, editor.Caret);
        Assert.True(editor.History.CanUndo);
        editor.Load("other");
        Assert.False(editor.History.CanUndo);
        Assert.Equal(5, editor.Caret);
    }

    [Fact]
    public void TheHistoryKeepsItsMost()
    {
        var editor = With("|");
        for (var index = 0; index < NoteHistory.Most + 50; index++)
        {
            Assert.True(editor.Enter());
        }
        var undone = 0;
        while (editor.Undo())
        {
            undone++;
        }
        Assert.Equal(NoteHistory.Most, undone);
    }

    /// <summary>The marks show as text (§321), so the arrows go through them a character at a time.</summary>
    [Fact]
    public void ArrowsGoThroughTheMarksACharacterAtATime()
    {
        var editor = With("first\n- [ ] |milk");
        editor.Left(extend: false, word: false);
        Assert.Equal("first\n- [ ]| milk", Mark(editor));
        for (var step = 0; step < 5; step++)
        {
            editor.Left(extend: false, word: false);
        }
        Assert.Equal("first\n|- [ ] milk", Mark(editor));
        editor.Left(extend: false, word: false);
        Assert.Equal("first|\n- [ ] milk", Mark(editor));
        editor.Right(extend: false, word: false);
        editor.Right(extend: false, word: false);
        Assert.Equal("first\n-| [ ] milk", Mark(editor));
    }
}
