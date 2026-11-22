// menus: the icon of every row of a widget's and a face's settings, and of every choice in them (2026-10-19,
// the person: "add icons to all of the submenus / subitems, like the different styles or so"; docs\
// widgets.md §249). A row names its icon by a Segoe Fluent Icons glyph (OptionRow.Glyph, a choice's
// OptionRow.ChoiceGlyphs), set where the row is made; these are the glyphs, by what they stand for, so
// one thing has one icon in every menu: a name is Rename's, a time zone the world, a place a pin.
//
// Chosen off the font on Windows 11 (a sheet of every glyph it has, 2026-10-19), Windows 11's own where
// Settings has the same row: Personalization's brush for a style, Time & language's world for a zone,
// the moon for Do not disturb, Word's three align glyphs for where the words stand. Several are
// Explorer's commands too and draw as Explorer draws them (ExplorerIcons): Rename, New, Delete,
// Refresh, Personalize; none is one whose Explorer picture means something else (View's is a grid, so
// "shown" is the eye; Properties' a wrench, so "details" is a list). A list of like things a step
// apart (the paces, the ranges) steps through glyphs of a kind — a speed gauge, a calendar's day,
// week and month — rather than one icon said again.

namespace Karpo.Core;

/// <summary>The glyphs the settings' rows and choices are drawn with; see the top of the file.</summary>
public static class OptionGlyphs
{
    // How a widget shows what it shows.
    public const string Style = "\uE771";
    public const string Reading = "\uE9D9";
    public const string Processor = "\uEEA1";
    public const string Graphics = "\uF211";
    public const string Memory = "\uE964";
    public const string Disk = "\uEDA2";
    public const string Network = "\uE968";
    public const string Graph = "\uE9D2";
    public const string Figure = "\uF146";
    public const string Details = "\uE8FD";
    public const string Agenda = "\uE8FD";
    public const string Events = "\uEE93";
    public const string Day = "\uE8BF";
    public const string Week = "\uE8C0";
    public const string Month = "\uE787";
    public const string Year = "\uF0E2";
    public const string Sun = "\uE706";
    public const string Moon = "\uE708";

    /// <summary>sun (\u00A7368): the sun on the horizon, its rays long, the sun widget's and its arc's.</summary>
    public const string Sunrise = "\uED39";
    public const string Cloud = "\uE753";
    public const string Clock = "\uE917";
    public const string Time = "\uE823";
    public const string DateAndTime = "\uEC92";

    /// <summary>\u00A7440: a clock in a frame, the clock's look of the time and the date beside a face.</summary>
    public const string ClockAndTime = "\uF182";
    public const string Volume = "\uE767";
    public const string Speakers = "\uE7F5";
    public const string Microphone = "\uE720";
    public const string Mixer = "\uE9E9";
    public const string Headphones = "\uE7F6";

    // How often, how far, how much.
    public const string Pace = "\uE72C";
    public const string Fast = "\uEC4A";
    public const string Steady = "\uEC49";
    public const string Slow = "\uEC48";
    public const string Seconds = "\uE916";
    public const string History = "\uE81C";
    public const string Ruler = "\uED5E";
    public const string Height = "\uECE7";

    /// <summary>\u00A7533: the room between a bar's widgets (Segoe Fluent Icons' Ruler: a length measured).</summary>
    public const string Spacing = Ruler;

    /// <summary>\u00A7409: a folder line's rows, the stepper under its Height (Segoe Fluent Icons' List, lines one under another).</summary>
    public const string Rows = "\uE14C";

    /// <summary>
    /// \u00A7412: Appearance's theme, a whole set of colours at once (Segoe Fluent Icons' two colours swapped): apart from the
    /// Dark mode toggle's (\u00A7403, which \u00A7411 gave it) and from its own colours' palette, brush and letter.
    /// </summary>
    public const string Theme = "\uEF1F";

    /// <summary>§411: what is asked about, a question's (Segoe Fluent Icons' Help).</summary>
    public const string Help = "\uE897";

    /// <summary>§411: a timer's, what it has left (Segoe Fluent Icons' Timer).</summary>
    public const string Timer = "\uF739";
    public const string Automatic = "\uE895";
    public const string Fit = "\uE9A6";
    public const string Fill = "\uE740";
    public const string Compact = "\uE73F";
    public const string Size = "\uE8E9";
    public const string Smaller = "\uE8E7";
    public const string Larger = "\uE8E8";
    public const string Calculator = "\uE8EF";

    // Where, and whose.
    public const string Zone = "\uE909";
    public const string Globe = "\uE774";
    public const string Region = "\uE775";
    public const string Place = "\uE707";
    public const string Device = "\uE81D";
    public const string Flag = "\uE7C1";
    public const string Pc = "\uE977";
    public const string Windows = "\uE770";
    public const string Monitor = "\uE7F4";
    public const string Monitors = "\uEBC6";
    public const string Keyboard = "\uE765";
    public const string Taskbar = "\uE75B";

    /// <summary>\u00A7585: the taskbar's right-click menu, its expander on the Taskbar page (GlobalNavigationButton: a menu).</summary>
    public const string Menu = "\uE700";

    // Karpo as a whole (\u00A7331): the edit bar's \u2699 menu's gear, its Quit Karpo's power button.
    public const string Settings = "\uE713";
    public const string Power = "\uE7E8";

    // The \u2699 menu's general settings and the taskbar's behaviour (\u00A7337): the Localization and General pages' own.
    public const string Language = "\uF2B7";
    public const string Temperature = "\uE9CA";
    public const string Combine = "\uE8A9";
    public const string Launch = "\uE8A7";

    /// <summary>\u00A7360: the windows of every desktop on the taskbar: Task View, Windows' picture of its desktops.</summary>
    public const string Desktops = "\uE7C4";

    // Accounts (\u00A7322): Settings' Accounts page's, and each kind's.
    public const string Accounts = "\uE910";
    public const string Contact = "\uE77B";
    public const string Mail = "\uE715";
    public const string People = "\uE716";

    /// <summary>\u00A7449: which of the accounts' parts a widget shows: Filter.</summary>
    public const string ShowFrom = "\uE71C";

    /// <summary>Signed in to again: Sync, as Windows' own accounts sync.</summary>
    public const string SignIn = "\uE895";

    // \u00A7338: a server's form: its password, its security, the test of its connection, and what is said
    // about an account's sync.
    public const string Lock = "\uE72E";
    public const string Shield = "\uEA18";
    public const string Connect = "\uE703";
    public const string Warning = "\uE7BA";

    // An edge of the screen (BarLooks): Windows' arrows towards it.
    public const string ArrowUp = "\uE74A";
    public const string ArrowDown = "\uE74B";
    public const string ArrowLeft = "\uE72B";
    public const string ArrowRight = "\uE72A";

    // Words, names and lists.
    public const string Name = "\uE8AC";
    public const string Font = "";

    public const string Label = "\uE8EC";
    public const string Link = "\uE71B";
    public const string Feed = "\uF57E";
    public const string Checklist = "\uE9D5";
    public const string Note = "\uE70B";
    public const string New = "\uE710";
    public const string Delete = "\uE74D";
    public const string Copy = "\uE8C8";
    public const string Reset = "\uE7A7";
    public const string Search = "\uE721";
    public const string Apps = "\uE71D";

    // Showing and hiding, on and off.
    public const string Show = "\uE7B3";
    public const string Hide = "\uED1A";
    public const string Preview = "\uE7B3";
    public const string Picture = "\uEB9F";
    public const string Album = "\uE93C";
    public const string Bell = "\uEA8F";
    public const string Repeat = "\uE8EE";
    public const string More = "\uE70E";
    public const string Video = "\uE714";

    /// <summary>camera (\u00A7322): the video camera, the camera line's own; Streaming, how it is read; Mute, its sound off.</summary>
    public const string Camera = Video;
    public const string Stream = "\uE93E";
    public const string Mute = "\uE74F";
    public const string Next = "\uE72A";

    // Money and markets.
    public const string Trend = "\uEAFC";
    public const string Money = "\uE8C7";
    public const string Exchange = "\uE8AB";
    public const string Bank = "\uE825";

    /// <summary>ReportDocument, a sheet of bars: the nearest Segoe Fluent Icons has to candles.</summary>
    public const string Bars = "\uE9F9";

    // Where the words stand: Word's align glyphs.
    public const string AlignLeft = "\uE8E4";
    public const string AlignCenter = "\uE8E3";
    public const string AlignRight = "\uE8E2";

    // \u00A7589: along a bar on a display's side, Word's three a quarter turn clockwise: the font has no top, middle and bottom
    // of its own (none of its glyphs is one of the three turned), so Left's lines hang from the top, Right's stand on the
    // bottom.
    public static readonly string AlignTop = Turned(AlignLeft);
    public static readonly string AlignMiddle = Turned(AlignCenter);
    public static readonly string AlignBottom = Turned(AlignRight);

    /// <summary>
    /// \u00A7589: what follows a glyph drawn a quarter turn clockwise (<see cref="Turned"/>): a word joiner, which draws as nothing
    /// where a glyph is drawn without asking (and is in no glyph of the font).
    /// </summary>
    public const char QuarterTurn = '\u2060';

    /// <summary>\u00A7589: <paramref name="glyph"/> drawn a quarter turn clockwise about its middle.</summary>
    public static string Turned(string glyph) => glyph + QuarterTurn;

    /// <summary>\u00A7589: whether <paramref name="glyph"/> is drawn a quarter turn (<see cref="Turned"/>), and the glyph it turns.</summary>
    public static bool IsTurned(string glyph, out string upright)
    {
        var turned = glyph.Length > 1 && glyph[^1] == QuarterTurn;
        upright = turned ? glyph[..^1] : glyph;
        return turned;
    }

    public const string Vertical = "\uE8CB";
    public const string Rotate = "\uE7AD";

    // Colours and icons.
    public const string Palette = "\uE790";

    /// <summary>\u00A7419: the styles, looks laid one over another (MapLayers).</summary>
    public const string Styles = "\uE81E";

    /// <summary>\u00A7302: no colour at all (Cancel) and every colour back to the bar's (Undo).</summary>
    public const string None = "\uE711";
    public const string Undo = "\uE7A7";
    public const string Grayscale = "\uE793";
    public const string Inverted = "\uF08C";
    public const string Contrast = "\uE746";
    public const string Icon = "\uED58";
    public const string AppIcon = "\uECAA";

    /// <summary>§306: Cancel's cross, an icon picker's first tile: none chosen, its default drawn.</summary>
    public const string Clear = "\uE711";
    public const string Folder = "\uE8B7";
    public const string FolderOpen = "\uE838";
    public const string Pin = "\uE718";
    public const string Download = "\uE896";
    public const string Document = "\uE8A5";
    public const string Music = "\uE8D6";
    public const string Home = "\uE80F";
    public const string Explorer = "\uEC50";
    public const string Open = "\uE8DA";
    public const string Cascade = "\uE7C4";
    public const string Quake = "\uE945";
    public const string Usb = "\uE88E";
    public const string NetworkDrive = "\uE8CE";
    public const string Sports = "\uE7FC";
    public const string Left = "\uE8A0";
    public const string Right = "\uE89F";
    public const string Tiles = "\uECA5";

    /// <summary>§489: Karpo's animations (Slideshow): things coming in and going.</summary>
    public const string Animations = "\uE786";


    /// <summary>§484: what a line shows of what it has (the sports line's view): Explorer's View, drawn as Explorer draws it.</summary>
    public const string View = "\uE890";


    /// <summary>
    /// §498: a folder's line's views (<see cref="PinFolders.Views"/>, in its order), as File Explorer's View menu draws
    /// them: four large squares, six small ones, a bulleted list, rows with their columns, and tiles.
    /// </summary>
    public static IReadOnlyList<string> FolderViews { get; } = ["", "", "", "", ""];

    /// <summary>A pace of <paramref name="seconds"/> on the gauge: a second or two fast, five or ten steady, slower slow.</summary>
    public static string PaceOf(int seconds) => seconds <= 2 ? Fast : seconds <= 10 ? Steady : Slow;

    /// <summary>A market's range on the calendar: a day, five days a week, a month and up to a year a month, longer the history.</summary>
    public static string RangeOf(MarketRange range) => MarketRanges.Known(range) switch
    {
        MarketRange.Day => Day,
        MarketRange.FiveDays => Week,
        MarketRange.Month or MarketRange.SixMonths or MarketRange.YearToDate or MarketRange.Year => Month,
        _ => History,
    };

    /// <summary>A drive by its kind: a removable one's stick, a network one's share, any other a disk.</summary>
    public static string DriveOf(DriveKind kind) => kind switch
    {
        DriveKind.Removable => Usb,
        DriveKind.Remote => NetworkDrive,
        _ => Disk,
    };

    /// <summary>A spatial sound format by what it is for: headphones, else speakers.</summary>
    public static string SoundFormatOf(string name) => name.Contains("Headphone", StringComparison.OrdinalIgnoreCase) ? Headphones : Speakers;

    /// <summary>A colour filter: the three for colour blindness a palette, grayscale and the inverted ones their own.</summary>
    public static string FilterOf(ColorFilter filter) => filter switch
    {
        ColorFilter.Grayscale => Grayscale,
        ColorFilter.GrayscaleInverted => Contrast,
        ColorFilter.Inverted => Inverted,
        _ => Palette,
    };

    /// <summary>A face's width, compact, standard and wide: drawn in, fitted, spread out.</summary>
    public static IReadOnlyList<string> Widths { get; } = [Compact, Fit, Fill];

    /// <summary>The air quality's index: the region's, then the European and the US one, each a region's flag.</summary>
    public static IReadOnlyList<string> AirIndexes { get; } = [Automatic, Flag, Flag];

    /// <summary>How far the earthquakes are looked for (QuakeChoices.Radii): so far round the place, and the world.</summary>
    public static IReadOnlyList<string> Radii { get; } = [.. QuakeChoices.Radii.Select(km => km == QuakeChoices.World ? Globe : Place)];

    /// <summary>One glyph for each of <paramref name="count"/> choices.</summary>
    public static IReadOnlyList<string> Each(string glyph, int count) => [.. Enumerable.Repeat(glyph, count)];
}
