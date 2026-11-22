// The edit bar (§352, §520: folded, Settings ⌄, the errors, Add, Done; its settings panel folded out of it), and while a
// folder is open (2026-10-16, 2026-10-23, §297): a Taskbar along the top and one along the bottom in edit mode, without a
// folder and with the clock's folder open — the bar's place kept, the folder where it would stand there —
// (EditPlacePictures.EditedDisplay). A display 1280 DIPs wide at 150 %, dark and light. To KARPO_PNG_DIR as
// editbar-*-<dark|light>-150.png.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Text.RegularExpressions;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void EditBarShowsTheFoldersButtonsSheets()
    {
        WindowShots.OnHiddenDesktop("editbar-folder", (gfx, instance) =>
        {
            const uint dpi = 144;
            var config = new Config();
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                var theme = dark ? "dark" : "light";
                foreach (var edge in new[] { Edge.Top, Edge.Bottom })
                {
                    var layout = PictureLayout(LayoutType.Taskbar, edge);
                    var name = edge == Edge.Top ? "top" : "bottom";
                    EditedDisplay(gfx, instance, dpi, config, look, layout, $"editbar-{name}-edit-{theme}-150.png", editing: true);
                    EditedDisplay(gfx, instance, dpi, config, look, layout, $"editbar-{name}-folder-{theme}-150.png", editing: true, folder: true);
                }
                // §326: a display that shows None, its edit bar alone over the dim.
                EditedDisplay(gfx, instance, dpi, config, look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), $"editbar-none-{theme}-150.png", editing: true, none: true);
            }
        });
    }

    /// <summary>
    /// A folder opening and every way one closes (Forget) set the edit bars again, and the dock's
    /// edit bars follow — the folder's crumb on its display's, in front of its window, which stands under
    /// it; every other display's as it was (§390, ArrangedFolderStandsTests.AFolderOpenLeavesTheOtherScreensEditBar);
    /// Escape on the bar closes a folder before it leaves. §352: the selection changing sets them too
    /// (its widget's crumb), and each says what its display stands as (DeckState).
    /// </summary>
    [Fact]
    public void FoldersOpeningAndClosingSetTheEditBars()
    {
        var repo = RepoSource.Root();
        var folders = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.Folders.cs"));
        Assert.Equal(2, folders.Split("DecksFollowFolders();").Length - 1);
        // §359: where the folder stands is its stratum's, nothing the dock tells it.
        Assert.DoesNotContain("StandsUnder", folders);
        var decks = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.EditDeck.cs"));
        // §390: no edit bar goes aside for a folder: the model always says what it shows, the chrome shows it.
        Assert.Contains("var state = DeckState(display, folder);", decks);
        Assert.DoesNotContain("CanUndo", decks);
        Assert.Contains("StirChrome();", File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.EditSelect.cs")));
        Assert.Contains("folderOpen: openFolders.Count > 0", File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.EditFollow.cs")));
        Assert.Contains("EscapeTo(EscapeStep(window.CancelDrag() || DropFollow()));", File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.cs")));
    }

    /// <summary>
    /// §352, what the crumbs and the buttons do, done the dock's way: the folder's crumb the folder's own
    /// menu under it (WidgetMenu on its key, §335); the widget's crumb the selected widget's menu, a bar's
    /// or a line's over the folder (WidgetCrumbMenu); one "Add a widget", into the folder open arranged on
    /// its display (FolderAdd, its pick carried, §298) or onto the bar; Undo and Redo edit mode's history;
    /// "Done" the end of edit mode, the folder left open, at rest (§348, EndEdit's keepFolder;
    /// OneFolderTests.AFolderKeptAsEditModeEndsRestsWhereItStands). Layouts ▾, Taskbar ▾, Folders ▾, "Add to
    /// folder" and the red cross went, and their words with them.
    /// </summary>
    [Fact]
    public void TheCrumbsAndButtonsDoTheirThing()
    {
        var repo = RepoSource.Root();
        var decks = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.EditDeck.cs"));
        // §575: an entry opens its flyout, clicked again closes it; a page's control does what its row did; no menus, no Undo
        // nor Redo (Ctrl+Z and Ctrl+Y stay: EditFollowHook); no fold, no side list.
        Assert.Contains("OpenPanel(id, happened.Page);", decks);
        Assert.Contains("Report(() => ClosePanel());", decks);
        Assert.Contains("Report(() => PanelActed(id, happened.Page, happened.Id));", decks);
        Assert.DoesNotMatch(new Regex(@"\b(OpenCrumb|CrumbMenu|WidgetCrumbMenu|CrumbSettings|DeckLooks|ShowSettingsMenu|FoldPanel|Unfolded|FolderAdd|DeckAdd)\b"), decks);
        Assert.Matches(@"case EditDeckAction\.Done:\s+Report\(LeaveEdit\);", decks); // §487: leaving edit mode always keeps the folder
        Assert.DoesNotContain("EditDeckAction.Undo", decks);
        Assert.DoesNotContain("EditDeckAction.Redo", decks);
        Assert.DoesNotContain("CloseFolder:", decks);
        Assert.Contains("CloseFolder(FolderCloser.EditEnds);", File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.cs")));
        foreach (var gone in new[] { "dock.edit.closeFolder", "dock.edit.close", "dock.edit.addToFolder", "dock.edit.folders", "dock.edit.undo", "dock.edit.redo" })
        {
            Assert.Null(Strings.Lookup(gone, "en"));
        }
        Assert.DoesNotMatch(new Regex(@"\b(LayoutsPart|TaskbarPart|FoldersPart|AddToFolderPart|ClosePart|CloseRed)\b"), decks);
        // §575: "+ Add"'s list a flyout of its own: §593 into the folder open arranged on its display where it adds (§298),
        // else at the bar's last "+" (Dock.AddAt) — a place either way, no pick carried.
        var folders = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.Folders.cs"));
        Assert.Contains("open.Flyout.AddPlace() is { } place", folders);
        Assert.Contains("AtPlus(bar, bar.Layout.Adds()[^1])", folders);
        Assert.Contains("AddAt(displayId)", File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.SettingsPanel.cs")));
        Assert.DoesNotContain("Follows", File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "AddList.cs")));
    }

    /// <summary>
    /// §344 (the person, 2026-10-25: "add a widget should not close the folder"), §352: "Add a widget"
    /// with no folder open on its display adds to the bar, and nothing on the way closes a folder: not the add page's place
    /// (Dock.AddOf, §593 the place it was opened for), not the pick (AddChosen, §593 at the place at once), not the menu closing.
    /// The click that puts a copied widget down is the follow hook's, kept from whatever is under the pointer (FollowGesture),
    /// so a bar locked under a folder never hears the press that would close the folder (§212, §366: EditClicks.OnLocked) —
    /// nor its release.
    /// </summary>
    [Fact]
    public void AddAWidgetLeavesTheFolderOpen()
    {
        var repo = RepoSource.Root();
        // §575: the add list's flyout (Dock.AddOf) for its place; a pick its flyout closing and the add list's own (PanelActed,
        // AddChosen) — nothing on the way closes a folder.
        var panel = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.SettingsPanel.cs"));
        var from = panel.IndexOf("private (Pending What, List<FlyoutItem> Rows)? AddOf(", StringComparison.Ordinal);
        var to = panel.IndexOf("private string PlaceOf(", StringComparison.Ordinal);
        Assert.True(from >= 0 && to > from);
        var add = panel[from..to];
        Assert.Contains("adding is not { } open", add);
        Assert.DoesNotContain("CloseFolder", add);
        Assert.DoesNotContain("EscapeTo", add);
        var acted = panel[panel.IndexOf("private void PanelActed(", StringComparison.Ordinal)..panel.IndexOf("private void PanelMoved(", StringComparison.Ordinal)];
        Assert.Contains("ClosePanel();", acted);
        Assert.DoesNotContain("CloseFolder", acted);
        var folders = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.Folders.cs"));
        var chosen = folders[folders.IndexOf("private bool AddChosen(", StringComparison.Ordinal)..folders.IndexOf("private void EndAdding(", StringComparison.Ordinal)];
        Assert.DoesNotContain("CloseFolder", chosen);
        Assert.Contains("Apply(made.Config);", chosen);

        var landing = new FollowGesture { Following = true, Editing = true };
        Assert.Equal((true, FollowInput.Place), landing.Mouse(WM.WM_LBUTTONDOWN));
        landing.Following = false;
        Assert.Equal((true, FollowInput.None), landing.Mouse(WM.WM_LBUTTONUP));
    }

    /// <summary>
    /// §343: the toggle done at the root, the dock's way. The popups' hook leaves a press on an edit bar
    /// to the dock, as a bar's (IsBar → FlyoutHost.PressesHandledBy): the menu is still open as the press
    /// arrives, so the edit bar knows what opened it and no dismissal can race the press. The dock closes
    /// the popups as the hook would (DeckPressed), and a press that closed its button's menu goes no
    /// further. §430: what opened a menu is the one rule every opener shares (Pending.Opener, Openers,
    /// OpenersTests): a click on its button says so, and a menu hung from a button by anything else
    /// (a face's click, a pick, a second start) is that button's (DeckOpened).
    /// </summary>
    [Fact]
    public void TheToggleIsTheDocksOwnPress()
    {
        var repo = RepoSource.Root();
        var dock = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.cs"));
        // §575: the edit bar's flyout as the bar.
        Assert.Contains("private bool IsBar(HWND hwnd) => !busy && (windows.Exists(window => window.Hwnd == hwnd) || chromes.Values.Any(chrome => chrome.Deck.Hwnd == hwnd || chrome.Deck.FlyoutHwnd == hwnd));", dock);
        Assert.Contains("FlyoutHost.PressesHandledBy = dock.IsBar;", dock);
        var decks = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.EditDeck.cs"));
        // §522, §525: and where it was, so a menu the hook or the click's activation closed first is one it closed (Pressing).
        Assert.Contains("&& DeckPressed(window, pair.Deck.PartAt(window, lParam), LightDismiss.ButtonOf(msg), ScreenOf(window, lParam))", decks);
        Assert.Contains("var (press, _) = Pressing(deck, part, button, screen);", decks);
        Assert.Contains("return openers.ClosedItsOwn;", decks);
        Assert.Contains("FlyoutHost.WM_KARPO_HOST_DISMISS", decks);
        // §520: a dropdown's popup is its box's. §689: the errors no popup list of their indicator's any more but a page of
        // the flyout, opened by it as a page by its entry (no ShowFaults, no opener of its own to say).
        Assert.Contains("DeckOpened(displayId, deck.Activated);", File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.SettingsPanel.cs")));
        Assert.Contains("return new EditDeckEvent(EditDeckAction.Open, Page: PanelPage.Errors);", decks);
        Assert.DoesNotContain("ShowFaults", File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.Faults.cs")));
        Assert.DoesNotContain("EditDeckMenus", decks);
    }

    /// <summary>
    /// §522: the hook leaves a bar's or an edit bar's press to it only while the dock is not busy (IsBar); busy as it
    /// heard the press, the hook closed the popup itself, and the press, handled after, closed nothing — so the click on
    /// what opened a menu opened it again (MenuToggleTests' rest-right-start and rest-right-bar, red on a loaded machine:
    /// "clicked again, it is open still"). Every window's press asks what the hook closed for it, at its point on the
    /// screen: a bar's (PressedOn, which counts it as closed by the press), an edit bar's and a folder's alike. §525: the
    /// same answer counts a popup the click's activation closed first (the edit bar's open panel; PanelPopupToggleTests).
    /// </summary>
    [Fact]
    public void EveryPressKnowsWhatTheHookClosedForIt()
    {
        var repo = RepoSource.Root();
        var sessions = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.MenuSessions.cs"));
        Assert.Contains("private (PopupOpener Press, Pending? Closed) Pressing(HWND window, object? part, PressButton button, POINT screen)", sessions);
        var dock = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.cs"));
        Assert.Contains("private bool PressedOn(DockWindow window, Hit? hit, PressButton button, POINT screen)", dock);
        var pressed = dock[dock.IndexOf("private bool PressedOn(", StringComparison.Ordinal)..];
        pressed = pressed[..pressed.IndexOf("private bool IsBar(", StringComparison.Ordinal)];
        Assert.Contains("var (press, byHook) = Pressing(window.Hwnd, hit, button, screen);", pressed);
        Assert.Contains("Closed(byHook);", pressed);
        Assert.Equal(2, dock.Split(", ScreenOf(window.Hwnd, lParam)))").Length - 1);
        Assert.Contains("_ = Pressing(window, open.Flyout.OpenerAt(lParam), LightDismiss.ButtonOf(msg), ScreenOf(window, lParam));", File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.Folders.cs")));
    }
}
