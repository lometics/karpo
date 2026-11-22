// notes: the accounts' task lists (To Do's, §687 Google Tasks') a to-do line may show beside this PC's notebook's, for its menu on the
// bar (Karpo.Core\OptionRows.Lines.cs, docs\widgets.md §219, §324): every account's To Do lists as the
// dock last read them (SyncReport) — never the network — or, before it has, the account's default list.
// A list turned off on the Accounts page is not offered.

using Karpo.Core;

namespace Karpo.Accounts;

public static class TodoSources
{
    /// <summary>
    /// The sources of <paramref name="accounts"/>' To Do lists in <paramref name="report"/>, each account's default list
    /// first, each with its account's name (§445, <see cref="AccountNames"/>).
    /// </summary>
    public static IReadOnlyList<TodoSource> Of(IReadOnlyCollection<Account> accounts, SyncReportFile report)
    {
        List<TodoSource> sources = [];
        var names = AccountNames.Of(accounts);
        foreach (var account in accounts.Where(account => account.Gives.HasFlag(AccountData.Tasks)))
        {
            var name = names[account.Id];
            var service = ConsentException.Service(account.Kind, AccountData.Tasks);
            var lists = report.For(account.Id)?.Tasks?.Items.Where(item => account.Syncs(SyncKeys.TaskList(item.Id))).ToList() ?? [];
            if (lists.Count == 0)
            {
                sources.Add(new(account.Address, null, S.WidgetTodoNewList, Default: true) { AccountName = name, Service = service });
                continue;
            }
            foreach (var item in lists.OrderByDescending(item => item.Default == true))
            {
                sources.Add(new(account.Address, item.Id, item.Name, item.Default == true) { AccountName = name, Service = service });
            }
        }
        return sources;
    }

    /// <summary>The sources this PC's accounts give, as the dock last reported them.</summary>
    public static IReadOnlyList<TodoSource> Here() => Of(AccountBook.Load(), (SyncReport.ForDock() ?? SyncReport.Here).Read());
}
