// The dock's part of the switchers: what a layout chosen in the layouts' line does, and what the
// desktops' line reads and does through the dock (DesktopsService).
//
// Both were switchers on the bar whose click opened a list of their own until each became a preset
// (2026-10-11, docs\widgets.md §116 and §127): a folder wearing the name of the desktop you are on,
// or of the layout this bar shows, whose line lists them all. A row chosen in either comes back
// through the folder (Dock.Folders.cs, FolderForward), and the order of a switch is the reason:
//
//   1. The folder closes itself while the bar it was opened from is still the bar it was opened from.
//   2. The dock closes anything else of Karpo's still standing over that bar.
//   3. Only then does it switch, which for a layout replaces every widget on the bar, the folder
//      the line stood in included.
//
// The layouts' rows are the bar's own right-click menu's (Dock.LayoutRows), and choosing one runs
// ChooseLayout, the very method that menu runs: one answer to "how do I change layout", in two
// places. The desktops' line is made per folder and cannot be handed the dock, so it reads and acts
// through DesktopState.Service, which is the dock's here.

using Karpo.Core;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>
    /// What the desktop you are on may have done to it, which is what the desktops face's right
    /// click is always about: it names that desktop and no other.
    /// </summary>
    private Switchers.DesktopActions CurrentDesktopActions() => desktops is { } known
        ? Switchers.ActionsFor(known.Current, known.Current, known.Count, DesktopControl.Available())
        : Switchers.ActionsFor(0, 0, 0, shell: false);

    /// <summary>
    /// layouts: a layout chosen in the layouts' line, once its folder has gone. Everything else of
    /// Karpo's that stands over this bar goes before the switch, because a layout switch builds the
    /// bar again from another list of widgets; then the bar's own menu's act, unchanged — the same
    /// method, so the same rules about which layout a display shows and what a desktop remembers.
    /// </summary>
    private void SwitchLayout(DockWindow window, int layout)
    {
        CloseFolder();
        if (!windows.Contains(window))
        {
            // The display went while the folder was open.
            return;
        }
        ClosePreview(window);
        window.Scene.Tooltip(gfx, null, null);
        ChooseLayout(window, layout);
    }

    /// <summary>
    /// desktops: a desktop chosen in the desktops' line, once its folder has gone (without giving the
    /// focus back: that window is on the desktop being left). Anything else of Karpo's over the bar
    /// goes first, as it did for the switcher's row.
    /// </summary>
    private void SwitchDesktop(DockWindow window, int desktop)
    {
        CloseFolder();
        if (windows.Contains(window))
        {
            ClosePreview(window);
            window.Scene.Tooltip(gfx, null, null);
        }
        desktops?.Switch(desktop);
        // Straight away rather than waiting for the registry: Switch already knows where it went (it
        // holds that for a second), so a later read reports nothing changed and nothing would redraw
        // the face. Then again in a moment, for what Explorer writes.
        RefreshDesktopApplications();
        CheckDesktopsSoon();
    }

    /// <summary>
    /// The desktops' lines are told: a window came or went, the desktops changed, or the config did,
    /// for what is open on each. Their rows are read afresh each build, so this is all it takes.
    /// </summary>
    private void RefreshSwitcher() => desktopsService?.Tell();

    /// <summary>desktops: what the desktops' line reads and acts through, set as the dock starts (DesktopState.Service).</summary>
    private DesktopsService? desktopsService;

    /// <summary>
    /// desktops: the dock's desktops, as the desktops' line reads and changes them. The apps are those
    /// on the display the open folder stands on, as the switcher's list showed them; each change is
    /// read back at once (CheckDesktops), which tells the line.
    /// </summary>
    private sealed class DesktopsService(Dock dock) : IDesktops
    {
        public event Action? Changed;

        /// <summary>The desktops were read again: every line showing them lays itself out again.</summary>
        public void Tell() => Changed?.Invoke();

        public DesktopStrip Strip() =>
            (dock.openFolders.LastOrDefault()?.Window ?? dock.windows.FirstOrDefault())?.DesktopsNow() ?? new DesktopStrip([], 0);

        public bool Shell => DesktopControl.Available();

        public bool Add()
        {
            // Through the shell's own call where it can be made, which leaves you where you are, as
            // Task View's "New desktop" does; else Ctrl+Win+D, which goes there too.
            if (!DesktopControl.Create())
            {
                Desktops.NewDesktop();
                dock.CheckDesktopsSoon();
                return true;
            }
            dock.CheckDesktops();
            dock.CheckDesktopsSoon();
            return true;
        }

        public bool Remove(int index)
        {
            if (dock.desktops?.Remove(index) != true)
            {
                return false;
            }
            dock.CheckDesktops();
            dock.CheckDesktopsSoon();
            return true;
        }

        public bool Move(int from, int to)
        {
            if (dock.desktops?.Move(from, to) != true)
            {
                return false;
            }
            // Read again at once rather than waiting for the watch, so the line shows Windows' order.
            dock.CheckDesktops();
            dock.CheckDesktopsSoon();
            return true;
        }

        public bool Rename(int index, string name)
        {
            if (dock.desktops is not { } known || index < 0 || index >= known.Ids.Count || !DesktopControl.Rename(known.Ids[index], name))
            {
                return false;
            }
            dock.CheckDesktops();
            return true;
        }
    }
}
