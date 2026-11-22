// accounts: the accounts' page (docs\widgets.md §324, §338, §535; the person: "Accounts can only be managed in the
// Settings app."; 2026-11-07: "accounts should get their complete own section."). Under "Your accounts" the person's own
// things as a line's notes are (OptionRow.Owned, §317), standing on the page itself (OptionRow.Inline): each account, its
// kind under its address, at its end "Sign in again" where it signs in in the browser (a Microsoft or Google account) and
// its bin, asked first; "No account yet" for none. Under "Add an account" a way to add each kind. Removing one removes it
// from Karpo, for every widget. Adding one is Settings' own sign-in: a Microsoft or Google account's in the browser there
// and then (§687).
// §324 to §534 this was one row, "Accounts", a level under ⚙'s Services.
//
// §351 (the person, 2026-10-25: "some settings appear multiple times like account, then i can also change
// account settings in a widget for example. i would rather not have that"): §324 to §350 had this level in
// every widget that shows what the accounts hold too, filtered to those that give it. The accounts are
// Karpo's as a whole: a widget that shows what they hold — the mail, the calendars in every style and the
// meetings, the contacts, a to-do line, and the faces that show them — has "Manage accounts" instead, a
// link (OptionRowKind.Link) to ⚙ › Accounts.
//
// §338 (the settings-in-the-dock plan's P4): the rest of the Accounts page is here too:
//
//  - "Add an account ›" is a level of the kinds: "Microsoft account" and §687 "Google account" (signed in to in the
//    browser, greyed where this Karpo has no client there), "Mail server (IMAP) ›" and "CalDAV and CardDAV ›", each
//    its form (AccountForm.cs: the address, a masked password, the server set by hand, Test connection
//    and Add).
//  - **An account's submenu** is what is synced of it, kind by kind, as the page's card opened: a heading
//    per kind (Calendars, Contacts, Mail, Tasks), why it did not sync under it where it did not, a switch
//    per calendar, address book, inbox or task list with how many it holds under its name, "Reconnect to
//    sync tasks" (§687: calendars, contacts, mail) where the person never let Karpo at that kind; §452: each
//    part's colour (PartColours) a swatch on its
//    switch's row, opening the colours in place. A switch and a colour are written where the page writes
//    them (accounts.json, the caller's Sync and Recolour), which the dock hears, and the menu shows them as
//    they are now.
//  - §646 (the person, 2026-11-09: "the email folder selector to select which folders to sync should be under an
//    additional menu so that the account is not that long when extended"): the mail's parts — the inbox, then §639 every
//    folder — are not among them but on a page of their own, Mail folders, under the Accounts entry, its way back at its
//    top; in their place one link, "Mail folders", saying what is synced ("Inbox and 2 more synced"). No third level.
//
// The accounts are Karpo.Accounts', which Karpo.Core does not know: the caller lists them and does what
// is asked (OptionAccounts; the dock's Dock.MenuAccounts.cs, a test's own).

namespace Karpo.Core;

/// <summary>§324: what of an account's a widget shows.</summary>
[Flags]
public enum AccountShows
{
    None = 0,
    Calendars = 1,
    Contacts = 2,
    Mail = 4,
    Tasks = 8,
}

/// <summary>§324: a kind of account, as Settings' Accounts page adds them.</summary>
public enum OptionAccountKind
{
    /// <summary>A Microsoft account: calendars, contacts, mail and To Do, signed in to in the browser.</summary>
    Microsoft,

    /// <summary>§687: a Google account: Calendar, Contacts, Gmail and Tasks, signed in to in the browser.</summary>
    Google,

    /// <summary>A mail server (IMAP): mail alone.</summary>
    Mail,

    /// <summary>A CalDAV and CardDAV server: calendars and contacts.</summary>
    Dav,
}

/// <summary>§324: one account as a menu lists it.</summary>
public sealed record OptionAccount(string Id, string Address, OptionAccountKind Kind)
{
    /// <summary>§445: the name it is listed by (AccountNames), its address under it; null, or its address, for its address, its kind under it.</summary>
    public string? Name { get; init; }

    /// <summary>§445: what it is listed by: its <see cref="Name"/>, else its address.</summary>
    public string Shown => Name is { Length: > 0 } name ? name : Address;

    /// <summary>§338: what is synced of it, kind by kind, in the page's order; empty for nothing listed.</summary>
    public IReadOnlyList<OptionAccountGroup> Groups { get; init; } = [];

    /// <summary>§638: everything synced of it, kind by kind, as one line ("1,234 events · 210 contacts"); null where nothing is counted yet.</summary>
    public string? Holds { get; init; }
}

/// <summary>§338: one kind of an account's parts (its calendars, its mail), under its heading <paramref name="Title"/>.</summary>
public sealed record OptionAccountGroup(string Title, IReadOnlyList<OptionAccountPart> Parts)
{
    /// <summary>What is said under the heading: why it did not sync, or that it has not yet where it lists nothing; null for nothing.</summary>
    public string? Note { get; init; }

    /// <summary>A kind the person never let Karpo at (To Do, §687 a kind unticked at Google): "Reconnect to sync …" under the heading.</summary>
    public bool Reconnect { get; init; }

    /// <summary>§687: which kind it is, which its Reconnect names; None, as the tasks.</summary>
    public AccountShows Kind { get; init; }

    /// <summary>§685: its kind's glyph (calendars, mail, contacts, tasks), beside its heading in the account's card; empty for none.</summary>
    public string Glyph { get; init; } = "";

    /// <summary>
    /// §646: the mail's: its parts (the inbox, then every folder) on a page of their own, "Mail folders", one link in their
    /// place saying this of them ("Inbox and 2 more synced"); null for parts that stand in the account's own rows.
    /// </summary>
    public string? Folders { get; init; }
}

/// <summary>
/// §338: one calendar, address book, inbox or To Do list of an account: <paramref name="Key"/> what turns its
/// sync off and keeps its colour (Karpo.Accounts' SyncKeys), its name and glyph, and whether it is synced.
/// </summary>
public sealed record OptionAccountPart(string Key, string Name, string Glyph, bool On)
{
    /// <summary>How many it holds, or that it is not synced, under its name.</summary>
    public string? Detail { get; init; }

    /// <summary>The colour the person picked, <c>#RRGGBB</c>; null for none.</summary>
    public string? Colour { get; init; }

    /// <summary>What it wears unpicked: the account's own colour, else the palette's; empty for a part that wears none (a To Do list).</summary>
    public string Automatic { get; init; } = "";

    public bool Coloured => Automatic.Length > 0;
}

/// <summary>
/// §324: the accounts there are (<paramref name="All"/>), and what signs in to an account of a kind in the
/// browser (<paramref name="SignIn"/>: Microsoft, §687 Google), signs in to one again and removes one, by its id;
/// §338: the mail server's and the CalDAV server's forms (<paramref name="Forms"/>). None changes the widget's
/// settings: the accounts are not the config's.
/// </summary>
public sealed record OptionAccounts(IReadOnlyList<OptionAccount> All, Action<OptionAccountKind> SignIn, Action<string> SignInAgain, Action<string> Remove, AccountForms Forms)
{
    /// <summary>Whether an account of a kind can be signed in to in the browser: this Karpo has its client there.</summary>
    public Func<OptionAccountKind, bool> CanSignIn { get; init; } = _ => true;

    /// <summary>§338: an account's part synced or not: its id, the part's key, on.</summary>
    public Action<string, string, bool> Sync { get; init; } = (_, _, _) => { };

    /// <summary>§338: an account's part's colour: its id, the part's key, <c>#RRGGBB</c> or null for Automatic.</summary>
    public Action<string, string, string?> Recolour { get; init; } = (_, _, _) => { };
}

/// <summary>§324, §535: the Accounts page's rows; see the top of the file.</summary>
public static class OptionAccountRows
{
    /// <summary>The accounts themselves (each a thing of theirs), or "No account yet".</summary>
    public const string Accounts = "Accounts";

    /// <summary>§535: the page's headings, "Your accounts" and "Add an account".</summary>
    public const string YoursHeading = "Heading.Accounts", AddHeading = "Heading.AddAccount";

    /// <summary>An account signed in to again, then its id.</summary>
    public const string Again = "AccountsAgain.";

    /// <summary>An account removed, then its id.</summary>
    public const string Remove = "AccountsRemove.";

    /// <summary>§338: a kind signed in to in the browser in "Add an account", then its kind.</summary>
    public const string Add = "AccountsAdd.";

    /// <summary>§338: "Microsoft account" in "Add an account".</summary>
    public const string AddMicrosoft = Add + nameof(OptionAccountKind.Microsoft);

    /// <summary>§687: "Google account" in "Add an account".</summary>
    public const string AddGoogle = Add + nameof(OptionAccountKind.Google);

    /// <summary>§338: a form's rows, then its kind and the field ("AccountsForm.Mail.Address").</summary>
    public const string Form = "AccountsForm.";

    /// <summary>§338: a part's switch, then the account's id, '|' and the part's key.</summary>
    public const string Part = "AccountsPart.";

    /// <summary>§338: a part's colour, then the account's id, '|' and the part's key.</summary>
    public const string Colour = "AccountsColour.";

    /// <summary>§646: an account's "Mail folders", the link to its page, then the account's id.</summary>
    public const string Folders = "AccountsFolders.";

    /// <summary>§338: "Reconnect to sync tasks", then the account's id.</summary>
    public const string Reconnect = "AccountsReconnect.";

    /// <summary>What a masked field's row says at its end while it holds a password: as many bullets whatever its length.</summary>
    public const string Bullets = "●●●●●●●●";

    /// <summary>The bullet a masked field draws for each letter, as Windows' PasswordBox does.</summary>
    public const char Bullet = '●';

    /// <summary>What <paramref name="item"/> shows of the accounts: a mail line, a calendar of any style and the meetings, the contacts, a to-do line.</summary>
    public static AccountShows Shows(WidgetItem? item) =>
        item?.Info is not { } info ? AccountShows.None
        : info == WidgetInfos.Mail ? AccountShows.Mail
        : info == WidgetInfos.Contacts ? AccountShows.Contacts
        : info == WidgetInfos.Todo ? AccountShows.Tasks
        : info == WidgetInfos.Meeting || OptionHolidays.Shows(item) ? AccountShows.Calendars
        : AccountShows.None;

    /// <summary>What a folder's <paramref name="face"/> shows of the accounts: the calendar's, the meeting's, the mail's, the contacts'.</summary>
    public static AccountShows Shows(FolderFace face) => face.Kind switch
    {
        FolderFaces.CalendarId or FolderFaces.MeetingId => AccountShows.Calendars,
        FolderFaces.MailId => AccountShows.Mail,
        FolderFaces.ContactsId => AccountShows.Contacts,
        _ => AccountShows.None,
    };

    /// <summary>
    /// §351, §529: "Manage accounts", the way to §535's Accounts page, into <paramref name="rows"/>, for a widget that shows
    /// what they hold: one line of what it draws on — the accounts its parts come from (<paramref name="parts"/>), a
    /// calendar's public holidays (<paramref name="holidays"/>) —, standing under "Show from" where that is among the rows
    /// (§455). It is the one link there: §351 to §528 had "Public holidays" a link of its own too, to ⚙ › Public holidays.
    /// </summary>
    public static void Into(WidgetItem? item, IReadOnlyList<SourcePart>? parts, HolidayOptions? holidays, List<OptionRow> rows)
    {
        if (Shows(item) != AccountShows.None)
        {
            rows.Add(Words(parts, OptionHolidays.Shows(item) ? holidays : null, rows));
        }
    }

    /// <summary>§351, §529: "Manage accounts" into a face's <paramref name="rows"/>, for a face that shows what they hold; the calendar face's holidays in its words.</summary>
    public static void Into(FolderFace face, HolidayOptions? holidays, List<OptionRow> rows)
    {
        if (Shows(face) != AccountShows.None)
        {
            rows.Add(Words(null, face.Kind == FolderFaces.CalendarId ? holidays : null, rows));
        }
    }

    /// <summary>
    /// §529: the accounts' link as a line of what it draws on ("Ada Lovelace, Contoso, Switzerland"): the accounts its
    /// <paramref name="parts"/> come from and their holiday calendars, else the <paramref name="holidays"/> kept; under
    /// "Show from" where <paramref name="rows"/> have it.
    /// </summary>
    private static OptionRow Words(IReadOnlyList<SourcePart>? parts, HolidayOptions? holidays, List<OptionRow> rows)
    {
        List<string> names = parts is { Count: > 0 }
            ? [.. parts.Where(part => part.Group != WidgetSources.HolidaysGroup).Select(part => part.Group).Distinct(StringComparer.Ordinal),
                .. parts.Where(part => part.Group == WidgetSources.HolidaysGroup).Select(part => part.Name)]
            : [.. PublicHolidays.Calendars(holidays?.Normalized()).Select(calendar => calendar.Label)];
        var showFrom = rows.Any(row => row.Group == S.DockMenuShowFromTitle);
        return OptionLinks.Row(OptionLink.Accounts, OptionSlot.Subject) with
        {
            Title = S.DockWidgetMenuManageAccounts,
            Summary = names.Count > 0 ? string.Join(", ", names.Where(name => name.Length > 0)) : null,
            Under = showFrom ? OptionRows.GroupKey(S.DockMenuShowFromTitle) : null,
        };
    }

    /// <summary>A kind's name, as the Accounts page's cards name them.</summary>
    public static string KindName(OptionAccountKind kind) => kind switch
    {
        OptionAccountKind.Microsoft => S.SettingsAccountsMicrosoftHeader,
        OptionAccountKind.Google => S.SettingsAccountsGoogleHeader,
        OptionAccountKind.Mail => S.SettingsAccountsImapHeader,
        _ => S.SettingsAccountsDavHeader,
    };

    /// <summary>§411 (R8): the way to add a kind: "Add a Microsoft account", "Add a Google account", "Add a mail account", "Add a CalDAV account".</summary>
    public static string AddName(OptionAccountKind kind) => kind switch
    {
        OptionAccountKind.Microsoft => S.SettingsAccountsAddMicrosoft,
        OptionAccountKind.Google => S.SettingsAccountsAddGoogle,
        OptionAccountKind.Mail => S.SettingsAccountsAddMail,
        _ => S.SettingsAccountsAddDav,
    };

    /// <summary>A kind's icon: §687 a Google account the globe, as no logo is drawn by hand.</summary>
    public static string GlyphOf(OptionAccountKind kind) => kind switch
    {
        OptionAccountKind.Microsoft => OptionGlyphs.Contact,
        OptionAccountKind.Google => OptionGlyphs.Globe,
        OptionAccountKind.Mail => OptionGlyphs.Mail,
        _ => OptionGlyphs.People,
    };

    /// <summary>A kind's line under its name in "Add an account", as the page's cards say it.</summary>
    public static string KindDescription(OptionAccountKind kind) => kind switch
    {
        OptionAccountKind.Microsoft => S.SettingsAccountsMicrosoftDescription,
        OptionAccountKind.Google => S.SettingsAccountsGoogleDescription,
        OptionAccountKind.Mail => S.SettingsAccountsImapDescription,
        _ => S.SettingsAccountsDavDescription,
    };

    /// <summary>
    /// §535: the Accounts page: under "Your accounts" each account by its name, its address under it (§445; one that gave no
    /// name by its address, its kind under it), what is synced of it its own rows (§338), signed in to again where it is a
    /// Microsoft account, and removed, asked first; "No account yet" for none. Then under "Add an account" a way to add each
    /// kind (§411, R1 and R9: "Add a mail account" is its form, its fields on it).
    /// </summary>
    public static IReadOnlyList<OptionRow> Page(OptionAccounts accounts)
    {
        const OptionSlot slot = OptionSlot.Subject;
        var mine = accounts.All;
        var things = mine.Select(account => new OptionThing(account.Shown, GlyphOf(account.Kind))
        {
            // §445: by its name, its address under it; by its address, its kind.
            Summary = account.Shown == account.Address ? KindName(account.Kind) : account.Address,
            // §638 (the person, 2026-11-09: "the accounts when collapsed should show the amount of items totally synced"):
            // at its head's end while it is shut.
            Holds = account.Holds,
            Rows = Parts(account, accounts),
            Acts = InBrowser(account.Kind)
                ?
                [
                    new OptionRow(Again + account.Id, OptionRowKind.Action, S.DockMenuAccountsSignInAgain)
                    {
                        Glyph = OptionGlyphs.SignIn,
                        Slot = slot,
                        Set = _ => Done(() => accounts.SignInAgain(account.Id)),
                    },
                ]
                : [],
            Delete = new OptionRow(Remove + account.Id, OptionRowKind.Action, S.SettingsCommonRemove)
            {
                Glyph = OptionGlyphs.Delete,
                Slot = slot,
                Ask = new DeleteQuestion(S.SettingsAccountsRemoveTitle(account.Address), S.SettingsAccountsRemoveBody, S.SettingsCommonRemove, S.DockDeleteCancel, 0),
                Set = _ => Done(() => accounts.Remove(account.Id)),
            },
        }).ToList();
        return
        [
            new OptionRow(YoursHeading, OptionRowKind.Heading, S.SettingsAccountsYours) { Slot = slot },
            mine.Count == 0
                ? new OptionRow(Accounts, OptionRowKind.Note, S.SettingsAccountsEmpty) { Glyph = OptionGlyphs.Accounts, Slot = slot }
                : new OptionRow(Accounts, OptionRowKind.Things, S.SettingsNavAccounts)
                {
                    Glyph = OptionGlyphs.Accounts,
                    Slot = slot,
                    Inline = true,
                    Owned = new OptionOwned(null, things),
                },
            new OptionRow(AddHeading, OptionRowKind.Heading, S.SettingsAccountsAddHeader) { Slot = slot },
            .. Enum.GetValues<OptionAccountKind>().Select(kind => KindRow(kind, accounts)),
        ];
    }

    /// <summary>A kind signed in to in the browser — Microsoft, §687 Google —, signed in to again from its row.</summary>
    public static bool InBrowser(OptionAccountKind kind) => kind is OptionAccountKind.Microsoft or OptionAccountKind.Google;

    /// <summary>The provider a kind signed in to in the browser signs in at, by its name ("Microsoft", "Google").</summary>
    private static string Provider(OptionAccountKind kind) => kind == OptionAccountKind.Google ? "Google" : "Microsoft";

    /// <summary>§687: what a kind's Reconnect says: "Reconnect to sync calendars", … tasks.</summary>
    public static string ReconnectName(AccountShows kind) => kind switch
    {
        AccountShows.Calendars => S.CoreAccountsReconnectCalendars,
        AccountShows.Contacts => S.CoreAccountsReconnectContacts,
        AccountShows.Mail => S.CoreAccountsReconnectMail,
        _ => S.CoreAccountsReconnectTasks,
    };

    /// <summary>
    /// §338: a kind in "Add an account": a Microsoft or §687 Google account signed in to in the browser (greyed where
    /// this Karpo has no client there), a mail server's and a CalDAV server's form (<see cref="FormRows"/>), its line under it.
    /// </summary>
    private static OptionRow KindRow(OptionAccountKind kind, OptionAccounts accounts) =>
        accounts.Forms.Of(kind) is { } form
            ? new OptionRow(Form + kind, OptionRowKind.Level, AddName(kind))
            {
                Glyph = GlyphOf(kind),
                Slot = OptionSlot.Subject,
                Detail = KindDescription(kind),
                Level = FormRows(form, accounts.Forms),
                Form = true,
            }
            : new OptionRow(Add + kind, OptionRowKind.Action, AddName(kind))
            {
                Glyph = GlyphOf(kind),
                Slot = OptionSlot.Subject,
                Detail = KindDescription(kind),
                Enabled = accounts.CanSignIn(kind),
                Why = accounts.CanSignIn(kind) ? null : S.SettingsAccountsNoRegistration(Provider(kind)),
                Set = _ => Done(() => accounts.SignIn(kind)),
            };

    /// <summary>
    /// §338: <paramref name="form"/>'s rows (AccountForm.cs): the address, the password masked, "Set the
    /// server myself" and while it is set the server (a mail server's port and security too) and the user
    /// name; then Test connection and Add, greyed with what is missing under them, or what they are doing,
    /// how the last one went under it. Every field greyed while a try is on its way. §415: a field's name is
    /// its box's placeholder (§411) and what it needs to know its <see cref="OptionRow.Detail"/>, under the box.
    /// </summary>
    public static IReadOnlyList<OptionRow> FormRows(AccountForm form, AccountForms forms)
    {
        var key = Form + form.Kind + ".";
        var mail = form.Kind == OptionAccountKind.Mail;
        var busy = form.Busy;
        OptionRow Line(string field, string title, string glyph, string text, string? hint, Func<string, bool> set) =>
            new(key + field, OptionRowKind.Text, title)
            {
                Glyph = glyph,
                Slot = OptionSlot.Subject,
                Field = OptionField.Text,
                Text = text,
                Detail = hint,
                Enabled = !busy,
                SetText = set,
            };
        List<OptionRow> rows =
        [
            Line("Address", S.SettingsAccountsEmailHeader, OptionGlyphs.Mail, form.Address, mail ? null : S.SettingsAccountsDavEmailDescription, form.SetAddress),
            Line("Password", S.SettingsAccountsPasswordHeader, OptionGlyphs.Lock, "", mail ? S.SettingsAccountsImapPasswordDescription : S.SettingsAccountsDavPasswordDescription, form.SetPassword) with
            {
                Masked = true,
                Summary = form.HasPassword ? Bullets : null,
            },
            new(key + "Own", OptionRowKind.Toggle, S.SettingsAccountsOwnServer)
            {
                Glyph = OptionGlyphs.Settings,
                Slot = OptionSlot.Subject,
                On = form.OwnServer,
                Set = on => form.SetOwnServer(on == 1),
            },
        ];
        if (form.OwnServer)
        {
            rows.Add(Line("Server", S.SettingsAccountsServerHeader, OptionGlyphs.Network, form.Server, mail ? S.SettingsAccountsImapServerDescription : S.SettingsAccountsDavServerDescription, form.SetServer));
            if (mail)
            {
                rows.Add(Line("Port", S.DockAccountsPort, OptionGlyphs.Calculator, form.Port, null, form.SetPort) with { Placeholder = form.UsualPort.ToString(System.Globalization.CultureInfo.InvariantCulture) });
                rows.Add(new OptionRow(key + "Security", OptionRowKind.Choice, S.DockAccountsSecurity)
                {
                    Glyph = OptionGlyphs.Shield,
                    Slot = OptionSlot.Subject,
                    // Protocols' names, the same in every language.
                    Choices = ["SSL/TLS", "STARTTLS"],
                    Chosen = (int)form.Security,
                    Set = at => at is >= 0 and <= 1 && form.SetSecurity((AccountSecurity)at),
                });
            }
            rows.Add(Line("User", S.SettingsAccountsUserHeader, OptionGlyphs.Contact, form.User, S.SettingsAccountsUserDescription, form.SetUser));
        }
        // Greyed with what is missing, or what it is doing; how the last try went under the one it was.
        var problem = form.Problem;
        var doing = busy ? (mail ? S.SettingsAccountsImapLooking(form.Address) : S.SettingsAccountsDavLooking) : null;
        OptionRow Act(string field, string title, string glyph, bool add) => new(key + field, OptionRowKind.Action, title)
        {
            Glyph = glyph,
            Slot = OptionSlot.Subject,
            Enabled = !busy && problem is null,
            Why = busy ? (form.Added == add ? doing : null) : problem,
            Detail = form.Outcome is { } outcome && form.Added == add ? outcome : null,
            Set = _ => Done(() => forms.Try(form, add)),
        };
        rows.Add(new OptionRow(key + "Rule", OptionRowKind.Heading, ""));
        rows.Add(Act("Test", S.DockAccountsTest, OptionGlyphs.Connect, add: false));
        rows.Add(Act("Add", S.SettingsCommonAdd, OptionGlyphs.New, add: true));
        return rows;
    }

    /// <summary>
    /// §338: what is synced of <paramref name="account"/>, its submenu: per kind its heading, what is said
    /// under it, a switch per part with how many it holds at its end (§453), "Reconnect to sync tasks"
    /// where To Do was never allowed (§687: any kind the person never let Karpo at, in its own words). §452: a part that wears a colour has it on its switch's row
    /// (<see cref="OptionRow.Colour"/>); §338 to §448 had the colours a level apart, "Colors ›" under the parts.
    /// None for an account that lists nothing.
    /// </summary>
    private static IReadOnlyList<OptionRow> Parts(OptionAccount account, OptionAccounts accounts)
    {
        const OptionSlot slot = OptionSlot.Subject;
        List<OptionRow> rows = [];
        foreach (var group in account.Groups)
        {
            // §685 (the person, 2026-11-12: "for the accounts it should be more obivous / clearer what belongs to email, calendar,
            // etc."): its heading its kind's glyph, so it stands in the account's card as the head of the rows under it.
            rows.Add(new OptionRow(Part + account.Id + "|#" + group.Title, OptionRowKind.Heading, group.Title) { Slot = slot, Glyph = group.Glyph });
            if (group.Note is { } note)
            {
                rows.Add(new OptionRow(Part + account.Id + "|!" + group.Title, OptionRowKind.Action, note) { Glyph = OptionGlyphs.Warning, Slot = slot, Enabled = false });
            }
            if (group.Reconnect)
            {
                rows.Add(new OptionRow(Reconnect + account.Id, OptionRowKind.Action, ReconnectName(group.Kind))
                {
                    Glyph = OptionGlyphs.SignIn,
                    Slot = slot,
                    Set = _ => Done(() => accounts.SignInAgain(account.Id)),
                });
            }
            List<OptionRow> switches = [.. group.Parts.Select(part => Switch(account, part, accounts))];
            // §646: the mail's parts on their page, Mail folders, its link in their place — the link's own level is the page,
            // so a pick there finds its switch here (OptionRows.Flat). §639 to §645 every folder's switch stood here, making an
            // account's expander as long as its mailbox has folders.
            rows.AddRange(group.Folders is { } said
                ? [OptionLinks.Row(OptionLink.MailFolders, slot) with { Key = Folders + account.Id, Summary = said, Level = switches }]
                : switches);
        }
        return rows;
    }

    /// <summary>
    /// §338: one of <paramref name="account"/>'s parts, its switch, how many it holds at its end (§453), §452 its colour on its
    /// row where it wears one.
    /// </summary>
    private static OptionRow Switch(OptionAccount account, OptionAccountPart part, OptionAccounts accounts)
    {
        const OptionSlot slot = OptionSlot.Subject;
        var id = account.Id + "|" + part.Key;
        return new OptionRow(Part + id, OptionRowKind.Toggle, part.Name)
        {
            Glyph = part.Glyph,
            Slot = slot,
            On = part.On,
            // §453: its count at its end, before its switch; a second line under its name made every part a tall row.
            Summary = part.Detail,
            Set = on => Done(() => accounts.Sync(account.Id, part.Key, on == 1)),
            Colour = part.Coloured
                ? PartColours.Row(Colour + id, S.SettingsAccountsColorHeader, part.Colour, part.Automatic, picked => Done(() => accounts.Recolour(account.Id, part.Key, picked))) with { Slot = slot }
                : null,
        };
    }

    /// <summary>
    /// §646: the title of the Mail folders page its link keyed <paramref name="key"/> (<see cref="Folders"/>) leads to, "Mail
    /// folders · Ann Smith": its account by what it is listed by; null where none of <paramref name="accounts"/> has it (gone since).
    /// </summary>
    public static string? FoldersTitle(OptionAccounts accounts, string key) =>
        accounts.All.FirstOrDefault(account => Folders + account.Id == key) is { } account
            ? S.SettingsAccountsSyncPair(first: S.SettingsAccountsMailFolders, second: account.Shown)
            : null;

    /// <summary><paramref name="act"/> done: nothing of the widget's own changed.</summary>
    private static bool Done(Action act)
    {
        act();
        return false;
    }
}
