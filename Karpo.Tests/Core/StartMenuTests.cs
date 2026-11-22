// §641: what the Start widgets know without Windows (Karpo.Core\StartMenu.cs): what was opened and first seen, the A–Z
// and the categories, the pins and their changes, what Recommended lists, and what a line keeps.

using System.Globalization;

namespace Karpo.Tests.Core;

public sealed class StartMenuTests
{
    private static StartApp App(string name, string? id = null) => new(name, StartApp.AppsFolder + (id ?? name.Replace(' ', '.')));

    private static readonly DateTime Now = new(2026, 11, 9, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void AnAppIsNewOnlyOnceKarpoKnewThePcWithoutItAndForAWeek()
    {
        var uses = new StartUses();
        Assert.True(uses.Saw(["a", "b"], Now.AddDays(-30)));
        Assert.False(uses.IsNew("a", Now.AddDays(-30)), "the first look sees every app long ago");
        Assert.False(uses.Saw(["a", "b"], Now.AddDays(-20)));
        Assert.True(uses.Saw(["a", "b", "c"], Now.AddDays(-2)));
        Assert.True(uses.IsNew("c", Now));
        Assert.False(uses.IsNew("c", Now.AddDays(6)));
        Assert.False(uses.IsNew("a", Now));

        // What is written reads back the same, a line that does not read left out.
        var again = StartUses.Read(uses.Write() + "broken\tline\n");
        Assert.True(again.Looked);
        Assert.Equal(uses.Of("c"), again.Of("c"));
        Assert.True(again.IsNew("c", Now));
        Assert.Null(again.Of("broken"));
        Assert.False(StartUses.Read(null).Looked);
    }

    [Fact]
    public void AllAppsHeadsAreSignsThenDigitsThenTheLettersFolded()
    {
        var groups = StartLetters.Group([App("apple"), App("7-Zip"), App("&Co"), App("Écran"), App("Edge"), App("Zoom")]);
        Assert.Equal(["&", "#", "A", "E", "Z"], groups.Select(group => group.Head));
        Assert.Equal(["Écran", "Edge"], groups[3].Apps.Select(app => app.Name));
    }

    [Fact]
    public void ACategoryOfFewerThanThreeFoldsIntoOther()
    {
        var groups = StartCategories.Group([App("Xbox"), App("Steam"), App("Minecraft Launcher"), App("Discord"), App("Word"), App("Excel"), App("Outlook"), App("Unknown Thing")]);
        Assert.Equal([StartCategory.Productivity, StartCategory.Games, StartCategory.Other], groups.Select(group => group.Category));
        Assert.Equal(["Discord", "Unknown Thing"], groups[^1].Apps.Select(app => app.Name));
        Assert.Equal(StartCategory.Utilities, StartCategories.Of("Task Manager"));
        Assert.Equal(StartCategory.Creativity, StartCategories.Of("Paint"));
        Assert.Equal(StartCategory.Other, StartCategories.Of("Painter's Helper"));
    }

    [Fact]
    public void ThePinsAreThePcsUsualOnesUntilChangedAndChangeAsStartsDo()
    {
        StartApp[] installed = [App("Notepad", "Microsoft.WindowsNotepad_8wekyb3d8bbwe!App"), App("Edge", "MSEdge"), App("Other")];
        var usual = StartPins.Defaults(installed);
        Assert.Equal([installed[1].Target, installed[0].Target], usual);
        Assert.Equal(usual, StartPins.Of(null, installed));
        Assert.Equal(["x"], StartPins.Of(new StartOptions { Pins = ["x"] }, installed));
        // A pin of an app gone is not shown.
        Assert.Equal([installed[1]], StartPins.Apps([installed[1].Target, "gone"], installed));

        List<string> pins = ["a", "b", "c"];
        Assert.Equal(["a", "b", "c", "d"], StartPins.Pin(pins, "d"));
        Assert.Equal(["a", "b", "c"], StartPins.Pin(pins, "B"));
        Assert.Equal(["a", "c"], StartPins.Unpin(pins, "b"));
        Assert.Equal(["c", "a", "b"], StartPins.ToFront(pins, "c"));
        Assert.Equal(["b", "c", "a"], StartPins.Move(pins, "a", 2));
        Assert.Equal(["b", "a", "c"], StartPins.Move(pins, "a", 1));
        Assert.Equal(["a", "b", "c"], StartPins.Move(pins, "z", 0));
    }

    [Fact]
    public void ALineKeepsItsOwnPartAndNoDefault()
    {
        var all = new StartOptions { Pins = ["a", "a", " "], View = StartView.Grid, Places = [StartPlace.Downloads, StartPlace.Settings, StartPlace.Settings], Files = false, NewApps = true };
        Assert.Equal(["a"], all.Normalized(ListKind.StartPins)!.Pins);
        Assert.Null(all.Normalized(ListKind.StartPins)!.View);
        Assert.Equal(StartView.Grid, all.Normalized(ListKind.StartApps)!.View);
        Assert.Null(new StartOptions { View = StartView.Category }.Normalized(ListKind.StartApps));
        Assert.Equal([StartPlace.Settings, StartPlace.Downloads], all.Normalized(ListKind.StartProfile)!.Places);
        Assert.Empty(new StartOptions { Places = [] }.Normalized(ListKind.StartProfile)!.Places!);
        var recommended = all.Normalized(ListKind.StartRecommended)!;
        Assert.Equal((false, (bool?)null), (recommended.Files!.Value, recommended.NewApps));
        Assert.Equal(StartPlaces.Defaults, StartPlaces.Of(null));
    }

    /// <summary>A config holding the Start group on its first layout's bar, and the owner and the folder's id.</summary>
    private static (Config Config, string Owner, BarItem Folder) WithStart()
    {
        var folder = Presets.Folder(Presets.Find(Presets.StartId)!, new PresetMachine(false, []));
        var config = new Config { Layouts = [new TaskbarLayout { Id = TaskbarLayouts.DefaultId, Islands = [folder] }] };
        return (config, Config.LayoutOwner(config.Layouts[0].Id), folder);
    }

    private static string IdOf(BarItem folder, ListKind kind) => $"{folder.Id}/{folder.Folder!.Cells.First(cell => cell.Island.List == kind).Island.Id}";

    [Fact]
    public void AllAppsPinsToThePinnedAppsBesideItAndSetsItsView()
    {
        var (config, owner, folder) = WithStart();
        StartApp[] installed = [App("Edge", "MSEdge"), App("Spotify")];
        var apps = IdOf(folder, ListKind.StartApps);
        var pins = (CellItem)config.FindIsland(owner, IdOf(folder, ListKind.StartPins))!;

        Assert.Same(pins, StartEdits.PinsOf(config, owner, apps));
        Assert.True(StartEdits.Apply(config, owner, apps, StartEdits.Pin, installed[1].Target, installed));
        Assert.Equal([installed[0].Target, installed[1].Target], pins.Start!.Pins);
        Assert.False(StartEdits.Apply(config, owner, apps, StartEdits.Pin, installed[1].Target, installed), "pinned already");
        Assert.True(StartEdits.Apply(config, owner, apps, StartEdits.Front, installed[1].Target, installed));
        Assert.Equal([installed[1].Target, installed[0].Target], pins.Start!.Pins);
        Assert.True(StartEdits.Apply(config, owner, IdOf(folder, ListKind.StartPins), StartEdits.Move, installed[1].Target, installed, index: 1));
        Assert.Equal([installed[0].Target, installed[1].Target], pins.Start!.Pins);
        Assert.True(StartEdits.Apply(config, owner, apps, StartEdits.Unpin, installed[0].Target, installed));
        Assert.Equal([installed[1].Target], pins.Start!.Pins);

        Assert.True(StartEdits.Apply(config, owner, apps, StartEdits.View, "grid", installed));
        Assert.Equal(StartView.Grid, ((CellItem)config.FindIsland(owner, apps)!).Start!.View);
        Assert.False(StartEdits.Apply(config, owner, apps, StartEdits.View, "grid", installed));
        // It survives the file.
        var read = Config.Parse(config.ToJson());
        Assert.Equal(StartView.Grid, ((CellItem)read.FindIsland(owner, apps)!).Start!.View);
        Assert.Equal([installed[1].Target], ((CellItem)read.FindIsland(owner, IdOf(folder, ListKind.StartPins))!).Start!.Pins);

        // A folder with no pinned apps has nothing to pin to.
        folder.Folder!.Lists = [[.. folder.Folder.Cells.Where(cell => cell.Island.List != ListKind.StartPins)]];
        Assert.Null(StartEdits.PinsOf(config, owner, apps));
        Assert.False(StartEdits.Apply(config, owner, apps, StartEdits.Pin, installed[0].Target, installed));
    }

    [Fact]
    public void RecommendedIsTheNewestFilesAndAppsAsItsLineSays()
    {
        StartRecommendation[] files = [new("a.docx", @"C:\a.docx", Now.AddHours(-1), false), new("b.pdf", @"C:\b.pdf", Now.AddDays(-3), false)];
        StartRecommendation[] apps = [new("Clipchamp", "x", Now.AddHours(-2), true)];
        Assert.Equal(["a.docx", "Clipchamp", "b.pdf"], StartRecommendations.Of(null, files, apps).Select(item => item.Name));
        Assert.Equal(["Clipchamp"], StartRecommendations.Of(new StartOptions { Files = false }, files, apps).Select(item => item.Name));
        Assert.Equal(["a.docx", "b.pdf"], StartRecommendations.Of(new StartOptions { NewApps = false }, files, apps).Select(item => item.Name));
        Assert.Single(StartRecommendations.Of(null, files, apps, most: 1));
    }

    [Fact]
    public void WhenAFileWasOpenedIsTheTimeTodayTheDayThisWeekElseTheDate()
    {
        static string Time(DateTime at) => at.ToString("HH:mm", CultureInfo.InvariantCulture);
        static string Day(DayOfWeek day) => day.ToString();
        static string Date(DateOnly day) => day.ToString("d.M.yyyy", CultureInfo.InvariantCulture);
        var now = new DateTime(2026, 11, 11, 15, 0, 0, DateTimeKind.Local).ToUniversalTime();
        var today = new DateTime(2026, 11, 11, 9, 5, 0, DateTimeKind.Local).ToUniversalTime();
        var monday = new DateTime(2026, 11, 9, 9, 5, 0, DateTimeKind.Local).ToUniversalTime();
        var longAgo = new DateTime(2026, 10, 1, 9, 5, 0, DateTimeKind.Local).ToUniversalTime();
        Assert.Equal("09:05", StartRecommendations.When(today, now, Time, Day, Date));
        Assert.Equal("Monday 09:05", StartRecommendations.When(monday, now, Time, Day, Date));
        Assert.Equal("1.10.2026", StartRecommendations.When(longAgo, now, Time, Day, Date));
    }

    [Fact]
    public void AnAppsProgramIsItsPathWhereTheAppsFolderNamesOne()
    {
        Assert.Null(App("Edge", "MSEdge").Program);
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"7-Zip\7zFM.exe"),
            App("7-Zip", @"{6D809377-6AF0-444B-8957-A3773F02200E}\7-Zip\7zFM.exe").Program);
        Assert.Equal(@"C:\Tools\tool.exe", App("Tool", @"C:\Tools\tool.exe").Program);
    }
}
