// menus: the person's own things managed one way wherever they are (docs\widgets.md §324; §317 the
// pattern, NoteMenuTests): a to-do line's lists of every source in one level, under a heading for each,
// "New list" at its top; the accounts as ⚙'s level (§351: a widget links there), through the
// caller's seams (no sign-in is reached). The holiday calendars' level is HolidaysTests'. Pictures (KARPO_UI_TESTS=1,
// KARPO_PNG_DIR), dark, 150 %: items-todo-lists-dark.png, items-holidays-dark.png, items-accounts-dark.png.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed class OwnedLevelsTests
{
    private const float Scale = 1.5f;

    /// <summary>Two accounts' To Do lists as the dock last read them, each its default and one of its own, each account by its name (§445).</summary>
    private static readonly TodoSource[] Remote =
    [
        new("me@outlook.com", "tasks", "Tasks", Default: true) { AccountName = "Ann Smith" },
        new("me@outlook.com", "shop", "Groceries") { AccountName = "Ann Smith" },
        new("work@contoso.com", "tasks", "Tasks", Default: true) { AccountName = "Contoso" },
        new("work@contoso.com", "launch", "Launch") { AccountName = "Contoso" },
    ];

    /// <summary>What the menu asked of To Do.</summary>
    private sealed class Lists : ITodoLists
    {
        public List<string> Calls { get; } = [];

        public bool Rename(string account, string list, string name)
        {
            Calls.Add($"rename {account} {list} {name}");
            return true;
        }

        public bool Delete(string account, string list)
        {
            Calls.Add($"delete {account} {list}");
            return true;
        }
    }

    private static void WithBook(Action<Notebook> body)
    {
        var folder = Path.Combine(Isolation.Folder, "owned-levels", Guid.NewGuid().ToString("N"));
        using var book = new Notebook(folder);
        body(book);
    }

    /// <summary><paramref name="item"/>'s row called <paramref name="title"/> as its menu shows it, and the menu's picks.</summary>
    private static (FlyoutItem Row, List<OptionPick> Picks) Menu(WidgetItem item, OptionDevices devices, string title, OptionPlace? place = null)
    {
        List<OptionPick> picks = [];
        return (OptionMenu.Rows(OptionRows.Of(item, devices, place), picks).Single(row => row.Text == title), picks);
    }

    private static IReadOnlyList<FlyoutTool> Tools(FlyoutItem row) => (row.Control as FlyoutControl.Tools)?.Buttons ?? [];

    /// <summary>
    /// §324: a to-do line's list is one row, "List", its level this PC's lists and each account's, the one
    /// shown ticked and said at the row's end; "New list" under them makes one on this PC and shows it. A pick
    /// of an account's names the account and the list. A list is renamed (F2) and deleted (asked first) where
    /// it is kept allows it: this PC's in the notebook, an account's in To Do — never its default "Tasks", nor
    /// where To Do is not written. §445: the lists grouped under one heading for each place they come from,
    /// "On this PC" and each account's name, and no list says its account (§411 had the address under every
    /// row, again and again); from one place alone, no heading.
    /// </summary>
    [Fact]
    public void AToDoLinesListsAreEveryListOfEverySourceInOneLevel()
    {
        WithBook(book =>
        {
            var home = book.AddList("Home");
            var lists = new Lists();
            var devices = new OptionDevices { Book = book, TodoSources = Remote, TodoLists = lists };
            var line = ListKind.Todo.New();
            bool Act(FlyoutItem item, List<OptionPick> picks) => OptionMenu.Apply(line, OptionMenu.PickOf(picks, item.Id)!, devices);

            // One row for where the tasks come from and which list: no "Tasks from" any more.
            Assert.Equal([OptionRows.TodoList], OptionRows.Of(line, devices).Where(row => row.Slot == OptionSlot.Subject && row.Kind != OptionRowKind.Link).Select(row => row.Key));
            var (row, picks) = Menu(line, devices, S.SettingsWidgetTodoListHeader);
            Assert.Equal("Home", row.Value!.Text);
            var level = row.Children!;
            // §445: a heading for each place, its lists under it; "New list" right under them (§411, R9), ruled off from the
            // last place's, as it makes one on this PC.
            Assert.Equal(
                [S.WidgetTodoOnThisPc, "Home", "Ann Smith", "Tasks", "Groceries", "Contoso", "Tasks", "Launch", "", S.SettingsWidgetNotebookNewList],
                level.Select(item => item.Text));
            Assert.Equal([0, 2, 5], level.Select((item, at) => (item, at)).Where(each => each.item.Header).Select(each => each.at));
            Assert.Equal([8], level.Select((item, at) => (item, at)).Where(each => each.item.Separator).Select(each => each.at));
            // No list says where it is kept: not its account's address, nor its name.
            Assert.All(level, item => Assert.Null(item.Detail));
            int[] things = [1, 3, 4, 6, 7];
            Assert.Equal([true, false, false, false, false], things.Select(at => level[at].Checked == true));
            Assert.Equal([2, 0, 2, 0, 2], things.Select(at => Tools(level[at]).Count));
            Assert.Equal([MenuChord.Rename, MenuChord.Delete], Tools(level[4]).Select(tool => tool.Keys!.Value));

            // An account's list picked: the account and the list named; the row says it, ticked.
            Assert.True(Act(level[4], picks));
            Assert.Equal(("me@outlook.com", "shop"), (line.Todo!.Account, line.Todo.TaskList));
            (row, picks) = Menu(line, devices, S.SettingsWidgetTodoListHeader);
            Assert.Equal("Groceries", row.Value!.Text);
            Assert.True(row.Children![4].Checked);
            Assert.False(Act(row.Children[4], picks));

            // Renamed in To Do, from its name's field; deleted there, asked first.
            var field = Tools(row.Children[4])[0].Level!.Single();
            Assert.Equal("Groceries", Assert.IsType<FlyoutField>(field.Control).Text);
            Assert.True(FieldPicks.Keep(line, OptionMenu.PickOf(picks, field.Id)!, new FieldCommit(field.Id, "Food", null), devices));
            var bin = OptionMenu.PickOf(picks, Tools(row.Children[4])[1].Id)!;
            var question = OptionMenu.AskOf(line, bin, devices)!;
            Assert.Equal((S.SettingsWidgetNotebookDeleteList, S.WidgetTodoDeleteRemoteListBody(name: "Groceries", service: "Microsoft To Do")), (question.Title, question.Content));
            Assert.True(OptionMenu.Apply(line, bin, devices));
            Assert.Equal(["rename me@outlook.com shop Food", "delete me@outlook.com shop"], lists.Calls);

            // The other account's list of the same name is its own.
            Assert.True(Act(row.Children[6], picks));
            Assert.Equal(("work@contoso.com", "tasks"), (line.Todo!.Account, line.Todo.TaskList));

            // This PC's list picked again: no account.
            Assert.True(Act(row.Children[1], picks));
            Assert.Equal((null, null, home), (line.Todo.Account, line.Todo.TaskList, line.Todo.List));

            // "New list": one made on this PC and shown at once.
            Assert.True(Act(row.Children[^1], picks));
            var made = book.Current.Lists.Single(each => each.Id != home);
            Assert.Equal((null, made.Id, S.WidgetTodoNewList), (line.Todo.Account, line.Todo.List, made.Name));

            // Where To Do is not written, an account's list has no buttons.
            (row, _) = Menu(line, devices with { TodoLists = null }, S.SettingsWidgetTodoListHeader);
            Assert.Empty(Tools(row.Children!.Single(item => item.Text == "Groceries")));
            Assert.Equal(2, Tools(row.Children!.Single(item => item.Text == "Home")).Count);
            // From one place alone, no heading: this PC's lists without accounts; one account's lists alone.
            (row, _) = Menu(line, devices with { TodoSources = [] }, S.SettingsWidgetTodoListHeader);
            Assert.Equal(["Home", S.WidgetTodoNewList, "", S.SettingsWidgetNotebookNewList], row.Children!.Select(item => item.Text));
            Assert.All(row.Children!, item => Assert.Null(item.Detail));
            (row, _) = Menu(line, new OptionDevices { TodoSources = Remote[..2], Notebook = new NotebookFile() }, S.SettingsWidgetTodoListHeader);
            Assert.Equal(["Tasks", "Groceries"], row.Children!.Select(item => item.Text));
            Assert.All(row.Children!, item => Assert.False(item.Header || item.Separator || item.Detail is not null));
        });
    }

    /// <summary>
    /// §445, §535: the Accounts page lists an account by its name (<see cref="OptionAccount.Name"/>), its address under it — the
    /// name its lists are grouped under in a to-do line's menu; one without a name by its address, its kind under it.
    /// </summary>
    [Fact]
    public void AnAccountIsListedByItsNameItsAddressUnderIt()
    {
        var (accounts, _) = Accounts(new OptionAccount("m1", "me@outlook.com", OptionAccountKind.Microsoft) { Name = "Ann Smith" }, Three[1]);
        var page = OptionMenu.Rows(OptionAccountRows.Page(accounts), []);
        Assert.Equal([("Ann Smith", "me@outlook.com"), ("me@example.com", S.SettingsAccountsImapHeader)], page.Skip(1).Take(2).Select(item => (item.Text, item.Detail)));
    }

    /// <summary>
    /// §452 (the person, 2026-10-31: "the list of accounts should be a little more separate from the add account
    /// buttons"): every list of the person's own things is ruled off from the ways to add to it, which stand right
    /// under it (§411, R9) — the holiday calendars, a to-do line's lists from one place, a line's notes, the layouts —;
    /// a level with nothing in it yet is its ways to add alone, no rule. §535: the accounts, a page of their own, stand
    /// under "Your accounts" and the ways to add one under "Add an account".
    /// </summary>
    [Fact]
    public void EveryListIsRuledOffFromTheWaysToAddToIt()
    {
        // The words of a level, a rule "—".
        static IEnumerable<string> Words(FlyoutItem row) => row.Children!.Select(item => item.Separator ? "—" : item.Text);
        static FlyoutItem Only(IReadOnlyList<OptionRow> rows, string title) => OptionMenu.Rows(rows, []).Single(item => item.Text == title);

        // §535: the page's own rows, a heading "#".
        static IEnumerable<string> Page(OptionAccounts accounts) => OptionMenu.Rows(OptionAccountRows.Page(accounts), []).Select(item => item.Header ? "#" + item.Text : item.Separator ? "—" : item.Text);
        var (accounts, _) = Accounts(Three[..2]);
        Assert.Equal(["#" + S.SettingsAccountsYours, "me@outlook.com", "me@example.com", "#" + S.SettingsAccountsAddHeader, S.SettingsAccountsAddMicrosoft, S.SettingsAccountsAddGoogle, S.SettingsAccountsAddMail, S.SettingsAccountsAddDav], Page(accounts));
        (accounts, _) = Accounts();
        Assert.Equal(["#" + S.SettingsAccountsYours, S.SettingsAccountsEmpty, "#" + S.SettingsAccountsAddHeader, S.SettingsAccountsAddMicrosoft, S.SettingsAccountsAddGoogle, S.SettingsAccountsAddMail, S.SettingsAccountsAddDav], Page(accounts));

        var config = new Config { Holidays = new HolidayOptions { Calendars = [new() { Country = "GB" }] } };
        Assert.Equal([HolidayPlaces.Name("GB"), "—", S.CoreHolidaysAdd], Words(Only(OptionHolidays.Rows(config), S.CoreHolidaysName)));


        WithBook(book =>
        {
            _ = book.AddList("Home");
            var devices = new OptionDevices { Book = book };
            Assert.Equal(["Home", "—", S.SettingsWidgetNotebookNewList], Words(Only(OptionRows.Of(ListKind.Todo.New(), devices), S.SettingsWidgetTodoListHeader)));
            var note = ListKind.Note.New();
            Assert.Equal([S.SettingsWidgetNotebookNewNote], Words(Only(OptionRows.Of(note, devices), S.SettingsWidgetNoteNoteHeader)));
            _ = book.AddNote("Ideas", DateTimeOffset.Now);
            Assert.Equal(["Ideas", "—", S.SettingsWidgetNotebookNewNote], Words(Only(OptionRows.Of(note, devices), S.SettingsWidgetNoteNoteHeader)));
        });
    }

    /// <summary>The accounts there are, and what the menu asked of them.</summary>
    private static (OptionAccounts Accounts, List<string> Did) Accounts(params OptionAccount[] all)
    {
        List<string> did = [];
        var forms = new AccountForms((form, add) => Task.FromResult(new AccountTry("imap.example.com")), work => work(), () => { });
        return (new OptionAccounts(all, kind => did.Add($"add {kind}"), id => did.Add($"again {id}"), id => did.Add($"remove {id}"), forms), did);
    }

    private static readonly OptionAccount[] Three =
    [
        new("m1", "me@outlook.com", OptionAccountKind.Microsoft),
        new("i1", "me@example.com", OptionAccountKind.Mail),
        new("d1", "me@fastmail.com", OptionAccountKind.Dav),
    ];

    /// <summary>
    /// §324, §351: every widget that shows what the accounts hold — the mail, a calendar of any style and the
    /// meetings, the contacts, a to-do line, and the faces that show them — has "Manage accounts", a link to
    /// the Accounts page (§535), and no accounts of its own. There: each account, its kind under it, "Sign in again" on a
    /// Microsoft account's and its bin, asked first with Settings' question; then a way to add every kind. Each is done
    /// by the caller; none changes the config.
    /// </summary>
    [Fact]
    public void TheAccountsAreAddedSignedInToAgainAndRemovedInSettingsAlone()
    {
        Assert.Equal(
            [AccountShows.Mail, AccountShows.Contacts, AccountShows.Tasks, AccountShows.Calendars, AccountShows.Calendars, AccountShows.Calendars, AccountShows.None],
            new[] { ListKind.Mail, ListKind.Contacts, ListKind.Todo, ListKind.Meeting, ListKind.Month, ListKind.WeekView, ListKind.Heading }.Select(kind => OptionAccountRows.Shows(kind.New())));
        static bool Links(OptionRow row) => row is { Kind: OptionRowKind.Link, Link: OptionLink.Accounts };
        Assert.Single(OptionRows.Of(ListKind.Mail.New()), Links);
        Assert.DoesNotContain(OptionRows.Of(ListKind.Heading.New()), Links);
        var folder = BarKind.FolderList.New();
        folder.Folder!.Faces = [FolderFace.Of(FolderFaces.MailId), FolderFace.Of(FolderFaces.WifiId)];
        Assert.Single(OptionRows.OfFace(folder.Folder, 0), Links);
        Assert.DoesNotContain(OptionRows.OfFace(folder.Folder, 1), Links);
        Assert.DoesNotContain(OptionRows.Of(ListKind.Mail.New()).Concat(OptionRows.OfFace(folder.Folder, 0)), row => row.Key == OptionAccountRows.Accounts);

        // §535: the Accounts page: every account, every kind to add, each under its heading.
        var (accounts, did) = Accounts(Three);
        var rows = KarpoMenu.Accounts(new Config(), new KarpoServices(accounts, () => { }));
        List<OptionPick> picks = [];
        var level = OptionMenu.Rows(rows, picks);
        // §411 (R9): every account, then right under them a way to add each kind.
        Assert.Equal([S.SettingsAccountsYours, "me@outlook.com", "me@example.com", "me@fastmail.com", S.SettingsAccountsAddHeader, S.SettingsAccountsAddMicrosoft, S.SettingsAccountsAddGoogle, S.SettingsAccountsAddMail, S.SettingsAccountsAddDav], level.Select(item => item.Text));
        Assert.True(level[0].Header && level[4].Header);
        level = [.. level.Where(item => !item.Header)];
        level.Insert(3, new FlyoutItem(0, "", Separator: true));
        Assert.Equal([S.SettingsAccountsMicrosoftHeader, S.SettingsAccountsImapHeader, S.SettingsAccountsDavHeader], level.Take(3).Select(item => item.Detail));
        Assert.All(level.Take(3), item => Assert.Null(item.Checked));
        Assert.Equal([S.DockMenuAccountsSignInAgain, S.SettingsCommonRemove], Tools(level[0]).Select(tool => tool.Name));
        Assert.Equal([S.SettingsCommonRemove], Tools(level[1]).Select(tool => tool.Name));
        // §338: a mail server is added in its form, its fields on it (§411, R1).
        var form = level[6].Children!;
        Assert.Equal(
            [S.SettingsAccountsEmailHeader, S.SettingsAccountsPasswordHeader, S.SettingsAccountsOwnServer, "", S.DockAccountsTest, S.SettingsCommonAdd],
            form.Select(item => item.Text));
        Assert.All(form.Take(2), item => Assert.Equal(item.Text, Assert.IsType<FlyoutField>(item.Control).Placeholder));

        OptionRow RowOf(OptionPick pick) => OptionRows.Find(rows, pick.Key)!;
        var microsoft = OptionMenu.PickOf(picks, level[4].Id)!;
        Assert.False(OptionMenu.ApplyRow(RowOf(microsoft), microsoft));
        var again = OptionMenu.PickOf(picks, Tools(level[0])[0].Id)!;
        Assert.Null(RowOf(again).Ask);
        Assert.False(OptionMenu.ApplyRow(RowOf(again), again));
        var remove = OptionMenu.PickOf(picks, Tools(level[1])[0].Id)!;
        var question = RowOf(remove).Ask!;
        Assert.Equal((S.SettingsAccountsRemoveTitle("me@example.com"), S.SettingsAccountsRemoveBody), (question.Title, question.Content));
        Assert.False(OptionMenu.ApplyRow(RowOf(remove), remove));
        Assert.Equal(["add Microsoft", "again m1", "remove i1"], did);

        // None: "No account yet" under "Your accounts", a note; the ways to add one under theirs.
        var (none, _) = Accounts();
        var empty = OptionMenu.Rows(OptionAccountRows.Page(none), []);
        Assert.True(empty.Single(item => item.Text == S.SettingsAccountsEmpty).Note);
        Assert.Equal([S.SettingsAccountsAddMicrosoft, S.SettingsAccountsAddGoogle, S.SettingsAccountsAddMail, S.SettingsAccountsAddDav], empty.SkipWhile(item => item.Text != S.SettingsAccountsAddHeader).Skip(1).Select(item => item.Text));
    }

    // --- Pictures -----------------------------------------------------------------------------------

    private static Flyout Show(Gfx gfx, HINSTANCE instance, IReadOnlyList<FlyoutItem> items)
    {
        var work = new RECT { right = (int)(1920 * Scale), bottom = (int)(1040 * Scale) };
        var menu = new Flyout(gfx, instance, 0) { Configures = true, Configuration = true };
        menu.Show(gfx, AddListPictures.LookFor(dark: true), Scale, items, FlyoutLayout.List, new POINT { x = work.right / 3, y = work.bottom }, work, Edge.Bottom, null);
        WindowShots.Settle();
        return menu;
    }

    /// <summary><paramref name="title"/>'s level of <paramref name="items"/> open in place, the pointer on <paramref name="lit"/>'s row: <paramref name="file"/>.</summary>
    private static void Picture(Gfx gfx, HINSTANCE instance, IReadOnlyList<FlyoutItem> items, string title, string lit, string file)
    {
        var row = items.Single(item => item.Text == title);
        using var menu = Show(gfx, instance, items);
        _ = menu.Click(row.Id);
        Assert.Equal((1, title), (menu.InlineDepth, menu.InlineTitle));
        menu.PointTo(row.Children!.First(item => item.Text == lit && !item.Header).Id);
        WindowShots.Settle(300);
        Assert.True(WindowShots.SaveTogether([.. menu.OpenPanels], file, (0, 0, 0)));
    }

    /// <summary>The three levels, dark, 150 %: a to-do line's lists, ⚙'s public holidays and accounts.</summary>
    [UiFact]
    public void TheLevelsArePictured()
    {
        WindowShots.OnHiddenDesktop("owned-levels", (gfx, instance) => WithBook(book =>
        {
            _ = book.AddList("Home");
            _ = book.AddList("Work");
            var line = ListKind.Todo.New();
            var devices = new OptionDevices { Book = book, TodoSources = Remote, TodoLists = new Lists() };
            Picture(gfx, instance, OptionMenu.Section(OptionRows.Of(line, devices), []), S.SettingsWidgetTodoListHeader, "Groceries", "items-todo-lists-dark.png");

            var config = new Config { Holidays = new HolidayOptions { Calendars = [new() { Country = "CH", Region = "CH-ZH" }, new() { Country = "GB" }, new() { Country = "FR" }] } };
            // ⚙'s levels, the one place the holidays and the accounts are set (§351).
            // §445: the Microsoft account by its name, its address under it.
            var (accounts, _) = Accounts([Three[0] with { Name = "Ann Smith" }, .. Three[1..]]);
            var services = new KarpoServices(accounts, () => { });
            var items = OptionMenu.Rows(KarpoMenu.Holidays(config, services), []);
            Picture(gfx, instance, items, S.CoreHolidaysName, HolidayPlaces.Name("GB"), "items-holidays-dark.png");
            // §535: the accounts a page of their own, its rows the menu's own.
            items = OptionMenu.Rows(KarpoMenu.Accounts(config, services), []);
            using var menu = Show(gfx, instance, items);
            menu.PointTo(items.First(item => item.Text == "Ann Smith").Id);
            WindowShots.Settle(300);
            Assert.True(WindowShots.SaveTogether([.. menu.OpenPanels], "items-accounts-dark.png", (0, 0, 0)));
        }));
    }
}
