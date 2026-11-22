// §652: what Windows search's hits of Karpo's own open, once the folder has gone (Dock.Folders.cs, FolderForward): a page or a
// row of Karpo's settings in edit mode (the one way in from outside, OpenEditLanding), a widget to add on the add page with
// its name in the page's search, a layout this display switches to (as the layouts' line does), a note in the folder on this
// bar that shows it — and where none does, the add page at the note widget, to put one somewhere.

using Karpo.Core;
using Karpo.Dock.WinSearch;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>§652: <paramref name="target"/>, a hit of Karpo's own Windows search's line <paramref name="line"/> on <paramref name="window"/> found (see the top of the file).</summary>
    private void OpenKarpo(DockWindow window, WinSearchLine line, KarpoTarget target)
    {
        switch (target.Kind)
        {
            case KarpoTargetKind.Settings:
                _ = OpenEditLanding(window, new EditLanding(target.Page, target.Value));
                break;
            case KarpoTargetKind.Add:
                AddSearching(window, target.Value ?? "");
                break;
            case KarpoTargetKind.Layout when int.TryParse(target.Value, out var layout) && layout >= 0 && layout < config.Layouts.Count:
                SwitchLayout(window, layout);
                break;
            case KarpoTargetKind.Note when target.Value is { } note:
                if (NoteFolder(window, Islands.OwnerOf(line.Key), note) is { } part)
                {
                    Later(() => ShowFolder(window, Hit.Cell(part, 0)));
                }
                else
                {
                    AddSearching(window, WidgetInfos.Note.Title);
                }
                break;
        }
    }

    /// <summary>§652: the add page open on <paramref name="window"/>'s display, its search saying <paramref name="words"/>.</summary>
    private void AddSearching(DockWindow window, string words)
    {
        if (OpenEditLanding(window, new EditLanding(PanelPage.Add)) is { } display && chromes.TryGetValue(display, out var chrome))
        {
            chrome.Deck.Search(words);
        }
    }

    /// <summary>
    /// §652: the place on <paramref name="window"/>'s bar of the folder (of the list of <paramref name="owner"/>) holding a note
    /// line that shows note <paramref name="id"/> — a line set to none shows the notebook's first —; null where none does.
    /// </summary>
    private int? NoteFolder(DockWindow window, string? owner, string id)
    {
        if (owner is null)
        {
            return null;
        }
        var first = Notebook.Shared.Current.Notes.FirstOrDefault()?.Id;
        foreach (var island in config.ListFor(owner))
        {
            var shows = island.Folder?.Cells.Any(cell => cell?.Island is { Kind: var kind } inside && kind == WidgetInfos.Note.Id
                && (inside.Note?.Note is { Length: > 0 } own ? own == id : first == id)) == true;
            if (shows && window.Layout.PartIndex(Config.IslandKey(owner, island.Id)) is var part and >= 0)
            {
                return part;
            }
        }
        return null;
    }
}
