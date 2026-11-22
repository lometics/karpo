// pins: §425's pictures in KARPO_PNG_DIR, at 150 %, dark: a pin to Documents, its menu and Target's level of
// kinds (pinpick-target-dark), each kind's level as this PC lists it — App, Folder, File, Website, Settings page —
// (pinpick-kinds-dark), and a website being typed, then kept (pinpick-website-dark). Needs Direct2D and a window
// station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Folders;

[Collection(InLanguage.Collection)]
public sealed unsafe class PinTargetPictures
{
    private const float Scale = 1.5f;

    private static readonly RECT Work = new() { right = (int)(2560 * Scale), bottom = (int)(1040 * Scale) };

    private static POINT At(int twelfth) => new() { x = Work.right * twelfth / 12, y = Work.bottom };

    private static Flyout Show(Gfx gfx, HINSTANCE instance, IReadOnlyList<FlyoutItem> items, POINT anchor)
    {
        var menu = new Flyout(gfx, instance, 0) { Configures = true, Configuration = true, TypeTitle = S.CoreWidgetPinTitle };
        menu.Show(gfx, AddListPictures.LookFor(true), Scale, items, FlyoutLayout.List, anchor, Work, Edge.Bottom, null);
        WindowShots.Settle();
        return menu;
    }

    private static bool Save(IEnumerable<Flyout> menus, string name)
    {
        WindowShots.Settle(300);
        return WindowShots.SaveTogether([.. menus.SelectMany(menu => menu.OpenPanels)], $"{name}-dark.png", (0, 0, 0));
    }

    /// <summary>The menu's own posted messages handed to it until <paramref name="done"/>.</summary>
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
    public void TheTargetPictures()
    {
        WindowShots.OnHiddenDesktop("pin-target-pictures", (gfx, instance) =>
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var pin = BarKind.Pin.New();
            pin.Pin!.Target = documents;
            var config = new Config();
            var choices = TargetLooks.Choices(config) with { Sites = [TargetChoices.Site("github.com", "https://github.com")] };
            var devices = new OptionDevices { Targets = choices };
            var place = new OptionPlace(config, Dark: true);
            var pictures = Flyout.Pictures;
            Flyout.Pictures = (address, _, came) => TargetPictures.Shared.Get(address, came);
            // Every picture in before the menus are shown, so the sheets have them.
            foreach (var row in choices.Places.Concat(choices.Folders).Concat(choices.Files.Take(10)).Concat(choices.Apps.Take(12)).Append(TargetChoices.Row("", documents)))
            {
                SpinWait.SpinUntil(() => TargetPictures.Shared.Get(row.Picture, () => { }) is not null, 3000);
            }
            List<FlyoutItem> Menu(WidgetItem item) =>
                [.. Karpo.Dock.Dock.WidgetRowsFor(item), .. OptionMenu.Settings(item, [], devices, place, FolderSide.Face, roomCells: null, pictures: address => TargetPictures.Shared.Get(address, () => { }) is { } pixels ? new FlyoutIcon.Picture(pixels) : null)];
            try
            {
                var items = Menu(pin);
                var target = items.Single(item => item.Text == S.SettingsPinTargetHeader);
                using (var root = Show(gfx, instance, items, At(1)))
                using (var kinds = Show(gfx, instance, items, At(4)))
                {
                    _ = kinds.Click(target.Id);
                    Assert.True(Save([root, kinds], "pinpick-target"));
                }

                // Each kind's level, before anything is typed.
                List<Flyout> levels = [];
                try
                {
                    var at = 1;
                    foreach (var kind in target.Children!)
                    {
                        var menu = Show(gfx, instance, items, At(at));
                        levels.Add(menu);
                        _ = menu.Click(target.Id);
                        _ = menu.Click(kind.Id);
                        if (kind.Children!.FirstOrDefault(item => item.Control is FlyoutField { Finds: true }) is { } field)
                        {
                            PumpUntil(menu, () => menu.FieldSearchOf(field.Id)?.Rows.Count > 0);
                        }
                        at += 2;
                    }
                    Assert.True(Save(levels, "pinpick-kinds"));
                }
                finally
                {
                    levels.ForEach(menu => menu.Dispose());
                }

                // A website typed: its address offered with its host; words that are none said so; then kept, ticked.
                var site = target.Children!.Single(item => item.Text == S.SettingsPinKindWebsite);
                var address = site.Children!.Single(item => item.Control is FlyoutField);
                using var typing = Show(gfx, instance, items, At(1));
                _ = typing.Click(target.Id);
                _ = typing.Click(site.Id);
                foreach (var c in "example.com/news")
                {
                    _ = typing.Message(WM.WM_CHAR, (WPARAM)(nuint)c, 0);
                }
                PumpUntil(typing, () => typing.FieldSearchOf(address.Id)?.Rows.Count > 0);
                using var wrong = Show(gfx, instance, items, At(4));
                _ = wrong.Click(target.Id);
                _ = wrong.Click(site.Id);
                foreach (var c in "not a site")
                {
                    _ = wrong.Message(WM.WM_CHAR, (WPARAM)(nuint)c, 0);
                }
                PumpUntil(wrong, () => wrong.FieldSearchOf(address.Id)?.Rows.Count > 0, 1000);
                var web = BarKind.Pin.New();
                web.Pin!.Target = "https://example.com/news";
                var kept = Menu(web);
                using var root2 = Show(gfx, instance, kept, At(7));
                using var after = Show(gfx, instance, kept, At(10));
                _ = after.Click(kept.Single(item => item.Text == S.SettingsPinTargetHeader).Id);
                Assert.True(Save([typing, wrong, root2, after], "pinpick-website"));
            }
            finally
            {
                Flyout.Pictures = pictures;
            }
        });
    }
}
