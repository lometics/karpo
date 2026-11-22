// A Microsoft account read through Graph: calendars through calendarView,
// contacts from every contact folder, and the inbox. An event is read with what
// the next meeting needs (docs\widgets.md §205): its online meeting's link, where
// it is shown as free and whether the person declined it — all under the
// Calendars.Read Karpo always asked for.
//
// calendarView expands a series into its occurrences with their exceptions
// applied, so nothing here knows what a recurrence is. Times come back in UTC
// (Prefer: outlook.timezone="UTC") so the dock converts them to whichever zone
// it draws in; an all-day event is a date, the same day wherever it is read.
//
// A contact's picture is its own request (/me/contacts/{id}/photo/$value, under
// the Contacts.Read Karpo always asked for): a contact says nothing of whether it
// has one, so its changeKey stands for its picture's version (Person.Photo) and
// the dock asks once per version, a 404 being "none".

using System.Globalization;
using System.Text.Json.Nodes;

namespace Karpo.Accounts.Microsoft;

internal sealed class MicrosoftReader : IAccountReader, ITaskReader
{
    private const string Utc = "outlook.timezone=\"UTC\"";

    /// <summary>The id of the default contact folder, which /me/contacts reads and Graph lists with no id of its own.</summary>
    internal const string DefaultBook = "default";

    private readonly Graph graph;
    private readonly MicrosoftTodo todo;

    public MicrosoftReader(Account account, Graph? graph = null, Graph? tasks = null)
    {
        Account = account;
        this.graph = graph ?? new Graph(Web.Http, cancellation => MicrosoftAuth.Shared.TokenAsync(account, cancellation));
        todo = tasks is null ? new MicrosoftTodo(account) : new MicrosoftTodo(account, tasks);
    }

    public Account Account { get; }

    // To Do, on the tasks' own token (MicrosoftTodo).
    public Task<IReadOnlyList<RemoteTaskList>> TaskListsAsync(CancellationToken cancellation) => todo.TaskListsAsync(cancellation);

    public Task<IReadOnlyList<RemoteTask>> TasksAsync(string list, CancellationToken cancellation) => todo.TasksAsync(list, cancellation);

    public Task<RemoteTask> AddTaskAsync(string list, string title, bool done, CancellationToken cancellation) => todo.AddTaskAsync(list, title, done, cancellation);

    public Task<RemoteTask?> UpdateTaskAsync(string list, string task, TaskEdit edit, string? etag, CancellationToken cancellation) =>
        todo.UpdateTaskAsync(list, task, edit, etag, cancellation);

    public Task DeleteTaskAsync(string list, string task, string? etag, CancellationToken cancellation) => todo.DeleteTaskAsync(list, task, etag, cancellation);

    public Task<RemoteTask?> TaskAsync(string list, string task, CancellationToken cancellation) => todo.TaskAsync(list, task, cancellation);

    public Task RenameListAsync(string list, string name, CancellationToken cancellation) => todo.RenameListAsync(list, name, cancellation);

    public Task DeleteListAsync(string list, CancellationToken cancellation) => todo.DeleteListAsync(list, cancellation);

    public async Task<IReadOnlyList<CalendarRead>> CalendarsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellation)
    {
        var calendars = await graph.AllAsync(
            $"{Graph.V1}/me/calendars?$select=id,name,color,hexColor&$top=100", cancellation).ConfigureAwait(false);

        var found = new List<CalendarRead>();
        AccountException? first = null;
        foreach (var item in calendars)
        {
            if (Calendar(item) is not { } calendar)
            {
                continue;
            }
            if (!Account.Syncs(SyncKeys.Calendar(calendar.Id)))
            {
                found.Add(new CalendarRead(calendar, [], [], Off: true));
                continue;
            }
            var url = $"{Graph.V1}/me/calendars/{Uri.EscapeDataString(calendar.Id)}/calendarView"
                + $"?startDateTime={Stamp(fromUtc)}&endDateTime={Stamp(toUtc)}"
                + $"&$select={EventFields}&$top=250";
            try
            {
                var events = await graph.AllAsync(url, cancellation, Utc).ConfigureAwait(false);
                found.Add(new CalendarRead(calendar, [.. events.Select(Event).OfType<Occurrence>()], []) { Incomplete = events.More });
            }
            catch (AccountException refused)
            {
                // A shared calendar its owner stopped sharing (403), one gone since it was listed (404), a
                // server error for it: it fails alone, and the account's other calendars are read (Parts).
                first ??= refused;
                found.Add(new CalendarRead(calendar, [], []) { Failed = refused.Message });
            }
        }
        Parts.Check(found, read => read.Off, read => read.Failed is not null, first);
        return found;
    }

    public async Task<IReadOnlyList<BookRead>> ContactsAsync(CancellationToken cancellation)
    {
        const string Select = "$select=id,displayName,emailAddresses,businessPhones,homePhones,mobilePhone,companyName,changeKey&$top=500";

        // /me/contacts is the default folder only; the others are read one by one.
        var books = new List<BookRead>();
        AccountException? first = null;
        var main = new AddressBook(DefaultBook, S.CoreAccountsDefaultBook);
        books.Add(await BookAsync(main, $"{Graph.V1}/me/contacts?{Select}").ConfigureAwait(false));
        var folders = await graph.AllAsync($"{Graph.V1}/me/contactFolders?$select=id,displayName&$top=100", cancellation).ConfigureAwait(false);
        foreach (var folder in folders)
        {
            if (Web.Str(folder["id"]) is { Length: > 0 } id)
            {
                var name = Web.Str(folder["displayName"]);
                var book = new AddressBook(id, string.IsNullOrWhiteSpace(name) ? S.CoreAccountsDefaultBook : name);
                books.Add(await BookAsync(book, $"{Graph.V1}/me/contactFolders/{Uri.EscapeDataString(id)}/contacts?{Select}").ConfigureAwait(false));
            }
        }
        Parts.Check(books, read => read.Off, read => read.Failed is not null, first);
        return books;

        async Task<BookRead> BookAsync(AddressBook book, string url)
        {
            if (!Account.Syncs(SyncKeys.Book(book.Id)))
            {
                return new BookRead(book, [], Off: true);
            }
            try
            {
                var people = await graph.AllAsync(url, cancellation).ConfigureAwait(false);
                return new BookRead(book, [.. people.Select(Contact).OfType<Person>().DistinctBy(person => person.Id)]) { Incomplete = people.More };
            }
            catch (AccountException refused)
            {
                // A folder Graph will not give fails alone, as a calendar does (Parts).
                first ??= refused;
                return new BookRead(book, []) { Failed = refused.Message };
            }
        }
    }

    /// <summary>A contact's picture as Graph keeps it, whatever folder holds the contact; null where it has none (404).</summary>
    public Task<byte[]?> PhotoAsync(Person person, CancellationToken cancellation) =>
        graph.BytesAsync($"{Graph.V1}/me/contacts/{Uri.EscapeDataString(person.Id)}/photo/$value", cancellation);

    /// <summary>The most mail folders listed (§455, §639), and how deep in each other: a mailbox of thousands lists its first.</summary>
    internal const int MostFolders = 500;

    private const int DeepestFolder = 4;

    private const string FolderFields = "$select=id,displayName,childFolderCount,unreadItemCount,totalItemCount&$top=100";

    public async Task<IReadOnlyList<FolderRead>> MailAsync(int count, CancellationToken cancellation)
    {
        // The inbox by its well-known name, which tells its id among the folders listed; its messages unless turned off.
        var inbox = await graph.GetAsync($"{Graph.V1}/me/mailFolders/inbox?$select=id,unreadItemCount,totalItemCount", cancellation).ConfigureAwait(false);
        var inboxId = Web.Str(inbox["id"]) is { Length: > 0 } id ? id : "inbox";
        List<FolderRead> found =
        [
            new(new MailFolder(inboxId, S.CoreAccountsInbox, Inbox: true, Web.Int(inbox["unreadItemCount"]), Web.Int(inbox["totalItemCount"])),
                Account.Syncs(SyncKeys.Inbox) ? await MessagesAsync("inbox", inbox).ConfigureAwait(false) : null),
        ];
        // §639: every other folder, a folder's folders after it, with the counts the list gives; read where the person
        // turned it on on the Accounts page.
        var listed = new List<(JsonObject Item, string Name)>();
        await ListAsync($"{Graph.V1}/me/mailFolders?{FolderFields}", null, 0).ConfigureAwait(false);
        foreach (var (item, name) in listed)
        {
            var folder = new MailFolder(Web.Str(item["id"])!, name, Unread: Web.Int(item["unreadItemCount"]), Total: Web.Int(item["totalItemCount"]));
            if (folder.Id == inboxId)
            {
                continue;
            }
            if (!Account.Syncs(SyncKeys.MailFolder(folder.Id)))
            {
                found.Add(new FolderRead(folder, null));
                continue;
            }
            try
            {
                found.Add(new FolderRead(folder, await MessagesAsync(folder.Id, item).ConfigureAwait(false)));
            }
            catch (AccountException refused)
            {
                // A folder Graph will not give fails alone, as a calendar does (Parts).
                found.Add(new FolderRead(folder, null) { Failed = refused.Message });
            }
        }
        return found;

        async Task ListAsync(string url, string? parent, int depth)
        {
            foreach (var item in await graph.AllAsync(url, cancellation, most: MostFolders).ConfigureAwait(false))
            {
                if (listed.Count >= MostFolders || Web.Str(item["id"]) is not { Length: > 0 } id)
                {
                    continue;
                }
                var own = Web.Str(item["displayName"]) is { Length: > 0 } said ? said : id;
                var name = id == inboxId ? S.CoreAccountsInbox : own;
                name = parent is null ? name : $"{parent} / {name}";
                listed.Add((item, name));
                if (Web.Int(item["childFolderCount"]) > 0 && depth + 1 < DeepestFolder)
                {
                    await ListAsync($"{Graph.V1}/me/mailFolders/{Uri.EscapeDataString(id)}/childFolders?{FolderFields}", name, depth + 1).ConfigureAwait(false);
                }
            }
        }

        // A folder's newest messages, by its id or well-known name, and its counts as Graph listed them.
        async Task<Inbox> MessagesAsync(string folder, JsonObject counts)
        {
            var page = await graph.GetAsync(
                $"{Graph.V1}/me/mailFolders/{Uri.EscapeDataString(folder)}/messages?$top={count.ToString(CultureInfo.InvariantCulture)}"
                + "&$select=id,subject,from,receivedDateTime,isRead,bodyPreview,webLink&$orderby=receivedDateTime desc",
                cancellation).ConfigureAwait(false);
            return new Inbox(
                Web.Int(counts["unreadItemCount"]) ?? 0,
                [.. Web.Items(page, "value").Select(Message).OfType<MailItem>()],
                Web.Int(counts["totalItemCount"]));
        }
    }

    /// <summary>One calendar as Graph lists it.</summary>
    internal static RemoteCalendar? Calendar(JsonObject item)
    {
        if (Web.Str(item["id"]) is not { Length: > 0 } id)
        {
            return null;
        }
        var name = Web.Str(item["name"]);
        var colour = Web.Str(item["hexColor"]) is { Length: 7 } hex && hex[0] == '#'
            ? hex
            : Named(Web.Str(item["color"]));
        return new RemoteCalendar(id, string.IsNullOrWhiteSpace(name) ? S.CoreAccountsUnnamedCalendar : name, colour);
    }

    /// <summary>
    /// Outlook's named calendar colours, where the calendar has no hex colour of its own: close to
    /// the swatches Outlook shows, a shade darker so a dot reads on a light bar. "auto" is none.
    /// </summary>
    internal static string? Named(string? colour) => colour switch
    {
        "lightBlue" => "#3A96DD",
        "lightGreen" => "#498205",
        "lightOrange" => "#CA5010",
        "lightGray" => "#7A7574",
        "lightYellow" => "#C19C00",
        "lightTeal" => "#038387",
        "lightPink" => "#E3008C",
        "lightBrown" => "#8E562E",
        "lightRed" => "#D13438",
        _ => null,
    };

    /// <summary>What calendarView is asked for of each occurrence: all <see cref="Event"/> reads.</summary>
    internal const string EventFields = "subject,location,start,end,isAllDay,isCancelled,isOnlineMeeting,onlineMeeting,onlineMeetingUrl,responseStatus,showAs,bodyPreview,webLink,originalStartTimeZone,originalEndTimeZone";

    /// <summary>One occurrence from calendarView; null for a cancelled one, or one with no start.</summary>
    internal static Occurrence? Event(JsonObject item)
    {
        if (Web.Bool(item["isCancelled"]) == true)
        {
            return null;
        }
        var allDay = Web.Bool(item["isAllDay"]) ?? false;
        if (Time(item["start"], allDay, Web.Str(item["originalStartTimeZone"])) is not { } start)
        {
            return null;
        }
        var end = Time(item["end"], allDay, Web.Str(item["originalEndTimeZone"]) ?? Web.Str(item["originalStartTimeZone"])) ?? start;
        if (end < start)
        {
            end = start;
        }
        var title = Web.Str(item["subject"]);
        var location = Web.Str(item["location"]?["displayName"]);
        // Its meeting's link: Teams' own join link, an add-in's (Zoom's, Webex's) where it set one, else
        // one in its place or in the first lines of its notes — kept as the link alone, never the notes.
        var join = MeetingLinks.Find(
            Web.Str(item["onlineMeeting"]?["joinUrl"]),
            Web.Str(item["onlineMeetingUrl"]),
            location,
            Web.Str(item["bodyPreview"]));
        // Shown as free, or declined: none of the person's time, which the next meeting passes by.
        var free = Web.Str(item["showAs"]) is "free"
            || Web.Str(item["responseStatus"]?["response"]) is "declined";
        return new Occurrence(
            string.IsNullOrWhiteSpace(title) ? S.DockCalendarNoTitle : title,
            string.IsNullOrWhiteSpace(location) ? null : location,
            allDay,
            false,
            start,
            end,
            join?.Url,
            free,
            // Its page in Outlook on the web, where a click on it opens (§434).
            MailLinks.Web(Web.Str(item["webLink"])));
    }

    /// <summary>
    /// A dateTimeTimeZone as UTC — which it is, having been asked for in UTC — or as a date for an
    /// all-day event (<see cref="Day"/>).
    /// </summary>
    /// <param name="zone">The zone the event was made in (originalStartTimeZone), which an all-day event's midnight is of.</param>
    private static DateTime? Time(JsonNode? value, bool allDay, string? zone)
    {
        if (Web.Str(value?["dateTime"]) is not { Length: > 0 } text
            || !DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var when))
        {
            return null;
        }
        return allDay
            ? DateTime.SpecifyKind(Day(when, zone), DateTimeKind.Unspecified)
            : DateTime.SpecifyKind(when, DateTimeKind.Utc);
    }

    /// <summary>
    /// The day an all-day event's start or end is. Graph keeps it as midnight in the event's own zone, and
    /// answers it two ways under <c>Prefer: outlook.timezone="UTC"</c>: often that midnight as it is,
    /// "00:00" with "UTC" beside it, and for some events and mailboxes that midnight carried to UTC — an
    /// event of the 2nd made in Zurich comes as the 1st at 23:00, one made in Seattle as the 2nd at 08:00.
    /// Taking the date of that would put an event of a zone east of UTC a day early. So a midnight is
    /// the day it says; anything else is turned back into the zone the event was made in, where it is
    /// a midnight again; and where that zone is not one Windows knows, it is the nearest midnight —
    /// every zone is within twelve hours of UTC but for the few of Kiribati, Tonga and Samoa.
    /// </summary>
    internal static DateTime Day(DateTime utc, string? zone)
    {
        if (utc.TimeOfDay == TimeSpan.Zero)
        {
            return utc.Date;
        }
        if (MicrosoftTodo.Zone(zone) is { } made)
        {
            var wall = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), made);
            if (wall.TimeOfDay == TimeSpan.Zero)
            {
                return wall.Date;
            }
        }
        return utc.AddHours(12).Date;
    }

    /// <summary>One contact; null for one without an id.</summary>
    internal static Person? Contact(JsonObject item)
    {
        if (Web.Str(item["id"]) is not { Length: > 0 } id)
        {
            return null;
        }
        var emails = new List<string>();
        foreach (var address in Web.Items(item, "emailAddresses"))
        {
            if (Web.Str(address["address"]) is { Length: > 0 } email)
            {
                emails.Add(email);
            }
        }
        var phones = new List<string>();
        if (Web.Str(item["mobilePhone"]) is { Length: > 0 } mobile)
        {
            phones.Add(mobile);
        }
        foreach (var list in (string[])["businessPhones", "homePhones"])
        {
            foreach (var phone in item[list] as JsonArray ?? [])
            {
                if (Web.Str(phone) is { Length: > 0 } number)
                {
                    phones.Add(number);
                }
            }
        }
        var organization = Web.Str(item["companyName"]);
        var name = Web.Str(item["displayName"]);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = emails.FirstOrDefault() ?? phones.FirstOrDefault() ?? organization ?? S.CoreAccountsNoName;
        }
        // Any contact may have a picture: its version is the contact's (changeKey), which a new picture moves.
        return new Person(id, name.Trim(), string.IsNullOrWhiteSpace(organization) ? null : organization, emails, phones)
        {
            Photo = Web.Str(item["changeKey"]) is { Length: > 0 } version ? version : id,
        };
    }

    /// <summary>One message; null for one without an id.</summary>
    internal static MailItem? Message(JsonObject item)
    {
        if (Web.Str(item["id"]) is not { Length: > 0 } id)
        {
            return null;
        }
        var from = item["from"]?["emailAddress"];
        var address = Web.Str(from?["address"]);
        var name = Web.Str(from?["name"]);
        var subject = Web.Str(item["subject"]);
        var preview = Web.Str(item["bodyPreview"])?.Trim();
        var received = DateTimeOffset.TryParse(
            Web.Str(item["receivedDateTime"]), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)
            ? at
            : DateTimeOffset.MinValue;
        return new MailItem(
            id,
            string.IsNullOrWhiteSpace(name) ? address ?? S.CoreAccountsUnknownSender : name,
            address,
            string.IsNullOrWhiteSpace(subject) ? S.CoreAccountsNoSubject : subject,
            string.IsNullOrEmpty(preview) ? null : preview,
            received,
            Web.Bool(item["isRead"]) == false,
            // Outlook on the web's own address of the message, which opens it there.
            MailLinks.Web(Web.Str(item["webLink"])));
    }

    private static string Stamp(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
