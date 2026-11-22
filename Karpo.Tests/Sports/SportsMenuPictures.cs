// sports §369: the sports line's menu on a real menu, its "Team or league" level open with nothing typed:
// the leagues to browse, each with its logo (a stand-in disc, so the sheet is the same on every machine;
// one still coming wears the glyph until it is in, and the menu is shown again with it), the one followed
// ticked. sports-menu-<dark|light>.png, before and after the logos came. Runs only with KARPO_UI_TESTS=1.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Sports;

public sealed class SportsMenuPictures
{
    /// <summary>A disc of a colour of the address's own, 64 px.</summary>
    private static Pixels Disc(string address)
    {
        const int side = 64;
        var hash = (uint)address.GetHashCode(StringComparison.Ordinal);
        var (r, g, b) = ((byte)(60 + (hash % 160)), (byte)(60 + ((hash >> 8) % 160)), (byte)(60 + ((hash >> 16) % 160)));
        var data = new byte[side * side * 4];
        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var dx = x - 31.5f;
                var dy = y - 31.5f;
                if ((dx * dx) + (dy * dy) <= 30 * 30)
                {
                    var at = ((y * side) + x) * 4;
                    (data[at], data[at + 1], data[at + 2], data[at + 3]) = (b, g, r, 255);
                }
            }
        }
        return new Pixels { Data = data, Width = side, Height = side };
    }

    private static unsafe void Handed(Flyout menu)
    {
        MSG msg;
        var until = Environment.TickCount64 + 250;
        while (Environment.TickCount64 < until)
        {
            while (PeekMessageW(&msg, menu.Hwnd, WM.WM_USER, WM.WM_USER + 0xFF, PM.PM_REMOVE))
            {
                _ = menu.Message(msg.message, msg.wParam, msg.lParam);
            }
            Thread.Sleep(10);
        }
    }

    /// <summary>
    /// §484: the line's View on a real menu — its value at the row's end, "Knockout (not yet)" for a league that has none
    /// now — and its level: Matches, Table and Knockout, each with its glyph, the one kept ticked; a pick goes back.
    /// sports-view-<row|level>-<dark|light>.png.
    /// </summary>
    [UiFact]
    public void TheLeaguesToBrowseWearTheirLogos()
    {
        var was = Flyout.Pictures;
        try
        {
            WindowShots.OnHiddenDesktop("sports-menu", (gfx, instance) =>
            {
                const float scale = 1.5f;
                foreach (var dark in new[] { true, false })
                {
                    // The logos come a moment after they are asked for, as the network's do.
                    var came = new HashSet<string>(StringComparer.Ordinal);
                    var waiting = new List<Action>();
                    var asked = new List<bool>();
                    Flyout.Pictures = (address, onDark, arrived) =>
                    {
                        asked.Add(onDark);
                        if (came.Contains(address))
                        {
                            return Disc(address);
                        }
                        came.Add(address);
                        waiting.Add(arrived);
                        return null;
                    };
                    var look = AddListPictures.LookFor(dark);
                    var tone = dark ? "dark" : "light";
                    var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
                    var anchor = new POINT { x = work.right / 4, y = work.bottom };
                    var item = ListKind.Sports.New();
                    item.Sports = SavedSports.Bundesliga();
                    using var menu = new Flyout(gfx, instance, 0) { Configures = true };
                    List<OptionPick> picks = [];
                    List<FlyoutItem> items = [.. TheDock.WidgetButtons(), .. OptionMenu.Section(OptionRows.Of(item), picks)];
                    menu.Show(gfx, look, scale, items, FlyoutLayout.List, anchor, work, Edge.Bottom, null);
                    WindowShots.Settle();
                    var row = items.First(each => OptionMenu.PickOf(picks, each.Id)?.Key == OptionRows.SportsTeam && each.Children is not null);
                    for (var step = 0; step < 80 && menu.HotId != row.Id; step++)
                    {
                        _ = menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
                    }
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                    WindowShots.Settle();
                    Assert.Equal(1, menu.InlineDepth);
                    var leagues = menu.FieldShowing.Where(shown => SportsLeagues.All.Any(league => league.Name == shown.Text)).ToList();
                    Assert.Equal(SportsLeagues.All.Count, leagues.Count);
                    // Asked for the menu's ground; until they come, the glyph.
                    Assert.All(asked, onDark => Assert.Equal(dark, onDark));
                    Assert.All(leagues, shown => Assert.IsType<FlyoutIcon.Glyph>(shown.Icon));
                    Assert.True(Assert.Single(leagues, shown => shown.Checked == true).Text == "Bundesliga");
                    Assert.True(WindowShots.SaveTogether([.. menu.OpenPanels], $"sports-menu-coming-{tone}.png", dark ? ((byte)0, (byte)0, (byte)0) : ((byte)243, (byte)243, (byte)243)));

                    // They come: the menu is shown again, each row with its logo.
                    foreach (var arrived in waiting)
                    {
                        arrived();
                    }
                    // The menu's posted message handed to it, as the dock's window procedure hands it (the test's hands it nowhere).
                    Handed(menu);
                    WindowShots.Settle();
                    Assert.True(menu.FieldShowing.Where(shown => SportsLeagues.All.Any(league => league.Name == shown.Text)).All(shown => shown.Icon is FlyoutIcon.Picture),
                        $"asked {asked.Count}, waiting {waiting.Count}, open {menu.IsOpen}, depth {menu.InlineDepth}");
                    Assert.True(WindowShots.SaveTogether([.. menu.OpenPanels], $"sports-menu-{tone}.png", dark ? ((byte)0, (byte)0, (byte)0) : ((byte)243, (byte)243, (byte)243)));
                    menu.Hide();
                }
            });
        }
        finally
        {
            Flyout.Pictures = was;
        }
    }
}
