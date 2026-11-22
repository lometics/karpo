// What the taskbar's menu draws from the person's list (BarMenus.Lines and BarMenus.Tidy): no empty
// groups, no divider at either end or beside another, the layouts a row each — §561 a lone one too, ticked —,
// and a shortcut whose target this PC doesn't have greyed with the reason.

namespace Karpo.Tests.Core;

public sealed class BarMenuLinesTests
{
    private static readonly TargetCheck Here = new(TargetState.Here, "There");

    private static TargetCheck Gone(string target) =>
        target.Contains("gone", StringComparison.OrdinalIgnoreCase) ? new TargetCheck(TargetState.Missing, "This file isn't on this PC") : Here;

    private static readonly BarMenuLayoutRow[] TwoLayouts = [new("Work", "\uE8A9", false), new("Play", "\uE7FC", true)];

    private static List<BarMenuLineInfo> Lines(IReadOnlyList<BarMenuEntry> entries, IReadOnlyList<BarMenuLayoutRow>? layouts = null, Func<string, string?>? shellName = null) =>
        BarMenus.Lines(entries, layouts ?? [], windowsTerminal: true, Gone, shellName);

    /// <summary>A line as a word: a row's text, "—" for a divider, "# " and its words for a heading.</summary>
    private static string[] Words(IEnumerable<BarMenuLineInfo> lines) =>
        [.. lines.Select(line => line.Kind switch
        {
            BarMenuLine.Divider => "—",
            BarMenuLine.Heading => "# " + line.Text,
            _ => line.Text,
        })];

    // --- Tidy -----------------------------------------------------------------------------

    private static string[] Tidy(params string[] lines) =>
        [.. BarMenus.Tidy(lines, line => line == "—" ? BarMenuLine.Divider : line.StartsWith('#') ? BarMenuLine.Heading : BarMenuLine.Row)];

    [Fact]
    public void NoDividerAtTheTop() => Assert.Equal(["a", "—", "b"], Tidy("—", "a", "—", "b"));

    [Fact]
    public void NoDividerAtTheBottom() => Assert.Equal(["a"], Tidy("a", "—", "—"));

    [Fact]
    public void NoDividerBesideAnother() => Assert.Equal(["a", "—", "b"], Tidy("a", "—", "—", "—", "b"));

    [Fact]
    public void AHeadingOverRowsStays() => Assert.Equal(["# Tools", "a", "b"], Tidy("# Tools", "a", "b"));

    /// <summary>"No empty groups": a heading whose group has no rows goes, at the end, before a divider or before another heading.</summary>
    [Fact]
    public void AHeadingWithNothingUnderItGoes()
    {
        Assert.Equal(["a"], Tidy("a", "# Empty"));
        Assert.Equal(["a", "—", "b"], Tidy("a", "# Empty", "—", "b"));
        Assert.Equal(["# Full", "b"], Tidy("# Empty", "# Full", "b"));
    }

    /// <summary>A dropped heading between two dividers leaves one divider, not two.</summary>
    [Fact]
    public void ADroppedGroupLeavesNoDoubleDivider() => Assert.Equal(["a", "—", "b"], Tidy("a", "—", "# Empty", "—", "b"));

    [Fact]
    public void OnlyDividersAndHeadingsDrawNothing() => Assert.Empty(Tidy("—", "# A", "—", "# B"));

    [Fact]
    public void ADividerBetweenGroupsStays() => Assert.Equal(["# A", "a", "—", "# B", "b"], Tidy("# A", "a", "—", "# B", "b"));

    // --- The fixed menu -----------------------------------------------------------------------

    /// <summary>
    /// §561 (the person, 2026-11-08: "in the right click taskbar menu layouts should also show even if only one layout is
    /// present"; §366 to §560 one was left out as no choice): one layout is its row, ticked, ruled off as several are.
    /// </summary>
    [Fact]
    public void TheMenuWithOneLayoutShowsItTicked()
    {
        var lines = Lines(BarMenus.Default(), TwoLayouts[1..]);

        Assert.Equal(["Play", "—", "Task Manager", "—", "Lock the taskbar", "Taskbar settings"], Words(lines));
        // §663: Lock the taskbar unticked, as these lines are drawn unlocked.
        Assert.Equal([true, null, null, null, false, null], lines.Select(line => line.Checked));
        Assert.Equal([0, -1, -1, -1, -1, -1], lines.Select(line => line.Layout));
    }

    /// <summary>§366: "the title for the different layouts should also not be shown": no heading over them.</summary>
    [Fact]
    public void TheMenuWithLayoutsHasThemUnderNoHeading()
    {
        var lines = Lines(BarMenus.Default(), TwoLayouts);

        Assert.Equal(["Work", "Play", "—", "Task Manager", "—", "Lock the taskbar", "Taskbar settings"], Words(lines));
        Assert.Equal([false, true, null, null, null, false, null], lines.Select(line => line.Checked));
        Assert.Equal([0, 1, -1, -1, -1, -1, -1], lines.Select(line => line.Layout));
    }

    [Fact]
    public void TheLayoutsUnderAHeadingGoWithItWhereNoneIsOffered()
    {
        List<BarMenuEntry> entries = [BarMenuEntry.Heading("Layouts"), BarMenuEntry.Of(BarMenuAction.Layouts), BarMenuEntry.Divider(), BarMenuEntry.Of(BarMenuAction.TaskManager)];

        Assert.Equal(["Task Manager"], Words(Lines(entries)));
        Assert.Equal(["# Layouts", "Play", "—", "Task Manager"], Words(Lines(entries, TwoLayouts[1..])));
        Assert.Equal(["# Layouts", "Work", "Play", "—", "Task Manager"], Words(Lines(entries, TwoLayouts)));
    }

    // --- Rows -------------------------------------------------------------------------------

    [Fact]
    public void ABuiltInRowHasItsNameAndGlyph()
    {
        var line = Assert.Single(Lines([BarMenuEntry.Of(BarMenuAction.DeviceManager)]));

        Assert.Equal("Device Manager", line.Text);
        Assert.Equal(BarMenus.Info(BarMenuAction.DeviceManager)!.Glyph, line.Glyph);
        Assert.True(line.Enabled);
    }

    [Fact]
    public void TheTerminalRowSaysWhichTerminal()
    {
        var entries = new[] { BarMenuEntry.Of(BarMenuAction.Terminal) };

        Assert.Equal("Windows PowerShell", BarMenus.Lines(entries, [], windowsTerminal: false, Gone).Single().Text);
    }

    /// <summary>"A missing target greyed, with the reason."</summary>
    [Fact]
    public void AShortcutWhoseTargetIsGoneIsGreyedWithTheReason()
    {
        var lines = Lines([BarMenuEntry.Shortcut(@"C:\Tools\gone\tool.exe", name: "Old tool"), BarMenuEntry.Shortcut(@"C:\Windows\notepad.exe")]);

        Assert.False(lines[0].Enabled);
        Assert.Equal("This file isn't on this PC", lines[0].Detail);
        Assert.True(lines[1].Enabled);
        Assert.Null(lines[1].Detail);
    }

    [Fact]
    public void ALinkNobodyCanCheckStaysUsable()
    {
        var line = BarMenus.Lines([BarMenuEntry.Shortcut("https://example.com")], [], true, target => new TargetCheck(TargetState.Unchecked, "A link")).Single();

        Assert.True(line.Enabled);
        Assert.Null(line.Detail);
    }

    [Fact]
    public void AShortcutIsNamedByItselfThenTheShellThenItsTarget()
    {
        Assert.Equal("Mine", Lines([BarMenuEntry.Shortcut(@"C:\a.exe", name: "Mine")], shellName: _ => "Shell's").Single().Text);
        Assert.Equal("Shell's", Lines([BarMenuEntry.Shortcut(@"C:\a.exe")], shellName: _ => "Shell's").Single().Text);
        Assert.Equal("a", Lines([BarMenuEntry.Shortcut(@"C:\a.exe")], shellName: _ => null).Single().Text);
    }

    /// <summary>A shortcut with a glyph of its own is drawn with it; one without leaves its picture to the shell (Glyph null).</summary>
    [Fact]
    public void AShortcutsGlyphIsItsOwnOrNone()
    {
        Assert.Equal("\uE774", Lines([BarMenuEntry.Shortcut("https://github.com", icon: "\uE774")]).Single().Glyph);
        Assert.Null(Lines([BarMenuEntry.Shortcut(@"C:\a.exe")]).Single().Glyph);
        Assert.Null(Lines([BarMenuEntry.Shortcut(@"C:\a.exe", icon: @"C:\icons\a.ico")]).Single().Glyph);
    }

    [Fact]
    public void AHeadingWithNoWordsIsNotDrawn() =>
        Assert.Equal(["Task Manager"], Words(Lines([new BarMenuEntry { Id = "h", Kind = BarMenuKind.Heading }, BarMenuEntry.Of(BarMenuAction.TaskManager)])));

    /// <summary>
    /// §532: the person's own menu is tidied as it is drawn: the layouts taken out leave no rule at the menu's top, a
    /// heading over nothing goes, and a shortcut still to choose draws nothing; Quit Karpo is a row as any other.
    /// </summary>
    [Fact]
    public void ThePersonsOwnMenuIsTidiedAsItIsDrawn()
    {
        var config = new Config();
        Assert.True(BarMenus.Change(config, list => BarMenus.Remove(list, list.FindIndex(entry => entry.Action == BarMenuAction.Layouts))));
        Assert.Equal(["Task Manager", "—", "Lock the taskbar", "Taskbar settings"], Words(Lines(config.BarMenuEntries, TwoLayouts)));

        Assert.True(BarMenus.Change(config, list =>
            BarMenus.Add(list, BarMenuEntry.Divider()) && BarMenus.Add(list, BarMenuEntry.Heading("Tools")) && BarMenus.Add(list, BarMenuEntry.Shortcut(""))
            && BarMenus.Add(list, BarMenuEntry.Divider()) && BarMenus.Add(list, BarMenuEntry.Of(BarMenuAction.QuitKarpo))));
        Assert.Equal(["Task Manager", "—", "Lock the taskbar", "Taskbar settings", "—", S.SettingsAboutQuitHeader], Words(Lines(config.BarMenuEntries, TwoLayouts)));
    }

    /// <summary>Every line keeps the entry it was drawn from, which is what a chosen row acts on.</summary>
    [Fact]
    public void EveryLineKnowsItsEntry()
    {
        var entries = BarMenus.Default();

        Assert.All(Lines(entries, TwoLayouts), line => Assert.Contains(line.Entry, entries));
    }
}
