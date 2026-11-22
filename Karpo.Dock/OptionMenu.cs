// menus: a widget's settings, and a folder's faces, as rows of its menu (2026-10-19, the person: "the
// settings for the processor graph, inline them into the context menu, all of them" and "in edit mode
// for the taskbar, you should inline the settings for the folder faces into the left click menu ...
// adding and removing and reordering a folder face should all be possible"; docs\widgets.md §215).
//
// The settings are the core's (Karpo.Core\OptionRows.cs), the very rows Settings' page has, each read
// from the widget or the face and set there; this file is only how a menu shows them:
//
//  - **A toggle** is a row with Windows 11's toggle switch at its end: a click flips it, and the menu
//    stays open, showing it as it is now (Flyout.Controls.cs).
//  - **A choice** is a row opening a submenu of its choices, on whatever level it stands, the one in
//    force ticked at the row's end as a menu ticks its choice; a pick sets it and the menu stays, the
//    submenu with it, the tick moved (MenuKeeps.cs, §219: the person changes several things in a row).
//    §220 to §225 had a face's choices inline in its submenu, a heading and its choices as rows;
//    the person took that back (2026-10-19: "the menus like style, or index, or
//    location picker etc, should be shown as their own level again as they were at the beginning";
//    §226), so a third and a fourth level are fine. A folder's alignment is the exception
//    (OptionRow.Segmented, §253: "alignment left center right should not have a submenu, it should
//    just show three buttons on the same level"): its icons in its row, Windows 11's
//    segmented control, the one in force raised (FlyoutControl.Segments, Flyout.Controls.cs). A
//    display's None in Display ▾ is the other (OptionRow.Inline, §309, §361): a row of the level it
//    stands on, ticked, ruled off from the rows after it.
//  - **A number** is a row with a "− N +" stepper; a step sets it, and the menu stays open.
//  - **A group** — an alarm's days ("Repeat on"), the status icons, the tray's icons — is one row
//    opening a submenu of its rows, so they don't read as switches of the widget's own (§225); a choice
//    in it opens a level of its own again. A tray icon's row wears the icon's own picture, as the bar
//    draws it (OptionRow.Picture, §261).
//  - **A line of text** — a name, a label, an amount owned — is a row saying what it holds, opening a
//    level of its text field alone, which has the keys as it opens; Enter keeps, Escape puts back, and
//    either shuts it (Flyout.Fields.cs, §246; §219 to §245 had the field in the row beside the name);
//    **a search** — a place, a time zone, a share, a feed — a submenu of its field and its
//    results under it. A place's "Use my device's location" is a switch at the top of that level
//    (OptionRow.Within), the field and its results whole while it is on too (§411; a pick turns it off), the row then saying "Device
//    location" (§240, the person: "use my devices location should be a submenu entry in the place
//    selector, also toggleable"); it is no row of the widget's own any more, nor a result of the search.
//    What a pin opens is a choice of kinds, each a search, "Browse…" over a folder's and a file's field (§292, §425).
//  - **The order** is the core's (OptionSlot, §292): what it is of, what of it, its style, its look, its
//    words, where it stands, what it does; then the foot rows.
//  - **A colour, an icon, a target, a time of day, a length** are pickers (MenuPickers.cs, §224): a
//    colour's swatches and its own colour's spectrum (§523), an icon's tiles a submenu, a target and a time's steppers rows.
//  - **An ordered list** — a toggles row's toggles — is a row for each one shown in its order (no heading,
//    §411), a grip at its start to drag it into order among the others, as a folder's faces are, and a
//    button at its end taking it out; then "Add a toggle", a submenu of the rest, a pick going back to the
//    list with the new row lit (§228: the settings app's list and its "Add a toggle", which are gone).
//  - **An action** — a line's list deleted — is a row of its own, asked first in Windows' dialog
//    (OptionRow.Ask, the dock's DeleteAsk), and the menu closes. It and a line of text stand greyed,
//    why on a second line, where they may not be used now (OptionRow.Enabled, Why, §309).
//  - **A note** — the licence's state, Karpo's version — is a row to read (FlyoutItem.Note, §339): its why
//    wrapped whole under it, never greyed, nothing lit or picked.
//  - **A link** — a setting a higher level owns (OptionRowKind.Link, §351: the accounts, the public
//    holidays, a kind of separators' look) — is a row named for it, the arrow
//    out at its end where a value stands, no chevron; picked, the menu closes and the owner's opens at its
//    level (Dock.EditEntries.cs). A screen reader is told what it opens ("Opens Accounts in Settings").
//  - **The person's own things** (OptionRow.Owned, §317, §324) — a line's notes or to-do lists, the
//    calendars of public holidays, the accounts — are managed one way everywhere: one row, its level
//    the way to add one at its top ("New note", made and shown at once; "Add a country", its search
//    opening in place; "Add an account"), ruled off from the things; then each thing a row, under a
//    heading for where it comes from where they come from several (a to-do line's lists: "On this PC",
//    each account's name, §445), what tells it apart on a second line under its name (a calendar's region, an
//    account's kind), its own rows its submenu (none, no chevron, the bins in one column all the same);
//    at its end the buttons it allows: a pencil (F2) opening its name's field in place
//    (FlyoutTool.Level), "Sign in again", a bin (Delete), asked first in Windows' dialog. The menu
//    stays, shown again as it is now. A choice's things (a line's note or list) are ticked where in
//    force and picked by a click on their row. Things put in order — a bar's layouts (§333) — have a
//    grip at their start and are dragged among each other as a folder's faces are — §532: and Move
//    earlier and Move later at their end, a step either way, the settings panel dragging nothing; the
//    layouts, the taskbar menu's entries —, the way to add one under them, and what is about them all (OptionOwned.After) under a rule; a click on
//    one opens its own level as on a face, the one shown ticked (§336). Inline (§343, §361), that level
//    stands in the menu itself: the layouts in the edit bar's Display ▾.
//  - **A level of its own rows** (OptionRowKind.Level, §338) — the kinds of account to add, a server's
//    form, an account's colours — a row opening them, its line under its name; a heading over rows, or
//    without words a rule (OptionRowKind.Heading); a password's line masked (OptionRow.Masked).
//  - **What it is set to** (§228, the person: "also show the set configuration value in the settings
//    as well"): every row opening a choice, a search or a picker says it at its end before the chevron,
//    as Windows 11's settings rows do — the choice in force, the place, the zone; a colour its swatch,
//    an icon its glyph (MenuPickers.cs) —, cut short before the setting's own name is (Flyout.Values.cs;
//    §220 had dropped it when a menu of one width cut the name, "How oft…"; menus grow since §225). A
//    group says what it comes to where that reads at a glance: an alarm's days ("Weekdays", "Mon, Wed",
//    "Never"); a toggles row's list stands open in the menu and needs none; the status icons' mix says nothing.
//
// Every setting is here (§228: "the settings for the widgets in the main application should be
// removed"): no row leads to a page of the settings app any more. Every submenu here is a level that
// opens in place of the menu's rows, its head going back (Flyout.Inline.cs, §271).
//
// A folder's faces (FaceSection), in the menu a click opens on an arranged bar: under the buttons, with
// no heading (§288, the person: "right click folder widget configuration should only show one header, it
// should not show the header for the faces"; the menu's one name, "Folder", is its top row's start,
// Flyout.Primary.cs, §323), a row for each face in the button's order, a grip at its start (§226: "the
// reorder handles should be moved to the front of the items"), opening its settings in place under the face's name (§230, §271), one
// with none no submenu and no chevron (§253); a bin at its end before the chevron, with Delete for its shortcut
// while the row is lit, greyed on the last face (§240, the person: "for the face settings move the
// delete button into the root view again. on the right side before the angled arrow"; §226 and §230
// had it in the submenu). The rows are dragged into order among each
// other, or moved by Ctrl+Up and Ctrl+Down (Flyout.Reorder.cs), as the page's list moves and removes
// them (FaceRows), and the menu stays open, shown again as the button is now. Then "Add a face", the
// face gallery (FaceGallery) with a Find field over it, its groups ruled off, a face the button cannot wear greyed;
// a pick goes back to the faces, the new one lit (§411). It is the list's last row, right under the faces, as "Add a toggle"
// is a toggles list's (§314, the person: "add a face should not be bottom aligned anymore but right
// under the faces list"; §280 to §313 stood it still at the menu's foot).
//
// The foot (Foot; §276, §311): a folder line's "Height ›" stands still at the menu's foot (§517: by its content — how
// tall the widget's box is, the same row in every line's menu, so it stands in the same place in every one —, nothing
// else: a divider's Style is a row of its own, §435 to §513 at the foot, R16). §567: no widget's "Look ›" beside it any
// more (§311 to §566): one look, Karpo's Appearance.
//
// Every row's command is one of this file's (FirstId on), and what it stands for is kept with the menu
// (OptionPick), so a pick acts on the setting the row named even when the menu was shown again since.
using Karpo.Core;

namespace Karpo.Dock;

/// <summary>What a row of a widget's settings does when picked or acted on.</summary>
internal enum OptionPickKind
{
    /// <summary>Nothing: a row that opens a submenu, a heading.</summary>
    None,

    /// <summary>A choice set to <see cref="OptionPick.Value"/>.</summary>
    Set,

    /// <summary>A toggle flipped.</summary>
    Flip,

    /// <summary>A number stepped by <see cref="OptionPick.Value"/>.</summary>
    Step,

    /// <summary>A row of an ordered list moved to place <see cref="OptionPick.Value"/> among the list's (<see cref="OptionRow.Move"/>): its row, dragged into order.</summary>
    Move,

    /// <summary>An action done, asked first where its row asks (<see cref="OptionRow.Ask"/>); the menu closes.</summary>
    Act,

    /// <summary>A face of <see cref="OptionPick.FaceKind"/> put on after the others.</summary>
    AddFace,

    /// <summary>The face taken off.</summary>
    RemoveFace,

    /// <summary>The face moved by <see cref="OptionPick.Value"/> places: its row, dragged into order (<see cref="OptionMenu.Moved"/>).</summary>
    MoveFace,

    /// <summary>A text or a search field: what it kept is the menu's (Flyout.TakeFieldCommit, Dock.MenuFields.cs).</summary>
    Field,

    /// <summary>Windows' own dialog for the setting: a pin's target (MenuPickers.cs).</summary>
    Browse,

    /// <summary>
    /// §523: a colour of one's own mixed in a colour row's picker — its spectrum, its sliders (FlyoutMix) —, the colour the
    /// menu says (FlyoutEventKind.Mix, Dock.MenuPickers.cs).
    /// </summary>
    Mix,

    /// <summary>§351: the menu closes and the one that owns the setting opens at its level, <see cref="OptionPick.Value"/> its <see cref="OptionLink"/> (Dock.EditEntries.cs).</summary>
    Link,
}

/// <summary>
/// A row of a widget's settings as it was shown: its command, what it does, and what to: the widget's
/// own setting <see cref="Key"/> (<see cref="Face"/> -1), or that of its folder's face at
/// <see cref="Face"/>, which was of <see cref="FaceKind"/>. A row of the person's own things that is
/// dragged into order (§333) names the row that <see cref="Moves"/> it (<see cref="OptionThing.Move"/>).
/// </summary>
internal sealed record OptionPick(uint Id, OptionPickKind Kind, int Face = -1, string FaceKind = "", string Key = "", int Value = 0, string Moves = "")
{
    /// <summary>§529: of several widgets selected, the one (its place among them) a row of its own value sets alone; -1 for all.</summary>
    public int Target { get; init; } = -1;
}

/// <summary>
/// §528: a widget a menu's or a page's rows are of — one of the widgets selected: the widget as a copy of the file has it,
/// what its rows choose from, and what its pickers read and write (OptionMenu.Row, Apply, Settings).
/// </summary>
internal sealed record OptionTarget(WidgetItem? Item, OptionDevices? Devices = null, OptionPlace? Place = null);

/// <summary>A widget's settings and a folder's faces as rows of its menu; see the top of the file.</summary>
internal static class OptionMenu
{
    /// <summary>The first command of the rows; the rest count up from it, past every other menu's.</summary>
    public const uint FirstId = 10000;

    /// <summary>
    /// How many commands the rows may take: §467 ⚙ holds every style's editor, each colour's picker its own commands, about
    /// 1600 a style (2000 in all till then); a million, under the fields' results' (Flyout.Fields.cs).
    /// </summary>
    public const int Most = 1_000_000;

    // Segoe Fluent Icons.
    private const string GlyphAdd = "";
    private static readonly string GlyphRemove = char.ConvertFromUtf32(0xE74D);

    /// <summary>§249: a row's icon, from its glyph (OptionRow.Glyph); none for no glyph.</summary>
    internal static FlyoutIcon? IconOf(string glyph) => glyph.Length > 0 ? new FlyoutIcon.Glyph(glyph) : null;

    /// <summary>§429: a style's look as a row's small swatch: its card, its rim where it outlines, a line of its text across it; §462: its pattern on it.</summary>
    internal static FlyoutTile SwatchOf(LookSwatch swatch) => new(swatch.Fill, Cell: false) { Rim = swatch.Rim, Ink = swatch.Ink, Pattern = swatch.Pattern };

    /// <summary>
    /// §634: a look's choice as a tile of a grid (<see cref="OptionRow.Tiled"/>): its card, its rim, its pattern drawn as it is
    /// drawn there, <see cref="MenuPickers.LookTileDip"/> square in a cell of its own, <see cref="MenuPickers.LookTilesAcross"/>
    /// to a line; one wearing no pattern (None) slashed across.
    /// </summary>
    internal static FlyoutTile TileOf(LookSwatch swatch) =>
        new(swatch.Fill, SideDip: MenuPickers.LookTileDip, Across: MenuPickers.LookTilesAcross, CellDip: MenuPickers.LookCellDip)
        {
            Rim = swatch.Rim,
            Pattern = swatch.Pattern,
            Slashed = swatch.Pattern is null,
        };

    /// <summary>§529: a folder's faces' rows and its Add a face (FlyoutItem.Setting): the settings panel's Folder page gathers them.</summary>
    public const string FacesSetting = "FolderFaces";

    /// <summary>Whether <paramref name="id"/> is one of the rows'.</summary>
    public static bool Holds(uint id) => id is >= FirstId and < FirstId + Most;

    /// <summary>The pick row <paramref name="id"/> stands for in <paramref name="picks"/>; null for another row's.</summary>
    public static OptionPick? PickOf(IReadOnlyList<OptionPick> picks, uint id) =>
        Holds(id) && id - FirstId < (uint)picks.Count && picks[(int)(id - FirstId)] is { } pick && pick.Id == id ? pick : null;

    /// <summary>
    /// The rows for <paramref name="options"/> — a widget's own (<paramref name="face"/> -1) or its
    /// folder's face's at <paramref name="face"/> — their commands added to <paramref name="picks"/>,
    /// each by its kind. A choice, a search, a colour, an icon and a group are each a submenu, on
    /// whatever level they stand; an ordered list stands on the level it is on (see the top of the file).
    /// <paramref name="pictures"/>: a row's own picture by what it names (<see cref="OptionRow.Picture"/>,
    /// a tray icon's as the bar draws it, §261), its glyph where it gives none.
    /// </summary>
    public static List<FlyoutItem> Rows(IReadOnlyList<OptionRow> options, List<OptionPick> picks, int face = -1, string faceKind = "", Func<string, FlyoutIcon?>? pictures = null, bool form = false, IReadOnlyList<string>? names = null, int target = -1)
    {
        // §509: a choice whose choices are swatches (a look's pattern, the palette worn) shows the chosen one's in its icon's place.
        FlyoutIcon? Icon(OptionRow option) =>
            option.Swatch is { } own ? SwatchOf(own)
            : option.Chosen >= 0 && option.Chosen < option.ChoiceSwatches.Count ? SwatchOf(option.ChoiceSwatches[option.Chosen])
            : option.Picture is { } named && pictures?.Invoke(named) is { } picture ? picture : IconOf(option.Glyph);
        var items = new List<FlyoutItem>();
        uint Next(OptionPickKind kind, string key = "", int value = 0)
        {
            var id = FirstId + (uint)picks.Count;
            picks.Add(new OptionPick(id, kind, face, faceKind, key, value) { Target = target });
            return id;
        }
        // §337: what holds against a toggle under its name (OptionRow.Why): Start with Windows turned off in Task Manager;
        // §453: what it says at its end, before its switch (OptionRow.Summary): an account's calendar's count of events.
        // §452: one with a colour of its own (an account's calendar) the colour's swatch before the switch (ColourTool).
        // §595: else what tells it apart under its name (OptionRow.Detail), as any row's.
        FlyoutItem Toggle(OptionRow option)
        {
            var flip = Next(OptionPickKind.Flip, option.Key);
            FlyoutControl control = option.Colour is { } colour && ColourTool(colour, picks, face, faceKind) is { } swatch
                ? new FlyoutControl.Tools([swatch]) { Switch = option.On }
                : new FlyoutControl.Toggle(option.On) { Mixed = option.Mixed };
            return new(flip, option.Title, IconOf(option.Glyph), Detail: option.Why ?? option.Detail, Control: control,
                Value: option.Summary is { Length: > 0 } said ? new FlyoutValue(said) : null);
        }
        // §337: a choice's own picture where the menu has one by what it names (a search engine's logo), else its own glyph;
        // §411 (R13): none where the choices have no glyph each of their own, rather than the row's on every one; among
        // pictures (the engines' logos) one without its own glyph (Custom).
        FlyoutIcon? ChoiceIcon(OptionRow option, int at) =>
            at < option.ChoiceSwatches.Count ? SwatchOf(option.ChoiceSwatches[at])
            : at < option.ChoicePictures.Count && option.ChoicePictures[at] is { Length: > 0 } named && pictures?.Invoke(named) is { } picture ? picture
            : option.ChoicesApart || option.ChoicePictures.Any(named => named.Length > 0) ? IconOf(option.ChoiceGlyph(at)) : null;
        // §556: a font's choices (OptionRow.ChoiceFaces), each drawn in its own face; §638 a family's (fonts of several
        // families) under a filter, "Search fonts", a weight's (one family's: a handful) none. A choice found by its words and
        // its English ones.
        List<FlyoutItem> FontChoices(OptionRow option)
        {
            List<FlyoutItem> listed = option.ChoiceFaces.DistinctBy(face => face.Family).Skip(1).Any()
                ? [new FlyoutItem(Next(OptionPickKind.None, option.Key, -1), S.SettingsAppearanceStylesFontSearch, Control: new FlyoutField("", S.SettingsAppearanceStylesFontSearch, Filters: true, NoMatch: S.DockMenuNoMatch))]
                : [];
            for (var at = 0; at < option.Choices.Count; at++)
            {
                var said = option.Choices[at];
                listed.Add(new FlyoutItem(Next(OptionPickKind.Set, option.Key, at), said,
                    Words: [said, Strings.EnglishOf(said) ?? said], Checked: at == option.Chosen, Stays: true) { Face = option.ChoiceFaces[at] });
            }
            return listed;
        }
        // §240: what heads a search's own level (OptionRow.Within), not a row of its own: a place's device
        // switch, a pin's "Browse…" (§292).
        IEnumerable<FlyoutItem> Head(OptionRow option) => option.Kind == OptionRowKind.Toggle ? [Toggle(option)] : MenuPickers.Rows(option, (pick, key, value) => Next(pick, key, value));
        var heads = options.Where(option => option.Within is { } search && options.Any(each => each.Key == search && each.Kind == OptionRowKind.Search)).ToList();
        for (var index = 0; index < options.Count; index++)
        {
            var option = options[index];
            if (heads.Contains(option))
            {
                continue;
            }
            // §529: the first item a row makes says its setting and the choice it stands under (the settings panel nests it).
            var first = items.Count;
            Rows1(option, ref index);
            if (items.Count > first && items[first] is { Separator: false, Setting: null } made)
            {
                items[first] = made with { Setting = option.Group is { } group ? OptionRows.GroupKey(group) : option.Key, Under = option.Under, Beside = option.Beside, Subtle = option.Subtle };
            }
        }
        return items;

        void Rows1(OptionRow option, ref int index)
        {
            // A group's rows together (OptionRow.Group): a toggles row's toggles, an alarm's days, the
            // status icons — a submenu under the group's name, so they don't read as switches of the
            // widget's own; what stands in it may open a level of its own again. An ordered list
            // (OptionRow.Move) stands where it is, its rows dragged into order (OrderedList).
            if (option.Group is { } together)
            {
                var run = options.Skip(index).TakeWhile(each => each.Group == together).Select(each => each with { Group = null }).ToList();
                index += run.Count - 1;
                if (run.Any(each => each.Ordered))
                {
                    items.AddRange(OrderedList(run, Next));
                    return;
                }
                var row = Next(OptionPickKind.None, option.Key);
                // §411 (R13): a group whose rows all wear one icon (an alarm's days) shows none, rather than it on every row;
                // §644 but a swatch, each row's own colour (an account's in Show from).
                var group = Rows(run, picks, face, faceKind, pictures, names: names, target: target);
                if (run.Count > 1 && run.All(each => each.Glyph == run[0].Glyph && each.Picture is null))
                {
                    group = [.. group.Select(each => each.Icon is FlyoutTile ? each : each with { Icon = null })];
                }
                items.Add(new FlyoutItem(row, option.GroupTitle ?? together, IconOf(option.GroupGlyph), Children: group, Value: option.GroupSummary is { } said ? new FlyoutValue(said) : GroupValue(run)));
                return;
            }
            var made = items.Count;
            switch (option.Kind)
            {
                case OptionRowKind.Toggle:
                    items.Add(Toggle(option));
                    break;
                // A few with icons — a folder's alignment — picked in the row itself (§253).
                case OptionRowKind.Choice when option.Segmented:
                {
                    var row = Next(OptionPickKind.None, option.Key);
                    var segments = option.Choices.Select((choice, at) => new FlyoutTool(Next(OptionPickKind.Set, option.Key, at), option.ChoiceGlyph(at), choice)).ToList();
                    items.Add(new FlyoutItem(row, option.Title, IconOf(option.Glyph), Control: new FlyoutControl.Segments(segments, option.Chosen)));
                    break;
                }
                // §343, §361: things whose level stands in the menu itself (the layouts in Display ▾): its rows on this level.
                case OptionRowKind.Things when option.Owned is { } owned && option.Inline:
                    items.AddRange(OwnedLevel(option, owned, picks, face, faceKind, Next));
                    break;
                // §317, §324: the person's own things — a line's notes or lists, the holiday calendars, the
                // accounts —: one row, its level the way to add one over each thing (OwnedLevel).
                case OptionRowKind.Choice or OptionRowKind.Things when option.Owned is { } owned:
                {
                    var row = Next(OptionPickKind.None, option.Key);
                    // §585: its line under its name and the look it shows (the taskbar menu's: every layout's, the menu itself,
                    // the settings panel's preview at its expander's top).
                    items.Add(new FlyoutItem(row, option.Title, Icon(option), Detail: option.Detail, Children: OwnedLevel(option, owned, picks, face, faceKind, Next), Value: ValueOf(option),
                        PickOne: option.Kind == OptionRowKind.Choice, Shows: option.Shows));
                    break;
                }
                // §309: the layouts a bar may show, rows of this level, ruled off from what follows.
                case OptionRowKind.Choice when option.Inline:
                    items.AddRange(option.Choices.Select((choice, at) => new FlyoutItem(Next(OptionPickKind.Set, option.Key, at), choice, IconOf(option.ChoiceGlyph(at)), Checked: at == option.Chosen, Stays: true)));
                    if (index < options.Count - 1)
                    {
                        items.Add(new FlyoutItem(0, "", Separator: true));
                    }
                    break;
                // §634: a choice of looks (a Pattern) a grid of tiles, each the look itself, its name its tooltip and what a
                // screen reader says; the one in force ringed in the accent. The row as any choice's: the chosen one's swatch
                // in its icon's place, its name at its end.
                case OptionRowKind.Choice when option.Tiled:
                {
                    var row = Next(OptionPickKind.None, option.Key);
                    var tiles = option.Choices.Select((choice, at) => new FlyoutItem(Next(OptionPickKind.Set, option.Key, at), choice,
                        at < option.ChoiceSwatches.Count ? TileOf(option.ChoiceSwatches[at]) : null, Checked: at == option.Chosen, Stays: true)).ToList();
                    items.Add(new FlyoutItem(row, option.Title, Icon(option), Detail: option.Detail, Children: tiles, Value: ValueOf(option), PickOne: true));
                    break;
                }
                case OptionRowKind.Choice:
                {
                    // The row first, so its command stays the same however many choices it has.
                    var row = Next(OptionPickKind.None, option.Key);
                    // §472: what each is for under its name, where it says (a layout preset's). §556: a font's under its families.
                    var choices = option.ChoiceFaces.Count == option.Choices.Count && option.Choices.Count > 0 ? FontChoices(option)
                        : option.Choices.Select((choice, at) => new FlyoutItem(Next(OptionPickKind.Set, option.Key, at), choice, ChoiceIcon(option, at),
                            Detail: at < option.ChoiceDetails.Count ? option.ChoiceDetails[at] : null, Checked: at == option.Chosen, Stays: true,
                            // §583: its mark (a palette holding the person's edits) after its name, as a value is said.
                            Value: at < option.ChoiceMarks.Count && option.ChoiceMarks[at].Length > 0 ? new FlyoutValue(option.ChoiceMarks[at]) : null)).ToList();
                    // Greyed with why under it, where it may not be chosen now (a new layout past the licence's); what tells it
                    // apart under it otherwise.
                    items.Add(option.Enabled
                        ? new FlyoutItem(row, option.Title, Icon(option), Detail: option.Detail, Children: choices, Value: ValueOf(option), PickOne: true, Grows: option.Grows, Adds: option.Adds)
                        : new FlyoutItem(row, option.Title, Icon(option), Detail: option.Why, Enabled: false, Value: ValueOf(option)));
                    break;
                }
                case OptionRowKind.Number:
                {
                    var fewer = Next(OptionPickKind.Step, option.Key, -1);
                    var more = Next(OptionPickKind.Step, option.Key, 1);
                    // §333: one that takes a number typed opens a level of its field, as a line of text does,
                    // what it offers under it (a bar's size: AmountFinder, §332); §521 every number does (its value typed in
                    // the panel's number box, held to its range, OptionUnits.Typed).
                    List<FlyoutItem> typed = [new FlyoutItem(Next(OptionPickKind.Field, option.Key), option.Title, Control: new FlyoutField(option.Mixed ? "" : option.SetText is null ? option.Value.ToString(System.Globalization.CultureInfo.CurrentCulture) : option.Text, option.Placeholder, option.Longest, option.Finder))];
                    items.Add(new FlyoutItem(
                        fewer,
                        option.Title,
                        IconOf(option.Glyph),
                        Children: typed,
                        Control: new FlyoutControl.Stepper(
                            option.Shown,
                            fewer,
                            more,
                            option.Value > option.Least,
                            option.Value < option.Most,
                            S.DockEditLess,
                            S.DockEditGreater)
                        {
                            // §456: every number of its range in its unit, and what it says in its figure's place (OptionRow.Said),
                            // so neither button moves as it steps.
                            Range = [.. FlyoutControl.Stepper.Over(option.Value, option.Least, option.Most, value => OptionUnits.Say(option.Unit, value)).Union([option.Shown])],
                        }));
                    break;
                }
                // A line of text: a row saying what it holds, opening a level of its field alone (Flyout.Fields.cs,
                // §246), what it offers under it where it offers anything (§304: its finder).
                // §411 (R1): in a form (OptionRow.Form) the field stands on the form's level itself, saying what it takes;
                // §415: what it needs to know on a quieter line under its box (Gmail's app password).
                case OptionRowKind.Text when form:
                    items.Add(new FlyoutItem(Next(OptionPickKind.Field, option.Key), option.Title, Detail: option.Detail, Enabled: option.Enabled, Control: option.Masked
                        ? new FlyoutField("", option.Title, option.Longest, Masked: true)
                        : new FlyoutField(option.Text, option.Title, option.Longest, option.Finder)));
                    break;
                case OptionRowKind.Text:
                {
                    var row = Next(OptionPickKind.None, option.Key);
                    // §338: a password's masked (OptionRow.Masked): it holds nothing to begin with and offers nothing.
                    var field = new FlyoutItem(Next(OptionPickKind.Field, option.Key), option.Title, Control: option.Masked
                        ? new FlyoutField("", option.Placeholder, option.Longest, Masked: true)
                        : new FlyoutField(option.Text, option.Placeholder, option.Longest, option.Finder));
                    // §309: greyed with why under it, where it may not be used now (a new layout past the licence's).
                    items.Add(new FlyoutItem(row, option.Title, IconOf(option.Glyph), Detail: option.Why, Enabled: option.Enabled, Children: option.Enabled ? [field] : null, Value: ValueOf(option)));
                    break;
                }
                // A search — a place, a time zone, a share, what a pin opens — a submenu of its field and
                // its results; a place's device switch over them (§240), a pin's "Browse…" (§292). §411 (R6): the
                // field and its results stand whole while the device's location is on too (§240 to §410 greyed
                // them): a place picked turns it off (OptionTexts.SetPlace), and a search always lists something.
                case OptionRowKind.Search:
                {
                    var row = Next(OptionPickKind.None, option.Key);
                    var over = heads.Where(head => head.Within == option.Key).ToList();
                    List<FlyoutItem> level = [.. over.SelectMany(Head)];
                    // §411 (R6): a Find field opens empty, the setting's own ticked at the top of what it lists; a Type field with its words.
                    var field = new FlyoutField(option.Text, option.Placeholder, option.Longest, option.Finder);
                    level.Add(new FlyoutItem(Next(OptionPickKind.Field, option.Key), option.Title, Control: field.Finds ? field with { Text = "" } : field));
                    // §425: one of a choice of kinds (a pin's App, Folder, Website, …): the one in force ticked.
                    // §630: what tells it apart under its name (a place's: where its data comes from).
                    items.Add(new FlyoutItem(row, option.Title, IconOf(option.Glyph), Detail: option.Detail, Checked: option.Ticked, Children: level, Value: ValueOf(option), PickOne: true, Adds: option.Adds));
                    break;
                }
                // A colour, an icon, a target, a time, a length: pickers (MenuPickers.cs).
                case var kind when MenuPickers.Shows(kind):
                    items.AddRange(MenuPickers.Rows(option, (pick, key, value) => Next(pick, key, value)));
                    break;
                // An action: done when picked, asked first where it asks (Dock.OptionPicked); one that asks
                // nothing is done where the menu stands, which stays (§311's "Reset all"); greyed with nothing to do. §628 one that
                // pictures what it makes (a layout preset's bar) carries it, for the settings panel's tile.
                case OptionRowKind.Action:
                    items.Add(new FlyoutItem(Next(OptionPickKind.Act, option.Key), option.Title, Icon(option) ?? new FlyoutIcon.Glyph(GlyphRemove), Detail: option.Why ?? option.Detail, Enabled: option.Enabled && option.Set is not null, Stays: option.Ask is null, Shows: option.Shows));
                    break;
                // §338: a level of its own rows — the kinds of account to add, a server's form, an account's
                // colours —, its line under its name, what it comes to at its end.
                case OptionRowKind.Level:
                {
                    var row = Next(OptionPickKind.None, option.Key);
                    // §425: a choice of kinds (a pin's Target) is one setting: what is set at its end, with its picture.
                    var value = ValueOf(option);
                    if (value is not null && option.ValuePicture is { } named && pictures?.Invoke(named) is { } picture)
                    {
                        value = value with { Icon = picture };
                    }
                    // §592: the look it edits (an Appearance tab's), for the settings panel's preview over its tabs.
                    items.Add(new FlyoutItem(row, option.Title, Icon(option), Detail: option.Detail, Children: Rows(option.Level, picks, face, faceKind, pictures, option.Form, names, target), Value: value,
                        PickOne: Kinds(option), Shows: option.Shows));
                    break;
                }
                // §338: a heading over the rows after it (an account's calendars, its mail); one without words a
                // rule (a form's fields from its actions).
                case OptionRowKind.Heading when option.Title.Length == 0:
                    items.Add(new FlyoutItem(0, "", Separator: true));
                    break;
                // §553: its caption under it on the settings panel (the Taskbar menu's: every layout's).
                case OptionRowKind.Heading:
                    // §551: a section whose look it shows says so, for the settings panel's preview under it. §577: its icon,
                    // where it has one.
                    items.Add(new FlyoutItem(Next(OptionPickKind.None, option.Key), option.Title, Icon(option), Detail: option.Detail, Header: true, Shows: option.Shows));
                    break;
                // §339: words to read: its why wrapped whole under it, what it comes to at its end, nothing to pick.
                case OptionRowKind.Note:
                    items.Add(new FlyoutItem(Next(OptionPickKind.None, option.Key), option.Title, Icon(option), Detail: option.Why, Value: option.Summary is { Length: > 0 } said ? new FlyoutValue(said) : null, Note: true));
                    break;
                // §351: the way to the level that owns it: the arrow out at its end, what it opens said to a screen reader.
                case OptionRowKind.Link:
                    items.Add(LinkItem(option, Next(OptionPickKind.Link, option.Key, (int)option.Link)));
                    break;
            }
            // §529: a mixed row of several widgets holds each one's own, named, set on it alone (the settings panel shows them under it).
            if (option.Mixed && names is { } whose && option.Each.Count == whose.Count && items.Count > made && items[made] is { Separator: false } mixed)
            {
                items[made] = mixed with { Theirs = [.. option.Each.Select((own, at) => Rows([own with { Title = whose[at], Under = null, Group = null }], picks, face, faceKind, pictures, form, target: at).FirstOrDefault()).OfType<FlyoutItem>()] };
            }
            // §528: several widgets selected that do not agree on it: its control says so (the settings panel's "Mixed").
            for (var at = made; option.Mixed && at < items.Count; at++)
            {
                items[at] = items[at] with { Mixed = true };
            }
        }
    }

    /// <summary>
    /// §317, §324: the level of the person's own things (<paramref name="owned"/>, <paramref name="option"/>'s):
    /// each thing — §445: where they come from more than one place, under a heading for each, its name said once (§411
    /// ruled them off and said the place under every row) —, what tells it apart under it, its own rows its submenu — §554: its one
    /// choice (a holiday calendar's region) nested under it on the settings panel —, at its end the buttons it allows —
    /// rename (F2, its name's field opened in place: FlyoutTool.Level), its own acts (sign in again, §551 a layout's
    /// duplicate), its colour (§423, its swatches
    /// opened in place the same way), delete (Delete; asked
    /// first by the dock). A choice's things are picked by a click on their rows and ticked where in force.
    /// §411 (R9): then, right under them, the ways to add one (<see cref="OptionOwned.Add"/>, <see cref="OptionOwned.AlsoAdd"/>),
    /// as the rows they are (an action, a search's level, a form); §317 to §410 had them at the level's top.
    /// §333: things put in order (<see cref="OptionOwned.Ordered"/>, a bar's layouts) have a grip at their
    /// start and are dragged into order among each other, as a folder's faces are; the rows after them
    /// (<see cref="OptionOwned.After"/>) under a rule.
    /// §336: a thing of no choice may be ticked all the same (<see cref="OptionThing.Ticked"/>, the layout
    /// shown), its row still opening its own level, as a face's does. §366: one that is picked
    /// (<see cref="OptionThing.Pick"/>, a display's None) does that on a click, the menu staying; among
    /// things put in order, one that is not moved stands in their columns, no grip, never dragged.
    /// </summary>
    private static List<FlyoutItem> OwnedLevel(OptionRow option, OptionOwned owned, List<OptionPick> picks, int face, string faceKind, Func<OptionPickKind, string, int, uint> next)
    {
        List<FlyoutItem> level = [];
        var choice = option.Kind == OptionRowKind.Choice;
        // A row with a submenu beside rows without keeps its chevron's slot, so every bin stands in one
        // column; rows put in order are dragged among their own.
        var reorder = owned.Ordered || owned.Things.Any(thing => thing.Rows.Count > 0) ? ItemGroup : 0;
        var movables = owned.Things.Where(thing => thing.Move is not null).ToList();
        var several = movables.Count > 1;
        // §445: things from more than one place (this PC, an account) stand under a heading for each, its name once.
        var places = owned.Things.Select(thing => thing.Heading).Distinct(StringComparer.Ordinal).Count() > 1;
        string? heading = null;
        for (var at = 0; at < owned.Things.Count; at++)
        {
            var thing = owned.Things[at];
            if (places && thing.Heading is { } from && (at == 0 || from != heading))
            {
                level.Add(new FlyoutItem(next(OptionPickKind.None, option.Key, at), from, Header: true));
            }
            heading = thing.Heading;
            // The row first, so its command stays the same while its buttons' levels come and go; one
            // dragged into order says the row that moves it (Moved).
            var pick = choice ? next(OptionPickKind.Set, option.Key, at)
                : thing.Pick is { } picked ? next(OptionPickKind.Act, picked.Key, 0)
                : next(OptionPickKind.None, option.Key, at);
            List<FlyoutTool> tools = [];
            if (thing.Move is { } move)
            {
                var index = (int)(pick - FirstId);
                picks[index] = picks[index] with { Moves = move.Key };
                // §551: dragged among its own by it, in a menu and in the settings panel alike (§532's ↑ and ↓ are gone).
                tools.Add(Grip(several, move.Title));
            }
            if (thing.Name is { } name)
            {
                var holder = next(OptionPickKind.None, name.Key, 0);
                var field = new FlyoutItem(next(OptionPickKind.Field, name.Key, 0), name.Title, Control: new FlyoutField(name.Text, name.Placeholder, name.Longest));
                tools.Add(new FlyoutTool(holder, name.Glyph, name.Title, Keys: MenuChord.Rename, Level: [field]));
            }
            // Its own actions (an account's Sign in again, §551 a layout's Duplicate), greyed with why in their tip where they may not be now.
            foreach (var act in thing.Acts)
            {
                tools.Add(new FlyoutTool(next(OptionPickKind.Act, act.Key, 0), act.Glyph, act.Enabled ? act.Title : act.Why ?? act.Title, Enabled: act.Enabled && act.Set is not null));
            }
            // §423: its colour's swatches opened in place by a button, as its rename's field is (a holiday calendar's).
            if (thing.Colour is { } colour && ColourTool(colour, picks, face, faceKind) is { } paint)
            {
                tools.Add(paint);
            }
            if (thing.Delete is { } delete)
            {
                // Greyed where it may not be now (the last layout), why in its tip.
                tools.Add(new FlyoutTool(next(OptionPickKind.Act, delete.Key, 0), delete.Glyph, delete.Enabled ? delete.Title : delete.Why ?? delete.Title, Enabled: delete.Enabled, Keys: MenuChord.Delete));
            }
            // §554: a thing of one choice (a holiday calendar's region): the settings panel stands that dropdown with it. §411 (R1)
            // to §553 its row opened the choice's choices themselves, which the panel read as a row of buttons alone: its region
            // could not be picked there. §554 to §608 a row of its own under it; §609 in its row, before its buttons: §610's one
            // rule, the choice standing beside (FlyoutItem.Beside) the thing whose level it is.
            var one = thing.Rows is [{ Kind: OptionRowKind.Choice, Owned: null, Segmented: false, Inline: false }];
            var own = thing.Rows.Count > 0 ? Rows(one ? [thing.Rows[0] with { Beside = true }] : thing.Rows, picks, face, faceKind) : null;
            level.Add(new FlyoutItem(
                pick,
                thing.Title,
                thing.Swatch is { } swatch ? SwatchOf(swatch) : IconOf(thing.Glyph),
                Detail: thing.Summary,
                Checked: choice ? at == option.Chosen : thing.Ticked,
                Children: own,
                Control: tools.Count > 0 ? new FlyoutControl.Tools(tools) : null,
                Stays: choice || thing.Pick is not null,
                Enabled: thing.Pick is not { Enabled: false },
                Reorder: owned.Ordered && thing.Move is null ? -reorder : reorder,
                // §638: what it holds at its end (an account's items synced).
                Value: thing.Holds is { Length: > 0 } holds ? new FlyoutValue(holds) : null)
            {
                Badge = thing.Badge,
            });
        }
        // §333, §411 (R9): the ways to add one right under the list they add to, as "Add a face" is (§314); §452: ruled off
        // from it, every list alike, so they do not read as one more of it (§445 ruled them off from a last place's run alone).
        if (owned.Things.Count > 0 && (owned.Add is not null || owned.AlsoAdd.Count > 0))
        {
            level.Add(new FlyoutItem(0, "", Separator: true));
        }
        foreach (var add in (IEnumerable<OptionRow>)[.. owned.Add is { } under ? [under] : Array.Empty<OptionRow>(), .. owned.AlsoAdd])
        {
            level.AddRange(Rows([add], picks, face, faceKind));
        }
        if (owned.After.Count > 0)
        {
            level.Add(new FlyoutItem(0, "", Separator: true));
            // Each on its own, so a row of choices on this level (None) is no rule from the next.
            foreach (var row in owned.After)
            {
                level.AddRange(Rows([row], picks, face, faceKind));
            }
        }
        return level;
    }

    /// <summary>
    /// §423, §452: <paramref name="colour"/> (a colour row) as a tool at a row's end, the one way a thing that wears a colour
    /// shows it: a swatch of what it wears, picked or Automatic, its swatches opened in place as a rename's field is — a
    /// holiday calendar's beside its bin, an account's calendar's before its switch. Null where it makes no swatches.
    /// </summary>
    private static FlyoutTool? ColourTool(OptionRow colour, List<OptionPick> picks, int face, string faceKind) =>
        Rows([colour], picks, face, faceKind) is [{ Children: { } swatches } picker]
            ? new FlyoutTool(picker.Id, colour.Glyph, colour.Title, Level: swatches) { Swatch = colour.Color }
            : null;

    /// <summary>§351: the arrow out at a link's end, as Windows 11's Settings ends a row that leads elsewhere (Segoe Fluent Icons' OpenInNewWindow).</summary>
    public const string GlyphLink = OptionGlyphs.Launch;

    /// <summary>
    /// §351: a link row (<see cref="OptionRowKind.Link"/>) as command <paramref name="id"/>: its owner's name and
    /// icon, the arrow out where a value stands, no chevron (nothing opens here); its help what it opens
    /// ("Opens Accounts in Settings"). Picked, the menu closes (<see cref="KeepsOpen"/>). §529: what it draws on under its name (a calendar's accounts).
    /// §628: a way to add one that leads to its page (Add a layout) says so (<see cref="FlyoutItem.Adds"/>: the settings panel's
    /// button alone); greyed with why under it, and leading nowhere, where it may not be used now (past the licence's layouts).
    /// §646: one to a page under its own (<see cref="OptionLinks.Under"/>: an account's Mail folders) says what it holds at its
    /// end, where the arrow out stands, as an expander says it: "Inbox and 2 more synced".
    /// </summary>
    internal static FlyoutItem LinkItem(OptionRow option, uint id) => option.Enabled
        ? OptionLinks.Under(option.Link) && option.Summary is { Length: > 0 } holds
            ? new(id, option.Title, IconOf(option.Glyph), Value: new FlyoutValue(holds), Opens: OptionLinks.Opens(option.Link), Adds: option.Adds)
            : new(id, option.Title, IconOf(option.Glyph), Detail: option.Summary, Value: new FlyoutValue(Icon: new FlyoutIcon.Glyph(GlyphLink)), Opens: OptionLinks.Opens(option.Link), Adds: option.Adds)
        : new(id, option.Title, IconOf(option.Glyph), Detail: option.Why, Enabled: false);

    /// <summary>The <see cref="FlyoutItem.Reorder"/> group an ordered list's rows are dragged into order in.</summary>
    public const int ListGroup = 2;

    /// <summary>
    /// §265: the <see cref="FlyoutItem.Reorder"/> group of the person's own things' rows where some have a
    /// submenu (a holiday calendar's region), so a row without one keeps its chevron's slot and every bin
    /// stands in one column (Flyout.SlotBeside); and of things put in order, which are dragged among it
    /// (§333, Flyout.Reorder.cs: the level stands in place of the menu's rows).
    /// </summary>
    public const int ItemGroup = 3;

    /// <summary>
    /// An ordered list (<see cref="OptionRow.Ordered"/>): a row for each of <paramref name="run"/> that is
    /// on, in its order, a grip at its start to drag it among the others (<see cref="ListGroup"/>; its
    /// command a <see cref="OptionPickKind.Move"/>) and a button at its end taking it out (a flip); then
    /// "Add a toggle", a submenu of those off, each put at the list's end, a pick going back (§411). With
    /// none on, it says so, as the settings app's list said.
    /// </summary>
    private static List<FlyoutItem> OrderedList(IReadOnlyList<OptionRow> run, Func<OptionPickKind, string, int, uint> next)
    {
        // §411 (R10): no heading over it; the rule over the widget's rows sets it off.
        List<FlyoutItem> items = [];
        var shown = run.Where(row => row.On).ToList();
        var several = shown.Count > 1;
        foreach (var row in shown)
        {
            // The row first, so its command stays the same however the list moves; -1, for a move to
            // nowhere until it is dragged (Moved). A click on it does nothing, and the menu stays.
            var id = next(OptionPickKind.Move, row.Key, -1);
            var tools = Handles(several, new FlyoutTool(next(OptionPickKind.Flip, row.Key, 0), GlyphRemove, S.SettingsWidgetTogglesRemove));
            items.Add(new FlyoutItem(id, row.Title, row.Text.Length > 0 ? new FlyoutIcon.Glyph(row.Text) : null, Control: tools, Stays: true, Reorder: ListGroup));
        }
        // §655: in the words of what it orders, a toggles row's by default.
        var words = run.Select(row => row.OrderedWords).FirstOrDefault(said => said is not null)
            ?? new OrderedWords(S.SettingsWidgetTogglesAdd, S.SettingsWidgetTogglesEmpty, S.SettingsWidgetTogglesAll);
        if (shown.Count == 0)
        {
            items.Add(new FlyoutItem(next(OptionPickKind.None, "", 0), words.Empty, Enabled: false));
        }
        var adding = next(OptionPickKind.None, "", 0);
        var rest = run.Where(row => !row.On)
            .Select(row => new FlyoutItem(next(OptionPickKind.Flip, row.Key, 0), row.Title, row.Text.Length > 0 ? new FlyoutIcon.Glyph(row.Text) : null, Stays: true))
            .ToList();
        // §411 (R5): one picked goes back to the list, the new row lit (Dock.OptionPicked).
        items.Add(new FlyoutItem(adding, rest.Count > 0 ? words.Add : words.All, new FlyoutIcon.Glyph(GlyphAdd), Enabled: rest.Count > 0, Children: rest.Count > 0 ? rest : null, PickOne: true, Adds: true));
        return items;
    }

    /// <summary>
    /// A row of a list dragged into order (a face's, an ordered list's): the grip at its start, the
    /// handle it is dragged by where <paramref name="movable"/> (Flyout.Reorder.cs), and
    /// <paramref name="remove"/> at its end taking it out.
    /// </summary>
    private static FlyoutControl.Tools Handles(bool movable, FlyoutTool remove) => new([Grip(movable), remove]);

    /// <summary>The grip at the start of a row dragged into order, named <paramref name="name"/> (how to move it); greyed where it cannot move.</summary>
    private static FlyoutTool Grip(bool movable, string? name = null) => new(0, "", name ?? S.DockEditFaceDrag, Enabled: movable, Grip: true);

    /// <summary>
    /// What a choice's or a search's row says it is set to at its end (§228): the choice in force, the
    /// search's words at rest (a place, a share), or what it says short (a zone's "UTC+1", §230); null for none.
    /// </summary>
    internal static FlyoutValue? ValueOf(OptionRow option) => option.Kind switch
    {
        // §556: a font's said in that font.
        OptionRowKind.Choice when option.Chosen >= 0 && option.Chosen < option.Choices.Count => new FlyoutValue(option.SaidOf(option.Chosen))
        {
            Face = option.Chosen < option.ChoiceFaces.Count ? option.ChoiceFaces[option.Chosen] : null,
        },
        // §459: several selected widgets wearing several styles: "Mixed".
        OptionRowKind.Choice when option.Summary is { Length: > 0 } mixed => new FlyoutValue(mixed),
        // §472: a choice of what to add says nothing: none of it is in force.
        OptionRowKind.Choice when option.Adds => null,
        // §411 (R4): a choice of none (a drive not attached, nothing listed) says so, greyed.
        OptionRowKind.Choice when option.Owned is null => new FlyoutValue(S.SettingsWidgetActionsNone, Implied: true),
        // §425: a choice of kinds (a pin's Target) says what is set, or greyed that nothing is; a kind not in force nothing.
        OptionRowKind.Level when Kinds(option) && option.Summary is { Length: > 0 } set => new FlyoutValue(set),
        OptionRowKind.Level when Kinds(option) => new FlyoutValue(option.Empty ?? S.SettingsWidgetActionsNone, Implied: true),
        OptionRowKind.Search when option.Ticked == false => null,
        // §517 (the Sections design): a row opening a level says what it holds at its end — the person's own things the first
        // of them (⚙'s Accounts, Public holidays) or that there are none, a level what its builder sums it up as (a look's
        // "Default", "Custom"); one of actions alone nothing (§411 to §513: no level said anything).
        // §684: one that says how many it holds that, never their names.
        OptionRowKind.Things when option.Holds is { Length: > 0 } holds => new FlyoutValue(holds, Whole: true),
        OptionRowKind.Things when option.Owned is { Things.Count: > 0 } owned => new FlyoutValue(owned.Things[0].Title),
        OptionRowKind.Things when option.Summary is { Length: > 0 } none => new FlyoutValue(none, Implied: true),
        OptionRowKind.Level when option.Summary is { Length: > 0 } said => new FlyoutValue(said),
        OptionRowKind.Things or OptionRowKind.Level => null,
        OptionRowKind.Search when option.Summary is { Length: > 0 } summary => new FlyoutValue(summary),
        // §338: a password's says bullets while it holds one, never its words.
        OptionRowKind.Text when option.Masked => option.Summary is { Length: > 0 } bullets ? new FlyoutValue(bullets) : null,
        OptionRowKind.Search or OptionRowKind.Text when option.Text.Length > 0 => new FlyoutValue(option.Text),
        // §411 (R4): an empty line says what applies instead, greyed: its default (a folder's "Folder"), else "None".
        OptionRowKind.Text => new FlyoutValue(option.Empty ?? (option.Placeholder.Length > 0 ? option.Placeholder : S.SettingsWidgetActionsNone), Implied: true),
        OptionRowKind.Search => new FlyoutValue(option.Empty ?? S.SettingsWidgetActionsNone, Implied: true),
        _ => null,
    };

    /// <summary>§425: whether a level's rows are a choice of kinds, each opening a level of its own (OptionRow.Ticked): one setting, a pick going back.</summary>
    private static bool Kinds(OptionRow option) => option.Kind == OptionRowKind.Level && option.Level.Any(row => row.Ticked is not null);

    /// <summary>
    /// What a group's row says it comes to (§228): an alarm's days as its line says them ("Weekdays",
    /// "Mon, Wed"), "Never" for none; another group of toggles how many are on ("3 shown"); null for any other.
    /// </summary>
    internal static FlyoutValue? GroupValue(IReadOnlyList<OptionRow> run)
    {
        if (run.Count == 0 || run.Any(row => row.Kind != OptionRowKind.Toggle))
        {
            return null;
        }
        if (run.All(row => row.Key.StartsWith(OptionRows.AlarmDay, StringComparison.Ordinal)))
        {
            var days = run.Select(row => (Day: (DayOfWeek)int.Parse(row.Key[OptionRows.AlarmDay.Length..], System.Globalization.CultureInfo.InvariantCulture), row.On)).ToList();
            var mask = days.Where(day => day.On).Aggregate(0, (all, day) => all | AlarmDays.Bit(day.Day));
            var names = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat;
            return new FlyoutValue(mask == 0 ? S.DockEditRepeatNever : AlarmWords.Days(mask, days[0].Day, names.GetAbbreviatedDayName));
        }
        return new FlyoutValue(S.DockEditShownCount(run.Count(row => row.On)));
    }

    /// <summary>
    /// A widget's settings under its menu's other rows (<see cref="Rows"/>, with
    /// <paramref name="pictures"/>), ruled off; none where it has none. No heading: what the widget is
    /// is the start of the menu's top row (Flyout.Primary.cs, §323; §279 to §313 had a heading row here).
    /// </summary>
    public static List<FlyoutItem> Section(IReadOnlyList<OptionRow> options, List<OptionPick> picks, Func<string, FlyoutIcon?>? pictures = null, IReadOnlyList<string>? names = null)
    {
        var rows = Rows(options, picks, pictures: pictures, names: names);
        return rows.Count == 0 ? [] : [new FlyoutItem(0, "", Separator: true), .. rows];
    }

    /// <summary>
    /// The rows that stand still at the menu's foot (<see cref="FlyoutItem.Foot"/>, §276), in their order,
    /// under one rule and none between them (§314); none for none.
    /// </summary>
    public static List<FlyoutItem> Foot(params FlyoutItem?[] rows) =>
        rows.OfType<FlyoutItem>().ToList() is { Count: > 0 } foot ? [new FlyoutItem(0, "", Separator: true), .. foot] : [];

    /// <summary>
    /// A folder's faces as its menu on an arranged bar offers them (see the top of the file), with no
    /// heading of their own (§288): each face's row, a grip at its start, dragged into order among the
    /// others (<see cref="FlyoutItem.Reorder"/>), a bin at its end taking it off, opening its settings
    /// (<see cref="Rows"/>); then, right under them, "Add a face" (§314). <paramref name="devices"/>: the adapters and disks a
    /// reading's face may follow; <paramref name="place"/>: the config the calendar face's holidays write to.
    /// </summary>
    public static List<FlyoutItem> FaceSection(FolderOptions folder, List<OptionPick> picks, OptionDevices? devices = null, OptionPlace? place = null)
    {
        uint Next(OptionPickKind kind, int face = -1, string faceKind = "", int value = 0)
        {
            var id = FirstId + (uint)picks.Count;
            picks.Add(new OptionPick(id, kind, face, faceKind, Value: value));
            return id;
        }
        var faces = folder.Faces;
        List<FlyoutItem> items = [];
        for (var at = 0; at < faces.Count; at++)
        {
            var face = faces[at];
            var info = face.Info;
            // The row first, so its command stays the same while its submenu's rows come and go: a move
            // of it, by as many places as it is dragged (Moved).
            var row = Next(OptionPickKind.MoveFace, at, face.Kind);
            // The face's name heads its level, the level's own head (Flyout.Inline.cs, §271; §230: "always
            // show the name of the face in the folder settings on the second level", a heading of its own
            // until then). §253: a face with nothing to set has no submenu and no chevron (the person: "if a
            // face has no settings dont show the submenu"); a click or Enter on its row does nothing, as on
            // any row of tools without a submenu (Flyout.Controls.cs).
            var rows = Rows(OptionRows.OfFace(folder, at, devices, place), picks, at, face.Kind);
            List<FlyoutItem>? own = rows.Count > 0 ? rows : null;
            // Never the last: a button wears one face at least (FaceRows.RemoveFace).
            var several = faces.Count > 1;
            // The bin at its end, before the chevron, Delete while the row is lit (§240).
            var tools = Handles(several, new FlyoutTool(Next(OptionPickKind.RemoveFace, at, face.Kind), GlyphRemove, S.DockMenuRemove, Enabled: several, Keys: MenuChord.Delete));
            var glyph = info == FolderFaces.Icon ? face.GlyphOrDefault : FaceGallery.GlyphOf(info);
            // §529: the settings panel stands its settings nested under it, its Remove at its end, rather than in an expander.
            items.Add(new FlyoutItem(row, Dock.TitleOf(face), IconOf(glyph), Children: own, Control: tools, Reorder: FaceGroup) { Nests = true, Setting = FacesSetting });
        }
        // "Add a face", the list's last row, right under the faces (§314). §411 (R1, R5, R9): its level the gallery as the add
        // list has its catalogue — a Find field over it, then every face, a group ruled off from the one before, one the
        // button cannot wear greyed —; a pick goes back to the faces, the new one lit (Dock.OptionPicked). §215 to §410
        // had each group a level of its own, and the level stayed.
        var adding = Next(OptionPickKind.None);
        List<FlyoutItem> gallery = [new FlyoutItem(Next(OptionPickKind.None), S.SettingsFolderFacesSearch, Control: new FlyoutField("", S.SettingsFolderFacesSearch, Filters: true, NoMatch: S.DockMenuNoMatch))];
        foreach (var group in FaceGallery.Search(folder, null, null))
        {
            if (gallery.Count > 1)
            {
                gallery.Add(new FlyoutItem(0, "", Separator: true));
            }
            gallery.AddRange(group.Tiles.Select(tile => new FlyoutItem(
                Next(OptionPickKind.AddFace, faceKind: tile.Face.Id),
                tile.Face.Title,
                IconOf(FaceGallery.GlyphOf(tile.Face)),
                Enabled: tile.Available,
                Words: [tile.Face.Blurb, FaceGallery.Title(group.Group)],
                Stays: true)));
        }
        items.Add(new FlyoutItem(adding, S.SettingsFolderFacesAddHeader, new FlyoutIcon.Glyph(GlyphAdd), Children: gallery, PickOne: true, Adds: true) { Setting = FacesSetting });
        return items;
    }

    /// <summary>The <see cref="FlyoutItem.Reorder"/> group a folder's faces' rows are dragged into order in.</summary>
    public const int FaceGroup = 1;

    /// <summary>
    /// What a face's row (<paramref name="pick"/>, a <see cref="OptionPickKind.MoveFace"/> of none)
    /// dragged to place <paramref name="to"/> among the faces does: a move by so many places; an
    /// ordered list's row (<see cref="OptionPickKind.Move"/>), a move to that place among the list's; a
    /// thing's row put in order (§333), its move row's move there; null for any other row.
    /// </summary>
    public static OptionPick? Moved(OptionPick? pick, int to) => pick switch
    {
        { Kind: OptionPickKind.MoveFace, Face: >= 0 } row => row with { Value = to - row.Face },
        { Kind: OptionPickKind.Move } row when to >= 0 => row with { Value = to },
        { Moves.Length: > 0 } row when to >= 0 => row with { Kind = OptionPickKind.Move, Key = row.Moves, Value = to },
        _ => null,
    };

    /// <summary>
    /// The command of the row of the face at <paramref name="face"/> in <paramref name="picks"/>, as a
    /// menu shown again lights the face it moved; null for none.
    /// </summary>
    public static uint? FaceRowOf(IReadOnlyList<OptionPick> picks, int face) =>
        picks.FirstOrDefault(pick => pick is { Kind: OptionPickKind.MoveFace, Value: 0 } && pick.Face == face)?.Id;

    /// <summary>§411 (R5): the command of the row of an ordered list's <paramref name="key"/> in <paramref name="picks"/> (a toggle just added); null for none.</summary>
    public static uint? ListRowOf(IReadOnlyList<OptionPick> picks, string key) =>
        picks.FirstOrDefault(pick => pick is { Kind: OptionPickKind.Move, Value: -1 } && pick.Key == key)?.Id;

    /// <summary>
    /// §467: the command of the row of thing <paramref name="at"/> of the person's things <paramref name="key"/> (a layout in
    /// Display ▾) in <paramref name="picks"/>, the level it opens shown in place by it; null for none. The things' own row
    /// is keyed as their first is, and stands before it.
    /// </summary>
    public static uint? ThingRowOf(IReadOnlyList<OptionPick> picks, string key, int at) =>
        at < 0 ? null : picks.LastOrDefault(pick => pick is { Kind: OptionPickKind.None } && pick.Key == key && pick.Value == at)?.Id;

    /// <summary>
    /// <paramref name="items"/> — a menu showing these rows — with them as <paramref name="now"/> has them,
    /// and every other row as it is: from the first of them to the last, the rules and the headings
    /// between; the rule over them stays.
    /// </summary>
    public static List<FlyoutItem> Updated(IReadOnlyList<FlyoutItem> items, IReadOnlyList<FlyoutItem> now)
    {
        var from = -1;
        var to = -1;
        for (var at = 0; at < items.Count; at++)
        {
            if (Holds(items[at].Id))
            {
                from = from < 0 ? at : from;
                to = at;
            }
        }
        var fresh = now.SkipWhile(item => item.Separator).ToList();
        if (from < 0)
        {
            return [.. items];
        }
        return [.. items.Take(from), .. fresh, .. items.Skip(to + 1)];
    }

    /// <summary>
    /// What <paramref name="pick"/> does to <paramref name="item"/> — the widget as the file has it, in
    /// a copy that is saved where this says it changed: a setting of its own or of its folder's face set
    /// through its row (OptionRows), an ordered list's row moved, an action done (asked first by the
    /// caller), a face added, taken off or moved (FaceRows). A face's pick acts only while the face in
    /// its place is still of the kind it was shown for.
    /// </summary>
    public static bool Apply(WidgetItem? item, OptionPick pick, OptionDevices? devices = null, OptionPlace? place = null)
    {
        if (item is null)
        {
            return false;
        }
        var folder = (item as BarItem)?.Folder;
        var faceHolds = folder is not null && pick.Face >= 0 && pick.Face < folder.Faces.Count && folder.Faces[pick.Face].Kind == pick.FaceKind;
        switch (pick.Kind)
        {
            case OptionPickKind.Move or OptionPickKind.Act or OptionPickKind.Set or OptionPickKind.Flip or OptionPickKind.Step:
                return ApplyRow(Row(item, pick, devices, place), pick);
            case OptionPickKind.AddFace:
                return folder is not null && FolderFaces.Find(pick.FaceKind) is { } adding && FaceRows.AddFace(folder, adding);
            case OptionPickKind.RemoveFace:
                return faceHolds && FaceRows.RemoveFace(folder!, pick.Face);
            case OptionPickKind.MoveFace:
                return faceHolds && FaceRows.MoveFace(folder!, pick.Face, pick.Face + pick.Value);
            default:
                return false;
        }
    }

    /// <summary>
    /// §528: what <paramref name="pick"/> does to every one of <paramref name="targets"/> (the widgets selected, each in the
    /// copy of the file it is of): a setting set on them all through the one row they have in common
    /// (<see cref="OptionRows.Common(IReadOnlyList{OptionRow?})"/>); a face added, taken off or moved on the one folder alone.
    /// </summary>
    public static bool Apply(IReadOnlyList<OptionTarget> targets, OptionPick pick) =>
        pick.Kind is OptionPickKind.Move or OptionPickKind.Act or OptionPickKind.Set or OptionPickKind.Flip or OptionPickKind.Step
            ? ApplyRow(Row(targets, pick), pick)
            : (pick.Target >= 0 && pick.Target < targets.Count ? [targets[pick.Target]] : targets) is [var one] && Apply(one.Item, pick, one.Devices, one.Place);

    /// <summary>§528: the setting <paramref name="pick"/> was shown for as all of <paramref name="targets"/> have it now: their row in common; null for none.</summary>
    public static OptionRow? Row(IReadOnlyList<OptionTarget> targets, OptionPick pick) =>
        // §529: a row of one of them, its own value: that one's alone.
        pick.Target >= 0 && pick.Target < targets.Count ? Row(targets[pick.Target].Item, pick, targets[pick.Target].Devices, targets[pick.Target].Place)
        : OptionRows.Common([.. targets.Select(target => Row(target.Item, pick, target.Devices, target.Place))]);

    /// <summary>§528: what <paramref name="pick"/> asks before it acts on <paramref name="targets"/> (<see cref="AskOf(WidgetItem?, OptionPick, OptionDevices?, OptionPlace?)"/>), once for all.</summary>
    public static DeleteQuestion? AskOf(IReadOnlyList<OptionTarget> targets, OptionPick pick) =>
        pick.Kind == OptionPickKind.Act ? Row(targets, pick)?.Ask : null;

    /// <summary>
    /// What <paramref name="pick"/> asks in Windows' dialog before it acts (<see cref="OptionRow.Ask"/>):
    /// an action's that asks — a note, a holiday calendar, an account deleted (§317, §324) —; null for
    /// nothing to ask.
    /// </summary>
    public static DeleteQuestion? AskOf(WidgetItem? item, OptionPick pick, OptionDevices? devices = null, OptionPlace? place = null) =>
        pick.Kind == OptionPickKind.Act ? Row(item, pick, devices, place)?.Ask : null;

    /// <summary>
    /// What <paramref name="pick"/> does to <paramref name="row"/>, the setting it was shown for as it
    /// is now — a widget's, a face's, a bar's look (BarLooks, §290): a choice set, a toggle flipped, a
    /// number stepped, an ordered list's row moved, an action done. False where
    /// there is no such row or nothing changed.
    /// </summary>
    public static bool ApplyRow(OptionRow? row, OptionPick pick) => pick.Kind switch
    {
        OptionPickKind.Move => pick.Value >= 0 && row is { Move: { } move } && move(pick.Value),
        OptionPickKind.Act => row is { Kind: OptionRowKind.Action, Set: { } act } && act(0),
        OptionPickKind.Flip => row is { Set: { } set } && set(row.On ? 0 : 1),
        // A number by its step within its range; a time of day round the clock, a length within its own (OptionPickers.Stepped).
        OptionPickKind.Step => row is { Set: { } set } && OptionPickers.Stepped(row, pick.Value) is { } stepped && set(stepped),
        OptionPickKind.Set => row is { Set: { } set } && set(pick.Value),
        _ => false,
    };

    /// <summary>
    /// The setting <paramref name="pick"/> was shown for, read again from <paramref name="item"/> as it
    /// is now: its own (with <paramref name="place"/>'s colours), or its folder's face's while the face
    /// in that place is still of the kind it was shown for, a level of the person's own things' among them; null
    /// where there is none.
    /// </summary>
    public static OptionRow? Row(WidgetItem? item, OptionPick pick, OptionDevices? devices = null, OptionPlace? place = null)
    {
        var folder = (item as BarItem)?.Folder;
        var faceHolds = folder is not null && pick.Face >= 0 && pick.Face < folder.Faces.Count && folder.Faces[pick.Face].Kind == pick.FaceKind;
        var rows = item is null ? [] : pick.Face < 0 ? OptionRows.Of(item, devices, place) : faceHolds ? OptionRows.OfFace(folder!, pick.Face, devices, place) : [];
        return OptionRows.Find(rows, pick.Key);
    }

    /// <summary>
    /// <paramref name="item"/>'s settings as its menu shows them, in their order: its <see cref="Section"/>
    /// (with <paramref name="pictures"/>); then the <see cref="Foot"/>: a folder line's "Height" where
    /// <paramref name="roomCells"/> says how many cells its folder holds (LineHeightRows.cs). §481: a folder's are one of its
    /// two menus (<paramref name="side"/>, <see cref="FolderSide"/>): its face's — its faces and "Add a face"
    /// (<see cref="FaceSection"/>) — or its panel's — its own rows. §567: no look of its own.
    /// </summary>
    public static List<FlyoutItem> Settings(WidgetItem? item, List<OptionPick> picks, OptionDevices? devices, OptionPlace? place, FolderSide side, int? roomCells, Func<string, FlyoutIcon?>? pictures = null)
    {
        var folder = (item as BarItem)?.Folder;
        // §481: a folder's own rows are its panel's: none in its face's menu, its faces there.
        List<FlyoutItem> rows = folder is not null && side == FolderSide.Face
            ? [.. FaceSection(folder, picks, devices, place)]
            : Section(OptionRows.Of(item, devices, place), picks, pictures);
        // §432: a sized line's "Size" in the Height's place; one of one form has neither.
        List<FlyoutItem?> height = roomCells is not { } room ? []
            : LineHeightRows.For(item, room) is { } choice ? [.. LineHeightRows.Rows(choice)]
            : [.. LineHeightRows.SizeRows(item)];
        return [.. rows, .. Foot([.. height])];
    }

    /// <summary>
    /// §528: the settings <paramref name="targets"/> — the widgets selected — have in common (OptionRows.Common.cs): of their
    /// own rows those they all have (a folder's face's side holds its faces, which are its own: none); each set on them all.
    /// The one widget's own settings for one.
    /// </summary>
    public static List<FlyoutItem> Settings(IReadOnlyList<OptionTarget> targets, List<OptionPick> picks, FolderSide side, int? roomCells = null, Func<string, FlyoutIcon?>? pictures = null)
    {
        if (targets is [var one])
        {
            return Settings(one.Item, picks, one.Devices, one.Place, side, roomCells, pictures);
        }
        static bool Folder(OptionTarget target) => (target.Item as BarItem)?.Folder is not null;
        var rows = OptionRows.Common([.. targets.Select(target => Folder(target) && side == FolderSide.Face ? Array.Empty<OptionRow>() : OptionRows.Of(target.Item, target.Devices, target.Place))]);
        // §529: each one's name, for a mixed row's own rows (OptionRow.Each).
        // Two of one name told apart by their place among them ("Clock (2)"): each one's row its own.
        List<string> said = [.. targets.Select(target => target.Item is { } item ? IslandEdits.Header(item) : "")];
        List<string> names = [.. said.Select((name, at) => said.Count(other => other == name) > 1 ? $"{name} ({said.Take(at + 1).Count(other => other == name)})" : name)];
        return Section(rows, picks, pictures, names);
    }

    /// <summary>
    /// Whether a pick of <paramref name="kind"/> keeps the menu open — every setting and every change to
    /// the faces, made where the menu stands — rather than closing it: only an action, asked first in
    /// a dialog of its own, and Windows' own dialogs close it (MenuKeeps.cs, §219).
    /// </summary>
    public static bool KeepsOpen(OptionPickKind kind) => kind is OptionPickKind.Flip or OptionPickKind.Step or OptionPickKind.Set
        or OptionPickKind.Field or OptionPickKind.AddFace or OptionPickKind.RemoveFace or OptionPickKind.MoveFace or OptionPickKind.Move;

    /// <summary>
    /// Whether <paramref name="item"/>'s rows need the adapters or the disks this PC has: a graphics or
    /// disk graph, or a folder wearing a graphics or disk face. Listing them asks the samplers, so only
    /// then.
    /// </summary>
    public static bool NeedsDevices(WidgetItem? item) =>
        item?.Info?.Metric is Metric.Gpu or Metric.Disk
        || (item as BarItem)?.Folder?.Faces.Any(face => face.Info.Options.Any(option => option.Kind is FaceOptionKind.Adapter or FaceOptionKind.Disk)) == true;
}
