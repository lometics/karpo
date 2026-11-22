// The calendars, the inboxes, the address books and To Do's tasks of the person's
// accounts, read from the dock's start for as long as it runs, whether or not any
// widget shows them (docs\accounts.md, "When it reads").
//
// The accounts decide, not the config: every account that gives calendars, mail,
// contacts or tasks (accounts.json, AccountSet) is synced at the online interval,
// so a widget placed later, a folder opened or a start without a connection has
// the events, the messages and the people at once, from memory or the cache. Each
// store is the one sync of its kind — one per account, a minute's rest after a
// failure, a read at once when the accounts change — and stops its timer while no
// account gives it anything; an account removed stops at the next read, and what
// was kept of it goes with it. No widget starts a sync of its own: a widget reads
// the store, and at most asks it for what is due.
//
// Not on an isolated run (a test, --smoke): the dock's stores there reach no
// account anyway, and the start is the person's own.

namespace Karpo.Dock.Pim;

internal static class PimSync
{
    /// <summary>On the dock's thread, at its start: the dock's stores read from now on.</summary>
    public static void Start() => Start(CalendarStore.Shared, MailStore.Shared, TaskStore.Shared, ContactStore.Shared);

    /// <summary>
    /// Every store given reads its accounts from now on, at its own cadence, for good. On the
    /// thread their news is to be raised on.
    /// </summary>
    internal static void Start(CalendarStore calendars, MailStore mail, TaskStore? tasks = null, ContactStore? contacts = null)
    {
        // The calendars' reading, once opened, goes on by its own timer and on every account change.
        calendars.Open();
        // The mail's, the tasks' and the contacts' go on while the dock keeps them: for its life.
        mail.Keep();
        // To Do's so a change made on a line goes the moment the account answers.
        tasks?.Keep();
        contacts?.Keep();
    }
}
