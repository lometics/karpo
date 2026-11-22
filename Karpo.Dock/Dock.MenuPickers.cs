// menus: what a widget's menu's pickers ask of the dock beyond a pick (MenuPickers.cs, docs\widgets.md §224):
//
//  - **What a pin opens**, chosen in Windows' open dialog (TargetDialog, a file or a folder, as Settings' "Browse…"), owned
//    by the bar the menu stood on and shown once the menu has closed; what it chose is set as Settings sets it
//    (OptionPickers) and saved as every change of the dock's is, and the widget's menu comes back where it stood
//    (Dock.Resume, §374), so the person carries on in it.
//  - **A colour of one's own** (§523), mixed in the colour's picker itself (Flyout.Mixer.cs): as it is dragged, each colour
//    is worn by the bars and the widgets a moment — the config as it would be, applied and not saved, no step of the undo,
//    the menu left as it stands —; let go (or the keys gone from the picker, or the menu closed meanwhile), the colour is set
//    on the config as it was before the drag, saved as one change, and the first recent colour; Escape puts the config the
//    drag began on back. The same for every colour row: a widget's, a folder face's, a bar's looks', ⚙'s (MixedRow). §622 worn
//    as a choice under the pointer is tried on (Dock.TryOn.cs): one copy of the config worn a moment, whatever wears it.
//    §224 to §522 mixed it in Windows' own colour dialog (ChooseColor), which had no alpha and stood over the menu.

using Karpo.Core;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>
    /// menus: what the pickers of the widget <paramref name="key"/> are read from and written to: <paramref name="of"/>,
    /// in the mode its taskbar reads in (<see cref="ReadsDark"/>), its layout's behaviour (§345). §396: a line of a
    /// folder's, the folder it stands in. §589: whether its bar stands on a display's side.
    /// </summary>
    private OptionPlace PickerPlace(Config of, string key)
    {
        var owner = Islands.OwnerOf(key);
        var id = Islands.IdOf(key);
        var cut = id.IndexOf('/');
        var folder = cut > 0 ? of.FindIsland(owner, id[..cut]) as BarItem : null;
        var layout = of.LayoutOf(owner);
        var side = windows.Find(window => string.Equals(Config.DisplayOwner(window.Screen.Id), owner, StringComparison.OrdinalIgnoreCase))?.Layout.Vertical == true;
        return new(of, ReadsDark(of)) { Layout = layout, Folder = folder, Side = side };
    }

    /// <summary>
    /// §547, §694 (the person, 2026-11-12: "the taskbar again does not care about the background value. it just shows as
    /// white"): the mode the settings edit and show <paramref name="of"/>'s Appearance in — the Appearance's own Mode, Windows'
    /// where it follows Windows (<see cref="ModeOf"/>). Never the mode the bar's ground reads in: a white ground in dark mode
    /// read as light, so the page edited the light mode's values and the dark mode's white could not be changed from it.
    /// </summary>
    private bool ReadsDark(Config of) => ModeOf(of.Appearance, look);

    /// <summary>§694: the mode <paramref name="appearance"/> is in on <paramref name="windows"/> (Windows' look as read): its Mode, else Windows'.</summary>
    internal static bool ModeOf(Appearance? appearance, Look windows) => Looks.Dark(appearance, windows.Windows ?? windows.Dark);

    /// <summary>
    /// §528: the widgets <paramref name="keys"/> as <paramref name="of"/> has them, each with what its pickers read and write
    /// (<see cref="PickerPlace"/>) and, where <paramref name="sources"/>, what its rows choose from (<see cref="MenuSources"/>):
    /// what a page's settings are of, the one widget or the several selected (OptionMenu.Row, Apply, Settings).
    /// </summary>
    private List<OptionTarget> TargetsOf(Config of, IReadOnlyList<string> keys, bool sources = true) =>
        [.. keys.Select(key =>
        {
            var item = of.FindIsland(Islands.OwnerOf(key), Islands.IdOf(key));
            return new OptionTarget(item, sources ? MenuSources(item) : null, PickerPlace(of, key));
        })];

    /// <summary>
    /// menus: the row <paramref name="pick"/> of the menu <paramref name="what"/> was shown for, as a config has it: a bar's
    /// looks' (§290), ⚙'s (§520: the settings panel's Karpo page), a widget's own with its sources (an account's part's
    /// colour, §338, is a row of its accounts' level); null for a menu of none of them.
    /// </summary>
    private Func<Config, OptionRow?>? RowOfPick(Pending what, OptionPick pick)
    {
        if (what.Looks is { } bar)
        {
            var scope = what.LooksScope;
            return of => LooksRow(of, bar, pick, scope);
        }
        if (what.SettingsMenu is { } settings)
        {
            return of => SettingsRow(of, settings.Page, pick);
        }
        // §528: the several widgets selected's row in common.
        if (what.Keys is { Count: > 0 } keys)
        {
            return of => OptionMenu.Row(TargetsOf(of, keys), pick);
        }
        return null;
    }

    /// <summary>
    /// menus: a pin's "Browse…" asked for from its widget's menu <paramref name="what"/> (<see cref="OptionPickKind.Browse"/>):
    /// Windows' open dialog shown once the menu has gone, its choice set and saved, and the menu shown again where it stood —
    /// a bar's widget's (a folder's line's folder may have gone); §520 a popup of the settings panel's needs nothing shown
    /// again: the panel stands.
    /// </summary>
    private void MenuBrowse(Pending what, OptionPick pick)
    {
        if (what.Owner is not { } window || RowOfPick(what, pick) is not { } rowIn)
        {
            return;
        }
        var anchor = Cursor();
        // §374: where the menu stood, closed by the pick or still open under the dialog to come, to show it there again.
        var stood = what.Stood ?? (flyout is { IsOpen: true } open && ReferenceEquals(pending, what) ? open.Remembered() : null);
        // §532: a taskbar menu shortcut's (§553 the Taskbar page's) is no widget's: the panel stands, nothing to show again.
        var key = what.Target ?? "";
        var (owner, id) = (Islands.OwnerOf(key), Islands.IdOf(key));
        Action back = what.Drops || key.Length == 0 ? static () => { } : () =>
        {
            if (what.Folder is null && config.FindIsland(owner, id) is not null)
            {
                WidgetMenu(window, key, anchor, what.Hit, beside: what.Beside);
            }
        };
        Later(() =>
        {
            if (rowIn(config) is { Kind: OptionRowKind.Target } row
                && TargetDialog.Pick(window.Hwnd, row.Text.Length > 0 ? row.Text : null, browses: row.Browses) is { Length: > 0 } target)
            {
                var next = config.Clone();
                if (rowIn(next)?.SetText?.Invoke(target) == true)
                {
                    Apply(next);
                }
            }
            Resume(stood, back);
        });
    }

    /// <summary>
    /// §523: a colour being mixed in the open menu's picker: the menu it is mixed in, the pick of the row and the colour it
    /// says now; §622 the person's config under it is the one under every copy worn (<see cref="Persons"/>). Null while none is.
    /// </summary>
    private (Pending What, object Where, OptionPick Pick, Argb Now)? mixing;

    /// <summary>
    /// §523: the picker of the menu <paramref name="what"/> said <paramref name="happened"/> (FlyoutEventKind.Mix, MixKept,
    /// MixUndone) of its row's colour: worn a moment (§622 <see cref="Wear"/>), kept, or put back (see the top of the file).
    /// <paramref name="where"/> the place it is mixed in, the menu itself where none: §670 an inline picker's page is built
    /// anew as each colour is worn, its menu a new one each time, so its mix is told by its page, not by its menu — else each
    /// move kept the one before and the recent colours filled with the drag.
    /// </summary>
    private void MenuMixed(Pending what, FlyoutEvent happened, object? where = null)
    {
        where ??= what;
        if (OptionMenu.PickOf(what.Options, happened.Id) is not { Kind: OptionPickKind.Mix } pick || RowOfPick(what, pick) is not { } rowIn)
        {
            return;
        }
        // Another row's or another menu's mix still under way is kept first.
        if (mixing is { } other && (!SameMix(other.Where, where) || other.Pick.Key != pick.Key))
        {
            MixDone(keep: true);
        }
        switch (happened.Kind)
        {
            case FlyoutEventKind.Mix:
            {
                // §622: a choice tried on under the pointer gives way to the colour mixed, worn over the person's config.
                tried = null;
                mixing = (what, where, pick, happened.Color);
                var next = Persons.Clone();
                if (rowIn(next) is { SetColor: { } look } && look(happened.Color, false))
                {
                    Wear(next);
                }
                break;
            }
            case FlyoutEventKind.MixKept:
                mixing = (what, where, pick, happened.Color);
                MixDone(keep: true);
                break;
            case FlyoutEventKind.MixUndone:
                MixDone(keep: false);
                break;
        }
    }

    /// <summary>§670: whether two mixes stand in one place: one menu (the same one), or one page of a settings panel.</summary>
    internal static bool SameMix(object a, object b) => a is Pending ? ReferenceEquals(a, b) : Equals(a, b);

    /// <summary>
    /// §523: the colour being mixed done with: <paramref name="keep"/>, set on the person's config and saved as one change (the
    /// first recent colour); else that config back, nothing saved. The menu shown again with it.
    /// </summary>
    private void MixDone(bool keep)
    {
        if (mixing is not { } done)
        {
            return;
        }
        mixing = null;
        // §622: the looks worn were never the config's: it is the person's again, so the change is one step from it.
        var looked = Settle();
        var next = config.Clone();
        if (keep && RowOfPick(done.What, done.Pick)?.Invoke(next) is { SetColor: { } set } && set(done.Now, true))
        {
            Apply(next);
        }
        else if (looked)
        {
            // What the bars wore a moment, put back: the config as it was, nothing saved.
            ShowSettled();
        }
        else
        {
            MenuAgain(done.What);
        }
        // §520: a popup of the settings panel's stays open for more; the panel shows the colour as it is now.
        StirChrome();
        PresentChrome();
    }
}
