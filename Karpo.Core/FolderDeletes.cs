// Deleting a folder (the person, 2026-10-11: "if there is no widget in a folder
// there should not be a confirmation on delete. the confirmation should be a system dialog that says
// that you will loose the widgets inside when you delete and should show how many widget you loose on
// the delete button").
//
// What is lost is counted in widgets: every line of the folder but a divider, which is a hairline
// between widgets and nothing of its own. A heading counts (its words are somebody's), and so does an
// actions footer (its buttons). Nothing lost, nothing asked: the folder goes at once.
// Else Windows' own question, a task dialog (the dock's TaskDialogs), whose words are made here.
// Several widgets removed together ask too, counted; one alone never (§253: the menu's "Remove" asked
// for every widget from 2026-10-11 until then).
// A folder's column has no delete any more (2026-10-12: "deleting a column should not even be
// possible anymore"), so it asks nothing.

namespace Karpo.Core;

/// <summary>
/// The question Windows' dialog asks before a delete loses widgets: its main instruction
/// (<paramref name="Title"/>), what it says under it (<paramref name="Content"/>), the button that
/// deletes (<paramref name="Delete"/>, with the count) and the one that does not
/// (<paramref name="Cancel"/>, the default); <paramref name="Widgets"/> lost.
/// </summary>
public sealed record DeleteQuestion(string Title, string Content, string Delete, string Cancel, int Widgets);

/// <summary>Whether a delete asks first, and what it asks. Pure.</summary>
public static class FolderDeletes
{
    /// <summary>Whether <paramref name="cell"/> is a widget a delete loses: anything but a divider.</summary>
    public static bool IsWidget(FolderCell? cell) =>
        cell?.Island is { } island && (island.List ?? ListWidgets.Find(island.Kind)?.Kind) != ListKind.Divider;

    /// <summary>The widgets of <paramref name="cells"/> (<see cref="IsWidget"/>).</summary>
    public static int Widgets(IEnumerable<FolderCell?>? cells) => cells?.Count(IsWidget) ?? 0;

    /// <summary>The widgets of every list of <paramref name="folder"/>.</summary>
    public static int Widgets(FolderOptions? folder) => folder is null ? 0 : folder.Lists.Sum(list => Widgets(list));

    /// <summary>
    /// What removing <paramref name="removed"/> together asks — the widgets taken off a bar or out of a
    /// folder, by the menu's "Remove" or the Delete key alike — or null where it goes without a
    /// question: one widget, or an empty folder (§253, the person: "if i just remove one widget there
    /// should not be a confirmation dialog, only when there are multiple ones"). A folder that loses
    /// widgets asks how many, the folders counted (one too: a folder has no name to ask by, §427); else
    /// several widgets are counted.
    /// </summary>
    public static DeleteQuestion? ForRemoving(IReadOnlyList<WidgetItem?> removed)
    {
        var folders = removed.OfType<BarItem>().Where(island => island.Folder is not null).ToList();
        var widgets = folders.Sum(island => Widgets(island.Folder));
        if (widgets > 0)
        {
            return Question(S.DockDeleteFoldersTitle(folders.Count),
                folders.Count == 1 ? S.DockDeleteFolderContent(widgets) : S.DockDeleteFoldersContent(widgets), widgets);
        }
        var several = removed.Count(island => island is not null);
        return several > 1 ? new(S.DockDeleteWidgetsTitle(several), S.DockDeleteWidgetsContent, S.DockDeleteRemove, S.DockDeleteCancel, several) : null;
    }

    private static DeleteQuestion Question(string title, string content, int widgets) =>
        new(title, content, S.DockDeleteButton(widgets), S.DockDeleteCancel, widgets);
}
