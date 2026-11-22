// What the note's toolbar does to the Markdown (Karpo.Dock\Notes\NoteFormat.cs): each format on
// and off again, over one line and several, with nothing selected, and what its button shows. A
// caret in the texts below is written "|", a selection's other end "^".

using Karpo.Dock;

namespace Karpo.Tests.Islands;

public sealed class NoteFormatTests
{
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

    private static string? Apply(string marked, NoteFormat format)
    {
        var (text, caret, anchor) = Marked(marked);
        return NoteFormats.Apply(text, caret, anchor, format) is { } edit ? Mark(edit) : null;
    }

    private static bool On(string marked, NoteFormat format)
    {
        var (text, caret, anchor) = Marked(marked);
        return NoteFormats.IsOn(text, Markdown.Lines(text), caret, anchor, format);
    }

    /// <summary>Applied twice, the text and the selection are back where they were.</summary>
    private static void RoundTrips(string marked, NoteFormat format)
    {
        var once = Apply(marked, format);
        Assert.NotNull(once);
        Assert.Equal(marked, Apply(once!, format));
    }

    // --- Inline ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("some ^words| here", NoteFormat.Bold, "some **^words|** here")]
    [InlineData("some |words^ here", NoteFormat.Italic, "some *|words^* here")]
    [InlineData("some ^words| here", NoteFormat.Strike, "some ~~^words|~~ here")]
    [InlineData("some ^words| here", NoteFormat.Code, "some `^words|` here")]
    // The spaces at a selection's ends stay outside the marks, which stand against words.
    [InlineData("some^ words |here", NoteFormat.Bold, "some^ **words** |here")]
    // A list item's or a heading's words, not its lead.
    [InlineData("^- item|", NoteFormat.Bold, "^- **item|**")]
    [InlineData("# ^Title|", NoteFormat.Italic, "# *^Title|*")]
    public void ASelectionIsWrapped(string before, NoteFormat format, string after) => Assert.Equal(after, Apply(before, format));

    [Theory]
    [InlineData("some **^words|** here", NoteFormat.Bold, "some ^words| here")]
    [InlineData("some **wo|rds** here", NoteFormat.Bold, "some wo|rds here")]
    [InlineData("some __wo|rds__ here", NoteFormat.Bold, "some wo|rds here")]
    [InlineData("some *wo|rds* here", NoteFormat.Italic, "some wo|rds here")]
    [InlineData("some _wo|rds_ here", NoteFormat.Italic, "some wo|rds here")]
    [InlineData("some ~~wo|rds~~ here", NoteFormat.Strike, "some wo|rds here")]
    [InlineData("some `wo|rds` here", NoteFormat.Code, "some wo|rds here")]
    // The marks selected with the words.
    [InlineData("some ^**words**| here", NoteFormat.Bold, "some ^words| here")]
    // Part of a bold run: the run's bold goes, all of it.
    [InlineData("**some ^wor|ds**", NoteFormat.Bold, "some ^wor|ds")]
    // Bold and italic both: the other one stays.
    [InlineData("***wo|rds***", NoteFormat.Bold, "*wo|rds*")]
    [InlineData("***wo|rds***", NoteFormat.Italic, "**wo|rds**")]
    public void OnBoldBoldGoes(string before, NoteFormat format, string after) => Assert.Equal(after, Apply(before, format));

    [Theory]
    [InlineData("some |", NoteFormat.Bold, "some **|**")]
    [InlineData("|", NoteFormat.Italic, "*|*")]
    [InlineData("a | b", NoteFormat.Strike, "a ~~|~~ b")]
    [InlineData("x|", NoteFormat.Code, "x`|`")]
    // Before a list's marker, the pair goes after it.
    [InlineData("|- item", NoteFormat.Bold, "- **|**item")]
    public void NothingSelectedPutsInAPairWithTheCaretBetween(string before, NoteFormat format, string after) => Assert.Equal(after, Apply(before, format));

    [Theory]
    [InlineData("some **|**", NoteFormat.Bold, "some |")]
    [InlineData("*|*", NoteFormat.Italic, "|")]
    [InlineData("a ~~|~~ b", NoteFormat.Strike, "a | b")]
    [InlineData("x`|`", NoteFormat.Code, "x|")]
    public void AnEmptyPairGoesAgain(string before, NoteFormat format, string after)
    {
        Assert.True(On(before, format));
        Assert.Equal(after, Apply(before, format));
    }

    [Fact]
    public void TheEmptyBoldPairIsNotItalic() => Assert.False(On("a **|** b", NoteFormat.Italic));

    [Fact]
    public void ASelectionOverLinesIsWrappedLineByLine() =>
        Assert.Equal("**^one**\n\n- **two**\n**thr|**ee", Apply("^one\n\n- two\nthr|ee", NoteFormat.Bold));

    [Fact]
    public void ASelectionOverLinesAllBoldLosesItsBold() =>
        Assert.Equal("^one\ntwo|", Apply("^**one**\n**two**|", NoteFormat.Bold));

    [Fact]
    public void ASelectionOverLinesPartBoldIsMadeAllBold() =>
        Assert.Equal("^**one**\n**two|**", Apply("^**one**\ntwo|", NoteFormat.Bold));

    [Fact]
    public void ACodeBlockIsLeftAlone() => Assert.Null(Apply("```\nco|de\n```", NoteFormat.Bold));

    [Theory]
    [InlineData("some ^words| here", NoteFormat.Bold)]
    [InlineData("some ^words| here", NoteFormat.Italic)]
    [InlineData("some ^words| here", NoteFormat.Strike)]
    [InlineData("some ^words| here", NoteFormat.Code)]
    [InlineData("some |", NoteFormat.Bold)]
    [InlineData("^one\n\n- two\nthr|ee", NoteFormat.Italic)]
    public void InlineFormatsRoundTrip(string marked, NoteFormat format) => RoundTrips(marked, format);

    [Theory]
    [InlineData("**wo|rds**", NoteFormat.Bold, true)]
    [InlineData("**words**|", NoteFormat.Bold, false)]
    [InlineData("|**words**", NoteFormat.Bold, false)]
    [InlineData("**words|**", NoteFormat.Bold, true)]
    [InlineData("***wo|rds***", NoteFormat.Italic, true)]
    [InlineData("**wo|rds**", NoteFormat.Italic, false)]
    [InlineData("~~wo|rds~~", NoteFormat.Strike, true)]
    [InlineData("`wo|rds`", NoteFormat.Code, true)]
    [InlineData("^**one** two|", NoteFormat.Bold, false)]
    public void TheButtonShowsWhetherItIsOn(string marked, NoteFormat format, bool on) => Assert.Equal(on, On(marked, format));

    // --- A link ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("see |", "see [|]()")]
    [InlineData("see ^docs|", "see [docs](|)")]
    [InlineData("see ^https://karpo.app|", "see [|](https://karpo.app)")]
    [InlineData("see ^www.karpo.app|", "see [|](www.karpo.app)")]
    [InlineData("see [do|cs](https://karpo.app) now", "see do|cs now")]
    [InlineData("see ^[docs](https://karpo.app)| now", "see ^docs| now")]
    [InlineData("see [do|cs]() now", "see do|cs now")]
    public void ALinkIsMadeAndTakenAway(string before, string after) => Assert.Equal(after, Apply(before, NoteFormat.Link));

    [Fact]
    public void ALinkOverLinesIsLeftAlone() => Assert.Null(Apply("^one\ntwo|", NoteFormat.Link));

    [Theory]
    [InlineData("[do|cs](https://karpo.app)", true)]
    [InlineData("[docs](https://karpo.app)|", false)]
    [InlineData("https://kar|po.app", false)]
    [InlineData("![pic|ture](a.png)", false)]
    public void TheLinkButtonShowsALink(string marked, bool on) => Assert.Equal(on, On(marked, NoteFormat.Link));

    // --- Lines ----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("Ti|tle", "# Ti|tle")]
    [InlineData("# Ti|tle", "## Ti|tle")]
    [InlineData("## Ti|tle", "### Ti|tle")]
    [InlineData("### Ti|tle", "Ti|tle")]
    [InlineData("###### Ti|tle", "Ti|tle")]
    [InlineData("|", "# |")]
    [InlineData("- it|em", "# it|em")]
    [InlineData("> quo|ted", "> # quo|ted")]
    [InlineData("|# Title", "## |Title")]
    public void TheHeadingGoesRound(string before, string after) => Assert.Equal(after, Apply(before, NoteFormat.Heading));

    [Fact]
    public void TheHeadingOfSeveralLinesFollowsTheFirst() =>
        Assert.Equal("## ^one\n## two\n\n## thr|ee", Apply("# ^one\ntwo\n\nthr|ee", NoteFormat.Heading));

    [Theory]
    [InlineData("|", NoteFormat.Bullets, "- |")]
    [InlineData("|", NoteFormat.Numbers, "1. |")]
    [InlineData("|", NoteFormat.Tasks, "- [ ] |")]
    [InlineData("wo|rds", NoteFormat.Bullets, "- wo|rds")]
    [InlineData("wo|rds", NoteFormat.Numbers, "1. wo|rds")]
    [InlineData("wo|rds", NoteFormat.Tasks, "- [ ] wo|rds")]
    [InlineData("- wo|rds", NoteFormat.Bullets, "wo|rds")]
    [InlineData("1. wo|rds", NoteFormat.Numbers, "wo|rds")]
    [InlineData("- [x] wo|rds", NoteFormat.Tasks, "wo|rds")]
    // Another kind of list becomes this kind.
    [InlineData("1. wo|rds", NoteFormat.Bullets, "- wo|rds")]
    [InlineData("* wo|rds", NoteFormat.Tasks, "* [ ] wo|rds")]
    [InlineData("- [ ] wo|rds", NoteFormat.Bullets, "- wo|rds")]
    [InlineData("- [ ] wo|rds", NoteFormat.Numbers, "1. wo|rds")]
    // A heading's #s go; a nested item keeps its indent; a quoted line its quote.
    [InlineData("## wo|rds", NoteFormat.Bullets, "- wo|rds")]
    [InlineData("  - wo|rds", NoteFormat.Numbers, "  1. wo|rds")]
    [InlineData("> wo|rds", NoteFormat.Bullets, "> - wo|rds")]
    // The caret before the marker goes to the words.
    [InlineData("|- words", NoteFormat.Bullets, "|words")]
    public void AListOnALine(string before, NoteFormat format, string after) => Assert.Equal(after, Apply(before, format));

    [Theory]
    [InlineData("^one\ntwo\n\nthr|ee", NoteFormat.Bullets, "- ^one\n- two\n\n- thr|ee")]
    [InlineData("^one\ntwo\nthr|ee", NoteFormat.Numbers, "1. ^one\n2. two\n3. thr|ee")]
    [InlineData("^one\ntwo|", NoteFormat.Tasks, "- [ ] ^one\n- [ ] two|")]
    [InlineData("- ^one\n- two|", NoteFormat.Bullets, "^one\ntwo|")]
    // Some of it a list already: all of it is made one.
    [InlineData("- ^one\ntwo|", NoteFormat.Bullets, "- ^one\n- two|")]
    // Numbers count on from the item above, and each depth counts on its own.
    [InlineData("1. one\n2. two\n^three\nfo|ur", NoteFormat.Numbers, "1. one\n2. two\n3. ^three\n4. fo|ur")]
    [InlineData("^a\n  - b\n  - c\nd|", NoteFormat.Numbers, "1. ^a\n  1. b\n  2. c\n2. d|")]
    // A selection that ends at a line's start does not take that line.
    [InlineData("^one\n|two", NoteFormat.Bullets, "- ^one\n|two")]
    // A code block in the selection is left as it is.
    [InlineData("^one\n```\ncode\n```\ntw|o", NoteFormat.Bullets, "- ^one\n```\ncode\n```\n- tw|o")]
    public void AListOverLines(string before, NoteFormat format, string after) => Assert.Equal(after, Apply(before, format));

    [Theory]
    [InlineData("^one\ntwo\n\nthr|ee", NoteFormat.Bullets)]
    [InlineData("^one\ntwo\nthr|ee", NoteFormat.Numbers)]
    [InlineData("^one\ntwo|", NoteFormat.Tasks)]
    [InlineData("^one\n\ntwo|", NoteFormat.Quote)]
    [InlineData("wo|rds", NoteFormat.Tasks)]
    public void LineFormatsRoundTrip(string marked, NoteFormat format) => RoundTrips(marked, format);

    [Theory]
    [InlineData("wo|rds", "> wo|rds")]
    [InlineData("|", "> |")]
    [InlineData("> wo|rds", "wo|rds")]
    [InlineData("> > wo|rds", "> wo|rds")]
    [InlineData("- it|em", "> - it|em")]
    // A blank line between is quoted too, so the quote stays one.
    [InlineData("^one\n\ntwo|", "> ^one\n> \n> two|")]
    [InlineData("^> one\n>\n> two|", "^one\n\ntwo|")]
    // Part of it quoted already: all of it is.
    [InlineData("^> one\ntwo|", "^> one\n> two|")]
    public void AQuoteOnLines(string before, string after) => Assert.Equal(after, Apply(before, NoteFormat.Quote));

    [Theory]
    [InlineData("- wo|rds", NoteFormat.Bullets, true)]
    [InlineData("- [ ] wo|rds", NoteFormat.Bullets, false)]
    [InlineData("- [ ] wo|rds", NoteFormat.Tasks, true)]
    [InlineData("3. wo|rds", NoteFormat.Numbers, true)]
    [InlineData("- ^one\ntwo|", NoteFormat.Bullets, false)]
    [InlineData("- ^one\n- two|", NoteFormat.Bullets, true)]
    [InlineData("## Ti|tle", NoteFormat.Heading, true)]
    [InlineData("Ti|tle", NoteFormat.Heading, false)]
    [InlineData("> ^one\n\n> two|", NoteFormat.Quote, true)]
    [InlineData("wo|rds", NoteFormat.Quote, false)]
    public void TheLineButtonsShowWhatTheLinesAre(string marked, NoteFormat format, bool on) => Assert.Equal(on, On(marked, format));

    [Fact]
    public void TheHeadingsLevelIsTheCaretLines()
    {
        var lines = Markdown.Lines("# one\n### three\nplain");
        Assert.Equal(1, NoteFormats.Level(lines, 2));
        Assert.Equal(3, NoteFormats.Level(lines, 8));
        Assert.Equal(0, NoteFormats.Level(lines, 18));
    }

    // --- The editor -----------------------------------------------------------------------------------

    [Fact]
    public void AFormatIsOneStepToUndo()
    {
        var editor = new NoteEditor();
        editor.Load("some words");
        editor.MoveTo(5, extend: false);
        editor.MoveTo(10, extend: true);
        Assert.True(editor.Format(NoteFormat.Bold));
        Assert.Equal("some **words**", editor.Text);
        Assert.True(editor.IsOn(NoteFormat.Bold));
        Assert.Equal("words", editor.SelectedText);
        Assert.True(editor.Format(NoteFormat.Bullets));
        Assert.Equal("- some **words**", editor.Text);
        Assert.True(editor.Undo());
        Assert.Equal("some **words**", editor.Text);
        Assert.True(editor.Undo());
        Assert.Equal("some words", editor.Text);
        Assert.Equal("words", editor.SelectedText);
        Assert.True(editor.Redo());
        Assert.Equal("some **words**", editor.Text);
    }

    [Fact]
    public void TypingIntoAnEmptyPairMakesItBold()
    {
        var editor = new NoteEditor();
        editor.Load("a ");
        Assert.True(editor.Format(NoteFormat.Bold));
        Assert.True(editor.IsOn(NoteFormat.Bold));
        Assert.True(editor.Type("b"));
        Assert.Equal("a **b**", editor.Text);
        Assert.True(editor.IsOn(NoteFormat.Bold));
        // Undone: the typing first, then the pair.
        Assert.True(editor.Undo());
        Assert.Equal("a ****", editor.Text);
        Assert.True(editor.Undo());
        Assert.Equal("a ", editor.Text);
    }
}
