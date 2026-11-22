// A folder's face: what its button on the bar shows (docs\widgets.md §67). The person: "for a
// folder you should be able to select the 'face' which could be a clock or a bluetooth icon, or
// just a fixed icon etc." and "when you select a face there should basically spawn more options
// for the face style, face look etc.".
//
// A face is a descriptor: an id the file stores, and the options it has — each a switch, a choice
// of a few words, a line of text or a time zone — with their defaults. Its words are found by
// its id (core.face.<face>.title, core.face.<face>.<option>.title, …), so the record holds no
// text. The settings app draws a face's options from the descriptor alone, and the dock's drawing
// of a face is found by the same id (Karpo.Dock\Islands\FaceDrawings.cs). Adding a face is a
// descriptor here, a drawing there, and its words; nothing else names one.
//
// The choice and its options are kept in the folder (FolderOptions.Face) as a word and a bag of
// values, read through the descriptor: a face or an option this build does not know is kept as it
// is and read as the default, so a file round-trips through an older build unharmed.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>What kind of value a face's option is, which is also the control the settings app gives it.</summary>
public enum FaceOptionKind
{
    /// <summary>On or off: a toggle.</summary>
    Switch,

    /// <summary>One of a few words (<see cref="FaceOption.Choices"/>): a dropdown.</summary>
    Choice,

    /// <summary>A short line somebody types.</summary>
    Text,

    /// <summary>A Windows time zone id, or empty for this PC's.</summary>
    Zone,

    /// <summary>
    /// How often a performance face steps, a pace of <see cref="PerformanceChoices.Paces"/> in seconds ("5", <see cref="PerformanceOptions.Seconds"/>):
    /// the settings app's pace list, the one a reading's line has.
    /// </summary>
    Pace,

    /// <summary>
    /// A graphics adapter as the settings file names it ("GPU 0: …", <see cref="PerformanceOptions.AdapterName"/>),
    /// or empty for the main one: what this PC has, listed when the settings app shows it.
    /// </summary>
    Adapter,

    /// <summary>A disk ("Disk 1", <see cref="PerformanceOptions.DiskName"/>), or empty for every disk together: what this PC has, listed when shown.</summary>
    Disk,
}

/// <summary>
/// One option of a face. Its words are keyed by the face and its own key:
/// <c>core.face.&lt;face&gt;.&lt;key&gt;.title</c> and <c>.description</c>, and a choice's
/// <c>core.face.&lt;face&gt;.&lt;key&gt;.&lt;choice&gt;</c>.
/// </summary>
/// <param name="Key">The option's word in the file.</param>
/// <param name="Kind">What its value is.</param>
/// <param name="Default">What it is where nobody chose: "true"/"false" for a switch, a choice's word, a text.</param>
public sealed record FaceOption(string Key, FaceOptionKind Kind, string Default)
{
    /// <summary>A choice's words, the first the default where <see cref="Default"/> is not one of them.</summary>
    public IReadOnlyList<string> Choices { get; init; } = [];

    /// <summary>
    /// Shown only while another option of the face has a value: (its key, the value). Null for
    /// always. Hidden, the option keeps its value and the face reads it as it is.
    /// </summary>
    public (string Key, string Value)? When { get; init; }

    /// <summary>§249: its icon in a menu (OptionGlyphs); a pace's, an adapter's and a disk's are their kind's.</summary>
    public string Glyph { get; init; } = "";

    /// <summary>§249: a choice's icons, in <see cref="Choices"/>' order.</summary>
    public IReadOnlyList<string> ChoiceGlyphs { get; init; } = [];

    /// <summary>
    /// Where its choices' words are, where they are a widget's own (a reading face's styles are the
    /// performance widget's, "core.widgetStyle.performance"): each choice's key under it. Empty for
    /// the face's own words (<see cref="FaceInfo.ChoiceTitle"/>).
    /// </summary>
    public string Words { get; init; } = "";

    /// <summary>
    /// §292: the part of the menu its row stands in (OptionRows.OfFace). Where none is given, its kind's:
    /// a zone what it is of, an adapter or a disk what of it it shows, a text its words, a pace what it
    /// does, a switch the rest of its look; a choice says its own.
    /// </summary>
    public OptionSlot Slot
    {
        get => slot != OptionSlot.Unset ? slot : Kind switch
        {
            FaceOptionKind.Zone => OptionSlot.Subject,
            FaceOptionKind.Adapter or FaceOptionKind.Disk => OptionSlot.Scope,
            FaceOptionKind.Text => OptionSlot.Words,
            FaceOptionKind.Pace => OptionSlot.Behaviour,
            FaceOptionKind.Switch => OptionSlot.Look,
            _ => OptionSlot.Unset,
        };
        init => slot = value;
    }

    private readonly OptionSlot slot;

    /// <summary>The longest a text may be.</summary>
    public const int TextLength = 40;
}

/// <summary>A face a folder's button can wear: its id and its options.</summary>
/// <param name="Id">The face's word in the file.</param>
/// <param name="Glyph">A Segoe Fluent Icons glyph for it, where a list shows faces.</param>
public sealed record FaceInfo(string Id, string Glyph)
{
    public IReadOnlyList<FaceOption> Options { get; init; } = [];

    /// <summary>
    /// Whether a folder may wear it more than once among its faces (<see cref="FolderOptions.Faces"/>):
    /// a face whose settings make each one something else — an icon of its own (§259), a clock in
    /// another zone, a reading of another disk. The rest show what is the folder's one of a kind — the
    /// Wi-Fi, its first drive line, the one place and the one market the folder follows — and a second
    /// would only say it again.
    /// </summary>
    public bool Repeats { get; init; }

    /// <summary>What it is called, in the language Karpo speaks.</summary>
    public string Title => Strings.Text($"core.face.{Id}.title");

    /// <summary>A line saying what it shows.</summary>
    public string Blurb => Strings.Text($"core.face.{Id}.blurb");

    /// <summary>The option <paramref name="key"/>, or null.</summary>
    public FaceOption? Option(string key) =>
        Options.FirstOrDefault(option => string.Equals(option.Key, key, StringComparison.Ordinal));

    /// <summary>
    /// What <paramref name="option"/> is called: a style "Style", as every widget's (§292); §411 (R7): a pace "Update
    /// interval", a name shown or not "Show name", a label "Name", the glossary's one word as a widget's has it.
    /// </summary>
    public string TitleOf(FaceOption option) => option.Slot == OptionSlot.Style ? S.SettingsWidgetStyleHeader : option.Key switch
    {
        "pace" => S.SettingsOnlineUpdateHeader,
        "name" when option.Kind == FaceOptionKind.Switch => S.SettingsWidgetSwitchLabelHeader,
        "label" when option.Kind == FaceOptionKind.Text => S.SettingsWidgetCountdownNameHeader,
        _ => Strings.Text(KeyOf(option, "title")),
    };

    /// <summary>A line under it, or empty.</summary>
    public string DescriptionOf(FaceOption option) =>
        Strings.Lookup(KeyOf(option, "description")) ?? "";

    /// <summary>What a choice of <paramref name="option"/> is called: in its own words, or the widget's it shares them with (<see cref="FaceOption.Words"/>).</summary>
    public string ChoiceTitle(FaceOption option, string choice) =>
        Strings.Text(option.Words.Length > 0 ? $"{option.Words}.{choice}" : KeyOf(option, choice));

    /// <summary>The key of <paramref name="option"/>'s <paramref name="part"/> (its title, its description, a choice): core.face.&lt;face&gt;.&lt;key&gt;.&lt;part&gt;.</summary>
    public string KeyOf(FaceOption option, string part) => $"core.face.{Id}.{option.Key}.{part}";

    /// <summary>Whether <paramref name="option"/> is shown for <paramref name="face"/>: always, or while the option it follows has its value.</summary>
    public bool Shows(FaceOption option, FolderFace face) =>
        option.When is not { } when || Option(when.Key) is not { } other || face.Text(other) == when.Value;
}

/// <summary>
/// A folder's face as the file keeps it: which face, and the values of its options somebody set.
/// Read through <see cref="FolderFaces"/> and the face's <see cref="FaceInfo"/>, never directly:
/// what is here may be from a newer build.
/// </summary>
public sealed class FolderFace
{
    /// <summary>The face's id (<see cref="FaceInfo.Id"/>); one this build does not know is kept and read as <see cref="FolderFaces.Icon"/>.</summary>
    public string Kind { get; set; } = FolderFaces.IconId;

    /// <summary>
    /// The options somebody set, by key, in JSON's own values (true, "twentyFour", "Tokyo"). An option not
    /// here is its default; one this build does not know, or a value of the wrong kind, is kept and
    /// read as the default.
    /// </summary>
    public Dictionary<string, JsonElement> Options { get; set; } = [];

    /// <summary>
    /// icon: the icon it shows, a Segoe Fluent Icons code point somebody chose, or null for the folder
    /// glyph (<see cref="GlyphOrDefault"/>). Its own, so a folder wearing several icons shows a
    /// different one on each (docs\widgets.md §259). Read by the icon face alone.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Glyph { get; set; }

    /// <summary>The icon to draw: the chosen one, or the folder glyph.</summary>
    [JsonIgnore]
    public string GlyphOrDefault => string.IsNullOrEmpty(Glyph) ? FolderOptions.FolderGlyph : Glyph;

    /// <summary>
    /// weather: the place the weather face shows the weather for, its own as every weather widget's
    /// is (WeatherPlaces, docs\widgets.md §80); null for another face. A new weather face has one,
    /// the device's location (<see cref="Of"/>); one without shows its "Set up".
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WeatherOptions? Weather { get; set; }

    /// <summary>
    /// markets: what the markets face tracks and how it shows it, its own as the weather face's place is
    /// (docs\widgets.md §246); null for another face. A new one is a share with no symbol yet.
    /// </summary>
    [JsonPropertyName("market"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MarketOptions? Market { get; set; }

    /// <summary>
    /// news: the feed the news face shows of its own, as a feed line keeps it (§477); null for Inherited: the
    /// folder's first feed line's (<see cref="FaceSources"/>).
    /// </summary>
    [JsonPropertyName("rss"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RssOptions? Rss { get; set; }

    /// <summary>
    /// drive, networkDrive: the drive a drive face shows of its own, as a drive line keeps it (§477); null for
    /// Inherited: the folder's first drive line's of its kind (<see cref="FaceSources"/>).
    /// </summary>
    [JsonPropertyName("drive"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DriveTargetOptions? Drive { get; set; }

    /// <summary>
    /// sports: the team or league the sports face follows of its own, as a sports line keeps it (§477); null for
    /// Inherited: the folder's sports lines' (<see cref="FaceSources"/>).
    /// </summary>
    [JsonPropertyName("sports"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SportsOptions? Sports { get; set; }

    /// <summary>
    /// battery, §485: the charge's glyph alone, its percentage not beside it — Windows' own quick settings button's battery,
    /// where Windows' "Battery percentage" is off (<see cref="WindowsFaces"/>). Never in the file: Karpo's own battery face
    /// always says it.
    /// </summary>
    [JsonIgnore]
    public bool NoPercent { get; set; }

    /// <summary>The face this is, as this build knows it.</summary>
    [JsonIgnore]
    public FaceInfo Info => FolderFaces.Of(Kind);

    /// <summary>
    /// A face of <paramref name="kind"/> with nothing set; a weather or an air-quality face for where the device is
    /// (<see cref="WeatherOptions.New"/>), a markets face a share with no symbol yet.
    /// </summary>
    public static FolderFace Of(string kind) => new()
    {
        Kind = kind,
        Weather = FolderFaces.HasPlace(kind) ? WeatherOptions.New() : null,
        Market = kind == FolderFaces.MarketsId ? MarketOptions.New() : null,
    };

    /// <summary>A switch's value: what was set where it is a true or a false, else its default.</summary>
    public bool Switch(FaceOption option) =>
        Options.TryGetValue(option.Key, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.ValueKind == JsonValueKind.True
            : option.Default == "true";

    /// <summary>The option's value as a word: a choice it knows, a text, a zone, "true"/"false"; else its default.</summary>
    public string Text(FaceOption option)
    {
        if (option.Kind == FaceOptionKind.Switch)
        {
            return Switch(option) ? "true" : "false";
        }
        var set = Options.TryGetValue(option.Key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return option.Kind switch
        {
            FaceOptionKind.Choice => set is not null && option.Choices.Contains(set) ? set
                : option.Choices.Contains(option.Default) ? option.Default
                : option.Choices.FirstOrDefault() ?? "",
            FaceOptionKind.Text => Cut((set ?? option.Default).Trim()),
            // A pace that is none of the list's is the nearest that is (§228): a "3" from when every
            // second up to ten was offered reads as "2".
            FaceOptionKind.Pace => int.TryParse(set, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var seconds)
                && seconds >= 1
                    ? PerformanceChoices.Nearest(seconds).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : option.Default,
            // A name that is no disk is every disk, as a reading's line reads it.
            FaceOptionKind.Disk => PerformanceOptions.DiskNumber(set) is { } number ? PerformanceOptions.DiskName(number) : "",
            _ => set?.Trim() ?? option.Default,
        };

        static string Cut(string text) => text.Length > FaceOption.TextLength ? text[..FaceOption.TextLength] : text;
    }

    /// <summary>Sets a switch.</summary>
    public void Set(FaceOption option, bool on) =>
        Options[option.Key] = JsonSerializer.SerializeToElement(on, ConfigJson.Default.Boolean);

    /// <summary>Sets a choice, a text or a zone; a switch from "true" or "false".</summary>
    public void Set(FaceOption option, string value)
    {
        if (option.Kind == FaceOptionKind.Switch)
        {
            Set(option, value == "true");
            return;
        }
        Options[option.Key] = JsonSerializer.SerializeToElement(value.Trim(), ConfigJson.Default.String);
    }

    /// <summary>A copy, its values its own.</summary>
    public FolderFace Clone() => new()
    {
        Kind = Kind,
        Options = new Dictionary<string, JsonElement>(Options.Select(pair => KeyValuePair.Create(pair.Key, pair.Value.Clone()))),
        Glyph = Glyph,
        NoPercent = NoPercent,
        Weather = Weather is null ? null : WeatherPlaces.Settled(Weather),
        Market = Market?.Copy(),
        Rss = Rss is null ? null : new RssOptions { Address = Rss.Address, Title = Rss.Title, Glyph = Rss.Glyph },
        Drive = Drive is null ? null : new DriveTargetOptions { Volume = Drive.Volume, Letter = Drive.Letter, Label = Drive.Label },
        Sports = Sports is null ? null : new SportsOptions { Sport = Sports.Sport, League = Sports.League, Team = Sports.Team, Name = Sports.Name, Abbreviation = Sports.Abbreviation },
    };

    /// <summary>A face and a bag in place; what is in the bag is kept, whatever it is.</summary>
    internal void Normalize()
    {
        Kind = string.IsNullOrWhiteSpace(Kind) ? FolderFaces.IconId : Kind.Trim();
        Options ??= [];
        // Empty is nobody has chosen, as null is: not written.
        Glyph = string.IsNullOrWhiteSpace(Glyph) ? null : Glyph.Trim();
        // markets: its own record where it is the markets face, made where it has none (§246).
        Market = Kind == FolderFaces.MarketsId ? Market ?? MarketOptions.New() : null;
        Market?.Normalize();
        // §477: a source of its own only on the face that takes one of that kind, and only where it is one; none is Inherited.
        FaceSources.Normalize(this);
    }
}

/// <summary>The faces a folder's button can wear, in the order the settings app offers them.</summary>
public static class FolderFaces
{
    public const string IconId = "icon";
    public const string ClockId = "clock";
    public const string CalendarId = "calendar";
    public const string WeatherId = "weather";
    public const string WifiId = "wifi";
    public const string BluetoothId = "bluetooth";
    public const string SoundId = "sound";
    public const string RemovableId = "removable";
    public const string TimerId = "timer";
    public const string FocusId = "focus";
    public const string StopwatchId = "stopwatch";
    public const string HotspotId = "hotspot";
    public const string CastId = "cast";
    public const string NearbyId = "nearbySharing";
    public const string BatteryId = "battery";
    public const string PowerId = "power";
    public const string MailId = "mail";
    public const string NowPlayingId = "nowPlaying";
    public const string CpuId = "cpu";
    public const string GpuId = "gpu";
    public const string MemoryId = "memory";
    public const string DiskId = "disk";
    public const string NetworkActivityId = "networkActivity";
    public const string MarketsId = "markets";
    public const string NewsId = "news";
    public const string DriveId = "drive";
    public const string NetworkDriveId = "networkDrive";
    public const string NotificationsId = "notifications";
    public const string ClipboardId = "clipboard";
    public const string ContactsId = "contacts";
    public const string WinSearchId = "winSearch";
    public const string DesktopsId = "desktops";
    public const string DateTimeId = "dateTime";
    public const string KeyboardId = "keyboard";
    public const string VpnId = "vpn";
    public const string LayoutsId = "layout";
    public const string MeetingId = "meeting";
    public const string AirQualityId = "airQuality";
    public const string TidesId = "tides";
    public const string SportsId = "sports";
    public const string QuakesId = "quakes";
    public const string SunId = "sun";
    public const string ScreenTimeId = "screenTime";

    /// <summary>
    /// Whether a face of <paramref name="kind"/> is for a place of its own (<see cref="FolderFace.Weather"/>,
    /// as the weather face's): the weather, the air quality, the tides, the earthquakes and the sun.
    /// A new one is for where the device is; its rows start with the place's (FaceRows).
    /// </summary>
    public static bool HasPlace(string kind) => kind is WeatherId or AirQualityId or TidesId or QuakesId or SunId;

    /// <summary>
    /// Its icon: the one somebody chose for it, or the folder glyph (<see cref="FolderFace.GlyphOrDefault"/>),
    /// each icon face its own, so a folder may wear several (§259).
    /// What a new folder wears, and what a face this build does not know reads as (<see cref="Of"/>):
    /// the face of what is inside, which went (§254), among them.
    /// </summary>
    public static readonly FaceInfo Icon = new(IconId, "") { Repeats = true };

    /// <summary>
    /// clock: the time, as the clock on the taskbar shows it — one row across a horizontal bar, the
    /// time and what follows it — with its seconds, its zone, its label, a 12- or 24-hour clock, and
    /// what a timer in the folder has left. Only the clock: the date is the calendar face's (the
    /// person, 2026-10-09: "the clock face should only show the clock and not the calendar stuff").
    /// </summary>
    public static readonly FaceInfo Clock = new(ClockId, "")
    {
        Repeats = true,
        Options =
        [
            new FaceOption(ClockFace.Seconds, FaceOptionKind.Switch, "false") { Glyph = OptionGlyphs.Seconds },
            new FaceOption(ClockFace.Hours, FaceOptionKind.Choice, ClockFace.Region)
            {
                Choices = [ClockFace.Region, ClockFace.Twelve, ClockFace.TwentyFour],
                Glyph = OptionGlyphs.Time,
                Slot = OptionSlot.Look,
                ChoiceGlyphs = [OptionGlyphs.Region, OptionGlyphs.Time, OptionGlyphs.Clock],
            },
            new FaceOption(ClockFace.Zone, FaceOptionKind.Zone, "") { Glyph = OptionGlyphs.Zone },
            new FaceOption(ClockFace.Label, FaceOptionKind.Text, "") { Glyph = OptionGlyphs.Name },
            new FaceOption(ClockFace.Timer, FaceOptionKind.Switch, "true") { Glyph = OptionGlyphs.Timer },
        ],
    };

    /// <summary>
    /// calendar: today, as the date under Windows' clock reads, or as a calendar page with the day's
    /// number on it. Turns over at midnight.
    /// </summary>
    public static readonly FaceInfo Calendar = new(CalendarId, "")
    {
        Repeats = true,
        Options =
        [
            new FaceOption(CalendarFace.Look, FaceOptionKind.Choice, CalendarFace.Text)
            {
                Choices = [CalendarFace.Text, CalendarFace.Page],
                Glyph = OptionGlyphs.Style,
                Slot = OptionSlot.Style,
                ChoiceGlyphs = [OptionGlyphs.Font, OptionGlyphs.Month],
            },
            new FaceOption(CalendarFace.Form, FaceOptionKind.Choice, CalendarFace.Short)
            {
                Choices = [CalendarFace.Short, CalendarFace.DayAndDate],
                Glyph = OptionGlyphs.Day,
                Slot = OptionSlot.Look,
                ChoiceGlyphs = [OptionGlyphs.Day, OptionGlyphs.Month],
                When = (CalendarFace.Look, CalendarFace.Text),
            },
            // Whose today: this PC's unless set (§230), as a clock's zone is.
            new FaceOption(CalendarFace.Zone, FaceOptionKind.Zone, "") { Glyph = OptionGlyphs.Zone },
        ],
    };

    /// <summary>
    /// dateTime: the time over the date, as Windows 11's own clock on the taskbar shows them (2026-10-11,
    /// the person: "create a folder face that combined time and clock together like it is shown on
    /// the windows default taskbar") — two lines at the taskbar's text size, right-aligned, in the
    /// region's time and short date, with the seconds where Windows' clock shows them
    /// (ShowSecondsInSystemClock) unless set otherwise. One line where the bar is too thin for two;
    /// down a bar on its side the time over the date, each as wide as the bar lets it be.
    /// </summary>
    public static readonly FaceInfo DateTime = new(DateTimeId, "")
    {
        Repeats = true,
        Options =
        [
            new FaceOption(DateTimeFace.Seconds, FaceOptionKind.Choice, DateTimeFace.Windows)
            {
                Choices = [DateTimeFace.Windows, DateTimeFace.Shown, DateTimeFace.Hidden],
                Glyph = OptionGlyphs.Seconds,
                Slot = OptionSlot.Look,
                ChoiceGlyphs = [OptionGlyphs.Windows, OptionGlyphs.Show, OptionGlyphs.Hide],
            },
            // The time and the date in a zone of their own: this PC's unless set (§230).
            new FaceOption(DateTimeFace.Zone, FaceOptionKind.Zone, "") { Glyph = OptionGlyphs.Zone },
        ],
    };

    /// <summary>
    /// weather: the conditions' glyph and the temperature for the face's own place (FolderFace.Weather),
    /// as the weather entry on the bar showed them — simple, with today's high and low, or today and
    /// the next two days — which went with the weather preset (2026-10-09). Its looks were the
    /// entry's (BarEntry.Looks), the one kind that had any.
    /// </summary>
    public static readonly FaceInfo Weather = new(WeatherId, "\uE753")
    {
        Options =
        [
            new FaceOption(WeatherFace.Look, FaceOptionKind.Choice, WeatherFace.Simple)
            {
                Choices = [WeatherFace.Simple, WeatherFace.Detailed, WeatherFace.Forecast, WeatherFace.Conditions],
                Glyph = OptionGlyphs.Style,
                Slot = OptionSlot.Style,
                ChoiceGlyphs = [OptionGlyphs.Sun, OptionGlyphs.Agenda, OptionGlyphs.Month, OptionGlyphs.Cloud],
            },
        ],
    };

    /// <summary>
    /// wifi: the connection, as Windows' network icon on the taskbar shows it and the network button
    /// on the bar did — the Wi-Fi's bars, the cable, or no connection, with a VPN's shield — which
    /// went with the Wi-Fi preset (2026-10-10). Its tooltip is the icon's: the network and whether it
    /// reaches the internet.
    /// </summary>
    public static readonly FaceInfo Wifi = new(WifiId, "");

    /// <summary>bluetooth: Bluetooth's rune, dimmed while the radio is off, as the Bluetooth button on the bar was.</summary>
    public static readonly FaceInfo Bluetooth = new(BluetoothId, "");

    /// <summary>
    /// sound: the speaker, as Windows' volume icon on the taskbar shows it and the sound button on
    /// the bar did — its waves for the level, crossed out while muted — which went with the sound
    /// preset (2026-10-10). Its tooltip is the icon's: the output and its level. The wheel over it
    /// steps the volume and a middle click mutes, as over the button.
    /// </summary>
    public static readonly FaceInfo Sound = new(SoundId, "");

    /// <summary>
    /// removable: the USB stick, as the removable-devices button on the bar showed it — quiet while
    /// nothing is plugged in, a count on its corner from two devices — which went with the
    /// removable-devices preset (2026-10-10). Its tooltip is the button's: how many, and their names.
    /// </summary>
    public static readonly FaceInfo Removable = new(RemovableId, "");

    /// <summary>
    /// timer: the timer's glyph and what the timer in the folder has left, as the timer's entry on the
    /// bar showed it — the length set at rest, what is left while it runs, quieter while it is paused
    /// — which went with the timer preset (2026-10-10). The countdown is the line's, the one in the
    /// folder: the face reads it, and keeps none of its own.
    /// </summary>
    public static readonly FaceInfo Timer = new(TimerId, WidgetInfos.Timer.Glyph);

    /// <summary>focus: the same for the focus session in the folder, as the focus entry on the bar was.</summary>
    public static readonly FaceInfo Focus = new(FocusId, WidgetInfos.Focus.Glyph);

    /// <summary>
    /// stopwatch: the stopwatch's glyph and what the stopwatch in the folder has counted, in whole
    /// seconds, as its entry on the bar showed it, which went with the stopwatch preset (2026-10-10).
    /// </summary>
    public static readonly FaceInfo Stopwatch = new(StopwatchId, WidgetInfos.Stopwatch.Glyph);

    /// <summary>
    /// hotspot: the mobile hotspot's antenna, as Windows' hotspot icon on the taskbar shows it (the
    /// button's own glyph, a tray with an arrow, did not read as a hotspot beside the Wi-Fi and
    /// Bluetooth faces), dimmed while the hotspot is off or this PC cannot host one, and on its corner
    /// how many devices are on it — the mobile hotspot button's face since it went with the mobile
    /// hotspot preset (2026-10-10). Its tooltip says whether it is on and how many of how many devices
    /// are on it, or why it cannot be.
    /// </summary>
    public static readonly FaceInfo Hotspot = new(HotspotId, WidgetInfos.Hotspot.Glyph);

    /// <summary>
    /// cast: Windows' cast glyph, with an accent dot on its corner while this PC shows on a wireless
    /// display — the cast button's face since it went with the cast preset (2026-10-11). Its tooltip
    /// names the display cast to, or says nothing is.
    /// </summary>
    public static readonly FaceInfo Cast = new(CastId, WidgetInfos.WirelessDisplays.Glyph);

    /// <summary>
    /// nearbySharing: the share glyph, dimmed while nearby sharing is off — the nearby sharing
    /// button's face since it went with the nearby sharing preset (2026-10-11). Its tooltip says who
    /// may share with this PC.
    /// </summary>
    public static readonly FaceInfo Nearby = new(NearbyId, WidgetInfos.NearbySharing.Glyph);

    /// <summary>
    /// battery: the charge, as the battery on the bar showed it — the level's glyph, charging or in
    /// energy saver as Windows' battery icon draws it, and the percentage beside it; the power bolt
    /// alone on a PC without a battery — which went with the battery preset (2026-10-10). Its tooltip
    /// is the battery's.
    /// </summary>
    public static readonly FaceInfo Battery = new(BatteryId, WidgetInfos.Battery.Glyph);

    /// <summary>power: the power button's glyph, as the power widget on the bar was, which went with the power preset (2026-10-10).</summary>
    public static readonly FaceInfo Power = new(PowerId, "");

    /// <summary>
    /// mail: the envelope with how many are unread on its corner, as the mail entry on the bar showed
    /// it — dimmed while an inbox cannot be read, the stale mark on its lower corner while what it
    /// shows could not be refreshed — which went with the mail preset (2026-10-10). Its tooltip is the
    /// entry's: none but the stale mark's, over the bar; the count or why, to a screen reader.
    /// </summary>
    public static readonly FaceInfo Mail = new(MailId, WidgetInfos.Mail.Glyph);

    /// <summary>
    /// nowPlaying: what is playing, as the now-playing entry on the bar showed it — the art (the note
    /// where there is none) and the title with the artist, over each other where the bar is thick
    /// enough and on one line where it is not; "Nothing playing" quietly while nothing plays — and a
    /// pause mark on the art while it is paused. It went with the media preset (2026-10-10), and its
    /// two options were the entry's: the art, and taking the folder off the bar while nothing plays;
    /// §503 gave it its width, one of three fixed ones (<see cref="NowPlayingFace.Width"/>), named as a
    /// folder's list widths are. Its tooltip is the entry's: none over the bar; the track and where it
    /// plays to a screen reader.
    /// </summary>
    public static readonly FaceInfo NowPlaying = new(NowPlayingId, WidgetInfos.Media.Glyph)
    {
        Options =
        [
            new FaceOption(NowPlayingFace.Art, FaceOptionKind.Switch, "true") { Glyph = OptionGlyphs.Album },
            new FaceOption(NowPlayingFace.Width, FaceOptionKind.Choice, NowPlayingFace.Medium)
            {
                Choices = [NowPlayingFace.Narrow, NowPlayingFace.Medium, NowPlayingFace.Wide],
                Glyph = OptionGlyphs.Style,
                ChoiceGlyphs = OptionGlyphs.Widths,
                Slot = OptionSlot.Style,
                Words = "dock.folder.width",
            },
            new FaceOption(NowPlayingFace.HideIdle, FaceOptionKind.Switch, "true") { Glyph = OptionGlyphs.Hide, Slot = OptionSlot.Behaviour },
        ],
    };

    /// <summary>
    /// cpu, gpu, memory, disk, networkActivity: a performance reading's figure, as its entry on the bar
    /// showed it — the reading's glyph and the figure it reads now ("63%", "8.8M") — which went with the
    /// readings' presets (2026-10-10); or in its graph style (§295) the same with its recent history as a
    /// graph behind them, as a reading's line draws its graph. Its options are the line's: for the
    /// graphics and the disk which adapter or disk it follows, its style (Numbers, the default, or
    /// Graph: the performance widget's own words), and how often it steps. Its tooltip is the entry's:
    /// none over the bar; the reading to a screen reader.
    /// </summary>
    public static readonly FaceInfo Cpu = Performance(CpuId, WidgetInfos.Cpu);

    /// <inheritdoc cref="Cpu"/>
    public static readonly FaceInfo Gpu = Performance(GpuId, WidgetInfos.Gpu, new FaceOption(PerformanceFace.Adapter, FaceOptionKind.Adapter, "") { Glyph = OptionGlyphs.Graphics });

    /// <inheritdoc cref="Cpu"/>
    public static readonly FaceInfo Memory = Performance(MemoryId, WidgetInfos.Memory);

    /// <inheritdoc cref="Cpu"/>
    public static readonly FaceInfo Disk = Performance(DiskId, WidgetInfos.Disk, new FaceOption(PerformanceFace.Disk, FaceOptionKind.Disk, "") { Glyph = OptionGlyphs.Disk });

    /// <inheritdoc cref="Cpu"/>
    public static readonly FaceInfo NetworkActivity = Performance(NetworkActivityId, WidgetInfos.Network);

    /// <summary>
    /// screenTime: how long this PC was used today (docs\widgets.md §504) — the screen-time glyph and the day's
    /// total, "3 h 42 min", as the screen-time line counts it (the one tracker every screen-time widget reads), up
    /// to the minute; the glyph over it down a bar on its side. Always today's, whatever day the folder's lines
    /// show, and this PC's: there is no other to read, so nothing to set.
    /// </summary>
    public static readonly FaceInfo ScreenTime = new(ScreenTimeId, WidgetInfos.ScreenTime.Glyph);

    /// <summary>A reading's face: its glyph, the device it follows where it has one, its style, then its pace, as the reading's line has them.</summary>
    private static FaceInfo Performance(string id, WidgetInfo reading, FaceOption? device = null) => new(id, reading.Glyph)
    {
        Repeats = true,
        Options =
        [
            .. device is null ? [] : new[] { device },
            new FaceOption(PerformanceFace.Style, FaceOptionKind.Choice, PerformanceFace.Figure)
            {
                Choices = [PerformanceFace.Figure, PerformanceFace.Graph],
                Glyph = OptionGlyphs.Style,
                Slot = OptionSlot.Style,
                ChoiceGlyphs = [OptionGlyphs.Figure, OptionGlyphs.Graph],
                Words = $"core.widgetStyle.{WidgetStyles.Performance.Id}",
            },
            new FaceOption(PerformanceFace.Pace, FaceOptionKind.Pace, "1") { Glyph = OptionGlyphs.Pace },
        ],
    };

    /// <summary>
    /// markets: what a markets widget shows on the bar (docs\widgets.md §246), for what it tracks of its
    /// own (FolderFace.Market), never a line's: a share's, a rate's or a coin's ticker — its name, the
    /// price and how far it has moved in the up or down colour on one row, the name over the price down a
    /// bar on its side, quieter once the price is past due, the chart faded behind them — the price alone
    /// in the compact style, and a currency's converter "100 EUR = 94.12 CHF" in the converter style.
    /// </summary>
    public static readonly FaceInfo Markets = new(MarketsId, WidgetInfos.Markets.Glyph);

    /// <summary>
    /// news: a feed's newest headline, as the RSS entry on the bar showed it — the news glyph with the
    /// accent's dot while something new has come, and the headline beside it in a room of its own —
    /// which went with the news preset (2026-10-10). It shows the folder's first feed line; its one
    /// option is the entry's own: the headline, or the glyph alone. Its tooltip is the entry's: none
    /// over the bar, the feed and its newest headline to a screen reader.
    /// </summary>
    public static readonly FaceInfo News = new(NewsId, WidgetInfos.Rss.Glyph)
    {
        Options = [new FaceOption(NewsFace.Headline, FaceOptionKind.Switch, "true") { Glyph = OptionGlyphs.Font }],
    };

    /// <summary>
    /// drive: a drive, as the drive entry on the bar showed it — its own picture, its name, and
    /// Windows 11's capacity bar filling the room after the name, in the alarm colour once nearly
    /// nothing is left; faint with the warning badge while the drive is not there — which went with
    /// the drive preset (2026-10-10). It shows the folder's first drive line, its drive; its one
    /// option is the entry's own: the name, or the letter alone. Its tooltip is the entry's: none
    /// over the bar, the drive and how full it is to a screen reader.
    /// </summary>
    public static readonly FaceInfo Drive = new(DriveId, WidgetInfos.Drive.Glyph)
    {
        Options = [new FaceOption(DriveFace.Name, FaceOptionKind.Switch, "true") { Glyph = OptionGlyphs.Label }],
    };

    /// <summary>
    /// networkDrive: the same for a mapped network drive, as the network drive entry showed it, which
    /// went with the network drive preset: one that is not connected is File Explorer's disconnected
    /// network drive, the drive with the red X, its name with no capacity bar.
    /// </summary>
    public static readonly FaceInfo NetworkDrive = new(NetworkDriveId, WidgetInfos.NetworkDrive.Glyph)
    {
        Options = [new FaceOption(DriveFace.Name, FaceOptionKind.Switch, "true") { Glyph = OptionGlyphs.Label }],
    };

    /// <summary>
    /// notifications: the bell, as the bell on the bar was — how many arrived since the folder was last
    /// opened in an accent pill on its corner, Windows 11's bell with "zZ" while do not disturb is on,
    /// dimmed while the notifications cannot be read — which went with the notifications preset
    /// (2026-10-10). Its one option is the bell's own: the count. Its tooltip is the bell's: none over
    /// the bar; how many are new, or why none can be read, to a screen reader.
    /// </summary>
    public static readonly FaceInfo Notifications = new(NotificationsId, WidgetInfos.NotificationsList.Glyph)
    {
        Options = [new FaceOption(NotificationsFace.Count, FaceOptionKind.Switch, "true") { Glyph = OptionGlyphs.Bell }],
    };

    /// <summary>
    /// clipboard: the clipboard's glyph, as the clipboard history entry on the bar was, which went with
    /// the clipboard preset (2026-10-10): a glyph's square, with no count — the entry had none, so its
    /// width never moved — and nothing to set. Its tooltip is the entry's: none over the bar; its name
    /// to a screen reader.
    /// </summary>
    public static readonly FaceInfo Clipboard = new(ClipboardId, WidgetInfos.Clipboard.Glyph);

    /// <summary>
    /// pim: the contact's glyph, as the contacts entry on the bar was, which went with the contacts
    /// preset (2026-10-11): a glyph's square, no count — the entry had none — and nothing to set. Its
    /// tooltip is the entry's: none over the bar; its name to a screen reader.
    /// </summary>
    public static readonly FaceInfo Contacts = new(ContactsId, WidgetInfos.Contacts.Glyph);

    /// <summary>
    /// §642: Windows search's face: a search box as Windows 11's taskbar draws its own (SearchPill) — the magnifier and
    /// "Search" — along a top or bottom bar, the magnifier alone down a side or on a thin bar. Its click opens the folder,
    /// the keys in its line. Nothing to set.
    /// </summary>
    public static readonly FaceInfo WinSearch = new(WinSearchId, WidgetInfos.WinSearch.Glyph);

    /// <summary>
    /// desktops: the desktop you are on, as the desktop switcher on the bar was, which went with the
    /// desktops preset (2026-10-11): the switcher's glyph and the desktop's name after it, in the room
    /// of Windows' "Desktop 88" whatever the name is — the name changes as you switch, and the bar
    /// must not move under the hand that is switching — cut where it is longer; the glyph alone down
    /// a bar on its side. Its tooltip is the switcher's: the whole name and where it stands
    /// among the desktops.
    /// </summary>
    public static readonly FaceInfo Desktops = new(DesktopsId, WidgetInfos.Desktops.Glyph);

    /// <summary>
    /// keyboard: the input indicator, as the keyboard entry on the bar was, which went with the
    /// keyboard preset (2026-10-11): the language over the region of the layout in use ("ENG" over
    /// "US"), at the bar's text size, as wide as the widest code this PC's layouts show, so switching
    /// layout never moves the bar. Words of its own, so no name beside it.
    /// </summary>
    public static readonly FaceInfo Keyboard = new(KeyboardId, WidgetInfos.Keyboard.Glyph);

    /// <summary>
    /// quick: VPN's shield, as the VPN entry on the bar was, which went with the VPN preset
    /// (2026-10-11): a glyph's square, full while a connection is up and dimmed while none is, as a
    /// status glyph says on and off; nothing to set. Its tooltip is the switch's: on as what, or off.
    /// </summary>
    public static readonly FaceInfo Vpn = new(VpnId, WidgetInfos.Vpn.Glyph);

    /// <summary>
    /// layouts: the layout this bar shows, as the layout switcher on the bar was, which went with
    /// the layouts preset (2026-10-11): the layout's glyph and its name after it, in the room of ten
    /// characters whatever the name is, dimmed where there is no other layout to switch to; the
    /// glyph alone down a bar on its side. Its tooltip is the switcher's.
    /// </summary>
    public static readonly FaceInfo Layouts = new(LayoutsId, WidgetInfos.Layouts.Glyph);

    /// <summary>
    /// meeting: the next meeting (docs\widgets.md §205) — its calendar's colour as a small mark, its title
    /// cut to the room and how soon it starts ("Standup · in 4 min", "now", "started 3 min ago"), and a
    /// compact Join button with the camera where it is online; a calm "No more meetings today" where
    /// nothing is coming, or the folder off the bar until shortly before one where it is set so. Read
    /// from the accounts' calendars, as every calendar widget is. Its options: which meeting it shows,
    /// from how long before it is lit, how much room its words keep, and the Join button.
    /// </summary>
    public static readonly FaceInfo Meeting = new(MeetingId, WidgetInfos.Meeting.Glyph)
    {
        Options =
        [
            new FaceOption(MeetingFace.Show, FaceOptionKind.Choice, MeetingFace.Today)
            {
                Choices = [MeetingFace.Today, MeetingFace.Next, MeetingFace.Soon],
                Glyph = OptionGlyphs.Events,
                Slot = OptionSlot.Scope,
                ChoiceGlyphs = [OptionGlyphs.Day, OptionGlyphs.Next, OptionGlyphs.Time],
            },
            new FaceOption(MeetingFace.Lead, FaceOptionKind.Choice, MeetingFace.Ten)
            {
                Choices = [MeetingFace.Five, MeetingFace.Ten, MeetingFace.Fifteen, MeetingFace.Thirty],
                Glyph = OptionGlyphs.Bell,
                Slot = OptionSlot.Behaviour,
                ChoiceGlyphs = OptionGlyphs.Each(OptionGlyphs.Seconds, 4),
            },
            new FaceOption(MeetingFace.Width, FaceOptionKind.Choice, MeetingFace.Standard) { Choices = [MeetingFace.Compact, MeetingFace.Standard, MeetingFace.Wide], Glyph = OptionGlyphs.Style, ChoiceGlyphs = OptionGlyphs.Widths, Slot = OptionSlot.Style },
            new FaceOption(MeetingFace.Join, FaceOptionKind.Switch, "true") { Glyph = OptionGlyphs.Video },
        ],
    };

    /// <summary>
    /// air: the air quality for the face's own place (FolderFace.Weather, as the weather face's;
    /// docs\widgets.md §210) — a dot in the level's colour and the index, "32 · Good", from
    /// Open-Meteo's air-quality service. Its options: which index (the region's, the European or the
    /// US one) and how much its words say.
    /// </summary>
    public static readonly FaceInfo AirQuality = new(AirQualityId, WidgetInfos.AirQuality.Glyph)
    {
        Options =
        [
            new FaceOption(AirQualityFace.Index, FaceOptionKind.Choice, AirIndexes.Auto) { Choices = [AirIndexes.Auto, AirIndexes.European, AirIndexes.Us], Glyph = OptionGlyphs.Graph, ChoiceGlyphs = OptionGlyphs.AirIndexes, Slot = OptionSlot.Scope },
            new FaceOption(AirQualityFace.Width, FaceOptionKind.Choice, AirQualityFace.Standard) { Choices = [AirQualityFace.Compact, AirQualityFace.Standard, AirQualityFace.Wide], Glyph = OptionGlyphs.Style, ChoiceGlyphs = OptionGlyphs.Widths, Slot = OptionSlot.Style },
        ],
    };

    /// <summary>
    /// tides: the tides for the face's own place (FolderFace.Weather, as the weather face's;
    /// docs\widgets.md §236) — the way the sea goes and the next high or low, "High 14:32", from
    /// Open-Meteo's marine service. Its option: what its words say.
    /// </summary>
    public static readonly FaceInfo Tides = new(TidesId, WidgetInfos.Tides.Glyph)
    {
        Options =
        [
            new FaceOption(TidesFace.Show, FaceOptionKind.Choice, TidesFace.Next)
            {
                Choices = [TidesFace.Next, TidesFace.NextHeight, TidesFace.Countdown, TidesFace.Level],
                Glyph = OptionGlyphs.Style,
                Slot = OptionSlot.Style,
                ChoiceGlyphs = [OptionGlyphs.Time, OptionGlyphs.Ruler, OptionGlyphs.Seconds, OptionGlyphs.Graph],
            },
        ],
    };

    /// <summary>
    /// quakes: the latest earthquake near the face's own place (FolderFace.Weather, as the weather
    /// face's; docs\widgets.md §240) — a dot in its magnitude's colour and "M4.8 · 120 km", from the
    /// USGS's feed. Its options: how far it looks, the least magnitude, the alert and how much its words
    /// say.
    /// </summary>
    public static readonly FaceInfo Quakes = new(QuakesId, WidgetInfos.Quakes.Glyph)
    {
        Options =
        [
            new FaceOption(QuakesFace.Radius, FaceOptionKind.Choice, QuakeChoices.RadiusWord(QuakeChoices.DefaultRadius))
            {
                Choices = QuakeChoices.RadiusWords,
                // §411 (R13): a length, apart from the place it is around.
                Glyph = OptionGlyphs.Ruler,
                Slot = OptionSlot.Scope,
                ChoiceGlyphs = OptionGlyphs.Radii,
            },
            new FaceOption(QuakesFace.Magnitude, FaceOptionKind.Choice, QuakeChoices.MagnitudeWord(QuakeChoices.DefaultMagnitude))
            {
                Choices = QuakeChoices.MagnitudeWords,
                Glyph = OptionGlyphs.Quake,
                Slot = OptionSlot.Scope,
            },
            new FaceOption(QuakesFace.Alerts, FaceOptionKind.Switch, "true") { Glyph = OptionGlyphs.Bell, Slot = OptionSlot.Behaviour },
            new FaceOption(QuakesFace.Width, FaceOptionKind.Choice, QuakesFace.Standard) { Choices = [QuakesFace.Compact, QuakesFace.Standard, QuakesFace.Wide], Glyph = OptionGlyphs.Style, ChoiceGlyphs = OptionGlyphs.Widths, Slot = OptionSlot.Style },
        ],
    };

    /// <summary>
    /// sun: the next sunrise or sunset at the face's own place (FolderFace.Weather, as the weather face's;
    /// docs\widgets.md §368), worked out on this PC — the sun on the horizon and "18:32". Its option:
    /// what its words say.
    /// </summary>
    public static readonly FaceInfo Sun = new(SunId, WidgetInfos.SunArc.Glyph)
    {
        Options =
        [
            new FaceOption(SunFace.Show, FaceOptionKind.Choice, SunFace.Next)
            {
                Choices = [SunFace.Next, SunFace.Named, SunFace.Countdown],
                Glyph = OptionGlyphs.Style,
                Slot = OptionSlot.Style,
                ChoiceGlyphs = [OptionGlyphs.Time, OptionGlyphs.Label, OptionGlyphs.Seconds],
            },
        ],
    };

    /// <summary>
    /// sports: the folder's first sports line's match (docs\widgets.md §239), as a drive face shows its
    /// drive line's: "FCB 2–1 RMA 67'" with a red dot while it is on, "vs RMA Sat 20:00" before it, the
    /// final score for a few hours after. The team is the line's, set in one place.
    /// </summary>
    public static readonly FaceInfo Sports = new(SportsId, WidgetInfos.Sports.Glyph);

    /// <summary>Every face, in the order offered.</summary>
    public static IReadOnlyList<FaceInfo> All { get; } =
        [Icon, Clock, Calendar, DateTime, Weather, Wifi, Bluetooth, Sound, Removable, Timer, Focus, Stopwatch, Hotspot, Cast, Nearby, Battery, Power, Mail, NowPlaying,
         Cpu, Gpu, Memory, Disk, NetworkActivity, ScreenTime, Markets, News, Drive, NetworkDrive, Notifications, Clipboard, Contacts, WinSearch, Desktops, Keyboard, Vpn, Layouts, Meeting, AirQuality, Tides,
         Sports, Quakes, Sun];

    /// <summary>
    /// quick: the switch a switch's face shows, on or off (the VPN's); null for every other face.
    /// </summary>
    public static QuickSwitch? SwitchOf(FaceInfo face) =>
        face == Vpn ? QuickSwitch.Vpn : null;

    /// <summary>
    /// The widget a drive face reads in its folder (<paramref name="face"/>): the drive's or the
    /// network drive's line; null for every other face.
    /// </summary>
    public static WidgetInfo? DriveOf(FaceInfo face) =>
        face == Drive ? WidgetInfos.Drive : face == NetworkDrive ? WidgetInfos.NetworkDrive : null;

    /// <summary>
    /// The widget a countdown or stopwatch face reads in its folder (<paramref name="face"/>): the
    /// timer's, the focus session's or the stopwatch's; null for every other face.
    /// </summary>
    public static WidgetInfo? LineOf(FaceInfo face) =>
        face == Timer ? WidgetInfos.Timer : face == Focus ? WidgetInfos.Focus : face == Stopwatch ? WidgetInfos.Stopwatch : null;

    /// <summary>The face <paramref name="id"/>, or null for one this build does not know.</summary>
    public static FaceInfo? Find(string? id) =>
        All.FirstOrDefault(face => string.Equals(face.Id, id, StringComparison.Ordinal));

    /// <summary>The face <paramref name="id"/>, or <see cref="Icon"/> for one this build does not know.</summary>
    public static FaceInfo Of(string? id) => Find(id) ?? Icon;

    /// <summary>
    /// Whether <paramref name="folder"/> may wear <paramref name="face"/> among its faces — added, or
    /// put in the place of its face at <paramref name="replacing"/>: a face that repeats always, any
    /// other where none of the folder's other faces is it already (<see cref="FaceInfo.Repeats"/>).
    /// </summary>
    public static bool CanWear(FolderOptions folder, FaceInfo face, int replacing = -1)
    {
        if (face.Repeats)
        {
            return true;
        }
        for (var index = 0; index < folder.Faces.Count; index++)
        {
            if (index != replacing && folder.Faces[index].Info == face)
            {
                return false;
            }
        }
        return true;
    }
}

/// <summary>
/// The performance faces' option keys, and what a face of a reading follows (<see cref="FolderFaces.Cpu"/>
/// and the four beside it).
/// </summary>
public static class PerformanceFace
{
    /// <summary>How often it steps, a pace of <see cref="PerformanceChoices.Paces"/> in seconds, as a reading's line's.</summary>
    public const string Pace = "pace";

    /// <summary>The graphics face's adapter, as the entry's; empty for the main one.</summary>
    public const string Adapter = "adapter";

    /// <summary>The disk face's disk, as the entry's; empty for every disk together.</summary>
    public const string Disk = "disk";

    /// <summary>§295: its style, the figure alone (<see cref="Figure"/>) or with its graph behind it (<see cref="Graph"/>), as a reading's line's.</summary>
    public const string Style = "style";

    /// <summary>The figure alone: the face as it always was, and the default.</summary>
    public const string Figure = WidgetStyles.FigureWord;

    /// <summary>The figure with its recent history as a graph behind it.</summary>
    public const string Graph = WidgetStyles.GraphWord;

    /// <summary>The slowest pace, in seconds a step: the last of <see cref="PerformanceChoices.Paces"/>, a minute.</summary>
    public const int SlowestPace = 60;

    /// <summary>The reading <paramref name="face"/> shows; null for a face that is no reading's.</summary>
    public static Metric? MetricOf(FaceInfo face) =>
        face == FolderFaces.Cpu ? Metric.Cpu
        : face == FolderFaces.Gpu ? Metric.Gpu
        : face == FolderFaces.Memory ? Metric.Memory
        : face == FolderFaces.Disk ? Metric.Disk
        : face == FolderFaces.NetworkActivity ? Metric.Network
        : null;

    /// <summary>The face of <paramref name="metric"/>.</summary>
    public static FaceInfo Of(Metric metric) => metric switch
    {
        Metric.Cpu => FolderFaces.Cpu,
        Metric.Gpu => FolderFaces.Gpu,
        Metric.Memory => FolderFaces.Memory,
        Metric.Disk => FolderFaces.Disk,
        _ => FolderFaces.NetworkActivity,
    };

    /// <summary>
    /// What a face of a reading is set to, as a reading's line keeps it: its pace, its adapter or disk,
    /// and whether it draws its graph — its style, as the line's Numbers is its graph's form drawing
    /// none (§229, §295).
    /// </summary>
    public static PerformanceOptions Options(FolderFace face)
    {
        var info = face.Info;
        return new PerformanceOptions
        {
            ShowGraph = info.Option(Style) is { } style && face.Text(style) == Graph,
            Seconds = info.Option(Pace) is { } pace ? PerformanceChoices.Nearest(int.Parse(face.Text(pace), System.Globalization.CultureInfo.InvariantCulture)) : 1,
            GpuAdapter = info.Option(Adapter) is { } adapter ? face.Text(adapter) : "",
            Disk = info.Option(Disk) is { } disk && face.Text(disk) is { Length: > 0 } named ? named : null,
        };
    }
}

/// <summary>The clock face's option keys and words.</summary>
public static class ClockFace
{
    public const string Seconds = "seconds";
    public const string Hours = "hours";
    public const string Region = "region";
    public const string Twelve = "twelve";
    public const string TwentyFour = "twentyFour";
    public const string Zone = "zone";
    public const string Label = "label";
    public const string Timer = "timer";

    /// <summary>A clock face: the time, with its seconds where asked; the rest its defaults.</summary>
    public static FolderFace New(bool seconds = false)
    {
        var face = FolderFace.Of(FolderFaces.ClockId);
        if (seconds)
        {
            face.Set(FolderFaces.Clock.Option(Seconds)!, true);
        }
        return face;
    }

    /// <summary>The clock face's settings, read from <paramref name="face"/> through its descriptor.</summary>
    public static ClockFaceSettings Read(FolderFace face)
    {
        var info = FolderFaces.Clock;
        return new ClockFaceSettings(
            Seconds: face.Switch(info.Option(Seconds)!),
            Hours: face.Text(info.Option(Hours)!),
            Zone: face.Text(info.Option(Zone)!),
            Label: face.Text(info.Option(Label)!),
            Timer: face.Switch(info.Option(Timer)!));
    }

    /// <summary>
    /// A time pattern ("h:mm:ss tt", "HH:mm") as <paramref name="hours"/> asks: the region's own
    /// for <see cref="Region"/>; with a 12-hour clock's hour and its AM/PM for <see cref="Twelve"/>;
    /// with a 24-hour clock's hour and no AM/PM for <see cref="TwentyFour"/>. Quoted text is left
    /// alone. The seconds are the caller's (Nls.WithoutSeconds).
    /// </summary>
    public static string Pattern(string pattern, string hours)
    {
        if (hours is not (Twelve or TwentyFour))
        {
            return pattern;
        }
        var twelve = hours == Twelve;
        var result = new System.Text.StringBuilder();
        var quoted = false;
        var marker = false;
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '\'')
            {
                quoted = !quoted;
                result.Append(c);
                continue;
            }
            if (quoted)
            {
                result.Append(c);
                continue;
            }
            switch (c)
            {
                case 'h' or 'H':
                    var run = 1;
                    while (i + 1 < pattern.Length && pattern[i + 1] == c)
                    {
                        i++;
                        run++;
                    }
                    // 24-hour hours keep the region's padding ("HH" stays two figures); a 12-hour
                    // clock's hour is written as the 12-hour taskbar writes it, one figure before ten.
                    result.Append(twelve ? "h" : run >= 2 ? "HH" : "H");
                    break;
                case 't':
                    while (i + 1 < pattern.Length && pattern[i + 1] == 't')
                    {
                        i++;
                    }
                    marker = true;
                    if (twelve)
                    {
                        result.Append("tt");
                    }
                    else
                    {
                        // The marker goes, and the space that set it apart from the time with it.
                        while (result.Length > 0 && result[^1] == ' ')
                        {
                            result.Length--;
                        }
                        while (i + 1 < pattern.Length && pattern[i + 1] == ' ')
                        {
                            i++;
                        }
                    }
                    break;
                default:
                    result.Append(c);
                    break;
            }
        }
        if (twelve && !marker)
        {
            result.Append(" tt");
        }
        var text = result.ToString().Trim();
        return text.Length > 0 ? text : pattern;
    }
}

/// <summary>A clock face's settings, as its drawing reads them.</summary>
/// <param name="Seconds">The seconds in the time.</param>
/// <param name="Hours">The region's clock, or a 12- or a 24-hour one (<see cref="ClockFace.Hours"/>).</param>
/// <param name="Zone">A Windows time zone id; empty for this PC's.</param>
/// <param name="Label">Shown after the time, such as a city ("Tokyo").</param>
/// <param name="Timer">What a timer or a focus session in the folder has left, after the time while it runs.</param>
public readonly record struct ClockFaceSettings(bool Seconds, string Hours, string Zone, string Label, bool Timer);

/// <summary>The time-and-date face's option and its words (<see cref="FolderFaces.DateTime"/>).</summary>
public static class DateTimeFace
{
    /// <summary>The seconds in the time: as Windows' own clock has them, always, or never.</summary>
    public const string Seconds = "seconds";

    public const string Windows = "windows";
    public const string Shown = "shown";
    public const string Hidden = "hidden";

    /// <summary>The zone the time and the date are in, a Windows zone id; this PC's where empty.</summary>
    public const string Zone = ClockFace.Zone;

    /// <summary>The zone <paramref name="face"/> shows the time and the date in; empty for this PC's.</summary>
    public static string ZoneOf(FolderFace face) => face.Text(FolderFaces.DateTime.Option(Zone)!);

    /// <summary>
    /// Whether <paramref name="face"/> shows the seconds, where Windows' clock shows them
    /// (<paramref name="windows"/>, ShowSecondsInSystemClock) for "as Windows has them".
    /// </summary>
    public static bool ShowsSeconds(FolderFace face, bool windows) =>
        face.Text(FolderFaces.DateTime.Option(Seconds)!) switch
        {
            Shown => true,
            Hidden => false,
            _ => windows,
        };
}

/// <summary>The now-playing face's option keys.</summary>
public static class NowPlayingFace
{
    /// <summary>The track's art before its words, as the entry's "Show album art" had it; on unless set off.</summary>
    public const string Art = "art";

    /// <summary>
    /// The face off the bar while nothing plays, as the entry's "Hide it while nothing plays" took the
    /// entry off; on unless set off (the person, 2026-10-23: "this should be on by default", §319) —
    /// a file that says false keeps it off, one that says nothing has it on. While the bar is arranged
    /// the face is there whatever it says, greyed while nothing plays, so it can be found and set.
    /// </summary>
    public const string HideIdle = "hideIdle";

    /// <summary>Whether <paramref name="face"/> shows the art.</summary>
    public static bool ShowsArt(FolderFace face) => face.Switch(FolderFaces.NowPlaying.Option(Art)!);

    /// <summary>Whether <paramref name="face"/> takes its folder off the bar while nothing plays.</summary>
    public static bool HidesIdle(FolderFace face) => face.Switch(FolderFaces.NowPlaying.Option(HideIdle)!);

    /// <summary>
    /// §503 (the person, 2026-11-05: "the now playing widget on the taskbar should have three fixed widths"): how
    /// long the face is along the bar, whatever plays — its words cut with an ellipsis past their room, never the
    /// face growing or shrinking for them. Its words are a folder's list widths' (Narrow, Medium, Wide).
    /// </summary>
    public const string Width = "width";

    public const string Narrow = "narrow";
    public const string Medium = "medium";
    public const string Wide = "wide";

    /// <summary>The width <paramref name="face"/> is set to: one of the three, Medium for anything else.</summary>
    public static string WidthOf(FolderFace face) => face.Text(FolderFaces.NowPlaying.Option(Width)!);

    /// <summary>
    /// How long a face of <paramref name="width"/> is along a horizontal bar, in DIPs at 100 % text, its art in it
    /// where it shows it: beside Windows' own taskbar widgets — the weather's about 140 DIP, the clock's about 80 —
    /// a short title, a title and its artist, a long title.
    /// </summary>
    public static int Dips(string width) => width switch
    {
        Narrow => 128,
        Wide => 256,
        _ => 192,
    };
}

/// <summary>The next meeting's face's option keys and words (<see cref="FolderFaces.Meeting"/>).</summary>
public static class MeetingFace
{
    /// <summary>Which meeting it shows: today's next, the next whatever the day, or one only shortly before it.</summary>
    public const string Show = "show";

    /// <summary>The meeting on now, else today's next; "No more meetings today" after the last.</summary>
    public const string Today = "today";

    /// <summary>The meeting on now, else the next of the coming week, its day said where it is not today's.</summary>
    public const string Next = "next";

    /// <summary>Only from <see cref="Lead"/> before a meeting until it ends: the folder takes no room on the bar the rest of the day.</summary>
    public const string Soon = "soon";

    /// <summary>From how many minutes before its start a meeting is lit — its time bold, Join in the accent — five to thirty.</summary>
    public const string Lead = "lead";

    public const string Five = "five";
    public const string Ten = "ten";
    public const string Fifteen = "fifteen";
    public const string Thirty = "thirty";

    /// <summary>How much room the words keep on the bar: the time alone, a short title, a long one.</summary>
    public const string Width = "width";

    public const string Compact = "compact";
    public const string Standard = "standard";
    public const string Wide = "wide";

    /// <summary>The Join button beside the words, for a meeting with a link; on unless set off.</summary>
    public const string Join = "join";

    /// <summary>A next-meeting face as it starts: today's next meeting, lit from ten minutes before, a short title, Join.</summary>
    public static FolderFace New() => FolderFace.Of(FolderFaces.MeetingId);

    /// <summary>Which meeting <paramref name="face"/> shows (<see cref="Today"/>, <see cref="Next"/> or <see cref="Soon"/>).</summary>
    public static string ShowOf(FolderFace face) => face.Text(FolderFaces.Meeting.Option(Show)!);

    /// <summary>From how long before its start a meeting is lit on <paramref name="face"/>, and shown at all where it shows them only then.</summary>
    public static TimeSpan LeadOf(FolderFace face) => TimeSpan.FromMinutes(face.Text(FolderFaces.Meeting.Option(Lead)!) switch
    {
        Five => 5,
        Fifteen => 15,
        Thirty => 30,
        _ => 10,
    });

    /// <summary>How much room <paramref name="face"/>'s words keep (<see cref="Compact"/>, <see cref="Standard"/> or <see cref="Wide"/>).</summary>
    public static string WidthOf(FolderFace face) => face.Text(FolderFaces.Meeting.Option(Width)!);

    /// <summary>Whether <paramref name="face"/> keeps room for the Join button.</summary>
    public static bool Joins(FolderFace face) => face.Switch(FolderFaces.Meeting.Option(Join)!);
}

/// <summary>The bell face's option key.</summary>
public static class NotificationsFace
{
    /// <summary>How many are new on the bell's corner, as the bell's "Show how many are new" had it; on unless set off.</summary>
    public const string Count = "count";

    /// <summary>Whether <paramref name="face"/> shows how many are new.</summary>
    public static bool ShowsCount(FolderFace face) => face.Info.Option(Count) is { } option && face.Switch(option);
}

/// <summary>The drive faces' option key.</summary>
public static class DriveFace
{
    /// <summary>The drive's name beside its picture, where there is room, as the entry's "Show its name" had it; on unless set off.</summary>
    public const string Name = "name";

    /// <summary>Whether <paramref name="face"/> says the drive's name rather than its letter alone.</summary>
    public static bool ShowsName(FolderFace face) => face.Info.Option(Name) is { } option && face.Switch(option);
}

/// <summary>The news face's option key.</summary>
public static class NewsFace
{
    /// <summary>The newest headline beside the glyph, as the entry's "Show the newest headline" had it; on unless set off.</summary>
    public const string Headline = "headline";

    /// <summary>Whether <paramref name="face"/> says the newest headline.</summary>
    public static bool ShowsHeadline(FolderFace face) => face.Switch(FolderFaces.News.Option(Headline)!);
}

/// <summary>The calendar face's option keys and words.</summary>
public static class CalendarFace
{
    public const string Look = "look";
    public const string Text = "text";
    public const string Page = "page";
    public const string Form = "form";
    public const string Short = "short";
    public const string DayAndDate = "dayDate";

    /// <summary>Whose today it shows: a Windows zone id; this PC's where empty (§230).</summary>
    public const string Zone = ClockFace.Zone;

    /// <summary>The zone whose date <paramref name="face"/> shows; empty for this PC's.</summary>
    public static string ZoneOf(FolderFace face) => face.Text(FolderFaces.Calendar.Option(Zone)!);

    /// <summary>A calendar face: the date as text, the day's name and the date where <paramref name="withDay"/>, as the calendar entry wrote it.</summary>
    public static FolderFace New(bool withDay = false)
    {
        var face = FolderFace.Of(FolderFaces.CalendarId);
        if (withDay)
        {
            face.Set(FolderFaces.Calendar.Option(Form)!, DayAndDate);
        }
        return face;
    }

    /// <summary>Whether <paramref name="face"/> is the calendar page rather than the date as text.</summary>
    public static bool IsPage(FolderFace face) => face.Text(FolderFaces.Calendar.Option(Look)!) == Page;

    /// <summary>Whether the text is the day's name and the date ("Fri, 09.10.") rather than the short date ("09.10.2026").</summary>
    public static bool WithDay(FolderFace face) => face.Text(FolderFaces.Calendar.Option(Form)!) == DayAndDate;
}

/// <summary>
/// The weather face's option and its looks. Simple is the default: the glyph and the temperature,
/// which is what the weather on the bar has always been. Every look is one row across a horizontal
/// bar and takes the width its content needs; down a bar on its side, and before a place is set,
/// the weather is its simple self whichever look is set.
/// </summary>
public static class WeatherFace
{
    public const string Look = "look";

    /// <summary>The conditions' glyph and the temperature.</summary>
    public const string Simple = "simple";

    /// <summary>The same, and the day's high and low after them.</summary>
    public const string Detailed = "detailed";

    /// <summary>Today and the next two days: the weekday, the conditions' glyph and the high and low.</summary>
    public const string Forecast = "forecast";

    /// <summary>
    /// Windows' Widgets button (2026-10-13; a new bar's until 2026-10-14): the conditions' glyph, then the
    /// temperature over the conditions in words, "18°" over "Partly cloudy".
    /// </summary>
    public const string Conditions = "conditions";

    /// <summary>A weather face in <paramref name="look"/>.</summary>
    public static FolderFace New(string look = Simple)
    {
        var face = FolderFace.Of(FolderFaces.WeatherId);
        if (look != Simple)
        {
            face.Set(FolderFaces.Weather.Option(Look)!, look);
        }
        return face;
    }

    /// <summary>The look <paramref name="face"/> is set to: one of the three, Simple for anything else.</summary>
    public static string LookOf(FolderFace face) => face.Text(FolderFaces.Weather.Option(Look)!);
}

/// <summary>The air-quality face's option keys and words (<see cref="FolderFaces.AirQuality"/>, docs\widgets.md §210).</summary>
public static class AirQualityFace
{
    /// <summary>Which index it shows (<see cref="AirIndexes"/>): the region's, the European or the US one.</summary>
    public const string Index = "index";

    /// <summary>How much its words say: the index alone, the index and its level, or "AQI", the index and its level.</summary>
    public const string Width = "width";

    /// <summary>"32".</summary>
    public const string Compact = "compact";

    /// <summary>"32 · Good".</summary>
    public const string Standard = "standard";

    /// <summary>"AQI 32 · Good".</summary>
    public const string Wide = "wide";

    /// <summary>An air-quality face as it starts: for where the device is, the region's index, "32 · Good", in the middle.</summary>
    public static FolderFace New() => FolderFace.Of(FolderFaces.AirQualityId);

    /// <summary>The index <paramref name="face"/> is set to; <see cref="AirIndexes.Auto"/> for anything else.</summary>
    public static string IndexOf(FolderFace face) => face.Text(FolderFaces.AirQuality.Option(Index)!);

    /// <summary>How much <paramref name="face"/>'s words say (<see cref="Compact"/>, <see cref="Standard"/> or <see cref="Wide"/>).</summary>
    public static string WidthOf(FolderFace face) => face.Text(FolderFaces.AirQuality.Option(Width)!);
}

/// <summary>The sun face's option keys and words (<see cref="FolderFaces.Sun"/>, docs\widgets.md §368).</summary>
public static class SunFace
{
    /// <summary>What its words say, after the sun on the horizon.</summary>
    public const string Show = "show";

    /// <summary>"18:32": the next sunrise's or sunset's time, its glyph saying which.</summary>
    public const string Next = "next";

    /// <summary>"Sunset 18:32".</summary>
    public const string Named = "named";

    /// <summary>"Sunset in 3 h 10 min".</summary>
    public const string Countdown = "countdown";

    /// <summary>A sun face as it starts: for where the device is, "18:32".</summary>
    public static FolderFace New() => FolderFace.Of(FolderFaces.SunId);

    /// <summary>What <paramref name="face"/>'s words say (<see cref="Next"/>, <see cref="Named"/> or <see cref="Countdown"/>).</summary>
    public static string ShowOf(FolderFace face) => face.Text(FolderFaces.Sun.Option(Show)!);

    /// <summary>The place the sun face of <paramref name="folder"/> is for; null where it has none set or wears no such face.</summary>
    public static WeatherOptions? PlaceOfFace(FolderOptions folder) =>
        folder.FaceOf(FolderFaces.SunId)?.Weather is { IsSet: true } set ? set : null;
}

/// <summary>The tides face's option keys and words (<see cref="FolderFaces.Tides"/>, docs\widgets.md §236).</summary>
public static class TidesFace
{
    /// <summary>What its words say, after the arrow of the way the sea goes.</summary>
    public const string Show = "show";

    /// <summary>"High 14:32".</summary>
    public const string Next = "next";

    /// <summary>"High 14:32 · 2.1 m".</summary>
    public const string NextHeight = "nextHeight";

    /// <summary>"High in 3 h 10 min".</summary>
    public const string Countdown = "countdown";

    /// <summary>"1.2 m": the sea's height now.</summary>
    public const string Level = "level";

    /// <summary>A tides face as it starts: for where the device is, "High 14:32", in the middle.</summary>
    public static FolderFace New() => FolderFace.Of(FolderFaces.TidesId);

    /// <summary>What <paramref name="face"/>'s words say (<see cref="Next"/>, <see cref="NextHeight"/>, <see cref="Countdown"/> or <see cref="Level"/>).</summary>
    public static string ShowOf(FolderFace face) => face.Text(FolderFaces.Tides.Option(Show)!);

    /// <summary>
    /// The place the tides face of <paramref name="folder"/> is for, its own as the weather face's is
    /// (<see cref="FolderFace.Weather"/>); null where it has none set or wears no such face.
    /// </summary>
    public static WeatherOptions? PlaceOfFace(FolderOptions folder) =>
        folder.FaceOf(FolderFaces.TidesId)?.Weather is { IsSet: true } set ? set : null;
}
