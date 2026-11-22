// §557: what Karpo holds open on a drive, let go of when Windows asks for the drive (Karpo.Dock\Drives\DriveHolds.cs),
// and the eject itself (Launch.Eject). The person, 2026-11-07: "ejecting a flash drive in karpo does not work".
//
//   * The eject asks the drive's own menu for its Eject (SEE_MASK_INVOKEIDLIST): "eject" is no verb of the registry's,
//     and without it ShellExecuteEx failed with ERROR_NO_ASSOCIATION — nothing was ever ejected. It is no launch: it
//     closes nothing of Karpo's.
//   * A folder followed on a drive is held by that drive's volume, registered once for all the folders on it.
//   * Asked to let go (a removal's query, a lock, a dismount), every watch on the volume stops and the registration's
//     handle closes, its registration kept; a removal that fails takes them back, and reads the folder again.
//   * A drive that went — asked or pulled out — stays let go of until it arrives again; a watch started while its
//     drive was away is taken back by the drive's arrival.
//   * What Windows says is read off its message: the registration it is about, and what it asks.
//   * The disk graph keeps no disk open between its samples (DiskSampler).
//
// Nothing here touches a real volume: the folders' watchers and the volumes' handles are made up (FakeNotices,
// Source), and nothing is ejected — Launch is stood in for.

using System.Reflection;
using Karpo.Dock;
using Karpo.Dock.Drives;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Storage;

// With the other tests that stand in for the drives' verbs (DriveWatch.Verb).
[Collection(AttachedDrivesCollection.Collection)]
public sealed class DriveHoldsTests
{
    private const uint SEE_MASK_INVOKEIDLIST = 0x0000000C;
    private const uint SEE_MASK_NOASYNC = 0x00000100;

    // --- The eject itself -------------------------------------------------------------------------------------

    /// <summary>
    /// Properties and Eject are verbs of the item's own menu, which ShellExecuteEx only finds when told to ask it; the
    /// registry's verbs and the default are not. The eject's own thread waits for the verb to have run.
    /// </summary>
    [Theory]
    [InlineData("eject", true)]
    [InlineData("Eject", true)]
    [InlineData("properties", true)]
    [InlineData("open", false)]
    [InlineData("runas", false)]
    [InlineData(null, false)]
    public void AnItemsOwnVerbIsAskedOfItsMenu(string? verb, bool menu)
    {
        Assert.Equal(menu, (Launch.Shell.Mask(verb) & SEE_MASK_INVOKEIDLIST) == SEE_MASK_INVOKEIDLIST);
        Assert.Equal(0u, Launch.Shell.Mask(verb) & SEE_MASK_NOASYNC);
        Assert.Equal(SEE_MASK_NOASYNC, Launch.Shell.Mask(verb, waits: true) & SEE_MASK_NOASYNC);
    }

    /// <summary>An eject hands the drive's root to the shell's Eject, and closes nothing of Karpo's: nothing opens.</summary>
    [Fact]
    public void AnEjectIsTheShellsOwnAndClosesNothing()
    {
        using var stand = Launch.Stand();
        Launch.Eject(@"E:\");
        var launched = Assert.IsType<Launched>(Assert.Single(stand.Said));
        Assert.Equal((LaunchKind.Eject, @"E:\", (string?)"eject"), (launched.Kind, launched.Target, launched.Verb));
    }

    /// <summary>A drive's Eject, from a menu or a removable device's button, goes to the launcher's.</summary>
    [Fact]
    public void ADrivesEjectGoesThroughTheLauncher()
    {
        using var stand = Launch.Stand();
        DriveWatch.Eject(@"F:\");
        DriveWatch.Eject("");
        var launched = Assert.IsType<Launched>(Assert.Single(stand.Said));
        Assert.Equal((LaunchKind.Eject, @"F:\"), (launched.Kind, launched.Target));
    }

    // --- Pure rules ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(@"E:\Photos\2026", @"E:\")]
    [InlineData(@"e:\", @"E:\")]
    [InlineData("e:", @"E:\")]
    [InlineData("E:/Photos", @"E:\")]
    [InlineData(@"\\server\share\docs", "")]
    [InlineData(@"\\?\Volume{aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee}\", "")]
    [InlineData("Photos", "")]
    [InlineData("", "")]
    [InlineData("E:Photos", "")]
    public void AFolderIsHeldByTheDriveItIsOn(string path, string root)
    {
        Assert.Equal(root, DriveHolds.RootOf(path));
    }

    [Fact]
    public void WhatEachChangeAsks()
    {
        Assert.Equal(DriveNotice.LetGo, DriveHolds.Meaning(DriveHolds.DBT_DEVICEQUERYREMOVE, Guid.Empty));
        Assert.Equal(DriveNotice.TakeBack, DriveHolds.Meaning(DriveHolds.DBT_DEVICEQUERYREMOVEFAILED, Guid.Empty));
        Assert.Equal(DriveNotice.Gone, DriveHolds.Meaning(DriveHolds.DBT_DEVICEREMOVEPENDING, Guid.Empty));
        Assert.Equal(DriveNotice.Gone, DriveHolds.Meaning(DriveHolds.DBT_DEVICEREMOVECOMPLETE, Guid.Empty));
        Assert.Equal(DriveNotice.Arrived, DriveHolds.Meaning(DriveHolds.DBT_DEVICEARRIVAL, Guid.Empty));
        Assert.Equal(DriveNotice.LetGo, DriveHolds.Meaning(DriveHolds.DBT_CUSTOMEVENT, DriveHolds.VolumeLock));
        Assert.Equal(DriveNotice.LetGo, DriveHolds.Meaning(DriveHolds.DBT_CUSTOMEVENT, DriveHolds.VolumeDismount));
        Assert.Equal(DriveNotice.TakeBack, DriveHolds.Meaning(DriveHolds.DBT_CUSTOMEVENT, DriveHolds.VolumeLockFailed));
        Assert.Equal(DriveNotice.TakeBack, DriveHolds.Meaning(DriveHolds.DBT_CUSTOMEVENT, DriveHolds.VolumeUnlock));
        Assert.Equal(DriveNotice.TakeBack, DriveHolds.Meaning(DriveHolds.DBT_CUSTOMEVENT, DriveHolds.VolumeDismountFailed));
        Assert.Equal(DriveNotice.TakeBack, DriveHolds.Meaning(DriveHolds.DBT_CUSTOMEVENT, DriveHolds.VolumeMount));
        Assert.Equal(DriveNotice.None, DriveHolds.Meaning(DriveHolds.DBT_CUSTOMEVENT, Guid.NewGuid()));
        Assert.Equal(DriveNotice.None, DriveHolds.Meaning(0x0007, Guid.Empty));
    }

    /// <summary>
    /// What Windows' message says, read off it: the registration a handle's change is about and what it asks, a
    /// custom event by its GUID; a change about anything but a handle is none of the holds', an arrival any device's.
    /// </summary>
    [Fact]
    public unsafe void TheMessageIsReadForItsRegistration()
    {
        var said = new DriveHolds.BroadcastHandle
        {
            Size = (uint)sizeof(DriveHolds.BroadcastHandle),
            DeviceType = DriveHolds.DBT_DEVTYP_HANDLE,
            Handle = 7,
            Notify = 42,
            EventGuid = DriveHolds.VolumeLock,
        };
        var at = (LPARAM)(nint)(&said);
        Assert.Equal((DriveNotice.LetGo, (nint)42), DriveHolds.Read((WPARAM)DriveHolds.DBT_DEVICEQUERYREMOVE, at));
        Assert.Equal((DriveNotice.LetGo, (nint)42), DriveHolds.Read((WPARAM)DriveHolds.DBT_CUSTOMEVENT, at));
        Assert.Equal((DriveNotice.Gone, (nint)42), DriveHolds.Read((WPARAM)DriveHolds.DBT_DEVICEREMOVECOMPLETE, at));
        said.DeviceType = 2; // DBT_DEVTYP_VOLUME: a letter came or went, the islands' news.
        Assert.Equal((DriveNotice.None, (nint)0), DriveHolds.Read((WPARAM)DriveHolds.DBT_DEVICEREMOVECOMPLETE, at));
        Assert.Equal((DriveNotice.Arrived, (nint)0), DriveHolds.Read((WPARAM)DriveHolds.DBT_DEVICEARRIVAL, at));
        Assert.Equal((DriveNotice.None, (nint)0), DriveHolds.Read((WPARAM)DriveHolds.DBT_DEVICEQUERYREMOVE, (LPARAM)0));
    }

    // --- The holds ----------------------------------------------------------------------------------------------

    /// <summary>Every folder on a drive is held by its volume, registered once for all of them, and let go of with the last.</summary>
    [Fact]
    public void AVolumeIsRegisteredOnceForAllItsFolders()
    {
        var (folders, notices, holds) = Made();
        var photos = new FolderWatch(folders.Source, @"E:\Photos", () => { });
        var music = new FolderWatch(folders.Source, @"E:\Music", () => { });
        var documents = new FolderWatch(folders.Source, @"C:\Users\Public\Documents", () => { });
        Assert.Equal([@"E:\", @"C:\"], notices.Followed.Select(entry => entry.Root));
        Assert.Equal((2, false), Watches(holds, @"E:\"));
        photos.Dispose();
        Assert.Empty(notices.Ended);
        music.Dispose();
        Assert.Equal([Notice(notices, @"E:\")], notices.Ended);
        Assert.Null(holds.Of(@"E:\"));
        documents.Dispose();
        Assert.Empty(folders.Live);
    }

    /// <summary>
    /// Asked to let the volume go, every watch on it stops — its watcher, the handle that would stop the eject — and so
    /// does the handle the registration was made with, the registration kept to hear the outcome. The question is
    /// answered: it was about something held. One about a volume held by nothing is not.
    /// </summary>
    [Fact]
    public void AskedForTheDriveEverythingOnItLetsGo()
    {
        var (folders, notices, holds) = Made();
        using var photos = new FolderWatch(folders.Source, @"E:\Photos", () => { });
        using var music = new FolderWatch(folders.Source, @"E:\Music", () => { });
        using var documents = new FolderWatch(folders.Source, @"C:\Users\Public\Documents", () => { });
        var stick = Notice(notices, @"E:\");
        Assert.True(holds.Heard(DriveNotice.LetGo, stick));
        Assert.False(photos.Watching);
        Assert.False(music.Watching);
        Assert.True(documents.Watching);
        Assert.Equal([@"C:\Users\Public\Documents"], folders.Live);
        Assert.Equal([stick], notices.LetGone);
        Assert.Empty(notices.Ended);
        Assert.False(holds.Heard(DriveNotice.LetGo, 999));
        Assert.False(holds.Heard(DriveNotice.None, stick));
    }

    /// <summary>A removal that failed — another program said no — takes everything back, registered anew, and reads the folders again.</summary>
    [Fact]
    public void ARemovalThatFailedTakesItBack()
    {
        var (folders, notices, holds) = Made();
        var settled = 0;
        using var photos = new FolderWatch(folders.Source, @"E:\Photos", () => Interlocked.Increment(ref settled));
        var stick = Notice(notices, @"E:\");
        Assert.True(holds.Heard(DriveNotice.LetGo, stick));
        Assert.True(holds.Heard(DriveNotice.TakeBack, stick));
        Assert.True(photos.Watching);
        Assert.Equal([@"E:\Photos"], folders.Live);
        Assert.Equal([stick], notices.Ended);
        var again = Notice(notices, @"E:\");
        Assert.NotEqual(stick, again);
        Assert.Equal((1, false), Watches(holds, @"E:\"));
        // The folder may have changed while it was not watched: it is read again.
        Assert.True(Patience.Until(() => Volatile.Read(ref settled) > 0), "the folder was not read again");
        // A mount or an unlock of a volume held all along changes nothing.
        Assert.True(holds.Heard(DriveNotice.TakeBack, again));
        Assert.Equal(again, Notice(notices, @"E:\"));
    }

    /// <summary>
    /// A drive that went stays let go of, its registration ended — pulled out unasked too —, until it arrives again: its
    /// folders are then watched again, and it is registered anew. An arrival while it is still away changes nothing.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ADriveThatWentIsTakenBackWhenItArrives(bool asked)
    {
        var (folders, notices, holds) = Made();
        using var photos = new FolderWatch(folders.Source, @"E:\Photos", () => { });
        var stick = Notice(notices, @"E:\");
        if (asked)
        {
            Assert.True(holds.Heard(DriveNotice.LetGo, stick));
        }
        folders.Away.Add(@"E:\");
        Assert.True(holds.Heard(DriveNotice.Gone, stick));
        Assert.False(photos.Watching);
        Assert.Empty(folders.Live);
        Assert.Equal([stick], notices.Ended);
        Assert.Equal((1, true), Watches(holds, @"E:\"));
        // Another device: the stick is still away.
        Assert.False(holds.Heard(DriveNotice.Arrived, 0));
        Assert.False(photos.Watching);
        folders.Away.Clear();
        Assert.True(holds.Heard(DriveNotice.Arrived, 0));
        Assert.True(photos.Watching);
        Assert.Equal((1, false), Watches(holds, @"E:\"));
        Assert.Equal(2, notices.Followed.Count(entry => entry.Root == @"E:\"));
    }

    /// <summary>A folder shown while its drive is away is not watched; the drive's arrival starts its watch.</summary>
    [Fact]
    public void AWatchStartedWhileItsDriveIsAwayStartsWhenItArrives()
    {
        var (folders, notices, holds) = Made();
        folders.Away.Add(@"E:\");
        using var photos = new FolderWatch(folders.Source, @"E:\Photos", () => { });
        Assert.False(photos.Watching);
        Assert.Empty(notices.Followed);
        Assert.Equal((1, true), Watches(holds, @"E:\"));
        folders.Away.Clear();
        Assert.True(holds.Heard(DriveNotice.Arrived, 0));
        Assert.True(photos.Watching);
        Assert.Equal([@"E:\"], notices.Followed.Select(entry => entry.Root));
    }

    /// <summary>
    /// Nothing is registered before there is a window to tell; once there is, every volume held is, and when the dock
    /// goes every registration ends. A watch ended after that registers nothing.
    /// </summary>
    [Fact]
    public void TheVolumesAreRegisteredWithTheDocksWindowWhileThereIsOne()
    {
        var holds = new DriveHolds();
        var folders = new Folders { Holds = holds };
        using var photos = new FolderWatch(folders.Source, @"E:\Photos", () => { });
        using var documents = new FolderWatch(folders.Source, @"C:\Users\Public\Documents", () => { });
        Assert.Equal((1, false, (nint)0), holds.Of(@"E:\"));
        var notices = new FakeNotices();
        holds.Notices = notices;
        Assert.Equal([@"C:\", @"E:\"], notices.Followed.Select(entry => entry.Root).Order(StringComparer.Ordinal));
        holds.Notices = null;
        Assert.Equal(2, notices.Ended.Count);
        Assert.Equal((nint)0, holds.Of(@"E:\")!.Value.Notice);
        // Still followed, only no longer registered.
        Assert.True(photos.Watching);
    }

    /// <summary>A test's own folders, and every folder of a source without holds, hold nothing.</summary>
    [Fact]
    public void ASourceWithoutHoldsHoldsNothing()
    {
        var folders = new Folders();
        using var photos = new FolderWatch(folders.Source, @"E:\Photos", () => { });
        Assert.True(photos.Watching);
        Assert.Null(DriveHolds.Shared.Of(@"E:\Photos"));
        Assert.Null(folders.Source.Holds);
    }

    /// <summary>A watch let go of and ended is not started again by anything; one started again is let go of again.</summary>
    [Fact]
    public void AWatchEndedIsNeverStartedAgain()
    {
        var folders = new Folders();
        var watch = new FolderWatch(folders.Source, @"E:\Photos", () => { });
        Assert.True(watch.Release());
        Assert.False(watch.Release());
        Assert.True(watch.Resume());
        Assert.False(watch.Resume());
        watch.Dispose();
        Assert.False(watch.Resume());
        Assert.Empty(folders.Live);
    }

    /// <summary>
    /// The disk graph keeps no handle on a disk between two samples: one on \\.\PhysicalDriveN is one on a USB stick's
    /// disk too, and Windows will not eject a device something holds open — Karpo's own Eject, Explorer's or the tray's.
    /// </summary>
    [Fact]
    public void TheDiskGraphHoldsNoDiskOpen()
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var kept = new[] { typeof(DiskSampler) }
            .Concat(typeof(DiskSampler).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            .SelectMany(type => type.GetFields(Fields).Select(field => (type, field)))
            .Where(entry => entry.field.FieldType == typeof(HANDLE) || entry.field.FieldType == typeof(Microsoft.Win32.SafeHandles.SafeFileHandle))
            .Select(entry => $"{entry.type.Name}.{entry.field.Name}");
        Assert.Empty(kept);
    }

    // --- Made up --------------------------------------------------------------------------------------------------

    private static (Folders Folders, FakeNotices Notices, DriveHolds Holds) Made()
    {
        var notices = new FakeNotices();
        var holds = new DriveHolds { Notices = notices };
        return (new Folders { Holds = holds }, notices, holds);
    }

    private static nint Notice(FakeNotices notices, string root) => notices.Followed.Last(entry => entry.Root == root).Notice;

    private static (int Watches, bool LetGo) Watches(DriveHolds holds, string root) =>
        holds.Of(root) is { } held ? (held.Watches, held.LetGo) : (0, false);

    /// <summary>Made-up handles and registrations, numbered as they are made.</summary>
    private sealed class FakeNotices : IDriveNotices
    {
        private nint next = 100;

        public List<(string Root, nint Notice)> Followed { get; } = [];

        public List<nint> LetGone { get; } = [];

        public List<nint> Ended { get; } = [];

        public nint Follow(string root)
        {
            var notice = ++next;
            Followed.Add((root, notice));
            return notice;
        }

        public void LetGo(nint notice) => LetGone.Add(notice);

        public void End(nint notice) => Ended.Add(notice);
    }

    /// <summary>Folders that are always there, on drives that are there unless put <see cref="Away"/>, each watcher written down while it lives.</summary>
    private sealed class Folders
    {
        private readonly Lock gate = new();
        private readonly List<string> live = [];
        private FolderSource? source;

        public DriveHolds? Holds { get; init; }

        public HashSet<string> Away { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> Live
        {
            get
            {
                lock (gate)
                {
                    return [.. live];
                }
            }
        }

        public FolderSource Source => source ??= new FolderSource
        {
            Enumerate = _ => [],
            Exists = path => !Away.Contains(DriveHolds.RootOf(path)),
            Icon = (_, _) => null,
            Watch = (path, _) =>
            {
                if (Away.Contains(DriveHolds.RootOf(path)))
                {
                    return null;
                }
                lock (gate)
                {
                    live.Add(path);
                }
                return new Watcher(this, path);
            },
            Open = (_, _) => { },
            Run = work => work(),
            Holds = Holds,
        };

        private sealed class Watcher(Folders folders, string path) : IDisposable
        {
            private bool done;

            public void Dispose()
            {
                lock (folders.gate)
                {
                    if (!done)
                    {
                        _ = folders.live.Remove(path);
                        done = true;
                    }
                }
            }
        }
    }
}
