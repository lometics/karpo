// §644: a mail widget's Show from by account (docs\widgets.md §644; the person, 2026-11-09: "for the mail widget you
// should also be able to select which accounts and from the account which folders you want to show in the widget
// itself. e.g. inbox from two accounts or similar."), as Karpo draws it, dark and light, to KARPO_PNG_DIR:
//
//  - mailpick-page-<dark|light>.png: the widget's settings page on the display EditPlacePictures.EditedDisplay stands up
//    (1920 × 1300 DIP at 150 %), Show from landed on and so open: Ann Smith's line (her address, her inbox's colour)
//    over her inbox, her archive, Inbox / Projects just picked ("Syncing…") and Sent Items not synced; the work account's
//    line over its inbox and its two folders not synced;
//  - mailpick-menu-<dark|light>.png: the same in the widget's menu, its Show from level.
//
// No account, no server: the parts as SourceParts lists them, by hand. Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Accounts;
using Karpo.Dock;
using Karpo.Tests.Islands;
using Karpo.Tests.Menus;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    private const string MailPickAnn = "ann@outlook.com";

    private const string MailPickWork = "ann@contoso.com";

    /// <summary>Two accounts' mail as Show from lists it: Ann's (named, blue) and the work server's (teal).</summary>
    private static List<SourcePart> MailPickParts()
    {
        SourcePart Ann(string key, string name, bool inbox = false, SourceSync sync = SourceSync.Synced) =>
            new(WidgetSources.Key(MailPickAnn, key), name, "Ann Smith", inbox) { GroupDetail = MailPickAnn, GroupColour = "#0078D4", Sync = sync };
        SourcePart Work(string key, string name, bool inbox = false, SourceSync sync = SourceSync.Synced) =>
            new(WidgetSources.Key(MailPickWork, key), name, MailPickWork, inbox) { GroupColour = "#038387", Sync = sync };
        return
        [
            Ann(SyncKeys.Inbox, S.CoreAccountsInbox, inbox: true),
            Ann("mail:AR", "Archive"),
            Ann("mail:PR", "Inbox / Projects", sync: SourceSync.Reading),
            Ann("mail:SE", "Sent Items", sync: SourceSync.Off),
            Work(SyncKeys.Inbox, S.CoreAccountsInbox, inbox: true),
            Work("mail:Sent", "Sent", sync: SourceSync.Off),
            Work("mail:Receipts", "Receipts", sync: SourceSync.Off),
        ];
    }

    /// <summary>A mail line showing both inboxes, Ann's archive and Inbox / Projects.</summary>
    private static WidgetItem MailPickLine()
    {
        var line = ListKind.Mail.New();
        line.Sources = [.. new[] { "mail", "mail:AR", "mail:PR" }.Select(key => WidgetSources.Key(MailPickAnn, key)), WidgetSources.Key(MailPickWork, SyncKeys.Inbox)];
        return line;
    }

    [UiFact]
    public void MailPickSheets()
    {
        var devices = new OptionDevices { Parts = MailPickParts() };
        WindowShots.OnHiddenDesktop("mail-pick", (gfx, instance) =>
        {
            const uint dpi = 144;
            const float scale = dpi / 96f;
            foreach (var dark in new[] { true, false })
            {
                var mode = dark ? "dark" : "light";
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                var place = new OptionPlace(MenuCorpus.Fresh(), Dark: dark);
                var line = MailPickLine();

                // The widget's page, Show from landed on: open.
                List<OptionPick> picks = [];
                List<FlyoutItem> items = [.. TheDock.WidgetRowsFor(line), .. OptionMenu.Settings(line, picks, devices, place, FolderSide.Face, roomCells: LineHeight.Most)];
                var page = PanelGroups.Page(PanelPage.Widget, IslandEdits.Kind(line), line.Info?.Glyph ?? "", items,
                    header: PanelRows.HeaderOf([(line.Info?.Glyph ?? "", IslandEdits.Kind(line))], PanelRows.PlaceOf(LayoutType.Taskbar, true)));
                var land = PanelRows.All(page.Rows).Single(row => row.Text == S.DockMenuShowFromTitle).Key;
                List<PanelPageView> pages = [.. SettingsPanelTests.Content(dark: dark).Pages, page];
                EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), $"mailpick-page-{mode}.png", dips: new Vector2(1920f, 1300f), panel: deck =>
                {
                    deck.Memory = new PanelMemory();
                    deck.Show(gfx, look, scale, deck.Room!, deck.State ?? PictureState(), new SettingsContent(pages), PanelPage.Widget);
                    deck.Land(PanelPage.Widget, land, light: false);
                    Assert.Contains(land, deck.Memory.Opened);
                });

                // The widget's menu, its Show from level.
                var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
                var anchor = new POINT { x = work.right / 3, y = work.bottom };
                var backdrop = dark ? ((byte)0, (byte)0, (byte)0) : ((byte)0xF3, (byte)0xF3, (byte)0xF3);
                var menuItems = OptionMenu.Section(OptionRows.Of(line, devices, place), []);
                using var menu = new Flyout(gfx, instance, 0) { Configures = true };
                menu.Show(gfx, AddListPictures.LookFor(dark), scale, menuItems, FlyoutLayout.List, anchor, work, Edge.Bottom, null);
                WindowShots.Settle();
                var showFrom = menuItems.Single(each => each.Text == S.DockMenuShowFromTitle).Id;
                for (var step = 0; step < 80 && menu.KeysOn != showFrom; step++)
                {
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
                }
                Assert.Equal(showFrom, menu.KeysOn);
                _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                WindowShots.Settle();
                Assert.Equal((1, S.DockMenuShowFromTitle), (menu.InlineDepth, menu.InlineTitle));
                Assert.True(WindowShots.SaveTogether([.. menu.OpenPanels], $"mailpick-menu-{mode}.png", backdrop));
                menu.Hide();
            }
        });
    }
}
