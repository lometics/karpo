// menus: the settings a menu sets with a picker rather than a switch, a list or a number (2026-10-19,
// the person: "inline every setting of every bar widget and folder widget into its edit menu"; docs\
// widgets.md §224). Rows of OptionRows like the others, read from the widget and set there, so the
// menu writes what Settings' page writes; Karpo.Dock\MenuPickers.cs is how a menu shows them:
//
//  - **A colour** (OptionRowKind.Color, §405, §459): a part's background, outline, text or accent in a style's editor
//    (LookRows; Paints). Its choices are tiles (§574: no "Default (…)", a colour of the Appearance a value as any other):
//    None for a background or an outline, the recent colours, then the grid (ColorPalette), a swatch picked
//    keeping the opacity in force; and a colour of one's own, mixed in the picker itself (§523: SetColor, ColorMix), its
//    opacity with it where the value takes one (Alpha), kept first among the recent colours.
//  - **An icon** (OptionRowKind.Glyph): a folder's, a pin's, a feed's, a layout's (§309). The cross first, which takes
//    the icon chosen away and leaves the default, "Default (Folder)" (§518; §306: one way to clear it, the same in every
//    picker), then the whole gallery in one grid (MenuIcons, §249, §306), each tile with the words it
//    is found by, which the picker's field filters the tiles by as the settings app's page searched
//    them (IconSearch).
//  - **A target** (OptionRowKind.Target): "Browse…" over the field of what a pin opens, of its kind (OptionFields.
//    OfPinTarget, §292, §425), Windows' open dialog for what it Browses (SetText).
//  - **A time of day** (OptionRowKind.Time): an alarm's, minutes after midnight; hours and minutes
//    stepped as a time picker's columns turn (TimeSteps), on the clock Karpo shows (12 or 24 hours).
//  - **A length** (OptionRowKind.Duration): a timer's or a focus session's, in minutes, as hours and
//    minutes, a minute to twelve hours.

using System.Globalization;

namespace Karpo.Core;

/// <summary>
/// A tile of a colour's or an icon's row: its colour or its glyph, the name a screen reader says, the
/// group a colour's stands under (an icon's stand in one grid, §306), whether it is its row's plain
/// choice (None, an account colour's Automatic, an icon's cross), and the words an icon's is found by (§249).
/// </summary>
public sealed record OptionTile(string Name, Argb? Color = null, string Glyph = "", string Group = "", bool Plain = false, IReadOnlyList<string>? Words = null);

/// <summary>
/// What a widget's rows are read from and written to: the config (its style, the clock Karpo shows), and the mode its
/// swatches are shown in.
/// </summary>
/// <param name="Exists">Whether a folder is there, which a pin's folder rows ask (<see cref="PinFolders.FolderOf"/>); null for the disk's.</param>
public sealed record OptionPlace(Config Config, bool Dark, Func<string, bool>? Exists = null)
{
    /// <summary>
    /// §345: the layout whose bar the menu is on, whose behaviour its rows read and write; null for
    /// <see cref="Config"/>'s default layout.
    /// </summary>
    public TaskbarLayout? Layout { get; init; }

    /// <summary>§405: the folder the widget stands in as a line; null on a bar. §464: nothing of its style reaches the line.</summary>
    public BarItem? Folder { get; init; }

    /// <summary>
    /// §589: the bar the widget stands on is on a display's left or right edge: what runs along it reads Top, Center and
    /// Bottom (a folder's Alignment), not Left, Center and Right.
    /// </summary>
    public bool Side { get; init; }
}

/// <summary>The pickers' parts of a row (OptionPickers).</summary>
public sealed partial record OptionRow
{
    /// <summary>A colour's or an icon's choices, in <see cref="Chosen"/>'s order: <see cref="Set"/> takes a tile's place.</summary>
    public IReadOnlyList<OptionTile> Tiles { get; init; } = [];

    /// <summary>A colour as it is now; null for Automatic (an account's, PartColours), one of no alpha for None.</summary>
    public Argb? Color { get; init; }

    /// <summary>§523: whether a colour row's value takes an alpha: its picker has an opacity slider, its hex box "#RRGGBBAA".</summary>
    public bool Alpha { get; init; }

    /// <summary>A time of day on a 12-hour clock, with these for before and after noon.</summary>
    public bool TwelveHour { get; init; }

    public string Morning { get; init; } = "AM";

    public string Evening { get; init; } = "PM";

    /// <summary>
    /// §523: a colour of one's own, mixed in the picker (its spectrum, its sliders, its hex box), written as it is — its alpha
    /// where the value takes one (<see cref="Alpha"/>), else opaque —; kept (the second argument true: let go, Enter, typed)
    /// it goes first among the recent colours where the row has them, a live look while it is dragged (false) not. False
    /// where nothing changed, or where the row writes only what is kept (a part's colour, an account's own).
    /// </summary>
    public Func<Argb, bool, bool>? SetColor { get; init; }

    /// <summary>An icon's row: the glyph drawn while none is chosen, which its row shows at its end; empty for an app's own icon.</summary>
    public string Fallback { get; init; } = "";

    /// <summary>§425: what a target's "Browse…" lets Windows' open dialog pick: a file or a folder, a file, a folder.</summary>
    public TargetBrowse Browses { get; init; }

    /// <summary>
    /// §610: the row stands in the line of the row it is beside, its control before that row's (the settings panel's
    /// <c>PanelRow.Pair</c>): the row before it — the palette worn's reset before Palette's dropdown, §613 a value's reset
    /// before its control —, or, alone in a thing's level, that thing (§609 a holiday calendar's region). A menu shows it where
    /// it is.
    /// </summary>
    public bool Beside { get; init; }

    /// <summary>
    /// §613: a row that acts drawn as Windows' subtle button, its icon alone (no fill until the pointer is on it), its words
    /// its tooltip: a value's reset beside it. False for a button of words.
    /// </summary>
    public bool Subtle { get; init; }
}

/// <summary>§425: what Windows' open dialog picks for a "Browse…" (OptionRowKind.Target).</summary>
public enum TargetBrowse
{
    /// <summary>A file, or a folder by its "Select folder" button: a folder's contents'.</summary>
    Any,

    /// <summary>A file alone: a pin's File.</summary>
    File,

    /// <summary>A folder alone: a pin's Folder.</summary>
    Folder,
}

/// <summary>The picker rows of a widget and a folder; see the top of the file.</summary>
public static class OptionPickers
{
    /// <summary>pins: what it opens (OptionFields.OfPinTarget, §292): a pin's level of kinds (§425), a folder's contents' folder found or typed.</summary>
    public const string PinTarget = "PinTarget";

    /// <summary>A folder's contents: "Browse…" over their folder's field, Windows' open dialog.</summary>
    public const string PinBrowse = "PinBrowse";

    // §425: a pin's kinds of target, each a level of its own under Target, and their "Browse…".

    public const string PinApp = "PinApp";
    public const string PinFolder = "PinFolder";
    public const string PinFolderBrowse = "PinFolderBrowse";
    public const string PinFile = "PinFile";
    public const string PinFileBrowse = "PinFileBrowse";
    public const string PinSite = "PinSite";
    public const string PinSetting = "PinSetting";

    public const string PinIcon = "PinIcon";

    /// <summary>rss: the icon chosen in place of the site's own (§306).</summary>
    public const string RssIcon = "RssIcon";

    /// <summary>A folder's own icon: the face row's kind, as OptionRows keys it.</summary>
    public const string FolderIcon = "icon";

    /// <summary>
    /// <paramref name="item"/>'s rows (<paramref name="rows"/>, its page's) with its pickers: an alarm's
    /// time (the page's time picker) and a countdown's length (its number box) as steppers in place of
    /// their rows; a divider's Style (§435). A pin's are OptionRows.Pin's.
    /// </summary>
    public static void Into(WidgetItem item, OptionPlace? place, List<OptionRow> rows)
    {
        for (var at = 0; at < rows.Count; at++)
        {
            if (rows[at].Key == OptionRows.AlarmTime)
            {
                rows[at] = Clock(item, place?.Config);
            }
            else if (rows[at].Key == OptionRows.CountdownMinutes && rows[at].Kind == OptionRowKind.Number)
            {
                // The page's own row and write (CountdownLength), its hours and minutes stepped apart.
                rows[at] = rows[at] with { Kind = OptionRowKind.Duration };
            }
        }
        // §505: a divider is empty space, with no look and nothing that sets one (§351 to §502 its kind's look, the
        // style its layout wears, linked to as Layout ↗). §435: a folder's divider's own Style, its fixed room or Grow, a
        // row of its menu's (§517; §435 to §513 at its foot, where another widget's Look stands, R16).
        if (item.Info == WidgetInfos.Divider && item is CellItem divider)
        {
            rows.Add(DividerStyleRow(divider));
        }
    }

    /// <summary>§435: a divider's Style: its fixed room, which is no options at all, or Grow (<see cref="DividerOptions"/>).</summary>
    private static OptionRow DividerStyleRow(CellItem divider)
    {
        var chosen = divider.Divider?.Style == DividerStyle.Grow ? 1 : 0;
        return new OptionRow(OptionRows.DividerStyle, OptionRowKind.Choice, S.SettingsWidgetStyleHeader)
        {
            Glyph = OptionGlyphs.Style,
            Slot = OptionSlot.Style,
            Choices = [S.DockEditHeightFixedHeight, S.SettingsWidgetSeparatorGrow],
            ChoiceGlyphs = [OptionGlyphs.Ruler, OptionGlyphs.Fill],
            Chosen = chosen,
            Set = at =>
            {
                if (at is < 0 or > 1 || at == chosen)
                {
                    return false;
                }
                divider.Divider = at == 1 ? new DividerOptions { Style = DividerStyle.Grow } : null;
                return true;
            },
        };
    }

    /// <summary>
    /// pins: "Browse…", Windows' open dialog (TargetDialog) for what <paramref name="browses"/> says, opening near what
    /// <paramref name="pin"/> opens: over the field <paramref name="within"/>, on that row's level (OptionRow.Within).
    /// </summary>
    public static OptionRow Browse(PinOptions pin, string key = PinBrowse, string within = PinTarget, TargetBrowse browses = TargetBrowse.Any) =>
        Browse(pin.Target, target => OptionTexts.SetPinTarget(pin, target), key, within, browses);

    /// <summary>§532: "Browse…" for what opens <paramref name="target"/>, set by <paramref name="set"/> (a pin's, a taskbar menu shortcut's).</summary>
    public static OptionRow Browse(string target, Func<string, bool> set, string key, string within, TargetBrowse browses) => new(key, OptionRowKind.Target, S.SettingsPinTargetBrowse)
    {
        Glyph = OptionGlyphs.FolderOpen,
        Slot = OptionSlot.Subject,
        Within = within,
        Text = target,
        Browses = browses,
        SetText = set,
    };

    /// <summary>pins: what it is drawn with, its target's own icon for none.</summary>
    public static OptionRow PinIconRow(PinOptions pin) =>
        Icons(PinIcon, pin.IconIsGlyph ? pin.Icon : "", glyph => pin.Icon = glyph) with { Slot = OptionSlot.Words };

    /// <summary>An icon face's own icon (FolderFace.Glyph, §259): the folder glyph for none.</summary>
    public static OptionRow Folder(FolderFace face) =>
        Icons(FolderIcon, face.Glyph ?? "", glyph => face.Glyph = glyph.Length > 0 ? glyph : null, FolderOptions.FolderGlyph);

    /// <summary>rss: what a feed is drawn with (RssOptions.Glyph, §306): its site's own icon, or the news glyph, for none.</summary>
    public static OptionRow RssIconRow(RssOptions rss, Func<RssOptions> kept) =>
        Icons(RssIcon, rss.Glyph ?? "", glyph => kept().Glyph = glyph.Length > 0 ? glyph : null, OptionGlyphs.Feed) with { Slot = OptionSlot.Words };

    /// <summary>
    /// §459: a colour's row of <paramref name="tone"/>'s choices in the mode <paramref name="dark"/>, keyed <paramref name="key"/>:
    /// <paramref name="shown"/> the colour it is (§574: a value, nothing else — no "Default (…)" of what it would come to),
    /// written by <paramref name="write"/>. None for a background or an outline; the recent colours; the grid. A swatch picked
    /// keeps the opacity the colour in force has; §523 a colour mixed is written with its own, and kept goes first among the
    /// recent ones.
    /// </summary>
    internal static OptionRow Paints(OptionPlace place, Tone tone, string key, string title, Paint shown, Func<Paint, bool> write, bool dark)
    {
        var config = place.Config;
        // §690: one line — None where it may be none, then the colours last mixed — and under it the presets every picker has.
        var palette = ColorPalette.Presets;
        List<OptionTile> tiles = [];
        if (ToneNames.MayBeNone(tone))
        {
            tiles.Add(new OptionTile(S.SettingsAppearanceSeparatorStyleNone, new Argb(0, 0, 0, 0), OptionGlyphs.None, Plain: true));
        }
        var first = tiles.Count;
        var recent = ColorPalette.Recent(config).Where(color => !palette.Contains(color)).Take(ColorPalette.Columns - first).ToList();
        tiles.AddRange(recent.Select(color => new OptionTile(Say(color), color)));
        tiles.AddRange(palette.Select(color => new OptionTile(ColorPalette.NameOf(color) ?? Say(color), color, Group: ColorPalette.PresetsGroup)));
        // The opacity a colour picked keeps: the one in force's, opaque where none is (§623: a backdrop half).
        var alpha = shown.Color?.A ?? (tone == Tone.Backdrop ? ToneNames.BackdropAlpha : (byte)255);
        Paint Picked(Argb color) => new(color with { A = alpha });
        return new OptionRow(key, OptionRowKind.Color, title)
        {
            Slot = OptionSlot.Look,
            Tiles = tiles,
            Chosen = shown switch
            {
                { None: true } => first > 0 ? 0 : -1,
                { Color: { } color } => tiles.FindIndex(first, tile => tile.Color == color with { A = 255 }),
                _ => -1,
            },
            Color = shown.None ? new Argb(0, 0, 0, 0) : shown.Color,
            Set = at => at >= 0 && at < tiles.Count && write(tiles[at].Plain ? Paint.Nothing : Picked(tiles[at].Color!.Value)),
            // §523: a colour mixed, its opacity its own; kept, first among the recent colours (opaque, as they all are).
            Alpha = true,
            SetColor = (mixed, kept) => write(new Paint(mixed)) | (kept && ColorPalette.Remember(config, mixed with { A = 255 })),
        };
    }

    /// <summary>
    /// A row that is a step of a stepper (<paramref name="row"/>'s value <paramref name="by"/>): a
    /// number's by its step within its range, a time of day's and a length's as TimeSteps steps them; null where it
    /// can't step that way.
    /// </summary>
    public static int? Stepped(OptionRow row, int by) => row.Kind switch
    {
        OptionRowKind.Time => TimeSteps.Clock(row.Value, by),
        OptionRowKind.Duration => TimeSteps.Length(row.Value, by, row.Least, row.Most),
        // §528: several widgets mixed are stepped to the first's value stepped, all of them, at its end too.
        _ => Math.Clamp(row.Value + (by * row.Step), row.Least, row.Most) is var stepped && (stepped != row.Value || row.Mixed) ? stepped : null,
    };

    /// <summary>
    /// Whether the time Karpo shows is on the 24-hour clock: Localization's time pattern where one is
    /// chosen (<paramref name="config"/>), else Windows' own.
    /// </summary>
    public static bool TwentyFourHours(Config? config) =>
        config?.Formats?.Time is { Length: > 0 } pattern
            ? pattern.Contains('H', StringComparison.Ordinal)
            : RegionFormats.FollowedTime(new DateTime(2000, 1, 1, 13, 0, 0)) is { } written && written.Contains("13", StringComparison.Ordinal);

    /// <summary>"#1E3A5F", or with its alpha "#1E3A5F, 80%", as a swatch says it; §509 no "100%" for an alpha that rounds to it.</summary>
    public static string Say(Argb color)
    {
        var hex = string.Create(CultureInfo.InvariantCulture, $"#{color.R:X2}{color.G:X2}{color.B:X2}");
        var percent = (int)Math.Round(color.A * 100 / 255.0);
        return percent == 100 ? hex : S.SettingsAppearanceCustomValue(hex, percent);
    }

    /// <summary>An alarm's time, as its page's time picker sets it (AlarmTimes), on the clock Karpo shows.</summary>
    private static OptionRow Clock(WidgetItem item, Config? config)
    {
        var twelve = !TwentyFourHours(config);
        return new OptionRow(OptionRows.AlarmTime, OptionRowKind.Time, S.SettingsWidgetAlarmTimeHeader)
        {
            Glyph = OptionGlyphs.Time,
            Slot = OptionSlot.Subject,
            Value = AlarmTimes.Minutes(item.Alarm?.Time),
            Least = 0,
            Most = AlarmTimes.Day - 1,
            TwelveHour = twelve,
            Morning = twelve ? Designator(9) ?? "AM" : "AM",
            Evening = twelve ? Designator(21) ?? "PM" : "PM",
            Set = minutes =>
            {
                // As the page's picker: only a time that is not the one set already (AlarmTimes.Picked).
                if (minutes is < 0 or >= AlarmTimes.Day || AlarmTimes.Picked(item.Alarm?.Time, TimeSpan.FromMinutes(minutes)) is not { } text)
                {
                    return false;
                }
                (item.Alarm ??= new AlarmOptions()).Time = text;
                return true;
            },
        };
    }

    /// <summary>The region's word for before or after noon ("AM", "PM"), read at <paramref name="hour"/>; null where it writes none.</summary>
    private static string? Designator(int hour) =>
        RegionFormats.Time(new DateTime(2000, 1, 1, hour, 0, 0), "tt") is { Length: > 0 } word ? word : null;

    /// <summary>
    /// An icon's row: the cross first — none chosen, drawn with <paramref name="fallback"/>, an app's
    /// own icon's glyph where it is empty (§306) — then the whole gallery (MenuIcons), each with the
    /// words it is found by; set by writing the glyph, or "" for none. Its field filters the tiles
    /// (§249): <see cref="OptionRow.Placeholder"/> is what it says while empty.
    /// </summary>
    internal static OptionRow Icons(string key, string glyph, Action<string> write, string fallback = "")
    {
        // §518: the cross "Default (…)", the gallery's name for what it is drawn with, else its own icon (an app's, a site's).
        var unset = S.SettingsValueDefault(MenuIcons.All.FirstOrDefault(icon => icon.Glyph == fallback)?.Name ?? S.SettingsIconPickerOwnIcon);
        List<OptionTile> tiles =
        [
            new(unset, Glyph: OptionGlyphs.Clear, Plain: true, Words: [unset]),
            .. MenuIcons.All.Select(icon => new OptionTile(icon.Name, Glyph: icon.Glyph, Words: icon.Words)),
        ];
        return new OptionRow(key, OptionRowKind.Glyph, S.SettingsFolderIconHeader)
        {
            Glyph = OptionGlyphs.Icon,
            Tiles = tiles,
            Text = glyph,
            Fallback = fallback.Length > 0 ? fallback : OptionGlyphs.AppIcon,
            Chosen = glyph.Length == 0 ? 0 : tiles.FindIndex(1, tile => tile.Glyph == glyph),
            Set = at => at >= 0 && at < tiles.Count && Changed(glyph, at == 0 ? "" : tiles[at].Glyph, write),
            Field = OptionField.Search,
            Placeholder = S.SettingsIconPickerSearch,
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

/// <summary>
/// How a menu's steppers step a time (OptionPickers): a time of day as a time picker's columns turn,
/// a length as a number does.
/// </summary>
public static class TimeSteps
{
    /// <summary>A step of an hour, of a minute, and across noon (AM and PM), as the steppers send them.</summary>
    public const int Hour = 60;

    public const int Minute = 1;

    public const int Noon = 12 * 60;

    /// <summary>
    /// <paramref name="minutes"/> after midnight stepped <paramref name="by"/>: an hour (±60) or across
    /// noon (720) round the day, so 23:00 and an hour on is 00:00; a minute (±1) round its hour, as a
    /// time picker's minutes column turns, so 07:59 and a minute on is 07:00. Null for any other step.
    /// </summary>
    public static int? Clock(int minutes, int by)
    {
        var day = Wrap(minutes, AlarmTimes.Day);
        return Math.Abs(by) switch
        {
            Hour or Noon => Wrap(day + by, AlarmTimes.Day),
            Minute => (day / 60 * 60) + Wrap((day % 60) + by, 60),
            _ => null,
        };
    }

    /// <summary>
    /// A length of <paramref name="minutes"/> stepped <paramref name="by"/> minutes, kept within
    /// <paramref name="least"/> and <paramref name="most"/>: an hour on from 11 h 30 is the most, 12 h;
    /// null where it is at the end it steps towards already.
    /// </summary>
    public static int? Length(int minutes, int by, int least, int most)
    {
        var next = Math.Clamp(minutes + by, least, most);
        return by == 0 || next == minutes ? null : next;
    }

    /// <summary>Whether a length of <paramref name="minutes"/> can step <paramref name="by"/>.</summary>
    public static bool CanStep(int minutes, int by, int least, int most) => Length(minutes, by, least, most) is not null;

    /// <summary>The hour <paramref name="minutes"/> after midnight shows: "07" on a 24-hour clock, "7" on a 12-hour one (12 for noon and midnight).</summary>
    public static string HourText(int minutes, bool twelveHour)
    {
        var hour = Wrap(minutes, AlarmTimes.Day) / 60;
        return twelveHour
            ? (hour % 12 == 0 ? 12 : hour % 12).ToString(CultureInfo.InvariantCulture)
            : hour.ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>The minute it shows, two digits.</summary>
    public static string MinuteText(int minutes) => (Wrap(minutes, 60)).ToString("00", CultureInfo.InvariantCulture);

    /// <summary>Whether it is after noon.</summary>
    public static bool Afternoon(int minutes) => Wrap(minutes, AlarmTimes.Day) >= Noon;

    private static int Wrap(int value, int around) => ((value % around) + around) % around;
}
