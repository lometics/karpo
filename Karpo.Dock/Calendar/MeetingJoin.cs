// Joining a meeting (docs\widgets.md §205): the Join button on the next meeting's face and on its
// line open the meeting's link with the app it is for. A Teams or Zoom link that maps onto the app's
// own address (MeetingLinks.AppUri) goes to the app straight away where it is installed — msteams: and
// zoommtg: are registered then — so no browser tab is left behind to close; any other link, and those
// where the app is not there, open in the default browser, whose page offers the app or the web.
//
// Off the dock's thread, as every link Karpo opens: the shell may take a moment to find the handler.
// §665: the shell is waited for there (Launch.Aside), and a meeting that does not open at all says so —
// "Couldn't open the meeting" and Windows' reason — rather than nothing. A meeting with no link Karpo
// knows has no Join to press (JoinButton.Of), so there is no "no link" to say.

using Karpo.Accounts;
using Microsoft.Win32;

namespace Karpo.Dock;

internal static class MeetingJoin
{
    [ThreadStatic]
    private static Action<string>? opening;

    /// <summary>
    /// For a test, per thread (the dock's, which Join is clicked on): what opens a meeting's address, or an event's
    /// (EventLinks), instead of the shell.
    /// </summary>
    internal static Action<string>? Opening
    {
        get => opening;
        set => opening = value;
    }

    /// <summary>
    /// What <paramref name="link"/> is opened with: the app's own address where it has one and
    /// <paramref name="registered"/> says its scheme has a handler on this PC, else the link itself.
    /// </summary>
    internal static string Target(MeetingLink link, Func<string, bool> registered) =>
        MeetingLinks.AppUri(link) is { } app && app != link.Url && registered(app[..app.IndexOf(':')]) ? app : link.Url;

    /// <summary>
    /// Opens <paramref name="link"/>'s meeting (<see cref="Target"/>); the browser where the app's address does not open,
    /// and §665 "Couldn't open the meeting" where neither does.
    /// </summary>
    public static void Open(MeetingLink link) => Open(link, Registered);

    /// <summary>The same, with <paramref name="registered"/> saying which schemes have a handler (a test's).</summary>
    internal static void Open(MeetingLink link, Func<string, bool> registered)
    {
        var target = Target(link, registered);
        Start(target, target != link.Url ? link.Url : null, S.DockMeetingJoinFailed);
    }

    /// <summary>
    /// Opens <paramref name="target"/> through the launcher, off the dock's thread, and <paramref name="otherwise"/> where
    /// it does not open (<see cref="Launch.Aside"/>), saying <paramref name="failure"/> (else the launcher's own words) where
    /// neither does; a test's <see cref="Opening"/> is handed <paramref name="target"/> instead.
    /// </summary>
    internal static void Start(string target, string? otherwise = null, string? failure = null)
    {
        if (Opening is { } stand)
        {
            stand(target);
            return;
        }
        Launch.Aside(target, otherwise, failure);
    }

    /// <summary>Whether <paramref name="scheme"/> (msteams, zoommtg) has a handler on this PC: an app said it opens such addresses.</summary>
    private static bool Registered(string scheme)
    {
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(scheme);
            return key?.GetValue("URL Protocol") is not null; // not-ui: the registry's value name
        }
        catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}
