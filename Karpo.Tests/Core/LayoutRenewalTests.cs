// A layout made new again (Karpo.exe --renew-layout, LayoutRenewal): a first install's layout under
// the old one's id, name and icon, holding the old one's pins; every other part of config.json as
// it was; the file kept beside it first, never over the first copy.

using System.Text.Json.Nodes;
using Karpo.Tests.Persistence;

namespace Karpo.Tests.Core;

[Collection(ConfigFile.Collection)]
public sealed class LayoutRenewalTests : IDisposable
{
    public LayoutRenewalTests() => Clean();

    public void Dispose() => Clean();

    private static void Clean()
    {
        Files.Reset();
        foreach (var file in Directory.GetFiles(Config.Folder, "config.before-renew-*"))
        {
            File.Delete(file);
        }
    }

    private static BarItem Pin(string target, string id)
    {
        var pin = BarKind.Pin.New();
        pin.Id = id;
        pin.Pin!.Target = target;
        return pin;
    }

    /// <summary>
    /// A config as the person keeps one: the default layout and "Mine", a dock in a different
    /// arrangement with two pins (one to a folder) and things a first install
    /// doesn't have, shown by a display; and an edit elsewhere in the file.
    /// </summary>
    private static (Config Config, TaskbarLayout Mine) Persons()
    {
        var config = Config.Parse("{}");
        config.Repair();
        var notes = Pin(@"C:\Notes", "pinNotes");
        var mine = config.AddLayout("Mine", [BarKind.Apps.New(), Pin(@"C:\Apps\Code.lnk", "pinCode"), BarKind.WidgetsBoard.New(), notes, BarKind.Start.New()], LayoutType.Dock, "\uE80F");
        mine.Size = 64;
        Assert.True(config.SelectLayout("D2", mine.Id));
        config.TaskbarOpacity = 55;
        config.Repair();
        return (config, mine);
    }

    /// <summary>The file's JSON without the layout <paramref name="id"/>: what renewing it must leave as it was.</summary>
    private static string AllBut(Config config, string id)
    {
        var json = JsonNode.Parse(config.ToJson())!;
        var layouts = json["layouts"]!.AsArray();
        layouts.Remove(layouts.First(layout => (string?)layout!["id"] == id));
        return json.ToJsonString();
    }

    private static List<(BarKind, BarSection)> Places(IEnumerable<BarItem> islands) =>
        [.. islands.Select(island => (island.Kind, island.Section))];

    [Fact]
    public void ItBecomesAFirstInstallsLayoutUnderItsOwnIdNameAndIcon()
    {
        var (config, mine) = Persons();
        var first = Config.FirstLayout();

        var layout = LayoutRenewal.Renew(config, mine.Id)!;

        Assert.Same(layout, config.Layout(mine.Id));
        Assert.Equal(mine.Id, layout.Id);
        Assert.Equal("Mine", layout.Name);
        Assert.Equal("\uE80F", layout.Icon);
        Assert.Equal(first.Type, layout.Type);
        Assert.Equal(first.Size, layout.Size);
        Assert.Equal(first.Show, layout.Show);
        // A first install's items, its pins (none in a test) the layout's own, before the open apps.
        var expected = Places(first.Islands.Where(island => island.Kind != BarKind.Pin)).ToList();
        var apps = expected.FindIndex(place => place.Item1 == BarKind.Apps);
        expected.InsertRange(apps, [(BarKind.Pin, expected[apps].Item2), (BarKind.Pin, expected[apps].Item2)]);
        Assert.Equal(expected, Places(layout.Islands));
        Assert.DoesNotContain(layout.Islands, island => island.Kind == BarKind.WidgetsBoard);
        // The folders the presets make, by the faces they wear (§427: a folder has no name).
        Assert.Equal(
            first.Islands.Where(island => island.Folder is not null).Select(island => string.Join(" ", island.Folder!.Faces.Select(face => face.Kind))),
            layout.Islands.Where(island => island.Folder is not null).Select(island => string.Join(" ", island.Folder!.Faces.Select(face => face.Kind))));
    }

    [Fact]
    public void ItsPinsStayAsTheyAreAndInTheirOrder()
    {
        var (config, mine) = Persons();

        var layout = LayoutRenewal.Renew(config, mine.Id)!;

        var pins = layout.Islands.Where(island => island.Kind == BarKind.Pin).ToList();
        Assert.Equal(["pinCode", "pinNotes"], pins.Select(pin => pin.Id));
        Assert.Equal([@"C:\Apps\Code.lnk", @"C:\Notes"], pins.Select(pin => pin.Pin!.Target));
        var at = layout.Islands.IndexOf(pins[0]);
        Assert.Same(pins[1], layout.Islands[at + 1]);
        Assert.Equal(BarKind.Apps, layout.Islands[at + 2].Kind);
    }

    [Fact]
    public void NothingElseInTheConfigChanges()
    {
        var (config, mine) = Persons();
        var before = AllBut(config, mine.Id);
        var at = config.Layouts.IndexOf(mine);

        LayoutRenewal.Renew(config, mine.Id);
        config.Repair();

        Assert.Equal(before, AllBut(config, mine.Id));
        Assert.Equal(at, config.Layouts.FindIndex(layout => layout.Id == mine.Id));
        Assert.Equal(mine.Id, config.Screens.Single(screen => screen.Id == "D2").ActiveLayoutId);
    }

    [Fact]
    public void ALayoutItDoesntHaveChangesNothing()
    {
        var (config, _) = Persons();
        var before = config.ToJson();

        Assert.Null(LayoutRenewal.Renew(config, "gone"));
        Assert.Equal(before, config.ToJson());
    }

    /// <summary>In the file: the file as it was kept first, the layout renewed, the rest as it was.</summary>
    [Fact]
    public void InTheFileTheOldFileIsKeptBesideItFirst()
    {
        var (persons, mine) = Persons();
        persons.Save();
        var text = Files.Text();
        var before = AllBut(Config.Load(), mine.Id);

        var done = LayoutRenewal.RenewInFile(mine.Id);

        Assert.Equal(Path.Combine(Config.Folder, $"config.before-renew-{mine.Id}.json"), done.Backup);
        Assert.Equal(text, File.ReadAllText(done.Backup));
        Assert.Equal(2, done.PinsKept);
        Assert.Equal(5, done.ItemsBefore);
        var file = Config.Load();
        Assert.Equal(before, AllBut(file, mine.Id));
        Assert.Contains(file.Layout(mine.Id)!.Islands, island => island.Kind == BarKind.Start && island.Folder is null);
        Assert.Equal(LayoutType.Taskbar, file.Layout(mine.Id)!.Type);

        // Again: the first copy, the layout as the person made it, is not written over.
        var again = LayoutRenewal.RenewInFile(mine.Id);
        Assert.NotEqual(done.Backup, again.Backup);
        Assert.Equal(text, File.ReadAllText(done.Backup));
    }

    [Fact]
    public void InTheFileALayoutItDoesntHaveWritesNothing()
    {
        Persons().Config.Save();
        var text = Files.Text();

        var error = Assert.Throws<KeyNotFoundException>(() => LayoutRenewal.RenewInFile("gone"));

        Assert.Contains("Mine", error.Message);
        Assert.Equal(text, Files.Text());
        Assert.Empty(Directory.GetFiles(Config.Folder, "config.before-renew-*"));
    }
}
