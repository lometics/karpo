// §639: mail folders synced per account, through to the mail widget (KARPO_UI_TESTS=1), dark and light, 150 %, in
// KARPO_PNG_DIR: an account's Mail folders (§646: a page of its own, its link's level, the Accounts page's), its inbox
// and every other folder a switch, the archive turned on (mailfolders-account-<dark|light>.png); a mail line's "Show from", offering the inbox and the folders synced, the
// archive picked (mailfolders-showfrom-<dark|light>.png); and the line of the inbox and the archive, merged by date,
// each row naming its folder (mailfolders-line-<dark|light>.png, every height). No server: fake accounts (FakeReader).

using Karpo.Accounts;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Pim;
using Karpo.Tests.Edit;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Pim;

public sealed class MailFolderPictures
{
    private const float Scale = 1.5f;

    private const string Ann = "ann@outlook.com";

    private static readonly DateTime Frozen = new(2026, 11, 9, 9, 0, 0);

    /// <summary>Ann's mailbox: the inbox, the archive turned on, and three folders not synced.</summary>
    private static FakeReader Mailbox()
    {
        MailItem Message(string from, string subject, int minutesAgo, bool unread) =>
            new(from + subject, from, null, subject, null, new DateTimeOffset(Frozen.AddMinutes(-minutesAgo)), unread);
        var reader = new FakeReader(AccountKind.Microsoft, Ann)
        {
            Inbox = new Inbox(2,
            [
                Message("Sandra Gerber", "Lunch on Friday?", 4, true),
                Message("GitHub", "[karpo-app] CI passed on master", 95, true),
                Message("Swiss Post", "Your parcel is on its way", 300, false),
            ], 1234),
        };
        reader.Folders.Add((new MailFolder("AR", "Archive", Unread: 1, Total: 512), new Inbox(1,
        [
            Message("Adrian Hager", "Re: Quarterly review", 40, true),
            Message("Lena Brunner", "Invoice October", 180, false),
        ], 512)));
        reader.Folders.Add((new MailFolder("PR", "Inbox / Projects", Unread: 0, Total: 38), new Inbox(0, [], 38)));
        reader.Folders.Add((new MailFolder("SE", "Sent Items", Unread: 0, Total: 2210), new Inbox(0, [], 2210)));
        reader.Folders.Add((new MailFolder("RE", "Receipts", Unread: 3, Total: 64), new Inbox(3, [], 64)));
        return reader.Turned(SyncKeys.MailFolder("AR"), true);
    }

    /// <summary>The mail store over <see cref="Mailbox"/> after a read, and what it told the Accounts page.</summary>
    private static (MailStore Store, Account Account, SyncReportFile Report) Read()
    {
        var reader = Mailbox();
        var report = new SyncReport(Path.Combine(Isolation.Folder, "mailfolders-" + Guid.NewGuid().ToString("N")));
        var store = new MailStore(new AccountSet([reader]), report: report, now: () => Frozen.ToUniversalTime());
        store.Refresh();
        store.Idle().GetAwaiter().GetResult();
        return (store, reader.Account, report.Read());
    }

    private static void KeysTo(Flyout menu, uint id)
    {
        for (var step = 0; step < 80 && menu.KeysOn != id; step++)
        {
            _ = menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
        }
        Assert.Equal(id, menu.KeysOn);
    }

    [UiFact]
    public void TheAccountsFoldersTheShowFromAndTheLine()
    {
        var (store, account, report) = Read();
        using var held = store;
        var line = ListKind.Mail.New();
        line.Id = "mail";
        line.Sources = [WidgetSources.Key(Ann, SyncKeys.Inbox), WidgetSources.Key(Ann, SyncKeys.MailFolder("AR"))];

        WindowShots.OnHiddenDesktop("mail-folders", (gfx, instance) =>
        {
            var work = new RECT { right = (int)(1920 * Scale), bottom = (int)(1040 * Scale) };
            var anchor = new POINT { x = work.right / 3, y = work.bottom };
            foreach (var dark in new[] { true, false })
            {
                var mode = dark ? "dark" : "light";
                var backdrop = dark ? ((byte)0, (byte)0, (byte)0) : ((byte)0xF3, (byte)0xF3, (byte)0xF3);

                // The account's Mail folders (§646: its link's own level): the inbox, then each folder, the archive on.
                var forms = new AccountForms((_, _) => Task.FromResult(new AccountTry("")), each => each(), () => { });
                var accounts = new OptionAccounts([AccountSyncRows.Menu(account, report.For(account.Id), Frozen) with { Name = "Ann Smith" }], _ => { }, _ => { }, _ => { }, forms);
                var parts = OptionAccountRows.Page(accounts).Single(row => row.Kind == OptionRowKind.Things).Owned!.Things.Single().Rows;
                var folders = parts.Single(row => row.Kind == OptionRowKind.Link && row.Link == OptionLink.MailFolders);
                Assert.Equal("Inbox and 1 more synced", folders.Summary);
                var level = OptionMenu.Rows([.. folders.Level], []);
                Assert.Contains(level, item => item.Text == "Archive");
                using (var menu = new Flyout(gfx, instance, 0) { Configures = true })
                {
                    menu.Show(gfx, AddListPictures.LookFor(dark), Scale, level, FlyoutLayout.List, anchor, work, Edge.Bottom, null);
                    WindowShots.Settle();
                    Assert.True(WindowShots.SaveTogether([.. menu.OpenPanels], $"mailfolders-account-{mode}.png", backdrop));
                    menu.Hide();
                }

                // The line's "Show from": the inbox and every folder, §644 those not synced saying so; the archive picked.
                var devices = new OptionDevices { Parts = SourceParts.Of([account], report, AccountShows.Mail) };
                Assert.Equal([("Inbox", SourceSync.Synced), ("Archive", SourceSync.Synced), ("Inbox / Projects", SourceSync.Off), ("Sent Items", SourceSync.Off), ("Receipts", SourceSync.Off)], devices.Parts!.Select(part => (part.Name, part.Sync)));
                var place = new OptionPlace(new Config(), Dark: dark);
                var items = OptionMenu.Section(OptionRows.Of(line, devices, place), []);
                using (var menu = new Flyout(gfx, instance, 0) { Configures = true })
                {
                    menu.Show(gfx, AddListPictures.LookFor(dark), Scale, items, FlyoutLayout.List, anchor, work, Edge.Bottom, null);
                    WindowShots.Settle();
                    KeysTo(menu, items.Single(each => each.Text == S.DockMenuShowFromTitle).Id);
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                    WindowShots.Settle();
                    Assert.Equal((1, S.DockMenuShowFromTitle), (menu.InlineDepth, menu.InlineTitle));
                    Assert.True(WindowShots.SaveTogether([.. menu.OpenPanels], $"mailfolders-showfrom-{mode}.png", backdrop));
                    menu.Hide();
                }
            }
        });

        // The line of the inbox and the archive: their newest merged by date, each row naming its folder.
        Offscreen.OnStaThread(gfx =>
        {
            using var island = new MailIsland(Config.IslandKey("layout:default", "mail"), line, gfx, new Config(), store, () => Frozen);
            Assert.Equal(["Inbox", "Archive", "Inbox", "Archive", "Inbox"], island.Shown.Messages.Select(message => message.Folder));
            foreach (var dark in new[] { true, false })
            {
                ListLinesTests.Sheet(gfx, island, ListKind.Mail, Scale, ListFormPictures.LookFor(dark), Path.Combine(Offscreen.Pictures, $"mailfolders-line-{(dark ? "dark" : "light")}.png"));
            }
        });
    }
}
