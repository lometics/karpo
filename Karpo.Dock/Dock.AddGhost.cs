// §593: the add page's ghost (the person, 2026-11-09: "when i hover over a group or widget to add it should show the whole group
// and widget on the taskbar as a ghost preview. when i click it should place it there"; "same behaviour should be implemented
// for the widgets placed inside the folder").
//
//  - **What it is.** The widget or the ready-made group under the pointer on the add page (or the keys: EditDeck.Proposing) put
//    in at the place the page adds at, in a copy of the config (AddList.Into, an AddGhost). The bars and the open folder are
//    built from that copy (Seen) through their own layout — the islands made for it as for any widget (SyncIslands), each bar
//    laid out again (DockWindow.Build), the folder configured again (FolderIsland.Configure) — so the ghost is the widget as it
//    will stand, at its size, its neighbours making room for it, gliding as a carried block's do. It wears the ghost's look on
//    top: its own drawing faint, the accent's wash and outline round it (Scene's EditView.Ghost, FolderFlyout.Ghost).
//  - **Nothing written.** The config stays the config: nothing is saved, no step of the undo, the add page's counts unchanged;
//    the copy is the ghost's alone and goes with it — the pointer off the tile, the page closed, edit mode ended (EndAdding),
//    or the config changed under it by anything else (a ghost of another config is none: Ghost).
//  - **The click** puts in that very copy (Dock.AddChosen, Ghosted): the same ids, so the bar the ghost laid out is the bar the
//    click leaves — the ghost's widget made real where it stands, nothing made again, nothing moving.
//  - **§606 a group's folder open too** (the person, 2026-11-09: "when placing a folder group it should show the folder contents
//    as ghost preview as well not only the faces on the taskbar"): the folder the ghost put on a bar stands open from its face
//    as the click opens it (GhostFolder): its own view, arranged, its lines the group's, opened as a look (no keys taken from
//    the add page), all of it faint in the ghost's outline, nothing of it to press. A folder open arranged steps aside for it
//    meanwhile (one folder at a time: the click closes it). The click's opening takes that very view (GhostTaken), whole.
//  - **§617 the empty ghost** (the person, 2026-11-09: "when adding a widget it still first shows just a blue line, this should
//    not be. it should from teh beginning show an empty blue rectangle"): from the page's opening, while no tile is under the
//    pointer, the place holds one standard room the same way (AddList.Empty: one cell on a bar, one line in a folder), none
//    of its drawing shown, only the ghost's outline round it; a tile under the pointer turns it into that pick's ghost, the
//    outline growing from the one into the other; off every tile it is back. It is no pick: a click takes nothing of it
//    (TakeGhost), a press on it is on the bar's room. §593's line (the bar's insertion line, the folder's accent bar) is gone.
//  - **§629 seen through the page** (the person, 2026-11-09: "the add widget modal should be semi transparent in case the ghost
//    preview is directly behind it. but it should only be semi transparent for the ghost preview and not for everything
//    actually."): wherever the ghost — on a bar, a ghost folder's lines, a line of the folder the page adds into — runs under the
//    add page, the page shows it through itself there, nothing else (SeeGhosts, SeeThrough.cs).
//
// Not given to a ghost: its data sources (DataSources follows the config) — a hover starts no reading of anything far away. §635
// what it shows instead are the samples (Samples: a place that certainly has the data, made up and built in, no network, no
// account): the ghost's widget and everything inside it are made showing them (SyncIslands sets Samples.Ghost, Islands.Make),
// and the ghost put in is made again, reading the person's own (Samples.Outlived). §643 a widget not set up yet is shown set up:
// the ghost is shown from a copy whose empty settings are filled in (AddGhost.Shown, Samples.Dress: this PC's drive, Documents,
// File Explorer, a made-up share and camera), and the click puts in the copy without them.

using Karpo.Core;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>§593: the add page's ghost: the pick under the pointer put in, in a copy of the config; null for none.</summary>
    private AddGhost? ghost;

    /// <summary>§593: the ghost a click on the add page puts in, taken from <see cref="ghost"/> as the page closes (PanelActed).</summary>
    private AddGhost? placing;

    /// <summary>§593: the ghost standing, made of the config as it is now; none where none stands, or the config changed since.</summary>
    private AddGhost? Ghost => ghost is { } shown && ReferenceEquals(shown.Base, config) ? shown : null;

    /// <summary>
    /// §593: what the bars and the folders are built from (DockWindow.Items, PlacedIslands): the config, with the add page's
    /// ghost in where one stands — §643 set up where it is not yet (AddGhost.Shown, Samples.Dress).
    /// </summary>
    private Config Seen => Ghost?.Shown ?? config;

    /// <summary>
    /// §593: the add page's pick of command <paramref name="id"/> (<paramref name="what"/>, its page's) put in at its place, in a
    /// copy of the config: the ghost standing or being placed where it is that pick's, made anew else.
    /// </summary>
    private AddGhost? Ghosted(Pending what, uint id) =>
        (placing ?? ghost) is { } shown && shown.Id == id && ReferenceEquals(shown.Base, config) ? shown
        : what.Spot is { } spot ? AddList.Into(config, spot, id, CMD_ADD_WIDGET, what.Library, what.Presets, Machine) : null;

    /// <summary>This PC as a ready-made group is made for: its battery, its drives and its power menu, read now.</summary>
    private static PresetMachine Machine()
    {
        var power = QuickLinkMachine.Read();
        return PresetMachine.Read(BatteryIsland.Probe() is not null, power.Sleep, power.Hibernate);
    }

    /// <summary>
    /// §593: the add page on display <paramref name="displayId"/>'s edit bar has its pick of command <paramref name="id"/> under
    /// the pointer or the keys (0: none, a folder's head, a greyed tile): its ghost where the page adds, the one before gone.
    /// </summary>
    private void GhostOf(string displayId, uint id)
    {
        AddGhost? next = null;
        if (editor is not null && adding is { } add && add.Window.Id == displayId && Arranging(add.Window))
        {
            next = id != 0 && panelPages.TryGetValue((displayId, PanelPage.Add), out var built) ? Ghosted(built.What, id) : null;
            // §617: no pick, the empty ghost: the one standing kept, so the bar does not move for it again.
            next ??= Ghost is { Empty: true } kept ? kept : AddList.Empty(config, add.Spot);
        }
        if (ReferenceEquals(next, Ghost) || adding is not { } at)
        {
            return;
        }
        ghost = next;
        Reghost(at);
    }

    /// <summary>§593: the add page <paramref name="open"/> going: its ghost with it, the bars and the folder as the config has them.</summary>
    private void Unghost(Adding open)
    {
        if (ghost is null)
        {
            return;
        }
        // §606: its folder too, however stale the ghost is.
        UnghostFolder();
        var stood = Ghost is not null;
        ghost = null;
        if (stood && !disposed)
        {
            Reghost(open);
        }
        else if (open.Folder is { } folder)
        {
            folder.Flyout.Ghost = null;
        }
        SeeGhosts();
    }

    /// <summary>
    /// §593: a click on the add page: its ghost taken to be put in (<see cref="placing"/>), nothing laid out again for its going
    /// — the click's change lays the bar out as the ghost had it.
    /// </summary>
    private AddGhost? TakeGhost()
    {
        // §617: the empty ghost is no pick: nothing of it is put in, and it goes as the page closes (Unghost).
        if (Ghost is not { Empty: false } taken)
        {
            return null;
        }
        ghost = null;
        return taken;
    }

    /// <summary>
    /// §593: the bars and the open folder of <paramref name="add"/>'s place shown from <see cref="Seen"/>: the islands as it has
    /// them (a ghost's made, one gone let go), every bar showing its list laid out again, the folder it adds into marking its
    /// ghost line or, with none, its place.
    /// </summary>
    private void Reghost(Adding add)
    {
        if (add.Folder is { } folder)
        {
            folder.Flyout.GhostEmpty = Ghost?.Empty == true;
            folder.Flyout.Ghost = Ghost?.Key;
        }
        // §606: the folder of a ghost gone or another one shut before its island goes with it.
        if (ghostFolder is { } open && !ReferenceEquals(open.Of, Ghost))
        {
            UnghostFolder();
        }
        SyncIslands();
        foreach (var window in windows.Where(window => Arranging(window) && string.Equals(Config.DisplayOwner(window.Screen.Id), add.Spot.Owner, StringComparison.OrdinalIgnoreCase)))
        {
            window.Build();
        }
        UpdateIslands();
        GhostFolder(add);
        SeeGhosts();
    }

    /// <summary>
    /// §629: every edit bar's add page shown its ghost where it stands now (<see cref="EditDeck.SeeThrough"/>): the page adding's,
    /// the ghost on its bars, its ghost folder's lines, its line in the folder it adds into; the rest none.
    /// </summary>
    private void SeeGhosts()
    {
        foreach (var (id, chrome) in chromes)
        {
            chrome.Deck.SeeThrough(adding is { } add && add.Window.Id == id && Ghost is not null ? GhostSights(add) : []);
        }
    }

    /// <summary>§629: where <paramref name="add"/>'s ghost stands: on the bars, in the folder it adds into, its ghost folder over them.</summary>
    private List<GhostSight> GhostSights(Adding add)
    {
        List<GhostSight> sights = [];
        foreach (var window in windows)
        {
            if (Arranging(window) && GhostPart(window.Layout) is int part && window.GhostSightOf(part) is { } bar)
            {
                sights.Add(bar);
            }
        }
        if (add.Folder?.Flyout.GhostSight is { } line)
        {
            sights.Add(line);
        }
        if (ghostFolder is { } open && folderViews.TryGetValue(open.Key, out var view) && view.IsOpen && view.GhostSight is { } lines)
        {
            sights.Add(lines);
        }
        return sights;
    }

    /// <summary>
    /// §606: the folder of the ghost's ready-made group, standing open as the ghost's click opens it (<see cref="AddChosen"/>:
    /// <see cref="OpenArranged"/>), by the ghost it is of; null for none.
    /// </summary>
    private (AddGhost Of, string Key)? ghostFolder;

    /// <summary>§606: the folder open arranged that stands aside while the ghost's folder stands open in its place; null for none.</summary>
    private OpenFolder? ghostAside;

    /// <summary>
    /// §606 (the person, 2026-11-09: "when placing a folder group it should show the folder contents as ghost preview as well
    /// not only the faces on the taskbar"): the ghost a ready-made group put on a bar, its folder opened from its face as the
    /// click opens it — its own view (FolderView: the one the click shows), arranged, where a folder opened from that face
    /// stands, its lines the group's — as a look (FlyoutHost.Previewing: the add page keeps the keys), in the ghost's look
    /// (<see cref="FolderFlyout.Ghosted"/>: all of it faint, §621 the ghost's outline round each of its lines, nothing to
    /// press). A folder open arranged stands aside for it (<see cref="FolderFlyout.Aside"/>), one folder at a time, as the
    /// click closes it.
    /// Nothing for a widget, a ghost in a folder, or a folder standing already.
    /// </summary>
    private void GhostFolder(Adding add)
    {
        if (ghostFolder is not null || Ghost is not { } shown || add.Spot.FolderId is not null
            || windows.Find(window => Arranging(window) && window.Layout.PartIndex(shown.Key) >= 0) is not { } bar)
        {
            return;
        }
        var hit = Hit.Cell(bar.Layout.PartIndex(shown.Key), 0);
        if (bar.IslandAt(hit) is not FolderIsland { IsFolder: true } folder)
        {
            return;
        }
        ghostAside = openFolders.Find(open => open.Flyout.IsOpen);
        if (ghostAside is { } other)
        {
            other.Flyout.Aside = true;
        }
        var view = FolderView(shown.Key);
        view.Picked = null;
        view.AlsoPicked = [];
        view.Copied = [];
        view.Covered = chromes.TryGetValue(bar.Id, out var over) ? over.Deck.Panel ?? default : default;
        FlyoutHost.Previewing = true;
        try
        {
            view.Paper = bar.Paper;
            view.Show(gfx, bar.Look, bar.UiScale, bar.WidgetAnchor(hit), bar.FolderWork(), bar.Layout.Edge, folder,
                editing: true, display: Rect(bar.Monitor.Bounds), motion: MotionOf(bar, arranging: true, switching: ghostAside is not null), dock: bar.Layout.Floating);
            view.Ghosted = true;
            ghostFolder = (shown, shown.Key);
        }
        catch (Exception error)
        {
            DropFolderView(shown.Key);
            Report(error);
        }
        finally
        {
            FlyoutHost.Previewing = false;
        }
    }

    /// <summary>
    /// §606: the ghost's folder gone — the pointer off its tile, another pick, the page closed — its view let go of at once, a
    /// folder aside for it back as it was.
    /// </summary>
    private void UnghostFolder()
    {
        if (ghostFolder is { } open)
        {
            ghostFolder = null;
            // §629: the page lets go of it before it goes.
            SeeGhosts();
            if (folderViews.TryGetValue(open.Key, out var view) && !openFolders.Exists(each => ReferenceEquals(each.Flyout, view)))
            {
                view.Hide();
                DropFolderView(open.Key);
            }
        }
        if (ghostAside is { } aside)
        {
            ghostAside = null;
            if (openFolders.Contains(aside))
            {
                aside.Flyout.Aside = false;
            }
        }
    }

    /// <summary>
    /// §606: the click's folder opening (<see cref="ShowFolder"/>) where the ghost's stands open: the view the ghost showed,
    /// itself from now on — no ghost of it to let go of, the folder aside for it closing as one folder at a time has it.
    /// Whether it was the ghost's.
    /// </summary>
    private bool GhostTaken(string key)
    {
        if (ghostFolder is not { } open || !string.Equals(open.Key, key, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        ghostFolder = null;
        ghostAside = null;
        return true;
    }

    /// <summary>§593: the part of <paramref name="layout"/> the ghost stands in; null where it stands in none of it.</summary>
    private int? GhostPart(Layout layout) =>
        Ghost is { } shown && layout.PartIndex(shown.Key) is var part and >= 0 ? part : null;
}
