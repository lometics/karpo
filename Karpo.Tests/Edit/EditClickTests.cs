// edit: a click on a widget while the bar is arranged selects it, a folder's face opens it (§527) (the person,
// 2026-10-12: "the application looses focus when the settings window is opened automatically, so
// keyboard shortcuts dont work anymore then"; Karpo.Dock\EditClicks.cs).
//
// The rules, each held here where it could fail:
//
//  - **A click, not a drag.** Inside the system's drag threshold a press and release is a click;
//    past it, a move — on the same side of the threshold as every other drag of Karpo's.
//  - **No settings window.** No block's click or Enter opens the settings app or has
//    an open one follow (§228: "the settings for the widgets in the main application should be
//    removed"): a click selects, and the settings panel follows (§520); a folder's face opens the
//    folder too, its face closing it again (§527).
//  - **The keys and Narrator.** Space and Enter are the click, once however long held; the menu key
//    and Shift+F10 the menu beside it; UI Automation's Invoke and Toggle the click.
//  - **A double-click is two clicks**: on a folder the first opens it and the second closes it (§527).
//
// A folder's half, on real folder windows: Folders\FolderEditClickTests.

using Karpo.Core;
using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed class EditClickTests
{
    /// <summary>The drag threshold on a default Windows (SM_CXDRAG, SM_CYDRAG).</summary>
    private const int Slop = 4;

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(Slop, 0, true)]
    [InlineData(0, -Slop, true)]
    [InlineData(-Slop, Slop, true)]
    [InlineData(Slop + 1, 0, false)]
    [InlineData(0, Slop + 1, false)]
    [InlineData(-(Slop + 1), 0, false)]
    [InlineData(40, 40, false)]
    public void APressThatStaysInsideTheDragThresholdIsAClick(int dx, int dy, bool click)
    {
        Assert.Equal(click, EditClicks.IsClick(dx, dy, Slop, Slop));
    }

    /// <summary>The same threshold, and the same side of it, as every other drag.</summary>
    [Fact]
    public void TheThresholdIsEveryOtherDragsOne()
    {
        for (var dx = -10; dx <= 10; dx++)
        {
            for (var dy = -10; dy <= 10; dy++)
            {
                Assert.Equal(!EditClicks.PastThreshold(dx, dy, Slop, 3), EditClicks.IsClick(dx, dy, Slop, 3));
            }
        }
    }

    /// <summary>
    /// The regression (the person, 2026-10-12: "the application looses focus when the settings window
    /// is opened automatically, so keyboard shortcuts dont work anymore then"): no click on any block
    /// brings the settings window forward, nor has it follow (§228: no widget has a page there). A click
    /// selects (§520), a folder's face opens its folder too (§527, the person, 2026-11-07: "it should toggle
    /// open the folder too"); a click whose press closed its menu does nothing more, on a folder too.
    /// </summary>
    [Fact]
    public void AClickSelectsAFolderOpensAndNothingComesForward()
    {
        foreach (var kind in Enum.GetValues<BarKind>())
        {
            Assert.Equal(kind.IsFolder() ? EditClickAct.Open : EditClickAct.Select, EditClicks.OnClick(kind, closedItsMenu: false));
            Assert.Equal(EditClickAct.None, EditClicks.OnClick(kind, closedItsMenu: true));
        }
    }

    /// <summary>
    /// §520 decision 3: a click selects and nothing pops up, the settings panel following the selection. §527, the person,
    /// 2026-11-07: "when i click on a folder in edit mode it still does not open it, it only opens the settings, but it
    /// should toggle open the folder too"; earlier: "a folder should be opened again by clicking on it in edit mode, remove
    /// the separate open folder action". A click done the way the dock does it: the one path of the pointer's release, Space,
    /// Enter and Narrator's Invoke (Dock.EditActivate) selects, and on a folder's face opens the folder arranged
    /// (Dock.OpenArranged) — no menu, no "Open", no double-click of its own.
    /// </summary>
    [Fact]
    public void AClickOnAFolderSelectsItAndOpensIt()
    {
        var repo = RepoSource.Root();
        var dock = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.cs")).ReplaceLineEndings("\n");
        var activate = dock[dock.IndexOf("private void EditActivate(", StringComparison.Ordinal)..];
        activate = activate[..activate.IndexOf("private void BesideMenu(", StringComparison.Ordinal)];
        Assert.Contains("EditClicks.OnClick(layout.Specs[part].Kind, closedItsMenu)", activate);
        Assert.Contains("EditPick(key);\n        if (act == EditClickAct.Open)\n        {\n            OpenArranged(window, key);\n        }", activate);
        Assert.DoesNotContain("BesideMenu", activate);
        Assert.DoesNotContain("Crumb", activate);
        // The release is the one call; the keys too.
        Assert.Contains("dock.EditActivate(this, part, OnScreen(new Vector2(x, y)), closedItsMenu: key == optionsClosed);", dock);
        Assert.Contains("EditActivate(window, chosen, null);", dock);
        // The separate ways in are gone: the face's "Open", the double-click's second, and what they went through.
        foreach (var gone in new[] { "CMD_OPEN_FOLDER", "OpenFolderEditing", "ClickedAgain", "lastClick" })
        {
            Assert.DoesNotContain(gone, dock);
        }
        // A folder's line: the click and Space select it (LineSelected); the right click and the menu key its menu beside it.
        var folders = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.Folders.cs"));
        var line = folders[folders.IndexOf("private void LineClicked(", StringComparison.Ordinal)..];
        line = line[..line.IndexOf("private void FolderChoices(", StringComparison.Ordinal)];
        Assert.DoesNotContain("LineSettings", folders);
        Assert.DoesNotContain("LineSettings", File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.Faults.cs")));
        Assert.Contains("LineSelected(opened, () => EditPick(key));", line);
        Assert.Contains("LineSelected(opened, () => EditPick(spaced.Key));", folders);
        Assert.Contains("LineMenu(opened, line, card, ", line);
        Assert.DoesNotContain("EditMenuRows", dock);
        Assert.Contains("if (hovering && EditClicks.OnLocked(layout.HitTest(x, y, true) is { } on ? layout.PartOf(on) : -1, closing))", File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.EditDeck.cs")));
    }

    /// <summary>
    /// The person, 2026-10-19: "the separate right click menus in the edit mode should be removed"
    /// (§214). A right click on a widget opens the one menu beside it — a folder's too, whose left
    /// click opens it instead (§253) — selecting it alone where it was not selected, keeping several
    /// selected where it is one of them; on the widget whose menu its press closed it only closes it,
    /// as a click there; on the room round the widgets the bar's own looks (§290: "when i right click
    /// in edit mode onto the taskbar all the appearance settings should appear in the context menu").
    /// (Out of edit mode a right click is the widget's or the bar's own menu, as before: BarMenuTests.)
    /// </summary>
    [Fact]
    public void ARightClickOpensTheMenuAndTheRoomItsLooks()
    {
        Assert.Equal(EditRight.Widget, EditClicks.OnRight(onWidget: true, closedItsMenu: false));
        Assert.Equal(EditRight.None, EditClicks.OnRight(onWidget: true, closedItsMenu: true));
        Assert.Equal(EditRight.Bar, EditClicks.OnRight(onWidget: false, closedItsMenu: false));

        // The bar's right click opens the menu itself, never the left click's act, which opens a folder; §520 the bar's own
        // room the settings panel on its Taskbar page.
        var dock = File.ReadAllText(Path.Combine(RepoSource.Root(), "Karpo.Dock", "Dock.cs"));
        var right = dock[dock.IndexOf("private void EditRightClick(", StringComparison.Ordinal)..];
        right = right[..right.IndexOf("private string? rightClosed", StringComparison.Ordinal)];
        Assert.Contains("BesideMenu(window, part);", right);
        Assert.Contains("OpenPanel(window.Id, PanelPage.Taskbar);", right);
        Assert.DoesNotContain("EditActivate", right);
    }

    /// <summary>
    /// The person, 2026-10-22: "in edit mode when i have the folder open and i right click on the
    /// folder or any other folder it should close the folder and open the context menu of the folder"
    /// (§287). A bar locked under a folder open arranged (§212) still takes a right click on a widget —
    /// the open folder's face, another folder's, any widget's: the one path a right click takes on a
    /// bar that is not locked, the folder closed first by Escape's step, then the widget's menu at
    /// once, no second right click; on the room round the widgets the bar's looks the same way (§290).
    /// </summary>
    [Fact]
    public void ARightClickOnALockedBarClosesTheFolderAndOpensTheMenu()
    {
        var dock = File.ReadAllText(Path.Combine(RepoSource.Root(), "Karpo.Dock", "Dock.cs")).ReplaceLineEndings("\n");
        var right = dock[dock.IndexOf("private void EditRightClick(", StringComparison.Ordinal)..];
        right = right[..right.IndexOf("private string? rightClosed", StringComparison.Ordinal)];
        var asked = right.IndexOf("EditClicks.OnRight(", StringComparison.Ordinal);
        var closes = right.IndexOf("if (BarLocked)\n        {\n            EscapeTo(EditEscape.CloseFolder);\n        }", StringComparison.Ordinal);
        var menu = right.IndexOf("BesideMenu(window, part);", StringComparison.Ordinal);
        var looks = right.IndexOf("OpenPanel(window.Id, PanelPage.Taskbar);", StringComparison.Ordinal);
        // The folder gone before either menu comes; nothing returns early for the lock.
        Assert.True(asked >= 0 && closes > asked && menu > closes && looks > closes, right);
        Assert.Single(right.Split("BarLocked").Skip(1));
    }

    /// <summary>
    /// The person, 2026-10-22: "its not possible to click another folder while one folder is open in
    /// edit mode" (§289). A left press on a bar locked under a folder open arranged (§212) keeps the
    /// rule a right click keeps (§287): on the open folder's face it closes the folder; on any other
    /// widget — another folder, any widget, one of another bar while the folder is this one's — the
    /// folder closes by Escape's step and the press goes on as on a bar not locked, so its click
    /// opens the other folder (§527), or selects the widget, in the one click; §366 ("when clicking away on the
    /// taskbar it should close the current folder if one is open"): on the room round the widgets and the
    /// resize grip the folder closes too, and that is all. Done the way the dock does it, five clicks.
    /// </summary>
    [Fact]
    public void ALeftClickOnALockedBarClosesTheFolderAndClicksAsUnlocked()
    {
        Assert.True(EditClicks.OnLocked(part: 3, closing: 3));
        Assert.False(EditClicks.OnLocked(part: 2, closing: 3));
        Assert.False(EditClicks.OnLocked(part: 0, closing: -1));
        Assert.False(EditClicks.OnLocked(part: -1, closing: 3));

        // Parts 0 and 1 folders, 2 a widget; the press on a locked bar, then the click as unlocked (§527: a folder's opens it).
        BarKind[] kinds = [BarKind.FolderList, BarKind.FolderList, BarKind.NightLight];
        int? open = null;
        // -1: the room round the widgets, which selects nothing.
        void Click(int part)
        {
            if (open is { } shown)
            {
                open = null;
                if (EditClicks.OnLocked(part, shown))
                {
                    return;
                }
            }
            if (part >= 0 && EditClicks.OnClick(kinds[part], closedItsMenu: false) == EditClickAct.Open)
            {
                open = part;
            }
        }
        // A folder's face opens it, and closes it again.
        Click(1);
        Assert.Equal(1, open);
        Click(1);
        Assert.Null(open);
        // Another folder's face opens that one in the one click.
        Click(0);
        Assert.Equal(0, open);
        Click(1);
        Assert.Equal(1, open);
        // Another widget, the room round them: the folder closes, nothing opens.
        Click(2);
        Assert.Null(open);
        Click(1);
        Click(-1);
        Assert.Null(open);

        // The dock: the one switch on the lock, the folder closed before the bar's own press, which
        // picks the block up and whose release is the click (EditActivate); a widget lights under the
        // pointer on a locked bar too, its click reaching it.
        var dock = File.ReadAllText(Path.Combine(RepoSource.Root(), "Karpo.Dock", "Dock.cs")).ReplaceLineEndings("\n");
        var pressed = dock[dock.IndexOf("private void EditPressed(", StringComparison.Ordinal)..];
        pressed = pressed[..pressed.IndexOf("public bool AdoptDrag(", StringComparison.Ordinal)];
        var asked = pressed.IndexOf("EditClicks.OnLocked(part, dock.ClosingPart(this))", StringComparison.Ordinal);
        var closes = pressed.IndexOf("dock.EscapeTo(EditEscape.CloseFolder);", StringComparison.Ordinal);
        var own = pressed.IndexOf("dock.FocusBar(this);", StringComparison.Ordinal);
        Assert.True(asked >= 0 && closes > asked && own > closes, pressed);
        Assert.Single(pressed.Split("BarLocked").Skip(1));
        var view = dock[dock.IndexOf("private EditView EditView()", StringComparison.Ordinal)..];
        Assert.DoesNotContain("ClosingPart", view[..view.IndexOf("return view;", StringComparison.Ordinal)]);
    }

    /// <summary>
    /// The person, 2026-10-19: "the whole folder face should become the close button" (§251), and
    /// 2026-10-20: "the folder widget that is open should also not be the close button anymore, at
    /// least not visually. there should be a red close button at the top" (§267). A press on the face
    /// of the folder open arranged over a locked bar (§212) closes it, as a second click on a folder at
    /// rest does (another widget's: <see cref="ALeftClickOnALockedBarClosesTheFolderAndClicksAsUnlocked"/>).
    /// Escape still closes the folder first, and the keys of edit mode stay locked but Escape and
    /// Ctrl+V; the press and the release are the dock's own, wired where a locked bar's press went
    /// before. The folder has no close button of its own (§287: "remove the close button in folder edit
    /// mode"), and the bar is drawn as it is meanwhile: no dim over it, no face made a button (Scene).
    /// </summary>
    [Fact]
    public void APressOnTheOpenFoldersFaceClosesIt()
    {
        Assert.True(EditClicks.OnLocked(part: 3, closing: 3));
        Assert.False(EditClicks.OnLocked(part: 2, closing: 3));
        Assert.False(EditClicks.OnLocked(part: -1, closing: 3));
        Assert.False(EditClicks.OnLocked(part: 0, closing: -1));
        Assert.False(EditClicks.OnLocked(part: -1, closing: -1));
        Assert.Equal(EditEscape.CloseFolder, EditKeys.Escape(carrying: false, copied: false, folderOpen: true));

        var repo = RepoSource.Root();
        var dock = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.cs"));
        Assert.Contains("LockedPressed();", dock);
        Assert.Contains("if (CloseReleased(x, y))", dock);
        Assert.Contains("BarLocked && stroke.Act is not (EditKeyAct.Escape or EditKeyAct.Paste or EditKeyAct.Undo or EditKeyAct.Redo)", dock);
        var decks = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.EditDeck.cs"));
        Assert.Contains("dock.EscapeTo(EditEscape.CloseFolder);", decks);
        // No close button (§287): nothing of the folder's asks to close it.
        Assert.DoesNotContain("CloseAsked", Enum.GetNames<FolderFlyoutAction>());
        Assert.DoesNotContain("CloseAsked", File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.Folders.cs")));
        // Nothing dims the bar, nor makes its face a button (§267).
        var scene = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Scene.cs"));
        Assert.DoesNotContain("Scrim", scene);
        Assert.DoesNotContain("CloseFace", scene);
    }

    /// <summary>
    /// Enter and Space are the click: the block selected, a folder opened (§527) — no page of the settings app any more
    /// (§228). The menu key the right click's menu beside it.
    /// </summary>
    [Fact]
    public void EnterOpensTheMenu()
    {
        Assert.Equal(EditKeyPress.Open, EditClicks.OnKey(VK.VK_RETURN, shift: false, repeat: false));
        Assert.Equal(EditKeyPress.Click, EditClicks.OnKey(VK.VK_SPACE, shift: false, repeat: false));
        var dock = File.ReadAllText(Path.Combine(RepoSource.Root(), "Karpo.Dock", "Dock.cs")).ReplaceLineEndings("\n");
        Assert.Contains("if (stroke.Act == EditKeyAct.Menu)\n                    {\n                        BesideMenu(window, chosen);\n                    }\n                    else\n                    {\n                        EditActivate(window, chosen, null);\n                    }", dock);
    }

    /// <summary>
    /// The person, 2026-10-22: "double clicking a widget should not open its context menu, it should just
    /// behave like two normal clicks behind each other"). Two clicks on a widget, however quick, done the
    /// way the dock does them (EditClicks.OnClick): each selects it. §527: on a folder the first opens it and the
    /// second, on the face of the folder open, closes it (EditClicks.OnLocked) — however quick (§481 to §526 a
    /// double-click's second opened it). A click whose press closed the widget's right click's menu does nothing
    /// more. No guard of its own (EditClickGuard and OnDoubleClick are gone).
    /// </summary>
    [Fact]
    public void ADoubleClickIsTwoClicks()
    {
        Assert.Equal(EditClickAct.Select, EditClicks.OnClick(BarKind.NightLight, closedItsMenu: false));
        Assert.Equal(EditClickAct.Select, EditClicks.OnClick(BarKind.NightLight, closedItsMenu: false));

        var open = false;
        void FolderClick(bool closedItsMenu = false)
        {
            if (open)
            {
                // The face of the folder open, whose press closes it (§252, §267).
                open = !EditClicks.OnLocked(part: 2, closing: 2);
                return;
            }
            open = EditClicks.OnClick(BarKind.FolderList, closedItsMenu) == EditClickAct.Open;
        }
        // A double-click: open, then closed.
        FolderClick();
        Assert.True(open);
        FolderClick();
        Assert.False(open);
        // A third: open again.
        FolderClick();
        Assert.True(open);
        FolderClick();
        // A click whose press closed the face's right click's menu: that alone.
        FolderClick(closedItsMenu: true);
        Assert.False(open);

        // The dock: no guard asked anywhere, on the bar, a folder's face or a folder's line.
        var repo = RepoSource.Root();
        foreach (var file in new[] { "Dock.cs", "Dock.EditDeck.cs", "Dock.Folders.cs", "EditClicks.cs" })
        {
            var source = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", file));
            Assert.DoesNotContain("ClickActs", source);
            Assert.DoesNotContain("MenuClosedClick", source);
            Assert.DoesNotContain("OnDoubleClick", source);
            Assert.DoesNotContain("class EditClickGuard", source);
        }
    }

    /// <summary>
    /// Space on a folder with edit mode's focus, and Narrator's primary action on it, are the click
    /// (Dock.EditKey and Dock.ActFor both go to Dock.EditActivate): the folder opens (§527), and Space held down does it
    /// once.
    /// </summary>
    [Fact]
    public void TheKeysAndNarratorClickAFolder()
    {
        Assert.Equal(EditKeyPress.Click, EditClicks.OnKey(VK.VK_SPACE, shift: false, repeat: false));
        Assert.Equal(EditKeyPress.Nothing, EditClicks.OnKey(VK.VK_SPACE, shift: false, repeat: true));
        // A folder on the bar is an island's hit, and Invoke (Narrator's primary action) or Toggle
        // on it is the click.
        Assert.True(EditClicks.ByAutomation(UiaAct.Invoke, HitKind.Island));
        Assert.True(EditClicks.ByAutomation(UiaAct.Toggle, HitKind.Island));

        // The click the keys and Narrator make carries no point, so no guard: each press is one.
        var done = new List<string>();
        foreach (var (key, repeat) in new[] { (VK.VK_SPACE, false), (VK.VK_SPACE, true), (VK.VK_SPACE, true) })
        {
            // The keys carry no point: no double-click of theirs, nor a menu closed by a press.
            if (EditClicks.OnKey(key, shift: false, repeat) == EditKeyPress.Click)
            {
                done.Add(EditClicks.OnClick(BarKind.FolderList, closedItsMenu: false).ToString());
            }
        }
        Assert.Equal(["Open"], done);
    }

    /// <summary>Space is a click on the block and Enter a folder opened or its menu, once however long held; the menu key and Shift+F10 its menu.</summary>
    [Fact]
    public void TheKeysClickOrOpenTheMenu()
    {
        Assert.Equal(EditKeyPress.Open, EditClicks.OnKey(VK.VK_RETURN, shift: false, repeat: false));
        Assert.Equal(EditKeyPress.Click, EditClicks.OnKey(VK.VK_SPACE, shift: false, repeat: false));
        Assert.Equal(EditKeyPress.Open, EditClicks.OnKey(VK.VK_RETURN, shift: true, repeat: false));
        // Held down: the first press opened it; the repeats open nothing more.
        Assert.Equal(EditKeyPress.Nothing, EditClicks.OnKey(VK.VK_RETURN, shift: false, repeat: true));
        Assert.Equal(EditKeyPress.Nothing, EditClicks.OnKey(VK.VK_SPACE, shift: false, repeat: true));
        Assert.Equal(EditKeyPress.Menu, EditClicks.OnKey(VK.VK_APPS, shift: false, repeat: false));
        Assert.Equal(EditKeyPress.Menu, EditClicks.OnKey(VK.VK_F10, shift: true, repeat: false));
        Assert.Equal(EditKeyPress.Nothing, EditClicks.OnKey(VK.VK_F10, shift: false, repeat: false));
        Assert.Equal(EditKeyPress.Nothing, EditClicks.OnKey(VK.VK_TAB, shift: false, repeat: false));
    }

    /// <summary>Narrator's primary action on a widget is the click on its block; a "+" stays its own button.</summary>
    [Fact]
    public void NarratorPressesABlockAsAClickDoes()
    {
        foreach (var kind in new[] { HitKind.Island, HitKind.Tile, HitKind.Start, HitKind.Search, HitKind.Tray, HitKind.TrayOverflow, HitKind.ShowDesktop })
        {
            Assert.True(EditClicks.ByAutomation(UiaAct.Invoke, kind));
            // A switch's Toggle switches nothing while the bar is arranged: it is the click too.
            Assert.True(EditClicks.ByAutomation(UiaAct.Toggle, kind));
            Assert.False(EditClicks.ByAutomation(UiaAct.Focus, kind));
            Assert.False(EditClicks.ByAutomation(UiaAct.Menu, kind));
            Assert.False(EditClicks.ByAutomation(UiaAct.SetValue, kind));
        }
    }
}
