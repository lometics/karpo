// Which media session belongs to a window's app (MediaSessions.Matches and
// Pick): by the app's AUMID, or for a desktop app by its exe's name, never by a
// pin's shortcut; case aside; a playing session first.

using Karpo.Dock;

// Not Karpo.Tests.Desktops: the core tests name BarKind.Desktops as "Desktops" (using static),
// which a namespace of that name would shadow.
namespace Karpo.Tests.DesktopsArea;

public sealed class MediaSessionsTests
{
    private static MediaControls Session(string source, bool playing = false) => new(source, playing, true, true, true);

    [Theory]
    [InlineData("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", null)]
    [InlineData("MSEdge", "msedge", null)]
    [InlineData("Spotify.exe", null, @"C:\Users\me\AppData\Roaming\Spotify\Spotify.exe")]
    [InlineData("spotify.exe", "Some.Other!App", @"C:\Spotify\SPOTIFY.EXE")]
    [InlineData("vlc.exe", null, "vlc.exe")]
    [InlineData(@"C:\Program Files\VideoLAN\VLC\vlc.exe", null, @"D:\vlc.exe")]
    public void BelongsToTheApp(string source, string? aumid, string? exe) =>
        Assert.True(MediaSessions.Matches(source, aumid, exe));

    [Theory]
    [InlineData("Spotify.exe", null, @"C:\Users\me\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Spotify.lnk")]
    [InlineData("Spotify.exe", "Spotify", null)]
    [InlineData("chrome.exe", null, @"C:\Program Files\Google\Chrome\Application\notchrome.exe")]
    [InlineData("", "", "")]
    [InlineData("Spotify.exe", null, null)]
    [InlineData(null, "Spotify.exe", "Spotify.exe")]
    public void DoesNotBelongToTheApp(string? source, string? aumid, string? exe) =>
        Assert.False(MediaSessions.Matches(source, aumid, exe));

    [Fact]
    public void APinsShortcutFindsNothingButItsRunningExeDoes()
    {
        // Regression: pin and group previews asked with the .lnk target, so a
        // pinned Spotify never showed its media buttons.
        var sessions = new[] { Session("Spotify.exe") };

        Assert.Null(MediaSessions.Pick(sessions, null, @"C:\Start Menu\Programs\Spotify.lnk"));
        Assert.NotNull(MediaSessions.Pick(sessions, null, @"C:\Users\me\AppData\Roaming\Spotify\Spotify.exe"));
    }

    [Fact]
    public void APlayingSessionWinsOverAPausedOneOfTheSameApp()
    {
        var sessions = new[] { Session("MSEdge"), Session("MSEdge", playing: true), Session("Spotify.exe", playing: true) };

        var picked = MediaSessions.Pick(sessions, "MSEdge", @"C:\Edge\msedge.exe");

        Assert.NotNull(picked);
        Assert.True(picked.Playing);
        Assert.Equal("MSEdge", picked.SourceAppId);
    }

    [Fact]
    public void TheFirstWhenNonePlays()
    {
        var first = Session("MSEdge");
        var sessions = new[] { first, Session("MSEdge") };

        Assert.Same(first, MediaSessions.Pick(sessions, "MSEdge", null));
        Assert.Null(MediaSessions.Pick([], "MSEdge", null));
    }

    [Fact]
    public void ControlsCompareByTheirValues()
    {
        // A snapshot read again with nothing new in it equals the old one, so
        // an open preview doesn't redraw for it.
        Assert.Equal(Session("a", playing: true), Session("a", playing: true));
        Assert.NotEqual(Session("a", playing: true), Session("a"));
        Assert.Equal(Session("a").GetHashCode(), Session("a").GetHashCode());
    }
}
