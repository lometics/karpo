// The app buttons' one setting, as Windows 11 keeps it under Taskbar behaviors: "Combine taskbar
// buttons and hide labels" (Karpo.Core\TaskbarApps.cs), on every bar alike. Windows' "…on other
// taskbars" is not a setting (the person, 2026-10-05: "it should follow the default one for the
// main taskbar / be the same"), nor is "Show my taskbar apps on": the open apps widget shows every
// open window on every bar. A file that still says either loads all the same and loses the word
// at its next save.
//
// A new config takes Windows' own answer, read from Explorer\Advanced — but never under the tests,
// which are isolated and so keep Windows' default whatever this PC says. A file from before this
// was a setting, or at the default, says nothing of it, so it stays byte for byte what it was; a
// word this build does not know reads as the default and is written back once.

namespace Karpo.Tests.Core;

public sealed class TaskbarAppsTests
{
    [Fact]
    public void ANewConfigCombinesAlways()
    {
        var config = new Config();

        Assert.Equal(ButtonCombine.Always, config.DefaultLayout.Combine);
    }

    /// <summary>The tests are isolated, and an isolated config never reads the PC's own taskbar.</summary>
    [Fact]
    public void AnIsolatedConfigNeverReadsWindowsOwnAnswers()
    {
        Assert.True(Config.IsIsolated);
        var config = new Config { DefaultLayout = { Combine = ButtonCombine.Never } };

        TaskbarApps.FromWindows(config);

        Assert.Equal(ButtonCombine.Never, config.DefaultLayout.Combine);
    }

    /// <summary>Windows' <c>TaskbarGlomLevel</c>; a missing or unknown value is Windows' default.</summary>
    [Theory]
    [InlineData(null, ButtonCombine.Always)]
    [InlineData(0, ButtonCombine.Always)]
    [InlineData(1, ButtonCombine.WhenFull)]
    [InlineData(2, ButtonCombine.Never)]
    [InlineData(3, ButtonCombine.Always)]
    [InlineData(-1, ButtonCombine.Always)]
    public void WindowsCombineLevelsReadAsItsChoices(int? level, ButtonCombine combine) =>
        Assert.Equal(combine, TaskbarApps.CombineOf(level));

    /// <summary>
    /// Every bar, the main display's and every other, combines as its layout's one setting says (§345): nothing is
    /// left that could choose otherwise — no second setting in the config, no question of whether a
    /// bar is the main display's, and every bar's buttons laid out by its layout's setting
    /// (AppButtons.Lay, which "When taskbar is full" goes through as well).
    /// </summary>
    [Fact]
    public void EveryBarCombinesByTheOneSetting()
    {
        const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;

        Assert.Null(typeof(Config).GetProperty("CombineOther", Any));
        Assert.DoesNotContain(typeof(TaskbarApps).GetMethods(Any), method => method.Name == "CombineOn"
            || method.GetParameters().Any(parameter => parameter.ParameterType == typeof(bool)));
        var repo = RepoSource.Root();
        if (repo.Length == 0)
        {
            return;
        }
        var dock = string.Join("\n", Directory.GetFiles(Path.Combine(repo, "Karpo.Dock"), "*.cs").Select(File.ReadAllText));
        Assert.DoesNotContain("CombineOther", dock);
        // Each bar lays its buttons out by its layout's one setting (Config.BarLayout), and asks nothing of its display.
        var lays = System.Text.RegularExpressions.Regex.Matches(dock, @"AppButtons\.Lay\(\s*([^,]+),");
        Assert.NotEmpty(lays);
        Assert.All(lays, lay => Assert.Equal("config.BarLayout.Combine", lay.Groups[1].Value.Trim()));
    }

    /// <summary>At Windows' default the file says nothing of it, so a file from before it was a setting stays as it was.</summary>
    [Fact]
    public void AtWindowsDefaultTheFileSaysNothingOfIt()
    {
        var keys = Configs.Keys(Configs.With(BarKind.Start.New()));

        Assert.DoesNotContain("combine", keys);
        Assert.DoesNotContain("combineOther", keys);
        Assert.DoesNotContain("showAppsOn", keys);
    }

    [Theory]
    [InlineData(ButtonCombine.WhenFull, "whenFull")]
    [InlineData(ButtonCombine.Never, "never")]
    [InlineData(ButtonCombine.Always, null)]
    public void ChosenItIsWrittenAndReadBack(ButtonCombine combine, string? word)
    {
        var config = Configs.With(BarKind.Start.New());
        config.DefaultLayout.Combine = combine;
        var json = config.ToJson();

        if (word is null)
        {
            Assert.DoesNotContain("\"combine\"", json);
        }
        else
        {
            Assert.Contains($"\"combine\": \"{word}\"", json);
        }
        var again = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal(combine, again.DefaultLayout.Combine);
        Assert.Equal(json, again.ToJson());
    }

    /// <summary>A word from another build, or a hand edit, reads as Windows' default and is written back once.</summary>
    [Fact]
    public void AWordThisBuildDoesNotKnowReadsAsWindowsDefault()
    {
        var known = Configs.With(BarKind.Start.New()).ToJson();
        var json = known.Replace("\"previews\": true", "\"combine\": \"sometimes\", \"previews\": true", StringComparison.Ordinal);
        Assert.Contains("sometimes", json, StringComparison.Ordinal);

        var config = Configs.Repaired(json, out var changed);

        Assert.True(changed);
        Assert.Equal(ButtonCombine.Always, config.DefaultLayout.Combine);
        var again = Configs.RoundTrip(config, out var twice);
        Assert.False(twice);
        Assert.DoesNotContain("\"combine\"", again.ToJson());
    }

    /// <summary>
    /// "…on other taskbars" is not a setting any more. A file an earlier build wrote with it — any
    /// of its words, or one nobody knows, beside the one setting or without it — loads whole, the
    /// one setting kept as it said, and the word is read past quietly: nothing to save for it, and
    /// gone from the file the next time it is saved for anything else.
    /// </summary>
    [Theory]
    [InlineData("always", ButtonCombine.Never)]
    [InlineData("whenFull", ButtonCombine.Never)]
    [InlineData("never", ButtonCombine.WhenFull)]
    [InlineData("never", ButtonCombine.Always)]
    [InlineData("sometimes", ButtonCombine.Never)]
    public void AFileThatStillSaysOtherTaskbarsLoadsAndForgetsIt(string word, ButtonCombine combine)
    {
        var written = Configs.With(BarKind.Start.New(), BarKind.Apps.New());
        written.DefaultLayout.Combine = combine;
        var known = written.ToJson();
        var json = $"{{ \"combineOther\": \"{word}\"," + known.TrimStart()[1..];

        var config = Configs.Repaired(json, out var changed);

        Assert.False(changed);
        Assert.Equal(combine, config.DefaultLayout.Combine);
        Assert.Equal(Configs.Islands(written).Select(island => island.Kind), Configs.Islands(config).Select(island => island.Kind));
        Assert.DoesNotContain("combineOther", config.ToJson());
        Assert.Equal(known, config.ToJson());
    }

    /// <summary>
    /// The open apps widget shows every open window on every bar it stands on, the same on each:
    /// nothing is left that could say otherwise — no setting in the config, no choice of Windows'
    /// read into one, and no bar asking which display a window is on (Dock.TilesFor).
    /// </summary>
    [Fact]
    public void EveryBarShowsEveryOpenWindow()
    {
        const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;

        Assert.Null(typeof(Config).GetProperty("ShowAppsOn", Any));
        Assert.Null(typeof(Config).Assembly.GetType("Karpo.Core.AppsOn"));
        Assert.DoesNotContain(typeof(TaskbarApps).GetMethods(Any), method => method.Name is "Shows" or "AppsOnOf");
        var bar = typeof(Karpo.Dock.Dock).GetNestedType("DockWindow", Any);
        Assert.NotNull(bar);
        Assert.Null(bar!.GetMethod("OnDisplay", Any));
    }

    /// <summary>
    /// "Show my taskbar apps on" is not a setting any more. A file an earlier build wrote with it —
    /// any of its three words, or one nobody knows — loads whole, the rest of what it says kept, and
    /// the word is read past quietly: nothing to save for it (it is not a rewording), and gone from
    /// the file the next time it is saved for anything else.
    /// </summary>
    [Theory]
    [InlineData("all")]
    [InlineData("mainAndWhereOpen")]
    [InlineData("whereOpen")]
    [InlineData("everywhere")]
    public void AFileThatStillSaysWhereAppsShowLoadsAndForgetsIt(string word)
    {
        var written = Configs.With(BarKind.Start.New(), BarKind.Apps.New());
        written.DefaultLayout.Combine = ButtonCombine.Never;
        var known = written.ToJson();
        var json = $"{{ \"showAppsOn\": \"{word}\"," + known.TrimStart()[1..];

        var config = Configs.Repaired(json, out var changed);

        Assert.False(changed);
        Assert.Equal(ButtonCombine.Never, config.DefaultLayout.Combine);
        Assert.Equal(Configs.Islands(written).Select(island => island.Kind), Configs.Islands(config).Select(island => island.Kind));
        Assert.DoesNotContain("showAppsOn", config.ToJson());
        Assert.Equal(known, config.ToJson());
    }
}
