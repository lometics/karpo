// A button on a tray icon is a click its app hears or a drag it never hears of, decided in one place
// (Karpo.Dock\TrayClicks.cs) for every tray: each tray of a bar, at rest, and the overflow.
//
// The person (2026-10-10): "when you try to reorder the tray widgets it left clicks them
// immediately, this should not be." The dock sent WM_LBUTTONDOWN as the button went down
// (Tray.Press, from the rewrite on, 2298bf5; in the overflow from 61bba08), so an app that opens on
// the down opened under every drag. Windows' taskbar sends the click whole as the button comes up
// within the drag threshold, and nothing for a drag. These drive TrayClicks as the dock does — the
// bar's grab asks Moved at every move, the overflow says Dragged at its first DragMove, a release on
// the icon it went down on asks Released — and send what comes of it to a real window, as Tray does.

using System.Runtime.InteropServices;
using Karpo.Dock;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

// Not Karpo.Tests.Tray: a local named "tray" elsewhere in the tests would shadow a namespace of that name.
namespace Karpo.Tests.TrayIcons;

public sealed unsafe class TrayClicksTests
{
    // SM_CXDRAG and SM_CYDRAG as Windows ships them.
    private const int DragX = 4;
    private const int DragY = 4;
    private const uint DoubleClickTime = 500;

    private static readonly POINT OnIcon = new() { x = 1700, y = 1056 };

    private static TrayIcon Icon(string key = "app|1", uint version = 4, HWND owner = default, uint callback = 0x8001) =>
        new(key, owner, 1, Guid.Empty, callback, version, null, "App", false, key);

    private static POINT Off(int x, int y = 0) => new() { x = OnIcon.x + x, y = OnIcon.y + y };

    /// <summary>
    /// A left press on <paramref name="icon"/>, the pointer through <paramref name="path"/>, and the
    /// button up, as a bar's tray hears it: whether it dragged, and the click to send (null for none).
    /// </summary>
    private static (bool Dragged, TrayClick? Click) OnBar(TrayClicks clicks, TrayIcon icon, uint at, params POINT[] path)
    {
        clicks.Pressed(icon, right: false, OnIcon, at, DoubleClickTime);
        var dragged = false;
        foreach (var point in path)
        {
            dragged |= clicks.Moved(point, DragX, DragY);
        }
        return (dragged, clicks.Released(icon.Key, right: false));
    }

    private static uint[] Events(TrayClick click) =>
        [.. click.Gestures.SelectMany(gesture => TrayMessages.For(gesture, click.Icon.Version, click.Icon.Id, OnIcon))
            .Select(pair => TrayMessages.EventOf(click.Icon.Version, pair.LParam))];

    // --- A drag is no click -----------------------------------------------------------------------

    /// <summary>The regression: held, moved past the threshold and let go on a bar's tray — at rest, the chevron coming as it starts — nothing.</summary>
    [Fact]
    public void ADragAlongTheBarSendsItsAppNothing()
    {
        var clicks = new TrayClicks();
        var icon = Icon();

        clicks.Pressed(icon, right: false, OnIcon, 1_000, DoubleClickTime);
        Assert.True(clicks.Holding);
        Assert.False(clicks.Moved(Off(DragX), DragX, DragY));
        // Past the threshold: the drag begins, and the bar lays itself out again with the chevron;
        // the screen point the press was taken at stands through that.
        Assert.True(clicks.Moved(Off(DragX + 1), DragX, DragY));
        Assert.False(clicks.Holding);
        // Back over the place it went down on: a drag still, as Windows keeps it.
        Assert.True(clicks.Moved(OnIcon, DragX, DragY));

        Assert.Null(clicks.Released(icon.Key, right: false));
    }

    [Theory]
    [InlineData(0, DragY + 1)]
    [InlineData(0, -(DragY + 1))]
    [InlineData(-(DragX + 1), 0)]
    [InlineData(40, 2)]
    public void PastTheThresholdEitherWayIsADrag(int x, int y)
    {
        var (dragged, click) = OnBar(new TrayClicks(), Icon(), 1_000, Off(x, y));

        Assert.True(dragged);
        Assert.Null(click);
    }

    /// <summary>The same icon in a bar's second tray (a bar may hold the tray twice): the one rule, so the same answer.</summary>
    [Fact]
    public void ADragInABarsSecondTraySendsNothingEither()
    {
        var clicks = new TrayClicks();
        var icon = Icon();
        var second = new POINT { x = 400, y = OnIcon.y };

        clicks.Pressed(icon, right: false, second, 1_000, DoubleClickTime);
        Assert.False(clicks.Moved(second with { x = second.x + 3 }, DragX, DragY));
        Assert.True(clicks.Moved(second with { x = second.x + 30 }, DragX, DragY));

        Assert.Null(clicks.Released(icon.Key, right: false));
    }

    /// <summary>
    /// In the overflow the flyout tells the drag itself (DragMove, then DragDrop for the up): the dock
    /// says Dragged, and even a Release told after it would send nothing.
    /// </summary>
    [Fact]
    public void ADragOutOfTheOverflowSendsItsAppNothing()
    {
        var clicks = new TrayClicks();
        var icon = Icon("hidden|1");

        clicks.Pressed(icon, right: false, OnIcon, 1_000, DoubleClickTime);
        clicks.Dragged();

        Assert.False(clicks.Holding);
        Assert.Null(clicks.Released(icon.Key, right: false));
    }

    /// <summary>A press called off — Escape, the mouse lost to another window — is no click when the button comes up.</summary>
    [Fact]
    public void APressCalledOffIsNoClick()
    {
        var clicks = new TrayClicks();
        var icon = Icon();

        clicks.Pressed(icon, right: false, OnIcon, 1_000, DoubleClickTime);
        clicks.Forget();

        Assert.Null(clicks.Released(icon.Key, right: false));
    }

    [Fact]
    public void LetGoOnAnotherIconIsNoClick()
    {
        var clicks = new TrayClicks();

        clicks.Pressed(Icon("a|1"), right: false, OnIcon, 1_000, DoubleClickTime);

        Assert.Null(clicks.Released("b|1", right: false));
        // And the press is over: the button coming up again on the first sends nothing.
        Assert.Null(clicks.Released("a|1", right: false));
        Assert.Null(clicks.Released(null, right: false));
    }

    // --- A click is sent whole as the button comes up ---------------------------------------------

    /// <summary>Down and up within the threshold: the click Windows sends, all of it on the release.</summary>
    [Theory]
    [InlineData(4u, new uint[] { WM.WM_LBUTTONDOWN, WM.WM_LBUTTONUP, TrayMessages.NinSelect })]
    [InlineData(3u, new uint[] { WM.WM_LBUTTONDOWN, WM.WM_LBUTTONUP, TrayMessages.NinSelect })]
    [InlineData(0u, new uint[] { WM.WM_LBUTTONDOWN, WM.WM_LBUTTONUP })]
    public void AClickWithinTheThresholdIsSentWholeOnTheRelease(uint version, uint[] expected)
    {
        var (dragged, click) = OnBar(new TrayClicks(), Icon(version: version), 1_000, Off(DragX, -DragY), Off(1, 1));

        Assert.False(dragged);
        var sent = Assert.NotNull(click);
        Assert.False(sent.Right);
        Assert.False(sent.Doubled);
        Assert.Equal(expected, Events(sent));
    }

    /// <summary>Two clicks on the same icon within the double-click time: the second goes down as a double click, as from Explorer.</summary>
    [Fact]
    public void TheSecondOfTwoQuickClicksIsADoubleClick()
    {
        var clicks = new TrayClicks();
        var icon = Icon();

        Assert.Equal([WM.WM_LBUTTONDOWN, WM.WM_LBUTTONUP, TrayMessages.NinSelect], Events(OnBar(clicks, icon, 1_000).Click!.Value));
        Assert.Equal([WM.WM_LBUTTONDBLCLK, WM.WM_LBUTTONUP, TrayMessages.NinSelect], Events(OnBar(clicks, icon, 1_300).Click!.Value));
        // A third starts a pair of its own.
        Assert.False(OnBar(clicks, icon, 1_500).Click!.Value.Doubled);
    }

    /// <summary>A drag between two presses breaks the pair: the click after it is a click, not the second half of one.</summary>
    [Fact]
    public void ADragBetweenTwoClicksIsNoDoubleClick()
    {
        var clicks = new TrayClicks();
        var icon = Icon();

        Assert.NotNull(OnBar(clicks, icon, 1_000).Click);
        Assert.Null(OnBar(clicks, icon, 1_100, Off(30)).Click);
        Assert.False(OnBar(clicks, icon, 1_200).Click!.Value.Doubled);
    }

    /// <summary>The overflow and the bar share the one mouse: a press in the overflow is measured against the bar's click.</summary>
    [Fact]
    public void TheOverflowsClickIsTheSameRule()
    {
        var clicks = new TrayClicks();
        var icon = Icon("hidden|1");

        clicks.Pressed(icon, right: false, OnIcon, 1_000, DoubleClickTime);
        Assert.True(clicks.Holding);
        var click = Assert.NotNull(clicks.Released(icon.Key, right: false));

        Assert.Equal([WM.WM_LBUTTONDOWN, WM.WM_LBUTTONUP, TrayMessages.NinSelect], Events(click));
    }

    /// <summary>A right click is sent on its release too — down, up and the menu — however far the pointer went; the right button drags nothing.</summary>
    [Fact]
    public void ARightClickIsSentOnItsRelease()
    {
        var clicks = new TrayClicks();
        var icon = Icon();

        clicks.Pressed(icon, right: true, OnIcon, 1_000, DoubleClickTime);
        Assert.False(clicks.Moved(Off(30), DragX, DragY));
        Assert.Null(clicks.Released(icon.Key, right: false));

        clicks.Pressed(icon, right: true, OnIcon, 1_100, DoubleClickTime);
        var click = Assert.NotNull(clicks.Released(icon.Key, right: true));
        Assert.Equal([WM.WM_RBUTTONDOWN, WM.WM_RBUTTONUP, WM.WM_CONTEXTMENU], Events(click));
    }

    // --- What the app's window hears --------------------------------------------------------------

    /// <summary>
    /// The click as Tray sends it, to a real window standing for the app's: the three messages of a
    /// click, in order, on the callback message the app gave — and before the release, nothing, as
    /// there is no way left to send a press on its own.
    /// </summary>
    [Fact]
    public void TheAppsWindowHearsTheClickOnlyAsTheButtonComesUp()
    {
        var owner = Owner();
        Heard.Clear();
        try
        {
            var icon = Icon(owner: owner, callback: Callback);
            var clicks = new TrayClicks();
            var tray = new Tray(HWND.NULL, HWND.NULL);

            clicks.Pressed(icon, right: false, OnIcon, 1_000, DoubleClickTime);
            Assert.False(clicks.Moved(Off(2), DragX, DragY));
            Assert.Empty(Heard);

            tray.Click(Assert.NotNull(clicks.Released(icon.Key, right: false)), HWND.NULL);
            Assert.Equal([WM.WM_LBUTTONDOWN, WM.WM_LBUTTONUP, TrayMessages.NinSelect], Heard.Select(lParam => TrayMessages.EventOf(4, lParam)));

            // And a drag: nothing more.
            Heard.Clear();
            clicks.Pressed(icon, right: false, OnIcon, 5_000, DoubleClickTime);
            Assert.True(clicks.Moved(Off(40), DragX, DragY));
            Assert.Null(clicks.Released(icon.Key, right: false));
            Assert.Empty(Heard);
        }
        finally
        {
            DestroyWindow(owner);
        }
    }

    private const uint Callback = WM.WM_APP + 0x7C1;

    [ThreadStatic]
    private static List<nint>? heardOn;

    private static List<nint> Heard => heardOn ??= [];

    [UnmanagedCallersOnly]
    private static LRESULT Procedure(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == Callback)
        {
            Heard.Add(lParam);
            return 0;
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    /// <summary>A message-only window of a class of this test's own, its procedure the one above.</summary>
    private static HWND Owner()
    {
        var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
        fixed (char* name = "KarpoTrayClicksTestsOwner")
        {
            var cls = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = &Procedure,
                hInstance = instance,
                lpszClassName = name,
            };
            // A second run in this process finds the class there already, with this procedure.
            _ = RegisterClassExW(&cls);
            var window = CreateWindowExW(0, name, null, 0, 0, 0, 0, 0, HWND.HWND_MESSAGE, HMENU.NULL, instance, null);
            Assert.True(window != HWND.NULL, $"CreateWindowExW failed: error {Marshal.GetLastSystemError()}");
            return window;
        }
    }
}
