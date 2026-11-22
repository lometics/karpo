// The dock's saves (Karpo.Dock\ConfigSaves.cs) against a config.json that won't take them: another program
// holding it, a newer Karpo's file, a file not read yet. A change the bars show is in the file as soon as it
// can be, without another edit, or the person is told.

using Karpo.Dock;

namespace Karpo.Tests.Persistence;

[Collection(ConfigFile.Collection)]
public sealed class ConfigSavesTests : IDisposable
{
    public ConfigSavesTests() => Files.Reset();

    public void Dispose() => Files.Reset();

    private static FileStream Held() => new(Config.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

    /// <summary>
    /// Regression: a change the dock applied while another program held config.json was shown and never
    /// written — after a restart it was gone. It is kept, and the next try writes it, with no other edit.
    /// </summary>
    [Fact]
    public void AChangeTheFileDidNotTakeLandsOnceItIsFree()
    {
        var config = Files.Start();
        var saves = new ConfigSaves();
        var next = config.Clone();
        next.TaskbarOpacity = 35;

        using (Held())
        {
            Assert.False(saves.Save(next));
        }
        Assert.True(saves.Unsaved);
        Assert.Equal(ConfigSaves.FirstMs, saves.NextTryMs);
        Assert.NotEqual(35, Config.Load().TaskbarOpacity);

        Assert.True(saves.Again(next));
        Assert.False(saves.Unsaved);
        Assert.Null(saves.NextTryMs);
        Assert.Equal(35, Config.Load().TaskbarOpacity);
    }

    /// <summary>Tried again less and less often, a minute apart at most; the person told outside edit mode once it keeps failing, and no more once it saves.</summary>
    [Fact]
    public void ItIsTriedAgainLessOftenAndToldOnceItKeepsFailing()
    {
        var config = Files.Start();
        var saves = new ConfigSaves();
        config.TaskbarOpacity = 22;
        using (Held())
        {
            Assert.False(saves.Save(config));
            Assert.Equal(1000u, saves.NextTryMs);
            Assert.False(saves.Telling);
            Assert.Empty(Dock.Dock.SavesRows(saves));
            Assert.False(saves.Again(config));
            Assert.Equal(2000u, saves.NextTryMs);
            Assert.False(saves.Again(config));
            Assert.Equal(4000u, saves.NextTryMs);
            Assert.True(saves.Telling);
        }
        var rows = Dock.Dock.SavesRows(saves);
        Assert.Equal(Dock.Dock.CMD_SAVE_AGAIN, rows[0].Id);
        Assert.True(rows[0].Enabled);
        Assert.Contains(saves.Problem!, rows[0].Detail, StringComparison.Ordinal);
        Assert.Equal(ConfigSaves.LongestMs, ConfigSaves.Delay(30));

        Assert.True(saves.Again(config));
        Assert.False(saves.Telling);
        Assert.Empty(Dock.Dock.SavesRows(saves));
    }

    /// <summary>A newer Karpo's file is never written over: said at once, plainly, and not tried again on a timer.</summary>
    [Fact]
    public void ANewerKarposFileIsToldAtOnceAndNeverWrittenOver()
    {
        var newer = $$"""{ "version": {{Config.Shape + 1}}, "onlineUpdateMinutes": 40, "somethingNew": true }""";
        File.WriteAllText(Config.FilePath, newer);
        var config = Config.Load();
        var saves = new ConfigSaves();
        config.TaskbarOpacity = 22;

        Assert.False(saves.Save(config));

        Assert.True(saves.Newer);
        Assert.True(saves.Telling);
        Assert.Null(saves.NextTryMs);
        Assert.Equal(newer, Files.Text());
        var row = Dock.Dock.SavesRows(saves)[0];
        Assert.False(row.Enabled);
        Assert.Equal(S.DockFaultNewer, row.Detail);
    }

    /// <summary>
    /// Regression: a config.json held as Karpo started left the defaults standing until the file next
    /// changed, and a reload that met it held was dropped. It is read again on a timer until it reads;
    /// the defaults' saves meanwhile wait (no failure, nothing told) and are never written over it.
    /// </summary>
    [Fact]
    public void AFileHeldAtStartIsReadAgainUntilItReadsAndTheDefaultsAreNeverWritten()
    {
        Files.Start(start => Files.AddPin(start, "kept"));
        var before = Files.Text();
        var saves = new ConfigSaves();
        Config config;
        using (Held())
        {
            config = Config.Load();
            Assert.True(ConfigSaves.ReadAgain(ConfigReload.Unchanged, config));
            // The dock puts its display into the defaults and saves, as at every start.
            config.AddDisplay("D1", "Dell");
            Assert.False(saves.Save(config));
            Assert.True(saves.Waiting);
            Assert.False(saves.Telling);
            Assert.Null(saves.NextTryMs);
            // A read again meets it held still: read again later, a little later each time.
            var held = config.Reload(out _);
            Assert.Equal(ConfigReload.Busy, held);
            Assert.True(ConfigSaves.ReadAgain(held, config));
        }
        Assert.Equal(ConfigSaves.ReadFirstMs, ConfigSaves.ReadDelay(1));
        Assert.True(ConfigSaves.ReadDelay(2) > ConfigSaves.ReadDelay(1));
        Assert.Equal(ConfigSaves.ReadLongestMs, ConfigSaves.ReadDelay(20));

        var read = config.Reload(out var fresh);

        Assert.Equal(ConfigReload.Changed, read);
        Assert.False(ConfigSaves.ReadAgain(read, fresh));
        Assert.Equal(["kept"], Files.Pins(fresh));
        Assert.True(saves.Again(fresh));
        Assert.Equal(["kept"], Files.Pins(Config.Load()));
    }

    /// <summary>Regression: the dock saved config.json in three places of its own, each writing a failure to a console it doesn't have. Every save goes through ConfigSaves now.</summary>
    [Fact]
    public void EverySaveOfTheDocksGoesThroughConfigSaves()
    {
        var saving = new System.Text.RegularExpressions.Regex(@"\b(config|next|fresh|written)\.Save\(\)");
        var found = RepoSource.CSharp("Karpo.Dock")
            .Where(path => !path.EndsWith("ConfigSaves.cs", StringComparison.OrdinalIgnoreCase))
            .Where(path => saving.IsMatch(File.ReadAllText(path)))
            .Select(Path.GetFileName)
            .ToList();
        Assert.Empty(found);
    }
}
