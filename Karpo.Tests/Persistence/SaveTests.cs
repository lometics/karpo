// Config.Save and Config.Reload with two writers on one config.json: the dock and
// the settings app each hold a config, edit it and save, in any order.

namespace Karpo.Tests.Persistence;

[Collection(ConfigFile.Collection)]
public sealed class SaveTests : IDisposable
{
    private static readonly DateTime LongAgo = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public SaveTests() => Files.Reset();

    public void Dispose() => Files.Reset();

    [Fact]
    public void TwoConfigsSavingInTurnLoseNothing()
    {
        Files.Start();
        var dock = Config.Load();
        var settings = Config.Load();

        Files.AddPin(dock, @"C:\dock.exe");
        dock = dock.Save();
        settings.TaskbarOpacity = 60;
        var saved = settings.Save();

        var file = Config.Load();
        Assert.Equal(60, file.TaskbarOpacity);
        Assert.Equal([@"C:\dock.exe"], Files.Pins(file));
        // What Save hands back holds the other app's change too.
        Assert.Equal(60, saved.TaskbarOpacity);
        Assert.Equal([@"C:\dock.exe"], Files.Pins(saved));
        Assert.Equal(file.ToJson(), saved.ToJson());
    }

    /// <summary>A config kept after its save merged (Save's result ignored) still never undoes the other's change.</summary>
    [Fact]
    public void AConfigKeptAfterAMergedSaveLosesNothingLater()
    {
        Files.Start();
        var dock = Config.Load();
        var settings = Config.Load();

        Files.AddPin(dock, "x");
        dock.Save();
        settings.OnlineUpdateMinutes = 60;
        settings.Save();
        settings.TaskbarOpacity = 30;
        settings.Save();

        var file = Config.Load();
        Assert.Equal(["x"], Files.Pins(file));
        Assert.Equal(60, file.OnlineUpdateMinutes);
        Assert.Equal(30, file.TaskbarOpacity);
    }

    /// <summary>
    /// Regression (found by the stress test): a config kept after a save that merged the other app's
    /// changes in kept its old base, so taking away something it had added itself looked like the
    /// other app adding it, and the removal was undone.
    /// </summary>
    [Fact]
    public void ARemovalAfterAMergedSaveSticks()
    {
        Files.Start();
        var dock = Config.Load();
        var settings = Config.Load();
        settings.TaskbarOpacity = 60;
        settings.Save();

        Files.AddPin(dock, "mine");
        dock.Save();
        Files.RemovePin(dock, "mine");
        dock.Save();

        var file = Config.Load();
        Assert.DoesNotContain("mine", Files.Pins(file));
        Assert.Equal(60, file.TaskbarOpacity);
    }

    [Fact]
    public void InterleavedSavesOfTwoConfigsAllArrive()
    {
        Files.Start();
        var dock = Config.Load();
        var settings = Config.Load();

        for (var round = 0; round < 10; round++)
        {
            Files.AddPin(dock, $"dock-{round}");
            dock = dock.Save();
            Files.TrayStates(settings)[$"settings-{round}"] = round % 2 == 0 ? TrayState.Taskbar : TrayState.Overflow;
            Files.AddPin(settings, $"settings-{round}", first: true);
            settings = settings.Save();
        }

        var file = Config.Load();
        var pins = Files.Pins(file);
        Assert.Equal(20, pins.Count);
        Assert.Equal(Enumerable.Range(0, 10).Select(round => $"dock-{round}"), pins.Where(pin => pin.StartsWith("dock-")));
        Assert.Equal(Enumerable.Range(0, 10).Reverse().Select(round => $"settings-{round}"), pins.Where(pin => pin.StartsWith("settings-")));
        Assert.Equal(10, Files.TrayStates(file).Count);
    }

    [Fact]
    public void AValueBothChangedIsTheLastSavers()
    {
        Files.Start();
        var dock = Config.Load();
        var settings = Config.Load();

        dock.TaskbarOpacity = 40;
        dock.Save();
        settings.TaskbarOpacity = 72;
        settings.Save();

        Assert.Equal(72, Config.Load().TaskbarOpacity);
    }

    /// <summary>The dock's pattern: a clone edited and saved merges against the file the original came from.</summary>
    [Fact]
    public void ACloneSavesAgainstTheFileItsOriginalCameFrom()
    {
        Files.Start();
        var dock = Config.Load();
        var settings = Config.Load();

        Files.AddPin(settings, "s");
        settings.Save();
        var next = dock.Clone();
        next.TaskbarOpacity = 66;
        next.Save();

        var file = Config.Load();
        Assert.Equal(["s"], Files.Pins(file));
        Assert.Equal(66, file.TaskbarOpacity);
    }

    /// <summary>The dock's file watcher must not take its own save for someone else's (and save again, in a loop).</summary>
    [Fact]
    public void AnOwnSaveReloadsAsUnchanged()
    {
        var config = Files.Start();
        config.TaskbarOpacity = 50;
        config = config.Save();

        Assert.Equal(ConfigReload.Unchanged, config.Reload(out var fresh));
        Assert.Same(config, fresh);
    }

    [Fact]
    public void AnotherAppsSaveReloadsWithUnsavedEditsOnTop()
    {
        Files.Start();
        var dock = Config.Load();
        var settings = Config.Load();
        // An edit not saved yet, as the bar's size is while its edge is dragged: saved when the drag ends.
        dock.TaskbarOpacity = 71;
        Files.AddPin(settings, "p");
        settings.Save();

        Assert.Equal(ConfigReload.Changed, dock.Reload(out var fresh));

        Assert.Equal(71, fresh.TaskbarOpacity);
        Assert.Equal(["p"], Files.Pins(fresh));
        fresh.Save();
        Assert.Equal(71, Config.Load().TaskbarOpacity);
        Assert.Equal(["p"], Files.Pins(Config.Load()));
    }

    [Fact]
    public void TheSameSettingsWrittenDifferentlyReloadAsUnchanged()
    {
        var config = Files.Start();
        File.WriteAllText(Config.FilePath, Files.Text().Replace("\n", "\r\n"));

        Assert.Equal(ConfigReload.Unchanged, config.Reload(out _));
        Assert.Equal(ConfigReload.Unchanged, config.Reload(out _));
    }

    [Fact]
    public void ADeletedFileReloadsAsDefaults()
    {
        var config = Files.Start(start => start.TaskbarOpacity = 90);
        File.Delete(Config.FilePath);

        Assert.Equal(ConfigReload.Changed, config.Reload(out var fresh));
        Assert.Equal(85, fresh.TaskbarOpacity);
    }

    /// <summary>A hand edit with a mistake: kept, never used as the backup, and replaced by the next save.</summary>
    [Fact]
    public void ABrokenFileReloadsAsBrokenAndTheNextSaveKeepsItAside()
    {
        var config = Files.Start(start => start.TaskbarOpacity = 40);
        config.TaskbarOpacity = 42;
        config = config.Save();
        const string broken = "{ \"opacity\": 40, oops";
        File.WriteAllText(Config.FilePath, broken);

        Assert.Equal(ConfigReload.Broken, config.Reload(out var same));
        Assert.Same(config, same);
        config.TaskbarOpacity = 44;
        config.Save();

        Assert.Equal(44, Config.Load().TaskbarOpacity);
        Assert.Equal(broken, File.ReadAllText(Assert.Single(Files.Broken())));
        Assert.Equal(40, Config.Parse(File.ReadAllText(Config.BackupPath)).TaskbarOpacity);
    }

    [Fact]
    public void ASaveKeepsTheVersionBeforeAsTheBackup()
    {
        var config = Files.Start();
        config.TaskbarOpacity = 40;
        config = config.Save();
        config.TaskbarOpacity = 50;
        config.Save();

        Assert.Equal(40, Config.Parse(File.ReadAllText(Config.BackupPath)).TaskbarOpacity);
        Assert.Equal(50, Config.Load().TaskbarOpacity);
        Assert.Empty(Files.Temporary());
    }

    [Fact]
    public void ASaveThatChangesNothingDoesntWrite()
    {
        var config = Files.Start();
        File.SetLastWriteTimeUtc(Config.FilePath, LongAgo);

        config.Save();

        Assert.Equal(LongAgo, File.GetLastWriteTimeUtc(Config.FilePath));
    }

    /// <summary>A reader that doesn't share deletion (another program, a virus scanner) makes the replace wait, not fail.</summary>
    [Fact]
    public async Task AFileHeldOpenForAMomentIsWaitedFor()
    {
        var config = Files.Start();
        var held = new FileStream(Config.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var release = Task.Run(async () =>
        {
            await Task.Delay(150);
            held.Dispose();
        });

        config.TaskbarOpacity = 56;
        config.Save();
        await release;

        Assert.Equal(56, Config.Load().TaskbarOpacity);
    }

    /// <summary>A config made in code (not loaded) has nothing to merge against: its save replaces the file.</summary>
    [Fact]
    public void ANewConfigReplacesTheFile()
    {
        Files.Start(start => Files.AddPin(start, "kept?"));
        var fresh = Config.Parse("{}");
        fresh.Repair();
        fresh.TaskbarOpacity = 30;

        fresh.Save();

        var file = Config.Load();
        Assert.Equal(30, file.TaskbarOpacity);
        Assert.Empty(Files.Pins(file));
    }
}
