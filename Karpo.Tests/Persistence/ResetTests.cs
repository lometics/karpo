// The file's shape (Config.Shape): there is one, today's. A file that says an earlier version, or
// none, is read as it is — never kept aside, never replaced by the defaults — and written back with
// this build's version. A file from a newer Karpo is left alone. And the defaults a start writes are
// a bar of bare taskbar entries.

namespace Karpo.Tests.Persistence;

[Collection(ConfigFile.Collection)]
public sealed class ResetTests : IDisposable
{
    private static readonly DateTime LongAgo = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public ResetTests() => Files.Reset();

    public void Dispose() => Files.Reset();

    /// <summary>The files in the config folder, by name: what a start left there.</summary>
    private static string[] Names() => [.. Directory.GetFiles(Config.Folder).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal)];

    // --- The version -------------------------------------------------------------------

    /// <summary>
    /// A file that says an earlier version, or none, is the person's settings all the same: read as it
    /// is, nothing kept aside and nothing reset; the next save writes it with this build's version (a
    /// version alone is no reason to write the file on load: Config.MadeUpIds).
    /// </summary>
    [Theory]
    [InlineData("\"version\": 12,")]
    [InlineData("")]
    public void AnEarlierVersionIsReadAsItIsAndNeverReset(string version)
    {
        File.WriteAllText(Config.FilePath, $$"""
            { {{version}} "onlineUpdateMinutes": 40, "layouts": [ { "id": "home", "name": "Home", "islands": [ { "id": "s", "kind": "start" }, { "id": "t", "kind": "tray" } ] } ], "defaultLayoutId": "home" }
            """);

        var config = Config.Load();

        Assert.Null(config.Recovery);
        Assert.Equal(40, config.OnlineUpdateMinutes);
        Assert.Equal(["s", "t"], Karpo.Tests.Core.Configs.Islands(config).Select(island => island.Id));
        Assert.Equal(Config.Shape, config.Version);
        Assert.DoesNotContain(Names(), name => name.StartsWith("config.shape", StringComparison.Ordinal));
        config.Save();
        Assert.Equal(Config.Shape, Config.Parse(Files.Text()).Version);
        Assert.Equal(40, Config.Parse(Files.Text()).OnlineUpdateMinutes);
    }

    /// <summary>A file from a newer Karpo is read as it is and never written: not kept aside, not replaced, not touched.</summary>
    [Fact]
    public void ANewerFileIsLeftAlone()
    {
        var newer = $$"""{ "version": {{Config.Shape + 1}}, "onlineUpdateMinutes": 40, "somethingNew": true }""";
        File.WriteAllText(Config.FilePath, newer);
        File.SetLastWriteTimeUtc(Config.FilePath, LongAgo);

        var config = Config.Load();

        Assert.True(config.FromNewerKarpo);
        Assert.Equal(40, config.OnlineUpdateMinutes);
        Assert.Equal(newer, Files.Text());
        Assert.Equal(LongAgo, File.GetLastWriteTimeUtc(Config.FilePath));
        Assert.Equal(["config.json"], Names());
    }

    // --- The defaults ----------------------------------------------------------------------

    /// <summary>
    /// What a start without a file writes: the taskbar's own entries, bare — no folder
    /// but the keyboard, quick settings and the clock button (their presets) — Windows' buttons at
    /// the left (2026-10-14), the rest in the section Windows keeps it in, the list in its sections'
    /// order, no spacer anywhere, every id its own, and nothing a repair would change.
    /// </summary>
    [Fact]
    public void TheDefaultsAreBareTaskbarEntriesInTheTaskbarsSections()
    {
        var config = new Config();
        config.Repair();
        var bar = Karpo.Tests.Core.Configs.Islands(config);

        Assert.Single(config.Layouts);
        Assert.All(bar, island => Assert.True(BarEntries.Find(island.Kind) is not null, $"{island.Kind} can't stand on a bar"));
        // The folders are the presets: the clock button, quick settings and the keyboard; no weather (2026-10-14).
        Assert.Single(bar, island => island.Folder?.Face.Kind == FolderFaces.DateTimeId);
        Assert.All(bar.Where(island => island.Folder is not null), island => Assert.Contains(island.Folder!.Face.Kind, new[] { FolderFaces.DateTimeId, FolderFaces.WifiId, FolderFaces.KeyboardId }));
        Assert.Equal(bar.Count, bar.Select(island => island.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.DoesNotContain(bar, island => island.Info?.Id == "spacer");
        // Every entry in its kind's section, but Windows' own buttons, which a new bar has at the
        // left with nothing in the middle (2026-10-14); the sections in the bar's order. Windows'
        // buttons one run (2026-10-13). No brightness, and no separator (§511).
        Assert.All(bar, island => Assert.Equal(island.Kind.SectionOf() == BarSection.Middle ? BarSection.Start : island.Kind.SectionOf(), island.Section));
        Assert.DoesNotContain(bar, island => island.Kind == BarKind.Brightness);
        Assert.Equal(bar, bar.OrderBy(island => island.Section).ToList());
        Assert.Contains(bar, island => island.Kind == BarKind.Start);
        Assert.Contains(bar, island => island.Kind == BarKind.FolderList);
        Karpo.Tests.Core.Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
    }
}
