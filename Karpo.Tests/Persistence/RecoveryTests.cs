// Config.Load when config.json isn't what a finished save leaves: broken by hand
// or by another program, cut off by a crash, or held by someone else.

namespace Karpo.Tests.Persistence;

[Collection(ConfigFile.Collection)]
public sealed class RecoveryTests : IDisposable
{
    public RecoveryTests() => Files.Reset();

    public void Dispose() => Files.Reset();

    public static TheoryData<string> BrokenTexts => new()
    {
        "{ \"opacity\": 40, oops",
        "{ \"opacity\": 72, \"pins\": [ \"C:\\\\Tools\\\\a.exe\", \"C:\\\\To",
        "",
        "   \r\n",
        new string('\0', 512),
        "null",
        "[ ]",
    };

    /// <summary>The last good version (config.json.bak) takes the place of a file that doesn't read; the broken text is kept, byte for byte.</summary>
    [Theory]
    [MemberData(nameof(BrokenTexts))]
    public void ABrokenFileGivesWayToTheBackup(string broken)
    {
        var good = Files.Start(start => start.TaskbarOpacity = 40);
        good.TaskbarOpacity = 64;
        good.Save();
        File.WriteAllText(Config.FilePath, broken);

        var config = Config.Load();

        Assert.Equal(40, config.TaskbarOpacity);
        Assert.NotNull(config.Recovery);
        Assert.Equal(broken, File.ReadAllText(Assert.Single(Files.Broken())));
        Assert.Equal(config.ToJson(), Files.Text());
        Assert.Equal(ConfigReload.Unchanged, config.Reload(out _));
    }

    [Fact]
    public void ABrokenFileWithoutBackupGivesDefaultsAndIsKept()
    {
        Directory.CreateDirectory(Config.Folder);
        File.WriteAllText(Config.FilePath, "{ \"opacity\": 40, oops");

        var config = Config.Load();

        Assert.Equal(85, config.TaskbarOpacity);
        Assert.NotNull(config.Recovery);
        Assert.Equal("{ \"opacity\": 40, oops", File.ReadAllText(Assert.Single(Files.Broken())));
        Assert.False(File.Exists(Config.FilePath));
    }

    /// <summary>A crash after the temporary file was written and before it replaced config.json: the old file stands, the leftover goes.</summary>
    [Fact]
    public void ACrashBeforeTheReplaceLeavesTheOldFileAndTheLeftoverIsCleanedUp()
    {
        Files.Start(start => start.TaskbarOpacity = 40);
        var leftover = Config.FilePath + ".4242-deadbeef.tmp";
        File.WriteAllText(leftover, "{ \"opacity\": 50, \"pi");
        File.SetLastWriteTimeUtc(leftover, DateTime.UtcNow.AddMinutes(-5));

        var config = Config.Load();

        Assert.Equal(40, config.TaskbarOpacity);
        Assert.Null(config.Recovery);
        Assert.False(File.Exists(leftover));
    }

    /// <summary>A temporary file of a save that may still be going on (a young one) is left alone.</summary>
    [Fact]
    public void AYoungTemporaryFileIsLeftAlone()
    {
        Files.Start();
        var young = Config.FilePath + ".4242-cafebabe.tmp";
        File.WriteAllText(young, "{");

        Config.Load().Save();

        Assert.True(File.Exists(young));
    }

    /// <summary>
    /// A crash between ReplaceFile's two renames: no config.json, the version before as the backup, the
    /// new one whole in its temporary file. The save is finished, not undone.
    /// </summary>
    [Fact]
    public void ASaveCutOffBetweenItsRenamesIsFinished()
    {
        Directory.CreateDirectory(Config.Folder);
        var before = Config.Parse("{ \"onlineUpdateMinutes\": 40 }");
        before.Repair();
        File.WriteAllText(Config.BackupPath, before.ToJson());
        File.SetLastWriteTimeUtc(Config.BackupPath, DateTime.UtcNow.AddMinutes(-10));
        var after = Config.Parse("{ \"onlineUpdateMinutes\": 50 }");
        after.Repair();
        var temporary = Config.FilePath + ".4242-0badf00d.tmp";
        File.WriteAllText(temporary, after.ToJson());
        File.SetLastWriteTimeUtc(temporary, DateTime.UtcNow.AddMinutes(-9));

        var config = Config.Load();

        Assert.Equal(50, config.OnlineUpdateMinutes);
        Assert.NotNull(config.Recovery);
        Assert.Equal(50, Config.Parse(Files.Text()).OnlineUpdateMinutes);
        Assert.Empty(Files.Temporary());
    }

    /// <summary>The same, found by the other app's save instead of a start: finished first, then merged into.</summary>
    [Fact]
    public void ASaveFindingACutOffSaveMergesIntoIt()
    {
        var settings = Files.Start();
        var dock = Config.Load();
        Files.AddPin(dock, "dock");
        dock.Save();
        // As if the dock's save had died between the renames.
        File.Move(Config.FilePath, Config.FilePath + ".4242-00c0ffee.tmp");

        settings.TaskbarOpacity = 58;
        settings.Save();

        var file = Config.Load();
        Assert.Equal(["dock"], Files.Pins(file));
        Assert.Equal(58, file.TaskbarOpacity);
    }

    /// <summary>With a backup there, a missing config.json was deleted to start over: defaults, not the backup, and no older Karpo's settings.</summary>
    [Fact]
    public void ADeletedConfigStartsOver()
    {
        var config = Files.Start(start => start.TaskbarOpacity = 40);
        config.TaskbarOpacity = 50;
        config.Save();
        File.Delete(Config.FilePath);

        var fresh = Config.Load();

        Assert.Equal(85, fresh.TaskbarOpacity);
        Assert.Null(fresh.Recovery);
        Assert.False(File.Exists(Config.FilePath));
    }

    [Fact]
    public void OnlyTheNewestBrokenFilesAreKept()
    {
        Directory.CreateDirectory(Config.Folder);
        for (var day = 1; day <= 7; day++)
        {
            File.WriteAllText($"{Config.FilePath}.broken-2026090{day}-120000", "x");
        }

        Config.Load();

        Assert.Equal(
            Enumerable.Range(3, 5).Select(day => Path.GetFileName($"{Config.FilePath}.broken-2026090{day}-120000")),
            Files.Broken().Select(Path.GetFileName).Order());
    }

    /// <summary>
    /// A config.json another program holds without sharing reads at the start: defaults for now
    /// (Config.Unread), which are never saved — merged into the person's file, the display they stood
    /// on would show the default layout instead of theirs —, and the person's file as it is once a
    /// reload reads it, nothing of the defaults' on top.
    /// </summary>
    [Fact]
    public void AFileLockedAtStartIsNeverWrittenOverAndIsReadOnceItIsFree()
    {
        Files.Start(start =>
        {
            Files.AddPin(start, "kept");
            start.AddDisplay("D1", "Dell");
            start.ActiveLayout("D1").Name = "Mine";
        });
        var before = Files.Text();
        Config config;
        using (new FileStream(Config.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            config = Config.Load();
        }
        Assert.NotNull(config.Recovery);
        Assert.True(config.Unread);
        Assert.Empty(Files.Pins(config));

        // What the dock does with the defaults at its start: its display put in, and saved.
        config.AddDisplay("D1", "Dell");
        Assert.Throws<IOException>(() => config.Save());
        Assert.Throws<IOException>(() => config.Clone().Save());
        Assert.Equal(before, Files.Text());

        Assert.Equal(ConfigReload.Changed, config.Reload(out var fresh));
        Assert.False(fresh.Unread);
        Assert.False(fresh.StandsIn);
        Assert.Equal(["kept"], Files.Pins(fresh));
        Assert.Equal("Mine", fresh.ActiveLayout("D1").Name);
        fresh.Save();
        var file = Config.Load();
        Assert.Equal(["kept"], Files.Pins(file));
        Assert.Equal("Mine", file.ActiveLayout("D1").Name);
    }
}
