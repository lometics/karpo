// §658: Start's source on the dock (Karpo.Dock\StartWidgets\StartStore.cs) over reads of the test's own (StartReads): the
// first line shown reads Windows at once, whatever the clock says — until §658 the "read a minute ago" test overflowed on
// its long.MinValue start and the store never read, so All apps listed nothing and Windows search's home had no apps, files
// or folders (docs\widgets.md §658) —; a line shown within the minute reads nothing again, one after it does; what it read
// is said (Changed, Version). No test reads this PC's apps, files or account.

using Karpo.Dock;
using Karpo.Dock.StartWidgets;

namespace Karpo.Tests.Folders;

public sealed class StartStoreTests
{
    private sealed class Reads
    {
        public long Ticks;
        public int Read;
        public readonly List<Action> Work = [];

        public StartReads Of() => new(
            () =>
            {
                Read++;
                return [new InstalledApp("Paint", @"shell:AppsFolder\Microsoft.Paint_8wekyb3d8bbwe!App"), new InstalledApp("Notepad", @"shell:AppsFolder\Microsoft.WindowsNotepad_8wekyb3d8bbwe!App")];
            },
            () => [new RecentItem(@"C:\Users\me\Documents\plan.docx", new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc), Folder: false), new RecentItem(@"C:\Users\me\Pictures", new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc), Folder: true)],
            () => StartSamples.SampleAccount,
            () => false,
            () => new QuickLinkMachine(false, false, true, false),
            Work.Add,
            () => Ticks);

        public void Pump()
        {
            var those = Work.ToList();
            Work.Clear();
            those.ForEach(work => work());
        }
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(12_345_678L)]
    [InlineData(long.MaxValue / 2)]
    public void TheFirstLineShownReadsWindowsAtAnyClock(long ticks)
    {
        var reads = new Reads { Ticks = ticks };
        var store = new StartStore(reads.Of());
        var said = 0;
        store.Changed += () => said++;

        store.Refresh();
        // Off the caller's thread: handed to the reader, nothing read on the asking thread.
        Assert.Single(reads.Work);
        Assert.Empty(store.Apps);
        reads.Pump();

        Assert.Equal(["Paint", "Notepad"], store.Apps.Select(app => app.Name));
        Assert.Equal(["plan.docx"], store.Files.Select(file => file.Name));
        Assert.Equal(["Pictures"], store.Folders.Select(folder => folder.Name));
        Assert.Equal(1, said);
        Assert.Equal(1, store.Version);
    }

    [Fact]
    public void ALineShownWithinTheMinuteReadsNothingAgainOneAfterItDoes()
    {
        var reads = new Reads { Ticks = 1_000_000 };
        var store = new StartStore(reads.Of());
        store.Refresh();
        // A second line shown while the first read runs asks nothing more.
        store.Refresh();
        reads.Pump();
        Assert.Equal(1, reads.Read);

        reads.Ticks += 59_000;
        store.Refresh();
        Assert.Empty(reads.Work);

        reads.Ticks += 2_000;
        store.Refresh();
        reads.Pump();
        Assert.Equal(2, reads.Read);
    }
}
