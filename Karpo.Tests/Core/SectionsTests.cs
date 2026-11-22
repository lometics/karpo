using static Karpo.Core.BarKind;

namespace Karpo.Tests.Core;

/// <summary>
/// A bar's list: the order a taskbar's entries stand in when nobody moved anything, where a new one
/// goes (<see cref="Sections.Insert"/>), and what a new bar holds (<see cref="Sections.DefaultLayout"/>).
/// What each entry is and says is the registry's (TypedRegistryTests).
/// </summary>
public sealed class SectionsTests
{
    [Fact]
    public void TheEntriesAreInTheTaskbarsOrder()
    {
        BarKind[] expected =
        [
            // Windows' widgets board where Windows keeps it (a new bar has neither it nor the weather)
            // (the stock and the feed that stood beside it are presets too, 2026-10-10).
            WidgetsBoard,
            // The contacts and the mail beside the web-search box went to presets (2026-10-11, 2026-10-10),
            // and the web-search box itself went (2026-10-11).
            Start, Search,
            // Task view with Windows' own buttons (the desktop you are on and the layout this bar
            // shows were buttons too, until they became the desktops and layouts presets' faces).
            TaskView,
            // The shortcuts to Windows' apps, drawn as Windows' buttons are, beside them.
            SnippingTool, WindowsSettings,
            Pin, Apps,
            // folders: the list of widgets, the one kind of folder there is.
            FolderList,
            // quick: the volume slider stands with the other quick-settings kinds, and the
            // one-button widgets (airplane mode and the rest) come after them.
            // The status icons Windows shows beside its tray while something is so, before the tray.
            Indicators,
            // The pen menu, the touch keyboard and the virtual touchpad where Windows keeps them,
            // after the tray's icons and before the quick settings.
            Tray, PenMenu, TouchKeyboard, VirtualTouchpad,
            Volume, Brightness, SpatialSound,
            // The toggles of the radios and the sound, before the other one-button widgets.
            WifiSwitch, BluetoothSwitch, SoundSwitch, MicrophoneSwitch, GameMode, ColorFilters, MonoAudio,
            AirplaneMode, EnergySaver, NightLight, RotationLock, LiveCaptions, Accessibility,
            // The accessibility page's toggles beside it (2026-10-14).
            Magnifier, Narrator, StickyKeys,
            // Keep awake, a toggle of Karpo's own (§269); Windows' dark mode (§403).
            KeepAwake, DarkMode,
            // Whether this PC is protected: with the one-button widgets, because that is where a
            // person looking for a glyph-and-a-state would go looking for it.
            Security,
            // Windows' own do not disturb, whose "zZ" the notifications preset's bell wears.
            DoNotDisturb,
            // windowsMenus: Windows' menus (§466), the quick link menu beside Start, the rest at the end.
            QuickLink, WindowsNotifications, WindowsQuickSettings, WindowsSound, WindowsNetworks,
            WindowsCast, WindowsClipboard, EmojiPanel,
            ShowDesktop,
        ];
        Assert.Equal(expected, BarEntries.All.Select(entry => entry.Kind));
    }

    /// <summary>
    /// Kinds that are gone read as kinds this build does not know: the combined "performance"
    /// island, whose four cells are four widgets now, the "system", "pins" and "drives" islands
    /// only older files carried, the row folder, removed ("the row folder should actually be
    /// removed completely for now"), and screen time, which is no widget at all since
    /// 2026-09-26. No registry has them, so nothing can place one.
    /// </summary>
    [Theory]
    [InlineData("performance")]
    [InlineData("system")]
    [InlineData("pins")]
    [InlineData("drives")]
    [InlineData("folderRow")]
    public void RemovedKindsAreNotPlaceable(string name)
    {
        var kind = Configs.Kind(name);
        Assert.Null(BarEntries.Find(kind));
        Assert.False(Sections.CanAdd([], kind));
        Assert.Null(BarEntries.Find(name));
        Assert.Null(ListWidgets.Find(name));
    }

    [Fact]
    public void CanAddTakesEveryTaskbarEntry()
    {
        List<BarItem> list = [Start.New(), BarKind.NightLight.New(), DoNotDisturb.New(), Pin.New()];
        foreach (var entry in BarEntries.All)
        {
            Assert.True(Sections.CanAdd(list, entry.Kind), $"CanAdd({entry.Kind})");
        }
        Assert.False(Sections.CanAdd(list, TolerantEnumConverter<BarKind>.Unknown));
    }

    [Fact]
    public void DefaultLayoutHoldsOnlyPlaceableKindsOnce()
    {
        var list = Sections.DefaultLayout();
        Assert.All(list, island => Assert.NotNull(BarEntries.Find(island.Kind)));
        // Every kind once, but for the folders (the keyboard, quick settings and the clock).
        Assert.Equal(list.Count, list.Select(island => island.Folder is { } folder ? $"{island.Kind}/{folder.Face.Kind}" : $"{island.Kind}").Distinct().Count());
        Assert.Equal(list.Count, list.Select(island => island.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(list, island => island.Kind == Start);
        Assert.Contains(list, island => island.Kind == FolderList);
    }

    /// <summary>A list whose entries carry <paramref name="sections"/>, one each, in the order given.</summary>
    private static List<BarItem> Bar(params (BarKind Kind, BarSection Section)[] sections) =>
        [.. sections.Select(entry =>
        {
            var island = entry.Kind.New();
            island.Section = entry.Section;
            return island;
        })];

    private static List<BarSection> SectionsOf(IEnumerable<BarItem> list) => [.. list.Select(island => island.Section)];

    [Fact]
    public void InsertPutsAnIslandAtItsPlaceInItsSection()
    {
        var list = Bar((Start, BarSection.Middle), (Apps, BarSection.Middle), (Tray, BarSection.End), (BarKind.NightLight, BarSection.End), (ShowDesktop, BarSection.End));
        var volume = Volume.New();

        Assert.Equal(3, Sections.Insert(list, volume));
        Assert.Equal([Start, Apps, Tray, Volume, BarKind.NightLight, ShowDesktop], Configs.Kinds(list));
        Assert.Equal(BarSection.End, volume.Section);
    }

    [Fact]
    public void InsertGoesBeforeTheSectionsFirstWhenAllThereComeLater()
    {
        var list = Bar((Start, BarSection.Middle), (BarKind.NightLight, BarSection.End));

        Assert.Equal(1, Sections.Insert(list, FolderList.New()));
        Assert.Equal([Start, FolderList, BarKind.NightLight], Configs.Kinds(list));
    }

    /// <summary>
    /// A widget is ranked only against the section it belongs in: a folder belongs at the end, so
    /// the desktops at the start are not something it can stand after.
    /// </summary>
    [Fact]
    public void InsertRanksWithinTheSectionItBelongsIn()
    {
        var list = Bar((BarKind.WidgetsBoard, BarSection.Start), (Tray, BarSection.End), (BarKind.NightLight, BarSection.End));

        Assert.Equal(1, Sections.Insert(list, FolderList.New()));
        Assert.Equal([BarKind.WidgetsBoard, FolderList, Tray, BarKind.NightLight], Configs.Kinds(list));
        Assert.Equal([BarSection.Start, BarSection.End, BarSection.End, BarSection.End], SectionsOf(list));
    }

    /// <summary>A kind whose place is the start goes into the start, even where that section is empty.</summary>
    [Fact]
    public void InsertPutsAStartKindInTheStartSection()
    {
        var list = Bar((Start, BarSection.Middle), (Tray, BarSection.End), (BarKind.NightLight, BarSection.End));
        var weather = BarKind.WidgetsBoard.New();

        Assert.Equal(0, Sections.Insert(list, weather));
        Assert.Equal([BarKind.WidgetsBoard, Start, Tray, BarKind.NightLight], Configs.Kinds(list));
        Assert.Equal(BarSection.Start, weather.Section);
    }

    [Fact]
    public void ASecondOfAKindGoesAfterTheFirst()
    {
        var list = Bar((BarKind.NightLight, BarSection.End), (DoNotDisturb, BarSection.End));
        var first = list[0];
        var second = BarKind.NightLight.New();

        Assert.Equal(1, Sections.Insert(list, second));
        Assert.Same(first, list[0]);
        Assert.Same(second, list[1]);
    }

    /// <summary>
    /// The list keeps the start's entries, then the middle's, then the end's, each section in its own
    /// order; putting a list in that order again moves nothing and says so.
    /// </summary>
    [Fact]
    public void OrderKeepsEachSectionsOwnOrder()
    {
        var list = Bar((BarKind.NightLight, BarSection.End), (Start, BarSection.Middle), (BarKind.WidgetsBoard, BarSection.Start), (Tray, BarSection.End), (Apps, BarSection.Middle));

        Assert.True(Sections.Order(list));
        Assert.Equal([BarKind.WidgetsBoard, Start, Apps, BarKind.NightLight, Tray], Configs.Kinds(list));
        Assert.False(Sections.Order(list));
    }

    /// <summary>A section this build does not know is the start's, wherever it stood.</summary>
    [Fact]
    public void AnUnknownSectionOrdersAsTheStart()
    {
        var list = Bar((BarKind.NightLight, BarSection.End), (BarKind.WidgetsBoard, (BarSection)7));

        Sections.Order(list);
        Assert.Equal([BarKind.WidgetsBoard, BarKind.NightLight], Configs.Kinds(list));
        Assert.Equal(BarSection.Start, Sections.Known((BarSection)7));
    }

    /// <summary>Each section's stretch of the list; an empty one is empty, between its neighbours.</summary>
    [Fact]
    public void RangeIsEachSectionsStretchOfTheList()
    {
        var list = Bar((BarKind.WidgetsBoard, BarSection.Start), (Tray, BarSection.End), (BarKind.NightLight, BarSection.End));

        Assert.Equal((0, 1), Sections.Range(list, BarSection.Start));
        Assert.Equal((1, 1), Sections.Range(list, BarSection.Middle));
        Assert.Equal((1, 3), Sections.Range(list, BarSection.End));
        Assert.Equal((0, 0), Sections.Range([], BarSection.End));
    }

    /// <summary>
    /// A place is a section and an index: the end of one section and the start of the next are the
    /// same index, and the section says which of the two is meant. An index outside the section's own
    /// stretch lands at its nearer end.
    /// </summary>
    [Fact]
    public void PutLandsInTheSectionItWasGiven()
    {
        var list = Bar((BarKind.WidgetsBoard, BarSection.Start), (Start, BarSection.Middle), (BarKind.NightLight, BarSection.End));
        var left = Volume.New();
        var middle = Brightness.New();
        var past = EnergySaver.New();

        Assert.Equal(1, Sections.Put(list, left, new BarSlot(BarSection.Start, 1)));
        Assert.Equal(2, Sections.Put(list, middle, new BarSlot(BarSection.Middle, 2)));
        // Index 0 is the start's; the section wins, so it goes to the end's first place.
        Assert.Equal(4, Sections.Put(list, past, new BarSlot(BarSection.End, 0)));
        Assert.Equal([BarKind.WidgetsBoard, Volume, Brightness, Start, EnergySaver, BarKind.NightLight], Configs.Kinds(list));
        Assert.Equal([BarSection.Start, BarSection.Start, BarSection.Middle, BarSection.Middle, BarSection.End, BarSection.End], SectionsOf(list));
    }

    /// <summary>The section a bare index is in: the entry's standing there, the last's past the end.</summary>
    [Fact]
    public void SectionAtIsTheSectionOfTheEntryThere()
    {
        var list = Bar((BarKind.WidgetsBoard, BarSection.Start), (BarKind.NightLight, BarSection.End));

        Assert.Equal(BarSection.Start, Sections.SectionAt(list, 0));
        Assert.Equal(BarSection.End, Sections.SectionAt(list, 1));
        Assert.Equal(BarSection.End, Sections.SectionAt(list, 5));
        Assert.Equal(BarSection.Start, Sections.SectionAt([], 0));
    }

    /// <summary>What a section is called, across the bar and down it.</summary>
    [Fact]
    public void ASectionIsNamedForTheWayTheBarRuns()
    {
        BarSection[] all = [BarSection.Start, BarSection.Middle, BarSection.End];
        Assert.Equal(["left", "middle", "right"], all.Select(section => section.Title(vertical: false)));
        Assert.Equal(["top", "middle", "bottom"], all.Select(section => section.Title(vertical: true)));
    }

    [Fact]
    public void NewIslandsHaveTheirKindsOptionsAndAnId()
    {
        var clock = BarKind.NightLight.New();
        Assert.Null(clock.Calendar);
        Assert.Null(clock.Weather);
        Assert.Null(clock.Pin);
        Assert.Matches("^[0-9a-f]{8}$", clock.Id);
        Assert.NotEqual(clock.Id, BarKind.NightLight.New().Id);
        // The section a new one goes into when nobody said where: the clock at the end, with the
        // tray; the widgets button at the start, where Windows keeps its own; Start in the middle,
        // as on Windows 11, whatever this PC's taskbar alignment says.
        Assert.Equal(BarSection.End, BarKind.NightLight.SectionOf());
        Assert.Equal(BarSection.Start, BarKind.WidgetsBoard.SectionOf());
        Assert.Equal(BarSection.Middle, Start.SectionOf());
    }

    /// <summary>
    /// A new bar is Windows 11's, but at the left (2026-10-14): Start, search, Task view, the pins
    /// and the open apps at the left, nothing in the middle, and the tray, the status icons, the
    /// keyboard, quick settings and the clock button at the right — the list in its sections'
    /// order, no spacer anywhere, and no weather. Entry by entry in DefaultBarTests.
    /// </summary>
    [Fact]
    public void ANewBarIsWindows11sThreeSections()
    {
        var list = Sections.DefaultLayout(["C:\\Links\\Mail.lnk"], new InputHardware(Pen: false, Touch: false, Slate: false));
        BarSection SectionOf(BarKind kind) => list.First(island => island.Kind == kind).Section;

        foreach (var kind in (BarKind[])[Start, TaskView, Pin, Apps])
        {
            Assert.Equal(BarSection.Start, SectionOf(kind));
        }
        Assert.DoesNotContain(list, island => island.Section == BarSection.Middle);
        foreach (var kind in (BarKind[])[Tray, Indicators, ShowDesktop])
        {
            Assert.Equal(BarSection.End, SectionOf(kind));
        }
        Assert.Equal(BarSection.End, list.Single(island => island.Folder?.Face.Kind == FolderFaces.KeyboardId).Section);
        Assert.Equal(BarSection.End, list.Single(island => island.Folder?.Face.Kind == FolderFaces.DateTimeId).Section);
        Assert.DoesNotContain(list, island => island.Folder?.Face.Kind == FolderFaces.WeatherId);
        Assert.Equal(list, list.OrderBy(island => island.Section).ToList());
        Assert.DoesNotContain(list, island => island.Info?.Id == "spacer");
    }

    [Fact]
    public void ANewBarPinsTheTaskbarsAppsBeforeTheOpenOnes()
    {
        var list = Sections.DefaultLayout(["C:\\Links\\Mail.lnk", "C:\\Links\\Edge.lnk"]);

        var apps = list.FindIndex(island => island.Kind == Apps);
        Assert.Equal(["C:\\Links\\Mail.lnk", "C:\\Links\\Edge.lnk"], list.Take(apps).Where(island => island.Kind == Pin).Select(island => island.Pin!.Target));
        Assert.Equal(apps - 2, list.FindIndex(island => island.Kind == Pin));
        // The pins and the open apps are one stretch of one section.
        Assert.Single(list.Skip(apps - 2).Take(3).Select(island => island.Section).Distinct());
    }

    /// <summary>Tests never read the person's taskbar: what a new bar holds doesn't depend on the PC then.</summary>
    [Fact]
    public void AnIsolatedConfigPinsNothingOfTheTaskbars() => Assert.Empty(Sections.TaskbarPins());
}
