// menus: §292's pictures in KARPO_PNG_DIR, at 150 %, dark: several widgets' edit menus side by side in the
// one order (config-widgets-dark: the weather, a clock, a markets line, a graphics graph, a calendar;
// config-faces-dark: the weather, the earthquakes and the meeting face's levels), and a pin to a folder:
// its menu, its "What it opens" level before anything is typed and with a path typed, and a pin to a web
// address (config-pin-dark).

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed unsafe class MenuConventionPictures
{
    private const float Scale = 1.5f;

    private static readonly RECT Work = new() { right = (int)(1920 * Scale), bottom = (int)(1040 * Scale) };

    private static readonly OptionDevices Sources = new(["GPU 0: Intel UHD Graphics 770", "GPU 1: NVIDIA GeForce RTX 4060 Ti"], ["Disk 0 (C:)"]);

    private static POINT At(int tenth) => new() { x = Work.right * tenth / 10, y = Work.bottom };

    private static List<FlyoutItem> Menu(WidgetItem item, List<OptionPick> picks, OptionDevices? devices = null, OptionPlace? place = null) =>
        [.. TheDock.WidgetRowsFor(item), .. OptionMenu.Section(OptionRows.Of(item, devices ?? Sources, place), picks)];

    private static Flyout Show(Gfx gfx, HINSTANCE instance, IReadOnlyList<FlyoutItem> items, POINT anchor)
    {
        var menu = new Flyout(gfx, instance, 0) { Configures = true };
        menu.Show(gfx, AddListPictures.LookFor(true), Scale, items, FlyoutLayout.List, anchor, Work, Edge.Bottom, null);
        WindowShots.Settle();
        return menu;
    }

    private static bool Save(IEnumerable<Flyout> menus, string name)
    {
        WindowShots.Settle(300);
        return WindowShots.SaveTogether([.. menus.SelectMany(menu => menu.OpenPanels)], $"{name}-dark.png", (0, 0, 0));
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
    public void ConventionPictures()
    {
        WindowShots.OnHiddenDesktop("config-pictures", (gfx, instance) =>
        {
            var weather = ListKind.WeatherNow.New();
            weather.Weather = new WeatherOptions { Place = "Gais, Appenzell Ausserrhoden, Schweiz", Latitude = 47.36, Longitude = 9.45 };
            var clock = ListKind.Clock.New();
            clock.Clock!.TimeZone = "Tokyo Standard Time";
            var markets = ListKind.Markets.New();
            markets.Market = new MarketOptions { Symbol = "MSFT", Detail = "Microsoft Corporation · NASDAQ · USD" };
            var gpu = ListKind.Gpu.New();
            var calendar = ListKind.Calendar.New();
            using (var one = Show(gfx, instance, Menu(weather, []), At(1)))
            using (var two = Show(gfx, instance, Menu(clock, []), At(3)))
            using (var three = Show(gfx, instance, Menu(markets, []), At(5)))
            using (var four = Show(gfx, instance, Menu(gpu, []), At(7)))
            using (var five = Show(gfx, instance, Menu(calendar, [], place: new OptionPlace(new Config(), Dark: true)), At(9)))
            {
                Assert.True(Save([one, two, three, four, five], "config-widgets"));
            }

            // A folder's faces: each face's level, in the one order.
            Flyout Face(string id, int tenth)
            {
                var folder = BarKind.FolderList.New();
                folder.Folder!.Faces = [FolderFace.Of(id)];
                List<OptionPick> picks = [];
                List<FlyoutItem> items = [.. TheDock.WidgetRowsFor(folder), .. OptionMenu.FaceSection(folder.Folder, picks, Sources)];
                var menu = Show(gfx, instance, items, At(tenth));
                _ = menu.Click(items.Single(item => item.Reorder == OptionMenu.FaceGroup).Id);
                return menu;
            }
            using (var face1 = Face(FolderFaces.WeatherId, 1))
            using (var face2 = Face(FolderFaces.QuakesId, 4))
            using (var face3 = Face(FolderFaces.MeetingId, 7))
            {
                Assert.True(Save([face1, face2, face3], "config-faces"));
            }

            // A pin to a folder that is there: its menu; what it opens before anything is typed — this PC's
            // folders and apps, by their names and with their pictures (§380) —; a path typed.
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var pin = BarKind.Pin.New();
            pin.Pin!.Target = documents;
            var choices = TargetLooks.Choices(new Config());
            var devices = Sources with { Targets = choices };
            var pictures = Flyout.Pictures;
            Flyout.Pictures = (address, _, came) => TargetPictures.Shared.Get(address, came);
            // Every picture in before the menus are shown, so the sheet has them.
            foreach (var row in choices.Folders.Concat(choices.Apps.Take(12)))
            {
                SpinWait.SpinUntil(() => TargetPictures.Shared.Get(row.Picture, () => { }) is not null, 3000);
            }
            List<OptionPick> pinPicks = [];
            var pinned = Menu(pin, pinPicks, devices, new OptionPlace(new Config(), Dark: true));
            // §425: its Folder kind's level.
            var target = pinned.Single(item => item.Text == S.SettingsPinTargetHeader);
            var folder = target.Children!.Single(item => item.Text == S.SettingsPinKindFolder);
            var field = folder.Children!.Single(item => item.Control is FlyoutField);
            var web = BarKind.Pin.New();
            web.Pin!.Target = "https://example.com/news";
            using var root = Show(gfx, instance, pinned, At(1));
            using var level = Show(gfx, instance, pinned, At(3));
            _ = level.Click(target.Id);
            _ = level.Click(folder.Id);
            PumpUntil(level, () => level.FieldSearchOf(field.Id)?.Rows.Count > 0);
            using var typed = Show(gfx, instance, pinned, At(5));
            _ = typed.Click(target.Id);
            _ = typed.Click(folder.Id);
            foreach (var c in @"C:\")
            {
                _ = typed.Message(WM.WM_CHAR, (WPARAM)(nuint)c, 0);
            }
            PumpUntil(typed, () => typed.FieldSearchOf(field.Id)?.Rows.Count > 1);
            using var link = Show(gfx, instance, Menu(web, [], devices), At(8));
            Assert.True(Save([root, level, typed, link], "config-pin"));
            Flyout.Pictures = pictures;
        });
    }
}
