// edit, §682 (the person, 2026-11-12: "mail folders should actually be shown like a tree and not individually listed"): a
// mailbox's folders, named by their paths, as a tree on the Mail folders page.

using Karpo.Dock;

namespace Karpo.Tests.Edit;

public sealed class MailTreeTests
{
    /// <summary>Each under its nearest parent listed, a step further in, by its own name; the rest of its path where its parent is not listed.</summary>
    [Fact]
    public void EachFolderStandsUnderItsParentByItsOwnName()
    {
        var tree = MailTree.Of(["Inbox", "Inbox / Projects", "Inbox / Projects / 2026", "Archive", "Archive / 2025", "Lists / Rust / Announce", "Sent"]);
        Assert.Equal(
            [(0, "Inbox"), (1, "Projects"), (2, "2026"), (0, "Archive"), (1, "2025"), (0, "Lists / Rust / Announce"), (0, "Sent")],
            tree);
    }

    /// <summary>A parent missing between (not listed): under the nearest that is, the rest of the path its name.</summary>
    [Fact]
    public void AMissingParentIsSkippedToTheNearestListed()
    {
        Assert.Equal([(0, "Work"), (1, "Clients / Acme")], MailTree.Of(["Work", "Work / Clients / Acme"]));
    }

    /// <summary>The page's switches: renamed and stepped in; headings and rules as they are.</summary>
    [Fact]
    public void ThePagesSwitchesAreTheTree()
    {
        List<FlyoutItem> items =
        [
            new(0, "Mail", Header: true),
            new(1, "Inbox", Control: new FlyoutControl.Toggle(true)),
            new(2, "Inbox / Receipts", Control: new FlyoutControl.Toggle(false)),
        ];
        var shown = MailTree.Shown(items);
        Assert.Equal([("Mail", 0), ("Inbox", 0), ("Receipts", 1)], shown.Select(item => (item.Text, item.Indent)));
        Assert.True(shown[0].Header);
    }
}
