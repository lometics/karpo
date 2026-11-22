// Tray icons dragged between the bar and the overflow, as the Windows 11
// taskbar lets them be dragged.
//
// An icon held on the bar and moved leaves its place: the others make room, and
// letting go puts it where the gap is. Over the chevron — or anywhere in the
// open overflow — the chevron lights up and letting go hides it. An icon
// dragged out of the open overflow onto the tray shows the same gap and becomes
// a shown icon there. Within the overflow a drag reorders the hidden ones.
// Escape, a drop outside the tray, or losing the mouse leaves everything as it
// was.
//
// As on Windows 11, the chevron stands on the bar for as long as an icon is
// dragged, even with nothing behind it yet, so the first icon can be hidden by a
// drop onto it; and the overflow opens as the drag starts, empty or not, and
// stands open for it, to let the icon go at a place of its choosing in there
// (TrayDragOverflow); it closes with the drag unless it was open before. The icon
// follows the pointer anywhere, across the bar and up into the overflow, drawn in
// a window of its own (TrayCarry). A tray of a set size that has no room for every
// icon it should show puts the rest in the overflow, ahead of the hidden ones
// (TrayOverflowOf).
//
// What a drop means is worked out by TrayDrags (px along the bar) and written
// by TrayLayout (Karpo.Core: the choice per icon and the order), so both are
// tested without a window. Everything here only carries the pointer to them.
//
// An icon dragged out of the overflow onto the bar is "Show on taskbar" from then on, one dragged
// onto the chevron or within the overflow "Show in hidden icon menu" (TrayState). One hidden
// completely shows nowhere to be dragged: the tray's menu in edit mode brings it back (OptionTray, §261).
//
// §347: each tray its own icons (TraySplit, BarItem.TrayIcons). A drag stays in the tray it was taken
// from — along its icons, onto its chevron, into the overflow its chevron opens — and writes that
// tray alone; another tray, on this bar or another, is not somewhere to drop it.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>
    /// A tray icon being dragged: what it is, and whether it was taken out of the overflow — and if
    /// so, from which of its rows (<see cref="Row"/>), -1 otherwise.
    /// </summary>
    private sealed record TrayDragging(string Identity, string Key, bool FromOverflow, int Row = -1)
    {
        /// <summary>The bar and the tray of it the icon was taken from, where it was taken from a bar (a bar may hold two).</summary>
        public (DockWindow Bar, int Part)? From { get; init; }

        /// <summary>§347: the key of the tray it was taken from — off its bar, or out of the overflow its chevron opened —, the one it lands in.</summary>
        public required string Tray { get; init; }

        /// <summary>The tray of <see cref="Over"/> that <see cref="Drop"/> is about; -1 for none.</summary>
        public int Part { get; set; } = -1;

        /// <summary>
        /// Where it would land now: what the bars show, and so what the drop commits. Read again at
        /// the drop it was read without the drag (its own place, the place the last move gave it):
        /// an icon moved along the bar landed by the overflow's rule, a place too far whenever the
        /// pointer was past a cell's middle, and one moved within the overflow a row too far.
        /// </summary>
        public TrayDrop? Drop { get; set; }

        /// <summary>The bar <see cref="Drop"/> is over; null for the open overflow, or for no drop.</summary>
        public DockWindow? Over { get; set; }

        /// <summary>The place among the open overflow's icons it was last shown at; -1 for none (Flyout.DropIndex).</summary>
        public int Gap { get; set; } = -1;

        /// <summary>
        /// The last place it was shown at, kept while the pointer is over no tray and not the overflow
        /// (<see cref="TrayDrags.MoreFor"/>): the bar it is over (null for the overflow), its drop, and
        /// the key of the tray it is in.
        /// </summary>
        public (DockWindow? Over, TrayDrop Drop, string? Tray)? Aim { get; set; }

        /// <summary>
        /// What each bar's trays (by key) take beyond their icons now (Content.TrayMore), the ones at 0
        /// left out; the bars are laid out again when it changes.
        /// </summary>
        public Dictionary<DockWindow, Dictionary<string, int>> Rooms { get; set; } = [];
    }

    private TrayDragging? trayDrag;

    // tray: the overflow a drag opens and keeps open (TrayDragOverflow).
    private TrayDragOverflow? trayKeep;

    // tray: the window the dragged icon goes with the pointer in (TrayCarry); made with the first drag.
    private TrayCarry? trayCarry;

    /// <summary>
    /// §347: the icons of the tray <paramref name="key"/> names, its own (<see cref="TraySplit"/>); none for
    /// no tray. A tray placed since the tray was last read (a bar a display brought) is split as it is asked for.
    /// </summary>
    private TraySplit TrayOf(string? key)
    {
        if (key is null)
        {
            return TraySplit.Empty;
        }
        if (traySplits.TryGetValue(key, out var split))
        {
            return split;
        }
        if (tray is null || config.FindIsland(Islands.OwnerOf(key), Islands.IdOf(key)) is not BarItem { Kind: BarKind.Tray } placed)
        {
            return TraySplit.Empty;
        }
        return traySplits[key] = TraySplit.Of(placed.TrayIcons, TrayHeld(tray.Icons));
    }

    /// <summary>§347: the key of the tray <paramref name="at"/> is in on <paramref name="window"/> (<see cref="ChevronOf"/>); null for a bar with none.</summary>
    private static string? TrayKeyAt(DockWindow window, Hit? at)
    {
        var part = ChevronOf(window.Layout, at).Part;
        return part >= 0 && part < window.Layout.Specs.Count && window.Layout.Specs[part].Kind == BarKind.Tray ? window.Layout.Specs[part].Key : null;
    }

    /// <summary>§347: the icons of the tray <paramref name="at"/> is in on <paramref name="window"/> — an icon's or the chevron's hit, else its first.</summary>
    private TraySplit TrayAt(DockWindow window, Hit? at) => TrayOf(TrayKeyAt(window, at));

    /// <summary>tray: the icon <paramref name="hit"/> is on — its own tray's —, null for none.</summary>
    private TrayIcon? TrayIconAt(DockWindow window, Hit? hit) =>
        tray is not null && hit is { Kind: HitKind.Tray, Index: var index } && TrayAt(window, hit).Shown is var shown && index >= 0 && index < shown.Count
            ? shown[index]
            : null;

    /// <summary>
    /// tray: the icons this bar's tray draws — those the split shows, as many as its room holds (a
    /// tray of a set size may hold fewer).
    /// </summary>
    private int TrayDrawn(DockWindow window, Hit? at = null)
    {
        var layout = window.Layout;
        var part = layout.PartOf(ChevronOf(layout, at));
        var shown = TrayAt(window, at).Shown.Count;
        return part < 0 || !layout.Placed(part) ? shown : Math.Min(shown, layout.PartAt(part).Count);
    }

    /// <summary>
    /// tray: the chevron of the tray <paramref name="at"/> is in — an icon's or the chevron's own hit,
    /// tagged with its part —, else of the bar's first tray. A bar may hold the tray twice, each the
    /// whole tray with its own chevron, and the overflow opens from the one clicked. A hit kept from
    /// an older layout whose part is no tray now is the first's too.
    /// </summary>
    internal static Hit ChevronOf(Layout layout, Hit? at)
    {
        var part = at is { Kind: HitKind.Tray or HitKind.TrayOverflow } tray ? layout.PartOf(tray) : -1;
        if (part < 0 || part >= layout.Specs.Count || layout.Specs[part].Kind != BarKind.Tray || !layout.Placed(part))
        {
            part = layout.PartOf(Hit.TrayOverflow);
        }
        return Hit.TrayOverflow.In(part);
    }

    /// <summary>
    /// tray: what the overflow opened from <paramref name="window"/>'s tray <paramref name="at"/> (the
    /// first where null) holds — the icons that tray has no room for, then the hidden ones
    /// (<see cref="TrayDrags.Overflow{T}"/>) — and how many of the first sort there are.
    /// </summary>
    private (List<TrayIcon> Icons, int Spilled) TrayOverflowOf(DockWindow window, Hit? at = null)
    {
        var drawn = TrayDrawn(window, at);
        var split = TrayAt(window, at);
        return (TrayDrags.Overflow(split.Shown, drawn, split.Hidden), Math.Max(0, split.Shown.Count - drawn));
    }

    /// <summary>Whether a tray icon is being dragged: its bar takes the pointer, and clicks don't count.</summary>
    internal bool DraggingTrayIcon => trayDrag is not null;

    /// <summary>The icons the tray has now, as the rules see them (<see cref="TrayLayout"/>).</summary>
    private List<TrayEntry> TrayEntries() =>
        [.. TrayHeld(tray?.Icons ?? []).Select(icon => new TrayEntry(icon.Identity, icon.Hidden))];

    /// <summary>
    /// tray: the icons the tray holds now as its menu in edit mode lists them (OptionTray, §261), those
    /// hidden completely among them — the menu is the way back for them: each by its identity, named as
    /// Windows' "Other system tray icons" names it (<see cref="TrayIconName"/>), with Windows' own choice
    /// for it and its tooltip, which tells two of one app apart.
    /// </summary>
    private List<TrayMenuIcon> TrayMenuIcons() =>
        [.. TrayHeld(tray?.Icons ?? []).Select(icon => new TrayMenuIcon(icon.Identity, TrayIconName(icon), icon.Hidden, FirstLine(icon.Tip)))];

    /// <summary>tray: the picture of the live icon <paramref name="identity"/> names, as the bar draws it, for its row of the tray's menu; null for none.</summary>
    private FlyoutIcon? TrayPicture(string identity) =>
        (tray?.Icons ?? []).FirstOrDefault(icon => string.Equals(icon.Identity, identity, StringComparison.OrdinalIgnoreCase))?.Icon is { } pixels
            ? new FlyoutIcon.Picture(pixels)
            : null;

    // tray: an app's own names, by executable, read once each (TrayIconName).
    private readonly Dictionary<string, (string? Description, string? Product)> trayAppNames = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// tray: <paramref name="icon"/>'s name in the tray's menu (<see cref="OptionTray.NameOf"/>): what its
    /// app's executable calls itself, its description or its product name, read once per executable; else
    /// its tooltip, else its file's name.
    /// </summary>
    private string TrayIconName(TrayIcon icon)
    {
        if (string.IsNullOrWhiteSpace(icon.Path))
        {
            return OptionTray.NameOf(icon.Identity, icon.Path, icon.Tip, null, null);
        }
        if (!trayAppNames.TryGetValue(icon.Path, out var names))
        {
            try
            {
                var version = System.Diagnostics.FileVersionInfo.GetVersionInfo(icon.Path);
                names = (version.FileDescription, version.ProductName);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            {
                names = (null, null);
            }
            trayAppNames[icon.Path] = names;
        }
        return OptionTray.NameOf(icon.Identity, icon.Path, icon.Tip, names.Description, names.Product);
    }

    /// <summary>
    /// tray: of <paramref name="icons"/>, those the bars' trays hold: all of them, but Windows' own
    /// update icon where a bar shows the status icons, whose update icon stands for it beside the
    /// network, sound and battery icons, where Windows shows it (QuickSettings\Services\
    /// WindowsUpdateIcon.cs). Where no bar shows them the tray shows it as any other icon.
    /// </summary>
    private IReadOnlyList<TrayIcon> TrayHeld(IReadOnlyList<TrayIcon> icons) =>
        WindowsUpdateTray.Held(icons, icon => icon.Path, () => PlacedIslands().Any(entry => entry.Island.Kind == BarKind.Indicators));

    /// <summary>
    /// tray: tells the status icons what Windows' own update icon says now, its hover and the dot on
    /// its picture, or that it has none (<see cref="WindowsUpdateTray"/>).
    /// </summary>
    private static void TellWindowsUpdateIcon(IReadOnlyList<TrayIcon> icons)
    {
        var update = icons.FirstOrDefault(icon => WindowsUpdateTray.Is(icon.Path));
        WindowsUpdateTray.Publish(update is null
            ? null
            : new WindowsUpdateIcon(update.Tip, update.Icon is { } picture ? WindowsUpdateTray.BadgeOf(picture.Data, picture.Width, picture.Height) : UpdateBadge.None));
    }

    /// <summary>The icon of <paramref name="identity"/>, wherever it stands now.</summary>
    private TrayIcon? TrayIconOf(string identity) =>
        (tray?.Icons ?? []).FirstOrDefault(icon => string.Equals(icon.Identity, identity, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A drag begins in the tray <paramref name="trayKey"/> names: from one of its icons on a bar
    /// (<paramref name="from"/>), or from row <paramref name="row"/> of the overflow its chevron opened.
    /// </summary>
    private void BeginTrayDrag(TrayIcon icon, string trayKey, bool fromOverflow, int row = -1, (DockWindow Bar, int Part)? from = null)
    {
        trayKeep = new TrayDragOverflow(wasOpen: flyout is { IsOpen: true } && pending is { Topic: Topic.Overflow });
        trayDrag = new TrayDragging(icon.Identity, icon.Key, fromOverflow, fromOverflow ? row : -1) { From = fromOverflow ? null : from, Tray = trayKey };
        // tray: the chevron stands on the bar while an icon is dragged, as on Windows 11, even with
        // nothing behind it yet: the bars are laid out again with it (Content.Trays).
        if (TrayOf(trayKey).Hidden.Count == 0)
        {
            RequestRebuild();
        }
    }

    /// <summary>
    /// Where the pointer, in screen px, would drop the icon being dragged: over a bar's tray as
    /// <see cref="TrayDrags.OnBar"/> reads it, anywhere over the open overflow as one of its hidden
    /// icons, and nowhere else. The bar it is over, if any, comes back with it.
    /// <para>
    /// The overflow is a menu-mode flyout (it takes the focus, ShowFlyout), and this asked for any
    /// mode but that one, so no point over the overflow was ever a drop: an icon dragged within it
    /// went back where it was. Its rows are the ones it was opened with (<c>Pending.Hidden</c>), and
    /// so is the count; the tray's own hidden icons may have moved on since.
    /// </para>
    /// <para>
    /// The overflow is its panel, not its window: the window keeps room around the panel for the
    /// shadow, and on the bar's side that room reaches down to the chevron's cell, over the top of
    /// the bar beside it — a drop there, on the bar's icons under the overflow, went back into it.
    /// </para>
    /// <para>
    /// A bar is its strip, not its window (<see cref="Layout.AlongOnBar"/>): the window keeps room
    /// over the bar for its tooltips, and the point was measured across from the window's top, so on
    /// a bottom bar the tray itself was never under it — what stopped every drag out of the overflow.
    /// </para>
    /// <para>
    /// §347: only the tray the icon was taken from, and the overflow its chevron opened: another tray
    /// shows its own icons, and is no drop.
    /// </para>
    /// </summary>
    private (DockWindow? Window, TrayDrop? Drop, int Part) TrayTargetAt(POINT screen)
    {
        if (trayDrag is not { } drag)
        {
            return (null, null, -1);
        }
        if (flyout is { IsOpen: true } open && pending is { Topic: Topic.Overflow, Owner: { } owner } overflow && open.OnPanel(screen)
            && TrayKeyAt(owner, overflow.Hit) == drag.Tray)
        {
            var held = drag.FromOverflow ? drag.Row : -1;
            return (null, TrayDrags.InOverflow(open.DropIndex(screen, held, drag.Gap), overflow.Hidden.Count, held, overflow.Spilled), -1);
        }
        var split = TrayOf(drag.Tray);
        foreach (var window in windows)
        {
            var layout = window.Layout;
            var part = layout.PartIndex(drag.Tray);
            if (part < 0 || layout.AlongOnBar(screen) is null)
            {
                continue;
            }
            // tray: where the dragged one stands among the shown icons when it was taken from the bar.
            var heldAt = drag.FromOverflow ? -1 : split.Shown.FindIndex(icon => icon.Key == drag.Key);
            // and the place the last move gave it, which holds a little past its edge (TrayDrags.Hysteresis).
            var current = drag is { Drop: { Area: TrayArea.Shown } last } && drag.Over == window ? last.Index : -1;
            return TrayDrags.OnLayout(layout, screen, split.Shown.Count, split.Hidden.Count, heldAt, current, part) is { } drop
                ? (window, drop, part)
                : (window, null, -1);
        }
        return (null, null, -1);
    }

    /// <summary>The bars show where the icon would land: a gap among the shown icons, or the chevron lit.</summary>
    private void ShowTrayDrag(POINT screen)
    {
        if (trayDrag is not { } drag)
        {
            return;
        }
        var (window, drop, part) = TrayTargetAt(screen);
        AimTrayDrag(window, drop, part);
        foreach (var bar in windows)
        {
            ShowTrayDragOn(bar, window, drop);
        }
        // tray: in the open overflow, the icons there make room where it would land, and the place
        // of one taken from there stays empty (Flyout.DropGap).
        var inOverflow = window is null && drop is not null;
        if (flyout is { IsOpen: true } open && pending is { Topic: Topic.Overflow })
        {
            var held = drag.FromOverflow ? drag.Row : -1;
            drag.Gap = inOverflow ? open.DropIndex(screen, held, drag.Gap) : -1;
            open.DropGap(drag.Gap, held, carried: true);
        }
        CarryTrayIcon(screen);
        KeepOverflowOpen();
    }

    /// <summary>
    /// tray: the dragged icon under the pointer at <paramref name="screen"/>, wherever it is
    /// (TrayCarry), at the size the tray it was taken from draws it.
    /// </summary>
    private void CarryTrayIcon(POINT screen)
    {
        if (trayDrag is not { } drag)
        {
            return;
        }
        var owner = drag.From?.Bar ?? (pending is { Topic: Topic.Overflow, Owner: { } opened } ? opened : null);
        var size = (int)MathF.Ceiling(owner is not null && windows.Contains(owner) ? owner.Layout.TrayIconSize : windows.Count > 0 ? windows[0].Layout.TrayIconSize : 0f);
        try
        {
            trayCarry ??= new TrayCarry(gfx, instance);
            trayCarry.Show(TrayIconOf(drag.Identity)?.Icon, size, screen);
        }
        catch (Exception error)
        {
            Report(error);
        }
    }

    /// <summary>
    /// tray: the overflow opens from the tray the icon was taken from as soon as its bar shows the
    /// chevron — at once, or a layout later where the drag brings it (BeginTrayDrag) —, empty or not,
    /// and stands open for the drag (<see cref="TrayDragOverflow"/>).
    /// </summary>
    private void KeepOverflowOpen()
    {
        if (trayKeep is not { } keep || trayDrag is not { From: { } from } || !windows.Contains(from.Bar))
        {
            return;
        }
        var open = flyout is { IsOpen: true } && pending is { Topic: Topic.Overflow };
        var chevron = ChevronOf(from.Bar.Layout, Hit.TrayOverflow.In(from.Part));
        if (keep.Opens(open, !from.Bar.Layout.ItemRect(chevron).IsEmpty))
        {
            OverflowFlyout(from.Bar, chevron);
        }
    }

    /// <summary>
    /// What <paramref name="bar"/> shows of the drag at <paramref name="drop"/> over
    /// <paramref name="window"/>: the gap where it would land, the dragged icon's own place empty, the
    /// chevron lit for a drop onto it.
    /// </summary>
    private void ShowTrayDragOn(DockWindow bar, DockWindow? window, TrayDrop? drop)
    {
        if (trayDrag is not { } drag)
        {
            return;
        }
        var key = drag.FromOverflow ? null : drag.Key;
        var here = bar == window;
        // §347: the tray it was taken from makes room where the pointer is over it, and keeps the
        // dragged icon's place open where not: it goes with the pointer in a window of its own
        // (TrayCarry), and comes home on a cancel. Every other tray shows its own icons as they stand.
        var over = here ? drag.Part : -1;
        var views = new List<TrayDragView>();
        foreach (var part in bar.Layout.TrayParts.Where(part => bar.Layout.Specs[part].Key == drag.Tray))
        {
            var more = TrayMoreOn(bar, part);
            var gap = part == over && drop is { Area: TrayArea.Shown } place ? place.Index
                // tray: a tray grown for the icon keeps its gap where it was last shown while the
                // pointer crosses to the overflow and back (TrayDragging.Aim).
                : more > 0 && drop is null && drag.Aim is { Over: var aimed, Drop: { Area: TrayArea.Shown } last, Tray: var tray }
                    && aimed == bar && tray == bar.Layout.Specs[part].Key ? last.Index
                : -1;
            if (gap >= 0 || key is not null)
            {
                views.Add(new TrayDragView(key, gap, part, more));
            }
        }
        bar.Scene.TrayDrag(views);
        bar.Scene.Highlight(over >= 0 && drop is { Area: TrayArea.Overflow } ? Hit.TrayOverflow.In(over) : null, false);
    }

    /// <summary>
    /// tray: <paramref name="bar"/> was laid out again in the middle of a drag — the chevron that
    /// stands for it, as it starts — and the bar laid out again shows the drag as it is, the icons going on
    /// from where the last layout had them (Scene.CarryTray) rather than starting at their places.
    /// </summary>
    private void TrayDragRebuilt(DockWindow bar)
    {
        if (trayDrag is null)
        {
            return;
        }
        var screen = Cursor();
        var (window, drop, part) = TrayTargetAt(screen);
        // What the bar shows now is what a drop commits, read against its new layout.
        AimTrayDrag(window, drop, part);
        ShowTrayDragOn(bar, window, drop);
        // tray: the overflow stays over its chevron, which a tray grown or shrunk for the drag moved.
        if (flyout is { IsOpen: true } list && pending is { Topic: Topic.Overflow } open && open.Owner == bar)
        {
            list.Reanchor(bar.Anchor(ChevronOf(bar.Layout, open.Hit)));
        }
    }

    /// <summary>
    /// tray: the dragged icon would land at <paramref name="drop"/> over <paramref name="window"/>'s
    /// tray <paramref name="part"/> now: kept for the drop, and the trays given the cell it brings or
    /// takes away (<see cref="TrayRooms"/>), the bars laid out again where that changed.
    /// </summary>
    private void AimTrayDrag(DockWindow? window, TrayDrop? drop, int part)
    {
        if (trayDrag is not { } drag)
        {
            return;
        }
        (drag.Over, drag.Drop, drag.Part) = (window, drop, part);
        if (drop is { } aimed)
        {
            var key = window is not null && part >= 0 && part < window.Layout.Specs.Count ? window.Layout.Specs[part].Key : null;
            drag.Aim = (window, aimed, key);
        }
        var rooms = new Dictionary<DockWindow, Dictionary<string, int>>();
        var shown = TrayOf(drag.Tray).Shown;
        var heldAt = drag.FromOverflow ? -1 : shown.FindIndex(icon => icon.Key == drag.Key);
        foreach (var bar in windows)
        {
            var layout = bar.Layout;
            // §347: only the tray it was taken from grows or shrinks for it.
            foreach (var tray in layout.TrayParts.Where(part => layout.Specs[part].Key == drag.Tray))
            {
                var key = layout.Specs[tray].Key;
                var draws = heldAt >= 0 && heldAt < Math.Min(shown.Count, layout.PartAt(tray).Count);
                var aimedHere = drag.Aim is { } aim && aim.Over == bar && aim.Tray == key;
                if (TrayDrags.MoreFor(drag.Aim?.Drop, aimedHere, draws) is var more and not 0)
                {
                    if (!rooms.TryGetValue(bar, out var mine))
                    {
                        rooms[bar] = mine = [];
                    }
                    mine[key] = more;
                }
            }
        }
        if (!SameRooms(rooms, drag.Rooms))
        {
            drag.Rooms = rooms;
            RequestRebuild();
        }
    }

    private static bool SameRooms(Dictionary<DockWindow, Dictionary<string, int>> one, Dictionary<DockWindow, Dictionary<string, int>> other) =>
        one.Count == other.Count && one.All(bar => other.TryGetValue(bar.Key, out var theirs)
            && bar.Value.Count == theirs.Count && bar.Value.All(tray => theirs.TryGetValue(tray.Key, out var more) && more == tray.Value));

    /// <summary>tray: what <paramref name="window"/>'s trays take beyond their icons while an icon is dragged (Content.TrayMore); null for nothing.</summary>
    private IReadOnlyDictionary<string, int>? TrayRooms(DockWindow window) =>
        trayDrag is { } drag && drag.Rooms.TryGetValue(window, out var rooms) ? rooms : null;

    /// <summary>tray: what <paramref name="bar"/>'s tray <paramref name="part"/> takes beyond its icons now (<see cref="TrayRooms"/>).</summary>
    private int TrayMoreOn(DockWindow bar, int part) =>
        part >= 0 && part < bar.Layout.Specs.Count && TrayRooms(bar) is { } rooms && rooms.TryGetValue(bar.Layout.Specs[part].Key, out var more) ? more : 0;

    /// <summary>
    /// tray: the drag's timer ticked (DockWindow, DRAG): a pointer resting still is heard without a
    /// move — the overflow opening once the chevron the drag brings stands (KeepOverflowOpen).
    /// </summary>
    private void TrayDragTick()
    {
        if (trayDrag is { FromOverflow: false })
        {
            ShowTrayDrag(Cursor());
        }
    }

    /// <summary>Every bar back to what it shows when nothing is dragged.</summary>
    private void ClearTrayDrag(bool settle = true)
    {
        foreach (var bar in windows)
        {
            if (settle)
            {
                bar.Scene.TrayDrag([]);
            }
            bar.Scene.Highlight(null, false);
        }
    }

    /// <summary>
    /// The icon was let go: it lands where the bars showed it would — the place the last move showed
    /// (<see cref="TrayDragging.Drop"/>), not one read again from the point of the release, which
    /// may not be the point last shown (the drag's timer reads the cursor as it is now, the release
    /// where the button came up). A drop outside the tray and the overflow leaves it where it was, as
    /// a drag off a Windows taskbar button does.
    /// </summary>
    private void EndTrayDrag(bool cancelled)
    {
        if (trayDrag is not { } drag)
        {
            return;
        }
        var (window, drop) = cancelled ? (null, null) : (drag.Over, drag.Drop);
        var split = TrayOf(drag.Tray);
        // tray: the icons of the area as the drop's place counts them, the dragged one out, so the
        // place is kept beside the same neighbours should the tray have changed under the drag.
        IEnumerable<TrayIcon> seen = drop is not { } aimed ? []
            : aimed.Area == TrayArea.Shown ? split.Shown
            : window is null && pending is { Topic: Topic.Overflow } rows ? rows.Hidden.Skip(rows.Spilled)
            : split.Hidden;
        List<string> neighbours = [.. seen.Where(icon => icon.Key != drag.Key).Select(icon => icon.Identity)];
        trayDrag = null;
        trayCarry?.Hide();
        if (flyout is { IsOpen: true } list && pending is { Topic: Topic.Overflow })
        {
            list.DropGap(-1);
        }
        // tray: an overflow this drag opened goes with it, let go anywhere or called off; one open
        // before the drag stays (TrayDragOverflow).
        var keep = trayKeep;
        trayKeep = null;
        if (keep?.Ended() == true)
        {
            CloseFlyout();
        }
        // tray: the chevron that stood only for the drag goes (BeginTrayDrag), and a tray that grew or
        // shrank for it is as long as its icons again; a drop that changes the config lays the bars
        // out again anyway, with the icons where it put them.
        if (split.Hidden.Count == 0 || drag.Rooms.Count > 0)
        {
            RequestRebuild();
        }
        // §347: written into the tray it was taken from, and that one alone.
        var next = config.Clone();
        var changed = !cancelled && drop is { } landing && next.FindIsland(Islands.OwnerOf(drag.Tray), Islands.IdOf(drag.Tray)) is BarItem { Kind: BarKind.Tray } home
            && TrayLayout.Apply(home.TrayIcons ??= new(), TrayEntries(), drag.Identity, landing, neighbours);
        // tray: a drop that changes the order leaves the icons where the drag put them: the bar that
        // is laid out anew glides each from there into its place (Scene), where clearing them first
        // sent them back to the old places for a frame, and on from there. Anything else comes home.
        ClearTrayDrag(settle: !changed);
        if (!changed || drop is not { } landed)
        {
            return;
        }
        // Saved, and Windows' own record told the same (PromoteChoices, from Apply).
        Apply(next);
        // tray: an overflow standing open shows its icons as they are now, in their new order, or
        // closes when none are left; its rows were the ones it opened with.
        // A drop onto the bar closes it anyway (TrayDragFromOverflow).
        if (landed.Area == TrayArea.Overflow && flyout is { IsOpen: true } && pending is { Topic: Topic.Overflow, Owner: { } owner } standing
            && windows.Contains(owner))
        {
            if (TrayOverflowOf(owner, standing.Hit).Icons.Count == 0)
            {
                CloseFlyout();
            }
            else
            {
                OverflowFlyout(owner, standing.Hit);
            }
        }
    }

    // tray: Windows' choice for each live icon as last read, to tell a change to it (WindowsChoices).
    private readonly WindowsChoices windowsChoices = new();

    /// <summary>
    /// tray: the choices a tray of <paramref name="next"/> makes that it did not in <paramref name="before"/>,
    /// every other tray keeping such an icon where it shows it now (§347, <see cref="TrayLayout.NewChoices"/>):
    /// what Windows' own records are to be told (<see cref="PromoteChoices"/>). Asked by <see cref="Apply"/>
    /// before it saves, so a choice made in a tray's menu is told too.
    /// </summary>
    private List<(string Identity, bool Promoted)> TrayChoices(Config before, Config next) =>
        tray is null ? [] : TrayLayout.NewChoices(before, next, identity => TrayIconOf(identity)?.Hidden);

    /// <summary>
    /// tray: <paramref name="choices"/> written into Windows' own records (IsPromoted) as a drag on
    /// Windows' taskbar or Settings writes them, so Windows and Karpo agree on every icon chosen in
    /// either. The key's watch reads them back, which is Karpo's own word and changes nothing
    /// (<see cref="WindowsChoices.Told"/>, <see cref="FollowWindowsChoices"/>).
    /// </summary>
    private void PromoteChoices(List<(string Identity, bool Promoted)> choices)
    {
        if (tray is null)
        {
            return;
        }
        foreach (var (identity, shown) in choices)
        {
            if (tray.Promote(identity, shown))
            {
                windowsChoices.Told(identity, shown);
            }
        }
    }

    /// <summary>
    /// tray: Windows' own choice for a live icon changed while Karpo ran (Settings, Other system tray
    /// icons): the newest word on it becomes every tray's state for it — on the taskbar, or in the
    /// hidden icon menu —, saved like a drag's. An icon a tray hides completely stays so there
    /// (TrayLayout.FollowWindows). True when the config changed (and was applied).
    /// </summary>
    private bool FollowWindowsChoices()
    {
        if (tray is null)
        {
            return false;
        }
        var changes = windowsChoices.Changes(tray.PromotionReads, (tray.Icons).Select(icon => (icon.Key, icon.Identity, !icon.Hidden)));
        if (changes.Count == 0)
        {
            return false;
        }
        var next = config.Clone();
        var changed = false;
        foreach (var (identity, promoted) in changes)
        {
            changed |= TrayLayout.FollowWindows(next, identity, promoted);
        }
        if (!changed)
        {
            return false;
        }
        Console.Error.WriteLine($"Karpo tray: Windows' choice followed for {string.Join(", ", changes.Select(change => $"{change.Identity}={change.Promoted}"))}");
        Apply(next);
        return true;
    }

/// <summary>
    /// tray: one of the overflow's hidden icons being dragged. The bars show where it would land,
    /// and a drop onto a bar's tray closes the overflow, as choosing an icon in it does.
    /// </summary>
    private void TrayDragFromOverflow(uint id, POINT at, bool drop, bool cancelled)
    {
        if (pending is not { Topic: Topic.Overflow } what || id < CMD_TRAY || id - CMD_TRAY >= (uint)what.Hidden.Count)
        {
            return;
        }
        var row = (int)(id - CMD_TRAY);
        var icon = what.Hidden[row];
        if (cancelled)
        {
            EndTrayDrag(cancelled: true);
            return;
        }
        if (trayDrag is null)
        {
            // §347: of the tray whose chevron opened it, the one it lands in.
            if (what.Owner is not { } owner || !windows.Contains(owner) || TrayKeyAt(owner, what.Hit) is not { } home)
            {
                return;
            }
            // Its button went down in the overflow (OverflowPointer): a drag, so no click (TrayClicks).
            trayClicks.Dragged();
            BeginTrayDrag(icon, home, fromOverflow: true, row);
        }
        if (!drop)
        {
            ShowTrayDrag(at);
            return;
        }
        // The drop is what the last move showed (EndTrayDrag).
        var toBar = trayDrag is { Drop.Area: TrayArea.Shown };
        EndTrayDrag(cancelled: false);
        if (toBar)
        {
            CloseFlyout();
        }
    }

    private sealed partial class DockWindow
    {
        /// <summary>A tray icon held down, in case the pointer moves far enough to drag it (<see cref="TrayClicks.Moved"/>).</summary>
        private sealed class TrayGrab(TrayIcon icon, int part)
        {
            public TrayIcon Icon { get; } = icon;

            /// <summary>The tray of the bar it was pressed in, where the bar holds two.</summary>
            public int Part { get; } = part;

            public bool Moved { get; set; }
        }

        private TrayGrab? trayGrab;

        /// <summary>Where the pointer last was on this bar, in its px.</summary>
        internal Vector2 PointerAt => pointer;

        /// <summary>Whether an icon is being dragged from this bar: its hover and its click are off.</summary>
        public bool TrayDragging => trayGrab is { Moved: true };

        /// <summary>A press on a tray icon, kept in case it becomes a drag (its app has heard nothing yet, TrayClicks).</summary>
        internal void TrayGrabbed(TrayIcon icon, int part)
        {
            trayGrab = new TrayGrab(icon, part);
            _ = SetCapture(Hwnd);
        }

        /// <summary>The pointer moved while an icon is held: past the threshold it leaves its place. True while it does.</summary>
        private bool TrayDragMoved()
        {
            if (trayGrab is not { } grab)
            {
                return false;
            }
            if (!grab.Moved)
            {
                // Whether it is a drag yet is the one rule's, which then sends its app no click.
                if (!dock.trayClicks.Moved(ScreenPoint(pointer), GetSystemMetrics(SM.SM_CXDRAG), GetSystemMetrics(SM.SM_CYDRAG)))
                {
                    return false;
                }
                if (grab.Part < 0 || grab.Part >= layout.Specs.Count)
                {
                    return false;
                }
                grab.Moved = true;
                dock.BeginTrayDrag(grab.Icon, layout.Specs[grab.Part].Key, fromOverflow: false, from: (this, grab.Part));
                scene.Tooltip(dock.gfx, null, null);
                // Escape, which the bar may not have the keys to hear.
                Arm(DRAG, DRAG_MS);
            }
            SetCursor(LoadCursorW(HINSTANCE.NULL, IDC.IDC_SIZEALL));
            dock.ShowTrayDrag(ScreenPoint(pointer));
            return true;
        }

        /// <summary>The button came up over the bar. True when a drag ended, so the tray click that would follow is none.</summary>
        private bool TrayReleased(float x, float y)
        {
            if (trayGrab is not { } grab)
            {
                return false;
            }
            trayGrab = null;
            if (GetCapture() == Hwnd && sizing is null)
            {
                _ = ReleaseCapture();
            }
            if (!grab.Moved)
            {
                return false;
            }
            Kill(DRAG);
            dock.EndTrayDrag(cancelled: false);
            return true;
        }

        /// <summary>The drag is off (Escape, the mouse lost, the bar going): everything back where it was.</summary>
        public void CancelTrayDrag()
        {
            var grab = trayGrab;
            trayGrab = null;
            if (grab is null)
            {
                return;
            }
            // A press called off is no click either.
            dock.trayClicks.Forget();
            Kill(DRAG);
            if (GetCapture() == Hwnd && sizing is null)
            {
                _ = ReleaseCapture();
            }
            if (grab.Moved)
            {
                dock.EndTrayDrag(cancelled: true);
            }
        }

        /// <summary>A point in this bar's px, on screen.</summary>
        internal POINT ScreenPoint(Vector2 at) => new() { x = layout.X + (int)at.X, y = layout.Y + (int)at.Y };
    }
}
