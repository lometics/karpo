// The add list's search (AddList.cs; the add page's own search over its rows, §575). The person: "the add
// widget dialog should have a search field where you can search for widgets"; and, the list a tree of
// folders (2026-10-11), a search lists what matches flat, each with where it is. What a search lists is
// plain code (§576: the add list's menu, its field and its window tests, gone).

using Karpo.Dock;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed class AddListSearchTests
{
    private const uint First = TheDock.CMD_ADD_WIDGET;

    // --- What a search lists, without a window ---------------------------------------------------

    /// <summary>
    /// A search lists each widget that matches once, however often the tree shows it (Popular's
    /// clock is Time and calendar's), and only those: every home whose name, words or folders have
    /// every word typed, and nothing for an empty query.
    /// </summary>
    [Theory]
    [InlineData(WidgetType.Bar, "vol")]
    [InlineData(WidgetType.Bar, "clock")]
    [InlineData(WidgetType.List, "time")]
    [InlineData(WidgetType.List, "wi-fi")]
    [InlineData(WidgetType.List, "nothing at all matches this")]
    public void ASearchListsEachMatchOnce(WidgetType type, string query)
    {
        var (items, _, _) = AddList.Rows(type, turns: false, First);
        var results = AddList.Results(items, query);

        Assert.Equal(results.Count, results.Select(result => result.Id).Distinct().Count());
        Assert.All(results, result => Assert.Null(result.Children));
        var homes = AddList.Picks(items).Where(pick => pick.Words is not null).ToList();
        foreach (var home in homes)
        {
            var found = results.Any(result => result.Id == home.Id);
            Assert.True(CatalogueSearch.Matches(query, home.Text, [.. home.Words!, .. Path(items, home.Id)]) == found, $"{home.Text}: listed {found} for \"{query}\"");
        }
        Assert.Empty(AddList.Results(items, ""));
        Assert.Empty(AddList.Results(items, "   "));
        Assert.Empty(AddList.Results(items, null));
    }

    /// <summary>
    /// Each result says where it is in the tree, under its name: the folders it is in, or at the top
    /// its column's heading; a greyed one its reason after that.
    /// </summary>
    [Fact]
    public void AResultSaysWhereItIs()
    {
        var (items, _, _) = AddList.Rows(WidgetType.List, turns: false, First);
        Assert.Equal("Connections", AddList.Results(items, "wi-fi on/off").Single().Detail);
        Assert.Equal("Battery and power", AddList.Results(items, "hibernate").Single().Detail);
        // The mixer is a widget of its own (§318), found by its name; the details a reading each in System info (§490).
        Assert.Equal("Music and sound", AddList.Results(items, "volume mixer").First(result => result.Text == "Volume mixer").Detail);
        Assert.Equal("System info", AddList.Results(items, "memory details").First(result => result.Text == "Memory details").Detail);
        Assert.Equal(AddTree.Popular.Title, AddList.Results(items, "heading").First(result => result.Text == "Heading").Detail);
        var rotation = AddList.Results(items, "rotation lock").Single();
        Assert.False(rotation.Enabled);
        Assert.Equal("Screen and sharing · This screen doesn't turn", rotation.Detail);
    }

    /// <summary>
    /// What is called what was typed comes first, then what has it in its name, then what has it in
    /// its words, then what is in a folder of that name — each in the tree's order.
    /// </summary>
    [Fact]
    public void WhatIsCalledThatComesFirst()
    {
        var (items, _, _) = AddList.Rows(WidgetType.List, turns: false, First);
        var results = AddList.Results(items, "battery");
        Assert.Equal("Battery", results[0].Text);
        Assert.Contains(results, result => result.Text == "Power mode");
        Assert.True(results.FindIndex(result => result.Text == "Power mode") > results.FindIndex(result => result.Text.Contains("Battery", StringComparison.Ordinal) && result.Text != "Battery"));
        // "Connections" is no widget's name: what is in that folder.
        var connections = AddList.Results(items, "connections");
        Assert.Contains(connections, result => result.Text == "Airplane mode on/off");
        Assert.Equal("VPN connections", connections[0].Text);
    }

    /// <summary>
    /// §283: each reading of the performance widget is found by its own words — "ram" Memory, "gpu"
    /// Graphics, "cpu" Processor — and not by another reading's; "performance" finds all five.
    /// </summary>
    [Theory]
    [InlineData("ram", "Memory")]
    [InlineData("gpu", "Graphics")]
    [InlineData("cpu", "Processor")]
    [InlineData("bandwidth", "Network activity")]
    [InlineData("ssd", "Disk")]
    public void EachReadingIsFoundByItsOwnWords(string query, string reading)
    {
        string[] readings = ["Processor", "Graphics", "Memory", "Disk", "Network activity"];
        var (items, _, _) = AddList.Rows(WidgetType.List, turns: false, First);
        var found = AddList.Results(items, query).Where(result => result.Detail == "System info").Select(result => result.Text).Intersect(readings);
        Assert.Equal([reading], found);
        Assert.Equal(readings, AddList.Results(items, "performance").Select(result => result.Text).Intersect(readings));
    }

    /// <summary>
    /// Enter adds the one result; not where there are more, or none, or where the one is greyed and
    /// can't be added here.
    /// </summary>
    [Fact]
    public void EnterAddsOnlyASingleMatchThatCanBeAdded()
    {
        var (items, entries, _) = AddList.Rows(WidgetType.List, turns: false, First);
        var hibernate = AddList.Results(items, "hibernate");
        Assert.Equal("hibernate", entries[(int)(AddList.Single(hibernate)!.Value - First)].Id);
        Assert.Null(AddList.Single(AddList.Results(items, "sound")));
        Assert.Null(AddList.Single([]));
        Assert.Null(AddList.Single(AddList.Results(items, "rotation lock")));
    }

    /// <summary>The folders <paramref name="id"/>'s home is in, by their names and words.</summary>
    private static IEnumerable<string> Path(IReadOnlyList<FlyoutItem> items, uint id)
    {
        foreach (var item in items.Where(item => item.Children is not null))
        {
            if (Within(item, id))
            {
                return Folders(item, id);
            }
        }
        return [];

        static bool Within(FlyoutItem item, uint id) =>
            item.Children is { } children ? children.Any(child => Within(child, id)) : item.Id == id && item.Words is not null;

        static IEnumerable<string> Folders(FlyoutItem item, uint id) =>
            item.Children is { } children && Within(item, id)
                ? [item.Text, .. item.Words ?? [], .. children.SelectMany(child => Folders(child, id))]
                : [];
    }
}

/// <summary>
/// §485: Windows' own three widgets are found by what they show and open — the clock by clock, time, date, calendar and
/// notifications; the Widgets button by weather, forecast and widgets; quick settings' button by Wi-Fi, network, volume,
/// sound and battery —, in each of Karpo's languages by its own words and by the English ones.
/// </summary>
[Collection(Localization.InLanguage.Collection)]
public sealed class WindowsOwnSearchTests
{
    private static readonly (BarKind Kind, string Key, string[] English)[] Own =
    [
        (BarKind.WindowsNotifications, "core.widget.windowsNotifications.keywords", ["clock", "time", "date", "calendar", "notifications"]),
        (BarKind.WidgetsBoard, "core.widget.widgetsBoard.keywords", ["weather", "forecast", "widgets"]),
        (BarKind.WindowsQuickSettings, "core.widget.windowsQuickSettings.keywords", ["wifi", "network", "volume", "sound", "battery"]),
    ];

    public static TheoryData<string> Languages => [.. Localization.StringFiles.Codes];

    [Theory]
    [MemberData(nameof(Languages))]
    public void WindowsOwnWidgetsAreFoundByWhatTheyShow(string language)
    {
        var file = Localization.StringFiles.Read(language).ToDictionary(pair => pair.Key, pair => pair.Value);
        using var speaking = Localization.InLanguage.Use(language);
        var (items, _, _) = AddList.Rows(WidgetType.Bar, turns: false, TheDock.CMD_ADD_WIDGET);
        foreach (var (kind, key, english) in Own)
        {
            var title = BarEntries.Of(kind).Info.Title;
            // Its own words, each of them, and the English ones.
            var own = file[key].Split([',', '،'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Assert.True(own.Length > english.Length, $"{language}: {key} has {own.Length} words");
            foreach (var word in own.Concat(english))
            {
                Assert.True(AddList.Results(items, word).Any(result => result.Text == title), $"{language}: \"{word}\" does not find {title}");
            }
        }
    }
}
