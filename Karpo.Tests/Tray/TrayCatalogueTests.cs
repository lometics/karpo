// The list of icons the dock writes next to the config (TrayCatalogue).

// Not Karpo.Tests.Tray: a local named "tray" elsewhere in the tests would shadow a namespace of that name.
namespace Karpo.Tests.TrayIcons;

public sealed class TrayCatalogueTests
{
    // --- The list for the settings app ------------------------------------------------------

    [Fact]
    public void TheListOfIconsReadsBackWhatTheDockWrote()
    {
        TrayCatalogueEntry[] icons =
        [
            new("spotify|0", "Spotify Premium", @"C:\Apps\Spotify.exe", "10094480254612307281", Promoted: true),
            new("{guid}", "", "", "", Promoted: false),
        ];

        var read = TrayCatalogue.Parse(TrayCatalogue.ToJson(icons));

        Assert.Equal(icons[0], read[0]);
        // No tooltip reads as the plain name.
        Assert.Equal(icons[1] with { Name = TrayCatalogue.NoName }, read[1]);
    }

    [Fact]
    public void AnEntryWithoutAKeyIsLeftOut()
    {
        var read = TrayCatalogue.Parse("""[ { "Key": "a|1", "Name": "App" }, { "Name": "no key" }, 7 ]""");

        Assert.Equal([new TrayCatalogueEntry("a|1", "App")], read);
    }

    [Fact]
    public void TheListIsReadFromTheFolderTheDockWritesIt()
    {
        var folder = Path.Combine(Path.GetTempPath(), "karpo-tray-catalogue-" + Guid.NewGuid().ToString("N"));
        try
        {
            TrayCatalogueEntry[] icons = [new("a|1", "App", @"C:\Apps\app.exe", "123", Promoted: true)];

            Assert.Empty(TrayCatalogue.Read(folder)!);
            TrayCatalogue.Write(folder, icons);

            Assert.Equal(icons, TrayCatalogue.Read(folder)!);
            Assert.True(File.Exists(Path.Combine(folder, TrayCatalogue.FileName)));
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Fact]
    public void TheListIsInTheConfigsFolder()
    {
        // The dock writes into Config.Folder and reads its order back from there; a test config
        // (KARPO_CONFIG_DIR) moves it.
        if (Environment.GetEnvironmentVariable("KARPO_CONFIG_DIR") is { Length: > 0 } isolated)
        {
            Assert.Equal(Path.GetFullPath(isolated), Path.GetFullPath(Config.Folder));
        }
    }
}
