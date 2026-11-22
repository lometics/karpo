// folders: §623 (the person, 2026-11-09: "it should be possible to actually set a background for the whole screen when a
// folder is open. that way you can darken everything else when a folder is open for example. this background should be
// applied to all the screens when the folder is open."): the Appearance's Flyouts › "Background while a folder is open"
// (Tone.Backdrop; none by default, so nothing changes unless it is set) laid over every display while a folder stands open
// at rest — over every other app, under Karpo's bars and the folder (Stratum.Dim, ZPlan) —, fading in and out by the one
// motion as the folder opens and closes (at once with animations off).
//
// It is edit mode's dim's layer (EditShade): one full-screen window a display, kept for the dock's life once made, tinted the
// Appearance's colour of the taskbar's mode. Not in edit mode, which has its own dim (§612; a folder kept open as edit mode
// ends gets its backdrop as the dim goes), nor in high contrast, where Windows' system colours are the only ones.
//
// A press on it closes the folder, as a press anywhere outside it does (the popups' hook, LightDismiss: on a window of
// Karpo's, the folder closes and gives the focus back), and goes no further: the backdrop is the window under the pointer,
// so nothing under it hears the press. It never takes the activation (WS_EX_NOACTIVATE, MA_NOACTIVATE): the folder keeps
// the keys until it closes.
//
// Reconciled as each of the dock's frames ends (PresentChrome), from what the frame left: a folder opened, closed, switched
// for another, edit mode begun or ended, a display come or gone, the colour changed — one path for all of them.

using System.Runtime.InteropServices;
using Karpo.Core;
using TerraFX.Interop.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Dock;

/// <summary>§623: when a folder's backdrop stands, and in which colour (pure; see Dock.Backdrop.cs).</summary>
internal static class FolderBackdrop
{
    /// <summary>
    /// The colour laid over every display: <paramref name="flyouts"/>' backdrop (the Appearance's Flyouts as worn) while a
    /// folder is open (<paramref name="folderOpen"/>) out of edit mode (<paramref name="editing"/>) and out of high contrast;
    /// null for none — none set, None, or clear.
    /// </summary>
    public static Argb? Of(bool folderOpen, bool editing, bool highContrast, PartPaints flyouts) =>
        folderOpen && !editing && !highContrast ? flyouts.BackdropColor : null;
}

public sealed unsafe partial class Dock
{
    /// <summary>§623: each display's backdrop layer, by the display's id; made the first time one is shown there.</summary>
    private readonly Dictionary<string, EditShade> backdrops = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>§623: what the backdrops were last laid as: the colour and the displays (null: none shown).</summary>
    private string? backdropLaid;

    /// <summary>§623: whether a backdrop stands now (do not disturb hears it as a dim, <see cref="TellDims"/>).</summary>
    private bool backdropShown;

    /// <summary>
    /// §623: the backdrops reconciled to what the frame left (see the top of the file): over every display in the Appearance's
    /// colour while a folder is open at rest, fading in by the taskbar's motion; gone, fading out, otherwise. Nothing where
    /// nothing changed.
    /// </summary>
    private void PresentBackdrop()
    {
        try
        {
            var open = editor is null && openFolders.Exists(each => each.Flyout.IsOpen);
            Argb? tint = null;
            if (open)
            {
                var worn = TaskbarLook;
                tint = FolderBackdrop.Of(open, editing: false, worn.HighContrast, worn.Worn.Flyouts);
            }
            var displays = tint is null ? [] : EditedDisplays;
            var laid = tint is { } colour ? colour + "|" + string.Join(';', displays.Select(display => display.Id + "=" + display.Bounds)) : null;
            if (laid == backdropLaid)
            {
                return;
            }
            backdropLaid = laid;
            backdropShown = tint is not null;
            var motion = look.Motion;
            if (tint is not { } shown)
            {
                foreach (var shade in backdrops.Values)
                {
                    shade.Hide(motion);
                }
                return;
            }
            var ink = Color.FromArgb(shown.A, shown.R, shown.G, shown.B);
            foreach (var display in displays)
            {
                if (!backdrops.TryGetValue(display.Id, out var shade))
                {
                    shade = new EditShade(gfx, instance, GCHandle.ToIntPtr(cell), ink, EditShade.BackdropTitle);
                    backdrops[display.Id] = shade;
                }
                shade.Tint(ink);
                shade.Show(display.Bounds, dimmed: true, motion);
            }
            // A display gone: its layer with it.
            foreach (var (id, shade) in backdrops.ToList())
            {
                if (!displays.Exists(display => string.Equals(display.Id, id, StringComparison.OrdinalIgnoreCase)))
                {
                    backdrops.Remove(id);
                    shade.Dispose();
                }
            }
        }
        catch (Exception error)
        {
            Report(error);
        }
    }

    /// <summary>§623: whether <paramref name="window"/> is a backdrop's.</summary>
    private bool BackdropWindow(HWND window) => window != HWND.NULL && backdrops.Values.Any(shade => shade.Hwnd == window);

    /// <summary>
    /// §623: a message of a backdrop's window. It never takes the activation, so the folder keeps the keys; a press on it closes
    /// the folder open — the popups' hook has, as a press on a window of Karpo's (LightDismiss), where it heard it — and is
    /// used up there, nothing under it hearing it.
    /// </summary>
    private LRESULT? BackdropMessage(uint msg)
    {
        switch (msg)
        {
            case WM.WM_MOUSEACTIVATE:
                return (LRESULT)MA.MA_NOACTIVATE;
            case WM.WM_LBUTTONDOWN or WM.WM_RBUTTONDOWN or WM.WM_MBUTTONDOWN:
                if (editor is null)
                {
                    CloseFolder();
                }
                return (LRESULT)0;
            case WM.WM_LBUTTONUP or WM.WM_RBUTTONUP or WM.WM_MBUTTONUP or WM.WM_LBUTTONDBLCLK or WM.WM_RBUTTONDBLCLK or WM.WM_MBUTTONDBLCLK:
                return (LRESULT)0;
        }
        return null;
    }

    /// <summary>§623: the backdrops go with the dock.</summary>
    private void DisposeBackdrops()
    {
        foreach (var shade in backdrops.Values)
        {
            shade.Dispose();
        }
        backdrops.Clear();
        backdropLaid = null;
        backdropShown = false;
    }
}
