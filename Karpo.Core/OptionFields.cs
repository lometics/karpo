// menus: the settings a person types or searches for, as rows a menu can show (2026-10-19, the person:
// "inline every setting of every bar widget and folder widget into its edit menu, so configuring
// happens in the menu"; docs\widgets.md §219). The text and search kinds of OptionRows.cs:
//
//  - **A line of text** (OptionRowKind.Text): a clock's label, a timer's, a focus session's and an
//    alarm's name, a heading's words, a share's name on the bar, a pin's name, a clock
//    face's label. Set as the page's box sets it — trimmed, held to the box's length — on Enter or as
//    its field is left, empty too: empty is its default. Where there is anything to offer (the others'
//    labels and names, a clock's zone's places; an amount's, §332) it is offered as it is typed in, the
//    words typed still the value (OptionSuggestions.cs, §304, §332).
//  - **A search** (OptionRowKind.Search): a time zone among Windows' (this PC's first, then the zones
//    the person's other clocks show, most used first), a place for the weather and the air quality
//    (Open-Meteo's places; "Use my device's location" is a switch over its search, §240), an RSS feed's
//    address (the person's other feeds and well-known ones offered, §304; read as a feed before it is
//    kept). An OptionFinder finds and proves; the row keeps.
//
// A markets widget's search and rows are its own (MarketRows.cs, MarketSearch.cs, §258).
//
// Every write is the one the settings app's pages made (IslandOptions, FolderRows, gone since §228):
// the same function where the page had one (WeatherPlaces.Choose, OptionTexts).

using System.Globalization;
using System.Text.Json;

namespace Karpo.Core;

/// <summary>A row a search field offers: its words, a quieter line under them, and what it stands for.</summary>
/// <param name="Value">What a pick keeps: a <see cref="Place"/>, a <see cref="Listing"/> or an <see cref="Instrument"/>, a <see cref="ZoneEntry"/>, an address.</param>
public sealed record OptionFound(string Title, string Detail = "", object? Value = null)
{
    /// <summary>Whether it is what the setting is now: ticked, as a menu ticks its choice.</summary>
    public bool Chosen { get; init; }

    /// <summary>An icon's glyph, drawn where a menu row's icon stands (an icon search's); empty for none.</summary>
    public string Glyph { get; init; } = "";

    /// <summary>
    /// §369: a picture's address, drawn where the row's icon stands once it has come (a team's or a
    /// league's logo, which the dock fetches and keeps: Flyout.Pictures), the <see cref="Glyph"/> until then; empty for none.
    /// </summary>
    public string Picture { get; init; } = "";

    /// <summary>§380: the heading it stands under, as a menu's group ("Folders", "Apps"); empty for none.</summary>
    public string Group { get; init; } = "";
}

/// <summary>
/// §380: what a pin's and a folder's contents' Target offers, and how it names a target — the dock asks this
/// PC's shell (Karpo.Dock\TargetLooks.cs), a test hands in its own. Each row's value is the target it keeps,
/// and its picture the target's (<see cref="PinTargets.PictureOf"/>). §425: a pin's, kind by kind.
/// </summary>
public sealed record TargetChoices
{
    /// <summary>The folders offered first: what the person's pins and folders show, then Windows' own, by File Explorer's names.</summary>
    public IReadOnlyList<OptionFound> Folders { get; init; } = [];

    /// <summary>§425: Windows' own places a pin may open as a folder, over the folders (This PC): no folder on disk, so never a folder's contents'.</summary>
    public IReadOnlyList<OptionFound> Places { get; init; } = [];

    /// <summary>§425: the files used lately, newest first, as Windows' Recent folder has them.</summary>
    public IReadOnlyList<OptionFound> Files { get; init; } = [];

    /// <summary>§425: the websites the person's pins open (<see cref="OptionFields.SitesInUse"/>).</summary>
    public IReadOnlyList<OptionFound> Sites { get; init; } = [];

    /// <summary>The apps, as Start's All apps lists them, each by its name; a pin's alone.</summary>
    public IReadOnlyList<OptionFound> Apps { get; init; } = [];

    /// <summary>The name Windows gives a target ("File Explorer" for its shortcut, "Spotify" for its app's ID); empty where it has none.</summary>
    public Func<string, string>? Name { get; init; }

    /// <summary>§425: whether a path is a folder, which tells a folder from a file (<see cref="PinTargets.KindOf"/>); null for by its extension.</summary>
    public Func<string, bool>? IsFolder { get; init; }

    /// <summary><paramref name="target"/>'s name: Windows', else the one it is called before Windows is asked (<see cref="PinTargets.NameOf"/>).</summary>
    public string NameOf(string target) =>
        target.Trim().Length > 0 && Name?.Invoke(target) is { Length: > 0 } named ? named : PinTargets.NameOf(target);

    /// <summary>A folder offered: its name, its path beside it and as its value, its picture, and <paramref name="glyph"/> until that has come.</summary>
    public static OptionFound Row(string name, string path, string glyph = OptionGlyphs.Folder) =>
        new(name, path, path) { Glyph = glyph, Picture = PinTargets.PictureOf(path) };

    /// <summary>§425: a file offered: its name, its path beside it and as its value, and its picture.</summary>
    public static OptionFound File(string name, string path) => Row(name, path, OptionGlyphs.Document);

    /// <summary>§425: a website offered: its host, its address as its value, and the site's picture.</summary>
    public static OptionFound Site(string name, string address) =>
        new(name, "", address) { Glyph = OptionGlyphs.Globe, Picture = PinTargets.PictureOf(address) };

    /// <summary>An app offered: its name alone — never its ID — its target as its value, and its picture.</summary>
    public static OptionFound App(string name, string target) =>
        new(name, "", target) { Glyph = OptionGlyphs.Apps, Picture = PinTargets.PictureOf(target) };
}

/// <summary>What proving a pick came to: the thing to keep (<paramref name="Found"/>), or why not (<paramref name="Said"/>).</summary>
public readonly record struct OptionChecked(OptionFound? Found, string? Said = null);

/// <summary>
/// How a search field finds what it offers and proves a pick before it is kept: the menu's field asks
/// it as the person types (a moment after the last key, <see cref="Wait"/>), off the menu's thread.
/// </summary>
public abstract class OptionFinder
{
    /// <summary>Fewer letters than this ask nothing: only <see cref="Offered"/> shows.</summary>
    public virtual int LeastLetters => 1;

    /// <summary>How long the field waits after the last key before it asks; nothing for a finder that asks no service.</summary>
    public virtual TimeSpan Wait => TimeSpan.Zero;

    /// <summary>Whether it asks a service (and so a "Searching…" row stands while it does).</summary>
    public virtual bool Remote => false;

    /// <summary>Whether it finds anything as it is typed in (its field shows a magnifier), rather than only proving what is entered (a feed's address).</summary>
    public virtual bool Finds => true;

    /// <summary>
    /// §332: whether what it offers only suggests — the words typed are the value, a line of text's
    /// (SuggestFinder) —, rather than being what a pick keeps (a zone, a place, a feed proved): its field
    /// keeps its words as it is left, and Enter keeps them, as a line with nothing to offer does.
    /// </summary>
    public virtual bool Suggests => false;

    /// <summary>The rows offered before what is found, whatever is typed: an action's "None".</summary>
    public virtual IReadOnlyList<OptionFound> Offered => [];

    /// <summary>
    /// §230: the rows offered after <see cref="Offered"/> while too little is typed to ask (fewer than
    /// <see cref="LeastLetters"/>): a place's or a share's own and the ones the person's other widgets
    /// show. A finder that needs no letters finds its own for nothing typed instead.
    /// </summary>
    public virtual IReadOnlyList<OptionFound> Suggested => [];

    /// <summary>What <paramref name="query"/> finds, best first. May throw what <see cref="Expected"/> allows.</summary>
    public abstract Task<IReadOnlyList<OptionFound>> FindAsync(string query, CancellationToken cancel);

    /// <summary>What Enter keeps of the words typed without a pick: a symbol written out, an address; null where they are a search.</summary>
    public virtual OptionChecked? Typed(string text) => null;

    /// <summary>The words while <paramref name="found"/> is proved; null where it needs no proving.</summary>
    public virtual string? Checking(OptionFound found) => null;

    /// <summary><paramref name="found"/> proved before it is kept: the symbol looked up, the feed read.</summary>
    public virtual Task<OptionChecked> CheckAsync(OptionFound found, CancellationToken cancel) => Task.FromResult(new OptionChecked(found));

    /// <summary>What a search that found nothing for <paramref name="query"/> says.</summary>
    public abstract string NoneFound(string query);

    /// <summary>What a search or a check that could not ask says.</summary>
    public virtual string Failed => S.SettingsWidgetOffline;

    /// <summary>The exceptions asking may fail with: it could not ask. Anything else is a bug worth seeing.</summary>
    public virtual bool Expected(Exception error) =>
        error is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException;
}

/// <summary>A feed's address as it was read and proved one, and what it called itself.</summary>
public sealed record ProvedFeed(string Address, string Title);

/// <summary>The text and search rows of a widget and a face; see the top of the file.</summary>
public static class OptionFields
{
    // A widget's rows, by their keys: OptionRows'.

    public const string Heading = OptionRows.HeadingText;
    public const string ClockZone = OptionRows.ClockZone;
    public const string ClockLabel = OptionRows.ClockName;
    public const string CountdownName = OptionRows.CountdownName;
    public const string AlarmName = OptionRows.AlarmName;
    public const string WeatherPlace = OptionRows.WeatherPlace;
    public const string RssAddress = OptionRows.RssAddress;
    public const string CameraUrl = OptionRows.CameraUrl;
    public const string CameraName = OptionRows.CameraName;
    public const string PinName = "PinName";

    /// <summary>A weather face's place, which its device switch heads (§240).</summary>
    public const string FacePlace = "place";

    /// <summary>The most a pin's name may be, as its box allows.</summary>
    public const int PinNameLength = 60;

    /// <summary>The most a name of a timer or a focus session may be, as its box allows.</summary>
    public const int NameLength = 40;

    /// <summary>
    /// <paramref name="item"/>'s text and search rows of its rows (OptionRows.Of, where they stand in
    /// its page's order). <paramref name="zones"/>: the zones the person's clocks show, most used first
    /// (<see cref="ZonesInUse"/>).
    /// </summary>
    public static IReadOnlyList<OptionRow> OfWidget(WidgetItem item, IReadOnlyList<string>? zones = null) =>
        [.. OptionRows.Of(item, new OptionDevices { Zones = zones }).Where(row => row.Kind is OptionRowKind.Text or OptionRowKind.Search)];

    // Each row of a widget's, as OptionRows.Lines puts it among the rest. Each reads what the widget
    // shows (a record it has none of yet read as a new one) and writes where it is kept, made only then.

    /// <summary>heading: its words.</summary>
    public static OptionRow OfHeading(WidgetItem item, IReadOnlyDictionary<string, IReadOnlyList<OptionFound>>? words = null) =>
        Text(Heading, S.SettingsWidgetCountdownNameHeader, item.Heading?.Text ?? "", S.SettingsWidgetHeadingTextPlaceholder, HeadingOptions.MostLength, typed => OptionTexts.SetHeading(item, typed), WordsInUse.For(words, Heading)) with { Glyph = OptionGlyphs.Font };

    /// <summary>clock: its zone, searched for among Windows'.</summary>
    public static OptionRow OfClockZone(WidgetItem item, IReadOnlyList<string>? zones) =>
        Zone(ClockZone, item.Clock?.TimeZone ?? "", zones, id => OptionTexts.SetClockZone(item, id));

    /// <summary>clock: its label; offered the other clocks' labels, then the places of its zone (§304).</summary>
    public static OptionRow OfClockLabel(WidgetItem item, IReadOnlyDictionary<string, IReadOnlyList<OptionFound>>? words = null) =>
        Text(ClockLabel, S.SettingsWidgetCountdownNameHeader, item.Clock?.Label ?? "", S.SettingsWidgetClockLabelPlaceholder, int.MaxValue, typed => OptionTexts.SetClockLabel(item, typed),
            [.. WordsInUse.For(words, ClockLabel), .. WordsInUse.ZonePlaces(item.Clock?.TimeZone)]) with { Empty = S.SettingsWidgetActionsNone };

    /// <summary>A timer's or a focus session's name, its kind's title for none; offered the others' names (§304).</summary>
    public static OptionRow OfCountdownName(WidgetItem item, IReadOnlyDictionary<string, IReadOnlyList<OptionFound>>? words = null) =>
        Text(CountdownName, S.SettingsWidgetCountdownNameHeader, item.Countdown?.Name ?? "", item.Info?.Title ?? "", NameLength, typed => OptionTexts.SetCountdownName(item, CountdownLength.Name(typed)), WordsInUse.For(words, CountdownName));

    /// <summary>alarm: its name, its kind's title for none; offered the other alarms' names (§304).</summary>
    public static OptionRow OfAlarmName(WidgetItem item, IReadOnlyDictionary<string, IReadOnlyList<OptionFound>>? words = null) =>
        Text(AlarmName, S.SettingsWidgetCountdownNameHeader, item.Alarm?.Name ?? "", item.Info?.Title ?? "", AlarmOptions.MostName, typed => OptionTexts.SetAlarmName(item, typed), WordsInUse.For(words, AlarmName));

    /// <summary>weather, air quality: its place, under its device switch (OptionRows.WeatherUsesDevice, §240).</summary>
    public static OptionRow OfWeatherPlace(WidgetItem item, IReadOnlyList<Place>? places = null) =>
        Place(WeatherPlace, item.Weather ?? new WeatherOptions(), places, () => item.Weather ??= new WeatherOptions()) with { Detail = Credit(OpenMeteoLines.Contains(item.Info)) };

    /// <summary>§630: the lines whose data is Open-Meteo's forecast, air-quality or marine answer; a place's other lines (the sun's, the earthquakes') compute or fetch theirs elsewhere.</summary>
    private static readonly HashSet<WidgetInfo?> OpenMeteoLines =
    [
        WidgetInfos.WeatherNow, WidgetInfos.WeatherHours, WidgetInfos.WeatherDays, WidgetInfos.WeatherHoursList, WidgetInfos.WeatherDaysGraph,
        WidgetInfos.AirQuality, WidgetInfos.AirPollutants, WidgetInfos.AirPollen, WidgetInfos.AirForecast, WidgetInfos.Tides, WidgetInfos.TideGraph,
    ];

    /// <summary>§630: Open-Meteo's credit (its CC BY 4.0 licence asks for it) where the place's data is its; null else.</summary>
    private static string? Credit(bool openMeteo) => openMeteo ? S.SettingsWidgetWeatherSource : null;

    /// <summary>rss: its feed's address, read as a feed before it is kept; offered the person's other feeds, then well-known ones (§304).</summary>
    public static OptionRow OfRssAddress(WidgetItem item, IReadOnlyDictionary<string, IReadOnlyList<OptionFound>>? words = null) =>
        OfFeed(item.Rss, () => item.Rss ??= new RssOptions(), words);

    /// <summary>
    /// rss: the feed <paramref name="rss"/> is, kept in what <paramref name="kept"/> makes where there is none yet: a
    /// feed line's, a news face's own (FaceSources, §477) — the one feed picker.
    /// </summary>
    public static OptionRow OfFeed(RssOptions? rss, Func<RssOptions> kept, IReadOnlyDictionary<string, IReadOnlyList<OptionFound>>? words = null) =>
        Feed(RssAddress, rss ?? new RssOptions(), WordsInUse.For(words, RssAddress), kept);

    /// <summary>
    /// camera (§331): its stream's address; offered the person's other cameras, then public ones
    /// (<see cref="KnownCameras"/>), its own ticked; an address typed kept as typed. Which way it is read
    /// is found when it is shown, not here: a camera need not answer while its menu is open.
    /// </summary>
    public static OptionRow OfCameraUrl(WidgetItem item, IReadOnlyDictionary<string, IReadOnlyList<OptionFound>>? words = null)
    {
        var camera = item.Camera ?? new CameraOptions();
        return new OptionRow(CameraUrl, OptionRowKind.Search, S.CoreCameraUrlTitle)
        {
            Glyph = OptionGlyphs.Camera,
            Slot = OptionSlot.Subject,
            Field = OptionField.Text,
            Text = camera.Url,
            Summary = camera.IsSet ? Cameras.NameOf(camera) : null,
            Placeholder = S.CoreCameraUrlPlaceholder,
            Finder = new CameraFinder(camera.Url, [.. WordsInUse.For(words, CameraUrl), .. KnownCameras.For()]),
            Pick = found => found.Value is string address && OptionTexts.SetCamera(item.Camera ??= camera, address),
        };
    }

    /// <summary>camera: its name; empty for a known camera's or its host, which its field says while empty; offered the other cameras' names (§304).</summary>
    public static OptionRow OfCameraName(WidgetItem item, IReadOnlyDictionary<string, IReadOnlyList<OptionFound>>? words = null)
    {
        var camera = item.Camera ?? new CameraOptions();
        return Text(CameraName, S.SettingsWidgetCountdownNameHeader, camera.Name ?? "", Cameras.NameOf(new CameraOptions { Url = camera.Url }), CameraOptions.MostName,
            typed => OptionTexts.SetCameraName(item.Camera ??= camera, typed), WordsInUse.For(words, CameraName));
    }

    /// <summary>
    /// sports: what it follows (docs\widgets.md §239, §369): a team searched for among ESPN's in the
    /// table's leagues, or a league of the table itself; with nothing typed the leagues to browse, and a
    /// league's name typed the league and every team of it, each with its logo.
    /// </summary>
    public static OptionRow OfSportsTeam(WidgetItem item) => OfSportsTeam(item.Sports, () => item.Sports ??= new SportsOptions());

    /// <summary>sports: what <paramref name="sports"/> follows, kept in what <paramref name="kept"/> makes: a sports line's, a sports face's own (§477).</summary>
    public static OptionRow OfSportsTeam(SportsOptions? sports, Func<SportsOptions> kept)
    {
        var team = sports ?? new SportsOptions();
        return new OptionRow(OptionRows.SportsTeam, OptionRowKind.Search, S.SettingsWidgetSportsTeamHeader)
        {
            Glyph = OptionGlyphs.Sports,
            Slot = OptionSlot.Subject,
            Field = OptionField.Search,
            Text = team.IsSet ? team.Name : "",
            Placeholder = S.SettingsWidgetSportsTeamPlaceholder,
            Finder = new FollowFinder(team.IsSet ? team.Key : ""),
            Pick = found => found.Value switch
            {
                SportsTeam picked => OptionTexts.SetTeam(kept(), picked),
                SportsLeague league => OptionTexts.SetLeague(kept(), league),
                _ => false,
            },
        };
    }

    /// <summary>pin: its name; empty for the name Windows gives what it opens (<paramref name="targets"/>'), which its field says while empty (§292).</summary>
    public static OptionRow OfPinName(PinOptions pin, TargetChoices? targets = null) =>
        Text(PinName, S.SettingsPinNameHeader, pin.Name, (targets ?? new TargetChoices()).NameOf(pin.Target), PinNameLength, typed => OptionTexts.SetPinName(pin, typed));

    /// <summary>
    /// A folder's contents: their folder (§292, the person: "path and what it opens should be combined"), one
    /// row: its name at the row's end, as Windows names it (§380); its level "Browse…" (Windows' open dialog,
    /// <see cref="OptionPickers.PinBrowse"/>) over the path in a field, typed or found — the folders under a
    /// path as it is typed, <paramref name="targets"/>' folders by their names, those for nothing typed.
    /// </summary>
    public static IReadOnlyList<OptionRow> OfFolderTarget(PinOptions pin, TargetChoices? targets)
    {
        var choices = targets ?? new TargetChoices();
        return
        [
            Kind(pin.Target, PinSetter(pin), OptionPickers.PinTarget, S.SettingsFolderContentsTargetHeader, OptionGlyphs.Folder, S.SettingsFolderContentsTargetPlaceholder,
                new TargetFinder(pin.Target, choices.Folders)) with { Summary = Said(pin.Target, choices), Empty = S.SettingsPinTargetNone },
            OptionPickers.Browse(pin),
        ];
    }

    /// <summary>What sets a pin's target: typed, found or browsed, normalized (<see cref="OptionTexts.SetPinTarget"/>).</summary>
    private static Func<string, bool> PinSetter(PinOptions pin) => target => OptionTexts.SetPinTarget(pin, target);

    /// <summary>
    /// §425: a pin's Target (the person: "for the pin widget it should be more clear to add an app, folder,
    /// website, etc. ... put them under separate menus"), one row saying what it opens — its name as Windows
    /// names it (§380) and its picture — opening a choice of kind, each a level of its own, the kind in force
    /// ticked and saying the target: App, Start's apps found by name; Folder, "Browse…" for a folder over This
    /// PC and the folders, a path typed; File, "Browse…" for a file over the files used lately, a path typed;
    /// Website, its address typed and proved; Settings page, Windows' Settings' pages found by name. A pick
    /// goes back to the kinds, as a choice of one does (§392).
    /// </summary>
    public static OptionRow OfPinTarget(PinOptions pin, TargetChoices? targets) => OfTarget(pin.Target, PinSetter(pin), targets);

    /// <summary>
    /// §425: what opens <paramref name="target"/> (a pin's; §532 a shortcut's of the taskbar's menu), set by
    /// <paramref name="set"/>, as <see cref="OfPinTarget"/> has it: the kinds, each a level of its own. <paramref name="prefix"/>
    /// before each of its rows' keys, so several of them stand on one page apart (the taskbar menu's shortcuts).
    /// </summary>
    public static OptionRow OfTarget(string target, Func<string, bool> set, TargetChoices? targets, string prefix = "")
    {
        var choices = targets ?? new TargetChoices();
        var kind = PinTargets.KindOf(target, choices.IsFolder);
        var said = Said(target, choices);
        OptionRow Of(PinKind which, OptionRow row) => row with { Ticked = which == kind, Summary = which == kind ? said : null };
        var site = kind == PinKind.Website ? target : "";
        return new(prefix + OptionPickers.PinTarget, OptionRowKind.Level, S.SettingsPinTargetHeader)
        {
            Glyph = OptionGlyphs.Pin,
            Slot = OptionSlot.Subject,
            Summary = said,
            Empty = S.SettingsPinTargetNone,
            ValuePicture = said is null ? null : PinTargets.PictureOf(target),
            Level =
            [
                Of(PinKind.App, Kind(target, set, prefix + OptionPickers.PinApp, S.SettingsPinKindApp, OptionGlyphs.Apps, S.SettingsPinAppPlaceholder, new NamedFinder(target, choices.Apps))),
                OptionPickers.Browse(target, set, prefix + OptionPickers.PinFolderBrowse, prefix + OptionPickers.PinFolder, TargetBrowse.Folder),
                Of(PinKind.Folder, Kind(target, set, prefix + OptionPickers.PinFolder, S.SettingsPinKindFolder, OptionGlyphs.Folder, S.SettingsFolderContentsTargetPlaceholder,
                    new TargetFinder(target, [.. choices.Places, .. choices.Folders]))),
                OptionPickers.Browse(target, set, prefix + OptionPickers.PinFileBrowse, prefix + OptionPickers.PinFile, TargetBrowse.File),
                Of(PinKind.File, Kind(target, set, prefix + OptionPickers.PinFile, S.SettingsPinKindFile, OptionGlyphs.Document, S.SettingsPinFilePlaceholder, new TargetFinder(target, choices.Files))),
                Of(PinKind.Website, Kind(target, set, prefix + OptionPickers.PinSite, S.SettingsPinKindWebsite, OptionGlyphs.Globe, S.SettingsPinWebsitePlaceholder, new SiteFinder(site, choices.Sites)) with { Text = site }),
                Of(PinKind.SettingsPage, Kind(target, set, prefix + OptionPickers.PinSetting, S.SettingsPinKindSettings, OptionGlyphs.Settings, S.SettingsPinSettingsPlaceholder, new PageFinder(target))),
            ],
        };
    }

    /// <summary>What a pin's or a folder's contents' Target says: its target's name; null for nothing yet.</summary>
    private static string? Said(string target, TargetChoices choices) => target.Trim().Length > 0 ? choices.NameOf(target) : null;

    /// <summary>A kind of what a pin opens: a search's row, <paramref name="finder"/> under its field, a pick its target.</summary>
    private static OptionRow Kind(string target, Func<string, bool> set, string key, string title, string glyph, string placeholder, OptionFinder finder) =>
        new(key, OptionRowKind.Search, title)
        {
            Glyph = glyph,
            Slot = OptionSlot.Subject,
            Field = OptionField.Text,
            Text = target,
            Placeholder = placeholder,
            Finder = finder,
            Pick = found => found.Value is string picked && set(picked),
        };

    /// <summary>
    /// §292, §380: what the person's pins and folders in <paramref name="config"/> show that is a folder
    /// (<paramref name="isFolder"/>), each once, in the order first met, named as Windows names it
    /// (<paramref name="name"/>; its last part where it says none): the folders a picker offers first.
    /// </summary>
    public static IReadOnlyList<OptionFound> PinsInUse(Config config, Func<string, bool> isFolder, Func<string, string>? name = null)
    {
        var names = new TargetChoices { Name = name };
        return [.. config.Layouts.SelectMany(layout => Folders.All(layout.Islands))
            .Select(each => each.Island.Pin?.Target?.Trim() ?? "")
            .Where(target => PinFolders.PathOf(target) is { Length: > 0 } path && isFolder(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(target => TargetChoices.Row(names.NameOf(target), target, OptionGlyphs.Pin))];
    }

    /// <summary>§425: the websites the person's pins in <paramref name="config"/> open, each once, in the order first met, by their hosts: what a pin's Website offers.</summary>
    public static IReadOnlyList<OptionFound> SitesInUse(Config config) =>
        [.. config.Layouts.SelectMany(layout => Folders.All(layout.Islands))
            .Select(each => each.Island.Pin?.Target?.Trim() ?? "")
            .Where(target => PinTargets.KindOf(target) == PinKind.Website)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(target => TargetChoices.Site(PinTargets.NameOf(target), target))];

    /// <summary>actions: the page of Windows' Settings a slot opens, searched for among the actions; "None" first.</summary>
    public static OptionRow OfAction(WidgetItem item, string key, string title, bool left)
    {
        var id = left ? item.Actions?.Left : item.Actions?.Right;
        return new OptionRow(key, OptionRowKind.Search, title)
        {
            Glyph = left ? OptionGlyphs.Left : OptionGlyphs.Right,
            Slot = OptionSlot.Subject,
            Field = OptionField.Search,
            Text = SettingsActions.Find(id)?.Name ?? S.SettingsWidgetActionsNone,
            Placeholder = S.SettingsWidgetActionsSearch,
            Finder = new ActionFinder(SettingsActions.Find(id)?.Id),
            Pick = found => OptionTexts.SetAction(item, left, found.Value as SettingsAction),
        };
    }

    /// <summary>A face's line of text (<see cref="FaceOptionKind.Text"/>): set as the folder page's box sets it, on Enter; a clock's label offered as a clock line's is (§304).</summary>
    public static OptionRow OfText(FolderFace face, FaceOption option, string title, IReadOnlyDictionary<string, IReadOnlyList<OptionFound>>? words = null)
    {
        var label = face.Kind == FolderFaces.ClockId && option.Key == ClockFace.Label;
        IEnumerable<OptionFound> offered = label ? [.. WordsInUse.For(words, ClockLabel), .. WordsInUse.ZonePlaces(face.Info.Option(ClockFace.Zone) is { } zone ? face.Text(zone) : "")] : [];
        return Text(option.Key, title, face.Text(option), label ? S.SettingsWidgetClockLabelPlaceholder : "", FaceOption.TextLength, typed =>
        {
            if (face.Text(option) == typed.Trim())
            {
                return false;
            }
            face.Set(option, typed);
            return true;
        }, offered) with { Empty = label ? S.SettingsWidgetActionsNone : null };
    }

    /// <summary>A face's time zone (<see cref="FaceOptionKind.Zone"/>): a search, set as the folder page's list sets it.</summary>
    public static OptionRow OfZone(FolderFace face, FaceOption option, string title, IReadOnlyList<string>? zones) =>
        Zone(option.Key, face.Text(option), zones, id =>
        {
            if (string.Equals(face.Text(option), id, StringComparison.Ordinal))
            {
                return false;
            }
            face.Set(option, id);
            return true;
        }) with { Title = title };

    /// <summary>The weather face's and the air quality face's place: a search, under its device switch (§240).</summary>
    public static OptionRow OfPlace(FolderFace face, IReadOnlyList<Place>? places = null) => Place(FacePlace, face.Weather ?? new WeatherOptions(), places, () => FaceRows.PlaceKept(face))
        with { Detail = Credit(face.Kind is FolderFaces.WeatherId or FolderFaces.AirQualityId or FolderFaces.TidesId) };

    /// <summary>
    /// The zones <paramref name="config"/>'s clocks show — their lines' and their faces' —, most used
    /// first, then in the order first met; this PC's (empty) is not one.
    /// </summary>
    public static IReadOnlyList<string> ZonesInUse(Config config)
    {
        var counted = new Dictionary<string, (int Count, int First)>(StringComparer.OrdinalIgnoreCase);
        void Count(string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }
            var trimmed = id.Trim();
            counted[trimmed] = counted.TryGetValue(trimmed, out var had) ? (had.Count + 1, had.First) : (1, counted.Count);
        }
        foreach (var (_, item) in config.Layouts.SelectMany(layout => Folders.All(layout.Islands)))
        {
            Count(item.Clock?.TimeZone);
            // A clock's, a date's, the time and date's (§230).
            foreach (var face in (item as BarItem)?.Folder?.Faces ?? [])
            {
                foreach (var option in face?.Info.Options.Where(option => option.Kind == FaceOptionKind.Zone) ?? [])
                {
                    Count(face!.Text(option));
                }
            }
        }
        return [.. counted.OrderByDescending(pair => pair.Value.Count).ThenBy(pair => pair.Value.First).Select(pair => pair.Key)];
    }

    /// <summary>
    /// §230: the places <paramref name="config"/>'s weather and air quality show — their lines' and
    /// their faces' —, each once, in the order first met; a device's location is not one.
    /// </summary>
    public static IReadOnlyList<Place> PlacesInUse(Config config)
    {
        var found = new List<Place>();
        void Add(WeatherOptions? weather)
        {
            if (weather is not null && Own(weather) is { } place && !found.Exists(other => other.Label == place.Label))
            {
                found.Add(place);
            }
        }
        foreach (var (_, item) in config.Layouts.SelectMany(layout => Folders.All(layout.Islands)))
        {
            Add(item.Weather);
            foreach (var face in (item as BarItem)?.Folder?.Faces ?? [])
            {
                Add(face?.Weather);
            }
        }
        return found;
    }

    /// <summary>
    /// §230: what <paramref name="config"/>'s markets widgets show — their lines' and their faces', shares,
    /// pairs and coins (§258) —, each once, in the order first met, named as the service named them when they
    /// were picked.
    /// </summary>
    public static IReadOnlyList<Listing> SharesInUse(Config config)
    {
        var found = new List<Listing>();
        void Add(MarketOptions? market)
        {
            if (market is { IsSet: true } && !found.Exists(share => string.Equals(share.Symbol, market.Symbol, StringComparison.OrdinalIgnoreCase)))
            {
                found.Add(MarketRows.Share(market));
            }
        }
        foreach (var (_, item) in config.Layouts.SelectMany(layout => Folders.All(layout.Islands)))
        {
            Add(item.Market);
            foreach (var face in (item as BarItem)?.Folder?.Faces ?? [])
            {
                Add(face?.Market);
            }
        }
        return found;
    }

    /// <summary>
    /// §230: the place <paramref name="weather"/> shows where it is one of its own (not the device's
    /// location, unless <paramref name="any"/>); null for none. Its town as its name and where it is as
    /// its detail, as the search lists it (§283), its coordinates where it has no words.
    /// </summary>
    private static Place? Own(WeatherOptions weather, bool any = false)
    {
        if (weather is not { Latitude: { } latitude, Longitude: { } longitude } || (weather.UseDeviceLocation && !any))
        {
            return null;
        }
        var (name, detail) = PlaceNames.Parts(weather.Place);
        return name.Length > 0
            ? new Place(name, detail, latitude, longitude)
            : new Place(PlaceQuery.CoordinatesText(latitude, longitude), "", latitude, longitude);
    }

    // --- The rows ----------------------------------------------------------------------------------

    /// <summary>A line of text; offered <paramref name="offered"/> as it is typed in where there is anything else to offer (§304: SuggestFinder), a plain field where not.</summary>
    private static OptionRow Text(string key, string title, string text, string placeholder, int longest, Func<string, bool> set, IEnumerable<OptionFound>? offered = null) =>
        new(key, OptionRowKind.Text, title)
        {
            Glyph = OptionGlyphs.Name,
            Slot = OptionSlot.Words,
            Field = OptionField.Text,
            Text = text,
            Placeholder = placeholder,
            Longest = longest,
            SetText = set,
            Finder = SuggestFinder.Of(text, offered ?? []),
        };

    private static OptionRow Zone(string key, string zone, IReadOnlyList<string>? zones, Func<string, bool> set) =>
        new(key, OptionRowKind.Search, S.SettingsWidgetClockZoneHeader)
        {
            Glyph = OptionGlyphs.Zone,
            Slot = OptionSlot.Subject,
            Field = OptionField.Zone,
            Text = ZoneFinder.Name(zone),
            // §230: at its row's end only its offset now, "UTC+1"; the field and its results the whole name.
            Summary = TimeZoneText.Short(zone, DateTimeOffset.Now),
            Placeholder = S.SettingsWidgetClockZonePlaceholder,
            Finder = new ZoneFinder(zone, zones ?? []),
            Pick = found => found.Value is ZoneEntry entry && set(entry.Id),
        };

    // A place's rows read what is shown (a face that has none yet reads a new one) and write where it is
    // kept, made only then (FaceRows.PlaceKept): showing the menu changes nothing in the file.

    /// <summary>
    /// A place's search: the place it has in its field, greyed while the device's location stands
    /// instead (its switch, <see cref="OptionRow.Within"/>; §240), which its row then says it is.
    /// §630: under its name, where its widget's data is Open-Meteo's, the credit Open-Meteo's CC BY 4.0 licence asks for
    /// (<see cref="Credit"/>; About Karpo no longer gives it).
    /// </summary>
    private static OptionRow Place(string key, WeatherOptions weather, IReadOnlyList<Place>? places, Func<WeatherOptions> kept) =>
        new(key, OptionRowKind.Search, S.SettingsWidgetWeatherPlaceHeader)
        {
            Glyph = OptionGlyphs.Place,
            Slot = OptionSlot.Subject,
            Field = OptionField.Place,
            Text = PlaceText(weather),
            Summary = weather.UseDeviceLocation ? DeviceSummary() : null,
            Placeholder = S.SettingsWidgetWeatherPlacePlaceholder,
            Finder = new PlaceFinder(Own(weather), places ?? []),
            Pick = found => OptionTexts.SetPlace(kept(), found),
        };

    private static OptionRow Feed(string key, RssOptions rss, IReadOnlyList<OptionFound> used, Func<RssOptions> kept) =>
        new(key, OptionRowKind.Search, S.SettingsRssAddressHeader)
        {
            Glyph = OptionGlyphs.Feed,
            Slot = OptionSlot.Subject,
            Field = OptionField.Text,
            Text = rss.Address,
            Placeholder = S.SettingsRssAddressPlaceholder,
            Finder = new FeedFinder(rss.Address, [.. used, .. KnownFeeds.For()]),
            Pick = found => found.Value is ProvedFeed feed && OptionTexts.SetFeed(kept(), feed.Address, feed.Title),
        };

    /// <summary>
    /// §253: what a place's row says while the device's location stands instead: where the device is,
    /// short, "Zürich (this device)"; "Device location" until that is known.
    /// </summary>
    private static string DeviceSummary() =>
        PlaceNames.Device is { } name && !PlaceNames.DeviceOff ? S.DockEditDeviceLocationAt(PlaceNames.Short(name)) : S.DockEditDeviceLocation;

    /// <summary>A place as its field shows it at rest: its town (§283), its coordinates where it has no words, empty with none.</summary>
    private static string PlaceText(WeatherOptions weather) => Own(weather, any: true)?.Name ?? "";

    // --- The finders ---------------------------------------------------------------------------------

    /// <summary>Windows' time zones, as the page's zone box searches them: this PC's first, then the ones in use (<paramref name="used"/>).</summary>
    private sealed class ZoneFinder(string current, IReadOnlyList<string> used) : OptionFinder
    {
        public override int LeastLetters => 0;

        /// <summary>A zone as its field shows it at rest: this PC's, or Windows' name for it.</summary>
        public static string Name(string id) =>
            id.Length == 0 ? S.SettingsWidgetClockZoneLocal
            : TimeZoneSearch.Windows.FirstOrDefault(zone => string.Equals(zone.Id, id, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? id;

        public override Task<IReadOnlyList<OptionFound>> FindAsync(string query, CancellationToken cancel) => Task.FromResult(Find(query));

        public override string NoneFound(string query) => S.DockEditZoneNotFound(query);

        private IReadOnlyList<OptionFound> Find(string query)
        {
            // Every zone that matches, as the menu shows them all (§271); every zone for nothing typed.
            var all = TimeZoneSearch.Windows;
            var typed = query.Trim();
            var local = new ZoneEntry("", S.SettingsWidgetClockZoneLocal, "", TimeZoneInfo.Local.BaseUtcOffset);
            IEnumerable<ZoneEntry> found;
            if (typed.Length == 0)
            {
                // Nothing typed: this PC's, then the person's own, most used first, then every other in
                // Windows' order, by offset (§230: the rows show before anything is typed).
                List<ZoneEntry> mine = [.. used.Select(id => all.FirstOrDefault(zone => string.Equals(zone.Id, id, StringComparison.OrdinalIgnoreCase))).OfType<ZoneEntry>().Distinct()];
                found = [local, .. mine, .. all.Except(mine)];
            }
            else
            {
                var matches = TimeZoneSearch.Find(all, typed, all.Count);
                var mine = matches.Where(zone => used.Contains(zone.Id, StringComparer.OrdinalIgnoreCase)).OrderBy(zone => IndexOf(used, zone.Id));
                found = [.. local.DisplayName.Contains(typed, StringComparison.CurrentCultureIgnoreCase) ? [local] : Array.Empty<ZoneEntry>(), .. mine, .. matches.Except(mine)];
            }
            var now = DateTimeOffset.Now;
            return [.. found.Select(zone => new OptionFound(
                zone.Id.Length == 0 ? zone.DisplayName : string.Join(", ", TimeZoneSearch.Places(zone.DisplayName)),
                // Under the places its offset now, as its row's end says it (§230); this PC's its places too.
                zone.Id.Length == 0 ? $"{TimeZoneText.Short(TimeZoneInfo.Local, now)} · {string.Join(", ", TimeZoneSearch.Places(TimeZoneInfo.Local.DisplayName))}" : TimeZoneText.Short(zone.Id, now),
                zone)
            {
                Chosen = string.Equals(zone.Id, current, StringComparison.OrdinalIgnoreCase),
            })];
        }

        private static int IndexOf(IReadOnlyList<string> ids, string id)
        {
            for (var at = 0; at < ids.Count; at++)
            {
                if (string.Equals(ids[at], id, StringComparison.OrdinalIgnoreCase))
                {
                    return at;
                }
            }
            return ids.Count;
        }
    }

    /// <summary>§411: the great cities a place's search offers before anything is typed: their names, their countries' codes, where they are.</summary>
    private static readonly (string Name, string Country, double Latitude, double Longitude)[] PopularCities =
    [
        ("London", "GB", 51.5085, -0.1257), // not-ui: a city's name
        ("New York", "US", 40.7143, -74.006), // not-ui: a city's name
        ("Paris", "FR", 48.8534, 2.3488), // not-ui: a city's name
        ("Berlin", "DE", 52.5244, 13.4105), // not-ui: a city's name
        ("Zürich", "CH", 47.3667, 8.55), // not-ui: a city's name
        ("Tokyo", "JP", 35.6895, 139.6917), // not-ui: a city's name
        ("Sydney", "AU", -33.8679, 151.2073), // not-ui: a city's name
        ("São Paulo", "BR", -23.5475, -46.6361), // not-ui: a city's name
        ("Mumbai", "IN", 19.0728, 72.8826), // not-ui: a city's name
        ("Singapore", "SG", 1.2897, 103.8501), // not-ui: a city's name
        ("Toronto", "CA", 43.7001, -79.4163), // not-ui: a city's name
        ("Cape Town", "ZA", -33.9258, 18.4232), // not-ui: a city's name
    ];

    /// <summary>Open-Meteo's places, as the page's place box finds them.</summary>
    /// <param name="own">The place it shows now; null for none or the device's.</param>
    /// <param name="used">The places the person's widgets show (<see cref="PlacesInUse"/>).</param>
    private sealed class PlaceFinder(Place? own, IReadOnlyList<Place> used) : OptionFinder
    {
        private const int PlaceCount = 20;

        public override int LeastLetters => PlaceQuery.LeastLetters;

        public override TimeSpan Wait => PlaceQuery.Wait;

        public override bool Remote => true;

        /// <summary>
        /// §230: before anything is typed, its own place, ticked, then the others' — nothing asked of the service; §411:
        /// then the world's great cities, so the search lists something before anything is typed.
        /// </summary>
        public override IReadOnlyList<OptionFound> Suggested
        {
            get
            {
                List<Place> listed = [.. own is null ? Array.Empty<Place>() : [own], .. used.Where(place => place.Label != own?.Label)];
                listed.AddRange(Cities().Where(city => !listed.Exists(place => place.Name == city.Name)));
                return [.. listed.Select(place => new OptionFound(place.Name, place.Detail, place) { Chosen = place == own })];
            }
        }

        /// <summary>§411: the cities a place's search offers after the person's own, their countries in Karpo's words.</summary>
        private static IEnumerable<Place> Cities() =>
            from city in PopularCities
            select new Place(city.Name, HolidayPlaces.Name(city.Country), city.Latitude, city.Longitude);

        public override async Task<IReadOnlyList<OptionFound>> FindAsync(string query, CancellationToken cancel)
        {
            // §368: a latitude and a longitude typed are the place, asked of nobody; off the Earth, nothing.
            if (PlaceQuery.Coordinates(query) is { } typed)
            {
                return typed.InRange
                    ? [new OptionFound(PlaceQuery.CoordinatesText(typed.Latitude, typed.Longitude), S.SettingsWidgetWeatherPlaceCoordinates, new Place("", "", typed.Latitude, typed.Longitude))]
                    : [];
            }
            // More than the page's five: the menu shows every one, scrolling (§271).
            var places = await Places.SearchAsync(PlaceQuery.Clean(query), cancel, PlaceCount).ConfigureAwait(false);
            return [.. places.Select(place => new OptionFound(place.Name, place.Detail, place))];
        }

        public override string NoneFound(string query) =>
            PlaceQuery.Coordinates(query) is { InRange: false } ? S.SettingsWidgetWeatherPlaceOutOfRange : S.SettingsWidgetWeatherPlaceNotFound(PlaceQuery.Clean(query));
    }

    /// <summary>
    /// An RSS or Atom feed's address, read as a feed before it is kept (§304): <paramref name="offered"/>
    /// — the person's other feeds, then well-known ones — found by their names, sites or addresses; an
    /// address typed a row of its own first; the one it follows (<paramref name="current"/>) ticked.
    /// </summary>
    private sealed class FeedFinder(string current, IReadOnlyList<OptionFound> offered) : SuggestFinder(current, offered)
    {
        /// <summary>A feed is kept once it is read and proved one: what is typed is an address to prove, not the value.</summary>
        public override bool Suggests => false;

        /// <summary>An address typed, as a row of its own: words with a dot, a colon or a slash; anything else words to find a feed by.</summary>
        protected override OptionFound? Own(string typed) =>
            typed.IndexOfAny(['.', ':', '/']) >= 0 && Feeds.Address(typed) is { } address ? KnownFeeds.Row(Feeds.HostOf(address), address, address) : null;

        public override OptionChecked? Typed(string text) =>
            Feeds.Address(text) is { } address ? new OptionChecked(new OptionFound(address, "", address))
            : text.Trim().Length > 0 ? new OptionChecked(null, S.SettingsRssAddressNotAddress)
            : null;

        public override string? Checking(OptionFound found) => S.SettingsRssAddressChecking(Feeds.HostOf(found.Value as string));

        public override async Task<OptionChecked> CheckAsync(OptionFound found, CancellationToken cancel)
        {
            if (found.Value is not string address)
            {
                return new OptionChecked(null, S.SettingsRssAddressNotAddress);
            }
            try
            {
                var feed = await FeedCheck.ReadAsync(address, cancel).ConfigureAwait(false);
                return new OptionChecked(found with { Value = new ProvedFeed(address, feed.Title) });
            }
            catch (FeedException)
            {
                return new OptionChecked(null, S.SettingsRssAddressNotAFeed);
            }
        }

        public override string Failed => S.SettingsRssAddressUnreachable;
    }

    /// <summary>
    /// camera (§331): a camera's address: <paramref name="offered"/> — the person's other cameras, then public
    /// ones — found by their names, places or addresses; an address typed a row of its own first; the one it
    /// shows (<paramref name="current"/>) ticked. Nothing is asked of the camera before it is kept.
    /// </summary>
    private sealed class CameraFinder(string current, IReadOnlyList<OptionFound> offered) : SuggestFinder(current, offered)
    {
        /// <summary>An address typed, as a row of its own: words with a dot, a colon or a slash; anything else words to find a camera by.</summary>
        protected override OptionFound? Own(string typed) =>
            typed.IndexOfAny(['.', ':', '/']) >= 0 && Cameras.Address(typed) is { } address ? KnownCameras.Row(Cameras.HostOf(address), address, address) : null;

        public override OptionChecked? Typed(string text) =>
            Cameras.Address(text) is { } address ? new OptionChecked(new OptionFound(address, "", address))
            : text.Trim().Length > 0 ? new OptionChecked(null, S.CoreCameraUrlNotAddress)
            : null;
    }

    /// <summary>
    /// sports (§369): what a sports widget may follow, the one followed ticked. With fewer letters than a
    /// search needs, the table's leagues to browse, each with its logo and its sport. Typed: the leagues
    /// it names first, then every team of the first of them (ESPN's list of the league's teams), then the
    /// teams ESPN's search finds, each once and with its logo. A league's teams or the search failing
    /// leaves the rest standing; all of it failing says so.
    /// </summary>
    internal sealed class FollowFinder(string current, Func<SportsLeague, CancellationToken, Task<List<SportsTeam>>>? teamsOf = null, Func<string, CancellationToken, Task<List<SportsTeam>>>? search = null) : OptionFinder
    {
        public override int LeastLetters => SportsSearch.LeastLetters;

        public override TimeSpan Wait => SportsSearch.Wait;

        public override bool Remote => true;

        public override IReadOnlyList<OptionFound> Suggested => [.. SportsLeagues.All.Select(League)];

        public override async Task<IReadOnlyList<OptionFound>> FindAsync(string query, CancellationToken cancel)
        {
            var words = SymbolQuery.Clean(query);
            var leagues = SportsSearch.Leagues(words);
            var found = new List<OptionFound>(leagues.Select(League));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            void Add(IEnumerable<SportsTeam> teams)
            {
                foreach (var team in teams.Where(team => seen.Add($"{team.Sport}/{team.League}/{team.Id}")))
                {
                    found.Add(Team(team));
                }
            }
            if (leagues.Count > 0)
            {
                try
                {
                    Add(await (teamsOf ?? SportsSearch.TeamsOfAsync)(leagues[0], cancel).ConfigureAwait(false));
                }
                catch (Exception error) when (Expected(error) && !cancel.IsCancellationRequested)
                {
                    // The league's row stands; the search below may still find its teams.
                }
            }
            try
            {
                Add(await (search ?? SportsSearch.FindAsync)(words, cancel).ConfigureAwait(false));
            }
            catch (Exception error) when (Expected(error) && !cancel.IsCancellationRequested && found.Count > 0)
            {
                // What was found stands.
            }
            return found;
        }

        private OptionFound League(SportsLeague league) =>
            new(league.Name, SportsLeagues.SportName(league.Sport), league)
            {
                Chosen = $"{league.Sport}/{league.League}" == current,
                Glyph = OptionGlyphs.Sports,
                Picture = league.Logo,
            };

        private OptionFound Team(SportsTeam team) =>
            new(team.Name, SportsSearch.Detail(team), team)
            {
                Chosen = $"{team.Sport}/{team.League}/{team.Id}" == current,
                Glyph = OptionGlyphs.Sports,
                Picture = team.Logo,
            };

        public override string NoneFound(string query) => S.SettingsWidgetSportsTeamNotFound(SymbolQuery.Clean(query));
    }

    /// <summary>The pages of Windows' Settings an actions line's slot opens, as the page's picker finds them: "None" first, each under its section.</summary>
    private sealed class ActionFinder(string? current) : OptionFinder
    {
        public override int LeastLetters => 0;

        public override IReadOnlyList<OptionFound> Offered => [new OptionFound(S.SettingsWidgetActionsNone) { Chosen = current is null }];

        public override Task<IReadOnlyList<OptionFound>> FindAsync(string query, CancellationToken cancel) =>
            Task.FromResult<IReadOnlyList<OptionFound>>([.. SettingsActions.Search(query).SelectMany(group => group.Actions.Select(action =>
                new OptionFound(action.Name, group.Section.Title(), action) { Chosen = action.Id == current }))]);

        public override string NoneFound(string query) => S.SettingsWidgetActionsNoMatch;
    }

    /// <summary>
    /// §425: a pin's apps, found by their names: <paramref name="offered"/> for nothing typed, the one in force
    /// ticked; typed, those whose name holds the words. Words typed are never a target of their own.
    /// </summary>
    internal sealed class NamedFinder(string current, IReadOnlyList<OptionFound> offered) : SuggestFinder(current, offered)
    {
        /// <summary>A pick of what is found, never the words: they go back as they are left.</summary>
        public override bool Suggests => false;

        protected override OptionFound? Own(string typed) => null;

        protected override bool Holds(OptionFound row, string typed) => row.Title.Contains(typed, StringComparison.CurrentCultureIgnoreCase);
    }

    /// <summary>
    /// §425: a pin's website, typed: a line of text opening with the address in force, the sites the person's pins
    /// open offered under it; an address typed is a row of its own first, its host and its site's picture, which
    /// Enter keeps (<see cref="PinTargets.Site"/>: a bare domain https's); words that are no address say so.
    /// </summary>
    internal sealed class SiteFinder(string current, IReadOnlyList<OptionFound> offered) : SuggestFinder(current, offered)
    {
        public override OptionChecked? Typed(string text) =>
            text.Trim().Length == 0 ? null
            : PinTargets.Site(text) is { } address ? new OptionChecked(Row(address))
            : new OptionChecked(null, S.SettingsPinWebsiteInvalid);

        protected override OptionFound? Own(string typed) => PinTargets.Site(typed) is { } address ? Row(address) : null;

        /// <summary>Words that are no address, and no site offered holds: said under the field as they are typed.</summary>
        public override string NoneFound(string query) => S.SettingsPinWebsiteInvalid;

        private static OptionFound Row(string address) => TargetChoices.Site(PinTargets.NameOf(address), address);
    }

    /// <summary>
    /// §425: the pages of Windows' Settings a pin may open (<see cref="SettingsActions.Pages"/>), each by its name in
    /// Settings' own words with its section beside it, found as an actions line's are (by its name, its section's, its
    /// English name); every one for nothing typed, the one in force ticked.
    /// </summary>
    internal sealed class PageFinder(string current) : OptionFinder
    {
        public override int LeastLetters => 0;

        public override Task<IReadOnlyList<OptionFound>> FindAsync(string query, CancellationToken cancel) =>
            Task.FromResult<IReadOnlyList<OptionFound>>([.. SettingsActions.Search(query)
                .SelectMany(group => group.Actions.Select(action => (group.Section, Page: SettingsActions.Page(action.Uri))))
                .Where(each => each.Page is not null)
                .DistinctBy(each => each.Page!.Uri, StringComparer.OrdinalIgnoreCase)
                .Select(each => new OptionFound(each.Page!.Name, each.Section.Title(), each.Page.Uri)
                {
                    Glyph = each.Page.Glyph.Length > 0 ? each.Page.Glyph : OptionGlyphs.Settings,
                    Chosen = string.Equals(each.Page.Uri, current.Trim(), StringComparison.OrdinalIgnoreCase),
                })]);

        public override string NoneFound(string query) => S.DockMenuNoMatch;
    }

    /// <summary>
    /// pins: what a pin opens (§292). Nothing typed, <paramref name="folders"/>. Typed, what is typed
    /// where it reads as a target of its own (a path, an address), then the folders under a path as far
    /// as it is typed ("C:\Us" → "C:\Users"), then <paramref name="folders"/> whose name or path holds the
    /// words; the one it opens now ticked. Enter keeps the words typed where nothing was found. A
    /// field's suggestions as every other's are (§304, SuggestFinder), with the disk's folders.
    /// </summary>
    internal sealed class TargetFinder(string current, IReadOnlyList<OptionFound> folders) : SuggestFinder(current, folders)
    {
        /// <summary>The most folders a path lists under it.</summary>
        public const int MostFolders = 50;

        /// <summary>What a pin opens is a search's pick, as §292 has it: words left in its field go back.</summary>
        public override bool Suggests => false;

        public override Task<IReadOnlyList<OptionFound>> FindAsync(string query, CancellationToken cancel) =>
            // The disk is asked off the menu's thread; nothing typed asks nothing.
            query.Trim() is { Length: > 0 } typed ? Task.Run(() => Ticked(Find(typed, cancel)), cancel) : base.FindAsync(query, cancel);

        public override OptionChecked? Typed(string text) => PinTargets.Normalize(text) is { Length: > 0 } target ? new OptionChecked(Row(target)) : null;

        public override bool Expected(Exception error) => base.Expected(error) || error is IOException or UnauthorizedAccessException;

        protected override OptionFound? Own(string typed) => OwnTarget(typed) ? Row(PinTargets.Normalize(typed)) : null;

        protected override IEnumerable<OptionFound> More(string typed, CancellationToken cancel) => Under(typed, cancel);

        protected override string Same(string value) => value.Trim().TrimEnd('\\');

        /// <summary>§380: a row holds what is typed in its name or its path; an app's ID ("shell:AppsFolder\…") is no word of its.</summary>
        protected override bool Holds(OptionFound row, string typed) =>
            row.Title.Contains(typed, StringComparison.CurrentCultureIgnoreCase) || row.Detail.Contains(typed, StringComparison.CurrentCultureIgnoreCase);

        /// <summary>Whether <paramref name="typed"/> reads as a target of its own rather than words to find a folder by: a path, an address.</summary>
        internal static bool OwnTarget(string typed) => typed.IndexOfAny([':', '\\', '/', '.', '%']) >= 0;

        /// <summary>
        /// The folders on this PC's own disks under <paramref name="typed"/> as far as it goes: those in
        /// "C:\Users\" , those of "C:\Us" whose name starts so; none on a network, where asking may wait
        /// for it, and none hidden.
        /// </summary>
        internal static IReadOnlyList<OptionFound> Under(string typed, CancellationToken cancel = default)
        {
            var path = PinFolders.PathOf(typed);
            if (path.Length < 3 || path[1] != ':' || path[2] != '\\' || !char.IsAsciiLetter(path[0]))
            {
                return [];
            }
            try
            {
                if (new DriveInfo(path[..3]).DriveType is DriveType.Network or DriveType.NoRootDirectory)
                {
                    return [];
                }
                var whole = path.EndsWith('\\');
                var folder = whole ? path : Path.GetDirectoryName(path);
                var start = whole ? "" : Path.GetFileName(path);
                if (folder is null || !Directory.Exists(folder))
                {
                    return [];
                }
                List<OptionFound> under = [];
                foreach (var each in new DirectoryInfo(folder).EnumerateDirectories(start + "*"))
                {
                    cancel.ThrowIfCancellationRequested();
                    if ((each.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                    {
                        continue;
                    }
                    under.Add(new OptionFound(each.Name, each.FullName, each.FullName) { Glyph = OptionGlyphs.Folder, Picture = PinTargets.PictureOf(each.FullName) });
                    if (under.Count == MostFolders)
                    {
                        break;
                    }
                }
                return under;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
            {
                return [];
            }
        }

        /// <summary>What is typed as a row: its name, and the target it keeps under it.</summary>
        private static OptionFound Row(string target) => new(PinTargets.NameOf(target), target, target) { Glyph = OptionGlyphs.Open, Picture = PinTargets.PictureOf(target) };
    }
}

/// <summary>
/// The writes a widget's text settings make, one for the settings app's boxes (IslandOptions) and the
/// fields of its menu (<see cref="OptionFields"/>): each says whether anything changed.
/// </summary>
public static class OptionTexts
{
    /// <summary>A heading's words: one line, held to <see cref="HeadingOptions.MostLength"/>.</summary>
    public static bool SetHeading(WidgetItem item, string typed)
    {
        var text = (typed ?? "").ReplaceLineEndings(" ").Trim();
        text = text.Length <= HeadingOptions.MostLength ? text : text[..HeadingOptions.MostLength].TrimEnd();
        if ((item.Heading?.Text ?? "") == text)
        {
            return false;
        }
        (item.Heading ??= new HeadingOptions()).Text = text;
        return true;
    }

    /// <summary>A clock's zone: a Windows id, empty for this PC's.</summary>
    public static bool SetClockZone(WidgetItem item, string id)
    {
        if (item.Clock is not { } clock || clock.TimeZone == id)
        {
            return false;
        }
        clock.TimeZone = id;
        return true;
    }

    /// <summary>A clock's label, trimmed.</summary>
    public static bool SetClockLabel(WidgetItem item, string typed)
    {
        var label = (typed ?? "").Trim();
        if (item.Clock is not { } clock || clock.Label == label)
        {
            return false;
        }
        clock.Label = label;
        return true;
    }

    /// <summary>A timer's or a focus session's name, trimmed; its record made with its length where it had none.</summary>
    public static bool SetCountdownName(WidgetItem item, string typed)
    {
        var name = (typed ?? "").Trim();
        if ((item.Countdown?.Name ?? "") == name)
        {
            return false;
        }
        (item.Countdown ??= new CountdownOptions { Minutes = CountdownOptions.Clamp(null, Minutes(item)) }).Name = name;
        return true;
    }

    /// <summary>An alarm's name, trimmed.</summary>
    public static bool SetAlarmName(WidgetItem item, string typed)
    {
        var name = (typed ?? "").Trim();
        var alarm = item.Alarm ??= new AlarmOptions();
        if (alarm.Name == name)
        {
            return false;
        }
        alarm.Name = name;
        return true;
    }

    /// <summary>
    /// A weather's place: a place found is chosen (<see cref="WeatherPlaces.Choose"/>) and the device's
    /// location turned off for it, as the page's switch is turned off before its place box is used.
    /// </summary>
    public static bool SetPlace(WeatherOptions weather, OptionFound found)
    {
        if (found.Value is not Place place)
        {
            return false;
        }
        var before = (weather.Place, weather.Latitude, weather.Longitude, weather.UseDeviceLocation);
        weather.UseDeviceLocation = false;
        WeatherPlaces.Choose(weather, place.Label, place.Latitude, place.Longitude);
        return before != (weather.Place, weather.Latitude, weather.Longitude, weather.UseDeviceLocation);
    }

    /// <summary>A feed at <paramref name="address"/> that was read and proved one: its address, and its name (<paramref name="title"/>) as it is kept.</summary>
    public static bool SetFeed(RssOptions rss, string address, string title)
    {
        if (!Feeds.Opens(address))
        {
            return false;
        }
        var kept = Feeds.Plain(title, RssOptions.MostTitle);
        if (rss.Address == address && rss.Title == kept)
        {
            return false;
        }
        rss.Address = address;
        rss.Title = kept;
        return true;
    }

    /// <summary>sports: the team a search found, followed in the league it was found in; its name and abbreviation kept as found.</summary>
    public static bool SetTeam(SportsOptions sports, SportsTeam team)
    {
        if (team.Of is null || team.Id.Length == 0)
        {
            return false;
        }
        var next = new SportsOptions { Sport = team.Sport, League = team.League, Team = team.Id, Name = team.Name, Abbreviation = team.Abbreviation };
        next.Normalize();
        if (!next.IsSet || (sports.Key == next.Key && sports.Name == next.Name && sports.Abbreviation == next.Abbreviation))
        {
            return false;
        }
        (sports.Sport, sports.League, sports.Team, sports.Name, sports.Abbreviation) = (next.Sport, next.League, next.Team, next.Name, next.Abbreviation);
        return true;
    }

    /// <summary>sports (§369): a league of the table followed itself, by its name; no team.</summary>
    public static bool SetLeague(SportsOptions sports, SportsLeague league)
    {
        var next = new SportsOptions { Sport = league.Sport, League = league.League, Name = league.Name };
        next.Normalize();
        if (!next.IsSet || (sports.Key == next.Key && sports.Name == next.Name))
        {
            return false;
        }
        (sports.Sport, sports.League, sports.Team, sports.Name, sports.Abbreviation) = (next.Sport, next.League, "", next.Name, "");
        return true;
    }

    /// <summary>What a pin opens, typed or picked: as the settings app's Path box took it (<see cref="PinTargets.Normalize"/>).</summary>
    public static bool SetPinTarget(PinOptions pin, string typed)
    {
        var target = PinTargets.Normalize(typed ?? "");
        if (pin.Target == target)
        {
            return false;
        }
        pin.Target = target;
        return true;
    }

    /// <summary>A pin's name, trimmed; empty for the name Windows gives what it opens.</summary>
    /// <summary>camera (§331): the stream's address, where it is one Karpo asks for; which way it is read goes back to automatic with it.</summary>
    public static bool SetCamera(CameraOptions camera, string typed)
    {
        if (Cameras.Address(typed) is not { } address || camera.Url == address)
        {
            return false;
        }
        camera.Url = address;
        camera.Protocol = null;
        return true;
    }

    /// <summary>camera: its name, trimmed, held to its length; empty for its known name or its host.</summary>
    public static bool SetCameraName(CameraOptions camera, string typed)
    {
        var name = (typed ?? "").ReplaceLineEndings(" ").Trim();
        name = name.Length <= CameraOptions.MostName ? name : name[..CameraOptions.MostName].TrimEnd();
        if ((camera.Name ?? "") == name)
        {
            return false;
        }
        camera.Name = name.Length == 0 ? null : name;
        return true;
    }

    public static bool SetPinName(PinOptions pin, string typed)
    {
        var name = (typed ?? "").Trim();
        if (pin.Name == name)
        {
            return false;
        }
        pin.Name = name;
        return true;
    }

    /// <summary>An actions line's slot: the page <paramref name="action"/> opens, null for none, as the page's picker sets it.</summary>
    public static bool SetAction(WidgetItem item, bool left, SettingsAction? action)
    {
        var id = action?.Id;
        if ((left ? item.Actions?.Left : item.Actions?.Right) == id)
        {
            return false;
        }
        var actions = item.Actions ??= new ActionsOptions();
        if (left)
        {
            actions.Left = id;
        }
        else
        {
            actions.Right = id;
        }
        return true;
    }

    /// <summary>What a countdown of <paramref name="item"/>'s kind starts at: 25 minutes for a focus session, five for a timer.</summary>
    private static int Minutes(WidgetItem item) =>
        item.Info == WidgetInfos.Focus ? CountdownOptions.FocusMinutes : CountdownOptions.TimerMinutes;
}
