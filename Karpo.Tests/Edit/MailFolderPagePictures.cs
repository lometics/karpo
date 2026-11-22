// §646 (the person, 2026-11-09: "the email folder selector to select which folders to sync should be under an additional menu so
// that the account is not that long when extended"): an account's Mail folders in the Accounts entry's flyout, as the dock shows
// it (EditDeck), on a display of its own at 150 %, dark and light — the Accounts page, the account's expander open, its mail one
// row, "Mail folders", saying what is synced, a chevron at its end; and the page it opens, its way back at its top, "Mail
// folders · me@outlook.com", the inbox and every folder a switch. The corpus's accounts (MenuCorpus.Services), no server. To
// KARPO_PNG_DIR as mailfolderpage-<account|page>-<dark|light>.png.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using Karpo.Tests.Menus;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void MailFolderPageSheets()
    {
        WindowShots.OnHiddenDesktop("mail-folder-page-sheets", (gfx, instance) =>
        {
            const uint dpi = 144;
            const float scale = dpi / 96f;
            const string link = OptionAccountRows.Folders + "ms";
            foreach (var dark in new[] { true, false })
            {
                var tone = dark ? "dark" : "light";
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                var services = MenuCorpus.Services();
                var folders = PanelRows.Page(PanelPage.MailFolders, OptionAccountRows.FoldersTitle(services.Accounts!, link)!, PanelRows.GlyphOf(PanelPage.MailFolders),
                    TheDock.MailFoldersItems(MenuCorpus.Fresh(), services, [], link));
                var content = new SettingsContent([.. SettingsPanelTests.Content(dark: dark).Pages, folders]);
                var accounts = content.Of(PanelPage.Accounts)!;
                var account = accounts.Rows.Single(row => row.Text == "me@outlook.com");
                var row = PanelRows.All(accounts.Rows).Single(each => each.Text == S.SettingsAccountsMailFolders);
                foreach (var (scene, page) in new[] { ("account", PanelPage.Accounts), ("page", PanelPage.MailFolders) })
                {
                    EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), $"mailfolderpage-{scene}-{tone}.png", dips: new Vector2(1920f, 1080f),
                        panel: deck =>
                        {
                            deck.Memory = new PanelMemory();
                            deck.Memory.Show(page);
                            // The account's expander open, as the way back leaves it.
                            deck.Memory.Open([account.Key]);
                            deck.Show(gfx, look, scale, deck.Room!, deck.State ?? PictureState(), content);
                            deck.Land(page, null, light: false);
                            var layout = deck.Layout!;
                            Assert.True(layout.IsOpen);
                            // The Accounts entry stands selected whichever of its pages shows.
                            Assert.True(layout.Opened(new PanelHit(PanelHitKind.Nav, Page: PanelPage.Accounts)));
                            if (page == PanelPage.MailFolders)
                            {
                                Assert.False(layout.BackHead.IsEmpty);
                                Assert.Equal(["Inbox", "Archive", "Receipts"], layout.Blocks.SelectMany(block => block.Lines).Select(line => line.Row.Text));
                            }
                            else
                            {
                                Assert.True(layout.BackHead.IsEmpty);
                                Assert.True(deck.Memory.IsOpen(account.Key));
                                Assert.NotNull(layout.LineOf(row.Key));
                            }
                        });
                }
            }
        });
    }
}
