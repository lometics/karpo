// The text field's editing rules: typing, deleting, moving and selecting by
// characters and words, the clipboard, the length limit, and what a password
// field keeps to itself.

using Karpo.Dock.QuickSettings.Controls;

namespace Karpo.Tests.QuickSettings;

public sealed class TextEditingTests
{
    private static TextEditing With(string text, int caret, int? anchor = null, int maxLength = int.MaxValue, bool password = false)
    {
        var editing = new TextEditing(maxLength, password);
        editing.SetText(text);
        editing.MoveTo(anchor ?? caret, extend: false);
        editing.MoveTo(caret, extend: true);
        return editing;
    }

    [Fact]
    public void TypingInsertsAtTheCaret()
    {
        var editing = With("helo", 3);
        Assert.True(editing.Insert("l"));
        Assert.Equal("hello", editing.Text);
        Assert.Equal(4, editing.Caret);
        Assert.False(editing.HasSelection);
    }

    [Fact]
    public void TypingReplacesTheSelection()
    {
        var editing = With("hello world", 11, anchor: 6);
        Assert.Equal("world", editing.SelectedText);
        editing.Insert("there");
        Assert.Equal("hello there", editing.Text);
        Assert.Equal(11, editing.Caret);
    }

    [Fact]
    public void ControlCharactersAndLineBreaksNeverGetIn()
    {
        var editing = With("", 0);
        editing.Insert("ab\tc");
        Assert.Equal("ab c", editing.Text);
        editing.Paste("first line\r\nsecond line");
        Assert.Equal("ab cfirst line", editing.Text);
        Assert.False(editing.Insert(""));
    }

    [Fact]
    public void BackspaceAndDeleteTakeOneCharacter()
    {
        var editing = With("abc", 2);
        Assert.True(editing.Backspace(word: false));
        Assert.Equal("ac", editing.Text);
        Assert.Equal(1, editing.Caret);
        Assert.True(editing.Delete(word: false));
        Assert.Equal("a", editing.Text);
        Assert.False(editing.Delete(word: false));
        editing.Home(extend: false);
        Assert.False(editing.Backspace(word: false));
    }

    [Fact]
    public void AnEmojiIsOneCharacterForTheCaret()
    {
        // U+1F44D (a surrogate pair) with a skin tone modifier (another pair): one text element.
        var thumb = "\U0001F44D\U0001F3FD";
        var editing = With("a" + thumb + "b", 1);
        editing.Right(extend: false, word: false);
        Assert.Equal(1 + thumb.Length, editing.Caret);
        editing.Left(extend: false, word: false);
        Assert.Equal(1, editing.Caret);
        Assert.True(editing.Delete(word: false));
        Assert.Equal("ab", editing.Text);
    }

    [Fact]
    public void BackspaceTakesAnAccentWithoutItsLetter()
    {
        var editing = With("é", 2);
        Assert.True(editing.Backspace(word: false));
        Assert.Equal("e", editing.Text);
        // A surrogate pair goes whole.
        editing = With("x\U0001F600", 3);
        Assert.True(editing.Backspace(word: false));
        Assert.Equal("x", editing.Text);
    }

    [Fact]
    public void AClickNeverLandsInsideACharacter()
    {
        var editing = With("a\U0001F600b", 0);
        editing.MoveTo(2, extend: false);
        Assert.True(editing.Caret is 1 or 3);
        editing.MoveTo(99, extend: false);
        Assert.Equal(editing.Text.Length, editing.Caret);
        editing.MoveTo(-5, extend: false);
        Assert.Equal(0, editing.Caret);
    }

    [Fact]
    public void WordMovesStopAtWordStarts()
    {
        var editing = With("the quick, brown fox", 0);
        editing.Right(extend: false, word: true);
        Assert.Equal(4, editing.Caret);
        editing.Right(extend: false, word: true);
        Assert.Equal(9, editing.Caret);
        editing.Right(extend: false, word: true);
        Assert.Equal(11, editing.Caret);
        editing.End(extend: false);
        editing.Left(extend: false, word: true);
        Assert.Equal(17, editing.Caret);
        editing.Left(extend: false, word: true);
        Assert.Equal(11, editing.Caret);
        editing.Left(extend: false, word: true);
        Assert.Equal(9, editing.Caret);
    }

    [Fact]
    public void WordDeletes()
    {
        var editing = With("the quick brown", 15);
        Assert.True(editing.Backspace(word: true));
        Assert.Equal("the quick ", editing.Text);
        editing.Home(extend: false);
        Assert.True(editing.Delete(word: true));
        Assert.Equal("quick ", editing.Text);
    }

    [Fact]
    public void ShiftExtendsAndAnArrowCollapses()
    {
        var editing = With("abcdef", 2);
        editing.Right(extend: true, word: false);
        editing.Right(extend: true, word: false);
        Assert.Equal("cd", editing.SelectedText);
        editing.Left(extend: false, word: false);
        Assert.Equal(2, editing.Caret);
        Assert.False(editing.HasSelection);
        editing.End(extend: true);
        Assert.Equal("cdef", editing.SelectedText);
        editing.Right(extend: false, word: false);
        Assert.Equal(6, editing.Caret);
        Assert.False(editing.HasSelection);
    }

    [Fact]
    public void SelectAllAndTheClipboard()
    {
        var editing = With("secret", 0);
        editing.SelectAll();
        Assert.Equal("secret", editing.Copy());
        Assert.Equal("secret", editing.Cut());
        Assert.Equal("", editing.Text);
        Assert.Null(editing.Copy());
    }

    [Fact]
    public void APasswordStaysInItsField()
    {
        var editing = With("hunter two", 3, password: true);
        editing.SelectAll();
        Assert.Null(editing.Copy());
        Assert.Null(editing.Cut());
        Assert.Equal("hunter two", editing.Text);
        // No words in a password: Ctrl+arrows go to the ends.
        editing.MoveTo(3, extend: false);
        editing.Right(extend: false, word: true);
        Assert.Equal(10, editing.Caret);
        editing.Left(extend: false, word: true);
        Assert.Equal(0, editing.Caret);
        // Pasting into one is fine.
        Assert.True(editing.Paste("x"));
        Assert.Equal("xhunter two", editing.Text);
    }

    [Fact]
    public void TheLengthLimitCutsWhatDoesntFit()
    {
        var editing = With("abc", 3, maxLength: 5);
        editing.Insert("defgh");
        Assert.Equal("abcde", editing.Text);
        Assert.False(editing.Insert("x"));
        // Never half a surrogate pair at the end.
        editing = With("abcd", 4, maxLength: 5);
        editing.Insert("\U0001F600");
        Assert.Equal("abcd", editing.Text);
        editing.SetText("0123456789");
        Assert.Equal("01234", editing.Text);
        Assert.Equal(5, editing.Caret);
    }

    [Fact]
    public void ADoubleClickSelectsAWord()
    {
        var editing = With("hello big world", 0);
        editing.SelectWordAt(7);
        Assert.Equal("big", editing.SelectedText);
        editing.SelectWordAt(5);
        Assert.Equal(" ", editing.SelectedText);
        editing.SelectWordAt(15);
        Assert.Equal("world", editing.SelectedText);
    }

    [Fact]
    public void BrokenSurrogatesDontThrow()
    {
        var editing = With("a\uD800b", 0);
        editing.Right(extend: false, word: true);
        editing.SelectWordAt(1);
        editing.End(extend: false);
        editing.Backspace(word: true);
        Assert.NotNull(editing.Text);
    }

    // --- Multi-line: a note being edited (InlineEditor) ---------------------------------------------

    [Fact]
    public void AMultiLineTextKeepsItsLines()
    {
        var editing = new TextEditing(multiLine: true);
        editing.SetText("one\r\ntwo\rthree\tfour\u0007");
        Assert.Equal("one\ntwo\nthree    four", editing.Text);
        editing.Insert("\n- [ ] ");
        Assert.EndsWith("four\n- [ ] ", editing.Text);
        // A one-line field still takes the first line alone.
        var single = new TextEditing();
        single.SetText("one\ntwo");
        Assert.Equal("one", single.Text);
    }

    [Fact]
    public void ALineBreakIsOneCharacterToMoveOverAndDelete()
    {
        var editing = new TextEditing(multiLine: true);
        editing.SetText("ab\ncd");
        editing.MoveTo(3, extend: false);
        Assert.True(editing.Backspace(word: false));
        Assert.Equal("abcd", editing.Text);
        editing.Insert("\n");
        editing.Left(extend: false, word: false);
        Assert.Equal(2, editing.Caret);
        Assert.True(editing.Delete(word: false));
        Assert.Equal("abcd", editing.Text);
    }

    [Fact]
    public void APasteKeepsItsLinesInAMultiLineText()
    {
        var editing = new TextEditing(maxLength: 9, multiLine: true);
        Assert.True(editing.Paste("a\r\nb\nccccccccc"));
        Assert.Equal("a\nb\nccccc", editing.Text);
    }
}
