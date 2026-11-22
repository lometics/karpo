// edit: the selected widget's own flyout (SelectedFlyout.cs), standing open while it is the one
// selected: one widget selected alone in edit mode, whose kind has a flyout of Karpo's own, has it
// standing open from where it stands — through the settings app the click brought up having the
// keys, through presses on the bar (Dock.PressedOn leaves a look alone in edit mode) and on the dim —
// opened again where the widget's settings or its place change, closed while it is carried and
// opened again where it lands, and closed once another is selected (that one's instead), several or
// none are, or edit mode ends. It is opened as a look: no focus taken, standing through presses
// elsewhere (FlyoutHost.Previewing).
//
// The settings app had a look of its own here too, at a flyout's height while it was changed; the
// heights went with the last flyout that had one (2026-10-11: the keyboard's and VPN's lists are
// folders' lines now).

using System.Text.Json;
using Karpo.Core;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>
    /// Opens the flyout of <paramref name="island"/>, at <paramref name="hit"/> of
    /// <paramref name="window"/>, as its click would, for a look (<see cref="FlyoutHost.Previewing"/>):
    /// no focus taken, nothing of Windows' closed, standing through presses elsewhere.
    /// </summary>
    private void OpenLook(DockWindow window, Hit hit, IIsland island)
    {
        FlyoutHost.Previewing = true;
        try
        {
            // The one table a click goes through; a switch's list is the widget's own rows, which its
            // click hands back (IslandClick).
            if (!OpenWidgetFlyout(window, hit, island, hit.Index))
            {
                IslandClick(window, hit);
            }
        }
        finally
        {
            FlyoutHost.Previewing = false;
        }
    }

    // --- edit: the selected widget's flyout (SelectedFlyout.cs) -----------------------------------

    /// <summary>
    /// edit: the widget whose flyout stands open for the selection, where it was opened from, and its
    /// settings then (as the file has them): opened again when either changes. Null for none.
    /// </summary>
    private (string Key, POINT Anchor, string Settings)? selectedShown;

    /// <summary>edit: the host's timer that looks at the selection's flyout again once the queue is empty: after a drag, its landing.</summary>
    private const uint SELECTED_FLYOUT = 61;

    /// <summary>
    /// edit: whose flyout the selection wants open now (<see cref="SelectedFlyout.For"/>); null for
    /// none. None while a block is carried, nor while a selection rectangle is drawn over the dim, which
    /// would open and close one under it at every widget it ran over: it is looked at once let go.
    /// None while the widget's menu stands, which a click on it opens beside it (§212): looked at
    /// again once a pick from the menu is done.
    /// </summary>
    private string? SelectedFlyoutWanted() =>
        EditMenuStands ? null
        : SelectedFlyout.For(editor is not null, windows.Exists(window => window.CarriedAway) || banding is { Moving: true }, editSelection.Keys, key => SelectedPlace(key) is not null);

    /// <summary>
    /// edit: where the widget keyed <paramref name="key"/> stands on a surface being arranged and shown,
    /// where its kind opens its flyout for a selection (<see cref="SelectedFlyout.Opens"/>): a flyout of
    /// the dock's table, or the list of choices the keyboard's and a listing switch's click hand back.
    /// Null for one in a folder, out of sight, or with none.
    /// </summary>
    private (DockWindow Window, Hit Hit, IIsland Island)? SelectedPlace(string key)
    {
        foreach (var window in windows)
        {
            if (!Arranging(window) || !window.Shown || window.IsAnchor)
            {
                continue;
            }
            var part = window.Layout.PartIndex(key);
            if (part < 0 || !window.Layout.Placed(part))
            {
                continue;
            }
            var hit = Hit.Cell(part, 0);
            if (window.IslandAt(hit) is not { } island)
            {
                return null;
            }
            var own = HasFlyout(window.KindAt(hit));
            return SelectedFlyout.Opens(window.KindAt(hit), own) ? (window, hit, island) : null;
        }
        return null;
    }

    /// <summary>
    /// edit: the selection's flyout follows the selection (<see cref="SelectedFlyout"/>): opened for the
    /// one selected alone, from where it stands, and again where its place or its settings changed or
    /// it was closed meanwhile; closed where none is wanted.
    /// </summary>
    private void SyncSelectedFlyout(bool selectionChanged)
    {
        var wanted = SelectedFlyoutWanted();
        if (wanted is null || SelectedPlace(wanted) is not { } at)
        {
            if (selectedShown is not null)
            {
                selectedShown = null;
                CloseLooks();
            }
            return;
        }
        var anchor = at.Window.WidgetAnchor(at.Hit);
        var settings = SettingsOf(wanted);
        if (selectedShown is { } was && string.Equals(was.Key, wanted, StringComparison.OrdinalIgnoreCase)
            && was.Anchor.x == anchor.x && was.Anchor.y == anchor.y && was.Settings == settings && LookStanding)
        {
            return;
        }
        selectedShown = (wanted, anchor, settings);
        // Another widget's flyout, or a look's, goes as this one opens (PopupOpening).
        OpenLook(at.Window, at.Hit, at.Island);
    }

    /// <summary>edit: the selection's flyout is looked at again once the queue is empty — a drag ended, and its landing is applied first.</summary>
    private void SelectedFlyoutSoon()
    {
        if (editor is not null)
        {
            Arm(SELECTED_FLYOUT, 0);
        }
    }

    /// <summary>Whether a look's flyout stands open: one opened while <see cref="FlyoutHost.Previewing"/> was set.</summary>
    private static bool LookStanding => FlyoutHost.OpenMenus.Any(host => host.Previewed);

    /// <summary>
    /// edit: the looks' flyouts close: the widgets' own (in edit mode a click opens none of them, so any
    /// open is a look's), and the list of choices where it is a look's — never a menu the person opened.
    /// </summary>
    private void CloseLooks()
    {
        CloseIslandPopups();
        if (flyout is { IsOpen: true, Previewed: true })
        {
            CloseFlyout();
        }
    }

    /// <summary>edit: the settings of the widget keyed <paramref name="key"/> as the file has them, to tell a change by; "" for none.</summary>
    private string SettingsOf(string key) =>
        config.FindIsland(Islands.OwnerOf(key), Islands.IdOf(key)) is BarItem island ? JsonSerializer.Serialize(island, DockJson.Default.BarItem) : "";

    private sealed partial class DockWindow
    {
        /// <summary>edit: a block of this window is being carried, past the drag threshold: the selection's flyout stands closed meanwhile.</summary>
        public bool CarriedAway => dragKey is not null && dragMoved;
    }
}
