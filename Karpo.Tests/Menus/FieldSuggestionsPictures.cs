// menus: §304's pictures in KARPO_PNG_DIR, at 150 %, dark (config-suggest-dark): a feed's level before
// anything is typed (the person's other feed, then the well-known ones, its own ticked), with words typed,
// with an address typed; and a clock's label's level (the other clocks' labels, then its zone's places).
// §332 (config-suggest-amount-dark): an amount owned before anything is typed and with "2" typed, and a
// converter's amount.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed unsafe class FieldSuggestionsPictures
{
    private const float Scale = 1.5f;

    private static readonly RECT Work = new() { right = (int)(1920 * Scale), bottom = (int)(1040 * Scale) };

    private static POINT At(int tenth) => new() { x = Work.right * tenth / 10, y = Work.bottom };

    private static Flyout Show(Gfx gfx, HINSTANCE instance, IReadOnlyList<FlyoutItem> items, POINT anchor)
    {
        var menu = new Flyout(gfx, instance, 0) { Configures = true };
        menu.Show(gfx, AddListPictures.LookFor(true), Scale, items, FlyoutLayout.List, anchor, Work, Edge.Bottom, null);
        WindowShots.Settle();
        return menu;
    }

    /// <summary>The menu's own posted messages handed to it until <paramref name="done"/>, as the dock's window procedure hands them.</summary>
    private static void PumpUntil(Flyout menu, Func<bool> done, int most = 4000)
    {
        var until = Environment.TickCount64 + most;
        MSG msg;
        while (!done() && Environment.TickCount64 < until)
        {
            while (PeekMessageW(&msg, menu.Hwnd, WM.WM_USER, WM.WM_USER + 0xFF, PM.PM_REMOVE) || PeekMessageW(&msg, menu.Hwnd, WM.WM_TIMER, WM.WM_TIMER, PM.PM_REMOVE))
            {
                _ = menu.Message(msg.message, msg.wParam, msg.lParam);
            }
            Thread.Sleep(10);
        }
    }

    [UiFact]
    public void SuggestionPictures()
    {
        WindowShots.OnHiddenDesktop("suggest-pictures", (gfx, instance) =>
        {
            var news = ListKind.Rss.New();
            news.Rss = new RssOptions { Address = "https://www.srf.ch/news/bnf/rss/1646", Title = "SRF News" };
            var blog = ListKind.Rss.New();
            blog.Rss = new RssOptions { Address = "https://blog.example.com/feed.xml", Title = "Example Blog" };
            var clock = ListKind.Clock.New();
            clock.Clock!.TimeZone = "Tokyo Standard Time";
            clock.Clock.Label = "Tokyo";
            var home = ListKind.Clock.New();
            home.Clock!.Label = "Home";
            var folder = BarKind.FolderList.New();
            foreach (var line in (CellItem[])[news, blog, clock, home])
            {
                CellItems.Put(folder.Folder!, line);
            }
            var devices = new OptionDevices { Words = WordsInUse.Of(new Config { Layouts = [new TaskbarLayout { Islands = [folder] }] }) };

            List<FlyoutItem> Menu(WidgetItem item) => [.. TheDock.WidgetRowsFor(item), .. OptionMenu.Section(OptionRows.Of(item, devices), [])];
            Flyout Level(WidgetItem item, string header, string typed, int tenth)
            {
                var items = Menu(item);
                var row = items.Single(each => each.Text == header);
                var field = row.Children!.Single(each => each.Control is FlyoutField);
                var menu = Show(gfx, instance, items, At(tenth));
                _ = menu.Click(row.Id);
                PumpUntil(menu, () => menu.FieldSearchOf(field.Id)?.Rows.Count > 0);
                foreach (var c in typed)
                {
                    _ = menu.Message(WM.WM_CHAR, (WPARAM)(nuint)c, 0);
                }
                PumpUntil(menu, () => menu.FieldSearchOf(field.Id)?.Rows.Count > 0);
                return menu;
            }
            using var feeds = Level(news, S.SettingsRssAddressHeader, "", 1);
            using var words = Level(news, S.SettingsRssAddressHeader, "heise", 3);
            using var address = Level(news, S.SettingsRssAddressHeader, "example.com/f", 5);
            using var label = Level(clock, S.SettingsWidgetCountdownNameHeader, "", 8);
            WindowShots.Settle(300);
            Assert.True(WindowShots.SaveTogether([.. new[] { feeds, words, address, label }.SelectMany(menu => menu.OpenPanels)], "config-suggest-dark.png", (0, 0, 0)));

            // §332: an amount owned, before anything is typed and with "2" typed; a converter's amount.
            MarketOptions Owning(string symbol, double holding) => new() { Symbol = symbol, Holding = holding, Detail = "Apple Inc. · NASDAQ · USD" };
            var apple = ListKind.Markets.New();
            apple.Market = Owning("AAPL", 12.5);
            var msft = ListKind.Markets.New();
            msft.Market = Owning("MSFT", 3);
            var converter = ListKind.Markets.New();
            converter.Market = new MarketOptions { Symbol = "EURCHF=X", Style = MarketStyle.Converter, Amount = 250 };
            var wallet = BarKind.FolderList.New();
            foreach (var line in (CellItem[])[apple, msft, converter])
            {
                CellItems.Put(wallet.Folder!, line);
            }
            devices = new OptionDevices { Words = WordsInUse.Of(new Config { Layouts = [new TaskbarLayout { Islands = [wallet] }] }) };
            using var owned = Level(apple, S.SettingsWidgetMarketHoldingHeader, "", 1);
            using var two = Level(apple, S.SettingsWidgetMarketHoldingHeader, "2", 4);
            using var amount = Level(converter, S.SettingsWidgetMarketAmountHeader, "", 7);
            WindowShots.Settle(300);
            Assert.True(WindowShots.SaveTogether([.. new[] { owned, two, amount }.SelectMany(menu => menu.OpenPanels)], "config-suggest-amount-dark.png", (0, 0, 0)));
        });
    }
}
