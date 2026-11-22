// menus: every setting a widget has, as a row of its menu (2026-10-19, the person: "inline every
// setting of every bar widget and folder widget into its edit menu"; Karpo.Core\OptionRows.Lines.cs,
// docs\widgets.md §219), and since §228 the only place they are set ("the settings for the widgets in
// the main application should be removed, make sure that all the widget settings have been ported
// over correctly"). Held here:
//
//  - **Nothing forgotten.** Every option a widget's own records keep, read off the model by
//    reflection, is a row of some widget's menu, or one of a few named with why
//    (EveryOptionOfAWidgetIsARowOfItsMenu). What the settings app's pages had besides — a pin's typed
//    path, the icon gallery's search, the toggles' order, a line's list made, renamed and deleted, the
//    amount owned of a share — is a row too, each held below.
//  - **The same write as Settings.** Each row set through OptionRows leaves the widget as the page's
//    control left it before the pages went: the same file, byte for byte.

using System.Reflection;
using System.Text.Json;
using Karpo.Dock;
using Karpo.Tests.Localization;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed class OptionRowsTests
{
    /// <summary>Every list a row chooses from, listed: two adapters and disks, a local and a network drive, two To Do lists, a notebook.</summary>
    private static readonly OptionDevices Sources = new(
        ["GPU 0: Intel UHD Graphics 770", "GPU 1: NVIDIA GeForce RTX 4060 Ti"],
        ["Disk 0 (C:)", "Disk 1 (E: F:)"])
    {
        Drives =
        [
            new DriveVolume(@"\\?\Volume{1}\", "C:", "Local Disk", DriveKind.Fixed, 100, 50),
            new DriveVolume(@"\\?\Volume{2}\", "D:", "Data", DriveKind.Fixed, 100, 50),
            new DriveVolume(@"\\server\share", "Z:", "share", DriveKind.Remote, 0, 0),
        ],
        TodoSources =
        [
            new TodoSource("me@example.com", "tasks", "Tasks", Default: true),
            new TodoSource("me@example.com", "shop", "Shopping"),
        ],
        Notebook = new NotebookFile
        {
            Lists = [new TodoList { Id = "a", Name = "Home" }, new TodoList { Id = "b", Name = "Work" }],
            Notes = [new Note { Id = "n1", Title = "Ideas" }, new Note { Id = "n2", Title = "Plans" }],
        },
        FirstDay = DayOfWeek.Monday,
        // §347: a tray's live icons, whose places are its own rows.
        TrayIcons = [new TrayMenuIcon("spotify|0", "Spotify", AppHidden: false)],
    };

    private static string Json(WidgetItem item) => item switch
    {
        BarItem bar => JsonSerializer.Serialize(bar, ConfigJson.Default.BarItem),
        CellItem cell => JsonSerializer.Serialize(cell, ConfigJson.Default.CellItem),
        _ => "",
    };

    private static T Copy<T>(T item)
        where T : WidgetItem => item switch
        {
            BarItem bar => (T)(WidgetItem)JsonSerializer.Deserialize(Json(bar), ConfigJson.Default.BarItem)!,
            CellItem cell => (T)(WidgetItem)JsonSerializer.Deserialize(Json(cell), ConfigJson.Default.CellItem)!,
            _ => item,
        };

    /// <summary>A new widget of every kind there is: each taskbar entry, each folder line.</summary>
    private static IEnumerable<WidgetItem> EveryKind() =>
        Enum.GetValues<BarKind>().Select(kind => (WidgetItem)kind.New())
            .Concat(Enum.GetValues<ListKind>().Select(kind => kind.New()))
            .Where(item => item.Info is not null);

    /// <summary>A new widget whose kind <paramref name="matches"/>: a folder line's, or with <paramref name="bar"/> a taskbar entry's.</summary>
    private static WidgetItem New(Func<WidgetInfo, bool> matches, bool bar = false) =>
        (bar ? Enum.GetValues<BarKind>().Select(kind => (WidgetItem)kind.New()) : Enum.GetValues<ListKind>().Select(kind => kind.New())).Concat(EveryKind())
            .First(item => item.Info is { } info && matches(info));

    /// <summary>
    /// Every row any widget has, a markets widget's of every type and its years, and a to-do line's
    /// lists too, each with the lists listed; and every row of every face, alone and among others, and
    /// a folder's own.
    /// </summary>
    private static List<OptionRow> AllRows()
    {
        List<OptionRow> rows = [];
        foreach (var item in EveryKind())
        {
            rows.AddRange(OptionRows.Of(item, Sources));
            if (item.Market is { } market)
            {
                market.Range = MarketRange.Years;
                foreach (var sample in Markets.MarketRowsTests.Samples)
                {
                    (market.Symbol, market.Style) = (sample.Symbol, sample.Style);
                    rows.AddRange(OptionRows.Of(item, Sources));
                }
            }
        }
        foreach (var face in FolderFaces.All)
        {
            foreach (var faces in new[] { new[] { face.Id }, [face.Id, FolderFaces.WifiId] })
            {
                var folder = BarKind.FolderList.New();
                folder.Folder!.Faces = [.. faces.Select(FolderFace.Of)];
                rows.AddRange(OptionRows.OfFace(folder.Folder, 0, Sources));
                if (folder.Folder.Faces[0].Market is { } market)
                {
                    market.Range = MarketRange.Years;
                }
                rows.AddRange(OptionRows.OfFace(folder.Folder, 0, Sources));
            }
        }
        return rows;
    }

    /// <summary>
    /// No widget has an alignment (2026-10-29: "the alignment settings for the widgets (e.g. the clock)
    /// should be removed"): a line's words stand where the kit's anatomy puts them. No row of any
    /// widget's or face's menu is one; only a folder's own (§385) is.
    /// </summary>
    [Fact]
    public void NoWidgetHasAnAlignment()
    {
        Assert.DoesNotContain(AllRows(), row => row.Key != OptionRows.FolderAlign && (row.Title == S.CoreAlignTitle || row.Key.Contains("Align", StringComparison.Ordinal)));
    }

    // --- Nothing forgotten -------------------------------------------------------------------------

    /// <summary>
    /// What each option of a widget's own options records is set with: the key of the row of its menu
    /// that sets it ("Type.Property" → row key; a key ending in "." is a group's, its members keyed
    /// after it). A property added to a record fails <see cref="EveryOptionOfAWidgetIsARowOfItsMenu"/>
    /// until it is a row here, or in <see cref="NoRow"/> with why.
    /// </summary>
    private static readonly Dictionary<string, string> RowOf = new(StringComparer.Ordinal)
    {
        ["PinOptions.Target"] = OptionPickers.PinTarget,
        ["PinOptions.Name"] = OptionFields.PinName,
        ["PinOptions.Icon"] = OptionPickers.PinIcon,
        ["PinOptions.Navigation"] = OptionRows.PinNavigation,
        ["PinOptions.View"] = OptionRows.PinView,
        ["DriveTargetOptions.Volume"] = OptionRows.Drive,
        ["DriveTargetOptions.Letter"] = OptionRows.Drive,
        ["DriveTargetOptions.Label"] = OptionRows.Drive,
        ["WeatherOptions.UseDeviceLocation"] = OptionRows.WeatherUsesDevice,
        ["WeatherOptions.Place"] = OptionRows.WeatherPlace,
        ["WeatherOptions.Latitude"] = OptionRows.WeatherPlace,
        ["WeatherOptions.Longitude"] = OptionRows.WeatherPlace,
        ["WeatherOptions.AirIndex"] = OptionRows.AirIndex,
        // quakes: the record's radius, magnitude and alert are a row each (QuakeRadius, QuakeMagnitude, QuakeAlerts; §240).
        ["WeatherOptions.Quakes"] = OptionRows.QuakeRadius,
        // markets: what it tracks, then the rows that mean something for that (§246, §258).
        ["MarketOptions.Symbol"] = MarketRows.Symbol,
        ["MarketOptions.Label"] = MarketRows.Label,
        ["MarketOptions.Amount"] = MarketRows.Amount,
        ["MarketOptions.Range"] = MarketRows.Range,
        ["MarketOptions.RangeYears"] = MarketRows.Years,
        ["MarketOptions.Holding"] = MarketRows.Holding,
        ["MarketOptions.Currency"] = MarketRows.Currency,
        ["MarketOptions.Style"] = MarketRows.Style,
        ["MarketOptions.Chart"] = MarketRows.Chart,
        ["PerformanceOptions.GpuAdapter"] = OptionRows.PerformanceAdapter,
        ["PerformanceOptions.Disk"] = OptionRows.PerformanceDisk,
        ["PerformanceOptions.ShowGraph"] = OptionRows.Style,
        ["ClockOptions.Style"] = OptionRows.Style, // §443: the clock's look, an entry of its Style row
        ["PerformanceOptions.Seconds"] = OptionRows.PerformancePace,
        ["ClockOptions.Seconds"] = OptionRows.ClockSeconds,
        ["ClockOptions.TimeZone"] = OptionRows.ClockZone,
        ["ClockOptions.Label"] = OptionRows.ClockName,
        ["DesktopOptions.ShowApps"] = OptionRows.DesktopsShowApps,
        ["VolumeOptions.Orientation"] = OptionRows.VolumeOrientation,
        ["VolumeOptions.Length"] = OptionRows.VolumeLength,
        ["VolumeOptions.ShowSpeaker"] = OptionRows.VolumeShowSpeaker,
        ["SwitchOptions.ShowLabel"] = OptionRows.SwitchShowLabel,
        ["SwitchOptions.SpatialFormat"] = OptionRows.SpatialFormat,
        ["SwitchOptions.ColorFilter"] = OptionRows.ColorFilter,
        ["SwitchOptions.AwakeMinutes"] = OptionRows.AwakeLength,
        ["CountdownOptions.Minutes"] = OptionRows.CountdownMinutes,
        ["CountdownOptions.Name"] = OptionRows.CountdownName,
        ["CountdownOptions.DoNotDisturb"] = OptionRows.CountdownDoNotDisturb,
        ["HeadingOptions.Text"] = OptionRows.HeadingText,
        ["BrightnessOptions.Screens"] = OptionRows.BrightnessScreens,
        ["RssOptions.Address"] = OptionRows.RssAddress,
        ["RssOptions.Glyph"] = OptionPickers.RssIcon,
        ["TogglesOptions.Shown"] = OptionRows.Toggle,
        ["ActionsOptions.Left"] = OptionRows.ActionLeft,
        ["ActionsOptions.Right"] = OptionRows.ActionRight,
        ["TodoOptions.Account"] = OptionRows.TodoList,
        ["TodoOptions.TaskList"] = OptionRows.TodoList,
        ["TodoOptions.List"] = OptionRows.TodoList,
        ["NoteOptions.Note"] = OptionRows.Notebook,
        ["AlarmOptions.Time"] = OptionRows.AlarmTime,
        ["AlarmOptions.On"] = OptionRows.AlarmOn,
        ["AlarmOptions.Days"] = OptionRows.AlarmDay,
        ["AlarmOptions.Name"] = OptionRows.AlarmName,
        ["ItemStyleOptions.Size"] = OptionRows.ItemSize,
        ["ItemStyleOptions.Preview"] = OptionRows.ItemsPreview,
        ["ItemStyleOptions.Pictures"] = OptionRows.ItemsPictures,
        ["ItemStyleOptions.Time"] = OptionRows.ItemsTime,
        ["InputButtonOptions.Show"] = OptionRows.InputButtonShow,
        ["WindowsSearchOptions.ShowBox"] = OptionRows.WindowsSearchShowBox,
        ["ShowDesktopOptions.Style"] = OptionRows.ShowDesktopStyle,
        // §435: a folder's divider's, the folders' look or growing.
        ["DividerOptions.Style"] = OptionRows.DividerStyle,
        ["StatusIconOptions.States"] = OptionRows.StatusIcon,
        // §347: a tray's own icons' places, a row per live icon.
        ["TrayIconOptions.States"] = OptionTray.Icon,
        // §385: where it stands along its bar.
        ["FolderOptions.Align"] = OptionRows.FolderAlign,
        // §647: how it stands beside its bar.
        ["FolderOptions.Style"] = OptionRows.FolderStyle,
        // sports: the team, found by its search; its league, id, name and abbreviation are the team picked (§239).
        ["SportsOptions.Sport"] = OptionRows.SportsTeam,
        ["SportsOptions.League"] = OptionRows.SportsTeam,
        ["SportsOptions.Team"] = OptionRows.SportsTeam,
        ["SportsOptions.Name"] = OptionRows.SportsTeam,
        ["SportsOptions.Abbreviation"] = OptionRows.SportsTeam,
        // camera: its address, how it is read, a snapshot's pace, how it fits, its name and its sound (§331).
        ["CameraOptions.Url"] = OptionRows.CameraUrl,
        ["CameraOptions.Protocol"] = OptionRows.CameraProtocol,
        ["CameraOptions.Seconds"] = OptionRows.CameraPace,
        ["CameraOptions.Fit"] = OptionRows.CameraFit,
        ["CameraOptions.Name"] = OptionRows.CameraName,
        ["CameraOptions.Mute"] = OptionRows.CameraMute,
        // §652: Windows search's sources, a switch each under "Search in".
        ["WinSearchOptions.Sources"] = OptionRows.WinSearchSource,
        // §655: its home's sections, an ordered list under "When nothing is typed", and a count each under "How many each shows".
        ["WinSearchOptions.Home"] = OptionRows.WinSearchHome,
        ["WinSearchOptions.HomeMost"] = OptionRows.WinSearchHomeMost,
        // §496: how many days the agenda lists.
        ["CalendarOptions.DaysAhead"] = OptionRows.CalendarDaysAhead,
        // §641: All apps' view, what Recommended lists, a folder beside power each.
        ["StartOptions.View"] = OptionRows.StartViewRow,
        ["StartOptions.Files"] = OptionRows.StartFiles,
        ["StartOptions.NewApps"] = OptionRows.StartNewApps,
        ["StartOptions.Places"] = OptionRows.StartPlacePrefix,
    };

    /// <summary>
    /// The options a widget keeps that no row of its menu sets, each with why: set elsewhere on the
    /// taskbar, written by what it does, or kept and set by nothing any more.
    /// </summary>
    private static readonly Dictionary<string, string> NoRow = new(StringComparer.Ordinal)
    {
        // A folder's faces are its menu's face section (OptionMenu.FaceSection: added, moved, taken
        // off, each face's own rows); its lists and their widths the open folder's edit mode.
        ["FolderOptions.FirstFace"] = "the folder's face rows",
        ["FolderOptions.AllFaces"] = "the folder's face rows",
        ["FolderOptions.Lists"] = "the open folder's edit mode",
        ["FolderOptions.Widths"] = "the open folder's width buttons",
        // calendar: the retired iCalendar address an old widget may carry and how often it was read,
        // its badge's next event; clock: the view a date once opened in. Kept so a file round-trips.
        ["CalendarOptions.FeedUrl"] = "retired",
        ["CalendarOptions.RefreshMinutes"] = "retired",
        ["CalendarOptions.ShowNextEvent"] = "retired",
        ["ClockOptions.View"] = "retired",
        // rss: what the feed called itself, written when its address is proved.
        ["RssOptions.Title"] = "written with the address",
        // markets: what the price service says the share is, written when its symbol is picked.
        ["MarketOptions.Detail"] = "written with the symbol",
        // §347: a tray's order is what a drag along it writes, as it always was.
        ["TrayIconOptions.Order"] = "dragged into order along the tray",
        // §641: the pinned apps are pinned, unpinned and dragged where they stand, as Start's.
        ["StartOptions.Pins"] = "pinned, unpinned and dragged into order where they stand",
    };

    /// <summary>
    /// The guard (§228: "the settings for the widgets in the main application should be removed, make
    /// sure that all the widget settings have been ported over"): every option a widget's own records
    /// keep — read off the model itself, every options record a taskbar entry or a folder's line may
    /// carry, every property of it the file keeps — is a row of some widget's menu, or one of the few
    /// <see cref="NoRow"/> names with why. A setting added to the model without a way to set it fails here.
    /// </summary>
    [Fact]
    public void EveryOptionOfAWidgetIsARowOfItsMenu()
    {
        var keys = AllRows().Concat(PinRows()).Select(row => row.Key).ToHashSet(StringComparer.Ordinal);
        var records = new[] { typeof(BarItem), typeof(CellItem) }
            .SelectMany(item => item.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Select(property => Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType)
            .Where(type => type.Name.EndsWith("Options", StringComparison.Ordinal) && type.Namespace == "Karpo.Core")
            .Distinct()
            .ToList();
        Assert.True(records.Count >= 25, string.Join(", ", records.Select(type => type.Name)));

        List<string> kept = [];
        foreach (var record in records)
        {
            foreach (var property in record.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                // What the file keeps: a public setter or one the file is let in by, and not left out of the file.
                if ((property.SetMethod is not { IsPublic: true } && property.GetCustomAttribute<System.Text.Json.Serialization.JsonIncludeAttribute>() is null)
                    || property.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>() is { Condition: System.Text.Json.Serialization.JsonIgnoreCondition.Always })
                {
                    continue;
                }
                kept.Add($"{record.Name}.{property.Name}");
            }
        }
        List<string> missing = [];
        foreach (var name in kept.Where(name => !NoRow.ContainsKey(name)))
        {
            if (!RowOf.TryGetValue(name, out var key))
            {
                missing.Add($"{name}: no row");
            }
            else if (!keys.Contains(key) && !keys.Any(each => key.EndsWith('.') && each.StartsWith(key, StringComparison.Ordinal)))
            {
                missing.Add($"{name}: no row {key} in any menu");
            }
        }
        Assert.True(missing.Count == 0, string.Join("; ", missing));
        // Nothing listed that the model no longer keeps.
        Assert.Empty(RowOf.Keys.Concat(NoRow.Keys).Where(name => !kept.Contains(name)));

        // A folder's faces: every option a face has, and what it has beside them, is a row of its
        // submenu while it shows (FaceRows.Rows, Shows).
        foreach (var face in FolderFaces.All)
        {
            var folder = BarKind.FolderList.New();
            folder.Folder!.Faces = [FolderFace.Of(face.Id)];
            var shown = OptionRows.OfFace(folder.Folder, 0, Sources).Select(row => row.Key).ToHashSet(StringComparer.Ordinal);
            foreach (var row in FaceRows.Rows(face).Where(row => FaceRows.Shows(row, folder.Folder.Faces[0])))
            {
                var kind = row.Kind.ToString();
                // The markets face's own record is its type and that type's rows (§246).
                Assert.Contains(row.Kind switch
                {
                    FaceRowKind.Option => row.Option!.Key,
                    FaceRowKind.Market => MarketRows.Symbol,
                    _ => char.ToLowerInvariant(kind[0]) + kind[1..],
                }, shown);
            }
        }
    }

    /// <summary>A folder's contents line, showing <c>C:\Work</c>, which is there; or a pin to it on the taskbar.</summary>
    private static (WidgetItem Folder, OptionPlace Place) FolderOf(bool pin = false)
    {
        WidgetItem folder = pin ? BarKind.Pin.New() : ListKind.FolderContents.New();
        folder.Pin!.Target = @"C:\Work";
        return (folder, new OptionPlace(new Config(), Dark: true, Exists: path => path == @"C:\Work"));
    }

    /// <summary>Every row a folder's contents have.</summary>
    private static IReadOnlyList<OptionRow> PinRows()
    {
        var (folder, place) = FolderOf();
        return OptionRows.Of(folder, Sources, place);
    }

    /// <summary>
    /// §380: a pin's rows are what it opens, its name and its icon — to a folder too, which it only opens;
    /// §425: what it opens a level of its kinds, each with its own. A folder's contents have their folder, its
    /// "Browse…", §498 their view, the name, the icon and what a click on a subfolder does, in the
    /// menus' one order (§292). Each writes what the page's control wrote.
    /// </summary>
    [Fact]
    public void APinsRowsAreItsPagesAndWriteWhatItWrites()
    {
        var (pin, place) = FolderOf(pin: true);
        Assert.Equal([OptionPickers.PinTarget, OptionFields.PinName, OptionPickers.PinIcon],
            OptionRows.Of(pin, Sources, place).Select(row => row.Key));
        Assert.Equal([OptionPickers.PinApp, OptionPickers.PinFolderBrowse, OptionPickers.PinFolder, OptionPickers.PinFileBrowse, OptionPickers.PinFile, OptionPickers.PinSite, OptionPickers.PinSetting],
            OptionRows.Of(pin, Sources, place)[0].Level.Select(row => row.Key));
        var (folder, at) = FolderOf();
        Assert.Equal([OptionPickers.PinTarget, OptionPickers.PinBrowse, OptionRows.PinView, OptionFields.PinName, OptionPickers.PinIcon, OptionRows.PinNavigation],
            OptionRows.Of(folder, Sources, at).Select(row => row.Key));

        void Same(string key, int value, Action<PinOptions> page)
        {
            var (menu, there) = FolderOf();
            var paged = Copy(menu);
            Assert.True(OptionRows.Of(menu, Sources, there).Single(row => row.Key == key).Set!(value));
            page(paged.Pin!);
            Assert.Equal(Json(paged), Json(menu));
        }
        Same(OptionRows.PinNavigation, 1, pin => pin.Navigation = PinFolders.Navigations[1]);
        Same(OptionRows.PinView, 3, pin => pin.View = PinFolders.Views[3]);
    }

    /// <summary>
    /// What a pin opens, typed (§292; §228's Path box, which took a web address or a shell link as well as
    /// what Windows' open dialog picks), §425 in the field of its kind: Enter keeps a bare domain as a web
    /// address, anything with a scheme, a drive, a UNC path or a variable as written; the same again
    /// changes nothing. "Browse…" writes the same.
    /// </summary>
    [Fact]
    public void APinsPathIsTypedAsItsPagesBoxTookIt()
    {
        var pin = BarKind.Pin.New();
        OptionRow Row(string key) => OptionRows.Find(OptionRows.Of(pin), key)!;
        OptionRow Target() => Row(OptionPickers.PinFile);
        bool Enter(string typed) => Target().Finder!.Typed(typed) is { Found: { } found } && Target().Pick!(found);
        bool Site(string typed) => Row(OptionPickers.PinSite).Finder!.Typed(typed) is { Found: { } found } && Row(OptionPickers.PinSite).Pick!(found);
        var target = Row(OptionPickers.PinTarget);
        Assert.Equal((OptionRowKind.Level, S.SettingsPinTargetHeader, null, S.SettingsPinTargetNone), (target.Kind, target.Title, target.Summary, target.Empty));

        Assert.True(Site("  example.com/news "));
        Assert.Equal("https://example.com/news", pin.Pin!.Target);
        Assert.Equal(("https://example.com/news", "example.com"), (Row(OptionPickers.PinSite).Text, Row(OptionPickers.PinTarget).Summary));
        Assert.False(Site("https://example.com/news"));
        Assert.True(Enter(@"C:\Windows\notepad.exe"));
        Assert.Equal((@"C:\Windows\notepad.exe", "notepad.exe"), (pin.Pin.Target, Row(OptionPickers.PinTarget).Summary));
        Assert.True(Enter(@"\\server\share"));
        Assert.Equal(@"\\server\share", pin.Pin.Target);
        Assert.True(Enter("shell:AppsFolder\\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"));
        Assert.True(Enter(@"%USERPROFILE%\Music"));
        Assert.Equal(@"%USERPROFILE%\Music", pin.Pin.Target);
        Assert.Null(Target().Finder!.Typed("  "));

        var browse = Row(OptionPickers.PinFileBrowse);
        Assert.Equal((OptionRowKind.Target, OptionPickers.PinFile, @"%USERPROFILE%\Music"), (browse.Kind, browse.Within, browse.Text));
        Assert.True(browse.SetText!(@"D:\Tools"));
        Assert.Equal(@"D:\Tools", pin.Pin.Target);
    }

    /// <summary>
    /// A folder's or a pin's icon: the whole gallery as tiles (§249; §228 to §245 had 48 and a search
    /// for the rest), each glyph once, each found by its words — those of every gallery id of its
    /// glyph — and the one in force ringed.
    /// </summary>
    [Fact]
    public void AnIconIsFoundInTheWholeGallery()
    {
        var pin = BarKind.Pin.New();
        OptionRow Icon() => OptionRows.Of(pin).Single(row => row.Key == OptionPickers.PinIcon);
        Assert.Equal((OptionField.Search, S.SettingsIconPickerSearch), (Icon().Field, Icon().Placeholder));
        Assert.Null(Icon().Finder);
        var tiles = Icon().Tiles;
        Assert.Equal(IconSearch.All.Select(icon => icon.Glyph).Distinct().Order(), tiles.Skip(1).Select(tile => tile.Glyph).Order());
        var print = tiles.Single(tile => tile.Glyph == IconSearch.All.Single(icon => icon.Id == "fingerprint").Glyph);
        Assert.True(IconSearch.Matches(print.Words!, "finger"));
        Assert.False(IconSearch.Matches(print.Words!, "zzzqqq"));
        // A glyph the gallery names twice is found by both its names.
        var console = tiles.Single(tile => tile.Glyph == IconSearch.All.Single(icon => icon.Id == "xbox").Glyph);
        Assert.True(IconSearch.Matches(console.Words!, "xbox"));
        Assert.True(Icon().Set!(tiles.ToList().IndexOf(print)));
        Assert.Equal(print.Glyph, pin.Pin!.Icon);
        Assert.Equal(tiles.ToList().IndexOf(print), Icon().Chosen);
        Assert.False(Icon().Set!(tiles.ToList().IndexOf(print)));

        var folder = BarKind.FolderList.New();
        folder.Folder!.Faces = [FolderFace.Of(FolderFaces.IconId)];
        var row = OptionRows.OfFace(folder.Folder, 0).Single(row => row.Key == OptionPickers.FolderIcon);
        Assert.True(row.Set!(row.Tiles.ToList().FindIndex(tile => tile.Glyph == print.Glyph)));
        Assert.Equal(print.Glyph, folder.Folder.Face.Glyph);
    }

    /// <summary>
    /// §306: every icon that can be chosen in place of a default — a folder's, a pin's, a feed's — is
    /// one picker: the cross first, ticked while none is chosen, then the gallery with no groups; a
    /// pick sets it and the cross takes it away again, the default (its row's end) drawn then.
    /// </summary>
    [Fact]
    public void EveryIconPickerClearsByItsCross()
    {
        var pin = BarKind.Pin.New();
        var feed = ListKind.Rss.New();
        var folder = BarKind.FolderList.New();
        folder.Folder!.Faces = [FolderFace.Of(FolderFaces.IconId)];
        // §518: the cross "Default (…)", named for what it is drawn with: the gallery's name, else its own icon (an app's, a site's).
        var folderName = MenuIcons.All.Single(icon => icon.Glyph == FolderOptions.FolderGlyph).Name;
        (Func<OptionRow> Row, Func<string?> Glyph, string Fallback, string Unset)[] pickers =
        [
            (() => OptionRows.Of(pin).Single(row => row.Key == OptionPickers.PinIcon), () => pin.Pin?.Icon, OptionGlyphs.AppIcon, S.SettingsIconPickerOwnIcon),
            (() => OptionRows.Of(feed).Single(row => row.Key == OptionPickers.RssIcon), () => feed.Rss?.Glyph, OptionGlyphs.Feed, S.SettingsIconPickerOwnIcon),
            (() => OptionRows.OfFace(folder.Folder, 0).Single(row => row.Key == OptionPickers.FolderIcon), () => folder.Folder.Face.Glyph, FolderOptions.FolderGlyph, folderName),
        ];
        Assert.Equal("Folder", folderName);
        foreach (var (row, glyph, fallback, unset) in pickers)
        {
            var tiles = row().Tiles;
            Assert.Equal((S.SettingsValueDefault(unset), OptionGlyphs.Clear, true), (tiles[0].Name, tiles[0].Glyph, tiles[0].Plain));
            Assert.All(tiles, tile => Assert.Equal("", tile.Group));
            Assert.Equal(MenuIcons.All.Select(icon => icon.Glyph), tiles.Skip(1).Select(tile => tile.Glyph));
            Assert.Equal((0, fallback), (row().Chosen, row().Fallback));
            var star = tiles.ToList().FindIndex(tile => tile.Glyph == "");
            Assert.True(row().Set!(star));
            Assert.Equal(("", star), (glyph(), row().Chosen));
            Assert.True(row().Set!(0));
            Assert.True(string.IsNullOrEmpty(glyph()));
            Assert.Equal(0, row().Chosen);
            Assert.False(row().Set!(0));
        }
        // A feed's is among its words, after what it is of (§292).
        Assert.Equal(OptionSlot.Words, OptionRows.Of(feed).Single(row => row.Key == OptionPickers.RssIcon).Slot);
    }

    /// <summary>
    /// A toggles row's toggles: those it shows first, in its order, each moved by its grip to a place
    /// among them and taken out by its switch; then the rest, put at the row's end by theirs — the
    /// page's list, its drag and its "Add a toggle" (§228).
    /// </summary>
    [Fact]
    public void AToggleRowsTogglesAreOrderedAddedAndTakenOut()
    {
        var toggles = New(info => info.Options == WidgetOptionsKind.Toggles);
        toggles.Toggles = new TogglesOptions { Shown = [Toggles.All[2].Toggle, Toggles.All[0].Toggle, Toggles.All[1].Toggle] };
        IReadOnlyList<OptionRow> Rows() => [.. OptionRows.Of(toggles).Where(row => row.Ordered)];
        Assert.Equal(Toggles.All.Count(), Rows().Count);
        Assert.Equal([Toggles.All[2].Name, Toggles.All[0].Name, Toggles.All[1].Name], Rows().Take(3).Select(row => row.Title));
        Assert.All(Rows().Take(3), row => Assert.True(row.On && row.Move is not null));
        Assert.All(Rows().Skip(3), row => Assert.True(!row.On && row.Move is null));
        Assert.All(Rows(), row => Assert.Equal(S.DockEditTogglesShown, row.GroupTitle));

        // Dragged from the first place to the last.
        Assert.True(Rows()[0].Move!(2));
        Assert.Equal([Toggles.All[0].Toggle, Toggles.All[1].Toggle, Toggles.All[2].Toggle], toggles.Toggles.Shown);
        Assert.False(Rows()[2].Move!(2));
        // Its switch on again keeps its place; off takes it out; one added goes to the end.
        Assert.False(Rows()[1].Set!(1));
        Assert.True(Rows()[1].Set!(0));
        Assert.Equal([Toggles.All[0].Toggle, Toggles.All[2].Toggle], toggles.Toggles.Shown);
        var added = Rows().First(row => !row.On);
        Assert.True(added.Set!(1));
        Assert.Equal(added.Title, Rows()[2].Title);
    }

    /// <summary>
    /// §317: a line's note or list is one row, its level all of them: "New note" at its top makes one,
    /// named "Note", "Note 2"…, and the line shows it in the same step; a click on another's row shows
    /// that one; the one shown is ticked; each row's pencil (F2) opens its name's field, which renames
    /// it; its bin (Delete) deletes it from every line, asked first. None of the making without the
    /// notebook to write in, and no other row of the line's for it.
    /// </summary>
    [Fact]
    public void ALinesNoteIsPickedMadeRenamedAndDeletedInItsLevel()
    {
        var folder = Path.Combine(Path.GetTempPath(), "karpo-notebook-" + Guid.NewGuid().ToString("N"));
        using var book = new Notebook(folder);
        try
        {
            var note = New(info => info.Options == WidgetOptionsKind.Note);
            var devices = new OptionDevices { Book = book };
            // The menu as it shows the line now: its Note row and that row's level.
            (FlyoutItem Row, List<FlyoutItem> Level, List<OptionPick> Picks) Menu()
            {
                List<OptionPick> picks = [];
                var row = OptionMenu.Rows(OptionRows.Of(note, devices), picks).Single(item => item.Text == S.SettingsWidgetNoteNoteHeader);
                return (row, [.. row.Children!], picks);
            }
            bool Act(FlyoutItem item, List<OptionPick> picks) => OptionMenu.Apply(note, OptionMenu.PickOf(picks, item.Id)!, devices);

            // Nothing yet: the level is "New note" alone, and a click on it makes "Note" and shows it.
            var (_, level, picks) = Menu();
            var add = Assert.Single(level);
            Assert.Equal((S.SettingsWidgetNotebookNewNote, true, OptionPickKind.Act), (add.Text, add.Stays, OptionMenu.PickOf(picks, add.Id)!.Kind));
            Assert.Null(OptionMenu.AskOf(note, OptionMenu.PickOf(picks, add.Id)!, devices));
            Assert.True(Act(add, picks));
            var first = Assert.Single(book.Current.Notes);
            Assert.Equal((S.WidgetNoteNewNote, first.Id), (first.Title, note.Note!.Note));
            (_, level, picks) = Menu();
            Assert.True(Act(level[^1], picks));
            var second = book.Current.Notes[1];
            Assert.Equal((S.WidgetNoteNewNoteNumbered(2), second.Id), (second.Title, note.Note!.Note));

            // The level: every note with its pencil and bin, the one shown ticked, "New note" right under them (§411); the row says it.
            var (row, now, rowPicks) = Menu();
            Assert.Equal(second.Title, row.Value!.Text);
            Assert.Equal([first.Title, second.Title, "", S.SettingsWidgetNotebookNewNote], now.Select(item => item.Text));
            Assert.Equal([false, true], now.Take(2).Select(item => item.Checked == true));
            Assert.All(now.Take(2), item =>
            {
                var tools = Assert.IsType<FlyoutControl.Tools>(item.Control).Buttons;
                Assert.Equal([S.SettingsWidgetNotebookRename, S.SettingsWidgetNotebookDelete], tools.Select(tool => tool.Name));
                Assert.Equal([MenuChord.Rename, MenuChord.Delete], tools.Select(tool => tool.Keys!.Value));
                Assert.True(item.Stays);
            });

            // A click on the first's row shows it.
            Assert.True(Act(now[0], rowPicks));
            Assert.Equal(first.Id, note.Note!.Note);
            Assert.False(Act(now[0], rowPicks));

            // Its pencil's level is its name's field, which renames it.
            (_, now, rowPicks) = Menu();
            var rename = ((FlyoutControl.Tools)now[0].Control!).Buttons[0];
            var field = Assert.Single(rename.Level!);
            Assert.Equal(first.Title, Assert.IsType<FlyoutField>(field.Control).Text);
            Assert.True(FieldPicks.Keep(note, OptionMenu.PickOf(rowPicks, field.Id)!, new FieldCommit(field.Id, "Ideas", null), devices));
            Assert.Equal("Ideas", book.Current.Note(first.Id)!.Title);
            Assert.False(FieldPicks.Keep(note, OptionMenu.PickOf(rowPicks, field.Id)!, new FieldCommit(field.Id, "  ", null), devices));

            // Its bin asks Windows' question first, then deletes it from every line.
            (_, now, rowPicks) = Menu();
            var bin = OptionMenu.PickOf(rowPicks, ((FlyoutControl.Tools)now[0].Control!).Buttons[1].Id)!;
            var question = OptionMenu.AskOf(note, bin, devices)!;
            Assert.Equal((S.SettingsWidgetNotebookDeleteNote, S.SettingsWidgetNotebookDeleteNoteBody("Ideas")), (question.Title, question.Content));
            Assert.True(OptionMenu.Apply(note, bin, devices));
            Assert.Equal([second.Id], book.Current.Notes.Select(each => each.Id));
            // The line shows the notebook's first again, ticked.
            (row, now, _) = Menu();
            Assert.Equal(second.Title, row.Value!.Text);
            Assert.True(now[0].Checked);

            // A to-do line's are lists: "New list", "Tasks"; no making without the notebook to write in.
            var todo = New(info => info.Options == WidgetOptionsKind.Todo);
            var lists = OptionRows.Of(todo, devices).Single(each => each.Key == OptionRows.TodoList);
            Assert.Equal(S.SettingsWidgetNotebookNewList, lists.Owned!.Add!.Title);
            Assert.True(lists.Owned.Add.Set!(0));
            Assert.Equal((S.WidgetTodoNewList, todo.Todo!.List), (book.Current.Lists.Single().Name, book.Current.Lists.Single().Id));
            Assert.Null(OptionRows.Of(note, Sources with { Book = null, Notebook = book.Current }).Single(each => each.Key == OptionRows.Notebook).Owned);
            // One row for it all: nothing of it beside the choice.
            Assert.DoesNotContain(OptionRows.Of(note, devices), each => each.Key.StartsWith("Notebook", StringComparison.Ordinal) && each.Key != OptionRows.Notebook);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>Every row can be set where it stands: no row leads to a page any more (§228).</summary>
    [Fact]
    public void EveryRowCanBeSetInTheMenu()
    {
        var rows = AllRows().Concat(PinRows()).ToList();
        // §351: a link sets nothing here: it leads to the level that owns its setting.
        Assert.All(rows.Where(row => row.Kind is not (OptionRowKind.Text or OptionRowKind.Search or OptionRowKind.Target or OptionRowKind.Link or OptionRowKind.Level)), row => Assert.NotNull(row.Set));
        Assert.All(rows.Where(row => row.Kind == OptionRowKind.Link), row => Assert.Null(row.Set));
        // A field the menu has (§219): typed or searched for, with the field it stands for.
        Assert.All(rows.Where(row => row.Kind == OptionRowKind.Text), row => Assert.True(row.SetText is not null && row.Field == OptionField.Text));
        Assert.All(rows.Where(row => row.Kind == OptionRowKind.Search), row => Assert.True(row.Finder is not null && row.Pick is not null
            && row.Field is OptionField.Text or OptionField.Place or OptionField.Zone or OptionField.Symbol or OptionField.Search));
        // A pin's target is set with the path Windows' open dialog chose (OptionPickers, §224).
        Assert.All(rows.Where(row => row.Kind == OptionRowKind.Target), row => Assert.NotNull(row.SetText));
        // A list nobody listed is no row: the menu lists the drives before it shows one.
        Assert.DoesNotContain(OptionRows.Of(New(info => info.Options == WidgetOptionsKind.Drive)), row => row.Key == OptionRows.Drive);
        List<OptionPick> picks = [];
        foreach (var item in EveryKind())
        {
            _ = OptionMenu.Rows(OptionRows.Of(item, Sources, new OptionPlace(new Config(), Dark: true)), picks);
        }
        Assert.True(picks.Count > 500);
    }

    // --- The same write as Settings ------------------------------------------------------------------

    /// <summary>
    /// Each row set through OptionRows, and the same setting as the page's control wrote it (IslandOptions,
    /// FolderPage, before they went through the rows and then went, §228): the same widget, byte for byte.
    /// </summary>
    [Fact]
    public void EachRowWritesWhatTheSettingsPageWrote()
    {
        void Same(WidgetItem widget, string key, int value, Action<WidgetItem> page)
        {
            var settings = Copy(widget);
            Assert.True(OptionRows.Apply(OptionRows.Of(widget, Sources), key, value), $"{key} changed nothing on {widget.Info?.Id}");
            page(settings);
            Assert.Equal(Json(settings), Json(widget));
            // Set again, nothing changes.
            Assert.False(OptionRows.Apply(OptionRows.Of(widget, Sources), key, value), $"{key} changed {widget.Info?.Id} twice");
        }
        WidgetItem Of(WidgetOptionsKind kind, bool bar = false) => New(info => info.Options == kind, bar);

        Same(Of(WidgetOptionsKind.Desktops, bar: true), OptionRows.DesktopsShowApps, 0, item => item.Desktops!.ShowApps = false);
        Same(Of(WidgetOptionsKind.Weather), OptionRows.WeatherUsesDevice, 0, item => item.Weather!.UseDeviceLocation = false);
        Same(New(info => info == WidgetInfos.AirQuality), OptionRows.AirIndex, 2, item => item.Weather!.AirIndex = AirIndexes.All[2]);
        Same(New(info => info == WidgetInfos.Quakes), OptionRows.QuakeRadius, 5, item => item.Weather!.Quakes = new QuakeOptions { Radius = QuakeChoices.World });
        Same(New(info => info == WidgetInfos.Quakes), OptionRows.QuakeMagnitude, 0, item => item.Weather!.Quakes = new QuakeOptions { Magnitude = 2.5 });
        Same(New(info => info == WidgetInfos.Quakes), OptionRows.QuakeAlerts, 0, item => item.Weather!.Quakes = new QuakeOptions { Alerts = false });
        var years = MarketRanges.All.ToList().IndexOf(MarketRange.Years);
        Same(Of(WidgetOptionsKind.Market), MarketRows.Range, years, item => item.Market!.Range = MarketRange.Years);
        var stock = Of(WidgetOptionsKind.Market);
        stock.Market!.Range = MarketRange.Years;
        Same(stock, MarketRows.Years, 7, item => item.Market!.RangeYears = 7);
        var clock = New(info => info.Options == WidgetOptionsKind.Clock && info.ShowsTime == true);
        Same(clock, OptionRows.ClockSeconds, 1, item => item.Clock!.Seconds = true);
        Same(Of(WidgetOptionsKind.Drive), OptionRows.Drive, 1, item => DriveTargets.PointAt(item.Drive!, Sources.Drives![1]));
        Same(Of(WidgetOptionsKind.Countdown), OptionRows.CountdownMinutes, 30, item => (item.Countdown ??= new CountdownOptions()).Minutes = 30);
        Same(New(info => info == WidgetInfos.Focus), OptionRows.CountdownDoNotDisturb, 0,
            item => (item.Countdown ??= new CountdownOptions { Minutes = CountdownLength.Of(item) }).DoNotDisturb = CountdownLength.DoNotDisturb(false));
        Same(Of(WidgetOptionsKind.Alarm), OptionRows.AlarmOn, 1, item => item.Alarm!.On = true);
        Same(Of(WidgetOptionsKind.Alarm), OptionRows.AlarmDay + (int)DayOfWeek.Wednesday, 1,
            item => item.Alarm!.Days = AlarmDays.Of(AlarmDays.Mask(item.Alarm.Days) | AlarmDays.Bit(DayOfWeek.Wednesday)));
        var toggles = Of(WidgetOptionsKind.Toggles);
        var added = Toggles.All.First(toggle => !toggles.Toggles!.Shown.Contains(toggle.Toggle)).Toggle;
        Same(toggles, OptionRows.Toggle + added, 1, item => item.Toggles!.Shown = [.. item.Toggles.Shown, added]);
        var removed = toggles.Toggles!.Shown[1];
        Same(toggles, OptionRows.Toggle + removed, 0, item => item.Toggles!.Shown = [.. item.Toggles.Shown.Where((_, at) => at != 1)]);
        // §324: this PC's lists, then the account's.
        Same(Of(WidgetOptionsKind.Todo), OptionRows.TodoList, 3, item =>
        {
            item.Todo!.Account = "me@example.com";
            item.Todo.TaskList = "shop";
        });
        Same(Of(WidgetOptionsKind.Todo), OptionRows.TodoList, 1, item => item.Todo!.List = "b");
        Same(Of(WidgetOptionsKind.Note), OptionRows.Notebook, 1, item => item.Note!.Note = "n2");
        Same(Of(WidgetOptionsKind.WindowsSearch, bar: true), OptionRows.WindowsSearchShowBox, 1, item => ((BarItem)item).WindowsSearch = new WindowsSearchOptions { ShowBox = true });
        Same(Of(WidgetOptionsKind.ShowDesktop, bar: true), OptionRows.ShowDesktopStyle, 1, item => ((BarItem)item).ShowDesktop = new ShowDesktopOptions { Style = ShowDesktopStyle.Corner });
        Same(Of(WidgetOptionsKind.Divider), OptionRows.DividerStyle, 1, item => ((CellItem)item).Divider = new DividerOptions { Style = DividerStyle.Grow });
        Same(Of(WidgetOptionsKind.StatusIcons, bar: true), OptionRows.StatusIcon + StatusIcons.Key(StatusIcon.Camera), 2,
            item => ((BarItem)item).StatusIcons = StatusIcons.With(((BarItem)item).StatusIcons, StatusIcon.Camera, TrayState.Hidden));
        Same(Of(WidgetOptionsKind.Volume), OptionRows.VolumeOrientation, 2, item => item.Volume!.Orientation = SliderOrientation.Vertical);
        Same(Of(WidgetOptionsKind.Volume), OptionRows.VolumeLength, 200, item => item.Volume!.Length = 200);
        Same(Of(WidgetOptionsKind.Volume), OptionRows.VolumeShowSpeaker, 0, item => item.Volume!.ShowSpeaker = false);
        Same(Of(WidgetOptionsKind.Calendar), OptionRows.CalendarDaysAhead, 14, item => item.Calendar!.DaysAhead = 14);
        Same(Of(WidgetOptionsKind.Brightness), OptionRows.BrightnessScreens, 1, item => item.Brightness = new BrightnessOptions { Screens = BrightnessScreens.Own });
        Same(Of(WidgetOptionsKind.InputButton, bar: true), OptionRows.InputButtonShow, 1, item => ((BarItem)item).InputButton = new InputButtonOptions { Show = InputButtonShow.Always });
        Same(Of(WidgetOptionsKind.Switch), OptionRows.SwitchShowLabel, 1, item => item.Switch!.ShowLabel = true);
        Same(New(info => info == WidgetInfos.SpatialSound), OptionRows.SpatialFormat, 1, item => item.Switch!.SpatialFormat = SpatialSoundFormats.Known[0].Id);
        Same(New(info => info == WidgetInfos.ColorFilters), OptionRows.ColorFilter, 2, item => item.Switch!.ColorFilter = ColorFilterChoices.All[2]);

        // A line of items: its size, and each part it offers, as the page's SetItems wrote them.
        var items = ListKind.Mail.New();
        void Items(Action<ItemStyleOptions> change, WidgetItem item)
        {
            var options = item.Items ?? new ItemStyleOptions();
            change(options);
            item.Items = options.Normalized(ItemStyles.KindOf(item)!.Value);
        }
        Same(Copy(items), OptionRows.ItemSize, 0, item => Items(options => options.Size = ItemSize.Compact, item));
        Same(Copy(items), OptionRows.ItemsTime, ItemStyles.Of(items).Time ? 0 : 1, item => Items(options => options.Time = !ItemStyles.Of(items).Time, item));

    }

    /// <summary>
    /// The lists a row chooses from: a drive widget the drives it may show and the one it names that is
    /// not attached, which picks nothing; a to-do line this PC and the To Do lists, a list not named its
    /// account's default; an alarm's days in the week as Karpo starts it.
    /// </summary>
    [Fact]
    public void AListsRowOffersWhatWasListed()
    {
        var drive = New(info => info.Options == WidgetOptionsKind.Drive && info.Drives != DriveFilter.Network);
        drive.Drive!.Volume = @"\\?\Volume{9}\";
        drive.Drive.Letter = "Q:";
        drive.Drive.Label = "Old";
        var row = OptionRows.Of(drive, Sources).Single(row => row.Key == OptionRows.Drive);
        Assert.Equal(["Local Disk (C:)", "Data (D:)", S.SettingsWidgetDriveNotConnectedEntry(DriveTargets.Title("Old", "Q:"))], row.Choices);
        Assert.Equal(2, row.Chosen);
        Assert.False(row.Set!(2));

        var todo = New(info => info.Options == WidgetOptionsKind.Todo);
        todo.Todo!.Account = "ME@example.com";
        var source = OptionRows.Of(todo, Sources).Single(row => row.Key == OptionRows.TodoList);
        // §324: this PC's lists and the account's in one row; a list not named is the account's default.
        Assert.Equal(["Home", "Work", "Tasks", "Shopping"], source.Choices);
        Assert.Equal(2, source.Chosen);
        Assert.DoesNotContain(OptionRows.Of(todo, Sources), each => each.Key == OptionRows.Notebook);

        var alarm = New(info => info.Options == WidgetOptionsKind.Alarm);
        var days = OptionRows.Of(alarm, Sources).Where(each => each.Key.StartsWith(OptionRows.AlarmDay, StringComparison.Ordinal)).ToList();
        Assert.Equal(AlarmDays.From(DayOfWeek.Monday).Select(day => OptionRows.AlarmDay + (int)day), days.Select(each => each.Key));
        Assert.All(days, each => Assert.Equal(S.SettingsWidgetAlarmDaysHeader, each.Group));
    }

    /// <summary>The volume's length steps four at a time from its menu, as its slider does, and stops at its ends.</summary>
    [Fact]
    public void ANumberStepsAsItsPageDoes()
    {
        var volume = New(info => info.Options == WidgetOptionsKind.Volume);
        List<OptionPick> picks = [];
        _ = OptionMenu.Section(OptionRows.Of(volume), picks);
        var more = picks.Single(pick => pick.Kind == OptionPickKind.Step && pick.Key == OptionRows.VolumeLength && pick.Value == 1);
        Assert.True(OptionMenu.Apply(volume, more));
        Assert.Equal(124, volume.Volume!.Length);
        volume.Volume.Length = VolumeOptions.LongestDip - 2;
        Assert.True(OptionMenu.Apply(volume, more));
        Assert.Equal(VolumeOptions.LongestDip, volume.Volume.Length);
        Assert.False(OptionMenu.Apply(volume, more));
    }
}
