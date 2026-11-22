// What a notification icon is called in the config, and why an app's update
// must not change it (TrayIdentity).

namespace Karpo.Tests.TrayIcons;

public sealed class TrayIdentityTests
{
    const string SpotifyOld = @"C:\Program Files\WindowsApps\SpotifyAB.SpotifyMusic_1.300.277.0_x64__zpdnekdrzrea0\Spotify.exe";
    const string SpotifyNew = @"C:\Program Files\WindowsApps\SpotifyAB.SpotifyMusic_1.301.234.0_x64__zpdnekdrzrea0\Spotify.exe";

    [Fact]
    public void AnIconWithAGuidIsNamedByIt()
    {
        var guid = Guid.Parse("e5768d8b-6936-4f45-b1ad-4c5fb414cb35");

        Assert.Equal("{e5768d8b-6936-4f45-b1ad-4c5fb414cb35}", TrayIdentity.Of(@"C:\app.exe", 3, guid));
    }

    [Fact]
    public void AnIconWithoutOneIsNamedByItsAppAndItsId()
    {
        Assert.Equal(@"c:\tools\app.exe|3", TrayIdentity.Of(@"C:\Tools\App.exe", 3, null));
        // Its app's window has gone before the path could be read.
        Assert.Equal("unknown|0", TrayIdentity.Of(null, 0, null));
    }

    [Fact]
    public void APackagedAppKeepsItsNameAcrossAnUpdate()
    {
        // The defect the user saw: Spotify was kept on the taskbar, updated, and came back behind
        // the chevron, because to Windows the new build is a different icon.
        Assert.Equal(TrayIdentity.Of(SpotifyOld, 0, null), TrayIdentity.Of(SpotifyNew, 0, null));
        Assert.Equal(
            @"c:\program files\windowsapps\spotifyab.spotifymusic__zpdnekdrzrea0\spotify.exe|0",
            TrayIdentity.Of(SpotifyNew, 0, null));
        Assert.True(TrayIdentity.SameApp(SpotifyOld, SpotifyNew));
    }

    [Fact]
    public void AnotherPackagedAppIsStillAnotherApp()
    {
        const string claude = @"C:\Program Files\WindowsApps\Claude_2.2553.1.0_x64__pzs8sxrjxfjjc\app\claude.exe";

        Assert.NotEqual(TrayIdentity.Of(claude, 3, null), TrayIdentity.Of(SpotifyNew, 3, null));
        Assert.False(TrayIdentity.SameApp(claude, SpotifyNew));
        // The same app, a different icon of it.
        Assert.NotEqual(TrayIdentity.Of(claude, 3, null), TrayIdentity.Of(claude, 4, null));
    }

    [Fact]
    public void ASquirrelInstallKeepsItsNameAcrossAnUpdate()
    {
        const string before = @"C:\Users\x\AppData\Local\Discord\app-1.0.9243\Discord.exe";
        const string after = @"C:\Users\x\AppData\Local\Discord\app-1.0.9250\Discord.exe";

        Assert.Equal(TrayIdentity.Of(before, 1, null), TrayIdentity.Of(after, 1, null));
        Assert.Equal(@"c:\users\x\appdata\local\discord\app\discord.exe|1", TrayIdentity.Of(after, 1, null));
    }

    [Theory]
    // A plain path is left exactly as it is.
    [InlineData(@"C:\Users\x\AppData\Local\SynologyDrive\SynologyDrive.app\bin\cloud-drive-ui.exe")]
    [InlineData(@"C:\Program Files\Docker\Docker\frontend\Docker Desktop.exe")]
    // A folder that merely ends in a number is not a version folder.
    [InlineData(@"C:\Program Files\Office-2021\word.exe")]
    [InlineData(@"C:\Program Files\My_App_x64\app.exe")]
    // A name whose own field looks like an architecture but carries no version.
    [InlineData(@"C:\Program Files\WindowsApps\Name_x64__publisher\app.exe")]
    public void APathWithNoVersionFolderIsLeftAlone(string path) => Assert.Equal(path, TrayIdentity.Stable(path));

    [Fact]
    public void AGuidIsNeverTakenForAVersionFolder()
    {
        // Its last group is all digits, which the Squirrel rule would otherwise cut off.
        const string identity = "{ba82e2dc-f405-47ad-b032-123456789012}";

        Assert.Equal(identity, TrayIdentity.Stable(identity));
    }

    [Fact]
    public void AnAppThatGivesEachBuildANewGuidKeepsItsName()
    {
        // The person's registry: WhatsApp's packaged app has a NotifyIconSettings record for every
        // build, each with a GUID of its own. Named by its GUID, its icon was a new one after each
        // update, and where it had been dragged to was lost.
        const string before = @"C:\Program Files\WindowsApps\5319275A.WhatsAppDesktop_2.2636.100.0_x64__cv1g1gvanyjgm\WhatsApp.Root.exe";
        const string after = @"C:\Program Files\WindowsApps\5319275A.WhatsAppDesktop_2.2637.100.0_x64__cv1g1gvanyjgm\WhatsApp.Root.exe";
        var one = TrayIdentity.Of(before, 0, Guid.Parse("2a9749be-92c7-b2db-3b18-aa5b146fbfa6"));
        var other = TrayIdentity.Of(after, 0, Guid.Parse("436eae5c-f1ef-0c3e-5d19-a4b38e330a51"));

        Assert.Equal(one, other);
        Assert.Equal(@"c:\program files\windowsapps\5319275a.whatsappdesktop__cv1g1gvanyjgm\whatsapp.root.exe|guid", one);
        // Not the name an icon of the same app without a GUID gets.
        Assert.NotEqual(TrayIdentity.Of(after, 0, null), other);
    }

    [Fact]
    public void AGuidIconFromAPlainPathKeepsItsGuid()
    {
        // Windows Security's shield: its GUID is its own, and its executable sits in no version folder.
        var guid = Guid.Parse("ba82e2dc-f405-47ad-b032-cf0faa0e3933");

        Assert.Equal("{ba82e2dc-f405-47ad-b032-cf0faa0e3933}", TrayIdentity.Of(@"C:\Windows\System32\SecurityHealthSystray.exe", 0, guid));
        // Nor does a path that could not be read change it.
        Assert.Equal("{ba82e2dc-f405-47ad-b032-cf0faa0e3933}", TrayIdentity.Of(null, 0, guid));
    }

    [Fact]
    public void TakingTheVersionOutTwiceChangesNothingMore() =>
        Assert.Equal(TrayIdentity.Stable(SpotifyNew), TrayIdentity.Stable(TrayIdentity.Stable(SpotifyNew)));
}
