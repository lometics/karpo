// pins (§380; 2026-10-28, the person: "what it opens, for the pins should show the pin icons and the proper
// names, not the weird ass names"; "what it opens should also show a list of apps if possible"): what a
// pin's and a folder's "What it opens" shows, asked of this PC's shell (Karpo.Dock\TargetLooks.cs): each
// target by the name Windows gives it — a shortcut without ".lnk", an app as Start names it, a known
// folder as File Explorer does — the apps as Start's All apps lists them, and each one's picture, fetched
// off the menu's thread. Nothing is opened and nothing of the person's is written: a shortcut of the
// test's own is made in a folder of its own.

using System.Runtime.InteropServices;
using Karpo.Dock;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Pinning;

public sealed unsafe class TargetLooksTests
{
    private const string Apps = @"shell:AppsFolder\";

    /// <summary>A shortcut to Notepad at <paramref name="path"/>, made as File Explorer makes one.</summary>
    private static void Shortcut(string path)
    {
        var clsid = new Guid("00021401-0000-0000-C000-000000000046");
        IShellLinkW* link = null;
        Marshal.ThrowExceptionForHR(CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IShellLinkW>(), (void**)&link));
        try
        {
            fixed (char* notepad = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "notepad.exe"))
            {
                Marshal.ThrowExceptionForHR(link->SetPath(notepad));
            }
            IPersistFile* file = null;
            Marshal.ThrowExceptionForHR(link->QueryInterface(__uuidof<IPersistFile>(), (void**)&file));
            try
            {
                fixed (char* at = path)
                {
                    Marshal.ThrowExceptionForHR(file->Save(at, BOOL.TRUE));
                }
            }
            finally
            {
                file->Release();
            }
        }
        finally
        {
            link->Release();
        }
    }

    /// <summary>
    /// A shortcut is named as File Explorer names it, without ".lnk"; a web address by its host; a folder
    /// by its name; This PC and Downloads as File Explorer says them, never "shell:…".
    /// </summary>
    [Fact]
    public void EveryTargetIsNamedAsWindowsNamesIt()
    {
        var root = Directory.CreateTempSubdirectory("karpo-target-looks-").FullName;
        try
        {
            var link = Path.Combine(root, "My Notes.lnk");
            Shortcut(link);
            Assert.Equal("My Notes", TargetLooks.Name(link));
            var folder = Directory.CreateDirectory(Path.Combine(root, "Receipts 2026")).FullName;
            Assert.Equal("Receipts 2026", TargetLooks.Name(folder));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
        Assert.Equal("example.com", TargetLooks.Name("https://example.com/news"));
        foreach (var place in new[] { "shell:MyComputerFolder", "shell:Downloads" })
        {
            var name = TargetLooks.Name(place);
            Assert.False(name.Length == 0 || name.Contains(':') || name.Contains('\\') || name.Contains("Folder", StringComparison.Ordinal) && name.Contains("Computer", StringComparison.Ordinal), $"{place} is named \"{name}\"");
        }
    }

    /// <summary>
    /// The apps, as Start's All apps lists them: each by its name — never its ID — opened through the
    /// Apps folder, each once, by name; File Explorer among them, and a pin to one named as the list names it.
    /// </summary>
    [Fact]
    public void TheAppsAreStartsAllApps()
    {
        var apps = InstalledApps.List();
        Assert.True(apps.Count > 5, $"only {apps.Count} apps");
        Assert.All(apps, app =>
        {
            Assert.StartsWith(Apps, app.Target, StringComparison.OrdinalIgnoreCase);
            Assert.False(app.Name.Length == 0 || app.Name.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) || app.Name.Contains('!'), $"\"{app.Name}\" for {app.Target}");
        });
        Assert.Equal(apps.Count, apps.Select(app => app.Target).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(apps.Select(app => app.Name), apps.Select(app => app.Name).Order(StringComparer.CurrentCultureIgnoreCase));
        var explorer = Assert.Single(apps, app => string.Equals(app.Target, Apps + "Microsoft.Windows.Explorer", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(explorer.Name, TargetLooks.Name(explorer.Target));
    }

    /// <summary>
    /// What a picker is offered: the folders first — Windows' own among them, by File Explorer's names —
    /// and the apps, each with its picture's address; and a picture comes, off the asking thread.
    /// </summary>
    [Fact]
    public void ThePickerIsOfferedFoldersAndAppsWithTheirPictures()
    {
        var choices = TargetLooks.Choices(new Config());
        var downloads = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\Downloads");
        Assert.Contains(choices.Folders, row => string.Equals((string)row.Value!, downloads, StringComparison.OrdinalIgnoreCase) && row.Title == TargetLooks.Name(downloads));
        Assert.NotEmpty(choices.Apps);
        Assert.All(choices.Folders.Concat(choices.Apps), row => Assert.Equal(PinTargets.PictureOf((string)row.Value!), row.Picture));

        using var came = new ManualResetEventSlim();
        var address = PinTargets.PictureOf(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        Assert.True(TargetPictures.Owns(address));
        Assert.False(TargetPictures.Owns("https://a.espncdn.com/i/teamlogos/nba/500/lal.png"));
        var first = TargetPictures.Shared.Get(address, came.Set);
        Assert.True(first is not null || came.Wait(TimeSpan.FromSeconds(10)), "the picture never came");
        Assert.NotNull(TargetPictures.Shared.Get(address, () => { }));

        // §425: This PC among Windows' places, by File Explorer's name; the files used lately, each a file that is there.
        Assert.Contains(choices.Places, row => (string)row.Value! == "shell:MyComputerFolder" && row.Title == TargetLooks.Name("shell:MyComputerFolder"));
        Assert.All(choices.Files, row => Assert.True(File.Exists((string)row.Value!), (string)row.Value!));
        Assert.True(choices.Files.Count <= TargetLooks.MostRecent);

        // The apps wear their own icons, as Start draws them.
        var apps = choices.Apps.Take(8).ToList();
        var drawn = apps.Count(app => SpinWait.SpinUntil(() => TargetPictures.Shared.Get(app.Picture, () => { }) is not null, 5000));
        Assert.True(drawn >= apps.Count - 1, $"only {drawn} of {apps.Count} apps' pictures came: {string.Join(", ", apps.Select(app => app.Title))}");
    }

    /// <summary>
    /// §425: a site's picture is fetched from the site, so it may come after it was first asked for (a website typed):
    /// it is asked again when a site's picture arrives (Again), and the row waiting for it is told then. A file's
    /// none stays none.
    /// </summary>
    [Fact]
    public void ASitesPictureThatComesLaterIsShown()
    {
        var fetched = false;
        var reads = 0;
        var picture = new Pixels { Data = new byte[4 * 4 * 4], Width = 4, Height = 4 };
        var pictures = new TargetPictures((target, _) =>
        {
            Interlocked.Increment(ref reads);
            return target.StartsWith("https://", StringComparison.Ordinal) && Volatile.Read(ref fetched) ? picture : null;
        });
        var site = PinTargets.PictureOf("https://example.com");
        var file = PinTargets.PictureOf(@"C:\nothing.txt");
        using var came = new ManualResetEventSlim();
        Assert.Null(pictures.Get(site, came.Set));
        Assert.Null(pictures.Get(file, () => { }));
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref reads) == 2, 5000));
        // Asked again before it has come: nothing read again, nothing told.
        Assert.Null(pictures.Get(site, came.Set));
        Thread.Sleep(50);
        Assert.Equal(2, Volatile.Read(ref reads));

        Volatile.Write(ref fetched, true);
        pictures.Again();
        Assert.True(came.Wait(TimeSpan.FromSeconds(5)), "the site's picture was never told");
        Assert.Same(picture, pictures.Get(site, () => { }));
        Assert.Equal(3, Volatile.Read(ref reads));
    }

    /// <summary>A line: what it asks with is its own method, a new delegate at every drawing, as WinSearchLine's and StartLine's Arrived.</summary>
    private sealed class Line
    {
        private int told;

        public int Told => Volatile.Read(ref told);

        public void Arrived() => Interlocked.Increment(ref told);
    }

    /// <summary>
    /// §658: an ask never waits for the read, and a picture coming tells each line waiting once, however often it asked while
    /// it came. Before, every ask added a waiter: a line asks at every drawing, so the first search's icons woke it thousands
    /// of times and the dock froze.
    /// </summary>
    [Fact]
    public void AnAskNeverWaitsForTheReadAndAPictureComingTellsItsLineOnce()
    {
        var patience = TimeSpan.FromSeconds(10);
        using var release = new ManualResetEventSlim();
        using var reading = new ManualResetEventSlim();
        var reads = 0;
        var pictures = new TargetPictures((_, _) =>
        {
            Interlocked.Increment(ref reads);
            reading.Set();
            return release.Wait(patience) ? new Pixels { Data = new byte[4], Width = 1, Height = 1 } : null;
        });
        var line = new Line();
        var other = new Line();
        var address = PinTargets.PictureOf(@"C:\Users\me\Documents\plan.docx");

        // The line drawn again and again while its picture comes (the read holding): each ask returns at once, with nothing.
        for (var drawn = 0; drawn < 50; drawn++)
        {
            Assert.Null(pictures.Get(address, line.Arrived));
        }
        Assert.Null(pictures.Get(address, other.Arrived));
        Assert.True(reading.Wait(patience));
        release.Set();
        Assert.True(SpinWait.SpinUntil(() => line.Told >= 1 && other.Told >= 1, patience));
        Thread.Sleep(50);
        Assert.Equal(1, Volatile.Read(ref reads));
        Assert.Equal(1, line.Told);
        Assert.Equal(1, other.Told);
        Assert.NotNull(pictures.Get(address, line.Arrived));
    }

    /// <summary>
    /// §658: where Windows keeps no recent items (the person turned them off: an empty Recent folder, or none), what changed
    /// last on the Desktop, in Documents and Downloads stands in, files and folders alike, newest first, nothing hidden.
    /// </summary>
    [Fact]
    public void WithoutRecentItemsWhatChangedLastStandsIn()
    {
        var root = Directory.CreateTempSubdirectory("karpo-recent-").FullName;
        try
        {
            var recent = Directory.CreateDirectory(Path.Combine(root, "Recent")).FullName;
            File.WriteAllText(Path.Combine(recent, "desktop.ini"), "");
            var documents = Directory.CreateDirectory(Path.Combine(root, "Documents")).FullName;
            var downloads = Directory.CreateDirectory(Path.Combine(root, "Downloads")).FullName;
            var at = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
            string Made(string folder, string name, int hours, bool isFolder = false)
            {
                var path = Path.Combine(folder, name);
                if (isFolder)
                {
                    Directory.CreateDirectory(path);
                    Directory.SetLastWriteTimeUtc(path, at.AddHours(hours));
                }
                else
                {
                    File.WriteAllText(path, name);
                    File.SetLastWriteTimeUtc(path, at.AddHours(hours));
                }
                return path;
            }
            var plan = Made(documents, "plan.docx", 1);
            var trip = Made(documents, "Trip", 3, isFolder: true);
            var setup = Made(downloads, "setup.exe", 2);
            var hidden = Made(documents, "secret.txt", 9);
            File.SetAttributes(hidden, FileAttributes.Hidden);
            _ = Made(documents, ".git", 8, isFolder: true);
            _ = Made(documents, "desktop.ini", 7);

            var items = RecentFiles.Read(recent, [documents, downloads, Path.Combine(root, "gone")]);
            Assert.Equal([trip, setup, plan], items.Select(item => item.Path));
            Assert.Equal([true, false, false], items.Select(item => item.Folder));
            Assert.Equal(at.AddHours(3), items[0].UsedUtc);
            // No Recent folder at all: the same.
            Assert.Equal([trip, setup, plan], RecentFiles.Read(Path.Combine(root, "no recent"), [documents, downloads]).Select(item => item.Path));

            // §658: where Windows keeps some, they alone: nothing stands in.
            Shortcut(Path.Combine(recent, "notepad.lnk"));
            var kept = RecentFiles.Read(recent, [documents, downloads]);
            Assert.Equal([Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "notepad.exe")], kept.Select(item => item.Path), StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// §661: the icons Windows search and Start draw are each file's own, read at the size they are drawn: kept by the target
    /// and the shell's size (a row's 24 px and a tile's 48 two reads, two pictures; 22 and 24 px one), every file its own —
    /// two programs of one kind (.exe) never one picture —, a path's case aside.
    /// </summary>
    [Fact]
    public void EachFilesIconIsReadAtTheSizeItIsDrawnAndKeptByTheFileAndTheSize()
    {
        Assert.Equal(16, TargetPictures.Bucket(16));
        Assert.Equal(24, TargetPictures.Bucket(21.5f));
        Assert.Equal(24, TargetPictures.Bucket(24.4f));
        Assert.Equal(48, TargetPictures.Bucket(48));
        Assert.Equal(96, TargetPictures.Bucket(72));
        Assert.Equal(256, TargetPictures.Bucket(900));

        var read = new System.Collections.Concurrent.ConcurrentQueue<(string Target, int Px)>();
        var pictures = new TargetPictures((target, px) =>
        {
            read.Enqueue((target, px));
            return new Pixels { Data = new byte[px * px * 4], Width = px, Height = px };
        });
        const string Word = @"C:\Users\me\Documents\plan.docx";
        const string Setup = @"C:\Users\me\Downloads\setup.exe";
        const string Tool = @"C:\Tools\tool.exe";
        Pixels? Wait(string target, float px)
        {
            using var came = new ManualResetEventSlim();
            var address = PinTargets.PictureOf(target);
            if (pictures.Get(address, came.Set, px) is { } now)
            {
                return now;
            }
            Assert.True(came.Wait(TimeSpan.FromSeconds(5)), $"{target} at {px} px never came");
            return pictures.Get(address, () => { }, px);
        }

        Assert.Equal(24, Wait(Word, 24)!.Width);
        Assert.Equal(48, Wait(Word, 48)!.Width);
        Assert.Equal(24, Wait(Word.ToUpperInvariant(), 22)!.Width);
        Assert.Equal(24, Wait(Setup, 24)!.Width);
        Assert.Equal(24, Wait(Tool, 24)!.Width);
        Assert.Equal([(Word, 24), (Word, 48), (Setup, 24), (Tool, 24)], read.ToArray());
    }
}
