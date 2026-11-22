// edit: what edit mode puts on every display besides its bars and places (the person, 2026-10-08:
// "edit mode doesn't shrink the screen any more"):
//
//  - **The dim.** A window over the whole display (EditShade), 50 % black, as Windows dims behind
//    what wants the attention. The layout's bars are edited where they stand, over it — every bar
//    shown all the time, and every bar that comes in on hover brought in — on every display at once.
//    The bars and places stand over it, the edit bar and the sections' edit bars over them, by their
//    strata (ZPlan, §359), all of it one model a display (EditChrome). A click on it leaves edit
//    mode on (the person, 2026-10-07: "remove that you go out of edit mode if you click into the
//    middle"): it only clears the selection, and a drag on it selects the widgets it runs over
//    (Dock.EditSelect.cs).
//  - **The edit bar**, one on every display, §575 a command bar of the settings' pages (the person, 2026-11-08: "all the
//    individual menus that we have right now are basically in the edit bar and then they open a flyout individually with
//    their settings in it"), its buttons no fill and no rim, as a title bar's, Done the one accent button:
//
//      [⚙ Karpo] [👤 Accounts] [🖥 Display] [▤ Layouts] [▭ Taskbar] [🎨 Appearance]  [📁 Folder] [🕑 Clock]         [⊗] [+ Add] [✓ Done]
//
//    §578: one width whatever is selected — §596 the room for the selection's entries (a folder's and a widget's, each an
//    entry as the pages' are, as wide as its words; §584 none) and the errors' room (their icon button) always kept —; no
//    search.
//
//    §630 (the person, 2026-11-09: "the topbar should not be movable anymore"): no grip; it stands at its own spot, below
//    (§395 to §629 dragged by a grip ⠿ at its start anywhere in the work area). §394: the errors' indicator while there are
//    any, their list under it (Dock.Faults.cs). Each page's entry opens its flyout, hung from it, the page the rows
//    the edit bar's drop-down menus showed until §520, read as controls (SettingsPanel.cs), laid out
//    (SettingsPanel.Layout.cs) and drawn (SettingsPanel.Paint.cs); what a control does the dock's (Dock.SettingsPanel.cs).
//    One flyout open at a time, on one display; the entry open selected; clicked again, Escape or a click elsewhere closes
//    it. The selection's entry (a widget's, a folder's) comes with it and opens its flyout. "Add" opens the add list in a
//    flyout of the same look (§575), §593 hung from the whole bar, as wide, whatever opened it (Add, a bar's "+", a folder's),
//    adding at the bar's last "+" or with a folder open into the folder where it adds (§298, Dock.AddAt), its pick under the
//    pointer a ghost there (Dock.AddGhost.cs); greyed on a display showing None. "Done" ends edit mode (§348: a folder open
//    stays open, at rest).
//    No Undo nor Redo buttons (§352 to §519): Ctrl+Z and Ctrl+Y (EditFollowHook). A screen reader finds the bar a tool bar,
//    its entries buttons that expand and collapse, and a flyout a pane of groups (DeckUia).
//  - **Where it stands** (EditDeckPlan.Place, EditDeckRoom.Side): centred on the display, on the side
//    across from its bars (the person, 2026-10-23: "when the taskbar is shown on the top the edit bar
//    should be shown on the bottom"; §296). Something along the top (a Taskbar's or a Dock's bar on the
//    top edge, a place or surface of edit mode there) and nothing along the bottom: at the bottom, 8
//    DIPs above the display's bottom. Otherwise at the top — a bar at the bottom, at the sides, none at
//    all — 8 DIPs below the display's top, or below the deepest of what stands there, so it never
//    covers an area where a widget can be placed. Inside the display, 8 DIPs from its sides. It hangs
//    from that inner side as a flyout does, so it never covers a bar, a place or a widget in them; the
//    sections' edit bars keep clear of it. What it opens hangs from the same side, §575 its flyouts from their
//    entries: down from a bar at the top, up from one at the bottom (PopupPlace).
//  - **While a folder is open** (§297, §329): the edit bar of the folder's display stays where it stands,
//    in front of the folder's window, which takes the full height under it, a column's buttons it would
//    cover standing just past it (FolderFlyout.Covered). The other displays' edit bars stay as they
//    stand (§390). The bars stay as they are, locked but not dimmed (§212, §267), and in front of the
//    folder's window (§279). The folder closes as before: Escape, a click on its face on the bar
//    (DockWindow.EditPressed), a click or a right click on another widget of a bar (§287, §289).
//
// The edit bar is a Tool host (FlyoutHost, EditDeck), as the sections' edit bars are, above the dim; those keep clear of
// it; §575 its flyout another, a popup's stratum over it. Where it stands is EditDeckPlan, pure and tested; what it shows
// PanelLayout, pure and tested too. Each content is one surface drawn whole whenever what it shows changes, shown in the
// commit that brings it (§520; §363 to §519 its buttons kept one by one, KeptButtons, which the sections' edit bars still are).

using System.Numerics;
using System.Runtime.InteropServices;
using Karpo.Core;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using Edge = Karpo.Core.Edge;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>
    /// edit: every display's edit chrome — its dim, its edit bar, its sections' edit bars, the selection
    /// rectangle — by the display's id; made with the first edit mode (§359, EditChrome).
    /// </summary>
    private readonly Dictionary<string, EditChrome> chromes = [];

    /// <summary>edit: something the chrome shows changed: it is reconciled as the frame ends (<see cref="PresentChrome"/>).</summary>
    private bool chromeStale;

    /// <summary>edit: whether the dims stand, which do not disturb hears (DoNotDisturb.Dimmed, <see cref="TellDims"/>).</summary>
    private bool dimmed;

    /// <summary>§623: what do not disturb was last told: whether a layer over a whole display stands, edit mode's dim or a folder's backdrop.</summary>
    private bool dimsTold;

    /// <summary>edit: the dim a left press went down on, which its release clears the selection by.</summary>
    private HWND shadePressed;

    /// <summary>
    /// edit: what edit mode shows besides the bars changed — edit mode begun or ended, a build of a bar
    /// (its sections moved), a folder opened or closed, the selection, the band: the chrome follows as the
    /// frame ends, once, from what the frame left (<see cref="PresentChrome"/>), never from bars half built.
    /// </summary>
    private void StirChrome() => chromeStale = true;

    /// <summary>§696: how long a change or a redraw of the chrome may take before karpo.log says where the time went, ms.</summary>
    private const double SlowMs = 50;

    /// <summary>
    /// edit: every display's edit chrome reconciled to its model (§359, <see cref="ChromeModels"/>) where
    /// anything stirred it: every display attached, Karpo shown on it or not (§326), has its dim, its edit
    /// bar and its sections' edit bars in edit mode, none out of it. Run as each of the dock's frames ends,
    /// and at once where what follows needs the chrome where it stands (edit mode begun, an arrange done).
    /// </summary>
    private void PresentChrome()
    {
        if (disposed)
        {
            return;
        }
        if (chromeStale)
        {
            chromeStale = false;
            ReconcileChrome();
        }
        // §612: the dress changed (the Appearance's flyout opened or closed, by whatever closed it): the bars and the folders
        // open laid out in it, in this frame, so they change in the commit the dim fades in or out in; the chrome follows the
        // sections where they stand now. Edit mode's start and end lay them out themselves (BeginEdit, EndEdit).
        if (editor is not null && Dress != laidDress)
        {
            Rebuild();
            if (chromeStale)
            {
                chromeStale = false;
                ReconcileChrome();
            }
        }
        // §623: the folder's backdrop as the frame leaves the folders, edit mode and the Appearance.
        PresentBackdrop();
        TellDims();
        MapEditForStage();
    }

    /// <summary>
    /// A layer over a whole display — edit mode's dim, §623 a folder's backdrop — is an app in full screen to Windows, whose
    /// rule turns do not disturb on for it: not the person's, so not shown while one stands (DoNotDisturb.Dimmed). Told once a
    /// change, whichever layer it was.
    /// </summary>
    private void TellDims()
    {
        var now = dimmed || backdropShown;
        if (now != dimsTold)
        {
            dimsTold = now;
            QuickSettings.Services.DoNotDisturb.Shared.Dimmed(now);
        }
    }

    /// <summary>edit: <see cref="PresentChrome"/>'s reconciling, where something stirred the chrome.</summary>
    private void ReconcileChrome()
    {
        try
        {
            if (editor is null)
            {
                shadePressed = HWND.NULL;
                foreach (var chrome in chromes.Values)
                {
                    chrome.Update(null);
                }
                FlyoutHost.Avoided = [];
                dimmed = false;
                return;
            }
            var displays = EditedDisplays;
            foreach (var display in displays)
            {
                if (!chromes.ContainsKey(display.Id))
                {
                    var made = new EditChrome(gfx, instance, GCHandle.ToIntPtr(cell));
                    var id = display.Id;
                    // narrator, §517: a button pressed by a screen reader does what its click does.
                    made.Deck.Invoked = happened => DeckAct(id, made.Deck, happened);
                    // §520: every display's bar remembers in the one memory (the expanders open, §575 the flyout open).
                    made.Deck.Memory = panelMemory;
                    made.Deck.Changed = MapEditForStage;
                    // §593: the add page's pick under the pointer, or the keys, stands where it would go as a ghost.
                    made.Deck.Proposing = pick => Report(() => GhostOf(id, pick));
                    // §613: the Appearance's preview over the wallpaper of the display its flyout stands on (§547's reading).
                    made.Deck.Wallpaper = () => EditedDisplays.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.OrdinalIgnoreCase)) is { } on ? WallpaperOn(on.Bounds) : null;
                    chromes[display.Id] = made;
                }
            }
            foreach (var (id, chrome) in chromes.ToList())
            {
                if (!displays.Exists(display => display.Id == id))
                {
                    chromes.Remove(id);
                    chrome.Dispose();
                }
            }
            // §696: how long the edit chrome takes to make (its pages) and to show (laid out, painted, its previews), where slow.
            var timing = System.Diagnostics.Stopwatch.StartNew();
            var models = ChromeModels(displays);
            var modelled = timing.Elapsed.TotalMilliseconds;
            foreach (var (id, model) in models)
            {
                chromes[id].Update(model);
            }
            var shown = timing.Elapsed.TotalMilliseconds - modelled;
            if (modelled + shown > SlowMs)
            {
                Troubles.Write($"Slow: the edit chrome {modelled + shown:0} ms — its pages made {modelled:0} ms, shown {shown:0} ms"); // not-ui: karpo.log
            }
            // §576: the add page gone, however it went (closed, another page or the selection's opened), the place its "+"
            // marked with it.
            if (adding is not null && panelMemory.Page != PanelPage.Add)
            {
                EndAdding();
            }
            foreach (var chrome in chromes.Values)
            {
                chrome.Prune(surface => surface is DockWindow window && !windows.Contains(window));
            }
            // Over the folder, which takes the full height under it (§329): what of the folder's buttons
            // it would cover stands just past it.
            if (ArrangedFolder() is { } open && chromes.TryGetValue(open.Window.Id, out var over))
            {
                open.Flyout.Covered = over.Deck.Panel ?? default;
            }
            // Popups of a bar along the top open under it, where the edit bar stands: they keep clear of it.
            // Not a folder, which it stands over (FlyoutHost.UnderEditBar, §329).
            FlyoutHost.Avoided = [.. DeckPanels()];
            // A dim over the whole display is an app in full screen to Windows, whose rule turns do
            // not disturb on for it: not the person's, so not shown (DoNotDisturb.Dimmed, TellDims).
            dimmed = true;
        }
        catch (Exception error)
        {
            Report(error);
        }
    }

    /// <summary>
    /// edit: what edit mode shows of each of <paramref name="displays"/> now (§359): dimmed; its edit bar,
    /// centred at its top or its bottom (see the top of the file), saying what the display stands as
    /// (<see cref="DeckState"/>), §575 an entry for each of its pages as the config has them now (<see cref="PanelOf"/>), the
    /// flyout of the one open on the display it is open on (<see cref="panelOn"/>), following the selection; beside every
    /// section with widgets it cannot show its edit bar, clear of the others, of the surfaces' bars and of the displays' edit
    /// bars, none while a folder is open arranged (the bars locked under it, §212); the selection rectangle on the display a
    /// drag over its dim is on.
    /// </summary>
    private List<(string Id, EditChromeModel Model)> ChromeModels(List<MonitorInfo> displays)
    {
        var open = ArrangedFolder();
        var picked = SelectedAlone(open);
        var several = SelectedHere(open);
        var decks = new Dictionary<string, EditDeckModel>(StringComparer.OrdinalIgnoreCase);
        var taken = new List<RECT>();
        if (panelOn is { } on && !displays.Exists(display => string.Equals(display.Id, on, StringComparison.OrdinalIgnoreCase)))
        {
            panelOn = null;
            panelMemory.Show(null);
        }
        // §575: a widget selected, or a folder opened, opens its entry's flyout on its display; as it goes, the flyout closes.
        var (widget, followed, where) = Followed(open, picked, several);
        if (panelMemory.Follow(widget, followed) is not null && where is not null)
        {
            panelOn = where;
        }
        foreach (var display in displays)
        {
            var folder = open?.Window.Id == display.Id ? open : null;
            var state = DeckState(display, folder);
            // §526: in Windows' own look, never the layout's the display shows (SettingsLook).
            var (deckLook, scale, room) = (SettingsLook, display.Dpi / 96f, DeckRoom(display));
            var inUse = string.Equals(panelOn, display.Id, StringComparison.OrdinalIgnoreCase);
            var content = PanelOf(display, folder, picked, several, where, full: inUse);
            var shown = inUse ? panelMemory.Page : null;
            if (shown is { } page && content.Of(page) is null)
            {
                panelMemory.Show(null);
                shown = null;
            }
            // §593: the add page into the folder open here keeps clear of it where it can, as it stood when the page opened —
            // not as its ghost grows it, which would shorten the page under the pointer.
            var clear = shown == PanelPage.Add && adding?.Folder is { } into && into == folder ? addingClear : null;
            decks[display.Id] = new EditDeckModel(deckLook, scale, room, state, content, shown, clear);
            // Where the bar stands.
            taken.Add(EditDeck.PanelFor(gfx, deckLook, scale, room, state, content));
        }
        // §612: the Appearance's preview shows the display as out of edit mode: the dim clear, no section's edit bar.
        var dressed = Dress.Worn();
        var rails = dressed ? RailModels(taken) : [];
        var accent = FlyoutTheme.For(SettingsLook).Accent;
        return [.. displays.Select(display => (display.Id, new EditChromeModel(
            display.Bounds,
            decks[display.Id],
            [.. rails.Where(rail => rail.Surface is DockWindow window && window.Id == display.Id)],
            bandArea is { } band && string.Equals(band.DisplayId, display.Id, StringComparison.OrdinalIgnoreCase)
                ? new EditBandModel(band.Area, accent, BarOf(display.Id)?.UiScale ?? 1f)
                : null,
            dressed)))];
    }

    /// <summary>
    /// edit: what display <paramref name="display"/>'s edit bar is of (§352): the display by its name; the layout it shows
    /// (none on one that shows None); <paramref name="folder"/>, open arranged on it; §394 how many errors Karpo knows of.
    /// </summary>
    private EditDeckState DeckState(MonitorInfo display, OpenFolder? folder) =>
        new(new EditDeckSubject(OptionGlyphs.Monitor, display.Name), LayoutSubject(display.Id), folder is not null ? FolderSubject : null, faults.Count);

    /// <summary>
    /// edit: every display attached, once each, Karpo shown on it or not (§326): each has its dim and
    /// its edit bar in edit mode.
    /// </summary>
    private List<MonitorInfo> EditedDisplays => [.. arranged.DistinctBy(display => display.Id, StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// edit: what the layout crumb of display <paramref name="displayId"/>'s edit bar says (§326, §352): the
    /// layout it shows, by its icon and name; none where Karpo is hidden there.
    /// </summary>
    private EditDeckSubject? LayoutSubject(string displayId)
    {
        if (BarOf(displayId) is null)
        {
            return null;
        }
        var layout = config.ActiveLayout(displayId);
        return new EditDeckSubject(BarLooks.GlyphOf(layout), layout.Name);
    }

    /// <summary>edit: the folder crumb (§326, §343) while a folder stands open arranged: "Folder", never which (§427).</summary>
    private static EditDeckSubject FolderSubject => new(OptionGlyphs.FolderOpen, S.CoreWidgetFolderListTitle);

    /// <summary>
    /// edit: the widget selected alone (§352), whose crumb the edit bar of its display shows: a line of
    /// <paramref name="open"/>, the folder open arranged, while one is; else a widget of a bar — the one
    /// clicked, a folder too. Its key, its display, and its kind by its icon (<see cref="SubjectOf"/>), whose
    /// icon the crumb wears, saying "Widget" (§427, <see cref="EditDeck.Buttons"/>). Null for none selected, or several.
    /// </summary>
    private (string Key, string Display, EditDeckSubject Subject)? SelectedAlone(OpenFolder? open)
    {
        if (editSelection.Count != 1 || SelectedHere(open) is not [var key] || config.FindIsland(Islands.OwnerOf(key), Islands.IdOf(key)) is not { } island)
        {
            return null;
        }
        var display = open?.Window.Id ?? windows.Find(window => Arranging(window) && window.Layout.PartIndex(key) >= 0)?.Id;
        return display is null ? null : (key, display, SubjectOf(island));
    }

    /// <summary>
    /// §575: what the bar follows (PanelMemory.Follow): the widget selected (§528 several as one), a folder selected or open
    /// arranged, and the display its entry stands on; nulls for none.
    /// </summary>
    private (string? Widget, string? Folder, string? Display) Followed(OpenFolder? open, (string Key, string Display, EditDeckSubject Subject)? picked, IReadOnlyList<string> several)
    {
        if (several.Count > 1)
        {
            return (string.Join("\n", several), null, open?.Window.Id ?? windows.Find(window => Arranging(window) && window.Layout.PartIndex(several[0]) >= 0)?.Id);
        }
        if (picked is { } one)
        {
            return IsFolderKey(one.Key) ? (null, one.Key, one.Display) : (one.Key, open?.Key, one.Display);
        }
        return open is not null ? (null, open.Key, open.Window.Id) : (null, null, null);
    }

    /// <summary>§529: whether the widget keyed <paramref name="key"/> is a folder (its face): a folder selected alone is the Folder page's.</summary>
    private bool IsFolderKey(string key) => config.FindIsland(Islands.OwnerOf(key), Islands.IdOf(key)) is BarItem { Folder: not null };

    /// <summary>
    /// §528: the widgets selected that edit mode shows now, in the order they were selected: of <paramref name="open"/>, the
    /// folder open arranged, its lines; else a bar's widgets, of any display; each one the file has.
    /// </summary>
    private List<string> SelectedHere(OpenFolder? open) =>
        [.. editSelection.Keys.Where(key => EditScope.In(key, open?.Key) && config.FindIsland(Islands.OwnerOf(key), Islands.IdOf(key)) is not null)];

    /// <summary>
    /// edit: what a widget is called where the edit bar names it (§352: its crumb; §394: its errors): its kind, by
    /// its icon, as its menu is named (<see cref="IslandEdits.Kind"/>); a folder's the folder's.
    /// </summary>
    private static EditDeckSubject SubjectOf(WidgetItem island) =>
        new(island is BarItem { Folder: not null } ? OptionGlyphs.Folder : WidgetStyles.Of(island)?.Widget.Glyph ?? island.Info?.Glyph ?? "", IslandEdits.Kind(island));

    /// <summary>
    /// edit: the part of <paramref name="window"/> whose folder stands open arranged, its face that
    /// a click closes the folder on (§251), drawn as itself (§267); -1 where none of its stands open,
    /// or the bar is not locked.
    /// </summary>
    private int ClosingPart(DockWindow window) =>
        BarLocked && ArrangedFolder() is { } open && ReferenceEquals(open.Window, window) ? window.Layout.PartIndex(open.Key) : -1;

    /// <summary>
    /// edit: a folder of <paramref name="window"/> stands open arranged (§397, the person: "dont show the add
    /// buttons on the taskbar anymore if a folder is open"): its columns' "+" are where to add, and this bar's
    /// stand aside — this bar's alone, the folder being its; another display's bar keeps its own.
    /// </summary>
    private bool FolderOpenOn(DockWindow window) => ArrangedFolder() is { } open && ReferenceEquals(open.Window, window);

    private sealed partial class DockWindow
    {
        /// <summary>edit: a press went down on the face of the folder open arranged (§251), whose release on it closes the folder.</summary>
        private bool closePressed;

        /// <summary>
        /// edit: a press on the face of the folder open arranged over this locked bar (§212,
        /// <see cref="EditClicks.OnLocked"/>), whose click closes it (§251): pressed, holding the mouse.
        /// </summary>
        private void LockedPressed()
        {
            closePressed = true;
            _ = SetCapture(Hwnd);
            scene.Edit(EditView());
        }

        /// <summary>
        /// edit: the release of a press on the open folder's face (<see cref="LockedPressed"/>): on it the
        /// folder closes, as by Escape, and the bars come back — a double-click's second too, whose first
        /// opened the folder: two clicks; off it, nothing. False where no press went down there.
        /// </summary>
        private bool CloseReleased(float x, float y)
        {
            if (!closePressed)
            {
                return false;
            }
            closePressed = false;
            if (GetCapture() == Hwnd)
            {
                _ = ReleaseCapture();
            }
            var closing = dock.ClosingPart(this);
            if (hovering && EditClicks.OnLocked(layout.HitTest(x, y, true) is { } on ? layout.PartOf(on) : -1, closing))
            {
                dock.EscapeTo(EditEscape.CloseFolder);
            }
            else
            {
                scene.Edit(EditView());
            }
            return true;
        }

        /// <summary>edit: the open folder's face's press lost the mouse: it lets go, and nothing closes.</summary>
        private void CloseCaptureLost()
        {
            if (closePressed)
            {
                closePressed = false;
                scene.Edit(EditView());
            }
        }
    }

    /// <summary>
    /// edit: a folder opened or closed: the edit bars' folder crumb comes or goes, the sections' edit bars
    /// with it — the chrome, as the frame ends (<see cref="PresentChrome"/>), so a folder switched for
    /// another leaves the edit bars as they stand, never without the crumb for a moment between the two.
    /// And the bars and places lock under a folder open arranged, and come back as it closes
    /// (<see cref="BarLocked"/>, §212) — undimmed throughout since §267.
    /// </summary>
    private void DecksFollowFolders()
    {
        if (editor is null)
        {
            return;
        }
        StirChrome();
        // edit: Escape leaves edit mode next, wherever the keys are (EditFollowHook.Refresh).
        followHook?.Refresh();
        foreach (var window in windows)
        {
            if (Arranging(window))
            {
                window.ShowEdit();
            }
        }
    }

    /// <summary>
    /// edit: what the edit bar of <paramref name="at"/> stands by (EditDeckPlan.Place): the display, and
    /// how deep what stands along each of its edges goes — every bar and place of it in sight there,
    /// each by what it takes (DockWindow.Takes) — or 0 with nothing there, as on a display that shows None.
    /// </summary>
    private EditDeckRoom DeckRoom(MonitorInfo at)
    {
        var display = at.Bounds;
        var shown = windows.Where(window => window.Id == at.Id && !window.IsAnchor && window.Shown).ToList();
        int Depth(Edge edge) => Math.Max(0, shown
            .Where(window => window.Layout.Edge == edge)
            .Select(window => window.Takes())
            .Select(takes => edge switch
            {
                Edge.Top => takes.bottom - display.Top,
                Edge.Left => takes.right - display.Left,
                Edge.Right => display.Right - takes.left,
                _ => display.Bottom - takes.top,
            })
            .DefaultIfEmpty(0)
            .Max());
        // And its work area, which its flyouts hang within.
        return new EditDeckRoom(display, Depth(Edge.Top), Depth(Edge.Bottom), Depth(Edge.Left), Depth(Edge.Right), at.Work);
    }

    /// <summary>edit: where the displays' edit bars stand on the screen, which the sections' edit bars keep clear of.</summary>
    private IEnumerable<RECT> DeckPanels() => chromes.Values.Select(chrome => chrome.Deck.Panel).OfType<RECT>();

    /// <summary>edit: the chrome for good: the dock is closing.</summary>
    private void DisposeChromes()
    {
        foreach (var chrome in chromes.Values)
        {
            chrome.Dispose();
        }
        chromes.Clear();
        FlyoutHost.Avoided = [];
    }

    /// <summary>Whether <paramref name="window"/> is a dim's or an edit bar's.</summary>
    private bool DeckWindow(HWND window) =>
        window != HWND.NULL && chromes.Values.Any(chrome => chrome.Shade.Hwnd == window || chrome.Deck.Owns(window));

    /// <summary>
    /// edit: edit mode ends as the person asked — "Done", Escape. §348, §487: a folder open stays open, at rest, whichever
    /// way they asked (<see cref="EndEdit"/>'s keepFolder): ending edit mode never closes the folder they are in. Escape
    /// closes it first as a step of its own (EditKeys.Escape), so its end of edit mode finds none open.
    /// </summary>
    private void LeaveEdit()
    {
        if (editor is not null)
        {
            EndEdit(rebuild: true, keepFolder: true);
        }
    }

    /// <summary>
    /// edit: a message of a dim's window or of an edit bar's. The dim takes no activation; a drag on it selects the widgets
    /// it runs over (Dock.EditSelect.cs), and a click only clears the selection, §575 and closes the flyout open. The edit
    /// bar's — its entries, its buttons, §575 its flyout's page, the keys while it has them — the bar's own
    /// (<see cref="EditDeck.Message"/>), what it asks for here (<see cref="DeckAct"/>). A press closes what a press elsewhere
    /// closes, here (<see cref="DeckPressed"/>): on the thing whose popup that was, it does nothing else (§343).
    /// </summary>
    private LRESULT? DeckMessage(HWND window, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == WM.WM_MOUSEACTIVATE)
        {
            // §520: a click on a flyout, §575 or on an entry, gives it the keys (the add list's search, its controls), as a
            // click on a window does; elsewhere on the edit bar the keys stay where they are (a bar's).
            var at = Cursor();
            return (LRESULT)(chromes.Values.Any(chrome => chrome.Deck.Owns(window) && chrome.Deck.TakesKeysAt(at)) ? MA.MA_ACTIVATE : MA.MA_NOACTIVATE);
        }
        foreach (var (id, chrome) in chromes)
        {
            var pair = (chrome.Shade, chrome.Deck);
            if (pair.Shade.Hwnd == window)
            {
                ShadeMessage(id, pair.Shade, msg, lParam);
                return null;
            }
            if (!pair.Deck.Owns(window))
            {
                continue;
            }
            if (msg is WM.WM_LBUTTONDOWN or WM.WM_RBUTTONDOWN or WM.WM_MBUTTONDOWN
                && DeckPressed(window, pair.Deck.PartAt(window, lParam), LightDismiss.ButtonOf(msg), ScreenOf(window, lParam))
                && msg == WM.WM_LBUTTONDOWN)
            {
                return null;
            }
            var (happened, handled) = pair.Deck.Message(window, msg, wParam, lParam);
            DeckAct(id, pair.Deck, happened);
            return handled ? (LRESULT)0 : null;
        }
        return null;
    }

    /// <summary>
    /// edit: what display <paramref name="id"/>'s edit bar (<paramref name="deck"/>) asked for, clicked or pressed by a
    /// screen reader (EditDeck.Invoked): §575 an entry's flyout opened or closed; §520 a page's control
    /// acted, a popup of a dropdown, a link followed; the errors' list, the end of edit mode.
    /// </summary>
    private void DeckAct(string id, EditDeck deck, EditDeckEvent happened)
    {
        // §684: a choice tried on under the pointer comes off before anything done on the panel is.
        if (happened.Action == EditDeckAction.Hover)
        {
            Report(() => PanelHovered(id, happened.Page, happened.Id));
            return;
        }
        if (happened.Action != EditDeckAction.None && tried is not null)
        {
            TakeOff();
        }
        switch (happened.Action)
        {
            // §575: an entry opens its flyout, any other closing; clicked again, it closes.
            case EditDeckAction.Open:
                Report(() =>
                {
                    // §339: ⚙'s backup words start afresh as Karpo's flyout opens from its entry.
                    if (happened.Page == PanelPage.Karpo)
                    {
                        ForgetBackupSaid();
                    }
                    OpenPanel(id, happened.Page);
                });
                break;
            case EditDeckAction.Close:
                Report(() => ClosePanel());
                break;
            // §520: a page's control: what its menu row did (Dock.SettingsPanel.cs).
            case EditDeckAction.Act:
                Report(() => PanelActed(id, happened.Page, happened.Id));
                break;
            // §521: a text or a number box typed in place kept its words: its field's, as a menu's field keeps them.
            case EditDeckAction.Type:
                Report(() => PanelTyped(id, happened.Page, happened.Id, happened.Text, happened.Found));
                break;
            case EditDeckAction.Mix:
                Report(() => PanelMixed(id, happened.Page, happened.Id, happened.Mixed, happened.Color));
                break;
            case EditDeckAction.Pop:
                Report(() => PanelPopup(id, deck, happened));
                break;
            case EditDeckAction.Link:
                Report(() => PanelLinked(id, happened.Page, happened.Id));
                break;
            // edit: out of edit mode in one click (§343); a folder open stays open, at rest (§348:
            // "it should only close the edit mode but not also the folder"; §487). Escape still closes it first.
            case EditDeckAction.Done:
                Report(LeaveEdit);
                break;
            // §394: the errors Karpo knows of, listed under their indicator (Dock.Faults.cs).
            // §551: a row of a page put in order (dragged and let go, Ctrl+Up, Ctrl+Down): its menu row's move (Dock.SettingsPanel.cs).
            case EditDeckAction.Reorder:
                Report(() => PanelMoved(id, happened.Page, happened.Id, happened.To));
                break;
        }
    }

    /// <summary>
    /// edit: the menu just opened hangs from <paramref name="part"/> of display <paramref name="displayId"/>'s edit bar (the
    /// errors' indicator, §520 a dropdown of §575 its flyout): that thing's, in the window it stands in, which one click on
    /// closes (§430, <see cref="Pending.Opener"/>).
    /// </summary>
    private void DeckOpened(string displayId, PanelHit part)
    {
        if (FlyoutOpen && pending is { } menu && chromes.TryGetValue(displayId, out var chrome))
        {
            menu.Opener = new PopupOpener((nint)(PanelLayout.OnBar(part.Kind) ? chrome.Deck.Hwnd : chrome.Deck.FlyoutHwnd), part, PressButton.Left);
        }
    }

    /// <summary>
    /// edit: a press (<paramref name="button"/>) went down on an edit bar (<paramref name="deck"/>), on
    /// <paramref name="part"/> (null for nothing it opens from), at <paramref name="screen"/>: it closes every popup a press
    /// elsewhere closes, as the hook would have — the hook leaves an edit bar's presses to it, as it does a bar's
    /// (<see cref="IsBar"/>), so the menu is still open here and what opened it known. Unless it closed before the press
    /// got here: by the hook, the dock busy as it heard the press (§522), or by the click's activation (§525: the open
    /// panel takes the keys from a click, WM_MOUSEACTIVATE, and the menu that had them goes as it loses them) — which
    /// the press knows all the same as one it closed (<see cref="Pressing"/>, heard at this point, dismissed since). True
    /// where the press closed the popup its thing opened (§343, §430: "when clicking again when a menu is open it should
    /// close it"): that press, and its release, do nothing else.
    /// </summary>
    private bool DeckPressed(HWND deck, object? part, PressButton button, POINT screen)
    {
        // §575: a press on another display's edit bar closes the flyout open (one at a time, a click elsewhere closing it).
        if (panelMemory.Page is not null && panelOn is { } on && chromes.TryGetValue(on, out var shown) && !shown.Deck.Owns(deck))
        {
            ClosePanel(now: false);
        }
        var (press, _) = Pressing(deck, part, button, screen);
        // Its menu, whatever kind of popup it is, goes with the press on its button.
        if (FlyoutOpen && pending?.Opener is { } own && own.On(press))
        {
            openers.Shut(own);
            CloseFlyout();
        }
        foreach (var host in FlyoutHost.OpenMenus)
        {
            dismissed = null;
            // What the hook would have posted: dismissed, with the focus back where it was; a folder
            // standing open arranged answers it by staying (FlyoutHost.StaysOpen).
            _ = DispatchNow(host.Hwnd, FlyoutHost.WM_KARPO_HOST_DISMISS, (WPARAM)(nuint)host.Generation, (LPARAM)(nint)1);
            openers.Shut(dismissed?.What.Opener);
        }
        dismissed = null;
        return openers.ClosedItsOwn;
    }

    /// <summary>
    /// edit: a message of display <paramref name="displayId"/>'s dim (<paramref name="shade"/>): a press
    /// may begin a drag that selects (Dock.EditSelect.cs); one that never leaves the drag threshold
    /// clears the selection, unless Ctrl or Shift is held. Edit mode goes on either way.
    /// </summary>
    private void ShadeMessage(string displayId, EditShade shade, uint msg, LPARAM lParam)
    {
        var at = new POINT { x = (short)((nint)lParam & 0xffff), y = (short)(((nint)lParam >> 16) & 0xffff) };
        _ = ClientToScreen(shade.Hwnd, &at);
        switch (msg)
        {
            case WM.WM_MOUSEMOVE:
                if (shadePressed == shade.Hwnd)
                {
                    BandMoved(at);
                }
                break;
            case WM.WM_LBUTTONDOWN:
                shadePressed = shade.Hwnd;
                // §575: a click elsewhere closes the flyout open.
                if (panelMemory.Page is not null)
                {
                    ClosePanel(now: false);
                }
                // edit: the keys to the display's bar, as a press on its own room gives them (DockWindow.EditPressed): the dim
                // takes no activation, so what a lasso here selected was out of the keys' reach — Ctrl+X, Ctrl+C and Delete
                // after it went wherever the keys had been (the person, 2026-11-03: "when selecting multiple widgets ctrl x
                // does not work"). A folder standing open arranged keeps them: its lines are what a lasso selects then.
                if (ArrangedFolder() is null && BarOf(displayId) is { } bar)
                {
                    FocusBar(bar);
                }
                BandPressed(displayId, at, KeyState.Down(VK.VK_CONTROL) || KeyState.Down(VK.VK_SHIFT));
                _ = SetCapture(shade.Hwnd);
                break;
            case WM.WM_LBUTTONUP:
            {
                if (shadePressed != shade.Hwnd)
                {
                    break;
                }
                shadePressed = HWND.NULL;
                if (GetCapture() == shade.Hwnd)
                {
                    _ = ReleaseCapture();
                }
                // A drag that selected, or a click, which leaves edit mode on (EditClicks.OnFrame).
                var adding = KeyState.Down(VK.VK_CONTROL) || KeyState.Down(VK.VK_SHIFT);
                if (EditClicks.OnFrame(BandReleased(at), adding) == FrameClick.Unselect && editSelection.Count > 0)
                {
                    editSelection.Clear();
                    ShowSelection();
                }
                break;
            }
            case WM.WM_CAPTURECHANGED:
                if ((HWND)(nint)lParam != shade.Hwnd && shadePressed == shade.Hwnd)
                {
                    shadePressed = HWND.NULL;
                    BandCancelled();
                }
                break;
        }
    }

    /// <summary>
    /// §521, §531: a control of a display's edit bar or §575 its flyout a popup hangs from (<see cref="Pending.From"/>, PopupPlace) —
    /// a dropdown, the errors' indicator —: <paramref name="Box"/> where it stands, <paramref name="Within"/> the edit bar or the
    /// flyout, whose sides the popup keeps inside where it fits; that display's work area and scale. All in that display's
    /// screen px.
    /// </summary>
    private readonly record struct DeckControl(RECT Box, RECT Within, RECT Work, float Scale)
    {
        /// <summary>Where a popup of it is handed (its placement is the control's): its foot's left.</summary>
        public POINT Anchor => new() { x = Box.left, y = Box.bottom };
    }

    /// <summary>
    /// The control <paramref name="box"/> of display <paramref name="displayId"/>'s edit bar (<see cref="EditDeck.OnScreen"/>)
    /// as a popup hangs from it; null where it is not in sight.
    /// </summary>
    private DeckControl? DeckControlOf(string displayId, RECT box) =>
        box.right > box.left && box.bottom > box.top && chromes.TryGetValue(displayId, out var chrome)
        && EditedDisplays.Find(display => string.Equals(display.Id, displayId, StringComparison.OrdinalIgnoreCase)) is { } display
            ? new DeckControl(box, chrome.Deck.Within(box), BarOf(displayId)?.WorkArea() ?? Rect(display.Work), display.Dpi / 96f)
            : null;
}

/// <summary>
/// edit: what a display in edit mode shows behind its bars and places (see the top of the file): the
/// whole display dimmed, 50 % black, as Windows dims behind what wants the attention. §623: the same layer, in the
/// Appearance's colour (<see cref="Tint"/>), is the folder's backdrop at rest (Dock.Backdrop.cs): one window a display,
/// under the bars and the folder, over every other app, fading by the motion.
/// </summary>
internal sealed unsafe class EditShade : IDisposable
{
    /// <summary>
    /// How dark: 50 % black, in every theme (the person, 2026-10-23: "the background should be even
    /// a little darker"; §297; it was 40 %). The desktop still shows through.
    /// </summary>
    internal const byte Alpha = 0x80;

    /// <summary>The dim's colour: black at <see cref="Alpha"/>.</summary>
    internal static Color Ink => Color.FromArgb(Alpha, 0, 0, 0);

    private readonly DesktopWindowTarget target;
    private readonly SpriteVisual dim;
    private readonly CompositionColorBrush brush;

    /// <summary>§623: the window's title of the folder's backdrop (<see cref="EditShade(Gfx, HINSTANCE, nint, Color?, string)"/>).</summary>
    internal const string BackdropTitle = "Karpo folder backdrop"; // not-ui: a window's title

    /// <param name="dock">Goes into the window's GWLP_USERDATA, so the dock's window procedure finds the dock.</param>
    /// <param name="ink">Tests only: another colour than <see cref="Ink"/>, which a film's capture tells from nothing (it gives what is translucent flattened on black).</param>
    /// <param name="name">§623: its window's title: edit mode's dim's, or the folder's backdrop's (<see cref="BackdropTitle"/>).</param>
    public EditShade(Gfx gfx, HINSTANCE instance, nint dock, Color? ink = null, string name = "Karpo edit dim") // not-ui: a window's title
    {
        fixed (char* className = Preview.ClassName)
        fixed (char* title = name)
        {
            Hwnd = CreateWindowExW(
                WS.WS_EX_TOOLWINDOW | WS.WS_EX_TOPMOST | WS.WS_EX_NOACTIVATE | WS.WS_EX_NOREDIRECTIONBITMAP,
                className,
                title,
                WS.WS_POPUP,
                0,
                0,
                1,
                1,
                HWND.NULL,
                HMENU.NULL,
                instance,
                null);
        }
        if (Hwnd == HWND.NULL)
        {
            throw new InvalidOperationException($"{nameof(CreateWindowExW)} failed: 0x{Marshal.GetLastPInvokeError():X8}");
        }
        _ = SetWindowLongPtrW(Hwnd, GWLP.GWLP_USERDATA, dock);
        BOOL on = BOOL.TRUE;
        _ = DwmSetWindowAttribute(Hwnd, (uint)DWMWINDOWATTRIBUTE.DWMWA_TRANSITIONS_FORCEDISABLED, &on, (uint)sizeof(BOOL));
        try
        {
            var c = gfx.Compositor;
            target = PointerWindow.CreateTarget(c, Hwnd);
            dim = c.CreateSpriteVisual();
            brush = c.CreateColorBrush(ink ?? Ink);
            dim.Brush = brush;
            // Clear while out of sight (§359): it comes and goes in the commit the bars' edit dress does.
            dim.Opacity = 0f;
            target.Root = dim;
        }
        catch
        {
            Surfaces.Destroy(Hwnd);
            throw;
        }
        // Under everything edit mode shows, over every other app (ZPlan).
        Surfaces.Enlist(Hwnd, Stratum.Dim, clear: true);
    }

    public HWND Hwnd { get; }

    /// <summary>§623: drawn in <paramref name="ink"/> from now on, its opacity as it stands (the folder's backdrop's colour).</summary>
    public void Tint(Color ink)
    {
        if (brush.Color != ink)
        {
            brush.Color = ink;
        }
    }

    /// <summary>§623: the colour it is drawn in.</summary>
    internal Color Tinted => brush.Color;

    /// <summary>
    /// Over the display at <paramref name="bounds"/> (screen px), under everything edit mode shows (its
    /// stratum, ZPlan). Its window comes into sight clear as the frame ends and its dim with the frame's
    /// commit — the one the bars' edit dress comes in (§359); it goes (<see cref="Hide"/>) clear in the
    /// commit the bars come to rest in, its window after it. Placed again only for another display.
    /// §612: <paramref name="dimmed"/> or clear — the Appearance's preview shows the display as it is, the window still there,
    /// so a click on it is one on the dim —, fading there by <paramref name="motion"/> (a large surface's Show and Hide), at
    /// once with motion off.
    /// </summary>
    public void Show(RectI bounds, bool dimmed, Motion motion)
    {
        dim.Size = new Vector2(bounds.Width, bounds.Height);
        if (placed != bounds)
        {
            _ = Surfaces.Place(Hwnd, bounds.Left, bounds.Top, bounds.Width, bounds.Height);
            placed = bounds;
        }
        _ = Fade(dimmed, motion);
        Surfaces.Show(Hwnd);
    }

    /// <summary>Where it was placed last.</summary>
    private RectI? placed;

    /// <summary>§623: tests only: where it was placed last (screen px); empty before it ever was.</summary>
    internal RECT Placed => placed is { } at ? new RECT { left = at.Left, top = at.Top, right = at.Right, bottom = at.Bottom } : default;

    /// <summary>Tests only: its dim's opacity as it ends, the content the frame's commit brings.</summary>
    internal float Opacity => dim.Opacity;

    /// <summary>§612: tests only: whether its last change faded by the motion, and toward which opacity.</summary>
    internal (bool Fading, float To) Faded => (fading, dim.Opacity);

    private bool fading;

    /// <summary>§612: when the fade set going last ends (ms on the stopwatch).</summary>
    private double ends;

    /// <summary>§612: dimmed or clear as edit mode last asked; where between the two it stands, by its motion.</summary>
    private MotionClock clock;

    /// <summary>
    /// §612: edit mode over: clear by <paramref name="motion"/>'s Hide (at once with motion off), its window out of sight once
    /// it is.
    /// </summary>
    public void Hide(Motion motion)
    {
        var ms = Fade(false, motion);
        Surfaces.Hide(Hwnd, afterMs: ms > 0 ? ms + FadeGraceMs : 0);
    }

    /// <summary>§612: past the fade, before its window goes: a frame for its last step to be drawn.</summary>
    private const int FadeGraceMs = 17;

    /// <summary>
    /// §612: the dim set going toward dimmed (<paramref name="dimmed"/>) or clear from where it stands, on a large surface's
    /// times (<see cref="PopupEntrance.Bar"/>: in 250 ms decelerating, out 167 accelerating, at Normal) in the share of them
    /// left of the way; how long it takes, in ms (0: there at once).
    /// </summary>
    private int Fade(bool dimmed, Motion motion)
    {
        var to = dimmed ? 1f : 0f;
        var now = Now();
        // On its way there already, or there: left to go on, what is left of it said.
        if (dim.Opacity == to)
        {
            return fading && ends > now ? (int)Math.Ceiling(ends - now) : 0;
        }
        var plan = PopupMotion.Of(motion, PopupEntrance.Bar);
        var (from, ms) = clock.Go(now, dimmed, plan);
        dim.StopAnimation("Opacity");
        dim.Opacity = to;
        fading = ms >= 1;
        ends = now + ms;
        if (!fading)
        {
            clock.Snap(dimmed);
            return 0;
        }
        var c = dim.Compositor;
        using var ease = MotionEase.Of(c, dimmed ? plan.EnterCurve : plan.ExitCurve);
        using var fade = c.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0, from);
        fade.InsertKeyFrame(1, to, ease);
        fade.Duration = Motion.Span(ms);
        dim.StartAnimation("Opacity", fade);
        return (int)Math.Ceiling(ms);
    }

    private static double Now() => System.Diagnostics.Stopwatch.GetTimestamp() * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

    public void Dispose()
    {
        Surfaces.Destroy(Hwnd, () =>
        {
            target.Root = null;
            dim.Brush = null;
            brush.Dispose();
            dim.Dispose();
            target.Dispose();
        });
    }
}

/// <summary>What the edit bar and its flyouts ask the dock to do (§520, §575).</summary>
public enum EditDeckAction
{
    None,

    /// <summary>§575: an entry clicked (the event's page; "+ Add" the add list's): its flyout opens, any other closing.</summary>
    Open,

    /// <summary>§575: the entry whose flyout stands open clicked again: it closes.</summary>
    Close,

    /// <summary>"Done" (§352): edit mode ends in one click; a folder open stays open, at rest (§348).</summary>
    Done,


    /// <summary>§520: a control of a page acted — a toggle, a step, a segment, a button, a row that acts, §575 a widget of the add list — sending the row's id, as its menu row did.</summary>
    Act,

    /// <summary>§520: a dropdown, a colour or a text box clicked: what its row's level holds, in a popup under it (the event's box).</summary>
    Pop,

    /// <summary>§520: a link row: where it leads, a page of its own.</summary>
    Link,

    /// <summary>§521: a text or a number box typed in place kept its words (the event's text): its field's id, as a menu's field kept them.</summary>
    Type,

    /// <summary>
    /// §670: an inline picker's colour mixed (SettingsPanel.Picker.cs): its mixer's id, the colour (the event's
    /// <see cref="EditDeckEvent.Color"/>) worn while dragged, kept let go, put back called off (<see cref="EditDeckEvent.Mixed"/>).
    /// </summary>
    Mix,

    /// <summary>
    /// §551: a row of a page dragged into order and let go, or moved by Ctrl+Up or Ctrl+Down: its menu row's id, the place
    /// among its own it goes to (the event's <see cref="EditDeckEvent.To"/>), as the menus' grip moves it.
    /// </summary>
    Reorder,

    /// <summary>
    /// §684 (the person, 2026-11-12: "when hovering over a font for example or color it should also apply it as preview again
    /// ... this should be a general design rule"): the choice of an inline picker the pointer rests on (its menu row's id; 0
    /// off every choice), tried on (Dock.TryOn.cs) till it moves off or something is done.
    /// </summary>
    Hover,
}

/// <param name="Id">§520: the menu row's id a page's control sends; its page (<paramref name="Page"/>) and its row's key there (<paramref name="Key"/>).</param>
/// <param name="Box">§520: a popup's control on the screen, which the popup hangs from: a page's control, "+ Add", the errors' indicator (§531).</param>
/// <param name="Text">§521: the words a text or a number box kept (<see cref="EditDeckAction.Type"/>).</param>
/// <param name="To">§551: the place a row put in order goes to among its own (<see cref="EditDeckAction.Reorder"/>).</param>
/// <param name="Found">§676: what a lookup's inline list picked (<see cref="EditDeckAction.Type"/>), its field's as a menu's search keeps it.</param>
public readonly record struct EditDeckEvent(EditDeckAction Action, uint Id = 0, PanelPage Page = default, string Key = "", RECT Box = default, string Text = "", int To = -1,
    FlyoutEventKind Mixed = default, Argb Color = default, OptionFound? Found = null);

/// <summary>What the edit bar's display or layout is (§352): its icon and its name.</summary>
public readonly record struct EditDeckSubject(string Glyph, string Name);

/// <summary>
/// What a display's edit bar is of (§352, §520): the display (<paramref name="Display"/>), the layout it shows
/// (<paramref name="Layout"/>; null on one that shows None), the folder open arranged on it (<paramref name="Folder"/>; null
/// for none). "Add" adds to the folder, or to the bar; with neither (a display showing None) it stands greyed. §394: how many
/// errors Karpo knows of (<paramref name="Errors"/>, Faults): their indicator shows while there are any.
/// </summary>
public sealed record EditDeckState(EditDeckSubject Display, EditDeckSubject? Layout, EditDeckSubject? Folder = null, int Errors = 0)
{
    /// <summary>A display called "Display 1" showing a layout called "Default", nothing open: a test's.</summary>
    internal static EditDeckState Plain => new(new EditDeckSubject(OptionGlyphs.Monitor, S.CoreMonitorDisplay("1")), new EditDeckSubject(TaskbarLayouts.LayoutGlyph, S.CoreLayoutDefaultName));

    /// <summary>Whether "Add" has somewhere to add to: the folder open, or the display's bar.</summary>
    public bool CanAdd => Folder is not null || Layout is not null;
}

/// <summary>
/// What the edit bar of a display stands by (<see cref="EditDeckPlan.Place"/>): the display at
/// <paramref name="Display"/> (screen px), and how deep, in px from its top and from its bottom, what
/// stands along its top edge and along its bottom edge goes: a bar or a place of it, 0 with nothing there;
/// §343: and from its left and its right, what stands along its sides, which the edit bar, wider now,
/// keeps clear of too. The display's work area (<paramref name="Work"/>, the display where none is given), which its
/// flyouts hang within.
/// </summary>
public sealed record EditDeckRoom(RectI Display, int TopDepth, int BottomDepth = 0, int LeftDepth = 0, int RightDepth = 0, RectI? Work = null)
{
    /// <summary>
    /// The side the edit bar stands at, across from the display's bars (§296): the bottom where something
    /// stands along the top and nothing along the bottom; the top otherwise — a bar at the bottom, bars
    /// at the sides, none, or a bar at the top and one at the bottom (it stands under the top one).
    /// </summary>
    public Edge Side => TopDepth > 0 && BottomDepth <= 0 ? Edge.Bottom : Edge.Top;

    /// <summary>Where its flyouts may stand: the work area, or the display where none was given.</summary>
    public RectI Inside => Work ?? Display;
}

/// <param name="Panel">Where the bar's panel stands (screen px).</param>
/// <param name="Anchor">The middle of the inner side of what stands along its side, beside the panel: where it hangs from, <see cref="EditDeckPlan.MarginDip"/> off.</param>
/// <param name="Edge">The side it stands at (<see cref="EditDeckRoom.Side"/>), which it and what it opens hang from, as a flyout from its bar's edge.</param>
public readonly record struct EditDeckSpot(RectI Panel, POINT Anchor, Edge Edge = Edge.Top);

/// <summary>Where the edit bar stands on its display (see the top of the file): pure, so the tests place it.</summary>
public static class EditDeckPlan
{
    /// <summary>Between the edit bar and the bar it stands by, and between it and the display's sides.</summary>
    public const float MarginDip = 8;

    /// <summary>
    /// Where the edit bar <paramref name="width"/> by <paramref name="height"/> px stands on the display
    /// of <paramref name="room"/> at <paramref name="scale"/>: centred, at the side across from the bars
    /// (<see cref="EditDeckRoom.Side"/>; the person, 2026-10-09: centred whatever the layout; 2026-10-23:
    /// at the bottom under a bar at the top), <see cref="MarginDip"/> off the deepest of what stands along
    /// that edge (<see cref="EditDeckRoom.TopDepth"/>, <see cref="EditDeckRoom.BottomDepth"/>), or off the
    /// display's edge with nothing there. Inside the display, <see cref="MarginDip"/> from its sides, so
    /// it never covers a bar, a place, or a widget in them; §343: off a bar along a side by its window's
    /// room round the panel (<see cref="FlyoutHost.RoomDip"/>, which takes the pointer too), pushed along
    /// from the middle where it would reach one. What it opens — the add list, the menus, the tooltips —
    /// hangs from that side too (<see cref="EditDeckSpot.Edge"/>), never running over the edit bar, a bar
    /// or off the display. §578: one width whatever is selected (PanelLayout), so nothing of it moves as the selection comes
    /// and goes. §630: always there; nothing moves it (§395 to §629 a drag by its grip did).
    /// </summary>
    public static EditDeckSpot Place(EditDeckRoom room, int width, int height, float scale)
    {
        var display = room.Display;
        var gap = (int)MathF.Round(MarginDip * scale, MidpointRounding.AwayFromZero);
        var side = room.Side;
        // The inner side of what stands along that edge: the display's own edge with nothing there.
        var inner = side == Edge.Bottom ? display.Bottom - Math.Max(0, room.BottomDepth) : display.Top + Math.Max(0, room.TopDepth);
        var (least, right) = Across(room, scale);
        var most = right - width;
        var left = Math.Clamp((display.Left + display.Right - width) / 2, least, Math.Max(least, most));
        var top = Math.Clamp(side == Edge.Bottom ? inner - gap - height : inner + gap, display.Top + gap, Math.Max(display.Top + gap, display.Bottom - gap - height));
        return new EditDeckSpot(new RectI(left, top, left + width, top + height), new POINT { x = left + (width / 2), y = inner }, side);
    }

    /// <summary>
    /// How far across <paramref name="room"/>'s display the edit bar may reach at <paramref name="scale"/>
    /// (screen px): <see cref="MarginDip"/> in from its sides, or off a bar along a side by its window's
    /// room (<see cref="FlyoutHost.RoomDip"/>, §343).
    /// </summary>
    private static (int Left, int Right) Across(EditDeckRoom room, float scale)
    {
        var gap = (int)MathF.Round(MarginDip * scale, MidpointRounding.AwayFromZero);
        var clear = (int)MathF.Round(FlyoutHost.RoomDip * scale, MidpointRounding.AwayFromZero);
        return (room.Display.Left + (room.LeftDepth > 0 ? room.LeftDepth + clear : gap), room.Display.Right - (room.RightDepth > 0 ? room.RightDepth + clear : gap));
    }

    /// <summary>
    /// §348: the widest the edit bar may be on <paramref name="room"/>'s display at <paramref name="scale"/>
    /// (px), what <see cref="Place"/> leaves it between the sides (<see cref="Across"/>): wider, it would
    /// run off the display or into a bar along a side, so <see cref="For"/> narrows its crumbs.
    /// </summary>
    public static float Widest(EditDeckRoom room, float scale)
    {
        var (left, right) = Across(room, scale);
        return Math.Max(0, right - left);
    }
}

/// <summary>
/// §520, §575: a display's edit bar in edit mode — a row of entries centred at the display's top or bottom (see the top of
/// the file and SettingsPanel.Layout.cs), one popup (FlyoutHost) — and the flyout of the entry open, a popup of its own hung
/// from it; each one surface drawn whole whenever what it shows changes (PanelPainter).
/// </summary>
public sealed unsafe class EditDeck : IDisposable
{
    /// <summary>The caret's blink and a landing's row lit a moment (§520 decision 8).</summary>
    private const nuint BlinkTimer = 1;

    private const nuint LitTimer = 2;

    /// <summary>How long a landing's row stays lit.</summary>
    private const uint LitMs = 1600;

    /// <summary>The bar's window.</summary>
    private readonly FlyoutHost host;

    /// <summary>§575: the flyout's window: the page of the entry open.</summary>
    private readonly FlyoutHost drop;

    /// <summary>§575: the add list's own search, at its flyout's top.</summary>
    private readonly MenuTextField filter = new("");

    private (CompositionDrawingSurface Surface, SpriteVisual Sprite, CompositionSurfaceBrush Brush)? barDrawn;
    private (CompositionDrawingSurface Surface, SpriteVisual Sprite, CompositionSurfaceBrush Brush)? dropDrawn;
    private PanelLayout? layout;

    /// <summary>§629: the flyout's ground under its drawing, and the add page's ghost through it where it stands behind the page.</summary>
    private readonly SeeThroughView seeThrough = new();

    /// <summary>§629: the add page's ghost, where it stands (the dock's: <see cref="SeeThrough"/>); none for none.</summary>
    private IReadOnlyList<GhostSight> sights = [];

    /// <summary>§529: the tooltip of the header's icon buttons and of a line cut short.</summary>
    private readonly TipView tip;

    private EditDeckSpot spot;
    private Placement? placed;
    private PanelHit hot;
    private PanelHit pressed;
    private PanelHit focus;

    /// <summary>The keys moved the focus last (not the pointer): the focus ring shows.</summary>
    private bool keys;

    /// <summary>§650: a page whose field has the keys as it opens (the add page's search): its window takes them once it shows.</summary>
    private bool takeKeys;

    private float scroll;

    private string? lit;

    /// <summary>§575: what the flyout hung from as it was shown last (screen px): shown again from it, it stands as it stands.</summary>
    private RECT dropFrom;

    /// <summary>
    /// §521: a text or a number box being typed in: what it is (its field's id, its row's key), its field, and what it said
    /// when the typing began (Escape puts it back); null while none is.
    /// </summary>
    private (PanelHit Hit, MenuTextField Field, string Original)? editing;

    /// <summary>§670: what is typed in each inline picker's search, by its row's key: kept while the page is shown again.</summary>
    private readonly Dictionary<string, string> searches = new(StringComparer.Ordinal);

    /// <summary>
    /// §676: each lookup's search (a time zone's, a language's, a place's) by its search box's key: what it finds as it is
    /// typed, asked of its finder off this thread (MenuSearch, the popup's own); its answers brought back here
    /// (<see cref="answers"/>) on <see cref="FindTimer"/>, which runs while one is waited for.
    /// </summary>
    private readonly Dictionary<string, MenuSearch> lookups = new(StringComparer.Ordinal);

    /// <summary>§682: how far each picker body is scrolled inside itself (px), by its key: kept while the panel stands.</summary>
    private readonly Dictionary<string, float> pickerScrolls = new(StringComparer.Ordinal);

    private readonly System.Collections.Concurrent.ConcurrentQueue<Action> answers = new();

    /// <summary>§676: while a lookup waits or is asked, every 33 ms: its answers in, a wait over.</summary>
    private const nuint FindTimer = 6;

    /// <summary>§670: an inline picker's colour being dragged: its mixer, the colour it started from, the one now and its hue held.</summary>
    private (PanelHit Hit, Argb Start, Argb Color, Hsv Hsv)? mixed;

    /// <summary>§551: a row dragged into order scrolls the page while the pointer stands near its top or foot.</summary>
    private const nuint ReorderTimer = 4;

    /// <summary>
    /// §671 (the person, 2026-11-12: "when opening a configuration inline dropdown and it does not fit on the screen it should
    /// smoothly scroll so that the whole inline dropdown is shown"): the page gliding to show an expander just opened, a frame
    /// each 16 ms over <see cref="RevealMs"/>, easing out (WinUI's BringIntoView); none while animations are off.
    /// </summary>
    private const nuint RevealTimer = 5;

    private const float RevealMs = 250;

    /// <summary>§671: the glide under way: from where, to where, since when (Environment.TickCount64).</summary>
    private (float From, float To, long Since)? reveal;

    /// <summary>§551: a press on a row put in order, which a move past the system's drag distance lifts: its key, where (flyout px).</summary>
    private (string Key, Vector2 At)? reorderArmed;

    /// <summary>§551: the row being dragged into order, and where the pointer is (flyout px); null for none.</summary>
    private PanelDrag? reorder;

    private Vector2 reorderPointer;

    /// <summary>§551: a row moved by the keys: a screen reader is told where it stands now, once the page shows it there.</summary>
    private bool announce;

    /// <summary>§551, tests: the row being dragged into order; null for none.</summary>
    internal PanelDrag? Reordering => reorder;

    /// <param name="Content">§575: the pages, an entry each.</param>
    /// <param name="Open">§575: the page whose flyout stands open; null for none.</param>
    private sealed record Placement(Gfx Gfx, Look Look, float Scale, EditDeckRoom Room, EditDeckState State, SettingsContent? Content, PanelPage? Open, RECT? Clear = null);

    /// <param name="dock">Goes into the window's GWLP_USERDATA, so the dock's window procedure finds the dock.</param>
    public EditDeck(Gfx gfx, HINSTANCE instance, nint dock)
    {
        // A flyout's own rim (§344); over the bars and an arranged folder (§359); §489: a large surface's rise and fade as
        // it comes and goes (PopupEntrance.Bar).
        host = new FlyoutHost(gfx, instance, dock, FlyoutMode.Tool) { Stratum = Stratum.EditBar, Entrance = PopupEntrance.Bar };
        // §575: a flyout as the edit bar stands, over the bars and an arranged folder, under the popups and the menus its controls open.
        drop = new FlyoutHost(gfx, instance, dock, FlyoutMode.Tool) { Stratum = Stratum.EditBar };
        // §529: the header's icon buttons and a line cut short say themselves in a tooltip, kept (§359).
        tip = new TipView(gfx, instance, dock);
        // narrator, §520: a screen reader finds the bar's buttons, §575 and the flyout's page (DeckAccess).
        Uia.Register(host.Hwnd, new DeckAccess(this, bar: true));
        Uia.Register(drop.Hwnd, new DeckAccess(this, bar: false));
    }

    public HWND Hwnd => host.Hwnd;

    /// <summary>§575: the flyout's window.</summary>
    internal HWND FlyoutHwnd => drop.Hwnd;

    /// <summary>§520: what a thing pressed by a screen reader asks for (UI Automation's Invoke), as its click does; the dock's.</summary>
    public Action<EditDeckEvent>? Invoked { get; set; }

    /// <summary>§520: what it shows changed of itself — a page, an expander —: the dock's stage map follows.</summary>
    internal Action? Changed { get; set; }

    /// <summary>§520: what the bar remembers (the expanders open, §575 the flyout open): the dock's one, every display's.</summary>
    internal PanelMemory Memory { get; set; } = new();

    public bool IsOpen => host.IsOpen;

    /// <summary>§575: whether its flyout stands open.</summary>
    internal bool FlyoutOpen => drop.IsOpen;

    /// <summary>§575: the page whose flyout stands open; null for none.</summary>
    internal PanelPage? Page => drop.IsOpen && layout is { IsOpen: true } ? placed?.Open : null;

    /// <summary>§575: what is typed in the add list's own search.</summary>
    internal string Filtered => filter.Text;

    /// <summary>§521: a text or a number box being typed in, §575 or the add list's search: Ctrl+Z is its own then.</summary>
    internal bool Typing => host.IsOpen && (filter.Focused || editing is not null);

    /// <summary>§521, tests: the words of the box being typed in; null for none.</summary>
    internal string? Editing => editing?.Field.Text;

    /// <summary>Whether <paramref name="window"/> is the bar's, §575 its flyout's or its tooltip's.</summary>
    public bool Owns(HWND window) => window != HWND.NULL && (window == host.Hwnd || window == drop.Hwnd || window == tip.Hwnd);

    /// <summary>Where the bar stands on the screen while it shows; null otherwise.</summary>
    public RECT? Panel => IsOpen ? host.PanelOnScreen : null;

    /// <summary>Whether it shows a folder's (§297, §352).</summary>
    public bool Folder => IsOpen && placed is { State.Folder: not null };

    /// <summary>What it shows now, for a test.</summary>
    internal EditDeckState? State => placed?.State;

    /// <summary>§526, stage: the tokens it is drawn in.</summary>
    internal FlyoutTheme Theme => host.Theme;

    /// <summary>§526, stage: the font it is measured and drawn in (<see cref="Wearing"/>).</summary>
    internal TextStyle Font => placed?.Look.Typeface ?? default;

    /// <summary>What it stands by now, for a test.</summary>
    internal EditDeckRoom? Room => placed?.Room;

    /// <summary>Tests only: how many times it came into sight sliding and fading in (<see cref="FlyoutHost.Enter"/>).</summary>
    internal int Entrances { get; private set; }

    /// <summary>Where the bar stands now, for a test.</summary>
    internal RECT PanelOnScreen => host.PanelOnScreen;

    /// <summary>§575: where its flyout stands now; empty while none is open.</summary>
    internal RECT FlyoutOnScreen => drop.IsOpen ? drop.PanelOnScreen : default;

    /// <summary>Tests only: §584 its flyout's beak — the side it stands out of, its tip along it, how far out (px); none while none is open.</summary>
    internal (Edge Side, float Tip, float Tall)? FlyoutBeak => drop.IsOpen ? drop.BeakPlaced : null;

    /// <summary>Tests only: the materials its window made, and how many times the window itself was placed (§355).</summary>
    internal (int Materials, int Frames) HostMade => (host.MaterialsMade, host.FramesPlaced);

    /// <summary>The layout shown, for a test that asks what is where.</summary>
    internal PanelLayout? Layout => layout;

    /// <summary>Where it was placed (<see cref="EditDeckPlan.Place"/>), for a test that asks.</summary>
    internal EditDeckSpot Spot => spot;

    /// <summary>
    /// Where <paramref name="hit"/> stands on the screen — on the bar, §575 or in its flyout —; empty where it is not in sight.
    /// What it opens hangs from it (Dock.DeckControl, PopupPlace): the errors' list, a page's dropdown alike (§531).
    /// </summary>
    internal RECT OnScreen(PanelHit hit)
    {
        var bar = PanelLayout.OnBar(hit.Kind);
        if (layout is not { } now || !(bar ? host : drop).IsOpen || now.RectOf(hit) is not { IsEmpty: false } r)
        {
            return default;
        }
        var panel = (bar ? host : drop).PanelOnScreen;
        return new RECT { left = panel.left + (int)r.X, top = panel.top + (int)r.Y, right = panel.left + (int)MathF.Ceiling(r.Right), bottom = panel.top + (int)MathF.Ceiling(r.Bottom) };
    }

    /// <summary>§531, §575: what a popup hung from <paramref name="box"/> (screen px) keeps inside: the flyout it stands in, else the bar.</summary>
    internal RECT Within(RECT box) =>
        drop.IsOpen && drop.PanelOnScreen is var flyout && box.left >= flyout.left && box.top >= flyout.top && box.right <= flyout.right && box.bottom <= flyout.bottom
            ? flyout : host.PanelOnScreen;

    /// <summary>
    /// Shows the bar at the top or the bottom of <paramref name="room"/>'s display (<see cref="EditDeckPlan.Place"/>), an entry
    /// for each page of <paramref name="content"/>, showing <paramref name="state"/> (a test's plain one for none); §575 the
    /// flyout of <paramref name="open"/> hung from its entry (none for null), §593 the add page's from the whole bar. It stands
    /// over its dim, the bars and an arranged folder by its stratum (ZPlan, §359); its flyout over it.
    /// </summary>
    internal void Show(Gfx gfx, Look look, float scale, EditDeckRoom room, EditDeckState? state = null, SettingsContent? content = null, PanelPage? open = null, RECT? clear = null)
    {
        var next = new Placement(gfx, look, scale, room, state ?? EditDeckState.Plain, content, open, clear);
        // Shown again as it stands: nothing to do (the chrome reconciled for something else's sake).
        if (host.IsOpen && placed is { } last && last == next)
        {
            return;
        }
        if (open != placed?.Open)
        {
            Opening(open);
        }
        placed = next;
        Open();
    }

    /// <summary>Public for the tests' and pictures' plain bar: <see cref="Show(Gfx, Look, float, EditDeckRoom, EditDeckState?, SettingsContent?, PanelPage?)"/> with no entries.</summary>
    public void Show(Gfx gfx, Look look, float scale, EditDeckRoom room, EditDeckState? state) => Show(gfx, look, scale, room, state, null, null);

    /// <summary>
    /// §575: another flyout comes (<paramref name="open"/>; none for null): its page from its top, a box being typed in kept, the
    /// add list's search empty with the caret in it (as the list's own search had the keys as it opened).
    /// </summary>
    private void Opening(PanelPage? open)
    {
        scroll = 0;
        if (editing is not null)
        {
            Kept();
        }
        CancelReorder();
        filter.Show("");
        filter.Blur();
        if (open == PanelPage.Add)
        {
            filter.Focus(selectAll: false);
            focus = new PanelHit(PanelHitKind.Filter);
            Blink();
            // §650: and its window the keys once it shows (Open), whatever opened it — a bar's "+", a folder's, this bar's
            // Add —: the first letter typed lands in the search, no second click.
            takeKeys = true;
        }
        // §628: a page under another's entry opened by the keys (Enter on Add a layout) has them on its way back, the first of it.
        else if (open is { } under && PanelRows.IsUnder(under) && keys)
        {
            focus = new PanelHit(PanelHitKind.Back);
        }
        else if (focus.Kind == PanelHitKind.Filter || (!focus.IsNone && !PanelLayout.OnBar(focus.Kind)))
        {
            focus = PanelHit.None;
        }
    }

    /// <summary>Out of sight, as edit mode ends: fading as a popup closes (none with animations off).</summary>
    public void Hide()
    {
        hot = pressed = focus = PanelHit.None;
        // §593: no ghost of a page gone (edit mode's end takes the dock's).
        proposed = 0;
        // §551: a row being dragged into order as edit mode ends: put back.
        reorder = null;
        reorderArmed = null;
        lit = null;
        // §521: a box being typed in as edit mode ends: what it said stays.
        editing = null;
        foreach (var window in (HWND[])[host.Hwnd, drop.Hwnd])
        {
            if (window != HWND.NULL)
            {
                _ = KillTimer(window, BlinkTimer);
                _ = KillTimer(window, LitTimer);
                _ = KillTimer(window, ReorderTimer);
                if (GetCapture() == window)
                {
                    _ = ReleaseCapture();
                }
            }
        }
        filter.Show("");
        filter.Blur();
        placed = placed is { } last ? last with { Content = null, Open = null } : null;
        // §536: the looks' samples, their widgets and bars, go with edit mode.
        previews?.Dispose();
        previews = null;
        tip.Hide();
        sights = [];
        seeThrough.Clear();
        drop.Hide();
        host.Hide();
    }

    /// <summary>
    /// §652: the add list's search says <paramref name="words"/>, the caret at its end and the keys in it, what matches listed —
    /// a widget Windows search found, to add: as if typed. Nothing where the add list's flyout is not the one open.
    /// </summary>
    internal void Search(string words)
    {
        if (placed?.Open != PanelPage.Add)
        {
            return;
        }
        filter.Blur();
        filter.Show(words);
        filter.Focus(selectAll: false);
        focus = new PanelHit(PanelHitKind.Filter);
        scroll = 0;
        Blink();
        Open();
        Changed?.Invoke();
    }

    /// <summary>
    /// §520: <paramref name="page"/>'s flyout shows, §575 its entry's, scrolled to its row at <paramref name="key"/> (its
    /// expanders open) and lit a moment: a link's landing, a way in from outside.
    /// </summary>
    internal void Land(PanelPage page, string? key, bool light = true)
    {
        Memory.Show(page);
        if (placed is { } at && at.Open != page)
        {
            Opening(page);
            placed = at with { Open = page };
        }
        scroll = 0;
        if (key is not null && placed?.Content?.Of(page) is { } view && PanelRows.PathTo(view.Rows, key) is { } path)
        {
            // §594: a level landed on opens too, its rows in sight (Public holidays, Karpo Pro: a link shows what it stands for).
            Memory.Open(PanelRows.All(view.Rows).Any(row => row.Key == key && row.Kind == PanelRowKind.Expander) ? [.. path, key] : path);
            Open();
            if (layout?.LineOf(key) is { } line)
            {
                scroll = MathF.Max(0, line.Rect.Y - layout.P(PanelMetrics.Gutter));
                // §628: the keys, where they are in use, on what was landed at (the layout a preset made: its head).
                if (keys && layout.Controls(line).Where(each => each.Hit.Key == key).OrderBy(each => each.Hit.Kind == PanelHitKind.Head ? 0 : 1).FirstOrDefault().Hit is { IsNone: false } there)
                {
                    focus = there;
                }
            }
            if (light)
            {
                lit = key;
                _ = SetTimer(host.Hwnd, LitTimer, LitMs, null);
            }
        }
        Open();
        Changed?.Invoke();
    }

    /// <summary>
    /// §520 decision 4: Escape's step in the bar and its flyout: §521 a box being typed in says what it said again and lets the
    /// keys go; else §575 the add list's search's words go; whether there was any.
    /// </summary>
    internal bool ClearSearch()
    {
        if (editing is { } typing)
        {
            editing = null;
            StopBlink();
            // §670: Escape in a picker's search takes its words, every choice shown again.
            if (searches.Remove(typing.Hit.Key))
            {
                if (lookups.TryGetValue(typing.Hit.Key, out var lookup))
                {
                    _ = lookup.Typed("", Environment.TickCount64);
                    FindLater();
                }
                Open();
            }
            else
            {
                Paint();
            }
            return true;
        }
        if (filter.Focused && filter.Text.Length > 0)
        {
            _ = filter.Clear();
            Open();
            return true;
        }
        return false;
    }

    /// <summary>§575: whether there are words for Escape to take first (<see cref="ClearSearch"/>).</summary>
    internal bool HasWords => editing is not null || (filter.Focused && filter.Text.Length > 0);

    /// <summary>
    /// A message of the bar's window or §575 its flyout's (<paramref name="window"/>), which the dock routes here: hover and
    /// press, the wheel, the keys; what a click asks for, and whether the message was its.
    /// </summary>
    internal (EditDeckEvent Happened, bool Handled) Message(HWND window, uint msg, WPARAM wParam, LPARAM lParam)
    {
        var onFlyout = window == drop.Hwnd;
        var own = onFlyout ? drop : host;
        var happened = own.Message(msg, wParam, lParam);
        if (happened.Kind == FlyoutHostEventKind.DpiChanged && placed is { } last && Hwnd != HWND.NULL)
        {
            placed = last with { Scale = GetDpiForWindow(Hwnd) / 96f };
            Open();
            return default;
        }
        if (layout is not { } now)
        {
            return default;
        }
        // §551: a row being dragged into order has the pointer until it is let go (or called off).
        if (onFlyout && (reorder is not null || reorderArmed is not null) && msg is WM.WM_MOUSEMOVE or WM.WM_LBUTTONUP or WM.WM_CAPTURECHANGED or WM.WM_RBUTTONDOWN or WM.WM_TIMER
            && Reordered(now, msg, wParam, lParam) is { } reordered)
        {
            return (reordered, true);
        }
        PanelHit At(LPARAM at) => own.ToPanel(at, out var point) ? onFlyout ? now.HitIn(point) : now.HitAt(point) : PanelHit.None;
        switch (msg)
        {
            case WM.WM_MOUSEMOVE when mixed is not null && own.ToPanel(lParam, out var mixing):
                return (Mixing(now, mixing, FlyoutEventKind.Mix), true);
            case WM.WM_MOUSEMOVE:
                Hot(At(lParam));
                if (Rests(now) is { } resting)
                {
                    return (resting, true);
                }
                break;
            case WM.WM_MOUSELEAVE:
                if (onFlyout != PanelLayout.OnBar(hot.Kind))
                {
                    Hot(PanelHit.None);
                    if (Rests(now) is { } left)
                    {
                        return (left, true);
                    }
                }
                break;
            case WM.WM_LBUTTONDOWN:
            {
                if (!own.ToPanel(lParam, out var down))
                {
                    break;
                }
                var hit = onFlyout ? now.HitIn(down) : now.HitAt(down);
                // §521: a press off the box being typed in keeps its words, as a box losing the keys does, and goes on.
                if (editing is { } typed && hit != typed.Hit)
                {
                    Kept();
                }
                keys = false;
                if (hit.Kind == PanelHitKind.Filter)
                {
                    FilterPressed(now, down);
                    break;
                }
                // §525: a press on anything else takes the keys there, as a click on a control does (no ring): the add list's
                // search loses them; a dropdown's keys are its own (F4 opens it again).
                if (!hit.IsNone)
                {
                    focus = hit;
                    if (filter.Focused)
                    {
                        filter.Blur();
                        StopBlink();
                        Open();
                    }
                }
                pressed = hit;
                // §551: a press on a row put in order — its handle, its name, an expander's head — lifts it once moved past
                // the drag distance; let go before that, it is the click it was.
                reorderArmed = onFlyout && hit.Kind is PanelHitKind.Handle or PanelHitKind.Head or PanelHitKind.Page && now.LineAt(down) is { Row: { Drags: true, Greyed: false } } liftable
                    ? (liftable.Row.Key, down) : null;
                _ = SetCapture(window);
                // §670: a press on a colour's spectrum or track mixes there at once, and the drag goes on mixing.
                if (hit.Kind == PanelHitKind.Mix && now.Row(hit.Key) is { } body
                    && body.Item.Children?.FirstOrDefault(kid => kid.Id == hit.Id)?.Control is FlyoutMix start)
                {
                    mixed = (hit, start.Color, start.Color, ColorMix.ToHsv(start.Color));
                    return (Mixing(now, down, FlyoutEventKind.Mix), true);
                }
                Paint();
                break;
            }
            case WM.WM_LBUTTONUP when mixed is not null:
            {
                pressed = PanelHit.None;
                if (GetCapture() == window)
                {
                    _ = ReleaseCapture();
                }
                return (MixDone(FlyoutEventKind.MixKept), true);
            }
            case WM.WM_LBUTTONUP:
            {
                var was = pressed;
                pressed = PanelHit.None;
                if (GetCapture() == window)
                {
                    _ = ReleaseCapture();
                }
                var up = At(lParam);
                hot = up;
                if (was.IsNone || up != was)
                {
                    Paint();
                    break;
                }
                return (Activate(up), true);
            }
            case WM.WM_CAPTURECHANGED when mixed is not null && (HWND)(nint)lParam != window:
                pressed = PanelHit.None;
                return (MixDone(FlyoutEventKind.MixUndone), true);
            case WM.WM_CAPTURECHANGED:
                if (!pressed.IsNone && (HWND)(nint)lParam != window)
                {
                    pressed = PanelHit.None;
                    Paint();
                }
                break;
            case WM.WM_MOUSEWHEEL when now.IsOpen:
            {
                var by = -(short)(((nint)wParam >> 16) & 0xffff) / 120f * now.P(PanelMetrics.OneLine + PanelMetrics.Tight) * 2;
                // §682: over a picker that scrolls, the picker, till it is at its end; then the page.
                var screen = new POINT { x = (short)((nint)lParam & 0xffff), y = (short)(((nint)lParam >> 16) & 0xffff) };
                if (drop.ToPanel(screen, out var over) && now.ScrollerAt(over) is { } picker)
                {
                    var was = now.PickerScroll(picker);
                    var next = Math.Clamp(was + by, 0, now.PickerOverflow(picker));
                    if (next != was)
                    {
                        pickerScrolls[picker.Row.Key] = next;
                        Open();
                        return (default, true);
                    }
                }
                ScrollBy(by);
                return (default, true);
            }
            case WM.WM_KEYDOWN or WM.WM_SYSKEYDOWN:
                return Key((int)(nuint)wParam, LightDismiss.AltOf(msg, lParam));
            case WM.WM_CHAR when editing is { } typing:
                if (typing.Field.Char((char)(nuint)wParam) == FieldKey.Edited)
                {
                    typing.Field.CaretOn = true;
                    Edited(typing);
                }
                return (default, true);
            case WM.WM_CHAR when filter.Focused:
                if (filter.Char((char)(nuint)wParam) == FieldKey.Edited)
                {
                    scroll = 0;
                    Blink();
                    Open();
                }
                return (default, true);
            case WM.WM_CHAR:
                return (default, true);
            case WM.WM_TIMER when (nuint)wParam == BlinkTimer:
                if (editing is { } blinking)
                {
                    blinking.Field.CaretOn = !blinking.Field.CaretOn;
                }
                else if (filter.Focused)
                {
                    filter.CaretOn = !filter.CaretOn;
                }
                Paint();
                return (default, true);
            case WM.WM_TIMER when (nuint)wParam == RevealTimer:
                Revealing();
                return (default, true);
            case WM.WM_TIMER when (nuint)wParam == FindTimer:
                Finding();
                return (default, true);
            case WM.WM_TIMER when (nuint)wParam == LitTimer:
                _ = KillTimer(window, LitTimer);
                lit = null;
                Paint();
                return (default, true);
        }
        return default;
    }

    /// <summary>§575: a press on the add list's search: it takes the keys, its caret where the press was; on its ✕, its words go.</summary>
    private void FilterPressed(PanelLayout now, Vector2 down)
    {
        var look = PanelPainter.FilterLook(now);
        var (x, y) = (down.X - now.Filter.X, down.Y - now.Filter.Y);
        if (filter.OnClear(look, x, y))
        {
            _ = filter.Clear();
        }
        else
        {
            filter.Focus(selectAll: false);
            var at = placed!;
            using var typeface = Wearing(at);
            filter.Editing.MoveTo(filter.IndexAt(at.Gfx, look, x), extend: KeyState.Down(VK.VK_SHIFT));
        }
        focus = new PanelHit(PanelHitKind.Filter);
        Blink();
        Open();
    }

    /// <summary>The caret of the field with the keys on and blinking (the system's rate).</summary>
    private void Blink()
    {
        filter.CaretOn = true;
        _ = KillTimer(host.Hwnd, BlinkTimer);
        if (filter.Focused || editing is not null)
        {
            _ = SetTimer(host.Hwnd, BlinkTimer, GetCaretBlinkTime() is var rate and > 0 and < 10_000 ? rate : 530u, null);
        }
    }

    private void StopBlink() => _ = KillTimer(host.Hwnd, BlinkTimer);

    /// <summary>
    /// A key (§520 decision 11): §575 the add list's search's, while it has the keys; Tab and Shift+Tab from one thing to the
    /// next — the bar's entries and buttons, then the flyout's —; Left and Right along the bar; Down from an open entry into
    /// its flyout; Space and Enter act (an entry opens its flyout). Escape is edit mode's (Dock.EscapeHeard), from the inside
    /// out. §525: F4, Alt+Down and Alt+Up open the
    /// popup of the thing that has the keys, as a ComboBox's (<paramref name="alt"/>: Alt held).
    /// </summary>
    private (EditDeckEvent, bool) Key(int key, bool alt = false)
    {
        if (layout is not { } now)
        {
            return default;
        }
        // §689: an inline picker's head the same: its body opened or shut, as a ComboBox's keys open its list.
        if (LightDismiss.TogglesDrop(key, alt) && (Drops(focus) || (focus.Kind == PanelHitKind.Head && now.Row(focus.Key) is { Kind: PanelRowKind.Picker })))
        {
            keys = true;
            return (Activate(focus), true);
        }
        var shift = KeyState.Down(VK.VK_SHIFT);
        var control = KeyState.Down(VK.VK_CONTROL);
        // §521: a box being typed in has the keys: Enter keeps its words, Escape puts back what it said (EditKeys' step, and
        // here), Tab keeps them and goes on; the rest are its own.
        if (editing is { } typing)
        {
            switch (key)
            {
                case VK.VK_RETURN:
                    return (Kept(), true);
                case VK.VK_ESCAPE:
                    _ = ClearSearch();
                    return (default, true);
                case VK.VK_TAB:
                    Kept();
                    break;
                default:
                    var said = typing.Field.Key(key, shift, control, () => Clipboard.GetText(Hwnd), words => Clipboard.SetText(Hwnd, words));
                    if (said != FieldKey.None)
                    {
                        typing.Field.CaretOn = true;
                        if (said == FieldKey.Edited)
                        {
                            Edited(typing);
                        }
                        else
                        {
                            Paint();
                        }
                    }
                    return (default, true);
            }
        }
        // §628: a page under another's entry goes back by Backspace and Alt+Left, as Windows' Settings and File Explorer go back.
        if (!now.BackHead.IsEmpty && (key == VK.VK_BACK || (alt && key == VK.VK_LEFT)))
        {
            keys = true;
            return (Activate(new PanelHit(PanelHitKind.Back)), true);
        }
        if (key == VK.VK_TAB)
        {
            var stops = now.Stops();
            var at = stops.IndexOf(focus);
            var next = stops.Count == 0 ? PanelHit.None : stops[((at < 0 ? (shift ? 0 : -1) : at) + (shift ? -1 : 1) + stops.Count) % stops.Count];
            Focus(next);
            return (default, true);
        }
        // §575: the add list's search: Enter adds the one widget that matches, Down goes into the list; the rest its own.
        if (filter.Focused)
        {
            if (key == VK.VK_RETURN && now.Shown is { } add && AddList.Single([.. add.RowsFor(filter.Text).Where(row => row.Kind == PanelRowKind.Pick).Select(row => row.Item)]) is { } only)
            {
                keys = true;
                return (Activate(new PanelHit(PanelHitKind.Act, only, Key: add.RowsFor(filter.Text).First(row => row.Item.Id == only).Key)), true);
            }
            if (key == VK.VK_DOWN)
            {
                var stops = now.Stops();
                var at = stops.IndexOf(new PanelHit(PanelHitKind.Filter));
                if (at >= 0 && at + 1 < stops.Count)
                {
                    Focus(stops[at + 1]);
                }
                return (default, true);
            }
            var said = filter.Key(key, shift, control, () => Clipboard.GetText(Hwnd), words => Clipboard.SetText(Hwnd, words));
            if (said is FieldKey.Edited)
            {
                scroll = 0;
            }
            if (said is FieldKey.Edited or FieldKey.Moved)
            {
                Blink();
                Open();
            }
            return (default, said != FieldKey.None || key is not VK.VK_ESCAPE);
        }
        // §575: Left and Right along the bar's entries and buttons, as a command bar's; Down from an open entry into its flyout.
        if (PanelLayout.OnBar(focus.Kind))
        {
            var along = now.Stops().Where(stop => PanelLayout.OnBar(stop.Kind)).ToList();
            if (key is VK.VK_LEFT or VK.VK_RIGHT && along.IndexOf(focus) is var at and >= 0)
            {
                var forward = (key == VK.VK_RIGHT) != Mirror.On;
                Focus(along[Math.Clamp(at + (forward ? 1 : -1), 0, along.Count - 1)]);
                return (default, true);
            }
            if (key == VK.VK_DOWN && now.Opened(focus) && now.Stops().FirstOrDefault(stop => !PanelLayout.OnBar(stop.Kind)) is { IsNone: false } inside)
            {
                Focus(inside);
                return (default, true);
            }
        }
        // §592: Left and Right along a tab strip, Home and End to its ends, choosing as they go (WinUI's SelectorBar).
        if (focus.Kind == PanelHitKind.Tab && key is VK.VK_LEFT or VK.VK_RIGHT or VK.VK_HOME or VK.VK_END && now.TabsAlong(focus) is { Count: > 0 } tabs)
        {
            var at = tabs.IndexOf(focus);
            var chosen = key switch
            {
                VK.VK_HOME => 0,
                VK.VK_END => tabs.Count - 1,
                _ => Math.Clamp(at + ((key == VK.VK_RIGHT) != Mirror.On ? 1 : -1), 0, tabs.Count - 1),
            };
            keys = true;
            if (chosen != at)
            {
                _ = Activate(tabs[chosen]);
                Focus(tabs[chosen]);
            }
            return (default, true);
        }
        // §551: Ctrl+Up and Ctrl+Down on any control of a row put in order move it a place among its own (Karpo's menus' keys
        // for it: dock.edit.faceDrag); a screen reader told where it stands once it does.
        if (reorder is not null && key == VK.VK_ESCAPE)
        {
            _ = CancelReorder();
            return (default, true);
        }
        if (!focus.IsNone && now.Row(focus.Key) is { Drags: true, Greyed: false } moved && now.PlaceOf(moved.Key) is { } place
            && PanelReorder.Stepped(key, control, place.At, place.Count) is { } to)
        {
            keys = true;
            announce = true;
            Activated = focus;
            return (new EditDeckEvent(EditDeckAction.Reorder, Id: moved.Item.Id, Page: placed?.Open ?? default, Key: moved.Key, To: to), true);
        }
        if (key is VK.VK_SPACE or VK.VK_RETURN && !focus.IsNone)
        {
            keys = true;
            return (Activate(focus), true);
        }
        if (key is VK.VK_PRIOR or VK.VK_NEXT && now.IsOpen)
        {
            ScrollBy((key == VK.VK_NEXT ? 1 : -1) * now.Viewport.Height * 0.9f);
            return (default, true);
        }
        if (key is VK.VK_HOME or VK.VK_END && now.IsOpen && !PanelLayout.OnBar(focus.Kind))
        {
            ScrollBy(key == VK.VK_END ? now.MostScroll : -now.MostScroll);
            return (default, true);
        }
        return default;
    }

    /// <summary>The keys on <paramref name="hit"/>: its focus ring shows, and the page scrolls it into sight.</summary>
    private void Focus(PanelHit hit)
    {
        focus = hit;
        keys = true;
        // A screen reader follows the keys, in the window the thing stands in.
        Uia.FocusMoved(PanelLayout.OnBar(hit.Kind) ? host.Hwnd : drop.Hwnd);
        if (hit.Kind == PanelHitKind.Filter)
        {
            filter.Focus(selectAll: true);
        }
        else
        {
            filter.Blur();
        }
        Blink();
        if (layout is { } now && !hit.IsNone && now.LineOf(hit.Key) is { } line)
        {
            var view = now.Viewport.Height;
            if (line.Rect.Y < now.Scroll)
            {
                scroll = line.Rect.Y - now.P(PanelMetrics.Gap);
            }
            else if (line.Rect.Bottom > now.Scroll + view)
            {
                scroll = line.Rect.Bottom - view + now.P(PanelMetrics.Gap);
            }
        }
        // §575: the keys go to the window the thing stands in.
        Surfaces.Focus(PanelLayout.OnBar(hit.Kind) ? host.Hwnd : drop.Hwnd);
        Open();
    }

    /// <summary>
    /// §671: the page scrolled, gliding (<see cref="RevealTimer"/>), till the card keyed <paramref name="key"/> stands whole in
    /// sight, a gap off the viewport's ends — its top where it is taller than the viewport; nothing where it is in sight.
    /// </summary>
    private void Reveal(string key)
    {
        if (layout is not { } now || !now.Spans.TryGetValue(key, out var span))
        {
            return;
        }
        var gap = now.P(PanelMetrics.Gap);
        var view = now.Viewport.Height;
        var to = span.Bottom + gap > now.Scroll + view ? MathF.Min(span.Y - gap, span.Bottom + gap - view)
            : span.Y - gap < now.Scroll ? span.Y - gap
            : now.Scroll;
        to = Math.Clamp(to, 0, now.MostScroll);
        if (MathF.Abs(to - now.Scroll) < 1)
        {
            return;
        }
        if (placed?.Look.Animations != true)
        {
            scroll = to;
            Open();
            return;
        }
        reveal = (now.Scroll, to, Environment.TickCount64);
        _ = SetTimer(drop.Hwnd, RevealTimer, 16, null);
    }

    /// <summary>§671: a frame of the glide: where it eases to now (a cubic ease-out), done once its time is up.</summary>
    private void Revealing()
    {
        if (reveal is not { } glide)
        {
            _ = KillTimer(drop.Hwnd, RevealTimer);
            return;
        }
        var t = Math.Clamp((Environment.TickCount64 - glide.Since) / RevealMs, 0, 1);
        var eased = 1 - MathF.Pow(1 - t, 3);
        scroll = glide.From + ((glide.To - glide.From) * eased);
        if (t >= 1)
        {
            reveal = null;
            _ = KillTimer(drop.Hwnd, RevealTimer);
        }
        Open();
    }

    private void ScrollBy(float by)
    {
        // §671: the person's own scrolling takes over from a glide.
        if (reveal is not null)
        {
            reveal = null;
            _ = KillTimer(drop.Hwnd, RevealTimer);
        }
        if (layout is not { } now)
        {
            return;
        }
        var next = Math.Clamp(now.Scroll + by, 0, now.MostScroll);
        if (next != now.Scroll)
        {
            scroll = next;
            Open();
        }
    }

    /// <summary>
    /// §575: whether a click at <paramref name="screen"/> gives it the keys: anywhere on its flyout; on the bar, an entry or
    /// Add, which have the keys next.
    /// </summary>
    internal bool TakesKeysAt(POINT screen) =>
        layout is { } now
        && ((drop.IsOpen && drop.ToPanel(screen, out var inside) && !now.HitIn(inside).IsNone)
            || (host.ToPanel(screen, out var point) && now.HitAt(point).Kind is PanelHitKind.Nav or PanelHitKind.Add));

    /// <summary>What a press at <paramref name="lParam"/> of <paramref name="window"/> (a mouse message's) is on: what it may have opened a popup from (§430); null for nothing.</summary>
    internal object? PartAt(HWND window, LPARAM lParam)
    {
        if (layout is not { } now)
        {
            return null;
        }
        var hit = window == drop.Hwnd ? drop.ToPanel(lParam, out var inside) ? now.HitIn(inside) : PanelHit.None
            : host.ToPanel(lParam, out var point) ? now.HitAt(point) : PanelHit.None;
        return hit.IsNone ? null : hit;
    }

    /// <summary>What a click, a key or a screen reader acted on last: what a popup it opened hangs from (§430).</summary>
    internal PanelHit Activated { get; private set; }

    /// <summary>§541: what the keys are on (the tests').</summary>
    internal PanelHit Focused => focus;

    /// <summary>Whether <paramref name="hit"/> opens a popup under it: a dropdown, a colour, a field's box, a button with a level of its own.</summary>
    private bool Drops(PanelHit hit) =>
        hit.Kind == PanelHitKind.Pop
        || hit.Kind == PanelHitKind.Tool && layout?.Row(hit.Key) is { } row && hit.Index >= 0 && hit.Index < row.Tools.Count && row.Tools[hit.Index].Level is not null;

    /// <summary>What a click (or Space, Enter, a screen reader's Invoke) on <paramref name="hit"/> does: some things the bar does itself, the rest the dock.</summary>
    internal EditDeckEvent Activate(PanelHit hit)
    {
        var page = placed?.Open ?? default;
        Activated = hit;
        if (keys && PanelLayout.OnBar(hit.Kind))
        {
            focus = hit;
        }
        switch (hit.Kind)
        {
            // §575: an entry opens its flyout, or closes it again; Add its list, where it has somewhere to add to.
            case PanelHitKind.Nav or PanelHitKind.Add when layout?.Opened(hit) == true:
                return new EditDeckEvent(EditDeckAction.Close);
            case PanelHitKind.Nav:
                return new EditDeckEvent(EditDeckAction.Open, Page: hit.Page);
            case PanelHitKind.Add when placed is { State.CanAdd: true }:
                return new EditDeckEvent(EditDeckAction.Open, Page: PanelPage.Add);
            // §689: the errors a page of the flyout, as every page.
            case PanelHitKind.Errors:
                return new EditDeckEvent(EditDeckAction.Open, Page: PanelPage.Errors);
            case PanelHitKind.Done:
                return new EditDeckEvent(EditDeckAction.Done);
            case PanelHitKind.Filter:
                Focus(hit);
                return default;
            // §628: a page under another's entry goes back to that entry's page.
            case PanelHitKind.Back when placed?.Open is { } under:
                return new EditDeckEvent(EditDeckAction.Open, Page: PanelRows.EntryOf(under));
            case PanelHitKind.Head:
                Memory.Toggle(hit.Key);
                Open();
                if (Memory.IsOpen(hit.Key))
                {
                    Reveal(hit.Key);
                }
                return default;
            // §592: a tab chosen, kept for the run of the app; the keys, on its strip, go with it.
            case PanelHitKind.Tab:
                Memory.Open([hit.Key]);
                if (focus.Kind == PanelHitKind.Tab)
                {
                    focus = hit;
                }
                Open();
                return default;
            // §529: a mixed row's widgets' own values shown or hidden under it.
            case PanelHitKind.Each:
                Memory.Toggle(PanelGroups.EachOf(hit.Key));
                Open();
                return default;
            // §682: a dropdown whose picker unfolds under its line: opened there, or shut.
            case PanelHitKind.Pop when layout?.Row(hit.Key) is { } inline && PanelPickers.Inline(inline):
                Memory.Toggle(hit.Key);
                Open();
                if (Memory.IsOpen(hit.Key))
                {
                    Reveal(hit.Key);
                }
                return default;
            // §682: a button whose level is a picker (an account's colour): unfolded under its line, or folded.
            case PanelHitKind.Tool when layout?.Row(hit.Key) is { } tooled && hit.Index >= 0 && hit.Index < tooled.Tools.Count
                && tooled.Folded.FirstOrDefault(folded => folded.Item.Id == tooled.Tools[hit.Index].Id) is { } folded:
                Memory.Toggle(folded.Key);
                Open();
                if (Memory.IsOpen(folded.Key))
                {
                    Reveal(folded.Rows[^1].Key);
                }
                return default;
            case PanelHitKind.Pop or PanelHitKind.Tool when Drops(hit):
                return new EditDeckEvent(EditDeckAction.Pop, hit.Id, page, hit.Key, OnScreen(hit));
            case PanelHitKind.Link:
                return new EditDeckEvent(EditDeckAction.Link, hit.Id, page, hit.Key);
            // §676: one of what a lookup found picked: kept as its field's (proved first where its finder proves).
            case PanelHitKind.Act when layout?.Row(hit.Key) is { Kind: PanelRowKind.PickerBody } body && PanelPickers.Lookup(body.Item.Children ?? []) is { } lookup:
                Picked(lookup, hit, page);
                return default;
            case PanelHitKind.Toggle or PanelHitKind.Fewer or PanelHitKind.More or PanelHitKind.Tool or PanelHitKind.Act or PanelHitKind.TitleAct:
                return new EditDeckEvent(EditDeckAction.Act, hit.Id, page, hit.Key);
            // §521: a text or a number box: typed in place, its words all chosen, the caret blinking in it.
            case PanelHitKind.Text or PanelHitKind.Number when editing?.Hit != hit && layout?.Row(hit.Key) is { } row:
            {
                var said = hit.Kind == PanelHitKind.Number ? row.Typed?.Text is { Length: > 0 } typed ? typed : row.Stepper?.Value ?? ""
                    : PanelPickers.Searches(row) ? searches.GetValueOrDefault(row.Key, "") : row.Typed?.Text ?? "";
                var field = new MenuTextField(said, row.Typed?.Longest ?? int.MaxValue, row.Typed?.Masked == true);
                field.Show(said);
                field.Focus(selectAll: true);
                editing = (hit, field, said);
                focus = hit;
                filter.Blur();
                Blink();
                Paint();
                return default;
            }
        }
        return default;
    }

    /// <summary>§521: the box being typed in changed its words: §670 a picker's search filters its choices as they are typed.</summary>
    private void Edited((PanelHit Hit, MenuTextField Field, string Original) typing)
    {
        if (layout?.Row(typing.Hit.Key) is { } row && PanelPickers.Searches(row))
        {
            searches[row.Key] = typing.Field.Text;
            // §676: a lookup's asked of its finder, a moment after the last key where it asks a service.
            if (lookups.TryGetValue(row.Key, out var lookup))
            {
                _ = lookup.Typed(typing.Field.Text, Environment.TickCount64);
                FindLater();
            }
            Open();
            return;
        }
        Paint();
    }

    /// <summary>
    /// §676: each lookup's search shown on <paramref name="laid"/> its own (<see cref="lookups"/>): made as it first shows, its
    /// finder's first ones offered; made again for a new finder (the setting changed) while nothing is typed in it.
    /// </summary>
    private bool LookUp(PanelLayout laid)
    {
        var made = false;
        foreach (var line in laid.Blocks.SelectMany(block => block.Lines))
        {
            if (!PanelPickers.LooksUp(line.Row) || line.Row.Typed?.Finder is not { } finder
                || (lookups.TryGetValue(line.Row.Key, out var had) && (ReferenceEquals(had.Finder, finder) || had.Query.Length > 0)))
            {
                continue;
            }
            had?.Close();
            var search = new MenuSearch(finder, answers.Enqueue, () => { });
            lookups[line.Row.Key] = search;
            search.Opened();
            FindLater();
            made = true;
        }
        return made;
    }

    /// <summary>§676: <see cref="FindTimer"/> running, for a lookup's answer or its wait.</summary>
    private void FindLater() => _ = SetTimer(drop.Hwnd, FindTimer, 33, null);

    /// <summary>§676: a lookup's answers in, a wait over; the page shown with them; the timer stopped once none is waited for.</summary>
    private void Finding()
    {
        var any = false;
        while (answers.TryDequeue(out var answer))
        {
            answer();
            any = true;
        }
        var now = Environment.TickCount64;
        foreach (var lookup in lookups.Values)
        {
            if (lookup.Phase == SearchPhase.Waiting && now >= lookup.DueAt)
            {
                lookup.Due(now);
                any = true;
            }
        }
        if (!lookups.Values.Any(lookup => lookup.Phase is SearchPhase.Waiting or SearchPhase.Searching or SearchPhase.Checking))
        {
            _ = KillTimer(drop.Hwnd, FindTimer);
        }
        if (any)
        {
            Open();
        }
    }

    /// <summary>
    /// §676: the found <paramref name="hit"/> of lookup <paramref name="field"/>'s list picked: kept as its field's
    /// (<see cref="EditDeckAction.Type"/> with it), proved first where its finder proves ("Looking up…" under the list
    /// meanwhile); its search's words gone, what it finds the setting's own again.
    /// </summary>
    private void Picked(FlyoutItem field, PanelHit hit, PanelPage page)
    {
        var key = PanelPickers.SearchOf(hit.Key);
        var rows = lookups.TryGetValue(key, out var search) ? search.Rows : PanelFinds.Before(((FlyoutField)field.Control!).Finder!).Rows;
        if (hit.Index < 0 || hit.Index >= rows.Count)
        {
            return;
        }
        void Keep(OptionFound found)
        {
            searches.Remove(key);
            if (lookups.Remove(key, out var done))
            {
                done.Close();
            }
            Invoked?.Invoke(new EditDeckEvent(EditDeckAction.Type, field.Id, page, key, Text: found.Title, Found: found));
        }
        if (search is null)
        {
            Keep(rows[hit.Index]);
            return;
        }
        search.Pick(rows[hit.Index], Keep);
        FindLater();
        Open();
    }

    /// <summary>§670: the colour the pointer at <paramref name="point"/> (panel px) mixes, worn (<paramref name="kind"/> Mix).</summary>
    private EditDeckEvent Mixing(PanelLayout now, Vector2 point, FlyoutEventKind kind)
    {
        if (mixed is not { } mixing || now.MixAt(mixing.Hit, point, mixing.Color, mixing.Hsv) is not { } next)
        {
            return default;
        }
        mixed = mixing with { Color = next.Color, Hsv = next.Hsv };
        Paint();
        return new EditDeckEvent(EditDeckAction.Mix, mixing.Hit.Id, placed?.Open ?? default, mixing.Hit.Key, Mixed: kind, Color: next.Color);
    }

    /// <summary>§670: the colour being mixed let go — kept (MixKept) — or called off — put back (MixUndone).</summary>
    private EditDeckEvent MixDone(FlyoutEventKind kind)
    {
        if (mixed is not { } done)
        {
            return default;
        }
        mixed = null;
        Paint();
        return new EditDeckEvent(EditDeckAction.Mix, done.Hit.Id, placed?.Open ?? default, done.Hit.Key, Mixed: kind, Color: kind == FlyoutEventKind.MixKept ? done.Color : done.Start);
    }

    /// <summary>
    /// §521: the box being typed in kept its words: they go to the dock as its field's (<see cref="EditDeckAction.Type"/>), as a
    /// menu's field keeps them; the keys leave it. Nothing where they are what it said. What it asked for.
    /// </summary>
    private EditDeckEvent Kept()
    {
        if (editing is not { } typing)
        {
            return default;
        }
        editing = null;
        StopBlink();
        Paint();
        // §670: a picker's search keeps its words in its box, filtering; nothing of the config's.
        if (layout?.Row(typing.Hit.Key) is { } searched && PanelPickers.Searches(searched))
        {
            return default;
        }
        if (typing.Field.Text == typing.Original)
        {
            return default;
        }
        var kept = new EditDeckEvent(EditDeckAction.Type, typing.Hit.Id, placed?.Open ?? default, typing.Hit.Key, Text: typing.Field.Text);
        Invoked?.Invoke(kept);
        return default;
    }

    /// <summary>
    /// §551: a message while a row put in order is pressed (<see cref="reorderArmed"/>) or dragged (<see cref="reorder"/>): a
    /// move past the system's drag distance lifts it, a move then takes it along, the others making room; near the page's top
    /// or foot the page scrolls under it (<see cref="ReorderTimer"/>); let go, it goes where it would land, one change
    /// (<see cref="EditDeckAction.Reorder"/>) — where it stood, nothing; a right press or the pointer lost put it back. Null
    /// where the message is not its: a press let go before it was lifted is a click.
    /// </summary>
    private EditDeckEvent? Reordered(PanelLayout now, uint msg, WPARAM wParam, LPARAM lParam)
    {
        switch (msg)
        {
            case WM.WM_MOUSEMOVE when drop.ToPanel(lParam, out var point):
                if (reorder is null && reorderArmed is { } armed)
                {
                    if (MathF.Abs(point.X - armed.At.X) <= GetSystemMetrics(SM.SM_CXDRAG) && MathF.Abs(point.Y - armed.At.Y) <= GetSystemMetrics(SM.SM_CYDRAG))
                    {
                        return default(EditDeckEvent);
                    }
                    reorderArmed = null;
                    reorder = now.DragOf(armed.Key, armed.At.Y - now.Viewport.Y + now.Scroll);
                    if (reorder is null)
                    {
                        return null;
                    }
                    pressed = hot = PanelHit.None;
                    tip.Hide();
                    _ = SetTimer(drop.Hwnd, ReorderTimer, 16, null);
                }
                if (reorder is { } dragged)
                {
                    reorderPointer = point;
                    reorder = dragged with { Y = point.Y - now.Viewport.Y + now.Scroll };
                    Paint();
                    return default(EditDeckEvent);
                }
                return null;
            case WM.WM_TIMER when (nuint)wParam == ReorderTimer:
            {
                if (reorder is not { } held)
                {
                    _ = KillTimer(drop.Hwnd, ReorderTimer);
                    return default(EditDeckEvent);
                }
                // Near the page's top or foot it scrolls, the faster the nearer, a row's height a second at most.
                var band = now.P(PanelMetrics.DragScrollBand);
                var y = reorderPointer.Y;
                var by = y < now.Viewport.Y + band ? -(now.Viewport.Y + band - y)
                    : y > now.Viewport.Bottom - band ? y - (now.Viewport.Bottom - band)
                    : 0;
                if (by != 0 && Math.Clamp(now.Scroll + (MathF.Min(band, MathF.Abs(by)) * MathF.Sign(by) / 3), 0, now.MostScroll) is var next && next != now.Scroll)
                {
                    scroll = next;
                    Open();
                    if (layout is { } scrolled)
                    {
                        reorder = held with { Y = y - scrolled.Viewport.Y + scrolled.Scroll };
                        Paint();
                    }
                }
                return default(EditDeckEvent);
            }
            case WM.WM_LBUTTONUP when reorder is { } done:
            {
                EndReorder();
                if (done.To == done.From)
                {
                    return default(EditDeckEvent);
                }
                Activated = new PanelHit(PanelHitKind.Handle, done.Id, Key: done.Key);
                return new EditDeckEvent(EditDeckAction.Reorder, Id: done.Id, Page: placed?.Open ?? default, Key: done.Key, To: done.To);
            }
            case WM.WM_LBUTTONUP:
                // Let go before it was lifted: the click it was.
                reorderArmed = null;
                return null;
            case WM.WM_RBUTTONDOWN when reorder is not null:
                _ = CancelReorder();
                return default(EditDeckEvent);
            case WM.WM_CAPTURECHANGED when (HWND)(nint)lParam != drop.Hwnd:
                _ = CancelReorder();
                return null;
        }
        return null;
    }

    /// <summary>§551: a row being dragged into order put back where it stood (Escape, a right press, the pointer lost); whether one was.</summary>
    internal bool CancelReorder()
    {
        var was = reorder is not null;
        reorderArmed = null;
        if (was)
        {
            EndReorder();
        }
        return was;
    }

    /// <summary>§551: the drag into order over: the pointer let go, the page drawn as it stands.</summary>
    private void EndReorder()
    {
        reorder = null;
        reorderArmed = null;
        pressed = PanelHit.None;
        _ = KillTimer(drop.Hwnd, ReorderTimer);
        if (GetCapture() == drop.Hwnd)
        {
            _ = ReleaseCapture();
        }
        Paint();
    }

    public void Dispose()
    {
        Uia.Forget(host.Hwnd);
        Uia.Forget(drop.Hwnd);
        previews?.Dispose();
        previews = null;
        Release(ref barDrawn);
        seeThrough.Dispose();
        Release(ref dropDrawn);
        tip.Dispose();
        drop.Dispose();
        host.Dispose();
    }

    private static void Release(ref (CompositionDrawingSurface Surface, SpriteVisual Sprite, CompositionSurfaceBrush Brush)? drawn)
    {
        if (drawn is { } made)
        {
            made.Sprite.Dispose();
            made.Brush.Dispose();
            made.Surface.Dispose();
        }
        drawn = null;
    }

    /// <summary>§541: whether <paramref name="kind"/> is a page's row's control (its key a row's), not the bar's, a suggestion.</summary>
    private static bool RowControl(PanelHitKind kind) => kind is PanelHitKind.Toggle or PanelHitKind.Fewer or PanelHitKind.More or PanelHitKind.Number
        or PanelHitKind.Pop or PanelHitKind.Text or PanelHitKind.Tool or PanelHitKind.Act or PanelHitKind.Head
        or PanelHitKind.Link or PanelHitKind.Each or PanelHitKind.Handle or PanelHitKind.Mix;

    /// <summary>
    /// How its host hangs it from <paramref name="spot"/>'s side, its margin off it: the window starts there (a Tool host keeps
    /// no shadow room on the anchor's side), so nothing of it is over what stands along that edge.
    /// </summary>
    private static FlyoutPlacement PlacementOf(EditDeckRoom room, EditDeckSpot spot) =>
        new(spot.Anchor, new RECT { left = room.Display.Left, top = room.Display.Top, right = room.Display.Right, bottom = room.Display.Bottom }, spot.Edge, GapDip: EditDeckPlan.MarginDip, Align: FlyoutAlign.Center, Margin: 0);

    /// <summary>§575: the layout of <paramref name="at"/>: the bar no wider than its display's room, the flyout no taller than <paramref name="tall"/> px.</summary>
    private PanelLayout LayOut(Placement at, float tall, bool bar = false)
    {
        using (Wearing(at))
        {
            PanelLayout Laid()
            {
                var finds = lookups.Count == 0 ? null : lookups.ToDictionary(each => each.Key, each => new PanelFinds(each.Value.Rows, each.Value.Said), StringComparer.Ordinal);
                var input = new PanelInput(at.State.Errors, at.Content, bar ? null : at.Open, Memory.Opened, scroll, at.State.CanAdd, Filter: filter.Text, Searches: searches, Finds: finds, Scrolls: pickerScrolls);
                return new PanelLayout(new GfxPanelText(at.Gfx, at.Look), at.Scale, input, new Vector2(EditDeckPlan.Widest(at.Room, at.Scale), tall));
            }
            var laid = Laid();
            // §682: a lookup's search made as it first shows answers at once where its finder asks no service: laid out again
            // with what it found, else the list stood empty till the next thing happened.
            return !bar && LookUp(laid) ? Laid() : laid;
        }
    }

    /// <summary>
    /// §359: where the edit bar showing <paramref name="state"/> and §575 <paramref name="content"/>'s entries stands on the
    /// screen, before it is shown — the sections' edit bars keep clear of it, each shown once where it stands.
    /// </summary>
    internal static RECT PanelFor(Gfx gfx, Look look, float scale, EditDeckRoom room, EditDeckState state, SettingsContent? content = null)
    {
        PanelLayout bar;
        using (gfx.Wearing(look.Typeface))
        {
            bar = new PanelLayout(new GfxPanelText(gfx, look), scale, new PanelInput(state.Errors, content, CanAdd: state.CanAdd), new Vector2(EditDeckPlan.Widest(room, scale), 0));
        }
        var size = bar.Size;
        var spot = EditDeckPlan.Place(room, (int)MathF.Ceiling(size.X), (int)MathF.Ceiling(size.Y), scale);
        return PlacementOf(room, spot).PanelAt((int)MathF.Ceiling(size.X), (int)MathF.Ceiling(size.Y), scale);
    }

    /// <summary>
    /// Lays it out and shows it: the bar at its side; §575 the flyout of the entry open hung from it (PopupPlace: under it, over
    /// it at the display's foot, §578 centred on it, pushed in only at the work area's side), as tall as its page and no taller
    /// than the room on its side; §593 the add page from the whole bar, as wide as it, no beak (the person, 2026-11-09: "i want
    /// the add menu to be shown on the top in the same width as the edit bar all the time"): none else.
    /// </summary>
    private void Open()
    {
        if (placed is not { } at)
        {
            return;
        }
        var (gfx, look, scale, room, _, _, open, clear) = at;
        // The bar first: where it stands is what the flyout hangs from.
        var bar = LayOut(at, 0, bar: true);
        spot = EditDeckPlan.Place(room, (int)MathF.Ceiling(bar.Size.X), (int)MathF.Ceiling(bar.Size.Y), scale);
        var barAt = PlacementOf(room, spot).PanelAt((int)MathF.Ceiling(bar.Size.X), (int)MathF.Ceiling(bar.Size.Y), scale);
        // What the flyout hangs from: the entry open; §593 the add page the whole bar, whatever opened it.
        var adds = open == PanelPage.Add;
        // §628: a page under another's entry (Add a layout's) hangs from that entry, as its page did: shown in its place.
        // §689: the errors' page hangs from the errors' indicator.
        var from = open is { } page && !adds
            ? bar.RectOf(page == PanelPage.Errors ? new PanelHit(PanelHitKind.Errors) : new PanelHit(PanelHitKind.Nav, Page: PanelRows.EntryOf(page)))
            : default;
        var control = adds ? barAt : from.IsEmpty ? default : HungFrom(barAt, from);
        var work = room.Inside;
        var workRect = new RECT { left = work.Left, top = work.Top, right = work.Right, bottom = work.Bottom };
        var none = control.right <= control.left;
        // §578 (the person, 2026-11-09: "the flyouts should be centered under the button"): centred on what it hangs from; §584
        // its beak pointing at it, across a gap. §593 the add page, as wide as the bar and centred on it, flush with its sides,
        // which keep their own margin (EditDeckPlan.Place); no beak — no one entry to point at.
        var hung = new FlyoutPlacement(new POINT { x = control.left, y = control.bottom }, workRect, spot.Edge, GapDip: Beak.GapDip, Margin: adds ? 0 : null, Control: control, Beak: !adds);
        int Px(float dip) => (int)MathF.Round(dip * scale, MidpointRounding.AwayFromZero);
        var tall = none ? 0 : MathF.Min(PopupPlace.Room(control, workRect, Px(Beak.GapDip), Px(hung.MarginDip)), MathF.Floor(work.Height * FlyoutPlacement.MostShare));
        // §593: the add page into an open folder stops short of it, its "+" and where its ghost stands in sight — where that
        // leaves it a page's worth (AddLeast); over it else.
        if (adds && clear is { } folder)
        {
            tall = AddTall(tall, control, folder, spot.Edge, Px(Beak.GapDip), Px(AddLeastDip));
        }
        var next = none ? bar : LayOut(at, MathF.Max(1, tall));
        scroll = next.Scroll;
        // §541: the keys on a control gone with the content made again (a reset, gone once its value is reset): on its row's
        // first control.
        if (next.IsOpen && !focus.IsNone && RowControl(focus.Kind) && !next.Stops().Contains(focus) && next.LineOf(focus.Key) is { } stays
            && next.Controls(stays).Select(each => each.Hit).Where(each => !each.IsNone).ToList() is [var first, ..] there)
        {
            // §551: a row moved by the keys keeps them on the same control of it (its handle, its Remove).
            focus = there.FirstOrDefault(each => each.Kind == focus.Kind && each.Index == focus.Index) is { IsNone: false } same ? same : first;
        }
        if (announce && next.IsOpen)
        {
            // §551: where it stands now, said (its handle's status, "3 of 7").
            announce = false;
            Uia.FocusMoved(drop.Hwnd);
        }
        layout = next;
        var coming = !host.IsOpen;
        // §363: shown again while it stands, its content stays — the one surface is drawn again in place.
        host.Show(gfx, look, scale, next.Size, PlacementOf(room, spot), keep: !coming);
        Place(host, ref barDrawn, gfx, next.Size);
        var flyout = next.FlyoutSize;
        if (flyout.X > 0 && flyout.Y > 0)
        {
            // A flyout shown again from where it hung stands as it stands; from another entry it comes in afresh.
            var same = drop.IsOpen && dropFrom == control;
            dropFrom = control;
            drop.Show(gfx, look, scale, flyout, hung, keep: same);
            Place(drop, ref dropDrawn, gfx, flyout);
        }
        else if (drop.IsOpen)
        {
            dropFrom = default;
            previews?.Dispose();
            previews = null;
            seeThrough.Clear();
            drop.Hide();
        }
        // §650: a page whose field has the keys as it opens (Opening) takes them to its window, once; drawn again, never.
        if (takeKeys)
        {
            takeKeys = false;
            drop.TakeKeys();
        }
        Paint();
        if (coming && host.Entered)
        {
            // It comes into sight rising from its side and fading in (§310, §489: the host's own), not when drawn again in place.
            Entrances++;
        }
        Changed?.Invoke();
    }

    /// <summary>
    /// §593: the least the add page is made, short of an open folder it adds into. §698 (the person, 2026-11-13: "sometimes the
    /// add widget flyout is very short in height ... when i open it via the folder column add button"): its search and four
    /// rows of tiles, where it was two.
    /// </summary>
    internal const float AddLeastDip = 360;

    /// <summary>
    /// §593, §698: how tall the add page hung from <paramref name="control"/> (the edit bar, at <paramref name="edge"/>) is made, of
    /// <paramref name="tall"/>, with <paramref name="folder"/> (screen px) open, which it adds into: short of the folder, a
    /// <paramref name="gap"/> from both, where that leaves it <paramref name="least"/> and half of its room at least; over it
    /// else. §698: only a folder under it — one beside it (a column at the display's side) takes nothing of it. Pure.
    /// </summary>
    internal static float AddTall(float tall, RECT control, RECT folder, Edge edge, int gap, int least)
    {
        if (folder.bottom <= folder.top || folder.right <= control.left || folder.left >= control.right)
        {
            return tall;
        }
        var free = edge == Edge.Bottom ? control.top - gap - (folder.bottom + gap) : folder.top - gap - (control.bottom + gap);
        return free >= least && free >= tall / 2 ? MathF.Min(tall, free) : tall;
    }

    /// <summary>
    /// §584: what an entry's flyout hangs from (screen px): the entry <paramref name="entry"/> (bar px) across, the whole bar
    /// <paramref name="bar"/> (screen px) down — so the flyout keeps its gap from the bar's edge, not from the entry inside it,
    /// which it stood 4 DIP over; its beak over the entry's middle.
    /// </summary>
    internal static RECT HungFrom(RECT bar, RectF entry) => new()
    {
        left = bar.left + (int)entry.X,
        top = bar.top,
        right = bar.left + (int)MathF.Ceiling(entry.Right),
        bottom = bar.bottom,
    };

    /// <summary>The surface of <paramref name="on"/>'s content, <paramref name="size"/> px, made again where its size changed, standing in its window.</summary>
    private static void Place(FlyoutHost on, ref (CompositionDrawingSurface Surface, SpriteVisual Sprite, CompositionSurfaceBrush Brush)? drawn, Gfx gfx, Vector2 size)
    {
        if (drawn is not { } made || made.Sprite.Size != size)
        {
            Release(ref drawn);
            var surface = gfx.Draw(MathF.Max(1, size.X), MathF.Max(1, size.Y), _ => { });
            var brush = gfx.Compositor.CreateSurfaceBrush(surface);
            var sprite = gfx.Compositor.CreateSpriteVisual();
            sprite.Brush = brush;
            sprite.Size = size;
            drawn = made = (surface, sprite, brush);
        }
        if (made.Sprite.Parent != on.Content)
        {
            try
            {
                made.Sprite.Parent?.Children.Remove(made.Sprite);
            }
            catch (Exception error) when (error is ObjectDisposedException or COMException)
            {
                // The window it stood in was let go of (Alt+F4): its tree with it.
            }
            on.Content.Children.InsertAtTop(made.Sprite);
        }
    }

    /// <summary>The bar's surface and the flyout's drawn again from the layout, the pointer's and the keys' marks on them.</summary>
    private void Paint()
    {
        if (layout is not { } now || placed is not { } at)
        {
            return;
        }
        var painter = new PanelPainter(at.Gfx, host.Theme, now, new PanelMarks(hot, pressed, focus, lit, keys, reorder, mixed is { } mixing ? (mixing.Hit.Key, mixing.Color, mixing.Hsv) : null), editing is { } typing ? (typing.Hit.Key, typing.Field) : null);
        using (Wearing(at))
        {
            if (barDrawn is { } bar)
            {
                at.Gfx.Redraw(bar.Surface, dc => painter.PaintBar(dc));
            }
            if (drop.IsOpen && dropDrawn is { } flyout)
            {
                at.Gfx.Redraw(flyout.Surface, dc => painter.PaintFlyout(dc, filter, ground: false));
            }
        }
        ShowSeeThrough();
        // §536: the looks' samples over the flyout where their rooms are, in the same commit: drawn once a look, moved as it scrolls.
        if (drop.IsOpen && dropDrawn is { } shown && now.IsOpen)
        {
            previews ??= new LookPreviews(at.Gfx);
            previews.Place(drop.Content, shown.Sprite, now, at.Look, Wallpaper?.Invoke());
        }
        Propose();
    }

    /// <summary>
    /// §593: the add page's pick the dock shows a ghost of (its command; 0 for none), as it changes: the tile under the pointer,
    /// else the one the keys are on — a widget or a ready-made group, not greyed; a folder's head is none.
    /// </summary>
    internal Action<uint>? Proposing { get; set; }

    /// <summary>§593: the pick last said (<see cref="Proposing"/>).</summary>
    private uint proposed;

    /// <summary>§593, tests: the add page's pick a ghost is shown of; 0 for none.</summary>
    internal uint Proposed => proposed;

    private void Propose()
    {
        var pick = drop.IsOpen && placed?.Open == PanelPage.Add && layout is { IsOpen: true } now
            ? PickOf(now, hot) is var under and not 0 ? under : keys ? PickOf(now, focus) : 0
            : 0;
        if (pick == proposed)
        {
            return;
        }
        proposed = pick;
        Proposing?.Invoke(pick);
    }

    /// <summary>§593: the add page's pick <paramref name="hit"/> is on — a tile, not greyed —; 0 for none.</summary>
    internal static uint PickOf(PanelLayout now, PanelHit hit) =>
        hit.Kind == PanelHitKind.Act && now.Row(hit.Key) is { Kind: PanelRowKind.Pick, Greyed: false } ? hit.Id : 0;

    /// <summary>§536: the looks' samples over the page (LookPreviews); made as the first one shows.</summary>
    private LookPreviews? previews;

    /// <summary>
    /// §629 (the person, 2026-11-09: "the add widget modal should be semi transparent in case the ghost preview is directly behind
    /// it. but it should only be semi transparent for the ghost preview and not for everything actually."): the add page's ghost
    /// — on a bar, a ghost folder's lines, a line of the folder it adds into — where it stands now; the page lets it show through
    /// wherever it runs under it (SeeThroughView), nothing else. The dock's, as the ghost changes.
    /// </summary>
    internal void SeeThrough(IReadOnlyList<GhostSight> ghosts)
    {
        sights = ghosts;
        ShowSeeThrough();
    }

    /// <summary>§629: the flyout's ground, and over it the add page's ghost where it runs under the page; nothing while none is open.</summary>
    private void ShowSeeThrough()
    {
        if (!drop.IsOpen || dropDrawn is not { } shown || placed is not { } at)
        {
            seeThrough.Clear();
            return;
        }
        seeThrough.Place(at.Gfx.Compositor, drop.Content, shown.Sprite, shown.Sprite.Size, host.Theme.Ground, drop.PanelOnScreen, at.Open == PanelPage.Add ? sights : []);
    }

    /// <summary>Tests only: §629 where the page shows its ghost through now (screen px), each piece; none for none.</summary>
    internal IReadOnlyList<RECT> SeeThroughOnScreen
    {
        get
        {
            if (!drop.IsOpen)
            {
                return [];
            }
            var page = drop.PanelOnScreen;
            return [.. seeThrough.Shown.Select(cut => new RECT
            {
                left = page.left + (int)MathF.Floor(cut.At.X),
                top = page.top + (int)MathF.Floor(cut.At.Y),
                right = page.left + (int)MathF.Ceiling(cut.At.Right),
                bottom = page.top + (int)MathF.Ceiling(cut.At.Bottom),
            })];
        }
    }

    /// <summary>§613: what the desktop of the display this deck stands on shows, which a look's sample is drawn over; null for none known.</summary>
    internal Func<WallpaperSource?>? Wallpaper { get; set; }

    /// <summary>§536, tests: the looks' samples shown.</summary>
    internal LookPreviews? Previews => previews;

    /// <summary>
    /// §526: the font the bar and its flyout are measured and drawn in while the scope handed back stands: their look's
    /// (Windows' own, Segoe UI Variable: Dock.SettingsLook), never what Karpo's chrome is in (Gfx.Typeface, the default layout's menus').
    /// </summary>
    private static Gfx.TypefaceScope Wearing(Placement at) => at.Gfx.Wearing(at.Look.Typeface);

    /// <summary>§684: the choice of an inline picker last said to rest under the pointer (<see cref="EditDeckAction.Hover"/>); 0 for none.</summary>
    private uint resting;

    /// <summary>
    /// §684: <see cref="EditDeckAction.Hover"/> where the choice under the pointer is another than last said: a choice of an
    /// inline picker's body (not a lookup's find, which is no setting's value till it is picked), else none; null where it is
    /// the same.
    /// </summary>
    private EditDeckEvent? Rests(PanelLayout now)
    {
        var choice = hot.Kind == PanelHitKind.Act && now.Row(hot.Key) is { Kind: PanelRowKind.PickerBody } body
            && PanelPickers.Lookup(body.Item.Children ?? []) is null ? hot.Id : 0u;
        if (choice == resting)
        {
            return null;
        }
        resting = choice;
        return new EditDeckEvent(EditDeckAction.Hover, choice, placed?.Open ?? default);
    }

    private void Hot(PanelHit hit)
    {
        if (hit == hot)
        {
            return;
        }
        hot = hit;
        Paint();
        Tip(hit);
    }

    /// <summary>
    /// §529: what <paramref name="hit"/> says in a tooltip under it: a header's icon button its name, a line whose words were
    /// cut short its words whole, §575 an entry showing its icon alone its words (<see cref="PanelLayout.TipOf"/>); none for the
    /// rest, and the tip goes.
    /// </summary>
    private void Tip(PanelHit hit)
    {
        if (layout is not { } now || placed is not { } at || !host.IsOpen || now.TipOf(hit) is not { } words)
        {
            tip.Hide();
            return;
        }
        var r = OnScreen(hit);
        var room = at.Room.Work ?? at.Room.Display;
        tip.Show(at.Gfx, at.Look, at.Scale, words, new POINT { x = (r.left + r.right) / 2, y = r.bottom }, new RECT { left = room.Left, top = room.Top, right = room.Right, bottom = room.Bottom }, Edge.Top);
    }

    // --- UI Automation ------------------------------------------------------------------------------

    /// <summary>
    /// narrator, §520, §575: the trees a screen reader is given — the bar a tool bar named "Edit", its entries buttons that
    /// expand and collapse (their flyout open or not), its buttons; the flyout a pane named for its page: its
    /// title's buttons, each section a group, each control by its row's name — a toggle that toggles, a number box a range, a
    /// dropdown and an expander that expand and collapse, the rest buttons —. Greyed rows are in
    /// it, said to be off.
    /// </summary>
    internal UiaTree AccessTree(bool bar) =>
        DeckUia.Tree(layout, bar ? (host.IsOpen ? host.PanelOnScreen : default) : FlyoutOnScreen, keys || filter.Focused ? focus : PanelHit.None, bar);

    /// <summary>narrator: element <paramref name="id"/> of the bar's tree (<paramref name="bar"/>) or the flyout's acted on by a screen reader: what its click does.</summary>
    internal void Act(UiaAct act, int id, double value = 0, bool bar = true)
    {
        // §536: a look's preview is a picture: nothing to act on.
        if (layout is not { } now || DeckUia.HitOf(now, id, bar) is not { IsNone: false } hit || hit.Kind == PanelHitKind.Preview)
        {
            return;
        }
        if (act == UiaAct.Focus)
        {
            Focus(hit);
            return;
        }
        // A number box set to a value: a step toward it (its − where the value is lower).
        if (act == UiaAct.SetValue && hit.Kind == PanelHitKind.More && now.Row(hit.Key)?.Stepper is { } stepper)
        {
            var range = DeckUia.Range(stepper);
            if (value == range.Value)
            {
                return;
            }
            hit = value < range.Value ? new PanelHit(PanelHitKind.Fewer, stepper.Fewer, Key: hit.Key) : hit;
        }
        if (Activate(hit) is { Action: not EditDeckAction.None } happened)
        {
            Invoked?.Invoke(happened);
        }
    }

    /// <summary>narrator: the edit bar's elements, or §575 its flyout's, for UI Automation (Accessibility\Uia.cs).</summary>
    private sealed class DeckAccess(EditDeck deck, bool bar) : IUiaSource
    {
        public UiaTree Build() => deck.AccessTree(bar);

        public void Act(UiaAct act, int id, double value) => deck.Act(act, id, value, bar);
    }
}
