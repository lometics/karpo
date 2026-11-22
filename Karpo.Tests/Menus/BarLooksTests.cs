// edit: a bar's looks and the separators' in their menus in edit mode (the person, 2026-10-22: "when i
// right click in edit mode onto the taskbar all the appearance settings should appear in the context
// menu. the same should be for the divider context menu"; Karpo.Core\BarLooks.cs, Karpo.Dock\
// Dock.BarLooks.cs, docs\widgets.md §290). Held here:
//
//  - **Nothing missed.** Every setting of config.json is written by a row of some menu: ConfigCoverageTests
//    (§341; it replaced the test that read the settings app's pages).
//  - **Each sets what it says**, through the rows the menu shows, and only that bar's: another edge,
//    type, the top bar's own size and way of showing, the material and its opacity, a colour; a
//    separator's width (Automatic at 0), its room; a divider's the same rows, the folder separators'
//    own (§307).
//  - **Pictures** (KARPO_UI_TESTS=1, KARPO_PNG_DIR), 150 %, dark: the bar's menu, its colours' level
//    and its size's (bar-looks-*), a separator's and a divider's menus (separator-looks-dark).
//
// §309 (the person, 2026-10-23: "in the taskbar context menu there should also be the option to select
// the taskbar shown and add / remove additional taskbars. it should basically be on par with the
// configuration from the configuration app"): parity now with nothing left out but what is no setting
// of a bar (a card's being open, Identify, Refresh, Windows' own colours); the Layouts level chooses,
// makes, copies, names and deletes, each as the settings app does; another layout shown in edit mode
// is the one arranged, edit mode kept; the bar's menu and its Layouts level (bar-layouts-dark).
//
// §333 (the person, 2026-10-23: "taskbar size should be a number input and not a select input";
// "improve the design of the layout settings on the taskbar context menu. they should be in behaviour
// be similar to the faces one"): the size a stepper in pixels, each bar's own, held to what a bar may
// be; the Layouts level a list of the layouts as a folder's faces are — picked by a click, dragged into
// order (kept in the file), each one's own level naming, drawing and copying that one, its bin asking
// first and greyed on the last, "Add layout" under them in one click, None and new displays' after.
//
// §336 (the person, 2026-10-23: "click should open its settings like faces"): a layout's row opens its
// own level by a click, Enter or Right, as a face's row does, the one shown ticked; it shows nothing.
// A layout's level has "Show on this display" first.
//
// §348: the edit bar's Display ▾ has the display's own — Position, the layout shown (ticked, None last), New
// displays show. §352: its Layout ▾ the rest, the layout's whole in sections (Taskbar, Folders, Behavior);
// the two in order the right click's whole. §361: the layouts are managed in Display ▾, as its own rows.
//
// §585: one page, Layouts: every display in Windows' one order, each an expander, the flyout's own marked; then
// the layouts. §595: the default layout badged "Default", each layout's expander a switch making it the default; §636 a
// button, Set as default, greyed on the default: always exactly one.

using System.Reflection;
using System.Text.RegularExpressions;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed partial class BarLooksTests
{
    private const string Display = "\\\\.\\DISPLAY1";

    private static Config Fresh()
    {
        var config = new Config();
        config.EditScreen(Display, "One");
        return config;
    }

    private static BarSpot Spot() => new(Display, "One");

    /// <summary>
    /// A pick of <paramref name="key"/>'s choice at <paramref name="at"/>, a step of it by
    /// <paramref name="by"/>, or its action done (<paramref name="act"/>), as the menu of
    /// <paramref name="scope"/> acts it (the dock finds the row in that menu's, Dock.LooksRow).
    /// </summary>
    private static bool Act(Config config, BarSpot bar, string key, int at = 0, int? by = null, bool act = false, LooksScope? scope = null)
    {
        // §411: a display's row is the Layouts page's alone (§585), the rest the Taskbar page's.
        var looks = scope ?? (OptionRows.Find(BarLooks.Rows(config, bar, dark: true, scope: LooksScope.Layouts), key) is not null ? LooksScope.Layouts : LooksScope.Taskbar);
        List<OptionPick> picks = [];
        _ = TheDock.LooksItems(config, bar, dark: true, picks, scope: looks);
        var pick = act ? picks.Single(each => each.Key == key && each.Kind == OptionPickKind.Act)
            : by is { } step ? picks.First(each => each.Key == key && each.Kind == OptionPickKind.Step && each.Value == Math.Sign(step))
            : picks.Single(each => each.Key == key && each.Kind == OptionPickKind.Set && each.Value == at);
        return OptionMenu.ApplyRow(OptionRows.Find(BarLooks.Rows(config, bar, dark: true, scope: looks), key), pick);
    }

    /// <summary>A flip of the switch <paramref name="key"/>, as the menu of <paramref name="scope"/> acts it.</summary>
    private static bool Flip(Config config, BarSpot bar, string key, LooksScope scope = LooksScope.Layouts)
    {
        List<OptionPick> picks = [];
        _ = TheDock.LooksItems(config, bar, dark: true, picks, scope: scope);
        return OptionMenu.ApplyRow(OptionRows.Find(BarLooks.Rows(config, bar, dark: true, scope: scope), key), picks.Single(each => each.Key == key && each.Kind == OptionPickKind.Flip));
    }

    /// <summary>§595: the row in <paramref name="layout"/>'s expander making it the default layout (§636 its Set as default).</summary>
    private static string DefaultOf(TaskbarLayout layout) => BarLooks.KeyOf(BarLooks.DefaultRow, layout);

    /// <summary>§636: <paramref name="layout"/>'s Set as default pressed, as the Layouts page acts it.</summary>
    private static bool MakeDefault(Config config, BarSpot bar, TaskbarLayout layout) => Act(config, bar, DefaultOf(layout), act: true, scope: LooksScope.Layouts);

    /// <summary>§521: <paramref name="layout"/> shown on <paramref name="bar"/>'s display by its Layout (§585 in its expander).</summary>
    private static bool ShowHere(Config config, BarSpot bar, TaskbarLayout layout) => Act(config, bar, BarLooks.KeyOf(BarLooks.ShownRow, bar.Display), at: config.Layouts.IndexOf(layout), scope: LooksScope.Layouts);

    /// <summary>§579: the Layout of the display <paramref name="display"/>, keyed with it.</summary>
    private static string ShownOf(string display) => BarLooks.KeyOf(BarLooks.ShownRow, display);

    /// <summary>§579: the Position of the display <paramref name="display"/>, keyed with it.</summary>
    private static string PositionOf(string display) => BarLooks.KeyOf(BarLooks.PositionRow, display);

    /// <summary>§585: the page's row of <paramref name="key"/> (the displays', the layouts').</summary>
    private static OptionRow PageRow(Config config, BarSpot bar, string key) => BarLooks.LayoutsRows(config, bar).Single(row => row.Key == key);

    /// <summary>§585: the layouts as things of the page.</summary>
    private static IReadOnlyList<OptionThing> LayoutThings(Config config, BarSpot bar) => PageRow(config, bar, BarLooks.LayoutsRow).Owned!.Things;

    /// <summary>§585: the displays as things of the page, each an expander.</summary>
    private static IReadOnlyList<OptionThing> DisplayThings(Config config, BarSpot bar) => PageRow(config, bar, BarLooks.DisplaysRow).Owned!.Things;

    /// <summary>The row <paramref name="key"/> of <paramref name="bar"/>'s menu as <paramref name="config"/> has it now, a level's among them.</summary>
    private static OptionRow Row(Config config, BarSpot bar, string key) =>
        Assert.Single(OptionRows.Flat([.. BarLooks.Rows(config, bar, dark: true, scope: LooksScope.Layouts), .. BarLooks.Rows(config, bar, dark: true, scope: LooksScope.Taskbar)]), row => row.Key == key);

    /// <summary>§333: what a typed size keeps (the stepper's row set to it, as typed entry sets it).</summary>
    private static bool Size(Config config, BarSpot bar, int size) => Row(config, bar, BarLooks.SizeRow).Set!(size);

    /// <summary>Words typed into <paramref name="key"/>'s field and kept, as the dock keeps them (Dock.LooksKept).</summary>
    private static bool Type(Config config, BarSpot bar, string key, string text) =>
        Row(config, bar, key) is { Kind: OptionRowKind.Text, Enabled: true, SetText: { } set } && set(text);

    private const string Other = "\\\\.\\DISPLAY2";
    private const string Eight = "\\\\.\\DISPLAY8";
    private const string Nine = "\\\\.\\DISPLAY9";

    /// <summary>Three layouts — the first shown on <see cref="Display"/>, Work, and Gaming, a Dock — and the bar there, another display attached that shows Karpo.</summary>
    private static (Config Config, BarSpot Bar) Three()
    {
        var config = Fresh();
        config.EditScreen(Other, "Two");
        BarLooks.Add(config, "Work").Icon = OptionGlyphs.Pc;
        _ = BarLooks.Add(config, "Gaming").Become(LayoutType.Dock);
        return (config, Spot() with { Attached = [Display, Other], Connected = [(Display, "One"), (Other, "Two")] });
    }

    /// <summary>
    /// §521 (the person, 2026-11-07: "layouts should get their own separate entry in the settings."): the Layouts page holds
    /// every layout in the person's order as the person's own things, each its own rows — its name, its icon —, §551 its
    /// Duplicate, its Delete and its move, the one this display shows saying so under its name, "Add a layout" under them; which one this display shows is its
    /// display's expander's (§585) dropdown, the layouts and None (where Karpo may be hidden here). Each layout named, drawn, copied and deleted for itself,
    /// whichever is shown; added from a preset (§472) and shown here.
    /// </summary>
    [Fact]
    public void TheLayoutsPageHoldsEachLayoutAsAnExpanderOfItsOwnRows()
    {
        var (config, bar) = Three();
        var home = config.Layouts[0];
        var work = config.Layouts[1];
        var gaming = config.Layouts[2];

        // §585: under Displays the displays; under Layouts the layouts (§595: no Default layout row over them).
        var rows = BarLooks.Rows(config, bar, dark: true, LooksScope.Layouts);
        Assert.Equal([BarLooks.DisplaysHeading, BarLooks.DisplaysRow, BarLooks.LayoutsHeading, BarLooks.LayoutsRow], rows.Select(each => each.Key));
        var row = rows[^1];
        Assert.Equal((BarLooks.LayoutsRow, OptionRowKind.Things, true), (row.Key, row.Kind, row.Inline));
        IReadOnlyList<OptionThing> Things() => LayoutThings(config, bar);
        Assert.Equal([home.Name, "Work", "Gaming"], Things().Select(thing => thing.Title));
        Assert.Equal([S.SettingsSectionsThisDisplay, null, null], Things().Select(thing => thing.Summary));
        Assert.Equal([S.SettingsLayoutsDefaultBadge, null, null], Things().Select(thing => thing.Badge));
        Assert.Equal(OptionGlyphs.Pc, Things()[1].Glyph);
        Assert.Equal(
            [BarLooks.KeyOf(BarLooks.NameRow, work), BarLooks.KeyOf(BarLooks.IconRow, work), DefaultOf(work)],
            Things()[1].Rows.Select(each => each.Key));
        // §551: its Duplicate an action of its own, a button at its row's end.
        Assert.Equal([BarLooks.KeyOf(BarLooks.DuplicateRow, work)], Things()[1].Acts.Select(each => each.Key));
        Assert.Equal((BarLooks.KeyOf(BarLooks.DeleteRow, work), BarLooks.KeyOf(BarLooks.MoveRow, work)), (Things()[1].Delete?.Key, Things()[1].Move?.Key));
        // §628: a way to add one leading to its page of the layout presets (§472 to §627 a choice of them).
        var add = row.Owned!.Add!;
        Assert.Equal((OptionRowKind.Link, true, OptionLink.AddLayout), (add.Kind, add.Adds, add.Link));
        Assert.Empty(add.Choices);

        // A display's Layout: the layouts and None, the one shown chosen; picked, shown here.
        var shown = Row(config, bar, ShownOf(Display));
        Assert.Equal([home.Name, "Work", "Gaming", S.SettingsDisplaysNone], shown.Choices);
        Assert.Equal((0, OptionGlyphs.Hide), (shown.Chosen, shown.ChoiceGlyphs[^1]));
        Assert.True(Act(config, bar, ShownOf(Display), at: 1, scope: LooksScope.Layouts));
        Assert.Equal("Work", config.ActiveLayout(Display).Name);
        Assert.False(Act(config, bar, ShownOf(Display), at: 1, scope: LooksScope.Layouts));
        Assert.Equal([null, S.SettingsSectionsThisDisplay, null], Things().Select(thing => thing.Summary));
        Assert.Equal([S.SettingsLayoutsDefaultBadge, null, null], Things().Select(thing => thing.Badge));
        // None hides Karpo here while another display shows it: no layout says it is shown here.
        Assert.True(Act(config, bar, ShownOf(Display), at: 3, scope: LooksScope.Layouts));
        Assert.False(config.ScreenFor(Display).Enabled);
        Assert.Equal((3, OptionGlyphs.Hide), (Row(config, bar, ShownOf(Display)).Chosen, Row(config, bar, ShownOf(Display)).Glyph));
        Assert.Equal([null, null, null], Things().Select(thing => thing.Summary));
        Assert.True(Act(config, bar, ShownOf(Display), at: 0, scope: LooksScope.Layouts));
        Assert.True(config.ScreenFor(Display).Enabled);
        // The last display showing Karpo has no None.
        Assert.Equal(3, BarLooks.Shown(config, Spot()).Choices.Count);

        // Named, drawn and copied in its own rows: that layout, not the one shown (Home).
        Assert.False(Type(config, bar, BarLooks.KeyOf(BarLooks.NameRow, gaming), "   "));
        Assert.True(Type(config, bar, BarLooks.KeyOf(BarLooks.NameRow, gaming), new string('x', 80)));
        Assert.Equal(BarLooks.NameLongest, gaming.Name.Length);
        Assert.True(Type(config, bar, BarLooks.KeyOf(BarLooks.NameRow, gaming), "Play"));
        Assert.Equal(("Play", home.Name), (gaming.Name, config.ActiveLayout(Display).Name));
        var icons = Row(config, bar, BarLooks.KeyOf(BarLooks.IconRow, gaming));
        Assert.Equal(0, icons.Chosen);
        Assert.True(Act(config, bar, icons.Key, at: 5, scope: LooksScope.Layouts));
        Assert.Equal((icons.Tiles[5].Glyph, TaskbarLayouts.LayoutGlyph), (gaming.Icon, home.Icon));
        Assert.True(Act(config, bar, icons.Key, at: 0, scope: LooksScope.Layouts));
        Assert.Equal(TaskbarLayouts.LayoutGlyph, gaming.Icon);
        // A copy in one click, at the end, its widgets ids of their own, shown here.
        Assert.True(Act(config, bar, BarLooks.KeyOf(BarLooks.DuplicateRow, gaming), act: true, scope: LooksScope.Layouts));
        var copy = config.Layouts[^1];
        Assert.Equal((S.SettingsLayoutsCopyName("Play"), LayoutType.Dock, gaming.Islands.Count, copy.Id), (copy.Name, copy.Type, copy.Islands.Count, config.ActiveLayout(Display).Id));
        Assert.Empty(copy.Islands.Select(island => island.Id).Intersect(gaming.Islands.Select(island => island.Id)));

        // Added from a preset (§472), §628 its tile on Add a layout's page: Karpo's as "Karpo", then "Karpo 2", shown here.
        var karpo = BarLooks.KeyOf(BarLooks.PresetRow, LayoutPresets.KarpoId);
        Assert.True(Act(config, bar, karpo, act: true, scope: LooksScope.AddLayout));
        var added = config.ActiveLayout(Display);
        Assert.Equal((LayoutPresets.Karpo.Title, LayoutType.Taskbar, Config.FirstLayout().Islands.Count, 5), (added.Name, added.Type, added.Islands.Count, config.Layouts.Count));
        Assert.True(Act(config, bar, karpo, act: true, scope: LooksScope.AddLayout));
        Assert.Equal(S.SettingsLayoutsNumbered(LayoutPresets.Karpo.Title, 2), config.ActiveLayout(Display).Name);
        var newest = config.ActiveLayout(Display);

        // Deleted, asked first: the display showing it shows the Default layout.
        Assert.True(MakeDefault(config, bar, work));
        var delete = Row(config, bar, BarLooks.KeyOf(BarLooks.DeleteRow, newest));
        Assert.Equal((true, S.SettingsLayoutsDeleteConfirm), (delete.Enabled, delete.Ask?.Content));
        Assert.True(Act(config, bar, delete.Key, act: true, scope: LooksScope.Layouts));
        Assert.Null(config.Layout(newest.Id));
        Assert.Equal("Work", config.ActiveLayout(Display).Name);
        // The last one stays: its Delete greyed, saying why.
        while (config.Layouts.Count > 1)
        {
            Assert.True(config.DeleteLayout(config.Layouts[^1].Id));
        }
        var last = Row(config, bar, BarLooks.KeyOf(BarLooks.DeleteRow, config.Layouts[0]));
        Assert.Equal((false, S.SettingsLayoutsLastOne), (last.Enabled, last.Why));
        // Moved: the file's order.
        Assert.False(BarLooks.MoveLayout(config, config.Layouts[0].Id, 9));
        Assert.False(BarLooks.MoveLayout(config, "nothing", 0));
    }

    /// <summary>
    /// §517, §521, §585: the bar's rows cut three ways for the panel's pages, nothing lost and nothing twice. The Displays and
    /// layouts page: under Displays each display an expander of the layout it shows (a dropdown, None among them) and where its
    /// bar stands (§348); under Layouts the layouts, each its own rows (§595 whether it is the default among them), and "Add a
    /// layout". The Taskbar page
    /// the shown layout's bar: under "Bar" its type, its size and showing; under "Behavior" combine, previews, desktops, flyouts
    /// and folders on hover. The Appearance page its look.
    /// </summary>
    [Fact]
    public void TheBarsRowsAreCutThreeWays()
    {
        var (config, bar) = Three();
        var home = config.Layouts[0];
        string[] KeysOf(LooksScope scope) => [.. OptionRows.Flat(BarLooks.Rows(config, bar, dark: true, scope: scope)).Where(row => row.Kind is not (OptionRowKind.Heading or OptionRowKind.Things)).Select(row => row.Key)];
        var layouts = KeysOf(LooksScope.Layouts);
        var taskbar = KeysOf(LooksScope.Taskbar);
        var look = KeysOf(LooksScope.Appearance);

        // Nothing twice.
        string[][] all = [layouts, taskbar, look];
        Assert.Equal(all.Sum(keys => keys.Length), all.SelectMany(keys => keys).Distinct().Count());
        // §585: every display connected in Windows' order, each its Layout and Position; then the layouts, §595 each its switch.
        Assert.Equal([ShownOf(Display), PositionOf(Display), ShownOf(Other), PositionOf(Other), BarLooks.AddRow], layouts.Take(5));
        Assert.DoesNotContain(BarLooks.DefaultRow, layouts);
        Assert.All(config.Layouts, each => Assert.Contains(BarLooks.KeyOf(BarLooks.NameRow, each), layouts));
        Assert.All(config.Layouts, each => Assert.Contains(DefaultOf(each), layouts));
        // §534: "Lock the taskbar" with the bar's own rows, every bar's.
        Assert.Equal([BarLooks.TypeRow, BarLooks.SizeRow, BarLooks.SpacingRow, BarLooks.ShowRow, BarMoves.LockRowKey, BarLooks.CombineRow], taskbar.Take(6));
        Assert.Contains(BarLooks.HoverRow, taskbar);
        // §682: Animations the Appearance page's too, its own key.
        Assert.All(look.Where(key => key != KarpoMenu.AnimationsRow), key => Assert.StartsWith(LookRows.Key, key, StringComparison.Ordinal));
        // §423: the panel's pages have no title over their rows from the menus, their titles name them.
        Assert.Equal(
            ((string?)null, (string?)null, (string?)null, BarLooks.TypeName(LayoutType.Taskbar)),
            (BarLooks.Kind(config, bar, LooksScope.Layouts), BarLooks.Kind(config, bar, LooksScope.Taskbar), BarLooks.Kind(config, bar, LooksScope.Appearance), BarLooks.Kind(config, bar)));

        // §585: the page as its rows: Displays, each display a row of its name ("This display" under this one's) opening its
        // Layout, a dropdown, and its Position; then Layouts, the layouts.
        List<OptionPick> picks = [];
        var items = TheDock.LooksItems(config, bar, dark: true, picks, scope: LooksScope.Layouts);
        Assert.Equal(
            ["h:" + S.SettingsSectionsDisplays, "One", "Two", "h:" + S.CoreBarMenuLayouts, home.Name],
            items.Take(5).Select(item => item.Separator ? "" : item.Header ? "h:" + item.Text : item.Text));
        Assert.Equal([S.SettingsSectionsThisDisplay, null], items.Skip(1).Take(2).Select(item => item.Detail));
        var one = items[1].Children!;
        Assert.Equal([S.DockEditLayout, S.SettingsDisplaysEdgeHeader], one.Select(item => item.Text));
        Assert.True(one[0].PickOne);
        Assert.Equal([home.Name, "Work", "Gaming", S.SettingsDisplaysNone], one[0].Children!.Select(item => item.Text));
        Assert.IsType<FlyoutControl.Segments>(one[1].Control);
        // Nothing to delete, rename, drag or copy: the displays are Windows'.
        Assert.All(items.Skip(1).Take(2), item => Assert.Null(item.Control));
        Assert.True(MakeDefault(config, bar, config.Layouts[2]));
        Assert.Equal("Gaming", config.DefaultLayout.Name);
        Assert.True(Act(config, bar, PositionOf(Display), at: Array.IndexOf(BarLooks.Edges, Edge.Top), scope: LooksScope.Layouts));
        Assert.Equal(Edge.Top, config.EdgeOfScreen(Display));

        // The Taskbar page: the shown layout's bar, nothing of the displays' nor the layouts' (§553: the taskbar menu's own
        // "Layouts" entry its expander's, §585).
        items = TheDock.LooksItems(config, bar, dark: true, picks = [], scope: LooksScope.Taskbar);
        Assert.DoesNotContain(items.TakeWhile(item => item.Text != S.SettingsBarMenuHeader), item => item.Text is var text && (text == S.SettingsDisplaysEdgeHeader || text == S.CoreBarMenuLayouts || text == S.SettingsLayoutsAdd || text == S.SettingsDisplaysNone));
        // §514: one row opens folders and widgets' flyouts on hover, the shown layout's.
        var shown = config.ActiveLayout(Display);
        Assert.True(Act(config, bar, BarLooks.HoverRow, at: 1, scope: LooksScope.Taskbar));
        Assert.True(shown.OpenOnHover);
    }

    /// <summary>
    /// §595 (the person, 2026-11-09: "default layout should not be a dropdown but should be a badge in the layout list and then
    /// when expanding a layout you should be able to make it the default one"), §636 ("one of the layouts always has to be the
    /// default one"): no Default layout row; the default layout wears the badge "Default" in the list, whatever this display
    /// shows, and there is always exactly one. Each layout's expander has "Shown on new displays" before a button, Set as
    /// default, greyed on the default — nothing turns it off. Pressed on another, the default moves there, the badge with it;
    /// pressed on the default (were it reached), nothing. Deleting the default makes the first layout left it. A display seen for
    /// the first time shows the default, Karpo on.
    /// </summary>
    [Fact]
    public void ThereIsAlwaysOneDefaultLayoutMovedByItsButton()
    {
        var (config, bar) = Three();
        var (home, work, gaming) = (config.Layouts[0], config.Layouts[1], config.Layouts[2]);
        string?[] Badges() => [.. LayoutThings(config, bar).Select(thing => thing.Badge)];
        bool[] Pressable() => [.. config.Layouts.Select(each => Row(config, bar, DefaultOf(each)).Enabled)];
        Assert.DoesNotContain(OptionRows.Flat(BarLooks.Rows(config, bar, dark: true, LooksScope.Layouts)), row => row.Key == BarLooks.DefaultRow);
        var row = Row(config, bar, DefaultOf(work));
        Assert.Equal((OptionRowKind.Action, S.SettingsLayoutsDefaultSet, S.SettingsLayoutsDefaultDescription, (string?)null), (row.Kind, row.Title, row.Detail, row.Why));
        // The menu says it under its name, as any action's detail; greyed on the default.
        FlyoutItem Item(string layout) => TheDock.LooksItems(config, bar, dark: true, [], LooksScope.Layouts).Single(each => each.Text == layout).Children!.Single(kid => kid.Text == S.SettingsLayoutsDefaultSet);
        Assert.Equal((S.SettingsLayoutsDefaultDescription, true, (FlyoutControl?)null), (Item("Work").Detail, Item("Work").Enabled, Item("Work").Control));
        Assert.False(Item(home.Name).Enabled);
        Assert.Equal([S.SettingsLayoutsDefaultBadge, null, null], Badges());
        Assert.Equal([false, true, true], Pressable());

        // Set as default in its expander: the badge moves there, its button greyed, the others' not.
        Assert.True(MakeDefault(config, bar, work));
        Assert.Equal(work.Id, config.DefaultLayout.Id);
        Assert.Equal([null, S.SettingsLayoutsDefaultBadge, null], Badges());
        Assert.Equal([true, false, true], Pressable());
        // Pressed again on the default: nothing; the default stays, still one.
        Assert.False(MakeDefault(config, bar, work));
        Assert.False(BarLooks.WriteDefault(config, "gone"));
        Assert.Equal(work.Id, config.DefaultLayout.Id);
        // The badge is the default's whatever this display shows; "This display" stays the shown one's.
        Assert.True(ShowHere(config, bar, gaming));
        Assert.Equal([null, S.SettingsLayoutsDefaultBadge, null], Badges());
        Assert.Equal([null, null, S.SettingsSectionsThisDisplay], LayoutThings(config, bar).Select(thing => thing.Summary));
        // A display seen for the first time shows it, Karpo on.
        config.EditScreen(Nine, "Nine");
        Assert.Equal(("Work", true), (config.ActiveLayout(Nine).Name, config.ScreenFor(Nine).Enabled));

        // The default deleted on the page: the first layout left is the default, badged; one badge always.
        Assert.True(Act(config, bar, BarLooks.KeyOf(BarLooks.DeleteRow, work), act: true, scope: LooksScope.Layouts));
        Assert.Equal(home.Id, config.DefaultLayout.Id);
        Assert.Equal([S.SettingsLayoutsDefaultBadge, null], Badges());
        Assert.Equal([false, true], Pressable());
        Assert.Equal(home.Id, config.ActiveLayout(Nine).Id);
        config.EditScreen(Eight, "Eight");
        Assert.Equal((home.Id, true), (config.ActiveLayout(Eight).Id, config.ScreenFor(Eight).Enabled));
        Assert.True(MakeDefault(config, bar, gaming));
        Assert.Equal([null, S.SettingsLayoutsDefaultBadge], Badges());
        // §585: the layouts' alone; in no display's expander.
        Assert.DoesNotContain(DisplayThings(config, bar).SelectMany(thing => thing.All), each => each.Key.StartsWith(BarLooks.DefaultRow, StringComparison.Ordinal));
    }

    /// <summary>
    /// §585 (the person, 2026-11-09: "the displays should actually be ordered the same way everywhere but the one that is the
    /// one on which the flyout is shown should be highlighted or marked"; "the displays should get the expansion section for
    /// their settings too"): every display connected, each once, in Windows' one order whichever display the page is shown on —
    /// the same list from each —, the one it is shown on marked "This display", not moved (§579 put it first); each an
    /// expander of its Layout and Position, which set that display alone.
    /// </summary>
    [Fact]
    public void TheDisplaysAreInOneOrderEverywhereThisOneMarked()
    {
        var (config, _) = Three();
        const string Third = "\\\\.\\DISPLAY3";
        // Windows' order: Two (the primary), Three, One; listed twice by mistake.
        IReadOnlyList<(string, string)> windows = [(Other, "Two"), (Third, "Three"), (Display, "One"), (Display, "One")];
        BarSpot On(string id, string name) => new(id, name) { Attached = [Display, Other], Connected = windows };
        var bar = On(Display, "One");
        Assert.Equal([Other, Third, Display], BarLooks.DisplaysOf(bar).Select(each => each.Display));
        Assert.Equal(["Two", "Three", "One"], BarLooks.DisplaysOf(bar).Select(each => each.Name));
        Assert.All(BarLooks.DisplaysOf(bar), each => Assert.Equal(bar.Attached, each.Attached));
        // Alone, it lists itself; one Windows has not listed (yet) stands first.
        Assert.Equal([Display], BarLooks.DisplaysOf(Spot()).Select(each => each.Display));
        Assert.Equal(["\\\\.\\DISPLAY9", Other, Third, Display], BarLooks.DisplaysOf(On("\\\\.\\DISPLAY9", "Nine")).Select(each => each.Display));

        // The same order from every display, the mark on the one the page is shown on.
        foreach (var (id, name) in windows.Distinct())
        {
            var things = DisplayThings(config, On(id, name));
            Assert.Equal(["Two", "Three", "One"], things.Select(thing => thing.Title));
            Assert.Equal(
                windows.Distinct().Select(each => each.Item1 == id ? S.SettingsSectionsThisDisplay : null),
                things.Select(thing => thing.Summary));
        }
        // Each an expander of its own Layout and Position, nothing to delete, rename or move.
        Assert.All(DisplayThings(config, bar), thing =>
        {
            Assert.Equal((OptionGlyphs.Monitor, 2), (thing.Glyph, thing.Rows.Count));
            Assert.Equal((OptionRowKind.Choice, OptionRowKind.Choice), (thing.Rows[0].Kind, thing.Rows[1].Kind));
            Assert.Equal((null, null, null, 0), (thing.Delete, thing.Move, thing.Name, thing.Acts.Count));
        });
        Assert.Equal([ShownOf(Other), PositionOf(Other)], DisplayThings(config, bar)[0].Rows.Select(row => row.Key));

        // Another display's Layout and Position are its own: this one's stay.
        Assert.True(Act(config, bar, ShownOf(Other), at: 2));
        Assert.Equal(("Gaming", config.Layouts[0].Name), (config.ActiveLayout(Other).Name, config.ActiveLayout(Display).Name));
        Assert.True(Act(config, bar, PositionOf(Other), at: Array.IndexOf(BarLooks.Edges, Edge.Left)));
        Assert.Equal((Edge.Left, Edge.Bottom), (config.EdgeOfScreen(Other), config.EdgeOfScreen(Display)));
        // A display without a bar yet (Karpo never shown there) gets its entry as it is set, under its own name.
        Assert.True(Act(config, bar, ShownOf(Third), at: 1));
        Assert.Equal("Work", config.ActiveLayout(Third).Name);
        Assert.Equal("Three", config.Screens.Single(screen => screen.Id == Third).Name);

        // The panel: each display an expander under Displays, keyed apart (two of one name too), its two rows in it; the mark
        // under its name.
        var twins = bar with { Connected = [(Other, "Generic"), (Third, "Generic"), (Display, "One")] };
        var page = PanelRows.Page(PanelPage.Layouts, "", "", TheDock.LooksItems(config, twins, dark: true, [], LooksScope.Layouts));
        var displays = page.Rows.Where(each => each.Key.StartsWith("Layouts/" + S.SettingsSectionsDisplays + "/", StringComparison.Ordinal)).ToList();
        Assert.Equal(
            [("Layouts/" + S.SettingsSectionsDisplays + "/Generic", null), ("Layouts/" + S.SettingsSectionsDisplays + "/Generic #2", null), ("Layouts/" + S.SettingsSectionsDisplays + "/One", S.SettingsSectionsThisDisplay)],
            displays.Select(each => (each.Key, each.Detail)));
        Assert.All(displays, each => Assert.Equal((PanelRowKind.Expander, 2, false), (each.Kind, each.Rows.Count, each.Drags)));
        Assert.Equal([PanelRowKind.Picker, PanelRowKind.Picker], displays[2].Rows.Select(each => each.Kind));
        Assert.Equal(page.Rows.Count, page.Rows.Select(each => each.Key).Distinct().Count());
        Assert.Equal([S.SettingsSectionsDisplays, S.CoreBarMenuLayouts], page.Rows.Where(each => each.Kind == PanelRowKind.Heading).Select(each => each.Text));
    }

    /// <summary>
    /// §520: the bar's looks are the settings panel's Display, Taskbar and Appearance pages, done the dock's way: each page its
    /// part of the bar's rows (Dock.PanelOf), its picks the looks' handler's; on a display showing None no Taskbar page, owned
    /// by the bar holding edit mode; the bar's own right click in edit mode opens the panel on its Taskbar page; a folder's room
    /// keeps its menu, the folders'.
    /// </summary>
    [Fact]
    public void TheBarsLooksAreThePanelsPagesTheDocksWay()
    {
        var repo = RepoSource.Root();
        var panel = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.SettingsPanel.cs"));
        Assert.Contains("new Pending(Topic.LayoutEditor, owner) { Looks = bar, LooksScope = scope },", panel);
        Assert.Contains("picks => LooksRows(bar, picks, scope));", panel);
        Assert.Contains("if (own is null && scope == LooksScope.Taskbar)", panel);
        Assert.Contains("var owner = BarOf(id) ?? editor;", panel);
        var looks = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.BarLooks.cs"));
        Assert.Contains("TypeTitle = BarLooks.Kind(config, bar, what.LooksScope)", looks);
        Assert.Contains("LooksScope = LooksScope.Folders, Folder = open", looks);
        Assert.DoesNotContain("DeckLooks", File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.MenuPickers.cs")));
        Assert.Contains("OpenPanel(window.Id, PanelPage.Taskbar);", File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.cs")));
        Assert.Equal(["Layouts", "AddLayout", "Taskbar", "Appearance", "Folders"], Enum.GetNames<LooksScope>());
    }

    /// <summary>
    /// §521: what cannot be done is greyed, saying why: past the free version's layouts a new one and a copy; the last
    /// layout's Delete; each one's rows on the Layouts page as the panel shows them — an expander of its name, icon, copy and
    /// Delete, "Add a layout" §628 a link to its page of the presets (greyed past the licence, why under it, leading nowhere).
    /// </summary>
    [Fact]
    public void TheLayoutsPageGreysWhatCannotBeDone()
    {
        var (config, bar) = Three();
        var free = new LicenseStatus(LicenseTier.Free, LicenseBasis.StoreFree);
        List<OptionPick> picks = [];
        // §585: after the displays and the Layouts heading; §595 the layouts first, no Default layout over them.
        var items = TheDock.LooksItems(config, bar, dark: true, picks, LooksScope.Layouts, free).SkipWhile(item => !(item.Header && item.Text == S.CoreBarMenuLayouts)).Skip(1).ToList();
        Assert.Equal([config.Layouts[0].Name, "Work", "Gaming", "", S.SettingsLayoutsAdd], items.Select(item => item.Separator ? "" : item.Text));
        Assert.All(items.Take(3), item =>
        {
            Assert.Equal([S.SettingsLayoutsNameHeader, S.SettingsLayoutsIconHeader, S.SettingsLayoutsDefaultSet], item.Children!.Select(row => row.Text));
            // Its grip (§551: dragged by it in a menu and in the settings panel alike, no ↑ ↓), its Duplicate, and its Delete at its end.
            var tools = Assert.IsType<FlyoutControl.Tools>(item.Control).Buttons;
            Assert.Equal([(true, ""), (false, OptionGlyphs.Copy), (false, OptionGlyphs.Delete)], tools.Select(tool => (tool.Grip, tool.Grip ? "" : tool.Glyph)));
            Assert.True(tools[0].Enabled);
            Assert.Equal((true, S.SettingsLayoutsDelete), (tools[^1].Enabled, tools[^1].Name));
        });
        // The panel reads each as an expander of its rows, dragged into order, its Duplicate and Delete at its head's end, "This
        // display" under the one shown, §595 the default's badge after its name; §636 in each its words before Set as default, a
        // button in a card (not a foot), greyed on the default alone.
        var page = PanelRows.Page(PanelPage.Layouts, "", "", items);
        Assert.All(page.Rows.Take(3), row => Assert.Equal((PanelRowKind.Expander, 3, 2, true), (row.Kind, row.Rows.Count, row.Tools.Count, row.Drags)));
        Assert.Equal([(S.SettingsSectionsThisDisplay, S.SettingsLayoutsDefaultBadge), (null, null), (null, null)], page.Rows.Take(3).Select(row => (row.Detail, row.Badge)));
        Assert.Equal(
            [(PanelRowKind.Action, S.SettingsLayoutsDefaultSet, S.SettingsLayoutsDefaultDescription, true, false), (PanelRowKind.Action, S.SettingsLayoutsDefaultSet, S.SettingsLayoutsDefaultDescription, false, false)],
            page.Rows.Take(2).Select(row => row.Rows[^1]).Select(set => (set.Kind, set.Text, set.Detail, set.Greyed, set.Foot)));
        var add = items[^1];
        Assert.Equal((false, LicenseGate.Layouts(free, config.Layouts.Count), null), (add.Enabled, add.Detail, add.Children));
        // §551: past the licence's layouts its Duplicate greyed, why its name.
        var copy = ((FlyoutControl.Tools)items[2].Control!).Buttons[^2];
        Assert.Equal((false, OptionGlyphs.Copy), (copy.Enabled, copy.Glyph));
        Assert.NotEqual(S.SettingsLayoutsDuplicate, copy.Name);

        // One layout: its Delete greyed, saying why.
        var one = Fresh();
        var only = TheDock.LooksItems(one, Spot(), dark: true, [], LooksScope.Layouts).SkipWhile(item => !(item.Header && item.Text == S.CoreBarMenuLayouts)).ElementAt(1);
        var bin = Assert.IsType<FlyoutControl.Tools>(only.Control).Buttons[^1];
        Assert.Equal((false, S.SettingsLayoutsLastOne), (bin.Enabled, bin.Name));

        // §507: no styles; the look's reset is inside Look ›, none on the Taskbar page.
        Assert.DoesNotContain(TheDock.LooksItems(config, bar, dark: true, [], LooksScope.Taskbar), item => item.Text == S.SettingsAppearanceCustomResetAll);
    }

    /// <summary>
    /// Another layout shown from the bar's menu in edit mode is the one arranged: the menu read again
    /// is that layout's (its title, its size), and the dock applies the pick as every look of the bar
    /// (LooksPicked, and a name typed LooksKept, both Apply) — nothing of the menu's ends edit mode. A
    /// layout of the same type keeps the bar's window, arranged; another type makes the bar again, and
    /// edit mode passes to it (Dock.EditHolder, §296, EditHolderTests).
    /// </summary>
    [Fact]
    public void AnotherLayoutShownInEditModeIsTheOneArranged()
    {
        var (config, bar) = Three();
        var home = config.Layouts[0];
        Assert.True(ShowHere(config, bar, config.Layouts[1]));
        var work = config.ActiveLayout(Display);
        Assert.Equal("Work", work.Name);
        Assert.Equal(BarLooks.TypeName(LayoutType.Taskbar), BarLooks.Kind(config, bar));
        Assert.True(Size(config, bar, 64));
        Assert.Equal((64, Config.DefaultHeight), (work.Size, home.Size));
        Assert.Equal(config.EdgeOfScreen(Display), Edge.Bottom);

        var repo = RepoSource.Root();
        var looks = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.BarLooks.cs"));
        var fields = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.MenuFields.cs"));
        Assert.Equal(2, Regex.Matches(looks, @"Apply\(next\);").Count);
        Assert.Contains("LooksKept(bar, what.LooksScope, pick, kept)", fields);
        Assert.DoesNotContain("EndEdit", looks);
        Assert.DoesNotContain("LeaveEdit", looks);
    }

    /// <summary>
    /// The Type choice offers Taskbar and Dock, nothing else (the person, 2026-10-30: "the dock with two rows style should be
    /// removed"; 2026-11-02, §488: "the top and bottom layout style should also be removed").
    /// </summary>
    [Fact]
    public void TheTypeChoiceOffersTaskbarAndDockOnly()
    {
        var type = Row(Fresh(), Spot(), BarLooks.TypeRow);
        Assert.Equal([S.SettingsLayoutsTypeTaskbar, S.SettingsLayoutsTypeDock], type.Choices);
        Assert.Equal([LayoutType.Taskbar, LayoutType.Dock], Enum.GetValues<LayoutType>());
    }

    [Fact]
    public void EachLookOfTheBarsMenuSetsWhatItSays()
    {
        var config = Fresh();
        var layout = config.ActiveLayout(Display);

        Assert.True(Act(config, Spot(), PositionOf(Display), at: Array.IndexOf(BarLooks.Edges, Edge.Top)));
        Assert.Equal(Edge.Top, config.EdgeOfScreen(Display));
        Assert.False(Act(config, Spot(), PositionOf(Display), at: Array.IndexOf(BarLooks.Edges, Edge.Top)));

        Assert.True(Act(config, Spot(), BarLooks.TypeRow, at: Array.IndexOf(LayoutTypes.All, LayoutType.Dock)));
        Assert.Equal(LayoutType.Dock, layout.Type);

        // §333: the size a stepper in pixels, over all a bar may be, a step of four; and how the bar shows.
        var size = Row(config, Spot(), BarLooks.SizeRow);
        Assert.Equal(
            (OptionRowKind.Number, Config.DefaultHeight, Config.MinHeight, Config.MaxHeight, BarLooks.SizeStep, OptionUnit.Pixels, BarLooks.SizeName(Config.DefaultHeight)),
            (size.Kind, size.Value, size.Least, size.Most, size.Step, size.Unit, size.Shown));
        Assert.True(Act(config, Spot(), BarLooks.SizeRow, by: 1));
        Assert.True(Act(config, Spot(), BarLooks.ShowRow, at: Array.IndexOf(BarLooks.Shows, EdgeMode.Hover)));
        Assert.Equal((52, EdgeMode.Hover), (layout.Size, layout.Show));
        Assert.True(Act(config, Spot(), BarLooks.SizeRow, by: -1));
        Assert.True(Act(config, Spot(), BarLooks.SizeRow, by: -1));
        Assert.Equal(44, layout.Size);
        // Typed: any size, held to what a bar may be; a size dragged to says itself and steps from there.
        Assert.True(Size(config, Spot(), 45));
        Assert.Equal(BarLooks.SizeName(45), Row(config, Spot(), BarLooks.SizeRow).Shown);
        Assert.True(Act(config, Spot(), BarLooks.SizeRow, by: 1));
        Assert.Equal(49, layout.Size);
        Assert.True(Size(config, Spot(), 500));
        Assert.Equal(Config.MaxHeight, layout.Size);
        Assert.False(Size(config, Spot(), Config.MaxHeight + 1));
        Assert.False(Act(config, Spot(), BarLooks.SizeRow, by: 1));
        Assert.True(Size(config, Spot(), 0));
        Assert.Equal(Config.MinHeight, layout.Size);
        Assert.False(Act(config, Spot(), BarLooks.SizeRow, by: -1));
        // The stepper's buttons greyed at either end.
        List<OptionPick> picks = [];
        var stepper = Assert.IsType<FlyoutControl.Stepper>(TheDock.LooksItems(config, Spot(), dark: true, picks, LooksScope.Taskbar).Single(item => item.Text == S.SettingsLayoutsSizeHeader).Control);
        Assert.Equal((false, true, BarLooks.SizeName(Config.MinHeight)), (stepper.CanFewer, stepper.CanMore, stepper.Value));
    }

    /// <summary>
    /// §333: a size is typed too, as an amount is (§332, AmountFinder): its stepper's row opens a level of
    /// its field, offered the person's other layouts' bars' sizes — each with its layout's name — then the
    /// usual ones, each once, the bar's own ticked. Typed, the number its own row first;
    /// kept rounded and held to what a bar may be, nothing typed Windows' 48, words no number nothing.
    /// </summary>
    [Fact]
    public async Task ASizeTypedIsOfferedTheOtherBarsSizesThenTheUsualOnes()
    {
        var config = Fresh();
        var home = config.ActiveLayout(Display);
        BarLooks.Add(config, "Work").Size = 64;
        BarLooks.Add(config, "Tall").Size = 56;

        var size = Row(config, Spot(), BarLooks.SizeRow);
        Assert.Equal((OptionRowKind.Number, "48", "48", BarLooks.SizeLongest), (size.Kind, size.Text, size.Placeholder, size.Longest));
        var finder = Assert.IsType<AmountFinder>(size.Finder);
        Assert.True(finder.Suggests);
        Assert.Equal(["64", "56", "32", "40", "48", "80"], finder.Suggested.Select(row => row.Title));
        Assert.Equal(["Work", "Tall", "", "", "", ""], finder.Suggested.Select(row => row.Detail));
        Assert.Equal(["48"], finder.Suggested.Where(row => row.Chosen).Select(row => row.Title));
        Assert.Equal(["5", "56"], (await finder.FindAsync("5", CancellationToken.None)).Select(row => row.Title));
        Assert.Equal(["72"], (await finder.FindAsync("72", CancellationToken.None)).Select(row => row.Title));
        Assert.Empty(await finder.FindAsync("tall", CancellationToken.None));

        // Kept as typed, rounded, held to the range; nothing typed the default; words no number nothing.
        Assert.True(size.SetText!("72"));
        Assert.Equal(72, home.Size);
        Assert.True(BarLooks.WriteTyped(home, "45.6", NumberMarks.Region()));
        Assert.Equal(46, home.Size);
        Assert.True(BarLooks.WriteTyped(home, "500", NumberMarks.Region()));
        Assert.Equal(Config.MaxHeight, home.Size);
        Assert.True(BarLooks.WriteTyped(home, "10", NumberMarks.Region()));
        Assert.Equal(Config.MinHeight, home.Size);
        Assert.True(BarLooks.WriteTyped(home, " ", NumberMarks.Region()));
        Assert.Equal(Config.DefaultHeight, home.Size);
        Assert.False(BarLooks.WriteTyped(home, "big", NumberMarks.Region()));
        Assert.False(BarLooks.WriteTyped(home, "48", NumberMarks.Region()));

        // In the menu: the stepper's row has its field's level, the field's pick the size's; a number
        // without a field (a separator's width) none.
        List<OptionPick> picks = [];
        var item = TheDock.LooksItems(config, Spot(), dark: true, picks, LooksScope.Taskbar).Single(each => each.Text == S.SettingsLayoutsSizeHeader);
        Assert.IsType<FlyoutControl.Stepper>(item.Control);
        var field = Assert.Single(item.Children!);
        Assert.Equal(("48", 3), (Assert.IsType<FlyoutField>(field.Control).Text, Assert.IsType<FlyoutField>(field.Control).Longest));
        Assert.Same(typeof(AmountFinder), Assert.IsType<FlyoutField>(field.Control).Finder!.GetType());
        Assert.Equal((OptionPickKind.Field, BarLooks.SizeRow), OptionMenu.PickOf(picks, field.Id) is { } pick ? (pick.Kind, pick.Key) : default);
    }

    // --- Pictures -------------------------------------------------------------------------------------

    private const float Scale = 1.5f;

    private static readonly RECT Work = new() { right = (int)(1920 * Scale), bottom = (int)(1040 * Scale) };

    private static Flyout Show(Gfx gfx, HINSTANCE instance, IReadOnlyList<FlyoutItem> items, int eighth)
    {
        var menu = new Flyout(gfx, instance, 0) { Configures = true };
        menu.Show(gfx, AddListPictures.LookFor(dark: true), Scale, items, FlyoutLayout.List, new POINT { x = Work.right * eighth / 8, y = Work.bottom }, Work, Edge.Bottom, null);
        WindowShots.Settle();
        return menu;
    }

    private static LPARAM At(POINT point) => (LPARAM)(nint)((point.y << 16) | (point.x & 0xFFFF));

    /// <summary>A click (down and up) at <paramref name="point"/>, after the pointer came there; what the release said.</summary>
    private static FlyoutEvent Click(Flyout menu, POINT point)
    {
        _ = menu.Message(WM.WM_MOUSEMOVE, 0, At(point));
        _ = menu.Message(WM.WM_LBUTTONDOWN, 1, At(point));
        return menu.Message(WM.WM_LBUTTONUP, 0, At(point));
    }

    private static FlyoutEvent Key(Flyout menu, int key) => menu.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)key, 0);

    /// <summary>
    /// §290's pictures, 150 %, dark: the bar's menu, its colours' level and its size's
    /// (bar-looks-dark.png); §309's, the menu of a bar of three layouts, its Layouts level and two
    /// layouts' own levels, one not shown and the one shown (bar-layouts-dark.png); a separator's menu (and one with a width and a room chosen, §302), a
    /// divider's and Show desktop's, as edit mode shows them (separator-looks-dark.png).
    /// </summary>
    [UiFact]
    public void BarLooksPictures()
    {
        WindowShots.OnHiddenDesktop("bar-looks", (gfx, instance) =>
        {
            var black = ((byte)0, (byte)0, (byte)0);
            bool Save(IEnumerable<Flyout> menus, string name)
            {
                WindowShots.Settle(300);
                return WindowShots.SaveTogether([.. menus.SelectMany(menu => menu.OpenPanels)], $"{name}-dark.png", black);
            }
            var config = Fresh();
            List<FlyoutItem> Bar() => TheDock.LooksItems(config, Spot(), dark: true, [], LooksScope.Taskbar);
            var colors = Bar().Single(item => item.Text == S.SettingsLayoutsShowHeader);
            // §333: and its size's level, a number typed over the sizes offered.
            var size = Bar().Single(item => item.Text == S.SettingsLayoutsSizeHeader);
            using (var root = Show(gfx, instance, Bar(), 1))
            using (var level = Show(gfx, instance, Bar(), 4))
            using (var typed = Show(gfx, instance, Bar(), 7))
            {
                // §517: its Show (§507 to §513 its Look ›, a way to Look ▾ now).
                level.OpenLevel(colors.Id);
                Assert.Equal((1, S.SettingsLayoutsShowHeader), (level.InlineDepth, level.InlineTitle));
                _ = typed.Click(size.Id);
                Assert.Equal((1, S.SettingsLayoutsSizeHeader), (typed.InlineDepth, typed.InlineTitle));
                Assert.True(Save([root, level, typed], "bar-looks"));
            }

            List<FlyoutItem> Arranged(WidgetItem item, Config? of = null) =>
                [.. TheDock.WidgetRowsFor(item), .. OptionMenu.Section(OptionRows.Of(item, null, new OptionPlace(of ?? config, Dark: true)), [])];
            // A divider's Style (§435); Show desktop's style.
            using var divider = Show(gfx, instance, Arranged(ListKind.Divider.New()), 5);
            using var desktop = Show(gfx, instance, Arranged(BarKind.ShowDesktop.New()), 7);
            Assert.True(Save([divider, desktop], "separator-looks"));
        });
    }

    /// <summary>§507: the look editor's rows wear no icon twice (R13): each section's.</summary>
    [Fact]
    public void ALookEditorWearsEachIconOnce()
    {
        var config = new Config();
        var place = new OptionPlace(config, Dark: true);
        foreach (var part in LookParts.All)
        {
            var slot = Looks.Section(config, part);
            Assert.Distinct(Glyphs(LookRows.Editor(place, slot, "k")));
        }

        static List<string> Glyphs(IEnumerable<OptionRow> rows) => [.. rows.Where(row => row.Kind != OptionRowKind.Heading && row.Glyph.Length > 0).Select(row => row.Glyph)];
    }
}
