// The dock's half of the sections' edit bars (EditRail.cs): one beside every section of every
// display that has widgets it cannot show — a bar's three and every place's one — keeping each beside
// its section, routing their windows' messages, and doing what their buttons say.
//
// Since 2026-10-07 an edit bar holds one button, what its section has no room to show, and is there
// only while there is any (EditRailPlan): its "+" and its grid, list and separator buttons went to
// every display's add button and the add list's band (Dock.EditDeck.cs). Close is
// <see cref="EndEdit"/>, the one Escape and the edit bar's "Done" take. A click away no longer ends edit
// mode (the person, 2026-10-07: "remove that you go out of edit mode if you click into the middle"):
// a click on the dim or another window leaves it on.
//
// The edit bars stand beside their sections, towards the middle of the screen, never on them (the
// person, 2026-10-07: "each section gets its own small edit bar … next to the section, not inside
// it"), nor on another surface's bar or a display's own edit bar in its top right corner.

using System.Runtime.InteropServices;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>
    /// The sections' edit bars, by the surface and the section each stands beside, every display's
    /// (EditChrome, §359): made with edit mode, hidden when it ends, and let go of with their surface.
    /// </summary>
    private IEnumerable<((DockWindow Window, BarSection Section) Key, EditRail Rail)> Rails =>
        chromes.Values.SelectMany(chrome => chrome.Rails).Where(entry => entry.Key.Surface is DockWindow)
            .Select(entry => (((DockWindow)entry.Key.Surface, entry.Key.Section), entry.Value));

    /// <summary>
    /// §359: every section's edit bar beside its section, where the sections are now, for the chrome's
    /// model (<see cref="ChromeModels"/>): beside every section of every surface being edited that has
    /// widgets it cannot show. Where two places meet at a corner their edit bars would stand on each other,
    /// so each keeps clear of those already placed (EditRails.Clear): the bars' first, then the rows along
    /// the top and the bottom, then the columns down the sides, which give way — worked out before any is
    /// shown (EditRail.PanelFor), so each is shown once, where it stands. Nor on another surface's bar, nor on
    /// a display's edit bar (<paramref name="taken"/>). None while a folder is open arranged: the bars are
    /// locked under it (§212).
    /// </summary>
    private List<EditRailModel> RailModels(List<RECT> taken)
    {
        var models = new List<EditRailModel>();
        if (BarLocked)
        {
            return models;
        }
        var surfaces = windows
            .Where(window => Arranging(window) && !window.IsAnchor)
            .OrderBy(window => window.Place is null ? 0 : window.Layout.Vertical ? 2 : 1)
            .ToList();
        taken = [.. surfaces.Select(window => window.DropArea()).OfType<RECT>(), .. taken];
        var standing = Rails.ToDictionary(entry => entry.Key, entry => entry.Rail.Shift);
        foreach (var window in surfaces)
        {
            var layout = window.Layout;
            var work = window.DisplayWork();
            var vertical = layout.Vertical;
            foreach (var (section, rect) in window.SectionsOnScreen())
            {
                // The bar says nothing about what it could not fit — a badge on a bar already too
                // crowded is the last thing it needs — so the edit bar of the section it would be
                // in is the only place it appears, and a section with nothing hidden has none.
                var notShown = layout.NotShown.Where(spec => spec.Section == section).ToList();
                if (notShown.Count == 0)
                {
                    continue;
                }
                // Where it stood, so nothing moves when nothing changed; then clear of the others.
                var shift = standing.GetValueOrDefault((window, section));
                if (EditRail.PanelFor(window.UiScale, rect, layout.Edge, work, anyHidden: true, shift) is { } panel)
                {
                    var middle = vertical ? (work.top + work.bottom) / 2 : (work.left + work.right) / 2;
                    var centre = (vertical ? (panel.top + panel.bottom) / 2 : (panel.left + panel.right) / 2) - shift;
                    var gap = (int)MathF.Round(EditRailPlan.MarginDip * window.UiScale, MidpointRounding.AwayFromZero);
                    var clear = EditRails.Clear(panel, shift, taken, vertical, middle >= centre ? 1 : -1, gap);
                    if (clear != shift && EditRail.PanelFor(window.UiScale, rect, layout.Edge, work, anyHidden: true, clear) is { } moved)
                    {
                        panel = moved;
                    }
                    shift = clear;
                    taken.Add(panel);
                }
                // §526: edit mode's own, in Windows' look (SettingsLook), never its bar's.
                models.Add(new EditRailModel(window, section, SettingsLook, window.UiScale, rect, layout.Edge, work, notShown, shift));
            }
        }
        return models;
    }

    /// <summary>The follow hook for good: the dock is closing (the edit bars go with their chrome).</summary>
    private void DisposeRails()
    {
        followHook?.Dispose();
        followHook = null;
    }

    /// <summary>The edit bars in the order the keys walk them: by surface, then along each.</summary>
    private List<((DockWindow Window, BarSection Section) Key, EditRail Rail)> RailsInOrder() =>
        [.. Rails
            .Where(entry => entry.Rail.IsOpen)
            .OrderBy(entry => windows.IndexOf(entry.Key.Window))
            .ThenBy(entry => entry.Key.Section)];

    /// <summary>No edit bar has the keyboard's focus any more: the bar has taken it.</summary>
    private void ClearRailFocus()
    {
        foreach (var (_, rail) in Rails)
        {
            rail.ClearFocus();
        }
    }

    /// <summary>The edit bar whose window, or tooltip's, <paramref name="window"/> is, if any.</summary>
    private ((DockWindow Window, BarSection Section) Key, EditRail Rail)? RailOf(HWND window)
    {
        foreach (var (key, rail) in Rails)
        {
            if (rail.Owns(window))
            {
                return (key, rail);
            }
        }
        return null;
    }

    /// <summary>Whether <paramref name="window"/> is an edit bar's window or its tooltip's.</summary>
    private bool RailWindow(HWND window) => RailOf(window) is not null;

    /// <summary>
    /// A message of an edit bar's window. The tooltip's messages are its own host's and want nothing
    /// from the dock, so only the bar's are read.
    /// </summary>
    private LRESULT? RailMessage(HWND window, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == WM.WM_MOUSEACTIVATE)
        {
            return (LRESULT)MA.MA_NOACTIVATE;
        }
        if (RailOf(window) is not { } found || window != found.Rail.Hwnd)
        {
            return null;
        }
        var happened = found.Rail.Message(msg, wParam, lParam);
        if (happened.Action != EditRailAction.None && editor is not null)
        {
            // The section's surface has the keys: a click on an edit bar never takes them, and its own window
            // cannot have them (WS_EX_NOACTIVATE). Its own focus ring stays where the click was.
            foreach (var (key, rail) in Rails)
            {
                if (key != found.Key)
                {
                    rail.ClearFocus();
                }
            }
            FocusBar(found.Key.Window);
            Report(() => RailAction(found.Key.Window, found.Key.Section, happened));
        }
        // Then the default handling, as the host expects.
        return null;
    }

    /// <summary>What an edit button does, beside <paramref name="section"/> of <paramref name="window"/>.</summary>
    private void RailAction(DockWindow window, BarSection section, EditRailEvent happened)
    {
        if (!windows.Contains(window) || !Arranging(window))
        {
            return;
        }
        switch (happened.Action)
        {
            case EditRailAction.Close:
                LeaveEdit();
                break;
            case EditRailAction.NotShown:
                ShowNotShown(window, section, happened.Anchor);
                break;
        }
    }

    /// <summary>
    /// The widgets the bar holds in <paramref name="section"/> and has no room to show. One is taken
    /// straight to its own menu — the menu is the only thing that fixes it (a narrower width, a move
    /// earlier in the list, or off the bar), and it is the only way to reach a widget at all when it
    /// has no block to be clicked, right-clicked or tabbed to. Several are listed by name first, and
    /// a row goes to the same place.
    /// <para>
    /// There is no button here that fixes it by itself, and none is offered: which widget to give
    /// up for which is the person's decision, and a button that guessed would be wrong most of the
    /// time. What the edit bar owes them is the name and a way in.
    /// </para>
    /// </summary>
    private void ShowNotShown(DockWindow window, BarSection section, POINT anchor)
    {
        var missing = window.Layout.NotShown.Where(spec => spec.Section == section).ToList();
        if (missing.Count == 0)
        {
            return;
        }
        if (missing.Count == 1)
        {
            WidgetMenu(window, missing[0].Key, anchor, unknown: missing[0].Kind.Title());
            return;
        }
        var rows = new List<FlyoutItem>();
        for (var index = 0; index < missing.Count; index++)
        {
            rows.Add(new FlyoutItem(CMD_NOT_SHOWN + (uint)index, missing[index].Kind.Title(), Glyph(missing[index].Kind.Glyph())));
        }
        ShowFlyout(
            new Pending(Topic.NotShown, window) { NotShown = [.. missing] },
            rows,
            FlyoutLayout.List,
            anchor,
            S.DockEditRailNoRoom);
    }

    /// <summary>A row of that list: that widget's own menu, at the same place the list stood.</summary>
    private bool NotShownChosen(Pending what, uint id, POINT anchor)
    {
        if (what.Topic != Topic.NotShown || what.Owner is not { } window
            || id < CMD_NOT_SHOWN || id - CMD_NOT_SHOWN >= (uint)what.NotShown.Count)
        {
            return false;
        }
        var spec = what.NotShown[(int)(id - CMD_NOT_SHOWN)];
        WidgetMenu(window, spec.Key, anchor, unknown: spec.Kind.Title());
        return true;
    }

    // --- The keyboard ---------------------------------------------------------

    /// <summary>
    /// Tab (and Shift+Tab) walk the bar's blocks and then the edit bars' buttons, one edit bar after
    /// the other, as one ring: the bar never wraps on its own, because running off its end is what
    /// reaches the edit bars. The arrows stay where the focus is — an edit bar sits *across* from its
    /// section, not along it, so an arrow along the bar has no business leaving it.
    /// </summary>
    private void RailTab(DockWindow window, int step)
    {
        var order = RailsInOrder();
        if (order.Count == 0)
        {
            window.MoveFocus(step);
            return;
        }
        var at = order.FindIndex(entry => entry.Rail.Focus >= 0);
        if (at >= 0)
        {
            if (order[at].Rail.MoveFocus(step))
            {
                return;
            }
            // MoveFocus let the focus go: on to the next edit bar, or back onto the bar from the end
            // it left by.
            var next = at + Math.Sign(step);
            if (next >= 0 && next < order.Count)
            {
                _ = order[next].Rail.MoveFocus(step);
                return;
            }
            window.StepFocus(step);
            return;
        }
        if (window.StepFocus(step))
        {
            return;
        }
        window.DropFocus();
        _ = order[step > 0 ? 0 : order.Count - 1].Rail.MoveFocus(step);
    }

    /// <summary>An arrow key while an edit bar has the focus; false when none has.</summary>
    private bool RailArrow(int step)
    {
        if (Rails.Select(entry => entry.Rail).FirstOrDefault(rail => rail.Focus >= 0) is not { } strip)
        {
            return false;
        }
        _ = strip.MoveFocus(step, clamp: true);
        return true;
    }

    /// <summary>Enter, Space or a menu key while an edit bar has the focus; false when none has.</summary>
    private bool RailChosen()
    {
        foreach (var (key, rail) in Rails)
        {
            if (rail.Focus >= 0 && rail.Chosen() is { Action: not EditRailAction.None } chosen)
            {
                RailAction(key.Window, key.Section, chosen);
                return true;
            }
        }
        return false;
    }

    private sealed partial class DockWindow
    {
        /// <summary>
        /// The keyboard focus one block along the bar, without wrapping: false where the step runs
        /// off the end, which is the edit bars' cue to take it. With no block focused it comes in
        /// from the end the step leads away from.
        /// </summary>
        public bool StepFocus(int step)
        {
            var order = FocusOrder();
            if (order.Count == 0 || step == 0)
            {
                return false;
            }
            var at = order.IndexOf(FocusedPart);
            var to = at < 0 ? (step > 0 ? 0 : order.Count - 1) : at + Math.Sign(step);
            if (to < 0 || to >= order.Count)
            {
                return false;
            }
            FocusStop(order[to]);
            return true;
        }

        /// <summary>No block of the bar has the focus any more: an edit bar has taken it.</summary>
        public void DropFocus()
        {
            if (focusKey is null && !focusShown)
            {
                return;
            }
            focusKey = null;
            focusShown = false;
            scene.Edit(EditView());
        }

        /// <summary>
        /// edit: this window's sections on the screen, each where its edit bar goes beside it
        /// (<see cref="Layout.SectionRect"/>): a bar's three, a place's one; one of no length has none.
        /// </summary>
        public IEnumerable<(BarSection Section, RECT Rect)> SectionsOnScreen()
        {
            foreach (var section in (BarSection[])[BarSection.Start, BarSection.Middle, BarSection.End])
            {
                if (Place is { } place && place.Section != section)
                {
                    continue;
                }
                var rect = layout.SectionRect(section);
                if (rect.IsEmpty)
                {
                    continue;
                }
                yield return (section, layout.OnScreen(rect));
            }
        }
    }
}
