// §664, the person (2026-11-10): "when clicking refresh in the context menu it closed the panel, this should never happen".
//
// A pick in a widget's menu that acts in place — Refresh, a switch, a copy, a choice — leaves the folder the menu stood over
// open; only a pick that leaves Karpo (an app, a file, a page, a Windows panel) closes it (Dock.HandsAway, FolderAfterMenu),
// and the focus closes it only where another program took it (Dock.ForegroundTakenFromKarpo) — never because the folder is
// not the window in front the moment the menu ends. Asked of the real dock (Karpo.exe --stage on a desktop of its own, where
// no window of the dock's is ever in front), at rest: a folder opened from its face, a feed line's right click, "Refresh"
// clicked as the mouse clicks it — the folder is still open, the line read again.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using Karpo.Tests.Present;
using TerraFX.Interop.Windows;
using Xunit.Abstractions;
using static TerraFX.Interop.Windows.Windows;
using Edge = Karpo.Core.Edge;
using Spot = Karpo.Tests.Present.PanelPopupPlaceTests.Spot;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

public sealed unsafe class MenuPickInPlaceTests(ITestOutputHelper output)
{
    private const string Folder = "inplace-folder";

    private const string Feed = Folder + "-feed";

    /// <summary>
    /// At rest, a folder of a clock and a feed line whose address is a listener of the test's on this PC (it hangs up on
    /// every read, so each read fails at once and nothing leaves the machine): the feed's right click, Escape — the folder
    /// stays (§185); its right click again, "Refresh" clicked as the mouse clicks it — the menu goes, the folder stays open,
    /// and the feed is read again.
    /// </summary>
    [UiFact]
    public void RefreshFromALinesMenuLeavesTheFolderOpen()
    {
        using var reads = new Reads();
        PanelPopupPlaceTests.Drive(output, Edge.Bottom, null, probe =>
        {
            var face = probe.Read("stage-map.json", wait: true).First(spot => spot.Key.EndsWith("/" + Folder, StringComparison.Ordinal));
            probe.Click(face.Center);
            Assert.True(Wait(probe, () => Open(probe) is not null), "the folder did not open at rest");
            var line = Wait(probe, () => Lines(probe).FirstOrDefault(spot => spot.Key.EndsWith("/" + Feed, StringComparison.Ordinal)), "the feed line");

            // Escape on its menu: the folder stays (§185).
            RightClick(line.Center);
            Assert.True(probe.WaitMenu(open: true), "the feed's right click opened no menu");
            probe.Escape();
            Assert.True(probe.WaitMenu(open: false), "Escape left the menu open");
            probe.Pump(500);
            Assert.True(Open(probe) is not null, "Escape on a line's menu closed the folder");

            // "Refresh", clicked: the menu goes, the folder stays, the feed read again.
            RightClick(line.Center);
            Assert.True(probe.WaitMenu(open: true), "the feed's right click opened no menu");
            var refresh = Wait(probe, () => Rows(probe).FirstOrDefault(row => row.Key == S.DockWidgetMenuRefresh), "Refresh in the feed's menu");
            var before = reads.Count;
            probe.Click(refresh.Center);
            Assert.True(probe.WaitMenu(open: false), "picking Refresh left the menu open");
            probe.Pump(500);
            Assert.True(Open(probe) is not null, "picking Refresh in a line's menu closed the folder");
            Assert.True(Wait(probe, () => reads.Count > before), $"Refresh did not read the feed again ({reads.Count} reads)");
            Assert.True(Open(probe) is not null, "the feed read again closed the folder");
        }, config =>
        {
            var options = new FolderOptions();
            var island = BarKind.FolderList.New();
            island.Id = Folder;
            island.Folder = options;
            island.Section = BarSection.Middle;
            var clock = ListKind.Clock.New();
            clock.Id = Folder + "-clock";
            options.Put(clock);
            var feed = ListKind.Rss.New();
            feed.Id = Feed;
            feed.Rss = new RssOptions { Address = reads.Address, Title = "Nobody's news" };
            options.Put(feed);
            config.DefaultLayout.Islands.Add(island);
            Sections.Order(config.DefaultLayout.Islands);
        });
    }

    /// <summary>
    /// Where the focus stands as a menu over a folder ends closes the folder only where another program has it: none in
    /// front, or a window of Karpo's (the folder, the menu still going), is not the focus taken.
    /// </summary>
    [Fact]
    public void OnlyAnotherProgramInFrontIsTheFocusTaken()
    {
        Assert.False(TheDock.ForegroundTakenFromKarpo(HWND.NULL));
        // A window of this process's own.
        HWND own;
        fixed (char* kind = "STATIC")
        {
            own = CreateWindowExW(0, kind, null, 0, 0, 0, 10, 10, HWND.HWND_MESSAGE, HMENU.NULL, HINSTANCE.NULL, null);
        }
        try
        {
            Assert.NotEqual(HWND.NULL, own);
            Assert.False(TheDock.ForegroundTakenFromKarpo(own));
        }
        finally
        {
            _ = DestroyWindow(own);
        }
        // The desktop's window is Windows' own, another process's.
        Assert.True(TheDock.ForegroundTakenFromKarpo(GetDesktopWindow()));
    }

    /// <summary>A listener on this PC's loopback that counts the feed's reads and hangs up on each, so it fails at once.</summary>
    private sealed class Reads : IDisposable
    {
        private readonly System.Net.Sockets.TcpListener listener = new(System.Net.IPAddress.Loopback, 0);
        private int count;

        public Reads()
        {
            listener.Start();
            new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        using var client = listener.AcceptTcpClient();
                        _ = Interlocked.Increment(ref count);
                    }
                    catch (Exception error) when (error is System.Net.Sockets.SocketException or ObjectDisposedException or InvalidOperationException)
                    {
                        return;
                    }
                }
            }) { IsBackground = true }.Start();
        }

        public string Address => $"http://127.0.0.1:{((System.Net.IPEndPoint)listener.LocalEndpoint).Port}/feed.xml";

        public int Count => Volatile.Read(ref count);

        public void Dispose() => listener.Stop();
    }

    /// <summary>The folder open (stage-edit.json's "FolderOpen"); null for none.</summary>
    private static Spot? Open(PanelPopupPlaceTests.Probe probe) => probe.Read("stage-edit.json").FirstOrDefault(spot => spot.Hit == "FolderOpen");

    /// <summary>The open folders' lines (stage-edit.json's "FolderLine").</summary>
    private static List<Spot> Lines(PanelPopupPlaceTests.Probe probe) => [.. probe.Read("stage-edit.json").Where(spot => spot.Hit == "FolderLine")];

    /// <summary>The open menu's rows (stage-edit.json's "MenuRow"), each by its middle.</summary>
    private static List<Spot> Rows(PanelPopupPlaceTests.Probe probe) => [.. probe.Read("stage-edit.json").Where(spot => spot.Hit == "MenuRow")];

    private static bool Wait(PanelPopupPlaceTests.Probe probe, Func<bool> done)
    {
        var until = Environment.TickCount64 + 5000;
        while (!done())
        {
            if (Environment.TickCount64 > until)
            {
                return false;
            }
            probe.Pump(100);
        }
        return true;
    }

    private static T Wait<T>(PanelPopupPlaceTests.Probe probe, Func<T?> found, string what)
        where T : class
    {
        T? it = null;
        Assert.True(Wait(probe, () => (it = found()) is not null), $"no {what}");
        return it!;
    }

    /// <summary>A right click at <paramref name="at"/> (screen px), posted to the dock's window there.</summary>
    private static void RightClick(POINT at)
    {
        var window = GetAncestor(WindowFromPoint(at), GA.GA_ROOT);
        RECT rect;
        _ = GetWindowRect(window, &rect);
        var where = (LPARAM)(nint)(((at.y - rect.top) << 16) | ((at.x - rect.left) & 0xFFFF));
        _ = PostMessageW(window, WM.WM_MOUSEMOVE, 0, where);
        _ = PostMessageW(window, WM.WM_RBUTTONDOWN, 0x0002, where);
        _ = PostMessageW(window, WM.WM_RBUTTONUP, 0, where);
    }
}
