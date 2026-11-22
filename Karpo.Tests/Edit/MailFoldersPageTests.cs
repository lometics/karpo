// §646 (the person, 2026-11-09: "the email folder selector to select which folders to sync should be under an additional menu so
// that the account is not that long when extended"): an account's Mail folders a page of its own under the Accounts entry
// (Karpo.Core\OptionAccountRows.cs, Karpo.Dock\Dock.SettingsPanel.cs). Held here:
//
//  - **The account's expander** holds one row for its mail, Mail folders — the folder glyph, what is synced at its end, the
//    chevron —, and none of its folders' switches (§639 to §645 every folder's stood there).
//  - **The page** is that link's own level: the inbox and every folder, each a switch with its counts, the inbox its colour; a
//    folder turned on there writes the account's "on" (a file of the test's own), as the page's switch did.
//  - **Under the Accounts entry**: no entry of its own; its way back at its top, "Mail folders · the account"; the way back lands
//    on the link it was opened by, the account's expander open. The keys: Enter on the link opens it with the keys on its way
//    back, then each switch; Backspace goes back to the link. A screen reader: "Back, Mail folders · the account", each switch.

using System.Numerics;
using Karpo.Accounts;
using Karpo.Dock;
using Karpo.Tests.Islands;
using Karpo.Tests.Menus;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed class MailFoldersPageTests
{
    private const float Scale = 1.5f;

    /// <summary>The Microsoft account's Mail folders link (MenuCorpus.Services: "ms", me@outlook.com).</summary>
    private const string Link = OptionAccountRows.Folders + "ms";

    private const string Address = "me@outlook.com";

    /// <summary>The Accounts page as the dock builds it (Dock.PanelOf), its commands into <paramref name="picks"/>.</summary>
    private static PanelPageView AccountsPage(List<OptionPick> picks, KarpoServices? services = null) =>
        PanelRows.Page(PanelPage.Accounts, S.SettingsNavAccounts, PanelRows.GlyphOf(PanelPage.Accounts),
            TheDock.SettingsItems(MenuCorpus.Fresh(), services ?? MenuCorpus.Services(), picks, page: KarpoPage.Accounts));

    /// <summary>The Microsoft account's Mail folders page as the dock builds it (Dock.PanelOf), its commands into <paramref name="picks"/>.</summary>
    private static PanelPageView FoldersPage(List<OptionPick> picks, KarpoServices? services = null)
    {
        services ??= MenuCorpus.Services();
        return PanelRows.Page(PanelPage.MailFolders, OptionAccountRows.FoldersTitle(services.Accounts!, Link)!, PanelRows.GlyphOf(PanelPage.MailFolders),
            TheDock.MailFoldersItems(MenuCorpus.Fresh(), services, picks, Link));
    }

    /// <summary>The panel's pages with the Mail folders page under the Accounts entry.</summary>
    private static SettingsContent Content() => new([.. SettingsPanelTests.Content().Pages, FoldersPage([])]);

    private static PanelLayout Laid(SettingsContent content, PanelPage page, IReadOnlySet<string>? open = null) =>
        new(new SettingsPanelTests.Words(), Scale, new PanelInput(0, content, page, open), PanelLayout.SizeOf(Scale, 1548));

    private static PanelRow Account(PanelPageView page) => page.Rows.Single(row => row.Text == Address);

    /// <summary>
    /// The account's expander: its calendars' switches, then under Mail one row, Mail folders — the folder glyph, what is synced
    /// at its end ("Inbox and 1 more synced"), a link whose chevron points on, its one control —; no inbox's nor folder's switch
    /// anywhere on the page. Its way in is its own page, under the Accounts entry.
    /// </summary>
    [Fact]
    public void TheAccountHoldsOneMailFoldersRowSayingWhatIsSynced()
    {
        var page = AccountsPage([]);
        var account = Account(page);
        var link = account.Rows.Single(row => row.Text == S.SettingsAccountsMailFolders);
        Assert.Equal([S.SettingsAccountsGivesCalendars, "Work", "Home", S.SettingsAccountsGivesMail, S.SettingsAccountsMailFolders], account.Rows.Select(row => row.Text));
        Assert.Equal((PanelRowKind.Link, OptionGlyphs.Folder, S.SettingsAccountsMailFoldersInboxAnd(1)), (link.Kind, link.Glyph, link.Value));
        Assert.Equal(OptionLinks.Opens(OptionLink.MailFolders), link.Item.Opens);
        Assert.Null(link.Second);
        Assert.DoesNotContain(PanelRows.All(page.Rows), row => row.Text is "Inbox" or "Archive" or "Receipts");
        Assert.Equal(new EditLanding(PanelPage.MailFolders), EditEntries.Landing(EditEntries.Of(OptionLink.MailFolders)));

        var layout = Laid(new SettingsContent([page]), PanelPage.Accounts, new HashSet<string> { account.Key });
        var line = layout.LineOf(link.Key)!;
        Assert.Equal([(PanelHitKind.Link, link.Item.Id)], layout.Controls(line).Select(each => (each.Hit.Kind, each.Hit.Id)));
    }

    /// <summary>
    /// The page: "Mail folders · me@outlook.com", the inbox and every folder, each a switch with its counts at its end, the
    /// inbox its colour button; a folder off turned on writes "on" in the account's file, as the Accounts page's switches
    /// write, and nothing of the config.
    /// </summary>
    [Fact]
    public void ThePageListsTheInboxAndEveryFolderAndATurnWritesOn()
    {
        var folder = Path.Combine(Isolation.Folder, "mailfolders-page", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "accounts.json");
        File.WriteAllText(file, """{ "version": 1, "accounts": [ { "id": "ms", "kind": "Microsoft", "address": "me@outlook.com" } ] }""");
        var corpus = MenuCorpus.Services();
        var services = corpus with { Accounts = corpus.Accounts! with { Sync = (id, key, on) => AccountBook.SetSynced(file, id, key, on) } };

        List<OptionPick> picks = [];
        var page = FoldersPage(picks, services);
        Assert.Equal(S.SettingsAccountsSyncPair(first: S.SettingsAccountsMailFolders, second: Address), page.Title);
        Assert.Equal(["Inbox", "Archive", "Receipts"], page.Rows.Select(row => row.Text));
        Assert.Equal([true, true, false], page.Rows.Select(row => row.On));
        Assert.Equal(["1234 messages", "512 messages · 1 unread", "Not synced · 3 unread"], page.Rows.Select(row => row.Value));
        Assert.Equal(Argb.Parse("#8764B8"), PanelSwatch.Of(Assert.Single(page.Rows[0].Tools))!.Color);
        Assert.True(page.Rows[0].Switches);
        Assert.All(page.Rows.Skip(1), row => Assert.Equal(PanelRowKind.Toggle, row.Kind));

        // Receipts turned on, found among the Accounts page's rows as the dock finds a pick's row (Dock.SettingsRow).
        var config = MenuCorpus.Fresh();
        var before = config.ToJson();
        var pick = OptionMenu.PickOf(picks, page.Rows[2].Item.Id)!;
        Assert.Equal(OptionAccountRows.Part + "ms|mail:RE", pick.Key);
        Assert.False(OptionMenu.ApplyRow(OptionRows.Find(KarpoMenu.Of(KarpoPage.Accounts, config, services), pick.Key), pick));
        Assert.Equal(["mail:RE"], AccountBook.Read(file, out _).Accounts.Single().On);
        Assert.Contains("\"on\"", File.ReadAllText(file));
        Assert.Equal(before, config.ToJson());

        // Every key of the account's mail is sent by a control of the page, none by the Accounts page.
        string[] mail = [OptionAccountRows.Part + "ms|inbox", OptionAccountRows.Colour + "ms|inbox", OptionAccountRows.Part + "ms|mail:AR", OptionAccountRows.Part + "ms|mail:RE"];
        Assert.All(mail, key => Assert.Contains(key, AccountsPageTests.KeysSent(page, picks)));
        List<OptionPick> accounts = [];
        var sent = AccountsPageTests.KeysSent(AccountsPage(accounts), accounts);
        Assert.All(mail, key => Assert.DoesNotContain(key, sent));
    }

    /// <summary>
    /// The page under the Accounts entry: no entry of its own, the Accounts entry selected; its way back at its top, its title
    /// after the arrow. The keys: the way back the first stop, then each switch in order. A screen reader: "Back, Mail folders ·
    /// me@outlook.com" at the pane's head, each folder a toggle saying whether it is synced.
    /// </summary>
    [Fact]
    public void ThePageStandsUnderTheAccountsEntryItsWayBackFirst()
    {
        var layout = Laid(Content(), PanelPage.MailFolders);
        Assert.True(layout.IsOpen);
        Assert.DoesNotContain(layout.Header, part => part.Kind == PanelHitKind.Nav && part.Page == PanelPage.MailFolders);
        Assert.True(layout.Opened(new PanelHit(PanelHitKind.Nav, Page: PanelPage.Accounts)));
        Assert.False(layout.BackHead.IsEmpty);
        Assert.Equal(layout.BackHead.Bottom, layout.Viewport.Y);

        var stops = layout.Stops().Where(stop => !PanelLayout.OnBar(stop.Kind)).ToList();
        Assert.Equal(PanelHitKind.Back, stops[0].Kind);
        Assert.Equal(["Inbox", "Archive", "Receipts"], stops.Skip(1).Select(stop => layout.Row(stop.Key)!.Text).Distinct());

        var title = S.SettingsAccountsSyncPair(first: S.SettingsAccountsMailFolders, second: Address);
        var said = DeckUia.Things(layout, bar: false);
        Assert.Equal((S.DockPageBack + ", " + title, UiaRole.Button, UiaPattern.Invoke), (said[0].Name, said[0].Role, said[0].Pattern));
        Assert.Equal([("Inbox", true), ("Archive", true), ("Receipts", false)],
            said.Where(thing => thing.Pattern == UiaPattern.Toggle).Select(thing => (thing.Name, thing.On == true)));
        Assert.Equal(title, DeckUia.Tree(layout, new RECT { right = 900, bottom = 900 }, PanelHit.None, bar: false).Root.Name);
    }

    /// <summary>
    /// The way back lands on the link it was opened by, back to the page whose entry it stands under, and only there; the
    /// link's row, found by its pick as the dock finds it (Dock.OpenPanel), stands in the account's expander, which its
    /// landing opens.
    /// </summary>
    [Fact]
    public void BackLandsOnTheLinkInItsAccountsExpander()
    {
        Assert.Equal(Link, PanelRows.BackTo(PanelPage.MailFolders, PanelPage.Accounts, Link));
        Assert.Null(PanelRows.BackTo(PanelPage.MailFolders, PanelPage.Layouts, Link));
        Assert.Null(PanelRows.BackTo(PanelPage.Accounts, PanelPage.Accounts, Link));
        Assert.Null(PanelRows.BackTo(null, PanelPage.Accounts, Link));

        List<OptionPick> picks = [];
        var page = AccountsPage(picks);
        var holder = picks.Single(pick => pick.Kind == OptionPickKind.Link && pick.Key == Link);
        var row = PanelRows.WithId(page.Rows, holder.Id)!;
        Assert.Equal(S.SettingsAccountsMailFolders, row.Text);
        Assert.Equal([Account(page).Key], PanelRows.PathTo(page.Rows, row.Key));
    }

    /// <summary>
    /// The real edit bar, by the keys: on the account's Mail folders, Enter asks for its link; the page shown, the keys are on
    /// its way back, Tab on to its first switch; Backspace asks for the Accounts page, and landed (Dock.OpenPanel's way back)
    /// the account's expander stands open, the keys on Mail folders, in sight.
    /// </summary>
    [UiFact]
    public void TheKeysGoInAndBackToTheLinkTheExpanderStillOpen()
    {
        WindowShots.OnHiddenDesktop("mail-folders-keys", (gfx, instance) =>
        {
            var look = AddListPictures.LookFor(dark: true) with { Animations = false };
            var room = new EditDeckRoom(new RectI(0, 0, 2880, 1620), 0, 72, Work: new RectI(0, 0, 2880, 1548));
            var content = Content();
            var accounts = content.Of(PanelPage.Accounts)!;
            var account = Account(accounts);
            var link = PanelRows.All(accounts.Rows).Single(row => row.Text == S.SettingsAccountsMailFolders);
            using var deck = new EditDeck(gfx, instance, 0);
            EditDeckEvent Key(int key) => deck.Message(deck.Hwnd, WM.WM_KEYDOWN, (WPARAM)(nuint)key, 0).Happened;

            deck.Show(gfx, look, Scale, room, EditDeckState.Plain, content, PanelPage.Accounts);
            deck.Land(PanelPage.Accounts, link.Key, light: false);
            for (var step = 0; step < 60 && deck.Focused.Key != link.Key; step++)
            {
                _ = Key(VK.VK_TAB);
            }
            Assert.Equal((PanelHitKind.Link, link.Key), (deck.Focused.Kind, deck.Focused.Key));
            Assert.Equal(new EditDeckEvent(EditDeckAction.Link, link.Item.Id, PanelPage.Accounts, link.Key), Key(VK.VK_RETURN));

            // The dock's answer (Dock.PanelLinked): the page, under the Accounts entry, the keys on its way back.
            deck.Show(gfx, look, Scale, room, EditDeckState.Plain, content, PanelPage.MailFolders);
            Assert.Equal(new PanelHit(PanelHitKind.Back), deck.Focused);
            _ = Key(VK.VK_TAB);
            Assert.Equal("Inbox", deck.Layout!.Row(deck.Focused.Key)!.Text);

            Assert.Equal(new EditDeckEvent(EditDeckAction.Open, Page: PanelPage.Accounts), Key(VK.VK_BACK));
            // The dock's answer (Dock.OpenPanel, PanelRows.BackTo): the Accounts page landed on the link, not lit.
            deck.Show(gfx, look, Scale, room, EditDeckState.Plain, content, PanelPage.Accounts);
            deck.Land(PanelPage.Accounts, PanelRows.BackTo(PanelPage.MailFolders, PanelPage.Accounts, link.Key), light: false);
            Assert.True(deck.Memory.IsOpen(account.Key));
            Assert.Equal((PanelHitKind.Link, link.Key), (deck.Focused.Kind, deck.Focused.Key));
            var shown = deck.Layout!.OnPanel(deck.Layout.LineOf(link.Key)!.Rect);
            Assert.True(shown.Y >= deck.Layout.Viewport.Y && shown.Bottom <= deck.Layout.Viewport.Bottom, "Mail folders is out of sight");
        });
    }
}
