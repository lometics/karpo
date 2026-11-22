// accounts: ⚙ Settings › Accounts and every widget's Accounts level, the rest of the Accounts page
// (Karpo.Core\AccountForm.cs, OptionAccountRows.cs; docs\widgets.md §338). Held here:
//
//  - **The forms.** A mail server's and a CalDAV server's: what each needs before Test connection and Add
//    may be used, and what they say greyed; a try through the caller's connect (no network: a test's),
//    greyed meanwhile and how it went under the row once back; an account added empties the form and
//    wipes its password; the menu closing does too, unless a try is on its way.
//  - **The password** is a masked row: no text of its own, bullets at its end while it holds one, never in
//    the config; on a real menu its field draws bullets, a screen reader is told it is a password and
//    never hears its words.
//  - **An account's submenu**: its kinds' headings, a switch per part with its count, why a kind did not
//    sync, Reconnect, its colours; a switch and a colour go to the caller as the Accounts page wrote them.
//  - **Pictures** in KARPO_PNG_DIR, dark, 150 %: ⚙ Settings' Accounts level (settings-accounts-dark-150.png),
//    an account's (settings-account-dark-150.png), and the mail server's form, the server set by hand, a
//    password typed (settings-imap-form-dark-150.png).

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

public sealed class AccountFormTests
{
    private const float Scale = 1.5f;

    /// <summary>The forms, their tries answered by <paramref name="answer"/> at once, and what was asked.</summary>
    private static (AccountForms Forms, List<string> Tried) Forms(Func<AccountForm, bool, AccountTry>? answer = null)
    {
        List<string> tried = [];
        AccountForms? forms = null;
        forms = new AccountForms(
            (form, add) =>
            {
                tried.Add($"{(add ? "add" : "test")} {form.Kind} {form.Address} {form.Password()} {form.Server}:{form.PortNumber}");
                return Task.FromResult(answer?.Invoke(form, add) ?? new AccountTry("imap.example.com"));
            },
            work => work(),
            () => tried.Add("changed"));
        return (forms, tried);
    }

    private static OptionRow Row(IReadOnlyList<OptionRow> rows, string field) => rows.Single(row => row.Key.EndsWith("." + field, StringComparison.Ordinal));

    /// <summary>
    /// A mail server's form needs an address and its password before it can be tried, and a port, where
    /// one is typed, from 1 to 65535; a CalDAV server's an address or a server, the password, and a user
    /// name where there is no address. Test and Add stand greyed saying what is missing.
    /// </summary>
    [Fact]
    public void AFormCanBeTriedOnceItHasWhatItNeeds()
    {
        var (forms, _) = Forms();
        var mail = forms.Mail;
        Assert.Equal(S.SettingsAccountsImapMissing, mail.Problem);
        mail.SetAddress(" anna@example.com ");
        Assert.Equal("anna@example.com", mail.Address);
        Assert.Equal(S.SettingsAccountsImapMissing, mail.Problem);
        mail.SetPassword("secret");
        Assert.Null(mail.Problem);
        mail.SetAddress("anna@");
        Assert.Equal(S.SettingsAccountsImapMissing, mail.Problem);
        mail.SetAddress("anna@example.com");
        mail.SetOwnServer(true);
        mail.SetPort("99999");
        Assert.Equal(S.DockAccountsPortBad, mail.Problem);
        mail.SetPort("x1");
        Assert.Equal(S.DockAccountsPortBad, mail.Problem);
        mail.SetPort("");
        Assert.Equal((null, (int?)null, 993), (mail.Problem, mail.PortNumber, mail.UsualPort));
        mail.SetSecurity(AccountSecurity.StartTls);
        mail.SetPort("1143");
        Assert.Equal((null, (int?)1143, 143), (mail.Problem, mail.PortNumber, mail.UsualPort));
        // A port typed while the server is found from the address counts for nothing.
        mail.SetOwnServer(false);
        mail.SetPort("0");
        Assert.Null(mail.Problem);

        var rows = OptionAccountRows.FormRows(mail, forms);
        Assert.All(new[] { Row(rows, "Test"), Row(rows, "Add") }, act => Assert.Equal((true, null), (act.Enabled, act.Why)));

        var dav = forms.Dav;
        Assert.Equal(S.SettingsAccountsDavMissing, dav.Problem);
        rows = OptionAccountRows.FormRows(dav, forms);
        Assert.All(new[] { Row(rows, "Test"), Row(rows, "Add") }, act => Assert.Equal((false, S.SettingsAccountsDavMissing), (act.Enabled, act.Why)));
        dav.SetPassword("secret");
        dav.SetOwnServer(true);
        dav.SetServer("https://dav.example.com/");
        Assert.Equal(S.SettingsAccountsDavUser, dav.Problem);
        dav.SetUser("anna");
        Assert.Null(dav.Problem);
        Assert.Equal("anna", dav.SignInName);
    }

    /// <summary>
    /// The rows, read from the form: the address, the password masked (no text, bullets at its end once it
    /// holds one), "Set the server myself" and, while set, the server, the port and the security (a mail
    /// server's alone) and the user name; Test connection and Add. Setting one changes the form and none of
    /// the config: the password is never the config's.
    /// </summary>
    [Fact]
    public void TheFormsRowsWriteTheFormAndNeverTheConfig()
    {
        var (forms, _) = Forms();
        var config = new Config();
        var services = new KarpoServices(Accounts(forms), () => { });
        var before = config.ToJson();
        OptionRow Find(string field) => OptionRows.Find(KarpoMenu.Accounts(config, services), OptionAccountRows.Form + "Mail." + field)!;

        Assert.Equal(
            [S.SettingsAccountsEmailHeader, S.SettingsAccountsPasswordHeader, S.SettingsAccountsOwnServer, "", S.DockAccountsTest, S.SettingsCommonAdd],
            OptionAccountRows.FormRows(forms.Mail, forms).Select(row => row.Title));
        var password = Find("Password");
        Assert.Equal((OptionRowKind.Text, true, "", null), (password.Kind, password.Masked, password.Text, password.Summary));

        // Kept as a field of the menu keeps its words (FieldPicks.KeepRow): the form's, never the config's.
        Assert.False(FieldPicks.KeepRow(Find("Address"), new FieldCommit(1, "anna@example.com", null)));
        Assert.False(FieldPicks.KeepRow(Find("Password"), new FieldCommit(2, "hunter2", null)));
        password = Find("Password");
        Assert.Equal(("", OptionAccountRows.Bullets), (password.Text, password.Summary));
        Assert.DoesNotContain(KarpoMenu.Accounts(config, services).SelectMany(row => OptionRows.Flat([row])), row => row.Text.Contains("hunter2") || row.Summary?.Contains("hunter2") == true);
        Assert.False(Find("Own").Set!(1));
        Assert.Equal(
            [S.SettingsAccountsEmailHeader, S.SettingsAccountsPasswordHeader, S.SettingsAccountsOwnServer, S.SettingsAccountsServerHeader, S.DockAccountsPort, S.DockAccountsSecurity, S.SettingsAccountsUserHeader, "", S.DockAccountsTest, S.SettingsCommonAdd],
            OptionAccountRows.FormRows(forms.Mail, forms).Select(row => row.Title));
        Assert.Equal(["SSL/TLS", "STARTTLS"], Find("Security").Choices);
        Assert.False(Find("Security").Set!(1));
        Assert.Equal((AccountSecurity.StartTls, "143"), (forms.Mail.Security, Find("Port").Placeholder));
        Assert.Equal(before, config.ToJson());
        Assert.DoesNotContain("hunter2", config.ToJson());
    }

    /// <summary>
    /// §415: a form's field says what it needs to know on a quieter line under its box (its row's Detail), its name
    /// the box's placeholder: the mail password that Gmail and iCloud need an app password, the server that it is found
    /// from the address, the user name that it is only for one that isn't the address; the CalDAV form's its own.
    /// The address of a mail account needs no word.
    /// </summary>
    [Fact]
    public void AFormsFieldsSayWhatTheyNeedUnderThem()
    {
        var (forms, _) = Forms();
        forms.Mail.SetOwnServer(true);
        forms.Dav.SetOwnServer(true);
        string?[] Hints(AccountForm form) => [.. OptionAccountRows.FormRows(form, forms).Where(row => row.Kind == OptionRowKind.Text).Select(row => row.Detail)];

        Assert.Equal(
            [null, S.SettingsAccountsImapPasswordDescription, S.SettingsAccountsImapServerDescription, null, S.SettingsAccountsUserDescription],
            Hints(forms.Mail));
        Assert.Equal(
            [S.SettingsAccountsDavEmailDescription, S.SettingsAccountsDavPasswordDescription, S.SettingsAccountsDavServerDescription, S.SettingsAccountsUserDescription],
            Hints(forms.Dav));

        // On the menu: the field's item carries it under its box; its placeholder is its name.
        var services = new KarpoServices(Accounts(forms), () => { });
        var accounts = TheDock.SettingsItems(new Config(), services, [], page: KarpoPage.Accounts);
        var form = accounts.Single(item => item.Text == S.SettingsAccountsAddMail).Children!;
        var password = form.Single(item => item.Text == S.SettingsAccountsPasswordHeader);
        Assert.Equal((S.SettingsAccountsImapPasswordDescription, S.SettingsAccountsPasswordHeader),
            (password.Detail, Assert.IsType<FlyoutField>(password.Control).Placeholder));
    }

    /// <summary>
    /// Test connection signs in through the caller and keeps nothing; while it runs every row is greyed,
    /// the one tried saying what it does; once back, how it went stands under it. A failure says why under
    /// the one that failed. Add keeps the account (the caller's), empties the form, wipes the password, and
    /// says it was added under Add. Editing a field takes the last outcome away.
    /// </summary>
    [Fact]
    public void TestAndAddGoThroughTheCallerAndSayHowTheyWent()
    {
        TaskCompletionSource<AccountTry>? held = null;
        List<string> tried = [];
        var forms = new AccountForms(
            (form, add) =>
            {
                tried.Add($"{(add ? "add" : "test")} {form.Address} {form.Password()}");
                held = new TaskCompletionSource<AccountTry>();
                return held.Task;
            },
            work => work(),
            () => tried.Add("changed"));
        var mail = forms.Mail;
        mail.SetAddress("anna@example.com");
        mail.SetPassword("hunter2");

        var rows = OptionAccountRows.FormRows(mail, forms);
        Assert.False(Row(rows, "Test").Set!(0));
        Assert.Equal(["test anna@example.com hunter2"], tried);
        Assert.True(mail.Busy);
        rows = OptionAccountRows.FormRows(mail, forms);
        Assert.All(rows.Where(row => row.Kind == OptionRowKind.Text), row => Assert.False(row.Enabled));
        Assert.Equal((false, S.SettingsAccountsImapLooking("anna@example.com")), (Row(rows, "Test").Enabled, Row(rows, "Test").Why));
        Assert.Equal((false, null), (Row(rows, "Add").Enabled, Row(rows, "Add").Why));
        // A second try while one is on its way, and an edit, do nothing.
        Assert.False(forms.Try(mail, add: true));
        Assert.False(mail.SetAddress("other@example.com"));
        Assert.Equal("anna@example.com", mail.Address);

        held!.SetResult(new AccountTry("imap.example.com"));
        Assert.Equal("changed", tried[^1]);
        rows = OptionAccountRows.FormRows(mail, forms);
        Assert.Equal((AccountFormState.Tested, S.DockAccountsConnected("imap.example.com")), (mail.State, Row(rows, "Test").Detail));
        Assert.Null(Row(rows, "Add").Detail);
        Assert.True(mail.HasPassword);

        // Refused: why, under the one that failed.
        Assert.True(forms.Try(mail, add: true));
        held.SetResult(new AccountTry("", "The server refused that user name and password."));
        rows = OptionAccountRows.FormRows(mail, forms);
        Assert.Equal((AccountFormState.Failed, null, "The server refused that user name and password."), (mail.State, Row(rows, "Test").Detail, Row(rows, "Add").Detail));
        Assert.Equal("anna@example.com", mail.Address);

        // An edit takes the outcome away.
        mail.SetUser("anna");
        Assert.Equal((AccountFormState.Editing, null), (mail.State, mail.Outcome));

        // Added: the form empty, the password wiped, what was added said under Add.
        Assert.True(forms.Try(mail, add: true));
        held.SetResult(new AccountTry("imap.example.com"));
        Assert.Equal((AccountFormState.Added, "", false), (mail.State, mail.Address, mail.HasPassword));
        Assert.Equal(S.SettingsAccountsAddedAt(address: "anna@example.com", host: "imap.example.com"), Row(OptionAccountRows.FormRows(mail, forms), "Add").Detail);
        Assert.Equal(S.SettingsAccountsImapMissing, mail.Problem);
    }

    /// <summary>
    /// The menu closing empties both forms and wipes their passwords; a form being tried is left to finish,
    /// and emptied once back.
    /// </summary>
    [Fact]
    public void TheMenuClosingEmptiesTheForms()
    {
        TaskCompletionSource<AccountTry>? held = null;
        var forms = new AccountForms((_, _) => (held = new()).Task, work => work(), () => { });
        forms.Dav.SetAddress("anna@fastmail.com");
        forms.Dav.SetPassword("secret");
        forms.Mail.SetAddress("anna@example.com");
        forms.Mail.SetPassword("secret");
        Assert.True(forms.Try(forms.Mail, add: false));

        forms.Close();
        Assert.Equal(("", false), (forms.Dav.Address, forms.Dav.HasPassword));
        Assert.Equal(("anna@example.com", true), (forms.Mail.Address, forms.Mail.HasPassword));
        held!.SetResult(new AccountTry("imap.example.com"));
        Assert.Equal(("", false, AccountFormState.Tested), (forms.Mail.Address, forms.Mail.HasPassword, forms.Mail.State));
    }

    private static readonly OptionAccount Ann = new("a", "ann@outlook.com", OptionAccountKind.Microsoft)
    {
        Groups =
        [
            new("Calendars",
            [
                new OptionAccountPart("calendar:work", "Work", OptionGlyphs.Month, true) { Detail = "56 events", Automatic = "#038387" },
                new OptionAccountPart("calendar:home", "Home", OptionGlyphs.Month, false) { Detail = "Not synced", Colour = "#E3008C", Automatic = "#3A96DD" },
            ]),
            new("Mail", [new OptionAccountPart("inbox", "Inbox", OptionGlyphs.Mail, true) { Detail = "1234 messages", Automatic = "#8764B8" }]) { Note = "Couldn't sync: no" },
            new("Tasks", []) { Note = "Karpo needs your permission", Reconnect = true },
        ],
    };

    private static OptionAccounts Accounts(AccountForms forms, List<string>? did = null, params OptionAccount[] all) =>
        new(all, _ => did?.Add("sign in"), id => did?.Add($"again {id}"), id => did?.Add($"remove {id}"), forms)
        {
            Sync = (id, key, on) => did?.Add($"sync {id} {key} {on}"),
            Recolour = (id, key, colour) => did?.Add($"colour {id} {key} {colour ?? "automatic"}"),
        };

    /// <summary>
    /// An account's submenu: per kind its heading, why it did not sync under it, a switch per part with its
    /// count at its end (§453), Reconnect where To Do was never allowed; §452: each part that wears a colour has it on
    /// its own row with its switch (<see cref="OptionRow.Colour"/>), no level of colours apart; a To Do list wears
    /// none. A switch and a colour go to the caller as the Accounts page wrote them — the part's key, on or off,
    /// "#RRGGBB" or Automatic —, and the menu's config is not touched.
    /// </summary>
    [Fact]
    public void AnAccountsPartsAreSwitchedAndColouredAsThePageDidIt()
    {
        List<string> did = [];
        var (forms, _) = Forms();
        var lists = new OptionAccount("t", "tom@outlook.com", OptionAccountKind.Microsoft)
        {
            Groups = [new("Tasks", [new OptionAccountPart("list:shop", "Groceries", OptionGlyphs.Checklist, true) { Detail = "3 open tasks" }])],
        };
        var page = OptionAccountRows.Page(Accounts(forms, did, Ann, lists));
        var row = page.Single(each => each.Kind == OptionRowKind.Things);
        var parts = row.Owned!.Things[0].Rows;
        Assert.Equal(
            [(OptionRowKind.Heading, "Calendars"), (OptionRowKind.Toggle, "Work"), (OptionRowKind.Toggle, "Home"), (OptionRowKind.Heading, "Mail"), (OptionRowKind.Action, "Couldn't sync: no"),
             (OptionRowKind.Toggle, "Inbox"), (OptionRowKind.Heading, "Tasks"), (OptionRowKind.Action, "Karpo needs your permission"), (OptionRowKind.Action, S.CoreAccountsReconnectTasks)],
            parts.Select(part => (part.Kind, part.Title)));
        Assert.Equal(["56 events", "Not synced"], parts.Where(part => part.Kind == OptionRowKind.Toggle).Take(2).Select(part => part.Summary));
        Assert.False(parts[4].Enabled);

        var colours = parts.Where(part => part.Kind == OptionRowKind.Toggle).Select(part => part.Colour!).ToList();
        Assert.All(colours, colour => Assert.Equal((OptionRowKind.Color, S.SettingsAccountsColorHeader), (colour.Kind, colour.Title)));
        Assert.Equal((0, Argb.Parse("#038387")), (colours[0].Chosen, colours[0].Color));
        Assert.Equal(Argb.Parse("#E3008C"), colours[1].Color);
        Assert.Null(Assert.Single(row.Owned.Things[1].Rows, part => part.Kind == OptionRowKind.Toggle).Colour);

        Assert.False(parts[2].Set!(1));
        Assert.False(parts[1].Set!(0));
        Assert.False(parts[8].Set!(0));
        Assert.False(colours[0].Set!(ColorPalette.Presets.ToList().IndexOf(Argb.Parse("#0078D4")!.Value) + 1));
        Assert.False(colours[1].Set!(0));
        // §523: a colour mixed is written once kept, never while it is dragged (an account's colours are its service's).
        Assert.False(colours[2].SetColor!(new Argb(255, 0x65, 0x43, 0x21), false));
        Assert.False(colours[2].SetColor!(new Argb(255, 0x12, 0x34, 0x56), true));
        Assert.Equal(
            ["sync a calendar:home True", "sync a calendar:work False", "again a", "colour a calendar:work #0078D4", "colour a calendar:home automatic", "colour a inbox #123456"],
            did);
        // Found by their keys, as a pick finds its row again.
        Assert.Same(parts[1].Set, OptionRows.Find(page, parts[1].Key)!.Set);
        Assert.Same(colours[2].Set, OptionRows.Find(page, colours[2].Key)!.Set);
    }

    /// <summary>
    /// §452 (the person, 2026-10-31: "the colors for the different accounts / lists should be integrated into the enable /
    /// disable menu and not separate"): in the menu a part's row carries its colour and its switch — a swatch of the
    /// colour it wears, picked or Automatic, before the switch, opening the swatches in place; a click elsewhere on the
    /// row flips the switch. No "Colors" level.
    /// </summary>
    [Fact]
    public void AnAccountsPartsRowCarriesItsSwatchAndItsSwitch()
    {
        List<string> did = [];
        var (forms, _) = Forms();
        List<OptionRow> rows = [.. OptionAccountRows.Page(Accounts(forms, did, Ann))];
        List<OptionPick> picks = [];
        var parts = OptionMenu.Rows(rows, picks).Single(item => item.Text == Ann.Address).Children!;
        Assert.DoesNotContain(parts, item => item.Text == "Colors" || item.Icon is FlyoutIcon.Glyph { Code: OptionGlyphs.Palette });
        FlyoutControl.Tools ToolsOf(string name) => Assert.IsType<FlyoutControl.Tools>(parts.Single(item => item.Text == name).Control);
        Assert.Equal([true, false, true], new[] { "Work", "Home", "Inbox" }.Select(name => ToolsOf(name).Switch));
        // The colour it wears: Work's Automatic, Home's picked, the inbox's Automatic.
        Assert.Equal([Argb.Parse("#038387"), Argb.Parse("#E3008C"), Argb.Parse("#8764B8")], new[] { "Work", "Home", "Inbox" }.Select(name => Assert.Single(ToolsOf(name).Buttons).Swatch));
        var swatch = ToolsOf("Work").Buttons[0];
        Assert.Equal(S.SettingsAccountsColorHeader, swatch.Name);

        bool Apply(uint id) => OptionMenu.PickOf(picks, id) is { } pick && OptionMenu.ApplyRow(OptionRows.Find(rows, pick.Key), pick);
        _ = Apply(parts.Single(item => item.Text == "Work").Id);
        _ = Apply(swatch.Level!.Single(item => item.Text == S.SettingsAccountsColorBlue).Id);
        Assert.Equal(["sync a calendar:work False", "colour a calendar:work #0078D4"], did);
    }

    // --- On a real menu -----------------------------------------------------------------------------

    private static Flyout Show(Gfx gfx, HINSTANCE instance, IReadOnlyList<FlyoutItem> items)
    {
        var work = new RECT { right = (int)(1920 * Scale), bottom = (int)(1040 * Scale) };
        var menu = new Flyout(gfx, instance, 0) { Configures = true, Configuration = true, TypeTitle = S.DockEditSettings };
        menu.Show(gfx, AddListPictures.LookFor(dark: true), Scale, items, FlyoutLayout.List, new POINT { x = work.right / 3, y = work.bottom }, work, Edge.Bottom, null);
        WindowShots.Settle();
        return menu;
    }

    /// <summary>The row titled <paramref name="title"/> among <paramref name="items"/>, opened in place: its level's items.</summary>
    private static IReadOnlyList<FlyoutItem> Open(Flyout menu, IReadOnlyList<FlyoutItem> items, string title)
    {
        var row = items.First(item => item.Text == title && item.Children is not null);
        _ = menu.Click(row.Id);
        Assert.Equal(title, menu.InlineTitle);
        return row.Children!;
    }

    /// <summary>
    /// The password's field on a real menu: typed in, it draws a bullet for each letter; a screen reader
    /// finds an edit that says it is a password, named for its row, and nowhere in the menu's tree the
    /// words typed; Enter keeps them for whoever showed the menu and shuts the level.
    /// </summary>
    [UiFact]
    public void ThePasswordsFieldNeverShowsItsWords()
    {
        WindowShots.OnHiddenDesktop("account-password", (gfx, instance) =>
        {
            var (forms, _) = Forms();
            var services = new KarpoServices(Accounts(forms), () => { });
            var config = new Config();
            var items = TheDock.SettingsItems(config, services, [], page: KarpoPage.Accounts);
            using var menu = Show(gfx, instance, items);
            // §411 (R1), §535: the form is a level of the Accounts page's, its fields on it.
            var level = Open(menu, items, S.SettingsAccountsAddMail);
            var field = level.Single(item => item.Text == S.SettingsAccountsPasswordHeader && item.Control is FlyoutField);
            _ = menu.Click(field.Id);
            Assert.Equal(field.Id, menu.FieldKeys);
            foreach (var c in "hunter2")
            {
                _ = menu.Message(WM.WM_CHAR, (WPARAM)(nuint)c, 0);
            }
            var typed = menu.FieldOf(field.Id)!;
            Assert.Equal(("hunter2", new string(MenuTextField.Bullet, 7)), (typed.Text, typed.Drawn));

            var tree = menu.AccessTree;
            var node = tree.Root.Children.Single(child => child.AutomationId == $"Item{field.Id}");
            Assert.Equal((UiaRole.Edit, true, S.SettingsAccountsPasswordHeader), (node.Role, node.Password, node.Name));
            Assert.All(tree.Root.Children, child => Assert.DoesNotContain("hunter2", $"{child.Name}|{child.Status}|{child.Help}"));
            Assert.All(tree.Root.Children.Where(child => child != node), child => Assert.False(child.Password));

            // Enter keeps the words for whoever showed the menu (the dock: the form), the form standing (§411).
            _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RETURN, 0);
            Assert.Equal("hunter2", menu.TakeFieldCommit(field.Id)?.Text);
            Assert.Equal(S.SettingsAccountsAddMail, menu.InlineTitle);
        });
    }

    /// <summary>The pictures (see the top of the file), dark, 150 %.</summary>
    [UiFact]
    public void TheAccountsLevelsArePictured()
    {
        WindowShots.OnHiddenDesktop("account-pictures", (gfx, instance) =>
        {
            var (forms, _) = Forms();
            var ms = Ann with { Address = "anna@outlook.com" };
            var imap = new OptionAccount("i", "anna@example.com", OptionAccountKind.Mail)
            {
                Groups = [new("Mail", [new OptionAccountPart("inbox", "Inbox", OptionGlyphs.Mail, true) { Detail = "812 messages · 3 unread", Automatic = "#3A96DD" }])],
            };
            var dav = new OptionAccount("d", "anna@fastmail.com", OptionAccountKind.Dav);
            var services = new KarpoServices(Accounts(forms, null, ms, imap, dav), () => { });
            var config = new Config();

            void Save(Flyout menu, string file)
            {
                WindowShots.Settle(300);
                Assert.True(WindowShots.SaveTogether([.. menu.OpenPanels], file, (0, 0, 0)));
            }

            var items = TheDock.SettingsItems(config, services, [], page: KarpoPage.Accounts);
            using (var menu = Show(gfx, instance, items))
            {
                Save(menu, "settings-accounts-dark-150.png");
                var parts = Open(menu, items, "anna@outlook.com");
                Save(menu, "settings-account-dark-150.png");
                // §452: a calendar's swatch pressed, its colours opened in place.
                var at = menu.ToolMiddle(parts.Single(item => item.Text == "Home").Id, 0)!.Value;
                var lParam = (LPARAM)(nint)((at.y << 16) | (at.x & 0xFFFF));
                _ = menu.Message(WM.WM_MOUSEMOVE, 0, lParam);
                _ = menu.Message(WM.WM_LBUTTONDOWN, 1, lParam);
                _ = menu.Message(WM.WM_LBUTTONUP, 0, lParam);
                WindowShots.Settle();
                Assert.Equal(2, menu.InlineDepth);
                Save(menu, "settings-account-colour-dark-150.png");
            }

            forms.Mail.SetAddress("anna@example.com");
            forms.Mail.SetOwnServer(true);
            forms.Mail.SetServer("imap.example.com");
            forms.Mail.SetSecurity(AccountSecurity.StartTls);
            items = TheDock.SettingsItems(config, services, [], page: KarpoPage.Accounts);
            using (var menu = Show(gfx, instance, items))
            {
                var level = Open(menu, items, S.SettingsAccountsAddMail);
                _ = menu.Click(level.Single(item => item.Text == S.SettingsAccountsPasswordHeader && item.Control is FlyoutField).Id);
                foreach (var c in "correct horse")
                {
                    _ = menu.Message(WM.WM_CHAR, (WPARAM)(nuint)c, 0);
                }
                Save(menu, "settings-imap-password-dark-150.png");
            }
            forms.Mail.SetPassword("correct horse");
            items = TheDock.SettingsItems(config, services, [], page: KarpoPage.Accounts);
            using (var menu = Show(gfx, instance, items))
            {
                _ = Open(menu, items, S.SettingsAccountsAddMail);
                Save(menu, "settings-imap-form-dark-150.png");
            }

            // How a test went, under it: here, refused.
            var (refusing, _) = Forms((_, _) => new AccountTry("", S.CoreAccountsRefusedPassword));
            refusing.Mail.SetAddress("anna@example.com");
            refusing.Mail.SetPassword("wrong");
            Assert.True(refusing.Try(refusing.Mail, add: false));
            services = new KarpoServices(Accounts(refusing, null, ms, imap, dav), () => { });
            items = TheDock.SettingsItems(config, services, [], page: KarpoPage.Accounts);
            using (var menu = Show(gfx, instance, items))
            {
                _ = Open(menu, items, S.SettingsAccountsAddMail);
                Save(menu, "settings-imap-refused-dark-150.png");
            }
        });
    }
}
