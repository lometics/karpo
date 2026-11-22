// markets: the one search on a real menu (docs\widgets.md §258), on a desktop of the test's own
// (KARPO_UI_TESTS=1), the service's answers canned: its level just opened — what the widget tracks ticked,
// then what the others track — and "eur" typed, the pairs it names over the service's share, index and coin,
// each with its type's glyph and what it is at its end. Pictures in KARPO_PNG_DIR at 150 %, dark and light:
// markets-search-open-<look>.png, markets-search-eur-<look>.png.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Markets;

[Collection(InLanguage.Collection)]
public sealed unsafe class MarketSearchPictures
{
    private const float Scale = 1.5f;

    private static readonly RECT Work = new() { right = (int)(1920 * Scale), bottom = (int)(1040 * Scale) };

    private static readonly POINT Anchor = new() { x = Work.right / 3, y = Work.bottom };

    /// <summary>A markets line on CHF/USD's menu, its search answering from <see cref="MarketRowsTests.EurAnswer"/>: the items, and the symbol's row.</summary>
    private static (List<FlyoutItem> Items, FlyoutItem Symbol) Menu()
    {
        var line = ListKind.Markets.New();
        line.Market = new MarketOptions { Symbol = "CHFUSD=X" };
        List<Listing> used = [new("AAPL", "Apple Inc.", "", ""), new("BTC-USD", "", "", "")];
        List<OptionPick> picks = [];
        List<FlyoutItem> items = [new(1, "Remove", Keys: MenuChord.Delete), .. OptionMenu.Rows(OptionRows.Of(line, new OptionDevices { Shares = used }), picks)];
        var symbol = items.Single(item => item.Text == S.SettingsWidgetMarketSymbolHeader);
        var field = symbol.Children!.Single(item => item.Control is FlyoutField);
        var finder = new MarketFinder(MarketRows.Share(line.Market), used, "CHF", (_, cancel) =>
            Task.Delay(60, cancel).ContinueWith(_ => SymbolQuery.Listings(MarketRowsTests.EurAnswer), cancel, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default));
        var canned = symbol with { Children = [.. symbol.Children!.Select(item => item == field ? field with { Control = ((FlyoutField)field.Control!) with { Finder = finder } } : item)] };
        items[items.IndexOf(symbol)] = canned;
        return (items, canned);
    }

    private static void Pump(Flyout menu, int ms)
    {
        var until = Environment.TickCount64 + ms;
        MSG msg;
        while (Environment.TickCount64 < until)
        {
            while (PeekMessageW(&msg, menu.Hwnd, WM.WM_USER, WM.WM_USER + 0xFF, PM.PM_REMOVE)
                || PeekMessageW(&msg, menu.Hwnd, WM.WM_TIMER, WM.WM_TIMER, PM.PM_REMOVE))
            {
                _ = menu.Message(msg.message, msg.wParam, msg.lParam);
            }
            // A submenu's window has its own procedure, which hands them on itself: its level's search waits on its timer.
            foreach (var (window, _) in menu.OpenPanels.Where(panel => panel.Window != menu.Hwnd))
            {
                while (PeekMessageW(&msg, window, 0, 0, PM.PM_REMOVE))
                {
                    _ = TranslateMessage(&msg);
                    _ = DispatchMessageW(&msg);
                }
            }
            Thread.Sleep(10);
        }
    }

    private static void Click(Flyout menu, uint id)
    {
        var middle = menu.RowFill(id)!.Value.Middle;
        var at = (nint)((middle.y << 16) | (middle.x & 0xFFFF));
        _ = menu.Message(WM.WM_LBUTTONDOWN, 1, at);
        _ = menu.Message(WM.WM_LBUTTONUP, 0, at);
    }

    [UiFact]
    public void TheSearchOpenAndTyped()
    {
        WindowShots.OnHiddenDesktop("markets-search", (gfx, instance) =>
        {
            foreach (var dark in new[] { true, false })
            {
                var tone = dark ? "dark" : "light";
                var backdrop = dark ? ((byte)0, (byte)0, (byte)0) : ((byte)243, (byte)243, (byte)243);
                // Each picture of a menu of its own: one with no pointer over it shuts half a second on.
                MenuSearch Opened(Flyout menu)
                {
                    var (items, symbol) = Menu();
                    menu.Show(gfx, AddListPictures.LookFor(dark), Scale, items, FlyoutLayout.List, Anchor, Work, Edge.Bottom, null);
                    WindowShots.Settle();
                    Click(menu, symbol.Id);
                    WindowShots.Settle();
                    return menu.FieldSearchOf(symbol.Children!.Single(item => item.Control is FlyoutField).Id)!;
                }
                using (var menu = new Flyout(gfx, instance, 0) { Configures = true })
                {
                    var search = Opened(menu);
                    // §411: then well-known ones.
                    Assert.Equal(["CHF/USD", "Apple Inc.", "BTC/USD"], search.Rows.Take(3).Select(row => row.Title));
                    Assert.True(search.Rows[0].Chosen);
                    Assert.True(WindowShots.SaveTogether([.. menu.OpenPanels], $"markets-search-open-{tone}.png", backdrop));
                }
                using (var menu = new Flyout(gfx, instance, 0) { Configures = true })
                {
                    var search = Opened(menu);
                    foreach (var c in "eur")
                    {
                        _ = menu.Message(WM.WM_CHAR, (WPARAM)(nuint)c, 0);
                    }
                    Pump(menu, 480);
                    Assert.Equal(7, search.Rows.Count);
                    WindowShots.Settle(100);
                    Assert.True(WindowShots.SaveTogether([.. menu.OpenPanels], $"markets-search-eur-{tone}.png", backdrop));
                }
            }
        });
    }
}
