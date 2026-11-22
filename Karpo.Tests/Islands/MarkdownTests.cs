// A note's Markdown read line by line for the live editor, and each line as shown, marks and all,
// wherever the caret is (Karpo.Dock\Notes\Markdown.cs, §321): pure, no device.

using Karpo.Dock;

namespace Karpo.Tests.Islands;

public sealed class MarkdownTests
{
    private static MdLine One(string note) => Assert.Single(Markdown.Lines(note));

    /// <summary>Line <paramref name="line"/> of <paramref name="note"/> as shown: its text, and its runs as (words, style, link).</summary>
    private static (string Text, (string Words, MdStyle Style, string? Link)[] Runs) Rest(string note, int line = 0)
    {
        var shown = Markdown.Show(note, Markdown.Lines(note)[line]);
        return (shown.Text, [.. shown.Runs.Select(run => (shown.Text.Substring(run.Start, run.Length), run.Style, run.Link))]);
    }

    /// <summary>The runs that are not marks: what the marks make of the words.</summary>
    private static (string Words, MdStyle Style, string? Link)[] Words(string note) =>
        [.. Rest(note).Runs.Where(run => !run.Style.HasFlag(MdStyle.Mark))];

    /// <summary>The marks, as they stand in the line.</summary>
    private static string[] Marks(string note) =>
        [.. Rest(note).Runs.Where(run => run.Style.HasFlag(MdStyle.Mark)).Select(run => run.Words)];

    // --- Lines ----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("# One", 1, "One")]
    [InlineData("###   Three", 3, "Three")]
    [InlineData("###### Six", 6, "Six")]
    [InlineData("##", 2, "")]
    public void Headings(string note, int level, string words)
    {
        var line = One(note);
        Assert.Equal(MdKind.Heading, line.Kind);
        Assert.Equal(level, line.Level);
        Assert.Equal(words, note[line.Content..line.End]);
        Assert.Equal(note, Rest(note).Text);
    }

    [Theory]
    [InlineData("#No space")]
    [InlineData("####### Seven")]
    public void NotHeadings(string note) => Assert.Equal(MdKind.Paragraph, One(note).Kind);

    [Fact]
    public void EveryLineOfTheTextIsALine()
    {
        var lines = Markdown.Lines("one\ntwo\n\nthree\n");
        Assert.Equal([MdKind.Paragraph, MdKind.Paragraph, MdKind.Blank, MdKind.Paragraph, MdKind.Blank], lines.Select(line => line.Kind));
        Assert.Equal([(0, 3), (4, 7), (8, 8), (9, 14), (15, 15)], lines.Select(line => (line.Start, line.End)));
        Assert.Single(Markdown.Lines(""));
        Assert.Single(Markdown.Lines(null));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    [InlineData(8, 2)]
    [InlineData(14, 3)]
    [InlineData(15, 4)]
    public void APlaceIsOnItsLine(int at, int line) => Assert.Equal(line, Markdown.LineAt(Markdown.Lines("one\ntwo\n\nthree\n"), at));

    [Fact]
    public void ListsNest()
    {
        const string note = "- a\n  - b\n    - c\n* d\n3. three\n4) four\n   1. inner\n- [ ] task\n  - [x] done";
        var lines = Markdown.Lines(note);
        Assert.Equal(
            [(MdKind.Bullet, 0, "a"), (MdKind.Bullet, 1, "b"), (MdKind.Bullet, 2, "c"), (MdKind.Bullet, 0, "d"),
             (MdKind.Numbered, 0, "three"), (MdKind.Numbered, 0, "four"), (MdKind.Numbered, 1, "inner"),
             (MdKind.Task, 0, "task"), (MdKind.Task, 1, "done")],
            lines.Select(line => (line.Kind, line.Level, note[line.Content..line.End])));
        // A numbered item shows its own number, as written.
        Assert.Equal(["3.", "4)", "1."], lines.Where(line => line.Kind == MdKind.Numbered).Select(line => line.Marker));
    }

    [Theory]
    [InlineData("-")]
    [InlineData("- ")]
    [InlineData("1.")]
    [InlineData("- [ ]")]
    [InlineData("- [x] ")]
    public void AnItemWithNoWordsIsStillAnItem(string note)
    {
        var line = One(note);
        Assert.True(line.IsItem);
        Assert.Equal(note.Length, line.Content);
        Assert.Equal(note, Rest(note).Text);
    }

    [Theory]
    [InlineData("-not an item")]
    [InlineData("1.5 kg")]
    [InlineData("- [] no box")]
    public void NotItems(string note)
    {
        var line = One(note);
        Assert.False(line.Kind is MdKind.Task, note);
        if (line.IsItem)
        {
            Assert.Equal(MdKind.Bullet, line.Kind);
        }
    }

    [Fact]
    public void TasksKnowWhereTheirBoxIs()
    {
        const string note = "Shopping\n\n- [ ] milk\n- [x] bread\n  - [X] nested";
        var tasks = Markdown.Lines(note).Where(line => line.Kind == MdKind.Task).ToList();
        Assert.Equal(["milk", "bread", "nested"], tasks.Select(task => note[task.Content..task.End]));
        Assert.Equal([false, true, true], tasks.Select(task => task.Checked));
        Assert.All(tasks, task => Assert.Equal('[', note[task.Box]));
        // Ticking one ticks the note and nothing else.
        Assert.Equal(note.Replace("- [ ] milk", "- [x] milk"), Markdown.Toggled(note, tasks[0].Box));
        Assert.Equal(note.Replace("- [x] bread", "- [ ] bread"), Markdown.Toggled(note, tasks[1].Box));
        // Nowhere near a box, nothing changes.
        Assert.Equal(note, Markdown.Toggled(note, 0));
        Assert.Equal(note, Markdown.Toggled(note, note.Length + 5));
    }

    [Fact]
    public void ATaskInAQuoteStillKnowsItsBox()
    {
        const string note = "> - [ ] quoted";
        var task = One(note);
        Assert.Equal(MdKind.Task, task.Kind);
        Assert.Equal(1, task.Quote);
        Assert.Equal("> - [x] quoted", Markdown.Toggled(note, task.Box));
    }

    [Fact]
    public void QuotesHoldLines()
    {
        const string note = "> # Title\n> words\n> > deeper\n>\nout";
        var lines = Markdown.Lines(note);
        Assert.Equal(
            [(MdKind.Heading, 1, "Title"), (MdKind.Paragraph, 1, "words"), (MdKind.Paragraph, 2, "deeper"), (MdKind.Paragraph, 1, ""), (MdKind.Paragraph, 0, "out")],
            lines.Select(line => (line.Kind, line.Quote, note[line.Content..line.End])));
    }

    [Fact]
    public void FencedCodeIsKeptAsItIs()
    {
        const string note = "```cs\nvar x = **1**;\n  indented\n```\nafter\n~~~\nunclosed";
        var lines = Markdown.Lines(note);
        Assert.Equal([MdKind.Fence, MdKind.Code, MdKind.Code, MdKind.Fence, MdKind.Paragraph, MdKind.Fence, MdKind.Code], lines.Select(line => line.Kind));
        Assert.All(lines.Where(line => line.Kind == MdKind.Code), line => Assert.Empty(line.Spans));
        // A line of code shows as it is, in code; a fence shows too, faint.
        Assert.Equal(("var x = **1**;", MdStyle.Code, (string?)null), Assert.Single(Rest(note, 1).Runs));
        Assert.Equal(("```cs", MdStyle.Code | MdStyle.Mark, (string?)null), Assert.Single(Rest(note, 0).Runs));
    }

    [Theory]
    [InlineData("---")]
    [InlineData("***")]
    [InlineData("_ _ _")]
    [InlineData("> ---")]
    public void Rules(string note)
    {
        Assert.Equal(MdKind.Rule, One(note).Kind);
        Assert.Equal((note, MdStyle.Mark, (string?)null), Assert.Single(Rest(note).Runs));
    }

    // --- Inline -------------------------------------------------------------------------------------

    [Fact]
    public void Emphasis()
    {
        const string note = "a **bold** and *it* and __b__ and _i_ and ***both*** ~~gone~~";
        Assert.Equal(note, Rest(note).Text);
        Assert.Equal(
            [("bold", MdStyle.Bold, null), ("it", MdStyle.Italic, null), ("b", MdStyle.Bold, null), ("i", MdStyle.Italic, null), ("both", MdStyle.Bold | MdStyle.Italic, null), ("gone", MdStyle.Strike, null)],
            Words(note));
        Assert.Equal(["**", "**", "*", "*", "__", "__", "_", "_", "***", "***", "~~", "~~"], Marks(note));
    }

    [Fact]
    public void NestedEmphasis()
    {
        const string note = "**bold *and italic***";
        var (text, runs) = Rest(note);
        Assert.Equal(note, text);
        Assert.Contains(("and italic", MdStyle.Bold | MdStyle.Italic, (string?)null), runs);
        Assert.Contains(("bold ", MdStyle.Bold, (string?)null), runs);
        // Marks inside a run are as bold as the words round them.
        Assert.Contains(("*", MdStyle.Bold | MdStyle.Mark, (string?)null), Rest("**a *b* c**").Runs);
    }

    [Theory]
    [InlineData("2 * 3 * 4")]
    [InlineData("snake_case_name")]
    [InlineData("** not bold **")]
    [InlineData("a ~single~ tilde")]
    [InlineData("an *unclosed star")]
    public void MarksThatAreNotEmphasisStay(string source)
    {
        var (text, runs) = Rest(source);
        Assert.Equal(source, text);
        Assert.Empty(runs);
    }

    [Fact]
    public void CodeSpansKeepTheirMarks()
    {
        const string note = "run `a *b* c` and `` x ` y ``";
        Assert.Equal(note, Rest(note).Text);
        Assert.Equal([("a *b* c", MdStyle.Code, null), (" x ` y ", MdStyle.Code, null)], Words(note));
        Assert.Equal(["`", "`", "``", "``"], Marks(note));
    }

    [Fact]
    public void EscapesWriteTheMark()
    {
        const string note = @"\*not\* \[a\](b) \n";
        Assert.Equal(note, Rest(note).Text);
        Assert.Empty(Words(note));
        Assert.Equal(["\\", "\\", "\\", "\\"], Marks(note));
    }

    [Fact]
    public void Links()
    {
        const string note = "see [the **docs**](https://example.com/a \"title\"), <https://x.org> and www.karpo.app/page. Mail <mailto:me@x.org>";
        Assert.Equal(note, Rest(note).Text);
        Assert.Equal(
            [("the ", MdStyle.Link, "https://example.com/a"), ("docs", MdStyle.Link | MdStyle.Bold, "https://example.com/a"), ("https://x.org", MdStyle.Link, "https://x.org"),
             ("www.karpo.app/page", MdStyle.Link, "https://www.karpo.app/page"), ("me@x.org", MdStyle.Link, "mailto:me@x.org")],
            Words(note));
        // The address is a mark: faint, not underlined, and no link of its own.
        Assert.Equal(["[", "**", "**](https://example.com/a \"title\")", "<", ">", "<mailto:", ">"], Marks(note));
        Assert.All(Rest(note).Runs.Where(run => run.Style.HasFlag(MdStyle.Mark)), run => Assert.Null(run.Link));
    }

    [Theory]
    [InlineData("[run](file:///C:/Windows/notepad.exe)")]
    [InlineData("[run](javascript:alert(1))")]
    [InlineData("[run](C:\\Windows\\notepad.exe)")]
    [InlineData("[run](ms-settings:display)")]
    public void ALinkThatWouldStartAProgramIsOnlyWords(string source)
    {
        var (text, runs) = Rest(source);
        Assert.Equal(source, text);
        Assert.DoesNotContain(runs, run => run.Link is not null);
    }

    [Fact]
    public void ABareAddressLeavesTheSentencesPunctuation()
    {
        var (_, runs) = Rest("(see https://example.com/x_(y)), then");
        Assert.Equal(("https://example.com/x_(y)", MdStyle.Link, "https://example.com/x_(y)"), Assert.Single(runs));
    }

    [Fact]
    public void AnImageIsItsWords()
    {
        const string note = "![a cat](https://example.com/cat.png)";
        Assert.Equal(note, Rest(note).Text);
        Assert.Empty(Words(note));
        Assert.Equal(["![", "](https://example.com/cat.png)"], Marks(note));
    }

    [Theory]
    [InlineData("https://example.com", true)]
    [InlineData("http://example.com", true)]
    [InlineData("mailto:a@b.c", true)]
    [InlineData("file:///c:/x", false)]
    [InlineData("\\\\server\\share", false)]
    [InlineData("calc.exe", false)]
    [InlineData("", false)]
    public void WhatALinkMayOpen(string link, bool opens) => Assert.Equal(opens, Markdown.Opens(link));

    // --- A line's lead ----------------------------------------------------------------------------------

    [Fact]
    public void AHeadingsMarksShow()
    {
        var (text, runs) = Rest("## Title\nnext");
        Assert.Equal("## Title", text);
        Assert.Equal([("## ", MdStyle.Mark, null)], runs);
        // On every line, not only the first.
        Assert.Equal("# B", Rest("a\n# B", 1).Text);
        Assert.Equal([("# ", MdStyle.Mark, null)], Rest("a\n# B", 1).Runs);
    }

    [Fact]
    public void AListsMarkerAndATasksBoxShow()
    {
        Assert.Equal("- milk", Rest("- milk").Text);
        Assert.Equal([("- ", MdStyle.Mark, null)], Rest("- milk").Runs);
        Assert.Equal([("12. ", MdStyle.Mark, null)], Rest("12. twelve").Runs);
        Assert.Equal([("> ", MdStyle.Mark, null)], Rest("> quoted").Runs);
        // A task's box in the accent, a click's to tick; its words struck once it is ticked.
        Assert.Equal([("- ", MdStyle.Mark, null), ("[ ]", MdStyle.Box, null), (" ", MdStyle.Mark, null)], Rest("- [ ] milk").Runs);
        Assert.Equal([("  - ", MdStyle.Mark, null), ("[x]", MdStyle.Box, null), (" ", MdStyle.Mark, null), ("milk ", MdStyle.Strike, null), ("now", MdStyle.Strike | MdStyle.Bold, null)],
            Rest("  - [x] milk **now**").Runs.Where(run => run.Words != "**"));
    }

    /// <summary>
    /// A shown line is its text: every place in the note is the same place on screen, one for one,
    /// and the runs lie inside it in order. Every line of a set of notes every kind of broken.
    /// </summary>
    [Theory]
    [InlineData("**")]
    [InlineData("[")]
    [InlineData("[a](")]
    [InlineData("`")]
    [InlineData("- [")]
    [InlineData("> ")]
    [InlineData("1.")]
    [InlineData("***___~~~```")]
    [InlineData("<https://")]
    [InlineData("\\")]
    [InlineData("# **a** _b_ `c` [d](https://e.f) \\* <https://g.h> www.i.jk ~~l~~")]
    [InlineData("> > - [x] **quoted** task\n>\n> 2) [link **in** it](https://x.org) and `code`")]
    [InlineData("```\n**not bold**\n```\n---\n  - nested *it*\n    1. deeper __b__\n![img](https://x.org/a.png) end")]
    public void EveryPlaceIsShownOneForOne(string note)
    {
        foreach (var line in Markdown.Lines(note))
        {
            var shown = Markdown.Show(note, line);
            Assert.Equal(note[line.Start..line.End], shown.Text);
            for (var place = line.Start; place <= line.End; place++)
            {
                Assert.Equal(place - line.Start, shown.ToShown(place));
                Assert.Equal(place, shown.ToSource(shown.ToShown(place)));
            }
            var end = 0;
            foreach (var run in shown.Runs)
            {
                Assert.True(run.Start >= end && run.Length > 0 && run.Start + run.Length <= shown.Text.Length);
                end = run.Start + run.Length;
            }
        }
    }
}
