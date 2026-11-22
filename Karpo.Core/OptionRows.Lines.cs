// menus: every setting a widget has, as rows (2026-10-19, the person: "inline every setting of every
// bar widget and folder widget into its edit menu"; docs\widgets.md §219), and since §228 the only
// place they are set ("the settings for the widgets in the main application should be removed, make
// sure that all the widget settings have been ported over correctly").
//
// A row per card the settings app's widget page had, keyed by the page's own property names, each in
// its part of the menu (OptionSlot, §292), which orders it. What is typed or searched for is a Text or a
// Search row (OptionFields.cs) — an amount owned
// too, a number typed and kept as the page's number box kept it. Lists of this PC's or of the
// accounts' (the drives, the To Do lists, the notebook's) are choices of what the caller listed
// (OptionDevices), and no row where it listed nothing. A line's list or note is made, renamed and
// deleted in its choice's level, beside its choices (OptionRow.Owned, §317); a to-do line's lists are
// this PC's and every account's To Do lists in one level, under a heading for each (§324). The accounts
// themselves are ⚙ Settings', which every widget that shows what they hold links to (OptionAccountRows, §351).

using System.Globalization;

namespace Karpo.Core;

/// <summary>notes: an account's To Do list a to-do line may show, beside the notebook's on this PC.</summary>
/// <param name="Account">The account's address, as the line names it (<see cref="TodoOptions.Account"/>).</param>
/// <param name="List">The list's id; null for the account's default one, before its lists were read.</param>
/// <param name="Default">Whether it is its account's default list ("Tasks"), which To Do neither renames nor deletes.</param>
public sealed record TodoSource(string Account, string? List, string Name, bool Default = false)
{
    /// <summary>§445: the account's name as ⚙ › Accounts lists it (AccountNames), the heading its lists stand under; null for its address.</summary>
    public string? AccountName { get; init; }

    /// <summary>§687: what the account calls its tasks — "Microsoft To Do", "Google Tasks" —, which a list's delete names.</summary>
    public string Service { get; init; } = "Microsoft To Do"; // not-ui: a product's name
}

/// <summary>
/// §324: an account's To Do lists changed where they are kept (Microsoft To Do, through Graph): shown so
/// at once, sent after. Each says whether anything changed.
/// </summary>
public interface ITodoLists
{
    bool Rename(string account, string list, string name);

    bool Delete(string account, string list);
}

public static partial class OptionRows
{
    // A widget's rows by the settings app's widget page's property names (gone since §228); a group's
    // rows by its card's name and the member's.

    public const string DesktopsShowApps = "DesktopsShowApps";
    public const string WeatherUsesDevice = "WeatherUsesDevice";
    public const string WeatherPlace = "WeatherPlace";
    public const string RssAddress = "RssAddress";
    public const string ClockSeconds = "ClockSeconds";
    public const string ClockZone = "ClockZone";
    public const string ClockName = "ClockName";
    public const string Drive = "DriveIndex";
    public const string CountdownMinutes = "CountdownMinutes";
    public const string CountdownName = "CountdownName";
    public const string CountdownDoNotDisturb = "CountdownDoNotDisturb";
    public const string AlarmTime = "AlarmTime";
    public const string AlarmOn = "AlarmOn";

    /// <summary>alarm: a day it repeats on, then the day's number (<see cref="DayOfWeek"/>).</summary>
    public const string AlarmDay = "AlarmDay.";
    public const string AlarmName = "AlarmName";
    public const string HeadingText = "HeadingText";

    /// <summary>list: a toggle a toggles row shows, then the toggle's name (<see cref="Core.Toggle"/>).</summary>
    public const string Toggle = "Toggle.";
    public const string ActionLeft = "ActionLeft";
    public const string ActionRight = "ActionRight";
    /// <summary>notes (§324): a to-do line's list, of this PC's and of every account's To Do lists.</summary>
    public const string TodoList = "TodoListIndex";
    public const string Notebook = "NotebookIndex";

    /// <summary>
    /// notes (§317): in the list's or the note's level, a new one made and shown by the line; then, each
    /// followed by its id (an account's To Do list's by the account's address, "|" and its id), one's
    /// name and its delete.
    /// </summary>
    public const string NotebookNew = "NotebookNew";
    public const string NotebookName = "NotebookName.";
    public const string NotebookDelete = "NotebookDelete.";

    public const string WindowsSearchShowBox = "WindowsSearchShowBox";
    public const string ShowDesktopStyle = "ShowDesktopStyleIndex";

    /// <summary>§435: a folder's divider as the folders' look or growing (<see cref="DividerOptions"/>).</summary>
    public const string DividerStyle = "DividerStyleIndex";

    /// <summary>The status icons: where one shows, then the icon's key (<see cref="StatusIcons.Key"/>).</summary>
    public const string StatusIcon = "StatusIcon.";
    public const string VolumeOrientation = "VolumeOrientationIndex";
    public const string VolumeLength = "VolumeLength";
    public const string VolumeShowSpeaker = "VolumeShowSpeaker";
    public const string ItemSize = "ItemSizeIndex";
    public const string ItemsPreview = "ItemsPreview";
    public const string ItemsPictures = "ItemsPictures";
    public const string ItemsTime = "ItemsTime";
    public const string BrightnessScreens = "BrightnessScreensIndex";
    public const string InputButtonShow = "InputButtonShowIndex";
    public const string SwitchShowLabel = "SwitchShowLabel";
    public const string SpatialFormat = "SpatialFormatIndex";
    public const string ColorFilter = "ColorFilterIndex";
    public const string AwakeLength = "AwakeLengthIndex";
    public const string AirIndex = "AirIndex";
    public const string SportsTeam = "SportsTeam";

    /// <summary>§496: how many days an agenda lists events of (<see cref="CalendarOptions.DaysAhead"/>).</summary>
    public const string CalendarDaysAhead = "CalendarDaysAhead";
    public const string QuakeRadius = "QuakeRadius";
    public const string QuakeMagnitude = "QuakeMagnitude";
    public const string QuakeAlerts = "QuakeAlerts";

    /// <summary>§652: a Windows search source's switch, by its id after this ("WinSearchSource.mail").</summary>
    public const string WinSearchSource = "WinSearchSource.";

    /// <summary>§655: Windows' Searching Windows page, where what it indexes is chosen: under the sources' switches.</summary>
    public const string WinSearchIndex = "WinSearchIndex";

    /// <summary>§655: a Windows search home section's switch, in the home's order, by its id after this ("WinSearchHome.files").</summary>
    public const string WinSearchHome = "WinSearchHome.";

    /// <summary>§655: how many a Windows search home section shows, by its id after this ("WinSearchHomeMost.files").</summary>
    public const string WinSearchHomeMost = "WinSearchHomeMost.";

    public const string CameraUrl = "CameraUrl";
    public const string CameraProtocol = "CameraProtocolIndex";
    public const string CameraPace = "CameraPaceIndex";
    public const string CameraFit = "CameraFitIndex";
    public const string CameraName = "CameraName";
    public const string CameraMute = "CameraMute";

    /// <summary>list: a line's "hide while idle" (<see cref="CellItem.HideIdle"/>, §327).</summary>
    public const string HideIdle = "HideIdle";

    /// <summary>§385: where the open folder stands along its bar (<see cref="FolderOptions.Align"/>).</summary>
    public const string FolderAlign = "FolderAlign";

    /// <summary>§647: how the open folder stands beside its bar, cards or attached to it (<see cref="FolderOptions.Style"/>).</summary>
    public const string FolderStyle = "FolderStyle";

    // §380: a folder's contents' own.
    public const string PinNavigation = "PinNavigation";

    /// <summary>§498: how a folder's line lays out its items (<see cref="PinOptions.View"/>).</summary>
    public const string PinView = "PinViewIndex";

    /// <summary>Every row of <paramref name="item"/> but its style, each in its part of the menu (§292).</summary>
    private static void Lines(WidgetItem item, OptionDevices? devices, List<OptionRow> rows, OptionPlace? place = null)
    {
        var info = item.Info;
        bool Carries(WidgetOptionsKind kind) => info?.Options == kind;
        var entry = item as BarItem;

        // A line's item size first among its looks, as every line of items has it.
        ItemRows(item, rows);
        if (entry?.Folder is { } folder)
        {
            FolderRows(folder, rows, place?.Side == true, place is null || FolderStyles.Offered(place.Layout ?? place.Config.DefaultLayout));
        }
        if (Carries(WidgetOptionsKind.Desktops))
        {
            rows.Add(Toggled(DesktopsShowApps, OptionSlot.Look, OptionGlyphs.Apps, S.SettingsWidgetDesktopsAppsHeader, item.Desktops?.ShowApps ?? true, on => (item.Desktops ??= new DesktopOptions()).ShowApps = on));
        }
        if (Carries(WidgetOptionsKind.Weather))
        {
            // The device's location at the top of the place's own level (§240).
            rows.Add(Toggled(WeatherUsesDevice, OptionSlot.Subject, OptionGlyphs.Device, S.SettingsWidgetWeatherDeviceHeader, item.Weather?.UseDeviceLocation ?? false, on => (item.Weather ??= new WeatherOptions()).UseDeviceLocation = on) with { Within = WeatherPlace });
            rows.Add(OptionFields.OfWeatherPlace(item, devices?.Places));
        }
        if (Carries(WidgetOptionsKind.Market))
        {
            // markets: its search, then the rows what it tracks has (MarketRows, §258).
            rows.AddRange(MarketRows.Of(item.Market ?? MarketOptions.New(), () => item.Market ??= MarketOptions.New(), devices?.Shares, words: devices?.Words));
        }
        if (Carries(WidgetOptionsKind.Rss))
        {
            rows.Add(OptionFields.OfRssAddress(item, devices?.Words));
            // §306: the icon it is drawn with in place of its site's own, among its words.
            rows.Add(OptionPickers.RssIconRow(item.Rss ?? new RssOptions(), () => item.Rss ??= new RssOptions()));
        }
        Performance(item, devices, rows);
        if (Carries(WidgetOptionsKind.Clock))
        {
            // The clock's line alone shows the time, and so has seconds and a label; the date has its zone.
            var clock = info!.ShowsTime == true;
            if (clock)
            {
                // §529: how the time is drawn is the style's: its seconds stand under it.
                rows.Add(Toggled(ClockSeconds, OptionSlot.Look, OptionGlyphs.Seconds, S.SettingsWidgetClockSecondsHeader, item.Clock?.Seconds ?? false, on => (item.Clock ??= new ClockOptions()).Seconds = on) with { Under = Style });
            }
            rows.Add(OptionFields.OfClockZone(item, devices?.Zones));
            if (clock)
            {
                rows.Add(OptionFields.OfClockLabel(item, devices?.Words));
            }
        }
        if (Carries(WidgetOptionsKind.Drive) && devices?.Drives is { } drives)
        {
            rows.Add(DriveRow(item.Info?.Drives ?? DriveFilter.Local, item.Drive, () => item.Drive ??= new DriveTargetOptions(), drives));
        }
        if (Carries(WidgetOptionsKind.Countdown))
        {
            rows.Add(new OptionRow(CountdownMinutes, OptionRowKind.Number, S.SettingsWidgetCountdownLengthHeader)
            {
                Glyph = OptionGlyphs.Seconds,
                Slot = OptionSlot.Subject,
                Value = CountdownLength.Of(item),
                Least = CountdownLength.Fewest,
                Most = CountdownLength.Most,
                // A widget nobody set has no record yet: this is its first.
                Set = value => CountdownLength.Minutes(value) is { } minutes
                    && Changed(CountdownLength.Of(item), minutes, kept => (item.Countdown ??= new CountdownOptions()).Minutes = kept),
            });
            rows.Add(OptionFields.OfCountdownName(item, devices?.Words));
            if (info == WidgetInfos.Focus)
            {
                rows.Add(Toggled(CountdownDoNotDisturb, OptionSlot.Behaviour, OptionGlyphs.Moon, S.SettingsWidgetClockDndHeader, item.Countdown?.DoNotDisturb != false,
                    on => (item.Countdown ??= new CountdownOptions { Minutes = CountdownLength.Of(item) }).DoNotDisturb = CountdownLength.DoNotDisturb(on)));
            }
        }
        if (Carries(WidgetOptionsKind.Alarm))
        {
            AlarmRows(item, devices, rows);
        }
        if (Carries(WidgetOptionsKind.Heading))
        {
            rows.Add(OptionFields.OfHeading(item, devices?.Words));
        }
        if (Carries(WidgetOptionsKind.Toggles))
        {
            TogglesRows(item, rows);
        }
        if (Carries(WidgetOptionsKind.Actions))
        {
            rows.Add(OptionFields.OfAction(item, ActionLeft, S.SettingsWidgetActionsLeftHeader, left: true) with { Glyph = OptionGlyphs.Left });
            rows.Add(OptionFields.OfAction(item, ActionRight, S.SettingsWidgetActionsRightHeader, left: false) with { Glyph = OptionGlyphs.Right });
        }
        if (Carries(WidgetOptionsKind.Todo))
        {
            TodoListRow(item, devices, rows);
        }
        if (Carries(WidgetOptionsKind.Note))
        {
            NoteRow(item, devices, rows);
        }
        if (Carries(WidgetOptionsKind.Start))
        {
            StartRows(item, rows);
        }
        if (Carries(WidgetOptionsKind.Calendar))
        {
            // §496: how many days the agenda lists, today the first; a week is no option at all (CalendarOptions.KeptDaysAhead).
            var days = CalendarOptions.DaysAheadOf(item);
            rows.Add(new OptionRow(CalendarDaysAhead, OptionRowKind.Number, S.SettingsWidgetCalendarDaysAheadHeader)
            {
                Glyph = OptionGlyphs.Week,
                Slot = OptionSlot.Scope,
                // §529: how far the agenda lists is the style's: it stands under it.
                Under = Style,
                Value = days,
                Least = CalendarOptions.DaysAheadFewest,
                Most = CalendarOptions.DaysAheadMost,
                Unit = OptionUnit.Days,
                Set = value => Changed(days, Math.Clamp(value, CalendarOptions.DaysAheadFewest, CalendarOptions.DaysAheadMost),
                    kept => (item.Calendar ??= new CalendarOptions()).DaysAhead = CalendarOptions.KeptDaysAhead(kept)),
            });
        }
        // §455: which of the accounts' parts it shows, over the way to the accounts themselves (§351).
        WidgetSources.Into(item, devices?.Parts, rows);
        // §351, §529: the way to the accounts, of a widget that shows what they hold: what it draws on, under Show from.
        OptionAccountRows.Into(item, devices?.Parts, place?.Config.Holidays, rows);
        if (Carries(WidgetOptionsKind.WindowsSearch) && entry is not null)
        {
            // The icon is no options at all, as the file keeps it (WindowsSearchOptions.Normalized).
            rows.Add(Toggled(WindowsSearchShowBox, OptionSlot.Look, OptionGlyphs.Search, S.SettingsWidgetWindowsSearchBoxHeader, entry.WindowsSearch?.ShowBox == true,
                on => entry.WindowsSearch = on ? new WindowsSearchOptions { ShowBox = true } : null));
        }
        if (Carries(WidgetOptionsKind.ShowDesktop) && entry is not null)
        {
            // §302: its icon, or Windows 11's bare corner; the icon is no options at all (ShowDesktopOptions.Normalized).
            rows.Add(Chose(ShowDesktopStyle, OptionSlot.Style, S.SettingsWidgetStyleHeader, [S.SettingsWidgetShowDesktopIcon, S.SettingsWidgetShowDesktopCorner],
                entry.ShowDesktop?.Style == Core.ShowDesktopStyle.Corner ? 1 : 0,
                at => entry.ShowDesktop = at == 1 ? new ShowDesktopOptions { Style = Core.ShowDesktopStyle.Corner } : null) with
                {
                    Glyph = OptionGlyphs.Style,
                    ChoiceGlyphs = [OptionGlyphs.Pc, OptionGlyphs.Hide],
                });
        }
        if (Carries(WidgetOptionsKind.StatusIcons) && entry is not null)
        {
            // Where each shows, of the tray icons' three (OptionTray, §261); §517 (R4) the group says how many show.
            var said = S.DockEditShownCount(StatusIcons.All.Count(icon => StatusIcons.State(entry.StatusIcons, icon) != TrayState.Hidden));
            foreach (var icon in StatusIcons.All)
            {
                rows.Add(Chose(StatusIcon + StatusIcons.Key(icon), OptionSlot.Subject, StatusIcons.Title(icon), [S.SettingsTrayShowOnTaskbar, S.SettingsStatusIconsShowBehindArrow, S.SettingsTrayHide],
                    OptionTray.IndexOf(StatusIcons.State(entry.StatusIcons, icon)), at => entry.StatusIcons = StatusIcons.With(entry.StatusIcons, icon, OptionTray.States[at])) with
                    {
                        Group = S.SettingsStatusIconsHeader,
                        GroupGlyph = info!.Glyph,
                        GroupSummary = said,
                        Glyph = StatusIcons.Glyph(icon),
                        ChoiceGlyphs = [OptionGlyphs.Taskbar, OptionGlyphs.More, OptionGlyphs.Hide],
                        ChoicesSaid = OptionTray.Said,
                    });
            }
        }
        if (Carries(WidgetOptionsKind.Volume))
        {
            SliderOrientation[] orientations = [SliderOrientation.Auto, SliderOrientation.Horizontal, SliderOrientation.Vertical];
            var orientation = item.Volume?.Orientation;
            rows.Add(Chose(VolumeOrientation, OptionSlot.Placement, S.SettingsWidgetVolumeDirectionHeader,
                [S.SettingsWidgetVolumeDirectionFollow, S.SettingsWidgetVolumeDirectionLying, S.SettingsWidgetVolumeDirectionStanding],
                orientation == SliderOrientation.Horizontal ? 1 : orientation == SliderOrientation.Vertical ? 2 : 0,
                at => (item.Volume ??= new VolumeOptions()).Orientation = orientations[at]) with
                {
                    Glyph = OptionGlyphs.Rotate,
                    // §411 (R3): a direction of three, its icons in its row.
                    Segmented = true,
                    ChoiceGlyphs = [OptionGlyphs.Automatic, OptionGlyphs.Exchange, OptionGlyphs.Vertical],
                });
            var length = item.Volume?.Length ?? 120;
            rows.Add(new OptionRow(VolumeLength, OptionRowKind.Number, S.SettingsWidgetVolumeLengthHeader)
            {
                Glyph = OptionGlyphs.Ruler,
                Slot = OptionSlot.Placement,
                Value = length,
                Least = VolumeOptions.ShortestDip,
                Most = VolumeOptions.LongestDip,
                // The page's slider steps four at a time.
                Step = 4,
                Unit = OptionUnit.Pixels,
                Set = value => Changed(length, Math.Clamp(value, VolumeOptions.ShortestDip, VolumeOptions.LongestDip), kept => (item.Volume ??= new VolumeOptions()).Length = kept),
            });
            rows.Add(Toggled(VolumeShowSpeaker, OptionSlot.Look, OptionGlyphs.Volume, S.SettingsWidgetVolumeSpeakerHeader, item.Volume?.ShowSpeaker ?? true, on => (item.Volume ??= new VolumeOptions()).ShowSpeaker = on));
        }
        if (item is CellItem { IdleKey: { } idle } line)
        {
            // §327: a line that leaves its folder at rest while it has nothing to show; on is no option at all (CellItem.Normalize).
            rows.Add(Toggled(HideIdle, OptionSlot.Behaviour, OptionGlyphs.Hide, Strings.Text(idle), line.HidesIdle, on => line.HideIdle = on ? null : false));
        }
        if (Carries(WidgetOptionsKind.Brightness))
        {
            // Every display is no options at all, as the file keeps it (BrightnessOptions.Normalized).
            rows.Add(Chose(BrightnessScreens, OptionSlot.Behaviour, S.SettingsWidgetBrightnessScreensHeader, [S.SettingsWidgetBrightnessAll, S.SettingsWidgetBrightnessOwn],
                item.Brightness?.Screens == Core.BrightnessScreens.Own ? 1 : 0,
                at => item.Brightness = at == 1 ? new BrightnessOptions { Screens = Core.BrightnessScreens.Own } : null) with
                {
                    Glyph = OptionGlyphs.Sun,
                    ChoiceGlyphs = [OptionGlyphs.Monitors, OptionGlyphs.Monitor],
                });
        }
        if (Carries(WidgetOptionsKind.InputButton) && entry is not null)
        {
            // Automatic is no options at all, as the file keeps it (InputButtonOptions.Normalized).
            // §411 (R7, R14): a noun for its title, its choice short at its end.
            rows.Add(Chose(InputButtonShow, OptionSlot.Behaviour, S.SettingsLayoutsShowHeader, [S.SettingsWidgetInputShowAutomatic, S.SettingsWidgetInputShowAlways],
                entry.InputButton?.Show == Core.InputButtonShow.Always ? 1 : 0,
                at => entry.InputButton = at == 1 ? new InputButtonOptions { Show = Core.InputButtonShow.Always } : null) with
                {
                    ChoicesSaid = [S.CoreCameraProtocolAuto, S.SettingsWidgetInputShowAlways],
                    Glyph = OptionGlyphs.Keyboard,
                    ChoiceGlyphs = [OptionGlyphs.Automatic, OptionGlyphs.Show],
                });
        }
        if (Carries(WidgetOptionsKind.Switch))
        {
            rows.Add(Toggled(SwitchShowLabel, OptionSlot.Look, OptionGlyphs.Label, S.SettingsWidgetSwitchLabelHeader, item.Switch?.ShowLabel ?? false, on => (item.Switch ??= new SwitchOptions()).ShowLabel = on));
        }
        if (info == WidgetInfos.SpatialSound)
        {
            // The one last used, then each format Windows knows by name; a file's format that is none
            // of those shows as the one last used, and is kept until another is chosen.
            var formats = SpatialSoundFormats.Known;
            var chosen = item.Switch?.SpatialFormat;
            rows.Add(Chose(SpatialFormat, OptionSlot.Behaviour, S.SettingsWidgetSpatialFormatHeader, [S.SettingsWidgetSpatialFormatLastUsed, .. formats.Select(format => format.Name)],
                Array.FindIndex(formats, format => string.Equals(format.Id, chosen, StringComparison.OrdinalIgnoreCase)) + 1,
                at => (item.Switch ??= new SwitchOptions()).SpatialFormat = at == 0 ? null : formats[at - 1].Id) with
                {
                    Glyph = info.Glyph,
                    ChoiceGlyphs = [OptionGlyphs.History, .. formats.Select(format => OptionGlyphs.SoundFormatOf(format.Name))],
                });
        }
        if (info == WidgetInfos.ColorFilters)
        {
            var filters = ColorFilterChoices.All;
            rows.Add(Chose(ColorFilter, OptionSlot.Behaviour, S.SettingsWidgetColorFilterHeader, [.. filters.Select(FilterName)],
                Array.IndexOf(filters, ColorFilterChoices.Known(item.Switch?.ColorFilter ?? Core.ColorFilter.Grayscale)),
                at => (item.Switch ??= new SwitchOptions()).ColorFilter = filters[at]) with
                {
                    // §411 (R13): apart from Appearance's palette under it.
                    Glyph = OptionGlyphs.Contrast,
                    ChoiceGlyphs = [.. filters.Select(OptionGlyphs.FilterOf)],
                });
        }
        if (info == WidgetInfos.KeepAwake)
        {
            // keep awake (§269): how long a click turns it on for, until turned off unless another; the
            // screen is always kept on with the PC (§586). The menu at rest turns it on for another length
            // there and then, and changes nothing here (Dock.WidgetMenus.cs).
            var lengths = KeepAwakeLengths.All;
            rows.Add(Chose(AwakeLength, OptionSlot.Behaviour, S.SettingsWidgetAwakeLengthHeader, [.. lengths.Select(KeepAwakeLengths.Name)],
                Array.IndexOf(lengths, KeepAwakeLengths.Known(item.Switch?.AwakeMinutes ?? 0)),
                at => (item.Switch ??= new SwitchOptions()).AwakeMinutes = lengths[at]) with
                {
                    Glyph = OptionGlyphs.Seconds,
                    ChoiceGlyphs = [.. lengths.Select(KeepAwakeLengths.GlyphOf)],
                });
        }
        // §590: the index's line and the day's, both drawn on the index; the pollutants and the pollen read none.
        if (info == WidgetInfos.AirQuality || info == WidgetInfos.AirForecast)
        {
            // The region's where nobody chose (§210), which is no choice written.
            var indexes = AirIndexes.All;
            rows.Add(Chose(AirIndex, OptionSlot.Scope, S.CoreFaceAirQualityIndexTitle, [S.CoreFaceAirQualityIndexAuto, S.CoreFaceAirQualityIndexEuropean, S.CoreFaceAirQualityIndexUs],
                Math.Max(0, indexes.ToList().IndexOf(AirIndexes.Chosen(item.Weather?.AirIndex) ?? AirIndexes.Auto)),
                at => (item.Weather ??= new WeatherOptions()).AirIndex = at == 0 ? null : indexes[at]) with
                {
                    Glyph = OptionGlyphs.Graph,
                    ChoiceGlyphs = OptionGlyphs.AirIndexes,
                });
        }
        if (Carries(WidgetOptionsKind.Sports))
        {
            rows.Add(OptionFields.OfSportsTeam(item));
        }
        if (info == WidgetInfos.Quakes)
        {
            // quakes: how far, how strong, and the alert (§240); a default chosen is written as none.
            var quakes = QuakeChoices.Of(item.Weather);
            rows.Add(Chose(QuakeRadius, OptionSlot.Scope, S.CoreFaceQuakesRadiusTitle, [.. QuakeChoices.Radii.Select(QuakeChoices.RadiusName)],
                QuakeChoices.Radii.ToList().IndexOf(quakes.Radius),
                at => QuakeChoices.Kept(item).Radius = QuakeChoices.Radii[at] == QuakeChoices.DefaultRadius ? null : QuakeChoices.Radii[at]) with
                {
                    // §411 (R13): a length, apart from the place it is around.
                    Glyph = OptionGlyphs.Ruler,
                    ChoiceGlyphs = OptionGlyphs.Radii,
                });
            rows.Add(Chose(QuakeMagnitude, OptionSlot.Scope, S.CoreFaceQuakesMagnitudeTitle, [.. QuakeChoices.Magnitudes.Select(QuakeChoices.MagnitudeName)],
                QuakeChoices.Magnitudes.ToList().IndexOf(quakes.Magnitude),
                at => QuakeChoices.Kept(item).Magnitude = QuakeChoices.Magnitudes[at] == QuakeChoices.DefaultMagnitude ? null : QuakeChoices.Magnitudes[at]) with { Glyph = OptionGlyphs.Quake });
            rows.Add(Toggled(QuakeAlerts, OptionSlot.Behaviour, OptionGlyphs.Bell, S.CoreFaceQuakesAlertsTitle, quakes.Alerts, on => QuakeChoices.Kept(item).Alerts = on ? null : false));
        }
        if (Carries(WidgetOptionsKind.Camera))
        {
            CameraRows(item, devices, rows);
        }
        // §652: Windows search's sources, a switch each in their order, one group "Search in" — its page's one section, its
        // menu's one level (§594). A source at its default is written as none (WinSearchOptions).
        if (Carries(WidgetOptionsKind.WinSearch))
        {
            WinSearchRows(item, devices, rows);
        }
        // A folder's column widths are not a widget's rows (the person, 2026-10-19: "the column width
        // settings should be removed from the taskbar widget menu"; §226): they are set where the
        // columns are, in the open folder's edit mode (its width buttons).
    }

    /// <summary>
    /// §652, §655: Windows search's page, three plain sections. "Search in": a switch per source in the registry's order,
    /// then the way to Windows' Searching Windows page, where what Windows indexes is chosen (the person, 2026-11-09:
    /// "windows indexing settings should not show up on the bottom in the search widget. it should be linked in the
    /// configuration."). "When nothing is typed": the home's sections, those shown in their order, dragged into another,
    /// taken out by their ×, the others added from "Add a section" (the toggles row's ordered list). "How many each
    /// shows": a count per section shown. Everything at its default is written as none (WinSearchOptions).
    /// </summary>
    private static void WinSearchRows(WidgetItem item, OptionDevices? devices, List<OptionRow> rows)
    {
        var search = item.WinSearch;
        bool Write(Func<WinSearchOptions, bool> change)
        {
            var kept = item.WinSearch ?? new WinSearchOptions();
            var changed = change(kept);
            item.WinSearch = kept.Empty ? null : kept;
            return changed;
        }
        foreach (var source in WinSearchSourceInfos.All)
        {
            rows.Add(Toggled(WinSearchSource + source.Id, OptionSlot.Subject, source.Glyph, source.Title, WinSearchSourceInfos.On(search, source.Id),
                on => Write(kept =>
                {
                    kept.Set(source.Id, on);
                    return true;
                })) with
            {
                Group = WinSearchWords.SearchIn,
                GroupGlyph = OptionGlyphs.Search,
            });
        }
        var index = WindowsPages.SearchingWindows;
        rows.Add(new OptionRow(WinSearchIndex, OptionRowKind.Action, index.Title)
        {
            Glyph = OptionGlyphs.Launch,
            Slot = OptionSlot.Subject,
            Group = WinSearchWords.SearchIn,
            GroupGlyph = OptionGlyphs.Search,
            Set = _ =>
            {
                devices?.Open?.Invoke(index.Uri);
                return false;
            },
        });

        // The home's sections: those shown, in their order, then every other, off.
        var shown = WinSearchHomeInfos.Shown(search);
        var order = shown.Select(info => info.Id).ToList();
        bool Order(Func<List<string>, List<string>> change) => Write(kept =>
        {
            var next = change([.. order]);
            if (next.SequenceEqual(order, StringComparer.Ordinal))
            {
                return false;
            }
            kept.SetHome(next);
            return true;
        });
        var words = new OrderedWords(S.CoreWinSearchHomeAdd, S.CoreWinSearchHomeEmpty, S.CoreWinSearchHomeAll);
        foreach (var info in shown.Concat(WinSearchHomeInfos.All.Where(info => !order.Contains(info.Id))))
        {
            var id = info.Id;
            var showing = order.Contains(id);
            rows.Add(new OptionRow(WinSearchHome + id, OptionRowKind.Toggle, info.Title)
            {
                On = showing,
                Ordered = true,
                OrderedWords = words,
                Group = WinSearchWords.HomeShown,
                GroupGlyph = OptionGlyphs.Home,
                Glyph = info.Glyph,
                Text = info.Glyph,
                Slot = OptionSlot.Subject,
                Set = value => Order(list => value == 0 ? [.. list.Where(each => each != id)] : list.Contains(id) ? list : [.. list, id]),
                Move = showing
                    ? to => Order(list =>
                    {
                        list.Remove(id);
                        list.Insert(Math.Clamp(to, 0, list.Count), id);
                        return list;
                    })
                    : null,
            });
        }

        // How many each section shown shows.
        foreach (var info in shown)
        {
            var id = info.Id;
            var count = WinSearchHomeInfos.Count(search, id);
            rows.Add(new OptionRow(WinSearchHomeMost + id, OptionRowKind.Number, info.Title)
            {
                Glyph = info.Glyph,
                Slot = OptionSlot.Subject,
                Group = WinSearchWords.HomeCounts,
                GroupGlyph = OptionGlyphs.Rows,
                Value = count,
                Least = WinSearchHomeInfos.Fewest,
                Most = info.Most,
                Set = value => Math.Clamp(value, WinSearchHomeInfos.Fewest, info.Most) is var held && held != count && Write(kept =>
                {
                    kept.SetHomeMost(id, held);
                    return true;
                }),
            });
        }
    }

    /// <summary>
    /// camera (§331): its address and which way it is read, how the picture meets the line, its name, and —
    /// where they apply to how it is read, or nobody knows yet — a snapshot's pace and a stream's sound.
    /// Read as the line has them; its record made only on the first change.
    /// </summary>
    private static void CameraRows(WidgetItem item, OptionDevices? devices, List<OptionRow> rows)
    {
        var camera = item.Camera ?? new CameraOptions();
        CameraOptions Kept() => item.Camera ??= camera;
        rows.Add(OptionFields.OfCameraUrl(item, devices?.Words));
        Core.CameraProtocol?[] protocols = [null, .. Cameras.Protocols.Select(protocol => (Core.CameraProtocol?)protocol)];
        rows.Add(Chose(CameraProtocol, OptionSlot.Subject, S.CoreCameraProtocolTitle, [.. protocols.Select(Cameras.ProtocolName)],
            Math.Max(0, Array.IndexOf(protocols, camera.Protocol)), at => Kept().Protocol = protocols[at]) with { Glyph = OptionGlyphs.Stream });
        rows.Add(Chose(CameraFit, OptionSlot.Look, S.CoreCameraFitTitle, [S.CoreCameraFitFill, S.CoreCameraFitWhole], camera.Fit == Core.CameraFit.Whole ? 1 : 0,
            at => Kept().Fit = at == 1 ? Core.CameraFit.Whole : null) with
            {
                Glyph = OptionGlyphs.Fit,
                ChoiceGlyphs = [OptionGlyphs.Fill, OptionGlyphs.Fit],
            });
        rows.Add(OptionFields.OfCameraName(item, devices?.Words));
        var chosen = Cameras.Chosen(camera);
        if (chosen is null or Core.CameraProtocol.Snapshot)
        {
            var paces = Cameras.Paces;
            rows.Add(Chose(CameraPace, OptionSlot.Behaviour, S.SettingsOnlineUpdateHeader, [.. paces.Select(Cameras.PaceName)],
                Array.IndexOf(paces, Cameras.PaceOf(camera)), at => Kept().Seconds = paces[at] == Cameras.DefaultSeconds ? null : paces[at]) with
                {
                    Glyph = OptionGlyphs.Pace,
                    ChoiceGlyphs = [.. paces.Select(OptionGlyphs.PaceOf)],
                });
        }
        if (chosen is not { } known || Cameras.Played(known))
        {
            rows.Add(Toggled(CameraMute, OptionSlot.Behaviour, OptionGlyphs.Mute, S.DockWidgetMenuMute, camera.Mute != false, on => Kept().Mute = on ? null : false));
        }
    }

    /// <summary>perf: a reading's adapter or disk, then its pace.</summary>
    private static void Performance(WidgetItem item, OptionDevices? devices, List<OptionRow> rows)
    {
        if (item.Info is not { Options: WidgetOptionsKind.Performance, Metric: { } metric })
        {
            return;
        }
        // Read as the widget has them, and written where it keeps them: a widget the file kept
        // none for gets its own on the first change, never on a look.
        var options = item.Performance ?? new PerformanceOptions();
        PerformanceOptions Held() => item.Performance ??= options;
        if (metric == Metric.Gpu && Device(PerformanceAdapter, S.SettingsWidgetPerformanceAdapterHeader, OptionGlyphs.Graphics, devices?.Adapters, attached =>
            {
                var choices = PerformanceChoices.AdapterChoices(attached, options.GpuAdapter);
                return (choices, PerformanceChoices.ChosenIndex(choices, options.GpuAdapter), at => Changed(options.GpuAdapter, PerformanceChoices.Picked(choices, at), value => Held().GpuAdapter = value));
            }) is { } adapter)
        {
            rows.Add(adapter);
        }
        if (metric == Metric.Disk && Device(PerformanceDisk, S.SettingsWidgetPerformanceDiskHeader, OptionGlyphs.Disk, devices?.Disks, attached =>
            {
                var choices = PerformanceChoices.DiskChoices(attached, options.Disk);
                return (choices, PerformanceChoices.ChosenDiskIndex(choices, options.Disk), at => Changed(options.Disk, PerformanceChoices.PickedDisk(choices, at), value => Held().Disk = value));
            }) is { } disk)
        {
            rows.Add(disk);
        }
        // Its pace, in every style: the details step at it as the graph and the figure do. Whether a
        // graph is drawn is no switch any more but the style (the figure alone, §228; WidgetStyles).
        var paces = PerformanceChoices.Paces;
        rows.Add(new OptionRow(PerformancePace, OptionRowKind.Choice, S.SettingsOnlineUpdateHeader)
        {
            Glyph = OptionGlyphs.Pace,
            Slot = OptionSlot.Behaviour,
            Choices = [.. paces.Select(PerformanceChoices.Pace)],
            ChoiceGlyphs = [.. paces.Select(OptionGlyphs.PaceOf)],
            Chosen = Array.IndexOf(paces, PerformanceChoices.Nearest(options.Seconds)),
            Set = at => at >= 0 && at < paces.Length && Changed(options.Seconds, paces[at], value => Held().Seconds = value),
        });
    }

    /// <summary>
    /// drives: which drive <paramref name="shown"/> is, of <paramref name="attached"/> those <paramref name="filter"/>
    /// lets its subject show (<see cref="WidgetInfo.Drives"/>), and the one it names where that is not attached now, so a
    /// choice made on another PC is seen and kept; picking that one changes nothing. The one drive picker: a drive
    /// line's, a drive face's own (§477); <paramref name="kept"/> makes the record a pick is written to where there is none.
    /// </summary>
    internal static OptionRow DriveRow(DriveFilter filter, DriveTargetOptions? shown, Func<DriveTargetOptions> kept, IReadOnlyList<DriveVolume> attached)
    {
        var (drives, chosen) = DriveChoices(attached, filter, shown);
        return Chose(Drive, OptionSlot.Subject, S.SettingsWidgetDriveHeader,
            [.. drives.Select(drive => drive.Kind == DriveKind.Unknown ? S.SettingsWidgetDriveNotConnectedEntry(drive.Label) : DriveTargets.Title(drive.Label, drive.Letter))],
            chosen,
            at => DriveTargets.PointAt(kept(), drives[at]),
            at => drives[at].Kind != DriveKind.Unknown
                && !(shown is { } target && DriveTargets.Same(target.Volume, drives[at].Volume) && target.Letter == DriveTargets.Letter(drives[at].Letter))) with
            {
                Glyph = OptionGlyphs.Disk,
                ChoiceGlyphs = [.. drives.Select(drive => OptionGlyphs.DriveOf(drive.Kind))],
            };
    }

    /// <summary>
    /// The drives a drive widget pointed at <paramref name="target"/> offers, of <paramref name="attached"/>
    /// those that fit <paramref name="filter"/>, then the one it names where that is not among them
    /// (<see cref="DriveKind.Unknown"/>); and which it names, -1 for none.
    /// </summary>
    public static (List<DriveVolume> Drives, int Chosen) DriveChoices(IReadOnlyList<DriveVolume> attached, DriveFilter filter, DriveTargetOptions? target)
    {
        var drives = attached.Where(drive => DriveTargets.Fits(filter, drive.Kind)).ToList();
        var volume = target?.Volume ?? "";
        var letter = target?.Letter ?? "";
        var chosen = drives.FindIndex(drive => volume.Length > 0
            ? DriveTargets.Same(drive.Volume, volume)
            : letter.Length > 0 && DriveTargets.Letter(drive.Letter) == letter);
        if (chosen < 0 && target is { IsSet: true })
        {
            drives.Add(new DriveVolume(volume, letter, DriveTargets.Title(target.Label, target.Letter), DriveKind.Unknown, 0, 0));
            chosen = drives.Count - 1;
        }
        return (drives, chosen);
    }

    /// <summary>alarm: its time, its switch, a switch per day of the week as Karpo starts it, and its name.</summary>
    private static void AlarmRows(WidgetItem item, OptionDevices? devices, List<OptionRow> rows)
    {
        AlarmOptions Held() => item.Alarm ??= new AlarmOptions();
        // Its time's place: the picker in it is OptionPickers' (Into), on the clock Karpo shows.
        // §411 (R12): the switch that turns it on first, then its time, its days and its name.
        rows.Add(Toggled(AlarmOn, OptionSlot.Subject, OptionGlyphs.Bell, S.SettingsWidgetAlarmOnHeader, item.Alarm?.On == true, on => Held().On = on));
        rows.Add(new OptionRow(AlarmTime, OptionRowKind.Time, S.SettingsWidgetAlarmTimeHeader) { Glyph = OptionGlyphs.Time, Slot = OptionSlot.Subject });
        var mask = AlarmDays.Mask(item.Alarm?.Days);
        foreach (var day in AlarmDays.From(devices?.FirstDay ?? CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek))
        {
            // None left, and it rings once.
            rows.Add(Toggled(AlarmDay + (int)day, OptionSlot.Subject, OptionGlyphs.Day, CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(day), AlarmDays.Has(mask, day), on =>
            {
                var now = AlarmDays.Mask(Held().Days);
                Held().Days = AlarmDays.Of(on ? now | AlarmDays.Bit(day) : now & ~AlarmDays.Bit(day));
            }) with { Group = S.SettingsWidgetAlarmDaysHeader, GroupTitle = S.DockEditRepeatOn, GroupGlyph = OptionGlyphs.Repeat });
        }
        rows.Add(OptionFields.OfAlarmName(item, devices?.Words));
    }

    /// <summary>
    /// notes: the note a line shows, of the notebook's (<paramref name="devices"/>' Notebook, else its
    /// Book's): its own, else the first. One row, its level every note to pick (searched past ten, as every
    /// long list), and — with the Book to write in — made, renamed and deleted there (§317, the person: "the
    /// note selection / creating / removing needs to be way better in the edit menu. it is to complicated";
    /// §228 to §313 had New, Rename and Delete as rows of their own beside it, the last two only for the
    /// one shown): "New note" at its top makes one called "Note", "Note 2"… and shows it at once; each row's
    /// buttons rename it (its name's field) and delete it from every line, asked first. None where no
    /// notebook was read.
    /// </summary>
    private static void NoteRow(WidgetItem item, OptionDevices? devices, List<OptionRow> rows)
    {
        if ((devices?.Notebook ?? devices?.Book?.Current) is not { } notebook)
        {
            return;
        }
        List<(string Id, string Name)> ones = [.. notebook.Notes.Select(one => (one.Id, one.Title))];
        var picked = item.Note?.Note;
        var at = ones.FindIndex(one => one.Id == (picked ?? ""));
        var chosen = ones.Count == 0 ? -1 : Math.Max(0, at);
        void Show(string id) => (item.Note ??= new NoteOptions()).Note = id;
        var row = Chose(Notebook, OptionSlot.Subject, S.SettingsWidgetNoteNoteHeader, [.. ones.Select(one => one.Name)], chosen, pick => Show(ones[pick].Id),
            pick => item.Note is not null && ones[pick].Id != picked) with { Glyph = OptionGlyphs.Note, ChoiceGlyphs = OptionGlyphs.Each(OptionGlyphs.Note, ones.Count) };
        if (devices?.Book is { } book)
        {
            row = row with { Owned = new OptionOwned(NotebookAdd(book, note: true, Show), NotebookThings(book, note: true, ones, null)) };
        }
        rows.Add(row);
    }

    /// <summary>
    /// notes: the list a to-do line shows — one row, its level every list there is, this PC's notebook's
    /// and each account's To Do lists, under a heading for each where there are accounts' (§324, the
    /// person: "To-do sources take two rows, and Microsoft To Do lists can't be created or renamed from the
    /// menu"; §228 to §321 had "Tasks from" and then "List" for the notebook's). A pick names the list and
    /// where it is kept; one not listed — gone, or an account's before its lists were read — is its
    /// account's default, else its first; a notebook's line its own, else the first. "New list" at the top
    /// makes one on this PC and shows it, as a note's level does (§317); each list has its rename (F2) and
    /// its delete, asked first, where it is kept allows them: the notebook's with the Book to write in, an
    /// account's but its default "Tasks" where they are written (<see cref="OptionDevices.TodoLists"/>).
    /// None where nothing was listed.
    /// </summary>
    private static void TodoListRow(WidgetItem item, OptionDevices? devices, List<OptionRow> rows)
    {
        var notebook = devices?.Notebook ?? devices?.Book?.Current;
        var sources = devices?.TodoSources ?? [];
        if (notebook is null && sources.Count == 0)
        {
            return;
        }
        List<(string Id, string Name)> local = [.. (notebook?.Lists ?? []).Select(one => (one.Id, one.Name))];
        var todo = item.Todo;
        int Chosen()
        {
            if (todo is null)
            {
                return -1;
            }
            if (!todo.IsToDo)
            {
                return local.Count == 0 ? -1 : Math.Max(0, local.FindIndex(one => one.Id == (todo.List ?? "")));
            }
            bool Mine(TodoSource source) => string.Equals(source.Account, todo.Account, StringComparison.OrdinalIgnoreCase);
            var all = sources.ToList();
            var at = all.FindIndex(source => Mine(source) && source.List == todo.TaskList);
            if (at < 0)
            {
                // No list named, or one gone: the account's default, else its first.
                at = all.FindIndex(source => Mine(source) && source.Default);
            }
            at = at < 0 ? all.FindIndex(Mine) : at;
            return at < 0 ? -1 : local.Count + at;
        }
        var chosen = Chosen();
        void Local(string id)
        {
            var options = item.Todo ??= new TodoOptions();
            (options.Account, options.TaskList, options.List) = (null, null, id);
        }
        void Pick(int at)
        {
            if (at < local.Count)
            {
                Local(local[at].Id);
                return;
            }
            var source = sources[at - local.Count];
            var options = item.Todo ??= new TodoOptions();
            (options.Account, options.TaskList) = (source.Account, source.List);
        }
        // Headings only where the lists come from more than this PC.
        var headed = sources.Count > 0;
        var book = devices?.Book;
        var things = book is not null
            ? NotebookThings(book, note: false, local, headed ? S.WidgetTodoOnThisPc : null)
            : [.. local.Select(one => new OptionThing(one.Name, OptionGlyphs.Checklist) { Heading = headed ? S.WidgetTodoOnThisPc : null })];
        things.AddRange(sources.Select(source => RemoteThing(source, devices?.TodoLists)));
        var row = Chose(TodoList, OptionSlot.Subject, S.SettingsWidgetTodoListHeader, [.. things.Select(thing => thing.Title)], chosen, Pick, at => item.Todo is not null && at != chosen) with
        {
            Glyph = OptionGlyphs.Checklist,
            ChoiceGlyphs = OptionGlyphs.Each(OptionGlyphs.Checklist, things.Count),
        };
        if (book is not null || devices?.TodoLists is not null)
        {
            row = row with { Owned = new OptionOwned(book is null ? null : NotebookAdd(book, note: false, Local), things) };
        }
        rows.Add(row);
    }

    /// <summary>§317: "New note" or "New list", at the top of the level: one made in <paramref name="book"/> and shown by the line (<paramref name="show"/>).</summary>
    private static OptionRow NotebookAdd(Notebook book, bool note, Action<string> show) =>
        new(NotebookNew, OptionRowKind.Action, note ? S.SettingsWidgetNotebookNewNote : S.SettingsWidgetNotebookNewList)
        {
            Glyph = OptionGlyphs.New,
            Slot = OptionSlot.Subject,
            Set = _ =>
            {
                show(note ? book.AddNote(null, DateTimeOffset.Now) : book.AddList(null));
                return true;
            },
        };

    /// <summary>§317: the notebook's notes or lists (<paramref name="ones"/>) as things of the level, each renamed and deleted in <paramref name="book"/>.</summary>
    private static List<OptionThing> NotebookThings(Notebook book, bool note, IReadOnlyList<(string Id, string Name)> ones, string? heading) =>
        [.. ones.Select(one => new OptionThing(one.Name, note ? OptionGlyphs.Note : OptionGlyphs.Checklist)
        {
            Heading = heading,
            Name = NameRow(NotebookName + one.Id, one.Name, typed => note ? book.RenameNote(one.Id, typed) : book.RenameList(one.Id, typed)),
            Delete = DeleteRow(
                NotebookDelete + one.Id,
                note ? S.SettingsWidgetNotebookDeleteNote : S.SettingsWidgetNotebookDeleteList,
                note ? S.SettingsWidgetNotebookDeleteNoteBody(one.Name) : S.SettingsWidgetNotebookDeleteListBody(one.Name),
                () => note ? book.DeleteNote(one.Id) : book.DeleteList(one.Id)),
        })];

    /// <summary>
    /// §324: an account's To Do list as a thing of the level, under its account's heading — its name (§445),
    /// as ⚙ › Accounts lists it: renamed and deleted in To Do, asked first, where <paramref name="lists"/>
    /// writes them — never its default "Tasks", which To Do keeps, nor one not read yet.
    /// </summary>
    private static OptionThing RemoteThing(TodoSource source, ITodoLists? lists)
    {
        var thing = new OptionThing(source.Name, OptionGlyphs.Checklist) { Heading = source.AccountName ?? source.Account };
        if (lists is null || source.List is not { } id || source.Default)
        {
            return thing;
        }
        var key = source.Account + "|" + id;
        return thing with
        {
            Name = NameRow(NotebookName + key, source.Name, typed => lists.Rename(source.Account, id, typed)),
            Delete = DeleteRow(NotebookDelete + key, S.SettingsWidgetNotebookDeleteList, S.WidgetTodoDeleteRemoteListBody(name: source.Name, service: source.Service), () => lists.Delete(source.Account, id)),
        };
    }

    /// <summary>§317: a thing's rename, its name's field.</summary>
    private static OptionRow NameRow(string key, string name, Func<string, bool> rename) =>
        new(key, OptionRowKind.Text, S.SettingsWidgetNotebookRename)
        {
            Glyph = OptionGlyphs.Name,
            Slot = OptionSlot.Subject,
            Field = OptionField.Text,
            Text = name,
            Placeholder = S.SettingsWidgetNotebookName,
            Longest = Core.Notebook.MostName,
            SetText = rename,
        };

    /// <summary>§317: a thing's delete, asked first in Windows' dialog (<paramref name="title"/>, <paramref name="body"/>).</summary>
    private static OptionRow DeleteRow(string key, string title, string body, Func<bool> delete) =>
        new(key, OptionRowKind.Action, S.SettingsWidgetNotebookDelete)
        {
            Glyph = OptionGlyphs.Delete,
            Slot = OptionSlot.Subject,
            Ask = new DeleteQuestion(title, body, S.SettingsWidgetNotebookDelete, S.DockDeleteCancel, 0),
            Set = _ => delete(),
        };
    /// <summary>
    /// list: the toggles a toggles row shows, in its order — each one on, moved by dragging it among
    /// the others (<see cref="OptionRow.Move"/>) and taken out by its switch — then every other there
    /// is, off, which its switch puts at the row's end, as the page's list and its "Add a toggle" did.
    /// </summary>
    private static void TogglesRows(WidgetItem item, List<OptionRow> rows)
    {
        var shown = item.Toggles?.Shown ?? [];
        bool Write(Func<List<Toggle>, List<Toggle>> change)
        {
            var options = item.Toggles ??= new TogglesOptions();
            var next = change([.. options.Shown]);
            if (next.SequenceEqual(options.Shown))
            {
                return false;
            }
            options.Shown = next;
            return true;
        }
        var on = shown.Select(toggle => Toggles.All.FirstOrDefault(info => info.Toggle == toggle)).OfType<ToggleInfo>();
        var off = Toggles.All.Where(info => !shown.Contains(info.Toggle));
        foreach (var info in on.Concat(off))
        {
            var toggle = info.Toggle;
            var showing = shown.Contains(toggle);
            rows.Add(new OptionRow(Toggle + toggle, OptionRowKind.Toggle, info.Name)
            {
                On = showing,
                Ordered = true,
                Group = S.SettingsWidgetTogglesHeader,
                GroupTitle = S.DockEditTogglesShown,
                Glyph = info.Glyph,
                Slot = OptionSlot.Subject,
                Text = info.Glyph,
                Set = value => Write(list => value == 0 ? [.. list.Where(each => each != toggle)] : list.Contains(toggle) ? list : [.. list, toggle]),
                Move = showing
                    ? to => Write(list =>
                    {
                        list.Remove(toggle);
                        list.Insert(Math.Clamp(to, 0, list.Count), toggle);
                        return list;
                    })
                    : null,
            });
        }
    }

    /// <summary>
    /// list: a line of items' item style (ItemStyles): the size its items are drawn at, where its kind draws
    /// them by one, and the parts its kind's items have that may be shown or left out — down a folder's
    /// list, where the line is its own card.
    /// </summary>
    private static void ItemRows(WidgetItem item, List<OptionRow> rows)
    {
        if (item is not CellItem || ListWidgets.Find(item.Info?.Id) is not { } line || !line.Kind.HasHeight()
            || ItemStyles.KindOf(item) is not { } kind || ItemStyles.Offer(kind) is not { } offer)
        {
            return;
        }
        var look = ItemStyles.Of(item);
        // What is left of the style after its kind's defaults, none where nothing is; a change that
        // changes nothing the line draws is none.
        bool Items(Action<ItemStyleOptions> change)
        {
            var before = ItemStyles.Of(item);
            var options = item.Items ?? new ItemStyleOptions();
            change(options);
            item.Items = options.Normalized(kind);
            return ItemStyles.Of(item) != before;
        }
        Core.ItemSize[] sizes = [Core.ItemSize.Compact, Core.ItemSize.Standard, Core.ItemSize.Large];
        if (offer.Sized)
        {
            rows.Add(new OptionRow(ItemSize, OptionRowKind.Choice, S.SettingsWidgetItemsSizeHeader)
            {
                Glyph = OptionGlyphs.Size,
                Slot = OptionSlot.Look,
                Choices = [S.SettingsWidgetItemsSizeCompact, S.SettingsWidgetItemsSizeStandard, S.SettingsWidgetItemsSizeLarge],
                ChoiceGlyphs = [OptionGlyphs.Smaller, OptionGlyphs.Size, OptionGlyphs.Larger],
                Chosen = look.Size switch
                {
                    Core.ItemSize.Compact => 0,
                    Core.ItemSize.Large => 2,
                    _ => 1,
                },
                Set = at => at >= 0 && at < sizes.Length && Items(options => options.Size = sizes[at]),
            });
        }
        void Part(ItemParts part, string key, string glyph, string title, bool on, Action<ItemStyleOptions, bool> set)
        {
            if (offer.Parts.HasFlag(part))
            {
                rows.Add(new OptionRow(key, OptionRowKind.Toggle, title) { Glyph = glyph, Slot = OptionSlot.Look, On = on, Set = value => Items(options => set(options, value != 0)) });
            }
        }
        Part(ItemParts.Preview, ItemsPreview, OptionGlyphs.Preview, S.SettingsWidgetItemsPreviewHeader, look.Preview, (options, on) => options.Preview = on);
        Part(ItemParts.Pictures, ItemsPictures, OptionGlyphs.Picture, S.SettingsWidgetItemsPicturesHeader, look.Pictures, (options, on) => options.Pictures = on);
        Part(ItemParts.Time, ItemsTime, OptionGlyphs.Time, S.SettingsWidgetItemsTimeHeader, look.Time, (options, on) => options.Time = on);
    }

    /// <summary>
    /// pins (§292, the person: "path and what it opens should be combined ... it should be as easy as
    /// possible to configure everything"): what it opens, one row — §425 its level the kinds, each with
    /// what <paramref name="targets"/> offers of it (OptionFields.OfPinTarget) —; its name, hinting the
    /// target's own while empty, and its icon. §380: a folder's contents (<paramref name="item"/>'s kind)
    /// have their folder, one level of "Browse…" over the path's field, folders alone offered
    /// (OptionFields.OfFolderTarget), and what a click on a subfolder does;
    /// a pin has nothing of theirs. Each in its part of the menu.
    /// </summary>
    private static void Pin(WidgetItem item, PinOptions pin, TargetChoices? targets, List<OptionRow> rows)
    {
        var lists = PinFolders.Lists(item);
        if (lists)
        {
            rows.AddRange(OptionFields.OfFolderTarget(pin, targets));
        }
        else
        {
            rows.Add(OptionFields.OfPinTarget(pin, targets));
        }
        rows.Add(OptionFields.OfPinName(pin, targets));
        rows.Add(OptionPickers.PinIconRow(pin));
        if (!lists)
        {
            return;
        }
        var navigations = PinFolders.Navigations;
        // A line's view, File Explorer's (R3, R13: each its own glyph).
        var views = PinFolders.Views;
        rows.Add(Chose(PinView, OptionSlot.Look, S.SettingsPinViewHeader, [.. views.Select(view => view.Title())],
            Math.Max(0, Array.IndexOf(views, PinFolders.Known(pin.View))), at => pin.View = views[at]) with
            {
                Glyph = OptionGlyphs.View,
                ChoiceGlyphs = OptionGlyphs.FolderViews,
            });
        rows.Add(Chose(PinNavigation, OptionSlot.Behaviour, S.SettingsPinNavigationHeader, [.. navigations.Select(way => way.Title())],
            Array.IndexOf(navigations, PinFolders.Known(pin.Navigation)), at => pin.Navigation = navigations[at]) with
            {
                Glyph = OptionGlyphs.Cascade,
                ChoiceGlyphs = [OptionGlyphs.Explorer, OptionGlyphs.Agenda],
            });
    }

    /// <summary>
    /// §364 (the settings scope audit): a folder's own rows, over its faces in its menu — §385: where it
    /// stands as it opens; §647 how it stands beside its bar, §656 on a taskbar alone (<paramref name="styled"/>: on a dock a
    /// folder is cards). §427: no name, nor whether one stands beside its button: a folder has none.
    /// </summary>
    private static void FolderRows(FolderOptions folder, List<OptionRow> rows, bool side, bool styled)
    {
        // §385: its four buttons in the row (§253). §589: along a bar on a display's side, Top, Center and Bottom, the same
        // values (Left the work area's start, its top there).
        var aligns = FolderAligns.All;
        rows.Add(Chose(FolderAlign, OptionSlot.Placement, S.CoreAlignTitle, [.. aligns.Select(align => FolderAligns.Name(align, side))], Math.Max(0, aligns.ToList().IndexOf(folder.Align)),
            at => folder.Align = aligns[at]) with
            {
                Glyph = side ? OptionGlyphs.AlignTop : OptionGlyphs.AlignLeft,
                ChoiceGlyphs = [.. aligns.Select(align => FolderAligns.Glyph(align, side))],
                Segmented = true,
            });
        // §647: cards apart off the bar, or attached to it on its ground; a dropdown of the two, each its icon. §656 not on a dock.
        if (!styled)
        {
            return;
        }
        var styles = FolderStyles.All;
        rows.Add(Chose(FolderStyle, OptionSlot.Look, S.DockFolderStyleTitle, [.. styles.Select(FolderStyles.Name)], Math.Max(0, styles.ToList().IndexOf(folder.Style)),
            at => folder.Style = styles[at]) with
            {
                Glyph = OptionGlyphs.Styles,
                ChoiceGlyphs = [.. styles.Select(FolderStyles.Glyph)],
            });
    }

    /// <summary>A toggle in <paramref name="slot"/>, on where <paramref name="on"/>, set by <paramref name="write"/> where it changes.</summary>

    private static OptionRow Toggled(string key, OptionSlot slot, string glyph, string title, bool on, Action<bool> write) =>
        new(key, OptionRowKind.Toggle, title) { Glyph = glyph, Slot = slot, On = on, Set = value => Changed(on, value != 0, write) };

    /// <summary>
    /// A choice in <paramref name="slot"/> of <paramref name="choices"/>, <paramref name="chosen"/> in
    /// force, set by <paramref name="write"/> where <paramref name="changes"/> says it changes anything:
    /// by default, where it is another than the one in force.
    /// </summary>
    private static OptionRow Chose(string key, OptionSlot slot, string title, IReadOnlyList<string> choices, int chosen, Action<int> write, Func<int, bool>? changes = null) =>
        new(key, OptionRowKind.Choice, title)
        {
            Slot = slot,
            Choices = choices,
            Chosen = chosen,
            Set = at =>
            {
                if (at < 0 || at >= choices.Count || !(changes?.Invoke(at) ?? at != chosen))
                {
                    return false;
                }
                write(at);
                return true;
            },
        };

    /// <summary>A color filter in Windows' words.</summary>
    public static string FilterName(Core.ColorFilter filter) => filter switch
    {
        Core.ColorFilter.Deuteranopia => S.SettingsWidgetColorFilterDeuteranopia,
        Core.ColorFilter.Protanopia => S.SettingsWidgetColorFilterProtanopia,
        Core.ColorFilter.Tritanopia => S.SettingsWidgetColorFilterTritanopia,
        Core.ColorFilter.GrayscaleInverted => S.SettingsWidgetColorFilterGrayscaleInverted,
        Core.ColorFilter.Inverted => S.SettingsWidgetColorFilterInverted,
        _ => S.SettingsWidgetColorFilterGrayscale,
    };
}
