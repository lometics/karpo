// menus: the dock's side of a menu's session (docs\widgets.md §356, §374; MenuSessions.cs): its rows read again
// from the config as it is now (MenuAgain, Pending.Again: after a pick, after another language, after the bars
// were made again under it), and a menu the dock closed for a dialog of Windows' shown again where it stood
// (Resume). Every other opening is the person's, and is at the menu's top (the person, 2026-10-28: "the
// settings menus dont always open at the root level although they should").

using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>
    /// §374: where the menu the dock shows again stood as it closed for a dialog of Windows' (a colour mixed, a
    /// pin's target browsed; <see cref="MenuBrowse"/>), while <see cref="Resume"/> shows it: the menu
    /// <see cref="ShowFlyout"/> shows then opens there. Null for every other opening, which is at the menu's top.
    /// </summary>
    private MenuMemory? resuming;

    /// <summary>
    /// §374: <paramref name="show"/>, which shows a menu again, shows it where <paramref name="stood"/> says it
    /// stood (<see cref="Flyout.Recall"/>): the same menu carried on after the dock's own dialog, not an opening.
    /// </summary>
    private void Resume(MenuMemory? stood, Action show)
    {
        resuming = stood;
        try
        {
            show();
        }
        finally
        {
            resuming = null;
        }
    }

    /// <summary>
    /// §356: the open menu <paramref name="what"/> shown again where it stands with its rows read again from
    /// the config as it is now (<see cref="Pending.Again"/>): its levels found again by their rows — by their
    /// ids where their words changed (a layout renamed in its level, another language; §365) —, the face at
    /// <paramref name="litFace"/> lit where one moved. Nothing where it is not the open menu, or reads nothing again.
    /// </summary>
    private void MenuAgain(Pending what, int? litFace = null, string? litToggle = null, (string Key, int At)? litThing = null)
    {
        // §523, §622: not for a copy of the config worn a moment (a colour mixed, a choice tried on): the menu shows it already,
        // and its rows stand still under the pointer.
        if (disposed || wearing || wornOver is not null || flyout is not { IsOpen: true } list || !ReferenceEquals(pending, what) || what.Again is not { } again)
        {
            return;
        }
        List<OptionPick> picks = [];
        var (rows, type) = again(picks);
        what.Options.Clear();
        what.Options.AddRange(picks);
        list.TypeTitle = type;
        // §411 (R5): the face, the toggle or (§472) the layout just added lit, in its list.
        var lit = litFace is { } face ? OptionMenu.FaceRowOf(picks, face)
            : litToggle is { } toggle ? OptionMenu.ListRowOf(picks, toggle)
            : litThing is { } thing ? OptionMenu.ThingRowOf(picks, thing.Key, thing.At)
            : null;
        // §396: in its window's look as it is now: a colour picked in it is what it wears at once.
        list.Refresh(OptionMenu.Updated(list.ShownItems, rows), lit, look: what.Owner is { } window ? ShownIn(what, window) : null);
    }

    /// <summary>
    /// §356: <paramref name="window"/> is going, and the open menu stood on it: a configuration menu is handed
    /// on to the window of its display that stands now (<see cref="MenuSessions.Successor"/>, among
    /// <paramref name="next"/>) and stays open; any other closes, as does one whose display has no window left.
    /// §357: one over a folder too, with its folder, which the arrange hands on to the window that shows it
    /// then (<see cref="FoldersFollow"/>: the menu moves beside its line there, or closes with the folder
    /// where none shows it). Whether it was handed on.
    /// </summary>
    private bool HandOn(DockWindow window, IReadOnlyList<DockWindow>? next)
    {
        if (pending is not { Topic: Topic.LayoutEditor, Again: not null } what || what.Owner != window || next is null || !FlyoutOpen
            || MenuSessions.Successor(window, next, each => each.Id, each => each.HoldsAppbar) is not { } successor)
        {
            return false;
        }
        what.Owner = successor;
        handedOn = true;
        return true;
    }

    // §356: a menu was handed on to another window by the arrange under way: its rows are read again after it.
    private bool handedOn;

    /// <summary>
    /// §396: every popup standing open dressed again in its window's look as it is now — each open folder in
    /// place (<see cref="FolderFlyout.Relook"/>), the open menu read again (<see cref="MenuAgain"/>) — after a
    /// change of how things look that built nothing (<see cref="Recolour"/>) or Windows' own look changed. A
    /// build does the same as it goes (Rebuild, Arrange's end).
    /// </summary>
    private void Relooked()
    {
        foreach (var open in openFolders)
        {
            open.Flyout.Relook(open.Window.Look);
        }
        if (pending is { } menu)
        {
            MenuAgain(menu);
        }
    }

    /// <summary>§430: the click being handled: what it went down on, and the openers of the popups its press closed.</summary>
    private readonly Openers openers = new();

    /// <summary>
    /// §430: a button (<paramref name="button"/>) went down on <paramref name="part"/> of <paramref name="window"/> (a bar's
    /// hit, an edit bar's button, a folder's line or "+"; null for the window's room), at <paramref name="screen"/>: what
    /// its click opens is that thing's (<see cref="Pending.Opener"/>), and the popup closed for this very press before it
    /// got here — heard by the popups' hook at this point, dismissed since — is one it closed, whatever closed it. §522: on
    /// every window: a folder's, whose presses the hook never leaves to it, and a bar's and an edit bar's too, whose it
    /// leaves them unless the dock was busy as it heard the press (<see cref="IsBar"/>). §525: and closed by the click's
    /// activation too, on a window that takes it (the edit bar's open panel), the menu going as it lost the keys. What it
    /// went down on, and that popup (null for none).
    /// </summary>
    private (PopupOpener Press, Pending? Closed) Pressing(HWND window, object? part, PressButton button, POINT screen)
    {
        // §575: a press anywhere but on an edit bar or its flyout closes the flyout open, as a click elsewhere closes a popup;
        // §576 but on the widget whose flyout it is, which its click keeps open.
        addClosed = null;
        if (panelMemory.Page is not null && !chromes.Values.Any(chrome => chrome.Deck.Owns(window)) && !OnItsWidget(window, part))
        {
            // §576: a "+" whose add page this closes only closes it (OpenAdd).
            addClosed = panelMemory.Page == PanelPage.Add ? adding?.Plus : null;
            ClosePanel(now: false);
        }
        var press = new PopupOpener((nint)window, part, button);
        var heard = FlyoutHost.Heard;
        FlyoutHost.Heard = null;
        var closed = heard is { } hook && Math.Abs(hook.At.x - screen.x) <= 2 && Math.Abs(hook.At.y - screen.y) <= 2
            && dismissed is { What: var what, At: var when } && unchecked((int)(when - hook.Tick)) >= 0 ? what : null;
        openers.Pressed(press, closed?.Opener);
        return (press, closed);
    }

    /// <summary>
    /// §576: whether <paramref name="part"/> of <paramref name="window"/> (a bar's hit, a folder's line by its key) is a widget
    /// selected while its entry's flyout stands open: a press on it keeps the flyout, its click opening it all the same.
    /// </summary>
    private bool OnItsWidget(HWND window, object? part) =>
        panelMemory.Page == PanelPage.Widget && editSelection.Contains(part switch
        {
            string line => line,
            Hit hit when windows.Find(each => each.Hwnd == window) is { } bar && bar.Layout.PartOf(hit) is var at and >= 0 => bar.Layout.Specs[at].Key,
            _ => null,
        });

    /// <summary>Where a press's <paramref name="lParam"/> (client px of <paramref name="window"/>) is on the screen.</summary>
    private static POINT ScreenOf(HWND window, LPARAM lParam)
    {
        var at = new POINT { x = (short)((nint)lParam & 0xFFFF), y = (short)(((nint)lParam >> 16) & 0xFFFF) };
        _ = ClientToScreen(window, &at);
        return at;
    }
}
