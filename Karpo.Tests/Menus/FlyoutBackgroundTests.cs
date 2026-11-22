// The person, 2026-10-29: "i believe that flyout background does not properly update". The open menu is the
// one the colour is picked in; the dock shows it again after the pick (Dock.MenuAgain → Flyout.Refresh), and
// what it shows it in must be the look as it is now, not the one it was opened in. And the cheap path for a
// change of colours alone (Dock.Recolour) re-dresses every popup standing open, as a build does.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1 (the source pin needs neither).

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Menus;

public sealed class FlyoutBackgroundTests
{
    private const float Scale = 1.5f;

    private static readonly Argb Navy = new(0xFF, 0x1E, 0x3A, 0x5F);
    private static readonly Argb Rose = new(0xFF, 0x8E, 0x2A, 0x4A);

    private static Color Ink(Argb color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    private static List<FlyoutItem> Rows() =>
        [new FlyoutItem(1, "Taskbar background"), new FlyoutItem(2, "Flyout background"), new FlyoutItem(3, "Widget cards")];

    /// <summary>
    /// The menu the flyout background is picked in, shown again with the look as it is after the pick: its
    /// panel wears the new colour at once, drawn and captured, not the one it opened in.
    /// </summary>
    [UiFact]
    public void AnOpenMenuShownAgainWearsTheFlyoutBackgroundAsItIsNow()
    {
        var failures = new List<string>();
        WindowShots.OnHiddenDesktop("flyout-background", (gfx, instance) =>
        {
            var before = AddListPictures.LookFor(dark: true) with { Animations = false, Colors = new Palette(Flyout: Navy) };
            var after = before with { Colors = new Palette(Flyout: Rose) };
            var work = new RECT { right = (int)(1920 * Scale), bottom = (int)(1040 * Scale) };
            using var menu = new Flyout(gfx, instance, 0) { Configures = true };
            menu.Show(gfx, before, Scale, Rows(), FlyoutLayout.List, new POINT { x = work.right / 3, y = work.bottom }, work, Edge.Bottom, null);
            WindowShots.Settle();
            if (menu.Theme.Base != Ink(Navy))
            {
                failures.Add($"opened: the panel is {menu.Theme.Base}, not navy");
            }

            menu.Refresh(Rows(), look: after);
            WindowShots.Settle();
            if (menu.Theme.Base != Ink(Rose))
            {
                failures.Add($"shown again after the pick: the panel is {menu.Theme.Base}, not the rose picked");
            }
            byte[] pixels = [];
            Assert.True(WindowShots.Save(menu.Hwnd, "flyout-background-again-150.png", seen: (seen, _) => pixels = seen));
            // The plate drawn: most of the window is the panel, so most of its pixels are the colour picked.
            var rose = 0;
            for (var at = 0; at + 3 < pixels.Length; at += 4)
            {
                rose += Math.Abs(pixels[at + 2] - Rose.R) + Math.Abs(pixels[at + 1] - Rose.G) + Math.Abs(pixels[at] - Rose.B) <= 9 ? 1 : 0;
            }
            if (rose < pixels.Length / 4 / 3)
            {
                failures.Add($"shown again after the pick: {rose} of {pixels.Length / 4} pixels drawn are the rose picked");
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>
    /// The dock's two ways of showing a change: a build re-dresses the open folders and reads the open menu
    /// again with the window's look (Arrange's end); the cheap path for colours alone did neither — it
    /// returned once the bars were recoloured. Both now end in the one place, <c>Relooked</c>, and the open
    /// menu is always read again in its window's look.
    /// </summary>
    [Fact]
    public void EveryPathOfAChangeDressesTheOpenPopupsAgainInTheWindowsLook()
    {
        var dock = RepoSource.Read("Karpo.Dock", "Dock.cs");
        // §696: whether only the colours differ is asked once (colours), for the cheap path and for a kept colour built after all.
        Assert.Contains("var colours = OnlyColorsDiffer(before, next);", dock);
        var cheap = dock[dock.IndexOf("if (colours && Recolour())", StringComparison.Ordinal)..];
        cheap = cheap[..cheap.IndexOf("return;", StringComparison.Ordinal)];
        Assert.Contains("Relooked();", cheap);

        var sessions = RepoSource.Read("Karpo.Dock", "Dock.MenuSessions.cs");
        // §514: in what the popup is shown in, its window's look as a menu or as a flyout (Dock.ShownIn).
        Assert.Contains("look: what.Owner is { } window ? ShownIn(what, window) : null", sessions);
    }
}
