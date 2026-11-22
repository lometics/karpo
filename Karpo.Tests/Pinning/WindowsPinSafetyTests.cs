// Windows' taskbar pins are the person's own, and Karpo acts on them (backlog 2). These are the ways
// the sync was shown to unpin or add pins nobody asked for, each held to the safe side: defaults run
// on for now never unpin (A), a program's pin never holds its web apps (B), a lost memory or a flood
// adds nothing (C), a short read moves nothing (D), and an import never unpins (E). And the review
// layouts are left out of it. All of it on a made-up pin store and shell, and on the test run's own
// config folder: nothing here reads or touches the taskbar of the PC the tests run on.

using Karpo.Tests.Persistence;

namespace Karpo.Tests.Pinning;

[Collection(ConfigFile.Collection)]
public sealed class WindowsPinSafetyTests : IDisposable
{
    private const string Folder = @"C:\Users\a\AppData\Roaming\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\";
    private const string Brave = Folder + "Brave.lnk";
    private const string YouTube = Folder + "YouTube.lnk";
    private const string Tool = Folder + "Tool.lnk";
    private const string Code = Folder + "Visual Studio Code.lnk";
    private const string BraveExe = @"C:\Program Files\Brave\brave.exe";
    private const string ToolExe = @"C:\Program Files\Tool\tool.exe";
    private const string Outlook = @"shell:AppsFolder\Microsoft.OutlookForWindows_8wekyb3d8bbwe!Microsoft.OutlookforWindows";
    private const string Spotify = @"shell:AppsFolder\SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify";
    private const string Teams = @"shell:AppsFolder\MSTeams_8wekyb3d8bbwe!MSTeams";
    private const string Photos = @"shell:AppsFolder\Microsoft.Windows.Photos_8wekyb3d8bbwe!App";

    /// <summary>Program Files' known folder id, as Windows writes it in an app id it makes up for a program.</summary>
    private const string ProgramFilesId = "{6d809377-6af0-444b-8957-a3773f02200e}";

    private static string EditsLog => Path.Combine(Config.LocalFolder, "config-edits");

    public WindowsPinSafetyTests()
    {
        Files.Reset();
        Directory.CreateDirectory(Config.LocalFolder);
        File.Delete(EditsLog);
    }

    public void Dispose()
    {
        Files.Reset();
        File.Delete(EditsLog);
    }

    private sealed class FakeStore(params string[] pins) : IWindowsPinStore
    {
        /// <summary>Null reads as a read that wasn't whole.</summary>
        public List<string>? Pins { get; set; } = [.. pins];

        public IReadOnlyList<string>? Read() => Pins?.ToList();
    }

    private sealed class FakeShell : IWindowsPinShell
    {
        public List<string> Asked { get; } = [];

        public bool Unpin(string target)
        {
            Asked.Add(target);
            return true;
        }
    }

    private static readonly Dictionary<string, string[]> Keys = new(StringComparer.OrdinalIgnoreCase)
    {
        // Brave's shortcut names an app id of its own.
        [Brave] = ["aumid:brave", @"exe:c:\program files\brave\brave.exe"],
        // A Brave web app pinned on Windows: its own app id, the browser's program.
        [YouTube] = ["aumid:brave._crx_youtube", @"exe:c:\program files\brave\brave.exe"],
        // A Karpo pin of the program itself: no app id.
        [BraveExe] = [@"exe:c:\program files\brave\brave.exe"],
        // A program that names no app id: its shortcut gets the one Windows makes up from its path.
        [Tool] = [$@"aumid:{ProgramFilesId}\tool\tool.exe", @"exe:c:\program files\tool\tool.exe"],
        [ToolExe] = [@"exe:c:\program files\tool\tool.exe"],
        [Code] = ["aumid:microsoft.visualstudiocode", @"exe:c:\code\code.exe"],
        [Outlook] = ["aumid:microsoft.outlookforwindows_8wekyb3d8bbwe!microsoft.outlookforwindows"],
        [Spotify] = ["aumid:spotifyab.spotifymusic_zpdnekdrzrea0!spotify"],
        [Teams] = ["aumid:msteams_8wekyb3d8bbwe!msteams"],
        [Photos] = ["aumid:microsoft.windows.photos_8wekyb3d8bbwe!app"],
    };

    private static IReadOnlyCollection<string> KeysOf(string target) =>
        Keys.TryGetValue(target.Trim(), out var keys) ? keys : [];

    private static BarItem Pin(string id, string target)
    {
        var pin = BarKind.Pin.New();
        pin.Id = id;
        pin.Pin!.Target = target;
        return pin;
    }

    private static List<BarItem> Bar(params BarItem[] pins)
    {
        var islands = new List<BarItem>();
        var start = BarKind.Start.New();
        start.Id = "s";
        islands.Add(start);
        islands.AddRange(pins);
        var apps = BarKind.Apps.New();
        apps.Id = "a";
        islands.Add(apps);
        return islands;
    }

    private static Config OneLayout(params BarItem[] pins)
    {
        var config = new Config
        {
            Layouts = [new TaskbarLayout { Id = "home", Name = "Home", Islands = Bar(pins) }],
            DefaultLayoutId = "home",
        };
        config.Repair();
        return config;
    }

    private static List<string?> PinsOf(Config config, string layout = "home") =>
        config.Layouts.Single(each => each.Id == layout).Islands.Where(item => item.Kind == BarKind.Pin).Select(item => item.Pin?.Target).ToList();

    /// <summary>The layout new displays get, its pins those of <paramref name="pins"/>.</summary>
    private static void PinsAre(Config config, params string[] pins)
    {
        config.DefaultLayout.Islands.RemoveAll(island => island.Kind == BarKind.Pin);
        foreach (var target in pins)
        {
            Files.AddPin(config, target);
        }
    }

    // --- A: defaults for now, a recovery, a reload: never an unpin ----------------------------------

    /// <summary>
    /// The dock started on defaults "for now" (config.json locked at start): the defaults hold every
    /// Windows pin. Taking the person's real file afterwards, which holds Brave only, unpins nothing.
    /// </summary>
    [Fact]
    public void DefaultsForNowNeverUnpinWhenTheRealFileIsRead()
    {
        var store = new FakeStore(Brave, Outlook, Spotify);
        var shell = new FakeShell();
        var sync = new WindowsPinSync(store, shell, KeysOf);
        Files.Start(start => PinsAre(start, Brave));
        Config waiting;
        using (new FileStream(Config.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            waiting = Config.Load();
        }
        // What the defaults would hold on the person's PC: every pin Windows has.
        var defaults = waiting.Clone();
        defaults.DefaultLayout.Islands = Sections.DefaultLayout(pins: store.Pins!);
        Assert.True(defaults.StandsIn);
        Assert.Equal(ConfigReload.Changed, waiting.Reload(out var real));

        Assert.Empty(sync.Release(defaults, real));
        Assert.Empty(shell.Asked);
    }

    /// <summary>The file side of A: a dock that loaded defaults while the file was locked takes the real file as no edit of Karpo's.</summary>
    [Fact]
    public void ALockedFileAtStartIsLaterTakenAsNoEditOfKarpos()
    {
        Files.Start(start => start.TaskbarOpacity = 66);
        Config waiting;
        using (new FileStream(Config.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            waiting = Config.Load();
        }
        Assert.NotNull(waiting.Recovery);
        Assert.True(waiting.StandsIn);
        Assert.True(waiting.Clone().StandsIn);

        Assert.Equal(ConfigReload.Changed, waiting.Reload(out var fresh));

        Assert.Equal(66, fresh.TaskbarOpacity);
        Assert.False(fresh.KarposOwnEdit);
        // Read from the file: the person's own from now on.
        Assert.False(fresh.StandsIn);
    }

    /// <summary>config.json deleted while the dock runs, then put back: neither is an edit, and the defaults in between stand in.</summary>
    [Fact]
    public void AFileDeletedAndPutBackIsNoEdit()
    {
        var store = new FakeStore(Brave, Outlook, Spotify);
        var shell = new FakeShell();
        var sync = new WindowsPinSync(store, shell, KeysOf);
        Files.Start(start => { start.TaskbarOpacity = 66; PinsAre(start, Brave); });
        var dock = Config.Load();
        var text = Files.Text();

        ConfigFile.Delete();
        Assert.Equal(ConfigReload.Changed, dock.Reload(out var gone));
        Assert.True(gone.StandsIn);
        Assert.False(gone.KarposOwnEdit);
        // What the defaults would hold on the person's PC.
        var shown = gone.Clone();
        shown.DefaultLayout.Islands = Sections.DefaultLayout(pins: store.Pins!);
        Assert.True(shown.StandsIn);

        File.WriteAllText(Config.FilePath, text);
        Assert.Equal(ConfigReload.Changed, gone.Reload(out var back));

        Assert.False(back.KarposOwnEdit);
        Assert.False(back.StandsIn);
        Assert.Equal([Brave], Files.Pins(back));
        Assert.Empty(sync.Release(dock, gone));
        Assert.Empty(sync.Release(shown, back));
        Assert.Empty(shell.Asked);
    }

    /// <summary>A file that didn't read, its last good version put back: the config stands in, and releases nothing.</summary>
    [Fact]
    public void ARecoveredFileStandsInAndReleasesNothing()
    {
        var store = new FakeStore(Brave, Outlook);
        var shell = new FakeShell();
        var sync = new WindowsPinSync(store, shell, KeysOf);
        Files.Start(start => PinsAre(start, Brave, Outlook));
        // A second save, so the backup is a good version.
        var saved = Config.Load();
        saved.TaskbarOpacity = 60;
        saved.Save();
        File.WriteAllText(Config.FilePath, "{ \"opacity\": 6");

        var recovered = Config.Load();

        Assert.NotNull(recovered.Recovery);
        Assert.True(recovered.StandsIn);
        var edited = recovered.Clone();
        Files.RemovePin(edited, Outlook);
        Assert.Empty(sync.Release(recovered, edited));
        Assert.Empty(shell.Asked);
    }

    /// <summary>A hand edit of config.json is no edit made through Karpo.</summary>
    [Fact]
    public void AHandEditIsNoEditOfKarpos()
    {
        Files.Start(start => start.OnlineUpdateMinutes = 66);
        var dock = Config.Load();

        File.WriteAllText(Config.FilePath, Files.Text().Replace("66", "70", StringComparison.Ordinal));

        Assert.Equal(ConfigReload.Changed, dock.Reload(out var fresh));
        Assert.Equal(70, fresh.OnlineUpdateMinutes);
        Assert.False(fresh.KarposOwnEdit);
    }

    /// <summary>The settings app's saves, one or several, are edits made through Karpo; a hand edit among them is not.</summary>
    [Fact]
    public void TheSettingsAppsSavesAreKarposOwnEdits()
    {
        Files.Start(start => PinsAre(start, Brave, Outlook));
        var dock = Config.Load();
        var settings = Config.Load();

        Files.RemovePin(settings, Outlook);
        settings = settings.Save();
        Assert.Equal(ConfigReload.Changed, dock.Reload(out var once));
        Assert.True(once.KarposOwnEdit);

        // Two saves before the dock looks.
        settings.OnlineUpdateMinutes = 60;
        settings = settings.Save();
        settings.OnlineUpdateMinutes = 62;
        settings = settings.Save();
        Assert.Equal(ConfigReload.Changed, once.Reload(out var twice));
        Assert.True(twice.KarposOwnEdit);

        // A hand edit, then a save of the settings app's on top of it: not all of it was Karpo's.
        File.WriteAllText(Config.FilePath, Files.Text().Replace("62", "64", StringComparison.Ordinal));
        settings.OnlineUpdateMinutes = 61;
        settings.Save();
        Assert.Equal(ConfigReload.Changed, twice.Reload(out var mixed));
        Assert.False(mixed.KarposOwnEdit);
    }

    /// <summary>Through the dock's own rules: a settings app edit lets go of the pin it removed, and only that one.</summary>
    [Fact]
    public void ASettingsEditLetsGoOfThePinItRemovedOnly()
    {
        var store = new FakeStore(Brave, Outlook, Spotify);
        var shell = new FakeShell();
        var sync = new WindowsPinSync(store, shell, KeysOf);
        Files.Start(start => PinsAre(start, Brave, Outlook));
        var dock = Config.Load();
        var settings = Config.Load();

        Files.RemovePin(settings, Outlook);
        settings.Save();
        Assert.Equal(ConfigReload.Changed, dock.Reload(out var fresh));

        Assert.True(fresh.KarposOwnEdit);
        Assert.Equal([Outlook], sync.Release(dock, fresh));
        Assert.Equal([Outlook], shell.Asked);
    }

    /// <summary>
    /// A pin removed in the settings app and put back with Undo (same id, same place: the file's text
    /// as before), then config.json.bak — the file without the pin — coming back, by hand or by a
    /// recovery: no edit of Karpo's, though Karpo once wrote that text, so nothing is unpinned.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnOlderTextOfKarposComingBackIsNoEdit(bool recovered)
    {
        Files.Start(start => PinsAre(start, Brave, Outlook));
        var dock = Config.Load();
        var settings = Config.Load();
        var list = settings.DefaultLayout.Islands;
        var index = list.FindIndex(item => item.Pin?.Target == Outlook);
        var removed = list[index];
        list.RemoveAt(index);
        settings = settings.Save();
        Assert.Equal(ConfigReload.Changed, dock.Reload(out var without));
        Assert.True(without.KarposOwnEdit);
        // Undo.
        settings.DefaultLayout.Islands.Insert(index, removed.Clone());
        settings.Save();
        Assert.Equal(ConfigReload.Changed, without.Reload(out var back));
        Assert.True(back.KarposOwnEdit);

        if (recovered)
        {
            // A hand edit with a mistake, then the settings app starting: Load puts the backup back.
            File.WriteAllText(Config.FilePath, "{ \"opacity\": 6");
            Assert.Equal(ConfigReload.Broken, back.Reload(out _));
            Assert.NotNull(Config.Load().Recovery);
        }
        else
        {
            File.Copy(Config.BackupPath, Config.FilePath, overwrite: true);
        }
        Assert.Equal(ConfigReload.Changed, back.Reload(out var restored));

        Assert.DoesNotContain(Outlook, Files.Pins(restored));
        Assert.False(restored.KarposOwnEdit);
    }

    // --- E: an import never unpins ------------------------------------------------------------------

    /// <summary>An import replaces the file whole: no edit of Karpo's, whatever the mark says.</summary>
    [Fact]
    public void AnImportIsNoEditOfKarpos()
    {
        Files.Start(start => PinsAre(start, Brave, Outlook));
        var dock = Config.Load();
        var imported = Config.Load().Clone();
        PinsAre(imported, Spotify);

        Config.Replace(imported, out var kept);

        Assert.NotNull(kept);
        Assert.Equal(ConfigReload.Changed, dock.Reload(out var fresh));
        Assert.False(fresh.KarposOwnEdit);
    }

    /// <summary>
    /// The import's mark is used up by the reload that takes the imported config, not by the copy an
    /// import keeps beside the file first (a reload that finds the file unchanged), nor by one that
    /// meets it Busy or Broken.
    /// </summary>
    [Fact]
    public void TheImportMarkWaitsForTheReloadThatTakesIt()
    {
        Files.Start();
        var seen = Config.ReplacedAt();
        Config.Replace(Config.Load().Clone(), out _);

        Assert.False(Config.ReplacedFor(ConfigReload.Unchanged, ref seen));
        Assert.False(Config.ReplacedFor(ConfigReload.Busy, ref seen));
        Assert.False(Config.ReplacedFor(ConfigReload.Broken, ref seen));
        Assert.True(Config.ReplacedFor(ConfigReload.Changed, ref seen));
        // Used up: the next edit is not an import.
        Assert.False(Config.ReplacedFor(ConfigReload.Changed, ref seen));
    }

    // --- B: a program's pin holds the program's own app, nothing wider -------------------------------

    /// <summary>Taking a Karpo pin of brave.exe off unpins none of Brave's web apps, nor Brave's own shortcut with its own app id.</summary>
    [Fact]
    public void RemovingAProgramPinLeavesItsWebAppsOnWindows()
    {
        var store = new FakeStore(Brave, YouTube);
        var shell = new FakeShell();
        var sync = new WindowsPinSync(store, shell, KeysOf);
        var config = OneLayout(Pin("b", BraveExe));

        Assert.Empty(sync.Release(config, OneLayout()));

        Assert.DoesNotContain(YouTube, shell.Asked);
        Assert.Empty(shell.Asked);
    }

    /// <summary>A program that names no app id: its pin and Windows' shortcut of it are one app, both ways.</summary>
    [Fact]
    public void AProgramPinHoldsTheShortcutWindowsMadeForItself()
    {
        var store = new FakeStore(Tool, Outlook);
        var shell = new FakeShell();
        var sync = new WindowsPinSync(store, shell, KeysOf);
        var config = OneLayout(Pin("t", ToolExe), Pin("o", Outlook));

        Assert.False(sync.AddTo(config, ["home"], [Tool]));
        Assert.Equal([Tool], sync.Release(config, OneLayout(Pin("o", Outlook))));
    }

    [Fact]
    public void DifferentAppIdsAreAlwaysDifferentApps()
    {
        const string program = @"exe:c:\program files\brave\brave.exe";
        // A program with no app id, and a web app of it.
        Assert.False(WindowsPinSync.SameApp([program], ["aumid:brave._crx_youtube", program]));
        Assert.False(WindowsPinSync.SameApp(["aumid:brave._crx_youtube", program], [program]));
        // A shortcut naming an app id of its own.
        Assert.False(WindowsPinSync.SameApp([program], ["aumid:brave", program]));
        // The id Windows makes up for the program itself.
        Assert.True(WindowsPinSync.SameApp([program], [$@"aumid:{ProgramFilesId}\brave\brave.exe", program]));
        Assert.True(WindowsPinSync.SameApp([program], [program]));
        // Neither has an app id, and they start different programs.
        Assert.False(WindowsPinSync.SameApp([program], [@"exe:c:\other.exe"]));
    }

    [Theory]
    [InlineData(@"aumid:c:\program files\brave\brave.exe", @"exe:c:\program files\brave\brave.exe", true)]
    [InlineData(@"aumid:{6d809377-6af0-444b-8957-a3773f02200e}\brave\brave.exe", @"exe:c:\program files\brave\brave.exe", true)]
    [InlineData(@"{6D809377-6AF0-444B-8957-A3773F02200E}\Brave\brave.exe", @"C:\Program Files\Brave\brave.exe", true)]
    [InlineData("aumid:brave", @"exe:c:\program files\brave\brave.exe", false)]
    [InlineData("aumid:brave._crx_youtube", @"exe:c:\program files\brave\brave.exe", false)]
    // A folder name that only ends the same way.
    [InlineData(@"aumid:{6d809377-6af0-444b-8957-a3773f02200e}\ave\brave.exe", @"exe:c:\program files\brave\brave.exe", false)]
    [InlineData(@"aumid:{not-a-guid}\brave\brave.exe", @"exe:c:\program files\brave\brave.exe", false)]
    [InlineData("aumid:", @"exe:c:\brave.exe", false)]
    public void TheIdWindowsMakesUpForAProgramIsTold(string aumid, string exe, bool expected) =>
        Assert.Equal(expected, WindowsPinSync.IsDefaultId(aumid, exe));

    /// <summary>A web app's shortcut is unpinned by its own app id, never by the browser it starts.</summary>
    [Fact]
    public void AWebAppIsNeverUnpinnedThroughItsBrowser()
    {
        var program = @"C:\Program Files\Brave\brave.exe";

        var webApp = Karpo.Dock.WindowsPinShell.Candidates(YouTube, "Brave._crx_youtube", program).ToList();
        Assert.Equal([@"shell:AppsFolder\Brave._crx_youtube", YouTube], webApp);

        // The program's own shortcut: no app id, or the one Windows makes up for it.
        Assert.Contains(program, Karpo.Dock.WindowsPinShell.Candidates(Brave, null, program));
        Assert.Contains(program, Karpo.Dock.WindowsPinShell.Candidates(Brave, @"{6D809377-6AF0-444B-8957-A3773F02200E}\Brave\brave.exe", program));
        // A shortcut with an app id of its own.
        Assert.DoesNotContain(program, Karpo.Dock.WindowsPinShell.Candidates(Brave, "Brave", program));
    }

    // --- C: a lost memory or a flood adds nothing -----------------------------------------------------

    /// <summary>windows-pins.txt left empty: no memory, so Windows' pins now are the start, and nothing floods in.</summary>
    [Fact]
    public void AnEmptyRememberedFileAddsNothing()
    {
        var store = new FakeStore(Brave, Outlook, Spotify);
        var sync = new WindowsPinSync(store, new FakeShell(), KeysOf, remembered: []);
        var config = OneLayout(Pin("b", Brave));

        var gained = sync.Gained();

        Assert.Empty(gained);
        Assert.False(sync.AddTo(config, ["home"], gained));
        Assert.Equal([Brave], PinsOf(config));
        Assert.Equal([Brave, Outlook, Spotify], sync.Seen);
    }

    /// <summary>More than a few pins gained in one read are taken as they are, and only what comes after is added.</summary>
    [Fact]
    public void ManyPinsGainedAtOnceAreTakenAsTheyAre()
    {
        var store = new FakeStore(Brave);
        var sync = new WindowsPinSync(store, new FakeShell(), KeysOf, remembered: [Brave]);

        store.Pins = [Brave, Outlook, Spotify, Teams, Code];
        Assert.Empty(sync.Gained());
        Assert.Equal([Brave, Outlook, Spotify, Teams, Code], sync.Seen);

        store.Pins = [Brave, Outlook, Spotify, Teams, Code, Photos];
        Assert.Equal([Photos], sync.Gained());

        // As many as somebody might pin at once still count.
        store.Pins = [Brave];
        Assert.Empty(sync.Gained());
        store.Pins = [Brave, Outlook, Spotify, Teams];
        Assert.Equal([Outlook, Spotify, Teams], sync.Gained());
    }

    [Fact]
    public void TheMemoryIsWrittenWholeAndReadBack()
    {
        var path = Path.Combine(Config.LocalFolder, "windows-pins-test.txt");
        try
        {
            WindowsPinMemory.Write(path, [Brave, Outlook]);
            Assert.Equal([Brave, Outlook], WindowsPinMemory.Read(path));
            WindowsPinMemory.Write(path, []);
            Assert.Empty(WindowsPinMemory.Read(path)!);
            Assert.Empty(Directory.GetFiles(Config.LocalFolder, "windows-pins-test.txt.*.tmp"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    // Left empty by a crash.
    [InlineData("")]
    // Cut short: its last line never written.
    [InlineData("# Karpo: Windows' taskbar pins as last seen\nC:\\a.lnk\n")]
    // As the first build of the sync wrote it: no lines of its own.
    [InlineData("C:\\a.lnk\nC:\\b.lnk\n")]
    // NULs a write the disk never finished left.
    [InlineData("\0\0\0\0")]
    public void AMemoryNotWrittenWholeIsNoMemory(string text)
    {
        var path = Path.Combine(Config.LocalFolder, "windows-pins-test.txt");
        try
        {
            Directory.CreateDirectory(Config.LocalFolder);
            File.WriteAllText(path, text);
            Assert.Null(WindowsPinMemory.Read(path));
            Assert.Null(WindowsPinMemory.Read(path + ".missing"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // --- D: a read that isn't whole moves nothing -----------------------------------------------------

    /// <summary>A read that wasn't whole forgets nothing, so the next whole one finds nothing gained.</summary>
    [Fact]
    public void AShortReadThenAFullOneAddsNothing()
    {
        var store = new FakeStore(Brave, Outlook, Spotify);
        var sync = new WindowsPinSync(store, new FakeShell(), KeysOf);
        var config = OneLayout(Pin("b", Brave));

        store.Pins = null;
        Assert.Empty(sync.Gained());
        Assert.Equal([Brave, Outlook, Spotify], sync.Seen);
        store.Pins = [Brave, Outlook, Spotify];
        var gained = sync.Gained();

        Assert.Empty(gained);
        Assert.False(sync.AddTo(config, ["home"], gained));
        Assert.Equal([Brave], PinsOf(config));
    }

    /// <summary>With no memory and no whole read at the start, the first whole read is the start: it adds nothing.</summary>
    [Fact]
    public void NothingSeenYetTakesTheFirstWholeReadAsItIs()
    {
        var store = new FakeStore { Pins = null };
        var shell = new FakeShell();
        var sync = new WindowsPinSync(store, shell, KeysOf);
        Assert.False(sync.HasSeen);
        Assert.Empty(sync.Release(OneLayout(Pin("b", Brave)), OneLayout()));

        store.Pins = [Brave, Outlook];
        Assert.Empty(sync.Gained());

        Assert.True(sync.HasSeen);
        Assert.Equal([Brave, Outlook], sync.Seen);
        Assert.Empty(shell.Asked);
    }

    /// <summary>An entry of Explorer's pins the shell gives no name for makes the read not whole, rather than a pin short.</summary>
    [Fact]
    public void AnEntryWithoutANameMakesTheReadNotWhole()
    {
        var shortcut = new TaskbandFavorite([1], InAppsFolder: false);
        var app = new TaskbandFavorite([2], InAppsFolder: true);
        var other = new TaskbandFavorite([3], InAppsFolder: false);
        string? Named(byte[] pidl) => pidl[0] switch
        {
            1 => Brave,
            2 => "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify",
            // Named, but neither a shortcut nor a packaged app: left out, every time alike.
            _ => "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
        };

        Assert.Equal([Brave, Spotify], TaskbandFavorites.Targets([shortcut, app, other], Named));
        Assert.Null(TaskbandFavorites.Targets([shortcut, app], pidl => pidl[0] == 2 ? null : Named(pidl)));
        Assert.Null(TaskbandFavorites.Targets([shortcut, app], pidl => pidl[0] == 1 ? " " : Named(pidl)));
    }

    // --- The review layouts, and layouts with no pin ---------------------------------------------------

    private static Config WithReview()
    {
        var config = OneLayout(Pin("b", Brave));
        var folder = BarKind.FolderList.New();
        folder.Id = "wl-pin";
        Karpo.Core.Folders.Add(folder.Kind, folder.Folder!, Pin("o", Outlook).AsCell());
        config.Layouts.Add(new TaskbarLayout { Id = "widgets-list", Name = "List widgets", Islands = [folder, Pin("t", Teams)] });
        config.Repair();
        return config;
    }

    [Fact]
    public void TheReviewLayoutsGetNoWindowsPins()
    {
        var sync = new WindowsPinSync(new FakeStore(), new FakeShell(), KeysOf);
        var config = WithReview();

        Assert.True(sync.AddTo(config, ["home", "widgets-list"], [Code]));

        Assert.Equal([Brave, Code], PinsOf(config));
        Assert.Equal([Teams], PinsOf(config, "widgets-list"));
    }

    /// <summary>A pin the review layouts hold too comes off Windows once the person's own layouts let go of it.</summary>
    [Fact]
    public void TheReviewLayoutsHoldNoWindowsPins()
    {
        var store = new FakeStore(Brave, Outlook);
        var shell = new FakeShell();
        var sync = new WindowsPinSync(store, shell, KeysOf);
        // Outlook in the person's own layout, and in a folder of the review layout.
        var config = WithReview();
        config.Layouts[0].Islands.Insert(2, Pin("o", Outlook));
        var next = config.Clone();
        next.Layouts[0].Islands.RemoveAll(item => item.Id == "o");

        Assert.Equal([Outlook], sync.Release(config, next));
        Assert.Equal([Outlook], shell.Asked);
    }

    /// <summary>
    /// A layout a display shows gets a new Windows pin even when it holds no pin yet (the person,
    /// 2026-10-09: a pin made in Windows shows up on Karpo's bar as well); one no display shows does not.
    /// </summary>
    [Fact]
    public void ALayoutWithNoPinGetsItWhenShown()
    {
        var sync = new WindowsPinSync(new FakeStore(), new FakeShell(), KeysOf);
        var config = OneLayout();
        foreach (var id in new[] { "bare", "unseen" })
        {
            var clock = BarKind.NightLight.New();
            clock.Id = "c";
            config.Layouts.Add(new TaskbarLayout { Id = id, Name = id, Islands = [clock] });
        }
        config.Repair();

        Assert.True(sync.AddTo(config, ["home", "bare"], [Outlook]));

        Assert.Equal([Outlook], PinsOf(config));
        Assert.Equal([Outlook], PinsOf(config, "bare"));
        Assert.Empty(PinsOf(config, "unseen"));
    }

    // --- Follow: what the dock runs on a change Windows made --------------------------------------------

    /// <summary>
    /// Defaults run on for now (config.json locked at start) get no Windows pin: Windows' pins are
    /// not even read, so the pin gained meanwhile is still gained once the person's own file is in force.
    /// </summary>
    [Fact]
    public void ALockedConfigGetsNothingAndThePinWaitsForTheRealOne()
    {
        var store = new FakeStore(Brave);
        var sync = new WindowsPinSync(store, new FakeShell(), KeysOf);
        Files.Start(start => PinsAre(start, Brave));
        Config waiting;
        using (new FileStream(Config.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            waiting = Config.Load();
        }
        Assert.True(waiting.StandsIn);
        store.Pins = [Brave, Outlook];

        Assert.Null(sync.Follow(waiting, [waiting.DefaultLayoutId!]));
        Assert.Equal([Brave], sync.Seen);

        Assert.Equal(ConfigReload.Changed, waiting.Reload(out var real));
        var next = sync.Follow(real, [real.DefaultLayout.Id]);
        Assert.NotNull(next);
        Assert.Equal([Brave, Outlook], next.DefaultLayout.Islands.Where(item => item.Kind == BarKind.Pin).Select(item => item.Pin?.Target));
    }

    /// <summary>A read that isn't whole, and a memory left empty, add nothing through Follow either, and remove nothing.</summary>
    [Fact]
    public void FollowAddsNothingOnAShortReadOrAnEmptyMemory()
    {
        var store = new FakeStore(Brave, Outlook);
        var sync = new WindowsPinSync(store, new FakeShell(), KeysOf, remembered: []);
        var config = OneLayout(Pin("b", Brave), Pin("t", Teams));

        Assert.Null(sync.Follow(config, ["home"]));
        store.Pins = null;
        Assert.Null(sync.Follow(config, ["home"]));
        store.Pins = [Brave];
        Assert.Null(sync.Follow(config, ["home"]));
        store.Pins = [Brave, Outlook];
        // Outlook again after Windows lost it is gained: somebody pinned it once more.
        Assert.Equal([Brave, Teams, Outlook], PinsOf(sync.Follow(config, ["home"])!));
        Assert.Equal([Brave, Teams], PinsOf(config));
    }
}
