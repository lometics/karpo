// menus: a widget's or a folder face's settings as rows a menu can show (2026-10-19, the person: "the
// settings for the processor graph, inline them into the context menu, all of them" and "in edit mode
// for the taskbar, you should inline the settings for the folder faces into the left click menu";
// docs\widgets.md §214).
//
// The one description of a widget's settings, and since §228 the only place they are set: the widget's
// menu on the bar (Karpo.Dock\OptionMenu.cs; Settings has no widget pages any more, the person,
// 2026-10-19: "the settings for the widgets in the main application should be removed"). Each row is
// read from the thing it sets — a widget, a face of a folder — and carries the way to set it there (Set):
//
//  - **Toggle** — on or off: a toggle switch at a menu row's end. A toggle with Move is one of an
//    ordered list (a toggles row's toggles): the menu shows the ones on in their order, dragged by
//    their grips, and the rest to add (§228).
//  - **Choice** — one of a few: a submenu of the choices with the one in force ticked; a folder's
//    alignment's four (Segmented) are buttons in its row instead (§253); a display's None a row of the level it
//    stands on in the edit bar's Display ▾ (Inline, §309, §361).
//  - **Number** — a whole number in a short range: a "− N +" stepper.
//  - **Text** — a line somebody types: a text field (§219).
//  - **Search** — one of many found by what is typed (a time zone, a place, a share, a feed): a search
//    field with its results under it (OptionFields.cs, §219).
//  - **Color, Glyph, Target, Time, Duration** — a colour, an icon, what a pin opens, a time of day and
//    a length: pickers (OptionPickers.cs, §224).
//  - **Action** — a row that does something once: a line's list or note deleted, asked first (Ask).
//    It, and a line of text, may stand greyed with why under it (Enabled, Why, §309).
//  - **Note** — words to read: a state and why, wrapped whole under its name (§339).
//  - **Link** — a setting another level owns, which this menu only leads to (§351, the person: "i would
//    rather not have that. i think in that case it would be better to link into the account settings
//    instead of duplicating them"): its owner's name, the arrow out at its end; picked, the menu closes
//    and the owner's menu opens at its level (OptionLink): a widget's "Manage accounts" ⚙ › Accounts, a
//    calendar's "Public holidays" ⚙ › Public holidays (§352). Every setting
//    is at one level — a widget's, a layout's, a display's, Karpo's — and set there alone.
//  - **The person's own things** (Owned, §317, §324) — a line's notes or to-do lists, the calendars of
//    public holidays, the accounts — are managed one way wherever they are: one row, its level the way
//    to add one at the top ("New note", "Add a country", "Add an account"), then each thing a row (under
//    the heading of where it comes from, where they come from several), its own settings its submenu, at
//    its end the buttons it allows — rename (F2), sign in again, delete (asked first). A choice's (a
//    line's note, its list) is picked by a click on its row and ticked where in force; Things are only
//    managed (the holidays, the accounts). Things put in order (a bar's layouts, §333) have a grip to
//    drag them by, and Add under them, as a folder's faces have; a click on one opens its own level, as
//    on a face, the one in force ticked (§336).
//
// Nothing leads to a page any more. A list this PC or the accounts have (the adapters, the drives, the
// To Do lists) is a choice of what the caller listed (OptionDevices), and no row where it listed none.
//
// A face's rows are its descriptor's (FolderFaces) and what it has beside them (FaceRows.Rows), each
// shown while its condition holds. A widget's are keyed by the names the settings app's widget page
// gave them, kept as they were. Its height has rows of its own in a folder line's menu
// (Karpo.Dock\LineHeightRows.cs).
//
// One order for every menu (2026-10-22, the person: "for all the widgets, go ahead and make sure that
// the configuration is implemented as similarly as possible, same order, same depth, same types of
// menus"; docs\widgets.md §292): every row says which part of the menu it belongs to (OptionSlot), and
// Of and OfFace lay the rows out by it — what it is of, what of it it shows, its style, the rest of its
// look, its words, where it stands, what it does — whatever order a widget's code adds them in. The kind
// of a row follows its value: on or off a toggle, named choices a level of them, free text a field's
// level, what is found by typing a search's level (OptionRowKind).

namespace Karpo.Core;

/// <summary>What kind of setting a row is, and so the control a menu gives it; see the top of the file.</summary>
public enum OptionRowKind
{
    Toggle,
    Choice,

    /// <summary>
    /// A number stepped: <see cref="OptionRow.Value"/> in its range. §333: one with <see cref="OptionRow.SetText"/>
    /// takes a number typed too, offered what <see cref="OptionRow.Finder"/> offers (a bar's size).
    /// </summary>
    Number,

    /// <summary>A line of text: <see cref="OptionRow.Text"/>, set by <see cref="OptionRow.SetText"/> (OptionFields.cs).</summary>
    Text,

    /// <summary>One of many, found by <see cref="OptionRow.Finder"/> and kept by <see cref="OptionRow.Pick"/> (OptionFields.cs).</summary>
    Search,

    // menus: pickers (OptionPickers.cs, §224).
    Color,
    Glyph,
    Target,
    Time,
    Duration,

    /// <summary>A row that does something once, set with 0: asked first where it has <see cref="OptionRow.Ask"/>.</summary>
    Action,

    /// <summary>§324: the person's own things managed in a level (<see cref="OptionRow.Owned"/>), none of them picked: the holiday calendars, the accounts.</summary>
    Things,

    /// <summary>
    /// A row opening a level of its own rows (<see cref="OptionRow.Level"/>), what it says at its end its
    /// <see cref="OptionRow.Summary"/>: the kinds of account to add, a mail server's form, an account's colours.
    /// </summary>
    Level,

    /// <summary>A heading over the rows after it, no row of its own: an account's calendars, its mail; one without a title a rule.</summary>
    Heading,

    /// <summary>
    /// §339: words to read and nothing to do — the licence's state, what counts, Karpo's version: its title,
    /// its <see cref="OptionRow.Why"/> under it wrapped whole, however long, its <see cref="OptionRow.Summary"/>
    /// at its end ("12 of 20"). Drawn as a row that may be used, never greyed, but nothing lights or picks it.
    /// </summary>
    Note,

    /// <summary>§351: a way to the level that owns a setting (<see cref="OptionRow.Link"/>), nothing set here; see the top of the file.</summary>
    Link,
}

/// <summary>§655: what an ordered list says of what it orders (<see cref="OptionRow.OrderedWords"/>).</summary>
/// <param name="Add">Its row over those off ("Add a section").</param>
/// <param name="Empty">What it says with none on.</param>
/// <param name="All">What its add row says with every one on.</param>
public sealed record OrderedWords(string Add, string Empty, string All);

/// <summary>§351: the menu and the level a <see cref="OptionRowKind.Link"/> row opens, the one place its setting is made (the dock's EditEntries lands each).</summary>
public enum OptionLink
{
    /// <summary>⚙ Settings › Accounts: every account, added, removed, synced, coloured.</summary>
    Accounts,

    /// <summary>⚙ Settings › Public holidays: the calendars every calendar shows, and their colour.</summary>
    Holidays,

    /// <summary>§567: the Appearance page, Karpo's one look; the folder room's right click's way there.</summary>
    Appearance,

    /// <summary>§628: "Add a layout"'s page of the layout presets, the Layouts page's way to add one (BarLooks.AddLayoutRows).</summary>
    AddLayout,

    /// <summary>
    /// §646: an account's "Mail folders" page, its inbox and every folder a switch (OptionAccountRows.Folders), the Accounts page's
    /// way there from the account's own rows; the link's own level is the page.
    /// </summary>
    MailFolders,
}

/// <summary>
/// §292: which part of a widget's menu a row belongs to, in the order the parts stand: every menu the
/// same (OptionRows.Of, OfFace). <see cref="Unset"/> is no part: a row must say its own.
/// </summary>
public enum OptionSlot
{
    Unset,

    /// <summary>What it is of: the place, the time zone, the symbol, the feed, the team, the drive, the reading, what a pin opens, the list, an alarm's time and days, a timer's length, the toggles and icons it shows.</summary>
    Subject,

    /// <summary>What of it it shows: the reading's adapter or disk, the amount owned, the chart's range, the distance, the index, the public holidays, which meeting, how a pin shows its folder.</summary>
    Scope,

    /// <summary>Its style, one row called Style (§270): drawings of the same data.</summary>
    Style,

    /// <summary>The rest of how it is drawn: the chart, the item size, the parts it shows, its name beside it, a colour.</summary>
    Look,

    /// <summary>Its own words and icon: a name, a label, a heading's text, a pin's icon.</summary>
    Words,

    /// <summary>Where it stands: a folder's alignment, which way a slider runs, how long it is.</summary>
    Placement,

    /// <summary>What it does: what a click does or turns on, alerts, when it shows, how often it updates.</summary>
    Behaviour,
}

/// <summary>
/// One setting of a widget or a face as it stands now, and the way to set it: <see cref="Set"/> takes
/// a toggle's 1 or 0, a choice's place in <see cref="Choices"/>, a number or an action's 0, and says
/// whether anything changed.
/// </summary>
/// <param name="Key">Which setting it is: a face option's key, a face row's kind, a widget's own name for it.</param>
public sealed partial record OptionRow(string Key, OptionRowKind Kind, string Title)
{
    /// <summary>§249: its icon, a Segoe Fluent Icons glyph (OptionGlyphs): every row has one.</summary>
    public string Glyph { get; init; } = "";

    /// <summary>§292: the part of the menu it stands in, which orders it among the others.</summary>
    public OptionSlot Slot { get; init; }

    /// <summary>
    /// §529: the <see cref="Key"/> of the choice this row only matters after (an agenda's Days ahead after its Style, a
    /// clock's Show seconds after its Style): the settings panel stands it under that row, nested; null for a row of its own.
    /// A key no row of its level has leaves it where it is.
    /// </summary>
    public string? Under { get; init; }

    /// <summary>A toggle's state.</summary>
    public bool On { get; init; }

    /// <summary>
    /// §452: a toggle's colour (an account's calendar's, inbox's, address book's): a colour row whose swatches a swatch of
    /// it before the switch opens in place, as a thing's colour does (<see cref="OptionThing.Colour"/>); null for none.
    /// </summary>
    public OptionRow? Colour { get; init; }

    /// <summary>A choice's words, in the order offered.</summary>
    public IReadOnlyList<string> Choices { get; init; } = [];

    /// <summary>
    /// §323: what the row says it is set to at its end for each of <see cref="Choices"/>, in their order,
    /// where a choice's words are a sentence that would be cut there ("Show in hidden icon menu" →
    /// "Behind arrow"); empty for the choices' own words.
    /// </summary>
    public IReadOnlyList<string> ChoicesSaid { get; init; } = [];

    /// <summary>
    /// What the row says at its end for choice <paramref name="at"/>: its <see cref="ChoicesSaid"/>, else its words; §411
    /// (R14): words that name a choice and then show it after a colon ("Simple: the conditions and the temperature")
    /// say the name alone.
    /// </summary>
    public string SaidOf(int at) => at < ChoicesSaid.Count ? ChoicesSaid[at] : Named(Choices[at]);

    /// <summary>§411: <paramref name="words"/> up to a colon and a space (or a full-width colon), else whole.</summary>
    private static string Named(string words) => words.IndexOf(": ", StringComparison.Ordinal) is > 0 and var at ? words[..at] : words.IndexOf('：') is > 0 and var wide ? words[..wide] : words;

    /// <summary>§249: each of <see cref="Choices"/>' icon, in their order; a choice past them has the row's own (<see cref="ChoiceGlyph"/>).</summary>
    public IReadOnlyList<string> ChoiceGlyphs { get; init; } = [];

    /// <summary>
    /// §472: a choice of what to add ("Add a layout"'s layout presets): none is in force, so its row says nothing at its end;
    /// a pick adds one and goes back to the list (R5).
    /// </summary>
    public bool Adds { get; init; }

    /// <summary>§472: each of <see cref="Choices"/>' line under its name, what it is for (a layout preset's); none for none.</summary>
    public IReadOnlyList<string> ChoiceDetails { get; init; } = [];

    /// <summary>
    /// §583: each of <see cref="Choices"/>' mark, a quieter word after its name in the list and, the one chosen, in the row's
    /// box (a palette holding the person's own edits: "Changed"); empty for none.
    /// </summary>
    public IReadOnlyList<string> ChoiceMarks { get; init; } = [];

    /// <summary>§429: each of <see cref="Choices"/>' look as a swatch in its icon's place (a palette's, a pattern's); none for none.</summary>
    public IReadOnlyList<LookSwatch> ChoiceSwatches { get; init; } = [];

    /// <summary>
    /// §634: its choices a grid of tiles, each its swatch (<see cref="ChoiceSwatches"/>) large enough to see what it is — a
    /// look's Pattern, each pattern on the section it would be drawn on —, never a list of names.
    /// </summary>
    public bool Tiled { get; init; }

    /// <summary>
    /// §556: a font's choices (a look's Font): each of <see cref="Choices"/>' font, which its words are drawn in; a menu lists
    /// them under their families, each its weight's name, a filter over them. None for any other choice.
    /// </summary>
    public IReadOnlyList<TextStyle> ChoiceFaces { get; init; } = [];

    /// <summary>§249: the icon of choice <paramref name="at"/>: its own, else the row's.</summary>
    public string ChoiceGlyph(int at) => at >= 0 && at < ChoiceGlyphs.Count && ChoiceGlyphs[at].Length > 0 ? ChoiceGlyphs[at] : Glyph;

    /// <summary>
    /// §411 (R13): whether each of <see cref="Choices"/> has a glyph of its own, none the same as another's: a
    /// menu shows the choices' icons then, and none at all else, rather than one icon on every choice.
    /// </summary>
    public bool ChoicesApart =>
        Choices.Count > 0 && Enumerable.Range(0, Choices.Count).Select(ChoiceGlyph).Distinct(StringComparer.Ordinal).Count() == Choices.Count
        && Enumerable.Range(0, Choices.Count).All(at => at < ChoiceGlyphs.Count && ChoiceGlyphs[at].Length > 0);

    /// <summary>Which of <see cref="Choices"/> is in force; -1 for none.</summary>
    public int Chosen { get; init; } = -1;

    /// <summary>
    /// §253: a choice of a few short ones, each with its icon (<see cref="ChoiceGlyphs"/>), picked in
    /// its row as Windows 11's segmented control picks rather than in a submenu: a folder's alignment
    /// (the person: "alignment left center right should not have a submenu, it should just show three
    /// buttons on the same level").
    /// </summary>
    public bool Segmented { get; init; }

    /// <summary>
    /// §521: a choice among things there are as many of as there are — the person's (the layouts a display shows) or the
    /// system's —, however few now: a dropdown, never side by side (the settings panel's segmented control is for a fixed
    /// two or three).
    /// </summary>
    public bool Grows { get; init; }

    /// <summary>A number's value and its range.</summary>
    public int Value { get; init; }

    public int Least { get; init; }

    public int Most { get; init; }

    /// <summary>How far a number's step goes: one, or the page's own step (the volume slider's four).</summary>
    public int Step { get; init; } = 1;

    /// <summary>§302: what a number is counted in, which its stepper says with it ("4 px", "70%"); none for a count its title names (Years).</summary>
    public OptionUnit Unit { get; init; }

    /// <summary>§290: what a number's stepper says in place of its figure ("Automatic"); null for the figure in its unit.</summary>
    public string? Said { get; init; }

    /// <summary>§302: what a number's stepper says: its <see cref="Said"/>, else its value in its <see cref="Unit"/>.</summary>
    public string Shown => Said ?? OptionUnits.Say(Unit, Value);

    /// <summary>
    /// The card several rows are one of, by its title: a toggles row's toggles, an alarm's days, the
    /// status icons, a folder's column widths — rows a menu may put in one submenu under it; null for
    /// a row of its own.
    /// </summary>
    public string? Group { get; init; }

    /// <summary>
    /// What a menu calls <see cref="Group"/> where the card's title says too little away from its page:
    /// "Toggles shown", "Repeat on" (OptionMenu's group); null for the card's title.
    /// </summary>
    public string? GroupTitle { get; init; }

    /// <summary>§249: the icon of a <see cref="Group"/>'s row, on its first row ("Repeat on", "Public holidays"); empty for the row's own.</summary>
    public string GroupGlyph { get; init; } = "";

    /// <summary>
    /// What a group's row says it is set to at its end, on its first row, where the rows' own reading
    /// says too little: the public holidays' "Off" or "Switzerland" (§240); null for the rows' own.
    /// </summary>
    public string? GroupSummary { get; init; }

    /// <summary>
    /// §240: a toggle that heads a search's own level, by the search's <see cref="Key"/>, rather than
    /// standing on its own: "Use my device's location" over a place's search, which stands greyed
    /// while it is on (OptionMenu). Null for a row of its own.
    /// </summary>
    public string? Within { get; init; }

    /// <summary>What a <see cref="OptionRowKind.Text"/> or <see cref="OptionRowKind.Search"/> row's field takes; <see cref="OptionField.None"/> for the rest.</summary>
    public OptionField Field { get; init; }

    /// <summary>
    /// A toggle of an ordered list (a toggles row's toggles): those on are the list, in its order, and
    /// those off the ones that may be added to its end.
    /// </summary>
    public bool Ordered { get; init; }

    /// <summary>
    /// §655: what an ordered list (<see cref="Ordered"/>) says of what it orders — its "Add a …" over those off, what it says
    /// with none on, and with every one on — on its first row; null for a toggles row's (Add a toggle).
    /// </summary>
    public OrderedWords? OrderedWords { get; init; }

    /// <summary>
    /// A toggle of an ordered list that is on: moves it to that place among the list's toggles that
    /// are on (dragged by its grip in the menu); a thing's move (<see cref="OptionThing.Move"/>, §333),
    /// to that place among the things. False where nothing moved. Null for every other row.
    /// </summary>
    public Func<int, bool>? Move { get; init; }

    /// <summary>What an <see cref="OptionRowKind.Action"/> asks before it acts, in Windows' own dialog; null for nothing.</summary>
    public DeleteQuestion? Ask { get; init; }

    /// <summary>
    /// §309: a choice whose choices stand as rows of the level it is on, ticked, under nothing and
    /// ruled off from the rows after them, rather than in a level of their own: a display's None (§361).
    /// §343, §361: the person's own things (<see cref="Owned"/>) whose level's rows stand on the level it
    /// is on: the layouts in the edit bar's Display ▾.
    /// </summary>
    public bool Inline { get; init; }

    /// <summary>§309: whether a text's or an action's row may be used now; greyed, with <see cref="Why"/> under it, where not.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>§309: why a row is greyed (<see cref="Enabled"/>), on a second line under its name; null for nothing said.</summary>
    public string? Why { get; init; }

    /// <summary>What tells a row apart, on a second line under its name where it is not greyed: a calendar's count of events, how a test went; null for none.</summary>
    public string? Detail { get; init; }

    /// <summary>§644: a colour as a swatch in its icon's place (an account's line in Show from: its inbox's), as a thing's (<see cref="OptionThing.Swatch"/>); null for its glyph.</summary>
    public LookSwatch? Swatch { get; init; }

    /// <summary>
    /// A <see cref="OptionRowKind.Text"/> row whose words are a secret (a password): its field draws a
    /// bullet for each letter, gives nothing to the clipboard and offers nothing; its <see cref="Text"/>
    /// is always empty, and what its row says at its end is its <see cref="Summary"/>.
    /// </summary>
    public bool Masked { get; init; }

    /// <summary>§351: where a <see cref="OptionRowKind.Link"/> row leads.</summary>
    public OptionLink Link { get; init; }

    /// <summary>A <see cref="OptionRowKind.Level"/> row's rows, in their order.</summary>
    public IReadOnlyList<OptionRow> Level { get; init; } = [];

    /// <summary>
    /// §536: the look the rows under it edit — §551, §585 the taskbar menu's expander (the menu itself, LookShown.Menu), §592 the
    /// Appearance's tabs (the whole look) —: the settings panel draws a sample of what it styles over them (Karpo.Dock's
    /// LookPreviews); null for any other row.
    /// </summary>
    public LookShown? Shows { get; init; }

    /// <summary>
    /// §411 (R1): a <see cref="OptionRowKind.Level"/> row's rows are a form (an account's server's): its lines of text
    /// stand as fields on the level itself, each saying what it takes, rather than each a level of its own.
    /// </summary>
    public bool Form { get; init; }

    /// <summary>
    /// §425: a row that is one of a choice of kinds, each opening a level of its own (a pin's Target's App, Folder,
    /// Website, …): ticked where it is the kind in force, saying what is set; else no tick and nothing at its end.
    /// Null for a row of no such choice.
    /// </summary>
    public bool? Ticked { get; init; }

    /// <summary>§425: a picture's address drawn with what the row says at its end (<see cref="Summary"/>): a pin's target's icon; null for none.</summary>
    public string? ValuePicture { get; init; }

    /// <summary>Sets the value where it was read from; false where nothing changed.</summary>
    public Func<int, bool>? Set { get; init; }

    /// <summary>A text's or a search's words as the field shows them at rest, a target's path, an icon's glyph: the setting as it is now.</summary>
    public string Text { get; init; } = "";

    /// <summary>
    /// §230: what a search's row says it is set to at its end, where not its <see cref="Text"/>: a zone's
    /// offset ("UTC+1", TimeZoneText); a <see cref="OptionRowKind.Things"/> row's, what it holds (the
    /// holidays' countries, §324); null for its text.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// §684: what a <see cref="OptionRowKind.Things"/> row says it holds at its end however many it holds, in place of their
    /// names (the taskbar menu: "12 entries"); null for their names.
    /// </summary>
    public string? Holds { get; init; }

    /// <summary>What an empty field says.</summary>
    public string Placeholder { get; init; } = "";

    /// <summary>
    /// §411 (R4): what applies where a <see cref="OptionRowKind.Text"/> or <see cref="OptionRowKind.Search"/> row
    /// holds nothing, said greyed at its row's end; null for its <see cref="Placeholder"/> (a line's default name), a
    /// search's "None". A placeholder that only shows what may be typed ("Tokyo, Home, …") is no default.
    /// </summary>
    public string? Empty { get; init; }

    /// <summary>The most a text may be, in UTF-16 units, as the page's box allows.</summary>
    public int Longest { get; init; } = int.MaxValue;

    /// <summary>Sets a text where it was read from (trimmed and held as the page holds it), a target to the path Windows' open dialog chose, a number typed (§333); false where nothing changed.</summary>
    public Func<string, bool>? SetText { get; init; }

    /// <summary>A search's finder: what it offers for what is typed, and how a pick is proved.</summary>
    public OptionFinder? Finder { get; init; }

    /// <summary>Keeps a search's pick, proved, where it was read from; false where nothing changed.</summary>
    public Func<OptionFound, bool>? Pick { get; init; }

    /// <summary>
    /// §317, §324: the person's own things this row manages in its level (see the top of the file): a
    /// choice's (a line's note or list; its <see cref="Choices"/> the things' names, in their order) or a
    /// <see cref="OptionRowKind.Things"/> row's; null for a choice of what there is.
    /// </summary>
    public OptionOwned? Owned { get; init; }
}

/// <summary>
/// §317, §324: the level of the person's own things (<see cref="OptionRow.Owned"/>): <paramref name="Add"/>
/// — an action making one ("New note"), a search ("Add a country"), a choice of kinds ("Add an account")
/// —, null for none; and <paramref name="Things"/>, in their order. §333: where they are put in order
/// (<see cref="Ordered"/>, a bar's layouts) Add is the list's last row, where the new one lands, as "Add
/// a face" is (§314); else the level's first.
/// </summary>
public sealed record OptionOwned(OptionRow? Add, IReadOnlyList<OptionThing> Things)
{
    /// <summary>§411 (R9): more ways to add one, under <see cref="Add"/>: an account of each kind; none for none.</summary>
    public IReadOnlyList<OptionRow> AlsoAdd { get; init; } = [];

    /// <summary>§333: the rows under the things and Add, ruled off from them: the holiday calendars' colour; none for none.</summary>
    public IReadOnlyList<OptionRow> After { get; init; } = [];

    /// <summary>§333: whether the things are dragged into order (<see cref="OptionThing.Move"/>), and Add stands under them.</summary>
    public bool Ordered => Things.Any(thing => thing.Move is not null);

    /// <summary>Every row of it, for a pick to find its own by its key (<see cref="OptionRows.Flat"/>).</summary>
    public IEnumerable<OptionRow> All => [.. Add is null ? [] : OptionRows.Flat([Add]), .. OptionRows.Flat(AlsoAdd), .. Things.SelectMany(thing => thing.All), .. OptionRows.Flat(After)];
}

/// <summary>
/// §324: one of the person's own things in its level (<see cref="OptionOwned"/>): its name and icon, and
/// what it allows, each a row its button stands for; none where it does not allow it.
/// </summary>
public sealed record OptionThing(string Title, string Glyph)
{
    /// <summary>What tells it apart, on a second line under its name: a calendar's region, an account's kind; null for none.</summary>
    public string? Summary { get; init; }

    /// <summary>
    /// §638: what it holds, said at its row's end — on the settings panel at its expander's head while it is shut (an account's
    /// items synced, "1,234 events · 210 contacts"); null for nothing said.
    /// </summary>
    public string? Holds { get; init; }

    /// <summary>§595: a word in a badge after its name, what sets it apart among its own (the default layout's "Default"); null for none.</summary>
    public string? Badge { get; init; }

    /// <summary>§429: its look as a swatch in its icon's place (a style's); null for its glyph.</summary>
    public LookSwatch? Swatch { get; init; }

    /// <summary>Where it comes from, a heading over the run of things from there ("On this PC", an account); null where they come from one place.</summary>
    public string? Heading { get; init; }

    /// <summary>Its own settings, its submenu (a holiday calendar's region); empty for none.</summary>
    public IReadOnlyList<OptionRow> Rows { get; init; } = [];

    /// <summary>
    /// §336: ticked where it is the one in force though its row is no choice — a click opens its own
    /// level (the layout a display shows); null for no tick. A choice's things are ticked by its choice.
    /// </summary>
    public bool? Ticked { get; init; }

    /// <summary>Its rename: a line of text, its name's field (F2); null where it cannot be renamed.</summary>
    public OptionRow? Name { get; init; }

    /// <summary>
    /// Its own actions, each a button at its row's end before its colour and its delete: an account's Sign in again, §551 a
    /// layout's Duplicate; none for none.
    /// </summary>
    public IReadOnlyList<OptionRow> Acts { get; init; } = [];

    /// <summary>
    /// §423: its colour (a holiday calendar's), a colour row whose swatches a button at its row's end opens in place, as
    /// its rename's field opens, beside its bin: its row keeps opening its own (its region's choices, §411 R1); null for none.
    /// </summary>
    public OptionRow? Colour { get; init; }

    /// <summary>Its delete: an action, asked first (<see cref="OptionRow.Ask"/>), greyed where it may not be now (the last layout); null where it cannot be deleted.</summary>
    public OptionRow? Delete { get; init; }

    /// <summary>
    /// §333: its move among the things, dragged by its grip: a row whose <see cref="OptionRow.Move"/> puts
    /// it at that place among them; null where they keep the order they have.
    /// </summary>
    public OptionRow? Move { get; init; }

    /// <summary>
    /// §366: what a click on it does where it has no level of its own: an action, the menu staying (a
    /// display's None among its layouts, which shows none there); null for none. Among things dragged into
    /// order one with none of its own (<see cref="Move"/> null) stands in their columns, never moved.
    /// </summary>
    public OptionRow? Pick { get; init; }

    /// <summary>Every row of it.</summary>
    public IEnumerable<OptionRow> All => [.. OptionRows.Flat(Rows), .. new[] { Name }.OfType<OptionRow>(), .. Acts, .. new[] { Colour, Delete, Move, Pick }.OfType<OptionRow>()];
}

/// <summary>
/// §302: what a number row's value is counted in (the person: "make sure that all properties that
/// have a unit also have it in the ui"). <see cref="None"/> is a count whose row's title names what it
/// counts ("Years").
/// </summary>
public enum OptionUnit
{
    None,

    /// <summary>Pixels, as Windows' Settings says a length of the screen: "4 px" (DIPs, as Settings' are).</summary>
    Pixels,

    /// <summary>Per cent: "70%".</summary>
    Percent,

    /// <summary>§496: days, "7 days": how far ahead an agenda lists.</summary>
    Days,
}

/// <summary>§302: a number in its unit, in the words Karpo speaks: the one way every number row says it.</summary>
public static class OptionUnits
{
    /// <summary><paramref name="value"/> in <paramref name="unit"/>: "4 px", "70%", "7 days", "3".</summary>
    public static string Say(OptionUnit unit, int value) => unit switch
    {
        OptionUnit.Pixels => S.SettingsLayoutsSizeValue(value),
        OptionUnit.Percent => S.SettingsAppearanceOpacityValue(value),
        OptionUnit.Days => S.SettingsWidgetCalendarDaysAheadValue(value),
        _ => value.ToString(System.Globalization.CultureInfo.CurrentCulture),
    };

    /// <summary>
    /// §521: what a number box's words typed in place (<paramref name="typed"/>) set <paramref name="row"/> to: the whole number
    /// in them — its unit's words, a sign or spaces round it no matter ("48 px", " 70% ", "-3") — held to the row's range
    /// (<see cref="OptionRow.Least"/> to <see cref="OptionRow.Most"/>) and to its steps from its least; null for no number,
    /// or the value it has (nothing to set). WinUI's NumberBox, its value coerced to its range.
    /// </summary>
    public static int? Typed(OptionRow row, string typed)
    {
        var digits = new string([.. typed.SkipWhile(c => !char.IsAsciiDigit(c) && c != '-').TakeWhile(c => char.IsAsciiDigit(c) || c == '-')]);
        if (!long.TryParse(digits, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var number))
        {
            return null;
        }
        var held = (int)Math.Clamp(number, row.Least, row.Most);
        var step = Math.Max(1, row.Step);
        held = Math.Clamp(row.Least + (int)Math.Round((held - row.Least) / (double)step, MidpointRounding.AwayFromZero) * step, row.Least, row.Most);
        // §528: several widgets mixed take it whatever the first says.
        return held == row.Value && !row.Mixed ? null : held;
    }
}

/// <summary>What a text or a search row's field takes (OptionFields.cs, §219).</summary>
public enum OptionField
{
    None,

    /// <summary>A line of text: a label, a name, a heading, a feed's address, an amount owned, a pin's path.</summary>
    Text,

    /// <summary>A place searched for: the weather's.</summary>
    Place,

    /// <summary>A Windows time zone, searched for among hundreds.</summary>
    Zone,

    /// <summary>A share, fund, index or commodity, searched for: a markets widget's symbol.</summary>
    Symbol,

    /// <summary>An item searched for in a list of many: an actions line's page of Windows' Settings, an icon of the gallery.</summary>
    Search,
}

/// <summary>The graphics adapters and the disks this PC has, where a caller listed them; null for not listed.</summary>
public sealed record OptionDevices(IReadOnlyList<string>? Adapters = null, IReadOnlyList<string>? Disks = null)
{
    /// <summary>§655: opens an address of Windows' (a page of its Settings) for a row that links there; null where nothing opens (a test).</summary>
    public Action<string>? Open { get; init; }

    /// <summary>drives: the drives attached, of every kind (<see cref="DriveTargets.Attached"/>); null for not listed.</summary>
    public IReadOnlyList<DriveVolume>? Drives { get; init; }

    /// <summary>notes: the accounts' To Do lists a to-do line may show beside the notebook's (<see cref="TodoSource"/>); null for not listed.</summary>
    public IReadOnlyList<TodoSource>? TodoSources { get; init; }

    /// <summary>
    /// §455: the parts of the accounts a calendar, a mail or a contacts widget may show from (<see cref="WidgetSources"/>), as
    /// the dock last synced them; null for not listed.
    /// </summary>
    public IReadOnlyList<SourcePart>? Parts { get; init; }

    /// <summary>§324: where an account's To Do list is renamed and deleted (Microsoft To Do); null where they are only read.</summary>
    public ITodoLists? TodoLists { get; init; }

    /// <summary>notes: the notebook, whose lists and notes a line shows; null for not read.</summary>
    public NotebookFile? Notebook { get; init; }

    /// <summary>notes: the notebook a line's list or note is made, renamed and deleted in (§228); null for none, and then no such rows.</summary>
    public Notebook? Book { get; init; }

    /// <summary>alarm: the first day of the week as Karpo shows it; null for the region's.</summary>
    public DayOfWeek? FirstDay { get; init; }

    /// <summary>The zones the person's clocks show, most used first (<see cref="OptionFields.ZonesInUse"/>), which a zone's search offers first; null for none.</summary>
    public IReadOnlyList<string>? Zones { get; init; }

    /// <summary>§230: the places the person's weather and air quality show (<see cref="OptionFields.PlacesInUse"/>), which a place's search offers before anything is typed; null for none.</summary>
    public IReadOnlyList<Place>? Places { get; init; }

    /// <summary>§230: the shares the person's markets widgets show (<see cref="OptionFields.SharesInUse"/>), which a share's search offers before anything is typed; null for none.</summary>
    public IReadOnlyList<Listing>? Shares { get; init; }

    /// <summary>§261: the icons the tray has now, which its rows set where each shows (<see cref="OptionTray"/>); null for not listed.</summary>
    public IReadOnlyList<TrayMenuIcon>? TrayIcons { get; init; }

    /// <summary>
    /// §292, §380: what a pin's and a folder's contents' "What it opens" offers and how it names a
    /// target (<see cref="TargetChoices"/>); null for nothing but what is typed.
    /// </summary>
    public TargetChoices? Targets { get; init; }

    /// <summary>§304: what the person's widgets hold in each field of text, by its row's key (<see cref="WordsInUse.Of"/>), which the same field offers; null for none.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<OptionFound>>? Words { get; init; }

    /// <summary>The list for <paramref name="kind"/>: the adapters, the disks; null for another kind or not listed.</summary>
    public IReadOnlyList<string>? Of(FaceOptionKind kind) => kind switch
    {
        FaceOptionKind.Adapter => Adapters,
        FaceOptionKind.Disk => Disks,
        _ => null,
    };
}

/// <summary>A widget's and a face's settings as rows; see the top of the file.</summary>
public static partial class OptionRows
{
    // A widget's rows by the names the settings app's widget page gave them (gone since §228).

    /// <summary>The style, of a widget with styles: how it shows what it shows (<see cref="WidgetStyles.LookNames"/>).</summary>
    public const string Style = "StyleIndex";

    /// <summary>§529: the key a group's row (<see cref="OptionRow.Group"/>, "Show from") goes by, so a row may stand under it (<see cref="OptionRow.Under"/>).</summary>
    public static string GroupKey(string group) => "group:" + group;

    /// <summary>perf: what it measures, of a widget with readings (<see cref="WidgetStyles.ReadingNames"/>).</summary>
    public const string Reading = "ReadingIndex";

    /// <summary>perf: the graphics graph's adapter.</summary>
    public const string PerformanceAdapter = "PerformanceAdapterIndex";

    /// <summary>perf: the disk graph's disk.</summary>
    public const string PerformanceDisk = "PerformanceDiskIndex";

    /// <summary>perf: how often it steps.</summary>
    public const string PerformancePace = "PerformancePace";

    /// <summary>
    /// The settings <paramref name="item"/> has: its reading where it has readings, its style where it
    /// has styles (the figure alone among a reading's, §228), then its kind's own (OptionRows.Lines).
    /// The lists are <paramref name="devices"/>' (no row where one was not listed). Empty for nothing.
    /// Its pickers with them (OptionPickers.Into): an alarm's
    /// time and a countdown's length as steppers, a pin's target and icon, and with <paramref name="place"/>
    /// its card's colours.
    /// </summary>
    public static IReadOnlyList<OptionRow> Of(WidgetItem? item, OptionDevices? devices = null, OptionPlace? place = null)
    {
        if (item is null)
        {
            return [];
        }
        var rows = new List<OptionRow>();
        if (item is CellItem line && WidgetStyles.Has(line))
        {
            // perf: what it measures first, then how it shows it; the two never change each other.
            if (WidgetStyles.HasReadings(line))
            {
                rows.Add(new OptionRow(Reading, OptionRowKind.Choice, S.SettingsWidgetReadingHeader)
                {
                    Glyph = OptionGlyphs.Reading,
                    Slot = OptionSlot.Subject,
                    Choices = WidgetStyles.ReadingNames(line),
                    ChoiceGlyphs = WidgetStyles.ReadingGlyphs(line),
                    Chosen = WidgetStyles.ReadingIndex(line),
                    Set = at => WidgetStyles.ChooseReading(line, at),
                });
            }
            // §318: a choice of one is none (the performance details, a reading each in one style).
            if (WidgetStyles.LookNames(line) is { Count: > 1 } looks)
            {
                rows.Add(new OptionRow(Style, OptionRowKind.Choice, S.SettingsWidgetStyleHeader)
                {
                    Glyph = OptionGlyphs.Style,
                    Slot = OptionSlot.Style,
                    Choices = looks,
                    ChoiceGlyphs = WidgetStyles.LookGlyphs(line),
                    Chosen = WidgetStyles.LookIndex(line),
                    Set = at => WidgetStyles.ChooseLook(line, at),
                });
            }
        }
        Lines(item, devices, rows, place);
        if (item.Pin is { } pin)
        {
            Pin(item, pin, devices?.Targets, rows);
        }
        // tray: where each icon shows, the tray's own (OptionTray.cs, §261, §347).
        OptionTray.Into(item, devices, rows);
        // menus: its pickers (OptionPickers.cs, §224).
        OptionPickers.Into(item, place, rows);
        return Laid(rows);
    }

    /// <summary>§292: <paramref name="rows"/> in the menu's one order, by their parts (<see cref="OptionSlot"/>), as added within a part.</summary>
    private static List<OptionRow> Laid(List<OptionRow> rows) => [.. rows.OrderBy(row => row.Slot)];

    /// <summary>
    /// The settings of <paramref name="folder"/>'s face at <paramref name="index"/>
    /// (<see cref="FaceRows.Rows"/>, each while it shows): its options
    /// by their kinds, the weather's device switch and
    /// its place (OptionFields), the markets face's own rows (MarketRows), an icon face's icon
    /// (OptionPickers.Folder); with <paramref name="place"/> the calendar face's public holidays
    /// (OptionHolidays, the person's for every calendar). Empty for no face there.
    /// </summary>
    public static IReadOnlyList<OptionRow> OfFace(FolderOptions folder, int index, OptionDevices? devices = null, OptionPlace? place = null)
    {
        if (index < 0 || index >= folder.Faces.Count)
        {
            return [];
        }
        var face = folder.Faces[index];
        var info = face.Info;
        var rows = new List<OptionRow>();
        foreach (var row in FaceRows.Rows(info))
        {
            if (!FaceRows.Shows(row, face))
            {
                continue;
            }
            // markets: its search, then the rows what it tracks has, as its line's (MarketRows, §258).
            if (row.Kind == FaceRowKind.Market)
            {
                rows.AddRange(MarketRows.Of(face.Market ?? MarketOptions.New(), () => MarketFaces.Kept(face), devices?.Shares, face: true, devices?.Words));
                continue;
            }
            var made = row.Kind switch
            {
                FaceRowKind.Option => OfOption(face, row.Option!, devices),
                // At the top of its place's level (§240).
                FaceRowKind.Device => new OptionRow("device", OptionRowKind.Toggle, S.SettingsWidgetWeatherDeviceHeader)
                {
                    Glyph = OptionGlyphs.Device,
                    Slot = OptionSlot.Subject,
                    On = FaceRows.UsesDevice(face),
                    Within = OptionFields.FacePlace,
                    Set = on => Changed(FaceRows.UsesDevice(face), on != 0, value => FaceRows.UseDevice(face, value)),
                },
                // An icon face's icon is what it shows.
                FaceRowKind.Icon => OptionPickers.Folder(face) with { Slot = OptionSlot.Subject },
                // §477: Inherited from the folder's line, or its own through the line's picker.
                FaceRowKind.Source => FaceSources.Row(folder, face, devices),
                // The place is searched for (OptionFields.cs, §219).
                _ => OptionFields.OfPlace(face, devices?.Places),
            };
            if (made is not null)
            {
                rows.Add(made);
            }
        }
        // §324, §351, §529: the way to the accounts of a face that shows what they hold, the calendar's holidays in its words (OptionAccountRows.cs).
        OptionAccountRows.Into(face, place?.Config.Holidays, rows);
        return Laid(rows);
    }

    /// <summary>Sets the row <paramref name="key"/> of <paramref name="rows"/> to <paramref name="value"/>; false where there is none, it cannot be set, or nothing changed.</summary>
    public static bool Apply(IReadOnlyList<OptionRow> rows, string key, int value) =>
        Find(rows, key) is { Set: { } set } && set(value);

    /// <summary>The row <paramref name="key"/> of <paramref name="rows"/>, or of a level of the person's own things among them (<see cref="OptionRow.Owned"/>); null for none.</summary>
    public static OptionRow? Find(IEnumerable<OptionRow> rows, string key) => Flat(rows).FirstOrDefault(row => row.Key == key);

    /// <summary>
    /// <paramref name="rows"/>, each followed by its colour (<see cref="OptionRow.Colour"/>, §452), the rows of its level of the person's own things
    /// (<see cref="OptionRow.Owned"/>, §317, §324) and of its own level (<see cref="OptionRow.Level"/>), in their order.
    /// </summary>
    public static IEnumerable<OptionRow> Flat(IEnumerable<OptionRow> rows) =>
        rows.SelectMany(row => (IEnumerable<OptionRow>)[row, .. row.Colour is { } colour ? [colour] : Array.Empty<OptionRow>(), .. row.Owned?.All ?? [], .. Flat(row.Level)]);

    /// <summary>A face's option as a row, by its kind; null for an adapter or a disk not listed.</summary>
    private static OptionRow? OfOption(FolderFace face, FaceOption option, OptionDevices? devices)
    {
        var info = face.Info;
        var title = info.TitleOf(option);
        switch (option.Kind)
        {
            case FaceOptionKind.Switch:
                return new OptionRow(option.Key, OptionRowKind.Toggle, title)
                {
                    Glyph = option.Glyph,
                    Slot = option.Slot,
                    On = face.Switch(option),
                    Set = on => Changed(face.Switch(option), on != 0, value => face.Set(option, value)),
                };
            case FaceOptionKind.Choice:
                return new OptionRow(option.Key, OptionRowKind.Choice, title)
                {
                    Glyph = option.Glyph,
                    Slot = option.Slot,
                    Choices = [.. option.Choices.Select(word => info.ChoiceTitle(option, word))],
                    ChoiceGlyphs = option.ChoiceGlyphs,
                    Chosen = option.Choices.ToList().IndexOf(face.Text(option)),
                    Set = at => at >= 0 && at < option.Choices.Count && Changed(face.Text(option), option.Choices[at], value => face.Set(option, value)),
                };
            case FaceOptionKind.Pace:
            {
                var paces = PerformanceChoices.Paces;
                return new OptionRow(option.Key, OptionRowKind.Choice, title)
                {
                    Glyph = OptionGlyphs.Pace,
                    Slot = option.Slot,
                    Choices = [.. paces.Select(PerformanceChoices.Pace)],
                    ChoiceGlyphs = [.. paces.Select(OptionGlyphs.PaceOf)],
                    Chosen = PerformanceChoices.FacePaceIndex(face.Text(option)),
                    Set = at => at >= 0 && at < paces.Length && Changed(face.Text(option), PerformanceChoices.FacePace(at), value => face.Set(option, value)),
                };
            }
            case FaceOptionKind.Adapter or FaceOptionKind.Disk:
                return Device(option.Key, title, option.Kind == FaceOptionKind.Adapter ? OptionGlyphs.Graphics : OptionGlyphs.Disk, devices?.Of(option.Kind), attached =>
                {
                    var choices = PerformanceChoices.FaceDevices(option.Kind, attached, face.Text(option));
                    return (choices, PerformanceChoices.FaceDeviceIndex(option.Kind, choices, face.Text(option)),
                        at => Changed(face.Text(option), PerformanceChoices.FaceDevicePicked(option.Kind, choices, at), value => face.Set(option, value)));
                });
            // A line of text, a time zone among hundreds: typed and searched for (OptionFields.cs, §219).
            case FaceOptionKind.Text:
                return OptionFields.OfText(face, option, title, devices?.Words) with { Glyph = option.Glyph.Length > 0 ? option.Glyph : OptionGlyphs.Name, Slot = option.Slot };
            default:
                return OptionFields.OfZone(face, option, title, devices?.Zones);
        }
    }

    /// <summary>
    /// An adapter's or a disk's row: a choice of <paramref name="attached"/> as <paramref name="make"/>
    /// lists it; none where they were not listed.
    /// </summary>
    private static OptionRow? Device(string key, string title, string glyph, IReadOnlyList<string>? attached, Func<IReadOnlyList<string>, (List<string> Choices, int Chosen, Func<int, bool> Set)> make)
    {
        if (attached is null)
        {
            return null;
        }
        var (choices, chosen, set) = make(attached);
        return new OptionRow(key, OptionRowKind.Choice, title)
        {
            Glyph = glyph,
            // What of the reading it shows (§292).
            Slot = OptionSlot.Scope,
            Choices = choices,
            ChoiceGlyphs = OptionGlyphs.Each(glyph, choices.Count),
            Chosen = chosen,
            Set = at => at >= 0 && at < choices.Count && set(at),
        };
    }

    /// <summary>Writes <paramref name="next"/> where it is not <paramref name="now"/>; whether it did.</summary>
    private static bool Changed<T>(T now, T next, Action<T> write)
    {
        if (EqualityComparer<T>.Default.Equals(now, next))
        {
            return false;
        }
        write(next);
        return true;
    }
}
