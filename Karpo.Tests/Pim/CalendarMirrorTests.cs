// §616: a CalDAV calendar's mirror on disk (Karpo.Dock\Pim\CalendarMirror.cs):
// encrypted for this Windows user with nothing of the calendar in its bytes or
// its name, read back as it was written, its .bak when it does not read, a file
// of another version downloaded again, a file another program holds neither
// read nor written over, and deleted with its calendar or its account. Every
// folder is the test run's own (Isolation.Folder).

using System.Text;
using Karpo.Accounts;
using Karpo.Accounts.Dav;
using Karpo.Dock.Pim;
using Karpo.Tests.Accounts;

namespace Karpo.Tests.Pim;

public sealed class CalendarMirrorTests
{
    private static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    private static readonly RemoteCalendar Work = new("https://dav.example.com/cal/work/", "Work", "#D13438");

    private static string Folder() => Path.Combine(Isolation.Folder, "mirror-" + Guid.NewGuid().ToString("N"));

    private static CalendarObject Object(string name, string summary, int day) =>
        new($"https://dav.example.com/cal/work/{name}.ics", $"\"etag-{name}-{summary.Length}\"",
            KeptICalendar.Of(FakeCalDav.Event(name, summary, new DateTime(2026, 11, day, 9, 0, 0, DateTimeKind.Utc))));

    private static MirroredCalendar Calendar(params CalendarObject[] objects) =>
        CalendarMirror.Apply(null, new CalendarChanges(Work) { Changed = objects, Token = "http://dav.example.com/sync/secret-token-77", Tag = "ctag-77", ReconciledUtc = Now }, 0, Now, final: true);

    private static List<string> Titles(MirroredCalendar calendar) =>
        [.. calendar.Index.Between(new DateTime(2026, 11, 1), new DateTime(2026, 12, 1), TimeZoneInfo.Utc).OrderBy(one => one.Start).Select(one => one.Title)];

    /// <summary>
    /// A calendar written and read back is the calendar: header, etags, events. The file is DPAPI's and nothing else:
    /// no title, href, token or etag in its bytes, and its name a hash, not the calendar's id.
    /// </summary>
    [Fact]
    public async Task ACalendarIsReadBackAsWrittenWithNothingOfItInTheClear()
    {
        var folder = Folder();
        var mirror = new CalendarMirror(folder);
        var written = Calendar(Object("dentist", "Dentist Doctor Zahnweh", 3), Object("standup", "Standup Meeting", 4));

        Assert.True(await mirror.WriteAsync("acc1", written, CancellationToken.None));
        var read = Assert.Single(await new CalendarMirror(folder).ReadAsync("acc1", CancellationToken.None));

        Assert.Equal(written.Header, read.Header);
        Assert.Equal(written.Etags, read.Etags);
        Assert.Equal(["Dentist Doctor Zahnweh", "Standup Meeting"], Titles(read));
        Assert.True(read.State.Complete);
        var file = Assert.Single(Directory.GetFiles(Path.Combine(folder, "acc1")));
        Assert.Equal(CalendarMirror.FileName(Work.Id), Path.GetFileName(file));
        Assert.DoesNotContain("work", Path.GetFileName(file), StringComparison.OrdinalIgnoreCase);
        var bytes = File.ReadAllBytes(file);
        foreach (var secret in (string[])["Zahnweh", "Standup", "dentist", "secret-token", "ctag-77", "etag-", "dav.example.com", "Work", "BEGIN:V"])
        {
            Assert.DoesNotContain(secret, Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
            Assert.DoesNotContain(secret, Encoding.Unicode.GetString(bytes), StringComparison.Ordinal);
        }
    }

    /// <summary>A file that does not read is its .bak, the version before; with neither reading it is deleted, to be downloaded again.</summary>
    [Fact]
    public async Task AFileThatDoesNotReadIsItsBackupElseGone()
    {
        var folder = Folder();
        var mirror = new CalendarMirror(folder);
        await mirror.WriteAsync("acc1", Calendar(Object("one", "First", 3)), CancellationToken.None);
        await mirror.WriteAsync("acc1", Calendar(Object("one", "First", 3), Object("two", "Second", 4)), CancellationToken.None);
        var path = Path.Combine(folder, "acc1", CalendarMirror.FileName(Work.Id));
        File.WriteAllBytes(path, [1, 2, 3, 4, 5]);

        var kept = Assert.Single(await new CalendarMirror(folder).ReadAsync("acc1", CancellationToken.None));
        Assert.Equal(["First"], Titles(kept));

        File.WriteAllBytes(path, [1, 2, 3]);
        File.WriteAllBytes(path + ".bak", [4, 5, 6]);
        Assert.Empty(await new CalendarMirror(folder).ReadAsync("acc1", CancellationToken.None));
        Assert.False(File.Exists(path));
        Assert.False(File.Exists(path + ".bak"));
    }

    /// <summary>A file kept under another version of what is kept (KeptICalendar.Version) is deleted: the calendar downloads again whole.</summary>
    [Fact]
    public async Task AFileOfAnotherVersionIsDownloadedAgain()
    {
        var folder = Folder();
        var old = Calendar(Object("one", "First", 3));
        await new CalendarMirror(folder).WriteAsync("acc1", old with { Header = old.Header with { Version = KeptICalendar.Version - 1 } }, CancellationToken.None);

        Assert.Empty(await new CalendarMirror(folder).ReadAsync("acc1", CancellationToken.None));
        Assert.Empty(Directory.GetFiles(Path.Combine(folder, "acc1"), "*.bin"));
    }

    /// <summary>A file another program holds is not written over (the store keeps it to write again) and not taken for none.</summary>
    [Fact]
    public async Task AFileAnotherProgramHoldsIsLeftAlone()
    {
        var folder = Folder();
        var mirror = new CalendarMirror(folder);
        await mirror.WriteAsync("acc1", Calendar(Object("one", "First", 3)), CancellationToken.None);
        var path = Path.Combine(folder, "acc1", CalendarMirror.FileName(Work.Id));

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.False(await mirror.WriteAsync("acc1", Calendar(Object("two", "Second", 4)), CancellationToken.None));
            Assert.Empty(await mirror.ReadAsync("acc1", CancellationToken.None));
        }
        Assert.Equal(["First"], Titles(Assert.Single(await mirror.ReadAsync("acc1", CancellationToken.None))));
    }

    /// <summary>A calendar's file goes with it (turned off, no longer listed); an account's folder with the account, at once or at the next start.</summary>
    [Fact]
    public async Task ACalendarOrAnAccountGoneTakesItsFiles()
    {
        var folder = Folder();
        var mirror = new CalendarMirror(folder);
        await mirror.WriteAsync("acc1", Calendar(Object("one", "First", 3)), CancellationToken.None);
        await mirror.WriteAsync("acc1", Calendar(Object("one", "First", 3)), CancellationToken.None);
        await mirror.WriteAsync("acc2", Calendar(Object("one", "First", 3)), CancellationToken.None);
        await mirror.WriteAsync("acc3", Calendar(Object("one", "First", 3)), CancellationToken.None);

        await mirror.DeleteAsync("acc1", Work.Id, CancellationToken.None);
        Assert.Empty(Directory.GetFiles(Path.Combine(folder, "acc1")));

        await mirror.SweepAsync(new HashSet<string>(["acc2"]), CancellationToken.None);
        Assert.Equal(["acc2"], Directory.GetDirectories(folder).Select(Path.GetFileName));
    }

    /// <summary>An isolated run keeps its calendars in memory only: nothing written, nothing read.</summary>
    [Fact]
    public async Task WithNoFolderNothingIsWritten()
    {
        var mirror = new CalendarMirror(null);

        Assert.True(await mirror.WriteAsync("acc1", Calendar(Object("one", "First", 3)), CancellationToken.None));
        Assert.Empty(await mirror.ReadAsync("acc1", CancellationToken.None));
    }

    /// <summary>
    /// Changes put in: one changed, one new, one removed; an object with no event is held by its etag alone; one whose
    /// text does not parse is counted as not shown. A step of a first download is never whole; the answer after it is.
    /// </summary>
    [Fact]
    public void ChangesArePutInByHref()
    {
        var before = Calendar(Object("one", "First", 3), Object("two", "Second", 4), Object("three", "Third", 5));
        var task = new CalendarObject("https://dav.example.com/cal/work/task.ics", "\"t\"", null);

        var step = CalendarMirror.Apply(before, new CalendarChanges(Work) { Changed = [Object("two", "Second again", 4), task], Incomplete = true }, 0, Now, final: false);
        Assert.False(step.Header.Complete);
        Assert.Null(step.Header.Tag);
        Assert.Equal(4, step.Etags.Count);

        var after = CalendarMirror.Apply(step, new CalendarChanges(Work) { Changed = [Object("four", "Fourth", 6)], Removed = [Object("one", "", 3).Href], Token = "t2", Tag = "c2" }, 0, Now, final: true);
        Assert.True(after.Header.Complete);
        Assert.Equal(["Second again", "Third", "Fourth"], Titles(after));
        Assert.Equal(4, after.Etags.Count);
        Assert.Equal("\"t\"", after.Etags[task.Href]);
        Assert.Equal(3, after.Index.Count);

        var broken = CalendarMirror.Apply(after, new CalendarChanges(Work) { Changed = [new CalendarObject("https://dav.example.com/cal/work/x.ics", "\"x\"", "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nDTSTART:20261101T090000Z\r\nRRULE:FREQ=HOURLY\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n")] }, 0, Now, final: true);
        Assert.Equal(1, broken.Index.Dropped);
    }
}
