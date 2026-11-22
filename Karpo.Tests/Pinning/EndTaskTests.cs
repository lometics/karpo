// "End task" in an app's jump list, where Windows' own developer setting puts it (EndTask): where the
// row stands, and which processes it would end. Decided without ending anything: no process is
// opened, let alone ended, and Windows' setting is not read.

using Karpo.Dock;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Pinning;

public sealed class EndTaskTests
{
    private static readonly FlyoutIcon AppIcon = new FlyoutIcon.Glyph("");

    private static List<string> Texts(IEnumerable<FlyoutItem> rows) => [.. rows.Select(row => row.Text)];

    private static List<FlyoutItem> Menu(int open, bool endTask, Edge edge = Edge.Bottom) =>
        TheDock.AppRows([], "Notepad", AppIcon, pinned: true, open, buttons: null, edge, endTask);

    /// <summary>
    /// On, a running app's menu has End task just above Close window, as Windows 11 has it: the
    /// closing row stays the one nearest the pointer, and End task stands beside it after the rule.
    /// </summary>
    [Fact]
    public void EndTaskStandsJustAboveTheClosingRow() =>
        Assert.Equal(["Notepad", "Unpin from taskbar", "", "End task", "Close window"], Texts(Menu(open: 1, endTask: true)));

    /// <summary>On a top bar the closing row comes first, and End task after it, still beside it.</summary>
    [Fact]
    public void OnATopBarEndTaskFollowsTheClosingRow() =>
        Assert.Equal(["Close all windows", "End task", "", "Notepad", "Unpin from taskbar"], Texts(Menu(open: 2, endTask: true, Edge.Top)));

    /// <summary>Off, the menu is what it always was; with nothing open there is nothing to end either way.</summary>
    [Fact]
    public void OffOrWithNothingOpenThereIsNoEndTask()
    {
        Assert.DoesNotContain("End task", Texts(Menu(open: 1, endTask: false)));
        Assert.DoesNotContain("End task", Texts(Menu(open: 0, endTask: true)));
        Assert.Equal(Texts(Menu(open: 3, endTask: false)), Texts(Menu(open: 3, endTask: true)).Where(text => text != "End task"));
    }

    /// <summary>Each window's process once, and never Karpo's own or the system's.</summary>
    [Fact]
    public void EachProcessIsEndedOnceAndNeverKarposOrTheSystems()
    {
        TaskWindow[] windows =
        [
            new(100, @"C:\Apps\editor.exe"),
            new(100, @"C:\Apps\editor.exe"),
            new(200, @"C:\Apps\helper.exe"),
            new(42, @"C:\Karpo\Karpo.exe"),
            new(4, "System"),
            new(0, ""),
        ];

        Assert.Equal([100u, 200u], EndTask.Processes(windows, own: 42));
    }

    /// <summary>
    /// A store app's windows belong to its frame, which every store app shares: the app's own process
    /// inside it is ended, and where there is none known, nothing — never the frame.
    /// </summary>
    [Fact]
    public void AStoreAppsFrameIsNeverEndedOnlyTheAppInsideIt()
    {
        TaskWindow[] windows =
        [
            new(300, @"C:\Windows\System32\ApplicationFrameHost.exe", Inside: 310),
            new(300, @"C:\Windows\System32\applicationframehost.EXE", Inside: 0),
            new(300, @"C:\Windows\System32\ApplicationFrameHost.exe", Inside: 310),
        ];

        Assert.Equal([310u], EndTask.Processes(windows, own: 42));
    }
}
