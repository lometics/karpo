// The taskbar's own menu (Karpo.Core\BarMenus.cs): §532 the person's to change again ("the right click context
// menu of the taskbar should be editable in the settings again. it should be scoped on a global level"), one for
// Karpo as a whole at the file's top ("barMenu"); absent, §581 Windows 11's (the layouts, then Task Manager and Taskbar
// settings; §663 Lock the taskbar again, over Taskbar settings), and an unchanged default never written. "Taskbar
// settings" is always in it; Quit Karpo may be added and is in no default.

namespace Karpo.Tests.Core;

public sealed class BarMenuModelTests
{
    private static (BarMenuKind Kind, BarMenuAction Action)[] Shape(IEnumerable<BarMenuEntry> entries) =>
        [.. entries.Select(entry => (entry.Kind, entry.Action))];

    // --- The default menu ---------------------------------------------------------------

    /// <summary>
    /// §581 ("the default right click menu on the taskbar should be like in windows: task manager and taskbar settings ... and
    /// the layout selector should be also there"): the layouts, then behind a divider Task Manager; §663 ("locking the
    /// taskbar should be shown in the default right click context menu again"): behind another, Lock the taskbar over
    /// Taskbar settings, as Windows 10's menu ended.
    /// </summary>
    [Fact]
    public void TheMenuIsTheLayoutsTaskManagerThenLockAndTaskbarSettings() =>
        Assert.Equal(
            [
                (BarMenuKind.Action, BarMenuAction.Layouts),
                (BarMenuKind.Divider, (BarMenuAction)0),
                (BarMenuKind.Action, BarMenuAction.TaskManager),
                (BarMenuKind.Divider, (BarMenuAction)0),
                (BarMenuKind.Action, BarMenuAction.LockTaskbar),
                (BarMenuKind.Action, BarMenuAction.TaskbarSettings),
            ],
            Shape(BarMenus.Default()));

    /// <summary>§581: the two rows named and pictured as Windows 11's own taskbar menu has them.</summary>
    [Fact]
    public void WindowsRowsSayWhatWindowsSays()
    {
        Assert.Equal(("Task Manager", ""), (BarMenus.Info(BarMenuAction.TaskManager)!.Name, BarMenus.Info(BarMenuAction.TaskManager)!.Glyph));
        Assert.Equal(("Taskbar settings", OptionGlyphs.Settings), (BarMenus.Info(BarMenuAction.TaskbarSettings)!.Name, BarMenus.Info(BarMenuAction.TaskbarSettings)!.Glyph));
    }

    [Fact]
    public void TheIdsAreTheSameEveryTimeAndUnique()
    {
        var first = BarMenus.Default().Select(entry => entry.Id).ToList();

        Assert.Equal(first, BarMenus.Default().Select(entry => entry.Id));
        Assert.Equal(first.Count, first.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>§532: a file that says nothing of the menu has the default, and writes none.</summary>
    [Fact]
    public void AFileWithoutAMenuHasTheDefaultAndWritesNone()
    {
        var config = Configs.Repaired("{}");

        Assert.Null(config.BarMenu);
        Assert.Equal(Shape(BarMenus.Default()), Shape(config.BarMenuEntries));
        Assert.Equal(BarMenus.Default().Select(entry => entry.Id), config.BarMenuEntries.Select(entry => entry.Id));
        Assert.DoesNotContain("barMenu", config.ToJson(), StringComparison.Ordinal);
    }

    /// <summary>§532: Quit Karpo may be added, and the default has it not (the person is still deciding where Quit lives).</summary>
    [Fact]
    public void ARowOfNoDefaultIsAddable()
    {
        Assert.DoesNotContain(BarMenus.Default(), entry => entry.Action == BarMenuAction.QuitKarpo);
        Assert.Contains(BarMenus.Addable(BarMenus.Default()), info => info.Action == BarMenuAction.QuitKarpo);
    }

    /// <summary>§663: Lock the taskbar is the default's, so not offered to add twice; it may be taken out, and then added back.</summary>
    [Fact]
    public void LockTheTaskbarIsInTheDefaultAndAddableOnceTakenOut()
    {
        var entries = BarMenus.Default();
        Assert.DoesNotContain(BarMenus.Addable(entries), info => info.Action == BarMenuAction.LockTaskbar);
        var at = entries.FindIndex(entry => entry.Action == BarMenuAction.LockTaskbar);
        Assert.Null(BarMenus.WhyNotRemove(entries[at]));
        Assert.True(BarMenus.Remove(entries, at));
        Assert.Contains(BarMenus.Addable(entries), info => info.Action == BarMenuAction.LockTaskbar);
    }

    [Fact]
    public void QuitKarpoIsNamedAsThePanelsAndOpensNoFile()
    {
        Assert.Equal(S.SettingsAboutQuitHeader, BarMenus.Info(BarMenuAction.QuitKarpo)!.Name);
        Assert.Null(BarMenus.Target(BarMenuAction.QuitKarpo, windowsTerminal: true));
    }

    // --- The person's own (§532) ------------------------------------------------------

    /// <summary>A menu of every kind — built-in rows, a shortcut with all it holds, a divider, a heading — is read back as it was written.</summary>
    [Fact]
    public void AMenuOfEveryKindIsReadBackAsWritten()
    {
        var config = new Config();
        Assert.True(BarMenus.Change(config, list =>
        {
            list.Clear();
            list.AddRange(
            [
                BarMenuEntry.Of(BarMenuAction.TaskbarSettings, "settings"),
                BarMenuEntry.Heading("Tools"),
                BarMenuEntry.Of(BarMenuAction.Terminal),
                BarMenuEntry.Shortcut(@"C:\Tools\tool.exe", name: "Tool", icon: "\uE774", arguments: "--fast"),
                BarMenuEntry.Divider(),
                BarMenuEntry.Of(BarMenuAction.QuitKarpo),
            ]);
            return true;
        }));

        var json = config.ToJson();
        var read = Configs.RoundTrip(config, out _);

        // Nothing of it the repair had to mend.
        Assert.False(BarMenus.Repair([.. read.BarMenu!.Select(entry => entry.Copy())]));
        Assert.Contains("\"barMenu\"", json, StringComparison.Ordinal);
        Assert.Contains("\"quitKarpo\"", json, StringComparison.Ordinal);
        var entries = read.BarMenuEntries;
        Assert.Equal(config.BarMenu!.Count, entries.Count);
        Assert.All(config.BarMenu.Zip(entries), pair => Assert.True(pair.First.SameAs(pair.Second) && pair.First.Id == pair.Second.Id, pair.First.Kind.ToString()));
        var shortcut = entries.Single(entry => entry.Kind == BarMenuKind.Shortcut);
        Assert.Equal((@"C:\Tools\tool.exe", "Tool", "\uE774", "--fast"), (shortcut.Target, shortcut.Name, shortcut.Icon, shortcut.Arguments));
        Assert.Equal("Tools", entries.Single(entry => entry.Kind == BarMenuKind.Heading).Name);
    }

    /// <summary>§532: a menu changed back into the default leaves the file again, as does Reset to default.</summary>
    [Fact]
    public void AMenuChangedBackIntoTheDefaultLeavesTheFile()
    {
        var config = new Config();
        Assert.True(BarMenus.Change(config, list => BarMenus.Move(list, 3, 0)));
        Assert.NotNull(config.BarMenu);
        Assert.Contains("\"barMenu\"", config.ToJson(), StringComparison.Ordinal);

        Assert.True(BarMenus.Change(config, list => BarMenus.Move(list, 0, 3)));

        Assert.Null(config.BarMenu);
        Assert.DoesNotContain("barMenu", config.ToJson(), StringComparison.Ordinal);
        // Nothing moved, nothing changed.
        Assert.False(BarMenus.Change(config, list => BarMenus.Move(list, 2, 2)));
        // A file holding the default (written by hand) reads as none.
        var typed = Configs.Repaired("{ \"barMenu\": [ { \"id\": \"c\", \"kind\": \"action\", \"action\": \"layouts\" }, { \"id\": \"d\", \"kind\": \"divider\" }, "
            + "{ \"id\": \"e\", \"kind\": \"action\", \"action\": \"taskManager\" }, { \"id\": \"f\", \"kind\": \"divider\" }, "
            + "{ \"id\": \"l\", \"kind\": \"action\", \"action\": \"lockTaskbar\" }, { \"id\": \"a\", \"kind\": \"action\", \"action\": \"taskbarSettings\" } ] }");
        Assert.Null(typed.BarMenu);
    }

    /// <summary>
    /// §532, the way back: §581 "Taskbar settings" cannot be taken out (its Remove says why), and a list typed without it
    /// gets it back first; everything else can.
    /// </summary>
    [Fact]
    public void TaskbarSettingsIsAlwaysInTheMenu()
    {
        var config = new Config();
        var settings = config.BarMenuEntries.Count - 1;

        Assert.Equal(BarMenuAction.TaskbarSettings, config.BarMenuEntries[settings].Action);
        Assert.Equal(S.SettingsBarMenuRequired, BarMenus.WhyNotRemove(config.BarMenuEntries[settings]));
        Assert.False(BarMenus.Change(config, list => BarMenus.Remove(list, settings)));
        Assert.Null(config.BarMenu);
        Assert.All(config.BarMenuEntries.Take(settings), entry => Assert.Null(BarMenus.WhyNotRemove(entry)));
        Assert.True(BarMenus.Change(config, list => BarMenus.Remove(list, 2)));
        Assert.Equal([BarMenuAction.Layouts, BarMenuAction.LockTaskbar, BarMenuAction.TaskbarSettings], config.BarMenuEntries.Where(entry => entry.Kind == BarMenuKind.Action).Select(entry => entry.Action));

        var typed = Configs.Repaired("{ \"barMenu\": [ { \"id\": \"t\", \"kind\": \"action\", \"action\": \"taskManager\" } ] }");
        Assert.Equal([(BarMenuKind.Action, BarMenuAction.TaskbarSettings), (BarMenuKind.Action, BarMenuAction.TaskManager)], Shape(typed.BarMenuEntries));
    }

    /// <summary>
    /// §532: a list from the file is made one the menu can draw: what this build doesn't know goes, as does a built-in row's
    /// second showing; ids are unique; words trimmed, a heading's held to its most; a shortcut still to choose stays.
    /// </summary>
    [Fact]
    public void AListFromTheFileIsRepaired()
    {
        var config = Configs.Repaired("""
            { "barMenu": [
              { "id": "x", "kind": "action", "action": "taskbarSettings" },
              { "id": "x", "kind": "action", "action": "fileExplorer" },
              { "id": "y", "kind": "action", "action": "fileExplorer" },
              { "id": "z", "kind": "action", "action": "somethingNew" },
              { "id": "w", "kind": "somethingElse" },
              { "id": "h", "kind": "heading", "name": "  Tools  ", "target": "C:\\a.exe" },
              { "id": "s", "kind": "shortcut", "name": " " }
            ] }
            """);

        var entries = config.BarMenuEntries;
        Assert.Equal(
            [(BarMenuKind.Action, BarMenuAction.TaskbarSettings), (BarMenuKind.Action, BarMenuAction.FileExplorer), (BarMenuKind.Heading, (BarMenuAction)0), (BarMenuKind.Shortcut, (BarMenuAction)0)],
            Shape(entries));
        Assert.Equal(entries.Count, entries.Select(entry => entry.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(("Tools", (string?)null), (entries[2].Name, entries[2].Target));
        Assert.Null(entries[3].Name);
        var heading = new List<BarMenuEntry> { BarMenuEntry.Of(BarMenuAction.TaskbarSettings), BarMenuEntry.Heading(new string('a', 90)) };
        Assert.True(BarMenus.Repair(heading));
        Assert.Equal(BarMenus.MostHeading, heading[1].Name!.Length);
    }

    /// <summary>§532: Add puts a row at the end; a built-in row the menu has is not added twice, nor anything past the most.</summary>
    [Fact]
    public void AnEntryIsAddedAtTheEndOnce()
    {
        var entries = BarMenus.Default();
        Assert.True(BarMenus.Add(entries, BarMenuEntry.Of(BarMenuAction.QuitKarpo)));
        Assert.Equal(BarMenuAction.QuitKarpo, entries[^1].Action);
        Assert.False(BarMenus.Add(entries, BarMenuEntry.Of(BarMenuAction.QuitKarpo)));
        Assert.DoesNotContain(BarMenus.Addable(entries), info => info.Action is BarMenuAction.QuitKarpo or BarMenuAction.TaskbarSettings or BarMenuAction.TaskManager);
        while (entries.Count < BarMenus.MostEntries)
        {
            entries.Add(BarMenuEntry.Divider());
        }
        Assert.False(BarMenus.Add(entries, BarMenuEntry.Divider()));
    }

    // --- Names and places ------------------------------------------------------------------

    [Fact]
    public void EveryBuiltInRowHasANameAndOneGlyph()
    {
        foreach (var action in Enum.GetValues<BarMenuAction>())
        {
            var info = BarMenus.Info(action);
            Assert.NotNull(info);
            Assert.False(string.IsNullOrWhiteSpace(info.Name));
            Assert.True(info.Glyph is { Length: 1 } glyph && glyph[0] is >= '\uE000' and <= '\uF8FF', $"{action}'s glyph");
        }
        Assert.Equal(Enum.GetValues<BarMenuAction>().Length, BarMenus.Actions.Count);
    }

    /// <summary>The places the menu can draw open what Windows opens.</summary>
    [Theory]
    [InlineData(BarMenuAction.FileExplorer, "explorer.exe")]
    [InlineData(BarMenuAction.DeviceManager, "devmgmt.msc")]
    [InlineData(BarMenuAction.DiskManagement, "diskmgmt.msc")]
    [InlineData(BarMenuAction.EventViewer, "eventvwr.msc")]
    [InlineData(BarMenuAction.NetworkConnections, "ms-settings:network")]
    [InlineData(BarMenuAction.PowerOptions, "ms-settings:powersleep")]
    [InlineData(BarMenuAction.System, "ms-settings:about")]
    [InlineData(BarMenuAction.Display, "ms-settings:display")]
    [InlineData(BarMenuAction.Sound, "ms-settings:sound")]
    [InlineData(BarMenuAction.InstalledApps, "ms-settings:appsfeatures")]
    public void ABuiltInPlaceOpensWhatWindowsOpens(BarMenuAction action, string ends)
    {
        var target = BarMenus.Target(action, windowsTerminal: true);

        Assert.NotNull(target);
        Assert.EndsWith(ends, target.Value.File, StringComparison.OrdinalIgnoreCase);
        Assert.Null(target.Value.Verb);
    }

    [Fact]
    public void TheTerminalIsWindowsPowerShellWithoutWindowsTerminal()
    {
        Assert.Equal("Terminal", BarMenus.NameOf(BarMenuAction.Terminal, windowsTerminal: true));
        Assert.Equal("Windows PowerShell", BarMenus.NameOf(BarMenuAction.Terminal, windowsTerminal: false));
        Assert.Equal("Windows PowerShell (Admin)", BarMenus.NameOf(BarMenuAction.TerminalAdmin, windowsTerminal: false));
        Assert.Equal("powershell.exe", BarMenus.Target(BarMenuAction.TerminalAdmin, windowsTerminal: false)!.Value.File);
        Assert.Equal("runas", BarMenus.Target(BarMenuAction.TerminalAdmin, windowsTerminal: false)!.Value.Verb);
        Assert.Equal("wt.exe", BarMenus.Target(BarMenuAction.Terminal, windowsTerminal: true)!.Value.File);
        Assert.Null(BarMenus.Target(BarMenuAction.Terminal, windowsTerminal: true)!.Value.Verb);
    }

    /// <summary>Karpo's own rows, Run and Show desktop are the dock's to do, not a file to open.</summary>
    [Theory]
    [InlineData(BarMenuAction.TaskManager)]
    [InlineData(BarMenuAction.Layouts)]
    [InlineData(BarMenuAction.TaskbarSettings)]
    [InlineData(BarMenuAction.LockTaskbar)]
    [InlineData(BarMenuAction.Run)]
    [InlineData(BarMenuAction.ShowDesktop)]
    public void TheDocksOwnRowsOpenNoFile(BarMenuAction action) => Assert.Null(BarMenus.Target(action, windowsTerminal: true));

    [Theory]
    [InlineData("https://www.github.com/karpo", "github.com")]
    [InlineData(@"C:\Windows\notepad.exe", "notepad")]
    [InlineData(@"""C:\Program Files\Tool\tool.exe""", "tool")]
    [InlineData(@"C:\Users\Ann\Documents\", "Documents")]
    [InlineData(@"D:", @"D:\")]
    [InlineData(@"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", "WindowsCalculator")]
    [InlineData("calc.exe", "calc")]
    [InlineData("ms-settings:display", "ms-settings:display")]
    public void AShortcutWithoutANameIsNamedByItsTarget(string target, string name) =>
        Assert.Equal(name, BarMenus.FallbackName(target));
}
