// edit, §532: the taskbar's own right-click menu changed on the settings panel, §543 on a page of its own, §553 the Taskbar
// page's last section, §585 one expander, Behavior's last row, "Taskbar menu" (the person, 2026-11-07: "the right click
// context menu of the taskbar should be editable in the settings again. it should be scoped on a global level"; "taskbar menu
// should be folded into taskbar configuration page."; 2026-11-09: "taskbar menu should only get an expansion section with the
// configuration inside there but not a full own section"; Karpo.Core\KarpoMenu.BarMenu.cs). Held here, the page as the dock builds it (Dock.LooksItems) and
// each control's id done as the dock does it (Dock.LooksPicked: the row read again from a copy of the config, Dock.LooksOf):
//
//  - §585 **The expander**: Behavior's last row, no section of its own (§553 to §584), "The same on every layout" under its
//    name; no side item of its own; every key the menu's rows have sent by a control of the Taskbar page, none by the Karpo
//    page.
//  - **The entries**: in it, under its preview, each entry a row in the menu's order, §551 dragged into order by its handle (no
//    ↑ ↓), at its end Remove, an icon button (Edit none, why under its name); a shortcut and a heading an expander of their
//    own rows; Add an entry a button whose list is the built-in rows the menu has not, Separator, Heading and Shortcut;
//    Reset to default greyed while the menu is the default.
//  - **Adding, moving and removing** change the menu the bar draws (Dock.BarRows), each one step of edit mode's undo
//    (EditHistory: the config before it), undone back to the default, which the file then holds nothing of.

using Karpo.Dock;
using Karpo.Tests.Menus;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed class BarMenuPanelTests
{
    private static readonly FlyoutItem[] TwoLayouts =
    [
        new(5600, "Work", new FlyoutIcon.Glyph("\uE8A9"), Checked: true),
        new(5601, "Play", new FlyoutIcon.Glyph("\uE7FC"), Checked: false),
    ];

    private static BarSpot Bar => new(MenuCorpus.Display, "One") { Attached = [MenuCorpus.Display] };

    /// <summary>§553: the Taskbar page of the panel as the dock builds it from <paramref name="config"/> (Dock.PanelOf, LooksRows), its picks.</summary>
    internal static (PanelPageView Page, List<OptionPick> Picks) Page(Config config)
    {
        List<OptionPick> picks = [];
        var items = TheDock.LooksItems(config, Bar, dark: true, picks, LooksScope.Taskbar, services: MenuCorpus.Services());
        return (PanelRows.Page(PanelPage.Taskbar, PanelRows.SideOf(PanelPage.Taskbar), PanelRows.GlyphOf(PanelPage.Taskbar), items), picks);
    }

    /// <summary>§585: the page's expander Taskbar menu, Behavior's last row.</summary>
    private static PanelRow Expander(PanelPageView page) => Assert.Single(page.Rows, row => row.Text == S.SettingsBarMenuHeader);

    /// <summary>§585: the rows in the expander Taskbar menu (§551: its preview over them left out).</summary>
    private static List<PanelRow> Section(PanelPageView page) => [.. Expander(page).Rows.Where(row => row.Kind != PanelRowKind.Preview)];

    /// <summary>§553: the rows a pick of the page finds its row in (Dock.LooksRow: Dock.LooksOf the Taskbar page's).</summary>
    private static IReadOnlyList<OptionRow> Rows(Config config) => TheDock.LooksOf(config, Bar, dark: true, LooksScope.Taskbar, services: MenuCorpus.Services());

    /// <summary>
    /// A control's <paramref name="id"/> done as the dock does it (Dock.LooksPicked, Apply): on a copy of the config, the
    /// row read again from it; the config before it a step of the undo. The config it makes.
    /// </summary>
    private static Config Act(Config config, List<OptionPick> picks, uint id, EditHistory<Config> history)
    {
        var pick = OptionMenu.PickOf(picks, id);
        Assert.NotNull(pick);
        var next = config.Clone();
        var row = OptionRows.Find(Rows(next), pick.Key);
        Assert.True(row is not { Enabled: false } && OptionMenu.ApplyRow(row, pick), $"{pick} changed nothing");
        history.Record(config);
        return next;
    }

    /// <summary>§551: <paramref name="row"/> dragged to place <paramref name="to"/> among its own, as the dock does it (Dock.PanelMoved: OptionMenu.Moved, the row found as LooksPicked finds it).</summary>
    internal static Config Moved(Config config, List<OptionPick> picks, PanelRow row, int to, EditHistory<Config> history)
    {
        Assert.True(row.Drags);
        var move = OptionMenu.Moved(OptionMenu.PickOf(picks, row.Item.Id), to);
        Assert.NotNull(move);
        var next = config.Clone();
        Assert.True(OptionMenu.ApplyRow(OptionRows.Find(Rows(next), move.Key), move), $"{move} changed nothing");
        history.Record(config);
        return next;
    }

    /// <summary>The bar's menu as the dock draws it from <paramref name="config"/> (ShowBarMenu), two layouts offered.</summary>
    private static string[] Menu(Config config) =>
        [.. TheDock.BarRows(config.BarMenuEntries, TwoLayouts, windowsTerminal: true, ShortcutLooks.None).Items.Select(item => item.Separator ? "—" : item.Text)];

    private static FlyoutTool Tool(PanelRow row, string name) => row.Tools.Single(tool => tool.Name == name);

    /// <summary>§551: the page's one preview: the taskbar menu's.</summary>
    private static PanelRow Preview(PanelPageView page) => Assert.Single(PanelRows.All(page.Rows), row => row.Kind == PanelRowKind.Preview);

    /// <summary>
    /// §585 (the person, 2026-11-09: "taskbar menu should only get an expansion section with the configuration inside there but
    /// not a full own section"): Taskbar menu is no side item of its own (§553) nor a section (§553 to §584) but one expander,
    /// Behavior's last row, "The same on every layout" under its name, its preview, entries, Add an entry and Reset to default
    /// in it; the dock builds it with the Taskbar page (LooksRows), so a display that shows None, with no Taskbar page and no
    /// bar to right-click, has none.
    /// </summary>
    [Fact]
    public void TheTaskbarMenuIsAnExpanderOfBehavior()
    {
        var content = SettingsPanelTests.Content();
        Assert.Equal([PanelPage.Karpo, PanelPage.Accounts, PanelPage.Layouts, PanelPage.Taskbar, PanelPage.Appearance], content.Shown);
        Assert.DoesNotContain(content.Pages, page => page.Side == S.SettingsBarMenuHeader);
        var taskbar = content.Of(PanelPage.Taskbar)!;
        Assert.Equal([S.SettingsSectionsBar, S.DockTaskbarBehaviour], taskbar.Rows.Where(row => row.Kind == PanelRowKind.Heading).Select(row => row.Text));
        Assert.All(taskbar.Rows.Where(row => row.Kind == PanelRowKind.Heading), row => Assert.Null(row.Detail));
        // Behavior's last row, the page's last: an expander, its line under its name.
        var menu = taskbar.Rows[^1];
        Assert.Equal((PanelRowKind.Expander, S.SettingsBarMenuHeader, "Taskbar/" + S.DockTaskbarBehaviour + "/" + S.SettingsBarMenuHeader, (string?)S.SettingsBarMenuEveryLayout), (menu.Kind, menu.Text, menu.Key, menu.Detail));
        Assert.Equal(PanelRowKind.Preview, menu.Rows[0].Kind);
        Assert.Equal(S.SettingsBarMenuReset, menu.Rows[^1].Text);
        // §684 (the person, 2026-11-12: "taskbar menu should just show how many entries there are in it and not which ones").
        var entries = menu.Rows.Count(row => row.Kind == PanelRowKind.Item);
        Assert.True(entries > 1);
        Assert.Equal(S.SettingsBarMenuEntries(entries), menu.Value);
        Assert.DoesNotContain(taskbar.Rows, row => row.Text == S.CoreBarMenuEdit || row.Text == S.SettingsBarMenuAdd || row.Text == S.SettingsBarMenuReset);

        // The dock's own: the Taskbar page's rows, and what a pick finds, are LooksOf's; the page left out where there is no layout.
        var looks = File.ReadAllText(Path.Combine(RepoSource.Root(), "Karpo.Dock", "Dock.BarLooks.cs"));
        Assert.Contains("picks, scope, license.Status, BarMenuServices(), LayoutMachineOf(scope));", looks);
        Assert.Contains("OptionRows.Find(LooksOf(of, bar, ReadsDark(of), scope, license.Status, BarMenuServices(), LayoutMachineOf(scope)), pick.Key);", looks);
        var panel = File.ReadAllText(Path.Combine(RepoSource.Root(), "Karpo.Dock", "Dock.SettingsPanel.cs"));
        Assert.Contains("if (own is null && scope == LooksScope.Taskbar)", panel);
        Assert.DoesNotContain("KarpoPage.BarMenu", panel);
    }

    /// <summary>
    /// §553: no row of the taskbar menu lost moving to the Taskbar page: every key its rows have (each entry's Remove and
    /// move, a shortcut's and a heading's fields, Add an entry's, Reset to default) is sent by a control of the page; none
    /// by the Karpo page, nor a row of it there.
    /// </summary>
    [Fact]
    public void NoRowIsLostAndNoneIsLeftOnTheKarpoPage()
    {
        var config = new Config();
        SceneTreeTests.PersonsBarMenu(config);
        var (page, picks) = Page(config);
        var sent = AccountsPageTests.KeysSent(page, picks);
        var notes = config.BarMenuEntries.Single(entry => entry.Name == "Notes").Id;
        var tools = config.BarMenuEntries.Single(entry => entry.Kind == BarMenuKind.Heading).Id;
        string[] wanted =
        [
            KarpoMenu.BarMenuAddRow, KarpoMenu.BarMenuResetRow,
            KarpoMenu.BarMenuKey("settings", "Move"), KarpoMenu.BarMenuKey(notes, "Remove"), KarpoMenu.BarMenuKey(notes, "Move"),
            KarpoMenu.BarMenuKey(notes, "Name"), KarpoMenu.BarMenuKey(notes, "Arguments"), KarpoMenu.BarMenuKey(tools, "Name"),
        ];
        Assert.All(wanted, key => Assert.Contains(key, sent));
        // Every key the page's builder made a pick for, and §551 every move its rows are dragged by, a control of the page sends.
        Assert.Equal(picks.Where(pick => pick.Kind != OptionPickKind.None).Select(pick => pick.Key).Concat(picks.Where(pick => pick.Moves.Length > 0).Select(pick => pick.Moves)).ToHashSet(), sent);

        List<OptionPick> karpo = [];
        _ = TheDock.SettingsItems(config, MenuCorpus.Services(), karpo);
        Assert.DoesNotContain(karpo, pick => pick.Key.StartsWith(KarpoMenu.BarMenuRow, StringComparison.Ordinal));
        var karpoPage = SettingsPanelTests.Content(edit: SceneTreeTests.PersonsBarMenu).Of(PanelPage.Karpo)!;
        Assert.DoesNotContain(PanelRows.All(karpoPage.Rows), row => row.Text is "Notes" or "Tools" || row.Text == S.SettingsBarMenuHeader || row.Text == S.SettingsBarMenuAdd);
    }

    /// <summary>The expander of a fresh config: the default menu's entries, Add an entry, Reset to default greyed.</summary>
    [Fact]
    public void TheExpanderListsTheMenuAddAndReset()
    {
        var config = new Config();
        var (page, _) = Page(config);
        var rows = Section(page);
        Assert.Equal(
            [S.CoreBarMenuLayouts, S.CoreWidgetDividerTitle, S.CoreBarMenuTaskManager, S.CoreWidgetDividerTitle, S.CoreBarMenuLockTaskbar, S.CoreBarMenuTaskbarSettings, S.SettingsBarMenuAdd, S.SettingsBarMenuReset],
            rows.Select(row => row.Text));
        // A separator is a row; the page's keys all its own.
        Assert.Equal(rows.Count, rows.Select(row => row.Key).Distinct().Count());
        Assert.Equal(PanelRows.All(page.Rows).Count(), PanelRows.All(page.Rows).Select(row => row.Key).Distinct().Count());
        Assert.All(rows.Take(6), row => Assert.Equal(PanelRowKind.Item, row.Kind));
        // §581 Taskbar settings stays: no Remove, why under its name; every other entry, §663 Lock the taskbar too, has its Remove.
        Assert.DoesNotContain(rows[5].Tools, tool => tool.Glyph == OptionGlyphs.Delete);
        Assert.Equal(S.SettingsBarMenuRequired, rows[5].Detail);
        Assert.All(rows.Take(5), row => Assert.True(Tool(row, S.DockMenuRemove).Enabled));
        // §551: Remove an icon button, no ↑ ↓: every entry, Taskbar settings too, dragged into order by its handle.
        Assert.Equal([OptionGlyphs.Delete], rows[0].Tools.Select(tool => tool.Glyph));
        Assert.Empty(rows[5].Tools);
        Assert.All(rows.Take(6), row => Assert.True(row.Drags));
        Assert.All(rows.Skip(6), row => Assert.False(row.Drags));
        Assert.Equal(rows.Take(6), PanelRows.Order(page.Rows, rows[2].Key));
        // Add an entry: a button, its list the built-in rows the menu has not, then Separator, Heading, Shortcut.
        var add = rows[6];
        Assert.True(add.Adds);
        var choices = add.Item.Children!.Select(child => child.Text).ToList();
        Assert.Equal([S.CoreWidgetDividerTitle, S.CoreWidgetHeadingTitle, S.SettingsBarMenuShortcut], choices[^3..]);
        Assert.Contains(S.SettingsAboutQuitHeader, choices);
        Assert.Contains(S.CoreBarMenuFileExplorer, choices);
        // §663: Lock the taskbar the default's again (§581 to §662 the person's to add), so not offered twice.
        Assert.DoesNotContain(S.CoreBarMenuLockTaskbar, choices);
        Assert.DoesNotContain(S.CoreBarMenuTaskbarSettings, choices);
        Assert.DoesNotContain(S.CoreBarMenuTaskManager, choices);
        // Reset to default: a button, greyed while the menu is the default.
        Assert.Equal((PanelRowKind.Action, true), (rows[7].Kind, rows[7].Greyed));
    }

    /// <summary>
    /// An entry added, moved and removed on the Taskbar page changes the menu the bar draws, each a step of the undo; undone,
    /// it is the default again and the file holds no menu.
    /// </summary>
    [Fact]
    public void AddMoveAndRemoveChangeTheMenuTheBarDrawsAndAreUndone()
    {
        var history = new EditHistory<Config>();
        var config = new Config();
        string[] before = ["Work", "Play", "—", "Task Manager", "—", "Lock the taskbar", "Taskbar settings"];
        Assert.Equal(before, Menu(config));

        // Add an entry › File Explorer: at the menu's end.
        var (page, picks) = Page(config);
        var explorer = Section(page).Single(row => row.Adds).Item.Children!.Single(child => child.Text == S.CoreBarMenuFileExplorer);
        config = Act(config, picks, explorer.Id, history);
        Assert.Equal([.. before, "File Explorer"], Menu(config));
        Assert.NotNull(config.BarMenu);
        Assert.False(Section(Page(config).Page).Single(row => row.Text == S.SettingsBarMenuReset).Greyed);

        // §551: File Explorer dragged over Task Manager: its row's move there (Dock.PanelMoved), one step of the undo.
        (page, picks) = Page(config);
        config = Moved(config, picks, Section(page).Single(row => row.Text == S.CoreBarMenuFileExplorer), 2, history);
        Assert.Equal(["Work", "Play", "—", "File Explorer", "Task Manager", "—", "Lock the taskbar", "Taskbar settings"], Menu(config));

        // Task Manager, Remove.
        (page, picks) = Page(config);
        config = Act(config, picks, Tool(Section(page).Single(row => row.Text == S.CoreBarMenuTaskManager), S.DockMenuRemove).Id, history);
        Assert.Equal(["Work", "Play", "—", "File Explorer", "—", "Lock the taskbar", "Taskbar settings"], Menu(config));
        Assert.Contains("\"barMenu\"", config.ToJson(), StringComparison.Ordinal);

        // Undone step by step: the menu as it was each time, then the default, which the file holds nothing of.
        config = history.Undo(config)!;
        Assert.Equal(["Work", "Play", "—", "File Explorer", "Task Manager", "—", "Lock the taskbar", "Taskbar settings"], Menu(config));
        config = history.Undo(config)!;
        Assert.Equal([.. before, "File Explorer"], Menu(config));
        config = history.Undo(config)!;
        Assert.Equal(before, Menu(config));
        Assert.Null(config.BarMenu);
        Assert.DoesNotContain("barMenu", config.ToJson(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A shortcut and a heading added: each an expander of its own rows — a shortcut's Target (a pin's kinds), Name and
    /// Arguments, a heading's Name — typed in place, and drawn in the bar's menu by what was typed; Reset to default asks
    /// first, then puts the menu back as it came.
    /// </summary>
    [Fact]
    public void AShortcutAndAHeadingAreEditedInPlaceAndResetPutsTheMenuBack()
    {
        var history = new EditHistory<Config>();
        var config = new Config();
        foreach (var kind in new[] { S.CoreWidgetHeadingTitle, S.SettingsBarMenuShortcut })
        {
            var (page, picks) = Page(config);
            config = Act(config, picks, Section(page).Single(row => row.Adds).Item.Children!.Single(child => child.Text == kind).Id, history);
        }
        var rows = Section(Page(config).Page);
        var heading = rows.Single(row => row.Text == S.CoreWidgetHeadingTitle);
        var shortcut = rows.Single(row => row.Text == S.SettingsBarMenuShortcut);
        Assert.Equal((PanelRowKind.Expander, PanelRowKind.Expander), (heading.Kind, shortcut.Kind));
        Assert.Equal([S.SettingsPinNameHeader], heading.Rows.Select(row => row.Text));
        Assert.Equal([S.SettingsPinTargetHeader, S.SettingsPinNameHeader, S.SettingsBarMenuArguments], shortcut.Rows.Select(row => row.Text));
        // Nothing to open yet: the menu draws nothing of it, the heading over nothing neither.
        Assert.Equal(["Work", "Play", "—", "Task Manager", "—", "Lock the taskbar", "Taskbar settings"], Menu(config));

        // Typed in place, as the panel keeps a text box's words (Dock.PanelTyped → FieldKept → LooksKept).
        Config Typed(Config of, string entryRow, string box, string words)
        {
            var (page, picks) = Page(of);
            var row = PanelRows.All(Section(page)).Single(each => each.Text == entryRow).Rows.Single(each => each.Text == box);
            var pick = OptionMenu.PickOf(picks, row.Field!.Id)!;
            var next = of.Clone();
            Assert.True(FieldPicks.KeepRow(OptionRows.Find(Rows(next), pick.Key), new FieldCommit(row.Field.Id, words, null)));
            return next;
        }
        config = Typed(config, S.CoreWidgetHeadingTitle, S.SettingsPinNameHeader, "Tools");
        config = Typed(config, S.SettingsBarMenuShortcut, S.SettingsPinNameHeader, "Notes");
        // Its target, as a pin's Target sets it (here the row's own set, as a pick or Browse… hands it a path).
        var target = OptionRows.Find(Rows(config), KarpoMenu.BarMenuKey(config.BarMenuEntries[^1].Id, "") + OptionPickers.PinFile)!;
        Assert.True(target.Pick!(new OptionFound("notepad", Value: @"C:\Windows\notepad.exe")));
        Assert.Equal(["Work", "Play", "—", "Task Manager", "—", "Lock the taskbar", "Taskbar settings", "Tools", "Notes"], [.. TheDock.BarRows(config.BarMenuEntries, TwoLayouts, true, ShortcutLooks.None).Items.Select(item => item.Separator ? "—" : item.Text)]);
        Assert.True(TheDock.BarRows(config.BarMenuEntries, TwoLayouts, true, ShortcutLooks.None).Items.Single(item => item.Text == "Tools").Header);
        Assert.Equal(@"C:\Windows\notepad.exe", config.BarMenuEntries[^1].Target);

        // Reset to default: asked first (Windows' dialog in the dock), then the default, out of the file.
        var reset = OptionRows.Find(Rows(config), KarpoMenu.BarMenuResetRow)!;
        Assert.NotNull(reset.Ask);
        Assert.True(reset.Enabled && reset.Set!(0));
        Assert.Null(config.BarMenu);
        Assert.Equal(["Work", "Play", "—", "Task Manager", "—", "Lock the taskbar", "Taskbar settings"], Menu(config));
    }

    /// <summary>
    /// §553: a shortcut's Browse… on the Taskbar page is Windows' open dialog, asked before the page's own looks handler
    /// takes the pick (which would set nothing with it): Dock.OptionPicked's order.
    /// </summary>
    [Fact]
    public void BrowseIsAskedBeforeTheLooksHandler()
    {
        var folders = File.ReadAllText(Path.Combine(RepoSource.Root(), "Karpo.Dock", "Dock.Folders.cs"));
        var picked = folders.IndexOf("private void OptionPicked(", StringComparison.Ordinal);
        var browse = folders.IndexOf("if (pick.Kind == OptionPickKind.Browse)", picked, StringComparison.Ordinal);
        var looks = folders.IndexOf("if (what.Looks is { } bar)", picked, StringComparison.Ordinal);
        Assert.True(picked >= 0 && browse > picked && browse < looks, $"OptionPicked {picked}, Browse {browse}, Looks {looks}");
    }

    /// <summary>
    /// §551 (the person, 2026-11-07: "taskbar menu should show a preview"): the Taskbar page has one preview, at the top of its
    /// expander Taskbar menu (§585; §553 under its section's heading) — the menu
    /// of the bar's own rows (Dock.BarRows: the entries, the layouts, the lock), as tall as they are; another picture as an
    /// entry is added, moved and removed, and as the bar's own look changes (the layout's Flyouts: a menu of the bar wears
    /// it, never the settings' look); the same rows again, the same picture.
    /// </summary>
    [Fact]
    public void ThePageShowsTheMenuAsTheBarDrawsItAtItsTop()
    {
        var history = new EditHistory<Config>();
        var config = new Config();
        var (page, picks) = Page(config);
        var preview = Preview(page);
        Assert.Same(preview, Expander(page).Rows[0]);
        Assert.Equal(config.ActiveLayout(MenuCorpus.Display).Id, preview.Shows!.Layout.Id);
        var shows = preview.Shows!;
        Assert.Equal((LookSample.Menu, S.DockEditPreviewOf(S.SettingsBarMenuHeader)), (LookPreview.SampleOf(shows), LookPreview.NameOf(shows)));
        var bar = TheDock.BarRows(config.BarMenuEntries, TheDock.LayoutRows(config.Layouts, shows.Layout.Id), File.Exists(QuickLinks.WindowsTerminal), ShortcutLooks.None, config.LockTaskbar).Items;
        Assert.Equal(bar.Select(item => (item.Text, item.Separator, item.Checked)), LookPreview.MenuOf(shows).Select(item => (item.Text, item.Separator, item.Checked)));
        // §561: the one layout a new config has is in it, ticked.
        var only = Assert.Single(config.Layouts);
        Assert.Contains(LookPreview.MenuOf(shows), item => item.Text == only.Name && item.Checked == true);
        Assert.Equal(MenuSample.Plan(bar).Height + (2 * LookPreview.AroundDip), LookPreview.BoxDip(shows, new SettingsPanelTests.Words(), 1.5f));
        // Rows of 32, rules of 9, 3 over and under: Explorer's.
        Assert.Equal(3 + (bar.Count(item => !item.Separator) * 32) + (bar.Count(item => item.Separator) * 9) + 3, MenuSample.Plan(bar).Height);

        var windows = AddListPictures.LookFor(dark: true);
        var size = new System.Numerics.Vector2(780, 300);
        string Key(Config of) => LookPreview.KeyOf(Preview(Page(of).Page).Shows!, windows, 1.5f, size);
        List<string> keys = [Key(config)];
        Assert.Equal(keys[0], Key(config));
        // Added, moved, removed: another picture each time.
        var explorer = Section(page).Single(row => row.Adds).Item.Children!.Single(child => child.Text == S.CoreBarMenuFileExplorer);
        config = Act(config, picks, explorer.Id, history);
        keys.Add(Key(config));
        (page, picks) = Page(config);
        config = Moved(config, picks, Section(page).Single(row => row.Text == S.CoreBarMenuFileExplorer), 0, history);
        keys.Add(Key(config));
        Assert.Equal(S.CoreBarMenuFileExplorer, LookPreview.MenuOf(Preview(Page(config).Page).Shows!)[0].Text);
        (page, picks) = Page(config);
        config = Act(config, picks, Tool(Section(page).Single(row => row.Text == S.CoreBarMenuTaskManager), S.DockMenuRemove).Id, history);
        keys.Add(Key(config));
        // The bar's look: its flyouts' outline another picture.
        Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.Flyouts), Tone.Outline, Karpo.Core.Paint.Parse("#FFB900")));
        keys.Add(Key(config));
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }
}
