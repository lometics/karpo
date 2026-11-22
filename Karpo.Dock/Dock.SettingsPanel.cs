// edit, §520: the settings' side of the dock (docs\widgets.md §520, §575; SettingsPanel.cs for the model, EditDeck for its
// windows). The pages replaced the edit bar's drop-down menus (⚙ Karpo ▾, Display ▾, Taskbar ▾, Look ▾, Folder ▾, Widget ▾),
// and each is what that menu was — the same builder, the same picks, the same handler —, §575 each an entry of the edit bar
// opening it in a flyout of its own:
//
//  - **Its pages** (PanelOf) are built where the chrome is reconciled, as the config has them now — whole on the one display
//    whose bar is in use (panelOn: its flyout open), their entries alone on the others: Karpo's and §535 the
//    accounts' (SettingsItems), the display's, the layouts', the taskbar's and the Appearance (LooksRows), the folder's open
//    arranged or selected there (OptionRowsFor, its panel's side) and the widget's selected there (its Cut, Copy and Remove,
//    and its settings), §575 the add list's while it is open. Each with the menu it was (a Pending, its picks), so a control
//    acts as its row did: Chosen, the one way every menu row is picked (PanelActed).
//  - **A dropdown**, a colour, a text box and a button with a level of its own open their row's level in a popup under them
//    (PanelPopup): a menu of the same rows, its picks the page's own handler's, a choice closing it as a ComboBox closes.
//  - **A link** goes to the page that owns its setting, its flyout open at its row (PanelLinked, EditEntries.Landing); so
//    does every way in from outside (OpenEditMenu: Win+Alt+Enter, a face's "Manage accounts", a second start). §628, §646 one
//    to a page under another's entry (Add a layout, an account's Mail folders) is remembered (linkedBy): that page's way back
//    lands on it, and Mail folders is its own level.
//  - **The selection** is followed (PanelMemory.Follow): a click on a widget selects it and opens its entry's flyout, never a
//    menu (§520 decision 3); as the selection goes, its entry and its flyout go.

using Karpo.Core;
using TerraFX.Interop.Windows;
using Edge = Karpo.Core.Edge;

namespace Karpo.Dock;

/// <summary>
/// §686, §695 (the person, 2026-11-12: "the reset button also appears when i am just hovering over a color value. this should
/// obviously also not be"): while a choice is tried on, a page is made of the person's own config — what is chosen, what a
/// dropdown says, which values have a reset, every row as it stands —, and only what pictures the look as a whole (the
/// Appearance's preview, the tabs' look: a LookShown of the person's config) the config worn, so it shows the choice tried on.
/// </summary>
internal static class TriedOnPreviews
{
    public static List<FlyoutItem> Of(IReadOnlyList<FlyoutItem> items, Config persons, Config worn) =>
        [.. items.Select(item =>
        {
            var next = item.Children is { Count: > 0 } children ? item with { Children = Of(children, persons, worn) } : item;
            return item.Shows is { } shows && ReferenceEquals(shows.Config, persons) ? next with { Shows = shows with { Config = worn } } : next;
        })];
}

/// <summary>
/// §682 (the person, 2026-11-12: "mail folders should actually be shown like a tree and not individually listed"): a mailbox's
/// folders, each named by its path ("Inbox / Projects / 2026", as the readers name them), as a tree — each under the nearest
/// of its parents listed, a step further in, by its own name; one whose parents are not listed by the rest of its path.
/// </summary>
internal static class MailTree
{
    /// <summary>What separates a folder's parents' names from its own in its path.</summary>
    public const string Separator = " / ";

    /// <summary>Each of <paramref name="paths"/> as the tree shows it: how many steps in, and what it is called there.</summary>
    public static List<(int Indent, string Name)> Of(IReadOnlyList<string> paths)
    {
        var at = new Dictionary<string, int>(StringComparer.Ordinal);
        List<(int, string)> shown = [];
        foreach (var path in paths)
        {
            var parts = path.Split(Separator);
            var (indent, name) = (0, path);
            // The nearest parent listed: the longest of its path's beginnings that is a folder's own.
            for (var take = parts.Length - 1; take > 0; take--)
            {
                if (at.TryGetValue(string.Join(Separator, parts.Take(take)), out var parent))
                {
                    (indent, name) = (parent + 1, string.Join(Separator, parts.Skip(take)));
                    break;
                }
            }
            at.TryAdd(path, indent);
            shown.Add((indent, name));
        }
        return shown;
    }

    /// <summary><paramref name="items"/> (the Mail folders page's switches) as a tree: each its step in, its own name.</summary>
    public static List<FlyoutItem> Shown(List<FlyoutItem> items)
    {
        var tree = Of([.. items.Select(item => item.Text)]);
        return [.. items.Select((item, index) => item.Header || item.Separator ? item : item with { Text = tree[index].Name, Indent = tree[index].Indent })];
    }
}

public sealed unsafe partial class Dock
{
    /// <summary>
    /// §575: the display whose edit bar is in use — its flyout open, its search's — whose pages are built whole; the display
    /// edit mode began on until another is used.
    /// </summary>
    private string? panelOn;

    /// <summary>§520: what the edit bar remembers: the expanders open, for the run of the app; §575 the flyout open. Memory only.</summary>
    private readonly PanelMemory panelMemory = new();

    /// <summary>§520: each page of the bar in use with the menu it was (its picks) and how it is built again, by display and page.</summary>
    private readonly Dictionary<(string Display, PanelPage Page), (Pending What, Func<List<OptionPick>, List<FlyoutItem>> Build)> panelPages = [];

    /// <summary>
    /// §697: each page as made of the person's config while copies of it are tried on (<see cref="wornOver"/>), the config it
    /// was made of with it: made again only once another is tried over.
    /// </summary>
    private readonly Dictionary<(string Display, PanelPage Page), (Config Of, Pending What, List<FlyoutItem> Items)> wornPages = [];

    /// <summary>Tests only: the display whose edit bar is in use.</summary>
    internal string? PanelOn => panelOn;

    /// <summary>Tests only: the page whose flyout stands open; null for none.</summary>
    internal PanelPage? FlyoutPage => panelMemory.Page;

    /// <summary>
    /// §575: the flyout open closes (its entry clicked again, Escape, a click elsewhere); edit mode stays. At once, or
    /// (<paramref name="now"/> false: a press elsewhere, in the middle of its own handling) as the frame ends.
    /// </summary>
    private void ClosePanel(bool now = true)
    {
        // §634: never while Windows' question asked over it stands (Ask): a press on the dim then is no click elsewhere.
        if (panelMemory.Page is null || asking > 0)
        {
            return;
        }
        panelMemory.Show(null);
        // §684: a choice tried on under the pointer comes off with the panel.
        if (tried is not null)
        {
            TakeOff();
        }
        // §535: what was typed in an account's form goes as its flyout closes, its password wiped.
        MenuAccountsClosed();
        StirChrome();
        if (now)
        {
            PresentChrome();
        }
    }

    /// <summary>§634: how many of Windows' questions (<see cref="Ask"/>) stand now; the panel stays open while one does.</summary>
    private int asking;

    /// <summary>
    /// §634: whether <paramref name="row"/>'s <paramref name="pick"/> goes ahead: an action that asks (<see cref="OptionRow.Ask"/>,
    /// an enabled Act) once <paramref name="ask"/> answered yes; anything else at once. The one rule of every settings row's
    /// question (a palette's reset, a layout's delete, Quit Karpo, an account removed).
    /// </summary>
    internal static bool GoesAhead(OptionRow? row, OptionPick pick, Func<DeleteQuestion, bool> ask) =>
        pick.Kind != OptionPickKind.Act || row is not { Enabled: true, Ask: { } question } || ask(question);

    /// <summary>
    /// §634: <paramref name="question"/> asked in Windows' dialog (<see cref="DeleteAsk"/>), modal to the settings flyout standing
    /// open (its page's control asked it), else to <paramref name="owner"/>: the flyout is not light-dismissed while it stands
    /// (<see cref="ClosePanel"/> waits), and has the keys back once it is answered. True for its yes.
    /// </summary>
    private bool Ask(HWND owner, DeleteQuestion question)
    {
        var flyout = panelMemory.Page is not null && panelOn is { } on && chromes.TryGetValue(on, out var chrome) && chrome.Deck.FlyoutOpen ? chrome.Deck.FlyoutHwnd : HWND.NULL;
        asking++;
        try
        {
            return DeleteAsk.Ask(flyout != HWND.NULL ? flyout : owner, question);
        }
        finally
        {
            asking--;
            if (flyout != HWND.NULL && TerraFX.Interop.Windows.Windows.IsWindow(flyout))
            {
                _ = TerraFX.Interop.Windows.Windows.SetForegroundWindow(flyout);
            }
        }
    }

    /// <summary>§634: <see cref="GoesAhead(OptionRow?, OptionPick, Func{DeleteQuestion, bool})"/> asked by the dock (<see cref="Ask"/>).</summary>
    private bool GoesAhead(Pending what, OptionRow? row, OptionPick pick) =>
        GoesAhead(row, pick, question => Ask(what.Owner?.Hwnd ?? default, question));

    /// <summary>§520 decision 4: Escape's step on the bar: the words typed (a box's, the add list's search's), else §575 the flyout closes; whether it took one.</summary>
    private bool PanelEscape(EditEscape step)
    {
        if (panelOn is not { } on || !chromes.TryGetValue(on, out var chrome))
        {
            return false;
        }
        if (step == EditEscape.ClearSearch)
        {
            return chrome.Deck.ClearSearch();
        }
        ClosePanel();
        return true;
    }

    /// <summary>
    /// §575: <paramref name="page"/>'s flyout open on display <paramref name="displayId"/>'s edit bar — any other closing —, at
    /// the row of level <paramref name="level"/> (a menu's level key, as OptionPick has it) where given, its expanders open
    /// and the row lit a moment: an entry clicked, a link's landing, a way in from outside (<see cref="OpenEditMenu"/>). §576:
    /// Add's for <paramref name="add"/>, the place it adds at (a "+"'s); none, §593 the edit bar's own Add's (<see cref="AddAt"/>),
    /// nothing opening where there is nowhere to add. §628 the <paramref name="nth"/> of the things of level <paramref name="level"/>
    /// where given (a layout among the layouts: OptionMenu.OwnedLevel's).
    /// </summary>
    private void OpenPanel(string displayId, PanelPage page, string? level = null, Adding? add = null, int? nth = null, string? by = null)
    {
        if (editor is null)
        {
            return;
        }
        // §646: back from a page under another's entry, at the link it was opened by, its expanders open (an account's Mail
        // folders: that account's expander), not lit: the place it was left.
        var from = panelMemory.Page;
        var back = level is null ? PanelRows.BackTo(from, page, linkedBy) : null;
        level ??= back;
        // §646: a page under another's entry knows the link it was opened by (Mail folders: whose account's); none opened otherwise.
        if (PanelRows.IsUnder(page) && page != from)
        {
            linkedBy = by;
        }
        // §593: Add's page always for a place: the edit bar's own Add's where no "+" opened it (AddAt).
        if (page == PanelPage.Add && (add ??= adding is { } open && open.Window.Id == displayId ? open : AddAt(displayId)) is null)
        {
            return;
        }
        if (!ReferenceEquals(adding, add))
        {
            EndAdding();
            adding = add;
            if (add?.Folder is { } into)
            {
                addingClear = into.Flyout.ContentOnScreen;
            }
            // §617: from the page's opening its place holds the empty ghost, the bar or the folder laid out round it.
            if (add is not null)
            {
                GhostOf(add.Window.Id, 0);
                add.Window.ShowEdit();
            }
        }
        // §646: its Mail folders pages are the Accounts entry's too: what was typed in a form stays while one is shown.
        if (from is { } was && PanelRows.EntryOf(was) == PanelPage.Accounts && PanelRows.EntryOf(page) != PanelPage.Accounts)
        {
            MenuAccountsClosed();
        }
        // §628: Add a layout's page opened afresh reads this PC afresh (its pins may have changed), kept while it stands.
        if (page == PanelPage.AddLayout && panelMemory.Page != PanelPage.AddLayout)
        {
            presetMachine = null;
        }
        panelOn = displayId;
        panelMemory.Show(page);
        StirChrome();
        PresentChrome();
        if (!chromes.TryGetValue(displayId, out var chrome))
        {
            return;
        }
        string? key = null;
        if (level is not null && panelPages.TryGetValue((displayId, page), out var built)
            && built.What.Options.Find(pick => pick.Kind is OptionPickKind.None or OptionPickKind.Link && pick.Key == level && (nth is null || pick.Value == nth)) is { } holder
            && PanelRows.WithId(PanelRows.Of(built.Build([]), page.ToString()), holder.Id) is { } row)
        {
            key = row.Key;
        }
        chrome.Deck.Land(page, key, light: key is not null && back is null);
    }

    /// <summary>
    /// §628, §646: the link the page under another's entry standing open was opened by (its <see cref="OptionPick.Key"/>): the
    /// way back lands on it; §646 the Mail folders page is its own level (whose account's). Null where it was opened otherwise.
    /// </summary>
    private string? linkedBy;

    /// <summary>
    /// §646: the Mail folders page of the account whose link (<paramref name="link"/>, OptionAccountRows.Folders) it was opened
    /// by: that link's own level, the inbox and every folder a switch, its commands into <paramref name="picks"/> — the Accounts
    /// page's rows, so a pick finds its row there (SettingsRow) and is set as the page's. None for an account gone since.
    /// </summary>
    internal static List<FlyoutItem> MailFoldersItems(Config of, KarpoServices services, List<OptionPick> picks, string link) =>
        MailTree.Shown(OptionMenu.Rows(OptionRows.Find(KarpoMenu.Accounts(of, services), link)?.Level ?? [], picks, pictures: MenuPictures.Of));

    /// <summary>
    /// §520, §575: the pages of display <paramref name="display"/>'s edit bar as the config has them now, an entry each:
    /// Karpo, §535 Accounts, §585 (§624) Layouts, Taskbar (§585 the taskbar menu an expander of its Behavior) where it shows a layout,
    /// §567 Appearance; on the display the selection's entry stands on (<paramref name="where"/>), Folder for
    /// <paramref name="folder"/> open arranged or a folder selected, Widget for the widget selected (<paramref name="picked"/>),
    /// §528 or for the several selected (<paramref name="several"/>): the settings they have in common; §575 Add while its
    /// flyout is open. Their rows only where <paramref name="full"/> (the bar in use, its flyout's), their
    /// entries alone else.
    /// </summary>
    private SettingsContent PanelOf(MonitorInfo display, OpenFolder? folder, (string Key, string Display, EditDeckSubject Subject)? picked, IReadOnlyList<string> several, string? where, bool full)
    {
        var id = display.Id;
        foreach (var stale in panelPages.Keys.Where(key => key.Display == id).ToList())
        {
            panelPages.Remove(stale);
        }
        List<PanelPageView> pages = [];
        var owner = BarOf(id) ?? editor;
        void Add(PanelPage page, string title, string glyph, Pending what, Func<List<OptionPick>, List<FlyoutItem>> build, string? side = null, PanelHeader? header = null)
        {
            // §697 (the person, 2026-11-12: "when changing the color it is very laggy ... it is laggy when changing other settings as
            // well"): the page whose flyout is open alone made — every page's rows made at every change took 25 to 100 ms.
            if (!full || page != panelMemory.Page)
            {
                // §575: an entry alone, its page built as its flyout opens.
                pages.Add(new PanelPageView(page, title, glyph, [], []) { Side = side ?? PanelRows.SideOf(page) });
                return;
            }
            // §675: the choices that change how a thing is drawn pictured by what they draw.
            // §686, §695 (the person, 2026-11-12: "when hovering over a style it should not show the blue selection box even though
            // i have not selected it", "the reset button also appears when i am just hovering over a color value"): while a choice
            // is tried on, the page is made of the person's own config — its marks, its values, its resets as they stand —, only
            // its pictures of the whole look the config worn (TriedOnPreviews).
            // §697: and made once while one config is tried on after another — a colour's swatches hovered, a colour mixed —: the
            // person's own does not change under them (34 to 50 ms a step, the Appearance page's).
            List<FlyoutItem> items;
            if (wornOver is { } persons)
            {
                if (wornPages.TryGetValue((id, page), out var kept) && ReferenceEquals(kept.Of, persons))
                {
                    (what, items) = (kept.What, kept.Items);
                }
                else
                {
                    var worn = config;
                    config = persons;
                    try
                    {
                        items = Pictured(build(what.Options), what);
                    }
                    finally
                    {
                        config = worn;
                    }
                    wornPages[(id, page)] = (persons, what, items);
                }
                items = TriedOnPreviews.Of(items, persons, config);
            }
            else
            {
                items = Pictured(build(what.Options), what);
            }
            panelPages[(id, page)] = (what, build);
            // §645: a widget's page its rows in plain sections under its header card, as every page's.
            pages.Add(page switch
            {
                PanelPage.Widget => PanelGroups.Page(page, title, glyph, items, side, header),
                PanelPage.Folder => PanelGroups.FolderPage(page, title, glyph, items, header),
                _ => PanelRows.Page(page, title, glyph, items, side) with { Header = header },
            });
        }
        Add(PanelPage.Karpo, PanelRows.SideOf(PanelPage.Karpo), PanelRows.GlyphOf(PanelPage.Karpo),
            new Pending(Topic.LayoutEditor, owner) { SettingsMenu = new SettingsMenu(id, KarpoPage.Karpo) },
            picks => SettingsItems(config, SettingsServices(KarpoPage.Karpo), picks));
        // §535: the accounts a page of their own, Karpo's as a whole as its page is, on every display.
        Add(PanelPage.Accounts, S.SettingsNavAccounts, PanelRows.GlyphOf(PanelPage.Accounts),
            new Pending(Topic.LayoutEditor, owner) { SettingsMenu = new SettingsMenu(id, KarpoPage.Accounts) },
            picks => SettingsItems(config, SettingsServices(KarpoPage.Accounts), picks, page: KarpoPage.Accounts));
        var own = BarOf(id);
        var bar = own is not null ? SpotOf(own) : SpotOf(id, display.Name);
        foreach (var (page, scope) in new[] { (PanelPage.Layouts, LooksScope.Layouts), (PanelPage.Taskbar, LooksScope.Taskbar), (PanelPage.Appearance, LooksScope.Appearance) })
        {
            // No layout on a display that shows None: its bar's page is not there; §521 the layouts are (§585 and the displays), §567 and the
            // Appearance, Karpo's as a whole. §553 Nor is the taskbar menu, Taskbar's section: no Karpo bar there, none to
            // right-click.
            if (own is null && scope == LooksScope.Taskbar)
            {
                continue;
            }
            Add(page, PanelRows.SideOf(page), PanelRows.GlyphOf(page),
                new Pending(Topic.LayoutEditor, owner) { Looks = bar, LooksScope = scope },
                picks => LooksRows(bar, picks, scope));
        }
        // §628: Add a layout's page, under the Layouts page's entry, while its flyout shows it: its presets made for this PC.
        if (full && panelMemory.Page == PanelPage.AddLayout)
        {
            Add(PanelPage.AddLayout, PanelRows.SideOf(PanelPage.AddLayout), PanelRows.GlyphOf(PanelPage.AddLayout),
                new Pending(Topic.LayoutEditor, owner) { Looks = bar, LooksScope = LooksScope.AddLayout },
                picks => LooksRows(bar, picks, LooksScope.AddLayout));
        }
        // §646: an account's Mail folders page, under the Accounts page's entry, while its flyout shows it: the level of the link
        // it was opened by, set as the Accounts page's rows are.
        if (full && panelMemory.Page == PanelPage.MailFolders && linkedBy is { } folders)
        {
            Add(PanelPage.MailFolders, OptionAccountRows.FoldersTitle(MenuAccounts(), folders) ?? PanelRows.SideOf(PanelPage.MailFolders), PanelRows.GlyphOf(PanelPage.MailFolders),
                new Pending(Topic.LayoutEditor, owner) { SettingsMenu = new SettingsMenu(id, KarpoPage.Accounts) },
                picks => MailFoldersItems(config, SettingsServices(KarpoPage.Accounts), picks, folders));
        }
        // §689: the errors' page while its flyout shows it.
        if (full && panelMemory.Page == PanelPage.Errors)
        {
            var shown = faults.All.Take(MostFaultRows).ToList();
            Add(PanelPage.Errors, S.DockEditErrors(faults.Count), PanelGlyphs.ErrorRing,
                new Pending(Topic.Faults, owner) { Faults = shown, Target = id },
                _ => FaultItems(shown, Clock.Real()));
        }
        // §575: the selection's entries on the display it stands on alone.
        var here = string.Equals(where, id, StringComparison.OrdinalIgnoreCase);
        // The widget selected there, its settings; §528 several selected, the settings they have in common: one page of the
        // one row model, set on them all.
        List<string> chosen = !here ? [] : several.Count > 1 ? [.. several] : picked is { } one ? [one.Key] : [];
        // §529: a folder selected alone is the Folder page's, as the folder open arranged is: no page of its face of its own.
        var selectedFolder = chosen is [var lone] && IsFolderKey(lone) ? lone : null;
        if (selectedFolder is not null)
        {
            chosen = [];
        }
        if (here && (selectedFolder ?? folder?.Key) is { } folderKey && config.FindIsland(Islands.OwnerOf(folderKey), Islands.IdOf(folderKey)) is not null
            && (selectedFolder is null ? folder!.Window : windows.Find(window => Arranging(window) && window.Layout.PartIndex(folderKey) >= 0)) is { } folderHolder)
        {
            // §529: its faces, each its settings under it, Add a face, its panel's Alignment and its look on one page, under its
            // header; its Cut, Copy and Remove the header's (a click opens it, §527: no Open).
            var key = folderKey;
            var header = PanelRows.HeaderOf([(PanelRows.GlyphOf(PanelPage.Folder), S.CoreWidgetFolderListTitle)], PlaceOf(key));
            Add(PanelPage.Folder, S.CoreWidgetFolderListTitle, PanelRows.GlyphOf(PanelPage.Folder),
                new Pending(Topic.LayoutEditor, folderHolder) { Target = key },
                picks => [.. WidgetButtons(CanDuplicate(config, key)),
                    .. OptionMenu.Settings(TargetsOf(config, [key]), picks, FolderSide.Face, pictures: TrayPicture),
                    .. OptionMenu.Settings(TargetsOf(config, [key]), picks, FolderSide.Panel, pictures: TrayPicture)],
                header: header);
        }
        if (chosen.Count > 0 && (folder?.Window ?? windows.Find(window => Arranging(window) && window.Layout.PartIndex(chosen[0]) >= 0)) is { } holder)
        {
            var keys = chosen;
            var inFolder = folder is not null && folder.Flyout.LineOf(keys[0]) is not null ? folder : null;
            var islands = keys.Select(key => config.FindIsland(Islands.OwnerOf(key), Islands.IdOf(key))!).ToList();
            var kinds = islands.Select(IslandEdits.Kind).Distinct(StringComparer.Ordinal).ToList();
            var single = keys.Count == 1;
            var title = single ? kinds[0] : kinds.Count == 1 ? S.DockEditSelectedKind(count: keys.Count, kind: kinds[0]) : S.WidgetFolderWidgets(keys.Count);
            var glyph = islands.Select(island => SubjectOf(island).Glyph).Distinct(StringComparer.Ordinal).ToList() is [{ Length: > 0 } same] ? same : PanelRows.GlyphOf(PanelPage.Widget);
            // §529: its header card: their icons, their name, where they stand; Cut, Copy and Remove its icon buttons.
            var header = PanelRows.HeaderOf([.. islands.Select(island => (SubjectOf(island).Glyph is { Length: > 0 } its ? its : PanelRows.GlyphOf(PanelPage.Widget), IslandEdits.Kind(island)))], PlaceOf(keys[0]));
            Add(PanelPage.Widget, title, glyph,
                new Pending(Topic.LayoutEditor, holder) { Target = keys[0], Targets = single ? [] : keys, Folder = inFolder },
                picks => [.. WidgetButtons(keys.All(key => CanDuplicate(config, key))),
                    .. OptionMenu.Settings(TargetsOf(config, keys), picks, FolderSide.Face, inFolder?.Flyout.RoomCells(), TrayPicture)],
                side: single ? null : title,
                header: header);
        }
        // §575: the add list, a page as every other, while its flyout is open.
        if (full && panelMemory.Page == PanelPage.Add && AddOf(id) is { } adding)
        {
            var (what, rows) = adding;
            panelPages[(id, PanelPage.Add)] = (what, _ => rows);
            var at = PanelPage.Add.ToString();
            pages.Add(new PanelPageView(PanelPage.Add, S.DockEditAdd, PanelRows.GlyphOf(PanelPage.Add), PanelRows.Picks(rows, at), [])
            {
                Filter = typed => PanelRows.Picks(rows, at, typed),
            });
        }
        return new SettingsContent(pages);
    }

    /// <summary>
    /// §575: "+ Add"'s list for display <paramref name="displayId"/> (AddList.cs), for the place it was opened for
    /// (<see cref="adding"/>: §576 a "+"'s, §593 or the edit bar's own Add's) — the add tree's share for that place, §545 greyed
    /// where the licence says so; with the menu it is (its picks the add list's, Dock.AddChosen). Null where it is open for
    /// none there.
    /// </summary>
    private (Pending What, List<FlyoutItem> Rows)? AddOf(string displayId)
    {
        if (adding is not { } open || open.Window.Id != displayId || !Arranging(open.Window))
        {
            return null;
        }
        var (window, spot) = (open.Window, open.Spot);
        if (AddList.TypeOf(spot, config.IslandsFor(window.Screen.Id)) is not { } type)
        {
            return null;
        }
        // The add tree's share for the place: the widgets' commands first, the ready-made folders' after them; §586 each saying
        // how many of it the layout added into has, on its bar and in its folders.
        var placed = AddCounts.Placed(config.IslandsFor(window.Screen.Id));
        var (rows, entries, presets) = AddList.Rows(type, QuickSettings.Services.RotationSwitch.ScreenTurns, CMD_ADD_WIDGET, placed);
        return (new Pending(Topic.Library, window) { Library = entries, Presets = presets, Spot = spot }, Licensed(rows, entries, presets, spot));
    }

    /// <summary>§529: where the widget keyed <paramref name="key"/> stands, its header's line: on its layout's taskbar or dock, in a folder there or not.</summary>
    private string PlaceOf(string key) =>
        PanelRows.PlaceOf(config.LayoutOf(Islands.OwnerOf(key))?.Type ?? LayoutType.Taskbar, EditScope.FolderOf(key) is not null);

    /// <summary>
    /// §520: a control of page <paramref name="page"/> of display <paramref name="displayId"/>'s panel acted, sending its row's
    /// id <paramref name="id"/>: what the row did in its menu (<see cref="Chosen"/>: a setting set and saved, a widget cut,
    /// copied or removed, Karpo quit once asked), and the page shows the rows as they are now. §575 a widget of the add list
    /// added at its place, its flyout closing first (Dock.AddChosen): §593 the change its ghost showed, the bar not closing up
    /// behind the ghost before it is put in — and laid out as the config has it where nothing was.
    /// </summary>
    private void PanelActed(string displayId, PanelPage page, uint id)
    {
        if (!panelPages.TryGetValue((displayId, page), out var built))
        {
            return;
        }
        if (OptionMenu.PickOf(built.What.Options, id) is { Kind: OptionPickKind.Link })
        {
            PanelLinked(displayId, page, id);
            return;
        }
        if (page == PanelPage.AddLayout)
        {
            // §628: a preset picked makes its layout (one step of the undo, shown here) and goes back to the Layouts page, at
            // the new layout, its expander open — where it is named and made the default.
            var made = config.Layouts.Count;
            Chosen(built.What, id);
            if (config.Layouts.Count > made)
            {
                OpenPanel(displayId, PanelPage.Layouts, BarLooks.LayoutsRow, nth: config.Layouts.Count - 1);
                return;
            }
        }
        else if (page != PanelPage.Add)
        {
            Chosen(built.What, id);
        }
        else
        {
            var add = adding;
            placing = TakeGhost();
            try
            {
                ClosePanel();
                Chosen(built.What, id);
            }
            finally
            {
                // A ghost not put in (refused by the licence, Get Karpo Pro, the config changed under it): the bars as they are.
                if (placing is not null && !ReferenceEquals(placing.Config, config) && add is not null)
                {
                    Reghost(add);
                }
                placing = null;
                // §606: a ghost's folder its click did not open (ShowFolder takes it): gone.
                UnghostFolder();
            }
        }
        StirChrome();
        PresentChrome();
    }

    /// <summary>
    /// §551: a row of page <paramref name="page"/> put in order — dragged there and let go, or a place by Ctrl+Up or Ctrl+Down —
    /// to place <paramref name="to"/> among its own: its menu row's move (<see cref="OptionMenu.Moved"/>: a thing's, a face's, a
    /// toggle's), done as its menu's grip does it (<see cref="OptionPicked"/>): one change, saved, one step of the undo.
    /// </summary>
    private void PanelMoved(string displayId, PanelPage page, uint id, int to)
    {
        if (panelPages.TryGetValue((displayId, page), out var built) && OptionMenu.Moved(OptionMenu.PickOf(built.What.Options, id), to) is { } move)
        {
            OptionPicked(built.What, move, null);
        }
        StirChrome();
        PresentChrome();
    }

    /// <summary>
    /// §521: a text or a number box of page <paramref name="page"/> typed in place kept <paramref name="text"/>: its field
    /// <paramref name="id"/>'s words, kept as a menu's field keeps them (<see cref="FieldKept"/>: a layout's name, a clock's
    /// label, a number within its row's range), and the panel shows the rows as they are now.
    /// </summary>
    private void PanelTyped(string displayId, PanelPage page, uint id, string text, OptionFound? found = null)
    {
        // §676: what a lookup's inline list picked kept as its search's, as the popup's search keeps it.
        if (panelPages.TryGetValue((displayId, page), out var built) && OptionMenu.PickOf(built.What.Options, id) is { Kind: OptionPickKind.Field } pick)
        {
            FieldKept(built.What, pick, new FieldCommit(id, text, found));
        }
        StirChrome();
        PresentChrome();
    }

    /// <summary>
    /// §670: an inline picker's colour of page <paramref name="page"/> mixed: worn while dragged, kept let go, put back called
    /// off — the popup picker's own events (<see cref="MenuMixed"/>), so a colour is mixed one way wherever it is.
    /// </summary>
    private void PanelMixed(string displayId, PanelPage page, uint id, FlyoutEventKind kind, Argb color)
    {
        if (panelPages.TryGetValue((displayId, page), out var built))
        {
            MenuMixed(built.What, new FlyoutEvent(kind, id, Color: color), where: (displayId, page));
        }
        StirChrome();
        PresentChrome();
    }

    /// <summary>§520: a link row of page <paramref name="page"/> followed: the page that owns its setting, at its row (EditEntries.Landing).</summary>
    private void PanelLinked(string displayId, PanelPage page, uint id)
    {
        // §689: an error followed: what it is of opens.
        if (page == PanelPage.Errors && panelPages.TryGetValue((displayId, page), out var errors))
        {
            FaultChosen(errors.What, id);
            return;
        }
        if (panelPages.TryGetValue((displayId, page), out var built) && OptionMenu.PickOf(built.What.Options, id) is { Kind: OptionPickKind.Link } pick)
        {
            var landing = EditEntries.Landing(EditEntries.Of((OptionLink)pick.Value));
            OpenPanel(displayId, landing.Page, landing.Level, by: pick.Key);
        }
    }

    /// <summary>
    /// §520: a dropdown, a colour, a text box or a button with a level of its own clicked on the panel of display
    /// <paramref name="displayId"/>: its row's level in a popup under it (over it with no room under it, §521), as a
    /// ComboBox's list — the very rows its menu's level had, picked by the page's own handler; a choice made, it closes.
    /// </summary>
    private void PanelPopup(string displayId, EditDeck deck, EditDeckEvent happened)
    {
        if (!panelPages.TryGetValue((displayId, happened.Page), out var built) || DeckControlOf(displayId, happened.Box) is not { } from)
        {
            return;
        }
        var page = happened.Page.ToString();
        var key = happened.Key;
        var tool = happened.Id;
        List<FlyoutItem>? Level(List<OptionPick> picks) => PanelRows.Find(built.Build(picks), page, key) is { } holder ? PanelRows.PopupOf(holder, tool) : null;
        List<OptionPick> picks = [];
        if (Level(picks) is not { } rows || built.What.Owner is not { } owner)
        {
            return;
        }
        // §521: under its control, flush with its left, over it where there is no room under it (PopupPlace) — not hung from
        // the bar's edge nor centred on it, and not moved off the panel it stands over (FlyoutHost.ClearOf); flush with its
        // right where flush with its left it would pass the panel's side (§531). §587 its box's one width (Flyout.Open).
        var source = built.What;
        ShowFlyout(
            new Pending(Topic.LayoutEditor, owner)
            {
                Looks = source.Looks,
                LooksScope = source.LooksScope,
                SettingsMenu = source.SettingsMenu,
                Target = source.Target,
                Targets = source.Targets,
                Folder = source.Folder,
                Options = picks,
                Drops = true,
                From = from,
                // §356: its rows as the config has them now.
                Again = again => (Level(again) ?? [], null),
            },
            rows,
            FlyoutLayout.List,
            from.Anchor);
        DeckOpened(displayId, deck.Activated);
    }

    /// <summary>
    /// §520: a popup of the panel (<see cref="Pending.Drops"/>) after a pick: a choice made (<paramref name="choseOne"/>: the
    /// flyout's own rule for a level of one choice, <see cref="Flyout.OneChoice"/>) — a setting set, a colour picked, §554 a
    /// search's find, §589 an Add's row (Add a face's face, Add a toggle's toggle) — closes it, as a ComboBox's list closes;
    /// a step, a switch, a field's words keep it. §520 to §588 the dock asked the pick's kind (a Set, a Field with a find)
    /// instead, which knew no Add's row: Add a face stayed open after its face was added.
    /// </summary>
    private void PanelDropped(Pending what, bool choseOne)
    {
        if (what.Drops && choseOne && ReferenceEquals(pending, what))
        {
            CloseFlyout();
        }
        StirChrome();
        PresentChrome();
    }
}
