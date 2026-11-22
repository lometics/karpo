// The now-playing line's pure parts: how a time is written and how repeat goes round. What the
// media flyout offered — the timeline, shuffle and repeat, the apps to pick from — is the line's
// now, and tested with the media preset (Folders\MediaPresetTests.cs).

using Karpo.Core;
using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Tests.QuickSettings;

public sealed class MediaTrackTests
{
    [Fact]
    public void TimesAreWrittenAsAPlayerWritesThem()
    {
        Assert.Equal("0:00", MediaModes.Clock(TimeSpan.Zero));
        Assert.Equal("0:07", MediaModes.Clock(TimeSpan.FromSeconds(7)));
        Assert.Equal("3:34", MediaModes.Clock(TimeSpan.FromSeconds(214)));
        Assert.Equal("59:59", MediaModes.Clock(TimeSpan.FromSeconds(3599)));
        Assert.Equal("1:00:00", MediaModes.Clock(TimeSpan.FromHours(1)));
        Assert.Equal("1:02:33", MediaModes.Clock(new TimeSpan(1, 2, 33)));
        // A negative span is the start, not a minus sign.
        Assert.Equal("0:00", MediaModes.Clock(TimeSpan.FromSeconds(-5)));
    }

    [Fact]
    public void RepeatGoesRoundThreeWays()
    {
        Assert.Equal(MediaRepeat.Track, MediaModes.Next(MediaRepeat.None));
        Assert.Equal(MediaRepeat.List, MediaModes.Next(MediaRepeat.Track));
        Assert.Equal(MediaRepeat.None, MediaModes.Next(MediaRepeat.List));
        Assert.Equal("Repeat off", MediaModes.Say(MediaRepeat.None));
        Assert.Equal("Repeat this track", MediaModes.Say(MediaRepeat.Track));
        Assert.Equal("Repeat all", MediaModes.Say(MediaRepeat.List));
    }

    /// <summary>
    /// It is a line of a list with what is playing on it — the art, the track and its own controls —
    /// rather than its glyph and its name; on the bar it is the media preset's face, not an entry.
    /// </summary>
    [Fact]
    public void TheWidgetIsALineOfAList()
    {
        Assert.True(Karpo.Core.Folders.CanAdd(BarKind.FolderList, "media"));
        Assert.True(ListKind.Media.CardInList());
        Assert.Null(BarEntries.Find("media"));
        Assert.Equal(WidgetOptionsKind.None, WidgetInfos.Media.Options);
    }
}
