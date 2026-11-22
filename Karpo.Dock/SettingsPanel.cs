// edit, §520: the settings' model (docs\widgets.md §520; §575, the person, 2026-11-08: "all of the menu entries we have on the
// sidemenu get promoted to menu entries on the topbar"). The edit bar holds an entry for each page — Karpo, §535 Accounts,
// §585 (§624) Layouts (§521 to §584 Display and Layouts, two), Taskbar (§585 the taskbar menu an expander of its
// Behavior), §567 Appearance, Folder and Widget while there is one, §575 Add —, each opening a flyout of its own,
// and each page the very rows the edit bar's menus showed until §520
// (Dock.SettingsItems, Dock.LooksItems, OptionMenu.Settings), read here as controls (PanelRows.Of; §521 pass two, the
// person, 2026-11-07: "check all the settings and make sure that they are implemented with the proper controlls"):
//
//   a toggle's row                  a toggle switch, "On"/"Off" before it
//   a stepper's row                 a number box: its value typed in it (§521), − and + inside it
//   a few choices in the row        a segmented control; a fixed choice of 2 or 3 short ones too (§521: whatever is set); §529
//                                   four or more a dropdown (a folder's alignment, a display's position)
//   a choice of one (PickOne)       a dropdown, the choices in a popup under it; a search's
//                                   (a time zone's, a currency's) its field over its finds in the popup, a long list's filter
//   a level holding a line of text an inline text box (§521): typed in place, kept on Enter or as it loses the keys,
//                                   Escape putting back what it said
//   a level of rows                 an inline expander, its rows inset in its card
//   one of the person's things      its buttons at its row's end (an account's Sign in again, Remove), §551 icon buttons;
//                                   §521 with rows of its own (a layout's) an expander, its buttons at its head's end; §535 a
//                                   switch of its own (an account's calendar) at its end, its buttons before it; §554 with
//                                   one choice of its own (a holiday calendar's region) that dropdown in its row, 8 before
//                                   its buttons, §609 (PanelRow.Pair; under its name where its name would not stand whole); §551
//                                   one put in order (a layout, an entry of the taskbar menu, a face, a toggle) a handle at
//                                   its start, dragged among its own, or moved by Ctrl+Up and Ctrl+Down
//   a colour                        §535: a colour button showing the colour in force, what it is set to said before it;
//                                   a thing's colour (an account's calendar's, a holiday calendar's) one too, no words
//   a value's reset                 §613: an icon button 8 before its control, while the value is not its palette's
//                                   (an Appearance row's; PanelRow.Pair, Subtle)
//   a choice beside a choice        §638: a font's weight's dropdown (140 DIP) at the line's end, its family's of the one width
//                                   8 before it, the font's reset before both (PanelRow.Pair, its pair's Pair)
//   a link                          where it leads at its end ("General › Public holidays"); §529 one with words (a calendar's
//                                   accounts) a line of them, its link at its end
//   §529: a row that only matters after a choice nested under it (FlyoutItem.Under); a mixed one of several widgets each
//   one's own under it; a widget's and a folder's page in groups (SettingsPanel.Widget.cs). §574: no row says where its
//   value comes from (§541's set here or inherited, its reset and its level's counts gone): every value is drawn alike
//   a row that acts                 §521: a button at the row's end, its words the row's, what it does said before it;
//                                   §610 one standing beside a dropdown (the palette's reset) its button before the dropdown,
//                                   in its line (PanelRow.Pair)
//   levels shown as tabs            §592: the Appearance's parts, a tab strip (WinUI's SelectorBar) over the chosen one's
//                                   rows, the look they edit previewed over the strip (PanelTabs)
//
// One source of settings: nothing here lists a setting; a page is what its menu's builder says. What the panel shows
// and where is PanelLayout's (SettingsPanel.Layout.cs), what it draws PanelPainter's, its window the edit bar's
// (EditDeck, Dock.EditDeck.cs), what a control does the dock's (Dock.SettingsPanel.cs).

using System.Globalization;
using Karpo.Core;

namespace Karpo.Dock;

/// <summary>§520: a page of the settings, §575 each an entry of the edit bar opening a flyout of its own, in the bar's order.</summary>
public enum PanelPage
{
    /// <summary>Karpo's own, app-wide (KarpoMenu.Rows): General, Services, Karpo.</summary>
    Karpo,

    /// <summary>§535: the accounts (KarpoMenu.Accounts): each one, its parts, its switches and colours; the ways to add one.</summary>
    Accounts,

    /// <summary>
    /// §521, §585 (§624) Layouts (LooksScope.Layouts): every display, an expander of the layout it shows and where its bar
    /// stands (§348), the flyout's own marked; the Default layout; the layouts, each one's own rows, "Add a layout". On every
    /// display, None or not.
    /// </summary>
    Layouts,

    /// <summary>The shown layout's bar (LooksScope.Taskbar); not on a display that shows None.</summary>
    Taskbar,

    /// <summary>§567: Karpo's one look (LooksScope.Appearance), on every display.</summary>
    Appearance,

    /// <summary>The folder open arranged on the display, or §529 selected alone: its faces, its panel's settings and its look on one page (PanelGroups.FolderPage).</summary>
    Folder,

    /// <summary>The widget selected alone: its settings, Cut, Copy and Remove at its title's end; §528 the several selected: the settings they have in common.</summary>
    Widget,

    /// <summary>
    /// §575: "+ Add"'s list, a page as every other (the person, 2026-11-08: "the add menu should also be adjusted so its style
    /// aligns with the style of the other settings pages"): the add tree's sections and folders, each widget a card a click
    /// adds, §578 tiles side by side, its own search at the flyout's top (AddList).
    /// </summary>
    Add,

    /// <summary>
    /// §628: "Add a layout"'s page (LooksScope.AddLayout), a page under the Layouts page's entry (<see cref="PanelRows.EntryOf"/>):
    /// its way back and its title at its flyout's top, then a tile for each layout preset picturing its bar, Blank first.
    /// </summary>
    AddLayout,

    /// <summary>
    /// §646: an account's "Mail folders" page (OptionAccountRows.Folders: its link's own level), a page under the Accounts page's
    /// entry: its way back and "Mail folders · the account" at its flyout's top, then its inbox and every folder, each a switch.
    /// </summary>
    MailFolders,

    /// <summary>
    /// §689 (the person, 2026-11-12: "the errors flyout should be styled the same as the other configuration pages"): the
    /// errors Karpo knows of (Dock.Faults.cs), a page hung from the edit bar's errors indicator — each a card, its icon, what
    /// failed, why under it, since when and a chevron at its end; a click opens what it is of.
    /// </summary>
    Errors,
}

/// <summary>§520: what a row of a page is drawn as (see the top of the file).</summary>
internal enum PanelRowKind
{
    Heading,
    Toggle,
    Choice,
    Color,
    Stepper,
    Expander,
    Item,
    Action,
    Note,
    Link,

    /// <summary>§521: a line of text typed in place, in its box (WinUI's TextBox).</summary>
    Text,

    /// <summary>
    /// §536: a look's preview, at the top of the level that edits it (<see cref="PanelRow.Shows"/>): a sample of what the look
    /// styles drawn by Karpo's own painters (LookPreviews), under its Caption "Preview". An image: nothing to press or stop on.
    /// </summary>
    Preview,

    /// <summary>§529: a line of words with a link at its end (a calendar's accounts and "Manage accounts"): a link with words to say.</summary>
    Words,

    /// <summary>
    /// §552: a folder's face on the folder's page: one card, its head its name, its Remove and what it shows
    /// (<see cref="PanelRow.Sentence"/>), its settings its rows, open unless shut. §645: a widget's page has none, its sections plain.
    /// </summary>
    Group,

    /// <summary>§575: a widget of the add list: a clickable card — its icon, its name, why it is greyed or where it is under it —, a click adding it.</summary>
    Pick,

    /// <summary>
    /// §592: a tab strip (WinUI's SelectorBar): its tabs (<see cref="PanelRow.Rows"/>, each a <see cref="Tab"/>) side by side,
    /// the chosen one's rows laid under it on the page; the one chosen kept by PanelMemory (<see cref="PanelTabs"/>).
    /// </summary>
    Tabs,

    /// <summary>§592: one tab of a strip (an Appearance part): its words, its rows (<see cref="PanelRow.Rows"/>) shown while it is chosen.</summary>
    Tab,

    /// <summary>
    /// §670: a choice or a colour with its picker inline (SettingsPanel.Picker.cs): an expander's head saying what it is set to,
    /// its picker's rows (<see cref="PanelRow.Rows"/>) in the card under it while open.
    /// </summary>
    Picker,

    /// <summary>§670: an inline picker's cells (PanelLayout.PickerCells): its choices, its swatches or tiles, its mixer.</summary>
    PickerBody,
}

/// <summary>
/// §535: what a colour button shows (the person, 2026-11-07: "the button should show the color"): the colour in force,
/// picked or what an unset one comes to (<paramref name="Color"/>; null for Windows' own, the accent and the base half and
/// half), or none (<paramref name="Slashed"/>, a slash across it); §528 several widgets wearing several (<paramref name="Mixed"/>,
/// a dash across it). Under a see-through colour a checker shows its opacity.
/// </summary>
internal sealed record PanelSwatch(Argb? Color, bool Slashed = false, bool Mixed = false)
{
    /// <summary>A colour row's: its swatch (OptionRow.Color, the colour in force).</summary>
    public static PanelSwatch Of(FlyoutTile tile) => new(tile.Slashed || tile.Mixed ? null : tile.Color, tile.Slashed && !tile.Mixed, tile.Mixed);

    /// <summary>A row's colour button (an account's calendar's, a holiday calendar's: OptionMenu.ColourTool); null for a button of words.</summary>
    public static PanelSwatch? Of(FlyoutTool tool) => tool.Swatch is { } colour ? new(colour) : null;
}

/// <summary>
/// §520: a row of a page, the menu row <paramref name="Item"/> read as a control (<see cref="PanelRows.Of"/>).
/// <paramref name="Key"/>: where it is on its page — its page and the names of the expanders it is in, and its own —, which
/// the open expanders and a link's landing remember it by.
/// </summary>
internal sealed record PanelRow(PanelRowKind Kind, FlyoutItem Item, string Key)
{
    public string Text => Item.Text;

    public string Glyph => Item.Icon is FlyoutIcon.Glyph glyph ? glyph.Code : "";

    /// <summary>What it is set to, at a dropdown's start, an expander's end while shut, a link's end: where it leads.</summary>
    public string? Value { get; init; }

    /// <summary>Its second line, Caption under its name: a description; a note's words.</summary>
    public string? Detail => Item.Detail is { Length: > 0 } detail ? detail : null;

    /// <summary>§595: a badge after its name (the default layout's "Default"); null for none.</summary>
    public string? Badge => Item.Badge is { Length: > 0 } badge ? badge : null;

    /// <summary>Greyed: in the tree all the same, said to be off (§520).</summary>
    public bool Greyed => !Item.Enabled;

    /// <summary>A toggle's, or a row of buttons' switch.</summary>
    public bool On { get; init; }

    /// <summary>§528: the setting of several widgets selected that do not agree on it: a toggle neither on nor off, a segmented control of no choice, each saying "Mixed"; a number box, a dropdown, a colour say it in themselves.</summary>
    public bool Mixed => Item.Mixed;

    /// <summary>§535: a row of buttons with a switch of its own at its end (an account's calendar: its colour, then its switch).</summary>
    public bool Switches { get; init; }

    /// <summary>§535: a colour row's colour button's colour; null for a row of another kind.</summary>
    public PanelSwatch? Colour => Kind == PanelRowKind.Color && Swatch is { } tile ? PanelSwatch.Of(tile) : null;

    public FlyoutControl.Stepper? Stepper => Item.Control as FlyoutControl.Stepper;

    /// <summary>
    /// §521: the field a text box types in (its id the field's pick, its words, its placeholder, its most), or a number box's
    /// value typed in place: its item's own level's field; null where nothing is typed.
    /// </summary>
    public FlyoutItem? Field => Item.Control is FlyoutField ? Item : Item.Children?.FirstOrDefault(kid => kid.Control is FlyoutField);

    /// <summary>§521: the field's control (<see cref="Field"/>).</summary>
    public FlyoutField? Typed => Field?.Control as FlyoutField;

    /// <summary>§521: a way to add one (FlyoutItem.Adds: Add a layout, Add a country): a button whose list is its popup, no value of its own.</summary>
    public bool Adds => Item.Adds;

    /// <summary>
    /// §578: a row that only acts — a button (a way to add one among them) with nothing to say before it: Add an entry, Reset to
    /// default, Add a layout — drawn as its button alone, no card round it (PanelLayout.Foots).
    /// </summary>
    public bool Foot => (Kind == PanelRowKind.Action || Adds) && Detail is null;

    /// <summary>
    /// §521: what a dropdown is set to as a picture (an icon picker's icon), drawn in its box before its words; §578 (the
    /// person, 2026-11-09: "the inline dropdown selectors should all receive icons if possible") else the icon of the choice in
    /// force, where its choices have icons — every dropdown alike, as its list shows them.
    /// </summary>
    public FlyoutIcon? ValueIcon => Kind is PanelRowKind.Choice or PanelRowKind.Picker && !Adds && !Item.Mixed
        ? Item.Value?.Icon ?? Item.Children?.FirstOrDefault(kid => kid.Checked == true && kid.Control is null)?.Icon
        : null;

    /// <summary>
    /// §583: the mark of the choice in force, drawn quieter after what a dropdown says (a palette holding the person's edits:
    /// "Changed"), as its list marks it; null for none.
    /// </summary>
    public string? ValueMark => Kind is PanelRowKind.Choice or PanelRowKind.Picker && !Adds && !Item.Mixed
        && Item.Children?.FirstOrDefault(kid => kid.Checked == true && kid.Control is null)?.Value?.Text is { Length: > 0 } mark ? mark : null;

    /// <summary>§556: the font a dropdown's words are drawn in where it is a font's (a look's Font): the one it is set to, or comes to.</summary>
    public Karpo.Core.TextStyle? ValueFace => Kind is PanelRowKind.Choice or PanelRowKind.Picker && !Item.Mixed ? Item.Value?.Face : null;

    /// <summary>A row of buttons' buttons, grips left out.</summary>
    public IReadOnlyList<FlyoutTool> Tools { get; init; } = [];

    /// <summary>
    /// §682 (the person, 2026-11-12: "the color picker in the accounts should also be inlined somehow"): the pickers of this
    /// row's buttons that open a level of choices (an account's calendar's colour, a holiday calendar's), each a dropdown
    /// keyed by its button (<see cref="FoldedKey"/>) whose body unfolds under the line (<see cref="PanelPickers.Inline"/>).
    /// </summary>
    public IEnumerable<PanelRow> Folded =>
        Tools.Where(tool => tool.Level is { } level && PanelPickers.Unfolds(level)).Select(tool =>
        {
            var item = new FlyoutItem(tool.Id, tool.Name, Children: tool.Level, PickOne: true);
            var key = FoldedKey(Key, tool.Id);
            return new PanelRow(PanelRowKind.Color, item, key) { Rows = PanelPickers.Rows(item, tool.Level!, key) };
        });

    /// <summary>§682: the key of the picker of the button <paramref name="tool"/> of the row keyed <paramref name="row"/>.</summary>
    public static string FoldedKey(string row, uint tool) => row + "#tool" + tool;

    /// <summary>
    /// §551: put in order among its own by dragging it (a grip of its menu row's that may move: one of two or more of a
    /// FlyoutItem.Reorder group) — its handle at its start, the whole row taken by a press —, or by Ctrl+Up and Ctrl+Down.
    /// </summary>
    public bool Drags => Item.Reorder > 0 && Item.Control is FlyoutControl.Tools tools && tools.Buttons.Any(tool => tool.Grip && tool.Enabled);

    /// <summary>An expander's rows.</summary>
    public IReadOnlyList<PanelRow> Rows { get; init; } = [];

    /// <summary>
    /// §671 (the person, 2026-11-12: "preview should not have an additional card around it ... the preview header should also go
    /// away"): a preview of a page's own (the Appearance page's, not one in a section or an expander) stands bare — its sample's
    /// room alone, across the page, no card round it, no "Preview" over it.
    /// </summary>
    public bool Bare => Kind == PanelRowKind.Preview && Key.Count(c => c == '/') == 1;

    /// <summary>§536: the look a preview draws a sample of; null for every other row.</summary>
    public LookShown? Shows => Kind == PanelRowKind.Preview ? Item.Shows : null;

    /// <summary>
    /// §628: what a tile pictures over its words — a layout preset's bar as it makes it (Add a layout's tiles, LookShown.Itself),
    /// drawn as a preview's sample is (LookPreviews) —; null for a tile of words alone and every other row.
    /// </summary>
    public LookShown? Picture => Kind == PanelRowKind.Pick ? Item.Shows : null;

    /// <summary>§529: whether its head folds it — a group with rows (§552: a face with nothing to set folds nothing) —, its chevron at its end.</summary>
    public bool Folds => Kind == PanelRowKind.Group && Rows.Count > 0;

    /// <summary>§529: a group's head's second line: what the group is set to, as a sentence; null for its title alone.</summary>
    public string? Sentence { get; init; }

    /// <summary>
    /// The row standing beside this one in its line (FlyoutItem.Beside), its own item, key, value and state; one mechanism
    /// (§610): §609 one of the person's things of one choice (a holiday calendar), that choice (its region), its dropdown
    /// before the thing's buttons; §610 Palette's dropdown, the palette worn's reset before it; §613 an Appearance value's
    /// control, its reset before it (an icon button); §638 a font's family, its weight's dropdown after it — whose own pair is
    /// the font's reset: a pair may hold one in turn (<see cref="Pairs"/>). Null for every other row.
    /// </summary>
    public PanelRow? Pair { get; init; }

    /// <summary>§638: the rows standing beside this one in its line, in order: its <see cref="Pair"/>, that one's, and so on.</summary>
    public IEnumerable<PanelRow> Pairs
    {
        get
        {
            for (var pair = Pair; pair is not null; pair = pair.Pair)
            {
                yield return pair;
            }
        }
    }

    /// <summary>§610, §613: the button standing beside this row's controls (a reset), among its <see cref="Pairs"/>; null for none.</summary>
    public PanelRow? Button => Pairs.FirstOrDefault(pair => pair.Kind == PanelRowKind.Action);

    /// <summary>§610: the row a control keyed <paramref name="key"/> of this one's line is: one of its <see cref="Pairs"/> where keyed so, else itself.</summary>
    public PanelRow Own(string key) => Pairs.FirstOrDefault(pair => pair.Key == key) ?? this;

    /// <summary>§638: this row with <paramref name="beside"/> standing beside it, after the rows that stand beside it already.</summary>
    public PanelRow With(PanelRow beside) => this with { Pair = Pair is { } pair ? pair.With(beside) : beside };

    /// <summary>§529: a mixed row of several widgets: each one's own row, named for it, set on it alone (FlyoutItem.Theirs).</summary>
    public IReadOnlyList<PanelRow> Theirs { get; init; } = [];

    /// <summary>§529: a mixed row's line under its name: how its widgets' values split ("2 × Regular, 1 × Semibold").</summary>
    public string? Split => Theirs.Count > 0 ? PanelSentences.SplitOf(Theirs) : null;

    /// <summary>§529: its second line: a group's sentence, how a mixed one splits, its description.</summary>
    public string? Second => Sentence ?? Split ?? (Kind is PanelRowKind.Text or PanelRowKind.Action or PanelRowKind.Words || Adds ? null : Detail);

    /// <summary>§529: the rows that only matter after this one (FlyoutItem.Under), standing nested under it, shown with it.</summary>
    public IReadOnlyList<PanelRow> Nested { get; init; } = [];

    /// <summary>A colour's swatch, a choice's own picture.</summary>
    public FlyoutTile? Swatch => Item.Icon as FlyoutTile;

    /// <summary>A dropdown or a colour: what a click opens in a popup under it is its item's children (§521: a text box types in place).</summary>
    public bool Pops => Kind is PanelRowKind.Choice or PanelRowKind.Color;

    /// <summary>Two lines tall: a description, a note's words (a text box's why it is greyed is its tip).</summary>
    public bool Tall => Second is not null;
}

/// <summary>§520: a page as shown: its entry's words and icon, its title, its rows, its title's buttons (a widget's Cut, Copy, Remove).</summary>
internal sealed record PanelPageView(PanelPage Page, string Title, string Glyph, IReadOnlyList<PanelRow> Rows, IReadOnlyList<FlyoutItem> Actions)
{
    /// <summary>The words of its entry on the edit bar: its page's; §575 a widget's and a folder's their title ("Clock", "3 widgets").</summary>
    public string Side { get; init; } = PanelRows.SideOf(Page);

    /// <summary>§529: a widget's or a folder's page opens with a header card (<see cref="PanelHeader"/>); null for a page's title row.</summary>
    public PanelHeader? Header { get; init; }

    /// <summary>
    /// §575: a page with a search of its own at its flyout's top (the add list): its rows for what is typed there; null for a
    /// page without one.
    /// </summary>
    public Func<string, IReadOnlyList<PanelRow>>? Filter { get; init; }

    /// <summary>§575: the rows shown for what is typed in its own search (<paramref name="typed"/>); a page without one its rows.</summary>
    public IReadOnlyList<PanelRow> RowsFor(string typed) => Filter is { } filter ? filter(typed) : Rows;
}

/// <summary>
/// §529: the header card of a widget's or a folder's page (the person, 2026-11-07, of the flow study: "header of the 3rd
/// one"): what is selected — its icon on a tile (several fanned out), its name ("Calendar", "3 widgets"), where it stands
/// ("In a folder on the taskbar") —, the page's Cut, Copy and Remove as a command bar's icon buttons at its end.
/// </summary>
internal sealed record PanelHeader(IReadOnlyList<string> Glyphs, string Name, string Place);

/// <summary>§520: what the edit bar shows: its pages, §575 each an entry, the one open in its flyout.</summary>
internal sealed record SettingsContent(IReadOnlyList<PanelPageView> Pages)
{
    public PanelPageView? Of(PanelPage page) => Pages.FirstOrDefault(each => each.Page == page);

    public IReadOnlyList<PanelPage> Shown => [.. Pages.Select(each => each.Page)];
}

/// <summary>§520: menu rows read as the panel's controls (see the top of the file).</summary>
internal static class PanelRows
{
    /// <summary>§529, §672: a row of segments as a choice's list: each segment a choice, the one in force ticked.</summary>
    public static List<FlyoutItem> ChoicesOf(FlyoutControl.Segments segments) =>
        [.. segments.Buttons.Select((button, at) => new FlyoutItem(button.Id, button.Name, button.Glyph.Length > 0 ? new FlyoutIcon.Glyph(button.Glyph) : null, Enabled: button.Enabled, Checked: at == segments.Chosen, Stays: true))];

    /// <summary>
    /// §520: the rows a control of <paramref name="holder"/> opens in its popup: a row of buttons' button <paramref name="tool"/>
    /// with a level of its own (a name, its colours); else the row's own level; §529 a row of more segments than fit, its
    /// dropdown's list. Null for none.
    /// </summary>
    public static List<FlyoutItem>? PopupOf(FlyoutItem holder, uint tool) =>
        holder.Control is FlyoutControl.Tools tools && tools.Buttons.FirstOrDefault(button => button.Id == tool && button.Level is not null) is { } button ? [.. button.Level!]
        : holder.Children is { Count: > 0 } children ? [.. children]
        : holder.Control is FlyoutControl.Segments segments ? ChoicesOf(segments)
        : null;

    /// <summary>Between the names of a place: "Appearance › Taskbar widgets".</summary>
    public const string Separator = " › ";

    /// <summary>The words of a page's entry on the edit bar.</summary>
    public static string SideOf(PanelPage page) => page switch
    {
        // §614: Windows Settings' word for a page of everything app-wide, "General" (was "Karpo").
        PanelPage.Karpo => S.SettingsGeneralHeader,
        PanelPage.Accounts => S.SettingsNavAccounts,
        PanelPage.Layouts => S.CoreBarMenuLayouts,
        PanelPage.Taskbar => S.DockEditTaskbar,
        PanelPage.Appearance => S.SettingsAppearanceHeader,
        PanelPage.Folder => S.CoreWidgetFolderListTitle,
        PanelPage.Add => S.DockEditAdd,
        PanelPage.AddLayout => S.SettingsLayoutsAdd,
        PanelPage.MailFolders => S.SettingsAccountsMailFolders,
        PanelPage.Errors => S.DockEditErrors(0),
        _ => S.DockMenuWidget,
    };

    /// <summary>
    /// §628: the entry of the edit bar whose flyout shows <paramref name="page"/>: its own; a page under another's entry (Add a
    /// layout's, the Layouts page's; §646 Mail folders, the Accounts page's) that one's — its flyout hangs from it, it stands
    /// selected, and the page's way back goes there.
    /// </summary>
    public static PanelPage EntryOf(PanelPage page) => page switch
    {
        PanelPage.AddLayout => PanelPage.Layouts,
        PanelPage.MailFolders => PanelPage.Accounts,
        _ => page,
    };

    /// <summary>
    /// §646: where going back from <paramref name="from"/> to <paramref name="to"/> lands: on the link <paramref name="by"/> it
    /// was opened by, where it goes back to the page under whose entry it stands (an account's Mail folders, its expander
    /// open, as Windows' Settings goes back to the row it went in by); else nowhere in particular.
    /// </summary>
    public static string? BackTo(PanelPage? from, PanelPage to, string? by) =>
        from is { } under && IsUnder(under) && EntryOf(under) == to ? by : null;

    /// <summary>§628: whether <paramref name="page"/> stands under another page's entry (<see cref="EntryOf"/>), its way back at its top.</summary>
    public static bool IsUnder(PanelPage page) => EntryOf(page) != page;

    /// <summary>
    /// §529: where a widget stands, a header's line under its name: on a taskbar or a dock (<paramref name="type"/>), in a
    /// folder there or not.
    /// </summary>
    public static string PlaceOf(LayoutType type, bool inFolder) => (type, inFolder) switch
    {
        (LayoutType.Dock, true) => S.DockPanelPlaceDockFolder,
        (LayoutType.Dock, false) => S.DockPanelPlaceDock,
        (_, true) => S.DockPanelPlaceBarFolder,
        _ => S.DockPanelPlaceBar,
    };

    /// <summary>
    /// §529: the header of the widgets <paramref name="selected"/> (each its glyph, its kind's name), standing at
    /// <paramref name="place"/>: one its own name; several of one kind "3 × Clock", of several kinds "3 widgets".
    /// </summary>
    public static PanelHeader HeaderOf(IReadOnlyList<(string Glyph, string Name)> selected, string place)
    {
        if (selected.Count == 1)
        {
            return new PanelHeader([selected[0].Glyph], selected[0].Name, place);
        }
        var kinds = selected.Select(each => each.Name).Distinct(StringComparer.Ordinal).Count();
        return new PanelHeader([.. selected.Select(each => each.Glyph)], kinds == 1 ? S.DockEditSelectedKind(count: selected.Count, kind: selected[0].Name) : S.WidgetFolderWidgets(selected.Count), place);
    }

    /// <summary>The glyph of a page's entry on the edit bar (a widget's page: its kind's, given with it).</summary>
    public static string GlyphOf(PanelPage page) => page switch
    {
        PanelPage.Karpo => OptionGlyphs.Settings,
        PanelPage.Accounts => OptionGlyphs.Accounts,
        PanelPage.Layouts => OptionGlyphs.Monitor,
        PanelPage.Taskbar => OptionGlyphs.Taskbar,
        PanelPage.Appearance => OptionGlyphs.Palette,
        PanelPage.Folder => OptionGlyphs.FolderOpen,
        PanelPage.Add => PanelGlyphs.Add,
        PanelPage.AddLayout => OptionGlyphs.New,
        PanelPage.MailFolders => OptionGlyphs.Folder,
        PanelPage.Errors => PanelGlyphs.ErrorRing,
        _ => OptionGlyphs.Settings,
    };

    /// <summary>
    /// §575: the add list's rows (<paramref name="items"/>, AddList.Rows) as the Add page's in the place <paramref name="at"/>:
    /// each section a heading, each folder of the tree an expander of its rows, each widget a card a click adds
    /// (<see cref="PanelRowKind.Pick"/>), greyed with why; for words typed (<paramref name="typed"/>), the widgets that match,
    /// flat, each saying where it is in the tree (AddList.Results). §586 a tile's and a folder's value how many of it the
    /// layout has (<see cref="FlyoutItem.Placed"/>), nothing for none.
    /// </summary>
    public static List<PanelRow> Picks(IReadOnlyList<FlyoutItem> items, string at, string typed = "")
    {
        if (typed.Trim().Length > 0)
        {
            return [.. AddList.Results(items, typed).Select(item => new PanelRow(PanelRowKind.Pick, item, at + "/" + item.Id.ToString(CultureInfo.InvariantCulture)) { Value = PlacedOf(item) })];
        }
        List<PanelRow> rows = [];
        string? heading = null;
        Dictionary<string, int> seen = new(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (item.Separator)
            {
                continue;
            }
            // Under its section's heading, as a page's row (Popular's and Browse's Clock two rows).
            var place = item.Header || heading is null ? at : at + "/" + heading;
            var key = KeyOf(place, item.Text, Nth(seen, place + "/" + item.Text));
            if (item.Header)
            {
                heading = item.Text;
            }
            rows.Add(item.Header ? new PanelRow(PanelRowKind.Heading, item, key)
                : item.Children is { Count: > 0 } children ? new PanelRow(PanelRowKind.Expander, item, key) { Rows = Picks(children, key), Value = PlacedOf(item) }
                : new PanelRow(PanelRowKind.Pick, item, key) { Value = PlacedOf(item) });
        }
        return rows;
    }

    /// <summary>§586: how many of an add page's row the layout has, as its value says it; nothing for none.</summary>
    private static string? PlacedOf(FlyoutItem item) => item.Placed > 0 ? item.Placed.ToString(CultureInfo.InvariantCulture) : null;

    /// <summary>
    /// <paramref name="page"/> as its menu's builder gave its rows (<paramref name="items"/>): each row a control, a
    /// context menu's buttons (Cut, Copy, Remove) its title's, a foot row a row as any other.
    /// </summary>
    public static PanelPageView Page(PanelPage page, string title, string glyph, IReadOnlyList<FlyoutItem> items, string? side = null)
    {
        List<FlyoutItem> actions = [.. items.Where(item => item.Primary)];
        var rows = Of(items.Where(item => !item.Primary).ToList(), page.ToString());
        // §671 (the person, 2026-11-12: "preview in the appearance section should be shown all the way on the top"): a page's
        // own preview (PanelRow.Bare: the Appearance page's) over everything it shows, its Mode and its palette too.
        if (rows.FindIndex(row => row.Bare) is > 0 and var preview)
        {
            var shown = rows[preview];
            rows.RemoveAt(preview);
            rows.Insert(0, shown);
        }
        var view = new PanelPageView(page, title, glyph, rows, actions);
        return side is null ? view : view with { Side = side };
    }

    /// <summary>
    /// The rows of <paramref name="items"/> in the place <paramref name="at"/>: no rule; no heading with nothing under it, none
    /// last. §521: a row's key holds its section's heading ("Display/New displays/Layout"), so two rows of one name under two
    /// headings (a display's Layout and new displays') are two.
    /// </summary>
    public static List<PanelRow> Of(IReadOnlyList<FlyoutItem> items, string at)
    {
        List<PanelRow> rows = [];
        string? heading = null;
        Dictionary<string, int> seen = new(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var place = item.Header || heading is null ? at : at + "/" + heading;
            if (One(item, place, Nth(seen, place + "/" + item.Text)) is { } row)
            {
                // §610: the palette's reset beside Palette's dropdown, §613 a value's reset beside its control — one rule: a row
                // standing beside (FlyoutItem.Beside) is the pair of the one before it, or, first of its level, of the thing
                // whose level it is (§609, One). §638: one beside a row standing beside stands after it (a font's reset after its
                // weight), in the same line.
                // §670: a choice beside an inline picker (a font's weight beside its family) its choices in that picker's card,
                // under its own: no dropdown on the Appearance page.
                if (row is { Item.Beside: true, Kind: PanelRowKind.Choice } && rows.Count > 0 && rows[^1] is { Kind: PanelRowKind.Picker } picker)
                {
                    rows[^1] = picker with { Rows = [.. picker.Rows, PanelPickers.Beside(row)] };
                    continue;
                }
                if (row.Item.Beside && rows.Count > 0 && rows[^1] is { Kind: not PanelRowKind.Heading } before)
                {
                    rows[^1] = before.With(row);
                    continue;
                }
                rows.Add(row);
            }
            if (item.Header && item.Text.Length > 0)
            {
                heading = item.Text;
                // §551: a section showing a look its preview at its top, under its heading.
                if (item.Shows is { } shows)
                {
                    rows.Add(PreviewOf(shows, at + "/" + heading));
                }
            }
        }
        // §592: levels shown as tabs (the Appearance's parts) one strip, the look they edit previewed over it.
        rows = PanelTabs.Of(Nest(rows), at);
        return [.. rows.Where((row, index) => row.Kind != PanelRowKind.Heading || (index + 1 < rows.Count && rows[index + 1].Kind != PanelRowKind.Heading))];
    }

    /// <summary>
    /// §532: which of the rows of one name in one place a row is, from 1 — two of the taskbar menu's separators are two
    /// rows, keyed apart (<see cref="KeyOf"/>).
    /// </summary>
    private static int Nth(Dictionary<string, int> seen, string key) => seen[key] = seen.GetValueOrDefault(key) + 1;

    /// <summary>The key of the <paramref name="nth"/> row named <paramref name="text"/> in the place <paramref name="at"/>: "Taskbar/Separator #2" for a second.</summary>
    private static string KeyOf(string at, string text, int nth) => at + "/" + text + (nth > 1 ? " #" + nth.ToString(CultureInfo.InvariantCulture) : "");

    /// <summary>
    /// The key of <paramref name="item"/>, the <paramref name="nth"/> of its name in the place <paramref name="at"/>; §592 a tab's
    /// marked (<see cref="PanelTabs.Mark"/>), so its rows are in it ("Appearance/@Taskbar widgets/Font") and the memory knows it.
    /// </summary>
    private static string KeyOf(string at, FlyoutItem item, int nth) => KeyOf(at, PanelTabs.Is(item) ? PanelTabs.Mark + item.Text : item.Text, nth);

    /// <summary>
    /// §529: <paramref name="rows"/> with each row that only matters after a choice (its item's <see cref="FlyoutItem.Under"/>)
    /// nested under that choice's row (<see cref="PanelRow.Nested"/>), in their order, wherever either stood; one whose choice
    /// is not among them stays where it is.
    /// </summary>
    public static List<PanelRow> Nest(List<PanelRow> rows)
    {
        var parents = rows.Where(row => row.Item.Setting is not null).GroupBy(row => row.Item.Setting!, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        bool Nests(PanelRow row) => row.Item.Under is { } under && parents.TryGetValue(under, out var parent) && !ReferenceEquals(parent, row);
        if (!rows.Any(Nests))
        {
            return rows;
        }
        return [.. rows.Where(row => !Nests(row)).Select(row => row.Item.Setting is { } setting && ReferenceEquals(parents.GetValueOrDefault(setting), row)
            ? row with { Nested = [.. row.Nested, .. rows.Where(each => Nests(each) && each.Item.Under == setting)] }
            : row)];
    }

    /// <summary>One menu row as a control; null for a rule, an empty heading or a button of a context menu's row.</summary>
    public static PanelRow? One(FlyoutItem item, string at, int nth = 1)
    {
        if (item.Separator || item.Primary || (item.Header && item.Text.Length == 0))
        {
            return null;
        }
        var key = KeyOf(at, item, nth);
        var value = item.Value?.Text is { Length: > 0 } said && item.Value.Icon is null ? said : null;
        // §529: a mixed row of several widgets holds each one's own row, under its key.
        IReadOnlyList<PanelRow> theirs = item.Theirs is { Count: > 0 } each ? Of(each, key) : [];
        PanelRow Make(PanelRowKind kind) => new(kind, item, key) { Value = value, Theirs = theirs };
        // §536, §585: an expander's rows; one that shows a look (the taskbar menu's: the menu itself) its preview at its top,
        // over the rows that set it — the one way a look's sample comes into a level, as a heading's comes under it (Of).
        List<PanelRow> Level(IReadOnlyList<FlyoutItem> own) => item.Shows is { } shows ? [PreviewOf(shows, key), .. Of(own, key)] : Of(own, key);
        if (item.Header)
        {
            return Make(PanelRowKind.Heading);
        }
        // §592: a level shown as a tab (an Appearance part): its rows in it, no preview of its own (its strip's is over them all).
        if (PanelTabs.Is(item))
        {
            return Make(PanelRowKind.Tab) with { Rows = Of(item.Children!, key) };
        }
        switch (item.Control)
        {
            case FlyoutControl.Toggle toggle:
                return Make(PanelRowKind.Toggle) with { On = toggle.On };
            case FlyoutControl.Radio radio:
                return Make(PanelRowKind.Toggle) with { On = radio.On };
            case FlyoutControl.Stepper:
                return Make(PanelRowKind.Stepper);
            // §529: four or more (a folder's alignment, a display's position) do not fit one control's width side by side: a
            // dropdown of them (ChoicesOf), as any choice of more.
            // §672 (the person, 2026-11-12: "the dropdown should also be integrated for the switches that switch between even
            // only 2 values"): a row of segments (a folder's alignment, Show desktop's style) its choices, an inline picker as
            // any choice of one — no segmented control.
            case FlyoutControl.Segments segments:
                return One(item with { Control = null, Children = ChoicesOf(segments), PickOne = true }, at, nth);
            // §521: one of the person's things with rows of its own (a layout: its name, its icon, a copy) an expander, its
            // buttons (Delete) at its head's end; one without, a row with its buttons.
            // §529: a folder's face: its settings nested under it, its Remove at its end.
            case FlyoutControl.Tools tools when item.Nests && item.Children is { Count: > 0 } settings:
                return Make(PanelRowKind.Item) with { Tools = [.. tools.Buttons.Where(tool => !tool.Grip)], Nested = Of(settings, key) };
            // §609: a thing of one choice (a holiday calendar's region): that choice's dropdown in its row, before its buttons —
            // §610's one rule, the choice standing beside (FlyoutItem.Beside) its thing, the row its level is of.
            case FlyoutControl.Tools tools when item.Children is { Count: > 0 } one && Of(one, key) is [{ Kind: PanelRowKind.Choice, Item.Beside: true } choice]:
                return Make(PanelRowKind.Item) with { Tools = [.. tools.Buttons.Where(tool => !tool.Grip)], Pair = choice };
            case FlyoutControl.Tools tools when item.Children is { Count: > 0 } own && !item.PickOne:
                return Make(PanelRowKind.Expander) with { Tools = [.. tools.Buttons.Where(tool => !tool.Grip)], Rows = Level(own) };
            // §521: a form's field (an account's address, its password) a text box in place, its row the field itself.
            case FlyoutField field:
                return Make(PanelRowKind.Text) with { Value = field.Masked ? null : field.Text.Length > 0 ? field.Text : null };
            case FlyoutControl.Tools tools:
                return Make(PanelRowKind.Item) with
                {
                    Tools = [.. tools.Buttons.Where(tool => !tool.Grip)],
                    On = tools.Switch == true,
                    Switches = tools.Switch is not null,
                    Value = value ?? Ticked(item),
                };
        }
        if (item.Note)
        {
            return Make(PanelRowKind.Note);
        }
        // §529: a link with words to say (a calendar's accounts) a line of them, the link at its end; §689 one whose words are its
        // second line (an error's why: FlyoutItem.Under) a link as any.
        if (item.Opens is not null && item.Detail is { Length: > 0 } && !item.DetailUnder)
        {
            return Make(PanelRowKind.Words);
        }
        // §646: one to a page under its own what it holds at its end ("Inbox and 2 more synced"), else where it leads.
        if (item.Opens is not null)
        {
            return Make(PanelRowKind.Link) with { Value = value ?? LeadOf(item) ?? item.Opens };
        }
        if (item.Children is not { Count: > 0 } children)
        {
            // §521: a row that acts a button at its end; one greyed without a level of its own (a text past the licence) too.
            // §628: one that pictures what it makes (Add a layout's presets) a tile, the picture over its words.
            return Make(item.Shows is not null ? PanelRowKind.Pick : PanelRowKind.Action);
        }
        // §670: a choice or a colour its picker inline, in its card (SettingsPanel.Picker.cs); §672 on every page. §682 (the
        // person, 2026-11-12: "the color picker in the accounts should also be inlined somehow", "public holidays should also be
        // moved to the inline dropdowns"): one standing beside another's controls (an account's colour, a country's region)
        // keeps its box there, its picker's body opening under its line (PanelLayout.Inside), never a popup.
        PanelRow Picked(PanelRowKind kind) => PanelPickers.Takes(item, children)
            ? Make(PanelRowKind.Picker) with { Rows = PanelPickers.Rows(item, children, key), Value = value ?? Ticked(item) }
            : Make(kind) with { Rows = PanelPickers.Unfolds(children) ? PanelPickers.Rows(item, children, key) : [] };
        // A colour: its swatch in the control, its picker (§523: a spectrum) among its choices (a pattern has a swatch, and no colour).
        if (item.PickOne && item.Icon is FlyoutTile { Cell: false } && children.Any(kid => kid.Control is FlyoutMix))
        {
            return Picked(PanelRowKind.Color);
        }
        // §521: a line of text typed in place in its box; a search (a zone, a currency: its field finds) a dropdown whose popup is
        // its field over its finds, the filtered list.
        if (children.Any(kid => kid.Control is FlyoutField) && children.All(kid => kid.Separator || kid.Header || kid.Control is FlyoutField || kid.Note))
        {
            var field = (FlyoutField)children.First(kid => kid.Control is FlyoutField).Control!;
            // §676: a lookup (a time zone, a language, a place) its picker inline too: its search over what it finds.
            if (!item.Adds && !item.Beside && PanelPickers.Lookup(children) is not null)
            {
                return Make(PanelRowKind.Picker) with { Rows = PanelPickers.Rows(item, children, key), Value = value ?? (field.Text.Length > 0 ? field.Text : null) };
            }
            return field.Finds
                ? Make(PanelRowKind.Choice) with
                {
                    Value = value ?? (field.Text.Length > 0 ? field.Text : null),
                    // §682: a lookup beside another's controls, or one that adds (Add a country), its search and finds under its line.
                    Rows = PanelPickers.Lookup(children) is not null ? PanelPickers.Rows(item, children, key) : [],
                }
                : Make(PanelRowKind.Text) with { Value = field.Masked ? value : field.Text.Length > 0 ? field.Text : null };
        }
        if (item.PickOne)
        {
            return Picked(PanelRowKind.Choice);
        }
        var rows = Level(children);
        // §684: one that says what it holds itself (a count, FlyoutValue.Whole) says that, not its things' names.
        return Make(PanelRowKind.Expander) with { Rows = rows, Value = item.Value is { Whole: true } ? value : Summary(rows, value) };
    }

    /// <summary>§536: the last part of a preview's key, which no row's name is.</summary>
    public const string PreviewKey = "#preview";

    /// <summary>
    /// §536: the preview of <paramref name="shows"/> in the place <paramref name="at"/>: the one way a look's sample comes onto a
    /// page — §551, §583 under the heading of the section its rows are in (the Appearance page's), §585 at the top of the expander
    /// that shows it (the taskbar menu's).
    /// </summary>
    public static PanelRow PreviewOf(LookShown shows, string at) =>
        new(PanelRowKind.Preview, new FlyoutItem(0, S.DockEditPreview, Shows: shows), at + "/" + PreviewKey);

    /// <summary>
    /// Where a link row leads, said at its end: the page that owns its setting, and the expander there ("General › Public holidays",
    /// EditEntries.Landing); null for a row that is no link.
    /// </summary>
    public static string? LeadOf(FlyoutItem item)
    {
        foreach (var link in Enum.GetValues<OptionLink>())
        {
            if (item.Opens == OptionLinks.Opens(link))
            {
                var landing = EditEntries.Landing(EditEntries.Of(link));
                return landing.Level is null ? SideOf(landing.Page) : SideOf(landing.Page) + Separator + item.Text;
            }
        }
        return null;
    }

    /// <summary>The choice ticked among a row's level, said at its end.</summary>
    private static string? Ticked(FlyoutItem item) => item.Children?.FirstOrDefault(kid => kid.Checked == true)?.Text;

    /// <summary>
    /// What a shut expander says at its end: the accounts or the layouts it holds by name, else what it is set to. Never a
    /// list of its rows' names (§520: they told nothing opening it doesn't).
    /// </summary>
    private static string? Summary(List<PanelRow> rows, string? value)
    {
        var items = rows.Where(row => row.Kind == PanelRowKind.Item).Select(row => row.Text).Distinct(StringComparer.Ordinal).ToList();
        return items.Count > 0 ? string.Join(", ", items) : value;
    }

    /// <summary>Every row of <paramref name="rows"/>, an expander's rows after it, §529 the rows nested under it after those, at any depth.</summary>
    public static IEnumerable<PanelRow> All(IReadOnlyList<PanelRow> rows)
    {
        foreach (var row in rows)
        {
            yield return row;
            foreach (var inner in All([.. row.Rows, .. row.Nested, .. row.Theirs]))
            {
                yield return inner;
            }
        }
    }

    /// <summary>
    /// The expanders <paramref name="key"/>'s row is in, outermost first; null where no row of <paramref name="rows"/> has it.
    /// §529: a row nested under another is shown with it: that one is no expander to open.
    /// </summary>
    public static List<string>? PathTo(IReadOnlyList<PanelRow> rows, string key)
    {
        foreach (var row in rows)
        {
            // §609, §610: a row's pair stands in its line.
            if (row.Key == key || row.Pairs.Any(pair => pair.Key == key))
            {
                return [];
            }
            // §592: a tab strip no expander to open: its tab is (PanelMemory: chosen).
            if (PathTo(row.Rows, key) is { } inner)
            {
                return row.Kind == PanelRowKind.Tabs ? inner : [row.Key, .. inner];
            }
            if (PathTo(row.Nested, key) is { } under)
            {
                return under;
            }
            // §529: each one's own row of a mixed one: shown as that one's are opened.
            if (PathTo(row.Theirs, key) is { } theirs)
            {
                return [PanelGroups.EachOf(row.Key), .. theirs];
            }
        }
        return null;
    }

    /// <summary>
    /// §551: the rows <paramref name="key"/>'s row is put in order among (itself one of them), in their order: the rows of the
    /// list it stands in (a page's, an expander's, a group's) that drag in its group; none where it drags not.
    /// </summary>
    public static IReadOnlyList<PanelRow> Order(IReadOnlyList<PanelRow> rows, string key)
    {
        if (rows.FirstOrDefault(row => row.Key == key) is { } found)
        {
            return found.Drags ? [.. rows.Where(row => row.Drags && row.Item.Reorder == found.Item.Reorder)] : [];
        }
        foreach (var row in rows)
        {
            foreach (var under in (IReadOnlyList<PanelRow>[])[row.Rows, row.Nested])
            {
                if (Order(under, key) is { Count: > 0 } order)
                {
                    return order;
                }
            }
        }
        return [];
    }

    /// <summary>The row of <paramref name="rows"/> (at any depth) whose item is <paramref name="id"/>; null for none.</summary>
    public static PanelRow? WithId(IReadOnlyList<PanelRow> rows, uint id) => All(rows).FirstOrDefault(row => row.Item.Id == id);

    /// <summary>The item of <paramref name="items"/> (at any depth) at <paramref name="key"/> in place <paramref name="at"/>; null for none.</summary>
    public static FlyoutItem? Find(IReadOnlyList<FlyoutItem> items, string at, string key)
    {
        string? heading = null;
        Dictionary<string, int> seen = new(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var place = item.Header || heading is null ? at : at + "/" + heading;
            var here = KeyOf(place, item, Nth(seen, place + "/" + item.Text));
            if (item.Header && item.Text.Length > 0)
            {
                heading = item.Text;
            }
            if (here == key)
            {
                return item;
            }
            if (item.Children is { Count: > 0 } children && key.StartsWith(here + "/", StringComparison.Ordinal) && Find(children, here, key) is { } found)
            {
                return found;
            }
            // §529: each one's own row of a mixed one.
            if (item.Theirs is { Count: > 0 } theirs && key.StartsWith(here + "/", StringComparison.Ordinal) && Find(theirs, here, key) is { } own)
            {
                return own;
            }
        }
        return null;
    }
}

/// <summary>
/// §592 (the person, 2026-11-09: "for the appearance settings page i want only one preview now, then there should be tabs
/// underneath for the four different sections / groups of styles that you can change"): levels shown as tabs — the
/// Appearance's parts (LookRows.IsTab) — one strip, as WinUI's SelectorBar: their words side by side, the chosen one's rows
/// laid under it as the page's own, the look they edit previewed once over the strip. The tab chosen is kept as an open key
/// (PanelMemory: one of a strip at a time, for the run of the app), the first where none is.
/// </summary>
internal static class PanelTabs
{
    /// <summary>What a tab's key holds before its name: "Appearance/@Bar".</summary>
    public const string Mark = "@";

    /// <summary>The last part of a strip's key ("Appearance/#tabs"), which no row's name is.</summary>
    public const string Key = "#tabs";

    /// <summary>Whether <paramref name="item"/> is a level shown as a tab: an Appearance part's, its rows its children.</summary>
    public static bool Is(FlyoutItem item) => LookRows.IsTab(item.Setting) && item.Children is { Count: > 0 };

    /// <summary>Whether <paramref name="key"/> is a tab's: its last part marked.</summary>
    public static bool IsTab(string key) => key.LastIndexOf('/') is var at and >= 0 && at + 1 < key.Length && key[at + 1] == Mark[0];

    /// <summary>The place a tab's strip is in: its key less its own part.</summary>
    public static string StripOf(string key) => key[..Math.Max(0, key.LastIndexOf('/'))];

    /// <summary>
    /// <paramref name="rows"/> (a place <paramref name="at"/>'s) with each run of tabs one strip (<see cref="PanelRowKind.Tabs"/>),
    /// the look its tabs edit previewed right over it; the rest as they are.
    /// </summary>
    public static List<PanelRow> Of(IReadOnlyList<PanelRow> rows, string at)
    {
        List<PanelRow> made = [];
        for (var index = 0; index < rows.Count; index++)
        {
            if (rows[index].Kind != PanelRowKind.Tab)
            {
                made.Add(rows[index]);
                continue;
            }
            var tabs = rows.Skip(index).TakeWhile(row => row.Kind == PanelRowKind.Tab).ToList();
            if (tabs[0].Item.Shows is { } shows)
            {
                made.Add(PanelRows.PreviewOf(shows, at));
            }
            // §673: what stands beside the last tab (the palette's reset) the strip's, at its right end.
            var beside = tabs[^1].Pair;
            tabs[^1] = tabs[^1] with { Pair = null };
            made.Add(new PanelRow(PanelRowKind.Tabs, new FlyoutItem(0, ""), at + "/" + Key) { Rows = tabs, Pair = beside });
            index += tabs.Count - 1;
        }
        return made;
    }

    /// <summary>Which of <paramref name="strip"/>'s tabs is chosen (from 0): the first <paramref name="open"/> holds, else the first.</summary>
    public static int Chosen(PanelRow strip, IReadOnlySet<string> open)
    {
        for (var at = 0; at < strip.Rows.Count; at++)
        {
            if (open.Contains(strip.Rows[at].Key))
            {
                return at;
            }
        }
        return 0;
    }
}

/// <summary>
/// §520: what the edit bar remembers, in memory only (no config): the expanders open on each page, for the run of the app;
/// §575 the entry whose flyout stands open, for the edit session; and the selection it follows — a widget selected, or a
/// folder opened, opens its entry's flyout; as it goes, that flyout closes.
/// </summary>
internal sealed class PanelMemory
{
    private readonly HashSet<string> open = new(StringComparer.Ordinal);

    /// <summary>§575: the page whose flyout stands open; null for none.</summary>
    public PanelPage? Page { get; private set; }

    /// <summary>The widget and the folder followed last.</summary>
    private (string? Widget, string? Folder) followed;

    /// <summary>Whether the expander at <paramref name="key"/> stands open; §529 a group's, whether it stands shut.</summary>
    public bool IsOpen(string key) => open.Contains(key);

    /// <summary>The expander at <paramref name="key"/> opened or shut.</summary>
    public void Toggle(string key)
    {
        if (!open.Remove(key))
        {
            _ = open.Add(key);
        }
    }

    /// <summary>
    /// The expanders at <paramref name="keys"/> open (a link's landing, a way in from outside); §529 a group there no longer
    /// shut; §592 a tab there chosen, the others of its strip not.
    /// </summary>
    public void Open(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            if (PanelTabs.IsTab(key))
            {
                _ = open.RemoveWhere(other => PanelTabs.IsTab(other) && PanelTabs.StripOf(other) == PanelTabs.StripOf(key));
                _ = open.Add(key);
                continue;
            }
            _ = PanelGroups.IsGroup(key) ? open.Remove(key) : open.Add(key);
        }
    }

    /// <summary>The open expanders' keys, for a layout.</summary>
    public IReadOnlySet<string> Opened => open;

    /// <summary>§575: <paramref name="page"/>'s flyout open (none for null): an entry clicked, a link's landing, a way in from outside.</summary>
    public void Show(PanelPage? page) => Page = page;

    /// <summary>§575: a new edit session: no flyout open, no selection followed (§537 to §574 edit mode opened with its panel open).</summary>
    public void Reset()
    {
        Page = null;
        followed = default;
    }

    /// <summary>
    /// §576: the selection picked — a widget clicked, the one selected too —: its entry's flyout opens as it is followed next
    /// (<see cref="Follow"/>), though it is the one followed already.
    /// </summary>
    public void Picked() => followed = default;

    /// <summary>
    /// §575: the selection as it is now (§520 decision 3): another widget selected (<paramref name="widget"/>; several as one),
    /// its entry's flyout opens; another folder opened or selected (no widget; <paramref name="folder"/>), its; the widget or the
    /// folder whose flyout stands open gone, it closes. The page whose flyout the selection opened now; null for none.
    /// </summary>
    public PanelPage? Follow(string? widget, string? folder)
    {
        var (lastWidget, lastFolder) = followed;
        followed = (widget, folder);
        if (widget is not null && widget != lastWidget)
        {
            return Page = PanelPage.Widget;
        }
        if (widget is null && folder is not null && folder != lastFolder)
        {
            return Page = PanelPage.Folder;
        }
        if ((Page == PanelPage.Widget && widget is null) || (Page == PanelPage.Folder && folder is null))
        {
            Page = null;
        }
        return null;
    }
}

