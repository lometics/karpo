// What a folder on this PC holds, read through the folder source (Karpo.Dock\Drives\FolderContents.cs)
// over made-up folders (FakeFolders): the drive flyout's order, what is left out, the bound, the words for
// every state a folder can be in, the icons asked for once a kind, and a run of changes read once —
// without a disk.

using Karpo.Dock.Drives;

namespace Karpo.Tests.Folders;

public sealed class FolderReadingTests
{
    private const string Projects = @"C:\Users\Ann\Projects";

    private static FolderContents Read(FakeFolders folders, string path, int most = FolderReader.MostListed) =>
        FolderReader.List(new FolderRequest(path, most, 40), folders.Source);

    /// <summary>Folders first, then files, names as a person reads them: "notes 2" before "notes 10".</summary>
    [Fact]
    public void AFolderIsListedInTheDriveFlyoutsOrder()
    {
        var read = Read(FakeFolders.Projects(), Projects);

        Assert.Equal(FolderListState.Listed, read.State);
        Assert.Equal(
            ["Karpo", "Notes", "Receipts 2026", "Website", "budget.xlsx", "Holiday plan.docx", "logo.png", "Meeting notes 2.md", "Meeting notes 10.md", "setup.exe"],
            read.Entries.Select(entry => entry.Name));
        Assert.All(read.Entries.Take(4), entry => Assert.True(entry.Folder));
        Assert.All(read.Entries.Skip(4), entry => Assert.False(entry.Folder));
        Assert.Equal(10, read.Count);
        Assert.Equal(0, read.Unlisted);
    }

    /// <summary>What File Explorer hides by default is left out: hidden files and the system's own.</summary>
    [Fact]
    public void HiddenAndSystemItemsAreLeftOut()
    {
        var folders = new FakeFolders().With(Projects, "plan.txt")
            .Hidden(Projects, "desktop.ini", FileAttributes.Hidden | FileAttributes.System)
            .Hidden(Projects, ".secret", FileAttributes.Hidden)
            .Hidden(Projects, "thumbs.db", FileAttributes.System);

        Assert.Equal(["plan.txt"], Read(folders, Projects).Entries.Select(entry => entry.Name));

        var nothingShown = new FakeFolders().With(Projects).Hidden(Projects, "desktop.ini", FileAttributes.Hidden);
        Assert.Equal(FolderListState.Empty, Read(nothingShown, Projects).State);
    }

    /// <summary>
    /// A list asks for everything a read walks (FolderReader.MostListed), so a folder of 130 is listed whole and its line
    /// scrolls (§461); a read asked for fewer — the bar's buttons — lists that many, and the rest are only counted.
    /// </summary>
    [Fact]
    public void AListIsListedWholeAndFewerAreCounted()
    {
        var names = Enumerable.Range(1, 130).Select(index => $"photo {index}.jpg").ToArray();
        var whole = Read(new FakeFolders().With(Projects, names), Projects);
        Assert.Equal(130, whole.Entries.Count);
        Assert.Equal(0, whole.Unlisted);
        Assert.Equal("photo 130.jpg", whole.Entries[^1].Name);

        var read = Read(new FakeFolders().With(Projects, names), Projects, most: 100);
        Assert.Equal(100, read.Entries.Count);
        Assert.Equal(30, read.Unlisted);
        Assert.Equal(130, read.Count);
        Assert.Equal("photo 1.jpg", read.Entries[0].Name);
        Assert.Equal("photo 100.jpg", read.Entries[^1].Name);
    }

    /// <summary>
    /// Every way a folder can fail is a state with words of its own, never an exception: none chosen,
    /// one that is not there, one this account may not read, a share that does not answer.
    /// </summary>
    [Fact]
    public void EveryFailureIsAStateWithWords()
    {
        var folders = new FakeFolders()
            .With(@"C:\Empty")
            .Failing(@"C:\Locked", new UnauthorizedAccessException("Access to the path is denied."))
            .Failing(@"\\server\share", new IOException("The network name cannot be found. Try again."))
            .Failing(@"E:\Card", new IOException("The device is not ready.", unchecked((int)0x80070015)));

        var cases = new (string Path, FolderListState State, string Line, string Word)[]
        {
            ("", FolderListState.Unset, "Choose a folder in its settings", "No folder"),
            (@"C:\Empty", FolderListState.Empty, "This folder is empty", "Empty"),
            (@"C:\Gone", FolderListState.Missing, "This folder isn't there", "Not there"),
            (@"C:\Locked", FolderListState.Denied, "You don't have permission to see what's in this folder", "No access"),
            (@"\\server\share", FolderListState.Failed, "The network name cannot be found", "Can't read"),
            (@"E:\Card", FolderListState.Missing, "This folder isn't there", "Not there"),
        };
        foreach (var (path, state, line, word) in cases)
        {
            var read = Read(folders, path);
            Assert.Equal(state, read.State);
            Assert.Equal(line, FolderReader.StateLine(read));
            Assert.Equal(word, FolderReader.StateWord(read));
            Assert.Empty(read.Entries);
        }
        Assert.Equal("Reading…", FolderReader.StateLine(FolderContents.Reading(Projects)));
    }

    /// <summary>The shell is asked for one icon a kind, by the first item of the kind, as the drive flyout asks.</summary>
    [Fact]
    public void AnIconIsAskedForOnceAKind()
    {
        var folders = new FakeFolders().With(Projects, "a\\", "b\\", "one.txt", "two.txt", "three.TXT", "sheet.xlsx", "setup.exe", "tool.exe");
        var read = Read(folders, Projects);

        var icons = FolderReader.Icons(new FolderRequest(Projects, 100, 40), read.Entries, folders.Source);

        // One for the folders, one for .txt, one for .xlsx, and each program its own.
        Assert.Equal(5, folders.Icons);
        Assert.Equal(5, icons.Count);
        Assert.Contains(DriveListing.FolderIcon, icons.Keys);
        Assert.Contains(".txt", icons.Keys);
        Assert.Contains("setup.exe", icons.Keys);
        Assert.Contains("tool.exe", icons.Keys);
    }

    /// <summary>What a folder and an item are called, and how many a folder holds, as File Explorer says it.</summary>
    [Fact]
    public void TheWordsAreFileExplorers()
    {
        Assert.Equal(@"C:\Users\Ann\Projects\notes.md", FolderReader.PathOf(Projects, "notes.md"));
        Assert.Equal(@"D:\notes.md", FolderReader.PathOf(@"D:\", "notes.md"));
        Assert.Equal("Projects", FolderReader.NameOf(Projects + "\\"));
        Assert.Equal(@"D:\", FolderReader.NameOf(@"D:\"));
        Assert.Equal("share", FolderReader.NameOf(@"\\server\share"));
        Assert.Equal("Empty", FolderReader.CountOf(0));
        Assert.Equal("1 item", FolderReader.CountOf(1));
        Assert.Equal("12 items", FolderReader.CountOf(12));
    }

    /// <summary>
    /// A run of changes — a copy of a hundred files — is one read, a moment after the last of them,
    /// and nothing at all once the watch has stopped.
    /// </summary>
    [Fact]
    public void ARunOfChangesIsOneRead()
    {
        var folders = FakeFolders.Projects();
        var settled = 0;
        using (var watch = new FolderWatch(folders.Source, Projects, () => Interlocked.Increment(ref settled)))
        {
            Assert.True(watch.Watching);
            Assert.Equal([Projects], folders.Watched);
            for (var change = 0; change < 100; change++)
            {
                folders.Change(Projects, "a.txt");
            }
            Assert.True(Patience.Until(() => Volatile.Read(ref settled) > 0), "the folder was never read again");
            Thread.Sleep(FolderWatch.SettleMs * 2);
            Assert.Equal(1, Volatile.Read(ref settled));
        }
        Assert.Empty(folders.Watched);
        folders.Change(Projects, "b.txt");
        Thread.Sleep(FolderWatch.SettleMs * 2);
        Assert.Equal(1, Volatile.Read(ref settled));
        // No folder chosen: nothing to watch.
        using var none = new FolderWatch(folders.Source, "", () => { });
        Assert.False(none.Watching);
    }
}
