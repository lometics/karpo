// Ready-made groups (2026-10-05: "in the add widget menu there should also be another additional
// section where you can add precompiled groups of stuff. for example a list folder which contains
// all the system widgets already, or a list folder which shows a list of stocks already").
//
// A preset is a group somebody would otherwise put together widget by widget: a list folder of the
// system's graphs, one of a few shares, the notification center. The add list offers
// each in its subject beside the widgets it is made of (AddTree.cs), and one pick adds the whole group at the "+"
// that was pressed (Karpo.Dock\AddList.cs).
//
// The recipe is **data**, declared once here, and the one act that turns it into widgets is here
// too, so the dock's add list and the settings app — if it ever offers them — cannot make two
// different things of one preset. A preset is never stored: what is stored is what it makes, an
// ordinary folder of ordinary widgets, so no file shape changes.
//
// **One folder, always** (2026-10-05: "the ready made widgets should only ever add one folder with
// multiple widgets inside"). Every preset is a list folder, and adding one adds that
// one folder with its widgets inside, never its widgets loose. Connectivity was the exception, a
// row of loose taskbar entries (the network and Bluetooth buttons have no form a folder holds), and
// put four buttons on the bar; it is a list folder of the connections a folder can hold now.
//
// Where a preset is offered (On): **on the bar alone**, in any of its sections. A folder's "+" does
// not offer them: folders do not nest, so a group there could only be its widgets loose, which is
// what the person ruled out.
//
// A preset's folder **shows its icon** on the bar (the Icon face, FolderFaces.Icon), each its own glyph — the
// network's for Connectivity, a chart for Stocks — rather than its first widgets' icons in a 2×2,
// which for a group made at once is four icons nobody chose (2026-10-05: "the ready made folders
// should also get an icon per default and not show the multiple icon ones"); the 2×2 is no face at
// all since 2026-10-20 (§254). A preset may name another face (Preset.Face): the
// clock's is the clock face, its time and its date; the calendar's the date; the weather's the
// conditions and the temperature; the network's, Bluetooth's and the sound's their status glyphs;
// the removable devices' the stick; the timer's, the focus session's and the stopwatch's what their
// line in the folder has left or has counted; the mobile hotspot's its glyph and how many are on it;
// the battery's the charge; the power button's its glyph; the desktops' the name of the desktop
// you are on; the mail's the envelope and its count;
// the media's what is playing; a performance reading's its figure; the stock's its ticker; the
// news its newest headline; the drive's and the network drive's the drive and how full it is; the
// notifications' the bell and how many are new; the keyboard's the language and region in use; the
// VPN's the shield, lit while a connection is up.
//
// What this PC has decides two things, read when the pick is made (PresetMachine): the battery's
// line is left out of System and of the battery preset where there is no battery, and Storage has a line for each fixed drive.
//
// **Dividers** (2026-10-08: "add proper dividers for the ready made widget groups"). A list group
// parts its lines where they are two things: the network's activity from the switches, the fixed
// drives from what is plugged in, the focus lines from the calendar. A divider splits the list into
// two cards (DividerTile, 2026-10-09), so it stands **only where a split clearly helps** (the
// person, the same day) and nowhere it would only cut one thing in two. The person's own clock
// (2026-10-11: "the clock layout i already changed on the given list, get it from there, design the
// other ones in a similar fashion") is one card of related lines with its footer in it: so a
// footer (an actions line) stands under the lines it is about with no divider, and lines of one
// subject — a forecast's three views — are one card. The person's own arrangement of every preset (2026-10-11: "i changed the layouts on the layout page already. so take them over to the presets") is what each makes now, line for line (PersonsPresetsTests). It stands only between two groups: one whose group this PC lacks
// (Storage's fixed drives) is left out with it, so none ever stands first, last or next to another
// (ItemsOn). A group of one kind of thing, the Watchlist's shares, has none.
//
// Quick settings and the Widgets board were grid presets, and went with the grid folder
// (2026-10-09).
//
// Presets died once (2026-09-26, "faces and presets go"): they rebuilt the panels as folders, and
// the panels went back to being flyouts. These are not panels. They are groups somebody asked for,
// added as the folders anybody could have made.

namespace Karpo.Core;

/// <summary>What a widget of a preset needs of this PC to be put in at all.</summary>
public enum PresetNeed
{
    /// <summary>Nothing: it is always put in.</summary>
    None,

    /// <summary>A battery: the battery's line has nothing to show on a PC without one.</summary>
    Battery,

    /// <summary>power: sleep in Windows' power menu, which a PC that cannot sleep, or whose owner took it out, lacks.</summary>
    Sleep,

    /// <summary>power: hibernate in Windows' power menu, which is there only where somebody turned it on.</summary>
    Hibernate,

    /// <summary>quick: a screen that turns with the PC, the only kind Windows has a rotation lock tile for.</summary>
    Rotation,
}

/// <summary>
/// One widget of a preset: its word, where it stands and how big it is, and what it is set to.
/// </summary>
public sealed record PresetItem
{
    /// <summary>
    /// The widget's word ("cpu"), a list widget: the lines stand in the order they are given.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// The style of a widget with styles ("week" of "calendar", <see cref="WidgetStyles"/>); null for
    /// its first style, and for a widget of one style.
    /// </summary>
    public string? Style { get; init; }

    /// <summary>What a widget with readings measures ("gpu" of "performance", <see cref="WidgetStyles"/>); null for any other.</summary>
    public string? Reading { get; init; }

    /// <summary>The list widget the line is: <see cref="Kind"/> in <see cref="Style"/> of <see cref="Reading"/> (<see cref="WidgetStyles.FormWord"/>).</summary>
    public string Form => WidgetStyles.FormWord(Kind, Style, Reading);

    /// <summary>
    /// list: the line's height (<see cref="LineHeight"/>); null for where a new line of its kind
    /// starts (<see cref="LineHeight.StartFor"/>).
    /// </summary>
    public LineHeight? Height { get; init; }

    /// <summary>list, §432: the line's size, for a kind of a few forms (<see cref="ListWidget.Forms"/>); null for where its kind starts.</summary>
    public LineSize? Size { get; init; }

    /// <summary>stocks: the symbol it follows, so it shows a price at once rather than asking for one.</summary>
    public string? Symbol { get; init; }

    /// <summary>stocks: what it is called instead of the symbol ("S&amp;P 500" for ^GSPC); clock: the city after the time.</summary>
    public string? Label { get; init; }

    /// <summary>clock: the Windows time zone it shows ("Tokyo Standard Time"); null for this PC's.</summary>
    public string? Zone { get; init; }

    /// <summary>
    /// The premade widget it is (<see cref="PremadeWidgets"/>), which sets it up; null for a new line
    /// of <see cref="Kind"/>. A line made with <see cref="Of"/> is the record the add list offers.
    /// </summary>
    public PremadeWidget? Premade { get; init; }

    /// <summary>What this PC must have for the widget to be put in (<see cref="PresetNeed"/>).</summary>
    public PresetNeed Needs { get; init; }

    /// <summary>drives: one of it for each fixed drive of this PC, in letter order, each pointed at its drive.</summary>
    public bool EachFixedDrive { get; init; }

    /// <summary>
    /// drives: a folder's contents line (<see cref="WidgetInfos.FolderContents"/>, §380),
    /// pointed at the root of the drive it is made for (<see cref="EachFixedDrive"/>), or at nothing
    /// yet: the drive's line before it gives it its drive's once one is chosen (DriveTargets.SpreadContents).
    /// </summary>
    public bool DriveContents { get; init; }

    /// <summary>toggles: the toggles its row holds, in their order; null for a new row's (<see cref="Karpo.Core.Toggles.Default"/>).</summary>
    public IReadOnlyList<Toggle>? Toggles { get; init; }

    /// <summary>
    /// list: the line heads a list of its own, to the right of the one before (a folder's next
    /// column, <see cref="FolderOptions.Lists"/>); the lines after it stand under it.
    /// </summary>
    public bool NewList { get; init; }

    /// <summary>
    /// §620: list: the line's height in the one list a side bar's folder has (<see cref="Preset.Stacked"/>), where it differs
    /// from <see cref="Height"/>: a height given only so two lists side by side end level; null for <see cref="Height"/>.
    /// </summary>
    public LineHeight? SideHeight { get; init; }

    /// <summary>§620: the line in a side bar's one list: no list of its own, at its <see cref="SideHeight"/> where it has one.</summary>
    internal PresetItem Down() => this with { NewList = false, Height = SideHeight ?? Height, SideHeight = null };

    /// <summary>
    /// list: a divider (DividerTile) between two groups of the list's lines, the one a person would
    /// put there.
    /// </summary>
    public static readonly PresetItem Divider = new() { Kind = WidgetInfos.Divider.Id };

    /// <summary>A line that is <paramref name="premade"/>: its widget, set up as it is.</summary>
    public static PresetItem Of(PremadeWidget premade) => new() { Kind = premade.Kind.Id(), Premade = premade };

    /// <summary>Whether it is a list's divider rather than a widget.</summary>
    public bool IsDivider => string.Equals(Kind, WidgetInfos.Divider.Id, StringComparison.Ordinal);
}

/// <summary>
/// A ready-made group: a folder and the widgets in it. One record per preset, in
/// <see cref="Presets.All"/>; adding another is adding a record.
/// </summary>
public sealed record Preset
{
    /// <summary>Its name in code and in a test: never a widget's word, so the two cannot be mistaken.</summary>
    public required string Id
    {
        get => id;
        init
        {
            id = value;
            titleKey = $"core.preset.{value}.title";
            blurbKey = $"core.preset.{value}.blurb";
            keywordsKey = $"core.preset.{value}.keywords";
        }
    }

    private readonly string id = "";
    private readonly string titleKey = "";
    private readonly string blurbKey = "";
    private readonly string keywordsKey = "";

    /// <summary>
    /// What the add list calls it, and the folder it makes, in the language Karpo speaks now: its
    /// words are keyed by its id (<c>core.preset.&lt;id&gt;.title</c>, <c>.blurb</c>, <c>.keywords</c>), as
    /// a widget's are (<see cref="WidgetInfo.WordsKey"/>).
    /// </summary>
    public string Title => Strings.Text(titleKey);

    /// <summary>
    /// A Segoe Fluent Icons code point: its row's icon, and the folder's, which its button on the
    /// bar shows (<see cref="FolderFaces.Icon"/>).
    /// </summary>
    public required string Glyph { get; init; }

    /// <summary>One line saying what is in it, which the search reads.</summary>
    public string Blurb => Strings.Text(blurbKey);

    /// <summary>More words it is found by, besides its name, its line and its widgets' names: the language's own, then English's.</summary>
    public IReadOnlyList<string> Keywords => Strings.List(keywordsKey);

    /// <summary>Its English name and line, where Karpo speaks another language, for the search; nothing in English.</summary>
    public IEnumerable<string> EnglishWords => Strings.AlsoEnglish(titleKey, blurbKey);

    /// <summary>
    /// The face its folder's button wears (<see cref="FolderFaces"/>) with its options; null for its
    /// icon (<see cref="FolderFaces.Icon"/>). Each folder made gets a copy, so two never share one.
    /// </summary>
    public FolderFace? Face { get; init; }

    /// <summary>
    /// The faces its button wears after <see cref="Face"/>, side by side (<see cref="FolderOptions.Faces"/>),
    /// each where this PC has what it needs: a combined group's second subject, the weather beside
    /// the time. Empty for one face.
    /// </summary>
    public IReadOnlyList<PresetFace> MoreFaces { get; init; } = [];

    /// <summary>
    /// Whether it puts several groups' subjects together (the clock and the calendar, the mail and
    /// the notifications) rather than being one flyout's. The add list shows each in its subject as
    /// any other (AddTree); <see cref="All"/> has them last.
    /// </summary>
    public bool Combined { get; init; }

    /// <summary>The folder it makes: <see cref="BarKind.FolderList"/>.</summary>
    public required BarKind Folder { get; init; }

    /// <summary>Its widgets, the lists' lines in their order, left to right (<see cref="PresetItem.NewList"/>).</summary>
    public required IReadOnlyList<PresetItem> Items { get; init; }

    /// <summary>How wide each list is (<see cref="FolderOptions.Widths"/>); null for every one medium.</summary>
    public IReadOnlyList<FolderListWidth>? Widths { get; init; }

    /// <summary>Which widgets it holds, as its folder has them: a list's lines.</summary>
    public WidgetType Type => Partners.TypeOf(Folders.View(Folder));

    /// <summary>§641: whether it holds a widget marked Beta (<see cref="WidgetInfo.Beta"/>): the add page marks it so too.</summary>
    public bool Beta => Items.Any(item => WidgetInfos.Find(item.Form)?.Beta == true);

    /// <summary>
    /// §620: its lines as a bar on <paramref name="edge"/> has them: <see cref="Items"/> on a top or bottom bar, its lists
    /// side by side; down a side bar one list (<see cref="Stacked"/>), as a folder opened beside a side bar has the
    /// screen's height and little of its width to spare.
    /// </summary>
    public IReadOnlyList<PresetItem> ItemsFor(Edge edge) => edge.IsVertical() ? Stacked : Items;

    /// <summary>
    /// §620: its lists one under the other, left to right, a divider where one ends and the next begins — two lists are two
    /// cards, and one list keeps them two —, each line at its <see cref="PresetItem.SideHeight"/> where it has one.
    /// </summary>
    public IReadOnlyList<PresetItem> Stacked =>
        [.. Items.SelectMany((item, index) => item.NewList && index > 0
            ? [PresetItem.Divider, item.Down()]
            : (IEnumerable<PresetItem>)[item.Down()])];

    /// <summary>§620: how wide its lists are on a bar on <paramref name="edge"/>: <see cref="Widths"/>, or down a side bar its one list as wide as its widest.</summary>
    public IReadOnlyList<FolderListWidth>? WidthsFor(Edge edge) =>
        edge.IsVertical() && Widths is { Count: > 0 } widths ? [widths.MaxBy(FolderListWidths.Dip)] : Widths;
}

/// <summary>A face a preset's folder wears after its first (<see cref="Preset.MoreFaces"/>), and what this PC must have for it.</summary>
public sealed record PresetFace(FolderFace Face, PresetNeed Needs = PresetNeed.None);

/// <summary>What this PC has that a preset asks about, read when a preset is picked.</summary>
/// <param name="HasBattery">Whether it has a battery (System's battery line).</param>
/// <param name="FixedDrives">Its fixed drives, in letter order (Storage's lines).</param>
/// <param name="Sleeps">power: whether Windows' power menu offers sleep (the power preset's sleep button).</param>
/// <param name="Hibernates">power: whether it offers hibernate, which it does only where somebody turned it on.</param>
/// <param name="Rotates">quick: whether its screen turns with it (<see cref="WindowsTaskbar.ScreenRotates"/>), where Windows offers rotation lock.</param>
public sealed record PresetMachine(bool HasBattery, IReadOnlyList<DriveVolume> FixedDrives, bool Sleeps = true, bool Hibernates = false, bool Rotates = false)
{
    /// <summary>
    /// This PC, as the drives' own listing, the power status, Windows' power menu and its screen's
    /// rotation say (the power two the dock's to read). Never waits on a drive: no sizes are read.
    /// </summary>
    public static PresetMachine Read(bool hasBattery, bool sleeps = true, bool hibernates = false) => new(
        hasBattery,
        [.. DriveTargets.Attached(network: false, space: false).Where(drive => drive.Kind == DriveKind.Fixed)],
        sleeps,
        hibernates,
        WindowsTaskbar.ScreenRotates());

    /// <summary>Whether this PC has what <paramref name="need"/> asks for.</summary>
    public bool Has(PresetNeed need) => need switch
    {
        PresetNeed.Battery => HasBattery,
        PresetNeed.Sleep => Sleeps,
        PresetNeed.Hibernate => Hibernates,
        PresetNeed.Rotation => Rotates,
        _ => true,
    };
}

/// <summary>The ready-made groups, and the one act that adds one.</summary>
public static class Presets
{
    /// <summary>
    /// Every preset, in the order the add list shows them: the two the person named, then Windows
    /// 11's own compositions, then the rest.
    /// </summary>
    public static readonly Preset[] All =
    [
        // The clock (2026-10-09: "the clock taskbar widget is actually just a premade widget that is
        // actually a list folder with a face and correct widgets inside the folder"), as the person
        // arranged it by hand (2026-10-11: "the clock layout i already changed on the given list, get
        // it from there, design the other ones in a similar fashion"): the clock flyout's time over
        // its date, the timer, the stopwatch, the focus session and the footer, each line at its own
        // height, one card with no divider. The month and the agenda went: they are the calendar's
        // (below), and no two presets hold the same piece. Its face is the clock, the time only: the
        // date is the calendar face's. The footer's link is Date & time, the clock flyout's own; its
        // button is Focus, the Settings page of the focus session above it (an action is a Settings page, and
        // the folder holds no notifications for Notifications to be about). Nothing about it is special
        // once added: an ordinary folder anybody could have made, and a new bar's clock until 2026-10-13,
        // when Windows' clock button became the notification center's; a bar that has one keeps it as it was made.
        new()
        {
            Id = ClockId, Glyph = "",
            Folder = BarKind.FolderList,
            Face = ClockFace.New(),
            Items =
            [
                new() { Kind = "clock", Style = "time" },
                new() { Kind = "timer" },
                // The person's own: the stopwatch's one row (a new line is automatic, §388).
                new() { Kind = "stopwatch", Height = LineHeight.Exactly(2) },
                new() { Kind = "focus" },
                PresetItem.Of(PremadeWidgets.ClockActions),
            ],
        },
        // The calendar (2026-10-09: "now do the same for the weather forecast and calendar widget. make
        // sure that their styles are preserved as well as possible"). What a click on the calendar
        // entry gave, made of widgets, top to bottom: the flyout's header, today's date, whose click
        // goes back to today; the day's events, following the day the month picks (the folder's Day);
        // its month, with a dot under each day with events; and its footer: Date & time as the link,
        // the calendar app as the button. One card with its footer, as the person arranged it in
        // their own (2026-10-11: "i changed the layouts on the layout page already. so take them
        // over to the presets"): the date, the month to pick a day, and that day's events under it,
        // six rows fixed so the month doesn't move from day to day. Its face is the date as the calendar entry wrote
        // it, the day's name and the date. The flyout's other views are lines of their own (the day,
        // week and year views), for whoever wants one in its place.
        new()
        {
            Id = CalendarId, Glyph = "\uE787",
            Folder = BarKind.FolderList,
            Face = CalendarFace.New(withDay: true),
            Items =
            [
                new() { Kind = "dateHeader" },
                new() { Kind = "calendar", Style = "month" },
                new() { Kind = "calendar", Style = "events", Height = LineHeight.Exactly(12) },
                PresetItem.Of(PremadeWidgets.CalendarActions),
            ],
        },
        // The next meeting (2026-10-17, docs\widgets.md §205): on the bar the meeting on now, else
        // today's next, how soon it starts and its Join; opened, today's meetings still to come, each
        // with its Join, over the calendar's footer — the calendar app and Date & time.
        new()
        {
            Id = MeetingId, Glyph = WidgetInfos.Meeting.Glyph,
            Folder = BarKind.FolderList,
            Face = MeetingFace.New(),
            Items =
            [
                new() { Kind = "meeting" },
                PresetItem.Of(PremadeWidgets.CalendarActions),
            ],
        },
        // The weather (2026-10-09, as the calendar): what a click on the weather entry gave — the place
        // and the conditions now with how old the reading is, the next hours' curve and the week —
        // one card, as the clock is (2026-10-11, "in a similar fashion"): three views of one forecast,
        // which a divider would only cut in three; and no footer, as the flyout had none but
        // the reading's age. Its face is the conditions' glyph and the temperature, as the entry on
        // the bar was. Each piece has a place of its own (WeatherPlaces); the first chosen as the preset
        // is added is given to the others still without one, once (WeatherPlaces.Spread). A new bar had it where Windows keeps its widgets button
        // until 2026-10-14, when the person had it off the default ("dont actually show the widgets button").
        new()
        {
            Id = WeatherId, Glyph = "\uE753",
            Folder = BarKind.FolderList,
            Face = WeatherFace.New(),
            Items =
            [
                // §590: the current weather, the hourly forecast's graph at the flyout's band and the daily forecast's list.
                new() { Kind = "weatherNow" },
                new() { Kind = "hourlyForecast" },
                new() { Kind = "dailyForecast" },
            ],
        },
        // The air quality (2026-10-18, docs\widgets.md §210): on the bar the index with its level's
        // dot, "32 · Good"; opened, the air-quality line — the index, the pollutants, the pollen and
        // the next hours. One line, its own card; its place the device's as the weather's starts, and
        // the first place chosen given to the face as well (WeatherPlaces.Spread).
        new()
        {
            Id = AirQualityId, Glyph = WidgetInfos.AirQuality.Glyph,
            Folder = BarKind.FolderList,
            Face = AirQualityFace.New(),
            Items =
            [
                // §590: the index over its scale, the pollutants and the pollen in columns, the day's hours.
                new() { Kind = "airQuality" },
                new() { Kind = "airPollutants", Size = LineSize.Compact },
                new() { Kind = "airPollen", Size = LineSize.Compact },
                new() { Kind = "airForecast" },
            ],
        },
        // The tides (2026-10-19, docs\widgets.md §236: "add a tides widget"): on the bar the way the
        // sea goes and the next high or low, "High 14:32"; opened, the tides line — the height now,
        // the day's curve and the next turns. One line, its own card; its place the device's as the
        // weather's starts, and the first place chosen given to the face as well (WeatherPlaces.Spread).
        new()
        {
            Id = TidesId, Glyph = WidgetInfos.Tides.Glyph,
            Folder = BarKind.FolderList,
            Face = TidesFace.New(),
            Items =
            [
                // §603: the tides now with its caption over the day's curve.
                new() { Kind = "tides" },
                new() { Kind = "tideGraph" },
            ],
        },
        // The sun (2026-10-26, docs\widgets.md §368: "add a sunset and sunrise widget. similar as the one
        // in themia"): on the bar the next sunrise or sunset, "18:32" by the sun on the horizon; opened,
        // the sun line in its arc — the sun's path, sunrise, daylight and sunset — over the times the
        // times style started with, each a widget of its own since §404: sunrise, sunset, the daylight
        // and solar noon. Worked out on this PC; its place the device's as the weather's starts, the
        // first place chosen given to the face and every line (WeatherPlaces.Spread).
        new()
        {
            Id = SunId, Glyph = WidgetInfos.SunArc.Glyph,
            Folder = BarKind.FolderList,
            Face = SunFace.New(),
            Items =
            [
                new() { Kind = "sun" },
                new() { Kind = "sunrise" },
                new() { Kind = "sunset" },
                new() { Kind = "daylight" },
                new() { Kind = "solarNoon" },
            ],
        },
        // sports: a favourite team (2026-10-19, docs\widgets.md §239): on the bar its live score or
        // next match; opened, the sports line where a new one starts, automatic up to twelve rows (§371):
        // the match as a scoreboard and what it has under it, as tall as that. No team to begin with:
        // the line asks for one on its one row, and the face shows the line's.
        new()
        {
            Id = SportsId, Glyph = WidgetInfos.Sports.Glyph,
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.SportsId),
            Items =
            [
                // §603: the scoreboard over the matches to come and played.
                new() { Kind = "sports" },
                new() { Kind = "sportsFixtures" },
            ],
        },
        // The earthquakes (2026-10-19, docs\widgets.md §240): on the bar the latest near the place,
        // "M4.8 · 120 km"; opened, the earthquake line — the past week's, newest first, each opening
        // its page on the USGS's site. Its place the device's as the weather's starts, the first
        // place chosen given to the face as well (WeatherPlaces.Spread).
        new()
        {
            Id = QuakesId, Glyph = WidgetInfos.Quakes.Glyph,
            Folder = BarKind.FolderList,
            Face = QuakesFace.New(),
            Items =
            [
                new() { Kind = "quakes" },
            ],
        },
        // The network (2026-10-10: "port the wifi and bluetooth flyouts to the new system as well"):
        // what a click on the network button gave — the quick settings' Wi-Fi page with the cables at
        // the head of its list — as its line, and its footer as the premade Wi-Fi actions (§85):
        // "Wi-Fi", and "Network & internet" as the flyout's "Network and Internet settings". One card, the footer under the page, as the clock's (2026-10-11).
        // The page ten rows fixed, as the person set it (2026-10-11), so the folder keeps its size
        // while networks come and go. Its face is the
        // connection's glyph, as the button on the bar was; a new bar's network is quick settings' since 2026-10-13.
        new()
        {
            Id = WifiId, Glyph = "",
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.WifiId),
            Items =
            [
                new() { Kind = "wifi", Height = LineHeight.Exactly(20) },
                PresetItem.Of(PremadeWidgets.WifiActions),
            ],
        },
        // Bluetooth, the same way: the page's switch and paired devices, ten rows fixed, and its footer, the premade
        // Bluetooth actions — "Bluetooth & devices" as its "More Bluetooth settings" and "Add device", which opens
        // Settings' Bluetooth page with its own "Add device" at the top. Its face is the rune, dimmed
        // while the radio is off.
        new()
        {
            Id = BluetoothId, Glyph = "",
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.BluetoothId),
            Items =
            [
                new() { Kind = "bluetooth", Height = LineHeight.Exactly(20) },
                PresetItem.Of(PremadeWidgets.BluetoothActions),
            ],
        },
        // The sound (2026-10-10, after the network's and Bluetooth's): what a click on the sound
        // button gave — the sound flyout — as its regions' lines in one list, as the person rearranged
        // it on their main bar (2026-10-11, "i made it a one column design"): the devices, the output
        // over the microphone, one card; a divider; the volume mixer, the volume under it and the
        // footer, the premade sound actions (§85): "Sound", and "Volume mixer" as the flyout's "More
        // volume settings", the second card. Its face is the speaker, as the button on
        // the bar was; a new bar's is quick settings' since 2026-10-13.
        new()
        {
            Id = SoundId, Glyph = "\uE767",
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.SoundId),
            Items =
            [
                new() { Kind = "soundOutput" },
                new() { Kind = "soundInput" },
                PresetItem.Divider,
                new() { Kind = "soundMixer" },
                new() { Kind = "soundVolume" },
                PresetItem.Of(PremadeWidgets.SoundActions),
            ],
        },
        // The removable devices (2026-10-10, after the sound): what a click on the removable-devices
        // button gave — its flyout, a row per device with what it holds and its Eject, or a line
        // saying nothing is plugged in — is the line the widget always had in a folder, alone: the
        // flyout had no footer, so there is none. Its face is the stick, as the button was. A new
        // bar never had the button, so it has no preset either; it is in the add list's groups.
        new()
        {
            Id = RemovableId, Glyph = "",
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.RemovableId),
            Items =
            [
                new() { Kind = "removableDevices" },
            ],
        },
        // The timer, the focus session and the stopwatch (2026-10-10, after the removable devices):
        // what a click on each entry gave — its own panel, its card — is its line at the height the
        // person gave it (2026-10-11): a countdown two rows fixed, its time with the steppers and the
        // start button; the stopwatch five, its row with the laps under it. The panels had
        // no footer, so there is none; nor since the clock has one (2026-10-11): a timer and a
        // stopwatch have no Settings page for a footer to open, and Date & time says nothing about
        // either, so a footer would be a second card's worth of links for one line. Each wears its face, what the line in it has left or has
        // counted, as the entry did. A new bar never had them, so they are in the add list's groups.
        new()
        {
            Id = TimerId, Glyph = WidgetInfos.Timer.Glyph,
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.TimerId),
            Items =
            [
                new() { Kind = "timer" },
            ],
        },
        new()
        {
            Id = FocusId, Glyph = WidgetInfos.Focus.Glyph,
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.FocusId),
            Items =
            [
                new() { Kind = "focus" },
            ],
        },
        new()
        {
            Id = StopwatchId, Glyph = WidgetInfos.Stopwatch.Glyph,
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.StopwatchId),
            Items =
            [
                new() { Kind = "stopwatch", Height = LineHeight.Exactly(10) },
            ],
        },
        // The mobile hotspot (2026-10-10, after the timers): what a click on the mobile hotspot
        // button gave — its page, the switch, the network's name, password and band and the devices
        // on it — as its line, and under it in the same card, as the clock's (2026-10-11), its footer as
        // the premade hotspot actions (§85): "Mobile hotspot" for the flyout's "Mobile hotspot
        // settings", and "Network & internet", where Windows keeps it. Its face is the hotspot's
        // glyph, dimmed while it is off, with how many devices are on it. A new bar never had the
        // button, so it is in the add list's groups. The switch alone is the mobileHotspot line,
        // which Connectivity holds.
        new()
        {
            Id = HotspotId, Glyph = WidgetInfos.Hotspot.Glyph,
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.HotspotId),
            Items =
            [
                new() { Kind = "hotspot" },
                PresetItem.Of(PremadeWidgets.HotspotActions),
            ],
        },
        // Cast (2026-10-11: "the cast icon opens the windows cast flyout ... why dont we implement it
        // so that it looks and behaves the same everywhere?"): Windows' Cast panel as a line — the
        // wireless displays in range, the one this PC shows on first — then Win+P's modes, which the
        // panel's "Change projection mode" leads to, and in the same card its footer as the premade
        // cast actions: Display, and Add device for "Find other devices". Connecting and disconnecting
        // stay Windows' panel's (a display's row opens it): no unpackaged app may do either. Its
        // face is the cast glyph, with a dot while this PC is cast to a display.
        new()
        {
            Id = CastId, Glyph = WidgetInfos.WirelessDisplays.Glyph,
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.CastId),
            Items =
            [
                new() { Kind = "wirelessDisplays" },
                new() { Kind = "projection" },
                PresetItem.Of(PremadeWidgets.CastActions),
            ],
        },
        // Nearby sharing (2026-10-11: "sharing widget just opens the sharing settings, is it possible
        // to actually make a proper flyout out of this?"): Windows' nearby sharing as a line — off, my
        // devices only, everyone nearby, where received files go and the name this PC is found by —
        // and in the same card its footer as the premade nearby actions, "Nearby sharing settings".
        // Its face is the share glyph, dimmed while it is off; the switch alone stays Connectivity's line.
        new()
        {
            Id = NearbyId, Glyph = WidgetInfos.NearbySharing.Glyph,
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.NearbyId),
            Items =
            [
                new() { Kind = "nearbySharing" },
                PresetItem.Of(PremadeWidgets.NearbyActions),
            ],
        },
        // The battery (2026-10-10, after the mobile hotspot): what a click on the battery gave — its
        // flyout, the charge with what it is doing and the time left, energy saver, and the power
        // mode — as lines in the flyout's order: the battery's line (left out where this PC has no
        // battery, as the flyout left the charge out), energy saver's switch, the power mode; then,
        // in the same card as the clock's (2026-10-11), its footer as the premade power actions
        // (§85): "Power & battery" for its "Battery settings", and "Energy saver". No divider among
        // the three: the flyout kept them on one page. Its face is the charge, as the battery on the
        // bar was; a new bar's is quick settings' since 2026-10-13.
        new()
        {
            Id = BatteryId, Glyph = WidgetInfos.Battery.Glyph,
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.BatteryId),
            Items =
            [
                new() { Kind = "battery", Needs = PresetNeed.Battery },
                new() { Kind = "energySaver" },
                new() { Kind = "powerMode" },
                PresetItem.Of(PremadeWidgets.PowerActions),
            ],
        },
        // The power button (2026-10-10, with the battery): what a click on it gave — the power menu
        // and the account menu — as the power buttons, a line each (2026-10-11: "the power folder /
        // premade group should only show the individual power buttons"), in the order the person put
        // them in their own (2026-10-11: "update the second layout, i did some changes, take mine"):
        // sign out and lock, then sleep, restart and shut down; sleep only where Windows' power menu
        // offers it, as the flyout left it out. Hibernate is not among them, as it is not in theirs:
        // Windows offers it only where somebody turned it on, and a button for it is one pick in the
        // add list. Nothing else: the power mode and the footer about it are the battery's (above).
        // Its face is the power glyph, as the button was. A new bar never had the button, so it is
        // in the add list's groups.
        new()
        {
            Id = PowerId, Glyph = FolderFaces.Power.Glyph,
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.PowerId),
            Items =
            [
                new() { Kind = "signOut" },
                new() { Kind = "lock" },
                new() { Kind = "sleep", Needs = PresetNeed.Sleep },
                new() { Kind = "restart" },
                new() { Kind = "shutDown" },
            ],
        },
        // The virtual desktops (2026-10-11: "the desktop switcher should be made into a folder with
        // face and widgets inside"): what a click on the desktop switcher gave — every desktop with
        // the apps open on it, the one you are on marked, a click switching, each renamed, closed and
        // moved, and "New desktop" under them — is the desktops' line, which does all of that, a
        // desktop dragged by its gripper into another place where the flyout had Move up and Move
        // down, as tall as there are desktops up to the most a line may be; then the premade desktop
        // actions in its card (Multitasking | Task view). Its face is what the switcher was: its glyph
        // and the desktop's name. A new bar never had the switcher, so it is in the add list's groups.
        new()
        {
            Id = DesktopsId, Glyph = WidgetInfos.Desktops.Glyph,
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.DesktopsId),
            Items =
            [
                new() { Kind = "desktops", Height = LineHeight.Automatic(FolderOptions.MostCells) },
                PresetItem.Of(PremadeWidgets.DesktopActions),
            ],
        },
        // The mail (2026-10-10, after the battery and the power button): what a click on the mail
        // entry gave — its panel, how many are unread over the newest messages, each its sender,
        // subject and when, the unread in bold; "Set up in Settings" with no account; a note while an
        // inbox could not be refreshed — is the inbox's line alone, at its own height (Automatic): the
        // count over the newest messages, up to eight, the setup prompt and the stale mark its own.
        // The panel had no footer but that note, which the line's mark says, so there is none. Its
        // face is the envelope and its count, as the entry was. A new bar never had the entry, so it
        // has no preset either; it is in the add list's groups.
        new()
        {
            Id = MailId, Glyph = WidgetInfos.Mail.Glyph,
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.MailId),
            Items =
            [
                new() { Kind = "mail" },
            ],
        },
        // The person's first: "a list folder which contains all the system widgets already". Task
        // Manager's Performance page, top to bottom — processor, memory, disk, network, graphics —
        // and the battery where there is one, and under the last the performance readings' footer,
        // Task Manager, in its card, as the person added it (2026-10-11). Each reading Compact (§603; three
        // rows of its whole widget until the tiers went): its name, its figure and its second reading
        // over its graph — rather than the reading's details, whose every fact would make five readings
        // taller than a screen; and a divider between each two, a card each (2026-10-11: "make a divider
        // in between in the default layout"). In two lists side by side, the processor, the memory and the
        // disk, then the network, the graphics and the battery: five cards over one another would pass the
        // 70 % of a 1440p screen at 150 % a folder may stand (638 DIP). The battery keeps its two rows, the compact line, which makes the two
        // lists one height; its divider goes with it (Needs), so a PC without one has its graphics
        // over the footer.
        new()
        {
            Id = "systemGroup", Glyph = "",
            Folder = BarKind.FolderList,
            Items =
            [
                new() { Kind = "performance", Size = LineSize.Compact },
                PresetItem.Divider,
                new() { Kind = "performance", Reading = "memory", Style = WidgetStyles.GraphWord, Size = LineSize.Compact },
                PresetItem.Divider,
                new() { Kind = "performance", Reading = "disk", Style = WidgetStyles.GraphWord, Size = LineSize.Compact },
                new() { Kind = "performance", Reading = "network", Style = WidgetStyles.GraphWord, Size = LineSize.Compact, NewList = true },
                PresetItem.Divider,
                new() { Kind = "performance", Reading = "gpu", Style = WidgetStyles.GraphWord, Size = LineSize.Compact },
                PresetItem.Divider with { Needs = PresetNeed.Battery },
                new() { Kind = "battery", Size = LineSize.Medium, Needs = PresetNeed.Battery },
                PresetItem.Of(PremadeWidgets.TaskManagerActions),
            ],
        },
        // The performance readings (2026-10-10, after now playing): what a click on each of the five
        // entries gave — its flyout, Task Manager's page for the reading in a panel: its name, the
        // figure large, the last minute as a graph with what its top stands for, the facts behind the
        // figure in a grid (a processor's seven, a disk's three), and "Open Task Manager" — is two
        // widgets and a footer (2026-10-11: "the graphics / cpu / network graphs and details should
        // be separate widgets. they should not have a divider in between them"): the reading's graph
        // at its Full size, the figure and the minute (§603); its details under it at
        // their own height, the facts; and the button as the premade Task Manager footer — one card,
        // as the clock's (2026-10-11), no divider anywhere in it. Its face is what the entry was: the
        // glyph and the figure. A new bar never had the entries, so it has none of these either; they
        // are in the add list's groups, after System, which keeps the five as lines of three rows.
        Reading(CpuId, Metric.Cpu, FolderFaces.CpuId),
        Reading(GpuId, Metric.Gpu, FolderFaces.GpuId),
        Reading(MemoryId, Metric.Memory, FolderFaces.MemoryId),
        // The disk's own drive is Storage's icon, and no two groups share one: the gauge, its activity.
        Reading(DiskId, Metric.Disk, FolderFaces.DiskId, glyph: "\uEC4A"),
        Reading(NetworkActivityId, Metric.Network, FolderFaces.NetworkActivityId),
        // The markets (§648: "market and stocks group preset should be merged as they are basically the same"): Windows 11's
        // Watchlist widget, the person's second ("a list folder which shows a list of stocks already") — a share a line, the
        // shares nearly everybody has heard of and the S&P 500 for the market as a whole, one list, no divider —, each line the
        // markets widget (§246), a share, a currency's rate or a coin, its menu turning it into another. Wide, as the person set
        // it (2026-10-11), each share at its Full form. Its face the S&P 500's ticker, as Markets and news'; its glyph the chart
        // the person asked for. It was two: the Watchlist ("Stocks") and Markets, one line with no symbol yet under a face that
        // said "Set up" — the same widget once against four times.
        new()
        {
            Id = MarketId, Glyph = "",
            Folder = BarKind.FolderList,
            Face = MarketFace(),
            Items =
            [
                new() { Kind = "markets", Symbol = "MSFT", Size = LineSize.Full },
                new() { Kind = "markets", Symbol = "AAPL", Size = LineSize.Full },
                new() { Kind = "markets", Symbol = "NVDA", Size = LineSize.Full },
                new() { Kind = "markets", Symbol = "^GSPC", Label = "S&P 500", Size = LineSize.Full }, // not-ui: the index's name
            ],
            Widths = [FolderListWidth.Wide],
        },
        // The news (2026-10-10, with the stock): what a click on the RSS entry gave — its panel: the
        // feed's name with a refresh, the newest stories each with its headline, where it is from,
        // how old it is and its picture, the new ones dotted, and a link to the feed's site — is the
        // feed's line at its own height (Automatic, up to eight stories and how many more), whose
        // whole widget is all of it. It starts with no feed, as the entry did. Its face is what the
        // entry was: the news glyph, its dot and the newest headline.
        new()
        {
            Id = NewsId, Glyph = WidgetInfos.Rss.Glyph,
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.NewsId),
            Items =
            [
                new() { Kind = "rss" },
            ],
        },
        // The drive (2026-10-10, after the stock and the news): what a click on the drive entry gave —
        // its flyout: the drive's picture, its name, its kind and the capacity bar with what is used
        // and free, and under it the drive's top level, folders first and then files, each opened with
        // a click — is two lines (2026-10-11: "the drive widget should get split in two ... it should
        // only show the drive information and then the drive contents should just be shown by the
        // normal folder widget inline"): the drive's line, its information alone, and under it the
        // contents of the drive's root (§380: the folder's contents widget). The two together are as
        // tall as the person's one line was, ten rows at the most (2026-10-11): the drive's line its own height, the folder the rest. It starts with
        // no drive, as the entry did: the line's "Set up in Settings" asks for one, and the drive
        // chosen there is given to the folder still without one, once (DriveTargets.SpreadContents).
        // Its face is what the entry was: the drive's picture, its name and the bar. A new bar never
        // had the entry, so it has no preset either; it is in the add list's groups, beside Storage.
        new()
        {
            Id = DriveId, Glyph = WidgetInfos.Drive.Glyph,
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.DriveId),
            Items =
            [
                new() { Kind = "drive" },
                new() { Kind = "folderContents", DriveContents = true, Height = LineHeight.Automatic(20 - DriveCells) },
            ],
        },
        // The network drive, the same for a mapped share, twelve rows at the most as the person set
        // it: its face the network drive entry's, with File Explorer's red X while it is not connected.
        new()
        {
            Id = NetworkDriveId, Glyph = WidgetInfos.NetworkDrive.Glyph,
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.NetworkDriveId),
            Items =
            [
                new() { Kind = "networkDrive" },
                new() { Kind = "folderContents", DriveContents = true, Height = LineHeight.Automatic(FolderOptions.MostCells - DriveCells) },
            ],
        },
        // The notifications (2026-10-10, after the drive): what a click on the bell gave — Karpo's
        // notification center, its "Clear all" and do not disturb's switch in its header, the
        // notifications grouped by app with a × per app and "See more" past three, each card
        // opened, expanded to its whole body, its buttons and its reply box, and its footer's ways to
        // Windows' settings — is the notification list's line, which does all of that now, as tall as
        // its notifications up to the most a line may be (past it, how many more is said), over the
        // premade notification actions (Notifications | Focus). It was the notification center
        // preset, the list over the calendar and a focus session; those are the calendar's and the
        // clock's (the calendar's preset holds the month and the agenda, the clock's the focus
        // session), so it is the bell's flyout alone now, its footer in its card as the clock's
        // (2026-10-11). Its face is what the bell was: the bell, its count and the "zZ". A new bar has
        // it where the bell stood, beside the time.
        new()
        {
            Id = NotificationsId, Glyph = WidgetInfos.NotificationsList.Glyph,
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.NotificationsId),
            Items =
            [
                new() { Kind = "notificationsList", Height = LineHeight.Automatic(FolderOptions.MostCells) },
                PresetItem.Of(PremadeWidgets.NotificationActions),
            ],
        },
        // The clipboard history (2026-10-10, after the notifications): what a click on its entry gave
        // — Win+V's panel, "Clear all" over a card per item, text in its first lines and a picture as
        // its thumbnail, each to pin and to delete, a click pasting it where the person was, and
        // while Windows' own history is off why, with the way to its setting — is the clipboard's
        // line, which does all of that as rows, ten rows fixed as the person set it (2026-10-11) so
        // the folder keeps its size as items come and go (past them, how many more is said), over
        // the premade clipboard actions (Clipboard settings),
        // the flyout's footer link. Its face is the glyph the entry was. A new bar never had the
        // entry, so it has no preset either; it is in the add list's groups.
        new()
        {
            Id = ClipboardId, Glyph = WidgetInfos.Clipboard.Glyph,
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.ClipboardId),
            Items =
            [
                new() { Kind = "clipboard", Height = LineHeight.Exactly(20) },
                PresetItem.Of(PremadeWidgets.ClipboardActions),
            ],
        },
        // The contacts (2026-10-11, after the clipboard history): what a click on its entry gave — a
        // search box over everyone in the person's accounts, typing narrowing them at once, Enter or
        // a click copying one's address, and under them how many there are or what was copied — is
        // the contacts' line, which does all of that, as tall as its people up to the most a line may
        // be (past it, how many more is said). The flyout had no link in its footer, so the preset
        // has no actions under the line. Its face is the glyph the entry was. A new bar never had
        // the entry, so it has no preset either; it is in the add list's groups.
        // §642: Windows search (beta): a search box on the bar, Windows 11's, whose click opens the folder with the keys in
        // its line; §652 the line at its one height whatever it finds (ListWidgets: fixed, twelve rows), on every edge; §655
        // Extra wide (640 DIP), so its list and the chosen result's pane stand side by side as Windows 11's search has them.
        new()
        {
            Id = WinSearchId, Glyph = WidgetInfos.WinSearch.Glyph,
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.WinSearchId),
            Widths = [FolderListWidth.ExtraWide],
            Items =
            [
                new() { Kind = "winSearch", Height = LineHeight.Exactly(FolderOptions.MostCells) },
            ],
        },
        new()
        {
            Id = ContactsId, Glyph = WidgetInfos.Contacts.Glyph,
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.ContactsId),
            Items =
            [
                new() { Kind = "contacts", Height = LineHeight.Automatic(FolderOptions.MostCells) },
            ],
        },
        // The input indicator (2026-10-11: "all the remaining flyouts should become folders with a
        // face now"): what a click on it gave — the layouts installed, the one in use ticked, a pick
        // switching the window in front — is the keyboard's line, as tall as there are layouts, over
        // the premade keyboard actions (Language & region), Windows' input switcher's "More keyboard
        // settings". Its face is the indicator, the language over the region, as the entry was,
        // where a new bar has it (Config.FirstLayout).
        new()
        {
            Id = KeyboardId, Glyph = WidgetInfos.Keyboard.Glyph,
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.KeyboardId),
            Items =
            [
                new() { Kind = "keyboard" },
                PresetItem.Of(PremadeWidgets.KeyboardActions),
            ],
        },
        // VPN (2026-10-11, after the input indicator): what a click on the VPN entry gave — its
        // connections, each connected or disconnected from its row, as Windows' VPN page has them —
        // is the connections' line, as tall as there are connections, over the premade VPN actions:
        // "VPN" for the flyout's "VPN settings", and Network & internet. Its face is the shield, lit
        // while a connection is up. A new bar never had the entry, so it is in the add list's groups;
        // the switch alone is the vpn line, which Connectivity holds.
        new()
        {
            Id = VpnId, Glyph = WidgetInfos.Vpn.Glyph,
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.VpnId),
            Items =
            [
                new() { Kind = "vpnConnections" },
                PresetItem.Of(PremadeWidgets.VpnActions),
            ],
        },
        // Project (2026-10-11, after VPN): what a click on the project entry gave — Win+P's four
        // modes, the one in force marked — is the projection line the cast preset has too, over the
        // premade project actions: Display, the flyout's "More display settings". Its face is its
        // icon, the second screen, as the entry was: Windows tells no app which mode is in force
        // until asked, so the entry had no state to show either. In the add list's groups.
        new()
        {
            Id = ProjectId, Glyph = "\uE8AF",
            Folder = BarKind.FolderList,
            Items =
            [
                new() { Kind = "projection" },
                PresetItem.Of(PremadeWidgets.ProjectActions),
            ],
        },
        // Layouts (2026-10-11, after project): what a click on the layout switcher gave — the taskbar
        // layouts, the one this bar shows marked, a click switching this display to another — is the
        // layouts' line, as tall as there are layouts. Its face is what the switcher was: the
        // layout's glyph and its name. A new bar never had the switcher, so it is in the add list's
        // groups.
        new()
        {
            Id = LayoutsId, Glyph = WidgetInfos.Layouts.Glyph,
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.LayoutsId),
            Items =
            [
                new() { Kind = "layout" },
            ],
        },
        // Getting work done: the time, a timer and a focus session, do not disturb, and the day ahead.
        // Two cards: keeping one's focus (the Clock app's time, timer and focus sessions, and the
        // quick settings' switch that keeps the notifications away), and the calendar.
        new()
        {
            Id = "focusGroup", Glyph = "",
            Folder = BarKind.FolderList,
            Items =
            [
                new() { Kind = "clock" },
                new() { Kind = "timer" },
                new() { Kind = "focus" },
                new() { Kind = "doNotDisturb" },
                PresetItem.Divider,
                new() { Kind = "calendar" },
            ],
        },
        // Screen time on the bar (2026-10-09): what was a taskbar entry with a flyout of its own until
        // 2026-09-26 is a list folder now, as every flyout is becoming, its button the screen-time
        // glyph: the day's total with the days before it as a graph, four rows so the graph and the
        // step back and on a day are there, then the day's apps, eight rows fixed (2026-10-10) so the
        // folder keeps its size from day to day. Both follow the folder's day, so a step on the
        // total's line shows that day's apps. No divider: the apps are the total's parts.
        //
        // The day as a ring between the two (2026-10-12), four rows, following the day shown and the
        // day pointed at in the bars; the apps six rows then, so the folder is two rows taller than
        // it was and still fits over the taskbar of a 1080p screen at 150 %. A folder made before
        // keeps what it has: a preset is only what a new folder starts as.
        new()
        {
            Id = "screenTimeGroup", Glyph = "",
            Folder = BarKind.FolderList,
            // Its face today's total since §504, "3 h 42 min" by the glyph, as a preset of a widget with a face wears it.
            Face = FolderFace.Of(FolderFaces.ScreenTimeId),
            Items =
            [
                // §603: today's total and average over the fortnight's bars.
                new() { Kind = "screenTime" },
                new() { Kind = "screenTimeHistory" },
                new() { Kind = "screenTimeChart" },
                new() { Kind = "screenTimeApps", Height = LineHeight.Exactly(12) },
            ],
        },
        // What Windows'' quick settings show of the sound: what is playing with its controls, the
        // volume, and the device it plays on. Windows draws what's playing as a card of its own over
        // its panel, and so does the person's own (2026-10-11): what's playing a card, a divider, the
        // volume and its device the next.
        //
        // It is the media preset too (2026-10-10, after the mail): what a click on the now-playing
        // entry gave — its flyout, where it plays, the art, the track and the artist, the timeline,
        // previous, play or pause and next, shuffle and repeat, the apps playing to pick from — is
        // the now-playing line at four rows, the whole widget, which has all of it (the picker on the
        // app's name), over the volume the flyout's footer led to; at its own height it was the
        // compact line, the art beside the track and the transport alone. Its face is what the entry
        // was: the art and the track. A new bar never had the entry, so it has no preset either.
        // The apps playing to pick from are a line of their own, the flyout's "Playing in" list, and
        // the output device is not here (2026-10-11: "show the selector for the source again ...
        // dont show output device, volume keep"). Then the person made it the now-playing line alone,
        // four rows fixed, in their own layout (2026-10-11: "take mine in the: now playing widget"):
        // the line has the picker on the app's name, and the volume is the sound's. Only a new
        // folder: one already made keeps its lines.
        new()
        {
            Id = MediaId, Glyph = "",
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.NowPlayingId),
            Items =
            [
                new() { Kind = "media", Size = LineSize.Full },
            ],
        },
        // This PC's drives as File Explorer's "This PC" lists them: a line for each fixed drive, read
        // when it is added, then what is plugged in, a divider between the two (and none where no
        // fixed drive could be read).
        new()
        {
            Id = "storageGroup", Glyph = "",
            Folder = BarKind.FolderList,
            Items =
            [
                new() { Kind = "drive", EachFixedDrive = true },
                PresetItem.Divider,
                new() { Kind = "removableDevices" },
            ],
        },
        // The connections, a list folder of them: how fast the network is moving, then the switches
        // of Windows' quick settings that are about connecting — airplane mode, the VPN, the mobile
        // hotspot and nearby sharing — a line each. It was the network and Bluetooth buttons with
        // the VPN and airplane mode as loose taskbar entries, which put four buttons on the bar
        // rather than one folder (2026-10-05: "the connectivity ready made folder when added to the
        // taskbar adds multiple individual widgets, this should not be"). The Wi-Fi and Bluetooth
        // pages are presets of their own (above). Its icon is the network's. A divider parts the
        // network's activity from the switches.
        new()
        {
            Id = "connectivityGroup", Glyph = "",
            Folder = BarKind.FolderList,
            Items =
            [
                new() { Kind = "performance", Reading = "network", Style = WidgetStyles.GraphWord, Size = LineSize.Compact },
                PresetItem.Divider,
                new() { Kind = "airplaneMode" },
                new() { Kind = "vpn" },
                new() { Kind = "mobileHotspot" },
                new() { Kind = "nearbyShare" },
            ],
        },
        // notes (2026-10-10): "add a generic todo list widget and add a notes widget". A folder of one
        // to-do list, its button the checklist, and a folder of one note, its button the note: each
        // shows the notebook's first list or note until its line is set to another, and either can
        // take more lines of either kind. Each ten rows fixed, as the person set them (2026-10-11):
        // a page to write on that doesn't grow and shrink with what is on it.
        new()
        {
            Id = "todoGroup", Glyph = "",
            Folder = BarKind.FolderList,
            Items = [new() { Kind = "todo", Height = LineHeight.Exactly(20) }],
        },
        new()
        {
            Id = "notesGroup", Glyph = "",
            Folder = BarKind.FolderList,
            Items = [new() { Kind = "note", Height = LineHeight.Exactly(20) }],
        },

        // §641 Start (beta) (the person, 2026-11-09: "create a widget group that looks like the start"): Windows 11's Start
        // as a folder, from its face on the bar, Windows' glyph: the pinned apps (three lines), Recommended (three lines) and
        // its foot, the account and power, as Start stands; every app beside them, a list of its own as tall as they are,
        // where 2025's Start has All under Recommended — side by side the folder needs no scrolling of its own. Down a side
        // bar one list, All apps shorter under the rest (§620). §653: no search box at its top any more (the person,
        // 2026-11-09: "start search folder widget does not seem to do anything. remove it.").
        new()
        {
            Id = StartId, Glyph = OptionGlyphs.Windows,
            Folder = BarKind.FolderList,
            Items =
            [
                new() { Kind = WidgetInfos.StartPins.Id, Size = LineSize.Full },
                new() { Kind = WidgetInfos.StartRecommended.Id, Size = LineSize.Full },
                new() { Kind = WidgetInfos.StartProfile.Id },
                new() { Kind = WidgetInfos.StartApps.Id, Height = LineHeight.Exactly(StartAppsCells), SideHeight = LineHeight.Exactly(StartAppsSideCells), NewList = true },
            ],
            Widths = [FolderListWidth.Wide, FolderListWidth.Medium],
        },

        // --- Combined groups ---------------------------------------------------------------------
        // (2026-10-11, the person: "add some combined folder / widget groups / premade groups now. for
        // example one that combine the time and calendar stuff ... it just needs to look good an make
        // sense ... they can also have two or more columns as well.") Each puts two or three of the
        // groups above together, a subject a list, side by side, so the folder is as tall as its
        // tallest subject and stands whole on a 1440p screen at 150 % (CombinedPresetTests). A list is
        // one card where its lines are one subject, with its footer in it, as the person's clock is;
        // a divider only where a list holds two. The button wears each subject's face, side by side.
        // The add list shows them under their own heading, after the others; the layout of every
        // group has them at the end, after the notes.

        // The clock and the calendar together (2026-10-11, the person: "create a folder face that
        // combined time and clock together like it is shown on the windows default taskbar. you
        // should also make a premade group that combines both too"). Its face is the time over the
        // date, as Windows' clock on the taskbar shows them (FolderFaces.DateTime). Inside, the two
        // presets as the person arranged them, side by side: the clock's list on the left — the time
        // over the long date, the timer, the stopwatch, the focus session and its footer — and the
        // calendar's on the right — the month to pick a day, that day's events six rows fixed, and its
        // footer. The calendar's date header is left out: the time header over it says the date
        // already. Two lists, not one: one would be some 21 rows, taller than a 1080-pixel screen
        // leaves a folder; side by side the folder is as tall as the calendar alone. The timer and
        // the focus session two rows each and the stopwatch three, their compact lines with the
        // steppers and the laps, so the clock's list stands about as tall as the calendar's.
        new()
        {
            Id = ClockCalendarId, Glyph = "",
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.DateTimeId),
            Combined = true,
            Items =
            [
                new() { Kind = "clock", Style = "time" },
                new() { Kind = "timer" },
                // §620: down a side bar its one row, as the clock's: three were to end its list level with the calendar's.
                new() { Kind = "stopwatch", Height = LineHeight.Exactly(6), SideHeight = LineHeight.Exactly(2) },
                new() { Kind = "focus" },
                PresetItem.Of(PremadeWidgets.ClockActions),
                new() { Kind = "calendar", Style = "month", NewList = true },
                new() { Kind = "calendar", Style = "events", Height = LineHeight.Exactly(12) },
                PresetItem.Of(PremadeWidgets.CalendarActions),
            ],
        },
        // Windows 11's clock button (2026-10-13, the person: "show the date and time over each other
        // like windows does it and also group the notifications functionality with the bell there as
        // well"): the time over the date with the bell to its right, one button, as 24H2 has them; a
        // click opens what Windows' opens, the notification center over the calendar, one column, a
        // card each. The notifications Fill (2026-10-14, the person: "then you can actually make the
        // notifications widget to grow"; §171): as tall as they are, up to what the calendar leaves
        // under the folder's most (70 % of the screen), scrolling past that, the calendar never
        // pushed off, as Windows' notification center grows up the screen; the calendar its header,
        // the month and the focus session Windows keeps at its foot. No footers: Windows' two cards
        // have none. A new bar's (Sections.DefaultLayout).
        new()
        {
            Id = NotificationCenterId, Glyph = "",
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.DateTimeId),
            MoreFaces = [new(FolderFace.Of(FolderFaces.NotificationsId))],
            Combined = true,
            Items =
            [
                new() { Kind = "notificationsList", Height = LineHeight.Automatic(NotificationCenterCells).As(LineHeightMode.Fill) },
                PresetItem.Divider,
                new() { Kind = "dateHeader" },
                new() { Kind = "calendar", Style = "month" },
                new() { Kind = "focus" },
            ],
        },
        // Today: what the day holds at a glance. The time over the date with the day's events under it
        // and the calendar's footer, beside the weather's three views. Its faces are the time over the
        // date and the weather, as Windows' bar has its clock and its widgets button.
        new()
        {
            Id = TodayId, Glyph = "",
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.DateTimeId),
            MoreFaces = [new(WeatherFace.New())],
            Combined = true,
            Items =
            [
                new() { Kind = "clock", Style = "time" },
                // §620: down a side bar six rows, as the calendar's: ten were to end level with the weather beside them.
                new() { Kind = "calendar", Style = "events", Height = LineHeight.Exactly(TodayEventCells), SideHeight = LineHeight.Exactly(12) },
                PresetItem.Of(PremadeWidgets.CalendarActions),
                new() { Kind = "weatherNow", NewList = true },
                new() { Kind = "hourlyForecast" },
                new() { Kind = "dailyForecast" },
            ],
        },
        // The world clock: this PC's time and three cities' a line each, as the Clock app's world
        // clock lists them — the Americas, Europe and Asia: this PC's two rows, the time large over
        // the date, each city's one, its name, how far ahead or behind and its time. One subject, one card. Its face is the clock, this PC's time; a city's is a face of
        // its own in the folder's settings, the clock face repeating.
        new()
        {
            Id = WorldClockId, Glyph = "",
            Folder = BarKind.FolderList,
            Face = ClockFace.New(),
            Combined = true,
            Items =
            [
                new() { Kind = "clock", Size = LineSize.Full },
                new() { Kind = "clock", Size = LineSize.Compact, Zone = "Eastern Standard Time", Label = "New York" }, // not-ui: a city's name
                new() { Kind = "clock", Size = LineSize.Compact, Zone = "GMT Standard Time", Label = "London" }, // not-ui: a city's name
                new() { Kind = "clock", Size = LineSize.Compact, Zone = "Tokyo Standard Time", Label = "Tokyo" }, // not-ui: a city's name
            ],
        },
        // The media center: now playing with the apps to pick from beside the sound — the volume, the
        // device it plays on, and the volume mixer with the sound's footer. Two lists: what plays,
        // and where it is heard. The lists that grow with what this PC has — the apps playing, the
        // devices, the apps in the mixer — each held to a few rows so the folder stands whole on a
        // 1440p screen. Its faces are what is playing and the speaker.
        new()
        {
            Id = MediaCenterId, Glyph = "",
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.NowPlayingId),
            MoreFaces = [new(FolderFace.Of(FolderFaces.SoundId))],
            Combined = true,
            Items =
            [
                new() { Kind = "media", Size = LineSize.Full },
                new() { Kind = "mediaSources", Height = LineHeight.Automatic(6) },
                new() { Kind = "soundVolume", NewList = true },
                new() { Kind = "soundOutput", Height = LineHeight.Automatic(10) },
                PresetItem.Divider,
                new() { Kind = "soundMixer", Height = LineHeight.Automatic(12) },
                PresetItem.Of(PremadeWidgets.SoundActions),
            ],
        },
        // Quick settings, as Windows 11's own panel is a list of them: what is playing over it, then
        // Wi-Fi and Bluetooth, a line each with its name and what it is on, as the panel's first two
        // tiles; the other tiles, lines and a row of toggles; the brightness and the volume; and on a PC with a battery, the battery under
        // a divider, as the panel's footer has it. No power mode (2026-10-11, the person: "quick
        // settings should not show power mode selector, they should show the wifi, bluetooth"): it is
        // Power & battery's. Its faces are the three status icons Windows' quick settings button
        // shows — the network, the speaker and the battery — the battery's only on a PC with one, as
        // its line. Not the grid preset of that name, which went with the grid folder (2026-10-09).
        new()
        {
            Id = QuickPanelId, Glyph = "",
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.WifiId),
            MoreFaces = [new(FolderFace.Of(FolderFaces.SoundId)), new(FolderFace.Of(FolderFaces.BatteryId), PresetNeed.Battery)],
            Combined = true,
            // Windows 11's own tiles out of the box (2026-10-14, the person: "you should also be able to
            // do the same functionality wise"), every one of them Windows offers this PC (2026-10-16:
            // "add all of the windows default items in the quick settings, like hotspot, live
            // captions, project, display, nearby sharing etc."), in Windows' order: what Windows'
            // panel pins by default (Control Panel\Quick Actions\Control Center) less what Karpo has
            // no switch for (mobile data, Studio effects, a display's color profile, LE audio, audio
            // sharing, mobile devices). Wi-Fi, Bluetooth and accessibility, live captions, cast and
            // project are lines: the first three's ">" opens their page in the folder (FolderPages)
            // — the networks, the devices, the magnifier and its neighbours — cast's and project's
            // all of them, the wireless displays and Win+P's modes, and live captions starts them.
            // Windows' plain on/off tiles are the toggles row's: airplane mode, the VPN, rotation lock
            // (on a screen that turns), energy saver, night light, the mobile hotspot and nearby
            // sharing — the VPN, the hotspot and nearby sharing switches there, their pages a line
            // of theirs away (Edit), so the folder stands whole in the 70 % of a 1440p screen at 150
            // % with what is playing over it. Then the brightness and the volume, whose ">" opens the
            // output devices and the mixer; at the foot the battery and the pencil and the gear:
            // Edit arranges the folder, and All settings opens Windows' Settings.
            //
            // What is playing on top, a card of its own over the tiles (2026-10-16, the person: "show
            // now playing on top of the quick settings as well, like windows does it, with a
            // separator between"): the compact line, the art beside the track and the transport.
            // Windows' media card is there only while something plays; a line stays, saying
            // "Nothing playing", so the folder never changes its height under the pointer.
            Items =
            [
                new() { Kind = "media" },
                PresetItem.Divider,
                new() { Kind = "wifiSwitch" },
                new() { Kind = "bluetoothSwitch" },
                new() { Kind = "accessibility" },
                new() { Kind = "liveCaptions" },
                new() { Kind = "cast" },
                new() { Kind = "project" },
                new()
                {
                    Kind = "toggles",
                    // The person's own: one row of buttons, its whole rows (§432).
                    Toggles = [Toggle.AirplaneMode, Toggle.Vpn, Toggle.RotationLock, Toggle.EnergySaver, Toggle.NightLight, Toggle.MobileHotspot, Toggle.NearbyShare],
                },
                new() { Kind = "brightness" },
                new() { Kind = "soundVolume" },
                // The battery under a divider; none without one, the foot right under the volume
                // as the person has it (2026-10-16).
                PresetItem.Divider with { Needs = PresetNeed.Battery },
                new() { Kind = "battery", Needs = PresetNeed.Battery },
                PresetItem.Of(PremadeWidgets.QuickActions),
            ],
        },
        // Network & internet, as Windows' Settings calls the page: the Wi-Fi's networks with their
        // footer, beside the network's activity, the VPN's connections with theirs and the switches
        // about connecting — airplane mode, the mobile hotspot and nearby sharing — a card each. Its
        // faces are the connection and the VPN's shield.
        new()
        {
            Id = NetworkHubId, Glyph = "",
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.WifiId),
            MoreFaces = [new(FolderFace.Of(FolderFaces.VpnId))],
            Combined = true,
            Items =
            [
                new() { Kind = "wifi", Height = LineHeight.Exactly(16) },
                PresetItem.Of(PremadeWidgets.WifiActions),
                new() { Kind = "performance", Reading = "network", Style = WidgetStyles.GraphWord, Size = LineSize.Compact, NewList = true },
                PresetItem.Divider,
                new() { Kind = "vpnConnections", Height = LineHeight.Automatic(8) },
                PresetItem.Of(PremadeWidgets.VpnActions),
                PresetItem.Divider,
                new() { Kind = "airplaneMode" },
                new() { Kind = "mobileHotspot" },
                new() { Kind = "nearbyShare" },
            ],
        },
        // Bluetooth & devices, as Windows' Settings calls the page: the paired devices with their
        // footer, beside the wireless displays with Display for a footer (Add device is Bluetooth's
        // already) and, a card under them, what is plugged in. Its faces are the rune and the stick.
        new()
        {
            Id = DevicesId, Glyph = "",
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.BluetoothId),
            MoreFaces = [new(FolderFace.Of(FolderFaces.RemovableId))],
            Combined = true,
            Items =
            [
                new() { Kind = "bluetooth", Height = LineHeight.Exactly(16) },
                PresetItem.Of(PremadeWidgets.BluetoothActions),
                new() { Kind = "wirelessDisplays", Height = LineHeight.Automatic(16), NewList = true },
                PresetItem.Of(PremadeWidgets.ProjectActions),
                PresetItem.Divider,
                new() { Kind = "removableDevices", Height = LineHeight.Automatic(6) },
            ],
        },
        // The inbox: what came in, the mail beside the notifications with their footer, each fixed
        // as the clipboard is, so the folder keeps its size as messages come and go. Its faces are
        // the envelope and the bell, each with its count.
        new()
        {
            Id = InboxId, Glyph = "",
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.MailId),
            MoreFaces = [new(FolderFace.Of(FolderFaces.NotificationsId))],
            Combined = true,
            Items =
            [
                new() { Kind = "mail", Height = LineHeight.Exactly(InboxCells) },
                new() { Kind = "notificationsList", Height = LineHeight.Exactly(InboxCells - LineHeight.Row), NewList = true },
                PresetItem.Of(PremadeWidgets.NotificationActions),
            ],
        },
        // Notes & to-do: the to-do list beside a note, each ten rows fixed as the person set them, so
        // the two columns end level. No clipboard history (2026-10-12, the person: "the notes and todo
        // group should not show the clibboard history"): it is a preset of its own. Its icon is the
        // pen: no face says what two pages hold.
        new()
        {
            Id = NotebookId, Glyph = "",
            Folder = BarKind.FolderList,
            Combined = true,
            Items =
            [
                new() { Kind = "todo", Height = LineHeight.Exactly(20) },
                new() { Kind = "note", Height = LineHeight.Exactly(20), NewList = true },
            ],
        },
        // The markets: the Watchlist's shares, wide as the person set it, beside a news feed as tall
        // as the four of them. Its faces
        // are the S&P 500's ticker — the market as a whole — and the newest headline.
        new()
        {
            Id = MarketsNewsId, Glyph = "",
            Folder = BarKind.FolderList,
            Face = MarketFace(),
            MoreFaces = [new(FolderFace.Of(FolderFaces.NewsId))],
            Combined = true,
            Items =
            [
                new() { Kind = "markets", Symbol = "MSFT", Size = LineSize.Full },
                new() { Kind = "markets", Symbol = "AAPL", Size = LineSize.Full },
                new() { Kind = "markets", Symbol = "NVDA", Size = LineSize.Full },
                new() { Kind = "markets", Symbol = "^GSPC", Label = "S&P 500", Size = LineSize.Full }, // not-ui: the index's name
                // §620: down a side bar as tall as two shares: as tall as the four was to end level with them.
                new() { Kind = "rss", Height = LineHeight.Exactly(4 * WatchlistCells), SideHeight = LineHeight.Exactly(2 * WatchlistCells), NewList = true },
            ],
            Widths = [FolderListWidth.Wide, FolderListWidth.Medium],
        },
        // Power & battery: the battery's lines with their footer beside the power buttons, in the
        // order the person put them in the power preset, narrow: a button's name is a word or two.
        // Its faces are the charge and the power glyph.
        new()
        {
            Id = PowerBatteryId, Glyph = "",
            Folder = BarKind.FolderList,
            Face = FolderFace.Of(FolderFaces.BatteryId),
            MoreFaces = [new(FolderFace.Of(FolderFaces.PowerId))],
            Combined = true,
            Items =
            [
                new() { Kind = "battery", Needs = PresetNeed.Battery },
                new() { Kind = "energySaver" },
                new() { Kind = "powerMode" },
                PresetItem.Of(PremadeWidgets.PowerActions),
                new() { Kind = "signOut", NewList = true },
                new() { Kind = "lock" },
                new() { Kind = "sleep", Needs = PresetNeed.Sleep },
                new() { Kind = "restart" },
                new() { Kind = "shutDown" },
            ],
            Widths = [FolderListWidth.Medium, FolderListWidth.Narrow],
        },
    ];

    /// <summary>§641: All apps' cells in the Start group: as tall as the pins, Recommended and the foot beside it.</summary>
    public const int StartAppsCells = 29;

    /// <summary>§641: All apps' cells down a side bar, under the rest; §653 sixteen, so the one list stands whole beside a 1440p screen's side bar at 150 % (PresetSideTests).</summary>
    public const int StartAppsSideCells = 16;

    /// <summary>§641: Start (beta)'s id.</summary>
    public const string StartId = "startGroup";

    /// <summary>Today's events, in cells (two a row, §442): as many as the weather's three views beside them are tall.</summary>
    public const int TodayEventCells = 20;

    /// <summary>The inbox's mail, in cells (two a row), fixed so the folder keeps its size as mail comes and goes; the notifications beside it a row fewer, over their footer.</summary>
    public const int InboxCells = 18;

    /// <summary>The markets' face: the S&amp;P 500's ticker, the market as a whole.</summary>
    private static FolderFace MarketFace()
    {
        var face = FolderFace.Of(FolderFaces.MarketsId);
        // SPX, the ticker the index goes by: the face cuts a name past six letters, "S&P 500" to "S&P 50".
        face.Market = new MarketOptions { Symbol = "^GSPC", Label = "SPX" }; // not-ui: the index's ticker
        return face;
    }

    /// <summary>Today's id: the time over the date and the weather, the day's events and its forecast.</summary>
    public const string TodayId = "todayGroup";

    /// <summary>The media center's id: what is playing beside the sound.</summary>
    public const string MediaCenterId = "mediaCenterGroup";

    /// <summary>
    /// Quick settings' id, the network, sound and battery button of a new bar (Config.FirstLayout), as a list (not "quickSettingsGroup", the grid preset that went with the grid
    /// folder on 2026-10-09).
    /// </summary>
    public const string QuickPanelId = "quickPanelGroup";

    /// <summary>Network &amp; internet's id: Wi-Fi beside the VPN and the switches.</summary>
    public const string NetworkHubId = "networkHubGroup";

    /// <summary>Bluetooth &amp; devices' id.</summary>
    public const string DevicesId = "devicesGroup";

    /// <summary>The inbox's id: the mail beside the notifications.</summary>
    public const string InboxId = "inboxGroup";

    /// <summary>Notes &amp; to-do's id: the to-do list, a note and the clipboard history side by side.</summary>
    public const string NotebookId = "notebookGroup";

    /// <summary>Markets and news' id: the Watchlist's shares beside a news feed.</summary>
    public const string MarketsNewsId = "marketsGroup";

    /// <summary>Power &amp; battery's id.</summary>
    public const string PowerBatteryId = "powerBatteryGroup";

    /// <summary>The world clock's id: this PC's time and three cities'.</summary>
    public const string WorldClockId = "worldClockGroup";

    /// <summary>The clock's id.</summary>
    public const string ClockId = "clockGroup";

    /// <summary>The calendar's id.</summary>
    public const string CalendarId = "calendarGroup";

    /// <summary>meeting: the next meeting on the bar, today's meetings in it (docs\widgets.md §205).</summary>
    public const string MeetingId = "meetingGroup";

    /// <summary>The clock and the calendar together's id, wearing the time over the date.</summary>
    public const string ClockCalendarId = "clockCalendarGroup";

    /// <summary>Windows' clock button's id: the time over the date beside the bell, the notifications over the calendar (Config.FirstLayout).</summary>
    public const string NotificationCenterId = "clockNotificationsGroup";

    /// <summary>The notification center's notifications' cells under Fill: what going back to Automatic finds, the calendar under them and the bar on a 1080p screen at 150 %.</summary>
    public const int NotificationCenterCells = 10;

    /// <summary>The weather's id.</summary>
    public const string WeatherId = "weatherGroup";

    /// <summary>air: the air quality on the bar, its line in it (docs\widgets.md §210).</summary>
    public const string AirQualityId = "airQualityGroup";

    /// <summary>tides: the next high or low on the bar, the tides line in it (docs\widgets.md §236).</summary>
    public const string TidesId = "tidesGroup";

    /// <summary>sun: the next sunrise or sunset on the bar, the sun line in it (docs\widgets.md §368).</summary>
    public const string SunId = "sunGroup";
    /// <summary>sports: a team's score on the bar, its line in it (docs\widgets.md §239).</summary>
    public const string SportsId = "sportsGroup";
    /// <summary>quakes: the latest earthquake near a place on the bar, its line in it (docs\widgets.md §240).</summary>
    public const string QuakesId = "quakesGroup";
    /// <summary>The network's id.</summary>
    public const string WifiId = "wifiGroup";

    /// <summary>Bluetooth's id.</summary>
    public const string BluetoothId = "bluetoothGroup";

    /// <summary>The sound's id.</summary>
    public const string SoundId = "soundGroup";

    /// <summary>The mobile hotspot's id.</summary>
    public const string HotspotId = "hotspotGroup";

    /// <summary>Cast's id.</summary>
    public const string CastId = "castGroup";

    /// <summary>Nearby sharing's id: a file's nearby sharing button becomes it.</summary>
    public const string NearbyId = "nearbyGroup";

    /// <summary>The battery's id.</summary>
    public const string BatteryId = "batteryGroup";

    /// <summary>The power button's id.</summary>
    public const string PowerId = "powerGroup";

    /// <summary>The virtual desktops' id: the desktop switcher's preset, which a file's switcher becomes.</summary>
    public const string DesktopsId = "desktopsGroup";

    /// <summary>The mail's id.</summary>
    public const string MailId = "mailGroup";

    /// <summary>The notifications' id.</summary>
    public const string NotificationsId = "notificationsGroup";

    /// <summary>The clipboard history's id.</summary>
    public const string ClipboardId = "clipboardGroup";

    /// <summary>The contacts' id.</summary>
    public const string ContactsId = "contactsGroup";

    /// <summary>§642: Windows search's id: a folder whose face is a search box, holding the search's line.</summary>
    public const string WinSearchId = "winSearchGroup";

    /// <summary>The keyboard's id: the default bar wears it where Windows' input indicator is (Config.FirstLayout).</summary>
    public const string KeyboardId = "keyboardGroup";

    /// <summary>The VPN's id.</summary>
    public const string VpnId = "vpnGroup";

    /// <summary>Project's id (Win+P's modes).</summary>
    public const string ProjectId = "projectGroup";

    /// <summary>The taskbar layouts' id: the layout switcher's preset, which a file's switcher becomes.</summary>
    public const string LayoutsId = "layoutsGroup";

    /// <summary>The media's id: what is playing.</summary>
    public const string MediaId = "mediaGroup";

    /// <summary>The processor's id: its reading's line at its tallest, over Task Manager.</summary>
    public const string CpuId = "cpuGroup";

    /// <summary>The graphics' id.</summary>
    public const string GpuId = "gpuGroup";

    /// <summary>The memory's id.</summary>
    public const string MemoryId = "memoryGroup";

    /// <summary>The disk's id.</summary>
    public const string DiskId = "diskGroup";

    /// <summary>The network activity's id (not "networkGroup": the network's is Wi-Fi's).</summary>
    public const string NetworkActivityId = "networkActivityGroup";

    /// <summary>The removable devices' id.</summary>
    public const string RemovableId = "removableGroup";

    /// <summary>The timer's id.</summary>
    public const string TimerId = "timerGroup";

    /// <summary>The focus session's id (not "focusGroup", which is Getting work done's).</summary>
    public const string FocusId = "focusTimeGroup";

    /// <summary>The stopwatch's id.</summary>
    public const string StopwatchId = "stopwatchGroup";

    /// <summary>The markets' id, written "stockGroup" as the stock's was: the shares, the Watchlist that was "stocksGroup" with them (§648).</summary>
    public const string MarketId = "stockGroup";

    /// <summary>The news' id: a feed's line, its headlines panel.</summary>
    public const string NewsId = "newsGroup";

    /// <summary>The drive's id: a drive's line over a folder pin to its root, its flyout.</summary>
    public const string DriveId = "driveGroup";

    /// <summary>The network drive's id.</summary>
    public const string NetworkDriveId = "networkDriveGroup";

    /// <summary>The Watchlist's cells a share took (three rows, 2026-10-11): the news beside its four shares is four times as tall.</summary>
    public const int WatchlistCells = 6;

    /// <summary>
    /// The cells the drive presets keep for a drive's line, as the person's own folders have them: three rows, which
    /// its Full form (three cells since §442) fits in. The folder pin under it has the rest.
    /// </summary>
    public const int DriveCells = 6;

    /// <summary>
    /// The cells the processor's preset gives its top processes (four rows): four apps, automatic, which with the
    /// graph and the details over them and the footer under them stands whole in the 70 % of a 1440p
    /// screen at 150 % a folder may take (five until §432 gave the details their whole rows).
    /// </summary>
    public const int ProcessCells = 8;

    /// <summary>
    /// A reading's preset: its graph, its details and the Task Manager footer, under the reading's
    /// face; the processor's has its top processes over the footer (2026-10-12), Task Manager's
    /// processes sorted by CPU.
    /// </summary>
    private static Preset Reading(string id, Metric metric, string face, string? glyph = null) => new()
    {
        Id = id, Glyph = glyph ?? WidgetInfos.For(metric).Glyph,
        Folder = BarKind.FolderList,
        Face = FolderFace.Of(face),
        Items =
        [
            new() { Kind = WidgetStyles.Performance.Id, Reading = WidgetStyles.FileReading(WidgetInfos.For(metric).Id), Style = WidgetStyles.GraphWord, Size = LineSize.Full },
            new() { Kind = WidgetStyles.PerformanceDetails.Id, Reading = WidgetStyles.FileReading(WidgetInfos.For(metric).Id) },
            .. metric == Metric.Cpu ? [new PresetItem { Kind = WidgetInfos.CpuProcesses.Id, Height = LineHeight.Automatic(ProcessCells) }] : (PresetItem[])[],
            PresetItem.Of(PremadeWidgets.TaskManagerActions),
        ],
    };

    /// <summary>The preset <paramref name="id"/> names, or null.</summary>
    public static Preset? Find(string? id) =>
        All.FirstOrDefault(preset => string.Equals(preset.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// The presets a place of <paramref name="type"/> offers, in <see cref="All"/>'s order: every one
    /// on the bar, and none in a folder. A preset is one folder, and folders do not nest: in a folder
    /// it could only be its widgets loose, which is not one folder.
    /// </summary>
    public static IReadOnlyList<Preset> On(WidgetType type) =>
        type == WidgetType.Bar ? All : [];

    /// <summary>
    /// What a preset is found by besides its name: its line, its search words and the names of the
    /// widgets in it, so "cpu" finds System and "volume" finds Quick settings; and in another
    /// language, its English name and line as well. Not its dividers': "divider" finds no group.
    /// </summary>
    public static IReadOnlyList<string> WordsOf(Preset preset) =>
        [preset.Blurb, .. preset.Keywords, .. preset.Items.Where(item => !item.IsDivider).Select(item => WidgetInfos.Find(item.Form)?.Title ?? "").Where(title => title.Length > 0).Distinct(), .. preset.EnglishWords];

    /// <summary>
    /// The widgets of <paramref name="preset"/> as this PC has them: what it lacks left out
    /// (<see cref="PresetItem.Needs"/>), and a drive's line made once for each fixed drive
    /// (<see cref="PresetItem.EachFixedDrive"/>), each pointed at its drive. A divider needing what
    /// this PC lacks goes with the line it heads. A divider stands only between two lines: one that
    /// what is left out would leave first, last or against another divider goes too. A list whose head is left out starts at the line after it.
    /// </summary>
    public static IReadOnlyList<(PresetItem Item, DriveVolume? Drive)> ItemsOn(Preset preset, PresetMachine machine, Edge edge = Edge.Bottom)
    {
        var items = new List<(PresetItem Item, DriveVolume? Drive)>();
        var newList = false;
        void Add(PresetItem item, DriveVolume? drive)
        {
            if (newList || item.NewList)
            {
                // A list neither ends nor starts with a divider.
                if (items.Count > 0 && items[^1].Item.IsDivider)
                {
                    items.RemoveAt(items.Count - 1);
                }
                item = item.NewList || items.Count == 0 ? item : item with { NewList = true };
                newList = false;
            }
            items.Add((item, drive));
        }
        foreach (var item in preset.ItemsFor(edge))
        {
            if (item.IsDivider)
            {
                if (machine.Has(item.Needs) && items.Count > 0 && !items[^1].Item.IsDivider && !newList)
                {
                    items.Add((item, null));
                }
                continue;
            }
            if (!machine.Has(item.Needs))
            {
                newList |= item.NewList && items.Count > 0;
                continue;
            }
            if (item.EachFixedDrive)
            {
                var first = true;
                foreach (var drive in machine.FixedDrives)
                {
                    // Only the first heads a list: the other drives stand under it.
                    Add(first ? item : item with { NewList = false }, drive);
                    first = false;
                }
                newList |= first && item.NewList && items.Count > 0;
                continue;
            }
            Add(item, null);
        }
        if (items.Count > 0 && items[^1].Item.IsDivider)
        {
            items.RemoveAt(items.Count - 1);
        }
        return items;
    }

    /// <summary>
    /// The widgets <paramref name="preset"/> makes for its folder, each new with an id of its own and
    /// its settings, the lists' lines in their order, left to right. A recipe naming a widget a list
    /// has no line of throws: it is a
    /// mistake in <see cref="All"/>, which a test holds, not something a file can say.
    /// </summary>
    public static List<FolderCell> Cells(Preset preset, PresetMachine machine, Edge edge = Edge.Bottom) => [.. Lists(preset, machine, edge).SelectMany(list => list)];

    /// <summary>
    /// The same in its lists (<see cref="PresetItem.NewList"/>), left to right, each its lines top
    /// to bottom: the folder's <see cref="FolderOptions.Lists"/>. At least one.
    /// </summary>
    public static List<List<FolderCell>> Lists(Preset preset, PresetMachine machine, Edge edge = Edge.Bottom)
    {
        List<List<FolderCell>> lists = [[]];
        foreach (var (item, drive) in ItemsOn(preset, machine, edge))
        {
            var island = ListWidgets.Find(item.Form)?.Kind.New()
                ?? throw new InvalidOperationException($"the preset \"{preset.Id}\" holds a \"{item.Form}\", which a list folder has no line of.");
            Set(island, item, drive, machine);
            if (item.NewList && lists[^1].Count > 0)
            {
                lists.Add([]);
            }
            lists[^1].Add(new FolderCell { Island = island });
        }
        return lists;
    }

    /// <summary>
    /// The folder a preset makes, not yet on any bar: its kind, its glyph and its button wearing that
    /// glyph (<see cref="FolderFaces.Icon"/>), and its widgets in their lists (<see cref="Lists"/>) at
    /// their widths, as a bar on <paramref name="edge"/> arranges them (§620: one list down a side bar,
    /// <see cref="Preset.ItemsFor"/>). No name: a folder has none (§427).
    /// </summary>
    public static BarItem Folder(Preset preset, PresetMachine machine, Edge edge = Edge.Bottom)
    {
        var holder = preset.Folder.New();
        var folder = holder.Folder!;
        // Its own icon, not its first four widgets' in a 2×2: a group made at once has no first
        // widgets anybody chose to show, and its glyph says what the whole of it is.
        folder.Faces = [preset.Face?.Clone() ?? new FolderFace { Glyph = preset.Glyph },
            .. preset.MoreFaces.Where(face => machine.Has(face.Needs)).Select(face => face.Face.Clone())];
        folder.Lists = Lists(preset, machine, edge);
        if (preset.WidthsFor(edge) is { Count: > 0 } widths)
        {
            folder.Widths = [.. widths];
        }
        holder.Normalize();
        return holder;
    }

    /// <summary>
    /// Adds <paramref name="preset"/> to the bar <paramref name="list"/> at <paramref name="slot"/>:
    /// its one folder, in the section of the slot (<see cref="Sections.Put"/>), the folder and every
    /// widget in it with an id no island of the list or its folders has, arranged for a bar on <paramref name="edge"/>
    /// (§620). The folder's id, the one path added.
    /// </summary>
    public static string AddToBar(List<BarItem> list, BarSlot slot, Preset preset, PresetMachine machine, Edge edge = Edge.Bottom)
    {
        var folder = Folder(preset, machine, edge);
        var taken = Folders.Ids(list);
        folder.Id = Fresh(taken);
        foreach (var cell in folder.Folder!.Cells)
        {
            cell.Island.Id = Fresh(taken);
        }
        Sections.Put(list, folder, slot);
        return folder.Id;
    }

    /// <summary>What the recipe sets on a new widget: a share's symbol and name, a line's height, a drive, and a folder pin to a drive's root.</summary>
    private static void Set(WidgetItem island, PresetItem item, DriveVolume? drive, PresetMachine machine)
    {
        if (item.Symbol is { Length: > 0 } symbol)
        {
            island.Market = new MarketOptions { Symbol = symbol, Label = item.Label ?? "" };
        }
        item.Premade?.Apply(island);
        if (item.Zone is { Length: > 0 } zone && island.Clock is { } clock)
        {
            (clock.TimeZone, clock.Label) = (zone, item.Label ?? "");
        }
        if (item.Toggles is { } toggles)
        {
            // Each where this PC has it, as Windows offers a tile: rotation lock on a turning screen.
            island.Toggles = new TogglesOptions { Shown = [.. toggles.Where(toggle => machine.Has(NeedOf(toggle)))] };
        }
        if (drive is { } volume)
        {
            island.Drive = new DriveTargetOptions { Volume = volume.Volume, Letter = volume.Letter, Label = volume.Label };
        }
        if (item.DriveContents)
        {
            island.Pin = new PinOptions
            {
                Target = drive is { } root ? DriveTargets.ContentsFolder(new DriveTargetOptions { Volume = root.Volume, Letter = root.Letter }) : "",
            };
        }
        if (item.Height is { } height && island is CellItem { View: FolderView.List })
        {
            island.Height = height;
        }
        if (item.Size is { } size && island is CellItem { View: FolderView.List })
        {
            island.Size = size;
        }
        island.Normalize();
    }

    /// <summary>What this PC must have for a preset's toggles row to hold <paramref name="toggle"/>.</summary>
    private static PresetNeed NeedOf(Toggle toggle) => toggle == Toggle.RotationLock ? PresetNeed.Rotation : PresetNeed.None;

    /// <summary>
    /// A new id none of <paramref name="taken"/> is, and taken from then on: a preset's widgets
    /// arrive several at once, and must not meet each other's ids any more than the bar's.
    /// </summary>
    private static string Fresh(HashSet<string> taken)
    {
        var id = WidgetItem.NewId();
        while (!taken.Add(id))
        {
            id = WidgetItem.NewId();
        }
        return id;
    }
}
