// accounts: the parts a widget's "Show from" offers (Karpo.Core\WidgetSources.cs, docs\widgets.md §455): every
// account's calendars, address books or mail folders as the dock last synced them (SyncReport) — never the network,
// as a to-do line's lists (TodoSources) — each under its account's line (§445, §644). A calendar or a book turned off on
// the Accounts page is not offered: nothing of it is synced to show. §644: a mail account's every folder is, its inbox
// first, then every other folder as its last read listed them: one not synced says so, and picking it turns its sync on
// (accounts.json "on", as the Accounts page's switch), "Syncing…" until a read has it.

using Karpo.Core;

namespace Karpo.Accounts;

public static class SourceParts
{
    /// <summary>
    /// What a widget showing <paramref name="kind"/> may show from, of <paramref name="accounts"/> in
    /// <paramref name="report"/>, in the accounts' order and each account's: a calendar's or a book's every part, the
    /// mail's inbox first (shown where nothing is picked), then every other folder the account listed (§639, §644: shown
    /// where picked; one not synced turned on by <paramref name="syncOn"/>, given the account's id and the part's key, as
    /// the Accounts page's switch). Each names its account's address under the account's name where that is another, and a
    /// mail account the colour its rows wear (its inbox's).
    /// </summary>
    public static IReadOnlyList<SourcePart> Of(IReadOnlyCollection<Account> accounts, SyncReportFile report, AccountShows kind, Action<string, string>? syncOn = null)
    {
        var data = kind switch
        {
            AccountShows.Calendars => AccountData.Calendars,
            AccountShows.Contacts => AccountData.Contacts,
            AccountShows.Mail => AccountData.Mail,
            _ => AccountData.None,
        };
        List<SourcePart> parts = [];
        if (data == AccountData.None)
        {
            return parts;
        }
        var names = AccountNames.Of(accounts);
        var giving = accounts.Where(account => account.Gives.HasFlag(data)).ToList();
        // The colour a mail account's rows wear on the bar: its inbox's, as the mail store paints them (MailStore.Painted).
        var inboxes = data == AccountData.Mail ? SourceColours.Of(giving.Select(account => (account, SyncKeys.Inbox, (string?)null))) : null;
        foreach (var account in giving)
        {
            var name = names[account.Id];
            if (report.For(account.Id)?.Of(data) is not { } part)
            {
                continue;
            }
            SourcePart Part(string key, string shown, bool read, bool byDefault = true)
            {
                var synced = account.Syncs(key);
                return new SourcePart(WidgetSources.Key(account.Address, key), shown, name, byDefault)
                {
                    GroupDetail = string.Equals(name, account.Address, StringComparison.OrdinalIgnoreCase) ? null : account.Address,
                    GroupColour = inboxes?[SourceColours.Key(account.Id, SyncKeys.Inbox)].Colour,
                    Sync = !synced ? SourceSync.Off : read ? SourceSync.Synced : SourceSync.Reading,
                    SyncOn = !synced && syncOn is not null ? () => syncOn(account.Id, key) : null,
                };
            }
            if (data == AccountData.Mail)
            {
                // The inbox, then every other folder listed; one off in the report while synced not read since it was turned on.
                if (part.Items.FirstOrDefault() is { } inbox)
                {
                    parts.Add(Part(SyncKeys.Inbox, S.CoreAccountsInbox, !inbox.Off));
                }
                parts.AddRange((part.Folders ?? []).Select(folder => Part(SyncKeys.MailFolder(folder.Id), folder.Name, !folder.Off, byDefault: false)));
                continue;
            }
            foreach (var item in part.Items)
            {
                var key = data == AccountData.Calendars ? SyncKeys.Calendar(item.Id) : SyncKeys.Book(item.Id);
                if (!item.Off && account.Syncs(key))
                {
                    parts.Add(Part(key, item.Name, read: true));
                }
            }
        }
        return parts;
    }

    /// <summary>
    /// What this PC's accounts offer a widget showing <paramref name="kind"/>, as the dock last reported them; a part not
    /// synced turned on in accounts.json when picked (<see cref="AccountBook.SetSynced(string, string, bool)"/>), as the
    /// Accounts page's switch does.
    /// </summary>
    public static IReadOnlyList<SourcePart> Here(AccountShows kind) =>
        Of(AccountBook.Load(), (SyncReport.ForDock() ?? SyncReport.Here).Read(), kind, (account, key) => AccountBook.SetSynced(account, key, true));
}
