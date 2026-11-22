// edit: undo and redo while arranging (docs\widgets.md §352; Karpo.Dock\EditHistory.cs). Held here:
//
//  - **The history** (EditHistory): undo steps back through the snapshots recorded, each undone step
//    redoable; a new change drops what could be redone; at most 50 steps, the oldest going first; one
//    history an edit mode.
//  - **Every kind of change comes back** from its snapshot as the dock applies it (Config.Restored,
//    saved): a widget moved, added, removed; a setting; a layout added, deleted — undone and redone, the
//    file saying each step's settings, never a merge keeping what came after.
//  - **One choke point**: every change of edit mode is committed through Dock.Apply, which records it
//    (an undo's own not), and a bar's height dragged in place records its own; the history goes with
//    edit mode and with someone else's change to the file.
//  - **The keys**: Ctrl+Z undoes, Ctrl+Y and Ctrl+Shift+Z redo, in edit mode's one keymap, wherever the
//    keys are (the hook), but not while a widget follows the pointer or one of the dock's menus has them.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

[Collection(ConfigFile.Collection)]
public sealed class EditHistoryTests
{
    /// <summary>Undo steps back through what was recorded, each step redoable in turn; nothing to step past either end.</summary>
    [Fact]
    public void UndoAndRedoStepThroughTheSnapshots()
    {
        var history = new EditHistory<string>();
        Assert.Equal((false, false), (history.CanUndo, history.CanRedo));
        Assert.Null(history.Undo("a"));
        history.Record("a");
        history.Record("b");
        Assert.Equal((true, false), (history.CanUndo, history.CanRedo));
        // Now "c": back to "b", then "a"; then forward again.
        Assert.Equal("b", history.Undo("c"));
        Assert.Equal("a", history.Undo("b"));
        Assert.Equal((false, true), (history.CanUndo, history.CanRedo));
        Assert.Null(history.Undo("a"));
        Assert.Equal("b", history.Redo("a"));
        Assert.Equal("c", history.Redo("b"));
        Assert.Null(history.Redo("c"));
        Assert.Equal("b", history.Undo("c"));
    }

    /// <summary>A new change after an undo drops what could have been redone, as everywhere; Clear forgets both ways.</summary>
    [Fact]
    public void ANewChangeDropsTheRedo()
    {
        var history = new EditHistory<string>();
        history.Record("a");
        Assert.Equal("a", history.Undo("b"));
        Assert.True(history.CanRedo);
        history.Record("a");
        Assert.False(history.CanRedo);
        Assert.True(history.CanUndo);
        history.Clear();
        Assert.Equal((false, false), (history.CanUndo, history.CanRedo));
    }

    /// <summary>At most <see cref="EditHistory{T}.Most"/> steps are kept, the oldest going first.</summary>
    [Fact]
    public void ItKeepsTheLastFifty()
    {
        var history = new EditHistory<string>();
        for (var step = 0; step < EditHistory<string>.Most + 10; step++)
        {
            history.Record($"{step}");
        }
        var undone = new List<string>();
        var now = "now";
        while (history.Undo(now) is { } back)
        {
            undone.Add(back);
            now = back;
        }
        Assert.Equal(50, EditHistory<string>.Most);
        Assert.Equal(EditHistory<string>.Most, undone.Count);
        Assert.Equal(($"{EditHistory<string>.Most + 9}", "10"), (undone[0], undone[^1]));
    }

    /// <summary>Each kind of change edit mode makes, done to a config as the dock does it (a copy changed).</summary>
    public static TheoryData<string> Changes() => ["move", "add", "remove", "setting", "layout added", "layout deleted"];

    private static void Change(Config config, string change)
    {
        var islands = config.DefaultLayout.Islands;
        switch (change)
        {
            case "move":
                var first = islands[0];
                islands.RemoveAt(0);
                islands.Add(first);
                break;
            case "add":
                islands.Add(new BarItem { Id = "added", Kind = BarKind.Volume });
                break;
            case "remove":
                islands.RemoveAt(islands.Count - 1);
                break;
            case "setting":
                Assert.True(LookPalettes.Wear(config, LookPalettes.Of(LookPalettes.GraphiteId)!));
                break;
            case "layout added":
                config.SelectLayout("display", BarLooks.Add(config, "Work").Id, "Display");
                break;
            case "layout deleted":
                Assert.True(config.DeleteLayout(config.Layouts[^1].Id));
                break;
        }
    }

    /// <summary>
    /// Every kind of change comes back from its snapshot as the dock applies an undo (Config.Restored, then
    /// saved as every change is): the file says the settings before it; redone, those after it — never a
    /// merge that keeps the later change as someone else's (Config.Save merges against what a config knew
    /// of the file).
    /// </summary>
    [Theory]
    [MemberData(nameof(Changes))]
    public void EachKindOfChangeIsUndoneAndRedoneFromItsSnapshot(string change)
    {
        ConfigFile.Delete();
        var config = Config.Load();
        config.EditScreen("display", "Display");
        BarLooks.Add(config, "Spare");
        config = config.Save();
        var history = new EditHistory<Config>();
        // As Dock.Apply commits a change: a copy changed, the config before it recorded, the copy saved.
        var before = config;
        var next = config.Clone();
        Change(next, change);
        history.Record(before);
        config = next.Save();
        var after = config.ToJson();
        var afterOnFile = Config.Load().ToJson();
        Assert.NotEqual(before.ToJson(), after);

        // Undo (Dock.StepTo): the step before, restored on the file as it is now, saved.
        config = config.Restored(history.Undo(config)!).Save();
        Assert.Equal(before.ToJson(), config.ToJson());
        Assert.Equal(before.ToJson(), Config.Load().ToJson());
        // Redo: the change again.
        config = config.Restored(history.Redo(config)!).Save();
        Assert.Equal(after, config.ToJson());
        Assert.Equal(afterOnFile, Config.Load().ToJson());
        ConfigFile.Delete();
    }

    /// <summary>
    /// One choke point (§352): every change edit mode commits goes through Dock.Apply, which records the
    /// config before it while edit mode is on — not an undo's or a redo's own, not a reload of someone
    /// else's (save: false); a bar's height dragged, written in place, records the height it had; the
    /// history starts and ends with edit mode, and a change written into the file by someone else ends it.
    /// No config is written in edit mode but through those two.
    /// </summary>
    [Fact]
    public void EveryChangeOfEditModeIsRecordedInOnePlace()
    {
        var dock = RepoSource.Read("Karpo.Dock", "Dock.cs");
        Assert.Contains("if (save && changed && editor is not null && !stepping)", dock);
        Assert.Contains("history.Record(config);", dock);
        var height = dock[dock.IndexOf("private void KeepHeight(", StringComparison.Ordinal)..];
        Assert.Contains("before.ActiveLayout(displayId).Size = from;", height[..height.IndexOf("Rebuild();", StringComparison.Ordinal)]);
        Assert.Contains("history.Record(before);", height[..height.IndexOf("Rebuild();", StringComparison.Ordinal)]);
        var begin = dock[dock.IndexOf("private void BeginEdit(", StringComparison.Ordinal)..dock.IndexOf("private DockWindow? BarOffer(", StringComparison.Ordinal)];
        Assert.Contains("history.Clear();", begin);
        var end = dock[dock.IndexOf("private void EndEdit(", StringComparison.Ordinal)..dock.IndexOf("private void FocusBar(", StringComparison.Ordinal)];
        Assert.Contains("history.Clear();", end);
        var reload = dock[dock.IndexOf("private void ConfigChanged(", StringComparison.Ordinal)..];
        Assert.Contains("history.Clear();", reload[..reload.IndexOf("TakeTheirs(fresh);", StringComparison.Ordinal)]);
        var steps = RepoSource.Read("Karpo.Dock", "EditHistory.cs");
        Assert.Contains("Apply(config.Restored(to));", steps);
        Assert.Contains("stepping = true;", steps);
        // No config written in place anywhere else of the dock's: every edit a copy applied (Dock.Apply), every
        // save through the dock's one way to write the file (ConfigSaves.cs, Dock.KeepConfig).
        foreach (var path in RepoSource.CSharp("Karpo.Dock"))
        {
            var text = File.ReadAllText(path);
            var name = Path.GetFileName(path);
            var saves = text.Split("config.Save()").Length - 1;
            Assert.True(name == "ConfigSaves.cs" ? saves == 1 : saves == 0, $"{name} saves the config {saves} times");
        }
    }

    /// <summary>
    /// The keys (§352), edit mode's one keymap: Ctrl+Z undoes, Ctrl+Y and Ctrl+Shift+Z redo, held down
    /// stepping on; without Ctrl, Z and Y are nothing of edit mode's. The bar, a locked one under a folder
    /// too, and the folder hand them to the dock's undo and redo.
    /// </summary>
    [Fact]
    public void CtrlZUndoesAndCtrlYRedoes()
    {
        Assert.Equal(EditKeyAct.Undo, EditKeys.Of('Z', control: true, shift: false, repeat: false).Act);
        Assert.Equal(EditKeyAct.Redo, EditKeys.Of('Z', control: true, shift: true, repeat: false).Act);
        Assert.Equal(EditKeyAct.Redo, EditKeys.Of('Y', control: true, shift: false, repeat: false).Act);
        Assert.Equal(EditKeyAct.Undo, EditKeys.Of('Z', control: true, shift: false, repeat: true).Act);
        Assert.Equal(EditKeyAct.None, EditKeys.Of('Z', control: false, shift: false, repeat: false).Act);
        Assert.Equal(EditKeyAct.None, EditKeys.Of('Y', control: false, shift: true, repeat: false).Act);
        var dock = RepoSource.Read("Karpo.Dock", "Dock.cs");
        Assert.Contains("stroke.Act is not (EditKeyAct.Escape or EditKeyAct.Paste or EditKeyAct.Undo or EditKeyAct.Redo)", dock);
        // The bar's keys, the arranged folder's and a menu's passed on, one way (Dock.SelectionAct, SelectionKeysTests).
        Assert.True(EditKeys.OfTheSelection(EditKeyAct.Undo) && EditKeys.OfTheSelection(EditKeyAct.Redo));
        var selection = RepoSource.Read("Karpo.Dock", "Dock.EditSelect.cs");
        Assert.Matches(@"case EditKeyAct\.Undo:\s+EditUndo\(\);", selection);
        Assert.Matches(@"case EditKeyAct\.Redo:\s+EditRedo\(\);", selection);
        Assert.Contains("return new FolderFlyoutEvent(FolderFlyoutAction.Selection, keyboard ? line : null, focused, Act: act);", RepoSource.Read("Karpo.Dock", "FolderFlyout.cs"));
        Assert.Contains("SelectionAct(happened.Act, opened.Window, happened.Widget?.Key, opened);", RepoSource.Read("Karpo.Dock", "Dock.Folders.cs"));
    }

    /// <summary>
    /// Wherever the keys are (EditFollowHook): in edit mode, nothing following, Ctrl+Z is taken from the
    /// window with the keys and heard as an undo, Ctrl+Y and Ctrl+Shift+Z as a redo, each repeat again,
    /// their release kept as their press was; not while a widget follows the pointer, nor while one of the
    /// dock's menus stands open (a name being typed undoes its own), nor out of edit mode, nor without Ctrl.
    /// </summary>
    [Fact]
    public void TheHookHearsUndoAndRedoWhereverTheKeysAre()
    {
        var gesture = new FollowGesture { Editing = true };
        Assert.Equal((true, FollowInput.Undo), gesture.Key(WM.WM_KEYDOWN, 'Z', control: true));
        Assert.True(gesture.OwesKey);
        Assert.Equal((true, FollowInput.Undo), gesture.Key(WM.WM_KEYDOWN, 'Z', control: true));
        Assert.Equal((true, FollowInput.None), gesture.Key(WM.WM_KEYUP, 'Z'));
        Assert.False(gesture.OwesKey);
        Assert.Equal((true, FollowInput.Redo), gesture.Key(WM.WM_KEYDOWN, 'Y', control: true));
        Assert.Equal((true, FollowInput.None), gesture.Key(WM.WM_KEYUP, 'Y'));
        Assert.Equal((true, FollowInput.Redo), gesture.Key(WM.WM_KEYDOWN, 'Z', control: true, shift: true));
        Assert.Equal((true, FollowInput.None), gesture.Key(WM.WM_KEYUP, 'Z'));
        // Not without Ctrl, not with a menu open, not while a widget follows, not out of edit mode.
        Assert.Equal((false, FollowInput.None), gesture.Key(WM.WM_KEYDOWN, 'Z'));
        Assert.Equal((false, FollowInput.None), gesture.Key(WM.WM_KEYDOWN, 'Z', control: true, menu: () => true));
        Assert.Equal((false, FollowInput.None), gesture.Key(WM.WM_KEYUP, 'Z'));
        gesture.Following = true;
        Assert.Equal((false, FollowInput.None), gesture.Key(WM.WM_KEYDOWN, 'Z', control: true));
        gesture.Following = false;
        gesture.Editing = false;
        Assert.Equal((false, FollowInput.None), gesture.Key(WM.WM_KEYDOWN, 'Z', control: true));
        // Escape as ever.
        gesture.Editing = true;
        Assert.Equal((true, FollowInput.Leave), gesture.Key(WM.WM_KEYDOWN, VK.VK_ESCAPE));
        var follow = RepoSource.Read("Karpo.Dock", "Dock.EditFollow.cs");
        // §520: and the settings panel's search with the keys has them as a menu's field has.
        Assert.Contains("new EditFollowHook(instance, () => host, EscapeIsTheirs, () => FlyoutOpen || PanelTyping)", follow);
        Assert.Matches(@"case FollowInput\.Undo:\s+EditUndo\(\);", follow);
    }
}
