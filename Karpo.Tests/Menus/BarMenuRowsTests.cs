// The bar's own menu drawn from its list (Karpo.Dock\Dock.BarMenu.cs): the default list is the menu,
// command for command; Karpo's own rows keep their commands wherever they stand; every other row the
// model can draw is numbered from CMD_BAR_ENTRY and leads back to its entry; a heading is a header
// and a divider a separator; a shortcut has its shell icon, and is greyed with the reason where its
// target is gone; §532 Quit Karpo asks first, as the settings panel's does, and quits only on its yes.

using Karpo.Dock;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

public sealed class BarMenuRowsTests
{
    private static readonly FlyoutItem[] TwoLayouts =
    [
        new(5600, "Work", new FlyoutIcon.Glyph("\uE8A9"), Checked: true),
        new(5601, "Play", new FlyoutIcon.Glyph("\uE7FC"), Checked: false),
    ];

    private static readonly FlyoutIcon.Picture Shell = new(new Pixels { Data = new byte[16 * 16 * 4], Width = 16, Height = 16 });

    /// <summary>A PC where only what has "gone" in its path is missing, every name is the shell's own, and every file has a picture.</summary>
    private static readonly ShortcutLooks Looks = new(
        target => target.Contains("gone", StringComparison.OrdinalIgnoreCase) ? new TargetCheck(TargetState.Missing, "This file isn't on this PC") : new TargetCheck(TargetState.Here, ""),
        target => target.EndsWith("notepad.exe", StringComparison.OrdinalIgnoreCase) ? "Notepad" : null,
        _ => Shell);

    private static (List<FlyoutItem> Items, List<BarMenuRow> Rows) Rows(IReadOnlyList<BarMenuEntry> entries, IReadOnlyList<FlyoutItem>? layouts = null) =>
        TheDock.BarRows(entries, layouts ?? [], windowsTerminal: true, Looks);

    private static string[] Texts(IEnumerable<FlyoutItem> rows) =>
        [.. rows.Select(row => row.Separator ? "—" : row.Header ? "# " + row.Text : row.Text)];

    /// <summary>
    /// "The default list reproduces today's menu exactly": the same rows, the same commands — §581 Windows 11's Task Manager
    /// and Taskbar settings after the layouts, which stand under no heading (§366), §663 Lock the taskbar between them.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheDefaultListIsTheMenuItAlwaysWas(bool layouts)
    {
        IReadOnlyList<FlyoutItem> offered = layouts ? TwoLayouts : [];

        var (items, rows) = Rows(BarMenus.Default(), offered);
        var before = TheDock.BarRows(offered);

        Assert.Equal(Texts(before), Texts(items));
        Assert.Equal(before.Select(row => row.Id), items.Select(row => row.Id));
        Assert.Empty(rows);
        Assert.Equal(layouts
            ? ["Work", "Play", "—", "Task Manager", "—", "Lock the taskbar", "Taskbar settings"]
            : ["Task Manager", "—", "Lock the taskbar", "Taskbar settings"], Texts(items));
        Assert.DoesNotContain(items, item => item.Primary || item.Header);
    }

    /// <summary>Moved, Karpo's own rows are the same commands, so choosing one does what it always did.</summary>
    [Fact]
    public void KarposOwnRowsKeepTheirCommandsWhereverTheyAre()
    {
        var before = TheDock.BarRows(TwoLayouts).Where(row => row.Id != 0).ToDictionary(row => row.Text, row => row.Id);
        List<BarMenuEntry> moved =
        [
            BarMenuEntry.Of(BarMenuAction.Layouts),
            BarMenuEntry.Of(BarMenuAction.TaskbarSettings),
            BarMenuEntry.Of(BarMenuAction.TaskManager),
        ];

        var (items, rows) = Rows(moved, TwoLayouts);

        Assert.Equal(["Work", "Play", "Taskbar settings", "Task Manager"], Texts(items));
        Assert.All(items, row => Assert.Equal(before[row.Text], row.Id));
        Assert.Empty(rows);
    }

    [Fact]
    public void EveryOtherRowIsNumberedAndLeadsBackToItsEntry()
    {
        List<BarMenuEntry> entries =
        [
            BarMenuEntry.Of(BarMenuAction.FileExplorer),
            BarMenuEntry.Of(BarMenuAction.TaskManager),
            BarMenuEntry.Shortcut(@"C:\Windows\notepad.exe"),
            BarMenuEntry.Of(BarMenuAction.Run),
        ];

        var (items, rows) = Rows(entries);

        Assert.Equal([TheDock.CMD_BAR_ENTRY, TheDock.CMD_BAR_ENTRY + 1, TheDock.CMD_BAR_ENTRY + 2], rows.Select(row => row.Id));
        Assert.Equal([entries[0], entries[2], entries[3]], rows.Select(row => row.Entry));
        foreach (var row in rows)
        {
            Assert.Same(row, TheDock.BarRowOf(rows, row.Id));
            Assert.Contains(items, item => item.Id == row.Id);
        }
        Assert.Null(TheDock.BarRowOf(rows, TheDock.CMD_BAR_ENTRY + 3));
        Assert.Null(TheDock.BarRowOf(rows, TheDock.CMD_BAR_ENTRY - 1));
        Assert.Null(TheDock.BarRowOf([], TheDock.CMD_BAR_ENTRY));
    }

    /// <summary>The commands stay clear of every other range a menu of the bar's uses.</summary>
    [Fact]
    public void TheRowsCommandsAreTheirOwn()
    {
        Assert.True(TheDock.CMD_BAR_ENTRY >= TheDock.CMD_WIDGET_ACT + 100);
        Assert.True(TheDock.CMD_BAR_ENTRY + BarMenus.MostEntries <= TheDock.CMD_ADD_WIDGET);
    }

    [Fact]
    public void AHeadingIsAHeaderAndADividerASeparator()
    {
        var (items, _) = Rows(
        [
            BarMenuEntry.Heading("Tools"),
            BarMenuEntry.Of(BarMenuAction.DeviceManager),
            BarMenuEntry.Divider(),
            BarMenuEntry.Of(BarMenuAction.TaskManager),
        ]);

        Assert.True(items[0].Header);
        Assert.Equal("Tools", items[0].Text);
        Assert.Equal(0u, items[0].Id);
        Assert.True(items[2].Separator);
    }

    [Fact]
    public void AShortcutHasItsShellIconAndName()
    {
        var (items, _) = Rows([BarMenuEntry.Shortcut(@"C:\Windows\notepad.exe")]);

        Assert.Equal("Notepad", items[0].Text);
        Assert.Same(Shell, items[0].Icon);
        Assert.True(items[0].Enabled);
    }

    [Fact]
    public void AShortcutWithAGlyphOfItsOwnIsDrawnWithIt()
    {
        var (items, _) = Rows([BarMenuEntry.Shortcut("https://github.com", name: "GitHub", icon: "\uE774")]);

        Assert.Equal(new FlyoutIcon.Glyph("\uE774"), items[0].Icon);
    }

    [Fact]
    public void AShortcutTheShellHasNoPictureForHasTheLinkGlyph()
    {
        var (items, _) = TheDock.BarRows([BarMenuEntry.Shortcut("mailto:ann@example.com")], [], true, ShortcutLooks.None);

        Assert.Equal(new FlyoutIcon.Glyph(BarMenus.ShortcutGlyph), items[0].Icon);
    }

    /// <summary>A delete question's answer, kept for the test to read: never Windows' dialog.</summary>
    private sealed class Answer(bool yes) : IDeleteAsk
    {
        public List<DeleteQuestion> Asked { get; } = [];

        public bool Ask(HWND owner, DeleteQuestion question)
        {
            Asked.Add(question);
            return yes;
        }
    }

    /// <summary>
    /// §532: Quit Karpo, added to the menu, is a row of its own leading back to its entry; chosen, it asks first what the
    /// settings panel's Quit Karpo asks, and goes Karpo's one way out only on the dialog's yes (a fake: nothing quits).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QuitKarpoAsksFirstThenQuits(bool yes)
    {
        var (items, rows) = Rows([BarMenuEntry.Of(BarMenuAction.TaskbarSettings), BarMenuEntry.Of(BarMenuAction.QuitKarpo)]);
        Assert.Equal(["Taskbar settings", S.SettingsAboutQuitHeader], Texts(items));
        Assert.Equal(BarMenuAction.QuitKarpo, TheDock.BarRowOf(rows, items[1].Id)!.Entry.Action);

        var quits = 0;
        var answer = new Answer(yes);
        var went = TheDock.QuitAsked(answer, HWND.NULL, new KarpoServices(null, () => quits++));

        var asked = Assert.Single(answer.Asked);
        Assert.Equal(KarpoMenu.Quit(new Config(), new KarpoServices(null, () => { }))[0].Ask, asked);
        Assert.Equal((yes, yes ? 1 : 0), (went, quits));
    }

    /// <summary>"A missing target greyed, with the reason."</summary>
    [Fact]
    public void AShortcutWhoseTargetIsGoneIsGreyedWithTheReason()
    {
        var (items, _) = Rows([BarMenuEntry.Shortcut(@"C:\Tools\gone\tool.exe", name: "Old tool")]);

        Assert.False(items[0].Enabled);
        Assert.Equal("This file isn't on this PC", items[0].Detail);
    }

    /// <summary>
    /// The bar's menu as it is drawn (§581: "the default right click menu on the taskbar should be like in windows: task
    /// manager and taskbar settings (the taskbar settings should open edit mode.) and the layout selector should be also
    /// there"): the layouts under no heading (§366), as the bar offers them, command for command; then Windows 11's Task
    /// Manager and Taskbar settings with Windows' glyphs, the second edit mode's command; §663 between them, ruled off from
    /// Task Manager, "Lock the taskbar", its command the dock's own, ticked (locked, as a new config is). No row of
    /// buttons, no "Add".
    /// </summary>
    [Fact]
    public void TheMenuIsTheLayoutsThenTaskManagerLockAndTaskbarSettings()
    {
        var (items, rows) = Rows(BarMenus.Default(), TwoLayouts);

        Assert.Equal(["Work", "Play", "—", "Task Manager", "—", "Lock the taskbar", "Taskbar settings"], Texts(items));
        Assert.Equal(TwoLayouts.Select(layout => layout.Id), items.Take(TwoLayouts.Length).Select(item => item.Id));
        Assert.Equal((TheDock.CMD_LOCK_TASKBAR, (bool?)true), (items[^2].Id, items[^2].Checked));
        Assert.Equal((TheDock.CMD_TASK_MANAGER, new FlyoutIcon.Glyph("")), (items[^4].Id, items[^4].Icon));
        Assert.Equal((TheDock.CMD_TASKBAR_SETTINGS, new FlyoutIcon.Glyph("")), (items[^1].Id, items[^1].Icon));
        Assert.DoesNotContain(items, item => item.Primary || item.Header);
        Assert.Empty(rows);
    }
}
