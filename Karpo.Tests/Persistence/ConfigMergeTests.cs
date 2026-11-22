// ConfigMerge: the three-way merge of config.json texts a save does when the
// other app wrote the file since this one read it.

using System.Text.Json.Nodes;

namespace Karpo.Tests.Persistence;

public sealed class ConfigMergeTests
{
    /// <summary>The merge of three JSON texts (single quotes for readability), compact.</summary>
    static string Merge(string @base, string ours, string theirs) =>
        ConfigMerge.Merge(Json(@base), Json(ours), Json(theirs));

    static string Json(string text) => JsonNode.Parse(text.Replace('\'', '"'))!.ToJsonString();

    [Fact]
    public void EachSideKeepsWhatOnlyItChanged() =>
        Assert.Equal(Json("{'height':60,'opacity':30,'hide':'auto'}"),
            Merge("{'height':48,'opacity':70,'hide':'auto'}", "{'height':60,'opacity':70,'hide':'auto'}", "{'height':48,'opacity':30,'hide':'auto'}"));

    [Fact]
    public void AValueBothChangedIsTheSavers()
    {
        Assert.Equal(Json("{'height':60}"), Merge("{'height':48}", "{'height':60}", "{'height':72}"));
        Assert.Equal(Json("{'height':60}"), Merge("{'height':48}", "{'height':60}", "{'height':60}"));
    }

    [Fact]
    public void PropertiesAddedOrRemovedOnEitherSide() =>
        Assert.Equal(Json("{'a':1,'c':3}"), Merge("{'a':1,'b':2}", "{'a':1,'b':2,'c':3}", "{'a':1}"));

    /// <summary>
    /// start: Karpo's own Start menu is gone, and a save leaves its "startMenu" out. Where the file
    /// still has it as it was read, the key goes; where an older Karpo changed it since, the saver
    /// removed it and the other side changed it, and the saver wins, so it goes too.
    /// </summary>
    [Fact]
    public void TheOldStartMenuGoesWithASave()
    {
        Assert.Equal(Json("{'height':60}"),
            Merge("{'height':48,'startMenu':{'karpo':true}}", "{'height':60}", "{'height':48,'startMenu':{'karpo':true}}"));
        Assert.Equal(Json("{'height':60}"),
            Merge("{'height':48,'startMenu':{'karpo':true}}", "{'height':60}", "{'height':48,'startMenu':{'karpo':false}}"));
    }

    [Fact]
    public void DictionariesMergeKeyByKey() =>
        Assert.Equal(Json("{'trayIcons':{'states':{'x':'Taskbar','y':'Hidden'}}}"),
            Merge("{'trayIcons':{'states':{}}}", "{'trayIcons':{'states':{'x':'Taskbar'}}}", "{'trayIcons':{'states':{'y':'Hidden'}}}"));

    /// <summary>tray: the order icons were dragged into is a list of names, merged as one (TrayLayout).</summary>
    [Fact]
    public void TheTrayOrderMergesAsAList() =>
        Assert.Equal(Json("{'trayIcons':{'order':['c','a','b','d']}}"),
            Merge("{'trayIcons':{'order':['a','b']}}", "{'trayIcons':{'order':['c','a','b']}}", "{'trayIcons':{'order':['a','b','d']}}"));

    [Fact]
    public void IslandsMergeByIdAddsAndEdits() =>
        Assert.Equal(
            Json("{'islands':[{'id':'a','kind':'clock','clock':{'label':'Home'}},{'id':'b','kind':'start'},{'id':'c','kind':'weather'}]}"),
            Merge(
                "{'islands':[{'id':'a','kind':'clock','clock':{'label':''}},{'id':'b','kind':'start'}]}",
                "{'islands':[{'id':'a','kind':'clock','clock':{'label':'Home'}},{'id':'b','kind':'start'}]}",
                "{'islands':[{'id':'a','kind':'clock','clock':{'label':''}},{'id':'b','kind':'start'},{'id':'c','kind':'weather'}]}"));

    [Fact]
    public void TwoEditsOfOneIslandBothLand() =>
        Assert.Equal(
            Json("{'islands':[{'id':'a','clock':{'label':'Home','seconds':true}}]}"),
            Merge(
                "{'islands':[{'id':'a','clock':{'label':'','seconds':false}}]}",
                "{'islands':[{'id':'a','clock':{'label':'Home','seconds':false}}]}",
                "{'islands':[{'id':'a','clock':{'label':'','seconds':true}}]}"));

    /// <summary>A removal wins over an edit of the same island on the other side: no island comes back as a second copy.</summary>
    [Fact]
    public void ARemovedIslandStaysRemovedWhateverTheOtherSideDidToIt()
    {
        const string before = "{'islands':[{'id':'a','v':1},{'id':'b','v':1}]}";
        Assert.Equal(Json("{'islands':[{'id':'a','v':1}]}"), Merge(before, "{'islands':[{'id':'a','v':1}]}", "{'islands':[{'id':'a','v':1},{'id':'b','v':2}]}"));
        Assert.Equal(Json("{'islands':[{'id':'a','v':1}]}"), Merge(before, "{'islands':[{'id':'a','v':1},{'id':'b','v':2}]}", "{'islands':[{'id':'a','v':1}]}"));
    }

    [Fact]
    public void AReorderAndAnEditOfTheSameListBothLand() =>
        Assert.Equal(
            Json("{'islands':[{'id':'b'},{'id':'a','v':2}]}"),
            Merge("{'islands':[{'id':'a','v':1},{'id':'b'}]}", "{'islands':[{'id':'b'},{'id':'a','v':1}]}", "{'islands':[{'id':'a','v':2},{'id':'b'}]}"));

    [Fact]
    public void NestedListsMergeToo() =>
        Assert.Equal(
            Json("{'layouts':[{'id':'L','name':'Work','islands':[{'id':'a'},{'id':'x'},{'id':'y'}]}]}"),
            Merge(
                "{'layouts':[{'id':'L','name':'Main','islands':[{'id':'a'}]}]}",
                "{'layouts':[{'id':'L','name':'Work','islands':[{'id':'a'},{'id':'x'}]}]}",
                "{'layouts':[{'id':'L','name':'Main','islands':[{'id':'a'},{'id':'y'}]}]}"));

    [Fact]
    public void PinsMergeAsListsDividersIncluded() =>
        Assert.Equal(Json("{'pins':['x','a','|','b','|','c']}"),
            Merge("{'pins':['a','|','b']}", "{'pins':['a','|','b','|','c']}", "{'pins':['x','a','|','b']}"));

    [Fact]
    public void ARemovedDividerGoesOnce() =>
        Assert.Equal(Json("{'pins':['a','b','|','c','d']}"),
            Merge("{'pins':['a','|','b','|','c']}", "{'pins':['a','b','|','c']}", "{'pins':['a','|','b','|','c','d']}"));

    [Fact]
    public void AFoldersLinesMergeByTheIslandTheyHold() =>
        Assert.Equal(
            Json("{'lists':[[{'island':{'id':'a','height':3}},{'island':{'id':'c'}}],[{'island':{'id':'b'}},{'island':{'id':'d'}}]]}"),
            Merge(
                "{'lists':[[{'island':{'id':'a','height':1}}],[{'island':{'id':'b'}}]]}",
                "{'lists':[[{'island':{'id':'a','height':3}},{'island':{'id':'c'}}],[{'island':{'id':'b'}}]]}",
                "{'lists':[[{'island':{'id':'a','height':1}}],[{'island':{'id':'b'}},{'island':{'id':'d'}}]]}"));

    /// <summary>A line one side moved to another list stays moved, and a line the other added beside it stays too.</summary>
    [Fact]
    public void ALineMovedToAnotherListAndALineAddedBothStay() =>
        Assert.Equal(
            Json("{'lists':[[{'island':{'id':'a'}},{'island':{'id':'d'}}],[{'island':{'id':'c'}},{'island':{'id':'b'}}]]}"),
            Merge(
                "{'lists':[[{'island':{'id':'a'}},{'island':{'id':'b'}}],[{'island':{'id':'c'}}]]}",
                "{'lists':[[{'island':{'id':'a'}}],[{'island':{'id':'c'}},{'island':{'id':'b'}}]]}",
                "{'lists':[[{'island':{'id':'a'}},{'island':{'id':'d'}},{'island':{'id':'b'}}],[{'island':{'id':'c'}}]]}"));

    /// <summary>A line both sides moved, to different lists: once, where the saver put it; a line taken out on one side goes.</summary>
    [Fact]
    public void ALineBothMovedIsWhereTheSaverPutItOnce() =>
        Assert.Equal(
            Json("{'lists':[[],[{'island':{'id':'x'}},{'island':{'id':'a'}}],[{'island':{'id':'y'}}]]}"),
            Merge(
                "{'lists':[[{'island':{'id':'a'}},{'island':{'id':'z'}}],[{'island':{'id':'x'}}],[{'island':{'id':'y'}}]]}",
                "{'lists':[[{'island':{'id':'z'}}],[{'island':{'id':'x'}},{'island':{'id':'a'}}],[{'island':{'id':'y'}}]]}",
                "{'lists':[[],[{'island':{'id':'x'}}],[{'island':{'id':'y'}},{'island':{'id':'a'}}]]}"));

    /// <summary>A list one side added is there, with what the other side added to the lists before it.</summary>
    [Fact]
    public void AListAddedOnOneSideAndALineOnTheOtherBothStay() =>
        Assert.Equal(
            Json("{'lists':[[{'island':{'id':'a'}},{'island':{'id':'b'}}],[{'island':{'id':'n'}}]]}"),
            Merge(
                "{'lists':[[{'island':{'id':'a'}}]]}",
                "{'lists':[[{'island':{'id':'a'}}],[{'island':{'id':'n'}}]]}",
                "{'lists':[[{'island':{'id':'a'}},{'island':{'id':'b'}}]]}"));

    /// <summary>A folder's faces have no id: told apart by their kind, an option each side changed of another face both stay.</summary>
    [Fact]
    public void AFoldersFacesMergeByTheirKind() =>
        Assert.Equal(
            Json("{'faces':[{'kind':'clock','glyph':'x'},{'kind':'weather','place':'Bern'}]}"),
            Merge(
                "{'faces':[{'kind':'clock'},{'kind':'weather'}]}",
                "{'faces':[{'kind':'clock','glyph':'x'},{'kind':'weather'}]}",
                "{'faces':[{'kind':'clock'},{'kind':'weather','place':'Bern'}]}"));

    /// <summary>
    /// Regression: a folder's lists are lists of lines, which merged as the saver's whole: a line Karpo.exe
    /// --add-preset-layout or a hand edit put into a folder while the dock edited the same folder was lost.
    /// </summary>
    [Fact]
    public void ALineAnotherWriterAddedToAFolderSurvivesTheDocksEditOfIt()
    {
        var start = Config.Parse("{ }");
        start.Repair();
        Files.AddFolder(start, "g1", @"C:\tool.exe");
        var @base = start.ToJson();
        var writer = Config.Parse(@base);
        writer.Repair();
        var added = BarKind.Pin.New();
        added.Id = "fromCli";
        added.Pin!.Target = @"C:\cli.exe";
        Karpo.Core.Folders.Add(BarKind.FolderList, Files.Folder(writer, "g1")!, added.AsCell());
        var dock = Config.Parse(@base);
        dock.Repair();
        var line = Files.Folder(dock, "g1")!.Cells.Single().Island;
        line.Pin!.Name = "Tool";

        var merged = Config.Parse(ConfigMerge.Merge(@base, dock.ToJson(), writer.ToJson()));
        merged.Repair();

        var lines = Files.Folder(merged, "g1")!.Cells.Select(cell => cell.Island).ToList();
        Assert.Equal(2, lines.Count);
        Assert.Contains(lines, island => island.Id == "fromCli");
        Assert.Equal("Tool", lines.Single(island => island.Id != "fromCli").Pin!.Name);
    }

    [Fact]
    public void NoListYetBecomingAListOnBothSidesKeepsBoth() =>
        Assert.Equal(Json("{'pins':['a','b']}"), Merge("{'pins':null}", "{'pins':['a']}", "{'pins':['b']}"));

    [Fact]
    public void ListsWithNothingToTellEntriesApartAreTheSavers() =>
        Assert.Equal(Json("{'x':[[1],[2,3]]}"), Merge("{'x':[[1]]}", "{'x':[[1],[2,3]]}", "{'x':[[0]]}"));

    /// <summary>A whole config: what the settings app changed and what the dock changed meanwhile, both there after the merge.</summary>
    [Fact]
    public void TwoAppsEditsOfAWholeConfigBothSurvive()
    {
        var start = Config.Parse("""{ }""");
        start.Repair();
        Files.AddPin(start, @"C:\a.exe");
        Files.AddPin(start, @"C:\b.exe");
        Files.TrayStates(start)["k1"] = TrayState.Taskbar;
        var @base = start.ToJson();
        var settings = Config.Parse(@base);
        settings.TaskbarOpacity = 60;
        Files.TrayStates(settings)["k2"] = TrayState.Hidden;
        // The pins swapped round: b taken out and put back first.
        var b = Karpo.Tests.Core.Configs.Islands(settings).Single(island => island.Pin?.Target == @"C:\b.exe");
        Karpo.Tests.Core.Configs.Islands(settings).Remove(b);
        Karpo.Tests.Core.Configs.Islands(settings).Insert(Karpo.Tests.Core.Configs.Islands(settings).FindIndex(island => island.Pin?.Target == @"C:\a.exe"), b);
        var dock = Config.Parse(@base);
        Files.AddPin(dock, @"C:\dropped.exe");
        Files.AddFolder(dock, "g1", @"C:\tool.exe");

        var merged = Config.Parse(ConfigMerge.Merge(@base, settings.ToJson(), dock.ToJson()));
        merged.Repair();

        Assert.Equal(60, merged.TaskbarOpacity);
        Assert.Equal([@"C:\b.exe", @"C:\a.exe", @"C:\dropped.exe"], Files.Pins(merged));
        Assert.NotNull(Files.Folder(merged, "g1"));
        Assert.Equal(TrayState.Taskbar, Files.TrayStates(merged)["k1"]);
        Assert.Equal(TrayState.Hidden, Files.TrayStates(merged)["k2"]);
    }
}
