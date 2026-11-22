// Whether Windows itself keeps a notification icon on the taskbar, as Karpo
// reads HKCU\Control Panel\NotifyIconSettings (Tray.Promotions.Decide). The
// records here are what that key holds; nothing touches the registry.

using Karpo.Dock;

namespace Karpo.Tests.TrayIcons;

public sealed class TrayPromotionTests
{
    const string SpotifyOld = @"C:\Program Files\WindowsApps\SpotifyAB.SpotifyMusic_1.300.277.0_x64__zpdnekdrzrea0\Spotify.exe";
    const string SpotifyNew = @"C:\Program Files\WindowsApps\SpotifyAB.SpotifyMusic_1.301.234.0_x64__zpdnekdrzrea0\Spotify.exe";

    static Tray.Promotions.Record Record(string path, uint? uid, bool? promoted, Guid? guid = null) =>
        new(path, uid, guid, promoted);

    [Fact]
    public void AnIconTheKeyKnowsNothingAboutWaitsBehindTheChevron() =>
        Assert.False(Tray.Promotions.Decide([], @"C:\app.exe", 1, null));

    [Fact]
    public void TheRecordForTheIconDecides()
    {
        Tray.Promotions.Record[] records =
        [
            Record(@"C:\app.exe", 1, true),
            Record(@"C:\other.exe", 1, false),
        ];

        Assert.True(Tray.Promotions.Decide(records, @"c:\APP.exe", 1, null));
        Assert.False(Tray.Promotions.Decide(records, @"C:\other.exe", 1, null));
        // Another icon of the same app.
        Assert.False(Tray.Promotions.Decide(records, @"C:\app.exe", 2, null));
    }

    [Fact]
    public void AnIconWithAGuidIsFoundByIt()
    {
        var guid = Guid.Parse("ba82e2dc-f405-47ad-b032-cf0faa0e3933");
        Tray.Promotions.Record[] records = [Record("", null, true, guid)];

        Assert.True(Tray.Promotions.Decide(records, null, 0, guid));
        Assert.False(Tray.Promotions.Decide(records, null, 0, Guid.NewGuid()));
    }

    [Fact]
    public void APromotedAppThatUpdatedIsStillPromoted()
    {
        // What the user's registry holds: the version they kept on the taskbar, and the one that
        // replaced it, for which Windows wrote a record with no IsPromoted at all.
        Tray.Promotions.Record[] records = [Record(SpotifyOld, 0, true), Record(SpotifyNew, 0, null)];

        Assert.True(Tray.Promotions.Decide(records, SpotifyNew, 0, null));
    }

    [Fact]
    public void AnAppTakenOffTheTaskbarSinceItsUpdateStaysOff()
    {
        // The record for the version that runs now says no: that is the person's latest word on it.
        Tray.Promotions.Record[] records = [Record(SpotifyOld, 0, true), Record(SpotifyNew, 0, false)];

        Assert.False(Tray.Promotions.Decide(records, SpotifyNew, 0, null));
    }

    [Fact]
    public void AnUpdateOfAnAppNobodyPromotedIsStillNotPromoted()
    {
        const string claudeOld = @"C:\Program Files\WindowsApps\Claude_2.110.1.0_x64__pzs8sxrjxfjjc\app\claude.exe";
        const string claudeNew = @"C:\Program Files\WindowsApps\Claude_2.2553.1.0_x64__pzs8sxrjxfjjc\app\claude.exe";
        Tray.Promotions.Record[] records = [Record(claudeOld, 3, null), Record(claudeNew, 3, null)];

        Assert.False(Tray.Promotions.Decide(records, claudeNew, 3, null));
    }

    const string WhatsAppOld = @"C:\Program Files\WindowsApps\5319275A.WhatsAppDesktop_2.2634.101.0_x64__cv1g1gvanyjgm\WhatsApp.Root.exe";
    const string WhatsAppNew = @"C:\Program Files\WindowsApps\5319275A.WhatsAppDesktop_2.2636.100.0_x64__cv1g1gvanyjgm\WhatsApp.Root.exe";
    static readonly Guid WhatsAppOldGuid = Guid.Parse("2753843f-159d-d110-d927-eec24e4d97dd");
    static readonly Guid WhatsAppNewGuid = Guid.Parse("2a9749be-92c7-b2db-3b18-aa5b146fbfa6");

    [Fact]
    public void AnAppWithANewGuidPerBuildStaysPromotedAcrossAnUpdate()
    {
        // The person's registry as it is: the build kept on the taskbar, and the new one whose
        // record (with its new GUID) says nothing.
        Tray.Promotions.Record[] records =
        [
            Record(WhatsAppOld, null, true, WhatsAppOldGuid),
            Record(WhatsAppNew, null, null, WhatsAppNewGuid),
        ];

        Assert.True(Tray.Promotions.Decide(records, WhatsAppNew, 0, WhatsAppNewGuid));
        // Where the new build's record says something, that stands.
        records[1] = Record(WhatsAppNew, null, false, WhatsAppNewGuid);
        Assert.False(Tray.Promotions.Decide(records, WhatsAppNew, 0, WhatsAppNewGuid));
    }

    [Fact]
    public void AnotherGuidIconOfTheSameBuildIsNotBorrowed()
    {
        // explorer.exe has several GUID icons of its own: one promoted says nothing of the others.
        const string explorer = @"C:\Windows\explorer.exe";
        Tray.Promotions.Record[] records = [Record(explorer, null, true, Guid.NewGuid())];

        Assert.False(Tray.Promotions.Decide(records, explorer, 0, Guid.NewGuid()));
    }

    [Fact]
    public void TheRecordWrittenIsTheIconsOwn()
    {
        var guid = Guid.NewGuid();
        Tray.Promotions.Record[] records =
        [
            Record(SpotifyOld, 0, true),
            Record(SpotifyNew, 0, null),
            Record(SpotifyNew, 1, null),
            Record(@"C:\app.exe", null, true, guid),
        ];

        // The build that runs now, never the older one, nor another icon of it.
        Assert.Equal(1, Tray.Promotions.RecordFor(records, SpotifyNew.ToUpperInvariant(), 0, null));
        Assert.Equal(2, Tray.Promotions.RecordFor(records, SpotifyNew, 1, null));
        Assert.Equal(3, Tray.Promotions.RecordFor(records, @"C:\app.exe", 0, guid));
        // No record of its own: nothing is written (Windows makes the records, Karpo doesn't).
        Assert.Equal(-1, Tray.Promotions.RecordFor(records, @"C:\new.exe", 0, null));
        Assert.Equal(-1, Tray.Promotions.RecordFor(records, null, 0, null));
        Assert.Equal(-1, Tray.Promotions.RecordFor(records, @"C:\app.exe", 0, Guid.NewGuid()));
    }

    [Fact]
    public void AnotherAppsPromotionIsNotBorrowed()
    {
        Tray.Promotions.Record[] records =
        [
            Record(@"C:\Program Files\WindowsApps\Claude_2.110.1.0_x64__pzs8sxrjxfjjc\app\claude.exe", 0, true),
            Record(SpotifyNew, 0, null),
        ];

        Assert.False(Tray.Promotions.Decide(records, SpotifyNew, 0, null));
    }
}
