// Settings made new again (ConfigReset, the About page's "Reset", Karpo.exe --reset-settings,
// §266): every setting, or the layouts alone, as a new install on this PC has them; the file there
// was kept beside it, never over a copy kept before; whether Karpo
// starts with Windows, the accounts, their sign-ins and the licence as they were; and the file
// replaced whole, so the dock takes it as it does an import. All in the test run's own folder.

using System.Text.Json.Nodes;
using Karpo.Accounts;
using Karpo.Tests.Persistence;

namespace Karpo.Tests.Core;

[Collection(ConfigFile.Collection)]
public sealed class ConfigResetTests : IDisposable
{
    public ConfigResetTests() => Clean();

    public void Dispose() => Clean();

    private static void Clean()
    {
        Files.Reset();
        foreach (var file in (string[])[AccountBook.FilePath, LicenseCache.Path])
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }

    /// <summary>What a new install starts on in this folder: what Load makes of no file at all.</summary>
    private static Config FirstInstall()
    {
        Files.Reset();
        return Config.Load();
    }

    /// <summary>
    /// A config as the person keeps one, saved: a second layout shown on a display at the top, the
    /// default layout with a widget more and a widget less, and settings away from their defaults.
    /// </summary>
    private static Config Persons() => Files.Start(config =>
    {
        var mine = config.AddLayout("Mine", [BarKind.Apps.New(), BarKind.WidgetsBoard.New(), BarKind.Start.New()], LayoutType.Dock, "");
        Assert.True(config.SelectLayout("D2", mine.Id));
        config.Screens.Single(screen => screen.Id == "D2").Edge = Edge.Top;
        config.DefaultLayout.Islands.RemoveAll(island => island.Kind == BarKind.ShowDesktop);
        config.DefaultLayout.Islands.Add(BarKind.WidgetsBoard.New());
        config.TaskbarOpacity = 55;
        config.DefaultLayout.OpenOnHover = true;
        config.StartWithWindows = false;
        // §347: the tray's icons are its own, the layout's with it.
        config.DefaultLayout.Islands.First(island => island.Kind == BarKind.Tray).TrayIcons = new TrayIconOptions { States = { ["Some app"] = TrayState.Taskbar } };
    });

    /// <summary>A config's JSON with every id taken out: a new widget's id is made new each time.</summary>
    private static string WithoutIds(Config config) => WithoutIds(JsonNode.Parse(config.ToJson())!).ToJsonString();

    private static JsonNode WithoutIds(JsonNode node)
    {
        if (node is JsonObject json)
        {
            json.Remove("id");
            foreach (var (_, value) in json.ToList())
            {
                if (value is not null)
                {
                    WithoutIds(value);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not null)
                {
                    WithoutIds(item);
                }
            }
        }
        return node;
    }

    /// <summary>The JSON without the parts a reset of the layouts makes new: what it must leave as it was.</summary>
    private static string AllButLayouts(Config config)
    {
        var json = JsonNode.Parse(config.ToJson())!.AsObject();
        json.Remove("layouts");
        json.Remove("defaultLayoutId");
        json.Remove("screens");
        return json.ToJsonString();
    }

    private static string Part(Config config, string name) => WithoutIds(JsonNode.Parse(config.ToJson())![name]!.DeepClone()).ToJsonString();

    [Fact]
    public void AResetOfEverySettingIsANewInstallsConfigOnThisPc()
    {
        var fresh = FirstInstall();
        Persons();

        var reset = ConfigReset.ResetFile(ResetScope.All, out _);

        // But whether Karpo starts with Windows, which is this PC's own choice.
        Assert.False(reset.StartWithWindows);
        fresh.StartWithWindows = false;
        Assert.Equal(WithoutIds(fresh), WithoutIds(reset));
        Assert.Equal(WithoutIds(fresh), WithoutIds(Config.Load()));
        Assert.Single(reset.Layouts);
        Assert.DoesNotContain(reset.AllIslands, island => island.Kind == BarKind.WidgetsBoard);
        Assert.All(TrayLayout.Trays(reset), tray => Assert.Null(tray.TrayIcons));
    }

    [Fact]
    public void AResetOfTheLayoutsMakesThemANewInstallsAndKeepsEveryOtherSetting()
    {
        var fresh = FirstInstall();
        var persons = Persons();
        var others = AllButLayouts(persons);

        var reset = ConfigReset.ResetFile(ResetScope.Layouts, out _);

        Assert.Equal(others, AllButLayouts(reset));
        Assert.Equal(others, AllButLayouts(Config.Load()));
        Assert.Equal(Part(fresh, "layouts"), Part(reset, "layouts"));
        Assert.Equal(fresh.DefaultLayoutId, reset.DefaultLayoutId);
        // Every display shows the new install's layout, at the bottom, as a display it never saw.
        Assert.Equal(Part(fresh, "screens"), Part(reset, "screens"));
        Assert.Equal(Edge.Bottom, reset.EdgeOfScreen("D2"));
        Assert.Same(reset.DefaultLayout, reset.ActiveLayout("D2"));
        // §567: how everything looks is Karpo's one Appearance, no layout's: kept with every other setting.
        Assert.Equal(persons.TaskbarOpacity, reset.TaskbarOpacity);
        Assert.False(reset.StartWithWindows);
    }

    [Fact]
    public void ResetInMemoryLeavesTheConfigItWasGivenAsItIs()
    {
        var persons = Persons();
        var before = persons.ToJson();

        ConfigReset.Reset(persons, ResetScope.Layouts);
        ConfigReset.Reset(persons, ResetScope.All);

        Assert.Equal(before, persons.ToJson());
    }

    [Fact]
    public void TheFileThereWasIsKeptBesideItNeverOverACopyKeptBefore()
    {
        Persons();
        var text = Files.Text();

        ConfigReset.ResetFile(ResetScope.All, out var first);
        var between = Files.Text();
        ConfigReset.ResetFile(ResetScope.Layouts, out var second);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(Config.Folder, Path.GetDirectoryName(first));
        Assert.StartsWith("config.json.before-reset-", Path.GetFileName(first), StringComparison.Ordinal);
        Assert.NotEqual(first, second);
        Assert.Equal(text, File.ReadAllText(first!));
        Assert.Equal(between, File.ReadAllText(second!));
        // And as the backup, as any save before it would have.
        Assert.Equal(between, File.ReadAllText(Config.BackupPath));
    }

    [Fact]
    public void WithoutAFileThereIsNothingToKeep()
    {
        var fresh = FirstInstall();

        var reset = ConfigReset.ResetFile(ResetScope.All, out var kept);

        Assert.Null(kept);
        Assert.Equal(WithoutIds(fresh), WithoutIds(reset));
        Assert.True(File.Exists(Config.FilePath));
    }

    [Fact]
    public void AResetFileLoadsAsItWasWritten()
    {
        Persons();

        var reset = ConfigReset.ResetFile(ResetScope.All, out _);
        var text = Files.Text();
        var loaded = Config.Load();

        Assert.Null(loaded.Recovery);
        Assert.False(loaded.StandsIn);
        Assert.Equal(reset.ToJson(), loaded.ToJson());
        // Loading it wrote nothing back: it was a file as Karpo writes one.
        Assert.Equal(text, Files.Text());
        // It knows the file, so its next save merges as any other, and the file is its own.
        Assert.Equal(ConfigReload.Unchanged, reset.Reload(out _));
        reset.TaskbarOpacity = 30;
        reset.Save();
        Assert.Equal(30, Config.Load().TaskbarOpacity);
    }

    /// <summary>
    /// The accounts and their sign-ins live in %LOCALAPPDATA%\Karpo\accounts.json (docs\accounts.md),
    /// the licence beside them: a reset of every setting leaves both as they were.
    /// </summary>
    [Fact]
    public void TheAccountsTheirSignInsAndTheLicenceStay()
    {
        Persons();
        Directory.CreateDirectory(Config.LocalFolder);
        const string Accounts = """{ "accounts": [ { "id": "a1", "kind": "microsoft", "address": "someone@example.com", "secret": "AQAAANCMnd8BFdERjHoAwE" } ] }""";
        File.WriteAllText(AccountBook.FilePath, Accounts);
        File.WriteAllBytes(LicenseCache.Path, [1, 2, 3, 4]);

        ConfigReset.ResetFile(ResetScope.All, out _);

        Assert.Equal(Accounts, File.ReadAllText(AccountBook.FilePath));
        Assert.Equal([1, 2, 3, 4], File.ReadAllBytes(LicenseCache.Path));
        Assert.DoesNotContain("someone@example.com", Files.Text(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A reset replaces the file whole, as an import does (Config.ReplacedAt): the dock takes the pins
    /// it took away for no unpin, so they stay on Windows' taskbar (WindowsPins.cs). And it is no
    /// edit of Karpo's own (Config.KarposOwnEdit), which is what lets a pin go.
    /// </summary>
    [Fact]
    public void AResetIsAFileReplacedWholeNotAnEdit()
    {
        var persons = Persons();
        var before = Config.ReplacedAt();

        ConfigReset.ResetFile(ResetScope.Layouts, out _);

        Assert.True(Config.ReplacedAt() > before);
        Assert.Equal(ConfigReload.Changed, persons.Reload(out var fresh));
        Assert.False(fresh.KarposOwnEdit);
    }

    [Fact]
    public void AResetOfEverySettingFollowsWindowsLanguageAndLeavesTheOneSpokenAsItWas()
    {
        Persons();
        var chosen = Config.Load();
        chosen.Language = "de";
        chosen.Save();
        var speaking = Strings.Language;

        var layouts = ConfigReset.ResetFile(ResetScope.Layouts, out _);
        Assert.Equal("de", layouts.Language);
        Assert.Equal(speaking, Strings.Language);

        var all = ConfigReset.ResetFile(ResetScope.All, out _);
        Assert.Null(all.Language);
        Assert.Equal(speaking, Strings.Language);
    }

    /// <summary>The layouts alone of a newer Karpo's file are not reset: this build would drop what it doesn't know, as a save would.</summary>
    [Fact]
    public void ANewerKarposFileKeepsItsLayouts()
    {
        var newer = $$"""{ "version": {{Config.Shape + 1}}, "opacity": 40, "somethingNew": true }""";
        File.WriteAllText(Config.FilePath, newer);

        Assert.Throws<IOException>(() => ConfigReset.ResetFile(ResetScope.Layouts, out _));
        Assert.Equal(newer, Files.Text());
    }
}
