// edit: how a bar looks, as rows of its menu in edit mode (the person,
// 2026-10-22: "when i right click in edit mode onto the taskbar all the appearance settings should
// appear in the context menu. the same should be for the divider context menu"; docs\widgets.md §290).
//
// A bar's looks are spread over three pages of the settings app, each where Windows would put it: its
// position is the display's (Displays), its type, size and how it shows its layout's (Layouts), its
// material, opacity and colours every bar's (Appearance). The menu has them all, for the bar it was
// opened on, as OptionRows (Rows), and this is where what each offers is said once — the edges, the
// types, the sizes, the ways of showing, the materials, their names — and where a change is written,
// so the pages and the menu offer and write the same. A folder's divider has no look (§505: empty space; §290 to
// §502 a kind of separators' look, "Taskbar separators" and "Folder separators", was set here and later in
// a style); the bar has no separator since §511.
//
// §309 (the person, 2026-10-23: "in the taskbar context menu there should also be the option to select
// the taskbar shown and add / remove additional taskbars. it should basically be on par with the
// configuration from the configuration app"): the menu has the layouts too, the rest of what those
// pages offer for a bar, under one row, Layouts (Layouts): which this display shows, or None (the
// Displays page's dropdown, DisplayLayoutChoice), a new one and a copy of this one, each shown here at
// once and named as it is made, this one's name, icon and Delete (a Layouts page's card), and which
// new displays get (the Displays page's last card); and Appearance's "Reset all" under the colours.
// Made, copied, named, deleted and chosen here alone (Add, Duplicate, WriteName, WriteIcon,
// WriteShown, WriteDefault): the settings app's model writes through them too.
//
// §334: the taskbar and the folders each have their own look (SurfaceLook), and one builder offers
// either, Appearance(config, layout, surface, dark): the material and its opacity, its colours of one's own
// (SurfaceLooks.Slots) with their Reset all, its separators' look, and whether what stands on it opens
// on hover. The bar's menu has the taskbar's; an arranged folder's empty room the folders' (Dock's
// FolderLooksMenu), and the edit bar's Folders ▾ is to have them too. (§514: no SurfaceLook; one switch, the layout's
// OpenOnHover, opens on hover whatever a widget opens, a folder or a widget's flyout: Behavior's row and the folder room's.)
//
// §330 (the person, 2026-10-23: "the taskbar and screen settings menu add them to the top edit bar"):
// the edit bar has the menu's two halves, each a menu of its own (LooksScope): the display's — which
// layout it shows or None, where its bar stands, what new displays show; the Displays page's — from
// the layout's button, and the taskbar's — the layouts made, copied, named and deleted, the type,
// size, showing, material and colours; the Layouts and Appearance pages' — from "Taskbar". One list of rows, cut
// three ways (Rows); §520 each a page of the settings panel.
//
// §333 (the person, 2026-10-23: "taskbar size should be a number input and not a select input";
// "improve the design of the layout settings on the taskbar context menu. they should be in behaviour
// be similar to the faces one"): the size is a stepper in pixels over the whole range a bar may be
// (Config.MinHeight to MaxHeight), a number typed into it too, the usual sizes and the person's other
// bars' offered (SizeFinder); the Layouts level is the person's own things' (OptionOwned, §324), as a
// folder's faces are listed: each layout a row, the one shown ticked, a click showing it here, its
// grip dragging it into order (Config.Layouts' order, MoveLayout), its bin deleting it (asked first,
// greyed on the last), its own level naming, drawing and copying it — that layout, whichever is
// shown; "Add layout" under them, made in one click. In the whole menu None and what new displays
// show follow under a rule; in the taskbar's half they are the display's half's alone.
//
// §336 (the person, 2026-10-23: "click should open its settings like faces"): a layout's row is no
// choice; a click on it, Enter or Right opens its own level, as a face's row does, the one shown still
// ticked. Which layout this display shows is the display's half's choice (Shown, the edit bar's layout
// button); in the whole menu a layout's level has "Show on this display" first.
//
// §337 (the settings plan's Q4 A): the taskbar's half, and the whole, end with the General page's
// taskbar behaviour under one row, "Behavior" (Behaviour): hide the Windows taskbar, combine the app
// buttons, window previews, and whether the widgets' flyouts open on hover; the folders' menu keeps its
// own "Open folders" row alone.
//
// §343 (the person, 2026-10-25: "the first button is just about the layouts / multiple layouts, you can
// choose the default one and also delete them etc. ... then the position moves over to the second menu
// as well"): the edit bar cuts the menu in two again, differently. Its first button's menu is the
// layouts alone (LooksScope.Layouts): the Layouts level as the menu itself — each layout, the one this
// display shows ticked, each one's own level with "Show on this display", Add layout, None, New displays
// show. Its "Taskbar" has the rest, where the bar stands first (LooksScope.Taskbar). The display's half
// of §330 is gone: nothing was left in it.
//
// §345 (the person, 2026-10-25: "hiding windows taskbar should be a general setting, not a layout one";
// "the style and stuff like that should be fully configurable independently on the layout"): everything
// the Taskbar menu has is the shown layout's — its type, size and showing as before, and now its look
// (Appearance for the layout, TaskbarLayout.Look) and its behaviour (combine, previews, flyouts on hover).
// Hide the Windows taskbar is the whole of Karpo's, a row of ⚙ Settings (KarpoMenu.Startup.cs).
//
// §348 (the person, 2026-10-25: "taskbar position should be kept in the screen settings then, also put
// this into the screen configuration again"): the edit bar's general group has the display's own menu
// again (LooksScope.Display, DisplayRows): where its bar stands, which layout it shows — the layouts and
// None as the menu's own rows, ticked —, and what new displays show. Each thing once: Layouts ▾ keeps the
// layouts themselves (made, copied, named, drawn, put in order, deleted; each one's "Show on this
// display"), no tick and no None of its own; Taskbar ▾ the shown layout's taskbar, no Position. The
// right click's whole is the three in the edit bar's order.
//
// §352 (the person, 2026-10-25, the edit bar as a breadcrumb: "sounds good try that"): the edit bar's
// Layout ▾ is the shown layout's whole, in sections under their headings (LooksScope.Layout, LayoutRows):
// Taskbar — its type, size, showing and look —; Folders — the layout's folders' look and whether they open
// on hover, keyed apart from the taskbar's (FoldersKey) —; Behavior — combine, previews, flyouts on hover —;
// and "Manage layouts ›", the Layouts level (made, copied, named, drawn, put in order, deleted). Layouts ▾,
// Taskbar ▾ and Folders ▾ went into it. The right click's whole is Display ▾'s and Layout ▾'s, in that order.
//
// §361 (the person, 2026-10-26: "managing layouts should happen in the screen settings not all the way on
// the bottom of the layout settings"): the layouts are managed in Display ▾, where which one the display
// shows is chosen, as one list (Layouts, inline there): each layout a row, the one shown ticked, its own
// level with "Show on this display" (an action again, no link: the same menu), its name, icon and a copy;
// Add layout under them; then None and New displays show. Layout ▾ is the shown layout's own alone —
// Taskbar, Folders, Behavior. Which layout a display shows has no choice row of its own any more.
//
// §364 (the settings scope audit, the person: "fix them as suggested"): each menu says whose its rows are. A
// layout's menus name the layout over them — Layout ▾ "Layout · Work", an arranged folder's room "Folders ·
// Work" (its rows are every folder's of the layout, not that folder's), the right click "Taskbar · Work"
// (Kind) —, and the right click heads the display's rows "This display" (§411: gone). (On a Top and bottom
// Layout ▾ had each bar's size and showing, named, until §488 took that type away.) Add layout and Duplicate say
// that they show what they make here.
//
// §396 (the person, 2026-10-29: "the theming with the colors and material theme currently is a little bit all over
// the place"): Layout ▾ opens on its Appearance — the Theme (Themes), then Colors ›, the layout's nine roles
// (ColorRoles) each Inherited from the theme until set, and Reset all (ColorRows) —; the Taskbar's and the Folders'
// sections keep how each is made (material, opacity) and their separators' style. The folders' own colours of
// §334 and the separators' "Line color" are gone into the roles.
//
// §400 (the person, 2026-10-29: "improve the "layout" menu. right now you have to scroll on the first level and
// it is not that well structured"): Layout ▾'s first level is the layout's bars — Type, Size, Show — and, ruled
// off, its pages, as Windows' Settings groups a page's rows behind a few: Appearance › (the theme, Colors ›, then
// how the taskbar is made), Folders › (the folders' look and opening, the folder room's own menu), Behavior ›
// (LayoutRows). No headings, nothing to scroll. The right click's whole heads them "Layout" under the display's.
//
// §405 (the person, 2026-10-29: "the styles are still way to complicated. i think there are actually too many
// customization options"): Appearance is Karpo's, every layout alike (Config.Appearance), seven rows: the Theme,
// Customize › (Background, Accent, Text, Reset), Taskbar opacity, Folder opacity, Taskbar separators, Folder
// separators, Outlines (AppearanceRows). Material, the nine colours, the separators' width and room are gone. The
// Folders page is gone with them: its opacity and separators into Appearance, Open folders into Behavior. The folder
// room's right click keeps the folders' three (FolderRows).
//
// §409 (the person, 2026-10-29: "new displays show setting should be on the top"): New displays show is the
// display's rows' first (DisplayRows) — Display ▾'s top row, ruled off from Position; the right click's first under
// "This display" — no row of the Layouts level any more.
//
// §411 (the menu rules, the person approving them: "go with all your recommendations"): Display ▾'s Position is its
// four icons in its row (a place), its layouts under a rule and no heading, "Add a layout" and "Duplicate" short;
// Appearance has the colours itself (Customize gone), each row its own icon, "Reset all" last; Window previews a
// switch; Show says itself short at its row's end. The right click on the bar is Layout ▾'s rows and, under a rule,
// "Display ↗", the way to Display ▾ (OptionLink.Display): each setting has one home.
//
// §429 (the person, 2026-10-30: "there should be a global list of styles that come from us (the user can add his own as
// well though), and you can basically just apply them to widgets"): Appearance ends with "Styles ↗" (OptionLink.Styles),
// the styles' own menu (LooksScope.Styles, StyleRows) under Layout ▾'s crumb: ours, each with its swatch and a copy at its
// row's end; ruled off, the person's, each its own level — its name, its Light background, accent, text and outlines,
// "Same in dark mode" and, set apart, its Dark ones —, a copy and a bin at its row's end; "Add a style" under them. A
// style's level is one level below the menu's first so its colours' choices are one more (§411 R1).
//
// §441 (the person, 2026-10-30: "the opacity, the colors, the primary color, the accent colors, the outline settings,
// etc. should all go into the style"): Appearance is no page any more but the one choice of the style Karpo wears, each
// with its swatch, "Styles ↗" under them (WidgetLooks.KarpoRow) — a widget's and a folder's Appearance's twin. The theme,
// the colours, the opacities, the separators and the outlines are a style's settings, set on a style of the person's
// level (StyleLevelRows): its mode and separators, then each mode's colours, outlines and opacities. The folder room's
// right click keeps Open folders alone.
//
// §459 (the person, 2026-10-31: "the styles are not reachable via root menu somewhere, put them either into global
// settings or into layout settings. remove display settings link from display settings. also improve the editor of the
// styles menu and also make sure that it appears everywhere the same"; "separators go back to layout"): Layout ▾ is the
// layout's bars, the Taskbar separators and the Folder separators again (Karpo's, every layout alike), and at its foot
// "Styles ↗" and Behavior. Styles (LooksScope.Styles, StyleRows) is Karpo's style — a choice, each with its swatch — and
// every style, ours and the person's alike, each opening its editor, "Add a style" the foot. A style's editor is a menu
// of its own (LooksScope.Style, EditorRows), so its colours' pickers are its third level (§411 R1): its name (the
// person's), its font and its weight; ruled off, a row for each part opening its colours, and under a rule its dark
// ones where it has different colours in dark mode; that switch; "Duplicate" and "Reset all" its foot. Display ▾'s
// foot is "Styles ↗" (Windows' display settings gone from it).
//
// §464 (the person, 2026-10-31: "yeah lets make it per layout"): which style is worn is the layout's — Layout ▾'s Style
// (StyleRow), ruled with the separators —, and Styles is every style, the layout's ticked; Karpo's style is gone.
//
// §467 (the person, 2026-11-01: "you should be able to edit and create and copy styles as before. right now the list of
// styles is just a fixed list"; "the styles should just be one entry on the top settings level (global settings blue cog)
// and it should show the list of styles and it does not have to adhere to the two levels rule"): the styles are ⚙'s
// (KarpoMenu.Styles), one row of its first level (StylesLevel): every style, each its own level, its editor (EditorRows);
// the person's with a bin; "Add a style" under them. No menu links to them any more (Layout ▾'s and Display ▾'s "Styles ↗",
// a widget's Appearance's, gone); picking one stays where it is worn: Layout ▾'s Style, a widget's Appearance, a folder's
// Face style and Flyout style. Display ▾'s foot is "Add a layout", Layout ▾'s Behavior.
//
// §478 (the person, 2026-11-02: "taskbar and folder separator configuration should go into the style as well and also
// respected as such"): the Taskbar separators and the Folder separators are a style's, the last rows of its Taskbar's and
// its Widgets in folders' levels (PartRows); Layout ▾ is its bars, its Style and Behavior.
//
// §505 (the person, 2026-11-05: "the spacers should always just be empty space and not a line or dot or anything"): a
// separator has no look, so neither row is left; a part's level is its colours alone.
//
// §507 (the person, 2026-11-05: "the styles are too complicated. styles should not exist anymore"): no styles, no ⚙ ›
// Styles, no Style row. Layout ▾ is its bars, its Look › — the layout's own four sections, a palette to apply, Reset all
// (LookRows.LayoutRow) — and Behavior.
//
// §517 (the person, 2026-11-05: "the settings in the topbar should actually be split so that it does not necessarily
// resemble a breadcrumb anymore. it should be split into more logical groups like display settings (currently already
// done), taskbar settings, etc..."; and "1. sections", no entry at the bottom unless the content asks for one): the edit
// bar's menus are side by side, each one page under small headings in a fixed order (the Sections design):
//
//   Display ▾   New displays: Layout; This display: where its bar stands, its layouts, the one shown ticked, None, Add a
//               layout (DisplayRows)
//   Taskbar ▾   Bar: Type, Size, Spacing (§533), Show; Behavior: combine, previews, desktops, flyouts and folders (TaskbarRows)
//   Look ▾      Palette and its Reset to default; two sections, each its parts' editors (§567: Karpo's one Appearance; §577, LookRows.Rows)
//
// Each setting has one of them for its home, by whose it is: where the bar stands is this display's (§348: "taskbar position
// should be kept in the screen settings"), whichever layout it shows, so Display ▾'s; the rest of the bar the shown
// layout's, Taskbar ▾'s. The right click on a bar is Taskbar ▾'s rows and the ways to Look ▾ and
// Display ▾; an arranged folder's room's its Open folders and the way to Look ▾, where the folders' look is. No row stands
// still at a menu's foot here (§423's foot, Behavior and "Add a layout", gone).
//
// §579: the Display page lists every display connected, the one it was opened from first, each its Layout and Position under
// its name (DisplayRows, DisplaysOf); what new displays show is the Layouts page's first row, "Default layout" (Default), the
// layout it names marked Default in the list (Thing).
//
// §585 (the person, 2026-11-09: "merge display settings together with layout settings"; "the displays should actually be
// ordered the same way everywhere but the one that is the one on which the flyout is shown should be highlighted or
// marked"; "the displays should get the expansion section for their settings too"): one page, Layouts
// (LooksScope.Layouts, LayoutsRows) — under Displays every display connected in Windows' one order (DisplaysOf), each an
// expander of its Layout and Position, the flyout's own marked "This display", never moved; under Layouts the Default
// layout and the layouts.
//
// §595 (the person, 2026-11-09: "default layout should not be a dropdown but should be a badge in the layout list and then
// when expanding a layout you should be able to make it the default one"): no Default layout row; the default layout wears
// a badge, "Default", after its name in the list (OptionThing.Badge), and every layout's expander has a switch, "Default
// layout" (DefaultRow keyed with its id): on, new displays show it; off on the default, they show None.
//
// §636 (the person, 2026-11-09: "one of the layouts always has to be the default one"): no None for new displays. The
// switch is a button, "Set as default" (DefaultButton), greyed on the default: the default moves, never goes.

namespace Karpo.Core;

/// <summary>
/// The bar a menu is about: the display it stands on (its id, and its name, for a display the file
/// has no entry for yet).
/// </summary>
public sealed record BarSpot(string Display, string Name)
{
    /// <summary>§309: the displays attached now with a bar, which whether this one may show None asks (<see cref="Config.CanHide"/>); none for this one alone.</summary>
    public IReadOnlyList<string> Attached { get; init; } = [];

    /// <summary>
    /// §579: every display connected now, Karpo shown on it or not, each its id and its name, in Windows' order (the primary
    /// first, then left to right: Monitors.List), which §585 the Layouts page lists in that order; none for this
    /// one alone.
    /// </summary>
    public IReadOnlyList<(string Id, string Name)> Connected { get; init; } = [];
}

/// <summary>
/// §330, §517, §520: which of a bar's looks a page of the settings panel has: §585 Layouts (every display's layout and
/// position, the layouts managed, §595 one of them the default), Taskbar (the shown layout's bar and its behaviour), §567 Appearance
/// (Karpo's one look); or (§334) the right click on an arranged folder's room. (§411 to §519 the bar's own right click in
/// edit mode had the taskbar's and the ways to the rest, LooksScope.All: it opens the panel's Taskbar page now. §521 to §584
/// the displays were a page of their own, Display.)
/// </summary>
public enum LooksScope
{
    /// <summary>§521, §585: the Layouts page: every display as an expander, the layouts (§595 the default badged), "Add a layout".</summary>
    Layouts,

    /// <summary>§628: "Add a layout"'s page, opened from the Layouts page: a tile for each layout preset, Blank first, its bar pictured (<see cref="BarLooks.AddLayoutRows"/>).</summary>
    AddLayout,
    Taskbar,
    Appearance,
    Folders,
}

/// <summary>A bar's looks as rows, and the one place each is written; see the top of the file.</summary>
public static class BarLooks
{
    // The rows' keys.
    public const string PositionRow = "BarPosition";
    public const string TypeRow = "BarType";
    public const string SizeRow = "BarSize";
    public const string ShowRow = "BarShow";
    public const string SpacingRow = "BarSpacing";
    public const string HoverRow = "OpenOnHover";
    public const string CombineRow = "Combine";
    public const string PreviewsRow = "Previews";
    public const string DesktopsRow = "AllDesktops";

    // §309: the layouts' rows' keys. §333: a layout's own rows' keys are
    // these and its id (KeyOf). §336: the Layouts row is no choice of the layout shown; §361: a layout's
    // "Show on this display" (HereRow) and None (NoneRow) choose it, in Display ▾ with the layouts; §366:
    // None a row among the layouts, picked by a click.
    public const string LayoutsRow = "Layouts";

    /// <summary>§521: which layout a display shows; §579 keyed with its display's id (<see cref="KeyOf(string, string)"/>).</summary>
    public const string ShownRow = "LayoutShown";
    public const string AddRow = "LayoutAdd";
    public const string DuplicateRow = "LayoutDuplicate";
    public const string NameRow = "LayoutName";
    public const string IconRow = "LayoutIcon";
    public const string DeleteRow = "LayoutDelete";
    public const string MoveRow = "LayoutMove";

    /// <summary>
    /// §579: the layout displays Karpo has not seen yet show, the Layouts page's Default layout; §595 a row in each layout's
    /// expander, keyed with its id (<see cref="KeyOf(string, string)"/>), §636 its Set as default button.
    /// </summary>
    public const string DefaultRow = "DefaultLayout";

    /// <summary>§517: the folder room's way to the Appearance page, ruled off from its own by the rule keyed its key and "-".</summary>
    public const string AppearanceLinkRow = OptionLinks.Key + nameof(OptionLink.Appearance);

    // §517: the menus' headings, each over its section.
    public const string BarHeading = "Heading.Bar";

    public const string BehaviourHeading = "Heading.Behaviour";

    /// <summary>§585: the Layouts page's two sections' headings, Displays and Layouts.</summary>
    public const string DisplaysHeading = "Heading.Displays";
    public const string LayoutsHeading = "Heading.Layouts";

    /// <summary>§585: the displays' list, each display an expander of its own (<see cref="DisplayThing"/>); its rows keyed with its id.</summary>
    public const string DisplaysRow = "Displays";

    /// <summary>§333: the key of <paramref name="row"/> (<see cref="NameRow"/>, <see cref="IconRow"/>…) of the layout <paramref name="layout"/>.</summary>
    public static string KeyOf(string row, TaskbarLayout layout) => KeyOf(row, layout.Id);

    /// <summary>A row's key for the layout or the style <paramref name="id"/>: "LayoutName:work".</summary>
    public static string KeyOf(string row, string id) => row + ":" + id;

    /// <summary>The longest a layout's name may be, as the Layouts page's box takes it.</summary>
    public const int NameLongest = 60;

    /// <summary>The edges a display's bar is offered, in the order the pictures and the menu list them.</summary>
    public static readonly Edge[] Edges = [Edge.Bottom, Edge.Top, Edge.Left, Edge.Right];

    /// <summary>The usual sizes of a bar, in DIPs: the thinnest that still reads, Windows' 48, and up (the settings app's choices; a size typed in the menu is offered them, §333).</summary>
    public static readonly int[] Sizes = [32, 40, 48, 56, 64, 80];

    /// <summary>§333: how far the size steps in the menu, in DIPs: every usual size a step from the thinnest a bar may be.</summary>
    public const int SizeStep = 4;

    /// <summary>§333: the most figures a size typed may have: the thickest bar's three.</summary>
    public const int SizeLongest = 3;

    /// <summary>The ways a bar shows, in the order they are offered.</summary>
    public static readonly EdgeMode[] Shows = [EdgeMode.Reserve, EdgeMode.Float, EdgeMode.Hover];

    /// <summary>The settings app's sizes for a bar <paramref name="own"/> DIPs thick: the usual ones, and its own in its place where it is another (a drag on its edge).</summary>
    public static int[] SizesWith(int own) => Sizes.Contains(own) ? Sizes : [.. Sizes.Append(own).Order()];

    /// <summary>A size in words: "48 px".</summary>
    public static string SizeName(int size) => S.SettingsLayoutsSizeValue(size);

    /// <summary>What a layout's type is called: "Taskbar", "Dock".</summary>
    public static string TypeName(LayoutType type) => type == LayoutType.Dock ? S.SettingsLayoutsTypeDock : S.SettingsLayoutsTypeTaskbar;

    /// <summary>What a way of showing is called: "Always, reserve space" and the other two.</summary>
    public static string ShowName(EdgeMode show) => show switch
    {
        EdgeMode.Float => S.SettingsLayoutsShowFloat,
        EdgeMode.Hover => S.SettingsLayoutsShowHover,
        _ => S.SettingsLayoutsShowReserve,
    };

    /// <summary>§411 (R14): what the row says of a way of showing, short: "Reserve space", "Over windows", "Auto-hide".</summary>
    public static string ShowSaid(EdgeMode show) => show switch
    {
        EdgeMode.Float => S.SettingsLayoutsShowFloatSaid,
        EdgeMode.Hover => S.SettingsLayoutsShowHoverSaid,
        _ => S.SettingsLayoutsShowReserveSaid,
    };

    /// <summary>What an edge is called: "Bottom".</summary>
    public static string EdgeName(Edge edge) => edge switch
    {
        Edge.Top => S.CoreEdgeTop,
        Edge.Left => S.CoreEdgeLeft,
        Edge.Right => S.CoreEdgeRight,
        _ => S.CoreEdgeBottom,
    };

    /// <summary>What <paramref name="bar"/> is: its layout's type, "Taskbar", "Dock".</summary>
    public static string Kind(Config config, BarSpot bar) => TypeName(config.ActiveLayout(bar.Display).Type);

    /// <summary>
    /// §330: what a menu of <paramref name="scope"/> is named over it: §364 a layout's with the layout shown —
    /// "Folders · Work" (every folder of the layout's, §346). §423 (the person, 2026-10-30: "there should not be a header
    /// in the menu that is opened by it anymore"): nothing over the settings panel's pages, whose titles say what they are
    /// (§520: Display, Taskbar, Appearance).
    /// </summary>
    public static string? Kind(Config config, BarSpot bar, LooksScope scope) =>
        scope == LooksScope.Folders ? S.DockEditOfLayout(S.CoreCategoryFolders, config.ActiveLayout(bar.Display).Name) : null;

    /// <summary>
    /// The looks of <paramref name="bar"/> a menu of <paramref name="scope"/> shows (§517), read from and written to
    /// <paramref name="config"/>, what is Windows' own drawn in the mode it is in (<paramref name="dark"/>): §585 the Layouts
    /// page's (<see cref="LayoutsRows"/>, what is new refused as <paramref name="license"/> refuses it — none:
    /// refused nothing), Taskbar ▾'s (<see cref="TaskbarRows"/>), §567 the Appearance page's (<see cref="LookRows.Rows"/>, Karpo's one look, its
    /// previews on the bar's layout's kind of bar); an arranged folder's room its Open folders and the way to the Appearance.
    /// Each setting on one page of the settings panel alone (§520).
    /// </summary>
    public static IReadOnlyList<OptionRow> Rows(Config config, BarSpot bar, bool dark, LooksScope scope, LicenseStatus? license = null, LayoutMachine? machine = null) => scope switch
    {
        LooksScope.Folders =>
        [
            .. FolderRows(config.ActiveLayout(bar.Display)),
            new(AppearanceLinkRow + "-", OptionRowKind.Heading, ""),
            OptionLinks.Row(OptionLink.Appearance, OptionSlot.Unset),
        ],
        LooksScope.Layouts => LayoutsRows(config, bar, license),
        LooksScope.AddLayout => AddLayoutRows(config, bar, machine ?? LayoutMachine.Read(), license),
        LooksScope.Appearance => LookRows.Rows(config, config.ActiveLayout(bar.Display), dark),
        _ => TaskbarRows(config, bar),
    };

    /// <summary>
    /// §517: Taskbar ▾, the bar of the layout <paramref name="bar"/>'s display shows: under "Bar" its type, its size and how
    /// it shows (<see cref="BarRows"/>), §534 and "Lock the taskbar", every bar's (<see cref="BarMoves.LockRow"/>); under
    /// "Behavior" combine, previews, desktops, flyouts and folders on hover (<see cref="Behaviour"/>). Where it stands is the
    /// display's (Display ▾, §348).
    /// </summary>
    public static IReadOnlyList<OptionRow> TaskbarRows(Config config, BarSpot bar)
    {
        var layout = config.ActiveLayout(bar.Display);
        return
        [
            new(BarHeading, OptionRowKind.Heading, S.SettingsSectionsBar),
            new(TypeRow, OptionRowKind.Choice, S.SettingsLayoutsTypeHeader)
            {
                Glyph = OptionGlyphs.Taskbar,
                Choices = [.. LayoutTypes.All.Select(TypeName)],
                Chosen = Array.IndexOf(LayoutTypes.All, layout.Type),
                Set = at => at >= 0 && at < LayoutTypes.All.Length && layout.Become(LayoutTypes.All[at]),
            },
            .. BarRows(config, layout),
            // §534: whether the bars may be dragged to another edge, or their height dragged, at rest: every bar's.
            BarMoves.LockRow(config),
            new(BehaviourHeading, OptionRowKind.Heading, S.DockTaskbarBehaviour),
            .. Behaviour(layout),
        ];
    }

    /// <summary>
    /// The size, the spacing (§533) and the way of showing of <paramref name="layout"/>'s bar. §333: the size a stepper in pixels over
    /// all a bar may be, a number typed too, offered the person's other bars' sizes, then the usual ones.
    /// </summary>
    private static IEnumerable<OptionRow> BarRows(Config config, TaskbarLayout layout)
    {
        var marks = NumberMarks.Region();
        var size = layout.Size;
        yield return new(SizeRow, OptionRowKind.Number, S.SettingsLayoutsSizeHeader)
        {
            Glyph = OptionGlyphs.Height,
            Value = size,
            Least = Config.MinHeight,
            Most = Config.MaxHeight,
            Step = SizeStep,
            Unit = OptionUnit.Pixels,
            Set = value => WriteSize(layout, value),
            Text = Currencies.Typed(size, marks),
            Placeholder = Currencies.Typed(Config.DefaultHeight, marks),
            Longest = SizeLongest,
            Finder = new AmountFinder(Currencies.Typed(size, marks), SizesOffered(config, layout, marks), marks),
            SetText = typed => WriteTyped(layout, typed, marks),
        };
        // §533, §589: the room between its widgets, a number of pixels (DIPs, as the size is) — the settings panel's number
        // box, − and +, a pixel a step.
        yield return new(SpacingRow, OptionRowKind.Number, S.SettingsLayoutsSpacingHeader)
        {
            Glyph = OptionGlyphs.Spacing,
            Value = Math.Clamp(layout.Spacing, Config.LeastSpacing, Config.MostSpacing),
            Least = Config.LeastSpacing,
            Most = Config.MostSpacing,
            Unit = OptionUnit.Pixels,
            Set = value => Changed(layout.Spacing, Math.Clamp(value, Config.LeastSpacing, Config.MostSpacing), spacing => layout.Spacing = spacing),
        };
        yield return new(ShowRow, OptionRowKind.Choice, S.SettingsLayoutsShowHeader)
        {
            Glyph = OptionGlyphs.Show,
            Choices = [.. Shows.Select(ShowName)],
            // §578: each its icon in the dropdown's list and in its box: space kept, over the windows, hidden till pointed at.
            ChoiceGlyphs = [OptionGlyphs.Pin, OptionGlyphs.Cascade, OptionGlyphs.Hide],
            // §411 (R14): short at the row's end.
            ChoicesSaid = [.. Shows.Select(ShowSaid)],
            Chosen = Array.IndexOf(Shows, layout.Show),
            Set = at => at >= 0 && at < Shows.Length && WriteShow(layout, Shows[at]),
        };
    }

    /// <summary>
    /// §334, §441, §514: the folder room's menu, a right click on an arranged folder's room: whether what the widgets open —
    /// a folder among it — opens on hover (<paramref name="layout"/>'s, Behavior's one row, <see cref="Hover"/>). How they
    /// look is the Appearance's, the way there under it.
    /// </summary>
    public static IReadOnlyList<OptionRow> FolderRows(TaskbarLayout layout) => [Hover(layout)];

    /// <summary>
    /// §337: what the bars of <paramref name="layout"/> do: the General page's taskbar behaviour, Taskbar ▾'s Behavior (§517;
    /// Q4 A, §400 to §513 Layout ▾'s Behavior page) — combine the app buttons (Windows' three, in its words), window previews,
    /// the desktops' windows, and §514 whether what the widgets open — a folder, a widget's flyout — opens on hover, one row
    /// (§334 to §515 two: the widgets' flyouts' and the folders') —, the layout's own (§345). Windows' taskbar is hidden
    /// whenever Karpo runs, with nothing to turn (§465).
    /// </summary>
    public static IReadOnlyList<OptionRow> Behaviour(TaskbarLayout layout) =>
        [
            new OptionRow(CombineRow, OptionRowKind.Choice, S.SettingsGeneralCombineHeader)
            {
                Glyph = OptionGlyphs.Combine,
                Slot = OptionSlot.Behaviour,
                Choices = [.. Combines.Select(CombineName)],
                // §578: always combined, combined when it is full, never: each its icon.
                ChoiceGlyphs = [OptionGlyphs.Combine, OptionGlyphs.Fit, OptionGlyphs.Apps],
                Chosen = Array.IndexOf(Combines, layout.Combine),
                Set = at => at >= 0 && at < Combines.Length && Changed(layout.Combine, Combines[at], value => layout.Combine = value),
            },
            // §411 (R3): on or off, a switch (§337 to §410 a choice of On hover and Off).
            new OptionRow(PreviewsRow, OptionRowKind.Toggle, S.SettingsGeneralPreviewsHeader)
            {
                // §517 (R13): a window's picture, apart from Show's eye on the same page.
                Glyph = OptionGlyphs.Picture,
                Slot = OptionSlot.Behaviour,
                On = layout.Previews,
                Set = value => Changed(layout.Previews, value != 0, on => layout.Previews = on),
            },
            // §362: Windows' "On the taskbar, show all the open windows" (Multitasking > Desktops), in words a row holds whole.
            new OptionRow(DesktopsRow, OptionRowKind.Choice, S.SettingsGeneralDesktopsHeader)
            {
                Glyph = OptionGlyphs.Desktops,
                Slot = OptionSlot.Behaviour,
                Choices = [S.SettingsGeneralDesktopsThis, S.SettingsGeneralDesktopsAll],
                Chosen = layout.AllDesktops ? 1 : 0,
                Set = at => at is 0 or 1 && Changed(layout.AllDesktops, at == 1, value => layout.AllDesktops = value),
            },
            Hover(layout),
        ];

    /// <summary>
    /// §514: whether what a widget on the bars of <paramref name="layout"/> opens — a folder, a widget's flyout — opens on hover,
    /// or on a click: "Open flyouts", Behavior's row and the folder room's.
    /// </summary>
    private static OptionRow Hover(TaskbarLayout layout) => new(HoverRow, OptionRowKind.Choice, S.SettingsGeneralHoverFlyoutsHeader)
    {
        Glyph = OptionGlyphs.Open,
        Slot = OptionSlot.Behaviour,
        Choices = [S.SettingsGeneralHoverFlyoutsOnClick, S.SettingsGeneralHoverFlyoutsOnHover],
        Chosen = layout.OpenOnHover ? 1 : 0,
        Set = at => at is 0 or 1 && Changed(layout.OpenOnHover, at == 1, on => layout.OpenOnHover = on),
    };

    /// <summary>§337: Windows' ways of combining the app buttons, in the order its dropdown lists them.</summary>
    public static readonly ButtonCombine[] Combines = [ButtonCombine.Always, ButtonCombine.WhenFull, ButtonCombine.Never];

    /// <summary>What a way of combining is called, in Windows' words: "Always", "When taskbar is full", "Never".</summary>
    public static string CombineName(ButtonCombine combine) => combine switch
    {
        ButtonCombine.WhenFull => S.SettingsGeneralCombineWhenFull,
        ButtonCombine.Never => S.SettingsGeneralCombineNever,
        _ => S.SettingsGeneralCombineAlways,
    };

    /// <summary>Sets <paramref name="now"/> to <paramref name="next"/> through <paramref name="set"/>; false where it is already.</summary>
    private static bool Changed<T>(T now, T next, Action<T> set)
    {
        if (EqualityComparer<T>.Default.Equals(now, next))
        {
            return false;
        }
        set(next);
        return true;
    }

    /// <summary>
    /// §521 (the person, 2026-11-07: "layouts should get their own separate entry in the settings."): the layouts as the
    /// person's own things (<see cref="OptionOwned"/>), the page's own rows (<see cref="OptionRow.Inline"/>): each in the
    /// person's order, the one this display shows saying so under its name, its own rows (<see cref="Thing"/>) — its name, its
    /// icon, a copy — opened under it, its Delete at its end (asked first, greyed on the last), dragged into order where a menu
    /// shows it; then "Add a layout", §628 a link to its own page of the layout presets (<see cref="AddLayoutRows"/>; §472 to
    /// §627 a choice of them), greyed and said why past what <paramref name="license"/> holds. §585 (the person, 2026-11-09: "merge
    /// display settings together with layout settings"): the Layouts page — under Displays every display
    /// connected (<see cref="DisplaysOf"/>), each an expander (<see cref="DisplayThing"/>); under Layouts the layouts, §595 the
    /// default badged, each with its switch (§579 to §594 a Default layout row first).
    /// </summary>
    public static IReadOnlyList<OptionRow> LayoutsRows(Config config, BarSpot bar, LicenseStatus? license = null)
    {
        var layouts = config.Layouts;
        var refusal = license is { } status ? LicenseGate.Layouts(status, layouts.Count) : null;
        var shown = config.ScreenFor(bar.Display).Enabled;
        var active = config.ActiveLayout(bar.Display);
        // §628 (the person, 2026-11-09: "when adding a layout it should not open a dropdown but should open a new page"): a way
        // to add one that leads to a page of its own (AddLayoutRows), as a link leads to the page that owns a setting.
        var add = new OptionRow(AddRow, OptionRowKind.Link, S.SettingsLayoutsAdd)
        {
            Glyph = OptionGlyphs.New,
            Enabled = refusal is null,
            Why = refusal,
            Adds = true,
            Link = OptionLink.AddLayout,
        };
        var marked = config.DefaultLayout.Id;
        return
        [
            new OptionRow(DisplaysHeading, OptionRowKind.Heading, S.SettingsSectionsDisplays),
            new OptionRow(DisplaysRow, OptionRowKind.Things, S.SettingsSectionsDisplays)
            {
                Glyph = OptionGlyphs.Monitor,
                Inline = true,
                Owned = new OptionOwned(null, [.. DisplaysOf(bar).Select(display => DisplayThing(config, display, here: IsBar(display, bar)))]),
            },
            new OptionRow(LayoutsHeading, OptionRowKind.Heading, S.CoreBarMenuLayouts),
            new OptionRow(LayoutsRow, OptionRowKind.Things, S.CoreBarMenuLayouts)
            {
                Glyph = TaskbarLayouts.LayoutGlyph,
                Inline = true,
                Owned = new OptionOwned(add, [.. layouts.Select(layout => Thing(config, bar, layout, refusal, shown && layout.Id == active.Id, layout.Id == marked))]),
            },
        ];
    }

    /// <summary>The start of a layout preset's tile's key on the Add a layout page: "LayoutPreset:planner" (<see cref="KeyOf(string, string)"/>).</summary>
    public const string PresetRow = "LayoutPreset";

    /// <summary>
    /// §628 (the person, 2026-11-09: "when adding a layout it should not open a dropdown but should open a new page where you
    /// also have a preview of the different layout presets. there should also be one that starts just blank"): the Add a
    /// layout page — each layout preset (<see cref="LayoutPresets.All"/>, Blank first) a row that acts, its name, its icon,
    /// what it is for under it, and the layout it makes on <paramref name="machine"/> as the look it shows
    /// (<see cref="LookShown.Itself"/>: the settings panel draws that very layout's bar, a tile picturing it). A pick makes that
    /// layout of the same <paramref name="machine"/> (<see cref="AddShown"/>: what is pictured is what is made) and shows it on
    /// <paramref name="bar"/>'s display; every tile greyed with why past what <paramref name="license"/> holds.
    /// </summary>
    public static IReadOnlyList<OptionRow> AddLayoutRows(Config config, BarSpot bar, LayoutMachine machine, LicenseStatus? license = null)
    {
        var refusal = license is { } status ? LicenseGate.Layouts(status, config.Layouts.Count) : null;
        return [.. LayoutPresets.All.Select(preset => new OptionRow(KeyOf(PresetRow, preset.Id), OptionRowKind.Action, preset.Title)
        {
            Glyph = preset.Glyph,
            Detail = preset.Blurb,
            Enabled = refusal is null,
            Why = refusal,
            Shows = new LookShown(config, LayoutPresets.Make(preset, machine, PresetName(config, preset))) { Itself = true },
            Set = _ => refusal is null && AddShown(config, bar, preset, license, machine),
        })];
    }

    /// <summary>
    /// §585 (the person, 2026-11-09: "the displays should get the expansion section for their settings too"): <paramref name="display"/>
    /// as a thing of the Layouts page, an expander: its name, "This display" under it where it is the one the
    /// flyout is shown on (<paramref name="here"/>, §579's mark of the layout it shows too); in it the layout it shows or None
    /// (<see cref="Shown"/>) and where its bar stands (<see cref="Position"/>, whichever layout it shows). Not deleted, not
    /// moved: the displays are Windows'.
    /// </summary>
    private static OptionThing DisplayThing(Config config, BarSpot display, bool here) => new(display.Name, OptionGlyphs.Monitor)
    {
        Summary = here ? S.SettingsSectionsThisDisplay : null,
        Rows = [Shown(config, display), Position(config, display)],
    };

    /// <summary>Whether <paramref name="display"/> is <paramref name="bar"/>'s display, the one the page is shown on.</summary>
    private static bool IsBar(BarSpot display, BarSpot bar) => string.Equals(display.Display, bar.Display, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// §579, §585 (the person, 2026-11-09: "the displays should actually be ordered the same way everywhere"): the displays the
    /// page lists, in Windows' one order (<see cref="BarSpot.Connected"/>: the primary first, then left to right), each once,
    /// each as a bar of its own asking the same displays whether it may show None; <paramref name="bar"/> itself (the one the
    /// page is shown on) where it stands among them, never moved — first where Windows has not listed it (a page of one
    /// display). §579 put it first.
    /// </summary>
    public static IReadOnlyList<BarSpot> DisplaysOf(BarSpot bar)
    {
        var connected = bar.Connected.DistinctBy(other => other.Id, StringComparer.OrdinalIgnoreCase).ToList();
        IEnumerable<BarSpot> all = connected.Select(other => string.Equals(other.Id, bar.Display, StringComparison.OrdinalIgnoreCase)
            ? bar
            : new BarSpot(other.Id, other.Name) { Attached = bar.Attached, Connected = bar.Connected });
        return connected.Exists(other => string.Equals(other.Id, bar.Display, StringComparison.OrdinalIgnoreCase)) ? [.. all] : [bar, .. all];
    }

    /// <summary>
    /// §521: which layout <paramref name="bar"/>'s display shows, a dropdown of the layouts and None (where Karpo may be hidden
    /// here, or is: <see cref="DisplayLayoutChoice"/>), as the settings app's Displays page had it; §585 in its display's expander.
    /// </summary>
    public static OptionRow Shown(Config config, BarSpot bar)
    {
        var layouts = config.Layouts;
        var shown = config.ScreenFor(bar.Display).Enabled;
        var active = config.ActiveLayout(bar.Display);
        var canHide = config.CanHide(bar.Display, bar.Attached);
        var choices = DisplayLayoutChoice.Choices(layouts.Select(each => each.Name), shown, canHide);
        return new OptionRow(KeyOf(ShownRow, bar.Display), OptionRowKind.Choice, S.DockEditLayout)
        {
            Glyph = shown ? GlyphOf(active) : OptionGlyphs.Hide,
            Grows = true,
            Choices = choices,
            ChoiceGlyphs = [.. layouts.Select(GlyphOf), .. choices.Count > layouts.Count ? [OptionGlyphs.Hide] : Array.Empty<string>()],
            Chosen = DisplayLayoutChoice.IndexOf(layouts.IndexOf(active), layouts.Count, shown),
            Set = at => WriteShown(config, bar.Display, bar.Name, at, canHide) is DisplayPick.Show or DisplayPick.Hide,
        };
    }

    /// <summary>§348: where <paramref name="bar"/>'s display's bar stands, whichever layout it shows; §585 in its display's expander.</summary>
    public static OptionRow Position(Config config, BarSpot bar) => new(KeyOf(PositionRow, bar.Display), OptionRowKind.Choice, S.SettingsDisplaysEdgeHeader)
    {
        Glyph = OptionGlyphs.Monitor,
        // §411 (R3): a place of four, its icons in its row.
        Segmented = true,
        Choices = [.. Edges.Select(EdgeName)],
        ChoiceGlyphs = [OptionGlyphs.ArrowDown, OptionGlyphs.ArrowUp, OptionGlyphs.ArrowLeft, OptionGlyphs.ArrowRight],
        Chosen = Array.IndexOf(Edges, config.EdgeOfScreen(bar.Display)),
        Set = at => at >= 0 && at < Edges.Length && WriteEdge(config, bar, Edges[at]),
    };

    /// <summary>
    /// §333, §521: <paramref name="layout"/> as a thing of the Layouts page: its name and icon, "This display" under its name
    /// where it is <paramref name="here"/> (the one this display shows), §595 the badge "Default" after its name where it is the
    /// default layout (<paramref name="byDefault"/>: new displays show it); its own rows — its name, its icon, §636 making it
    /// the default (<see cref="DefaultButton"/>) —; §551 its
    /// Duplicate, a copy of it shown here at once (refused as <paramref name="refusal"/> says, past the licence's layouts); its Delete (asked first, greyed
    /// on the last layout); its move among the layouts (Config.Layouts' order).
    /// </summary>
    private static OptionThing Thing(Config config, BarSpot bar, TaskbarLayout layout, string? refusal, bool here, bool byDefault)
    {
        var last = config.Layouts.Count <= 1;
        return new OptionThing(layout.Name, GlyphOf(layout))
        {
            Summary = here ? S.SettingsSectionsThisDisplay : null,
            Badge = byDefault ? S.SettingsLayoutsDefaultBadge : null,
            Rows =
            [
                new(KeyOf(NameRow, layout), OptionRowKind.Text, S.SettingsLayoutsNameHeader)
                {
                    Glyph = OptionGlyphs.Name,
                    Field = OptionField.Text,
                    Text = layout.Name,
                    Placeholder = S.SettingsLayoutsNameBoxName,
                    Longest = NameLongest,
                    SetText = typed => WriteName(layout, typed),
                },
                // Its own icon, or the layouts' (Default), from the gallery a folder's and a pin's come from.
                OptionPickers.Icons(KeyOf(IconRow, layout), GlyphOf(layout) == TaskbarLayouts.LayoutGlyph ? "" : layout.Icon, glyph => WriteIcon(layout, glyph), TaskbarLayouts.LayoutGlyph) with { Title = S.SettingsLayoutsIconHeader },
                DefaultButton(config, layout, byDefault),
            ],
            // §551: a copy of it an action of the layout itself, a button at its row's end before its Delete (an icon button in
            // the settings panel, as Delete is), no row of its own.
            Acts =
            [
                new(KeyOf(DuplicateRow, layout), OptionRowKind.Action, S.SettingsLayoutsDuplicate)
                {
                    Glyph = OptionGlyphs.Copy,
                    Enabled = refusal is null,
                    Why = refusal,
                    Set = _ => refusal is null && Select(config, bar, Duplicate(config, layout)),
                },
            ],
            Delete = new(KeyOf(DeleteRow, layout), OptionRowKind.Action, S.SettingsLayoutsDelete)
            {
                Glyph = OptionGlyphs.Delete,
                Enabled = !last,
                Why = last ? S.SettingsLayoutsLastOne : null,
                Ask = new DeleteQuestion(S.DockDeleteFolderTitle(layout.Name), S.SettingsLayoutsDeleteConfirm, S.SettingsLayoutsDelete, S.DockDeleteCancel, 0),
                Set = _ => config.DeleteLayout(layout.Id),
            },
            Move = new(KeyOf(MoveRow, layout), OptionRowKind.Action, S.DockEditFaceDrag)
            {
                Move = to => MoveLayout(config, layout.Id, to),
            },
        };
    }

    /// <summary>
    /// §595 (the person, 2026-11-09: "when expanding a layout you should be able to make it the default one"), §636 ("one of
    /// the layouts always has to be the default one"): making <paramref name="layout"/> what displays Karpo has not seen yet show,
    /// "Shown on new displays" before a button, "Set as default", as Windows' Settings sets a printer or a sound device the
    /// default — one of several is always it, so there is nothing to turn off. Greyed on the default (<paramref name="isDefault"/>):
    /// the default moves to another layout by that one's button, never to none. §595 to §635 a switch, off on the default
    /// leaving None; §579 to §594 a dropdown of the layouts and None at the Layouts section's top.
    /// </summary>
    private static OptionRow DefaultButton(Config config, TaskbarLayout layout, bool isDefault) => new(KeyOf(DefaultRow, layout), OptionRowKind.Action, S.SettingsLayoutsDefaultSet)
    {
        Glyph = OptionGlyphs.Monitors,
        Detail = S.SettingsLayoutsDefaultDescription,
        Enabled = !isDefault,
        Set = _ => WriteDefault(config, layout.Id),
    };

    /// <summary>
    /// §333: the layout <paramref name="id"/> at place <paramref name="to"/> among <paramref name="config"/>'s
    /// layouts (held to them), the order the menus and the pages list them in; false where it stood there,
    /// or for no such layout.
    /// </summary>
    public static bool MoveLayout(Config config, string id, int to)
    {
        var layouts = config.Layouts;
        var from = layouts.FindIndex(layout => layout.Id == id);
        to = Math.Clamp(to, 0, layouts.Count - 1);
        if (from < 0 || from == to)
        {
            return false;
        }
        var layout = layouts[from];
        layouts.RemoveAt(from);
        layouts.Insert(to, layout);
        return true;
    }

    /// <summary>§309: a layout's icon: its own, the layouts' for none.</summary>
    public static string GlyphOf(TaskbarLayout layout) => layout.Icon is { Length: > 0 } icon ? icon : TaskbarLayouts.LayoutGlyph;

    /// <summary>
    /// §309: what a new layout is called where nothing is typed: "New layout", or "New layout 2" and on
    /// where that is taken, in the language the menus speak, as Explorer names a new folder.
    /// </summary>
    public static string NewName(Config config)
    {
        var names = config.Layouts.Select(layout => layout.Name).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        var name = S.SettingsLayoutsNewName;
        for (var number = 2; names.Contains(name); number++)
        {
            name = S.SettingsLayoutsNewNameNumbered(number);
        }
        return name;
    }

    /// <summary>§309: what a copy of <paramref name="layout"/> is called where nothing is typed: "Home copy".</summary>
    public static string CopyName(TaskbarLayout layout) => S.SettingsLayoutsCopyName(layout.Name);

    /// <summary>
    /// §309: a new layout at the end of <paramref name="config"/>'s, a Taskbar holding Windows 11's
    /// taskbar as a new install's, called <paramref name="name"/> (trimmed, held to
    /// <see cref="NameLongest"/>), or <see cref="NewName"/> for nothing; shown by no display yet.
    /// §472: Karpo's layout preset (<see cref="LayoutPresets.Karpo"/>).
    /// </summary>
    public static TaskbarLayout Add(Config config, string? name = null) => Add(config, LayoutPresets.Karpo, NameOr(name, NewName(config)));

    /// <summary>
    /// §472: a new layout of <paramref name="preset"/> at the end of <paramref name="config"/>'s (<see cref="LayoutPresets.Make"/>),
    /// made for <paramref name="machine"/> (this PC for none), called <paramref name="name"/> or the preset's name ("Windows",
    /// "Windows 2" where that is taken, <see cref="PresetName"/>); shown by no display yet. Nothing in it names the preset.
    /// </summary>
    public static TaskbarLayout Add(Config config, LayoutPreset preset, string? name = null, LayoutMachine? machine = null)
    {
        var layout = LayoutPresets.Make(preset, machine ?? LayoutMachine.Read(), NameOr(name, PresetName(config, preset)));
        config.Layouts.Add(layout);
        return layout;
    }

    /// <summary>
    /// §309, §404: "Add a layout" — a new layout shown on <paramref name="bar"/>'s display: §472 of the
    /// <paramref name="preset"/> picked, under its name (<see cref="Add(Config, LayoutPreset, string?, LayoutMachine?)"/>). The
    /// one picker of the presets is §628 the Add a layout page (<see cref="AddLayoutRows"/>); §485 the layouts line's "Add" opens
    /// it. False, and nothing made, past what <paramref name="license"/> holds.
    /// </summary>
    public static bool AddShown(Config config, BarSpot bar, LayoutPreset preset, LicenseStatus? license = null, LayoutMachine? machine = null) =>
        (license is not { } status || LicenseGate.Layouts(status, config.Layouts.Count) is null)
        && Select(config, bar, Add(config, preset, machine: machine));

    /// <summary>
    /// §472: what a new layout of <paramref name="preset"/> is called: its name ("Windows"), or "Windows 2" and on where that is
    /// taken, as <see cref="NewName"/> numbers "New layout".
    /// </summary>
    public static string PresetName(Config config, LayoutPreset preset)
    {
        var names = config.Layouts.Select(layout => layout.Name).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        var name = preset.Title;
        for (var number = 2; names.Contains(name); number++)
        {
            name = S.SettingsLayoutsNumbered(preset.Title, number);
        }
        return name;
    }

    /// <summary>
    /// §309: a copy of <paramref name="source"/>, its type, bars and widgets, at the end of
    /// <paramref name="config"/>'s, called <paramref name="name"/> or <see cref="CopyName"/>; its widgets
    /// and its folders' widgets new ids, as a widget copied anywhere gets (<see cref="TaskbarLayout.CopyAs"/>).
    /// </summary>
    public static TaskbarLayout Duplicate(Config config, TaskbarLayout source, string? name = null)
    {
        var copy = source.CopyAs(NameOr(name, CopyName(source)));
        config.Layouts.Add(copy);
        return copy;
    }

    /// <summary>What was typed, trimmed and held to <see cref="NameLongest"/>, or <paramref name="otherwise"/> where nothing was.</summary>
    private static string NameOr(string? typed, string otherwise) =>
        typed?.Trim() is { Length: > 0 } name ? name[..Math.Min(name.Length, NameLongest)] : otherwise;

    /// <summary>§309: <paramref name="bar"/>'s display shows <paramref name="layout"/>, just made; true.</summary>
    private static bool Select(Config config, BarSpot bar, TaskbarLayout layout)
    {
        config.SelectLayout(bar.Display, layout.Id, bar.Name);
        return true;
    }

    /// <summary>§309: <paramref name="layout"/> called <paramref name="name"/>, trimmed and held to <see cref="NameLongest"/>; false for nothing typed or the name it has.</summary>
    public static bool WriteName(TaskbarLayout layout, string name)
    {
        name = NameOr(name, "");
        if (name.Length == 0 || layout.Name == name)
        {
            return false;
        }
        layout.Name = name;
        return true;
    }

    /// <summary>§309: <paramref name="layout"/> drawn with <paramref name="glyph"/>, the layouts' own for none; false where it was.</summary>
    public static bool WriteIcon(TaskbarLayout layout, string glyph)
    {
        glyph = glyph.Length > 0 ? glyph : TaskbarLayouts.LayoutGlyph;
        if (layout.Icon == glyph)
        {
            return false;
        }
        layout.Icon = glyph;
        return true;
    }

    /// <summary>
    /// §309: the display <paramref name="display"/> (called <paramref name="name"/>) shows the entry
    /// <paramref name="at"/> of its list (<see cref="DisplayLayoutChoice"/>): a layout, and Karpo there
    /// where it was hidden, or None, Karpo hidden there — refused where it may not be
    /// (<paramref name="canHide"/>: the last display that shows it). What it did.
    /// </summary>
    public static DisplayPick WriteShown(Config config, string display, string name, int at, bool canHide)
    {
        var layouts = config.Layouts;
        var shown = config.ScreenFor(display).Enabled;
        var now = DisplayLayoutChoice.IndexOf(layouts.IndexOf(config.ActiveLayout(display)), layouts.Count, shown);
        var pick = DisplayLayoutChoice.Choose(at, now, layouts.Count, canHide);
        if (pick == DisplayPick.Hide)
        {
            config.EditScreen(display, name).Enabled = false;
        }
        else if (pick == DisplayPick.Show)
        {
            var screen = config.EditScreen(display, name);
            (screen.Enabled, screen.ActiveLayoutId) = (true, layouts[at].Id);
        }
        return pick;
    }

    /// <summary>
    /// §309: displays Karpo has not seen yet show the layout <paramref name="id"/> (§636: always a layout, never none). False
    /// where they did, or for no such layout.
    /// </summary>
    public static bool WriteDefault(Config config, string id)
    {
        if (config.Layout(id) is null || config.DefaultLayout.Id == id)
        {
            return false;
        }
        config.DefaultLayoutId = id;
        return true;
    }

    /// <summary><paramref name="bar"/>'s display's bar stands on <paramref name="edge"/>, whichever layout it shows; false where it did.</summary>
    public static bool WriteEdge(Config config, BarSpot bar, Edge edge)
    {
        if (!Edges.Contains(edge) || config.EdgeOfScreen(bar.Display) == edge)
        {
            return false;
        }
        config.EditScreen(bar.Display, bar.Name).Edge = edge;
        return true;
    }

    /// <summary><paramref name="layout"/>'s bar <paramref name="size"/> DIPs thick, held to the sizes a bar may be; false where it was.</summary>
    public static bool WriteSize(TaskbarLayout layout, int size)
    {
        size = Math.Clamp(size, Config.MinHeight, Config.MaxHeight);
        if (layout.Size == size)
        {
            return false;
        }
        layout.Size = size;
        return true;
    }

    /// <summary>
    /// §333: the sizes a size typed for <paramref name="layout"/>'s bar is offered, written in <paramref name="marks"/>:
    /// the person's other layouts' bars', each with its layout's name, then the usual ones (<see cref="Sizes"/>); each
    /// held to what a bar may be.
    /// </summary>
    public static IReadOnlyList<OptionFound> SizesOffered(Config config, TaskbarLayout layout, NumberMarks marks)
    {
        var others = config.Layouts.Where(each => each != layout).Select(each => AmountFinder.Row(each.BarSize, marks, each.Name));
        return [.. others, .. Sizes.Select(size => AmountFinder.Row(size, marks))];
    }

    /// <summary>
    /// §333: a size typed for <paramref name="layout"/>'s bar, read in <paramref name="marks"/>, rounded and held to
    /// what a bar may be; nothing typed its default, Windows' 48. False where it is no number, or the size it was.
    /// </summary>
    public static bool WriteTyped(TaskbarLayout layout, string typed, NumberMarks marks)
    {
        if (string.IsNullOrWhiteSpace(typed))
        {
            return WriteSize(layout, Config.DefaultHeight);
        }
        return Currencies.Parse(typed, marks) is { } size && double.IsFinite(size)
            && WriteSize(layout, (int)Math.Round(Math.Clamp(size, Config.MinHeight, Config.MaxHeight)));
    }

    /// <summary><paramref name="layout"/>'s bar shows <paramref name="show"/>; false where it did.</summary>
    public static bool WriteShow(TaskbarLayout layout, EdgeMode show)
    {
        if (!Shows.Contains(show) || layout.Show == show)
        {
            return false;
        }
        layout.Show = show;
        return true;
    }
}
