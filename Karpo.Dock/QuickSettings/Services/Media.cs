// What the now-playing line says of a session besides its track (MediaIsland, MediaLine.cs): the
// apps playing to pick from, how an app repeats, a time as a player writes it, and an app's name.
//
// It was the media flyout's service too, its own reading of the sessions (WindowsMedia) beside the
// line's, until the flyout went with the now-playing entry (2026-10-10, docs\widgets.md §96): the
// line reads the sessions for itself and has all the flyout had.

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>How an app repeats: what GSMTC's AutoRepeatMode says, or null where it says nothing.</summary>
internal enum MediaRepeat
{
    None,
    Track,
    List,
}

/// <summary>One session to pick from: the app it belongs to, its icon, and whether it is playing.</summary>
/// <param name="Key">The session's source app id, which is how it is chosen again.</param>
/// <param name="Icon">
/// The app's icon (NowPlayingSource.AppIcon: a packaged app's logo, a desktop app's exe icon, a
/// browser's own), beside its name in the list to pick from; null where none was found.
/// </param>
internal readonly record struct MediaEntry(string Key, string App, bool Playing, Pixels? Icon = null);

/// <summary>The next repeat mode a click asks for: none, the track, the list, and round again.</summary>
internal static class MediaModes
{
    public static MediaRepeat Next(MediaRepeat repeat) => repeat switch
    {
        MediaRepeat.None => MediaRepeat.Track,
        MediaRepeat.Track => MediaRepeat.List,
        _ => MediaRepeat.None,
    };

    public static string Say(MediaRepeat repeat) => repeat switch
    {
        MediaRepeat.Track => S.WidgetMediaRepeatTrack,
        MediaRepeat.List => S.WidgetMediaRepeatAll,
        _ => S.WidgetMediaRepeatOff,
    };

    /// <summary>A time as a player writes it: 3:07, or 1:02:33 past an hour.</summary>
    public static string Clock(TimeSpan span)
    {
        if (span < TimeSpan.Zero)
        {
            span = TimeSpan.Zero;
        }
        var whole = (int)span.TotalSeconds;
        var (hours, minutes, seconds) = (whole / 3600, whole / 60 % 60, whole % 60);
        return hours > 0
            ? $"{hours}:{minutes:00}:{seconds:00}"
            : $"{minutes}:{seconds:00}";
    }
}

/// <summary>What an app is called, from the id a session carries.</summary>
internal static class MediaNames
{
    /// <summary>The app's name as Start shows it, or the executable's name for a desktop app.</summary>
    public static string Of(string sourceAppId)
    {
        if (string.IsNullOrWhiteSpace(sourceAppId))
        {
            return "";
        }
        var id = sourceAppId.Trim();
        if (AppLooks.NameOf(id, id.Contains('!') ? id : null, id.Contains('!') ? null : id, null) is { Length: > 0 } name)
        {
            return name;
        }
        var stem = Items.FileStem(id);
        return stem is { Length: > 0 } ? stem : id;
    }
}
