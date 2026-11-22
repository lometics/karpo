// The add list's tree (Karpo.Core\AddTree.cs; 2026-10-11: "do it so that it makes the most sense to a
// new user"): on both pages, everything the page offers — its registry, a folder's premade widgets,
// the bar's presets — stands in it exactly once, so a widget or a preset added to a registry and
// not to the tree fails here; only Popular and Quick settings show some again. And it stays easy to
// scan: no folder of more than twelve rows (nineteen in runs between lines), none with one row or none; and §490 no
// more than two levels — the list's top and a subject's submenu.

namespace Karpo.Tests.Core;

public sealed class AddTreeTests
{
    public static TheoryData<WidgetType> Pages => new() { WidgetType.Bar, WidgetType.List };

    /// <summary>
    /// Every member of the page has its home in the tree, once, and the tree names nothing the page
    /// lacks as a home; nothing is left for "More". A member missing names itself.
    /// </summary>
    [Theory]
    [MemberData(nameof(Pages))]
    public void EverythingThePageOffersIsPlacedOnce(WidgetType page)
    {
        var sections = AddTree.On(page);
        var homes = sections.SelectMany(section => AddTree.Picks(section.Rows)).Where(pick => !pick.Again).Select(pick => pick.Key).ToList();
        var members = AddTree.Members(page).Keys.ToList();

        Assert.Empty(members.Except(homes));
        Assert.Equal(homes.Count, homes.Distinct().Count());
        Assert.Equal(members.Order(StringComparer.Ordinal), homes.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(sections.SelectMany(section => Folders(section.Rows)), folder => folder.Shelf == AddTree.More);
    }

    /// <summary>
    /// The tree names every widget, premade widget and preset there is, each as a home once, whatever
    /// page it is on: a word of the tree that no page has would be a typo.
    /// </summary>
    [Fact]
    public void TheTreeNamesWhatThereIsEachOnce()
    {
        var leaves = Leaves(AddTree.Sections).ToList();
        var homes = leaves.Where(leaf => !leaf.Again).ToList();
        Assert.Equal(homes.Count, homes.Select(leaf => (leaf.What, leaf.Id)).Distinct().Count());
        foreach (var leaf in leaves)
        {
            var known = leaf.What switch
            {
                AddWhat.Preset => Presets.Find(leaf.Id) is not null,
                AddWhat.Premade => PremadeWidgets.Find(leaf.Id) is not null,
                _ => Catalogue.Everything().Any(row => row.Id == leaf.Id),
            };
            Assert.True(known, $"the tree names {leaf.What} {leaf.Id}, which there is none of");
            // A row shown again has a home.
            Assert.Contains(homes, home => home.What == leaf.What && home.Id == leaf.Id);
        }
        Assert.Equal(Presets.All.Select(preset => preset.Id).Order(StringComparer.Ordinal), homes.Where(leaf => leaf.What == AddWhat.Preset).Select(leaf => leaf.Id).Order(StringComparer.Ordinal));
        Assert.Equal(PremadeWidgets.All.Select(premade => premade.Id).Order(StringComparer.Ordinal), homes.Where(leaf => leaf.What == AddWhat.Premade).Select(leaf => leaf.Id).Order(StringComparer.Ordinal));
    }

    /// <summary>Only Popular and Quick settings show a row again: everywhere else a row is its home.</summary>
    [Fact]
    public void OnlyPopularAndQuickSettingsShowARowAgain()
    {
        foreach (var shelf in Shelves(AddTree.Sections))
        {
            var again = shelf.Rows.OfType<AddLeaf>().Where(leaf => leaf.Again).ToList();
            Assert.True(again.Count == 0 || shelf == AddTree.Popular || shelf == AddTree.QuickSettings, $"{shelf.Id} shows {string.Join(", ", again.Select(leaf => leaf.Id))} again");
        }
    }

    /// <summary>
    /// §490: the levels a row may be at. The list's top is the first (a column's rows: Popular's picks, Browse's
    /// subjects), a subject's submenu the second; a third only where named here, with why — none. The person
    /// (2026-11-02): "make sure that there are generally speaking not more than 2 levels, only if required up to three
    /// but not more".
    /// </summary>
    private static readonly string[] ThirdLevelAllowed = [];

    /// <summary>
    /// §490: nothing in the add list is deeper than a subject's submenu — no folder in a subject — but where
    /// <see cref="ThirdLevelAllowed"/> names it, and nothing ever deeper than three levels.
    /// </summary>
    [Theory]
    [MemberData(nameof(Pages))]
    public void NothingIsDeeperThanTwoLevels(WidgetType page)
    {
        foreach (var section in AddTree.On(page))
        {
            foreach (var subject in section.Rows.OfType<AddFolder>())
            {
                foreach (var inner in subject.Rows.OfType<AddFolder>())
                {
                    var path = $"{section.Shelf.Id} › {subject.Shelf.Id} › {inner.Shelf.Id}";
                    Assert.True(ThirdLevelAllowed.Contains(path), $"{page}: {path} is a third level");
                    Assert.DoesNotContain(inner.Rows, row => row is AddFolder);
                }
            }
        }
        // The tree as declared, both pages at once: a folder in a subject is one in ThirdLevelAllowed.
        foreach (var subject in AddTree.Sections.SelectMany(section => section.Rows.OfType<AddShelf>()))
        {
            Assert.All(subject.Rows.OfType<AddShelf>(), inner => Assert.Contains(ThirdLevelAllowed, path => path.EndsWith($"{subject.Id} › {inner.Id}", StringComparison.Ordinal)));
        }
    }

    /// <summary>
    /// §490: Windows' menus are a subject of their own on the list's top, not a folder in Start and taskbar's — every
    /// one of Windows' menus (§466) and the Widgets button in it, on the bar's page; a folder's has none of them, so
    /// none there.
    /// </summary>
    [Fact]
    public void WindowsMenusIsASubjectOfItsOwn()
    {
        var menus = Subject(WidgetType.Bar, "windowsMenus");
        var kinds = AddTree.Picks(menus.Rows).Select(pick => pick.Entry!.Id).ToList();
        Assert.Subset(kinds.ToHashSet(), Enum.GetValues<BarKind>().Where(kind => kind.IsWindowsMenu() || kind == BarKind.WidgetsBoard).Select(kind => kind.Id()).ToHashSet());
        Assert.DoesNotContain(AddTree.Picks(Subject(WidgetType.Bar, "taskbar").Rows), pick => pick.Entry?.Id is { } id && BarEntries.Find(id)?.Kind.IsWindowsMenu() == true);
        Assert.DoesNotContain(AddTree.On(WidgetType.List)[1].Rows, row => row is AddFolder { Shelf.Id: "windowsMenus" });
        // Told apart from Quick settings at a glance: no icon twice among the subjects.
        var glyphs = AddTree.Browse.Rows.OfType<AddShelf>().Select(shelf => shelf.Glyph).ToList();
        Assert.Equal(glyphs.Count, glyphs.Distinct().Count());
    }

    /// <summary>
    /// Easy to scan on both pages: a column or a folder has twelve rows at most, besides its lines — one parted by
    /// lines twenty-four in runs of twelve at most (Weather and news, §590: the weather's and the air's widgets each a run;
    /// §603: the tides' graph and the sports' four a run of their own;
    /// Quick settings, every toggle; §490: a subject whose own folders became runs) and Browse thirteen, a subject each; a folder has two rows or more (one is that row in its place, none no
    /// folder); a line stands only between two rows.
    /// </summary>
    [Theory]
    [MemberData(nameof(Pages))]
    public void EveryFolderIsShortShallowAndNeverOfOneRow(WidgetType page)
    {
        var sections = AddTree.On(page);
        Assert.Equal(["popular", "browse"], sections.Select(section => section.Shelf.Id));
        foreach (var section in sections)
        {
            Check(section.Shelf.Id, section.Rows, depth: 0, least: 1);
        }

        static void Check(string name, IReadOnlyList<AddRow> rows, int depth, int least)
        {
            Assert.True(depth <= 2, $"{name} is {depth} folders deep");
            var parted = rows.Any(row => row is AddRule);
            Assert.InRange(rows.Count(row => row is not AddRule), least, name == AddTree.Browse.Id ? 13 : parted ? 24 : 12);
            var runs = string.Concat(rows.Select(row => row is AddRule ? '|' : '.')).Split('|');
            Assert.All(runs, run => Assert.InRange(run.Length, 1, name == AddTree.Browse.Id ? 13 : 12));
            Assert.IsNotType<AddRule>(rows[0]);
            Assert.IsNotType<AddRule>(rows[^1]);
            for (int i = 1; i < rows.Count; i++)
            {
                Assert.False(rows[i] is AddRule && rows[i - 1] is AddRule, $"{name} has two lines together");
            }
            foreach (var folder in rows.OfType<AddFolder>())
            {
                Check(folder.Shelf.Id, folder.Rows, depth + 1, least: 2);
            }
        }
    }

    /// <summary>
    /// Each page keeps what it has of a subject's runs: on the bar Connections' VPN is its ready-made folder, in a folder
    /// the VPN's switch, its connections and its actions; the bar has no calendar views, so its calendar's run is the
    /// calendar's and the next meeting's ready-made folders. A subject the page has nothing of is not there (Windows
    /// menus in a folder, <see cref="WindowsMenusIsASubjectOfItsOwn"/>).
    /// </summary>
    [Fact]
    public void EachPageKeepsWhatItHasOfASubject()
    {
        var bar = Subject(WidgetType.Bar, "connections");
        Assert.Contains(bar.Rows, row => row is AddPick { Preset.Id: Presets.VpnId });
        var list = Subject(WidgetType.List, "connections");
        Assert.Subset(AddTree.Picks(list.Rows).Select(pick => pick.Entry!.Id).ToHashSet(), new HashSet<string> { "vpn", "vpnConnections" });

        var time = AddTree.Picks(Subject(WidgetType.Bar, "time").Rows).ToList();
        Assert.Contains(time, pick => pick.Preset?.Id == Presets.CalendarId);
        Assert.Contains(time, pick => pick.Preset?.Id == Presets.MeetingId);
        Assert.All(AddTree.On(WidgetType.Bar)[1].Rows.OfType<AddFolder>(), subject => Assert.DoesNotContain(subject.Rows, row => row is AddFolder));
    }

    /// <summary>
    /// The bar's page has the ready-made folders among the widgets, and a folder's page none of them
    /// but the premade widgets; neither has the other's widgets. Each column's heading is there.
    /// </summary>
    [Fact]
    public void EachPageHasItsOwn()
    {
        var bar = AddTree.On(WidgetType.Bar).SelectMany(section => AddTree.Picks(section.Rows)).ToList();
        var list = AddTree.On(WidgetType.List).SelectMany(section => AddTree.Picks(section.Rows)).ToList();
        Assert.All(bar, pick => Assert.True(pick.Preset is not null || pick.Entry is { Type: WidgetType.Bar, Premade: null }, pick.Key));
        Assert.All(list, pick => Assert.True(pick.Entry is { Type: WidgetType.List }, pick.Key));
        Assert.Contains(list, pick => pick.Entry!.Premade is not null);
        Assert.All(AddTree.Sections, section => Assert.False(string.IsNullOrEmpty(section.Title)));
    }

    /// <summary>Popular is the fixed handful, a few on each page, all shown again from their homes.</summary>
    [Theory]
    [MemberData(nameof(Pages))]
    public void PopularIsAFixedHandful(WidgetType page)
    {
        var popular = AddTree.On(page)[0].Rows.TakeWhile(row => row is not AddRule).ToList();
        Assert.InRange(popular.Count, 6, 10);
        Assert.All(popular, row => Assert.True(Assert.IsType<AddPick>(row).Again));
    }

    /// <summary>Every folder has its name in every language, and a glyph where it is a row.</summary>
    [Fact]
    public void EveryFolderHasItsWords()
    {
        foreach (var shelf in Shelves(AddTree.Sections).Append(AddTree.More))
        {
            Assert.NotEqual($"core.addTree.{shelf.Id}.title", shelf.Title);
            Assert.True(AddTree.Sections.Contains(shelf) || shelf.Glyph.Length > 0, shelf.Id);
        }
    }

    private static AddFolder Subject(WidgetType page, string id) =>
        AddTree.On(page)[1].Rows.OfType<AddFolder>().Single(folder => folder.Shelf.Id == id);

    private static IEnumerable<AddFolder> Folders(IEnumerable<AddRow> rows) =>
        rows.OfType<AddFolder>().SelectMany(folder => Folders(folder.Rows).Prepend(folder));

    private static IEnumerable<AddShelf> Shelves(IEnumerable<AddShelf> shelves) =>
        shelves.SelectMany(shelf => Shelves(shelf.Rows.OfType<AddShelf>()).Prepend(shelf)).Distinct();

    private static IEnumerable<AddLeaf> Leaves(IEnumerable<AddShelf> shelves) =>
        Shelves(shelves).SelectMany(shelf => shelf.Rows.OfType<AddLeaf>());
}
