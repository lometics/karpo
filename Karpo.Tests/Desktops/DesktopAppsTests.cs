// What a desktop's row shows of the apps open on it (DesktopApps): one icon per
// app, the app used last first, and "+N" for the rest.
//
// The two "may the new icons take the old ones' places?" answers went with the
// strip of buttons on the bar: what is open on a desktop is in the list the
// switcher opens now, which is built afresh each time it is shown.

using Karpo.Dock;

// Not Karpo.Tests.Desktops: the core tests name BarKind.Desktops as "Desktops" (using static),
// which a namespace of that name would shadow.
namespace Karpo.Tests.DesktopsArea;

public sealed class DesktopAppsTests
{
    private static PreviewWindow Window(string key, nint hwnd, bool minimized = false) =>
        new(key, false, $"window {hwnd}", hwnd, minimized, null, @"C:\apps\" + key + ".exe");

    [Fact]
    public void TopOfTheStackComesFirst()
    {
        // Regression: icons were taken bottom of the stack first, so the app
        // used last showed last.
        var bottomToTop = new[] { Window("exe:a", 1), Window("exe:b", 2), Window("exe:c", 3) };

        var apps = DesktopApps.Distinct(bottomToTop);

        Assert.Equal(new[] { "exe:c", "exe:b", "exe:a" }, apps.Select(app => app.Key));
    }

    [Fact]
    public void OneIconPerAppTakenFromItsTopmostWindow()
    {
        var bottomToTop = new[] { Window("exe:a", 1), Window("exe:b", 2), Window("EXE:A", 3), Window("exe:b", 4) };

        var apps = DesktopApps.Distinct(bottomToTop);

        Assert.Equal(new nint[] { 4, 3 }, apps.Select(app => app.Hwnd));
    }

    [Fact]
    public void MinimisedWindowsCountAndWindowsWithoutAKeyDoNot()
    {
        var bottomToTop = new[] { Window("exe:a", 1, minimized: true), Window("", 2), Window("exe:b", 3) };

        var apps = DesktopApps.Distinct(bottomToTop);

        Assert.Equal(new[] { "exe:b", "exe:a" }, apps.Select(app => app.Key));
    }

    [Fact]
    public void TheIconsARowHoldsThenACount()
    {
        var (icons, more) = DesktopApps.Split(new[] { 1, 2, 3, 4, 5, 6 }, Switchers.RowIcons);

        Assert.Equal(new[] { 1, 2, 3, 4 }, icons);
        Assert.Equal(2, more);
    }

    [Fact]
    public void NoCountWhenAllFit()
    {
        var (icons, more) = DesktopApps.Split(new[] { 1, 2, 3 }, Switchers.RowIcons);

        Assert.Equal(3, icons.Count);
        Assert.Equal(0, more);
        Assert.Empty(DesktopApps.Split(Array.Empty<int>(), Switchers.RowIcons).Icons);
    }
}
