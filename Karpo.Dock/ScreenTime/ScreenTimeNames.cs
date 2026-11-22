// An app of screen time as a person knows it: the name and the logo the Start menu shows, and the
// pin a click on its row opens (the pins' one rule: its windows brought forward where it is open,
// else started). Looked up once per app and kept, for every line that lists it.

using Karpo.Core;

namespace Karpo.Dock;

/// <summary>What an app of screen time is called, looks like and opens.</summary>
internal sealed record ScreenTimeAppInfo(string Name, Pixels? Logo, Item? Item);

internal static class ScreenTimeNames
{
    /// <summary>The logo's size asked of the shell, px: the largest a list's row draws it at 200 %.</summary>
    private const int LogoPx = 48;

    private static readonly Dictionary<string, ScreenTimeAppInfo> Known = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>How an app is looked up; a test stands in for the shell here.</summary>
    internal static Func<string, ScreenTimeAppInfo> Look { get; set; } = Resolve;

    /// <summary><paramref name="app"/>'s (a screen-time key, <see cref="ScreenTimeApps.KeyOf"/>) name, logo and pin.</summary>
    public static ScreenTimeAppInfo Of(string app)
    {
        lock (Known)
        {
            if (Known.TryGetValue(app, out var found))
            {
                return found;
            }
        }
        ScreenTimeAppInfo made;
        try
        {
            made = Look(app);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            made = new ScreenTimeAppInfo(Fallback(app), null, null);
        }
        lock (Known)
        {
            if (Known.Count >= 128)
            {
                Known.Clear();
            }
            Known[app] = made;
        }
        return made;
    }

    /// <summary>Forgets every app looked up: a test's stand-in, or another language's names.</summary>
    internal static void Forget()
    {
        lock (Known)
        {
            Known.Clear();
        }
    }

    /// <summary>A name with nothing asked of the shell: the AUMID's app, or the executable's stem.</summary>
    internal static string Fallback(string app) =>
        ScreenTimeApps.AumidOf(app) is { } aumid ? NotificationText.FallbackName(aumid) : Items.FileStem(app) ?? app;

    private static ScreenTimeAppInfo Resolve(string app)
    {
        if (ScreenTimeApps.AumidOf(app) is { } aumid)
        {
            var target = $@"shell:AppsFolder\{aumid}";
            var item = Item.Resolve(target) is { Kind: ItemKind.Shell } found
                && !string.IsNullOrWhiteSpace(found.Name)
                && !found.Name.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)
                    ? found
                    : null;
            return new ScreenTimeAppInfo(item?.Name ?? NotificationText.FallbackName(aumid), Items.Icon(target, LogoPx), item);
        }
        var running = Item.Running(null, app);
        return new ScreenTimeAppInfo(running.Name, Items.Icon(app, LogoPx), File.Exists(app) ? running : null);
    }
}
