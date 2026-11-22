// NotificationStore (Karpo.Dock\NotificationStore.cs), always against a
// synthetic database built here with the schema Windows' wpndatabase.db has
// (never the real one): reading toasts read-only, the schema check, the copy
// read when the database is locked, toast XML, the dismissed and seen state,
// relative times and grouping.

using System.Globalization;
using System.Text;
using Karpo.Dock;

namespace Karpo.Tests.Islands;

public sealed class NotificationStoreTests : IDisposable
{
    private const string Mail = "microsoft.windowscommunicationsapps_8wekyb3d8bbwe!microsoft.windowslive.mail";
    private const string Teams = "MSTeams_8wekyb3d8bbwe!MSTeams";

    private readonly string folder = Path.Combine(Path.GetTempPath(), "karpo-tests", "wpn-" + Guid.NewGuid().ToString("N"));
    private readonly long now = DateTime.UtcNow.ToFileTimeUtc();

    public NotificationStoreTests() => Directory.CreateDirectory(folder);

    public void Dispose()
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string Database => Path.Combine(folder, "db", "wpndatabase.db");

    private string Copies => Path.Combine(folder, "copy");

    private string State => Path.Combine(folder, "state");

    private static long Minutes(long filetime, int minutes) => filetime + minutes * TimeSpan.TicksPerMinute;

    private static string Toast(string title, string body = "", string? image = null) =>
        $"<toast><visual><binding template=\"ToastGeneric\"><text>{title}</text>{(body.Length > 0 ? $"<text>{body}</text>" : "")}"
        + (image is null ? "" : $"<image placement=\"appLogoOverride\" src=\"{image}\" hint-crop=\"circle\"/>")
        + "</binding></visual></toast>";

    /// <summary>A database with Windows' tables and columns, and the rows given.</summary>
    private void Build(IEnumerable<(long Id, string App, string Type, long Arrival, long Expiry, string Payload)> rows, bool handlers = true)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Database)!);
        Assert.Equal(Sqlite.OK, Sqlite.Open(Database, Sqlite.OPEN_READWRITE | Sqlite.OPEN_CREATE, out var db));
        try
        {
            var sql = new StringBuilder();
            sql.Append("CREATE TABLE Notification (\"Order\" INTEGER PRIMARY KEY AUTOINCREMENT, Id INTEGER NOT NULL, HandlerId INTEGER, ActivityId GUID, "
                + "Type TEXT NOT NULL, Payload BLOB, Tag TEXT, \"Group\" TEXT, ExpiryTime INT64, ArrivalTime INT64, DataVersion INT64 DEFAULT 0, "
                + "PayloadType TEXT NOT NULL, BootId INT64 DEFAULT 0, ExpiresOnReboot BOOLEAN DEFAULT FALSE);");
            if (handlers)
            {
                sql.Append("CREATE TABLE NotificationHandler (RecordId INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, PrimaryId TEXT NOT NULL, WNSId TEXT, "
                    + "HandlerType TEXT, WNFEventName INT64, SystemDataPropertySet BLOB, CreatedTime DATETIME, ModifiedTime DATETIME, ParentId TEXT, ContainerSid TEXT);");
            }
            var apps = new List<string>();
            foreach (var row in rows)
            {
                if (!apps.Contains(row.App))
                {
                    apps.Add(row.App);
                    if (handlers)
                    {
                        sql.Append($"INSERT INTO NotificationHandler (RecordId, PrimaryId, HandlerType) VALUES ({apps.Count}, '{row.App}', 'app:immersive');");
                    }
                }
                var payload = Convert.ToHexString(Encoding.UTF8.GetBytes(row.Payload));
                sql.Append($"INSERT INTO Notification (Id, HandlerId, Type, Payload, ExpiryTime, ArrivalTime, PayloadType) "
                    + $"VALUES ({row.Id}, {apps.IndexOf(row.App) + 1}, '{row.Type}', X'{payload}', {row.Expiry}, {row.Arrival}, 'Xml');");
            }
            Assert.Equal(Sqlite.OK, Sqlite.Execute(db, sql.ToString()));
        }
        finally
        {
            Sqlite.Close(db);
        }
    }

    private void BuildThree() => Build(
    [
        (1, Mail, "toast", Minutes(now, -30), Minutes(now, 60 * 24), Toast("Old mail", "From Ada")),
        (2, Teams, "toast", Minutes(now, -5), 0, Toast("Standup", "Starting now")),
        (3, Mail, "toast", Minutes(now, -1), Minutes(now, 60), Toast("New mail", "From Grace")),
        (4, Mail, "tile", Minutes(now, -1), 0, "<tile/>"),
        (5, Teams, "toast", Minutes(now, -90), Minutes(now, -10), Toast("Expired")),
    ]);

    private NotificationStore Store() => new(Database, Copies, State, app => (app == Mail ? "Mail" : "Teams", null));

    [Fact]
    public void ReadsToastsNewestFirstWithTheirApps()
    {
        BuildThree();

        var read = Wpn.Read(Database, Copies);

        Assert.True(read.Available);
        Assert.Equal([3L, 2L, 1L, 5L], read.Rows.Select(row => row.Id));
        var newest = read.Rows[0];
        Assert.Equal(Mail, newest.AppId);
        Assert.Equal("New mail", newest.Title);
        Assert.Equal("From Grace", newest.Body);
        // Read only: no copy was needed, and Windows' file was left as it was.
        Assert.False(Directory.Exists(Copies));
    }

    [Fact]
    public void ReadsBadgeRowsBesideTheToasts()
    {
        // As the probe found them in Windows' own store: one row per app, Type 'badge'.
        Build(
        [
            (1, Mail, "badge", Minutes(now, -3), 0, "<badge value=\"11\"/>"),
            (2, Teams, "badge", Minutes(now, -2), 0, "<badge value=\"0\"/>"),
            (3, Mail, "toast", Minutes(now, -1), 0, Toast("New mail")),
        ]);

        var read = Wpn.Read(Database, Copies);

        Assert.True(read.Available);
        // The toasts are what they were: badges are not toasts.
        Assert.Equal([3L], read.Rows.Select(row => row.Id));
        Assert.NotNull(read.Badges);
        Assert.Equal([Teams, Mail], read.Badges!.Select(row => row.AppId));
        Assert.Null(read.Badges[0].Badge);
        Assert.Equal(11, read.Badges[1].Badge?.Count);
        var badges = WpnBadges.Current(read.Badges, now);
        Assert.Equal(Badge.Of("11"), badges[Mail]);
        Assert.False(badges.ContainsKey(Teams));
    }

    // --- Badges as an app changes them ---------------------------------------------------------------
    // What WhatsApp was measured doing to Windows' store (2026-10-16): one 'badge' row for the app,
    // replaced with a new Id and arrival at every change ("1", "2", "1" a second apart while messages
    // come), and deleted outright (BadgeUpdater.Clear) when the person opens the chat.

    private const string WhatsApp = "5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App";

    /// <summary>Runs <paramref name="sql"/> on the made-up database, as Windows' service writes its own.</summary>
    private void Write(string sql)
    {
        Assert.Equal(Sqlite.OK, Sqlite.Open(Database, Sqlite.OPEN_READWRITE, out var db));
        try
        {
            // The store may be reading just then.
            Sqlite.BusyTimeout(db, 2000);
            Assert.Equal(Sqlite.OK, Sqlite.Execute(db, sql));
        }
        finally
        {
            Sqlite.Close(db);
        }
    }

    /// <summary>The app's badge row replaced, as BadgeUpdater.Update replaces it.</summary>
    private void SetBadge(long id, string value) => Write(
        $"DELETE FROM Notification WHERE Type = 'badge' AND HandlerId = 1; "
        + $"INSERT INTO Notification (Id, HandlerId, Type, Payload, ExpiryTime, ArrivalTime, PayloadType) "
        + $"VALUES ({id}, 1, 'badge', X'{Convert.ToHexString(Encoding.UTF8.GetBytes($"<badge value=\"{value}\"/>"))}', 0, {Minutes(now, (int)id)}, 'Xml');");

    /// <summary>
    /// What the store shows once it has heard the change by itself (its watch, not a Refresh), taken on
    /// this thread as the dock takes it; fails if that takes longer than <paramref name="withinMs"/>.
    /// </summary>
    private static Badge? Heard(NotificationStore store, Badge? expected, int withinMs = 3000)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            store.Deliver();
            var shown = store.Badges.TryGetValue(WhatsApp, out var badge) ? badge : (Badge?)null;
            if (shown == expected || clock.ElapsedMilliseconds > withinMs)
            {
                return shown;
            }
            Thread.Sleep(20);
        }
    }

    [Fact]
    public void ABadgeFollowsItsAppUpDownAndCleared()
    {
        Build([(1, WhatsApp, "badge", Minutes(now, 1), 0, "<badge value=\"1\"/>")]);
        var store = Store();
        var user = new object();
        store.Use(user);
        try
        {
            Assert.Equal(Badge.Of("1"), Heard(store, Badge.Of("1")));

            SetBadge(2, "2");
            Assert.Equal(Badge.Of("2"), Heard(store, Badge.Of("2")));

            // Down again: a count that falls is a change like any other.
            SetBadge(3, "1");
            Assert.Equal(Badge.Of("1"), Heard(store, Badge.Of("1")));

            // Opened: the row is gone (Clear), and so is the badge.
            Write("DELETE FROM Notification WHERE Type = 'badge';");
            Assert.Null(Heard(store, null));

            // A glyph, then 0, which clears as no row does.
            SetBadge(4, "attention");
            Assert.Equal(Badge.Of("attention"), Heard(store, Badge.Of("attention")));
            SetBadge(5, "0");
            Assert.Null(Heard(store, null));
        }
        finally
        {
            store.Release(user);
        }
    }

    [Fact]
    public void ABurstOfBadgesShowsTheLastOneWithoutWaitingForTheBurstToEnd()
    {
        Build([(1, WhatsApp, "badge", Minutes(now, 1), 0, "<badge value=\"1\"/>")]);
        var store = Store();
        var user = new object();
        store.Use(user);
        try
        {
            Assert.Equal(Badge.Of("1"), Heard(store, Badge.Of("1")));

            // A write every 100 ms for over a second, as a chat app setting its count while messages
            // come: the store reads on the way (MostWaitMs), not only once the writes stop.
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var seenDuring = false;
            for (var id = 2; id < 16; id++)
            {
                SetBadge(id, (id % 2 == 0 ? 2 : 3).ToString(System.Globalization.CultureInfo.InvariantCulture));
                Thread.Sleep(100);
                store.Deliver();
                seenDuring |= store.Badges.TryGetValue(WhatsApp, out var shown) && shown.Count is 2 or 3;
            }
            Assert.True(seenDuring, $"no read in {clock.ElapsedMilliseconds} ms of writes");

            SetBadge(16, "7");
            Assert.Equal(Badge.Of("7"), Heard(store, Badge.Of("7")));
        }
        finally
        {
            store.Release(user);
        }
    }

    [Fact]
    public void ABadgeClearedRightAfterItWasSetGoesUnderAWriterThatStaysOpen()
    {
        // §508: Windows writes its store as one long-lived connection in WAL mode, and WhatsApp set its badge
        // and cleared it 0 to 5 ms apart (11:35:11.557, .562), or a settle's length apart. However close
        // together, the clear is what the store shows once its watch has heard it.
        Build([]);
        Assert.Equal(Sqlite.OK, Sqlite.Open(Database, Sqlite.OPEN_READWRITE, out var windows));
        try
        {
            Assert.Equal(Sqlite.OK, Sqlite.Execute(windows, "PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; "
                + $"INSERT INTO NotificationHandler (RecordId, PrimaryId, HandlerType) VALUES (1, '{WhatsApp}', 'app:immersive');"));
            var store = Store();
            var user = new object();
            store.Use(user);
            try
            {
                Assert.Null(Heard(store, null));
                var badge = Convert.ToHexString(Encoding.UTF8.GetBytes("<badge value=\"1\"/>"));
                var id = 0;
                foreach (var apart in new[] { 0, 5, 0, 60, 140, 150, 160, 300, 5, 420 })
                {
                    id++;
                    Assert.Equal(Sqlite.OK, Sqlite.Execute(windows, "BEGIN; DELETE FROM Notification WHERE Type = 'badge'; "
                        + "INSERT INTO Notification (Id, HandlerId, Type, Payload, ExpiryTime, ArrivalTime, PayloadType) "
                        + $"VALUES ({id}, 1, 'badge', X'{badge}', 0, {Minutes(now, id)}, 'Xml'); COMMIT;"));
                    Thread.Sleep(apart);
                    Assert.Equal(Sqlite.OK, Sqlite.Execute(windows, "DELETE FROM Notification WHERE Type = 'badge';"));
                    Assert.Null(Heard(store, null));
                }
            }
            finally
            {
                store.Release(user);
            }
        }
        finally
        {
            Sqlite.Close(windows);
        }
    }

    [Fact]
    public void ABadgeIsWhatTheStoreHoldsAfterARestart()
    {
        Build([(1, WhatsApp, "badge", Minutes(now, 1), 0, "<badge value=\"5\"/>")]);
        var before = Store();
        before.Prepare();
        Assert.Equal(Badge.Of("5"), before.Badges[WhatsApp]);

        // Read while Karpo was not running: Karpo starts again with what Windows holds now, not
        // with what it showed last.
        Write("DELETE FROM Notification WHERE Type = 'badge';");
        var after = Store();
        after.Prepare();
        Assert.False(after.Badges.ContainsKey(WhatsApp));

        // And the app, started again, sets its count again.
        SetBadge(2, "3");
        var again = Store();
        again.Prepare();
        Assert.Equal(Badge.Of("3"), again.Badges[WhatsApp]);
    }

    [Theory]
    [InlineData(0, NotificationStore.SettleMs)]
    [InlineData(100, NotificationStore.SettleMs)]
    [InlineData(300, NotificationStore.MostWaitMs - 300)]
    [InlineData(NotificationStore.MostWaitMs, 0)]
    [InlineData(5000, 0)]
    public void AReadWaitsForTheBurstToSettleButNotForever(int sinceFirst, int delay) =>
        Assert.Equal(delay, NotificationStore.ReadDelay(10_000, 10_000 + sinceFirst));

    // --- Writes the watch does not hear (§569) --------------------------------------------------------
    // 2026-11-08: WhatsApp set its badge at 19:31:44 (Windows' own log, 3053; the row in the store, its
    // log's last write the same moment), Windows' taskbar showed it, and a Karpo running since 18:52 did
    // not until it was started again. A folder watch is not told of every write: a commit SQLite does not
    // flush reaches it only now and then, and a FileSystemWatcher that hits any error but an overflow
    // stops for good, which the store never noticed. The files' own size and last write say it.

    [Fact]
    public void AWatchThatStoppedIsStartedAgainAndTheWriteItMissedIsRead()
    {
        Build([(1, WhatsApp, "badge", Minutes(now, 1), 0, "<badge value=\"1\"/>")]);
        var store = new NotificationStore(Database, Copies, State, app => ("WhatsApp", null), checkMs: 100);
        var user = new object();
        store.Use(user);
        try
        {
            Assert.Equal(Badge.Of("1"), Heard(store, Badge.Of("1")));

            // The watch left as .NET leaves it after an error: no longer raising, and nothing says so.
            store.Deafen();
            SetBadge(2, "2");

            // Read anyway, once the watch has had its time to hear it, and watched again.
            Assert.Equal(Badge.Of("2"), Heard(store, Badge.Of("2"), withinMs: NotificationStore.HeardWithinMs + 3000));
            Assert.True(store.Watching);
            SetBadge(3, "3");
            Assert.Equal(Badge.Of("3"), Heard(store, Badge.Of("3")));
        }
        finally
        {
            store.Release(user);
        }
    }

    [Fact]
    public void ABadgeSetUnderAWriterThatDoesNotFlushIsReadThoughTheWatchNeverHearsIt()
    {
        // §569: one connection that stays open in WAL mode, as Windows' service writes, committing without a
        // flush (synchronous NORMAL, SQLite's usual for WAL): NTFS tells a folder watch of such a commit only
        // now and then, so a badge set this way can go unheard however long. §508's writer flushed every
        // commit (FULL), which the watch always hears.
        Build([]);
        Assert.Equal(Sqlite.OK, Sqlite.Open(Database, Sqlite.OPEN_READWRITE, out var windows));
        try
        {
            Assert.Equal(Sqlite.OK, Sqlite.Execute(windows, "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; "
                + $"INSERT INTO NotificationHandler (RecordId, PrimaryId, HandlerType) VALUES (1, '{WhatsApp}', 'app:immersive');"));
            var store = new NotificationStore(Database, Copies, State, app => ("WhatsApp", null), checkMs: 100);
            var user = new object();
            store.Use(user);
            try
            {
                Assert.Null(Heard(store, null));
                var id = 0;
                foreach (var count in new[] { "1", "2", "3", "1", "5" })
                {
                    id++;
                    Assert.Equal(Sqlite.OK, Sqlite.Execute(windows, "BEGIN; DELETE FROM Notification WHERE Type = 'badge'; "
                        + "INSERT INTO Notification (Id, HandlerId, Type, Payload, ExpiryTime, ArrivalTime, PayloadType) "
                        + $"VALUES ({id}, 1, 'badge', X'{Convert.ToHexString(Encoding.UTF8.GetBytes($"<badge value=\"{count}\"/>"))}', 0, {Minutes(now, id)}, 'Xml'); COMMIT;"));
                    Assert.Equal(Badge.Of(count), Heard(store, Badge.Of(count), withinMs: NotificationStore.HeardWithinMs + 3000));
                }
            }
            finally
            {
                store.Release(user);
            }
        }
        finally
        {
            Sqlite.Close(windows);
        }
    }

    [Fact]
    public void AWriteNoReadTookIsReadOnceTheWatchHadItsTimeToHearIt()
    {
        var ms = TimeSpan.TicksPerMillisecond;
        var read = new WpnStamp(4096, 1_000 * ms, 8192, 2_000 * ms);
        var written = read with { WalLength = 12288, WalWrite = 5_000 * ms };

        // Nothing written since the last read: nothing to read, however long ago that was.
        Assert.False(NotificationStore.Unheard(read, read, 60_000 * ms));
        // Written since, but just now: the watch's own read is still on its way.
        Assert.False(NotificationStore.Unheard(read, written, (5_000 + NotificationStore.HeardWithinMs - 1) * ms));
        // Written since, and long enough ago that a heard write would have been read: the watch missed it.
        Assert.True(NotificationStore.Unheard(read, written, (5_000 + NotificationStore.HeardWithinMs) * ms));
        // A log rewritten in place (its length the same, as after a checkpoint) is a write too, and so is the database's.
        Assert.True(NotificationStore.Unheard(read, read with { WalWrite = 3_000 * ms }, 60_000 * ms));
        Assert.True(NotificationStore.Unheard(read, read with { DbWrite = 3_000 * ms }, 60_000 * ms));
    }

    [Fact]
    public void TheCheckReadsWhatNoReadTookAndLeavesAReadOnItsWayAlone()
    {
        // §198's retry is the check's now: a read that failed on the way (Windows held the database) took no
        // write, and is read again at the next check however long Windows then writes nothing.
        var ms = TimeSpan.TicksPerMillisecond;
        var read = new WpnStamp(4096, 1_000 * ms, 8192, 2_000 * ms);
        var written = read with { WalWrite = 3_000 * ms };
        var late = 60_000 * ms;

        Assert.Equal(CheckFinding.Failed, NotificationStore.Finds(idle: true, failed: true, read, read, late));
        Assert.Equal(CheckFinding.Unheard, NotificationStore.Finds(idle: true, failed: false, read, written, late));
        Assert.Equal(CheckFinding.Nothing, NotificationStore.Finds(idle: true, failed: false, read, read, late));
        // A read waiting or running takes every write so far.
        Assert.Equal(CheckFinding.Nothing, NotificationStore.Finds(idle: false, failed: true, read, written, late));
        // Before the first read there is nothing to compare with: that read is on its way.
        Assert.Equal(CheckFinding.Nothing, NotificationStore.Finds(idle: true, failed: false, null, written, late));
    }

    [Fact]
    public void ADatabaseOfAnotherShapeIsNotAvailable()
    {
        Build([(1, Mail, "toast", now, 0, Toast("x"))], handlers: false);

        var read = Wpn.Read(Database, Copies);

        Assert.False(read.Available);
        Assert.False(read.Transient);
        Assert.Empty(read.Rows);
    }

    [Fact]
    public void AMissingDatabaseIsNotAvailable()
    {
        var read = Wpn.Read(Path.Combine(folder, "nowhere", "wpndatabase.db"), Copies);

        Assert.False(read.Available);
    }

    [Fact]
    public void ALockedDatabaseIsReadFromACopy()
    {
        BuildThree();
        Assert.Equal(Sqlite.OK, Sqlite.Open(Database, Sqlite.OPEN_READWRITE, out var writer));
        try
        {
            // What Windows does while it writes: every other connection gets SQLITE_BUSY.
            Assert.Equal(Sqlite.OK, Sqlite.Execute(writer, "PRAGMA locking_mode=EXCLUSIVE; BEGIN EXCLUSIVE;"));

            var read = Wpn.Read(Database, Copies);

            Assert.True(read.Available);
            Assert.Equal(4, read.Rows.Count);
            Assert.True(File.Exists(Path.Combine(Copies, "wpndatabase.db")));
        }
        finally
        {
            Sqlite.Execute(writer, "ROLLBACK;");
            Sqlite.Close(writer);
        }
    }

    [Fact]
    public void TheReadOnlyUriEscapesThePath()
    {
        var uri = Wpn.ReadOnlyUri(@"C:\Users\Ada Lovelace\AppData\Local\wpn#1\wpndatabase.db");

        Assert.Equal("file:///C:/Users/Ada%20Lovelace/AppData/Local/wpn%231/wpndatabase.db?mode=ro", uri);
    }

    [Fact]
    public void TheListLeavesOutTheDismissedAndTheExpired()
    {
        BuildThree();
        var store = Store();

        store.Prepare();

        Assert.True(store.Available);
        Assert.Equal(["New mail", "Standup", "Old mail"], store.Items.Select(item => item.Title));
        Assert.Equal(3, store.Unseen);
        Assert.Equal("Mail", store.Items[0].AppName);

        store.Dismiss(2);
        Assert.Equal(["New mail", "Old mail"], store.Items.Select(item => item.Title));

        store.DismissApp(Mail);
        Assert.Empty(store.Items);
    }

    [Fact]
    public void SeenAndDismissedLastAcrossRuns()
    {
        BuildThree();
        var first = Store();
        first.Prepare();
        first.MarkSeen();
        first.Dismiss(1);
        Assert.Equal(0, first.Unseen);

        var second = Store();
        second.Prepare();

        Assert.Equal(0, second.Unseen);
        Assert.Equal(["New mail", "Standup"], second.Items.Select(item => item.Title));
    }

    [Fact]
    public void ANotificationAfterTheListWasOpenedIsUnseen()
    {
        BuildThree();
        var store = Store();
        store.Prepare();
        store.MarkSeen();
        File.Delete(Database);
        Build(
        [
            (1, Mail, "toast", Minutes(now, -30), 0, Toast("Old mail")),
            (7, Teams, "toast", Minutes(now, 1), 0, Toast("Later")),
        ]);

        var again = Store();
        again.Prepare();

        Assert.Equal(1, again.Unseen);
    }

    [Fact]
    public void ClearAllHidesEverythingShownButNotWhatComesLater()
    {
        BuildThree();
        var store = Store();
        store.Prepare();

        store.DismissAll();
        Assert.Empty(store.Items);

        File.Delete(Database);
        Build(
        [
            (3, Mail, "toast", Minutes(now, -1), 0, Toast("New mail")),
            (8, Mail, "toast", Minutes(now, 2), 0, Toast("Newer mail")),
        ]);
        var again = Store();
        again.Prepare();

        Assert.Equal(["Newer mail"], again.Items.Select(item => item.Title));
    }

    [Fact]
    public void DismissalsOfGoneNotificationsAreForgotten()
    {
        var state = new NotificationState();
        state.Dismissed.Add(NotificationState.KeyOf(1, 100));
        state.Dismissed.Add(NotificationState.KeyOf(2, 200));

        state.Keep([new WpnRow(2, 200, 0, Mail, "t", "", null)]);

        Assert.Equal([NotificationState.KeyOf(2, 200)], state.Dismissed);
    }

    [Fact]
    public void AReusedIdIsNotHidden()
    {
        var state = new NotificationState();
        state.Dismissed.Add(NotificationState.KeyOf(1, 100));
        WpnRow[] rows = [new(1, 300, 0, Mail, "same id, later", "", null)];

        Assert.Single(NotificationState.Visible(rows, state, 400, 10));
    }

    [Fact]
    public void TheStateFileRoundTrips()
    {
        var state = new NotificationState { SeenUpTo = 12345 };
        state.Dismissed.Add("1:100");

        var loaded = NotificationState.Load(state.Save());

        Assert.Equal(12345, loaded.SeenUpTo);
        Assert.Equal(["1:100"], loaded.Dismissed);
        Assert.Equal(0, NotificationState.Load("{ not json").SeenUpTo);
        Assert.Empty(NotificationState.Load(null).Dismissed);
    }

    [Fact]
    public void ToastXmlGivesTitleBodyAndLocalLogo()
    {
        var logo = Path.Combine(folder, "logo folder", "logo.png");
        Directory.CreateDirectory(Path.GetDirectoryName(logo)!);
        File.WriteAllBytes(logo, [1, 2, 3]);
        var xml = "<toast launch=\"x\"><visual><binding template=\"ToastGeneric\">"
            + "<text>Ada</text><text>Lunch at\n  noon?</text><text placement=\"attribution\">via Mail</text><text>Bring cake</text>"
            + $"<image placement=\"appLogoOverride\" src=\"{new Uri(logo).AbsoluteUri}\" hint-crop=\"circle\"/>"
            + "</binding></visual><actions><action content=\"Reply\" arguments=\"r\"/></actions></toast>";

        var toast = ToastXml.Parse(xml);

        Assert.NotNull(toast);
        Assert.Equal("Ada", toast.Value.Title);
        Assert.Equal("Lunch at\nnoon?\nBring cake", toast.Value.Body);
        Assert.Equal(logo, toast.Value.ImagePath);
    }

    /// <summary>A toast's hero (placement hero) and its first inline image (no placement) are read beside its logo; a web image is none.</summary>
    [Fact]
    public void ToastXmlGivesTheHeroAndTheFirstInlineImage()
    {
        string File(string name)
        {
            var path = Path.Combine(folder, name);
            System.IO.File.WriteAllBytes(path, [1, 2, 3]);
            return new Uri(path).AbsoluteUri;
        }
        var xml = "<toast><visual><binding template=\"ToastGeneric\"><text>Trip</text>"
            + "<image src=\"https://example.com/a.png\"/>"
            + $"<image src=\"{File("inline.png")}\"/><image src=\"{File("second.png")}\"/>"
            + $"<image placement=\"hero\" src=\"{File("hero.png")}\"/><image placement=\"appLogoOverride\" src=\"{File("logo.png")}\"/>"
            + "</binding></visual></toast>";

        var toast = ToastXml.Parse(xml);

        Assert.NotNull(toast);
        Assert.Equal(Path.Combine(folder, "logo.png"), toast.Value.ImagePath);
        Assert.Equal(Path.Combine(folder, "hero.png"), toast.Value.HeroPath);
        Assert.Equal(Path.Combine(folder, "inline.png"), toast.Value.InlinePath);
    }

    [Fact]
    public void ToastXmlTakesTheFirstBindingOfAnOlderTemplate()
    {
        var toast = ToastXml.Parse("<toast><visual><binding template=\"ToastText02\"><text id=\"1\">Title</text><text id=\"2\">Body</text></binding></visual></toast>");

        Assert.NotNull(toast);
        Assert.Equal("Title", toast.Value.Title);
        Assert.Equal("Body", toast.Value.Body);
        Assert.Null(toast.Value.ImagePath);
    }

    [Theory]
    [InlineData("<toast><visual><binding template=\"ToastGeneric\"></binding></visual></toast>")]
    [InlineData("<toast><visual></visual></toast>")]
    [InlineData("<toast><visual><binding template=\"ToastGeneric\"><text>unclosed")]
    [InlineData("")]
    public void ToastXmlWithoutTextIsLeftOut(string xml) => Assert.Null(ToastXml.Parse(xml));

    [Theory]
    [InlineData("https://example.com/logo.png")]
    [InlineData("ms-appdata:///local/logo.png")]
    [InlineData("file:///C:/nowhere/at/all/logo.png")]
    [InlineData("logo.png")]
    public void OnlyALocalFileThatExistsIsALogo(string source) => Assert.Null(ToastXml.LocalFile(source));

    [Theory]
    [InlineData(0, "Now")]
    [InlineData(59, "Now")]
    [InlineData(60, "1m")]
    [InlineData(5 * 60 + 30, "5m")]
    [InlineData(59 * 60, "59m")]
    [InlineData(2 * 3600 + 5, "2h")]
    public void RelativeTimesToday(int secondsAgo, string expected)
    {
        var at = new DateTime(2026, 9, 17, 18, 0, 0);

        Assert.Equal(expected, NotificationText.Relative(at.AddSeconds(-secondsAgo), at, Day, Day));
    }

    [Fact]
    public void RelativeTimesOnEarlierDays()
    {
        var at = new DateTime(2026, 9, 17, 1, 0, 0);

        Assert.Equal("Yesterday", NotificationText.Relative(new DateTime(2026, 9, 16, 23, 0, 0), at, Day, Day));
        Assert.Equal("m 09-10", NotificationText.Relative(new DateTime(2026, 9, 10, 12, 0, 0), at, Day, Short));
        Assert.Equal("s 2025-12-31", NotificationText.Relative(new DateTime(2025, 12, 31, 12, 0, 0), at, Day, Short));
        // A clock set back is not the future.
        Assert.Equal("Now", NotificationText.Relative(at.AddMinutes(3), at, Day, Short));
    }

    /// <summary>
    /// quick: the list is not grouped by app any more — every notification, newest first, whichever
    /// app it came from, so what happened last is at the top wherever it came from.
    /// </summary>
    [Fact]
    public void TheListIsNewestFirstWhateverTheApp()
    {
        NotificationEntry Entry(long id, string app, int minute) =>
            new(id, app, app, null, $"n{id}", "", new DateTime(2026, 9, 17, 12, minute, 0), null);
        var items = new[] { Entry(1, Mail, 1), Entry(2, Teams, 5), Entry(3, Mail, 9), Entry(4, Teams, 2) };

        var list = NotificationText.Newest(items);

        Assert.Equal([3L, 2L, 4L, 1L], list.Select(item => item.Id));
        Assert.Equal([Mail, Teams, Teams, Mail], list.Select(item => item.AppId));
        // Two that arrived in the same minute keep the order they were given ids in, newest first.
        var same = new[] { Entry(7, Mail, 3), Entry(8, Teams, 3) };
        Assert.Equal([8L, 7L], NotificationText.Newest(same).Select(item => item.Id));
    }

    private static string Day(DateOnly day) => "m " + day.ToString("MM-dd", CultureInfo.InvariantCulture);

    private static string Short(DateOnly day) => "s " + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
