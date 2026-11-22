// app clicks: files dragged over the bar and dropped on it, as Windows 11's taskbar takes them.
//
// Resting on an app's button with its windows open brings its window forward after Windows' hover
// time, so the files can be dropped into it (DragHover). Dropped on an app's button they open with
// that app — its program, or a store app by its ID — where it is one that opens files (Decide);
// dropped on a folder they go into it; anywhere else, or on a pin that is not an app, they are
// pinned where the line shows, as Karpo's bar always pinned what was dropped on it. The button of
// an open app that nothing here knows how to start takes nothing: pinning the files beside it, as
// the bar once did, was nobody's idea of dropping them on an app.

namespace Karpo.Dock;

/// <summary>app clicks: what files dropped on the bar do.</summary>
internal enum DropKind
{
    /// <summary>Pinned where the line shows.</summary>
    Pin,

    /// <summary>Into the folder they were dropped on.</summary>
    IntoFolder,

    /// <summary>Opened with a program: <see cref="DropAction.App"/> is its path.</summary>
    OpenWith,

    /// <summary>Opened with a store app: <see cref="DropAction.App"/> is its ID.</summary>
    OpenApp,

    /// <summary>Nothing: an app's button that no program here stands for.</summary>
    Nothing,
}

/// <summary>app clicks: what files dropped on the bar do, and with what.</summary>
internal readonly record struct DropAction(DropKind Kind, string? App = null);

/// <summary>app clicks: what is under files dropped on the bar.</summary>
/// <param name="Folder">A folder that takes pins.</param>
/// <param name="Button">An app's button: a pin, or an open app.</param>
/// <param name="Pinned">That button is a pin — which, when it is not an app, still takes the files by pinning them beside it.</param>
/// <param name="Program">The program the pin starts: an .exe, or the one a shortcut points at.</param>
/// <param name="Aumid">The app's ID, where it has one.</param>
/// <param name="WindowExe">The program of one of the button's open windows.</param>
internal readonly record struct DropSpot(
    bool Folder = false,
    bool Button = false,
    bool Pinned = false,
    string? Program = null,
    string? Aumid = null,
    string? WindowExe = null);

internal static class AppDrops
{
    private const string AppsFolder = @"shell:AppsFolder\";

    /// <summary>What files dropped on <paramref name="spot"/> do (see the top of the file).</summary>
    internal static DropAction Decide(DropSpot spot)
    {
        if (spot.Folder)
        {
            return new DropAction(DropKind.IntoFolder);
        }
        if (!spot.Button)
        {
            return new DropAction(DropKind.Pin);
        }
        if (spot.Program is { Length: > 0 } program)
        {
            return new DropAction(DropKind.OpenWith, program);
        }
        if (Packaged(spot.Aumid))
        {
            return new DropAction(DropKind.OpenApp, spot.Aumid);
        }
        if (spot.WindowExe is { Length: > 0 } exe && !IsFrameHost(exe))
        {
            return new DropAction(DropKind.OpenWith, exe);
        }
        return new DropAction(spot.Pinned ? DropKind.Pin : DropKind.Nothing);
    }

    /// <summary>
    /// Whether <paramref name="aumid"/> is a store app's ID — its package's family and its app,
    /// "Family!App" —, which Windows opens files with by that ID alone.
    /// </summary>
    internal static bool Packaged(string? aumid) =>
        aumid is { Length: > 2 } id && id.IndexOf('!') is var bang && bang > 0 && bang < id.Length - 1;

    /// <summary>The app ID of an Apps folder target (<c>shell:AppsFolder\ID</c>); null for any other target.</summary>
    internal static string? AumidIn(string target) =>
        target.Trim().StartsWith(AppsFolder, StringComparison.OrdinalIgnoreCase) && target.Trim().Length > AppsFolder.Length
            ? target.Trim()[AppsFolder.Length..]
            : null;

    /// <summary>The frame every store app's windows belong to, which opens nothing itself.</summary>
    internal static bool IsFrameHost(string exe) =>
        Path.GetFileName(exe).Equals("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// app clicks: a drag resting on an app's button. Once it has stayed on the same button for
/// Windows' hover time, that button's window comes forward — once, until the drag moves to another
/// button, leaves the bar or is dropped.
/// </summary>
internal sealed class DragHover(uint delayMs)
{
    private int tile = -1;
    private long since;
    private bool done;

    /// <summary>How long the drag rests before the window comes, in ms.</summary>
    public uint DelayMs { get; } = delayMs;

    /// <summary>
    /// The drag is over tile <paramref name="button"/> at <paramref name="now"/> (ms) — -1 for none,
    /// or for a button with nothing to bring forward: how long until it is due, or null while
    /// nothing is waited for (no button, or its window already came).
    /// </summary>
    public uint? Over(int button, long now)
    {
        if (button != tile)
        {
            tile = button;
            since = now;
            done = false;
        }
        if (tile < 0 || done)
        {
            return null;
        }
        return (uint)Math.Max(0L, since + DelayMs - now);
    }

    /// <summary>At <paramref name="now"/>: the button whose window comes forward, once; -1 while none is due.</summary>
    public int Due(long now)
    {
        if (tile < 0 || done || now < since + DelayMs)
        {
            return -1;
        }
        done = true;
        return tile;
    }

    /// <summary>The drag left the bar or was dropped: nothing is waited for.</summary>
    public void Leave()
    {
        tile = -1;
        done = false;
    }
}
