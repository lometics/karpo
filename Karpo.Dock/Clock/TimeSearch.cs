// What a clock's click keeps between the click and the folder going (docs\widgets.md §377): the
// address that searches the time where the clock stands, with the app's engine (SearchUrls.Time).
// The clock's line and the clock flyout's header share it; the dock opens it once the folder has
// gone (IOpensOnceGone, Dock.FolderForward), as a ticker's search opens (§263).

using Karpo.Core;

namespace Karpo.Dock;

internal sealed class TimeSearch
{
    private string? chosen;

    /// <summary>Where the search opens: the browser, through the shell. A test puts its own here.</summary>
    public Action<string> Opened { get; set; } = static link => Launch.Open(link);

    /// <summary>
    /// A click on a clock of <paramref name="zone"/> (a Windows id; empty for this PC's): the time in the
    /// zone's city, or "time" alone for this PC's own — and for a zone Windows does not know, whose
    /// clock shows this PC's time.
    /// </summary>
    public void Choose(Config? config, string zone) =>
        chosen = SearchUrls.Time(config ?? new Config(), zone.Length > 0 ? Clock.City(Clock.Read(zone, seconds: false).Zone) : "");

    /// <summary>The dock, once the folder has gone: opens the search the click chose, once.</summary>
    public void Open()
    {
        if (chosen is { } link)
        {
            chosen = null;
            Opened(link);
        }
    }
}
