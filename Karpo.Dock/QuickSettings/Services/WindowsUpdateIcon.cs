// Windows 11's own update icon, as Karpo's tray hears it: the notification icon the update
// orchestrator puts in the tray while it has something to say about updates (MoNotificationUx.exe,
// one icon at a time; the top of WindowsUpdate.cs has what it is and its three dots).
//
// It is an ordinary notification icon to the tray, so on its own it lands among the tray's icons,
// or behind the "^" where Windows' record of it says so, and its picture is whatever the tray makes
// of the icon handed over. Karpo's status icons stand for it instead, where Windows shows it, beside
// the network, sound and battery icons, in their look (Indicators.cs). So:
//
//   - where a bar shows the Status icons entry, the tray leaves Windows' icon out (Dock.TrayAvailable),
//     and the status icons show one icon for updates: Karpo's own reading where it has one, else
//     Windows' icon's word for it — its hover, and its dot as read from its picture — so nothing
//     Windows says is lost where Karpo's reading has nothing (a check that failed, a version at its
//     end of service);
//   - where no bar shows them, the tray shows Windows' icon as it shows any other, as before.
//
// Published by the dock each time it reads the tray (on its own thread), read by the status icons'
// source on any thread.

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>Windows' own update icon in the tray: its hover, and the dot on it.</summary>
internal sealed record WindowsUpdateIcon(string Tip, UpdateBadge Badge);

internal static class WindowsUpdateTray
{
    /// <summary>
    /// The executables Windows' update icon comes from: the update orchestrator's notifications host
    /// (Windows 11's, in C:\Windows\UUS\amd64 or System32), and Windows 10's MusNotifyIcon.exe.
    /// </summary>
    internal static readonly string[] Owners = ["MoNotificationUx.exe", "MusNotifyIcon.exe"];

    private static readonly object Gate = new();
    private static WindowsUpdateIcon? current;

    /// <summary>Windows' icon changed, came or went. Any thread.</summary>
    public static event Action? Changed;

    /// <summary>Windows' icon now, or null while it has none in the tray.</summary>
    public static WindowsUpdateIcon? Current
    {
        get
        {
            lock (Gate)
            {
                return current;
            }
        }
    }

    /// <summary>Whether the tray icon of <paramref name="path"/>'s app is Windows' update icon.</summary>
    internal static bool Is(string? path) =>
        path is { Length: > 0 } && Owners.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Of the tray's <paramref name="icons"/> (each its app's executable by <paramref name="path"/>),
    /// those the bars' trays hold: all of them, but Windows' update icon where a bar shows the status
    /// icons (<paramref name="statusIcons"/>, asked only where Windows' icon is there), which stand
    /// for it. The same list where nothing is left out. Pure.
    /// </summary>
    internal static IReadOnlyList<T> Held<T>(IReadOnlyList<T> icons, Func<T, string?> path, Func<bool> statusIcons) =>
        icons.Any(icon => Is(path(icon))) && statusIcons() ? [.. icons.Where(icon => !Is(path(icon)))] : icons;

    /// <summary>What the tray has of Windows' icon now: says so where that changed.</summary>
    public static void Publish(WindowsUpdateIcon? now)
    {
        lock (Gate)
        {
            if (now == current)
            {
                return;
            }
            current = now;
        }
        Changed?.Invoke();
    }

    /// <summary>
    /// The dot on Windows' icon, read from its picture (premultiplied BGRA, <paramref name="width"/>
    /// by <paramref name="height"/>): the colour around the dot's middle, three quarters of the way
    /// across and down as in every one of its icon files (RestartDot) — blue is the Seeker's, red
    /// with little green the Important's, and orange, red with half its green, the Warning's. None
    /// where nothing is drawn there. Pure.
    /// </summary>
    internal static UpdateBadge BadgeOf(byte[] pixels, int width, int height)
    {
        if (width <= 0 || height <= 0 || pixels.Length < width * height * 4)
        {
            return UpdateBadge.None;
        }
        float blue = 0, green = 0, red = 0, alpha = 0;
        var (across, down) = ((int)(width * RestartDot.Middle), (int)(height * RestartDot.Middle));
        var reach = Math.Max(1, width / 16);
        for (var y = Math.Max(0, down - reach); y <= Math.Min(height - 1, down + reach); y++)
        {
            for (var x = Math.Max(0, across - reach); x <= Math.Min(width - 1, across + reach); x++)
            {
                var at = ((y * width) + x) * 4;
                blue += pixels[at];
                green += pixels[at + 1];
                red += pixels[at + 2];
                alpha += pixels[at + 3];
            }
        }
        if (alpha <= 0)
        {
            return UpdateBadge.None;
        }
        // Premultiplied: the sums over the alpha's are the colour itself.
        (blue, green, red) = (blue / alpha, green / alpha, red / alpha);
        if (blue > red && blue > green)
        {
            return UpdateBadge.Seeker;
        }
        if (red < 0.5f || red < blue)
        {
            return UpdateBadge.None;
        }
        return green >= red * 0.35f ? UpdateBadge.Warning : UpdateBadge.Important;
    }
}
