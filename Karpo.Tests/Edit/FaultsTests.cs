// §394: what has gone wrong that Karpo knows of, in one place (Karpo.Dock\Faults.cs), which the edit bar's error
// indicator counts and its list shows: a source's faults set all at once — what it does not say any more gone,
// what it says again since when it began —, newest first, the same message of the same thing once; the widgets'
// from their failed reads (IRefreshable.Problem), the accounts' from what the account layer last said (SyncReport).

using Karpo.Accounts;
using Karpo.Dock;

namespace Karpo.Tests.Edit;

public sealed class FaultsTests
{
    private static readonly DateTime Noon = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    private static FaultNow Weather(string message = "The forecast service did not answer") =>
        new("widget:layout:a/w", FaultKind.Widget, "Weather", "", message, "layout:a/w");

    /// <summary>
    /// A widget's failed read is a fault until the widget says it no more: raised, it is there; said again in
    /// other words, it is the same fault, since when it began; not said, it is gone — the indicator's count with it.
    /// </summary>
    [Fact]
    public void AFaultIsThereUntilItsSourceSaysItNoMore()
    {
        var faults = new Faults();
        Assert.Equal(0, faults.Count);
        Assert.True(faults.Set(FaultKind.Widget, [Weather()], Noon));
        var raised = Assert.Single(faults.All);
        Assert.Equal(("Weather", "The forecast service did not answer", "layout:a/w", Noon), (raised.Name, raised.Message, raised.Of, raised.SinceUtc));

        var version = faults.Version;
        Assert.False(faults.Set(FaultKind.Widget, [Weather()], Noon.AddMinutes(5)));
        Assert.Equal(version, faults.Version);
        Assert.True(faults.Set(FaultKind.Widget, [Weather("No connection")], Noon.AddMinutes(10)));
        Assert.Equal(("No connection", Noon), (faults.All[0].Message, faults.All[0].SinceUtc));

        Assert.True(faults.Set(FaultKind.Widget, [], Noon.AddMinutes(15)));
        Assert.Empty(faults.All);
        Assert.True(faults.Version > version);
    }

    /// <summary>A source sets its own kind alone: the widgets' setting none leaves the accounts' and the settings' standing.</summary>
    [Fact]
    public void EachSourceSetsItsOwn()
    {
        var faults = new Faults();
        faults.Set(FaultKind.Widget, [Weather()], Noon);
        faults.Set(FaultKind.Account, [new FaultNow("account:1:Mail", FaultKind.Account, "me@example.com", "", "Couldn't sync: denied", "1")], Noon);
        faults.Raise(new FaultNow(FaultSources.Broken, FaultKind.Settings, "Settings", "", "config.json doesn't read"), Noon);
        Assert.Equal(3, faults.Count);

        faults.Set(FaultKind.Widget, [], Noon);
        Assert.Equal([FaultKind.Account, FaultKind.Settings], faults.All.Select(fault => fault.Kind).Order());
        Assert.True(faults.Clear(FaultSources.Broken));
        Assert.False(faults.Clear(FaultSources.Broken));
        Assert.Equal(FaultKind.Account, Assert.Single(faults.All).Kind);
    }

    /// <summary>Newest first; an account's parts failing for one reason are one row, for two reasons two.</summary>
    [Fact]
    public void NewestFirstAndOneRowAReason()
    {
        var faults = new Faults();
        faults.Set(FaultKind.Widget, [Weather()], Noon);
        FaultNow Part(string kind, string why) => new($"account:1:{kind}", FaultKind.Account, "me@example.com", "", why, "1");
        faults.Set(FaultKind.Account, [Part("Calendars", "Couldn't sync: denied"), Part("Mail", "Couldn't sync: denied")], Noon.AddMinutes(1));
        Assert.Equal(["me@example.com", "Weather"], faults.All.Select(fault => fault.Name));

        faults.Set(FaultKind.Account, [Part("Calendars", "Couldn't sync: denied"), Part("Mail", "Couldn't sync: no server")], Noon.AddMinutes(1));
        Assert.Equal(3, faults.Count);
    }

    /// <summary>A widget that reads what it shows: a fault where its last read failed and it has something to read; none otherwise, none of a widget nobody knows.</summary>
    [Fact]
    public void AWidgetsFaultIsItsFailedRead()
    {
        (string, string)? Known(string key) => key == "layout:a/unknown" ? null : ("RSS", "");
        var faults = FaultSources.Widgets(
            [
                ("layout:a/failed", new Reads(true, "Couldn't reach the site")),
                ("layout:a/fine", new Reads(true, null)),
                ("layout:a/unset", new Reads(false, "Nothing to read")),
                ("layout:a/unknown", new Reads(true, "Couldn't reach the site")),
                ("layout:a/clock", new object()),
            ],
            Known).ToList();
        var fault = Assert.Single(faults);
        Assert.Equal(new FaultNow("widget:layout:a/failed", FaultKind.Widget, "RSS", "", "Couldn't reach the site", "layout:a/failed"), fault);
    }

    /// <summary>
    /// The accounts' faults are what the account layer last said (SyncReport, ⚙ › Accounts): each part that
    /// failed in the Accounts' words, To Do waiting for consent, by the account's address; an account the
    /// report says nothing of, or whose syncs went well, none.
    /// </summary>
    [Fact]
    public void AnAccountsFaultIsWhatItsLastSyncSaid()
    {
        Account[] accounts =
        [
            new() { Id = "ms", Kind = AccountKind.Microsoft, Address = "me@outlook.com" },
            new() { Id = "imap", Kind = AccountKind.Imap, Address = "me@example.com" },
            new() { Id = "new", Kind = AccountKind.Dav, Address = "me@dav.example" },
            new() { Id = "g", Kind = AccountKind.Google, Address = "me@gmail.com" },
        ];
        var report = new SyncReportFile
        {
            Accounts =
            [
                new SyncReportAccount { Account = "ms", Calendars = new SyncPart { Problem = "Sign in again" }, Mail = new SyncPart(), Tasks = new SyncPart { Consent = true } },
                new SyncReportAccount { Account = "imap", Mail = new SyncPart { UpdatedUtc = Noon } },
                // §687: a Google account whose person unticked Google Calendar: its calendars wait for the consent.
                new SyncReportAccount { Account = "g", Calendars = new SyncPart { Consent = true }, Mail = new SyncPart { UpdatedUtc = Noon } },
            ],
        };
        var faults = FaultSources.Accounts(accounts, report).ToList();
        Assert.Equal(
            [
                ("account:ms:Calendars", "me@outlook.com", S.SettingsAccountsSyncFailed("Sign in again"), "ms"),
                ("account:ms:Tasks", "me@outlook.com", S.SettingsAccountsSyncConsent("Microsoft To Do"), "ms"),
                ("account:g:Calendars", "me@gmail.com", S.SettingsAccountsSyncConsent("Google Calendar"), "g"),
            ],
            faults.Select(fault => (fault.Key, fault.Name, fault.Message, fault.Of)));
        Assert.All(faults, fault => Assert.Equal(FaultKind.Account, fault.Kind));
        Assert.Equal([OptionGlyphs.Contact, OptionGlyphs.Contact, OptionGlyphs.Globe], faults.Select(fault => fault.Glyph));
    }

    /// <summary>A widget that reads what it shows, as a test has it.</summary>
    private sealed class Reads(bool can, string? problem) : IRefreshable
    {
        public bool CanRefresh => can;

        public bool Refreshing => false;

        public DateTime? UpdatedUtc => null;

        public string? Problem => problem;

        public void RefreshNow()
        {
        }
    }
}
