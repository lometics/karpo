// sports: the sports line in config.json (docs\widgets.md §239) — its team, written and read back as
// written; a file from before it, or a newer one's word, read as it was; a hand-edited team that is no
// team of the table's dropped; and the widget wherever the registries list widgets: a line, its face,
// its preset, its menu's rows and the team's search row.

using System.Text.Json;
using Karpo.Dock;
using Karpo.Tests.Core;

namespace Karpo.Tests.Sports;

public sealed class SportsConfigTests
{
    private static CellItem Line(SportsOptions? team = null)
    {
        var line = ListKind.Sports.New();
        line.Id = "sports";
        line.Sports = team ?? SavedSports.Barcelona();
        line.Normalize();
        return line;
    }

    private static SportsOptions? Team(Config config) => Configs.Islands(config)[0].Folder!.Cells.Single().Island.Sports;

    [Fact]
    public void ANewLineHasNoTeamAndAsksForOne()
    {
        var made = ListKind.Sports.New();
        Assert.NotNull(made.Sports);
        Assert.False(made.Sports!.IsSet);
        Assert.Equal(SetupField.Team, WidgetSetup.Needs(made));
        Assert.Null(WidgetSetup.Needs(Line()));
    }

    [Fact]
    public void TheTeamIsWrittenAndReadBackAsWritten()
    {
        var config = Configs.With(Configs.Holding(Line()));
        using (var written = JsonDocument.Parse(config.ToJson()))
        {
            var saved = Configs.Find(written.RootElement, "sports")!.Value;
            Assert.Equal("soccer", saved.GetProperty("sport").GetString());
            Assert.Equal("esp.1", saved.GetProperty("league").GetString());
            Assert.Equal("83", saved.GetProperty("team").GetString());
            Assert.Equal("Barcelona", saved.GetProperty("name").GetString());
            Assert.False(saved.TryGetProperty("isSet", out _));
            Assert.False(saved.TryGetProperty("key", out _));
        }
        var back = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal("soccer/esp.1/83", Team(back)!.Key);
    }

    /// <summary>§603: a "view" a file still holds is no word of the options any more: read as nothing, never written back.</summary>
    [Fact]
    public void AViewIsNoWordOfTheOptions()
    {
        var config = Configs.With(Configs.Holding(Line()));
        var json = config.ToJson().Replace("\"abbreviation\": \"BAR\"", "\"abbreviation\": \"BAR\", \"view\": \"table\"", StringComparison.Ordinal);
        using var written = JsonDocument.Parse(Configs.Repaired(json).ToJson());
        Assert.False(Configs.Find(written.RootElement, "sports")!.Value.TryGetProperty("view", out _));
    }

    [Theory]
    // Another league than the table's, an id no team has, a sport ESPN has but the table not.
    [InlineData("soccer", "rsa.1", "7081")]
    [InlineData("soccer", "esp.1", "83; drop")]
    [InlineData("rugby", "242041", "25934")]
    public void AHandEditedTeamThatIsNoneOfTheTablesIsNoTeam(string sport, string league, string id)
    {
        var line = Line(new SportsOptions { Sport = sport, League = league, Team = id, Name = "Somebody" });
        Assert.False(line.Sports!.IsSet);
        Assert.Equal(SetupField.Team, WidgetSetup.Needs(line));
    }

    [Fact]
    public void AFileFromBeforeTheWidgetAndAWordFromANewerBuildLoadWithEverythingElseKept()
    {
        // A folder of today's shape, its rss line and a sports line a newer build wrote with a field this one does not know.
        var folder = Configs.Holding(Line());
        var json = Configs.With(folder).ToJson().Replace("\"abbreviation\": \"BAR\"", "\"abbreviation\": \"BAR\", \"colour\": \"990000\"", StringComparison.Ordinal);
        var back = Configs.Repaired(json);
        Assert.Equal("Barcelona", Team(back)!.Name);

        // A line's record on a widget of another kind is dropped, the widget kept.
        var clock = ListKind.Clock.New();
        clock.Sports = SavedSports.Barcelona();
        clock.Normalize();
        Assert.Null(clock.Sports);

        // An older build's file: no sports anywhere, nothing changes.
        var older = Configs.With(Configs.Holding(ListKind.Clock.New()));
        Configs.RoundTrip(older, out var changed);
        Assert.False(changed);
    }

    [Fact]
    public void TheWidgetIsInEveryRegistry()
    {
        Assert.Same(WidgetInfos.Sports, WidgetInfos.Find("sports"));
        Assert.Equal(ListKind.Sports, ListWidgets.Find("sports")!.Kind);
        // §603: the score sized, Compact and Full; its three lists rows-based, as the line was.
        Assert.Equal([LineSize.Compact, LineSize.Full], ListWidgets.Of(ListKind.Sports).Forms!.Select(form => form.Size));
        Assert.All([ListKind.Sports, ListKind.SportsFixtures, ListKind.SportsTable, ListKind.SportsKnockout], kind =>
        {
            Assert.Equal(CellAct.Action, ListWidgets.Of(kind).Act);
            Assert.Equal(WidgetSource.Sports, ListWidgets.Of(kind).Drawing);
            Assert.Equal([WidgetAct.OpenWebsite], Karpo.Dock.Dock.ActsFor(kind));
        });
        Assert.All([ListKind.SportsFixtures, ListKind.SportsTable, ListKind.SportsKnockout], kind => Assert.Equal((2, 24), ListWidgets.Of(kind).Range));
        Assert.Same(FolderFaces.Sports, FolderFaces.Find("sports"));
        var preset = Presets.Find(Presets.SportsId)!;
        Assert.Equal(FolderFaces.SportsId, preset.Face!.Kind);
        Assert.Equal(["sports", "sportsFixtures"], preset.Items.Select(item => item.Kind));
        // In the add list beside the news and the markets: the ready-made folder on the bar's page, the line on a folder's.
        Assert.Contains(AddTree.On(WidgetType.Bar).SelectMany(section => AddTree.Picks(section.Rows)), pick => pick.Key == $"preset/{Presets.SportsId}");
        Assert.Contains(AddTree.On(WidgetType.List).SelectMany(section => AddTree.Picks(section.Rows)), pick => pick.Entry?.Id == "sports");
        Assert.Equal([WidgetAct.OpenWebsite], Karpo.Dock.Dock.ActsFor(ListKind.Sports));
        Assert.Equal([WidgetAct.OpenWebsite], Karpo.Dock.Dock.ActsFor(FolderFaces.Sports));
    }

    [Fact]
    public void TheTeamIsASearchRowThatKeepsWhatWasPicked()
    {
        var line = ListKind.Sports.New();
        var row = Assert.Single(OptionRows.Of(line), row => row.Key == OptionRows.SportsTeam);
        Assert.Equal(OptionRowKind.Search, row.Kind);
        Assert.NotNull(row.Finder);
        Assert.True(row.Finder!.Remote);
        Assert.Equal("", row.Text);

        var lakers = SportsAnswers.Teams(SavedSports.Answer("search-lakers"))[0];
        Assert.True(row.Pick!(new OptionFound(lakers.Name, SportsSearch.Detail(lakers), lakers)));
        Assert.Equal("basketball/nba/13", line.Sports!.Key);
        Assert.Equal(("Los Angeles Lakers", "LAL"), (line.Sports.Name, line.Sports.Abbreviation));
        Assert.Equal("Los Angeles Lakers · NBA", SportsSearch.Status(line.Sports));
        // The same pick again changes nothing; the menu's field now shows the team.
        var again = Assert.Single(OptionRows.Of(line), row => row.Key == OptionRows.SportsTeam);
        Assert.False(again.Pick!(new OptionFound(lakers.Name, "", lakers)));
        Assert.Equal("Los Angeles Lakers", again.Text);
    }
}
