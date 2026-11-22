// Pins: the dock's part of them.
//
// A pin is a widget of the bar like any other (BarKind.Pin), drawn and behaving
// as a pinned app on Windows' taskbar: its icon with a running indicator, the
// window previews on hover, the jump list on a right click, and the same click
// rule as every other app entry of Karpo's (AppClicks). The Apps widget keeps
// only the open apps nothing on that bar pins — a pin inside a folder counts
// too, and the folder shows their indicator instead.
//
// So the app buttons are a bar's own: each bar works out its own tiles (its
// pins in the order they stand, and the open apps where its Apps widget is),
// because two bars may show different layouts.
//
// Dragging: a press on a pin or a folder may become a drag along
// the bar. Past the system's drag threshold the widget follows the pointer and
// the others open a gap where it would land, as in edit mode. Over a folder it
// goes into it; resting on the middle of another widget for half a second makes
// a folder of the two, which the target says by lighting up with a folder glyph
// (PinDrags decides, Scene.Drag draws). The same rules run in edit mode.
//
// Files dropped on the bar are pinned where the line shows, put into a folder
// they are dropped on, or opened with the program a pin they are dropped on
// stands for, as dropping them on it in File Explorer would.
//
// A pin whose target this PC doesn't have (settings brought from another PC, a
// drive that isn't there) is kept and drawn dimmed with a warning badge; a
// click explains and offers to find it again or take it off the bar.

using System.Numerics;
using System.Runtime.InteropServices;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>Segoe Fluent Icons: Search, for "Locate…".</summary>
    private const string GlyphLocate = "\uE721";

    // --- What a bar's app buttons are ----------------------------------------

    /// <summary>
    /// The app buttons of <paramref name="window"/>'s bar: one for every pin widget on it, where it
    /// stands, and the open apps nothing on that bar pins, where its Apps widget is — at each of
    /// them, where it holds two. A pin in a folder takes its app's windows too: the folder shows that
    /// they are open, and the Apps widget leaves them out.
    /// <para>
    /// A pinned app is its pin's alone on the whole display: its windows stand at the pin, merged
    /// with it, and no Apps widget of that display shows the app again — not a second one on the
    /// same bar, nor one on the display's other row. Another display's bar, which does not hold the
    /// pin, shows it among its open apps as any other.
    /// </para>
    /// </summary>
    private List<Tile> TilesFor(DockWindow window)
    {
        // perf: every app ever opened would stay resolved; past a few hundred they are resolved afresh.
        if (known.Count > 256)
        {
            known.Clear();
        }
        // §699 (the person, 2026-11-13: "when click add ... they still show actually, just only with the pin icon and not the real
        // icon"): the pins of what the bar shows (Seen, its Items) — the add page's ghost in a copy of the config while it stands —,
        // never the config's: a pin is found by the very widget the bar holds, and the copy's are others.
        var list = Seen.IslandsFor(window.Id);
        var owner = Config.DisplayOwner(window.Id);
        var front = Front();
        var pins = new List<(WidgetItem Island, Item Item, Tile? Tile, string? Folder)>();
        foreach (var (path, island) in Folders.All(list.Where(island => island is not null)))
        {
            if (island.Info != WidgetInfos.Pin)
            {
                continue;
            }
            var item = PinItems.For(island.Pin);
            var cut = path.IndexOf('/');
            pins.Add((
                island,
                item,
                cut < 0 ? new Tile(item, true, Config.IslandKey(owner, island.Id)) : null,
                cut < 0 ? null : Config.IslandKey(owner, path[..cut])));
        }
        var others = new List<Tile>();
        var inFolders = new Dictionary<string, List<HWND>>(StringComparer.OrdinalIgnoreCase);
        // desktops: another desktop's windows only where the bar's layout shows every desktop's (§362).
        var allDesktops = window.ShownLayout.AllDesktops;
        // app buttons: every open window, on every bar — the same wherever the open apps stand, as
        // Windows' "Show my taskbar apps on: All taskbars" (no setting: TaskbarApps.cs).
        foreach (var win in apps.All)
        {
            if (win.Elsewhere && !allDesktops)
            {
                continue;
            }
            var keys = win.Keys().ToList();
            var claimed = false;
            foreach (var pin in pins)
            {
                if (!keys.Any(pin.Item.Matches))
                {
                    continue;
                }
                claimed = true;
                pin.Tile?.Open.Add(win.Hwnd);
                if (pin.Folder is { } folder)
                {
                    if (!inFolders.TryGetValue(folder, out var open))
                    {
                        inFolders[folder] = open = [];
                    }
                    open.Add(win.Hwnd);
                }
                break;
            }
            // pins: no setting decides this any more. The Apps widget's being on the bar is
            // the setting — a taskbar with only Pin widgets shows only pins.
            if (claimed)
            {
                continue;
            }
            var index = others.FindIndex(tile => tile.Item.Matches(win.Key));
            if (index < 0)
            {
                if (!known.TryGetValue(win.Key, out var item))
                {
                    // The window, whose own icon is the last resort.
                    item = Item.Running(win.Aumid, win.Exe, win.Hwnd);
                    known[win.Key] = item;
                }
                others.Add(new Tile(item, false));
                index = others.Count - 1;
            }
            others[index].Open.Add(win.Hwnd);
        }
        // places: the buttons of the entries this window shows; a pin anywhere on the display still
        // claims its app's windows from every Apps widget there.
        var shown = window.Items();
        var tiles = new List<Tile>();
        foreach (var island in shown)
        {
            if (island.Kind == BarKind.Pin)
            {
                if (pins.Find(pin => ReferenceEquals(pin.Island, island)).Tile is { } tile)
                {
                    tiles.Add(tile);
                }
            }
            else if (island.Kind == BarKind.Apps)
            {
                // Every Apps widget of the bar shows them all, each its own buttons: a layout may
                // hold the open apps twice, and it was the first alone that showed them.
                var apps = Config.IslandKey(owner, island.Id);
                foreach (var app in others)
                {
                    var copy = new Tile(app.Item, false) { Apps = apps };
                    copy.Open.AddRange(app.Open);
                    tiles.Add(copy);
                }
            }
        }
        var now = Environment.TickCount64;
        foreach (var tile in tiles)
        {
            // OrderBy is stable: windows never active keep the order they were first seen in. The
            // decisions walk them by recency (AppClicks); their list shows them as they were opened (Listed).
            var ordered = tile.Open.OrderBy(apps.Recency).ToList();
            tile.Open.Clear();
            tile.Open.AddRange(ordered);
            var attention = tile.Open.Any(open => AttentionLit(apps.Get(open), now));
            tile.Mark = new Mark(tile.Open.Count, tile.Open.Contains(front), attention, SignalFor(tile.Item, tile.Open));
        }
        // What a folder's pins have open, so the folder shows an indicator of its own.
        foreach (var island in shown)
        {
            if (island.Kind.IsFolder()
                && islands.GetValueOrDefault(Config.IslandKey(owner, island.Id)) is FolderIsland folder)
            {
                var open = inFolders.GetValueOrDefault(Config.IslandKey(owner, island.Id)) ?? [];
                // Each pin in it, for the indicator and the badge its own card, line or button shows;
                // and the folder's button the badges of them all, so a count is seen with it shut.
                folder.SetMarks(item => MarkFor(item, allDesktops));
                folder.SetRunning(new Mark(open.Count, open.Contains(front), false, new TileSignal(Badge: folder.PinBadge)));
            }
        }
        // §667: and the windows each folder stands for, so a window minimizes into the folder its pin is in (WindowButtons).
        window.FolderWindows = inFolders;
        return tiles;
    }

    /// <summary>
    /// app buttons: <paramref name="combined"/>'s buttons a window each, where a bar's windows are not
    /// combined (<see cref="ButtonCombine"/>): a button per open window, in the order the windows were
    /// first seen — so a window keeps its place as others come and go, as Windows 11's do —, each saying
    /// its title (<see cref="AppButtons.Label"/>); a pin with nothing open keeps its one button, which
    /// says nothing.
    /// </summary>
    private List<Tile> Expanded(List<Tile> combined)
    {
        var front = Front();
        var now = Environment.TickCount64;
        var tiles = new List<Tile>();
        foreach (var tile in combined)
        {
            if (tile.Open.Count == 0)
            {
                tiles.Add(tile);
                continue;
            }
            foreach (var win in apps.All)
            {
                if (!tile.Open.Contains(win.Hwnd))
                {
                    continue;
                }
                var one = new Tile(tile.Item, tile.Pinned, tile.Key) { Apps = tile.Apps, Window = win.Hwnd, Label = AppButtons.Label(win.Title, tile.Item.Name) };
                one.Open.Add(win.Hwnd);
                // signals: a window's own button shows what that window says, as Windows 11's does.
                one.Mark = new Mark(1, win.Hwnd == front, AttentionLit(win, now), SignalFor(tile.Item, one.Open));
                tiles.Add(one);
            }
        }
        return tiles;
    }

    /// <summary>
    /// The pins the bars show now, each resolved once (see <see cref="PinItems"/>); what they had
    /// before and no bar shows any more is forgotten, and so is every broken one, so a drive plugged
    /// in or an app installed since makes its pins whole again.
    /// </summary>
    private void KeepPinItems()
    {
        // folder contents: their folders, resolved as pins are.
        PinItems.Keep(Folders.All(config.AllIslands)
            .Where(entry => entry.Island.Info == WidgetInfos.Pin || PinFolders.Lists(entry.Island))
            .Where(entry => entry.Island.Pin is not null)
            .Select(entry => entry.Island.Pin!.Target));
        // And the sites among them, whose pictures are kept the same way (Dock.Favicons.cs).
        KeepFavicons();
    }

    /// <summary>
    /// The window a click on an app is about: the one in front, or, while a popup of Karpo's has the
    /// focus (a folder, a jump list), the app window that had it before — so clicking an app in an
    /// open folder minimizes it, as clicking it on the bar does. A dialog or other owned window in
    /// front stands for the window of its owners that has a button (<see cref="WindowOwners"/>): the
    /// app is in front, and its button says so. And a window an app marked active itself
    /// (ITaskbarList::ActivateTab, SetActiveAlt) is, until another one is activated (<see cref="ActiveAltWindow"/>).
    /// </summary>
    private HWND Front()
    {
        var front = Apps.Foreground();
        uint pid = 0;
        _ = GetWindowThreadProcessId(front, &pid);
        var alt = ActiveAltWindow != HWND.NULL && apps.Find(ActiveAltWindow) is not null ? ActiveAltWindow : HWND.NULL;
        return (HWND)AppClicks.ActiveButton((nint)front, front != HWND.NULL && pid == GetCurrentProcessId(), (nint)apps.MostRecent, (nint)alt,
            at => (nint)WindowOwners.ButtonOf((HWND)at, hwnd => apps.Find(hwnd) is not null), at => IsIconic((HWND)at));
    }

    /// <summary>
    /// §619: the button that was active as a button went down on a bar (<see cref="Front"/> then), and
    /// where: Windows' taskbar decides a click by the button active at its press, never by what the
    /// press itself changed — the popups it closed handing the focus back, the time the click took.
    /// </summary>
    private (DockWindow Window, int Tile, HWND Front)? pressedFront;

    /// <summary>§619: a press went down on <paramref name="hit"/> of <paramref name="window"/>: the active button kept for its click, where it is an app's.</summary>
    private void FrontAtPress(DockWindow window, Hit? hit) =>
        pressedFront = hit is { Kind: HitKind.Tile, Index: var index } ? (window, index, Front()) : null;

    /// <summary>§619: the active button a click on tile <paramref name="index"/> of <paramref name="window"/> goes by: the one at its press, else now's (the keys, a menu's row).</summary>
    private HWND FrontForClick(DockWindow window, int index)
    {
        var at = pressedFront is { } pressed && pressed.Window == window && pressed.Tile == index ? pressed.Front : Front();
        pressedFront = null;
        return at;
    }

    // --- Clicks --------------------------------------------------------------

    /// <summary>
    /// A click on an app button: a pin this PC can't open explains itself, everything else follows
    /// the one rule (<see cref="AppClicks"/>); a middle click always starts another window.
    /// </summary>
    private void TileClicked(DockWindow window, int index, Tile tile, bool middle)
    {
        if (tile.Broken is not null)
        {
            BrokenFlyout(window, index, tile);
            return;
        }
        // app clicks: the list this button put up by a click (or the keys) goes with the next click on
        // it, as Windows' does; one the pointer put up stays, held from now on.
        var listing = previewOwner == window && preview is { IsOpen: true } shown && shown.Tile == index;
        var held = listing && previewHeld;
        // app clicks: Shift — or the middle button — starts another, Ctrl+Shift one as administrator,
        // Ctrl walks the windows; a plain click on several shows their list (AppClicks).
        var ask = AppClicks.Asked(middle, KeyState.Down(VK.VK_SHIFT), KeyState.Down(VK.VK_CONTROL));
        var listed = false;
        OpenApp(tile.Item, tile.Open, ask, () => window.Scene.Bounce(index), front: FrontForClick(window, index), list: () =>
        {
            listed = true;
            if (held)
            {
                ClosePreview(window);
            }
            else if (listing)
            {
                HoldPreview();
            }
            else
            {
                ClosePreview(window);
                OpenPreview(window, index, held: true);
            }
        });
        // Anything else the click did (a window brought forward or minimized) puts a list away.
        if (!listed)
        {
            ClosePreview(window);
        }
    }

    /// <summary>
    /// What <see cref="AppClicks.Decide"/> says, carried out: the app started, one of its windows
    /// brought to the front — switching to its desktop where it is on another one (§362) —, the one in
    /// front minimized, or, where <paramref name="list"/> can show them and a plain click asks it
    /// (<see cref="AppClicks.Lists"/>), the windows' list shown. <paramref name="launching"/> shows the
    /// press on what was clicked; <paramref name="front"/> is the active button it goes by, where not
    /// now's (<see cref="FrontForClick"/>).
    /// </summary>
    private void OpenApp(Item item, IReadOnlyList<HWND> open, AppAsk ask, Action? launching = null, Action? list = null, HWND? front = null)
    {
        var windowsOf = open.Select(hwnd => new AppWindow((nint)hwnd, IsIconic(hwnd))).ToList();
        var decision = AppClicks.Decide(windowsOf, (nint)(front ?? Front()), ask, list is not null && AppClicks.Lists(LastActiveClick));
        switch (decision.Kind)
        {
            case AppClickKind.Launch:
                if (item.Target.Length == 0)
                {
                    return;
                }
                launching?.Invoke();
                LaunchLater(item.Target, decision.Elevated && AppClicks.CanElevate(item.Target));
                return;
            case AppClickKind.ShowList:
                list?.Invoke();
                return;
            case AppClickKind.Activate:
                Bring((HWND)windowsOf[decision.Window].Handle);
                return;
            default:
                Apps.Minimize((HWND)windowsOf[decision.Window].Handle);
                return;
        }
    }

    /// <summary>
    /// <paramref name="hwnd"/> brought forward: on its own desktop, switching there first, where it is
    /// on another one — a layout showing every desktop's windows lists it (§362), and a card of the
    /// desktop flyout's lists any.
    /// </summary>
    private void Bring(HWND hwnd)
    {
        if (desktops is not null && (apps.Get(hwnd) is not { } win || win.Elsewhere))
        {
            desktops.Activate((nint)hwnd);
            return;
        }
        Apps.Activate(hwnd);
    }

    /// <summary>
    /// A pin whose target isn't on this PC: what is wrong, and what can be done about it — find it
    /// again, or take the pin off the bar, as Windows offers for a shortcut whose target has gone
    /// (<see cref="BrokenRows"/>).
    /// </summary>
    private void BrokenFlyout(DockWindow window, int index, Tile tile)
    {
        // app buttons: a pin behind "…", chosen from its list, answers by "…".
        var at = window.Layout.ItemRect(Hit.Tile(index)).IsEmpty ? Hit.AppOverflow : Hit.Tile(index);
        BrokenMenu(window, at, tile.Item, tile.Key, window.Anchor(at), folder: null);
    }

    /// <summary>
    /// pins: a missing pin's menu (<see cref="BrokenRows"/>), one for a pin on the bar and one in an
    /// open <paramref name="folder"/>, at <paramref name="anchor"/>. No drawn title, as no widget's menu
    /// has one (Dock.Menus.cs, WidgetMenuName): its first row names the pin.
    /// </summary>
    private void BrokenMenu(DockWindow window, Hit hit, Item item, string? key, POINT anchor, OpenFolder? folder)
    {
        var what = new Pending(Topic.Broken, window) { Hit = hit, Target = item.Target, Pin = key, Folder = folder };
        ShowFlyout(what, BrokenRows(item.Name, item.Broken, item.Target, folder is not null), FlyoutLayout.List, anchor, spoken: item.Name);
    }

    /// <summary>
    /// The rows of a missing pin's menu: the pin by its <paramref name="name"/>, greyed, with why it
    /// can't open (<paramref name="broken"/>) and its <paramref name="target"/> under it, wrapped and
    /// never cut; then "Locate…" ("Choose…" for a pin with nothing to open yet) and the row every pin
    /// has to take it away (<see cref="UnpinItem"/>: "Unpin from taskbar", "Remove from folder"
    /// <paramref name="inFolder"/>). No "Settings": what it opens, its name and its icon are its menu's
    /// rows (§228), and "Locate…" is what mends it.
    /// </summary>
    internal static List<FlyoutItem> BrokenRows(string name, string? broken, string target, bool inFolder) =>
    [
        new(0, name, Glyph(GlyphWarning), Detail: string.Join("\n", new[] { broken, target }.Where(line => line is { Length: > 0 })), Enabled: false),
        new(0, "", Separator: true),
        new(CMD_LOCATE, target.Length > 0 ? S.DockPinLocate : S.DockPinChooseMenu, Glyph(GlyphLocate)),
        UnpinItem(inFolder),
    ];

    /// <summary>Segoe Fluent Icons: Warning, for a pin whose target is gone.</summary>
    private const string GlyphWarning = "\uE7BA";

    /// <summary>
    /// An app's open windows, most recently used first: what the one rule walks through. Those of this
    /// desktop, and with <paramref name="allDesktops"/> every desktop's (§362).
    /// </summary>
    private List<HWND> OpenWindowsOf(Item item, bool allDesktops = false) =>
        [.. apps.All.Where(win => (allDesktops || !win.Elsewhere) && win.Keys().Any(item.Matches)).Select(win => win.Hwnd).OrderBy(apps.Recency)];

    /// <summary>
    /// What an app's tile in a folder shows: how many windows it has, whether one is in front, whether
    /// one asks for attention, and what the app says on its button — its badge, its windows' overlay —
    /// as the bar's own buttons do (<see cref="SignalFor"/>). Without the last a pin in a folder never
    /// showed a count.
    /// </summary>
    private Mark MarkFor(Item item, bool allDesktops)
    {
        var open = OpenWindowsOf(item, allDesktops);
        var front = Front();
        var now = Environment.TickCount64;
        return new Mark(open.Count, open.Contains(front), open.Any(window => AttentionLit(apps.Get(window), now)), SignalFor(item, open));
    }

    /// <summary>
    /// pins: the menu of a pin inside a folder, at the pointer over the folder (<paramref name="at"/>),
    /// which stays open behind it — its jump list, with "Unpin from taskbar" taking it out of the
    /// folder, and the rows Windows has for its file (Dock.WidgetMenus.cs).
    /// </summary>
    private void PinMenu(DockWindow window, Hit hit, PinTile pin, POINT at, OpenFolder folder)
    {
        if (pin.Item.Broken is not null)
        {
            BrokenMenu(window, hit, pin.Item, pin.Key, at, folder);
            return;
        }
        AppMenuAt(window, hit, pin.Item, OpenWindowsOf(pin.Item), pin.Key, pinned: true, at, folder);
    }

    /// <summary>
    /// pins: a widget dragged out of the open folder. Over a bar it is refused — nothing moves from a
    /// folder onto the taskbar (<see cref="Partners"/>) — and the folder says why for as long as it
    /// is carried there. Let go anywhere outside the folder, nothing happens: one folder stands open
    /// at a time, so there is no other folder to carry it into.
    /// </summary>
    private void FolderDragged(OpenFolder from, FolderFlyoutEvent happened)
    {
        var at = happened.At;
        // places: a bar, or a place in sight that is no other display's; never an anchor, which shows nothing.
        var bar = windows.Find(window => window.Droppable && window.Holds(at));
        foreach (var window in windows)
        {
            window.ShowNoDrop();
        }
        // folders: a folder's widget is refused on the bar before it is carried there, not after
        // (Partners): the bar opens no gap for it and the folder says why, once, rather than snapping
        // it back in silence.
        if (happened.Kind == FolderFlyoutAction.DragMove)
        {
            if (bar is not null && happened.Widget is CellIsland refused)
            {
                if (!ReferenceEquals(refusedFrom, from))
                {
                    refusedFrom = from;
                    from.Flyout.Say(Partners.WhyNot(refused.Settings, WidgetType.Bar));
                }
            }
            else if (refusedFrom is not null)
            {
                refusedFrom.Flyout.Say("");
                refusedFrom = null;
            }
            return;
        }
        refusedFrom = null;
        if (happened.Widget is not CellIsland widget)
        {
            return;
        }
        // Let go on the bar: it stays where it was, and the folder says why. Let go over nothing at
        // all: a cancel, and the widget stays in its folder.
        from.Flyout.Say(bar is null ? "" : Partners.WhyNot(widget.Settings, WidgetType.Bar));
    }

    /// <summary>folders: the open folder whose line is saying why a carried widget can't go on a bar.</summary>
    private OpenFolder? refusedFrom;

    /// <summary>A pin's own command from a jump list or a broken pin's flyout; false for anything else.</summary>
    private bool PinChosen(Pending what, uint id)
    {
        switch (id)
        {
            case CMD_PIN:
                PinApp(what);
                return true;
            case CMD_UNPIN when what.Pin is { Length: > 0 }:
                Unpin(what.Pin);
                return true;
            case CMD_LOCATE:
                Locate(what);
                return true;
            // menus: the jump list's row of buttons (Dock.Menus.cs, AppRows): its "Remove" is the
            // unpinning, asked nothing as one widget's "Remove" is (§253); its "Copy" a copy of
            // the pin following the pointer in edit mode, as any widget's (Dock.EditCopy.cs).
            case CMD_REMOVE_ISLAND when what.Pin is { Length: > 0 } pin && what.Owner is { } from:
                return SelectionAct(EditKeyAct.Remove, from, pin);
            case CMD_COPY_ISLAND when what.Pin is { Length: > 0 } pin && what.Owner is { } from:
                return CopyToPointer(from, pin, what.Folder);
            case CMD_CUT_ISLAND when what.Pin is { Length: > 0 } pin && what.Owner is { } from:
                return CutFromMenu(from, pin, what.Folder);
            default:
                return false;
        }
    }

    // --- Pinning and unpinning ------------------------------------------------

    /// <summary>
    /// "Pin to taskbar": a pin widget for the open app, where the pinned ones end — right before the
    /// Apps widget it was shown in, as the taskbar puts a new pin after the others.
    /// </summary>
    private void PinApp(Pending what)
    {
        if (what.Owner is not { } window || what.Target.Length == 0)
        {
            return;
        }
        var next = config.Clone();
        var list = next.IslandsFor(window.Id);
        // Never twice: a target this bar pins already, in another case or with a trailing slash, stays one pin.
        if (Folders.All(list).Any(entry => entry.Island.Info == WidgetInfos.Pin && PinList.Same(entry.Island.Pin?.Target, what.Target)))
        {
            return;
        }
        var island = BarKind.Pin.New();
        island.Pin!.Target = what.Target;
        island.Id = Folders.FreeId(list, island.Id);
        // The open apps of the bar it was pinned from, else any its layout has.
        var layout = next.ActiveLayout(window.Id);
        // The Apps widget whose button it was, where the bar holds two.
        var shownIn = what.Hit is { Kind: HitKind.Tile } hit && window.Layout.PartOf(hit) is var part and >= 0
            && window.Layout.Specs[part].Kind == BarKind.Apps
            ? Islands.IdOf(window.Layout.Specs[part].Key)
            : null;
        var appsId = window.Items().FirstOrDefault(entry => entry.Kind == BarKind.Apps && (shownIn is null || entry.Id == shownIn))?.Id
            ?? window.Items().FirstOrDefault(entry => entry.Kind == BarKind.Apps)?.Id
            ?? list.FirstOrDefault(entry => entry.Kind == BarKind.Apps)?.Id;
        var apps = appsId is null ? -1 : list.FindIndex(entry => entry.Kind == BarKind.Apps && entry.Id == appsId);
        if (apps >= 0)
        {
            // In the open apps' own section, right before them, in their place.
            Sections.Put(list, island, new BarSlot(Sections.Of(list[apps]), apps));
        }
        else
        {
            Sections.Insert(list, island);
        }
        Apply(next);
    }

    /// <summary>"Unpin from taskbar": the pin widget <paramref name="key"/> off the bar (or out of its folder).</summary>
    private void Unpin(string? key)
    {
        if (key is not { Length: > 0 })
        {
            return;
        }
        var next = config.Clone();
        var list = next.ListFor(Islands.OwnerOf(key));
        if (Folders.Remove(list, Islands.IdOf(key)) is null)
        {
            return;
        }
        foreach (var window in windows)
        {
            window.Removed(key);
        }
        Apply(next);
    }

    /// <summary>
    /// "Locate…": the file, folder or program a pin should open instead, chosen in Windows' own
    /// dialog, which the pin then points at. The one dialog picks a file or a folder alike
    /// (<see cref="TargetDialog"/>), starting near where the old target was.
    /// </summary>
    private void Locate(Pending what)
    {
        if (what.Pin is not { Length: > 0 } key || what.Owner is not { } window)
        {
            return;
        }
        var picked = TargetDialog.Pick(window.Hwnd, what.Target);
        if (picked is not { Length: > 0 })
        {
            return;
        }
        var next = config.Clone();
        if (next.FindIsland(Islands.OwnerOf(key), Islands.IdOf(key))?.Pin is not { } pin)
        {
            return;
        }
        pin.Target = picked;
        Apply(next);
    }

    // --- Files dropped on the bar ----------------------------------------------

    /// <summary>
    /// Files, folders or links dropped at <paramref name="at"/> on <paramref name="window"/>'s bar
    /// (<see cref="DropAt"/>): into the folder they were dropped on, opened with the app whose
    /// button they were dropped on, or pinned where the line showed. Never the same target twice on
    /// one bar.
    /// </summary>
    private void Dropped(DockWindow window, List<string> targets, Vector2 at)
    {
        var layout = window.Layout;
        var along = layout.Vertical ? at.Y : at.X;
        var (action, spot, _) = DropAt(window, at);
        switch (action.Kind)
        {
            // app clicks: as dropping them on the program in File Explorer: it opens them.
            case DropKind.OpenWith when action.App is { } program:
                Later(() => Launch.With(program, targets));
                return;
            case DropKind.OpenApp when action.App is { } aumid:
                Later(() => Launch.WithApp(aumid, targets));
                return;
            case DropKind.Nothing:
                return;
        }
        var part = action.Kind == DropKind.IntoFolder ? spot : -1;
        var next = config.Clone();
        var list = next.IslandsFor(window.Id);
        var pinned = Folders.All(list)
            .Where(entry => entry.Island.Info == WidgetInfos.Pin)
            .Select(entry => entry.Island.Pin?.Target ?? "")
            .ToList();
        var made = new List<BarItem>();
        foreach (var target in targets)
        {
            if (string.IsNullOrWhiteSpace(target) || pinned.Exists(already => PinList.Same(already, target)))
            {
                continue;
            }
            pinned.Add(target.Trim());
            var island = BarKind.Pin.New();
            island.Pin!.Target = target.Trim();
            island.Id = Folders.FreeId(list, island.Id);
            made.Add(island);
        }
        if (made.Count == 0)
        {
            return;
        }
        if (part >= 0 && Folders.Find(list, Islands.IdOf(layout.Specs[part].Key)) is BarItem { Folder: { } folder } holder)
        {
            // Each a pin's line of the list: files are not a taskbar entry moved into the folder,
            // they are pinned there.
            foreach (var island in made)
            {
                var line = ListKind.Pin.New();
                line.Id = island.Id;
                line.Pin!.Target = island.Pin!.Target;
                Folders.Add(holder.Kind, folder, line);
            }
            Apply(next);
            return;
        }
        // places: where the line stood, as the list has it (DockWindow.ListSlot).
        var slot = window.ListSlot(layout.DropSlot(along, -1));
        foreach (var island in made)
        {
            list.Add(island);
            IslandEdits.Move(list, island, slot);
            slot = slot with { Index = slot.Index + 1 };
        }
        Apply(next);
    }

    private sealed partial class DockWindow
    {
        /// <summary>pins: this bar's app buttons — its pins, and the open apps it shows (see <see cref="TilesFor"/>).</summary>
        public List<Tile> Tiles { get; private set; } = [];

        // app buttons: this bar's buttons both ways — an app each, and a window each where its
        // windows may not be combined —, so Build can take the second while it fits and the first
        // once the bar is full (ButtonCombine.WhenFull), and whether it last had to.
        private List<Tile> combined = [];
        private List<Tile>? expanded;
        private bool full;

        /// <summary>The bar of the main display, Windows' "main taskbar": the one the keyboard's chords are about first (Dock.KeysBar).</summary>
        public bool Main => monitor.Primary;

        /// <summary>
        /// app buttons: the buttons this bar shows, as things stand: an app each where they combine,
        /// a window each where they don't — and, where they combine "when taskbar is full", the
        /// latter until Build found the bar full with them (<see cref="full"/>).
        /// </summary>
        private List<Tile> Buttons() => expanded is { } each && !(full && ShownLayout.Combine == ButtonCombine.WhenFull) ? each : combined;

        /// <summary>Tile <paramref name="index"/> of this bar, or null.</summary>
        public Tile? TileAt(int index) => index >= 0 && index < Tiles.Count ? Tiles[index] : null;

        /// <summary>§667: the windows the pins in each of this bar's folders have open, by the folder's key (Dock.TilesFor).</summary>
        public Dictionary<string, List<HWND>> FolderWindows { get; set; } = [];

        /// <summary>
        /// §667: what this bar laid out that stands for windows, where Windows' minimize and restore animations go and come
        /// from (WindowButtons, MinRect): published as it is laid out again and as its buttons' windows change. An anchor
        /// shows no buttons, and publishes none.
        /// </summary>
        private void PublishButtons() => WindowButtons.Shared.Publish(
            (nint)Hwnd,
            IsAnchor || disposed
                ? null
                : WindowButtons.Laid(
                    layout,
                    [.. Tiles.Select(tile => (IReadOnlyList<nint>)[.. tile.Open.Select(open => (nint)open)])],
                    FolderWindows.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<nint>)[.. pair.Value.Select(open => (nint)open)], StringComparer.OrdinalIgnoreCase),
                    Bounds,
                    Main));

        /// <summary>
        /// app buttons: this bar is to be built again on its own (Dock.WindowsChanged), rather than
        /// every bar and every widget with it: a title changed on a button that says it, or — full,
        /// where the buttons combine "when taskbar is full" — its windows came, went or were renamed,
        /// which may let a window each fit again. Build finds out, and clears it.
        /// </summary>
        public bool Stale { get; private set; }

        /// <summary>
        /// This bar's app buttons again. True when the buttons themselves changed (an app opened, a
        /// pin came or went) and the bar has to be built again; false when only their indicators did,
        /// which are set in place — or only what they say, which leaves the bar <see cref="Stale"/>.
        /// </summary>
        public bool DeriveTiles()
        {
            var was = expanded;
            combined = dock.TilesFor(this);
            expanded = ShownLayout.Combine == ButtonCombine.Always ? null : dock.Expanded(combined);
            var fresh = Buttons();
            var before = Tiles;
            Tiles = fresh;
            if (!Same(fresh, before, labels: false))
            {
                return true;
            }
            // perf: a new title is this bar's alone to draw, and so is a window each put away for
            // being full that changed: whether they fit now is Build's to find out (ButtonCombine.WhenFull).
            Stale |= !Same(fresh, before, labels: true)
                || (expanded is not null && !ReferenceEquals(fresh, expanded) && (was is null || !Same(expanded, was, labels: true)));
            var called = false;
            for (var index = 0; index < before.Count && index < Tiles.Count; index++)
            {
                var old = before[index].Mark;
                var mark = Tiles[index].Mark;
                if (old != mark)
                {
                    scene.SetMark(index, mark);
                }
                // signals: once, as the attention begins — not with every blink that lights it again.
                if (mark.Attention && !old.Attention && dock.AttentionBegan(Tiles[index].Open))
                {
                    scene.Bounce(index);
                    called = true;
                }
            }
            if (called && !Shown && !Fullscreen)
            {
                Peek();
            }
            // §667: the same buttons where they stood, their windows as they are now.
            PublishButtons();
            return false;
        }

        /// <summary>
        /// app buttons: whether two lists of buttons are the same buttons — the same apps and pins and
        /// the same windows, and where <paramref name="labels"/> says so the same labels —, so only
        /// their indicators can differ and are set in place.
        /// </summary>
        private static bool Same(List<Tile> one, List<Tile> other, bool labels)
        {
            if (one.Count != other.Count)
            {
                return false;
            }
            for (var index = 0; index < one.Count; index++)
            {
                if (one[index].Item.Target != other[index].Item.Target
                    || one[index].Pinned != other[index].Pinned
                    || one[index].Key != other[index].Key
                    || one[index].Apps != other[index].Apps
                    || one[index].Broken != other[index].Broken
                    || one[index].Window != other[index].Window
                    || (labels && one[index].Label != other[index].Label))
                {
                    return false;
                }
            }
            return true;
        }

        // --- A widget dragged along the bar ------------------------------------

        /// <summary>A press on a pin or a folder, which a move past the drag threshold turns into a drag.</summary>
        private sealed class PinDrag(string key, Vector2 at, float grab)
        {
            public string Key { get; } = key;

            /// <summary>Where the press was, in window px: the drag begins once the pointer leaves the system's threshold.</summary>
            public Vector2 At { get; } = at;

            /// <summary>Where along the block it was taken hold of.</summary>
            public float Grab { get; } = grab;

            public bool Moved { get; set; }
        }

        private PinDrag? pinDrag;

        /// <summary>pins: whether a widget is being dragged along this bar outside edit mode.</summary>
        public bool PinDragging => pinDrag is { Moved: true };

        /// <summary>pins: edit mode's drag shown again on the drag's timer.</summary>
        private void ShowEditDrag()
        {
            if (Editing && dragKey is not null)
            {
                scene.Edit(EditView());
            }
        }

        /// <summary>
        /// pins: a press on any placed widget, kept in case it becomes a drag. It was a pin, a
        /// separator or a folder and is now everything: a widget is moved where it stands, and edit
        /// mode is what the move turns on rather than what it needs first.
        /// <para>
        /// The kind list that stood here is gone, and nothing replaced it: what is left is "a
        /// placed part of the list", which already leaves out the resize grip, because it is no
        /// part of its own. The one question asked instead is the widget's own
        /// (<see cref="TakesDragItself"/>).
        /// </para>
        /// <para>
        /// <b>A running app's tile moves the whole Apps widget</b>, because <c>Layout.PartOf</c>
        /// maps a tile to the part that holds it and the Apps widget is one part. That is left as
        /// it is on purpose: Windows 11's own taskbar does not let running tiles be reordered
        /// either, so there is no reordering being taken away, and the alternative — a tile that
        /// refuses the drag — would make the Apps widget the one widget on the bar you cannot pick
        /// up. A pin's tile has always moved its pin, and still does.
        /// </para>
        /// <para>
        /// §663: not on a locked bar (<see cref="Arranges"/>): its widgets stay where they stand, and a drag on one is no
        /// way into edit mode. What a widget drags itself was its own before this (the slider) or after it (the tray).
        /// </para>
        /// </summary>
        private void PinPressed(Hit? hit, float x, float y)
        {
            pinDrag = null;
            if (Editing || !Arranges || hit is not { } on || layout.PartOf(on) is var part && (part < 0 || !layout.Placed(part)))
            {
                return;
            }
            if (TakesDragItself(on, part))
            {
                return;
            }
            var block = layout.PartAt(part);
            var along = layout.Vertical ? y : x;
            pinDrag = new PinDrag(layout.Specs[part].Key, new Vector2(x, y), along - block.Left);
            _ = SetCapture(Hwnd);
        }

        /// <summary>
        /// pins: whether the widget under this press takes the drag itself, so no move begins.
        /// <para>
        /// The entry's declaration is the <b>gate</b> (<see cref="BarEntries.TakesDrags"/>): a kind
        /// that never said it takes drags cannot take one by accident, however its widget is
        /// written. Where it did say so, the answer is still the widget's own, because a kind
        /// takes drags <em>somewhere</em> and not everywhere — the volume slider's track is
        /// dragged to set the volume and its speaker is dragged to move the widget.
        /// </para>
        /// <para>
        /// Two kinds declare it, and they have nothing in common type-wise, which is the argument
        /// for the field. The <b>slider</b> has already answered by the time this is reached:
        /// <c>SliderPressed</c> runs first and returns true when it took the pointer, so a press
        /// that got here is one it declined. The <b>tray</b> answers after, because its press is
        /// the dock's (<c>TrayPress</c> → <c>TrayGrabbed</c>, sent once this returns), and its
        /// drag is an icon's — so the hit says it: an icon is the tray's, the room around them is
        /// the widget's and moves it.
        /// </para>
        /// </summary>
        private bool TakesDragItself(Hit hit, int part) =>
            layout.Specs[part].Kind.TakesDrags()
            && hit.Kind switch
            {
                // tray: this icon is about to be grabbed for a reorder or for the overflow.
                HitKind.Tray => true,
                _ => false,
            };

        /// <summary>
        /// pins: the pointer moved while a widget is held. Past the drag threshold it follows the
        /// pointer and the others make room. True while a drag is going on, so hover does nothing.
        /// </summary>
        private bool PinDragMoved()
        {
            if (pinDrag is not { } drag)
            {
                return false;
            }
            if (!drag.Moved)
            {
                if (MathF.Abs(pointer.X - drag.At.X) <= GetSystemMetrics(SM.SM_CXDRAG)
                    && MathF.Abs(pointer.Y - drag.At.Y) <= GetSystemMetrics(SM.SM_CYDRAG))
                {
                    return false;
                }
                drag.Moved = true;
                dock.ClosePreview(this);
                dock.CloseFlyout();
                scene.Tooltip(dock.gfx, null, null);
                scene.Highlight(null, false);
                // edit: moving a widget is arranging the bar, so edit mode turns on and takes the
                // drag over — here, at the threshold, and not at the press: a press that turns out
                // to be a click would otherwise flash the whole edit dress. The same widget goes on
                // being carried (the person, 2026-10-08), now that edit mode is edited in place and
                // nothing shifts under the pointer.
                if (HandOverToEditMode(drag))
                {
                    return true;
                }
                // Escape, which the bar may not have the keys to hear.
                Arm(DRAG, DRAG_MS);
            }
            SetCursor(LoadCursorW(HINSTANCE.NULL, IDC.IDC_SIZEALL));
            ShowPinDrag();
            return true;
        }

        /// <summary>
        /// pins: a widget dragged at rest has passed the threshold: edit mode turns on, and this drag
        /// is edit mode's from here on. True when the bar is now editing; carrying the block, then
        /// <see cref="Moved"/> goes down edit mode's drag for every move after it, the ghost at the
        /// pointer and the landing shown, and the release places it.
        /// <para>
        /// The widget is <b>carried on</b>, not let go (the person, 2026-10-08: dragging a widget into
        /// edit mode keeps the drag). It was let go for a while (2026-10-07) because edit mode then
        /// shrank the screen and moved the bar from under the pointer; edit mode is edited where the
        /// bar stands now, so the block stays under the pointer as it comes.
        /// </para>
        /// <para>
        /// The pin drag is put down <b>before</b> edit mode is asked for, and that ordering is the
        /// whole of it: <c>BeginEditing</c> ends any drag, which releases the capture this one holds,
        /// and <c>CaptureChanged</c> reads a released capture with a pin drag still live as the drag
        /// being lost. Handed over, not lost: it is picked up again in <see cref="AdoptDrag"/>, which
        /// takes the capture back. Refused (the bar has gone), or left with no block to carry, the
        /// release does nothing.
        /// </para>
        /// </summary>
        private bool HandOverToEditMode(PinDrag drag)
        {
            var key = drag.Key;
            pinDrag = null;
            Kill(DRAG);
            // Edit mode draws the carried block itself (Scene.Edit); this drag's own drawing goes,
            // and the press's give with it.
            scene.Drag(dock.gfx, null);
            if (pressed is not null)
            {
                scene.Press(Lit(pressed), false);
                pressed = null;
            }
            if (GetCapture() == Hwnd && sizing is null)
            {
                _ = ReleaseCapture();
            }
            return dock.BeginEditFromDrag(this, key);
        }

        /// <summary>pins: what the bar shows while a widget is dragged along it.</summary>
        private void ShowPinDrag()
        {
            if (pinDrag is { Moved: true } drag)
            {
                scene.Drag(dock.gfx, PinView(drag));
            }
        }

        /// <summary>pins: where the dragged widget would land.</summary>
        private EditView PinView(PinDrag drag)
        {
            var part = layout.PartIndex(drag.Key);
            if (part < 0 || !layout.Placed(part))
            {
                return new EditView();
            }
            var at = pointer;
            var along = layout.Vertical ? at.Y : at.X;
            // The pointer decides where it lands, not the block's middle kept on the bar: a block
            // kept on the bar cannot reach past the end widget's middle when it is the wider of
            // the two, which made the ends unreachable (see the same line in Dock.EditView).
            return new EditView(part, along, drag.Grab, layout.DropSlot(along, part));
        }

        /// <summary>
        /// pins: the button came up. True when a drag ended here, so the click that would follow is
        /// not one: the widget lands where it showed it would.
        /// </summary>
        private bool PinReleased(float x, float y)
        {
            if (pinDrag is not { } drag)
            {
                return false;
            }
            pinDrag = null;
            if (GetCapture() == Hwnd && sizing is null)
            {
                _ = ReleaseCapture();
            }
            if (!drag.Moved)
            {
                return false;
            }
            Kill(DRAG);
            pointer = new Vector2(x, y);
            var view = PinView(drag);
            var part = layout.PartIndex(drag.Key);
            if (part < 0 || view.Dragging is null)
            {
                scene.Drag(dock.gfx, null);
                return true;
            }
            // No Scene.Drag here: the scene the change brings glides the block from where it was let go.
            dock.MoveIslands([drag.Key], this, ListSlot(view.DropIndex, [drag.Key]));
            return true;
        }

        /// <summary>pins: the drag is off (Escape, the mouse lost, the bar going): everything back where it was.</summary>
        public void CancelPinDrag()
        {
            if (pinDrag is not { } drag)
            {
                return;
            }
            pinDrag = null;
            Kill(DRAG);
            if (GetCapture() == Hwnd && sizing is null)
            {
                _ = ReleaseCapture();
            }
            if (drag.Moved)
            {
                scene.Drag(dock.gfx, null);
            }
        }

        // --- Files dragged over the bar -----------------------------------------

        /// <summary>
        /// pins: files over the bar: the line where they would be pinned, the folder they would go
        /// into, or the app's button they would open with (app clicks, <see cref="Dock.DropAt"/>) —
        /// whose window comes forward once they rest on it (<see cref="DropHover"/>).
        /// </summary>
        private void DropMoved(float x, float y)
        {
            if (Editing)
            {
                return;
            }
            var at = new Vector2(x, y);
            var along = layout.Vertical ? at.Y : at.X;
            var (action, part, tile) = dock.DropAt(this, at);
            scene.Drag(dock.gfx, action.Kind switch
            {
                DropKind.IntoFolder => new EditView(DropFolder: part),
                DropKind.OpenWith or DropKind.OpenApp => new EditView(DropTile: tile),
                DropKind.Nothing => null,
                _ => LandingView(layout.DropSlot(along, -1)),
            });
            DropHover(tile >= 0 && TileAt(tile) is { Open.Count: > 0 } ? tile : -1);
        }

        // pins: where files over the bar would land, laid out once per place rather than once per
        // move of the pointer: the bar and the place it was worked out for, and the answer.
        private (Layout Bar, BarSlot Slot, RectF Rect, float[] Lefts)? landing;

        /// <summary>
        /// pins: what files over the bar at <paramref name="slot"/> show: the new pin's cell, at the
        /// size it will have and the place it will land (<see cref="Layout.Landing"/>), with the bar
        /// opening that room for it in its section — never a line between two parts.
        /// Where the bar would have no room to show the pin, the line between the parts says where
        /// it goes.
        /// </summary>
        private EditView LandingView(BarSlot slot)
        {
            if (landing is not { } known || known.Bar != layout || known.Slot != slot)
            {
                var (rect, lefts) = layout.Landing(slot);
                landing = known = (layout, slot, rect, lefts);
            }
            return known.Rect.IsEmpty
                ? new EditView(Insert: slot)
                : new EditView(Landing: known.Rect, Lefts: known.Lefts);
        }

        /// <summary>pins: the files went elsewhere, or landed: the marks go, and nothing waits to come forward.</summary>
        private void DropLeft()
        {
            landing = null;
            scene.Drag(dock.gfx, null);
            DropHoverEnded();
        }

        // --- A widget dragged out of an open folder --------------------------------

        /// <summary>
        /// places: whether this bar is one a widget carried out of a folder is over, to be refused on: the
        /// display's bar; a place only while it is in sight; never an anchor.
        /// </summary>
        public bool Droppable => Place is null ? !IsAnchor : Shown;

        /// <summary>Whether <paramref name="at"/> (screen px) is on this bar's panel.</summary>
        public bool Holds(POINT at)
        {
            var panel = PanelOnScreen();
            return at.x >= panel.left && at.x < panel.right && at.y >= panel.top && at.y < panel.bottom;
        }

        /// <summary>Where a widget let go at <paramref name="at"/> (screen px) would land on this bar.</summary>
        public BarSlot DropSlotAt(POINT at) => layout.DropSlot(AlongAt(at), -1);

        /// <summary>pins: nothing is being dragged over this bar any more.</summary>
        public void ShowNoDrop()
        {
            if (Editing)
            {
                // folders: the line goes; the bar keeps everything else it shows while it is edited.
                scene.Edit(EditView());
                return;
            }
            scene.Drag(dock.gfx, null);
        }

        /// <summary>Where <paramref name="at"/> (screen px) is along this bar, in the layout's own px.</summary>
        private float AlongAt(POINT at)
        {
            var local = Local(at);
            return layout.Vertical ? local.Y : local.X;
        }
    }
}
