// The library's two pages, Taskbar and List (docs\widgets.md §7, §16; the split, S7): each
// lists exactly its type's registry, grouped, and nothing decides membership but the registries
// themselves. Every decision here is a pure one, so all of it is checked without drawing anything;
// the pictures are only about how the pages look.
//
// These replaced the tests of the Widgets and Components pages, which were two views of one
// registry cut by a flag on each kind (WidgetKind.Offer). A page that is its registry has nothing
// to cut, so what is held here is that the cut is gone: the rows of a page are the registry's
// members, no more and no fewer, in the registry's order within each group.

namespace Karpo.Tests.Core;

public sealed class CataloguePagesTests
{
    /// <summary>The ids of a registry, in its own order.</summary>
    private static IReadOnlyList<string> Registry(WidgetType page) => page switch
    {
        // A widget with styles is one member, its first style (WidgetStyles, §209); one a reading for a widget with readings (§283).
        WidgetType.List => [.. ListWidgets.All.Where(widget => WidgetStyles.Of(widget.Kind) is not { } styled || styled.Widget.Offered.Contains(styled.Style)).Select(widget => widget.Info.Id)],
        _ => [.. BarEntries.All.Select(entry => entry.Info.Id)],
    };

    public static TheoryData<WidgetType> Pages()
    {
        var pages = new TheoryData<WidgetType>();
        foreach (var page in Catalogue.Pages)
        {
            pages.Add(page);
        }
        return pages;
    }

    [Fact]
    public void ThereAreTwoPagesInTheTypesOwnOrder() =>
        Assert.Equal([WidgetType.Bar, WidgetType.List], Catalogue.Pages);

    /// <summary>
    /// A page is its registry: every member once, nothing else. A widget missing from its page would
    /// be placeable and in front of nobody; a row that is not a member would add something the page's
    /// place cannot hold.
    /// </summary>
    [Theory]
    [MemberData(nameof(Pages))]
    public void EachPageListsExactlyItsRegistry(WidgetType page)
    {
        var rows = Catalogue.On(page);
        var members = Registry(page);

        Assert.Equal(members.Count, rows.Count);
        Assert.Equal(members.ToHashSet(StringComparer.Ordinal), rows.Select(row => row.Id).ToHashSet(StringComparer.Ordinal));
        // Once each: an id never stands twice on one page.
        Assert.Equal(rows.Count, rows.Select(row => row.Id).Distinct(StringComparer.Ordinal).Count());
        // Every row says its page, and its kind is the one that page's registry would place.
        Assert.All(rows, row => Assert.Equal(page, row.Type));
    }

    /// <summary>
    /// A page read twice is the same page, row for row: a row's words are read afresh each time
    /// (so a language change reaches them), and two lists of the same words are the same row's
    /// words. A row that says another word is another row.
    /// </summary>
    [Theory]
    [MemberData(nameof(Pages))]
    public void APageReadTwiceIsTheSamePage(WidgetType page)
    {
        var first = Catalogue.On(page);
        var again = Catalogue.On(page);

        Assert.Equal(first, again);
        Assert.Equal(first.Select(row => row.GetHashCode()), again.Select(row => row.GetHashCode()));
        Assert.NotEqual(first[0], first[0] with { Keywords = [.. first[0].Keywords, "another"] });
        Assert.NotEqual(first[0], first[0] with { English = [.. first[0].English, "another"] });
    }

    /// <summary>
    /// Group by group, and inside a group in the registry's own order — the order a bar shows them
    /// when nobody has moved anything, which is also the order somebody scanning a group expects.
    /// </summary>
    [Theory]
    [MemberData(nameof(Pages))]
    public void APageIsGroupedAndKeepsTheRegistrysOrderInsideAGroup(WidgetType page)
    {
        var rows = Catalogue.On(page);
        // The groups come in the enum's order, and never come back once left.
        Assert.Equal(rows.Select(row => (int)row.Category).OrderBy(order => order), rows.Select(row => (int)row.Category));
        var registry = Registry(page);
        foreach (var group in rows.GroupBy(row => row.Category))
        {
            var ids = group.Select(row => row.Id).ToList();
            Assert.Equal(registry.Where(ids.Contains), ids);
        }
    }

    /// <summary>
    /// What a row says is the widget's own <see cref="WidgetInfo"/>, which the two registries
    /// share, so the calendar on the Taskbar page and the calendar on the List page cannot be two names.
    /// </summary>
    [Fact]
    public void ARowSaysWhatItsWidgetInfoSays()
    {
        foreach (var row in Catalogue.Everything())
        {
            var info = row.Type switch
            {
                WidgetType.List => ListWidgets.Find(row.Id)!.Info,
                _ => BarEntries.Find(row.Id)!.Info,
            };
            // A widget with styles says the widget's words, or its reading's, found by each style's too (§209, §283).
            if (row.List is { } form && WidgetStyles.Of(form) is { Widget: var styled, Style: var style })
            {
                Assert.Equal(styled.TitleOf(style), row.Title);
                Assert.Equal(styled.GlyphOf(style), row.Glyph);
                Assert.Equal(styled.BlurbOf(style), row.Blurb);
                Assert.Equal(info.Category, row.Category);
                Assert.Equal(styled.KeywordsOf(style), row.Keywords);
                Assert.Contains(info.Title, row.Keywords);
                continue;
            }
            Assert.Equal(info.AddTitle, row.Title);
            Assert.Equal(info.Glyph, row.Glyph);
            Assert.Equal(info.Blurb, row.Blurb);
            Assert.Equal(info.Category, row.Category);
            Assert.Equal(info.Keywords, row.Keywords);
        }
    }

    /// <summary>
    /// A row places its own type's member and nothing else: exactly one of the two is set, it is
    /// the one its page stands for, and it is the registry member of the row's id. So adding from a
    /// row goes straight to the typed add for its place, with no conversion in between.
    /// </summary>
    [Fact]
    public void ARowPlacesItsOwnTypedMember()
    {
        foreach (var row in Catalogue.Everything())
        {
            var set = (row.Bar is null ? 0 : 1) + (row.List is null ? 0 : 1);
            Assert.True(set == 1, $"{row.Key} places {set} members");
            var id = row.Type switch
            {
                WidgetType.List => row.List is { } line ? line.Id() : null,
                _ => row.Bar is { } entry ? entry.Id() : null,
            };
            Assert.Equal(row.Id, id);
        }
    }

    /// <summary>
    /// The same id on more than one page is expected — "calendar" is a taskbar entry and a list widget
    /// — so a row is named by its page and its id together, and that name is unique across the
    /// library.
    /// </summary>
    [Fact]
    public void ARowIsNamedByItsPageAndItsId()
    {
        var everything = Catalogue.Everything();
        Assert.Equal(Catalogue.Pages.Sum(page => Catalogue.On(page).Count), everything.Count);
        Assert.Equal(everything.Count, everything.Select(row => row.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(everything, row => row.Key == "bar/calendar");
        Assert.Contains(everything, row => row.Key == "list/calendar");
        // Start is the taskbar's alone: one row, on one page, and no key for the other.
        Assert.Contains(everything, row => row.Key == "bar/start");
        Assert.DoesNotContain(everything, row => row.Key == "list/start");
    }

    /// <summary>A folder is a list, and what stands in it is a list widget; no folder at all is the bar.</summary>
    [Fact]
    public void TheTypeOfWhatStandsInAFolderIsTheFoldersView()
    {
        Assert.Equal(WidgetType.Bar, Catalogue.TypeOf(null));
        Assert.Equal(WidgetType.List, Catalogue.TypeOf(FolderView.List));
    }

    /// <summary>Each page names itself and says what it is in one line.</summary>
    [Fact]
    public void EachPageSaysWhatItIs()
    {
        Assert.Equal(["Taskbar", "Folder"], Catalogue.Pages.Select(page => page.Title()));
        Assert.Equal(["Taskbar entry", "Folder widget"], Catalogue.Pages.Select(page => page.Name()));
        Assert.Equal(2, Catalogue.Pages.Select(page => page.Blurb()).Distinct().Count());
        Assert.All(Catalogue.Pages, page => Assert.False(string.IsNullOrWhiteSpace(page.Blurb())));
    }

    // --- The groups (once the settings app's search held these; the add list reads them now) ---

    /// <summary>
    /// The groups come in the library's own order on every page, as the add list shows them, and
    /// every row has a name and a blurb.
    /// </summary>
    [Fact]
    public void EveryRowHasAGroupAndTheGroupsAreInOrder()
    {
        WidgetCategory[] order =
        [
            WidgetCategory.Taskbar,
            // windowsMenus: Windows' own menus together, after the taskbar's own parts (§466).
            WidgetCategory.WindowsMenus,
            WidgetCategory.Time,
            // calendar, weather: the flyouts' pieces as lines, each a family of its own.
            WidgetCategory.Calendar, WidgetCategory.Weather,
            WidgetCategory.Network, WidgetCategory.System,
            // quick: the screen's brightness, night light and rotation lock, beside the PC's.
            WidgetCategory.Display,
            // perf: the live graphs, after System, where somebody asking how busy the PC is reads next.
            WidgetCategory.Performance,
            WidgetCategory.Media, WidgetCategory.Files,
            // notes: the to-do lists and the notes, before the folders that hold them.
            WidgetCategory.Notes, WidgetCategory.Folders,
        ];
        foreach (var page in Catalogue.Pages)
        {
            var groups = Catalogue.On(page).Select(row => row.Category).Distinct().ToList();
            Assert.Equal(order.Where(groups.Contains), groups);
        }
        Assert.All(Catalogue.Everything(), row =>
        {
            Assert.Contains(row.Category, order);
            Assert.NotEmpty(row.Title);
            Assert.NotEmpty(row.Blurb);
        });
    }

    /// <summary>Where someone browsing would look for them (docs\widgets.md §7), whichever page they are on.</summary>
    [Theory]
    [InlineData("start", WidgetCategory.WindowsMenus)]
    [InlineData("windowsQuickSettings", WidgetCategory.WindowsMenus)]
    [InlineData("tray", WidgetCategory.Taskbar)]
    [InlineData("clock", WidgetCategory.Time)]
    [InlineData("weatherNow", WidgetCategory.Weather)]
    [InlineData("calendar", WidgetCategory.Calendar)]
    [InlineData("wifi", WidgetCategory.Network)]
    [InlineData("airplaneMode", WidgetCategory.Network)]
    [InlineData("battery", WidgetCategory.System)]
    [InlineData("keyboard", WidgetCategory.System)]
    [InlineData("brightness", WidgetCategory.Display)]
    [InlineData("nightLight", WidgetCategory.Display)]
    [InlineData("volume", WidgetCategory.Media)]
    [InlineData("media", WidgetCategory.Media)]
    [InlineData("pin", WidgetCategory.Files)]
    [InlineData("drive", WidgetCategory.Files)]
    [InlineData("folderList", WidgetCategory.Folders)]
    public void AWidgetSitsWhereSomeoneWouldLookForIt(string id, WidgetCategory group)
    {
        var rows = Catalogue.Everything().Where(row => row.Id == id).ToList();
        Assert.NotEmpty(rows);
        // The group is the widget's, not the page's: the same on every page it is on.
        Assert.All(rows, row => Assert.Equal(group, row.Category));
    }

    /// <summary>
    /// Folders do not nest, so only the taskbar's library, where folders stand, has a folder. The
    /// list's has the Folders group for two things that are not folders: the heading and the
    /// divider, which arrange a list folder's lines (backlog 29; 2026-10-05, the divider "you have
    /// to add deliberately").
    /// </summary>
    [Fact]
    public void OnlyTheTaskbarsLibraryHasFolders()
    {
        Assert.Contains(Catalogue.On(WidgetType.Bar), row => row.Category == WidgetCategory.Folders);
        Assert.Equal([ListKind.Heading, ListKind.Divider], Catalogue.On(WidgetType.List).Where(row => row.Category == WidgetCategory.Folders).Select(row => row.List));
    }
}
