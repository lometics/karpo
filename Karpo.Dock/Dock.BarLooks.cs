// edit: a bar's looks (the person, 2026-10-22: "when i right click in edit mode onto the taskbar all the appearance
// settings should appear in the context menu"; docs\widgets.md §290): every one the settings app had for it, as
// Karpo.Core\BarLooks.cs lists them, cut three ways (BarLooks.LooksScope, §517) into the settings panel's Displays and
// layouts (§585), Taskbar and §567 Appearance pages (§520, Dock.SettingsPanel.cs; the right click on the room round an
// arranged bar's widgets opens the panel on its Taskbar page). A change is saved and shown at once, as a widget's setting is (OptionPicked, LooksPicked).
// Where the change takes the bar away from under it — another edge, another type — edit mode stays, the bar made again
// arranged (Dock.EditHolder, §296).
//
// §334: a right click on the room of an arranged folder, on none of its lines, opens the folders' look
// the same way (FolderLooksMenu, LooksScope.Folders: BarLooks.Appearance for Surface.Folders), over the
// folder, which shows each change as it is made (FolderFlyout.Relook).

using Karpo.Core;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>
    /// edit: <paramref name="window"/>'s bar as its looks' menu names it: its display, and the displays
    /// with a bar now, which whether its display may show None asks (§309).
    /// </summary>
    private BarSpot SpotOf(DockWindow window) => SpotOf(window.Id, window.Name);

    /// <summary>
    /// edit: the bar of the display <paramref name="id"/> (called <paramref name="name"/>), shown on it or not: the displays with a
    /// bar now (§309) and §579 every display connected, in Windows' order, which the Display page lists.
    /// </summary>
    private BarSpot SpotOf(string id, string name) => new(id, name)
    {
        Attached = [.. windows.Select(each => each.Id).Distinct(StringComparer.OrdinalIgnoreCase)],
        Connected = [.. EditedDisplays.Select(display => (display.Id, display.Name))],
    };

    /// <summary>
    /// edit: the menu of <paramref name="what"/>'s bar's looks, as much of them as it asks for
    /// (<see cref="Pending.LooksScope"/>, §330), at <paramref name="anchor"/>: a configuration menu,
    /// named by what it is of (<see cref="BarLooks.Kind(Config, BarSpot, LooksScope)"/>).
    /// </summary>
    private void LooksMenu(Pending what, POINT anchor)
    {
        if (what.Looks is not { } bar)
        {
            return;
        }
        List<OptionPick> picks = [];
        ShowFlyout(
            new Pending(Topic.LayoutEditor, what.Owner)
            {
                Looks = bar,
                LooksScope = what.LooksScope,
                Folder = what.Folder,
                Options = picks,
                // §517: no widget's menu: as tall and as wide as its rows, the edit bar's and a right click's alike.
                FitsContent = true,
                TypeTitle = BarLooks.Kind(config, bar, what.LooksScope),
                Edge = what.Edge,
                Work = what.Work,
                Scale = what.Scale,
                // §356: the looks as the config has them now: another layout shown, the bar the same.
                Again = again => (LooksRows(bar, again, what.LooksScope), BarLooks.Kind(config, bar, what.LooksScope)),
            },
            LooksRows(bar, picks, what.LooksScope),
            FlyoutLayout.List,
            anchor);
    }

    /// <summary>
    /// edit, §334: the folders' look at <paramref name="anchor"/>, over <paramref name="open"/>, the folder
    /// standing open arranged whose room was right-clicked; the folder holds itself open for it and is
    /// let go when it ends (<see cref="FolderMenuEnded"/>, through <see cref="Pending.Folder"/>).
    /// </summary>
    private void FolderLooksMenu(OpenFolder open, POINT anchor)
    {
        if (!disposed && windows.Contains(open.Window) && Arranging(open.Window))
        {
            LooksMenu(new Pending(Topic.LayoutEditor, open.Window) { Looks = SpotOf(open.Window), LooksScope = LooksScope.Folders, Folder = open }, anchor);
        }
        // No menu came of it: the folder goes back to what it was.
        if (!FlyoutOpen || !ReferenceEquals(pending?.Folder, open))
        {
            open.Flyout.MenuClosed();
        }
    }

    /// <summary>edit: <paramref name="bar"/>'s looks of <paramref name="scope"/> as the config has them now (<see cref="LooksItems"/>).</summary>
    private List<FlyoutItem> LooksRows(BarSpot bar, List<OptionPick> picks, LooksScope scope) => LooksItems(config, bar, ReadsDark(config), picks, scope, license.Status, BarMenuServices(), LayoutMachineOf(scope));

    /// <summary>
    /// §628: this PC as the Add a layout page makes its presets for it (LayoutMachine: its pins, its pen and touch, Windows'
    /// taskbar), read once as the page opens and kept while it stands, so its tiles picture what a pick makes; none for any
    /// other scope.
    /// </summary>
    private LayoutMachine? presetMachine;

    private LayoutMachine? LayoutMachineOf(LooksScope scope) => scope == LooksScope.AddLayout ? presetMachine ??= LayoutMachine.Read() : null;

    /// <summary>
    /// edit: the rows of <paramref name="bar"/>'s looks' menu as <paramref name="of"/> has them, in the
    /// mode its taskbar reads in (<paramref name="dark"/>, §547: never Windows'), with no rule over them — the bar's type is the menu's
    /// top row, as a widget's is its buttons' row's start (<see cref="BarLooks.Kind"/>, §323) —; their commands into
    /// <paramref name="picks"/>. A new layout is refused as <paramref name="license"/> refuses it (§309).
    /// The Display, Layouts, Taskbar or Appearance page's, or the folder room's alone (<paramref name="scope"/>, §330, §517).
    /// </summary>
    internal static List<FlyoutItem> LooksItems(Config of, BarSpot bar, bool dark, List<OptionPick> picks, LooksScope scope, LicenseStatus? license = null, KarpoServices? services = null, LayoutMachine? machine = null) =>
        [.. OptionMenu.Section(LooksOf(of, bar, dark, scope, license, services, machine), picks).SkipWhile(item => item.Separator)];

    /// <summary>
    /// edit: <see cref="BarLooks.Rows"/> of <paramref name="scope"/>; §553 the Taskbar page's with the taskbar's right-click menu
    /// after the bar's own, §585 one expander, Behavior's last row (KarpoMenu.BarMenu: Karpo's one menu, every layout's), a
    /// shortcut's targets offered as <paramref name="services"/> offer them (none for none); §628 Add a layout's presets made for
    /// <paramref name="machine"/> (this PC read for none).
    /// </summary>
    internal static IReadOnlyList<OptionRow> LooksOf(Config of, BarSpot bar, bool dark, LooksScope scope, LicenseStatus? license = null, KarpoServices? services = null, LayoutMachine? machine = null) =>
        scope == LooksScope.Taskbar
            ? [.. BarLooks.Rows(of, bar, dark, scope, license), KarpoMenu.BarMenu(of, services ?? new KarpoServices(null, static () => { }), of.ActiveLayout(bar.Display))]
            : BarLooks.Rows(of, bar, dark, scope, license, machine);

    /// <summary>§553: what the Taskbar page's taskbar menu asks of the dock: what a shortcut may open, as a pin's Target offers it.</summary>
    private KarpoServices BarMenuServices() => new(null, QuitFromMenu) { Targets = () => TargetLooks.Choices(config) };

    /// <summary>edit: the look of <paramref name="bar"/> <paramref name="pick"/> was shown for, read from <paramref name="of"/> as its menu's <paramref name="scope"/> has it; null for none.</summary>
    private OptionRow? LooksRow(Config of, BarSpot bar, OptionPick pick, LooksScope scope) =>
        OptionRows.Find(LooksOf(of, bar, ReadsDark(of), scope, license.Status, BarMenuServices(), LayoutMachineOf(scope)), pick.Key);

    /// <summary>
    /// edit: a row of a bar's looks acted: set as the settings app sets it (BarLooks) and saved as every
    /// change of the dock's is (§523: a colour of one's own mixed in its picker, MenuMixed); a layout deleted
    /// once Windows' own dialog asked (§309). Where the menu stays open (<paramref name="list"/>) it
    /// shows the looks as they are now: another layout shown, the bar the same window, it is that
    /// layout's, the bar arranged as it is (edit mode is the dock's, §296).
    /// </summary>
    private void LooksPicked(Pending what, BarSpot bar, OptionPick pick, Flyout? list)
    {
        var next = config.Clone();
        var row = LooksRow(next, bar, pick, what.LooksScope);
        if (!GoesAhead(what, row, pick))
        {
            return;
        }
        if (row is not { Enabled: false } && OptionMenu.ApplyRow(row, pick))
        {
            Apply(next);
        }
        if (list is { IsOpen: true })
        {
            MenuAgain(what);
        }
    }

    /// <summary>
    /// edit: a line of text of a bar's looks' menu kept <paramref name="kept"/> (§309): a layout named
    /// (§333: any of them, in its own level), a size typed or picked among those offered; saved as every
    /// change of the dock's is, the row read from the menu's own <paramref name="scope"/>. False where
    /// nothing changed.
    /// </summary>
    private bool LooksKept(BarSpot bar, LooksScope scope, OptionPick pick, FieldCommit kept)
    {
        var next = config.Clone();
        if (!FieldPicks.KeepRow(LooksRow(next, bar, pick, scope), kept))
        {
            return false;
        }
        Apply(next);
        return true;
    }
}
